using System.Text;
using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Testy adaptera wyjscia nowej sesji "Spotify — Librespot".
///
/// Mierzone sa te zachowania, ktore uzytkownik z czytnikiem ekranu uslyszy:
/// zdarzenia Preparing/Started/Duration/Ended/Failed takie jak w sesji SDK,
/// IsPreparing, brak zmiany tempa, realne przejscie glosnosci/czasu/przewijania
/// do hosta oraz to, ze pauza i zatrzymanie w trakcie przygotowania NIE wlaczaja
/// pozniej muzyki.
///
/// Konta tu nie ma: token jest jawnie fikcyjny, a host to atrapa strumieni.
/// Ten zestaw NIE dowodzi odsluchu.
/// </summary>
internal static class SpotifyLibrespotOutputTests
{
    internal static void Run()
    {
        ZdarzeniaSaTakieJakWSesjiSdk();
        CzasUtworuIPozycjaIdaDoGory();
        StareZdarzeniaNieRuszajaBiezacegoUtworu();
        TenSamUtworDwaRazyNieDostajeStaregoKonca();
        PauzaWPrzygotowaniuNieWlaczaMuzyki();
        StopWPrzygotowaniuNieWlaczaMuzyki();
        GlosnoscCzasIPrzewijanieIdaDoHosta();
        TempoOdtwarzaniaNieJestObslugiwane();
        KontenerNieJestOdtwarzany();
        BladUtworuJestPowiedzianyWprost();
        AwariaHostaJestPowiedzianaWprost();
        WyborUrzadzeniaPrzyjmujeDokladnaNazwe();
        OdmowaZmianyWyjsciaNieCichnie();
        BrakSamoczynnegoLogowaniaIZastepnika();
        Console.WriteLine("OK: wyjście sesji Spotify — Librespot"
            + " (zdarzenia jak w sesji SDK, stare playId odrzucane,"
            + " pauza i stop w przygotowaniu nie włączają muzyki,"
            + " głośność, czas i przewijanie idą do hosta)");
    }

    private const string FikcyjnyToken = "FIKCYJNY-TOKEN-TESTOWY-nie-jest-poswiadczeniem";

    private static MediaItem Utwor(string externalId = "ABC", string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString("N"),
        Title = "Testowy utwór",
        Kind = MediaItemKind.Track,
        ExternalId = externalId,
        Source = $"spotify:track:{externalId}"
    };

    private static void ZdarzeniaSaTakieJakWSesjiSdk()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);

        Check(scena.Preparing.Count == 1, "Start odtwarzania musi zgłosić Preparing");
        Check(!scena.Preparing[0].CloudDownloadRequired,
            "Sesja strumieniowa nie pobiera pliku z chmury");
        Check(scena.Output.IsPreparing, "W trakcie przygotowania IsPreparing musi być prawdą");
        Check(scena.Output.LoadedItemId == item.Id, "Wczytany element musi być zapamiętany");

        scena.Host.Settle();
        var play = scena.Host.Command("play");
        scena.Host.EmitState(play.PlayId, play.Uri, 0, 210_000, isPlaying: true);
        scena.Host.Settle();

        Check(scena.Started.Count == 1, "Potwierdzone granie musi zgłosić Started");
        Check(!scena.Output.IsPreparing, "Po potwierdzonym graniu przygotowanie się kończy");

        scena.Host.EmitEnded(play.PlayId, play.Uri);
        scena.Host.Settle();
        Check(scena.Ended.Count == 1, "Koniec utworu musi zgłosić Ended");
        Check(scena.Ended[0].Item.Id == item.Id, "Ended musi dotyczyć tego elementu, który grał");
        Check(scena.Failed.Count == 0, "Poprawne odtworzenie nie może zgłosić błędu");
    }

    private static void CzasUtworuIPozycjaIdaDoGory()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.FromSeconds(20), 50, 1d);
        scena.Host.Settle();
        var play = scena.Host.Command("play");
        scena.Host.EmitState(play.PlayId, play.Uri, 21_000, 195_000, isPlaying: true);
        scena.Host.Settle();

        Check(scena.Durations.Count == 1, "Czas utworu z hosta musi zgłosić DurationAvailable");
        Check(scena.Durations[0].Duration == TimeSpan.FromMilliseconds(195_000),
            "Czas utworu musi być dokładnie tym, co podał host");
        Check(item.Duration == TimeSpan.FromMilliseconds(195_000),
            "Czas z hosta musi trafić do modelu elementu, żeby pasek postępu miał się na czym oprzeć");
        Check(scena.Output.Position == TimeSpan.FromMilliseconds(21_000),
            "Pozycja odtwarzania musi pochodzić z hosta, nie z katalogu");
    }

    private static void StareZdarzeniaNieRuszajaBiezacegoUtworu()
    {
        using var scena = Scena.Nowa();
        var pierwszy = Utwor("AAA");
        scena.Output.Play(pierwszy, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var pierwszePlay = scena.Host.Command("play");
        scena.Host.EmitState(pierwszePlay.PlayId, pierwszePlay.Uri, 5_000, 180_000, isPlaying: true);
        scena.Host.Settle();

        var drugi = Utwor("BBB");
        scena.Output.Play(drugi, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var drugiePlay = scena.Host.Command("play", index: 1);
        scena.Host.EmitState(drugiePlay.PlayId, drugiePlay.Uri, 1_000, 240_000, isPlaying: true);
        scena.Host.Settle();
        var zdarzenCzasu = scena.Durations.Count;

        // Spoznione zdarzenia PIERWSZEGO utworu.
        scena.Host.EmitState(pierwszePlay.PlayId, pierwszePlay.Uri, 179_000, 180_000, isPlaying: true);
        scena.Host.EmitEnded(pierwszePlay.PlayId, pierwszePlay.Uri);
        scena.Host.Settle();

        Check(scena.Ended.Count == 0,
            "Koniec poprzedniego utworu nie może zakończyć utworu, który gra teraz");
        Check(scena.Durations.Count == zdarzenCzasu,
            "Stary utwór nie może zmienić czasu czytanego dla nowego");
        Check(scena.Output.Position == TimeSpan.FromMilliseconds(1_000),
            "Pozycja nie może skoczyć na koniec poprzedniego utworu");
        Check(scena.Output.LoadedItemId == drugi.Id, "Wczytany element musi zostać nowy");
    }

    private static void TenSamUtworDwaRazyNieDostajeStaregoKonca()
    {
        using var scena = Scena.Nowa();
        // TEN SAM element, uruchomiony dwa razy. Sam URI ich nie rozroznia -
        // rozroznia je playId nadane przez AMC.
        var item = Utwor("POWTORKA", id: "stale-id");
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var pierwsze = scena.Host.Command("play");
        scena.Host.EmitState(pierwsze.PlayId, pierwsze.Uri, 1_000, 120_000, isPlaying: true);
        scena.Host.Settle();

        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var drugie = scena.Host.Command("play", index: 1);
        scena.Host.EmitState(drugie.PlayId, drugie.Uri, 2_000, 120_000, isPlaying: true);
        scena.Host.Settle();

        Check(pierwsze.PlayId != drugie.PlayId,
            "Dwa uruchomienia tego samego utworu muszą dostać różne playId");

        scena.Host.EmitEnded(pierwsze.PlayId, pierwsze.Uri);
        scena.Host.Settle();
        Check(scena.Ended.Count == 0,
            "Ten sam utwór uruchomiony dwa razy nie może dostać końca pierwszej próby");

        scena.Host.EmitEnded(drugie.PlayId, drugie.Uri);
        scena.Host.Settle();
        Check(scena.Ended.Count == 1, "Koniec drugiej próby musi dojść");
    }

    private static void PauzaWPrzygotowaniuNieWlaczaMuzyki()
    {
        using var scena = Scena.Nowa(withhold: "play");
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.WaitForCommand("play");
        // Uzytkownik pauzuje, gdy host jeszcze nie potwierdzil grania.
        scena.Output.Pause();
        scena.Host.WaitForCommand("pause");
        scena.Host.WithholdAck.Clear();
        scena.Host.AckAllPending();
        // Czekamy na POWTORNA pauze: to ona dowodzi, ze transport nie pozwolil
        // muzyce wystartowac po spoznionym potwierdzeniu grania.
        scena.Host.Commands("pause", minimum: 2);
        scena.Host.Settle();

        Check(!scena.Output.IsPreparing,
            "Pauza w trakcie przygotowania musi zakończyć przygotowanie");
        Check(scena.Started.Count == 0,
            "Pauza w trakcie przygotowania nie może później zgłosić startu odtwarzania");
        Check(scena.Host.CommandNames()[^1] == "pause",
            "Ostatnim poleceniem po przerwanym przygotowaniu musi być pauza, nie graj");
    }

    private static void StopWPrzygotowaniuNieWlaczaMuzyki()
    {
        using var scena = Scena.Nowa(withhold: "play");
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.WaitForCommand("play");
        scena.Output.Stop();
        scena.Host.WaitForCommand("stop");
        scena.Host.WithholdAck.Clear();
        scena.Host.AckAllPending();
        scena.Host.Commands("stop", minimum: 2);
        scena.Host.Settle();

        Check(!scena.Output.IsPreparing, "Zatrzymanie musi zakończyć przygotowanie");
        Check(scena.Output.LoadedItemId is null, "Po zatrzymaniu nie ma wczytanego elementu");

        // Spozniony stan hosta nie moze wskrzesic odtwarzania.
        var play = scena.Host.Command("play");
        scena.Host.EmitState(play.PlayId, play.Uri, 0, 120_000, isPlaying: true);
        scena.Host.Settle();
        Check(scena.Started.Count == 0,
            "Zatrzymana próba nie może później zgłosić startu odtwarzania");
    }

    private static void GlosnoscCzasIPrzewijanieIdaDoHosta()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.FromSeconds(33), 64, 1d);
        scena.Host.Settle();
        scena.Output.SetVolume(150);
        scena.Output.Seek(TimeSpan.FromSeconds(-9));
        scena.Output.Seek(TimeSpan.FromSeconds(120));
        scena.Host.Settle();

        var play = scena.Host.Command("play");
        Check(play.PositionMs == 33_000, "Pozycja startowa musi realnie przejść do hosta");
        // Glosnosc: jedna przy graniu, jedna z SetVolume.
        var glosnosci = scena.Host.Commands("volume", minimum: 2);
        Check(glosnosci[0].Volume == 64,
            "Głośność podana przy graniu musi realnie przejść do hosta");
        Check(glosnosci[^1].Volume == 100, "Głośność musi być ograniczona do 100");
        var przewijania = scena.Host.Commands("seek", minimum: 2);
        Check(przewijania[0].PositionMs == 0, "Ujemne przewijanie musi być ścięte do zera");
        Check(przewijania[1].PositionMs == 120_000,
            "Przewijanie musi realnie przejść do hosta w milisekundach");
        Check(scena.Output.Position == TimeSpan.FromSeconds(120),
            "Po przewinięciu pozycja czytana użytkownikowi musi się zgadzać");
    }

    private static void TempoOdtwarzaniaNieJestObslugiwane()
    {
        using var scena = Scena.Nowa();
        Check(!scena.Output.SupportsPlaybackRate,
            "Sesja Librespot nie obsługuje zmiany tempa, tak jak sesja SDK");
        scena.Output.SetPlaybackRate(1.5d);
        scena.Host.Settle();
        Check(scena.Host.CommandNames().All(name => name != "playbackRate"),
            "Brak obsługi tempa nie może wysyłać do hosta nieistniejącego polecenia");
    }

    private static void KontenerNieJestOdtwarzany()
    {
        using var scena = Scena.Nowa();
        var album = new MediaItem
        {
            Title = "Album",
            Kind = MediaItemKind.Album,
            ExternalId = "ALB",
            Source = "spotify:album:ALB"
        };
        scena.Output.Play(album, TimeSpan.Zero, 70, 1d);

        Check(scena.Failed.Count == 1, "Album nie jest utworem: musi dać czytelny komunikat");
        Check(scena.Failed[0].Message.Contains("strzałką w prawo"),
            "Komunikat musi powiedzieć, co zrobić, a nie tylko że się nie udało");
        Check(scena.Preparing.Count == 0, "Kontener nie może zgłaszać przygotowania odtwarzania");
    }

    private static void BladUtworuJestPowiedzianyWprost()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var play = scena.Host.Command("play");
        scena.Host.EmitTrackError(play.PlayId, play.Uri, "premium_required");
        scena.Host.Settle();

        Check(scena.Failed.Count == 1, "Błąd utworu musi być powiedziany, nie przemilczany");
        Check(scena.Failed[0].Message.Contains("Premium"),
            "Brak Premium musi być powiedziany WPROST, a nie jako ogólny błąd");
        Check(!scena.Output.IsPreparing, "Błąd utworu kończy przygotowanie");
        Check(scena.Ended.Count == 0,
            "Błąd utworu nie jest końcem utworu: sesja nie może przeskoczyć dalej jak po udanym odtworzeniu");
    }

    private static void AwariaHostaJestPowiedzianaWprost()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        scena.Host.EmitLine(new JsonObject
        {
            ["type"] = "error",
            ["code"] = LibrespotHostErrorCodes.HostExited,
            ["message"] = "Proces zakończył się."
        });
        scena.Host.Settle();

        Check(scena.Failed.Count == 1, "Awaria hosta musi być powiedziana, nie przemilczana");
        Check(scena.Failed[0].Message.Contains("Librespot"),
            "Komunikat musi nazwać składnik, którego dotyczy");
        Check(!scena.Output.IsPreparing, "Awaria hosta kończy przygotowanie");
    }

    private static void WyborUrzadzeniaPrzyjmujeDokladnaNazwe()
    {
        using var scena = Scena.Nowa();
        scena.Host.Devices = new JsonArray
        {
            new JsonObject { ["name"] = "Wyjście domyślne", ["isDefault"] = true },
            new JsonObject { ["name"] = "Karta USB (2- Audio)", ["isDefault"] = false }
        };
        var urzadzenia = scena.Output.GetOutputDevicesAsync().GetAwaiter().GetResult();
        Check(urzadzenia.Count == 2, "Lista wyjść musi dojść bez konta");

        // Zmiana wyjscia wymaga, zeby host mial JUZ poswiadczenia: inaczej
        // transport slusznie odmawia (kod not_started). Token jest wstrzyknietym
        // ciagiem testowym - zadnego konta ani sekretu tu nie ma.
        scena.Client.InitializeAsync(null, null, 70).GetAwaiter().GetResult();

        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var play = scena.Host.Command("play");
        scena.Host.EmitState(play.PlayId, play.Uri, 40_000, 200_000, isPlaying: true);
        scena.Host.Settle();

        var zmiana = scena.Output.TrySetOutputDeviceAsync(urzadzenia[1].Name)
            .GetAwaiter().GetResult();
        Check(zmiana, "Zmiana wyjścia na nazwę z listy musi się udać");
        var initialize = scena.Host.Commands("initialize")[^1];
        Check(initialize.Device == "Karta USB (2- Audio)",
            "Wybór wyjścia musi przekazać DOKŁADNĄ nazwę zwróconą przez host");
        Check(initialize.KeepUri == play.Uri, "Zmiana wyjścia musi zachować utwór");
        Check(initialize.KeepPositionMs == 40_000, "Zmiana wyjścia musi zachować pozycję");
        Check(initialize.KeepPlaying, "Zmiana wyjścia musi zachować informację, że utwór gra");
        Check(scena.Failed.Count == 0, "Udana zmiana wyjścia nie może zgłaszać błędu");
    }

    private static void OdmowaZmianyWyjsciaNieCichnie()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        // Poswiadczenia (token testowy) musza byc wczesniej, inaczej odmowa
        // wyszlaby z braku logowania, a nie z odmowy hosta - test mierzylby co innego.
        scena.Client.InitializeAsync(null, null, 70).GetAwaiter().GetResult();
        scena.Host.AutoError = ("audio_device_unavailable", "Nie ma takiego wyjścia.");
        var zmiana = scena.Output.TrySetOutputDeviceAsync("Nie ma takiego")
            .GetAwaiter().GetResult();

        Check(!zmiana, "Odmowa zmiany wyjścia musi być zgłoszona jako niepowodzenie");
        Check(scena.Failed.Count == 1,
            "Odmowa zmiany wyjścia nie może przejść w ciszy: użytkownik musi wiedzieć");
        Check(scena.Failed[0].Message.Contains("Nie ma takiego"),
            "Komunikat musi nazwać wyjście, którego nie udało się ustawić");
    }

    private static void BrakSamoczynnegoLogowaniaIZastepnika()
    {
        using var scena = Scena.Nowa();
        var item = Utwor();
        scena.Output.Play(item, TimeSpan.Zero, 70, 1d);
        scena.Host.Settle();
        var play = scena.Host.Command("play");
        scena.Host.EmitTrackError(play.PlayId, play.Uri, "invalid_token");
        scena.Host.Settle();

        Check(scena.Failed[0].Message.Contains("zaloguj się ponownie"),
            "Wygasłe logowanie musi POPROSIĆ użytkownika o logowanie, nie logować się samo");
        var nazwy = scena.Host.CommandNames();
        Check(nazwy.Count(name => name == "initialize") == 0,
            "Adapter nie może samoczynnie ponawiać logowania po błędzie tokenu");
        Check(nazwy.Count(name => name == "play") == 1,
            "Adapter nie może po cichu ponawiać odtwarzania ani podmieniać silnika na SDK");
    }

    // ---------- Scena testowa ----------

    /// <summary>
    /// Adapter, transport i atrapa hosta zlozone razem. Wywolania interfejsu
    /// wykonujemy od razu (bez dispatchera WPF), zeby test mierzyl zdarzenia, a
    /// nie kolejke okna.
    /// </summary>
    private sealed class Scena : IDisposable
    {
        public required AtrapaHosta Host { get; init; }
        public required LibrespotHostClient Client { get; init; }
        public required SpotifyLibrespotMediaOutput Output { get; init; }
        public List<MediaPlaybackPreparingEventArgs> Preparing { get; } = [];
        public List<MediaPlaybackStartedEventArgs> Started { get; } = [];
        public List<MediaPlaybackEndedEventArgs> Ended { get; } = [];
        public List<MediaOutputFailedEventArgs> Failed { get; } = [];
        public List<MediaDurationAvailableEventArgs> Durations { get; } = [];

        public static Scena Nowa(string? withhold = null)
        {
            var host = new AtrapaHosta();
            if (withhold is not null) host.WithholdAck.Add(withhold);
            var client = new LibrespotHostClient(
                () => host,
                new LibrespotHostOptions
                {
                    ReadyTimeout = TimeSpan.FromSeconds(5),
                    RequestTimeout = TimeSpan.FromSeconds(5),
                    ShutdownTimeout = TimeSpan.FromMilliseconds(200)
                },
                _ => Task.FromResult(FikcyjnyToken));
            host.EmitReady();
            client.StartAsync().GetAwaiter().GetResult();
            host.Client = client;
            host.Attach();
            var output = new SpotifyLibrespotMediaOutput(client, action => action());
            var scena = new Scena { Host = host, Client = client, Output = output };
            output.PlaybackPreparing += (_, e) => scena.Preparing.Add(e);
            output.PlaybackStarted += (_, e) => scena.Started.Add(e);
            output.PlaybackEnded += (_, e) => scena.Ended.Add(e);
            output.PlaybackFailed += (_, e) => scena.Failed.Add(e);
            output.DurationAvailable += (_, e) => scena.Durations.Add(e);
            return scena;
        }

        public void Dispose()
        {
            Output.Dispose();
            Client.Dispose();
            Host.Dispose();
        }
    }

    /// <summary>Odczytane polecenie wyslane do hosta.</summary>
    private sealed record Polecenie(JsonObject Raw)
    {
        public string Name => Raw["command"]?.GetValue<string>() ?? string.Empty;
        public long PlayId => Liczba("playId");
        public long PositionMs => Liczba("positionMs");
        public int Volume => (int)Liczba("volume");
        public string Uri => Raw["uri"]?.GetValue<string>() ?? string.Empty;
        public string? Device => Raw["device"]?.GetValue<string>();
        public string? KeepUri => Raw["keepUri"]?.GetValue<string>();
        public long KeepPositionMs => Liczba("keepPositionMs");
        public bool KeepPlaying => Raw["keepPlaying"] is JsonValue value
            && value.TryGetValue(out bool flag) && flag;

        private long Liczba(string name)
        {
            if (Raw[name] is not JsonValue value) return 0;
            if (value.TryGetValue(out long number)) return number;
            return value.TryGetValue(out int small) ? small : 0;
        }
    }

    /// <summary>
    /// Atrapa strumieni hosta. Steruje kolejnoscia zdarzen z dokladnoscia do
    /// jednej linii, czego prawdziwy proces nie daje - a wlasnie wyscig miedzy
    /// pauza i potwierdzeniem grania jest tu mierzony.
    /// </summary>
    private sealed class AtrapaHosta : ILibrespotHostProcess
    {
        private readonly object gate = new();
        private readonly StringBuilder input = new();
        private readonly List<Polecenie> odebrane = [];
        private readonly List<long> bezOdpowiedzi = [];
        private readonly Kanal kanal = new();
        private bool podlaczony;

        public bool AutoAck { get; set; } = true;

        /// <summary>
        /// Polecenia, ktorych host CELOWO nie potwierdza od razu. Trzymamy
        /// otwarte tylko „play”, bo wlasnie wtedy uzytkownik moze wcisnac pauze
        /// albo stop. Gdyby host milczal na wszystko, „play” nigdy by nie
        /// wyszlo - adapter czeka najpierw na potwierdzenie glosnosci.
        /// </summary>
        public HashSet<string> WithholdAck { get; } = new(StringComparer.Ordinal);

        public JsonArray? Devices { get; set; }
        public (string Code, string Message)? AutoError { get; set; }

        /// <summary>Transport, ktory czyta te atrape; potrzebny do bariery.</summary>
        public LibrespotHostClient? Client { get; set; }

        public TextWriter StandardInput => new Pisarz(this);
        public TextReader StandardOutput => kanal.Reader;
        public bool HasExited { get; private set; }
        public int? ExitCode => HasExited ? 0 : null;

        public void Attach() => podlaczony = true;
        public void Kill() => HasExited = true;
        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            HasExited = true;
            return Task.CompletedTask;
        }
        public void Dispose() => HasExited = true;

        public void EmitReady() => EmitLine(new JsonObject
        {
            ["type"] = "ready",
            ["protocolVersion"] = LibrespotHostContract.ProtocolVersion
        });

        public void EmitState(long playId, string uri, long positionMs, long durationMs, bool isPlaying) =>
            EmitLine(new JsonObject
            {
                ["type"] = "state",
                ["sessionId"] = LibrespotHostContract.SessionId,
                ["playId"] = playId,
                ["uri"] = uri,
                ["positionMs"] = positionMs,
                ["durationMs"] = durationMs,
                ["isPlaying"] = isPlaying,
                ["isPaused"] = !isPlaying
            });

        public void EmitEnded(long playId, string uri) => EmitLine(new JsonObject
        {
            ["type"] = "ended",
            ["sessionId"] = LibrespotHostContract.SessionId,
            ["playId"] = playId,
            ["uri"] = uri
        });

        public void EmitTrackError(long playId, string uri, string code) => EmitLine(new JsonObject
        {
            ["type"] = "trackError",
            ["sessionId"] = LibrespotHostContract.SessionId,
            ["playId"] = playId,
            ["uri"] = uri,
            ["code"] = code
        });

        public void EmitLine(JsonObject payload) => kanal.Write(payload.ToJsonString());

        /// <summary>Potwierdza wszystkie polecenia, ktore czekaja bez odpowiedzi.</summary>
        public void AckAllPending()
        {
            long[] czekajace;
            lock (gate)
            {
                czekajace = bezOdpowiedzi.ToArray();
                bezOdpowiedzi.Clear();
            }
            foreach (var requestId in czekajace)
                EmitLine(new JsonObject { ["type"] = "ack", ["requestId"] = requestId });
        }

        /// <summary>
        /// Bariera oparta o prawdziwe polecenie protokolu. Wysylamy „ping” i
        /// czekamy na jego potwierdzenie. Czytelnik obsluguje linie po kolei, a
        /// potwierdzenie pingu trafia do kolejki PO wszystkim, co host wyslal
        /// wczesniej - wiec jego obsluga dowodzi obslugi wszystkiego przed nim.
        ///
        /// Poprzednia wersja wstrzykiwala tu sztuczne „ready” i liczyla pobrania
        /// z kolejki. To bylo bledne z dwoch powodow: zasmiecalo protokol
        /// zdarzeniem powitalnym w srodku sesji, a pobranie linii to jeszcze nie
        /// jej obsluzenie. Testy wychodzily raz zielone, raz czerwone.
        ///
        /// Gdy transport wlasnie padl (test awarii hosta), ping rzuca wyjatkiem.
        /// To tez jest dowod, ze linia bledu zostala obsluzona, wiec milczymy.
        /// </summary>
        public void Settle()
        {
            var client = Client;
            if (client is null) throw new InvalidOperationException("Brak klienta w atrapie hosta.");
            try
            {
                if (!client.PingAsync().Wait(TimeSpan.FromSeconds(10)))
                    throw new InvalidOperationException("Transport nie odpowiedział na ping w barierze.");
            }
            catch (AggregateException exception)
                when (exception.InnerException is LibrespotHostException)
            {
                // Transport padl - poprzednie linie sa juz obsluzone.
            }
            catch (LibrespotHostException)
            {
            }
        }

        public void WaitForCommand(string name)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                lock (gate)
                {
                    if (odebrane.Any(command => command.Name == name)) return;
                }
                Thread.Sleep(5);
            }
            throw new InvalidOperationException($"Host nie odebrał polecenia „{name}”.");
        }

        public List<string> CommandNames()
        {
            lock (gate) return odebrane.Select(command => command.Name).ToList();
        }

        /// <summary>
        /// Polecenia danego rodzaju, ktore doszly do hosta. CZEKA, az bedzie ich
        /// co najmniej <paramref name="minimum"/>.
        ///
        /// Czekanie jest tu konieczne, a nie ostrozne: adapter startuje
        /// odtwarzanie w tle i wysyla najpierw glosnosc, a dopiero potem „play”.
        /// Bariera pingu dowodzi tylko, ze transport obsluzyl linie PRZYCHODZACE
        /// od hosta - nie ze polecenie WYCHODZACE juz doszlo. Odczyt bez
        /// czekania mierzyl wyscig i sypal sie raz tu, raz w innym tescie.
        /// </summary>
        public List<Polecenie> Commands(string name, int minimum = 1)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (true)
            {
                lock (gate)
                {
                    var wybrane = odebrane.Where(command => command.Name == name).ToList();
                    if (wybrane.Count >= minimum) return wybrane;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    throw new InvalidOperationException(
                        $"Host nie odebrał polecenia „{name}” w oczekiwanej liczbie {minimum}.");
                }
                Thread.Sleep(5);
            }
        }

        public Polecenie Command(string name, int index = 0) =>
            Commands(name, index + 1)[index];

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
            var polecenie = new Polecenie(request);
            var requestId = request["requestId"] is JsonValue value
                && value.TryGetValue(out long identifier) ? identifier : 0L;
            // „ping” sluzy tu WYLACZNIE jako bariera testu, wiec nie zapisujemy
            // go do dziennika polecen. Inaczej ostatnim poleceniem po pauzie
            // bylby ping bariery i asercje o KOLEJNOSCI mierzylyby narzedzie
            // testowe, a nie zachowanie adaptera.
            var toBariera = polecenie.Name == LibrespotHostContract.CommandPing;
            if (!toBariera) lock (gate) odebrane.Add(polecenie);
            if (!podlaczony) return;

            if (AutoError is { } error && !toBariera)
            {
                EmitLine(new JsonObject
                {
                    ["type"] = "error",
                    ["requestId"] = requestId,
                    ["code"] = error.Code,
                    ["message"] = error.Message
                });
                return;
            }
            if (polecenie.Name == LibrespotHostContract.CommandDevices)
            {
                EmitLine(new JsonObject
                {
                    ["type"] = "devices",
                    ["requestId"] = requestId,
                    ["devices"] = Devices?.DeepClone() ?? new JsonArray
                    {
                        new JsonObject { ["name"] = "Domyślne", ["isDefault"] = true }
                    }
                });
                return;
            }
            if (AutoAck && !WithholdAck.Contains(polecenie.Name))
                EmitLine(new JsonObject { ["type"] = "ack", ["requestId"] = requestId });
            else lock (gate) bezOdpowiedzi.Add(requestId);
        }

        private sealed class Pisarz(AtrapaHosta host) : TextWriter
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

        /// <summary>Kanal linii udajacy stdout hosta; czytelnik nie konczy sie sam.</summary>
        private sealed class Kanal
        {
            private readonly SemaphoreSlim dostepne = new(0);
            private readonly Queue<string> linie = new();
            private readonly object kolejkaGate = new();
            private long zapisane;
            private long pobrane;

            public TextReader Reader { get; }
            public long Pobrane => Interlocked.Read(ref pobrane);

            public Kanal()
            {
                Reader = new Czytelnik(this);
            }

            public long Write(string line)
            {
                long numer;
                lock (kolejkaGate)
                {
                    linie.Enqueue(line + "\n");
                    numer = ++zapisane;
                }
                dostepne.Release();
                return numer;
            }

            private sealed class Czytelnik(Kanal kanal) : TextReader
            {
                private string bufor = string.Empty;
                private int offset;

                public override async Task<int> ReadAsync(char[] destination, int index, int count)
                {
                    while (offset >= bufor.Length)
                    {
                        await kanal.dostepne.WaitAsync().ConfigureAwait(false);
                        lock (kanal.kolejkaGate) bufor = kanal.linie.Dequeue();
                        Interlocked.Increment(ref kanal.pobrane);
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

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
