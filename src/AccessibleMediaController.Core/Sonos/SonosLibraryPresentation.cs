using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// BIBLIOTEKA TRESCI SONOSA pod ISTNIEJACYM "Pokaż bibliotekę" (Ctrl+L).
///
/// Decyzja, ktora ten plik zapisuje wprost: Ctrl+L w sesji Sonos pokazuje
/// BIBLIOTEKE MATERIALU - kategorie "Ulubione Sonos" i "Playlisty Sonos".
/// GLOSNIKI I GRUPY NIE SA BIBLIOTEKA MUZYCZNA: sa CELEM STEROWANIA i ich
/// wybor mieszka pod Ctrl+F5. Dlatego tutaj nie ma ani jednego wiersza grupy.
///
/// Trzy twarde zasady:
///  1) KATEGORIA NIE JEST MATERIALEM. Wiersz kategorii nie jest utworem,
///     stacja ani urzadzeniem: nie wolno go dodac do ulubionych AMC, do
///     Biblioteki AMC ani zapisac jako preset. Kategorie NIE WCHODZA do
///     <c>Session.Items</c> - zyja wylacznie jako wiersze widoku, wiec zaden
///     ogolny tor odtwarzania i zaden zapis presetu nie ma czego zlapac.
///  2) ZERO WYCIEKU I ZERO ZGADYWANIA. Kategoria nie podaje liczby pozycji,
///     dopoki ich nie odczytano - "Ulubione Sonos: 3" przed odczytem byloby
///     wymyslone. Liczba pojawia sie w OKNIE kategorii, po prawdziwym GET.
///  3) STALE IDENTYFIKATORY. Odswiezenie topologii w tle NIE MA prawa
///     przestawic zaznaczenia kategorii, wiec identyfikator wiersza jest staly
///     i nie pochodzi z zadnego identyfikatora domu, grupy ani pozycji.
/// </summary>
public static class SonosLibraryPresentation
{
    /// <summary>Nazwa widoku Biblioteki - ISTNIEJACA nazwa AMC, nie nowy widok.</summary>
    public const string LibraryViewName = "Biblioteka";

    /// <summary>
    /// STALY identyfikator wiersza "Ulubione Sonos". Nie jest identyfikatorem
    /// domu ani pozycji; sluzy WYLACZNIE do rozpoznania wiersza i do zachowania
    /// zaznaczenia przy odswiezeniu topologii.
    /// </summary>
    public const string FavoritesCategoryId = "sonos.library.favorites";

    /// <summary>STALY identyfikator wiersza "Playlisty Sonos".</summary>
    public const string PlaylistsCategoryId = "sonos.library.playlists";

    public const string FavoritesCategoryName = "Ulubione Sonos";

    public const string PlaylistsCategoryName = "Playlisty Sonos";
    public const string OwnStreamsCategoryId = "sonos.library.ownstreams";
    public const string OwnStreamsCategoryName = "Moje stacje";

    /// <summary>
    /// PIERWSZA tresc widoku. Mowi, CO tu jest, czego tu NIE MA i gdzie szukac
    /// celu sterowania - zeby nikt nie szukal glosnikow w bibliotece materialu.
    /// </summary>
    public const string ViewIntroduction =
        "Biblioteka Sonos: materiały z konta i stacje zapisane w AMC. Enter otwiera kategorię. "
        + "Głośniki i grupy to nie materiał - cel sterowania wybierasz skrótem Control F5.";

    /// <summary>
    /// PODPOWIEDZ dla kategorii ULUBIONYCH. Mowi, co zrobi Enter, i nie obiecuje
    /// odtwarzania samym wejsciem do kategorii.
    /// </summary>
    public const string FavoritesCategoryHint =
        "Ulubione Sonos, kategoria. Enter otwiera listę ulubionych z konta Sonos.";

    /// <summary>PODPOWIEDZ dla kategorii PLAYLIST.</summary>
    public const string PlaylistsCategoryHint =
        "Playlisty Sonos, kategoria. Enter otwiera listę playlist z konta Sonos.";

    /// <summary>
    /// JEDEN wiersz Biblioteki. Typowany, zeby wolajacy NIE rozpoznawal kategorii
    /// po polskiej nazwie wyswietlanej uzytkownikowi.
    /// </summary>
    /// <param name="CategoryId">Staly identyfikator kategorii.</param>
    /// <param name="Name">Nazwa czytana przez czytnik ekranu.</param>
    /// <param name="Hint">Co zrobi Enter - do pomocy kontekstowej wiersza.</param>
    public sealed record SonosLibraryCategoryRow(string CategoryId, string Name, string Hint);

    /// <summary>
    /// WIERSZE Biblioteki Sonosa. Kolejnosc jest STALA i swiadoma: ulubione
    /// pierwsze, bo to ISTNIEJACA, odebrana droga (Ctrl+U), playlisty drugie.
    ///
    /// Lista NIE ZALEZY od konta, domu ani topologii: kategoria istnieje takze
    /// wtedy, gdy jest pusta albo gdy konta nie ma - jej okno powie o tym
    /// UCZCIWIE po prawdziwym odczycie, zamiast znikac z widoku bez slowa.
    /// </summary>
    public static IReadOnlyList<SonosLibraryCategoryRow> DescribeCategories() =>
        new ReadOnlyCollection<SonosLibraryCategoryRow>(new List<SonosLibraryCategoryRow>
        {
            new(FavoritesCategoryId, FavoritesCategoryName, FavoritesCategoryHint),
            new(PlaylistsCategoryId, PlaylistsCategoryName, PlaylistsCategoryHint),
            new(OwnStreamsCategoryId, OwnStreamsCategoryName,
                "Moje stacje, kategoria. Adresy radia zapisane w AMC, nie w Ulubionych Sonosa.")
        });

    /// <summary>
    /// Czy WSKAZANY identyfikator wiersza jest kategoria Biblioteki Sonosa.
    /// Rozpoznanie idzie po IDENTYFIKATORZE, nigdy po nazwie: polska nazwa jest
    /// trescia dla uzytkownika, nie kluczem.
    /// </summary>
    public static bool IsCategoryId(string? itemId) =>
        string.Equals(itemId, FavoritesCategoryId, StringComparison.Ordinal)
        || string.Equals(itemId, PlaylistsCategoryId, StringComparison.Ordinal)
        || string.Equals(itemId, OwnStreamsCategoryId, StringComparison.Ordinal);

    /// <summary>
    /// ODMOWA dla prob potraktowania kategorii jak materialu (preset, ulubione
    /// AMC, czlonkostwo w Bibliotece AMC). Uczciwie mowi, czym kategoria JEST i
    /// co w niej dziala - zamiast cichego "nie da sie".
    /// </summary>
    public const string CategoryIsNotMaterial =
        "To kategoria Biblioteki Sonos, a nie materiał do zapisania. "
        + "Enter otwiera jej listę, a tam wybierasz konkretną pozycję.";
}
