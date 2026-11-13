using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AccessibleMediaController.AppleMusicWebHost;

internal sealed class PlayerWindow : Form
{
    private readonly WebView2 web = new()
    {
        Dock = DockStyle.Fill,
        AccessibleName = "Strona Apple Music do logowania i odtwarzania"
    };
    private readonly Label status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        AccessibleName = "Stan Apple Music",
        Text = "Apple Music jest uruchamiane."
    };
    private readonly TaskCompletionSource<bool> ready = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private string? startupError;

    // The process starts minimized. Only a requested login or "show" can
    // present the page; routine transport never activates or raises it.
    protected override bool ShowWithoutActivation => true;

    public PlayerWindow()
    {
        Text = "AMC Test — logowanie Apple Music";
        Width = 1100;
        Height = 740;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Minimized;
        TopMost = false;
        Controls.Add(web);
        Controls.Add(status);
        Shown += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessibleMediaController", "AppleMusicWebHost-Test");
            Directory.CreateDirectory(folder);
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: folder);
            await web.EnsureCoreWebView2Async(environment);
            web.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                    status.Text = "Nie udało się otworzyć Apple Music.";
            };
            ready.TrySetResult(true);
        }
        catch
        {
            startupError = "Nie można uruchomić osadzonego Apple Music.";
            ready.TrySetResult(false);
        }
    }

    public async Task<object> HandleAsync(Request request)
    {
        if (!await ready.Task.WaitAsync(TimeSpan.FromSeconds(15)) ||
            web.CoreWebView2 is null)
            return Failure(startupError ?? "Nie można uruchomić Apple Music.");
        if (request.Command == "show")
        {
            ShowLoginWindow();
            return new { handled = true, message = "Pokazano okno Apple Music." };
        }
        if (request.Command == "play")
            return await PlayAsync(request.SongId!);
        if (!IsMusicPage())
            return Failure("Najpierw wybierz utwór Apple Music.");
        if (request.Command == "state")
            return await StateAsync();
        var command = request.Command switch
        {
            "toggle" => "if (music.isPlaying) await music.pause(); else await music.play();",
            "stop" => "await music.stop();",
            "next" => "await music.skipToNextItem();",
            "previous" => "await music.skipToPreviousItem();",
            "seek" => "await music.seekToTime(" + JsonSerializer.Serialize(request.Seconds) + ");",
            _ => ""
        };
        if (command.Length == 0)
            return Failure("Nieznane polecenie Apple Music.");
        var successMessage = request.Command switch
        {
            "stop" => "Zatrzymano",
            "next" => "Następny utwór Apple Music",
            "previous" => "Poprzedni utwór Apple Music",
            "seek" => "Przewinięto utwór Apple Music",
            _ => ""
        };
        var result = await ScriptAsync($$"""
            (async () => {
              const music = window.MusicKit?.getInstance?.();
              if (!music) return {handled:false, message:'Odtwarzacz Apple Music nie odpowiada.'};
              try {
                {{command}}
                return {handled:true, isPlaying:!!music.isPlaying,
                  message: {{JsonSerializer.Serialize(successMessage)}} ||
                    (music.isPlaying ? 'Odtwarzanie' : 'Wstrzymano')};
              } catch { return {handled:false, message:'Apple Music nie przyjęło polecenia.'}; }
            })()
            """);
        return result ?? Failure("Odtwarzacz Apple Music nie odpowiada.");
    }

    private async Task<object> PlayAsync(string songId)
    {
        var url = "https://music.apple.com/pl/song/" + songId;
        web.CoreWebView2.Navigate(url);
        string? title = null;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            await Task.Delay(500);
            if (!IsMusicPage()) continue;
            var probe = await ScriptAsync($$"""
                (() => {
                  const id = '{{songId}}';
                  const anchor = [...document.querySelectorAll('a[href]')].find(a => {
                    try { return new URL(a.href).pathname.split('/').at(-1) === id; }
                    catch { return false; }
                  });
                  const login = [...document.querySelectorAll('button')].some(b =>
                    ['Zaloguj się', 'Sign In'].includes((b.textContent || '').trim()));
                  return {found:!!anchor, title:(anchor?.textContent || '').trim().slice(0,200),
                    login};
                })()
                """);
            if (probe is null) continue;
            if (probe.Value.TryGetProperty("login", out var login) &&
                login.ValueKind == JsonValueKind.True)
            {
                status.Text = "Zaloguj się ręcznie po otwarciu okna z menu AMC.";
                return new { handled = false, loginRequired = true,
                    message = "Z menu Odtwarzanie wybierz Pokaż okno odtwarzacza Apple Music, zaloguj się, potem ponownie naciśnij Enter." };
            }
            if (probe.Value.TryGetProperty("found", out var found) &&
                found.ValueKind == JsonValueKind.True)
            {
                title = probe.Value.GetProperty("title").GetString();
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(title))
            return new { handled = false, loginRequired = true,
                message = "Apple Music nie wczytało wskazanego utworu. Z menu Odtwarzanie wybierz Pokaż okno odtwarzacza Apple Music, sprawdź logowanie i spróbuj ponownie." };

        // A row click preserves Apple's album or playlist queue when possible.
        // The site itself owns authorization, DRM and the media URL.
        await TryClickSongAsync(songId);
        var matched = await PlayerMatchesAsync(songId);
        if (!matched)
        {
            // MusicKit is the fixed fallback; no arbitrary script crosses IPC.
            var result = await ScriptAsync($$"""
                (async () => {
                  const music = window.MusicKit?.getInstance?.();
                  if (!music) return {ok:false};
                  try {
                    await music.setQueue({song:'{{songId}}'});
                    await music.play();
                    return {ok:true};
                  } catch { return {ok:false}; }
                })()
                """);
            matched = result?.TryGetProperty("ok", out var ok) == true &&
                ok.ValueKind == JsonValueKind.True &&
                await PlayerMatchesAsync(songId);
        }
        if (!matched)
            return Failure("Apple Music nie przyjęło odtwarzania wskazanego utworu.");
        WindowState = FormWindowState.Minimized;
        status.Text = "Odtwarzanie Apple Music działa w tle.";
        return new { handled = true, title,
            message = "Uruchomiono odtwarzanie wskazanego utworu Apple Music." };
    }

    private async Task TryClickSongAsync(string songId)
    {
        // A trusted browser input event selects the exact song in Apple's own
        // page. No selector or script crosses IPC.
        var point = await ScriptAsync($$"""
            (() => {
              const id = '{{songId}}';
              const anchor = [...document.querySelectorAll('a[href]')].find(a => {
                try { return new URL(a.href).pathname.split('/').at(-1) === id; }
                catch { return false; }
              });
              const row = anchor?.closest('[role="row"]');
              const button = row?.querySelector('button.play-button');
              if (!button) return {ok:false};
              row.scrollIntoView({block:'center'});
              const rect = button.getBoundingClientRect();
              return {ok:rect.width > 0 && rect.height > 0,
                x:rect.left + rect.width / 2, y:rect.top + rect.height / 2};
            })()
            """);
        if (point?.TryGetProperty("ok", out var ok) != true ||
            ok.ValueKind != JsonValueKind.True)
            return;
        var x = point.Value.GetProperty("x").GetDouble();
        var y = point.Value.GetProperty("y").GetDouble();
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 ||
            x >= web.ClientSize.Width || y >= web.ClientSize.Height)
            return;
        try
        {
            foreach (var type in new[] { "mouseMoved", "mousePressed", "mouseReleased" })
            {
                await web.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Input.dispatchMouseEvent",
                    JsonSerializer.Serialize(new {
                        type, x, y,
                        button = type == "mouseMoved" ? "none" : "left",
                        clickCount = 1
                    }));
            }
        }
        catch { /* The caller verifies state and reports a safe failure. */ }
    }

    private async Task<bool> PlayerMatchesAsync(string songId)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            await Task.Delay(350);
            var item = await ScriptAsync("""
                (() => {
                  const music = window.MusicKit?.getInstance?.();
                  return {id:String(music?.nowPlayingItem?.id ||
                    music?.nowPlayingItem?.playParams?.id || ''),
                    playing:!!music?.isPlaying};
                })()
                """);
            if (item is null) continue;
            var id = item.Value.GetProperty("id").GetString();
            if (item.Value.GetProperty("playing").ValueKind == JsonValueKind.True &&
                id == songId)
                return true;
        }
        return false;
    }

    private async Task<object> StateAsync()
    {
        var result = await ScriptAsync("""
            (() => {
              const music = window.MusicKit?.getInstance?.();
              if (!music) return {hasSession:false};
              const item = music.nowPlayingItem;
              const number = value => value == null ? null :
                (Number.isFinite(Number(value)) ? Number(value) : null);
              return {hasSession:!!item, title:String(item?.title || '').slice(0,200),
                artist:String(item?.artistName || '').slice(0,200),
                songId:String(item?.id || item?.playParams?.id || '').slice(0,30),
                isPlaying:!!music.isPlaying,
                positionSeconds:number(music.currentPlaybackTime),
                durationSeconds:number(item?.durationInMillis) === null ? null
                  : number(item.durationInMillis) / 1000};
            })()
            """);
        return result.HasValue ? (object)result.Value : new { hasSession = false };
    }

    private async Task<JsonElement?> ScriptAsync(string script)
    {
        if (!IsMusicPage()) return null;
        try
        {
            var json = await web.CoreWebView2.ExecuteScriptAsync(script)
                .WaitAsync(TimeSpan.FromSeconds(12));
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            return document.RootElement.Clone();
        }
        catch { return null; }
    }

    private bool IsMusicPage() => Uri.TryCreate(web.CoreWebView2?.Source,
        UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        uri.Host.Equals("music.apple.com", StringComparison.OrdinalIgnoreCase);

    private void ShowLoginWindow()
    {
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        status.Text = "Możesz tu zalogować się lub sprawdzić odtwarzanie.";
        Activate();
    }

    private static object Failure(string message) => new { handled = false, message };
}
