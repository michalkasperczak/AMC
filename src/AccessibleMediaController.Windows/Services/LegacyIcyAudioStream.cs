using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Opens older Icecast/Shoutcast responses whose status line is "ICY 200 OK".
/// The returned stream contains audio bytes only: response headers and optional
/// ICY metadata blocks are removed before a format-specific decoder sees them.
/// </summary>
internal sealed class LegacyIcyAudioStream : Stream
{
    private const int MaximumHeaderBytes = 32 * 1024;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(12);
    private readonly TcpClient _client;
    private readonly Stream _audio;
    private long _position;
    private int _disposed;

    private LegacyIcyAudioStream(TcpClient client, Stream audio, string? contentType)
    {
        _client = client;
        _audio = audio;
        ContentType = contentType;
    }

    public string? ContentType { get; }

    public bool IsMp3 => MatchesContentType("mpeg", "mp3") || string.IsNullOrWhiteSpace(ContentType);
    public bool IsAac => MatchesContentType("aac", "aacp");
    public bool IsOgg => MatchesContentType("ogg", "vorbis");

    public static async Task<LegacyIcyAudioStream> OpenAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Nieprawidłowy adres strumienia.", nameof(source));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectionTimeout);
        var client = new TcpClient { NoDelay = true, ReceiveTimeout = 20_000, SendTimeout = 12_000 };
        Stream? transport = null;
        try
        {
            var port = uri.IsDefaultPort
                ? uri.Scheme == "https" ? 443 : 80
                : uri.Port;
            await client.ConnectAsync(uri.Host, port, timeout.Token).ConfigureAwait(false);
            transport = client.GetStream();
            if (uri.Scheme == "https")
            {
                var secure = new SslStream(transport, false);
                await secure.AuthenticateAsClientAsync(
                    new SslClientAuthenticationOptions { TargetHost = uri.Host },
                    timeout.Token).ConfigureAwait(false);
                transport = secure;
            }

            var buffered = new BufferedStream(transport, 32 * 1024);
            transport = buffered;
            var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            var path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            var request = Encoding.ASCII.GetBytes(
                $"GET {path} HTTP/1.0\r\n" +
                $"Host: {host}\r\n" +
                "User-Agent: AccessibleMultimediaController/0.1\r\n" +
                "Accept: audio/mpeg,audio/aac,audio/aacp,audio/ogg,application/ogg,*/*\r\n" +
                "Icy-MetaData: 0\r\n" +
                "Connection: close\r\n\r\n");
            await buffered.WriteAsync(request, timeout.Token).ConfigureAwait(false);
            await buffered.FlushAsync(timeout.Token).ConfigureAwait(false);

            var headerText = await ReadHeadersAsync(buffered, timeout.Token).ConfigureAwait(false);
            var lines = headerText.Split("\r\n", StringSplitOptions.None);
            if (lines.Length == 0 || !IsSuccessfulStatus(lines[0]))
            {
                throw new InvalidDataException("Serwer nie zwrócił prawidłowego strumienia audio.");
            }

            var headers = ParseHeaders(lines.Skip(1));
            if (headers.TryGetValue("transfer-encoding", out var transferEncoding)
                && transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Starszy strumień używa nieobsługiwanego kodowania fragmentowego.");
            }

            Stream audio = buffered;
            if (headers.TryGetValue("icy-metaint", out var metadataIntervalText)
                && int.TryParse(metadataIntervalText, out var metadataInterval)
                && metadataInterval > 0)
            {
                audio = new IcyMetadataStrippingStream(buffered, metadataInterval);
                transport = audio;
            }

            var result = new LegacyIcyAudioStream(
                client,
                audio,
                headers.GetValueOrDefault("content-type"));
            transport = null;
            return result;
        }
        catch
        {
            transport?.Dispose();
            client.Dispose();
            throw;
        }
    }

    private bool MatchesContentType(params string[] values)
    {
        if (string.IsNullOrWhiteSpace(ContentType)) return false;
        var mediaType = ContentType.Split(';', 2)[0].Trim();
        return values.Any(value => mediaType.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public override bool CanRead => Volatile.Read(ref _disposed) == 0;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    // Media Foundation asks IStream.Stat for a length even when CanSeek is
    // false. A live broadcast has no real end, so expose a large logical
    // length without claiming that the stream can seek.
    public override long Length => long.MaxValue;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var read = _audio.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var read = _audio.Read(buffer);
        _position += read;
        return read;
    }

    public override int ReadByte()
    {
        Span<byte> value = stackalloc byte[1];
        return Read(value) == 1 ? value[0] : -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && disposing)
        {
            try { _audio.Dispose(); } catch (Exception) { }
            _client.Dispose();
        }
        base.Dispose(disposing);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[MaximumHeaderBytes];
        var count = 0;
        while (count < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(count, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("Serwer zakończył połączenie przed wysłaniem nagłówków.");
            count += read;
            if (count >= 4
                && header[count - 4] == '\r'
                && header[count - 3] == '\n'
                && header[count - 2] == '\r'
                && header[count - 1] == '\n')
            {
                return Encoding.ASCII.GetString(header, 0, count - 4);
            }
        }
        throw new InvalidDataException("Nagłówki odpowiedzi stacji są zbyt duże.");
    }

    private static bool IsSuccessfulStatus(string statusLine)
    {
        if (!statusLine.StartsWith("ICY ", StringComparison.OrdinalIgnoreCase)
            && !statusLine.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[1], out var status) && status is >= 200 and <= 299;
    }

    private static Dictionary<string, string> ParseHeaders(IEnumerable<string> lines)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return headers;
    }

    private sealed class IcyMetadataStrippingStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _interval;
        private int _audioRemaining;

        public IcyMetadataStrippingStream(Stream inner, int interval)
        {
            _inner = inner;
            _interval = interval;
            _audioRemaining = interval;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_audioRemaining == 0 && !SkipMetadata()) return 0;
            var read = _inner.Read(buffer, offset, Math.Min(count, _audioRemaining));
            if (read > 0) _audioRemaining -= read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            if (_audioRemaining == 0 && !SkipMetadata()) return 0;
            var read = _inner.Read(buffer[..Math.Min(buffer.Length, _audioRemaining)]);
            if (read > 0) _audioRemaining -= read;
            return read;
        }

        private bool SkipMetadata()
        {
            var lengthByte = _inner.ReadByte();
            if (lengthByte < 0) return false;
            var bytesToSkip = lengthByte * 16;
            Span<byte> discard = stackalloc byte[256];
            while (bytesToSkip > 0)
            {
                var read = _inner.Read(discard[..Math.Min(discard.Length, bytesToSkip)]);
                if (read == 0) return false;
                bytesToSkip -= read;
            }
            _audioRemaining = _interval;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
