using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Aktualizacja calego AMC: decyzja, wpis czekajacej paczki i posrednik, ktory
/// uruchamia instalator po zamknieciu programu.
///
/// KAZDY test pracuje na WLASNYM katalogu tymczasowym. Produkcyjny katalog
/// %LOCALAPPDATA%\AccessibleMediaController\updates nie jest tu dotykany ani
/// czytany - test, ktory kasuje czekajaca aktualizacje uzytkownika, jest gorszy
/// niz brak testu.
///
/// Instalator jest UDAWANY kontrolowanym skryptem .cmd, ktory dopisuje wiersz do
/// dziennika i konczy sie zadanym kodem. Zaden prawdziwy instalator AMC nie jest
/// uruchamiany, zaden plik programu nie jest podmieniany.
/// </summary>
internal static class ApplicationUpdateManagerTests
{
    internal static void Run()
    {
        TestBrakSumyKontrolnejBlokujePobranie();
        TestGotowaPaczkaJestUznawanaBezPonownegoPobrania();
        TestCzekajacaAktualizacjaJestSprawdzana();
        TestAnulowaniePrzygotowaniaNieZapisujePaczki();
        TestPosrednikInstalatoraPilnujeKolejnosciIWpisu();
        TestSciezkiWpisuSaOgraniczoneDoKataloguAktualizacji();
        TestTloNieNadpisujeRecznejZgodyUzytkownika();
        TestStartSprawdzaTenSamRekordKtoryUruchamia();
        Console.WriteLine("OK: aktualizacja AMC sprawdza sume, pomija zbedne pobranie i uruchamia instalator w kolejnosci");
    }

    /// <summary>
    /// Zgoda, numer wersji, suma SHA i sciezki musza byc sprawdzone na TYM SAMYM
    /// rekordzie, ktorego dane potem ida do uruchomienia.
    ///
    /// Wczesniej MainWindow robil HasPendingUpdate(...) &amp;&amp; TryStartPendingInstall(...):
    /// pierwsze wczytanie sprawdzalo wszystko, drugie wczytywalo plik JESZCZE RAZ
    /// i sprawdzalo wylacznie sciezki. Miedzy jednym a drugim wpis mogl sie
    /// zmienic na reczny (bez zgody na instalacje przy zamknieciu), niesprawdzony
    /// albo STARSZY od uruchomionej wersji - i taki rekord i tak sie wykonywal.
    ///
    /// Nic tu nie startuje naprawde: uruchomienie idzie przez wstrzykniety
    /// callback, ktory tylko zapisuje, co dostal.
    /// </summary>
    private static void TestStartSprawdzaTenSamRekordKtoryUruchamia()
    {
        using var katalog = new TempDirectory();
        var root = Path.Combine(katalog.Path, "updates");
        Directory.CreateDirectory(root);
        var instalator = Path.Combine(root, "amc-setup.exe");
        File.WriteAllText(instalator, "udawany instalator AMC");
        var suma = Sha256(instalator);
        const string zainstalowana = "0.1.0-alpha.395";

        var starty = new List<ApplicationUpdateManager.PendingInstallPlan>();
        bool Start(ApplicationUpdateManager.PendingInstallPlan plan, ProcessStartInfo _)
        {
            starty.Add(plan);
            return true;
        }

        // (c) Poprawny przypadek: startuje DOKLADNIE raz, z danymi z wpisu.
        Zapisz(root, "0.1.0-alpha.400", instalator, suma, allowInstallOnExit: true);
        starty.Clear();
        Assert(ApplicationUpdateManager.TryStartPendingInstall(
                root, zainstalowana, allowManualOnlyPackage: false, relaunch: true, visible: false, start: Start),
            "Poprawna, sprawdzona paczka nie wystartowala.");
        Assert(starty.Count == 1, $"Poprawna paczka wystartowala {starty.Count} razy zamiast raz.");
        Assert(starty[0].Version == "0.1.0-alpha.400" && starty[0].Sha256 == suma
               && starty[0].InstallerPath == instalator,
            "Snapshot uruchomienia nie zawiera wersji, sumy i sciezki ze sprawdzonego wpisu.");

        // (a) Stary HasPendingUpdate dawal true; po podmianie JSON na wpis bez
        //     zgody / niesprawdzony / starszy start NIE MOZE nic uruchomic.
        var warianty = new (string Opis, Action Podmien, bool AllowManualOnly)[]
        {
            ("wpis reczny (AllowInstallOnExit=false) w trybie automatycznym",
                () => Zapisz(root, "0.1.0-alpha.400", instalator, suma, allowInstallOnExit: false), false),
            ("wpis niezweryfikowany (ChecksumVerified=false)",
                () => ApplicationUpdateManager.SavePending(root, new ApplicationUpdateManager.PendingUpdate
                {
                    Version = "0.1.0-alpha.400",
                    Sha256 = suma,
                    ChecksumVerified = false,
                    InstallerPath = instalator,
                    TargetDirectory = root,
                    AllowInstallOnExit = true,
                    PreparedAtUtc = DateTimeOffset.UtcNow
                }), true),
            ("wpis STARSZY od uruchomionej wersji",
                () => Zapisz(root, "0.1.0-alpha.300", instalator, suma, allowInstallOnExit: true), true),
            ("wpis z niezgodna suma SHA-256",
                () => Zapisz(root, "0.1.0-alpha.400", instalator, new string('a', 64), allowInstallOnExit: true), true)
        };

        foreach (var wariant in warianty)
        {
            wariant.Podmien();
            starty.Clear();
            var wystartowal = ApplicationUpdateManager.TryStartPendingInstall(
                root,
                zainstalowana,
                allowManualOnlyPackage: wariant.AllowManualOnly,
                relaunch: wariant.AllowManualOnly,
                visible: false,
                start: Start);
            Assert(!wystartowal && starty.Count == 0,
                $"Start instalacji przyjal {wariant.Opis} — walidacja i wykonanie nie dotycza tego samego rekordu.");
        }

        // Tryb JAWNY ma prawo uruchomic paczke reczna (AllowInstallOnExit=false).
        Zapisz(root, "0.1.0-alpha.400", instalator, suma, allowInstallOnExit: false);
        starty.Clear();
        Assert(ApplicationUpdateManager.TryStartPendingInstall(
                   root, zainstalowana, allowManualOnlyPackage: true, relaunch: true, visible: true, start: Start)
               && starty.Count == 1,
            "Jawne zadanie uzytkownika nie uruchomilo paczki przygotowanej recznie.");

        // (b) Podmiana JSON JUZ PO walidacji nie zmienia przekazanych danych:
        //     snapshot jest niezmienny, a pliku nie czytamy ponownie.
        Zapisz(root, "0.1.0-alpha.400", instalator, suma, allowInstallOnExit: true);
        ApplicationUpdateManager.PendingInstallPlan? wPodmianie = null;
        Assert(ApplicationUpdateManager.TryStartPendingInstall(
                root,
                zainstalowana,
                allowManualOnlyPackage: false,
                relaunch: true,
                visible: false,
                start: (plan, _) =>
                {
                    // Wpis zmienia sie DOKLADNIE w momencie uruchamiania.
                    Zapisz(root, "0.1.0-alpha.300", Path.Combine(root, "inny.exe"), new string('b', 64),
                        allowInstallOnExit: false);
                    wPodmianie = plan;
                    return true;
                }),
            "Poprawny wpis nie wystartowal w tescie podmiany w trakcie.");
        Assert(wPodmianie is not null
               && wPodmianie.Version == "0.1.0-alpha.400"
               && wPodmianie.Sha256 == suma
               && wPodmianie.InstallerPath == instalator,
            "Podmiana pending.json w trakcie uruchamiania zmienila wersje, sume albo sciezke przekazana do instalacji.");
    }

    private static void TestBrakSumyKontrolnejBlokujePobranie()
    {
        using var katalog = new TempDirectory();
        var release = new ApplicationRelease(
            "0.1.0-alpha.400",
            "Wydanie bez sumy kontrolnej.",
            new Uri("https://127.0.0.1:9/amc-setup.exe"),
            "amc-setup.exe",
            1024,
            IsPrerelease: true,
            PackageIsInstaller: true);
        var plan = new ApplicationUpdatePlan(
            ApplicationUpdateDecision.UpdateAvailable,
            "Dostępna jest nowsza wersja AMC.",
            release,
            ExpectedSha256: null);

        var status = ApplicationUpdateManager
            .DecideAsync(katalog.Path, "0.1.0-alpha.395", plan, downloadAutomatically: true, allowInstallOnExit: true)
            .GetAwaiter()
            .GetResult();

        Assert(status.Decision == ApplicationUpdateDecision.NotUnderstood,
            $"Wydanie bez sumy kontrolnej nadal prowadzi do instalacji: {status.Decision}.");
        Assert(status.Message.Contains("sumy kontrolnej", StringComparison.OrdinalIgnoreCase),
            $"Komunikat nie mówi wprost o braku sumy kontrolnej: {status.Message}.");
        Assert(!status.ReadyToInstall && !status.ChecksumVerified,
            "Paczka bez sumy została ogłoszona jako gotowa albo sprawdzona.");
        Assert(!Directory.Exists(katalog.Path) || Directory.GetFileSystemEntries(katalog.Path).Length == 0,
            "Mimo braku sumy kontrolnej coś zostało pobrane lub zapisane.");
    }

    private static void TestGotowaPaczkaJestUznawanaBezPonownegoPobrania()
    {
        using var katalog = new TempDirectory();
        var instalator = Path.Combine(katalog.Path, "amc-setup.exe");
        File.WriteAllText(instalator, "udawany instalator AMC");
        var suma = Sha256(instalator);
        ApplicationUpdateManager.SavePending(katalog.Path, new ApplicationUpdateManager.PendingUpdate
        {
            Version = "0.1.0-alpha.400",
            Sha256 = suma,
            ChecksumVerified = false,
            InstallerPath = instalator,
            TargetDirectory = katalog.Path,
            AllowInstallOnExit = false,
            PreparedAtUtc = DateTimeOffset.UtcNow
        });
        var zapisane = File.GetLastWriteTimeUtc(instalator);

        var release = new ApplicationRelease(
            "0.1.0-alpha.400",
            $"amc-setup.exe {suma}",
            new Uri("https://127.0.0.1:9/amc-setup.exe"),
            "amc-setup.exe",
            1024,
            IsPrerelease: true,
            PackageIsInstaller: true);
        var plan = new ApplicationUpdatePlan(
            ApplicationUpdateDecision.UpdateAvailable,
            "Dostępna jest nowsza wersja AMC.",
            release,
            suma);

        var status = ApplicationUpdateManager
            .DecideAsync(katalog.Path, "0.1.0-alpha.395", plan, downloadAutomatically: false, allowInstallOnExit: false)
            .GetAwaiter()
            .GetResult();

        Assert(status.ReadyToInstall,
            "Gotowa, sprawdzona paczka nie została uznana za gotową do instalacji.");
        Assert(status.ChecksumVerified,
            "Suma gotowej paczki nie została policzona ponownie i potwierdzona.");
        Assert(ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out _),
            "Po sprawdzeniu starego pliku wobec sumy z wydania nie zapisano potwierdzenia weryfikacji.");
        Assert(status.AvailableVersion == "0.1.0-alpha.400",
            $"Zgubiono numer gotowego wydania: {status.AvailableVersion}.");
        Assert(File.GetLastWriteTimeUtc(instalator) == zapisane,
            "Gotowa paczka została pobrana po raz drugi.");

        // Suma kontrolna znaleziona w opisie, ale NIC nie pobrane i nic nie
        // przygotowane, to nie jest sprawdzona suma - tylko obietnica wydawcy.
        var pusty = new TempDirectory();
        using (pusty)
        {
            var bezPaczki = ApplicationUpdateManager
                .DecideAsync(pusty.Path, "0.1.0-alpha.395", plan, downloadAutomatically: false, allowInstallOnExit: true)
                .GetAwaiter()
                .GetResult();
            Assert(!bezPaczki.ChecksumVerified,
                "Sama obecność sumy w opisie wydania została zgłoszona jako sprawdzona suma pliku.");
            Assert(!bezPaczki.ReadyToInstall,
                "Niepobrana paczka została ogłoszona jako gotowa do instalacji.");
        }
    }

    private static void TestCzekajacaAktualizacjaJestSprawdzana()
    {
        using var katalog = new TempDirectory();
        var instalator = Path.Combine(katalog.Path, "amc-setup.exe");
        File.WriteAllText(instalator, "udawany instalator AMC");
        var suma = Sha256(instalator);

        Zapisz(katalog.Path, "0.1.0-alpha.400", instalator, suma, allowInstallOnExit: true);
        Assert(ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out var wersja)
               && wersja == "0.1.0-alpha.400",
            "Poprawna czekająca aktualizacja nie została rozpoznana.");
        Assert(ApplicationUpdateManager.HasPendingUpdate(
                   katalog.Path, "0.1.0-alpha.395", out _, includeManualRequests: false),
            "Aktualizacja przygotowana w tle została pominięta przy zamykaniu programu.");

        Zapisz(katalog.Path, "0.1.0-alpha.400", instalator, suma, allowInstallOnExit: false);
        Assert(ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out _),
            "Paczka przygotowana na żądanie użytkownika zniknęła z widoku okna aktualizacji.");
        Assert(!ApplicationUpdateManager.HasPendingUpdate(
                   katalog.Path, "0.1.0-alpha.395", out _, includeManualRequests: false),
            "Paczka odłożona przez użytkownika zainstalowałaby się sama przy zamknięciu programu.");

        Zapisz(katalog.Path, "0.1.0-alpha.390", instalator, suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out _),
            "Stara czekająca paczka cofnęłaby AMC do wcześniejszej wersji.");

        Zapisz(katalog.Path, "0.1.0-alpha.400", instalator, new string('a', 64), allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out _),
            "Paczka o innej sumie SHA-256 została uznana za gotową do uruchomienia.");

        ApplicationUpdateManager.SavePending(katalog.Path, new ApplicationUpdateManager.PendingUpdate
        {
            Version = "0.1.0-alpha.400", InstallerPath = instalator, Sha256 = suma,
            ChecksumVerified = false, AllowInstallOnExit = true
        });
        Assert(!ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out _),
            "Stary niesprawdzony plik został uznany za zaufany tylko dlatego, że pasuje do własnej zapisanej sumy.");

        Zapisz(katalog.Path, "0.1.0-alpha.400", Path.Combine(katalog.Path, "nie-ma.exe"), suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(katalog.Path, "0.1.0-alpha.395", out _),
            "Wpis wskazujący nieistniejący instalator został uznany za gotową aktualizację.");
    }

    /// <summary>
    /// Wpis pending.json jest zwyklym plikiem konta uzytkownika: moze zostac
    /// uszkodzony albo podmieniony. To NIE jest obrona przed dowolnym programem
    /// dzialajacym na tym samym koncie - suma SHA sprawdza calosc pliku, nie
    /// jego pochodzenie. Chodzi o to, zeby BLEDNY albo ZMIENIONY wpis nie
    /// wyprowadzil AMC poza wlasny katalog aktualizacji: sciezka instalatora
    /// musi lezec w zaufanym updateRoot, a nie gdziekolwiek na dysku.
    ///
    /// Test pracuje wylacznie na wlasnym katalogu tymczasowym.
    /// </summary>
    private static void TestSciezkiWpisuSaOgraniczoneDoKataloguAktualizacji()
    {
        using var katalog = new TempDirectory();
        var root = Path.Combine(katalog.Path, "updates");
        Directory.CreateDirectory(root);

        var wlasciwy = Path.Combine(root, "amc-setup.exe");
        File.WriteAllText(wlasciwy, "udawany instalator AMC");
        var suma = Sha256(wlasciwy);

        // Odniesienie: poprawny wpis w katalogu aktualizacji ma dzialac dalej.
        Zapisz(root, "0.1.0-alpha.400", wlasciwy, suma, allowInstallOnExit: true);
        Assert(ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out var wersja)
               && wersja == "0.1.0-alpha.400",
            "Poprawny wpis w katalogu aktualizacji przestal byc rozpoznawany.");

        // Podkatalog updateRoot tez jest w srodku.
        var podkatalog = Path.Combine(root, "gotowe");
        Directory.CreateDirectory(podkatalog);
        var wPodkatalogu = Path.Combine(podkatalog, "amc-setup.exe");
        File.Copy(wlasciwy, wPodkatalogu, overwrite: true);
        Zapisz(root, "0.1.0-alpha.400", wPodkatalogu, suma, allowInstallOnExit: true);
        Assert(ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Instalator w podkatalogu katalogu aktualizacji zostal odrzucony.");

        // 1. Plik poza updateRoot - istnieje i ma zgodna sume, a mimo to nie
        //    wolno go uznac za przygotowana aktualizacje.
        var obcy = Path.Combine(katalog.Path, "obcy", "amc-setup.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(obcy)!);
        File.Copy(wlasciwy, obcy, overwrite: true);
        Zapisz(root, "0.1.0-alpha.400", obcy, suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Wpis wskazujacy plik POZA katalogiem aktualizacji zostal uznany za gotowa aktualizacje.");

        // 2. Sztuczny prefiks: katalog obok, ktorego nazwa zaczyna sie tak samo.
        var podszywka = Path.Combine(katalog.Path, "updates-evil");
        Directory.CreateDirectory(podszywka);
        var podszywkaExe = Path.Combine(podszywka, "amc-setup.exe");
        File.Copy(wlasciwy, podszywkaExe, overwrite: true);
        Zapisz(root, "0.1.0-alpha.400", podszywkaExe, suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Katalog o nazwie zaczynajacej sie jak katalog aktualizacji zostal uznany za jego wnetrze.");

        // 3. Wyjscie w gore przez '..' wewnatrz sciezki.
        var wyjscie = Path.Combine(root, "..", "obcy", "amc-setup.exe");
        Zapisz(root, "0.1.0-alpha.400", wyjscie, suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Sciezka wychodzaca z katalogu aktualizacji przez '..' zostala przyjeta.");

        // 4. Sciezka wzgledna i sciezka z niedozwolonymi znakami - odrzucenie
        //    bez wyjatku przy zamykaniu programu.
        Zapisz(root, "0.1.0-alpha.400", "amc-setup.exe", suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Sciezka wzgledna zostala przyjeta jako instalator w katalogu aktualizacji.");

        Zapisz(root, "0.1.0-alpha.400", "C:\\nie|ma\\amc-setup.exe", suma, allowInstallOnExit: true);
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Sciezka z niedozwolonymi znakami nie zostala odrzucona spokojnie.");

        // 5. Wariant ZIP: katalog zrodlowy tez musi lezec w updateRoot.
        var obcyZrodlowy = Path.Combine(katalog.Path, "obce-zrodlo");
        Directory.CreateDirectory(obcyZrodlowy);
        ApplicationUpdateManager.SavePending(root, new ApplicationUpdateManager.PendingUpdate
        {
            Version = "0.1.0-alpha.400",
            Sha256 = suma,
            ChecksumVerified = true,
            InstallerPath = "",
            SourceDirectory = obcyZrodlowy,
            TargetDirectory = katalog.Path,
            AllowInstallOnExit = true,
            PreparedAtUtc = DateTimeOffset.UtcNow
        });
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Katalog zrodlowy paczki ZIP spoza katalogu aktualizacji zostal przyjety.");

        // JSON moze byc skladniowo poprawny i miec null zamiast sciezki.
        File.WriteAllText(Path.Combine(root, "pending.json"),
            """{"Version":"0.1.0-alpha.400","ChecksumVerified":true,"InstallerPath":null}""");
        Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
            "Null zamiast sciezki instalatora zostal przyjety.");

        // 6. Uszkodzony JSON: cisza i falsz, nie wyjatek przy zamykaniu.
        File.WriteAllText(Path.Combine(root, "pending.json"), "{to nie jest json");
        var padlo = false;
        try
        {
            Assert(!ApplicationUpdateManager.HasPendingUpdate(root, "0.1.0-alpha.395", out _),
                "Uszkodzony wpis zostal uznany za gotowa aktualizacje.");
        }
        catch (JsonException)
        {
            padlo = true;
        }

        Assert(!padlo, "Uszkodzony pending.json wyrzucil wyjatek zamiast zostac pominiety.");
    }

    /// <summary>
    /// Asercja DOWODOWA istniejacego zachowania, nie poprawka: przeglad
    /// sugerowal, ze przygotowanie w tle (allowInstallOnExit: true) nadpisze
    /// recznie odlozona zgode uzytkownika (AllowInstallOnExit = false) na tej
    /// samej, juz pobranej wersji. Mierzymy to wprost, czytajac zapisany JSON.
    /// </summary>
    private static void TestTloNieNadpisujeRecznejZgodyUzytkownika()
    {
        using var katalog = new TempDirectory();
        var instalator = Path.Combine(katalog.Path, "amc-setup.exe");
        File.WriteAllText(instalator, "udawany instalator AMC");
        var suma = Sha256(instalator);

        ApplicationUpdateManager.SavePending(katalog.Path, new ApplicationUpdateManager.PendingUpdate
        {
            Version = "0.1.0-alpha.400",
            Sha256 = suma,
            ChecksumVerified = true,
            InstallerPath = instalator,
            TargetDirectory = katalog.Path,
            AllowInstallOnExit = false,
            PreparedAtUtc = DateTimeOffset.UtcNow
        });

        var release = new ApplicationRelease(
            "0.1.0-alpha.400",
            $"amc-setup.exe {suma}",
            new Uri("https://127.0.0.1:9/amc-setup.exe"),
            "amc-setup.exe",
            1024,
            IsPrerelease: true,
            PackageIsInstaller: true);
        var plan = new ApplicationUpdatePlan(
            ApplicationUpdateDecision.UpdateAvailable,
            "Dostępna jest nowsza wersja AMC.",
            release,
            suma);

        var status = ApplicationUpdateManager
            .DecideAsync(katalog.Path, "0.1.0-alpha.395", plan, downloadAutomatically: true, allowInstallOnExit: true)
            .GetAwaiter()
            .GetResult();

        Assert(status.ReadyToInstall, "Gotowa reczna paczka przestala byc widziana jako gotowa.");

        var zapisane = JsonSerializer.Deserialize<ApplicationUpdateManager.PendingUpdate>(
            File.ReadAllText(Path.Combine(katalog.Path, "pending.json")));
        Assert(zapisane is not null, "Po sprawdzeniu w tle nie da sie odczytac wpisu pending.json.");
        Assert(!zapisane!.AllowInstallOnExit,
            "Sprawdzenie w tle nadpisalo reczna decyzje uzytkownika i paczka zainstalowalaby sie sama przy zamknieciu.");
        Assert(!ApplicationUpdateManager.HasPendingUpdate(
                   katalog.Path, "0.1.0-alpha.395", out _, includeManualRequests: false),
            "Reczna paczka mimo wszystko weszla na sciezke automatycznej instalacji przy zamykaniu.");
    }

    private static void TestAnulowaniePrzygotowaniaNieZapisujePaczki()
    {
        using var katalog = new TempDirectory();
        var release = new ApplicationRelease(
            "0.1.0-alpha.400",
            "opis",
            new Uri("https://127.0.0.1:9/amc-setup.exe"),
            "amc-setup.exe",
            1024,
            IsPrerelease: true,
            PackageIsInstaller: true);
        var plan = new ApplicationUpdatePlan(
            ApplicationUpdateDecision.UpdateAvailable,
            "Dostępna jest nowsza wersja AMC.",
            release,
            new string('b', 64));

        using var anulowane = new CancellationTokenSource();
        anulowane.Cancel();
        var przerwane = false;
        try
        {
            _ = ApplicationUpdateManager
                .DecideAsync(
                    katalog.Path,
                    "0.1.0-alpha.395",
                    plan,
                    downloadAutomatically: true,
                    allowInstallOnExit: true,
                    progress: null,
                    cancellationToken: anulowane.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            przerwane = true;
        }

        Assert(przerwane, "Anulowane przygotowanie aktualizacji zakończyło się jak udane.");
        Assert(!File.Exists(Path.Combine(katalog.Path, "pending.json")),
            "Po anulowaniu zapisano paczkę jako czekającą na instalację.");
    }

    private static void TestPosrednikInstalatoraPilnujeKolejnosciIWpisu()
    {
        // Powodzenie: stary proces musi zniknac PRZED instalatorem, a program
        // wystartowac PO nim. Kolejnosc czytamy z dziennika, nie z zalozen.
        var powodzenie = UruchomPosrednika(
            instalatorKod: 0, relaunch: true, czekajSekund: 15, zyjeDlugo: false, oczekiwaneWiersze: 3);
        Assert(powodzenie.Kod == 0, $"Poprawny przebieg instalacji zakończył się kodem {powodzenie.Kod}. {powodzenie.Dziennik}");
        Assert(powodzenie.Wiersze.Count == 3
               && powodzenie.Wiersze[0] == "stary-proces-koniec"
               && powodzenie.Wiersze[1] == "instalator"
               && powodzenie.Wiersze[2] == "relaunch",
            $"Zła kolejność cyklu instalacji: {string.Join(" -> ", powodzenie.Wiersze)}. {powodzenie.Dziennik}");
        Assert(!File.Exists(powodzenie.PendingPath),
            "Po udanej instalacji został wpis o czekającej aktualizacji.");

        // Stary proces zyje: instalator NIE MOZE wystartowac, a wpis musi zostac,
        // zeby aktualizacja dala sie powtorzyc.
        var zyje = UruchomPosrednika(instalatorKod: 0, relaunch: true, czekajSekund: 2, zyjeDlugo: true);
        Assert(zyje.Kod != 0, "Żyjący program nie zablokował uruchomienia instalatora.");
        Assert(!zyje.Wiersze.Contains("instalator"),
            $"Instalator wystartował przy żywym AMC: {string.Join(" -> ", zyje.Wiersze)}.");
        Assert(File.Exists(zyje.PendingPath),
            "Przerwana instalacja usunęła wpis, więc aktualizacji nie da się powtórzyć.");

        // Zla suma po oczekiwaniu: plik mogl zostac podmieniony miedzy pobraniem
        // i uruchomieniem, wiec sprawdzamy go PONOWNIE.
        var zlaSuma = UruchomPosrednika(
            instalatorKod: 0, relaunch: true, czekajSekund: 15, zyjeDlugo: false, sumaWLauncherze: new string('c', 64));
        Assert(zlaSuma.Kod != 0, "Instalator o niezgodnej sumie SHA-256 został uruchomiony.");
        Assert(!zlaSuma.Wiersze.Contains("instalator"),
            $"Niesprawdzony plik został wykonany: {string.Join(" -> ", zlaSuma.Wiersze)}.");
        Assert(File.Exists(zlaSuma.PendingPath), "Niezgodna suma usunęła wpis o czekającej aktualizacji.");

        // Instalator konczy sie bledem: brak ponownego uruchomienia programu i
        // zachowany wpis.
        var blad = UruchomPosrednika(instalatorKod: 3, relaunch: true, czekajSekund: 15, zyjeDlugo: false);
        Assert(blad.Kod != 0, "Nieudana instalacja zgłosiła sukces.");
        Assert(blad.Wiersze.Contains("instalator") && !blad.Wiersze.Contains("relaunch"),
            $"Po nieudanej instalacji program został uruchomiony ponownie: {string.Join(" -> ", blad.Wiersze)}.");
        Assert(File.Exists(blad.PendingPath), "Nieudana instalacja usunęła wpis o czekającej aktualizacji.");

        // Wpis o INNEJ wersji nie nalezy do tej instalacji - nie wolno go ruszac.
        var inny = UruchomPosrednika(
            instalatorKod: 0, relaunch: false, czekajSekund: 15, zyjeDlugo: false, wersjaWLauncherze: "0.1.0-alpha.777");
        Assert(inny.Kod == 0, $"Poprawna instalacja bez ponownego uruchomienia zwróciła {inny.Kod}. {inny.Dziennik}");
        Assert(!inny.Wiersze.Contains("relaunch"),
            "Program uruchomiono ponownie, choć nie o to proszono.");
        Assert(File.Exists(inny.PendingPath),
            "Usunięto wpis czekającej aktualizacji należący do innej wersji.");
    }

    private static Przebieg UruchomPosrednika(
        int instalatorKod,
        bool relaunch,
        int czekajSekund,
        bool zyjeDlugo,
        string? sumaWLauncherze = null,
        string? wersjaWLauncherze = null,
        int oczekiwaneWiersze = 0)
    {
        var katalog = new TempDirectory();
        try
        {
            var dziennikCyklu = Path.Combine(katalog.Path, "cykl.log");
            var skrypt = Path.Combine(katalog.Path, "uruchom-instalator.ps1");
            File.WriteAllText(
                skrypt,
                ApplicationUpdateManager.BuildInstallerLauncherScript(),
                new UTF8Encoding(true));

            var instalator = Path.Combine(katalog.Path, "amc-setup.cmd");
            File.WriteAllText(
                instalator,
                "@echo off\r\n"
                + $">>\"{dziennikCyklu}\" echo instalator\r\n"
                + $"exit /b {instalatorKod}\r\n",
                Encoding.ASCII);
            var program = Path.Combine(katalog.Path, "AccessibleMediaController.cmd");
            File.WriteAllText(
                program,
                "@echo off\r\n" + $">>\"{dziennikCyklu}\" echo relaunch\r\n" + "exit /b 0\r\n",
                Encoding.ASCII);

            var suma = Sha256(instalator);
            var wersja = "0.1.0-alpha.400";
            Zapisz(katalog.Path, wersja, instalator, suma, allowInstallOnExit: true, program);

            // Udawany "stary AMC": konczy sie sam i zostawia slad w dzienniku.
            // Posrednik ma tylko CZEKAC - nigdy nie zabijac procesu.
            var czekanie = zyjeDlugo ? 20_000 : 1_500;
            using var stary = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList =
                {
                    "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
                    $"Start-Sleep -Milliseconds {czekanie}; "
                    + $"Add-Content -LiteralPath '{dziennikCyklu}' -Value 'stary-proces-koniec'"
                }
            }) ?? throw new InvalidOperationException("Nie uruchomiono kontrolnego procesu udającego AMC.");

            var start = ApplicationUpdateManager.BuildLauncherStartInfo(
                skrypt,
                stary.Id,
                instalator,
                "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                sumaWLauncherze ?? suma,
                Path.Combine(katalog.Path, "pending.json"),
                wersjaWLauncherze ?? wersja,
                program,
                relaunch,
                czekajSekund);
            using var posrednik = Process.Start(start)
                ?? throw new InvalidOperationException("Nie uruchomiono pośrednika instalatora.");
            if (!posrednik.WaitForExit(90_000))
            {
                posrednik.Kill(entireProcessTree: true);
                throw new TimeoutException("Pośrednik instalatora nie zakończył się w terminie.");
            }

            if (zyjeDlugo && !stary.HasExited) stary.Kill(entireProcessTree: true);
            stary.WaitForExit(15_000);

            // Ostatni krok cyklu (ponowne uruchomienie programu) pomocnik odpala
            // BEZ czekania - tak samo jak w produkcji, bo AMC ma zyc dalej po
            // zakonczeniu pomocnika. Dziennik czytamy wiec dopiero, gdy dopisze
            // sie spodziewana liczba wierszy, a nie natychmiast po exit.
            var dziennikCykluGotowy = Path.Combine(katalog.Path, "cykl.log");
            if (oczekiwaneWiersze > 0)
            {
                var termin = DateTime.UtcNow.AddSeconds(20);
                while (DateTime.UtcNow < termin && Czytaj(dziennikCykluGotowy).Count < oczekiwaneWiersze)
                {
                    Thread.Sleep(200);
                }
            }

            var dziennikPosrednika = Path.Combine(katalog.Path, "uruchomienie-instalatora.log");
            var wiersze = Czytaj(dziennikCyklu);
            var pending = Path.Combine(katalog.Path, "pending.json");
            return new Przebieg(
                posrednik.ExitCode,
                wiersze,
                pending,
                File.Exists(dziennikPosrednika) ? File.ReadAllText(dziennikPosrednika) : "(brak dziennika pośrednika)",
                katalog);
        }
        catch
        {
            katalog.Dispose();
            throw;
        }
    }

    private sealed record Przebieg(
        int Kod,
        List<string> Wiersze,
        string PendingPath,
        string Dziennik,
        TempDirectory Katalog);

    private static void Zapisz(
        string root,
        string wersja,
        string instalator,
        string suma,
        bool allowInstallOnExit,
        string? program = null) =>
        ApplicationUpdateManager.SavePending(root, new ApplicationUpdateManager.PendingUpdate
        {
            Version = wersja,
            Sha256 = suma,
            ChecksumVerified = true,
            InstallerPath = instalator,
            TargetDirectory = program is null ? root : Path.GetDirectoryName(program)!,
            ProgramPath = program ?? "",
            AllowInstallOnExit = allowInstallOnExit,
            PreparedAtUtc = DateTimeOffset.UtcNow
        });

    private static List<string> Czytaj(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd()
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => w.Length > 0)
                .ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TempDirectory : IDisposable
    {
        internal TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"amc-aktualizacja-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
