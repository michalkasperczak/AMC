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

    public static readonly TimeSpan AutomaticTimeBudget = TimeSpan.FromSeconds(75);

    private readonly LitePodcastProgressStore _podcasts;
    private readonly LiteBookmarkStore? _bookmarks;
    private readonly PodcastFeedClient? _rssClient;
    private readonly YouTubeCollectionClient? _youTubeClient;
    private readonly FacebookPublicPageClient? _facebookClient;
    private readonly Func<PodcastRefreshTarget, CancellationToken, Task<PodcastFeedDocument>> _fetch;
    private readonly Func<PodcastRefreshTarget, CancellationToken, Task<bool>>? _rssProbe;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _automaticTimeBudget;
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
        _rssProbe = ProbeYouTubeChannelAsync;
        _timeProvider = TimeProvider.System;
        _automaticTimeBudget = AutomaticTimeBudget;
    }

    internal LitePodcastRefreshCoordinator(
        LitePodcastProgressStore podcasts,
        LiteBookmarkStore? bookmarks,
        Func<PodcastRefreshTarget, CancellationToken, Task<PodcastFeedDocument>> fetch,
        TimeSpan? automaticTimeBudget = null,
        TimeProvider? timeProvider = null,
        Func<PodcastRefreshTarget, CancellationToken, Task<bool>>? rssProbe = null)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
        _bookmarks = bookmarks;
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _rssProbe = rssProbe;
        _automaticTimeBudget = automaticTimeBudget ?? AutomaticTimeBudget;
        if (_automaticTimeBudget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(automaticTimeBudget));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public object Refresh(JsonElement args) => RefreshCore(
        () =>
        {
            var targets = _podcasts.GetRefreshTargets(
                LiteArgs.ReadText(args, "subscriptionId"));
            return new PodcastAutomaticRefreshPlan(targets, targets.Count, targets.Count);
        },
        emptyIsError: true,
        automatic: false);

    public object RefreshAutomatic() => RefreshAutomatic(TimeSpan.Zero);

    public object RefreshAutomatic(TimeSpan schedulingCadence)
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
                () => _podcasts.GetDueRefreshPlan(DateTime.UtcNow, schedulingCadence),
                emptyIsError: false,
                automatic: true);
        }
        finally
        {
            thread.Priority = previousPriority;
        }
    }

    private object RefreshCore(
        Func<PodcastAutomaticRefreshPlan> readPlan,
        bool emptyIsError,
        bool automatic)
    {
        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
            throw new LiteRequestException("Odświeżanie źródeł już trwa.");

        try
        {
            var plan = readPlan();
            var targets = plan.Targets;
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
                    targetedSources = plan.TargetCount,
                    dueSources = plan.DueCount,
                    deferredDueSources = plan.DueCount,
                    budgetExpired = false,
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
            using var automaticBudget = automatic
                ? new CancellationTokenSource(_automaticTimeBudget, _timeProvider)
                : null;
            var cancellationToken = automaticBudget?.Token ?? CancellationToken.None;
            var requested = 0;
            var budgetExpired = false;

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
                if (automatic && cancellationToken.IsCancellationRequested)
                {
                    budgetExpired = true;
                    break;
                }
                try
                {
                    if (automatic && !_podcasts.TryMarkRefreshAttempt(
                            target.SubscriptionId, cancellationToken))
                    {
                        budgetExpired = true;
                        break;
                    }
                    requested++;
                    // A successful Atom probe with no unseen IDs needs no yt-dlp
                    // and no rewrite of the saved catalogue. A missing feed or a
                    // new ID falls through to the ordinary full refresh.
                    if (automatic && target.SourceKind == PodcastSourceKind.YouTubeChannel
                        && _rssProbe is not null
                        && _rssProbe(target, cancellationToken).GetAwaiter().GetResult())
                    {
                        succeeded++;
                        continue;
                    }
                    var feed = _fetch(target, cancellationToken).GetAwaiter().GetResult();
                    var hasChapters = feed.Episodes.Any(episode =>
                        episode.Chapters is { Count: > 0 });
                    // Próba automatu jest zapisana przed pobraniem. Wynik
                    // każdego ukończonego kanału musi więc dostać własną,
                    // krótką transakcję przed przejściem do kolejnego: proces
                    // może zostać zamknięty w dowolnej chwili. Nie tworzymy
                    // ogólnego potoku ani masowego zapisu całej biblioteki.
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
                catch (OperationCanceledException) when (automatic && cancellationToken.IsCancellationRequested)
                {
                    // Anulowanie budżetu nie jest wynikiem źródła: kolejnych
                    // kanałów nie oznaczamy próbą, a telemetria zachowuje ich
                    // należność do następnego zwykłego taktu.
                    budgetExpired = true;
                    break;
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

            return new
            {
                requested,
                targetedSources = plan.TargetCount,
                dueSources = plan.DueCount,
                deferredDueSources = Math.Max(0, plan.DueCount - requested),
                budgetExpired,
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

    private async Task<bool> ProbeYouTubeChannelAsync(
        PodcastRefreshTarget target, CancellationToken cancellationToken)
    {
        if (!YouTubeSubscriptionsExporter.TryParseSubscriptionId(
                target.SubscriptionId, out var channelId, out var isChannel)
            || !isChannel) return false;
        var snapshot = _podcasts.GetYouTubeFeedSnapshot(target.SubscriptionId);
        if (snapshot is null) return false;
        var entries = await _youTubeClient!.FetchChannelFeedEntriesAsync(
            channelId, cancellationToken).ConfigureAwait(false);
        return CanSkipYouTubeExtraction(snapshot, entries, DateTime.UtcNow);
    }

    internal static bool CanSkipYouTubeExtraction(
        PodcastYouTubeFeedSnapshot snapshot,
        IReadOnlyDictionary<string, YouTubeFeedEntry> entries,
        DateTime utcNow)
    {
        // A periodic full extraction catches gaps that the bounded feed never
        // exposed. A future or stale timestamp cannot authorize skipping.
        var age = utcNow.ToUniversalTime().Ticks - snapshot.LastFullRefreshUtcTicks;
        return age >= 0
            && age < TimeSpan.FromHours(24).Ticks
            && entries.Count > 0
            && entries.Keys.All(snapshot.KnownVideoIds.Contains);
    }

    private async Task<PodcastFeedDocument> FetchAsync(
        PodcastRefreshTarget target,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(target.FeedUrl, UriKind.Absolute, out var address))
            throw new InvalidDataException("Zapisany adres kanału jest nieprawidłowy.");
        return target.SourceKind switch
        {
            PodcastSourceKind.Rss or PodcastSourceKind.SoundCloudPodcast => await _rssClient!
                .FetchAsync(address, cancellationToken)
                .ConfigureAwait(false),
            PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist =>
                (await _youTubeClient!
                    .FetchAsync(address, cancellationToken)
                    .ConfigureAwait(false)).Feed,
            PodcastSourceKind.FacebookPage => await _facebookClient!
                .FetchAsync(address, cancellationToken)
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
