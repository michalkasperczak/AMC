using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;
using NAudio.CoreAudioApi;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Official Spotify Web Playback SDK in an isolated, headless system Chrome.
/// The catalogue service owns token refresh; the browser only receives short
/// lived access tokens through a nonce protected loopback page.
/// </summary>
internal sealed class LiteSpotifyChromeOutput : ILiteSpotifyOutput
{
    private const string SdkOrigin = "https://sdk.scdn.co";
    private readonly Func<CancellationToken, Task<LiteSpotifyAccessToken>> credentials;
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly object stateGate = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CancellationTokenSource lifetime = new();
    private TaskCompletionSource<string> ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TcpListener? listener;
    private Task? serverTask;
    private Process? chrome;
    private LiteChromeDevTools? page;
    private string? origin;
    private string? deviceId;
    private string? selectedOutputId;
    private string? fallbackOutputName;
    private string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private MediaItem? currentItem;
    private TimeSpan position;
    private long positionAt;
    private long generation;
    private int volume = 35;
    private double rate = 1d;
    private bool playing;
    private bool paused;
    private bool started;
    private bool disposed;
    private TaskCompletionSource<bool>? resumeSignal;

    internal LiteSpotifyChromeOutput(
        Func<CancellationToken, Task<LiteSpotifyAccessToken>> credentials,
        string? selectedOutputId)
    {
        this.credentials = credentials;
        this.selectedOutputId = selectedOutputId;
    }

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;

    public TimeSpan Position
    {
        get
        {
            lock (stateGate)
            {
                if (!playing || paused) return position;
                var elapsed = Stopwatch.GetElapsedTime(positionAt);
                return position + TimeSpan.FromTicks((long)(elapsed.Ticks * rate));
            }
        }
    }

    public string? TakeDefaultOutputFallbackNotice()
    {
        lock (stateGate)
        {
            var notice = fallbackOutputName;
            fallbackOutputName = null;
            return notice;
        }
    }

    public void Play(MediaItem item, TimeSpan start, int nextVolume, double nextRate)
    {
        var rejection = SpotifyPlaybackItemContract.Rejection(item);
        if (rejection is not null)
        {
            PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, rejection));
            return;
        }
        long playGeneration;
        lock (stateGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            resumeSignal?.TrySetResult(true);
            resumeSignal = null;
            currentItem = item;
            position = start < TimeSpan.Zero ? TimeSpan.Zero : start;
            positionAt = Stopwatch.GetTimestamp();
            volume = nextVolume;
            rate = nextRate;
            playing = false;
            paused = false;
            started = false;
            playGeneration = ++generation;
        }
        PlaybackPreparing?.Invoke(this, new MediaPlaybackPreparingEventArgs(item, false));
        _ = RunPlayAsync(item, start, nextVolume, nextRate, playGeneration);
    }

    private async Task RunPlayAsync(
        MediaItem item, TimeSpan start, int nextVolume, double nextRate, long playGeneration)
    {
        var stage = "Chrome startup";
        try
        {
            var ct = lifetime.Token;
            await EnsureReadyAsync(ct).ConfigureAwait(false);
            if (!IsCurrent(item, playGeneration)) return;
            stage = "SDK volume";
            await SetVolumeInPlayerAsync(nextVolume, ct).ConfigureAwait(false);
            if (nextRate != 1d)
            {
                stage = "playback rate";
                await SetMediaRateAsync(nextRate, ct).ConfigureAwait(false);
            }
            stage = "audio output";
            await ApplyOutputAsync(required: !string.IsNullOrWhiteSpace(selectedOutputId), ct)
                .ConfigureAwait(false);
            stage = "Spotify credentials";
            var token = await credentials(ct).ConfigureAwait(false);
            if (!token.AllowsPlayback)
            {
                Fail(item, playGeneration,
                    "Zaloguj ponownie Spotify na koncie Premium z uprawnieniem do odtwarzania.");
                return;
            }
            if (!token.AllowsRemoteControl)
            {
                Fail(item, playGeneration,
                    "Zaloguj ponownie Spotify z uprawnieniem do sterowania odtwarzaniem.");
                return;
            }
            if (!IsCurrent(item, playGeneration)) return;
            await WaitForResumeAsync(ct).ConfigureAwait(false);
            if (!IsCurrent(item, playGeneration)) return;
            var uri = SpotifyPlaybackItemContract.PlaybackUri(item);
            var endpoint = $"https://api.spotify.com/v1/me/player/play?device_id={Uri.EscapeDataString(deviceId!)}";
            using var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                uris = new[] { uri },
                position_ms = (long)Math.Max(0, start.TotalMilliseconds)
            }), Encoding.UTF8, "application/json");
            stage = "Spotify play request";
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new IOException($"Spotify odmówił odtwarzania (HTTP {(int)response.StatusCode}).");
            bool pauseAfterStart;
            lock (stateGate) pauseAfterStart = paused;
            if (pauseAfterStart && IsCurrent(item, playGeneration))
                await CommandAsync("pause", null).ConfigureAwait(false);
            _ = WatchStartAsync(item, playGeneration);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[spotify-chrome] {stage}: {exception.GetType().Name}"
                + (stage == "audio output" ? $" ({exception.Message})" : ""));
            Fail(item, playGeneration,
                "Nie udało się uruchomić odtwarzacza Spotify w Chrome. Sprawdź konto Premium, Chrome i połączenie.");
        }
    }

    private async Task WatchStartAsync(MediaItem item, long playGeneration)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), lifetime.Token).ConfigureAwait(false);
            lock (stateGate)
            {
                if (generation != playGeneration || started || paused
                    || !ReferenceEquals(currentItem, item)) return;
            }
            Fail(item, playGeneration, "Spotify nie potwierdził rozpoczęcia odtwarzania.");
        }
        catch (OperationCanceledException) { }
    }

    private async Task WaitForResumeAsync(CancellationToken ct)
    {
        Task? wait;
        lock (stateGate)
        {
            if (!paused) return;
            resumeSignal ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            wait = resumeSignal.Task;
        }
        await wait.WaitAsync(ct).ConfigureAwait(false);
    }

    public void Pause()
    {
        bool send;
        lock (stateGate)
        {
            position = Position;
            paused = true;
            playing = false;
            send = started;
            if (!started)
                resumeSignal ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (send) _ = CommandAsync("pause", null);
    }

    public void Resume()
    {
        bool send;
        lock (stateGate)
        {
            positionAt = Stopwatch.GetTimestamp();
            paused = false;
            playing = started;
            send = started;
            resumeSignal?.TrySetResult(true);
            resumeSignal = null;
        }
        if (send) _ = CommandAsync("resume", null);
    }

    public void Stop()
    {
        lock (stateGate)
        {
            ++generation;
            resumeSignal?.TrySetResult(true);
            resumeSignal = null;
            currentItem = null;
            position = TimeSpan.Zero;
            playing = false;
            paused = false;
            started = false;
        }
        _ = CommandAsync("pause", null);
    }

    public void Seek(TimeSpan next)
    {
        lock (stateGate)
        {
            position = next < TimeSpan.Zero ? TimeSpan.Zero : next;
            positionAt = Stopwatch.GetTimestamp();
        }
        _ = CommandAsync("seek", null, (long)Math.Max(0, next.TotalMilliseconds));
    }

    public void SetVolume(int next)
    {
        lock (stateGate) volume = Math.Clamp(next, 0, 100);
        _ = SetVolumeInPlayerAsync(next, lifetime.Token);
    }

    public void SetPlaybackRate(double next)
    {
        lock (stateGate)
        {
            position = Position;
            positionAt = Stopwatch.GetTimestamp();
            rate = Math.Clamp(next, 0.5d, 2d);
        }
        _ = SetMediaRateAsync(next, lifetime.Token);
    }

    private async Task CommandAsync(string command, Action? update, long argument = 0)
    {
        try
        {
            var client = page;
            if (client is null) return;
            var expression = command == "seek"
                ? $"window.amcSpotifyPlayer.seek({argument})"
                : $"window.amcSpotifyPlayer.{command}()";
            var result = await client.EvaluateAsync(expression, lifetime.Token).ConfigureAwait(false);
            if (result.ValueKind == JsonValueKind.False)
                throw new IOException("Spotify nie przyjął polecenia odtwarzania.");
            update?.Invoke();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch
        {
            MediaItem? item;
            long currentGeneration;
            lock (stateGate) { item = currentItem; currentGeneration = generation; }
            if (item is not null)
                Fail(item, currentGeneration, "Spotify nie wykonał polecenia odtwarzania.");
        }
    }

    private async Task SetVolumeInPlayerAsync(int value, CancellationToken ct)
    {
        var client = page;
        if (client is null) return;
        await client.EvaluateAsync(
            $"window.amcSpotifyPlayer.setVolume({(Math.Clamp(value, 0, 100) / 100d).ToString(System.Globalization.CultureInfo.InvariantCulture)})",
            ct).ConfigureAwait(false);
    }

    private async Task SetMediaRateAsync(double value, CancellationToken ct)
    {
        var script = "async function(rate){for(const e of this){if(e instanceof HTMLMediaElement && !Object.prototype.hasOwnProperty.call(e,'constructor'))e.playbackRate=rate;}return true;}";
        await ForMediaElementsAsync(script, Math.Clamp(value, 0.5d, 2d), ct)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<LiteSpotifyChromeDevice>> GetOutputDevicesAsync()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new LiteSpotifyChromeDevice(d.ID, d.FriendlyName))
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .ToArray();
        return Task.FromResult<IReadOnlyList<LiteSpotifyChromeDevice>>(devices);
    }

    internal async Task<string?> PrepareConfiguredOutputAsync()
    {
        var wanted = selectedOutputId;
        if (string.IsNullOrWhiteSpace(wanted)) return null;
        var devices = await GetOutputDevicesAsync().ConfigureAwait(false);
        return ResolveDevice(devices, wanted) is null ? wanted : null;
    }

    public async Task<bool> TrySetOutputDeviceAsync(string? wanted)
    {
        var devices = await GetOutputDevicesAsync().ConfigureAwait(false);
        var resolved = string.IsNullOrWhiteSpace(wanted) ? null : ResolveDevice(devices, wanted);
        if (!string.IsNullOrWhiteSpace(wanted) && resolved is null) return false;
        var previous = selectedOutputId;
        selectedOutputId = resolved?.Id;
        try
        {
            if (page is not null)
                await ApplyOutputAsync(required: true, lifetime.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            selectedOutputId = previous;
            return false;
        }
    }

    private static LiteSpotifyChromeDevice? ResolveDevice(
        IReadOnlyList<LiteSpotifyChromeDevice> devices, string wanted) =>
        devices.FirstOrDefault(d => string.Equals(d.Id, wanted, StringComparison.OrdinalIgnoreCase))
        ?? devices.FirstOrDefault(d => string.Equals(d.Name, wanted, StringComparison.OrdinalIgnoreCase));

    private async Task ApplyOutputAsync(bool required, CancellationToken ct)
    {
        var selected = selectedOutputId;
        var devices = await GetOutputDevicesAsync().ConfigureAwait(false);
        var device = string.IsNullOrWhiteSpace(selected) ? null : ResolveDevice(devices, selected);
        if (selected is not null && device is null)
        {
            lock (stateGate) fallbackOutputName = selected;
            if (required) throw new IOException("Wybrane urządzenie Spotify jest niedostępne.");
            selectedOutputId = null;
        }
        var script = """
            async function(name){
              const outputs=(await navigator.mediaDevices.enumerateDevices()).filter(d=>d.kind==='audiooutput');
              const wanted=name ? outputs.find(d=>d.label===name) ||
                outputs.find(d=>!d.label.startsWith('Domyślny') && d.label.startsWith(name)) : null;
              if(name && !wanted)return {ok:false,reason:'device'};
              let count=0;
              for(const e of this){
                if(!(e instanceof HTMLMediaElement) ||
                   Object.prototype.hasOwnProperty.call(e,'constructor'))continue;
                await e.setSinkId(wanted?.deviceId || '');
                if(e.sinkId!==(wanted?.deviceId || ''))return {ok:false,reason:'sink'};
                count++;
              }
              return {ok:count>0,count};
            }
            """;
        var result = await ForMediaElementsAsync(script, device?.Name ?? "", ct)
            .ConfigureAwait(false);
        if (required && (!result.TryGetProperty("ok", out var ok) || !ok.GetBoolean()))
            throw new IOException("Chrome nie potwierdził zmiany wyjścia Spotify.");
    }

    private async Task<JsonElement> ForMediaElementsAsync(
        string function, object argument, CancellationToken ct)
    {
        var iframe = await ConnectSdkFrameAsync(ct).ConfigureAwait(false);
        await using (iframe.ConfigureAwait(false))
        {
            var prototype = await iframe.CallAsync("Runtime.evaluate", new
            {
                expression = "HTMLMediaElement.prototype"
            }, ct).ConfigureAwait(false);
            var prototypeId = prototype.GetProperty("result").GetProperty("objectId").GetString();
            var objects = await iframe.CallAsync("Runtime.queryObjects", new
            {
                prototypeObjectId = prototypeId
            }, ct).ConfigureAwait(false);
            var objectId = objects.GetProperty("objects").GetProperty("objectId").GetString();
            var response = await iframe.CallAsync("Runtime.callFunctionOn", new
            {
                objectId,
                functionDeclaration = function,
                arguments = new[] { new { value = argument } },
                awaitPromise = true,
                returnByValue = true
            }, ct).ConfigureAwait(false);
            if (response.TryGetProperty("exceptionDetails", out var details))
            {
                var reason = details.TryGetProperty("exception", out var exception)
                    && exception.TryGetProperty("description", out var description)
                    ? description.GetString() : details.GetProperty("text").GetString();
                throw new IOException("Chrome nie wykonał polecenia na odtwarzaczu Spotify: "
                    + reason?.Split('\n')[0]);
            }
            return response.GetProperty("result").GetProperty("value").Clone();
        }
    }

    private async Task<LiteChromeDevTools> ConnectSdkFrameAsync(CancellationToken ct)
    {
        var targets = await ReadTargetsAsync(ct).ConfigureAwait(false);
        var frame = targets.FirstOrDefault(t => t.Type == "iframe"
            && t.Url.StartsWith(SdkOrigin, StringComparison.OrdinalIgnoreCase));
        if (frame is null)
            throw new IOException("Nie znaleziono ramki odtwarzacza Spotify.");
        return await LiteChromeDevTools.ConnectAsync(frame.WebSocketUrl, ct)
            .ConfigureAwait(false);
    }

    private sealed record Target(string Type, string Url, string WebSocketUrl);

    private async Task<IReadOnlyList<Target>> ReadTargetsAsync(CancellationToken ct)
    {
        var portFile = Path.Combine(ProfilePath, "DevToolsActivePort");
        var lines = await File.ReadAllLinesAsync(portFile, ct).ConfigureAwait(false);
        if (lines.Length == 0 || !int.TryParse(lines[0], out var port))
            throw new IOException("Chrome nie udostępnił sterowania odtwarzaczem.");
        using var response = await http.GetAsync($"http://127.0.0.1:{port}/json/list", ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)
            .ConfigureAwait(false));
        return document.RootElement.EnumerateArray()
            .Where(t => t.TryGetProperty("webSocketDebuggerUrl", out _))
            .Select(t => new Target(
                t.GetProperty("type").GetString() ?? "",
                t.GetProperty("url").GetString() ?? "",
                t.GetProperty("webSocketDebuggerUrl").GetString() ?? ""))
            .ToArray();
    }

    private static string ProfilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AMC Test", "spotify-chrome-profile");

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (page is not null && chrome?.HasExited == false && deviceId is not null) return;
        await startGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (page is not null && chrome?.HasExited == false && deviceId is not null) return;
            CloseBrowser();
            ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
            nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            var executable = FindChrome();
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            origin = $"http://127.0.0.1:{port}";
            serverTask = Task.Run(() => ServeAsync(listener, port, lifetime.Token), lifetime.Token);
            Directory.CreateDirectory(ProfilePath);
            var portFile = Path.Combine(ProfilePath, "DevToolsActivePort");
            if (File.Exists(portFile)) File.Delete(portFile);
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in new[]
            {
                $"--user-data-dir={ProfilePath}", "--no-first-run", "--no-default-browser-check",
                "--autoplay-policy=no-user-gesture-required", "--disable-background-media-suspend",
                "--remote-debugging-port=0", "--headless=new", origin + "/"
            }) start.ArgumentList.Add(argument);
            chrome = Process.Start(start) ?? throw new IOException("Nie można uruchomić Chrome.");
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(ct);
            startup.CancelAfter(TimeSpan.FromSeconds(45));
            deviceId = await ready.Task.WaitAsync(startup.Token).ConfigureAwait(false);
            while (!File.Exists(portFile))
                await Task.Delay(100, startup.Token).ConfigureAwait(false);
            var targets = await ReadTargetsAsync(startup.Token).ConfigureAwait(false);
            var main = targets.FirstOrDefault(t => t.Type == "page"
                && t.Url.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
                ?? throw new IOException("Chrome nie otworzył strony odtwarzacza.");
            page = await LiteChromeDevTools.ConnectAsync(main.WebSocketUrl, startup.Token)
                .ConfigureAwait(false);
            await page.CallAsync("Browser.setPermission", new
            {
                permission = new { name = "speaker-selection" },
                setting = "granted",
                origin,
                embeddedOrigin = SdkOrigin
            }, startup.Token).ConfigureAwait(false);
        }
        catch
        {
            CloseBrowser();
            throw;
        }
        finally { startGate.Release(); }
    }

    private static string FindChrome()
    {
        foreach (var path in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe")
        })
            if (File.Exists(path)) return path;
        throw new IOException("Zainstaluj Google Chrome, aby odtwarzać Spotify w AMC Test.");
    }

    private async Task ServeAsync(TcpListener server, int port, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient connection;
            try { connection = await server.AcceptTcpClientAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            _ = Task.Run(() => HandleRequestAsync(connection, port, ct), ct);
        }
    }

    private async Task HandleRequestAsync(TcpClient connection, int port, CancellationToken ct)
    {
        using (connection)
        {
            try
            {
                using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (request is null || request.Length > 4096 || !request.StartsWith("GET ", StringComparison.Ordinal))
                    return;
                var host = "";
                for (var i = 0; i < 64; i++)
                {
                    var header = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                    if (header is null || header.Length > 4096) return;
                    if (header.Length == 0) break;
                    if (header.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                        host = header[5..].Trim();
                }
                if (host != $"127.0.0.1:{port}") return;
                var path = request.Split(' ', 3)[1];
                var type = "text/plain; charset=utf-8";
                string body;
                if (path == "/")
                {
                    type = "text/html; charset=utf-8";
                    body = PageHtml();
                }
                else if (path == $"/token/{nonce}")
                {
                    var token = await credentials(ct).ConfigureAwait(false);
                    body = token.AccessToken;
                }
                else if (path.StartsWith($"/event/{nonce}?", StringComparison.Ordinal))
                {
                    HandleEvent(path[(path.IndexOf('?') + 1)..]);
                    body = "ok";
                }
                else { body = ""; }
                var bytes = Encoding.UTF8.GetBytes(body);
                var headerBytes = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: {type}\r\nContent-Length: {bytes.Length}\r\n" +
                    "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headerBytes, ct).ConfigureAwait(false);
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
            catch { /* A browser retry or closed page must not exit the host. */ }
        }
    }

    private string PageHtml() => $$"""
        <!doctype html><html><head><meta charset="utf-8"><title>AMC Spotify</title>
        <script>
        const originalCreate=document.createElement.bind(document);
        document.createElement=function(tag,...args){
          const element=originalCreate(tag,...args);
          if(String(tag).toLowerCase()==='iframe'){
            const originalSet=element.setAttribute.bind(element);
            const descriptor=Object.getOwnPropertyDescriptor(HTMLIFrameElement.prototype,'allow');
            Object.defineProperty(element,'allow',{
              get(){return descriptor.get.call(this)},
              set(value){descriptor.set.call(this,value+'; speaker-selection')}
            });
            element.setAttribute=(name,value)=>originalSet(name,
              name==='allow'?value+'; speaker-selection':value);
            originalSet('allow','autoplay; encrypted-media; speaker-selection');
          }
          return element;
        };
        const notify=(data)=>fetch('/event/{{nonce}}?'+new URLSearchParams(data)).catch(()=>{});
        window.onSpotifyWebPlaybackSDKReady=()=>{
          const player=new Spotify.Player({
            name:'AMC Test',
            getOAuthToken:callback=>fetch('/token/{{nonce}}').then(r=>r.text()).then(callback),
            volume:0.35
          });
          window.amcSpotifyPlayer=player;
          player.addListener('ready',({device_id})=>notify({type:'ready',device:device_id}));
          player.addListener('not_ready',()=>notify({type:'not_ready'}));
          for(const type of ['initialization_error','authentication_error','account_error','playback_error'])
            player.addListener(type,({message})=>notify({type:'error'}));
          player.addListener('player_state_changed',state=>{
            if(state)notify({type:'state',paused:state.paused,pos:state.position,
              duration:state.duration,uri:state.track_window?.current_track?.uri||''});
            else notify({type:'empty'});
          });
          player.connect().then(ok=>{if(!ok)notify({type:'error'})});
        };
        </script><script src="https://sdk.scdn.co/spotify-player.js"></script>
        </head><body></body></html>
        """;

    private void HandleEvent(string query)
    {
        var values = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(part => Uri.UnescapeDataString(part[0]),
                part => Uri.UnescapeDataString(part[1]), StringComparer.Ordinal);
        var type = values.GetValueOrDefault("type");
        if (type == "ready")
        {
            var id = values.GetValueOrDefault("device");
            if (!string.IsNullOrWhiteSpace(id)) ready.TrySetResult(id);
            return;
        }
        if (type == "error" || type == "not_ready")
        {
            MediaItem? failed;
            long currentGeneration;
            lock (stateGate) { failed = currentItem; currentGeneration = generation; }
            if (failed is not null)
                Fail(failed, currentGeneration, "Odtwarzacz Spotify w Chrome stracił połączenie.");
            else ready.TrySetException(new IOException("Spotify nie uruchomił odtwarzacza w Chrome."));
            return;
        }
        if (type != "state") return;
        MediaItem? item;
        bool announceStart = false;
        bool announceEnd = false;
        TimeSpan duration = TimeSpan.Zero;
        lock (stateGate)
        {
            item = currentItem;
            if (item is null) return;
            var uri = values.GetValueOrDefault("uri");
            if (!string.Equals(uri, SpotifyPlaybackItemContract.PlaybackUri(item),
                StringComparison.OrdinalIgnoreCase))
            {
                if (started)
                {
                    ++generation;
                    currentItem = null;
                    playing = false;
                    announceEnd = true;
                }
            }
            else
            {
                if (long.TryParse(values.GetValueOrDefault("pos"), out var milliseconds))
                    position = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
                if (long.TryParse(values.GetValueOrDefault("duration"), out var length))
                {
                    duration = TimeSpan.FromMilliseconds(Math.Max(0, length));
                    item.Duration = duration;
                }
                positionAt = Stopwatch.GetTimestamp();
                paused = values.GetValueOrDefault("paused") == "true";
                playing = !paused;
                if (playing && !started) { started = true; announceStart = true; }
            }
        }
        if (announceStart)
        {
            PlaybackStarted?.Invoke(this, new MediaPlaybackStartedEventArgs(item!));
            _ = ReapplyOutputAsync();
        }
        if (duration > TimeSpan.Zero)
            DurationAvailable?.Invoke(this, new MediaDurationAvailableEventArgs(item!, duration, 0));
        if (announceEnd)
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(item!));
    }

    private async Task ReapplyOutputAsync()
    {
        try
        {
            if (selectedOutputId is not null)
                await ApplyOutputAsync(required: true, lifetime.Token).ConfigureAwait(false);
            if (rate != 1d)
                await SetMediaRateAsync(rate, lifetime.Token).ConfigureAwait(false);
        }
        catch
        {
            MediaItem? item;
            long currentGeneration;
            lock (stateGate) { item = currentItem; currentGeneration = generation; }
            if (item is not null)
                Fail(item, currentGeneration, "Spotify nie przywrócił wybranego wyjścia dźwięku.");
        }
    }

    private bool IsCurrent(MediaItem item, long playGeneration)
    {
        lock (stateGate) return generation == playGeneration && ReferenceEquals(currentItem, item);
    }

    private void Fail(MediaItem item, long playGeneration, string message)
    {
        lock (stateGate)
        {
            if (generation != playGeneration || !ReferenceEquals(currentItem, item)) return;
            ++generation;
            playing = false;
            paused = false;
        }
        PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message));
    }

    private void CloseBrowser()
    {
        var client = page;
        page = null;
        if (client is not null) _ = client.DisposeAsync();
        listener?.Stop();
        listener = null;
        if (chrome is { HasExited: false } owned)
        {
            try { owned.Kill(entireProcessTree: true); owned.WaitForExit(5000); }
            catch { /* Only this process, started by AMC, is ever targeted. */ }
        }
        chrome?.Dispose();
        chrome = null;
        deviceId = null;
    }

    public void Dispose()
    {
        lock (stateGate)
        {
            if (disposed) return;
            disposed = true;
            ++generation;
        }
        lifetime.Cancel();
        CloseBrowser();
        lifetime.Dispose();
        http.Dispose();
        startGate.Dispose();
    }
}

internal sealed record LiteSpotifyChromeDevice(string Id, string Name);
