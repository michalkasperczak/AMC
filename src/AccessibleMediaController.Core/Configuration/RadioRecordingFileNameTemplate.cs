using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AccessibleMediaController.Core.Configuration;

public static class RadioRecordingFileNameTemplate
{
    public const string DefaultTemplate = "{stacja} - {data} {czas}";

    private const int MaximumTemplateLength = 240;
    private const int MaximumFileBaseNameLength = 180;
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly Regex TokenPattern = new(
        @"\{[^{}]+\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "{stacja}",
        "{data}",
        "{data-polska}",
        "{data-zwarta}",
        "{rok}",
        "{miesiąc}",
        "{dzień}",
        "{dzień-tygodnia}",
        "{czas}",
        "{godzina}",
        "{minuta}",
        "{część}"
    };
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string NormalizeOrDefault(string? template) =>
        TryValidate(template, out _)
            ? template!.Trim()
            : DefaultTemplate;

    public static bool TryValidate(string? template, out string error)
    {
        var value = template?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            error = "Wpisz nazwę pliku albo wybierz gotowy szablon";
            return false;
        }
        if (value.Length > MaximumTemplateLength)
        {
            error = $"Szablon nazwy może mieć najwyżej {MaximumTemplateLength} znaków";
            return false;
        }

        foreach (Match match in TokenPattern.Matches(value))
        {
            if (SupportedTokens.Contains(match.Value)) continue;
            error = $"Nieznany token w nazwie pliku: {match.Value}";
            return false;
        }

        var withoutTokens = TokenPattern.Replace(value, string.Empty);
        if (withoutTokens.Contains('{') || withoutTokens.Contains('}'))
        {
            error = "Token w nazwie pliku ma niepełny nawias klamrowy";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static string Expand(
        string? template,
        string? stationName,
        DateTime occurrenceLocal,
        int partNumber = 1)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{stacja}"] = string.IsNullOrWhiteSpace(stationName) ? "Radio" : stationName.Trim(),
            ["{data}"] = occurrenceLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["{data-polska}"] = occurrenceLocal.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            ["{data-zwarta}"] = occurrenceLocal.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            ["{rok}"] = occurrenceLocal.ToString("yyyy", CultureInfo.InvariantCulture),
            ["{miesiąc}"] = occurrenceLocal.ToString("MM", CultureInfo.InvariantCulture),
            ["{dzień}"] = occurrenceLocal.ToString("dd", CultureInfo.InvariantCulture),
            ["{dzień-tygodnia}"] = occurrenceLocal.ToString("dddd", PolishCulture),
            ["{czas}"] = occurrenceLocal.ToString("HH-mm", CultureInfo.InvariantCulture),
            ["{godzina}"] = occurrenceLocal.ToString("HH", CultureInfo.InvariantCulture),
            ["{minuta}"] = occurrenceLocal.ToString("mm", CultureInfo.InvariantCulture),
            ["{część}"] = Math.Max(1, partNumber).ToString("00", CultureInfo.InvariantCulture)
        };
        var normalized = NormalizeOrDefault(template);
        var expanded = TokenPattern.Replace(
            normalized,
            match => values.TryGetValue(match.Value, out var replacement) ? replacement : string.Empty);
        return SanitizeBaseName(expanded);
    }

    public static string SanitizeBaseName(string? value)
    {
        var source = value?.Normalize(NormalizationForm.FormC) ?? string.Empty;
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            builder.Append(character < 32 || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
                ? '_'
                : character);
        }

        var result = builder.ToString().Trim().TrimEnd('.', ' ');
        if (result.Length > MaximumFileBaseNameLength)
            result = result[..MaximumFileBaseNameLength].TrimEnd('.', ' ');
        if (result.Length == 0) result = "Radio";

        var firstSegment = result.Split('.', 2)[0].TrimEnd(' ');
        return ReservedWindowsNames.Contains(firstSegment) ? $"_{result}" : result;
    }
}
