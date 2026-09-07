using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Podcasts;

public enum PodcastMembershipCollection
{
    Library,
    Favorites
}

public static class PodcastMembershipToggle
{
    public static IReadOnlyList<PodcastSubscriptionSettings> ResolveSubscriptions(
        PodcastSettings podcasts,
        IEnumerable<MediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(podcasts);
        ArgumentNullException.ThrowIfNull(items);

        var episodeParents = podcasts.Episodes
            .Where(episode => !string.IsNullOrWhiteSpace(episode.Id)
                && !string.IsNullOrWhiteSpace(episode.SubscriptionId))
            .GroupBy(episode => episode.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().SubscriptionId,
                StringComparer.Ordinal);
        var requestedIds = items
            .Select(item => item.Kind switch
            {
                MediaItemKind.Podcast => item.Id,
                MediaItemKind.Episode when episodeParents.TryGetValue(item.Id, out var parentId) => parentId,
                MediaItemKind.Episode => item.ExternalId,
                _ => null
            })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        return podcasts.Subscriptions
            .Where(subscription => requestedIds.Contains(subscription.Id))
            .ToArray();
    }

    public static bool Apply(
        IReadOnlyList<PodcastSubscriptionSettings> subscriptions,
        PodcastMembershipCollection collection)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);
        if (subscriptions.Count == 0) return false;

        var add = collection == PodcastMembershipCollection.Library
            ? subscriptions.Any(subscription => !subscription.IsInLibrary)
            : subscriptions.Any(subscription => !subscription.IsFavorite);
        foreach (var subscription in subscriptions)
        {
            if (collection == PodcastMembershipCollection.Library)
            {
                subscription.IsInLibrary = add;
                if (!add) subscription.IsFavorite = false;
            }
            else
            {
                subscription.IsFavorite = add;
                if (add) subscription.IsInLibrary = true;
            }
        }
        return add;
    }
}
