using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

internal static class ApplicationUpdateRoutingTests
{
    internal static void ShowForNvda()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-update-main-gui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var state = new PersistedState();
            state.Tidal.ClientId = string.Empty;
            state.Spotify.ClientId = string.Empty;
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;
            MainWindow? window = null;
            try
            {
                window = new MainWindow(state, new ConfigurationStore(Path.Combine(root, "state.json")));
                window.Title = "AMC — próba F11, pusty profil testowy";
                window.ShowDialog();
                Console.WriteLine("UI_FIXTURE_MAIN_CLOSED");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                Console.WriteLine("UI_FIXTURE_STATE|" + root);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(10))) throw new Exception("Zakończono limit próby F11 z NVDA.");
        if (failure is not null) throw new Exception("Próba F11 z NVDA", failure);
    }

    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-update-routing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            var updateRequests = 0;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.Updates.InstallOnExit = false;
                window = new MainWindow(state, new ConfigurationStore(Path.Combine(root, "state.json")), () => updateRequests++);
                using var inputSource = new HwndSource(new HwndSourceParameters("AMC update routing test")
                { ParentWindow = new IntPtr(-3), WindowStyle = 0, Width = 1, Height = 1 });
                var key = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, Key.F11)
                { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                typeof(MainWindow).GetMethod("Window_PreviewKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, new object[] { window, key });
                if (!key.Handled || updateRequests != 1)
                    throw new Exception("F11 w rzeczywistym oknie nie wywołało sprawdzania aktualizacji.");

                var helpArgs = new object[] { Key.F11, ModifierKeys.None, "" };
                if (!(bool)typeof(MainWindow).GetMethod("TryResolveKeyboardHelpCommand", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, helpArgs)! || (string)helpArgs[2] != "application.checkUpdates")
                    throw new Exception("Pomoc klawiatury nie rozpoznaje F11.");
                if (updateRequests != 1) throw new Exception("Sprawdzanie pomocy uruchomiło aktualizację.");
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new Exception("Test F11 przekroczył czas.");
        if (failure is not null) throw new Exception("Obsługa F11 w głównym oknie", failure);
        Console.WriteLine("OK: F11 w głównym oknie i opis w pomocy, bez sieci ani instalatora");
    }

}
