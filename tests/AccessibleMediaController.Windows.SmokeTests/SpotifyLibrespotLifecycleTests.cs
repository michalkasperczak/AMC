using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Testy CYKLU ZYCIA adaptera "Spotify — Librespot" wobec RZECZYWISTEGO
/// kontraktu hosta Rust.
///
/// Atrapa hosta jest tu celowo SUROWA i odwzorowuje to, co host naprawde robi
/// (native/AmcSpotifyLibrespotHost/src/main.rs):
/// - "devices" odpowiada BEZ konta i bez initialize,
/// - "initialize" wymaga NIEPUSTEGO tokenu (pusty/null = badType),
/// - DRUGI "initialize" w tym samym procesie = blad alreadyInitialized,
/// - play/pause/resume/stop/seek/volume przed initialize = notInitialized,
/// - "ack" dla "play" znaczy tylko "przyjete do kolejki", NIE "leci dzwiek".
///
/// Konta tu nie ma: token jest jawnie fikcyjny, a host to atrapa strumieni.
/// Ten zestaw NIE dowodzi odsluchu ani logowania do Spotify.
/// </summary>
internal static class SpotifyLibrespotLifecycleTests
{
    private const string FikcyjnyToken = "FIKCYJNY-TOKEN-TESTOWY-nie-jest-poswiadczeniem";

    internal static void Run()
    {
        // Ten sam powod, co w SpotifyLibrespotPreparationRaceTests: w pelnym
        // przebiegu suite'y stoi juz kontekst Dispatchera na STA, a te testy sa
        // async. Blokowanie na nim wprost zakleszcza sie, wiec calosc idzie
        // poza kontekstem. Tu uiInvoker jest albo natychmiastowy, albo recznie
        // opozniany w tescie - nie potrzebuje prawdziwego okna.
        Task.Run(async () =>
        {
            await ListaUrzadzenBezKonta();
            await PierwszyPlayLogujeRazZWybranymWyjsciem();
            await ZmianaWyjsciaWTrakcieOdtwarzaRealnyNowyProces();
            await ZmianaWyjsciaWPauzieNieWlaczaDzwieku();
            await SterowanieBezKontaNieUruchamiaHosta();
            await StopWInicjalizacjiNieDopuszczaPozniejszegoPlay();
            await StrazPrzygotowaniaKonczyCisze();
            await AwariaHostaNiePetliLogowaniaAleRecznyPlayWstaje();
            await StareZdarzeniaPoZamianieNieRuszajaNowego();
            await CudzyTransportNieJestZamykany();
            await ZamkniecieWysylaShutdownZamiastUbijac();
        }).GetAwaiter().GetResult();
        Console.WriteLine(
            "OK: cykl życia Librespot (leniwy start, lista wyjść bez konta,"
            + " zmiana wyjścia przez NOWY proces, pauza bez wycieku dźwięku,"
            + " straż przygotowania, brak samoczynnej pętli logowania)");
    }

    // ---------- Mierzone zachowania ----------

    /// <summary>Lista wyjsc musi dojsc bez konta: proces wstaje, initialize NIE idzie.</summary>
    private static async Task ListaUrzadzenBezKonta()
    {
        using var scena = Scena.Nowa();
        var urzadzenia = await scena.Output.GetOutputDevicesAsync();

        Check(urzadzenia.Count == 2, "Lista wyjść musi dojść bez konta");
        Check(urzadzenia[1].Name == "Karta USB (2- Audio)", "Nazwa wyjścia musi być DOKŁADNA");
        var host = scena.Hosts.Single();
        Check(host.Names().Contains("devices"), "Lista wyjść idzie poleceniem devices");
        Check(!host.Names().Contains("initialize"),
            "Sama lista wyjść NIE MOŻE logować konta ani wysyłać initialize");
        Check(host.Tokens() == 0, "Żadne polecenie listy wyjść nie może nieść tokenu");
    }

    /// <summary>Pierwszy Play loguje RAZ i przekazuje wyjscie wybrane wczesniej.</summary>
    private static async Task PierwszyPlayLogujeRazZWybranymWyjsciem()
    {
        using var scena = Scena.Nowa(device: "Karta USB (2- Audio)");
        Check(!scena.Output.IsHostRunning, "Przed pierwszym użyciem host NIE MOŻE działać");

        scena.Output.Play(Utwor("A"), TimeSpan.FromSeconds(12), 44, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));

        var host = scena.Hosts.Single();
        var initialize = host.Commands("initialize").Single();
        Check(initialize["accessToken"]?.GetValue<string>() == FikcyjnyToken,
            "Token musi wejść do hosta WYŁĄCZNIE w initialize przez stdin");
        Check(initialize["device"]?.GetValue<string>() == "Karta USB (2- Audio)",
            "Wyjście wybrane przed Play musi trafić do PIERWSZEGO initialize");
        var play = host.Commands("play").Single();
        Check(play["uri"]?.GetValue<string>() == "spotify:track:A", "Play musi nieść URI utworu");
        Check(Liczba(play, "positionMs") == 12_000, "Play musi nieść zapamiętaną pozycję");
        Check(host.Tokens() == 1, "Token przechodzi DOKŁADNIE raz, w initialize");
        Check(scena.Failed.Count == 0, $"Poprawny pierwszy Play nie może zgłaszać błędu: {scena.Blad}");
    }

    /// <summary>
    /// Zmiana wyjscia GDY GRA: nowy PROCES, nie drugie initialize. Stary proces
    /// musi byc zakonczony PRZED startem nowego, inaczej dwa wyjscia naklada
    /// na siebie dzwiek.
    /// </summary>
    private static async Task ZmianaWyjsciaWTrakcieOdtwarzaRealnyNowyProces()
    {
        using var scena = Scena.Nowa();
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var pierwszy = scena.Hosts.Single();
        pierwszy.EmitState(pierwszy.LastPlayId, "spotify:track:A", 40_000, 200_000, isPlaying: true);
        await scena.Settle(pierwszy);
        Check(scena.Started.Count == 1, "Stan isPlaying musi zgłosić start");

        var zmiana = await scena.Output.TrySetOutputDeviceAsync("Karta USB (2- Audio)");
        Check(zmiana, $"Zmiana wyjścia na nazwę z listy musi się udać: {scena.Blad}");

        Check(scena.Hosts.Count == 2, "Zmiana wyjścia MUSI postawić NOWY proces hosta");
        var nowy = scena.Hosts[1];
        Check(pierwszy.HasExited, "STARY proces musi być zakończony, żeby dwa wyjścia nie grały razem");
        Check(pierwszy.KilledAt < nowy.StartedAt,
            "Stary proces musi zniknąć PRZED startem nowego, nie po nim");
        Check(pierwszy.Commands("initialize").Count == 1,
            "Do STAREGO procesu nie wolno wysłać drugiego initialize - host go odrzuca");

        var initialize = nowy.Commands("initialize").Single();
        Check(initialize["device"]?.GetValue<string>() == "Karta USB (2- Audio)",
            "NOWY proces musi dostać DOKŁADNĄ nazwę nowego wyjścia");
        Check(initialize["accessToken"]?.GetValue<string>() == FikcyjnyToken,
            "NOWY proces musi dostać NORMALNY token, nie accessToken=null");
        Check(initialize["keepUri"] is null && initialize["keepPlayId"] is null
            && initialize["keepPositionMs"] is null && initialize["keepPlaying"] is null,
            "Zmyślonych pól keepUri/keepPlayId/keepPositionMs/keepPlaying nie wolno wysyłać");

        var play = nowy.Commands("play").Single();
        Check(play["uri"]?.GetValue<string>() == "spotify:track:A",
            "Po zmianie wyjścia musi wrócić TEN SAM utwór");
        Check(Liczba(play, "positionMs") == 40_000,
            $"Po zmianie wyjścia utwór musi wrócić od zapamiętanej pozycji; wysłano {Liczba(play, "positionMs")} ms");
        Check(scena.Output.SelectedOutputDeviceName == "Karta USB (2- Audio)",
            "Wybrane wyjście musi być zapamiętane");
        Check(scena.Failed.Count == 0, $"Udana zmiana wyjścia nie może zgłaszać błędu: {scena.Blad}");
    }

    /// <summary>
    /// Zmiana wyjscia W PAUZIE nie moze wlaczyc dzwieku. Wczesniejsza droga
    /// (Play, a potem Pause na nowym procesie) przez chwile puszczala muzyke -
    /// dla uzytkownika niewidomego to wyciek dzwieku, nie kosmetyka.
    /// </summary>
    private static async Task ZmianaWyjsciaWPauzieNieWlaczaDzwieku()
    {
        using var scena = Scena.Nowa();
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var pierwszy = scena.Hosts.Single();
        pierwszy.EmitState(pierwszy.LastPlayId, "spotify:track:A", 33_000, 200_000, isPlaying: true);
        await scena.Settle(pierwszy);
        scena.Output.Pause();
        await scena.Settle(pierwszy);

        Check(await scena.Output.TrySetOutputDeviceAsync("Karta USB (2- Audio)"),
            $"Zmiana wyjścia w pauzie musi się udać: {scena.Blad}");
        Check(pierwszy.HasExited,
            "STARY proces musi zniknąć także w pauzie - trzymałby stare wyjście dźwięku");
        Check(scena.Hosts.Count == 1,
            "PAUZA: nowego procesu nie wolno stawiać z wyprzedzeniem - to on zacząłby grać");
        Check(scena.Output.Position == TimeSpan.FromSeconds(33),
            "Pozycja w pauzie musi zostać zapamiętana lokalnie");

        // Dzwiek wraca dopiero na ZADANIE uzytkownika.
        scena.Output.Resume();
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        Check(scena.Hosts.Count == 2, "Resume po zmianie wyjścia stawia NOWY proces");
        var nowy = scena.Hosts[1];
        var play = nowy.Commands("play").Single();
        Check(Liczba(play, "positionMs") == 33_000,
            $"Resume po zmianie wyjścia musi wrócić od zapamiętanej pozycji; wysłano {Liczba(play, "positionMs")} ms");
        Check(nowy.Commands("initialize").Single()["device"]?.GetValue<string>()
            == "Karta USB (2- Audio)", "Nowy proces gra na NOWYM wyjściu");
        Check(nowy.Commands("initialize").Single()["accessToken"]?.GetValue<string>()
            == FikcyjnyToken, "Nowy proces loguje się NORMALNYM tokenem");
    }

    /// <summary>
    /// Pause/Stop/Seek/SetVolume przed zalogowaniem nie moga uruchamiac hosta
    /// ani krzyczec "nie uruchomiono". To zwykly zamiar uzytkownika.
    /// </summary>
    private static async Task SterowanieBezKontaNieUruchamiaHosta()
    {
        using var scena = Scena.Nowa();
        scena.Output.SetVolume(31);
        scena.Output.Seek(TimeSpan.FromSeconds(9));
        scena.Output.Pause();
        scena.Output.Stop();
        await Task.Delay(120);

        Check(scena.Hosts.Count == 0,
            "Sterowanie przed logowaniem NIE MOŻE stawiać procesu hosta");
        Check(scena.Failed.Count == 0,
            $"Sterowanie przed logowaniem nie może dawać błędu host_not_started: {scena.Blad}");

        // Zapamietana glosnosc musi realnie trafic do PIERWSZEGO logowania.
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 31, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var initialize = scena.Hosts.Single().Commands("initialize").Single();
        Check(initialize["volume"]!.GetValue<int>() == 31,
            "Głośność ustawiona przed logowaniem musi wejść do initialize");
    }

    /// <summary>Stop w trakcie INICJALIZACJI nie dopuszcza zadnego pozniejszego Play.</summary>
    private static async Task StopWInicjalizacjiNieDopuszczaPozniejszegoPlay()
    {
        using var scena = Scena.Nowa(withholdInitialize: true);
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        var czeka = scena.Output.PendingPlaybackStart;
        var host = await scena.WaitForHost();
        var initialize = await host.AwaitWithheld("initialize");

        scena.Output.Stop();
        host.Ack(initialize);
        await czeka.WaitAsync(TimeSpan.FromSeconds(5));
        await scena.Settle(host);

        Check(host.Commands("play", minimum: 0).Count == 0,
            "Stop w trakcie inicjalizacji musi odciąć KAŻDE późniejsze play");
        Check(!scena.Output.IsPreparing, "Anulowane przygotowanie nie może trwać dalej");
        Check(scena.Started.Count == 0, "Po Stop w inicjalizacji nie wolno zgłosić startu");
    }

    /// <summary>
    /// "ack" dla play nie znaczy dzwiek. Bez strazy przygotowania sesja umiałaby
    /// stac w ciszy bez konca; straz musi to POWIEDZIEC.
    /// </summary>
    private static async Task StrazPrzygotowaniaKonczyCisze()
    {
        using var scena = Scena.Nowa(preparationTimeout: TimeSpan.FromMilliseconds(300));
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var host = scena.Hosts.Single();
        Check(host.Commands("play").Count == 1, "Host musi przyjąć play");
        Check(scena.Output.IsPreparing, "Po samym ack przygotowanie wciąż trwa - ack to nie dźwięk");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (scena.Failed.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);

        Check(scena.Failed.Count == 1,
            "Przyjęty play, po którym dźwięk nie zaczął się w limicie, MUSI być powiedziany");
        Check(scena.Failed[0].Message.Contains("dźwięk nie zaczął się"),
            $"Komunikat musi nazwać powód, a nie milczeć; otrzymano: {scena.Blad}");
        Check(!scena.Output.IsPreparing, "Straż przygotowania musi zakończyć stan przygotowania");
    }

    /// <summary>
    /// Po awarii hosta NIE wolno wchodzic w samoczynna petle logowania. Dopiero
    /// RECZNY Play stawia nowy proces.
    /// </summary>
    private static async Task AwariaHostaNiePetliLogowaniaAleRecznyPlayWstaje()
    {
        using var scena = Scena.Nowa();
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var pierwszy = scena.Hosts.Single();
        pierwszy.EmitState(pierwszy.LastPlayId, "spotify:track:A", 5_000, 200_000, isPlaying: true);
        await scena.Settle(pierwszy);

        pierwszy.Crash();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (scena.Failed.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        Check(scena.Failed.Count >= 1, "Awaria hosta musi być powiedziana wprost, nie przemilczana");
        await Task.Delay(300);
        Check(scena.Hosts.Count == 1,
            "Po awarii adapter NIE MOŻE sam wstawać i logować się w pętli");

        // Reczny Play - i tylko on - stawia nowy proces.
        scena.Output.Play(Utwor("B"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        Check(scena.Hosts.Count == 2, "RĘCZNY Play po awarii musi postawić nowy proces");
        Check(scena.Hosts[1].Commands("play").Single()["uri"]?.GetValue<string>()
            == "spotify:track:B", "Nowy proces gra to, co użytkownik wybrał");
    }

    /// <summary>
    /// Zdarzenia STAREGO procesu (takze te odlozone na watek interfejsu) nie
    /// moga ruszyc nowego stanu. Czytnik ekranu przeczytalby nie ten utwor.
    /// </summary>
    private static async Task StareZdarzeniaPoZamianieNieRuszajaNowego()
    {
        var kolejka = new List<Action>();
        using var scena = Scena.Nowa(uiInvoker: kolejka.Add);
        scena.Output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await scena.Output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var pierwszy = scena.Hosts.Single();
        pierwszy.EmitState(pierwszy.LastPlayId, "spotify:track:A", 10_000, 200_000, isPlaying: true);
        await scena.Settle(pierwszy);
        Odpal(kolejka);
        Check(scena.Started.Count == 1, "Start pierwszego utworu musi być zgłoszony");

        var staryPlayId = pierwszy.LastPlayId;
        Check(await scena.Output.TrySetOutputDeviceAsync("Karta USB (2- Audio)"),
            $"Zmiana wyjścia musi się udać: {scena.Blad}");
        Odpal(kolejka);
        var poZamianie = scena.Hosts[1];
        var play = poZamianie.Commands("play").Single();
        Check(Liczba(play, "positionMs") == 10_000,
            $"Nowy proces musi wrócić od pozycji z chwili zmiany; wysłano {Liczba(play, "positionMs")} ms");

        // Zdarzenia STAREGO procesu wyslane JUZ PO zamianie. Stary host i tak
        // nie zyje, ale gdyby jego petla czytania dosłała jeszcze linie, nie
        // wolno im ruszyc nowego utworu.
        var pozycjaPrzed = scena.Output.Position;
        pierwszy.EmitState(staryPlayId, "spotify:track:A", 99_000, 200_000, isPlaying: true);
        pierwszy.EmitEnded(staryPlayId, "spotify:track:A");
        await Task.Delay(150);
        Odpal(kolejka);

        Check(scena.Ended.Count == 0, "Koniec ze STAREGO procesu nie może zakończyć nowego utworu");
        Check(scena.Output.Position != TimeSpan.FromMilliseconds(99_000),
            $"Stan ze STAREGO procesu nie może przesunąć pozycji nowego; pozycja: {scena.Output.Position}");
        Check(scena.Output.LoadedItemId is not null,
            "Stary proces nie może wyczyścić wczytanego utworu nowej generacji");
        Check(pozycjaPrzed >= TimeSpan.Zero, "Pozycja musi być odczytywalna po zamianie");
    }

    /// <summary>
    /// Transport podany z zewnatrz (stary konstruktor) jest CUDZY: Dispose go
    /// nie zamyka. W testach zamknelibysmy zycie, ktorego nie stworzylismy.
    /// </summary>
    private static async Task CudzyTransportNieJestZamykany()
    {
        var host = new AtrapaHosta(devices: DomyslneUrzadzenia());
        var client = new LibrespotHostClient(() => host, SzybkieOpcje, _ => Task.FromResult(FikcyjnyToken));
        await client.StartAsync();
        host.Attach(client);
        var output = new SpotifyLibrespotMediaOutput(client, action => action());
        output.Dispose();

        Check(!host.HasExited, "Dispose NIE MOŻE zamykać cudzego transportu ani jego procesu");
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(3));
        client.Dispose();
        host.Dispose();
    }

    /// <summary>
    /// Zamkniecie sesji MUSI wyslac do hosta "shutdown", a nie tylko go ubic.
    ///
    /// Blad, ktory to lamal: DisposeAsync ustawialo disposed=true PRZED
    /// ShutdownAsync. SendAsync sprawdza te flage pierwsza linia, wiec polecenie
    /// odbijalo sie od wlasnego Dispose i zostawal sam Kill(). Ubity host nie
    /// oddaje urzadzenia audio po dobremu.
    /// </summary>
    private static async Task ZamkniecieWysylaShutdownZamiastUbijac()
    {
        var hosty = new List<AtrapaHosta>();
        var output = new SpotifyLibrespotMediaOutput(
            () =>
            {
                var host = new AtrapaHosta(devices: DomyslneUrzadzenia());
                hosty.Add(host);
                return new LibrespotHostClient(
                    () => host, SzybkieOpcje, _ => Task.FromResult(FikcyjnyToken));
            },
            akcja => akcja(),
            deviceName: null);

        output.Play(Utwor("A"), TimeSpan.Zero, 70, 1d);
        await output.PendingPlaybackStart.WaitAsync(TimeSpan.FromSeconds(5));
        var host = hosty.Single();
        host.Commands("play");

        output.Dispose();
        // Dispose adaptera zamyka wlasny transport asynchronicznie w tle;
        // czekamy na faktyczne wyjscie procesu, nie na sam powrot z Dispose.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!host.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25);

        Check(host.Names().Contains("shutdown"),
            "Zamknięcie sesji musi WYSŁAĆ do hosta polecenie shutdown, nie tylko ubić proces");
        Check(host.WyszedlSam,
            "Host musi zakończyć się po swojemu (po shutdown), nie przez Kill z limitu czasu");
        Check(host.HasExited, "Po zamknięciu sesji proces hosta nie może zostać w pamięci");
    }

    // ---------- Atrapy ----------

    private static LibrespotHostOptions SzybkieOpcje => new()
    {
        ReadyTimeout = TimeSpan.FromSeconds(5),
        RequestTimeout = TimeSpan.FromSeconds(5),
        InitializeTimeout = TimeSpan.FromSeconds(5),
        ShutdownTimeout = TimeSpan.FromMilliseconds(200)
    };

    private static JsonArray DomyslneUrzadzenia() =>
    [
        new JsonObject { ["name"] = "Wyjście domyślne", ["isDefault"] = true },
        new JsonObject { ["name"] = "Karta USB (2- Audio)", ["isDefault"] = false }
    ];

    private static MediaItem Utwor(string id) => new()
    {
        Id = id,
        ExternalId = id,
        Source = "spotify:track:" + id,
        Title = id,
        Kind = MediaItemKind.Track
    };

    private static void Odpal(List<Action> kolejka)
    {
        var praca = kolejka.ToArray();
        kolejka.Clear();
        foreach (var action in praca) action();
    }

    private static long Liczba(JsonObject root, string name)
    {
        if (root[name] is not JsonValue value) return -1;
        if (value.TryGetValue(out long number)) return number;
        return value.TryGetValue(out int small) ? small : -1;
    }

    /// <summary>
    /// Adapter z FABRYKA transportu: kazde wywolanie fabryki to nowy proces
    /// hosta, dokladnie tak jak w programie.
    /// </summary>
    private sealed class Scena : IDisposable
    {
        private readonly List<AtrapaHosta> hosts = [];
        private readonly object gate = new();

        public required SpotifyLibrespotMediaOutput Output { get; init; }
        public List<MediaPlaybackStartedEventArgs> Started { get; } = [];
        public List<MediaPlaybackEndedEventArgs> Ended { get; } = [];
        public List<MediaOutputFailedEventArgs> Failed { get; } = [];

        public List<AtrapaHosta> Hosts
        {
            get { lock (gate) return hosts.ToList(); }
        }

        public string Blad => Failed.Count == 0 ? "brak błędu" : Failed[^1].Message;

        public static Scena Nowa(
            string? device = null,
            bool withholdInitialize = false,
            TimeSpan? preparationTimeout = null,
            Action<Action>? uiInvoker = null)
        {
            Scena? scena = null;
            var output = new SpotifyLibrespotMediaOutput(
                () =>
                {
                    var host = new AtrapaHosta(DomyslneUrzadzenia());
                    if (withholdInitialize) host.WithholdAck.Add("initialize");
                    var client = new LibrespotHostClient(
                        () => host, SzybkieOpcje, _ => Task.FromResult(FikcyjnyToken));
                    host.Attach(client);
                    scena!.Register(host);
                    return client;
                },
                uiInvoker ?? (action => action()),
                device,
                preparationTimeout ?? TimeSpan.FromSeconds(30));
            scena = new Scena { Output = output };
            output.PlaybackStarted += (_, e) => scena.Started.Add(e);
            output.PlaybackEnded += (_, e) => scena.Ended.Add(e);
            output.PlaybackFailed += (_, e) => scena.Failed.Add(e);
            return scena;
        }

        private void Register(AtrapaHosta host)
        {
            lock (gate) hosts.Add(host);
        }

        public async Task<AtrapaHosta> WaitForHost()
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                lock (gate)
                {
                    if (hosts.Count > 0) return hosts[^1];
                }
                await Task.Delay(10);
            }
            throw new InvalidOperationException("Adapter nie postawił procesu hosta.");
        }

        /// <summary>
        /// Bariera na prawdziwym poleceniu protokolu: "ping" jest obslugiwany po
        /// wszystkim, co host wyslal wczesniej, wiec jego potwierdzenie dowodzi
        /// obslugi tych linii. Liczenie pobran z kolejki bylo niepewne.
        /// </summary>
        public async Task Settle(AtrapaHosta host)
        {
            try
            {
                await host.Client!.PingAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (LibrespotHostException)
            {
                // Transport padl - wczesniejsze linie sa juz obsluzone.
            }
        }

        public void Dispose()
        {
            Output.Dispose();
            foreach (var host in Hosts) host.Dispose();
        }
    }

    /// <summary>
    /// Atrapa hosta trzymajaca sie RZECZYWISTYCH regul procesu Rust: token musi
    /// byc niepusty, drugi initialize jest odrzucany, polecenia sterujace przed
    /// initialize daja notInitialized.
    /// </summary>
    private sealed class AtrapaHosta : ILibrespotHostProcess
    {
        private static long licznik;
        private readonly Kanal kanal = new();
        private readonly ConcurrentQueue<JsonObject> odebrane = new();
        private readonly Channel<JsonObject> wstrzymane = Channel.CreateUnbounded<JsonObject>();
        private readonly JsonArray devices;
        private bool initialized;

        public AtrapaHosta(JsonArray devices)
        {
            this.devices = devices;
            StartedAt = Interlocked.Increment(ref licznik);
            EmitLine(new JsonObject
            {
                ["type"] = "ready",
                ["protocolVersion"] = LibrespotHostContract.ProtocolVersion,
                ["sessionId"] = LibrespotHostContract.SessionId
            });
        }

        /// <summary>Kolejnosc powstania i ubicia - dowod, ze stary ginie PRZED nowym.</summary>
        public long StartedAt { get; }
        public long KilledAt { get; private set; } = long.MaxValue;

        public HashSet<string> WithholdAck { get; } = new(StringComparer.Ordinal);
        public LibrespotHostClient? Client { get; private set; }
        public long LastPlayId { get; private set; }

        public TextWriter StandardInput => new Pisarz(Accept);
        public TextReader StandardOutput => kanal;
        public bool HasExited { get; private set; }
        public int? ExitCode => HasExited ? 0 : null;

        public void Attach(LibrespotHostClient client) => Client = client;

        /// <summary>Czy host wyszedl na wlasnych nogach (po "shutdown"), czy zostal ubity.</summary>
        public bool WyszedlSam { get; private set; }

        public void Kill()
        {
            if (HasExited) return;
            HasExited = true;
            KilledAt = Interlocked.Increment(ref licznik);
            kanal.End();
        }

        /// <summary>Pad procesu bez zamkniecia sesji przez AMC.</summary>
        public void Crash() => Kill();

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            // Prawdziwy host konczy sie SAM po przyjeciu "shutdown"; tu
            // odwzorowujemy to, zeby dalo sie odroznic wyjscie od ubicia.
            if (Names().Contains("shutdown")) WyszedlSam = true;
            Kill();
            return Task.CompletedTask;
        }

        public void Dispose() => Kill();

        public List<string> Names() =>
            odebrane.Select(command => command["command"]?.GetValue<string>() ?? string.Empty).ToList();

        public int Tokens() =>
            odebrane.Count(command => command.ToJsonString().Contains(FikcyjnyToken));

        /// <summary>Polecenia danego rodzaju; CZEKA na <paramref name="minimum"/> sztuk.</summary>
        public List<JsonObject> Commands(string name, int minimum = 1)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (true)
            {
                var wybrane = odebrane
                    .Where(command => command["command"]?.GetValue<string>() == name)
                    .ToList();
                if (wybrane.Count >= minimum) return wybrane;
                if (DateTime.UtcNow >= deadline)
                {
                    throw new InvalidOperationException(
                        $"Host nie odebrał polecenia „{name}” w liczbie {minimum}; "
                        + $"odebrano: {string.Join(", ", Names())}.");
                }
                Thread.Sleep(5);
            }
        }

        public async Task<JsonObject> AwaitWithheld(string name)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                var command = await wstrzymane.Reader.ReadAsync(timeout.Token);
                if (command["command"]?.GetValue<string>() == name) return command;
            }
        }

        private void Accept(string text)
        {
            var command = JsonNode.Parse(text)!.AsObject();
            odebrane.Enqueue(command);
            var name = command["command"]?.GetValue<string>() ?? string.Empty;
            if (name == "play") LastPlayId = Liczba(command, "playId");

            if (WithholdAck.Contains(name))
            {
                wstrzymane.Writer.TryWrite(command);
                return;
            }

            switch (name)
            {
                case "ping":
                case "shutdown":
                    Ack(command);
                    return;

                // Lista wyjsc NIE wymaga konta - tak samo jak w hoscie Rust.
                case "devices":
                    EmitLine(new JsonObject
                    {
                        ["type"] = "devices",
                        ["sessionId"] = LibrespotHostContract.SessionId,
                        ["requestId"] = command["requestId"]!.DeepClone(),
                        ["devices"] = devices.DeepClone()
                    });
                    return;

                case "initialize":
                    if (initialized)
                    {
                        // Host Rust ODRZUCA kazdy kolejny initialize.
                        Error(command, "alreadyInitialized", "sesja jest już zainicjowana");
                        return;
                    }
                    if (command["accessToken"]?.GetValue<string>() is not { Length: > 0 })
                    {
                        // Host Rust wymaga NIEPUSTEGO tokenu.
                        Error(command, "badType", "accessToken: wymagany niepusty tekst");
                        return;
                    }
                    initialized = true;
                    Ack(command);
                    return;

                default:
                    if (!initialized)
                    {
                        Error(command, "notInitialized", "najpierw initialize");
                        return;
                    }
                    Ack(command);
                    return;
            }
        }

        public void Ack(JsonObject command) => EmitLine(new JsonObject
        {
            ["type"] = "ack",
            ["sessionId"] = LibrespotHostContract.SessionId,
            ["requestId"] = command["requestId"]!.DeepClone()
        });

        private void Error(JsonObject command, string code, string message) => EmitLine(new JsonObject
        {
            ["type"] = "error",
            ["sessionId"] = LibrespotHostContract.SessionId,
            ["requestId"] = command["requestId"]!.DeepClone(),
            ["code"] = code,
            ["message"] = message
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

        public void EmitLine(JsonObject payload) => kanal.Write(payload.ToJsonString());
    }

    private sealed class Pisarz(Action<string> accept) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override Task WriteLineAsync(string? value)
        {
            accept(value!);
            return Task.CompletedTask;
        }

        public override Task FlushAsync() => Task.CompletedTask;
    }

    private sealed class Kanal : TextReader
    {
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>();

        public void Write(string line) => lines.Writer.TryWrite(line + "\n");
        public void End() => lines.Writer.TryComplete();

        public override async ValueTask<int> ReadAsync(
            Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await lines.Reader.WaitToReadAsync(cancellationToken)) return 0;
                var line = await lines.Reader.ReadAsync(cancellationToken);
                if (line.Length > buffer.Length)
                    throw new InvalidOperationException("Linia atrapy nie mieści się w buforze.");
                line.AsMemory().CopyTo(buffer);
                return line.Length;
            }
            catch (ChannelClosedException)
            {
                return 0;
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
