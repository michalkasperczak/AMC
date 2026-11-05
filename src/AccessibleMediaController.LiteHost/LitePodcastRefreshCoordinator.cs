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
    public const string AutomaticOperation = "podcast.refreshAutomatic";

    private readonly LitePodcastProgressStore _podcasts;
    private readonly LiteBookmarkStore? _bookmarks;
    private readonly PodcastFeedClient? _rssClient;
    private readonly YouTubeCollectionClient? _youTubeClient;
    private readonly FacebookPublicPageClient? _facebookClient;
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
        _facebookClient = new FacebookPublicPageClient();
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

    public object Refresh(JsonElement args) => RefreshCore(
        () => _podcasts.GetRefreshTargets(
            LiteArgs.ReadText(args, "subscriptionId")),
        emptyIsError: true,
        automatic: false);

    public object RefreshAutomatic()
    {
        // Duża biblioteka może wymagać zapisu tysięcy odcinków. Automatyczny
        // przebieg ustępuje sterowaniu odtwarzacza i interfejsowi; ręczne
        // odświeżenie zachowuje zwykły priorytet.
        var thread = Thread.CurrentThread;
        var previousPriority = thread.Priority;
        try
        {
            thread.Priority = ThreadPriority.BelowNormal;
            return RefreshCore(
                _podcasts.GetDueRefreshTargets,
                emptyIsError: false,
                automatic: true);
        }
        finally
        {
            thread.Priority = previousPriority;
        }
    }

    private object RefreshCore(
        Func<IReadOnlyList<PodcastRefreshTarget>> readTargets,
        bool emptyIsError,
        bool automatic)
    {
        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
            throw new LiteRequestException("Odświeżanie źródeł już trwa.");

        try
        {
            var targets = readTargets();
            if (targets.Count == 0)
            {
                if (emptyIsError)
                {
                    throw new LiteRequestException(
                        "Brak podcastów, kanałów lub playlist do odświeżenia.");
                }
                return new
                {
                    requested = 0,
                    succeeded = 0,
                    partial = 0,
                    failed = 0,
                    addedEpisodes = 0,
                    retainedArchivedEpisodes = 0,
                    inboxCount = _podcasts.GetInboxCount(),
                    failures = Array.Empty<PodcastRefreshIssue>(),
                    partialSources = Array.Empty<PodcastRefreshIssue>()
                };
            }

            var succeeded = 0;
            var partial = 0;
            var failed = 0;
            var addedEpisodes = 0;
            var retainedArchivedEpisodes = 0;
            var inboxCount = _podcasts.GetInboxCount();
            var failures = new List<PodcastRefreshIssue>();
            var partialSources = new List<PodcastRefreshIssue>();
            var pendingBatch = new List<(PodcastRefreshTarget Target, PodcastFeedDocument Feed)>();

            void RecordSuccess(PodcastRefreshTarget target, PodcastFeedDocument feed,
                PodcastRefreshResult result)
            {
                if (feed.EpisodesTruncated)
                {
                    partial++;
                    partialSources.Add(new PodcastRefreshIssue(
                        UserFacingTitle(target.Title),
                        $"wczytano pierwsze {PodcastFeedParser.MaximumEpisodes} odcinków; kanał zawiera więcej pozycji"));
                }
                else
                {
                    succeeded++;
                }
                addedEpisodes += result.AddedEpisodes;
                retainedArchivedEpisodes += result.RetainedEpisodesAbsentFromFeed;
                inboxCount = result.InboxCount;
            }

            foreach (var target in targets)
            {
                try
                {
                    if (automatic) _podcasts.MarkRefreshAttempt(target.SubscriptionId);
                    var feed = _fetch(target).GetAwaiter().GetResult();
                    var hasChapters = feed.Episodes.Any(episode =>
                        episode.Chapters is { Count: > 0 });
                    if (automatic && !hasChapters)
                    {
                        pendingBatch.Add((target, feed));
                        continue;
                    }
                    PodcastRefreshResult result;
                    if (_bookmarks is not null && hasChapters)
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
                    RecordSuccess(target, feed, result);
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
                    var issue = FailureIssue(target, exception);
                    failures.Add(issue);
                    Console.Error.WriteLine(
                        $"[lite-host] nie odświeżono źródła „{issue.Title}”: {issue.Reason}");
                }
            }

            if (pendingBatch.Count > 0)
            {
                var requests = pendingBatch.Select(item => new PodcastRefreshRequest(
                    item.Target.SubscriptionId, item.Feed)).ToArray();
                var results = _podcasts.ApplyRefreshBatch(requests);
                for (var index = 0; index < results.Count; index++)
                {
                    RecordSuccess(pendingBatch[index].Target, pendingBatch[index].Feed,
                        results[index]);
                }
            }

            return new
            {
                requested = targets.Count,
                succeeded,
                partial,
                failed,
                addedEpisodes,
                retainedArchivedEpisodes,
                inboxCount,
                failures,
                partialSources
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
            PodcastSourceKind.Rss or PodcastSourceKind.SoundCloudPodcast => await _rssClient!
                .FetchAsync(address, CancellationToken.None)
                .ConfigureAwait(false),
            PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist =>
                (await _youTubeClient!
                    .FetchAsync(address, CancellationToken.None)
                    .ConfigureAwait(false)).Feed,
            PodcastSourceKind.FacebookPage => await _facebookClient!
                .FetchAsync(address, CancellationToken.None)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException(
                "Publiczne medium internetowe jest sprawdzane ponownie przy każdym odtwarzaniu.")
        };
    }

    private static PodcastRefreshIssue FailureIssue(
        PodcastRefreshTarget target,
        Exception exception) => new(
            UserFacingTitle(target.Title),
            exception switch
            {
                OperationCanceledException or TimeoutException =>
                    "przekroczono czas oczekiwania na odpowiedź",
                HttpRequestException { StatusCode: { } status } =>
                    $"serwer źródła zwrócił kod {(int)status}",
                HttpRequestException => "nie udało się połączyć z serwerem źródła",
                System.Xml.XmlException => "kanał zawiera nieprawidłowy dokument RSS lub Atom",
                ArgumentException => "zapisany adres źródła jest nieprawidłowy",
                InvalidDataException or NotSupportedException => UserFacingReason(exception.Message),
                _ => "nie udało się odczytać źródła"
            });

    private static string UserFacingTitle(string? value)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length == 0 ? "Źródło bez nazwy" : normalized[..Math.Min(160, normalized.Length)];
    }

    private static string UserFacingReason(string? value)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0) return "nie udało się odczytać źródła";
        return normalized[..Math.Min(240, normalized.Length)].TrimEnd('.', ' ', ';');
    }

    private sealed record PodcastRefreshIssue(string Title, string Reason);

    public void Dispose()
    {
        _rssClient?.Dispose();
        _facebookClient?.Dispose();
    }
}
