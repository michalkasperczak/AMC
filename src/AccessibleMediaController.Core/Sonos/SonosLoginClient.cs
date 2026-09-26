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

    /// <summary>
    /// Gorny zakres budzetu wynika z CancellationTokenSource/HttpClient: oba
    /// przyjmuja najwyzej int.MaxValue milisekund. Nie jest to dobrany "limit
    /// produktowy", tylko granica uzywanych mechanizmow.
    /// </summary>
    public static readonly TimeSpan MaxOperationTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly SonosLoginBrokerConfiguration configuration;
    private readonly TimeSpan operationTimeout;
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

        // NAJPIERW budzet: niepoprawny albo nieskonczony limit jest odrzucany
        // PRZED utworzeniem HttpClient i handlera, zeby zadne zasoby nie zostaly
        // po odrzuconym wywolaniu.
        operationTimeout = ValidateTimeout(timeout ?? DefaultTimeout);

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

        http.Timeout = operationTimeout;
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.MaxResponseContentBufferSize = MaxResponseBytes + 1;
    }

    /// <summary>
    /// Budzet CALEJ operacji musi byc skonczony i dodatni. Timeout.InfiniteTimeSpan
    /// (-1 ms) oraz kazda inna wartosc <= 0 lub ponad zakres CTS/HttpClient jest
    /// odrzucana - nieskonczony budzet znaczy, ze zawieszona odpowiedz blokuje
    /// logowanie bez konca.
    /// </summary>
    private static TimeSpan ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > MaxOperationTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Budżet operacji logowania Sonos musi być skończony i dodatni.");
        }

        return timeout;
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

                var tokenType = ReadString(root, "token_type");
                if (!SonosTokens.TryCanonicalizeTokenType(tokenType, out var canonicalTokenType))
                {
                    // Nieobslugiwany typ tokenu: fail-closed i STALY komunikat.
                    // Wartosci z odpowiedzi nie wolno zwrocic ani wypisac.
                    return SonosLoginResultOutcome.Failure(SonosLoginStatus.InvalidResponse);
                }

                int? expiresIn = TryReadPositiveInt(root, "expires_in", out var seconds) ? seconds : null;
                return SonosLoginResultOutcome.Ok(new SonosTokens(
                    accessToken!,
                    canonicalTokenType,
                    expiresIn,
                    ReadString(root, "refresh_token"),
                    ReadString(root, "scope")));
            }
        }

        return SonosLoginResultOutcome.Failure(MapResultFailure(read, expired));
    }


    /// <summary>
    /// ODNOWIENIE dostepu: JEDNO zadanie POST /login/refresh na SKONFIGUROWANY,
    /// zaufany origin, tym samym ograniczonym transportem, co Start/Fetch (bez
    /// przekierowan, bez ciasteczek, jeden skonczony deadline na naglowki i
    /// cialo, limit 64 KiB). Zadnego pollingu i ZADNEGO automatycznego
    /// ponowienia po niejednoznacznym wyniku.
    ///
    /// Token jest NIEPRZEZROCZYSTY: idzie dokladnie tak, jak przyszedl (bez
    /// trim, normalizacji i wzorcow). Cialo ma DOKLADNIE jedno pole
    /// refresh_token - klient nie zna i nie wysyla sekretu aplikacji.
    ///
    /// Ta metoda NIGDY nie kasuje poswiadczen. Zwraca rozpoznany wynik; jedynie
    /// 401 z kontraktowym reauthorization_required ustawia
    /// RequiresReauthorization, a decyzja nalezy do warstwy wyzszej.
    /// </summary>
    public async Task<SonosRefreshOutcome> RefreshAsync(
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return SonosRefreshOutcome.Failure(SonosRefreshStatus.Canceled);
        }

        // NASZA polityka zadania sprawdzana LOKALNIE: zle wejscie nie generuje
        // ZADNEGO zapytania HTTP i nie jest wyrokiem o waznosci poswiadczen.
        if (!SonosRefreshTokenPolicy.IsAcceptable(refreshToken))
        {
            return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidLocalToken);
        }

        var payload = JsonSerializer.Serialize(
            new SonosRefreshTokenPolicy.RefreshRequestBody(refreshToken!));

        // Druga, NIEZALEZNA kontrola: liczymy RZECZYWISTE bajty ciala, ktore
        // wyslemy. Model budzetu jest zachowawczy, ale to pomiar decyduje - bez
        // niego mozna by wyslac cialo, ktore broker odrzuci przez 413.
        if (Encoding.UTF8.GetByteCount(payload) > SonosRefreshTokenPolicy.MaxRequestBodyBytes)
        {
            return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidLocalToken);
        }

        var read = await SendAsync(configuration.RefreshUri, payload, cancellationToken).ConfigureAwait(false);
        if (read.Status is not null)
        {
            return SonosRefreshOutcome.Failure(MapRefreshTransport(read.Status.Value));
        }

        if (read.HttpStatus != HttpStatusCode.OK)
        {
            // Kod bledu to rowniez TEKST z odpowiedzi: wadliwe Unicode (np.
            // samotny surogat) parsuje sie jako dokument, ale rzuca dopiero przy
            // dekodowaniu. Taka odpowiedz jest NIEZGODNA - nigdy nie wolno z niej
            // wyprowadzic zadania ponownego logowania ani wypuscic wyjatku.
            try
            {
                return SonosRefreshOutcome.Failure(MapRefreshFailure(read));
            }
            catch (InvalidOperationException)
            {
                return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
            }
        }

        if (!TryParseObject(read.Body, out var document))
        {
            return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
        }

        // Granica DEKODOWANIA odpowiedzi. Transport, deadline i anulowanie sa
        // rozstrzygniete WYZEJ i zostaja poza tym blokiem. Tu lapiemy wylacznie
        // InvalidOperationException z JsonElement.GetString dla wadliwego UTF-16;
        // bez echa tresci ciala i bez tekstu wyjatku.
        using (document)
        try
        {
            var root = document!.RootElement;
            var accessToken = ReadString(root, "access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
            }

            // token_type jest tu WYMAGANY (backend gwarantuje go w kazdym 200
            // /login/refresh). Brak pola, null i zly typ to trzy rozne wady tej
            // samej odpowiedzi - zadnej z nich NIE domyslamy sie na Bearer, bo
            // wtedy zepsuta odpowiedz wygladalaby na sukces. To NIE zmienia
            // semantyki /login/result, gdzie brak pola jest kontraktowy.
            if (!root.TryGetProperty("token_type", out var tokenTypeElement)
                || tokenTypeElement.ValueKind != JsonValueKind.String
                || !SonosTokens.TryCanonicalizeTokenType(tokenTypeElement.GetString(), out var canonicalTokenType)
                || tokenTypeElement.GetString() is null)
            {
                return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
            }

            // refresh_token jest ZAWSZE obecny w kontraktowym 200 (broker sam
            // realizuje dopuszczalny nawrot do dotychczasowej wartosci). Gdy go
            // nie ma, jest nullem, nie-tekstem albo nie przechodzi TEJ SAMEJ
            // polityki co nasze wejscie - to zepsuta odpowiedz, a NIE zgoda na
            // ciche zatrzymanie starej wartosci.
            if (!root.TryGetProperty("refresh_token", out var refreshElement)
                || refreshElement.ValueKind != JsonValueKind.String)
            {
                return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
            }

            var rotated = refreshElement.GetString();
            if (!SonosRefreshTokenPolicy.IsAcceptable(rotated))
            {
                return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
            }

            // expires_in jest OPCJONALNE, ale obecne musi byc dodatnia liczba
            // calkowita. Obecna, niepoprawna wartosc to zepsuta odpowiedz.
            int? expiresIn = null;
            if (root.TryGetProperty("expires_in", out var expiresElement)
                && expiresElement.ValueKind != JsonValueKind.Undefined)
            {
                if (!TryReadPositiveInt(root, "expires_in", out var seconds))
                {
                    return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
                }

                expiresIn = seconds;
            }

            // scope jest OPCJONALNE i musi byc tekstem, gdy jest obecne.
            string? scope = null;
            if (root.TryGetProperty("scope", out var scopeElement))
            {
                if (scopeElement.ValueKind != JsonValueKind.String)
                {
                    return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
                }

                scope = scopeElement.GetString();
            }

            return SonosRefreshOutcome.Ok(new SonosTokens(
                accessToken!,
                canonicalTokenType,
                expiresIn,
                rotated,
                scope));
        }
        catch (InvalidOperationException)
        {
            // Wadliwy UTF-16 w tekstowym polu odpowiedzi. Zwracamy bezpieczny
            // wynik: brak tokenow i BRAK zadania ponownego logowania.
            return SonosRefreshOutcome.Failure(SonosRefreshStatus.InvalidResponse);
        }
    }

    /// <summary>Wyniki rozstrzygniete w transporcie (przed odczytem ciala).</summary>
    private static SonosRefreshStatus MapRefreshTransport(SonosLoginStatus status) => status switch
    {
        SonosLoginStatus.Canceled => SonosRefreshStatus.Canceled,
        SonosLoginStatus.RedirectRefused => SonosRefreshStatus.RedirectRefused,
        SonosLoginStatus.BrokerUnreachable => SonosRefreshStatus.BrokerUnreachable,
        _ => SonosRefreshStatus.InvalidResponse
    };

    /// <summary>
    /// Mapowanie statusow /login/refresh BEZ echa ciala. Kasowania poswiadczen
    /// domaga sie WYLACZNIE 401 z kontraktowym kodem reauthorization_required -
    /// HTML, nieznany kod i zepsuty JSON przy 401 to niezgodna odpowiedz.
    /// </summary>
    private static SonosRefreshStatus MapRefreshFailure(ReadResponse read)
    {
        switch (read.HttpStatus)
        {
            case HttpStatusCode.Unauthorized:
                return ReadErrorCode(read.Body) == "reauthorization_required"
                    ? SonosRefreshStatus.ReauthorizationRequired
                    : SonosRefreshStatus.InvalidResponse;
            case HttpStatusCode.BadRequest:
                // invalid_body / invalid_json / invalid_refresh_token: broker NIE
                // wolal dostawcy, wiec nic nie wiemy o waznosci tokenu.
                return ReadErrorCode(read.Body) is "invalid_body" or "invalid_json" or "invalid_refresh_token"
                    ? SonosRefreshStatus.RequestRejected
                    : SonosRefreshStatus.InvalidResponse;
            case HttpStatusCode.MethodNotAllowed:
            case HttpStatusCode.RequestEntityTooLarge:
                return SonosRefreshStatus.RequestRejected;
            case HttpStatusCode.TooManyRequests:
                return SonosRefreshStatus.RateLimited;
            case HttpStatusCode.ServiceUnavailable:
                return ReadErrorCode(read.Body) switch
                {
                    "server_not_configured" => SonosRefreshStatus.BrokerNotConfigured,
                    "refresh_unavailable" => SonosRefreshStatus.RefreshUnavailable,
                    _ => SonosRefreshStatus.BrokerError
                };
            default:
                // 404 nigdy nie znaczy "trwa" - to niezgodna odpowiedz.
                return (int)read.HttpStatus >= 500
                    ? SonosRefreshStatus.BrokerError
                    : SonosRefreshStatus.InvalidResponse;
        }
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

        // JEDEN skonczony deadline na wyslanie, odczyt naglowkow i odczyt ciala.
        // Nie obejmuje samego Dispose odpowiedzi/strumienia - to robi blok
        // using po wyjsciu z operacji i deadline go nie przerywa. HttpClient.Timeout przy
        // ResponseHeadersRead nie obejmuje fazy ciala, wiec bez tego zawieszony
        // strumien blokowalby logowanie bez konca.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(operationTimeout);
        try
        {
            using var response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
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

            var body = await ReadBoundedAsync(response, deadline.Token).ConfigureAwait(false);
            if (body is null)
            {
                return ReadResponse.Rejected(SonosLoginStatus.InvalidResponse);
            }

            return ReadResponse.Received(response.StatusCode, body);
        }
        catch (OperationCanceledException)
        {
            // Anulowanie WOLAJACEGO odrozniamy od wlasnego deadline. Oba moga
            // wystapic w fazie ciala, ale znacza dla uzytkownika co innego.
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

    /// <summary>
    /// Czyta NAJWYZEJ MaxResponseBytes w ramach TEGO SAMEGO deadline calej
    /// operacji - limit NIE jest odnawiany po kazdym odczycie, wiec saczone
    /// cialo tez sie w nim miesci albo zostaje przerwane. Wieksza odpowiedz jest
    /// odrzucana, nie obcinana.
    /// </summary>
    private static async Task<string?> ReadBoundedAsync(
        HttpResponseMessage response,
        CancellationToken deadlineToken)
    {
        if (response.Content.Headers.ContentLength is { } declared && declared > MaxResponseBytes)
        {
            return null;
        }

        // Jawne, ograniczone sprzatanie: strumien jest zwalniany takze wtedy, gdy
        // deadline przerwie odczyt w polowie ciala.
        var stream = await response.Content.ReadAsStreamAsync(deadlineToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[8192];
            using var accumulated = new MemoryStream();
            while (true)
            {
                var read = await stream.ReadAsync(buffer, deadlineToken).ConfigureAwait(false);
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
                // TYLKO kontraktowe 404 {"error": "unknown_session"} (core.py
                // 328/337) znaczy "broker nie ma dla nas wyniku". Broker oddaje
                // 404 {"error": "not_found"} takze dla nieznanej sciezki
                // (server.py 299), a obcy serwer, proxy lub blad konfiguracji
                // moga zwrocic 404 z dowolnym cialem. Takie 404 NIE jest
                // dowodem, ze logowanie trwa, wiec nie wolno go tlumaczyc na
                // Pending/Expired - to byloby ciche zapraszanie do czekania na
                // wynik, ktory nigdy nie przyjdzie.
                if (ReadErrorCode(read.Body) != "unknown_session")
                {
                    return SonosLoginStatus.InvalidResponse;
                }

                // Sam broker nie rozroznia "jeszcze nie ma wyniku" od "wygasla".
                // Rozstrzyga lokalny, znany czas zycia sesji z /login/start.
                return sessionAlreadyExpired ? SonosLoginStatus.Expired : SonosLoginStatus.Pending;
            case HttpStatusCode.Forbidden:
                // 403 verifier_mismatch: broker odrzucil NASZ lokalny dowod PKCE i
                // NIE skonsumowal gotowego wyniku. Sonos nie podjal tu zadnej
                // decyzji, wiec nie wolno go obwiniac.
                return SonosLoginStatus.ProofMismatch;
            case HttpStatusCode.BadRequest:
                return ReadErrorCode(read.Body) switch
                {
                    // TYLKO to jest rzeczywista odmowa: core.py zapisuje
                    // access_denied wtedy, gdy dostawca zglosil provider_error.
                    "access_denied" => SonosLoginStatus.Denied,
                    // invalid_callback to brak/niepoprawna dlugosc parametru code
                    // w callbacku (core.py 271-273), a token_exchange_failed to
                    // WYJATEK wymiany kodu po stronie brokera (core.py 274-277:
                    // timeout, zly JSON, status dostawcy). Ani jedno, ani drugie
                    // nie jest decyzja Sonos.
                    "invalid_callback" or "token_exchange_failed" or "login_failed" =>
                        SonosLoginStatus.BrokerError,
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
