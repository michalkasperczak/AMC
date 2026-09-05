using System.Globalization;
using System.Net;
using System.Text.Json;

namespace AccessibleMediaController.Core.Devices.WiiM;

public sealed record WiiMDeviceInformation(
    string Address,
    string Id,
    string Name,
    string Model,
    string Firmware,
    int PresetButtonCount);

public sealed record WiiMPlaybackInformation(
    string State,
    string Source,
    int Volume,
    bool Muted,
    TimeSpan Position,
    TimeSpan Duration)
{
    public int LoopMode { get; init; } = 4;
    public int EqualizerPresetNumber { get; init; }
}

public sealed record WiiMTrackInformation(
    string Title,
    string Artist,
    string Album,
    int? SampleRateHz,
    int? BitDepth);

public sealed record WiiMPresetInformation(
    int Number,
    string Name,
    string Source,
    string? Uri);

public sealed record WiiMDeviceSnapshot(
    WiiMDeviceInformation Device,
    WiiMPlaybackInformation Playback,
    WiiMTrackInformation Track,
    IReadOnlyList<WiiMPresetInformation> Presets)
{
    public string PlaybackSummary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Playback.Source)) parts.Add(Playback.Source);
            if (!string.IsNullOrWhiteSpace(Track.Title))
            {
                parts.Add(string.IsNullOrWhiteSpace(Track.Artist)
                    ? Track.Title
                    : $"{Track.Title}, {Track.Artist}");
            }
            parts.Add(Playback.State);
            parts.Add(Playback.Muted ? "wyciszone" : $"głośność {Playback.Volume}%");
            return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}

public static class WiiMAddressPolicy
{
    public static bool TryNormalize(string? value, out string address)
    {
        address = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var candidate = value.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is not ("http" or "https")) return false;
            candidate = uri.Host;
        }

        if (!IPAddress.TryParse(candidate.Trim('[', ']'), out var parsed)
            || IPAddress.IsLoopback(parsed)
            || parsed.Equals(IPAddress.Any)
            || parsed.Equals(IPAddress.IPv6Any)
            || parsed.IsIPv6Multicast)
        {
            return false;
        }

        if (parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = parsed.GetAddressBytes();
            var local = bytes[0] == 10
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 169 && bytes[1] == 254;
            if (!local) return false;
        }
        else if (!parsed.IsIPv6LinkLocal && (parsed.GetAddressBytes()[0] & 0xFE) != 0xFC)
        {
            return false;
        }

        address = parsed.ToString();
        return true;
    }

    public static Uri CreateApiUri(string address, string command)
    {
        if (!TryNormalize(address, out var normalized))
            throw new ArgumentException("Podaj lokalny adres IP urządzenia WiiM.", nameof(address));
        var host = normalized.Contains(':', StringComparison.Ordinal) ? $"[{normalized}]" : normalized;
        return new Uri($"https://{host}/httpapi.asp?command={Uri.EscapeDataString(command)}");
    }
}

public static class WiiMApiParser
{
    public static WiiMDeviceInformation ParseDeviceInformation(string json, string address)
    {
        using var document = ParseObject(json, "odpowiedź o urządzeniu");
        var root = document.RootElement;
        var id = Text(root, "uuid", "upnp_uuid", "MAC");
        var name = Text(root, "DeviceName", "ssid");
        var model = Text(root, "project", "priv_prj", "hardware");
        var firmware = Text(root, "firmware", "FW_Release_version");
        return new WiiMDeviceInformation(
            address,
            string.IsNullOrWhiteSpace(id) ? address : id,
            string.IsNullOrWhiteSpace(name) ? $"WiiM {address}" : name,
            FriendlyModel(model),
            firmware,
            Math.Clamp(Integer(root, "preset_key") ?? 12, 0, 12));
    }

    public static WiiMPlaybackInformation ParsePlaybackInformation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return EmptyPlayback();
        using var document = ParseObject(json, "stan odtwarzania");
        var root = document.RootElement;
        return new WiiMPlaybackInformation(
            FriendlyState(Text(root, "status")),
            FriendlySource(Integer(root, "mode"), Text(root, "mode")),
            Math.Clamp(Integer(root, "vol") ?? 0, 0, 100),
            Integer(root, "mute") == 1,
            TimeSpan.FromMilliseconds(Math.Max(0, Long(root, "curpos") ?? 0)),
            TimeSpan.FromMilliseconds(Math.Max(0, Long(root, "totlen") ?? 0)))
        {
            LoopMode = Integer(root, "loop") ?? 4,
            EqualizerPresetNumber = Math.Max(0, Integer(root, "eq") ?? 0)
        };
    }

    public static WiiMTrackInformation ParseTrackInformation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return EmptyTrack();
        using var document = ParseObject(json, "metadane utworu");
        var root = document.RootElement;
        if (TryProperty(root, "metaData", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
            root = metadata;
        return new WiiMTrackInformation(
            Text(root, "title"),
            Text(root, "artist"),
            Text(root, "album"),
            Integer(root, "sampleRate"),
            Integer(root, "bitDepth"));
    }

    public static IReadOnlyList<WiiMPresetInformation> ParsePresets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        using var document = ParseObject(json, "lista presetów");
        if (!TryProperty(document.RootElement, "preset_list", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return list.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new WiiMPresetInformation(
                Integer(item, "number") ?? 0,
                Text(item, "name"),
                Text(item, "source"),
                NormalizeUri(Text(item, "url"))))
            .Where(item => item.Number is >= 1 and <= 12)
            .OrderBy(item => item.Number)
            .ToArray();
    }

    public static bool ParseEqualizerEnabled(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        using var document = ParseObject(json, "stan korektora");
        return Text(document.RootElement, "EQStat").Equals("On", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> ParseEqualizerPresets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 8
            });
            if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
            return document.RootElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (JsonException exception)
        {
            throw new FormatException("Nieprawidłowa lista ustawień korektora.", exception);
        }
    }

    public static int ParseAudioOutputHardwareMode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        using var document = ParseObject(json, "informacja o wyjściu audio");
        return Integer(document.RootElement, "hardware") is >= 1 and <= 3 ? Integer(document.RootElement, "hardware")!.Value : 0;
    }

    public static WiiMPlaybackInformation EmptyPlayback() =>
        new("stan nieznany", string.Empty, 0, false, TimeSpan.Zero, TimeSpan.Zero);

    public static WiiMTrackInformation EmptyTrack() =>
        new(string.Empty, string.Empty, string.Empty, null, null);

    private static JsonDocument ParseObject(string json, string label)
    {
        try
        {
            var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 32
            });
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose();
            throw new FormatException($"Nieprawidłowa {label}.");
        }
        catch (JsonException exception)
        {
            throw new FormatException($"Nieprawidłowa {label}.", exception);
        }
    }

    private static string Text(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryProperty(element, name, out var value)) continue;
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        return string.Empty;
    }

    private static int? Integer(JsonElement element, params string[] names) =>
        int.TryParse(Text(element, names), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static long? Long(JsonElement element, params string[] names) =>
        long.TryParse(Text(element, names), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string FriendlyState(string state) => state.ToLowerInvariant() switch
    {
        "play" => "odtwarzanie",
        "pause" => "pauza",
        "loading" => "ładowanie",
        "stop" => "zatrzymane",
        _ => "stan nieznany"
    };

    private static string FriendlySource(int? mode, string original) => mode switch
    {
        1 => "AirPlay",
        2 => "DLNA",
        >= 10 and <= 19 => "odtwarzacz WiiM",
        31 => "Spotify Connect",
        32 => "TIDAL Connect",
        40 => "wejście liniowe",
        41 => "Bluetooth",
        42 => "pamięć zewnętrzna",
        43 => "wejście optyczne",
        50 => "przesyłanie ekranu",
        60 => "wiadomość głosowa",
        99 => "urządzenie w grupie",
        _ => string.IsNullOrWhiteSpace(original) || original == "0" ? string.Empty : $"źródło {original}"
    };

    private static string FriendlyModel(string model) => string.IsNullOrWhiteSpace(model)
        ? "model nierozpoznany"
        : model.Replace('_', ' ').Trim();

    private static string? NormalizeUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;
}
