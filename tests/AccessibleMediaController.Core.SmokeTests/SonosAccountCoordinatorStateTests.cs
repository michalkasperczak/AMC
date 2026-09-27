using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

// Dodatkowe testy rodzica: rzeczywisty koordynator, walidacja formatu
// magazynu z produkcji, syntetyczna bramka i dane. Bez sieci i dysku.
internal static class SonosAccountCoordinatorStateTests
{
    private const string Origin = "https://state-probe.invalid/";
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);
    private static SonosLoginBrokerConfiguration Config()
    {
        SonosLoginBrokerConfiguration.TryCreate(Origin, out var configuration);
        return configuration!;
    }
    private static SonosTokens Tokens(string label) => new("SYNTH-AT-" + label, "Bearer", 3600, "SYNTH-RT-" + label, "scope");
    private static SonosStoredCredentials Record(string label) => new(Origin, Tokens(label), Now);
    private static void Assert(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static T Wait<T>(Task<T> task) => task.WaitAsync(Limit).GetAwaiter().GetResult();
    private static TaskCompletionSource<T> Gate<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static SonosAccountCoordinator Coordinator(Gateway gateway, Store store, string origin = Origin) => new(gateway, store, origin, () => Now);

    public static void RefreshPreservesPendingLogin()
    {
        var g = new Gateway(); var s = new Store { Current = Record("old") };
        using var c = Coordinator(g, s);
        c.RestoreOnce(); Assert(Wait(c.BeginLoginAsync(default)).Started, "Kontrola: rozpoczęto nowe logowanie.");
        var generation = c.Snapshot.LoginGeneration;
        var renewed = Wait(c.RefreshAsync(default));
        Assert(renewed.Renewed, "Kontrola: odnowienie aktualnego konta działa.");
        Assert(c.Snapshot.IsAwaitingBrowser && c.Snapshot.LoginGeneration == generation,
            "Odnowienie bieżącego konta nie może ukryć niezakończonej próby nowego logowania.");
        var done = Wait(c.CheckLoginAsync(default));
        Assert(done.Connected && !c.Snapshot.IsAwaitingBrowser, "Kontrola: nadal można zakończyć tę samą próbę logowania.");
    }

    public static void TemporaryFetchFailureCanBeRetried()
    {
        var g = new Gateway(); var s = new Store(); using var c = Coordinator(g, s);
        c.RestoreOnce(); Wait(c.BeginLoginAsync(default));
        g.Fetch = (_, _) => Task.FromResult(SonosLoginResultOutcome.Failure(SonosLoginStatus.RateLimited));
        var limited = Wait(c.CheckLoginAsync(default));
        Assert(limited.LoginStatus == SonosLoginStatus.RateLimited && s.Writes == 0 && s.Deletes == 0,
            "Kontrola: przejściowa odmowa jest rozpoznana i nie zmienia magazynu.");
        Assert(c.Snapshot.IsAwaitingBrowser, "Przejściowa odmowa odbioru nie może zgubić sesji oczekującej na wynik.");
        g.Fetch = (_, _) => Task.FromResult(SonosLoginResultOutcome.Ok(Tokens("retry")));
        var done = Wait(c.CheckLoginAsync(default));
        Assert(done.Connected && g.Starts == 1 && g.Fetches == 2, "Ponowienie odbioru działa bez ponownego Start/logowania w przeglądarce.");
    }

    public static void BrokerOriginMatchesAcceptedConfiguration()
    {
        var g = new Gateway(); var s = new Store();
        // Ta sama wejściowa postać URL jest akceptowana przez konfigurację klienta.
        var withoutSlash = Origin.TrimEnd('/');
        Assert(SonosLoginBrokerConfiguration.TryCreate(withoutSlash, out var cfg) && cfg!.Origin.AbsoluteUri == Origin,
            "Kontrola: klient normalizuje równoważny origin.");
        using var c = Coordinator(g, s, withoutSlash);
        Wait(c.BeginLoginAsync(default)); var done = Wait(c.CheckLoginAsync(default));
        Assert(done.Connected && done.WriteStatus == SonosCredentialWriteStatus.Success,
            "Koordynator musi przekazać do magazynu tak samo znormalizowany origin jak klient.");
    }

    public static void RestoreBrokerMismatchDoesNotMutateStore()
    {
        var g = new Gateway(); var s = new Store { ReadStatus = SonosCredentialReadStatus.BrokerMismatch };
        using var c = Coordinator(g, s); var r = c.RestoreOnce();
        Assert(r.Snapshot.State == SonosAccountState.NeedsLogin && r.Snapshot.Issue == SonosAccountIssue.BrokerMismatch,
            "Obcy zapis jest rozpoznany bez udawania połączonego konta.");
        Assert(s.Reads == 1 && s.Writes == 0 && s.Deletes == 0, "Odczyt obcego zapisu niczego nie nadpisuje ani nie kasuje.");
    }

    public static void ReverseStartKeepsNewerAttempt()
    {
        var a = Gate<SonosLoginStartOutcome>(); var b = Gate<SonosLoginStartOutcome>();
        var g = new Gateway(); var s = new Store(); using var c = Coordinator(g, s);
        g.Start = _ => g.Starts == 1 ? a.Task : b.Task;
        var first = c.BeginLoginAsync(default); var second = c.BeginLoginAsync(default);
        try
        {
            b.SetResult(Started("second")); Assert(Wait(second).Started, "Kontrola: nowsza próba rozpoczęta.");
            a.SetResult(Started("first")); Assert(Wait(first).Discarded, "Starszy Start wraca za późno i jest odrzucony.");
            g.Fetch = (session, _) =>
            {
                Assert(session.SessionId == SessionId("second"), "Odbierana jest sesja nowszej próby.");
                return Task.FromResult(SonosLoginResultOutcome.Ok(Tokens("second")));
            };
            Assert(Wait(c.CheckLoginAsync(default)).Connected, "Kontrola: nowsza próba może się zakończyć.");
        }
        finally { a.TrySetResult(SonosLoginStartOutcome.Failure(SonosLoginStatus.Canceled)); b.TrySetResult(SonosLoginStartOutcome.Failure(SonosLoginStatus.Canceled)); }
    }

    public static void CanceledLoginIgnoresLateSuccess()
    {
        var held = Gate<SonosLoginResultOutcome>(); var g = new Gateway(); var s = new Store();
        using var c = Coordinator(g, s); Wait(c.BeginLoginAsync(default)); g.Fetch = (_, _) => held.Task;
        var check = c.CheckLoginAsync(default);
        try
        {
            c.CancelPendingLogin(); held.SetResult(SonosLoginResultOutcome.Ok(Tokens("late")));
            Assert(Wait(check).Discarded && s.Writes == 0 && !c.Snapshot.HasCredentials,
                "Późny sukces anulowanej próby nie zapisuje ani nie odtwarza konta.");
        }
        finally { held.TrySetResult(SonosLoginResultOutcome.Failure(SonosLoginStatus.Canceled)); }
    }

    public static void Stale401CannotDeleteNewLogin()
    {
        var held = Gate<SonosRefreshOutcome>(); var g = new Gateway(); var s = new Store { Current = Record("old") };
        using var c = Coordinator(g, s); c.RestoreOnce(); g.Refresh = (_, _) => held.Task;
        var refresh = c.RefreshAsync(default);
        try
        {
            Wait(c.BeginLoginAsync(default)); Assert(Wait(c.CheckLoginAsync(default)).Connected, "Kontrola: nowe konto zapisane.");
            var latest = s.Current;
            held.SetResult(SonosRefreshOutcome.Failure(SonosRefreshStatus.ReauthorizationRequired));
            Assert(Wait(refresh).Discarded && s.Deletes == 0 && ReferenceEquals(s.Current, latest),
                "401 starego zestawu nie kasuje nowszego udanego logowania.");
        }
        finally { held.TrySetResult(SonosRefreshOutcome.Failure(SonosRefreshStatus.Canceled)); }
    }

    public static void DisposeCancelsOwnedRefreshWithoutWriting()
    {
        var entered = Gate<bool>(); var g = new Gateway(); var s = new Store { Current = Record("old") };
        using var c = Coordinator(g, s); c.RestoreOnce();
        g.Refresh = async (_, ct) =>
        {
            entered.SetResult(true);
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { return SonosRefreshOutcome.Failure(SonosRefreshStatus.Canceled); }
            return SonosRefreshOutcome.Ok(Tokens("unexpected"));
        };
        var refresh = c.RefreshAsync(default); Wait(entered.Task); c.Dispose();
        Assert(Wait(refresh).Discarded && s.Writes == 0 && s.Deletes == 0,
            "Dispose kończy własne odnowienie bez zapisu ani wylogowania konta.");
    }

    private static string SessionId(string seed) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static SonosLoginStartOutcome Started(string seed)
    {
        var json = JsonSerializer.Serialize(new { session_id = SessionId(seed), authorize_url = "https://api.sonos.com/login/v3/oauth?client_id=synthetic&response_type=code", expires_in = 600 });
        using var client = new SonosLoginClient(Config(), new Handler(json), clock: () => Now);
        return Wait(client.StartAsync(default));
    }
    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
    private sealed class Gateway : ISonosLoginGateway
    {
        public int Starts, Fetches, Refreshes;
        public Func<CancellationToken, Task<SonosLoginStartOutcome>> Start = _ => Task.FromResult(Started("default"));
        public Func<SonosLoginSession, CancellationToken, Task<SonosLoginResultOutcome>> Fetch = (_, _) => Task.FromResult(SonosLoginResultOutcome.Ok(Tokens("login")));
        public Func<string?, CancellationToken, Task<SonosRefreshOutcome>> Refresh = (_, _) => Task.FromResult(SonosRefreshOutcome.Ok(Tokens("renewed")));
        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken ct) { Starts++; return Start(ct); }
        public Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken ct) { Fetches++; return Fetch(session, ct); }
        public Task<SonosRefreshOutcome> RefreshAsync(string? token, CancellationToken ct) { Refreshes++; return Refresh(token, ct); }
    }
    private sealed class Store : ISonosCredentialStore
    {
        public SonosStoredCredentials? Current;
        public SonosCredentialReadStatus ReadStatus = SonosCredentialReadStatus.Success;
        public int Reads, Writes, Deletes;
        public SonosCredentialReadOutcome Read()
        {
            Reads++;
            return ReadStatus != SonosCredentialReadStatus.Success ? SonosCredentialReadOutcome.Failure(ReadStatus)
                : Current is null ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing) : SonosCredentialReadOutcome.Ok(Current);
        }
        public SonosCredentialWriteOutcome Write(SonosStoredCredentials record)
        {
            Writes++;
            var bytes = SonosCredentialSerializer.TrySerialize(record);
            if (bytes is null || record.BrokerOrigin != Config().Origin.AbsoluteUri)
                return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.InvalidRecord);
            Array.Clear(bytes); Current = record; return SonosCredentialWriteOutcome.Ok();
        }
        public bool Delete() { Deletes++; Current = null; return true; }
    }
}
