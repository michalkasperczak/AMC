using AccessibleMediaController.Core.Updates;

/// <summary>
/// Testy aktualizacji calej aplikacji i tresci zgloszenia bledu.
/// Wszystkie dzialaja bez siec i bez interfejsu.
/// </summary>
internal static class ApplicationUpdateTests
{
    public static void Run()
    {
        TestVersionOrdering();
        TestPrereleaseNumberIsComparedAsNumber();
        TestUpToDate();
        TestUpdateAvailable();
        TestLocalBuildIsNewer();
        TestStableChannelRefusesPrereleaseBeforeAnnouncingIt();
        TestChecksumIsMatchedToPackageName();
        TestAmbiguousChecksumIsRejected();
        TestMissingPackageIsReported();
        TestUnreadableVersionsAreReported();
        TestReportTitleAndBody();
        TestReportKeepsUserTextFirst();
        TestReportLogTailIsLimited();
        TestIssueUriIsShortenedWithNotice();
    }

    private static ApplicationRelease Release(
        string tag,
        bool prerelease = true,
        string? notes = null,
        string? package = "AMC-windows-x64.zip") =>
        new(
            tag,
            notes,
            package is null ? null : new Uri($"https://example.invalid/{package}"),
            package,
            1024,
            prerelease);

    private static void TestVersionOrdering()
    {
        Assert(ApplicationVersion.TryParse("v0.1.0-alpha.354", out var newer), "Numer z v powinien się odczytać.");
        Assert(ApplicationVersion.TryParse("0.1.0-alpha.342", out var older), "Numer bez v powinien się odczytać.");
        Assert(newer > older, "alpha.354 powinna być nowsza niż alpha.342.");

        Assert(ApplicationVersion.TryParse("0.1.0-alpha.354+9f2ab1", out var withBuild),
            "Numer ze znacznikiem kompilacji powinien się odczytać.");
        Assert(withBuild.Equals(newer), "Znacznik kompilacji nie powinien zmieniać porządku wersji.");

        Assert(ApplicationVersion.TryParse("0.1.0", out var stable), "Wydanie zwykłe powinno się odczytać.");
        Assert(stable > newer, "Wydanie 0.1.0 powinno być nowsze niż 0.1.0-alpha.354.");
        Assert(!stable.IsPrerelease, "0.1.0 nie jest wydaniem przedpremierowym.");
        Assert(newer.IsPrerelease, "0.1.0-alpha.354 jest wydaniem przedpremierowym.");

        Assert(!ApplicationVersion.TryParse("", out _), "Puste wejście nie jest numerem wersji.");
        Assert(!ApplicationVersion.TryParse("wersja robocza", out _), "Tekst nie jest numerem wersji.");
    }

    private static void TestPrereleaseNumberIsComparedAsNumber()
    {
        // Porownanie tekstowe daloby tu wynik odwrotny, bo "9" > "354" alfabetycznie.
        Assert(ApplicationVersion.TryParse("0.1.0-alpha.354", out var big), "Numer powinien się odczytać.");
        Assert(ApplicationVersion.TryParse("0.1.0-alpha.9", out var small), "Numer powinien się odczytać.");
        Assert(big > small, "alpha.354 musi być nowsza niż alpha.9 - człon liczbowy porównujemy liczbowo.");
    }

    private static void TestUpToDate()
    {
        var plan = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.354", Release("v0.1.0-alpha.354"), "beta");
        Assert(plan.Decision == ApplicationUpdateDecision.UpToDate, "Ta sama wersja to brak aktualizacji.");
        Assert(plan.Message.Contains("aktualne", StringComparison.OrdinalIgnoreCase),
            "Komunikat powinien mówić, że wersja jest aktualna.");
    }

    private static void TestUpdateAvailable()
    {
        var plan = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.342", Release("v0.1.0-alpha.354"), "beta");
        Assert(plan.Decision == ApplicationUpdateDecision.UpdateAvailable, "Nowsze wydanie to dostępna aktualizacja.");
        Assert(plan.Message.Contains("0.1.0-alpha.342", StringComparison.Ordinal)
               && plan.Message.Contains("0.1.0-alpha.354", StringComparison.Ordinal),
            "Komunikat powinien podać obie wersje: uruchomioną i do pobrania.");
        Assert(!plan.HasChecksum, "Wydanie bez sumy w opisie nie powinno udawać, że sumę ma.");
    }

    private static void TestLocalBuildIsNewer()
    {
        var plan = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.360", Release("v0.1.0-alpha.354"), "beta");
        Assert(plan.Decision == ApplicationUpdateDecision.LocalIsNewer,
            "Wersja robocza nowsza od wydania nie jest aktualizacją.");
        Assert(plan.Message.Contains("robocza", StringComparison.OrdinalIgnoreCase),
            "Komunikat powinien wyjaśnić, że to wersja robocza.");
    }

    private static void TestStableChannelRefusesPrereleaseBeforeAnnouncingIt()
    {
        var plan = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.342", Release("v0.1.0-alpha.354"), "stable");
        Assert(plan.Decision == ApplicationUpdateDecision.PrereleaseBlockedByChannel,
            "Kanał stabilny nie powinien proponować wydania testowego.");
        Assert(plan.Message.Contains("testow", StringComparison.OrdinalIgnoreCase)
               && plan.Message.Contains("kanał", StringComparison.OrdinalIgnoreCase),
            "Komunikat powinien wprost powiedzieć, że blokuje go kanał aktualizacji.");

        Assert(ApplicationUpdatePolicy.IsPrereleaseAllowed("beta"), "Kanał testowy dopuszcza alfy.");
        Assert(!ApplicationUpdatePolicy.IsPrereleaseAllowed("stable"), "Kanał stabilny nie dopuszcza alf.");
        Assert(!ApplicationUpdatePolicy.IsPrereleaseAllowed(null), "Brak kanału traktujemy jak stabilny.");
    }

    private static void TestChecksumIsMatchedToPackageName()
    {
        const string amc = "1111111111111111111111111111111111111111111111111111111111111111";
        const string addon = "2222222222222222222222222222222222222222222222222222222222222222";
        var notes = $"Paczka AMC-windows-x64.zip - SHA256: {amc}\nDodatek NVDA amc.nvda-addon - SHA256: {addon}";

        var plan = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.342", Release("v0.1.0-alpha.354", notes: notes), "beta");
        Assert(plan.HasChecksum, "Suma przypisana do nazwy paczki powinna zostać znaleziona.");
        Assert(plan.ExpectedSha256 == amc,
            "Powinna to być suma paczki AMC, nie dodatku NVDA - inaczej sprawdzenie odrzuci poprawny plik.");

        var twoLine = $"AMC-windows-x64.zip\nSHA256\n{amc}";
        Assert(ApplicationUpdatePolicy.ReadChecksumFor(twoLine, "AMC-windows-x64.zip") == amc,
            "Suma w wierszu poniżej nazwy pliku też powinna zostać znaleziona.");
    }

    private static void TestAmbiguousChecksumIsRejected()
    {
        const string first = "3333333333333333333333333333333333333333333333333333333333333333";
        const string second = "4444444444444444444444444444444444444444444444444444444444444444";
        var notes = $"SHA256: {first}\nSHA256: {second}";
        Assert(ApplicationUpdatePolicy.ReadChecksum(notes) is null,
            "Dwie różne sumy bez nazw plików są niejednoznaczne - lepiej żadna niż zgadnięta.");
        Assert(ApplicationUpdatePolicy.ReadChecksum($"SHA256: {first}") == first,
            "Jedna suma w opisie powinna zostać odczytana.");
        Assert(ApplicationUpdatePolicy.ReadChecksum("bez sumy") is null, "Opis bez sumy nie ma sumy.");
    }

    private static void TestMissingPackageIsReported()
    {
        var plan = ApplicationUpdatePolicy.Evaluate(
            "0.1.0-alpha.342", Release("v0.1.0-alpha.354", package: null), "beta");
        Assert(plan.Decision == ApplicationUpdateDecision.NotUnderstood,
            "Wydanie bez paczki nie jest aktualizacją do pobrania.");
        Assert(plan.Message.Contains("paczk", StringComparison.OrdinalIgnoreCase),
            "Komunikat powinien powiedzieć, że brakuje paczki.");
    }

    private static void TestUnreadableVersionsAreReported()
    {
        var noRelease = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.354", null, "beta");
        Assert(noRelease.Decision == ApplicationUpdateDecision.NotUnderstood, "Brak wydania to brak odpowiedzi.");

        var badTag = ApplicationUpdatePolicy.Evaluate("0.1.0-alpha.354", Release("najnowsze"), "beta");
        Assert(badTag.Decision == ApplicationUpdateDecision.NotUnderstood,
            "Znacznika, którego nie da się odczytać, nie wolno porównywać.");

        var badLocal = ApplicationUpdatePolicy.Evaluate("nieznana", Release("v0.1.0-alpha.354"), "beta");
        Assert(badLocal.Decision == ApplicationUpdateDecision.NotUnderstood,
            "Nieczytelnej wersji uruchomionej też nie wolno porównywać.");
    }

    private static ProblemReportEnvironment Environment() => new(
        "0.1.0-alpha.354",
        "Windows 11 26100",
        ".NET 8.0.8",
        "pl-PL",
        "TIDAL",
        "Głośniki");

    private static void TestReportTitleAndBody()
    {
        var input = new ProblemReportInput("Zamarza lista", "Po dodaniu folderu okno stoi.", ProblemReportKind.NotWorking);
        var title = ProblemReportComposer.ComposeTitle(input);
        Assert(title.Contains("Coś nie działa", StringComparison.Ordinal) && title.Contains("Zamarza lista", StringComparison.Ordinal),
            "Tytuł powinien zawierać rodzaj zgłoszenia i temat.");

        var body = ProblemReportComposer.ComposeBody(input, Environment());
        Assert(body.Contains("0.1.0-alpha.354", StringComparison.Ordinal), "Treść powinna zawierać wersję AMC.");
        Assert(body.Contains("Windows 11 26100", StringComparison.Ordinal), "Treść powinna zawierać wersję Windows.");
        Assert(body.Contains("(nie podano)", StringComparison.Ordinal),
            "Brak adresu zwrotnego powinien być napisany wprost, a nie pominięty.");

        var empty = ProblemReportComposer.ComposeTitle(
            new ProblemReportInput("   ", "", ProblemReportKind.Question));
        Assert(empty.Contains("(bez tematu)", StringComparison.Ordinal), "Puste pole tematu nie powinno dać pustego tytułu.");
    }

    private static void TestReportKeepsUserTextFirst()
    {
        var input = new ProblemReportInput(
            "Temat", "To napisał człowiek.", ProblemReportKind.NotWorking,
            ExceptionTrace: "System.InvalidOperationException: coś",
            LogTail: ["wiersz dziennika"]);
        var body = ProblemReportComposer.ComposeBody(input, Environment());

        var human = body.IndexOf("To napisał człowiek.", StringComparison.Ordinal);
        var trace = body.IndexOf("InvalidOperationException", StringComparison.Ordinal);
        var technical = body.IndexOf("Windows 11 26100", StringComparison.Ordinal);

        Assert(human >= 0 && trace > human, "Opis człowieka musi być przed śladem błędu.");
        Assert(technical > trace, "Dane techniczne muszą być na końcu, po treści zgłoszenia.");
    }

    private static void TestReportLogTailIsLimited()
    {
        var log = Enumerable.Range(1, 500).Select(number => $"wiersz {number}").ToList();
        var body = ProblemReportComposer.ComposeBody(
            new ProblemReportInput("Temat", "Opis", ProblemReportKind.NotWorking, LogTail: log),
            Environment());

        Assert(!body.Contains("wiersz 1\n", StringComparison.Ordinal), "Najstarsze wiersze dziennika powinny zostać odcięte.");
        Assert(body.Contains("wiersz 500", StringComparison.Ordinal),
            "Najnowsze wiersze muszą zostać - awaria jest na końcu dziennika.");
        Assert(body.Contains($"ostatnie {ProblemReportComposer.LogTailLines} wierszy", StringComparison.Ordinal),
            "Zgłoszenie powinno napisać, ile wierszy dziennika dołączono.");
    }

    private static void TestIssueUriIsShortenedWithNotice()
    {
        var body = new string('x', 20_000);
        var uri = ProblemReportComposer.ComposeIssueUri(
            "https://github.com/michalkasperczak/AMC", "Tytuł", body, @"C:\kopia\zgloszenie.txt");

        Assert(uri.ToString().StartsWith("https://github.com/michalkasperczak/AMC/issues/new", StringComparison.Ordinal),
            "Adres powinien prowadzić do formularza nowego zgłoszenia.");
        Assert(uri.ToString().Length < 20_000, "Za długa treść powinna zostać przycięta.");
        Assert(Uri.UnescapeDataString(uri.Query).Contains("zgloszenie.txt", StringComparison.Ordinal),
            "Przycięta treść powinna wskazać plik z pełną wersją.");

        var name = ProblemReportComposer.ComposeFileName(new DateTimeOffset(2026, 9, 14, 3, 4, 5, TimeSpan.Zero));
        Assert(name == "zgloszenie-20260914-030405.txt", $"Nazwa pliku kopii jest nieoczekiwana: {name}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
