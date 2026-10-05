using System.Text.Json;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// TRANSPORT zywej kolejki: glosnosc, tempo, zatrzymanie, odciecie od
/// bezposredniego odtwarzania i stan "wczytano kolejke".
///
/// Te testy mierza ARGUMENTY, ktore host wydaje silnikowi
/// (<see cref="IMediaOutput.Play"/>: Id, pozycja, glosnosc, tempo), a nie samo
/// pole w odpowiedzi protokolu. Etykieta w statusie moze byc poprawna przy
/// zupelnie innym dzwieku.
///
/// To NIE jest pelny dowod na zywym hoscie: warstwa protokolu celowo nie zna
/// Windows. Przebieg na prawdziwym <c>WindowsMediaOutput</c> jest osobno, w
/// raporcie.
/// </summary>
internal static class QueueTransportTests
{
    /// <summary>
    /// Wyjscie zapisujace wywolania. Poza <c>Play</c> zapisuje takze ZMIANY
    /// glosnosci i tempa w trakcie, bo wlasnie one gina, gdy host omija sesje.
    /// </summary>
    private sealed class RecordingOutput : IMediaOutput
    {
        public List<(string Id, TimeSpan Position, int Volume, double Rate)> Plays { get; } = [];
        public List<int> Volumes { get; } = [];
        public List<double> Rates { get; } = [];
        public int Pauses { get; private set; }
        public int Stops { get; private set; }
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; set; }
        public bool SupportsPlaybackRate => true;

        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
        {
            Plays.Add((item.Id, position, volume, playbackRate));
            LoadedItemId = item.Id;
            Position = position;
        }

        public void Pause() => Pauses++;
        public void Stop() { Stops++; LoadedItemId = null; }
        public void Seek(TimeSpan position) => Position = position;
        public void SetVolume(int volume) => Volumes.Add(volume);
        public void SetPlaybackRate(double playbackRate) => Rates.Add(playbackRate);
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string _folder = string.Empty;

    private static string PathOf(string name) => Path.Combine(_folder, name);

    private static string Json(string value) => JsonSerializer.Serialize(value);

    private static string ThreeRows => $$"""
        {"sessionId":"local","items":[
          {"id":"file:B.wav","title":"B utwor","path":{{Json(PathOf("B.wav"))}},"isInQueue":true},
          {"id":"file:A.wav","title":"A utwor","path":{{Json(PathOf("A.wav"))}},"isInQueue":true},
          {"id":"file:C.wav","title":"C utwor","path":{{Json(PathOf("C.wav"))}},"isInQueue":true}
        ],"order":["file:B.wav","file:A.wav","file:C.wav"]}
        """;

    public static void Run()
    {
        _folder = Path.Combine(Path.GetTempPath(), "amc-transport-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_folder);
        try
        {
            foreach (var name in new[] { "A.wav", "B.wav", "C.wav" })
            {
                File.WriteAllBytes(PathOf(name), []);
            }
            RunSuites();
        }
        finally
        {
            try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
        }
    }

    private static void RunSuites()
    {
        GlosnoscITempoZZadaniaDochodzaDoSilnika();
        GlosnoscITempoTrwajaPrzezNaturalneNastepstwo();
        GlosnoscITempoPrzezywajaPauzeIWznowienie();
        ZmianaParametrowWTrakcieObowiazujeNastepnyUtwor();
        StopZatrzymujeSesjeKolejkiAWznowienieWracaDoPozycji();
        PoWyjsciuPozaKolejkeStaryTransportJejNieWznawia();
        OdlaczonaKolejkaNieTwierdziZeGraAleZachowujeWiersze();
        SwiadomyPlayAtPrzywracaProwadzenieKolejki();
        StanWczytaniaKolejkiJestJawny();
        BiezacyMaterialToPRAWDZIWAPozycjaKolejki();
        SpoznionyKoniecPoPonownymStarcieTegoSamegoIdNiePrzesuwa();
        PrzelaczenieWTrakcieKonczacegoSieUtworuNieGubiProwadzenia();
        Console.WriteLine("QueueTransportTests: OK");
    }

    /// <summary>
    /// PUNKT 1 (wejscie). <c>queue.playAt</c> z glosnoscia 0 i tempem 1,5 musi
    /// wydac silnikowi DOKLADNIE te wartosci. Dotad argumenty byly ignorowane i
    /// sesja grala swoim domyslnym 35 oraz tempem 1.
    /// </summary>
    private static void GlosnoscITempoZZadaniaDochodzaDoSilnika()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));

        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":0,"rate":1.5}"""));

        Assert.True(output.Plays.Count == 1, "Enter wywoluje Play dokladnie raz");
        Assert.True(output.Plays[0].Volume == 0,
            $"Play dostal glosnosc z zadania (0), a nie domyslna sesji; bylo {output.Plays[0].Volume}");
        Assert.True(Math.Abs(output.Plays[0].Rate - 1.5d) < 0.001d,
            $"Play dostal tempo z zadania (1,5); bylo {output.Plays[0].Rate}");
    }

    /// <summary>
    /// PUNKT 1 (naturalne nastepstwo). Kolejny utwor tej samej sesji gra TYMI
    /// SAMYMI parametrami: uzytkownik nie ustawia glosnosci po kazdym utworze.
    /// </summary>
    private static void GlosnoscITempoTrwajaPrzezNaturalneNastepstwo()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":0,"rate":1.5}"""));

        var next = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(next?.Id == "file:A.wav", "po B kolejka idzie na A");
        var play = output.Plays[^1];
        Assert.True(play.Id == "file:A.wav", "ostatni Play dotyczy A");
        Assert.True(play.Volume == 0,
            $"naturalne nastepstwo zachowalo glosnosc 0; bylo {play.Volume}");
        Assert.True(Math.Abs(play.Rate - 1.5d) < 0.001d,
            $"naturalne nastepstwo zachowalo tempo 1,5; bylo {play.Rate}");
    }

    /// <summary>PUNKT 1 (pauza/wznowienie) -- wznowienie nie wraca do 35 i tempa 1.</summary>
    private static void GlosnoscITempoPrzezywajaPauzeIWznowienie()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":0,"rate":1.5}"""));

        output.Position = TimeSpan.FromSeconds(12);
        queue.PauseResume();
        Assert.True(output.Pauses == 1, "pauza doszla do silnika");
        queue.PauseResume();

        var play = output.Plays[^1];
        Assert.True(play.Id == "file:B.wav", "wznowienie dotyczy tego samego utworu");
        Assert.True(play.Position.TotalSeconds > 11d,
            $"wznowienie wraca na zapamietana pozycje; bylo {play.Position}");
        Assert.True(play.Volume == 0, $"wznowienie zachowalo glosnosc 0; bylo {play.Volume}");
        Assert.True(Math.Abs(play.Rate - 1.5d) < 0.001d,
            $"wznowienie zachowalo tempo 1,5; bylo {play.Rate}");
    }

    /// <summary>
    /// PUNKT 1 (zamiana w trakcie). Glosnosc i tempo zmienione PODCZAS gry
    /// kolejki musza obowiazywac nastepny utwor tej sesji. Dotad
    /// <c>transport.setVolume</c> ruszalo samo wyjscie, wiec kolejny Play
    /// wracal do wartosci sesji.
    /// </summary>
    private static void ZmianaParametrowWTrakcieObowiazujeNastepnyUtwor()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":0,"rate":1.5}"""));

        Assert.True(queue.SetVolume(80), "kolejka prowadzi transport, wiec przyjmuje glosnosc");
        Assert.True(queue.SetRate(0.75d), "kolejka prowadzi transport, wiec przyjmuje tempo");
        Assert.True(output.Volumes.Contains(80), "zmiana glosnosci doszla do silnika od razu");
        Assert.True(output.Rates.Any(rate => Math.Abs(rate - 0.75d) < 0.001d),
            "zmiana tempa doszla do silnika od razu");

        var next = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(next?.Id == "file:A.wav", "po B kolejka idzie na A");
        var play = output.Plays[^1];
        Assert.True(play.Volume == 80,
            $"nastepny utwor gra NOWA glosnoscia 80; bylo {play.Volume}");
        Assert.True(Math.Abs(play.Rate - 0.75d) < 0.001d,
            $"nastepny utwor gra NOWYM tempem 0,75; bylo {play.Rate}");
    }

    /// <summary>
    /// PUNKT 2. <c>transport.stop</c> przy kolejce musi zatrzymac SESJE, nie
    /// tylko wyjscie: inaczej sesja nadal twierdzi, ze gra. Elementy zostaja w
    /// kolejce (kontrakt Core), a wznowienie wraca na zapamietana pozycje.
    /// </summary>
    private static void StopZatrzymujeSesjeKolejkiAWznowienieWracaDoPozycji()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":40,"rate":1.0}"""));

        output.Position = TimeSpan.FromSeconds(9);
        var stopped = queue.Stop();
        Assert.True(output.Stops == 1, "zatrzymanie doszlo do silnika");
        Assert.True(!stopped.Playing, "po zatrzymaniu sesja NIE twierdzi, ze gra");
        Assert.True(!stopped.Paused, "zatrzymanie nie jest pauza");
        Assert.True(stopped.Rows.Count == 3, "elementy zostaja w kolejce po zatrzymaniu");

        var playsBefore = output.Plays.Count;
        queue.PauseResume();
        Assert.True(output.Plays.Count == playsBefore + 1,
            "po zatrzymaniu wznowienie znow gra");
        var play = output.Plays[^1];
        Assert.True(play.Id == "file:B.wav", "wznowienie dotyczy tego samego utworu");
        Assert.True(play.Position.TotalSeconds > 8d,
            $"wznowienie wraca na pozycje z chwili zatrzymania; bylo {play.Position}");
    }

    /// <summary>
    /// PUNKT 4. Odlaczona kolejka nie moze TWIERDZIC, ze gra. Material sesji
    /// (wiersze, biezaca pozycja, wczytanie) zostaje nietkniety -- naprawiamy
    /// sam STAN, bo frontend czyta <c>playing</c> z <c>queue.status</c> i stawia
    /// na tej podstawie etykiete przycisku.
    /// </summary>
    private static void OdlaczonaKolejkaNieTwierdziZeGraAleZachowujeWiersze()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        var playing = queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));
        Assert.True(playing.Playing, "kolejka prowadzaca odtwarzanie mowi, ze gra");

        output.Position = TimeSpan.FromSeconds(4);
        queue.DetachFromDirectPlay();

        var status = queue.Status();
        Assert.True(!status.Playing,
            "po odlaczeniu (files.play/radio) kolejka NIE twierdzi, ze gra");
        Assert.True(!status.Paused,
            "odlaczona kolejka nie jest tez 'wstrzymana'");
        Assert.True(status.Rows.Count == 3,
            $"odlaczenie NIE kasuje wierszy kolejki; bylo {status.Rows.Count}");
        Assert.True(status.CurrentId == "file:B.wav",
            $"odlaczenie NIE kasuje biezacej pozycji; bylo {status.CurrentId}");
        Assert.True(status.Initialized,
            "odlaczenie NIE gubi stanu wczytania kolejki");
    }

    /// <summary>
    /// PUNKT 3. Po wyjsciu poza kolejke (bezposrednie <c>files.play</c>, radio,
    /// zakladka) stary koniec utworu, Nastepny i pauza NIE moga wznowic starej
    /// kolejki. Dotyczy to TAKZE tego samego Id, bo host rozpoznawal wlasciciela
    /// po Id biezacej pozycji sesji.
    /// </summary>
    private static void PoWyjsciuPozaKolejkeStaryTransportJejNieWznawia()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));

        // Uzytkownik wyszedl poza kolejke (host wola to przy files.play i radio.play).
        queue.DetachFromDirectPlay();

        var playsBefore = output.Plays.Count;
        Assert.True(!queue.OwnsCurrent("file:B.wav"),
            "po wyjsciu poza kolejke host NIE uznaje jej za wlasciciela transportu -- nawet dla tego samego Id");
        Assert.True(queue.HandlePlaybackEnded("file:B.wav") is null,
            "stary koniec utworu nie przesuwa odlaczonej kolejki");
        Assert.True(!queue.WouldAdvanceAfter("file:B.wav"),
            "odlaczona kolejka nie zapowiada przejscia");
        Assert.True(!queue.PlayRelative(1),
            "Nastepny nie wznawia odlaczonej kolejki");
        Assert.True(output.Plays.Count == playsBefore,
            "zadna z tych drog nie wydala Play: kolejka milczy, dopoki uzytkownik do niej nie wroci");
    }

    /// <summary>PUNKT 3 (powrot). Swiadomy <c>queue.playAt</c> znow wlacza prowadzenie.</summary>
    private static void SwiadomyPlayAtPrzywracaProwadzenieKolejki()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));
        queue.DetachFromDirectPlay();

        queue.PlayAt(Args("""{"itemId":"file:A.wav","volume":30,"rate":1.0}"""));
        Assert.True(queue.OwnsCurrent("file:A.wav"), "po swiadomym starcie kolejka znow prowadzi");
        Assert.True(queue.WouldAdvanceAfter("file:A.wav"), "i znow zapowiada przejscie");
        var next = queue.HandlePlaybackEnded("file:A.wav");
        Assert.True(next is not null, "i faktycznie prowadzi dalej");
    }

    /// <summary>
    /// PUNKT 6. Jawny stan "czy kolejka byla kiedykolwiek wczytana". Pusta
    /// kolejka PO zuzyciu utworow to NIE to samo co brak wczytania -- frontend
    /// bral to za drugie i przywracal zapisany porzadek, czyli wracaly
    /// skonsumowane utwory.
    /// </summary>
    private static void StanWczytaniaKolejkiJestJawny()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        Assert.True(!queue.Status().Initialized,
            "bez wczytania kolejki initialized = false");

        // ODMOWA nie moze uznac kolejki za wczytana.
        var refused = false;
        try { queue.Set(Args("""{"sessionId":"local"}""")); }
        catch (LiteRequestException) { refused = true; }
        Assert.True(refused, "Set bez \"items\" odmawia");
        Assert.True(!queue.Status().Initialized,
            "po ODMOWIE Set kolejka nadal nie jest wczytana");

        var loaded = queue.Set(Args(ThreeRows));
        Assert.True(loaded.Initialized, "po poprawnym Set initialized = true");
        Assert.True(queue.Status().Initialized, "i tak samo w kolejnym queue.status");

        // Zuzywamy cala kolejke naturalnym koncem utworow.
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":20,"rate":1.0}"""));
        queue.HandlePlaybackEnded("file:B.wav");
        queue.HandlePlaybackEnded("file:A.wav");
        queue.HandlePlaybackEnded("file:C.wav");
        var exhausted = queue.Status();
        Assert.True(exhausted.Rows.Count == 0,
            $"po zuzyciu wszystkich utworow kolejka jest pusta; bylo {exhausted.Rows.Count}");
        Assert.True(exhausted.Initialized,
            "pusta PO zuzyciu kolejka nadal jest WCZYTANA -- frontend nie ma prawa przywrocic zapisanego porzadku");

        // Pusty, ale POPRAWNY wsad tez jest wczytaniem.
        var empty = new LiteQueueCoordinator(new RecordingOutput());
        var emptyStatus = empty.Set(Args("""{"sessionId":"local","items":[],"order":[]}"""));
        Assert.True(emptyStatus.Initialized && emptyStatus.Rows.Count == 0,
            "poprawny pusty wsad jest wczytaniem kolejki o zero wierszy");
    }

    /// <summary>
    /// PUNKT 4 (polowa mierzalna bez Windows). Host musi buforowac PRAWDZIWA
    /// pozycje kolejki -- ta sama instancje, ktora dostal silnik, ze sciezka
    /// zrodla -- a nie obiekt zlozony z samego zadania.
    /// </summary>
    private static void BiezacyMaterialToPRAWDZIWAPozycjaKolejki()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:C.wav","volume":25,"rate":1.0}"""));

        var current = queue.CurrentItem;
        Assert.True(current is not null, "kolejka oddaje biezaca pozycje");
        Assert.True(current!.Id == "file:C.wav", "to WYBRANY wiersz");
        Assert.True(current.Source == PathOf("C.wav"),
            $"biezaca pozycja ma prawdziwa sciezke zrodla; bylo {current.Source ?? "<null>"}");
        Assert.True(output.LoadedItemId == current.Id,
            "silnik ma wczytany dokladnie ten material");
    }

    /// <summary>
    /// PUNKT 5. Token przejscia trzyma samo Id, wiec sam z siebie NIE odroznia
    /// konca sprzed ponownego startu TEGO SAMEGO utworu od konca po nim. Ten
    /// test mierzy, co sie dzieje, gdy spozniony <c>ended</c> dociera PO
    /// ponownym starcie tego samego Id.
    ///
    /// Obrona lezy w silniku: <c>WindowsMediaOutput</c> zglasza koniec tylko
    /// dla tego toru (<c>PlaybackPipeline</c>), ktory JEST biezacy
    /// (<c>ReferenceEquals(_pipeline, pipeline)</c>), i podbija
    /// <c>_requestVersion</c> przy kazdym odlaczeniu. Ponowny start buduje NOWY
    /// tor, wiec stary nigdy nie wyemituje drugiego konca. Nie dokladamy wiec
    /// drugiej bramki w koordynatorze -- mierzymy tylko, ze jedno zdarzenie na
    /// start daje dokladnie jedno przejscie.
    /// </summary>
    private static void SpoznionyKoniecPoPonownymStarcieTegoSamegoIdNiePrzesuwa()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));

        // Ponowny start TEGO SAMEGO Id (uzytkownik wcisnal Enter dwa razy).
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));
        var playsBefore = output.Plays.Count;

        // Pierwszy koniec po ponownym starcie: przejscie ma nastapic RAZ.
        var first = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(first?.Id == "file:A.wav", "koniec B przesuwa na A dokladnie raz");
        Assert.True(output.Plays.Count == playsBefore + 1, "i wydaje jeden Play");

        // POWTORZONY (spozniony) koniec tego samego Id nie moze przesunac dalej.
        var repeated = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(repeated is null, "powtorzony koniec B jest odrzucany");
        Assert.True(output.Plays.Count == playsBefore + 1,
            $"i nie wydaje kolejnego Play; bylo {output.Plays.Count - playsBefore}");
        Assert.True(output.LoadedItemId == "file:A.wav",
            $"silnik zostaje na A; bylo {output.LoadedItemId ?? "<null>"}");
    }

    /// <summary>
    /// PUNKT 5 (przelaczenie w trakcie konca). Gdy uzytkownik przechodzi na
    /// bezposrednie odtwarzanie DOKLADNIE w chwili, gdy konczy sie utwor
    /// kolejki, stary koniec nie ma prawa przesunac kolejki, ale swiadomy
    /// powrot do kolejki musi znow prowadzic.
    /// </summary>
    private static void PrzelaczenieWTrakcieKonczacegoSieUtworuNieGubiProwadzenia()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));

        // Przelaczenie poza kolejke (radio.play / files.play) w trakcie.
        queue.DetachFromDirectPlay();
        var playsAfterDetach = output.Plays.Count;

        // Koniec, ktory "byl juz w drodze", gdy nastapilo przelaczenie.
        Assert.True(queue.HandlePlaybackEnded("file:B.wav") is null,
            "koniec sprzed przelaczenia nie przesuwa kolejki");
        Assert.True(output.Plays.Count == playsAfterDetach,
            "i nie wydaje zadnego Play");

        // Swiadomy powrot do kolejki przywraca prowadzenie i dziala dalej.
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":30,"rate":1.0}"""));
        var next = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(next?.Id == "file:A.wav",
            "po swiadomym powrocie koniec znow prowadzi kolejke");
    }
}
