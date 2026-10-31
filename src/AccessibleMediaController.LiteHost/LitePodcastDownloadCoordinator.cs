using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Pobiera odcinki tym samym torem co glowne AMC, a po opublikowaniu pliku
/// zapisuje tylko jego sciezke przez waska brame podcasts.db.
/// </summary>
internal sealed class LitePodcastDownloadCoordinator : IDisposable
{
    public const string Operation = "podcast.download";
    public const string SaveAsInfoOperation = "podcast.downloadSaveAsInfo";
    public const string SaveAsOperation = "podcast.downloadSaveAs";
    public const string VideoOperation = "podcast.videoDownload";
    public const string VideoSaveAsInfoOperation = "podcast.videoSaveAsInfo";
    public const string VideoSaveAsOperation = "podcast.videoSaveAs";
    private const int MaximumBatchSize = 100;

    private readonly LitePodcastProgressStore _podcasts;
    private readonly PodcastEpisodeDownloader _downloader = new();
    private readonly CancellationTokenSource _cancellation = new();
    private int _downloadInProgress;

    public LitePodcastDownloadCoordinator(LitePodcastProgressStore podcasts)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
    }

    public object Download(JsonElement args, LiteEventSink events)
    {
        if (Interlocked.CompareExchange(ref _downloadInProgress, 1, 0) != 0)
            throw new LiteRequestException("Pobieranie odcinków już trwa.");

        try
        {
            var episodeIds = ReadEpisodeIds(args);
            var separateSourceFolders = LiteArgs.ReadBool(
                args, "separateSourceFolders", false);
            var targets = _podcasts.GetDownloadTargets(episodeIds)
                .ToDictionary(target => target.EpisodeId, StringComparer.Ordinal);
            var downloaded = 0;
            var alreadyDownloaded = 0;
            var failed = 0;
            string? firstFailure = null;
            string? singleTitle = null;
            var files = new List<object>();

            for (var index = 0; index < episodeIds.Length; index++)
            {
                if (!targets.TryGetValue(episodeIds[index], out var target))
                {
                    failed++;
                    firstFailure ??= "Odcinka nie ma już w bibliotece Podcastów i YouTube.";
                    continue;
                }
                singleTitle ??= target.Title;
                try
                {
                    if (!string.IsNullOrWhiteSpace(target.DownloadPath)
                        && File.Exists(target.DownloadPath))
                    {
                        alreadyDownloaded++;
                        files.Add(new {
                            episodeId = target.EpisodeId,
                            title = target.Title,
                            path = target.DownloadPath
                        });
                        continue;
                    }
                    if (!Uri.TryCreate(target.MediaUrl, UriKind.Absolute, out var source)
                        || source.Scheme is not ("http" or "https"))
                    {
                        throw new InvalidDataException(
                            "Odcinek nie ma prawidłowego adresu audio.");
                    }

                    var folder = PodcastDownloadFolderResolver.Resolve(
                        target.ConfiguredDownloadsFolder);
                    if (separateSourceFolders)
                    {
                        folder = Path.Combine(folder,
                            PodcastDownloadNaming.SuggestedFolderName(
                                target.SourceTitle, "Nieznany podcast"));
                    }
                    if (separateSourceFolders) Directory.CreateDirectory(folder);
                    var destination = PodcastDownloadNaming.UniquePath(
                        folder,
                        PodcastDownloadNaming.SuggestedFileName(
                            target.Title,
                            target.MediaUrl,
                            target.MediaType));
                    PublishProgress(events, target, index, episodeIds.Length, percent: 0);

                    PodcastDownloadResult result;
                    if (YouTubeMediaDownloader.IsSupportedVideoUrl(target.MediaUrl))
                    {
                        result = YouTubeMediaDownloader.DownloadMp3Async(
                                target.MediaUrl,
                                destination,
                                _cancellation.Token,
                                overwrite: false)
                            .GetAwaiter()
                            .GetResult();
                    }
                    else
                    {
                        var lastProgressUtc = DateTime.MinValue;
                        var lastPercent = -1;
                        var progress = new InlineProgress<PodcastDownloadProgress>(value =>
                        {
                            var percent = value.TotalBytes is > 0
                                ? (int)Math.Clamp(
                                    value.BytesReceived * 100 / value.TotalBytes.Value,
                                    0,
                                    100)
                                : -1;
                            var now = DateTime.UtcNow;
                            if (percent == lastPercent
                                && now - lastProgressUtc < TimeSpan.FromSeconds(1)) return;
                            if (now - lastProgressUtc < TimeSpan.FromMilliseconds(500)
                                && percent is not 100) return;
                            lastProgressUtc = now;
                            lastPercent = percent;
                            PublishProgress(
                                events,
                                target,
                                index,
                                episodeIds.Length,
                                percent,
                                value.BytesReceived);
                        });
                        result = _downloader.DownloadAsync(
                                source,
                                destination,
                                progress,
                                _cancellation.Token,
                                overwrite: false)
                            .GetAwaiter()
                            .GetResult();
                    }

                    _podcasts.SaveDownloadPath(target.EpisodeId, result.Path);
                    downloaded++;
                    files.Add(new {
                        episodeId = target.EpisodeId,
                        title = target.Title,
                        path = result.Path
                    });
                    PublishProgress(events, target, index, episodeIds.Length, percent: 100);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is HttpRequestException
                    or IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or ArgumentException
                    or NotSupportedException
                    or TimeoutException)
                {
                    failed++;
                    firstFailure ??= exception.Message;
                    Console.Error.WriteLine(
                        $"[lite-host] nie pobrano odcinka; blad {exception.GetType().Name}");
                }
            }

            return new
            {
                requested = episodeIds.Length,
                downloaded,
                alreadyDownloaded,
                failed,
                singleTitle,
                firstFailure,
                files
            };
        }
        finally
        {
            Volatile.Write(ref _downloadInProgress, 0);
        }
    }

    public object SaveAsInfo(JsonElement args)
    {
        var target = GetSingleTarget(args);
        return new
        {
            title = target.Title,
            suggestedFileName = PodcastDownloadNaming.SuggestedFileName(
                target.Title,
                target.MediaUrl,
                target.MediaType),
            initialFolder = PodcastDownloadFolderResolver.ResolveDialogInitialFolder(
                target.ConfiguredDownloadsFolder)
        };
    }

    public object SaveAs(JsonElement args, LiteEventSink events)
    {
        if (Interlocked.CompareExchange(ref _downloadInProgress, 1, 0) != 0)
            throw new LiteRequestException("Pobieranie odcinków już trwa.");

        try
        {
            var target = GetSingleTarget(args);
            var destination = NormalizeDestination(LiteArgs.RequirePath(args, "path"));
            if (!Uri.TryCreate(target.MediaUrl, UriKind.Absolute, out var source)
                || source.Scheme is not ("http" or "https"))
            {
                throw new LiteRequestException("Odcinek nie ma prawidłowego adresu audio.");
            }

            PublishProgress(events, target, 0, 1, percent: 0);
            PodcastDownloadResult result;
            try
            {
                if (YouTubeMediaDownloader.IsSupportedVideoUrl(target.MediaUrl))
                {
                    result = YouTubeMediaDownloader.DownloadMp3Async(
                            target.MediaUrl,
                            destination,
                            _cancellation.Token,
                            overwrite: true)
                        .GetAwaiter()
                        .GetResult();
                }
                else
                {
                    var lastProgressUtc = DateTime.MinValue;
                    var lastPercent = -1;
                    var progress = new InlineProgress<PodcastDownloadProgress>(value =>
                    {
                        var percent = value.TotalBytes is > 0
                            ? (int)Math.Clamp(
                                value.BytesReceived * 100 / value.TotalBytes.Value,
                                0,
                                100)
                            : -1;
                        var now = DateTime.UtcNow;
                        if (percent == lastPercent
                            && now - lastProgressUtc < TimeSpan.FromSeconds(1)) return;
                        if (now - lastProgressUtc < TimeSpan.FromMilliseconds(500)
                            && percent is not 100) return;
                        lastProgressUtc = now;
                        lastPercent = percent;
                        PublishProgress(events, target, 0, 1, percent, value.BytesReceived);
                    });
                    result = _downloader.DownloadAsync(
                            source,
                            destination,
                            progress,
                            _cancellation.Token,
                            overwrite: true)
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException
                or IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException
                or TimeoutException)
            {
                throw new LiteRequestException("Nie można pobrać odcinka: " + exception.Message);
            }

            PublishProgress(events, target, 0, 1, percent: 100);
            return new
            {
                title = target.Title,
                fileName = Path.GetFileName(result.Path),
                bytesWritten = result.BytesWritten
            };
        }
        finally
        {
            Volatile.Write(ref _downloadInProgress, 0);
        }
    }

    public object DownloadVideos(JsonElement args, LiteEventSink events)
    {
        if (Interlocked.CompareExchange(ref _downloadInProgress, 1, 0) != 0)
            throw new LiteRequestException("Pobieranie odcinków lub wideo już trwa.");
        try
        {
            var episodeIds = ReadEpisodeIds(args);
            var format = ReadVideoFormat(args);
            var targets = _podcasts.GetDownloadTargets(episodeIds)
                .ToDictionary(target => target.EpisodeId, StringComparer.Ordinal);
            var downloaded = 0;
            var failed = 0;
            string? firstFailure = null;
            string? fileName = null;
            var files = new List<object>();
            for (var index = 0; index < episodeIds.Length; index++)
            {
                if (!targets.TryGetValue(episodeIds[index], out var target))
                {
                    failed++;
                    firstFailure ??= "Materiału nie ma już w bibliotece Podcastów i YouTube.";
                    continue;
                }
                try
                {
                    RequireVideoTarget(target);
                    var folder = PodcastDownloadFolderResolver.Resolve(
                        target.ConfiguredDownloadsFolder);
                    PublishProgress(events, target, index, episodeIds.Length, percent: 0);
                    var result = YouTubeMediaDownloader.DownloadVideoAsync(
                            target.MediaUrl,
                            folder,
                            target.Title,
                            format,
                            _cancellation.Token)
                        .GetAwaiter()
                        .GetResult();
                    downloaded++;
                    fileName ??= Path.GetFileName(result.Path);
                    files.Add(new {
                        episodeId = target.EpisodeId,
                        title = target.Title,
                        path = result.Path
                    });
                    PublishProgress(events, target, index, episodeIds.Length, percent: 100);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is HttpRequestException
                    or IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or ArgumentException
                    or NotSupportedException
                    or TimeoutException)
                {
                    failed++;
                    firstFailure ??= exception.Message;
                    Console.Error.WriteLine(
                        $"[lite-host] nie pobrano wideo; blad {exception.GetType().Name}");
                }
            }
            return new
            {
                requested = episodeIds.Length,
                downloaded,
                failed,
                firstFailure,
                fileName,
                files
            };
        }
        finally
        {
            Volatile.Write(ref _downloadInProgress, 0);
        }
    }

    public object VideoSaveAsInfo(JsonElement args)
    {
        var target = GetSingleTarget(args);
        RequireVideoTarget(target);
        return new
        {
            title = target.Title,
            suggestedBaseName = PodcastDownloadNaming.SuggestedVideoBaseName(target.Title),
            initialFolder = PodcastDownloadFolderResolver.ResolveDialogInitialFolder(
                target.ConfiguredDownloadsFolder)
        };
    }

    public object SaveVideoAs(JsonElement args, LiteEventSink events)
    {
        if (Interlocked.CompareExchange(ref _downloadInProgress, 1, 0) != 0)
            throw new LiteRequestException("Pobieranie odcinków lub wideo już trwa.");
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "AccessibleMediaController",
            "save-video-as",
            Guid.NewGuid().ToString("N"));
        try
        {
            var target = GetSingleTarget(args);
            RequireVideoTarget(target);
            var format = ReadVideoFormat(args);
            var requestedPath = NormalizeDestination(LiteArgs.RequirePath(args, "path"));
            Directory.CreateDirectory(temporaryDirectory);
            PublishProgress(events, target, 0, 1, percent: 0);
            var downloaded = YouTubeMediaDownloader.DownloadVideoAsync(
                    target.MediaUrl,
                    temporaryDirectory,
                    target.Title,
                    format,
                    _cancellation.Token)
                .GetAwaiter()
                .GetResult();
            var destination = YouTubeMediaDownloader.ResolveSaveAsDestination(
                requestedPath,
                Path.GetExtension(downloaded.Path),
                format);
            YouTubeMediaDownloader.PublishCompletedFileAsync(
                    downloaded.Path,
                    destination,
                    overwrite: true,
                    _cancellation.Token)
                .GetAwaiter()
                .GetResult();
            PublishProgress(events, target, 0, 1, percent: 100);
            return new
            {
                title = target.Title,
                fileName = Path.GetFileName(destination),
                bytesWritten = downloaded.BytesWritten
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException
            or TimeoutException)
        {
            throw new LiteRequestException("Nie można pobrać wideo: " + exception.Message);
        }
        finally
        {
            try { Directory.Delete(temporaryDirectory, recursive: true); }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException) { }
            Volatile.Write(ref _downloadInProgress, 0);
        }
    }

    private static YouTubeVideoDownloadFormat ReadVideoFormat(JsonElement args)
    {
        var value = LiteArgs.ReadText(args, "format") ?? "mp4";
        return value.Equals("original", StringComparison.OrdinalIgnoreCase)
            ? YouTubeVideoDownloadFormat.Original
            : YouTubeVideoDownloadFormat.Mp4;
    }

    private static void RequireVideoTarget(PodcastDownloadTarget target)
    {
        if (!YouTubeMediaDownloader.IsSupportedVideoUrl(target.MediaUrl))
            throw new LiteRequestException(
                "Pobieranie wideo jest dostępne dla filmów YouTube, Facebooka i Instagrama.");
    }

    private PodcastDownloadTarget GetSingleTarget(JsonElement args)
    {
        var episodeId = LiteArgs.RequireText(args, "episodeId");
        var target = _podcasts.GetDownloadTargets([episodeId]).FirstOrDefault();
        return target ?? throw new LiteRequestException(
            "Odcinka nie ma już w bibliotece Podcastów i YouTube.");
    }

    private static string NormalizeDestination(string path)
    {
        string destination;
        try
        {
            destination = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            throw new LiteRequestException("Wskazana ścieżka pliku jest niepoprawna.");
        }
        var directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new LiteRequestException("Folder docelowy nie istnieje.");
        return destination;
    }

    private static string[] ReadEpisodeIds(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("episodeIds", out var values)
            || values.ValueKind != JsonValueKind.Array)
            throw new LiteRequestException("Zaznacz co najmniej jeden odcinek podcastu.");
        var result = values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumBatchSize)
            .ToArray();
        if (result.Length == 0)
            throw new LiteRequestException("Zaznacz co najmniej jeden odcinek podcastu.");
        return result;
    }

    private static void PublishProgress(
        LiteEventSink events,
        PodcastDownloadTarget target,
        int zeroBasedIndex,
        int total,
        int percent,
        long bytesReceived = 0) =>
        events.Publish("podcast.downloadProgress", new
        {
            current = zeroBasedIndex + 1,
            total,
            title = target.Title,
            percent,
            bytesReceived
        });

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        _downloader.Dispose();
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
