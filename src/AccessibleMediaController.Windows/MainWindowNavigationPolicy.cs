using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

internal enum PlayerDepartureReason
{
    ReturnToList,
    BrowserNavigation,
    SessionSwitch
}

internal enum MainWindowFocusRecoveryTarget
{
    None,
    Player,
    MediaList
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

    public static int ResolveListSelectionIndex(
        IReadOnlyList<(string ItemId, string ActionItemId)> rows,
        string? preferredItemId,
        int? fallbackIndex)
    {
        if (rows.Count == 0) return -1;

        if (!string.IsNullOrWhiteSpace(preferredItemId))
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (string.Equals(row.ItemId, preferredItemId, StringComparison.Ordinal)
                    || string.Equals(row.ActionItemId, preferredItemId, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return Math.Clamp(fallbackIndex ?? 0, 0, rows.Count - 1);
    }

    public static MainWindowFocusRecoveryTarget ResolveFocusRecoveryTarget(
        bool windowActive,
        bool ownedWindowActive,
        bool menuFocus,
        bool playerViewActive,
        bool playerFocusValid,
        bool browserFocusValid,
        bool nativeFocusValid = true)
    {
        if (!windowActive || ownedWindowActive || menuFocus)
            return MainWindowFocusRecoveryTarget.None;
        if (!nativeFocusValid)
        {
            return playerViewActive
                ? MainWindowFocusRecoveryTarget.Player
                : MainWindowFocusRecoveryTarget.MediaList;
        }
        if (playerViewActive)
        {
            return playerFocusValid
                ? MainWindowFocusRecoveryTarget.None
                : MainWindowFocusRecoveryTarget.Player;
        }
        return browserFocusValid
            ? MainWindowFocusRecoveryTarget.None
            : MainWindowFocusRecoveryTarget.MediaList;
    }

    public static bool IsPodcastLibraryLocation(string sessionId, string viewName) =>
        string.Equals(sessionId, PodcastSessionId, StringComparison.Ordinal)
        && (string.Equals(viewName, PodcastLibraryView, StringComparison.Ordinal)
            || viewName.StartsWith(PodcastContentsViewPrefix, StringComparison.Ordinal)
               && viewName.Length > PodcastContentsViewPrefix.Length);

    public static string? ResolvePodcastParentView(string sessionId, string viewName) =>
        string.Equals(sessionId, PodcastSessionId, StringComparison.Ordinal)
        && viewName.StartsWith(PodcastContentsViewPrefix, StringComparison.Ordinal)
        && viewName.Length > PodcastContentsViewPrefix.Length
            ? PodcastLibraryView
            : null;

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

    public static string ResolvePodcastSearchLandingView(
        MediaItem item,
        IEnumerable<string> availablePodcastIds)
    {
        if (item.Kind == MediaItemKind.Episode
            && !string.IsNullOrWhiteSpace(item.ExternalId)
            && availablePodcastIds.Contains(item.ExternalId, StringComparer.Ordinal))
        {
            return $"{PodcastContentsViewPrefix}{item.ExternalId}";
        }

        return PodcastLibraryView;
    }

    public static string ResolveRadioSearchLandingView(MediaItem item) =>
        item.IsFavorite
            ? "Ulubione"
            : "Biblioteka";

    public static string ResolveSafeSessionView(
        string sessionId,
        string requestedView,
        string podcastLibraryReturnView)
    {
        if (!string.Equals(requestedView, "Multimedia", StringComparison.Ordinal))
            return requestedView;
        if (string.Equals(sessionId, PodcastSessionId, StringComparison.Ordinal))
            return podcastLibraryReturnView;
        if (string.Equals(sessionId, "radio", StringComparison.Ordinal))
            return "Biblioteka";
        return requestedView;
    }

    public static string FormatPodcastSearchResult(
        MediaItem item,
        string formattedItem,
        string? parentPodcastTitle,
        bool parentPodcastInLibrary)
    {
        if (item.Kind == MediaItemKind.Podcast)
        {
            if (item.IsInLibrary) return $"{formattedItem}, w Bibliotece";
            return item.Id.StartsWith("podcast-directory:", StringComparison.Ordinal)
                ? $"{formattedItem}, katalog Apple Podcasts"
                : $"{formattedItem}, poza Biblioteką";
        }

        if (item.Kind != MediaItemKind.Episode) return formattedItem;

        var parts = new List<string> { formattedItem };
        if (!string.IsNullOrWhiteSpace(parentPodcastTitle)
            && !formattedItem.Contains(parentPodcastTitle, StringComparison.CurrentCultureIgnoreCase))
        {
            parts.Add($"podcast {parentPodcastTitle}");
        }
        parts.Add(parentPodcastInLibrary ? "podcast w Bibliotece" : "podcast poza Biblioteką");
        return string.Join(", ", parts);
    }

    public static string FormatPodcastAggregateEpisodeLabel(
        MediaItem item,
        string formattedItem,
        string? parentPodcastTitle)
    {
        var parentTitle = parentPodcastTitle?.Trim();
        if (item.Kind != MediaItemKind.Episode || string.IsNullOrWhiteSpace(parentTitle))
            return formattedItem;

        // When an episode has no author, the in-memory model already uses the
        // podcast title in Artist. Do not repeat it. Feeds such as Radio Gdańsk
        // provide both values, so an aggregate view must expose both of them.
        if (string.Equals(item.Artist?.Trim(), parentTitle, StringComparison.CurrentCultureIgnoreCase))
            return formattedItem;

        var episodeTitle = item.Title.Trim();
        if (episodeTitle.Length > 0
            && formattedItem.StartsWith(episodeTitle, StringComparison.CurrentCultureIgnoreCase)
            && (formattedItem.Length == episodeTitle.Length || formattedItem[episodeTitle.Length] == ','))
        {
            return $"{episodeTitle}, {parentTitle}{formattedItem[episodeTitle.Length..]}";
        }

        return $"{formattedItem}, {parentTitle}";
    }
}
