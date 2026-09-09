namespace AccessibleMediaController.Core.Sessions;

public sealed record QueuePersistenceSnapshot(
    IReadOnlyList<string> StorageOrder,
    IReadOnlyList<string> RegularItemIds,
    IReadOnlyList<string> PlayNextItemIds,
    IReadOnlyList<string> RuntimeOrder);

/// <summary>
/// Preserves a local AMC queue when a remote service replaces its catalog
/// objects during synchronization. The service identity is stable even when
/// the same track has different occurrence IDs inside several containers.
/// </summary>
public static class TransientQueuePersistence
{
    public static bool IsLegacyDemonstrationItemId(string sessionId, string? itemId)
    {
        if (!string.Equals(sessionId, "tidal", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(itemId)
            || !itemId.StartsWith("tidal-", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(itemId.AsSpan("tidal-".Length), out var number)
            && number is >= 1 and <= 17;
    }

    public static QueuePersistenceSnapshot Capture(
        string sessionId,
        IEnumerable<MediaItem> source,
        IEnumerable<string>? storedOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(source);
        var items = source.ToArray();
        var storageKeyByRuntimeId = items.ToDictionary(
            item => item.Id,
            item => StorageItemId(sessionId, item),
            StringComparer.Ordinal);
        var representatives = items
            .GroupBy(item => StorageItemId(sessionId, item), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(item => string.Equals(item.Id, group.Key, StringComparison.Ordinal))
                    ?? group.First(),
                StringComparer.Ordinal);
        var normalized = new List<string>();
        foreach (var storedId in storedOrder ?? [])
        {
            var storageKey = representatives.ContainsKey(storedId)
                ? storedId
                : storageKeyByRuntimeId.GetValueOrDefault(storedId);
            if (storageKey is not null
                && representatives.ContainsKey(storageKey)
                && !normalized.Contains(storageKey, StringComparer.Ordinal))
            {
                normalized.Add(storageKey);
            }
        }
        foreach (var storageKey in representatives.Keys)
        {
            if (!normalized.Contains(storageKey, StringComparer.Ordinal)) normalized.Add(storageKey);
        }

        return new QueuePersistenceSnapshot(
            normalized,
            normalized.Where(key => representatives[key].IsInQueue).ToArray(),
            normalized.Where(key => representatives[key].IsPlayNext).ToArray(),
            normalized.Select(key => representatives[key].Id).ToArray());
    }

    public static void Restore(
        string sessionId,
        IReadOnlyList<MediaItem> items,
        IReadOnlyList<string>? storedOrder,
        IReadOnlyList<string>? regularItemIds,
        IReadOnlyList<string>? playNextItemIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(items);
        if (storedOrder is not { Count: > 0 }) return;

        var regularIds = (regularItemIds ?? []).ToHashSet(StringComparer.Ordinal);
        var nextIds = (playNextItemIds ?? []).ToHashSet(StringComparer.Ordinal);
        var legacyRegularQueue = regularIds.Count == 0 && nextIds.Count == 0;
        var byStorageKey = items
            .GroupBy(item => StorageItemId(sessionId, item), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var storageKeyByRuntimeId = items.ToDictionary(
            item => item.Id,
            item => StorageItemId(sessionId, item),
            StringComparer.Ordinal);
        foreach (var candidate in items)
        {
            candidate.IsInQueue = false;
            candidate.IsPlayNext = false;
        }
        foreach (var storedId in storedOrder.Distinct(StringComparer.Ordinal))
        {
            var storageKey = byStorageKey.ContainsKey(storedId)
                ? storedId
                : storageKeyByRuntimeId.GetValueOrDefault(storedId);
            if (storageKey is null || !byStorageKey.TryGetValue(storageKey, out var candidates)) continue;
            var representative = candidates.FirstOrDefault(item =>
                    string.Equals(item.Id, storageKey, StringComparison.Ordinal))
                ?? candidates[0];
            representative.IsInQueue = legacyRegularQueue
                || regularIds.Contains(storageKey)
                || regularIds.Contains(storedId);
            representative.IsPlayNext = nextIds.Contains(storageKey) || nextIds.Contains(storedId);
        }
    }

    public static string StorageItemId(string sessionId, MediaItem item)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(item);
        return string.Equals(sessionId, "tidal", StringComparison.Ordinal)
               && item.ExternalId is { Length: > 0 } externalId
            ? $"tidal:{externalId}"
            : item.Id;
    }
}
