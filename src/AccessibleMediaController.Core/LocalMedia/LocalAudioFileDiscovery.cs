using System.Globalization;

namespace AccessibleMediaController.Core.LocalMedia;

public static class LocalAudioFileDiscovery
{
    // Windows SDK attributes not exposed by every target framework's enum.
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes Pinned = (FileAttributes)0x00080000;
    private const FileAttributes Unpinned = (FileAttributes)0x00100000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    private const FileAttributes CloudPlaceholderAttributes =
        FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess | Pinned | Unpinned;
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".mp2", ".wav", ".m4a", ".aac", ".flac", ".wma",
        ".ogg", ".opus", ".aif", ".aiff"
    };

    public const string DialogFilter =
        "Pliki audio|*.mp3;*.mp2;*.wav;*.m4a;*.aac;*.flac;*.wma;*.ogg;*.opus;*.aif;*.aiff|Wszystkie pliki|*.*";

    public static bool IsAudioFile(string path) =>
        AudioExtensions.Contains(Path.GetExtension(path));

    public static int? EstimateBitrateKbps(long fileSizeBytes, TimeSpan duration)
    {
        if (fileSizeBytes <= 0 || duration <= TimeSpan.Zero) return null;
        var kilobitsPerSecond = fileSizeBytes * 8d / duration.TotalSeconds / 1000d;
        if (!double.IsFinite(kilobitsPerSecond) || kilobitsPerSecond <= 0) return null;
        return Math.Max(1, (int)Math.Round(Math.Min(kilobitsPerSecond, int.MaxValue)));
    }

    public static IReadOnlyList<string> FindFiles(string folderPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = 0
        };

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;

            FileSystemInfo[] entries;
            try
            {
                entries = new DirectoryInfo(current)
                    .EnumerateFileSystemInfos("*", options)
                    .ToArray();
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try
                {
                    attributes = entry.Attributes;
                }
                catch (Exception exception) when (
                    exception is IOException
                        or UnauthorizedAccessException
                        or FileNotFoundException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (IsSymbolicDirectory(entry, attributes)) continue;
                    pending.Push(entry.FullName);
                    continue;
                }

                // Cloud Files placeholders (including iCloud) are reparse-point
                // files. Reading only their path and attributes indexes them
                // without opening or hydrating the audio payload.
                if (IsAudioFile(entry.FullName)) files.Add(entry.FullName);
            }
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetRelativePath(root, path), NaturalPathComparer.Instance)
            .ToArray();
    }

    private static bool IsSymbolicDirectory(FileSystemInfo entry, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) == 0) return false;
        try
        {
            // Cloud-provider directories have ReparsePoint but no LinkTarget.
            // Symbolic links and junctions expose a target and are skipped to
            // prevent cycles and accidental traversal outside the source.
            return entry.LinkTarget is not null;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            // Some Cloud Files providers reject LinkTarget queries even though
            // directory enumeration is safe and does not hydrate file data.
            // Unknown reparse points remain excluded; known placeholder flags
            // provide a conservative fallback for OneDrive and similar roots.
            return (attributes & CloudPlaceholderAttributes) == 0;
        }
    }

    private sealed class NaturalPathComparer : IComparer<string>
    {
        public static NaturalPathComparer Instance { get; } = new();

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
                    var leftSignificant = SkipLeadingZeroes(left, leftIndex, leftEnd);
                    var rightSignificant = SkipLeadingZeroes(right, rightIndex, rightEnd);
                    var leftLength = leftEnd - leftSignificant;
                    var rightLength = rightEnd - rightSignificant;

                    if (leftLength != rightLength) return leftLength.CompareTo(rightLength);
                    for (var index = 0; index < leftLength; index++)
                    {
                        var digitComparison = left[leftSignificant + index].CompareTo(right[rightSignificant + index]);
                        if (digitComparison != 0) return digitComparison;
                    }

                    var runLengthComparison = (leftEnd - leftIndex).CompareTo(rightEnd - rightIndex);
                    if (runLengthComparison != 0) return runLengthComparison;
                    leftIndex = leftEnd;
                    rightIndex = rightEnd;
                    continue;
                }

                var leftElement = StringInfo.GetNextTextElement(left, leftIndex);
                var rightElement = StringInfo.GetNextTextElement(right, rightIndex);
                var textComparison = CultureInfo.CurrentCulture.CompareInfo.Compare(
                    leftElement,
                    rightElement,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                if (textComparison != 0) return textComparison;
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

        private static int SkipLeadingZeroes(string value, int start, int end)
        {
            var index = start;
            while (index < end - 1 && value[index] == '0') index++;
            return index;
        }
    }
}
