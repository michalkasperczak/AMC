using System.Globalization;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.LocalMedia;

public sealed record LocalAlbumGroup(
    string Id,
    string Title,
    string Artist,
    string FolderPath,
    TimeSpan Duration,
    IReadOnlyList<MediaItem> Tracks);

public static class LocalAlbumInference
{
    public static IReadOnlyList<LocalAlbumGroup> Infer(
        IEnumerable<MediaItem> items,
        IEnumerable<string>? sourceRoots = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        var roots = NormalizeRoots(sourceRoots);
        return items
            .Where(item => item.Kind == MediaItemKind.Track)
            .Select(item => (Item: item, Folder: GetFolder(item.Source)))
            .Where(pair => pair.Folder is not null)
            .GroupBy(pair => pair.Folder!, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateAlbum(group.Key, group.Select(pair => pair.Item), roots))
            .Where(album => album is not null)
            .Select(album => album!)
            .OrderBy(album => album.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(album => album.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(album => album.FolderPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<MediaItem> OrderTracks(IEnumerable<MediaItem> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        return tracks
            .Select((track, index) =>
            {
                var hasNumber = TryGetTrackNumber(track.Source, out var number);
                return (track, index, hasNumber, number);
            })
            .OrderBy(pair => pair.hasNumber ? 0 : 1)
            .ThenBy(pair => pair.number)
            .ThenBy(
                pair => Path.GetFileName(pair.track.Source) ?? string.Empty,
                NaturalTextComparer.Instance)
            .ThenBy(pair => pair.index)
            .Select(pair => pair.track)
            .ToArray();
    }

    public static bool TryGetTrackNumber(string? path, out int trackNumber)
    {
        trackNumber = 0;
        if (string.IsNullOrWhiteSpace(path)) return false;
        var name = Path.GetFileNameWithoutExtension(path).TrimStart();
        if (name.Length == 0 || !char.IsDigit(name[0])) return false;

        var digitCount = 1;
        while (digitCount < name.Length && digitCount < 3 && char.IsDigit(name[digitCount]))
        {
            digitCount++;
        }
        if (digitCount > 2) return false;
        if (digitCount < name.Length
            && name[digitCount] is not (' ' or '-' or '_' or '.' or ')'))
        {
            return false;
        }
        if (!int.TryParse(name[..digitCount], NumberStyles.None, CultureInfo.InvariantCulture, out trackNumber))
        {
            return false;
        }
        return trackNumber is >= 1 and <= 99;
    }

    private static LocalAlbumGroup? CreateAlbum(
        string folderPath,
        IEnumerable<MediaItem> tracks,
        IReadOnlyList<string> sourceRoots)
    {
        var trackArray = tracks.ToArray();
        var numbered = trackArray
            .Select(track => TryGetTrackNumber(track.Source, out var number) ? number : (int?)null)
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .ToArray();
        if (trackArray.Length < 2
            || numbered.Distinct().Count() < 2
            || numbered.Length * 2 < trackArray.Length)
        {
            return null;
        }

        var title = new DirectoryInfo(folderPath).Name;
        if (string.IsNullOrWhiteSpace(title)) return null;
        var orderedTracks = OrderTracks(trackArray);
        var duration = orderedTracks.All(track => track.Duration > TimeSpan.Zero)
            ? TimeSpan.FromTicks(orderedTracks.Sum(track => track.Duration.Ticks))
            : TimeSpan.Zero;
        return new LocalAlbumGroup(
            $"local-album:{folderPath.ToUpperInvariant()}",
            title,
            InferArtist(folderPath, sourceRoots),
            folderPath,
            duration,
            orderedTracks);
    }

    private static string InferArtist(string albumFolder, IReadOnlyList<string> sourceRoots)
    {
        var root = sourceRoots
            .Where(candidate => IsSameOrDescendant(albumFolder, candidate))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        var parent = Directory.GetParent(albumFolder);
        if (parent is null
            || root is null
            || string.Equals(albumFolder, root, StringComparison.OrdinalIgnoreCase)
            || string.Equals(parent.FullName, root, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        return parent.Name;
    }

    private static string? GetFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(path));
            return string.IsNullOrWhiteSpace(folder)
                ? null
                : Path.TrimEndingDirectorySeparator(folder);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> NormalizeRoots(IEnumerable<string>? roots) =>
        (roots ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
            {
                try
                {
                    return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    return string.Empty;
                }
            })
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsSameOrDescendant(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return true;
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NaturalTextComparer : IComparer<string>
    {
        public static NaturalTextComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            var leftIndex = 0;
            var rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                if (char.IsDigit(left[leftIndex]) && char.IsDigit(right[rightIndex]))
                {
                    var leftEnd = DigitRunEnd(left, leftIndex);
                    var rightEnd = DigitRunEnd(right, rightIndex);
                    var leftNumber = left[leftIndex..leftEnd].TrimStart('0');
                    var rightNumber = right[rightIndex..rightEnd].TrimStart('0');
                    if (leftNumber.Length == 0) leftNumber = "0";
                    if (rightNumber.Length == 0) rightNumber = "0";
                    var lengthComparison = leftNumber.Length.CompareTo(rightNumber.Length);
                    if (lengthComparison != 0) return lengthComparison;
                    var numberComparison = string.CompareOrdinal(leftNumber, rightNumber);
                    if (numberComparison != 0) return numberComparison;
                    leftIndex = leftEnd;
                    rightIndex = rightEnd;
                    continue;
                }

                var leftElement = StringInfo.GetNextTextElement(left, leftIndex);
                var rightElement = StringInfo.GetNextTextElement(right, rightIndex);
                var comparison = CultureInfo.CurrentCulture.CompareInfo.Compare(
                    leftElement,
                    rightElement,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                if (comparison != 0) return comparison;
                leftIndex += leftElement.Length;
                rightIndex += rightElement.Length;
            }
            return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        }

        private static int DigitRunEnd(string value, int start)
        {
            var index = start;
            while (index < value.Length && char.IsDigit(value[index])) index++;
            return index;
        }
    }
}
