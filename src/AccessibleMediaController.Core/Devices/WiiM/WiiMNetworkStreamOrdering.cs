using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Devices.WiiM;

public sealed record WiiMNetworkStreamOrderResult(
    IReadOnlyList<WiiMNetworkStreamSettings> Streams,
    IReadOnlyList<string> NormalizedItemIds);

public static class WiiMNetworkStreamOrdering
{
    public static IReadOnlyList<string> RememberImportedBatch(
        IEnumerable<WiiMNetworkStreamSettings> allStreams,
        IEnumerable<string>? storedItemIds,
        IEnumerable<string> importedItemIds)
    {
        ArgumentNullException.ThrowIfNull(allStreams);
        ArgumentNullException.ThrowIfNull(importedItemIds);
        var availableIds = allStreams
            .Select(stream => stream.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var available = availableIds.ToHashSet(StringComparer.Ordinal);
        var imported = importedItemIds
            .Where(available.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var importedSet = imported.ToHashSet(StringComparer.Ordinal);
        var result = (storedItemIds ?? [])
            .Where(id => available.Contains(id) && !importedSet.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var known = result.ToHashSet(StringComparer.Ordinal);
        result.AddRange(availableIds.Where(id => !known.Contains(id) && !importedSet.Contains(id)));

        // The AddedNewest view reverses this durable oldest-to-newest order.
        // Store a newly imported batch backwards so the playlist's first entry
        // remains the first entry of that batch on screen.
        result.AddRange(imported.Reverse());
        return result;
    }

    public static WiiMNetworkStreamOrderResult OrderForDisplay(
        IEnumerable<WiiMNetworkStreamSettings> streams,
        CollectionSortMode mode,
        IEnumerable<string>? storedItemIds)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var items = streams
            .Where(stream => !string.IsNullOrWhiteSpace(stream.Id))
            .DistinctBy(stream => stream.Id, StringComparer.Ordinal)
            .ToArray();
        var knownIds = items
            .Select(stream => stream.Id)
            .ToHashSet(StringComparer.Ordinal);
        var normalizedItemIds = (storedItemIds ?? [])
            .Where(knownIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var normalizedIds = normalizedItemIds.ToHashSet(StringComparer.Ordinal);
        normalizedItemIds.AddRange(items
            .Where(stream => !normalizedIds.Contains(stream.Id))
            .Select(stream => stream.Id));

        IReadOnlyList<WiiMNetworkStreamSettings> ordered = mode switch
        {
            CollectionSortMode.Alphabetical => items
                .OrderBy(stream => stream.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(stream => stream.StreamUrl, StringComparer.OrdinalIgnoreCase)
                .ThenBy(stream => stream.Id, StringComparer.Ordinal)
                .ToArray(),
            CollectionSortMode.Custom => OrderByStoredIds(items, normalizedItemIds),
            _ => OrderByStoredIds(items, normalizedItemIds).Reverse().ToArray()
        };
        return new WiiMNetworkStreamOrderResult(ordered, normalizedItemIds);
    }

    private static IReadOnlyList<WiiMNetworkStreamSettings> OrderByStoredIds(
        IEnumerable<WiiMNetworkStreamSettings> streams,
        IReadOnlyList<string> storedItemIds)
    {
        var indices = storedItemIds
            .Select((id, index) => (id, index))
            .ToDictionary(entry => entry.id, entry => entry.index, StringComparer.Ordinal);
        return streams
            .Select((stream, originalIndex) => (stream, originalIndex))
            .OrderBy(entry => indices.GetValueOrDefault(entry.stream.Id, int.MaxValue))
            .ThenBy(entry => entry.originalIndex)
            .Select(entry => entry.stream)
            .ToArray();
    }
}
