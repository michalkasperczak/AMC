using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Tidal;

/// <summary>
/// A partial collection response must not erase catalogue facts already known
/// for the same TIDAL resource. Membership still comes from the fresh response.
/// </summary>
public static class TidalCollectionMetadata
{
    public static IReadOnlyList<MediaItem> PreserveKnownDetails(
        IReadOnlyList<MediaItem> fresh,
        IReadOnlyList<MediaItem> previous)
    {
        var known = previous
            .Where(item => !string.IsNullOrWhiteSpace(item.ExternalId))
            .GroupBy(item => item.ExternalId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var item in fresh)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId)
                || !known.TryGetValue(item.ExternalId, out var old)
                || item.Kind != old.Kind)
                continue;
            if (item.Duration <= TimeSpan.Zero && old.Duration > TimeSpan.Zero)
                item.Duration = old.Duration;
            if (string.IsNullOrWhiteSpace(item.Artist))
                item.Artist = old.Artist;
            if (string.IsNullOrWhiteSpace(item.RelatedAlbumExternalId))
                item.RelatedAlbumExternalId = old.RelatedAlbumExternalId;
            if (string.Equals(item.RelatedAlbumExternalId, old.RelatedAlbumExternalId,
                    StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(item.RelatedAlbumTitle))
                item.RelatedAlbumTitle = old.RelatedAlbumTitle;
            if (string.IsNullOrWhiteSpace(item.RelatedArtistExternalId))
                item.RelatedArtistExternalId = old.RelatedArtistExternalId;
            if (string.Equals(item.RelatedArtistExternalId, old.RelatedArtistExternalId,
                    StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(item.RelatedArtistName))
                item.RelatedArtistName = old.RelatedArtistName;
            item.CollectionAddedUtcTicks ??= old.CollectionAddedUtcTicks;
        }
        return fresh;
    }
}
