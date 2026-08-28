using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using AccessibleMediaController.Core.LocalMedia;
using NAudio.Wave;
using NLayer.NAudioSupport;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Sequential MP3 reader for older Shoutcast/Icecast servers which begin the
/// response with "ICY 200 OK". Media Foundation rejects that legacy status
/// line even though the server is sending a valid live MP3 stream.
/// </summary>
internal sealed class LegacyIcyMp3StreamReader : IWaveProvider, IDisposable
{
    private const int MaximumHeaderBytes = 32 * 1024;
    private const int MaximumFrameAlignmentBytes = 8 * 1024;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(12);
    private readonly TcpClient _client;
    private readonly Stream _transport;
    private readonly Mp3FrameDecompressor _decoder;
    private readonly byte[] _decodedFrame = new byte[32 * 1024];
    private readonly int _sourceSampleRate;
    private readonly int _sourceChannels;
    private Mp3Frame? _firstFrame;
    private int _decodedOffset;
    private int _decodedCount;
    private int _disposed;

    private LegacyIcyMp3StreamReader(TcpClient client, Stream transport)
    {
        _client = client;
        try
        {
            _transport = AlignToVerifiedFrame(transport);
            _firstFrame = Mp3Frame.LoadFromStream(_transport, true);
            if (_firstFrame is null)
            {
                throw new InvalidDataException("Strumień nie zawiera obsługiwanego dźwięku MP3.");
            }
            var channels = _firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2;
            _sourceSampleRate = _firstFrame.SampleRate;
            _sourceChannels = channels;
            var sourceFormat = new Mp3WaveFormat(
                _firstFrame.SampleRate,
                channels,
                _firstFrame.FrameLength,
                _firstFrame.BitRate);
            _decoder = new Mp3FrameDecompressor(sourceFormat);
            WaveFormat = _decoder.OutputFormat;
        }
        catch
        {
            try { _transport?.Dispose(); } catch (Exception) { transport.Dispose(); }
            client.Dispose();
            throw;
        }
    }

    public WaveFormat WaveFormat { get; }

    private static Stream AlignToVerifiedFrame(Stream transport)
    {
        var prefix = new byte[MaximumFrameAlignmentBytes];
        var count = 0;
        while (count < prefix.Length)
        {
            var read = transport.Read(prefix, count, prefix.Length - count);
            if (read <= 0) break;
            count += read;
        }

        if (Mp3StructureProbe.TryFindConsecutiveFrameOffset(prefix.AsSpan(0, count), out var offset))
        {
            return new PrefixReadStream(prefix, offset, count - offset, transport);
        }
        transport.Dispose();
        throw new InvalidDataException(
            "Nie znaleziono dwóch kolejnych prawidłowych ramek w strumieniu MP3.");
    }

    public static async Task<LegacyIcyMp3StreamReader> OpenAsync(
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
                "Accept: audio/mpeg,audio/mp3,*/*\r\n" +
                "Icy-MetaData: 0\r\n" +
                "Connection: close\r\n\r\n");
            await buffered.WriteAsync(request, timeout.Token).ConfigureAwait(false);
            await buffered.FlushAsync(timeout.Token).ConfigureAwait(false);

            var headerText = await ReadHeadersAsync(buffered, timeout.Token).ConfigureAwait(false);
            var lines = headerText.Split("\r\n", StringSplitOptions.None);
            if (lines.Length == 0 || !TryReadSuccessfulStatus(lines[0], out _))
            {
                throw new InvalidDataException("Serwer nie zwrócił prawidłowego strumienia audio.");
            }

            var headers = ParseHeaders(lines.Skip(1));
            if (headers.TryGetValue("transfer-encoding", out var transferEncoding)
                && transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Starszy strumień używa nieobsługiwanego kodowania fragmentowego.");
            }
            if (headers.TryGetValue("content-type", out var contentType)
                && !IsSupportedMp3ContentType(contentType))
            {
                throw new InvalidDataException("Awaryjny dekoder otrzymał strumień inny niż MP3.");
            }

            Stream audio = buffered;
            if (headers.TryGetValue("icy-metaint", out var metadataIntervalText)
                && int.TryParse(metadataIntervalText, out var metadataInterval)
                && metadataInterval > 0)
            {
                audio = new IcyMetadataStrippingStream(buffered, metadataInterval);
                transport = audio;
            }

            var result = new LegacyIcyMp3StreamReader(client, new PositionTrackingReadStream(audio));
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

    internal static bool IsSupportedMp3ContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return true;
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Contains("mpeg", StringComparison.OrdinalIgnoreCase)
            || mediaType.Contains("mp3", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("binary/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Nieprawidłowy zakres bufora.");

        var written = 0;
        while (written < count)
        {
            if (_decodedOffset < _decodedCount)
            {
                var copy = Math.Min(count - written, _decodedCount - _decodedOffset);
                Buffer.BlockCopy(_decodedFrame, _decodedOffset, buffer, offset + written, copy);
                _decodedOffset += copy;
                written += copy;
                continue;
            }

            var frame = _firstFrame ?? Mp3Frame.LoadFromStream(_transport, true);
            _firstFrame = null;
            if (frame is null) break;
            var channels = frame.ChannelMode == ChannelMode.Mono ? 1 : 2;
            if (frame.SampleRate != _sourceSampleRate || channels != _sourceChannels)
            {
                throw new InvalidDataException("Format strumienia MP3 zmienił się podczas odtwarzania.");
            }
            _decodedOffset = 0;
            _decodedCount = _decoder.DecompressFrame(frame, _decodedFrame, 0);
            if (_decodedCount <= 0) continue;
        }
        return written;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _decoder.Dispose(); } catch (Exception) { }
        try { _transport.Dispose(); } catch (Exception) { }
        _client.Dispose();
    }

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

    private static bool TryReadSuccessfulStatus(string statusLine, out bool legacyIcy)
    {
        legacyIcy = statusLine.StartsWith("ICY ", StringComparison.OrdinalIgnoreCase);
        if (!legacyIcy && !statusLine.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase)) return false;
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

        public override int ReadByte()
        {
            Span<byte> value = stackalloc byte[1];
            return Read(value) == 1 ? value[0] : -1;
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

    /// <summary>
    /// NLayer reads a non-seekable stream sequentially but still inspects its
    /// current byte position. NetworkStream and BufferedStream do not expose
    /// that value, so this wrapper supplies a monotonically increasing one
    /// without pretending that seeking is possible.
    /// </summary>
    private sealed class PositionTrackingReadStream(Stream inner) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            _position += read;
            return read;
        }

        public override int ReadByte()
        {
            var value = inner.ReadByte();
            if (value >= 0) _position++;
            return value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PrefixReadStream(
        byte[] prefix,
        int prefixOffset,
        int prefixCount,
        Stream inner) : Stream
    {
        private int _prefixOffset = prefixOffset;
        private int _prefixRemaining = prefixCount;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var total = 0;
            if (_prefixRemaining > 0)
            {
                var copied = Math.Min(count, _prefixRemaining);
                Buffer.BlockCopy(prefix, _prefixOffset, buffer, offset, copied);
                _prefixOffset += copied;
                _prefixRemaining -= copied;
                total += copied;
            }
            while (total < count)
            {
                // NetworkStream may legally return only part of the requested
                // MP3 frame. Mp3Frame.LoadFromStream treats such a short read
                // as an incomplete final frame, so join packet fragments here
                // until the request is complete or the server really closes.
                var read = inner.Read(buffer, offset + total, count - total);
                if (read == 0) break;
                total += read;
            }
            _position += total;
            return total;
        }

        public override int Read(Span<byte> buffer)
        {
            var total = 0;
            if (_prefixRemaining > 0)
            {
                var copied = Math.Min(buffer.Length, _prefixRemaining);
                prefix.AsSpan(_prefixOffset, copied).CopyTo(buffer);
                _prefixOffset += copied;
                _prefixRemaining -= copied;
                total += copied;
            }
            while (total < buffer.Length)
            {
                var read = inner.Read(buffer[total..]);
                if (read == 0) break;
                total += read;
            }
            _position += total;
            return total;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
