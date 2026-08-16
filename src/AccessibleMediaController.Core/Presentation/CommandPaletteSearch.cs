using System.Globalization;
using System.Text;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Core.Presentation;

public sealed record CommandPaletteEntry(
    string CommandId,
    string DisplayName,
    string? LocalShortcut,
    string? PrefixShortcut)
{
    public string Label
    {
        get
        {
            var parts = new List<string> { DisplayName };
            if (!string.IsNullOrWhiteSpace(LocalShortcut)) parts.Add(LocalShortcut);
            if (!string.IsNullOrWhiteSpace(PrefixShortcut)
                && !string.Equals(LocalShortcut, PrefixShortcut, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"prefiks {PrefixShortcut}");
            }
            return string.Join(", ", parts);
        }
    }

    public override string ToString() => Label;
}

public static class CommandPaletteSearch
{
    public static IReadOnlyList<CommandPaletteEntry> CreateEntries(KeyboardProfile profile)
    {
        var shortcuts = profile.Bindings
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group
                    .Select(pair => pair.Key)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)),
                StringComparer.Ordinal);

        return CommandCatalog.GetAllCommandIds()
            .Where(commandId => commandId != CommandIds.CommandPalette)
            .Select(commandId => new CommandPaletteEntry(
                commandId,
                CommandCatalog.GetDisplayName(commandId),
                GetLocalShortcut(commandId),
                shortcuts.GetValueOrDefault(commandId)))
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string ContinueOrRestartListQuery(
        IReadOnlyList<CommandPaletteEntry> entries,
        string currentQuery,
        string input)
    {
        if (string.IsNullOrEmpty(input)) return currentQuery;

        var continued = currentQuery + input;
        if (Filter(entries, continued).Count > 0) return continued;
        if (Filter(entries, input).Count > 0) return input;
        return string.Empty;
    }

    private static string? GetLocalShortcut(string commandId)
    {
        const string sessionSlotPrefix = "session.slot.";
        if (commandId.StartsWith(sessionSlotPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(sessionSlotPrefix.Length), out var slot)
            && slot is >= 1 and <= 9)
        {
            return $"Ctrl+{slot}";
        }

        return commandId switch
        {
            CommandIds.PlayPause => "Space",
            CommandIds.ActivateSelected => "Ctrl+Enter",
            CommandIds.SessionList => "Ctrl+0",
            CommandIds.SessionPrevious => "Ctrl+PageUp",
            CommandIds.SessionNext => "Ctrl+PageDown",
            CommandIds.ViewFavorites => "Ctrl+U",
            CommandIds.ToggleFavorite => "Ctrl+Shift+U",
            CommandIds.ViewPlaylists => "Ctrl+P",
            CommandIds.ManagePlaylists => "Ctrl+Shift+P",
            CommandIds.ViewLibrary => "Ctrl+L",
            CommandIds.ToggleLibrary => "Ctrl+Shift+L",
            CommandIds.ViewQueue => "Ctrl+Q",
            CommandIds.AddQueue => "Shift+Enter",
            CommandIds.TogglePlayNext => "Ctrl+Shift+Enter",
            CommandIds.ViewAlbums => "Ctrl+Shift+A",
            CommandIds.FilterCurrent => "Ctrl+K",
            CommandIds.SearchCurrent => "Ctrl+F",
            CommandIds.SearchAll => "Ctrl+Shift+F",
            CommandIds.ItemInformation => "Alt+Enter",
            CommandIds.OpenOfficialApp => "Ctrl+Shift+O",
            CommandIds.Help => "F1",
            _ => null
        };
    }

    public static IReadOnlyList<CommandPaletteEntry> Filter(
        IEnumerable<CommandPaletteEntry> entries,
        string query)
    {
        var tokens = FoldForSearch(query)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return entries.ToArray();

        return entries
            .Where(entry =>
            {
                var searchableLabel = FoldForSearch(entry.Label);
                return tokens.All(token => searchableLabel.Contains(token, StringComparison.Ordinal));
            })
            .ToArray();
    }

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
