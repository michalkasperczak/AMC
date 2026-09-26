using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        UruchomPrzypadkiPoprawek();
        Console.WriteLine("Sonos: 35 testow klienta pierwszego logowania zaliczonych.");
    }

    /// <summary>
    /// Przypadki czterech zweryfikowanych blokad odbioru (B1 deadline calej
    /// operacji HTTP, B2 zakaz nieskonczonego budzetu, B3 bezpieczny token_type,
    /// B4 prawdziwa semantyka bledow). KAZDY przypadek jest lapany OSOBNO, zeby
    /// jedna awaria nie ukryla pozostalych.
    /// </summary>
    private static void UruchomPrzypadkiPoprawek()
    {
        var przypadki = new (string Nazwa, Action Test)[]
        {
            ("B1.1 zatrzymane cialo odpowiedzi przy starcie ma skonczony deadline", TestB1StartZatrzymaneCialo),
            ("B1.2 opoznione cialo przy odbiorze wyniku nie daje Success po deadline", TestB1WynikOpoznioneCialo),
            ("B1.3 szybka sciezka pozytywna z cialem strumieniowym nadal dziala", TestB1SzybkaKontrolaPozytywna),
            ("B1.4 anulowanie wolajacego w fazie ciala to Canceled, nie timeout", TestB1AnulowanieWFazieCiala),
            ("B1.5 saczone cialo ma LACZNY deadline, nie limit na odczyt", TestB1SaczoneCialoLacznyDeadline),
            ("B2 nieskonczony i niepoprawny budzet odrzucony przed zasobami", TestB2ZakazNieskonczonegoBudzetu),
            ("B3.1 token_type walidowany i kanonizowany, zly odrzucony bez echa", TestB3TokenTypeWalidowany),
            ("B3.2 model tokenow nie przyjmuje dowolnego typu do ToString", TestB3ModelNiePrzyjmujeDowolnegoTypu),
            ("B4.1 403 verifier_mismatch to blad lokalnego dowodu, nie odmowa Sonos", TestB4ProofMismatchOsobnyStatus),
            ("B4.2 400 invalid_callback/token_exchange_failed nie obwinia Sonos", TestB4CzterystaBezObwinianiaSonos),
            ("B4.3 gotowy wynik po lokalnym ExpiresAt nadal daje Success", TestB4GotowyWynikPoExpiresAt)
        };

        var bledy = new List<string>();
        foreach (var (nazwa, test) in przypadki)
        {
            try
            {
                test();
                Console.WriteLine("  [OK]   " + nazwa);
            }
            catch (Exception exception)
            {
                bledy.Add(nazwa + " => " + exception.Message);
                Console.WriteLine("  [FAIL] " + nazwa + " => " + exception.Message);
            }
        }

        if (bledy.Count > 0)
        {
            throw new InvalidOperationException(
                "Sonos: nieprzeszle przypadki poprawek: " + bledy.Count + "/" + przypadki.Length
                + Environment.NewLine + string.Join(Environment.NewLine, bledy));
        }
    }

    // ---------------- B1: skonczony deadline CALEJ operacji HTTP ----------------
    private static void TestB1StartZatrzymaneCialo()
    {
        var strumien = new SterowaneCialo(StartBody("sesja-zatrzymana"), TimeSpan.Zero, zawieszNaKoncu: true);
        var handler = new FakeBrokerHandler();
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(HttpStatusCode.OK, strumien));
        using var client = new SonosLoginClient(Konfiguracja(), handler, TimeSpan.FromMilliseconds(200));
        var zegar = Stopwatch.StartNew();
        var zadanie = Task.Run(() => client.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
        Assert(zadanie.Wait(TimeSpan.FromSeconds(5)),
            "start z zatrzymanym cialem MUSI wrocic w skonczonym czasie (deadline 200 ms)");
        zegar.Stop();
        Assert(zadanie.Result.Status == SonosLoginStatus.BrokerUnreachable,
            "zatrzymane cialo to bezpieczny blad transportu, dostano: " + zadanie.Result.Status);
        Assert(zadanie.Result.Session is null, "zatrzymane cialo nie tworzy sesji");
        Assert(zegar.Elapsed < TimeSpan.FromSeconds(3),
            "odczyt ciala musi zostac przerwany blisko deadline, minelo " + zegar.ElapsedMilliseconds + " ms");
        Assert(strumien.Zwolniony, "strumien odpowiedzi musi zostac zwolniony (ograniczone sprzatanie)");
    }

    private static void TestB1WynikOpoznioneCialo()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-opozniona"));
        var strumien = new SterowaneCialo(
            "{\"access_token\": \"NIE-WOLNO-PO-DEADLINE\", \"token_type\": \"Bearer\"}",
            TimeSpan.FromMilliseconds(1500),
            zawieszNaKoncu: false);
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(HttpStatusCode.OK, strumien));
        using var client = new SonosLoginClient(
            Konfiguracja(), handler, TimeSpan.FromMilliseconds(200), () => Zegar);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session!;
        var zegar = Stopwatch.StartNew();
        var zadanie = Task.Run(() => client.FetchResultAsync(sesja, CancellationToken.None).GetAwaiter().GetResult());
        Assert(zadanie.Wait(TimeSpan.FromSeconds(5)), "odbior wyniku musi wrocic w skonczonym czasie");
        zegar.Stop();
        Assert(zadanie.Result.Status == SonosLoginStatus.BrokerUnreachable,
            "cialo poza deadline to blad transportu, dostano: " + zadanie.Result.Status);
        Assert(zadanie.Result.Tokens is null, "token z ciala po deadline NIE moze zostac wydany");
        Assert(zegar.Elapsed < TimeSpan.FromSeconds(1),
            "deadline 200 ms nie moze czekac na cialo 1500 ms, minelo " + zegar.ElapsedMilliseconds + " ms");
    }

    private static void TestB1SzybkaKontrolaPozytywna()
    {
        var handler = new FakeBrokerHandler();
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(
            HttpStatusCode.OK,
            new SterowaneCialo(StartBody("sesja-szybka"), TimeSpan.FromMilliseconds(30), zawieszNaKoncu: false)));
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(
            HttpStatusCode.OK,
            new SterowaneCialo(
                "{\"access_token\": \"SYNTETYCZNY-SZYBKI\", \"token_type\": \"Bearer\", \"expires_in\": 3600}",
                TimeSpan.FromMilliseconds(30),
                zawieszNaKoncu: false)));
        using var client = new SonosLoginClient(
            Konfiguracja(), handler, TimeSpan.FromSeconds(5), () => Zegar);
        var start = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert(start.Succeeded, "skonczony deadline nie moze psuc normalnego startu");
        var wynik = client.FetchResultAsync(start.Session!, CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.Success, "normalny odbior w budzecie nadal daje Success");
        Assert(wynik.Tokens!.AccessToken == "SYNTETYCZNY-SZYBKI", "tokeny odczytane ze strumienia");
    }

    private static void TestB1AnulowanieWFazieCiala()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-anulowana-cialo"));
        var strumien = new SterowaneCialo(
            "{\"access_token\": \"NIE-WOLNO-PO-ANULOWANIU\"}",
            TimeSpan.FromMilliseconds(1500),
            zawieszNaKoncu: false);
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(HttpStatusCode.OK, strumien));
        using var client = new SonosLoginClient(
            Konfiguracja(), handler, TimeSpan.FromSeconds(10), () => Zegar);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session!;
        using var zrodlo = new CancellationTokenSource();
        zrodlo.CancelAfter(TimeSpan.FromMilliseconds(100));
        var zegar = Stopwatch.StartNew();
        var zadanie = Task.Run(() => client.FetchResultAsync(sesja, zrodlo.Token).GetAwaiter().GetResult());
        Assert(zadanie.Wait(TimeSpan.FromSeconds(5)), "anulowanie w fazie ciala musi zakonczyc operacje");
        zegar.Stop();
        Assert(zadanie.Result.Status == SonosLoginStatus.Canceled,
            "anulowanie wolajacego w fazie ciala to Canceled, nie timeout, dostano: " + zadanie.Result.Status);
        Assert(zadanie.Result.Tokens is null, "anulowanie nie wydaje tokenu");
        Assert(zegar.Elapsed < TimeSpan.FromSeconds(1),
            "anulowanie ma dzialac od razu, minelo " + zegar.ElapsedMilliseconds + " ms");
    }

    private static void TestB1SaczoneCialoLacznyDeadline()
    {
        // 60 ms na KAZDY odczyt: pojedynczy odczyt miesci sie w budzecie, ale
        // laczny czas nie. Limit nie moze byc resetowany co odczyt.
        var tresc = "{\"access_token\": \"" + new string('S', 80) + "\"}";
        var strumien = new SterowaneCialo(tresc, TimeSpan.FromMilliseconds(60), zawieszNaKoncu: false, bajtowNaOdczyt: 1);
        var handler = new FakeBrokerHandler();
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(HttpStatusCode.OK, strumien));
        using var client = new SonosLoginClient(Konfiguracja(), handler, TimeSpan.FromMilliseconds(400));
        var zegar = Stopwatch.StartNew();
        var zadanie = Task.Run(() => client.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
        Assert(zadanie.Wait(TimeSpan.FromSeconds(10)), "saczone cialo musi wrocic w skonczonym czasie");
        zegar.Stop();
        Assert(zadanie.Result.Status == SonosLoginStatus.BrokerUnreachable,
            "saczone cialo poza lacznym deadline to blad transportu, dostano: " + zadanie.Result.Status);
        Assert(zegar.Elapsed < TimeSpan.FromSeconds(2),
            "laczny deadline 400 ms, a nie limit na odczyt; minelo " + zegar.ElapsedMilliseconds + " ms");
        Assert(strumien.Odczyty < tresc.Length,
            "odczyt musi zostac przerwany przed przeczytaniem calego saczonego ciala, odczytow: " + strumien.Odczyty);
    }

    // ---------------- B2: zakaz nieskonczonego budzetu ----------------
    private static void TestB2ZakazNieskonczonegoBudzetu()
    {
        var zle = new (string Opis, TimeSpan Budzet)[]
        {
            ("Timeout.InfiniteTimeSpan", Timeout.InfiniteTimeSpan),
            ("-1 ms (rowne Infinite)", TimeSpan.FromMilliseconds(-1)),
            ("zero", TimeSpan.Zero),
            ("-2 ms", TimeSpan.FromMilliseconds(-2)),
            ("TimeSpan.MaxValue", TimeSpan.MaxValue),
            ("ponad zakres CTS/HttpClient", TimeSpan.FromMilliseconds((double)int.MaxValue + 1))
        };

        foreach (var (opis, budzet) in zle)
        {
            var handler = new FakeBrokerHandler();
            var zlapany = false;
            try
            {
                using var odrzucony = new SonosLoginClient(Konfiguracja(), handler, budzet);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                zlapany = true;
                Assert(exception.ParamName == "timeout",
                    "budzet ma byc odrzucony PRZED utworzeniem HttpClient (ParamName timeout, nie "
                    + exception.ParamName + "): " + opis);
            }

            Assert(zlapany, "niepoprawny budzet musi zostac odrzucony: " + opis);
            Assert(handler.Requests.Count == 0, "odrzucony budzet nie wysyla zadan: " + opis);
        }

        var dobryHandler = new FakeBrokerHandler();
        dobryHandler.Enqueue(HttpStatusCode.OK, StartBody("sesja-budzet-ok"));
        using var client = new SonosLoginClient(Konfiguracja(), dobryHandler, TimeSpan.FromMilliseconds(100));
        Assert(client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Succeeded,
            "poprawny skonczony budzet 100 ms nadal dziala");
    }

    // ---------------- B3: bezpieczny token_type ----------------
    private static void TestB3TokenTypeWalidowany()
    {
        foreach (var wariant in new[] { "Bearer", "bearer", "BEARER", " Bearer " })
        {
            var wynik = WynikPo(
                HttpStatusCode.OK,
                "{\"access_token\": \"SYNTETYCZNY-TT\", \"token_type\": \"" + wariant + "\"}");
            Assert(wynik.Status == SonosLoginStatus.Success, "Bearer w dowolnej wielkosci liter: " + wariant);
            Assert(wynik.Tokens!.TokenType == "Bearer",
                "typ tokenu kanonizowany do Bearer, dostano: " + wynik.Tokens.TokenType);
        }

        foreach (var zly in new[] { "MAC", SekretMarker, "Bearer " + SekretMarker, "" })
        {
            var wynik = WynikPo(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["access_token"] = "SYNTETYCZNY-TT-ZLY",
                    ["token_type"] = zly
                }));
            Assert(wynik.Status == SonosLoginStatus.InvalidResponse,
                "nieobslugiwany token_type to fail-closed, dostano: " + wynik.Status + " dla '" + zly + "'");
            Assert(wynik.Tokens is null, "nieobslugiwany token_type nie wydaje tokenow");
            Assert(!wynik.Message.Contains(SekretMarker, StringComparison.Ordinal)
                   && !wynik.ToString().Contains(SekretMarker, StringComparison.Ordinal),
                "typ tokenu z odpowiedzi NIE moze byc echem w komunikacie");
        }

        // Brak pola i JSON null zachowuja sie wg kontraktu backendu (domyslny Bearer).
        var brak = WynikPo(HttpStatusCode.OK, "{\"access_token\": \"TYLKO-ACCESS-TT\"}");
        Assert(brak.Status == SonosLoginStatus.Success && brak.Tokens!.TokenType == "Bearer",
            "brak token_type to domyslny Bearer wg kontraktu");
        var nullowy = WynikPo(HttpStatusCode.OK, "{\"access_token\": \"NULL-TT\", \"token_type\": null}");
        Assert(nullowy.Status == SonosLoginStatus.Success && nullowy.Tokens!.TokenType == "Bearer",
            "null token_type to domyslny Bearer wg kontraktu");
    }

    private static void TestB3ModelNiePrzyjmujeDowolnegoTypu()
    {
        var zlapany = false;
        try
        {
            var przeciek = new SonosTokens("ACCESS-SYNTETYCZNY-TT", SekretMarker, null, null, null);
            Assert(!przeciek.ToString().Contains(SekretMarker, StringComparison.Ordinal),
                "publiczny model NIE moze wypisywac dowolnego tekstu z konstruktora");
        }
        catch (ArgumentException exception)
        {
            zlapany = true;
            Assert(!exception.Message.Contains(SekretMarker, StringComparison.Ordinal),
                "komunikat wyjatku modelu nie cytuje wartosci z odpowiedzi");
        }

        Assert(zlapany, "model tokenow musi odrzucic nieobslugiwany typ tokenu");
        var poprawny = new SonosTokens("ACCESS-SYNTETYCZNY-TT", "bearer", 3600, null, null);
        Assert(poprawny.TokenType == "Bearer", "model kanonizuje typ tokenu");
        Assert(!poprawny.ToString().Contains("ACCESS-SYNTETYCZNY-TT", StringComparison.Ordinal),
            "ToString nadal nie wypisuje tokenu");
    }

    // ---------------- B4: prawdziwa semantyka bledow ----------------
    private static void TestB4ProofMismatchOsobnyStatus()
    {
        var handler = new FakeBrokerHandler();
        handler.Enqueue(HttpStatusCode.OK, StartBody("sesja-403"));
        handler.Enqueue(HttpStatusCode.Forbidden, "{\"error\": \"verifier_mismatch\"}");
        using var client = new SonosLoginClient(Konfiguracja(), handler, clock: () => Zegar);
        var sesja = client.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session!;
        var wynik = client.FetchResultAsync(sesja, CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosLoginStatus.ProofMismatch,
            "403 verifier_mismatch to blad LOKALNEGO dowodu klienta, dostano: " + wynik.Status);
        Assert(wynik.Status != SonosLoginStatus.Denied, "403 nie jest odmowa Sonos");
        Assert(wynik.Tokens is null, "403 nie daje tokenow");
        Assert(!wynik.Message.Contains("Sonos nie przyznał", StringComparison.Ordinal),
            "komunikat 403 nie moze obwiniac Sonos: " + wynik.Message);
        Assert(!wynik.Message.Contains("verifier_mismatch", StringComparison.Ordinal),
            "komunikat 403 nie cytuje kodu z ciala");
        Assert(handler.Requests.Count == 2,
            "przy 403 broker NIE konsumuje wyniku, ale klient nie ponawia automatycznie");
        Assert(SonosLoginMessages.Describe(SonosLoginStatus.ProofMismatch)
               != SonosLoginMessages.Describe(SonosLoginStatus.Denied),
            "ProofMismatch ma wlasny staly komunikat");
    }

    private static void TestB4CzterystaBezObwinianiaSonos()
    {
        var odmowa = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"access_denied\"}");
        Assert(odmowa.Status == SonosLoginStatus.Denied, "TYLKO access_denied pozostaje odmowa Sonos");

        foreach (var kod in new[] { "invalid_callback", "token_exchange_failed", "login_failed" })
        {
            var wynik = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"" + kod + "\"}");
            Assert(wynik.Status != SonosLoginStatus.Denied,
                kod + " to blad po stronie brokera/wymiany, NIE decyzja Sonos, dostano: " + wynik.Status);
            Assert(wynik.Status == SonosLoginStatus.BrokerError,
                kod + " ma dostac ogolny blad brokera, dostano: " + wynik.Status);
            Assert(wynik.Tokens is null, kod + " nie daje tokenow");
            Assert(!wynik.Message.Contains("Sonos nie przyznał", StringComparison.Ordinal),
                "komunikat dla " + kod + " nie moze obwiniac Sonos: " + wynik.Message);
            Assert(!wynik.Message.Contains(kod, StringComparison.Ordinal), "komunikat nie cytuje kodu z ciala");
        }

        var nierozpoznane = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"invalid_code_verifier\"}");
        Assert(nierozpoznane.Status == SonosLoginStatus.InvalidResponse,
            "400 invalid_code_verifier to nadal niezgodnosc kontraktu");
    }

    private static void TestB4GotowyWynikPoExpiresAt()
    {
        // Backend ma OSOBNE TTL: gotowy wynik zyje result_ttl_s od callbacku, wiec
        // moze przyjsc PO lokalnym ExpiresAt. Klient nie moze go wtedy zgubic.
        var wynik = WynikPo(
            HttpStatusCode.OK,
            "{\"access_token\": \"SYNTETYCZNY-PO-DEADLINE\", \"token_type\": \"Bearer\", \"expires_in\": 86400}",
            zegarPrzesuniecieSekund: 3600);
        Assert(wynik.Status == SonosLoginStatus.Success,
            "gotowy wynik po lokalnym ExpiresAt nadal jest sukcesem, dostano: " + wynik.Status);
        Assert(wynik.Tokens!.AccessToken == "SYNTETYCZNY-PO-DEADLINE", "tokeny odebrane po lokalnym deadline");

        var brak = WynikPo(
            HttpStatusCode.NotFound,
            "{\"error\": \"unknown_session\"}",
            zegarPrzesuniecieSekund: 3600);
        Assert(brak.Status == SonosLoginStatus.Expired, "brak wyniku po ExpiresAt to nadal Expired");
    }

    private static HttpResponseMessage OdpowiedzZeStrumieniem(HttpStatusCode status, SterowaneCialo cialo) =>
        new(status) { Content = new StreamContent(cialo) };

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
        // TYLKO access_denied jest rzeczywista odmowa Sonos (core.py 267-270).
        // Oczekiwanie dla pozostalych kodow POPRAWIONE wg zrodla backendu:
        // invalid_callback i token_exchange_failed to blad brokera/wymiany, nie
        // decyzja Sonos - patrz TestB4CzterystaBezObwinianiaSonos.
        var odmowa = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"access_denied\"}");
        Assert(odmowa.Status == SonosLoginStatus.Denied, "rozpoznana odmowa access_denied to Denied");
        Assert(odmowa.Tokens is null, "odmowa NIGDY nie moze nosic tokenu");
        Assert(!odmowa.Succeeded, "odmowa nie jest sukcesem");

        foreach (var kod in new[] { "invalid_callback", "token_exchange_failed", "login_failed" })
        {
            var wynik = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"" + kod + "\"}");
            Assert(wynik.Status == SonosLoginStatus.BrokerError,
                "blad brokera/wymiany " + kod + " to BrokerError, nie odmowa Sonos");
            Assert(wynik.Tokens is null, "blad NIGDY nie moze nosic tokenu: " + kod);
            Assert(!wynik.Succeeded, "blad nie jest sukcesem: " + kod);
        }

        var nierozpoznane = WynikPo(HttpStatusCode.BadRequest, "{\"error\": \"invalid_code_verifier\"}");
        Assert(nierozpoznane.Status == SonosLoginStatus.InvalidResponse,
            "400 bez rozpoznanego kodu odmowy to niezgodnosc, nie odmowa uzytkownika");
    }

    private static void TestWynikVerifierMismatchToOdmowa()
    {
        // Oczekiwanie POPRAWIONE: 403 verifier_mismatch (core.py 338-340) to blad
        // LOKALNEGO dowodu klienta, a broker nie konsumuje wtedy wyniku.
        var wynik = WynikPo(HttpStatusCode.Forbidden, "{\"error\": \"verifier_mismatch\"}");
        Assert(wynik.Status == SonosLoginStatus.ProofMismatch,
            "403 verifier_mismatch to blad lokalnego dowodu, nie odmowa Sonos");
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

        public void EnqueueCustom(Func<HttpRequestMessage, HttpResponseMessage> factory) =>
            planned.Enqueue(factory);

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

    /// <summary>
    /// Syntetyczne cialo odpowiedzi z REALNYM opoznieniem fazy ciala. Zero sieci:
    /// opoznienie jest zwyklym Task.Delay honorujacym token anulowania, dzieki
    /// czemu test mierzy rzeczywisty czas powrotu operacji, a nie atrape zegara.
    /// </summary>
    private sealed class SterowaneCialo : Stream
    {
        private readonly byte[] dane;
        private readonly TimeSpan opoznienieNaOdczyt;
        private readonly bool zawieszNaKoncu;
        private readonly int bajtowNaOdczyt;
        private int pozycja;

        public SterowaneCialo(
            string tresc,
            TimeSpan opoznienieNaOdczyt,
            bool zawieszNaKoncu,
            int bajtowNaOdczyt = int.MaxValue)
        {
            dane = Encoding.UTF8.GetBytes(tresc);
            this.opoznienieNaOdczyt = opoznienieNaOdczyt;
            this.zawieszNaKoncu = zawieszNaKoncu;
            this.bajtowNaOdczyt = bajtowNaOdczyt;
        }

        /// <summary>Liczba faktycznie wykonanych odczytow - dowod, ze odczyt zostal przerwany.</summary>
        public int Odczyty { get; private set; }

        /// <summary>Czy strumien zostal zwolniony (ograniczone sprzatanie po deadline).</summary>
        public bool Zwolniony { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => pozycja;
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Odczyty++;
            if (opoznienieNaOdczyt > TimeSpan.Zero)
            {
                await Task.Delay(opoznienieNaOdczyt, cancellationToken).ConfigureAwait(false);
            }

            if (pozycja >= dane.Length)
            {
                if (zawieszNaKoncu)
                {
                    // Serwer przestal nadawac i NIE zamyka polaczenia.
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }

                return 0;
            }

            var ile = Math.Min(Math.Min(buffer.Length, bajtowNaOdczyt), dane.Length - pozycja);
            dane.AsMemory(pozycja, ile).CopyTo(buffer);
            pozycja += ile;
            return ile;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Zwolniony = true;
            base.Dispose(disposing);
        }
    }
}
