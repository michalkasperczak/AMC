using System.Globalization;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Builds the short, supplementary announcement used by Left Arrow on media lists.
/// The primary row label is deliberately omitted because it has just been spoken
/// during list navigation.
/// </summary>
public static class QuickMediaInformationFormatter
{
    public static string Format(
        MediaItem item,
        string? containerFormat = null,
        long? sizeBytes = null,
        bool mayRequireCloudDownload = false,
        string? unavailableFormatMessage = null,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        culture ??= CultureInfo.CurrentCulture;

        var parts = new List<string>();
        AddDistinct(parts, NormalizeFormat(containerFormat));
        AddDistinct(parts, NormalizeFormat(item.Codec));

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(unavailableFormatMessage))
        {
            parts.Add(unavailableFormatMessage.Trim());
        }

        var audio = AudioParametersFormatter.FormatCompact(item, culture);
        if (!string.IsNullOrWhiteSpace(audio)) parts.Add(audio);
        if (sizeBytes is >= 0) parts.Add(FormatFileSize(sizeBytes.Value, culture));
        if (item.Duration > TimeSpan.Zero) parts.Add(CommandRouter.FormatTime(item.Duration));

        AddDistinct(parts, item.Artist);
        AddDistinct(parts, item.Country);
        AddDistinct(parts, item.Language);
        if (mayRequireCloudDownload) parts.Add("plik w chmurze, pobierany przy odtwarzaniu");

        return parts.Count == 0
            ? "Brak zapisanych informacji uzupełniających"
            : string.Join(", ", parts);
    }

    private static string? NormalizeFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().TrimStart('.').ToUpperInvariant();
    }

    private static void AddDistinct(ICollection<string> parts, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || parts.Any(existing => string.Equals(existing, value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        parts.Add(value.Trim());
    }

    private static string FormatFileSize(long bytes, CultureInfo culture)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value.ToString(unit == 0 ? "0" : "0.##", culture)} {units[unit]}";
    }
}
