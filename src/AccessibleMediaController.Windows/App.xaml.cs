using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\AccessibleMultimediaController.SingleInstance";
    private const string ActivationEventName = @"Local\AccessibleMultimediaController.Activate";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private DispatcherTimer? _uiHeartbeatTimer;
    private Timer? _uiWatchdogTimer;
    private readonly CancellationTokenSource _componentUpdateCancellation = new();
    private long _lastUiHeartbeat;
    private int _uiHangReported;

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

        var localDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AccessibleMediaController");
        DiagnosticLog.Initialize(Path.Combine(localDataDirectory, "logs"));
        _lastUiHeartbeat = Stopwatch.GetTimestamp();
        _uiHeartbeatTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiHeartbeatTimer.Tick += (_, _) =>
        {
            Interlocked.Exchange(ref _lastUiHeartbeat, Stopwatch.GetTimestamp());
            if (Interlocked.Exchange(ref _uiHangReported, 0) != 0)
            {
                DiagnosticLog.Info("ui-watchdog", "Interfejs ponownie odpowiada.");
            }
        };
        _uiHeartbeatTimer.Start();
        _uiWatchdogTimer = new Timer(
            _ =>
            {
                if (Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastUiHeartbeat)) < TimeSpan.FromSeconds(8)) return;
                if (Interlocked.Exchange(ref _uiHangReported, 1) == 0)
                {
                    DiagnosticLog.Warning("ui-watchdog", "Interfejs nie odpowiedział przez co najmniej 8 sekund.");
                }
            },
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3));
        DispatcherUnhandledException += (_, args) =>
            DiagnosticLog.Error("unhandled-ui", "Nieobsłużony wyjątek w interfejsie.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DiagnosticLog.Error(
                "unhandled-process",
                $"Nieobsłużony wyjątek procesu. Zamykanie: {args.IsTerminating}.",
                args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            DiagnosticLog.Error("unobserved-task", "Niezaobserwowany wyjątek zadania.", args.Exception);
            args.SetObserved();
        };

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
        var store = new ConfigurationStore(
            Path.Combine(configurationDirectory, "state.json"),
            Path.Combine(localDataDirectory, "library.db"),
            Path.Combine(localDataDirectory, "podcasts.db"));

        PersistedState state;
        try
        {
            state = store.LoadOrCreate();
            DiagnosticLog.Info(
                "storage",
                $"Załadowano Bibliotekę SQLite: {state.LocalMedia.Items.Count} elementów, {state.LocalMedia.FolderSources.Count} źródła folderowe.");
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("storage", "Nie udało się załadować stanu aplikacji.", exception);
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
        StartComponentUpdates(state);
        DiagnosticLog.Info(
            "startup",
            $"Pokazano główne okno. Widoczne: {mainWindow.IsVisible}; stan: {mainWindow.WindowState}; uchwyt: {new WindowInteropHelper(mainWindow).Handle}.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DiagnosticLog.Info("application", $"Zamykanie AMC, kod {e.ApplicationExitCode}.");
        _componentUpdateCancellation.Cancel();
        _uiHeartbeatTimer?.Stop();
        _uiHeartbeatTimer = null;
        _uiWatchdogTimer?.Dispose();
        _uiWatchdogTimer = null;
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
        _componentUpdateCancellation.Dispose();
        DiagnosticLog.Shutdown();
    }

    private void StartComponentUpdates(PersistedState state)
    {
        if (!state.Settings.Updates.CheckAutomatically) return;
        var status = FfmpegComponentManager.GetStatus();
        if (status.CheckedAtUtc is { } checkedAt
            && checkedAt >= DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(24)))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await FfmpegComponentManager.CheckAndUpdateAsync(
                    state.Settings.Updates.DownloadAutomatically,
                    cancellationToken: _componentUpdateCancellation.Token).ConfigureAwait(false);
                if (result.Success) DiagnosticLog.Info("component-update", result.Message);
                else DiagnosticLog.Warning("component-update", result.Message);
            }
            catch (OperationCanceledException)
            {
                DiagnosticLog.Info("component-update", "Sprawdzanie FFmpeg przerwane przy zamykaniu AMC.");
            }
        });
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
