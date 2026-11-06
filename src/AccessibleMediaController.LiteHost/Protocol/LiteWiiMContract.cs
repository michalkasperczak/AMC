using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Waski kontrakt zdalnego sterowania WiiM. Python przekazuje wylacznie
/// stabilne Id zapisanego urzadzenia; adres lokalny pozostaje w hoście C#.
/// </summary>
public static class LiteWiiMContract
{
    public const string DevicesOperation = "wiim.devices";
    public const string SnapshotOperation = "wiim.snapshot";
    public const string TransportOperation = "wiim.transport";
    public const string PresetsOperation = "wiim.presets";
    public const string ActivatePresetOperation = "wiim.presetActivate";
    public const string OptionsOperation = "wiim.options";
    public const string SetOptionOperation = "wiim.optionSet";
    public const string ToggleShuffleOperation = "wiim.shuffleToggle";
    public const string NavigateSourceOperation = "wiim.sourceNavigate";
    public const string NetworkStreamsOperation = "wiim.networkStreams";
    public const string PlayNetworkStreamOperation = "wiim.networkStreamPlay";
    public const string NetworkStreamEditInfoOperation = "wiim.networkStreamEditInfo";
    public const string AddNetworkStreamOperation = "wiim.networkStreamAdd";
    public const string EditNetworkStreamOperation = "wiim.networkStreamEdit";
    public const string RemoveNetworkStreamsOperation = "wiim.networkStreamRemove";
    public const string ImportNetworkStreamsOperation = "wiim.networkStreamImport";
    public const string ExportNetworkStreamsOperation = "wiim.networkStreamExport";
    public const string SortNetworkStreamsOperation = "wiim.networkStreamSort";
    public const string MoveNetworkStreamsOperation = "wiim.networkStreamMove";
    public const string PlaceNetworkStreamsOperation = "wiim.networkStreamPlace";

    public static string ReadDeviceId(JsonElement args) =>
        LiteArgs.RequireText(args, "deviceId").Trim();

    public static LiteWiiMTransportRequest ReadTransportRequest(JsonElement args)
    {
        var deviceId = ReadDeviceId(args);
        var command = LiteArgs.RequireText(args, "command").Trim();
        if (command is not (
                "toggle" or "previous" or "next"
                or "setVolume"
                or "volumeUp5" or "volumeDown5"
                or "volumeUp1" or "volumeDown1"))
        {
            throw new LiteRequestException("Nieznane polecenie sterowania WiiM.");
        }
        int? volume = null;
        if (command == "setVolume")
        {
            if (!args.TryGetProperty("volume", out var rawVolume)
                || rawVolume.ValueKind != JsonValueKind.Number
                || !rawVolume.TryGetInt32(out var requestedVolume)
                || requestedVolume is < 0 or > 100)
            {
                throw new LiteRequestException(
                    "Głośność WiiM musi być liczbą od 0 do 100.");
            }
            volume = requestedVolume;
        }
        return new LiteWiiMTransportRequest(deviceId, command, volume);
    }

    public static LiteWiiMPresetRequest ReadPresetRequest(JsonElement args)
    {
        var deviceId = ReadDeviceId(args);
        if (!args.TryGetProperty("presetNumber", out var rawNumber)
            || rawNumber.ValueKind != JsonValueKind.Number
            || !rawNumber.TryGetInt32(out var presetNumber)
            || presetNumber is < 1 or > 12)
        {
            throw new LiteRequestException(
                "Numer presetu WiiM musi należeć do zakresu od 1 do 12.");
        }
        return new LiteWiiMPresetRequest(deviceId, presetNumber);
    }

    public static LiteWiiMSourceNavigationRequest ReadSourceNavigationRequest(
        JsonElement args)
    {
        var deviceId = ReadDeviceId(args);
        if (!args.TryGetProperty("direction", out var rawDirection)
            || rawDirection.ValueKind != JsonValueKind.Number
            || !rawDirection.TryGetInt32(out var direction)
            || direction is not (-1 or 1))
        {
            throw new LiteRequestException(
                "Kierunek przechodzenia po źródłach WiiM jest nieprawidłowy.");
        }
        return new LiteWiiMSourceNavigationRequest(deviceId, direction);
    }

    public static LiteWiiMOptionRequest ReadOptionRequest(
        JsonElement args,
        bool requireValue = false)
    {
        var deviceId = ReadDeviceId(args);
        var option = LiteArgs.RequireText(args, "option").Trim();
        if (option is not ("input" or "output" or "equalizer" or "repeat" or "sleep"))
            throw new LiteRequestException("Nieznane ustawienie urządzenia WiiM.");

        string? value = null;
        if (requireValue)
        {
            value = LiteArgs.RequireText(args, "value").Trim();
            if (value.Length == 0 || value.Length > 128 || value.Any(char.IsControl))
                throw new LiteRequestException("Nieprawidłowa wartość ustawienia WiiM.");
        }
        return new LiteWiiMOptionRequest(deviceId, option, value);
    }

    public static LiteWiiMNetworkStreamRequest ReadNetworkStreamRequest(
        JsonElement args) => new(
            ReadDeviceId(args),
            LiteArgs.RequireText(args, "streamId").Trim());

    public static string ReadNetworkStreamId(JsonElement args) =>
        LiteArgs.RequireText(args, "streamId").Trim();

    public static CollectionSortMode ReadNetworkStreamSortMode(JsonElement args)
    {
        var text = LiteArgs.RequireText(args, "mode").Trim();
        if (Enum.TryParse<CollectionSortMode>(text, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
            return mode;
        throw new LiteRequestException("Nieznany sposób sortowania strumieni WiiM.");
    }

    public static int ReadNetworkStreamMoveDirection(JsonElement args)
    {
        if (args.TryGetProperty("direction", out var rawDirection)
            && rawDirection.ValueKind is JsonValueKind.Number
            && rawDirection.TryGetInt32(out var direction)
            && direction is (-1 or 1))
            return direction;
        throw new LiteRequestException(
            "Kierunek przenoszenia strumienia WiiM jest nieprawidłowy.");
    }

    public static object CreateDevicesResult(WiiMSettings settings)
    {
        var selected = settings.SelectedDeviceId;
        var items = (settings.Devices ?? [])
            .Where(device => !string.IsNullOrWhiteSpace(device.Id)
                && WiiMAddressPolicy.TryNormalize(device.Address, out _))
            .Select(device => new
            {
                itemId = "wiim:" + device.Id.Trim(),
                deviceId = device.Id.Trim(),
                title = UserTitle(device),
                detail = UserDetail(device),
                selected = string.Equals(
                    device.Id, selected, StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
        return new { items };
    }

    public static object CreateNetworkStreamsResult(
        WiiMSettings settings,
        CollectionSortMode mode = CollectionSortMode.AddedNewest,
        IEnumerable<string>? storedItemIds = null)
    {
        var valid = (settings.NetworkStreams ?? [])
            .Where(stream => !string.IsNullOrWhiteSpace(stream.Id)
                && !string.IsNullOrWhiteSpace(stream.Name)
                && WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out _))
            .DistinctBy(stream => stream.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var remembered = (storedItemIds ?? []).ToArray();
        if (mode is CollectionSortMode.AddedNewest && remembered.Length == 0)
        {
            // Starsze profile nie miały osobnej tabeli kolejności. Zbuduj
            // wtedy jej naturalny odpowiednik od najstarszego do najnowszego;
            // OrderForDisplay odwróci go dokładnie raz.
            remembered = valid
                .OrderBy(stream => stream.AddedUtcTicks)
                .ThenBy(stream => stream.Id, StringComparer.Ordinal)
                .Select(stream => stream.Id)
                .ToArray();
        }
        var items = WiiMNetworkStreamOrdering.OrderForDisplay(
                valid,
                mode,
                remembered)
            .Streams
            .Select(stream => new
            {
                itemId = "wiim-stream:" + stream.Id.Trim(),
                streamId = stream.Id.Trim(),
                title = stream.Name.Trim(),
                detail = "Strumień sieciowy",
                // Adres jest daną polecenia Ctrl+Shift+C. Python przechowuje
                // go w Row.url, ale nie dodaje do kolumn ani nazwy dostępnej.
                streamUrl = stream.StreamUrl.Trim()
            })
            .ToArray();
        return new { items, sortMode = mode.ToString() };
    }

    public static object CreateSnapshotResult(
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot,
        string? message = null,
        string? networkStreamTitle = null)
    {
        var track = snapshot.Track;
        var playback = snapshot.Playback;
        var selectedStreamTitle = Useful(networkStreamTitle);
        return new
        {
            itemId = "wiim:" + device.Id.Trim(),
            deviceId = device.Id.Trim(),
            deviceTitle = UserTitle(device),
            // Część firmware wkłada tutaj adres URL. Zwykły odczyt NVDA ma
            // dostać nazwę treści, nigdy techniczny adres manifestu.
            title = selectedStreamTitle.Length > 0
                ? selectedStreamTitle
                : UsefulMetadata(track.Title),
            subtitle = UsefulMetadata(track.Subtitle),
            artist = UsefulMetadata(track.Artist),
            album = UsefulMetadata(track.Album),
            bitrateKbps = track.BitrateKbps is > 0 and <= 30_000
                ? track.BitrateKbps : null,
            source = UsefulMetadata(playback.Source),
            playbackState = Useful(playback.State),
            volume = Math.Clamp(playback.Volume, 0, 100),
            muted = playback.Muted,
            positionSeconds = Math.Max(0, playback.Position.TotalSeconds),
            durationSeconds = playback.Duration > TimeSpan.Zero
                ? playback.Duration.TotalSeconds
                : (double?)null,
            message = string.IsNullOrWhiteSpace(message)
                ? SnapshotAnnouncement(device, snapshot, selectedStreamTitle)
                : message.Trim()
        };
    }

    public static object CreatePresetsResult(
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot)
    {
        var occupied = snapshot.Presets
            .Where(preset => preset.Number is >= 1 and <= 12)
            .GroupBy(preset => preset.Number)
            .ToDictionary(group => group.Key, group => group.First());
        var currentPresetNumber = WiiMPresetStateResolver.ResolveCurrentPreset(
            snapshot,
            device.LastActivatedPresetNumber);
        var items = Enumerable.Range(1, 12)
            .Select(number =>
            {
                occupied.TryGetValue(number, out var preset);
                var name = Useful(preset?.Name);
                if (preset is not null && name.Length == 0) name = "bez nazwy";
                var source = Useful(preset?.Source);
                var label = preset is null
                    ? $"Preset {number}, pusty"
                    : source.Length == 0
                        ? $"Preset {number}, {name}"
                        : $"Preset {number}, {name}, {source}";
                return new
                {
                    itemId = $"wiim-preset:{number}",
                    presetNumber = number,
                    // Celowy tekst użytkowy dla mapowania skrótów. URI oraz
                    // obiekt ustawień urządzenia nadal nie opuszczają hosta.
                    name,
                    source,
                    label,
                    occupied = preset is not null,
                    current = currentPresetNumber == number,
                    activationError = WiiMPresetPlayability.DescribeUnplayableReason(preset)
                        ?? string.Empty
                };
            })
            .ToArray();
        return new
        {
            deviceId = device.Id.Trim(),
            deviceTitle = UserTitle(device),
            currentPresetNumber,
            items
        };
    }

    public static object CreateOptionsResult(
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot,
        string option,
        bool equalizerEnabled = false,
        IEnumerable<string>? equalizerPresets = null,
        int outputMode = 0)
    {
        LiteWiiMOptionChoice[] items;
        string title;
        string description;
        string? selectedValue;

        switch (option)
        {
            case "input":
                title = "Wejście urządzenia WiiM";
                description = "Wybierz źródło sygnału. Urządzenie może odrzucić wejście, którego dany model nie posiada.";
                items =
                [
                    new("wifi", "Odtwarzanie sieciowe"),
                    new("line-in", "Wejście liniowe lub AUX"),
                    new("optical", "Wejście optyczne"),
                    new("bluetooth", "Bluetooth"),
                    new("udisk", "Pamięć USB")
                ];
                selectedValue = snapshot.Playback.Source switch
                {
                    "wejście liniowe" => "line-in",
                    "wejście optyczne" => "optical",
                    "Bluetooth" => "bluetooth",
                    "pamięć zewnętrzna" => "udisk",
                    _ => "wifi"
                };
                break;
            case "output":
                title = "Wyjście urządzenia WiiM";
                description = "Wybierz fizyczne wyjście audio. Dostępność zależy od modelu urządzenia.";
                items =
                [
                    new("2", "Wyjście analogowe AUX"),
                    new("1", "Wyjście optyczne SPDIF"),
                    new("3", "Wyjście koncentryczne")
                ];
                selectedValue = outputMode.ToString();
                break;
            case "equalizer":
                title = "Korektor urządzenia WiiM";
                description = "Wybierz ustawienie korektora zapisane w urządzeniu albo wyłącz korektor.";
                var rawPresets = (equalizerPresets ?? [])
                    .Select(Useful)
                    .ToArray();
                var presets = rawPresets
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .Select(value => new LiteWiiMOptionChoice(value, value))
                    .ToArray();
                items = [new("off", "Korektor wyłączony"), .. presets];
                selectedValue = equalizerEnabled
                    && snapshot.Playback.EqualizerPresetNumber >= 0
                    && snapshot.Playback.EqualizerPresetNumber < rawPresets.Length
                    && rawPresets[snapshot.Playback.EqualizerPresetNumber].Length > 0
                        ? rawPresets[snapshot.Playback.EqualizerPresetNumber]
                        : "off";
                break;
            case "repeat":
                title = "Tryb powtarzania WiiM";
                description = "Wybierz kolejność i sposób powtarzania bieżącej listy urządzenia.";
                items =
                [
                    new("0", "Bez powtarzania"),
                    new("-1", "Powtarzaj całą listę"),
                    new("1", "Powtarzaj jeden element"),
                    new("2", "Losowo z powtarzaniem")
                ];
                selectedValue = snapshot.Playback.LoopMode switch
                {
                    0 => "-1",
                    1 => "1",
                    2 => "2",
                    4 => "0",
                    _ => null
                };
                break;
            case "sleep":
                title = "Timer uśpienia WiiM";
                description = "Wybierz czas do zatrzymania urządzenia albo anuluj wcześniej ustawiony timer.";
                items =
                [
                    new("-1", "Timer uśpienia wyłączony"),
                    new("900", "Wyłącz odtwarzanie za 15 minut"),
                    new("1800", "Wyłącz odtwarzanie za 30 minut"),
                    new("2700", "Wyłącz odtwarzanie za 45 minut"),
                    new("3600", "Wyłącz odtwarzanie za godzinę"),
                    new("5400", "Wyłącz odtwarzanie za 90 minut"),
                    new("7200", "Wyłącz odtwarzanie za 2 godziny")
                ];
                selectedValue = null;
                break;
            default:
                throw new LiteRequestException("Nieznane ustawienie urządzenia WiiM.");
        }

        return new
        {
            deviceId = device.Id.Trim(),
            deviceTitle = UserTitle(device),
            option,
            title,
            description,
            selectedValue,
            items
        };
    }

    public static string OptionLabel(string option, string value) => option switch
    {
        "input" => value switch
        {
            "wifi" => "Odtwarzanie sieciowe",
            "line-in" => "Wejście liniowe lub AUX",
            "optical" => "Wejście optyczne",
            "bluetooth" => "Bluetooth",
            "udisk" => "Pamięć USB",
            _ => string.Empty
        },
        "output" => value switch
        {
            "2" => "Wyjście analogowe AUX",
            "1" => "Wyjście optyczne SPDIF",
            "3" => "Wyjście koncentryczne",
            _ => string.Empty
        },
        "equalizer" => value == "off" ? "Korektor wyłączony" : Useful(value),
        "repeat" => value switch
        {
            "0" => "Bez powtarzania",
            "-1" => "Powtarzaj całą listę",
            "1" => "Powtarzaj jeden element",
            "2" => "Losowo z powtarzaniem",
            _ => string.Empty
        },
        "sleep" => value switch
        {
            "-1" => "Timer uśpienia wyłączony",
            "900" => "Wyłącz odtwarzanie za 15 minut",
            "1800" => "Wyłącz odtwarzanie za 30 minut",
            "2700" => "Wyłącz odtwarzanie za 45 minut",
            "3600" => "Wyłącz odtwarzanie za godzinę",
            "5400" => "Wyłącz odtwarzanie za 90 minut",
            "7200" => "Wyłącz odtwarzanie za 2 godziny",
            _ => string.Empty
        },
        _ => string.Empty
    };

    private static string SnapshotAnnouncement(
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot,
        string? preferredTitle = null)
    {
        var parts = new List<string>();
        AddDistinct(parts, UsefulMetadata(preferredTitle));
        AddDistinct(parts, UsefulMetadata(snapshot.Track.Title));
        AddDistinct(parts, UsefulMetadata(snapshot.Track.Subtitle));
        AddDistinct(parts, UsefulMetadata(snapshot.Track.Artist));
        AddDistinct(parts, UsefulMetadata(snapshot.Track.Album));
        if (parts.Count == 0) AddDistinct(parts, UserTitle(device));
        AddDistinct(parts, Useful(snapshot.Playback.State));
        if (snapshot.Playback.Muted) parts.Add("wyciszone");
        parts.Add($"głośność {Math.Clamp(snapshot.Playback.Volume, 0, 100)}%");
        return string.Join(", ", parts);
    }

    private static void AddDistinct(List<string> parts, string value)
    {
        if (value.Length == 0 || parts.Any(part => string.Equals(
                part, value, StringComparison.CurrentCultureIgnoreCase))) return;
        parts.Add(value);
    }

    private static string UserTitle(WiiMDeviceSettings device)
    {
        var title = Useful(device.DisplayName);
        // Starsza normalizacja profilu potrafiła utworzyć nazwę
        // "WiiM 192.168...". Adres jest szczegółem połączenia, nie nazwą
        // dostępną pozycji listy.
        return title.Length == 0
            || (!string.IsNullOrWhiteSpace(device.Address)
                && title.Contains(device.Address.Trim(), StringComparison.OrdinalIgnoreCase))
            ? "Urządzenie WiiM"
            : title;
    }

    private static string UserDetail(WiiMDeviceSettings device)
    {
        var parts = new[] { device.Model, device.Firmware }
            .Select(Useful)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase);
        return string.Join(", ", parts);
    }

    private static string Useful(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            || text.Equals("unknow", StringComparison.OrdinalIgnoreCase)
            || text.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : text;
    }

    private static string UsefulMetadata(string? value) =>
        WiiMNetworkStreamPresentation.UsefulMetadataText(value) ?? string.Empty;
}

public sealed record LiteWiiMTransportRequest(
    string DeviceId,
    string Command,
    int? Volume);

public sealed record LiteWiiMPresetRequest(
    string DeviceId,
    int PresetNumber);

public sealed record LiteWiiMSourceNavigationRequest(
    string DeviceId,
    int Direction);

public sealed record LiteWiiMOptionRequest(
    string DeviceId,
    string Option,
    string? Value);

public sealed record LiteWiiMOptionChoice(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label);

public sealed record LiteWiiMNetworkStreamRequest(
    string DeviceId,
    string StreamId);
