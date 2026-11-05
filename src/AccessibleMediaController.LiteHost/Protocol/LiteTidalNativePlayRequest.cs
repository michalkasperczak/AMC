using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// A single AMC Test playback request. The protocol carries only the local
/// loopback capability, never an upstream playback URL or credentials.
/// </summary>
internal sealed record LiteTidalNativePlayRequest(
    string TrackId,
    string Title,
    Uri ProxyUri,
    TimeSpan Duration)
{
    internal const string Operation = "tidal.nativePlay";
    internal const string DisabledMessage =
        "Odtwarzanie TIDAL w AMC jest dostępne tylko w AMC Test.";

    private static readonly Regex TrackIdPattern = new(
        @"\A[0-9]{1,18}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ProxyUrlPattern = new(
        @"\Ahttp://127\.0\.0\.1:[1-9][0-9]{0,4}/audio/[A-Za-z0-9_-]{32}(?:\.mpd)?\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal string MediaItemId => "tidal:" + TrackId;
    internal string StableSource => "tidal:track:" + TrackId;

    internal static bool IsEnabledForProfile(string? releaseProfile) =>
        string.Equals(releaseProfile, "test", StringComparison.Ordinal);

    internal static LiteTidalNativePlayRequest Parse(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
            throw new LiteRequestException("Nieprawidłowe polecenie odtwarzania TIDAL.");

        foreach (var property in args.EnumerateObject())
        {
            if (property.Name is not ("proxyUrl" or "id" or "title" or "durationSeconds"))
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

        return new(id, title, proxyUri, TimeSpan.FromSeconds(durationSeconds));
    }
}
