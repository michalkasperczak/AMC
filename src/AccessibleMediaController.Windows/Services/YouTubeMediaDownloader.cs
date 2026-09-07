using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Downloads one public YouTube item as MP3 without browser cookies or account
/// access. Conversion is staged outside the destination library so neither a
/// library watcher nor a cloud-sync client can lock yt-dlp working files.
/// </summary>
internal static class YouTubeMediaDownloader
{
    private const int PublishRetryCount = 20;

    internal static async Task<PodcastDownloadResult> DownloadMp3Async(
        string pageUrl,
        string destinationPath,
        CancellationToken cancellationToken,
        bool overwrite)
    {
        if (!YouTubeSourceResolver.IsYouTubeUrl(pageUrl))
            throw new ArgumentException("Adres nie prowadzi do YouTube.", nameof(pageUrl));
        var ytDlp = YouTubeSourceResolver.FindExecutable()
            ?? throw new NotSupportedException(
                "Pobieranie z YouTube wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź aktualizacje i składniki.");
        var ffmpeg = FfmpegComponentManager.FindInstalledExecutable()
            ?? throw new NotSupportedException(
                "Pobieranie dźwięku z YouTube wymaga składnika FFmpeg. Wybierz Pomoc, Sprawdź aktualizacje i składniki.");

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

        var start = new ProcessStartInfo
        {
            FileName = ytDlp,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
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
                throw new InvalidDataException("YouTube nie udostępnił materiału do zapisania jako MP3.");
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

    private static async Task PublishCompletedFileAsync(
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
