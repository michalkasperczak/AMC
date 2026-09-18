using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Transport do osobnego procesu hosta Librespot (sesja "Spotify — Librespot").
/// Platform-neutral: zadnej zaleznosci od WPF, zeby dalo sie go zmierzyc
/// testem na prawdziwym procesie-atrapie, a nie tylko parsowaniem tekstu.
///
/// Za co ta klasa odpowiada i czego pilnuje:
/// - JEDEN pisarz do stdin. Zapisy sa serializowane semaforem, bo dwie linie
///   JSON wymieszane znakami to dla hosta blad protokolu, a dla uzytkownika
///   cisza.
/// - correlationId (requestId) plus limit czasu na kazde polecenie. Host,
///   ktory nie odpowie, daje CZYTELNY blad, nigdy ciszy.
/// - EOF strumienia konczy WSZYSTKIE oczekujace polecenia bledem
///   <see cref="LibrespotHostErrorCodes.HostExited"/>. Bez tego zamkniety host
///   zostawia AMC w nieskonczonym oczekiwaniu.
/// - Ograniczona dlugosc linii: zepsuty host nie zje pamieci jednym wierszem.
/// - STARY playId nie rusza biezacego stanu. Dwa razy ten sam URI to dwa
///   rozne playId, wiec "ended" pierwszej proby nie konczy drugiej.
/// - Pauza albo zatrzymanie W TRAKCIE przygotowania nie moze pozniej wlaczyc
///   muzyki: <see cref="PlayAsync"/> zwraca wtedy Superseded i sam odsyla
///   hostowi pauze albo stop.
/// - Token konta idzie WYLACZNIE w "initialize" przez stdin. Nie logujemy
///   tresci wysylanych linii ani odpowiedzi, bo moglyby niesc sekret.
/// </summary>
public sealed class LibrespotHostClient : IAsyncDisposable, IDisposable
{
    private readonly Func<ILibrespotHostProcess> processFactory;
    private readonly LibrespotHostOptions options;
    private readonly Func<CancellationToken, Task<string>>? credentialProvider;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly ConcurrentDictionary<long, PendingRequest> pending = new();
    private readonly TaskCompletionSource<int> readySignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object stateGate = new();

    private ILibrespotHostProcess? process;
    private Task? readerLoop;
    private long nextRequestId;
    private long currentPlayId;
    private string currentUri = string.Empty;
    private bool isPreparing;
    private bool playbackStarted;
    private bool stopRequestedDuringPreparation;
    private bool pauseRequestedDuringPreparation;
    private bool initialized;
    private string? selectedDeviceName;
    private int hostVolume = 100;
    private bool disposed;
    /// <summary>
    /// Trwa zamykanie: blokuje powtorne Dispose, ale NIE blokuje ostatniego
    /// polecenia "shutdown" wysylanego przez SendAsync.
    /// </summary>
    private bool disposing;

    public LibrespotHostClient(
        Func<ILibrespotHostProcess> processFactory,
        LibrespotHostOptions? options = null,
        Func<CancellationToken, Task<string>>? credentialProvider = null)
    {
        this.processFactory = processFactory
            ?? throw new ArgumentNullException(nameof(processFactory));
        this.options = options ?? new LibrespotHostOptions();
        this.credentialProvider = credentialProvider;
    }

    /// <summary>Postep i stan biezacego playId. Stare playId sa odrzucane.</summary>
    public event EventHandler<LibrespotStateEventArgs>? StateChanged;

    /// <summary>Potwierdzony koniec utworu biezacego playId.</summary>
    public event EventHandler<LibrespotEndedEventArgs>? PlaybackEnded;

    /// <summary>Blad jednego utworu; sesja i proces zyja dalej.</summary>
    public event EventHandler<LibrespotTrackErrorEventArgs>? TrackFailed;

    /// <summary>Awaria hosta: koniec procesu, EOF, zly protokol, przekroczony czas.</summary>
    public event EventHandler<LibrespotHostFailureEventArgs>? HostFailed;

    /// <summary>Wersja protokolu potwierdzona zdarzeniem "ready", albo null.</summary>
    public int? NegotiatedProtocolVersion { get; private set; }

    /// <summary>
    /// Czy trwa przygotowanie utworu. Prawda od <see cref="PlayAsync"/> do
    /// pierwszego stanu z isPlaying, bledu utworu albo pauzy/zatrzymania.
    /// </summary>
    public bool IsPreparing
    {
        get { lock (stateGate) return isPreparing; }
    }

    /// <summary>Identyfikator biezacej proby odtwarzania nadany przez AMC.</summary>
    public long CurrentPlayId
    {
        get { lock (stateGate) return currentPlayId; }
    }

    /// <summary>URI biezacej proby odtwarzania.</summary>
    public string CurrentUri
    {
        get { lock (stateGate) return currentUri; }
    }

    /// <summary>Nazwa urzadzenia wybranego w hoscie; null znaczy domyslne.</summary>
    public string? SelectedDeviceName
    {
        get { lock (stateGate) return selectedDeviceName; }
    }

    /// <summary>Glosnosc przekazana hostowi (0..100).</summary>
    public int HostVolume
    {
        get { lock (stateGate) return hostVolume; }
    }

    public bool IsHostRunning
    {
        get
        {
            var current = Volatile.Read(ref process);
            return current is not null && !current.HasExited;
        }
    }

    /// <summary>
    /// Uruchamia proces i czeka na zdarzenie startowe. "ready" mowi TYLKO, ze
    /// proces wstal i zna wersje protokolu - NIE ze konto jest zalogowane.
    /// Nieznana wersja protokolu konczy sie bledem, nie cicha zgoda.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (stateGate)
        {
            if (process is not null)
            {
                throw new LibrespotHostException(
                    LibrespotHostErrorCodes.ProtocolViolation,
                    "Host Librespot już został uruchomiony w tej sesji.");
            }
        }

        ILibrespotHostProcess started;
        try
        {
            started = processFactory()
                ?? throw new LibrespotHostException(
                    LibrespotHostErrorCodes.StartFailed,
                    "Nie udało się uruchomić procesu hosta Librespot.");
        }
        catch (Exception exception) when (exception is not LibrespotHostException)
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.StartFailed,
                "Nie udało się uruchomić procesu hosta Librespot.",
                exception);
        }

        lock (stateGate) process = started;
        readerLoop = Task.Run(() => ReadLoopAsync(started, lifetime.Token));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, lifetime.Token);
        try
        {
            var version = await readySignal.Task
                .WaitAsync(options.ReadyTimeout, linked.Token)
                .ConfigureAwait(false);
            NegotiatedProtocolVersion = version;
        }
        catch (TimeoutException)
        {
            Shutdown(
                LibrespotHostErrorCodes.Timeout,
                "Host Librespot nie zgłosił gotowości w bezpiecznym czasie.");
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.Timeout,
                "Host Librespot nie zgłosił gotowości w bezpiecznym czasie.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.HostExited,
                "Proces hosta Librespot zakończył się przed zgłoszeniem gotowości.");
        }
    }

    public Task PingAsync(CancellationToken cancellationToken = default) =>
        SendAsync(LibrespotHostContract.CommandPing, null, cancellationToken);

    /// <summary>
    /// Lista urzadzen wyjscia. Nie wymaga konta, wiec wolno ja pokazac przed
    /// logowaniem. Nazwy sa DOKLADNE: wybor urzadzenia przyjmuje tylko taka
    /// nazwe, jaka zwrocil host.
    /// </summary>
    public async Task<IReadOnlyList<LibrespotOutputDevice>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            LibrespotHostContract.CommandDevices, null, cancellationToken).ConfigureAwait(false);
        var devices = new List<LibrespotOutputDevice>();
        if (response["devices"] is JsonArray array)
        {
            foreach (var entry in array)
            {
                if (entry is not JsonObject device) continue;
                var name = device["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var isDefault = device["isDefault"] is JsonValue flag
                    && flag.TryGetValue(out bool value) && value;
                devices.Add(new LibrespotOutputDevice(name, isDefault));
            }
        }
        return devices;
    }

    /// <summary>
    /// Podaje hostowi token konta i wybrane urzadzenie. Token bierzemy z
    /// wstrzykniętego dostawcy, wiec testy nie musza dotykac konta.
    /// Urzadzenie null znaczy domyslne; inaczej musi byc DOKLADNA nazwa z
    /// <see cref="GetDevicesAsync"/>. Zadnego cichego zastepnika i zadnego
    /// samoczynnego logowania poza tym wywolaniem.
    /// </summary>
    public async Task InitializeAsync(
        string? accessToken = null,
        string? deviceName = null,
        int volume = 100,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var token = accessToken;
        if (string.IsNullOrEmpty(token))
        {
            if (credentialProvider is null)
            {
                throw new LibrespotHostException(
                    LibrespotHostErrorCodes.ProtocolViolation,
                    "Brak źródła poświadczeń Spotify dla hosta Librespot.");
            }
            token = await credentialProvider(cancellationToken).ConfigureAwait(false);
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.ProtocolViolation,
                "Konto Spotify nie zwróciło tokenu dostępu dla hosta Librespot.");
        }

        var clamped = Math.Clamp(volume, 0, 100);
        var payload = new JsonObject
        {
            ["accessToken"] = token,
            ["device"] = deviceName is null ? null : JsonValue.Create(deviceName),
            ["volume"] = clamped
        };
        await SendAsync(
            LibrespotHostContract.CommandInitialize,
            payload,
            cancellationToken,
            // Logowanie ma WLASNY, dluzszy limit: host czeka na Session::connect
            // do 30 s, a zwykly limit polecenia (15 s) urwalby poprawne
            // logowanie i zglosil falszywy blad przekroczenia czasu.
            options.InitializeTimeout).ConfigureAwait(false);
        lock (stateGate)
        {
            initialized = true;
            selectedDeviceName = deviceName;
            hostVolume = clamped;
        }
    }

    /// <summary>
    /// UCZCIWA ODMOWA: ten transport nie potrafi przelaczyc wyjscia dzwieku w
    /// dzialajacej sesji.
    ///
    /// Wczesniej wysylalo sie tu drugie "initialize" z accessToken=null i polami
    /// keepUri/keepPlayId/keepPositionMs/keepPlaying. Host tego NIE obsluguje:
    /// wymaga niepustego tokenu i odrzuca kazdy kolejny "initialize" kodem
    /// alreadyInitialized. Byl to kontrakt zmyslony po stronie AMC - dlatego go
    /// tu nie ma i nie wolno go przywracac.
    ///
    /// Wyjscie zmienia warstwa wyzej: konczy STARY proces i stawia NOWY, ktory
    /// dostaje docelowe urzadzenie w swoim pierwszym "initialize". Jedno zycie
    /// tego obiektu = jeden proces hosta.
    /// </summary>
    public Task SetOutputDeviceAsync(
        string? deviceName,
        CancellationToken cancellationToken = default)
    {
        lock (stateGate)
        {
            if (!initialized)
            {
                throw new LibrespotHostException(
                    LibrespotHostErrorCodes.NotStarted,
                    "Host Librespot nie ma jeszcze poświadczeń konta.");
            }
        }
        throw new LibrespotHostException(
            LibrespotHostErrorCodes.DeviceChangeNeedsNewHost,
            "Host Librespot przyjmuje wybór wyjścia tylko raz, przy logowaniu. "
            + "Zmiana wyjścia wymaga nowego procesu hosta.");
    }

    /// <summary>
    /// Uruchamia utwor. playId nadaje AMC, a nie host: dzieki temu DWA
    /// uruchomienia tego samego URI sa rozroznialne i zdarzenia pierwszego nie
    /// ruszaja drugiego.
    ///
    /// Gdy w trakcie przygotowania przyszla pauza albo zatrzymanie, nie
    /// puszczamy muzyki - odsylamy hostowi to, czego chcial uzytkownik, i
    /// zwracamy powod.
    /// </summary>
    public async Task<LibrespotPlayOutcome> PlayAsync(
        string uri,
        TimeSpan position,
        long playId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        if (playId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playId), "playId nadawany przez AMC musi być liczbą dodatnią.");
        }

        var positionMs = (long)Math.Max(0, position.TotalMilliseconds);
        lock (stateGate)
        {
            currentPlayId = playId;
            currentUri = uri;
            isPreparing = true;
            playbackStarted = false;
            stopRequestedDuringPreparation = false;
            pauseRequestedDuringPreparation = false;
            lastPosition = TimeSpan.FromMilliseconds(positionMs);
            lastPaused = false;
        }

        var payload = new JsonObject
        {
            ["uri"] = uri,
            ["positionMs"] = positionMs,
            ["playId"] = playId
        };

        try
        {
            await SendAsync(LibrespotHostContract.CommandPlay, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            lock (stateGate)
            {
                if (currentPlayId == playId) isPreparing = false;
            }
            throw;
        }

        bool supersededByStop;
        bool supersededByPause;
        bool supersededByNewPlay;
        lock (stateGate)
        {
            supersededByNewPlay = currentPlayId != playId;
            supersededByStop = !supersededByNewPlay && stopRequestedDuringPreparation;
            supersededByPause = !supersededByNewPlay && !supersededByStop
                && pauseRequestedDuringPreparation;
            if (supersededByStop || supersededByPause)
            {
                isPreparing = false;
                playbackStarted = false;
                stopRequestedDuringPreparation = false;
                pauseRequestedDuringPreparation = false;
            }
        }

        if (supersededByNewPlay) return LibrespotPlayOutcome.SupersededByNewPlay;

        // Pauza/stop wyslane PRZED odpowiedzia hosta moglyby sie zgubic: host
        // dostal je, gdy jeszcze nie mial utworu. Wysylamy je ponownie TERAZ,
        // inaczej muzyka wlaczylaby sie po tym, jak uzytkownik ja zatrzymal.
        if (supersededByStop)
        {
            await SendAsync(LibrespotHostContract.CommandStop, null, CancellationToken.None)
                .ConfigureAwait(false);
            lock (stateGate)
            {
                if (currentPlayId == playId)
                {
                    currentPlayId = 0;
                    currentUri = string.Empty;
                    lastPosition = TimeSpan.Zero;
                }
            }
            return LibrespotPlayOutcome.SupersededByStop;
        }
        if (supersededByPause)
        {
            await SendAsync(LibrespotHostContract.CommandPause, null, CancellationToken.None)
                .ConfigureAwait(false);
            lock (stateGate)
            {
                if (currentPlayId == playId) lastPaused = true;
            }
            return LibrespotPlayOutcome.SupersededByPause;
        }

        return LibrespotPlayOutcome.Accepted;
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        lock (stateGate)
        {
            if (isPreparing) pauseRequestedDuringPreparation = true;
            lastPaused = true;
        }
        await SendAsync(LibrespotHostContract.CommandPause, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        lock (stateGate)
        {
            pauseRequestedDuringPreparation = false;
            lastPaused = false;
        }
        await SendAsync(LibrespotHostContract.CommandResume, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (stateGate)
        {
            if (isPreparing) stopRequestedDuringPreparation = true;
            else
            {
                currentPlayId = 0;
                currentUri = string.Empty;
                lastPosition = TimeSpan.Zero;
            }
            playbackStarted = false;
            lastPaused = false;
        }
        await SendAsync(LibrespotHostContract.CommandStop, null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        var positionMs = (long)Math.Max(0, position.TotalMilliseconds);
        var payload = new JsonObject { ["positionMs"] = positionMs };
        await SendAsync(LibrespotHostContract.CommandSeek, payload, cancellationToken)
            .ConfigureAwait(false);
        lock (stateGate) lastPosition = TimeSpan.FromMilliseconds(positionMs);
    }

    public async Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(volume, 0, 100);
        var payload = new JsonObject { ["volume"] = clamped };
        await SendAsync(LibrespotHostContract.CommandVolume, payload, cancellationToken)
            .ConfigureAwait(false);
        lock (stateGate) hostVolume = clamped;
    }

    /// <summary>Prosi host o zamkniecie i czeka do limitu; potem ubija proces.</summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        var current = Volatile.Read(ref process);
        if (current is null || current.HasExited) return;
        try
        {
            await SendAsync(LibrespotHostContract.CommandShutdown, null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LibrespotHostException)
        {
            // Host mogl zamknac sie natychmiast po przyjeciu polecenia.
        }
        using var timeout = new CancellationTokenSource(options.ShutdownTimeout);
        try
        {
            await current.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            current.Kill();
        }
    }

    private TimeSpan lastPosition;
    private bool lastPaused;

    /// <summary>
    /// Wysyla jedno polecenie i czeka na odpowiedz z tym samym requestId.
    /// Zapisy sa serializowane, bo dwa rownolegle pisarze skleiliby linie JSON.
    /// </summary>
    private async Task<JsonObject> SendAsync(
        string command,
        JsonObject? payload,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        ThrowIfDisposed();
        var current = Volatile.Read(ref process);
        if (current is null)
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.NotStarted,
                "Host Librespot nie został uruchomiony.");
        }
        if (current.HasExited)
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.HostExited,
                "Proces hosta Librespot nie działa. Uruchom sesję ponownie.");
        }

        var requestId = Interlocked.Increment(ref nextRequestId);
        var envelope = new JsonObject
        {
            ["command"] = command,
            ["requestId"] = requestId,
            ["sessionId"] = LibrespotHostContract.SessionId
        };
        foreach (var property in payload ?? [])
        {
            envelope[property.Key] = property.Value?.DeepClone();
        }

        var request = new PendingRequest();
        pending[requestId] = request;
        try
        {
            var line = envelope.ToJsonString();
            await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Nie logujemy tresci linii: "initialize" niesie token konta.
                await current.StandardInput.WriteLineAsync(line).ConfigureAwait(false);
                await current.StandardInput.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                writeGate.Release();
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, lifetime.Token);
            try
            {
                return await request.Completion.Task
                    .WaitAsync(timeout ?? options.RequestTimeout, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new LibrespotHostException(
                    LibrespotHostErrorCodes.Timeout,
                    $"Host Librespot nie odpowiedział na polecenie „{command}” w bezpiecznym czasie.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new LibrespotHostException(
                    LibrespotHostErrorCodes.HostExited,
                    $"Proces hosta Librespot zakończył się w trakcie polecenia „{command}”.");
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.HostExited,
                $"Utracono połączenie z hostem Librespot przy poleceniu „{command}”.",
                exception);
        }
        finally
        {
            pending.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Czyta stdout hosta linia po linii. Kazda linia MUSI byc obiektem JSON;
    /// cokolwiek innego jest bledem protokolu, nie tekstem do zignorowania.
    /// Koniec strumienia konczy wszystkie oczekujace polecenia.
    /// </summary>
    private async Task ReadLoopAsync(ILibrespotHostProcess host, CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        var chunk = new char[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await host.StandardOutput.ReadAsync(chunk, cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                for (var index = 0; index < read; index++)
                {
                    var character = chunk[index];
                    if (character == '\n')
                    {
                        var line = buffer.ToString().TrimEnd('\r');
                        buffer.Clear();
                        if (line.Trim().Length == 0) continue;
                        if (!HandleLine(line)) return;
                        continue;
                    }
                    buffer.Append(character);
                    if (buffer.Length > options.MaxLineLength)
                    {
                        FailAll(
                            LibrespotHostErrorCodes.LineTooLong,
                            "Host Librespot przysłał zbyt długą linię. Sesja została zatrzymana.");
                        host.Kill();
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }

        var exitCode = host.ExitCode;
        FailAll(
            LibrespotHostErrorCodes.HostExited,
            exitCode is null
                ? "Strumień hosta Librespot został zamknięty. Sesja została zatrzymana."
                : $"Proces hosta Librespot zakończył się (kod {exitCode.Value.ToString(CultureInfo.InvariantCulture)}).");
    }

    /// <summary>Zwraca false, gdy petla czytania ma sie zakonczyc.</summary>
    private bool HandleLine(string line)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(line) as JsonObject
                ?? throw new JsonException("Linia hosta nie jest obiektem JSON.");
        }
        catch (JsonException)
        {
            // Nie logujemy tresci linii - mogla powstac z echa wejscia.
            FailAll(
                LibrespotHostErrorCodes.ProtocolViolation,
                "Host Librespot przysłał dane niezgodne z protokołem. Sesja została zatrzymana.");
            return false;
        }

        var type = root["type"]?.GetValue<string>() ?? string.Empty;
        switch (type)
        {
            case "ready":
                var version = ReadInt(root, "protocolVersion") ?? 0;
                if (version != LibrespotHostContract.ProtocolVersion)
                {
                    var message = "Host Librespot zgłosił nieobsługiwaną wersję protokołu "
                        + $"({version.ToString(CultureInfo.InvariantCulture)}); AMC obsługuje "
                        + $"{LibrespotHostContract.ProtocolVersion.ToString(CultureInfo.InvariantCulture)}. "
                        + "Zaktualizuj AMC albo składniki Librespot.";
                    readySignal.TrySetException(new LibrespotHostException(
                        LibrespotHostErrorCodes.ProtocolVersionMismatch, message));
                    FailAll(LibrespotHostErrorCodes.ProtocolVersionMismatch, message);
                    return false;
                }
                readySignal.TrySetResult(version);
                return true;

            case "ack":
            case "devices":
                var requestId = ReadLong(root, "requestId");
                if (requestId is not null && pending.TryRemove(requestId.Value, out var waiting))
                    waiting.Completion.TrySetResult(root);
                return true;

            case "error":
                HandleErrorLine(root);
                return true;

            case "state":
                HandleStateLine(root);
                return true;

            case "ended":
                HandleEndedLine(root);
                return true;

            case "trackError":
                HandleTrackErrorLine(root);
                return true;

            default:
                // Nieznany typ zdarzenia nie moze przerwac sesji, ale tez nie
                // moze przejsc w ciszy - zglaszamy go jako awarie protokolu.
                HostFailed?.Invoke(this, new LibrespotHostFailureEventArgs(
                    LibrespotHostErrorCodes.ProtocolViolation,
                    "Host Librespot przysłał nieznany typ zdarzenia."));
                return true;
        }
    }

    private void HandleErrorLine(JsonObject root)
    {
        var code = root["code"]?.GetValue<string>() ?? LibrespotHostErrorCodes.HostError;
        var message = root["message"]?.GetValue<string>() ?? "Host Librespot zgłosił błąd.";
        var requestId = ReadLong(root, "requestId");
        if (requestId is not null && pending.TryRemove(requestId.Value, out var waiting))
        {
            waiting.Completion.TrySetException(new LibrespotHostException(code, message));
            return;
        }
        // Blad bez znanego requestId dotyczy calej sesji: musi byc slyszalny.
        HostFailed?.Invoke(this, new LibrespotHostFailureEventArgs(code, message));
    }

    private void HandleStateLine(JsonObject root)
    {
        var playId = ReadLong(root, "playId") ?? 0;
        var uri = root["uri"]?.GetValue<string>() ?? string.Empty;
        if (!IsCurrentPlay(playId, uri)) return;
        var isPlaying = ReadBool(root, "isPlaying");
        var isPaused = ReadBool(root, "isPaused");
        var position = TimeSpan.FromMilliseconds(Math.Max(0, ReadLong(root, "positionMs") ?? 0));
        var duration = TimeSpan.FromMilliseconds(Math.Max(0, ReadLong(root, "durationMs") ?? 0));
        lock (stateGate)
        {
            if (stopRequestedDuringPreparation || pauseRequestedDuringPreparation) return;
            lastPosition = position;
            lastPaused = isPaused;
            if (isPlaying)
            {
                isPreparing = false;
                playbackStarted = true;
            }
        }
        StateChanged?.Invoke(this, new LibrespotStateEventArgs(
            playId, uri, position, duration, isPlaying, isPaused));
    }

    private void HandleEndedLine(JsonObject root)
    {
        var playId = ReadLong(root, "playId") ?? 0;
        var uri = root["uri"]?.GetValue<string>() ?? string.Empty;
        if (!IsCurrentPlay(playId, uri)) return;
        lock (stateGate)
        {
            // Koniec przed potwierdzonym startem to nie koniec utworu, tylko
            // zerwane przygotowanie. Bez tego warunku sesja przeskakiwalaby
            // dalej po utworze, ktory nigdy nie zagral.
            if (!playbackStarted || isPreparing) return;
            playbackStarted = false;
            currentPlayId = 0;
            currentUri = string.Empty;
            lastPosition = TimeSpan.Zero;
        }
        PlaybackEnded?.Invoke(this, new LibrespotEndedEventArgs(playId, uri));
    }

    private void HandleTrackErrorLine(JsonObject root)
    {
        var playId = ReadLong(root, "playId") ?? 0;
        var uri = root["uri"]?.GetValue<string>() ?? string.Empty;
        if (!IsCurrentPlay(playId, uri)) return;
        var code = root["code"]?.GetValue<string>() ?? LibrespotHostErrorCodes.HostError;
        lock (stateGate)
        {
            isPreparing = false;
            playbackStarted = false;
            currentPlayId = 0;
            currentUri = string.Empty;
        }
        TrackFailed?.Invoke(this, new LibrespotTrackErrorEventArgs(playId, uri, code));
    }

    /// <summary>
    /// Czy zdarzenie dotyczy TEJ proby odtwarzania. Sam URI nie wystarcza: ten
    /// sam utwor uruchomiony dwa razy ma dwa playId, a zdarzenie pierwszej
    /// proby nie moze ruszyc drugiej.
    /// </summary>
    private bool IsCurrentPlay(long playId, string uri)
    {
        lock (stateGate)
        {
            if (disposed || playId <= 0 || playId != currentPlayId) return false;
            return uri.Length == 0
                || string.Equals(uri, currentUri, StringComparison.Ordinal);
        }
    }

    private void FailAll(string code, string message)
    {
        foreach (var key in pending.Keys.ToArray())
        {
            if (pending.TryRemove(key, out var waiting))
                waiting.Completion.TrySetException(new LibrespotHostException(code, message));
        }
        readySignal.TrySetException(new LibrespotHostException(code, message));
        lock (stateGate)
        {
            isPreparing = false;
            playbackStarted = false;
        }
        HostFailed?.Invoke(this, new LibrespotHostFailureEventArgs(code, message));
    }

    private void Shutdown(string code, string message)
    {
        var current = Volatile.Read(ref process);
        current?.Kill();
        FailAll(code, message);
    }

    private void ThrowIfDisposed()
    {
        lock (stateGate)
        {
            if (disposed)
            {
                throw new LibrespotHostException(
                    LibrespotHostErrorCodes.Disposed,
                    "Sesja Spotify — Librespot została już zamknięta.");
            }
        }
    }

    private static int? ReadInt(JsonObject root, string name) =>
        root[name] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    private static long? ReadLong(JsonObject root, string name)
    {
        if (root[name] is not JsonValue value) return null;
        if (value.TryGetValue(out long number)) return number;
        return value.TryGetValue(out int small) ? small : null;
    }

    private static bool ReadBool(JsonObject root, string name) =>
        root[name] is JsonValue value && value.TryGetValue(out bool flag) && flag;

    public async ValueTask DisposeAsync()
    {
        lock (stateGate)
        {
            if (disposing || disposed) return;
            // UWAGA: flagi "disposed" NIE wolno tu ustawic. SendAsync sprawdza ja
            // pierwsza linia, wiec ShutdownAsync odbilby sie od wlasnego Dispose
            // i host NIGDY nie dostalby polecenia "shutdown" - konczyl zawsze
            // Kill(). Ubity host nie zwalnia urzadzenia audio po dobremu.
            // "disposing" blokuje drugie wejscie w Dispose, ale przepuszcza
            // pozegnalne polecenie.
            disposing = true;
        }
        try
        {
            await ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (LibrespotHostException)
        {
        }
        lock (stateGate) disposed = true;
        await lifetime.CancelAsync().ConfigureAwait(false);
        var loop = readerLoop;
        if (loop is not null)
        {
            try
            {
                await loop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
            {
            }
        }
        FailAll(LibrespotHostErrorCodes.Disposed, "Sesja Spotify — Librespot została zamknięta.");
        Volatile.Read(ref process)?.Dispose();
        lifetime.Dispose();
        writeGate.Dispose();
    }

    public void Dispose()
    {
        lock (stateGate)
        {
            if (disposed) return;
            disposed = true;
        }
        lifetime.Cancel();
        FailAll(LibrespotHostErrorCodes.Disposed, "Sesja Spotify — Librespot została zamknięta.");
        Volatile.Read(ref process)?.Dispose();
        lifetime.Dispose();
        writeGate.Dispose();
    }

    private sealed class PendingRequest
    {
        public TaskCompletionSource<JsonObject> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
