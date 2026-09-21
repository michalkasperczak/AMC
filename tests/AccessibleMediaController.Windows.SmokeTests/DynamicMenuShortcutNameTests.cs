using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

/// <summary>
/// Zgloszenie Michala: czytnik ekranu wymawia skrot DWA razy na pozycjach
/// dynamicznego menu ("Playlisty Ctrl+P ... Ctrl+ P"). Normalizacja w
/// konstruktorze czysci nazwy poprawnie, ale odswiezenie menu po zmianie sesji
/// wpisuje skrot z powrotem do AutomationProperties.Name.
///
/// Dlatego ten test NIE bada helpera MenuAccessibility ani zrodel: wola
/// rzeczywiste UpdateFileMenuForCurrentSession oraz PlayerContextMenu_Opened i
/// czyta KONCOWE wlasciwosci obiektow menu - Name razem z AcceleratorKey i
/// InputGestureText z tego samego obiektu. Skrot musi zostac w AcceleratorKey i
/// InputGestureText, a nazwa sesji i tresc etykiety musza przezyc poprawke.
/// </summary>
internal static class DynamicMenuShortcutNameTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-menu-skrot-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var store = new ConfigurationStore(
                    Path.Combine(root, "state.json"),
                    Path.Combine(root, "library.db"),
                    Path.Combine(root, "podcasts.db"));
                var state = store.LoadOrCreate();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "local";
                state.Tidal.ClientId = string.Empty;
                state.Spotify.ClientId = string.Empty;
                state.Tidal.CachedCollectionItems.Clear();
                state.Spotify.CachedCollectionItems.Clear();
                state.Podcasts.Subscriptions.Clear();
                state.Podcasts.Episodes.Clear();
                state.LocalMedia.FolderSources.Clear();
                state.Radio.Stations.Clear();
                window = new MainWindow(state, store) { SuppressDesktopIntegrationForTests = true };
                var manager = (SessionManager)typeof(MainWindow)
                    .GetField("_sessions", Private)!
                    .GetValue(window)!;
                var refresh = typeof(MainWindow).GetMethod("UpdateFileMenuForCurrentSession", Private)!;
                var playerMenuOpened = typeof(MainWindow).GetMethod("PlayerContextMenu_Opened", Private)!;

                var checks = 0;
                var errors = new List<string>();
                void Check(bool value, string message)
                {
                    checks++;
                    if (!value) errors.Add(message);
                }
                void SelectSession(string id)
                {
                    manager.SelectSession(id);
                    if (!string.Equals(manager.Current.Id, id, StringComparison.Ordinal))
                        throw new Exception($"Nie przygotowano sesji testowej {id}.");
                    refresh.Invoke(window, null);
                }
                void CheckMainMenu(DemoMediaSession session)
                {
                    var wiiM = string.Equals(session.Id, "wiim", StringComparison.Ordinal);
                    AssertNoRepeatedShortcut(
                        window.PlaylistsViewMenuItem, "Ctrl+P", "Playlisty", "Playlisty", session, Check);
                    AssertNoRepeatedShortcut(
                        window.RadioPresetsViewMenuItem, "Ctrl+Alt+P",
                        wiiM ? "Presety urządzenia WiiM" : $"Presety, {session.DisplayName}",
                        wiiM ? "Presety urządzenia WiiM…" : "Presety…", session, Check);
                    AssertNoRepeatedShortcut(
                        window.RadioAssignPresetMenuItem, "Ctrl+Alt+Shift+P",
                        wiiM ? "Przypisz skrót AMC do gotowego presetu WiiM" : $"Utwórz lub przypisz preset, {session.DisplayName}",
                        wiiM ? "Przypisz skrót AMC do gotowego presetu WiiM…" : "Utwórz lub przypisz preset…", session, Check);
                }

                if (manager.Sessions.Count == 0)
                    throw new Exception("Brak sesji do testowania nazw menu.");
                foreach (var session in manager.Sessions)
                {
                    SelectSession(session.Id);
                    CheckMainMenu(session);
                }

                // Jawny cykl: WiiM, potem lokalna. Po powrocie zadna z czterech
                // pozycji nie moze zachowac etykiety poprzedniej sesji.
                foreach (var id in new[] { "wiim", "local" })
                {
                    SelectSession(id);
                    CheckMainMenu(manager.Current);
                    playerMenuOpened.Invoke(
                        window,
                        new object[] { window.PlayerPanel.ContextMenu!, new RoutedEventArgs() });
                    var label = id == "wiim"
                        ? "Wybierz aktywne urządzenie WiiM"
                        : $"Wybierz urządzenie audio dla sesji {manager.Current.DisplayName}";
                    AssertNoRepeatedShortcut(
                        window.PlayerAudioOutputDeviceMenuItem, "Shift+A", label, label, manager.Current, Check);
                }

                Console.WriteLine(
                    $"DYNAMIC_MENU_CHECKS sessions={manager.Sessions.Count} checks={checks} failures={errors.Count} transition=wiim->local");
                if (errors.Count != 0)
                    throw new Exception(string.Join(Environment.NewLine, errors));
                Console.WriteLine(
                    $"OK: {checks} sprawdzen koncowych nazw menu w {manager.Sessions.Count} sesjach; "
                        + "skrot tylko w AcceleratorKey i InputGestureText");
            }
            catch (Exception e) { failure = e; }
            finally
            {
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
            throw new Exception("Test nazw dynamicznego menu nie zakonczyl sie w terminie.");
        if (failure is not null)
            throw new Exception("Skrot powtorzony w nazwie dostepnosci dynamicznego menu.", failure);
    }

    private static void AssertNoRepeatedShortcut(
        MenuItem item,
        string shortcut,
        string expectedName,
        string expectedHeader,
        DemoMediaSession session,
        Action<bool, string> Check)
    {
        var name = AutomationProperties.GetName(item);
        var accelerator = AutomationProperties.GetAcceleratorKey(item);
        var gesture = item.InputGestureText;
        var where = $"{item.Name}, sesja {session.Id}";

        Check(
            !name.Contains(shortcut, StringComparison.OrdinalIgnoreCase),
            $"Skrot {shortcut} powtorzony w nazwie dla czytnika ({where}): \"{name}\"");
        Check(
            !ContainsAnyShortcutToken(name),
            $"Nazwa dla czytnika nadal niesie kombinacje klawiszy ({where}): \"{name}\"");
        Check(
            string.Equals(name, expectedName, StringComparison.Ordinal),
            $"Niewlasciwa nazwa ({where}): [{name}], oczekiwano [{expectedName}]");
        Check(
            string.Equals(accelerator, shortcut, StringComparison.Ordinal),
            $"Czytnik stracil skrot w AcceleratorKey ({where}): \"{accelerator}\"");
        Check(
            string.Equals(gesture, shortcut, StringComparison.Ordinal),
            $"Pozycja menu stracila InputGestureText ({where}): \"{gesture}\"");
        Check(
            item.Header is string header
                && string.Equals(header, expectedHeader, StringComparison.Ordinal)
                && !ContainsAnyShortcutToken(header),
            $"Niewlasciwa widoczna etykieta ({where}): [{item.Header}], oczekiwano [{expectedHeader}]");
    }

    private static bool ContainsAnyShortcutToken(string text) =>
        text.Contains("Ctrl+", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Alt+", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Shift+", StringComparison.OrdinalIgnoreCase);

}
