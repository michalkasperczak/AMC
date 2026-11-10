using System.Text.Json;
using System.Runtime.InteropServices;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Właściciel zabezpieczonego potoku dodatku NVDA dla wariantu wxPython.
/// Serwer pozostaje w C#, dzięki czemu zachowuje dokładnie tę samą ochronę
/// <c>CurrentUserOnly</c>, limit czasu i listę dozwolonych poleceń co pełny AMC.
/// Samo wykonanie przekazuje właścicielowi interfejsu przez JSON-lines.
/// </summary>
internal sealed class LiteNvdaBridge(LiteNvdaRelay relay, string releaseProfile) : IDisposable
{
    internal const string ConfigureOperation = "nvda.configure";
    private NvdaCommandServer? _server;
    private LiteEventSink? _events;
    private nint _windowHandle;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint handle, int command);

    internal void Start(LiteEventSink events)
    {
        _events = events;
        var pipeName = NvdaCommandServer.ProfilePipeName(
            releaseProfile, System.Diagnostics.Process.GetCurrentProcess().SessionId);
        _server = new NvdaCommandServer(ExecuteAsync, pipeName);
    }

    private async Task<NvdaReply> ExecuteAsync(string command, CancellationToken cancellation)
    {
        var events = _events;
        if (events is null)
            return new(false, "AMC Python nie jest jeszcze gotowy.");
        // Dodatek udziela prawa do aktywacji dokładnie procesowi serwera
        // potoku. Serwer mieszka w bezokiennym hoście C#, dlatego to on — a
        // nie proces Python bez tego prawa — przywołuje zarejestrowane okno.
        // Robimy to wyłącznie dla jawnych poleceń otwierających widok.
        if (NvdaCommands.OpensWindow(command) && _windowHandle != 0 && IsWindow(_windowHandle))
        {
            ShowWindow(_windowHandle, 9); // SW_RESTORE
            SetForegroundWindow(_windowHandle);
        }
        var result = await relay.RequestAsync(command, events, cancellation).ConfigureAwait(false);
        return new NvdaReply(result.Ok, result.Message);
    }

    internal IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> Handlers() =>
        new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            [ConfigureOperation] = Configure
        };

    private object Configure(LiteRequest request, LiteEventSink _events)
    {
        if (request.Args.ValueKind != JsonValueKind.Object
            || !request.Args.TryGetProperty("windowHandle", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var rawHandle)
            || rawHandle <= 0
            || !IsWindow((nint)rawHandle))
        {
            throw new InvalidOperationException("Nie można zarejestrować okna AMC Python.");
        }
        _windowHandle = (nint)rawHandle;
        return new { ready = true };
    }

    public void Dispose()
    {
        _server?.Dispose();
        relay.Dispose();
    }
}
