using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

internal static class TransientFlacOutputTests
{
    private const string Capability = "0123456789abcdef0123456789abcdef";

    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var ffmpeg = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new InvalidOperationException("Test sieciowego FLAC wymaga FFmpeg.");
        var root = Path.Combine(Path.GetTempPath(), "amc-transient-flac-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var flacPath = Path.Combine(root, "tone.flac");
            CreateFlac(ffmpeg, flacPath);
            using var server = new LocalFlacServer(await File.ReadAllBytesAsync(flacPath));
            var transientUri = server.StreamUri;
            var item = new MediaItem
            {
                Id = "tidal:42",
                Source = "tidal:track:42",
                Kind = MediaItemKind.Track,
                Title = "Syntetyczny ton FLAC",
                Duration = TimeSpan.FromSeconds(8)
            };

            DiagnosticLog.Initialize(Path.Combine(root, "logs"));
            WindowsMediaOutput.ValidateTransientFlacRequest(item, transientUri, item.Duration);
            using var reader = WindowsMediaOutput.OpenTransientFlacReader(
                item.Source, transientUri.AbsoluteUri, item.Duration);
            var samples = new byte[32_768];
            var firstRead = reader.Read(samples, 0, samples.Length);
            Assert(firstRead > 0 && ContainsAudiblePcm(samples, firstRead),
                "Sieciowy FLAC nie dostarczył próbek PCM.");
            Assert(reader.SourcePath == item.Source,
                "Dekoder ujawnił chwilowy adres zamiast stałego identyfikatora.");

            reader.CurrentTime = TimeSpan.FromSeconds(3);
            var afterSeek = reader.Read(samples, 0, samples.Length);
            Assert(afterSeek > 0 && ContainsAudiblePcm(samples, afterSeek),
                "Sieciowy FLAC nie dostarczył próbek PCM po przewinięciu.");
            Assert(reader.CurrentTime >= TimeSpan.FromSeconds(2.5),
                "Przewijanie sieciowego FLAC nie osiągnęło żądanej pozycji.");
            Assert(server.RequestCount >= 2,
                "FFmpeg nie pobrał źródła ponownie po przewinięciu.");

            using var output = new WindowsMediaOutput();
            var unsafeItem = new MediaItem { Id = item.Id, Source = transientUri.AbsoluteUri };
            AssertThrows<ArgumentException>(() => output.PlayTransientFlac(
                    unsafeItem, transientUri, item.Duration, TimeSpan.Zero, 30, 1d),
                "Wejście odtwarzacza przyjęło URL w MediaItem.Source.");
            AssertThrows<ArgumentException>(() => output.PlayTransientFlac(
                    item, new Uri("https://audio.tidal.com/signed.flac?sig=upstream-secret"),
                    item.Duration, TimeSpan.Zero, 30, 1d),
                "Wejście odtwarzacza przyjęło podpisany adres upstream HTTPS.");
            AssertThrows<ArgumentException>(() => output.PlayTransientFlac(
                    item, new Uri($"http://localhost:{transientUri.Port}/audio/{Capability}"), item.Duration,
                    TimeSpan.Zero, 30, 1d),
                "Wejście odtwarzacza przyjęło alias localhost.");
            AssertThrows<ArgumentException>(() => output.PlayTransientFlac(
                    item, new Uri($"http://192.0.2.1:{transientUri.Port}/audio/{Capability}"), item.Duration,
                    TimeSpan.Zero, 30, 1d),
                "Wejście odtwarzacza przyjęło niezabezpieczony zdalny HTTP.");
            AssertThrows<ArgumentException>(() => output.PlayTransientFlac(
                    item, new Uri(transientUri.AbsoluteUri + "?token=query-secret"), item.Duration,
                    TimeSpan.Zero, 30, 1d),
                "Wejście odtwarzacza przyjęło parametry URL.");
            AssertThrows<ArgumentException>(() => output.PlayTransientFlac(
                    item, new Uri(transientUri.AbsoluteUri + "#fragment-secret"), item.Duration,
                    TimeSpan.Zero, 30, 1d),
                "Wejście odtwarzacza przyjęło fragment URL.");

            DiagnosticLog.Shutdown();
            var log = File.ReadAllText(Path.Combine(root, "logs", "amc.log"));
            Assert(!log.Contains(Capability, StringComparison.Ordinal)
                   && !log.Contains("upstream-secret", StringComparison.Ordinal),
                "Diagnostyka ujawniła chwilowy adres strumienia.");
            Assert(item.Source == "tidal:track:42" && item.Id == "tidal:42",
                "Chwilowy adres zmienił model utworu.");
            Console.WriteLine("OK: sieciowy FLAC dał PCM, seek działa, URL nie trafił do logu/modelu");
        }
        finally
        {
            DiagnosticLog.Shutdown();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateFlac(string ffmpeg, string path)
    {
        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=8",
            "-c:a", "flac", "-y", path
        }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie uruchomiono FFmpeg w teście.");
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(20_000) || process.ExitCode != 0)
            throw new InvalidOperationException("Nie utworzono syntetycznego FLAC: " + error);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool ContainsAudiblePcm(byte[] samples, int bytesRead)
    {
        for (var offset = 0; offset + sizeof(float) <= bytesRead; offset += sizeof(float))
        {
            if (Math.Abs(BitConverter.ToSingle(samples, offset)) > 0.01f) return true;
        }
        return false;
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private sealed class LocalFlacServer : IDisposable
    {
        private readonly byte[] _flac;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _serverTask;
        private int _requestCount;

        internal LocalFlacServer(byte[] flac)
        {
            _flac = flac;
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            StreamUri = new Uri($"http://127.0.0.1:{port}/audio/{Capability}");
            _serverTask = Task.Run(ServeAsync);
        }

        internal Uri StreamUri { get; }
        internal int RequestCount => Volatile.Read(ref _requestCount);

        private async Task ServeAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_cancellation.Token); }
                catch (OperationCanceledException) { break; }
                catch (SocketException) when (_cancellation.IsCancellationRequested) { break; }
                using (client)
                {
                    try { await RespondAsync(client, _cancellation.Token); }
                    catch (IOException) when (_cancellation.IsCancellationRequested) { break; }
                }
            }
        }

        private async Task RespondAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var request = await reader.ReadLineAsync(cancellationToken);
            if (request is null) return;
            Interlocked.Increment(ref _requestCount);
            long offset = 0;
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
            {
                if (line.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    var first = line["Range: bytes=".Length..].Split('-', 2)[0];
                    if (long.TryParse(first, out var requested)) offset = requested;
                }
            }
            if (offset >= _flac.Length) offset = 0;
            var partial = offset > 0;
            var header = new StringBuilder()
                .Append(partial ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n")
                .Append("Content-Type: audio/flac\r\n")
                .Append("Accept-Ranges: bytes\r\n")
                .Append("Content-Length: ").Append(_flac.Length - offset).Append("\r\n")
                .Append("Connection: close\r\n");
            if (partial)
                header.Append("Content-Range: bytes ").Append(offset).Append('-')
                    .Append(_flac.Length - 1).Append('/').Append(_flac.Length).Append("\r\n");
            header.Append("\r\n");
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header.ToString()), cancellationToken);
            if (!request.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase))
                await stream.WriteAsync(_flac.AsMemory((int)offset), cancellationToken);
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _listener.Stop();
            try { _serverTask.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
            _cancellation.Dispose();
        }
    }
}
