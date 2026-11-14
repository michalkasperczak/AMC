using System.Text.Json;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// Testy ZYWEJ kolejki odtwarzania hosta (<see cref="LiteQueueCoordinator"/>).
///
/// Kolejnosc, naturalny koniec utworu i Nastepny/Poprzedni NIE sa tu liczone
/// od nowa: koordynator trzyma <see cref="DemoMediaSession"/> z Core, czyli ten
/// sam obiekt, ktorego uzywa pelne AMC. Te testy sprawdzaja, ze kontrakt sesji
/// faktycznie dochodzi przez protokol, a nie ze napisalismy drugi silnik.
///
/// Wyjscie dzwieku jest tu LICZNIKIEM wywolan (<see cref="RecordingOutput"/>),
/// bo warstwa protokolu celowo nie zna Windows. To NIE zastepuje przebiegu na
/// zywym hoscie z prawdziwym <c>WindowsMediaOutput</c> -- mierzy wylacznie
/// kolejnosc i argumenty, ktore host ma wydac silnikowi.
/// </summary>
internal static class QueueCoordinatorTests
{
    /// <summary>Wyjscie zapisujace WYWOLANIA zamiast wydawac dzwiek.</summary>
    private sealed class RecordingOutput : IMediaOutput
    {
        public List<(string Id, TimeSpan Position, int Volume, double Rate)> Plays { get; } = [];
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
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double playbackRate) { }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>
    /// RZECZYWISTE pliki na dysku. Koordynator odmawia startu, gdy lokalnego
    /// pliku nie ma, wiec stan "plik istnieje" musi byc prawdziwy, a nie udany.
    /// Zawartosc jest nieistotna: ta warstwa nie dekoduje dzwieku.
    /// </summary>
    private static string _folder = string.Empty;

    private static string PathOf(string name) => Path.Combine(_folder, name);

    /// <summary>
    /// Trzy pozycje o jawnych Id -- kolejnosc zapisu B, A, C NIE jest
    /// alfabetyczna, wiec alfabet dalby inny wynik niz kolejka.
    /// </summary>
    private static string ThreeRows => $$"""
        {"sessionId":"local","items":[
          {"id":"file:B.wav","title":"B utwor","path":{{Json(PathOf("B.wav"))}},"isInQueue":true},
          {"id":"file:A.wav","title":"A utwor","path":{{Json(PathOf("A.wav"))}},"isInQueue":true},
          {"id":"file:C.wav","title":"C utwor","path":{{Json(PathOf("C.wav"))}},"isInQueue":true}
        ],"order":["file:B.wav","file:A.wav","file:C.wav"]}
        """;

    private static string Json(string value) => JsonSerializer.Serialize(value);

    /// <summary>
    /// Zapowiedz przejscia musi zgadzac sie z FAKTEM. Gdyby
    /// <c>WouldAdvanceAfter</c> klamalo, okno albo milczaloby na koncu kolejki,
    /// albo mowilo "Koniec utworu" w chwili przejscia.
    /// </summary>
    private static void ZapowiedzPrzejsciaZgadzaSieZFaktem()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));

        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        // Srodek kolejki: zapowiedz TRUE i faktyczne przejscie.
        var zapowiedzB = queue.WouldAdvanceAfter("file:B.wav");
        var faktB = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(zapowiedzB, "po pierwszej pozycji kolejka ma czym isc dalej");
        Assert.True(faktB is not null, "i faktycznie idzie");

        // Spoznione zdarzenie: ani zapowiedzi, ani przejscia.
        Assert.True(!queue.WouldAdvanceAfter("file:B.wav"), "stary koniec nie zapowiada przejscia");
        Assert.True(queue.HandlePlaybackEnded("file:B.wav") is null, "stary koniec nie przesuwa kolejki");

        // Ostatnia pozycja: zapowiedz FALSE i brak przejscia.
        queue.HandlePlaybackEnded("file:A.wav");
        Assert.True(
            !queue.WouldAdvanceAfter("file:C.wav"),
            "na koncu kolejki nie zapowiadamy przejscia -- tu okno MA powiedziec koniec");
        Assert.True(queue.HandlePlaybackEnded("file:C.wav") is null, "i nic dalej nie gra");
    }

    public static void Run()
    {
        _folder = Path.Combine(Path.GetTempPath(), "amc-queue-tests-" + Guid.NewGuid().ToString("N")[..8]);
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
            // Sprzatamy TYLKO wlasny katalog tymczasowy.
            try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
        }
    }

    private static void RunSuites()
    {
        SamOdczytNieOdtwarza();
        EnterOdWybranegoWiersza();
        NaturalnyKoniecIdzieKolejnosciaKolejkiNieAlfabetem();
        BlokOdtworzNastepneWyprzedzaZwykla();
        WidokPokazujeRzeczywisteNastepstwo();
        GrupowaZmianaNiePrzerywaAktywnejKolejki();
        DodanieZPowlokiJestIdempotentne();
        NastepnyPoprzedniUzywajaRzeczywistejKolejki();
        PauzaZachowujePozycjeIPrzeskokJejNieCofa();
        StaryEventNiePrzeskakujeDwochUtworow();
        PustaKolejkaIZleIdOdmawiajaUczciwie();
        ZapowiedzPrzejsciaZgadzaSieZFaktem();
        PrzestawienieKolejkiZachowujeDzwiekIZmieniaNastepny();
        DeleteUsuwaTylkoWybranyWpisKolejki();
        Console.WriteLine("QueueCoordinatorTests: OK");
    }

    private static void PrzestawienieKolejkiZachowujeDzwiekIZmieniaNastepny()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        var moved = queue.Move("file:C.wav", -1);
        Assert.True(moved.Outcome == "moved", "C mozna przesunac nad A");
        Assert.True(moved.Status.Rows.Select(row => row.Id).SequenceEqual(
            new[] { "file:B.wav", "file:C.wav", "file:A.wav" }),
            "po ruchu widok pokazuje B, C, A");
        Assert.True(output.Plays.Count == 1 && output.Stops == 0,
            "przestawienie nie uruchamia i nie zatrzymuje dzwieku");
        Assert.True(queue.Move("file:B.wav", 1).Outcome == "playing",
            "grajacy wiersz pozostaje na miejscu");
        Assert.True(queue.PlayRelative(1), "Nastepny po przestawieniu dziala");
        Assert.True(output.Plays.Last().Id == "file:C.wav",
            "silnik odtwarza C jako nastepny po przestawieniu");
    }

    private static void DeleteUsuwaTylkoWybranyWpisKolejki()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        var removed = queue.RemoveMembership(Args("""
            {"sessionId":"local","items":[{"id":"file:A.wav"}]}
            """));
        Assert.True(removed.Changed == 1, "Delete usuwa wskazana pozycje");
        Assert.True(removed.Status.Rows.Select(row => row.Id).SequenceEqual(
            new[] { "file:B.wav", "file:C.wav" }),
            "B i C pozostaja w kolejce");
        Assert.True(output.Plays.Count == 1 && output.Stops == 0,
            "Delete nie przerywa grajacego B");
        Assert.True(queue.PlayRelative(1) && output.Plays.Last().Id == "file:C.wav",
            "Nastepny omija usuniete A");

        var currentOutput = new RecordingOutput();
        var currentQueue = new LiteQueueCoordinator(currentOutput);
        currentQueue.Set(Args(ThreeRows));
        currentQueue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));
        currentQueue.RemoveMembership(Args("""
            {"sessionId":"local","items":[{"id":"file:B.wav"}]}
            """));
        Assert.True(currentOutput.Stops == 0,
            "usuniecie grajacego wpisu nie przerywa utworu");
        Assert.True(currentQueue.Move("file:C.wav", -1).Outcome == "moved",
            "po usunieciu grajacego wpisu pozostala kolejke mozna przestawiac");
        Assert.True(currentQueue.PlayRelative(1)
            && currentOutput.Plays.Last().Id == "file:C.wav",
            "Nastepny idzie do C po usunieciu grajacego wpisu i przestawieniu");
    }

    /// <summary>
    /// SAM odczyt stanu nie moze niczego zaczac grac. Zakres zadania mowi o tym
    /// wprost: wczytanie zapisanej kolejki to jeszcze nie odtwarzanie.
    /// </summary>
    private static void SamOdczytNieOdtwarza()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        var status = queue.Status();

        Assert.True(output.Plays.Count == 0,
            "wczytanie kolejki nie wywoluje Play ani razu");
        Assert.True(status.Playing == false,
            "status po samym wczytaniu nie twierdzi, ze cokolwiek gra");
        Assert.True(status.Rows.Count == 3,
            "wszystkie trzy pozycje weszly do zywej kolejki");
        // Kolejnosc ZAPISANA, nie alfabetyczna -- B przed A.
        Assert.True(status.Rows[0].Id == "file:B.wav",
            "kolejnosc zywej kolejki to kolejnosc zapisu, nie alfabet");
    }

    /// <summary>Enter z widoku kolejki startuje od WSKAZANEGO wiersza.</summary>
    private static void EnterOdWybranegoWiersza()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));

        // Wybrany jest wiersz TRZECI (C), nie pierwszy.
        queue.PlayAt(Args("""{"itemId":"file:C.wav"}"""));

        Assert.True(output.Plays.Count == 1, "Enter wywoluje Play dokladnie raz");
        Assert.True(output.Plays[0].Id == "file:C.wav",
            "Play dostaje Id WYBRANEGO wiersza, nie pierwszego w kolejce");
        Assert.True(queue.Status().CurrentId == "file:C.wav",
            "biezacy material to wybrany wiersz");
    }

    /// <summary>
    /// NATURALNY koniec utworu: B -> A -> C. To dowod, ze kolejnosc bierze sie
    /// z kolejki, a nie z alfabetu (ten dalby A, B, C) ani z listy wejsciowej.
    /// </summary>
    private static void NaturalnyKoniecIdzieKolejnosciaKolejkiNieAlfabetem()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        var first = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(first?.Id == "file:A.wav",
            "po B kolejka podaje A (kolejnosc zapisu), nie C i nie alfabet");

        var second = queue.HandlePlaybackEnded("file:A.wav");
        Assert.True(second?.Id == "file:C.wav", "po A kolejka podaje C");

        var third = queue.HandlePlaybackEnded("file:C.wav");
        Assert.True(third is null, "po ostatnim utworze kolejka konczy sie, bez petli");

        var order = output.Plays.Select(play => play.Id).ToArray();
        Assert.True(order.SequenceEqual(new[] { "file:B.wav", "file:A.wav", "file:C.wav" }),
            "silnik dostal dokladnie trzy Play w kolejnosci kolejki B, A, C");
    }

    /// <summary>
    /// "Odtworz nastepne" wyprzedza zwykla kolejke. Regula jest z
    /// <c>ContinueAfterPlaybackEnded</c> (OrderByDescending(IsPlayNext)), wiec
    /// test pilnuje, ze flaga faktycznie dochodzi przez protokol.
    /// </summary>
    private static void BlokOdtworzNastepneWyprzedzaZwykla()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:B.wav","title":"B","path":{{Json(PathOf("B.wav"))}},"isInQueue":true},
              {"id":"file:A.wav","title":"A","path":{{Json(PathOf("A.wav"))}},"isInQueue":true},
              {"id":"file:C.wav","title":"C","path":{{Json(PathOf("C.wav"))}},"isPlayNext":true}
            ],"order":["file:B.wav","file:A.wav","file:C.wav"]}
            """));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        var next = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(next?.Id == "file:C.wav",
            "pozycja z 'odtworz nastepne' wyprzedza zwykla kolejke, mimo pozniejszej kolejnosci");
    }

    /// <summary>
    /// Ctrl+Q ma pokazywac ten sam porzadek, ktory zastosuje naturalny koniec:
    /// przed startem blok priorytetowy jest pierwszy, a podczas gry biezacy
    /// utwor zostaje pierwszy i dopiero po nim widac rzeczywiste nastepstwo.
    /// </summary>
    private static void WidokPokazujeRzeczywisteNastepstwo()
    {
        var queue = new LiteQueueCoordinator(new RecordingOutput());
        queue.Set(Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:B.wav","title":"B","path":{{Json(PathOf("B.wav"))}},"isInQueue":true},
              {"id":"file:A.wav","title":"A","path":{{Json(PathOf("A.wav"))}},"isInQueue":true},
              {"id":"file:C.wav","title":"C","path":{{Json(PathOf("C.wav"))}},"isPlayNext":true}
            ],"order":["file:B.wav","file:A.wav","file:C.wav"]}
            """));

        Assert.True(queue.Status().Rows.Select(row => row.Id).SequenceEqual(
                new[] { "file:C.wav", "file:B.wav", "file:A.wav" }),
            "przed startem widok stawia play-next przed zwykla kolejka");

        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));
        Assert.True(queue.Status().Rows.Select(row => row.Id).SequenceEqual(
                new[] { "file:B.wav", "file:C.wav", "file:A.wav" }),
            "podczas gry widok pokazuje biezacy, potem play-next, potem zwykla kolejke");
    }

    /// <summary>
    /// Operacje z wielokrotnego zaznaczenia dopisuja do zywej sesji. Nie wolno
    /// robic queue.set, bo podmieniloby transport i zgubilo pozycje.
    /// </summary>
    private static void GrupowaZmianaNiePrzerywaAktywnejKolejki()
    {
        var refused = new LiteQueueCoordinator(new RecordingOutput());
        var missingPathRefused = false;
        try
        {
            refused.ToggleMembership(Args("""
                {"sessionId":"local","items":[{"id":"bez-pliku","title":"Bez pliku"}]}
                """), playNext: false);
        }
        catch (LiteRequestException) { missingPathRefused = true; }
        Assert.True(missingPathRefused && !refused.Status().Initialized,
            "odmowa nowego pliku bez sciezki nie inicjalizuje pustej kolejki");

        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        var added = queue.ToggleMembership(Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:A.wav","title":"A","path":{{Json(PathOf("A.wav"))}}},
              {"id":"file:B.wav","title":"B","path":{{Json(PathOf("B.wav"))}}}
            ]}
            """), playNext: false);
        Assert.True(added.Added && added.Changed == 2,
            "dwa nowe pliki zostaly dodane jedna decyzja grupowa");

        queue.PlayAt(Args("""{"itemId":"file:A.wav"}"""));
        var playsBefore = output.Plays.Count;
        var next = queue.ToggleMembership(Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:C.wav","title":"C","path":{{Json(PathOf("C.wav"))}}}
            ]}
            """), playNext: true);
        Assert.True(next.Added && next.Changed == 1,
            "nowa pozycja dostala priorytet odtworz nastepne");
        Assert.True(output.Plays.Count == playsBefore && queue.Status().CurrentId == "file:A.wav",
            "zmiana kolejki nie uruchamia ponownie i nie zmienia biezacego pliku");
        Assert.True(queue.Status().Rows.Select(row => row.Id).SequenceEqual(
                new[] { "file:A.wav", "file:C.wav", "file:B.wav" }),
            "widok aktywnej kolejki pokazuje rzeczywiste nastepstwo A, C, B");
        Assert.True(queue.HandlePlaybackEnded("file:A.wav")?.Id == "file:C.wav",
            "po koncu A faktycznie startuje oznaczone jako nastepne C");

        var removed = queue.ToggleMembership(Args("""
            {"sessionId":"local","items":[
              {"id":"file:B.wav","title":"B"},
              {"id":"file:C.wav","title":"C"}
            ]}
            """), playNext: false);
        Assert.True(!removed.Added && removed.Changed == 2,
            "obecność jednego zaznaczonego w kolejce zdejmuje członkostwo z całej grupy");
        Assert.True(output.Plays.Count == playsBefore + 1,
            "usunięcie z kolejki nie zatrzymuje już grającego C");
    }

    /// <summary>
    /// Menu powloki brzmi "Dodaj", wiec drugie wywolanie nie moze zachowywac
    /// sie jak przelacznik i usuwac pliku z kolejki.
    /// </summary>
    private static void DodanieZPowlokiJestIdempotentne()
    {
        var queue = new LiteQueueCoordinator(new RecordingOutput());
        var args = Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:A.wav","title":"A","path":{{Json(PathOf("A.wav"))}}}
            ]}
            """);

        var first = queue.AddMembership(args);
        var second = queue.AddMembership(args);

        Assert.True(first.Added && first.Changed == 1,
            "pierwsze polecenie powloki dodaje plik");
        Assert.True(second.Added && second.Changed == 0,
            "drugie polecenie pozostawia juz dodany plik bez zmian");
        Assert.True(queue.Status().Rows.Count(row => row.Id == "file:A.wav") == 1,
            "idempotentne dodanie nie duplikuje i nie usuwa pozycji");
    }

    /// <summary>Nastepny/Poprzedni chodza po RZECZYWISTEJ kolejce.</summary>
    private static void NastepnyPoprzedniUzywajaRzeczywistejKolejki()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        Assert.True(queue.PlayRelative(1), "Nastepny z B sie udaje");
        Assert.True(queue.Status().CurrentId == "file:A.wav",
            "Nastepny idzie kolejnoscia kolejki (A), nie alfabetem");

        Assert.True(queue.PlayRelative(-1), "Poprzedni sie udaje");
        Assert.True(queue.Status().CurrentId == "file:B.wav", "Poprzedni wraca na B");

        // Na pierwszej pozycji Poprzedni ODMAWIA, a nie zawija sie na koniec.
        Assert.True(!queue.PlayRelative(-1),
            "na pierwszej pozycji Poprzedni odmawia, bez zawijania na koniec");
    }

    /// <summary>
    /// Pauza zachowuje pozycje, a przeskok elementu NIE wyrywa z pauzy. To
    /// kontrakt <c>PlayRelative</c> (sprawdza IsPaused), nie nasz wymysl.
    /// </summary>
    private static void PauzaZachowujePozycjeIPrzeskokJejNieCofa()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));

        output.Position = TimeSpan.FromSeconds(7.5);
        queue.PauseResume();
        Assert.True(output.Pauses == 1, "pauza doszla do silnika");
        Assert.True(queue.Status().PositionSeconds > 7d,
            "pauza ZACHOWALA pozycje, nie wyzerowala jej");

        var playsBefore = output.Plays.Count;
        Assert.True(queue.PlayRelative(1), "przeskok w pauzie sie udaje");
        Assert.True(output.Plays.Count == playsBefore,
            "przeskok w pauzie NIE zaczyna grac: pauzy nie cofa");
        Assert.True(queue.Status().CurrentId == "file:A.wav",
            "przeskok w pauzie zmienia biezacy element");
    }

    /// <summary>
    /// STARY event po nowym play nie moze przeskoczyc dwoch utworow. Silnik
    /// audio zglasza koniec asynchronicznie, wiec spozniony <c>playback.ended</c>
    /// poprzedniego pliku musi zostac ODRZUCONY.
    /// </summary>
    private static void StaryEventNiePrzeskakujeDwochUtworow()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));
        // Uzytkownik SAM wybral C, gdy B jeszcze konczylo sie w tle.
        queue.PlayAt(Args("""{"itemId":"file:C.wav"}"""));

        var stale = queue.HandlePlaybackEnded("file:B.wav");
        Assert.True(stale is null,
            "spozniony koniec B jest odrzucony: nie przeskakuje o dwa utwory");
        Assert.True(queue.Status().CurrentId == "file:C.wav",
            "biezacy material zostaje tym, ktory uzytkownik wybral");

        // Ten sam Id ponowiony po nowym starcie tez nie moze zadzialac dwa razy.
        queue.PlayAt(Args("""{"itemId":"file:A.wav"}"""));
        var firstEnd = queue.HandlePlaybackEnded("file:A.wav");
        // Kontrakt Core: po A zostaja B i C, a pierwszy w KOLEJNOSCI ZAPISU
        // jest B. Zadne z nich nie ma "odtworz nastepne", wiec nic nie wyprzedza.
        Assert.True(firstEnd?.Id == "file:B.wav",
            "normalny koniec A podaje B: pierwszy pozostajacy w kolejnosci zapisu");
        var repeated = queue.HandlePlaybackEnded("file:A.wav");
        Assert.True(repeated is null,
            "POWTORZONY koniec tego samego Id nie przesuwa kolejki drugi raz");
    }

    /// <summary>Pusta kolejka i zle Id odmawiaja z komunikatem, bez petli.</summary>
    private static void PustaKolejkaIZleIdOdmawiajaUczciwie()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);

        queue.Set(Args("""{"sessionId":"local","items":[],"order":[]}"""));
        var refusedEmpty = false;
        try { queue.PlayAt(Args("""{"itemId":"file:B.wav"}""")); }
        catch (LiteRequestException) { refusedEmpty = true; }
        Assert.True(refusedEmpty, "pusta kolejka odmawia startu, nie milczy");
        Assert.True(queue.Status().Rows.Count == 0, "pusta kolejka ma zero wierszy");
        Assert.True(queue.HandlePlaybackEnded("file:B.wav") is null,
            "koniec nieznanego Id przy pustej kolejce nie wywraca hosta");
        Assert.True(!queue.PlayRelative(1), "Nastepny przy pustej kolejce odmawia");

        queue.Set(Args(ThreeRows));
        var refusedUnknown = false;
        try { queue.PlayAt(Args("""{"itemId":"file:NIE-MA.wav"}""")); }
        catch (LiteRequestException) { refusedUnknown = true; }
        Assert.True(refusedUnknown, "nieznane Id odmawia jawnie, nie gra czegos innego");
        Assert.True(output.Plays.Count == 0,
            "odmowa nie wydala zadnego Play: nie podstawia innego utworu");

        // BRAKUJACY plik lokalny: odmowa PRZED zajeciem silnika.
        var missing = Path.Combine(_folder, "nie-ma-takiego.wav");
        Assert.True(!File.Exists(missing), "plik testowy faktycznie nie istnieje");
        queue.Set(Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:brak","title":"brak","path":{{Json(missing)}},"isInQueue":true}
            ],"order":["file:brak"]}
            """));
        var refusedMissing = false;
        try { queue.PlayAt(Args("""{"itemId":"file:brak"}""")); }
        catch (LiteRequestException) { refusedMissing = true; }
        Assert.True(refusedMissing, "brakujacy plik lokalny odmawia jawnie");
        Assert.True(output.Plays.Count == 0,
            "brakujacy plik nie doszedl do silnika w ogole");

        // Powtorzone Id w jednym wsadzie: odmowa, bo zepsulo by szukanie po Id.
        var refusedDuplicate = false;
        try
        {
            queue.Set(Args($$"""
                {"sessionId":"local","items":[
                  {"id":"file:B.wav","title":"B","path":{{Json(PathOf("B.wav"))}},"isInQueue":true},
                  {"id":"file:B.wav","title":"B znowu","path":{{Json(PathOf("B.wav"))}},"isInQueue":true}
                ],"order":["file:B.wav"]}
                """));
        }
        catch (LiteRequestException) { refusedDuplicate = true; }
        Assert.True(refusedDuplicate, "powtorzone Id w kolejce odmowione jawnie");
    }
}
