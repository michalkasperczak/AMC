using System.Security.Cryptography;
using System.Text;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Configuration;

public readonly record struct ChapterAddResult(BookmarkEntry Entry, bool Added, bool NameChanged = false);

public sealed record ProviderChapterPoint(string SourceId, string Name, TimeSpan Start);

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
    public const int MaximumProviderChaptersPerItem = 500;
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
            .GroupBy(entry => entry.PositionTicks)
            .Select(group => group
                .OrderBy(entry => entry.ChapterOrigin == ChapterOrigin.User ? 0 : 1)
                .ThenBy(entry => ProviderSourcePriority(entry.ChapterSourceId))
                .ThenBy(entry => entry.CreatedUtcTicks)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .First())
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

    public int ReplaceProviderChapters(
        string sessionId,
        string sessionName,
        MediaItem item,
        string sourceFamily,
        IEnumerable<ProviderChapterPoint> chapters,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFamily);
        ArgumentNullException.ThrowIfNull(chapters);

        var sourcePrefix = $"{sourceFamily.Trim()}:";
        var previous = settings.Entries
            .Where(entry => IsChapter(entry)
                && entry.ChapterOrigin == ChapterOrigin.Provider
                && string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ItemId, item.Id, StringComparison.Ordinal)
                && entry.ChapterSourceId?.StartsWith(sourcePrefix, StringComparison.Ordinal) == true)
            .ToArray();
        foreach (var entry in previous)
        {
            if ((entry.Purpose & BookmarkPurpose.Bookmark) != 0)
            {
                entry.Purpose &= ~BookmarkPurpose.Chapter;
                entry.ChapterSourceId = null;
                entry.ChapterOrigin = ChapterOrigin.User;
            }
            else
            {
                settings.Entries.Remove(entry);
            }
        }

        var duration = item.Duration > TimeSpan.Zero ? item.Duration : TimeSpan.MaxValue;
        var normalized = chapters
            .Where(point => point is not null
                && point.Start >= TimeSpan.Zero
                && point.Start < duration)
            .Select(point => new ProviderChapterPoint(
                NormalizeSourceId(point.SourceId, point.Name, point.Start),
                NormalizeName(string.IsNullOrWhiteSpace(point.Name) ? "Rozdział" : point.Name),
                TimeSpan.FromMilliseconds(Math.Round(point.Start.TotalMilliseconds / 100d) * 100d)))
            .OrderBy(point => point.Start)
            .ThenBy(point => point.Name, StringComparer.CurrentCultureIgnoreCase)
            .GroupBy(point => point.Start.Ticks)
            .Select(group => group.First())
            .Take(MaximumProviderChaptersPerItem)
            .ToArray();

        var added = 0;
        foreach (var point in normalized)
        {
            // A local chapter at the same time is the user's intentional
            // override. Keep it and do not create a nearly identical row.
            if (settings.Entries.Any(entry =>
                    IsChapter(entry)
                    && entry.ChapterOrigin == ChapterOrigin.User
                    && string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.ItemId, item.Id, StringComparison.Ordinal)
                    && Math.Abs(entry.PositionTicks - point.Start.Ticks) <= DuplicateTolerance.Ticks))
            {
                continue;
            }

            var sourceId = sourcePrefix + point.SourceId;
            settings.Entries.Add(new BookmarkEntry
            {
                Id = StableProviderEntryId(sessionId, item.Id, sourceId),
                SessionId = sessionId,
                SessionName = string.IsNullOrWhiteSpace(sessionName) ? sessionId : sessionName,
                ItemId = item.Id,
                ItemTitle = item.Title,
                Name = point.Name,
                PositionTicks = point.Start.Ticks,
                CreatedUtcTicks = utcNow.ToUniversalTime().Ticks,
                Purpose = BookmarkPurpose.Chapter,
                ChapterOrigin = ChapterOrigin.Provider,
                ChapterSourceId = sourceId
            });
            added++;
        }

        TrimProviderEntriesToCapacity();
        return added;
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

    private static string NormalizeSourceId(string? sourceId, string name, TimeSpan start)
    {
        var normalized = string.Join(' ', (sourceId ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0)
            normalized = $"{start.Ticks}:{name}";
        if (normalized.Length > 300) normalized = normalized[..300];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string StableProviderEntryId(string sessionId, string itemId, string sourceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{sessionId}\n{itemId}\n{sourceId}"));
        return $"chapter-provider:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static int ProviderSourcePriority(string? sourceId)
    {
        if (sourceId?.StartsWith("podcast-json:", StringComparison.Ordinal) == true) return 0;
        if (sourceId?.StartsWith("podcast-feed:", StringComparison.Ordinal) == true) return 1;
        if (sourceId?.StartsWith("embedded:", StringComparison.Ordinal) == true) return 2;
        return 3;
    }

    private void TrimProviderEntriesToCapacity()
    {
        var overflow = settings.Entries.Count - BookmarkIndex.MaxEntries;
        if (overflow <= 0) return;
        var removable = settings.Entries
            .Where(entry => entry.ChapterOrigin == ChapterOrigin.Provider
                && entry.Purpose == BookmarkPurpose.Chapter)
            .OrderBy(entry => entry.CreatedUtcTicks)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .Take(overflow)
            .ToArray();
        foreach (var entry in removable) settings.Entries.Remove(entry);
    }
}
