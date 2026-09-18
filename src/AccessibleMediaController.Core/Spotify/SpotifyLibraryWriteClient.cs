using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>Dlaczego zapisu nie da się nawet spróbować.</summary>
public enum SpotifyWriteBlockReason
{
    None = 0,
    /// <summary>Konto nie przyznało zakresu zapisu. Potrzebne ponowne logowanie.</summary>
    MissingScope,
    /// <summary>Spotify odmówił (403): konto poza listą użytkowników aplikacji albo odebrana zgoda.</summary>
    Denied,
    /// <summary>Token nieważny (401).</summary>
    Unauthorized,
    /// <summary>Rodzaj pozycji, którego AMC nie zapisuje (np. playlista).</summary>
    UnsupportedKind
}

public sealed class SpotifyWriteBlockedException(string message, SpotifyWriteBlockReason reason)
    : Exception(message)
{
    public SpotifyWriteBlockReason Reason { get; } = reason;
}

/// <summary>
/// Wynik zapisu. <see cref="ConfirmedUris"/> to pozycje, których nowy stan
/// Spotify POTWIERDZIŁ odczytem po zapisie - tylko dla nich wolno zmienić flagi
/// lokalne. <see cref="FailedUris"/> zostają z poprzednim stanem, żeby częściowa
/// awaria nie kasowała danych ani nie kłamała o sukcesie.
/// </summary>
public sealed record SpotifyMembershipChangeResult(
    bool Added,
    IReadOnlyList<string> ConfirmedUris,
    IReadOnlyList<string> FailedUris,
    IReadOnlyList<string> Warnings)
{
    public bool AnythingConfirmed => ConfirmedUris.Count > 0;
}

/// <summary>
/// Zapis do biblioteki konta Spotify: Ulubione, Biblioteka i usuwanie.
/// Osobna klasa od odczytowego klienta biblioteki, bo ma inne uprawnienia,
/// inne granice wielkości partii i musi WERYFIKOWAĆ skutek, nie tylko wysłać
/// żądanie.
///
/// Punkt końcowy (sprawdzone w dokumentacji Spotify, wrzesień 2026):
///   PUT    /v1/me/library?uris=...           (zapis)
///   DELETE /v1/me/library?uris=...           (usunięcie)
///   GET    /v1/me/library/contains?uris=...  (stan)
/// Maksimum 40 adresów URI na żądanie. Starsze punkty per rodzaj
/// (/me/tracks, /me/albums, /me/following) są oznaczone jako WYCOFANE i
/// dokumentacja wskazuje na /me/library - nie wracaj do nich.
///
/// ZASADA, której nie wolno tu złamać: AMC NIE ogłasza sukcesu, dopóki Spotify
/// nie potwierdzi stanu. Lokalna flaga bez potwierdzenia wyglądała dla
/// użytkownika czytnika ekranu jak działający zapis, a po ponownym pobraniu
/// biblioteki pozycja wracała do starego stanu.
///
/// Ta klasa NIE prosi o logowanie i NIE otwiera przeglądarki. Przy braku
/// zakresu rzuca <see cref="SpotifyWriteBlockedException"/> z komunikatem
/// kierującym do okna konta (Ctrl+F5).
/// </summary>
public sealed class SpotifyLibraryWriteClient(HttpClient? httpClient = null) : IDisposable
{
    private const string ApiRoot = "https://api.spotify.com/v1";
    /// <summary>Limit narzucony przez Spotify na /me/library i /me/library/contains.</summary>
    public const int MaxUrisPerRequest = 40;

    private readonly HttpClient http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly bool ownsHttpClient = httpClient is null;

    /// <summary>
    /// Stan przynależności WEDŁUG API, nie według lokalnych flag. To jest
    /// źródło prawdy dla odwracania stanu jednym skrótem.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, bool>> ReadMembershipAsync(
        string accessToken,
        IReadOnlyList<string> uris,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uris);
        var wynik = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var batch in Batches(uris))
        {
            using var response = await SendAsync(
                HttpMethod.Get,
                $"{ApiRoot}/me/library/contains?uris={EncodeUris(batch)}",
                accessToken,
                cancellationToken).ConfigureAwait(false);
            EnsureReadable(response);
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument
                    .ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new SpotifyWriteBlockedException(
                        "Spotify nie zwrócił stanu biblioteki w oczekiwanej postaci.",
                        SpotifyWriteBlockReason.Denied);
                var flagi = document.RootElement.EnumerateArray().ToArray();
                // Spotify zwraca tablicę W TEJ SAMEJ kolejności co adresy.
                // Krótsza odpowiedź niż pytanie znaczy, że czegoś nie znamy -
                // brak wpisu, nie domyślne "nie należy".
                for (var i = 0; i < batch.Count && i < flagi.Length; i++)
                {
                    if (flagi[i].ValueKind is JsonValueKind.True or JsonValueKind.False)
                        wynik[batch[i]] = flagi[i].GetBoolean();
                }
            }
        }
        return wynik;
    }

    /// <summary>
    /// Zmienia przynależność i POTWIERDZA ją ponownym odczytem. Rzuca tylko
    /// wtedy, gdy nie udało się nic - częściowa awaria wraca w wyniku, żeby
    /// potwierdzone pozycje nie przepadły razem z nieudanymi.
    /// </summary>
    public async Task<SpotifyMembershipChangeResult> ChangeMembershipAsync(
        string accessToken,
        string? grantedScope,
        IReadOnlyList<string> uris,
        bool? requestedAddition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uris);
        var unikalne = uris
            .Where(uri => !string.IsNullOrWhiteSpace(uri))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unikalne.Length == 0)
            throw new SpotifyWriteBlockedException(
                "Brak pozycji Spotify z identyfikatorem, które można zapisać.",
                SpotifyWriteBlockReason.UnsupportedKind);

        // Zakresy sprawdzamy PRZED jakimkolwiek żądaniem i PRZED zmianą
        // czegokolwiek lokalnie. Bez tego użytkownik słyszał "dodano", a
        // Spotify odrzucał żądanie.
        var bezZakresu = unikalne
            .Where(uri => !SpotifyScopes.AllowsWriteForUriType(grantedScope, UriType(uri)))
            .ToArray();
        if (bezZakresu.Length == unikalne.Length)
        {
            var brakujace = unikalne
                .Select(uri => SpotifyScopes.WriteScopeForUriType(UriType(uri)))
                .Where(scope => scope is not null)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            throw new SpotifyWriteBlockedException(
                brakujace.Length == 0
                    ? "Spotify nie pozwala AMC zapisywać tego rodzaju pozycji."
                    : "Zapis do biblioteki Spotify wymaga ponownego zalogowania po aktualizacji AMC. "
                        + "Otwórz Ctrl+F5 i wybierz Zaloguj w przeglądarce.",
                brakujace.Length == 0
                    ? SpotifyWriteBlockReason.UnsupportedKind
                    : SpotifyWriteBlockReason.MissingScope);
        }

        var ostrzezenia = new List<string>();
        if (bezZakresu.Length > 0)
        {
            ostrzezenia.Add(
                $"Pominięto {bezZakresu.Length} pozycji, na które konto nie dało uprawnień. Zaloguj się ponownie przez Ctrl+F5.");
        }
        var doZapisu = unikalne.Except(bezZakresu, StringComparer.Ordinal).ToArray();

        // Jawny stan docelowy liczony ze stanu POTWIERDZONEGO przez API.
        var przed = await ReadMembershipAsync(accessToken, doZapisu, cancellationToken).ConfigureAwait(false);
        var add = SpotifyCollectionSemantics.ResolveAddition(doZapisu, przed, requestedAddition);

        var metoda = add ? HttpMethod.Put : HttpMethod.Delete;
        var wyslane = new List<string>();
        var nieudane = new List<string>();
        SpotifyWriteBlockedException? pierwszyBlad = null;
        foreach (var batch in Batches(doZapisu))
        {
            try
            {
                using var response = await SendAsync(
                    metoda,
                    $"{ApiRoot}/me/library?uris={EncodeUris(batch)}",
                    accessToken,
                    cancellationToken).ConfigureAwait(false);
                EnsureWritable(response);
                wyslane.AddRange(batch);
            }
            catch (SpotifyWriteBlockedException wyjatek)
            {
                // Częściowa awaria: partie, które przeszły, ZOSTAJĄ. Wycofanie
                // byłoby drugą operacją, która też może paść, a użytkownik
                // straciłby dane bez śladu. Przyczynę zapamiętujemy, żeby po
                // całkowitej porażce podać ją zamiast ogólnika.
                nieudane.AddRange(batch);
                pierwszyBlad ??= wyjatek;
                ostrzezenia.Add($"Spotify odrzucił część pozycji: {wyjatek.Message}");
            }
        }
        if (wyslane.Count == 0)
        {
            throw pierwszyBlad ?? new SpotifyWriteBlockedException(
                "Spotify nie przyjął żadnej ze wskazanych pozycji.",
                SpotifyWriteBlockReason.Denied);
        }

        // Odczyt po zapisie. Dopiero to potwierdza, że zmiana jest w koncie.
        var po = await ReadMembershipAsync(accessToken, wyslane, cancellationToken).ConfigureAwait(false);
        var potwierdzone = wyslane
            .Where(uri => po.TryGetValue(uri, out var member) && member == add)
            .ToArray();
        var niepotwierdzone = wyslane.Except(potwierdzone, StringComparer.Ordinal).ToArray();
        if (niepotwierdzone.Length > 0)
        {
            nieudane.AddRange(niepotwierdzone);
            ostrzezenia.Add(
                $"Spotify nie potwierdził zmiany dla {niepotwierdzone.Length} pozycji. Odśwież bibliotekę, żeby zobaczyć stan konta.");
        }
        if (potwierdzone.Length == 0)
        {
            throw new SpotifyWriteBlockedException(
                "Spotify przyjął żądanie, ale nie potwierdził zmiany w bibliotece. Nie zmieniono stanu w AMC.",
                SpotifyWriteBlockReason.Denied);
        }

        return new SpotifyMembershipChangeResult(
            add,
            potwierdzone,
            nieudane.Distinct(StringComparer.Ordinal).ToArray(),
            ostrzezenia);
    }

    internal static string? UriType(string uri)
    {
        var czesci = uri.Split(':');
        return czesci.Length >= 3 && string.Equals(czesci[0], "spotify", StringComparison.Ordinal)
            ? czesci[1]
            : null;
    }

    private static IEnumerable<IReadOnlyList<string>> Batches(IReadOnlyList<string> uris) =>
        uris.Chunk(MaxUrisPerRequest);

    private static string EncodeUris(IReadOnlyList<string> uris) =>
        string.Join(',', uris.Select(Uri.EscapeDataString));

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new SpotifyWriteBlockedException(
                "Nie ma zapisanego logowania Spotify. Otwórz Ctrl+F5 i zaloguj się.",
                SpotifyWriteBlockReason.Unauthorized);
        for (var proba = 0; proba < 4; proba++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
            // Retry-After od Spotify jest wiążący; własne opóźnienie grozi
            // blokadą całej rejestracji aplikacji.
            var czekaj = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } date
                    ? date - DateTimeOffset.UtcNow
                    : TimeSpan.FromSeconds(2));
            response.Dispose();
            if (czekaj < TimeSpan.Zero) czekaj = TimeSpan.FromSeconds(2);
            if (czekaj > TimeSpan.FromMinutes(2))
            {
                throw new SpotifyWriteBlockedException(
                    $"Spotify wstrzymał zapytania na {(int)czekaj.TotalSeconds} sekund. Spróbuj później.",
                    SpotifyWriteBlockReason.Denied);
            }
            await Task.Delay(czekaj, cancellationToken).ConfigureAwait(false);
        }
        throw new SpotifyWriteBlockedException(
            "Spotify wielokrotnie ograniczył tempo zapytań. Spróbuj ponownie za chwilę.",
            SpotifyWriteBlockReason.Denied);
    }

    private static void EnsureReadable(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw Blocked(response, czytanie: true);
    }

    private static void EnsureWritable(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw Blocked(response, czytanie: false);
    }

    private static SpotifyWriteBlockedException Blocked(HttpResponseMessage response, bool czytanie) =>
        response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new SpotifyWriteBlockedException(
                "Logowanie Spotify wygasło. Otwórz Ctrl+F5 i zaloguj się ponownie.",
                SpotifyWriteBlockReason.Unauthorized),
            HttpStatusCode.Forbidden => new SpotifyWriteBlockedException(
                czytanie
                    ? "Spotify odmówił odczytu stanu biblioteki. Otwórz Ctrl+F5 i zaloguj się ponownie, "
                        + "żeby przyznać uprawnienie do zapisu."
                    : "Spotify odmówił zmiany biblioteki konta. Otwórz Ctrl+F5 i zaloguj się ponownie; "
                        + "jeśli to nie pomoże, dopisz konto do listy użytkowników aplikacji w panelu Spotify.",
                SpotifyWriteBlockReason.Denied),
            _ => new SpotifyWriteBlockedException(
                $"Spotify nie wykonał operacji na bibliotece: błąd HTTP {(int)response.StatusCode}.",
                SpotifyWriteBlockReason.Denied)
        };

    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }
}
