using System.Diagnostics;
using System.IO;
using AccessibleMediaController.Core.LocalMedia;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Keeps decoder calls away from UI-owned position reads and detects a decoder
/// read that stops making progress. The wrapper never modifies the source.
/// </summary>
public sealed class GuardedWaveStream : WaveStream
{
    private static readonly TimeSpan MaximumAcceptedDuration = TimeSpan.FromDays(365);
    private static readonly TimeSpan SeekLockTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DisposeLockTimeout = TimeSpan.FromSeconds(2);

    private readonly WaveStream _inner;
    private readonly object _operationGate = new();
    private readonly long _length;
    private long _position;
    private long _readStartedTimestamp;
    private int _readInProgress;
    private int _disposeRequested;
    private int _innerDisposed;

    public GuardedWaveStream(WaveStream inner, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        _inner = inner;
        SourcePath = sourcePath;

        var format = inner.WaveFormat
            ?? throw new InvalidDataException("Dekoder nie podał formatu dźwięku.");
        if (format.SampleRate is < 1_000 or > 768_000
            || format.Channels is < 1 or > 64
            || format.BlockAlign <= 0
            || format.AverageBytesPerSecond <= 0)
        {
            throw new InvalidDataException("Plik zawiera nieprawidłowe parametry dźwięku.");
        }

        WaveFormat = format;
        _length = inner.Length;
        if (_length < 0)
        {
            throw new InvalidDataException("Dekoder podał nieprawidłową długość pliku.");
        }

        var duration = DurationFromBytes(_length);
        if (duration > MaximumAcceptedDuration)
        {
            throw new InvalidDataException(
                "Plik zawiera nieprawidłowy czas trwania. Odtwarzanie zatrzymano, aby program nie przestał odpowiadać.");
        }

        // A duration error can be smaller than the absolute one-year limit.
        // Reject only an unmistakably impossible ratio so very low bitrate
        // speech and long audio books remain valid.
        try
        {
            if (MediaSourceAccessPolicy.Classify(sourcePath).Kind != MediaSourceAccessKind.NetworkStream)
            {
                var fileLength = new FileInfo(sourcePath).Length;
                if (fileLength > 0 && duration >= TimeSpan.FromHours(1))
                {
                    var estimatedKbps = fileLength * 8d / duration.TotalSeconds / 1000d;
                    if (!double.IsFinite(estimatedKbps) || estimatedKbps < 0.5d)
                    {
                        throw new InvalidDataException(
                            "Plik zawiera niewiarygodny czas trwania w stosunku do rozmiaru. Odtwarzanie zatrzymano, aby program nie przestał odpowiadać.");
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The decoder can still be used when a cloud provider temporarily
            // refuses this supplementary size check.
        }

        _position = Math.Clamp(inner.Position, 0, _length);
    }

    public string SourcePath { get; }

    public TimeSpan CachedCurrentTime => DurationFromBytes(Interlocked.Read(ref _position));

    public bool IsReadStalled(TimeSpan threshold)
    {
        if (Volatile.Read(ref _readInProgress) == 0) return false;
        var started = Interlocked.Read(ref _readStartedTimestamp);
        return started != 0 && Stopwatch.GetElapsedTime(started) >= threshold;
    }

    public override WaveFormat WaveFormat { get; }

    public override long Length => _length;

    public override long Position
    {
        get => Interlocked.Read(ref _position);
        set
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
            if (!Monitor.TryEnter(_operationGate, SeekLockTimeout))
            {
                throw new TimeoutException("Dekoder nie odpowiedział na próbę przewinięcia pliku.");
            }
            try
            {
                var resolved = Math.Clamp(value, 0, _length);
                _inner.Position = resolved;
                Interlocked.Exchange(ref _position, resolved);
            }
            finally
            {
                Monitor.Exit(_operationGate);
                if (Volatile.Read(ref _disposeRequested) != 0) DisposeInner();
            }
        }
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeRequested) != 0, this);
        Monitor.Enter(_operationGate);
        Volatile.Write(ref _readInProgress, 1);
        Interlocked.Exchange(ref _readStartedTimestamp, Stopwatch.GetTimestamp());
        try
        {
            var read = _inner.Read(buffer, offset, count);
            if (read < 0 || read > count)
            {
                throw new InvalidDataException("Dekoder zwrócił nieprawidłową liczbę danych.");
            }
            var next = Math.Min(_length, checked(Interlocked.Read(ref _position) + read));
            Interlocked.Exchange(ref _position, next);
            return read;
        }
        finally
        {
            Interlocked.Exchange(ref _readStartedTimestamp, 0);
            Volatile.Write(ref _readInProgress, 0);
            Monitor.Exit(_operationGate);
            if (Volatile.Read(ref _disposeRequested) != 0) DisposeInner();
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var basis = origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => Position,
            SeekOrigin.End => Length,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        Position = checked(basis + offset);
        return Position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing && Monitor.TryEnter(_operationGate, DisposeLockTimeout))
        {
            try
            {
                DisposeInner();
            }
            finally
            {
                Monitor.Exit(_operationGate);
            }
        }
        base.Dispose(disposing);
    }

    private void DisposeInner()
    {
        if (Interlocked.Exchange(ref _innerDisposed, 1) == 0) _inner.Dispose();
    }

    private TimeSpan DurationFromBytes(long bytes)
    {
        if (bytes <= 0) return TimeSpan.Zero;
        var seconds = bytes / (double)WaveFormat.AverageBytesPerSecond;
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > TimeSpan.MaxValue.TotalSeconds)
        {
            throw new InvalidDataException("Dekoder podał nieprawidłowy czas trwania pliku.");
        }
        return TimeSpan.FromSeconds(seconds);
    }
}

/// <summary>
/// Watches the complete sample-provider chain, including tempo processing.
/// This catches a stall above the source reader as well as inside it.
/// </summary>
public sealed class DecoderReadMonitorSampleProvider(
    ISampleProvider inner,
    string? sourcePath = null) : ISampleProvider
{
    private long _readStartedTimestamp;
    private int _readInProgress;
    private int _invalidSampleWarningLogged;

    public WaveFormat WaveFormat { get; } = inner.WaveFormat;

    public bool IsReadStalled(TimeSpan threshold)
    {
        if (Volatile.Read(ref _readInProgress) == 0) return false;
        var started = Interlocked.Read(ref _readStartedTimestamp);
        return started != 0 && Stopwatch.GetElapsedTime(started) >= threshold;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Volatile.Write(ref _readInProgress, 1);
        Interlocked.Exchange(ref _readStartedTimestamp, Stopwatch.GetTimestamp());
        try
        {
            var read = inner.Read(buffer, offset, count);
            if (read < 0 || read > count)
            {
                throw new InvalidDataException("Dekoder zwrócił nieprawidłową liczbę próbek.");
            }
            if (read % WaveFormat.Channels != 0)
            {
                throw new InvalidDataException("Dekoder zwrócił niepełną ramkę wielokanałową.");
            }
            var replacedInvalidSamples = false;
            for (var index = offset; index < offset + read; index++)
            {
                if (float.IsFinite(buffer[index])) continue;
                buffer[index] = 0f;
                replacedInvalidSamples = true;
            }
            if (replacedInvalidSamples
                && Interlocked.Exchange(ref _invalidSampleWarningLogged, 1) == 0)
            {
                DiagnosticLog.Warning(
                    "decoder-samples",
                    string.IsNullOrWhiteSpace(sourcePath)
                        ? "Dekoder zwrócił nieprawidłowe próbki; zastąpiono je ciszą."
                        : $"Dekoder zwrócił nieprawidłowe próbki; zastąpiono je ciszą: {sourcePath}.");
            }
            return read;
        }
        finally
        {
            Interlocked.Exchange(ref _readStartedTimestamp, 0);
            Volatile.Write(ref _readInProgress, 0);
        }
    }
}
