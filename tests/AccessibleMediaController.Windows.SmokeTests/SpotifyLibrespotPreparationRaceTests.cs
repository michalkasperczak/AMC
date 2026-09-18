using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

internal static class SpotifyLibrespotPreparationRaceTests
{
    internal static void Run()
    {
        // Full WPF suite can leave a DispatcherSynchronizationContext on its
        // STA thread. These protocol tests have an inline fake UI dispatcher.
        Task.Run(async () =>
        {
            await VerifyCancelBeforeVolumeAck(false);
            await VerifyCancelBeforeVolumeAck(true);
            await VerifyNewPlayBeforeVolumeAck();
        }).GetAwaiter().GetResult();
        Console.WriteLine("OK: Librespot nie odtwarza po pauzie/stop ani starego Play po opóźnionej głośności");
    }

    private static MediaItem Track(string id) => new()
    {
        Id = id, ExternalId = id, Source = "spotify:track:" + id,
        Title = id, Kind = MediaItemKind.Track
    };

    private static async Task VerifyCancelBeforeVolumeAck(bool stop)
    {
        using var host = new Host();
        using var client = new LibrespotHostClient(() => host);
        await client.StartAsync();
        using var output = new SpotifyLibrespotMediaOutput(client, action => action());
        output.Play(Track("A"), TimeSpan.Zero, 50, 1);
        var started = output.PendingPlaybackStart;
        var volume = await host.Volumes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        if (stop) output.Stop(); else output.Pause();
        host.Ack(volume);
        // Await the actual preparation continuation, not a queue-read counter or delay.
        await started.WaitAsync(TimeSpan.FromSeconds(3));
        await client.PingAsync();
        if (host.Commands.Any(c => c["command"]?.GetValue<string>() == "play"))
            throw new Exception((stop ? "Stop" : "Pauza") + " przed potwierdzeniem głośności dopuściła późniejsze Play.");
        if (output.IsPreparing) throw new Exception("Anulowane przygotowanie nadal trwa.");
    }

    private static async Task VerifyNewPlayBeforeVolumeAck()
    {
        using var host = new Host();
        using var client = new LibrespotHostClient(() => host);
        await client.StartAsync();
        using var output = new SpotifyLibrespotMediaOutput(client, action => action());
        output.Play(Track("A"), TimeSpan.Zero, 50, 1);
        var first = output.PendingPlaybackStart;
        var a = await host.Volumes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        output.Play(Track("B"), TimeSpan.Zero, 60, 1);
        var second = output.PendingPlaybackStart;
        var b = await host.Volumes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        host.Ack(b);
        await second.WaitAsync(TimeSpan.FromSeconds(3));
        host.Ack(a);
        await first.WaitAsync(TimeSpan.FromSeconds(3));
        await client.PingAsync();
        var plays = host.Commands.Where(c => c["command"]?.GetValue<string>() == "play").ToArray();
        if (plays.Length != 1 || plays[0]["uri"]?.GetValue<string>() != "spotify:track:B")
            throw new Exception("Opóźnione przygotowanie A wyparło nowsze odtwarzanie B.");
    }

    private sealed class Host : ILibrespotHostProcess
    {
        private readonly Reader output = new();
        public Channel<JsonObject> Volumes { get; } = Channel.CreateUnbounded<JsonObject>();
        public ConcurrentQueue<JsonObject> Commands { get; } = new();
        public Host()
        {
            StandardInput = new Writer(Accept);
            output.Add(new JsonObject { ["type"] = "ready", ["protocolVersion"] = 1,
                ["sessionId"] = LibrespotHostContract.SessionId });
        }
        public TextWriter StandardInput { get; }
        public TextReader StandardOutput => output;
        public bool HasExited { get; private set; }
        public int? ExitCode => HasExited ? 0 : null;
        private void Accept(string text)
        {
            var command = JsonNode.Parse(text)!.AsObject();
            Commands.Enqueue(command);
            if (command["command"]?.GetValue<string>() == "volume")
                Volumes.Writer.TryWrite(command);
            else Ack(command);
        }
        public void Ack(JsonObject command) => output.Add(new JsonObject
        {
            ["type"] = "ack", ["sessionId"] = LibrespotHostContract.SessionId,
            ["requestId"] = command["requestId"]!.DeepClone()
        });
        public void Kill() { HasExited = true; output.End(); }
        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() => Kill();
    }

    private sealed class Writer(Action<string> accept) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value) { accept(value!); return Task.CompletedTask; }
        public override Task FlushAsync() => Task.CompletedTask;
    }

    private sealed class Reader : TextReader
    {
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>();
        public void Add(JsonObject value) => lines.Writer.TryWrite(value.ToJsonString() + "\n");
        public void End() => lines.Writer.TryComplete();
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!await lines.Reader.WaitToReadAsync(cancellationToken)) return 0;
            var line = await lines.Reader.ReadAsync(cancellationToken);
            if (line.Length > buffer.Length) throw new InvalidOperationException("Fixture line exceeds buffer.");
            line.AsMemory().CopyTo(buffer);
            return line.Length;
        }
    }
}
