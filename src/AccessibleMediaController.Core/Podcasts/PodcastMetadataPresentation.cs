namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Produces concise user-facing podcast metadata without changing the source
/// values retained from RSS or Atom.
/// </summary>
public static class PodcastMetadataPresentation
{
    public static string FormatAuthor(string? author)
    {
        var normalized = NormalizeWhitespace(author);
        if (normalized.Length == 0) return string.Empty;

        var index = 0;
        var removedMarker = false;
        while (index < normalized.Length)
        {
            if (char.IsWhiteSpace(normalized[index]))
            {
                index++;
                continue;
            }

            if (IsCopyrightMarker(normalized[index]))
            {
                removedMarker = true;
                index++;
                continue;
            }

            if (TrySkipParenthesizedMarker(normalized, ref index))
            {
                removedMarker = true;
                continue;
            }

            if (removedMarker && IsMarkerSeparator(normalized[index]))
            {
                index++;
                continue;
            }

            break;
        }

        return removedMarker
            ? normalized[index..].Trim()
            : normalized;
    }

    private static string NormalizeWhitespace(string? value) =>
        string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool IsCopyrightMarker(char value) => value is '©' or '℗' or '®' or '™';

    private static bool IsMarkerSeparator(char value) =>
        value is '&' or '+' or '/' or '\\' or '|' or ',' or ';' or ':' or '-' or '–' or '—' or '·' or '•';

    private static bool TrySkipParenthesizedMarker(string value, ref int index)
    {
        if (index + 2 >= value.Length || value[index] != '(' || value[index + 2] != ')')
            return false;

        var marker = char.ToUpperInvariant(value[index + 1]);
        if (marker is not ('C' or 'P' or 'R')) return false;
        index += 3;
        return true;
    }
}
