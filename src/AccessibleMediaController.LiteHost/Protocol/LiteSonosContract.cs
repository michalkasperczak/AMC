using System.Text.Json;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Wąski kontrakt sesji Sonos. Identyfikatory są danymi modelu i nigdy nie
/// stają się etykietą dostępną; tokeny, klucz API oraz adres brokera nie
/// przechodzą przez protokół w żadnym kierunku.
/// </summary>
public static class LiteSonosContract
{
    public const string TargetsOperation = "sonos.targets";
    public const string SnapshotOperation = "sonos.snapshot";
    public const string TransportOperation = "sonos.transport";

    public static LiteSonosTargetRequest ReadTarget(JsonElement args) => new(
        LiteArgs.RequireText(args, "householdId").Trim(),
        LiteArgs.RequireText(args, "groupId").Trim());

    public static LiteSonosTransportRequest ReadTransportRequest(JsonElement args)
    {
        var target = ReadTarget(args);
        var command = LiteArgs.RequireText(args, "command").Trim();
        if (command is not (
                "toggle" or "previous" or "next" or "toggleMute"
                or "setVolume" or "volumeUp5" or "volumeDown5"
                or "volumeUp1" or "volumeDown1" or "seekRelative"))
        {
            throw new LiteRequestException("Nieznane polecenie sterowania Sonos.");
        }

        int? volume = null;
        int? seconds = null;
        if (command == "setVolume")
        {
            volume = RequireInt(args, "volume", 0, 100,
                "Głośność Sonos musi być liczbą od 0 do 100.");
        }
        if (command == "seekRelative")
        {
            seconds = RequireInt(args, "seconds", -43200, 43200,
                "Przewinięcie Sonos musi mieścić się w zakresie 12 godzin.");
            if (seconds == 0)
                throw new LiteRequestException("Czas przewinięcia Sonos nie może wynosić zero.");
        }
        return new LiteSonosTransportRequest(
            target.HouseholdId, target.GroupId, command, volume, seconds);
    }

    public static object CreateTargetsResult(
        IReadOnlyList<LiteSonosHouseholdTargets> sources,
        bool incomplete,
        string? message = null)
    {
        var multipleHouseholds = sources.Count > 1;
        var items = new List<object>();
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var householdTitle = HouseholdTitle(source.Household, index + 1);
            foreach (var row in SonosSessionListPresentation.DescribeGroups(source.Topology))
            {
                var state = source.Topology.Groups.First(group => string.Equals(
                    group.Id, row.GroupId, StringComparison.Ordinal)).PlaybackState;
                var detail = (multipleHouseholds ? householdTitle + ", " : string.Empty)
                    + "głośników: " + row.PlayerCount
                    + ", " + SonosSessionListPresentation.DescribeState(state);
                items.Add(new
                {
                    itemId = "sonos:" + source.Household.Id + ":" + row.GroupId,
                    householdId = source.Household.Id,
                    groupId = row.GroupId,
                    title = row.Name,
                    detail
                });
            }
        }
        return new
        {
            items = items.ToArray(),
            incomplete,
            message = Useful(message)
        };
    }

    public static object CreateSnapshotResult(
        string householdId,
        SonosGroup group,
        SonosGroupPlaybackStatus? playback,
        SonosGroupMetadata? metadata,
        SonosGroupVolume? volume,
        DateTime readUtc,
        string? message = null)
    {
        var position = SonosPlayerPosition.Resolve(
            playback, metadata?.CurrentTrack?.DurationMillis, readUtc, DateTime.UtcNow);
        var view = SonosPlayerPresentation.Describe(playback, metadata, volume, position);
        var actions = playback?.AvailablePlaybackActions;
        return new
        {
            itemId = "sonos:" + householdId + ":" + group.Id,
            householdId,
            groupId = group.Id,
            groupTitle = GroupTitle(group),
            title = view.Title,
            source = view.Source,
            stateText = view.StateText,
            volumeText = view.VolumeText,
            positionText = view.PositionText,
            playbackState = PlaybackState(playback?.PlaybackState),
            volume = volume?.Volume,
            muted = volume?.Muted,
            fixedVolume = volume?.FixedVolume,
            positionSeconds = position.Position?.TotalSeconds,
            durationSeconds = position.Duration?.TotalSeconds,
            canPlay = actions?.CanPlay,
            canPause = actions?.CanPause,
            canStop = actions?.CanStop,
            canNext = actions?.CanSkip,
            canPrevious = actions?.SkipToPreviousAllowed,
            canSeek = actions?.CanSeek,
            message = string.IsNullOrWhiteSpace(message)
                ? SnapshotAnnouncement(group, view)
                : message.Trim()
        };
    }

    private static string SnapshotAnnouncement(SonosGroup group, SonosPlayerView view)
    {
        var parts = new List<string>();
        AddDistinct(parts, view.Title);
        AddDistinct(parts, view.Source.Replace("Źródło: ", string.Empty, StringComparison.Ordinal));
        AddDistinct(parts, view.StateText.Replace("Stan: ", string.Empty, StringComparison.Ordinal));
        AddDistinct(parts, view.VolumeText.Replace("Głośność: ", string.Empty, StringComparison.Ordinal));
        if (parts.Count == 0) parts.Add(GroupTitle(group));
        return string.Join(", ", parts);
    }

    private static string HouseholdTitle(SonosHousehold household, int ordinal) =>
        string.IsNullOrWhiteSpace(household.Name)
            ? $"Dom Sonos {ordinal}"
            : household.Name.Trim();

    private static string GroupTitle(SonosGroup group) =>
        string.IsNullOrWhiteSpace(group.Name) ? "Grupa Sonos" : group.Name.Trim();

    private static string PlaybackState(SonosPlaybackState? state) => state switch
    {
        SonosPlaybackState.Playing => "playing",
        SonosPlaybackState.Paused => "paused",
        SonosPlaybackState.Buffering => "buffering",
        SonosPlaybackState.Idle => "idle",
        _ => "unknown"
    };

    private static int RequireInt(
        JsonElement args, string name, int minimum, int maximum, string error)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var raw)
            && raw.ValueKind == JsonValueKind.Number
            && raw.TryGetInt32(out var value)
            && value >= minimum && value <= maximum)
        {
            return value;
        }
        throw new LiteRequestException(error);
    }

    private static void AddDistinct(List<string> values, string? value)
    {
        var text = Useful(value);
        if (text.Length == 0
            || text == SonosPlayerPresentation.Unknown
            || values.Any(existing => string.Equals(
                existing, text, StringComparison.CurrentCultureIgnoreCase))) return;
        values.Add(text);
    }

    private static string Useful(string? value) => value?.Trim() ?? string.Empty;
}

public sealed record LiteSonosTargetRequest(string HouseholdId, string GroupId);

public sealed record LiteSonosTransportRequest(
    string HouseholdId,
    string GroupId,
    string Command,
    int? Volume,
    int? Seconds);

public sealed record LiteSonosHouseholdTargets(
    SonosHousehold Household,
    SonosHouseholdTopology Topology);
