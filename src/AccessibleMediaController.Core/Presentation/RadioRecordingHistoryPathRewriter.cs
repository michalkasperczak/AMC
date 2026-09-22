using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Przepisuje odwolania Historii nagrywania po RZECZYWISTEJ zmianie miejsca
/// pliku.
///
/// Wymaganie historii nagrań: po Ctrl+X i
/// wklejeniu pliku poza AMC w Historii nagrywania zostawal wiersz „Nagrano
/// nazwa”, a Enter prowadzil do NIEISTNIEJACEJ starej sciezki.
///
/// Reguly, ktorych ten typ pilnuje:
/// * Gdy nowe miejsce jest ZNANE - przepisujemy sciezke w istniejacym wpisie.
///   NIE dodajemy drugiego wpisu, bo to jedno i to samo nagranie.
/// * Gdy nowego miejsca NIE znamy - wpis zostaje nietkniety. Kasowanie wpisu
///   zgubiloby historie, a chwilowo niedostepny dysk wygladalby jak celowe
///   przeniesienie. Uczciwy stan brakujacego pliku pokazuje warstwa odczytu
///   (<see cref="RadioRecordingRowLabels"/>), nie usuwanie danych.
/// * Puste nowe miejsce nigdy nie czysci sciezki w historii.
///
/// Logika siedzi w Core, zeby dala sie zmierzyc bez uruchamiania WPF.
/// </summary>
public static class RadioRecordingHistoryPathRewriter
{
    /// <summary>
    /// Przepisuje wpisy wskazujace <paramref name="oldPath"/> (sam plik ALBO
    /// cokolwiek pod tym folderem) na <paramref name="newPath"/>.
    /// Zwraca liczbe zmienionych wpisow; 0 oznacza, ze nic nie bylo do zmiany.
    /// </summary>
    public static int Rewrite(
        IReadOnlyList<RadioRecordingHistorySettings> history,
        string oldPath,
        string newPath)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath)) return 0;

        var normalizedOld = Normalize(oldPath);
        var normalizedNew = Normalize(newPath);
        if (normalizedOld.Length == 0 || normalizedNew.Length == 0) return 0;
        if (string.Equals(normalizedOld, normalizedNew, StringComparison.OrdinalIgnoreCase)) return 0;

        var changed = 0;
        foreach (var entry in history)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Path)) continue;
            var current = Normalize(entry.Path);
            if (current.Length == 0) continue;

            string? replacement = null;
            if (string.Equals(current, normalizedOld, StringComparison.OrdinalIgnoreCase))
            {
                replacement = normalizedNew;
            }
            else if (IsUnder(current, normalizedOld))
            {
                replacement = System.IO.Path.Combine(
                    normalizedNew,
                    System.IO.Path.GetRelativePath(normalizedOld, current));
            }

            if (replacement is null
                || string.Equals(replacement, current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            entry.Path = replacement;
            changed++;
        }
        return changed;
    }

    private static bool IsUnder(string candidate, string root)
    {
        var prefix = System.IO.Path.TrimEndingDirectorySeparator(root)
            + System.IO.Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or System.IO.PathTooLongException)
        {
            return string.Empty;
        }
    }
}
