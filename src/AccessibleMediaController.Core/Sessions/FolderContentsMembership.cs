using System.Collections.Generic;
using System.Linq;

namespace AccessibleMediaController.Core.Sessions;

public static class FolderContentsMembership
{
    public static bool ToggleFavorites(IReadOnlyList<MediaItem> items)
    {
        var add = !items.Any(item => item.IsFavorite);
        foreach (var item in items) item.IsFavorite = add;
        return add;
    }

    public static bool ToggleQueue(IReadOnlyList<MediaItem> items)
    {
        var add = !items.Any(item => item.IsInQueue || item.IsPlayNext);
        foreach (var item in items)
        {
            item.IsInQueue = add;
            if (!add) item.IsPlayNext = false;
        }
        return add;
    }

    public static bool TogglePlayNext(IReadOnlyList<MediaItem> items)
    {
        var add = !items.Any(item => item.IsPlayNext);
        foreach (var item in items) item.IsPlayNext = add;
        return add;
    }
}
