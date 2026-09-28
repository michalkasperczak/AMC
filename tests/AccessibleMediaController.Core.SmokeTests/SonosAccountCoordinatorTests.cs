using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// Testy ZACHOWANIA koordynatora konta Sonos. Wszystkie dane sa jawnie
/// SYNTETYCZNE: zero sieci, zero DPAPI, zero prawdziwego konta i zero UI.
///
/// Magazyn i bramka logowania sa MALYMI atrapami z licznikami. Sesja logowania
/// pochodzi z PRAWDZIWEGO <see cref="SonosLoginClient"/> z syntetycznym
/// HttpMessageHandler (ten sam wzorzec, co w SonosLoginClientTests), wiec do
/// produkcji nie dodano zadnego szwu testowego ani Reflection.
///
/// Wszystkie oczekiwania sa OGRANICZONE czasowo, a bariery to
/// TaskCompletionSource z RunContinuationsAsynchronously - zadnego usypiania
/// watku dla wywolania wyscigu.
/// </summary>
internal static class SonosAccountCoordinatorTests
{
    private const string BrokerOrigin = "https://broker-testowy.invalid";
    private static readonly DateTimeOffset Zegar = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    public static void Run()
    {
        var przypadki = new (string Nazwa, Action Test)[]
        {
            ("Odtworzenie jest jednorazowe i zachowuje UTC, brak RT oraz nieznana waznosc",
                TestOdtworzenieJednorazoweIWiernosc),
            ("Nieczytelny zapis nie udaje konta i nie zostaje skasowany", TestNieczytelnyZapisNieKasuje),
            ("Blad odczytu to blad magazynu, nie wyrok o tokenach", TestBladOdczytuToBladMagazynu),
            ("Sciezka odtworzenie-logowanie-zapis konczy sie polaczonym kontem", TestSciezkaLogowaniaIZapisu),
            ("Nieudany zapis zachowuje nowy zestaw w pamieci, a ponowienie nie rusza sieci",
                TestNieudanyZapisIPonowienie),
            ("Odnowienie tej samej generacji wysyla dokladnie jedno zapytanie", TestOdnowienieSingleFlight),
            ("Tylko 401 kasuje zapis; przejsciowy blad nie kasuje niczego", TestKasujeTylko401),
            ("Nieudane Delete nie udaje potwierdzonego wylogowania", TestNieudaneDeleteNiePotwierdza),
            ("Nieudane sprawdzenie logowania nie zostawia stanu oczekiwania na przegladarke",
                TestPoNieudanymSprawdzeniuNieMaOczekiwania),
            ("Spozniony Pending po udanym logowaniu nie wskrzesza oczekiwania", TestSpoznionyPendingPoSukcesie),
            ("Odnowienie z juz anulowanym tokenem nie wysyla zapytania", TestOdnowienieZAnulowanymTokenem),
            ("Rezygnacja jednego chetnego nie anuluje odnowienia pozostalym", TestRezygnacjaJednegoNieAnulujeInnym),
            ("Bramka nie jest wolana pod blokada koordynatora", TestBramkaNiePodBlokada),
            ("Spozniony sukces odnowienia nie nadpisuje nowszego logowania", TestSpoznionySukcesNieNadpisuje),
            ("Spoznione 401 nie kasuje zapisu po wylogowaniu", TestSpoznione401PoWylogowaniu),
            ("Odrzucony rekord nie udaje polaczonego konta", TestInvalidRecordNieUdajeKonta),
            ("Brak tokenu odswiezania nie powoduje zapytania ani kasacji", TestBrakRefreshTokenu),
            ("Samo rozpoczecie i anulowanie logowania nie porzuca odnowienia konta",
                TestLogowanieNiePorzucaOdnowienia),
            ("Odnowienie nie ukrywa niezakończonej próby logowania", SonosAccountCoordinatorStateTests.RefreshPreservesPendingLogin),
            ("Przejściowy błąd odbioru można ponowić bez nowego logowania", SonosAccountCoordinatorStateTests.TemporaryFetchFailureCanBeRetried),
            ("Origin koordynatora jest zgodny z konfiguracją klienta i magazynu", SonosAccountCoordinatorStateTests.BrokerOriginMatchesAcceptedConfiguration),
            ("Odczyt obcego brokera nie zmienia magazynu", SonosAccountCoordinatorStateTests.RestoreBrokerMismatchDoesNotMutateStore),
            ("Starty wracające odwrotnie zachowują nowszą próbę", SonosAccountCoordinatorStateTests.ReverseStartKeepsNewerAttempt),
            ("Anulowane logowanie odrzuca późny sukces", SonosAccountCoordinatorStateTests.CanceledLoginIgnoresLateSuccess),
            ("Stare 401 nie kasuje nowego logowania", SonosAccountCoordinatorStateTests.Stale401CannotDeleteNewLogin),
            ("Dispose kończy własne odnowienie bez zapisu", SonosAccountCoordinatorStateTests.DisposeCancelsOwnedRefreshWithoutWriting),
            ("Nowy i odtworzony stan ma stabilny znacznik podlaczenia", TestZnacznikPoczatkowyJestStabilny),
            ("Nieudane nowe logowanie nie zmienia znacznika podlaczenia", TestNieudaneLogowanieNieZmieniaZnacznika),
            ("Udane nowe logowanie zmienia znacznik podlaczenia", TestUdaneNoweLogowanieZmieniaZnacznik),
            ("Odnowienie i rotacja zestawu nie zmieniaja znacznika podlaczenia", TestOdnowienieNieZmieniaZnacznika),
            ("Odlaczenie i usuniecie konta zmieniaja znacznik podlaczenia", TestOdlaczenieZmieniaZnacznik),
            ("Spoznione wyniki nie udaja zastapienia konta", TestSpoznioneWynikiNieUdajaZastapienia),
            ("Znacznik podlaczenia nie niesie tokenow ani sekretow", TestZnacznikNieNiesieSekretow)
        };

        var bledy = new List<string>();
        foreach (var (nazwa, test) in przypadki)
        {
            try
            {
                test();
                Console.WriteLine("  OK   " + nazwa);
            }
            catch (Exception wyjatek)
            {
                bledy.Add(nazwa + ": " + Rozwin(wyjatek).Message);
                Console.WriteLine("  BLAD " + nazwa);
                Console.WriteLine("       " + Rozwin(wyjatek).Message);
            }
        }

        Console.WriteLine(
            "Sonos: koordynator konta - "
            + (przypadki.Length - bledy.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "/"
            + przypadki.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " testow zaliczonych.");
        if (bledy.Count > 0)
        {
            throw new InvalidOperationException(
                "Sonos: koordynator konta - niezaliczone przypadki: " + string.Join(" | ", bledy));
        }
    }

    // ================= 1. odtworzenie =================

    private static void TestOdtworzenieJednorazoweIWiernosc()
    {
        // Rekord zapisany w innej strefie z brakiem RT i BEZ expires_in.
        var zapisany = new SonosStoredCredentials(
            BrokerOrigin,
            Tokeny("ACCESS-ODTWORZONY", expiresIn: null, refreshToken: null),
            new DateTimeOffset(2026, 9, 26, 14, 0, 0, TimeSpan.FromHours(2)));
        var magazyn = new AtrapaMagazynu { Odczyt = SonosCredentialReadOutcome.Ok(zapisany) };
        using var koordynator = Koordynator(new AtrapaBramki(), magazyn);

        var pierwsze = koordynator.RestoreOnce();
        Assert(pierwsze.Performed, "pierwsze odtworzenie faktycznie czyta magazyn");
        Assert(pierwsze.Snapshot.State == SonosAccountState.Connected, "odczytany rekord daje konto polaczone");
        Assert(pierwsze.Snapshot.IsPersisted, "odczytany rekord jest utrwalony");
        Assert(pierwsze.Snapshot.ReceivedAtUtc == new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero),
            "moment otrzymania wraca w UTC bez przesuniecia");
        Assert(!pierwsze.Snapshot.IsExpiryKnown, "brak expires_in zostaje nieznana waznoscia");
        Assert(pierwsze.Snapshot.ExpiresAtUtc is null, "nieznana waznosc nie dostaje domyslnego czasu zycia");
        Assert(!pierwsze.Snapshot.HasRefreshToken, "brak tokenu odswiezania jest widoczny");
        Assert(magazyn.LiczbaOdczytow == 1, "odczyt wykonany dokladnie raz");

        var drugie = koordynator.RestoreOnce();
        Assert(!drugie.Performed, "drugie odtworzenie nie czyta magazynu");
        Assert(magazyn.LiczbaOdczytow == 1, "magazyn nadal odczytany tylko raz");
        Assert(magazyn.LiczbaUsuniec == 0, "odtworzenie niczego nie usuwa");
        Assert(magazyn.LiczbaZapisow == 0, "odtworzenie niczego nie zapisuje");
    }

    private static void TestNieczytelnyZapisNieKasuje()
    {
        var magazyn = new AtrapaMagazynu
        {
            Odczyt = SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Invalid)
        };
        using var koordynator = Koordynator(new AtrapaBramki(), magazyn);
        var wynik = koordynator.RestoreOnce();
        Assert(wynik.Snapshot.State == SonosAccountState.NeedsLogin, "nieczytelny zapis wymaga logowania");
        Assert(wynik.Snapshot.Issue == SonosAccountIssue.InvalidStoredRecord, "przyczyna nazwana wprost");
        Assert(!wynik.Snapshot.HasCredentials, "nieczytelny zapis nie udaje poswiadczen");
        Assert(wynik.Snapshot.PersistedRecordMayRemain, "zapis moze nadal lezec na dysku");
        Assert(magazyn.LiczbaUsuniec == 0, "nieczytelnego zapisu nie kasujemy");
        Assert(magazyn.LiczbaZapisow == 0, "nieczytelnego zapisu nie nadpisujemy");
    }

    private static void TestBladOdczytuToBladMagazynu()
    {
        var magazyn = new AtrapaMagazynu
        {
            Odczyt = SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.ReadFailure)
        };
        using var koordynator = Koordynator(new AtrapaBramki(), magazyn);
        var wynik = koordynator.RestoreOnce();
        Assert(wynik.Snapshot.State == SonosAccountState.StoreFailure, "blad odczytu to stan magazynu");
        Assert(wynik.Snapshot.Issue == SonosAccountIssue.ReadFailure, "przyczyna to blad odczytu");
        Assert(magazyn.LiczbaUsuniec == 0 && magazyn.LiczbaZapisow == 0, "blad odczytu nie rusza pliku");
    }

    // ================= 2. logowanie =================

    private static void TestSciezkaLogowaniaIZapisu()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu
        {
            Odczyt = SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
        };
        using var koordynator = Koordynator(bramka, magazyn);
        Assert(koordynator.RestoreOnce().Snapshot.State == SonosAccountState.NoAccount,
            "brak zapisu nie jest bledem");

        var sesja = Sesja("logowanie-pozytywne");
        bramka.PlanujStart(SonosLoginStartOutcome.Ok(sesja));
        var start = Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        Assert(start.Started, "logowanie rozpoczete");
        Assert(start.AuthorizeUri is not null && start.AuthorizeUri.Host == "api.sonos.com",
            "adres autoryzacji pochodzi z sesji brokera");
        Assert(start.Snapshot.IsAwaitingBrowser, "proba czeka na przegladarke");
        Assert(start.Snapshot.State == SonosAccountState.AwaitingBrowser, "stan mowi o przegladarce");

        bramka.PlanujWynik(SonosLoginResultOutcome.Failure(SonosLoginStatus.Pending));
        var oczekiwanie = Czekaj(koordynator.CheckLoginAsync(CancellationToken.None));
        Assert(oczekiwanie.StillWaiting, "Pending zostawia probe oczekujaca");
        Assert(oczekiwanie.Snapshot.IsAwaitingBrowser, "po Pending nadal czekamy na przegladarke");
        Assert(magazyn.LiczbaZapisow == 0, "Pending niczego nie zapisuje");

        bramka.PlanujWynik(SonosLoginResultOutcome.Ok(Tokeny("ACCESS-NOWY", 3600, "RT-NOWY")));
        var sukces = Czekaj(koordynator.CheckLoginAsync(CancellationToken.None));
        Assert(sukces.Connected, "udane logowanie daje konto");
        Assert(sukces.WriteStatus == SonosCredentialWriteStatus.Success, "udany zestaw zostal zapisany");
        Assert(sukces.Snapshot.State == SonosAccountState.Connected, "stan polaczony");
        Assert(sukces.Snapshot.IsPersisted, "zestaw utrwalony");
        Assert(!sukces.Snapshot.IsAwaitingBrowser, "po sukcesie nie czekamy na przegladarke");
        Assert(magazyn.LiczbaZapisow == 1, "dokladnie jeden zapis");
        Assert(magazyn.Ostatni?.Tokens.RefreshToken == "RT-NOWY", "zapisano caly nowy zestaw");
        Assert(magazyn.Ostatni?.ReceivedAtUtc == Zegar, "moment otrzymania z zegara koordynatora, w UTC");
        Assert(bramka.LiczbaStartow == 1 && bramka.LiczbaWynikow == 2, "dokladnie jedno Start i dwa Fetch");
    }

    // ================= 5. ponowienie zapisu =================

    private static void TestNieudanyZapisIPonowienie()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu { Zapis = SonosCredentialWriteStatus.WriteFailure };
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-NIEZAPISANY", "RT-NIEZAPISANY");

        var migawka = koordynator.Snapshot;
        Assert(migawka.HasCredentials, "nowy zestaw zostaje w pamieci");
        Assert(!migawka.IsPersisted, "nieudany zapis jest widoczny jako brak utrwalenia");
        Assert(migawka.State == SonosAccountState.Connected, "konto dziala, choc nie jest zapisane");
        Assert(migawka.Issue == SonosAccountIssue.WriteFailure, "przyczyna to nieudany zapis");
        Assert(migawka.CanRetryPersist, "ponowienie samego zapisu ma sens");
        Assert(magazyn.LiczbaUsuniec == 0, "nieudany zapis niczego nie kasuje");

        magazyn.Zapis = SonosCredentialWriteStatus.Success;
        var startyPrzed = bramka.LiczbaStartow;
        var wynikiPrzed = bramka.LiczbaWynikow;
        var odnowieniaPrzed = bramka.LiczbaOdnowien;
        var ponowienie = koordynator.RetryPersist();
        Assert(ponowienie.Attempted && ponowienie.Succeeded, "ponowienie zapisu sie udalo");
        Assert(koordynator.Snapshot.IsPersisted, "po ponowieniu zestaw jest utrwalony");
        Assert(koordynator.Snapshot.Issue == SonosAccountIssue.None, "przyczyna wyczyszczona");
        Assert(bramka.LiczbaStartow == startyPrzed
            && bramka.LiczbaWynikow == wynikiPrzed
            && bramka.LiczbaOdnowien == odnowieniaPrzed,
            "ponowienie zapisu nie wysyla ZADNEGO zapytania");

        var niepotrzebne = koordynator.RetryPersist();
        Assert(!niepotrzebne.Attempted, "ponowienie zapisanego zestawu jest niepotrzebne");
    }

    // ================= 3-4. odnawianie =================

    private static void TestOdnowienieSingleFlight()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-1", "RT-1");

        var brama = NowaBrama();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-2", 3600, "RT-2")), brama);
        var pierwsze = koordynator.RefreshAsync(CancellationToken.None);
        var drugie = koordynator.RefreshAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "pierwszy chetny wyslal zapytanie");
        Assert(bramka.LiczbaOdnowien == 1, "drugi chetny NIE wysyla drugiego zapytania");
        brama.TrySetResult(true);

        var a = Czekaj(pierwsze);
        var b = Czekaj(drugie);
        Assert(a.RefreshStatus == SonosRefreshStatus.Success && b.RefreshStatus == SonosRefreshStatus.Success,
            "oba wolajace dostaja ten sam sukces");
        Assert(b.Joined || a.Joined, "drugi chetny jest oznaczony jako dolaczony");
        Assert(bramka.LiczbaOdnowien == 1, "nadal dokladnie jedno zapytanie na generacje");
        Assert(bramka.UzyteTokenyOdswiezania.Count == 1 && bramka.UzyteTokenyOdswiezania[0] == "RT-1",
            "zapytanie poszlo z biezacym tokenem odswiezania");
        Assert(magazyn.Ostatni?.Tokens.RefreshToken == "RT-2", "zapisano ROTOWANY token odswiezania");
        Assert(magazyn.LiczbaUsuniec == 0, "udane odnowienie niczego nie kasuje");
    }

    private static void TestKasujeTylko401()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-T", "RT-T");

        foreach (var status in new[]
                 {
                     SonosRefreshStatus.RateLimited,
                     SonosRefreshStatus.RefreshUnavailable,
                     SonosRefreshStatus.BrokerError,
                     SonosRefreshStatus.BrokerUnreachable,
                     SonosRefreshStatus.InvalidResponse,
                     SonosRefreshStatus.Canceled
                 })
        {
            bramka.PlanujOdnowienie(SonosRefreshOutcome.Failure(status));
            var wynik = Czekaj(koordynator.RefreshAsync(CancellationToken.None));
            Assert(wynik.RefreshStatus == status, "status " + status + " wraca bez zmiany");
            Assert(koordynator.Snapshot.HasCredentials, status + " nie kasuje zestawu w pamieci");
            Assert(magazyn.LiczbaUsuniec == 0, status + " nie kasuje zapisu");
        }

        bramka.PlanujOdnowienie(SonosRefreshOutcome.Failure(SonosRefreshStatus.ReauthorizationRequired));
        var reauth = Czekaj(koordynator.RefreshAsync(CancellationToken.None));
        Assert(reauth.RefreshStatus == SonosRefreshStatus.ReauthorizationRequired, "401 rozpoznane");
        Assert(!koordynator.Snapshot.HasCredentials, "401 uniewaznia zestaw");
        Assert(koordynator.Snapshot.State == SonosAccountState.NeedsLogin, "po 401 trzeba sie zalogowac");
        Assert(koordynator.Snapshot.Issue == SonosAccountIssue.Reauthorization, "przyczyna to 401");
        Assert(magazyn.LiczbaUsuniec == 1, "dokladne 401 kasuje zapis dokladnie raz");
    }

    private static void TestBrakRefreshTokenu()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-BEZ-RT", refreshToken: null);

        var wynik = Czekaj(koordynator.RefreshAsync(CancellationToken.None));
        Assert(wynik.RefreshStatus == SonosRefreshStatus.InvalidLocalToken, "brak RT to lokalny brak, nie awaria");
        Assert(bramka.LiczbaOdnowien == 0, "brak RT nie wysyla ZADNEGO zapytania");
        Assert(magazyn.LiczbaUsuniec == 0, "brak RT nie kasuje zapisu");
        Assert(koordynator.Snapshot.HasCredentials, "zestaw bez RT zostaje uzyteczny");
    }

    // ================= wylogowanie =================

    private static void TestNieudaneDeleteNiePotwierdza()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu { UsuniecieUdane = false };
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-D", "RT-D");

        var wynik = koordynator.Disconnect();
        Assert(!wynik.Disconnected, "nieudane Delete nie jest potwierdzonym wylogowaniem");
        Assert(wynik.DeleteFailed, "porazka usuniecia jest jawna");
        Assert(wynik.Snapshot.PersistedRecordMayRemain, "zapis moze nadal lezec na dysku");
        Assert(wynik.Snapshot.State == SonosAccountState.StoreFailure, "stan to blad magazynu");
        Assert(wynik.Snapshot.Issue == SonosAccountIssue.DeleteFailure, "przyczyna to nieudane usuniecie");
        Assert(!wynik.Snapshot.HasCredentials, "zestaw przestaje byc uzywany");

        magazyn.UsuniecieUdane = true;
        var powtorka = koordynator.Disconnect();
        Assert(powtorka.Disconnected, "ponowione wylogowanie moze sie udac");
        Assert(!powtorka.Snapshot.PersistedRecordMayRemain, "po udanym usunieciu nic nie zostaje");
        Assert(powtorka.Snapshot.State == SonosAccountState.NoAccount, "po wylogowaniu nie ma konta");
    }

    // ================= hipotezy: stan po nieudanym logowaniu =================

    private static void TestPoNieudanymSprawdzeniuNieMaOczekiwania()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu
        {
            Odczyt = SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
        };
        using var koordynator = Koordynator(bramka, magazyn);
        koordynator.RestoreOnce();

        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("odmowa")));
        var start = Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        Assert(start.Snapshot.State == SonosAccountState.AwaitingBrowser, "po starcie czekamy na przegladarke");

        bramka.PlanujWynik(SonosLoginResultOutcome.Failure(SonosLoginStatus.Denied));
        var odmowa = Czekaj(koordynator.CheckLoginAsync(CancellationToken.None));
        Assert(!odmowa.StillWaiting && !odmowa.Connected, "odmowa nie jest ani oczekiwaniem, ani kontem");
        Assert(!odmowa.Snapshot.IsAwaitingBrowser, "po odmowie proba nie oczekuje juz na przegladarke");
        Assert(odmowa.Snapshot.State != SonosAccountState.AwaitingBrowser,
            "stan po odmowie NIE kaze dokonczyc logowania w przegladarce");

        // Ten sam blad po NIEUDANYM drugim rozpoczeciu: pierwsze Begin sie udalo.
        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("start-1")));
        Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        bramka.PlanujStart(SonosLoginStartOutcome.Failure(SonosLoginStatus.RateLimited));
        var drugiStart = Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        Assert(!drugiStart.Started, "drugie rozpoczecie sie nie udalo");
        Assert(!drugiStart.Snapshot.IsAwaitingBrowser, "po nieudanym rozpoczeciu nie ma na co czekac");
        Assert(drugiStart.Snapshot.State != SonosAccountState.AwaitingBrowser,
            "stan po nieudanym rozpoczeciu NIE kaze dokonczyc w przegladarce");
    }

    private static void TestSpoznionyPendingPoSukcesie()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu
        {
            Odczyt = SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
        };
        using var koordynator = Koordynator(bramka, magazyn);
        koordynator.RestoreOnce();
        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("dwa-sprawdzenia")));
        Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));

        // DWA sprawdzenia TEJ SAMEJ proby. Sukces przychodzi PIERWSZY,
        // spozniony Pending drugi.
        var bramaSukcesu = NowaBrama();
        var bramaPending = NowaBrama();
        bramka.PlanujWynik(SonosLoginResultOutcome.Ok(Tokeny("ACCESS-S", 3600, "RT-S")), bramaSukcesu);
        bramka.PlanujWynik(SonosLoginResultOutcome.Failure(SonosLoginStatus.Pending), bramaPending);
        var pierwsze = koordynator.CheckLoginAsync(CancellationToken.None);
        var drugie = koordynator.CheckLoginAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaWyniki(2, Limit), "oba sprawdzenia sa w toku");

        bramaSukcesu.TrySetResult(true);
        var sukces = Czekaj(pierwsze);
        Assert(sukces.Connected, "pierwsze sprawdzenie zainstalowalo konto");
        bramaPending.TrySetResult(true);
        var spozniony = Czekaj(drugie);

        Assert(!spozniony.StillWaiting,
            "spozniony Pending zakonczonej proby nie jest oczekiwaniem na przegladarke");
        Assert(!koordynator.Snapshot.IsAwaitingBrowser,
            "spozniony Pending nie wskrzesza oczekiwania po udanym logowaniu");
        Assert(koordynator.Snapshot.State == SonosAccountState.Connected,
            "konto pozostaje polaczone po spoznionym Pending");

        // Ta sama proba, ale spozniona ODMOWA po sukcesie: konta nie rusza.
        Assert(magazyn.LiczbaUsuniec == 0, "spozniona odpowiedz proby nie kasuje zapisu");
    }

    // ================= hipotezy: anulowanie i wspoldzielenie =================

    private static void TestOdnowienieZAnulowanymTokenem()
    {
        var bramka = new AtrapaBramki();
        using var koordynator = Koordynator(bramka, new AtrapaMagazynu());
        Polacz(koordynator, bramka, "ACCESS-A", "RT-A");

        using var zrodlo = new CancellationTokenSource();
        zrodlo.Cancel();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-NIEPOTRZEBNY", 3600, "RT-X")));
        var wynik = Czekaj(koordynator.RefreshAsync(zrodlo.Token));
        Assert(wynik.RefreshStatus == SonosRefreshStatus.Canceled, "rezygnacja od poczatku daje status Canceled");
        Assert(bramka.LiczbaOdnowien == 0,
            "wolajacy, ktory od poczatku zrezygnowal, NIE powoduje zapytania do bramki");
        Assert(koordynator.Snapshot.HasCredentials, "anulowanie nie rusza zestawu");
    }

    private static void TestRezygnacjaJednegoNieAnulujeInnym()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-W", "RT-W");

        var brama = NowaBrama();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-W2", 3600, "RT-W2")), brama);
        using var rezygnujacy = new CancellationTokenSource();
        var cierpliwy = koordynator.RefreshAsync(CancellationToken.None);
        var niecierpliwy = koordynator.RefreshAsync(rezygnujacy.Token);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "wspolne zapytanie jest w toku");

        rezygnujacy.Cancel();
        var porzucony = Czekaj(niecierpliwy);
        Assert(porzucony.WaiterCanceled, "rezygnujacy dostaje jawna informacje o wlasnej rezygnacji");
        Assert(porzucony.RefreshStatus == SonosRefreshStatus.Canceled, "rezygnujacy widzi Canceled");

        brama.TrySetResult(true);
        var wynik = Czekaj(cierpliwy);
        Assert(wynik.RefreshStatus == SonosRefreshStatus.Success,
            "rezygnacja JEDNEGO chetnego nie anuluje operacji pozostalym");
        Assert(wynik.Renewed, "pozostaly chetny dostaje nowy zestaw");
        Assert(magazyn.Ostatni?.Tokens.RefreshToken == "RT-W2", "nowy zestaw zostal zapisany");
    }

    private static void TestBramkaNiePodBlokada()
    {
        var bramka = new AtrapaBramki();
        using var koordynator = Koordynator(bramka, new AtrapaMagazynu());
        Polacz(koordynator, bramka, "ACCESS-L", "RT-L");

        // Bramka sprawdza OGRANICZONYM oczekiwaniem, czy inny watek moze
        // odczytac migawke w chwili, gdy trwa jej wywolanie. Blokada trzymana
        // przez czas wywolania bramki jest wlasnie zakazanym callbackiem pod lock.
        var brama = NowaBrama();
        var migawkaDostepna = false;
        bramka.PlanujOdnowienieCustom(async token =>
        {
            var probaMigawki = Task.Run(() => koordynator.Snapshot);
            migawkaDostepna = probaMigawki.Wait(TimeSpan.FromSeconds(2));
            await brama.Task.ConfigureAwait(false);
            return SonosRefreshOutcome.Ok(Tokeny("ACCESS-L2", 3600, "RT-L2"));
        });

        var zadanie = koordynator.RefreshAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "zapytanie do bramki wystartowalo");
        brama.TrySetResult(true);
        var wynik = Czekaj(zadanie);
        Assert(wynik.RefreshStatus == SonosRefreshStatus.Success, "odnowienie dokonczone");
        Assert(migawkaDostepna, "migawka jest osiagalna z innego watku w czasie wywolania bramki (brak lock)");
    }

    // ================= spoznione odpowiedzi =================

    private static void TestSpoznionySukcesNieNadpisuje()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-STARY", "RT-STARY");

        var brama = NowaBrama();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-SPOZNIONY", 3600, "RT-SPOZNIONY")), brama);
        var odnowienie = koordynator.RefreshAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "odnowienie w toku");

        // NOWE udane logowanie w trakcie: zmienia generacje zestawu.
        Polacz(koordynator, bramka, "ACCESS-NOWSZY", "RT-NOWSZY");
        var zapisyPoLogowaniu = magazyn.LiczbaZapisow;
        brama.TrySetResult(true);
        var wynik = Czekaj(odnowienie);
        Assert(wynik.Discarded, "spozniony sukces starszej generacji jest odrzucony");
        Assert(!wynik.Renewed, "spozniony sukces nie instaluje zestawu");
        Assert(magazyn.LiczbaZapisow == zapisyPoLogowaniu, "spozniony sukces niczego nie zapisuje");
        Assert(magazyn.Ostatni?.Tokens.RefreshToken == "RT-NOWSZY", "w magazynie stoi zestaw z NOWEGO logowania");
    }

    private static void TestSpoznione401PoWylogowaniu()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-401", "RT-401");

        var brama = NowaBrama();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Failure(SonosRefreshStatus.ReauthorizationRequired), brama);
        var odnowienie = koordynator.RefreshAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "odnowienie w toku");

        koordynator.Disconnect();
        var usunieciaPoWylogowaniu = magazyn.LiczbaUsuniec;
        brama.TrySetResult(true);
        var wynik = Czekaj(odnowienie);
        Assert(wynik.Discarded, "spoznione 401 starszej generacji jest odrzucone");
        Assert(magazyn.LiczbaUsuniec == usunieciaPoWylogowaniu, "spoznione 401 nie wykonuje dodatkowego usuniecia");
    }

    private static void TestInvalidRecordNieUdajeKonta()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu
        {
            Odczyt = SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing),
            Zapis = SonosCredentialWriteStatus.InvalidRecord
        };
        using var koordynator = Koordynator(bramka, magazyn);
        koordynator.RestoreOnce();
        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("odrzucony-rekord")));
        Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        bramka.PlanujWynik(SonosLoginResultOutcome.Ok(Tokeny("ACCESS-ODRZUCONY", 3600, "RT-ODRZUCONY")));
        var wynik = Czekaj(koordynator.CheckLoginAsync(CancellationToken.None));

        Assert(wynik.WriteStatus == SonosCredentialWriteStatus.InvalidRecord, "magazyn odrzucil rekord");
        Assert(!wynik.Connected, "odrzucony rekord NIE udaje polaczonego konta");
        Assert(!koordynator.Snapshot.HasCredentials, "odrzuconego rekordu nie ma w pamieci");
        Assert(koordynator.Snapshot.State == SonosAccountState.NeedsLogin, "trzeba zalogowac sie ponownie");
        Assert(koordynator.Snapshot.Issue == SonosAccountIssue.InvalidRecord, "przyczyna nazwana wprost");
        Assert(!koordynator.Snapshot.IsAwaitingBrowser, "odrzucony rekord nie zostawia oczekiwania");
        Assert(!koordynator.Snapshot.CanRetryPersist, "nie ma czego ponawiac zapisem");
    }

    private static void TestLogowanieNiePorzucaOdnowienia()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-R", "RT-R");

        var brama = NowaBrama();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-R2", 3600, "RT-R2")), brama);
        var odnowienie = koordynator.RefreshAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "odnowienie w toku");

        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("rownolegle-logowanie")));
        Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        koordynator.CancelPendingLogin();

        brama.TrySetResult(true);
        var wynik = Czekaj(odnowienie);
        Assert(!wynik.Discarded, "samo Begin i Cancel logowania NIE porzuca odnowienia biezacego konta");
        Assert(wynik.RefreshStatus == SonosRefreshStatus.Success, "odnowienie konczy sie sukcesem");
        Assert(magazyn.Ostatni?.Tokens.RefreshToken == "RT-R2", "odnowiony zestaw zostal zapisany");
        Assert(koordynator.Snapshot.State == SonosAccountState.Connected, "konto pozostaje polaczone");
    }

    // ================= znacznik podlaczenia konta (B2a) =================

    /// <summary>
    /// Pusty koordynator ma rozpoznawalny punkt wyjscia, a JEDNORAZOWE odtworzenie
    /// poprawnego zapisu daje STABILNY poczatkowy stan podlaczenia: odczyt wlasnego
    /// konta z dysku to nie zastapienie wczesniejszego konta.
    /// </summary>
    private static void TestZnacznikPoczatkowyJestStabilny()
    {
        var zapisany = new SonosStoredCredentials(BrokerOrigin, Tokeny("ACCESS-ODTWORZONY"), Zegar);
        var magazyn = new AtrapaMagazynu { Odczyt = SonosCredentialReadOutcome.Ok(zapisany) };
        using var koordynator = Koordynator(new AtrapaBramki(), magazyn);

        var puste = koordynator.Snapshot;
        Assert(puste.State == SonosAccountState.NoAccount, "nowy koordynator jest rozpoznawalnie pusty");
        var odniesienie = puste.AccountBindingGeneration;

        var pierwsze = koordynator.RestoreOnce();
        Assert(pierwsze.Snapshot.State == SonosAccountState.Connected, "kontrola: odtworzenie daje konto polaczone");
        Assert(pierwsze.Snapshot.AccountBindingGeneration == odniesienie,
            "odtworzenie WLASNEGO zapisu nie udaje zastapienia konta");

        var drugie = koordynator.RestoreOnce();
        Assert(!drugie.Performed, "kontrola: drugie odtworzenie nie czyta magazynu");
        Assert(drugie.Snapshot.AccountBindingGeneration == odniesienie,
            "powtorzone odtworzenie nie zmienia znacznika podlaczenia");
    }

    /// <summary>
    /// Rozpoczecie, anulowanie i NIEUDANE nowe logowanie nie zastepuja dzialajacego
    /// konta, wiec nie ruszaja jego znacznika podlaczenia.
    /// </summary>
    private static void TestNieudaneLogowanieNieZmieniaZnacznika()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-1", "RT-1");
        var podlaczenie = koordynator.Snapshot.AccountBindingGeneration;

        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("proba-anulowana")));
        Assert(Czekaj(koordynator.BeginLoginAsync(CancellationToken.None)).Started, "kontrola: nowa proba ruszyla");
        Assert(koordynator.Snapshot.AccountBindingGeneration == podlaczenie,
            "samo rozpoczecie nowego logowania nie zastepuje konta");

        koordynator.CancelPendingLogin();
        Assert(koordynator.Snapshot.AccountBindingGeneration == podlaczenie,
            "anulowanie nowego logowania nie zastepuje konta");

        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("proba-odmowiona")));
        Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        bramka.PlanujWynik(SonosLoginResultOutcome.Failure(SonosLoginStatus.Denied));
        var odmowa = Czekaj(koordynator.CheckLoginAsync(CancellationToken.None));
        Assert(odmowa.LoginStatus == SonosLoginStatus.Denied && !odmowa.Connected, "kontrola: proba odmowiona");
        Assert(koordynator.Snapshot.HasCredentials, "kontrola: stare konto dziala dalej");
        Assert(koordynator.Snapshot.AccountBindingGeneration == podlaczenie,
            "nieudane nowe logowanie nie zastepuje dzialajacego konta");
    }

    /// <summary>UDANE nowe logowanie przy JUZ istniejacym koncie to zastapienie konta.</summary>
    private static void TestUdaneNoweLogowanieZmieniaZnacznik()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-1", "RT-1");
        var podlaczenie = koordynator.Snapshot.AccountBindingGeneration;

        Polacz(koordynator, bramka, "ACCESS-2", "RT-2");
        Assert(koordynator.Snapshot.State == SonosAccountState.Connected, "kontrola: nowe konto jest polaczone");
        Assert(koordynator.Snapshot.AccountBindingGeneration != podlaczenie,
            "udane nowe logowanie instalujace zestaw zmienia znacznik podlaczenia");
        Assert(koordynator.Snapshot.AccountBindingGeneration > podlaczenie,
            "znacznik podlaczenia jest monotoniczny");
    }

    /// <summary>
    /// ZWYKLE odnowienie dostepu i rotacja zestawu NIE sa zmiana konta, choc
    /// CredentialGeneration zgodnie ze starym kontraktem rosnie.
    /// </summary>
    private static void TestOdnowienieNieZmieniaZnacznika()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-1", "RT-1");
        var przed = koordynator.Snapshot;

        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-R1", 3600, "RT-R1")));
        var pierwsze = Czekaj(koordynator.RefreshAsync(CancellationToken.None));
        Assert(pierwsze.Renewed, "kontrola: odnowienie sie udalo");
        Assert(pierwsze.Snapshot.CredentialGeneration != przed.CredentialGeneration,
            "kontrola: stary kontrakt generacji zestawu dziala bez zmian");
        Assert(pierwsze.Snapshot.AccountBindingGeneration == przed.AccountBindingGeneration,
            "zwykle odnowienie dostepu nie zmienia znacznika podlaczenia");

        bramka.PlanujOdnowienie(SonosRefreshOutcome.Ok(Tokeny("ACCESS-R2", 3600, "RT-R2")));
        var drugie = Czekaj(koordynator.RefreshAsync(CancellationToken.None));
        Assert(drugie.Renewed && magazyn.Ostatni?.Tokens.RefreshToken == "RT-R2", "kontrola: rotacja zestawu doszla");
        Assert(drugie.Snapshot.AccountBindingGeneration == przed.AccountBindingGeneration,
            "rotacja tokenu odswiezania nie zmienia znacznika podlaczenia");
    }

    /// <summary>
    /// JAWNE wylogowanie i REALNE usuniecie konta po dokladnym 401 zmieniaja znacznik,
    /// a spozniona odpowiedz starszej generacji NIE udaje zastapienia konta.
    /// </summary>
    private static void TestOdlaczenieZmieniaZnacznik()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-1", "RT-1");
        var poLogowaniu = koordynator.Snapshot.AccountBindingGeneration;

        bramka.PlanujOdnowienie(SonosRefreshOutcome.Failure(SonosRefreshStatus.ReauthorizationRequired));
        var reauth = Czekaj(koordynator.RefreshAsync(CancellationToken.None));
        Assert(reauth.Snapshot.State == SonosAccountState.NeedsLogin && !reauth.Snapshot.HasCredentials,
            "kontrola: 401 biezacej generacji usuwa konto");
        var poUniewaznieniu = reauth.Snapshot.AccountBindingGeneration;
        Assert(poUniewaznieniu > poLogowaniu, "realne usuniecie konta po 401 zmienia znacznik podlaczenia");

        var pusteWylogowanie = koordynator.Disconnect();
        Assert(pusteWylogowanie.Snapshot.AccountBindingGeneration == poUniewaznieniu,
            "wylogowanie bez konta nie udaje zastapienia konta");

        Polacz(koordynator, bramka, "ACCESS-2", "RT-2");
        var poDrugimLogowaniu = koordynator.Snapshot.AccountBindingGeneration;
        var wylogowanie = koordynator.Disconnect();
        Assert(wylogowanie.Disconnected, "kontrola: wylogowanie potwierdzone");
        Assert(wylogowanie.Snapshot.AccountBindingGeneration > poDrugimLogowaniu,
            "jawne wylogowanie zmienia znacznik podlaczenia");
    }

    /// <summary>
    /// Nieudane Delete NIE odwraca odlaczenia: konto przestaje byc uzywane, wiec
    /// znacznik i tak sie zmienia; spoznione 401 starszej generacji - nie.
    /// </summary>
    private static void TestSpoznioneWynikiNieUdajaZastapienia()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-1", "RT-1");

        var brama = NowaBrama();
        bramka.PlanujOdnowienie(SonosRefreshOutcome.Failure(SonosRefreshStatus.ReauthorizationRequired), brama);
        var odnowienie = koordynator.RefreshAsync(CancellationToken.None);
        Assert(bramka.PoczekajNaOdnowienia(1, Limit), "kontrola: odnowienie doszlo do bramki");

        Polacz(koordynator, bramka, "ACCESS-2", "RT-2");
        var poNowymLogowaniu = koordynator.Snapshot.AccountBindingGeneration;

        brama.TrySetResult(true);
        var spoznione = Czekaj(odnowienie);
        Assert(spoznione.Discarded, "kontrola: 401 starszej generacji jest odrzucone");
        Assert(koordynator.Snapshot.AccountBindingGeneration == poNowymLogowaniu,
            "spoznione 401 nie udaje zastapienia nowego konta");

        magazyn.UsuniecieUdane = false;
        var wylogowanie = koordynator.Disconnect();
        Assert(!wylogowanie.Disconnected && wylogowanie.Snapshot.PersistedRecordMayRemain,
            "kontrola: nieudane Delete nie potwierdza wylogowania");
        Assert(wylogowanie.Snapshot.AccountBindingGeneration > poNowymLogowaniu,
            "porzucone konto przy nieudanym Delete i tak konczy biezace podlaczenie");
    }

    /// <summary>Znacznik jest BEZPIECZNY: to sam licznik, bez tokenow i sekretow.</summary>
    private static void TestZnacznikNieNiesieSekretow()
    {
        var bramka = new AtrapaBramki();
        var magazyn = new AtrapaMagazynu();
        using var koordynator = Koordynator(bramka, magazyn);
        Polacz(koordynator, bramka, "ACCESS-TAJNY", "RT-TAJNY");
        var migawka = koordynator.Snapshot;
        var tekst = migawka.AccountBindingGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Assert(!tekst.Contains("ACCESS-TAJNY", StringComparison.Ordinal)
            && !tekst.Contains("RT-TAJNY", StringComparison.Ordinal)
            && !tekst.Contains(BrokerOrigin, StringComparison.Ordinal)
            && !tekst.Contains("playback-control-all", StringComparison.Ordinal),
            "znacznik podlaczenia nie cytuje tokenu, origin ani scope");
        Assert(!migawka.ToString().Contains("ACCESS-TAJNY", StringComparison.Ordinal)
            && !migawka.ToString().Contains("RT-TAJNY", StringComparison.Ordinal),
            "kontrola: migawka nadal nie cytuje tokenow");
        Assert(magazyn.LiczbaZapisow == 1,
            "znacznik nie dokłada zadnego nowego zapisu do magazynu konta");
    }

    // ================= pomocnicze (syntetyczne) =================

    private static SonosAccountCoordinator Koordynator(ISonosLoginGateway bramka, ISonosCredentialStore magazyn) =>
        new(bramka, magazyn, BrokerOrigin, () => Zegar);

    private static SonosTokens Tokeny(string accessToken, int? expiresIn = 3600, string? refreshToken = "RT") =>
        new(accessToken, "Bearer", expiresIn, refreshToken, "playback-control-all");

    private static TaskCompletionSource<bool> NowaBrama() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Doprowadza koordynator do konta polaczonego przez PELNA sciezke logowania.</summary>
    private static void Polacz(
        SonosAccountCoordinator koordynator,
        AtrapaBramki bramka,
        string accessToken,
        string? refreshToken)
    {
        bramka.PlanujStart(SonosLoginStartOutcome.Ok(Sesja("polacz-" + accessToken)));
        var start = Czekaj(koordynator.BeginLoginAsync(CancellationToken.None));
        Assert(start.Started, "przygotowanie: logowanie rozpoczete");
        bramka.PlanujWynik(SonosLoginResultOutcome.Ok(Tokeny(accessToken, 3600, refreshToken)));
        var check = Czekaj(koordynator.CheckLoginAsync(CancellationToken.None));
        Assert(check.Snapshot.HasCredentials, "przygotowanie: zestaw jest w pamieci");
    }

    /// <summary>
    /// PRAWDZIWA sesja logowania z <see cref="SonosLoginClient"/> i syntetycznym
    /// handlerem - bez publicznego szwu w produkcji i bez Reflection.
    /// </summary>
    private static SonosLoginSession Sesja(string ziarno)
    {
        SonosLoginBrokerConfiguration.TryCreate(BrokerOrigin, out var konfiguracja);
        if (konfiguracja is null)
        {
            throw new InvalidOperationException("Syntetyczny origin nie przeszedl walidacji.");
        }

        var sessionId = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(ziarno)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var cialo = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["session_id"] = sessionId,
            ["authorize_url"] = "https://api.sonos.com/login/v3/oauth?client_id=syntetyczny&response_type=code",
            ["expires_in"] = 600
        });

        using var handler = new JednorazowyHandler(cialo);
        using var klient = new SonosLoginClient(konfiguracja, handler, clock: () => Zegar);
        return klient.StartAsync(CancellationToken.None).GetAwaiter().GetResult().Session
            ?? throw new InvalidOperationException("Syntetyczny start nie wydal sesji.");
    }

    private static T Czekaj<T>(Task<T> zadanie)
    {
        if (!zadanie.Wait(Limit))
        {
            throw new InvalidOperationException("Operacja nie zakonczyla sie w limicie czasu.");
        }

        return zadanie.GetAwaiter().GetResult();
    }

    private static Exception Rozwin(Exception wyjatek) =>
        wyjatek is AggregateException agregat && agregat.InnerException is not null
            ? Rozwin(agregat.InnerException)
            : wyjatek;

    private static void Assert(bool warunek, string komunikat)
    {
        if (!warunek)
        {
            throw new InvalidOperationException(komunikat);
        }
    }

    /// <summary>Jedna zaplanowana odpowiedz startu logowania. Zero sieci.</summary>
    private sealed class JednorazowyHandler : HttpMessageHandler
    {
        private readonly string cialo;

        public JednorazowyHandler(string cialo) => this.cialo = cialo;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(cialo, Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
    }

    /// <summary>
    /// Atrapa magazynu: liczniki, przechowany rekord i kontrolowane wyniki.
    /// Synchroniczna, dokladnie jak kontrakt produkcyjny.
    /// </summary>
    private sealed class AtrapaMagazynu : ISonosCredentialStore
    {
        private readonly object gate = new();

        public SonosCredentialReadOutcome Odczyt { get; set; } =
            SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing);

        public SonosCredentialWriteStatus Zapis { get; set; } = SonosCredentialWriteStatus.Success;

        public bool UsuniecieUdane { get; set; } = true;

        public int LiczbaOdczytow { get; private set; }

        public int LiczbaZapisow { get; private set; }

        public int LiczbaUsuniec { get; private set; }

        public SonosStoredCredentials? Ostatni { get; private set; }

        public SonosCredentialReadOutcome Read()
        {
            lock (gate)
            {
                LiczbaOdczytow++;
                return Odczyt;
            }
        }

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            lock (gate)
            {
                LiczbaZapisow++;
                if (Zapis == SonosCredentialWriteStatus.Success)
                {
                    Ostatni = credentials;
                    return SonosCredentialWriteOutcome.Ok();
                }

                if (Zapis == SonosCredentialWriteStatus.WriteFailure)
                {
                    return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
                }

                return SonosCredentialWriteOutcome.Failure(Zapis);
            }
        }

        public bool Delete()
        {
            lock (gate)
            {
                LiczbaUsuniec++;
                return UsuniecieUdane;
            }
        }
    }

    /// <summary>
    /// Atrapa bramki logowania: kolejki zaplanowanych odpowiedzi, liczniki i
    /// BARIERY oparte o TaskCompletionSource. Nie ma tu zadnego usypiania watku.
    /// </summary>
    private sealed class AtrapaBramki : ISonosLoginGateway
    {
        private readonly object gate = new();
        private readonly Queue<Func<CancellationToken, Task<SonosLoginStartOutcome>>> starty = new();
        private readonly Queue<Func<CancellationToken, Task<SonosLoginResultOutcome>>> wyniki = new();
        private readonly Queue<Func<CancellationToken, Task<SonosRefreshOutcome>>> odnowienia = new();
        private readonly ManualResetEventSlim zmiana = new(false);

        public int LiczbaStartow { get; private set; }

        public int LiczbaWynikow { get; private set; }

        public int LiczbaOdnowien { get; private set; }

        public List<string?> UzyteTokenyOdswiezania { get; } = new();

        public void PlanujStart(SonosLoginStartOutcome outcome, TaskCompletionSource<bool>? brama = null)
        {
            lock (gate)
            {
                starty.Enqueue(async _ =>
                {
                    if (brama is not null)
                    {
                        await brama.Task.ConfigureAwait(false);
                    }

                    return outcome;
                });
            }
        }

        public void PlanujWynik(SonosLoginResultOutcome outcome, TaskCompletionSource<bool>? brama = null)
        {
            lock (gate)
            {
                wyniki.Enqueue(async _ =>
                {
                    if (brama is not null)
                    {
                        await brama.Task.ConfigureAwait(false);
                    }

                    return outcome;
                });
            }
        }

        public void PlanujOdnowienie(SonosRefreshOutcome outcome, TaskCompletionSource<bool>? brama = null)
        {
            lock (gate)
            {
                odnowienia.Enqueue(async _ =>
                {
                    if (brama is not null)
                    {
                        await brama.Task.ConfigureAwait(false);
                    }

                    return outcome;
                });
            }
        }

        public void PlanujOdnowienieCustom(Func<CancellationToken, Task<SonosRefreshOutcome>> fabryka)
        {
            lock (gate)
            {
                odnowienia.Enqueue(fabryka);
            }
        }

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<SonosLoginStartOutcome>> zaplanowany;
            lock (gate)
            {
                LiczbaStartow++;
                zaplanowany = starty.Count > 0
                    ? starty.Dequeue()
                    : throw new InvalidOperationException("Atrapa bramki: brak zaplanowanego startu.");
            }

            Obwiesc();
            return zaplanowany(cancellationToken);
        }

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(session);
            Func<CancellationToken, Task<SonosLoginResultOutcome>> zaplanowany;
            lock (gate)
            {
                LiczbaWynikow++;
                zaplanowany = wyniki.Count > 0
                    ? wyniki.Dequeue()
                    : throw new InvalidOperationException("Atrapa bramki: brak zaplanowanego wyniku.");
            }

            Obwiesc();
            return zaplanowany(cancellationToken);
        }

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task<SonosRefreshOutcome>> zaplanowany;
            lock (gate)
            {
                LiczbaOdnowien++;
                UzyteTokenyOdswiezania.Add(refreshToken);
                zaplanowany = odnowienia.Count > 0
                    ? odnowienia.Dequeue()
                    : throw new InvalidOperationException("Atrapa bramki: brak zaplanowanego odnowienia.");
            }

            Obwiesc();
            return zaplanowany(cancellationToken);
        }

        public bool PoczekajNaOdnowienia(int ile, TimeSpan limit) => Poczekaj(() => LiczbaOdnowien >= ile, limit);

        public bool PoczekajNaWyniki(int ile, TimeSpan limit) => Poczekaj(() => LiczbaWynikow >= ile, limit);

        private bool Poczekaj(Func<bool> warunek, TimeSpan limit)
        {
            var koniec = DateTime.UtcNow + limit;
            while (DateTime.UtcNow < koniec)
            {
                lock (gate)
                {
                    if (warunek())
                    {
                        return true;
                    }
                }

                zmiana.Reset();
                zmiana.Wait(TimeSpan.FromMilliseconds(50));
            }

            lock (gate)
            {
                return warunek();
            }
        }

        private void Obwiesc() => zmiana.Set();
    }
}
