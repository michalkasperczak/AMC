using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.LiteHost;

/// <summary>One serialized CDP connection. Replies can be interleaved with events.</summary>
internal sealed class LiteChromeDevTools : IAsyncDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private int nextId;

    private LiteChromeDevTools() { }

    internal static async Task<LiteChromeDevTools> ConnectAsync(
        string address, CancellationToken cancellationToken)
    {
        var client = new LiteChromeDevTools();
        try
        {
            await client.socket.ConnectAsync(new Uri(address), cancellationToken)
                .ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.socket.Dispose();
            throw;
        }
    }

    internal async Task<JsonElement> CallAsync(
        string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var id = ++nextId;
            var request = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id,
                method,
                @params = parameters ?? new { }
            });
            await socket.SendAsync(request, WebSocketMessageType.Text, true, timeout.Token)
                .ConfigureAwait(false);
            while (true)
            {
                using var message = new MemoryStream();
                var buffer = new byte[8192];
                WebSocketReceiveResult part;
                do
                {
                    part = await socket.ReceiveAsync(buffer, timeout.Token).ConfigureAwait(false);
                    if (part.MessageType == WebSocketMessageType.Close)
                        throw new IOException("Chrome zamknął połączenie sterujące.");
                    message.Write(buffer, 0, part.Count);
                    if (message.Length > 2_000_000)
                        throw new IOException("Odpowiedź Chrome jest zbyt duża.");
                } while (!part.EndOfMessage);
                using var document = JsonDocument.Parse(message.ToArray());
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var replyId)
                    || replyId.GetInt32() != id) continue;
                if (root.TryGetProperty("error", out _))
                    throw new IOException($"Chrome odrzucił polecenie {method}.");
                return root.TryGetProperty("result", out var result)
                    ? result.Clone()
                    : default;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    internal async Task<JsonElement> EvaluateAsync(
        string expression, CancellationToken cancellationToken = default)
    {
        var response = await CallAsync("Runtime.evaluate", new
        {
            expression,
            awaitPromise = true,
            returnByValue = true
        }, cancellationToken).ConfigureAwait(false);
        if (response.TryGetProperty("exceptionDetails", out _))
            throw new IOException("Chrome nie wykonał polecenia odtwarzacza Spotify.");
        var result = response.GetProperty("result");
        // SDK methods such as setVolume resolve with JavaScript undefined.
        return result.TryGetProperty("value", out var value) ? value.Clone() : default;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "AMC", CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch { /* A dead Chrome must not hold application shutdown. */ }
        socket.Dispose();
        gate.Dispose();
    }
}
