namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Rodzaj folderu wybranego dla harmonogramu nagrania.
/// </summary>
public enum RadioScheduleFolderKind
{
    /// <summary>Folder ogolny z ustawien nagrywania.</summary>
    Default,

    /// <summary>Wlasny folder TEJ stacji (z Alt+Shift+Enter).</summary>
    Station,

    /// <summary>Osobny folder wskazany tylko dla tego harmonogramu.</summary>
    Custom,

    /// <summary>Pozycja otwierajaca systemowy wybor folderu.</summary>
    Browse,
}

/// <summary>
/// Jedna pozycja listy "Folder nagrywania" w oknie harmonogramu.
/// </summary>
public sealed record RadioScheduleFolderChoice(
    RadioScheduleFolderKind Kind,
    string Label,
    string? Path);

/// <summary>
/// Buduje liste miejsc zapisu dla harmonogramu nagrania radia.
///
/// ZGLOSZENIE Michala (17.09.2026): folder wlasny stacji dzialal pod spodem, ale
/// w oknie tworzenia harmonogramu nie bylo go widac - byly tylko dwie pozycje,
/// "domyslny" i "uzytkownika". Program zapisywal wiec nagranie do folderu stacji
/// NIE MOWIAC o tym, a uzytkownik nie mial jak tego wybrac ani zobaczyc.
///
/// Dlatego lista jest KONTEKSTOWA: pozycja z folderem stacji pojawia sie tylko
/// wtedy, gdy wybrana stacja naprawde ma wlasny folder, i podaje jego sciezke
/// (czytnik ekranu wypowiada ja od razu, bez wchodzenia w osobne pole).
/// Gdy stacja swojego folderu nie ma - pozycji nie ma, bo puste opcje tylko
/// wydluzaja liste do przewijania strzalkami.
/// </summary>
public static class RadioScheduleFolderChoices
{
    public const string BrowseLabel = "Wybierz inny folder…";

    /// <summary>
    /// Buduje pozycje listy dla stacji o podanym wlasnym folderze.
    /// </summary>
    /// <param name="stationFolder">
    /// Wlasny folder wybranej stacji albo <c>null</c>, gdy stacja go nie ma.
    /// </param>
    /// <param name="customFolder">
    /// Osobny folder juz zapisany w tym harmonogramie albo <c>null</c> przy nowym.
    /// </param>
    public static IReadOnlyList<RadioScheduleFolderChoice> Build(
        string? stationFolder,
        string? customFolder)
    {
        var choices = new List<RadioScheduleFolderChoice>
        {
            new(RadioScheduleFolderKind.Default, "Domyślny folder nagrywania", null),
        };

        if (!string.IsNullOrWhiteSpace(stationFolder))
        {
            var path = stationFolder.Trim();
            choices.Add(new RadioScheduleFolderChoice(
                RadioScheduleFolderKind.Station,
                $"Folder tej stacji — {path}",
                path));
        }

        // Osobny folder tego harmonogramu pokazujemy jako pozycje TYLKO wtedy, gdy
        // jest juz wybrany. Przy nowym harmonogramie nie ma czego pokazac, wiec
        // uzytkownik siega po ostatnia pozycje, ktora otwiera wybor folderu.
        if (!string.IsNullOrWhiteSpace(customFolder))
        {
            var path = customFolder.Trim();
            // Gdy uzytkownik wskazal dokladnie folder stacji, nie dublujemy pozycji
            // - inaczej lista mialaby dwa wpisy o tej samej sciezce i nie dalyby sie
            // odroznic sluchem.
            var duplicatesStation = !string.IsNullOrWhiteSpace(stationFolder)
                && string.Equals(path, stationFolder.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!duplicatesStation)
            {
                choices.Add(new RadioScheduleFolderChoice(
                    RadioScheduleFolderKind.Custom,
                    $"Folder tego planu — {path}",
                    path));
            }
        }

        choices.Add(new RadioScheduleFolderChoice(
            RadioScheduleFolderKind.Browse,
            BrowseLabel,
            null));
        return choices;
    }

    /// <summary>
    /// Ktora pozycje zaznaczyc, gdy okno sie otwiera.
    /// Zapisany osobny folder wygrywa, bo to jawna decyzja uzytkownika;
    /// dalej folder stacji, gdy ustawienie programu tak mowi; na koncu domyslny.
    /// </summary>
    public static RadioScheduleFolderChoice ResolveInitial(
        IReadOnlyList<RadioScheduleFolderChoice> choices,
        string? savedCustomFolder,
        bool preferStationFolder)
    {
        ArgumentNullException.ThrowIfNull(choices);

        if (!string.IsNullOrWhiteSpace(savedCustomFolder))
        {
            var saved = choices.FirstOrDefault(choice =>
                choice.Path is { } path
                && string.Equals(path, savedCustomFolder.Trim(), StringComparison.OrdinalIgnoreCase));
            if (saved is not null) return saved;
        }

        if (preferStationFolder)
        {
            var station = choices.FirstOrDefault(choice =>
                choice.Kind == RadioScheduleFolderKind.Station);
            if (station is not null) return station;
        }

        return choices.First(choice => choice.Kind == RadioScheduleFolderKind.Default);
    }
}
