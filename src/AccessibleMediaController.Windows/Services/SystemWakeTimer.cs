using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Maintains one Windows wake timer for the nearest enabled schedule. The
/// timer can wake a sleeping computer while AMC remains running.
/// </summary>
internal sealed class SystemWakeTimer : IDisposable
{
    private readonly SafeWaitHandle _handle;

    public SystemWakeTimer()
    {
        _handle = CreateWaitableTimer(IntPtr.Zero, true, null);
    }

    public bool IsAvailable => !_handle.IsInvalid;

    public bool Arm(DateTime wakeUtc)
    {
        if (_handle.IsInvalid) return false;
        wakeUtc = wakeUtc.Kind == DateTimeKind.Utc ? wakeUtc : wakeUtc.ToUniversalTime();
        var relative = wakeUtc - DateTime.UtcNow;
        if (relative <= TimeSpan.Zero) relative = TimeSpan.FromSeconds(1);
        var dueTime = -Math.Max(1, relative.Ticks);
        return SetWaitableTimer(_handle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, true);
    }

    public void Cancel()
    {
        if (!_handle.IsInvalid) _ = CancelWaitableTimer(_handle);
    }

    public void Dispose()
    {
        Cancel();
        _handle.Dispose();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateWaitableTimer(
        IntPtr timerAttributes,
        bool manualReset,
        string? timerName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(
        SafeWaitHandle timer,
        ref long dueTime,
        int period,
        IntPtr completionRoutine,
        IntPtr argument,
        bool resume);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelWaitableTimer(SafeWaitHandle timer);
}
