namespace AccessibleMediaController.Windows;

// A request may update its data cache after departure, but not the new UI context.
internal sealed record TidalInteractionContext(
    long NavigationVersion,
    string SessionId,
    string View,
    string? ItemId,
    bool PlayerActive)
{
    internal bool CanPresent(TidalInteractionContext current, bool windowAvailable) =>
        windowAvailable && this == current;

    // Membership completion only speaks; it never navigates. Its own preceding
    // write may remove the selected row, so do not suppress the next confirmation.
    internal bool CanAnnounceCollectionOutcome(TidalInteractionContext current, bool windowAvailable) =>
        windowAvailable && SessionId == current.SessionId && View == current.View
        && PlayerActive == current.PlayerActive;
}
