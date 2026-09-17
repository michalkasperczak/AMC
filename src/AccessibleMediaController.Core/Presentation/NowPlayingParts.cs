namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// JEDNO miejsce skladania tekstu "co teraz leci" dla czytnika ekranu.
/// Zgloszenie Michala 17.09.2026: odtwarzacz radia ma mowic dokladnie tyle,
/// ile mowi sesja WiiM - stacja, utwor, wykonawca - bez powtorzen i bez
/// doklejania slowa "Odtwarzacz", nazwy sesji ani stanu odtwarzania.
///
/// Regula nie moze istniec w kopiach (patrz MediaItemFormatter): korzystaja
/// z niej nazwa przycisku odtwarzania (radio i WiiM) oraz skrot czytajacy
/// biezacy material przez wtyczke NVDA.
/// </summary>
public static class NowPlayingParts
{
    /// <summary>
    /// Sklada podane czlony w kolejnosci wejsciowej, pomijajac puste i takie,
    /// ktore juz sa na liscie (porownanie bez wielkosci liter).
    /// </summary>
    public static IReadOnlyList<string> Combine(params string?[] values)
    {
        var parts = new List<string>(values.Length);
        foreach (var value in values) Add(parts, value);
        return parts;
    }

    public static void Add(List<string> parts, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var text = value.Trim();
        if (parts.Any(existing => SameValue(existing, text))) return;
        parts.Add(text);
    }

    public static bool SameValue(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first)
        && !string.IsNullOrWhiteSpace(second)
        && string.Equals(first.Trim(), second.Trim(), StringComparison.CurrentCultureIgnoreCase);

    public static string Join(params string?[] values) => string.Join(", ", Combine(values));
}
