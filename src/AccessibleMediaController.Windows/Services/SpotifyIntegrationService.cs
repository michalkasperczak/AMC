using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
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
internal sealed class SpotifyIntegrationService(
    SpotifySettings settings,
    HttpClient? httpClient = null) : IDisposable
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);
    private readonly SpotifyOAuthClient oauth = new();
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
            return new SpotifyPlaybackCredentials(
                tokens.AccessToken,
                tokens.ExpiresAtUtc,
                SpotifyScopes.AllowsPlayback(tokens.Scope));
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
        settings.GrantedScope = refreshed.Scope;
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
        operationGate.Dispose();
        if (ownsHttpClient) http.Dispose();
    }
}
