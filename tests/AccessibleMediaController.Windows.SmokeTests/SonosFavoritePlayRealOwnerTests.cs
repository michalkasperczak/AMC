using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// F3c DOWOD CALEJ DROGI: PRAWDZIWE <see cref="MainWindow"/> -&gt; PRODUKCYJNY
/// <c>SonosAccountOwnerGroupBackend</c> -&gt; PRODUKCYJNY
/// <see cref="SonosAccountOwner"/> -&gt; ODEBRANY <see cref="SonosAccountCoordinator"/>
/// -&gt; ODEBRANY <see cref="SonosControlApiClient"/> -&gt; SYNTETYCZNY
/// <see cref="HttpMessageHandler"/>.
///
/// Dotychczasowe pomiary F3c liczyly wywolania ATRAPY zaplecza
/// (<c>LoadFakeBackend</c>), czyli NIE dowodzily, ze cokolwiek idzie w HTTP.
/// Tutaj mierzymy ZADANIA HTTP: metode, adres i CIALO.
///
/// Czego tu NIE MA i byc nie moze: prawdziwych poswiadczen, sieci, DPAPI,
/// przegladarki, audio, prawdziwego Sonosa. Magazyn i bramka logowania sa
/// syntetyczne i podstawione ISTNIEJACYMI punktami wlasciciela
/// (<c>StoreFactory</c>, <c>GatewayFactory</c>) USTAWIONYMI PRZED
/// <c>EnsureCoordinator</c>. Transport podstawiamy TEST-ONLY refleksja
/// (<c>_controlApi</c>, <c>_deviceApi</c>, <c>_groupApi</c>) PRZED pierwsza
/// praca wlasciciela - zeby NIE dodawac produkcyjnej fabryki handlera dla
/// samej wygody testu. To WSTRZYKNIECIE ZALEZNOSCI, nie reczna generacja konta.
/// </summary>
internal static class SonosFavoritePlayRealOwnerTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    /// <summary>Werdykt jednego przypadku - format wspolny z etapem spoznionego wyniku.</summary>
    internal readonly record struct Verdict(string Case, string Hypothesis, bool Passed, string Detail);

    /// <summary>
    /// WEJSCIE zestawu: WLASNY watek STA, bo mierzymy prawdziwe okna WPF.
    /// Werdykty zbieramy WSZYSTKIE i raportujemy jawnie, a na koniec zestaw
    /// PADA, jesli cokolwiek zostalo odrzucone.
    /// </summary>
    internal static void Run()
    {
        List<Verdict> verdicts = [];
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { verdicts = RunAll(); }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(180)))
        {
            throw new Exception("Limit czasu pomiaru realnej drogi F3c.");
        }

        if (failure is not null) throw failure;

        foreach (var verdict in verdicts)
        {
            Console.WriteLine(
                (verdict.Passed ? "REAL-POTWIERDZONY " : "REAL-ODRZUCONY ")
                + verdict.Case + ": " + verdict.Hypothesis + " -> " + verdict.Detail);
        }

        var rejected = verdicts.Where(verdict => !verdict.Passed).ToArray();
        Console.WriteLine("OK: F3c realna droga właściciela konta i HTTP "
            + $"({verdicts.Count} sprawdzeń)");
        if (rejected.Length != 0)
        {
            throw new Exception(
                "F3c realna droga: ODRZUCONO " + rejected.Length + " z " + verdicts.Count + ": "
                + string.Join(" || ", rejected.Select(verdict => verdict.Case + " " + verdict.Detail)));
        }
    }

    internal static List<Verdict> RunAll()
    {
        return
        [
            Measure("R1", "realna droga wysyla DOKLADNIE JEDEN POST loadFavorite z INSERT/true "
                + "dla DRUGIEJ z dwoch jednakowych etykiet", MeasureRealPostCarriesSelectedId),
            Measure("R2", "zmiana konta A->B publiczna droga koordynatora konczy stara probe "
                + "BEZ POST starym identyfikatorem i BEZ spoznionego komunikatu",
                MeasureAccountSwapStopsOldFavorite),
            Measure("R3", "busy, brak grupy i pusta lista NIE wysylaja zadnego POST, "
                + "a samo otwarcie i nawigacja nie ruszaja transportu", MeasureRefusalsNeverPost),
        ];
    }

    private static Verdict Measure(string name, string hypothesis, Func<string> body)
    {
        try
        {
            return new Verdict(name, hypothesis, true, body());
        }
        catch (Exception exception)
        {
            return new Verdict(name, hypothesis, false, exception.Message);
        }
    }

    // ==================== SYNTETYCZNY TRANSPORT ====================

    /// <summary>
    /// ZAPIS zadania HTTP dokladnie tak, jak poszlo: metoda, adres i CIALO.
    /// Cialo czytamy TU, bo po odpowiedzi strumien moze byc juz zamkniety.
    /// </summary>
    internal sealed record Wire(string Method, Uri Uri, string Body, string? Authorization);

    /// <summary>
    /// Handler proby. Natywny transport ZAWSZE ustawia
    /// <see cref="HttpResponseMessage.RequestMessage"/>, a klient tego WYMAGA
    /// (sprawdza, czy odpowiedz nalezy do tego adresu) - atrapa nie ma prawa
    /// tego kontraktu oslabiac.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _reply;

        internal RecordingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> reply) => _reply = reply;

        internal List<Wire> Requests { get; } = [];

        /// <summary>WSTRZYMANIE odpowiedzi - do pomiaru zmiany konta W LOCIE.</summary>
        internal Task? Gate { get; set; }

        /// <summary>
        /// BILET wstrzymanego POST: mowi, czy zlecenie DOTARLO do transportu,
        /// z jakim biletem konta i czy odpowiedz sie DOMKNELA. Bramka
        /// przepuszcza KAZDE nastepne zlecenie, zeby pomiar nie zawisl.
        /// </summary>
        internal sealed class HeldPost
        {
            private readonly TaskCompletionSource _release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal Task Gate => _release.Task;

            internal bool Arrived { get; set; }

            internal bool Completed { get; set; }

            internal string? Authorization { get; set; }

            internal void Release() => _release.TrySetResult();
        }

        internal HeldPost HoldNextPost()
        {
            var held = new HeldPost();
            Held = held;
            Gate = held.Gate;
            return held;
        }

        internal HeldPost? Held { get; private set; }

        internal List<Wire> Posts =>
            Requests.Where(wire => wire.Method == "POST").ToList();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            lock (Requests)
            {
                Requests.Add(new Wire(
                    request.Method.Method,
                    request.RequestUri ?? throw new Exception("Żądanie bez adresu."),
                    body,
                    request.Headers.Authorization?.Parameter));
            }

            if (Gate is { } gate && request.Method == HttpMethod.Post)
            {
                // Tylko POST czeka: odczyty topologii musza sie domknac, inaczej
                // nie byloby z czego wziac grupy. Bramke ZWALNIAMY po przejsciu,
                // zeby kolejne zlecenia nie wisialy bez powodu.
                var held = Held;
                if (held is not null)
                {
                    held.Authorization = request.Headers.Authorization?.Parameter;
                    held.Arrived = true;
                }

                Gate = null;
                await gate.ConfigureAwait(false);
                if (held is not null) held.Completed = true;
            }

            var response = _reply(request, body);
            response.RequestMessage ??= request;
            return response;
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // ==================== SYNTETYCZNE KONTO ====================

    /// <summary>Magazyn w PAMIECI. Zero DPAPI, zero dysku uzytkownika.</summary>
    private sealed class MemoryStore : ISonosCredentialStore
    {
        internal string BrokerOrigin = "https://broker-testowy.invalid/";
        internal string Access = "SYNTETYCZNY-ACCESS-A";
        internal string? Refresh = "SYNTETYCZNY-REFRESH-A";
        internal int Writes;
        internal int Deletes;

        public SonosCredentialReadOutcome Read() =>
            SonosCredentialReadOutcome.Ok(new SonosStoredCredentials(
                BrokerOrigin,
                new SonosTokens(Access, "Bearer", 3600, Refresh, "playback-control-all"),
                DateTimeOffset.UtcNow));

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            // Prawdziwy magazyn ZAPISUJE to, co dal koordynator - odwzorowujemy
            // to, zeby pomiar widzial FAKTYCZNY bilet biezacego konta.
            Writes++;
            BrokerOrigin = credentials.BrokerOrigin;
            Access = credentials.Tokens.AccessToken;
            Refresh = credentials.Tokens.RefreshToken;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            return true;
        }
    }

    /// <summary>
    /// Bramka logowania proby: oddaje ZAPLANOWANY start i wynik. Zadnej
    /// przegladarki, zadnego adresu publicznego, zadnego prawdziwego tokenu.
    /// </summary>
    private sealed class PlannedGateway : ISonosLoginGateway
    {
        internal string NextAccess = "SYNTETYCZNY-ACCESS-B";
        internal string NextRefresh = "SYNTETYCZNY-REFRESH-B";

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosLoginStartOutcome.Ok(new SonosLoginSession(
                "sesja-proby",
                new string('a', 48),
                new Uri("https://api.sonos.com/login/v3/oauth"),
                DateTimeOffset.UtcNow.AddMinutes(5))));

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session, CancellationToken cancellationToken) =>
            Task.FromResult(SonosLoginResultOutcome.Ok(new SonosTokens(
                NextAccess, "Bearer", 3600, NextRefresh, "playback-control-all")));

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken) =>
            throw new Exception("Pomiar F3c nie ma prawa odnawiać dostępu Sonos.");
    }

    // ==================== SYNTETYCZNA TOPOLOGIA ====================

    private const string HouseholdId = "Sonos_proba.dom1";
    private const string OtherHouseholdId = "Sonos_proba.dom2";
    private const string GroupId = "GRUPA-SALON:1";
    private const string OtherGroupId = "GRUPA-KUCHNIA:2";

    private static string HouseholdsBody =>
        "{\"households\":[{\"id\":\"" + HouseholdId + "\",\"name\":\"Dom próby\"},"
        + "{\"id\":\"" + OtherHouseholdId + "\",\"name\":\"Drugi dom\"}]}";

    private static string GroupsBody(string householdId) => householdId == HouseholdId
        ? "{\"groups\":[{\"id\":\"" + GroupId + "\",\"name\":\"Salon\",\"coordinatorId\":\"P1\","
            + "\"playerIds\":[\"P1\"],\"playbackState\":\"PLAYBACK_STATE_IDLE\"}],"
            + "\"players\":[{\"id\":\"P1\",\"name\":\"Salon\"}],\"partial\":false}"
        : "{\"groups\":[{\"id\":\"" + OtherGroupId + "\",\"name\":\"Kuchnia\",\"coordinatorId\":\"P2\","
            + "\"playerIds\":[\"P2\"],\"playbackState\":\"PLAYBACK_STATE_IDLE\"}],"
            + "\"players\":[{\"id\":\"P2\",\"name\":\"Kuchnia\"}],\"partial\":false}";

    /// <summary>
    /// DWIE JEDNAKOWE ETYKIETY i ROZNE identyfikatory: wybor drugiej musi
    /// wyslac DOKLADNIE jej identyfikator, nie pierwszy o tej nazwie.
    /// </summary>
    private const string FavoritesBody =
        "{\"version\":\"W1\",\"items\":["
        + "{\"id\":\"ULU-PIERWSZY\",\"name\":\"Nokturny\"},"
        + "{\"id\":\"ULU-DRUGI\",\"name\":\"Nokturny\"}]}";

    private const string EmptyFavoritesBody = "{\"version\":\"W1\",\"items\":[]}";

    /// <summary>
    /// ODPOWIEDZ syntetycznej chmury wybrana PO ADRESIE - dokladnie jak
    /// prawdziwy serwer. Nic tu nie zgaduje: nieznana trasa to twarde
    /// niepowodzenie pomiaru, a nie cichy sukces.
    /// </summary>
    private static HttpResponseMessage ReplyByRoute(
        HttpRequestMessage request, string body, Func<string>? favorites = null)
    {
        _ = body;
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get)
        {
            if (path.EndsWith("/households", StringComparison.Ordinal)) return Json(HouseholdsBody);
            if (path.EndsWith("/groups", StringComparison.Ordinal))
            {
                return Json(GroupsBody(path.Contains(OtherHouseholdId, StringComparison.Ordinal)
                    ? OtherHouseholdId
                    : HouseholdId));
            }

            if (path.EndsWith("/favorites", StringComparison.Ordinal))
            {
                return Json(favorites is null ? FavoritesBody : favorites());
            }

            if (path.Contains("/playback", StringComparison.Ordinal)
                || path.Contains("/groupVolume", StringComparison.Ordinal))
            {
                // ODCZYT stanu po poleceniu - istniejaca droga, nie nowa polityka.
                return Json("{}");
            }
        }

        if (request.Method == HttpMethod.Post && path.EndsWith("/favorites", StringComparison.Ordinal))
        {
            // PRZYJECIE zlecenia. Sonos oddaje pusty obiekt - 200 nie dowodzi,
            // ze muzyka zagrala.
            return Json("{}");
        }

        throw new Exception("Pomiar nie zaplanował odpowiedzi dla " + request.Method + " " + path + ".");
    }

    // ==================== R1: JEDEN POST realna droga ====================

    /// <summary>
    /// DWIE JEDNAKOWE ETYKIETY, ROZNE ID: Enter na DRUGIEJ musi wyslac
    /// DOKLADNIE JEDEN POST na adres AKTYWNEJ grupy, z cialem
    /// <c>favoriteId/action=INSERT/playOnCompletion=true</c> i BEZ
    /// <c>playModes</c>. Zadnego dodatkowego Play/Toggle.
    /// </summary>
    private static string MeasureRealPostCarriesSelectedId()
    {
        using var harness = RealHarness.Create();
        harness.Enter();

        var postsBefore = harness.Handler.Posts.Count;
        var status = string.Empty;
        var labels = Array.Empty<string>();
        harness.RunFavoritesModal(window =>
        {
            labels = window.RowLabelsForTests.ToArray();
            // DRUGA z dwoch jednakowych etykiet - realny callback Enter.
            window.SelectForTests(1);
            window.PressEnterForTests();
            window.AwaitPlayForTests();
            status = window.StatusForTests;
        });

        if (labels.Length != 2 || labels[0] != labels[1])
        {
            throw new Exception("Lista nie miała dwóch JEDNAKOWYCH etykiet: "
                + string.Join(" | ", labels) + ".");
        }

        var posts = harness.Handler.Posts.Skip(postsBefore).ToArray();
        if (posts.Length != 1)
        {
            throw new Exception($"Enter wysłał {posts.Length} POST zamiast dokładnie jednego: "
                + string.Join(" | ", posts.Select(post => post.Method + " " + post.Uri)) + ".");
        }

        var post = posts[0];
        var expected = new Uri(
            SonosControlApiConfiguration.CreateDefault().Origin,
            "groups/" + GroupId + "/favorites");
        if (post.Uri != expected)
        {
            throw new Exception("POST poszedł na \"" + post.Uri + "\" zamiast \"" + expected + "\".");
        }

        using var document = JsonDocument.Parse(post.Body);
        var root = document.RootElement;
        var favoriteId = root.GetProperty("favoriteId").GetString();
        var action = root.GetProperty("action").GetString();
        var play = root.GetProperty("playOnCompletion").GetBoolean();
        if (favoriteId != "ULU-DRUGI")
        {
            throw new Exception("POST poniósł favoriteId \"" + favoriteId
                + "\" - nie identyfikator DRUGIEJ, wybranej pozycji.");
        }

        if (action != "INSERT" || !play)
        {
            throw new Exception($"Ciało POST miało action=\"{action}\", playOnCompletion={play} "
                + "zamiast INSERT i true.");
        }

        if (root.TryGetProperty("playModes", out _))
        {
            throw new Exception("Ciało POST zawiera playModes - jawne tryby zmieniłyby ustawienia głośnika.");
        }

        if (root.EnumerateObject().Count() != 3)
        {
            throw new Exception("Ciało POST ma pola ponad umówione trzy: " + post.Body);
        }

        if (post.Authorization != harness.Store.Access)
        {
            throw new Exception("POST nie poniósł biletu bieżącego konta.");
        }

        // ZADNEGO dodatkowego polecenia transportu: po POST wolno tylko ODCZYTAC
        // stan tej samej grupy istniejaca droga.
        var extraPosts = harness.Handler.Posts.Count - postsBefore - 1;
        if (extraPosts != 0)
        {
            throw new Exception($"Po uruchomieniu poszło {extraPosts} dodatkowych POST (Play/Toggle).");
        }

        // STATUS mowi o PRZYJECIU, nie o potwierdzonym graniu.
        if (!status.Contains("Przyjęto polecenie uruchomienia", StringComparison.Ordinal))
        {
            throw new Exception("Modal nie pokazał przyjęcia zlecenia: \"" + status + "\".");
        }

        return "1 POST " + expected.AbsolutePath + ", favoriteId=ULU-DRUGI, INSERT/true, bez playModes, "
            + "bilet konta w nagłówku, status o PRZYJĘCIU";
    }

    // ==================== R2/R3: dopisywane po GREEN R1 ====================

    /// <summary>
    /// ZMIANA KONTA publiczna droga (BeginLoginAsync + CheckLoginAsync) przy
    /// OTWARTYM modalu: stare ULUBIONE nie ma prawa pojsc na bilecie konta B,
    /// a wstrzymany POST konta A po zmianie nie wolno oglaszac jako sukcesu.
    /// </summary>
    private static string MeasureAccountSwapStopsOldFavorite()
    {
        using var harness = RealHarness.Create();
        harness.Enter();

        var held = harness.Handler.HoldNextPost();
        var accountA = harness.Store.Access;
        var statusAfterSwap = string.Empty;
        var spokenAfterSwap = 0;
        var statusAfterRetry = string.Empty;
        var postsAfterRetry = 0;
        harness.RunFavoritesModal(window =>
        {
            window.SelectForTests(1);
            window.PressEnterForTests();
            harness.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(5));
            if (!held.Arrived) throw new Exception("POST konta A nie dotarł do transportu.");

            // ZMIANA KONTA PRAWDZIWA PUBLICZNA DROGA, modal wciaz otwarty,
            // a POST konta A wciaz wstrzymany.
            harness.SwapAccount("KONTO-B");

            var taskA = window.LastPlayTaskForTests
                ?? throw new Exception("Brak zadania polecenia A.");
            var spokenBefore = window.AnnouncementsForTests;
            held.Release();
            harness.Pump(taskA);
            spokenAfterSwap = window.AnnouncementsForTests - spokenBefore;
            statusAfterSwap = window.StatusForTests;
            if (statusAfterSwap == SonosFavoritesLabels.PlayPending)
            {
                throw new Exception("L1: zakonczona proba pozostawila zywy modal w stanie Czekaj: "
                    + statusAfterSwap);
            }

            // PONOWNA proba w TYM SAMYM modalu: stare ULUBIONE nie ma prawa
            // pojsc przez NOWE konto - droga konczy sie BEZ POST.
            var postsBeforeRetry = harness.Handler.Posts.Count;
            window.PressEnterForTests();
            window.AwaitPlayForTests();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(300));
            postsAfterRetry = harness.Handler.Posts.Count - postsBeforeRetry;
            statusAfterRetry = window.StatusForTests;
        });

        if (held.Authorization != accountA)
        {
            throw new Exception("Wstrzymany POST nie poniósł biletu konta A.");
        }

        if (harness.Store.Access == accountA)
        {
            throw new Exception("Publiczna droga logowania NIE zmieniła konta - pomiar nic nie mierzy.");
        }

        // GRANICA: zadne zlecenie NIE poszlo na bilecie konta B.
        var onB = harness.Handler.Posts
            .Where(post => post.Authorization == harness.Store.Access).ToArray();
        if (onB.Length != 0)
        {
            throw new Exception($"Stare ulubione poszło na bilecie konta B ({onB.Length} POST).");
        }

        // SPOZNIONA odpowiedz konta A nie oglasza sukcesu w modalu po zmianie konta.
        if (statusAfterSwap.Contains("Przyjęto polecenie uruchomienia", StringComparison.Ordinal))
        {
            throw new Exception("Modal po zmianie konta pokazał PRZYJĘCIE zlecenia konta A: \""
                + statusAfterSwap + "\".");
        }

        if (spokenAfterSwap != 0)
        {
            throw new Exception($"Modal ogłosił {spokenAfterSwap} komunikatów o wyniku konta A "
                + "po zmianie konta.");
        }

        if (postsAfterRetry != 0)
        {
            throw new Exception($"Po zmianie konta ponowny Enter wysłał {postsAfterRetry} POST "
                + "ze STARYM identyfikatorem ulubionego.");
        }

        if (statusAfterRetry.Contains("Przyjęto polecenie uruchomienia", StringComparison.Ordinal))
        {
            throw new Exception("Ponowna próba po zmianie konta udaje sukces: \""
                + statusAfterRetry + "\".");
        }

        return $"POST konta A na bilecie A, 0 POST na bilecie B, status po zmianie \"{statusAfterSwap}\", "
            + $"0 ogłoszeń wyniku A, ponowny Enter 0 POST, status \"{statusAfterRetry}\"";
    }

    /// <summary>
    /// ODMOWY nie ruszaja transportu: samo otwarcie i nawigacja (strzalki, Tab),
    /// PUSTA lista oraz brak celu daja ZERO POST, a zwykly przycisk i Enter
    /// dzialaja tak samo.
    /// </summary>
    private static string MeasureRefusalsNeverPost()
    {
        // (a) samo OTWARCIE i NAWIGACJA - zero POST.
        using (var quiet = RealHarness.Create())
        {
            quiet.Enter();
            var before = quiet.Handler.Posts.Count;
            quiet.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.SelectForTests(1);
                window.PressTabForTests();
            });

            var moved = quiet.Handler.Posts.Count - before;
            if (moved != 0)
            {
                throw new Exception($"Samo otwarcie i nawigacja wysłały {moved} POST.");
            }
        }

        // (b) PUSTA lista - zero POST nawet po Enter.
        using (var empty = RealHarness.Create(() => EmptyFavoritesBody))
        {
            empty.Enter();
            var before = empty.Handler.Posts.Count;
            var status = string.Empty;
            empty.RunFavoritesModal(window =>
            {
                window.PressEnterForTests();
                window.AwaitPlayForTests();
                status = window.StatusForTests;
            });

            var sent = empty.Handler.Posts.Count - before;
            if (sent != 0)
            {
                throw new Exception($"Pusta lista wysłała {sent} POST.");
            }

            if (status.Contains("Przyjęto polecenie uruchomienia", StringComparison.Ordinal))
            {
                throw new Exception("Pusta lista udaje przyjęcie zlecenia: \"" + status + "\".");
            }
        }

        // (c) ZWYKLY PRZYCISK dziala tak samo jak Enter - JEDEN POST.
        using (var button = RealHarness.Create())
        {
            button.Enter();
            var before = button.Handler.Posts.Count;
            button.RunFavoritesModal(window =>
            {
                window.SelectForTests(1);
                window.ClickPlayForTests();
                window.AwaitPlayForTests();
            });

            var sent = button.Handler.Posts.Count - before;
            if (sent != 1)
            {
                throw new Exception($"Przycisk Odtwórz wysłał {sent} POST zamiast jednego.");
            }
        }

        // (d) DWUKROTNE, SZYBKIE zlecenie (auto-powtarzanie klawisza) - jeden POST.
        using (var busy = RealHarness.Create())
        {
            busy.Enter();
            var held = busy.Handler.HoldNextPost();
            var second = string.Empty;
            busy.RunFavoritesModal(window =>
            {
                window.SelectForTests(1);
                window.PressEnterForTests();
                busy.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(5));
                window.PressEnterForTests();
                second = window.StatusForTests;
                held.Release();
                busy.PumpUntil(() => held.Completed, TimeSpan.FromSeconds(5));
                window.AwaitPlayForTests();
            });

            var sent = busy.Handler.Posts.Count;
            if (sent != 1)
            {
                throw new Exception($"Auto-powtarzanie wysłało {sent} POST zamiast jednego.");
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                throw new Exception("Druga próba w trakcie zlecenia nic nie powiedziała.");
            }
        }

        return "otwarcie i nawigacja 0 POST, pusta lista 0 POST, przycisk 1 POST, "
            + "auto-powtarzanie 1 POST";
    }

    // ==================== APARATURA REALNEJ DROGI ====================

    /// <summary>
    /// PRAWDZIWE okno glowne z PRODUKCYJNYM wlascicielem konta, syntetycznym
    /// magazynem/bramka i syntetycznym transportem. <c>SonosBackendOverride</c>
    /// NIE jest tu uzywany: zaplecze to PRODUKCYJNY
    /// <c>SonosAccountOwnerGroupBackend</c> nad tym samym wlascicielem.
    /// </summary>
    private sealed class RealHarness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(25);
        private readonly string _directory;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        private RealHarness(
            string directory,
            MainWindow window,
            SonosAccountOwner owner,
            RecordingHandler handler,
            MemoryStore store,
            PlannedGateway gateway,
            List<string> announcements)
        {
            _directory = directory;
            Window = window;
            Owner = owner;
            Handler = handler;
            Store = store;
            Gateway = gateway;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal SonosAccountOwner Owner { get; }

        internal RecordingHandler Handler { get; }

        internal MemoryStore Store { get; }

        internal PlannedGateway Gateway { get; }

        internal List<string> Announcements { get; }

        private ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal static RealHarness Create(Func<string>? favorites = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "amc-f3c-real-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var configuration = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = configuration.LoadOrCreate();
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            state.Podcasts.Subscriptions.Clear();
            state.Podcasts.Episodes.Clear();
            state.WiiM.Devices.Clear();
            state.Radio.RecordingSchedules.Clear();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;

            var announcements = new List<string>();
            var window = new MainWindow(state, configuration)
            {
                SuppressDesktopIntegrationForTests = true,
                AnnouncementSinkForTests = announcements.Add
            };
            window.DenyApplicationUpdateStartForTests();

            var owner = (SonosAccountOwner)(typeof(MainWindow).GetField("_sonosAccount", Instance)
                ?? throw new Exception("Nie ma pola _sonosAccount w prawdziwym MainWindow."))
                .GetValue(window)!;
            if (owner.HasCoordinator)
            {
                throw new Exception("Konstrukcja okna zainicjowała konto Sonos przed jawnym wejściem.");
            }

            // KONTO: ISTNIEJACE punkty podstawienia wlasciciela, USTAWIONE PRZED
            // pierwszym EnsureCoordinator. Inicjalizator po new dziala PO
            // konstruktorze, wiec liczy sie to, ze konstruktor konta NIE budzi -
            // co wlasnie sprawdzilismy wyzej.
            var store = new MemoryStore();
            var gateway = new PlannedGateway();
            owner.StoreFactory = broker =>
            {
                store.BrokerOrigin = broker.Origin.AbsoluteUri;
                return store;
            };
            owner.GatewayFactory = _ => gateway;

            // TRANSPORT: PRAWDZIWY SonosControlApiClient z syntetycznym handlerem,
            // wstawiony TEST-ONLY refleksja PRZED pierwsza praca wlasciciela.
            // EnsureControlApiClient nie ma fabryki handlera, a dodawanie jej do
            // produkcji tylko dla wygody pomiaru byloby publicznym API bez
            // produkcyjnego wolajacego.
            var handler = new RecordingHandler((request, body) => ReplyByRoute(request, body, favorites));
            var client = new SonosControlApiClient(
                SonosControlApiConfiguration.CreateDefault(SonosAccountOwner.IntegrationApiKey), handler);
            InjectTransport(owner, client);
            return new RealHarness(directory, window, owner, handler, store, gateway, announcements);
        }

        /// <summary>
        /// PODSTAWIENIE transportu w ISTNIEJACE pola wlasciciela wraz z
        /// ISTNIEJACYMI adapterami <c>_deviceApi</c>/<c>_groupApi</c>. Sygnatury
        /// odczytane ze zrodla, nie z pamieci - brak ktoregokolwiek pola to
        /// TWARDY blad pomiaru, nie cicha zmiana drogi.
        /// </summary>
        private static void InjectTransport(SonosAccountOwner owner, SonosControlApiClient client)
        {
            var type = typeof(SonosAccountOwner);
            void Set(string field, object value) =>
                (type.GetField(field, Instance)
                    ?? throw new Exception("Właściciel konta nie ma pola " + field + "."))
                .SetValue(owner, value);

            Set("_controlApi", client);
            Set("_deviceApi", new SonosControlApiDeviceApi(client));
            Set("_groupApi", new SonosControlApiGroupApi(client));
            if (owner.ControlApiCreations != 0)
            {
                throw new Exception("Wstrzyknięcie transportu podniosło licznik tworzenia klienta.");
            }
        }

        /// <summary>WEJSCIE w sesje Sonos PRODUKCYJNA droga - przez PRAWDZIWY HTTP GET.</summary>
        internal void Enter()
        {
            var rendered = (EventHandler)Delegate.CreateDelegate(
                typeof(EventHandler), Window, typeof(MainWindow).GetMethod("Window_ContentRendered", Instance)!);
            Window.ContentRendered -= rendered;
            Window.ShowInTaskbar = false;
            Window.Show();
            PumpUntil(() => Window.IsLoaded && PresentationSource.FromVisual(Window) is not null,
                "okno główne się nie pokazało");
            foreach (var field in new[] { "_nvdaCommandServer", "_prefixService" })
            {
                if (typeof(MainWindow).GetField(field, Instance)?.GetValue(Window) is not null)
                {
                    throw new Exception("Aparatura wystartowała produkcyjną usługę pulpitu: " + field);
                }
            }

            Window.Activate();
            PumpUntil(() => Window.IsActive, "okno główne nie stało się aktywne");
            // WYBOR DOMU idzie z PRAWDZIWEGO odczytu syntetycznej topologii: dwa
            // domy, wiec zadna regula "jeden dom sam sie wybiera" tu nie dziala -
            // ustawiamy wybor jawnie, jak uzytkownik w oknie wyboru domu.
            Window.StateForTests.Sonos.SelectedHouseholdId = HouseholdId;
            var slot = Window.SessionsForTests.FindSlot("sonos")
                ?? throw new Exception("Konfiguracja nie ma slotu sesji sonos.");
            ExecuteCommand(CommandIds.SessionSlot(slot));
            PumpUntil(() => MediaList.Items.Count >= 1, "sesja Sonos nie pokazała grupy z odczytu HTTP");
            Pump(Window.ActivateSonosGroupForTests(GroupId));
            if (Window.SonosActiveGroup is not { } group || group.Id != GroupId)
            {
                throw new Exception("Aktywna grupa Sonos nie pochodzi z odczytanej topologii.");
            }

            if (Handler.Requests.Count == 0)
            {
                throw new Exception("Wejście w sesję nie wykonało ŻADNEGO żądania HTTP - "
                    + "transport nie jest podłączony do realnej drogi.");
            }

            if (Handler.Posts.Count != 0)
            {
                throw new Exception($"Samo wejście w sesję wysłało {Handler.Posts.Count} POST.");
            }
        }

        /// <summary>
        /// PRAWDZIWY modal ulubionych: produkcyjne polecenie tworzy okno,
        /// podstawiamy TYLKO pokazanie, a kroki jada z timera w petli modalu.
        /// </summary>
        internal void RunFavoritesModal(Action<SonosFavoritesWindow> steps)
        {
            Exception? inside = null;
            Window.PresentSonosFavoritesOverrideForTests = dialog =>
            {
                dialog.ShowInTaskbar = false;
                var timer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(25)
                };
                timer.Tick += (_, _) =>
                {
                    if (!dialog.IsLoaded || !dialog.IsVisible) return;
                    timer.Stop();
                    try { steps(dialog); }
                    catch (Exception exception) { inside = exception; }
                    finally
                    {
                        if (dialog.IsVisible)
                        {
                            try { dialog.Close(); } catch (InvalidOperationException) { }
                        }
                    }
                };
                timer.Start();
                try { dialog.ShowDialog(); }
                finally { timer.Stop(); }
            };
            try
            {
                ExecuteCommand(CommandIds.ViewFavorites);
                Pump(Window.LastSonosFavoritesTaskForTests
                    ?? throw new Exception("Polecenie nie rozpoczęło odczytu ulubionych."));
            }
            finally
            {
                Window.PresentSonosFavoritesOverrideForTests = null;
            }

            if (inside is not null) throw inside;
        }

        /// <summary>
        /// ZMIANA KONTA PRODUKCYJNA, PUBLICZNA droga koordynatora: rozpoczecie
        /// logowania i odebranie wyniku z zaplanowanej bramki. Zaden licznik
        /// generacji nie jest tu podkrecany recznie.
        /// </summary>
        internal void SwapAccount(string label)
        {
            Gateway.NextAccess = "SYNTETYCZNY-ACCESS-" + label;
            Gateway.NextRefresh = "SYNTETYCZNY-REFRESH-" + label;
            var coordinator = Coordinator;
            var begin = coordinator.BeginLoginAsync(CancellationToken.None);
            Pump(begin);
            if (!begin.Result.Started)
            {
                throw new Exception("Publiczna droga nie rozpoczęła logowania.");
            }

            var check = coordinator.CheckLoginAsync(CancellationToken.None);
            Pump(check);
            if (!check.Result.Connected)
            {
                throw new Exception("Publiczna droga nie podłączyła nowego konta: "
                    + check.Result.LoginStatus + ".");
            }

            PumpQuietly(TimeSpan.FromMilliseconds(200));
        }

        /// <summary>PRAWDZIWY koordynator odebrany z produkcyjnego wlasciciela.</summary>
        internal SonosAccountCoordinator Coordinator =>
            (SonosAccountCoordinator)(typeof(SonosAccountOwner)
                .GetField("_coordinator", Instance)
                ?.GetValue(Owner)
                ?? throw new Exception("Właściciel konta nie ma koordynatora."));

        internal void ReactivateOwnWindow()
        {
            Window.Activate();
            PumpUntil(() => Window.IsActive, "okno główne nie wróciło do aktywności");
        }

        internal void ExecuteCommand(string commandId)
        {
            var method = typeof(MainWindow).GetMethod(
                "ExecuteCommand", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej metody ExecuteCommand(string).");
            try { method.Invoke(Window, [commandId]); }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu zadania produkcyjnego.");
                DoEvents();
            }

            task.GetAwaiter().GetResult();
        }

        /// <summary>Pompuje petle komunikatow do warunku, z WLASNYM limitem.</summary>
        internal void PumpUntil(Func<bool> condition, TimeSpan limit)
        {
            var deadline = DateTime.UtcNow + limit;
            while (!condition() && DateTime.UtcNow < deadline) DoEvents();
        }

        internal void PumpUntil(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }
        }

        internal void PumpQuietly(TimeSpan duration)
        {
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline) DoEvents();
        }

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        public void Dispose()
        {
            try { Window.Close(); } catch (InvalidOperationException) { }
            try { Owner.Dispose(); } catch (ObjectDisposedException) { }
            try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        }
    }
}
