using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public enum PodcastMembershipCollection
{
    Library,
    Favorites
}

public static class PodcastMembershipToggle
{
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
