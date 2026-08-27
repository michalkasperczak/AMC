using System.IO;
using NAudio.Wave;
using NVorbis;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Reads Ogg/Vorbis files whose first audio page can retain the absolute
/// granule position of a longer live stream. NVorbis exposes that absolute
/// value as the file duration and, with sample clipping enabled, can continue
/// decoding after the real end of a recorded fragment. This wrapper discovers
/// the first decoded sample, presents a zero-based timeline and stops at the
/// normalized end without modifying or converting the source file.
/// </summary>
public sealed class NormalizedVorbisWaveReader : WaveStream
{
    private const int MaximumProbeSeconds = 2;
    private const int MaximumProbeSampleValues = 1_048_576;
    private const int ProbeChunkSampleValues = 8_192;
    private static readonly TimeSpan MaximumAcceptedDuration = TimeSpan.FromDays(365);

    private readonly object _gate = new();
    private readonly VorbisReader _reader;
    private readonly float[] _prefetchedSamples;
    private float[] _readBuffer = [];
    private readonly long _length;
    private readonly long _totalSampleFrames;
    private int _prefetchedSampleOffset;
    private readonly int _prefetchedSampleCount;
    private long _logicalSampleFramePosition;
    private bool _disposed;

    public NormalizedVorbisWaveReader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _reader = new VorbisReader(path)
        {
            // A fragment taken from a continuous live stream may begin at a
            // very large granule. Clipping against that absolute value causes
            // NVorbis to spin after the fragment's real end.
            ClipSamples = false
        };

        try
        {
            if (_reader.Channels <= 0 || _reader.SampleRate <= 0)
            {
                throw new InvalidDataException("Plik OGG nie zawiera prawidłowego strumienia dźwięku.");
            }

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                _reader.SampleRate,
                _reader.Channels);

            var maximumProbeValues = checked(
                Math.Min(
                    MaximumProbeSampleValues,
                    _reader.SampleRate * _reader.Channels * MaximumProbeSeconds));
            maximumProbeValues -= maximumProbeValues % _reader.Channels;
            _prefetchedSamples = new float[maximumProbeValues];

            long decodedFrames = 0;
            long sampleOrigin = 0;
            var prefetchedValues = 0;
            while (prefetchedValues < maximumProbeValues)
            {
                var requestedValues = Math.Min(
                    ProbeChunkSampleValues - ProbeChunkSampleValues % _reader.Channels,
                    maximumProbeValues - prefetchedValues);
                if (requestedValues <= 0) break;

                var readValues = _reader.ReadSamples(
                    _prefetchedSamples,
                    prefetchedValues,
                    requestedValues);
                if (readValues <= 0) break;

                prefetchedValues += readValues;
                decodedFrames += readValues / _reader.Channels;

                var candidateOrigin = _reader.SamplePosition - decodedFrames;
                if (candidateOrigin > 0)
                {
                    sampleOrigin = candidateOrigin;
                    break;
                }
            }

            SampleOrigin = sampleOrigin;
            _prefetchedSampleCount = prefetchedValues;

            var normalizedTotalFrames = _reader.TotalSamples - SampleOrigin;
            var prefetchedFrames = prefetchedValues / _reader.Channels;
            if (normalizedTotalFrames < prefetchedFrames)
            {
                normalizedTotalFrames = prefetchedFrames;
            }

            if (normalizedTotalFrames <= 0)
            {
                throw new InvalidDataException("Nie można ustalić długości pliku OGG.");
            }

            var maximumFrames = checked(
                (long)Math.Ceiling(MaximumAcceptedDuration.TotalSeconds * _reader.SampleRate));
            if (normalizedTotalFrames > maximumFrames)
            {
                throw new InvalidDataException(
                    "Plik OGG zawiera nieprawidłową oś czasu. Odtwarzanie zatrzymano, aby program nie przestał odpowiadać.");
            }

            _totalSampleFrames = normalizedTotalFrames;
            _length = checked(_totalSampleFrames * WaveFormat.BlockAlign);
        }
        catch
        {
            _reader.Dispose();
            throw;
        }
    }

    public long SampleOrigin { get; }

    public bool HasNormalizedTimeline => SampleOrigin > 0;

    public override WaveFormat WaveFormat { get; }

    public override long Length => _length;

    public override long Position
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return checked(_logicalSampleFramePosition * WaveFormat.BlockAlign);
            }
        }
        set
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var alignedBytes = Math.Clamp(value, 0, Length);
                var targetFrame = alignedBytes / WaveFormat.BlockAlign;
                _reader.SeekTo(
                    checked(SampleOrigin + targetFrame),
                    SeekOrigin.Begin);
                _prefetchedSampleOffset = _prefetchedSampleCount;
                _logicalSampleFramePosition = targetFrame;
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count) throw new ArgumentException("Bufor jest zbyt mały.", nameof(count));

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var requestedFrames = count / WaveFormat.BlockAlign;
            var remainingFrames = _totalSampleFrames - _logicalSampleFramePosition;
            var framesToRead = (int)Math.Min(requestedFrames, Math.Max(0, remainingFrames));
            if (framesToRead <= 0) return 0;

            var requestedSampleValues = checked(framesToRead * WaveFormat.Channels);
            var copiedSampleValues = CopyPrefetchedSamples(
                buffer,
                offset,
                requestedSampleValues);

            var remainingSampleValues = requestedSampleValues - copiedSampleValues;
            var decodedSampleValues = 0;
            if (remainingSampleValues > 0)
            {
                EnsureReadBuffer(remainingSampleValues);
                decodedSampleValues = _reader.ReadSamples(
                    _readBuffer,
                    0,
                    remainingSampleValues);
                decodedSampleValues -= decodedSampleValues % WaveFormat.Channels;
                if (decodedSampleValues > 0)
                {
                    Buffer.BlockCopy(
                        _readBuffer,
                        0,
                        buffer,
                        offset + copiedSampleValues * sizeof(float),
                        decodedSampleValues * sizeof(float));
                }
            }

            var totalSampleValues = copiedSampleValues + decodedSampleValues;
            var framesRead = totalSampleValues / WaveFormat.Channels;
            _logicalSampleFramePosition += framesRead;
            return checked(framesRead * WaveFormat.BlockAlign);
        }
    }

    private int CopyPrefetchedSamples(
        byte[] destination,
        int destinationOffset,
        int maximumSampleValues)
    {
        var available = _prefetchedSampleCount - _prefetchedSampleOffset;
        var toCopy = Math.Min(maximumSampleValues, Math.Max(0, available));
        toCopy -= toCopy % WaveFormat.Channels;
        if (toCopy <= 0) return 0;

        Buffer.BlockCopy(
            _prefetchedSamples,
            _prefetchedSampleOffset * sizeof(float),
            destination,
            destinationOffset,
            toCopy * sizeof(float));
        _prefetchedSampleOffset += toCopy;
        return toCopy;
    }

    private void EnsureReadBuffer(int sampleValues)
    {
        if (_readBuffer.Length >= sampleValues) return;
        _readBuffer = new float[sampleValues];
    }

    protected override void Dispose(bool disposing)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                base.Dispose(disposing);
                return;
            }

            _disposed = true;
            if (disposing) _reader.Dispose();
        }
        base.Dispose(disposing);
    }
}
