using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Downloads one public YouTube item as MP3 without browser cookies or account
/// access. Conversion is staged outside the destination library so neither a
/// library watcher nor a cloud-sync client can lock yt-dlp working files.
/// </summary>
internal static class YouTubeMediaDownloader
{
    private const int PublishRetryCount = 20;
    private static readonly HashSet<string> KnownVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".webm", ".mkv", ".mov", ".avi", ".ogv", ".ts"
    };

    internal static async Task<PodcastDownloadResult> DownloadMp3Async(
        string pageUrl,
        string destinationPath,
        CancellationToken cancellationToken,
        bool overwrite)
    {
        if (!IsSupportedVideoUrl(pageUrl))
            throw new ArgumentException("Adres nie prowadzi do obsługiwanego filmu.", nameof(pageUrl));
        var ytDlp = YouTubeSourceResolver.FindExecutable()
            ?? throw new NotSupportedException(
                "Pobieranie filmu internetowego wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");
        var ffmpeg = FfmpegComponentManager.FindInstalledExecutable()
            ?? throw new NotSupportedException(
                "Pobieranie dźwięku z filmu wymaga składnika FFmpeg. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");

        var fullDestination = Path.GetFullPath(destinationPath);
        if (!Path.GetExtension(fullDestination).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            fullDestination = Path.ChangeExtension(fullDestination, ".mp3");
        if (!overwrite && File.Exists(fullDestination))
            throw new IOException("Plik o tej nazwie już istnieje.");
        var directory = Path.GetDirectoryName(fullDestination)
            ?? throw new InvalidDataException("Nie można ustalić folderu docelowego.");
        Directory.CreateDirectory(directory);
        var stagingDirectory = CreateStagingDirectory();
        var stagingBase = Path.Combine(stagingDirectory, "media");
        var outputTemplate = stagingBase + ".%(ext)s";
        var expectedStagingPath = stagingBase + ".mp3";

        var start = ExternalToolProcess.Create(ytDlp);
        foreach (var argument in new[]
        {
            "--ignore-config",
            "--no-playlist",
            "--no-warnings",
            "--force-ipv4",
            "--socket-timeout", "20",
            "--retries", "3",
            "--fragment-retries", "3",
            "--extractor-retries", "2",
            "--format", "bestaudio[ext=m4a]/bestaudio/best[acodec!=none]",
            "--extractor-args", "youtube:lang=pl",
            "--extract-audio",
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "--ffmpeg-location", ffmpeg,
            "--output", outputTemplate,
            overwrite ? "--force-overwrites" : "--no-overwrites",
            "--",
            pageUrl.Trim()
        })
        {
            start.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.");
            process.StandardInput.Close();
            using var termination = cancellationToken.Register(
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
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            _ = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(expectedStagingPath))
            {
                DiagnosticLog.Warning(
                    "youtube-download",
                    $"yt-dlp zakończył pobieranie kodem {process.ExitCode}; komunikat: {TrimDiagnostic(error)}");
                throw new InvalidDataException(
                    (YouTubeSourceResolver.IsYouTubeUrl(pageUrl)
                        ? YouTubeErrorTranslator.DescribePremiere(error)
                        : null)
                    ?? "Nie udało się pobrać dźwięku filmu. Sprawdź dostępność materiału i aktualność składnika yt-dlp.");
            }
            var length = new FileInfo(expectedStagingPath).Length;
            if (length <= 0) throw new InvalidDataException("Pobrany plik YouTube jest pusty.");
            await PublishCompletedFileAsync(
                expectedStagingPath,
                fullDestination,
                overwrite,
                cancellationToken).ConfigureAwait(false);
            return new PodcastDownloadResult(fullDestination, length);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            process?.Dispose();
            CleanupStagingDirectory(stagingDirectory);
        }
    }

    internal static async Task<PodcastDownloadResult> DownloadVideoAsync(
        string pageUrl,
        string destinationFolder,
        string? title,
        YouTubeVideoDownloadFormat format,
        CancellationToken cancellationToken,
        string? selectedFormat = null)
    {
        if (!IsSupportedVideoUrl(pageUrl))
            throw new ArgumentException("Adres nie prowadzi do obsługiwanego filmu.", nameof(pageUrl));
        if (!Enum.IsDefined(format))
            throw new ArgumentOutOfRangeException(nameof(format));
        var ytDlp = YouTubeSourceResolver.FindExecutable()
            ?? throw new NotSupportedException(
                "Pobieranie filmu internetowego wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");
        var ffmpeg = FfmpegComponentManager.FindInstalledExecutable()
            ?? throw new NotSupportedException(
                "Pobieranie wideo internetowego wymaga składnika FFmpeg. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");

        var fullDestinationFolder = Path.GetFullPath(destinationFolder);
        Directory.CreateDirectory(fullDestinationFolder);
        var stagingDirectory = CreateStagingDirectory();
        var outputTemplate = Path.Combine(stagingDirectory, "media.%(ext)s");
        var start = ExternalToolProcess.Create(ytDlp);
        foreach (var argument in BuildVideoArguments(outputTemplate, ffmpeg, format, pageUrl, selectedFormat))
            start.ArgumentList.Add(argument);

        Process? process = null;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.");
            process.StandardInput.Close();
            using var termination = cancellationToken.Register(
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
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            var completedPath = ResolveCompletedVideoPath(stagingDirectory, output);
            if (process.ExitCode != 0 || completedPath is null)
            {
                DiagnosticLog.Warning(
                    "youtube-video-download",
                    $"yt-dlp zakończył pobieranie wideo kodem {process.ExitCode}; komunikat: {TrimDiagnostic(error)}");
                throw new InvalidDataException(
                    (YouTubeSourceResolver.IsYouTubeUrl(pageUrl)
                        ? YouTubeErrorTranslator.DescribePremiere(error)
                        : null)
                    ?? "Nie udało się pobrać filmu. Sprawdź dostępność materiału i aktualność składnika yt-dlp.");
            }

            var extension = Path.GetExtension(completedPath).ToLowerInvariant();
            if (format == YouTubeVideoDownloadFormat.Mp4
                && !extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Nie udało się przygotować pliku wideo MP4.");
            }
            if (string.IsNullOrWhiteSpace(extension))
                throw new InvalidDataException("Pobrany plik wideo nie ma rozpoznawalnego formatu.");
            var length = new FileInfo(completedPath).Length;
            if (length <= 0) throw new InvalidDataException("Pobrany plik wideo jest pusty.");

            var destinationPath = PodcastDownloadNaming.UniquePath(
                fullDestinationFolder,
                PodcastDownloadNaming.SuggestedVideoBaseName(title) + extension);
            await PublishCompletedFileAsync(
                completedPath,
                destinationPath,
                overwrite: false,
                cancellationToken).ConfigureAwait(false);
            return new PodcastDownloadResult(destinationPath, length);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            process?.Dispose();
            CleanupStagingDirectory(stagingDirectory);
        }
    }

    internal static IReadOnlyList<string> BuildVideoArguments(
        string outputTemplate,
        string ffmpeg,
        YouTubeVideoDownloadFormat format,
        string pageUrl,
        string? selectedFormat = null)
    {
        var arguments = new List<string>
        {
            "--ignore-config",
            "--no-playlist",
            "--no-warnings",
            "--no-progress",
            "--force-ipv4",
            "--socket-timeout", "20",
            "--retries", "3",
            "--fragment-retries", "3",
            "--extractor-retries", "2",
            "--format", selectedFormat ?? "bv*+ba/b",
            "--extractor-args", "youtube:lang=pl"
        };
        if (selectedFormat is not null && selectedFormat.Contains('+'))
            arguments.AddRange(["--merge-output-format", "mkv"]);
        if (selectedFormat is null && format == YouTubeVideoDownloadFormat.Mp4)
        {
            arguments.AddRange([
                "--format-sort", "vcodec:h264,lang,quality,res,fps,hdr:12,acodec:aac",
                "--merge-output-format", "mp4",
                "--remux-video", "mp4"]);
        }
        arguments.AddRange([
            "--ffmpeg-location", ffmpeg,
            "--print", "after_move:filepath",
            "--output", outputTemplate,
            "--force-overwrites",
            "--",
            pageUrl.Trim()]);
        return arguments;
    }

    internal static bool IsSupportedVideoUrl(string? pageUrl) =>
        YouTubeSourceResolver.IsYouTubeUrl(pageUrl)
        || PublicSocialVideoResolver.IsVideoUrl(pageUrl);

    internal static string? ResolveCompletedVideoPath(
        string stagingDirectory,
        string standardOutput)
    {
        var fullStagingDirectory = Path.GetFullPath(stagingDirectory);
        foreach (var line in standardOutput.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            var candidate = line.Trim().Trim('"');
            if (IsCompletedStagingFile(fullStagingDirectory, candidate))
                return Path.GetFullPath(candidate);
        }
        return Directory.EnumerateFiles(
                fullStagingDirectory,
                "media.*",
                SearchOption.TopDirectoryOnly)
            .Where(path => IsCompletedStagingFile(fullStagingDirectory, path))
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault();
    }

    private static bool IsCompletedStagingFile(
        string fullStagingDirectory,
        string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) return false;
        var fullCandidate = Path.GetFullPath(candidate);
        var relative = Path.GetRelativePath(fullStagingDirectory, fullCandidate);
        if (Path.IsPathRooted(relative)
            || relative.Equals("..", StringComparison.Ordinal)
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return false;
        }
        var extension = Path.GetExtension(fullCandidate);
        return extension is not (".part" or ".ytdl" or ".tmp");
    }

    internal static string ResolveSaveAsDestination(
        string requestedPath,
        string actualExtension,
        YouTubeVideoDownloadFormat format)
    {
        var fullRequestedPath = Path.GetFullPath(requestedPath);
        var normalizedExtension = actualExtension.StartsWith('.')
            ? actualExtension.ToLowerInvariant()
            : $".{actualExtension.ToLowerInvariant()}";
        if (normalizedExtension.Length < 2
            || normalizedExtension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException("Pobrany plik wideo nie ma prawidłowego rozszerzenia.");
        }
        if (format == YouTubeVideoDownloadFormat.Mp4)
            return Path.ChangeExtension(fullRequestedPath, ".mp4");
        var requestedExtension = Path.GetExtension(fullRequestedPath);
        if (string.IsNullOrWhiteSpace(requestedExtension))
            return fullRequestedPath + normalizedExtension;
        if (KnownVideoExtensions.Contains(requestedExtension))
            return Path.ChangeExtension(fullRequestedPath, normalizedExtension);
        return fullRequestedPath + normalizedExtension;
    }

    private static string CreateStagingDirectory()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localData)
            ? Path.Combine(Path.GetTempPath(), "AccessibleMediaController", "download-staging")
            : Path.Combine(localData, "AccessibleMediaController", "download-staging");
        Directory.CreateDirectory(root);
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    internal static async Task PublishCompletedFileAsync(
        string source,
        string destination,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidDataException("Nie można ustalić folderu docelowego.");
        var incoming = Path.Combine(
            destinationDirectory,
            $".amc-download-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var input = new FileStream(
                             source,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                             incoming,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, 128 * 1024, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(incoming, destination, overwrite);
                    break;
                }
                catch (Exception exception) when (
                    attempt < PublishRetryCount
                    && exception is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
        finally
        {
            try { File.Delete(incoming); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static void CleanupStagingDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
        }
    }

    private static string TrimDiagnostic(string value)
    {
        var normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }
}
