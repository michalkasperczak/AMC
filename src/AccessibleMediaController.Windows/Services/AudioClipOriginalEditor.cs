using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AccessibleMediaController.Core.LocalMedia;

namespace AccessibleMediaController.Windows.Services;

internal sealed record AudioClipRemovalRequest(
    string SourcePath,
    TimeSpan Start,
    TimeSpan End,
    TimeSpan SourceDuration);

internal sealed record AudioClipRemovalResult(
    string BackupPath,
    TimeSpan Duration,
    int SampleRateHz);

/// <summary>
/// Removes one interval without decoding and encoding the audio again. Work is
/// produced beside the source, verified, and only then atomically replaces it.
/// </summary>
internal static class AudioClipOriginalEditor
{
    private static readonly TimeSpan FileReleaseTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(30);

    internal static bool IsAvailable => FfmpegRadioWaveProvider.FindExecutable() is not null;

    internal static async Task<AudioClipRemovalResult> RemoveAsync(
        AudioClipRemovalRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Validate(request);
        if (CloudFileAvailability.MayRequireRemoteAccess(request.SourcePath))
        {
            throw new InvalidOperationException(
                "Plik nie jest w pełni dostępny lokalnie. Pobierz go świadomie z chmury i spróbuj ponownie.");
        }
        if (LocalAudioFileDiscovery.IsVideoFile(request.SourcePath))
        {
            throw new NotSupportedException(
                "Usuwanie fragmentu z oryginału nie jest jeszcze dostępne dla plików wideo. "
                + "Klawisz X może zapisać ich ścieżkę audio do nowego pliku.");
        }

        var executable = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new NotSupportedException(
                "Usuwanie fragmentu z oryginalnego pliku wymaga składnika FFmpeg.");
        var sourcePath = Path.GetFullPath(request.SourcePath);
        var directory = Path.GetDirectoryName(sourcePath)
            ?? throw new ArgumentException("Plik źródłowy nie ma prawidłowego folderu.");
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            throw new NotSupportedException("Plik źródłowy nie ma rozpoznawalnego formatu.");
        if ((File.GetAttributes(sourcePath) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException("Plik źródłowy jest tylko do odczytu.");

        await WaitForExclusiveAccessAsync(sourcePath, FileReleaseTimeout, cancellationToken)
            .ConfigureAwait(false);
        // Some long CBR MP3 recordings have no reliable Xing header. Windows
        // then estimates their duration from byte size and nominal bitrate,
        // which can be several seconds shorter than the real packet timeline.
        // Use FFmpeg's packet scan both for preserving the true end of file and
        // for checking the edited output.
        var sourceTimelineDuration = await ReadPacketTimelineDurationAsync(
                executable,
                sourcePath,
                cancellationToken)
            .ConfigureAwait(false);
        var effectiveEnd = request.End > sourceTimelineDuration
            ? sourceTimelineDuration
            : request.End;
        if (effectiveEnd <= request.Start)
        {
            throw new ArgumentException("Początek i koniec fragmentu są poza rzeczywistą osią czasu pliku.");
        }

        var operationId = Guid.NewGuid().ToString("N");
        var prefix = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(sourcePath)}.amc-cut-{operationId}");
        var firstPart = prefix + "-before" + extension;
        var secondPart = prefix + "-after" + extension;
        var resultPath = prefix + "-result" + extension;
        var listPath = prefix + ".ffconcat";
        var backupPath = BuildBackupPath(sourcePath);
        var temporaryPaths = new[] { firstPart, secondPart, resultPath, listPath };

        try
        {
            var parts = new List<string>(2);
            if (request.Start > TimeSpan.FromMilliseconds(1))
            {
                await ExportPartAsync(
                    executable,
                    sourcePath,
                    firstPart,
                    TimeSpan.Zero,
                    request.Start,
                    0d,
                    0.4d,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                parts.Add(firstPart);
            }
            if (effectiveEnd < sourceTimelineDuration - TimeSpan.FromMilliseconds(1))
            {
                await ExportPartAsync(
                    executable,
                    sourcePath,
                    secondPart,
                    effectiveEnd,
                    sourceTimelineDuration - effectiveEnd,
                    parts.Count == 0 ? 0d : 0.4d,
                    parts.Count == 0 ? 0.8d : 0.4d,
                    progress,
                    cancellationToken,
                    readToEnd: true).ConfigureAwait(false);
                parts.Add(secondPart);
            }
            if (parts.Count == 0)
                throw new InvalidOperationException("Nie można usunąć całej zawartości pliku.");

            await File.WriteAllTextAsync(
                listPath,
                BuildConcatList(parts),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken).ConfigureAwait(false);
            await ConcatenateAsync(
                executable,
                sourcePath,
                listPath,
                resultPath,
                progress,
                cancellationToken).ConfigureAwait(false);

            var expectedDuration = sourceTimelineDuration - (effectiveEnd - request.Start);
            var metadata = await VerifyResultAsync(executable, resultPath, expectedDuration, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForExclusiveAccessAsync(sourcePath, FileReleaseTimeout, cancellationToken)
                .ConfigureAwait(false);
            ReplaceWithBackup(resultPath, sourcePath, backupPath);
            progress?.Report(1d);
            return new AudioClipRemovalResult(backupPath, metadata.Duration, metadata.SampleRateHz);
        }
        finally
        {
            await DeleteTemporaryFilesAsync(temporaryPaths).ConfigureAwait(false);
        }
    }

    internal static string BuildBackupPath(string sourcePath, DateTimeOffset? now = null)
    {
        var stamp = (now ?? DateTimeOffset.Now).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var candidate = $"{sourcePath}.{stamp}.amc-backup";
        for (var suffix = 2; File.Exists(candidate); suffix++)
        {
            candidate = $"{sourcePath}.{stamp}-{suffix}.amc-backup";
        }
        return candidate;
    }

    private static async Task ExportPartAsync(
        string executable,
        string sourcePath,
        string destinationPath,
        TimeSpan startAt,
        TimeSpan duration,
        double progressStart,
        double progressRange,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        bool readToEnd = false)
    {
        var start = CreateProcess(executable);
        AddArguments(start, "-nostdin", "-hide_banner", "-loglevel", "error", "-y");
        if (startAt > TimeSpan.Zero) AddArguments(start, "-ss", FfmpegTime(startAt));
        AddArguments(start, "-i", sourcePath);
        if (!readToEnd) AddArguments(start, "-t", FfmpegTime(duration));
        AddArguments(
            start,
            "-map", "0:a:0",
            "-vn",
            "-map_metadata", "0",
            "-c:a", "copy",
            "-avoid_negative_ts", "make_zero",
            "-progress", "pipe:1",
            "-nostats");
        if (Path.GetExtension(sourcePath).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            // Intermediate Xing/VBR headers describe only one part and become
            // misleading after concatenation. The final output receives the
            // single authoritative header instead.
            AddArguments(start, "-write_xing", "0", "-id3v2_version", "0");
        }
        start.ArgumentList.Add(destinationPath);
        await RunAsync(
            start,
            duration,
            value => progress?.Report(progressStart + progressRange * value),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task ConcatenateAsync(
        string executable,
        string sourcePath,
        string listPath,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var start = CreateProcess(executable);
        AddArguments(
            start,
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-f", "concat", "-safe", "0", "-i", listPath,
            "-i", sourcePath,
            "-map", "0:a:0",
            "-map", "1:v?",
            "-map", "1:t?",
            "-map_metadata", "1",
            "-c", "copy",
            "-avoid_negative_ts", "make_zero",
            "-progress", "pipe:1",
            "-nostats");
        if (Path.GetExtension(sourcePath).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            AddArguments(start, "-write_xing", "1");
        }
        start.ArgumentList.Add(destinationPath);
        await RunAsync(
            start,
            duration: null,
            value => progress?.Report(0.8d + 0.15d * value),
            cancellationToken).ConfigureAwait(false);
        progress?.Report(0.95d);
    }

    private static async Task RunAsync(
        ProcessStartInfo start,
        TimeSpan? duration,
        Action<double>? report,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić składnika edycji audio.");
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
            if (duration is not { } total || total <= TimeSpan.Zero) continue;
            var microseconds = ParseProgressMicroseconds(line);
            if (microseconds is null) continue;
            report?.Invoke(Math.Clamp(
                TimeSpan.FromTicks(microseconds.Value * 10).TotalSeconds / total.TotalSeconds,
                0d,
                1d));
        }
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(string.IsNullOrWhiteSpace(error)
                ? "Nie udało się przygotować pliku po usunięciu fragmentu. Oryginał nie został zmieniony."
                : $"Nie udało się usunąć fragmentu: {LastLine(error)}. Oryginał nie został zmieniony.");
        }
    }

    private static async Task<MediaMetadataReadResult> VerifyResultAsync(
        string executable,
        string path,
        TimeSpan expectedDuration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path) || new FileInfo(path).Length <= 0)
            throw new InvalidDataException("Plik wynikowy jest pusty. Oryginał nie został zmieniony.");
        var metadata = await WindowsMediaOutput.TryReadMetadataAsync(path, MetadataTimeout)
            .ConfigureAwait(false);
        if (!metadata.Success || metadata.Duration <= TimeSpan.Zero)
            throw new InvalidDataException("Nie można sprawdzić pliku wynikowego. Oryginał nie został zmieniony.");
        var packetTimelineDuration = await ReadPacketTimelineDurationAsync(
                executable,
                path,
                cancellationToken)
            .ConfigureAwait(false);
        if (!DurationMatches(expectedDuration, packetTimelineDuration))
        {
            throw new InvalidDataException(
                "Długość pliku wynikowego nie zgadza się z zaznaczeniem. Oryginał nie został zmieniony.");
        }
        DiagnosticLog.Info(
            "audio-clip",
            $"Zweryfikowano wynik cięcia: oczekiwano {expectedDuration:c}, "
            + $"oś czasu FFmpeg {packetTimelineDuration:c}, odczyt Windows {metadata.Duration:c}.");
        return metadata with { Duration = packetTimelineDuration };
    }

    internal static bool DurationMatches(TimeSpan expectedDuration, TimeSpan actualDuration)
    {
        var tolerance = TimeSpan.FromSeconds(
            Math.Max(2d, Math.Min(5d, expectedDuration.TotalSeconds * 0.02d)));
        return actualDuration > TimeSpan.Zero
            && (actualDuration - expectedDuration).Duration() <= tolerance;
    }

    private static async Task<TimeSpan> ReadPacketTimelineDurationAsync(
        string executable,
        string path,
        CancellationToken cancellationToken)
    {
        var start = CreateProcess(executable);
        AddArguments(
            start,
            "-nostdin", "-hide_banner", "-loglevel", "error",
            "-i", path,
            "-map", "0:a:0",
            "-vn",
            "-c:a", "copy",
            "-f", "null", "-",
            "-progress", "pipe:1",
            "-nostats");

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić kontroli pliku wynikowego.");
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

        long? lastMicroseconds = null;
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var microseconds = ParseProgressMicroseconds(line);
            if (microseconds.HasValue) lastMicroseconds = microseconds;
        }
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0 || lastMicroseconds is not > 0)
        {
            throw new InvalidDataException(string.IsNullOrWhiteSpace(error)
                ? "Nie można sprawdzić osi czasu pliku wynikowego. Oryginał nie został zmieniony."
                : $"Nie można sprawdzić osi czasu pliku wynikowego: {LastLine(error)}. "
                  + "Oryginał nie został zmieniony.");
        }
        return TimeSpan.FromTicks(lastMicroseconds.Value * 10);
    }

    private static async Task WaitForExclusiveAccessAsync(
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
                return;
            }
            catch (IOException) when (started.Elapsed < timeout)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void ReplaceWithBackup(string replacementPath, string sourcePath, string backupPath)
    {
        try
        {
            File.Replace(replacementPath, sourcePath, backupPath, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceWithRollback(replacementPath, sourcePath, backupPath);
        }
        catch (IOException) when (!File.Exists(backupPath) && File.Exists(sourcePath) && File.Exists(replacementPath))
        {
            ReplaceWithRollback(replacementPath, sourcePath, backupPath);
        }
    }

    private static void ReplaceWithRollback(string replacementPath, string sourcePath, string backupPath)
    {
        File.Move(sourcePath, backupPath);
        try
        {
            File.Move(replacementPath, sourcePath);
        }
        catch
        {
            if (!File.Exists(sourcePath) && File.Exists(backupPath)) File.Move(backupPath, sourcePath);
            throw;
        }
    }

    private static ProcessStartInfo CreateProcess(string executable) => new()
    {
        FileName = executable,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    private static string BuildConcatList(IEnumerable<string> paths) =>
        "ffconcat version 1.0\n" + string.Join(
            "\n",
            paths.Select(path => $"file '{EscapeConcatPath(path)}'")) + "\n";

    private static string EscapeConcatPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal);

    private static void Validate(AudioClipRemovalRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        if (!File.Exists(request.SourcePath))
            throw new FileNotFoundException("Nie znaleziono pliku źródłowego.", request.SourcePath);
        if (request.Start < TimeSpan.Zero
            || request.End <= request.Start
            || request.SourceDuration <= TimeSpan.Zero
            || request.End > request.SourceDuration)
        {
            throw new ArgumentException("Początek i koniec fragmentu są nieprawidłowe.");
        }
        if (request.Start <= TimeSpan.FromMilliseconds(1)
            && request.End >= request.SourceDuration - TimeSpan.FromMilliseconds(1))
        {
            throw new InvalidOperationException("Nie można usunąć całej zawartości pliku.");
        }
    }

    private static void AddArguments(ProcessStartInfo start, params string[] arguments)
    {
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
    }

    private static long? ParseProgressMicroseconds(string line)
    {
        const string prefix = "out_time_us=";
        const string legacyPrefix = "out_time_ms=";
        var value = line.StartsWith(prefix, StringComparison.Ordinal)
            ? line[prefix.Length..]
            : line.StartsWith(legacyPrefix, StringComparison.Ordinal)
                ? line[legacyPrefix.Length..]
                : string.Empty;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds)
            ? microseconds
            : null;
    }

    private static string FfmpegTime(TimeSpan value) =>
        value.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture);

    private static string LastLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault()
        ?? "nieznany błąd";

    private static async Task DeleteTemporaryFilesAsync(IEnumerable<string> paths)
    {
        var pending = paths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var attempt = 1; pending.Count > 0 && attempt <= 12; attempt++)
        {
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (File.Exists(pending[index])) File.Delete(pending[index]);
                    pending.RemoveAt(index);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            if (pending.Count == 0) break;
            await Task.Delay(Math.Min(100 * attempt, 500)).ConfigureAwait(false);
        }

        foreach (var path in pending)
        {
            DiagnosticLog.Warning(
                "audio-clip",
                $"Nie udało się usunąć technicznego pliku po cięciu: {path}. "
                + "Plik nie zostanie pokazany w Bibliotece.");
        }
    }
}
