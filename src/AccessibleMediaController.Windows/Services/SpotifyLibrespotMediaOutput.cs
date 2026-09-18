using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Wyjscie multimediow dla NOWEJ, niezaleznej sesji "Spotify — Librespot".
/// Nie zastepuje <see cref="SpotifyMediaOutput"/> (sesja na oficjalnym SDK) -
/// obie sesje maja osobne kolejki i osobne wyjscia.
///
/// Zdarzenia sa te same co w <see cref="SpotifyMediaOutput"/>
/// (Preparing/Started/Duration/Ended/Failed plus <see cref="IsPreparing"/>),
/// zeby okno glowne moglo je podlaczyc bez nowego wzorca obslugi.
///
/// CYKL ZYCIA (to ta klasa trzyma, a nie okno glowne):
/// - proces hosta wstaje LENIWIE. Lista urzadzen startuje sam proces BEZ
///   logowania; konto (initialize) wchodzi dopiero przy pierwszym Play,
/// - zmiana wyjscia dzwieku to ODTWORZENIE procesu: host Rust przyjmuje
///   "initialize" tylko raz i tylko z prawdziwym tokenem, wiec drugiego
///   initialize NIE wysylamy. Stary proces jest najpierw konczony, zeby dwa
///   wyjscia nie graly na siebie,
/// - Pause/Stop/Seek/SetVolume przed zalogowaniem NIE uruchamiaja hosta i nie
///   krzycza bledem: zapisujemy zamiar (glosnosc, pozycja, pauza) lokalnie,
/// - potwierdzenie "play" NIE znaczy, ze leci dzwiek. Przygotowanie ma wlasny
///   limit czasu, bo inaczej sesja umiałaby stac w ciszy bez konca,
/// - zdarzenia STAREGO playId i STAREGO procesu nie ruszaja biezacego utworu;
///   playId nadaje AMC, a kazdy proces ma wlasna "generacje",
/// - token konta nigdy nie idzie do argumentow procesu; wylacznie stdin-em w
///   "initialize", ktore robi transport,
/// - zadnego samoczynnego ponownego logowania. Po awarii dopiero RECZNY Play
///   tworzy nowy proces. Awaria jest mowiona wprost, bo cisza jest dla
///   uzytkownika niewidomego najgorszym z bledow.
/// </summary>
internal sealed class SpotifyLibrespotMediaOutput : IMediaOutput, IDisposable
{
    /// <summary>Ile czekamy na PIERWSZY stan z isPlaying po przyjeciu "play".</summary>
    internal static readonly TimeSpan DefaultPreparationTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Ile czekamy na kulturalne wyjscie starego procesu przy zmianie wyjscia.
    /// Nowego procesu nie wolno stawiac wczesniej, wiec ten limit jest zarazem
    /// gorna granica ciszy przy przelaczaniu urzadzenia.
    /// </summary>
    private static readonly TimeSpan HostCloseTimeout = TimeSpan.FromSeconds(6);

    private readonly Func<LibrespotHostClient>? clientFactory;
    private readonly Action<Action> uiInvoker;
    private readonly TimeSpan preparationTimeout;
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private readonly object stateGate = new();
    private readonly bool ownsClient;
    private LibrespotHostClient? client;
    private long clientGeneration;
    private bool clientInitialized;
    private MediaItem? currentItem;
    private string? loadedItemId;
    private string? desiredDeviceName;
    private TimeSpan position;
    private long currentPlayId;
    private long preparationVersion;
    private int lastVolume = 100;
    private bool isPreparing;
    private bool playbackStarted;
    private bool pausedIntent;
    private bool resumeNeedsReload;
    private bool disposed;

    /// <param name="client">
    /// Gotowy transport do procesu hosta - juz uruchomiony i (jesli trzeba)
    /// zalogowany przez wolajacego. Adapter go NIE zamyka i nie loguje sam.
    /// </param>
    /// <param name="uiInvoker">
    /// Sposob wejscia na watek interfejsu. Wstrzykiwany, zeby ta klasa dala sie
    /// zmierzyc bez okna WPF; w programie to dispatcher okna glownego.
    /// </param>
    public SpotifyLibrespotMediaOutput(LibrespotHostClient client, Action<Action> uiInvoker)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.uiInvoker = uiInvoker ?? throw new ArgumentNullException(nameof(uiInvoker));
        preparationTimeout = DefaultPreparationTimeout;
        ownsClient = false;
        clientGeneration = 1;
        // Wolajacy dostarczyl gotowy transport, wiec nie dokladamy wlasnego
        // logowania: sesja nie moze wysylac drugiego "initialize".
        clientInitialized = true;
        Attach(client);
    }

    /// <param name="clientFactory">
    /// Wytwarza NOWY transport (nowy proces hosta). Wolane leniwie: przy
    /// pierwszym zapytaniu o urzadzenia albo pierwszym Play, i ponownie przy
    /// zmianie wyjscia dzwieku oraz po recznym Play po awarii.
    /// </param>
    /// <param name="uiInvoker">Wejscie na watek interfejsu.</param>
    /// <param name="deviceName">
    /// Wyjscie wybrane wczesniej (null = domyslne). Zapamietane i uzyte przy
    /// PIERWSZYM "initialize", bez uruchamiania hosta z wyprzedzeniem.
    /// </param>
    /// <param name="preparationTimeout">
    /// Limit oczekiwania na faktyczny start dzwieku. Testy skracaja go.
    /// </param>
    public SpotifyLibrespotMediaOutput(
        Func<LibrespotHostClient> clientFactory,
        Action<Action> uiInvoker,
        string? deviceName = null,
        TimeSpan? preparationTimeout = null)
    {
        this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        this.uiInvoker = uiInvoker ?? throw new ArgumentNullException(nameof(uiInvoker));
        this.preparationTimeout = preparationTimeout is { } limit && limit > TimeSpan.Zero
            ? limit
            : DefaultPreparationTimeout;
        desiredDeviceName = deviceName;
        ownsClient = true;
    }

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;

    public string? LoadedItemId
    {
        get { lock (stateGate) return loadedItemId; }
    }

    public TimeSpan Position
    {
        get { lock (stateGate) return position; }
    }

    public bool IsPreparing
    {
        get { lock (stateGate) return isPreparing; }
    }

    /// <summary>Wyjscie dzwieku wybrane dla tej sesji; null znaczy domyslne.</summary>
    public string? SelectedOutputDeviceName
    {
        get { lock (stateGate) return desiredDeviceName; }
    }

    /// <summary>Czy proces hosta zyje. Przed pierwszym uzyciem: nie.</summary>
    internal bool IsHostRunning
    {
        get
        {
            LibrespotHostClient? current;
            lock (stateGate) current = client;
            return current?.IsHostRunning ?? false;
        }
    }

    /// <summary>Host Librespot nie zmienia tempa odtwarzania, tak jak sesja SDK.</summary>
    public bool SupportsPlaybackRate => false;

    internal Task PendingPlaybackStart { get; private set; } = Task.CompletedTask;

    public void SetPlaybackRate(double playbackRate)
    {
        // Host Librespot nie ma zmiany tempa odtwarzania. Swiadomie nic nie robimy.
    }

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        ArgumentNullException.ThrowIfNull(item);
        var uri = SpotifyMediaOutput.PlaybackTrackUri(item);
        if (item.Kind is not MediaItemKind.Track || string.IsNullOrWhiteSpace(uri))
        {
            RaiseOnUi(() => PlaybackFailed?.Invoke(
                this,
                new MediaOutputFailedEventArgs(
                    item,
                    item.Kind is MediaItemKind.Album or MediaItemKind.Artist or MediaItemKind.Playlist
                        ? "To jest album, wykonawca lub playlista. Otwórz ją strzałką w prawo i wybierz utwór."
                        : "Tego elementu Spotify nie można odtworzyć w sesji Librespot.")));
            return;
        }

        var start = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        long playId;
        long version;
        lock (stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            version = ++preparationVersion;
            // playId nadaje AMC. Dwa uruchomienia tego samego utworu to dwa
            // rozne playId, wiec zdarzenia pierwszego nie ruszaja drugiego.
            playId = ++currentPlayId;
            currentItem = item;
            loadedItemId = item.Id;
            this.position = start;
            lastVolume = Math.Clamp(volume, 0, 100);
            isPreparing = true;
            playbackStarted = false;
            pausedIntent = false;
            resumeNeedsReload = false;
        }

        RaiseOnUi(() => PlaybackPreparing?.Invoke(
            this, new MediaPlaybackPreparingEventArgs(item, false)));
        PendingPlaybackStart = StartAsync(item, uri, playId, version);
    }

    private async Task StartAsync(MediaItem item, string uri, long playId, long version)
    {
        try
        {
            // Konto wchodzi TERAZ, nie przy budowaniu sesji: pierwszy Play jest
            // jedynym miejscem, w ktorym wolno zalogowac hosta.
            var lease = await EnsureHostAsync(login: true, CancellationToken.None)
                .ConfigureAwait(false);
            if (IsStale(playId, version, lease.Generation)) return;

            // Logowanie trwa sekundy i uzytkownik w tym czasie steruje dalej:
            // wycisza, przewija. Glosnosc i pozycja z chwili WYWOLANIA Play sa
            // wiec przestarzale - bierzemy OSTATNI zamiar, inaczej SetVolume(0)
            // albo Seek w czasie logowania ginely bez sladu.
            int wysylanaGlosnosc;
            TimeSpan wysylanyStart;
            lock (stateGate)
            {
                if (IsStaleLocked(playId, version, lease.Generation)) return;
                wysylanaGlosnosc = lastVolume;
                wysylanyStart = position;
            }
            await lease.Client.SetVolumeAsync(wysylanaGlosnosc).ConfigureAwait(false);
            Task<LibrespotPlayOutcome> play;
            lock (stateGate)
            {
                if (IsStaleLocked(playId, version, lease.Generation)) return;
                // Jeszcze raz TU, a nie wyzej: przewiniecie moglo przyjsc w
                // czasie oczekiwania na potwierdzenie glosnosci.
                wysylanyStart = position;
                // Register the attempt before Pause/Stop can interleave. The async
                // transport yields while waiting for the host, outside this lock.
                play = lease.Client.PlayAsync(uri, wysylanyStart, playId);
            }
            var outcome = await play.ConfigureAwait(false);
            if (outcome is LibrespotPlayOutcome.SupersededByPause
                or LibrespotPlayOutcome.SupersededByStop)
            {
                // Uzytkownik zatrzymal albo zapauzowal W TRAKCIE przygotowania.
                // Muzyka NIE moze sie potem wlaczyc, wiec konczymy przygotowanie
                // i nie zglaszamy startu.
                lock (stateGate)
                {
                    if (currentPlayId != playId) return;
                    isPreparing = false;
                    if (outcome is LibrespotPlayOutcome.SupersededByStop)
                    {
                        loadedItemId = null;
                        currentItem = null;
                        position = TimeSpan.Zero;
                    }
                }
                return;
            }
            if (outcome is LibrespotPlayOutcome.Accepted)
            {
                // "ack" znaczy tylko "przyjete do kolejki". Straz przygotowania
                // pilnuje, zeby sesja nie stala w ciszy bez konca.
                _ = WatchPreparationAsync(item, playId, version, lease.Generation);
            }
        }
        catch (LibrespotHostException exception)
        {
            HandleFailure(playId, item, FriendlyFailure(exception));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Cisza jest tu najgorszym bledem: uzytkownik nacisnal odtwarzanie.
            // Nieznany wyjatek z drogi przygotowania (dostawca poswiadczen,
            // fabryka transportu, cokolwiek) MUSI wyjsc jako powiedziana awaria,
            // a nie jako martwe zadanie PendingPlaybackStart. Tresci wyjatku nie
            // pokazujemy - moglaby poniesc token.
            DiagnosticLog.Warning(
                "spotify-librespot",
                $"Nieoczekiwany błąd przygotowania; typ: {exception.GetType().Name}.");
            HandleFailure(
                playId,
                item,
                "Sesja Spotify — Librespot nie zdołała rozpocząć odtwarzania. "
                + "Sprawdź logowanie do Spotify i połączenie, potem spróbuj ponownie.");
        }
    }

    /// <summary>
    /// Limit przygotowania. Host potwierdza "play", zanim cokolwiek zagra, wiec
    /// bez tej strazy zerwane logowanie albo zajete wyjscie zostawialy sesje w
    /// ciszy - dla uzytkownika niewidomego to najgorszy z bledow.
    ///
    /// Sama wiadomosc NIE WYSTARCZA. Host moze obudzic sie PO limicie i zaczac
    /// grac: uzytkownik uslyszalby wtedy dzwiek juz po komunikacie o awarii i
    /// bez wlasnego polecenia. Dlatego straz najpierw UNIEWAZNIA probe
    /// (nowa preparationVersion odcina spozniony stan) i KONCZY odtwarzanie w
    /// hoscie, a dopiero potem mowi o bledzie.
    /// </summary>
    private async Task WatchPreparationAsync(
        MediaItem item, long playId, long version, long generation)
    {
        try
        {
            await Task.Delay(preparationTimeout).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        LibrespotHostClient? spozniony;
        long failureId;
        lock (stateGate)
        {
            if (!isPreparing || playbackStarted) return;
            if (IsStaleLocked(playId, version, generation)) return;
            // Uniewaznienie MUSI byc przed komunikatem. Nowa preparationVersion
            // odcina starta z StartAsync, a nowy currentPlayId - spozniony stan
            // "isPlaying" od hosta, ktory inaczej ogłosiłby start utworu JUZ PO
            // powiedzeniu o awarii.
            preparationVersion++;
            failureId = ++currentPlayId;
            isPreparing = false;
            playbackStarted = false;
            spozniony = ReadyClientLocked();
        }
        // Cisza po "ack" znaczy, ze host wciaz probuje. Zatrzymujemy go, zeby
        // dzwiek nie wlaczyl sie po tym, jak juz powiedzielismy o awarii.
        if (spozniony is not null) _ = ForwardAsync(spozniony.StopAsync());
        HandleFailure(
            failureId,
            item,
            "Sesja Spotify — Librespot przyjęła utwór, ale dźwięk nie zaczął się w bezpiecznym "
            + "czasie. Sprawdź wybrane wyjście dźwięku i połączenie, potem spróbuj ponownie.");
    }

    public void Pause()
    {
        LibrespotHostClient? current;
        lock (stateGate)
        {
            preparationVersion++;
            if (isPreparing) isPreparing = false;
            playbackStarted = false;
            // Zapamietany zamiar: po zmianie wyjscia NIE wolno samemu wlaczyc
            // dzwieku, bo uzytkownik go wlasnie wyciszyl.
            pausedIntent = true;
            current = ReadyClientLocked();
        }
        // Przed zalogowaniem hosta pauza jest tylko zamiarem: nie uruchamiamy
        // procesu i nie krzyczymy bledem "nie uruchomiono".
        if (current is null) return;
        _ = ForwardAsync(current.PauseAsync());
    }

    /// <summary>Wznowienie po pauzie bez ponownego wczytywania utworu.</summary>
    public void Resume()
    {
        MediaItem? item;
        TimeSpan resumeAt;
        int volume;
        bool reload;
        LibrespotHostClient? current;
        lock (stateGate)
        {
            item = currentItem;
            resumeAt = position;
            volume = lastVolume;
            reload = resumeNeedsReload;
            resumeNeedsReload = false;
            pausedIntent = false;
            current = ReadyClientLocked();
        }
        if (reload && item is not null)
        {
            // Wyjscie zmienilo sie w pauzie: nowy proces nie ma jeszcze utworu.
            // Dzwiek wraca TERAZ, na zadanie uzytkownika, a nie wcześniej.
            Play(item, resumeAt, volume, 1d);
            return;
        }
        if (current is null) return;
        _ = ForwardAsync(current.ResumeAsync());
    }

    public void Stop()
    {
        LibrespotHostClient? current;
        lock (stateGate)
        {
            preparationVersion++;
            loadedItemId = null;
            currentItem = null;
            position = TimeSpan.Zero;
            isPreparing = false;
            playbackStarted = false;
            pausedIntent = false;
            resumeNeedsReload = false;
            current = ReadyClientLocked();
        }
        if (current is null) return;
        _ = ForwardAsync(current.StopAsync());
    }

    public void Seek(TimeSpan position)
    {
        var normalized = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        LibrespotHostClient? current;
        lock (stateGate)
        {
            this.position = normalized;
            current = ReadyClientLocked();
        }
        if (current is null) return;
        _ = ForwardAsync(current.SeekAsync(normalized));
    }

    public void SetVolume(int volume)
    {
        var clamped = Math.Clamp(volume, 0, 100);
        LibrespotHostClient? current;
        lock (stateGate)
        {
            lastVolume = clamped;
            current = ReadyClientLocked();
        }
        if (current is null) return;
        _ = ForwardAsync(current.SetVolumeAsync(clamped));
    }

    /// <summary>
    /// Lista urzadzen wyjscia hosta. Nie wymaga konta, wiec wolno ja pokazac
    /// przed zalogowaniem - proces wstaje, ale "initialize" NIE idzie.
    /// </summary>
    public async Task<IReadOnlyList<LibrespotOutputDevice>> GetOutputDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var lease = await EnsureHostAsync(login: false, cancellationToken).ConfigureAwait(false);
        return await lease.Client.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Przelacza wyjscie na DOKLADNA nazwe z <see cref="GetOutputDevicesAsync"/>
    /// (null znaczy domyslne). Gdy host jeszcze nie ma konta, wybor jest tylko
    /// zapamietany i uzyty przy pierwszym logowaniu. Gdy host gra, wyjscie
    /// zmienia sie przez ODTWORZENIE procesu, bo Rust nie przyjmuje drugiego
    /// "initialize". Odmowa jest zglaszana jako blad - bez cichego powrotu na
    /// stare wyjscie.
    /// </summary>
    public async Task<bool> TrySetOutputDeviceAsync(
        string? deviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            LibrespotHostClient? current;
            bool initialized;
            lock (stateGate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                current = client;
                initialized = clientInitialized;
            }

            if (deviceName is not null)
            {
                // Tylko nazwa, ktora host FAKTYCZNIE zglosil. Zla nazwa nie moze
                // skonczyc sie cichym wyjsciem domyslnym.
                var devices = await GetOutputDevicesAsync(cancellationToken).ConfigureAwait(false);
                if (!devices.Any(device =>
                        string.Equals(device.Name, deviceName, StringComparison.Ordinal)))
                {
                    throw new LibrespotHostException(
                        "audio_device_unavailable",
                        "Host Librespot nie zgłasza takiego wyjścia dźwięku.");
                }
            }

            if (!initialized || current is null)
            {
                // Konta jeszcze nie ma: nie ma czego przelaczac i nie wolno tu
                // logowac. Zapamietujemy wybor na pierwsze "initialize".
                lock (stateGate) desiredDeviceName = deviceName;
                return true;
            }

            if (!ownsClient)
            {
                // Cudzego procesu nie wolno zakonczyc, a innej drogi nie ma -
                // wiec odmawiamy WPROST, zamiast wysylac drugie "initialize",
                // ktorego host nie obsluguje.
                await current.SetOutputDeviceAsync(deviceName, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SwapHostForDeviceAsync(deviceName).ConfigureAwait(false);
            return true;
        }
        catch (LibrespotHostException exception)
        {
            MediaItem? item;
            lock (stateGate) item = currentItem;
            var nazwa = deviceName ?? "domyślne";
            RaiseOnUi(() => PlaybackFailed?.Invoke(
                this,
                new MediaOutputFailedEventArgs(
                    item,
                    $"Nie udało się przełączyć wyjścia Spotify — Librespot na „{nazwa}”. "
                    + FriendlyFailure(exception))));
            DiagnosticLog.Warning(
                "spotify-librespot",
                $"Odmowa zmiany wyjścia; kod: {exception.Code}.");
            return false;
        }
    }

    // ---------- Cykl zycia procesu hosta ----------

    /// <summary>
    /// REALNA zmiana wyjscia: konczy STARY proces i stawia NOWY, ktory dostaje
    /// docelowe urzadzenie w swoim pierwszym (i jedynym) "initialize".
    ///
    /// Kolejnosc jest tu istotna, a nie kosmetyczna:
    /// 1. zapisujemy utwor, pozycje i to, czy gralo,
    /// 2. STARY proces konczymy PRZED postawieniem nowego, inaczej dwa wyjscia
    ///    gralyby jednoczesnie,
    /// 3. nowy proces logujemy i - tylko gdy dzwiek FAKTYCZNIE szedl - wracamy
    ///    do utworu od zapamietanej pozycji. Gdy uzytkownik byl w pauzie,
    ///    NICZEGO nie wlaczamy: dzwiek wrocilby wbrew niemu. Utwor czeka na
    ///    <see cref="Resume"/>.
    /// </summary>
    private async Task SwapHostForDeviceAsync(string? deviceName)
    {
        MediaItem? item;
        TimeSpan resumeAt;
        int volume;
        bool wasPlaying;
        LibrespotHostClient? old;
        lock (stateGate)
        {
            item = currentItem;
            resumeAt = position;
            volume = lastVolume;
            wasPlaying = playbackStarted && !pausedIntent;
            old = client;
            // Nowa generacja OD RAZU: zdarzenia konczacego sie procesu (i te
            // odlozone w kolejce okna) nie moga ruszyc nowego stanu.
            clientGeneration++;
            client = null;
            clientInitialized = false;
            isPreparing = false;
            playbackStarted = false;
            desiredDeviceName = deviceName;
            if (!wasPlaying && item is not null) resumeNeedsReload = true;
        }

        if (old is not null) await DetachAndCloseAsync(old).ConfigureAwait(false);
        if (item is null || !wasPlaying)
        {
            // Nic nie gralo: nowy proces wstanie leniwie przy nastepnym Play.
            return;
        }
        // Utwor wraca od zapamietanej pozycji na NOWYM wyjsciu. To zwykla droga
        // Play, wiec idzie przez to samo logowanie, te sama straz generacji i
        // ten sam limit przygotowania.
        Play(item, resumeAt, volume, 1d);
        await PendingPlaybackStart.ConfigureAwait(false);
    }

    private readonly record struct HostLease(LibrespotHostClient Client, long Generation);

    /// <summary>
    /// Zwraca dzialajacy transport. Uruchamia proces, gdy go nie ma, i loguje
    /// konto TYLKO gdy <paramref name="login"/>. Wywolania sa szeregowane, zeby
    /// dwa Play nie postawily dwoch procesow na raz.
    /// </summary>
    private async Task<HostLease> EnsureHostAsync(bool login, CancellationToken cancellationToken)
    {
        await lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LibrespotHostClient? existing;
            bool initialized;
            string? device;
            int volume;
            long generation;
            lock (stateGate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                existing = client;
                initialized = clientInitialized;
                device = desiredDeviceName;
                volume = lastVolume;
                generation = clientGeneration;
            }

            if (existing is null || (existing.IsHostRunning is false && ownsClient && !initialized))
            {
                if (clientFactory is null)
                {
                    throw new LibrespotHostException(
                        LibrespotHostErrorCodes.NotStarted,
                        "Sesja Spotify — Librespot nie jest uruchomiona.");
                }
                if (existing is not null) DetachAndClose(existing);
                var fresh = clientFactory()
                    ?? throw new LibrespotHostException(
                        LibrespotHostErrorCodes.StartFailed,
                        "Nie udało się utworzyć transportu hosta Librespot.");
                lock (stateGate)
                {
                    client = fresh;
                    clientInitialized = false;
                    generation = ++clientGeneration;
                }
                Attach(fresh);
                await fresh.StartAsync(cancellationToken).ConfigureAwait(false);
                existing = fresh;
                initialized = false;
            }

            if (login && !initialized)
            {
                // Token bierze transport ze wstrzyknietego dostawcy i wysyla go
                // WYLACZNIE stdin-em. Tu nie ma ani argv, ani dziennika.
                await existing.InitializeAsync(null, device, volume, cancellationToken)
                    .ConfigureAwait(false);
                lock (stateGate) clientInitialized = true;
            }
            return new HostLease(existing, generation);
        }
        finally
        {
            lifecycleGate.Release();
        }
    }

    private void Attach(LibrespotHostClient target)
    {
        target.StateChanged += OnStateChanged;
        target.PlaybackEnded += OnPlaybackEnded;
        target.TrackFailed += OnTrackFailed;
        target.HostFailed += OnHostFailed;
    }

    private void Detach(LibrespotHostClient target)
    {
        target.StateChanged -= OnStateChanged;
        target.PlaybackEnded -= OnPlaybackEnded;
        target.TrackFailed -= OnTrackFailed;
        target.HostFailed -= OnHostFailed;
    }

    /// <summary>
    /// Odlacza zdarzenia i KONCZY proces, gdy jest nasz. Cudzego transportu
    /// (podanego w konstruktorze) nie zamykamy.
    ///
    /// Wersja synchroniczna jest dla drog, ktore nie moga czekac (zdarzenie
    /// awarii, Dispose): ubija proces. Gdy host JESZCZE ZYJE i mamy gdzie
    /// czekac, uzywaj <see cref="DetachAndCloseAsync"/> - tylko ona wysyla
    /// "shutdown", a ubity host nie oddaje urzadzenia audio po dobremu.
    /// </summary>
    private void DetachAndClose(LibrespotHostClient target)
    {
        Detach(target);
        if (!ownsClient) return;
        try
        {
            target.Dispose();
        }
        catch (LibrespotHostException)
        {
        }
    }

    /// <summary>
    /// Kulturalne zamkniecie naszego procesu: "shutdown" i czekanie na wyjscie.
    /// Limit jest OBOWIAZKOWY - bez niego zawieszony host (albo niedomknieta
    /// petla czytania stdout) zablokowalby zmiane wyjscia na zawsze. Po limicie
    /// schodzimy do ubicia, bo zostawiony proces trzymalby stare wyjscie
    /// dzwieku i dwa wyjscia gralyby razem.
    /// </summary>
    private async Task DetachAndCloseAsync(LibrespotHostClient target)
    {
        Detach(target);
        if (!ownsClient) return;
        try
        {
            await target.DisposeAsync().AsTask().WaitAsync(HostCloseTimeout).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is LibrespotHostException or TimeoutException)
        {
            try
            {
                target.Dispose();
            }
            catch (LibrespotHostException)
            {
            }
        }
    }

    /// <summary>Transport gotowy na polecenia sterujace, albo null.</summary>
    private LibrespotHostClient? ReadyClientLocked() =>
        !disposed && clientInitialized ? client : null;

    private bool IsStale(long playId, long version, long generation)
    {
        lock (stateGate) return IsStaleLocked(playId, version, generation);
    }

    private bool IsStaleLocked(long playId, long version, long generation) =>
        disposed
        || version != preparationVersion
        || currentPlayId != playId
        || generation != clientGeneration;

    private async Task ForwardAsync(Task command)
    {
        try
        {
            await command.ConfigureAwait(false);
        }
        catch (LibrespotHostException exception)
        {
            MediaItem? item;
            long playId;
            lock (stateGate)
            {
                item = currentItem;
                playId = currentPlayId;
            }
            if (item is not null) HandleFailure(playId, item, FriendlyFailure(exception));
        }
    }

    private void OnStateChanged(object? sender, LibrespotStateEventArgs e)
    {
        MediaItem? item;
        long generation;
        var raiseStarted = false;
        lock (stateGate)
        {
            // Zdarzenie STAREJ proby albo STAREGO procesu nie moze zmienic czasu
            // ani tytulu tego, co gra teraz - czytnik przeczytalby nie ten utwor.
            if (!IsCurrentSenderLocked(sender) || e.PlayId != currentPlayId) return;
            generation = clientGeneration;
            item = currentItem;
            position = e.Position;
            if (e.IsPlaying)
            {
                isPreparing = false;
                raiseStarted = !playbackStarted;
                playbackStarted = true;
            }
        }
        if (item is null) return;

        if (e.Duration > TimeSpan.Zero
            && Math.Abs(item.Duration.TotalSeconds - e.Duration.TotalSeconds) >= 1)
        {
            item.Duration = e.Duration;
            RaiseOnUi(generation, () => DurationAvailable?.Invoke(
                this,
                new MediaDurationAvailableEventArgs(item, item.Duration, item.SampleRateHz ?? 0)));
        }
        if (raiseStarted)
        {
            RaiseOnUi(generation, () => PlaybackStarted?.Invoke(
                this, new MediaPlaybackStartedEventArgs(item)));
        }
    }

    private void OnPlaybackEnded(object? sender, LibrespotEndedEventArgs e)
    {
        MediaItem? item;
        long generation;
        lock (stateGate)
        {
            if (!IsCurrentSenderLocked(sender) || e.PlayId != currentPlayId) return;
            if (!playbackStarted || isPreparing) return;
            generation = clientGeneration;
            item = currentItem;
            playbackStarted = false;
            loadedItemId = null;
        }
        if (item is null) return;
        RaiseOnUi(generation, () => PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(item)));
    }

    private void OnTrackFailed(object? sender, LibrespotTrackErrorEventArgs e)
    {
        MediaItem? item;
        lock (stateGate)
        {
            if (!IsCurrentSenderLocked(sender) || e.PlayId != currentPlayId) return;
            item = currentItem;
        }
        if (item is null) return;
        HandleFailure(e.PlayId, item, FriendlyTrackFailure(e.Code));
    }

    private void OnHostFailed(object? sender, LibrespotHostFailureEventArgs e)
    {
        MediaItem? item;
        long playId;
        long generation;
        LibrespotHostClient? dead = null;
        lock (stateGate)
        {
            if (!IsCurrentSenderLocked(sender)) return;
            item = currentItem;
            playId = currentPlayId;
            generation = clientGeneration;
            if (ownsClient && IsProcessLevel(e.Code))
            {
                // Proces padl. Zapominamy go, zeby RECZNY Play mogl postawic
                // nowy. Zadnej samoczynnej petli logowania.
                dead = client;
                client = null;
                clientInitialized = false;
                clientGeneration++;
                isPreparing = false;
                playbackStarted = false;
                // Generacja dla komunikatu musi byc TA NOWA. Wczesniej szla
                // stara, a straz generacji w wywolaniu zwrotnym odrzucala wtedy
                // KAZDY komunikat o padzie hosta bez wczytanego utworu - awaria
                // konczyla sie cisza.
                generation = clientGeneration;
            }
        }
        if (dead is not null) DetachAndClose(dead);
        var message = FriendlyHostFailure(e.Code, e.Message);
        DiagnosticLog.Warning("spotify-librespot", $"Awaria hosta; kod: {e.Code}.");
        if (item is not null) HandleFailure(playId, item, message);
        else RaiseOnUi(generation, () => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(null, message)));
    }

    private static bool IsProcessLevel(string code) => code
        is LibrespotHostErrorCodes.HostExited
        or LibrespotHostErrorCodes.Timeout
        or LibrespotHostErrorCodes.ProtocolViolation
        or LibrespotHostErrorCodes.LineTooLong
        or LibrespotHostErrorCodes.ProtocolVersionMismatch;

    /// <summary>Czy zdarzenie przyszlo od AKTUALNEGO transportu (nie od starego procesu).</summary>
    private bool IsCurrentSenderLocked(object? sender) =>
        !disposed && (sender is null || ReferenceEquals(sender, client));

    private void HandleFailure(long playId, MediaItem item, string message)
    {
        long generation;
        lock (stateGate)
        {
            if (disposed || playId != currentPlayId) return;
            generation = clientGeneration;
            loadedItemId = null;
            isPreparing = false;
            playbackStarted = false;
        }
        DiagnosticLog.Warning(
            "spotify-librespot",
            $"Odtwarzanie nie powiodło się; element: {item.ExternalId ?? item.Id}.");
        RaiseOnUi(generation, () => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));
    }

    /// <summary>
    /// Zamienia kod bledu transportu na zdanie, z ktorego uzytkownik wie, co
    /// zrobic. Tresci od hosta NIE wklejamy w calosci, zeby komunikat nie
    /// poniosl przypadkiem tokenu.
    /// </summary>
    internal static string FriendlyFailure(LibrespotHostException exception) =>
        FriendlyHostFailure(exception.Code, exception.Message);

    internal static string FriendlyHostFailure(string code, string message) => code switch
    {
        LibrespotHostErrorCodes.NotStarted =>
            "Sesja Spotify — Librespot nie jest uruchomiona.",
        LibrespotHostErrorCodes.StartFailed =>
            "Nie udało się uruchomić składnika Librespot. Sprawdź instalację AMC.",
        LibrespotHostErrorCodes.HostExited =>
            "Składnik Librespot przestał działać. Uruchom odtwarzanie ponownie.",
        LibrespotHostErrorCodes.Timeout =>
            "Składnik Librespot nie odpowiedział w bezpiecznym czasie. Sprawdź połączenie z internetem i spróbuj ponownie.",
        LibrespotHostErrorCodes.ProtocolVersionMismatch =>
            "Wersja składnika Librespot nie pasuje do tej wersji AMC. Zaktualizuj program.",
        LibrespotHostErrorCodes.ProtocolViolation or LibrespotHostErrorCodes.LineTooLong =>
            "Składnik Librespot przysłał dane niezgodne z protokołem. Sesja została zatrzymana.",
        LibrespotHostErrorCodes.TokenLeakGuard =>
            "Odmówiono uruchomienia składnika Librespot z powodu zabezpieczenia poświadczeń.",
        LibrespotHostErrorCodes.CredentialsUnavailable =>
            "Nie udało się pobrać poświadczeń konta Spotify. Otwórz okno konta Spotify "
            + "i zaloguj się ponownie, potem spróbuj odtworzyć utwór.",
        LibrespotHostErrorCodes.DeviceChangeNeedsNewHost =>
            "Tę sesję Librespot prowadzi transport zewnętrzny, który nie potrafi przełączyć wyjścia. "
            + "Zatrzymaj sesję i uruchom ją ponownie z wybranym wyjściem.",
        LibrespotHostErrorCodes.Disposed =>
            "Sesja Spotify — Librespot została zamknięta.",
        _ => SpotifyMediaOutput.SafeDiagnostic(message) is { Length: > 0 } safe
            ? $"Sesja Spotify — Librespot zgłosiła błąd. {safe}"
            : "Sesja Spotify — Librespot zgłosiła błąd."
    };

    internal static string FriendlyTrackFailure(string code) => code switch
    {
        "premium_required" =>
            "Sesja Spotify — Librespot wymaga konta Premium. To konto nie zagra w AMC.",
        "invalid_token" or "token_expired" =>
            "Logowanie do Spotify wygasło. Otwórz okno konta Spotify i zaloguj się ponownie.",
        "track_unavailable" =>
            "Spotify nie udostępnia tego utworu na tym koncie lub w tym kraju.",
        "audio_device_lost" or "audio_device_unavailable" =>
            "Wybrane wyjście dźwięku przestało być dostępne. Wybierz inne wyjście w opcjach sesji.",
        _ => "Nie udało się odtworzyć tego utworu w sesji Spotify — Librespot."
    };

    private void RaiseOnUi(Action action) => uiInvoker(action);

    /// <summary>
    /// Puszcza zdarzenie na watek interfejsu, ale straz generacji sprawdzamy
    /// JESZCZE RAZ w samym wywolaniu zwrotnym: kolejka okna moze je wykonac po
    /// wymianie procesu, a wtedy dotyczyloby juz nieistniejacego stanu.
    /// </summary>
    private void RaiseOnUi(long generation, Action action) => uiInvoker(() =>
    {
        lock (stateGate)
        {
            if (disposed || generation != clientGeneration) return;
        }
        action();
    });

    public void Dispose()
    {
        LibrespotHostClient? current;
        lock (stateGate)
        {
            if (disposed) return;
            disposed = true;
            current = client;
            client = null;
        }
        if (current is null) return;
        Detach(current);
        // Wlasny transport zamykamy (konczy proces). CUDZEGO nie - nie wolno
        // zamykac zycia, ktorego nie stworzylismy.
        if (!ownsClient) return;
        try
        {
            // DisposeAsync, nie Dispose: tylko sciezka asynchroniczna wysyla
            // hostowi "shutdown". Synchroniczny Dispose klienta jedynie zrywa
            // zycie i ubija proces, a ubity host nie oddaje urzadzenia audio po
            // dobremu. Czekamy ograniczony czas, bo Dispose nie moze zawisnac.
            if (!current.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(6)))
            {
                current.Dispose();
            }
        }
        catch (LibrespotHostException)
        {
        }
    }
}
