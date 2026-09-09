using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Tidal;

/// <summary>
/// Maps native TIDAL collection types onto two deliberately distinct AMC
/// views: individual playable items are Favorites and containers are Library.
/// </summary>
public static class TidalCollectionSemantics
{
    public static bool UsesFavorites(MediaItemKind kind) =>
        kind is MediaItemKind.Track or MediaItemKind.Video;

    public static bool UsesLibrary(MediaItemKind kind) =>
        kind is MediaItemKind.Album or MediaItemKind.Artist or MediaItemKind.Playlist;

    public static bool IsCollectionKind(MediaItemKind kind) =>
        UsesFavorites(kind) || UsesLibrary(kind);

    public static bool IsMember(MediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return UsesFavorites(item.Kind) ? item.IsFavorite
            : UsesLibrary(item.Kind) && item.IsInLibrary;
    }

    public static void ApplyMembership(MediaItem item, bool isMember)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.IsFavorite = UsesFavorites(item.Kind) && isMember;
        item.IsInLibrary = UsesLibrary(item.Kind) && isMember;
    }
}
