using System.Globalization;

namespace AccessibleMediaController.Core.LocalMedia;

public static class LocalAudioFileDiscovery
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".mp2", ".wav", ".m4a", ".aac", ".flac", ".wma",
        ".ogg", ".opus", ".aif", ".aiff"
    };

    public const string DialogFilter =
        "Pliki audio|*.mp3;*.mp2;*.wav;*.m4a;*.aac;*.flac;*.wma;*.ogg;*.opus;*.aif;*.aiff|Wszystkie pliki|*.*";

    public static bool IsAudioFile(string path) =>
        AudioExtensions.Contains(Path.GetExtension(path));

    public static IReadOnlyList<string> FindFiles(string folderPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        return Directory
            .EnumerateFiles(folderPath, "*", options)
            .Where(IsAudioFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetRelativePath(folderPath, path), NaturalPathComparer.Instance)
            .ToArray();
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
