using System.Text;
using System.Text.Json.Nodes;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// Testy transportu do osobnego procesu hosta Librespot (sesja
/// "Spotify — Librespot").
///
/// Czesc testow uruchamia PRAWDZIWY proces (ten sam plik wykonywalny w trybie
/// atrapy) z przekierowanymi stdin/stdout, a nie tylko parsuje tekst. Dlatego
/// mierzymy tu to, co faktycznie zawodzi w praniu: awarie procesu, EOF,
/// przekroczony czas, nieznana wersja protokolu i wymieszane zapisy.
///
/// ZADNEGO konta i zadnego sekretu tu nie ma: token jest jawnie fikcyjny, a
/// "odtwarzanie" to same zdarzenia protokolu. Ten zestaw nie dowodzi odsluchu.
/// </summary>
internal static class LibrespotHostClientTests
{
    internal static void Run()
    {
        // Zdarzenia i stan - atrapa strumieni, bez procesu.
        ReadyEventDoesNotMeanSignedIn();
        UnknownProtocolVersionIsLoudNotSilent();
        SamePlayIdCorrelatesAckWithCommand();
        OldPlayIdEventsCannotTouchNewPlayback();
        SameUriTwiceGetsSeparatePlayIds();
        EndedBeforeConfirmedStartIsIgnored();
        PauseDuringPreparationDoesNotStartMusic();
        StopDuringPreparationDoesNotStartMusic();
        TrackErrorClearsPreparationAndReportsCode();
        DeviceListWorksWithoutAccount();
        DeviceChangeIsRefusedNotFaked();
        VolumeSeekAndTimeReachTheHost();
        TokenTravelsOnlyThroughStdinInitialize();
        TokenIsRejectedInProcessArguments();
        CredentialProviderIsInjectedNotHardwired();
        ErrorWithoutRequestIdBecomesSessionFailure();
        GarbageLineIsProtocolFailureNotSilence();
        SerializedWritesProduceWholeJsonLines();

        // Prawdziwy proces - transport od konca do konca.
        RealProcessHandshakeAndCommands();
        RealProcessTimeoutIsReported();
        RealProcessExitFailsPendingRequests();
        RealProcessBadVersionFails();
        RealProcessLongLineIsBounded();
        RealProcessCleanupKillsHost();
    }

    private const string FikcyjnyToken = "FIKCYJNY-TOKEN-TESTOWY-nie-jest-poswiadczeniem";

    // ---------- Testy na atrapie strumieni ----------

    private static void ReadyEventDoesNotMeanSignedIn()
    {
        using var host = new FakeHost();
        var client = NewClient(host);
        host.EmitReady(LibrespotHostContract.ProtocolVersion);
        client.StartAsync().GetAwaiter().GetResult();

        Check(client.NegotiatedProtocolVersion == 1, "Wersja protokołu musi być uzgodniona na 1");
        // "ready" mowi tylko o procesie. Bez initialize nie ma poswiadczen,
        // wiec zmiana urzadzenia musi odmowic, a nie udawac zalogowanie.
        var failure = Throws(() => client.SetOutputDeviceAsync(null).GetAwaiter().GetResult());
        Check(failure?.Code == LibrespotHostErrorCodes.NotStarted,
            "Gotowość procesu nie może być traktowana jak zalogowane konto");
        client.Dispose();
    }

    private static void UnknownProtocolVersionIsLoudNotSilent()
    {
        using var host = new FakeHost();
        var client = NewClient(host);
        var failures = new List<LibrespotHostFailureEventArgs>();
        client.HostFailed += (_, e) => failures.Add(e);
        host.EmitReady(99);

        var error = Throws(() => client.StartAsync().GetAwaiter().GetResult());
        Check(error?.Code == LibrespotHostErrorCodes.ProtocolVersionMismatch,
            "Nieznana wersja protokołu musi dać błąd o niezgodnej wersji");
        Check(error!.Message.Contains("99") && error.Message.Contains("1"),
            "Komunikat musi podać obie wersje, żeby użytkownik wiedział, co zaktualizować");
        Check(failures.Count == 1, "Niezgodna wersja protokołu musi też zgłosić awarię sesji");
        client.Dispose();
    }

    private static void SamePlayIdCorrelatesAckWithCommand()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        client.PingAsync().GetAwaiter().GetResult();
        client.SetVolumeAsync(42).GetAwaiter().GetResult();

        var commands = host.WrittenCommands();
        Check(commands.Count == 2, "Dwa polecenia muszą dać dwie linie wejścia");
        Check(commands[0]["requestId"]!.GetValue<long>() == 1
            && commands[1]["requestId"]!.GetValue<long>() == 2,
            "requestId musi rosnąć i korelować odpowiedź z poleceniem");
        Check(commands.All(command =>
                command["sessionId"]!.GetValue<string>() == "spotifyLibrespot"),
            "Każde polecenie musi nieść sessionId nowej sesji");
        client.Dispose();
    }

    private static void OldPlayIdEventsCannotTouchNewPlayback()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;

        client.PlayAsync("spotify:track:AAA", TimeSpan.Zero, 10).GetAwaiter().GetResult();
        host.EmitState(10, "spotify:track:AAA", 5_000, 180_000, isPlaying: true);
        host.Settle();
        client.PlayAsync("spotify:track:BBB", TimeSpan.Zero, 11).GetAwaiter().GetResult();

        var states = new List<LibrespotStateEventArgs>();
        var ended = new List<LibrespotEndedEventArgs>();
        client.StateChanged += (_, e) => states.Add(e);
        client.PlaybackEnded += (_, e) => ended.Add(e);

        // Spoznione zdarzenia PIERWSZEJ proby.
        host.EmitState(10, "spotify:track:AAA", 170_000, 180_000, isPlaying: true);
        host.EmitEnded(10, "spotify:track:AAA");
        host.Settle();

        Check(states.Count == 0, "Stan starego playId nie może zmienić czasu nowego utworu");
        Check(ended.Count == 0, "Koniec starego playId nie może zakończyć nowego utworu");
        Check(client.CurrentPlayId == 11, "Bieżące playId musi zostać nowe");
        Check(client.CurrentUri == "spotify:track:BBB", "Bieżący URI musi zostać nowy");
        client.Dispose();
    }

    private static void SameUriTwiceGetsSeparatePlayIds()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        var ended = new List<long>();
        client.PlaybackEnded += (_, e) => ended.Add(e.PlayId);

        const string uri = "spotify:track:POWTORKA";
        client.PlayAsync(uri, TimeSpan.Zero, 20).GetAwaiter().GetResult();
        host.EmitState(20, uri, 1_000, 120_000, isPlaying: true);
        host.Settle();
        // Uzytkownik uruchamia TEN SAM utwor po raz drugi.
        client.PlayAsync(uri, TimeSpan.Zero, 21).GetAwaiter().GetResult();
        // DRUGA proba jest JUZ POTWIERDZONA jako grajaca. To jest istotne:
        // gdyby nie byla, spozniony koniec odpadlby na warunku "nie zagralo
        // jeszcze nic", a nie na porownaniu playId - i test nie mierzylby tego,
        // co ma mierzyc.
        host.EmitState(21, uri, 2_000, 120_000, isPlaying: true);
        host.Settle();
        Check(!client.IsPreparing && client.CurrentPlayId == 21,
            "Druga próba musi być potwierdzona jako grająca przed pomiarem");

        // Spozniony koniec PIERWSZEJ proby: TEN SAM URI, stare playId. Odrzucic
        // go moze WYLACZNIE porownanie playId.
        host.EmitEnded(20, uri);
        host.Settle();
        Check(ended.Count == 0,
            "Ten sam URI uruchomiony dwa razy nie może dostać końca pierwszej próby");
        Check(client.CurrentPlayId == 21,
            "Koniec starej próby nie może wyczyścić bieżącej próby odtwarzania");

        host.EmitEnded(21, uri);
        host.Settle();
        Check(ended.Count == 1 && ended[0] == 21, "Koniec drugiej próby musi przejść");
        client.Dispose();
    }

    private static void EndedBeforeConfirmedStartIsIgnored()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        var ended = 0;
        client.PlaybackEnded += (_, _) => ended++;

        client.PlayAsync("spotify:track:CCC", TimeSpan.Zero, 30).GetAwaiter().GetResult();
        host.EmitEnded(30, "spotify:track:CCC");
        host.Settle();

        Check(ended == 0,
            "Koniec przed potwierdzonym startem to zerwane przygotowanie, nie koniec utworu");
        client.Dispose();
    }

    private static void PauseDuringPreparationDoesNotStartMusic()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        // Host zwleka z potwierdzeniem "graj". W tym czasie uzytkownik pauzuje.
        host.AutoAck = false;
        var play = client.PlayAsync("spotify:track:DDD", TimeSpan.Zero, 40);
        host.AutoAck = true;
        client.PauseAsync().GetAwaiter().GetResult();
        host.AckRequest(1);
        var outcome = play.GetAwaiter().GetResult();

        Check(outcome == LibrespotPlayOutcome.SupersededByPause,
            "Pauza w trakcie przygotowania musi dać wynik SupersededByPause");
        Check(!client.IsPreparing, "Po pauzie w trakcie przygotowania nie trwa już przygotowanie");
        var commands = host.WrittenCommands();
        Check(commands.Count(command => command["command"]!.GetValue<string>() == "pause") == 2,
            "Pauza musi trafić do hosta PONOWNIE po potwierdzeniu grania, inaczej muzyka wystartuje");
        Check(commands[^1]["command"]!.GetValue<string>() == "pause",
            "Ostatnim poleceniem po przerwanym przygotowaniu musi być pauza, nie graj");
        client.Dispose();
    }

    private static void StopDuringPreparationDoesNotStartMusic()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = false;
        var play = client.PlayAsync("spotify:track:EEE", TimeSpan.Zero, 41);
        host.AutoAck = true;
        client.StopAsync().GetAwaiter().GetResult();
        host.AckRequest(1);
        var outcome = play.GetAwaiter().GetResult();

        Check(outcome == LibrespotPlayOutcome.SupersededByStop,
            "Zatrzymanie w trakcie przygotowania musi dać wynik SupersededByStop");
        Check(client.CurrentPlayId == 0, "Po zatrzymaniu nie ma bieżącej próby odtwarzania");
        var commands = host.WrittenCommands();
        // Stop musi trafic do hosta DWA razy: raz gdy uzytkownik go wcisnal (host
        // jeszcze nie mial utworu, wiec mogl go zgubic) i raz PO potwierdzeniu
        // polecenia graj. Sprawdzanie samego OSTATNIEGO polecenia nie wystarcza:
        // pierwszy stop juz nim jest, wiec test przechodzilby przy zgubionym
        // ponowieniu - i muzyka wlaczalaby sie po zatrzymaniu.
        Check(commands.Count(command => command["command"]!.GetValue<string>() == "stop") == 2,
            "Stop musi trafić do hosta PONOWNIE po potwierdzeniu grania, inaczej muzyka wystartuje");
        Check(commands[^1]["command"]!.GetValue<string>() == "stop",
            "Ostatnim poleceniem po przerwanym przygotowaniu musi być stop");

        // Spozniony stan hosta nie moze wskrzesic odtwarzania.
        var states = 0;
        client.StateChanged += (_, _) => states++;
        host.EmitState(41, "spotify:track:EEE", 0, 120_000, isPlaying: true);
        host.Settle();
        Check(states == 0, "Stan zatrzymanej próby nie może wrócić jako grające odtwarzanie");
        client.Dispose();
    }

    private static void TrackErrorClearsPreparationAndReportsCode()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        LibrespotTrackErrorEventArgs? failure = null;
        client.TrackFailed += (_, e) => failure = e;

        client.PlayAsync("spotify:track:FFF", TimeSpan.Zero, 50).GetAwaiter().GetResult();
        host.EmitTrackError(50, "spotify:track:FFF", "track_unavailable");
        host.Settle();

        Check(failure is not null, "Błąd utworu musi być zgłoszony, nie przemilczany");
        Check(failure!.Code == "track_unavailable", "Kod błędu utworu musi dojść bez zmian");
        Check(!client.IsPreparing, "Błąd utworu kończy przygotowanie");
        client.Dispose();
    }

    private static void DeviceListWorksWithoutAccount()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.DevicesResponse = new JsonArray
        {
            new JsonObject { ["name"] = "Wyjście domyślne", ["isDefault"] = true },
            new JsonObject { ["name"] = "Karta USB", ["isDefault"] = false }
        };
        var devices = client.GetDevicesAsync().GetAwaiter().GetResult();

        Check(devices.Count == 2, "Host musi zwrócić obie pozycje listy urządzeń");
        Check(devices[0] is { Name: "Wyjście domyślne", IsDefault: true },
            "Domyślne urządzenie musi być rozpoznane");
        Check(devices[1].Name == "Karta USB", "Nazwa urządzenia musi być DOKŁADNA");
        var commands = host.WrittenCommands();
        Check(commands.Single()["command"]!.GetValue<string>() == "devices",
            "Lista urządzeń nie wymaga konta: idzie samo polecenie devices");
        Check(commands.Single()["accessToken"] is null,
            "Polecenie devices nie może nieść tokenu");
        client.Dispose();
    }

    /// <summary>
    /// Transport MUSI odmowic zmiany wyjscia WPROST.
    ///
    /// Wczesniej wysylal tu drugie "initialize" z accessToken=null i polami
    /// keepUri/keepPlayId/keepPositionMs/keepPlaying. Host Rust tego NIE
    /// obsluguje: wymaga niepustego tokenu (badType) i odrzuca kazdy kolejny
    /// initialize (alreadyInitialized). Tamten test dowodzil wiec tylko tego, ze
    /// atrapa przyjmuje zmyslony kontrakt - i utrwalal blad.
    ///
    /// Realna zmiana wyjscia to ODTWORZENIE procesu; robi to adapter i mierzy
    /// SpotifyLibrespotLifecycleTests po stronie Windows.
    /// </summary>
    private static void DeviceChangeIsRefusedNotFaked()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        client.InitializeAsync(FikcyjnyToken, "Karta USB", 70).GetAwaiter().GetResult();
        client.PlayAsync("spotify:track:GGG", TimeSpan.Zero, 60).GetAwaiter().GetResult();
        host.EmitState(60, "spotify:track:GGG", 45_000, 200_000, isPlaying: true);
        host.Settle();

        var before = host.WrittenCommands().Count;
        var refusal = Throws(() =>
            client.SetOutputDeviceAsync("Inna karta").GetAwaiter().GetResult());

        Check(refusal?.Code == LibrespotHostErrorCodes.DeviceChangeNeedsNewHost,
            $"Zmiana wyjścia musi być odmówiona wprost; otrzymano: {refusal?.Code ?? "brak błędu"}");
        Check(host.WrittenCommands().Count == before,
            "Odmowa nie może wysłać do hosta ŻADNEGO polecenia, zwłaszcza drugiego initialize");
        Check(client.SelectedDeviceName == "Karta USB",
            "Po odmowie zostaje poprzednie wyjście, bez cichego zastępnika");
        Check(refusal!.Message.Contains("nowego procesu"),
            "Komunikat musi powiedzieć, czego zmiana wyjścia naprawdę wymaga");
        client.Dispose();
    }

    private static void VolumeSeekAndTimeReachTheHost()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        client.SetVolumeAsync(133).GetAwaiter().GetResult();
        client.SeekAsync(TimeSpan.FromSeconds(-5)).GetAwaiter().GetResult();
        client.SeekAsync(TimeSpan.FromSeconds(90)).GetAwaiter().GetResult();
        client.PlayAsync("spotify:track:HHH", TimeSpan.FromSeconds(30), 70)
            .GetAwaiter().GetResult();

        var commands = host.WrittenCommands();
        Check(commands[0]["volume"]!.GetValue<int>() == 100, "Głośność musi być ograniczona do 100");
        Check(commands[1]["positionMs"]!.GetValue<long>() == 0,
            "Ujemna pozycja przewijania musi być ścięta do zera");
        Check(commands[2]["positionMs"]!.GetValue<long>() == 90_000,
            "Przewijanie musi realnie przejść do hosta w milisekundach");
        Check(commands[3]["positionMs"]!.GetValue<long>() == 30_000,
            "Pozycja startowa utworu musi realnie przejść do hosta");
        Check(commands[3]["playId"]!.GetValue<long>() == 70, "playId nadaje AMC, nie host");
        Check(client.HostVolume == 100, "Klient musi pamiętać głośność przekazaną hostowi");

        var durations = new List<TimeSpan>();
        client.StateChanged += (_, e) => durations.Add(e.Duration);
        host.EmitState(70, "spotify:track:HHH", 31_000, 210_000, isPlaying: true);
        host.Settle();
        Check(durations.Single() == TimeSpan.FromMilliseconds(210_000),
            "Czas utworu z hosta musi dojść do warstwy wyżej");
        client.Dispose();
    }

    private static void TokenTravelsOnlyThroughStdinInitialize()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        client.InitializeAsync(FikcyjnyToken, "Karta USB", 55).GetAwaiter().GetResult();
        client.PlayAsync("spotify:track:III", TimeSpan.Zero, 80).GetAwaiter().GetResult();
        client.SetVolumeAsync(20).GetAwaiter().GetResult();

        var commands = host.WrittenCommands();
        var initialize = commands.Single(c => c["command"]!.GetValue<string>() == "initialize");
        Check(initialize["accessToken"]!.GetValue<string>() == FikcyjnyToken,
            "Token musi trafić do hosta w initialize przez stdin");
        Check(initialize["volume"]!.GetValue<int>() == 55, "Głośność startowa idzie w initialize");
        Check(commands.Count(c => c.ToJsonString().Contains(FikcyjnyToken)) == 1,
            "Token nie może pojawić się w żadnym innym poleceniu niż initialize");
        client.Dispose();
    }

    private static void TokenIsRejectedInProcessArguments()
    {
        var leak = Throws(() => LibrespotHostProcess.GuardAgainstSecret(
            "BQC4YyNlonge-token-konta-ktorego-nikt-nie-powinien-zobaczyc-w-argv"));
        Check(leak?.Code == LibrespotHostErrorCodes.TokenLeakGuard,
            "Token w argumentach procesu musi być odrzucony: argv widzi cały system");
        var named = Throws(() =>
            LibrespotHostProcess.GuardAgainstSecret("--access_token=cokolwiek"));
        Check(named?.Code == LibrespotHostErrorCodes.TokenLeakGuard,
            "Argument nazwany access_token musi być odrzucony");
        LibrespotHostProcess.GuardAgainstSecret("--protocol-version=1");
    }

    private static void CredentialProviderIsInjectedNotHardwired()
    {
        using var host = new FakeHost();
        var calls = 0;
        var client = new LibrespotHostClient(
            () => host,
            FastOptions,
            _ =>
            {
                calls++;
                return Task.FromResult(FikcyjnyToken);
            });
        host.EmitReady(LibrespotHostContract.ProtocolVersion);
        client.StartAsync().GetAwaiter().GetResult();
        host.AutoAck = true;
        client.InitializeAsync().GetAwaiter().GetResult();

        Check(calls == 1, "Token musi pochodzić ze wstrzykniętego dostawcy, nie z konta w teście");
        Check(host.WrittenCommands().Single()["accessToken"]!.GetValue<string>() == FikcyjnyToken,
            "Token ze wstrzykniętego dostawcy musi dojść do hosta");

        // Brak dostawcy i brak tokenu = jawny blad, nie ciche logowanie.
        using var drugi = new FakeHost();
        var bezDostawcy = NewClient(drugi);
        drugi.EmitReady(LibrespotHostContract.ProtocolVersion);
        bezDostawcy.StartAsync().GetAwaiter().GetResult();
        var missing = Throws(() => bezDostawcy.InitializeAsync().GetAwaiter().GetResult());
        Check(missing?.Code == LibrespotHostErrorCodes.ProtocolViolation,
            "Bez tokenu i bez dostawcy nie wolno cicho udawać logowania");
        client.Dispose();
        bezDostawcy.Dispose();
    }

    private static void ErrorWithoutRequestIdBecomesSessionFailure()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        var failures = new List<LibrespotHostFailureEventArgs>();
        client.HostFailed += (_, e) => failures.Add(e);
        host.EmitLine(new JsonObject
        {
            ["type"] = "error",
            ["code"] = "audio_device_lost",
            ["message"] = "Urządzenie wyjścia zniknęło."
        });
        host.Settle();

        Check(failures.Count == 1, "Błąd bez requestId musi być zgłoszony jako awaria sesji");
        Check(failures[0].Code == "audio_device_lost", "Kod awarii sesji musi dojść bez zmian");
        client.Dispose();
    }

    private static void GarbageLineIsProtocolFailureNotSilence()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        var failures = new List<LibrespotHostFailureEventArgs>();
        client.HostFailed += (_, e) => failures.Add(e);
        host.AutoAck = false;
        var pending = client.PingAsync();
        host.EmitRaw("host wypisal zwykly tekst zamiast JSON");

        var error = Throws(() => pending.GetAwaiter().GetResult());
        Check(error?.Code == LibrespotHostErrorCodes.ProtocolViolation,
            "Linia inna niż JSON musi zakończyć oczekujące polecenie błędem protokołu");
        Check(failures.Any(f => f.Code == LibrespotHostErrorCodes.ProtocolViolation),
            "Naruszenie protokołu musi być zgłoszone, nie zignorowane");
        client.Dispose();
    }

    private static void SerializedWritesProduceWholeJsonLines()
    {
        using var host = new FakeHost();
        var client = StartedClient(host);
        host.AutoAck = true;
        var tasks = Enumerable.Range(0, 24)
            .Select(index => client.SetVolumeAsync(index % 101))
            .ToArray();
        Task.WaitAll(tasks, TimeSpan.FromSeconds(10));

        var commands = host.WrittenCommands();
        Check(commands.Count == 24, "Każdy równoległy zapis musi dać jedną CAŁĄ linię JSON");
        Check(commands.Select(c => c["requestId"]!.GetValue<long>()).Distinct().Count() == 24,
            "Równoległe polecenia muszą mieć różne requestId");
        Check(!host.RawInput().Contains("}{", StringComparison.Ordinal),
            "Zapisy muszą być serializowane: dwie sklejone linie to błąd protokołu");
        client.Dispose();
    }

    // ---------- Testy na PRAWDZIWYM procesie ----------

    private static void RealProcessHandshakeAndCommands()
    {
        var log = Path.Combine(Path.GetTempPath(), $"amc-librespot-{Guid.NewGuid():N}.log");
        Environment.SetEnvironmentVariable(LibrespotHostFixture.CommandLogVariable, log);
        try
        {
            var client = new LibrespotHostClient(
                () => LibrespotHostFixture.Start(LibrespotHostFixture.ModeNormal),
                new LibrespotHostOptions
                {
                    ReadyTimeout = TimeSpan.FromSeconds(30),
                    RequestTimeout = TimeSpan.FromSeconds(20)
                },
                _ => Task.FromResult(FikcyjnyToken));
            try
            {
                client.StartAsync().GetAwaiter().GetResult();
                Check(client.NegotiatedProtocolVersion == 1,
                    "Prawdziwy proces musi uzgodnić wersję protokołu 1");
                Check(client.IsHostRunning, "Proces hosta musi działać po uzgodnieniu");

                client.PingAsync().GetAwaiter().GetResult();
                var devices = client.GetDevicesAsync().GetAwaiter().GetResult();
                Check(devices.Count == 2 && devices[0].IsDefault,
                    "Prawdziwy proces musi zwrócić listę urządzeń bez konta");

                client.InitializeAsync(null, devices[1].Name, 64).GetAwaiter().GetResult();

                var started = new List<LibrespotStateEventArgs>();
                using var gotState = new ManualResetEventSlim(false);
                client.StateChanged += (_, e) =>
                {
                    started.Add(e);
                    gotState.Set();
                };
                var outcome = client.PlayAsync("spotify:track:REAL", TimeSpan.FromSeconds(12), 900)
                    .GetAwaiter().GetResult();
                Check(outcome == LibrespotPlayOutcome.Accepted,
                    "Prawdziwy host musi przyjąć polecenie graj");
                Check(gotState.Wait(TimeSpan.FromSeconds(15)),
                    "Prawdziwy host musi przysłać stan odtwarzania");
                Check(started[0].PlayId == 900 && started[0].IsPlaying,
                    "Stan z prawdziwego procesu musi nieść nasze playId");
                Check(started[0].Duration == TimeSpan.FromMilliseconds(180_000),
                    "Czas utworu z prawdziwego procesu musi dojść do warstwy wyżej");
                Check(!client.IsPreparing,
                    "Potwierdzony stan grania kończy przygotowanie");

                client.SeekAsync(TimeSpan.FromSeconds(75)).GetAwaiter().GetResult();
                client.SetVolumeAsync(31).GetAwaiter().GetResult();
                client.PauseAsync().GetAwaiter().GetResult();
                client.ResumeAsync().GetAwaiter().GetResult();
                client.StopAsync().GetAwaiter().GetResult();
                client.ShutdownAsync().GetAwaiter().GetResult();

                var received = File.Exists(log)
                    ? File.ReadAllLines(log)
                        .Where(line => line.Length > 0)
                        .Select(line => JsonNode.Parse(line) as JsonObject ?? new JsonObject())
                        .ToList()
                    : [];
                var names = received
                    .Select(command => command["command"]?.GetValue<string>() ?? string.Empty)
                    .ToList();
                Check(names.SequenceEqual(new[]
                    {
                        "ping", "devices", "initialize", "play",
                        "seek", "volume", "pause", "resume", "stop", "shutdown"
                    }),
                    $"Prawdziwy proces musi odebrać wszystkie polecenia w kolejności; odebrano: {string.Join(", ", names)}");
                var seek = received.Single(c => c["command"]!.GetValue<string>() == "seek");
                Check(seek["positionMs"]!.GetValue<long>() == 75_000,
                    "Przewijanie musi realnie dojść do prawdziwego procesu");
                var volume = received.Single(c => c["command"]!.GetValue<string>() == "volume");
                Check(volume["volume"]!.GetValue<int>() == 31,
                    "Głośność musi realnie dojść do prawdziwego procesu");
                var initialize = received.Single(c => c["command"]!.GetValue<string>() == "initialize");
                Check(initialize["device"]!.GetValue<string>() == devices[1].Name,
                    "Wybór urządzenia musi przekazać dokładną nazwę z listy hosta");
                Check(!File.ReadAllText(log).Contains("FIKCYJNY-TOKEN") == false,
                    "Token idzie stdin-em, więc dziennik atrapy go widzi - to potwierdza kanał");
                Check(received.Count(c => c.ToJsonString().Contains(FikcyjnyToken)) == 1,
                    "Token przeszedł DOKŁADNIE raz, w initialize");
            }
            finally
            {
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(LibrespotHostFixture.CommandLogVariable, null);
            try { if (File.Exists(log)) File.Delete(log); } catch (IOException) { }
        }
    }

    private static void RealProcessTimeoutIsReported()
    {
        var client = new LibrespotHostClient(
            () => LibrespotHostFixture.Start(LibrespotHostFixture.ModeSilent),
            new LibrespotHostOptions
            {
                ReadyTimeout = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromMilliseconds(600)
            });
        try
        {
            client.StartAsync().GetAwaiter().GetResult();
            var error = Throws(() => client.PingAsync().GetAwaiter().GetResult());
            Check(error?.Code == LibrespotHostErrorCodes.Timeout,
                "Milczący prawdziwy host musi dać błąd przekroczenia czasu, nie ciszę");
            Check(error!.Message.Contains("ping"),
                "Komunikat musi nazwać polecenie, które nie dostało odpowiedzi");
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Pad hosta przy POLECENIU W LOCIE. Limit czasu jest tu ustawiony wysoko
    /// (25 s) i test mierzy czas: tylko wtedy dowodzi, ze polecenie zakonczyl
    /// KONIEC STRUMIENIA, a nie zwykle przekroczenie czasu. Gdyby transport
    /// polegal na limicie, uzytkownik czekalby w ciszy caly ten czas.
    ///
    /// Host tu najpierw potwierdza odbior, chwile zyje (zeby zapis do stdin sie
    /// UDAL), i dopiero wtedy konczy sie bez odpowiedzi. Bez tego opoznienia
    /// zapis padalby na przerwanym potoku i test przechodzilby inna droga niz
    /// mierzona.
    /// </summary>
    private static void RealProcessExitFailsPendingRequests()
    {
        Environment.SetEnvironmentVariable(LibrespotHostFixture.ExitDelayVariable, "400");
        var client = new LibrespotHostClient(
            () => LibrespotHostFixture.Start(LibrespotHostFixture.ModeExitAfterFirst),
            new LibrespotHostOptions
            {
                ReadyTimeout = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromSeconds(25)
            });
        try
        {
            client.StartAsync().GetAwaiter().GetResult();
            var failures = new List<LibrespotHostFailureEventArgs>();
            client.HostFailed += (_, e) => failures.Add(e);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var error = Throws(() => client.PingAsync().GetAwaiter().GetResult());
            stopwatch.Stop();
            Check(error?.Code == LibrespotHostErrorCodes.HostExited,
                $"Koniec procesu musi dać błąd o padniętym hoście; otrzymano: {error?.Code ?? "brak błędu"}");
            Check(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Pad procesu musi zakończyć polecenie OD RAZU, nie po limicie czasu; zmierzono {stopwatch.Elapsed.TotalSeconds:0.0} s");
            Check(failures.Any(f => f.Code == LibrespotHostErrorCodes.HostExited),
                "Awaria procesu musi być zgłoszona jako awaria sesji");
            Check(error!.Message.Length > 0, "Komunikat o padniętym hoście nie może być pusty");

            // Kolejne polecenie tez nie moze czekac w ciszy.
            var next = Throws(() => client.SetVolumeAsync(10).GetAwaiter().GetResult());
            Check(next?.Code == LibrespotHostErrorCodes.HostExited,
                "Po padzie procesu każde następne polecenie musi od razu zgłosić błąd");
        }
        finally
        {
            Environment.SetEnvironmentVariable(LibrespotHostFixture.ExitDelayVariable, null);
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void RealProcessBadVersionFails()
    {
        var client = new LibrespotHostClient(
            () => LibrespotHostFixture.Start(LibrespotHostFixture.ModeBadVersion),
            new LibrespotHostOptions { ReadyTimeout = TimeSpan.FromSeconds(30) });
        try
        {
            var error = Throws(() => client.StartAsync().GetAwaiter().GetResult());
            Check(error?.Code == LibrespotHostErrorCodes.ProtocolVersionMismatch,
                "Prawdziwy host ze złą wersją protokołu musi dać czytelny błąd wersji");
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void RealProcessLongLineIsBounded()
    {
        var client = new LibrespotHostClient(
            () => LibrespotHostFixture.Start(LibrespotHostFixture.ModeLongLine),
            new LibrespotHostOptions
            {
                ReadyTimeout = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromSeconds(10),
                MaxLineLength = 4096
            });
        try
        {
            client.StartAsync().GetAwaiter().GetResult();
            using var failed = new ManualResetEventSlim(false);
            string? code = null;
            client.HostFailed += (_, e) =>
            {
                code = e.Code;
                failed.Set();
            };
            Check(failed.Wait(TimeSpan.FromSeconds(15)),
                "Zbyt długa linia musi zgłosić awarię, a nie rosnąć w pamięci");
            Check(code == LibrespotHostErrorCodes.LineTooLong,
                "Awaria musi mieć kod zbyt długiej linii");
        }
        finally
        {
            client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void RealProcessCleanupKillsHost()
    {
        var host = LibrespotHostFixture.Start(LibrespotHostFixture.ModeSilent);
        var client = new LibrespotHostClient(
            () => host,
            new LibrespotHostOptions
            {
                ReadyTimeout = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromMilliseconds(400),
                ShutdownTimeout = TimeSpan.FromMilliseconds(400)
            });
        client.StartAsync().GetAwaiter().GetResult();
        Check(!host.HasExited, "Proces hosta musi żyć przed sprzątaniem");
        client.DisposeAsync().AsTask().GetAwaiter().GetResult();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!host.HasExited && DateTime.UtcNow < deadline) Thread.Sleep(50);
        Check(host.HasExited, "Sprzątanie musi zamknąć proces hosta, żeby nie został sierotą");

        var afterDispose = Throws(() => client.PingAsync().GetAwaiter().GetResult());
        Check(afterDispose?.Code == LibrespotHostErrorCodes.Disposed,
            "Po zamknięciu sesji polecenie musi dać jasny błąd, nie ciszę");
        host.Dispose();
    }

    // ---------- Narzedzia ----------

    private static LibrespotHostOptions FastOptions => new()
    {
        ReadyTimeout = TimeSpan.FromSeconds(5),
        RequestTimeout = TimeSpan.FromSeconds(5),
        ShutdownTimeout = TimeSpan.FromMilliseconds(200)
    };

    private static LibrespotHostClient NewClient(FakeHost host) =>
        new(() => host, FastOptions);

    private static LibrespotHostClient StartedClient(FakeHost host)
    {
        var client = NewClient(host);
        host.EmitReady(LibrespotHostContract.ProtocolVersion);
        client.StartAsync().GetAwaiter().GetResult();
        host.Attach(client);
        return client;
    }

    private static LibrespotHostException? Throws(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (LibrespotHostException exception)
        {
            return exception;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Atrapa strumieni hosta: pozwala sterowac kolejnoscia zdarzen z dokladnoscia
    /// do jednej linii, czego prawdziwy proces nie da (wyscigi czasowe).
    /// </summary>
    private sealed class FakeHost : ILibrespotHostProcess
    {
        private readonly StringBuilder input = new();
        private readonly object gate = new();
        private readonly LineChannel channel = new();
        private LibrespotHostClient? client;
        private long lastRequestId;

        public bool AutoAck { get; set; }
        public JsonArray? DevicesResponse { get; set; }
        public (string Code, string Message)? AutoError { get; set; }

        public TextWriter StandardInput => new HostWriter(this);
        public TextReader StandardOutput => channel.Reader;
        public bool HasExited { get; private set; }
        public int? ExitCode => HasExited ? 0 : null;

        public void Attach(LibrespotHostClient value) => client = value;

        public void Kill() => HasExited = true;

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            HasExited = true;
            return Task.CompletedTask;
        }

        public void Dispose() => HasExited = true;

        public void EmitReady(int protocolVersion) => EmitLine(new JsonObject
        {
            ["type"] = "ready",
            ["protocolVersion"] = protocolVersion
        });

        public void EmitState(
            long playId, string uri, long positionMs, long durationMs, bool isPlaying) =>
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

        public void AckRequest(long requestId) => EmitLine(new JsonObject
        {
            ["type"] = "ack",
            ["requestId"] = requestId
        });

        public void EmitLine(JsonObject payload) => channel.Write(payload.ToJsonString());

        public void EmitRaw(string text) => channel.Write(text);

        /// <summary>
        /// Bariera oparta o PRAWDZIWE polecenie protokolu, tak samo jak w tescie
        /// adaptera WPF. Wysylamy „ping” i czekamy na jego potwierdzenie.
        ///
        /// Poprzednia wersja wstrzykiwala sztuczne „ready” w srodku sesji i
        /// czekala, az czytelnik POBIERZE te linie z kolejki. To bylo bledne z
        /// dwoch powodow: pobranie linii nie jest jeszcze jej obsluzeniem (licznik
        /// rosnie w chwili zdjecia z kolejki, a zdarzenie leci dopiero po
        /// skopiowaniu i sparsowaniu znakow), a powitanie w srodku sesji zasmieca
        /// protokol. Testy wychodzily raz zielone, raz czerwone.
        ///
        /// Ping jest dowodem MOCNIEJSZYM: jego potwierdzenie wraca kolejka PO
        /// wszystkim, co host wyslal wczesniej, a transport konczy zadanie dopiero
        /// gdy OBSLUZY linie z ackiem - wiec obsluzyl tez wszystko przed nim.
        ///
        /// Gdy transport wlasnie padl (test awarii hosta), ping rzuca wyjatkiem.
        /// To tez dowodzi, ze linia bledu zostala obsluzona, wiec milczymy.
        /// </summary>
        public void Settle()
        {
            var current = client
                ?? throw new InvalidOperationException("Brak klienta w atrapie hosta.");
            var wasAcking = AutoAck;
            AutoAck = true;
            try
            {
                if (!current.PingAsync().Wait(TimeSpan.FromSeconds(10)))
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
            finally
            {
                AutoAck = wasAcking;
            }
        }

        public string RawInput()
        {
            lock (gate) return input.ToString();
        }

        public List<JsonObject> WrittenCommands()
        {
            foreach (var _ in Enumerable.Range(0, 50))
            {
                if (TryParseAll(out var parsed)) return parsed;
                Thread.Sleep(10);
            }
            TryParseAll(out var last);
            return last;
        }

        private bool TryParseAll(out List<JsonObject> commands)
        {
            commands = [];
            string text;
            lock (gate) text = input.ToString();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (JsonNode.Parse(trimmed) is not JsonObject command) return false;
                commands.Add(command);
            }
            return true;
        }

        private void OnLineWritten(string line)
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
            var requestId = request["requestId"] is JsonValue value
                && value.TryGetValue(out long identifier) ? identifier : 0L;
            Interlocked.Exchange(ref lastRequestId, requestId);
            var command = request["command"]?.GetValue<string>() ?? string.Empty;

            if (AutoError is { } error)
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
            if (command == LibrespotHostContract.CommandDevices)
            {
                EmitLine(new JsonObject
                {
                    ["type"] = "devices",
                    ["requestId"] = requestId,
                    ["devices"] = DevicesResponse?.DeepClone() ?? new JsonArray
                    {
                        new JsonObject { ["name"] = "Domyślne", ["isDefault"] = true }
                    }
                });
                return;
            }
            if (AutoAck) AckRequest(requestId);
        }

        /// <summary>Zapis do stdin atrapy; pelne linie od razu obsluguje host.</summary>
        private sealed class HostWriter(FakeHost host) : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                string? complete = null;
                lock (host.gate)
                {
                    host.input.Append(value);
                    if (value == '\n')
                    {
                        var text = host.input.ToString();
                        var lines = text.Split('\n');
                        complete = lines.Length >= 2 ? lines[^2] : null;
                    }
                }
                if (complete is not null) host.OnLineWritten(complete);
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

        /// <summary>Kanal linii udajacy stdout hosta: czytelnik nie konczy sie sam.</summary>
        private sealed class LineChannel
        {
            private readonly SemaphoreSlim available = new(0);
            private readonly Queue<string> lines = new();
            private readonly object queueGate = new();
            private long written;
            private long dequeued;

            public TextReader Reader { get; }

            /// <summary>Ile linii czytelnik POBRAL (czyli obsluzyl poprzednie).</summary>
            public long DequeuedCount => Interlocked.Read(ref dequeued);

            public LineChannel()
            {
                Reader = new ChannelReader(this);
            }

            /// <summary>Zwraca numer porzadkowy wstawionej linii.</summary>
            public long Write(string line)
            {
                long ordinal;
                lock (queueGate)
                {
                    lines.Enqueue(line + "\n");
                    ordinal = ++written;
                }
                available.Release();
                return ordinal;
            }

            private sealed class ChannelReader(LineChannel channel) : TextReader
            {
                private string buffer = string.Empty;
                private int offset;

                public override async Task<int> ReadAsync(char[] destination, int index, int count)
                {
                    while (offset >= buffer.Length)
                    {
                        await channel.available.WaitAsync().ConfigureAwait(false);
                        lock (channel.queueGate) buffer = channel.lines.Dequeue();
                        Interlocked.Increment(ref channel.dequeued);
                        offset = 0;
                    }
                    var copied = Math.Min(count, buffer.Length - offset);
                    buffer.CopyTo(offset, destination, index, copied);
                    offset += copied;
                    return copied;
                }

                public override int Read(char[] destination, int index, int count) =>
                    ReadAsync(destination, index, count).GetAwaiter().GetResult();
            }
        }
    }
}
