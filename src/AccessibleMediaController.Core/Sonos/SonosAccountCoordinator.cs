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

    /// <summary>
    /// Znacznik LOKALNEGO CYKLU PODLACZENIA konta (B2a). Rosnie WYLACZNIE przy
    /// zastapieniu konta (udane nowe logowanie instalujace zestaw) i przy realnym
    /// odlaczeniu biezacego konta. Zwykle odnowienie i rotacja zestawu go nie ruszaja.
    /// Nie jest utrwalany: zakres zycia to jedna instancja koordynatora.
    /// </summary>
    private long accountBindingGeneration;
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
    private Task<RefreshRun>? inflightRefresh;
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
            // NOWE logowanie zastepuje dotychczasowe podlaczenie konta.
            var write = InstallLocked(outcome.Tokens, replacesAccount: true);
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
    public async Task<SonosAccountRefreshResult> RefreshAsync(CancellationToken cancellationToken) =>
        (await RefreshCoreAsync(null, cancellationToken).ConfigureAwait(false)).Result;

    /// <summary>
    /// ATRYBUOWALNY wynik JEDNEGO przebiegu odnowienia. Obok publicznego wyniku
    /// niesie fakty przypisane DOKLADNIE temu odnowieniu: dla jakiej generacji
    /// wystartowalo, jaki zestaw ZAINSTALOWALO i czy to ono uniewaznilo konto.
    /// Dzieki temu wolajacy nie legalizuje swojej operacji dowolna, biezaca
    /// migawka konta ani heurystyka "generacja+1".
    /// </summary>
    private readonly struct RefreshRun
    {
        internal RefreshRun(
            SonosAccountRefreshResult result,
            long startedGeneration,
            SonosStoredCredentials? installed,
            long installedGeneration,
            bool invalidated,
            bool generationMismatch)
        {
            Result = result;
            StartedGeneration = startedGeneration;
            InstalledCredentials = installed;
            InstalledGeneration = installedGeneration;
            Invalidated = invalidated;
            GenerationMismatch = generationMismatch;
        }

        internal SonosAccountRefreshResult Result { get; }

        internal long StartedGeneration { get; }

        /// <summary>DOKLADNY zestaw zainstalowany TYM odnowieniem; null, gdy nic nie zainstalowano.</summary>
        internal SonosStoredCredentials? InstalledCredentials { get; }

        internal long InstalledGeneration { get; }

        /// <summary>TO odnowienie uniewaznilo konto (dokladne 401 tej samej generacji).</summary>
        internal bool Invalidated { get; }

        /// <summary>
        /// Oczekiwana generacja NIE obowiazywala w chwili sprawdzenia pod blokada:
        /// nie wystartowalo ani nie dolaczylo zadne odnowienie.
        /// </summary>
        internal bool GenerationMismatch { get; }
    }

    /// <summary>
    /// Wspolna logika odnowienia - JEDNA, bez kopii dla odczytu urzadzen.
    ///
    /// <paramref name="expectedGeneration"/> to WARUNEK WSTEPNY sprawdzany
    /// ATOMOWO pod ta sama blokada, pod ktora odnowienie startuje albo dolacza
    /// do trwajacego. Gdy generacja zestawu jest juz inna, NIE leci zadne
    /// zapytanie do bramki i nie ma dolaczenia - zamyka to okienko miedzy
    /// sprawdzeniem a startem. Null = zwykle, reczne odnowienie biezacego
    /// zestawu, bez warunku.
    /// </summary>
    private Task<RefreshRun> RefreshCoreAsync(long? expectedGeneration, CancellationToken cancellationToken)
    {
        Task<RefreshRun> shared;
        bool joined;
        TaskCompletionSource<RefreshRun>? owned = null;
        var generation = 0L;
        string? refreshToken = null;
        CancellationTokenSource? cts = null;
        lock (gate)
        {
            ThrowIfDisposed();
            if (expectedGeneration is { } expected && credentialGeneration != expected)
            {
                // SWIEZA generacja NIE legalizuje starszej operacji: jej wlasciciel
                // dostaje jawne porzucenie, a konto nie widzi zadnego zapytania.
                return Task.FromResult(new RefreshRun(
                    new SonosAccountRefreshResult(
                        SonosRefreshStatus.Canceled,
                        renewed: false, discarded: true, joined: false, waiterCanceled: false,
                        writeStatus: null, CreateSnapshot()),
                    expected, null, expected, invalidated: false, generationMismatch: true));
            }

            if (current is null)
            {
                return Task.FromResult(Plain(new SonosAccountRefreshResult(
                    SonosRefreshStatus.InvalidLocalToken,
                    renewed: false, discarded: false, joined: false, waiterCanceled: false,
                    writeStatus: null, CreateSnapshot()), credentialGeneration));
            }

            if (!current.HasRefreshToken)
            {
                // Brak RT to nie awaria sieci: zadnego zapytania HTTP i zadnej kasacji.
                return Task.FromResult(Plain(new SonosAccountRefreshResult(
                    SonosRefreshStatus.InvalidLocalToken,
                    renewed: false, discarded: false, joined: false, waiterCanceled: false,
                    writeStatus: null, CreateSnapshot()), credentialGeneration));
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
                return Task.FromResult(Plain(new SonosAccountRefreshResult(
                    SonosRefreshStatus.Canceled,
                    renewed: false, discarded: false, joined: false, waiterCanceled: true,
                    writeStatus: null, CreateSnapshot()), credentialGeneration));
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
                owned = new TaskCompletionSource<RefreshRun>(
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
        TaskCompletionSource<RefreshRun> owned,
        long generation,
        string? refreshToken,
        CancellationTokenSource cts)
    {
        Task<RefreshRun> work;
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
                var target = (TaskCompletionSource<RefreshRun>)state!;
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

    private async Task<RefreshRun> RunRefreshAsync(
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
                    return new RefreshRun(
                        new SonosAccountRefreshResult(
                            outcome.Status,
                            renewed: false, discarded: true, joined: false, waiterCanceled: false,
                            writeStatus: null, CreateSnapshot()),
                        generation, null, generation, invalidated: false, generationMismatch: false);
                }

                if (outcome.Succeeded && outcome.Tokens is not null)
                {
                    // ODNOWIENIE tego samego konta: nowy zestaw, to samo podlaczenie.
                    var write = InstallLocked(outcome.Tokens, replacesAccount: false);
                    var installed = current;
                    return new RefreshRun(
                        new SonosAccountRefreshResult(
                            outcome.Status,
                            renewed: current is not null && credentialGeneration != generation,
                            discarded: false, joined: false, waiterCanceled: false,
                            write, CreateSnapshot()),
                        generation, installed, credentialGeneration, invalidated: false, generationMismatch: false);
                }

                if (outcome.Status == SonosRefreshStatus.ReauthorizationRequired)
                {
                    InvalidateLocked();
                    return new RefreshRun(
                        new SonosAccountRefreshResult(
                            outcome.Status,
                            renewed: false, discarded: false, joined: false, waiterCanceled: false,
                            writeStatus: null, CreateSnapshot()),
                        generation, null, credentialGeneration, invalidated: true, generationMismatch: false);
                }

                // Przejsciowe i kontraktowe bledy ZACHOWUJA zestaw bez zmian.
                return new RefreshRun(
                    new SonosAccountRefreshResult(
                        outcome.Status,
                        renewed: false, discarded: false, joined: false, waiterCanceled: false,
                        writeStatus: null, CreateSnapshot()),
                    generation, null, credentialGeneration, invalidated: false, generationMismatch: false);
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
    private async Task<RefreshRun> AwaitSharedAsync(
        Task<RefreshRun> shared,
        bool joined,
        CancellationToken cancellationToken)
    {
        RefreshRun result;
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
            // Rezygnacja WOLAJACEGO: nic nie zostalo zainstalowane z jego punktu
            // widzenia, wiec nie dostaje zadnego zestawu do legalizacji.
            return Plain(
                new SonosAccountRefreshResult(
                    SonosRefreshStatus.Canceled,
                    renewed: false, discarded: false, joined: joined, waiterCanceled: true,
                    writeStatus: null, Snapshot),
                Snapshot.CredentialGeneration);
        }

        result = await shared.ConfigureAwait(false);
        return joined ? WithJoined(result) : result;
    }

    /// <summary>Wynik bez wlasnego odnowienia: nic nie zainstalowano, nic nie uniewazniono.</summary>
    private static RefreshRun Plain(SonosAccountRefreshResult result, long generation) =>
        new(result, generation, null, generation, invalidated: false, generationMismatch: false);

    private static RefreshRun WithJoined(RefreshRun run) =>
        new(
            new SonosAccountRefreshResult(
                run.Result.RefreshStatus, run.Result.Renewed, run.Result.Discarded, joined: true,
                waiterCanceled: false, run.Result.WriteStatus, run.Result.Snapshot),
            run.StartedGeneration,
            run.InstalledCredentials,
            run.InstalledGeneration,
            run.Invalidated,
            run.GenerationMismatch);

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
            if (current is not null)
            {
                // JAWNE odlaczenie dzialajacego konta konczy biezace podlaczenie -
                // takze wtedy, gdy Delete zawiedzie i rekord moze zostac na dysku.
                accountBindingGeneration++;
            }

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
    private SonosCredentialWriteStatus InstallLocked(SonosTokens tokens, bool replacesAccount)
    {
        var record = new SonosStoredCredentials(brokerOrigin, tokens, clock().ToUniversalTime());
        var write = store.Write(record);
        if (write.Status == SonosCredentialWriteStatus.InvalidRecord)
        {
            // Odrzucony rekord przy odnowieniu USUWA dzialajace konto - to tez koniec
            // biezacego podlaczenia, a nie zwykla rotacja zestawu.
            if (replacesAccount || current is not null)
            {
                accountBindingGeneration++;
            }

            credentialGeneration++;
            current = null;
            persisted = false;
            state = SonosAccountState.NeedsLogin;
            issue = SonosAccountIssue.InvalidRecord;
            return write.Status;
        }

        if (replacesAccount)
        {
            accountBindingGeneration++;
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
        if (current is not null)
        {
            // REALNE usuniecie biezacego konta po dokladnym 401 tej samej generacji.
            accountBindingGeneration++;
        }

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
            loginGeneration,
            accountBindingGeneration);

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
