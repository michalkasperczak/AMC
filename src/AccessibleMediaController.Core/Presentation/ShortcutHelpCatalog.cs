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
            || commandId is CommandIds.Help or CommandIds.KeyboardHelp)
        {
            return "settings";
        }
        if (commandId.StartsWith("transport.", StringComparison.Ordinal)
            || commandId.StartsWith("information.time", StringComparison.Ordinal)
            || commandId is CommandIds.ViewNowPlaying or CommandIds.AddBookmark
                or CommandIds.AddNamedBookmark or CommandIds.PreviousBookmark
                or CommandIds.NextBookmark)
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
        yield return Info("lists", "Oznajmij wielkość, bitrate i dostępne parametry elementu", "Strzałka w lewo", "lista multimediów lub wyniki wyszukiwania");
        yield return Info("lists", "Poprzedni lub następny widok", "Alt+strzałka w lewo lub w prawo", "lista multimediów");

        yield return Info("collections", "Utwórz playlistę", "Insert", "lista playlist");
        yield return Info("collections", "Zmień nazwę playlisty", "F2", "lista playlist");

        yield return Info("radio", "Dodaj własną stację", "Insert", "Biblioteka radia internetowego");
        yield return Info("radio", "Edytuj nazwę i adres strumienia", "F2", "lista stacji radia internetowego");
        yield return Info("radio", "Zaznacz stacje do przeniesienia", "Ctrl+X", "Ulubione radia internetowego");
        yield return Info("radio", "Przenieś zaznaczone stacje przed bieżącą", "Ctrl+V", "Ulubione radia internetowego");
        yield return Info("radio", "Cofnij lub przewiń w buforze transmisji", "Strzałka w lewo lub w prawo", "odtwarzacz radia");
        yield return Info("radio", "Wróć do transmisji na żywo", "End", "odtwarzacz radia");
        yield return Info("radio", "Rozpocznij lub zakończ nagrywanie", "Ctrl+Alt+R", "odtwarzacz radia");
        yield return Info("collections", "Pokaż presety aktywnej sesji", "Ctrl+Alt+P", "Pliki lokalne lub Radio; skrót zarezerwowany także dla przyszłych usług");
        yield return Info("collections", "Utwórz lub przypisz preset aktywnej sesji", "Ctrl+Alt+Shift+P", "Plik, folder Biblioteki lub stacja radiowa");
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
