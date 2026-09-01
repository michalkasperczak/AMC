namespace AccessibleMediaController.Core.Playback;

/// <summary>
/// A transient, non-destructive selection of a fragment in one local media item.
/// The source file is never changed by this model.
/// </summary>
public sealed class AudioClipSelection
{
    public string? ItemId { get; private set; }
    public string? SourcePath { get; private set; }
    public TimeSpan? Start { get; private set; }
    public TimeSpan? End { get; private set; }

    public bool IsComplete => Start is not null && End is not null && End > Start;

    public void SetStart(
        string itemId,
        string sourcePath,
        TimeSpan position,
        TimeSpan duration)
    {
        BeginItem(itemId, sourcePath);
        Start = Clamp(position, duration);
        if (End is not null && End <= Start) End = null;
    }

    public bool TrySetEnd(
        string itemId,
        string sourcePath,
        TimeSpan position,
        TimeSpan duration)
    {
        if (!Matches(itemId, sourcePath) || Start is null) return false;
        var candidate = Clamp(position, duration);
        if (candidate <= Start) return false;
        End = candidate;
        return true;
    }

    public bool Matches(string itemId, string sourcePath) =>
        string.Equals(ItemId, itemId, StringComparison.Ordinal)
        && string.Equals(SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase);

    public TimeSpan? FindRelativeBoundary(TimeSpan position, int direction)
    {
        if (direction == 0) throw new ArgumentOutOfRangeException(nameof(direction));
        var boundaries = new[] { Start, End }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        return direction < 0
            ? boundaries.Where(value => value < position).Select(value => (TimeSpan?)value).LastOrDefault()
            : boundaries.Where(value => value > position).Select(value => (TimeSpan?)value).FirstOrDefault();
    }

    public void Clear()
    {
        ItemId = null;
        SourcePath = null;
        Start = null;
        End = null;
    }

    private void BeginItem(string itemId, string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (Matches(itemId, sourcePath)) return;
        ItemId = itemId;
        SourcePath = sourcePath;
        Start = null;
        End = null;
    }

    private static TimeSpan Clamp(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero) return TimeSpan.Zero;
        return duration > TimeSpan.Zero && position > duration ? duration : position;
    }
}
