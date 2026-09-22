// Wymaganie historii nagrań:
//
//  a) Po Ctrl+X i przeniesieniu pliku POZA AMC w Historii nagrywania nadal stoi
//     wiersz „Nagrano <nazwa>”, a Enter na nim wskazuje martwa stara sciezke.
//  b) Wiersz historii ma sie czytac: uzyteczna nazwa, czas/data, a na KONCU
//     folder, do ktorego nagranie zapisano - bez kopiowania sciezki Ctrl+C.
//
// Test mierzy RZECZYWISTE pliki w katalogu tymczasowym (rename/delete), nie
// same napisy. Logika siedzi w Core, wiec da sie ja zmierzyc bez WPF.
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Presentation;

internal static class RecordingMoveAndFolderRowTests
{
    internal static void Run()
    {
        FolderIsReadLastAndNotDuplicated();
        MissingFileIsNotReadAsReady();
        RenameRewritesHistoryPathWithoutDuplicate();
        ExternalMoveWithKnownDestinationIsAdopted();
        ExternalMoveWithoutDestinationKeepsEntryButHonestState();
        FailedAndActiveEntriesSurviveMissingDisk();
        ProbeDoesNotTouchWholeLibraryAndCachesFrames();
        DatesStayLocalAndOrderedAfterMove();
        Console.WriteLine(
            "OK: po przeniesieniu nagrania listy nie pokazuja martwej sciezki, a wiersz czyta folder na koncu");
    }

    private static string NewDirectory(string tag)
    {
        var path = Path.Combine(Path.GetTempPath(), $"amc-nagrania-{tag}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static RadioRecordingHistorySettings Entry(
        string id,
        string station,
        string path,
        RadioRecordingOutcome outcome = RadioRecordingOutcome.Completed,
        int finishedHour = 11) => new()
    {
        Id = id,
        StationId = $"{id}-stacja",
        StationName = station,
        Path = path,
        Outcome = outcome,
        SavedFileCount = path.Length == 0 ? 0 : 1,
        StartedUtcTicks = new DateTime(2026, 9, 15, finishedHour - 1, 0, 0, DateTimeKind.Utc).Ticks,
        FinishedUtcTicks = new DateTime(2026, 9, 15, finishedHour, 0, 0, DateTimeKind.Utc).Ticks
    };

    private static readonly DateTime Teraz = new(2026, 9, 16, 9, 0, 0, DateTimeKind.Utc);

    // ---- a) folder na koncu wiersza ------------------------------------

    private static void FolderIsReadLastAndNotDuplicated()
    {
        var root = NewDirectory("folder");
        try
        {
            var nagrania = Path.Combine(root, "Nagrania");
            Directory.CreateDirectory(nagrania);
            var plik = Path.Combine(nagrania, "trojka-2026-09-15.mp3");
            File.WriteAllBytes(plik, new byte[64]);

            var wpis = Entry("a", "Trójka", plik);
            var wiersz = RadioRecordingRowLabels.DescribeHistoryRow(wpis, Teraz, fileMissing: false);

            Check(wiersz.StartsWith("Nagrane", StringComparison.Ordinal),
                $"Skutek musi byc czytany pierwszy: {wiersz}");
            Check(wiersz.Contains("Trójka", StringComparison.Ordinal),
                $"Wiersz musi podac stacje: {wiersz}");
            Check(wiersz.Contains("trojka-2026-09-15", StringComparison.Ordinal),
                $"Wiersz musi podac uzyteczna nazwe pliku: {wiersz}");
            Check(wiersz.TrimEnd().EndsWith("folder Nagrania", StringComparison.Ordinal),
                $"Folder docelowy musi byc czytany na samym koncu wiersza: {wiersz}");
            // Data/czas przed folderem, nie po nim.
            Check(wiersz.IndexOf("15 września", StringComparison.Ordinal)
                    < wiersz.IndexOf("folder Nagrania", StringComparison.Ordinal),
                $"Czas/data musi poprzedzac folder: {wiersz}");
            Check(CountOccurrences(wiersz, "folder ") == 1,
                $"Folder nie moze byc czytany dwa razy: {wiersz}");
            Check(!wiersz.Contains(nagrania, StringComparison.Ordinal),
                $"Wiersz ma czytac NAZWE folderu, nie pelna sciezke: {wiersz}");
            Check(CountOccurrences(wiersz, "trojka-2026-09-15") == 1,
                $"Nazwa pliku nie moze dublowac sie w wierszu: {wiersz}");

            // Wiersz pliku z biblioteki: etykieta jest gotowa, folder dopisujemy
            // na koncu i NIE powtarzamy, gdy juz tam jest.
            var zBiblioteki = RadioRecordingRowLabels.AppendFolder(
                "trojka-2026-09-15, 59 min, Trójka", plik);
            Check(zBiblioteki.EndsWith("folder Nagrania", StringComparison.Ordinal),
                $"Wiersz pliku z biblioteki musi konczyc sie folderem: {zBiblioteki}");
            Check(RadioRecordingRowLabels.AppendFolder(zBiblioteki, plik) == zBiblioteki,
                "Powtorne dopisanie folderu nie moze dublowac tekstu.");

            // Pusta sciezka (nagranie nieudane) nie dopisuje zadnego folderu.
            Check(RadioRecordingRowLabels.AppendFolder("Nieudane, Dwójka", string.Empty)
                    == "Nieudane, Dwójka",
                "Brak sciezki nie moze dopisywac slowa „folder”.");
            Check(RadioRecordingRowLabels.FolderLabel(string.Empty) is null,
                "Pusta sciezka nie ma folderu docelowego.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- b) brakujacy plik nie udaje gotowego ---------------------------

    private static void MissingFileIsNotReadAsReady()
    {
        var root = NewDirectory("brak");
        try
        {
            var plik = Path.Combine(root, "trojka.mp3");
            File.WriteAllBytes(plik, new byte[32]);
            var wpis = Entry("b", "Trójka", plik);

            var probe = new RecordingPathProbe(File.Exists);
            Check(probe.Exists(plik), "Istniejacy plik musi byc widziany jako obecny.");
            var gotowy = RadioRecordingRowLabels.DescribeHistoryRow(wpis, Teraz, !probe.Exists(plik));
            Check(!gotowy.Contains("brak pliku", StringComparison.OrdinalIgnoreCase),
                $"Nagranie z plikiem nie moze byc czytane jako brakujace: {gotowy}");

            // RZECZYWISTE przeniesienie poza obserwowane miejsce: plik znika.
            var pozaAmc = Path.Combine(root, "poza-amc");
            Directory.CreateDirectory(pozaAmc);
            var nowy = Path.Combine(pozaAmc, "trojka.mp3");
            File.Move(plik, nowy);
            Check(!File.Exists(plik), "Przygotowanie testu: stary plik musi zniknac.");

            probe.Invalidate(plik);
            Check(!probe.Exists(plik), "Po przeniesieniu stara sciezka nie moze byc uznana za obecna.");

            var brakujacy = RadioRecordingRowLabels.DescribeHistoryRow(wpis, Teraz, fileMissing: true);
            Check(brakujacy.Contains("brak pliku", StringComparison.OrdinalIgnoreCase),
                $"Wiersz musi uczciwie powiedziec o braku pliku: {brakujacy}");
            Check(!RadioRecordingRowLabels.IsPlayableNow(wpis, fileMissing: true),
                "Enter nie moze uznawac brakujacego pliku za odtwarzalny.");
            Check(RadioRecordingRowLabels.IsPlayableNow(wpis, fileMissing: false),
                "Istniejacy plik nadal musi byc odtwarzalny.");
            Check(RadioRecordingRowLabels.DescribeMissing(wpis)
                    .Contains("Trójka", StringComparison.Ordinal),
                "Komunikat o braku pliku musi wskazac nagranie.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- c) zmiana nazwy/folderu przepisuje historie --------------------

    private static void RenameRewritesHistoryPathWithoutDuplicate()
    {
        var root = NewDirectory("rename");
        try
        {
            var stary = Path.Combine(root, "Nagrania");
            Directory.CreateDirectory(stary);
            var plik = Path.Combine(stary, "trojka.mp3");
            File.WriteAllBytes(plik, new byte[16]);
            var inny = Path.Combine(stary, "dwojka.mp3");
            File.WriteAllBytes(inny, new byte[16]);

            var historia = new List<RadioRecordingHistorySettings>
            {
                Entry("c1", "Trójka", plik),
                Entry("c2", "Dwójka", inny, finishedHour: 12),
                Entry("c3", "Czwórka", string.Empty, RadioRecordingOutcome.Failed, 13)
            };

            // RZECZYWISTA zmiana nazwy pliku.
            var poZmianie = Path.Combine(stary, "trojka-koncert.mp3");
            File.Move(plik, poZmianie);
            var zmienione = RadioRecordingHistoryPathRewriter.Rewrite(historia, plik, poZmianie);

            Check(zmienione == 1, $"Zmiana nazwy ma przepisac dokladnie jeden wpis: {zmienione}");
            Check(historia.Count == 3, "Przepisanie nie moze dodawac ani usuwac wpisow historii.");
            Check(historia[0].Path == poZmianie,
                $"Wpis historii musi wskazywac nowa sciezke: {historia[0].Path}");
            Check(File.Exists(historia[0].Path), "Przepisana sciezka musi istniec na dysku.");
            Check(historia[1].Path == inny, "Obcy wpis nie moze byc ruszony.");
            Check(historia[2].Path.Length == 0, "Wpis nieudany nie moze dostac sciezki.");

            // RZECZYWISTA zmiana nazwy FOLDERU - wpisy pod nim ida za nim.
            var nowyFolder = Path.Combine(root, "Nagrania radiowe");
            Directory.Move(stary, nowyFolder);
            var poFolderze = RadioRecordingHistoryPathRewriter.Rewrite(historia, stary, nowyFolder);
            Check(poFolderze == 2, $"Zmiana folderu ma przepisac oba pliki: {poFolderze}");
            foreach (var wpis in historia.Where(entry => entry.Path.Length > 0))
            {
                Check(File.Exists(wpis.Path),
                    $"Po zmianie nazwy folderu wpis musi wskazywac istniejacy plik: {wpis.Path}");
                Check(RadioRecordingRowLabels.FolderLabel(wpis.Path) == "folder Nagrania radiowe",
                    $"Wiersz ma czytac NOWY folder: {RadioRecordingRowLabels.FolderLabel(wpis.Path)}");
            }

            // Powtorne przepisanie tej samej zmiany nic nie robi (brak duplikatow).
            Check(RadioRecordingHistoryPathRewriter.Rewrite(historia, stary, nowyFolder) == 0,
                "Powtorne przepisanie tej samej zmiany nie moze nic zmieniac.");
            Check(historia.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() == 3,
                "Historia nie moze zyskac duplikatow wpisow.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- d) przeniesienie poza AMC, ale cel znany ----------------------

    private static void ExternalMoveWithKnownDestinationIsAdopted()
    {
        var root = NewDirectory("adopcja");
        try
        {
            var zrodlo = Path.Combine(root, "Nagrania");
            var cel = Path.Combine(root, "Archiwum");
            Directory.CreateDirectory(zrodlo);
            Directory.CreateDirectory(cel);
            var plik = Path.Combine(zrodlo, "trojka.mp3");
            File.WriteAllBytes(plik, new byte[128]);

            var historia = new List<RadioRecordingHistorySettings> { Entry("d", "Trójka", plik) };

            // Ctrl+X, wklejenie w innym OBSERWOWANYM folderze: plik pojawia sie tam.
            var nowy = Path.Combine(cel, "trojka.mp3");
            File.Move(plik, nowy);

            // Znana para ścieżek pochodzi z rzeczywistego rename, nie z podobnej nazwy.
            Check(RadioRecordingHistoryPathRewriter.Rewrite(historia, plik, nowy) == 1,
                "Znana zmiana miejsca musi przepisać historię.");
            Check(historia[0].Path == nowy, "Wpis musi wskazywać nowy folder.");
            Check(historia.Count == 1, "Przejecie nie moze tworzyc drugiego wpisu historii.");
            Check(RadioRecordingRowLabels.FolderLabel(nowy) == "folder Archiwum",
                "Wiersz musi czytac NOWY folder docelowy.");
            Check(RadioRecordingRowLabels
                    .DescribeHistoryRow(historia[0], Teraz, fileMissing: false)
                    .TrimEnd().EndsWith("folder Archiwum", StringComparison.Ordinal),
                "Po przejeciu wiersz czyta nowy folder na koncu.");

            // Samo Created i brak oryginału NIE dowodzą przeniesienia.
            // Kontrolę cudzej zawartości o tej samej nazwie wykonuje
            // RecordingFilesAcceptanceTests przez produkcyjny callback WPF.

        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- e) przeniesienie bez znanego celu -----------------------------

    private static void ExternalMoveWithoutDestinationKeepsEntryButHonestState()
    {
        var root = NewDirectory("bez-celu");
        try
        {
            var plik = Path.Combine(root, "trojka.mp3");
            File.WriteAllBytes(plik, new byte[16]);
            var historia = new List<RadioRecordingHistorySettings> { Entry("e", "Trójka", plik) };

            File.Delete(plik); // przeniesiony poza wszystkie obserwowane foldery
            var probe = new RecordingPathProbe(File.Exists);
            probe.MarkMissing(plik);

            Check(historia.Count == 1,
                "Brak pliku NIE moze usuwac wpisu historii - to byloby zgubienie danych.");
            var wiersz = RadioRecordingRowLabels.DescribeHistoryRow(
                historia[0], Teraz, probe.IsMissing(plik));
            Check(wiersz.Contains("brak pliku", StringComparison.OrdinalIgnoreCase),
                $"Bez znanego celu wiersz musi byc uczciwy, a nie „gotowy”: {wiersz}");
            Check(wiersz.Contains("Trójka", StringComparison.Ordinal),
                "Wpis musi dalej podawac stacje i czas.");
            Check(wiersz.TrimEnd().EndsWith("folder", StringComparison.Ordinal) == false,
                "Slowo „folder” nie moze zostac bez nazwy.");
            Check(!RadioRecordingRowLabels.IsPlayableNow(historia[0], fileMissing: true),
                "Enter nie moze prowadzic do martwej sciezki.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- f) nieudane/aktywne i chwilowo niedostepny dysk ---------------

    private static void FailedAndActiveEntriesSurviveMissingDisk()
    {
        var nieudane = Entry("f1", "Dwójka", string.Empty, RadioRecordingOutcome.Failed, 12);
        nieudane.Reason = "Zerwane połączenie ze stacją";
        nieudane.ScheduleName = "Poranek";
        var przerwane = Entry("f2", "Czwórka", @"Z:\niedostepny\czworka.mp3",
            RadioRecordingOutcome.Interrupted, 13);
        var historia = new List<RadioRecordingHistorySettings> { nieudane, przerwane };

        // Caly „dysk” niedostepny: KAZDA sciezka wyglada na brakujaca.
        var probe = new RecordingPathProbe(_ => false);
        foreach (var wpis in historia) probe.Exists(wpis.Path);

        Check(historia.Count == 2, "Niedostepny dysk nie moze usuwac wpisow historii.");
        var opisNieudanego = RadioRecordingRowLabels.DescribeHistoryRow(
            nieudane, Teraz, fileMissing: true);
        Check(opisNieudanego.StartsWith("Nieudane", StringComparison.Ordinal),
            $"Nieudane nagranie musi pozostac nieudane: {opisNieudanego}");
        Check(opisNieudanego.Contains("Zerwane połączenie ze stacją", StringComparison.Ordinal),
            "Powod niepowodzenia nie moze zniknac.");
        Check(opisNieudanego.Contains("harmonogram Poranek", StringComparison.Ordinal),
            "Wpis z harmonogramu musi dalej podawac harmonogram.");
        Check(!opisNieudanego.Contains("brak pliku", StringComparison.OrdinalIgnoreCase),
            $"Nieudane nagranie nigdy nie mialo pliku - nie dopisuj mu „brak pliku”: {opisNieudanego}");
        Check(RadioRecordingRowLabels.DescribeHistoryRow(przerwane, Teraz, fileMissing: true)
                .StartsWith("Przerwane", StringComparison.Ordinal),
            "Przerwane nagranie musi pozostac przerwane.");

        // Chwilowa niedostepnosc nie jest przeniesieniem: nie wolno przepisac
        // sciezki na nic ani wyczyscic wpisu.
        Check(RadioRecordingHistoryPathRewriter.Rewrite(historia, przerwane.Path, string.Empty) == 0,
            "Puste nowe miejsce nie moze wyczyscic sciezki w historii.");
        Check(przerwane.Path == @"Z:\niedostepny\czworka.mp3",
            "Sciezka przerwanego nagrania musi przetrwac niedostepny dysk.");
    }

    // ---- g) koszt: zadnego FileExists po calej bibliotece --------------

    private static void ProbeDoesNotTouchWholeLibraryAndCachesFrames()
    {
        var wywolania = new List<string>();
        var probe = new RecordingPathProbe(path =>
        {
            wywolania.Add(path);
            return true;
        });

        var historia = Enumerable.Range(0, 400)
            .Select(i => Entry($"g{i}", "Trójka", Path.Combine(Path.GetTempPath(), $"g{i}.mp3")))
            .ToList();

        // Pierwsza klatka listy: kazda sciezka sprawdzona raz.
        foreach (var wpis in historia) probe.Exists(wpis.Path);
        var poPierwszej = wywolania.Count;
        Check(poPierwszej == 400, $"Pierwsza klatka ma sprawdzic kazda sciezke raz: {poPierwszej}");

        // Kolejne klatki (kazde nacisniecie klawisza w filtrze) - zero dyskowych
        // sprawdzen, inaczej duza kolekcja zamula nawigacje.
        for (var klatka = 0; klatka < 10; klatka++)
        {
            foreach (var wpis in historia) probe.Exists(wpis.Path);
        }
        Check(wywolania.Count == poPierwszej,
            $"Kolejne klatki nie moga dotykac dysku: {wywolania.Count} wywolan.");
        Check(probe.HitCount >= 4000, $"Pamiec klatki musi dzialac: {probe.HitCount} trafien.");

        // Uniewaznienie pojedynczej sciezki sprawdza TYLKO ja.
        probe.Invalidate(historia[7].Path);
        foreach (var wpis in historia) probe.Exists(wpis.Path);
        Check(wywolania.Count == poPierwszej + 1,
            $"Uniewaznienie jednej sciezki nie moze przeskanowac calosci: {wywolania.Count}.");
        Check(wywolania[^1] == historia[7].Path,
            "Sprawdzona ma byc dokladnie uniewazniona sciezka.");

        // Wpisy bez sciezki nie generuja zadnego ruchu na dysku.
        var przedPustymi = wywolania.Count;
        for (var i = 0; i < 50; i++) probe.Exists(string.Empty);
        Check(wywolania.Count == przedPustymi,
            "Pusta sciezka nie moze byc sprawdzana na dysku.");
    }

    // ---- h) daty i kolejnosc po przeniesieniu ---------------------------

    private static void DatesStayLocalAndOrderedAfterMove()
    {
        var root = NewDirectory("daty");
        try
        {
            var starszy = Path.Combine(root, "starszy.mp3");
            var nowszy = Path.Combine(root, "nowszy.mp3");
            File.WriteAllBytes(starszy, new byte[8]);
            File.WriteAllBytes(nowszy, new byte[8]);
            var historia = new List<RadioRecordingHistorySettings>
            {
                Entry("h1", "Trójka", starszy, finishedHour: 10),
                Entry("h2", "Dwójka", nowszy, finishedHour: 14)
            };

            var przeniesiony = Path.Combine(root, "przeniesiony.mp3");
            File.Move(starszy, przeniesiony);
            RadioRecordingHistoryPathRewriter.Rewrite(historia, starszy, przeniesiony);

            // Czas zakonczenia opisuje nagranie, nie plik - przeniesienie go nie zmienia.
            Check(historia[0].FinishedUtcTicks
                    == new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc).Ticks,
                "Przeniesienie pliku nie moze zmieniac czasu nagrania.");

            var lokalny = new DateTime(historia[0].FinishedUtcTicks, DateTimeKind.Utc).ToLocalTime();
            var wiersz = RadioRecordingRowLabels.DescribeHistoryRow(
                historia[0], Teraz, fileMissing: false);
            Check(wiersz.Contains(lokalny.ToString("HH:mm"), StringComparison.Ordinal),
                $"Wiersz musi czytac czas LOKALNY: {wiersz} (oczekiwano {lokalny:HH:mm})");

            var posortowane = historia
                .OrderByDescending(entry => entry.FinishedUtcTicks)
                .Select(entry => entry.Id)
                .ToArray();
            Check(posortowane[0] == "h2" && posortowane[1] == "h1",
                "Kolejnosc od najnowszego musi przetrwac przeniesienie pliku.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
