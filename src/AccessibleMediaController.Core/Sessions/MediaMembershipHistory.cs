namespace AccessibleMediaController.Core.Sessions;

public readonly record struct MediaMembershipState(
    bool IsFavorite,
    bool IsInLibrary,
    bool IsInQueue,
    bool IsPlayNext)
{
    public static MediaMembershipState From(MediaItem item) =>
        new(item.IsFavorite, item.IsInLibrary, item.IsInQueue, item.IsPlayNext);

    public void ApplyTo(MediaItem item)
    {
        item.IsFavorite = IsFavorite;
        item.IsInLibrary = IsInLibrary;
        item.IsInQueue = IsInQueue;
        item.IsPlayNext = IsPlayNext;
    }
}

public sealed record MediaMembershipUndoItem(
    MediaItem Item,
    MediaMembershipState PreviousState);

public readonly record struct MediaMembershipOrderPosition(
    string ItemId,
    int Index);

public sealed record MediaMembershipOrderSnapshot(
    string CollectionId,
    IReadOnlyList<MediaMembershipOrderPosition> Positions);

public sealed record MediaMembershipUndo(
    string SessionId,
    IReadOnlyList<MediaMembershipUndoItem> Items,
    string Announcement,
    long Sequence = 0,
    MediaMembershipOrderSnapshot? OrderSnapshot = null)
{
    public MediaItem Item => Items[0].Item;
}

public sealed class MediaMembershipHistory
{
    private readonly Stack<MediaMembershipUndo> _entries = [];

    public int Count => _entries.Count;

    public void Clear() => _entries.Clear();

    public void Record(
        string sessionId,
        MediaItem item,
        MediaMembershipState previousState,
        string announcement,
        long sequence = 0,
        MediaMembershipOrderSnapshot? orderSnapshot = null) =>
        RecordBatch(sessionId, [(item, previousState)], announcement, sequence, orderSnapshot);

    public void RecordBatch(
        string sessionId,
        IEnumerable<(MediaItem Item, MediaMembershipState PreviousState)> items,
        string announcement,
        long sequence = 0,
        MediaMembershipOrderSnapshot? orderSnapshot = null)
    {
        var changedItems = items
            .Where(entry => MediaMembershipState.From(entry.Item) != entry.PreviousState)
            .Select(entry => new MediaMembershipUndoItem(entry.Item, entry.PreviousState))
            .ToArray();
        if (changedItems.Length == 0) return;
        _entries.Push(new MediaMembershipUndo(
            sessionId,
            changedItems,
            announcement,
            sequence,
            orderSnapshot));
    }

    public MediaMembershipUndo? Peek() => _entries.Count == 0 ? null : _entries.Peek();

    public MediaMembershipUndo? Undo(
        Func<string, string, MediaItem?>? currentItemResolver = null)
    {
        if (_entries.Count == 0) return null;
        var entry = _entries.Pop();
        foreach (var item in entry.Items)
        {
            item.PreviousState.ApplyTo(item.Item);
            var currentItem = currentItemResolver?.Invoke(entry.SessionId, item.Item.Id);
            if (currentItem is not null && !ReferenceEquals(currentItem, item.Item))
            {
                item.PreviousState.ApplyTo(currentItem);
            }
        }
        return entry;
    }
}
