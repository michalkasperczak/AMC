using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Windows.Services;

internal static class NvdaBridgeSmokeTests
{
    internal static void Run()
    {
        // Earlier WPF tests can leave a DispatcherSynchronizationContext installed.
        // Transport tests must not capture it while this STA waits for completion.
        Task.Run(RunAsync).GetAwaiter().GetResult();
        TestExpiredDispatcherWork();
    }

    private static void TestExpiredDispatcherWork()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var calls = 0;
        using var deadline = new CancellationTokenSource(30);
        var operation = dispatcher.InvokeAsync(() => calls++, DispatcherPriority.Input, deadline.Token);
        // Simulate a busy UI without pumping it until the request expires.
        Check(deadline.Token.WaitHandle.WaitOne(1000), "Dispatcher deadline expired");
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
        Check(calls == 0 && operation.Task.IsCanceled, "Expired request cannot run after the UI recovers");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private static async Task RunAsync()
    {
        Check(NvdaCommands.Resolve("playPause") == CommandIds.PlayPause, "NVDA uses canonical pause command");
        Check(NvdaCommands.Resolve("sessionNext") == CommandIds.SessionNext, "NVDA uses canonical session command");
        foreach (var value in new[] { "transport.playPause", "delete", "record", "shell", "", "status\nnext" })
            Check(!NvdaCommands.IsAllowed(value), "Reject arbitrary commands");
        foreach (var wire in new[] { "{}\n", "[]\n", "{\"version\":\"1\",\"command\":\"next\"}\n",
                     "{\"version\":2,\"command\":\"next\"}\n", "{\"version\":1,\"command\":\"delete\"}\n",
                     "{\"version\":1,\"command\":\"next\"}", new string('a', 513) })
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(wire));
            Check(await NvdaCommandServer.ReadRequestAsync(input, CancellationToken.None) is null, "Reject invalid wire request");
        }
        var calls = new List<string>();
        var name = "AMC.NVDA.test." + Guid.NewGuid().ToString("N");
        using var server = new NvdaCommandServer((command, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            calls.Add(command);
            return Task.FromResult(new NvdaReply(true, "Żółć, głośność 35%"));
        }, name);
        var response = await SendAsync(name, "{\"version\":1,\"command\":\"status\"}\n");
        Check(response.GetProperty("message").GetString() == "Żółć, głośność 35%", "Unicode reply preserved");
        response = await SendAsync(name, "{\"version\":1,\"command\":\"delete\"}\n");
        Check(!response.GetProperty("ok").GetBoolean(), "Forbidden operation never dispatched");
        await SendAsync(name, "{\"version\":1,\"command\":\"playPause\"}\n{\"version\":1,\"command\":\"playPause\"}\n");
        Check(calls.SequenceEqual(new[] { "status", "playPause" }), "One connection cannot toggle twice");
        // A silent client must not keep the only listener forever.
        using (var stalled = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await stalled.ConnectAsync(3000);
            using var timeout = new CancellationTokenSource(4000);
            var read = await stalled.ReadAsync(new byte[1], timeout.Token);
            Check(read == 0, "Slow client disconnected by deadline");
        }
        await SendAsync(name, "{\"version\":1,\"command\":\"next\"}\n");
        Check(calls.Count == 3, "Listener recovers after stalled client");
        server.Dispose();
        await server.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Console.WriteLine("NVDA bridge smoke: OK (protocol, allow-list, Unicode, no duplicate execution, timeout, shutdown)");
    }

    private static async Task<JsonElement> SendAsync(string name, string wire)
    {
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(4000);
        await pipe.ConnectAsync(timeout.Token);
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(wire), timeout.Token);
        using var reader = new StreamReader(pipe, Encoding.UTF8);
        var line = await reader.ReadLineAsync(timeout.Token);
        return JsonDocument.Parse(line!).RootElement.Clone();
    }

    // Test-only host for Python <-> .NET transport; no real AMC state/audio involved.
    internal static void RunInteropHost(string pipeName)
    {
        using var server = new NvdaCommandServer((command, _) =>
            Task.FromResult(new NvdaReply(true, "Test połączenia: " + command)), pipeName);
        Console.WriteLine("READY");
        Console.ReadLine();
    }
}
