using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Klient PIERWSZEGO logowania Sonos wobec brokera AMC. DWIE pojedyncze
/// operacje: rozpoczecie (POST /login/start) i JEDNORAZOWY odbior wyniku
/// (POST /login/result). Zadnego pollingu w petli, zadnych automatycznych
/// ponowien odbioru i zadnej orkiestracji UI - to osobne warstwy.
///
/// Kontrakt brokera odczytany ze zrodel amc_sonos_auth/core.py i server.py:
///   POST /login/start  {code_challenge, code_challenge_method:"S256"}
///        200 {session_id, authorize_url, expires_in}
///        400 invalid_code_challenge / unsupported_challenge_method / invalid_body
///        429 too_many_sessions, 503 server_not_configured
///   POST /login/result {session_id, code_verifier}
///        200 {access_token, token_type, expires_in, refresh_token, scope}
///        404 unknown_session (brak wyniku / juz odebrany / wygasla)
///        400 invalid_code_verifier albo rozpoznana ODMOWA (access_denied,
///            invalid_callback, token_exchange_failed)
///        403 verifier_mismatch
/// </summary>
public sealed class SonosLoginClient : IDisposable
{
    /// <summary>Skonczony limit odpowiedzi. Kontraktowe odpowiedzi brokera sa male.</summary>
    public const int MaxResponseBytes = 64 * 1024;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private readonly SonosLoginBrokerConfiguration configuration;
    private readonly HttpClient http;
    private readonly bool ownsHttpClient;
    private readonly Func<DateTimeOffset> clock;

    public SonosLoginClient(
        SonosLoginBrokerConfiguration configuration,
        HttpMessageHandler? handler = null,
        TimeSpan? timeout = null,
        Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this.configuration = configuration;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        if (handler is null)
        {
            // Brak automatycznych przekierowan: payload NIGDY nie moze pojsc na
            // inny host niz skonfigurowany, zaufany origin brokera.
            var ownHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            };
            http = new HttpClient(ownHandler, disposeHandler: true);
            ownsHttpClient = true;
        }
        else
        {
            http = new HttpClient(handler, disposeHandler: false);
            ownsHttpClient = true;
        }

        http.Timeout = timeout ?? DefaultTimeout;
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.MaxResponseContentBufferSize = MaxResponseBytes + 1;
    }

    /// <summary>
    /// Rozpoczyna logowanie: swiezy verifier 32 B CSPRNG i wyzwanie S256 tylko
    /// dla TEJ operacji. Nie otwiera przegladarki i nic nie zapisuje.
    /// </summary>
    public async Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
    {
        var (verifier, challenge) = SonosLoginPkce.Create();
        var payload = JsonSerializer.Serialize(new StartRequest(challenge, "S256"));
        var read = await SendAsync(configuration.StartUri, payload, cancellationToken).ConfigureAwait(false);
        if (read.Status is not null)
        {
            return SonosLoginStartOutcome.Failure(read.Status.Value);
        }

        if (read.HttpStatus != HttpStatusCode.OK)
        {
            return SonosLoginStartOutcome.Failure(MapStartFailure(read));
        }

        if (!TryParseObject(read.Body, out var document))
        {
            return SonosLoginStartOutcome.Failure(SonosLoginStatus.InvalidResponse);
        }

        using (document)
        {
            var root = document!.RootElement;
            var sessionId = ReadString(root, "session_id");
            var authorizeUrl = ReadString(root, "authorize_url");
            if (!SonosLoginPkce.LooksLikeS256Field(sessionId))
            {
                return SonosLoginStartOutcome.Failure(SonosLoginStatus.InvalidResponse);
            }

            if (!SonosAuthorizeUrlPolicy.TryParse(authorizeUrl, out var authorizeUri))
            {
                return SonosLoginStartOutcome.Failure(SonosLoginStatus.InvalidResponse);
            }

            if (!TryReadPositiveInt(root, "expires_in", out var expiresIn))
            {
                return SonosLoginStartOutcome.Failure(SonosLoginStatus.InvalidResponse);
            }

            var session = new SonosLoginSession(
                sessionId!,
                verifier,
                authorizeUri!,
                clock().AddSeconds(expiresIn));
            return SonosLoginStartOutcome.Ok(session);
        }
    }

    /// <summary>
    /// JEDNORAZOWY odbior wyniku. Verifier idzie wylacznie na zaufany origin z
    /// konfiguracji. Po niejednoznacznym bledzie klient NIE powtarza zadania -
    /// odbior jest jednorazowy po stronie brokera i ponowienie moglo by zgubic
    /// wydane tokeny.
    /// </summary>
    public async Task<SonosLoginResultOutcome> FetchResultAsync(
        SonosLoginSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (cancellationToken.IsCancellationRequested)
        {
            return SonosLoginResultOutcome.Failure(SonosLoginStatus.Canceled);
        }

        var expired = session.IsExpired(clock());
        var payload = JsonSerializer.Serialize(new ResultRequest(session.SessionId, session.CodeVerifier));
        var read = await SendAsync(configuration.ResultUri, payload, cancellationToken).ConfigureAwait(false);
        if (read.Status is not null)
        {
            return SonosLoginResultOutcome.Failure(read.Status.Value);
        }

        if (read.HttpStatus == HttpStatusCode.OK)
        {
            if (!TryParseObject(read.Body, out var document))
            {
                return SonosLoginResultOutcome.Failure(SonosLoginStatus.InvalidResponse);
            }

            using (document)
            {
                var root = document!.RootElement;
                var accessToken = ReadString(root, "access_token");
                if (string.IsNullOrEmpty(accessToken))
                {
                    return SonosLoginResultOutcome.Failure(SonosLoginStatus.InvalidResponse);
                }

                var tokenType = ReadString(root, "token_type") ?? "Bearer";
                int? expiresIn = TryReadPositiveInt(root, "expires_in", out var seconds) ? seconds : null;
                return SonosLoginResultOutcome.Ok(new SonosTokens(
                    accessToken!,
                    tokenType,
                    expiresIn,
                    ReadString(root, "refresh_token"),
                    ReadString(root, "scope")));
            }
        }

        return SonosLoginResultOutcome.Failure(MapResultFailure(read, expired));
    }

    // ---------- warstwa transportu ----------
    private async Task<ReadResponse> SendAsync(Uri target, string json, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ReadResponse.Rejected(SonosLoginStatus.Canceled);
        }

        if (!configuration.IsSameOrigin(target))
        {
            return ReadResponse.Rejected(SonosLoginStatus.RedirectRefused);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        try
        {
            using var response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            // Fail-closed: ani przekierowanie, ani cudzy host nie dostana payloadu.
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                return ReadResponse.Rejected(SonosLoginStatus.RedirectRefused);
            }

            if (response.RequestMessage?.RequestUri is { } finalUri
                && !configuration.IsSameOrigin(finalUri))
            {
                return ReadResponse.Rejected(SonosLoginStatus.RedirectRefused);
            }

            var body = await ReadBoundedAsync(response, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                return ReadResponse.Rejected(SonosLoginStatus.InvalidResponse);
            }

            return ReadResponse.Received(response.StatusCode, body);
        }
        catch (OperationCanceledException)
        {
            return ReadResponse.Rejected(
                cancellationToken.IsCancellationRequested
                    ? SonosLoginStatus.Canceled
                    : SonosLoginStatus.BrokerUnreachable);
        }
        catch (HttpRequestException)
        {
            return ReadResponse.Rejected(SonosLoginStatus.BrokerUnreachable);
        }
        catch (IOException)
        {
            return ReadResponse.Rejected(SonosLoginStatus.BrokerUnreachable);
        }
    }

    /// <summary>Czyta NAJWYZEJ MaxResponseBytes. Wieksza odpowiedz jest odrzucana, nie obcinana.</summary>
    private static async Task<string?> ReadBoundedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } declared && declared > MaxResponseBytes)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        using var accumulated = new MemoryStream();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            if (accumulated.Length + read > MaxResponseBytes)
            {
                return null;
            }

            accumulated.Write(buffer, 0, read);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(accumulated.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // ---------- mapowanie bledow (BEZ echa ciala odpowiedzi) ----------
    private static SonosLoginStatus MapStartFailure(ReadResponse read) => read.HttpStatus switch
    {
        HttpStatusCode.TooManyRequests => SonosLoginStatus.RateLimited,
        HttpStatusCode.ServiceUnavailable =>
            ReadErrorCode(read.Body) == "server_not_configured"
                ? SonosLoginStatus.BrokerNotConfigured
                : SonosLoginStatus.BrokerError,
        >= HttpStatusCode.InternalServerError => SonosLoginStatus.BrokerError,
        _ => SonosLoginStatus.InvalidResponse
    };

    private static SonosLoginStatus MapResultFailure(ReadResponse read, bool sessionAlreadyExpired)
    {
        switch (read.HttpStatus)
        {
            case HttpStatusCode.NotFound:
                // Broker nie rozroznia "jeszcze nie ma wyniku" od "wygasla".
                // Rozstrzyga lokalny, znany czas zycia sesji z /login/start.
                return sessionAlreadyExpired ? SonosLoginStatus.Expired : SonosLoginStatus.Pending;
            case HttpStatusCode.Forbidden:
                return SonosLoginStatus.Denied;
            case HttpStatusCode.BadRequest:
                return ReadErrorCode(read.Body) switch
                {
                    // Poprawnie zgloszona ODMOWA - nigdy nie wolno jej pomylic z tokenem.
                    "access_denied" or "invalid_callback" or "token_exchange_failed" or "login_failed" =>
                        SonosLoginStatus.Denied,
                    _ => SonosLoginStatus.InvalidResponse
                };
            case HttpStatusCode.TooManyRequests:
                return SonosLoginStatus.RateLimited;
            case HttpStatusCode.ServiceUnavailable:
                return ReadErrorCode(read.Body) == "server_not_configured"
                    ? SonosLoginStatus.BrokerNotConfigured
                    : SonosLoginStatus.BrokerError;
            default:
                return (int)read.HttpStatus >= 500
                    ? SonosLoginStatus.BrokerError
                    : SonosLoginStatus.InvalidResponse;
        }
    }

    /// <summary>
    /// Z ciala bledu czytamy WYLACZNIE krotki, rozpoznany kod z listy - nigdy
    /// dowolnego tekstu od serwera, zeby zadna zlosliwa odpowiedz nie przeciekla
    /// do komunikatu dla uzytkownika.
    /// </summary>
    private static string? ReadErrorCode(string body)
    {
        if (!TryParseObject(body, out var document))
        {
            return null;
        }

        using (document)
        {
            var code = ReadString(document!.RootElement, "error");
            if (code is null || code.Length is 0 or > 64)
            {
                return null;
            }

            foreach (var character in code)
            {
                var allowed = character is >= 'a' and <= 'z' or '_';
                if (!allowed)
                {
                    return null;
                }
            }

            return code;
        }
    }

    private static bool TryParseObject(string body, out JsonDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxResponseBytes)
        {
            return false;
        }

        try
        {
            var parsed = JsonDocument.Parse(body);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                parsed.Dispose();
                return false;
            }

            document = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryReadPositiveInt(JsonElement root, string name, out int result)
    {
        result = 0;
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (!value.TryGetInt32(out var parsed) || parsed <= 0)
        {
            return false;
        }

        result = parsed;
        return true;
    }

    public void Dispose()
    {
        if (ownsHttpClient)
        {
            http.Dispose();
        }
    }

    private sealed record StartRequest(string code_challenge, string code_challenge_method);

    private sealed record ResultRequest(string session_id, string code_verifier);

    private readonly struct ReadResponse
    {
        private ReadResponse(SonosLoginStatus? status, HttpStatusCode httpStatus, string body)
        {
            Status = status;
            HttpStatus = httpStatus;
            Body = body;
        }

        /// <summary>Ustawiony tylko wtedy, gdy wynik zostal rozstrzygniety przed odczytem ciala.</summary>
        public SonosLoginStatus? Status { get; }

        public HttpStatusCode HttpStatus { get; }

        public string Body { get; }

        public static ReadResponse Rejected(SonosLoginStatus status) =>
            new(status, HttpStatusCode.Unused, string.Empty);

        public static ReadResponse Received(HttpStatusCode status, string body) =>
            new(null, status, body);
    }
}
