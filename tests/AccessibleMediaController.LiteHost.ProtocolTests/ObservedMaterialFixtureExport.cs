using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Devices.WiiM;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class ObservedMaterialFixtureExport
{
    public static void Write(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Podaj ścieżkę pliku fixture obserwowanego materiału.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Nie można ustalić katalogu pliku fixture.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, CreateJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string CreateJson() => JsonSerializer.Serialize(
        new { cases = CreateCases() },
        new JsonSerializerOptions { WriteIndented = true });

    private static object[] CreateCases()
    {
        var wiimDevice = new WiiMDeviceSettings
        {
            Id = "device-1",
            Address = "192.168.1.40",
            DisplayName = "Salon"
        };
        var wiimStreamA = new WiiMNetworkStreamSettings
        {
            Id = "saved-a",
            Name = "Radio A",
            StreamUrl = "https://radio-a.example.invalid/live",
            AddedUtcTicks = 20
        };
        var wiimStreamB = new WiiMNetworkStreamSettings
        {
            Id = "saved-b",
            Name = "Radio B",
            StreamUrl = "https://radio-b.example.invalid/live",
            AddedUtcTicks = 10
        };
        var wiimStreams = new[] { wiimStreamA, wiimStreamB };
        var wiimAmbiguousStreams = new[]
        {
            wiimStreamA,
            new WiiMNetworkStreamSettings
            {
                Id = "saved-duplicate",
                Name = "Radio A (duplikat)",
                StreamUrl = "https://radio-a.example.invalid/live#fragment",
                AddedUtcTicks = 5
            }
        };

        var sonosStreamA = new SonosOwnStreamSettings
        {
            Id = "stream-a",
            Name = "Radio A",
            StreamUrl = "https://radio-a.example.invalid/live"
        };
        var sonosStreamB = new SonosOwnStreamSettings
        {
            Id = "stream-b",
            Name = "Radio B",
            StreamUrl = "https://radio-b.example.invalid/live"
        };
        var sonosStreams = new[] { sonosStreamA, sonosStreamB };
        var sonosAmbiguousStreams = new[]
        {
            sonosStreamA,
            new SonosOwnStreamSettings
            {
                Id = "stream-a",
                Name = "Radio A (duplikat)",
                StreamUrl = "https://radio-a.example.invalid/live"
            }
        };
        var sonosItemA = SonosOwnStreamIdentity.TryComputeItemId(
            sonosStreamA.Id, sonosStreamA.StreamUrl);

        return
        [
            WiiMCase("wiim-playing", "odtwarzanie", wiimStreams,
                "https://radio-a.example.invalid/live", "wiim-stream:saved-a"),
            WiiMCase("wiim-paused", "pauza", wiimStreams,
                "https://radio-a.example.invalid/live", "wiim-stream:saved-a"),
            WiiMCase("wiim-idle", "zatrzymane", wiimStreams,
                "https://radio-a.example.invalid/live", null),
            WiiMCase("wiim-mismatch", "odtwarzanie", wiimStreams,
                "https://other.example.invalid/live", null),
            WiiMCase("wiim-ambiguous", "odtwarzanie", wiimAmbiguousStreams,
                "https://radio-a.example.invalid/live", null),
            SonosCase("sonos-playing", SonosPlaybackState.Playing, sonosStreams,
                sonosItemA, "sonos-stream:stream-a"),
            SonosCase("sonos-paused", SonosPlaybackState.Paused, sonosStreams,
                sonosItemA, "sonos-stream:stream-a"),
            SonosCase("sonos-idle", SonosPlaybackState.Idle, sonosStreams,
                sonosItemA, null),
            SonosCase("sonos-mismatch", SonosPlaybackState.Playing, sonosStreams,
                "other-provider-item", null),
            SonosCase("sonos-ambiguous", SonosPlaybackState.Playing, sonosAmbiguousStreams,
                sonosItemA, null)
        ];

        object WiiMCase(
            string name,
            string state,
            IReadOnlyList<WiiMNetworkStreamSettings> streams,
            string contentUri,
            string? expectedMaterialId)
        {
            var settings = new WiiMSettings { NetworkStreams = streams.ToList() };
            var snapshot = new WiiMDeviceSnapshot(
                new WiiMDeviceInformation("192.168.1.40", "device-1", "Salon", "Pro", "4.8", 12),
                new WiiMPlaybackInformation(state, "radio", 35, false, TimeSpan.Zero, TimeSpan.Zero)
                {
                    ContentUri = contentUri
                },
                new WiiMTrackInformation("Program", "", "", 0, 0),
                []);
            return new
            {
                name,
                session = "WIIM",
                deviceId = "device-1",
                householdId = (string?)null,
                groupId = (string?)null,
                listPayload = LiteWiiMContract.CreateNetworkStreamsResult(settings),
                snapshot = LiteWiiMContract.CreateSnapshotResult(
                    wiimDevice, snapshot, networkStreams: streams),
                expectedMaterialId
            };
        }

        object SonosCase(
            string name,
            SonosPlaybackState state,
            IReadOnlyList<SonosOwnStreamSettings> streams,
            string? itemId,
            string? expectedMaterialId)
        {
            var group = new SonosGroup(
                "group-1", "Salon", "player-1", ["player-1"], state);
            var playback = new SonosGroupPlaybackStatus(
                state, null, null, itemId, null, null, null, null, null);
            return new
            {
                name,
                session = "SONOS",
                deviceId = (string?)null,
                householdId = "home-1",
                groupId = "group-1",
                listPayload = LiteSonosContract.CreateOwnStreamsResult(
                    streams,
                    new CollectionOrderSettings(),
                    CollectionSortMode.Custom),
                snapshot = LiteSonosContract.CreateSnapshotResult(
                    "home-1", group, playback, null, null, DateTime.UtcNow, ownStreams: streams),
                expectedMaterialId
            };
        }
    }
}
