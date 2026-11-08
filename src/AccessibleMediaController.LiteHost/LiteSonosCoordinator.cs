using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost.Protocol;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AccessibleMediaController.LiteHost;

internal interface ILiteSonosAccountBackend
{
    SonosAccountSnapshot ReadAccountSnapshot(out Uri? pendingAuthorizeUri);

    Task<SonosAccountLoginStartResult> BeginLoginAsync(
        CancellationToken cancellationToken);

    Task<SonosAccountLoginCheckResult> CheckLoginAsync(
        CancellationToken cancellationToken);

    SonosAccountSnapshot CancelPendingLogin();

    Task<SonosAccountRefreshResult> RefreshAccountAsync(
        CancellationToken cancellationToken);

    SonosAccountPersistRetryResult RetryPersist();

    SonosAccountDisconnectResult DisconnectAccount();
}

/// <summary>
/// Serializuje odczyty i polecenia sesji Sonos. Po każdym POST wykonuje jawny
/// odczyt, nigdy nie ponawia polecenia i nie nazywa HTTP 200 dowodem skutku.
/// </summary>
internal sealed class LiteSonosCoordinator(
    ISonosGroupSessionBackend backend,
    string? statePath = null,
    bool loginBrokerAvailable = true) : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object gate = new();
    private readonly Dictionary<string, LiteSonosTarget> targets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SonosHousehold> households = new(StringComparer.Ordinal);
    private long? accountBindingGeneration;
    private readonly string ownStreamAppContext = Guid.NewGuid().ToString("N");

    public object ListTargets()
    {
        RequireLoginBroker();
        lock (gate) return ReadTargets();
    }

    public object AccountStatus()
    {
        if (!loginBrokerAvailable)
            return LiteSonosContract.CreateBrokerUnavailableResult();

        var snapshot = RequireAccountBackend()
            .ReadAccountSnapshot(out var pendingAuthorizeUri);
        return LiteSonosContract.CreateAccountStatusResult(
            snapshot, pendingAuthorizeUri);
    }

    public object BeginAccountLogin() =>
        loginBrokerAvailable
            ? LiteSonosContract.CreateAccountLoginStartResult(
                RequireAccountBackend().BeginLoginAsync(CancellationToken.None)
                    .GetAwaiter().GetResult())
            : LiteSonosContract.CreateBrokerUnavailableResult();

    public object CheckAccountLogin() =>
        loginBrokerAvailable
            ? LiteSonosContract.CreateAccountLoginCheckResult(
                RequireAccountBackend().CheckLoginAsync(CancellationToken.None)
                    .GetAwaiter().GetResult())
            : LiteSonosContract.CreateBrokerUnavailableResult();

    public object CancelAccountLogin() =>
        loginBrokerAvailable
            ? LiteSonosContract.CreateAccountActionResult(
                RequireAccountBackend().CancelPendingLogin(),
                "Logowanie Sonos zostało anulowane.")
            : LiteSonosContract.CreateBrokerUnavailableResult();

    public object RefreshAccount() =>
        loginBrokerAvailable
            ? LiteSonosContract.CreateAccountRefreshResult(
                RequireAccountBackend().RefreshAccountAsync(CancellationToken.None)
                    .GetAwaiter().GetResult())
            : LiteSonosContract.CreateBrokerUnavailableResult();

    public object RetryAccountPersist() =>
        loginBrokerAvailable
            ? LiteSonosContract.CreateAccountPersistRetryResult(
                RequireAccountBackend().RetryPersist())
            : LiteSonosContract.CreateBrokerUnavailableResult();

    public object DisconnectAccount() =>
        loginBrokerAvailable
            ? LiteSonosContract.CreateAccountDisconnectResult(
                RequireAccountBackend().DisconnectAccount())
            : LiteSonosContract.CreateBrokerUnavailableResult();

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

    public object Library() => LiteSonosContract.CreateLibraryResult();

    public object Materials(System.Text.Json.JsonElement args)
    {
        var request = LiteSonosContract.ReadMaterialsRequest(args);
        lock (gate)
        {
            if (request.Kind == "ownStream")
            {
                var profile = ReadOwnStreamsProfile();
                return LiteSonosContract.CreateOwnStreamsResult(
                    profile.Streams, profile.Orders, profile.SortMode);
            }
            // Własne stacje są częścią profilu AMC, nie konta Sonos. Dopiero
            // ulubione i playlisty wymagają domu znanego zapleczu chmurowemu.
            // Dzięki temu lokalny adapter UPnP może zachować tę samą listę
            // także wtedy, gdy użytkownik nigdy nie łączył konta OAuth.
            ResolveHousehold(request.HouseholdId);
            if (request.Kind == "favorite")
            {
                if (backend is not ISonosFavoritesSessionBackend favoritesBackend)
                    throw new LiteRequestException("Odczyt ulubionych Sonos nie jest dostępny.");
                var result = favoritesBackend.ReadFavoritesAsync(
                        request.HouseholdId, CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (!result.Succeeded || result.Favorites is null)
                    throw new LiteRequestException(result.Message);
                return LiteSonosContract.CreateFavoritesResult(result.Favorites);
            }

            if (backend is not ISonosPlaylistsSessionBackend playlistsBackend)
                throw new LiteRequestException("Odczyt playlist Sonos nie jest dostępny.");
            var playlists = playlistsBackend.ReadPlaylistsAsync(
                    request.HouseholdId, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!playlists.Succeeded || playlists.Playlists is null)
                throw new LiteRequestException(playlists.Message);
            return LiteSonosContract.CreatePlaylistsResult(playlists.Playlists);
        }
    }

    public object LoadMaterial(System.Text.Json.JsonElement args)
    {
        var request = LiteSonosContract.ReadMaterialLoadRequest(args);
        lock (gate)
        {
            var target = ResolveTarget(new LiteSonosTargetRequest(
                request.HouseholdId, request.GroupId));
            SonosGroupCommandResult result;
            if (request.Kind == "ownStream")
            {
                if (backend is not ISonosOwnStreamsSessionBackend ownStreamsBackend)
                    throw new LiteRequestException("Uruchamianie własnych stacji Sonos nie jest dostępne.");
                var station = ReadOwnStreamsProfile().Streams.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, request.MaterialId, StringComparison.Ordinal));
                if (station is null
                    || !SonosStreamUrlPolicy.IsAcceptable(station.StreamUrl))
                {
                    throw new LiteRequestException(
                        "Nie można odnaleźć tej stacji na zapisanej liście AMC.");
                }
                if (!SonosSessionRequest.TryCreate(
                        "pl.amc.accessiblemultimediacontroller",
                        ownStreamAppContext,
                        out var sessionRequest)
                    || sessionRequest is null)
                {
                    throw new LiteRequestException(
                        "Nie przygotowano żądania własnej stacji Sonos.");
                }
                var created = ownStreamsBackend.CreateSessionAsync(
                        target.Group.Id, sessionRequest, CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (!created.TryGetSessionId(out var sessionId))
                    throw new LiteRequestException(created.Message);
                var loaded = ownStreamsBackend.LoadStreamUrlAsync(
                        sessionId,
                        station.StreamUrl,
                        playOnCompletion: true,
                        SonosOwnStreamIdentity.TryComputeItemId(
                            station.Id, station.StreamUrl),
                        CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (!loaded.Accepted)
                    throw new LiteRequestException(loaded.Message);
                Thread.Sleep(TimeSpan.FromMilliseconds(220));
                var ownStreamState = ReadState(target.Group.Id);
                return LiteSonosContract.CreateSnapshotResult(
                    target.Household.Id,
                    target.Group,
                    ownStreamState.Playback,
                    ownStreamState.Metadata,
                    ownStreamState.Volume,
                    ownStreamState.ReadUtc,
                    $"Uruchamianie: {SonosOwnStreamsOrder.Label(station).Trim()}");
            }
            if (request.Kind == "favorite")
            {
                if (backend is not ISonosFavoriteLoadSessionBackend favoritesBackend)
                    throw new LiteRequestException("Uruchamianie ulubionych Sonos nie jest dostępne.");
                result = favoritesBackend.LoadFavoriteAsync(
                        target.Group.Id, request.MaterialId,
                        SonosFavoriteQueueAction.Insert, playOnCompletion: true,
                        CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            else
            {
                if (backend is not ISonosPlaylistLoadSessionBackend playlistsBackend)
                    throw new LiteRequestException("Uruchamianie playlist Sonos nie jest dostępne.");
                result = playlistsBackend.LoadPlaylistAsync(
                        target.Group.Id, request.MaterialId,
                        SonosFavoriteQueueAction.Insert, playOnCompletion: true,
                        CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            if (!result.Accepted)
                throw new LiteRequestException(result.Message);
            Thread.Sleep(TimeSpan.FromMilliseconds(220));
            var after = ReadState(target.Group.Id);
            return LiteSonosContract.CreateSnapshotResult(
                target.Household.Id,
                target.Group,
                after.Playback,
                after.Metadata,
                after.Volume,
                after.ReadUtc,
                request.Kind == "favorite"
                    ? "Przyjęto polecenie uruchomienia ulubionego Sonos."
                    : "Przyjęto polecenie uruchomienia playlisty Sonos.");
        }
    }

    private object ReadTargets()
    {
        var householdResult = backend.ReadHouseholdsAsync(CancellationToken.None)
            .GetAwaiter().GetResult();
        if (!householdResult.Succeeded)
            throw new LiteRequestException(householdResult.Message);

        targets.Clear();
        households.Clear();
        accountBindingGeneration = householdResult.Snapshot.AccountBindingGeneration;
        var sources = new List<LiteSonosHouseholdTargets>();
        var incomplete = false;
        string? failure = null;
        foreach (var household in householdResult.Households)
        {
            this.households[household.Id] = household;
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
        if (householdResult.Households.Count == 0)
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

    private SonosHousehold ResolveHousehold(string householdId)
    {
        if (!SonosHouseholdIdPolicy.IsAcceptable(householdId))
            throw new LiteRequestException("Nie można rozpoznać wybranego domu Sonos.");
        InvalidateTargetsAfterAccountChange();
        if (households.Count == 0) ReadTargets();
        if (!households.TryGetValue(householdId, out var household))
            throw new LiteRequestException(
                "Wybrany dom Sonos nie jest już dostępny. Odśwież listę grup.");
        return household;
    }

    private LiteSonosOwnStreamsProfile ReadOwnStreamsProfile()
    {
        if (string.IsNullOrWhiteSpace(statePath))
            throw new LiteRequestException(
                "Host nie ma dostępu do profilu AMC z własnymi stacjami Sonos.");
        try
        {
            using var stream = new FileStream(
                statePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var settings = new SonosSessionSettings();
            if (TryGetProperty(root, out var sonos, "sonos", "Sonos")
                && sonos.ValueKind == JsonValueKind.Object)
            {
                settings = JsonSerializer.Deserialize<SonosSessionSettings>(
                    sonos.GetRawText(), JsonOptions) ?? new SonosSessionSettings();
            }
            var orders = new CollectionOrderSettings();
            if (TryGetProperty(root, out var rawOrders, "collectionOrders", "CollectionOrders")
                && rawOrders.ValueKind == JsonValueKind.Object)
            {
                orders = JsonSerializer.Deserialize<CollectionOrderSettings>(
                    rawOrders.GetRawText(), JsonOptions) ?? new CollectionOrderSettings();
            }
            // Stare albo ręcznie poprawiane profile mogą zawierać jawne null.
            // Logika kolejności Core słusznie zakłada już znormalizowany model;
            // host tylko do odczytu musi więc odtworzyć te dwa magazyny, których
            // używają Moje stacje, zanim przekaże go do Arrange.
            orders.LibraryAddedItemIdsBySession ??=
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            orders.LibraryItemIdsBySession ??=
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            return new LiteSonosOwnStreamsProfile(
                settings.OwnStreams ?? [],
                orders,
                ReadOwnStreamsSortMode(root));
        }
        catch (FileNotFoundException)
        {
            throw new LiteRequestException(
                "Nie znaleziono profilu AMC z własnymi stacjami Sonos.");
        }
        catch (JsonException)
        {
            throw new LiteRequestException(
                "Profil AMC z własnymi stacjami Sonos jest uszkodzony.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new LiteRequestException(
                "Nie można teraz odczytać własnych stacji Sonos z profilu AMC.");
        }
    }

    private static CollectionSortMode ReadOwnStreamsSortMode(JsonElement root)
    {
        if (TryGetProperty(root, out var navigation, "sessionNavigation", "SessionNavigation")
            && TryGetProperty(navigation, out var sessions, "sessions", "Sessions")
            && TryGetProperty(sessions, out var sonos, "sonos", "Sonos")
            && TryGetProperty(sonos, out var modes, "collectionSortModes", "CollectionSortModes")
            && TryGetProperty(modes, out var value, SonosOwnStreamsOrder.ViewName))
        {
            if (value.ValueKind == JsonValueKind.String
                && Enum.TryParse<CollectionSortMode>(
                    value.GetString(), ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
                return parsed;
            if (value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out var number)
                && Enum.IsDefined(typeof(CollectionSortMode), number))
                return (CollectionSortMode)number;
        }
        return CollectionSortMode.Custom;
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

    private void InvalidateTargetsAfterAccountChange()
    {
        if (backend is not ISonosAccountBoundBackend bound
            || bound.AccountSnapshot is not { } snapshot
            || accountBindingGeneration is null
            || snapshot.AccountBindingGeneration == accountBindingGeneration) return;
        targets.Clear();
        households.Clear();
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

    private ILiteSonosAccountBackend RequireAccountBackend() =>
        backend as ILiteSonosAccountBackend
        ?? throw new LiteRequestException(
            "Zarządzanie kontem Sonos nie jest dostępne w tym profilu AMC.");

    private void RequireLoginBroker()
    {
        if (!loginBrokerAvailable)
            throw new LiteRequestException(
                SonosIntegrationDefaults.BrokerUnavailableMessage);
    }

    public void Dispose()
    {
        if (backend is IDisposable disposable) disposable.Dispose();
    }
}

internal sealed record LiteSonosTarget(SonosHousehold Household, SonosGroup Group);

internal sealed record LiteSonosOwnStreamsProfile(
    IReadOnlyList<SonosOwnStreamSettings> Streams,
    CollectionOrderSettings Orders,
    CollectionSortMode SortMode);

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
