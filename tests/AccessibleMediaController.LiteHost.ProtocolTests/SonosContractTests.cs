using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class SonosContractTests
{
    public static void Run()
    {
        AccountStatusUsesOnlyExplicitUserFacingFields();
        PendingAccountStatusReturnsOnlyATrustedAuthorizeUrl();
        MissingPublicLoginServiceIsExplicitAndCannotStartLogin();
        PendingLoginExplainsTheHouseholdAccountMismatch();
        TargetLabelsNeverUseTechnicalIdentifiers();
        SnapshotUsesOnlyExplicitUserFacingFields();
        SnapshotDefaultsObservedMaterialIdentityToNull();
        SnapshotObservesOnlyMatchingOwnStreamPlaybackItem();
        TransportAcceptsOnlyTheNarrowAllowlist();
        LibraryAndMaterialLabelsNeverExposeIds();
        MaterialRequestsAcceptOnlyKnownKinds();
    }

    private static void PendingLoginExplainsTheHouseholdAccountMismatch()
    {
        var message = SonosLoginMessages.Describe(SonosLoginStatus.Pending);

        Check(message == SonosLoginMessages.PendingGuidance,
            "oczekujące logowanie nie używa wspólnej instrukcji dla użytkownika");
        Check(message.Contains("tego samego konta", StringComparison.Ordinal)
              && message.Contains("aplikacji Sonos", StringComparison.Ordinal)
              && message.Contains("rozpocznij logowanie ponownie", StringComparison.Ordinal),
            "komunikat nie wyjaśnia błędu po wyborze gospodarstwa domowego");
        Check(!message.Contains("selectHousehold", StringComparison.Ordinal)
              && !message.Contains("callback", StringComparison.OrdinalIgnoreCase)
              && !message.Contains("broker", StringComparison.OrdinalIgnoreCase),
            "komunikat dostępny ujawnił techniczny etap logowania");
    }

    private static void MissingPublicLoginServiceIsExplicitAndCannotStartLogin()
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateBrokerUnavailableResult()));
        var root = payload.RootElement;
        Check(root.GetProperty("message").GetString()
              == "Publiczna usługa logowania Sonos nie jest jeszcze skonfigurowana w tej wersji AMC.",
            "brak publicznej usługi nie ma jawnego komunikatu użytkowego");
        Check(!root.GetProperty("canLogin").GetBoolean()
              && !root.GetProperty("connected").GetBoolean()
              && root.GetProperty("authorizeUrl").ValueKind == JsonValueKind.Null,
            "brak publicznej usługi nadal pozwala rozpocząć logowanie");
        var json = root.GetRawText();
        Check(!json.Contains("hermes", StringComparison.OrdinalIgnoreCase)
              && !json.Contains("tail6caad7", StringComparison.OrdinalIgnoreCase),
            "kontrakt logowania nadal ujawnia historyczny host");
    }

    private static void AccountStatusUsesOnlyExplicitUserFacingFields()
    {
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateAccountStatusResult(
                SonosAccountSnapshots.Empty)));
        var root = payload.RootElement;
        Check(root.GetProperty("label").GetString() == "Konto Sonos nie jest połączone.",
            "stan konta nie ma jawnej etykiety użytkowej");
        Check(!root.GetProperty("connected").GetBoolean(),
            "pusta migawka została przedstawiona jako połączone konto");
        Check(!root.GetProperty("awaitingBrowser").GetBoolean(),
            "pusta migawka została przedstawiona jako oczekujące logowanie");
        var json = root.GetRawText();
        Check(!json.Contains("token", StringComparison.OrdinalIgnoreCase)
              && !json.Contains("broker", StringComparison.OrdinalIgnoreCase)
              && !json.Contains("SonosAccountState", StringComparison.Ordinal),
            "stan konta ujawnił token, brokera albo nazwę typu");
    }

    private static void PendingAccountStatusReturnsOnlyATrustedAuthorizeUrl()
    {
        var gateway = new PendingGateway();
        using var coordinator = new SonosAccountCoordinator(
            gateway,
            new EmptyStore(),
            "https://broker-testowy.invalid/");
        coordinator.RestoreOnce();
        var started = coordinator.BeginLoginAsync(default).GetAwaiter().GetResult();
        var snapshot = coordinator.ReadLoginStatus(out var address);

        using var trustedPayload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateAccountStatusResult(snapshot, address)));
        Check(trustedPayload.RootElement.GetProperty("awaitingBrowser").GetBoolean()
              && trustedPayload.RootElement.GetProperty("authorizeUrl").GetString()
                 == started.AuthorizeUri?.AbsoluteUri,
            "ponowne otwarcie konta zgubiło bezpieczny adres oczekującego logowania");

        using var untrustedPayload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateAccountStatusResult(
                snapshot, new Uri("https://example.invalid/phishing"))));
        Check(untrustedPayload.RootElement.GetProperty("authorizeUrl").ValueKind
              == JsonValueKind.Null,
            "stan konta ujawnił adres spoza zaufanej domeny Sonos");
    }

    private sealed class PendingGateway : ISonosLoginGateway
    {
        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
        {
            const string body = "{\"session_id\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\",\"authorize_url\":\"https://api.sonos.com/login/v3/oauth?client_id=synthetic&response_type=code\",\"expires_in\":600}";
            SonosLoginBrokerConfiguration.TryCreate(
                "https://broker-testowy.invalid/", out var configuration);
            using var client = new SonosLoginClient(
                configuration!,
                new StaticHandler(body));
            return client.StartAsync(cancellationToken);
        }

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Ten test nie odbiera wyniku logowania.");

        public Task<SonosRefreshOutcome> RefreshAsync(
            string? refreshToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Ten test nie odnawia tokenu.");
    }

    private sealed class StaticHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class EmptyStore : ISonosCredentialStore
    {
        public SonosCredentialReadOutcome Read() =>
            SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials) =>
            SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);

        public bool Delete() => true;
    }

    private static void LibraryAndMaterialLabelsNeverExposeIds()
    {
        using var library = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateLibraryResult()));
        Check(library.RootElement.GetProperty("items").GetArrayLength() == 3,
            "Biblioteka Sonos nie ma trzech działających kategorii");

        var favorite = new SonosFavorite(
            "technical-favorite-id", "Radio Poznań", "Audycja",
            new SonosFavoriteService("TuneIn", "7"));
        var favorites = new SonosFavoritesList(
            "technical-house-id", "version", [favorite]);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateFavoritesResult(favorites)));
        var item = payload.RootElement.GetProperty("items")[0];
        var title = item.GetProperty("title").GetString() ?? string.Empty;
        Check(title == "Radio Poznań, TuneIn, Audycja",
            "etykieta ulubionego nie używa formatera Core");
        Check(!title.Contains("technical", StringComparison.Ordinal),
            "identyfikator ulubionego wyciekł do etykiety");

        var playlist = new SonosPlaylist(
            "technical-playlist-id", "Wieczór", trackCount: 3);
        var playlists = new SonosPlaylistsList(
            "technical-house-id", null, [playlist]);
        using var playlistPayload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreatePlaylistsResult(playlists)));
        var playlistTitle = playlistPayload.RootElement
            .GetProperty("items")[0].GetProperty("title").GetString() ?? string.Empty;
        Check(playlistTitle == "Wieczór, 3 utwory",
            "etykieta playlisty nie używa formatera Core");
        Check(!playlistTitle.Contains("technical", StringComparison.Ordinal),
            "identyfikator playlisty wyciekł do etykiety");

        using var streamPayload = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateOwnStreamsResult(
                [new SonosOwnStreamSettings
                {
                    Id = "technical-stream-id",
                    Name = "Radio domowe",
                    StreamUrl = "https://radio.example/live"
                }],
                new CollectionOrderSettings(),
                CollectionSortMode.Custom)));
        var streamItem = streamPayload.RootElement.GetProperty("items")[0];
        var streamTitle = streamItem.GetProperty("title").GetString() ?? string.Empty;
        Check(streamTitle == "Radio domowe",
            "własna stacja nie zachowała nazwy użytkowej");
        Check(!streamTitle.Contains("technical", StringComparison.Ordinal),
            "identyfikator własnej stacji wyciekł do etykiety");
        Check(streamItem.GetProperty("streamUrl").GetString()
              == "https://radio.example/live",
            "własna stacja nie przekazuje adresu do jawnego polecenia kopiowania");
        Check(!streamTitle.Contains("radio.example", StringComparison.Ordinal)
              && !streamItem.GetProperty("detail").GetString()!.Contains(
                  "radio.example", StringComparison.Ordinal),
            "adres własnej stacji wyciekł do użytkowej etykiety listy");
    }

    private static void MaterialRequestsAcceptOnlyKnownKinds()
    {
        using var accepted = JsonDocument.Parse(
            """{"householdId":"home-1","kind":"favorite"}""");
        Check(LiteSonosContract.ReadMaterialsRequest(accepted.RootElement).Kind == "favorite",
            "nie przyjęto ulubionych Sonos");

        using var ownStream = JsonDocument.Parse(
            """{"householdId":"home-1","kind":"ownStream"}""");
        Check(LiteSonosContract.ReadMaterialsRequest(ownStream.RootElement).Kind == "ownStream",
            "nie przyjęto własnych stacji Sonos");

        using var rejected = JsonDocument.Parse(
            """{"householdId":"home-1","kind":"runtimeObject"}""");
        try
        {
            LiteSonosContract.ReadMaterialsRequest(rejected.RootElement);
            throw new InvalidOperationException("przyjęto nieznany rodzaj materiału Sonos");
        }
        catch (LiteRequestException)
        {
        }
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
        var playerIds = item.GetProperty("playerIds")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Check(title == "Salon", "lista nie zachowała nazwy grupy");
        Check(detail == "głośników: 2, odtwarza", "lista ma zły opis użytkowy");
        Check(!title.Contains("RINCON", StringComparison.Ordinal)
              && !detail.Contains("RINCON", StringComparison.Ordinal),
            "identyfikator grupy wyciekł do etykiety dostępnej");
        Check(!detail.Contains("player-technical", StringComparison.Ordinal),
            "identyfikator głośnika wyciekł do etykiety dostępnej");
        Check(playerIds.SequenceEqual([
                "player-technical-1", "player-technical-2"]),
            "model celu nie zachował logicznego składu dla stałego presetu");
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

    private static void SnapshotDefaultsObservedMaterialIdentityToNull()
    {
        var group = new SonosGroup(
            "group-1", "Salon", "player-1", ["player-1"], SonosPlaybackState.Idle);
        var playback = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Idle, null, null, null, null, null, null, null, null);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateSnapshotResult(
                "home-1", group, playback, null, null, DateTime.UtcNow)));
        var root = document.RootElement;
        Check(root.GetProperty("observedMaterialId").ValueKind == JsonValueKind.Null,
            "brak tożsamości Sonos musi pozostać null");
        Check(root.GetProperty("identityProvenance").ValueKind == JsonValueKind.Null,
            "brak pochodzenia tożsamości Sonos musi pozostać null");
    }

    private static void SnapshotObservesOnlyMatchingOwnStreamPlaybackItem()
    {
        var group = new SonosGroup(
            "group-1", "Salon", "player-1", ["player-1"], SonosPlaybackState.Playing);
        var ownStreams = new[]
        {
            new SonosOwnStreamSettings
            {
                Id = "stream-a", Name = "Radio A", StreamUrl = "https://radio.example/a"
            },
            new SonosOwnStreamSettings
            {
                Id = "stream-b", Name = "Radio B", StreamUrl = "https://radio.example/b"
            }
        };
        var matchingItemId = SonosOwnStreamIdentity.TryComputeItemId(
            ownStreams[1].Id, ownStreams[1].StreamUrl);
        var playing = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null, matchingItemId, null, null, null, null, null);

        using var matched = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateSnapshotResult(
                "home-1", group, playing, null, null, DateTime.UtcNow, ownStreams: ownStreams)));
        Check(matched.RootElement.GetProperty("observedMaterialId").GetString()
              == "sonos-stream:stream-b",
            "potwierdzone itemId Sonos nie wskazało własnej stacji AMC");
        Check(matched.RootElement.GetProperty("identityProvenance").GetString()
              == "playbackItemId",
            "Sonos nie podał pochodzenia playbackItemId");

        var staleA = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null,
            SonosOwnStreamIdentity.TryComputeItemId(ownStreams[0].Id, ownStreams[0].StreamUrl),
            null, null, null, null, null);
        using var stale = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateSnapshotResult(
                "home-1", group, staleA, null, null, DateTime.UtcNow, ownStreams: ownStreams)));
        Check(stale.RootElement.GetProperty("observedMaterialId").GetString()
              == "sonos-stream:stream-a",
            "odczyt A po przełączeniu nie może zgadywać ostatnio żądanego B");

        var mismatch = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Playing, null, null, "not-an-amc-stream", null, null, null, null, null);
        using var unknown = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateSnapshotResult(
                "home-1", group, mismatch, null, null, DateTime.UtcNow, ownStreams: ownStreams)));
        Check(unknown.RootElement.GetProperty("observedMaterialId").ValueKind == JsonValueKind.Null,
            "niezgodny itemId Sonos nie może dostać tożsamości AMC");

        var idle = new SonosGroupPlaybackStatus(
            SonosPlaybackState.Idle, null, null, matchingItemId, null, null, null, null, null);
        using var stopped = JsonDocument.Parse(JsonSerializer.Serialize(
            LiteSonosContract.CreateSnapshotResult(
                "home-1", group, idle, null, null, DateTime.UtcNow, ownStreams: ownStreams)));
        Check(stopped.RootElement.GetProperty("observedMaterialId").ValueKind == JsonValueKind.Null,
            "stan idle Sonos nie może ogłaszać tożsamości materiału");
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
