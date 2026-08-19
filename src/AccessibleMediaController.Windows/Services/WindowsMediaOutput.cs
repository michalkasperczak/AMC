using System.IO;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using NAudio.CoreAudioApi;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundTouch.Net.NAudioSupport;

namespace AccessibleMediaController.Windows.Services;

public sealed class MediaDurationAvailableEventArgs(
    MediaItem item,
    TimeSpan duration,
    int sampleRateHz) : EventArgs
{
    public MediaItem Item { get; } = item;
    public TimeSpan Duration { get; } = duration;
    public int SampleRateHz { get; } = sampleRateHz;
}

public sealed class MediaOutputFailedEventArgs(MediaItem? item, string message) : EventArgs
{
    public MediaItem? Item { get; } = item;
    public string Message { get; } = message;
}

public sealed class MediaPlaybackEndedEventArgs(MediaItem item) : EventArgs
{
    public MediaItem Item { get; } = item;
}

/// <summary>
/// Windows output based on NAudio, shared WASAPI and SoundTouch. Shared mode
/// coexists with screen readers. SoundTouch changes tempo independently of
/// pitch, so speech and music do not move to a higher or lower key.
/// </summary>
public sealed class WindowsMediaOutput : IMediaOutput, IDisposable
{
    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private WasapiOut? _outputDevice;
    private WaveStream? _reader;
    private SoundTouchWaveStream? _tempoStream;
    private VolumeSampleProvider? _volumeProvider;
    private MediaItem? _currentItem;
    private TimeSpan _pendingPosition;
    private double _playbackRate = 1d;
    private bool _disposed;

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;

    public bool SupportsPlaybackRate => true;

    public TimeSpan Position
    {
        get
        {
            if (_tempoStream is null) return _pendingPosition;
            var position = _tempoStream.CurrentTime;
            return position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }
    }

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(item.Source))
        {
            throw new InvalidOperationException("Element nie zawiera lokalnego źródła dźwięku.");
        }

        _pendingPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        _playbackRate = Math.Clamp(playbackRate, 0.50d, 2.00d);

        try
        {
            if (_outputDevice is not null
                && _tempoStream is not null
                && _currentItem is not null
                && string.Equals(_currentItem.Source, item.Source, StringComparison.OrdinalIgnoreCase))
            {
                SeekInternal(_pendingPosition);
                SetVolume(volume);
                SetPlaybackRate(_playbackRate);
                _outputDevice.Play();
                return;
            }

            ClosePipeline();
            _currentItem = item;
            var reader = CreateReader(item.Source);
            _reader = reader;
            _tempoStream = new SoundTouchWaveStream(reader)
            {
                Tempo = _playbackRate,
                Pitch = 1d,
                Rate = 1d
            };
            _volumeProvider = new VolumeSampleProvider(_tempoStream.ToSampleProvider());
            _outputDevice = new WasapiOut(AudioClientShareMode.Shared, true, 120);
            _outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            _outputDevice.Init(_volumeProvider.ToWaveProvider());
            SeekInternal(_pendingPosition);
            SetVolume(volume);
            DurationAvailable?.Invoke(
                this,
                new MediaDurationAvailableEventArgs(
                    item,
                    _tempoStream.TotalTime,
                    reader.WaveFormat.SampleRate));
            _outputDevice.Play();
        }
        catch (Exception exception)
        {
            ClosePipeline();
            RaisePlaybackFailed(item, exception.Message);
        }
    }

    private static WaveStream CreateReader(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".oga", StringComparison.OrdinalIgnoreCase)
                ? new VorbisWaveReader(path)
                : new AudioFileReader(path);
    }

    public static bool TryReadMetadata(
        string path,
        out TimeSpan duration,
        out int sampleRateHz)
    {
        duration = TimeSpan.Zero;
        sampleRateHz = 0;
        try
        {
            using var reader = CreateReader(path);
            duration = reader.TotalTime;
            sampleRateHz = reader.WaveFormat.SampleRate;
            return duration > TimeSpan.Zero || sampleRateHz > 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException
                or ArgumentException
                or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_outputDevice is null) return;
        _pendingPosition = Position;
        _outputDevice.Pause();
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pendingPosition = Position;
        ClosePipeline();
        _currentItem = null;
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pendingPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        SeekInternal(_pendingPosition);
    }

    public void SetVolume(int volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_volumeProvider is not null)
        {
            _volumeProvider.Volume = Math.Clamp(volume, 0, 100) / 100f;
        }
    }

    public void SetPlaybackRate(double playbackRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _playbackRate = Math.Clamp(playbackRate, 0.50d, 2.00d);
        if (_tempoStream is null) return;
        _tempoStream.Tempo = _playbackRate;
        _tempoStream.Pitch = 1d;
        _tempoStream.Rate = 1d;
    }

    private void SeekInternal(TimeSpan position)
    {
        if (_tempoStream is null) return;
        var resolved = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (_tempoStream.TotalTime > TimeSpan.Zero && resolved > _tempoStream.TotalTime)
        {
            resolved = _tempoStream.TotalTime;
        }
        _tempoStream.CurrentTime = resolved;
        _pendingPosition = resolved;
    }

    private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed) return;
        if (e.Exception is not null)
        {
            RaisePlaybackFailed(_currentItem, e.Exception.Message);
            return;
        }

        _pendingPosition = TimeSpan.Zero;
        if (_currentItem is { } item)
        {
            RaiseOnCapturedContext(() => PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(item)));
        }
    }

    private void RaisePlaybackFailed(MediaItem? item, string message) =>
        RaiseOnCapturedContext(
            () => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));

    private void RaiseOnCapturedContext(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }
        _synchronizationContext.Post(_ => action(), null);
    }

    private void ClosePipeline()
    {
        if (_outputDevice is not null)
        {
            _outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
            _outputDevice.Stop();
            _outputDevice.Dispose();
        }
        _outputDevice = null;

        // SoundTouchWaveStream owns and disposes the AudioFileReader.
        _tempoStream?.Dispose();
        _tempoStream = null;
        _volumeProvider = null;
        _reader = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClosePipeline();
    }
}
