using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Serializes state snapshots away from the WPF dispatcher and keeps only the
/// newest pending snapshot. It is deliberately session-agnostic: local files,
/// radio, streaming services and downloads share the same persistence path.
/// </summary>
internal sealed class StatePersistenceQueue
{
    private readonly object _gate = new();
    private readonly Func<PersistedState, PersistedState> _clone;
    private readonly Action<PersistedState> _save;
    private readonly Action<Exception>? _saveFailed;
    private PersistedState? _pending;
    private Task? _worker;
    private Exception? _lastFailure;

    public StatePersistenceQueue(
        ConfigurationStore store,
        Action<Exception>? saveFailed = null)
        : this(store.CloneState, store.Save, saveFailed)
    {
    }

    internal StatePersistenceQueue(
        Func<PersistedState, PersistedState> clone,
        Action<PersistedState> save,
        Action<Exception>? saveFailed = null)
    {
        _clone = clone;
        _save = save;
        _saveFailed = saveFailed;
    }

    public void Queue(PersistedState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var snapshot = _clone(state);
        lock (_gate)
        {
            _pending = snapshot;
            if (_worker is null || _worker.IsCompleted)
            {
                _worker = Task.Run(ProcessQueue);
            }
        }
    }

    public bool Flush(
        PersistedState finalState,
        TimeSpan timeout,
        out Exception? failure)
    {
        Queue(finalState);
        Task? worker;
        lock (_gate) worker = _worker;

        try
        {
            if (worker is not null && !worker.Wait(timeout))
            {
                failure = new TimeoutException(
                    $"Zapisywanie stanu nie zakończyło się przez {timeout}.");
                return false;
            }
        }
        catch (AggregateException exception)
        {
            failure = exception.GetBaseException();
            return false;
        }

        lock (_gate) failure = _lastFailure;
        return failure is null;
    }

    private void ProcessQueue()
    {
        while (true)
        {
            PersistedState snapshot;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _worker = null;
                    return;
                }

                snapshot = _pending;
                _pending = null;
            }

            Exception? failure = null;
            try
            {
                _save(snapshot);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            lock (_gate) _lastFailure = failure;
            if (failure is not null && _saveFailed is not null)
            {
                try
                {
                    _saveFailed(failure);
                }
                catch
                {
                    // Reporting must never stop processing a newer snapshot.
                }
            }
        }
    }
}
