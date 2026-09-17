using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Wybor rozpoznanego utworu, ktorym mozna zastapic BRAKUJACY tytul z transmisji.
///
/// ZGLOSZENIE Michala 17.09.2026: sluchal Radia Poznan, ktore nie wysyla tytulu
/// w transmisji, wiec skrot "co teraz leci" mowil sama nazwe stacji - mimo ze
/// program SAM rozpoznal utwor i mial go w historii rozpoznan (Ctrl+Alt+S).
/// Pytanie Michala bylo trafne: skoro juz to wiemy, trzeba to powiedziec.
///
/// Zasady, ktore musza tu zostac:
/// - tytul z SAMEJ transmisji ma pierwszenstwo. Rozpoznanie jest zastepnikiem
///   na wypadek milczacej stacji, nie zamiennikiem prawdziwych metadanych;
/// - wpis musi byc z TEJ stacji. Rozpoznanie z innej stacji (albo z nagrania w
///   tle) nie moze wyciec do biezacego odsluchu;
/// - wpis musi byc SWIEZY. Utwor rozpoznany kwadrans temu juz nie leci, a
///   podanie go jako biezacego to klamstwo, ktorego czytnik nie odsieje.
/// </summary>
public static class RecognizedTrackLookup
{
    /// <summary>
    /// Jak dlugo rozpoznanie wolno podawac jako "to leci teraz".
    /// Rozpoznawanie chodzi co minute, wiec kilka minut daje zapas na dluzszy
    /// utwor i na chwilowe niepowodzenia, a nie siega poprzedniej audycji.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(6);

    /// <summary>
    /// Najswiezszy rozpoznany utwor tej stacji, jeszcze wazny w chwili
    /// <paramref name="nowUtc"/>. Zwraca null, gdy nie ma czego podac.
    /// </summary>
    public static RadioRecognizedTrackSettings? Find(
        IEnumerable<RadioRecognizedTrackSettings>? entries,
        string? stationId,
        string? stationName,
        DateTime nowUtc)
    {
        if (entries is null) return null;
        // Bez wskazania stacji nie ma jak sprawdzic, czy wpis dotyczy tego, co
        // slychac - a zgadywanie tutaj oznaczaloby czytanie cudzego utworu.
        if (string.IsNullOrWhiteSpace(stationId) && string.IsNullOrWhiteSpace(stationName)) return null;

        var najstarszyDopuszczalny = nowUtc.Subtract(MaxAge).Ticks;
        return entries
            .Where(entry => entry is not null)
            .Where(entry => entry.RecognizedUtcTicks >= najstarszyDopuszczalny
                && entry.RecognizedUtcTicks <= nowUtc.AddMinutes(1).Ticks)
            .Where(entry => SameStation(entry, stationId, stationName))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Title)
                || !string.IsNullOrWhiteSpace(entry.Artist))
            .OrderByDescending(entry => entry.RecognizedUtcTicks)
            .FirstOrDefault();
    }

    /// <summary>
    /// Tytul i wykonawca rozpoznanego utworu jako jeden czlon wypowiedzi, w tej
    /// samej kolejnosci co w sesji WiiM: najpierw utwor, potem wykonawca.
    /// </summary>
    public static string Describe(RadioRecognizedTrackSettings entry) =>
        NowPlayingParts.Join(entry.Title, entry.Artist);

    private static bool SameStation(
        RadioRecognizedTrackSettings entry,
        string? stationId,
        string? stationName)
    {
        // Identyfikator jest pewny, wiec gdy oba wpisy go maja, decyduje on sam.
        // Nazwa jest zapasem dla stacji dodanych z adresu, ktore identyfikatora
        // w historii nie zapisaly.
        if (!string.IsNullOrWhiteSpace(stationId) && !string.IsNullOrWhiteSpace(entry.StationId))
            return string.Equals(entry.StationId.Trim(), stationId.Trim(), StringComparison.Ordinal);
        return NowPlayingParts.SameValue(entry.StationName, stationName);
    }
}
