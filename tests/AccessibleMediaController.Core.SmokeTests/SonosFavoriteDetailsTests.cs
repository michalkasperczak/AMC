using System.Net;
using System.Net.Http;
using System.Text;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// PARAMETRY i ADRES ULUBIONEGO Sonos - polityka tego, co WOLNO powiedziec pod
/// Strzalka w lewo i co WOLNO skopiowac Ctrl+Shift+C.
///
/// Pomiary ida PRZEZ PRAWDZIWY <c>SonosControlApiClient</c> z syntetycznym
/// handlerem, zeby sprawdzic CALA droge: parsowanie opcjonalnych pol
/// <c>track.mediaUrl</c>, <c>track.contentType</c> i <c>track.quality</c> (ktorych
/// zastany model NIE czytal) oraz decyzje <see cref="SonosFavoriteDetails"/>.
///
/// CZEGO NIE DOWODZI: niczego o prawdziwym glosniku ani o zadnym koncie Sonos.
/// Zadne POST, Play ani Load tu nie leci - wszystkie pomiary to GET.
/// </summary>
internal static class SonosFavoriteDetailsTests
{
    private const string Key = "klucz-pomiaru";
    private const string Token = "token-pomiaru";
    private const string Group = "GRUPA-1";

    /// <summary>Trojka tozsamosci ulubionego uzywana w pomiarach.</summary>
    private const string OwnId =
        "\"id\":{\"serviceId\":\"38\",\"objectId\":\"OBIEKT-NASZ\",\"accountId\":\"KONTO-1\"}";

    /// <summary>INNA, tez KOMPLETNA trojka - znany, ale CUDZY material.</summary>
    private const string OtherId =
        "\"id\":{\"serviceId\":\"38\",\"objectId\":\"OBIEKT-OBCY\",\"accountId\":\"KONTO-1\"}";

    internal static void Run()
    {
        MeasureOptionalTrackFieldsAreRead();
        MeasureMissingOptionalFieldsStillParse();
        MeasureParametersNeedProvenIdentity();
        MeasureStationParametersAndLocation();
        MeasureAlbumTrackIsNotAlbumLocation();
        MeasureNullCurrentItemIsLegalForRadio();
        MeasureNoRepeatedNameInParameters();
        Console.WriteLine("OK: parametry i adres ulubionego Sonos - tożsamość, stacja, brak danych");
    }

    // ============ 1. OPCJONALNE POLA, KTORYCH MODEL NIE CZYTAL ============

    /// <summary>
    /// RED PRZED POPRAWKA: <c>SonosTrackMetadata</c> nie mial pol
    /// <c>MediaUrl</c>, <c>ContentType</c> ani <c>Quality</c>, wiec caly ladunek
    /// z odpowiedzi Sonosa gubil sie w parserze. To byl brak NASZEGO modelu, nie
    /// API - schemat ma te pola (quality od 1.24.0).
    /// </summary>
    private static void MeasureOptionalTrackFieldsAreRead()
    {
        var outcome = ReadMetadata(
            "{\"container\":{\"name\":\"Radio Nasze\",\"type\":\"station\"," + OwnId + "},"
            + "\"currentItem\":{\"id\":\"POZ-1\",\"track\":{\"type\":\"track\",\"name\":\"Na żywo\","
            + "\"mediaUrl\":\"https://strumien.przyklad.test/nasze.mp3\","
            + "\"contentType\":\"audio/mpeg\","
            + "\"quality\":{\"codec\":\"mp3\",\"sampleRate\":44100,\"bitDepth\":16,"
            + "\"lossless\":false}}}}");

        Require(outcome.Succeeded, "Poprawne metadane ze wszystkimi opcjonalnymi polami nie wczytały się: " + outcome.Status);
        var track = outcome.Metadata!.CurrentTrack;
        Require(track is not null, "Parser zgubił currentItem.track.");
        Require(track!.MediaUrl == "https://strumien.przyklad.test/nasze.mp3",
            "Parser nie przeczytał track.mediaUrl - to brak naszego modelu, nie API.");
        Require(track.ContentType == "audio/mpeg", "Parser nie przeczytał track.contentType.");
        Require(track.Quality is not null, "Parser nie przeczytał track.quality.");
        Require(track.Quality!.Codec == "mp3", "Parser nie przeczytał quality.codec.");
        Require(track.Quality.SampleRateHz == 44100, "Parser nie przeczytał quality.sampleRate.");
        Require(track.Quality.BitDepth == 16, "Parser nie przeczytał quality.bitDepth.");
        Require(track.Quality.Lossless == false, "Parser nie przeczytał quality.lossless.");
    }

    /// <summary>
    /// BRAK OPCJONALNYCH DANYCH NIE PSUJE ODCZYTU. Stary glosnik i radio nie
    /// podaja ani quality, ani mediaUrl - a reszta metadanych musi wejsc.
    /// Null w <c>lossless</c> to NIEWIEDZA, nie "stratny".
    /// </summary>
    private static void MeasureMissingOptionalFieldsStillParse()
    {
        var outcome = ReadMetadata(
            "{\"container\":{\"name\":\"Radio Nasze\",\"type\":\"station\"," + OwnId + "},"
            + "\"currentItem\":{\"id\":\"POZ-1\",\"track\":{\"type\":\"track\",\"name\":\"Na żywo\"}}}");

        Require(outcome.Succeeded, "Brak opcjonalnych pól zepsuł odczyt metadanych.");
        var track = outcome.Metadata!.CurrentTrack!;
        Require(track.Name == "Na żywo", "Zgubiono nazwę utworu przy braku pól opcjonalnych.");
        Require(track.MediaUrl is null, "Brak mediaUrl zamienił się w wartość.");
        Require(track.ContentType is null, "Brak contentType zamienił się w wartość.");
        Require(track.Quality is null, "Brak quality zamienił się w obiekt.");

        // CZESCIOWE quality: jedno pole jest, pozostale nie - i to jest legalne.
        var partial = ReadMetadata(
            "{\"container\":{\"name\":\"Radio Nasze\",\"type\":\"station\"," + OwnId + "},"
            + "\"currentItem\":{\"id\":\"POZ-1\",\"track\":{\"name\":\"Na żywo\","
            + "\"quality\":{\"codec\":\"aac\"}}}}");
        Require(partial.Succeeded, "Częściowe quality zepsuło odczyt.");
        var quality = partial.Metadata!.CurrentTrack!.Quality!;
        Require(quality.Codec == "aac", "Zgubiono jedyne podane pole quality.");
        Require(quality.SampleRateHz is null && quality.BitDepth is null,
            "Brakujące pola quality dostały wymyśloną wartość.");
        Require(quality.Lossless is null, "Brak lossless zamienił się w false - to niewiedza, nie strata.");
        Require(quality.HasAnyValue, "Quality z jednym polem uznane za puste.");
    }

    // ==================== 2. TOZSAMOSC, NIE NAZWA ====================

    /// <summary>
    /// CUDZY GRAJACY MATERIAL NIE OPISUJE NASZEGO WIERSZA. Gdy kontener grupy ma
    /// INNA (kompletna) trojke, parametry techniczne grajacego utworu NIE MAJA
    /// prawa wejsc do opisu zaznaczonego ulubionego, a adresu nie ma wcale.
    /// </summary>
    private static void MeasureParametersNeedProvenIdentity()
    {
        var favorite = OwnFavorite();
        var metadata = ReadMetadata(
            "{\"container\":{\"name\":\"Coś innego\",\"type\":\"station\"," + OtherId + "},"
            + "\"currentItem\":{\"id\":\"POZ-9\",\"track\":{\"name\":\"Obcy\","
            + "\"mediaUrl\":\"https://obcy.przyklad.test/x.mp3\",\"contentType\":\"audio/mpeg\","
            + "\"quality\":{\"codec\":\"flac\",\"lossless\":true}}}}").Metadata;

        var described = SonosFavoriteDetails.DescribeParameters(favorite, metadata);
        Require(!described.Contains("flac", StringComparison.OrdinalIgnoreCase),
            "Parametry CUDZEGO materiału trafiły do opisu naszego ulubionego.");
        Require(!described.Contains("bezstratny", StringComparison.OrdinalIgnoreCase),
            "Jakość cudzego utworu trafiła do opisu naszego ulubionego.");
        Require(described.StartsWith(favorite.Name, StringComparison.Ordinal),
            "Opis nie zaczyna się od nazwy zaznaczonego ulubionego.");

        Require(SonosFavoriteDetails.TryResolveLocation(favorite, metadata) is null,
            "Adres CUDZEGO materiału został oddany do skopiowania.");

        // NIEPELNA trojka kontenera tez NIE jest dowodem: brak accountId.
        var incomplete = ReadMetadata(
            "{\"container\":{\"name\":\"Radio Nasze\",\"type\":\"station\","
            + "\"id\":{\"serviceId\":\"38\",\"objectId\":\"OBIEKT-NASZ\"}},"
            + "\"currentItem\":{\"id\":\"POZ-1\",\"track\":{\"name\":\"Na żywo\","
            + "\"mediaUrl\":\"https://strumien.przyklad.test/nasze.mp3\"}}}").Metadata;
        Require(SonosFavoriteDetails.TryResolveLocation(favorite, incomplete) is null,
            "Niepełna trójka tożsamości wystarczyła do oddania adresu.");
    }

    /// <summary>
    /// POTWIERDZONA STACJA: parametry wchodza i adres wolno skopiowac. To jedyna
    /// droga, ktora oddaje URL - i tylko RZECZYWISTY <c>track.mediaUrl</c>.
    /// </summary>
    private static void MeasureStationParametersAndLocation()
    {
        var favorite = OwnFavorite();
        var metadata = ReadMetadata(
            "{\"container\":{\"name\":\"Radio Nasze\",\"type\":\"station\"," + OwnId + "},"
            + "\"currentItem\":{\"id\":\"POZ-1\",\"track\":{\"name\":\"Na żywo\","
            + "\"mediaUrl\":\"https://strumien.przyklad.test/nasze.mp3\","
            + "\"contentType\":\"audio/mpeg\","
            + "\"imageUrl\":\"https://obraz.przyklad.test/okladka.png\","
            + "\"quality\":{\"codec\":\"mp3\",\"sampleRate\":44100,\"bitDepth\":16,"
            + "\"lossless\":false}}}}").Metadata;

        var described = SonosFavoriteDetails.DescribeParameters(favorite, metadata);
        foreach (var expected in new[] { "audio/mpeg", "mp3", "44,1 kHz", "16 bitów", "stratny" })
        {
            Require(described.Contains(expected, StringComparison.CurrentCulture),
                "Opis potwierdzonej stacji nie podał: " + expected);
        }

        Require(!described.Contains(SonosFavoriteDetails.NoParameters, StringComparison.Ordinal),
            "Opis z parametrami nadal twierdzi, że parametrów nie ma.");

        var location = SonosFavoriteDetails.TryResolveLocation(favorite, metadata);
        Require(location == "https://strumien.przyklad.test/nasze.mp3",
            "Adres potwierdzonej stacji nie został oddany dosłownie.");
        Require(location is not null && !location.Contains("okladka", StringComparison.Ordinal),
            "Do adresu trafiła okładka (imageUrl) zamiast materiału.");
        Require(location is not null && !location.Contains("OBIEKT-NASZ", StringComparison.Ordinal),
            "Do adresu trafił identyfikator obiektu zamiast adresu.");
    }

    /// <summary>
    /// LINK UTWORU Z ALBUMU TO NIE LINK ALBUMU. Tozsamosc moze byc potwierdzona,
    /// ale gdy kontener jest ALBUMEM, <c>mediaUrl</c> biezacego utworu wskazuje
    /// JEDEN utwor - nie caly material ulubionego. Adresu NIE oddajemy.
    /// </summary>
    private static void MeasureAlbumTrackIsNotAlbumLocation()
    {
        var favorite = OwnFavorite();
        var metadata = ReadMetadata(
            "{\"container\":{\"name\":\"Nokturny\",\"type\":\"album\"," + OwnId + "},"
            + "\"currentItem\":{\"id\":\"POZ-4\",\"track\":{\"name\":\"Nokturn nr 4\","
            + "\"mediaUrl\":\"https://sklep.przyklad.test/utwor-4.flac\","
            + "\"contentType\":\"audio/flac\","
            + "\"quality\":{\"codec\":\"flac\",\"lossless\":true}}}}").Metadata;

        Require(SonosFavoriteDetails.TryResolveLocation(favorite, metadata) is null,
            "Adres JEDNEGO utworu z albumu został oddany jako adres całego ulubionego.");

        // Parametry grajacego utworu nadal wolno POWIEDZIEC - to opis tego, co
        // Sonos faktycznie podal dla potwierdzonego materialu, nie adres.
        var described = SonosFavoriteDetails.DescribeParameters(favorite, metadata);
        Require(described.Contains("flac", StringComparison.OrdinalIgnoreCase),
            "Parametry potwierdzonego albumu zniknęły z opisu.");
    }

    /// <summary>
    /// BRAK <c>currentItem</c> JEST LEGALNY dla radia. Wtedy nie ma parametrow
    /// ani adresu - i musi to byc KROTKA informacja, nie blad.
    /// </summary>
    private static void MeasureNullCurrentItemIsLegalForRadio()
    {
        var favorite = OwnFavorite();
        var metadata = ReadMetadata(
            "{\"container\":{\"name\":\"Radio Nasze\",\"type\":\"station\"," + OwnId + "}}").Metadata;

        Require(metadata is not null, "Brak currentItem uznano za nieudany odczyt - a to legalne dla radia.");
        Require(SonosFavoriteDetails.TryResolveLocation(favorite, metadata) is null,
            "Brak currentItem wyprodukował adres z niczego.");

        // CALKOWITY brak metadanych (zaplecze bez odczytu, nieudany GET) - ta
        // sama krotka odmowa, bez wyjatku.
        var described = SonosFavoriteDetails.DescribeParameters(favorite, null);
        Require(described.Contains(SonosFavoriteDetails.NoParameters, StringComparison.Ordinal),
            "Brak metadanych nie dał krótkiej informacji o braku parametrów.");
        Require(described.StartsWith(favorite.Name, StringComparison.Ordinal),
            "Odmowa nie zaczyna się od nazwy ulubionego.");
        Require(SonosFavoriteDetails.TryResolveLocation(favorite, null) is null,
            "Brak metadanych wyprodukował adres.");
    }

    /// <summary>
    /// ZADNEGO POWTARZANIA NAZWY. Zrodlo potrafi podac ten sam napis jako nazwe,
    /// nazwe uslugi i opis; czytnik czytalby to trzy razy.
    /// </summary>
    private static void MeasureNoRepeatedNameInParameters()
    {
        var favorite = new SonosFavorite(
            "ULU-1", "TuneIn (New)", "TuneIn (New)",
            new SonosFavoriteService("TuneIn (New)", "38"),
            SonosResourceIdentity.TryCreate("38", "OBIEKT-NASZ", "KONTO-1"));

        var described = SonosFavoriteDetails.DescribeParameters(favorite, null);
        var occurrences = described.Split("TuneIn (New)").Length - 1;
        Require(occurrences == 1,
            "Nazwa powtórzona w opisie " + occurrences + " razy - czytnik przeczyta hałas.");
    }

    // ==================== APARATURA ====================

    /// <summary>Ulubiony o KOMPLETNEJ trojce zgodnej z <see cref="OwnId"/>.</summary>
    private static SonosFavorite OwnFavorite() => new(
        "ULU-NASZ", "Radio Nasze", description: null,
        new SonosFavoriteService("TuneIn", "38"),
        SonosResourceIdentity.TryCreate("38", "OBIEKT-NASZ", "KONTO-1"));

    private static SonosGroupMetadataOutcome ReadMetadata(string body)
    {
        using var handler = new Handler(_ => Json(body));
        using var client = new SonosControlApiClient(
            SonosControlApiConfiguration.CreateDefault(Key), handler);
        return client.GetGroupMetadataAsync(Token, Group, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private static void Require(bool ok, string why)
    {
        // Komunikat opisuje WYMAGANIE, nigdy danych ani tokenu.
        if (!ok) throw new InvalidOperationException(why);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        private bool _disposed;

        protected override void Dispose(bool disposing) { _disposed = true; base.Dispose(disposing); }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var response = reply(request);
            // BEZ TEGO klient uznaje odpowiedz za PRZEKIEROWANIE i odmawia
            // (RedirectRefused): produkcja porownuje adres koncowy z zadaniem.
            // To brak aparatury, nie polityka produkcji.
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }
}
