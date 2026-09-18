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
/// Granice, ktore ta klasa trzyma:
/// - zmiana tempa odtwarzania NIE jest obslugiwana (tak jak w sesji SDK),
/// - token konta nigdy nie przechodzi tedy do argumentow procesu; idzie
///   wylacznie stdin-em w "initialize", ktore robi transport,
/// - zadnego samoczynnego ponownego logowania i zadnego cichego powrotu do
///   sesji SDK. Awaria jest mowiona wprost, bo cisza jest dla uzytkownika
///   niewidomego najgorszym z bledow,
/// - zdarzenia STAREGO playId nie ruszaja biezacego utworu; playId nadaje AMC.
/// </summary>
internal sealed class SpotifyLibrespotMediaOutput : IMediaOutput, IDisposable
{
    private readonly LibrespotHostClient client;
    private readonly Action<Action> uiInvoker;
    private readonly object stateGate = new();
    private MediaItem? currentItem;
    private string? loadedItemId;
    private TimeSpan position;
    private long currentPlayId;
    private bool isPreparing;
    private bool playbackStarted;
    private bool disposed;

    /// <param name="client">Transport do osobnego procesu hosta Librespot.</param>
    /// <param name="uiInvoker">
    /// Sposob wejscia na watek interfejsu. Wstrzykiwany, zeby ta klasa dala sie
    /// zmierzyc bez okna WPF; w programie to dispatcher okna glownego.
    /// </param>
    public SpotifyLibrespotMediaOutput(LibrespotHostClient client, Action<Action> uiInvoker)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.uiInvoker = uiInvoker ?? throw new ArgumentNullException(nameof(uiInvoker));
        client.StateChanged += OnStateChanged;
        client.PlaybackEnded += OnPlaybackEnded;
        client.TrackFailed += OnTrackFailed;
        client.HostFailed += OnHostFailed;
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

    /// <summary>Host Librespot nie zmienia tempa odtwarzania, tak jak sesja SDK.</summary>
    public bool SupportsPlaybackRate => false;

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
        lock (stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            // playId nadaje AMC. Dwa uruchomienia tego samego utworu to dwa
            // rozne playId, wiec zdarzenia pierwszego nie ruszaja drugiego.
            playId = ++currentPlayId;
            currentItem = item;
            loadedItemId = item.Id;
            this.position = start;
            isPreparing = true;
            playbackStarted = false;
        }

        RaiseOnUi(() => PlaybackPreparing?.Invoke(
            this, new MediaPlaybackPreparingEventArgs(item, false)));
        _ = StartAsync(item, uri, start, volume, playId);
    }

    private async Task StartAsync(
        MediaItem item, string uri, TimeSpan start, int volume, long playId)
    {
        try
        {
            await client.SetVolumeAsync(volume).ConfigureAwait(false);
            var outcome = await client.PlayAsync(uri, start, playId).ConfigureAwait(false);
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
            }
        }
        catch (LibrespotHostException exception)
        {
            HandleFailure(playId, item, FriendlyFailure(exception));
        }
    }

    public void Pause()
    {
        lock (stateGate)
        {
            if (isPreparing) isPreparing = false;
            playbackStarted = false;
        }
        _ = ForwardAsync(client.PauseAsync());
    }

    /// <summary>Wznowienie po pauzie bez ponownego wczytywania utworu.</summary>
    public void Resume() => _ = ForwardAsync(client.ResumeAsync());

    public void Stop()
    {
        lock (stateGate)
        {
            loadedItemId = null;
            currentItem = null;
            position = TimeSpan.Zero;
            isPreparing = false;
            playbackStarted = false;
        }
        _ = ForwardAsync(client.StopAsync());
    }

    public void Seek(TimeSpan position)
    {
        var normalized = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        lock (stateGate) this.position = normalized;
        _ = ForwardAsync(client.SeekAsync(normalized));
    }

    public void SetVolume(int volume) =>
        _ = ForwardAsync(client.SetVolumeAsync(Math.Clamp(volume, 0, 100)));

    /// <summary>
    /// Lista urzadzen wyjscia hosta. Nie wymaga konta, wiec wolno ja pokazac
    /// przed zalogowaniem.
    /// </summary>
    public Task<IReadOnlyList<LibrespotOutputDevice>> GetOutputDevicesAsync(
        CancellationToken cancellationToken = default) =>
        client.GetDevicesAsync(cancellationToken);

    /// <summary>
    /// Przelacza wyjscie na DOKLADNA nazwe z <see cref="GetOutputDevicesAsync"/>
    /// (null znaczy domyslne), zachowujac utwor, pozycje i pauze. Odmowa hosta
    /// jest zglaszana jako blad - bez cichego powrotu na stare wyjscie.
    /// </summary>
    public async Task<bool> TrySetOutputDeviceAsync(
        string? deviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SetOutputDeviceAsync(deviceName, cancellationToken).ConfigureAwait(false);
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
        var raiseStarted = false;
        lock (stateGate)
        {
            // Zdarzenie STAREJ proby nie moze zmienic czasu ani tytulu tego, co
            // gra teraz - czytnik ekranu przeczytalby nie ten utwor.
            if (disposed || e.PlayId != currentPlayId) return;
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
            RaiseOnUi(() => DurationAvailable?.Invoke(
                this,
                new MediaDurationAvailableEventArgs(item, item.Duration, item.SampleRateHz ?? 0)));
        }
        if (raiseStarted)
        {
            RaiseOnUi(() => PlaybackStarted?.Invoke(
                this, new MediaPlaybackStartedEventArgs(item)));
        }
    }

    private void OnPlaybackEnded(object? sender, LibrespotEndedEventArgs e)
    {
        MediaItem? item;
        lock (stateGate)
        {
            if (disposed || e.PlayId != currentPlayId) return;
            if (!playbackStarted || isPreparing) return;
            item = currentItem;
            playbackStarted = false;
            loadedItemId = null;
        }
        if (item is null) return;
        RaiseOnUi(() => PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(item)));
    }

    private void OnTrackFailed(object? sender, LibrespotTrackErrorEventArgs e)
    {
        MediaItem? item;
        lock (stateGate)
        {
            if (disposed || e.PlayId != currentPlayId) return;
            item = currentItem;
        }
        if (item is null) return;
        HandleFailure(e.PlayId, item, FriendlyTrackFailure(e.Code));
    }

    private void OnHostFailed(object? sender, LibrespotHostFailureEventArgs e)
    {
        MediaItem? item;
        long playId;
        lock (stateGate)
        {
            if (disposed) return;
            item = currentItem;
            playId = currentPlayId;
        }
        var message = FriendlyHostFailure(e.Code, e.Message);
        DiagnosticLog.Warning("spotify-librespot", $"Awaria hosta; kod: {e.Code}.");
        if (item is not null) HandleFailure(playId, item, message);
        else RaiseOnUi(() => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(null, message)));
    }

    private void HandleFailure(long playId, MediaItem item, string message)
    {
        lock (stateGate)
        {
            if (disposed || playId != currentPlayId) return;
            loadedItemId = null;
            isPreparing = false;
            playbackStarted = false;
        }
        DiagnosticLog.Warning(
            "spotify-librespot",
            $"Odtwarzanie nie powiodło się; element: {item.ExternalId ?? item.Id}.");
        RaiseOnUi(() => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));
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

    public void Dispose()
    {
        lock (stateGate)
        {
            if (disposed) return;
            disposed = true;
        }
        client.StateChanged -= OnStateChanged;
        client.PlaybackEnded -= OnPlaybackEnded;
        client.TrackFailed -= OnTrackFailed;
        client.HostFailed -= OnHostFailed;
    }
}
