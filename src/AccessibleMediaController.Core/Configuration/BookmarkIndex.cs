using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Configuration;

public readonly record struct BookmarkAddResult(BookmarkEntry Entry, bool Added, bool NameChanged = false);

public sealed class BookmarkIndex(BookmarkSettings settings)
{
    public const int MaxEntries = 5000;
    private static readonly TimeSpan DuplicateTolerance = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NavigationTolerance = TimeSpan.FromSeconds(2);

    public IReadOnlyList<BookmarkEntry> GetAll() => settings.Entries
        .OrderByDescending(entry => entry.CreatedUtcTicks)
        .ThenBy(entry => entry.Id, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<BookmarkEntry> GetForDisplay(string currentSessionId, string currentItemId) =>
        settings.Entries
            .OrderBy(entry => IsCurrentItem(entry, currentSessionId, currentItemId) ? 0 : 1)
            .ThenBy(entry => entry.SessionName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.ItemTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.PositionTicks)
            .ThenBy(entry => entry.CreatedUtcTicks)
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
        DateTime utcNow,
        string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(item);

        var clamped = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (item.Duration > TimeSpan.Zero && clamped > item.Duration) clamped = item.Duration;
        var roundedTicks = TimeSpan.FromSeconds(Math.Round(clamped.TotalSeconds)).Ticks;
        var normalizedName = NormalizeName(name);
        var existing = GetForItem(sessionId, item.Id).FirstOrDefault(entry =>
            Math.Abs(entry.PositionTicks - roundedTicks) <= DuplicateTolerance.Ticks);
        if (existing is not null)
        {
            var nameChanged = normalizedName.Length > 0
                && !string.Equals(existing.Name, normalizedName, StringComparison.CurrentCulture);
            if (nameChanged) existing.Name = normalizedName;
            return new BookmarkAddResult(existing, false, nameChanged);
        }

        var entry = new BookmarkEntry
        {
            SessionId = sessionId,
            SessionName = string.IsNullOrWhiteSpace(sessionName) ? sessionId : sessionName,
            ItemId = item.Id,
            ItemTitle = item.Title,
            Name = normalizedName,
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

    public BookmarkEntry? FindAdjacent(
        string sessionId,
        string itemId,
        string anchorBookmarkId,
        int direction)
    {
        if (direction == 0 || string.IsNullOrWhiteSpace(anchorBookmarkId)) return null;
        var entries = GetForItem(sessionId, itemId);
        var anchorIndex = -1;
        for (var index = 0; index < entries.Count; index++)
        {
            if (!string.Equals(entries[index].Id, anchorBookmarkId, StringComparison.Ordinal)) continue;
            anchorIndex = index;
            break;
        }
        if (anchorIndex < 0) return null;
        var targetIndex = anchorIndex + Math.Sign(direction);
        return targetIndex >= 0 && targetIndex < entries.Count ? entries[targetIndex] : null;
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
                entry.Name = NormalizeName(entry.Name);
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

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var normalized = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 200 ? normalized : normalized[..200].TrimEnd();
    }

    private static bool IsCurrentItem(
        BookmarkEntry entry,
        string currentSessionId,
        string currentItemId) =>
        string.Equals(entry.SessionId, currentSessionId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.ItemId, currentItemId, StringComparison.Ordinal);
}
