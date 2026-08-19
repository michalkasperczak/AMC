using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\AccessibleMultimediaController.SingleInstance";
    private const string ActivationEventName = @"Local\AccessibleMultimediaController.Activate";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            AllowExistingInstanceToActivate();
            SignalExistingInstance();
            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut) Dispatcher.BeginInvoke(ActivateCurrentWindow);
            },
            null,
            Timeout.Infinite,
            false);

        var configurationDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AccessibleMediaController");
        var store = new ConfigurationStore(Path.Combine(configurationDirectory, "state.json"));

        PersistedState state;
        try
        {
            state = store.LoadOrCreate();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udało się odczytać konfiguracji. Program uruchomi ustawienia domyślne.\n\n{exception.Message}",
                "Dostępny kontroler multimedialny",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            state = ConfigurationStore.CreateDefaultState();
        }

        var mainWindow = new MainWindow(state, store);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationRegistration?.Unregister(null);
        _activationRegistration = null;
        _activationEvent?.Dispose();
        _activationEvent = null;
        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
        base.OnExit(e);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance may still be between acquiring the mutex and
            // creating the event. A short retry keeps a double-click reliable.
            Thread.Sleep(150);
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
                activationEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The primary process is already starting and will present its
                // main window without creating a second copy of AMC.
            }
        }
    }

    private static void AllowExistingInstanceToActivate()
    {
        var current = Environment.ProcessId;
        foreach (var process in System.Diagnostics.Process.GetProcessesByName(
                     System.Diagnostics.Process.GetCurrentProcess().ProcessName))
        {
            using (process)
            {
                if (process.Id != current) AllowSetForegroundWindow(process.Id);
            }
        }
    }

    private void ActivateCurrentWindow()
    {
        var visibleWindows = Windows
            .OfType<Window>()
            .Where(window => window.IsVisible)
            .ToArray();
        var target = visibleWindows.FirstOrDefault(window => window.IsActive)
            ?? visibleWindows.OrderByDescending(GetOwnerDepth).FirstOrDefault()
            ?? MainWindow;
        if (target is null) return;

        if (target.WindowState == WindowState.Minimized)
        {
            target.WindowState = WindowState.Normal;
        }
        target.Show();
        target.Activate();
        target.Focus();
        var handle = new WindowInteropHelper(target).Handle;
        if (handle != IntPtr.Zero) SetForegroundWindow(handle);
    }

    private static int GetOwnerDepth(Window window)
    {
        var depth = 0;
        for (var owner = window.Owner; owner is not null; owner = owner.Owner) depth++;
        return depth;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
