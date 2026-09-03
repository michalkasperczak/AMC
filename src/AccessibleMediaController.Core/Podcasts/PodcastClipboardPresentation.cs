namespace AccessibleMediaController.Core.Podcasts;

public sealed record PodcastClipboardEntry(
    string Title,
    string? Description,
    string? PublicPageUrl,
    string? DirectUrl);

/// <summary>
/// Keeps the human-facing podcast page separate from the technical feed or
/// enclosure URL copied by the explicit Ctrl+Shift+C command.
/// </summary>
public static class PodcastClipboardPresentation
{
    public static string FormatPublicDetails(IEnumerable<PodcastClipboardEntry> source) =>
        string.Join(
            Environment.NewLine + Environment.NewLine,
            source.Select(FormatPublicDetails)
                .Where(text => text.Length > 0));

    public static string FormatDirectUrls(IEnumerable<PodcastClipboardEntry> source) =>
        string.Join(
            Environment.NewLine,
            source.Select(entry => Normalize(entry.DirectUrl))
                .Where(value => value.Length > 0));

    private static string FormatPublicDetails(PodcastClipboardEntry entry)
    {
        var lines = new List<string>();
        AddIfPresent(lines, Normalize(entry.Title));
        AddIfPresent(lines, NormalizeMultiline(entry.Description));
        AddIfPresent(lines, Normalize(entry.PublicPageUrl));
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddIfPresent(ICollection<string> target, string value)
    {
        if (value.Length > 0) target.Add(value);
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static string NormalizeMultiline(string? value) => string.Join(
        Environment.NewLine,
        (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0));
}
