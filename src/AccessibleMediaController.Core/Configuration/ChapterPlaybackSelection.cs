namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Describes how an active selection of chapters relates to a position chosen
/// manually by the user. A manual boundary means that the current, unselected
/// chapter may finish, after which playback returns to the selected sequence.
/// </summary>
public readonly record struct ChapterPlaybackAlignment(
    int SelectedIndex,
    ChapterSegment? ManualChapter,
    TimeSpan? ManualBoundary)
{
    public bool IsInsideUnselectedRange => ManualBoundary is not null;
}

public static class ChapterPlaybackSelection
{
    private static readonly TimeSpan BoundaryTolerance = TimeSpan.FromMilliseconds(40);

    public static ChapterPlaybackAlignment Align(
        IReadOnlyList<ChapterSegment> selectedChapters,
        IReadOnlyList<ChapterSegment> allChapters,
        TimeSpan position,
        TimeSpan itemDuration)
    {
        ArgumentNullException.ThrowIfNull(selectedChapters);
        ArgumentNullException.ThrowIfNull(allChapters);
        if (selectedChapters.Count == 0)
            return new ChapterPlaybackAlignment(-1, null, null);

        var selectedIndex = FindContainingIndex(selectedChapters, position);
        if (selectedIndex >= 0)
            return new ChapterPlaybackAlignment(selectedIndex, null, null);

        var currentChapter = allChapters.FirstOrDefault(chapter => Contains(chapter, position));
        var boundary = currentChapter?.End;
        if (boundary is null)
        {
            boundary = allChapters
                .Where(chapter => chapter.Start > position)
                .Select(chapter => (TimeSpan?)chapter.Start)
                .FirstOrDefault()
                ?? (itemDuration > position ? itemDuration : position);
        }

        var previousSelectedIndex = 0;
        for (var index = selectedChapters.Count - 1; index >= 0; index--)
        {
            if (selectedChapters[index].Start <= position + BoundaryTolerance)
            {
                previousSelectedIndex = index;
                break;
            }
        }

        return new ChapterPlaybackAlignment(
            previousSelectedIndex,
            currentChapter,
            boundary);
    }

    public static int FindNextSelectedIndex(
        IReadOnlyList<ChapterSegment> selectedChapters,
        TimeSpan completedBoundary)
    {
        ArgumentNullException.ThrowIfNull(selectedChapters);
        for (var index = 0; index < selectedChapters.Count; index++)
        {
            if (selectedChapters[index].Start >= completedBoundary - BoundaryTolerance)
                return index;
        }
        return -1;
    }

    private static int FindContainingIndex(IReadOnlyList<ChapterSegment> chapters, TimeSpan position)
    {
        for (var index = 0; index < chapters.Count; index++)
        {
            if (Contains(chapters[index], position)) return index;
        }
        return -1;
    }

    private static bool Contains(ChapterSegment chapter, TimeSpan position) =>
        position >= chapter.Start
        && (position < chapter.End
            || chapter.End == chapter.Start && position == chapter.Start);
}
