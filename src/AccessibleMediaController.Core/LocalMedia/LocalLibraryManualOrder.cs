using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.LocalMedia;

public enum ManualOrderMoveResult
{
    Moved,
    Boundary,
    NonContiguousSelection,
    InvalidSelection
}

public readonly record struct ManualOrderPosition(string ItemId, int Index);

public static class LocalLibraryManualOrder
{
    public static IReadOnlyList<ManualOrderPosition> CapturePositions(
        IReadOnlyList<string> storedOrder,
        IEnumerable<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(storedOrder);
        ArgumentNullException.ThrowIfNull(itemIds);
        var selected = itemIds.ToHashSet(StringComparer.Ordinal);
        return storedOrder
            .Select((itemId, index) => new ManualOrderPosition(itemId, index))
            .Where(entry => selected.Contains(entry.ItemId))
            .ToArray();
    }

    public static void RestorePositions(
        IList<string> storedOrder,
        IEnumerable<ManualOrderPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(storedOrder);
        ArgumentNullException.ThrowIfNull(positions);
        var entries = positions
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ItemId))
            .GroupBy(entry => entry.ItemId, StringComparer.Ordinal)
            .Select(group => group.OrderBy(entry => entry.Index).First())
            .OrderBy(entry => entry.Index)
            .ToArray();
        if (entries.Length == 0) return;

        var restoredIds = entries.Select(entry => entry.ItemId).ToHashSet(StringComparer.Ordinal);
        for (var index = storedOrder.Count - 1; index >= 0; index--)
        {
            if (restoredIds.Contains(storedOrder[index])) storedOrder.RemoveAt(index);
        }
        foreach (var entry in entries)
        {
            storedOrder.Insert(Math.Clamp(entry.Index, 0, storedOrder.Count), entry.ItemId);
        }
    }

    public static List<string> Normalize(
        IEnumerable<string>? storedOrder,
        IEnumerable<MediaItem> catalog,
        bool initializeAlphabetically = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var items = catalog.ToArray();
        var knownIds = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var result = (storedOrder ?? [])
            .Where(knownIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var resultIds = result.ToHashSet(StringComparer.Ordinal);
        IEnumerable<MediaItem> missing = items.Where(item => !resultIds.Contains(item.Id));
        if (result.Count == 0 && initializeAlphabetically)
        {
            missing = missing
                .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }
        result.AddRange(missing.Select(item => item.Id));
        return result;
    }

    public static IReadOnlyList<MediaItem> Order(
        IEnumerable<MediaItem> items,
        IReadOnlyList<string> storedOrder)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(storedOrder);
        var indices = storedOrder
            .Select((id, index) => (id, index))
            .GroupBy(pair => pair.id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);
        return items
            .Select((item, originalIndex) => (item, originalIndex))
            .OrderBy(pair => indices.GetValueOrDefault(pair.item.Id, int.MaxValue))
            .ThenBy(pair => pair.originalIndex)
            .Select(pair => pair.item)
            .ToArray();
    }

    public static ManualOrderMoveResult MoveVisibleBlock(
        IList<string> storedOrder,
        IReadOnlyList<string> visibleItemIds,
        IReadOnlyCollection<string> selectedItemIds,
        int direction)
    {
        ArgumentNullException.ThrowIfNull(storedOrder);
        ArgumentNullException.ThrowIfNull(visibleItemIds);
        ArgumentNullException.ThrowIfNull(selectedItemIds);
        if (direction == 0 || visibleItemIds.Count == 0 || selectedItemIds.Count == 0)
        {
            return ManualOrderMoveResult.InvalidSelection;
        }

        var selected = selectedItemIds.ToHashSet(StringComparer.Ordinal);
        var selectedIndices = visibleItemIds
            .Select((id, index) => (id, index))
            .Where(pair => selected.Contains(pair.id))
            .Select(pair => pair.index)
            .ToArray();
        if (selectedIndices.Length != selected.Count || selectedIndices.Length == 0)
        {
            return ManualOrderMoveResult.InvalidSelection;
        }

        var first = selectedIndices[0];
        var last = selectedIndices[^1];
        if (last - first + 1 != selectedIndices.Length)
        {
            return ManualOrderMoveResult.NonContiguousSelection;
        }
        if ((direction < 0 && first == 0)
            || (direction > 0 && last == visibleItemIds.Count - 1))
        {
            return ManualOrderMoveResult.Boundary;
        }

        var reordered = visibleItemIds.ToList();
        var block = reordered.GetRange(first, selectedIndices.Length);
        reordered.RemoveRange(first, selectedIndices.Length);
        var insertionIndex = direction < 0 ? first - 1 : first + 1;
        reordered.InsertRange(insertionIndex, block);

        var visibleSet = visibleItemIds.ToHashSet(StringComparer.Ordinal);
        var slots = storedOrder
            .Select((id, index) => (id, index))
            .Where(pair => visibleSet.Contains(pair.id))
            .Select(pair => pair.index)
            .ToArray();
        if (slots.Length != reordered.Count)
        {
            return ManualOrderMoveResult.InvalidSelection;
        }
        for (var index = 0; index < slots.Length; index++)
        {
            storedOrder[slots[index]] = reordered[index];
        }
        return ManualOrderMoveResult.Moved;
    }
}
