using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.LocalMedia;

public sealed record LocalLibraryImportResult(
    IReadOnlyList<MediaItem> ImportedItems,
    IReadOnlyList<MediaItem> AddedItems,
    IReadOnlyList<MediaItem> RestoredItems);

public static class LocalLibraryImporter
{
    public static LocalLibraryImportResult Import(
        ICollection<MediaItem> catalog,
        IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(paths);

        var knownByPath = catalog
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .GroupBy(item => item.Source!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var imported = new List<MediaItem>();
        var added = new List<MediaItem>();
        var restored = new List<MediaItem>();

        foreach (var path in paths
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (knownByPath.TryGetValue(path, out var existing))
            {
                if (!existing.IsInLibrary)
                {
                    existing.IsInLibrary = true;
                    restored.Add(existing);
                }
                imported.Add(existing);
                continue;
            }

            var item = new MediaItem
            {
                Id = $"local-{Guid.NewGuid():N}",
                Title = Path.GetFileNameWithoutExtension(path),
                Kind = MediaItemKind.Track,
                Source = path,
                IsInLibrary = true
            };
            catalog.Add(item);
            knownByPath[path] = item;
            imported.Add(item);
            added.Add(item);
        }

        return new LocalLibraryImportResult(imported, added, restored);
    }
}
