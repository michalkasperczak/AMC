using System.Net;
using System.Net.Http;
using System.Text;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Zdalne wyszukiwanie Spotify (Ctrl+F) na wzor TIDAL.
///
/// Test mierzy TRZY rzeczy, bo zadna nie dowodzi pozostalych:
/// 1. zapytanie do API ma parametry w granicach dokumentacji ("Search for
///    Item": limit 0-10 na rodzaj, rynek obowiazkowy),
/// 2. parser oddaje wszystkie SZESC rodzajow wynikow, ktore AMC umie pokazac,
/// 3. routing i scalanie: sesja Spotify ma wlasne zdalne wyszukiwanie, a blad
///    sieci NIE kasuje wynikow lokalnych.
/// </summary>
internal static class SpotifySearchTests
{
    internal static void Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        await SprawdzZapytanieIParser();
        SprawdzRouting();
        SprawdzScalanie();
        await SprawdzBladNieKasujeWynikow();
        Console.WriteLine("OK: zdalne wyszukiwanie Spotify jak TIDAL"
            + " (parametry zapytania w granicach API, szesc rodzajow wynikow,"
            + " routing sesji, blad nie kasuje wynikow lokalnych)");
    }

    private static async Task SprawdzZapytanieIParser()
    {
        using var handler = new SearchHandler(FixtureJson);
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);

        var wyniki = await api.SearchAsync("token", "pl", "  budka suflera  ", CancellationToken.None);

        Check(handler.Urls.Count == 1, "Wyszukiwanie wyslalo inna liczbe zapytan niz jedno");
        var url = handler.Urls[0];
        Check(url.StartsWith("https://api.spotify.com/v1/search?", StringComparison.Ordinal),
            $"Zly adres wyszukiwania: {url}");
        Check(url.Contains("q=budka%20suflera", StringComparison.Ordinal),
            $"Zapytanie nie jest obciete i zakodowane: {url}");
        foreach (var typ in new[] { "artist", "album", "track", "playlist", "show", "episode" })
        {
            Check(url.Contains(typ, StringComparison.Ordinal),
                $"Wyszukiwanie nie prosi o rodzaj {typ}: {url}");
        }
        Check(!url.Contains("audiobook", StringComparison.Ordinal),
            "Wyszukiwanie prosi o audiobooki, ktorych AMC nie odtwarza");
        // Granica API, nie nasz kaprys: limit dotyczy kazdego rodzaju osobno i
        // konczy sie na 10. Wieksza liczba to HTTP 400, nie dluzsza lista.
        Check(url.Contains("limit=10", StringComparison.Ordinal),
            $"Limit wyszukiwania poza granica API Spotify: {url}");
        Check(url.Contains("market=PL", StringComparison.Ordinal),
            $"Brak rynku w zapytaniu (bez niego Spotify oznacza tresc jako niedostepna): {url}");

        Check(wyniki.Count == 6, $"Parser oddal {wyniki.Count} pozycji zamiast szesciu");
        var rodzaje = wyniki.Select(item => item.Kind).ToArray();
        Check(rodzaje.SequenceEqual([
                MediaItemKind.Artist,
                MediaItemKind.Album,
                MediaItemKind.Track,
                MediaItemKind.Playlist,
                MediaItemKind.Podcast,
                MediaItemKind.Episode
            ]),
            "Kolejnosc albo zestaw rodzajow wynikow jest inny niz wykonawca, album, utwor, playlista, podcast, odcinek");

        var wykonawca = wyniki[0];
        Check(wykonawca.Title == "Budka Suflera" && wykonawca.ExternalId == "art1"
            && wykonawca.Source == "spotify:artist:art1", "Wykonawca z wyszukiwania");
        var album = wyniki[1];
        Check(album.Title == "Cien wielkiej gory" && album.Artist == "Budka Suflera"
            && album.RelatedArtistExternalId == "art1" && album.Source == "spotify:album:alb1",
            "Album z wyszukiwania albo jego powiazanie z wykonawca");
        var utwor = wyniki[2];
        Check(utwor.Title == "Jolka, Jolka" && utwor.Source == "spotify:track:trk1"
            && utwor.Duration == TimeSpan.FromMilliseconds(245000)
            && utwor.RelatedAlbumExternalId == "alb1" && utwor.RelatedArtistExternalId == "art1",
            "Utwor z wyszukiwania albo jego czas i powiazania");
        var playlista = wyniki[3];
        Check(playlista.Title == "Polski rock" && playlista.Artist == "Michal"
            && playlista.Source == "spotify:playlist:pl1", "Playlista z wyszukiwania");
        var podcast = wyniki[4];
        Check(podcast.Title == "Tyfloprzeglad" && podcast.Artist == "Tyfloswiat"
            && podcast.ExternalId == "shw1", "Podcast z wyszukiwania");
        // Podcast jest kontenerem: Enter nie ma go odtwarzac, wchodzi sie do
        // niego strzalka w prawo po liste odcinkow.
        Check(podcast.Source is null, "Podcast z wyszukiwania dostal adres odtwarzania");
        var odcinek = wyniki[5];
        Check(odcinek.Title == "Odcinek 100" && odcinek.Source == "spotify:episode:ep1"
            && odcinek.Duration == TimeSpan.FromMilliseconds(3600000),
            "Odcinek z wyszukiwania");

        // Wynik z katalogu NIE jest biblioteka konta. Ctrl+L i Ctrl+U musza
        // dalej mowic prawde o tym, co uzytkownik zapisal.
        Check(wyniki.All(item => !item.IsInLibrary && !item.IsFavorite),
            "Wynik wyszukiwania udaje pozycje z biblioteki albo z ulubionych");
        // Pozycja niedostepna w rynku zostaje na liscie, ale jest oznaczona.
        Check(wyniki[2].IsAvailable, "Odtwarzalny utwor oznaczony jako niedostepny");

        handler.Urls.Clear();
        var puste = await api.SearchAsync("token", "PL", "   ", CancellationToken.None);
        Check(puste.Count == 0 && handler.Urls.Count == 0,
            "Puste zapytanie poszlo do sieci");

        handler.Fail = HttpStatusCode.Unauthorized;
        try
        {
            await api.SearchAsync("token", "PL", "cokolwiek", CancellationToken.None);
            throw new Exception("Blad HTTP zamienil sie w pusta liste wynikow");
        }
        catch (SpotifyApiException exception)
        {
            Check(exception.IsAuthorizationFailure, "Blad 401 nie jest rozpoznany jako problem logowania");
        }

        handler.Fail = null;
        handler.Body = """{"playlists":{"items":[null,{"id":"pl2","name":"Druga"}]}}""";
        var zDziura = await api.SearchAsync("token", "PL", "cokolwiek", CancellationToken.None);
        Check(zDziura.Count == 1 && zDziura[0].ExternalId == "pl2",
            "Wartosc null w wynikach playlist wywraca cale wyszukiwanie");
    }

    private static void SprawdzRouting()
    {
        Check(MainWindow.SessionHasRemoteSearch("spotify", allServices: false),
            "Sesja Spotify nie ma zdalnego wyszukiwania katalogu");
        Check(MainWindow.RemoteSearchLabel("spotify", allServices: false) == "katalogu Spotify",
            "Czytnik nie dowie sie, ze wyszukiwanie idzie do katalogu Spotify");
        // Stare sesje nie moga zmienic zachowania przy okazji.
        Check(MainWindow.SessionHasRemoteSearch("tidal", allServices: false)
            && MainWindow.RemoteSearchLabel("tidal", allServices: false) == "katalogu TIDAL",
            "Zdalne wyszukiwanie TIDAL zmienilo sie");
        Check(MainWindow.RemoteSearchLabel("podcasts", allServices: false)
            == "katalogach Apple Podcasts, Spreaker i YouTube", "Opis katalogu podcastow zmienil sie");
        Check(MainWindow.RemoteSearchLabel("radio", allServices: false) == "katalogu radia",
            "Opis katalogu radia zmienil sie");
        Check(MainWindow.RemoteSearchLabel("spotify", allServices: true)
            == "katalogach radia, podcastów i YouTube",
            "Wyszukiwanie we wszystkich serwisach opisuje sie katalogiem Spotify");
        Check(!MainWindow.SessionHasRemoteSearch("local", allServices: false),
            "Sesja lokalna probuje szukac w sieci");
    }

    private static void SprawdzScalanie()
    {
        var zSesji = new MediaItem
        {
            Id = "lokalne-id-z-biblioteki",
            Kind = MediaItemKind.Album,
            Title = "Cien wielkiej gory",
            ExternalId = "alb1",
            IsInLibrary = true
        };
        var zKatalogu = new[]
        {
            new MediaItem { Kind = MediaItemKind.Album, Title = "Cien wielkiej gory", ExternalId = "alb1" },
            new MediaItem { Kind = MediaItemKind.Track, Title = "Jolka, Jolka", ExternalId = "alb1" },
            new MediaItem { Kind = MediaItemKind.Track, Title = "Jolka, Jolka", ExternalId = "alb1" },
            new MediaItem { Kind = MediaItemKind.Track, Title = "Bez nazwy", ExternalId = "" }
        };

        var scalone = MainWindow.MergeSpotifySearchResults(zKatalogu, [zSesji]);
        Check(scalone.Count == 2, $"Scalanie oddalo {scalone.Count} pozycji zamiast dwoch");
        Check(ReferenceEquals(scalone[0], zSesji),
            "Pozycja znana sesji wrocila jako nowy obiekt - flagi i pamiec pozycji przepadly");
        Check(scalone[1].Id == "spotify:Track:alb1",
            $"Nowa pozycja nie ma stabilnego identyfikatora: {scalone[1].Id}");
        // Ten sam ExternalId w dwoch rodzajach to DWIE rozne pozycje: album i
        // utwor nie moga dzielic identyfikatora w liscie wynikow.
        Check(scalone.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == 2,
            "Album i utwor o tym samym identyfikatorze katalogu zlaly sie w jedna pozycje");
        Check(scalone.All(item => !string.IsNullOrWhiteSpace(item.ExternalId)),
            "Pozycja bez identyfikatora katalogu trafila do wynikow");
    }

    /// <summary>
    /// Brak sieci i wygasle logowanie MUSZA zostawic wyniki lokalne. Puste okno
    /// po Ctrl+F czyta sie jak awaria programu, a nie jak brak internetu.
    /// </summary>
    private static async Task SprawdzBladNieKasujeWynikow()
    {
        using var handler = new SearchHandler(FixtureJson) { Throw = new HttpRequestException("brak sieci") };
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);

        var lokalne = new MediaItem { Id = "lokalny", Kind = MediaItemKind.Track, Title = "Zapisany utwor" };
        var sesja = new DemoMediaSession("spotify", "Spotify", [lokalne]);
        IReadOnlyList<MediaItem> zKatalogu;
        var padlo = false;
        try
        {
            zKatalogu = await api.SearchAsync("token", "PL", "cokolwiek", CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            padlo = true;
            zKatalogu = [];
        }
        Check(padlo, "Brak sieci nie zglosil wyjatku - okno nie powie, dlaczego katalogu nie ma");

        // Tak robi SearchWindow.RunSearch: wyjatek zamienia na komunikat, a
        // wyniki lokalne scala z PUSTA lista zdalna.
        var scalone = SearchWindow.MergeSearchResults(
            [new MediaSearchResult(sesja, lokalne)],
            zKatalogu.Select(item => new SearchWindow.SearchResult("spotify", item)),
            [sesja],
            podcastOnly: false);
        Check(scalone.Count == 1 && ReferenceEquals(scalone[0].Item, lokalne),
            "Blad katalogu Spotify wyczyscil wyniki lokalne");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private const string FixtureJson = """
    {
      "artists": { "items": [ { "id": "art1", "name": "Budka Suflera", "type": "artist" } ] },
      "albums": { "items": [ { "id": "alb1", "name": "Cien wielkiej gory", "type": "album",
        "artists": [ { "id": "art1", "name": "Budka Suflera" } ] } ] },
      "tracks": { "items": [ { "id": "trk1", "name": "Jolka, Jolka", "type": "track",
        "duration_ms": 245000, "is_playable": true,
        "album": { "id": "alb1", "name": "Cien wielkiej gory" },
        "artists": [ { "id": "art1", "name": "Budka Suflera" } ] } ] },
      "playlists": { "items": [ { "id": "pl1", "name": "Polski rock", "type": "playlist",
        "owner": { "id": "michal", "display_name": "Michal" } } ] },
      "shows": { "items": [ { "id": "shw1", "name": "Tyfloprzeglad", "type": "show",
        "publisher": "Tyfloswiat" } ] },
      "episodes": { "items": [ { "id": "ep1", "name": "Odcinek 100", "type": "episode",
        "duration_ms": 3600000, "show": { "id": "shw1", "name": "Tyfloprzeglad" } } ] }
    }
    """;

    private sealed class SearchHandler(string body) : HttpMessageHandler
    {
        internal List<string> Urls { get; } = [];
        internal string Body { get; set; } = body;
        internal HttpStatusCode? Fail { get; set; }
        internal Exception? Throw { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // AbsoluteUri, NIE ToString(): ToString() rozkodowuje %20 z powrotem
            // na spacje, wiec asercja o kodowaniu zapytania mierzylaby wtedy
            // tekst, ktorego w sieci nie ma.
            Urls.Add(request.RequestUri!.AbsoluteUri);
            if (Throw is not null) throw Throw;
            if (Fail is { } kod)
                return Task.FromResult(new HttpResponseMessage(kod) { Content = new StringContent("{}") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json")
            });
        }
    }
}
