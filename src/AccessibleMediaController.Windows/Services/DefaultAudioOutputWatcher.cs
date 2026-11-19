using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Śledzi domyślne urządzenie odtwarzania Windows, czyli to samo, które
/// wybiera WasapiOut bez wskazanego urządzenia (renderowanie, rola Console).
/// Odłączenie urządzenia unieważnia otwarte wyjście i AMC odtwarza je
/// ponownie, ale podłączenie słuchawek tylko zmienia urządzenie domyślne:
/// stare wyjście dalej działa i bez tego powiadomienia dźwięk zostaje na
/// głośnikach. Windows wysyła kilka powiadomień naraz (po jednym na rolę,
/// czasem kilka przy łączeniu urządzeń Bluetooth), więc zgłaszamy zmianę
/// dopiero wtedy, gdy przez chwilę nie ma kolejnych powiadomień, i tylko gdy
/// identyfikator naprawdę się różni.
/// </summary>
internal sealed class DefaultAudioOutputWatcher : IDisposable
{
    internal static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CallbackShutdownWait = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly object _callbackGate = new();
    private readonly Action<string> _changed;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly NotificationClient _client;
    private readonly Timer _timer;
    private string? _lastReportedId;
    private bool _disposed;

    private DefaultAudioOutputWatcher(Action<string> changed)
    {
        _changed = changed;
        _timer = new Timer(_ => OnSettled(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _enumerator = new MMDeviceEnumerator();
        _lastReportedId = TryReadDefaultId(_enumerator);
        _client = new NotificationClient(this);
        try
        {
            _enumerator.RegisterEndpointNotificationCallback(_client);
        }
        catch
        {
            _enumerator.Dispose();
            _timer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Uruchamia śledzenie. Gdy Windows go nie udostępnia, AMC działa jak
    /// dotąd: zmiana nastąpi przy następnym otwarciu odtwarzania.
    /// </summary>
    public static DefaultAudioOutputWatcher? TryStart(Action<string> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        try
        {
            return new DefaultAudioOutputWatcher(changed);
        }
        catch (Exception exception) when (AudioOutputDeviceCatalog.IsDevicePlaybackFailure(exception)
            || exception is InvalidCastException)
        {
            DiagnosticLog.Warning(
                "audio-device",
                $"Nie można śledzić zmian domyślnego urządzenia audio; błąd {exception.GetType().Name}.");
            return null;
        }
    }

    /// <summary>
    /// Czy powiadomienie dotyczy urządzenia, którego używa WasapiOut bez
    /// wskazanego urządzenia.
    /// </summary>
    internal static bool IsRelevant(DataFlow flow, Role role) =>
        flow == DataFlow.Render && role == Role.Console;

    /// <summary>
    /// Czy wyjście sesji ma przejść na nowe urządzenie domyślne: tylko gdy
    /// sesja nie ma wybranego konkretnego urządzenia, ma otwarte wyjście na
    /// znanym, nadal aktywnym urządzeniu domyślnym i jest to inne urządzenie
    /// niż nowe. Gdy stare urządzenie zniknęło (odłączone słuchawki), wyjście
    /// odtwarza ponownie odzyskiwanie po utracie urządzenia, które zna też
    /// ustawienie wstrzymania po odłączeniu; wyjście już otwarte na nowym
    /// urządzeniu nie jest otwierane drugi raz.
    /// </summary>
    internal static bool ShouldFollow(
        string? configuredDeviceId,
        string? endpointInUse,
        bool endpointInUseActive,
        string? newDefaultId) =>
        string.IsNullOrWhiteSpace(configuredDeviceId)
        && !string.IsNullOrWhiteSpace(endpointInUse)
        && endpointInUseActive
        && !string.IsNullOrWhiteSpace(newDefaultId)
        && !string.Equals(endpointInUse, newDefaultId, StringComparison.OrdinalIgnoreCase);

    private void OnDefaultDeviceChanged(DataFlow flow, Role role)
    {
        if (!IsRelevant(flow, role)) return;
        lock (_gate)
        {
            if (_disposed) return;
            // Kolejne powiadomienia przesuwają termin: zgłaszamy stan po
            // ustaniu zmian, a nie każdy krok pośredni.
            _timer.Change(SettleDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnSettled()
    {
        // Jedno zgłoszenie naraz: przełączanie wyjścia może potrwać, a drugie
        // powiadomienie w tym czasie ma tylko sprawdzić stan po nim.
        lock (_callbackGate)
        {
            lock (_gate)
            {
                if (_disposed) return;
            }
            // Odczyt poza blokadą i na własnym enumeratorze: wątek powiadomień
            // Windows może w tym czasie czekać na _gate w OnDefaultDeviceChanged.
            var current = TryReadCurrentDefaultId();
            lock (_gate)
            {
                if (_disposed) return;
                if (current is null
                    || string.Equals(current, _lastReportedId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                _lastReportedId = current;
            }

            DiagnosticLog.Info(
                "audio-device",
                $"Windows zmienił domyślne urządzenie odtwarzania. Identyfikator {current}.");
            try
            {
                _changed(current);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                DiagnosticLog.Error(
                    "audio-device",
                    "Nie udało się przejść na nowe domyślne urządzenie audio.",
                    exception);
            }
        }
    }

    private static string? TryReadCurrentDefaultId()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return TryReadDefaultId(enumerator);
        }
        catch (Exception exception) when (AudioOutputDeviceCatalog.IsDevicePlaybackFailure(exception)
            || exception is InvalidCastException)
        {
            return null;
        }
    }

    private static string? TryReadDefaultId(MMDeviceEnumerator enumerator)
    {
        try
        {
            if (!enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Console)) return null;
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return device.ID;
        }
        catch (Exception exception) when (AudioOutputDeviceCatalog.IsDevicePlaybackFailure(exception))
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_client);
        }
        catch (Exception exception) when (AudioOutputDeviceCatalog.IsDevicePlaybackFailure(exception)
            || exception is InvalidCastException)
        {
            // Zamykanie programu nie może się przewrócić na wyrejestrowaniu.
        }
        // Czekamy chwilę na przejście, które już trwa, żeby nie działało
        // równolegle z zamykaniem wyjść. Dłużej nie: zawieszony strumień nie
        // może zatrzymać zamykania programu, a wyjścia i tak sprawdzają, czy
        // nie zostały już zamknięte.
        if (Monitor.TryEnter(_callbackGate, CallbackShutdownWait)) Monitor.Exit(_callbackGate);
        _timer.Dispose();
        _enumerator.Dispose();
    }

    /// <summary>
    /// Windows wywołuje te metody na własnym wątku i nie wolno w nich czekać,
    /// dlatego tylko przekazujemy sygnał dalej.
    /// </summary>
    private sealed class NotificationClient(DefaultAudioOutputWatcher owner) : IMMNotificationClient
    {
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) =>
            owner.OnDefaultDeviceChanged(flow, role);

        public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        {
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
        }

        public void OnDeviceRemoved(string deviceId)
        {
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }
    }
}
