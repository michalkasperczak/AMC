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
}

/// <summary>
/// Jedna pozycja listy "Folder nagrywania" w oknie harmonogramu.
/// </summary>
/// <param name="Kind">Rodzaj miejsca zapisu.</param>
/// <param name="Label">
/// To, co wypowiada czytnik ekranu. Sama nazwa miejsca, BEZ sciezki i BEZ
/// instrukcji obslugi - patrz uwaga o zgloszeniu ponizej.
/// </param>
/// <param name="Path">
/// Sciezka zwiazana z ta pozycja albo <c>null</c>, gdy jej nie znamy
/// (np. folder domyslny rozstrzyga sie dopiero przy nagrywaniu).
/// </param>
public sealed record RadioScheduleFolderChoice(
    RadioScheduleFolderKind Kind,
    string Label,
    string? Path)
{
    /// <summary>
    /// ZGLOSZENIE Michala 17.09.2026 (trzecie, po wersji 391): czytnik ekranu NADAL
    /// wypowiadal "RadioScheduleFolderChoice { Kind = Custom, Label = ..., Path = }",
    /// mimo DisplayMemberPath="Label" w XAML.
    ///
    /// PRZYCZYNA, ktora przegapilem: DisplayMemberPath rzadzi tylko tym, co WIDAC.
    /// Warstwa dostepnosci (UIA) buduje nazwe pozycji zwinietego pola kombi z samego
    /// OBIEKTU, wiec siega do ToString(). Rekord C# generuje ToString() wypisujacy
    /// wszystkie pola - i dokladnie to slyszal uzytkownik.
    ///
    /// Dlatego ToString() musi zwracac to samo, co widac. To nie jest kosmetyka ani
    /// obejscie: dla uzytkownika czytnika ToString() rekordu POKAZYWANEGO w liscie
    /// jest tekstem interfejsu.
    /// </summary>
    public override string ToString() => Label;
}

/// <summary>
/// Buduje liste miejsc zapisu dla harmonogramu nagrania radia.
///
/// ZGLOSZENIE Michala (17.09.2026, pierwsze): folder wlasny stacji dzialal pod
/// spodem, ale w oknie tworzenia harmonogramu nie bylo go widac - byly tylko dwie
/// pozycje, "domyslny" i "uzytkownika". Program zapisywal wiec nagranie do folderu
/// stacji NIE MOWIAC o tym, a uzytkownik nie mial jak tego wybrac ani zobaczyc.
/// Dlatego pozycja z folderem stacji pojawia sie tylko wtedy, gdy wybrana stacja
/// naprawde ma wlasny folder.
///
/// ZGLOSZENIE Michala (17.09.2026, drugie - POPRAWKA MOJEGO NADMIERNEGO
/// UPROSZCZENIA): pozbylem sie przycisku "Wybierz folder" i wsadzilem jego role
/// do listy jako ostatnia pozycje "Wybierz inny folder...". To bylo zle z trzech
/// powodow, ktore uzytkownik zglosil wprost:
/// 1. Zejscie strzalka na te pozycje SAMO otwieralo systemowe okno wyboru folderu,
///    bez zadnej decyzji uzytkownika. Przewijanie listy nie moze nic uruchamiac.
/// 2. Okno zdawalo sie zawieszac ("program nie odpowiada") przy ruchu w gore,
///    bo otwarcie modalnego dialogu z wnetrza zmiany zaznaczenia blokuje watek UI.
/// 3. Czytnik ekranu wypowiadal techniczna instrukcje obslugi doklejona do
///    pozycji i do pola.
/// Uklad docelowy, podany przez uzytkownika: pole kombi z miejscami zapisu, potem
/// Tab na pole edycyjne ze sciezka, potem Tab na przycisk "Wybierz folder".
/// Wybor folderu otwiera sie WYLACZNIE z tego przycisku.
///
/// Dlatego w tym typie nie ma juz rodzaju "Browse" - pozycja listy nie jest
/// przyciskiem. Etykiety sa krotkie i nie niosa sciezki, bo sciezke pokazuje
/// (i wypowiada) osobne pole edycyjne obok.
/// </summary>
public static class RadioScheduleFolderChoices
{
    /// <summary>
    /// Buduje pozycje listy dla stacji o podanym wlasnym folderze.
    ///
    /// Lista ma zawsze pozycje "domyslny" i "osobny folder", a pozycje
    /// "folder tej stacji" tylko wtedy, gdy stacja ma wlasny folder. Pozycja
    /// osobnego folderu istnieje TAKZE przy nowym harmonogramie - inaczej nie
    /// byloby jak zglosic zamiaru wskazania wlasnego miejsca zapisu.
    /// </summary>
    /// <param name="stationFolder">
    /// Wlasny folder wybranej stacji albo <c>null</c>, gdy stacja go nie ma.
    /// </param>
    /// <param name="customFolder">
    /// Osobny folder juz zapisany w tym harmonogramie albo <c>null</c> przy nowym.
    /// Nie zmienia skladu listy - sluzy tylko za sciezke pozycji "osobny folder".
    /// </param>
    public static IReadOnlyList<RadioScheduleFolderChoice> Build(
        string? stationFolder,
        string? customFolder)
    {
        var station = string.IsNullOrWhiteSpace(stationFolder) ? null : stationFolder.Trim();
        var custom = string.IsNullOrWhiteSpace(customFolder) ? null : customFolder.Trim();

        var choices = new List<RadioScheduleFolderChoice>
        {
            new(RadioScheduleFolderKind.Default, "Domyślny folder nagrywania", null),
        };

        if (station is not null)
        {
            choices.Add(new RadioScheduleFolderChoice(
                RadioScheduleFolderKind.Station,
                "Folder tej stacji",
                station));
        }

        choices.Add(new RadioScheduleFolderChoice(
            RadioScheduleFolderKind.Custom,
            "Osobny folder dla tego harmonogramu",
            custom));

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
            var saved = savedCustomFolder.Trim();

            // Gdy zapisana sciezka to dokladnie folder stacji, uczciwiej zaznaczyc
            // pozycje stacji - uzytkownik slyszy wtedy, SKAD ta sciezka sie bierze.
            var station = choices.FirstOrDefault(choice =>
                choice.Kind == RadioScheduleFolderKind.Station
                && choice.Path is { } path
                && string.Equals(path, saved, StringComparison.OrdinalIgnoreCase));
            if (station is not null) return station;

            var custom = choices.FirstOrDefault(choice =>
                choice.Kind == RadioScheduleFolderKind.Custom);
            if (custom is not null) return custom;
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
