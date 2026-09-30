using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// F1b: ODCZYT ULUBIONYCH domu przez ISTNIEJACEGO koordynatora konta i jego
/// zabezpieczenia generacji. Tylko Core: zero GUI, zero prawdziwego konta,
/// zero sieci - dane syntetyczne, atrapa bramki logowania, atrapa magazynu
/// i PRAWDZIWY klient Control API na sztucznym HttpMessageHandler.
///
/// Co jest mierzone PRAWDZIWYM kodem produkcyjnym:
///   * brak konta: ZERO GET, ZERO odnowien, brak danych,
///   * sukces i PUSTA lista: dokladnie JEDEN GET, ZERO POST, wlasciwy token
///     zestawu, literalny dom zapytania, migawka STARA (z wyniku odczytu),
///   * znana miniona waznosc: JEDNO odnowienie PRZED GET, nowym tokenem;
///     nieznana waznosc nie odnawia niczego,
///   * 401: najwyzej JEDNO odnowienie i JEDNO powtorzenie GET; ponowne 401
///     konczy sie Unauthorized bez petli,
///   * 403/404/429/499/500: bez odnowienia, bez powtorzenia, bez kasowania
///     konta; status i komunikat MOWIA O ULUBIONYCH,
///   * anulowanie przed i po wejsciu w HTTP: Canceled albo Discarded, NIGDY
///     sukces z pusta lista,
///   * Dispose i Disconnect w trakcie GET: Discarded i brak danych,
///   * BARIERA GENERACJI: stara operacja po PRAWDZIWYM nowym logowaniu jest
///     porzucana, nie odnawia konta B, nie pyta jego biletem i nie kasuje go,
///   * nowe logowanie w trakcie przedodczytowego odnowienia: porzucone bez GET,
///     a anulowanie wlasnego czekajacego nie konczy cudzego odnowienia,
///   * HTTP leci POZA blokada koordynatora (Snapshot z DRUGIEGO watku w trakcie),
///   * komunikaty i ToString nie ujawniaja tokenow, klucza, nazw ani
///     identyfikatorow ulubionych.
/// </summary>
internal static class SonosFavoritesAccountTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Household = "Sonos_synthetic.household-1";
    private const string FavoriteName = "SYNTHETIC-FAVORITE-NAME";
    private const string FavoriteId = "SYNTHETICFAVID";

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("koordynator konta wystawia odczyt ulubionych", CoordinatorExposesFavoritesRead),
            ("brak konta: zero zapytan i zero odnowien", NoAccountReadsNothing),
            ("sukces biletem konta: jeden GET, wlasciwy dom", SuccessUsesAccountTicket),
            ("pusta lista ulubionych to nadal sukces", EmptyFavoritesStillSuccess),
            ("nieznana waznosc nie odnawia przed odczytem", UnknownExpiryDoesNotRenew),
            ("znana miniona waznosc: jedno odnowienie PRZED GET", ExpiredRenewsOnceBeforeGet),
            ("401: jedno odnowienie i jedno powtorzenie GET", UnauthorizedRenewsOnceRetriesOnce),
            ("ponowne 401 konczy sie Unauthorized bez petli", RepeatedUnauthorizedStops),
            ("odmowy uslugi bez odnowienia, powtorzenia i kasowania konta", ServiceRefusalsAreTerminal),
            ("niepoprawny dom: zapytanie nie idzie", InvalidHouseholdNeverReachesHttp),
            ("anulowanie przed i w trakcie GET nigdy nie udaje pustej listy", CancellationIsNeverEmptySuccess),
            ("Dispose w trakcie GET: wynik porzucony", DiscardedAfterDispose),
            ("bariera: nowe logowanie B porzuca stary odczyt A", StaleReadNeverTouchesNewAccount),
            ("bariera: stary 401 A nie odnawia i nie pyta kontem B", StaleUnauthorizedNeverRenewsNewAccount),
            ("nowe konto w trakcie odnowienia: porzucone bez GET", NewAccountDuringRenewal),
            ("anulowanie jednego czekajacego nie konczy cudzego odnowienia", CancelingOneWaiterKeepsOtherRefresh),
            ("HTTP poza blokada: Snapshot z drugiego watku w trakcie GET", SnapshotDuringRead),
            ("konto po bledzie zapisu nadal odczytuje ulubione", UnpersistedAccountStillReads),
            ("komunikaty i ToString bez tokenow i tresci", MessagesHideSecrets)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Odczyt ulubionych Sonos przez konto: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Odczyt ulubionych Sonos przez konto: " + failures + " nieudanych testow.");
        }
    }

    // ================= przypadki =================

    private static void CoordinatorExposesFavoritesRead()
    {
        var method = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "ReadFavoritesAsync"
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[] { typeof(ISonosFavoritesApi), typeof(string), typeof(CancellationToken) }));

        Check(method is not null);
    }

    private static void NoAccountReadsNothing()
    {
        using var fixture = Fixture.WithoutAccount(_ => Favorites());
        var result = fixture.Read();

        Check(result.Status == SonosDeviceReadStatus.NoAccount);
        Check(result.Favorites is null && !result.Succeeded && !result.Discarded);
        Check(fixture.Requests == 0 && fixture.Refreshes == 0 && fixture.Deletes == 0);
        Check(!result.Snapshot.HasCredentials);
    }

    private static void SuccessUsesAccountTicket()
    {
        var tokens = new List<string?>();
        var paths = new List<string?>();
        var methods = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            paths.Add(request.RequestUri?.AbsolutePath);
            methods.Add(request.Method.Method);
            return Favorites();
        });

        var before = fixture.Coordinator.Snapshot.CredentialGeneration;
        var result = fixture.Read();

        Check(result.Succeeded && result.Status == SonosDeviceReadStatus.Success);
        Check(result.Favorites!.Items.Count == 1 && result.Favorites.Items[0].Id == FavoriteId);
        Check(result.Favorites.Items[0].Name == FavoriteName);
        // Dom pochodzi LITERALNIE z zapytania, nie z odpowiedzi uslugi.
        Check(result.Favorites.HouseholdId == Household);
        Check(fixture.Requests == 1 && fixture.Refreshes == 0 && !result.Renewed);
        Check(tokens.Count == 1 && tokens[0] == Fixture.Access);
        // DOKLADNIE jeden GET, zero POST.
        Check(methods.Count == 1 && methods[0] == "GET");
        Check(paths[0]!.EndsWith("/favorites", StringComparison.Ordinal));
        // Migawka to STARA tozsamosc zestawu z czasu odczytu, nie nowa.
        Check(result.Snapshot.HasCredentials && result.Snapshot.CredentialGeneration == before);
    }

    private static void EmptyFavoritesStillSuccess()
    {
        using var fixture = Fixture.Connected(_ => Json("{\"version\":\"v1\",\"items\":[]}"));
        var result = fixture.Read();

        Check(result.Succeeded && result.Favorites!.Items.Count == 0);
        Check(fixture.Requests == 1 && fixture.Refreshes == 0);
        // Pusta lista NIE jest odmowa: komunikat mowi o odczytaniu.
        Check(result.Message.Contains("odczytana", StringComparison.Ordinal));
    }

    private static void UnknownExpiryDoesNotRenew()
    {
        using var fixture = Fixture.Connected(_ => Favorites(), expiresInSeconds: null);
        var result = fixture.Read();

        Check(result.Succeeded && !result.Renewed);
        Check(fixture.Refreshes == 0 && fixture.Requests == 1);
        Check(!result.Snapshot.IsExpiryKnown);
    }

    private static void ExpiredRenewsOnceBeforeGet()
    {
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(
            request => { tokens.Add(request.Headers.Authorization?.Parameter); return Favorites(); },
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));

        var result = fixture.Read();

        Check(result.Succeeded && result.Renewed);
        Check(fixture.Refreshes == 1 && fixture.Requests == 1);
        // GET poszedl JUZ ODNOWIONYM biletem, nie starym.
        Check(tokens.Count == 1 && tokens[0] == Fixture.Renewed);
    }

    private static void UnauthorizedRenewsOnceRetriesOnce()
    {
        var attempt = 0;
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            attempt++;
            tokens.Add(request.Headers.Authorization?.Parameter);
            return attempt == 1 ? Json("{}", HttpStatusCode.Unauthorized) : Favorites();
        });

        var result = fixture.Read();

        Check(result.Succeeded && result.Renewed);
        Check(fixture.Refreshes == 1 && fixture.Requests == 2);
        Check(tokens[0] == Fixture.Access && tokens[1] == Fixture.Renewed);
        Check(fixture.Deletes == 0);
    }

    private static void RepeatedUnauthorizedStops()
    {
        using var fixture = Fixture.Connected(_ => Json("{}", HttpStatusCode.Unauthorized));
        var result = fixture.Read();

        Check(result.Status == SonosDeviceReadStatus.Unauthorized && result.Favorites is null);
        // JEDNO odnowienie, DWA zapytania, zadnej petli reauth.
        Check(fixture.Refreshes == 1 && fixture.Requests == 2);
        // Sam odczyt NIE kasuje konta.
        Check(fixture.Deletes == 0 && result.Snapshot.HasCredentials);
    }

    private static void ServiceRefusalsAreTerminal()
    {
        var cases = new (HttpStatusCode Code, SonosDeviceReadStatus Status)[]
        {
            (HttpStatusCode.Forbidden, SonosDeviceReadStatus.Forbidden),
            (HttpStatusCode.NotFound, SonosDeviceReadStatus.NotFound),
            ((HttpStatusCode)429, SonosDeviceReadStatus.RateLimited),
            ((HttpStatusCode)499, SonosDeviceReadStatus.CommandFailed),
            (HttpStatusCode.InternalServerError, SonosDeviceReadStatus.ServiceError)
        };

        foreach (var (code, expected) in cases)
        {
            using var fixture = Fixture.Connected(_ => Json("{\"globalError\":{\"reason\":\"SYNTHETIC-REASON\"}}", code));
            var result = fixture.Read();

            Check(result.Status == expected);
            Check(result.Favorites is null && !result.Succeeded && !result.Discarded);
            // Bez odnowienia, bez powtorzenia, bez kasowania konta.
            Check(fixture.Requests == 1 && fixture.Refreshes == 0 && fixture.Deletes == 0);
            Check(result.Snapshot.HasCredentials);
            // Odmowa NIE jest pusta lista i mowi o ULUBIONYCH albo o usludze,
            // nigdy o "liscie urzadzen".
            Check(!result.Message.Contains("urządzeń", StringComparison.Ordinal));
            Check(!result.Message.Contains("SYNTHETIC-REASON", StringComparison.Ordinal));
        }
    }

    private static void InvalidHouseholdNeverReachesHttp()
    {
        using var fixture = Fixture.Connected(_ => Favorites());
        // Klient waliduje segment sciezki PRZED HTTP - dokladnie jak przy grupach.
        var result = fixture.ReadCore(household: "dom/z/ukosnikami");

        Check(result.Status == SonosDeviceReadStatus.InvalidConfiguration);
        Check(result.Favorites is null && fixture.Requests == 0);
        // Niepoprawne wejscie nie wywoluje odnowienia konta.
        Check(fixture.Refreshes == 0 && fixture.Deletes == 0);
        Check(result.Snapshot.HasCredentials);
    }

    private static void CancellationIsNeverEmptySuccess()
    {
        // (a) anulowanie PRZED wejsciem w HTTP.
        using var beforeSource = new CancellationTokenSource();
        beforeSource.Cancel();
        using var before = Fixture.Connected(_ => Favorites());
        var beforeResult = before.ReadCore(cancellationToken: beforeSource.Token);
        Check(beforeResult.Status == SonosDeviceReadStatus.Canceled);
        Check(beforeResult.Favorites is null && !beforeResult.Succeeded);
        Check(before.Requests == 0);

        // (b) anulowanie PO wejsciu w HTTP.
        var gate = new Gate();
        using var during = new CancellationTokenSource();
        using var fixture = Fixture.Connected(_ =>
        {
            gate.EnterAndWait();
            throw new OperationCanceledException();
        });

        var task = Task.Run(() => fixture.ReadCore(cancellationToken: during.Token));
        Check(gate.WaitEntered());
        during.Cancel();
        gate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Status is SonosDeviceReadStatus.Canceled or SonosDeviceReadStatus.Discarded);
        Check(result.Favorites is null && !result.Succeeded);
    }

    private static void DiscardedAfterDispose()
    {
        var gate = new Gate();
        var fixture = Fixture.Connected(_ => { gate.EnterAndWait(); return Favorites(); });
        try
        {
            var task = Task.Run(() => fixture.ReadCore());
            Check(gate.WaitEntered());
            fixture.Coordinator.Dispose();
            gate.Release();

            var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(result.Status == SonosDeviceReadStatus.Discarded && result.Discarded);
            Check(result.Favorites is null && !result.Succeeded);
        }
        finally
        {
            fixture.DisposeTransportOnly();
        }
    }

    private static void StaleReadNeverTouchesNewAccount()
    {
        var gate = new Gate();
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            gate.EnterAndWait();
            return Favorites();
        });
        fixture.Gateway.AllowLogin(Fixture.AccessB);

        var task = Task.Run(() => fixture.ReadCore());
        Check(gate.WaitEntered());

        // PRAWDZIWE nowe logowanie przez istniejacy tor publiczny - bez refleksji
        // i bez podstawiania generacji.
        fixture.LogInAsNewAccount();
        var refreshesAfterLogin = fixture.Refreshes;
        var writesAfterLogin = fixture.Writes;
        var deletesAfterLogin = fixture.Deletes;
        gate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Check(result.Status == SonosDeviceReadStatus.Discarded && result.Discarded);
        // STARE dane A nie wychodza na zewnatrz.
        Check(result.Favorites is null && !result.Succeeded);
        // Po ZWOLNIENIU starej operacji: zero odnowien, zero nadpisan, zero usuniec.
        Check(fixture.Refreshes == refreshesAfterLogin);
        Check(fixture.Writes == writesAfterLogin && fixture.Deletes == deletesAfterLogin);
        // Konto B nietkniete, a stary GET poszedl TYLKO biletem A - raz.
        Check(fixture.StoredAccessToken == Fixture.AccessB);
        Check(fixture.Requests == 1 && tokens.Count == 1 && tokens[0] == Fixture.Access);
    }

    private static void StaleUnauthorizedNeverRenewsNewAccount()
    {
        var gate = new Gate();
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            gate.EnterAndWait();
            return Json("{}", HttpStatusCode.Unauthorized);
        });
        fixture.Gateway.AllowLogin(Fixture.AccessB);

        var task = Task.Run(() => fixture.ReadCore());
        Check(gate.WaitEntered());
        fixture.LogInAsNewAccount();
        var refreshesAfterLogin = fixture.Refreshes;
        var writesAfterLogin = fixture.Writes;
        gate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Check(result.Status == SonosDeviceReadStatus.Discarded && result.Favorites is null);
        // 401 STAREJ operacji nie odnawia i nie powtarza kontem B.
        Check(fixture.Refreshes == refreshesAfterLogin);
        Check(fixture.Requests == 1 && tokens.Count == 1 && tokens[0] == Fixture.Access);
        Check(fixture.StoredAccessToken == Fixture.AccessB && fixture.Deletes == 0);
        Check(fixture.Writes == writesAfterLogin);
    }

    private static void NewAccountDuringRenewal()
    {
        var refreshGate = new Gate();
        using var fixture = Fixture.Connected(
            _ => Favorites(),
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));
        fixture.Gateway.AllowLogin(Fixture.AccessB);
        fixture.Gateway.HoldRefresh(refreshGate);

        var task = Task.Run(() => fixture.ReadCore());
        Check(refreshGate.WaitEntered());
        fixture.LogInAsNewAccount();
        refreshGate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Check(result.Status == SonosDeviceReadStatus.Discarded && result.Favorites is null);
        // ZADNEGO GET - ani starym, ani nowym biletem.
        Check(fixture.Requests == 0);
        Check(fixture.StoredAccessToken == Fixture.AccessB && fixture.Deletes == 0);
    }

    private static void CancelingOneWaiterKeepsOtherRefresh()
    {
        var refreshGate = new Gate();
        using var canceled = new CancellationTokenSource();
        using var fixture = Fixture.Connected(
            _ => Favorites(),
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));
        fixture.Gateway.HoldRefresh(refreshGate);

        var first = Task.Run(() => fixture.ReadCore(cancellationToken: canceled.Token));
        Check(refreshGate.WaitEntered());
        var second = Task.Run(() => fixture.ReadCore());

        canceled.Cancel();
        refreshGate.Release();

        var firstResult = first.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        var secondResult = second.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

        Check(firstResult.Status is SonosDeviceReadStatus.Canceled or SonosDeviceReadStatus.Success);
        // CUDZE wspoldzielone odnowienie zyje dalej: drugi odczyt konczy sie normalnie.
        Check(secondResult.Succeeded && secondResult.Favorites!.Items.Count == 1);
        Check(fixture.Coordinator.Snapshot.HasCredentials);
    }

    private static void SnapshotDuringRead()
    {
        var gate = new Gate();
        var generation = -1L;
        using var fixture = Fixture.Connected(_ => { gate.EnterAndWait(); return Favorites(); });

        var task = Task.Run(() => fixture.ReadCore());
        Check(gate.WaitEntered());

        // Gdyby HTTP bieglo pod blokada koordynatora, ten odczyt z DRUGIEGO
        // watku nie wrocilby przed zwolnieniem bramki.
        var probe = Task.Run(() => generation = fixture.Coordinator.Snapshot.CredentialGeneration);
        Check(probe.Wait(TimeSpan.FromSeconds(3)));
        Check(generation >= 0);

        gate.Release();
        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Succeeded);
    }

    private static void UnpersistedAccountStillReads()
    {
        using var fixture = Fixture.Connected(_ => Favorites(), failWrites: true);
        fixture.Gateway.AllowLogin(Fixture.AccessB);
        fixture.LogInAsNewAccount(expectStored: false);

        var result = fixture.Read();

        // Blad ZAPISU nie udaje braku zywego konta.
        Check(result.Succeeded && result.Status == SonosDeviceReadStatus.Success);
        Check(result.Snapshot.HasCredentials && !result.Snapshot.IsPersisted);
        Check(fixture.Requests == 1);
    }

    private static void MessagesHideSecrets()
    {
        using var fixture = Fixture.Connected(_ => Favorites());
        fixture.Gateway.AllowLogin(Fixture.AccessB);
        var success = fixture.Read();
        foreach (var text in new[] { success.Message, success.ToString() })
        {
            Check(!text.Contains(Fixture.Access, StringComparison.Ordinal));
            Check(!text.Contains(Fixture.AccessB, StringComparison.Ordinal));
            Check(!text.Contains(Fixture.Renewed, StringComparison.Ordinal));
            Check(!text.Contains(Key, StringComparison.Ordinal));
            Check(!text.Contains(FavoriteId, StringComparison.Ordinal));
            Check(!text.Contains(FavoriteName, StringComparison.Ordinal));
            Check(!text.Contains(Household, StringComparison.Ordinal));
        }

        // Migawka konta tez nie wypisuje tokenu.
        Check(!success.Snapshot.ToString()!.Contains(Fixture.Access, StringComparison.Ordinal));

        using var refused = Fixture.Connected(_ => Json("{\"globalError\":{\"reason\":\"SYNTHETIC-REASON\"}}", HttpStatusCode.Forbidden));
        var refusedResult = refused.Read();
        Check(!refusedResult.Message.Contains("SYNTHETIC-REASON", StringComparison.Ordinal));
        Check(!refusedResult.ToString().Contains("SYNTHETIC-REASON", StringComparison.Ordinal));
        // Odmowa nie wyglada jak pusta lista.
        Check(refusedResult.ToString().Contains("brak danych", StringComparison.Ordinal));
        Check(!refusedResult.ToString().Contains("pozycji 0", StringComparison.Ordinal));

        // Kazdy staly tekst jest niepusty, mowi o ULUBIONYCH albo o usludze i
        // nie zawiera zadnego sekretu.
        foreach (var status in Enum.GetValues<SonosDeviceReadStatus>())
        {
            var text = SonosFavoritesReadMessages.Describe(status);
            Check(!string.IsNullOrWhiteSpace(text));
            Check(!text.Contains(Fixture.Access, StringComparison.Ordinal) && !text.Contains(Key, StringComparison.Ordinal));
            Check(!text.Contains("urządzeń", StringComparison.Ordinal));
        }
    }

    // ================= aparatura =================

    private static void Check(bool ok)
    {
        if (!ok)
        {
            throw new InvalidOperationException("Niespelniona asercja (dane syntetyczne ukryte).");
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Favorites() =>
        Json("{\"version\":\"v1\",\"items\":[{\"id\":\"" + FavoriteId + "\",\"name\":\"" + FavoriteName + "\"}]}");

    /// <summary>
    /// Bramka zdarzeniowa na TaskCompletionSource z RunContinuationsAsynchronously:
    /// wznowienie NIE biegnie na watku zwalniajacym, wiec test nie przejmuje
    /// przypadkiem watku operacji. Kazde czekanie ma skonczony limit.
    /// </summary>
    private sealed class Gate
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void EnterAndWait()
        {
            entered.TrySetResult();
            released.Task.Wait(TimeSpan.FromSeconds(5));
        }

        internal bool WaitEntered() => entered.Task.Wait(TimeSpan.FromSeconds(5));

        internal void Release() => released.TrySetResult();
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            var response = reply(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Access = "SYNTHETIC-ACCESS-ONLY";
        internal const string AccessB = "SYNTHETIC-ACCESS-B";
        internal const string Renewed = "SYNTHETIC-ACCESS-RENEWED";
        private const string Broker = "https://broker.invalid/";

        private readonly Handler handler;
        private readonly SonosControlApiClient client;
        private readonly Store store;
        private bool coordinatorDisposed;

        private Fixture(SonosAccountCoordinator coordinator, Handler handler, SonosControlApiClient client, Gateway gateway, Store store)
        {
            Coordinator = coordinator;
            this.handler = handler;
            this.client = client;
            this.store = store;
            Gateway = gateway;
            // PRAWDZIWY klient Control API JUZ implementuje ISonosFavoritesApi -
            // zaden nowy adapter nie jest potrzebny. Caly tor HTTP jest produkcyjny.
            Api = client;
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosFavoritesApi Api { get; }

        internal Gateway Gateway { get; }

        internal int Requests => handler.Count;

        internal int Refreshes => Gateway.RefreshCalls;

        internal string? StoredAccessToken => store.AccessToken;

        internal int Deletes => store.Deletes;

        internal int Writes => store.Writes;

        internal static Fixture Connected(
            Func<HttpRequestMessage, HttpResponseMessage> reply,
            int? expiresInSeconds = 3600,
            string? refreshToken = "SYNTHETIC-REFRESH",
            TimeSpan? receivedShift = null,
            bool failWrites = false) =>
            Create(reply, connected: true, expiresInSeconds, refreshToken, receivedShift, failWrites);

        internal static Fixture WithoutAccount(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: false, 3600, "SYNTHETIC-REFRESH", null, false);

        private static Fixture Create(
            Func<HttpRequestMessage, HttpResponseMessage> reply,
            bool connected,
            int? expiresInSeconds,
            string? refreshToken,
            TimeSpan? receivedShift,
            bool failWrites)
        {
            var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
            var store = new Store();
            if (connected)
            {
                store.Seed(new SonosStoredCredentials(
                    Broker,
                    new SonosTokens(Access, "Bearer", expiresInSeconds, refreshToken, "playback-control-all"),
                    now + (receivedShift ?? TimeSpan.Zero)));
            }

            store.FailWrites = failWrites;
            var gateway = new Gateway(expiresInSeconds, refreshToken);
            var coordinator = new SonosAccountCoordinator(gateway, store, Broker, () => now);
            coordinator.RestoreOnce();

            var handler = new Handler(reply);
            var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            return new Fixture(coordinator, handler, client, gateway, store);
        }

        /// <summary>PRAWDZIWA droga nowego logowania, bez podstawiania pol i bez refleksji.</summary>
        internal void LogInAsNewAccount(bool expectStored = true)
        {
            Coordinator.BeginLoginAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var login = Coordinator.CheckLoginAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(login.Snapshot.HasCredentials);
            Check(!expectStored || StoredAccessToken == AccessB);
        }

        internal SonosFavoritesReadResult Read() => ReadCore();

        internal SonosFavoritesReadResult ReadCore(
            string? household = Household,
            CancellationToken cancellationToken = default) =>
            Coordinator.ReadFavoritesAsync(Api, household, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal void DisposeTransportOnly()
        {
            coordinatorDisposed = true;
            client.Dispose();
            handler.Dispose();
        }

        public void Dispose()
        {
            if (!coordinatorDisposed)
            {
                Coordinator.Dispose();
            }

            client.Dispose();
            handler.Dispose();
        }
    }

    /// <summary>Atrapa bramki logowania: zero sieci, policzone odnowienia.</summary>
    private sealed class Gateway(int? expiresInSeconds, string? refreshToken) : ISonosLoginGateway
    {
        private string? loginAccessToken;
        private Gate? refreshGate;
        private int refreshCalls;

        public int RefreshCalls => Volatile.Read(ref refreshCalls);

        internal void AllowLogin(string accessToken) => loginAccessToken = accessToken;

        internal void HoldRefresh(Gate gate) => refreshGate = gate;

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
            Task.FromResult(loginAccessToken is null
                ? SonosLoginStartOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginStartOutcome.Ok(new SonosLoginSession(
                    "synthetic-session",
                    new string('a', 48),
                    new Uri("https://api.sonos.com/login/v3/oauth"),
                    DateTimeOffset.UtcNow.AddMinutes(5))));

        public Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken cancellationToken) =>
            Task.FromResult(loginAccessToken is null
                ? SonosLoginResultOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginResultOutcome.Ok(new SonosTokens(
                    loginAccessToken, "Bearer", 3600, "SYNTHETIC-REFRESH-B", "playback-control-all")));

        public Task<SonosRefreshOutcome> RefreshAsync(string? token, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref refreshCalls);
            refreshGate?.EnterAndWait();
            return Task.FromResult(SonosRefreshOutcome.Ok(new SonosTokens(
                Fixture.Renewed, "Bearer", expiresInSeconds ?? 3600, refreshToken, "playback-control-all")));
        }
    }

    /// <summary>Atrapa magazynu w PAMIECI: bez DPAPI i bez pliku. Liczy zapisy i usuniecia.</summary>
    private sealed class Store : ISonosCredentialStore
    {
        private SonosStoredCredentials? record;

        internal void Seed(SonosStoredCredentials value) => record = value;

        internal bool FailWrites { get; set; }

        internal string? AccessToken => record?.Tokens.AccessToken;

        internal int Deletes { get; private set; }

        internal int Writes { get; private set; }

        public SonosCredentialReadOutcome Read() => record is null
            ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
            : SonosCredentialReadOutcome.Ok(record);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            if (FailWrites)
            {
                // Kontrakt magazynu: nieudany zapis NIE zmienia zawartosci.
                return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
            }

            record = credentials;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            record = null;
            return true;
        }
    }
}
