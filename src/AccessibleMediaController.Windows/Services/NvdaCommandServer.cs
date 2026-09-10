using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Commands;

namespace AccessibleMediaController.Windows.Services;

// A small, local-only, versioned allow-list. Never accept shell commands,
// keyboard input, paths, arbitrary router IDs, or account data from the client.
internal static class NvdaCommands
{
    internal static string? Resolve(string command) => command switch
    {
        "playPause" => CommandIds.PlayPause,
        "previous" => CommandIds.Previous,
        "next" => CommandIds.Next,
        "volumeUp" => CommandIds.VolumeUp5,
        "volumeDown" => CommandIds.VolumeDown5,
        "mute" => CommandIds.ToggleMuteCurrentSession,
        "seekBack" => CommandIds.SeekBackward10,
        "seekForward" => CommandIds.SeekForward10,
        "elapsed" => CommandIds.TimeElapsed,
        "remaining" => CommandIds.TimeRemaining,
        "total" => CommandIds.TimeTotal,
        "sessionPrevious" => CommandIds.SessionPrevious,
        "sessionNext" => CommandIds.SessionNext,
        _ => null
    };

    internal static bool IsAllowed(string command) => command == "status" || Resolve(command) is not null;
}

internal sealed record NvdaReply(bool Ok, string Message);

internal sealed class NvdaCommandServer : IDisposable
{
    internal static string DefaultPipeName => $"AMC.NVDA.v1.{Process.GetCurrentProcess().SessionId}";
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<string, CancellationToken, Task<NvdaReply>> _execute;
    private readonly string _pipeName;
    internal Task Completion { get; }

    internal NvdaCommandServer(Func<string, CancellationToken, Task<NvdaReply>> execute, string? pipeName = null)
    {
        _execute = execute;
        _pipeName = pipeName ?? DefaultPipeName;
        Completion = Task.Run(ListenAsync);
    }

    private async Task ListenAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    4096, 16384);
                await pipe.WaitForConnectionAsync(_shutdown.Token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    var request = await ReadRequestAsync(pipe, deadline.Token);
                    var reply = request is not null
                        ? await _execute(request, deadline.Token)
                        : new NvdaReply(false, "Nieobsługiwane polecenie lub wersja dodatku AMC.");
                    var message = reply.Message.Length > 2000 ? reply.Message[..2000] : reply.Message;
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        version = 1, ok = reply.Ok, message
                    }) + "\n");
                    await pipe.WriteAsync(bytes, deadline.Token);
                    // One connection = at most one command. Never retry a toggle.
                }
                catch (Exception exception) when (exception is IOException or OperationCanceledException or JsonException)
                {
                    // Broken, oversized, partial, or timed-out client: discard it.
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Error("nvda-bridge", "Nie udało się wykonać polecenia dodatku NVDA.", exception);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception)
        {
            DiagnosticLog.Error("nvda-bridge", "Połączenie z dodatkiem NVDA jest niedostępne; odtwarzacz działa dalej.", exception);
        }
    }

    internal static async Task<string?> ReadRequestAsync(Stream stream, CancellationToken cancellation)
    {
        var bytes = new byte[512];
        var one = new byte[1];
        for (var length = 0; length < bytes.Length; length++)
        {
            if (await stream.ReadAsync(one, cancellation) != 1) return null;
            if (one[0] != (byte)'\n') { bytes[length] = one[0]; continue; }
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var number) || number != 1
                || !root.TryGetProperty("command", out var value)
                || value.ValueKind != JsonValueKind.String) return null;
            var command = value.GetString();
            return command is not null && NvdaCommands.IsAllowed(command) ? command : null;
        }
        return null;
    }

    public void Dispose() => _shutdown.Cancel(); // Never wait for the UI from the UI.
}
