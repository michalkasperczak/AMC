using System.Diagnostics;
using System.Globalization;
using System.IO;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

internal enum AudioClipExportFormat
{
    OriginalStream,
    Flac,
    Wav
}

internal sealed record AudioClipExportRequest(
    string SourcePath,
    string DestinationPath,
    TimeSpan Start,
    TimeSpan End,
    AudioClipExportFormat Format);

internal static class AudioClipExporter
{
    internal static bool IsFfmpegAvailable => FfmpegRadioWaveProvider.FindExecutable() is not null;

    internal static async Task ExportAsync(
        AudioClipExportRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var destinationDirectory = Path.GetDirectoryName(request.DestinationPath)
            ?? throw new ArgumentException("Plik docelowy nie ma prawidłowego folderu.");
        Directory.CreateDirectory(destinationDirectory);
        var extension = Path.GetExtension(request.DestinationPath);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileNameWithoutExtension(request.DestinationPath)}.amc-{Guid.NewGuid():N}{extension}");

        try
        {
            if (request.Format == AudioClipExportFormat.Wav)
            {
                await Task.Run(
                    () => ExportWav(request, temporaryPath, progress, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ExportWithFfmpegAsync(
                    request,
                    temporaryPath,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, request.DestinationPath, overwrite: true);
            progress?.Report(1d);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // The partial file is hidden and uniquely named. A later cleanup
                // can remove it if an external decoder still releases its handle.
            }
        }
    }

    internal static string SuggestedExtension(string sourcePath, AudioClipExportFormat format)
    {
        if (format == AudioClipExportFormat.Flac) return ".flac";
        if (format == AudioClipExportFormat.Wav) return ".wav";
        return Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" or ".mov" or ".aac" => ".m4a",
            ".mkv" or ".webm" or ".ts" or ".mts" or ".m2ts" => ".mka",
            ".oga" => ".ogg",
            { Length: > 1 } value => value,
            _ => ".mka"
        };
    }

    private static void ExportWav(
        AudioClipExportRequest request,
        string temporaryPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var reader = WindowsMediaOutput.OpenReaderForExport(request.SourcePath);
        var end = request.End > reader.TotalTime && reader.TotalTime > TimeSpan.Zero
            ? reader.TotalTime
            : request.End;
        if (end <= request.Start)
            throw new InvalidDataException("Zaznaczony fragment wykracza poza dostępny czas pliku.");

        reader.CurrentTime = request.Start;
        var requestedBytes = checked((long)Math.Ceiling(
            (end - request.Start).TotalSeconds * reader.WaveFormat.AverageBytesPerSecond));
        requestedBytes -= requestedBytes % reader.WaveFormat.BlockAlign;
        if (requestedBytes <= 0) throw new InvalidDataException("Zaznaczony fragment jest zbyt krótki.");

        using var writer = new WaveFileWriter(temporaryPath, reader.WaveFormat);
        var buffer = new byte[128 * 1024];
        long written = 0;
        while (written < requestedBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = requestedBytes - written;
            var wanted = (int)Math.Min(buffer.Length, remaining);
            wanted -= wanted % reader.WaveFormat.BlockAlign;
            if (wanted <= 0) break;
            var read = reader.Read(buffer, 0, wanted);
            if (read <= 0) break;
            writer.Write(buffer, 0, read);
            written += read;
            progress?.Report(Math.Clamp(written / (double)requestedBytes, 0d, 1d));
        }
        if (written <= 0) throw new InvalidDataException("Dekoder nie zwrócił dźwięku z zaznaczonego fragmentu.");
    }

    private static async Task ExportWithFfmpegAsync(
        AudioClipExportRequest request,
        string temporaryPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var executable = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new NotSupportedException(
                "Ten sposób zapisu wymaga składnika FFmpeg. Możesz wybrać WAV, który działa bez niego.");
        var duration = request.End - request.Start;
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        AddArguments(start, "-nostdin", "-hide_banner", "-loglevel", "error", "-y");
        if (request.Format == AudioClipExportFormat.OriginalStream)
        {
            AddArguments(start, "-ss", FfmpegTime(request.Start));
        }
        AddArguments(start, "-i", request.SourcePath);
        if (request.Format != AudioClipExportFormat.OriginalStream)
        {
            AddArguments(start, "-ss", FfmpegTime(request.Start));
        }
        AddArguments(start,
            "-t", FfmpegTime(duration),
            "-map", "0:a:0", "-vn",
            "-progress", "pipe:1", "-nostats");
        if (request.Format == AudioClipExportFormat.OriginalStream)
        {
            AddArguments(start, "-c:a", "copy", "-avoid_negative_ts", "make_zero");
        }
        else
        {
            AddArguments(start, "-c:a", "flac", "-compression_level", "5");
        }
        start.ArgumentList.Add(temporaryPath);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić składnika eksportu audio.");
        using var registration = cancellationToken.Register(
            static state =>
            {
                try
                {
                    var running = (Process)state!;
                    if (!running.HasExited) running.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                }
            },
            process);

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            const string microsecondsPrefix = "out_time_us=";
            const string legacyMicrosecondsPrefix = "out_time_ms=";
            var value = line.StartsWith(microsecondsPrefix, StringComparison.Ordinal)
                ? line[microsecondsPrefix.Length..]
                : line.StartsWith(legacyMicrosecondsPrefix, StringComparison.Ordinal)
                    ? line[legacyMicrosecondsPrefix.Length..]
                    : string.Empty;
            if (value.Length == 0
                || !long.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var microseconds))
            {
                continue;
            }
            progress?.Report(Math.Clamp(
                TimeSpan.FromTicks(microseconds * 10).TotalSeconds / duration.TotalSeconds,
                0d,
                1d));
        }
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(string.IsNullOrWhiteSpace(error)
                ? "Nie udało się zapisać zaznaczonego fragmentu."
                : $"Nie udało się zapisać fragmentu: {LastLine(error)}");
        }
    }

    private static void Validate(AudioClipExportRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);
        if (!File.Exists(request.SourcePath))
            throw new FileNotFoundException("Nie znaleziono pliku źródłowego.", request.SourcePath);
        if (string.Equals(
                Path.GetFullPath(request.SourcePath),
                Path.GetFullPath(request.DestinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Plik wynikowy nie może zastąpić pliku źródłowego.");
        }
        if (request.Start < TimeSpan.Zero || request.End <= request.Start)
            throw new ArgumentException("Początek i koniec fragmentu są nieprawidłowe.");
    }

    private static void AddArguments(ProcessStartInfo start, params string[] arguments)
    {
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
    }

    private static string FfmpegTime(TimeSpan value) =>
        value.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture);

    private static string LastLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()
        ?? "nieznany błąd";
}
