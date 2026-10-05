using System.Text.Json;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// SWIADEK RED. Celowo uzywa WYLACZNIE API, ktore istnialo PRZED poprawka
/// transportu, zeby dalo sie go skompilowac i uruchomic na kodzie bazowym.
/// Sluzy do jednego: pokazac, ze luki 1-3 byly prawdziwe, zanim cokolwiek
/// zmienilismy. Po poprawce ten sam plik musi przechodzic.
///
/// Punkty 4 i 6 nie maja tu swiadka, bo ich brak jest brakiem POLA
/// (<c>Initialized</c>) i brakiem dostepu do biezacej pozycji: na kodzie
/// bazowym nie kompiluja sie w ogole, co samo w sobie jest czerwienia.
/// </summary>
internal static class QueueTransportRedWitness
{
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

    private static string _folder = string.Empty;
    private static string PathOf(string name) => Path.Combine(_folder, name);
    private static string Json(string value) => JsonSerializer.Serialize(value);

    private static string ThreeRows => $$"""
        {"sessionId":"local","items":[
          {"id":"file:B.wav","title":"B","path":{{Json(PathOf("B.wav"))}},"isInQueue":true},
          {"id":"file:A.wav","title":"A","path":{{Json(PathOf("A.wav"))}},"isInQueue":true},
          {"id":"file:C.wav","title":"C","path":{{Json(PathOf("C.wav"))}},"isInQueue":true}
        ],"order":["file:B.wav","file:A.wav","file:C.wav"]}
        """;

    public static void Run()
    {
        _folder = Path.Combine(Path.GetTempPath(), "amc-red-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_folder);
        try
        {
            foreach (var name in new[] { "A.wav", "B.wav", "C.wav" }) File.WriteAllBytes(PathOf(name), []);

            // Kazdy punkt mierzymy OSOBNO: pierwszy upadek nie ma prawa ukryc
            // stanu pozostalych, bo wtedy nie wiedzielibysmy, ktore luki
            // naprawde istnieja, a ktore juz byly zamkniete.
            var cases = new (string Name, Action Run)[]
            {
                ("PUNKT 1 wejscie", Punkt1_GlosnoscITempoZZadania),
                ("PUNKT 1 nastepstwo", Punkt1_NastawyTrwajaPrzezNastepstwo),
                ("PUNKT 2 stop", Punkt2_StopZatrzymujeSesje),
                ("PUNKT 3 odciecie", Punkt3_PoWyjsciuKolejkaMilczy),
            };
            var failed = 0;
            foreach (var (name, run) in cases)
            {
                try
                {
                    run();
                    Console.WriteLine($"ZIELONY {name}");
                }
                catch (Exception exception)
                {
                    failed++;
                    Console.WriteLine($"CZERWONY {name}: {exception.Message}");
                }
            }
            Console.WriteLine($"RED-WITNESS-CZERWONYCH={failed} z {cases.Length}");
            if (failed > 0) throw new InvalidOperationException($"Czerwonych przypadkow: {failed}.");
            Console.WriteLine("QueueTransportRedWitness: OK");
        }
        finally
        {
            try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
        }
    }

    private static void Punkt1_GlosnoscITempoZZadania()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":0,"rate":1.5}"""));

        Assert.True(output.Plays.Count == 1, "Enter wywoluje Play raz");
        Assert.True(output.Plays[0].Volume == 0,
            $"PUNKT 1: Play ma dostac glosnosc 0 z zadania; dostal {output.Plays[0].Volume}");
        Assert.True(Math.Abs(output.Plays[0].Rate - 1.5d) < 0.001d,
            $"PUNKT 1: Play ma dostac tempo 1,5 z zadania; dostal {output.Plays[0].Rate}");
    }

    private static void Punkt1_NastawyTrwajaPrzezNastepstwo()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav","volume":0,"rate":1.5}"""));
        queue.HandlePlaybackEnded("file:B.wav");

        var play = output.Plays[^1];
        Assert.True(play.Id == "file:A.wav", "po B gra A");
        Assert.True(play.Volume == 0,
            $"PUNKT 1: nastepny utwor zachowuje glosnosc 0; dostal {play.Volume}");
        Assert.True(Math.Abs(play.Rate - 1.5d) < 0.001d,
            $"PUNKT 1: nastepny utwor zachowuje tempo 1,5; dostal {play.Rate}");
    }

    private static void Punkt2_StopZatrzymujeSesje()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));
        output.Position = TimeSpan.FromSeconds(9);

        var stopped = queue.Stop();
        Assert.True(output.Stops == 1, "zatrzymanie doszlo do silnika");
        Assert.True(!stopped.Playing, "PUNKT 2: po zatrzymaniu sesja NIE twierdzi, ze gra");
        Assert.True(stopped.Rows.Count == 3, "elementy zostaja w kolejce");

        var before = output.Plays.Count;
        queue.PauseResume();
        Assert.True(output.Plays.Count == before + 1, "PUNKT 2: wznowienie po zatrzymaniu znow gra");
        Assert.True(output.Plays[^1].Position.TotalSeconds > 8d,
            $"PUNKT 2: wznowienie wraca na pozycje; bylo {output.Plays[^1].Position}");
    }

    private static void Punkt3_PoWyjsciuKolejkaMilczy()
    {
        var output = new RecordingOutput();
        var queue = new LiteQueueCoordinator(output);
        queue.Set(Args(ThreeRows));
        queue.PlayAt(Args("""{"itemId":"file:B.wav"}"""));
        queue.DetachFromDirectPlay();

        var before = output.Plays.Count;
        Assert.True(!queue.OwnsCurrent("file:B.wav"),
            "PUNKT 3: po wyjsciu poza kolejke nie jest ona wlascicielem transportu tego Id");
        Assert.True(queue.HandlePlaybackEnded("file:B.wav") is null,
            "PUNKT 3: stary koniec nie przesuwa odlaczonej kolejki");
        Assert.True(!queue.WouldAdvanceAfter("file:B.wav"),
            "PUNKT 3: odlaczona kolejka nie zapowiada przejscia");
        Assert.True(!queue.PlayRelative(1),
            "PUNKT 3: Nastepny nie wznawia odlaczonej kolejki");
        Assert.True(output.Plays.Count == before,
            "PUNKT 3: zadna z drog nie wydala Play");
    }
}
