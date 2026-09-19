using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Podcasty Spotify: zapisane podcasty i zapisane odcinki, przejscie do
/// odcinkow BEZ sesji podcastow RSS, oraz dopuszczenie odcinka do obu silnikow.
///
/// Mierzone jest to, czego uzytkownik z czytnikiem faktycznie doswiadcza, i to
/// osobno dla kazdego ogniwa - zadne nie dowodzi pozostalych:
/// 1. HTTP: adresy /me/shows, /me/episodes i /shows/{id}/episodes z limitem w
///    granicach endpointu oraz stronicowanie DOKLADNIE adresem "next";
/// 2. parsowanie: rodzaj pozycji, adres odtwarzania (spotify:episode:...),
///    adres publiczny, PODCAST NADRZEDNY odcinka i opisy;
/// 3. widok: zapisane podcasty i zapisane odcinki razem, w kolejnosci
///    zrozumialej dla czytnika, z wlasna nazwa widoku;
/// 4. bramka odtwarzania OBU silnikow: odcinek gra, a album/wykonawca/playlista
///    /podcast nadal sa kontenerami i NIE zaczynaja niechcianego odtwarzania;
/// 5. PRAWDZIWY adapter Librespot na atrapie transportu: sprawdzamy, jaki URI
///    rzeczywiscie poszedl do hosta, nie sam komunikat.
///
/// Konta tu nie ma: token jest jawnie fikcyjny, transport i HTTP to atrapy.
/// Ten zestaw NIE dowodzi odsluchu prawdziwego podcastu.
/// </summary>
internal static class SpotifyPodcastsTests
{
    private const string FikcyjnyToken = "FIKCYJNY-TOKEN-TESTOWY-nie-jest-poswiadczeniem";

    internal static void Run() => Task.Run(RunAsync).GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        await ZapisanePodcastyIOdcinkiZHttp();
        await OdcinkiPodcastuStronicujaIZnajaRodzica();
        WidokZapisanychPodcastowIOdcinkow();
        BramkaOdtwarzaniaDopuszczaOdcinekIBroniKontenerow();
        await PrawdziwyAdapterLibrespotWysylaAdresOdcinka();
        Console.WriteLine("OK: podcasty Spotify"
            + " (zapisane podcasty i odcinki z HTTP, stronicowanie adresem next,"
            + " podcast nadrzędny i opisy, widok zapisanych podcastów,"
            + " odcinek dopuszczony w obu silnikach, kontenery nadal nieodtwarzane,"
            + " a do hosta Librespot idzie adres spotify:episode:...)");
    }

    // ---------- 1 i 2: HTTP, parsowanie, stronicowanie, rodzic, opisy ----------

    private static async Task ZapisanePodcastyIOdcinkiZHttp()
    {
        using var handler = new AtrapaSpotify();
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);

        var biblioteka = await api.GetSavedPodcastsAsync(FikcyjnyToken, CancellationToken.None);

        Check(biblioteka is not null, "Zapisane podcasty wrocily jako brak dostepu zamiast danych");
        var shows = biblioteka!.Shows;
        var odcinki = biblioteka.Episodes;

        // HTTP: oba adresy publiczne, oba stronicowane adresem "next".
        var adresy = handler.Urls;
        Check(adresy.Any(url => url.Contains("/me/shows", StringComparison.Ordinal)),
            $"Nie wyslano zapytania o zapisane podcasty: {string.Join(" | ", adresy)}");
        Check(adresy.Any(url => url.Contains("/me/episodes", StringComparison.Ordinal)),
            $"Nie wyslano zapytania o zapisane odcinki: {string.Join(" | ", adresy)}");
        Check(adresy.Contains(AtrapaSpotify.NextShows),
            $"Druga strona podcastow nie poszla adresem \"next\" od Spotify: {string.Join(" | ", adresy)}");
        var pierwszeShows = adresy.First(url => url.Contains("/me/shows", StringComparison.Ordinal));
        var limit = Limit(pierwszeShows);
        Check(limit is >= 1 and <= 50,
            $"Limit zapytania o zapisane podcasty poza zakresem 1-50: {pierwszeShows}");

        // Parsowanie podcastow: kontener, wydawca w miejscu wykonawcy, adres
        // publiczny, BRAK adresu odtwarzania (podcast sie nie odtwarza).
        Check(shows.Count == 3, $"Pobrano {shows.Count} podcastow zamiast trzech");
        Check(shows.All(item => item.Kind == MediaItemKind.Podcast),
            "Pozycja z /me/shows nie jest podcastem");
        var pierwszy = shows[0];
        Check(pierwszy.Title == "Tyflopodcast", $"Zly tytul podcastu: {pierwszy.Title}");
        Check(pierwszy.Artist == "Tyflopodcast", $"Podcast stracil wydawce: {pierwszy.Artist}");
        Check(pierwszy.ExternalId == "show1", "Podcast bez identyfikatora katalogu");
        Check(pierwszy.PublicUri == "https://open.spotify.com/show/show1",
            $"Zly adres publiczny podcastu: {pierwszy.PublicUri}");
        Check(string.IsNullOrEmpty(pierwszy.Source),
            "Podcast nie moze miec adresu odtwarzania - jest kontenerem, nie nagraniem");
        Check(shows.All(item => item.IsInLibrary),
            "Zapisany podcast musi byc oznaczony jako pozycja biblioteki");

        // Parsowanie odcinkow: adres spotify:episode:, czas, PODCAST NADRZEDNY.
        Check(odcinki.Count == 2, $"Pobrano {odcinki.Count} zapisanych odcinkow zamiast dwoch");
        var odcinek = odcinki[0];
        Check(odcinek.Kind == MediaItemKind.Episode, "Pozycja z /me/episodes nie jest odcinkiem");
        Check(odcinek.Source == "spotify:episode:ep1",
            $"Odcinek musi miec adres spotify:episode:..., a ma: {odcinek.Source}");
        Check(odcinek.PublicUri == "https://open.spotify.com/episode/ep1",
            $"Zly adres publiczny odcinka: {odcinek.PublicUri}");
        Check(odcinek.Duration == TimeSpan.FromMilliseconds(3_600_000),
            "Odcinek stracil czas trwania");
        Check(odcinek.RelatedAlbumExternalId == "show1",
            "Zapisany odcinek musi znac podcast nadrzedny - inaczej \"przejdz do podcastu\" jest martwe");
        Check(odcinek.RelatedAlbumTitle == "Tyflopodcast",
            $"Odcinek stracil nazwe podcastu nadrzednego: {odcinek.RelatedAlbumTitle}");
        Check(odcinek.Artist == "Tyflopodcast",
            "Wiersz odcinka musi czytac nazwe podcastu tam, gdzie u utworu stoi wykonawca");
        Check(odcinki[1].IsAvailable == false,
            "Odcinek niedostepny w regionie musi zostac na liscie oznaczony jako niedostepny");

        // Rodzic odcinka MUSI dawac ten sam klucz pojemnika, ktorego uzywa
        // pamiec pozycji - inaczej ustawienie na podcascie nie obejmuje odcinkow.
        Check(SpotifyPlaybackSettingsResolver.ContainerKey(odcinek) == "spotify:show:show1",
            "Klucz pojemnika odcinka nie wskazuje na podcast");

        // Opisy: czytnik ma miec co przeczytac pod Alt+D, oddzielnie dla
        // podcastu i dla odcinka.
        var opisy = api.PodcastDescriptions;
        Check(opisy.TryGetValue("spotify:show:show1", out var opisPodcastu)
            && opisPodcastu.Contains("niewidomych", StringComparison.OrdinalIgnoreCase),
            "Brak opisu zapisanego podcastu");
        Check(opisy.TryGetValue("spotify:episode:ep1", out var opisOdcinka)
            && opisOdcinka.Contains("NVDA", StringComparison.Ordinal),
            "Brak opisu zapisanego odcinka");
    }

    private static async Task OdcinkiPodcastuStronicujaIZnajaRodzica()
    {
        using var handler = new AtrapaSpotify();
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);

        var odcinki = await api.GetShowEpisodesAsync(FikcyjnyToken, "show1", CancellationToken.None);

        Check(odcinki is not null, "Odcinki podcastu wrocily jako brak dostepu");
        Check(odcinki!.Count == 3, $"Pobrano {odcinki.Count} odcinkow zamiast trzech (dwie strony)");
        Check(handler.Urls.Contains(AtrapaSpotify.NextShowEpisodes),
            $"Druga strona odcinkow nie poszla adresem \"next\": {string.Join(" | ", handler.Urls)}");
        Check(odcinki.All(item => item.Kind == MediaItemKind.Episode),
            "Pozycja z listy odcinkow podcastu nie jest odcinkiem");
        Check(odcinki.All(item => item.Source == $"spotify:episode:{item.ExternalId}"),
            "Odcinek z listy podcastu nie ma adresu odtwarzania spotify:episode:...");
        Check(odcinki.All(item => item.Artist == "Tyflopodcast"),
            "Odcinek w widoku podcastu musi czytac nazwe podcastu");
        Check(odcinki.All(item => item.RelatedAlbumExternalId == "show1"
                && item.RelatedAlbumTitle == "Tyflopodcast"),
            "Odcinek otwarty z podcastu musi znac swojego rodzica");
        Check(odcinki.All(item => !item.IsFavorite),
            "Odcinek z katalogu podcastu nie moze udawac zapisanego na koncie");
        Check(api.PodcastDescriptions.ContainsKey("spotify:episode:ep10"),
            "Odcinek z listy podcastu nie oddal opisu");
        Check(api.PodcastDescriptions.TryGetValue("spotify:show:show1", out var opis)
            && opis.Length > 0,
            "Otwarcie podcastu musi oddac takze jego wlasny opis");
    }

    // ---------- 3: widok zapisanych podcastow i odcinkow ----------

    private static void WidokZapisanychPodcastowIOdcinkow()
    {
        var pozycje = new[]
        {
            Utwor("t1", "Utwor"),
            Odcinek("ep2", "Odcinek zapisany B", zapisany: true),
            Podcast("show2", "Zzz podcast"),
            Odcinek("ep1", "Odcinek zapisany A", zapisany: true),
            Podcast("show1", "Aaa podcast"),
            Odcinek("ep9", "Odcinek z katalogu", zapisany: false)
        };

        var widok = MainWindow.BuildSpotifyPodcastsView(pozycje);

        Check(widok.Count == 4,
            $"Widok ma {widok.Count} pozycji zamiast czterech (dwa podcasty i dwa zapisane odcinki)");
        Check(widok[0].Kind == MediaItemKind.Podcast && widok[1].Kind == MediaItemKind.Podcast,
            "Zapisane podcasty musza byc na poczatku widoku");
        Check(widok[0].Title == "Aaa podcast" && widok[1].Title == "Zzz podcast",
            "Podcasty musza byc uporzadkowane alfabetycznie");
        Check(widok[2].Kind == MediaItemKind.Episode && widok[3].Kind == MediaItemKind.Episode,
            "Zapisane odcinki musza byc po podcastach, nie przemieszane");
        Check(widok.All(item => item.Kind is MediaItemKind.Podcast or MediaItemKind.Episode),
            "Widok podcastow nie moze pokazywac utworow");
        Check(widok.All(item => item.Title != "Odcinek z katalogu"),
            "Widok zapisanych odcinkow nie moze pokazywac odcinkow pobranych tylko do podgladu");

        Check(MainWindow.SpotifyPodcastsViewName("spotify") == "Spotify:podcasty",
            "Widok podcastow Spotify musi miec wlasna, stala nazwe");
        Check(MainWindow.SpotifyPodcastsViewName("spotifyLibrespot") == "Spotify:spotifyLibrespot:podcasty",
            "Druga sesja Spotify musi miec wlasny widok podcastow, nie wspolny");
        Check(MainWindow.IsSpotifyPodcastsView(MainWindow.SpotifyPodcastsViewName("spotifyLibrespot")),
            "Widok podcastow drugiej sesji nie jest rozpoznawany");

        // Metoda do podpiecia przez menu i skrot musi istniec w oknie glownym.
        // Jest publiczna, bo tej samej metody wola router polecen (Ctrl+Alt+O)
        // przez IApplicationActions - szukamy wiec obu widocznosci.
        var hook = typeof(MainWindow).GetMethod(
            "ShowSpotifyPodcasts",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public);
        Check(hook is not null,
            "Brak metody ShowSpotifyPodcasts() do podpiecia pod menu i skrot");

        // Opis pod Alt+D: podcast i odcinek czytaja SWOJ opis, a nie kanal RSS.
        var podcast = Podcast("show1", "Aaa podcast");
        var odcinek = Odcinek("ep1", "Odcinek zapisany A", zapisany: true);
        var cache = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["spotify:show:show1"] = "Opis podcastu z katalogu Spotify",
            ["spotify:episode:ep1"] = "Opis odcinka z katalogu Spotify"
        };
        Check(MainWindow.SpotifyPodcastDescription(cache, podcast) == "Opis podcastu z katalogu Spotify",
            "Podcast Spotify nie oddaje wlasnego opisu");
        Check(MainWindow.SpotifyPodcastDescription(cache, odcinek) == "Opis odcinka z katalogu Spotify",
            "Odcinek Spotify nie oddaje wlasnego opisu");
        Check(MainWindow.SpotifyPodcastDescription(cache, Utwor("t1", "Utwor")) is null,
            "Opis podcastu nie moze byc podawany dla utworu");
    }

    // ---------- 4: bramka odtwarzania obu silnikow ----------

    private static void BramkaOdtwarzaniaDopuszczaOdcinekIBroniKontenerow()
    {
        Check(SpotifyMediaOutput.PlaybackRejection(Odcinek("ep1", "Odcinek", zapisany: true)) is null,
            "Odcinek podcastu musi byc dopuszczony do odtwarzania");
        Check(SpotifyMediaOutput.PlaybackRejection(Utwor("t1", "Utwor")) is null,
            "Utwor musi pozostac odtwarzalny");

        foreach (var (kind, nazwa) in new[]
        {
            (MediaItemKind.Album, "album"),
            (MediaItemKind.Artist, "wykonawca"),
            (MediaItemKind.Playlist, "playlista"),
            (MediaItemKind.Podcast, "podcast")
        })
        {
            var kontener = new MediaItem
            {
                Title = nazwa,
                Kind = kind,
                ExternalId = "X",
                Source = $"spotify:{nazwa}:X"
            };
            var powod = SpotifyMediaOutput.PlaybackRejection(kontener);
            Check(powod is not null, $"Kontener ({nazwa}) nie moze byc odtwarzany wprost");
            Check(powod!.Contains("strzałką w prawo", StringComparison.Ordinal),
                $"Komunikat dla kontenera ({nazwa}) musi powiedziec, co zrobic: {powod}");
        }

        // Odcinek bez zadnego identyfikatora to blad danych, nie kontener.
        var pusty = new MediaItem { Title = "Bez adresu", Kind = MediaItemKind.Episode };
        Check(SpotifyMediaOutput.PlaybackRejection(pusty) is not null,
            "Odcinek bez adresu i identyfikatora nie moze byc wyslany do silnika");

        // URI: odcinek NIE moze pojsc jako spotify:track:
        Check(SpotifyMediaOutput.PlaybackTrackUri(new MediaItem
        {
            Title = "Odcinek bez adresu zrodlowego",
            Kind = MediaItemKind.Episode,
            ExternalId = "ep7"
        }) == "spotify:episode:ep7",
            "Odcinek zlozony z identyfikatora musi dostac przedrostek episode, nie track");
    }

    // ---------- 5: prawdziwy adapter na atrapie transportu ----------

    private static async Task PrawdziwyAdapterLibrespotWysylaAdresOdcinka()
    {
        using var host = new AtrapaHostaLibrespot();
        using var client = new LibrespotHostClient(
            () => host,
            new LibrespotHostOptions
            {
                ReadyTimeout = TimeSpan.FromSeconds(5),
                RequestTimeout = TimeSpan.FromSeconds(5),
                ShutdownTimeout = TimeSpan.FromMilliseconds(200)
            },
            _ => Task.FromResult(FikcyjnyToken));
        host.EmitReady();
        await client.StartAsync();
        host.Client = client;
        host.Attach();

        using var output = new SpotifyLibrespotMediaOutput(client, action => action());
        var bledy = new List<MediaOutputFailedEventArgs>();
        var przygotowania = new List<MediaPlaybackPreparingEventArgs>();
        output.PlaybackFailed += (_, e) => bledy.Add(e);
        output.PlaybackPreparing += (_, e) => przygotowania.Add(e);

        // Odcinek: adres MUSI faktycznie dojsc do hosta jako spotify:episode:...
        var odcinek = Odcinek("ep1", "Odcinek", zapisany: true);
        output.Play(odcinek, TimeSpan.FromSeconds(12), 60, 1d);
        var play = host.CzekajNaPolecenie("play");
        Check(play.Uri == "spotify:episode:ep1",
            $"Do hosta poszedl adres \"{play.Uri}\" zamiast spotify:episode:ep1");
        Check(play.PositionMs == 12_000,
            $"Host dostal pozycje {play.PositionMs} ms zamiast 12000 - wznowienie odcinka nie dziala");
        Check(przygotowania.Count == 1, "Odcinek musi zglosic przygotowanie odtwarzania");
        Check(bledy.Count == 0, "Poprawny odcinek nie moze zglaszac bledu");

        // Podcast: kontener. ZADNE polecenie odtwarzania nie moze wyjsc.
        var przedPodcastem = host.LiczbaPolecen("play");
        output.Play(Podcast("show1", "Tyflopodcast"), TimeSpan.Zero, 60, 1d);
        Check(host.LiczbaPolecen("play") == przedPodcastem,
            "Podcast wyslal polecenie odtwarzania do hosta - to niechciane odtwarzanie kontenera");
        Check(bledy.Count == 1 && bledy[0].Message.Contains("strzałką w prawo", StringComparison.Ordinal),
            "Podcast musi dostac komunikat mowiacy, jak wejsc do odcinkow");
        Check(przygotowania.Count == 1, "Kontener nie moze zglaszac przygotowania odtwarzania");
    }

    // ---------- narzedzia ----------

    private static MediaItem Utwor(string id, string tytul) => new()
    {
        Title = tytul,
        Kind = MediaItemKind.Track,
        ExternalId = id,
        Source = $"spotify:track:{id}"
    };

    private static MediaItem Podcast(string id, string tytul) => new()
    {
        Title = tytul,
        Kind = MediaItemKind.Podcast,
        ExternalId = id,
        PublicUri = $"https://open.spotify.com/show/{id}",
        IsInLibrary = true
    };

    private static MediaItem Odcinek(string id, string tytul, bool zapisany) => new()
    {
        Title = tytul,
        Kind = MediaItemKind.Episode,
        ExternalId = id,
        Source = $"spotify:episode:{id}",
        PublicUri = $"https://open.spotify.com/episode/{id}",
        IsFavorite = zapisany,
        RelatedAlbumExternalId = "show1",
        RelatedAlbumTitle = "Tyflopodcast"
    };

    private static int? Limit(string url)
    {
        foreach (var para in new Uri(url).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var podzial = para.Split('=', 2);
            if (podzial.Length == 2 && podzial[0] == "limit" && int.TryParse(podzial[1], out var wartosc))
                return wartosc;
        }
        return null;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Atrapa HTTP Spotify dla podcastow. Oddaje po dwie strony dla kazdej
    /// kolekcji i odrzuca limit poza zakresem 1-50 tak jak serwer, zeby test
    /// naprawde lapal blad zapytania, a nie tylko parser.
    /// </summary>
    private sealed class AtrapaSpotify : HttpMessageHandler
    {
        internal const string NextShows =
            "https://api.spotify.com/v1/me/shows?offset=2&limit=2";
        internal const string NextEpisodes =
            "https://api.spotify.com/v1/me/episodes?offset=1&limit=1";
        internal const string NextShowEpisodes =
            "https://api.spotify.com/v1/shows/show1/episodes?offset=2&limit=2";

        internal List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Add(url);
            var limit = Limit(url);
            if (url.Contains('?', StringComparison.Ordinal) && limit is not null and (< 1 or > 50))
                return Task.FromResult(Blad(HttpStatusCode.BadRequest, "Invalid limit"));

            var body = url switch
            {
                NextShows => Strona(
                    """{"added_at":"2026-02-01T10:00:00Z","show":{"id":"show3","name":"Trzeci podcast","publisher":"Wydawca 3","description":"Opis trzeciego podcastu","type":"show"}}""",
                    null),
                NextEpisodes => Strona(
                    """{"added_at":"2026-02-02T10:00:00Z","episode":{"id":"ep2","name":"Odcinek drugi","description":"Opis drugiego odcinka","duration_ms":1800000,"is_playable":false,"type":"episode","show":{"id":"show1","name":"Tyflopodcast","publisher":"Tyflopodcast","type":"show"}}}""",
                    null),
                NextShowEpisodes => Strona(
                    """{"id":"ep12","name":"Odcinek dwunasty","description":"Opis dwunastego","duration_ms":1200000,"type":"episode"}""",
                    null),
                _ when url.Contains("/me/shows", StringComparison.Ordinal) => Strona(
                    """{"added_at":"2026-01-01T10:00:00Z","show":{"id":"show1","name":"Tyflopodcast","publisher":"Tyflopodcast","description":"Podcast o technologiach dla niewidomych","type":"show"}},"""
                    + """{"added_at":"2026-01-02T10:00:00Z","show":{"id":"show2","name":"Drugi podcast","publisher":"Wydawca 2","description":"Opis drugiego podcastu","type":"show"}}""",
                    NextShows),
                _ when url.Contains("/me/episodes", StringComparison.Ordinal) => Strona(
                    """{"added_at":"2026-01-03T10:00:00Z","episode":{"id":"ep1","name":"Odcinek pierwszy","description":"Odcinek o czytniku NVDA","duration_ms":3600000,"is_playable":true,"type":"episode","show":{"id":"show1","name":"Tyflopodcast","publisher":"Tyflopodcast","type":"show"}}}""",
                    NextEpisodes),
                _ when url.EndsWith("/shows/show1", StringComparison.Ordinal) =>
                    """{"id":"show1","name":"Tyflopodcast","publisher":"Tyflopodcast","description":"Podcast o technologiach dla niewidomych","type":"show"}""",
                _ when url.Contains("/shows/show1/episodes", StringComparison.Ordinal) => Strona(
                    """{"id":"ep10","name":"Odcinek dziesiaty","description":"Opis dziesiatego","duration_ms":600000,"type":"episode"},"""
                    + """{"id":"ep11","name":"Odcinek jedenasty","description":"Opis jedenastego","duration_ms":900000,"type":"episode"}""",
                    NextShowEpisodes),
                _ => null
            };

            if (body is null) return Task.FromResult(Blad(HttpStatusCode.NotFound, "Not found"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        private static HttpResponseMessage Blad(HttpStatusCode code, string message) =>
            new(code)
            {
                Content = new StringContent(
                    $$$"""{"error":{"status":{{{(int)code}}},"message":"{{{message}}}"}}""",
                    Encoding.UTF8,
                    "application/json")
            };

        private static string Strona(string pozycje, string? next)
        {
            var nextJson = next is null ? "null" : $"\"{next}\"";
            return $$"""{"href":"x","limit":2,"offset":0,"total":3,"next":{{nextJson}},"previous":null,"items":[{{pozycje}}]}""";
        }
    }

    /// <summary>
    /// Atrapa procesu hosta Librespot. Minimalna: potwierdza kazde polecenie i
    /// daje odczytac, co NAPRAWDE zostalo wyslane.
    /// </summary>
    private sealed class AtrapaHostaLibrespot : ILibrespotHostProcess
    {
        private readonly object gate = new();
        private readonly StringBuilder input = new();
        private readonly List<JsonObject> odebrane = [];
        private readonly SemaphoreSlim dostepne = new(0);
        private readonly Queue<string> linie = new();
        private bool podlaczony;

        internal LibrespotHostClient? Client { get; set; }

        public AtrapaHostaLibrespot()
        {
            StandardOutput = new Czytelnik(this);
            StandardInput = new Pisarz(this);
        }

        public TextWriter StandardInput { get; }
        public TextReader StandardOutput { get; }
        public bool HasExited { get; private set; }
        public int? ExitCode => HasExited ? 0 : null;

        internal void Attach() => podlaczony = true;
        public void Kill() => HasExited = true;
        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            HasExited = true;
            return Task.CompletedTask;
        }
        public void Dispose() => HasExited = true;

        internal void EmitReady() => Emit(new JsonObject
        {
            ["type"] = "ready",
            ["protocolVersion"] = LibrespotHostContract.ProtocolVersion
        });

        internal void Emit(JsonObject payload)
        {
            lock (gate) linie.Enqueue(payload.ToJsonString() + "\n");
            dostepne.Release();
        }

        internal int LiczbaPolecen(string name)
        {
            lock (gate)
                return odebrane.Count(o => o["command"]?.GetValue<string>() == name);
        }

        internal Polecenie CzekajNaPolecenie(string name)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                lock (gate)
                {
                    var znalezione = odebrane.FirstOrDefault(
                        o => o["command"]?.GetValue<string>() == name);
                    if (znalezione is not null) return new Polecenie(znalezione);
                }
                Thread.Sleep(5);
            }
            throw new InvalidOperationException($"Host nie odebral polecenia \"{name}\".");
        }

        private void OnLine(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) return;
            JsonObject request;
            try
            {
                request = JsonNode.Parse(trimmed) as JsonObject ?? new JsonObject();
            }
            catch (System.Text.Json.JsonException)
            {
                return;
            }
            lock (gate) odebrane.Add(request);
            if (!podlaczony) return;
            var requestId = request["requestId"] is JsonValue value
                && value.TryGetValue(out long identifier) ? identifier : 0L;
            Emit(new JsonObject { ["type"] = "ack", ["requestId"] = requestId });
        }

        internal sealed record Polecenie(JsonObject Raw)
        {
            public string Uri => Raw["uri"]?.GetValue<string>() ?? string.Empty;
            public long PositionMs
            {
                get
                {
                    if (Raw["positionMs"] is not JsonValue value) return 0;
                    if (value.TryGetValue(out long number)) return number;
                    return value.TryGetValue(out int small) ? small : 0;
                }
            }
        }

        private sealed class Pisarz(AtrapaHostaLibrespot host) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                string? pelna = null;
                lock (host.gate)
                {
                    host.input.Append(value);
                    if (value == '\n')
                    {
                        var czesci = host.input.ToString().Split('\n');
                        pelna = czesci.Length >= 2 ? czesci[^2] : null;
                    }
                }
                if (pelna is not null) host.OnLine(pelna);
            }

            public override void Write(string? value)
            {
                if (value is null) return;
                foreach (var character in value) Write(character);
            }

            public override void WriteLine(string? value)
            {
                Write(value);
                Write('\n');
            }

            public override Task WriteLineAsync(string? value)
            {
                WriteLine(value);
                return Task.CompletedTask;
            }

            public override Task FlushAsync() => Task.CompletedTask;
        }

        private sealed class Czytelnik(AtrapaHostaLibrespot host) : TextReader
        {
            private string bufor = string.Empty;
            private int offset;

            public override async Task<int> ReadAsync(char[] destination, int index, int count)
            {
                while (offset >= bufor.Length)
                {
                    await host.dostepne.WaitAsync().ConfigureAwait(false);
                    lock (host.gate) bufor = host.linie.Dequeue();
                    offset = 0;
                }
                var skopiowane = Math.Min(count, bufor.Length - offset);
                bufor.CopyTo(offset, destination, index, skopiowane);
                offset += skopiowane;
                return skopiowane;
            }

            public override int Read(char[] destination, int index, int count) =>
                ReadAsync(destination, index, count).GetAwaiter().GetResult();
        }
    }
}
