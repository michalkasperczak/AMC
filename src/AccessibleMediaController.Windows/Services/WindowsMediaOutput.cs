using System.Windows;
using System.Windows.Media;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

public sealed class MediaDurationAvailableEventArgs(MediaItem item, TimeSpan duration) : EventArgs
{
    public MediaItem Item { get; } = item;
    public TimeSpan Duration { get; } = duration;
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
/// Windows implementation of the platform-neutral media-output boundary.
/// System.Windows.Media.MediaPlayer uses the codecs and shared audio path
/// available in Windows, so the first prototype does not install a codec pack
/// and does not take exclusive control away from a screen reader.
/// </summary>
public sealed class WindowsMediaOutput : IMediaOutput, IDisposable
{
    private readonly MediaPlayer _player = new();
    private MediaItem? _currentItem;
    private TimeSpan _pendingPosition;
    private bool _isOpen;
    private bool _playWhenOpened;
    private bool _disposed;

    public WindowsMediaOutput()
    {
        _player.MediaOpened += Player_MediaOpened;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
    }

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;

    public TimeSpan Position => _isOpen ? _player.Position : _pendingPosition;

    public void Play(MediaItem item, TimeSpan position, int volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(item.Source))
        {
            throw new InvalidOperationException("Element nie zawiera lokalnego źródła dźwięku.");
        }

        _player.Volume = Math.Clamp(volume, 0, 100) / 100d;
        _pendingPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        _playWhenOpened = true;

        if (_isOpen
            && _currentItem is not null
            && string.Equals(_currentItem.Source, item.Source, StringComparison.OrdinalIgnoreCase))
        {
            _player.Position = _pendingPosition;
            _player.Play();
            return;
        }

        _isOpen = false;
        _currentItem = item;
        _player.Open(new Uri(item.Source, UriKind.Absolute));
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _playWhenOpened = false;
        if (_isOpen)
        {
            _pendingPosition = _player.Position;
            _player.Pause();
        }
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pendingPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (_isOpen) _player.Position = _pendingPosition;
    }

    public void SetVolume(int volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _player.Volume = Math.Clamp(volume, 0, 100) / 100d;
    }

    private void Player_MediaOpened(object? sender, EventArgs e)
    {
        _isOpen = true;
        _player.Position = _pendingPosition;
        if (_currentItem is not null && _player.NaturalDuration.HasTimeSpan)
        {
            DurationAvailable?.Invoke(
                this,
                new MediaDurationAvailableEventArgs(_currentItem, _player.NaturalDuration.TimeSpan));
        }
        if (_playWhenOpened) _player.Play();
    }

    private void Player_MediaEnded(object? sender, EventArgs e)
    {
        _playWhenOpened = false;
        _pendingPosition = TimeSpan.Zero;
        if (_currentItem is not null)
        {
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(_currentItem));
        }
    }

    private void Player_MediaFailed(object? sender, ExceptionEventArgs e)
    {
        _isOpen = false;
        _playWhenOpened = false;
        _pendingPosition = TimeSpan.Zero;
        PlaybackFailed?.Invoke(
            this,
            new MediaOutputFailedEventArgs(
                _currentItem,
                e.ErrorException?.Message ?? "Nieznany błąd odtwarzania"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.MediaOpened -= Player_MediaOpened;
        _player.MediaEnded -= Player_MediaEnded;
        _player.MediaFailed -= Player_MediaFailed;
        _player.Close();
    }
}
