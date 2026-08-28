using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

internal sealed record RadioLibraryMergeResult(
    IReadOnlyList<MediaItem> Added,
    IReadOnlyList<MediaItem> Promoted,
    int SkippedEntries);

internal static class RadioLibraryMerge
{
    public static RadioLibraryMergeResult Apply(
        IReadOnlyList<MediaItem> existingItems,
        RadioPlaylistImportResult import)
    {
        var byUrl = existingItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .GroupBy(item => item.Source!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(item => item.IsInLibrary) ?? group.First(),
                StringComparer.OrdinalIgnoreCase);
        var added = new List<MediaItem>();
        var promoted = new List<MediaItem>();
        var skipped = import.SkippedEntries;

        foreach (var station in import.Stations)
        {
            if (byUrl.TryGetValue(station.StreamUrl, out var existing))
            {
                if (existing.IsInLibrary)
                {
                    skipped++;
                    continue;
                }

                // A Radio Browser catalog entry is already part of the radio
                // session. Import means “show it in my Library”, not “discard
                // it as a duplicate”. Keep the richer catalog metadata/name.
                existing.IsInLibrary = true;
                existing.IsAvailable = true;
                existing.PublicUri ??= station.StreamUrl;
                promoted.Add(existing);
                continue;
            }

            var item = new MediaItem
            {
                Id = $"radio:imported:{Guid.NewGuid():N}",
                Title = station.Name,
                HasCustomTitle = true,
                Kind = MediaItemKind.Station,
                Source = station.StreamUrl,
                PublicUri = station.StreamUrl,
                IsInLibrary = true,
                IsAvailable = true
            };
            added.Add(item);
            byUrl.Add(station.StreamUrl, item);
        }

        return new RadioLibraryMergeResult(added, promoted, skipped);
    }
}
