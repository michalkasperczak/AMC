using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

internal static class MainWindowMembershipPolicy
{
    public static MediaItem? ResolveCanonicalItem(
        string itemId,
        IEnumerable<MediaItem>? durableItems,
        IEnumerable<MediaItem>? sessionItems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var durableItem = durableItems?.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal));
        if (durableItem is not null) return durableItem;
        return sessionItems?.FirstOrDefault(item =>
            string.Equals(item.Id, itemId, StringComparison.Ordinal));
    }
}
