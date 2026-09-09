using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Tidal;

/// <summary>
/// Rebuilds the durable oldest-to-newest order from authoritative collection
/// metadata. A null result means that the snapshot lacks metadata and must not
/// replace an already saved order.
/// </summary>
public static class TidalCollectionAddedOrder
{
    public static IReadOnlyList<string>? Rebuild(
        IReadOnlyList<MediaItem> items,
        IEnumerable<string>? priorOrder)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Any(item => item.CollectionAddedUtcTicks is null)) return null;

        var priorPositions = (priorOrder ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select((id, index) => (id, index))
            .ToDictionary(entry => entry.id, entry => entry.index, StringComparer.Ordinal);
        return items
            .OrderBy(item => item.CollectionAddedUtcTicks)
            .ThenBy(item => priorPositions.GetValueOrDefault(item.Id, int.MaxValue))
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
