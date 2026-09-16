using AccessibleMediaController.Core.Tidal;

/// <summary>
/// Testy przeliczania czasu utworu podawanego przez windowsowa sesje
/// multimediow (SMTC).
///
/// Zgloszenie uzytkownika 16.09.2026: "Ctrl+Shift+E R i T nie czyta czasu
/// utworu, czyta go jako zero".
///
/// Windows podaje polozenie jako MIGAWKE ze znacznikiem czasu i nie odlicza
/// dalej. Tu sprawdzamy sam rachunek, bez dotykania Windowsa - inaczej nie
/// dalby sie przetestowac.
/// </summary>
internal static class ExternalPlaybackTimeTests
{
    public static void Run()
    {
        var znacznik = new DateTimeOffset(2026, 9, 16, 22, 0, 0, TimeSpan.Zero);
        var teraz = znacznik.AddSeconds(7);

        // Grajacy utwor: do migawki trzeba doliczyc czas, ktory od niej uplynal.
        var stan = ExternalPlaybackTime.Compute(
            position: TimeSpan.FromSeconds(30),
            endTime: TimeSpan.FromMinutes(4),
            startTime: TimeSpan.Zero,
            lastUpdated: znacznik,
            isPlaying: true,
            now: teraz);
        Assert(stan.HasPosition, "grajacy utwor ma znane polozenie");
        Assert(stan.Position == TimeSpan.FromSeconds(37),
            $"doliczone 7 s od migawki, a nie {stan.Position}");
        Assert(stan.Duration == TimeSpan.FromMinutes(4), "czas calkowity bez zmian");

        // Pauza: NIE doliczamy, bo utwor stoi.
        var pauza = ExternalPlaybackTime.Compute(
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(4), TimeSpan.Zero,
            znacznik, isPlaying: false, now: teraz);
        Assert(pauza.Position == TimeSpan.FromSeconds(30),
            $"na pauzie czas stoi, a nie {pauza.Position}");

        // Brak znacznika i zero: program NIE wystawia polozenia. To nie to samo
        // co poczatek utworu - stad osobna flaga, zeby powiedziec "nieznany".
        var brak = ExternalPlaybackTime.Compute(
            TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero,
            default, isPlaying: true, now: teraz);
        Assert(!brak.HasPosition, "bez znacznika polozenie jest nieznane");

        // Polozenie nie moze przekroczyc dlugosci utworu, choc migawka jest stara.
        var przeterminowana = ExternalPlaybackTime.Compute(
            TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(4), TimeSpan.Zero,
            znacznik, isPlaying: true, now: znacznik.AddMinutes(5));
        Assert(przeterminowana.Position <= TimeSpan.FromMinutes(4),
            $"polozenie przyciete do dlugosci, a jest {przeterminowana.Position}");

        // Migawka absurdalnie stara (ponad 10 minut) - nie doliczamy, bo to
        // znaczy, ze stan jest nieaktualny, a nie ze utwor gra od godziny.
        var stara = ExternalPlaybackTime.Compute(
            TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero,
            znacznik, isPlaying: true, now: znacznik.AddHours(1));
        Assert(stara.Position == TimeSpan.FromSeconds(30),
            $"stara migawka bez doliczania, a jest {stara.Position}");

        // Programy liczace od StartTime: polozenie i dlugosc liczymy wzglednie.
        var odStartu = ExternalPlaybackTime.Compute(
            position: TimeSpan.FromSeconds(90),
            endTime: TimeSpan.FromSeconds(300),
            startTime: TimeSpan.FromSeconds(60),
            lastUpdated: znacznik, isPlaying: false, now: teraz);
        Assert(odStartu.Position == TimeSpan.FromSeconds(30),
            $"polozenie wzgledem StartTime, a jest {odStartu.Position}");
        Assert(odStartu.Duration == TimeSpan.FromSeconds(240),
            $"dlugosc wzgledem StartTime, a jest {odStartu.Duration}");

        // Czas ujemny nigdy nie wychodzi na wierzch.
        var ujemny = ExternalPlaybackTime.Compute(
            TimeSpan.FromSeconds(-5), TimeSpan.Zero, TimeSpan.Zero,
            znacznik, isPlaying: false, now: teraz);
        Assert(ujemny.Position >= TimeSpan.Zero, "brak czasu ujemnego");
    }

    private static void Assert(bool warunek, string opis)
    {
        if (!warunek) throw new Exception("Nie spelniono: " + opis);
    }
}
