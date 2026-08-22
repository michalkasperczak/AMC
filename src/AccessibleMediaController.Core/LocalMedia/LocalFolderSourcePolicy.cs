using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.LocalMedia;

public enum LocalFolderSourceConflictKind
{
    SameSource,
    CoveredByExistingSource,
    ContainsExistingSource
}

public sealed record LocalFolderSourceConflict(
    LocalFolderSourceConflictKind Kind,
    LocalFolderSourceSettings ExistingSource);

public sealed record LocalFolderSourceStatus(
    string Id,
    string DisplayName,
    string Path,
    bool IsReachable,
    int ActiveItemCount,
    int UnavailableItemCount,
    int ExcludedItemCount,
    ResumePositionMode ResumePositionMode,
    string? OverlapWarning)
{
    public string ResumePositionLabel => ResumePositionMode switch
    {
        ResumePositionMode.Remember => "pozycja pamiętana",
        ResumePositionMode.StartFromBeginning => "zawsze od początku",
        _ => "pozycja według ustawienia ogólnego"
    };

    public string Label
    {
        get
        {
            var availability = IsReachable ? "dostępne" : "niedostępne — rekordy zachowane";
            var warning = string.IsNullOrWhiteSpace(OverlapWarning) ? string.Empty : $", uwaga: {OverlapWarning}";
            return $"{DisplayName}, {availability}, {ResumePositionLabel}, aktywne {ActiveItemCount}, niedostępne {UnavailableItemCount}, wykluczone {ExcludedItemCount}{warning}, {Path}";
        }
    }
}

/// <summary>
/// Keeps folder source registration deterministic. A media file may belong to
/// only one registered root, while detaching a root never mutates catalog rows.
/// </summary>
public static class LocalFolderSourcePolicy
{
    public static LocalFolderSourceConflict? FindConflict(
        IEnumerable<LocalFolderSourceSettings> sources,
        string candidatePath,
        string? sourceIdToIgnore = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var candidate = NormalizeFolderPath(candidatePath);
        foreach (var source in sources.Where(source =>
                     !string.Equals(source.Id, sourceIdToIgnore, StringComparison.Ordinal)))
        {
            var existing = NormalizeFolderPath(source.Path);
            if (string.Equals(candidate, existing, StringComparison.OrdinalIgnoreCase))
            {
                return new(LocalFolderSourceConflictKind.SameSource, source);
            }
            if (IsSameOrDescendant(candidate, existing))
            {
                return new(LocalFolderSourceConflictKind.CoveredByExistingSource, source);
            }
            if (IsSameOrDescendant(existing, candidate))
            {
                return new(LocalFolderSourceConflictKind.ContainsExistingSource, source);
            }
        }
        return null;
    }

    public static IReadOnlyList<LocalFolderSourceStatus> BuildStatuses(
        IEnumerable<LocalFolderSourceSettings> sources,
        IEnumerable<LocalMediaItemSettings> items,
        IEnumerable<string> excludedPaths,
        Func<string, bool>? isReachable = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(excludedPaths);
        isReachable ??= Directory.Exists;

        var sourceArray = sources.ToArray();
        var itemArray = items.ToArray();
        var exclusionArray = excludedPaths
            .Select(NormalizeFilePath)
            .Where(path => path.Length > 0)
            .ToArray();

        return sourceArray.Select(source =>
        {
            var root = NormalizeFolderPath(source.Path);
            var sourceItems = itemArray.Where(item =>
                    IsSameOrDescendant(NormalizeFilePath(item.Path), root))
                .ToArray();
            var conflict = FindConflict(sourceArray, root, source.Id);
            return new LocalFolderSourceStatus(
                source.Id,
                source.DisplayName,
                root,
                isReachable(root),
                sourceItems.Count(item => item.IsInLibrary && item.IsAvailable),
                sourceItems.Count(item => item.IsInLibrary && !item.IsAvailable),
                exclusionArray.Count(path => IsSameOrDescendant(path, root)),
                source.ResumePositionMode,
                DescribeOverlap(conflict));
        }).ToArray();
    }

    public static bool DetachSource(ICollection<LocalFolderSourceSettings> sources, string sourceId)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var source = sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.Ordinal));
        return source is not null && sources.Remove(source);
    }

    public static bool IsSameOrDescendant(string candidatePath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath)) return false;
        var candidate = NormalizeFolderPath(candidatePath);
        var root = NormalizeFolderPath(rootPath);
        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string? DescribeOverlap(LocalFolderSourceConflict? conflict) => conflict?.Kind switch
    {
        LocalFolderSourceConflictKind.CoveredByExistingSource =>
            $"źródło znajduje się wewnątrz „{conflict.ExistingSource.DisplayName}”",
        LocalFolderSourceConflictKind.ContainsExistingSource =>
            $"źródło obejmuje „{conflict.ExistingSource.DisplayName}”",
        LocalFolderSourceConflictKind.SameSource =>
            $"powtarza źródło „{conflict.ExistingSource.DisplayName}”",
        _ => null
    };

    private static string NormalizeFolderPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static string NormalizeFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
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
}
