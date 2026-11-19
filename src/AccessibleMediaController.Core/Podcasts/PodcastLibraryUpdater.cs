using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public sealed record PodcastLibraryUpdateResult(
    PodcastSubscriptionSettings Subscription,
    bool AddedSubscription,
    bool RestoredSubscription,
    int AddedEpisodes,
    int UpdatedEpisodes,
    int RetainedEpisodesAbsentFromFeed);

public static class PodcastLibraryUpdater
{
    public static PodcastLibraryUpdateResult Apply(
        PodcastSettings settings,
        PodcastFeedDocument feed,
        string? titleOverride,
        DateTime refreshUtc,
        BookmarkSettings? bookmarks = null,
        PodcastSourceKind sourceKind = PodcastSourceKind.Rss,
        bool preserveExistingTitle = false,
        bool preserveExistingMetadata = false,
        // Domyslnie NIE dopisujemy do Biblioteki. Wczesniej bylo tu true, wiec
        // produkcyjne odswiezanie (F5, Ctrl+F5, timer), ktore wola bez tego
        // parametru, milczaco zapisywalo do Biblioteki kanal otwarty wcześniej
        // bez zapisu. Zapis nalezy do jawnych wejsc: recznego dodania, importu
        // OPML oraz Ctrl+Shift+L - one podaja addToLibrary: true wprost.
        bool addToLibrary = false)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(feed);
        var normalizedOverride = Normalize(titleOverride);
        var feedUrlComparison = sourceKind is PodcastSourceKind.YouTubeChannel
            or PodcastSourceKind.YouTubePlaylist
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var subscription = settings.Subscriptions.FirstOrDefault(candidate =>
                string.Equals(candidate.FeedUrl, feed.FeedUri.AbsoluteUri, feedUrlComparison))
            ?? settings.Subscriptions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, feed.Id, StringComparison.Ordinal));
        var addedSubscription = subscription is null;
        var restoredSubscription = addToLibrary && subscription is { IsInLibrary: false };
        if (subscription is null)
        {
            subscription = new PodcastSubscriptionSettings
            {
                Id = feed.Id,
                IsInLibrary = addToLibrary
            };
            settings.Subscriptions.Add(subscription);
        }

        // Zapamiętujemy ostatni POTWIERDZONY sukces przed aktualizacją go
        // poniżej. Gdy dostawca zwróci całkiem inne ograniczone okno bez
        // wspólnego identyfikatora, kolejność nie odróżnia archiwum od
        // nowości; tylko jawna data po tym sukcesie może ustawić IsNew.
        var previousSuccessfulRefreshUtcTicks = subscription.LastRefreshUtcTicks;
        if (!preserveExistingMetadata || addedSubscription)
        {
            if (normalizedOverride.Length > 0
                && (!preserveExistingTitle || addedSubscription || string.IsNullOrWhiteSpace(subscription.Title)))
            {
                subscription.Title = normalizedOverride;
                subscription.HasCustomTitle = true;
            }
            else if (!subscription.HasCustomTitle || string.IsNullOrWhiteSpace(subscription.Title))
            {
                subscription.Title = feed.Title;
            }
            subscription.Author = feed.Author;
            subscription.Description = feed.Description;
            subscription.FeedUrl = feed.FeedUri.AbsoluteUri;
            subscription.SourceKind = sourceKind;
            subscription.HomepageUrl = feed.HomepageUri?.AbsoluteUri;
            subscription.LastRefreshUtcTicks = refreshUtc.ToUniversalTime().Ticks;
        }
        // NIGDY nie zdejmujemy istniejacego czlonkostwa - kanal zapisany
        // wczesniej zostaje w Bibliotece takze przy otwarciu z wyszukiwania.
        if (addToLibrary) subscription.IsInLibrary = true;

        var initialInboxEpisodeId = addedSubscription
            ? feed.Episodes
                .OrderByDescending(episode => episode.Published ?? DateTimeOffset.MinValue)
                .FirstOrDefault()?.Id
              ?? feed.Episodes.FirstOrDefault()?.Id
            : null;

        // RSS is a current publication window, not the Podcast library itself.
        // Some publishers expose only their newest few episodes.  Refreshing
        // therefore merges the current feed into AMC's durable catalogue and
        // must never prune an episode merely because it is absent today.
        var existingSubscriptionEpisodes = settings.Episodes
            .Where(episode => string.Equals(
                episode.SubscriptionId,
                subscription.Id,
                StringComparison.Ordinal))
            .ToArray();
        var knownEpisodeIds = existingSubscriptionEpisodes
            .Select(episode => episode.Id)
            .ToHashSet(StringComparer.Ordinal);
        var episodesById = new Dictionary<string, PodcastEpisodeSettings>(StringComparer.Ordinal);
        foreach (var episode in settings.Episodes)
            episodesById.TryAdd(episode.Id, episode);
        var episodesBySourceIdentifier = new Dictionary<string, PodcastEpisodeSettings>(StringComparer.Ordinal);
        var episodesByMediaUrl = new Dictionary<string, PodcastEpisodeSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var episode in existingSubscriptionEpisodes)
        {
            if (!string.IsNullOrWhiteSpace(episode.SourceIdentifier))
                episodesBySourceIdentifier.TryAdd(episode.SourceIdentifier, episode);
            if (!string.IsNullOrWhiteSpace(episode.MediaUrl))
                episodesByMediaUrl.TryAdd(episode.MediaUrl, episode);
        }

        PodcastEpisodeSettings? FindExisting(PodcastFeedEpisode source)
        {
            if (episodesById.TryGetValue(source.Id, out var byId)) return byId;
            if (!string.IsNullOrWhiteSpace(source.SourceIdentifier)
                && episodesBySourceIdentifier.TryGetValue(source.SourceIdentifier, out var bySource))
                return bySource;
            return episodesByMediaUrl.TryGetValue(source.MediaUri.AbsoluteUri, out var byMedia)
                ? byMedia
                : null;
        }

        // Feeds are normally newest-first.  Entries before the first known one
        // are genuinely new; unseen entries after that boundary are historical
        // backfill.  This prevents a newly enlarged parser window from flooding
        // the inbox with thousands of old TyfloPodcast episodes.
        var firstKnownFeedOrdinal = -1;
        for (var index = 0; index < feed.Episodes.Count; index++)
        {
            if (FindExisting(feed.Episodes[index]) is null) continue;
            firstKnownFeedOrdinal = index;
            break;
        }
        var matchedKnownEpisodeIds = new HashSet<string>(StringComparer.Ordinal);
        var addedEpisodes = 0;
        var updatedEpisodes = 0;
        var recordsInCurrentFeedOrder = new List<PodcastEpisodeSettings>();
        var sourceOrdinal = 0;
        foreach (var source in feed.Episodes)
        {
            var episode = FindExisting(source);
            if (episode is null)
            {
                episode = new PodcastEpisodeSettings
                {
                    Id = source.Id,
                    SubscriptionId = subscription.Id,
                    // A new subscription must not flood the inbox with its
                    // complete archive. Its newest available episode is still
                    // useful as an immediately visible confirmation that the
                    // subscription works. Later refreshes mark every newly
                    // discovered episode as new, unless the bounded feed has
                    // no common identity: then its date must prove freshness.
                    IsNew = addedSubscription
                        ? string.Equals(source.Id, initialInboxEpisodeId, StringComparison.Ordinal)
                        : firstKnownFeedOrdinal >= 0
                            ? sourceOrdinal < firstKnownFeedOrdinal
                            : source.Published is { } published
                                && previousSuccessfulRefreshUtcTicks > 0
                                && published.UtcDateTime.Ticks > previousSuccessfulRefreshUtcTicks
                };
                settings.Episodes.Add(episode);
                episodesById.TryAdd(episode.Id, episode);
                if (!string.IsNullOrWhiteSpace(source.SourceIdentifier))
                    episodesBySourceIdentifier.TryAdd(source.SourceIdentifier, episode);
                episodesByMediaUrl.TryAdd(source.MediaUri.AbsoluteUri, episode);
                addedEpisodes++;
            }
            else
            {
                if (knownEpisodeIds.Contains(episode.Id))
                    matchedKnownEpisodeIds.Add(episode.Id);
                updatedEpisodes++;
            }

            episode.SubscriptionId = subscription.Id;
            episode.SourceIdentifier = source.SourceIdentifier;
            episode.Title = source.Title;
            episode.Author = source.Author;
            episode.Description = source.Description;
            episode.MediaUrl = source.MediaUri.AbsoluteUri;
            episode.PageUrl = source.PageUri?.AbsoluteUri;
            episode.MediaType = source.MediaType;
            episode.MediaLength = source.MediaLength;
            var chaptersUrl = source.ChaptersUri?.AbsoluteUri;
            if (!string.IsNullOrWhiteSpace(chaptersUrl)
                && !string.Equals(episode.ProviderChaptersUrl, chaptersUrl, StringComparison.OrdinalIgnoreCase))
            {
                episode.ProviderChaptersUrl = chaptersUrl;
                episode.ProviderChaptersLoadedUrl = null;
            }
            episode.PublishedUtcTicks = source.Published?.UtcDateTime.Ticks ?? 0;
            episode.FeedOrdinal = sourceKind is PodcastSourceKind.YouTubeChannel
                or PodcastSourceKind.YouTubePlaylist
                    ? sourceOrdinal
                    : null;
            recordsInCurrentFeedOrder.Add(episode);
            sourceOrdinal++;
            if (source.Duration > TimeSpan.Zero)
            {
                episode.DurationTicks = source.Duration.Ticks;
                if (episode.ResumePositionTicks > episode.DurationTicks)
                    episode.ResumePositionTicks = episode.DurationTicks;
            }
            var feedChapters = source.Chapters ?? [];
            if (bookmarks is not null && feedChapters.Count > 0)
            {
                new ChapterIndex(bookmarks).ReplaceProviderChapters(
                    "podcasts",
                    "Podcasty i YouTube",
                    ToMediaItem(episode, subscription.Title),
                    "podcast-feed",
                    feedChapters,
                    refreshUtc);
            }
            if (feedChapters.Count > 0) episode.HasFeedChapters = true;
        }

        if (sourceKind is PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist)
        {
            // A flat YouTube response often contains no publication dates. Its
            // entry order is nevertheless meaningful: /videos is newest first,
            // while a playlist follows the order selected by its publisher.
            // Keep that order durably and append archived entries which are no
            // longer present in YouTube's bounded current window.
            var currentIds = recordsInCurrentFeedOrder
                .Select(episode => episode.Id)
                .ToHashSet(StringComparer.Ordinal);
            var retainedArchive = settings.Episodes
                .Where(episode => string.Equals(
                        episode.SubscriptionId,
                        subscription.Id,
                        StringComparison.Ordinal)
                    && !currentIds.Contains(episode.Id))
                .ToArray();
            foreach (var archivedEpisode in retainedArchive) archivedEpisode.FeedOrdinal = null;

            var firstSubscriptionIndex = settings.Episodes.FindIndex(episode =>
                string.Equals(episode.SubscriptionId, subscription.Id, StringComparison.Ordinal));
            if (firstSubscriptionIndex < 0) firstSubscriptionIndex = settings.Episodes.Count;
            settings.Episodes.RemoveAll(episode => string.Equals(
                episode.SubscriptionId,
                subscription.Id,
                StringComparison.Ordinal));
            settings.Episodes.InsertRange(
                firstSubscriptionIndex,
                recordsInCurrentFeedOrder.Concat(retainedArchive));
        }

        return new PodcastLibraryUpdateResult(
            subscription,
            addedSubscription,
            restoredSubscription,
            addedEpisodes,
            updatedEpisodes,
            knownEpisodeIds.Count - matchedKnownEpisodeIds.Count);
    }

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static AccessibleMediaController.Core.Sessions.MediaItem ToMediaItem(
        PodcastEpisodeSettings episode,
        string podcastTitle) => new()
    {
        Id = episode.Id,
        Title = episode.Title,
        Artist = podcastTitle,
        Kind = AccessibleMediaController.Core.Sessions.MediaItemKind.Episode,
        Duration = TimeSpan.FromTicks(Math.Max(0, episode.DurationTicks)),
        Source = episode.DownloadPath ?? episode.MediaUrl
    };
}
