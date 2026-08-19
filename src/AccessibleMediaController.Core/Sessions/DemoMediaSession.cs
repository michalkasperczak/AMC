using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Core.Sessions;

public sealed record RemovedMediaItem(MediaItem Item, int Index);

public sealed class DemoMediaSession
{
    private static readonly double[] PlaybackRates = [0.50d, 0.75d, 1.00d, 1.25d, 1.50d, 1.75d, 2.00d];
    private int _currentIndex;
    private TimeSpan _position;
    private readonly IMediaOutput? _output;
    private readonly Dictionary<string, TimeSpan> _rememberedPositions = new(StringComparer.Ordinal);
    private int? _resumeAfterQueueIndex;
    private readonly HashSet<string> _playedQueueItemIds = new(StringComparer.Ordinal);

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
    public IReadOnlyDictionary<string, TimeSpan> RememberedPositions => _rememberedPositions;

    public void TogglePlayback()
    {
        if (IsPlaying)
        {
            _position = Position;
            RememberCurrentPosition();
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
        if (index != _currentIndex)
        {
            RememberCurrentPosition();
            _position = _rememberedPositions.GetValueOrDefault(item.Id);
        }
        _currentIndex = index;
        return true;
    }

    public bool Play(MediaItem item)
    {
        ResetQueueDiversion();
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

        ResetQueueDiversion();
        RememberCurrentPosition();
        _currentIndex = index;
        _position = _rememberedPositions.GetValueOrDefault(item.Id);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
        return true;
    }

    public void Move(int direction)
    {
        RememberCurrentPosition();
        _currentIndex = (_currentIndex + direction + Items.Count) % Items.Count;
        _position = _rememberedPositions.GetValueOrDefault(CurrentItem.Id);
    }

    public bool PlayRelative(int direction)
    {
        if (direction == 0) return false;
        var nextIndex = _currentIndex + Math.Sign(direction);
        if (nextIndex < 0 || nextIndex >= Items.Count) return false;

        ResetQueueDiversion();
        RememberCurrentPosition();
        _currentIndex = nextIndex;
        _position = _rememberedPositions.GetValueOrDefault(CurrentItem.Id);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
        return true;
    }

    public void Seek(TimeSpan delta)
    {
        var next = Position + delta;
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (CurrentItem.Duration > TimeSpan.Zero && next > CurrentItem.Duration) next = CurrentItem.Duration;
        _position = next;
        _rememberedPositions[CurrentItem.Id] = next;
        _output?.Seek(next);
    }

    public void SetPosition(TimeSpan position)
    {
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        _rememberedPositions[CurrentItem.Id] = _position;
        _output?.Seek(_position);
    }

    public void SetRememberedPosition(string itemId, TimeSpan position)
    {
        if (Items.All(item => !string.Equals(item.Id, itemId, StringComparison.Ordinal))) return;
        _rememberedPositions[itemId] = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (string.Equals(CurrentItem.Id, itemId, StringComparison.Ordinal))
        {
            _position = _rememberedPositions[itemId];
        }
    }

    public void RememberCurrentPosition()
    {
        var position = Position;
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        _rememberedPositions[CurrentItem.Id] = _position;
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

    public IReadOnlyList<RemovedMediaItem> RemoveItems(IEnumerable<string> itemIds)
    {
        var ids = itemIds.ToHashSet(StringComparer.Ordinal);
        var removed = Items
            .Select((item, index) => new RemovedMediaItem(item, index))
            .Where(entry => ids.Contains(entry.Item.Id))
            .ToArray();
        if (removed.Length == 0 || removed.Length >= Items.Count) return [];

        RememberCurrentPosition();
        var currentId = CurrentItem.Id;
        var currentRemoved = ids.Contains(currentId);
        var oldCurrentIndex = _currentIndex;
        if (currentRemoved && IsPlaying)
        {
            IsPlaying = false;
            _output?.Pause();
        }

        foreach (var entry in removed.OrderByDescending(entry => entry.Index))
        {
            Items.RemoveAt(entry.Index);
            _playedQueueItemIds.Remove(entry.Item.Id);
        }

        _currentIndex = currentRemoved
            ? Math.Min(oldCurrentIndex, Items.Count - 1)
            : Items.FindIndex(item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
        if (_currentIndex < 0) _currentIndex = 0;
        _position = _rememberedPositions.GetValueOrDefault(CurrentItem.Id);
        ResetQueueDiversion();
        return removed;
    }

    public void RestoreItems(IEnumerable<RemovedMediaItem> removedItems)
    {
        var entries = removedItems
            .Where(entry => Items.All(item => !string.Equals(item.Id, entry.Item.Id, StringComparison.Ordinal)))
            .OrderBy(entry => entry.Index)
            .ToArray();
        if (entries.Length == 0) return;

        var currentId = CurrentItem.Id;
        foreach (var entry in entries)
        {
            Items.Insert(Math.Clamp(entry.Index, 0, Items.Count), entry.Item);
        }
        _currentIndex = Items.FindIndex(item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
        if (_currentIndex < 0) _currentIndex = 0;
    }

    public void MarkPlaybackEnded()
    {
        IsPlaying = false;
        _position = TimeSpan.Zero;
        _rememberedPositions[CurrentItem.Id] = TimeSpan.Zero;
        ResetQueueDiversion();
        _output?.Seek(TimeSpan.Zero);
    }

    public MediaItem? ContinueAfterPlaybackEnded(MediaItem endedItem)
    {
        if (!string.Equals(CurrentItem.Id, endedItem.Id, StringComparison.Ordinal)) return null;

        IsPlaying = false;
        _position = TimeSpan.Zero;
        _rememberedPositions[endedItem.Id] = TimeSpan.Zero;
        endedItem.IsInQueue = false;
        endedItem.IsPlayNext = false;

        var next = Items.FirstOrDefault(item =>
            item.Id != endedItem.Id && item.IsPlayNext);
        if (next is not null)
        {
            next.IsPlayNext = false;
        }
        else
        {
            next = Items.FirstOrDefault(item =>
                item.Id != endedItem.Id && item.IsInQueue);
            if (next is not null) next.IsInQueue = false;
        }

        if (next is not null)
        {
            _resumeAfterQueueIndex ??= Math.Min(_currentIndex + 1, Items.Count);
            _playedQueueItemIds.Add(next.Id);
        }
        else if (_resumeAfterQueueIndex is int resumeIndex)
        {
            next = Items
                .Skip(resumeIndex)
                .FirstOrDefault(item =>
                    item.Id != endedItem.Id && !_playedQueueItemIds.Contains(item.Id));
            _resumeAfterQueueIndex = null;
        }
        else if (_currentIndex + 1 < Items.Count)
        {
            next = Items
                .Skip(_currentIndex + 1)
                .FirstOrDefault(item => !_playedQueueItemIds.Contains(item.Id));
        }

        if (next is null)
        {
            ResetQueueDiversion();
            _output?.Seek(TimeSpan.Zero);
            return null;
        }

        _currentIndex = Items.FindIndex(item => item.Id == next.Id);
        _rememberedPositions[CurrentItem.Id] = TimeSpan.Zero;
        IsPlaying = true;
        _output?.Play(CurrentItem, TimeSpan.Zero, Volume, PlaybackRate);
        return CurrentItem;
    }

    public void MarkPlaybackFailed()
    {
        IsPlaying = false;
        _position = TimeSpan.Zero;
        ResetQueueDiversion();
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
        var add = !item.IsInQueue && !item.IsPlayNext;
        item.IsInQueue = add;
        if (!add) item.IsPlayNext = false;
        return add;
    }

    public bool TogglePlayNext(MediaItem item)
    {
        item.IsPlayNext = !item.IsPlayNext;
        return item.IsPlayNext;
    }

    private void ResetQueueDiversion()
    {
        _resumeAfterQueueIndex = null;
        _playedQueueItemIds.Clear();
    }
}
