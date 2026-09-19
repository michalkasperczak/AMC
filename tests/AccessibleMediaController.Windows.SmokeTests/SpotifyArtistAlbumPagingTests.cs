using System.Net;
using System.Net.Http;
using System.Text;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// HTTP 400 z "Przejdz do wykonawcy" (zgloszenie z dziennika Michala).
///
/// Zrodlo: dokumentacja "Get Artist's Albums" (wrzesien 2026) daje dla
/// /artists/{id}/albums limit Default 5, Minimum 1, MAXIMUM 10. Wspolny
/// PageSize=50 uzywany przez pozostale kolekcje jest tu poza zakresem, wiec
/// Spotify odrzuca KAZDE zapytanie o albumy wykonawcy kodem 400. Ten limit
/// dotyczy tylko tego endpointu - nie ruszamy PageSize innym kolekcjom.
///
/// Test mierzy TRZY rzeczy, bo zadna nie dowodzi pozostalych:
/// 1. PIERWSZE zapytanie ma limit w granicach endpointu (atrapa odrzuca >10
///    tak jak Spotify, wiec przed poprawka test pada na 400),
/// 2. stronicowanie idzie dalej adresem "next" od Spotify (ma wlasny limit i
///    offset) i NIE gubi albumow przez twarde ucieci pierwszej strony,
/// 3. relacja albumu z wykonawca zostaje - bez niej "Przejdz do wykonawcy"
///    jest martwe, czyli objaw zglosze zostaje, tylko bez bledu HTTP.
/// </summary>
internal static class SpotifyArtistAlbumPagingTests
{
    internal static void Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        using var handler = new ArtistAlbumsHandler();
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);

        var albumy = await api.GetArtistAlbumsAsync("token", "art1", CancellationToken.None);

        Check(albumy is not null, "Albumy wykonawcy wrocily jako brak dostepu zamiast listy");
        Check(handler.Urls.Count == 2,
            $"Stronicowanie wyslalo {handler.Urls.Count} zapytan zamiast dwoch (pierwsza strona + next)");

        // 1. Pierwsze zapytanie: limit MUSI byc w granicach tego endpointu.
        var pierwsze = handler.Urls[0];
        Check(pierwsze.Contains("/artists/art1/albums", StringComparison.Ordinal),
            $"Zly adres albumow wykonawcy: {pierwsze}");
        Check(pierwsze.Contains("include_groups=album,single", StringComparison.Ordinal)
            || pierwsze.Contains("include_groups=album%2Csingle", StringComparison.Ordinal),
            $"Zapytanie nie filtruje rodzajow wydan: {pierwsze}");
        var limit = OdczytajLimit(pierwsze);
        Check(limit is >= 1 and <= 10,
            $"Limit pierwszego zapytania poza granica endpointu (dokumentacja: 1-10): {pierwsze}");
        Check(!pierwsze.Contains("limit=50", StringComparison.Ordinal),
            $"Zapytanie nadal uzywa wspolnego PageSize=50, ktory Spotify odrzuca kodem 400: {pierwsze}");

        // 2. Druga strona idzie DOKLADNIE adresem "next" od Spotify - to on, a
        // nie nasz limit, rzadzi paginacja.
        Check(handler.Urls[1] == NextUrl,
            $"Druga strona nie poszla adresem \"next\" od Spotify: {handler.Urls[1]}");

        // 3. Zaden album nie moze zginac: dwie strony po trzy pozycje.
        var lista = albumy!;
        var tytuly = lista.Select(item => item.Title).ToArray();
        Check(lista.Count == 6,
            $"Pobrano {lista.Count} albumow zamiast szesciu: {string.Join(", ", tytuly)}");
        Check(tytuly.SequenceEqual([
                "Cien wielkiej gory", "Przechodniem bylem", "Nic nie boli",
                "Noc", "Za ostatni grosz", "Ona przyszla prosto z chmur"
            ]),
            $"Kolejnosc albo zestaw albumow jest inny: {string.Join(", ", tytuly)}");
        Check(lista.All(item => item.Kind == MediaItemKind.Album),
            "Pozycja z listy albumow wykonawcy nie jest albumem");
        Check(lista.All(item => item.RelatedArtistExternalId == "art1"
            && item.RelatedArtistName == "Budka Suflera"),
            "Album stracil powiazanie z wykonawca - \"Przejdz do wykonawcy\" zostanie martwe");
        Check(lista.All(item => item.Source == $"spotify:album:{item.ExternalId}"),
            "Album z listy wykonawcy nie ma adresu odtwarzania");
        Check(lista.All(item => !item.IsInLibrary),
            "Album z katalogu wykonawcy udaje pozycje zapisana w bibliotece");

        Console.WriteLine("OK: albumy wykonawcy Spotify w granicach limitu endpointu"
            + " (limit pierwszego zapytania 1-10, paginacja adresem next,"
            + " pelna lista i relacja z wykonawca)");
    }

    private static int? OdczytajLimit(string url)
    {
        var zapytanie = new Uri(url).Query.TrimStart('?');
        foreach (var para in zapytanie.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var podzial = para.Split('=', 2);
            if (podzial.Length == 2 && podzial[0] == "limit" && int.TryParse(podzial[1], out var wartosc))
                return wartosc;
        }
        return null;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private const string NextUrl =
        "https://api.spotify.com/v1/artists/art1/albums?include_groups=album,single&offset=3&limit=3";

    /// <summary>
    /// Atrapa Spotify: odrzuca limit poza 1-10 kodem 400 dokladnie tak, jak
    /// robi to endpoint albumow wykonawcy, i oddaje DWIE strony.
    /// </summary>
    private sealed class ArtistAlbumsHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Add(url);

            var limit = OdczytajLimit(url);
            if (limit is null or < 1 or > 10)
            {
                // Syntetyczna odpowiedz HTTP 400 dla niedozwolonego limitu.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """{"error":{"status":400,"message":"Invalid limit"}}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            var pierwszaStrona = Urls.Count == 1;
            var body = pierwszaStrona
                ? Strona(["alb1", "alb2", "alb3"], ["Cien wielkiej gory", "Przechodniem bylem", "Nic nie boli"], NextUrl)
                : Strona(["alb4", "alb5", "alb6"], ["Noc", "Za ostatni grosz", "Ona przyszla prosto z chmur"], null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        private static string Strona(string[] ids, string[] nazwy, string? next)
        {
            var pozycje = string.Join(",", ids.Zip(nazwy, (id, nazwa) => $$"""
                {"id":"{{id}}","name":"{{nazwa}}","type":"album","album_type":"album",
                 "total_tracks":9,
                 "artists":[{"id":"art1","name":"Budka Suflera","type":"artist"}]}
                """));
            var nextJson = next is null ? "null" : $"\"{next}\"";
            return $$"""{"href":"x","limit":3,"offset":0,"total":6,"next":{{nextJson}},"previous":null,"items":[{{pozycje}}]}""";
        }

        private static int? OdczytajLimit(string url)
        {
            var zapytanie = new Uri(url).Query.TrimStart('?');
            foreach (var para in zapytanie.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var podzial = para.Split('=', 2);
                if (podzial.Length == 2 && podzial[0] == "limit" && int.TryParse(podzial[1], out var wartosc))
                    return wartosc;
            }
            return null;
        }
    }
}
