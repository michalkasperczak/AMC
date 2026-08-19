namespace AccessibleMediaController.Core.Configuration;

public sealed class PlaybackHistory(PlaybackHistorySettings settings)
{
    public const int MaxEntriesPerSession = 500;

    public IReadOnlyList<string> GetItemIds(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)
            || !settings.ItemIdsBySession.TryGetValue(sessionId, out var entries))
        {
            return [];
        }
        return entries;
    }

    public bool Record(string sessionId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(itemId)) return false;
        if (!settings.ItemIdsBySession.TryGetValue(sessionId, out var entries))
        {
            entries = [];
            settings.ItemIdsBySession[sessionId] = entries;
        }

        var existingIndex = entries.FindIndex(id => string.Equals(id, itemId, StringComparison.Ordinal));
        if (existingIndex == 0) return false;
        if (existingIndex > 0) entries.RemoveAt(existingIndex);
        entries.Insert(0, itemId);
        if (entries.Count > MaxEntriesPerSession)
        {
            entries.RemoveRange(MaxEntriesPerSession, entries.Count - MaxEntriesPerSession);
        }
        return true;
    }

    public void Remove(string sessionId, IEnumerable<string> itemIds)
    {
        if (!settings.ItemIdsBySession.TryGetValue(sessionId, out var entries)) return;
        var ids = itemIds.ToHashSet(StringComparer.Ordinal);
        entries.RemoveAll(ids.Contains);
        if (entries.Count == 0) settings.ItemIdsBySession.Remove(sessionId);
    }

    public void Normalize()
    {
        settings.ItemIdsBySession = new Dictionary<string, List<string>>(
            settings.ItemIdsBySession ?? new Dictionary<string, List<string>>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var sessionId in settings.ItemIdsBySession.Keys.ToArray())
        {
            var normalized = (settings.ItemIdsBySession[sessionId] ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Take(MaxEntriesPerSession)
                .ToList();
            if (normalized.Count == 0) settings.ItemIdsBySession.Remove(sessionId);
            else settings.ItemIdsBySession[sessionId] = normalized;
        }
    }
}
