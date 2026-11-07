using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// A single AMC playback request. The protocol carries only the local
/// loopback capability, never an upstream playback URL or credentials.
/// </summary>
internal sealed record LiteTidalNativePlayRequest(
    string TrackId,
    string Title,
    Uri ProxyUri,
    TimeSpan Duration,
    int? BitrateKbps,
    int? SampleRateHz,
    string? SourceQualityLabel)
{
    internal const string Operation = "tidal.nativePlay";
    internal const string BitrateOperation = "tidal.nativeBitrate";
    internal const string DisabledMessage =
        "Odtwarzanie TIDAL w tej wersji AMC jest niedostępne.";

    private static readonly Regex TrackIdPattern = new(
        @"\A[0-9]{1,18}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ProxyUrlPattern = new(
        @"\Ahttp://127\.0\.0\.1:[1-9][0-9]{0,4}/audio/[A-Za-z0-9_-]{32}(?:\.mpd)?\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal string MediaItemId => "tidal:" + TrackId;
    internal string StableSource => "tidal:track:" + TrackId;

    internal static bool IsEnabledForProfile(string? releaseProfile) =>
        releaseProfile is "test" or "public";

    internal static LiteTidalNativePlayRequest Parse(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            throw new LiteRequestException("Nieprawidłowe polecenie odtwarzania TIDAL.");

        foreach (var property in args.EnumerateObject())
        {
            if (property.Name is not ("proxyUrl" or "id" or "title" or "durationSeconds" or "bitrateKbps"
                or "sampleRateHz" or "sourceQualityLabel"))
                throw new LiteRequestException("Polecenie odtwarzania TIDAL zawiera nieznane dane.");
        }

        var id = LiteArgs.RequireText(args, "id");
        if (!TrackIdPattern.IsMatch(id))
            throw new LiteRequestException("Identyfikator utworu TIDAL jest nieprawidłowy.");

        var title = LiteArgs.RequireText(args, "title").Trim();
        if (title.Length > 256 || title.Any(char.IsControl))
            throw new LiteRequestException("Nazwa utworu TIDAL jest nieprawidłowa.");

        var proxyUrl = LiteArgs.RequireText(args, "proxyUrl");
        if (!ProxyUrlPattern.IsMatch(proxyUrl)
            || !Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri)
            || proxyUri.Port is < 1 or > 65535
            || proxyUri.Host != "127.0.0.1"
            || proxyUri.UserInfo.Length != 0
            || proxyUri.Query.Length != 0
            || proxyUri.Fragment.Length != 0)
        {
            throw new LiteRequestException("Lokalny adres dźwięku TIDAL jest nieprawidłowy.");
        }

        if (!args.TryGetProperty("durationSeconds", out var durationProperty)
            || durationProperty.ValueKind != JsonValueKind.Number
            || !durationProperty.TryGetDouble(out var durationSeconds)
            || !double.IsFinite(durationSeconds)
            || durationSeconds <= 0
            || durationSeconds > 86_400)
        {
            throw new LiteRequestException("Czas utworu TIDAL jest nieprawidłowy.");
        }

        int? bitrateKbps = null;
        if (args.TryGetProperty("bitrateKbps", out var bitrateProperty))
        {
            if (bitrateProperty.ValueKind != JsonValueKind.Number
                || !bitrateProperty.TryGetInt32(out var value)
                || value is < 16 or > 30_000)
                throw new LiteRequestException("Przepływność TIDAL jest nieprawidłowa.");
            bitrateKbps = value;
        }

        int? sampleRateHz = null;
        if (args.TryGetProperty("sampleRateHz", out var sampleRateProperty))
        {
            if (sampleRateProperty.ValueKind != JsonValueKind.Number
                || !sampleRateProperty.TryGetInt32(out var value)
                || value is < 8_000 or > 384_000)
                throw new LiteRequestException("Częstotliwość źródła TIDAL jest nieprawidłowa.");
            sampleRateHz = value;
        }
        string? sourceQualityLabel = null;
        if (args.TryGetProperty("sourceQualityLabel", out var qualityProperty))
        {
            if (qualityProperty.ValueKind != JsonValueKind.String)
                throw new LiteRequestException("Opis jakości TIDAL jest nieprawidłowy.");
            sourceQualityLabel = qualityProperty.GetString()?.Trim();
            if (string.IsNullOrEmpty(sourceQualityLabel) || sourceQualityLabel.Length > 200
                || sourceQualityLabel.Any(char.IsControl))
                throw new LiteRequestException("Opis jakości TIDAL jest nieprawidłowy.");
        }
        return new(id, title, proxyUri, TimeSpan.FromSeconds(durationSeconds), bitrateKbps,
            sampleRateHz, sourceQualityLabel);
    }

    internal static string FormatPlaybackText(
        string title, string sourceQualityLabel, int? bitrateKbps, int? pcmSampleRateHz)
    {
        var parts = new List<string> { title, "źródło " + sourceQualityLabel.TrimEnd('.') };
        if (bitrateKbps is > 0) parts.Add($"{bitrateKbps} kb/s");
        if (pcmSampleRateHz is > 0)
        {
            var rate = (pcmSampleRateHz.Value / 1000d).ToString("0.###", CultureInfo.GetCultureInfo("pl-PL"));
            parts.Add($"PCM w silniku {rate} kHz");
        }
        return string.Join(", ", parts);
    }
}
