/// <summary>
/// Czy paczka wydania jest SAMOWYSTARCZALNA: instalator nie ma prawa niczego
/// dociagac z sieci ani instalowac obcego srodowiska .NET.
///
/// Staly wymog Michala: instalator dziala BEZ pobierania czegokolwiek i bez
/// systemowej strony "You must install .NET Desktop Runtime". Skrypt budujacy
/// publikuje wersje samowystarczalna (--self-contained true), wiec runtime jest
/// W PLIKU programu - pozostawiony downloader runtime w sekcji
/// [Code] instalatora byl wiec i zbedny, i szkodliwy (siec w trakcie instalacji
/// oraz okna bledu, ktorych czytnik ekranu prawie nie tlumaczy).
///
/// GRANICA TEGO TESTU: to test STRUKTURALNY, czytajacy TEKST dwoch skryptow
/// (build.ps1 i installer/AMC_Setup.iss). Dowodzi KONTRAKTU SKRYPTOW. NIE jest
/// dowodem, ze paczka faktycznie sie zbudowala, ze ISCC skompilowal instalator,
/// ze wymagane biblioteki DLL sa w katalogu programu ani ze instalacja na
/// Windows przebiegla poprawnie. Tego dowodza osobne, rzeczywiste przebiegi.
/// </summary>
public static class InstallerOfflineContractTests
{
    public static void Run()
    {
        var korzen = KorzenRepozytorium();

        // 1. Build MUSI publikowac wersje samowystarczalna - bez tego usuniecie
        //    downloadera z instalatora zostawiloby uzytkownika bez runtime.
        var build = File.ReadAllText(Path.Combine(korzen, "build.ps1"));
        Check(
            build.Contains("--self-contained true", StringComparison.Ordinal),
            "build.ps1 nie publikuje wersji samowystarczalnej (--self-contained true).");

        var iss = File.ReadAllText(Path.Combine(korzen, "installer", "AMC_Setup.iss"));

        // 2. Zero pobierania i zero instalowania obcego runtime w instalatorze.
        foreach (var zakazane in new[]
                 {
                     "DownloadTemporaryFile",   // pobieranie w trakcie instalacji
                     "UrlRuntime",              // adres instalatora runtime
                     "windowsdesktop-runtime",  // plik instalatora runtime
                     "MaRuntime8",              // diagnostyka obecnosci runtime
                     "BrakRuntime",             // decyzja "dociagnij runtime"
                 })
        {
            Check(
                !iss.Contains(zakazane, StringComparison.OrdinalIgnoreCase),
                $"Instalator wciaz zawiera zewnetrzny tor .NET: {zakazane}.");
        }

        // 3. To, co w instalatorze musi zostac nietkniete. Usuwanie downloadera
        //    nie moze po drodze zgubic stalej nazwy pliku, tozsamosci produktu,
        //    kopiowania plikow, skrotow ani wpisu App Paths.
        foreach (var wymagane in new[]
                 {
                     "DestName: \"AccessibleMediaController.exe\"",
                     "AppId={{8F3A6C21-4E7B-4D59-9C08-A3C0919E7B21}",
                     "[Files]",
                     "[Icons]",
                     "[Registry]",
                     "App Paths\\AccessibleMediaController.exe",
                     "CloseApplications=force",
                 })
        {
            Check(
                iss.Contains(wymagane, StringComparison.Ordinal),
                $"Instalator stracil wymagany element: {wymagane}.");
        }

        // 4. Naglowek pliku nie moze dalej obiecywac, ze instalator "dociaga
        //    runtime" - komentarz wprowadzalby w blad przy kazdym czytaniu.
        Check(
            !iss.Contains("dociaga runtime", StringComparison.OrdinalIgnoreCase),
            "Komentarz instalatora wciaz twierdzi, ze instalator dociaga runtime.");

        Console.WriteLine(
            "OK: instalator nie pobiera ani nie instaluje zewnetrznego .NET "
                + "(kontrakt skryptow, nie dowod instalacji)");
    }

    /// <summary>
    /// Korzen repozytorium: idziemy w gore od katalogu uruchomienia, szukajac
    /// build.ps1. Testy chodza z katalogu bin, wiec sciezka wpisana na sztywno
    /// pekalaby przy kazdej zmianie konfiguracji budowania.
    /// </summary>
    private static string KorzenRepozytorium()
    {
        var katalog = new DirectoryInfo(AppContext.BaseDirectory);
        while (katalog is not null)
        {
            if (File.Exists(Path.Combine(katalog.FullName, "build.ps1"))
                && Directory.Exists(Path.Combine(katalog.FullName, "installer")))
            {
                return katalog.FullName;
            }
            katalog = katalog.Parent;
        }
        throw new Exception("Nie znaleziono korzenia repozytorium (build.ps1 + installer).");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
