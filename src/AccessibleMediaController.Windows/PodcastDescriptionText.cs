namespace AccessibleMediaController.Windows;

internal static class PodcastDescriptionText
{
    private const int MaximumInitialFocusNameLength = 500;

    public static string Compose(string description, string details)
    {
        var normalizedDescription = description.Trim();
        var normalizedDetails = details.Trim();

        if (normalizedDescription.Length == 0) return normalizedDetails;
        if (normalizedDetails.Length == 0) return normalizedDescription;

        // The description deliberately comes first. A screen reader entering the
        // read-only text field therefore starts with the content the user asked
        // for, while the structured metadata remains available below it.
        return normalizedDescription
            + Environment.NewLine
            + Environment.NewLine
            + normalizedDetails;
    }

    public static string InitialFocusName(string description)
    {
        var normalized = string.Join(' ', description
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length <= MaximumInitialFocusNameLength) return normalized;

        var sentenceEnd = normalized
            .Take(MaximumInitialFocusNameLength)
            .Select((character, index) => (character, index))
            .Where(entry => entry.character is '.' or '!' or '?')
            .Select(entry => entry.index + 1)
            .LastOrDefault();
        if (sentenceEnd >= 40) return normalized[..sentenceEnd];

        var wordEnd = normalized.LastIndexOf(' ', MaximumInitialFocusNameLength - 1);
        if (wordEnd < 1) wordEnd = MaximumInitialFocusNameLength;
        return normalized[..wordEnd].TrimEnd() + "…";
    }
}
