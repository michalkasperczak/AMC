using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

internal enum PlayerDepartureReason
{
    ReturnToList,
    BrowserNavigation,
    SessionSwitch
}

internal static class MainWindowNavigationPolicy
{
    private const string PodcastSessionId = "podcasts";
    private const string PodcastLibraryView = "Biblioteka";
    private const string PodcastContentsViewPrefix = "Podcast:";

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

    public static bool IsPodcastLibraryLocation(string sessionId, string viewName) =>
        string.Equals(sessionId, PodcastSessionId, StringComparison.Ordinal)
        && (string.Equals(viewName, PodcastLibraryView, StringComparison.Ordinal)
            || viewName.StartsWith(PodcastContentsViewPrefix, StringComparison.Ordinal)
               && viewName.Length > PodcastContentsViewPrefix.Length);

    public static string ResolvePodcastLibraryReturnView(
        string? rememberedView,
        IEnumerable<string> availablePodcastIds)
    {
        if (string.Equals(rememberedView, PodcastLibraryView, StringComparison.Ordinal))
            return PodcastLibraryView;
        if (rememberedView is null
            || !rememberedView.StartsWith(PodcastContentsViewPrefix, StringComparison.Ordinal)
            || rememberedView.Length <= PodcastContentsViewPrefix.Length)
        {
            return PodcastLibraryView;
        }

        var podcastId = rememberedView[PodcastContentsViewPrefix.Length..];
        return availablePodcastIds.Contains(podcastId, StringComparer.Ordinal)
            ? rememberedView
            : PodcastLibraryView;
    }

    public static string? ResolveRelatedPodcastId(string sessionId, MediaItem? item) =>
        string.Equals(sessionId, PodcastSessionId, StringComparison.Ordinal)
        && item?.Kind == MediaItemKind.Episode
        && !string.IsNullOrWhiteSpace(item.ExternalId)
            ? item.ExternalId
            : null;
}
