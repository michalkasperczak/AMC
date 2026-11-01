using System.Globalization;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Presentation;

public static class AudioParametersFormatter
{
    public static string Format(MediaItem item, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        culture ??= CultureInfo.CurrentCulture;

        var parts = new List<string>(2);
        if (item.BitrateKbps is int bitrateKbps)
        {
            parts.Add(item.IsBitrateEstimated
                ? $"około {bitrateKbps} kb/s"
                : $"{bitrateKbps} kb/s");
        }

        if (item.SampleRateHz is int sampleRateHz && sampleRateHz > 0)
        {
            var sampleRateKHz = sampleRateHz / 1000d;
            parts.Add($"{sampleRateKHz.ToString("0.##", culture)} kHz");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "brak danych audio";
    }

    public static string FormatCompact(MediaItem item, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        culture ??= CultureInfo.CurrentCulture;

        var parts = new List<string>(2);
        if (item.BitrateKbps is int bitrateKbps)
        {
            parts.Add($"{bitrateKbps} kb/s");
        }

        if (item.SampleRateHz is int sampleRateHz && sampleRateHz > 0)
        {
            var sampleRateKHz = sampleRateHz / 1000d;
            parts.Add($"{sampleRateKHz.ToString("0.##", culture)} kHz");
        }

        return string.Join(", ", parts);
    }

    public static string FormatRadioCompact(MediaItem item, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        culture ??= CultureInfo.CurrentCulture;

        var parts = new List<string>(3);
        if (item.BitrateKbps is > 0)
        {
            parts.Add(item.IsBitrateEstimated
                ? $"około {item.BitrateKbps} kb/s"
                : $"{item.BitrateKbps} kb/s");
        }
        if (!string.IsNullOrWhiteSpace(item.Codec)) parts.Add(item.Codec.Trim());
        if (item.SampleRateHz is > 0)
        {
            parts.Add($"{(item.SampleRateHz.Value / 1000d).ToString("0.##", culture)} kHz");
        }
        return string.Join(", ", parts);
    }
}
