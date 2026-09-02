using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Core.Sessions;

public sealed record RemovedMediaItem(MediaItem Item, int Index, int? PlaybackContextIndex = null);
public sealed record MediaReplacementResult(bool CurrentItemRemoved, MediaItem? SelectedSuccessor);

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
    private readonly List<string> _queueItemIds = [];
    private readonly List<string> _queueNavigationItemIds = [];
    private bool _queueNavigationActive;
    private bool _playbackContextIsQueue;
    private List<string> _playbackContextItemIds;
    private readonly Func<MediaItem, double?> _playbackRateOverride;
    private readonly Func<MediaItem, int?> _volumeOverride;
    private readonly MediaItem _emptyItem;
    private bool _hasCurrentItem;

    public DemoMediaSession(
        string id,
        string displayName,
        IEnumerable<MediaItem> items,
        IMediaOutput? output = null,
        Func<MediaItem, bool>? rememberPosition = null,
        Func<MediaItem, double?>? playbackRateOverride = null,
        Func<MediaItem, int?>? volumeOverride = null)
    {
        Id = id;
        DisplayName = displayName;
        Items = items.ToList();
        _hasCurrentItem = Items.Count > 0;
        _playbackContextItemIds = Items.Select(item => item.Id).ToList();
        _emptyItem = new MediaItem
        {
            Id = $"{id}-empty",
            Title = $"Brak elementów w sesji {displayName}"
        };
        _output = output;
        _rememberPosition = rememberPosition ?? (_ => true);
        _playbackRateOverride = playbackRateOverride ?? (_ => null);
        _volumeOverride = volumeOverride ?? (_ => null);
        _position = output is null ? TimeSpan.FromSeconds(83) : TimeSpan.Zero;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public List<MediaItem> Items { get; }
    public bool HasItems => Items.Count > 0;
    public bool HasCurrentItem => HasItems && _hasCurrentItem;
    public MediaItem CurrentItem => HasCurrentItem ? Items[_currentIndex] : _emptyItem;
    public bool IsPlaying { get; private set; }
    public int Volume { get; private set; } = 35;
    public bool IsSessionMuted { get; private set; }
    public bool IsGloballyMuted { get; private set; }
    public bool IsMuted => IsSessionMuted || IsGloballyMuted;
    public double PlaybackRate { get; private set; } = 1d;
    public double DefaultPlaybackRate { get; private set; } = 1d;
    public bool SupportsPlaybackRate => _output?.SupportsPlaybackRate == true;
    public PlaybackAudioProcessingCapabilities AudioProcessingCapabilities =>
        (_output as IPlaybackAudioProcessingOutput)?.AudioProcessingCapabilities
        ?? PlaybackAudioProcessingCapabilities.None;
    public TimeSpan Position => _output is not null
        && string.Equals(_output.LoadedItemId, CurrentItem.Id, StringComparison.Ordinal)
            ? _output.Position
            : _position;
    public IReadOnlyDictionary<string, TimeSpan> RememberedPositions => _rememberedPositions;
    public IReadOnlyList<string> PlaybackContextItemIds => _playbackContextItemIds;
    public IReadOnlyList<string> QueueNavigationItemIds => _queueNavigationItemIds;
    public bool QueueNavigationActive => _queueNavigationActive;
    public IReadOnlyList<string> QueueItemIds
    {
        get
        {
            SynchronizeQueueOrder();
            return _queueItemIds.ToArray();
        }
    }

    public void SetQueueOrder(IEnumerable<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        _queueItemIds.Clear();
        _queueItemIds.AddRange(itemIds
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .Distinct(StringComparer.Ordinal));
        SynchronizeQueueOrder();
    }

    public void TogglePlayback()
    {
        if (!HasCurrentItem) return;
        if (IsPlaying)
        {
            _position = Position;
            RememberCurrentPosition();
            IsPlaying = false;
            _output?.Pause();
            return;
        }

        if ((_playbackContextIsQueue
                || (_queueNavigationActive
                    && _queueNavigationItemIds.Contains(CurrentItem.Id, StringComparer.Ordinal)))
            && (CurrentItem.IsInQueue || CurrentItem.IsPlayNext))
        {
            _playedQueueItemIds.Add(CurrentItem.Id);
            ConsumeQueueItem(CurrentItem);
        }
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, EffectiveVolume, PlaybackRate);
    }

    public void StopPlayback()
    {
        if (!HasCurrentItem) return;
        RememberCurrentPosition();
        IsPlaying = false;
        _output?.Stop();
    }

    public bool RestartPlaybackOutput(TimeSpan? positionOverride = null)
    {
        if (!HasCurrentItem || !IsPlaying || _output is null) return false;
        _position = positionOverride ?? Position;
        _output.Stop();
        _output.Play(CurrentItem, _position, EffectiveVolume, PlaybackRate);
        return true;
    }

    public bool SelectItem(MediaItem item)
    {
        var index = Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0) return false;
        if (!_hasCurrentItem || index != _currentIndex)
        {
            RememberCurrentPosition();
            _position = RememberedPosition(item);
        }
        _currentIndex = index;
        _hasCurrentItem = true;
        return true;
    }

    public bool Play(MediaItem item)
    {
        ResetQueueDiversion();
        RestoreExplicitQueueNavigation();
        if (!SelectItem(item)) return false;
        if (_playbackContextIsQueue) ConsumeQueueItem(CurrentItem);
        ApplyPlaybackRateForItem(CurrentItem);
        ApplyVolumeForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, EffectiveVolume, PlaybackRate);
        return true;
    }

    public bool Activate(MediaItem item)
    {
        var index = Items.FindIndex(candidate => candidate.Id == item.Id);
        if (index < 0) return false;

        if (_hasCurrentItem && index == _currentIndex)
        {
            if (_playbackContextIsQueue)
            {
                RestoreExplicitQueueNavigation();
                ConsumeQueueItem(CurrentItem);
            }
            TogglePlayback();
            return true;
        }

        ResetQueueDiversion();
        RestoreExplicitQueueNavigation();
        RememberCurrentPosition();
        _currentIndex = index;
        _hasCurrentItem = true;
        _position = RememberedPosition(item);
        if (_playbackContextIsQueue) ConsumeQueueItem(CurrentItem);
        ApplyPlaybackRateForItem(CurrentItem);
        ApplyVolumeForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, EffectiveVolume, PlaybackRate);
        return true;
    }

    public void Move(int direction)
    {
        if (!HasItems) return;
        RememberCurrentPosition();
        _currentIndex = _hasCurrentItem
            ? (_currentIndex + direction + Items.Count) % Items.Count
            : direction < 0 ? Items.Count - 1 : 0;
        _hasCurrentItem = true;
        _position = RememberedPosition(CurrentItem);
    }

    public bool PlayRelative(int direction)
    {
        if (!HasCurrentItem || direction == 0) return false;
        var usesQueueNavigation = _queueNavigationActive
            && _queueNavigationItemIds.Contains(CurrentItem.Id, StringComparer.Ordinal);
        var navigationItems = usesQueueNavigation
            ? _queueNavigationItemIds
            : _playbackContextItemIds;
        var contextIndex = navigationItems.FindIndex(itemId =>
            string.Equals(itemId, CurrentItem.Id, StringComparison.Ordinal));
        if (contextIndex < 0) return false;
        var targetContextIndex = contextIndex + Math.Sign(direction);
        if (targetContextIndex < 0 || targetContextIndex >= navigationItems.Count) return false;
        var targetId = navigationItems[targetContextIndex];
        var nextIndex = Items.FindIndex(item => string.Equals(item.Id, targetId, StringComparison.Ordinal));
        if (nextIndex < 0) return false;

        if (!usesQueueNavigation) ResetQueueDiversion();
        RememberCurrentPosition();
        if (usesQueueNavigation) ConsumeQueueItem(CurrentItem);
        _currentIndex = nextIndex;
        _position = RememberedPosition(CurrentItem);
        if (usesQueueNavigation)
        {
            _playedQueueItemIds.Add(CurrentItem.Id);
            ConsumeQueueItem(CurrentItem);
        }
        ApplyPlaybackRateForItem(CurrentItem);
        ApplyVolumeForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, EffectiveVolume, PlaybackRate);
        return true;
    }

    public void Seek(TimeSpan delta)
    {
        if (!HasCurrentItem) return;
        var next = Position + delta;
        if (next < TimeSpan.Zero) next = TimeSpan.Zero;
        if (CurrentItem.Duration > TimeSpan.Zero && next > CurrentItem.Duration) next = CurrentItem.Duration;
        _position = next;
        StoreRememberedPosition(CurrentItem, next);
        _output?.Seek(next);
    }

    public void SetPosition(TimeSpan position)
    {
        if (!HasCurrentItem) return;
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
        if (!HasCurrentItem) return;
        var position = Position;
        _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        StoreRememberedPosition(CurrentItem, _position);
    }

    public void ChangeVolume(int delta)
    {
        Volume = Math.Clamp(Volume + delta, 0, 100);
        IsSessionMuted = false;
        ApplyEffectiveVolume();
    }

    public void SetVolume(int volume)
    {
        Volume = Math.Clamp(volume, 0, 100);
        ApplyEffectiveVolume();
    }

    public bool ToggleMute()
    {
        IsSessionMuted = !IsSessionMuted;
        ApplyEffectiveVolume();
        return IsSessionMuted;
    }

    public void SetGlobalMute(bool muted)
    {
        IsGloballyMuted = muted;
        ApplyEffectiveVolume();
    }

    private int EffectiveVolume => IsMuted ? 0 : Volume;

    private void ApplyEffectiveVolume() => _output?.SetVolume(EffectiveVolume);

    public void ConfigureAudioProcessing(PlaybackAudioSettings settings)
    {
        if (_output is IPlaybackAudioProcessingOutput output)
            output.ConfigureAudioProcessing(settings);
    }

    private void ApplyVolumeForItem(MediaItem item)
    {
        if (_volumeOverride(item) is not { } volume) return;
        Volume = Math.Clamp(volume, 0, 100);
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

    public void ApplyPlaybackRateForCurrentItem()
    {
        if (HasCurrentItem) ApplyPlaybackRateForItem(CurrentItem);
    }

    public void SetPlaybackContext(IEnumerable<string> itemIds, bool isQueueContext = false)
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
        _playbackContextIsQueue = isQueueContext;
        ResetQueueDiversion();
        RestoreExplicitQueueNavigation();
        if (isQueueContext
            && IsPlaying
            && normalized.Contains(CurrentItem.Id, StringComparer.Ordinal))
        {
            ConsumeQueueItem(CurrentItem);
        }
    }

    public void AddItems(IEnumerable<MediaItem> items)
    {
        var selectFirstAddedItem = Items.Count == 0;
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
        if (selectFirstAddedItem && Items.Count > 0)
        {
            _currentIndex = 0;
            _hasCurrentItem = true;
            _position = RememberedPosition(CurrentItem);
        }
    }

    public MediaReplacementResult ReplaceItems(IEnumerable<MediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var replacement = items
            .DistinctBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        var catalogPreviouslyHadItems = HasItems;
        var previousCurrentId = HasCurrentItem ? CurrentItem.Id : null;
        var previousPlaybackContext = _playbackContextItemIds.ToList();
        var previousQueueNavigation = _queueNavigationItemIds.ToList();
        var previousQueueNavigationActive = _queueNavigationActive
            && previousCurrentId is not null
            && previousQueueNavigation.Contains(previousCurrentId, StringComparer.Ordinal);
        var previousResumeAfterQueueIndex = _resumeAfterQueueIndex;
        var contextWasWholeCatalog = PlaybackContextIsWholeCatalog();
        if (HasCurrentItem) RememberCurrentPosition();
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
        if (_playbackContextItemIds.Count == 0 && previousCurrentId is null && !catalogPreviouslyHadItems)
        {
            _playbackContextItemIds.AddRange(Items.Select(item => item.Id));
        }
        _queueNavigationItemIds.RemoveAll(itemId => !knownIds.Contains(itemId));
        if (_queueNavigationItemIds.Count == 0) _queueNavigationActive = false;
        if (!HasItems)
        {
            if (previousWasPlaying) _output?.Stop();
            IsPlaying = false;
            _currentIndex = 0;
            _hasCurrentItem = false;
            _position = TimeSpan.Zero;
            ResetQueueDiversion();
            return new(previousCurrentId is not null, null);
        }

        var restoredIndex = previousCurrentId is null
            ? -1
            : Items.FindIndex(item => string.Equals(item.Id, previousCurrentId, StringComparison.Ordinal));
        if (restoredIndex >= 0)
        {
            _currentIndex = restoredIndex;
            _hasCurrentItem = true;
            _position = RememberedPosition(CurrentItem);
            return new(false, null);
        }

        if (previousCurrentId is null && !catalogPreviouslyHadItems)
        {
            _currentIndex = 0;
            _hasCurrentItem = true;
            _position = RememberedPosition(CurrentItem);
            return new(false, null);
        }

        if (previousCurrentId is not null) _output?.Stop();
        IsPlaying = false;
        var successor = FindSuccessorAfterCurrentDisappeared(
            previousCurrentId!,
            previousPlaybackContext,
            previousQueueNavigation,
            previousQueueNavigationActive,
            previousResumeAfterQueueIndex,
            knownIds,
            out var successorUsesQueueNavigation);

        if (successor is not null)
        {
            _currentIndex = Items.FindIndex(item =>
                string.Equals(item.Id, successor.Id, StringComparison.Ordinal));
            _hasCurrentItem = true;
            _position = RememberedPosition(CurrentItem);
            ApplyPlaybackRateForItem(CurrentItem);
            // A Queue successor remains in the waiting set until playback
            // actually starts through TogglePlayback.
            if (!successorUsesQueueNavigation && previousQueueNavigationActive)
            {
                ResetQueueDiversion();
            }
            return new(true, successor);
        }

        _currentIndex = 0;
        _hasCurrentItem = false;
        _position = TimeSpan.Zero;
        ResetQueueDiversion();
        return new(previousCurrentId is not null, null);
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
        var currentId = HasCurrentItem ? CurrentItem.Id : null;
        var currentRemoved = currentId is not null && ids.Contains(currentId);
        var previousPlaybackContext = _playbackContextItemIds.ToList();
        var previousQueueNavigation = _queueNavigationItemIds.ToList();
        var previousQueueNavigationActive = _queueNavigationActive
            && currentId is not null
            && previousQueueNavigation.Contains(currentId, StringComparer.Ordinal);
        var previousResumeAfterQueueIndex = _resumeAfterQueueIndex;
        if (currentRemoved)
        {
            IsPlaying = false;
            _output?.Stop();
        }

        foreach (var entry in removed.OrderByDescending(entry => entry.Index))
        {
            Items.RemoveAt(entry.Index);
            _playbackContextItemIds.RemoveAll(itemId =>
                string.Equals(itemId, entry.Item.Id, StringComparison.Ordinal));
            _playedQueueItemIds.Remove(entry.Item.Id);
            _queueNavigationItemIds.RemoveAll(itemId =>
                string.Equals(itemId, entry.Item.Id, StringComparison.Ordinal));
        }
        if (_queueNavigationItemIds.Count == 0) _queueNavigationActive = false;

        if (!currentRemoved)
        {
            _currentIndex = currentId is null
                ? 0
                : Items.FindIndex(item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
            if (_currentIndex < 0) _currentIndex = 0;
            _hasCurrentItem = currentId is not null;
            _position = _hasCurrentItem ? RememberedPosition(CurrentItem) : TimeSpan.Zero;
            return removed;
        }

        var knownIds = Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var successor = FindSuccessorAfterCurrentDisappeared(
            currentId!,
            previousPlaybackContext,
            previousQueueNavigation,
            previousQueueNavigationActive,
            previousResumeAfterQueueIndex,
            knownIds,
            out var successorUsesQueueNavigation);
        if (successor is not null)
        {
            _currentIndex = Items.FindIndex(item =>
                string.Equals(item.Id, successor.Id, StringComparison.Ordinal));
            _hasCurrentItem = true;
            _position = RememberedPosition(CurrentItem);
            ApplyPlaybackRateForItem(CurrentItem);
            if (!successorUsesQueueNavigation && previousQueueNavigationActive)
            {
                ResetQueueDiversion();
            }
        }
        else
        {
            _currentIndex = 0;
            _hasCurrentItem = false;
            _position = TimeSpan.Zero;
            ResetQueueDiversion();
        }
        return removed;
    }

    public void RestoreItems(IEnumerable<RemovedMediaItem> removedItems)
    {
        var entries = removedItems
            .Where(entry => Items.All(item => !string.Equals(item.Id, entry.Item.Id, StringComparison.Ordinal)))
            .OrderBy(entry => entry.Index)
            .ToArray();
        if (entries.Length == 0) return;

        var currentId = HasCurrentItem ? CurrentItem.Id : null;
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
        _currentIndex = currentId is null
            ? 0
            : Items.FindIndex(item => string.Equals(item.Id, currentId, StringComparison.Ordinal));
        if (_currentIndex < 0) _currentIndex = 0;
        if (currentId is null && Items.Count > 0) _hasCurrentItem = true;
    }

    public void MarkPlaybackEnded()
    {
        if (!HasCurrentItem) return;
        IsPlaying = false;
        _position = TimeSpan.Zero;
        StoreRememberedPosition(CurrentItem, TimeSpan.Zero);
        ResetQueueDiversion();
        _output?.Seek(TimeSpan.Zero);
    }

    public MediaItem? ContinueAfterPlaybackEnded(MediaItem endedItem)
    {
        if (!HasCurrentItem || !string.Equals(CurrentItem.Id, endedItem.Id, StringComparison.Ordinal)) return null;

        IsPlaying = false;
        _position = TimeSpan.Zero;
        StoreRememberedPosition(endedItem, TimeSpan.Zero);
        endedItem.IsInQueue = false;
        endedItem.IsPlayNext = false;

        SynchronizeQueueOrder();
        var orderedQueue = _queueItemIds
            .Select(itemId => Items.FirstOrDefault(item =>
                string.Equals(item.Id, itemId, StringComparison.Ordinal)))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderByDescending(item => item.IsPlayNext)
            .ToArray();
        var next = orderedQueue.FirstOrDefault(item =>
            item.Id != endedItem.Id && item.IsPlayNext);
        if (next is not null)
        {
            next.IsPlayNext = false;
        }
        else
        {
            next = orderedQueue.FirstOrDefault(item =>
                item.Id != endedItem.Id && item.IsInQueue);
            if (next is not null) next.IsInQueue = false;
        }

        if (next is not null)
        {
            UpdateQueueNavigation(endedItem, orderedQueue);
            if (!_playbackContextIsQueue)
            {
                var contextIndex = _playbackContextItemIds.FindIndex(itemId =>
                    string.Equals(itemId, endedItem.Id, StringComparison.Ordinal));
                _resumeAfterQueueIndex ??= contextIndex < 0
                    ? _playbackContextItemIds.Count
                    : Math.Min(contextIndex + 1, _playbackContextItemIds.Count);
            }
            _playedQueueItemIds.Add(next.Id);
            ConsumeQueueItem(next);
        }
        else if (_playbackContextIsQueue && _queueNavigationActive)
        {
            _output?.Seek(TimeSpan.Zero);
            return null;
        }
        else if (_resumeAfterQueueIndex is int resumeIndex)
        {
            next = FindNextContextItem(resumeIndex, endedItem.Id);
            _resumeAfterQueueIndex = null;
            _queueNavigationActive = false;
            _queueNavigationItemIds.Clear();
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
        _position = RememberedPosition(CurrentItem);
        ApplyPlaybackRateForItem(CurrentItem);
        ApplyVolumeForItem(CurrentItem);
        IsPlaying = true;
        _output?.Play(CurrentItem, _position, EffectiveVolume, PlaybackRate);
        return CurrentItem;
    }

    public void MarkPlaybackFailed()
    {
        if (!HasCurrentItem) return;
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
        SynchronizeQueueOrder();
        return add;
    }

    public bool TogglePlayNext(MediaItem item)
    {
        item.IsPlayNext = !item.IsPlayNext;
        SynchronizeQueueOrder();
        return item.IsPlayNext;
    }

    private void SynchronizeQueueOrder()
    {
        var queuedItems = Items
            .Where(item => item.IsInQueue || item.IsPlayNext)
            .ToArray();
        var queuedIds = queuedItems.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var normalized = _queueItemIds
            .Where(queuedIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var normalizedIds = normalized.ToHashSet(StringComparer.Ordinal);
        normalized.AddRange(queuedItems
            .Where(item => normalizedIds.Add(item.Id))
            .Select(item => item.Id));
        _queueItemIds.Clear();
        _queueItemIds.AddRange(normalized);
    }

    private void RestoreExplicitQueueNavigation()
    {
        if (!_playbackContextIsQueue) return;
        _queueNavigationItemIds.Clear();
        _queueNavigationItemIds.AddRange(_playbackContextItemIds);
        _queueNavigationActive = _queueNavigationItemIds.Count > 0;
    }

    private void UpdateQueueNavigation(MediaItem endedItem, IReadOnlyList<MediaItem> orderedQueue)
    {
        var suffix = orderedQueue
            .Select(item => item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var endedIndex = _queueNavigationItemIds.FindIndex(itemId =>
            string.Equals(itemId, endedItem.Id, StringComparison.Ordinal));
        var updated = _queueNavigationActive && endedIndex >= 0
            ? _queueNavigationItemIds.Take(endedIndex + 1).ToList()
            : [];
        var known = updated.ToHashSet(StringComparer.Ordinal);
        updated.AddRange(suffix.Where(known.Add));
        _queueNavigationItemIds.Clear();
        _queueNavigationItemIds.AddRange(updated);
        _queueNavigationActive = _queueNavigationItemIds.Count > 0;
    }

    private void ConsumeQueueItem(MediaItem item)
    {
        item.IsInQueue = false;
        item.IsPlayNext = false;
        SynchronizeQueueOrder();
    }

    private void ResetQueueDiversion()
    {
        _resumeAfterQueueIndex = null;
        _playedQueueItemIds.Clear();
        _queueNavigationActive = false;
        _queueNavigationItemIds.Clear();
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

    private MediaItem? FindAvailableSuccessor(
        IReadOnlyList<string> contextItemIds,
        string currentItemId,
        IReadOnlySet<string> knownIds,
        bool skipPlayedQueueItems)
    {
        var currentIndex = contextItemIds
            .Select((itemId, index) => (itemId, index))
            .FirstOrDefault(entry => string.Equals(entry.itemId, currentItemId, StringComparison.Ordinal))
            .index;
        if (currentIndex < 0 || !contextItemIds.Contains(currentItemId, StringComparer.Ordinal)) return null;
        return FindAvailableAtOrAfter(
            contextItemIds,
            currentIndex + 1,
            knownIds,
            skipPlayedQueueItems);
    }

    private MediaItem? FindSuccessorAfterCurrentDisappeared(
        string currentItemId,
        IReadOnlyList<string> previousPlaybackContext,
        IReadOnlyList<string> previousQueueNavigation,
        bool previousQueueNavigationActive,
        int? previousResumeAfterQueueIndex,
        IReadOnlySet<string> knownIds,
        out bool usesQueueNavigation)
    {
        var successor = previousQueueNavigationActive
            ? FindAvailableSuccessor(
                previousQueueNavigation,
                currentItemId,
                knownIds,
                skipPlayedQueueItems: false)
            : null;
        usesQueueNavigation = successor is not null;
        if (successor is not null) return successor;

        if (previousQueueNavigationActive && !_playbackContextIsQueue
            && previousResumeAfterQueueIndex is int resumeIndex)
        {
            return FindAvailableAtOrAfter(
                previousPlaybackContext,
                resumeIndex,
                knownIds,
                skipPlayedQueueItems: true);
        }
        if (!previousQueueNavigationActive)
        {
            return FindAvailableSuccessor(
                previousPlaybackContext,
                currentItemId,
                knownIds,
                skipPlayedQueueItems: true);
        }
        return null;
    }

    private MediaItem? FindAvailableAtOrAfter(
        IReadOnlyList<string> contextItemIds,
        int startIndex,
        IReadOnlySet<string> knownIds,
        bool skipPlayedQueueItems)
    {
        for (var index = Math.Max(0, startIndex); index < contextItemIds.Count; index++)
        {
            var itemId = contextItemIds[index];
            if (!knownIds.Contains(itemId)
                || (skipPlayedQueueItems && _playedQueueItemIds.Contains(itemId)))
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
