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
            var targets = _podcasts.GetDownloadTargets(episodeIds)
                .ToDictionary(target => target.EpisodeId, StringComparer.Ordinal);
            var downloaded = 0;
            var alreadyDownloaded = 0;
            var failed = 0;
            string? firstFailure = null;
            string? singleTitle = null;

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
                    var destination = PodcastDownloadNaming.UniquePath(
                        folder,
                        PodcastDownloadNaming.SuggestedFileName(
                            target.Title,
                            target.MediaUrl,
                            target.MediaType));
                    PublishProgress(events, target, index, episodeIds.Length, percent: 0);

                    PodcastDownloadResult result;
                    if (YouTubeSourceResolver.IsYouTubeUrl(target.MediaUrl))
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
                firstFailure
            };
        }
        finally
        {
            Volatile.Write(ref _downloadInProgress, 0);
        }
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
