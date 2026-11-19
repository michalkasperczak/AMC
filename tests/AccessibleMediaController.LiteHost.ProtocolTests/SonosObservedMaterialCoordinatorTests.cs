using System.Text.Json;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class SonosObservedMaterialCoordinatorTests
{
    public static void Run()
    {
        SnapshotUsesTheFreshPlaybackItemInsteadOfTheLastRequestedStation();
        SnapshotReadsTheConfirmedOwnStreamWhenPlaybackIsPaused();
    }

    private static void SnapshotUsesTheFreshPlaybackItemInsteadOfTheLastRequestedStation()
    {
        var path = Path.Combine(Path.GetTempPath(), "amc-sonos-observed-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """
            {
              "sonos": {
                "ownStreams": [
                  { "id": "stream-a", "name": "Radio A", "streamUrl": "https://radio.example/a" },
                  { "id": "stream-b", "name": "Radio B", "streamUrl": "https://radio.example/b" }
                ]
              }
            }
            """);
        try
        {
            var backend = new FakeBackend
            {
                ItemId = SonosOwnStreamIdentity.TryComputeItemId(
                    "stream-a", "https://radio.example/a")
            };
            using var coordinator = new LiteSonosCoordinator(backend, path);
            coordinator.ListTargets();
            using var target = JsonDocument.Parse("""{"householdId":"home-1","groupId":"group-1"}""");

            using var initial = JsonDocument.Parse(JsonSerializer.Serialize(
                coordinator.Snapshot(target.RootElement)));
            Check(initial.RootElement.GetProperty("observedMaterialId").GetString()
                  == "sonos-stream:stream-a",
                "pierwszy odczyt nie użył potwierdzonego itemId A");

            backend.ItemId = SonosOwnStreamIdentity.TryComputeItemId(
                "stream-b", "https://radio.example/b");
            using var changed = JsonDocument.Parse(JsonSerializer.Serialize(
                coordinator.Snapshot(target.RootElement)));
            Check(changed.RootElement.GetProperty("observedMaterialId").GetString()
                  == "sonos-stream:stream-b",
                "odczyt po zmianie A do B użył pamiętanej intencji zamiast świeżego itemId");
            Check(changed.RootElement.GetProperty("identityProvenance").GetString()
                  == "playbackItemId",
                "koordynator nie podał pochodzenia świeżej tożsamości");

            backend.ItemId = "other-provider-item";
            using var unknown = JsonDocument.Parse(JsonSerializer.Serialize(
                coordinator.Snapshot(target.RootElement)));
            Check(unknown.RootElement.GetProperty("observedMaterialId").ValueKind == JsonValueKind.Null,
                "nieznany itemId nie może dziedziczyć tożsamości ostatniej stacji Sonos");
            Check(backend.PlaybackReadCount == 3,
                "koordynator nie wykonał osobnego rzeczywistego odczytu dla każdej migawki");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void SnapshotReadsTheConfirmedOwnStreamWhenPlaybackIsPaused()
    {
        var path = Path.Combine(Path.GetTempPath(), "amc-sonos-observed-paused-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """
            {
              "sonos": {
                "ownStreams": [
                  { "id": "stream-a", "name": "Radio A", "streamUrl": "https://radio.example/a" }
                ]
              }
            }
            """);
        try
        {
            var backend = new FakeBackend
            {
                PlaybackState = SonosPlaybackState.Paused,
                ItemId = SonosOwnStreamIdentity.TryComputeItemId(
                    "stream-a", "https://radio.example/a")
            };
            using var coordinator = new LiteSonosCoordinator(backend, path);
            coordinator.ListTargets();
            using var target = JsonDocument.Parse("""{"householdId":"home-1","groupId":"group-1"}""");

            using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(
                coordinator.Snapshot(target.RootElement)));
            Check(snapshot.RootElement.GetProperty("observedMaterialId").GetString()
                  == "sonos-stream:stream-a",
                "koordynator w pauzie nie użył potwierdzonego ItemId z rzeczywistego odczytu");
            Check(snapshot.RootElement.GetProperty("identityProvenance").GetString()
                  == "playbackItemId",
                "koordynator w pauzie zgubił pochodzenie rzeczywistego ItemId");
            Check(backend.PlaybackReadCount == 1,
                "koordynator nie wykonał rzeczywistego odczytu odtwarzania dla migawki pauzy");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        internal string? ItemId { get; set; }

        internal SonosPlaybackState PlaybackState { get; set; } = SonosPlaybackState.Playing;

        internal int PlaybackReadCount { get; private set; }

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReadCount++;
            return Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(
                new SonosGroupPlaybackStatus(
                    PlaybackState, null, null, ItemId, null, null, null, null, null)));
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(
                new SonosGroupMetadata(null, null, null, null, null)));

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(
                new SonosGroupVolume(30, false, false)));

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosHouseholdsReadResult.Success(
                [new SonosHousehold("home-1", "Dom", null)]));

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [new SonosGroup("group-1", "Salon", "player-1", ["player-1"], SonosPlaybackState.Playing)],
                [new SonosPlayer("player-1", "Salon", null, null, null)],
                partial: false)));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
