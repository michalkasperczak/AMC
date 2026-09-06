using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Keeps the station picker tied to an intentional user-visible scope.
/// Radio Browser search results live in the session catalogue for reuse, but
/// must not silently appear in a picker opened from Favorites or a playlist.
/// </summary>
internal static class RadioScheduleStationSelection
{
    public static IReadOnlyList<MediaItem> ForCurrentView(
        IEnumerable<MediaItem> visibleItems,
        MediaItem selectedStation) =>
        DistinctStations(visibleItems.Append(selectedStation));

    /// <summary>
    /// The active-recordings list is a transient status view, not a station
    /// collection.  A schedule opened there keeps the focused recording as
    /// the initial choice, but offers the complete Radio Library instead of
    /// accidentally limiting the picker to stations that happen to be
    /// recording at that moment.
    /// </summary>
    public static IReadOnlyList<MediaItem> ForActiveRecordingsView(
        IEnumerable<MediaItem> sessionItems,
        MediaItem selectedStation) =>
        DistinctStations(
            sessionItems
                .Where(item => item.IsInLibrary)
                .Prepend(selectedStation));

    public static IReadOnlyList<MediaItem> ForScheduleManager(
        IEnumerable<MediaItem> sessionItems,
        IEnumerable<MediaItem> visibleItems,
        MediaItem? currentStation,
        IEnumerable<RadioRecordingScheduleSettings> schedules)
    {
        var scheduleList = schedules.ToArray();
        var referencedIds = scheduleList
            .Select(schedule => schedule.StationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var referencedUrls = scheduleList
            .Select(schedule => schedule.StreamUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = visibleItems
            .Concat(sessionItems.Where(item => item.IsInLibrary
                || item.IsFavorite
                || referencedIds.Contains(item.Id)
                || item.Source is { Length: > 0 } source && referencedUrls.Contains(source)));
        if (currentStation is not null) candidates = candidates.Append(currentStation);

        var result = DistinctStations(candidates).ToList();
        foreach (var schedule in scheduleList)
        {
            if (!IsValidStation(schedule.StationId, schedule.StreamUrl)
                || result.Any(item => string.Equals(item.Id, schedule.StationId, StringComparison.Ordinal)
                    || string.Equals(item.Source, schedule.StreamUrl, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            result.Add(new MediaItem
            {
                Id = schedule.StationId,
                Title = schedule.StationName,
                Kind = MediaItemKind.Station,
                Source = schedule.StreamUrl,
                PublicUri = schedule.StreamUrl,
                IsAvailable = true
            });
        }
        return result;
    }

    private static IReadOnlyList<MediaItem> DistinctStations(IEnumerable<MediaItem> items) =>
        items
            .Where(item => IsValidStation(item.Id, item.Source) && item.Kind == MediaItemKind.Station)
            .DistinctBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

    private static bool IsValidStation(string? id, string? source) =>
        !string.IsNullOrWhiteSpace(id)
        && Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}
