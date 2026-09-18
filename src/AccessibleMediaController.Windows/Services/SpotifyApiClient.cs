using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

/// <summary>Co udało się pobrać i o czym trzeba uczciwie powiedzieć, że się nie udało.</summary>
internal sealed record SpotifyCollectionResult(
    IReadOnlyList<MediaItem> Items,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MediaItemKind> UpdatedKinds);

internal sealed class SpotifyApiException(string message, HttpStatusCode statusCode)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsAuthorizationFailure =>
        StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

/// <summary>
/// Odczyt biblioteki Spotify: polubione utwory, albumy, wykonawcy i playlisty.
///
/// Granice narzucone przez Spotify (zmiana API z listopada 2024, w mocy 2026) -
/// nie próbuj ich obchodzić, bo kończy się to blokadą aplikacji:
/// - Zawartości CUDZYCH playlist (w tym redakcyjnych Spotify) NIE da się
///   odczytać. Pobieramy nazwę i właściciela, nic więcej. Playlisty Michała
///   czytamy w całości.
/// - Nie ma pobierania hurtowego. Wszystko idzie stronami po 50 pozycji,
///   dlatego wynik zapisuje się lokalnie, a nie pobiera przy każdym otwarciu.
///
/// Limit zapytań: Spotify odpowiada 429 z nagłówkiem Retry-After. Czekamy
/// dokładnie tyle, ile każe - własne domysły co do opóźnienia kończą się
/// czasową blokadą całej aplikacji, nie jednego zapytania.
/// </summary>
internal sealed class SpotifyApiClient(HttpClient? httpClient = null) : IDisposable
{
    private const string ApiRoot = "https://api.spotify.com/v1";
    private const int PageSize = 50;
    // Zabezpiecznik na wypadek zapętlenia stronicowania: biblioteka Michała ma
    // rzędy tysięcy pozycji, ale nieskończona pętla przy błędzie API zawiesiłaby
    // synchronizację na zawsze.
    private const int MaxPages = 400;

    private readonly HttpClient http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly bool ownsHttpClient = httpClient is null;

    /// <summary>
    /// Pobiera bibliotekę. Częściowa awaria NIE przerywa całości: gdy padnie
    /// jedna kolekcja, pozostałe i tak wracają, a użytkownik dostaje ostrzeżenie.
    /// Zwrócenie pustej listy jako "sukcesu" byłoby gorsze - wyglądałoby jak
    /// puste konto.
    /// </summary>
    public async Task<SpotifyCollectionResult> SynchronizeCollectionAsync(
        string accessToken,
        string grantedScope,
        CancellationToken cancellationToken)
    {
        var items = new List<MediaItem>();
        var warnings = new List<string>();
        var updated = new List<MediaItemKind>();

        if (!SpotifyScopesHave(grantedScope, "user-library-read"))
        {
            warnings.Add(
                "Konto nie przyznało dostępu do biblioteki. Zaloguj się ponownie, żeby pobrać polubione utwory i albumy.");
        }
        else
        {
            await CollectAsync(
                "Polubione utwory",
                MediaItemKind.Track,
                $"{ApiRoot}/me/tracks?limit={PageSize}",
                ReadSavedTrack,
                items, warnings, updated, cancellationToken).ConfigureAwait(false);

            await CollectAsync(
                "Albumy",
                MediaItemKind.Album,
                $"{ApiRoot}/me/albums?limit={PageSize}",
                ReadSavedAlbum,
                items, warnings, updated, cancellationToken).ConfigureAwait(false);
        }

        if (SpotifyScopesHave(grantedScope, "user-follow-read"))
        {
            // Wykonawcy mają INNY kształt odpowiedzi: strony są zagnieżdżone
            // w "artists", a następna strona jest adresowana kursorem, nie
            // przesunięciem. Dlatego osobna ścieżka, nie wspólna pętla.
            await CollectFollowedArtistsAsync(items, warnings, updated, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            warnings.Add("Konto nie przyznało dostępu do listy obserwowanych wykonawców.");
        }

        if (SpotifyScopesHave(grantedScope, "playlist-read-private"))
        {
            await CollectAsync(
                "Playlisty",
                MediaItemKind.Playlist,
                $"{ApiRoot}/me/playlists?limit={PageSize}",
                ReadPlaylist,
                items, warnings, updated, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            warnings.Add("Konto nie przyznało dostępu do playlist.");
        }

        // Podcasty. Spotify trzyma je pod "shows" (zapisane podcasty) i osobno
        // pod "episodes" (pojedyncze zapisane odcinki). Odcinki mają czas i dają
        // się odtwarzać, podcasty są kontenerem do wejścia strzałką w prawo.
        if (SpotifyScopesHave(grantedScope, "user-library-read"))
        {
            await CollectAsync(
                "Podcasty",
                MediaItemKind.Podcast,
                $"{ApiRoot}/me/shows?limit={PageSize}",
                ReadSavedShow,
                items, warnings, updated, cancellationToken).ConfigureAwait(false);

            await CollectAsync(
                "Zapisane odcinki",
                MediaItemKind.Episode,
                $"{ApiRoot}/me/episodes?limit={PageSize}",
                ReadSavedEpisode,
                items, warnings, updated, cancellationToken).ConfigureAwait(false);
        }

        return new SpotifyCollectionResult(items, warnings, updated);
    }

    /// <summary>
    /// Utwory z playlisty. Zwraca null, gdy Spotify odmawia dostępu do
    /// zawartości - to normalne dla playlist redakcyjnych Spotify i playlist
    /// innych osób, więc wywołujący ma o tym powiedzieć spokojnie, nie jako
    /// o awarii.
    /// </summary>
    public async Task<IReadOnlyList<MediaItem>?> GetPlaylistTracksAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        var items = new List<MediaItem>();
        var next = $"{ApiRoot}/playlists/{Uri.EscapeDataString(playlistId)}/tracks?limit={PageSize}";
        var pages = 0;
        while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
        {
            JsonDocument document;
            try
            {
                document = await GetJsonAsync(accessToken, next, cancellationToken).ConfigureAwait(false);
            }
            catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound
                || exception.StatusCode == HttpStatusCode.Forbidden)
            {
                return null;
            }
            using (document)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        var item = ReadPlaylistTrack(entry);
                        if (item is not null) items.Add(item);
                    }
                }
                next = root.TryGetProperty("next", out var nextElement) && nextElement.ValueKind == JsonValueKind.String
                    ? nextElement.GetString()
                    : null;
            }
        }
        return items;
    }

    /// <summary>
    /// Odcinki podcastu, od najnowszego. Spotify zwraca je już w tej kolejności.
    /// </summary>
    public async Task<IReadOnlyList<MediaItem>?> GetShowEpisodesAsync(
        string accessToken,
        string showId,
        CancellationToken cancellationToken)
    {
        string? nazwaPodcastu = null;
        try
        {
            using var header = await GetJsonAsync(
                accessToken,
                $"{ApiRoot}/shows/{Uri.EscapeDataString(showId)}",
                cancellationToken).ConfigureAwait(false);
            nazwaPodcastu = Tekst(header.RootElement, "name");
        }
        catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound
            || exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return null;
        }

        var items = new List<MediaItem>();
        var next = $"{ApiRoot}/shows/{Uri.EscapeDataString(showId)}/episodes?limit={PageSize}";
        var pages = 0;
        while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
        {
            JsonDocument document;
            try
            {
                document = await GetJsonAsync(accessToken, next, cancellationToken).ConfigureAwait(false);
            }
            catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound
                || exception.StatusCode == HttpStatusCode.Forbidden)
            {
                return items.Count > 0 ? items : null;
            }
            using (document)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        var item = ReadEpisode(entry);
                        if (item is null) continue;
                        // Lista odcinków wewnątrz podcastu nie powtarza jego
                        // nazwy przy każdym wierszu, więc dopisujemy ją z nagłówka.
                        if (string.IsNullOrWhiteSpace(item.Artist) && !string.IsNullOrWhiteSpace(nazwaPodcastu))
                            item.Artist = nazwaPodcastu;
                        items.Add(item);
                    }
                }
                next = root.TryGetProperty("next", out var nextElement) && nextElement.ValueKind == JsonValueKind.String
                    ? nextElement.GetString()
                    : null;
            }
        }
        return items;
    }

    /// <summary>
    /// Utwory z albumu. Album zwracany przez Spotify NIE powtarza wykonawcy przy
    /// kazdym utworze, wiec uzupelniamy go z naglowka albumu - inaczej lista
    /// czytalaby "nieznany wykonawca".
    /// </summary>
    public async Task<IReadOnlyList<MediaItem>?> GetAlbumTracksAsync(
        string accessToken,
        string albumId,
        CancellationToken cancellationToken)
    {
        string albumArtist;
        string? albumTitle;
        try
        {
            using var header = await GetJsonAsync(
                accessToken,
                $"{ApiRoot}/albums/{Uri.EscapeDataString(albumId)}",
                cancellationToken).ConfigureAwait(false);
            albumArtist = Wykonawcy(header.RootElement);
            albumTitle = Tekst(header.RootElement, "name");
        }
        catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound
            || exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return null;
        }

        var items = new List<MediaItem>();
        var next = $"{ApiRoot}/albums/{Uri.EscapeDataString(albumId)}/tracks?limit={PageSize}";
        var pages = 0;
        while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
        {
            JsonDocument document;
            try
            {
                document = await GetJsonAsync(accessToken, next, cancellationToken).ConfigureAwait(false);
            }
            catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound
                || exception.StatusCode == HttpStatusCode.Forbidden)
            {
                return null;
            }
            using (document)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        var item = ReadTrack(entry);
                        if (item is null) continue;
                        if (string.IsNullOrWhiteSpace(item.Artist)) item.Artist = albumArtist;
                        item.RelatedAlbumExternalId = albumId;
                        item.RelatedAlbumTitle = albumTitle;
                        items.Add(item);
                    }
                }
                next = root.TryGetProperty("next", out var nextElement)
                    && nextElement.ValueKind == JsonValueKind.String
                    ? nextElement.GetString()
                    : null;
            }
        }
        return items;
    }

    /// <summary>Albumy wykonawcy. Bez singli-duplikatow z innych rynkow.</summary>
    public async Task<IReadOnlyList<MediaItem>?> GetArtistAlbumsAsync(
        string accessToken,
        string artistId,
        CancellationToken cancellationToken)
    {
        var items = new List<MediaItem>();
        var widziane = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var next = $"{ApiRoot}/artists/{Uri.EscapeDataString(artistId)}"
            + $"/albums?include_groups=album,single&limit={PageSize}";
        var pages = 0;
        while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
        {
            JsonDocument document;
            try
            {
                document = await GetJsonAsync(accessToken, next, cancellationToken).ConfigureAwait(false);
            }
            catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound
                || exception.StatusCode == HttpStatusCode.Forbidden)
            {
                return null;
            }
            using (document)
            {
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        var item = ReadAlbum(entry);
                        if (item is null) continue;
                        // Spotify zwraca te same wydania w wielu wersjach rynkowych.
                        // Bez odsiania lista wykonawcy mialaby ten sam album kilka razy.
                        var klucz = $"{item.Title}|{item.Artist}";
                        if (!widziane.Add(klucz)) continue;
                        items.Add(item);
                    }
                }
                next = root.TryGetProperty("next", out var nextElement)
                    && nextElement.ValueKind == JsonValueKind.String
                    ? nextElement.GetString()
                    : null;
            }
        }
        return items;
    }

    private async Task CollectAsync(
        string opis,
        MediaItemKind kind,
        string firstUrl,
        Func<JsonElement, MediaItem?> reader,
        List<MediaItem> items,
        List<string> warnings,
        List<MediaItemKind> updated,
        CancellationToken cancellationToken)
    {
        var collected = new List<MediaItem>();
        var next = firstUrl;
        var pages = 0;
        try
        {
            while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
            {
                using var document = await GetJsonAsync(accessTokenOverride: null, next, cancellationToken)
                    .ConfigureAwait(false);
                var root = document.RootElement;
                if (root.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        var item = reader(entry);
                        if (item is not null) collected.Add(item);
                    }
                }
                next = root.TryGetProperty("next", out var nextElement) && nextElement.ValueKind == JsonValueKind.String
                    ? nextElement.GetString()
                    : null;
            }
            items.AddRange(collected);
            updated.Add(kind);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Częściowy wynik zachowujemy: 900 z 1000 utworów jest dla
            // użytkownika użyteczne, zero nie jest.
            items.AddRange(collected);
            warnings.Add($"{opis}: {SkrocBlad(exception)} Pobrano {collected.Count} pozycji.");
            if (collected.Count > 0) updated.Add(kind);
        }
    }

    private async Task CollectFollowedArtistsAsync(
        List<MediaItem> items,
        List<string> warnings,
        List<MediaItemKind> updated,
        CancellationToken cancellationToken)
    {
        var collected = new List<MediaItem>();
        var next = $"{ApiRoot}/me/following?type=artist&limit={PageSize}";
        var pages = 0;
        try
        {
            while (!string.IsNullOrEmpty(next) && pages++ < MaxPages)
            {
                using var document = await GetJsonAsync(null, next, cancellationToken).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("artists", out var artists)) break;
                if (artists.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in array.EnumerateArray())
                    {
                        var item = ReadArtist(entry);
                        if (item is not null) collected.Add(item);
                    }
                }
                next = artists.TryGetProperty("next", out var nextElement) && nextElement.ValueKind == JsonValueKind.String
                    ? nextElement.GetString()
                    : null;
            }
            items.AddRange(collected);
            updated.Add(MediaItemKind.Artist);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            items.AddRange(collected);
            warnings.Add($"Obserwowani wykonawcy: {SkrocBlad(exception)} Pobrano {collected.Count} pozycji.");
            if (collected.Count > 0) updated.Add(MediaItemKind.Artist);
        }
    }

    private string? currentAccessToken;

    /// <summary>Ustawia token na czas jednej synchronizacji.</summary>
    public void UseAccessToken(string accessToken) => currentAccessToken = accessToken;

    private async Task<JsonDocument> GetJsonAsync(
        string? accessTokenOverride,
        string url,
        CancellationToken cancellationToken)
    {
        var token = accessTokenOverride ?? currentAccessToken
            ?? throw new InvalidOperationException("Brak tokenu Spotify do zapytania API.");
        for (var proba = 0; proba < 4; proba++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            try
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Retry-After od Spotify jest wiążący. Własne opóźnienie
                    // grozi blokadą całej rejestracji aplikacji.
                    var czekaj = response.Headers.RetryAfter?.Delta
                        ?? (response.Headers.RetryAfter?.Date is { } date
                            ? date - DateTimeOffset.UtcNow
                            : TimeSpan.FromSeconds(2));
                    if (czekaj < TimeSpan.Zero) czekaj = TimeSpan.FromSeconds(2);
                    if (czekaj > TimeSpan.FromMinutes(2))
                    {
                        throw new SpotifyApiException(
                            $"Spotify wstrzymał zapytania na {(int)czekaj.TotalSeconds} sekund. Spróbuj później.",
                            HttpStatusCode.TooManyRequests);
                    }
                    DiagnosticLog.Info(
                        "spotify-sync",
                        $"Spotify ogranicza tempo zapytań; czekam {(int)czekaj.TotalSeconds} s.");
                    await Task.Delay(czekaj, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new SpotifyApiException(
                        OpiszBladHttp(response.StatusCode),
                        response.StatusCode);
                }
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                response.Dispose();
            }
        }
        throw new SpotifyApiException(
            "Spotify wielokrotnie ograniczył tempo zapytań. Spróbuj ponownie za chwilę.",
            HttpStatusCode.TooManyRequests);
    }

    private static string OpiszBladHttp(HttpStatusCode kod) => kod switch
    {
        HttpStatusCode.Unauthorized => "Logowanie Spotify wygasło. Zaloguj się ponownie.",
        HttpStatusCode.Forbidden =>
            "Spotify odmówił dostępu. W panelu aplikacji Spotify dopisz swoje konto do listy użytkowników.",
        HttpStatusCode.NotFound => "Spotify nie znalazł tych danych.",
        _ => $"Spotify zwrócił błąd HTTP {(int)kod}."
    };

    private static string SkrocBlad(Exception exception) => exception switch
    {
        SpotifyApiException api => api.Message,
        HttpRequestException => "Brak połączenia ze Spotify.",
        TaskCanceledException => "Spotify nie odpowiedział w wyznaczonym czasie.",
        _ => $"{exception.GetType().Name}: {exception.Message}"
    };

    /// <summary>
    /// Czy konto przyznało dane uprawnienie.
    ///
    /// PUSTA lista zakresów oznacza "NIE WIEM", a nie "nie przyznano". Spotify
    /// przy odświeżaniu tokenu często nie powtarza listy zakresów, więc zapisane
    /// pole bywa puste na w pełni uprawnionym koncie. Odmowa w takiej sytuacji
    /// dawała ostrzeżenia typu "konto nie przyznało dostępu do playlist" na
    /// koncie, które dostęp ma - i pustą bibliotekę bez prawdziwej przyczyny.
    /// Gdy nie wiemy - próbujemy; realną odmowę i tak zgłosi serwer (HTTP 403).
    /// </summary>
    private static bool SpotifyScopesHave(string? granted, string required) =>
        string.IsNullOrWhiteSpace(granted)
        || granted.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(required, StringComparer.Ordinal);

    /// <summary>
    /// Zapisany podcast. Podcast jest kontenerem - nie odtwarza się sam,
    /// wchodzi się do niego strzałką w prawo po listę odcinków.
    /// </summary>
    private static MediaItem? ReadSavedShow(JsonElement entry)
    {
        if (!entry.TryGetProperty("show", out var show) || show.ValueKind != JsonValueKind.Object) return null;
        var id = Tekst(show, "id");
        var nazwa = Tekst(show, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nazwa)) return null;

        var item = new MediaItem
        {
            Kind = MediaItemKind.Podcast,
            Title = nazwa,
            ExternalId = id,
            // Wydawca podcastu stoi tam, gdzie u utworu wykonawca - czytnik
            // odczyta wtedy wiersz tym samym wzorem co resztę listy.
            Artist = Tekst(show, "publisher") ?? string.Empty,
            PublicUri = $"https://open.spotify.com/show/{id}",
            IsInLibrary = true
        };
        if (entry.TryGetProperty("added_at", out var added)
            && added.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(added.GetString(), out var kiedy))
        {
            item.CollectionAddedUtcTicks = kiedy.UtcDateTime.Ticks;
        }
        return item;
    }

    /// <summary>
    /// Zapisany odcinek podcastu - to już się odtwarza.
    /// </summary>
    private static MediaItem? ReadSavedEpisode(JsonElement entry)
    {
        var odcinek = entry.TryGetProperty("episode", out var e) && e.ValueKind == JsonValueKind.Object
            ? e
            : entry;
        var item = ReadEpisode(odcinek);
        if (item is null) return null;
        item.IsFavorite = true;
        if (entry.TryGetProperty("added_at", out var added)
            && added.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(added.GetString(), out var kiedy))
        {
            item.CollectionAddedUtcTicks = kiedy.UtcDateTime.Ticks;
        }
        return item;
    }

    private static MediaItem? ReadEpisode(JsonElement odcinek)
    {
        var id = Tekst(odcinek, "id");
        var nazwa = Tekst(odcinek, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nazwa)) return null;

        var wydawca = odcinek.TryGetProperty("show", out var show) && show.ValueKind == JsonValueKind.Object
            ? Tekst(show, "name")
            : null;

        var item = new MediaItem
        {
            Kind = MediaItemKind.Episode,
            Title = nazwa,
            ExternalId = id,
            Artist = wydawca ?? string.Empty,
            PublicUri = $"https://open.spotify.com/episode/{id}",
            Source = $"spotify:episode:{id}"
        };
        if (odcinek.TryGetProperty("duration_ms", out var ms) && ms.TryGetInt64(out var msWartosc))
            item.Duration = TimeSpan.FromMilliseconds(msWartosc);
        // Odcinek wycofany z regionu zostaje na liście - użytkownik pamięta,
        // że go zapisał; ukrycie wyglądałoby jak utrata danych.
        if (odcinek.TryGetProperty("is_playable", out var playable) && playable.ValueKind == JsonValueKind.False)
            item.IsAvailable = false;
        return item;
    }

    private static MediaItem? ReadSavedTrack(JsonElement entry)
    {
        if (!entry.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object) return null;
        var item = ReadTrack(track);
        if (item is null) return null;
        // Polubiony utwor nalezy do Ulubionych (Ctrl+U), NIE do Biblioteki
        // (Ctrl+L). Ustawienie obu flag sprawialo, ze oba skroty pokazywaly
        // prawie te sama liste i rozroznienie tracilo sens. Biblioteka to
        // albumy, wykonawcy i playlisty - tak samo jak w TIDAL.
        item.IsFavorite = true;
        if (entry.TryGetProperty("added_at", out var added)
            && added.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(added.GetString(), out var kiedy))
        {
            item.CollectionAddedUtcTicks = kiedy.UtcDateTime.Ticks;
        }
        return item;
    }

    private static MediaItem? ReadTrack(JsonElement track)
    {
        var id = Tekst(track, "id");
        var nazwa = Tekst(track, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nazwa)) return null;
        var item = new MediaItem
        {
            Kind = MediaItemKind.Track,
            Title = nazwa,
            ExternalId = id,
            Artist = Wykonawcy(track),
            PublicUri = $"https://open.spotify.com/track/{id}",
            // Source to identyfikator dla odtwarzacza (Web Playback SDK gra
            // po URI spotify:track:...), nie link dla człowieka.
            Source = $"spotify:track:{id}"
        };
        if (track.TryGetProperty("duration_ms", out var ms) && ms.TryGetInt64(out var msWartosc))
            item.Duration = TimeSpan.FromMilliseconds(msWartosc);
        // "Niedostępny" znaczy: jest na liście, ale nie zagra w tym kraju.
        // Ukrywanie takich pozycji myli - użytkownik pamięta, że je dodał.
        if (track.TryGetProperty("is_playable", out var playable) && playable.ValueKind == JsonValueKind.False)
            item.IsAvailable = false;
        if (track.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
        {
            item.RelatedAlbumExternalId = Tekst(album, "id");
            item.RelatedAlbumTitle = Tekst(album, "name");
        }
        if (track.TryGetProperty("artists", out var artysci)
            && artysci.ValueKind == JsonValueKind.Array
            && artysci.GetArrayLength() > 0)
        {
            var pierwszy = artysci[0];
            item.RelatedArtistExternalId = Tekst(pierwszy, "id");
            item.RelatedArtistName = Tekst(pierwszy, "name");
        }
        return item;
    }

    private static MediaItem? ReadPlaylistTrack(JsonElement entry)
    {
        if (!entry.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object) return null;
        // Playlisty mogą zawierać odcinki podcastów. Traktujemy je jako odcinki,
        // nie jako utwory, żeby lista mówiła prawdę o tym, co się otwiera.
        var typ = Tekst(track, "type");
        var item = ReadTrack(track);
        if (item is null) return null;
        if (string.Equals(typ, "episode", StringComparison.OrdinalIgnoreCase))
        {
            item = new MediaItem
            {
                Kind = MediaItemKind.Episode,
                Title = item.Title,
                Artist = item.Artist,
                ExternalId = item.ExternalId,
                Duration = item.Duration,
                PublicUri = string.IsNullOrWhiteSpace(item.ExternalId)
                    ? item.PublicUri
                    : $"https://open.spotify.com/episode/{item.ExternalId}",
                // Odcinek MUSI mieć adres "spotify:episode:...". Przepisanie
                // adresu utworu ("spotify:track:...") dawało pozycję, która
                // wygląda dobrze na liście i nie daje się odtworzyć.
                Source = string.IsNullOrWhiteSpace(item.ExternalId)
                    ? null
                    : $"spotify:episode:{item.ExternalId}"
            };
        }
        // Ta sama pozycja może być w playliście wielokrotnie, więc rozróżniamy
        // wystąpienia po dacie dodania i indeksie, nie po samym identyfikatorze.
        if (entry.TryGetProperty("added_at", out var added)
            && added.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(added.GetString(), out var kiedy))
        {
            item.CollectionAddedUtcTicks = kiedy.UtcDateTime.Ticks;
        }
        return item;
    }

    private static MediaItem? ReadSavedAlbum(JsonElement entry)
    {
        if (!entry.TryGetProperty("album", out var album) || album.ValueKind != JsonValueKind.Object) return null;
        return ReadAlbum(album, entry);
    }

    /// <summary>
    /// Album jako pozycja listy. "entry" jest tu tylko dla albumow z biblioteki
    /// (nosi date dodania); przy albumach wykonawcy go nie ma.
    /// </summary>
    private static MediaItem? ReadAlbum(JsonElement album, JsonElement? entry = null)
    {
        var id = Tekst(album, "id");
        var nazwa = Tekst(album, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nazwa)) return null;
        var item = new MediaItem
        {
            Kind = MediaItemKind.Album,
            Title = nazwa,
            ExternalId = id,
            Artist = Wykonawcy(album),
            IsInLibrary = entry is not null,
            PublicUri = $"https://open.spotify.com/album/{id}",
            Source = $"spotify:album:{id}"
        };
        // Wykonawca nadrzedny albumu. ReadTrack wypelnia te pola od dawna, a
        // ReadAlbum ich NIE ustawial - wiec album Spotify wracal z API bez
        // wykonawcy i "Przejdz do wykonawcy" bylo martwe (CanOpenRelatedArtist
        // czyta RelatedArtistExternalId, nie pole Artist, ktore jest tylko
        // tekstem do czytnika). Bez tego zadna poprawka nawigacji nie pomoze.
        if (album.TryGetProperty("artists", out var artysci)
            && artysci.ValueKind == JsonValueKind.Array
            && artysci.GetArrayLength() > 0)
        {
            var pierwszy = artysci[0];
            item.RelatedArtistExternalId = Tekst(pierwszy, "id");
            item.RelatedArtistName = Tekst(pierwszy, "name");
        }
        if (entry is { } wpis
            && wpis.TryGetProperty("added_at", out var added)
            && added.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(added.GetString(), out var kiedy))
        {
            item.CollectionAddedUtcTicks = kiedy.UtcDateTime.Ticks;
        }
        return item;
    }

    private static MediaItem? ReadArtist(JsonElement artist)
    {
        var id = Tekst(artist, "id");
        var nazwa = Tekst(artist, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nazwa)) return null;
        return new MediaItem
        {
            Kind = MediaItemKind.Artist,
            Title = nazwa,
            ExternalId = id,
            IsInLibrary = true,
            PublicUri = $"https://open.spotify.com/artist/{id}",
            Source = $"spotify:artist:{id}"
        };
    }

    private static MediaItem? ReadPlaylist(JsonElement playlist)
    {
        var id = Tekst(playlist, "id");
        var nazwa = Tekst(playlist, "name");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nazwa)) return null;
        var wlasciciel = playlist.TryGetProperty("owner", out var owner) && owner.ValueKind == JsonValueKind.Object
            ? Tekst(owner, "display_name") ?? Tekst(owner, "id")
            : null;
        var item = new MediaItem
        {
            Kind = MediaItemKind.Playlist,
            Title = nazwa,
            ExternalId = id,
            // Właściciel jako "wykonawca": dla czytnika ekranu to informacja,
            // czyja jest playlista, co decyduje o tym, czy da się ją otworzyć.
            Artist = string.IsNullOrWhiteSpace(wlasciciel) ? string.Empty : wlasciciel,
            IsInLibrary = true,
            PublicUri = $"https://open.spotify.com/playlist/{id}",
            Source = $"spotify:playlist:{id}"
        };
        return item;
    }

    private static string Wykonawcy(JsonElement element)
    {
        if (!element.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var nazwy = new List<string>();
        foreach (var artist in artists.EnumerateArray())
        {
            var nazwa = Tekst(artist, "name");
            if (!string.IsNullOrWhiteSpace(nazwa)) nazwy.Add(nazwa);
        }
        return string.Join(", ", nazwy);
    }

    private static string? Tekst(JsonElement element, string nazwa) =>
        element.TryGetProperty(nazwa, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }
}
