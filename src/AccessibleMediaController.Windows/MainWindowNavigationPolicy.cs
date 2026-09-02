namespace AccessibleMediaController.Windows;

internal enum PlayerDepartureReason
{
    ReturnToList,
    BrowserNavigation,
    SessionSwitch
}

internal static class MainWindowNavigationPolicy
{
    public static bool IsTransientRadioView(string sessionId, string viewName) =>
        string.Equals(sessionId, "radio", StringComparison.Ordinal)
        && string.Equals(viewName, "Nagrywane", StringComparison.Ordinal);

    public static bool ShouldPreservePlaybackContext(bool playerViewActive, string viewName) =>
        playerViewActive
        || string.Equals(viewName, "Zakładki", StringComparison.Ordinal);

    public static bool ShouldApplyPlaybackExitPolicy(PlayerDepartureReason reason) =>
        reason is not PlayerDepartureReason.SessionSwitch;
}
