using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;

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
    public const string CollectionViewOperation = "tidal.collectionView";
    public const string MembershipOperation = "tidal.collectionMembership";

    private static readonly IReadOnlyDictionary<string, MediaItemKind> Kinds =
        new Dictionary<string, MediaItemKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["album"] = MediaItemKind.Album,
            ["artist"] = MediaItemKind.Artist,
            ["playlist"] = MediaItemKind.Playlist
        };

    private static readonly IReadOnlyDictionary<string, MediaItemKind> CollectionKinds =
        new Dictionary<string, MediaItemKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["track"] = MediaItemKind.Track,
            ["video"] = MediaItemKind.Video,
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

    public static string ReadCollectionView(JsonElement args)
    {
        var view = LiteArgs.RequireText(args, "view").Trim().ToLowerInvariant();
        return view is "library" or "favorites" or "playlists"
            ? view
            : throw new LiteRequestException("Nieznany widok kolekcji TIDAL.");
    }

    public static LiteTidalCollectionViewResult CreateCollectionViewResult(
        string view,
        IReadOnlyList<MediaItem> items,
        string warning = "")
    {
        var filtered = view switch
        {
            "favorites" => items.Where(item => item.IsAvailable
                && TidalCollectionSemantics.UsesFavorites(item.Kind)
                && item.IsFavorite),
            "playlists" => items.Where(item => item.IsAvailable
                && item.Kind == MediaItemKind.Playlist
                && item.IsInLibrary),
            _ => items.Where(item => item.IsAvailable
                && TidalCollectionSemantics.UsesLibrary(item.Kind)
                && item.IsInLibrary)
        };
        var heading = view switch
        {
            "favorites" => "Ulubione TIDAL",
            "playlists" => "Playlisty TIDAL",
            _ => "Biblioteka TIDAL"
        };
        return new LiteTidalCollectionViewResult(
            heading,
            filtered.Select(LiteTidalCatalogItem.FromMediaItem)
                .DistinctBy(item => item.Id, StringComparer.Ordinal)
                .ToArray(),
            warning);
    }

    public static LiteTidalMembershipRequest ReadMembershipRequest(JsonElement args)
    {
        var mode = LiteArgs.RequireText(args, "mode").Trim().ToLowerInvariant();
        if (mode is not ("favorite" or "library"))
            throw new LiteRequestException("Nieznany rodzaj kolekcji TIDAL.");
        if (!args.TryGetProperty("items", out var rawItems)
            || rawItems.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Nie przekazano elementów TIDAL.");
        }

        var items = new List<MediaItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in rawItems.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object) continue;
            var id = LiteArgs.RequireText(raw, "itemId").Trim();
            var externalId = LiteArgs.RequireText(raw, "externalId").Trim();
            var title = LiteArgs.RequireText(raw, "title").Trim();
            var kindName = LiteArgs.RequireText(raw, "kind").Trim();
            if (!CollectionKinds.TryGetValue(kindName, out var kind)
                || !ExternalIdMatchesKind(externalId, kind))
            {
                throw new LiteRequestException(
                    "Element TIDAL ma nieprawidłową tożsamość katalogową.");
            }
            var expectedMode = TidalCollectionSemantics.UsesFavorites(kind)
                ? "favorite"
                : "library";
            if (!string.Equals(mode, expectedMode, StringComparison.Ordinal))
            {
                throw new LiteRequestException(mode == "favorite"
                    ? "Do Ulubionych TIDAL można dodać utwory i materiały wideo."
                    : "Do Biblioteki TIDAL można dodać albumy, wykonawców i playlisty.");
            }
            if (!seen.Add(id)) continue;
            items.Add(new MediaItem
            {
                Id = id,
                ExternalId = externalId,
                Title = title,
                Artist = LiteArgs.ReadText(raw, "artist")?.Trim() ?? string.Empty,
                Kind = kind,
                PublicUri = LiteArgs.ReadText(raw, "publicUri")?.Trim()
            });
        }
        if (items.Count == 0)
            throw new LiteRequestException("Brak elementów TIDAL możliwych do zapisania.");

        bool? requestedAddition = null;
        if (args.TryGetProperty("add", out var add))
        {
            if (add.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new LiteRequestException("Pole add musi mieć wartość logiczną.");
            requestedAddition = add.GetBoolean();
        }
        return new LiteTidalMembershipRequest(mode, items, requestedAddition);
    }

    private static bool ExternalIdMatchesKind(string externalId, MediaItemKind kind)
    {
        var prefix = kind switch
        {
            MediaItemKind.Track => "tracks:",
            MediaItemKind.Video => "videos:",
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

public sealed record LiteTidalMembershipRequest(
    string Mode,
    IReadOnlyList<MediaItem> Items,
    bool? RequestedAddition);

public sealed record LiteTidalMembershipResult(
    [property: JsonPropertyName("added")] bool Added,
    [property: JsonPropertyName("changed")] int Changed,
    [property: JsonPropertyName("itemIds")] IReadOnlyList<string> ItemIds,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteTidalContainerRequest(
    MediaItem Container,
    ArtistBrowseSection? ArtistSection);

public sealed record LiteTidalContainerResult(
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("items")] IReadOnlyList<LiteTidalCatalogItem> Items);

public sealed record LiteTidalCollectionViewResult(
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("items")] IReadOnlyList<LiteTidalCatalogItem> Items,
    [property: JsonPropertyName("warning")] string Warning);

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
