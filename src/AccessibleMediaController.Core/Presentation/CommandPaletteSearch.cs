using System.Globalization;
using System.Text;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
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
    public static IReadOnlyList<CommandPaletteEntry> CreateEntries(
        KeyboardProfile profile,
        AppSettings settings)
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
                GetDisplayName(commandId, settings),
                GetLocalShortcut(commandId),
                shortcuts.GetValueOrDefault(commandId)))
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string GetDisplayName(string commandId, AppSettings settings)
    {
        return commandId switch
        {
            CommandIds.SettingsToggleMessages => settings.Messages.Enabled
                ? "Komunikaty dostępności: włączone. Enter: wyłącz"
                : "Komunikaty dostępności: wyłączone. Enter: włącz",
            CommandIds.SettingsToggleDetailedHints => settings.Messages.DetailedHints
                ? "Szczegółowe podpowiedzi klawiatury: włączone. Enter: wyłącz"
                : "Szczegółowe podpowiedzi klawiatury: wyłączone. Enter: włącz",
            CommandIds.SettingsToggleSeekMessages => settings.Messages.SeekMessages
                ? "Odczyt pozycji po przewijaniu: włączony. Enter: wyłącz"
                : "Odczyt pozycji po przewijaniu: wyłączony. Enter: włącz",
            _ => CommandCatalog.GetDisplayName(commandId)
        };
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
        if (CommandIds.TryParseSeekPercent(commandId, out var percent))
        {
            return $"{percent / 10} (odtwarzacz)";
        }

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
            CommandIds.SeekBackward10 => "Left (odtwarzacz)",
            CommandIds.SeekForward10 => "Right (odtwarzacz)",
            CommandIds.SeekBackward30 => "Shift+Left (odtwarzacz)",
            CommandIds.SeekForward30 => "Shift+Right (odtwarzacz)",
            CommandIds.SeekBackward60 => "Ctrl+Left (odtwarzacz)",
            CommandIds.SeekForward60 => "Ctrl+Right (odtwarzacz)",
            CommandIds.VolumeUp5 => "Up (odtwarzacz)",
            CommandIds.VolumeDown5 => "Down (odtwarzacz)",
            CommandIds.VolumeUp1 => "Shift+Up (odtwarzacz)",
            CommandIds.VolumeDown1 => "Shift+Down (odtwarzacz)",
            CommandIds.TrackStart => "Home (odtwarzacz)",
            CommandIds.TrackEnd => "End (odtwarzacz)",
            CommandIds.TimeElapsed => "Ctrl+Shift+E",
            CommandIds.TimeRemaining => "Ctrl+Shift+R",
            CommandIds.TimeTotal => "Ctrl+Shift+T",
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
            CommandIds.ViewNowPlaying => "F6",
            CommandIds.FilterCurrent => "Ctrl+K",
            CommandIds.SearchCurrent => "Ctrl+F",
            CommandIds.SearchAll => "Ctrl+Shift+F",
            CommandIds.ItemInformation => "Alt+Enter",
            CommandIds.Help => "F1",
            CommandIds.OpenLocalFiles => "Ctrl+O",
            CommandIds.OpenLocalFolder => "Ctrl+Shift+O",
            CommandIds.SettingsGeneral => "Ctrl+,",
            CommandIds.SettingsToggleSeekMessages => "Ctrl+Shift+G",
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
                var searchableText = entry.CommandId.StartsWith("settings.", StringComparison.Ordinal)
                    ? $"{entry.Label} ustawienia"
                    : entry.Label;
                var searchableLabel = FoldForSearch(searchableText);
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
