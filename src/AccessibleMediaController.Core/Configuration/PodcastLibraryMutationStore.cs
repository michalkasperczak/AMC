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
