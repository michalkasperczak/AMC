using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows.Services;

/// <summary>Kim jest zalogowany użytkownik i czy jego konto może grać w AMC.</summary>
internal sealed record SpotifyAccountProfile(
    string DisplayName,
    string UserId,
    string Product,
    string Country)
{
    /// <summary>
    /// Web Playback SDK wymaga Premium. Rodzaj konta czytamy raz przy
    /// logowaniu, żeby powiedzieć to od razu, a nie po nieudanej próbie
    /// odtworzenia utworu.
    /// </summary>
    public bool IsPremium => Product.Equals("premium", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Poświadczenia dla wbudowanego odtwarzacza (Web Playback SDK). Token żyje
/// godzinę, więc odtwarzacz musi umieć poprosić o nowy w trakcie grania.
/// </summary>
/// <summary>Wynik pobrania biblioteki: co przyszlo i o czym trzeba powiedziec wprost.</summary>
internal sealed record SpotifySynchronizationResult(
    IReadOnlyList<MediaItem> Items,
    string AccountDisplayName,
    IReadOnlyList<string> Warnings,
    bool IsComplete);

/// <summary>Wynik zmiany przynależności: co Spotify POTWIERDZIŁ, a co nie.</summary>
internal sealed record SpotifyMembershipOutcome(
    bool Added,
    IReadOnlyList<MediaItem> ConfirmedItems,
    IReadOnlyList<MediaItem> FailedItems,
    IReadOnlyList<string> Warnings);

internal sealed record SpotifyPlaybackCredentials(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    bool AllowsPlayback);

/// <summary>
/// Spina logowanie, odświeżanie tokenu i odczyt profilu konta Spotify.
///
/// Zasady, których trzeba się tu trzymać:
/// - Token odświeżamy z zapasem, nie w chwili wygaśnięcia: odtwarzanie
///   przerwane w środku utworu przez wygasły token brzmi dla użytkownika jak
///   awaria programu.
/// - Spotify przy odświeżeniu zwraca NOWY refresh_token; zapisujemy go od
///   razu, inaczej następne odświeżenie odrzuci nieaktualny.
/// - Każda operacja przechodzi przez jedną bramkę, żeby dwa równoległe
///   odświeżenia nie unieważniły sobie wzajemnie tokenów.
/// </summary>
/// <param name="tokenProvider">
/// Wąskie wejście dla testów: skąd wziąć poświadczenie. Produkcja zostawia null
/// i czyta Menedżer poświadczeń Windows — test NIE może tam zaglądać ani nic tam
/// zapisywać, a bez tego szwu nie da się zmierzyć prawdziwego pisarza HTTP.
/// </param>
internal sealed class SpotifyIntegrationService(
    SpotifySettings settings,
    HttpClient? httpClient = null,
    Func<CancellationToken, Task<SpotifyTokenSet>>? tokenProvider = null) : IDisposable
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Rynek konta. Spotify wymaga go przy odczycie katalogu - bez niego uzna
    /// tresc za niedostepna i lista przyjdzie pusta.
    /// </summary>
    internal string CountryCode =>
        string.IsNullOrWhiteSpace(settings.CountryCode) ? "PL" : settings.CountryCode;

    private readonly SpotifyOAuthClient oauth = new();
    private readonly SpotifyApiClient api = new();
    // Pisarz dostaje TEN SAM transport co reszta serwisu: inaczej test z atrapą
    // HTTP mierzyłby wszystko poza jedynym miejscem, które faktycznie zapisuje.
    private readonly SpotifyLibraryWriteClient writer = new(httpClient);
    private readonly HttpClient http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly bool ownsHttpClient = httpClient is null;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private bool? storedLoginProbe;
    private string? storedLoginProbeFailure;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.ClientId);

    public bool HasStoredLogin
    {
        get
        {
            // Zapamiętany wynik: odczyt trafia tu z każdego odświeżenia menu
            // i palety poleceń, a przyczyna błędu nie zmienia się w obrębie
            // jednego uruchomienia programu.
            if (storedLoginProbe is { } cached) return cached;
            try
            {
                var found = SpotifyCredentialStore.TryRead(out _);
                storedLoginProbe = found;
                storedLoginProbeFailure = null;
                return found;
            }
            catch (Exception exception)
            {
                var description = $"{exception.GetType().Name}: {exception.Message}";
                if (storedLoginProbeFailure != description)
                {
                    storedLoginProbeFailure = description;
                    DiagnosticLog.Warning(
                        "spotify-auth",
                        $"Nie można odczytać bezpiecznie zapisanego logowania Spotify: {description}");
                }
                return false;
            }
        }
    }

    public void InvalidateStoredLoginProbe()
    {
        storedLoginProbe = null;
        storedLoginProbeFailure = null;
    }

    public async Task<SpotifyAccountProfile> LoginAsync(CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await oauth.AuthorizeAsync(settings, cancellationToken).ConfigureAwait(false);
            SpotifyCredentialStore.Write(tokens);
            InvalidateStoredLoginProbe();
            settings.GrantedScope = tokens.Scope;
            DiagnosticLog.Info(
                "spotify-auth",
                "Zalogowano konto Spotify; token zapisano w Menedżerze poświadczeń Windows.");
            var profile = await ReadProfileAsync(tokens, cancellationToken).ConfigureAwait(false);
            settings.AccountDisplayName = profile.DisplayName;
            settings.AccountProduct = profile.Product;
            if (!string.IsNullOrWhiteSpace(profile.Country)) settings.CountryCode = profile.Country;
            return profile;
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>
    /// Token dla wbudowanego odtwarzacza. Wołane także w trakcie grania, gdy
    /// SDK sam poprosi o odnowienie.
    /// </summary>
    public async Task<SpotifyPlaybackCredentials> GetPlaybackCredentialsAsync(
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            // Zakres bierzemy z tokenu, a gdy Spotify go nie powtorzylo - z tego,
            // co zapamietano przy logowaniu. Gdy NIE WIEMY nic, pozwalamy probowac:
            // blokada "na wszelki wypadek" zabralaby odtwarzanie koncie, ktore gra.
            var scope = !string.IsNullOrWhiteSpace(tokens.Scope)
                ? tokens.Scope
                : settings.GrantedScope;
            return new SpotifyPlaybackCredentials(
                tokens.AccessToken,
                tokens.ExpiresAtUtc,
                string.IsNullOrWhiteSpace(scope) || SpotifyScopes.AllowsPlayback(scope));
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task<SpotifyAccountProfile> GetProfileAsync(CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            var profile = await ReadProfileAsync(tokens, cancellationToken).ConfigureAwait(false);
            settings.AccountDisplayName = profile.DisplayName;
            settings.AccountProduct = profile.Product;
            return profile;
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>
    /// Pobiera bibliotekę konta. Rzuca wyjątkiem tylko wtedy, gdy NIE UDAŁO SIĘ
    /// NIC - częściowy wynik wraca normalnie, z ostrzeżeniami. Zwrócenie pustej
    /// listy jako sukcesu wyglądałoby dla użytkownika jak puste konto.
    /// </summary>
    public async Task<SpotifySynchronizationResult> SynchronizeAsync(CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            var profile = await ReadProfileAsync(tokens, cancellationToken).ConfigureAwait(false);
            settings.AccountDisplayName = profile.DisplayName;
            settings.AccountProduct = profile.Product;

            api.UseAccessToken(tokens.AccessToken);
            var collection = await api.SynchronizeCollectionAsync(
                tokens.AccessToken,
                tokens.Scope,
                cancellationToken).ConfigureAwait(false);

            if (collection.UpdatedKinds.Count == 0)
            {
                var szczegoly = collection.Warnings.Count == 0
                    ? string.Empty
                    : " " + string.Join(" ", collection.Warnings);
                throw new InvalidOperationException(
                    "Nie udało się pobrać żadnej części biblioteki Spotify." + szczegoly);
            }

            settings.LastSuccessfulSyncUtcTicks = DateTime.UtcNow.Ticks;
            DiagnosticLog.Info(
                "spotify-sync",
                $"Pobrano {collection.Items.Count} pozycji biblioteki Spotify; ostrzeżenia: {collection.Warnings.Count}.");
            return new SpotifySynchronizationResult(
                collection.Items,
                profile.DisplayName,
                collection.Warnings,
                // Pełne, gdy wszystkie cztery kolekcje przeszły: utwory,
                // albumy, wykonawcy, playlisty.
                collection.UpdatedKinds.Count == 4);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>
    /// Utwory z playlisty. Zwraca null, gdy Spotify nie udostępnia zawartości -
    /// tak jest dla playlist redakcyjnych Spotify i playlist innych osób.
    /// To nie awaria, tylko granica narzucona przez Spotify w 2024 roku.
    /// </summary>
    public async Task<IReadOnlyList<MediaItem>?> GetPlaylistTracksAsync(
        string playlistExternalId,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            return await api.GetPlaylistTracksAsync(
                tokens.AccessToken,
                playlistExternalId,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>
    /// Zmienia Ulubione / Bibliotekę konta Spotify NA PRAWDZIWYM API i zwraca
    /// tylko to, co Spotify potwierdził odczytem po zapisie. Wołający ustawia
    /// flagi lokalne WYŁĄCZNIE dla <see cref="SpotifyMembershipOutcome.ConfirmedItems"/>.
    ///
    /// Przechodzi przez tę samą bramkę co synchronizacja, żeby odświeżenie
    /// tokenu i dwa naciśnięcia skrótu pod rząd nie nadpisały się wzajemnie.
    ///
    /// NIE prosi o logowanie i NIE otwiera przeglądarki. Przy braku zakresu
    /// zapisu rzuca <see cref="SpotifyWriteBlockedException"/> z komunikatem
    /// kierującym do okna konta (Ctrl+F5).
    /// </summary>
    public async Task<SpotifyMembershipOutcome> ChangeCollectionMembershipAsync(
        IReadOnlyList<MediaItem> items,
        bool? requestedAddition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        // Mapa adres URI -> pozycje AMC. Dwie różne pozycje listy mogą wskazywać
        // ten sam zasób Spotify (wynik wyszukiwania i pozycja biblioteki) i obie
        // muszą dostać potwierdzony stan.
        var byUri = new Dictionary<string, List<MediaItem>>(StringComparer.Ordinal);
        var bezAdresu = new List<MediaItem>();
        foreach (var item in items)
        {
            if (SpotifyCollectionSemantics.TryBuildUri(item, out var uri))
            {
                if (!byUri.TryGetValue(uri, out var lista)) byUri[uri] = lista = [];
                lista.Add(item);
            }
            else
            {
                bezAdresu.Add(item);
            }
        }
        if (byUri.Count == 0)
        {
            throw new SpotifyWriteBlockedException(
                "Żadna z wybranych pozycji nie ma identyfikatora Spotify, który da się zapisać w koncie.",
                SpotifyWriteBlockReason.UnsupportedKind);
        }

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            // Zakres bierzemy z tokenu, a przy pustym polu z ustawień: Spotify
            // przy odświeżeniu często nie powtarza listy zakresów, a puste pole
            // nie znaczy "odebrano uprawnienia".
            var scope = string.IsNullOrWhiteSpace(tokens.Scope) ? settings.GrantedScope : tokens.Scope;
            var result = await writer.ChangeMembershipAsync(
                tokens.AccessToken,
                scope,
                byUri.Keys.ToArray(),
                requestedAddition,
                cancellationToken).ConfigureAwait(false);

            var potwierdzone = result.ConfirmedUris
                .SelectMany(uri => byUri.GetValueOrDefault(uri, []))
                .ToArray();
            var nieudane = result.FailedUris
                .SelectMany(uri => byUri.GetValueOrDefault(uri, []))
                .Concat(bezAdresu)
                .ToArray();
            var ostrzezenia = result.Warnings.ToList();
            if (bezAdresu.Count > 0)
            {
                ostrzezenia.Add(
                    $"Pominięto {bezAdresu.Count} pozycji bez identyfikatora Spotify.");
            }
            DiagnosticLog.Info(
                "spotify-collection",
                $"Spotify potwierdził {(result.Added ? "dodanie" : "usunięcie")} "
                    + $"{potwierdzone.Length} pozycji; bez potwierdzenia {nieudane.Length}.");
            return new SpotifyMembershipOutcome(result.Added, potwierdzone, nieudane, ostrzezenia);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <summary>
    /// Wyszukiwanie w katalogu Spotify dla Ctrl+F. Osobne wejscie obok
    /// synchronizacji: wyszukiwanie NIE dotyka biblioteki ani cache konta.
    /// </summary>
    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tokens = await EnsureValidTokensAsync(cancellationToken).ConfigureAwait(false);
            return await api.SearchAsync(
                tokens.AccessToken,
                settings.CountryCode,
                query,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public void Disconnect()
    {
        SpotifyCredentialStore.Delete();
        InvalidateStoredLoginProbe();
        settings.AccountDisplayName = string.Empty;
        settings.AccountProduct = string.Empty;
        settings.GrantedScope = string.Empty;
        settings.LastSuccessfulSyncUtcTicks = 0;
        DiagnosticLog.Info("spotify-auth", "Odłączono konto Spotify; usunięto token z Menedżera poświadczeń.");
    }

    private async Task<SpotifyTokenSet> EnsureValidTokensAsync(CancellationToken cancellationToken)
    {
        // Szew testowy: gdy podano źródło poświadczenia, nie dotykamy Menedżera
        // poświadczeń Windows w ogóle.
        if (tokenProvider is not null)
            return await tokenProvider(cancellationToken).ConfigureAwait(false);
        if (!SpotifyCredentialStore.TryRead(out var stored) || stored is null)
            throw new InvalidOperationException("Nie ma zapisanego logowania Spotify. Zaloguj się w oknie konta Spotify.");
        // Identyfikator aplikacji zmieniony w ustawieniach unieważnia token:
        // Spotify wiąże token z konkretną rejestracją.
        if (!string.Equals(stored.ClientId, settings.ClientId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Identyfikator aplikacji Spotify zmienił się po zalogowaniu. Zaloguj się ponownie.");
        }
        if (stored.ExpiresAtUtc - RefreshMargin > DateTimeOffset.UtcNow) return stored;

        DiagnosticLog.Info("spotify-auth", "Odświeżanie tokenu Spotify.");
        var refreshed = await oauth.RefreshAsync(settings, stored, cancellationToken).ConfigureAwait(false);
        SpotifyCredentialStore.Write(refreshed);
        // Spotify przy odswiezeniu czesto NIE powtarza listy zakresow. Puste
        // pole nie znaczy "odebrano uprawnienia" - nadpisanie go pustka kasowalo
        // wiedze o zakresie "streaming" i odtwarzanie zglaszalo nieprawdziwe
        // "konto bez Premium".
        if (!string.IsNullOrWhiteSpace(refreshed.Scope)) settings.GrantedScope = refreshed.Scope;
        return refreshed;
    }

    private async Task<SpotifyAccountProfile> ReadProfileAsync(
        SpotifyTokenSet tokens,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            // Najczęstsza przyczyna: konto nie jest dopisane do listy
            // użytkowników aplikacji w trybie rozwojowym. Bez tego zdania
            // użytkownik widzi tylko "403".
            throw new InvalidOperationException(
                "Spotify odmówił dostępu do danych konta. W panelu aplikacji Spotify dopisz swoje konto "
                    + "do listy użytkowników (User Management).");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Spotify nie zwrócił danych konta: błąd HTTP {(int)response.StatusCode}.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var displayName = root.TryGetProperty("display_name", out var name) ? name.GetString() : null;
        var userId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
        var product = root.TryGetProperty("product", out var productElement) ? productElement.GetString() : null;
        var country = root.TryGetProperty("country", out var countryElement) ? countryElement.GetString() : null;
        var profile = new SpotifyAccountProfile(
            string.IsNullOrWhiteSpace(displayName) ? userId ?? string.Empty : displayName,
            userId ?? string.Empty,
            product ?? string.Empty,
            country ?? string.Empty);
        DiagnosticLog.Info(
            "spotify-auth",
            $"Rodzaj konta Spotify: {(string.IsNullOrWhiteSpace(profile.Product) ? "nieznany" : profile.Product)}."
                + (profile.IsPremium
                    ? " Wbudowany odtwarzacz jest dopuszczalny."
                    : " Wbudowany odtwarzacz wymaga Premium i na tym koncie nie zagra."));
        return profile;
    }

    public void Dispose()
    {
        oauth.Dispose();
        api.Dispose();
        writer.Dispose();
        operationGate.Dispose();
        if (ownsHttpClient) http.Dispose();
    }
}