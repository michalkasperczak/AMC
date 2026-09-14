using System.Diagnostics;
using System.IO;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Testy oczyszczania srodowiska skladnikow zewnetrznych (yt-dlp, ffmpeg).
///
/// SEDNO: yt-dlp przy starcie przechodzi po wszystkich katalogach z PATH.
/// Wystarczy, ze OBCY program wpisze tam katalog bedacy punktem ponownej
/// analizy uznanym przez Windows za niezaufany, i yt-dlp konczy sie bledem
/// [WinError 448] jeszcze zanim cokolwiek pobierze. Zmierzone 14.09.2026 na
/// komputerze uzytkownika. Skladnik dostaje pelna sciezke do pliku
/// wykonywalnego, wiec PATH systemu nie jest mu do niczego potrzebny.
/// </summary>
internal static class ExternalToolEnvironmentTests
{
    internal static void Run()
    {
        TestObcyWpisNiePrzechodzi();
        TestKatalogSkladnikaZostaje();
        TestNieistniejacyKatalogOdpada();
        TestPathExtMaWartosciDomyslne();
    }

    // Rdzen poprawki: cokolwiek obcego siedzi w PATH systemu, skladnik tego
    // nie zobaczy.
    private static void TestObcyWpisNiePrzechodzi()
    {
        const string znacznik = "amc-obcy-katalog-w-path";
        var poprzedni = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var obcy = Path.Combine(Path.GetTempPath(), znacznik);
            Environment.SetEnvironmentVariable(
                "PATH",
                obcy + Path.PathSeparator + (poprzedni ?? string.Empty));
            var narzedzie = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "where.exe");
            var start = new ProcessStartInfo { FileName = narzedzie };
            ExternalToolProcess.ApplySafeEnvironment(start, narzedzie);
            var path = start.Environment["PATH"] ?? string.Empty;
            Check(
                !path.Contains(znacznik, StringComparison.OrdinalIgnoreCase),
                "Obcy katalog z PATH systemu przedostal sie do srodowiska skladnika");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", poprzedni);
        }
    }

    // Katalog samego skladnika musi zostac - ffmpeg bywa wolany obok yt-dlp.
    private static void TestKatalogSkladnikaZostaje()
    {
        var katalog = Path.Combine(
            Path.GetTempPath(),
            "amc-test-skladnik-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(katalog);
        try
        {
            var plik = Path.Combine(katalog, "narzedzie.exe");
            var start = new ProcessStartInfo { FileName = plik };
            ExternalToolProcess.ApplySafeEnvironment(start, plik);
            var path = start.Environment["PATH"] ?? string.Empty;
            Check(
                path.Contains(katalog, StringComparison.OrdinalIgnoreCase),
                "Katalog skladnika powinien zostac w jego PATH");
        }
        finally
        {
            try { Directory.Delete(katalog, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static void TestNieistniejacyKatalogOdpada()
    {
        var nieistniejacy = Path.Combine(
            Path.GetTempPath(),
            "amc-nie-ma-mnie-" + Guid.NewGuid().ToString("N"));
        var plik = Path.Combine(nieistniejacy, "narzedzie.exe");
        var start = new ProcessStartInfo { FileName = plik };
        ExternalToolProcess.ApplySafeEnvironment(start, plik);
        var path = start.Environment["PATH"] ?? string.Empty;
        Check(
            !path.Contains(nieistniejacy, StringComparison.OrdinalIgnoreCase),
            "Katalog, ktorego nie ma, nie ma po co byc w PATH skladnika");
    }

    // PATHEXT tez bywa rozszerzany przez obce narzedzia.
    private static void TestPathExtMaWartosciDomyslne()
    {
        var narzedzie = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "where.exe");
        var start = new ProcessStartInfo { FileName = narzedzie };
        ExternalToolProcess.ApplySafeEnvironment(start, narzedzie);
        Check(
            (start.Environment["PATHEXT"] ?? string.Empty) == ".COM;.EXE;.BAT;.CMD",
            "PATHEXT skladnika powinien miec wartosci domyslne Windows");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
