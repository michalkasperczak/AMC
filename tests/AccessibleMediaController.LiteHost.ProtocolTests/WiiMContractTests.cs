using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class WiiMContractTests
{
    public static void Run()
    {
        DeviceListDoesNotExposeAddressesOrRuntimeObjects();
        SnapshotContainsOnlyUserFacingPlaybackData();
        TransportAcceptsOnlyKnownCommands();
    }

    private static void DeviceListDoesNotExposeAddressesOrRuntimeObjects()
    {
        var settings = new WiiMSettings
        {
            SelectedDeviceId = "device-1",
            Devices =
            [
                new WiiMDeviceSettings
                {
                    Id = "device-1",
                    Address = "192.168.1.40",
                    DisplayName = "Salon",
                    Model = "WiiM Pro",
                    Firmware = "4.8"
                }
            ]
        };

        var json = JsonSerializer.Serialize(LiteWiiMContract.CreateDevicesResult(settings));
        using var document = JsonDocument.Parse(json);
        var item = document.RootElement.GetProperty("items")[0];
        Check(item.GetProperty("title").GetString() == "Salon", "brak nazwy urządzenia");
        Check(item.GetProperty("detail").GetString() == "WiiM Pro, 4.8", "brak opisu");
        Check(item.GetProperty("selected").GetBoolean(), "zgubiono wybór urządzenia");
        Check(!json.Contains("192.168.1.40", StringComparison.Ordinal),
            "adres urządzenia wyciekł do interfejsu Python");
        Check(!json.Contains("WiiMDeviceSettings", StringComparison.Ordinal),
            "techniczna reprezentacja obiektu wyciekła do odpowiedzi");

        settings.Devices[0].DisplayName = "WiiM 192.168.1.40";
        var legacyJson = JsonSerializer.Serialize(
            LiteWiiMContract.CreateDevicesResult(settings));
        Check(!legacyJson.Contains("192.168.1.40", StringComparison.Ordinal),
            "adres z automatycznej starej nazwy wyciekł do etykiety");
    }

    private static void SnapshotContainsOnlyUserFacingPlaybackData()
    {
        var device = new WiiMDeviceSettings
        {
            Id = "device-1", Address = "192.168.1.40", DisplayName = "Salon"
        };
        var snapshot = new WiiMDeviceSnapshot(
            new WiiMDeviceInformation("192.168.1.40", "device-1", "Salon", "Pro", "4.8", 12),
            new WiiMPlaybackInformation(
                "odtwarzanie", "radio internetowe", 35, false,
                TimeSpan.FromSeconds(61), TimeSpan.FromSeconds(600)),
            new WiiMTrackInformation("Audycja", "Radio", "", 48000, 24),
            []);

        var json = JsonSerializer.Serialize(
            LiteWiiMContract.CreateSnapshotResult(device, snapshot));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.GetProperty("title").GetString() == "Audycja", "brak tytułu");
        Check(root.GetProperty("volume").GetInt32() == 35, "brak głośności");
        Check(root.GetProperty("positionSeconds").GetDouble() == 61, "brak pozycji");
        Check(!json.Contains("192.168.1.40", StringComparison.Ordinal),
            "adres urządzenia wyciekł z migawki");
    }

    private static void TransportAcceptsOnlyKnownCommands()
    {
        foreach (var command in new[]
                 {
                     "toggle", "previous", "next", "volumeUp5",
                     "volumeDown5", "volumeUp1", "volumeDown1"
                 })
        {
            using var document = JsonDocument.Parse(
                $$"""{"deviceId":"device-1","command":"{{command}}"}""");
            Check(LiteWiiMContract.ReadTransportRequest(document.RootElement).Command == command,
                $"nie przyjęto polecenia {command}");
        }

        using var exactVolume = JsonDocument.Parse(
            """{"deviceId":"device-1","command":"setVolume","volume":37}""");
        var exactRequest = LiteWiiMContract.ReadTransportRequest(exactVolume.RootElement);
        Check(exactRequest.Command == "setVolume" && exactRequest.Volume == 37,
            "nie przyjęto jawnej głośności WiiM");

        using var invalidVolume = JsonDocument.Parse(
            """{"deviceId":"device-1","command":"setVolume","volume":137}""");
        try
        {
            LiteWiiMContract.ReadTransportRequest(invalidVolume.RootElement);
            throw new InvalidOperationException("przyjęto głośność WiiM poza zakresem");
        }
        catch (LiteRequestException)
        {
        }

        using var rejected = JsonDocument.Parse(
            """{"deviceId":"device-1","command":"shell"}""");
        try
        {
            LiteWiiMContract.ReadTransportRequest(rejected.RootElement);
            throw new InvalidOperationException("przyjęto nieznane polecenie WiiM");
        }
        catch (LiteRequestException)
        {
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
