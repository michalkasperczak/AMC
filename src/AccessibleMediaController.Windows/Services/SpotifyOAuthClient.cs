using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Logowanie do Spotify zwykłym kontem użytkownika: Authorization Code + PKCE
/// na oficjalnej stronie accounts.spotify.com, otwartej w domyślnej
/// przeglądarce. Hasło nigdy nie przechodzi przez AMC.
///
/// Wzorowane na TidalOAuthClient, bo ten schemat jest już sprawdzony w AMC.
/// Różnice wobec TIDAL, o których trzeba wiedzieć:
/// - Spotify NIE przyjmuje "localhost" w adresie powrotu, tylko 127.0.0.1.
/// - Odświeżenie tokenu wymaga client_id w treści żądania (klient publiczny
///   bez sekretu) i zwraca NOWY refresh_token, który trzeba nadpisać.
/// - Token dostępu żyje godzinę i nie da się tego wydłużyć.
/// </summary>
internal sealed class SpotifyOAuthClient(HttpClient? httpClient = null) : IDisposable
{
    private static readonly Uri AuthorizationEndpoint = new("https://accounts.spotify.com/authorize");
    private static readonly Uri TokenEndpoint = new("https://accounts.spotify.com/api/token");
    private readonly HttpClient http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly bool ownsHttpClient = httpClient is null;

    public async Task<SpotifyTokenSet> AuthorizeAsync(
        SpotifySettings settings,
        CancellationToken cancellationToken)
    {
        ValidateSettings(settings);
        var redirectUri = new Uri(settings.RedirectUri);
        DiagnosticLog.Info("spotify-auth", "Rozpoczęto logowanie OAuth Spotify.");
        var pkce = SpotifyPkce.Create();
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        using var listener = CreateListener(redirectUri);
        listener.Start();
        DiagnosticLog.Info("spotify-auth", $"Lokalny odbiornik odpowiedzi działa na porcie {redirectUri.Port}.");
        var authorizationUri = BuildAuthorizationUri(settings, pkce.Challenge, state);
        Process.Start(new ProcessStartInfo(authorizationUri.AbsoluteUri) { UseShellExecute = true });
        DiagnosticLog.Info("spotify-auth", "Otwarto oficjalną stronę logowania Spotify w przeglądarce.");

        var callback = await ReceiveCallbackAsync(
            listener,
            redirectUri,
            state,
            cancellationToken).ConfigureAwait(false);
        DiagnosticLog.Info(
            "spotify-auth",
            string.IsNullOrWhiteSpace(callback.Error)
                ? "Odebrano odpowiedź z logowania Spotify."
                : "Odebrano odpowiedź o nieukończonym logowaniu Spotify.");
        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new InvalidOperationException(DescribeAuthorizationError(callback.Error));
        if (string.IsNullOrWhiteSpace(callback.Code))
            throw new InvalidOperationException("Spotify nie przekazał kodu logowania.");

        return await ExchangeCodeAsync(
            settings,
            callback.Code,
            pkce.Verifier,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpotifyTokenSet> RefreshAsync(
        SpotifySettings settings,
        SpotifyTokenSet current,
        CancellationToken cancellationToken)
    {
        ValidateSettings(settings);
        using var response = await http.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = current.RefreshToken,
                // Klient publiczny (PKCE) musi podać client_id także tutaj -
                // inaczej Spotify odpowiada "invalid_client".
                ["client_id"] = settings.ClientId
            }),
            cancellationToken).ConfigureAwait(false);
        return await ReadTokensAsync(
            response,
            current.RefreshToken,
            current.Scope,
            settings.ClientId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SpotifyTokenSet> ExchangeCodeAsync(
        SpotifySettings settings,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        DiagnosticLog.Info("spotify-auth", "Wymiana kodu logowania na token Spotify.");
        using var response = await http.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = settings.RedirectUri,
                ["client_id"] = settings.ClientId,
                ["code_verifier"] = verifier
            }),
            cancellationToken).ConfigureAwait(false);
        var tokens = await ReadTokensAsync(
            response,
            null,
            null,
            settings.ClientId,
            cancellationToken).ConfigureAwait(false);
        DiagnosticLog.Info("spotify-auth", "Spotify zaakceptował kod logowania.");
        return tokens;
    }

    private static async Task<SpotifyTokenSet> ReadTokensAsync(
        HttpResponseMessage response,
        string? existingRefreshToken,
        string? existingScope,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var oauthError = await ReadOAuthErrorAsync(response, cancellationToken).ConfigureAwait(false);
            DiagnosticLog.Warning(
                "spotify-auth",
                $"Serwer odrzucił żądanie tokenu: HTTP {(int)response.StatusCode}" +
                (oauthError.Length == 0 ? "." : $", {oauthError}."));
            throw new InvalidOperationException(DescribeTokenError(response.StatusCode, oauthError));
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var accessToken = root.TryGetProperty("access_token", out var access) ? access.GetString() : null;
        // Spotify zwraca NOWY refresh_token przy odświeżeniu i stary przestaje
        // działać. Nadpisujemy, a poprzedni zostawiamy tylko gdy odpowiedź go
        // nie zawiera.
        var refreshToken = root.TryGetProperty("refresh_token", out var refresh)
            ? refresh.GetString()
            : existingRefreshToken;
        var expiresIn = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
            ? Math.Max(60, seconds)
            : 3600;
        var returnedScope = root.TryGetProperty("scope", out var scopeElement)
            ? scopeElement.GetString() ?? string.Empty
            : string.Empty;
        var scope = string.IsNullOrWhiteSpace(returnedScope)
            ? existingScope ?? string.Empty
            : returnedScope;
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidDataException("Spotify zwrócił niepełne dane logowania.");

        // Logujemy wyłącznie NAZWY zakresów - żadnego tokenu ani danych konta.
        var grantedScopes = scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        DiagnosticLog.Info(
            "spotify-auth",
            $"Zakresy przyznane przez Spotify: {(grantedScopes.Length == 0 ? "brak informacji" : string.Join(", ", grantedScopes))}.");
        if (!SpotifyScopes.AllowsPlayback(scope))
        {
            DiagnosticLog.Warning(
                "spotify-auth",
                "Konto nie przyznało zakresu streaming, który jest wymagany przez wbudowany odtwarzacz "
                    + "Spotify. Zakres ten Spotify przyznaje tylko kontom Premium. Biblioteka i sterowanie "
                    + "oficjalną aplikacją mogą działać, ale odtwarzanie wewnątrz AMC nie wstanie.");
        }
        var missing = SpotifyScopes.Missing(scope);
        if (missing.Count > 0)
        {
            DiagnosticLog.Info(
                "spotify-auth",
                $"Zakresy nieprzyznane: {string.Join(", ", missing)}.");
        }
        return new SpotifyTokenSet(
            accessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            scope,
            clientId);
    }

    /// <summary>
    /// Zamienia surowy kod błędu OAuth na zdanie, z którego użytkownik wie,
    /// co zrobić. Bez tego czytnik ekranu odczytuje "invalid_client", co nie
    /// mówi nic, a najczęstsza przyczyna to literówka w identyfikatorze albo
    /// adres powrotu niewpisany w panelu Spotify.
    /// </summary>
    private static string DescribeAuthorizationError(string error)
    {
        var normalized = error.Trim();
        if (normalized.Contains("invalid_client", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify nie rozpoznał identyfikatora aplikacji. Sprawdź, czy identyfikator jest "
                + "wpisany bez spacji i czy pochodzi z panelu Spotify for Developers.";
        }
        if (normalized.Contains("redirect_uri", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("INVALID_CLIENT: Invalid redirect URI", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify odrzucił adres powrotu. Wpisz go w panelu aplikacji Spotify znak w znak "
                + "tak samo jak w tym oknie, razem z ukośnikiem na końcu.";
        }
        if (normalized.Contains("access_denied", StringComparison.OrdinalIgnoreCase))
            return "Logowanie Spotify zostało anulowane na stronie logowania.";
        return $"Logowanie Spotify nie zostało ukończone: {normalized}.";
    }

    private static string DescribeTokenError(HttpStatusCode statusCode, string oauthError)
    {
        if (oauthError.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase))
        {
            return "Zapisane logowanie Spotify straciło ważność. Zaloguj się ponownie w oknie "
                + "konta Spotify.";
        }
        if (oauthError.Contains("invalid_client", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify nie rozpoznał identyfikatora aplikacji. Sprawdź identyfikator w oknie "
                + "konta Spotify.";
        }
        return statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
            ? "Spotify odrzucił logowanie. Spróbuj zalogować się ponownie."
            : $"Logowanie Spotify zwróciło błąd HTTP {(int)statusCode}.";
    }

    private static async Task<string> ReadOAuthErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var code = root.TryGetProperty("error", out var error)
                ? error.GetString()?.Trim()
                : null;
            var description = root.TryGetProperty("error_description", out var errorDescription)
                ? errorDescription.GetString()?.Trim()
                : null;
            return string.Join(
                ", ",
                new[] { code, description }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Length > 240 ? value[..240] : value));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static TcpListener CreateListener(Uri redirectUri)
    {
        if (!redirectUri.IsLoopback || redirectUri.Scheme != Uri.UriSchemeHttp)
            throw new InvalidOperationException("Adres powrotu Spotify musi być lokalnym adresem HTTP.");
        var address = IPAddress.TryParse(redirectUri.Host, out var parsed)
            ? parsed
            : IPAddress.Loopback;
        return new TcpListener(address, redirectUri.Port);
    }

    private static async Task<AuthorizationCallback> ReceiveCallbackAsync(
        TcpListener listener,
        Uri redirectUri,
        string expectedState,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        using var client = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false) ?? string.Empty;
        string? header;
        do { header = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false); }
        while (!string.IsNullOrEmpty(header));

        var target = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
        var callbackUri = Uri.TryCreate(redirectUri.GetLeftPart(UriPartial.Authority) + target, UriKind.Absolute, out var parsed)
            ? parsed
            : null;
        var query = callbackUri is null ? new Dictionary<string, string>() : ParseQuery(callbackUri.Query);
        var validPath = callbackUri is not null
            && string.Equals(
                callbackUri.AbsolutePath.TrimEnd('/'),
                redirectUri.AbsolutePath.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        var validState = query.TryGetValue("state", out var state)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state),
                Encoding.UTF8.GetBytes(expectedState));
        var success = validPath && validState && query.ContainsKey("code");
        var message = success
            ? "Logowanie zakończone. Możesz zamknąć tę kartę i wrócić do AMC."
            : "Logowanie nie zostało ukończone. Wróć do AMC i spróbuj ponownie.";
        var body = $"<!doctype html><html lang=\"pl\"><meta charset=\"utf-8\"><title>AMC — Spotify</title><body><h1>{message}</h1></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, timeout.Token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

        if (!validPath) return new AuthorizationCallback(null, "nieprawidłowy adres powrotu");
        if (!validState) return new AuthorizationCallback(null, "nieprawidłowe zabezpieczenie sesji");
        return new AuthorizationCallback(
            query.GetValueOrDefault("code"),
            query.GetValueOrDefault("error_description") ?? query.GetValueOrDefault("error"));
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
                pair => pair.Length > 1
                    ? Uri.UnescapeDataString(pair[1].Replace('+', ' '))
                    : string.Empty,
                StringComparer.Ordinal);

    private static Uri BuildAuthorizationUri(SpotifySettings settings, string challenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = settings.RedirectUri,
            ["scope"] = SpotifyScopes.Requested,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        var query = string.Join('&', parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new UriBuilder(AuthorizationEndpoint) { Query = query }.Uri;
    }

    private static void ValidateSettings(SpotifySettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new InvalidOperationException("Najpierw wpisz identyfikator aplikacji Spotify.");
        if (!Uri.TryCreate(settings.RedirectUri, UriKind.Absolute, out var redirect)
            || !redirect.IsLoopback
            || redirect.Scheme != Uri.UriSchemeHttp)
            throw new InvalidOperationException("Adres powrotu Spotify musi być lokalnym adresem HTTP.");
        // Spotify odrzuca nazwę "localhost" w adresie powrotu; wymaga
        // liczbowego adresu pętli zwrotnej. Mówimy o tym tutaj, zanim
        // użytkownik zobaczy niejasny błąd na stronie Spotify.
        if (redirect.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Spotify nie przyjmuje adresu powrotu z nazwą localhost. Wpisz 127.0.0.1 zamiast localhost.");
        }
    }

    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }

    private sealed record AuthorizationCallback(string? Code, string? Error);
}
