using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Tidal;

namespace AccessibleMediaController.Windows.Services;

internal sealed class TidalOAuthClient(HttpClient? httpClient = null) : IDisposable
{
    private static readonly Uri AuthorizationEndpoint = new("https://login.tidal.com/authorize");
    private static readonly Uri TokenEndpoint = new("https://auth.tidal.com/v1/oauth2/token");
    // Pelne utwory pobiera starsze API manifestu (api.tidal.com/v1/.../playbackinfo),
    // ktore autoryzuje sie zakresami r_usr/w_usr, a nie zakresami nowego
    // openapi.tidal.com/v2. Player SDK wysyla juz assetpresentation=FULL, wiec o
    // probce 30 s decyduje wylacznie zakres tokenu. Oficjalne przyklady SDK
    // (web, iOS, Android) uzywaja r_usr/w_usr. Nowe zakresy zostaja, bo na nich
    // dziala kolekcja i playlisty w API v2.
    private const string RequestedScope =
        "r_usr w_usr user.read collection.read collection.write playlists.read playlists.write";
    private readonly HttpClient http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private readonly bool ownsHttpClient = httpClient is null;

    public async Task<TidalTokenSet> AuthorizeAsync(
        TidalSettings settings,
        CancellationToken cancellationToken)
    {
        ValidateSettings(settings);
        var redirectUri = new Uri(settings.RedirectUri);
        DiagnosticLog.Info("tidal-auth", "Rozpoczęto logowanie OAuth TIDAL.");
        var pkce = TidalPkce.Create();
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        using var listener = CreateListener(redirectUri);
        listener.Start();
        DiagnosticLog.Info("tidal-auth", $"Lokalny odbiornik odpowiedzi działa na porcie {redirectUri.Port}.");
        var authorizationUri = BuildAuthorizationUri(settings, pkce.Challenge, state);
        Process.Start(new ProcessStartInfo(authorizationUri.AbsoluteUri) { UseShellExecute = true });
        DiagnosticLog.Info("tidal-auth", "Otwarto oficjalną stronę logowania TIDAL w przeglądarce.");

        var callback = await ReceiveCallbackAsync(
            listener,
            redirectUri,
            state,
            cancellationToken).ConfigureAwait(false);
        DiagnosticLog.Info(
            "tidal-auth",
            string.IsNullOrWhiteSpace(callback.Error)
                ? "Odebrano odpowiedź z logowania TIDAL."
                : "Odebrano odpowiedź o nieukończonym logowaniu TIDAL.");
        if (!string.IsNullOrWhiteSpace(callback.Error))
            throw new InvalidOperationException($"Logowanie TIDAL nie zostało ukończone: {callback.Error}.");
        if (string.IsNullOrWhiteSpace(callback.Code))
            throw new InvalidOperationException("TIDAL nie przekazał kodu logowania.");

        return await ExchangeCodeAsync(
            settings,
            callback.Code,
            pkce.Verifier,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TidalTokenSet> RefreshAsync(
        TidalSettings settings,
        TidalTokenSet current,
        CancellationToken cancellationToken)
    {
        ValidateSettings(settings);
        using var response = await http.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = current.RefreshToken,
                // Odswiezenie nie moze rozszerzac uprawnien: serwer odrzuca zakres
                // szerszy od przyznanego, co wylogowywaloby uzytkownika po zmianie
                // listy zakresow w nowej wersji AMC. Wysylamy to, co konto faktycznie
                // przyznalo; nowe zakresy wchodza dopiero przy ponownym logowaniu.
                ["scope"] = string.IsNullOrWhiteSpace(current.Scope)
                    ? RequestedScope
                    : current.Scope
            }),
            cancellationToken).ConfigureAwait(false);
        return await ReadTokensAsync(
            response,
            current.RefreshToken,
            current.Scope,
            settings.ClientId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<TidalTokenSet> ExchangeCodeAsync(
        TidalSettings settings,
        string code,
        string verifier,
        CancellationToken cancellationToken)
    {
        DiagnosticLog.Info("tidal-auth", "Wymiana kodu logowania na token TIDAL.");
        using var response = await http.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = settings.ClientId,
                ["code"] = code,
                ["redirect_uri"] = settings.RedirectUri,
                ["code_verifier"] = verifier
            }),
            cancellationToken).ConfigureAwait(false);
        var tokens = await ReadTokensAsync(
            response,
            null,
            null,
            settings.ClientId,
            cancellationToken).ConfigureAwait(false);
        DiagnosticLog.Info("tidal-auth", "TIDAL zaakceptował kod logowania.");
        return tokens;
    }

    private static async Task<TidalTokenSet> ReadTokensAsync(
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
                "tidal-auth",
                $"Serwer odrzucił żądanie tokenu: HTTP {(int)response.StatusCode}" +
                (oauthError.Length == 0 ? "." : $", {oauthError}."));
            throw new InvalidOperationException(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                ? "TIDAL odrzucił logowanie. Spróbuj zalogować się ponownie."
                : $"Logowanie TIDAL zwróciło błąd HTTP {(int)response.StatusCode}.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var accessToken = root.TryGetProperty("access_token", out var access) ? access.GetString() : null;
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
            throw new InvalidDataException("TIDAL zwrócił niepełne dane logowania.");
        return new TidalTokenSet(
            accessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            scope,
            clientId);
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
            throw new InvalidOperationException("Adres powrotu TIDAL musi być lokalnym adresem HTTP.");
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
        var body = $"<!doctype html><html lang=\"pl\"><meta charset=\"utf-8\"><title>AMC — TIDAL</title><body><h1>{message}</h1></body></html>";
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

    private static Uri BuildAuthorizationUri(TidalSettings settings, string challenge, string state)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = settings.RedirectUri,
            ["scope"] = RequestedScope,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state
        };
        var query = string.Join('&', parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new UriBuilder(AuthorizationEndpoint) { Query = query }.Uri;
    }

    private static void ValidateSettings(TidalSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new InvalidOperationException("Najpierw wpisz identyfikator aplikacji TIDAL.");
        if (!Uri.TryCreate(settings.RedirectUri, UriKind.Absolute, out var redirect)
            || !redirect.IsLoopback
            || redirect.Scheme != Uri.UriSchemeHttp)
            throw new InvalidOperationException("Adres powrotu TIDAL musi być lokalnym adresem HTTP.");
    }

    public void Dispose()
    {
        if (ownsHttpClient) http.Dispose();
    }

    private sealed record AuthorizationCallback(string? Code, string? Error);
}
