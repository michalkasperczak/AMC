using System;
using System.Collections.Generic;
using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// BEZPIECZNE etykiety PLAYLIST SONOSA. Rodzenstwo <see cref="SonosFavoritesLabels"/>
/// i ta sama, odebrana dyscyplina - ale OSOBNE teksty, bo uzytkownik ma uslyszec,
/// czego naprawde dotyczy lista.
///
/// Trzy twarde zasady, przeniesione 1:1 z ulubionych:
///  1) NAZWA JEST PIERWSZA. Liczba utworow to dopelnienie i pojawia sie
///     WYLACZNIE gdy Sonos ja podal - <c>null</c> znaczy "nie podano", NIE zero.
///  2) ZADNEGO ZGADYWANIA. Pole <c>Type</c> przenosimy, ale nie tlumaczymy go na
///     wymyslony rodzaj i nie uzywamy jako warunku.
///  3) ZERO WYCIEKU: identyfikator playlisty, identyfikator domu ani wersja listy
///     NIE trafiaja do etykiety. Dwie playlisty o tej samej nazwie i roznych
///     identyfikatorach daja DWIE takie same etykiety - i tak ma byc, bo to
///     widzi uzytkownik. Rozroznia je TYPOWANY wiersz, nie napis.
/// </summary>
public static class SonosPlaylistsLabels
{
    /// <summary>
    /// PIERWSZA tresc okna w wariancie BEZ uruchamiania (zaplecze nie umie
    /// ladowania). Jawnie mowi, ze to podglad - zeby nikt nie szukal tu Entera
    /// jako "zagraj".
    /// </summary>
    public const string ViewIntroduction =
        "Lista playlist Sonos; to tylko podgląd i nie uruchamia odtwarzania.";

    /// <summary>
    /// PIERWSZA tresc okna, gdy uruchamianie JEST dostepne. Cel czyta sie TUTAJ -
    /// RAZ, w opisie okna - a nie w nazwie kazdego wiersza listy. Nazwa grupy
    /// pochodzi z ODCZYTU topologii; okno nie zgaduje i nie sklada jej z
    /// identyfikatora.
    /// </summary>
    public static string DescribePlayIntroduction(string? groupName)
    {
        // BRAK grupy NIE blokuje odczytu listy: mowimy wprost, czego brakuje, i
        // wskazujemy PRAWDZIWA, AKTUALNA droge wyboru celu.
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return "Lista playlist Sonos. Nie ma wybranego celu, więc Odtwórz jest niedostępne; "
                + "wybierz grupę skrótem Control F5, a potem wróć tutaj.";
        }

        return "Lista playlist Sonos. Odtwórz albo Enter na pozycji uruchamia ją w grupie "
            + groupName + ".";
    }

    /// <summary>ETYKIETA przycisku uruchomienia. Bez skrotu w tresci opisu.</summary>
    public const string PlayButtonLabel = "Odtwórz";

    /// <summary>
    /// WYJASNIENIE, dlaczego Odtworz jest niedostepne przy braku grupy. Wskazuje
    /// PRAWDZIWA droge wyboru celu - Ctrl+F5 - a nie dawne "Biblioteka, Enter",
    /// bo Biblioteka pokazuje teraz MATERIAL, nie glosniki.
    /// </summary>
    public const string PlayNeedsGroup =
        "Odtwarzanie playlisty wymaga wybranej grupy Sonos. "
        + "Wybierz grupę skrótem Control F5, a potem otwórz playlisty jeszcze raz.";

    /// <summary>
    /// ZAPLECZE bez umiejetnosci uruchamiania: podglad dziala, uruchamianie nie.
    /// Uczciwie, bez udawania, ze przycisk zaraz zagra.
    /// </summary>
    public const string PlayUnsupported =
        "To zaplecze Sonos nie udostępnia uruchamiania playlist; lista jest tylko do odczytu.";

    /// <summary>KOMUNIKAT OCZEKIWANIA na wynik proby uruchomienia.</summary>
    public const string PlayPending = "Wysyłam polecenie uruchomienia playlisty. Czekaj.";

    /// <summary>
    /// POWTORKA podczas trwajacej proby: krotka informacja, ZERO drugiego POST.
    /// Dotyczy takze autopowtarzania klawisza i przytrzymania.
    /// </summary>
    public const string PlayAlreadyInFlight =
        "Poprzednie polecenie uruchomienia playlisty jeszcze się nie zakończyło.";

    /// <summary>PUSTA lista: nie ma czego uruchomic. Nie jest to blad i nie ma tu POST.</summary>
    public const string PlayNothingSelected = "Nie ma wybranej playlisty do uruchomienia.";

    /// <summary>
    /// PRZYJECIE zlecenia: HTTP 200 znaczy, ze Sonos POLECENIE PRZYJAL, a NIE ze
    /// muzyka gra. Tozsamosc pozycji pochodzi z NASZEJ listy (to my wyslalismy ten
    /// identyfikator), a nie z metadanych odczytanych po poleceniu.
    /// </summary>
    public static string DescribePlayAccepted(string playlistLabel) =>
        "Przyjęto polecenie uruchomienia playlisty: " + playlistLabel;

    /// <summary>KOMUNIKAT OCZEKIWANIA: dlugi odczyt nie ma wygladac jak zawieszenie.</summary>
    public const string Loading = "Odczytuję playlisty Sonos. Czekaj.";

    /// <summary>
    /// DOSTEPNY PUSTY STAN. Dom bez playlist to POPRAWNY wynik (definicja mowi
    /// wprost, ze pole playlists jest opcjonalne i nullable), nie blad i nie
    /// udawana pozycja na liscie.
    /// </summary>
    public const string EmptyState = "Ten dom Sonos nie ma zapisanych playlist.";

    /// <summary>
    /// Etykieta JEDNEJ playlisty: nazwa, a potem liczba utworow O ILE Sonos ja
    /// podal. Brak liczby NIE jest zerem i nie dopisujemy "0 utworów".
    /// </summary>
    public static string Describe(SonosPlaylist playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        var label = playlist.Name;
        if (playlist.TrackCount is { } count)
        {
            label += ", " + DescribeTrackCount(count);
        }

        return label;
    }

    /// <summary>
    /// Liczba utworow po polsku, z poprawna odmiana. Zero jest LEGALNE i mowione
    /// wprost: Sonos podal pusta playliste, to nie to samo co brak informacji.
    /// </summary>
    private static string DescribeTrackCount(int count) => count switch
    {
        0 => "brak utworów",
        1 => "1 utwór",
        >= 2 and <= 4 => count.ToString(CultureInfo.CurrentCulture) + " utwory",
        _ => count.ToString(CultureInfo.CurrentCulture) + " utworów"
    };

    /// <summary>
    /// Etykiety CALEJ listy w KOLEJNOSCI API. Zadnego sortowania i zadnego
    /// scalania powtorzonych nazw.
    /// </summary>
    public static IReadOnlyList<string> DescribeAll(IReadOnlyList<SonosPlaylist> playlists)
    {
        ArgumentNullException.ThrowIfNull(playlists);
        var labels = new string[playlists.Count];
        for (var index = 0; index < playlists.Count; index++)
        {
            labels[index] = Describe(playlists[index]);
        }

        return labels;
    }

    /// <summary>Podsumowanie liczby pozycji do wypowiedzenia po otwarciu.</summary>
    public static string SummarizeCount(int count) => count switch
    {
        0 => EmptyState,
        1 => "Playlisty Sonos: 1 pozycja",
        _ => "Playlisty Sonos: " + count.ToString(CultureInfo.CurrentCulture) + " pozycji"
    };
}
