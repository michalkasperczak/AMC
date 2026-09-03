using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Defines the stable, automatic orders available in the podcast inbox.
/// The Custom enum value is intentionally presented as grouping by podcast in
/// this view; unlike a playlist, the inbox never supports manual reordering.
/// </summary>
public static class PodcastInboxOrdering
{
    public static IReadOnlyList<PodcastEpisodeSettings> Order(
        IEnumerable<PodcastEpisodeSettings> source,
        IEnumerable<PodcastSubscriptionSettings> subscriptions,
        CollectionSortMode mode)
    {
        var podcastTitles = subscriptions.ToDictionary(
            subscription => subscription.Id,
            subscription => subscription.Title,
            StringComparer.Ordinal);
        var episodes = source.ToArray();

        return mode switch
        {
            CollectionSortMode.Alphabetical => episodes
                .OrderBy(episode => episode.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(episode => episode.PublishedUtcTicks)
                .ThenBy(episode => episode.Id, StringComparer.Ordinal)
                .ToArray(),
            CollectionSortMode.Custom => episodes
                .OrderBy(
                    episode => podcastTitles.GetValueOrDefault(episode.SubscriptionId, string.Empty),
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(episode => episode.PublishedUtcTicks)
                .ThenBy(episode => episode.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(episode => episode.Id, StringComparer.Ordinal)
                .ToArray(),
            _ => episodes
                .OrderByDescending(episode => episode.PublishedUtcTicks)
                .ThenBy(episode => episode.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(episode => episode.Id, StringComparer.Ordinal)
                .ToArray()
        };
    }
}
