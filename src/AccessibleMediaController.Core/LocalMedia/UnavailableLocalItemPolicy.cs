using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.LocalMedia;

public static class UnavailableLocalItemPolicy
{
    public static IReadOnlyList<LocalMediaItemSettings> GetForFolder(
        PersistedState state,
        string folderSourceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var source = state.LocalMedia.FolderSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, folderSourceId, StringComparison.Ordinal));
        if (source is null) return [];

        return state.LocalMedia.Items
            .Where(item => item.IsInLibrary
                && !item.IsAvailable
                && LocalFolderSourcePolicy.IsSameOrDescendant(item.Path, source.Path))
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<LocalMediaItemSettings> Forget(
        PersistedState state,
        string folderSourceId,
        IEnumerable<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(itemIds);
        var requestedIds = itemIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (requestedIds.Count == 0) return [];

        var eligible = GetForFolder(state, folderSourceId)
            .Where(item => requestedIds.Contains(item.Id))
            .ToArray();
        if (eligible.Length == 0) return [];

        var removedIds = eligible.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        state.LocalMedia.Items.RemoveAll(item => removedIds.Contains(item.Id));
        state.LocalMedia.CustomOrderItemIds.RemoveAll(removedIds.Contains);
        if (removedIds.Contains(state.LocalMedia.CurrentItemId ?? string.Empty))
        {
            state.LocalMedia.CurrentItemId = null;
        }

        state.Bookmarks.Entries.RemoveAll(entry =>
            string.Equals(entry.SessionId, "local", StringComparison.OrdinalIgnoreCase)
            && removedIds.Contains(entry.ItemId));
        if (state.PlaybackHistory.ItemIdsBySession.TryGetValue("local", out var history))
        {
            history.RemoveAll(removedIds.Contains);
            if (history.Count == 0) state.PlaybackHistory.ItemIdsBySession.Remove("local");
        }
        RemoveFromOrder(state.CollectionOrders.FavoriteItemIdsBySession, removedIds);
        RemoveFromOrder(state.CollectionOrders.QueueItemIdsBySession, removedIds);
        foreach (var playlist in state.Playlists.Entries.Where(entry =>
                     string.Equals(entry.SessionId, "local", StringComparison.OrdinalIgnoreCase)))
        {
            playlist.ItemIds.RemoveAll(removedIds.Contains);
        }

        if (state.SessionNavigation.Sessions.TryGetValue("local", out var navigation))
        {
            foreach (var view in navigation.SelectedItemIds.Keys.ToArray())
            {
                if (removedIds.Contains(navigation.SelectedItemIds[view] ?? string.Empty))
                {
                    navigation.SelectedItemIds[view] = null;
                }
            }
            navigation.PlaybackContextItemIds.RemoveAll(removedIds.Contains);
        }

        return eligible;
    }

    private static void RemoveFromOrder(
        IDictionary<string, List<string>> orders,
        IReadOnlySet<string> removedIds)
    {
        if (!orders.TryGetValue("local", out var order)) return;
        order.RemoveAll(removedIds.Contains);
        if (order.Count == 0) orders.Remove("local");
    }
}
