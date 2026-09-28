using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// AUTORYZOWANE operacje GRUPY przez ISTNIEJACEGO wlasciciela konta: trzy
/// odczyty (stan, metadane, glosnosc) i podstawowe polecenia. Wylacznie dane
/// syntetyczne, atrapa bramki logowania i atrapa magazynu; zero sieci, zero
/// prawdziwego konta, zero GUI.
///
/// Co jest mierzone PRAWDZIWYM kodem produkcyjnym:
///   * odczyt idzie BILETEM biezacego konta (naglowek z tokenem zestawu),
///   * znana MINIONA waznosc daje JEDNO odnowienie PRZED zapytaniem,
///     nieznana waznosc nie odnawia niczego,
///   * GET 401: najwyzej jedno odnowienie i jedno powtorzenie,
///   * POST: ZERO automatycznych powtorzen - takze po 401, 429, 5xx i po
///     utraconej odpowiedzi. Licznik POST = 1,
///   * HTTP leci POZA blokada koordynatora (Snapshot z DRUGIEGO watku w trakcie),
///   * stara operacja po nowym logowaniu albo Disconnect NIE odnawia konta B,
///     nie uzywa jego tokenu i nie publikuje spoznionych danych,
///   * porzucone POLECENIE nie udaje cofniecia: proba i nieznany skutek
///     zostaja rozdzielone,
///   * anulowanie jednego czekajacego nie konczy cudzego odnowienia.
/// </summary>
internal static class SonosGroupAccountOperationsTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Group = "RINCON_0001:1";

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("brak konta: zero zapytan grupy", NoAccountSendsNothing),
            ("stan grupy biletem konta", PlaybackUsesAccountTicket),
            ("metadane i glosnosc biletem konta", MetadataAndVolumeUseAccountTicket),
            ("nieznana waznosc nie odnawia przed odczytem", UnknownExpiryDoesNotRenewRead),
            ("znana miniona waznosc: jedno odnowienie przed odczytem", ExpiredRenewsOnceBeforeRead),
            ("odczyt 401: jedno odnowienie i jedno powtorzenie", ReadUnauthorizedRenewsOnceAndRetriesOnce),
            ("polecenie przyjete to nie potwierdzony skutek", CommandAcceptedIsNotEffect),
            ("polecenie: znana miniona waznosc odnawia PRZED jednym POST", CommandRenewsBeforeSinglePost),
            ("polecenie: 401 nie powtarza POST", CommandNeverRetriesAfterUnauthorized),
            ("polecenie: jeden POST mimo 429, 5xx i utraconej odpowiedzi", CommandCountsOnePostAlways),
            ("HTTP poza blokada: Snapshot z drugiego watku w trakcie POST", SnapshotDuringCommand),
            ("Disconnect w trakcie POST: porzucone, bez obietnicy cofniecia", DiscardedCommandDoesNotPromiseUndo),
            ("stary 401 odczytu nie dotyka NOWEGO konta", StaleReadNeverTouchesNewAccount),
            ("nowe konto w trakcie odnowienia polecenia: porzucone bez POST", NewAccountDuringCommandRenewal),
            ("Dispose w trakcie odczytu: wynik porzucony", DiscardedAfterDispose),
            ("anulowanie jednego czekajacego nie konczy cudzego odnowienia", CancelingOneWaiterKeepsOtherRefresh),
            ("konto po bledzie zapisu nadal wykonuje operacje", UnpersistedAccountStillWorks),
            ("komunikaty i wyniki bez tokenow", MessagesHideSecrets)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Operacje grupy Sonos przez konto: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Operacje grupy Sonos przez konto: " + failures + " nieudanych testow.");
        }
    }

    // ================= przypadki =================

    private static void NoAccountSendsNothing()
    {
        using var fixture = Fixture.WithoutAccount(_ => Json("{}"));
        var read = fixture.ReadPlayback();
        Check(read.Status == SonosDeviceReadStatus.NoAccount && read.Value is null);

        var command = fixture.Send(SonosGroupCommand.Play);
        Check(command.Status == SonosGroupOperationStatus.NoAccount);
        Check(!command.RequestSent && !command.Accepted && !command.EffectConfirmed);
        Check(fixture.Requests == 0);
    }

    private static void PlaybackUsesAccountTicket()
    {
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            return Json("{\"playbackState\":\"PLAYBACK_STATE_PLAYING\",\"positionMillis\":1234}");
        });

        var result = fixture.ReadPlayback();
        Check(result.Succeeded && result.Value!.PlaybackState == SonosPlaybackState.Playing);
        Check(result.Value!.PositionMillis == 1234);
        Check(fixture.Requests == 1 && fixture.Refreshes == 0 && !result.Renewed);
        Check(tokens.Count == 1 && tokens[0] == Fixture.Access);
        Check(result.Snapshot.HasCredentials);
    }

    private static void MetadataAndVolumeUseAccountTicket()
    {
        using var metadata = Fixture.Connected(_ =>
            Json("{\"currentItem\":{\"track\":{\"name\":\"Utwor\",\"artist\":{\"name\":\"Wykonawca\"}}}}"));
        var metadataResult = metadata.ReadMetadata();
        Check(metadataResult.Succeeded && metadataResult.Value!.CurrentTrack!.Name == "Utwor");
        Check(metadata.Requests == 1);

        using var volume = Fixture.Connected(_ => Json("{\"volume\":31,\"muted\":false,\"fixed\":false}"));
        var volumeResult = volume.ReadVolume();
        Check(volumeResult.Succeeded && volumeResult.Value!.Volume == 31 && volumeResult.Value.Adjustable);
        Check(volume.Requests == 1 && volume.Refreshes == 0);
    }

    private static void UnknownExpiryDoesNotRenewRead()
    {
        using var fixture = Fixture.Connected(_ => Json("{\"volume\":10}"), expiresInSeconds: null);
        var result = fixture.ReadVolume();
        Check(result.Succeeded && !result.Renewed);
        Check(fixture.Refreshes == 0 && fixture.Requests == 1);
        Check(!result.Snapshot.IsExpiryKnown);
    }

    private static void ExpiredRenewsOnceBeforeRead()
    {
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(
            request => { tokens.Add(request.Headers.Authorization?.Parameter); return Json("{\"volume\":5}"); },
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));

        var result = fixture.ReadVolume();
        Check(result.Succeeded && result.Renewed);
        Check(fixture.Refreshes == 1 && fixture.Requests == 1);
        // Zapytanie poszlo JUZ ODNOWIONYM biletem, nie starym.
        Check(tokens.Count == 1 && tokens[0] == Fixture.Renewed);
    }

    private static void ReadUnauthorizedRenewsOnceAndRetriesOnce()
    {
        var attempt = 0;
        using var fixture = Fixture.Connected(_ =>
        {
            attempt++;
            return attempt == 1
                ? Json("{}", HttpStatusCode.Unauthorized)
                : Json("{\"playbackState\":\"PLAYBACK_STATE_PAUSED\"}");
        });

        var result = fixture.ReadPlayback();
        Check(result.Succeeded && result.Renewed);
        Check(result.Value!.PlaybackState == SonosPlaybackState.Paused);
        Check(fixture.Refreshes == 1 && fixture.Requests == 2);
    }

    private static void CommandAcceptedIsNotEffect()
    {
        var methods = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            methods.Add(request.Method.Method);
            return Json("{}");
        });

        var result = fixture.Send(SonosGroupCommand.TogglePlayPause);
        Check(result.Status == SonosGroupOperationStatus.Attempted && result.RequestSent);
        Check(result.Accepted && !result.EffectConfirmed && !result.EffectAmbiguous);
        Check(result.Outcome!.Command == SonosGroupCommand.TogglePlayPause);
        Check(fixture.Requests == 1 && methods.Count == 1 && methods[0] == "POST");
        // Zaden komunikat nie oglasza wykonania polecenia.
        Check(result.Message.Contains("niepotwierdzone", StringComparison.Ordinal));
    }

    private static void CommandRenewsBeforeSinglePost()
    {
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(
            request => { tokens.Add(request.Headers.Authorization?.Parameter); return Json("{}"); },
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));

        var result = fixture.SetVolume(42);
        Check(result.Status == SonosGroupOperationStatus.Attempted && result.Accepted && result.Renewed);
        Check(fixture.Refreshes == 1 && fixture.Requests == 1);
        Check(tokens.Count == 1 && tokens[0] == Fixture.Renewed);
    }

    private static void CommandNeverRetriesAfterUnauthorized()
    {
        using var fixture = Fixture.Connected(_ => Json("{}", HttpStatusCode.Unauthorized));
        var result = fixture.Send(SonosGroupCommand.Pause);

        Check(result.Status == SonosGroupOperationStatus.Attempted && result.RequestSent);
        Check(!result.Accepted && result.Outcome!.Status == SonosControlApiStatus.Unauthorized);
        // ZERO odnowien po fakcie i ZERO powtorzen POST.
        Check(fixture.Requests == 1 && fixture.Refreshes == 0);
        // 401 polecenia NIE kasuje konta.
        Check(result.Snapshot.HasCredentials && fixture.Deletes == 0);
    }

    private static void CommandCountsOnePostAlways()
    {
        foreach (var code in new[] { (HttpStatusCode)429, HttpStatusCode.ServiceUnavailable, HttpStatusCode.InternalServerError })
        {
            using var fixture = Fixture.Connected(_ => Json("{}", code));
            var result = fixture.Send(SonosGroupCommand.SkipToNextTrack);
            Check(result.Status == SonosGroupOperationStatus.Attempted && !result.Accepted);
            Check(fixture.Requests == 1 && fixture.Refreshes == 0);
            Check(result.Snapshot.HasCredentials);
        }

        // UTRACONA odpowiedz: polecenie moglo sie wykonac, wiec ani go nie
        // ponawiamy, ani nie oglaszamy, ze nie doszlo.
        using var lost = Fixture.Connected(_ => throw new HttpRequestException("brak odpowiedzi", new IOException()));
        var lostResult = lost.Send(SonosGroupCommand.SkipToPreviousTrack);
        Check(lost.Requests == 1 && lost.Refreshes == 0);
        Check(lostResult.Status == SonosGroupOperationStatus.Attempted && !lostResult.Accepted);
        Check(lostResult.EffectAmbiguous && !lostResult.EffectConfirmed);
    }

    private static void SnapshotDuringCommand()
    {
        var gate = new Gate();
        var generation = -1L;
        using var fixture = Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{}"); });

        var task = Task.Run(() => fixture.SendCore(SonosGroupCommand.Play));
        Check(gate.WaitEntered());

        var probe = Task.Run(() => generation = fixture.Coordinator.Snapshot.CredentialGeneration);
        Check(probe.Wait(TimeSpan.FromSeconds(3)));
        Check(generation >= 0);

        gate.Release();
        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Accepted);
    }

    private static void DiscardedCommandDoesNotPromiseUndo()
    {
        var gate = new Gate();
        using var fixture = Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{}"); });

        var task = Task.Run(() => fixture.SendCore(SonosGroupCommand.SkipToNextTrack));
        Check(gate.WaitEntered());
        fixture.Coordinator.Disconnect();
        gate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Status == SonosGroupOperationStatus.Discarded);
        // Zadanie JUZ poszlo: uczciwie mowimy o probie i nieznanym skutku,
        // nie o cofnieciu ani o niewyslaniu.
        Check(result.RequestSent && !result.Accepted && !result.EffectConfirmed);
        Check(result.Message.Contains("nieznany", StringComparison.Ordinal));
        Check(!result.Message.Contains("cofni", StringComparison.Ordinal));
        Check(!result.Message.Contains("nie zostało wysłane", StringComparison.Ordinal));
        // RequestSent mowi tylko o PROBIE wyslania (wywolanie HttpClient), nie o
        // dowodzie opuszczenia maszyny: zaden tekst nie moze tego gwarantowac.
        Check(result.Message.Contains("próbie wysłania", StringComparison.Ordinal));
        Check(!result.Message.Contains("po wysłaniu", StringComparison.Ordinal));
        Check(result.ToString().Contains("podjęto próbę wysłania", StringComparison.Ordinal));
        Check(!result.ToString().Contains("żądanie wysłane", StringComparison.Ordinal));
        Check(fixture.Requests == 1);
    }

    private static void StaleReadNeverTouchesNewAccount()
    {
        var gate = new Gate();
        var tokens = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            tokens.Add(request.Headers.Authorization?.Parameter);
            gate.EnterAndWait();
            return Json("{}", HttpStatusCode.Unauthorized);
        });
        fixture.Gateway.AllowLogin("SYNTHETIC-ACCESS-B");

        var task = Task.Run(() => fixture.ReadPlaybackCore());
        Check(gate.WaitEntered());
        fixture.LogInAsNewAccount();
        var refreshesAfterLogin = fixture.Refreshes;
        gate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Status == SonosDeviceReadStatus.Discarded && result.Value is null);
        Check(fixture.Refreshes == refreshesAfterLogin);
        Check(fixture.Requests == 1 && tokens.Count == 1 && tokens[0] == Fixture.Access);
        Check(fixture.StoredAccessToken == "SYNTHETIC-ACCESS-B" && fixture.Deletes == 0);
    }

    private static void NewAccountDuringCommandRenewal()
    {
        var refreshGate = new Gate();
        using var fixture = Fixture.Connected(
            _ => Json("{}"),
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));
        fixture.Gateway.AllowLogin("SYNTHETIC-ACCESS-B");
        fixture.Gateway.HoldRefresh(refreshGate);

        var task = Task.Run(() => fixture.SendCore(SonosGroupCommand.Play));
        Check(refreshGate.WaitEntered());
        fixture.LogInAsNewAccount();
        refreshGate.Release();

        var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(result.Status == SonosGroupOperationStatus.Discarded);
        // Stare polecenie NIE poszlo wcale - ani starym, ani nowym biletem.
        Check(!result.RequestSent && fixture.Requests == 0);
        // ZERO wywolan POST, wiec komunikat ma mowic wprost o niewyslaniu,
        // a nie o nieznanym skutku.
        Check(result.Message.Contains("nie zostało wysłane", StringComparison.Ordinal));
        Check(!result.Message.Contains("nieznany", StringComparison.Ordinal));
        Check(result.ToString().Contains("żądania nie wysłano", StringComparison.Ordinal));
        Check(fixture.StoredAccessToken == "SYNTHETIC-ACCESS-B" && fixture.Deletes == 0);
    }

    private static void DiscardedAfterDispose()
    {
        var gate = new Gate();
        var fixture = Fixture.Connected(_ => { gate.EnterAndWait(); return Json("{\"volume\":7}"); });
        try
        {
            var task = Task.Run(() => fixture.ReadVolumeCore());
            Check(gate.WaitEntered());
            fixture.Coordinator.Dispose();
            gate.Release();
            var result = task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(result.Status == SonosDeviceReadStatus.Discarded && result.Value is null);
        }
        finally
        {
            fixture.DisposeTransportOnly();
        }
    }

    private static void CancelingOneWaiterKeepsOtherRefresh()
    {
        var refreshGate = new Gate();
        using var canceled = new CancellationTokenSource();
        using var fixture = Fixture.Connected(
            _ => Json("{\"volume\":3}"),
            expiresInSeconds: 1,
            receivedShift: TimeSpan.FromMinutes(-10));
        fixture.Gateway.HoldRefresh(refreshGate);

        var first = Task.Run(() => fixture.ReadVolumeCore(canceled.Token));
        Check(refreshGate.WaitEntered());
        var second = Task.Run(() => fixture.ReadVolumeCore());

        canceled.Cancel();
        refreshGate.Release();

        var firstResult = first.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        var secondResult = second.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        Check(firstResult.Status is SonosDeviceReadStatus.Canceled or SonosDeviceReadStatus.Success);
        // Cudze odnowienie zyje dalej: drugi odczyt konczy sie normalnie.
        Check(secondResult.Succeeded && secondResult.Value!.Volume == 3);
        Check(fixture.Coordinator.Snapshot.HasCredentials);
    }

    private static void UnpersistedAccountStillWorks()
    {
        using var fixture = Fixture.Connected(_ => Json("{}"), failWrites: true);
        fixture.Gateway.AllowLogin("SYNTHETIC-ACCESS-B");
        fixture.LogInAsNewAccount(expectStored: false);

        var command = fixture.Send(SonosGroupCommand.Play);
        Check(command.Status == SonosGroupOperationStatus.Attempted && command.Accepted);
        Check(command.Snapshot.HasCredentials && !command.Snapshot.IsPersisted);
        Check(fixture.Requests == 1);
    }

    private static void MessagesHideSecrets()
    {
        using var fixture = Fixture.Connected(_ => Json("{}", HttpStatusCode.Forbidden));
        var command = fixture.Send(SonosGroupCommand.Pause);
        Check(!command.Message.Contains(Fixture.Access, StringComparison.Ordinal));
        Check(!command.ToString().Contains(Fixture.Access, StringComparison.Ordinal));
        Check(!command.ToString().Contains(Key, StringComparison.Ordinal));

        var read = fixture.ReadPlayback();
        Check(!read.Message.Contains(Fixture.Access, StringComparison.Ordinal));
        Check(!read.ToString().Contains(Fixture.Access, StringComparison.Ordinal));
        Check(!read.ToString().Contains(Key, StringComparison.Ordinal));

        foreach (var status in Enum.GetValues<SonosGroupOperationStatus>())
        {
            var text = SonosGroupOperationMessages.Describe(status);
            Check(!string.IsNullOrWhiteSpace(text));
            Check(!text.Contains(Fixture.Access, StringComparison.Ordinal) && !text.Contains(Key, StringComparison.Ordinal));
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

    /// <summary>
    /// Blokada zdarzeniowa na TaskCompletionSource z RunContinuationsAsynchronously:
    /// wznowienie NIE biegnie na watku, ktory zwalnia blokade, wiec test nie
    /// przejmuje przypadkiem watku operacji.
    /// </summary>
    internal sealed class Gate
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
            // PRAWDZIWY klient Control API na sztucznym transporcie: autoryzowany
            // GET i POST przechodza caly tor produkcyjny.
            Api = new SonosControlApiGroupApi(client);
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosGroupApi Api { get; }

        internal Gateway Gateway { get; }

        internal int Requests => handler.Count;

        internal int Refreshes => Gateway.RefreshCalls;

        internal string? StoredAccessToken => store.AccessToken;

        internal int Deletes => store.Deletes;

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

        /// <summary>PRAWDZIWA droga nowego logowania, bez podstawiania pol.</summary>
        internal void LogInAsNewAccount(bool expectStored = true)
        {
            Coordinator.BeginLoginAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var login = Coordinator.CheckLoginAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check(login.Snapshot.HasCredentials);
            Check(!expectStored || StoredAccessToken == "SYNTHETIC-ACCESS-B");
        }

        internal SonosGroupReadResult<SonosGroupPlaybackStatus> ReadPlayback() =>
            ReadPlaybackCore();

        internal SonosGroupReadResult<SonosGroupPlaybackStatus> ReadPlaybackCore() =>
            Coordinator.ReadGroupPlaybackAsync(Api, Group, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal SonosGroupReadResult<SonosGroupMetadata> ReadMetadata() =>
            Coordinator.ReadGroupMetadataAsync(Api, Group, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal SonosGroupReadResult<SonosGroupVolume> ReadVolume() => ReadVolumeCore();

        internal SonosGroupReadResult<SonosGroupVolume> ReadVolumeCore(CancellationToken cancellationToken = default) =>
            Coordinator.ReadGroupVolumeAsync(Api, Group, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal SonosGroupCommandResult Send(SonosGroupCommand command) => SendCore(command);

        internal SonosGroupCommandResult SendCore(SonosGroupCommand command) =>
            Coordinator.SendGroupCommandAsync(Api, Group, command, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

        internal SonosGroupCommandResult SetVolume(int volume) =>
            Coordinator.SetGroupVolumeAsync(Api, Group, volume, CancellationToken.None)
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
    internal sealed class Gateway(int? expiresInSeconds, string? refreshToken) : ISonosLoginGateway
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

    /// <summary>Atrapa magazynu w PAMIECI: bez DPAPI i bez pliku.</summary>
    private sealed class Store : ISonosCredentialStore
    {
        private SonosStoredCredentials? record;

        internal void Seed(SonosStoredCredentials value) => record = value;

        internal bool FailWrites { get; set; }

        internal string? AccessToken => record?.Tokens.AccessToken;

        internal int Deletes { get; private set; }

        public SonosCredentialReadOutcome Read() => record is null
            ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
            : SonosCredentialReadOutcome.Ok(record);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
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
