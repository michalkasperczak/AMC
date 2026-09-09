using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Tidal;

public sealed record TidalCollectionSyncResult(
    IReadOnlyList<MediaItem> Items,
    IReadOnlySet<MediaItemKind> UpdatedKinds,
    IReadOnlyList<string> Warnings);

public sealed record TidalAccountIdentity(string UserId, string? DisplayName);

public sealed class TidalApiException(
    string message,
    HttpStatusCode statusCode,
    TimeSpan? retryAfter = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool IsAuthorizationFailure => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

/// <summary>
/// Thin adapter for the documented TIDAL JSON:API. Playback is deliberately
/// not implemented here: protected media must use TIDAL's official Player
/// module rather than extracted stream URLs.
/// </summary>
public sealed class TidalApiClient(HttpClient? httpClient = null) : IDisposable
{
    private static readonly Uri ApiBaseAddress = new("https://openapi.tidal.com/v2/");
    private readonly HttpClient http = httpClient ?? new HttpClient
    {
        BaseAddress = ApiBaseAddress,
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly bool ownsHttpClient = httpClient is null;

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string accessToken,
        string countryCode,
        string query,
        IReadOnlySet<string>? collectionExternalIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var path = $"searchResults?filter%5Bquery%5D={Uri.EscapeDataString(query.Trim())}" +
                   $"&countryCode={Uri.EscapeDataString(NormalizeCountryCode(countryCode))}" +
                   "&include=tracks,tracks.artists,tracks.albums,albums,albums.artists,artists,playlists";
        using var document = await GetDocumentAsync(path, accessToken, cancellationToken).ConfigureAwait(false);
        var directResultKeys = GetDirectSearchResultKeys(document.RootElement);
        var mapped = MapResources(
                document.RootElement,
                expectedTypes: null,
                collectionExternalIds,
                favoriteByDefault: false);
        return mapped
            .Where(item => directResultKeys.Count == 0
                || item.ExternalId is { } externalId && directResultKeys.Contains(externalId))
            .Where(item => item.Kind is MediaItemKind.Track or MediaItemKind.Album
                or MediaItemKind.Artist or MediaItemKind.Playlist)
            .ToArray();
    }

    public async Task<IReadOnlyList<MediaItem>> GetContainerItemsAsync(
        string accessToken,
        string countryCode,
        MediaItem container,
        IReadOnlySet<string>? collectionExternalIds,
        CancellationToken cancellationToken)
    {
        if (!TryParseExternalId(container.ExternalId, out var type, out var id))
            throw new InvalidOperationException("Element TIDAL nie ma prawidłowego identyfikatora katalogowego.");

        var request = type switch
        {
            "albums" => new ContainerRequest(
                "items",
                "items.artists,items.albums",
                new HashSet<string>(["tracks"], StringComparer.Ordinal)),
            "playlists" => new ContainerRequest(
                "items",
                "items.tracks:artists,items.tracks:albums",
                new HashSet<string>(["tracks", "videos"], StringComparer.Ordinal)),
            "artists" => new ContainerRequest(
                "albums",
                "albums.artists",
                new HashSet<string>(["albums"], StringComparer.Ordinal)),
            _ => throw new InvalidOperationException("Ten rodzaj elementu TIDAL nie zawiera listy możliwej do otwarcia.")
        };
        var firstPath = $"{type}/{Uri.EscapeDataString(id)}/relationships/{request.Relationship}" +
                        $"?include={Uri.EscapeDataString(request.Include)}" +
                        $"&countryCode={Uri.EscapeDataString(NormalizeCountryCode(countryCode))}";
        var next = new Uri(ApiBaseAddress, firstPath);
        var result = new List<MediaItem>();
        var pages = 0;
        while (next is not null && pages++ < 200)
        {
            using var document = await GetDocumentAsync(next, accessToken, cancellationToken).ConfigureAwait(false);
            result.AddRange(MapRelationshipResourcesInOrder(
                document.RootElement,
                request.ExpectedTypes,
                collectionExternalIds));
            next = TryGetNextPage(document.RootElement);
            if (next is not null)
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return result.DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task ChangeCollectionMembershipAsync(
        string accessToken,
        IReadOnlyList<MediaItem> items,
        bool add,
        CancellationToken cancellationToken)
    {
        var parsed = items
            .Select(item => TryParseExternalId(item.ExternalId, out var type, out var id)
                ? new CollectionMutationItem(type, id)
                : null)
            .ToArray();
        if (parsed.Any(item => item is null))
            throw new InvalidOperationException("Nie wszystkie wybrane elementy mają prawidłowy identyfikator TIDAL.");
        var resources = parsed
            .Select(item => item!)
            .DistinctBy(item => $"{item.Type}:{item.Id}", StringComparer.Ordinal)
            .ToArray();

        foreach (var group in resources.GroupBy(item => item.Type, StringComparer.Ordinal))
        {
            var collection = CollectionResourceForType(group.Key);
            foreach (var batch in group.Chunk(50))
            {
                await SendCollectionMutationAsync(
                    accessToken,
                    collection,
                    group.Key,
                    batch.Select(item => item.Id).ToArray(),
                    add,
                    cancellationToken).ConfigureAwait(false);
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task ReorderPlaylistItemsAsync(
        string accessToken,
        MediaItem playlist,
        IReadOnlyList<MediaItem> items,
        string positionBeforeEntryId,
        CancellationToken cancellationToken)
    {
        if (!TryParseExternalId(playlist.ExternalId, out var playlistType, out var playlistId)
            || !string.Equals(playlistType, "playlists", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wybrany element nie jest playlistą TIDAL.");
        }
        if (items.Count is < 1 or > 20)
            throw new InvalidOperationException("Jednocześnie można przenieść od 1 do 20 elementów playlisty TIDAL.");
        if (string.IsNullOrWhiteSpace(positionBeforeEntryId))
            throw new InvalidOperationException("Nie można ustalić miejsca docelowego na playliście TIDAL.");

        var resources = items.Select(item =>
        {
            if (!TryParseExternalId(item.ExternalId, out var type, out var id)
                || type is not ("tracks" or "videos")
                || string.IsNullOrWhiteSpace(item.ContainerEntryId))
            {
                throw new InvalidOperationException(
                    "Nie wszystkie zaznaczone elementy mają dane potrzebne do zmiany kolejności playlisty TIDAL.");
            }
            return new PlaylistMutationItem(type, id, item.ContainerEntryId!);
        }).ToArray();

        var payload = JsonSerializer.Serialize(new
        {
            data = resources.Select(item => new
            {
                id = item.Id,
                type = item.Type,
                meta = new { itemId = item.EntryId }
            }).ToArray(),
            meta = new { positionBefore = positionBeforeEntryId }
        });
        await SendPlaylistItemsUpdateAsync(
            accessToken,
            playlistId,
            payload,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AddPlaylistItemsAsync(
        string accessToken,
        MediaItem playlist,
        IReadOnlyList<MediaItem> items,
        CancellationToken cancellationToken)
    {
        var playlistId = ParsePlaylistId(playlist);
        var resources = ParsePlayablePlaylistItems(items, requireEntryId: false);
        if (resources.Count == 0)
            throw new InvalidOperationException("Brak utworów lub materiałów wideo możliwych do dodania do playlisty TIDAL.");

        foreach (var batch in resources.Chunk(50))
        {
            var payload = JsonSerializer.Serialize(new
            {
                data = batch.Select(item => new { id = item.Id, type = item.Type }).ToArray()
            });
            await SendPlaylistItemsMutationAsync(
                HttpMethod.Post,
                accessToken,
                playlistId,
                payload,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<MediaItem> CreatePlaylistAsync(
        string accessToken,
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 250)
            throw new InvalidOperationException("Nazwa playlisty TIDAL musi mieć od 1 do 250 znaków.");

        var payload = JsonSerializer.Serialize(new
        {
            data = new
            {
                type = "playlists",
                attributes = new { name = normalizedName }
            }
        });
        using var document = await SendPlaylistCreateAsync(
            accessToken,
            payload,
            cancellationToken).ConfigureAwait(false);
        var playlist = MapResources(
                document.RootElement,
                new HashSet<string>(["playlists"], StringComparer.Ordinal),
                collectionExternalIds: null,
                favoriteByDefault: true)
            .SingleOrDefault()
            ?? throw new InvalidOperationException("TIDAL utworzył playlistę, ale nie zwrócił jej danych.");
        playlist.CollectionAddedUtcTicks = DateTime.UtcNow.Ticks;
        TidalCollectionSemantics.ApplyMembership(playlist, true);
        return playlist;
    }

    public async Task RemovePlaylistItemsAsync(
        string accessToken,
        MediaItem playlist,
        IReadOnlyList<MediaItem> items,
        CancellationToken cancellationToken)
    {
        var playlistId = ParsePlaylistId(playlist);
        var resources = ParsePlayablePlaylistItems(items, requireEntryId: true);
        if (resources.Count == 0)
            throw new InvalidOperationException("Brak elementów możliwych do usunięcia z playlisty TIDAL.");

        foreach (var batch in resources.Chunk(50))
        {
            var payload = JsonSerializer.Serialize(new
            {
                data = batch.Select(item => new
                {
                    id = item.Id,
                    type = item.Type,
                    meta = new { itemId = item.EntryId }
                }).ToArray()
            });
            await SendPlaylistItemsMutationAsync(
                HttpMethod.Delete,
                accessToken,
                playlistId,
                payload,
                cancellationToken).ConfigureAwait(false);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<TidalCollectionSyncResult> SynchronizeCollectionAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requests = new[]
        {
            new CollectionRequest("utworów", "userCollectionTracks", "tracks", "items.artists,items.albums", MediaItemKind.Track),
            new CollectionRequest("albumów", "userCollectionAlbums", "albums", "items.artists", MediaItemKind.Album),
            new CollectionRequest("wykonawców", "userCollectionArtists", "artists", "items", MediaItemKind.Artist),
            new CollectionRequest("playlist", "userCollectionPlaylists", "playlists", "items", MediaItemKind.Playlist),
            new CollectionRequest("materiałów wideo", "userCollectionVideos", "videos", "items.artists,items.albums", MediaItemKind.Video)
        };
        var items = new List<MediaItem>();
        var updatedKinds = new HashSet<MediaItemKind>();
        var warnings = new List<string>();
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var collectionItems = await ReadCollectionAsync(
                    request,
                    accessToken,
                    cancellationToken).ConfigureAwait(false);
                items.AddRange(collectionItems);
                updatedKinds.Add(request.Kind);
            }
            catch (TidalApiException exception) when (!exception.IsAuthorizationFailure)
            {
                warnings.Add($"Nie odświeżono kolekcji {request.PolishName}: {exception.Message}");
            }

            // The public API has a deliberately low request rate. Keeping
            // collection families sequential avoids bursts and 429 errors.
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return new TidalCollectionSyncResult(
            items.DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            updatedKinds,
            warnings);
    }

    public async Task<TidalAccountIdentity> GetAccountIdentityAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var document = await GetDocumentAsync("users/me", accessToken, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("TIDAL nie zwrócił identyfikatora zalogowanego użytkownika.");
        }

        var userId = data.TryGetProperty("id", out var idElement)
            ? idElement.GetString()?.Trim()
            : null;
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidDataException("TIDAL nie zwrócił identyfikatora zalogowanego użytkownika.");

        var displayName = data.TryGetProperty("attributes", out var attributes)
            ? FirstNonEmptyString(attributes, "displayName", "profileName", "username", "email")
            : null;
        return new TidalAccountIdentity(userId, displayName);
    }

    private async Task<IReadOnlyList<MediaItem>> ReadCollectionAsync(
        CollectionRequest collection,
        string accessToken,
        CancellationToken cancellationToken)
    {
        // The current TIDAL JSON:API rejects undocumented query parameters.
        // User-collection relationship endpoints do not accept countryCode;
        // catalogue/search endpoints still do.
        var firstPath = $"{collection.Resource}/me/relationships/items" +
                        $"?include={Uri.EscapeDataString(collection.Include)}";
        var next = new Uri(ApiBaseAddress, firstPath);
        var result = new List<MediaItem>();
        var pages = 0;
        while (next is not null && pages++ < 200)
        {
            using var document = await GetDocumentAsync(next, accessToken, cancellationToken).ConfigureAwait(false);
            result.AddRange(MapRelationshipResourcesInOrder(
                document.RootElement,
                new HashSet<string>([collection.ItemType], StringComparer.Ordinal),
                collectionExternalIds: null,
                favoriteByDefault: true));
            next = TryGetNextPage(document.RootElement);
            if (next is not null)
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }

    private async Task<JsonDocument> GetDocumentAsync(
        string path,
        string accessToken,
        CancellationToken cancellationToken) =>
        await GetDocumentAsync(new Uri(ApiBaseAddress, path), accessToken, cancellationToken).ConfigureAwait(false);

    private async Task<JsonDocument> GetDocumentAsync(
        Uri address,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(address.Host, ApiBaseAddress.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Odrzucono nieprawidłowy adres kolejnej strony TIDAL.");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(
                    delay < TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : delay,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new TidalApiException(
                    UserFacingHttpError(response.StatusCode),
                    response.StatusCode,
                    response.Headers.RetryAfter?.Delta);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        throw new TidalApiException(
            "TIDAL ograniczył liczbę zapytań. Spróbuj ponownie za chwilę.",
            HttpStatusCode.TooManyRequests);
    }

    private async Task SendCollectionMutationAsync(
        string accessToken,
        string collectionResource,
        string itemType,
        IReadOnlyList<string> ids,
        bool add,
        CancellationToken cancellationToken)
    {
        var address = new Uri(ApiBaseAddress, $"{collectionResource}/me/relationships/items");
        var payload = JsonSerializer.Serialize(new
        {
            data = ids.Select(id => new { id, type = itemType }).ToArray()
        });
        var idempotencyKey = Guid.NewGuid().ToString("D");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(add ? HttpMethod.Post : HttpMethod.Delete, address);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(
                    delay < TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : delay,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new TidalApiException(
                    UserFacingHttpError(response.StatusCode),
                    response.StatusCode,
                    response.Headers.RetryAfter?.Delta);
            return;
        }

        throw new TidalApiException(
            "TIDAL ograniczył liczbę zapytań. Spróbuj ponownie za chwilę.",
            HttpStatusCode.TooManyRequests);
    }

    private async Task SendPlaylistItemsUpdateAsync(
        string accessToken,
        string playlistId,
        string payload,
        CancellationToken cancellationToken)
        => await SendPlaylistItemsMutationAsync(
            HttpMethod.Patch,
            accessToken,
            playlistId,
            payload,
            cancellationToken).ConfigureAwait(false);

    private async Task<JsonDocument> SendPlaylistCreateAsync(
        string accessToken,
        string payload,
        CancellationToken cancellationToken)
    {
        var address = new Uri(ApiBaseAddress, "playlists");
        var idempotencyKey = Guid.NewGuid().ToString("D");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, address);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(
                    delay < TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : delay,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new TidalApiException(
                    UserFacingHttpError(response.StatusCode),
                    response.StatusCode,
                    response.Headers.RetryAfter?.Delta);
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "TIDAL utworzył playlistę, ale zwrócił nieprawidłową odpowiedź.",
                    exception);
            }
        }

        throw new TidalApiException(
            "TIDAL ograniczył liczbę zapytań. Spróbuj ponownie za chwilę.",
            HttpStatusCode.TooManyRequests);
    }

    private async Task SendPlaylistItemsMutationAsync(
        HttpMethod method,
        string accessToken,
        string playlistId,
        string payload,
        CancellationToken cancellationToken)
    {
        var address = new Uri(
            ApiBaseAddress,
            $"playlists/{Uri.EscapeDataString(playlistId)}/relationships/items");
        var idempotencyKey = Guid.NewGuid().ToString("D");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(method, address);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(payload));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var delay = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.FromSeconds(attempt + 1);
                await Task.Delay(
                    delay < TimeSpan.FromMilliseconds(250) ? TimeSpan.FromMilliseconds(250) : delay,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!response.IsSuccessStatusCode)
                throw new TidalApiException(
                    UserFacingHttpError(response.StatusCode),
                    response.StatusCode,
                    response.Headers.RetryAfter?.Delta);
            return;
        }

        throw new TidalApiException(
            "TIDAL ograniczył liczbę zapytań. Spróbuj ponownie za chwilę.",
            HttpStatusCode.TooManyRequests);
    }

    private static string ParsePlaylistId(MediaItem playlist)
    {
        if (!TryParseExternalId(playlist.ExternalId, out var type, out var id)
            || !string.Equals(type, "playlists", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wybrany element nie jest playlistą TIDAL.");
        }
        return id;
    }

    private static IReadOnlyList<PlaylistMutationItem> ParsePlayablePlaylistItems(
        IReadOnlyList<MediaItem> items,
        bool requireEntryId)
    {
        var resources = new List<PlaylistMutationItem>(items.Count);
        foreach (var item in items)
        {
            if (!TryParseExternalId(item.ExternalId, out var type, out var id)
                || type is not ("tracks" or "videos")
                || requireEntryId && string.IsNullOrWhiteSpace(item.ContainerEntryId))
            {
                throw new InvalidOperationException(requireEntryId
                    ? "Nie wszystkie zaznaczone elementy mają dane potrzebne do usunięcia z playlisty TIDAL."
                    : "Do playlisty TIDAL można dodać utwory albo materiały wideo.");
            }
            resources.Add(new PlaylistMutationItem(type, id, item.ContainerEntryId ?? string.Empty));
        }
        return resources;
    }

    private static string UserFacingHttpError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Sesja TIDAL wygasła. Zaloguj się ponownie.",
        HttpStatusCode.Forbidden => "TIDAL nie udzielił wymaganych uprawnień.",
        HttpStatusCode.NotFound => "Żądany element nie jest dostępny w katalogu TIDAL.",
        HttpStatusCode.TooManyRequests => "TIDAL ograniczył liczbę zapytań. Spróbuj ponownie za chwilę.",
        _ => $"TIDAL zwrócił błąd HTTP {(int)statusCode}."
    };

    internal static IReadOnlyList<MediaItem> MapResources(
        JsonElement root,
        IReadOnlySet<string>? expectedTypes,
        IReadOnlySet<string>? collectionExternalIds,
        bool favoriteByDefault)
    {
        var resources = EnumerateResourceObjects(root).ToArray();
        var byKey = resources
            .Where(resource => TryResourceIdentity(resource, out _, out _))
            .GroupBy(resource => ResourceKey(resource), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var mapped = new List<MediaItem>();
        foreach (var resource in resources)
        {
            if (!TryResourceIdentity(resource, out var type, out var id)) continue;
            if (expectedTypes is not null && !expectedTypes.Contains(type)) continue;
            var kind = type switch
            {
                "tracks" => MediaItemKind.Track,
                "albums" => MediaItemKind.Album,
                "artists" => MediaItemKind.Artist,
                "playlists" => MediaItemKind.Playlist,
                "videos" => MediaItemKind.Video,
                _ => (MediaItemKind?)null
            };
            if (kind is null) continue;
            if (!resource.TryGetProperty("attributes", out var attributes)) continue;
            var title = type == "artists"
                ? FirstNonEmptyString(attributes, "name")
                : FirstNonEmptyString(attributes, "title", "name");
            if (string.IsNullOrWhiteSpace(title)) continue;
            var externalId = $"{type}:{id}";
            var favorite = favoriteByDefault || collectionExternalIds?.Contains(externalId) == true;
            var relatedAlbumExternalId = kind == MediaItemKind.Album
                ? externalId
                : ResolveFirstRelationshipExternalId(resource, "albums");
            var relatedArtistExternalId = kind == MediaItemKind.Artist
                ? externalId
                : ResolveFirstRelationshipExternalId(resource, "artists");
            var artistNames = ResolveArtistNames(resource, byKey);
            mapped.Add(new MediaItem
            {
                Id = $"tidal:{externalId}",
                ExternalId = externalId,
                Title = title,
                Artist = artistNames,
                Kind = kind.Value,
                Duration = ParseDuration(attributes),
                Source = $"tidal:{externalId}",
                PublicUri = BuildPublicUri(type, id),
                RelatedAlbumExternalId = relatedAlbumExternalId,
                RelatedAlbumTitle = kind == MediaItemKind.Album
                    ? title
                    : ResolveRelatedTitle(relatedAlbumExternalId, byKey),
                RelatedArtistExternalId = relatedArtistExternalId,
                RelatedArtistName = kind == MediaItemKind.Artist
                    ? title
                    : ResolveRelatedTitle(relatedArtistExternalId, byKey) ?? artistNames,
                IsFavorite = TidalCollectionSemantics.UsesFavorites(kind.Value) && favorite,
                IsInLibrary = TidalCollectionSemantics.UsesLibrary(kind.Value) && favorite
            });
        }
        return mapped.DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    internal static IReadOnlyList<MediaItem> MapRelationshipResourcesInOrder(
        JsonElement root,
        IReadOnlySet<string> expectedTypes,
        IReadOnlySet<string>? collectionExternalIds,
        bool favoriteByDefault = false)
    {
        var mapped = MapResources(root, expectedTypes, collectionExternalIds, favoriteByDefault);
        var byExternalId = mapped
            .Where(item => item.ExternalId is { Length: > 0 })
            .ToDictionary(item => item.ExternalId!, StringComparer.Ordinal);
        var ordered = new List<MediaItem>();
        if (root.TryGetProperty("data", out var data))
        {
            var identifiers = data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().ToArray()
                : data.ValueKind == JsonValueKind.Object ? [data] : [];
            foreach (var identifier in identifiers)
            {
                var key = ResourceKey(identifier);
                if (!byExternalId.TryGetValue(key, out var item)) continue;
                var entryId = TryGetString(identifier, "meta", "itemId");
                var addedUtcTicks = ParseCollectionAddedUtcTicks(identifier);
                ordered.Add(CloneContainerItem(item, entryId, addedUtcTicks));
            }
        }
        ordered.AddRange(mapped.Where(item => ordered.All(existing =>
            !string.Equals(existing.ExternalId, item.ExternalId, StringComparison.Ordinal))));
        return ordered;
    }

    private static long? ParseCollectionAddedUtcTicks(JsonElement identifier)
    {
        var value = TryGetString(identifier, "meta", "addedAt");
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var addedAt)
            ? addedAt.UtcDateTime.Ticks
            : null;
    }

    private static MediaItem CloneContainerItem(
        MediaItem item,
        string? entryId,
        long? collectionAddedUtcTicks = null) => new()
    {
        Id = string.IsNullOrWhiteSpace(entryId)
            ? item.Id
            : $"{item.Id}:entry:{entryId}",
        ExternalId = item.ExternalId,
        RelatedAlbumExternalId = item.RelatedAlbumExternalId,
        RelatedAlbumTitle = item.RelatedAlbumTitle,
        RelatedArtistExternalId = item.RelatedArtistExternalId,
        RelatedArtistName = item.RelatedArtistName,
        ContainerEntryId = entryId,
        CollectionAddedUtcTicks = collectionAddedUtcTicks ?? item.CollectionAddedUtcTicks,
        Title = item.Title,
        HasCustomTitle = item.HasCustomTitle,
        Artist = item.Artist,
        Kind = item.Kind,
        Duration = item.Duration,
        BitrateKbps = item.BitrateKbps,
        IsBitrateEstimated = item.IsBitrateEstimated,
        SampleRateHz = item.SampleRateHz,
        Source = item.Source,
        PublicUri = item.PublicUri,
        HomepageUri = item.HomepageUri,
        Country = item.Country,
        Language = item.Language,
        Tags = item.Tags,
        Codec = item.Codec,
        IsFavorite = item.IsFavorite,
        IsInLibrary = item.IsInLibrary,
        IsAvailable = item.IsAvailable,
        IsInQueue = item.IsInQueue,
        IsPlayNext = item.IsPlayNext
    };

    private static string? TryGetString(JsonElement resource, string objectName, string propertyName) =>
        resource.TryGetProperty(objectName, out var nested)
        && nested.ValueKind == JsonValueKind.Object
        && nested.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<JsonElement> EnumerateResourceObjects(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                foreach (var resource in data.EnumerateArray())
                    if (resource.ValueKind == JsonValueKind.Object && resource.TryGetProperty("attributes", out _))
                        yield return resource;
            }
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("attributes", out _))
            {
                yield return data;
            }
        }
        if (!root.TryGetProperty("included", out var included)
            || included.ValueKind != JsonValueKind.Array) yield break;
        foreach (var resource in included.EnumerateArray())
            if (resource.ValueKind == JsonValueKind.Object)
                yield return resource;
    }

    private static string ResolveArtistNames(
        JsonElement resource,
        IReadOnlyDictionary<string, JsonElement> resources)
    {
        if (!resource.TryGetProperty("relationships", out var relationships)
            || !relationships.TryGetProperty("artists", out var artists)
            || !artists.TryGetProperty("data", out var data)) return string.Empty;
        var identifiers = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToArray()
            : data.ValueKind == JsonValueKind.Object ? [data] : [];
        return string.Join(", ", identifiers
            .Select(identifier => ResourceKey(identifier))
            .Where(resources.ContainsKey)
            .Select(key => resources[key])
            .Select(artist => artist.TryGetProperty("attributes", out var attributes)
                ? FirstNonEmptyString(attributes, "name")
                : null)
            .Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    private static string? ResolveFirstRelationshipExternalId(
        JsonElement resource,
        string relationshipName)
    {
        if (!resource.TryGetProperty("relationships", out var relationships)
            || !relationships.TryGetProperty(relationshipName, out var relationship)
            || !relationship.TryGetProperty("data", out var data)) return null;
        var identifier = data.ValueKind switch
        {
            JsonValueKind.Array when data.GetArrayLength() > 0 => data[0],
            JsonValueKind.Object => data,
            _ => default
        };
        var key = identifier.ValueKind == JsonValueKind.Object
            ? ResourceKey(identifier)
            : string.Empty;
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    private static string? ResolveRelatedTitle(
        string? externalId,
        IReadOnlyDictionary<string, JsonElement> resources)
    {
        if (string.IsNullOrWhiteSpace(externalId)
            || !resources.TryGetValue(externalId, out var resource)
            || !resource.TryGetProperty("attributes", out var attributes)) return null;
        return FirstNonEmptyString(attributes, "title", "name");
    }

    private static string? FirstNonEmptyString(JsonElement attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!.Trim();
        }
        return null;
    }

    private static TimeSpan ParseDuration(JsonElement attributes)
    {
        if (!attributes.TryGetProperty("duration", out var value)) return TimeSpan.Zero;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var seconds))
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (value.ValueKind != JsonValueKind.String) return TimeSpan.Zero;
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text)) return TimeSpan.Zero;
        try { return XmlConvert.ToTimeSpan(text); }
        catch (FormatException) { return TimeSpan.Zero; }
    }

    private static bool TryResourceIdentity(JsonElement resource, out string type, out string id)
    {
        type = resource.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString() ?? string.Empty
            : string.Empty;
        id = resource.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;
        return !string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(id);
    }

    private static string ResourceKey(JsonElement resource) =>
        TryResourceIdentity(resource, out var type, out var id) ? $"{type}:{id}" : string.Empty;

    private static Uri? TryGetNextPage(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links)
            || !links.TryGetProperty("next", out var next)) return null;
        var address = next.ValueKind == JsonValueKind.String
            ? next.GetString()
            : next.ValueKind == JsonValueKind.Object
              && next.TryGetProperty("href", out var href)
                ? href.GetString()
                : null;
        if (Uri.TryCreate(address, UriKind.Absolute, out var absolute)) return absolute;
        if (string.IsNullOrWhiteSpace(address)) return null;

        // TIDAL currently returns collection cursors as paths such as
        // /userCollectionTracks/me/relationships/items?page[cursor]=20.
        // Although the leading slash looks root-relative, the public API is
        // rooted at /v2. Resolving it with Uri directly would silently drop
        // /v2 and make every second page return 404.
        var apiRelativeAddress = address.StartsWith("/v2/", StringComparison.OrdinalIgnoreCase)
            ? address[4..]
            : address.TrimStart('/');
        return Uri.TryCreate(ApiBaseAddress, apiRelativeAddress, out var relative) ? relative : null;
    }

    private static HashSet<string> GetDirectSearchResultKeys(JsonElement root)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("data", out var data)) return keys;
        var searchResources = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToArray()
            : data.ValueKind == JsonValueKind.Object ? [data] : [];
        foreach (var searchResource in searchResources)
        {
            if (!searchResource.TryGetProperty("relationships", out var relationships)) continue;
            foreach (var relationshipName in new[] { "tracks", "albums", "artists", "playlists" })
            {
                if (!relationships.TryGetProperty(relationshipName, out var relationship)
                    || !relationship.TryGetProperty("data", out var identifiers)) continue;
                var values = identifiers.ValueKind == JsonValueKind.Array
                    ? identifiers.EnumerateArray().ToArray()
                    : identifiers.ValueKind == JsonValueKind.Object ? [identifiers] : [];
                foreach (var identifier in values)
                {
                    var key = ResourceKey(identifier);
                    if (!string.IsNullOrWhiteSpace(key)) keys.Add(key);
                }
            }
        }
        return keys;
    }

    private static string BuildPublicUri(string type, string id) =>
        $"https://tidal.com/browse/{type switch
        {
            "tracks" => "track",
            "albums" => "album",
            "artists" => "artist",
            "playlists" => "playlist",
            "videos" => "video",
            _ => ""
        }}/{Uri.EscapeDataString(id)}";

    private static string NormalizeCountryCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 2 && normalized.All(character => character is >= 'A' and <= 'Z')
            ? normalized
            : "PL";
    }

    internal static bool TryParseExternalId(string? externalId, out string type, out string id)
    {
        type = string.Empty;
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(externalId)) return false;
        var separator = externalId.IndexOf(':');
        if (separator <= 0 || separator == externalId.Length - 1) return false;
        type = externalId[..separator];
        id = externalId[(separator + 1)..];
        return type is "tracks" or "albums" or "artists" or "playlists" or "videos"
            && !string.IsNullOrWhiteSpace(id);
    }

    private static string CollectionResourceForType(string type) => type switch
    {
        "tracks" => "userCollectionTracks",
        "albums" => "userCollectionAlbums",
        "artists" => "userCollectionArtists",
        "playlists" => "userCollectionPlaylists",
        "videos" => "userCollectionVideos",
        _ => throw new InvalidOperationException("Ten rodzaj elementu nie może należeć do kolekcji TIDAL.")
    };

    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }

    private sealed record CollectionRequest(
        string PolishName,
        string Resource,
        string ItemType,
        string Include,
        MediaItemKind Kind);

    private sealed record ContainerRequest(
        string Relationship,
        string Include,
        IReadOnlySet<string> ExpectedTypes);

    private sealed record CollectionMutationItem(string Type, string Id);

    private sealed record PlaylistMutationItem(string Type, string Id, string EntryId);
}
