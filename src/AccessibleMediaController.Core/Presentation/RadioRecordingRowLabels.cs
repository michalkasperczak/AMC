using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Wiersz Historii nagrywania czytany przy zwyklym przechodzeniu strzalkami.
///
/// Wymaganie historii nagrań: wiersz ma podac
/// uzyteczna nazwe, czas/date, a NA KONCU folder, do ktorego nagranie zapisano.
/// Wczesniej folder dawalo sie poznac tylko przez skopiowanie sciezki (Ctrl+C)
/// albo wejscie we wlasciwosci - czyli nie przy zwyklej nawigacji.
///
/// Folder idzie na KONIEC, bo to informacja pomocnicza: przy szybkim
/// przegladaniu czytnik ma najpierw powiedziec, co to za nagranie i kiedy
/// powstalo. Czytamy sama NAZWE folderu, nie pelna sciezke - pelna sciezka
/// zaglusza wiersz i powtarza te same nadrzedne katalogi w kazdym wpisie.
///
/// Druga czesc zgloszenia (punkt3): po przeniesieniu pliku poza AMC wiersz
/// nadal mowil „Nagrano”, a Enter szedl w martwa sciezke. Dlatego stan
/// brakującego pliku jest tu jawnym parametrem - wolajacy podaje wynik
/// TANIEJ obserwacji, a nie synchroniczny <c>File.Exists</c> po calej
/// bibliotece przy kazdej klatce listy (patrz <see cref="RecordingPathProbe"/>).
/// </summary>
public static class RadioRecordingRowLabels
{
    private const string FolderPrefix = "folder ";

    /// <summary>
    /// Nazwa folderu docelowego gotowa do odczytu, albo <c>null</c>, gdy
    /// nagranie nie ma sciezki (proba nieudana) lub sciezki nie da sie
    /// zinterpretowac. Nigdy nie zwraca samego slowa „folder” bez nazwy.
    /// </summary>
    public static string? FolderLabel(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string? name;
        try
        {
            var normalized = System.IO.Path.IsPathFullyQualified(path.Trim())
                ? System.IO.Path.GetFullPath(path.Trim()) : path.Trim();
            var directory = System.IO.Path.GetDirectoryName(normalized);
            if (string.IsNullOrWhiteSpace(directory)) return null;
            name = System.IO.Path.GetFileName(
                System.IO.Path.TrimEndingDirectorySeparator(directory));
            // Korzen dysku (C:\, /) nie ma nazwy pliku - czytamy wtedy sam korzen.
            if (string.IsNullOrWhiteSpace(name)) name = directory;
        }
        catch (Exception exception) when (
            exception is ArgumentException or System.IO.PathTooLongException or NotSupportedException)
        {
            return null;
        }
        return string.IsNullOrWhiteSpace(name) ? null : FolderPrefix + name;
    }

    /// <summary>
    /// Dopisuje folder docelowy na KONIEC gotowej etykiety wiersza. Idempotentne:
    /// etykieta, ktora juz konczy sie tym folderem, wraca bez zmian, zeby przy
    /// odswiezeniu listy nie czytac folderu dwa razy.
    /// </summary>
    public static string AppendFolder(string label, string? path)
    {
        var folder = FolderLabel(path);
        if (folder is null) return label;
        if (label.TrimEnd().EndsWith(folder, StringComparison.Ordinal)) return label;
        return label.Length == 0 ? folder : $"{label}, {folder}";
    }

    /// <summary>Uzyteczna nazwa nagrania: nazwa pliku bez rozszerzenia.</summary>
    public static string? FileNameLabel(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path.Trim());
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception exception) when (
            exception is ArgumentException or System.IO.PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Czy Enter ma czego odtworzyc TERAZ. <paramref name="fileMissing"/> pochodzi
    /// z obserwacji folderu albo z walidacji przy aktywacji - nie ze skanu
    /// biblioteki. Dzieki temu wpis po przeniesieniu pliku nie udaje gotowego.
    /// </summary>
    public static bool IsPlayableNow(RadioRecordingHistorySettings entry, bool fileMissing)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return RadioRecordingHistoryLabels.HasPlayableFile(entry) && !fileMissing;
    }

    /// <summary>
    /// Komunikat po Enterze na wpisie, ktorego plik zniknal z dysku (przeniesiony
    /// poza AMC albo usuniety). Mowi wprost, czego nie ma.
    /// </summary>
    public static string DescribeMissing(RadioRecordingHistorySettings entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!RadioRecordingHistoryLabels.HasPlayableFile(entry))
            return RadioRecordingHistoryLabels.DescribeUnplayable(entry);
        var station = string.IsNullOrWhiteSpace(entry.StationName)
            ? "nieznanej stacji"
            : entry.StationName;
        var name = FileNameLabel(entry.Path);
        var folder = FolderLabel(entry.Path);
        var where = folder is null ? string.Empty : $", {folder}";
        return name is null
            ? $"Plik nagrania {station} nie istnieje już na dysku"
            : $"Plik nagrania {station} nie istnieje już na dysku: {name}{where}";
    }

    /// <summary>
    /// Pelny wiersz Historii nagrywania: skutek, stacja, nazwa pliku, czas/data,
    /// ewentualny powod, a na samym koncu folder docelowy.
    ///
    /// Kolejnosc jest celowa i wynika ze zgloszenia: skutek pierwszy (czytnik od
    /// razu odrozni gotowe od przerwanego), potem co i kiedy, na koncu gdzie.
    /// </summary>
    public static string DescribeHistoryRow(
        RadioRecordingHistorySettings entry,
        DateTime nowUtc,
        bool fileMissing, bool fileUnavailable = false)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Baza z istniejacej reguly: skutek, stacja, czas/data, harmonogram,
        // liczba plikow, powod. Nie powtarzamy tu tych pol wlasnym kodem, zeby
        // nie rozjechac sie z reszta programu.
        var label = RadioRecordingHistoryLabels.Describe(entry, nowUtc, fileMissing, fileUnavailable);

        // Uzyteczna nazwa pliku. Wchodzi ZA stacja i PRZED czasem, ale tylko
        // wtedy, gdy nie powtarza nazwy stacji ani calego wiersza - Michal
        // wyraznie prosil, by nie dublowac pol.
        var name = FileNameLabel(entry.Path);
        if (name is not null && !label.Contains(name, StringComparison.Ordinal))
        {
            var station = string.IsNullOrWhiteSpace(entry.StationName)
                ? "Nieznana stacja"
                : entry.StationName;
            var anchor = $", {station},";
            var at = label.IndexOf(anchor, StringComparison.Ordinal);
            label = at >= 0
                ? label.Insert(at + anchor.Length, $" {name},")
                : $"{label}, {name}";
        }

        return AppendFolder(label, entry.Path);
    }
}
