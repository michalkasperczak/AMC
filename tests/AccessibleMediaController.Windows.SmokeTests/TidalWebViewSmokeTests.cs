using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Windows.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class TidalWebViewSmokeTests
{
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    var before = await ProbeAsync(false);
                    var after = await ProbeAsync(true);
                    Console.WriteLine($"WebView2 bez poprawki: {before}; z poprawką: {after}");
                    if (before != "NotAllowedError" || after != "playing")
                        throw new Exception("Nie potwierdzono regresji autoplay i jej naprawy.");
                }
                catch (Exception exception) { failure = exception; }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("WebView2 smoke timeout.");
        if (failure is not null) throw new Exception("WebView2 smoke failed.", failure);
        Console.WriteLine("OK: rzeczywisty WebView2 odtwarza po poleceniu hosta, bez przenoszenia fokusa i bez konta TIDAL.");
    }

    private static async Task<string> ProbeAsync(bool enableHostPlayback)
    {
        var profile = Directory.CreateTempSubdirectory("amc-webview-autoplay-").FullName;
        var view = new WebView2 { Focusable = false, IsHitTestVisible = false };
        var window = new Window
        {
            Content = view, Width = 1, Height = 1, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            Title = "AMC — test silnika audio"
        };
        try
        {
            window.Show();
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile,
                options: enableHostPlayback ? TidalWebViewPolicy.CreateOptions() : new CoreWebView2EnvironmentOptions());
            await view.EnsureCoreWebView2Async(environment);
            var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            view.CoreWebView2.NavigationCompleted += (_, e) => navigation.TrySetResult(e.IsSuccess);
            // Silent PCM but NOT a muted element: muted autoplay would hide the defect.
            var source = SilentWave();
            var content = Path.Combine(profile, "fixture");
            Directory.CreateDirectory(content);
            await File.WriteAllTextAsync(Path.Combine(content, "index.html"),
                $"<html><body><audio id='audio' src='data:audio/wav;base64,{source}'></audio><script>window.outcome='waiting';window.chrome.webview.addEventListener('message',()=>document.getElementById('audio').play().then(()=>window.outcome='playing').catch(e=>window.outcome=e.name));</script></body></html>");
            view.CoreWebView2.SetVirtualHostNameToFolderMapping("amc-autoplay.test", content, CoreWebView2HostResourceAccessKind.DenyCors);
            view.CoreWebView2.Navigate("https://amc-autoplay.test/index.html");
            if (!await navigation.Task.WaitAsync(TimeSpan.FromSeconds(10))) throw new Exception("Navigation failed.");
            // Match AMC exactly: PostWebMessage, NOT ExecuteScriptAsync(play),
            // which can itself acquire user-activation privileges in WebView2.
            view.CoreWebView2.PostWebMessageAsJson("{}");
            await Task.Delay(250);
            for (var i = 0; i < 100; i++)
            {
                var result = JsonSerializer.Deserialize<string>(await view.ExecuteScriptAsync("window.outcome"));
                if (result is not null && result != "waiting") return result;
                await Task.Delay(100);
            }
            throw new TimeoutException("Brak wyniku play().");
        }
        finally
        {
            view.Dispose(); window.Close();
            // A private temporary profile only; never touch AMC's real profile.
            try { Directory.Delete(profile, recursive: true); }
            catch (IOException) { Console.WriteLine($"Profil testowy oczekuje na zwolnienie przez WebView2: {profile}"); }
            catch (UnauthorizedAccessException) { Console.WriteLine($"Profil testowy pozostawiono: {profile}"); }
        }
    }

    private static string SilentWave()
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8); writer.Write(32036); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(32000); writer.Write(new byte[32000]);
        return Convert.ToBase64String(memory.ToArray());
    }
}
