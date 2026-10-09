using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Wąska brama zmian biblioteki Podcastów i YouTube dla bezokiennego hosta.
/// Nie przyjmuje niepełnego PersistedState i nie może nadpisać ustawień,
/// kolejki ani danych innych sesji.
/// </summary>
public sealed class PodcastLibraryMutationStore(string databasePath)
{
    private readonly PodcastLibraryDatabase _database = new(databasePath);

    public PodcastPlaybackCheckpointResult SavePlaybackCheckpoint(
        string episodeId,
        TimeSpan position,
        TimeSpan duration,
        bool completed) =>
        _database.SavePlaybackCheckpoint(episodeId, position, duration, completed);

    public IReadOnlyList<PodcastRefreshTarget> GetRefreshTargets(string? subscriptionId = null) =>
        _database.GetRefreshTargets(subscriptionId);

    public int GetInboxCount() => _database.GetInboxCount();

    public IReadOnlyList<PodcastDownloadTarget> GetDownloadTargets(
        IReadOnlyCollection<string> episodeIds) =>
        _database.GetDownloadTargets(episodeIds);

    public PodcastDownloadPathResult SaveDownloadPath(
        string episodeId,
        string downloadPath) =>
        _database.SaveDownloadPath(episodeId, downloadPath);

    public PodcastSourceAddResult AddSource(
        PodcastFeedDocument feed,
        string? titleOverride,
        PodcastSourceKind sourceKind,
        DateTime addedUtc,
        BookmarkSettings? bookmarks = null) =>
        _database.AddSource(
            feed,
            titleOverride,
            sourceKind,
            addedUtc,
            bookmarks);

    public PodcastInternetMediaAddResult AddInternetMedia(
        PodcastInternetMediaSource media,
        string? titleOverride) =>
        _database.AddInternetMedia(media, titleOverride);

    public IReadOnlyList<PodcastOpmlEntry> GetOpmlEntries() =>
        _database.GetOpmlEntries();

    public PodcastRefreshResult ApplyRefresh(
        string subscriptionId,
        PodcastFeedDocument feed,
        DateTime refreshUtc,
        BookmarkSettings? bookmarks = null) =>
        _database.ApplyRefresh(subscriptionId, feed, refreshUtc, bookmarks);
}

public sealed record PodcastRefreshTarget(
    string SubscriptionId,
    string Title,
    string FeedUrl,
    PodcastSourceKind SourceKind);

public sealed record PodcastRefreshResult(
    string Title,
    int AddedEpisodes,
    int UpdatedEpisodes,
    int RetainedEpisodesAbsentFromFeed,
    int InboxCount);

/// <summary>
/// Minimalny, niemutowalny opis odcinka potrzebny do pobrania. Nie przenosi
/// calego profilu ani surowego JSON-u poza warstwe konfiguracji.
/// </summary>
public sealed record PodcastDownloadTarget(
    string EpisodeId,
    string Title,
    string MediaUrl,
    string? MediaType,
    string? DownloadPath,
    string? ConfiguredDownloadsFolder);

public sealed record PodcastDownloadPathResult(
    string EpisodeId,
    string DownloadPath,
    bool Changed);

public sealed record PodcastSourceAddResult(
    string SubscriptionId,
    string Title,
    PodcastSourceKind SourceKind,
    bool AddedSubscription,
    bool RestoredSubscription,
    int AddedEpisodes,
    int UpdatedEpisodes,
    int AvailableEpisodes);

/// <summary>
/// Trwaly opis publicznego materialu. Tymczasowy podpisany adres strumienia
/// pozostaje w resolverze i nigdy nie trafia do bazy.
/// </summary>
public sealed record PodcastInternetMediaSource(
    string PageUrl,
    string Title,
    string Channel,
    TimeSpan Duration,
    bool IsLive);

public sealed record PodcastInternetMediaAddResult(
    string SubscriptionId,
    string EpisodeId,
    string Title,
    bool AddedEpisode);
