using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Powstrzymuje uspienie systemu podczas nagrywania. Klasa jest osobnym
/// plikiem, bo korzystaja z niej zarowno nagrania planowane, jak i reczne
/// uruchamiane przez bezokienny host interfejsu wxPython.
/// </summary>
internal sealed class WindowsPowerRequest : IDisposable
{
    private readonly SafeFileHandle _handle;
    private bool _active;

    private WindowsPowerRequest(SafeFileHandle handle)
    {
        _handle = handle;
        _active = PowerSetRequest(handle, PowerRequestType.SystemRequired);
    }

    public static WindowsPowerRequest? TryCreate(string reason)
    {
        try
        {
            var context = new ReasonContext
            {
                Version = 0,
                Flags = 1,
                SimpleReasonString = reason
            };
            var handle = PowerCreateRequest(ref context);
            return handle.IsInvalid ? null : new WindowsPowerRequest(handle);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            DiagnosticLog.Warning("radio-recording", "Windows nie udostepnia blokady uspienia dla nagrywania.");
            return null;
        }
    }

    public void Dispose()
    {
        if (_active)
        {
            _ = PowerClearRequest(_handle, PowerRequestType.SystemRequired);
            _active = false;
        }
        _handle.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)] public string SimpleReasonString;
    }

    private enum PowerRequestType
    {
        DisplayRequired,
        SystemRequired,
        AwayModeRequired,
        ExecutionRequired
    }

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle PowerCreateRequest(ref ReasonContext context);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool PowerSetRequest(SafeFileHandle powerRequest, PowerRequestType requestType);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool PowerClearRequest(SafeFileHandle powerRequest, PowerRequestType requestType);
}
