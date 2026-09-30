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
    /// PIERWSZA tresc okna. Jawnie mowi, ze to podglad i ze nie uruchamia
    /// odtwarzania - zeby nikt nie szukal tu Enter jako "zagraj".
    /// </summary>
    public const string ViewIntroduction =
        "Lista ulubionych Sonos; to tylko podgląd i nie uruchamia odtwarzania.";

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
