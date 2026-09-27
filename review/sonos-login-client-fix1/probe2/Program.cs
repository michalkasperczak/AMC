// Niezalezna sonda zgodnosci ze specyfikacja pierwszego klienta Sonos.
// WYLACZNIE dane syntetyczne, wstrzykniety handler, zero sieci, zero Sonos LIVE.
// Kazdy przypadek ma ASERCJE; niespelnione oczekiwanie => kod wyjscia 1.
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Sonos;

const string BrokerOrigin = "https://broker.invalid.test";
const string SyntheticMarker = "SYNTHETIC-DO-NOT-LOG-EXAMPLE";

var report = new List<object>();
int failures = 0;

void Check(string id, string spec, bool pass, object detail)
{
    if (!pass) failures++;
    report.Add(new { id, spec, verdict = pass ? "SPEC_PASS" : "SPEC_FAIL", detail });
}

if (!SonosLoginBrokerConfiguration.TryCreate(BrokerOrigin, out var cfg) || cfg is null)
{
    Console.Error.WriteLine("nie udalo sie zbudowac konfiguracji brokera");
    return 2;
}

string Sid() => SonosLoginPkce.CreateVerifier(); // 43 znaki base64url, jak w brokerze
string StartBody(string authorizeUrl, int expires = 600) => JsonSerializer.Serialize(
    new Dictionary<string, object> { ["session_id"] = Sid(), ["authorize_url"] = authorizeUrl, ["expires_in"] = expires });

SonosLoginSession FreshSession(DateTimeOffset now, int secondsLeft) =>
    new(Sid(), SonosLoginPkce.CreateVerifier(),
        new Uri("https://api.sonos.com/login/v3/oauth?client_id=synthetic"), now.AddSeconds(secondsLeft));

// ---------- 1. KONTROLA: szybkie cialo musi dac Success ----------
{
    var handler = new ScriptedHandler(HttpStatusCode.OK,
        "{\"access_token\":\"" + SyntheticMarker + "\",\"token_type\":\"Bearer\",\"expires_in\":3600}");
    var now = DateTimeOffset.UtcNow;
    using var client = new SonosLoginClient(cfg, handler, TimeSpan.FromSeconds(5), () => now);
    var sw = Stopwatch.StartNew();
    var r = await client.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);
    sw.Stop();
    Check("K1-kontrola-szybkie-cialo", "operacja bez opoznien konczy sie Success",
        r.Status == SonosLoginStatus.Success, new { status = r.Status.ToString(), elapsed_ms = sw.ElapsedMilliseconds });
}

// ---------- 2. BLOKADA kandydacka: czy timeout ogranicza CALA operacje ----------
{
    var handler = new ScriptedHandler(HttpStatusCode.OK,
        "{\"access_token\":\"" + SyntheticMarker + "\",\"token_type\":\"Bearer\",\"expires_in\":3600}",
        bodyDelayMs: 1500);
    var now = DateTimeOffset.UtcNow;
    using var client = new SonosLoginClient(cfg, handler, TimeSpan.FromMilliseconds(100), () => now);
    var sw = Stopwatch.StartNew();
    var r = await client.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);
    sw.Stop();
    // Spec: skuteczny SKONCZONY timeout CALEJ operacji => przy 100 ms limitu i 1500 ms
    // opoznienia ciala operacja NIE moze zakonczyc sie sukcesem po ~1500 ms.
    Check("B1-timeout-calej-operacji",
        "skuteczny skonczony timeout CALEJ operacji (naglowki + cialo)",
        r.Status != SonosLoginStatus.Success && sw.ElapsedMilliseconds < 1000,
        new
        {
            timeout_ms = 100,
            body_delay_ms = 1500,
            elapsed_ms = sw.ElapsedMilliseconds,
            status = r.Status.ToString(),
            oczekiwano = "BrokerUnreachable w < 1000 ms"
        });
}

// ---------- 3. Czy API przyjmuje timeout nieskonczony ----------
{
    bool accepted;
    string? thrown = null;
    try
    {
        using var c = new SonosLoginClient(cfg, new ScriptedHandler(HttpStatusCode.OK, "{}"), Timeout.InfiniteTimeSpan);
        accepted = true;
    }
    catch (Exception ex) { accepted = false; thrown = ex.GetType().Name; }
    Check("B2-timeout-nieskonczony-odrzucony",
        "API nie pozwala skonfigurowac operacji bez skonczonego limitu czasu",
        !accepted, new { infinite_timespan_zaakceptowany = accepted, wyjatek = thrown });
}

// ---------- 4. Czy tekst od serwera trafia do diagnostyki ----------
{
    var handler = new ScriptedHandler(HttpStatusCode.OK,
        "{\"access_token\":\"AT-" + SyntheticMarker + "\",\"token_type\":\"" + SyntheticMarker + "\",\"expires_in\":3600}");
    var now = DateTimeOffset.UtcNow;
    using var client = new SonosLoginClient(cfg, handler, TimeSpan.FromSeconds(5), () => now);
    var r = await client.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);
    var text = r.Tokens?.ToString() ?? string.Empty;
    bool tokenLeak = text.Contains("AT-" + SyntheticMarker, StringComparison.Ordinal);
    bool serverTextEcho = text.Contains(SyntheticMarker, StringComparison.Ordinal);
    Check("B3-brak-echa-tekstu-serwera-w-diagnostyce",
        "diagnostyka nie cytuje niezwalidowanego tekstu z odpowiedzi serwera",
        !serverTextEcho,
        new
        {
            status = r.Status.ToString(),
            wartosc_access_token_w_ToString = tokenLeak,
            niezwalidowany_token_type_w_ToString = serverTextEcho,
            ToString_dlugosc = text.Length
        });
}

// ---------- 5. Status: pending vs expiry na tym samym 404 brokera ----------
{
    var now = DateTimeOffset.UtcNow;
    var h1 = new ScriptedHandler(HttpStatusCode.NotFound, "{\"error\":\"unknown_session\"}");
    using var c1 = new SonosLoginClient(cfg, h1, TimeSpan.FromSeconds(5), () => now);
    var pending = await c1.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);

    var h2 = new ScriptedHandler(HttpStatusCode.NotFound, "{\"error\":\"unknown_session\"}");
    using var c2 = new SonosLoginClient(cfg, h2, TimeSpan.FromSeconds(5), () => now);
    var expired = await c2.FetchResultAsync(FreshSession(now, -5), CancellationToken.None);

    Check("S1-pending-i-expiry", "404 brokera rozdzielone na Pending oraz Expired",
        pending.Status == SonosLoginStatus.Pending && expired.Status == SonosLoginStatus.Expired,
        new { sesja_zywa = pending.Status.ToString(), sesja_wygasla = expired.Status.ToString() });
}

// ---------- 6. Statusy odmowy, 429, 503, 5xx ----------
{
    var now = DateTimeOffset.UtcNow;
    async Task<SonosLoginStatus> Result(HttpStatusCode code, string body)
    {
        using var c = new SonosLoginClient(cfg, new ScriptedHandler(code, body), TimeSpan.FromSeconds(5), () => now);
        return (await c.FetchResultAsync(FreshSession(now, 300), CancellationToken.None)).Status;
    }

    var denied400 = await Result(HttpStatusCode.BadRequest, "{\"error\":\"access_denied\"}");
    var exchFail = await Result(HttpStatusCode.BadRequest, "{\"error\":\"token_exchange_failed\"}");
    var mismatch403 = await Result(HttpStatusCode.Forbidden, "{\"error\":\"verifier_mismatch\"}");
    var rate429 = await Result(HttpStatusCode.TooManyRequests, "{\"error\":\"too_many_sessions\"}");
    var notConf = await Result(HttpStatusCode.ServiceUnavailable, "{\"error\":\"server_not_configured\"}");
    var busy503 = await Result(HttpStatusCode.ServiceUnavailable, "przeciazenie bez JSON");
    var err500 = await Result(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");

    // OCZEKIWANIE SKORYGOWANE w tej NOWEJ kopii sondy (stary dowod nienaruszony):
    // core.py 260-277 ustawia token_exchange_failed przy WYJATKU wymiany tokenu
    // (timeout/JSON/HTTP), a nie przy decyzji Sonos. Tylko access_denied jest
    // prawdziwa odmowa, wiec stare "exchFail == Denied" bylo tym samym bledem
    // przypisania winy, ktory naprawiamy.
    Check("S2-odmowa-429-503-5xx", "rozdzielne statusy odmowy, 429, braku konfiguracji i bledu serwera",
        denied400 == SonosLoginStatus.Denied && exchFail != SonosLoginStatus.Denied
        && rate429 == SonosLoginStatus.RateLimited && notConf == SonosLoginStatus.BrokerNotConfigured
        && busy503 == SonosLoginStatus.BrokerError && err500 == SonosLoginStatus.BrokerError,
        new
        {
            b400_access_denied = denied400.ToString(),
            b400_token_exchange_failed = exchFail.ToString(),
            b429 = rate429.ToString(),
            b503_server_not_configured = notConf.ToString(),
            b503_bez_json = busy503.ToString(),
            b500 = err500.ToString()
        });

    // Osobno: 403 verifier_mismatch to NIE odmowa Sonos; broker nie konsumuje wyniku.
    Check("S3-403-nie-jest-odmowa-sonos",
        "403 verifier_mismatch nie jest raportowane jako odmowa dostepu przez Sonos",
        mismatch403 != SonosLoginStatus.Denied,
        new
        {
            status = mismatch403.ToString(),
            komunikat = SonosLoginMessages.Describe(mismatch403),
            uwaga = "core.py login_result: przy 403 result_taken NIE jest ustawiane"
        });
}

// ---------- 7. Limit ciala odpowiedzi ----------
{
    var now = DateTimeOffset.UtcNow;
    var big = "{\"access_token\":\"" + new string('a', 70 * 1024) + "\"}";
    using var c = new SonosLoginClient(cfg, new ScriptedHandler(HttpStatusCode.OK, big), TimeSpan.FromSeconds(10), () => now);
    var r = await c.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);
    Check("S4-limit-ciala", "odpowiedz ponad 64 KiB odrzucona, nie obcieta",
        r.Status == SonosLoginStatus.InvalidResponse && r.Tokens is null,
        new { rozmiar_b = big.Length, status = r.Status.ToString() });
}

// ---------- 8. Fail-closed na przekierowaniu ----------
{
    var now = DateTimeOffset.UtcNow;
    var h = new ScriptedHandler(HttpStatusCode.Found, "{}", location: "https://zly-host.invalid.test/login/result");
    using var c = new SonosLoginClient(cfg, h, TimeSpan.FromSeconds(5), () => now);
    var r = await c.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);

    var h2 = new ScriptedHandler(HttpStatusCode.OK, "{\"access_token\":\"x\"}",
        finalUri: new Uri("https://zly-host.invalid.test/login/result"));
    using var c2 = new SonosLoginClient(cfg, h2, TimeSpan.FromSeconds(5), () => now);
    var r2 = await c2.FetchResultAsync(FreshSession(now, 300), CancellationToken.None);

    Check("S5-fail-closed-redirect", "3xx oraz obcy finalny host nie dostaja payloadu",
        r.Status == SonosLoginStatus.RedirectRefused && r2.Status == SonosLoginStatus.RedirectRefused,
        new { odpowiedz_302 = r.Status.ToString(), obcy_final_uri = r2.Status.ToString() });
}

// ---------- 9. Anulowanie ----------
{
    var now = DateTimeOffset.UtcNow;
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    using var c = new SonosLoginClient(cfg, new ScriptedHandler(HttpStatusCode.OK, "{}"), TimeSpan.FromSeconds(5), () => now);
    var r = await c.FetchResultAsync(FreshSession(now, 300), cts.Token);
    Check("S6-anulowanie", "anulowanie wolajacego daje status Canceled",
        r.Status == SonosLoginStatus.Canceled, new { status = r.Status.ToString() });
}

// ---------- 10. PKCE: 32 B CSPRNG, S256, swiezy per operacja ----------
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    bool allS256 = true;
    for (int i = 0; i < 200; i++)
    {
        var (v, ch) = SonosLoginPkce.Create();
        seen.Add(v);
        if (v.Length != 43 || ch != SonosLoginPkce.CreateChallenge(v)) allS256 = false;
    }
    Check("S7-pkce", "32 B CSPRNG, 43 znaki base64url, S256, osobna para per operacja",
        seen.Count == 200 && allS256 && SonosLoginPkce.VerifierEntropyBytes == 32,
        new { unikalne = seen.Count, entropia_b = SonosLoginPkce.VerifierEntropyBytes, s256_zgodne = allS256 });

    // Swiezosc per operacja mierzona na StartAsync (dwa wywolania, dwa verifiery).
    var now = DateTimeOffset.UtcNow;
    var body = StartBody("https://api.sonos.com/login/v3/oauth?client_id=synthetic&state=abc");
    var capture = new ScriptedHandler(HttpStatusCode.OK, body) { CaptureBodies = true };
    using var c = new SonosLoginClient(cfg, capture, TimeSpan.FromSeconds(5), () => now);
    var s1 = await c.StartAsync(CancellationToken.None);
    var s2 = await c.StartAsync(CancellationToken.None);
    var challenges = capture.Bodies
        .Select(b => JsonDocument.Parse(b).RootElement.GetProperty("code_challenge").GetString())
        .ToList();
    Check("S8-start-swiezy-verifier", "kazdy start wysyla nowe wyzwanie S256 i nie wysyla verifiera",
        s1.Status == SonosLoginStatus.Success && s2.Status == SonosLoginStatus.Success
        && challenges.Count == 2 && challenges[0] != challenges[1]
        && capture.Bodies.All(b => !b.Contains("code_verifier", StringComparison.Ordinal))
        && capture.Bodies.All(b => b.Contains("\"code_challenge_method\":\"S256\"", StringComparison.Ordinal)),
        new
        {
            start1 = s1.Status.ToString(),
            start2 = s2.Status.ToString(),
            wyzwania_rozne = challenges.Count == 2 && challenges[0] != challenges[1],
            verifier_w_ciele_start = capture.Bodies.Any(b => b.Contains("code_verifier", StringComparison.Ordinal))
        });
}

// ---------- 11. Polityka authorize_url ----------
{
    var now = DateTimeOffset.UtcNow;
    async Task<SonosLoginStatus> Start(string url)
    {
        using var c = new SonosLoginClient(cfg, new ScriptedHandler(HttpStatusCode.OK, StartBody(url)),
            TimeSpan.FromSeconds(5), () => now);
        return (await c.StartAsync(CancellationToken.None)).Status;
    }

    var good = await Start("https://api.sonos.com/login/v3/oauth?client_id=synthetic");
    var evilHost = await Start("https://api.sonos.com.zly.invalid.test/login/v3/oauth?a=1");
    var evilPath = await Start("https://api.sonos.com/phishing/oauth?a=1");
    var plainHttp = await Start("http://api.sonos.com/login/v3/oauth?a=1");
    var userInfo = await Start("https://user@api.sonos.com/login/v3/oauth?a=1");

    Check("S9-authorize-url", "authorize_url weryfikowany po hoscie I sciezce Sonos, nie po prefiksie https",
        good == SonosLoginStatus.Success && evilHost == SonosLoginStatus.InvalidResponse
        && evilPath == SonosLoginStatus.InvalidResponse && plainHttp == SonosLoginStatus.InvalidResponse
        && userInfo == SonosLoginStatus.InvalidResponse,
        new { poprawny = good.ToString(), obcy_host = evilHost.ToString(), obca_sciezka = evilPath.ToString(), http = plainHttp.ToString(), userinfo = userInfo.ToString() });
}

// ---------- 12. Origin brokera: tylko HTTPS ----------
{
    var cases = new (string Origin, bool Expected)[]
    {
        ("https://broker.invalid.test", true),
        ("http://broker.invalid.test", false),
        ("https://user:pass@broker.invalid.test", false),
        ("https://broker.invalid.test/?a=1", false),
        ("ws://broker.invalid.test", false),
        ("", false),
    };
    var bad = cases.Where(t => SonosLoginBrokerConfiguration.TryCreate(t.Origin, out _) != t.Expected).ToList();
    Check("S10-origin-brokera", "zaufany origin brokera wymaga HTTPS, bez userinfo/query",
        bad.Count == 0, new { niezgodne = bad.Select(b => b.Origin).ToArray() });
}

// ---------- 13. Diagnostyka sesji i wynikow ----------
{
    var now = DateTimeOffset.UtcNow;
    var session = FreshSession(now, 300);
    var sessionText = session.ToString();
    var startText = (await new SonosLoginClient(cfg,
        new ScriptedHandler(HttpStatusCode.TooManyRequests, "{\"error\":\"too_many_sessions\"}"),
        TimeSpan.FromSeconds(5), () => now).StartAsync(CancellationToken.None)).ToString();
    bool leaks = sessionText.Contains(session.SessionId, StringComparison.Ordinal)
        || sessionText.Contains("client_id", StringComparison.Ordinal)
        || sessionText.Contains("?", StringComparison.Ordinal);
    Check("S11-diagnostyka-sesji", "ToString sesji nie ujawnia session_id, verifiera ani query z client_id",
        !leaks, new { sesja = sessionText, start = startText });
}

var json = JsonSerializer.Serialize(new { failures, cases = report },
    new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
Console.WriteLine(failures == 0 ? "PROBE_RESULT=SPEC_PASS" : $"PROBE_RESULT=SPEC_FAIL ({failures} niespelnionych oczekiwan)");
return failures == 0 ? 0 : 1;

// ================= pomocnicze =================
sealed class ScriptedHandler : HttpMessageHandler
{
    private readonly HttpStatusCode status;
    private readonly byte[] body;
    private readonly int bodyDelayMs;
    private readonly string? location;
    private readonly Uri? finalUri;

    public ScriptedHandler(HttpStatusCode status, string body, int bodyDelayMs = 0,
        string? location = null, Uri? finalUri = null)
    {
        this.status = status;
        this.body = Encoding.UTF8.GetBytes(body);
        this.bodyDelayMs = bodyDelayMs;
        this.location = location;
        this.finalUri = finalUri;
    }

    public bool CaptureBodies { get; set; }
    public List<string> Bodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (CaptureBodies && request.Content is not null)
        {
            Bodies.Add(await request.Content.ReadAsStringAsync(ct));
        }

        var response = new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new StreamContent(new DelayedStream(body, bodyDelayMs))
        };
        response.Content.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        if (location is not null) response.Headers.TryAddWithoutValidation("Location", location);
        if (finalUri is not null) response.RequestMessage = new HttpRequestMessage(HttpMethod.Post, finalUri);
        return response;
    }
}

sealed class DelayedStream : Stream
{
    private readonly byte[] data;
    private readonly int delayMs;
    private int position;
    private bool delayed;

    public DelayedStream(byte[] data, int delayMs) { this.data = data; this.delayMs = delayMs; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!delayed)
        {
            delayed = true;
            if (delayMs > 0) await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        if (position >= data.Length) return 0;
        var count = Math.Min(buffer.Length, data.Length - position);
        data.AsMemory(position, count).CopyTo(buffer);
        position += count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(new Memory<byte>(buffer, offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;
    public override long Position { get => position; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
