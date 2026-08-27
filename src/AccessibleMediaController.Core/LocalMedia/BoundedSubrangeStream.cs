namespace AccessibleMediaController.Core.LocalMedia;

/// <summary>
/// Exposes a seekable window of another stream without copying it. The class
/// is used to present only the verified MP3 audio payload to a decoder when a
/// file contains a malformed or exceptionally large leading tag.
/// </summary>
public sealed class BoundedSubrangeStream : Stream
{
    private readonly Stream _source;
    private readonly long _origin;
    private readonly long _length;
    private readonly bool _ownsSource;
    private readonly object _gate = new();
    private long _position;
    private bool _disposed;

    public BoundedSubrangeStream(
        Stream source,
        long origin,
        long? length = null,
        bool ownsSource = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek)
        {
            throw new ArgumentException("Strumień źródłowy musi obsługiwać odczyt i przewijanie.", nameof(source));
        }
        if (origin < 0 || origin > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        var available = checked(source.Length - origin);
        var resolvedLength = length ?? available;
        if (resolvedLength < 0 || resolvedLength > available)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _source = source;
        _origin = origin;
        _length = resolvedLength;
        _ownsSource = ownsSource;
    }

    public static BoundedSubrangeStream OpenFile(string path, long origin)
    {
        var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.RandomAccess);
        try
        {
            return new BoundedSubrangeStream(source, origin, ownsSource: true);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public override bool CanRead => !_disposed && _source.CanRead;
    public override bool CanSeek => !_disposed && _source.CanSeek;
    public override bool CanWrite => false;
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _length;
        }
    }
    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lock (_gate) return _position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (value < 0 || value > _length) throw new ArgumentOutOfRangeException(nameof(value));
            lock (_gate) _position = value;
        }
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset > buffer.Length - count)
        {
            throw new ArgumentOutOfRangeException(offset < 0 ? nameof(offset) : nameof(count));
        }
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            var remaining = _length - _position;
            if (remaining <= 0 || buffer.Length == 0) return 0;
            var requested = (int)Math.Min(buffer.Length, remaining);
            _source.Position = checked(_origin + _position);
            var read = _source.Read(buffer[..requested]);
            if (read < 0 || read > requested)
            {
                throw new InvalidDataException("Strumień źródłowy zwrócił nieprawidłową liczbę bajtów.");
            }
            _position = checked(_position + read);
            return read;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            var target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(_length + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (target < 0 || target > _length)
            {
                throw new IOException("Próba przewinięcia poza udostępniony fragment strumienia.");
            }
            _position = target;
            return target;
        }
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing && _ownsSource) _source.Dispose();
        _disposed = true;
        base.Dispose(disposing);
    }
}
