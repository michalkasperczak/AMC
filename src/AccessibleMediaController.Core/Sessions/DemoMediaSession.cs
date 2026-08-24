using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Core.Sessions;

public sealed record RemovedMediaItem(MediaItem Item, int Index, int? PlaybackContextIndex = null);

public sealed class DemoMediaSession
{
    private static readonly double[] PlaybackRates = [0.50d, 0.75d, 1.00d, 1.25d, 1.50d, 1.75d, 2.00d];
    private int _currentIndex;
    private TimeSpan _position;
    private readonly IMediaOutput? _output;
    private readonly Func<MediaItem, bool> _rememberPosition;
    private readonly Dictionary<string, TimeSpan> _rememberedPositions = new(StringComparer.Ordinal);
    private int? _resumeAfterQueueIndex;
    private readonly HashSet<string> _playedQueueItemIds = new(StringComparer.Ordinal);
    private List<string> _playbackContextItemIds;
    private readonly Func<MediaItem, double?> _playbackRateOverride;
    private readonly MediaItem _emptyItem;

    public DemoMediaSession(
        string id,
        string displayName,
        IEnumerable<MediaItem> items,
        IMediaOutput? output = null,
        Func<MediaItem, bool>? rememberPosition = null,
        Func<MediaItem, double?>? playbackRateOverride = null)
    {
        Id = id;
        DisplayName = displayName;
        Items = items.ToList();
        _playbackContextItemIds = Items.Select(item => item.Id).ToList();
        _emptyItem = new MediaItem
        {
            Id = $"{id}-empty",
            Title = $"Brak elementów w sesji {displayName}"
        };
        _output = output;
        _rememberPosition = rememberPosition ?? (_ => true);
        _playbackRateOverride = playbackRateOverride ?? (_ => null);
        _position = output is null ? TimeSpan.FromSeconds(83) : TimeSpan.Zero;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public List<MediaItem> Items { get; }
    public bool HasItems => Items.Count > 0;
    public MediaItem CurrentItem => HasItems ? Items[_currentIndex] : _emptyItem;
    public bool IsPlaying { get; private set; }
    public int Volume { get; private set; } = 35;
    public double PlaybackRate { get; private set; } = 1d;
    public double DefaultPlaybackRate { get; private set; } = 1d;
    public bool SupportsPlaybackRate => _output?.SupportsPlaybackRate == true;
    public TimeSpan Position => _output is not null
        && string.Equals(_output.LoadedItemId, CurrentItem.Id, StringComparison.Ordinal)
            ? _output.Position
            : _position;
    public IReadOnlyDictionary<string, TimeSpan> RememberedPositions => _rememberedPositions;
    public IReadOnlyList<string> PlaybackContextItemIds => _playbackContextItemIds;

    public void TogglePlayback()
    {
        if (!HasItems) return;
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

    public void StopPlayback()
    {
        if (!HasItems) return;
        RememberCurrentPosition();
        IsPlaying = false;
        _output?.Stop();
    }

    public bool SelectItem(MediaItem item)
    {
        var index = Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0) return false;
        if (index != _currentIndex)
        {
            RememberCurrentPosition();
            _position = RememberedPosition(item);
        }
        _currentIndex = index;
        return true;
    }

    public bool Play(MediaItem item)
    {
        ResetQueueDiversion();
        if (!SelectItem(item)) return false;
        ApplyPlaybackRateForItem(CurrentItem);
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
        _position = RememberedPosition(item);
        ApplyPlaybackRateForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
        return true;
    }

    public void Move(int direction)
    {
        if (!HasItems) return;
        RememberCurrentPosition();
        _currentIndex = (_currentIndex + direction + Items.Count) % Items.Count;
        _position = RememberedPosition(CurrentItem);
    }

    public bool PlayRelative(int direction)
    {
        if (!HasItems || direction == 0) return false;
        var contextIndex = _playbackContextItemIds.FindIndex(itemId =>
            string.Equals(itemId, CurrentItem.Id, StringComparison.Ordinal));
        if (contextIndex < 0) return false;
        var targetContextIndex = contextIndex + Math.Sign(direction);
        if (targetContextIndex < 0 || targetContextIndex >= _playbackContextItemIds.Count) return false;
        var targetId = _playbackContextItemIds[targetContextIndex];
        var nextIndex = Items.FindIndex(item => string.Equals(item.Id, targetId, StringComparison.Ordinal));
        if (nextIndex < 0) return false;

        ResetQueueDiversion();
        RememberCurrentPosition();
        _currentIndex = nextIndex;
        _position = RememberedPosition(CurrentItem);
        ApplyPlaybackRateForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, Volume, PlaybackRate);
        return true;
    }

    public void Seek(TimeSpan delta)
    {
        if (!HasItems) return;
        var next = Position + delta;
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (CurrentItem.Duration > TimeSpan.Zero && next > CurrentItem.Duration) next = CurrentItem.Duration;
        _position = next;
        StoreRememberedPosition(CurrentItem, next);
        _output?.Seek(next);
    }

    public void SetPosition(TimeSpan position)
    {
        if (!HasItems) return;
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        StoreRememberedPosition(CurrentItem, _position);
        _output?.Seek(_position);
    }

    public void SetRememberedPosition(string itemId, TimeSpan position)
    {
        if (Items.All(item => !string.Equals(item.Id, itemId, StringComparison.Ordinal))) return;
        var item = Items.FirstOrDefault(candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
        if (item is null) return;
        StoreRememberedPosition(item, position < TimeSpan.Zero ? TimeSpan.Zero : position);
        if (string.Equals(CurrentItem.Id, itemId, StringComparison.Ordinal))
        {
            _position = RememberedPosition(item);
        }
    }

    public void ClearRememberedPosition(string itemId)
    {
        _rememberedPositions.Remove(itemId);
    }

    public void RememberCurrentPosition()
    {
        if (!HasItems) return;
        var position = Position;
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        StoreRememberedPosition(CurrentItem, _position);
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

    public bool SetDefaultPlaybackRate(double playbackRate)
    {
        var resolved = PlaybackRates.MinBy(rate => Math.Abs(rate - playbackRate));
        DefaultPlaybackRate = resolved;
        if (_playbackRateOverride(CurrentItem).HasValue) return true;
        return SetPlaybackRate(resolved);
    }

    public void ApplyPlaybackRateForCurrentItem() => ApplyPlaybackRateForItem(CurrentItem);

    public void SetPlaybackContext(IEnumerable<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var knownIds = Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var normalized = itemIds
            .Where(knownIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (normalized.Count == 0 && HasItems)
        {
            normalized.AddRange(Items.Select(item => item.Id));
        }
        _playbackContextItemIds = normalized;
        ResetQueueDiversion();
    }

    public void AddItems(IEnumerable<MediaItem> items)
    {
        var contextWasWholeCatalog = PlaybackContextIsWholeCatalog();
        var knownSources = Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .Select(item => item.Source!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (item.Source is { Length: > 0 } source && !knownSources.Add(source)) continue;
            Items.Add(item);
            if (contextWasWholeCatalog) _playbackContextItemIds.Add(item.Id);
        }
    }

    public void ReplaceItems(IEnumerable<MediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var replacement = items
            .DistinctBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        var previousCurrentId = HasItems ? CurrentItem.Id : null;
        var contextWasWholeCatalog = PlaybackContextIsWholeCatalog();
        if (HasItems) RememberCurrentPosition();
        var previousWasPlaying = IsPlaying;

        Items.Clear();
        Items.AddRange(replacement);
        var knownIds = Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        _playbackContextItemIds = contextWasWholeCatalog
            ? Items.Select(item => item.Id).ToList()
            : _playbackContextItemIds
                .Where(knownIds.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        if (_playbackContextItemIds.Count == 0)
        {
            _playbackContextItemIds.AddRange(Items.Select(item => item.Id));
        }
        if (!HasItems)
        {
            if (previousWasPlaying) _output?.Stop();
            IsPlaying = false;
            _currentIndex = 0;
            _position = TimeSpan.Zero;
            ResetQueueDiversion();
            return;
        }

        var restoredIndex = previousCurrentId is null
            ? -1
            : Items.FindIndex(item => string.Equals(item.Id, previousCurrentId, StringComparison.Ordinal));
        if (restoredIndex >= 0)
        {
            _currentIndex = restoredIndex;
            _position = RememberedPosition(CurrentItem);
            return;
        }

        if (previousWasPlaying) _output?.Stop();
        IsPlaying = false;
        _currentIndex = 0;
        _position = RememberedPosition(CurrentItem);
        ResetQueueDiversion();
    }

    public IReadOnlyList<RemovedMediaItem> RemoveItems(IEnumerable<string> itemIds)
    {
        var ids = itemIds.ToHashSet(StringComparer.Ordinal);
        var removed = Items
            .Select((item, index) => new RemovedMediaItem(item, index))
            .Where(entry => ids.Contains(entry.Item.Id))
            .Select(entry => entry with
            {
                PlaybackContextIndex = _playbackContextItemIds.FindIndex(itemId =>
                    string.Equals(itemId, entry.Item.Id, StringComparison.Ordinal)) is var contextIndex
                    && contextIndex >= 0
                        ? contextIndex
                        : null
            })
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
            _playbackContextItemIds.RemoveAll(itemId =>
                string.Equals(itemId, entry.Item.Id, StringComparison.Ordinal));
            _playedQueueItemIds.Remove(entry.Item.Id);
        }

        _currentIndex = currentRemoved
            ? Math.Min(oldCurrentIndex, Items.Count - 1)
            : Items.FindIndex(item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
        if (_currentIndex < 0) _currentIndex = 0;
        _position = RememberedPosition(CurrentItem);
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
            if (entry.PlaybackContextIndex is int contextIndex
                && !_playbackContextItemIds.Contains(entry.Item.Id, StringComparer.Ordinal))
            {
                _playbackContextItemIds.Insert(
                    Math.Clamp(contextIndex, 0, _playbackContextItemIds.Count),
                    entry.Item.Id);
            }
        }
        _currentIndex = Items.FindIndex(item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
        if (_currentIndex < 0) _currentIndex = 0;
    }

    public void MarkPlaybackEnded()
    {
        if (!HasItems) return;
        IsPlaying = false;
        _position = TimeSpan.Zero;
        StoreRememberedPosition(CurrentItem, TimeSpan.Zero);
        ResetQueueDiversion();
        _output?.Seek(TimeSpan.Zero);
    }

    public MediaItem? ContinueAfterPlaybackEnded(MediaItem endedItem)
    {
        if (!HasItems || !string.Equals(CurrentItem.Id, endedItem.Id, StringComparison.Ordinal)) return null;

        IsPlaying = false;
        _position = TimeSpan.Zero;
        StoreRememberedPosition(endedItem, TimeSpan.Zero);
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
            var contextIndex = _playbackContextItemIds.FindIndex(itemId =>
                string.Equals(itemId, endedItem.Id, StringComparison.Ordinal));
            _resumeAfterQueueIndex ??= contextIndex < 0
                ? _playbackContextItemIds.Count
                : Math.Min(contextIndex + 1, _playbackContextItemIds.Count);
            _playedQueueItemIds.Add(next.Id);
        }
        else if (_resumeAfterQueueIndex is int resumeIndex)
        {
            next = FindNextContextItem(resumeIndex, endedItem.Id);
            _resumeAfterQueueIndex = null;
        }
        else
        {
            var contextIndex = _playbackContextItemIds.FindIndex(itemId =>
                string.Equals(itemId, endedItem.Id, StringComparison.Ordinal));
            if (contextIndex >= 0)
            {
                next = FindNextContextItem(contextIndex + 1, endedItem.Id);
            }
        }

        if (next is null)
        {
            ResetQueueDiversion();
            _output?.Seek(TimeSpan.Zero);
            return null;
        }

        _currentIndex = Items.FindIndex(item => item.Id == next.Id);
        StoreRememberedPosition(CurrentItem, TimeSpan.Zero);
        ApplyPlaybackRateForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, TimeSpan.Zero, Volume, PlaybackRate);
        return CurrentItem;
    }

    public void MarkPlaybackFailed()
    {
        if (!HasItems) return;
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

    private MediaItem? FindNextContextItem(int startIndex, string excludedItemId)
    {
        for (var index = Math.Max(0, startIndex); index < _playbackContextItemIds.Count; index++)
        {
            var itemId = _playbackContextItemIds[index];
            if (string.Equals(itemId, excludedItemId, StringComparison.Ordinal)
                || _playedQueueItemIds.Contains(itemId))
            {
                continue;
            }
            var item = Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
            if (item is not null) return item;
        }
        return null;
    }

    private bool PlaybackContextIsWholeCatalog()
    {
        if (_playbackContextItemIds.Count != Items.Count) return false;
        var contextIds = _playbackContextItemIds.ToHashSet(StringComparer.Ordinal);
        return Items.All(item => contextIds.Contains(item.Id));
    }

    private void ApplyPlaybackRateForItem(MediaItem item)
    {
        if (!SupportsPlaybackRate) return;
        var preferred = _playbackRateOverride(item) ?? DefaultPlaybackRate;
        SetPlaybackRate(preferred);
    }

    private TimeSpan RememberedPosition(MediaItem item) =>
        _rememberPosition(item)
            ? _rememberedPositions.GetValueOrDefault(item.Id)
            : TimeSpan.Zero;

    private void StoreRememberedPosition(MediaItem item, TimeSpan position)
    {
        if (_rememberPosition(item))
        {
            _rememberedPositions[item.Id] = position;
        }
        else
        {
            _rememberedPositions.Remove(item.Id);
        }
    }
}
