using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Waski kontrakt katalogu TIDAL. Token nigdy nie przechodzi przez JSON-lines;
/// frontend wysyla tylko tozsamosc widocznego kontenera, a host zwraca dane
/// potrzebne do kolejnej listy. Identyfikatory pozostaja polami modelu i nie sa
/// gotowymi etykietami dostepnosciowymi.
/// </summary>
public static class LiteTidalCatalogContract
{
    public const string ContainerItemsOperation = "tidal.containerItems";

    private static readonly IReadOnlyDictionary<string, MediaItemKind> Kinds =
        new Dictionary<string, MediaItemKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["album"] = MediaItemKind.Album,
            ["artist"] = MediaItemKind.Artist,
            ["playlist"] = MediaItemKind.Playlist
        };

    private static readonly IReadOnlyDictionary<string, ArtistBrowseSection> ArtistSections =
        new Dictionary<string, ArtistBrowseSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["albums"] = ArtistBrowseSection.Albums,
            ["tracks"] = ArtistBrowseSection.Tracks,
            ["similarArtists"] = ArtistBrowseSection.SimilarArtists
        };

    public static LiteTidalContainerRequest ReadRequest(JsonElement args)
    {
        var itemId = LiteArgs.RequireText(args, "itemId").Trim();
        var externalId = LiteArgs.RequireText(args, "externalId").Trim();
        var title = LiteArgs.RequireText(args, "title").Trim();
        var kindName = LiteArgs.RequireText(args, "kind").Trim();
        if (!Kinds.TryGetValue(kindName, out var kind))
            throw new LiteRequestException("Ten rodzaj elementu TIDAL nie jest kontenerem.");
        if (!ExternalIdMatchesKind(externalId, kind))
            throw new LiteRequestException("Element TIDAL ma nieprawidłową tożsamość katalogową.");

        ArtistBrowseSection? section = null;
        if (LiteArgs.ReadText(args, "artistSection") is { } sectionName)
        {
            if (kind != MediaItemKind.Artist
                || !ArtistSections.TryGetValue(sectionName.Trim(), out var parsed))
            {
                throw new LiteRequestException(
                    "Wybrana kategoria nie należy do wykonawcy TIDAL.");
            }
            section = parsed;
        }

        return new LiteTidalContainerRequest(
            new MediaItem
            {
                Id = itemId,
                ExternalId = externalId,
                Title = title,
                Artist = LiteArgs.ReadText(args, "artist")?.Trim() ?? string.Empty,
                Kind = kind,
                PublicUri = LiteArgs.ReadText(args, "publicUri")?.Trim()
            },
            section);
    }

    public static LiteTidalContainerResult CreateResult(
        MediaItem container,
        ArtistBrowseSection? section,
        IReadOnlyList<MediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(items);
        var label = section is not null
            ? section.Value.Label()
            : container.Kind switch
            {
                MediaItemKind.Album => "Album",
                MediaItemKind.Artist => "Wykonawca",
                MediaItemKind.Playlist => "Playlista",
                _ => "TIDAL"
            };
        return new LiteTidalContainerResult(
            $"{label}, {container.Title}",
            items
                .Where(item => item.IsAvailable
                    && item.Kind is MediaItemKind.Track or MediaItemKind.Video
                        or MediaItemKind.Album or MediaItemKind.Artist or MediaItemKind.Playlist
                    && !string.IsNullOrWhiteSpace(item.Id)
                    && !string.IsNullOrWhiteSpace(item.Title)
                    && !string.IsNullOrWhiteSpace(item.ExternalId))
                .Select(LiteTidalCatalogItem.FromMediaItem)
                .DistinctBy(item => item.Id, StringComparer.Ordinal)
                .ToArray());
    }

    private static bool ExternalIdMatchesKind(string externalId, MediaItemKind kind)
    {
        var prefix = kind switch
        {
            MediaItemKind.Album => "albums:",
            MediaItemKind.Artist => "artists:",
            MediaItemKind.Playlist => "playlists:",
            _ => string.Empty
        };
        return prefix.Length > 0
            && externalId.StartsWith(prefix, StringComparison.Ordinal)
            && externalId.Length > prefix.Length;
    }
}

public sealed record LiteTidalContainerRequest(
    MediaItem Container,
    ArtistBrowseSection? ArtistSection);

public sealed record LiteTidalContainerResult(
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("items")] IReadOnlyList<LiteTidalCatalogItem> Items);

public sealed record LiteTidalCatalogItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("externalId")] string ExternalId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("durationTicks")] long DurationTicks,
    [property: JsonPropertyName("publicUri")] string? PublicUri,
    [property: JsonPropertyName("relatedAlbumExternalId")] string? RelatedAlbumExternalId,
    [property: JsonPropertyName("relatedAlbumTitle")] string? RelatedAlbumTitle,
    [property: JsonPropertyName("relatedArtistExternalId")] string? RelatedArtistExternalId,
    [property: JsonPropertyName("relatedArtistName")] string? RelatedArtistName,
    [property: JsonPropertyName("isFavorite")] bool IsFavorite,
    [property: JsonPropertyName("isInLibrary")] bool IsInLibrary)
{
    public static LiteTidalCatalogItem FromMediaItem(MediaItem item) => new(
        item.Id,
        item.ExternalId!,
        item.Title,
        item.Artist,
        item.Kind.ToString().ToLowerInvariant(),
        Math.Max(0, item.Duration.Ticks),
        string.IsNullOrWhiteSpace(item.PublicUri) ? null : item.PublicUri,
        item.RelatedAlbumExternalId,
        item.RelatedAlbumTitle,
        item.RelatedArtistExternalId,
        item.RelatedArtistName,
        item.IsFavorite,
        item.IsInLibrary);
}
