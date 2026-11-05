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
        PresetListUsesOnlyIntentionalLabels();
        PresetActivationAcceptsOnlyTwelveSlots();
        SourceNavigationAcceptsOnlyOneStepDirections();
        OptionsUseOnlyIntentionalLabelsAndStrictValues();
        NetworkStreamListKeepsUrlOutOfLabels();
        NetworkStreamSnapshotUsesTheSavedName();
        NetworkStreamSortContractIsStrictAndPreservesStoredOrder();
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
            new WiiMTrackInformation("Audycja", "Radio", "", 48000, 24)
            { BitrateKbps = 192 },
            []);

        var json = JsonSerializer.Serialize(
            LiteWiiMContract.CreateSnapshotResult(device, snapshot));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.GetProperty("title").GetString() == "Audycja", "brak tytułu");
        Check(root.GetProperty("volume").GetInt32() == 35, "brak głośności");
        Check(root.GetProperty("positionSeconds").GetDouble() == 61, "brak pozycji");
        Check(root.GetProperty("bitrateKbps").GetInt32() == 192, "brak bitrate");
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

    private static void PresetListUsesOnlyIntentionalLabels()
    {
        var device = new WiiMDeviceSettings
        {
            Id = "device-1",
            Address = "192.168.1.40",
            DisplayName = "Salon",
            LastActivatedPresetNumber = 2
        };
        var playback = new WiiMPlaybackInformation(
            "odtwarzanie", "radio internetowe", 35, false,
            TimeSpan.Zero, TimeSpan.Zero)
        {
            ContentUri = "https://radio.example/live"
        };
        var snapshot = new WiiMDeviceSnapshot(
            new WiiMDeviceInformation(
                "192.168.1.40", "device-1", "Salon", "Pro", "4.8", 12),
            playback,
            new WiiMTrackInformation("Radio Test", "", "", 0, 0),
            [
                new WiiMPresetInformation(
                    2, "Radio Test", "radio internetowe", "https://radio.example/live"),
                new WiiMPresetInformation(5, "TIDAL", "TIDAL Connect", null),
                new WiiMPresetInformation(8, "", "", null)
            ]);

        var json = JsonSerializer.Serialize(
            LiteWiiMContract.CreatePresetsResult(device, snapshot));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var items = root.GetProperty("items");
        Check(items.GetArrayLength() == 12, "lista nie zawiera dwunastu miejsc");
        Check(root.GetProperty("currentPresetNumber").GetInt32() == 2,
            "nie rozpoznano bieżącego presetu");
        Check(items[0].GetProperty("label").GetString() == "Preset 1, pusty",
            "puste miejsce nie ma celowej etykiety");
        Check(items[1].GetProperty("label").GetString()
            == "Preset 2, Radio Test, radio internetowe",
            "zajęty preset nie ma celowej etykiety");
        Check(items[1].GetProperty("name").GetString() == "Radio Test"
            && items[1].GetProperty("source").GetString() == "radio internetowe",
            "mapowanie skrótu nie dostało jawnych tekstów użytkowych");
        Check(items[4].GetProperty("activationError").GetString()!
            .StartsWith("Preset 5 to TIDAL Connect", StringComparison.Ordinal),
            "nie zachowano uczciwej odmowy dla presetu Connect");
        Check(items[7].GetProperty("label").GetString() == "Preset 8, bez nazwy",
            "zajęty preset bez nazwy nie ma celowej etykiety");
        Check(!json.Contains("192.168.1.40", StringComparison.Ordinal),
            "adres urządzenia wyciekł z listy presetów");
        Check(!json.Contains("https://radio.example/live", StringComparison.Ordinal),
            "adres strumienia wyciekł z listy presetów");
        Check(!json.Contains("WiiMPresetInformation", StringComparison.Ordinal),
            "techniczny typ presetu wyciekł do odpowiedzi");
    }

    private static void PresetActivationAcceptsOnlyTwelveSlots()
    {
        using var accepted = JsonDocument.Parse(
            """{"deviceId":"device-1","presetNumber":12}""");
        var request = LiteWiiMContract.ReadPresetRequest(accepted.RootElement);
        Check(request.DeviceId == "device-1" && request.PresetNumber == 12,
            "nie przyjęto prawidłowego numeru presetu");

        foreach (var rejectedNumber in new[] { 0, 13 })
        {
            using var rejected = JsonDocument.Parse(
                $$"""{"deviceId":"device-1","presetNumber":{{rejectedNumber}}}""");
            try
            {
                LiteWiiMContract.ReadPresetRequest(rejected.RootElement);
                throw new InvalidOperationException(
                    $"przyjęto numer presetu {rejectedNumber} poza zakresem");
            }
            catch (LiteRequestException)
            {
            }
        }
    }

    private static void SourceNavigationAcceptsOnlyOneStepDirections()
    {
        foreach (var direction in new[] { -1, 1 })
        {
            using var accepted = JsonDocument.Parse(
                $$"""{"deviceId":"device-1","direction":{{direction}}}""");
            var request = LiteWiiMContract.ReadSourceNavigationRequest(
                accepted.RootElement);
            Check(
                request.DeviceId == "device-1" && request.Direction == direction,
                "nie przyjęto prawidłowego kierunku źródła WiiM");
        }

        foreach (var direction in new[] { -2, 0, 2 })
        {
            using var rejected = JsonDocument.Parse(
                $$"""{"deviceId":"device-1","direction":{{direction}}}""");
            try
            {
                LiteWiiMContract.ReadSourceNavigationRequest(rejected.RootElement);
                throw new InvalidOperationException(
                    $"przyjęto nieprawidłowy kierunek źródła {direction}");
            }
            catch (LiteRequestException)
            {
            }
        }
    }

    private static void OptionsUseOnlyIntentionalLabelsAndStrictValues()
    {
        var device = new WiiMDeviceSettings
        {
            Id = "device-1", Address = "192.168.1.40", DisplayName = "Salon"
        };
        var playback = new WiiMPlaybackInformation(
            "odtwarzanie", "Bluetooth", 35, false,
            TimeSpan.Zero, TimeSpan.Zero)
        {
            EqualizerPresetNumber = 1,
            LoopMode = 2
        };
        var snapshot = new WiiMDeviceSnapshot(
            new WiiMDeviceInformation(
                "192.168.1.40", "device-1", "Salon", "Pro", "4.8", 12),
            playback,
            new WiiMTrackInformation("Radio Test", "", "", 0, 0),
            []);

        var inputJson = JsonSerializer.Serialize(
            LiteWiiMContract.CreateOptionsResult(device, snapshot, "input"));
        using var inputDocument = JsonDocument.Parse(inputJson);
        var input = inputDocument.RootElement;
        Check(input.GetProperty("title").GetString() == "Wejście urządzenia WiiM",
            "brak użytkowego tytułu wejścia WiiM");
        Check(input.GetProperty("selectedValue").GetString() == "bluetooth",
            "nie rozpoznano bieżącego wejścia");
        Check(input.GetProperty("items")[0].GetProperty("label").GetString()
              == "Odtwarzanie sieciowe",
            "lista wejść nie ma jawnej etykiety");
        Check(!inputJson.Contains("192.168.1.40", StringComparison.Ordinal),
            "adres urządzenia wyciekł do dialogu opcji");
        Check(!inputJson.Contains("WiiMPlaybackInformation", StringComparison.Ordinal),
            "typ modelu wyciekł do dialogu opcji");

        var equalizerJson = JsonSerializer.Serialize(
            LiteWiiMContract.CreateOptionsResult(
                device,
                snapshot,
                "equalizer",
                equalizerEnabled: true,
                equalizerPresets: ["Rock", "Klasyczny"]));
        using var equalizerDocument = JsonDocument.Parse(equalizerJson);
        var equalizer = equalizerDocument.RootElement;
        Check(equalizer.GetProperty("selectedValue").GetString() == "Klasyczny",
            "nie rozpoznano bieżącego ustawienia korektora");
        Check(equalizer.GetProperty("items")[0].GetProperty("label").GetString()
              == "Korektor wyłączony",
            "brak jawnej etykiety wyłączenia korektora");

        using var accepted = JsonDocument.Parse(
            """{"deviceId":"device-1","option":"sleep","value":"1800"}""");
        var request = LiteWiiMContract.ReadOptionRequest(
            accepted.RootElement, requireValue: true);
        Check(request.Option == "sleep" && request.Value == "1800",
            "nie przyjęto prawidłowej opcji WiiM");

        using var rejected = JsonDocument.Parse(
            """{"deviceId":"device-1","option":"runtimeObject","value":"x"}""");
        try
        {
            LiteWiiMContract.ReadOptionRequest(rejected.RootElement, requireValue: true);
            throw new InvalidOperationException("przyjęto techniczną nieznaną opcję WiiM");
        }
        catch (LiteRequestException)
        {
        }

        Check(LiteWiiMContract.OptionLabel("sleep", "1800")
              == "Wyłącz odtwarzanie za 30 minut",
            "ustawienie timera nie ma celowej zapowiedzi");
        Check(LiteWiiMContract.OptionLabel("output", "99").Length == 0,
            "nieznana wartość wyjścia dostała mylącą etykietę");
    }

    private static void NetworkStreamListKeepsUrlOutOfLabels()
    {
        var settings = new WiiMSettings
        {
            NetworkStreams =
            [
                new WiiMNetworkStreamSettings
                {
                    Id = "stream-1",
                    Name = "Radio Poznań",
                    StreamUrl = "https://secret.example/live.m3u8",
                    AddedUtcTicks = 20
                },
                new WiiMNetworkStreamSettings
                {
                    Id = "stream-2",
                    Name = "Radio Opole",
                    StreamUrl = "https://secret.example/opole.mp3",
                    AddedUtcTicks = 10
                }
            ]
        };

        var json = JsonSerializer.Serialize(
            LiteWiiMContract.CreateNetworkStreamsResult(settings));
        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("items");
        Check(items.GetArrayLength() == 2, "brak zapisanych strumieni WiiM");
        Check(items[0].GetProperty("title").GetString() == "Radio Poznań",
            "nie zachowano kolejności dodania strumieni");
        Check(items[0].GetProperty("detail").GetString() == "Strumień sieciowy",
            "brak użytkowego rodzaju strumienia");
        Check(items[0].GetProperty("streamUrl").GetString()
              == "https://secret.example/live.m3u8",
            "lista nie przekazuje adresu do jawnego polecenia kopiowania");
        Check(!items[0].GetProperty("title").GetString()!.Contains(
                "secret.example", StringComparison.Ordinal)
              && !items[0].GetProperty("detail").GetString()!.Contains(
                "secret.example", StringComparison.Ordinal),
            "adres strumienia wyciekł do użytkowej etykiety listy");

        using var requestJson = JsonDocument.Parse(
            """{"deviceId":"device-1","streamId":"stream-1"}""");
        var request = LiteWiiMContract.ReadNetworkStreamRequest(
            requestJson.RootElement);
        Check(request.DeviceId == "device-1" && request.StreamId == "stream-1",
            "nie odczytano bezpiecznych identyfikatorów strumienia");
    }

    private static void NetworkStreamSnapshotUsesTheSavedName()
    {
        var device = new WiiMDeviceSettings
        {
            Id = "device-1", Address = "192.168.1.40", DisplayName = "Salon"
        };
        var snapshot = new WiiMDeviceSnapshot(
            new WiiMDeviceInformation(
                "192.168.1.40", "device-1", "Salon", "Pro", "4.8", 12),
            new WiiMPlaybackInformation(
                "odtwarzanie", "https://secret.example/live.m3u8", 35, false,
                TimeSpan.Zero, TimeSpan.Zero),
            new WiiMTrackInformation(
                "https://secret.example/live.m3u8", "", "", 0, 0),
            []);

        var json = JsonSerializer.Serialize(LiteWiiMContract.CreateSnapshotResult(
            device,
            snapshot,
            networkStreamTitle: "Radio Poznań"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.GetProperty("title").GetString() == "Radio Poznań",
            "migawka nie użyła zapisanej nazwy strumienia");
        Check(root.GetProperty("message").GetString()!.StartsWith(
                "Radio Poznań", StringComparison.Ordinal),
            "komunikat migawki nie użył zapisanej nazwy strumienia");
        Check(!json.Contains("secret.example", StringComparison.Ordinal),
            "adres strumienia wyciekł z migawki lub komunikatu");
    }

    private static void NetworkStreamSortContractIsStrictAndPreservesStoredOrder()
    {
        var settings = new WiiMSettings
        {
            NetworkStreams =
            [
                new WiiMNetworkStreamSettings
                {
                    Id = "stream-1", Name = "Pierwszy",
                    StreamUrl = "https://radio.invalid/1"
                },
                new WiiMNetworkStreamSettings
                {
                    Id = "stream-2", Name = "Drugi",
                    StreamUrl = "https://radio.invalid/2"
                }
            ]
        };
        var json = JsonSerializer.Serialize(
            LiteWiiMContract.CreateNetworkStreamsResult(
                settings,
                CollectionSortMode.Custom,
                ["stream-2", "stream-1"]));
        using var result = JsonDocument.Parse(json);
        Check(result.RootElement.GetProperty("sortMode").GetString() == "Custom",
            "lista nie podaje aktywnego użytkowego trybu sortowania");
        Check(result.RootElement.GetProperty("items")[0]
                .GetProperty("streamId").GetString() == "stream-2",
            "kolejność własna nie została zachowana");

        using var accepted = JsonDocument.Parse("""{"mode":"Alphabetical"}""");
        Check(LiteWiiMContract.ReadNetworkStreamSortMode(accepted.RootElement)
              == CollectionSortMode.Alphabetical,
            "kontrakt odrzucił prawidłowy tryb sortowania");
        using var rejected = JsonDocument.Parse("""{"mode":"RuntimeObject"}""");
        try
        {
            LiteWiiMContract.ReadNetworkStreamSortMode(rejected.RootElement);
            throw new InvalidOperationException("przyjęto techniczny nieznany tryb");
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
