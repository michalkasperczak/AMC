using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// NAJMNIEJSZY szew miedzy koordynatorem a ODEBRANYM <see cref="SonosLoginClient"/>.
/// Metody sa dokladnie te, ktore klient juz udostepnia - to adapter, nie nowa
/// warstwa transportu i nie miejsce na logike. Dzieki temu testy koordynatora
/// mierza WLASNIE logike Core, a odebrany transport zostaje nietkniety.
/// </summary>
public interface ISonosLoginGateway
{
    Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken);

    Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken cancellationToken);

    Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
}

/// <summary>Adapter na prawdziwego klienta. Nie zmienia ani nie owija jego zachowan.</summary>
public sealed class SonosLoginClientGateway : ISonosLoginGateway
{
    private readonly SonosLoginClient client;

    public SonosLoginClientGateway(SonosLoginClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
        client.StartAsync(cancellationToken);

    public Task<SonosLoginResultOutcome> FetchResultAsync(
        SonosLoginSession session,
        CancellationToken cancellationToken) =>
        client.FetchResultAsync(session, cancellationToken);

    public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken) =>
        client.RefreshAsync(refreshToken, cancellationToken);
}

/// <summary>
/// KOORDYNATOR konta Sonos: odtworzenie zapisu, logowanie, odnawianie, trwaly
/// zapis i jawne wylogowanie. Jedna instancja jest WLASCICIELEM stanu konta w
/// procesie; nie ma tu blokad wieloprocesowych ani CAS - jest poprawne
/// SEKWENCJONOWANIE u wlasciciela.
///
/// Czego tu swiadomie NIE MA (nastepny etap): UI, uruchamiania przegladarki,
/// timerow, pollingu, automatycznego planowania odnowien, workerow w tle,
/// powtorek HTTP i frameworka DI.
///
/// DWIE NIEZALEZNE GENERACJE sa rdzeniem ochrony przed spoznionymi odpowiedziami:
/// generacja ZESTAWU POSWIADCZEN rosnie tylko wtedy, gdy zmienia sie uzyteczny
/// zestaw (odtworzenie, udane logowanie, udane odnowienie, uniewaznienie,
/// wylogowanie), a generacja PROBY LOGOWANIA rosnie przy rozpoczeciu i anulowaniu
/// proby. Dlatego samo rozpoczecie albo anulowanie logowania NIE porzuca
/// uzytecznego wyniku odnowienia biezacego konta, a nowe udane logowanie lub
/// wylogowanie czyni starsze odpowiedzi bezskutecznymi.
///
/// Stan pamieci I operacje magazynu zmieniamy pod JEDNA blokada. Kontrakt
/// <see cref="ISonosCredentialStore"/> jest synchroniczny, wiec nie ma tu awaitu
/// w srodku sekcji krytycznej: sprawdzenie generacji i I/O sa NIEROZDZIELNE, a nie
/// tylko "sprawdzone przed awaitem". Pod blokada nie wolamy niczyich callbackow,
/// a na zewnatrz oddajemy wylacznie niemutowalne migawki.
/// </summary>
public sealed partial class SonosAccountCoordinator : IDisposable
{
    private readonly ISonosLoginGateway gateway;
    private readonly ISonosCredentialStore store;
    private readonly string brokerOrigin;
    private readonly Func<DateTimeOffset> clock;
    private readonly object gate = new();

    // --- stan chroniony blokada ---
    private SonosStoredCredentials? current;
    private bool persisted;
    private bool persistedRecordMayRemain;
    private SonosAccountState state = SonosAccountState.NoAccount;
    private SonosAccountIssue issue = SonosAccountIssue.None;
    private long credentialGeneration;
    private long loginGeneration;
    private bool restoreAttempted;
    private SonosLoginSession? pendingSession;
    private long pendingSessionLoginGeneration = -1;
    private bool awaitingBrowser;

    /// <summary>
    /// Stan, ktory obowiazywal PRZED wejsciem w oczekiwanie na przegladarke.
    /// Zakonczona proba wraca dokladnie do niego, zeby nieudane logowanie nie
    /// zamienilo rozpoznanego NeedsLogin w ciche NoAccount.
    /// </summary>
    private SonosAccountState stateBeforeAwaitingBrowser = SonosAccountState.NoAccount;
    private bool disposed;

    // Wspolne odnowienie: JEDNO zapytanie na generacje zestawu.
    private Task<SonosAccountRefreshResult>? inflightRefresh;
    private long inflightRefreshGeneration = -1;
    private CancellationTokenSource? inflightRefreshCts;

    /// <summary>Zycie wlasnych operacji: Dispose przerywa je, nie przerywajac tokenow wolajacych.</summary>
    private readonly CancellationTokenSource lifetime = new();

    public SonosAccountCoordinator(
        ISonosLoginGateway gateway,
        ISonosCredentialStore store,
        string brokerOrigin,
        Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(store);
        if (!SonosLoginBrokerConfiguration.TryCreate(brokerOrigin, out var configuration) || configuration is null)
        {
            throw new ArgumentException("Nieprawidłowy adres serwera logowania Sonos.", nameof(brokerOrigin));
        }

        this.gateway = gateway;
        this.store = store;
        this.brokerOrigin = configuration.Origin.AbsoluteUri;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>BEZPIECZNA migawka: bez tokenow, do odczytu poza blokada.</summary>
    public SonosAccountSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                return CreateSnapshot();
            }
        }
    }

    // ================= 1. odtworzenie z magazynu =================

    /// <summary>
    /// JAWNE, JEDNORAZOWE odtworzenie konta. Kolejne wywolania nie czytaja magazynu
    /// (<see cref="SonosAccountRestoreResult.Performed"/> = false).
    ///
    /// Brak zapisu (<see cref="SonosCredentialReadStatus.Missing"/>) NIE jest bledem.
    /// Invalid, ReadFailure i BrokerMismatch daja rozpoznany, bezpieczny wynik i
    /// NIE usuwaja oraz NIE nadpisuja pliku. Moment otrzymania, termin waznosci i
    /// token odswiezania wracaja dokladnie takie, jakie byly zapisane - odczyt
    /// NIE odnawia TTL, a nieznana waznosc zostaje nieznana.
    /// </summary>
    public SonosAccountRestoreResult RestoreOnce()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (restoreAttempted)
            {
                return new SonosAccountRestoreResult(false, SonosCredentialReadStatus.Success, CreateSnapshot());
            }

            restoreAttempted = true;
            var outcome = store.Read();
            switch (outcome.Status)
            {
                case SonosCredentialReadStatus.Success when outcome.Credentials is not null:
                    current = outcome.Credentials;
                    persisted = true;
                    persistedRecordMayRemain = false;
                    credentialGeneration++;
                    state = SonosAccountState.Connected;
                    issue = SonosAccountIssue.None;
                    break;

                case SonosCredentialReadStatus.Missing:
                    state = SonosAccountState.NoAccount;
                    issue = SonosAccountIssue.None;
                    break;

                case SonosCredentialReadStatus.Invalid:
                    state = SonosAccountState.NeedsLogin;
                    issue = SonosAccountIssue.InvalidStoredRecord;
                    persistedRecordMayRemain = true;
                    break;

                case SonosCredentialReadStatus.BrokerMismatch:
                    state = SonosAccountState.NeedsLogin;
                    issue = SonosAccountIssue.BrokerMismatch;
                    persistedRecordMayRemain = true;
                    break;

                default:
                    // ReadFailure: blad MAGAZYNU, nie wyrok o waznosci tokenow.
                    state = SonosAccountState.StoreFailure;
                    issue = SonosAccountIssue.ReadFailure;
                    persistedRecordMayRemain = true;
                    break;
            }

            return new SonosAccountRestoreResult(true, outcome.Status, CreateSnapshot());
        }
    }

    // ================= 2. logowanie =================

    /// <summary>
    /// Rozpoczyna probe logowania przez ODEBRANY Start. Udana odpowiedz daje
    /// bezpieczny <see cref="SonosAccountLoginStartResult.AuthorizeUri"/> i probe
    /// oczekujaca - ale Core NIE uruchamia przegladarki.
    ///
    /// Rozpoczecie NOWSZEJ proby uniewaznia spoznione odpowiedzi starszej. NIE
    /// rusza generacji zestawu, wiec nie porzuca trwajacego odnowienia konta.
    /// </summary>
    public async Task<SonosAccountLoginStartResult> BeginLoginAsync(CancellationToken cancellationToken)
    {
        long myLoginGeneration;
        lock (gate)
        {
            ThrowIfDisposed();
            myLoginGeneration = ++loginGeneration;
            ClearPendingLoginLocked();
        }

        using var linked = Link(cancellationToken);
        var outcome = await gateway.StartAsync(linked.Token).ConfigureAwait(false);

        lock (gate)
        {
            if (disposed || myLoginGeneration != loginGeneration)
            {
                // Odpowiedz SPOZNIONA: nowsza proba albo anulowanie wyprzedzily ja.
                // Nie instalujemy sesji i nie ruszamy konta.
                return new SonosAccountLoginStartResult(
                    outcome.Status, started: false, discarded: true, authorizeUri: null, CreateSnapshot());
            }

            if (!outcome.Succeeded || outcome.Session is null)
            {
                // Nieudane ROZPOCZECIE nie kasuje dotychczasowego dobrego konta.
                return new SonosAccountLoginStartResult(
                    outcome.Status, started: false, discarded: false, authorizeUri: null, CreateSnapshot());
            }

            pendingSession = outcome.Session;
            pendingSessionLoginGeneration = myLoginGeneration;
            awaitingBrowser = true;
            if (current is null && state is SonosAccountState.NoAccount or SonosAccountState.NeedsLogin)
            {
                stateBeforeAwaitingBrowser = state;
                state = SonosAccountState.AwaitingBrowser;
            }

            return new SonosAccountLoginStartResult(
                outcome.Status,
                started: true,
                discarded: false,
                outcome.Session.AuthorizeUri,
                CreateSnapshot());
        }
    }

    /// <summary>
    /// JEDNORAZOWE sprawdzenie wyniku logowania: dokladnie jedno wywolanie Fetch,
    /// bez pollingu, timera i powtorek HTTP.
    ///
    /// Pending ZOSTAWIA probe oczekujaca. Success aktywuje NOWY zestaw i zapisuje go
    /// przez magazyn. Denied, Canceled i Expired konczą tylko PROBE - dotychczasowe
    /// dobre konto zostaje nietkniete.
    ///
    /// Swiadomie NIE ma tu wczesnego wyjscia po ExpiresAt sesji: backend ma osobny
    /// TTL gotowego wyniku i Fetch po zakonczeniu TTL jest juz odebrany.
    /// </summary>
    public async Task<SonosAccountLoginCheckResult> CheckLoginAsync(CancellationToken cancellationToken)
    {
        SonosLoginSession? session;
        long myLoginGeneration;
        lock (gate)
        {
            ThrowIfDisposed();
            session = pendingSession;
            myLoginGeneration = pendingSessionLoginGeneration;
            if (session is null)
            {
                return new SonosAccountLoginCheckResult(
                    SonosLoginStatus.Canceled,
                    connected: false,
                    stillWaiting: false,
                    discarded: false,
                    hadPendingLogin: false,
                    writeStatus: null,
                    CreateSnapshot());
            }
        }

        using var linked = Link(cancellationToken);
        var outcome = await gateway.FetchResultAsync(session, linked.Token).ConfigureAwait(false);

        lock (gate)
        {
            if (disposed
                || myLoginGeneration != loginGeneration
                || !ReferenceEquals(pendingSession, session))
            {
                // Anulowana, wyprzedzona albo JUZ ZAKONCZONA proba: spozniona
                // odpowiedz (takze Pending) nie wraca do konta i nie wskrzesza
                // oczekiwania na przegladarke, bo tej sesji juz nie ma.
                return new SonosAccountLoginCheckResult(
                    outcome.Status,
                    connected: false,
                    stillWaiting: false,
                    discarded: true,
                    hadPendingLogin: true,
                    writeStatus: null,
                    CreateSnapshot());
            }

            if (outcome.Status == SonosLoginStatus.Pending)
            {
                awaitingBrowser = true;
                return new SonosAccountLoginCheckResult(
                    outcome.Status,
                    connected: false,
                    stillWaiting: true,
                    discarded: false,
                    hadPendingLogin: true,
                    writeStatus: null,
                    CreateSnapshot());
            }

            if (!outcome.Succeeded || outcome.Tokens is null)
            {
                // Tylko zakonczona lub anulowana proba traci sesje. Przy
                // przejsciowym bledzie odbioru zachowujemy dowod, aby uzytkownik
                // mogl ponowic Fetch bez ponownego logowania w przegladarce.
                var terminal = outcome.Status is SonosLoginStatus.Denied
                    or SonosLoginStatus.Canceled or SonosLoginStatus.Expired;
                if (terminal)
                {
                    ClearPendingLoginLocked();
                }
                return new SonosAccountLoginCheckResult(
                    outcome.Status,
                    connected: false,
                    stillWaiting: !terminal,
                    discarded: false,
                    hadPendingLogin: true,
                    writeStatus: null,
                    CreateSnapshot());
            }

            ClearPendingLoginLocked();
            var write = InstallLocked(outcome.Tokens);
            return new SonosAccountLoginCheckResult(
                outcome.Status,
                connected: current is not null,
                stillWaiting: false,
                discarded: false,
                hadPendingLogin: true,
                write,
                CreateSnapshot());
        }
    }

    /// <summary>
    /// Anuluje probe logowania. Uniewaznia spoznione odpowiedzi tej proby i NIE
    /// rusza generacji zestawu, wiec nie porzuca odnawiania biezacego konta.
    /// </summary>
    public SonosAccountSnapshot CancelPendingLogin()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            loginGeneration++;
            ClearPendingLoginLocked();
            return CreateSnapshot();
        }
    }

    // ================= 3-4. odnawianie =================

    /// <summary>
    /// Odnawia dostep przez ISTNIEJACY RefreshAsync. Dla TEJ SAMEJ generacji zestawu
    /// leci DOKLADNIE JEDNO zapytanie: kolejni chetni DOLACZAJA do trwajacego, wiec
    /// nie ma dwoch zapytan tym samym rotowanym tokenem. Token odswiezania jest
    /// traktowany jako OPAQUE i wraca caly zestaw.
    ///
    /// Uniewaznic biezace poswiadczenia moze WYLACZNIE
    /// <see cref="SonosRefreshStatus.ReauthorizationRequired"/> (dokladne 401) i tylko
    /// dla TEJ SAMEJ generacji. Wszystko inne (413, 429, 503, 502, transport,
    /// niezgodny JSON, anulowanie) NIE kasuje niczego.
    /// </summary>
    public Task<SonosAccountRefreshResult> RefreshAsync(CancellationToken cancellationToken)
    {
        Task<SonosAccountRefreshResult> shared;
        bool joined;
        TaskCompletionSource<SonosAccountRefreshResult>? owned = null;
        var generation = 0L;
        string? refreshToken = null;
        CancellationTokenSource? cts = null;
        lock (gate)
        {
            ThrowIfDisposed();
            if (current is null)
            {
                return Task.FromResult(new SonosAccountRefreshResult(
                    SonosRefreshStatus.InvalidLocalToken,
                    renewed: false, discarded: false, joined: false, waiterCanceled: false,
                    writeStatus: null, CreateSnapshot()));
            }

            if (!current.HasRefreshToken)
            {
                // Brak RT to nie awaria sieci: zadnego zapytania HTTP i zadnej kasacji.
                return Task.FromResult(new SonosAccountRefreshResult(
                    SonosRefreshStatus.InvalidLocalToken,
                    renewed: false, discarded: false, joined: false, waiterCanceled: false,
                    writeStatus: null, CreateSnapshot()));
            }

            if (inflightRefresh is not null && inflightRefreshGeneration == credentialGeneration)
            {
                // TRWAJACEGO zapytania innego wolajacego NIE anulujemy - ten, kto
                // zrezygnowal, po prostu na nie nie czeka.
                shared = inflightRefresh;
                joined = true;
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                // Wolajacy zrezygnowal JESZCZE PRZED startem: nie zakladamy zadnego
                // nowego zapytania do bramki.
                return Task.FromResult(new SonosAccountRefreshResult(
                    SonosRefreshStatus.Canceled,
                    renewed: false, discarded: false, joined: false, waiterCanceled: true,
                    writeStatus: null, CreateSnapshot()));
            }
            else
            {
                generation = credentialGeneration;
                refreshToken = current.Tokens.RefreshToken;
                cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                // WSPOLNY placeholder jest opublikowany JESZCZE PRZED wywolaniem
                // bramki, a samo zapytanie startuje POZA blokada. Dzieki temu
                // bramka wykonana synchronicznie do pierwszego awaitu nie moze ani
                // wolac naszych callbackow pod lock, ani spowodowac drugiego
                // zapytania tej samej generacji.
                owned = new TaskCompletionSource<SonosAccountRefreshResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                inflightRefreshCts = cts;
                inflightRefreshGeneration = generation;
                inflightRefresh = owned.Task;
                shared = owned.Task;
                joined = false;
            }
        }

        if (owned is not null)
        {
            StartRefreshOutsideGate(owned, generation, refreshToken, cts!);
        }

        return AwaitSharedAsync(shared, joined, cancellationToken);
    }

    /// <summary>
    /// Uruchamia wspolne odnowienie POZA blokada i przekazuje jego wynik do
    /// opublikowanego wczesniej placeholdera.
    /// </summary>
    private void StartRefreshOutsideGate(
        TaskCompletionSource<SonosAccountRefreshResult> owned,
        long generation,
        string? refreshToken,
        CancellationTokenSource cts)
    {
        Task<SonosAccountRefreshResult> work;
        try
        {
            work = RunRefreshAsync(generation, refreshToken, cts);
        }
        catch (Exception exception)
        {
            owned.TrySetException(exception);
            return;
        }

        work.ContinueWith(
            static (finished, state) =>
            {
                var target = (TaskCompletionSource<SonosAccountRefreshResult>)state!;
                if (finished.IsFaulted)
                {
                    target.TrySetException(finished.Exception!.InnerExceptions);
                }
                else if (finished.IsCanceled)
                {
                    target.TrySetCanceled();
                }
                else
                {
                    target.TrySetResult(finished.Result);
                }
            },
            owned,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<SonosAccountRefreshResult> RunRefreshAsync(
        long generation,
        string? refreshToken,
        CancellationTokenSource cts)
    {
        try
        {
            var outcome = await gateway.RefreshAsync(refreshToken, cts.Token).ConfigureAwait(false);

            lock (gate)
            {
                var stale = generation != credentialGeneration;
                if (stale || disposed)
                {
                    // SPOZNIONA odpowiedz starszej generacji: ani sukces nie zapisze
                    // starszego zestawu, ani 401 nie skasuje nowszego.
                    return new SonosAccountRefreshResult(
                        outcome.Status,
                        renewed: false, discarded: true, joined: false, waiterCanceled: false,
                        writeStatus: null, CreateSnapshot());
                }

                if (outcome.Succeeded && outcome.Tokens is not null)
                {
                    var write = InstallLocked(outcome.Tokens);
                    return new SonosAccountRefreshResult(
                        outcome.Status,
                        renewed: current is not null && credentialGeneration != generation,
                        discarded: false, joined: false, waiterCanceled: false,
                        write, CreateSnapshot());
                }

                if (outcome.Status == SonosRefreshStatus.ReauthorizationRequired)
                {
                    InvalidateLocked();
                    return new SonosAccountRefreshResult(
                        outcome.Status,
                        renewed: false, discarded: false, joined: false, waiterCanceled: false,
                        writeStatus: null, CreateSnapshot());
                }

                // Przejsciowe i kontraktowe bledy ZACHOWUJA zestaw bez zmian.
                return new SonosAccountRefreshResult(
                    outcome.Status,
                    renewed: false, discarded: false, joined: false, waiterCanceled: false,
                    writeStatus: null, CreateSnapshot());
            }
        }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(inflightRefreshCts, cts))
                {
                    inflightRefresh = null;
                    inflightRefreshGeneration = -1;
                    inflightRefreshCts = null;
                }
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// Czeka na WSPOLNE odnowienie z wlasnym tokenem wolajacego. Rezygnacja tego
    /// wolajacego NIE anuluje operacji pozostalym - wspolne zapytanie idzie dalej.
    /// Rejestracja i pomocniczy Task sa zawsze zwalniane.
    /// </summary>
    private async Task<SonosAccountRefreshResult> AwaitSharedAsync(
        Task<SonosAccountRefreshResult> shared,
        bool joined,
        CancellationToken cancellationToken)
    {
        SonosAccountRefreshResult result;
        if (!cancellationToken.CanBeCanceled)
        {
            result = await shared.ConfigureAwait(false);
            return joined ? WithJoined(result) : result;
        }

        var abandoned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.Register(() => abandoned.TrySetResult(true))
            .ConfigureAwait(false);

        var finished = await Task.WhenAny(shared, abandoned.Task).ConfigureAwait(false);
        if (!ReferenceEquals(finished, shared))
        {
            return new SonosAccountRefreshResult(
                SonosRefreshStatus.Canceled,
                renewed: false, discarded: false, joined: joined, waiterCanceled: true,
                writeStatus: null, Snapshot);
        }

        result = await shared.ConfigureAwait(false);
        return joined ? WithJoined(result) : result;
    }

    private static SonosAccountRefreshResult WithJoined(SonosAccountRefreshResult result) =>
        new(result.RefreshStatus, result.Renewed, result.Discarded, joined: true, waiterCanceled: false,
            result.WriteStatus, result.Snapshot);

    // ================= 5. ponowienie samego zapisu =================

    /// <summary>
    /// Ponawia WYLACZNIE trwaly zapis zestawu, ktory jest w pamieci i nie zostal
    /// zapisany. ZERO zapytan HTTP i zero odnawiania - stary token odswiezania mogl
    /// juz stracic waznosc u Sonosa, wiec nie ma do czego wracac.
    /// </summary>
    public SonosAccountPersistRetryResult RetryPersist()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (current is null || persisted)
            {
                return new SonosAccountPersistRetryResult(false, null, CreateSnapshot());
            }

            var write = store.Write(current);
            ApplyWriteLocked(write.Status);
            return new SonosAccountPersistRetryResult(true, write.Status, CreateSnapshot());
        }
    }

    // ================= 4. jawne wylogowanie =================

    /// <summary>
    /// JAWNE wylogowanie zgodne z wola uzytkownika: usuwa biezacy zapis i uniewaznia
    /// spoznione odpowiedzi (nowa generacja zestawu).
    ///
    /// Gdy Delete zwroci false, NIE udajemy udanego wylogowania: zestaw przestaje byc
    /// uzywany w pamieci, ale wynik mowi wprost, ze zapis MOZE nadal lezec na dysku
    /// (<see cref="SonosAccountSnapshot.PersistedRecordMayRemain"/>) i stan to
    /// <see cref="SonosAccountState.StoreFailure"/> z
    /// <see cref="SonosAccountIssue.DeleteFailure"/>. Uzytkownik moze ponowic.
    ///
    /// Nie obiecujemy zadnego wycofania po stronie USLUGI zdalnej - jesli dostawca
    /// obrocil token, lokalne dzialania tego nie odkrecaja.
    /// </summary>
    public SonosAccountDisconnectResult Disconnect()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            credentialGeneration++;
            loginGeneration++;
            ClearPendingLoginLocked();
            var deleted = store.Delete();
            current = null;
            persisted = false;
            if (deleted)
            {
                persistedRecordMayRemain = false;
                state = SonosAccountState.NoAccount;
                issue = SonosAccountIssue.None;
            }
            else
            {
                persistedRecordMayRemain = true;
                state = SonosAccountState.StoreFailure;
                issue = SonosAccountIssue.DeleteFailure;
            }

            return new SonosAccountDisconnectResult(deleted, !deleted, CreateSnapshot());
        }
    }

    // ================= wspolne, pod blokada =================

    /// <summary>
    /// Instaluje NOWY zestaw: najpierw pamiec i nowa generacja, potem trwaly zapis.
    ///
    /// Gdy zapis padnie (<see cref="SonosCredentialWriteStatus.WriteFailure"/>),
    /// zestaw ZOSTAJE w pamieci jako niezapisany - NIE wracamy do starego tokenu
    /// odswiezania, bo dostawca mogl go juz obrocic. InvalidRecord to NIE awaria
    /// dysku: takiego rekordu nie instalujemy jako dzialajacego konta, a decyduje o
    /// tym ISTNIEJACA polityka magazynu, nie kopia walidatorow.
    /// </summary>
    private SonosCredentialWriteStatus InstallLocked(SonosTokens tokens)
    {
        var record = new SonosStoredCredentials(brokerOrigin, tokens, clock().ToUniversalTime());
        var write = store.Write(record);
        if (write.Status == SonosCredentialWriteStatus.InvalidRecord)
        {
            credentialGeneration++;
            current = null;
            persisted = false;
            state = SonosAccountState.NeedsLogin;
            issue = SonosAccountIssue.InvalidRecord;
            return write.Status;
        }

        credentialGeneration++;
        current = record;
        // Odnowienie konta nie konczy niezaleznej proby nowego logowania.
        // CheckLoginAsync sam usuwa swoja sesje przed instalacja jej wyniku.
        ApplyWriteLocked(write.Status);
        return write.Status;
    }

    private void ApplyWriteLocked(SonosCredentialWriteStatus status)
    {
        if (status == SonosCredentialWriteStatus.Success)
        {
            persisted = true;
            persistedRecordMayRemain = false;
            state = SonosAccountState.Connected;
            issue = SonosAccountIssue.None;
            return;
        }

        if (status == SonosCredentialWriteStatus.InvalidRecord)
        {
            persisted = false;
            state = SonosAccountState.NeedsLogin;
            issue = SonosAccountIssue.InvalidRecord;
            return;
        }

        // Dziala, ale NIE jest zapisany - jawny stan zamiast cichej utraty tokenu.
        persisted = false;
        state = SonosAccountState.Connected;
        issue = SonosAccountIssue.WriteFailure;
    }

    /// <summary>
    /// Uniewaznienie po dokladnym 401 tej samej generacji. Bezpieczne stany przy
    /// nieudanym Delete: zestaw przestaje byc uzywany (jest martwy u dostawcy),
    /// stan to NeedsLogin, a <see cref="SonosAccountIssue.DeleteFailure"/> i
    /// PersistedRecordMayRemain mowia, ze martwy rekord MOZE zostac na dysku do
    /// nadpisania przy nastepnym udanym logowaniu.
    /// </summary>
    private void InvalidateLocked()
    {
        credentialGeneration++;
        var deleted = store.Delete();
        current = null;
        persisted = false;
        state = SonosAccountState.NeedsLogin;
        if (deleted)
        {
            persistedRecordMayRemain = false;
            issue = SonosAccountIssue.Reauthorization;
        }
        else
        {
            persistedRecordMayRemain = true;
            issue = SonosAccountIssue.DeleteFailure;
        }
    }

    private void ClearPendingLoginLocked()
    {
        pendingSession = null;
        pendingSessionLoginGeneration = -1;
        awaitingBrowser = false;
        if (state == SonosAccountState.AwaitingBrowser)
        {
            // ZAKONCZONA proba nie moze zostawiac migawki, ktora kaze dokonczyc
            // logowanie w przegladarce, bo zadnej sesji juz nie ma. Wracamy do
            // stanu sprzed oczekiwania: rozpoznany NeedsLogin zostaje NeedsLogin,
            // a nie zamienia sie w ciche "nie ma konta".
            state = current is not null
                ? SonosAccountState.Connected
                : stateBeforeAwaitingBrowser == SonosAccountState.AwaitingBrowser
                    ? SonosAccountState.NoAccount
                    : stateBeforeAwaitingBrowser;
        }
    }

    private SonosAccountSnapshot CreateSnapshot() =>
        new(
            state,
            issue,
            current is not null,
            current?.HasRefreshToken ?? false,
            persisted,
            awaitingBrowser,
            persistedRecordMayRemain,
            current?.ReceivedAtUtc,
            current?.ExpiresAtUtc,
            current?.IsExpiryKnown ?? false,
            credentialGeneration,
            loginGeneration);

    private CancellationTokenSource Link(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(SonosAccountCoordinator));
        }
    }

    /// <summary>
    /// Przerywa WLASNE operacje koordynatora i zwalnia jego CTS. Nie czeka na
    /// zawieszenia: trwajace odnowienie widzi anulowanie, a jego finally zwalnia
    /// swoj token.
    /// </summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ClearPendingLoginLocked();
        }

        try
        {
            lifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Zwolniony token nie jest bledem zamykania.
        }

        lifetime.Dispose();
    }
}
