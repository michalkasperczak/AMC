using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AccessibleMediaController.Core.LocalMedia;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

internal sealed record AudioClipAppendRequest(
    string SourcePath,
    string TargetPath,
    TimeSpan Start,
    TimeSpan End);

internal sealed record AudioClipAppendResult(
    string BackupPath,
    TimeSpan TargetDurationBefore,
    TimeSpan AppendedDuration,
    TimeSpan TargetDurationAfter,
    bool TargetWasReencoded,
    string? ReencodeWarning);

/// <summary>
/// Appends a selected range of one file after the whole content of another,
/// already existing file. The clip is produced by the shared export service,
/// the new content is built and verified beside the target, and only then the
/// target is backed up under a unique name and atomically replaced.
/// </summary>
internal static class AudioClipAppender
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DestinationLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan FileReleaseTimeout = TimeSpan.FromSeconds(15);

    internal static bool SupportsTarget(string targetPath)
    {
        try { _ = EncoderArguments(Path.GetExtension(targetPath)); }
        catch (NotSupportedException) { return false; }
        return IsWave(targetPath) || FfmpegRadioWaveProvider.FindExecutable() is not null;
    }

    internal static async Task<AudioClipAppendResult> AppendAsync(
        AudioClipAppendRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var sourcePath = Validate(request);
        var targetPath = Path.GetFullPath(request.TargetPath);
        var gate = DestinationLocks.GetOrAdd(targetPath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AppendCoreAsync(request, sourcePath, targetPath, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<AudioClipAppendResult> AppendCoreAsync(
        AudioClipAppendRequest request,
        string sourcePath,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("Plik docelowy nie ma prawidłowego folderu.");
        var extension = Path.GetExtension(targetPath);
        if (string.IsNullOrWhiteSpace(extension))
            throw new NotSupportedException("Plik docelowy nie ma rozpoznawalnego formatu.");
        if ((File.GetAttributes(targetPath) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException("Plik docelowy jest tylko do odczytu.");
        await WaitForExclusiveAccessAsync(targetPath, cancellationToken).ConfigureAwait(false);

        var identityBefore = await ReadIdentityAsync(targetPath, cancellationToken).ConfigureAwait(false);
        var operationId = Guid.NewGuid().ToString("N");
        var prefix = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(targetPath)}.amc-append-{operationId}");
        var clipPath = prefix + "-clip" + (IsWave(targetPath) ? ".wav" : extension);
        var resultPath = prefix + "-result" + extension;
        var listPath = prefix + ".ffconcat";
        var temporaryPaths = new[] { clipPath, resultPath, listPath };

        try
        {
            var reencoded = false;
            string? warning = null;
            TimeSpan targetBefore;
            TimeSpan appended;

            if (IsWave(targetPath))
            {
                await ExportClipAsync(
                    request,
                    clipPath,
                    AudioClipExportFormat.Wav,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                (targetBefore, appended, warning) = BuildWaveResult(
                    targetPath,
                    clipPath,
                    resultPath,
                    cancellationToken);
            }
            else
            {
                var executable = FfmpegRadioWaveProvider.FindExecutable()
                    ?? throw new NotSupportedException(
                        "Dołączenie fragmentu do tego formatu wymaga składnika FFmpeg. "
                        + "Plik docelowy w formacie WAV działa bez niego.");
                targetBefore = await ReadTimelineDurationAsync(executable, targetPath, cancellationToken)
                    .ConfigureAwait(false);
                appended = await EncodeClipForTargetAsync(
                    executable,
                    request,
                    sourcePath,
                    targetPath,
                    clipPath,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                await ConcatenateAsync(executable, targetPath, clipPath, listPath, resultPath, cancellationToken)
                    .ConfigureAwait(false);
                reencoded = true;
                warning = IsLossy(extension)
                    ? "Cały plik został zakodowany ponownie w stratnym formacie, "
                      + "więc jakość dotychczasowej treści może być nieco niższa niż wcześniej. "
                      + "Poprzednia wersja jest w kopii zapasowej."
                    : "Plik został zapisany ponownie w tym samym, bezstratnym formacie. "
                      + "Poprzednia wersja jest w kopii zapasowej.";
            }

            cancellationToken.ThrowIfCancellationRequested();
            var expected = targetBefore + appended;
            var actual = await VerifyResultAsync(resultPath, expected, cancellationToken).ConfigureAwait(false);

            await WaitForExclusiveAccessAsync(targetPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(await ReadIdentityAsync(targetPath, cancellationToken).ConfigureAwait(false), identityBefore, StringComparison.Ordinal))
            {
                throw new IOException(
                    "Plik docelowy zmienił się w trakcie pracy, więc nie został zmieniony. "
                    + "Sprawdź jego zawartość i spróbuj ponownie.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var backupPath = AudioClipOriginalEditor.BuildBackupPath(targetPath);
            ReplaceWithBackup(resultPath, targetPath, backupPath);
            progress?.Report(1d);
            DiagnosticLog.Info(
                "audio-clip",
                $"Dołączono {appended:c} na koniec pliku {Path.GetFileName(targetPath)} "
                + $"({targetBefore:c} -> {actual:c}). Kopia: {Path.GetFileName(backupPath)}.");
            return new AudioClipAppendResult(backupPath, targetBefore, appended, actual, reencoded, warning);
        }
        finally
        {
            await DeleteTemporaryFilesAsync(temporaryPaths).ConfigureAwait(false);
        }
    }

    private sealed class ScaledProgress(IProgress<double> target, double scale) : IProgress<double>
    {
        // Caller owns dispatching (e.g. Progress<T> on UI). Do not post a second
        // asynchronous callback that could outlive the operation it describes.
        public void Report(double value) => target.Report(scale * value);
    }

    private static async Task ExportClipAsync(
        AudioClipAppendRequest request,
        string clipPath,
        AudioClipExportFormat format,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        await AudioClipExporter.ExportAsync(
            new AudioClipExportRequest(request.SourcePath, clipPath, request.Start, request.End, format),
            progress is null ? null : new ScaledProgress(progress, 0.7d),
            cancellationToken).ConfigureAwait(false);

    private static (TimeSpan TargetBefore, TimeSpan Appended, string? Warning) BuildWaveResult(
        string targetPath,
        string clipPath,
        string resultPath,
        CancellationToken cancellationToken)
    {
        using var target = new WaveFileReader(targetPath);
        using var clip = new WaveFileReader(clipPath);
        var targetBefore = target.TotalTime;
        var appended = clip.TotalTime;
        string? warning = null;
        IWaveProvider clipContent = clip;
        if (!clip.WaveFormat.Equals(target.WaveFormat))
        {
            warning = "Dołączany fragment został przeliczony do parametrów pliku docelowego "
                + $"({target.WaveFormat.SampleRate} Hz, {target.WaveFormat.Channels} kan.).";
            clipContent = new MediaFoundationResampler(clip, target.WaveFormat) { ResamplerQuality = 60 };
        }

        try
        {
            using var writer = new WaveFileWriter(resultPath, target.WaveFormat);
            Copy(target, writer, cancellationToken);
            Copy(clipContent, writer, cancellationToken);
        }
        finally
        {
            if (clipContent is IDisposable disposable && !ReferenceEquals(clipContent, clip)) disposable.Dispose();
        }

        return (targetBefore, appended, warning);
    }

    private static void Copy(IWaveProvider content, WaveFileWriter writer, CancellationToken cancellationToken)
    {
        var buffer = new byte[128 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = content.Read(buffer, 0, buffer.Length);
            if (read <= 0) return;
            writer.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// Encodes one range of a file into the container format implied by the
    /// destination extension. Exposed for tests that need real material in each
    /// supported format without a pre-existing target to match.
    /// </summary>
    internal static async Task EncodeRangeAsync(
        string sourcePath,
        string destinationPath,
        TimeSpan start,
        TimeSpan end,
        CancellationToken cancellationToken)
    {
        var executable = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new NotSupportedException("Ten format wymaga składnika FFmpeg.");
        await EncodeRangeCoreAsync(
            executable,
            sourcePath,
            destinationPath,
            start,
            end,
            shape: null,
            progress: null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TimeSpan> EncodeClipForTargetAsync(
        string executable,
        AudioClipAppendRequest request,
        string sourcePath,
        string targetPath,
        string clipPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var shape = await ReadStreamShapeAsync(executable, targetPath, cancellationToken).ConfigureAwait(false);
        await EncodeRangeCoreAsync(
            executable,
            sourcePath,
            clipPath,
            request.Start,
            request.End,
            shape,
            progress,
            cancellationToken).ConfigureAwait(false);
        return await ReadTimelineDurationAsync(executable, clipPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EncodeRangeCoreAsync(
        string executable,
        string sourcePath,
        string destinationPath,
        TimeSpan rangeStart,
        TimeSpan rangeEnd,
        (int SampleRateHz, int Channels)? shape,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var duration = rangeEnd - rangeStart;
        var start = CreateProcess(executable);
        AddArguments(start, "-nostdin", "-hide_banner", "-loglevel", "error", "-y");
        AddArguments(start, "-ss", FfmpegTime(rangeStart), "-i", sourcePath);
        AddArguments(start, "-t", FfmpegTime(duration), "-map", "0:a:0", "-vn");
        foreach (var argument in EncoderArguments(Path.GetExtension(destinationPath)))
        {
            start.ArgumentList.Add(argument);
        }
        if (shape is { } value)
        {
            AddArguments(
                start,
                "-ar", value.SampleRateHz.ToString(CultureInfo.InvariantCulture),
                "-ac", value.Channels.ToString(CultureInfo.InvariantCulture));
        }
        // Naglowek Xing MUSI tu zostac: bez niego dlugosc pliku MP3 jest tylko
        // szacowana z rozmiaru i bitrate'u, a wtedy ani ten kod, ani Windows nie
        // potrafia wiarygodnie zmierzyc wyniku. Czesci nie sa juz sklejane
        // kopiowaniem pakietow, wiec naglowek nie wprowadza w blad.
        AddArguments(start, "-progress", "pipe:1", "-nostats");
        start.ArgumentList.Add(destinationPath);
        await RunAsync(
            start,
            duration,
            value => progress?.Report(0.7d * value),
            "Nie udało się przygotować dołączanego fragmentu. Plik docelowy nie został zmieniony.",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task ConcatenateAsync(
        string executable,
        string targetPath,
        string clipPath,
        string listPath,
        string resultPath,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(
            listPath,
            "ffconcat version 1.0\n"
            + $"file '{EscapeConcatPath(targetPath)}'\n"
            + $"file '{EscapeConcatPath(clipPath)}'\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);
        // Kopiowanie pakietow przez demukser concat gubilo pierwsza czesc w
        // kontenerach z wlasnym naglowkiem strumienia (FLAC, OGG), wiec obie
        // czesci sa dekodowane i kodowane razem filtrem concat. Dla formatow
        // stratnych oznacza to ponowne kodowanie CALEGO pliku - wynik zglasza
        // to jawnie, zeby interfejs mogl ostrzec uzytkownika.
        var start = CreateProcess(executable);
        AddArguments(
            start,
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-i", targetPath,
            "-i", clipPath,
            "-filter_complex", "[0:a:0][1:a:0]concat=n=2:v=0:a=1[out]",
            "-map", "[out]", "-vn");
        foreach (var argument in EncoderArguments(Path.GetExtension(targetPath)))
        {
            start.ArgumentList.Add(argument);
        }
        AddArguments(start, "-progress", "pipe:1", "-nostats");
        if (Path.GetExtension(targetPath).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            AddArguments(start, "-write_xing", "1");
        }
        _ = listPath;
        start.ArgumentList.Add(resultPath);
        await RunAsync(
            start,
            duration: null,
            report: null,
            "Nie udało się dołączyć fragmentu na koniec pliku. Plik docelowy nie został zmieniony.",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<TimeSpan> VerifyResultAsync(
        string resultPath,
        TimeSpan expected,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(resultPath) || new FileInfo(resultPath).Length <= 0)
        {
            throw new InvalidDataException(
                "Plik z dołączonym fragmentem jest pusty. Plik docelowy nie został zmieniony.");
        }
        var actual = IsWave(resultPath)
            ? WaveDuration(resultPath)
            : await ReadTimelineDurationAsync(
                    FfmpegRadioWaveProvider.FindExecutable()!,
                    resultPath,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!AudioClipOriginalEditor.DurationMatches(expected, actual))
        {
            throw new InvalidDataException(
                $"Długość pliku po dołączeniu ({actual:c}) nie zgadza się z oczekiwaną ({expected:c}). "
                + "Plik docelowy nie został zmieniony.");
        }
        return actual;
    }

    private static TimeSpan WaveDuration(string path)
    {
        using var reader = new WaveFileReader(path);
        return reader.TotalTime;
    }

    /// <summary>
    /// Reads sample rate and channel count of the first audio stream. The concat
    /// demuxer can only copy streams with identical parameters, so the appended
    /// clip must be encoded into exactly this shape.
    /// </summary>
    private static async Task<(int SampleRateHz, int Channels)> ReadStreamShapeAsync(
        string executable,
        string path,
        CancellationToken cancellationToken)
    {
        var start = CreateProcess(executable);
        AddArguments(start, "-nostdin", "-hide_banner", "-i", path);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się odczytać parametrów pliku docelowego.");
        var description = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var sampleRate = 0;
        var channels = 0;
        foreach (var line in description.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("Audio:", StringComparison.Ordinal)) continue;
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (part.EndsWith(" Hz", StringComparison.Ordinal)
                    && int.TryParse(
                        part[..^3],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedRate))
                {
                    sampleRate = parsedRate;
                }
                else if (part.Equals("mono", StringComparison.Ordinal))
                {
                    channels = 1;
                }
                else if (part.Equals("stereo", StringComparison.Ordinal))
                {
                    channels = 2;
                }
                else if (part.EndsWith(" channels", StringComparison.Ordinal)
                    && int.TryParse(
                        part[..^" channels".Length],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var parsedChannels))
                {
                    channels = parsedChannels;
                }
            }
            if (sampleRate > 0 && channels > 0) break;
        }

        if (sampleRate <= 0 || channels <= 0)
        {
            throw new InvalidDataException(
                "Nie można odczytać parametrów dźwięku pliku docelowego. Plik nie został zmieniony.");
        }
        return (sampleRate, channels);
    }

    private static async Task<TimeSpan> ReadTimelineDurationAsync(
        string executable,
        string path,
        CancellationToken cancellationToken)
    {
        var start = CreateProcess(executable);
        // Dekodowanie, nie kopiowanie pakietow: po zlaczeniu kontenera znaczniki
        // czasu drugiej czesci zaczynaja sie od zera, wiec kopia pokazywalaby
        // tylko dlugosc ostatniej czesci. Pelne dekodowanie liczy prawdziwe probki.
        AddArguments(
            start,
            "-nostdin", "-hide_banner", "-loglevel", "error",
            "-i", path, "-map", "0:a:0", "-vn",
            "-f", "null", "-", "-progress", "pipe:1", "-nostats");
        long? last = null;
        await RunAsync(
            start,
            duration: null,
            report: null,
            "Nie można sprawdzić osi czasu pliku. Plik docelowy nie został zmieniony.",
            cancellationToken,
            line =>
            {
                var microseconds = ParseProgressMicroseconds(line);
                if (microseconds.HasValue) last = microseconds;
            }).ConfigureAwait(false);
        if (last is not > 0)
        {
            throw new InvalidDataException(
                "Nie można sprawdzić osi czasu pliku. Plik docelowy nie został zmieniony.");
        }
        return TimeSpan.FromTicks(last.Value * 10);
    }

    private static async Task RunAsync(
        ProcessStartInfo start,
        TimeSpan? duration,
        Action<double>? report,
        string failureMessage,
        CancellationToken cancellationToken,
        Action<string>? onLine = null)
    {
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić składnika dołączania audio.");
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
            onLine?.Invoke(line);
            if (report is null || duration is not { } total || total <= TimeSpan.Zero) continue;
            var microseconds = ParseProgressMicroseconds(line);
            if (microseconds is null) continue;
            report(Math.Clamp(
                TimeSpan.FromTicks(microseconds.Value * 10).TotalSeconds / total.TotalSeconds,
                0d,
                1d));
        }
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(string.IsNullOrWhiteSpace(error)
                ? failureMessage
                : $"{failureMessage} Szczegóły: {LastLine(error)}");
        }
    }

    private static IEnumerable<string> EncoderArguments(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".flac" => ["-c:a", "flac", "-compression_level", "5"],
            ".mp3" => ["-c:a", "libmp3lame", "-q:a", "2"],
            ".m4a" or ".aac" => ["-c:a", "aac", "-b:a", "192k"],
            ".ogg" or ".oga" => ["-c:a", "libvorbis", "-q:a", "6"],
            ".opus" => ["-c:a", "libopus", "-b:a", "128k"],
            ".wav" => ["-c:a", "pcm_s16le"],
            _ => throw new NotSupportedException(
                $"Dołączanie na koniec pliku {extension} nie jest jeszcze obsługiwane.")
        };

    private static bool IsLossy(string extension) =>
        extension.ToLowerInvariant() is ".mp3" or ".m4a" or ".aac" or ".ogg" or ".oga" or ".opus";

    private static bool IsWave(string path) =>
        Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ReadIdentityAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static string Validate(AudioClipAppendRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetPath);
        if (!File.Exists(request.SourcePath))
            throw new FileNotFoundException("Nie znaleziono pliku źródłowego.", request.SourcePath);
        if (!File.Exists(request.TargetPath))
        {
            throw new FileNotFoundException(
                "Nie znaleziono pliku, do którego fragment miał zostać dołączony.",
                request.TargetPath);
        }
        var sourcePath = Path.GetFullPath(request.SourcePath);
        if (string.Equals(sourcePath, Path.GetFullPath(request.TargetPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Fragmentu nie można dołączyć do tego samego pliku, z którego pochodzi.");
        }
        _ = EncoderArguments(Path.GetExtension(request.TargetPath));
        if (request.Start < TimeSpan.Zero || request.End <= request.Start)
            throw new ArgumentException("Początek i koniec fragmentu są nieprawidłowe.");
        if (CloudFileAvailability.MayRequireRemoteAccess(request.TargetPath)
            || CloudFileAvailability.MayRequireRemoteAccess(sourcePath))
        {
            throw new InvalidOperationException(
                "Plik nie jest w pełni dostępny lokalnie. Pobierz go świadomie z chmury i spróbuj ponownie.");
        }
        return sourcePath;
    }

    private static async Task WaitForExclusiveAccessAsync(string path, CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, bufferSize: 1, FileOptions.None);
                return;
            }
            catch (IOException) when (started.Elapsed < FileReleaseTimeout)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                throw new IOException(
                    "Plik docelowy jest teraz używany przez inny program. "
                    + "Zamknij go i spróbuj ponownie. Plik nie został zmieniony.");
            }
        }
    }

    private static void ReplaceWithBackup(string replacementPath, string targetPath, string backupPath)
    {
        try
        {
            File.Replace(replacementPath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceWithRollback(replacementPath, targetPath, backupPath);
        }
        catch (IOException) when (!File.Exists(backupPath) && File.Exists(targetPath) && File.Exists(replacementPath))
        {
            ReplaceWithRollback(replacementPath, targetPath, backupPath);
        }
    }

    private static void ReplaceWithRollback(string replacementPath, string targetPath, string backupPath)
    {
        File.Move(targetPath, backupPath);
        try
        {
            File.Move(replacementPath, targetPath);
        }
        catch
        {
            if (!File.Exists(targetPath) && File.Exists(backupPath)) File.Move(backupPath, targetPath);
            throw;
        }
    }

    private static ProcessStartInfo CreateProcess(string executable)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        ExternalToolProcess.ApplySafeEnvironment(start, executable);
        return start;
    }

    private static void AddArguments(ProcessStartInfo start, params string[] arguments)
    {
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
    }

    private static string EscapeConcatPath(string path) =>
        Path.GetFullPath(path).Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal);

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
        var pending = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
                $"Nie udało się usunąć technicznego pliku po dołączaniu fragmentu: {path}.");
        }
    }
}
