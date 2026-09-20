using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class SessionMenuScopeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-menu-scope-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var store = new ConfigurationStore(Path.Combine(root, "state.json"), Path.Combine(root, "library.db"), Path.Combine(root, "podcasts.db"));
                var state = store.LoadOrCreate();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "spotify";
                state.Tidal.ClientId = string.Empty; state.Spotify.ClientId = string.Empty;
                state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                state.Podcasts.Subscriptions.Clear(); state.Podcasts.Episodes.Clear();
                state.LocalMedia.FolderSources.Clear(); state.Radio.Stations.Clear();
                window = new MainWindow(state, store);
                var manager = (SessionManager)typeof(MainWindow).GetField("_sessions", Private)!.GetValue(window)!;
                var stream = Descendants(window.FileMenuItem).Single(m => AutomationProperties.GetName(m).StartsWith("Otwórz strumień", StringComparison.Ordinal));
                var podcasts = window.SpotifyPodcastsMenuItem;
                var sessionIds = manager.Sessions.Select(s => s.Id).ToArray();
                foreach (var id in new[] { "spotify" }.Concat(sessionIds.Where(id => id != "spotify")))
                {
                    manager.SelectSession(id);
                    typeof(MainWindow).GetMethod("UpdateFileMenuForCurrentSession", Private)!.Invoke(window, null);
                    Check(stream.Visibility == (id == "radio" ? Visibility.Visible : Visibility.Collapsed),
                        "Otworz strumien ma byc widoczny tylko w radiu, nie w sesji " + id);
                    Check(podcasts.Visibility == (id == "spotify" ? Visibility.Visible : Visibility.Collapsed),
                        "Podcasty Spotify maja niewlasciwy zakres widocznosci: " + id);
                    var visible = window.FileMenuItem.Items.OfType<FrameworkElement>().Where(x => x.Visibility == Visibility.Visible).ToArray();
                    Check(visible.Length == 0 || visible[0] is not Separator, "Puste poczatkowe oddzielenie menu: " + id);
                    Check(!visible.Zip(visible.Skip(1)).Any(pair => pair.First is Separator && pair.Second is Separator),
                        "Dwa sasiednie oddzielenia menu: " + id);
                }
                Check(podcasts.Parent is MenuItem parent && AutomationProperties.GetName(parent) == "Widok",
                    "Zapisane podcasty Spotify sa nadal w menu Plik zamiast Widok.");
                Check(podcasts.InputGestureText == "Ctrl+Alt+O" && AutomationProperties.GetAcceleratorKey(podcasts) == "Ctrl+Alt+O",
                    "Przeniesienie pozycji zmienilo skrot.");
                Check(!AutomationProperties.GetName(podcasts).Contains("Ctrl", StringComparison.OrdinalIgnoreCase),
                    "Czytnik dostaje skrot powtorzony w nazwie podcastow.");
                Console.WriteLine($"OK: kontekst menu {sessionIds.Length} sesji, strumien tylko radio, podcasty w Widok, skrot zachowany");
            }
            catch (Exception e) { failure = e; }
            finally
            {
                window?.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(40))) throw new Exception("Test menu nie zakonczyl sie w terminie.");
        if (failure is not null) throw new Exception("Zakres menu sesji.", failure);
    }
    private static IEnumerable<MenuItem> Descendants(MenuItem root)
    {
        foreach (var item in root.Items.OfType<MenuItem>())
        {
            yield return item;
            foreach (var child in Descendants(item)) yield return child;
        }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
