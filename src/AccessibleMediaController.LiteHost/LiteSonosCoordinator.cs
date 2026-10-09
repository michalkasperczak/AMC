using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Serializuje odczyty i polecenia sesji Sonos. Po każdym POST wykonuje jawny
/// odczyt, nigdy nie ponawia polecenia i nie nazywa HTTP 200 dowodem skutku.
/// </summary>
internal sealed class LiteSonosCoordinator(
    ISonosGroupSessionBackend backend) : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, LiteSonosTarget> targets = new(StringComparer.Ordinal);
    private long? accountBindingGeneration;

    public object ListTargets()
    {
        lock (gate) return ReadTargets();
    }

    public object Snapshot(System.Text.Json.JsonElement args)
    {
        var request = LiteSonosContract.ReadTarget(args);
        lock (gate)
        {
            var target = ResolveTarget(request);
            var state = ReadState(target.Group.Id);
            if (!state.AnySucceeded)
                throw new LiteRequestException(state.FirstFailure);
            return LiteSonosContract.CreateSnapshotResult(
                target.Household.Id,
                target.Group,
                state.Playback,
                state.Metadata,
                state.Volume,
                state.ReadUtc,
                state.CombinedFailure);
        }
    }

    public object Transport(System.Text.Json.JsonElement args)
    {
        var request = LiteSonosContract.ReadTransportRequest(args);
        lock (gate)
        {
            var target = ResolveTarget(new LiteSonosTargetRequest(
                request.HouseholdId, request.GroupId));
            var before = ReadState(target.Group.Id);
            if (!before.AnySucceeded)
                throw new LiteRequestException(before.FirstFailure);

            var gateResult = EvaluateGate(request, before);
            if (!gateResult.Allowed)
                throw new LiteRequestException(
                    gateResult.Refusal ?? "To polecenie nie jest teraz dostępne w Sonos.");

            SonosGroupCommandResult result;
            SonosVerdictCommand verdictCommand = SonosVerdictCommand.Toggle;
            int? requestedVolume = null;
            bool? requestedMute = null;
            switch (request.Command)
            {
                case "toggle":
                {
                    var pausing = before.Playback?.PlaybackState is
                        SonosPlaybackState.Playing or SonosPlaybackState.Buffering;
                    var stopping = pausing
                        && SonosTransportLabels.IsStopControl(
                            before.Playback?.AvailablePlaybackActions);
                    verdictCommand = pausing
                        ? stopping ? SonosVerdictCommand.Stop : SonosVerdictCommand.Pause
                        : SonosVerdictCommand.Play;
                    result = backend.SendGroupCommandAsync(
                        target.Group.Id,
                        pausing ? SonosGroupCommand.Pause : SonosGroupCommand.Play,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                }
                case "previous":
                    verdictCommand = SonosVerdictCommand.Previous;
                    result = backend.SendGroupCommandAsync(
                        target.Group.Id, SonosGroupCommand.SkipToPreviousTrack,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                case "next":
                    verdictCommand = SonosVerdictCommand.Next;
                    result = backend.SendGroupCommandAsync(
                        target.Group.Id, SonosGroupCommand.SkipToNextTrack,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                case "toggleMute":
                    requestedMute = !before.Volume!.Muted!.Value;
                    verdictCommand = SonosVerdictCommand.Mute;
                    result = backend.SetGroupMuteAsync(
                        target.Group.Id, requestedMute.Value,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                case "setVolume":
                    requestedVolume = request.Volume!.Value;
                    verdictCommand = SonosVerdictCommand.Volume;
                    result = backend.SetGroupVolumeAsync(
                        target.Group.Id, requestedVolume.Value,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                case "volumeUp5":
                case "volumeDown5":
                case "volumeUp1":
                case "volumeDown1":
                    requestedVolume = AdjustedVolume(before.Volume!, request.Command);
                    verdictCommand = SonosVerdictCommand.Volume;
                    result = backend.SetGroupVolumeAsync(
                        target.Group.Id, requestedVolume.Value,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                case "seekRelative":
                    verdictCommand = SonosVerdictCommand.Seek;
                    result = backend.SeekRelativeAsync(
                        target.Group.Id,
                        checked(request.Seconds!.Value * 1000),
                        before.Playback?.ItemId,
                        CancellationToken.None).GetAwaiter().GetResult();
                    break;
                default:
                    throw new LiteRequestException("Nieznane polecenie sterowania Sonos.");
            }

            // Polecenia nigdy nie ponawiamy. Krótka pauza daje chmurze szansę
            // opublikować stan, po czym wykonujemy jeden jawny odczyt.
            Thread.Sleep(TimeSpan.FromMilliseconds(220));
            var after = result.StateReadRecommended
                ? ReadState(target.Group.Id)
                : LiteSonosState.Empty(result.Message);
            var message = DescribeVerdict(
                result, verdictCommand, before, after,
                requestedVolume, requestedMute);
            return LiteSonosContract.CreateSnapshotResult(
                target.Household.Id,
                target.Group,
                after.Playback,
                after.Metadata,
                after.Volume,
                after.ReadUtc,
                message);
        }
    }

    private object ReadTargets()
    {
        var households = backend.ReadHouseholdsAsync(CancellationToken.None)
            .GetAwaiter().GetResult();
        if (!households.Succeeded)
            throw new LiteRequestException(households.Message);

        targets.Clear();
        accountBindingGeneration = households.Snapshot.AccountBindingGeneration;
        var sources = new List<LiteSonosHouseholdTargets>();
        var incomplete = false;
        string? failure = null;
        foreach (var household in households.Households)
        {
            var groups = backend.ReadGroupsAsync(household.Id, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!groups.Succeeded || groups.Topology is null)
            {
                incomplete = true;
                failure ??= groups.Message;
                continue;
            }
            sources.Add(new LiteSonosHouseholdTargets(household, groups.Topology));
            foreach (var group in groups.Topology.Groups)
            {
                targets[Key(household.Id, group.Id)] = new LiteSonosTarget(household, group);
            }
            if (groups.Topology.Partial) incomplete = true;
        }

        var message = failure;
        if (households.Households.Count == 0)
        {
            message = SonosSessionListPresentation.DescribeEmptyState(
                SonosSessionEmptyReason.NoHouseholds);
        }
        else if (targets.Count == 0 && !incomplete)
        {
            message = SonosSessionListPresentation.DescribeEmptyState(
                SonosSessionEmptyReason.NoGroups);
        }
        else if (incomplete && string.IsNullOrWhiteSpace(message))
        {
            message = "Lista grup Sonos jest niepełna. Odśwież ją później.";
        }
        return LiteSonosContract.CreateTargetsResult(sources, incomplete, message);
    }

    private LiteSonosTarget ResolveTarget(LiteSonosTargetRequest request)
    {
        if (!SonosHouseholdIdPolicy.IsAcceptable(request.HouseholdId)
            || !SonosGroupIdPolicy.IsAcceptable(request.GroupId))
        {
            throw new LiteRequestException("Nie można rozpoznać wybranej grupy Sonos.");
        }
        InvalidateTargetsAfterAccountChange();
        if (targets.Count == 0) ReadTargets();
        if (!targets.TryGetValue(Key(request.HouseholdId, request.GroupId), out var target))
            throw new LiteRequestException(
                "Wybrana grupa Sonos nie jest już dostępna. Odśwież listę grup.");
        return target;
    }

    private void InvalidateTargetsAfterAccountChange()
    {
        if (backend is not ISonosAccountBoundBackend bound
            || bound.AccountSnapshot is not { } snapshot
            || accountBindingGeneration is null
            || snapshot.AccountBindingGeneration == accountBindingGeneration) return;
        targets.Clear();
        accountBindingGeneration = snapshot.AccountBindingGeneration;
    }

    private LiteSonosState ReadState(string groupId)
    {
        var playback = backend.ReadGroupPlaybackAsync(groupId, CancellationToken.None)
            .GetAwaiter().GetResult();
        var metadata = backend.ReadGroupMetadataAsync(groupId, CancellationToken.None)
            .GetAwaiter().GetResult();
        var volume = backend.ReadGroupVolumeAsync(groupId, CancellationToken.None)
            .GetAwaiter().GetResult();
        return new LiteSonosState(
            playback.Succeeded ? playback.Value : null,
            metadata.Succeeded ? metadata.Value : null,
            volume.Succeeded ? volume.Value : null,
            DateTime.UtcNow,
            playback.Succeeded,
            metadata.Succeeded,
            volume.Succeeded,
            FirstFailure(playback, metadata, volume));
    }

    private static string FirstFailure(
        SonosGroupReadResult<SonosGroupPlaybackStatus> playback,
        SonosGroupReadResult<SonosGroupMetadata> metadata,
        SonosGroupReadResult<SonosGroupVolume> volume)
    {
        if (!playback.Succeeded) return playback.Message;
        if (!metadata.Succeeded) return metadata.Message;
        if (!volume.Succeeded) return volume.Message;
        return string.Empty;
    }

    private static SonosCommandGate EvaluateGate(
        LiteSonosTransportRequest request,
        LiteSonosState before)
    {
        var commandId = request.Command switch
        {
            "toggle" => CommandIds.PlayPause,
            "previous" => CommandIds.Previous,
            "next" => CommandIds.Next,
            "toggleMute" => CommandIds.ToggleMuteCurrentSession,
            "volumeDown5" => CommandIds.VolumeDown5,
            "volumeUp1" => CommandIds.VolumeUp1,
            "volumeDown1" => CommandIds.VolumeDown1,
            "seekRelative" when request.Seconds < 0 => CommandIds.SeekBackward10,
            "seekRelative" => CommandIds.SeekForward10,
            _ => CommandIds.VolumeUp5
        };
        return SonosCommandGating.Evaluate(
            commandId,
            before.Playback?.PlaybackState ?? SonosPlaybackState.Unknown,
            before.Playback?.AvailablePlaybackActions,
            before.Volume);
    }

    private static int AdjustedVolume(SonosGroupVolume volume, string command) =>
        Math.Clamp(volume.Volume + command switch
        {
            "volumeUp5" => 5,
            "volumeDown5" => -5,
            "volumeUp1" => 1,
            "volumeDown1" => -1,
            _ => 0
        }, 0, 100);

    private static string DescribeVerdict(
        SonosGroupCommandResult result,
        SonosVerdictCommand command,
        LiteSonosState before,
        LiteSonosState after,
        int? requestedVolume,
        bool? requestedMute)
    {
        if (!result.StateReadRecommended) return result.Message;
        if (command is SonosVerdictCommand.Volume or SonosVerdictCommand.Mute)
        {
            return SonosCommandVerdict.DescribeVolume(
                result.Accepted,
                after.VolumeSucceeded,
                before.Volume,
                after.Volume,
                requestedVolume,
                requestedMute).Text;
        }
        return SonosCommandVerdict.Describe(
            command,
            result.Accepted,
            after.PlaybackSucceeded,
            before.Playback?.PlaybackState ?? SonosPlaybackState.Unknown,
            after.Playback?.PlaybackState ?? SonosPlaybackState.Unknown,
            before.Playback?.ItemId,
            after.Playback?.ItemId).Text;
    }

    private static string Key(string householdId, string groupId) =>
        householdId + "\n" + groupId;

    public void Dispose()
    {
        if (backend is IDisposable disposable) disposable.Dispose();
    }
}

internal sealed record LiteSonosTarget(SonosHousehold Household, SonosGroup Group);

internal sealed record LiteSonosState(
    SonosGroupPlaybackStatus? Playback,
    SonosGroupMetadata? Metadata,
    SonosGroupVolume? Volume,
    DateTime ReadUtc,
    bool PlaybackSucceeded,
    bool MetadataSucceeded,
    bool VolumeSucceeded,
    string FirstFailure)
{
    public bool AnySucceeded => PlaybackSucceeded || MetadataSucceeded || VolumeSucceeded;

    public string? CombinedFailure =>
        PlaybackSucceeded && MetadataSucceeded && VolumeSucceeded
            ? null
            : string.IsNullOrWhiteSpace(FirstFailure)
                ? "Nie udało się odczytać pełnego stanu Sonos."
                : FirstFailure;

    public static LiteSonosState Empty(string message) => new(
        null, null, null, DateTime.UtcNow,
        false, false, false, message);
}
