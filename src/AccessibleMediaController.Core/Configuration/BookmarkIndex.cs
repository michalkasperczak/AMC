using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Configuration;

public readonly record struct BookmarkAddResult(BookmarkEntry Entry, bool Added);

public sealed class BookmarkIndex(BookmarkSettings settings)
{
    public const int MaxEntries = 5000;
    private static readonly TimeSpan DuplicateTolerance = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NavigationTolerance = TimeSpan.FromMilliseconds(250);

    public IReadOnlyList<BookmarkEntry> GetAll() => settings.Entries
        .OrderByDescending(entry => entry.CreatedUtcTicks)
        .ThenBy(entry => entry.Id, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<BookmarkEntry> GetForItem(string sessionId, string itemId) => settings.Entries
        .Where(entry => string.Equals(entry.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
        .OrderBy(entry => entry.PositionTicks)
        .ThenBy(entry => entry.CreatedUtcTicks)
        .ToArray();

    public BookmarkAddResult Add(
        string sessionId,
        string sessionName,
        MediaItem item,
        TimeSpan position,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(item);

        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (item.Duration > TimeSpan.Zero && clamped > item.Duration) clamped = item.Duration;
        var roundedTicks = TimeSpan.FromSeconds(Math.Round(clamped.TotalSeconds)).Ticks;
        var existing = GetForItem(sessionId, item.Id).FirstOrDefault(entry =>
            Math.Abs(entry.PositionTicks - roundedTicks) <= DuplicateTolerance.Ticks);
        if (existing is not null) return new BookmarkAddResult(existing, false);

        var entry = new BookmarkEntry
        {
            SessionId = sessionId,
            SessionName = string.IsNullOrWhiteSpace(sessionName) ? sessionId : sessionName,
            ItemId = item.Id,
            ItemTitle = item.Title,
            PositionTicks = roundedTicks,
            CreatedUtcTicks = utcNow.ToUniversalTime().Ticks
        };
        settings.Entries.Add(entry);
        if (settings.Entries.Count > MaxEntries)
        {
            var retainedIds = GetAll().Take(MaxEntries).Select(candidate => candidate.Id)
                .ToHashSet(StringComparer.Ordinal);
            settings.Entries.RemoveAll(candidate => !retainedIds.Contains(candidate.Id));
        }
        return new BookmarkAddResult(entry, true);
    }

    public BookmarkEntry? FindRelative(
        string sessionId,
        string itemId,
        TimeSpan currentPosition,
        int direction)
    {
        if (direction == 0) return null;
        var entries = GetForItem(sessionId, itemId);
        return direction > 0
            ? entries.FirstOrDefault(entry => entry.PositionTicks > currentPosition.Ticks + NavigationTolerance.Ticks)
            : entries.LastOrDefault(entry => entry.PositionTicks < currentPosition.Ticks - NavigationTolerance.Ticks);
    }

    public int Remove(IEnumerable<string> bookmarkIds)
    {
        var ids = bookmarkIds.ToHashSet(StringComparer.Ordinal);
        return settings.Entries.RemoveAll(entry => ids.Contains(entry.Id));
    }

    public void Normalize()
    {
        settings.Entries = (settings.Entries ?? [])
            .Where(entry => entry is not null
                && !string.IsNullOrWhiteSpace(entry.SessionId)
                && !string.IsNullOrWhiteSpace(entry.ItemId))
            .Select(entry =>
            {
                if (string.IsNullOrWhiteSpace(entry.Id)) entry.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(entry.SessionName)) entry.SessionName = entry.SessionId;
                if (string.IsNullOrWhiteSpace(entry.ItemTitle)) entry.ItemTitle = entry.ItemId;
                entry.PositionTicks = Math.Max(0, entry.PositionTicks);
                entry.CreatedUtcTicks = Math.Max(0, entry.CreatedUtcTicks);
                return entry;
            })
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.CreatedUtcTicks)
            .Take(MaxEntries)
            .ToList();
    }
}
