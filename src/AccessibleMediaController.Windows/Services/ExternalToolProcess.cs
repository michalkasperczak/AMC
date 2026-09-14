using System.Diagnostics;
using System.IO;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Uruchamianie skladnikow zewnetrznych (yt-dlp, ffmpeg) w OCZYSZCZONYM
/// srodowisku.
///
/// DLACZEGO: yt-dlp przy starcie przechodzi po wszystkich katalogach z PATH.
/// Wystarczy, ze OBCY program wpisze tam katalog bedacy punktem ponownej
/// analizy (dowiazaniem), ktory Windows uzna za niezaufany, i yt-dlp konczy
/// sie bledem [WinError 448] jeszcze przed pobraniem czegokolwiek. Zmierzone
/// 14.09.2026 na katalogu obcego narzedzia w PATH uzytkownika. Skladnik dostaje
/// pelna sciezke do pliku wykonywalnego, wiec PATH nie jest mu do niczego
/// potrzebny - dziedziczenie go bylo samym ryzykiem.
/// </summary>
internal static class ExternalToolProcess
{
    /// <summary>
    /// Przygotowuje uruchomienie skladnika: bez okna, ze strumieniami i z PATH
    /// ograniczonym do katalogow systemowych Windows.
    /// </summary>
    internal static ProcessStartInfo Create(string executablePath)
    {
        var start = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        ApplySafeEnvironment(start, executablePath);
        return start;
    }

    /// <summary>
    /// Zastepuje PATH lista katalogow systemowych i katalogiem samego
    /// skladnika. Kazdy katalog jest sprawdzany - zostaja tylko takie, ktore
    /// naprawde istnieja i daja sie odczytac.
    /// </summary>
    internal static void ApplySafeEnvironment(ProcessStartInfo start, string executablePath)
    {
        var directories = new List<string>();

        var toolDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
        if (!string.IsNullOrWhiteSpace(toolDirectory)) directories.Add(toolDirectory);

        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrWhiteSpace(system))
        {
            directories.Add(system);
            var windows = Path.GetDirectoryName(system);
            if (!string.IsNullOrWhiteSpace(windows))
            {
                directories.Add(windows);
                directories.Add(Path.Combine(system, "Wbem"));
                directories.Add(Path.Combine(system, "WindowsPowerShell", "v1.0"));
            }
        }

        var safe = new List<string>();
        foreach (var directory in directories)
        {
            if (safe.Contains(directory, StringComparer.OrdinalIgnoreCase)) continue;
            if (!IsUsableDirectory(directory)) continue;
            safe.Add(directory);
        }

        start.Environment["PATH"] = string.Join(Path.PathSeparator, safe);

        // PATHEXT bywa rozszerzany przez obce narzedzia; zostawiamy wartosci
        // domyslne Windows, zeby skladnik nie probowal cudzych rozszerzen.
        start.Environment["PATHEXT"] = ".COM;.EXE;.BAT;.CMD";
    }

    /// <summary>
    /// Katalog jest przydatny tylko wtedy, gdy istnieje I daje sie otworzyc.
    /// Katalog bedacy niezaufanym punktem ponownej analizy rzuci wyjatkiem
    /// wlasnie tutaj - i wypadnie z listy, zamiast wywrocic caly skladnik.
    /// </summary>
    private static bool IsUsableDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return false;
            _ = new DirectoryInfo(directory).Attributes;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
