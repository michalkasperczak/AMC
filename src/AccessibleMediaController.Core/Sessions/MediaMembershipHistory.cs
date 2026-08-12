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

public sealed record MediaMembershipUndo(
    string SessionId,
    MediaItem Item,
    MediaMembershipState PreviousState,
    string Announcement);

public sealed class MediaMembershipHistory
{
    private readonly Stack<MediaMembershipUndo> _entries = [];

    public int Count => _entries.Count;

    public void Clear() => _entries.Clear();

    public void Record(
        string sessionId,
        MediaItem item,
        MediaMembershipState previousState,
        string announcement)
    {
        if (MediaMembershipState.From(item) == previousState) return;
        _entries.Push(new MediaMembershipUndo(sessionId, item, previousState, announcement));
    }

    public MediaMembershipUndo? Undo()
    {
        if (_entries.Count == 0) return null;
        var entry = _entries.Pop();
        entry.PreviousState.ApplyTo(entry.Item);
        return entry;
    }
}
