namespace AccessibleMediaController.PythonUpdater;

internal sealed class UpdaterInstanceLease : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;
    private bool _disposed;

    private UpdaterInstanceLease(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    internal bool IsOwner => _ownsMutex;

    internal static UpdaterInstanceLease TryAcquire(string mutexName)
    {
        var mutex = new Mutex(false, mutexName);
        var ownsMutex = false;
        try
        {
            ownsMutex = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }
        return new UpdaterInstanceLease(mutex, ownsMutex);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }
        _mutex.Dispose();
    }
}
