using System.Diagnostics;
using System.Security.Cryptography;
using AccessibleMediaController.Windows.Services;

internal static class AudioClipLongMp3Tests
{
    internal static void Run()
    {
        var ffmpeg = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new InvalidOperationException("Brak FFmpeg do próby długiego MP3.");
        var directory = Path.Combine(Path.GetTempPath(), "amc-long-mp3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var passed = false;
        try
        {
            var path = Path.Combine(directory, "radio.mp3");
            var start = TimeSpan.FromSeconds(514.7636666);
            var end = TimeSpan.FromSeconds(890.169414);
            var fullDuration = TimeSpan.FromSeconds(1000);
            RunFfmpeg(ffmpeg,
                "-y", "-f", "lavfi", "-i", "sine=frequency=220:duration=514.7636666:sample_rate=44100",
                "-f", "lavfi", "-i", "sine=frequency=660:duration=375.4057474:sample_rate=44100",
                "-f", "lavfi", "-i", "sine=frequency=1200:duration=109.830586:sample_rate=44100",
                "-filter_complex", "[0:a][1:a][2:a]concat=n=3:v=0:a=1[a]",
                "-map", "[a]", "-c:a", "libmp3lame", "-b:a", "128k", path);
            var sourceHash = Hash(path);
            var result = AudioClipOriginalEditor.RemoveAsync(
                    new AudioClipRemovalRequest(path, start, end, fullDuration),
                    progress: null,
                    CancellationToken.None,
                    keepBackup: true)
                .GetAwaiter().GetResult();
            var expectedDuration = fullDuration - (end - start);
            Check(File.Exists(path), "Po cięciu zniknęła pierwotna nazwa MP3.");
            Check(File.Exists(result.BackupPath), "Brak kopii oryginalnego MP3.");
            Check(Hash(result.BackupPath) == sourceHash, "Kopia różni się od oryginalnego MP3.");
            Check(Math.Abs((result.Duration - expectedDuration).TotalSeconds) < 0.4,
                $"Wynik ma {result.Duration:c}, oczekiwano około {expectedDuration:c}.");
            var beforeHz = FrequencyAt(ffmpeg, path, start.TotalSeconds - 1.2);
            var afterHz = FrequencyAt(ffmpeg, path, start.TotalSeconds + 1.2);
            Check(Math.Abs(beforeHz - 220) < 25,
                $"Przed cięciem jest {beforeHz:0} Hz zamiast 220 Hz.");
            Check(Math.Abs(afterHz - 1200) < 60,
                $"Po cięciu jest {afterHz:0} Hz zamiast 1200 Hz.");
            Console.WriteLine($"OK: długi MP3, {result.Duration:c}; przed {beforeHz:0} Hz, po {afterHz:0} Hz; kopia identyczna.");
            passed = true;
        }
        finally
        {
            if (passed)
            {
                var resolved = Path.GetFullPath(directory);
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
                Check(string.Equals(Path.GetDirectoryName(resolved), tempRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(resolved).StartsWith("amc-long-mp3-", StringComparison.Ordinal),
                    "Nieprawidłowy katalog tymczasowy; pliki pozostają do sprawdzenia.");
                Directory.Delete(resolved, recursive: true);
            }
            else Console.Error.WriteLine("Pliki diagnostyczne zachowano: " + directory);
        }
    }

    private static double FrequencyAt(string ffmpeg, string path, double seconds)
    {
        var info = StartInfo(ffmpeg,
            "-ss", seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            "-i", path, "-t", "0.5", "-ac", "1", "-ar", "44100",
            "-f", "f32le", "pipe:1");
        info.RedirectStandardOutput = true;
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Nie uruchomiono dekodera FFmpeg.");
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        process.WaitForExit();
        Check(process.ExitCode == 0, "Nie udało się zdekodować okna MP3.");
        var samples = bytes.ToArray();
        var frames = samples.Length / sizeof(float);
        Check(frames > 11025, $"Za mało próbek w oknie MP3: {frames}.");
        var crossings = 0;
        var previous = BitConverter.ToSingle(samples, 0);
        for (var index = 1; index < frames; index++)
        {
            var value = BitConverter.ToSingle(samples, index * sizeof(float));
            if ((previous < 0 && value >= 0) || (previous >= 0 && value < 0)) crossings++;
            previous = value;
        }
        return crossings / 2d / (frames / 44100d);
    }

    private static void RunFfmpeg(string ffmpeg, params string[] args)
    {
        using var process = Process.Start(StartInfo(ffmpeg, args))
            ?? throw new InvalidOperationException("Nie uruchomiono FFmpeg.");
        process.WaitForExit();
        Check(process.ExitCode == 0, $"FFmpeg zakończył się kodem {process.ExitCode}.");
    }

    private static ProcessStartInfo StartInfo(string ffmpeg, params string[] args)
    {
        var info = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = false,
        };
        foreach (var option in new[] { "-nostdin", "-hide_banner", "-loglevel", "error" })
            info.ArgumentList.Add(option);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
