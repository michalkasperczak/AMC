using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Presentation;

public enum MediaItemField
{
    Title,
    Artist,
    Duration,
    Kind
}

public static class MediaItemFormatter
{
    public static string Format(MediaItem item, IEnumerable<MediaItemField> fieldOrder)
    {
        var values = new List<string>();
        var spokenValues = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var field in fieldOrder)
        {
            var value = FieldValue(item, field)?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && spokenValues.Add(value)) values.Add(value);
        }

        return values.Count > 0 ? string.Join(", ", values) : item.Title;
    }

    public static string GetFieldDisplayName(MediaItemField field) => field switch
    {
        MediaItemField.Title => "Tytuł",
        MediaItemField.Artist => "Wykonawca",
        MediaItemField.Duration => "Czas trwania",
        MediaItemField.Kind => "Typ elementu",
        _ => field.ToString()
    };

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
        return $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }

    private static string? FieldValue(MediaItem item, MediaItemField field) => field switch
    {
        MediaItemField.Title => item.Title,
        MediaItemField.Artist => item.Artist,
        MediaItemField.Duration when item.Duration > TimeSpan.Zero => FormatDuration(item.Duration),
        MediaItemField.Kind => item.KindLabel,
        _ => null
    };
}
