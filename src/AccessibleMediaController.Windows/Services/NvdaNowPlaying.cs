using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Opis "co teraz leci" dla skrotu czytajacego zrodlo i utwor.
/// Sesja Radio internetowe ma mowic dokladnie tyle, ile mowi juz sesja
/// urzadzenia WiiM: najpierw stacja, potem rozpoznany tytul utworu,
/// bez powtarzania tego samego tekstu dwa razy.
/// Jedno nacisniecie, bez wchodzenia w widok odtwarzacza i bez ruszania fokusu.
/// </summary>
internal static class NvdaNowPlaying
{
    internal static string Describe(
        DemoMediaSession session,
        string? radioNowPlayingTitle,
        bool radioNowPlayingMatchesCurrentItem)
    {
        if (!session.HasCurrentItem)
            return $"{session.DisplayName}, nic nie jest otwarte.";

        var item = session.CurrentItem;
        var czesci = new List<string>();

        if (string.Equals(session.Id, "radio", StringComparison.Ordinal))
        {
            // Kolejnosc i zasada "bez powtorzen" jak w BuildWiiMNowPlayingParts:
            // stacja, tytul, wykonawca, album.
            Dodaj(czesci, item.Title);
            var utwor = radioNowPlayingMatchesCurrentItem ? radioNowPlayingTitle : null;
            Dodaj(czesci, utwor);
            Dodaj(czesci, item.Artist);
            Dodaj(czesci, item.RelatedAlbumTitle);
            // Gdy poza nazwa stacji nie ma nic, powiedz to wprost, zamiast
            // zostawic uzytkownika w niepewnosci, czy skrot zadzialal.
            if (czesci.Count == 1) czesci.Add("stacja nie podaje tytułu utworu");
        }
        else
        {
            Dodaj(czesci, item.Title);
            Dodaj(czesci, item.Artist);
            Dodaj(czesci, item.RelatedAlbumTitle);
        }

        // BEZ dopisywania stanu odtwarzania. Michal wskazal wprost, jak to ma
        // brzmiec: "Poznan Nastolatek - Krzysztof Zalewski", czyli stacja i
        // utwor - tyle, ile mowi sesja WiiM. Stan jest pod Ctrl+Windows+I,
        // a "wyciszone" tylko wtedy, gdy naprawde nic nie slychac.
        if (session.IsMuted) czesci.Add("wyciszone");
        return string.Join(", ", czesci) + ".";
    }

    private static void Dodaj(List<string> czesci, string? wartosc)
    {
        if (string.IsNullOrWhiteSpace(wartosc)) return;
        var tekst = wartosc.Trim();
        if (czesci.Any(istniejacy =>
                string.Equals(istniejacy, tekst, StringComparison.CurrentCultureIgnoreCase)))
        {
            return;
        }
        czesci.Add(tekst);
    }
}
