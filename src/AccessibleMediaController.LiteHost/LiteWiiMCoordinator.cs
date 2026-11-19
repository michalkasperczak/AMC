using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Odczytuje rejestracje WiiM ze wspolnego profilu tylko do odczytu i wykonuje
/// polecenia przez ten sam klient urzadzenia co glowne AMC.
/// </summary>
internal sealed class LiteWiiMCoordinator(string statePath) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object gate = new();
    private readonly WiiMDeviceClient client = new();
    private readonly Dictionary<string, string> activeNetworkStreams =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> activePresets =
        new(StringComparer.OrdinalIgnoreCase);

    public object ListDevices() => LiteWiiMContract.CreateDevicesResult(ReadSettings());

    public object ListNetworkStreams()
    {
        var profile = ReadProfile();
        return LiteWiiMContract.CreateNetworkStreamsResult(
            profile.Settings,
            profile.SortMode,
            profile.StoredItemIds);
    }

    public object NetworkStreamEditInfo(JsonElement args)
    {
        var streamId = LiteWiiMContract.ReadNetworkStreamId(args);
        var stream = (ReadSettings().NetworkStreams ?? []).FirstOrDefault(candidate =>
            string.Equals(candidate.Id, streamId, StringComparison.Ordinal));
        if (stream is null
            || string.IsNullOrWhiteSpace(stream.Name)
            || !WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out var url))
        {
            throw new LiteRequestException(
                "Nie można odnaleźć tego strumienia na zapisanej liście AMC.");
        }
        // Adres wychodzi z hosta wyłącznie po jawnym F2 i trafia do pola
        // edycji. Zwykła lista, fokus i komunikaty nadal go nie otrzymują.
        return new { streamId, name = stream.Name.Trim(), url };
    }

    public object Snapshot(JsonElement args)
    {
        var settings = ReadSettings();
        var device = ResolveDevice(settings, LiteWiiMContract.ReadDeviceId(args));
        // Ten sam zamek co transport: cichy odczyt timera nie może wrócić
        // po poleceniu z wcześniejszym stanem i cofnąć interfejsu.
        lock (gate)
        {
            var snapshot = ReadSnapshot(device);
            return CreateSnapshotResult(settings, device, snapshot);
        }
    }

    public object Presets(JsonElement args)
    {
        var settings = ReadSettings();
        var device = ResolveDevice(settings, LiteWiiMContract.ReadDeviceId(args));
        lock (gate)
        {
            var snapshot = ReadSnapshot(device);
            return LiteWiiMContract.CreatePresetsResult(device, snapshot);
        }
    }

    public object ActivatePreset(JsonElement args)
    {
        var request = LiteWiiMContract.ReadPresetRequest(args);
        var settings = ReadSettings();
        var device = ResolveDevice(settings, request.DeviceId);
        lock (gate)
        {
            try
            {
                var before = ReadSnapshot(device);
                var preset = before.Presets.FirstOrDefault(candidate =>
                    candidate.Number == request.PresetNumber);
                if (preset is null)
                    throw new LiteRequestException(
                        $"Preset {request.PresetNumber} jest pusty.");
                if (WiiMPresetPlayability.DescribeUnplayableReason(preset)
                    is { } unavailableReason)
                {
                    throw new LiteRequestException(unavailableReason);
                }

                client.ActivatePresetAsync(device.Address, request.PresetNumber)
                    .GetAwaiter().GetResult();
                activeNetworkStreams.Remove(device.Id);
                activePresets[device.Id] = request.PresetNumber;
                Thread.Sleep(TimeSpan.FromMilliseconds(180));
                var refreshed = ReadSnapshot(device);
                var title = string.IsNullOrWhiteSpace(preset.Name)
                    ? $"preset {request.PresetNumber}"
                    : preset.Name.Trim();
                return LiteWiiMContract.CreateSnapshotResult(
                    device,
                    refreshed,
                    $"Uruchomiono preset {request.PresetNumber}, {title}");
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException
                or TaskCanceledException or IOException or FormatException
                or ArgumentException)
            {
                throw new LiteRequestException(
                    $"Nie udało się uruchomić presetu urządzenia {UserTitle(device)}.");
            }
        }
    }

    public object Options(JsonElement args)
    {
        var request = LiteWiiMContract.ReadOptionRequest(args);
        var settings = ReadSettings();
        var device = ResolveDevice(settings, request.DeviceId);
        lock (gate)
        {
            try
            {
                var snapshot = ReadSnapshot(device);
                var equalizerEnabled = false;
                IReadOnlyList<string> equalizerPresets = [];
                var outputMode = 0;
                if (request.Option == "equalizer")
                {
                    equalizerEnabled = client.ReadEqualizerEnabledAsync(device.Address)
                        .GetAwaiter().GetResult();
                    equalizerPresets = client.ReadEqualizerPresetsAsync(device.Address)
                        .GetAwaiter().GetResult();
                }
                else if (request.Option == "output")
                {
                    outputMode = client.ReadAudioOutputHardwareModeAsync(device.Address)
                        .GetAwaiter().GetResult();
                }
                return LiteWiiMContract.CreateOptionsResult(
                    device,
                    snapshot,
                    request.Option,
                    equalizerEnabled,
                    equalizerPresets,
                    outputMode);
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                throw new LiteRequestException(
                    $"Urządzenie {UserTitle(device)} nie udostępniło tej opcji.");
            }
        }
    }

    public object SetOption(JsonElement args)
    {
        var request = LiteWiiMContract.ReadOptionRequest(args, requireValue: true);
        var settings = ReadSettings();
        var device = ResolveDevice(settings, request.DeviceId);
        var value = request.Value!;
        lock (gate)
        {
            try
            {
                var label = LiteWiiMContract.OptionLabel(request.Option, value);
                if (request.Option == "equalizer")
                {
                    var available = client.ReadEqualizerPresetsAsync(device.Address)
                        .GetAwaiter().GetResult();
                    if (value != "off" && !available.Contains(
                            value, StringComparer.CurrentCultureIgnoreCase))
                    {
                        throw new LiteRequestException(
                            "Wybrane ustawienie korektora nie jest już dostępne w urządzeniu.");
                    }
                    if (value == "off")
                    {
                        client.SetEqualizerEnabledAsync(device.Address, false)
                            .GetAwaiter().GetResult();
                    }
                    else
                    {
                        client.LoadEqualizerPresetAsync(device.Address, value)
                            .GetAwaiter().GetResult();
                        client.SetEqualizerEnabledAsync(device.Address, true)
                            .GetAwaiter().GetResult();
                    }
                }
                else
                {
                    if (label.Length == 0)
                        throw new LiteRequestException(
                            "Wybrana wartość ustawienia WiiM jest nieprawidłowa.");
                    switch (request.Option)
                    {
                        case "input":
                            client.SwitchInputAsync(device.Address, value)
                                .GetAwaiter().GetResult();
                            activeNetworkStreams.Remove(device.Id);
                            activePresets.Remove(device.Id);
                            break;
                        case "output":
                            client.SetAudioOutputHardwareModeAsync(
                                    device.Address, int.Parse(value))
                                .GetAwaiter().GetResult();
                            break;
                        case "repeat":
                            client.SetLoopModeAsync(device.Address, int.Parse(value))
                                .GetAwaiter().GetResult();
                            break;
                        case "sleep":
                            client.SetSleepTimerAsync(device.Address, int.Parse(value))
                                .GetAwaiter().GetResult();
                            break;
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(120));
                var refreshed = ReadSnapshot(device);
                return CreateSnapshotResult(settings, device, refreshed, label);
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                throw new LiteRequestException(
                    $"Urządzenie {UserTitle(device)} nie przyjęło wybranej opcji.");
            }
        }
    }

    public object ToggleShuffle(JsonElement args)
    {
        var settings = ReadSettings();
        var device = ResolveDevice(settings, LiteWiiMContract.ReadDeviceId(args));
        lock (gate)
        {
            try
            {
                var before = ReadSnapshot(device);
                var enabled = before.Playback.LoopMode is 2 or 3;
                var target = enabled
                    ? before.Playback.LoopMode == 2 ? -1 : 0
                    : 2;
                client.SetLoopModeAsync(device.Address, target)
                    .GetAwaiter().GetResult();
                Thread.Sleep(TimeSpan.FromMilliseconds(120));
                var refreshed = ReadSnapshot(device);
                return CreateSnapshotResult(
                    settings,
                    device,
                    refreshed,
                    enabled
                        ? "Losowanie wyłączone"
                        : "Losowanie z powtarzaniem włączone");
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                throw new LiteRequestException(
                    $"Nie udało się zmienić losowania urządzenia {UserTitle(device)}.");
            }
        }
    }

    public object NavigateSource(JsonElement args)
    {
        var request = LiteWiiMContract.ReadSourceNavigationRequest(args);
        var profile = ReadProfile();
        var settings = profile.Settings;
        var device = ResolveDevice(settings, request.DeviceId);
        lock (gate)
        {
            try
            {
                var before = ReadSnapshot(device);
                var source = ResolveActiveSourceState(
                    settings, device, before, trustRememberedNetworkPreset: true);
                var streams = WiiMNetworkStreamOrdering.OrderForDisplay(
                        settings.NetworkStreams ?? [],
                        profile.SortMode,
                        profile.StoredItemIds)
                    .Streams
                    .ToArray();
                var currentStreamIndex = Array.FindIndex(streams, stream =>
                    string.Equals(
                        stream.Id,
                        source.NetworkStreamId,
                        StringComparison.OrdinalIgnoreCase));
                if (currentStreamIndex >= 0)
                {
                    if (streams.Length < 2)
                        throw new LiteRequestException(
                            "Lista zawiera tylko jeden strumień WiiM.");
                    var nextStreamIndex = (
                        currentStreamIndex + request.Direction + streams.Length
                    ) % streams.Length;
                    var target = streams[nextStreamIndex];
                    if (!WiiMPlaybackUriPolicy.TryNormalize(
                            target.StreamUrl, out var mediaUrl))
                    {
                        throw new LiteRequestException(
                            "Następny strumień WiiM ma nieprawidłowy adres.");
                    }
                    client.PlayNetworkStreamAsync(device.Address, mediaUrl)
                        .GetAwaiter().GetResult();
                    activeNetworkStreams[device.Id] = target.Id.Trim();
                    activePresets.Remove(device.Id);
                    Thread.Sleep(TimeSpan.FromMilliseconds(450));
                    var refreshedStream = ReadSnapshot(device);
                    return LiteWiiMContract.CreateSnapshotResult(
                        device,
                        refreshedStream,
                        $"Otwarto {target.Name.Trim()} na {UserTitle(device)}",
                        target.Name.Trim());
                }

                var currentNumber = WiiMPresetStateResolver.ResolveCurrentPreset(
                    before,
                    activePresets.GetValueOrDefault(
                        device.Id, device.LastActivatedPresetNumber),
                    trustRememberedNetworkPreset: true);
                if (currentNumber is null)
                    throw new LiteRequestException(
                        "Nie wiadomo, który preset jest odtwarzany. "
                        + "Ctrl+Alt+P otwiera listę presetów urządzenia.");

                var occupied = before.Presets
                    .Where(preset => preset.Number is >= 1 and <= 12)
                    .OrderBy(preset => preset.Number)
                    .ToArray();
                var currentIndex = Array.FindIndex(
                    occupied, preset => preset.Number == currentNumber);
                if (currentIndex < 0)
                {
                    activePresets.Remove(device.Id);
                    throw new LiteRequestException(
                        "Bieżący preset nie jest już zajęty. "
                        + "Ctrl+Alt+P otwiera aktualną listę.");
                }
                if (occupied.Length < 2)
                    throw new LiteRequestException(
                        "Urządzenie ma tylko jeden zajęty preset.");

                var playable = occupied.Where(WiiMPresetPlayability.CanActivate).ToArray();
                if (playable.Length == 0
                    || playable.Length == 1
                    && playable[0].Number == currentNumber)
                {
                    throw new LiteRequestException(
                        "Pozostałe presety to źródła, których urządzenie samo nie uruchomi.");
                }
                var nextIndex = (
                    currentIndex + request.Direction + occupied.Length
                ) % occupied.Length;
                while (!WiiMPresetPlayability.CanActivate(occupied[nextIndex]))
                {
                    nextIndex = (
                        nextIndex + request.Direction + occupied.Length
                    ) % occupied.Length;
                }
                var targetPreset = occupied[nextIndex];
                client.ActivatePresetAsync(device.Address, targetPreset.Number)
                    .GetAwaiter().GetResult();
                activeNetworkStreams.Remove(device.Id);
                activePresets[device.Id] = targetPreset.Number;
                Thread.Sleep(TimeSpan.FromMilliseconds(180));
                var refreshedPreset = ReadSnapshot(device);
                var title = string.IsNullOrWhiteSpace(targetPreset.Name)
                    ? "bez nazwy"
                    : targetPreset.Name.Trim();
                return LiteWiiMContract.CreateSnapshotResult(
                    device,
                    refreshedPreset,
                    $"Preset {targetPreset.Number}, {title}");
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (IsConnectionFailure(exception))
            {
                throw new LiteRequestException(
                    $"Nie udało się zmienić źródła urządzenia {UserTitle(device)}.");
            }
        }
    }

    public object PlayNetworkStream(JsonElement args)
    {
        var request = LiteWiiMContract.ReadNetworkStreamRequest(args);
        var settings = ReadSettings();
        var device = ResolveDevice(settings, request.DeviceId);
        var stream = (settings.NetworkStreams ?? []).FirstOrDefault(candidate =>
            string.Equals(candidate.Id, request.StreamId, StringComparison.OrdinalIgnoreCase));
        if (stream is null
            || string.IsNullOrWhiteSpace(stream.Name)
            || !WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out var mediaUrl))
        {
            throw new LiteRequestException(
                "Nie można odnaleźć zapisanego strumienia WiiM.");
        }

        lock (gate)
        {
            try
            {
                client.PlayNetworkStreamAsync(device.Address, mediaUrl)
                    .GetAwaiter().GetResult();
                activeNetworkStreams[device.Id] = stream.Id.Trim();
                activePresets.Remove(device.Id);
                Thread.Sleep(TimeSpan.FromMilliseconds(450));
                var snapshot = ReadSnapshot(device);
                return LiteWiiMContract.CreateSnapshotResult(
                    device,
                    snapshot,
                    $"Otwarto {stream.Name.Trim()} na {UserTitle(device)}",
                    stream.Name.Trim());
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException
                or TaskCanceledException or IOException or FormatException
                or ArgumentException)
            {
                throw new LiteRequestException(
                    $"Nie udało się otworzyć strumienia na urządzeniu {UserTitle(device)}.");
            }
        }
    }

    public object Transport(JsonElement args)
    {
        var request = LiteWiiMContract.ReadTransportRequest(args);
        var settings = ReadSettings();
        var device = ResolveDevice(settings, request.DeviceId);
        lock (gate)
        {
            try
            {
                var before = ReadSnapshot(device);
                int? expectedVolume = null;
                switch (request.Command)
                {
                    case "toggle":
                        client.TogglePlayPauseAsync(device.Address).GetAwaiter().GetResult();
                        break;
                    case "previous":
                        activeNetworkStreams.Remove(device.Id);
                        activePresets.Remove(device.Id);
                        client.PreviousAsync(device.Address).GetAwaiter().GetResult();
                        break;
                    case "next":
                        activeNetworkStreams.Remove(device.Id);
                        activePresets.Remove(device.Id);
                        client.NextAsync(device.Address).GetAwaiter().GetResult();
                        break;
                    case "setVolume":
                        expectedVolume = request.Volume!.Value;
                        SetVolume(device, expectedVolume.Value);
                        break;
                    case "volumeUp5":
                        expectedVolume = Math.Clamp(before.Playback.Volume + 5, 0, 100);
                        SetVolume(device, expectedVolume.Value);
                        break;
                    case "volumeDown5":
                        expectedVolume = Math.Clamp(before.Playback.Volume - 5, 0, 100);
                        SetVolume(device, expectedVolume.Value);
                        break;
                    case "volumeUp1":
                        expectedVolume = Math.Clamp(before.Playback.Volume + 1, 0, 100);
                        SetVolume(device, expectedVolume.Value);
                        break;
                    case "volumeDown1":
                        expectedVolume = Math.Clamp(before.Playback.Volume - 1, 0, 100);
                        SetVolume(device, expectedVolume.Value);
                        break;
                }

                var volumeCommand = expectedVolume is not null;
                Thread.Sleep(volumeCommand
                    ? TimeSpan.FromMilliseconds(180)
                    : TimeSpan.FromMilliseconds(120));
                var refreshed = ReadSnapshot(device);
                if (expectedVolume is { } target
                    && refreshed.Playback.Volume != target)
                {
                    // Część firmware publikuje przez moment starą wartość.
                    // Drugi, ograniczony odczyt odpowiada zachowaniu pełnego AMC.
                    Thread.Sleep(TimeSpan.FromMilliseconds(300));
                    refreshed = ReadSnapshot(device);
                }
                var message = volumeCommand
                    ? refreshed.Playback.Volume != expectedVolume
                        ? $"Urządzenie pozostało na głośności {refreshed.Playback.Volume}%"
                        : refreshed.Playback.Muted
                            ? "Wyciszono"
                            : $"Głośność {refreshed.Playback.Volume}%"
                    : request.Command == "toggle"
                        ? refreshed.Playback.State
                        : string.IsNullOrWhiteSpace(refreshed.Track.Title)
                            ? refreshed.Playback.State
                            : refreshed.Track.Title;
                return CreateSnapshotResult(settings, device, refreshed, message);
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (exception is HttpRequestException
                or TaskCanceledException or IOException or FormatException
                or ArgumentException)
            {
                throw new LiteRequestException(
                    $"Nie udało się sterować urządzeniem {UserTitle(device)}.");
            }
        }
    }

    private void SetVolume(WiiMDeviceSettings device, int value) =>
        client.SetVolumeAsync(device.Address, Math.Clamp(value, 0, 100))
            .GetAwaiter().GetResult();

    private static bool IsConnectionFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or IOException
            or FormatException or ArgumentException;

    private WiiMDeviceSnapshot ReadSnapshot(WiiMDeviceSettings device)
    {
        try
        {
            return client.ReadSnapshotAsync(device.Address).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException or IOException or FormatException
            or ArgumentException)
        {
            throw new LiteRequestException(
                $"Brak odpowiedzi urządzenia {UserTitle(device)}.");
        }
    }

    private object CreateSnapshotResult(
        WiiMSettings settings,
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot,
        string? message = null)
    {
        var source = ResolveActiveSourceState(settings, device, snapshot);
        var title = (settings.NetworkStreams ?? []).FirstOrDefault(stream =>
            string.Equals(
                stream.Id,
                source.NetworkStreamId,
                StringComparison.OrdinalIgnoreCase))?.Name;
        return LiteWiiMContract.CreateSnapshotResult(
            device,
            snapshot,
            message,
            title,
            settings.NetworkStreams);
    }

    private (string? NetworkStreamId, int? PresetNumber) ResolveActiveSourceState(
        WiiMSettings settings,
        WiiMDeviceSettings device,
        WiiMDeviceSnapshot snapshot,
        bool trustRememberedNetworkPreset = false)
    {
        activeNetworkStreams.TryGetValue(device.Id, out var rememberedId);
        var matched = (settings.NetworkStreams ?? []).FirstOrDefault(stream =>
            WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out var candidate)
            && WiiMPlaybackUriPolicy.TryNormalize(
                snapshot.Playback.ContentUri, out var current)
            && string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase));
        var preset = WiiMPresetStateResolver.ResolveCurrentPreset(
            snapshot,
            activePresets.GetValueOrDefault(
                device.Id, device.LastActivatedPresetNumber),
            trustRememberedNetworkPreset);
        var resolvedId = WiiMActiveSourceState.ResolveNetworkStreamId(
            rememberedId,
            matched?.Id,
            preset is not null,
            (settings.NetworkStreams ?? []).Select(stream => stream.Id));
        if (resolvedId is null) activeNetworkStreams.Remove(device.Id);
        else activeNetworkStreams[device.Id] = resolvedId;
        if (preset is null) activePresets.Remove(device.Id);
        else activePresets[device.Id] = preset.Value;
        return (resolvedId, preset);
    }

    private static WiiMDeviceSettings ResolveDevice(
        WiiMSettings settings, string deviceId)
    {
        var device = settings.Devices.FirstOrDefault(candidate => string.Equals(
            candidate.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        if (device is null)
            throw new LiteRequestException("Nie można odnaleźć zapisanego urządzenia WiiM.");
        if (!WiiMAddressPolicy.TryNormalize(device.Address, out _))
            throw new LiteRequestException("Zapisane urządzenie WiiM ma nieprawidłowy adres.");
        return device;
    }

    private WiiMSettings ReadSettings() => ReadProfile().Settings;

    private WiiMProfile ReadProfile()
    {
        try
        {
            using var stream = new FileStream(
                statePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryGetProperty(document.RootElement, out var wiim, "wiiM", "WiiM")
                || wiim.ValueKind != JsonValueKind.Object)
            {
                return new WiiMProfile(
                    new WiiMSettings(),
                    CollectionSortMode.AddedNewest,
                    []);
            }
            var settings = JsonSerializer.Deserialize<WiiMSettings>(
                wiim.GetRawText(),
                JsonOptions) ?? new WiiMSettings();
            var mode = ReadSortMode(document.RootElement);
            return new WiiMProfile(
                settings,
                mode,
                ReadOrderIds(
                    document.RootElement,
                    custom: mode is CollectionSortMode.Custom));
        }
        catch (FileNotFoundException)
        {
            throw new LiteRequestException(
                "Nie znaleziono profilu AMC potrzebnego do obsługi WiiM.");
        }
        catch (JsonException)
        {
            throw new LiteRequestException("Profil AMC z urządzeniami WiiM jest uszkodzony.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LiteRequestException(
                "Nie można teraz odczytać urządzeń WiiM z profilu AMC.");
        }
    }

    private static CollectionSortMode ReadSortMode(JsonElement root)
    {
        if (TryGetProperty(root, out var navigation, "sessionNavigation", "SessionNavigation")
            && TryGetProperty(navigation, out var sessions, "sessions", "Sessions")
            && TryGetProperty(sessions, out var wiiM, "wiim", "WiiM")
            && TryGetProperty(
                wiiM,
                out var modes,
                "collectionSortModes",
                "CollectionSortModes")
            && TryGetProperty(modes, out var value, "Biblioteka"))
        {
            if (value.ValueKind == JsonValueKind.String
                && Enum.TryParse<CollectionSortMode>(
                    value.GetString(),
                    ignoreCase: true,
                    out var parsed))
                return parsed;
            if (value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number)
                && Enum.IsDefined(typeof(CollectionSortMode), number))
                return (CollectionSortMode)number;
        }
        return CollectionSortMode.AddedNewest;
    }

    private static string[] ReadOrderIds(JsonElement root, bool custom)
    {
        var property = custom
            ? "libraryItemIdsBySession"
            : "libraryAddedItemIdsBySession";
        var legacy = custom
            ? "LibraryItemIdsBySession"
            : "LibraryAddedItemIdsBySession";
        if (!TryGetProperty(root, out var orders, "collectionOrders", "CollectionOrders")
            || !TryGetProperty(orders, out var bySession, property, legacy)
            || !TryGetProperty(bySession, out var values, "wiim", "WiiM")
            || values.ValueKind != JsonValueKind.Array)
            return [];
        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim() ?? "")
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryGetProperty(
        JsonElement source,
        out JsonElement value,
        params string[] names)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in names)
            {
                if (source.TryGetProperty(name, out value)) return true;
            }
        }
        value = default;
        return false;
    }

    private sealed record WiiMProfile(
        WiiMSettings Settings,
        CollectionSortMode SortMode,
        IReadOnlyList<string> StoredItemIds);

    private static string UserTitle(WiiMDeviceSettings device) =>
        string.IsNullOrWhiteSpace(device.DisplayName)
            ? "WiiM"
            : device.DisplayName.Trim();

    public void Dispose() => client.Dispose();
}
