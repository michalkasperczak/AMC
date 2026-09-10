using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AccessibleMediaController.Windows.Services;

public sealed class TidalPlaybackNoticeEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

/// <summary>
/// Hosts the official TIDAL Web Player in a non-focusable WebView2 surface.
/// AMC sends only catalogue identifiers and the short-lived account token to
/// the SDK. Protected media addresses never enter the application model,
/// diagnostic log or clipboard.
/// </summary>
internal sealed class TidalMediaOutput : IMediaOutput, IDisposable
{
    private const string VirtualHostName = "dev.tidal.com";
    private readonly TidalIntegrationService integration;
    private readonly WebView2 webView;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource<bool> bridgeReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object stateGate = new();
    private readonly HashSet<string> announcedPreviews = new(StringComparer.Ordinal);
    private Task? initialization;
    private MediaItem? currentItem;
    private string? loadedItemId;
    private TimeSpan position;
    private bool isPreparing;
    private bool wasPlaying;
    private bool playbackStarted;
    private bool isPreview;
    private bool disposed;
    private int playbackRequestVersion;
    private long preparationTimestamp;
    internal TidalPlaybackDiagnostics Diagnostics { get; } = new();

    public TidalMediaOutput(TidalIntegrationService integration, WebView2 webView)
    {
        this.integration = integration;
        this.webView = webView;
    }

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;
    public event EventHandler<TidalPlaybackNoticeEventArgs>? PlaybackNotice;

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

    public bool SupportsPlaybackRate => false;

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        var productId = PlaybackProductId(item);
        if (item.Kind is not (MediaItemKind.Track or MediaItemKind.Video)
            || string.IsNullOrWhiteSpace(productId))
        {
            RaiseOnUi(() => PlaybackFailed?.Invoke(
                this,
                new MediaOutputFailedEventArgs(
                    item,
                    "Ten element TIDAL nie jest utworem ani materiałem wideo, który można odtworzyć.")));
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
            if (!resumeLoadedItem) isPreview = false;
        }

        Diagnostics.Begin(item.Title, resumeLoadedItem);
        PlaybackPreparing?.Invoke(this, new MediaPlaybackPreparingEventArgs(item, false));
        _ = StartPlaybackAsync(item, position, volume, version, resumeLoadedItem, lifetime.Token);
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
        PostCommand(new
        {
            type = "seek",
            position = normalized.TotalSeconds
        });
    }

    public void SetVolume(int volume) => PostCommand(new
    {
        type = "volume",
        volume = NormalizeVolume(volume)
    });

    public void SetPlaybackRate(double playbackRate)
    {
        // The official TIDAL Web Player currently exposes no playback-rate API.
    }

    private async Task InitializeAsync()
    {
        try
        {
            var assetFolder = Path.Combine(AppContext.BaseDirectory, "tidal-player");
            if (!File.Exists(Path.Combine(assetFolder, "index.html"))
                || !File.Exists(Path.Combine(assetFolder, "tidal-player.js")))
            {
                throw new FileNotFoundException(
                    "W instalacji AMC brakuje składników oficjalnego odtwarzacza TIDAL.");
            }

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessibleMediaController",
                "WebView2",
                "Tidal");
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
            // The official player development host uses this origin. Keeping
            // it inside WebView2's virtual-host mapping also satisfies the
            // SDK media CDN's origin policy without opening an external page.
            core.Navigate($"https://{VirtualHostName}:5173/index.html");
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
                "tidal-player",
                "Nie udało się przygotować oficjalnego odtwarzacza TIDAL.",
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
            // Prepare credentials while the isolated browser is starting.
            // Observe both tasks, including errors, without touching UI focus.
            var startedAt = Stopwatch.GetTimestamp();
            var readyTask = PrepareBridgeAsync(cancellationToken);
            var credentialsTask = integration.GetPlaybackCredentialsAsync(cancellationToken);
            await Task.WhenAll(readyTask, credentialsTask).ConfigureAwait(false);
            var credentials = await credentialsTask.ConfigureAwait(false);
            if (!IsCurrentRequest(version, item.Id)) return;
            DiagnosticLog.Info("tidal-player-timing",
                $"Przygotowanie hosta i poświadczeń; próba: {version}; czas: {Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds:F0} ms.");

            var command = new
            {
                type = resume ? "resume" : "play",
                requestVersion = version,
                productId = PlaybackProductId(item),
                productType = item.Kind == MediaItemKind.Video ? "video" : "track",
                sourceId = PlaybackProductId(item),
                sourceType = item.Kind == MediaItemKind.Video ? "VIDEO" : "TRACK",
                referenceId = PlaybackProductId(item),
                position = Math.Max(0, startPosition.TotalSeconds),
                volume = NormalizeVolume(volume),
                credentials = new
                {
                    clientId = credentials.ClientId,
                    token = credentials.AccessToken,
                    expires = credentials.ExpiresAtUtc.ToUnixTimeMilliseconds(),
                    scopes = credentials.Scopes,
                    userId = credentials.UserId
                }
            };
            await webView.Dispatcher.InvokeAsync(
                () =>
                {
                    if (IsCurrentRequest(version, item.Id))
                    {
                        Diagnostics.CredentialsPrepared();
                        DiagnosticLog.Info("tidal-player-auth", $"Próba: {version}; aktualne poświadczenie użytkownika przygotowane; wznowienie: {resume}.");
                        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(command));
                    }
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
        var message = $"Nie udało się uruchomić silnika TIDAL: {e.WebErrorStatus}.";
        bridgeReady.TrySetException(new InvalidOperationException(message));
        HandleFailure(CurrentItem(), message);
    }

    private void Core_ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        var message = "Proces odtwarzacza TIDAL został nieoczekiwanie zatrzymany.";
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
            // Product ID alone is insufficient when the same track was
            // reopened. A previous SDK request must never affect a new one.
            if (type != "ready" && !MatchesBridgeRequest(root)) return;
            switch (type)
            {
                case "ready":
                    bridgeReady.TrySetResult(true);
                    DiagnosticLog.Info("tidal-player", "Oficjalny odtwarzacz TIDAL jest gotowy.");
                    break;
                case "progress":
                    ApplyProgress(root);
                    break;
                case "transition":
                    ApplyTransition(root);
                    break;
                case "credentials":
                    if (root.TryGetProperty("authenticatedUser", out var authenticated)
                        && authenticated.ValueKind == JsonValueKind.True)
                    {
                        Diagnostics.CredentialsRead();
                        DiagnosticLog.Info("tidal-player-auth", $"Próba: {playbackRequestVersion}; SDK odczytał logowanie użytkownika.");
                    }
                    break;
                case "state":
                    ApplyPlaybackState(
                        Text(root, "state"),
                        Text(root, "productId"));
                    break;
                case "timing":
                    DiagnosticLog.Info("tidal-player-timing",
                        $"SDK; próba: {playbackRequestVersion}; oczekiwanie na poprzednie polecenie: {TimingMilliseconds(root, "queueMs")} ms; wczytanie: {TimingMilliseconds(root, "loadMs")} ms; start: {TimingMilliseconds(root, "playMs")} ms.");
                    break;
                case "ended":
                    ApplyEnded(root);
                    break;
                case "error":
                    var failedItem = CurrentItemMatching(Text(root, "productId"));
                    if (failedItem is null) break;
                    var rawFailure = string.Join(
                        ' ',
                        new[]
                        {
                            Text(root, "message"),
                            Text(root, "code"),
                            Text(root, "id")
                        }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    DiagnosticLog.Warning(
                        "tidal-player-detail",
                        $"Szczegóły błędu SDK; element: {failedItem.ExternalId}; {SafeDiagnostic(rawFailure)}");
                    HandleFailure(
                        failedItem,
                        FriendlyFailure(rawFailure));
                    break;
            }
        }
        catch (JsonException exception)
        {
            DiagnosticLog.Warning(
                "tidal-player",
                $"Odrzucono nieprawidłowy komunikat odtwarzacza: {exception.GetType().Name}.");
        }
    }

    private void ApplyProgress(JsonElement root)
    {
        var item = CurrentItemMatching(Text(root, "productId"));
        if (item is null) return;
        var positionSeconds = Number(root, "position");
        var durationSeconds = Number(root, "duration");
        lock (stateGate)
        {
            if (positionSeconds >= 0) position = TimeSpan.FromSeconds(positionSeconds);
        }
        if (durationSeconds > 0 && Math.Abs(item.Duration.TotalSeconds - durationSeconds) >= 1)
        {
            item.Duration = TimeSpan.FromSeconds(durationSeconds);
            DurationAvailable?.Invoke(
                this,
                new MediaDurationAvailableEventArgs(
                    item,
                    item.Duration,
                    item.SampleRateHz ?? 0));
        }
    }

    private void ApplyTransition(JsonElement root)
    {
        var item = CurrentItemMatching(Text(root, "productId"));
        if (item is null) return;

        var durationSeconds = Number(root, "duration");
        var positionSeconds = Number(root, "position");
        var sampleRate = (int)Number(root, "sampleRate");
        var bandwidth = Number(root, "bandwidth");
        if (durationSeconds > 0) item.Duration = TimeSpan.FromSeconds(durationSeconds);
        if (sampleRate > 0) item.SampleRateHz = sampleRate;
        var codec = Text(root, "codec");
        if (!string.IsNullOrWhiteSpace(codec)) item.Codec = codec.ToUpperInvariant();
        if (bandwidth > 0) item.BitrateKbps = (int)Math.Round(bandwidth / 1000d);
        lock (stateGate)
        {
            if (positionSeconds >= 0) position = TimeSpan.FromSeconds(positionSeconds);
        }

        DurationAvailable?.Invoke(
            this,
            new MediaDurationAvailableEventArgs(
                item,
                item.Duration,
                item.SampleRateHz ?? 0));

        var presentation = Text(root, "assetPresentation");
        isPreview = string.Equals(presentation, "PREVIEW", StringComparison.OrdinalIgnoreCase);
        // The reason comes from the SDK/server. Keep an allowlist: diagnostics
        // must not accept arbitrary URLs, tokens or control characters here.
        var previewReason = NormalizePreviewReason(Text(root, "previewReason"));
        Diagnostics.Transition(presentation, previewReason, durationSeconds);
        DiagnosticLog.Info("tidal-player-access",
            $"Próba: {playbackRequestVersion}; materiał: {(isPreview ? "PREVIEW" : presentation == "FULL" ? "FULL" : "UNKNOWN")}; powód próbki: {previewReason}; czas: {Math.Clamp(durationSeconds, 0, 604800):F3} s.");
        if (string.Equals(presentation, "PREVIEW", StringComparison.OrdinalIgnoreCase)
            && announcedPreviews.Add(item.Id))
        {
            PlaybackNotice?.Invoke(
                this,
                new TidalPlaybackNoticeEventArgs(
                    PreviewNotice(previewReason)));
        }
    }

    private void ApplyPlaybackState(string state, string productId)
    {
        if (CurrentItemMatching(productId) is null) return;
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
            Diagnostics.PlaybackStarted();
            if (preparationTimestamp != 0)
                DiagnosticLog.Info("tidal-player-timing",
                    $"Potwierdzony start; próba: {playbackRequestVersion}; od polecenia AMC: {Stopwatch.GetElapsedTime(preparationTimestamp).TotalMilliseconds:F0} ms.");
            PlaybackStarted?.Invoke(this, new MediaPlaybackStartedEventArgs(item));
        }
    }

    private void ApplyEnded(JsonElement root)
    {
        if (CurrentItemMatching(Text(root, "productId")) is null) return;
        MediaItem? item;
        lock (stateGate)
        {
            // The official SDK also emits ended(reason=error/skip).
            if (!playbackStarted || isPreparing
                || Text(root, "reason") != "completed") return;
            item = currentItem;
            isPreparing = false;
            wasPlaying = false;
            playbackStarted = false;
        }
        if (isPreview)
        {
            HandleFailure(item, "Koniec próbki TIDAL. Pełny utwór nie został udostępniony tej aplikacji; kolejka pozostaje bez zmian.");
            return;
        }
        lock (stateGate) loadedItemId = null;
        if (item is not null)
        {
            DiagnosticLog.Info("tidal-player", $"Potwierdzony koniec utworu: {item.ExternalId}.");
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
                && CurrentItemMatching(Text(root, "productId")) is not null;
    }

    private MediaItem? CurrentItem()
    {
        lock (stateGate) return currentItem;
    }

    private MediaItem? CurrentItemMatching(string productId)
    {
        lock (stateGate)
        {
            if (currentItem is null
                || loadedItemId is null
                || !string.Equals(loadedItemId, currentItem.Id, StringComparison.Ordinal))
            {
                return null;
            }

            return !string.IsNullOrWhiteSpace(productId)
                && string.Equals(PlaybackProductId(currentItem), productId, StringComparison.Ordinal)
                    ? currentItem
                    : null;
        }
    }

    private static string PlaybackProductId(MediaItem item)
    {
        var externalId = item.ExternalId?.Trim() ?? string.Empty;
        var separator = externalId.IndexOf(':');
        if (separator <= 0 || separator >= externalId.Length - 1) return externalId;
        var type = externalId[..separator];
        return type is "tracks" or "videos"
            ? externalId[(separator + 1)..]
            : externalId;
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
            "tidal-player",
            $"Odtwarzanie nie powiodło się; element: {item?.ExternalId ?? "brak"}; {message}");
        Diagnostics.Failure(message);
        RaiseOnUi(() => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));
    }

    private void RaiseOnUi(Action action)
    {
        if (webView.Dispatcher.CheckAccess()) action();
        else _ = webView.Dispatcher.InvokeAsync(action, DispatcherPriority.Send);
    }

    private static double NormalizeVolume(int volume) => Math.Clamp(volume, 0, 100) / 100d;

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

    internal static string NormalizePreviewReason(string reason) => reason switch
    {
        "FULL_REQUIRES_HIGHER_ACCESS_TIER" or "FULL_REQUIRES_PURCHASE" or
            "FULL_REQUIRES_SUBSCRIPTION" => reason,
        "" => "NOT_PROVIDED",
        _ => "UNKNOWN"
    };

    internal static string PreviewNotice(string reason) => reason switch
    {
        "FULL_REQUIRES_HIGHER_ACCESS_TIER" =>
            "Próbka utworu. TIDAL wymaga wyższego poziomu dostępu aplikacji do pełnego odtwarzania.",
        "FULL_REQUIRES_SUBSCRIPTION" =>
            "Próbka utworu. TIDAL wymaga odpowiedniej subskrypcji do pełnego odtwarzania tego materiału.",
        "FULL_REQUIRES_PURCHASE" =>
            "Próbka utworu. TIDAL wymaga zakupu tego materiału do pełnego odtwarzania.",
        _ => "Próbka utworu. TIDAL nie podał rozpoznanego powodu ograniczenia pełnego odtwarzania."
    };

    internal static string FriendlyFailure(string rawMessage)
    {
        if (rawMessage.Contains("NotAllowedError", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("user didn't interact", StringComparison.OrdinalIgnoreCase))
        {
            return "Wbudowany odtwarzacz zablokował rozpoczęcie dźwięku. To problem integracji AMC, nie logowania TIDAL. Kolejka pozostaje bez zmian.";
        }
        if (rawMessage.Contains("FULL_REQUIRES_HIGHER_ACCESS_TIER", StringComparison.OrdinalIgnoreCase))
        {
            return "TIDAL udostępnia tej aplikacji tylko próbkę. Pełne odtwarzanie wymaga wyższego poziomu dostępu przyznanego aplikacji przez TIDAL.";
        }
        if (rawMessage.Contains("missing required scope", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("playback", StringComparison.OrdinalIgnoreCase)
               && rawMessage.Contains("scope", StringComparison.OrdinalIgnoreCase))
        {
            return "Aplikacja TIDAL nie ma uprawnienia do pełnego odtwarzania. Samo ponowne logowanie nie pomoże, jeśli odpowiedni zakres nie jest dostępny w portalu TIDAL.";
        }
        if (rawMessage.Contains("WebView2", StringComparison.OrdinalIgnoreCase))
        {
            return "Nie można uruchomić odtwarzacza TIDAL. Zainstaluj środowisko Microsoft Edge WebView2 Runtime i uruchom AMC ponownie.";
        }
        if (rawMessage.Contains("403", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("CORS", StringComparison.OrdinalIgnoreCase))
        {
            return "TIDAL odmówił pobrania chronionego materiału dla tej aplikacji.";
        }
        if (rawMessage.Contains("PEContentNotAvailableForSubscription", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("FULL_REQUIRES_SUBSCRIPTION", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("FULL_REQUIRES_PURCHASE", StringComparison.OrdinalIgnoreCase))
        {
            return "Ten materiał nie jest dostępny w bieżącym planie TIDAL.";
        }
        if (rawMessage.Contains("PEContentNotAvailableInLocation", StringComparison.OrdinalIgnoreCase))
        {
            return "Ten materiał nie jest dostępny w bieżącym regionie TIDAL.";
        }
        if (rawMessage.Contains("PEMonthlyStreamQuotaExceeded", StringComparison.OrdinalIgnoreCase))
        {
            return "TIDAL zgłosił przekroczenie miesięcznego limitu odtwarzania.";
        }
        if (rawMessage.Contains("PENetwork", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("PERetryable", StringComparison.OrdinalIgnoreCase))
        {
            return "TIDAL zgłosił przejściowy błąd połączenia. Spróbuj ponownie.";
        }
        if (rawMessage.Contains("401", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("invalid_token", StringComparison.OrdinalIgnoreCase)
            || rawMessage.Contains("token expired", StringComparison.OrdinalIgnoreCase))
        {
            return "Logowanie TIDAL wygasło. Otwórz Ctrl+F5 i wybierz Synchronizuj teraz; ponowne logowanie będzie potrzebne tylko wtedy, gdy odświeżenie zostanie odrzucone.";
        }
        if (rawMessage.StartsWith("Najpierw ", StringComparison.OrdinalIgnoreCase)
            || rawMessage.StartsWith("Identyfikator aplikacji", StringComparison.OrdinalIgnoreCase)
            || rawMessage.StartsWith("W instalacji AMC", StringComparison.OrdinalIgnoreCase)
            || rawMessage.StartsWith("Nie udało się uruchomić silnika", StringComparison.OrdinalIgnoreCase))
        {
            return rawMessage.Trim();
        }
        return "Oficjalny odtwarzacz TIDAL zgłosił błąd tego materiału. Spróbuj ponownie albo wybierz inny utwór.";
    }

    internal static string SafeDiagnostic(string rawMessage)
    {
        var safe = Regex.Replace(rawMessage, @"https?://[^\s""<>]+", "[adres pominięty]", RegexOptions.IgnoreCase);
        safe = Regex.Replace(safe, @"\bBearer\s+\S+", "Bearer [pominięto]", RegexOptions.IgnoreCase);
        safe = Regex.Replace(safe, @"\b(?:access_token|refresh_token|token|client_secret)\b[\s""':=]+[^\s,}""']+", "[poświadczenie pominięte]", RegexOptions.IgnoreCase);
        safe = Regex.Replace(safe, @"\beyJ[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+){1,2}", "[token pominięty]");
        var singleLine = safe
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return singleLine.Length <= 500 ? singleLine : singleLine[..500];
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        bridgeReady.TrySetCanceled();
        if (webView.CoreWebView2 is { } core)
        {
            core.WebMessageReceived -= Core_WebMessageReceived;
            core.ProcessFailed -= Core_ProcessFailed;
            core.NavigationCompleted -= Core_NavigationCompleted;
        }
        webView.Dispose();
        lifetime.Dispose();
    }
}
