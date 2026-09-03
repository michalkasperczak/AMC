namespace AccessibleMediaController.Windows;

internal static class PodcastDescriptionText
{
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
}
