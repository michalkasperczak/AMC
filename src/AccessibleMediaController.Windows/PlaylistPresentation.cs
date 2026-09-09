using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

internal static class PlaylistPresentation
{
    public static string BuildQueueLabel(IReadOnlyCollection<MediaItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var label = BuildLabel("Kolejka", items.Count, items.Count, items);
        var playNextCount = items.Count(item => item.IsPlayNext);
        return playNextCount == 0
            ? label
            : $"{label}, jako następne {FormatItemCount(playNextCount)}";
    }

    public static string BuildLabel(
        string name,
        int storedItemCount,
        int availableItemCount,
        IReadOnlyCollection<MediaItem> availableItems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(availableItems);

        var availability = availableItemCount == storedItemCount
            ? FormatItemCount(storedItemCount)
            : $"dostępne {availableItemCount} z {storedItemCount}";
        if (storedItemCount == 0) return $"{name}, {availability}";

        var finiteItems = availableItems
            .Where(item => item.Kind != MediaItemKind.Station)
            .ToArray();
        if (finiteItems.Length == 0 && availableItems.Count > 0)
            return $"{name}, {availability}, transmisje na żywo";

        var knownDurations = finiteItems
            .Where(item => item.Duration > TimeSpan.Zero)
            .Select(item => item.Duration)
            .ToArray();
        if (knownDurations.Length == 0)
            return $"{name}, {availability}, łączny czas nieznany";

        var duration = SumDurations(knownDurations);
        var durationLabel = FormatDurationWords(duration);
        return knownDurations.Length == finiteItems.Length
            ? $"{name}, {availability}, łączny czas {durationLabel}"
            : $"{name}, {availability}, znany czas {durationLabel}, część bez danych";
    }

    private static TimeSpan SumDurations(IEnumerable<TimeSpan> durations)
    {
        var ticks = 0L;
        foreach (var duration in durations)
        {
            ticks = duration.Ticks > long.MaxValue - ticks
                ? long.MaxValue
                : ticks + duration.Ticks;
        }
        return TimeSpan.FromTicks(ticks);
    }

    private static string FormatDurationWords(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours} godz. {duration.Minutes} min"
            : $"{(int)duration.TotalMinutes} min {duration.Seconds} s";

    private static string FormatItemCount(int count)
    {
        if (count == 1) return "1 element";
        var lastTwoDigits = count % 100;
        var lastDigit = count % 10;
        return lastDigit is >= 2 and <= 4 && lastTwoDigits is not (>= 12 and <= 14)
            ? $"{count} elementy"
            : $"{count} elementów";
    }
}
