using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Configuration;

public readonly record struct ChapterAddResult(BookmarkEntry Entry, bool Added, bool NameChanged = false);

public sealed record ChapterSegment(BookmarkEntry Entry, TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
    public string Name => string.IsNullOrWhiteSpace(Entry.Name)
        ? $"Rozdział od {FormatTime(Start)}"
        : Entry.Name;

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? value.ToString(@"h\:mm\:ss")
        : value.ToString(@"m\:ss");
}

/// <summary>
/// Provides chapter semantics over the same durable time-point records that
/// are also used for bookmarks. A point can be a bookmark, a chapter, or both.
/// </summary>
public sealed class ChapterIndex(BookmarkSettings settings)
{
    private static readonly TimeSpan DuplicateTolerance = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NavigationTolerance = TimeSpan.FromMilliseconds(250);

    public IReadOnlyList<ChapterSegment> GetForItem(
        string sessionId,
        string itemId,
        TimeSpan itemDuration)
    {
        var points = settings.Entries
            .Where(entry => IsChapter(entry)
                && string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
            .OrderBy(entry => entry.PositionTicks)
            .ThenBy(entry => entry.CreatedUtcTicks)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToArray();

        var duration = itemDuration < TimeSpan.Zero ? TimeSpan.Zero : itemDuration;
        var segments = new List<ChapterSegment>(points.Length);
        for (var index = 0; index < points.Length; index++)
        {
            var start = TimeSpan.FromTicks(Math.Max(0, points[index].PositionTicks));
            var end = index + 1 < points.Length
                ? TimeSpan.FromTicks(Math.Max(points[index].PositionTicks, points[index + 1].PositionTicks))
                : duration;
            if (duration > TimeSpan.Zero)
            {
                if (start > duration) start = duration;
                if (end > duration) end = duration;
            }
            segments.Add(new ChapterSegment(points[index], start, end));
        }
        return segments;
    }

    public ChapterAddResult AddUserChapter(
        string sessionId,
        string sessionName,
        MediaItem item,
        TimeSpan position,
        DateTime utcNow,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (item.Duration > TimeSpan.Zero && clamped > item.Duration) clamped = item.Duration;
        var roundedTicks = TimeSpan.FromMilliseconds(Math.Round(clamped.TotalMilliseconds / 100d) * 100d).Ticks;
        var normalizedName = NormalizeName(name);
        var existing = settings.Entries.FirstOrDefault(entry =>
            string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.ItemId, item.Id, StringComparison.Ordinal)
            && Math.Abs(entry.PositionTicks - roundedTicks) <= DuplicateTolerance.Ticks);
        if (existing is not null)
        {
            var wasChapter = IsChapter(existing);
            var nameChanged = !string.Equals(existing.Name, normalizedName, StringComparison.CurrentCulture);
            existing.Purpose |= BookmarkPurpose.Chapter;
            existing.ChapterOrigin = ChapterOrigin.User;
            existing.ChapterSourceId = null;
            existing.Name = normalizedName;
            return new ChapterAddResult(existing, !wasChapter, nameChanged);
        }

        var entry = new BookmarkEntry
        {
            SessionId = sessionId,
            SessionName = string.IsNullOrWhiteSpace(sessionName) ? sessionId : sessionName,
            ItemId = item.Id,
            ItemTitle = item.Title,
            Name = normalizedName,
            PositionTicks = roundedTicks,
            CreatedUtcTicks = utcNow.ToUniversalTime().Ticks,
            Purpose = BookmarkPurpose.Chapter,
            ChapterOrigin = ChapterOrigin.User
        };
        settings.Entries.Add(entry);
        if (settings.Entries.Count > BookmarkIndex.MaxEntries)
        {
            var remove = settings.Entries
                .OrderBy(candidate => candidate.CreatedUtcTicks)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .First();
            settings.Entries.Remove(remove);
        }
        return new ChapterAddResult(entry, true);
    }

    public ChapterSegment? FindRelative(
        string sessionId,
        string itemId,
        TimeSpan itemDuration,
        TimeSpan currentPosition,
        int direction)
    {
        if (direction == 0) return null;
        var chapters = GetForItem(sessionId, itemId, itemDuration);
        return direction > 0
            ? chapters.FirstOrDefault(chapter => chapter.Start > currentPosition + NavigationTolerance)
            : chapters.LastOrDefault(chapter => chapter.Start < currentPosition - NavigationTolerance);
    }

    public int RemoveUserChapters(IEnumerable<string> chapterIds)
    {
        var ids = chapterIds.ToHashSet(StringComparer.Ordinal);
        var changed = 0;
        for (var index = settings.Entries.Count - 1; index >= 0; index--)
        {
            var entry = settings.Entries[index];
            if (!ids.Contains(entry.Id) || !IsChapter(entry) || entry.ChapterOrigin != ChapterOrigin.User) continue;
            if ((entry.Purpose & BookmarkPurpose.Bookmark) != 0)
            {
                entry.Purpose &= ~BookmarkPurpose.Chapter;
                entry.ChapterSourceId = null;
            }
            else
            {
                settings.Entries.RemoveAt(index);
            }
            changed++;
        }
        return changed;
    }

    private static bool IsChapter(BookmarkEntry entry) =>
        (entry.Purpose & BookmarkPurpose.Chapter) != 0;

    private static string NormalizeName(string name)
    {
        var normalized = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 200 ? normalized : normalized[..200].TrimEnd();
    }
}
