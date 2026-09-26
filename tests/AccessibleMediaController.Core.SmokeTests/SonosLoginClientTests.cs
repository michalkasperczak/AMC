using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// Pierwszy przyrost klienta logowania Sonos. WSZYSTKIE dane tutaj sa jawnie
/// SYNTETYCZNE, a caly ruch idzie przez atrape HttpMessageHandler - zaden test
/// nie dotyka prawdziwego brokera, konta Sonos ani sieci.
///
/// Weryfikowane granice: PKCE wg kontraktu backendu, jeden zaufany origin,
/// polityka authorize_url na rzeczywistym hoscie/sciezce Sonos, fail-closed przy
/// przekierowaniu, rozdzielne statusy (pending/odmowa/wygasniecie/429/503/brak
/// serwera/anulowanie/sukces), skonczony limit odpowiedzi i brak echa ciala oraz
/// tokenu w komunikatach.
/// </summary>
internal static class SonosLoginClientTests
{
    private const string BrokerOrigin = "https://broker-testowy.invalid";
    private const string SekretMarker = "SEKRET-ATAKUJACEGO-4f2a9c";

    public static void Run()
    {
        TestPkceZgodnyZKontraktemBrokera();
        TestKazdaOperacjaMaWlasnyVerifier();
        TestZaufanyOriginTylkoHttps();
        TestPolitykaAdresuAutoryzacji();
        TestSciezkaPozytywnaStartu();
        TestStartWysylaS256NaZaufanyOrigin();
        TestStartOdrzucaObcyAuthorizeUrl();
        TestStartRateLimitIBrakKonfiguracji();
        TestSciezkaPozytywnaWyniku();
        TestWynikPendingPrzedCzasem();
        TestWynikWygasnieciePoCzasie();
        TestWynikPoprawnaOdmowaNieDajeTokenu();
        TestWynikVerifierMismatchToOdmowa();
        TestWynik429I503();
        TestBrakSerweraToOsobnyStatus();
        TestAnulowanieNieWysylaIProwadziDoStatusuCanceled();
        TestPrzekierowanieFailClosed();
        TestZlyJsonIBrakPolNieEchujaCiala();
        TestOversizeOdrzuconyBezObcinania();
        TestSekretWZlosliwejOdpowiedziNieWyciekaDoKomunikatu();
        TestTokenyNieWypisujaSieWToString();
        TestBrakAutomatycznychPonowienJednorazowegoOdbioru();
        TestMutacyjnyKontrolnyOdmowaKontraToken();
        TestNullowePolaOpcjonalneWgKontraktuBackendu();
        Console.WriteLine("Sonos: 24 testy klienta pierwszego logowania zaliczone.");
    }

    // ---------------- PKCE ----------------
    private static void TestPkceZgodnyZKontraktemBrokera()
    {
        // Wektor kontrolny RFC 7636 (jawnie publiczny, syntetyczny).
        Assert(
            SonosLoginPkce.CreateChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")
                == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            "S256 klienta musi zgadzac sie z wektorem kontrolnym PKCE");

        var (verifier, challenge) = SonosLoginPkce.Create();
        Assert(verifier.Length == 43, "32 bajty CSPRNG daja 43 znaki base64url");
        Assert(challenge.Length == SonosLoginPkce.ChallengeLength, "wyzwanie ma 43 znaki");
        Assert(MatchesBrokerRegex(challenge), "wyzwanie musi przejsc _S256_RE brokera");
        Assert(MatchesBrokerRegex(verifier), "verifier miesci sie w 43..128 znakow kontraktu");
        Assert(SonosLoginPkce.CreateChallenge(verifier) == challenge, "wyzwanie wynika z verifiera");
        Assert(!SonosLoginPkce.LooksLikeS256Field("krotkie"), "za krotkie pole odrzucone");
        Assert(!SonosLoginPkce.LooksLikeS256Field(new string('=', 43)), "niedozwolone znaki odrzucone");
    }

    private static void TestKazdaOperacjaMaWlasnyVerifier()
    {
        var widziane = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < 64; index++)
        {
            Assert(widziane.Add(SonosLoginPkce.CreateVerifier()), "verifier nie moze sie powtarzac");
        }

        var broker = Konfiguracja();
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-a"));
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-b"));
        using var client = new SonosLoginClient(broker, handler);
        var pierwszy = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        var drugi = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(pierwszy.Succeeded && drugi.Succeeded, "dwa niezalezne starty");
        var wyzwaniaWZadaniach = handler.Requests
            .Select(request => ReadJsonString(request.Body, "code_challenge"))
            .ToArray();
        Assert(
            wyzwaniaWZadaniach[0] != wyzwaniaWZadaniach[1],
            "kazda operacja startu wysyla WLASNE wyzwanie PKCE");
    }

    // ---------------- origin i adresy ----------------
    private static void TestZaufanyOriginTylkoHttps()
    {
        Assert(SonosLoginBrokerConfiguration.TryCreate(BrokerOrigin, out var dobry) && dobry is not null,
            "poprawny origin https przyjety");
        Assert(dobry!.StartUri.AbsoluteUri == BrokerOrigin + "/login/start", "sciezka start wg kontraktu");
        Assert(dobry.ResultUri.AbsoluteUri == BrokerOrigin + "/login/result", "sciezka result wg kontraktu");
        Assert(!SonosLoginBrokerConfiguration.TryCreate("http://broker-testowy.invalid", out _),
            "http nie jest zaufanym originem");
        Assert(!SonosLoginBrokerConfiguration.TryCreate("https://user:haslo@broker-testowy.invalid", out _),
            "origin z danymi logowania odrzucony");
        Assert(!SonosLoginBrokerConfiguration.TryCreate("https://broker-testowy.invalid/?a=1", out _),
            "origin z query odrzucony");
        Assert(!SonosLoginBrokerConfiguration.TryCreate("   ", out _), "pusty origin odrzucony");
        Assert(!dobry.IsSameOrigin(new Uri("https://obcy.invalid/login/result")), "obcy host to inny origin");
        Assert(!dobry.IsSameOrigin(new Uri("https://broker-testowy.invalid:8443/login/result")),
            "inny port to inny origin");
        Assert(dobry.IsSameOrigin(dobry.ResultUri), "wlasny adres wyniku jest tym samym originem");
    }

    private static void TestPolitykaAdresuAutoryzacji()
    {
        // Host i sciezka wprost z backendu: SONOS_AUTHORIZE_URL w core.py.
        Assert(SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl(
                "https://api.sonos.com/login/v3/oauth?client_id=x&response_type=code"),
            "rzeczywisty adres autoryzacji Sonos przyjety");
        Assert(!SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl("https://zlosliwy.invalid/login/v3/oauth"),
            "obcy host odrzucony mimo poprawnej sciezki");
        Assert(!SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl("https://api.sonos.com/zle/miejsce"),
            "wlasciwy host, zla sciezka - odrzucone");
        Assert(!SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl("http://api.sonos.com/login/v3/oauth"),
            "http odrzucony");
        Assert(!SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl(
                "https://api.sonos.com.zlosliwy.invalid/login/v3/oauth"),
            "sufiksowany host odrzucony");
        Assert(!SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl("https://api.sonos.com:8443/login/v3/oauth"),
            "niestandardowy port odrzucony");
        Assert(!SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl("nie-adres"), "nie-adres odrzucony");
    }

    // ---------------- start ----------------
    private static void TestSciezkaPozytywnaStartu()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-pozytywna", expiresIn: 600));
        using var client = new SonosLoginClient(Konfiguracja(), handler, clock: () => Zegar);
        var wynik = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.Success, "start konczy sie sukcesem");
        Assert(wynik.Session is not null, "sesja zwrocona");
        Assert(wynik.Session!.AuthorizeUri.Host == "api.sonos.com", "adres autoryzacji z brokera");
        Assert(wynik.Session.ExpiresAtUtc == Zegar.AddSeconds(600), "expires_in przeliczony na czas absolutny");
        Assert(!wynik.Session.IsExpired(Zegar.AddSeconds(599)), "sesja jeszcze zywa");
        Assert(wynik.Session.IsExpired(Zegar.AddSeconds(600)), "sesja wygasla po expires_in");
        Assert(!wynik.Session.ToString().Contains(wynik.Session.SessionId, StringComparison.Ordinal),
            "ToString sesji nie wypisuje identyfikatora");
    }

    private static void TestStartWysylaS256NaZaufanyOrigin()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-origin"));
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        var request = handler.Requests.Single();
        Assert(request.Uri.AbsoluteUri == BrokerOrigin + "/login/start", "start idzie na zaufany adres");
        Assert(ReadJsonString(request.Body, "code_challenge_method") == "S256",
            "metoda wyzwania musi byc S256 wg kontraktu");
        Assert(MatchesBrokerRegex(ReadJsonString(request.Body, "code_challenge")!),
            "wyzwanie w zadaniu spelnia _S256_RE brokera");
        Assert(!request.Body.Contains("code_verifier", StringComparison.Ordinal),
            "start NIE wysyla verifiera");
        Assert(!request.Body.Contains("secret", StringComparison.OrdinalIgnoreCase),
            "klient nie ma i nie wysyla zadnego sekretu aplikacji");
    }

    private static void TestStartOdrzucaObcyAuthorizeUrl()
    {
        foreach (var zlyAdres in new[]
                 {
                     "https://phishing.invalid/login/v3/oauth",
                     "http://api.sonos.com/login/v3/oauth",
                     "https://api.sonos.com/inna/sciezka"
                 })
        {
            var handler = new FakeBrokerHandler();
            handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-zla", authorizeUrl: zlyAdres));
            using var client = new SonosLoginClient(Konfiguracja(), handler);
            var wynik = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(wynik.Status == SonosLoginStatus.InvalidResponse,
                "adres autoryzacji poza protokolem Sonos musi dac InvalidResponse: " + zlyAdres);
            Assert(wynik.Session is null, "nie wolno wydac sesji z obcym adresem autoryzacji");
        }
    }

    private static void TestStartRateLimitIBrakKonfiguracji()
    {
        Assert(StartStatus(HttpStatusCode.TooManyRequests, "{\"error\": \"too_many_sessions\"}")
            == SonosLoginStatus.RateLimited, "429 to RateLimited");
        Assert(StartStatus(HttpStatusCode.ServiceUnavailable, "{\"error\": \"server_not_configured\"}")
            == SonosLoginStatus.BrokerNotConfigured, "503 server_not_configured rozpoznane osobno");
        Assert(StartStatus(HttpStatusCode.ServiceUnavailable, "{\"error\": \"server_busy\"}")
            == SonosLoginStatus.BrokerError, "inne 503 to BrokerError");
        Assert(StartStatus(HttpStatusCode.BadRequest, "{\"error\": \"invalid_code_challenge\"}")
            == SonosLoginStatus.InvalidResponse, "400 przy starcie oznacza niezgodnosc kontraktu");
    }

    // ---------------- wynik ----------------
    private static void TestSciezkaPozytywnaWyniku()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-tokeny"));
        handler.Enqueue(HttpStatusCode.OK, """
            {"access_token": "SYNTETYCZNY-ACCESS", "token_type": "Bearer",
             "expires_in": 86400, "refresh_token": "SYNTETYCZNY-REFRESH",
             "scope": "playback-control-all"}
            """);
        using var client = new SonosLoginClient(Konfiguracja(), handler, clock: () => Zegar);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session!;
        var wynik = client.FetchResultAsync(sesja, CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.Success, "odbior wyniku konczy sie sukcesem");
        Assert(wynik.Tokens!.AccessToken == "SYNTETYCZNY-ACCESS", "pole access_token wg kontraktu");
        Assert(wynik.Tokens.TokenType == "Bearer", "pole token_type wg kontraktu");
        Assert(wynik.Tokens.ExpiresInSeconds == 86400, "pole expires_in wg kontraktu");
        Assert(wynik.Tokens.RefreshToken == "SYNTETYCZNY-REFRESH", "pole refresh_token wg kontraktu");
        Assert(wynik.Tokens.Scope == "playback-control-all", "pole scope wg kontraktu");

        var zadanie = handler.Requests[1];
        Assert(zadanie.Uri.AbsoluteUri == BrokerOrigin + "/login/result", "wynik idzie na zaufany adres");
        Assert(ReadJsonString(zadanie.Body, "session_id") == sesja.SessionId, "session_id z /login/start");
        var wyslanyVerifier = ReadJsonString(zadanie.Body, "code_verifier")!;
        Assert(MatchesBrokerRegex(wyslanyVerifier), "verifier zgodny z kontraktem 43..128");
        Assert(SonosLoginPkce.CreateChallenge(wyslanyVerifier)
                == ReadJsonString(handler.Requests[0].Body, "code_challenge"),
            "wyslany verifier odpowiada wyzwaniu z tej samej operacji");
    }

    private static void TestWynikPendingPrzedCzasem()
    {
        var wynik = WynikPo(HttpStatusCode.NotFound, "{\"error\": \"unknown_session\"}", zegarPrzesuniecieSekund: 0);
        Assert(wynik.Status == SonosLoginStatus.Pending, "404 w zywej sesji to Pending, nie blad");
        Assert(wynik.Tokens is null, "Pending nie daje tokenow");
    }

    private static void TestWynikWygasnieciePoCzasie()
    {
        var wynik = WynikPo(HttpStatusCode.NotFound, "{\"error\": \"unknown_session\"}", zegarPrzesuniecieSekund: 601);
        Assert(wynik.Status == SonosLoginStatus.Expired, "404 po expires_in to Expired, nie Pending");
        Assert(wynik.Tokens is null, "Expired nie daje tokenow");
    }

    private static void TestWynikPoprawnaOdmowaNieDajeTokenu()
    {
        foreach (var kod in new[] { "access_denied", "invalid_callback", "token_exchange_failed", "login_failed" })
        {
            var wynik = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"" + kod + "\"}");
            Assert(wynik.Status == SonosLoginStatus.Denied, "rozpoznana odmowa " + kod + " to Denied");
            Assert(wynik.Tokens is null, "odmowa NIGDY nie moze nosic tokenu: " + kod);
            Assert(!wynik.Succeeded, "odmowa nie jest sukcesem: " + kod);
        }

        var nierozpoznane = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"invalid_code_verifier\"}");
        Assert(nierozpoznane.Status == SonosLoginStatus.InvalidResponse,
            "400 bez rozpoznanego kodu odmowy to niezgodnosc, nie odmowa uzytkownika");
    }

    private static void TestWynikVerifierMismatchToOdmowa()
    {
        var wynik = WynikPo(HttpStatusCode.Forbidden, "{\"error\": \"verifier_mismatch\"}");
        Assert(wynik.Status == SonosLoginStatus.Denied, "403 verifier_mismatch to odmowa wydania tokenu");
        Assert(wynik.Tokens is null, "403 nie daje tokenow");
    }

    private static void TestWynik429I503()
    {
        Assert(WynikPo(HttpStatusCode.TooManyRequests, "{\"error\": \"too_many_sessions\"}").Status
            == SonosLoginStatus.RateLimited, "429 przy odbiorze to RateLimited");
        Assert(WynikPo(HttpStatusCode.ServiceUnavailable, "{\"error\": \"server_not_configured\"}").Status
            == SonosLoginStatus.BrokerNotConfigured, "503 server_not_configured przy odbiorze");
        Assert(WynikPo(HttpStatusCode.ServiceUnavailable, "{\"error\": \"server_busy\"}").Status
            == SonosLoginStatus.BrokerError, "503 server_busy to BrokerError");
        Assert(WynikPo(HttpStatusCode.InternalServerError, "{\"error\": \"internal_error\"}").Status
            == SonosLoginStatus.BrokerError, "500 to BrokerError, nie odmowa");
    }

    private static void TestBrakSerweraToOsobnyStatus()
    {
        var handler = new FakeBrokerHandler();
        handler.EnqueueThrow(new HttpRequestException("brak trasy do hosta (syntetyczne)"));
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        var wynik = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.BrokerUnreachable, "brak serwera to osobny status");
        Assert(!wynik.Message.Contains("syntetyczne", StringComparison.Ordinal),
            "komunikat nie cytuje tresci wyjatku transportu");

        var timeoutHandler = new FakeBrokerHandler();
        timeoutHandler.EnqueueThrow(new TaskCanceledException("timeout (syntetyczny)"));
        using var timeoutClient = new SonosLoginClient(Konfiguracja(), timeoutHandler);
        Assert(timeoutClient.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Status
            == SonosLoginStatus.BrokerUnreachable,
            "skonczony timeout bez anulowania przez wolajacego to BrokerUnreachable, nie Canceled");
    }

    private static void TestAnulowanieNieWysylaIProwadziDoStatusuCanceled()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("nie-powinno-pojsc"));
        using var anulowany = new CancellationTokenSource();
        anulowany.Cancel();
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        var wynik = client.StartAsync(anulowany.Token).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.Canceled, "anulowanie przed wyslaniem daje Canceled");
        Assert(handler.Requests.Count == 0, "anulowany start nie wysyla zadania");

        // Anulowanie W TRAKCIE wysylki: token musi byc propagowany do transportu.
        var wTrakcie = new FakeBrokerHandler();
        using var zrodlo = new CancellationTokenSource();
        wTrakcie.EnqueueCancelDuringSend(zrodlo);
        using var drugiKlient = new SonosLoginClient(Konfiguracja(), wTrakcie);
        var drugi = drugiKlient.StartAsync(zrodlo.Token).GetAwaiter().GetResult();
        Assert(drugi.Status == SonosLoginStatus.Canceled,
            "anulowanie w trakcie wysylki musi dac Canceled - token jest propagowany");
        Assert(wTrakcie.SeenCancellationToken, "transport otrzymal token anulowania wolajacego");
    }

    private static void TestPrzekierowanieFailClosed()
    {
        foreach (var status in new[] { HttpStatusCode.Found, HttpStatusCode.MovedPermanently,
                                       HttpStatusCode.TemporaryRedirect, HttpStatusCode.PermanentRedirect })
        {
            var handler = new FakeBrokerHandler();
            handler.EnqueueRedirect(status, "https://zlosliwy.invalid/login/result");
            using var client = new SonosLoginClient(Konfiguracja(), handler);
            var wynik = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(wynik.Status == SonosLoginStatus.RedirectRefused,
                "przekierowanie " + status + " musi byc fail-closed");
            Assert(handler.Requests.All(request => request.Uri.Host == "broker-testowy.invalid"),
                "zaden payload nie poszedl na obcy host przy " + status);
            Assert(handler.Requests.All(request =>
                    !request.Body.Contains("code_verifier", StringComparison.Ordinal)),
                "przy starcie i tak nie ma verifiera, ale sprawdzamy to jawnie");
        }

        // Gdyby transport MIMO WSZYSTKO skonczyl na obcym adresie, wynik jest odrzucony.
        var podmieniony = new FakeBrokerHandler { RewriteFinalUriTo = new Uri("https://zlosliwy.invalid/login/result") };
        podmieniony.Enqueue(HttpStatusCode.OK, StartBody("sesja-podmieniona"));
        using var drugiKlient = new SonosLoginClient(Konfiguracja(), podmieniony);
        Assert(drugiKlient.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Status
            == SonosLoginStatus.RedirectRefused,
            "odpowiedz z obcym adresem koncowym jest odrzucana, a nie parsowana");
    }

    private static void TestZlyJsonIBrakPolNieEchujaCiala()
    {
        var zleCiala = new[]
        {
            "to nie jest JSON",
            "[]",
            "null",
            "{",
            "{\"session_id\": \"" + new string('a', 43) + "\"}",
            "{\"authorize_url\": \"https://api.sonos.com/login/v3/oauth\"}",
            "{\"session_id\": \"krotkie\", \"authorize_url\": \"https://api.sonos.com/login/v3/oauth\", \"expires_in\": 600}",
            "{\"session_id\": \"" + new string('b', 43) + "\", \"authorize_url\": \"https://api.sonos.com/login/v3/oauth\", \"expires_in\": 0}",
            "{\"session_id\": \"" + new string('c', 43) + "\", \"authorize_url\": \"https://api.sonos.com/login/v3/oauth\", \"expires_in\": \"600\"}"
        };
        foreach (var cialo in zleCiala)
        {
            var handler = new FakeBrokerHandler();
            handler.Enqueue(HttpStatusCode.OK, cialo);
            using var client = new SonosLoginClient(Konfiguracja(), handler);
            var wynik = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert(wynik.Status == SonosLoginStatus.InvalidResponse,
                "niezgodne cialo musi dac staly, bezpieczny blad: " + cialo);
            Assert(wynik.Session is null, "niezgodne cialo nie tworzy sesji");
            Assert(!wynik.Message.Contains("JSON", StringComparison.OrdinalIgnoreCase)
                   || !wynik.Message.Contains(cialo, StringComparison.Ordinal),
                "komunikat nie cytuje surowego ciala");
        }

        // Brak access_token w odpowiedzi 200 przy odbiorze wyniku.
        var brakTokenu = WynikPo(HttpStatusCode.OK, "{\"token_type\": \"Bearer\", \"expires_in\": 3600}");
        Assert(brakTokenu.Status == SonosLoginStatus.InvalidResponse, "200 bez access_token to niezgodnosc");
        Assert(brakTokenu.Tokens is null, "brak access_token nie tworzy modelu tokenow");
    }

    private static void TestOversizeOdrzuconyBezObcinania()
    {
        var wielkieCialo = "{\"access_token\": \"" + new string('T', SonosLoginClient.MaxResponseBytes + 4096) + "\"}";
        var wynik = WynikPo(HttpStatusCode.OK, wielkieCialo);
        Assert(wynik.Status == SonosLoginStatus.InvalidResponse,
            "odpowiedz ponad limit jest ODRZUCANA, nie obcinana do poprawnego JSON");
        Assert(wynik.Tokens is null, "z odpowiedzi ponad limit nie powstaje token");

        // Klamliwy Content-Length ponad limit tez zatrzymuje odczyt.
        var handler = new FakeBrokerHandler { DeclaredContentLength = SonosLoginClient.MaxResponseBytes + 1 };
        handler.Enqueue(HttpStatusCode.OK, "{\"access_token\": \"male\"}");
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        Assert(client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Status
            == SonosLoginStatus.InvalidResponse, "zadeklarowana dlugosc ponad limit zatrzymuje odczyt");
    }

    private static void TestSekretWZlosliwejOdpowiedziNieWyciekaDoKomunikatu()
    {
        var zlosliweCiala = new[]
        {
            "{\"error\": \"" + SekretMarker + "\"}",
            "{\"error\": \"access_denied\", \"detail\": \"" + SekretMarker + "\"}",
            "{\"error_description\": \"" + SekretMarker + "\", \"access_token\": \"" + SekretMarker + "\"}",
            SekretMarker
        };
        foreach (var cialo in zlosliweCiala)
        {
            foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.ServiceUnavailable })
            {
                var wynik = WynikPo(status, cialo);
                Assert(!wynik.Message.Contains(SekretMarker, StringComparison.Ordinal),
                    "komunikat nie moze zawierac markera z odpowiedzi atakujacego");
                Assert(!wynik.ToString().Contains(SekretMarker, StringComparison.Ordinal),
                    "ToString wyniku nie moze zawierac markera z odpowiedzi atakujacego");
                Assert(wynik.Tokens is null, "zlosliwa odpowiedz bledu nie tworzy tokenow");
            }
        }

        var startWynik = StartOutcome(HttpStatusCode.ServiceUnavailable, "{\"error\": \"" + SekretMarker + "\"}");
        Assert(!startWynik.Message.Contains(SekretMarker, StringComparison.Ordinal),
            "komunikat startu nie cytuje markera");
        Assert(!startWynik.ToString().Contains(SekretMarker, StringComparison.Ordinal),
            "ToString startu nie cytuje markera");
        Assert(startWynik.Status == SonosLoginStatus.BrokerError,
            "nierozpoznany kod bledu 503 nie awansuje na rozpoznana przyczyne");
    }

    private static void TestTokenyNieWypisujaSieWToString()
    {
        var tokeny = new SonosTokens("ACCESS-SYNTETYCZNY-9", "Bearer", 3600, "REFRESH-SYNTETYCZNY-9", "playback-control-all");
        var opis = tokeny.ToString();
        Assert(!opis.Contains("ACCESS-SYNTETYCZNY-9", StringComparison.Ordinal), "ToString nie wypisuje access_token");
        Assert(!opis.Contains("REFRESH-SYNTETYCZNY-9", StringComparison.Ordinal), "ToString nie wypisuje refresh_token");
        Assert(opis.Contains("Bearer", StringComparison.Ordinal), "ToString moze podac rodzaj tokenu");
        Assert(tokeny.HasRefreshToken, "obecnosc tokenu odswiezania jest widoczna bez jego wartosci");

        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-serializacja"));
        handler.Enqueue(HttpStatusCode.OK,
            "{\"access_token\": \"ACCESS-SYNTETYCZNY-9\", \"token_type\": \"Bearer\", \"expires_in\": 3600}");
        using var client = new SonosLoginClient(Konfiguracja(), handler, clock: () => Zegar);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session!;
        var wynik = client.FetchResultAsync(sesja, CancellationToken.None).GetAwaiter().GetResult();
        Assert(!wynik.ToString().Contains("ACCESS-SYNTETYCZNY-9", StringComparison.Ordinal),
            "opis wyniku nie wypisuje tokenu");
        Assert(!sesja.ToString().Contains(ReadJsonString(handler.Requests[1].Body, "code_verifier")!,
                StringComparison.Ordinal),
            "opis sesji nie wypisuje verifiera");
        var serializowanaSesja = JsonSerializer.Serialize(sesja);
        Assert(!serializowanaSesja.Contains("Verifier", StringComparison.OrdinalIgnoreCase),
            "verifier nie jest publiczna wlasnoscia, wiec nie trafi do state.json");
    }

    private static void TestBrakAutomatycznychPonowienJednorazowegoOdbioru()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-jednorazowa"));
        handler.Enqueue(HttpStatusCode.InternalServerError, "{\"error\": \"internal_error\"}");
        using var client = new SonosLoginClient(Konfiguracja(), handler, clock: () => Zegar);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session!;
        var wynik = client.FetchResultAsync(sesja, CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.BrokerError, "niejednoznaczny blad 5xx zwrocony wprost");
        Assert(handler.Requests.Count == 2,
            "jednorazowy odbior NIE moze byc automatycznie ponawiany po niejednoznacznym bledzie");
    }

    /// <summary>
    /// Kontrolowana MUTACJA: potwierdza, ze asercja odmowy faktycznie rozroznia
    /// blad. Mutacja zyje tylko w tym tescie (atrapa oddaje token przy 400) i nie
    /// zmienia finalnego kodu klienta.
    /// </summary>
    private static void TestMutacyjnyKontrolnyOdmowaKontraToken()
    {
        var poprawny = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"access_denied\"}");
        Assert(poprawny.Status == SonosLoginStatus.Denied && poprawny.Tokens is null,
            "kontrola pozytywna: poprawna odmowa");

        // Zlosliwy broker dokleja token do ciala odmowy 400. Klient NADAL nie moze
        // uznac tego za sukces ani wystawic tokenu.
        var zmutowany = WynikPo(
            HttpStatusCode.BadRequest,
            "{\"error\": \"access_denied\", \"access_token\": \"NIE-WOLNO-TEGO-UZNAC\"}");
        Assert(zmutowany.Status == SonosLoginStatus.Denied, "odmowa z doklejonym tokenem to nadal odmowa");
        Assert(zmutowany.Tokens is null, "HTTP 400 nie moze wydac tokenu, choc token jest w ciele");
        Assert(!zmutowany.ToString().Contains("NIE-WOLNO-TEGO-UZNAC", StringComparison.Ordinal),
            "doklejony token nie przecieka do opisu");

        // Dowod rozroznialnosci: ten sam token pod HTTP 200 DAJE sukces, wiec
        // asercja powyzej nie jest spelniona trywialnie.
        var podDwustoma = WynikPo(HttpStatusCode.OK, "{\"access_token\": \"NIE-WOLNO-TEGO-UZNAC\"}");
        Assert(podDwustoma.Status == SonosLoginStatus.Success && podDwustoma.Tokens is not null,
            "mutacja rozroznia: identyczne cialo pod 200 daje sukces, pod 400 nie");
    }

    /// <summary>
    /// Backend buduje tokeny przez doc.get(...), wiec expires_in, refresh_token i
    /// scope moga przyjsc jako JSON null. To nie jest blad kontraktu - tylko
    /// access_token jest wymagany (core.py sprawdza isinstance(..., str)).
    /// </summary>
    private static void TestNullowePolaOpcjonalneWgKontraktuBackendu()
    {
        var wynik = WynikPo(HttpStatusCode.OK, """
            {"access_token": "SYNTETYCZNY-ACCESS-NULL", "token_type": "Bearer",
             "expires_in": null, "refresh_token": null, "scope": null}
            """);
        Assert(wynik.Status == SonosLoginStatus.Success,
            "nullowe pola opcjonalne sa legalne wg kontraktu backendu");
        Assert(wynik.Tokens!.AccessToken == "SYNTETYCZNY-ACCESS-NULL", "access_token odczytany");
        Assert(wynik.Tokens.ExpiresInSeconds is null, "null expires_in to brak wartosci, nie zero");
        Assert(wynik.Tokens.RefreshToken is null, "null refresh_token to brak wartosci");
        Assert(!wynik.Tokens.HasRefreshToken, "brak tokenu odswiezania jest widoczny");
        Assert(wynik.Tokens.Scope is null, "null scope to brak wartosci");
        Assert(!wynik.Tokens.ToString().Contains("SYNTETYCZNY-ACCESS-NULL", StringComparison.Ordinal),
            "ToString nadal nie wypisuje tokenu");

        // Brakujace (nieobecne) pola opcjonalne zachowuja sie identycznie.
        var bezPol = WynikPo(HttpStatusCode.OK, "{\"access_token\": \"TYLKO-ACCESS\"}");
        Assert(bezPol.Status == SonosLoginStatus.Success, "sam access_token wystarcza wg kontraktu");
        Assert(bezPol.Tokens!.TokenType == "Bearer", "domyslny token_type Bearer jak w backendzie");
    }

    // ---------------- pomocnicze (syntetyczne) ----------------
    private static readonly DateTimeOffset Zegar = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static SonosLoginBrokerConfiguration Konfiguracja()
    {
        SonosLoginBrokerConfiguration.TryCreate(BrokerOrigin, out var configuration);
        return configuration ?? throw new InvalidOperationException("Syntetyczny origin nie przeszedl walidacji.");
    }

    private static string StartBody(
        string ziarno,
        string? authorizeUrl = null,
        int expiresIn = 600)
    {
        var sessionId = SonosLoginPkce.Base64UrlNoPadding(SHA256.HashData(Encoding.UTF8.GetBytes(ziarno)));
        var adres = authorizeUrl
            ?? "https://api.sonos.com/login/v3/oauth?client_id=syntetyczny&response_type=code&state="
               + new string('s', 43);
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = sessionId,
            ["authorize_url"] = adres,
            ["expires_in"] = expiresIn
        });
    }

    private static SonosLoginStatus StartStatus(HttpStatusCode status, string body) =>
        StartOutcome(status, body).Status;

    private static SonosLoginStartOutcome StartOutcome(HttpStatusCode status, string body)
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(status, body);
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        return client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private static SonosLoginResultOutcome WynikPo(
        HttpStatusCode status,
        string body,
        int zegarPrzesuniecieSekund = 0)
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-" + status + "-" + zegarPrzesuniecieSekund));
        handler.Enqueue(status, body);
        var teraz = Zegar;
        using var client = new SonosLoginClient(Konfiguracja(), handler, clock: () => teraz);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session
            ?? throw new InvalidOperationException("Atrapa startu nie wydala sesji.");
        teraz = Zegar.AddSeconds(zegarPrzesuniecieSekund);
        return client.FetchResultAsync(sesja, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static bool MatchesBrokerRegex(string value) =>
        value.Length is >= 43 and <= 128
        && value.All(character => character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '-'
            or '_');

    private static string? ReadJsonString(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(name, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Sonos: " + message);
        }
    }

    /// <summary>Atrapa transportu: zero sieci, pelna kontrola statusow i cial.</summary>
    private sealed class FakeBrokerHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> planned = new();

        public List<(Uri Uri, string Body)> Requests { get; } = new();

        public bool SeenCancellationToken { get; private set; }

        public Uri? RewriteFinalUriTo { get; init; }

        public long? DeclaredContentLength { get; init; }

        public void Enqueue(HttpStatusCode status, string body) =>
            planned.Enqueue(_ => Build(status, body));

        public void EnqueueThrow(Exception exception) =>
            planned.Enqueue(_ => throw exception);

        public void EnqueueRedirect(HttpStatusCode status, string location)
        {
            planned.Enqueue(_ =>
            {
                var response = Build(status, "{\"error\": \"moved\"}");
                response.Headers.Location = new Uri(location);
                return response;
            });
        }

        public void EnqueueCancelDuringSend(CancellationTokenSource source)
        {
            planned.Enqueue(_ =>
            {
                source.Cancel();
                source.Token.ThrowIfCancellationRequested();
                return Build(HttpStatusCode.OK, "{}");
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SeenCancellationToken |= cancellationToken.CanBeCanceled;
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add((request.RequestUri!, body));
            cancellationToken.ThrowIfCancellationRequested();
            if (planned.Count == 0)
            {
                throw new InvalidOperationException("Atrapa brokera nie miala zaplanowanej odpowiedzi.");
            }

            var response = planned.Dequeue()(request);
            response.RequestMessage = RewriteFinalUriTo is null
                ? request
                : new HttpRequestMessage(HttpMethod.Post, RewriteFinalUriTo);
            if (DeclaredContentLength is { } declared)
            {
                response.Content.Headers.ContentLength = declared;
            }

            return response;
        }

        private static HttpResponseMessage Build(HttpStatusCode status, string body) =>
            new(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
    }
}
