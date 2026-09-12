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
    public static string GetNavigationText(MediaItem item, IEnumerable<MediaItemField> fieldOrder)
    {
        var fields = fieldOrder.ToArray();
        var identifyingFields = fields
            .Where(field => field is MediaItemField.Title or MediaItemField.Artist)
            .ToArray();
        foreach (var field in identifyingFields.Length > 0 ? identifyingFields : fields)
        {
            var value = FieldValue(item, field)?.Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return item.Title;
    }

    // Podcasty i odcinki czyta sie ZAWSZE tytulem naprzod, nawet gdy uzytkownik
    // ustawil kolejnosc "wykonawca, tytul". Przy odcinku wykonawca to nazwa kanalu
    // albo audycji, wiec kolejnosc z ustawien dawalaby "nazwa kanalu, tytul odcinka" -
    // przy dziesiatkach odcinkow tego samego kanalu czytnik ekranu powtarzalby na
    // wstepie to samo, a rozstrzygajacy tytul konczylby sie dopiero na koncu.
    // Reguly trzymamy tu, w jednym miejscu, bo korzysta z niej i lista glowna,
    // i nawigacja z wtyczki NVDA - rozjezdzaly sie, gdy kazda miala wlasna kopie.
    public static IReadOnlyList<MediaItemField> OrderFieldsForItem(
        MediaItem item,
        IEnumerable<MediaItemField> fieldOrder)
    {
        var fields = fieldOrder as IReadOnlyList<MediaItemField> ?? fieldOrder.ToArray();
        if (item.Kind is not (MediaItemKind.Podcast or MediaItemKind.Episode)) return fields;
        return new[] { MediaItemField.Title }
            .Concat(fields.Where(field => field != MediaItemField.Title))
            .ToArray();
    }

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
