using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT domow, grup i glosnikow PRZEZ KONTO. Wylacznie dane syntetyczne,
/// atrapa bramki logowania i atrapa magazynu; zero sieci, kont i GUI.
///
/// Co jest mierzone prawdziwym kodem produkcyjnym:
///   * pusty dom i partial=true nie sa cicho gubione,
///   * brak konta nie wysyla zadnego zapytania,
///   * HTTP leci POZA blokada koordynatora (Snapshot z DRUGIEGO watku w trakcie),
///   * wynik po Disconnect, po nowym logowaniu i po Dispose jest PORZUCONY,
///   * 401 daje najwyzej JEDNO odnowienie i JEDNO powtorzenie GET,
///   * 403/429/5xx/Canceled NIE kasuja konta,
///   * nieznany termin waznosci NIE wywoluje odnowienia.
/// </summary>
internal static class SonosDeviceReadTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("brak konta: zero zapytan", NoAccountSendsNothing),
            ("domy: pusta lista jest sukcesem", EmptyHouseholdsIsSuccess),
            ("domy: nazwy i brak nazwy", HouseholdNames),
            ("grupy: partial i glosniki", GroupsPartial),
            ("HTTP poza blokada: Snapshot z drugiego watku", SnapshotDuringRead),
            ("po Disconnect wynik porzucony", DiscardedAfterDisconnect),
            ("po Dispose wynik porzucony", DiscardedAfterDispose),
            ("stary 401 nie odnawia NOWEGO konta", StaleUnauthorizedNeverTouchesNewAccount),
            ("stary wygasly odczyt nie odnawia NOWEGO konta", StaleExpiredReadNeverTouchesNewAccount),
            ("nieznana waznosc nie odnawia", UnknownExpiryDoesNotRenew),
            ("znana miniona waznosc: jedno odnowienie", ExpiredRenewsOnce),
            ("401: jedno odnowienie i jedno powtorzenie", UnauthorizedRenewsOnceAndRetriesOnce),
            ("401 bez tokenu odswiezania: bez odnowienia", UnauthorizedWithoutRefreshToken),
            ("403 nie kasuje konta", () => TerminalKeepsAccount(HttpStatusCode.Forbidden, SonosDeviceReadStatus.Forbidden)),
            ("429 nie kasuje konta", () => TerminalKeepsAccount((HttpStatusCode)429, SonosDeviceReadStatus.RateLimited)),
            ("503 nie kasuje konta", () => TerminalKeepsAccount(HttpStatusCode.ServiceUnavailable, SonosDeviceReadStatus.ServiceError)),
            ("anulowanie nie kasuje konta", CanceledKeepsAccount),
            ("komunikaty bez tokenu i klucza", MessagesHideSecrets)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Odczyt urzadzen Sonos przez konto: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Odczyt urzadzen Sonos: " + failures + " nieudanych testow.");
        }
    }

    // ================= przypadki =================

    private static void NoAccountSendsNothing()
    {
        using var fixture = Fixture.WithoutAccount(_ => Json("{}"));
        var result = fixture.ReadHouseholds();
        Check(result.Status == SonosDeviceReadStatus.NoAccount);
        Check(result.Households.Count == 0 && fixture.Requests == 0);
    }

    private static void EmptyHouseholdsIsSuccess()
    {
        foreach (var body in new[] { "{}", "{\"households\":null}", "{\"households\":[]}" })
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.ReadHouseholds();
            Check(result.Succeeded && result.Households.Count == 0 && fixture.Requests == 1);
        }
    }

    private static void HouseholdNames()
    {
        using var fixture = Fixture.Connected(_ =>
            Json("{\"households\":[{\"id\":\"Sonos_1.a\",\"name\":\"Dom\"},{\"id\":\"Sonos_2\"}]}"));
        var result = fixture.ReadHouseholds();
        Check(result.Succeeded && result.Households.Count == 2);
        Check(result.Households[0].HasName && result.Households[0].Name == "Dom");
        Check(!result.Households[1].HasName && result.Households[1].Name is null);
    }

    private static void GroupsPartial()
    {
        using var fixture = Fixture.Connected(_ => Json(GroupBody));
        var result = fixture.ReadGroups("Sonos_1.a");
        Check(result.Succeeded);
        var topology = result.Topology!;
        Check(topology.Partial);
        Check(topology.Groups.Count == 1 && topology.Players.Count == 2);
        Check(topology.Groups[0].PlaybackState == SonosPlaybackState.Playing);
        Check(topology.Groups[0].PlayerIds.Count == 2);

        using var empty = Fixture.Connected(_ => Json("{\"groups\":null,\"players\":[],\"partial\":null}"));
        var emptyResult = empty.ReadGroups("Sonos_1.a");
        Check(emptyResult.Succeeded);
        var emptyTopology = emptyResult.Topology!;
        Check(emptyTopology.IsEmpty && !emptyTopology.Partial);
    }

    private static void SnapshotDuringRead()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var readGeneration = -1L;
        using var fixture = Fixture.Connected(_ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            return Json("{\"households\":[]}");
        });

        // Odczyt startuje W TLE: atrapa transportu blokuje swoj watek, wiec
        // gdyby szedl na watku testu, nic nie daloby sie zmierzyc w trakcie.
        var task = StartHouseholds(fixture);
        Check(entered.Wait(TimeSpan.FromSeconds(5)));

        // DRUGI watek czyta migawke, gdy zapytanie HTTP jest w toku. Gdyby
        // odczyt trzymal blokade koordynatora, to by sie zawiesilo.
        var probe = Task.Run(() => readGeneration = fixture.Coordinator.Snapshot.CredentialGeneration);
        Check(probe.Wait(TimeSpan.FromSeconds(3)));
        Check(readGeneration >= 0);

        release.Set();
        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Succeeded);
    }

    private static void DiscardedAfterDisconnect()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var fixture = Fixture.Connected(_ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            return Json("{\"households\":[{\"id\":\"Sonos_1.a\",\"name\":\"Dom\"}]}");
        });

        var task = StartHouseholds(fixture);
        Check(entered.Wait(TimeSpan.FromSeconds(5)));
        fixture.Coordinator.Disconnect();
        release.Set();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        // Dane spoznione NIE podmieniaja listy, a to nie jest blad uslugi.
        Check(result.Discarded && result.Households.Count == 0);
        Check(!result.Snapshot.HasCredentials);
    }

    private static void DiscardedAfterDispose()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var fixture = Fixture.Connected(_ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            return Json("{\"households\":[{\"id\":\"Sonos_1.a\"}]}");
        });

        try
        {
            var task = StartHouseholds(fixture);
            Check(entered.Wait(TimeSpan.FromSeconds(5)));
            fixture.Coordinator.Dispose();
            release.Set();
            var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(result.Discarded && result.Households.Count == 0);
        }
        finally
        {
            fixture.DisposeTransportOnly();
        }
    }

    /// <summary>
    /// POTWIERDZONY BLAD: stare 401 wpadalo w odnowienie JUZ PO nowym logowaniu,
    /// odnawialo dostep NOWEGO konta i zwracalo jego dane jako wynik starego
    /// odczytu. Tu stare zapytanie ma zostac PORZUCONE, a konto B nietkniete.
    ///
    /// Nowe konto wchodzi PRAWDZIWA droga logowania (BeginLogin + CheckLogin),
    /// a nie zadnym pomocnikiem pomiarowym.
    /// </summary>
    private static void StaleUnauthorizedNeverTouchesNewAccount()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var tokensSeen = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            tokensSeen.Add(request.Headers.Authorization?.Parameter);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            // Stare zadanie wraca z 401 - dopiero to uruchamialo bledne odnowienie.
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        fixture.Gateway.AllowLogin("SYNTHETIC-ACCESS-B");

        var task = StartHouseholds(fixture);
        Check(entered.Wait(TimeSpan.FromSeconds(5)));

        // W TRAKCIE wiszacego zadania uzytkownik loguje sie na NOWE konto.
        LogInAsNewAccount(fixture);
        var refreshesAfterLogin = fixture.Refreshes;

        release.Set();
        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Check(result.Discarded && result.Households.Count == 0);
        // ZERO odnowien z tej starej operacji: swieza generacja nie legalizuje
        // starego zadania.
        Check(fixture.Refreshes == refreshesAfterLogin);
        // Tylko JEDNO zapytanie i to STARYM tokenem: dane konta B nie zostaly
        // ani odczytane, ani opublikowane.
        Check(fixture.Requests == 1);
        Check(tokensSeen.Count == 1 && tokensSeen[0] == Fixture.Access);
        // Konto B zyje - nie zostalo skasowane ani nadpisane.
        Check(result.Snapshot.HasCredentials);
        Check(fixture.StoredAccessToken == "SYNTHETIC-ACCESS-B");
        Check(fixture.Deletes == 0);
    }

    /// <summary>
    /// Ten sam blad wejsciem przez WYGASLY token: stary odczyt odnawial dostep
    /// dopiero po nowym logowaniu i wysylal GET juz na nowym koncie.
    /// </summary>
    private static void StaleExpiredReadNeverTouchesNewAccount()
    {
        using var refreshEntered = new ManualResetEventSlim(false);
        using var refreshRelease = new ManualResetEventSlim(false);
        var requests = 0;
        using var fixture = Fixture.Connected(
            _ => { requests++; return Json("{\"households\":[]}"); },
            expiresInSeconds: 60,
            receivedShift: TimeSpan.FromHours(-2));
        fixture.Gateway.AllowLogin("SYNTHETIC-ACCESS-B");
        fixture.Gateway.HoldRefresh(refreshEntered, refreshRelease);

        // Odczyt widzi MINIONA waznosc, wiec najpierw idzie odnowic dostep.
        var task = StartHouseholds(fixture);
        Check(refreshEntered.Wait(TimeSpan.FromSeconds(5)));

        LogInAsNewAccount(fixture);

        refreshRelease.Set();
        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Check(result.Discarded && result.Households.Count == 0);
        // Wynik odnowienia dla STAREGO konta nie moze zostac przypisany do
        // konta B ani wyslany w zapytaniu.
        Check(requests == 0);
        Check(result.Snapshot.HasCredentials);
        Check(fixture.StoredAccessToken == "SYNTHETIC-ACCESS-B");
        Check(fixture.Deletes == 0);
    }

    /// <summary>PRAWDZIWA droga nowego logowania, bez pomocnika pomiarowego.</summary>
    private static void LogInAsNewAccount(Fixture fixture)
    {
        fixture.Coordinator.BeginLoginAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        var login = fixture.Coordinator.CheckLoginAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(login.Snapshot.HasCredentials);
        Check(fixture.StoredAccessToken == "SYNTHETIC-ACCESS-B");
    }

    private static void UnknownExpiryDoesNotRenew()
    {
        using var fixture = Fixture.Connected(_ => Json("{\"households\":[]}"), expiresInSeconds: null);
        var result = fixture.ReadHouseholds();
        Check(result.Succeeded && !result.Renewed);
        Check(fixture.Refreshes == 0 && fixture.Requests == 1);
        Check(result.Snapshot.HasCredentials && !result.Snapshot.IsExpiryKnown);
    }

    private static void ExpiredRenewsOnce()
    {
        using var fixture = Fixture.Connected(_ => Json("{\"households\":[]}"), expiresInSeconds: 1,
            nowUtc: new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            receivedShift: TimeSpan.FromMinutes(-10));
        var result = fixture.ReadHouseholds();
        Check(result.Succeeded && result.Renewed);
        Check(fixture.Refreshes == 1 && fixture.Requests == 1);
    }

    private static void UnauthorizedRenewsOnceAndRetriesOnce()
    {
        var attempt = 0;
        using var fixture = Fixture.Connected(_ =>
        {
            attempt++;
            return attempt == 1
                ? Json("{}", HttpStatusCode.Unauthorized)
                : Json("{\"households\":[{\"id\":\"Sonos_1.a\",\"name\":\"Dom\"}]}");
        });

        var result = fixture.ReadHouseholds();
        Check(result.Succeeded && result.Renewed && result.Households.Count == 1);
        // Dokladnie jedno odnowienie i jedno powtorzenie - zadnej petli reauth.
        Check(fixture.Refreshes == 1 && fixture.Requests == 2);
    }

    private static void UnauthorizedWithoutRefreshToken()
    {
        using var fixture = Fixture.Connected(_ => Json("{}", HttpStatusCode.Unauthorized), refreshToken: null);
        var result = fixture.ReadHouseholds();
        Check(result.Status == SonosDeviceReadStatus.Unauthorized);
        Check(fixture.Refreshes == 0 && fixture.Requests == 1);
        // 401 na tej sciezce NIE kasuje konta.
        Check(result.Snapshot.HasCredentials);
    }

    private static void TerminalKeepsAccount(HttpStatusCode code, SonosDeviceReadStatus expected)
    {
        using var fixture = Fixture.Connected(_ => Json("{}", code));
        var result = fixture.ReadHouseholds();
        Check(result.Status == expected && fixture.Requests == 1 && fixture.Refreshes == 0);
        Check(result.Snapshot.HasCredentials && result.Snapshot.State == SonosAccountState.Connected);
    }

    private static void CanceledKeepsAccount()
    {
        using var caller = new CancellationTokenSource();
        using var fixture = Fixture.Connected(_ =>
        {
            caller.Cancel();
            return Json("{\"households\":[]}");
        });

        var result = fixture.Coordinator
            .ReadGroupsAsync(fixture.Api, "Sonos_1.a", caller.Token)
            .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Status is SonosDeviceReadStatus.Canceled or SonosDeviceReadStatus.Success);
        Check(fixture.Coordinator.Snapshot.HasCredentials);
    }

    private static void MessagesHideSecrets()
    {
        using var fixture = Fixture.Connected(_ => Json("{}", HttpStatusCode.Forbidden));
        var result = fixture.ReadHouseholds();
        Check(!result.Message.Contains(Fixture.Access, StringComparison.Ordinal));
        Check(!result.ToString().Contains(Fixture.Access, StringComparison.Ordinal));
        Check(!result.ToString().Contains(Key, StringComparison.Ordinal));

        foreach (var status in Enum.GetValues<SonosDeviceReadStatus>())
        {
            var text = SonosDeviceReadMessages.Describe(status);
            Check(!string.IsNullOrWhiteSpace(text));
            Check(!text.Contains(Fixture.Access, StringComparison.Ordinal) && !text.Contains(Key, StringComparison.Ordinal));
        }
    }

    private const string GroupBody = """
        {"groups":[{"id":"g1","name":"Salon + 1","coordinatorId":"p1","playerIds":["p1","p2"],"playbackState":"PLAYBACK_STATE_PLAYING"}],
         "players":[{"id":"p1","name":"Salon"},{"id":"p2","name":"Kuchnia"}],"partial":true}
        """;

    // ================= aparatura =================

    /// <summary>
    /// Uruchamia odczyt na INNYM watku. Atrapa transportu odpowiada
    /// synchronicznie, wiec bez tego caly odczyt przebiegalby, zanim test
    /// zdazylby cokolwiek zmienic - i "porzucenie" nie byloby mierzone.
    /// </summary>
    private static Task<SonosHouseholdsReadResult> StartHouseholds(Fixture fixture) =>
        Task.Run(() => fixture.Coordinator.ReadHouseholdsAsync(fixture.Api, CancellationToken.None));

    private static void Check(bool ok)
    {
        if (!ok)
        {
            throw new InvalidOperationException("Niespelniona asercja (dane syntetyczne ukryte).");
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// Natywny transport zapisuje RequestMessage; klient tego WYMAGA i nie wolno
    /// go dla atrapy oslabiac. Fixture odwzorowuje ten kontrakt jawnie.
    /// </summary>
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            var response = reply(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Access = "SYNTHETIC-ACCESS-ONLY";
        private const string Broker = "https://broker.invalid/";

        private readonly Handler _handler;
        private readonly SonosControlApiClient _client;
        private readonly Store _store;
        private bool _coordinatorDisposed;

        private Fixture(SonosAccountCoordinator coordinator, Handler handler, SonosControlApiClient client, Gateway gateway, Store store)
        {
            Coordinator = coordinator;
            _handler = handler;
            _client = client;
            _store = store;
            Gateway = gateway;
            Api = new SonosControlApiDeviceApi(client);
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosDeviceApi Api { get; }

        internal Gateway Gateway { get; }

        internal int Requests => _handler.Count;

        internal int Refreshes => Gateway.RefreshCalls;

        /// <summary>Token ZAPISANY w magazynie - kwit, ze konto B zyje.</summary>
        internal string? StoredAccessToken => _store.AccessToken;

        /// <summary>Ile razy magazyn skasowano - kwit, ze konta B nie usunieto.</summary>
        internal int Deletes => _store.Deletes;

        internal static Fixture Connected(
            Func<HttpRequestMessage, HttpResponseMessage> reply,
            int? expiresInSeconds = 3600,
            string? refreshToken = "SYNTHETIC-REFRESH",
            DateTimeOffset? nowUtc = null,
            TimeSpan? receivedShift = null) =>
            Create(reply, connected: true, expiresInSeconds, refreshToken, nowUtc, receivedShift);

        internal static Fixture WithoutAccount(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: false, 3600, "SYNTHETIC-REFRESH", null, null);

        private static Fixture Create(
            Func<HttpRequestMessage, HttpResponseMessage> reply,
            bool connected,
            int? expiresInSeconds,
            string? refreshToken,
            DateTimeOffset? nowUtc,
            TimeSpan? receivedShift)
        {
            var now = nowUtc ?? new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
            var store = new Store();
            if (connected)
            {
                store.Seed(new SonosStoredCredentials(
                    Broker,
                    new SonosTokens(Access, "Bearer", expiresInSeconds, refreshToken, "playback-control-all"),
                    now + (receivedShift ?? TimeSpan.Zero)));
            }

            var gateway = new Gateway(expiresInSeconds, refreshToken);
            var coordinator = new SonosAccountCoordinator(gateway, store, Broker, () => now);
            coordinator.RestoreOnce();

            var handler = new Handler(reply);
            var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            return new Fixture(coordinator, handler, client, gateway, store);
        }

        internal SonosHouseholdsReadResult ReadHouseholds() =>
            Coordinator.ReadHouseholdsAsync(Api, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal SonosGroupsReadResult ReadGroups(string householdId) =>
            Coordinator.ReadGroupsAsync(Api, householdId, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal void DisposeTransportOnly()
        {
            _coordinatorDisposed = true;
            _client.Dispose();
            _handler.Dispose();
        }

        public void Dispose()
        {
            if (!_coordinatorDisposed)
            {
                Coordinator.Dispose();
            }

            _client.Dispose();
            _handler.Dispose();
        }
    }

    /// <summary>Atrapa bramki logowania: zero sieci, policzone odnowienia.</summary>
    internal sealed class Gateway(int? expiresInSeconds, string? refreshToken) : ISonosLoginGateway
    {
        private string? _loginAccessToken;
        private ManualResetEventSlim? _refreshEntered;
        private ManualResetEventSlim? _refreshRelease;

        public int RefreshCalls { get; private set; }

        /// <summary>Wlacza PRAWDZIWA droge logowania na NOWE konto.</summary>
        internal void AllowLogin(string accessToken) => _loginAccessToken = accessToken;

        /// <summary>Zatrzymuje odnowienie, zeby dalo sie zmierzyc stan w toku.</summary>
        internal void HoldRefresh(ManualResetEventSlim entered, ManualResetEventSlim release)
        {
            _refreshEntered = entered;
            _refreshRelease = release;
        }

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_loginAccessToken is null
                ? SonosLoginStartOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginStartOutcome.Ok(new SonosLoginSession(
                    "synthetic-session",
                    new string('a', 48),
                    new Uri("https://api.sonos.com/login/v3/oauth"),
                    DateTimeOffset.UtcNow.AddMinutes(5))));

        public Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken cancellationToken) =>
            Task.FromResult(_loginAccessToken is null
                ? SonosLoginResultOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginResultOutcome.Ok(new SonosTokens(
                    _loginAccessToken, "Bearer", 3600, "SYNTHETIC-REFRESH-B", "playback-control-all")));

        public Task<SonosRefreshOutcome> RefreshAsync(string? token, CancellationToken cancellationToken)
        {
            RefreshCalls++;
            _refreshEntered?.Set();
            _refreshRelease?.Wait(TimeSpan.FromSeconds(5));
            return Task.FromResult(SonosRefreshOutcome.Ok(new SonosTokens(
                "SYNTHETIC-ACCESS-RENEWED", "Bearer", expiresInSeconds ?? 3600, refreshToken, "playback-control-all")));
        }
    }

    /// <summary>Atrapa magazynu w PAMIECI: bez DPAPI, bez pliku i bez drugiego odczytu.</summary>
    private sealed class Store : ISonosCredentialStore
    {
        private SonosStoredCredentials? _record;

        internal void Seed(SonosStoredCredentials record) => _record = record;

        internal string? AccessToken => _record?.Tokens.AccessToken;

        internal int Deletes { get; private set; }

        public SonosCredentialReadOutcome Read() => _record is null
            ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
            : SonosCredentialReadOutcome.Ok(_record);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            _record = credentials;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            _record = null;
            return true;
        }
    }
}
