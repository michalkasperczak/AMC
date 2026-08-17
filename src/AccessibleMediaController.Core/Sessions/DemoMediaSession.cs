using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Core.Sessions;

public sealed class DemoMediaSession
{
    private static readonly double[] PlaybackRates = [0.50d, 0.75d, 1.00d, 1.25d, 1.50d, 1.75d, 2.00d];
    private int _currentIndex;
    private TimeSpan _position;
    private readonly IMediaOutput? _output;

    public DemoMediaSession(
        string id,
        string displayName,
        IEnumerable<MediaItem> items,
        IMediaOutput? output = null)
    {
        Id = id;
        DisplayName = displayName;
        Items = items.ToList();
        if (Items.Count == 0) throw new ArgumentException("Sesja demonstracyjna wymaga elementów.", nameof(items));
        _output = output;
        _position = output is null ? TimeSpan.FromSeconds(83) : TimeSpan.Zero;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public List<MediaItem> Items { get; }
    public MediaItem CurrentItem => Items[_currentIndex];
    public bool IsPlaying { get; private set; }
    public int Volume { get; private set; } = 35;
    public double PlaybackRate { get; private set; } = 1d;
    public bool SupportsPlaybackRate => _output?.SupportsPlaybackRate == true;
    public TimeSpan Position => _output?.Position ?? _position;

    public void TogglePlayback()
    {
        if (IsPlaying)
        {
            _position = Position;
            IsPlaying = false;
            _output?.Pause();
            return;
        }

        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
    }

    public bool SelectItem(MediaItem item)
    {
        var index = Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0) return false;
        if (index != _currentIndex) _position = TimeSpan.Zero;
        _currentIndex = index;
        return true;
    }

    public bool Play(MediaItem item)
    {
        if (!SelectItem(item)) return false;
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
        return true;
    }

    public bool Activate(MediaItem item)
    {
        var index = Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0) return false;

        if (index == _currentIndex)
        {
            TogglePlayback();
            return true;
        }

        _currentIndex = index;
        _position = TimeSpan.Zero;
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
        return true;
    }

    public void Move(int direction)
    {
        _currentIndex = (_currentIndex + direction + Items.Count) % Items.Count;
        _position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan delta)
    {
        var next = Position + delta;
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (CurrentItem.Duration > TimeSpan.Zero && next > CurrentItem.Duration) next = CurrentItem.Duration;
        _position = next;
        _output?.Seek(next);
    }

    public void SetPosition(TimeSpan position)
    {
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        _output?.Seek(_position);
    }

    public void ChangeVolume(int delta)
    {
        Volume = Math.Clamp(Volume + delta, 0, 100);
        _output?.SetVolume(Volume);
    }

    public void SetVolume(int volume)
    {
        Volume = Math.Clamp(volume, 0, 100);
        _output?.SetVolume(Volume);
    }

    public bool ChangePlaybackRate(int direction)
    {
        if (!SupportsPlaybackRate || direction == 0) return false;
        var currentIndex = Array.FindIndex(
            PlaybackRates,
            rate => Math.Abs(rate - PlaybackRate) < 0.001d);
        if (currentIndex < 0)
        {
            currentIndex = Array.FindLastIndex(PlaybackRates, rate => rate < PlaybackRate);
        }
        var nextIndex = Math.Clamp(currentIndex + Math.Sign(direction), 0, PlaybackRates.Length - 1);
        return SetPlaybackRate(PlaybackRates[nextIndex]);
    }

    public bool SetPlaybackRate(double playbackRate)
    {
        if (!SupportsPlaybackRate) return false;
        var resolved = PlaybackRates.MinBy(rate => Math.Abs(rate - playbackRate));
        PlaybackRate = resolved;
        _output!.SetPlaybackRate(resolved);
        return true;
    }

    public void AddItems(IEnumerable<MediaItem> items)
    {
        var knownSources = Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .Select(item => item.Source!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item.Source is { Length: > 0 } source && !knownSources.Add(source)) continue;
            Items.Add(item);
        }
    }

    public void MarkPlaybackEnded()
    {
        IsPlaying = false;
        _position = TimeSpan.Zero;
        _output?.Seek(TimeSpan.Zero);
    }

    public void MarkPlaybackFailed()
    {
        IsPlaying = false;
        _position = TimeSpan.Zero;
        _output?.Seek(TimeSpan.Zero);
    }

    public bool ToggleFavorite(MediaItem item)
    {
        item.IsFavorite = !item.IsFavorite;
        return item.IsFavorite;
    }

    public bool ToggleLibrary(MediaItem item)
    {
        item.IsInLibrary = !item.IsInLibrary;
        return item.IsInLibrary;
    }

    public bool ToggleQueue(MediaItem item)
    {
        item.IsInQueue = !item.IsInQueue;
        return item.IsInQueue;
    }

    public bool TogglePlayNext(MediaItem item)
    {
        item.IsPlayNext = !item.IsPlayNext;
        return item.IsPlayNext;
    }
}
