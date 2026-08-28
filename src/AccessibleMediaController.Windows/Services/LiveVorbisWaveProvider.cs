using System.IO;
using NAudio.Wave;
using NVorbis;

namespace AccessibleMediaController.Windows.Services;

/// <summary>Sequential Ogg/Vorbis decoder for a live, non-seekable stream.</summary>
internal sealed class LiveVorbisWaveProvider : IWaveProvider, IDisposable
{
    private readonly VorbisReader _reader;
    private float[] _samples = [];
    private int _disposed;

    public LiveVorbisWaveProvider(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            _reader = new VorbisReader(stream, true) { ClipSamples = false };
            if (_reader.Channels <= 0 || _reader.SampleRate <= 0)
            {
                throw new InvalidDataException("Strumień OGG nie zawiera prawidłowego dźwięku Vorbis.");
            }
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_reader.SampleRate, _reader.Channels);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public WaveFormat WaveFormat { get; }

    public int? BitrateKbps => _reader.NominalBitrate > 0
        ? Math.Max(1, _reader.NominalBitrate / 1000)
        : null;

    public int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Nieprawidłowy zakres bufora.");

        var sampleValues = count / sizeof(float);
        sampleValues -= sampleValues % WaveFormat.Channels;
        if (sampleValues <= 0) return 0;
        if (_samples.Length < sampleValues) _samples = new float[sampleValues];
        var read = _reader.ReadSamples(_samples, 0, sampleValues);
        read -= read % WaveFormat.Channels;
        if (read <= 0) return 0;
        Buffer.BlockCopy(_samples, 0, buffer, offset, read * sizeof(float));
        return read * sizeof(float);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _reader.Dispose();
    }
}
