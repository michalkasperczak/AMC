namespace AccessibleMediaController.Core.Sessions;

public sealed class DemoMediaSession
{
    private int _currentIndex;

    public DemoMediaSession(string id, string displayName, IEnumerable<MediaItem> items)
    {
        Id = id;
        DisplayName = displayName;
        Items = items.ToList();
        if (Items.Count == 0) throw new ArgumentException("Sesja demonstracyjna wymaga elementów.", nameof(items));
    }

    public string Id { get; }
    public string DisplayName { get; }
    public List<MediaItem> Items { get; }
    public MediaItem CurrentItem => Items[_currentIndex];
    public bool IsPlaying { get; private set; }
    public int Volume { get; private set; } = 35;
    public TimeSpan Position { get; private set; } = TimeSpan.FromSeconds(83);

    public void TogglePlayback() => IsPlaying = !IsPlaying;

    public bool SelectItem(MediaItem item)
    {
        var index = Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0) return false;
        if (index != _currentIndex) Position = TimeSpan.Zero;
        _currentIndex = index;
        return true;
    }

    public bool Play(MediaItem item)
    {
        if (!SelectItem(item)) return false;
        IsPlaying = true;
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
        Position = TimeSpan.Zero;
        IsPlaying = true;
        return true;
    }

    public void Move(int direction)
    {
        _currentIndex = (_currentIndex + direction + Items.Count) % Items.Count;
        Position = TimeSpan.Zero;
    }

    public void Seek(TimeSpan delta)
    {
        var next = Position + delta;
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (CurrentItem.Duration > TimeSpan.Zero && next > CurrentItem.Duration) next = CurrentItem.Duration;
        Position = next;
    }

    public void SetPosition(TimeSpan position)
    {
        Position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
    }

    public void ChangeVolume(int delta) => Volume = Math.Clamp(Volume + delta, 0, 100);

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
