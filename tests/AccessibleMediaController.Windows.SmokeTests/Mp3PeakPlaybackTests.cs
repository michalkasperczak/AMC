using System.Diagnostics;
using System.Runtime.InteropServices;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

internal static class Mp3PeakPlaybackTests
{
    internal static void Run()
    {
        var ffmpeg = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new InvalidOperationException("Test MP3 wymaga FFmpeg.");
        var root = Path.Combine(Path.GetTempPath(), "amc-mp3-peaks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "loud.mp3");
            using (var process = Process.Start(new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList =
                {
                    "-y", "-hide_banner", "-loglevel", "error",
                    "-f", "lavfi", "-i",
                    "aevalsrc=1.5*sin(2*PI*1000*t)|1.5*sin(2*PI*1000*t):s=48000:d=2",
                    "-ac", "2", "-c:a", "libmp3lame", "-b:a", "320k", path
                }
            }))
            {
                process?.WaitForExit(TimeSpan.FromSeconds(15));
                Assert(process is { ExitCode: 0 }, "Nie udało się przygotować głośnego MP3.");
            }

            Assert(FfmpegLocalAudioWaveStream.TryOpen(path, out var raw),
                "FFmpeg nie otworzył kontrolnej próbki MP3.");
            using (raw)
            {
                var rawPeak = ReadPeak(raw);
                Assert(rawPeak > 1.1f,
                    $"Próbka kontrolna nie ma szczytów ponad 0 dBFS: {rawPeak}.");
            }

            using var playbackReader = WindowsMediaOutput.OpenReaderForExport(path);
            Assert(playbackReader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat,
                "Odtwarzacz nie zachował formatu float dla lokalnego MP3.");
            var limitedPeak = ReadPeak(playbackReader);
            Assert(limitedPeak > 0.75f && limitedPeak <= 0.981f,
                $"Nieprawidłowy szczyt po limiterze MP3: {limitedPeak}.");
            playbackReader.CurrentTime = TimeSpan.FromSeconds(1);
            Assert(ReadPeak(playbackReader) > 0.1f,
                "MP3 przestał dostarczać dźwięk po przewinięciu.");

            var deviceFailure = new COMException(
                "Urządzenie odłączone", unchecked((int)0x88890004));
            Assert(WindowsMediaOutput.IsOutputDeviceInvalidation(deviceFailure),
                "Nie rozpoznano odłączonego wyjścia WASAPI.");
            Assert(!WindowsMediaOutput.IsDecoderFailure(deviceFailure),
                "Błąd urządzenia trafił do odzyskiwania dekodera MP3.");
            Assert(WindowsMediaOutput.IsDecoderFailure(
                    new COMException("Błąd dekodera", unchecked((int)0xC00D36B4))),
                "Prawdziwy błąd dekodera utracił ścieżkę odzyskiwania.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static float ReadPeak(WaveStream stream)
    {
        var bytes = new byte[32_768];
        var peak = 0f;
        int read;
        while ((read = stream.Read(bytes, 0, bytes.Length)) > 0)
        {
            for (var index = 0; index + sizeof(float) <= read; index += sizeof(float))
            {
                var sample = BitConverter.ToSingle(bytes, index);
                Assert(float.IsFinite(sample), "Dekoder MP3 zwrócił niefinitywną próbkę.");
                peak = Math.Max(peak, Math.Abs(sample));
            }
        }
        return peak;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
