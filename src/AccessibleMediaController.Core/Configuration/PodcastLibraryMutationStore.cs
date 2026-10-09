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

    public PodcastFavoriteToggleResult ToggleFavorites(
        IReadOnlyCollection<string> subscriptionIds,
        IReadOnlyCollection<string> episodeIds) =>
        _database.ToggleFavorites(subscriptionIds, episodeIds);

    public PodcastPlaybackOptionsSnapshot GetPlaybackOptions(
        PodcastPlaybackOptionsTarget target,
        string itemId) =>
        _database.GetPlaybackOptions(target, itemId);

    public PodcastPlaybackOptionsSnapshot SetPlaybackOptions(
        PodcastPlaybackOptionsTarget target,
        string itemId,
        PodcastPlaybackOptionsChange change) =>
        _database.SetPlaybackOptions(target, itemId, change);

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

    public IReadOnlyList<YouTubeCollectionExportEntry> GetYouTubeCollectionsForExport() =>
        _database.GetYouTubeCollectionsForExport();

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

public sealed record PodcastFavoriteToggleResult(
    bool Favorite,
    int RequestedCount,
    int ChangedCount);

public enum PodcastPlaybackOptionsTarget
{
    Podcast,
    Episode
}

/// <summary>
/// Pelny, zweryfikowany wybor z okna opcji. Puste wartosci oznaczaja
/// dziedziczenie z szerszego zakresu, nigdy domyslne <c>false</c>.
/// </summary>
public sealed record PodcastPlaybackOptionsChange(
    ResumePositionMode ResumePositionMode,
    double? PlaybackRateOverride,
    bool? LoudnessNormalizationOverride,
    bool? SmoothTrackTransitionsOverride,
    int? InterTrackSilenceMillisecondsOverride,
    PlaybackTempoAlgorithm? TempoAlgorithmOverride,
    int? RefreshIntervalMinutes,
    string? DownloadsFolder);

/// <summary>
/// Dane dialogu oraz wartosci rozstrzygniete dla odtwarzacza. Tytul jest
/// jedynym tekstem przeznaczonym do pokazania uzytkownikowi; identyfikatory
/// pozostaja w kontrakcie technicznym i nie sa etykietami kontrolek.
/// </summary>
public sealed record PodcastPlaybackOptionsSnapshot(
    PodcastPlaybackOptionsTarget Target,
    string ItemId,
    string Title,
    ResumePositionMode ResumePositionMode,
    double? PlaybackRateOverride,
    bool? LoudnessNormalizationOverride,
    bool? SmoothTrackTransitionsOverride,
    int? InterTrackSilenceMillisecondsOverride,
    PlaybackTempoAlgorithm? TempoAlgorithmOverride,
    int? RefreshIntervalMinutes,
    string? DownloadsFolder,
    bool ShouldRememberPosition,
    double? ResolvedPlaybackRateOverride,
    bool? ResolvedLoudnessNormalizationOverride,
    bool? ResolvedSmoothTrackTransitionsOverride,
    int? ResolvedInterTrackSilenceMillisecondsOverride,
    PlaybackTempoAlgorithm? ResolvedTempoAlgorithmOverride);

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
