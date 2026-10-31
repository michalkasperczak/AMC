using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Spotify catalogue contract for the wx frontend. Credentials and playback
/// handles never cross JSON-lines. IDs are model fields; every item also has
/// an explicit, user-facing accessible label which never contains an ID or URI.
/// </summary>
public static class LiteSpotifyCatalogContract
{
    public const string SessionId = "spotify";
    public const string CapabilitiesOperation = "spotify.capabilities";
    public const string AccountStatusOperation = "spotify.accountStatus";
    public const string LoginOperation = "spotify.login";
    public const string ConfigureOperation = "spotify.configure";
    public const string PairingStartOperation = "spotify.pairingStart";
    public const string PairingStatusOperation = "spotify.pairingStatus";
    public const string PairingCancelOperation = "spotify.pairingCancel";
    public const string CollectionRootOperation = "spotify.collectionRoot";
    public const string CollectionViewOperation = "spotify.collectionView";
    public const string ContainerItemsOperation = "spotify.containerItems";
    public const string SearchOperation = "spotify.search";
    public const string DescriptionOperation = "spotify.description";
    public const string MembershipOperation = "spotify.collectionMembership";
    public const string PlaylistTargetsOperation = "spotify.playlistTargets";
    public const string PlaylistCreateOperation = "spotify.playlistCreate";
    public const string PlaylistAddItemsOperation = "spotify.playlistAddItems";

    private const int MaximumQueryLength = 200;
    private const int MaximumLabelPartLength = 300;
    private const int MaximumExternalIdLength = 128;
    private const int MaximumDescriptionLength = 50_000;
    public const int CollectionPageSize = 150;

    private static readonly IReadOnlyDictionary<string, MediaItemKind> ContainerKinds =
        new Dictionary<string, MediaItemKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["album"] = MediaItemKind.Album,
            ["artist"] = MediaItemKind.Artist,
            ["playlist"] = MediaItemKind.Playlist,
            ["podcast"] = MediaItemKind.Podcast
        };

    private static readonly IReadOnlyDictionary<string, string> ArtistSections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["albums"] = "albums",
            ["tracks"] = "tracks"
        };

    public static LiteSpotifyCapabilitiesResult CreateCapabilities(
        bool collectionWrite = false,
        bool login = false) => new(
        SessionId,
        ReadOnly: !collectionWrite,
        OneSession: true,
        AccountStatus: true,
        CollectionRoot: true,
        CollectionRead: true,
        ContainerBrowse: true,
        CatalogSearch: true,
        CollectionWrite: collectionWrite,
        Login: login,
        Playback: true,
        collectionWrite
            ? "Katalog i odtwarzanie Spotify mają osobne stany połączenia. "
                + "Zmiany kolekcji wykonuje oficjalne API, a dźwięk odtwarza natywna sesja Librespot."
            : "Katalog i odtwarzanie Spotify mają osobne stany połączenia. "
                + "Katalog pozostaje tylko do odczytu, a dźwięk odtwarza natywna sesja Librespot.");

    public static LiteSpotifyAccountStatusResult CreateAccountStatus(
        LiteSpotifyAccountProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        var catalog = !probe.CatalogConfigured
            ? new LiteSpotifyConnectionStatus(
                "notConfigured", false, false,
                "Katalog Spotify nie jest skonfigurowany.",
                null, "nieznany")
            : probe.CatalogConnected switch
            {
                true => new LiteSpotifyConnectionStatus(
                    "connected", true, true,
                    string.IsNullOrWhiteSpace(probe.AccountDisplayName)
                        ? "Katalog Spotify jest połączony."
                        : $"Katalog Spotify jest połączony: {CleanText(probe.AccountDisplayName)}.",
                    NullIfEmpty(CleanText(probe.AccountDisplayName)),
                    PlanLabel(probe.AccountProduct)),
                false => new LiteSpotifyConnectionStatus(
                    "disconnected", true, false,
                    "Katalog Spotify nie jest połączony.",
                    null, "nieznany"),
                null => new LiteSpotifyConnectionStatus(
                    "unavailable", true, false,
                    "Nie można teraz sprawdzić połączenia katalogu Spotify.",
                    null, "nieznany")
            };

        var playback = probe.PlaybackConnected switch
        {
            true => new LiteSpotifyConnectionStatus(
                "connected", true, true,
                "Konto odtwarzania Spotify jest zapisane i gotowe dla natywnej sesji.",
                null, "nieznany"),
            false => new LiteSpotifyConnectionStatus(
                "disconnected", true, false,
                "Konto odtwarzania Spotify nie jest połączone.",
                null, "nieznany"),
            null => new LiteSpotifyConnectionStatus(
                "unavailable", true, false,
                "Nie można teraz sprawdzić połączenia odtwarzania Spotify.",
                null, "nieznany")
        };

        return new LiteSpotifyAccountStatusResult(
            SessionId, catalog, playback,
            string.IsNullOrWhiteSpace(probe.RedirectUri)
                ? "http://127.0.0.1:43822/spotify/callback/"
                : probe.RedirectUri);
    }

    public static string ReadClientId(JsonElement args)
    {
        var value = LiteArgs.RequireText(args, "clientId").Trim();
        if (value.Length != 32 || !value.All(Uri.IsHexDigit))
            throw new LiteRequestException(
                "Identyfikator aplikacji Spotify powinien mieć 32 znaki szesnastkowe. Nie wpisuj Client Secret.");
        return value;
    }

    public static LiteSpotifyLoginResult CreateLoginResult(
        string? accountDisplayName,
        string? accountProduct)
    {
        var name = CleanText(accountDisplayName);
        var plan = PlanLabel(accountProduct);
        var account = name.Length == 0 ? string.Empty : $": {name}";
        var planText = plan == "nieznany" ? string.Empty : $" Plan: {plan}.";
        return new LiteSpotifyLoginResult(
            true,
            $"Zalogowano konto Spotify{account}.{planText} Uprawnienia konta zostały odświeżone.");
    }

    public static LiteSpotifyCollectionRootResult CreateCollectionRoot() => new(
        "Biblioteka Spotify",
        [
            new("liked", "Polubione utwory"),
            new("albums", "Albumy"),
            new("artists", "Wykonawcy"),
            new("playlists", "Playlisty"),
            new("podcasts", "Podcasty"),
            new("episodes", "Zapisane odcinki")
        ]);

    public static string ReadCollectionView(JsonElement args)
    {
        var view = LiteArgs.RequireText(args, "view").Trim().ToLowerInvariant();
        return view is "liked" or "albums" or "artists" or "playlists"
            or "podcasts" or "episodes"
            ? view
            : throw new LiteRequestException("Nieznany widok kolekcji Spotify.");
    }

    public static int ReadCollectionLoadedCount(JsonElement args) =>
        LiteArgs.ReadInt(
            args,
            "loadedCount",
            CollectionPageSize,
            CollectionPageSize,
            int.MaxValue);

    public static string ReadCollectionSortMode(JsonElement args) =>
        NormalizeCollectionSortMode(LiteArgs.ReadText(args, "sortMode"));

    public static string ReadSearchQuery(JsonElement args)
    {
        var query = LiteArgs.RequireText(args, "query").Trim();
        if (query.Length == 0)
            throw new LiteRequestException("Wpisz tekst do wyszukania w Spotify.");
        if (query.Length > MaximumQueryLength)
            throw new LiteRequestException("Tekst wyszukiwania Spotify jest zbyt długi.");
        return query;
    }

    public static LiteSpotifyContainerRequest ReadContainerRequest(JsonElement args)
    {
        var title = CleanText(LiteArgs.RequireText(args, "title"));
        var kindText = LiteArgs.RequireText(args, "kind").Trim();
        var externalId = LiteArgs.RequireText(args, "externalId").Trim();
        if (!ContainerKinds.TryGetValue(kindText, out var kind))
            throw new LiteRequestException("Ten rodzaj elementu Spotify nie jest kontenerem.");
        if (!TryNormalizeExternalId(externalId, kind, out var normalizedId))
            throw new LiteRequestException("Element Spotify ma nieprawidłową tożsamość katalogową.");

        string? artistSection = null;
        if (LiteArgs.ReadText(args, "artistSection") is { } requestedSection)
        {
            if (kind != MediaItemKind.Artist
                || !ArtistSections.TryGetValue(requestedSection.Trim(), out artistSection))
            {
                throw new LiteRequestException(
                    "Wybrana kategoria nie należy do wykonawcy Spotify.");
            }
        }

        return new LiteSpotifyContainerRequest(
            kind,
            normalizedId,
            title,
            CleanText(LiteArgs.ReadText(args, "artist") ?? string.Empty),
            artistSection,
            LiteArgs.ReadInt(
                args,
                "loadedCount",
                CollectionPageSize,
                CollectionPageSize,
                int.MaxValue));
    }

    public static LiteSpotifyDescriptionRequest ReadDescriptionRequest(JsonElement args)
    {
        var kindText = LiteArgs.RequireText(args, "kind").Trim();
        var kind = kindText.ToLowerInvariant() switch
        {
            "podcast" => MediaItemKind.Podcast,
            "episode" => MediaItemKind.Episode,
            _ => throw new LiteRequestException(
                "Pełny opis Spotify jest dostępny dla podcastu albo odcinka.")
        };
        var externalId = LiteArgs.RequireText(args, "externalId").Trim();
        if (!TryNormalizeExternalId(externalId, kind, out var normalizedId))
        {
            throw new LiteRequestException(
                "Wybrany podcast lub odcinek Spotify nie ma prawidłowych danych katalogu.");
        }
        return new LiteSpotifyDescriptionRequest(kind, normalizedId);
    }

    public static LiteSpotifyMembershipRequest ReadMembershipRequest(JsonElement args)
    {
        var mode = LiteArgs.RequireText(args, "mode").Trim().ToLowerInvariant();
        if (mode is not ("favorite" or "library"))
            throw new LiteRequestException("Nieznany rodzaj kolekcji Spotify.");
        if (!args.TryGetProperty("items", out var rawItems)
            || rawItems.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Nie przekazano elementów Spotify.");
        }

        var items = new List<MediaItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in rawItems.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object) continue;
            var itemId = LiteArgs.RequireText(raw, "itemId").Trim();
            var externalId = LiteArgs.RequireText(raw, "externalId").Trim();
            var title = CleanText(LiteArgs.RequireText(raw, "title"));
            var kindName = LiteArgs.RequireText(raw, "kind").Trim().ToLowerInvariant();
            MediaItemKind? kind = kindName switch
            {
                "track" => MediaItemKind.Track,
                "episode" => MediaItemKind.Episode,
                "album" => MediaItemKind.Album,
                "artist" => MediaItemKind.Artist,
                "podcast" => MediaItemKind.Podcast,
                _ => null
            };
            if (itemId.Length == 0 || title.Length == 0
                || kind is not { } itemKind
                || !TryNormalizeExternalId(externalId, itemKind, out var normalizedId))
            {
                throw new LiteRequestException(
                    "Element Spotify ma nieprawidłową tożsamość katalogową.");
            }
            var supported = mode == "favorite"
                ? itemKind is MediaItemKind.Track or MediaItemKind.Episode
                : itemKind is MediaItemKind.Album or MediaItemKind.Artist
                    or MediaItemKind.Podcast or MediaItemKind.Episode;
            if (!supported)
            {
                throw new LiteRequestException(mode == "favorite"
                    ? "Do Ulubionych Spotify wybierz utwór albo odcinek."
                    : "Do Biblioteki Spotify wybierz album, wykonawcę, podcast albo odcinek.");
            }
            var key = $"{itemKind}:{normalizedId}";
            if (!seen.Add(key)) continue;
            items.Add(new MediaItem
            {
                Id = itemId,
                ExternalId = normalizedId,
                Title = title,
                Artist = CleanText(LiteArgs.ReadText(raw, "artist") ?? string.Empty),
                Kind = itemKind,
                PublicUri = LiteArgs.ReadText(raw, "publicUri")?.Trim()
            });
        }
        if (items.Count == 0)
            throw new LiteRequestException("Brak elementów Spotify możliwych do zapisania.");

        bool? requestedAddition = null;
        if (args.TryGetProperty("add", out var add))
        {
            if (add.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new LiteRequestException("Pole add musi mieć wartość logiczną.");
            requestedAddition = add.GetBoolean();
        }
        return new LiteSpotifyMembershipRequest(mode, items, requestedAddition);
    }

    public static LiteSpotifyPlaylistCreateRequest ReadPlaylistCreateRequest(JsonElement args)
    {
        var name = CleanText(LiteArgs.RequireText(args, "name")).Trim();
        if (name.Length is 0 or > 100)
            throw new LiteRequestException("Nazwa playlisty Spotify musi mieć od 1 do 100 znaków.");
        return new LiteSpotifyPlaylistCreateRequest(
            name,
            LiteArgs.ReadBool(args, "isPublic", false));
    }

    public static LiteSpotifyPlaylistAddRequest ReadPlaylistAddRequest(JsonElement args)
    {
        if (!args.TryGetProperty("playlistIds", out var rawPlaylists)
            || rawPlaylists.ValueKind != JsonValueKind.Array)
            throw new LiteRequestException("Nie wybrano playlisty Spotify.");
        var playlistIds = rawPlaylists.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim() ?? string.Empty)
            .Where(value => value.Length is > 0 and <= MaximumExternalIdLength)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (playlistIds.Length == 0)
            throw new LiteRequestException("Nie wybrano playlisty Spotify.");
        if (!args.TryGetProperty("items", out var rawItems)
            || rawItems.ValueKind != JsonValueKind.Array)
            throw new LiteRequestException("Nie przekazano utworów ani odcinków Spotify.");

        var items = new List<MediaItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in rawItems.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object) continue;
            var externalId = LiteArgs.RequireText(raw, "externalId").Trim();
            var title = CleanText(LiteArgs.RequireText(raw, "title"));
            var itemId = LiteArgs.RequireText(raw, "itemId").Trim();
            var kindName = LiteArgs.RequireText(raw, "kind").Trim().ToLowerInvariant();
            var kind = kindName switch
            {
                "track" => MediaItemKind.Track,
                "episode" => MediaItemKind.Episode,
                _ => throw new LiteRequestException(
                    "Do playlisty Spotify można dodać tylko utwór albo odcinek.")
            };
            if (!TryNormalizeExternalId(externalId, kind, out var normalizedId))
                throw new LiteRequestException("Element Spotify ma nieprawidłową tożsamość katalogową.");
            if (!seen.Add($"{kind}:{normalizedId}")) continue;
            items.Add(new MediaItem
            {
                Id = itemId,
                ExternalId = normalizedId,
                Title = title,
                Artist = CleanText(LiteArgs.ReadText(raw, "artist") ?? string.Empty),
                Kind = kind
            });
        }
        if (items.Count == 0)
            throw new LiteRequestException("Brak utworów albo odcinków Spotify do dodania.");
        return new LiteSpotifyPlaylistAddRequest(playlistIds, items);
    }

    public static LiteSpotifyPlaylistTarget ToPlaylistTarget(SpotifyPlaylistTarget target) =>
        new(
            target.Id,
            CleanText(target.Name),
            target.IsPublic,
            target.Collaborative,
            CleanText(target.AccessibleLabel),
            target.SnapshotId);

    public static LiteSpotifyDescriptionResult CreateDescriptionResult(
        string? description) => new(CleanDescription(description));

    public static LiteSpotifyItemsResult CreateSearchResult(IReadOnlyList<MediaItem> items) =>
        CreateItemsResult(
            "Wyniki wyszukiwania Spotify",
            items,
            warning: "Spotify pokazuje najwyżej 10 wyników każdego rodzaju dla jednego wyszukiwania.");

    public static LiteSpotifyItemsResult CreateCollectionViewResult(
        string view,
        IReadOnlyList<MediaItem> items,
        string warning = "",
        int? loadedCount = null,
        string sortMode = "AddedNewest")
    {
        ArgumentNullException.ThrowIfNull(items);
        var filtered = view switch
        {
            "liked" => items.Where(item => item.Kind == MediaItemKind.Track && item.IsFavorite),
            "albums" => items.Where(item => item.Kind == MediaItemKind.Album && item.IsInLibrary),
            "artists" => items.Where(item => item.Kind == MediaItemKind.Artist && item.IsInLibrary),
            "playlists" => items.Where(item => item.Kind == MediaItemKind.Playlist && item.IsInLibrary),
            "podcasts" => items.Where(item => item.Kind == MediaItemKind.Podcast && item.IsInLibrary),
            "episodes" => items.Where(item => item.Kind == MediaItemKind.Episode && item.IsFavorite),
            _ => throw new LiteRequestException("Nieznany widok kolekcji Spotify.")
        };
        var heading = view switch
        {
            "liked" => "Polubione utwory Spotify",
            "albums" => "Albumy Spotify",
            "artists" => "Wykonawcy Spotify",
            "playlists" => "Playlisty Spotify",
            "podcasts" => "Podcasty Spotify",
            "episodes" => "Zapisane odcinki Spotify",
            _ => "Biblioteka Spotify"
        };
        return CreateItemsResult(
            heading,
            OrderCollection(filtered, sortMode),
            warning,
            maximumItems: loadedCount);
    }

    public static LiteSpotifyItemsResult CreateContainerResult(
        LiteSpotifyContainerRequest request,
        IReadOnlyList<MediaItem> items,
        bool available,
        bool hasMore = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(items);
        var heading = request.Kind switch
        {
            MediaItemKind.Album => $"Album, {request.Title}",
            MediaItemKind.Playlist => $"Playlista, {request.Title}",
            MediaItemKind.Podcast => $"Podcast, {request.Title}",
            MediaItemKind.Artist when request.ArtistSection == "tracks" =>
                $"Utwory wykonawcy, {request.Title}",
            MediaItemKind.Artist => $"Albumy wykonawcy, {request.Title}",
            _ => "Spotify"
        };
        var warning = available
            ? string.Empty
            : "Spotify nie udostępnił zawartości tego elementu.";
        return CreateItemsResult(
            heading,
            items,
            warning,
            available,
            hasMore,
            preserveOccurrences: true);
    }

    public static string FailureMessage(LiteSpotifyFailureReason reason, string operation) => reason switch
    {
        LiteSpotifyFailureReason.NotConfigured =>
            "Katalog Spotify nie jest skonfigurowany. Otwórz ustawienia konta Spotify.",
        LiteSpotifyFailureReason.NotConnected =>
            "Katalog Spotify nie jest połączony. Otwórz ustawienia konta Spotify.",
        LiteSpotifyFailureReason.AccessDenied =>
            "Spotify odmówił dostępu do katalogu. Sprawdź połączenie konta Spotify.",
        LiteSpotifyFailureReason.NotFound =>
            "Spotify nie znalazł wybranych danych.",
        LiteSpotifyFailureReason.RateLimited =>
            "Spotify chwilowo ograniczył liczbę zapytań. Spróbuj ponownie później.",
        LiteSpotifyFailureReason.Network =>
            "Brak połączenia ze Spotify.",
        LiteSpotifyFailureReason.Timeout =>
            "Spotify nie odpowiedział w wyznaczonym czasie.",
        LiteSpotifyFailureReason.Cancelled =>
            "Operację Spotify anulowano.",
        _ => operation switch
        {
            LoginOperation => "Nie udało się zalogować konta Spotify.",
            SearchOperation => "Nie udało się wyszukać w Spotify.",
            ContainerItemsOperation => "Nie udało się otworzyć zawartości Spotify.",
            CollectionViewOperation => "Nie udało się odświeżyć kolekcji Spotify.",
            MembershipOperation => "Nie udało się zmienić kolekcji Spotify.",
            PlaylistTargetsOperation => "Nie udało się odczytać playlist Spotify.",
            PlaylistCreateOperation => "Nie udało się utworzyć playlisty Spotify.",
            PlaylistAddItemsOperation => "Nie udało się dodać pozycji do playlist Spotify.",
            _ => "Nie udało się odczytać danych Spotify."
        }
    };

    private static LiteSpotifyItemsResult CreateItemsResult(
        string heading,
        IEnumerable<MediaItem> items,
        string warning = "",
        bool available = true,
        bool hasMore = false,
        bool preserveOccurrences = false,
        int? maximumItems = null)
    {
        var supported = items.Where(IsSupportedItem);
        if (!preserveOccurrences)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            supported = supported.Where(item =>
            {
                TryNormalizeExternalId(item.ExternalId, item.Kind, out var externalId);
                return seen.Add($"{item.Kind}:{externalId}");
            });
        }

        var requested = maximumItems is { } count
            ? Math.Max(0, count)
            : int.MaxValue;
        var readAhead = requested == int.MaxValue ? int.MaxValue : requested + 1;
        var page = supported.Take(readAhead).ToArray();
        var pageHasMore = requested != int.MaxValue && page.Length > requested;
        if (pageHasMore) page = page.Take(requested).ToArray();

        var visible = preserveOccurrences
            ? page.Select((item, index) =>
                    LiteSpotifyCatalogItem.FromMediaItem(item, index))
                .ToArray()
            : page.Select(item => LiteSpotifyCatalogItem.FromMediaItem(item))
                .ToArray();
        return new LiteSpotifyItemsResult(
            CleanText(heading), visible, CleanText(warning), available,
            hasMore || pageHasMore);
    }

    private static IEnumerable<MediaItem> OrderCollection(
        IEnumerable<MediaItem> items,
        string? sortMode)
    {
        var indexed = items.Select((item, index) => (Item: item, Index: index));
        return NormalizeCollectionSortMode(sortMode) switch
        {
            "Alphabetical" => indexed
                .OrderBy(entry => CleanText(entry.Item.Title),
                    StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Item),
            // Alt+3 przywraca kolejność źródłowego cache katalogu. Nie
            // nazywamy jej w interfejsie „własną”, bo ta warstwa tylko do
            // odczytu nie oferuje ręcznego przestawiania elementów.
            "Custom" => indexed.Select(entry => entry.Item),
            _ => indexed
                .OrderByDescending(entry => entry.Item.CollectionAddedUtcTicks.HasValue)
                .ThenByDescending(entry => entry.Item.CollectionAddedUtcTicks ?? long.MinValue)
                .ThenBy(entry => entry.Index)
                .Select(entry => entry.Item)
        };
    }

    private static string NormalizeCollectionSortMode(string? value) =>
        value?.Trim() switch
        {
            "Alphabetical" => "Alphabetical",
            "Custom" => "Custom",
            _ => "AddedNewest"
        };

    private static bool IsSupportedItem(MediaItem item) =>
        !string.IsNullOrWhiteSpace(item.Title)
        && TryNormalizeExternalId(item.ExternalId, item.Kind, out _)
        && item.Kind is MediaItemKind.Track or MediaItemKind.Album
            or MediaItemKind.Artist or MediaItemKind.Playlist
            or MediaItemKind.Podcast or MediaItemKind.Episode;

    internal static bool TryNormalizeExternalId(
        string? value,
        MediaItemKind kind,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var candidate = value.Trim();
        var expectedType = ServiceType(kind);
        if (expectedType is null) return false;
        if (candidate.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = candidate.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3
                || !string.Equals(parts[1], expectedType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            candidate = parts[2];
        }
        if (candidate.Length is 0 or > MaximumExternalIdLength
            || !candidate.All(char.IsLetterOrDigit))
        {
            return false;
        }
        normalized = candidate;
        return true;
    }

    internal static string? ServiceType(MediaItemKind kind) => kind switch
    {
        MediaItemKind.Track => "track",
        MediaItemKind.Album => "album",
        MediaItemKind.Artist => "artist",
        MediaItemKind.Playlist => "playlist",
        MediaItemKind.Podcast => "show",
        MediaItemKind.Episode => "episode",
        _ => null
    };

    internal static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var clean = new string(value
            .Where(character => !char.IsControl(character))
            .ToArray()).Trim();
        if (clean.Length > MaximumLabelPartLength)
            clean = clean[..MaximumLabelPartLength].TrimEnd() + "…";
        return clean;
    }

    internal static string CleanDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var clean = new string(value
            .Where(character => !char.IsControl(character)
                || character is '\r' or '\n' or '\t')
            .ToArray()).Trim();
        return clean.Length <= MaximumDescriptionLength
            ? clean
            : clean[..MaximumDescriptionLength].TrimEnd() + "…";
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string PlanLabel(string? product) => product?.Trim().ToLowerInvariant() switch
    {
        "premium" => "Premium",
        "free" or "open" => "bezpłatne",
        _ => "nieznany"
    };

    internal static string? PublicUri(MediaItem item)
    {
        if (!Uri.TryCreate(item.PublicUri, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "open.spotify.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return uri.AbsoluteUri;
    }
}

public enum LiteSpotifyFailureReason
{
    Unknown,
    NotConfigured,
    NotConnected,
    AccessDenied,
    NotFound,
    RateLimited,
    Network,
    Timeout,
    Cancelled
}

public sealed record LiteSpotifyCapabilitiesResult(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("readOnly")] bool ReadOnly,
    [property: JsonPropertyName("oneSession")] bool OneSession,
    [property: JsonPropertyName("accountStatus")] bool AccountStatus,
    [property: JsonPropertyName("collectionRoot")] bool CollectionRoot,
    [property: JsonPropertyName("collectionRead")] bool CollectionRead,
    [property: JsonPropertyName("containerBrowse")] bool ContainerBrowse,
    [property: JsonPropertyName("catalogSearch")] bool CatalogSearch,
    [property: JsonPropertyName("collectionWrite")] bool CollectionWrite,
    [property: JsonPropertyName("login")] bool Login,
    [property: JsonPropertyName("playback")] bool Playback,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyAccountProbe(
    bool CatalogConfigured,
    bool? CatalogConnected,
    string AccountDisplayName,
    string AccountProduct,
    bool? PlaybackConnected,
    string RedirectUri = "http://127.0.0.1:43822/spotify/callback/");

public sealed record LiteSpotifyAccountStatusResult(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("catalog")] LiteSpotifyConnectionStatus Catalog,
    [property: JsonPropertyName("playback")] LiteSpotifyConnectionStatus Playback,
    [property: JsonPropertyName("redirectUri")] string RedirectUri);

public sealed record LiteSpotifyLoginResult(
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyPairingStartResult(
    [property: JsonPropertyName("userCode")] string UserCode,
    [property: JsonPropertyName("verificationUri")] string VerificationUri,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyPairingStatusResult(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyConnectionStatus(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("configured")] bool Configured,
    [property: JsonPropertyName("connected")] bool Connected,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("accountDisplayName")] string? AccountDisplayName,
    [property: JsonPropertyName("accountPlan")] string AccountPlan);

public sealed record LiteSpotifyCollectionRootResult(
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("views")] IReadOnlyList<LiteSpotifyCollectionRootItem> Views);

public sealed record LiteSpotifyCollectionRootItem(
    [property: JsonPropertyName("view")] string View,
    [property: JsonPropertyName("label")] string Label);

public sealed record LiteSpotifyContainerRequest(
    MediaItemKind Kind,
    string ExternalId,
    string Title,
    string Artist,
    string? ArtistSection,
    int LoadedCount = LiteSpotifyCatalogContract.CollectionPageSize);

public sealed record LiteSpotifyDescriptionRequest(
    MediaItemKind Kind,
    string ExternalId);

public sealed record LiteSpotifyDescriptionResult(
    [property: JsonPropertyName("description")] string Description);

public sealed record LiteSpotifyMembershipRequest(
    string Mode,
    IReadOnlyList<MediaItem> Items,
    bool? RequestedAddition);

public sealed record LiteSpotifyMembershipResult(
    [property: JsonPropertyName("added")] bool Added,
    [property: JsonPropertyName("changed")] int Changed,
    [property: JsonPropertyName("itemIds")] IReadOnlyList<string> ItemIds,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyPlaylistCreateRequest(string Name, bool IsPublic);

public sealed record LiteSpotifyPlaylistAddRequest(
    IReadOnlyList<string> PlaylistIds,
    IReadOnlyList<MediaItem> Items);

public sealed record LiteSpotifyPlaylistTarget(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isPublic")] bool IsPublic,
    [property: JsonPropertyName("collaborative")] bool Collaborative,
    [property: JsonPropertyName("accessibleLabel")] string AccessibleLabel,
    [property: JsonPropertyName("snapshotId")] string SnapshotId)
{
    public override string ToString() => AccessibleLabel;
}

public sealed record LiteSpotifyPlaylistTargetsResult(
    [property: JsonPropertyName("playlists")] IReadOnlyList<LiteSpotifyPlaylistTarget> Playlists,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyPlaylistCreateResult(
    [property: JsonPropertyName("playlist")] LiteSpotifyPlaylistTarget Playlist,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyPlaylistOutcome(
    [property: JsonPropertyName("playlistId")] string PlaylistId,
    [property: JsonPropertyName("accessibleLabel")] string AccessibleLabel,
    [property: JsonPropertyName("added")] int Added,
    [property: JsonPropertyName("alreadyPresent")] int AlreadyPresent,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("snapshotId")] string SnapshotId,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyPlaylistAddResult(
    [property: JsonPropertyName("requestedItems")] int RequestedItems,
    [property: JsonPropertyName("added")] int Added,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("partial")] bool Partial,
    [property: JsonPropertyName("playlists")] IReadOnlyList<LiteSpotifyPlaylistOutcome> Playlists,
    [property: JsonPropertyName("message")] string Message);

public sealed record LiteSpotifyItemsResult(
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("items")] IReadOnlyList<LiteSpotifyCatalogItem> Items,
    [property: JsonPropertyName("warning")] string Warning,
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("hasMore")] bool HasMore);

public sealed record LiteSpotifyCatalogItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("externalId")] string ExternalId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("accessibleLabel")] string AccessibleLabel,
    [property: JsonPropertyName("durationTicks")] long DurationTicks,
    [property: JsonPropertyName("publicUri")] string? PublicUri,
    [property: JsonPropertyName("relatedAlbumExternalId")] string? RelatedAlbumExternalId,
    [property: JsonPropertyName("relatedAlbumTitle")] string? RelatedAlbumTitle,
    [property: JsonPropertyName("relatedArtistExternalId")] string? RelatedArtistExternalId,
    [property: JsonPropertyName("relatedArtistName")] string? RelatedArtistName,
    [property: JsonPropertyName("collectionAddedUtcTicks")] long? CollectionAddedUtcTicks,
    [property: JsonPropertyName("isFavorite")] bool IsFavorite,
    [property: JsonPropertyName("isInLibrary")] bool IsInLibrary,
    [property: JsonPropertyName("isAvailable")] bool IsAvailable)
{
    public static LiteSpotifyCatalogItem FromMediaItem(
        MediaItem item,
        int? occurrenceIndex = null)
    {
        if (!LiteSpotifyCatalogContract.TryNormalizeExternalId(
                item.ExternalId, item.Kind, out var externalId))
        {
            throw new ArgumentException("Nieprawidłowy element Spotify.", nameof(item));
        }
        var serviceType = LiteSpotifyCatalogContract.ServiceType(item.Kind)!;
        var title = LiteSpotifyCatalogContract.CleanText(item.Title);
        var artist = LiteSpotifyCatalogContract.CleanText(item.Artist);
        var kindLabel = item.Kind switch
        {
            MediaItemKind.Track => "utwór",
            MediaItemKind.Album => "album",
            MediaItemKind.Artist => "wykonawca",
            MediaItemKind.Playlist => "playlista",
            MediaItemKind.Podcast => "podcast",
            MediaItemKind.Episode => "odcinek",
            _ => "element"
        };
        var label = artist.Length == 0
            ? $"{title}, {kindLabel}"
            : $"{title}, {artist}, {kindLabel}";
        if (!item.IsAvailable) label += ", niedostępny";
        var id = $"spotify:{serviceType}:{externalId}";
        if (occurrenceIndex is { } index) id += $":occurrence:{index}";
        return new LiteSpotifyCatalogItem(
            id,
            externalId,
            title,
            artist,
            item.Kind.ToString().ToLowerInvariant(),
            label,
            Math.Max(0, item.Duration.Ticks),
            LiteSpotifyCatalogContract.PublicUri(item),
            NormalizeRelated(item.RelatedAlbumExternalId),
            NullIfEmpty(LiteSpotifyCatalogContract.CleanText(item.RelatedAlbumTitle)),
            NormalizeRelated(item.RelatedArtistExternalId),
            NullIfEmpty(LiteSpotifyCatalogContract.CleanText(item.RelatedArtistName)),
            item.CollectionAddedUtcTicks,
            item.IsFavorite,
            item.IsInLibrary,
            item.IsAvailable);
    }

    private static string? NormalizeRelated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var candidate = value.Trim();
        if (candidate.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = candidate.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return null;
            candidate = parts[2];
        }
        return candidate.Length is > 0 and <= 128 && candidate.All(char.IsLetterOrDigit)
            ? candidate
            : null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
