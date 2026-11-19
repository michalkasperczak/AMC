namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Składa gotowy tekst bieżącego odtwarzania dla paska stanu i NVDA+End.
/// Dane audio są podawane przed nazwą materiału; brak danych zachowuje samą nazwę.
/// </summary>
internal static class LitePlaybackStatusText
{
    internal static string Format(string title, params string?[] details)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(details);

        var parts = details
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Append(title)
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(", ", parts);
    }
}
