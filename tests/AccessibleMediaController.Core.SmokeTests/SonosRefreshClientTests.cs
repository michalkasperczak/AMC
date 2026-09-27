using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// Klient ODNAWIANIA dostepu Sonos (POST /login/refresh). WSZYSTKIE dane sa
/// jawnie SYNTETYCZNE, caly ruch idzie przez atrape HttpMessageHandler - zaden
/// test nie dotyka prawdziwego brokera, konta Sonos ani sieci.
///
/// Kazdy przypadek jest lapany OSOBNO i liczony; jedna awaria NIE zatrzymuje
/// zliczania pozostalych.
/// </summary>
internal static class SonosRefreshClientTests
{
    private const string BrokerOrigin = "https://broker-testowy.invalid";
    private const string SekretMarker = "SEKRET-ATAKUJACEGO-9b71e4";
    private const string StaryToken = "STARY-REFRESH-0001";
    private const string NowyToken = "NOWY-REFRESH-0002";

    public static void Run()
    {
        var przypadki = new (string Nazwa, Action Test)[]
        {
            ("R-01 sukces: rotowany token wraca do wolajacego", TestSukcesRotacja),
            ("R-02 broker oddal DOTYCHCZASOWA wartosc: wraca bajt w bajt", TestSukcesZachowanaWartosc),
            ("R-03 brak refresh_token w 200 to InvalidResponse, nie ciche stare", TestBrakRefreshToken),
            ("R-04 refresh_token null to InvalidResponse", TestRefreshTokenNull),
            ("R-05 refresh_token nie-tekst to InvalidResponse", TestRefreshTokenNieTekst),
            ("R-06 refresh_token pusty to InvalidResponse", TestRefreshTokenPusty),
            ("R-07 rotowany token lamiacy NASZA polityke to InvalidResponse", TestRotowanyZlamanaPolityka),
            ("R-08 brak token_type to InvalidResponse (nie domyslamy Bearer)", TestBrakTokenType),
            ("R-09 token_type null to InvalidResponse", TestTokenTypeNull),
            ("R-10 token_type 'bearer' kanonizowany do Bearer", TestTokenTypeKanonizacja),
            ("R-11 token_type obcy odrzucony bez echa wartosci", TestTokenTypeObcyBezEcha),
            ("R-12 brak access_token to InvalidResponse", TestBrakAccessToken),
            ("R-13 brak expires_in to sukces z nieznanym czasem", TestBrakExpiresIn),
            ("R-14 expires_in 0 to InvalidResponse", TestExpiresInZero),
            ("R-15 expires_in jako tekst to InvalidResponse", TestExpiresInTekst),
            ("R-16 scope nie-tekst to InvalidResponse", TestScopeNieTekst),
            ("R-17 spacje i znaki Unicode wejscia zachowane bajt w bajt", TestWejscieZachowaneDokladnie),
            ("R-18 2048 bezpiecznych ASCII przechodzi i idzie JEDNO zapytanie", TestDwaTysiaceCzterdziesciOsiemAscii),
            ("R-19 granica budzetu zakodowanego ciala: w limicie i o jeden za duzo", TestGranicaBudzetu),
            ("R-20 token z 2048 cudzyslowow: 0 zapytan (budzet, nie 2048 B)", TestCudzyslowyBezZapytania),
            ("R-21 512 emoji: 0 zapytan", TestEmojiBezZapytania),
            ("R-22 pusty token: 0 zapytan", TestPustyTokenBezZapytania),
            ("R-23 null token: 0 zapytan", TestNullTokenBezZapytania),
            ("R-24 znak sterujacy C0: 0 zapytan", TestC0BezZapytania),
            ("R-25 znak sterujacy C1: 0 zapytan", TestC1BezZapytania),
            ("R-26 DEL: 0 zapytan", TestDelBezZapytania),
            ("R-27 niesparowany surogat: 0 zapytan", TestSurogatBezZapytania),
            ("R-28 2049 B zdekodowanych: 0 zapytan", TestZaDlugiBezZapytania),
            ("R-29 dokladne 401 reauthorization_required to osobny wynik", TestDokladne401),
            ("R-30 401 HTML nie domaga sie ponownego logowania", TestHtml401),
            ("R-31 401 z nieznanym kodem nie domaga sie ponownego logowania", TestNieznany401),
            ("R-32 401 z zepsutym JSON nie domaga sie ponownego logowania", TestZepsutyJson401),
            ("R-33 400 invalid_refresh_token to blad kontraktu zadania", TestCzterysta),
            ("R-34 413 body_too_large to blad kontraktu zadania", TestCzterystaTrzynascie),
            ("R-35 429 to RateLimited bez wylogowania", TestCzterystaDwadziesciaDziewiec),
            ("R-36 503 server_not_configured to osobny wynik", TestServerNotConfigured),
            ("R-37 503 refresh_unavailable to osobny, przejsciowy wynik", TestRefreshUnavailable),
            ("R-38 502 provider_status to blad brokera bez wylogowania", TestPiecsetDwa),
            ("R-39 404 to InvalidResponse, NIGDY oczekiwanie", TestCzterystaCztery),
            ("R-40 przekierowanie fail-closed, token nie idzie dalej", TestPrzekierowanie),
            ("R-41 obcy koncowy adres to RedirectRefused", TestObcyKoncowyAdres),
            ("R-42 zawieszone cialo ma SKONCZONY deadline", TestSkonczonyDeadlineCiala),
            ("R-43 anulowanie wolajacego w fazie ciala to Canceled", TestAnulowanieWFazieCiala),
            ("R-44 token anulowania przed wywolaniem: 0 zapytan, Canceled", TestAnulowaneZGory),
            ("R-45 odpowiedz ponad 64 KiB odrzucona bez obcinania", TestOversize),
            ("R-46 brak automatycznych ponowien po niejednoznacznym wyniku", TestBrakPonowien),
            ("R-47 marker ze zlosliwej odpowiedzi nie wycieka do komunikatu", TestBrakWyciekuMarkera),
            ("R-48 cialo zadania ma DOKLADNIE jedno pole, bez sekretu aplikacji", TestCialoJednoPoleBezSekretu),
            ("R-49 RefreshUri to zaufany origin i sciezka login/refresh", TestRefreshUriZaufany),
            ("R-50 opis wyniku i tokenow nie wypisuje wartosci", TestOpisBezWartosci),
            ("R-51 wadliwy surogat w access_token to InvalidResponse, nie wyjatek", TestSurogatWAccessToken),
            ("R-52 wadliwy surogat w refresh_token to InvalidResponse, nie wyjatek", TestSurogatWRefreshToken),
            ("R-53 wadliwy surogat w token_type to InvalidResponse, nie wyjatek", TestSurogatWTokenType),
            ("R-54 wadliwy surogat w scope to InvalidResponse, nie wyjatek", TestSurogatWScope),
            ("R-55 wadliwy surogat w error przy 401 NIE domaga sie ponownego logowania", TestSurogatWError401),
            ("R-56 niski surogat i tekst wokol niego tez nie rzucaja", TestNiskiSurogatWPolach),
            ("R-57 poprawna para surogatow w scope to nadal sukces", TestPoprawnaParaSurogatow),
            ("R-58 401 reauthorization_required nadal dziala po zabezpieczeniu", TestDokladne401PoNaprawie)
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

        Console.WriteLine(
            "Sonos odnawianie: " + (przypadki.Length - bledy.Count) + "/" + przypadki.Length
            + " przypadkow zaliczonych.");
        if (bledy.Count > 0)
        {
            throw new InvalidOperationException(
                "Sonos odnawianie: nieprzeszle przypadki: " + bledy.Count + "/" + przypadki.Length
                + Environment.NewLine + string.Join(Environment.NewLine, bledy));
        }
    }

    // ---------------- sukces i rotacja ----------------
    private static void TestSukcesRotacja()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.Success, "200 z kompletnym cialem to sukces");
        Assert(wynik.Tokens!.RefreshToken == NowyToken, "ROTOWANY token musi wrocic do wolajacego");
        Assert(wynik.Tokens.AccessToken == "ACCESS-XYZ", "access token przepisany dokladnie");
        Assert(wynik.Tokens.TokenType == "Bearer", "typ tokenu kanonizowany");
        Assert(wynik.Tokens.ExpiresInSeconds == 86400, "expires_in przepisany");
        Assert(wynik.Tokens.Scope == "playback-control-all", "scope przepisany");
        Assert(!wynik.RequiresReauthorization, "sukces nie domaga sie ponownego logowania");
        Assert(handler.Requests.Count == 1, "dokladnie jedno zapytanie");
    }

    private static void TestSukcesZachowanaWartosc()
    {
        // Broker realizuje dopuszczalny nawrot: oddaje DOTYCHCZASOWA wartosc.
        var stary = "  token ze spacjami i ogonkiem ąćż  ";
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(stary));
        var wynik = Odnow(handler, stary);
        Assert(wynik.Status == SonosRefreshStatus.Success, "nawrot do starej wartosci to nadal sukces");
        Assert(wynik.Tokens!.RefreshToken == stary, "wartosc wraca bajt w bajt, bez trim i normalizacji");
    }

    private static void TestBrakRefreshToken() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\"}",
            SonosRefreshStatus.InvalidResponse,
            "brak refresh_token to zepsuta odpowiedz, nie zgoda na stary token");

    private static void TestRefreshTokenNull() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": null}",
            SonosRefreshStatus.InvalidResponse,
            "null to obecna wartosc o zlym kszalcie, nie brak pola");

    private static void TestRefreshTokenNieTekst() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": 12345}",
            SonosRefreshStatus.InvalidResponse,
            "liczba nie jest tokenem");

    private static void TestRefreshTokenPusty() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": \"\"}",
            SonosRefreshStatus.InvalidResponse,
            "pusty token nie przechodzi naszej polityki");

    private static void TestRotowanyZlamanaPolityka() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": \"zly\\u0001token\"}",
            SonosRefreshStatus.InvalidResponse,
            "rotowany token ze znakiem sterujacym jest odrzucany");

    private static void TestBrakTokenType() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"refresh_token\": \"" + NowyToken + "\"}",
            SonosRefreshStatus.InvalidResponse,
            "brak token_type w /login/refresh nie jest domyslnym Bearer");

    private static void TestTokenTypeNull() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": null, \"refresh_token\": \"" + NowyToken + "\"}",
            SonosRefreshStatus.InvalidResponse,
            "null token_type to zepsuta odpowiedz");

    private static void TestTokenTypeKanonizacja()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(
            HttpStatusCode.OK,
            "{\"access_token\": \"A\", \"token_type\": \"bearer\", \"refresh_token\": \"" + NowyToken + "\"}");
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.Success, "poprawna wielkosc liter Bearer jest akceptowana");
        Assert(wynik.Tokens!.TokenType == "Bearer", "typ kanonizowany do Bearer");
    }

    private static void TestTokenTypeObcyBezEcha()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(
            HttpStatusCode.OK,
            "{\"access_token\": \"A\", \"token_type\": \"" + SekretMarker
            + "\", \"refresh_token\": \"" + NowyToken + "\"}");
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "obcy typ tokenu odrzucony");
        Assert(!wynik.ToString().Contains(SekretMarker, StringComparison.Ordinal),
            "opis NIE cytuje typu tokenu z odpowiedzi");
        Assert(!wynik.Message.Contains(SekretMarker, StringComparison.Ordinal), "komunikat bez echa");
    }

    private static void TestBrakAccessToken() =>
        AssertStatus(
            "{\"token_type\": \"Bearer\", \"refresh_token\": \"" + NowyToken + "\"}",
            SonosRefreshStatus.InvalidResponse,
            "bez access_token nie ma sukcesu");

    private static void TestBrakExpiresIn()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(
            HttpStatusCode.OK,
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": \"" + NowyToken + "\"}");
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.Success, "backend moze pominac expires_in");
        Assert(wynik.Tokens!.ExpiresInSeconds is null, "brak expires_in znaczy nieznany czas");
    }

    private static void TestExpiresInZero() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": \"" + NowyToken
            + "\", \"expires_in\": 0}",
            SonosRefreshStatus.InvalidResponse,
            "obecne expires_in musi byc dodatnie");

    private static void TestExpiresInTekst() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": \"" + NowyToken
            + "\", \"expires_in\": \"86400\"}",
            SonosRefreshStatus.InvalidResponse,
            "expires_in jako tekst to zepsuta odpowiedz");

    private static void TestScopeNieTekst() =>
        AssertStatus(
            "{\"access_token\": \"A\", \"token_type\": \"Bearer\", \"refresh_token\": \"" + NowyToken
            + "\", \"scope\": 7}",
            SonosRefreshStatus.InvalidResponse,
            "scope musi byc tekstem, gdy jest obecne");

    // ---------------- NASZA polityka wejscia ----------------
    private static void TestWejscieZachowaneDokladnie()
    {
        var wejscie = "  spacje \u00a0 niezlamliwa ąćż \u4e2d\u6587 ogon  ";
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        var wynik = Odnow(handler, wejscie);
        Assert(wynik.Status == SonosRefreshStatus.Success, "spacje i Unicode sa dozwolone");
        var wyslany = CzytajPole(handler.Requests[0].Body, "refresh_token");
        Assert(wyslany == wejscie, "wyslana wartosc IDENTYCZNA z wejsciem (bez trim i normalizacji)");
    }

    private static void TestDwaTysiaceCzterdziesciOsiemAscii()
    {
        var token = new string('a', 2048);
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        var wynik = Odnow(handler, token);
        Assert(wynik.Status == SonosRefreshStatus.Success, "2048 bezpiecznych ASCII miesci sie w obu limitach");
        Assert(handler.Requests.Count == 1, "dokladnie jedno zapytanie");
        Assert(CzytajPole(handler.Requests[0].Body, "refresh_token") == token, "token przepisany w calosci");
    }

    private static void TestGranicaBudzetu()
    {
        var opakowanie = SonosRefreshTokenPolicy.OneFieldEnvelopeBytes;
        var maks = SonosRefreshTokenPolicy.MaxRequestBodyBytes;
        // Znak niebezpieczny liczony po 6 B; dobieramy DOKLADNIE granice.
        var ilosc = (maks - opakowanie) / SonosRefreshTokenPolicy.EscapedUnitBytes;
        var wLimicie = new string('"', ilosc);
        Assert(SonosRefreshTokenPolicy.EncodedRequestBudgetBytes(wLimicie) <= maks,
            "dobrana wartosc ma byc w limicie budzetu");
        Assert(SonosRefreshTokenPolicy.IsAcceptable(wLimicie), "wartosc w limicie jest akceptowana");

        var zaDuzo = new string('"', ilosc + 1);
        Assert(SonosRefreshTokenPolicy.EncodedRequestBudgetBytes(zaDuzo) > maks, "o jeden znak za duzo");
        Assert(!SonosRefreshTokenPolicy.IsAcceptable(zaDuzo), "wartosc ponad budzet jest odrzucana");

        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        var wynik = Odnow(handler, wLimicie);
        Assert(wynik.Status == SonosRefreshStatus.Success, "graniczna wartosc faktycznie przechodzi");
        Assert(Encoding.UTF8.GetByteCount(handler.Requests[0].Body) <= maks,
            "RZECZYWISTE cialo zadania miesci sie w 4096 B");

        var handler2 = new AtrapaBrokera();
        var wynik2 = Odnow(handler2, zaDuzo);
        Assert(wynik2.Status == SonosRefreshStatus.InvalidLocalToken, "ponad budzet to lokalna odmowa");
        Assert(handler2.Requests.Count == 0, "ponad budzet: ZERO zapytan HTTP");
    }

    private static void TestCudzyslowyBezZapytania() =>
        AssertZeroZapytan(new string('"', 2048), "2048 cudzyslowow miesci sie w 2048 B, ale nie w budzecie ciala");

    private static void TestEmojiBezZapytania() =>
        AssertZeroZapytan(string.Concat(Enumerable.Repeat("\U0001F600", 512)), "512 emoji przekracza budzet ciala");

    private static void TestPustyTokenBezZapytania() => AssertZeroZapytan(string.Empty, "pusty token");

    private static void TestNullTokenBezZapytania() => AssertZeroZapytan(null, "null token");

    private static void TestC0BezZapytania() => AssertZeroZapytan("abc\u0001def", "znak sterujacy C0");

    private static void TestC1BezZapytania() => AssertZeroZapytan("abc\u0085def", "znak sterujacy C1");

    private static void TestDelBezZapytania() => AssertZeroZapytan("abc\u007fdef", "DEL");

    private static void TestSurogatBezZapytania() => AssertZeroZapytan("abc\ud800def", "niesparowany surogat");

    private static void TestZaDlugiBezZapytania() =>
        AssertZeroZapytan(new string('a', 2049), "2049 B zdekodowanych przekracza NASZ limit");

    // ---------------- statusy HTTP ----------------
    private static void TestDokladne401()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, "{\"error\": \"reauthorization_required\"}");
        Assert(wynik.Status == SonosRefreshStatus.ReauthorizationRequired,
            "TYLKO to jest wynik wymagajacy ponownego logowania");
        Assert(wynik.RequiresReauthorization, "flaga wyprowadzona ze znanego wyniku");
        Assert(wynik.Tokens is null, "brak tokenow");
    }

    private static void TestHtml401()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, "<html><body>401 Unauthorized</body></html>");
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "HTML 401 nie jest kontraktowym wynikiem");
        Assert(!wynik.RequiresReauthorization, "HTML 401 NIE domaga sie ponownego logowania");
    }

    private static void TestNieznany401()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, "{\"error\": \"proxy_auth_required\"}");
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "nieznany kod 401 to niezgodna odpowiedz");
        Assert(!wynik.RequiresReauthorization, "nieznany kod NIE domaga sie ponownego logowania");
    }

    private static void TestZepsutyJson401()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, "{\"error\": ");
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "zepsuty JSON 401 to niezgodna odpowiedz");
        Assert(!wynik.RequiresReauthorization, "zepsuty JSON NIE domaga sie ponownego logowania");
    }

    private static void TestCzterysta()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.BadRequest, "{\"error\": \"invalid_refresh_token\"}");
        Assert(wynik.Status == SonosRefreshStatus.RequestRejected, "400 to blad kontraktu zadania");
        Assert(!wynik.RequiresReauthorization, "400 nie jest wyrokiem o poswiadczeniach");
    }

    private static void TestCzterystaTrzynascie()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.RequestEntityTooLarge, "{\"error\": \"body_too_large\"}");
        Assert(wynik.Status == SonosRefreshStatus.RequestRejected, "413 to blad kontraktu zadania");
        Assert(!wynik.RequiresReauthorization, "413 nie jest wyrokiem o poswiadczeniach");
    }

    private static void TestCzterystaDwadziesciaDziewiec()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.TooManyRequests, "{\"error\": \"refresh_rate_limited\"}");
        Assert(wynik.Status == SonosRefreshStatus.RateLimited, "429 to odmowa tempa");
        Assert(!wynik.RequiresReauthorization, "429 nic nie mowi o waznosci tokenu");
    }

    private static void TestServerNotConfigured()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.ServiceUnavailable, "{\"error\": \"server_not_configured\"}");
        Assert(wynik.Status == SonosRefreshStatus.BrokerNotConfigured, "sprawa operatora, osobny wynik");
        Assert(!wynik.RequiresReauthorization, "brak konfiguracji nie jest wylogowaniem");
    }

    private static void TestRefreshUnavailable()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.ServiceUnavailable, "{\"error\": \"refresh_unavailable\"}");
        Assert(wynik.Status == SonosRefreshStatus.RefreshUnavailable, "przejsciowy problem dostawcy");
        Assert(wynik.Status != SonosRefreshStatus.BrokerNotConfigured, "odrozniony od braku konfiguracji");
        Assert(!wynik.RequiresReauthorization, "stan przejsciowy nie jest wylogowaniem");
    }

    private static void TestPiecsetDwa()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.BadGateway, "{\"error\": \"provider_status\"}");
        Assert(wynik.Status == SonosRefreshStatus.BrokerError, "502 to blad brokera/dostawcy");
        Assert(!wynik.RequiresReauthorization, "502 nie jest wylogowaniem");
    }

    private static void TestCzterystaCztery()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.NotFound, "{\"error\": \"not_found\"}");
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "404 to niezgodna odpowiedz");
        Assert(!wynik.RequiresReauthorization, "404 nie jest wylogowaniem");
    }

    // ---------------- transport ----------------
    private static void TestPrzekierowanie()
    {
        var handler = new AtrapaBrokera();
        handler.EnqueueRedirect(HttpStatusCode.Found, "https://obcy-host.invalid/login/refresh");
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.RedirectRefused, "przekierowanie jest zatrzymywane");
        Assert(handler.Requests.Count == 1, "zadnego zapytania na obcy host");
        Assert(handler.Requests.All(r => r.Uri.Host == "broker-testowy.invalid"),
            "token nie idzie na inny host");
    }

    private static void TestObcyKoncowyAdres()
    {
        var handler = new AtrapaBrokera
        {
            RewriteFinalUriTo = new Uri("https://obcy-host.invalid/login/refresh")
        };
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.RedirectRefused, "obcy koncowy adres to fail-closed");
        Assert(wynik.Tokens is null, "zadnych tokenow z obcego hosta");
    }

    private static void TestSkonczonyDeadlineCiala()
    {
        var strumien = new SterowaneCialo(OkBody(NowyToken), TimeSpan.Zero, zawieszNaKoncu: true);
        var handler = new AtrapaBrokera();
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(HttpStatusCode.OK, strumien));
        using var client = new SonosLoginClient(Konfiguracja(), handler, TimeSpan.FromMilliseconds(250));
        var zegar = Stopwatch.StartNew();
        var zadanie = Task.Run(() => client.RefreshAsync(StaryToken, CancellationToken.None).GetAwaiter().GetResult());
        Assert(zadanie.Wait(TimeSpan.FromSeconds(5)), "zawieszone cialo MUSI wrocic w skonczonym czasie");
        zegar.Stop();
        Assert(zadanie.Result.Status == SonosRefreshStatus.BrokerUnreachable,
            "minal deadline, wiec nie ma sukcesu");
        Assert(zegar.Elapsed < TimeSpan.FromSeconds(5), "powrot w granicach deadline, nie po 5 s");
        Assert(strumien.Zwolniony, "strumien zwolniony takze po przerwaniu odczytu");
    }

    private static void TestAnulowanieWFazieCiala()
    {
        var strumien = new SterowaneCialo(OkBody(NowyToken), TimeSpan.FromMilliseconds(200), zawieszNaKoncu: true);
        var handler = new AtrapaBrokera();
        handler.EnqueueCustom(_ => OdpowiedzZeStrumieniem(HttpStatusCode.OK, strumien));
        using var client = new SonosLoginClient(Konfiguracja(), handler, TimeSpan.FromSeconds(30));
        using var zrodlo = new CancellationTokenSource();
        var zadanie = Task.Run(() => client.RefreshAsync(StaryToken, zrodlo.Token).GetAwaiter().GetResult());
        zrodlo.CancelAfter(TimeSpan.FromMilliseconds(150));
        Assert(zadanie.Wait(TimeSpan.FromSeconds(10)), "anulowanie wolajacego przerywa faze ciala");
        Assert(zadanie.Result.Status == SonosRefreshStatus.Canceled,
            "anulowanie wolajacego to Canceled, nie timeout brokera");
    }

    private static void TestAnulowaneZGory()
    {
        var handler = new AtrapaBrokera();
        using var zrodlo = new CancellationTokenSource();
        zrodlo.Cancel();
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        var wynik = client.RefreshAsync(StaryToken, zrodlo.Token).GetAwaiter().GetResult();
        Assert(wynik.Status == SonosRefreshStatus.Canceled, "anulowany token daje Canceled");
        Assert(handler.Requests.Count == 0, "ZERO zapytan po anulowaniu z gory");
    }

    private static void TestOversize()
    {
        var wielkie = "{\"access_token\": \"" + new string('a', SonosLoginClient.MaxResponseBytes + 64)
            + "\", \"token_type\": \"Bearer\", \"refresh_token\": \"" + NowyToken + "\"}";
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.OK, wielkie);
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "za duza odpowiedz odrzucona, nie obcieta");
        Assert(wynik.Tokens is null, "z obcietego ciala nie wnioskujemy sukcesu");
    }

    private static void TestBrakPonowien()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable, "{\"error\": \"refresh_unavailable\"}");
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.RefreshUnavailable, "niejednoznaczny wynik przekazany wprost");
        Assert(handler.Requests.Count == 1, "DOKLADNIE jedno zapytanie: zadnych automatycznych ponowien");
    }

    private static void TestBrakWyciekuMarkera()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(
            HttpStatusCode.BadRequest,
            "{\"error\": \"" + SekretMarker + "\", \"detail\": \"" + SekretMarker + "\"}");
        var wynik = Odnow(handler, StaryToken);
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "nierozpoznane cialo bledu 400");
        Assert(!wynik.Message.Contains(SekretMarker, StringComparison.Ordinal), "komunikat bez echa ciala");
        Assert(!wynik.ToString().Contains(SekretMarker, StringComparison.Ordinal), "ToString bez echa ciala");
    }

    private static void TestCialoJednoPoleBezSekretu()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        Odnow(handler, StaryToken);
        using var dokument = JsonDocument.Parse(handler.Requests[0].Body);
        var pola = dokument.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert(pola.Length == 1 && pola[0] == "refresh_token",
            "cialo ma DOKLADNIE jedno pole refresh_token, bez client_secret");
        Assert(!handler.Requests[0].Body.Contains("secret", StringComparison.OrdinalIgnoreCase),
            "zadnego sekretu aplikacji w zadaniu");
        Assert(handler.Requests[0].Uri.AbsolutePath == "/login/refresh", "sciezka kontraktowa");
    }

    private static void TestRefreshUriZaufany()
    {
        var konfiguracja = Konfiguracja();
        Assert(konfiguracja.RefreshUri.Scheme == "https", "tylko HTTPS");
        Assert(konfiguracja.RefreshUri.Host == "broker-testowy.invalid", "zaufany host z konfiguracji");
        Assert(konfiguracja.RefreshUri.AbsolutePath == "/login/refresh", "sciezka login/refresh");
        Assert(konfiguracja.IsSameOrigin(konfiguracja.RefreshUri), "adres nalezy do zaufanego origin");
    }

    private static void TestOpisBezWartosci()
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(HttpStatusCode.OK, OkBody(NowyToken));
        var wynik = Odnow(handler, StaryToken);
        var opis = wynik.ToString() + " " + wynik.Tokens!.ToString();
        Assert(!opis.Contains(NowyToken, StringComparison.Ordinal), "opis nie wypisuje tokenu odswiezania");
        Assert(!opis.Contains("ACCESS-XYZ", StringComparison.Ordinal), "opis nie wypisuje access tokenu");
        Assert(!opis.Contains(StaryToken, StringComparison.Ordinal), "opis nie wypisuje starego tokenu");
    }

    // ---------------- wadliwe Unicode w odpowiedzi (RAW JSON) ----------------
    // Ciala budujemy RECZNIE, jako surowy tekst z sekwencja \ud800 / \udc00.
    // JsonSerializer podmienilby taki znak na U+FFFD i przypadek przestalby
    // istniec - dlatego tu NIE MA serializacji. JSON parsuje sie poprawnie,
    // dopiero dekodowanie tekstu w JsonElement.GetString rzuca.
    private const string SamotnyWysokiSurogat = "\\ud800";
    private const string SamotnyNiskiSurogat = "\\udc00";

    /// <summary>Cialo 200 z JEDNYM polem podmienionym na wadliwy tekst.</summary>
    private static string SurowyOkBody(string pole, string zepsutaWartosc)
    {
        var pola = new Dictionary<string, string>
        {
            ["access_token"] = "\"ACCESS-XYZ\"",
            ["token_type"] = "\"Bearer\"",
            ["refresh_token"] = "\"" + NowyToken + "\"",
            ["scope"] = "\"playback-control-all\""
        };
        pola[pole] = "\"" + zepsutaWartosc + "\"";
        return "{" + string.Join(",", pola.Select(p => "\"" + p.Key + "\":" + p.Value)) + "}";
    }

    private static void AssertZepsuteUnicode(string pole, string sekwencja)
    {
        var body = SurowyOkBody(pole, sekwencja);
        // Kontrola zalozenia: sam DOKUMENT musi sie parsowac - blad jest pozniej.
        using (JsonDocument.Parse(body)) { }
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.OK, body);
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse,
            pole + " z wadliwym surogatem to InvalidResponse (otrzymano " + wynik.Status + ")");
        Assert(wynik.Tokens is null, pole + ": zepsuta odpowiedz nie wydaje tokenow");
        Assert(!wynik.RequiresReauthorization,
            pole + ": zepsute Unicode NIGDY nie domaga sie kasowania poswiadczen");
    }

    private static void TestSurogatWAccessToken() =>
        AssertZepsuteUnicode("access_token", SamotnyWysokiSurogat);

    private static void TestSurogatWRefreshToken() =>
        AssertZepsuteUnicode("refresh_token", SamotnyWysokiSurogat);

    private static void TestSurogatWTokenType() =>
        AssertZepsuteUnicode("token_type", SamotnyWysokiSurogat);

    private static void TestSurogatWScope() =>
        AssertZepsuteUnicode("scope", SamotnyWysokiSurogat);

    private static void TestSurogatWError401()
    {
        var body = "{\"error\":\"" + SamotnyWysokiSurogat + "\"}";
        using (JsonDocument.Parse(body)) { }
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, body);
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse,
            "401 z wadliwym error to niezgodna odpowiedz (otrzymano " + wynik.Status + ")");
        Assert(!wynik.RequiresReauthorization,
            "zepsute Unicode przy 401 NIGDY nie domaga sie ponownego logowania");
        Assert(wynik.Tokens is null, "401 nie wydaje tokenow");
    }

    private static void TestNiskiSurogatWPolach()
    {
        foreach (var pole in new[] { "access_token", "refresh_token", "token_type", "scope" })
        {
            AssertZepsuteUnicode(pole, "PRE-" + SamotnyNiskiSurogat + "-POST");
        }

        var body = "{\"error\":\"PRE-" + SamotnyNiskiSurogat + "\"}";
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, body);
        Assert(wynik.Status == SonosRefreshStatus.InvalidResponse, "niski surogat w error: InvalidResponse");
        Assert(!wynik.RequiresReauthorization, "niski surogat w error nie kasuje poswiadczen");
    }

    /// <summary>Kontrola POZYTYWNA: poprawna para surogatow ma dalej przechodzic.</summary>
    private static void TestPoprawnaParaSurogatow()
    {
        var body = SurowyOkBody("scope", "playback-\\ud83c\\udfb5-all");
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.OK, body);
        Assert(wynik.Status == SonosRefreshStatus.Success,
            "poprawna para surogatow to nadal sukces (otrzymano " + wynik.Status + ")");
        Assert(wynik.Tokens!.Scope == "playback-\ud83c\udfb5-all", "scope przepisany bajt w bajt");
        Assert(wynik.Tokens.RefreshToken == NowyToken, "rotowany token nietkniety");
    }

    /// <summary>Kontrola POZYTYWNA: zabezpieczenie nie psuje znanego 401.</summary>
    private static void TestDokladne401PoNaprawie()
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.Unauthorized, "{\"error\":\"reauthorization_required\"}");
        Assert(wynik.Status == SonosRefreshStatus.ReauthorizationRequired,
            "kontraktowe 401 nadal wymaga ponownego logowania (otrzymano " + wynik.Status + ")");
        Assert(wynik.RequiresReauthorization, "flaga nadal ustawiona dla kontraktowego 401");
    }

    // ---------------- pomocnicze (syntetyczne) ----------------
    private static string OkBody(string refreshToken) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["access_token"] = "ACCESS-XYZ",
            ["token_type"] = "Bearer",
            ["expires_in"] = 86400,
            ["scope"] = "playback-control-all",
            ["refresh_token"] = refreshToken
        });

    private static SonosLoginBrokerConfiguration Konfiguracja()
    {
        SonosLoginBrokerConfiguration.TryCreate(BrokerOrigin, out var configuration);
        return configuration ?? throw new InvalidOperationException("Syntetyczny origin nie przeszedl walidacji.");
    }

    private static SonosRefreshOutcome Odnow(AtrapaBrokera handler, string? token)
    {
        using var client = new SonosLoginClient(Konfiguracja(), handler);
        return client.RefreshAsync(token, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static SonosRefreshOutcome OdnowZOdpowiedzia(HttpStatusCode status, string body)
    {
        var handler = new AtrapaBrokera();
        handler.Enqueue(status, body);
        return Odnow(handler, StaryToken);
    }

    private static void AssertStatus(string body, SonosRefreshStatus oczekiwany, string message)
    {
        var wynik = OdnowZOdpowiedzia(HttpStatusCode.OK, body);
        Assert(wynik.Status == oczekiwany, message + " (otrzymano " + wynik.Status + ")");
        Assert(wynik.Tokens is null, "niezgodna odpowiedz nie wydaje tokenow");
        Assert(!wynik.RequiresReauthorization, "niezgodna odpowiedz nie domaga sie ponownego logowania");
    }

    private static void AssertZeroZapytan(string? token, string message)
    {
        var handler = new AtrapaBrokera();
        var wynik = Odnow(handler, token);
        Assert(wynik.Status == SonosRefreshStatus.InvalidLocalToken,
            message + ": lokalna odmowa (otrzymano " + wynik.Status + ")");
        Assert(handler.Requests.Count == 0, message + ": ZERO zapytan HTTP");
        Assert(!wynik.RequiresReauthorization, message + ": to NIE wyrok o poswiadczeniach");
    }

    private static string? CzytajPole(string json, string name)
    {
        using var dokument = JsonDocument.Parse(json);
        return dokument.RootElement.TryGetProperty(name, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static HttpResponseMessage OdpowiedzZeStrumieniem(HttpStatusCode status, SterowaneCialo cialo) =>
        new(status) { Content = new StreamContent(cialo) };

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Sonos odnawianie: " + message);
        }
    }

    /// <summary>Atrapa transportu: zero sieci, pelna kontrola statusow i cial.</summary>
    private sealed class AtrapaBrokera : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> planned = new();

        public List<(Uri Uri, string Body)> Requests { get; } = new();

        public Uri? RewriteFinalUriTo { get; init; }

        public void Enqueue(HttpStatusCode status, string body) =>
            planned.Enqueue(_ => Build(status, body));

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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
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
            return response;
        }

        private static HttpResponseMessage Build(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    /// <summary>
    /// Syntetyczne cialo odpowiedzi z REALNYM opoznieniem fazy ciala. Zero sieci:
    /// opoznienie jest zwyklym Task.Delay honorujacym token anulowania.
    /// </summary>
    private sealed class SterowaneCialo : Stream
    {
        private readonly byte[] dane;
        private readonly TimeSpan opoznienieNaOdczyt;
        private readonly bool zawieszNaKoncu;
        private int pozycja;

        public SterowaneCialo(string tresc, TimeSpan opoznienieNaOdczyt, bool zawieszNaKoncu)
        {
            dane = Encoding.UTF8.GetBytes(tresc);
            this.opoznienieNaOdczyt = opoznienieNaOdczyt;
            this.zawieszNaKoncu = zawieszNaKoncu;
        }

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
            if (opoznienieNaOdczyt > TimeSpan.Zero)
            {
                await Task.Delay(opoznienieNaOdczyt, cancellationToken).ConfigureAwait(false);
            }

            if (pozycja >= dane.Length)
            {
                if (zawieszNaKoncu)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                }

                return 0;
            }

            var ile = Math.Min(buffer.Length, dane.Length - pozycja);
            dane.AsMemory(pozycja, ile).CopyTo(buffer);
            pozycja += ile;
            return ile;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
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
