using System.Text.Json;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class SonosContractTests
{
    public static void Run()
    {
        TargetLabelsNeverUseTechnicalIdentifiers();
        SnapshotUsesOnlyExplicitUserFacingFields();
        TransportAcceptsOnlyTheNarrowAllowlist();
    }

    private static void TargetLabelsNeverUseTechnicalIdentifiers()
    {
        var household = new SonosHousehold("home-technical-1", "Mój dom", null);
        var group = new SonosGroup(
            "RINCON_TECHNICAL:1", "Salon", "player-technical-1",
            ["player-technical-1", "player-technical-2"],
            SonosPlaybackState.Playing);
        var topology = new SonosHouseholdTopology([group], [], partial: false);
        var payload = LiteSonosContract.CreateTargetsResult(
            [new LiteSonosHouseholdTargets(household, topology)],
            incomplete: false);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var item = document.RootElement.GetProperty("items")[0];
        var title = item.GetProperty("title").GetString() ?? string.Empty;
        var detail = item.GetProperty("detail").GetString() ?? string.Empty;
        Check(title == "Salon", "lista nie zachowała nazwy grupy");
        Check(detail == "głośników: 2, odtwarza", "lista ma zły opis użytkowy");
        Check(!title.Contains("RINCON", StringComparison.Ordinal)
              && !detail.Contains("RINCON", StringComparison.Ordinal),
            "identyfikator grupy wyciekł do etykiety dostępnej");
        Check(!detail.Contains("player-technical", StringComparison.Ordinal),
            "identyfikator głośnika wyciekł do etykiety dostępnej");
    }

    private static void SnapshotUsesOnlyExplicitUserFacingFields()
    {
        var group = new SonosGroup(
            "RINCON_TECHNICAL:1", "Salon", "player-1", ["player-1"],
            SonosPlaybackState.Playing);
        var actions = new SonosPlaybackActions(
            true, true, null, true, true, true, false,
            null, null, null, null);
        var playback = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null, "opaque-item",
            61000, null, null, null, actions);
        var track = new SonosTrackMetadata(
            "track", "Audycja", "Autor", null, null,
            new SonosMetadataService("Radio internetowe", "7"),
            600000);
        var metadata = new SonosGroupMetadata(
            null, new SonosQueueItem("queue-item", track, false),
            null, null, null);
        var volume = new SonosGroupVolume(35, false, false);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateSnapshotResult(
                "home-technical-1", group, playback, metadata, volume,
                DateTime.UtcNow)));
        var root = document.RootElement;
        Check(root.GetProperty("title").GetString() == "Audycja, Autor",
            "migawka zgubiła tytuł użytkowy");
        Check(root.GetProperty("volume").GetInt32() == 35,
            "migawka zgubiła głośność");
        Check(root.GetProperty("positionSeconds").GetDouble() >= 61,
            "migawka zgubiła pozycję");
        var message = root.GetProperty("message").GetString() ?? string.Empty;
        Check(!message.Contains("RINCON", StringComparison.Ordinal)
              && !message.Contains("opaque-item", StringComparison.Ordinal)
              && !message.Contains("queue-item", StringComparison.Ordinal),
            "techniczna tożsamość materiału wyciekła do komunikatu");
    }

    private static void TransportAcceptsOnlyTheNarrowAllowlist()
    {
        foreach (var command in new[]
                 {
                     "toggle", "previous", "next", "toggleMute",
                     "volumeUp5", "volumeDown5", "volumeUp1", "volumeDown1"
                 })
        {
            using var document = JsonDocument.Parse(
                $$"""{"householdId":"home-1","groupId":"group-1","command":"{{command}}"}""");
            Check(LiteSonosContract.ReadTransportRequest(document.RootElement).Command == command,
                $"nie przyjęto polecenia {command}");
        }

        using var volume = JsonDocument.Parse(
            """{"householdId":"home-1","groupId":"group-1","command":"setVolume","volume":37}""");
        Check(LiteSonosContract.ReadTransportRequest(volume.RootElement).Volume == 37,
            "nie przyjęto jawnej głośności");

        using var seek = JsonDocument.Parse(
            """{"householdId":"home-1","groupId":"group-1","command":"seekRelative","seconds":-30}""");
        Check(LiteSonosContract.ReadTransportRequest(seek.RootElement).Seconds == -30,
            "nie przyjęto przewinięcia względnego");

        using var rejected = JsonDocument.Parse(
            """{"householdId":"home-1","groupId":"group-1","command":"shell"}""");
        try
        {
            LiteSonosContract.ReadTransportRequest(rejected.RootElement);
            throw new InvalidOperationException("przyjęto nieznane polecenie Sonos");
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
