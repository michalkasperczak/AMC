using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.LocalMedia;

public sealed record LocalLibrarySyncResult(
    IReadOnlyList<MediaItem> AddedItems,
    IReadOnlyList<MediaItem> RestoredItems,
    IReadOnlyList<MediaItem> BecameUnavailableItems,
    IReadOnlyList<MediaItem> ExcludedItems)
{
    public bool Changed =>
        AddedItems.Count > 0
        || RestoredItems.Count > 0
        || BecameUnavailableItems.Count > 0
        || ExcludedItems.Count > 0;
}

public static class LocalLibrarySynchronizer
{
    public static LocalLibrarySyncResult Synchronize(
        ICollection<MediaItem> catalog,
        IEnumerable<string> successfullyScannedRoots,
        IEnumerable<string> discoveredPaths,
        IEnumerable<string> excludedPaths)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(successfullyScannedRoots);
        ArgumentNullException.ThrowIfNull(discoveredPaths);
        ArgumentNullException.ThrowIfNull(excludedPaths);

        var roots = successfullyScannedRoots
            .Select(NormalizeFolderPath)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var discovered = discoveredPaths
            .Select(NormalizeFilePath)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exclusions = excludedPaths
            .Select(NormalizeFilePath)
            .Where(path => path.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownByPath = catalog
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .GroupBy(item => NormalizeFilePath(item.Source!), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var added = new List<MediaItem>();
        var restored = new List<MediaItem>();
        var unavailable = new List<MediaItem>();
        var excluded = new List<MediaItem>();

        foreach (var pair in knownByPath)
        {
            if (!roots.Any(root => IsSameOrDescendant(pair.Key, root))) continue;
            var item = pair.Value;
            if (!discovered.Contains(pair.Key))
            {
                if (item.IsAvailable)
                {
                    item.IsAvailable = false;
                    unavailable.Add(item);
                }
                continue;
            }

            if (!item.IsAvailable)
            {
                item.IsAvailable = true;
                restored.Add(item);
            }
            if (exclusions.Contains(pair.Key))
            {
                if (item.IsInLibrary)
                {
                    item.IsInLibrary = false;
                    excluded.Add(item);
                }
            }
            else if (!item.IsInLibrary)
            {
                item.IsInLibrary = true;
                restored.Add(item);
            }
        }

        foreach (var path in discovered)
        {
            if (knownByPath.ContainsKey(path) || exclusions.Contains(path)) continue;
            var item = new MediaItem
            {
                Id = $"local-{Guid.NewGuid():N}",
                Title = Path.GetFileNameWithoutExtension(path),
                Kind = MediaItemKind.Track,
                Source = path,
                IsInLibrary = true,
                IsAvailable = true
            };
            catalog.Add(item);
            knownByPath[path] = item;
            added.Add(item);
        }

        return new LocalLibrarySyncResult(
            added,
            restored.DistinctBy(item => item.Id).ToArray(),
            unavailable,
            excluded);
    }

    private static string NormalizeFolderPath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeFilePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
