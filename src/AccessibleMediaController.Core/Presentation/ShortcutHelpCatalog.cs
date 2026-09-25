using System.Globalization;
using System.Text;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Core.Presentation;

public sealed record ShortcutHelpEntry(
    string SectionId,
    string DisplayName,
    string Shortcut,
    string Context,
    string? CommandId = null)
{
    public bool CanExecute => !string.IsNullOrWhiteSpace(CommandId)
        && !string.Equals(CommandId, CommandIds.Help, StringComparison.Ordinal);

    public string Label => $"{DisplayName}. Skrót: {Shortcut}. Kontekst: {Context}";

    public override string ToString() => Label;
}

public sealed record ShortcutHelpSection(
    string Id,
    string Label,
    IReadOnlyList<ShortcutHelpEntry> Entries)
{
    public override string ToString() => Label;
}

public static class ShortcutHelpCatalog
{
    private static readonly (string Id, string Label)[] SectionOrder =
    [
        ("general", "Ogólne i sesje"),
        ("lists", "Listy i widoki"),
        ("player", "Odtwarzacz"),
        ("search", "Wyszukiwanie i filtrowanie"),
        ("library", "Biblioteka lokalna"),
        ("podcasts", "Podcasty i YouTube"),
        ("radio", "Radio internetowe"),
        ("collections", "Playlisty i Zakładki"),
        ("settings", "Ustawienia i pomoc"),
        ("prefix", "Warstwa prefiksowa")
    ];

    public static IReadOnlyList<ShortcutHelpSection> Create(
        KeyboardProfile profile,
        AppSettings settings)
    {
        var entries = CommandPaletteSearch.CreateEntries(profile, settings, includeCommandPalette: true)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.LocalShortcut)
                || !string.IsNullOrWhiteSpace(entry.PrefixShortcut))
            .Select(CreateCommandEntry)
            .Concat(CreateInformationalEntries())
            .ToArray();

        return SectionOrder
            .Select(section => new ShortcutHelpSection(
                section.Id,
                section.Label,
                entries
                    .Where(entry => string.Equals(entry.SectionId, section.Id, StringComparison.Ordinal))
                    .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray()))
            .Where(section => section.Entries.Count > 0)
            .ToArray();
    }

    /// <summary>
    /// Sekcje istotne dla miejsca, w ktorym uzytkownik wlasnie jest. Sluzy
    /// pomocy KONTEKSTOWEJ pod Shift+F1 (ZGLOSZENIE Michala 15.09.2026: "te
    /// podpowiedzi powinny byc dostepne w calym programie kontekstowo pod
    /// klawiszem Shift+F1").
    ///
    /// Kolejnosc ma znaczenie: najwazniejsza sekcja idzie pierwsza, zeby po
    /// otwarciu okna czytnik od razu czytal to, co dotyczy biezacego widoku.
    /// Sekcje ogolne zostaja na koncu - nie usuwamy ich, bo uzytkownik czasem
    /// szuka polecenia z innego miejsca programu.
    /// </summary>
    public static IReadOnlyList<ShortcutHelpSection> CreateForContext(
        KeyboardProfile profile,
        AppSettings settings,
        string? sessionId,
        bool inPlayer)
    {
        var all = Create(profile, settings);
        var preferred = new List<string>();
        if (inPlayer) preferred.Add("player");
        switch (sessionId)
        {
            case "radio":
                preferred.Add("radio");
                break;
            case "podcasts":
                preferred.Add("podcasts");
                break;
            case "local":
                preferred.Add("library");
                break;
        }
        if (!inPlayer) preferred.Add("lists");
        if (preferred.Count == 0) return all;

        // ZGLOSZENIE Michala 15.09.2026: Shift+F1 ma pokazywac TO, CO MOZNA
        // ZROBIC TU I TERAZ. Wczesniej tylko przestawialo kolejnosc sekcji, a
        // dalej wypisywalo wszystkie - czyli w odtwarzaczu radia byla tez
        // biblioteka lokalna i podcasty. Teraz sekcje nie na temat wypadaja.
        // "general" i "settings" zostaja, bo tam jest wyjscie, pomoc i
        // ustawienia - dzialaja zawsze.
        var widoczne = new HashSet<string>(preferred, StringComparer.Ordinal)
        {
            "general",
            "settings"
        };

        var wybrane = all
            .Where(section => widoczne.Contains(section.Id) && section.Entries.Count > 0)
            .OrderBy(section =>
            {
                var index = preferred.IndexOf(section.Id);
                return index < 0 ? preferred.Count : index;
            })
            .ToArray();

        // Gdyby filtr wyciol wszystko (nieznana sesja), lepiej pokazac pelny
        // spis niz puste okno.
        return wybrane.Length > 0 ? wybrane : all;
    }

    public static IReadOnlyList<ShortcutHelpEntry> Filter(
        IEnumerable<ShortcutHelpSection> sections,
        string query)
    {
        var tokens = FoldForSearch(query)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries = sections.SelectMany(section => section.Entries);
        if (tokens.Length == 0) return entries.ToArray();

        return entries
            .Where(entry =>
            {
                var searchable = FoldForSearch(entry.Label);
                return tokens.All(token => searchable.Contains(token, StringComparison.Ordinal));
            })
            .ToArray();
    }

    private static ShortcutHelpEntry CreateCommandEntry(CommandPaletteEntry entry)
    {
        var (localShortcut, localContext) = SplitLocalShortcut(entry.LocalShortcut);
        var hasPrefix = !string.IsNullOrWhiteSpace(entry.PrefixShortcut);
        var shortcut = localShortcut;
        var context = localContext;
        if (hasPrefix)
        {
            var prefixed = $"po prefiksie {entry.PrefixShortcut}";
            shortcut = string.IsNullOrWhiteSpace(shortcut) ? prefixed : $"{shortcut}; {prefixed}";
            context = string.IsNullOrWhiteSpace(context)
                ? "warstwa prefiksowa"
                : $"{context} oraz warstwa prefiksowa";
        }

        return new ShortcutHelpEntry(
            GetSectionId(entry.CommandId, string.IsNullOrWhiteSpace(localShortcut)),
            entry.DisplayName,
            shortcut ?? string.Empty,
            Capitalize(context ?? "aktywne okno AMC"),
            entry.CommandId);
    }

    private static (string? Shortcut, string? Context) SplitLocalShortcut(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, null);
        var contextStart = value.LastIndexOf(" (", StringComparison.Ordinal);
        if (contextStart <= 0 || !value.EndsWith(')')) return (value, "aktywne okno AMC");
        return (value[..contextStart], value[(contextStart + 2)..^1]);
    }

    private static string GetSectionId(string commandId, bool prefixOnly)
    {
        if (prefixOnly) return "prefix";
        if (commandId.StartsWith("settings.", StringComparison.Ordinal)
            || commandId is CommandIds.Help or CommandIds.ContextHelp or CommandIds.KeyboardHelp)
        {
            return "settings";
        }
        if (commandId.StartsWith("transport.", StringComparison.Ordinal)
            || commandId.StartsWith("editing.clip.", StringComparison.Ordinal)
            || commandId.StartsWith("information.time", StringComparison.Ordinal)
            || commandId is CommandIds.ViewNowPlaying or CommandIds.AddBookmark
                or CommandIds.AddNamedBookmark or CommandIds.PreviousBookmark
                or CommandIds.NextBookmark or CommandIds.ViewChapters
                or CommandIds.AddNamedChapter or CommandIds.PreviousChapter
                or CommandIds.NextChapter)
        {
            return "player";
        }
        if (commandId.StartsWith("session.", StringComparison.Ordinal)) return "general";
        if (commandId.StartsWith("local.", StringComparison.Ordinal)
            || commandId is CommandIds.ViewFolders or CommandIds.ViewAllLocalFiles
                or CommandIds.ViewCustomLocalOrder or CommandIds.OpenLocalFiles
                or CommandIds.OpenLocalFolder)
        {
            return "library";
        }
        if (commandId.StartsWith("radio.", StringComparison.Ordinal)
            || commandId is CommandIds.ViewRadio or CommandIds.StartRadio)
        {
            return "radio";
        }
        if (commandId.StartsWith("podcast.", StringComparison.Ordinal)
            || commandId == CommandIds.DownloadInService)
        {
            return "podcasts";
        }
        if (commandId is CommandIds.FilterCurrent or CommandIds.SearchCurrent
            or CommandIds.SearchAll or CommandIds.CommandPalette)
        {
            return "search";
        }
        if (commandId is CommandIds.ViewPlaylists or CommandIds.ManagePlaylists
            or CommandIds.ViewBookmarks)
        {
            return "collections";
        }
        return "lists";
    }

    private static IEnumerable<ShortcutHelpEntry> CreateInformationalEntries()
    {
        yield return Info("general", "Zamknij aplikację", "Alt+F4", "aktywne okno AMC");
        yield return Info("general", "Wróć lub zamknij bieżący poziom", "Escape", "okno, menu, filtr lub odtwarzacz");
        yield return Info("general", "Otwórz spis skrótów", "?", "poza polem tekstowym");
        yield return Info("lists", "Poprzedni lub następny element", "Strzałka w górę lub w dół", "lista");
        yield return Info("lists", "Odczytaj format, bitrate, wielkość i inne dostępne parametry", "Strzałka w lewo", "lista multimediów lub wyników wyszukiwania");
        yield return Info("lists", "Zaznacz ciąg elementów", "Shift+strzałka w górę lub w dół", "lista wielokrotnego wyboru");
        yield return Info("lists", "Szybkie przejście według początku nazwy", "litery", "lista multimediów");
        yield return Info("lists", "Otwórz element albo rozpocznij odtwarzanie", "Enter", "lista multimediów");
        yield return Info("lists", "Kopiuj nazwy zaznaczonych elementów", "Ctrl+C", "lista multimediów lub wyniki wyszukiwania");
        yield return Info("lists", "Kopiuj lokalizacje i pliki", "Ctrl+Shift+C", "lokalne multimedia lub wyniki wyszukiwania");
        yield return Info("lists", "Wytnij lokalne pliki do przeniesienia", "Ctrl+X", "lokalna lista multimediów");
        yield return Info("lists", "Dodaj pliki ze schowka", "Ctrl+V", "lokalna Biblioteka, Kolejka lub playlista");
        yield return Info("lists", "Zaznacz wszystkie elementy", "Ctrl+A", "lista wielokrotnego wyboru");
        yield return Info("lists", "Cofnij ostatnią zmianę kolekcji", "Ctrl+Z", "Ulubione, Biblioteka, Kolejka lub playlista");
        yield return Info("lists", "Usuń z bieżącego widoku", "Delete", "lista; plik na dysku pozostaje bez zmian");
        yield return Info("lists", "Przenieś pliki do Kosza", "Shift+Delete", "lokalna lista po potwierdzeniu");
        yield return Info("lists", "Wróć poziom wyżej", "Backspace", "folder, album, playlista lub Zakładki");
        yield return Info("lists", "Poprzedni lub następny widok", "Alt+strzałka w lewo lub w prawo", "lista multimediów");
        yield return Info("lists", "Kolejność dodania, najnowsze na początku", "Alt+1", "Biblioteka lub Ulubione; lokalnie Alt+1 pokazuje foldery");
        yield return Info("lists", "Kolejność alfabetyczna", "Alt+2", "Biblioteka lub Ulubione; lokalnie płaska lista plików");
        yield return Info("lists", "Kolejność własna", "Alt+3", "Biblioteka lub Ulubione");

        yield return Info("collections", "Utwórz playlistę", "Insert", "lista playlist");
        yield return Info("collections", "Zmień nazwę playlisty", "F2", "lista playlist");

        yield return Info("radio", "Nowa stacja: nazwa i adres URL", "Ctrl+N", "Radio internetowe");
        yield return Info("radio", "Edytuj nazwę i adres strumienia", "F2", "lista stacji radia internetowego");
        yield return Info("radio", "Zaznacz stacje do przeniesienia", "Ctrl+X", "Ulubione radia internetowego");
        yield return Info("radio", "Przenieś zaznaczone stacje przed bieżącą", "Ctrl+V", "Ulubione radia internetowego");
        yield return Info("radio", "Cofnij lub przewiń w buforze transmisji", "Strzałka w lewo lub w prawo", "odtwarzacz radia");
        yield return Info("radio", "Przejdź do 0–90% aktualnego bufora transmisji", "0–9", "odtwarzacz radia");
        yield return Info("radio", "Przejdź do początku bufora transmisji", "Home", "odtwarzacz radia");
        yield return Info("radio", "Wróć do transmisji na żywo", "End", "odtwarzacz radia");
        yield return Info("radio", "Wycisz sam odsłuch, bez przerywania odbioru ani nagrania", "Ctrl+M", "odtwarzacz radia");
        yield return Info("radio", "Poprzednia lub następna stacja, bez zatrzymywania nagrań", "Page Up lub Page Down", "odtwarzacz radia");
        yield return Info("radio", "Dodaj szybką zakładkę do zapisywanego pliku", "B", "odtwarzacz radia w trakcie nagrywania");
        yield return Info("radio", "Dodaj nazwaną zakładkę do zapisywanego pliku", "Shift+B", "odtwarzacz radia w trakcie nagrywania");
        yield return Info("radio", "Rozpocznij lub zakończ nagrywanie", "Ctrl+R", "lista lub odtwarzacz radia");
        yield return Info("radio", "Wstrzymaj lub wznów wybrane nagranie", "Shift+Spacja", "nagrywana stacja na liście lub w odtwarzaczu radia");
        yield return Info("radio", "Pokaż aktualnie nagrywane stacje; Escape wraca do wcześniejszego widoku", "Alt+R", "lista radia internetowego");
        yield return Info("radio", "Pokaż historię nagrywania: nagrania gotowe oraz próby nieudane z powodem", "Alt+Shift+R", "Pliki lokalne lub Radio internetowe");
        yield return Info("collections", "Pokaż presety aktywnej sesji", "Ctrl+Alt+P", "Pliki lokalne lub Radio; skrót zarezerwowany także dla przyszłych usług");
        yield return Info("collections", "Utwórz albo przypisz preset; w WiiM przypisz lokalny skrót do gotowego presetu urządzenia", "Ctrl+Alt+Shift+P", "Sesja obsługująca presety");
    }

    private static ShortcutHelpEntry Info(
        string sectionId,
        string displayName,
        string shortcut,
        string context) => new(sectionId, displayName, shortcut, Capitalize(context));

    private static string Capitalize(string value) => string.IsNullOrWhiteSpace(value)
        ? value
        : char.ToUpper(value[0], CultureInfo.CurrentCulture) + value[1..];

    private static string FoldForSearch(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character switch
            {
                'ł' or 'Ł' => 'L',
                _ => char.ToUpperInvariant(character)
            });
        }
        return builder.ToString();
    }
}
