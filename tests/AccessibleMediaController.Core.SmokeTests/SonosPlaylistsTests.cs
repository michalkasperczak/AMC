using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODCZYT PLAYLIST SONOSA i ich URUCHOMIENIE w grupie - warstwa Core. Tylko
/// Core: zero GUI, zero presetow, zero prawdziwego konta, zero sieci i zero
/// muzyki. Dane syntetyczne, atrapa bramki logowania, atrapa magazynu w pamieci
/// i PRAWDZIWY <see cref="SonosControlApiClient"/> na sztucznym
/// HttpMessageHandler. Wywolania ida PRZEZ publiczne
/// <see cref="ISonosPlaylistsApi"/> i <see cref="ISonosPlaylistLoadApi"/> do
/// produkcyjnego klienta, wiec mierzony jest caly tor
/// koordynator -> klient -> HTTP.
///
/// Zakres jest CELOWO WASKI i nie powiela odebranych juz suit ulubionych:
/// macierz zabezpieczen HTTP i OAuth (przekierowania, limit tresci, kodowanie,
/// naglowki, 401/403/404/429/5xx, odnawianie, kasowanie konta) zostala zmierzona
/// przy F1/F3 na TYM SAMYM wspolnym transporcie i TYCH SAMYCH wspolnych torach
/// koordynatora. Tutaj mierzymy to, co dla playlist jest NOWE albo INNE:
///
///   * kolekcja poprawna, PUSTA i NULLABLE - w playlistsList zarowno version,
///     jak i playlists sa opcjonalne i nullable, inaczej niz w ulubionych; brak
///     pola i null to NORMALNY sukces z pusta lista, a nie blad,
///   * dwie playlisty o TAKIEJ SAMEJ nazwie i roznych identyfikatorach to DWIE
///     pozycje - tytul nie jest kluczem; powtorzony IDENTYFIKATOR odrzuca calosc,
///   * typowa nieprawidlowa odpowiedz odrzuca CALA liste, nie jej czesc,
///   * uruchomienie: DOKLADNIE JEDEN POST na /groups/{groupId}/playlists z
///     literalna grupa, literalnym playlistId, INSERT i playOnCompletion=true,
///     bez playModes,
///   * wadliwe zadanie: ZERO POST,
///   * cienkie podlaczenie koordynatora: sygnatury bez wartosci domyslnych,
///     null api to blad wolajacego, brak konta to zero zapytan,
///   * odmowa po ZMIANIE KONTA przez te sama wspolna granice generacji co F3.
/// </summary>
internal static class SonosPlaylistsTests
{
    private const string Key = "00000000-0000-0000-0000-000000000042";
    private const string Household = "Sonos_synthetic.household";
    private const string Group = "RINCON_0001:1";
    private const string PlaylistId = "SYNTHETIC-PL-42";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public static void Run()
    {
        var tests = new List<(string Name, Action Test)>
        {
            ("odczyt: poprawna kolekcja wchodzi doslownie", ReadsCollectionLiterally),
            ("odczyt: pusta i nullowa kolekcja to sukces, nie blad", EmptyAndNullCollectionsSucceed),
            ("odczyt: dwa identyczne tytuly o roznych ID to dwie pozycje", DuplicateTitlesAreTwoEntries),
            ("odczyt: nieprawidlowa odpowiedz odrzuca cala liste", InvalidResponseRejectsWholeList),
            ("uruchomienie: dokladnie jeden POST z INSERT i true", LoadSendsExactlyOnePost),
            ("uruchomienie: wadliwe zadanie nie wysyla POST", InvalidRequestNeverPosts),
            ("koordynator: cienkie podlaczenie odczytu i uruchomienia", CoordinatorExposesBothOperations),
            ("koordynator: brak konta to zero zapytan", NoAccountSendsNothing),
            ("koordynator: zmiana konta w trakcie odmawia wyniku", AccountChangeRefusesResult),
            ("komunikaty i ToString bez tokenow i tresci", MessagesHideSecrets)
        };

        var failures = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("[OK] " + name); }
            catch (Exception ex) { failures++; Console.WriteLine("[FAIL] " + name + " (" + ex.GetType().Name + ": " + ex.Message + ")"); }
        }

        Console.WriteLine($"Playlisty Sonos w Core: {tests.Count - failures}/{tests.Count}");
        if (failures != 0)
        {
            throw new InvalidOperationException("Playlisty Sonos w Core: " + failures + " nieudanych testow.");
        }
    }

    // ================= odczyt =================

    private static void ReadsCollectionLiterally()
    {
        var seen = new List<string?>();
        using var fixture = Fixture.Connected(request =>
        {
            seen.Add(request.RequestUri?.AbsolutePath);
            return Json("""
                {
                  "version": "v-7",
                  "playlists": [
                    { "id": "PL:1", "name": "  Poranek  ", "type": "playlist", "trackCount": 12 },
                    { "id": "PL:2", "name": "Wieczór" }
                  ]
                }
                """);
        });

        var result = fixture.Read();

        Check(result.Succeeded, "sukces odczytu");
        Check(fixture.Requests == 1, "dokladnie jedno zapytanie");
        Check(seen.Count == 1 && seen[0]!.EndsWith("/households/" + Household + "/playlists", StringComparison.Ordinal),
            "sciezka domu literalna");

        var list = result.Playlists!;
        // householdId pochodzi z ZAPYTANIA, literalnie.
        Check(list.HouseholdId == Household, "dom z zapytania");
        Check(list.Version == "v-7", "wersja przeniesiona");
        Check(list.Items.Count == 2, "dwie pozycje");

        // Zadnego trim: wartosci zrodlowe zostaja DOKLADNIE takie, jakie przyszly.
        Check(list.Items[0].Id == "PL:1" && list.Items[0].Name == "  Poranek  ", "nazwa bez trim");
        Check(list.Items[0].Type == "playlist" && list.Items[0].TrackCount == 12, "type i trackCount");

        // Brak type i trackCount daje null - "nie podano", a NIE zero.
        Check(list.Items[1].Type is null && list.Items[1].TrackCount is null, "brak pol to null, nie zero");
    }

    private static void EmptyAndNullCollectionsSucceed()
    {
        // Trzy ksztalty, ktore dla PLAYLIST sa legalnym sukcesem z pusta lista -
        // inaczej niz dla ulubionych, gdzie brak items jest bledem.
        var bodies = new[]
        {
            """{ "version": "v-1", "playlists": [] }""",
            """{ "version": "v-1" }""",
            """{ "version": null, "playlists": null }""",
            "{}"
        };

        foreach (var body in bodies)
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.Read();

            Check(result.Succeeded, "pusta kolekcja to sukces");
            Check(result.Playlists!.Items.Count == 0, "zero pozycji");
            Check(result.Status == SonosDeviceReadStatus.Success, "status Success");
        }

        // Nieznana wersja NIE jest bledem i NIE jest pustym napisem.
        using var noVersion = Fixture.Connected(_ => Json("""{ "playlists": [] }"""));
        Check(noVersion.Read().Playlists!.Version is null, "brak wersji to null");
    }

    private static void DuplicateTitlesAreTwoEntries()
    {
        using var fixture = Fixture.Connected(_ => Json("""
            {
              "playlists": [
                { "id": "PL:A", "name": "Ta sama nazwa" },
                { "id": "PL:B", "name": "Ta sama nazwa" }
              ]
            }
            """));

        var list = fixture.Read().Playlists!;

        // TYTUL NIE JEST KLUCZEM: dwie playlisty o tej samej nazwie to dwie
        // pozycje i obie musza dac sie wskazac osobno.
        Check(list.Items.Count == 2, "dwie pozycje mimo jednej nazwy");
        Check(list.Items[0].Id == "PL:A" && list.Items[1].Id == "PL:B", "oba identyfikatory zachowane");
        Check(list.Items[0].Name == list.Items[1].Name, "nazwy faktycznie identyczne");
    }

    private static void InvalidResponseRejectsWholeList()
    {
        var broken = new (string Label, string Body)[]
        {
            ("brak wymaganego id", """{ "playlists": [ { "name": "Bez id" } ] }"""),
            ("puste id", """{ "playlists": [ { "id": "", "name": "Puste id" } ] }"""),
            ("brak wymaganej nazwy", """{ "playlists": [ { "id": "PL:1" } ] }"""),
            ("id nie jest tekstem", """{ "playlists": [ { "id": 7, "name": "Liczba" } ] }"""),
            ("playlists nie jest tablica", """{ "playlists": { "id": "PL:1" } }"""),
            ("trackCount nie jest liczba", """{ "playlists": [ { "id": "PL:1", "name": "A", "trackCount": "12" } ] }"""),
            ("powtorzony identyfikator", """{ "playlists": [ { "id": "PL:1", "name": "A" }, { "id": "PL:1", "name": "B" } ] }"""),
            ("powtorzone pole JSON", """{ "playlists": [], "playlists": [] }""")
        };

        foreach (var (label, body) in broken)
        {
            using var fixture = Fixture.Connected(_ => Json(body));
            var result = fixture.Read();

            // CALA lista odpada - AMC nie pokazuje polowy playlist jako calosci.
            Check(!result.Succeeded, label + ": brak sukcesu");
            Check(result.Playlists is null, label + ": zero danych");
            Check(result.Status == SonosDeviceReadStatus.InvalidResponse, label + ": status InvalidResponse");
        }

        // Pozycja poprawna OBOK wadliwej tez nie przechodzi - nie ma wyniku czesciowego.
        using var mixed = Fixture.Connected(_ => Json(
            """{ "playlists": [ { "id": "PL:1", "name": "Dobra" }, { "name": "Zla" } ] }"""));
        Check(mixed.Read().Playlists is null, "brak wyniku czesciowego");
    }

    // ================= uruchomienie =================

    private static void LoadSendsExactlyOnePost()
    {
        var seen = new List<Sent>();
        using var fixture = Fixture.Connected(request => { seen.Add(Sent.From(request)); return Json("{}"); });

        // Jawne INSERT i true - tak jak przy ulubionych; zadnego REPLACE,
        // zadnego playModes, zadnego dodatkowego Play.
        var result = fixture.Load(action: SonosFavoriteQueueAction.Insert, playOnCompletion: true);

        Check(result.Status == SonosGroupOperationStatus.Attempted, "status Attempted");
        Check(result.RequestSent && result.Accepted, "wyslane i przyjete");
        Check(result.Command == SonosGroupCommand.LoadPlaylist, "polecenie LoadPlaylist");
        // PRZYJECIE to nie dowod, ze muzyka zagrala.
        Check(!result.EffectConfirmed, "przyjecie nie jest potwierdzonym skutkiem");

        Check(fixture.Requests == 1 && seen.Count == 1, "dokladnie jeden POST");
        Check(seen[0].Method == "POST" && seen[0].Token == Fixture.Access, "metoda i bilet");
        // Dwukropek jest legalnym znakiem segmentu sciezki i zostaje LITERALNIE.
        Check(seen[0].Path!.EndsWith("/groups/" + Group + "/playlists", StringComparison.Ordinal),
            "sciezka grupy literalna i zasob playlists");

        // Pole nazywa sie playlistId, NIE favoriteId.
        Check(seen[0].Playlist == PlaylistId, "playlistId literalnie");
        Check(seen[0].Favorite is null, "zadnego favoriteId w ciele");
        Check(seen[0].Action == "INSERT", "akcja INSERT");
        Check(seen[0].PlayOnCompletion == true, "playOnCompletion true");
        // Pominiecie playModes ZACHOWUJE tryby gloshnika; jawne false by je wylaczylo.
        Check(!seen[0].HasPlayModes, "zadnego playModes");
    }

    private static void InvalidRequestNeverPosts()
    {
        var bad = new (string Label, string? GroupId, string? Playlist, SonosFavoriteQueueAction Action)[]
        {
            ("brak playlistId", Group, null, SonosFavoriteQueueAction.Insert),
            ("pusty playlistId", Group, "", SonosFavoriteQueueAction.Insert),
            ("za dlugi playlistId", Group, new string('x', SonosPlaylistsLimits.MaxPlaylistIdLength + 1),
                SonosFavoriteQueueAction.Insert),
            ("samotny surogat w playlistId", Group, "PL\ud800", SonosFavoriteQueueAction.Insert),
            ("brak grupy", null, PlaylistId, SonosFavoriteQueueAction.Insert),
            ("pusta grupa", "", PlaylistId, SonosFavoriteQueueAction.Insert),
            ("akcja spoza enuma", Group, PlaylistId, (SonosFavoriteQueueAction)77)
        };

        foreach (var (label, groupId, playlist, action) in bad)
        {
            using var fixture = Fixture.Connected(_ => Json("{}"));
            var result = fixture.Load(groupId: groupId, playlistId: playlist, action: action);

            Check(fixture.Requests == 0, label + ": ZERO POST");
            Check(!result.RequestSent && !result.Accepted && !result.EffectConfirmed, label + ": nic nie wyszlo");
            Check(result.Command == SonosGroupCommand.LoadPlaylist, label + ": polecenie zachowane");
        }

        // Identyfikator ze samych spacji jest PRZYJMOWANY przez odczyt, wiec
        // zapis tez musi umiec go odeslac - zadnego trim po drodze.
        var seen = new List<Sent>();
        using var spaces = Fixture.Connected(request => { seen.Add(Sent.From(request)); return Json("{}"); });
        spaces.Load(playlistId: "   ");
        Check(spaces.Requests == 1 && seen[0].Playlist == "   ", "identyfikator ze spacji idzie doslownie");
    }

    // ================= koordynator =================

    private static void CoordinatorExposesBothOperations()
    {
        var read = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "ReadPlaylistsAsync"
                && candidate.ReturnType == typeof(Task<SonosPlaylistsReadResult>)
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[] { typeof(ISonosPlaylistsApi), typeof(string), typeof(CancellationToken) }));

        var load = typeof(SonosAccountCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(candidate =>
                candidate.Name == "LoadPlaylistAsync"
                && candidate.ReturnType == typeof(Task<SonosGroupCommandResult>)
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    new[]
                    {
                        typeof(ISonosPlaylistLoadApi), typeof(string), typeof(string),
                        typeof(SonosFavoriteQueueAction), typeof(bool), typeof(CancellationToken)
                    }));

        Check(read is not null, "koordynator wystawia odczyt playlist");
        Check(load is not null, "koordynator wystawia uruchomienie playlisty");
        // Akcja i playOnCompletion sa JAWNE: zadna z nich nie ma wartosci domyslnej.
        Check(load!.GetParameters().All(p => !p.HasDefaultValue), "zadnych wartosci domyslnych");

        // PRAWDZIWY klient JUZ implementuje oba waskie kontrakty - zaden adapter
        // ani drugi HttpClient nie jest potrzebny.
        Check(typeof(ISonosPlaylistsApi).IsAssignableFrom(typeof(SonosControlApiClient)), "klient to ISonosPlaylistsApi");
        Check(typeof(ISonosPlaylistLoadApi).IsAssignableFrom(typeof(SonosControlApiClient)), "klient to ISonosPlaylistLoadApi");

        // Null api jest bledem WOLAJACEGO, nie cichym brakiem konta.
        using var fixture = Fixture.Connected(_ => Json("{}"));
        var threwRead = false;
        var threwLoad = false;
        try
        {
            fixture.Coordinator.ReadPlaylistsAsync(null!, Household, CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threwRead = true; }

        try
        {
            fixture.Coordinator
                .LoadPlaylistAsync(null!, Group, PlaylistId, SonosFavoriteQueueAction.Insert, true, CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
        }
        catch (ArgumentNullException) { threwLoad = true; }

        Check(threwRead && threwLoad && fixture.Requests == 0, "null api: wyjatek i zero zapytan");
    }

    private static void NoAccountSendsNothing()
    {
        using var fixture = Fixture.WithoutAccount(_ => Json("{}"));

        var read = fixture.Read();
        Check(read.Status == SonosDeviceReadStatus.NoAccount, "odczyt bez konta");
        Check(read.Playlists is null && !read.Succeeded, "odczyt bez konta nie niesie danych");

        var load = fixture.Load();
        Check(load.Status == SonosGroupOperationStatus.NoAccount, "uruchomienie bez konta");
        Check(!load.RequestSent && !load.Accepted && !load.EffectConfirmed, "nic nie wyszlo");

        Check(fixture.Requests == 0 && fixture.Refreshes == 0 && fixture.Deletes == 0,
            "zero zapytan, zero odnowien, zero kasowania");
    }

    private static void AccountChangeRefusesResult()
    {
        // Ta sama WSPOLNA granica generacji, ktora zostala odebrana przy F3:
        // nowe logowanie w trakcie trwajacego zapytania porzuca stary wynik,
        // zeby stare playlisty nie pojawily sie pod nowa tozsamoscia.
        var gate = new Gate();
        using var fixture = Fixture.Connected(_ =>
        {
            gate.EnterAndWait();
            return Json("""{ "playlists": [ { "id": "PL:STARE", "name": "Stare konto" } ] }""");
        });

        // Task.Run jak w odebranej suicie F3: atrapa transportu czeka
        // SYNCHRONICZNIE, wiec wywolanie na watku testu zablokowaloby sie PRZED
        // oddaniem Taska i nikt nie zwolnilby bramki.
        var pending = Task.Run(() => fixture.Read());
        Check(gate.WaitEntered(), "zapytanie weszlo w transport");

        try
        {
            fixture.LogInAsNewAccount();
        }
        finally
        {
            gate.Release();
        }

        var result = pending.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        Check(result.Discarded, "spozniony wynik porzucony");
        Check(result.Status == SonosDeviceReadStatus.Discarded, "status Discarded");
        // Dane STAREGO konta nie wychodza - nawet poprawne.
        Check(result.Playlists is null && !result.Succeeded, "zero danych starego konta");
    }

    private static void MessagesHideSecrets()
    {
        var secrets = new[] { Fixture.Access, Fixture.AccessB, Key, Household, PlaylistId, "Poranek" };

        foreach (SonosDeviceReadStatus status in Enum.GetValues<SonosDeviceReadStatus>())
        {
            var message = SonosPlaylistsReadMessages.Describe(status);
            Check(!string.IsNullOrWhiteSpace(message), "komunikat niepusty");
            foreach (var secret in secrets)
            {
                Check(!message.Contains(secret, StringComparison.OrdinalIgnoreCase), "komunikat bez tajemnic");
            }
        }

        // Komunikat playlist mowi o PLAYLISTACH, nie o ulubionych ani urzadzeniach.
        Check(SonosPlaylistsReadMessages.Describe(SonosDeviceReadStatus.Success).Contains("playlist", StringComparison.OrdinalIgnoreCase),
            "komunikat nazywa wlasny zasob");

        var list = new SonosPlaylistsList(Household, "v-1", new[]
        {
            new SonosPlaylist(PlaylistId, "Poranek", "playlist", 12)
        });

        var texts = new[]
        {
            list.ToString(),
            list.Items[0].ToString(),
            SonosPlaylistsReadResult.CreateForMeasurement(SonosDeviceReadStatus.Success, list).ToString(),
            SonosPlaylistsOutcome.Ok(list).ToString()
        };

        foreach (var text in texts)
        {
            foreach (var secret in secrets)
            {
                Check(!text.Contains(secret, StringComparison.Ordinal), "ToString bez tajemnic: " + secret);
            }
        }

        // Pusta kolekcja NADAL jest sukcesem - takze po stronie wyniku konta.
        var empty = new SonosPlaylistsList(Household, null, Array.Empty<SonosPlaylist>());
        Check(SonosPlaylistsReadResult.CreateForMeasurement(SonosDeviceReadStatus.Success, empty).Succeeded,
            "pusta lista to sukces");

        // Blad i porzucenie NIE przenosza danych.
        Check(SonosPlaylistsReadResult.CreateForMeasurement(SonosDeviceReadStatus.Discarded, list).Playlists is null,
            "porzucony wynik bez danych");
    }

    // ================= aparatura =================

    private static void Check(bool ok, string label = "bez etykiety")
    {
        if (!ok)
        {
            throw new InvalidOperationException("Niespelniona asercja: " + label);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Co NAPRAWDE poszlo w zadaniu - odczytane z prawdziwego HttpRequestMessage.</summary>
    private sealed record Sent(
        string? Method, string? Token, string? Path,
        string? Playlist, string? Favorite, string? Action, bool? PlayOnCompletion, bool HasPlayModes)
    {
        internal static Sent From(HttpRequestMessage request)
        {
            string? playlist = null;
            string? favorite = null;
            string? action = null;
            bool? play = null;
            var hasPlayModes = false;
            if (request.Content is { } content)
            {
                var body = content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                playlist = root.TryGetProperty("playlistId", out var id) ? id.GetString() : null;
                favorite = root.TryGetProperty("favoriteId", out var other) ? other.GetString() : null;
                action = root.TryGetProperty("action", out var value) ? value.GetString() : null;
                play = root.TryGetProperty("playOnCompletion", out var flag) ? flag.GetBoolean() : null;
                hasPlayModes = root.TryGetProperty("playModes", out _);
            }

            return new Sent(
                request.Method.Method,
                request.Headers.Authorization?.Parameter,
                request.RequestUri?.AbsolutePath,
                playlist, favorite, action, play, hasPlayModes);
        }
    }

    /// <summary>Bramka zdarzeniowa z TWARDYM limitem: niedotrzymanie to BLAD, nie ciche przejscie.</summary>
    private sealed class Gate
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void EnterAndWait()
        {
            entered.TrySetResult();
            if (!released.Task.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Bramka testu nie zostala zwolniona w terminie.");
            }
        }

        internal bool WaitEntered() => entered.Task.Wait(Deadline);

        internal void Release() => released.TrySetResult();
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        private int count;

        internal Func<HttpRequestMessage, HttpResponseMessage> Reply { get; set; } = reply;

        public int Count => Volatile.Read(ref count);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref count);
            var response = Reply(request);
            // Bez tego klient nie widzi, na ktory adres przyszla odpowiedz.
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Access = "SYNTHETIC-ACCESS-ONLY";
        internal const string AccessB = "SYNTHETIC-ACCESS-B";
        internal const string Renewed = "SYNTHETIC-ACCESS-RENEWED";
        private const string Broker = "https://broker.invalid/";

        private readonly Handler handler;
        private readonly SonosControlApiClient client;
        private readonly Store store;

        private Fixture(SonosAccountCoordinator coordinator, Handler handler, SonosControlApiClient client, Gateway gateway, Store store)
        {
            Coordinator = coordinator;
            this.handler = handler;
            this.client = client;
            this.store = store;
            Gateway = gateway;
            // PRAWDZIWY klient Control API JUZ implementuje oba waskie kontrakty.
            ReadApi = client;
            LoadApi = client;
        }

        internal SonosAccountCoordinator Coordinator { get; }

        internal ISonosPlaylistsApi ReadApi { get; }

        internal ISonosPlaylistLoadApi LoadApi { get; }

        internal Gateway Gateway { get; }

        internal int Requests => handler.Count;

        internal int Refreshes => Gateway.RefreshCalls;

        internal string? StoredAccessToken => store.AccessToken;

        internal int Deletes => store.Deletes;

        internal static Fixture Connected(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: true);

        internal static Fixture WithoutAccount(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            Create(reply, connected: false);

        private static Fixture Create(Func<HttpRequestMessage, HttpResponseMessage> reply, bool connected)
        {
            var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
            var store = new Store();
            if (connected)
            {
                store.Seed(new SonosStoredCredentials(
                    Broker,
                    new SonosTokens(Access, "Bearer", 3600, "SYNTHETIC-REFRESH", "playback-control-all"),
                    now));
            }

            var gateway = new Gateway();
            var coordinator = new SonosAccountCoordinator(gateway, store, Broker, () => now);
            coordinator.RestoreOnce();

            var handler = new Handler(reply);
            var client = new SonosControlApiClient(SonosControlApiConfiguration.CreateDefault(Key), handler);
            return new Fixture(coordinator, handler, client, gateway, store);
        }

        /// <summary>PRAWDZIWA droga nowego logowania, bez podstawiania pol i bez refleksji.</summary>
        internal void LogInAsNewAccount()
        {
            Gateway.AllowLogin(AccessB);
            Coordinator.BeginLoginAsync(CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
            var login = Coordinator.CheckLoginAsync(CancellationToken.None)
                .WaitAsync(Deadline).GetAwaiter().GetResult();
            Check(login.Snapshot.HasCredentials, "nowe konto ma poswiadczenia");
            Check(StoredAccessToken == AccessB, "nowe konto zapisane");
        }

        internal SonosPlaylistsReadResult Read(string? householdId = Household) =>
            Coordinator.ReadPlaylistsAsync(ReadApi, householdId, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        internal SonosGroupCommandResult Load(
            string? groupId = Group,
            string? playlistId = PlaylistId,
            SonosFavoriteQueueAction action = SonosFavoriteQueueAction.Insert,
            bool playOnCompletion = true) =>
            Coordinator.LoadPlaylistAsync(LoadApi, groupId, playlistId, action, playOnCompletion, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();

        public void Dispose()
        {
            Coordinator.Dispose();
            client.Dispose();
            handler.Dispose();
        }
    }

    /// <summary>Atrapa bramki logowania: zero sieci, policzone odnowienia.</summary>
    private sealed class Gateway : ISonosLoginGateway
    {
        private string? loginAccessToken;
        private int refreshCalls;

        public int RefreshCalls => Volatile.Read(ref refreshCalls);

        internal void AllowLogin(string accessToken) => loginAccessToken = accessToken;

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
            Task.FromResult(loginAccessToken is null
                ? SonosLoginStartOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginStartOutcome.Ok(new SonosLoginSession(
                    "synthetic-session",
                    new string('a', 48),
                    new Uri("https://api.sonos.com/login/v3/oauth"),
                    DateTimeOffset.UtcNow.AddMinutes(5))));

        public Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken cancellationToken) =>
            Task.FromResult(loginAccessToken is null
                ? SonosLoginResultOutcome.Failure(SonosLoginStatus.BrokerUnreachable)
                : SonosLoginResultOutcome.Ok(new SonosTokens(
                    loginAccessToken, "Bearer", 3600, "SYNTHETIC-REFRESH-B", "playback-control-all")));

        public Task<SonosRefreshOutcome> RefreshAsync(string? token, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref refreshCalls);
            return Task.FromResult(SonosRefreshOutcome.Ok(new SonosTokens(
                Fixture.Renewed, "Bearer", 3600, "SYNTHETIC-REFRESH", "playback-control-all")));
        }
    }

    /// <summary>Atrapa magazynu w PAMIECI: bez DPAPI i bez pliku. Liczy zapisy i usuniecia.</summary>
    private sealed class Store : ISonosCredentialStore
    {
        private SonosStoredCredentials? record;

        internal void Seed(SonosStoredCredentials value) => record = value;

        internal string? AccessToken => record?.Tokens.AccessToken;

        internal int Deletes { get; private set; }

        public SonosCredentialReadOutcome Read() => record is null
            ? SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing)
            : SonosCredentialReadOutcome.Ok(record);

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            record = credentials;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            record = null;
            return true;
        }
    }
}
