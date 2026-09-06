using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Downloads one public YouTube item as MP3 without browser cookies or account
/// access. Work is staged beside the destination and only the completed file
/// is published under the user-visible name.
/// </summary>
internal static class YouTubeMediaDownloader
{
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
        var stagingBase = Path.Combine(directory, $".amc-youtube-{Guid.NewGuid():N}");
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
            File.Move(expectedStagingPath, fullDestination, overwrite);
            return new PodcastDownloadResult(fullDestination, length);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            process?.Dispose();
            CleanupStagingFiles(directory, Path.GetFileName(stagingBase));
        }
    }

    private static void CleanupStagingFiles(string directory, string prefix)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, prefix + ".*"))
            {
                try { File.Delete(path); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
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
