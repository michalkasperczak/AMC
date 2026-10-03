// Zgloszenie (po 417): Biblioteka -> folder Sideloads -> plik „No cześć”,
// Shift+F2, zmiana „cześć” na „Cześć” konczy sie komunikatem
// „Nie zmieniono nazwy pliku: Zmiana wylacznie wielkosci liter nie jest jeszcze
// obslugiwana.” To byla CELOWA, niezaimplementowana bramka w
// LocalFileRenamePolicy, nie awaria systemu.
//
// Ten zestaw mierzy CALA droge, ktora przechodzi Shift+F2:
//   1) LocalFileRenamePolicy.TryBuildTargetPath - zgoda na zmiane samej
//      wielkosci liter, z zachowaniem rozszerzenia,
//   2) RZECZYWISTY File.Move na pliku w katalogu tymczasowym - zawartosc i
//      rozszerzenie bez zmian, zaden inny plik nie zostaje nadpisany,
//   3) RadioRecordingHistoryPathRewriter - wpis historii nagran idzie za NOWA
//      pisownia, bez duplikatu i bez gubienia wpisu.
//
// Celowo NIE wolno przepuscic: identycznej nazwy (znak w znak) oraz kolizji z
// INNYM, osobnym plikiem w tym samym folderze.
//
// Pomiar jest systemowo uczciwy: na WSL/ext4 (case-sensitive) „No cześć.mp3” i
// „No Cześć.mp3” to dwa rozne pliki, wiec rename zachowuje sie jak zwykla
// zmiana nazwy; na NTFS to ten sam plik i rename zmienia tylko pisownie. Test
// sprawdza WYNIK (plik czytany dokladnie pod nowa pisownia, stara pisownia
// nieobecna albo rowna nowej), a nie droge systemu plikow, dlatego przechodzi
// na obu. Oddzielny przypadek jawnie nazywa roznice: liczbe plikow w folderze
// po operacji mierzymy wzgledem wrazliwosci biezacego systemu plikow.
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Presentation;

internal static class RenameCaseOnlyTests
{
    internal static void Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var tests = new (string Name, Action Test)[]
        {
            ("polskie litery: No cześć -> No Cześć", PolishCaseOnlyIsAccepted),
            ("zwykła nazwa: raport -> Raport", PlainCaseOnlyIsAccepted),
            ("rzeczywisty plik zmienia pisownię, treść zostaje", RealFileKeepsContentAndExtension),
            ("historia nagrań idzie za nową pisownią", HistoryFollowsNewCasing),
            ("identyczna nazwa nadal odrzucona", IdenticalNameStillRejected),
            ("kolizja z innym plikiem nadal odrzucona", CollisionWithOtherFileStillRejected),
            ("rozszerzenie nie zmienia pisowni z nazwy", ExtensionCasingIsNotTakenFromInput),
        };

        var failures = new List<string>();
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("OK: " + name); }
            catch (Exception exception) { failures.Add(name); Console.Error.WriteLine(name + ": " + exception); }
        }
        Console.WriteLine(
            $"ZMIANA WIELKOSCI LITER: {tests.Length - failures.Count} OK / {failures.Count} BLAD / razem {tests.Length}");
        if (failures.Count != 0) throw new InvalidOperationException(string.Join("; ", failures));
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "amc-rename-case-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    // Czy biezacy system plikow rozroznia wielkosc liter w nazwach. Mierzone,
    // nie zalozone: ten sam test chodzi na ext4 (WSL) i na NTFS (Windows).
    private static bool FileSystemIsCaseSensitive(string directory)
    {
        var lower = Path.Combine(directory, ".amc-case-probe-a");
        var upper = Path.Combine(directory, ".amc-case-probe-A");
        File.WriteAllText(lower, "a");
        try
        {
            return !File.Exists(upper);
        }
        finally
        {
            File.Delete(lower);
        }
    }

    private static void PolishCaseOnlyIsAccepted()
    {
        var directory = NewDirectory();
        try
        {
            var current = Path.Combine(directory, "No cześć.mp3");
            File.WriteAllBytes(current, [1, 2, 3]);
            Check(
                LocalFileRenamePolicy.TryBuildTargetPath(current, "No Cześć", out var target, out var error),
                "Zmiana samej wielkosci liter w polskiej nazwie musi byc przyjeta: " + error);
            Check(
                target == Path.Combine(directory, "No Cześć.mp3"),
                "Sciezka docelowa musi miec NOWA pisownie i STARE rozszerzenie: " + target);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void PlainCaseOnlyIsAccepted()
    {
        var directory = NewDirectory();
        try
        {
            var current = Path.Combine(directory, "raport.mp3");
            File.WriteAllBytes(current, [1]);
            Check(
                LocalFileRenamePolicy.TryBuildTargetPath(current, "Raport", out var target, out var error),
                "Zmiana samej wielkosci liter w zwyklej nazwie musi byc przyjeta: " + error);
            Check(
                target == Path.Combine(directory, "Raport.mp3"),
                "Sciezka docelowa zwyklej nazwy jest bledna: " + target);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void RealFileKeepsContentAndExtension()
    {
        var directory = NewDirectory();
        try
        {
            var caseSensitive = FileSystemIsCaseSensitive(directory);
            var current = Path.Combine(directory, "No cześć.mp3");
            var tresc = new byte[] { 7, 8, 9, 10 };
            File.WriteAllBytes(current, tresc);
            // Sasiad, ktorego operacja NIE MOZE tknac.
            var obcy = Path.Combine(directory, "Inny plik.mp3");
            File.WriteAllBytes(obcy, [99]);

            Check(
                LocalFileRenamePolicy.TryBuildTargetPath(current, "No Cześć", out var target, out var error),
                "Polityka odmowila zmiany wielkosci liter: " + error);

            LocalFileRenamePolicy.MoveFile(current, target);

            Check(File.Exists(target), "Plik nie istnieje pod nowa pisownia: " + target);
            Check(
                Path.GetFileName(target) == "No Cześć.mp3",
                "Nazwa po zmianie ma byc dokladnie „No Cześć.mp3”: " + Path.GetFileName(target));
            Check(File.ReadAllBytes(target).SequenceEqual(tresc), "Zawartosc pliku zostala zmieniona.");
            Check(File.Exists(obcy) && File.ReadAllBytes(obcy).Length == 1, "Operacja ruszyla obcy plik.");

            // Pisownia zapisana na dysku musi byc NOWA, nie stara.
            var naDysku = Directory.GetFiles(directory, "*.mp3").Select(Path.GetFileName).ToArray();
            Check(
                naDysku.Contains("No Cześć.mp3", StringComparer.Ordinal),
                "Katalog nie zglasza nowej pisowni: " + string.Join(", ", naDysku));
            Check(
                !naDysku.Contains("No cześć.mp3", StringComparer.Ordinal),
                "Stara pisownia nadal widnieje w katalogu: " + string.Join(", ", naDysku));
            // Na systemie bez rozroznienia wielkosci liter to jest TEN SAM plik,
            // wiec liczba plikow nie moze wzrosnac. Na ext4 stary byl osobnym
            // wpisem i po File.Move rowniez zostaja dwa (nowy + obcy).
            Check(
                Directory.GetFiles(directory, "*.mp3").Length == 2,
                $"Zla liczba plikow po zmianie pisowni (case-sensitive: {caseSensitive}).");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void HistoryFollowsNewCasing()
    {
        var directory = NewDirectory();
        try
        {
            var current = Path.Combine(directory, "No cześć.mp3");
            File.WriteAllBytes(current, [1, 2]);
            var wpis = new RadioRecordingHistorySettings
            {
                Id = "nagranie-1",
                StationId = "stacja",
                StationName = "Stacja próby",
                Path = current,
                Outcome = RadioRecordingOutcome.Completed,
                SavedFileCount = 1
            };
            var historia = new[] { wpis };

            Check(
                LocalFileRenamePolicy.TryBuildTargetPath(current, "No Cześć", out var target, out var error),
                "Polityka odmowila zmiany wielkosci liter: " + error);
            LocalFileRenamePolicy.MoveFile(current, target);

            var zmienione = RadioRecordingHistoryPathRewriter.Rewrite(historia, current, target);
            Check(zmienione == 1, $"Historia musi przepisac dokladnie jeden wpis, przepisala {zmienione}.");
            Check(historia.Length == 1, "Przepisanie nie moze dodawac ani usuwac wpisow historii.");
            Check(
                string.Equals(wpis.Path, target, StringComparison.Ordinal),
                "Wpis historii nie ma NOWEJ pisowni: " + wpis.Path);
            Check(wpis.Id == "nagranie-1", "Tozsamosc wpisu historii musi zostac zachowana.");
            Check(File.Exists(wpis.Path), "Wpis historii wskazuje nieistniejaca sciezke: " + wpis.Path);

            // Powtorzenie tej samej operacji nic nie zmienia (brak duplikatow).
            Check(
                RadioRecordingHistoryPathRewriter.Rewrite(historia, current, target) == 0,
                "Powtorne przepisanie tej samej zmiany nie moze nic zmieniac.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void IdenticalNameStillRejected()
    {
        var directory = NewDirectory();
        try
        {
            var current = Path.Combine(directory, "No cześć.mp3");
            File.WriteAllBytes(current, [1]);
            Check(
                !LocalFileRenamePolicy.TryBuildTargetPath(current, "No cześć", out _, out var error),
                "Nazwa identyczna znak w znak musi byc odrzucona.");
            Check(error.Length > 0, "Odmowa identycznej nazwy musi podac powod.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CollisionWithOtherFileStillRejected()
    {
        var directory = NewDirectory();
        try
        {
            var current = Path.Combine(directory, "No cześć.mp3");
            File.WriteAllBytes(current, [1]);
            var inny = Path.Combine(directory, "Zajęte.mp3");
            File.WriteAllBytes(inny, [2]);

            Check(
                !LocalFileRenamePolicy.TryBuildTargetPath(current, "Zajęte", out _, out var error),
                "Istniejacy INNY plik nie moze zostac nadpisany.");
            Check(error.Length > 0, "Odmowa kolizji musi podac powod.");

            // Ta sama ochrona przy roznicy wylacznie w wielkosci liter: celem
            // jest OSOBNY plik, nie zmiana pisowni naszego.
            var odmowa = LocalFileRenamePolicy.TryBuildTargetPath(current, "zajęte", out _, out var error2);
            if (FileSystemIsCaseSensitive(directory))
            {
                // Na ext4 „zajęte.mp3” to wolna, osobna nazwa - zgoda jest poprawna.
                Check(odmowa, "Na systemie rozrozniajacym wielkosc liter wolna nazwa musi byc przyjeta: " + error2);
            }
            else
            {
                Check(!odmowa, "Zmiana pisowni NIE moze celowac w inny istniejacy plik.");
                Check(error2.Length > 0, "Odmowa kolizji po wielkosci liter musi podac powod.");
            }

            Check(File.ReadAllBytes(inny).SequenceEqual(new byte[] { 2 }), "Obcy plik zostal zmieniony.");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void ExtensionCasingIsNotTakenFromInput()
    {
        var directory = NewDirectory();
        try
        {
            var current = Path.Combine(directory, "No cześć.mp3");
            File.WriteAllBytes(current, [1]);
            // Uzytkownik dopisal rozszerzenie INNA wielkoscia liter: nie wolno go
            // zdublowac ani podmienic pisowni rozszerzenia pliku.
            Check(
                LocalFileRenamePolicy.TryBuildTargetPath(current, "No Cześć.MP3", out var target, out var error),
                "Wpisanie rozszerzenia inna wielkoscia liter musi byc przyjete: " + error);
            Check(
                target == Path.Combine(directory, "No Cześć.mp3"),
                "Rozszerzenie musi zostac zachowane w pisowni PLIKU: " + target);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
