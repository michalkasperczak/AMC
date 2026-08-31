namespace AccessibleMediaController.Windows;

internal static class MainWindowNavigationPolicy
{
    public static bool IsTransientRadioView(string sessionId, string viewName) =>
        string.Equals(sessionId, "radio", StringComparison.Ordinal)
        && string.Equals(viewName, "Nagrywane", StringComparison.Ordinal);
}
