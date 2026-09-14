using AccessibleMediaController.Core.Updates;

/// <summary>
/// Czy AMC odczyta sume kontrolna z PRAWDZIWEGO opisu wydania 367.
///
/// Zgloszenie Michala 15.09.2026: okno aktualizacji powiedzialo "Wydanie nie
/// podalo sumy kontrolnej, wiec zgodnosc pliku nie zostala sprawdzona".
/// Przyczyna byla po stronie WYDANIA (opis nie zawieral sum), nie kodu, ale bez
/// testu na realnym ukladzie opisu nic tego nie pilnuje.
/// </summary>
public static class ReleaseNotesChecksumTests
{
    // Uklad opisu dokladnie taki, jaki ma wydanie v0.1.0-alpha.367 po poprawce:
    // naglowek, potem NAZWA PLIKU w jednym wierszu, a suma w wierszu nastepnym.
    private const string Notes = """
        Trzy zadania z listy 15.09.2026.

        Pokaż w folderze (zadanie 7). Pozycja w menu kontekstowym istniała.

        ## Sumy kontrolne SHA-256

        AMC-Setup-0.1.0-alpha.367.exe
        32715f13e98ba27184911568304983f3f527d3611c9aee15f01c2c3649d59c55

        AMC-367.zip
        235d643d4946f0759d9dd26be841ab77c172ac9311d932df5b6dd7c7748395cd
        """;

    private const string InstallerSha =
        "32715f13e98ba27184911568304983f3f527d3611c9aee15f01c2c3649d59c55";

    private const string ZipSha =
        "235d643d4946f0759d9dd26be841ab77c172ac9311d932df5b6dd7c7748395cd";

    public static void Run()
    {
        // Kazdy z dwoch plikow dostaje SWOJA sume, mimo ze opis wymienia obie.
        Check(
            ApplicationUpdatePolicy.ReadChecksumFor(Notes, "AMC-Setup-0.1.0-alpha.367.exe") == InstallerSha,
            "suma instalatora nie zostala odczytana z opisu wydania 367");

        Check(
            ApplicationUpdatePolicy.ReadChecksumFor(Notes, "AMC-367.zip") == ZipSha,
            "suma paczki ZIP nie zostala odczytana z opisu wydania 367");

        // Dwie rozne sumy w opisie: odczyt BEZ nazwy pliku musi zwrocic null,
        // bo zgadywanie ktora nalezy do paczki byloby gorsze od uczciwego braku.
        Check(
            ApplicationUpdatePolicy.ReadChecksum(Notes) is null,
            "przy dwoch sumach w opisie odczyt bez nazwy pliku musi dac null");

        // Plik, ktorego w opisie nie ma, nie moze dostac cudzej sumy.
        Check(
            ApplicationUpdatePolicy.ReadChecksumFor(Notes, "AMC-999.zip") is null,
            "plik nieobecny w opisie dostal sume innego pliku");

        // Opis bez sum: brak sumy to null, a nie wyjatek.
        Check(
            ApplicationUpdatePolicy.ReadChecksumFor("Zwykly opis bez sum.", "AMC-367.zip") is null,
            "opis bez sum powinien dac null");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
