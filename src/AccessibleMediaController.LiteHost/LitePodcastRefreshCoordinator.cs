using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Pobiera kanały tymi samymi klientami co główne AMC, a zmiany przekazuje
/// jedynemu właścicielowi zapisu podcasts.db. Operacja może działać poza
/// kolejką transportu; krótki etap zapisu pozostaje serializowany w store.
/// </summary>
internal sealed class LitePodcastRefreshCoordinator : IDisposable
{
    public const string Operation = "podcast.refresh";

    private readonly LitePodcastProgressStore _podcasts;
    private readonly LiteBookmarkStore? _bookmarks;
    private readonly PodcastFeedClient? _rssClient;
    private readonly YouTubeCollectionClient? _youTubeClient;
    private readonly Func<PodcastRefreshTarget, Task<PodcastFeedDocument>> _fetch;
    private int _refreshInProgress;

    public LitePodcastRefreshCoordinator(
        LitePodcastProgressStore podcasts,
        LiteBookmarkStore? bookmarks)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
        _bookmarks = bookmarks;
        _rssClient = new PodcastFeedClient();
        _youTubeClient = new YouTubeCollectionClient();
        _fetch = FetchAsync;
    }

    internal LitePodcastRefreshCoordinator(
        LitePodcastProgressStore podcasts,
        LiteBookmarkStore? bookmarks,
        Func<PodcastRefreshTarget, Task<PodcastFeedDocument>> fetch)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
        _bookmarks = bookmarks;
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    }

    public object Refresh(JsonElement args)
    {
        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
            throw new LiteRequestException("Odświeżanie źródeł już trwa.");

        try
        {
            var requestedSubscriptionId = LiteArgs.ReadText(args, "subscriptionId");
            var targets = _podcasts.GetRefreshTargets(requestedSubscriptionId);
            if (targets.Count == 0)
            {
                throw new LiteRequestException(
                    "Brak podcastów, kanałów lub playlist do odświeżenia.");
            }

            var succeeded = 0;
            var failed = 0;
            var addedEpisodes = 0;
            var retainedArchivedEpisodes = 0;
            var inboxCount = _podcasts.GetInboxCount();
            foreach (var target in targets)
            {
                try
                {
                    var feed = _fetch(target).GetAwaiter().GetResult();
                    PodcastRefreshResult result;
                    if (_bookmarks is not null
                        && feed.Episodes.Any(episode => episode.Chapters is { Count: > 0 }))
                    {
                        result = _bookmarks.MutateBookmarks(bookmarks =>
                            _podcasts.ApplyRefresh(
                                target.SubscriptionId,
                                feed,
                                bookmarks));
                    }
                    else
                    {
                        result = _podcasts.ApplyRefresh(target.SubscriptionId, feed);
                    }
                    succeeded++;
                    addedEpisodes += result.AddedEpisodes;
                    retainedArchivedEpisodes += result.RetainedEpisodesAbsentFromFeed;
                    inboxCount = result.InboxCount;
                }
                catch (Exception exception) when (exception is HttpRequestException
                    or InvalidDataException
                    or System.Xml.XmlException
                    or ArgumentException
                    or OperationCanceledException
                    or NotSupportedException
                    or TimeoutException)
                {
                    failed++;
                    Console.Error.WriteLine(
                        $"[lite-host] nie odświeżono źródła „{target.Title}”: "
                        + exception.GetType().Name);
                }
            }

            return new
            {
                requested = targets.Count,
                succeeded,
                failed,
                addedEpisodes,
                retainedArchivedEpisodes,
                inboxCount
            };
        }
        finally
        {
            Volatile.Write(ref _refreshInProgress, 0);
        }
    }

    private async Task<PodcastFeedDocument> FetchAsync(PodcastRefreshTarget target)
    {
        if (!Uri.TryCreate(target.FeedUrl, UriKind.Absolute, out var address))
            throw new InvalidDataException("Zapisany adres kanału jest nieprawidłowy.");
        return target.SourceKind switch
        {
            PodcastSourceKind.Rss => await _rssClient!
                .FetchAsync(address, CancellationToken.None)
                .ConfigureAwait(false),
            PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist =>
                (await _youTubeClient!
                    .FetchAsync(address, CancellationToken.None)
                    .ConfigureAwait(false)).Feed,
            _ => throw new InvalidOperationException(
                "Publiczne medium internetowe jest sprawdzane ponownie przy każdym odtwarzaniu.")
        };
    }

    public void Dispose() => _rssClient?.Dispose();
}
