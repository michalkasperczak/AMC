namespace AccessibleMediaController.Windows;

internal enum PlayerDepartureReason
{
    ReturnToList,
    BrowserNavigation,
    SessionSwitch
}

internal static class MainWindowNavigationPolicy
{
    public static string FormatFocusedListEntry(
        string itemLabel,
        string? prefix = null,
        string? suffix = null) =>
        string.Join(
            ", ",
            new[] { prefix, itemLabel, suffix }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

    public static bool IsTransientRadioView(string sessionId, string viewName) =>
        string.Equals(sessionId, "radio", StringComparison.Ordinal)
        && string.Equals(viewName, "Nagrywane", StringComparison.Ordinal);

    public static bool ShouldPreservePlaybackContext(bool playerViewActive, string viewName) =>
        playerViewActive
        || string.Equals(viewName, "Zakładki", StringComparison.Ordinal);

    public static bool ShouldApplyPlaybackExitPolicy(PlayerDepartureReason reason) =>
        reason is PlayerDepartureReason.ReturnToList;
}
