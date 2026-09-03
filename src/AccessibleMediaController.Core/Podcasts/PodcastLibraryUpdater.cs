using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public sealed record PodcastLibraryUpdateResult(
    PodcastSubscriptionSettings Subscription,
    bool AddedSubscription,
    bool RestoredSubscription,
    int AddedEpisodes,
    int UpdatedEpisodes);

public static class PodcastLibraryUpdater
{
    public static PodcastLibraryUpdateResult Apply(
        PodcastSettings settings,
        PodcastFeedDocument feed,
        string? titleOverride,
        DateTime refreshUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(feed);
        var normalizedOverride = Normalize(titleOverride);
        var subscription = settings.Subscriptions.FirstOrDefault(candidate =>
                string.Equals(candidate.FeedUrl, feed.FeedUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            ?? settings.Subscriptions.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, feed.Id, StringComparison.Ordinal));
        var addedSubscription = subscription is null;
        var restoredSubscription = subscription is { IsInLibrary: false };
        if (subscription is null)
        {
            subscription = new PodcastSubscriptionSettings
            {
                Id = feed.Id,
                IsInLibrary = true
            };
            settings.Subscriptions.Add(subscription);
        }

        if (normalizedOverride.Length > 0)
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
        subscription.HomepageUrl = feed.HomepageUri?.AbsoluteUri;
        subscription.LastRefreshUtcTicks = refreshUtc.ToUniversalTime().Ticks;
        subscription.IsInLibrary = true;

        var initialInboxEpisodeId = addedSubscription
            ? feed.Episodes
                .OrderByDescending(episode => episode.Published ?? DateTimeOffset.MinValue)
                .FirstOrDefault()?.Id
              ?? feed.Episodes.FirstOrDefault()?.Id
            : null;

        var addedEpisodes = 0;
        var updatedEpisodes = 0;
        foreach (var source in feed.Episodes)
        {
            var episode = settings.Episodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, source.Id, StringComparison.Ordinal))
                ?? settings.Episodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.SubscriptionId, subscription.Id, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(source.SourceIdentifier)
                    && string.Equals(
                        candidate.SourceIdentifier,
                        source.SourceIdentifier,
                        StringComparison.Ordinal))
                ?? settings.Episodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.SubscriptionId, subscription.Id, StringComparison.Ordinal)
                    && string.Equals(candidate.MediaUrl, source.MediaUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
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
                    // discovered episode as new.
                    IsNew = !addedSubscription
                        || string.Equals(source.Id, initialInboxEpisodeId, StringComparison.Ordinal)
                };
                settings.Episodes.Add(episode);
                addedEpisodes++;
            }
            else
            {
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
            episode.PublishedUtcTicks = source.Published?.UtcDateTime.Ticks ?? 0;
            if (source.Duration > TimeSpan.Zero)
            {
                episode.DurationTicks = source.Duration.Ticks;
                if (episode.ResumePositionTicks > episode.DurationTicks)
                    episode.ResumePositionTicks = episode.DurationTicks;
            }
        }

        return new PodcastLibraryUpdateResult(
            subscription,
            addedSubscription,
            restoredSubscription,
            addedEpisodes,
            updatedEpisodes);
    }

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
