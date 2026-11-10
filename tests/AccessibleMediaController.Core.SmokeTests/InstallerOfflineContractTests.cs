using System.Text.RegularExpressions;
using AccessibleMediaController.Core.ShellIntegration;

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

        // 1. Bieżący build wxPython publikuje WSZYSTKIE trzy procesy .NET
        //    samowystarczalnie i dokłada prywatny runtime Pythona.
        var build = File.ReadAllText(Path.Combine(
            korzen, "wxlite", "tools", "Build-AMCBundle.ps1"));
        Check(
            Count(build, "\"--self-contained\", \"true\"") == 3,
            "Build AMC nie publikuje trzech procesow .NET jako samowystarczalnych.");
        Check(
            build.Contains("\"--runtime\", $runtime", StringComparison.Ordinal),
            "Build AMC nie dolacza prywatnego runtime Pythona.");

        var iss = File.ReadAllText(Path.Combine(korzen, "installer", "AMC_Wx_Setup.iss"));

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

        // 3. Instalator nie moze zgubic odrebnej tozsamosci obu profili,
        //    kopiowania plikow, skrotow, App Paths ani pelnej kontroli paczki.
        foreach (var wymagane in new[]
                 {
                     "AppIdentity \"{{1E437448-30B4-4E4A-BCA1-CDA114B096A9}\"",
                     "AppIdentity \"{{0B807571-BDC5-4287-BD7C-4F4A7C71438F}\"",
                     "[Files]",
                     "[Icons]",
                     "[Registry]",
                     "App Paths\\{#AppPathName}",
                     "CloseApplications=no",
                     "--sprawdz-pakiet-instalatora-pelny-cicho",
                     "--sprawdz-start-instalatora-cicho",
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

        // 5. Integracja z powloka jest dobrowolna, odwracalna i ma dokladnie
        //    ten sam zakres rozszerzen co katalog Core. Nie ustawiamy
        //    UserChoice ani domyslnej aplikacji poza interfejsem Windows.
        foreach (var wymagane in new[]
                 {
                     "Name: \"shellcontext\"",
                     "Name: \"shellopenwith\"",
                     "Flags: unchecked",
                     "RemoveShellIntegration",
                     "CurUninstallStepChanged",
                     "Software\\RegisteredApplications",
                     "OpenWithProgids",
                     "SystemFileAssociations",
                     "/kolejka \"%1\"",
                     "AccessibleMediaController.{#ShellProfileKey}.",
                 })
        {
            Check(
                iss.Contains(wymagane, StringComparison.Ordinal),
                $"Instalator stracil bezpieczny element integracji powloki: {wymagane}.");
        }
        Check(
            !iss.Contains("UserChoice", StringComparison.OrdinalIgnoreCase),
            "Instalator nie moze wymuszac domyslnej aplikacji przez UserChoice.");

        // 6. Oba profile udostepniaja te same dobrowolne funkcje powloki,
        //    lecz pod osobnymi nazwami i kluczami. Odinstalowanie jednego
        //    profilu nie moze usunac wpisow drugiego.
        foreach (var wymagane in new[]
                 {
                     "ShellProfileKey \"Public\"",
                     "ShellProfileKey \"Test\"",
                     "ShellRegisteredApplication \"Accessible Multimedia Controller\"",
                     "ShellRegisteredApplication \"Accessible Multimedia Controller — Test\"",
                     "ShellOpenLabel \"Otwórz w AMC Public\"",
                     "ShellOpenLabel \"Otwórz w AMC Test\"",
                     "ShellQueueLabel \"Dodaj do kolejki AMC Public\"",
                     "ShellQueueLabel \"Dodaj do kolejki AMC Test\"",
                     "Software\\AccessibleMultimediaController\\{#ShellProfileKey}\\Capabilities",
                     "Software\\AccessibleMultimediaController\\{#ShellProfileKey}\\ShellIntegration",
                     "AccessibleMediaController.{#ShellProfileKey}.",
                 })
        {
            Check(
                iss.Contains(wymagane, StringComparison.Ordinal),
                $"Instalator stracil rozdzielenie powloki AMC i AMC Test: {wymagane}.");
        }

        const string extensionsMarker = "ShellExtensions = ";
        var extensionsStart = iss.IndexOf(extensionsMarker, StringComparison.Ordinal);
        var extensionsEnd = extensionsStart < 0
            ? -1
            : iss.IndexOf(';', extensionsStart);
        Check(extensionsStart >= 0 && extensionsEnd > extensionsStart,
            "Instalator nie zawiera katalogu rozszerzen powloki.");
        var extensionsBlock = iss[extensionsStart..extensionsEnd];
        var installerExtensions = Regex.Matches(extensionsBlock, "'([^']*)'")
            .Select(match => match.Groups[1].Value)
            .Aggregate(string.Empty, (left, right) => left + right)
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Check(
            installerExtensions.SetEquals(ShellIntegrationRules.AllExtensions),
            "Rozszerzenia instalatora i katalogu Core nie sa identyczne.");
        foreach (var wymagane in new[]
                 {
                     "ManagedByApplication",
                     "ContextMenuEnabled",
                     "OpenWithEnabled",
                     "SelectedExtensions",
                     "LoadShellIntegrationSelection",
                 })
        {
            Check(
                iss.Contains(wymagane, StringComparison.Ordinal),
                $"Aktualizacja instalatora nie zachowuje wyboru Ustawien: {wymagane}.");
        }

        var groupIds = ShellIntegrationRules.Groups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        Check(
            groupIds.Count == ShellIntegrationRules.Groups.Count
                && groupIds.All(id => !string.IsNullOrWhiteSpace(id)),
            "Grupy integracji nie maja unikatowych, stabilnych identyfikatorow.");
        Check(
            ShellIntegrationRules.ProfileFor("test").RegisteredApplication
                == "Accessible Multimedia Controller — Test"
                && ShellIntegrationRules.ProfileFor("public").RegisteredApplication
                == "Accessible Multimedia Controller",
            "Profile Core stracily osobne tozsamosci integracji z Windows.");

        Console.WriteLine(
            "OK: instalator nie pobiera ani nie instaluje zewnetrznego .NET "
                + "oraz ma opcjonalna, odwracalna integracje powloki "
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

    private static int Count(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
}
