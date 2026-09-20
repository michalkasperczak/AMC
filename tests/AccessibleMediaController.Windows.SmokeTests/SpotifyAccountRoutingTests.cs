using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

internal static class SpotifyAccountRoutingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        CheckEngine(SpotifyPlaybackEngine.Librespot);
        CheckEngine(SpotifyPlaybackEngine.Sdk);
        Console.WriteLine("OK: konto Spotify otwiera logowanie biblioteki; osobne parowanie wraca do tego samego okna, bez logowania i odtwarzania");
    }

    private static void CheckEngine(SpotifyPlaybackEngine engine)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var root = Path.Combine(Path.GetTempPath(), "amc-account-routing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? main = null;
            DispatcherTimer? timer = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "spotify";
                state.Settings.SpotifyEngine = engine;
                state.Settings.SpotifySessionUnificationVersion = SpotifySessionMigration.Version;
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                main = new MainWindow(state, store);
                main.Show();
                var sessions = (SessionManager)typeof(MainWindow).GetField("_sessions", Private)!.GetValue(main)!;
                sessions.SelectSession("spotify");
                // No catalog credentials are read by the account status in this test.
                var integration = typeof(MainWindow).GetField("_spotifyIntegration", Private)!.GetValue(main)!;
                integration.GetType().GetField("storedLoginProbe", Private)!.SetValue(integration, (bool?)false);
                SpotifyAccountWindow? catalog = null;
                var stage = 0;
                var began = Stopwatch.StartNew();
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        if (began.Elapsed > TimeSpan.FromSeconds(15)) throw new Exception("Nie zakończono nawigacji okien konta.");
                        if (stage == 0)
                        {
                            var first = main.OwnedWindows.OfType<Window>().FirstOrDefault(w => w.IsVisible);
                            if (first is null) return;
                            catalog = first as SpotifyAccountWindow
                                ?? throw new Exception("Ctrl+F5 otwiera parowanie zamiast konta biblioteki Spotify.");
                            if (catalog.FindName("LoginButton") is not Button login || !login.IsVisible)
                                throw new Exception("Pierwsze okno nie udostępnia Zaloguj w przeglądarce.");
                            var pairing = catalog.FindName("PlaybackAccountButton") as Button;
                            if (engine == SpotifyPlaybackEngine.Sdk)
                            {
                                if (pairing?.IsVisible == true) throw new Exception("SDK pokazuje nieużywane parowanie Librespot.");
                                stage = 3;
                                catalog.Close();
                                timer.Stop();
                                return;
                            }
                            if (pairing is null || !pairing.IsVisible)
                                throw new Exception("Brakuje osobnego przycisku parowania odtwarzacza.");
                            stage = 1;
                            main.Dispatcher.BeginInvoke(() => pairing.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                        }
                        else if (stage == 1)
                        {
                            var pairingWindow = catalog!.OwnedWindows.OfType<SpotifyLibrespotAccountWindow>().FirstOrDefault(w => w.IsVisible);
                            if (pairingWindow is null) return;
                            if (!ReferenceEquals(pairingWindow.Owner, catalog)) throw new Exception("Parowanie nie ma konta biblioteki jako właściciela.");
                            if (((TextBox)pairingWindow.FindName("PairingCodeBox")).Text.Length != 0)
                                throw new Exception("Samo wejście do parowania pobiera kod.");
                            stage = 2;
                            ((Button)pairingWindow.FindName("CatalogAccountButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        else if (stage == 2)
                        {
                            if (catalog!.OwnedWindows.OfType<Window>().Any(w => w.IsVisible)) return;
                            if (!catalog.IsVisible || main.OwnedWindows.OfType<SpotifyAccountWindow>().Count() != 1)
                                throw new Exception("Powrót z parowania otworzył duplikat albo zamknął konto biblioteki.");
                            stage = 3;
                            catalog.Close();
                            timer.Stop();
                        }
                    }
                    catch (Exception e)
                    {
                        failure = e;
                        timer.Stop();
                        CloseChildren(main);
                    }
                };
                timer.Start();
                main.ShowSpotifyAccountManager();
                if (failure is not null) throw failure;
                if (stage != 3) throw new Exception("Okno konta zakończyło się bez pełnej próby.");
                if (sessions.Current.Id != "spotify" || sessions.Current.HasCurrentItem)
                    throw new Exception("Obsługa konta zmieniła sesję lub rozpoczęła odtwarzanie.");
            }
            catch (Exception e) { failure = e; }
            finally
            {
                timer?.Stop();
                if (main is not null) { CloseChildren(main); main.Close(); }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(45))) throw new Exception("Próba konta Spotify przekroczyła limit.");
        if (failure is not null) throw new Exception("Prowadzenie konta Spotify (" + engine + "): " + failure.Message, failure);
    }

    internal static void ShowForNvda()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var root = Path.Combine(Path.GetTempPath(), "amc-account-gui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "spotify";
                state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
                state.Settings.SpotifySessionUnificationVersion = SpotifySessionMigration.Version;
                state.Spotify.ClientId = new string('0', 32); // Jawnie próbny identyfikator, nie konto.
                var main = new MainWindow(state, new ConfigurationStore(Path.Combine(root, "state.json")));
                var integration = typeof(MainWindow).GetField("_spotifyIntegration", Private)!.GetValue(main)!;
                integration.GetType().GetField("storedLoginProbe", Private)!.SetValue(integration, (bool?)false);
                new Application().Run(main);
            }
            catch (Exception e) { failure = e; }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private static void CloseChildren(Window parent)
    {
        foreach (var child in parent.OwnedWindows.OfType<Window>().ToArray())
        {
            CloseChildren(child);
            child.Close();
        }
    }
}
