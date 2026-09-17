using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Opis "co teraz leci" dla skrotu czytajacego zrodlo i utwor.
/// Wzorowane na odtwarzaczu Vim: jedno nacisniecie mowi i stacje, i tytul,
/// bez wchodzenia w widok odtwarzacza i bez ruszania fokusu.
/// Radio ma dwa rozne pola: nazwe stacji (Title) oraz rozpoznany
/// tytul utworu z metadanych strumienia - i oba maja byc slyszalne.
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
            czesci.Add(item.Title);
            var utwor = radioNowPlayingMatchesCurrentItem ? radioNowPlayingTitle : null;
            // Puste, powtarzajace nazwe stacji albo nieaktualne metadane
            // pomijamy - lepiej powiedziec mniej niz wprowadzic w blad.
            if (!string.IsNullOrWhiteSpace(utwor)
                && !string.Equals(utwor.Trim(), item.Title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                czesci.Add(utwor.Trim());
            }
            else
            {
                czesci.Add("stacja nie podaje tytułu utworu");
            }
        }
        else
        {
            czesci.Add(item.Title);
            if (!string.IsNullOrWhiteSpace(item.Artist)) czesci.Add(item.Artist.Trim());
        }

        czesci.Add(session.IsPlaying ? "odtwarzanie" : "pauza");
        if (session.IsMuted) czesci.Add("wyciszone");
        return string.Join(", ", czesci) + ".";
    }
}
