using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// PARAMETRY i ADRES zaznaczonego ULUBIONEGO Sonos - do Strzalki w lewo oraz do
/// Ctrl+Shift+C w oknie ulubionych.
///
/// CALA tresc tej klasy to GRANICE tego, co wolno powiedziec i skopiowac:
///
///  * <c>getFavorites</c> NIE DAJE ani adresu strumienia, ani bitrate. Z samej
///    listy mamy wylacznie nazwe, opcjonalny opis i opcjonalna nazwe uslugi.
///  * Parametry techniczne i <c>track.mediaUrl</c> istnieja tylko w metadanych
///    GRAJACEJ grupy. Wolno ich uzyc DOPIERO po DOWIEDZIONEJ tozsamosci:
///    <see cref="SonosResourceIdentity.AreSameMaterial"/> miedzy
///    <c>favorite.resource.id</c> a <c>metadata.container.id</c>. Nazwa, rodzaj
///    ani kod 200 NIE sa dowodem.
///  * LINK UTWORU Z ALBUMU TO NIE LINK ALBUMU. Gdy kontener nie jest stacja,
///    <c>currentItem.track.mediaUrl</c> wskazuje JEDEN utwor, nie caly material
///    ulubionego - i wtedy go NIE oddajemy. Adres wolno podac tylko dla STACJI
///    (<c>container.type == "station"</c>) o potwierdzonej tozsamosci.
///  * <c>imageUrl</c> (okladka) i identyfikator obiektu NIE SA adresem materialu
///    i nigdy tu nie wchodza.
///  * BRAK DANYCH to krotka informacja. Nigdy zgadnieta wartosc i nigdy
///    wyczyszczony schowek.
/// </summary>
public static class SonosFavoriteDetails
{
    /// <summary>Odpowiedz, gdy nie ma czego podac poza sama nazwa.</summary>
    public const string NoParameters = "Sonos nie podał parametrów tego ulubionego";

    /// <summary>Odpowiedz Ctrl+Shift+C, gdy adresu PO PROSTU NIE MA.</summary>
    public const string NoLocation = "Sonos nie podał adresu tego ulubionego";

    /// <summary>
    /// TEKST pod Strzalka w lewo. Zawsze zaczyna sie od NAZWY ulubionego, a
    /// dopelnienia wchodza tylko wtedy, gdy NIE POWTARZAJA juz powiedzianego -
    /// ta sama regula, co w etykiecie wiersza listy.
    ///
    /// <paramref name="metadata"/> moze byc null (nie czytalismy grupy albo
    /// odczyt odmowil) i to jest POPRAWNY przypadek: mowimy wtedy to, co dala
    /// sama lista ulubionych.
    /// </summary>
    public static string DescribeParameters(SonosFavorite favorite, SonosGroupMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(favorite);

        var parts = new List<string> { favorite.Name };
        AppendIfNew(parts, favorite.Service?.Name);
        AppendIfNew(parts, favorite.Description);
        // ILE CZLONOW DALA SAMA LISTA - granica, po ktorej poznajemy, czy
        // doszlo COKOLWIEK TECHNICZNEGO. Bez tego opis "Radio Nasze, TuneIn"
        // wygladalby jak odpowiedz o parametrach, a nazwa uslugi parametrem nie
        // jest - uzytkownik nie wiedzialby, czy czegos nie przeslyszal.
        var fromListOnly = parts.Count;

        // TOZSAMOSC, nie nazwa: parametry grajacego materialu wolno dopiac do
        // TEGO wiersza dopiero wtedy, gdy grupa ma zaladowany DOKLADNIE ten
        // material. Inaczej opisalibysmy cudza, wlasnie grajaca pozycje.
        if (TryResolveConfirmedTrack(favorite, metadata) is { } track)
        {
            AppendIfNew(parts, FormatContentType(track.ContentType));
            AppendQuality(parts, track.Quality);
        }

        // ZERO TECHNICZNYCH CZLONOW to uczciwy brak parametrow - mowimy to
        // WPROST, obok tego, co wiadomo z listy.
        return parts.Count == fromListOnly
            ? string.Join(", ", parts) + ". " + NoParameters
            : string.Join(", ", parts);
    }

    /// <summary>
    /// RZECZYWISTY adres materialu do Ctrl+Shift+C albo null.
    ///
    /// Trzy warunki, wszystkie konieczne:
    ///  1) tozsamosc ulubionego i kontenera grupy sa ZGODNE i KOMPLETNE,
    ///  2) kontener jest STACJA - dla albumu/playlisty adres biezacego utworu
    ///     NIE jest adresem calego materialu,
    ///  3) serwer PODAL <c>track.mediaUrl</c>.
    /// Brak ktoregokolwiek znaczy "nie ma czego skopiowac".
    /// </summary>
    public static string? TryResolveLocation(SonosFavorite favorite, SonosGroupMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(favorite);
        if (metadata?.Container is not { IsStation: true }) return null;
        if (TryResolveConfirmedTrack(favorite, metadata) is not { MediaUrl: { } url }) return null;
        return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    /// <summary>
    /// UTWOR grupy POTWIERDZONY jako ten sam material, co zaznaczony ulubiony,
    /// albo null. Brak <c>currentItem</c> jest LEGALNY (typowo radio) i daje
    /// null bez zadnego bledu.
    /// </summary>
    private static SonosTrackMetadata? TryResolveConfirmedTrack(
        SonosFavorite favorite, SonosGroupMetadata? metadata)
    {
        if (metadata?.Container is not { } container) return null;
        if (!SonosResourceIdentity.AreSameMaterial(favorite.ResourceIdentity, container.Identity))
        {
            return null;
        }

        return metadata.CurrentTrack;
    }

    /// <summary>
    /// contentType jak "audio/mpeg" zamieniamy na krotka, wymawialna nazwe
    /// formatu. NIEZNANY typ oddajemy DOSLOWNIE - lepiej przeczytac surowa
    /// wartosc niz ja zgubic.
    /// </summary>
    private static string? FormatContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return null;
        // Parametry po sredniku (np. "; charset=") nie sa formatem.
        var value = contentType.Split(';')[0].Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>
    /// Parametry z <c>track.quality</c>. KAZDE pole osobno opcjonalne - brak
    /// jednego nie kasuje pozostalych. BITRATE nie istnieje w tym schemacie,
    /// wiec go tu NIE MA i nie wolno go wyliczac.
    /// </summary>
    private static void AppendQuality(List<string> parts, SonosTrackQuality? quality)
    {
        if (quality is not { HasAnyValue: true }) return;
        AppendIfNew(parts, quality.Codec);
        if (quality.SampleRateHz is > 0 and var hz)
        {
            AppendIfNew(parts, FormatSampleRate(hz));
        }

        if (quality.BitDepth is > 0 and var bits)
        {
            AppendIfNew(parts, bits.ToString(CultureInfo.CurrentCulture) + " bitów");
        }

        // NULL to NIEWIEDZA, nie "stratny" - o nieznanym nie mowimy nic.
        if (quality.Lossless is { } lossless)
        {
            AppendIfNew(parts, lossless ? "bezstratny" : "stratny");
        }
    }

    /// <summary>44100 Hz czyta sie wygodniej jako 44,1 kHz.</summary>
    private static string FormatSampleRate(int hz) =>
        (hz / 1000.0).ToString("0.###", CultureInfo.CurrentCulture) + " kHz";

    /// <summary>
    /// Dopelnienie wchodzi TYLKO gdy wnosi nowa tresc. Zrodlo potrafi podac ten
    /// sam napis w nazwie, uslugi i opisie; czytanie go trzy razy to halas.
    /// </summary>
    private static void AppendIfNew(List<string> parts, string? part)
    {
        if (string.IsNullOrWhiteSpace(part)) return;
        var candidate = part.Trim();
        foreach (var said in parts)
        {
            if (string.Equals(said, candidate, StringComparison.CurrentCultureIgnoreCase)) return;
        }

        parts.Add(candidate);
    }
}
