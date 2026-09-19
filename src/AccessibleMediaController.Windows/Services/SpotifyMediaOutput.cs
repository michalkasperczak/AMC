using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Uruchamia oficjalny Spotify Web Playback SDK w niefokusowalnym WebView2.
///
/// Zasady, ktorych trzeba sie tu trzymac:
/// - Do SDK ida tylko identyfikator utworu i krotko zyjacy token konta.
///   Chronione adresy audio nie wchodza do modelu aplikacji ani do logu.
/// - Kazde polecenie ma numer. Odpowiedz na POPRZEDNIE polecenie nie moze
///   zmienic stanu biezacego utworu: po szybkiej zmianie utworu czytnik
///   przeczytalby czas i tytul nie tego, co gra.
/// - Wbudowany odtwarzacz wymaga konta Premium. Na koncie darmowym trzeba
///   powiedziec to WPROST, a nie zglaszac ogolny blad odtwarzania.
/// </summary>
internal sealed class SpotifyMediaOutput : IMediaOutput, IDisposable
{
    private const string VirtualHostName = "amc.spotify.local";
    private readonly SpotifyIntegrationService integration;
    private readonly WebView2 webView;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource<bool> bridgeReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object stateGate = new();
    private Task? initialization;
    private MediaItem? currentItem;
    private string? loadedItemId;
    private TimeSpan position;
    private bool isPreparing;
    private bool wasPlaying;
    private bool playbackStarted;
    private bool disposed;
    private int playbackRequestVersion;
    private long preparationTimestamp;

    public SpotifyMediaOutput(SpotifyIntegrationService integration, WebView2 webView)
    {
        this.integration = integration;
        this.webView = webView;
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

    // Spotify Web Playback SDK nie udostepnia zmiany tempa odtwarzania.
    public bool SupportsPlaybackRate => false;

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        ArgumentNullException.ThrowIfNull(item);
        var trackUri = PlaybackTrackUri(item);
        var rejection = PlaybackRejection(item);
        if (rejection is not null)
        {
            RaiseOnUi(() => PlaybackFailed?.Invoke(
                this,
                new MediaOutputFailedEventArgs(item, rejection)));
            return;
        }

        int version;
        var resumeLoadedItem = false;
        lock (stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            version = ++playbackRequestVersion;
            preparationTimestamp = Stopwatch.GetTimestamp();
            resumeLoadedItem = !isPreparing
                && string.Equals(loadedItemId, item.Id, StringComparison.Ordinal);
            currentItem = item;
            loadedItemId = item.Id;
            this.position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            isPreparing = true;
            playbackStarted = false;
        }

        PlaybackPreparing?.Invoke(this, new MediaPlaybackPreparingEventArgs(item, false));
        _ = StartPlaybackAsync(item, position, volume, version, resumeLoadedItem, lifetime.Token);
        // BEZPIECZNIK z sesji radia i YouTube (WatchPreparationTimeoutAsync):
        // gdy silnik NIE odpowie wcale - nie wczyta sie SDK, padnie siec, DRM
        // odmowi - bez tego program stoi w ciszy i NIC nie mowi, a uzytkownik
        // niewidomy nie ma jak zgadnac, czy trwa wczytywanie, czy zawieszenie.
        // Radio nauczylo nas, ze cisza bez komunikatu to najgorszy z bledow.
        _ = WatchPreparationTimeoutAsync(item, version, PreparationTimeout);
    }

    public void Pause()
    {
        int version;
        lock (stateGate)
        {
            version = ++playbackRequestVersion;
            if (isPreparing)
            {
                loadedItemId = null;
                isPreparing = false;
            }
            wasPlaying = false;
            playbackStarted = false;
        }
        PostCommand(new { type = "pause", requestVersion = version });
    }

    public void Stop()
    {
        int version;
        lock (stateGate)
        {
            version = ++playbackRequestVersion;
            loadedItemId = null;
            currentItem = null;
            position = TimeSpan.Zero;
            isPreparing = false;
            wasPlaying = false;
            playbackStarted = false;
        }
        PostCommand(new { type = "stop", requestVersion = version });
    }

    public void Seek(TimeSpan position)
    {
        var normalized = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        lock (stateGate) this.position = normalized;
        PostCommand(new { type = "seek", position = normalized.TotalSeconds });
    }

    public void SetVolume(int volume) => PostCommand(new
    {
        type = "volume",
        volume = Math.Clamp(volume, 0, 100)
    });

    public void SetPlaybackRate(double playbackRate)
    {
        // Spotify Web Playback SDK nie ma zmiany tempa odtwarzania.
    }

    private async Task InitializeAsync()
    {
        try
        {
            var assetFolder = Path.Combine(AppContext.BaseDirectory, "spotify-player");
            if (!File.Exists(Path.Combine(assetFolder, "index.html"))
                || !File.Exists(Path.Combine(assetFolder, "bridge.js")))
            {
                throw new FileNotFoundException(
                    "W instalacji AMC brakuje składników odtwarzacza Spotify.");
            }

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessibleMediaController",
                "WebView2",
                "Spotify");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: userDataFolder,
                options: TidalWebViewPolicy.CreateOptions());
            await webView.EnsureCoreWebView2Async(environment);
            if (disposed) return;

            var core = webView.CoreWebView2;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            core.WebMessageReceived += Core_WebMessageReceived;
            core.ProcessFailed += Core_ProcessFailed;
            core.NavigationCompleted += Core_NavigationCompleted;
            core.SetVirtualHostNameToFolderMapping(
                VirtualHostName,
                assetFolder,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.Navigate($"https://{VirtualHostName}/index.html");
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            or WebView2RuntimeNotFoundException
            or InvalidOperationException
            or COMException)
        {
            bridgeReady.TrySetException(exception);
            DiagnosticLog.Error(
                "spotify-player",
                "Nie udało się przygotować odtwarzacza Spotify.",
                exception);
        }
    }

    private async Task StartPlaybackAsync(
        MediaItem item,
        TimeSpan startPosition,
        int volume,
        int version,
        bool resume,
        CancellationToken cancellationToken)
    {
        try
        {
            var startedAt = Stopwatch.GetTimestamp();
            var credentials = await integration
                .GetPlaybackCredentialsAsync(cancellationToken).ConfigureAwait(false);
            if (!IsCurrentRequest(version, item.Id)) return;

            // Konto darmowe: powiedz to wprost, zamiast czekac na ogolny blad SDK.
            if (!credentials.AllowsPlayback)
            {
                RaiseOnUi(() => HandleFailure(
                    item,
                    "Wbudowany odtwarzacz Spotify wymaga konta Premium. To konto nie zagra w AMC."));
                return;
            }

            var poswiadczenia = new
            {
                token = credentials.AccessToken,
                expires = credentials.ExpiresAtUtc.ToUnixTimeMilliseconds()
            };

            // ZGLOSZENIE Michala 18.09.2026: "Spotify - cisza, czasu nie odtwarza".
            //
            // ZAKLESZCZENIE KOLEJNOSCI, ktore to powodowalo: czekalismy tu na
            // gotowosc mostka, a mostek zglaszal gotowosc dopiero po podlaczeniu
            // odtwarzacza, ktore robil w obsludze polecenia "graj" - czyli nigdy,
            // bo polecenia jeszcze nie wyslalismy. AMC czekalo 45 sekund i mowilo
            // "odtwarzacz nie odpowiedzial", zadnego bledu w dzienniku.
            //
            // Dlatego najpierw wysylamy "prepare" (samo logowanie), ktore kaze
            // mostkowi podlaczyc odtwarzacz, a DOPIERO POTEM czekamy na gotowosc.
            // Poswiadczenia musza byc gotowe wczesniej, bo SDK wola je wlasnie
            // przy podlaczaniu.
            await SendPrepareAsync(poswiadczenia, version, item.Id, cancellationToken)
                .ConfigureAwait(false);
            await PrepareBridgeAsync(cancellationToken).ConfigureAwait(false);
            if (!IsCurrentRequest(version, item.Id)) return;

            DiagnosticLog.Info("spotify-player-timing",
                $"Przygotowanie hosta i poświadczeń; próba: {version}; czas: {Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F0} ms.");

            var command = new
            {
                type = resume ? "resume" : "play",
                requestVersion = version,
                trackUri = PlaybackTrackUri(item),
                position = Math.Max(0, startPosition.TotalSeconds),
                volume = Math.Clamp(volume, 0, 100),
                credentials = poswiadczenia
            };
            await webView.Dispatcher.InvokeAsync(
                () =>
                {
                    if (IsCurrentRequest(version, item.Id))
                        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(command));
                },
                DispatcherPriority.Send,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RaiseOnUi(() =>
            {
                if (IsCurrentRequest(version, item.Id))
                    HandleFailure(item, FriendlyFailure(exception.Message));
            });
        }
    }

    // Wysyla polecenie "prepare" POMIJAJAC PostCommand. PostCommand milczy,
    // dopoki mostek nie jest gotowy (bridgeReady) - a wlasnie to polecenie ma
    // te gotowosc wywolac, wiec przez PostCommand nigdy by nie wyszlo.
    // Host WebView2 musi juz istniec, dlatego najpierw EnsureInitialization.
    private async Task SendPrepareAsync(
        object credentials,
        int version,
        string itemId,
        CancellationToken cancellationToken)
    {
        await EnsureInitialization().ConfigureAwait(false);
        if (bridgeReady.Task.IsCompletedSuccessfully) return;
        var command = new { type = "prepare", requestVersion = version, credentials };
        var json = JsonSerializer.Serialize(command);
        await webView.Dispatcher.InvokeAsync(
            () =>
            {
                if (!disposed && IsCurrentRequest(version, itemId))
                    webView.CoreWebView2.PostWebMessageAsJson(json);
            },
            DispatcherPriority.Send,
            cancellationToken);
    }

    private async Task PrepareBridgeAsync(CancellationToken cancellationToken)
    {
        await EnsureInitialization().ConfigureAwait(false);
        await bridgeReady.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task EnsureInitialization()
    {
        lock (stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return initialization ??= InitializeAsync();
        }
    }

    private bool IsCurrentRequest(int version, string itemId)
    {
        lock (stateGate)
        {
            return !disposed
                && playbackRequestVersion == version
                && string.Equals(loadedItemId, itemId, StringComparison.Ordinal);
        }
    }

    private void PostCommand(object command)
    {
        if (disposed || !bridgeReady.Task.IsCompletedSuccessfully) return;
        var json = JsonSerializer.Serialize(command);
        _ = webView.Dispatcher.InvokeAsync(() =>
        {
            if (!disposed && webView.CoreWebView2 is not null)
                webView.CoreWebView2.PostWebMessageAsJson(json);
        }, DispatcherPriority.Send);
    }

    private void Core_NavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess) return;
        var message = $"Nie udało się uruchomić silnika Spotify: {e.WebErrorStatus}.";
        bridgeReady.TrySetException(new InvalidOperationException(message));
        HandleFailure(CurrentItem(), message);
    }

    private void Core_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        var message = "Proces odtwarzacza Spotify został nieoczekiwanie zatrzymany.";
        bridgeReady.TrySetException(new InvalidOperationException(message));
        HandleFailure(CurrentItem(), message);
    }

    private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        => ProcessBridgeMessage(e.WebMessageAsJson);

    internal void ProcessBridgeMessage(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            var type = Text(root, "type");
            // Sam identyfikator utworu nie wystarcza, gdy ten sam utwor otwarto
            // ponownie. Odpowiedz na poprzednie polecenie nie moze wplywac na nowe.
            //
            // Wyjatki: "ready" dotyczy calego mostka, a nie utworu. Blad zgloszony
            // w odpowiedzi na "prepare" tez nie ma utworu (podlaczanie odtwarzacza
            // idzie przed wyborem utworu) - gdybysmy go tu odrzucili, nieudane
            // podlaczenie znow konczyloby sie cisza az do limitu 45 sekund.
            var bezUtworu = type == "ready"
                || (type == "error" && !root.TryGetProperty("trackUri", out _));
            if (!bezUtworu && !MatchesBridgeRequest(root)) return;
            switch (type)
            {
                case "ready":
                    bridgeReady.TrySetResult(true);
                    DiagnosticLog.Info("spotify-player", "Odtwarzacz Spotify jest gotowy.");
                    break;
                case "progress":
                    ApplyProgress(root);
                    break;
                case "state":
                    ApplyProgress(root);
                    ApplyPlaybackState(Text(root, "state"), Text(root, "trackUri"));
                    break;
                case "timing":
                    DiagnosticLog.Info("spotify-player-timing",
                        $"SDK; próba: {playbackRequestVersion}; oczekiwanie: {TimingMilliseconds(root, "queueMs")} ms; wczytanie: {TimingMilliseconds(root, "loadMs")} ms.");
                    break;
                case "ended":
                    ApplyEnded(root);
                    break;
                case "error":
                    var rawFailure = string.Join(
                        ' ',
                        new[]
                        {
                            Text(root, "message"),
                            Text(root, "code"),
                            Text(root, "id")
                        }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    // Blad z "prepare" nie ma utworu. Zglaszamy go dla biezacego
                    // elementu, zeby uzytkownik uslyszal przyczyne (np. brak
                    // Premium albo wygasle logowanie) zamiast czekac w ciszy.
                    var failedItem = CurrentItemMatching(Text(root, "trackUri"))
                        ?? CurrentItem();
                    if (failedItem is null) break;
                    DiagnosticLog.Warning(
                        "spotify-player-detail",
                        $"Szczegóły błędu SDK; element: {failedItem.ExternalId}; {SafeDiagnostic(rawFailure)}");
                    // Nieudane przygotowanie odtwarzacza musi tez przerwac
                    // czekanie na gotowosc - inaczej PrepareBridgeAsync wisi
                    // az do limitu 45 sekund, mimo ze przyczyne juz znamy.
                    bridgeReady.TrySetException(
                        new InvalidOperationException(FriendlyFailure(rawFailure)));
                    HandleFailure(failedItem, FriendlyFailure(rawFailure));
                    break;
            }
        }
        catch (JsonException exception)
        {
            DiagnosticLog.Warning(
                "spotify-player",
                $"Odrzucono nieprawidłowy komunikat odtwarzacza: {exception.GetType().Name}.");
        }
    }

    private void ApplyProgress(JsonElement root)
    {
        var item = CurrentItemMatching(Text(root, "trackUri"));
        if (item is null) return;
        var positionSeconds = Number(root, "position");
        var durationSeconds = Number(root, "duration");
        lock (stateGate)
        {
            if (positionSeconds >= 0) position = TimeSpan.FromSeconds(positionSeconds);
        }
        // Czas z SDK jest prawda o tym, co gra. Bez tego lista pokazywala czas
        // z katalogu albo zero, a pasek postepu nie mial na czym sie oprzec.
        if (durationSeconds > 0 && Math.Abs(item.Duration.TotalSeconds - durationSeconds) >= 1)
        {
            item.Duration = TimeSpan.FromSeconds(durationSeconds);
            DurationAvailable?.Invoke(
                this,
                new MediaDurationAvailableEventArgs(item, item.Duration, item.SampleRateHz ?? 0));
        }
    }

    private void ApplyPlaybackState(string state, string trackUri)
    {
        if (CurrentItemMatching(trackUri) is null) return;
        var playing = string.Equals(state, "PLAYING", StringComparison.OrdinalIgnoreCase);
        MediaItem? item;
        var raiseStarted = false;
        lock (stateGate)
        {
            item = currentItem;
            if (playing)
            {
                isPreparing = false;
                playbackStarted = true;
                raiseStarted = !wasPlaying;
            }
            wasPlaying = playing;
        }
        if (raiseStarted && item is not null)
        {
            if (preparationTimestamp != 0)
                DiagnosticLog.Info("spotify-player-timing",
                    $"Potwierdzony start; próba: {playbackRequestVersion}; od polecenia AMC: {Stopwatch.GetElapsedTime(preparationTimestamp).TotalMilliseconds:F0} ms.");
            PlaybackStarted?.Invoke(this, new MediaPlaybackStartedEventArgs(item));
        }
    }

    private void ApplyEnded(JsonElement root)
    {
        if (CurrentItemMatching(Text(root, "trackUri")) is null) return;
        MediaItem? item;
        lock (stateGate)
        {
            if (!playbackStarted || isPreparing) return;
            item = currentItem;
            isPreparing = false;
            wasPlaying = false;
            playbackStarted = false;
            loadedItemId = null;
        }
        if (item is not null)
        {
            DiagnosticLog.Info("spotify-player", $"Potwierdzony koniec utworu: {item.ExternalId}.");
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(item));
        }
    }

    private bool MatchesBridgeRequest(JsonElement root)
    {
        lock (stateGate)
            return !disposed
                && root.TryGetProperty("requestVersion", out var version)
                && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var serial)
                && serial == playbackRequestVersion
                && CurrentItemMatching(Text(root, "trackUri")) is not null;
    }

    private MediaItem? CurrentItem()
    {
        lock (stateGate) return currentItem;
    }

    private MediaItem? CurrentItemMatching(string trackUri)
    {
        lock (stateGate)
        {
            if (currentItem is null
                || loadedItemId is null
                || !string.Equals(loadedItemId, currentItem.Id, StringComparison.Ordinal))
            {
                return null;
            }

            return !string.IsNullOrWhiteSpace(trackUri)
                && string.Equals(PlaybackTrackUri(currentItem), trackUri, StringComparison.Ordinal)
                    ? currentItem
                    : null;
        }
    }

    /// <summary>
    /// Wspolna bramka odtwarzania OBU silnikow Spotify (SDK i Librespot).
    /// Zwraca komunikat dla uzytkownika, gdy pozycji nie wolno wyslac do silnika,
    /// albo null gdy wolno.
    ///
    /// Odtwarzalne sa TYLKO utwor i ODCINEK PODCASTU. Album, wykonawca,
    /// playlista i podcast to KONTENERY - Enter na nich nie moze zaczac
    /// niechcianego odtwarzania czegokolwiek, bo uzytkownik nie wybral jeszcze
    /// nagrania. Komunikat musi powiedziec, co zrobic, a nie tylko ze sie nie da.
    /// </summary>
    internal static string? PlaybackRejection(MediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind is MediaItemKind.Album or MediaItemKind.Artist
            or MediaItemKind.Playlist or MediaItemKind.Podcast)
        {
            return item.Kind == MediaItemKind.Podcast
                ? "To jest podcast. Otwórz go strzałką w prawo i wybierz odcinek."
                : "To jest album, wykonawca lub playlista. Otwórz ją strzałką w prawo i wybierz utwór.";
        }
        if (item.Kind is not (MediaItemKind.Track or MediaItemKind.Episode)
            || string.IsNullOrWhiteSpace(PlaybackTrackUri(item)))
        {
            return "Tego elementu Spotify nie można odtworzyć w AMC.";
        }
        return null;
    }

    /// <summary>
    /// Adres pozycji dla SDK. Bierzemy go z Source ("spotify:track:..." albo
    /// "spotify:episode:..."), a gdy go brakuje - skladamy z identyfikatora
    /// katalogowego, zeby pozycje zapamietane starsza wersja AMC tez dawaly sie
    /// odtworzyc.
    ///
    /// ODCINEK PODCASTU MA INNY PRZEDROSTEK niz utwor. Sklejanie wszystkiego jako
    /// "spotify:track:" dawalo odcinek, ktory wyglada dobrze na liscie i nie gra.
    /// </summary>
    internal static string PlaybackTrackUri(MediaItem item)
    {
        var source = item.Source?.Trim() ?? string.Empty;
        if (source.StartsWith("spotify:track:", StringComparison.Ordinal)
            || source.StartsWith("spotify:episode:", StringComparison.Ordinal))
        {
            return source;
        }
        var externalId = item.ExternalId?.Trim() ?? string.Empty;
        if (externalId.Length == 0) return string.Empty;
        var przedrostek = item.Kind == MediaItemKind.Episode ? "episode" : "track";
        return $"spotify:{przedrostek}:{externalId}";
    }

    private void HandleFailure(MediaItem? item, string message)
    {
        lock (stateGate)
        {
            if (item is null || loadedItemId is null
                || !string.Equals(currentItem?.Id, item.Id, StringComparison.Ordinal)) return;
            loadedItemId = null;
            isPreparing = false;
            wasPlaying = false;
            playbackStarted = false;
        }
        DiagnosticLog.Warning(
            "spotify-player",
            $"Odtwarzanie nie powiodło się; element: {item?.ExternalId ?? "brak"}; {message}");
        RaiseOnUi(() => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));
    }

    /// <summary>
    /// Ile czekamy, zanim powiemy uzytkownikowi, ze silnik nie odpowiada.
    /// 45 sekund, bo Web Playback SDK musi sciagnac skrypt Spotify, zarejestrowac
    /// urzadzenie i przejsc uzgodnienie DRM - na wolnym laczu 25 sekund radia
    /// bylo za malo i dawalo falszywy alarm przy dzialajacym odtwarzaniu.
    /// </summary>
    private static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Bezpiecznik ciszy: jesli po PreparationTimeout nadal przygotowujemy TE
    /// SAMA probe odtwarzania, mowimy o tym wprost zamiast milczec.
    /// Sprawdzenie numeru proby jest istotne - bez niego bezpiecznik starej
    /// proby zabilby komunikatem odtwarzanie juz zmienionego utworu.
    /// </summary>
    private async Task WatchPreparationTimeoutAsync(MediaItem item, int requestVersion, TimeSpan timeout)
    {
        await Task.Delay(timeout).ConfigureAwait(false);
        lock (stateGate)
        {
            if (disposed) return;
            if (requestVersion != playbackRequestVersion) return;
            if (!isPreparing || playbackStarted) return;
            isPreparing = false;
        }

        DiagnosticLog.Warning(
            "spotify-player",
            $"Przekroczono czas przygotowania odtwarzania ({timeout.TotalSeconds:F0} s); element: {item.ExternalId ?? item.Id}.");
        HandleFailure(
            item,
            "Odtwarzacz Spotify nie odpowiedział w bezpiecznym czasie. Sprawdź połączenie z internetem i spróbuj ponownie.");
    }

    private void RaiseOnUi(Action action)
    {
        if (webView.Dispatcher.CheckAccess()) action();
        else _ = webView.Dispatcher.InvokeAsync(action, DispatcherPriority.Send);
    }

    /// <summary>
    /// Zamienia surowy komunikat SDK na zdanie, z którego użytkownik wie, co
    /// zrobić. Nieznane treści przepuszczamy skrócone, nigdy z adresem
    /// chronionego strumienia ani z tokenem.
    /// </summary>
    internal static string FriendlyFailure(string rawMessage)
    {
        var text = rawMessage ?? string.Empty;
        if (text.Contains("premium", StringComparison.OrdinalIgnoreCase))
            return "Wbudowany odtwarzacz Spotify wymaga konta Premium.";
        if (text.Contains("invalid_token", StringComparison.OrdinalIgnoreCase)
            || text.Contains("401", StringComparison.Ordinal))
            return "Logowanie do Spotify wygasło. Otwórz okno konta Spotify i zaloguj się ponownie.";
        if (text.Contains("403", StringComparison.Ordinal))
            return "Spotify odmówiło odtworzenia tego utworu na tym koncie.";
        if (text.Contains("404", StringComparison.Ordinal))
            return "Odtwarzacz Spotify nie jest gotowy. Spróbuj ponownie za chwilę.";
        if (text.Contains("429", StringComparison.Ordinal))
            return "Spotify chwilowo ogranicza liczbę żądań. Spróbuj ponownie za chwilę.";
        return string.IsNullOrWhiteSpace(text)
            ? "Nie udało się odtworzyć tego utworu Spotify."
            : $"Nie udało się odtworzyć tego utworu Spotify. {SafeDiagnostic(text)}";
    }

    /// <summary>
    /// Do logu i komunikatów wpuszczamy tylko krótki, bezpieczny fragment:
    /// bez znaków sterujących i bez adresów, żeby chroniony strumień ani token
    /// nie wyciekły do pliku dziennika.
    /// </summary>
    internal static string SafeDiagnostic(string value)
    {
        var text = new string((value ?? string.Empty)
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();
        if (text.Contains("://", StringComparison.Ordinal)) return "szczegóły pominięto";
        return text.Length <= 200 ? text : text[..200];
    }

    private static string Text(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static double Number(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            && double.IsFinite(number)
            ? number
            : 0;

    private static long TimingMilliseconds(JsonElement root, string property) =>
        (long)Math.Clamp(Number(root, property), 0, 86400000);

    public void Dispose()
    {
        lock (stateGate)
        {
            if (disposed) return;
            disposed = true;
        }
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
