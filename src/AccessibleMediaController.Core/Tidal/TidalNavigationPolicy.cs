using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Tidal;

/// <summary>
/// Keeps the user-facing TIDAL hierarchy and list navigation consistent.
/// Internal relationship identifiers never become labels.
/// </summary>
public static class TidalNavigationPolicy
{
    public static bool CanOpenRelatedAlbum(MediaItem item) =>
        item.Kind is MediaItemKind.Track or MediaItemKind.Video
        && !string.IsNullOrWhiteSpace(item.RelatedAlbumExternalId);

    public static bool CanOpenRelatedArtist(MediaItem item) =>
        item.Kind is MediaItemKind.Track or MediaItemKind.Video or MediaItemKind.Album
        && !string.IsNullOrWhiteSpace(item.RelatedArtistExternalId);

    public static bool CanShowArtistAlbums(MediaItem item) =>
        item.Kind == MediaItemKind.Artist
        && !string.IsNullOrWhiteSpace(item.ExternalId);

    public static IReadOnlyList<MediaItemField> OrderFieldsForContainer(
        MediaItemKind containerKind,
        MediaItemKind itemKind,
        IEnumerable<MediaItemField> configuredFields)
    {
        var fields = configuredFields.ToArray();
        var titleIdentifiesRow = containerKind == MediaItemKind.Album
                && itemKind is MediaItemKind.Track or MediaItemKind.Video
            || containerKind == MediaItemKind.Artist
                && itemKind == MediaItemKind.Album;
        return titleIdentifiesRow
            ? new[] { MediaItemField.Title }
                .Concat(fields.Where(field => field != MediaItemField.Title))
                .ToArray()
            : fields;
    }
}
