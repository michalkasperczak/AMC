namespace AccessibleMediaController.Core.Tidal;

/// <summary>Czas utworu przeliczony z migawki windowsowej sesji multimediow.</summary>
public readonly record struct ExternalPlaybackTimeResult(
    TimeSpan Position,
    TimeSpan Duration,
    bool HasPosition);

/// <summary>
/// Przeliczanie czasu utworu granego przez OBCY program (np. oryginalny TIDAL).
///
/// Windows nie podaje zywego licznika. Podaje MIGAWKE: polozenie plus znacznik
/// czasu, kiedy je zmierzono. Kto o tym nie wie, czyta czas, ktory stoi w
/// miejscu albo pokazuje zero.
///
/// Rachunek jest tutaj, w czesci wspolnej, a nie przy kodzie Windowsa, zeby dal
/// sie sprawdzic testami bez zywej sesji multimediow.
/// </summary>
public static class ExternalPlaybackTime
{
    /// <summary>
    /// Migawka starsza niz to znaczy "stan nieaktualny", a nie "utwor gra od
    /// godziny" - wtedy nie doliczamy nic.
    /// </summary>
    private static readonly TimeSpan MaksWiekMigawki = TimeSpan.FromMinutes(10);

    public static ExternalPlaybackTimeResult Compute(
        TimeSpan position,
        TimeSpan endTime,
        TimeSpan startTime,
        DateTimeOffset lastUpdated,
        bool isPlaying,
        DateTimeOffset now)
    {
        var duration = endTime;

        // Czesc programow liczy polozenie od StartTime, nie od zera.
        if (startTime > TimeSpan.Zero)
        {
            if (position >= startTime) position -= startTime;
            if (duration > startTime) duration -= startTime;
        }

        // Doliczenie czasu od migawki - tylko gdy utwor faktycznie gra.
        if (isPlaying && lastUpdated != default)
        {
            var odMigawki = now - lastUpdated;
            if (odMigawki > TimeSpan.Zero && odMigawki < MaksWiekMigawki)
                position += odMigawki;
        }

        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        if (duration > TimeSpan.Zero && position > duration) position = duration;

        // Zero BEZ znacznika czasu znaczy "nie wystawiam polozenia". To nie to
        // samo co poczatek utworu, wiec rozdzielamy - inaczej czytnik mowilby
        // "zero sekund" tam, gdzie prawda jest "nie wiadomo".
        var hasPosition = lastUpdated != default || position > TimeSpan.Zero;

        return new ExternalPlaybackTimeResult(position, duration, hasPosition);
    }
}
