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

    /// <summary>
    /// Składa wybór pól dla interfejsu wxPython. Kolejność pochodzi z listy
    /// pól, więc lewa strzałka może zacząć od czasu bez zmiany starego AMC.
    /// Nieznane pola są pomijane; do mowy trafiają tylko etykiety użytkowe.
    /// </summary>
    public static string FormatSelected(
        MediaItem item,
        string? containerFormat,
        long? sizeBytes,
        bool mayRequireCloudDownload,
        IReadOnlyList<string> fields,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(fields);
        culture ??= CultureInfo.CurrentCulture;
        if (fields.Count == 0) return string.Empty;

        var parts = new List<string>();
        foreach (var field in fields)
        {
            switch (field)
            {
                case "duration" when item.Duration > TimeSpan.Zero:
                    AddDistinct(parts, CommandRouter.FormatTime(item.Duration));
                    break;
                case "bitrate" when item.BitrateKbps is > 0:
                    AddDistinct(parts, $"{item.BitrateKbps} kb/s");
                    break;
                case "size" when sizeBytes is >= 0:
                    AddDistinct(parts, FormatFileSize(sizeBytes.Value, culture));
                    break;
                case "format":
                    AddDistinct(parts, NormalizeFormat(containerFormat));
                    AddDistinct(parts, NormalizeFormat(item.Codec));
                    break;
                case "sampleRate" when item.SampleRateHz is > 0:
                    AddDistinct(parts, $"{(item.SampleRateHz.Value / 1000d).ToString("0.##", culture)} kHz");
                    break;
                case "artist": AddDistinct(parts, item.Artist); break;
                case "country": AddDistinct(parts, item.Country); break;
                case "language": AddDistinct(parts, item.Language); break;
                case "cloud" when mayRequireCloudDownload:
                    AddDistinct(parts, "plik w chmurze, pobierany przy odtwarzaniu");
                    break;
            }
        }
        return string.Join(", ", parts);
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
