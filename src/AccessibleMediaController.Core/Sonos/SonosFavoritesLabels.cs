using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// F2: BEZPIECZNE etykiety PODGLADU ULUBIONYCH.
///
/// Trzy twarde zasady:
///  1) NAZWA JEST PIERWSZA. Usluga i opis to dopelnienia i pojawiaja sie
///     WYLACZNIE gdy Sonos je podal.
///  2) ZADNEGO ZGADYWANIA RODZAJU. Control API nie ma pola typu ulubionego,
///     wiec nie ma tu ani "radio", ani "album", ani "utwor".
///  3) ZERO WYCIEKU: identyfikator ulubionego, identyfikator uslugi,
///     identyfikator domu, token ani nazwa typu NIE trafiaja do etykiety.
///     Dwa ulubione o tej samej nazwie i roznych identyfikatorach daja DWIE
///     takie same etykiety - i tak ma byc, bo to jest to, co widzi uzytkownik.
/// </summary>
public static class SonosFavoritesLabels
{
    /// <summary>
    /// PIERWSZA tresc okna w wariancie BEZ uruchamiania (zaplecze nie umie
    /// ladowania albo okno otwarto droga tylko do odczytu). Jawnie mowi, ze to
    /// podglad i ze nie uruchamia odtwarzania - zeby nikt nie szukal tu Enter
    /// jako "zagraj".
    /// </summary>
    public const string ViewIntroduction =
        "Lista ulubionych Sonos; to tylko podgląd i nie uruchamia odtwarzania.";

    /// <summary>
    /// F3c: PIERWSZA tresc okna, gdy uruchamianie JEST dostepne. Cel czyta sie
    /// TUTAJ - RAZ, w opisie okna - a nie w nazwie kazdego wiersza listy.
    /// Nazwa grupy jest podana przez wolajacego z ODCZYTU topologii; okno nie
    /// zgaduje i nie sklada jej z identyfikatora.
    /// </summary>
    public static string DescribePlayIntroduction(string? groupName)
    {
        // BRAK grupy NIE blokuje odczytu listy: mowimy wprost, czego brakuje, i
        // wskazujemy ISTNIEJACA droge wyboru, zamiast udawac cel.
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return "Lista ulubionych Sonos. Nie ma aktywnej grupy, więc Odtwórz jest niedostępne; "
                + "wybierz cel skrótem Control F5, a potem wróć tutaj.";
        }

        return "Lista ulubionych Sonos. Odtwórz albo Enter na pozycji uruchamia ją w grupie "
            + groupName + ".";
    }

    /// <summary>ETYKIETA przycisku uruchomienia. Bez skrotu w tresci opisu.</summary>
    public const string PlayButtonLabel = "Odtwórz";

    /// <summary>
    /// WYJASNIENIE, dlaczego Odtwórz jest niedostepne przy braku grupy.
    ///
    /// Tekst ZMIENIONY swiadomie: dawny mowil "wybierz grupę w Bibliotece
    /// Enterem", bo grupy stały na liscie sesji. Po zastapieniu tej listy
    /// BIBLIOTEKA MATERIALU (Ctrl+L: Ulubione/Playlisty) taka instrukcja
    /// prowadzilaby uzytkownika w miejsce, gdzie grup NIE MA. PRAWDZIWA droga
    /// wyboru celu to Ctrl+F5.
    /// </summary>
    public const string PlayNeedsGroup =
        "Odtwarzanie ulubionego wymaga aktywnej grupy Sonos. "
        + "Wybierz cel skrótem Control F5, a potem ponów Pokaż ulubione.";

    /// <summary>
    /// ZAPLECZE bez umiejetnosci uruchamiania: podglad dziala, uruchamianie nie.
    /// Uczciwie, bez udawania ze przycisk zaraz zagra.
    /// </summary>
    public const string PlayUnsupported =
        "To zaplecze Sonos nie udostępnia uruchamiania ulubionych; lista jest tylko do odczytu.";

    /// <summary>KOMUNIKAT OCZEKIWANIA na wynik proby uruchomienia.</summary>
    public const string PlayPending = "Wysyłam polecenie uruchomienia. Czekaj.";

    /// <summary>
    /// POWTORKA podczas trwajacej proby: krotka informacja, ZERO drugiego POST.
    /// Dotyczy takze autopowtarzania klawisza (e.IsRepeat) i przytrzymania.
    /// </summary>
    public const string PlayAlreadyInFlight =
        "Poprzednie polecenie uruchomienia ulubionego jeszcze się nie zakończyło.";

    /// <summary>
    /// PUSTA lista: nie ma czego uruchomic. Nie jest to blad i nie ma tu POST.
    /// </summary>
    public const string PlayNothingSelected = "Nie ma wybranego ulubionego do uruchomienia.";

    /// <summary>
    /// PRZYJECIE zlecenia: HTTP 200 znaczy, ze Sonos POLECENIE PRZYJAL, a NIE ze
    /// muzyka gra. Tozsamosc pozycji pochodzi z NASZEJ listy (to my wyslalismy
    /// ten identyfikator), a nie z metadanych odczytanych po poleceniu - tytul w
    /// metadanych nie dowodzi, ze gra WLASNIE ten ulubiony.
    /// </summary>
    public static string DescribePlayAccepted(string favoriteLabel) =>
        "Przyjęto polecenie uruchomienia: " + favoriteLabel;

    /// <summary>KOMUNIKAT OCZEKIWANIA: dlugi odczyt nie ma wygladac jak zawieszenie.</summary>
    public const string Loading = "Odczytuję ulubione Sonos. Czekaj.";

    /// <summary>
    /// DOSTEPNY PUSTY STAN. Dom bez ulubionych to POPRAWNY wynik, nie blad i nie
    /// udawana pozycja na liscie.
    /// </summary>
    public const string EmptyState = "Ten dom Sonos nie ma zapisanych ulubionych.";

    /// <summary>
    /// Etykieta JEDNEGO ulubionego. Nazwa, potem usluga i opis o ile istnieja.
    /// </summary>
    public static string Describe(SonosFavorite favorite)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        var label = favorite.Name;
        // NAZWA uslugi, nigdy jej identyfikator. Puste/biale traktujemy jak brak.
        if (favorite.Service?.Name is { } serviceName && !string.IsNullOrWhiteSpace(serviceName))
        {
            label += ", " + serviceName;
        }

        if (favorite.Description is { } description && !string.IsNullOrWhiteSpace(description))
        {
            label += ", " + description;
        }

        return label;
    }

    /// <summary>
    /// Etykiety CALEJ listy w KOLEJNOSCI API. Zadnego sortowania i zadnego
    /// scalania powtorzonych nazw.
    /// </summary>
    public static IReadOnlyList<string> DescribeAll(IReadOnlyList<SonosFavorite> favorites)
    {
        ArgumentNullException.ThrowIfNull(favorites);
        var labels = new string[favorites.Count];
        for (var index = 0; index < favorites.Count; index++)
        {
            labels[index] = Describe(favorites[index]);
        }

        return labels;
    }

    /// <summary>Podsumowanie liczby pozycji do wypowiedzenia po otwarciu.</summary>
    public static string SummarizeCount(int count) => count switch
    {
        0 => EmptyState,
        1 => "Ulubione Sonos: 1 pozycja",
        _ => "Ulubione Sonos: " + count.ToString(CultureInfo.InvariantCulture) + " pozycji"
    };
}
