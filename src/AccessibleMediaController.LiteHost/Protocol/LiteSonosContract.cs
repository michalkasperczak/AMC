using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
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
    public const string AccountStatusOperation = "sonos.accountStatus";
    public const string AccountLoginStartOperation = "sonos.accountLoginStart";
    public const string AccountLoginCheckOperation = "sonos.accountLoginCheck";
    public const string AccountLoginCancelOperation = "sonos.accountLoginCancel";
    public const string AccountRefreshOperation = "sonos.accountRefresh";
    public const string AccountPersistRetryOperation = "sonos.accountPersistRetry";
    public const string AccountDisconnectOperation = "sonos.accountDisconnect";
    public const string SnapshotOperation = "sonos.snapshot";
    public const string TransportOperation = "sonos.transport";
    public const string LibraryOperation = "sonos.library";
    public const string MaterialsOperation = "sonos.materials";
    public const string LoadMaterialOperation = "sonos.loadMaterial";
    public const string OwnStreamEditInfoOperation = "sonos.ownStreamEditInfo";
    public const string AddOwnStreamOperation = "sonos.ownStreamAdd";
    public const string EditOwnStreamOperation = "sonos.ownStreamEdit";
    public const string RemoveOwnStreamsOperation = "sonos.ownStreamRemove";
    public const string ImportOwnStreamsOperation = "sonos.ownStreamImport";
    public const string SortOwnStreamsOperation = "sonos.ownStreamSort";
    public const string MoveOwnStreamsOperation = "sonos.ownStreamMove";
    public const string PlaceOwnStreamsOperation = "sonos.ownStreamPlace";
    public const string ExportOwnStreamsOperation = "sonos.ownStreamExport";

    public static object CreateAccountStatusResult(
        SonosAccountSnapshot snapshot,
        Uri? pendingAuthorizeUri = null)
    {
        var authorizeUrl = snapshot.IsAwaitingBrowser
            ? TrustedAuthorizeUrl(pendingAuthorizeUri)
            : null;
        return CreateAccountActionResult(
            snapshot, DescribeAccountSnapshot(snapshot), authorizeUrl);
    }

    public static object CreateBrokerUnavailableResult() => new
    {
        label = SonosIntegrationDefaults.BrokerUnavailableMessage,
        message = SonosIntegrationDefaults.BrokerUnavailableMessage,
        connected = false,
        awaitingBrowser = false,
        canLogin = false,
        canRefresh = false,
        canRetryPersist = false,
        canDisconnect = false,
        authorizeUrl = (string?)null
    };

    public static object CreateAccountLoginStartResult(
        SonosAccountLoginStartResult result)
    {
        var authorizeUrl = result.Started
            ? TrustedAuthorizeUrl(result.AuthorizeUri)
            : null;
        var message = authorizeUrl is not null
            ? "Adres logowania Sonos jest gotowy. Otwórz go i zaloguj się, a potem wybierz Sprawdź logowanie."
            : result.Message;
        return CreateAccountActionResult(
            result.Snapshot, message, authorizeUrl);
    }

    public static object CreateAccountLoginCheckResult(
        SonosAccountLoginCheckResult result)
    {
        var message = !result.HadPendingLogin
            ? "Nie ma rozpoczętego logowania Sonos. Wybierz Rozpocznij logowanie."
            : result.Discarded
                ? "Ta próba logowania Sonos została zastąpiona nowszą."
                : result.Connected
                    ? "Konto Sonos zostało połączone."
                    : result.Message;
        return CreateAccountActionResult(result.Snapshot, message);
    }

    public static object CreateAccountRefreshResult(
        SonosAccountRefreshResult result)
    {
        var message = result.Renewed
            ? "Odnowiono dostęp do konta Sonos."
            : result.WaiterCanceled
                ? "Odświeżanie dostępu Sonos zostało przerwane."
                : result.Message;
        return CreateAccountActionResult(result.Snapshot, message);
    }

    public static object CreateAccountPersistRetryResult(
        SonosAccountPersistRetryResult result)
    {
        var message = result.Succeeded
            ? "Zapisano konto Sonos."
            : !result.Attempted
                ? "Konto Sonos nie wymaga ponownego zapisu."
                : "Nie udało się zapisać konta Sonos. Konto może nadal działać do zamknięcia AMC.";
        return CreateAccountActionResult(result.Snapshot, message);
    }

    public static object CreateAccountDisconnectResult(
        SonosAccountDisconnectResult result)
    {
        var message = result.Disconnected
            ? "Wylogowano konto Sonos."
            : "Konto Sonos odłączono z pamięci, ale zapis mógł pozostać na dysku. Spróbuj wylogować ponownie.";
        return CreateAccountActionResult(result.Snapshot, message);
    }

    public static object CreateAccountActionResult(
        SonosAccountSnapshot snapshot,
        string message,
        string? authorizeUrl = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new
        {
            label = DescribeAccountSnapshot(snapshot),
            message = Useful(message),
            connected = snapshot.HasCredentials,
            awaitingBrowser = snapshot.IsAwaitingBrowser,
            canLogin = true,
            canRefresh = snapshot.HasCredentials && snapshot.HasRefreshToken,
            canRetryPersist = snapshot.CanRetryPersist,
            canDisconnect = snapshot.HasCredentials || snapshot.PersistedRecordMayRemain,
            authorizeUrl
        };
    }

    private static string DescribeAccountSnapshot(SonosAccountSnapshot snapshot)
    {
        var parts = new List<string> { snapshot.Message };
        if (!string.IsNullOrWhiteSpace(snapshot.IssueMessage))
            parts.Add(snapshot.IssueMessage);
        if (snapshot.HasCredentials && !snapshot.IsPersisted)
            parts.Add("Konto działa, ale nie zostało zapisane. Możesz ponowić sam zapis.");
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string? TrustedAuthorizeUrl(Uri? authorizeUri) =>
        authorizeUri is not null
        && SonosAuthorizeUrlPolicy.IsTrustedAuthorizeUrl(authorizeUri.AbsoluteUri)
            ? authorizeUri.AbsoluteUri
            : null;

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

    public static LiteSonosMaterialsRequest ReadMaterialsRequest(JsonElement args)
    {
        var householdId = LiteArgs.RequireText(args, "householdId").Trim();
        var kind = LiteArgs.RequireText(args, "kind").Trim();
        if (kind is not ("favorite" or "playlist" or "ownStream"))
            throw new LiteRequestException("Nieznany rodzaj materiału Sonos.");
        return new LiteSonosMaterialsRequest(householdId, kind);
    }

    public static string ReadOwnStreamId(JsonElement args) =>
        LiteArgs.RequireText(args, "streamId").Trim();

    public static CollectionSortMode ReadOwnStreamSortMode(JsonElement args)
    {
        var text = LiteArgs.RequireText(args, "mode").Trim();
        if (Enum.TryParse<CollectionSortMode>(text, ignoreCase: true, out var mode)
            && Enum.IsDefined(mode))
            return mode;
        throw new LiteRequestException(
            "Nieznany sposób sortowania własnych stacji Sonos.");
    }

    public static int ReadOwnStreamMoveDirection(JsonElement args)
    {
        if (args.TryGetProperty("direction", out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var direction)
            && direction is -1 or 1)
            return direction;
        throw new LiteRequestException(
            "Kierunek przenoszenia stacji musi wynosić minus jeden albo jeden.");
    }

    public static LiteSonosMaterialLoadRequest ReadMaterialLoadRequest(JsonElement args)
    {
        var target = ReadTarget(args);
        var material = ReadMaterialsRequest(args);
        var materialId = LiteArgs.RequireText(args, "materialId").Trim();
        return new LiteSonosMaterialLoadRequest(
            target.HouseholdId, target.GroupId, material.Kind, materialId);
    }

    public static object CreateLibraryResult()
    {
        var items = SonosLibraryPresentation.DescribeCategories()
            .Select(row => new
            {
                itemId = row.CategoryId,
                title = row.Name,
                detail = row.Hint,
                kind = row.CategoryId switch
                {
                    SonosLibraryPresentation.FavoritesCategoryId => "favorites",
                    SonosLibraryPresentation.PlaylistsCategoryId => "playlists",
                    SonosLibraryPresentation.OwnStreamsCategoryId => "ownStreams",
                    _ => throw new InvalidOperationException(
                        "Nieznana kategoria Biblioteki Sonos.")
                }
            })
            .ToArray();
        return new { items, message = "Biblioteka Sonos" };
    }

    public static object CreateFavoritesResult(SonosFavoritesList favorites)
    {
        var items = favorites.Items.Select(item => new
        {
            itemId = "sonos-favorite:" + item.Id,
            materialId = item.Id,
            title = SonosFavoritesLabels.Describe(item),
            kind = "favorite"
        }).ToArray();
        return new { items, message = SonosFavoritesLabels.SummarizeCount(items.Length) };
    }

    public static object CreatePlaylistsResult(SonosPlaylistsList playlists)
    {
        var items = playlists.Items.Select(item => new
        {
            itemId = "sonos-playlist:" + item.Id,
            materialId = item.Id,
            title = SonosPlaylistsLabels.Describe(item),
            kind = "playlist"
        }).ToArray();
        return new { items, message = SonosPlaylistsLabels.SummarizeCount(items.Length) };
    }

    public static object CreateOwnStreamsResult(
        IReadOnlyList<SonosOwnStreamSettings> streams,
        CollectionOrderSettings orders,
        CollectionSortMode mode)
    {
        var valid = streams
            .Where(stream => !string.IsNullOrWhiteSpace(stream.Id)
                && !string.IsNullOrWhiteSpace(SonosOwnStreamsOrder.Label(stream))
                && SonosStreamUrlPolicy.IsAcceptable(stream.StreamUrl))
            .DistinctBy(stream => stream.Id.Trim(), StringComparer.Ordinal)
            .ToArray();
        var items = SonosOwnStreamsOrder.Arrange(orders, valid, mode)
            .Select(stream => new
            {
                itemId = "sonos-stream:" + stream.Id.Trim(),
                materialId = stream.Id.Trim(),
                title = SonosOwnStreamsOrder.Label(stream).Trim(),
                detail = "Stacja zapisana w AMC",
                kind = "ownStream",
                // Jawna dana dla Ctrl+Shift+C; nie jest częścią etykiety listy.
                streamUrl = stream.StreamUrl.Trim()
            })
            .ToArray();
        return new
        {
            items,
            message = DescribeOwnStreamsCount(items.Length),
            sortMode = mode.ToString()
        };
    }

    private static string DescribeOwnStreamsCount(int count) => count switch
    {
        0 => "Moje stacje, zero elementów",
        1 => "Moje stacje, 1 element",
        _ when count % 10 is >= 2 and <= 4 && count % 100 is not (12 or 13 or 14) =>
            $"Moje stacje, {count} elementy",
        _ => $"Moje stacje, {count} elementów"
    };

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
                var group = source.Topology.Groups.First(candidate => string.Equals(
                    candidate.Id, row.GroupId, StringComparison.Ordinal));
                var state = group.PlaybackState;
                var detail = (multipleHouseholds ? householdTitle + ", " : string.Empty)
                    + "głośników: " + row.PlayerCount
                    + ", " + SonosSessionListPresentation.DescribeState(state);
                items.Add(new
                {
                    itemId = "sonos:" + source.Household.Id + ":" + row.GroupId,
                    householdId = source.Household.Id,
                    groupId = row.GroupId,
                    playerIds = group.PlayerIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray(),
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
        string? message = null,
        IReadOnlyList<SonosOwnStreamSettings>? ownStreams = null)
    {
        var position = SonosPlayerPosition.Resolve(
            playback, metadata?.CurrentTrack?.DurationMillis, readUtc, DateTime.UtcNow);
        var view = SonosPlayerPresentation.Describe(playback, metadata, volume, position);
        var actions = playback?.AvailablePlaybackActions;
        var observed = ResolveObservedOwnStream(ownStreams, playback);
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
            observedMaterialId = observed is null ? null : "sonos-stream:" + observed.Id,
            identityProvenance = observed is null ? null : "playbackItemId",
            message = string.IsNullOrWhiteSpace(message)
                ? SnapshotAnnouncement(group, view)
                : message.Trim()
        };
    }

    private static SonosOwnStreamSettings? ResolveObservedOwnStream(
        IReadOnlyList<SonosOwnStreamSettings>? streams,
        SonosGroupPlaybackStatus? playback)
    {
        if (streams is null
            || playback?.PlaybackState is not (SonosPlaybackState.Playing or SonosPlaybackState.Paused)
            || string.IsNullOrWhiteSpace(playback.ItemId))
            return null;
        var matches = streams.Where(stream =>
            !string.IsNullOrWhiteSpace(stream.Id)
            && SonosStreamUrlPolicy.IsAcceptable(stream.StreamUrl)
            && string.Equals(
                SonosOwnStreamIdentity.TryComputeItemId(stream.Id, stream.StreamUrl),
                playback.ItemId,
                StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
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

public sealed record LiteSonosMaterialsRequest(string HouseholdId, string Kind);

public sealed record LiteSonosMaterialLoadRequest(
    string HouseholdId,
    string GroupId,
    string Kind,
    string MaterialId);

public sealed record LiteSonosHouseholdTargets(
    SonosHousehold Household,
    SonosHouseholdTopology Topology);
