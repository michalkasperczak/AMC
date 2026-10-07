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
    /// ZWYKLY SUKCES: KROTKA NAZWA pozycji i nic wiecej. Dawny tekst doklejal
    /// "Przyjęto polecenie uruchomienia", a do tego CALA etykiete wiersza, wiec
    /// usluga i opis brzmialy drugi raz zaraz po tym, jak lista je przeczytala.
    /// Techniczne potwierdzenie nie jest informacja dla uzytkownika - nazwa jest.
    ///
    /// SKROCENIE NIE JEST OBIETNICA: HTTP 200 znaczy, ze Sonos POLECENIE PRZYJAL,
    /// a NIE ze muzyka gra - i wlasnie dlatego nazwa nie dostaje slowa "gra".
    /// Tozsamosc pozycji pochodzi z NASZEJ listy (to my wyslalismy ten
    /// identyfikator), nie z metadanych odczytanych po poleceniu. NAZWA, nigdy
    /// identyfikator: numer nic nie mowi. Bledy, brak celu, zmiana konta i
    /// niepewny wynik maja WLASNE, pelne teksty i tutaj sie nie zmieniaja.
    /// </summary>
    public static string DescribePlayAccepted(SonosFavorite favorite)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        return favorite.Name;
    }

    /// <summary>KOMUNIKAT OCZEKIWANIA: dlugi odczyt nie ma wygladac jak zawieszenie.</summary>
    public const string Loading = "Odczytuję ulubione Sonos. Czekaj.";

    /// <summary>
    /// DOSTEPNY PUSTY STAN. Dom bez ulubionych to POPRAWNY wynik, nie blad i nie
    /// udawana pozycja na liscie.
    /// </summary>
    public const string EmptyState = "Ten dom Sonos nie ma zapisanych ulubionych.";

    /// <summary>
    /// Etykieta JEDNEGO ulubionego. Nazwa, potem usluga i opis o ile istnieja -
    /// i o ile NIE POWTARZAJA tego, co juz w etykiecie jest.
    ///
    /// Zrodlo potrafi podac TEN SAM napis dwa razy (np. nazwa uslugi rowna
    /// opisowi: "3, TuneIn (New), TuneIn (New)"). Czytnik czytal to dwa razy, co
    /// jest halasem, nie informacja. Pomijamy WYLACZNIE DOKLADNE powtorzenie
    /// (bez wielkosci liter i bialych znakow na brzegach); ROZNE dopelnienia
    /// wchodza bez zmian, bo to osobne dane.
    /// </summary>
    public static string Describe(SonosFavorite favorite)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        var label = favorite.Name;
        // NAZWA uslugi, nigdy jej identyfikator. Puste/biale traktujemy jak brak.
        label = AppendIfNew(label, favorite.Service?.Name);
        return AppendIfNew(label, favorite.Description);
    }

    /// <summary>
    /// Doklej dopelnienie TYLKO gdy wnosi nowa tresc. Porownujemy z KAZDYM juz
    /// wypowiedzianym czlonem, a nie z calym napisem, zeby powtorka w srodku tez
    /// zostala pominieta.
    /// </summary>
    private static string AppendIfNew(string label, string? part)
    {
        if (string.IsNullOrWhiteSpace(part)) return label;
        var candidate = part.Trim();
        foreach (var said in label.Split(", ", StringSplitOptions.TrimEntries))
        {
            if (string.Equals(said, candidate, StringComparison.CurrentCultureIgnoreCase))
            {
                return label;
            }
        }

        return label + ", " + candidate;
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
