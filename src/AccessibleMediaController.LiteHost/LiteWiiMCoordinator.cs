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

    public object ListDevices() => LiteWiiMContract.CreateDevicesResult(ReadSettings());

    public object Snapshot(JsonElement args)
    {
        var settings = ReadSettings();
        var device = ResolveDevice(settings, LiteWiiMContract.ReadDeviceId(args));
        // Ten sam zamek co transport: cichy odczyt timera nie może wrócić
        // po poleceniu z wcześniejszym stanem i cofnąć interfejsu.
        lock (gate)
        {
            var snapshot = ReadSnapshot(device);
            return LiteWiiMContract.CreateSnapshotResult(device, snapshot);
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
                        client.PreviousAsync(device.Address).GetAwaiter().GetResult();
                        break;
                    case "next":
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
                return LiteWiiMContract.CreateSnapshotResult(device, refreshed, message);
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

    private WiiMSettings ReadSettings()
    {
        try
        {
            using var stream = new FileStream(
                statePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("wiiM", out var wiim)
                || wiim.ValueKind != JsonValueKind.Object)
            {
                return new WiiMSettings();
            }
            return JsonSerializer.Deserialize<WiiMSettings>(wiim.GetRawText(), JsonOptions)
                ?? new WiiMSettings();
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

    private static string UserTitle(WiiMDeviceSettings device) =>
        string.IsNullOrWhiteSpace(device.DisplayName)
            ? "WiiM"
            : device.DisplayName.Trim();

    public void Dispose() => client.Dispose();
}
