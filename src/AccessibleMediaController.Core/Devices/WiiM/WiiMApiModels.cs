using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

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
    public int RawMode { get; init; }
    public string? ContentUri { get; init; }
}

public sealed record WiiMTrackInformation(
    string Title,
    string Artist,
    string Album,
    int? SampleRateHz,
    int? BitDepth)
{
    public string Subtitle { get; init; } = string.Empty;
    public int? BitrateKbps { get; init; }
}

public sealed record WiiMPresetInformation(
    int Number,
    string Name,
    string Source,
    string? Uri);

public enum WiiMGroupRole
{
    Unknown,
    Standalone,
    StandaloneOrLeader,
    Leader,
    Follower
}

public sealed record WiiMGroupMemberInformation(
    string Name,
    string Address,
    int? Volume,
    bool? Muted);

public sealed record WiiMGroupInformation(
    WiiMGroupRole Role,
    string GroupName,
    string LeaderAddress,
    IReadOnlyList<WiiMGroupMemberInformation> Members,
    bool MemberListAvailable)
{
    public static WiiMGroupInformation Unknown { get; } =
        new(WiiMGroupRole.Unknown, string.Empty, string.Empty, [], false);
}

public sealed record WiiMDeviceSnapshot(
    WiiMDeviceInformation Device,
    WiiMPlaybackInformation Playback,
    WiiMTrackInformation Track,
    IReadOnlyList<WiiMPresetInformation> Presets)
{
    public WiiMGroupInformation Group { get; init; } = WiiMGroupInformation.Unknown;

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

public sealed record WiiMUpnpPlaybackInformation(
    string? ContentUri,
    WiiMTrackInformation Track);

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
        var rawMode = Integer(root, "mode") ?? 0;
        return new WiiMPlaybackInformation(
            FriendlyState(Text(root, "status")),
            FriendlySource(rawMode, Text(root, "mode")),
            Math.Clamp(Integer(root, "vol") ?? 0, 0, 100),
            Integer(root, "mute") == 1,
            TimeSpan.FromMilliseconds(Math.Max(0, Long(root, "curpos") ?? 0)),
            TimeSpan.FromMilliseconds(Math.Max(0, Long(root, "totlen") ?? 0)))
        {
            LoopMode = Integer(root, "loop") ?? 4,
            EqualizerPresetNumber = Math.Max(0, Integer(root, "eq") ?? 0),
            RawMode = rawMode,
            ContentUri = NormalizeUri(Text(root, "uri", "iuri", "media_content_id", "stream_url"))
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
            CleanTrackText(Text(root, "title")),
            CleanTrackText(Text(root, "artist")),
            CleanTrackText(Text(root, "album")),
            PositiveInteger(root, "sampleRate", "sample_rate", "rate_hz"),
            PositiveInteger(root, "bitDepth", "bit_depth", "format_s"))
        {
            Subtitle = CleanTrackText(Text(root, "subtitle")),
            BitrateKbps = NormalizeBitrateKbps(PositiveInteger(root, "bitRate", "bitrate"))
        };
    }

    public static WiiMTrackInformation ParsePlayerTrackInformation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return EmptyTrack();
        using var document = ParseObject(json, "stan odtwarzania");
        var root = document.RootElement;
        return new WiiMTrackInformation(
            DecodePlayerTrackText(Text(root, "Title")),
            DecodePlayerTrackText(Text(root, "Artist")),
            DecodePlayerTrackText(Text(root, "Album")),
            null,
            null);
    }

    public static WiiMTrackInformation MergeTrackInformation(
        WiiMTrackInformation metadata,
        WiiMTrackInformation playerStatus)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(playerStatus);
        return new WiiMTrackInformation(
            PreferTrackText(metadata.Title, playerStatus.Title),
            PreferTrackText(metadata.Artist, playerStatus.Artist),
            PreferTrackText(metadata.Album, playerStatus.Album),
            metadata.SampleRateHz ?? playerStatus.SampleRateHz,
            metadata.BitDepth ?? playerStatus.BitDepth)
        {
            Subtitle = PreferTrackText(metadata.Subtitle, playerStatus.Subtitle),
            BitrateKbps = metadata.BitrateKbps ?? playerStatus.BitrateKbps
        };
    }

    public static WiiMUpnpPlaybackInformation ParseUpnpPlaybackInformation(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return new WiiMUpnpPlaybackInformation(null, EmptyTrack());
        try
        {
            var outer = ParseXml(xml);
            var contentUri = NormalizeUri(outer.Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("TrackURI", StringComparison.OrdinalIgnoreCase))
                ?.Value ?? string.Empty);
            var metadataText = outer.Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("TrackMetaData", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (string.IsNullOrWhiteSpace(metadataText))
                return new WiiMUpnpPlaybackInformation(contentUri, EmptyTrack());

            var metadata = ParseXml(metadataText);
            string Value(string name) => CleanTrackText(metadata.Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?.Value ?? string.Empty);
            int? Number(string name) => int.TryParse(
                Value(name),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) && value > 0
                    ? value
                    : null;
            var track = new WiiMTrackInformation(
                Value("title"),
                PreferTrackText(Value("artist"), Value("creator")),
                Value("album"),
                Number("rate_hz"),
                Number("format_s"))
            {
                BitrateKbps = NormalizeBitrateKbps(Number("bitrate"))
            };
            return new WiiMUpnpPlaybackInformation(contentUri, track);
        }
        catch (XmlException exception)
        {
            throw new FormatException("Nieprawidłowe metadane UPnP odtwarzacza.", exception);
        }
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

    public static WiiMGroupInformation ParseGroupInformation(
        string? deviceJson,
        string? groupMembersJson)
    {
        int? groupFlag = null;
        var groupName = string.Empty;
        var leaderAddress = string.Empty;
        if (!string.IsNullOrWhiteSpace(deviceJson))
        {
            try
            {
                using var status = ParseObject(deviceJson, "odpowiedź o urządzeniu");
                groupFlag = Integer(status.RootElement, "group");
                groupName = CleanTrackText(Text(status.RootElement, "GroupName", "group_name"));
                var rawLeaderAddress = Text(status.RootElement, "master_ip", "masterIp");
                if (WiiMAddressPolicy.TryNormalize(rawLeaderAddress, out var normalizedLeaderAddress))
                    leaderAddress = normalizedLeaderAddress;
            }
            catch (FormatException)
            {
                // Informacja o grupie jest opcjonalna. Błąd głównej odpowiedzi
                // urządzenia obsługuje osobno parser obowiązkowych danych.
            }
        }

        var members = Array.Empty<WiiMGroupMemberInformation>();
        var memberListAvailable = false;
        if (!string.IsNullOrWhiteSpace(groupMembersJson))
        {
            try
            {
                using var document = ParseObject(groupMembersJson, "lista urządzeń grupy");
                if (TryProperty(document.RootElement, "slave_list", out var list)
                    && list.ValueKind == JsonValueKind.Array)
                {
                    memberListAvailable = true;
                    members = list.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.Object)
                        .Select(ParseGroupMember)
                        .Where(member => member is not null)
                        .Select(member => member!)
                        .GroupBy(member => string.IsNullOrWhiteSpace(member.Address)
                                ? member.Name
                                : member.Address,
                            StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .Take(32)
                        .ToArray();
                }
            }
            catch (FormatException)
            {
                // Starszy firmware może zwrócić tekst „unknown command” z
                // kodem HTTP 200. Brak tej listy nie unieważnia całego odczytu.
            }
        }

        var role = groupFlag switch
        {
            1 => WiiMGroupRole.Follower,
            0 when memberListAvailable && members.Length > 0 => WiiMGroupRole.Leader,
            0 when memberListAvailable => WiiMGroupRole.Standalone,
            0 => WiiMGroupRole.StandaloneOrLeader,
            _ when memberListAvailable && members.Length > 0 => WiiMGroupRole.Leader,
            _ => WiiMGroupRole.Unknown
        };
        return new WiiMGroupInformation(
            role,
            groupName,
            leaderAddress,
            members,
            memberListAvailable);
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

    private static int? PositiveInteger(JsonElement element, params string[] names) =>
        Integer(element, names) is > 0 and var value ? value : null;

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
        >= 10 and <= 30 => "odtwarzacz WiiM",
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

    private static string DecodePlayerTrackText(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value.Length % 2 == 0 && value.All(Uri.IsHexDigit))
        {
            try
            {
                var bytes = Convert.FromHexString(value);
                value = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
            {
                // Some firmware returns plain text which happens to resemble hex.
            }
        }
        return CleanTrackText(WebUtility.HtmlDecode(value));
    }

    private static string CleanTrackText(string value)
    {
        var normalized = WebUtility.HtmlDecode(value).Trim();
        return normalized.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("unknow", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized;
    }

    private static string PreferTrackText(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    private static WiiMGroupMemberInformation? ParseGroupMember(JsonElement item)
    {
        var rawAddress = Text(item, "ip", "address");
        var address = WiiMAddressPolicy.TryNormalize(rawAddress, out var normalizedAddress)
            ? normalizedAddress
            : string.Empty;
        var name = CleanTrackText(Text(item, "name", "DeviceName", "ssid"));
        if (string.IsNullOrWhiteSpace(name)) name = address;
        if (string.IsNullOrWhiteSpace(name)) return null;
        var rawVolume = Integer(item, "volume", "vol");
        var volume = rawVolume is >= 0 and <= 100 ? rawVolume : null;
        var rawMute = Integer(item, "mute");
        bool? muted = rawMute switch
        {
            0 => false,
            1 => true,
            _ => null
        };
        return new WiiMGroupMemberInformation(name, address, volume, muted);
    }

    private static XDocument ParseXml(string xml)
    {
        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1024 * 1024
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static int? NormalizeBitrateKbps(int? bitrate)
    {
        if (bitrate is null) return null;
        return bitrate > 10_000
            ? Math.Max(1, (int)Math.Round(bitrate.Value / 1000d))
            : bitrate;
    }

    private static string? NormalizeUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;
}

public static class WiiMGroupPresentation
{
    public static string RoleLabel(WiiMGroupRole role) => role switch
    {
        WiiMGroupRole.Standalone => "urządzenie samodzielne",
        WiiMGroupRole.StandaloneOrLeader => "urządzenie samodzielne albo główne grupy",
        WiiMGroupRole.Leader => "urządzenie główne grupy",
        WiiMGroupRole.Follower => "urządzenie podrzędne w grupie",
        _ => "stan grupy nieudostępniony"
    };

    public static string Summary(WiiMGroupInformation group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var parts = new List<string> { RoleLabel(group.Role) };
        if (!string.IsNullOrWhiteSpace(group.GroupName)) parts.Add($"grupa {group.GroupName}");
        if (group.Role == WiiMGroupRole.Leader && group.MemberListAvailable)
        {
            parts.Add(group.Members.Count switch
            {
                0 => "bez urządzeń podrzędnych",
                1 => "jedno urządzenie podrzędne",
                _ => $"urządzenia podrzędne: {group.Members.Count}"
            });
        }
        return string.Join(", ", parts);
    }
}

public static class WiiMPresetStateResolver
{
    public static int? ResolveCurrentPreset(
        WiiMDeviceSnapshot snapshot,
        int lastActivatedPresetNumber,
        bool trustRememberedNetworkPreset = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var occupied = snapshot.Presets
            .Where(preset => preset.Number is >= 1 and <= 12)
            .ToArray();

        if (snapshot.Playback.ContentUri is { Length: > 0 } currentUri)
        {
            var exact = occupied.FirstOrDefault(preset =>
                preset.Uri is { Length: > 0 }
                && UriEquals(preset.Uri, currentUri));
            if (exact is not null) return exact.Number;
        }

        if (lastActivatedPresetNumber is < 1 or > 12
            || occupied.All(preset => preset.Number != lastActivatedPresetNumber))
        {
            return null;
        }

        // The LinkPlay API does not expose a current-preset number. Modes
        // 10-30 are device-managed network playback on current WiiM firmware.
        // Some service presets are reported as a Connect mode (31-39), though,
        // and omit the URI needed for an exact comparison. An explicit user
        // navigation command may then use AMC's persisted preset as its anchor.
        // Physical inputs, AirPlay, DLNA and grouped playback must never inherit
        // a remembered preset because that would silently navigate from the
        // wrong source.
        return snapshot.Playback.RawMode is >= 10 and <= 30
            || trustRememberedNetworkPreset
               && snapshot.Playback.RawMode is >= 31 and <= 39
            ? lastActivatedPresetNumber
            : null;
    }

    private static bool UriEquals(string first, string second)
    {
        if (!Uri.TryCreate(first, UriKind.Absolute, out var firstUri)
            || !Uri.TryCreate(second, UriKind.Absolute, out var secondUri))
        {
            return false;
        }

        return string.Equals(
            firstUri.AbsoluteUri,
            secondUri.AbsoluteUri,
            StringComparison.Ordinal);
    }
}
