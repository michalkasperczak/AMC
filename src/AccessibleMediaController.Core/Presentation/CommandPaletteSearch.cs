using System.Globalization;
using System.Text;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Core.Presentation;

public sealed record CommandPaletteEntry(
    string CommandId,
    string DisplayName,
    string? PrefixShortcut)
{
    public string Label => PrefixShortcut is null
        ? DisplayName
        : $"{DisplayName}, prefiks {PrefixShortcut}";

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
                shortcuts.GetValueOrDefault(commandId)))
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
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
