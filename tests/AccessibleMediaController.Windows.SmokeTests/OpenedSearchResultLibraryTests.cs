using System.Net;
using System.Net.Http;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Core.Tidal;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// GLOBALNOSC opcji „Enter dodaje do Biblioteki”: radio, TIDAL i Spotify.
///
/// Test mierzy PRODUKCYJNE pisarze (TidalIntegrationService, SpotifyIntegrationService
/// -> SpotifyLibraryWriteClient) przez atrapę transportu HTTP. Nic nie leci do
/// prawdziwych kont: poswiadczenie wchodzi waskim szwem tokenProvider, wiec
/// Menedzer poswiadczen Windows nie jest w ogole czytany.
///
/// Sprawdzane sa DWA tryby i zachowanie juz zapisanego elementu:
/// - OpenWithoutLibrary (domyslny): ZERO mutacji HTTP, istniejace czlonkostwa nietkniete.
/// - AddToLibrary: kazda z trzech uslug faktycznie dodaje otwarty wynik.
/// - Element juz zapisany: przy ON powtorny Enter NIE usuwa go (intencja ADD, nie TOGGLE).
/// </summary>
internal static class OpenedSearchResultLibraryTests
{
    internal static void Run()
    {
        TestPlanDlaObuTrybow();
        TestPlanNieJestPrzelacznikiem();
        TestPlanNieDotyczyNieotwierajacychPolecen();
        TestRadioObaTryby();
        TestPrawdziwegoEnterWWpf();
        // Earlier WPF tests can leave a DispatcherSynchronizationContext on
        // the runner thread. Service-only awaits must not capture that blocked
        // dispatcher; the separate UI cases above explicitly pump their STA.
        Task.Run(async () =>
        {
            await TidalObaTryby().ConfigureAwait(false);
            await SpotifyObaTryby().ConfigureAwait(false);
            await SpotifyOdmowaNieKlamieOZapisie().ConfigureAwait(false);
        }).GetAwaiter().GetResult();
        Console.WriteLine(
            "OK: Enter i Biblioteka — radio, TIDAL i Spotify w obu trybach, "
            + "zapisany element nietkniety, przy OFF zero zapytan HTTP");
    }

    // Actual modal-search completion -> MainWindow.ShowSearch -> production
    // membership writers. A removed call site must fail this test.
    private static void TestPrawdziwegoEnterWWpf()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            try
            {
                foreach (var sessionId in new[] { Radio, Tidal, Spotify })
                foreach (var behavior in new[] { SearchResultEnterBehavior.OpenWithoutLibrary, SearchResultEnterBehavior.AddToLibrary })
                    VerifyEnter(sessionId, behavior);
                TestCtrlEnterOutcome("pause");
                TestCtrlEnterOutcome("rejected");
                TestCtrlEnterOutcome("started");
            }
            catch (Exception ex) { failure = ex; }
            finally { dispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(100))) throw new Exception("WPF Enter did not finish within 100s");
        if (failure is not null) throw new Exception("Rzeczywisty Enter WPF", failure);
    }

    private static void TestCtrlEnterOutcome(string scenario)
    {
        const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var root = Path.Combine(Path.GetTempPath(), "amc-search-play-action-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        AccessibleMediaController.Windows.MainWindow? window = null;
        try
        {
            var state = ConfigurationStore.CreateDefaultState();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.LastSessionId = Radio;
            state.Settings.SearchResultEnterBehavior = SearchResultEnterBehavior.AddToLibrary;
            window = new AccessibleMediaController.Windows.MainWindow(state,
                new ConfigurationStore(Path.Combine(root, "state.json")))
                { SuppressDesktopIntegrationForTests = true };
            var type = typeof(AccessibleMediaController.Windows.MainWindow);
            type.GetField("_initialFocusApplied", f)!.SetValue(window, true);
            var sessions = (SessionManager)type.GetField("_sessions", f)!.GetValue(window)!;
            var session = sessions.FindSession(Radio)!;
            // Exercise the real session state machine without emitting audio.
            typeof(DemoMediaSession).GetField("_output", f)!.SetValue(session, null);
            var item = Station();
            var backing = (List<MediaItem>)type.GetField("_radioItems", f)!.GetValue(window)!;
            backing.Clear();
            backing.Add(item);
            session.ReplaceItems(scenario == "rejected" ? [] : [item]);
            sessions.SelectSession(Radio);
            if (scenario == "pause")
            {
                session.Play(item);
                Check(session.IsPlaying, "Precondition: target must already play before CtrlEnter");
            }
            window.ShowInTaskbar = false;
            window.Show();
            var result = new AccessibleMediaController.Windows.SearchWindow.SearchResult(Radio, item);
            var announcement = (string?)type.GetMethod("ExecuteSearchResultAction", f)!.Invoke(window,
                [new[] { result }, new[] { result }, AccessibleMediaController.Windows.SearchResultAction.TogglePlayback, false]);
            if (scenario == "pause")
            {
                Check(!session.IsPlaying && session.IsPaused, "CtrlEnter did not actually pause the existing item");
                Check(!item.IsInLibrary, "CtrlEnter pause unexpectedly added the station to Library");
            }
            else if (scenario == "rejected")
            {
                Check(!session.IsPlaying && !session.HasItems
                    && announcement?.Contains("Brak elementów", StringComparison.Ordinal) == true,
                    "Precondition: empty-session activation did not report missing items: " + announcement);
                Check(!item.IsInLibrary, "Rejected CtrlEnter unexpectedly added the station to Library");
            }
            else
            {
                Check(session.IsPlaying && session.CurrentItem.Id == item.Id, "CtrlEnter did not start the selected item");
                Check(item.IsInLibrary, "Successful CtrlEnter did not add the station in ON mode");
            }
            Console.WriteLine("OK: CtrlEnter outcome " + scenario);
        }
        finally
        {
            window?.Close();
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private static void VerifyEnter(string sessionId, SearchResultEnterBehavior behavior)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var root = Path.Combine(Path.GetTempPath(), "amc-search-enter-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        AccessibleMediaController.Windows.MainWindow? window = null;
        using var tidalHandler = new TidalMembershipStub();
        using var tidalHttp = new HttpClient(tidalHandler);
        using var spotifyHandler = new SpotifyLibraryStub();
        using var spotifyHttp = new HttpClient(spotifyHandler);
        try
        {
            var state = ConfigurationStore.CreateDefaultState();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.LastSessionId = sessionId;
            state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
            state.Settings.SearchResultEnterBehavior = behavior;
            var store = new ConfigurationStore(Path.Combine(root, "state.json"));
            window = new AccessibleMediaController.Windows.MainWindow(state, store)
            { SuppressDesktopIntegrationForTests = true };
            var type = typeof(AccessibleMediaController.Windows.MainWindow);
            var sessions = (SessionManager)type.GetField("_sessions", flags)!.GetValue(window)!;
            var session = sessions.FindSession(sessionId)!;
            var item = sessionId == Radio ? Station() : sessionId == Tidal ? TidalAlbum() : SpotifyTrack();
            var itemsField = sessionId == Radio ? "_radioItems" : sessionId == Tidal ? "_tidalItems" : "_spotifyItems";
            var backing = (List<MediaItem>)type.GetField(itemsField, flags)!.GetValue(window)!;
            backing.Clear();
            backing.Add(item);
            session.ReplaceItems([item]);
            sessions.SelectSession(sessionId);
            if (sessionId == Tidal)
            {
                ((IDisposable)type.GetField("_tidalIntegration", flags)!.GetValue(window)!).Dispose();
                var service = new TidalIntegrationService(new TidalSettings(), new TidalApiClient(tidalHttp),
                    _ => Task.FromResult(new TidalTokenSet("test-only", "test-only", DateTimeOffset.UtcNow.AddHours(1), "collection.write", "test")));
                service.RestoreCachedCollection([]);
                type.GetField("_tidalIntegration", flags)!.SetValue(window, service);
            }
            if (sessionId == Spotify)
            {
                ((IDisposable)type.GetField("_spotifyIntegration", flags)!.GetValue(window)!).Dispose();
                var settings = new SpotifySettings { ClientId = "test", GrantedScope = SpotifyScopes.Requested };
                var service = new SpotifyIntegrationService(settings, spotifyHttp,
                    _ => Task.FromResult(new SpotifyTokenSet("test-only", "test-only", DateTimeOffset.UtcNow.AddHours(1), SpotifyScopes.Requested, "test")));
                type.GetField("_spotifyIntegration", flags)!.SetValue(window, service);
            }
            var result = new AccessibleMediaController.Windows.SearchWindow.SearchResult(sessionId, item);
            // Selection alone (also used by non-opening commands) must not save.
            type.GetMethod("SelectSearchResultBrowserItem", flags)!.Invoke(window, [result]);
            Check(tidalHandler.Mutations.Count == 0 && spotifyHandler.Writes.Count == 0 && !item.IsInLibrary && !item.IsFavorite,
                "Sam wybor wyniku zmienil Biblioteke: " + sessionId);
            type.GetField("_initialFocusApplied", flags)!.SetValue(window, true);
            window.ShowInTaskbar = false;
            window.Show();
            CompleteRealSearch(window, result);
            var expected = behavior == SearchResultEnterBehavior.AddToLibrary;
            bool Applied() => sessionId == Radio ? state.Radio.Stations.Any(s => s.Id == item.Id && s.IsInLibrary)
                : sessionId == Tidal ? session.Items.Any(i => i.ExternalId == item.ExternalId && i.IsInLibrary)
                : session.Items.Any(i => i.ExternalId == item.ExternalId && i.IsFavorite);
            if (expected) PumpUntil(Applied, TimeSpan.FromSeconds(5));
            else PumpUntil(() => true, TimeSpan.FromMilliseconds(1));
            Check(Applied() == expected, $"Enter {sessionId}/{behavior} nie dal oczekiwanego czlonkostwa w prawdziwym oknie.");
            Check(tidalHandler.Mutations.Count == (sessionId == Tidal && expected ? 1 : 0), "Bledna liczba mutacji TIDAL po Enter WPF");
            Check(spotifyHandler.Writes.Count == (sessionId == Spotify && expected ? 1 : 0), "Bledna liczba mutacji Spotify po Enter WPF");
            if (expected)
            {
                var live = session.Items.First(i => i.Id == item.Id);
                CompleteRealSearch(window, new AccessibleMediaController.Windows.SearchWindow.SearchResult(sessionId, live));
                Check(Applied(), "Ponowny Enter usunal zapisane czlonkostwo");
                Check(tidalHandler.Mutations.Count == (sessionId == Tidal ? 1 : 0) && spotifyHandler.Writes.Count == (sessionId == Spotify ? 1 : 0),
                    "Ponowny Enter wywolal dodatkowy zapis na koncie");
            }
            Console.WriteLine($"OK: prawdziwe ShowSearch -> Enter {sessionId}/{behavior}");
        }
        finally
        {
            window?.Close();
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private static void CompleteRealSearch(AccessibleMediaController.Windows.MainWindow window,
        AccessibleMediaController.Windows.SearchWindow.SearchResult result)
    {
        const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        Exception? callbackFailure = null;
        AccessibleMediaController.Windows.SearchWindow? dialog = null;
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Send)
            { Interval = TimeSpan.FromSeconds(8) };
        timer.Tick += (_, _) => { callbackFailure = new TimeoutException("Modal search did not complete"); dialog?.Close(); timer.Stop(); };
        dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                dialog = (AccessibleMediaController.Windows.SearchWindow?)typeof(AccessibleMediaController.Windows.MainWindow).GetField("_activeSearchWindow", f)!.GetValue(window)
                    ?? throw new Exception("ShowSearch did not create its real dialog");
                var dialogType = typeof(AccessibleMediaController.Windows.SearchWindow);
                var rowType = dialogType.GetNestedType("SearchResultRow", System.Reflection.BindingFlags.NonPublic)!;
                var row = Activator.CreateInstance(rowType, f, null,
                    [result.SessionId, result.Item, result.Item.Title, result.Item.Title, ""], null)!;
                var list = (System.Windows.Controls.ListBox)dialogType.GetField("ResultsList", f)!.GetValue(dialog)!;
                list.ItemsSource = new[] { row };
                list.SelectedIndex = 0;
                dialogType.GetMethod("CompleteSelected", f)!.Invoke(dialog, [AccessibleMediaController.Windows.SearchResultAction.Open]);
            }
            catch (Exception ex) { callbackFailure = ex; dialog?.Close(); }
        }));
        timer.Start();
        try { typeof(AccessibleMediaController.Windows.MainWindow).GetMethod("ShowSearch", f)!.Invoke(window, [false]); }
        finally { timer.Stop(); dialog?.Close(); }
        if (callbackFailure is not null) throw new Exception("Search completion", callbackFailure);
    }

    private static void PumpUntil(Func<bool> condition, TimeSpan limit)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var started = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            if (condition()) return;
            Thread.Sleep(5);
        } while (started.Elapsed < limit);
    }

    private const string Radio = OpenedSearchResultLibraryPolicy.RadioSessionId;
    private const string Tidal = OpenedSearchResultLibraryPolicy.TidalSessionId;
    private const string Spotify = SpotifyPlaybackSettingsResolver.SessionId;
    private const string Librespot = SpotifyPlaybackSettingsResolver.LibrespotSessionId;

    private static MediaItem Station() => new()
    {
        Id = "radio:catalog:one",
        Title = "Radio testowe",
        Kind = MediaItemKind.Station,
        Source = "http://example.invalid/stream",
        ExternalId = "catalog-one"
    };

    private static MediaItem TidalAlbum() => new()
    {
        Id = "tidal:albums:55",
        ExternalId = "albums:55",
        Title = "Album TIDAL",
        Kind = MediaItemKind.Album
    };

    private static MediaItem SpotifyTrack() => new()
    {
        Id = "spotify:track:77",
        ExternalId = "77",
        Source = "spotify:track:77",
        Title = "Utwor Spotify",
        Kind = MediaItemKind.Track
    };

    /// <summary>
    /// Punkt decyzji jest jeden dla wszystkich uslug i reaguje na ustawienie.
    /// Rownolegle sprawdzamy, ze przyszla usluga bez zapisu nie blokuje otwarcia.
    /// </summary>
    private static void TestPlanDlaObuTrybow()
    {
        foreach (var (sessionId, item, oczekiwany) in new (string, MediaItem, OpenedSearchResultLibraryPlan)[]
                 {
                     (Radio, Station(), OpenedSearchResultLibraryPlan.RadioStation),
                     (Tidal, TidalAlbum(), OpenedSearchResultLibraryPlan.TidalCollection),
                     (Spotify, SpotifyTrack(), OpenedSearchResultLibraryPlan.SpotifyCollection),
                     (Librespot, SpotifyTrack(), OpenedSearchResultLibraryPlan.SpotifyCollection)
                 })
        {
            Check(
                OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                    SearchResultEnterBehavior.OpenWithoutLibrary, sessionId, item)
                    == OpenedSearchResultLibraryPlan.None,
                $"Domyslne ustawienie dopisuje wynik uslugi {sessionId}.");
            Check(
                OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                    SearchResultEnterBehavior.AddToLibrary, sessionId, item) == oczekiwany,
                $"Tryb dodawania pomija usluge {sessionId}.");
        }

        // Przyszly dostawca: nieznana sesja nie wywraca otwarcia.
        Check(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary,
                "przyszlaUsluga",
                new MediaItem { Id = "x", Kind = MediaItemKind.Track, ExternalId = "x" })
                == OpenedSearchResultLibraryPlan.None,
            "Nieznana usluga probuje zapisu, ktorego AMC nie ma.");
        // Radio ma tylko stacje; odcinek w sesji radia to nie czlonkostwo Biblioteki.
        Check(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary,
                Radio,
                new MediaItem { Id = "radio:rec", Kind = MediaItemKind.Episode })
                == OpenedSearchResultLibraryPlan.None,
            "Nagranie w sesji radia zostalo uznane za stacje do Biblioteki.");
        // Rodzaj bez zapisu w Spotify (playlista) nie moze zablokowac otwarcia.
        Check(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary,
                Spotify,
                new MediaItem { Id = "p", Kind = MediaItemKind.Playlist, ExternalId = "p" })
                == OpenedSearchResultLibraryPlan.SpotifyCollection,
            "Playlista Spotify powinna trafic do sciezki zapisu, ktora zglosi granice uslugi.");
    }

    /// <summary>Automatyczne dodanie NIE jest przelacznikiem.</summary>
    private static void TestPlanNieJestPrzelacznikiem()
    {
        var stacja = Station();
        stacja.IsInLibrary = true;
        var album = TidalAlbum();
        TidalCollectionSemantics.ApplyMembership(album, true);
        var utwor = SpotifyTrack();
        SpotifyCollectionSemantics.ApplyMembership(utwor, true);
        foreach (var (sessionId, item) in new[] { (Radio, stacja), (Tidal, album), (Spotify, utwor) })
        {
            Check(
                OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                    SearchResultEnterBehavior.AddToLibrary, sessionId, item)
                    == OpenedSearchResultLibraryPlan.None,
                $"Powtorny Enter na zapisanym elemencie {sessionId} zdejmuje czlonkostwo.");
        }
    }

    /// <summary>
    /// Plan wykonuje sie LENIWIE: przy None zaden pisarz nie jest wolany, wiec
    /// polecenia, ktore nie otwieraja wyniku, nie moga nic dopisac.
    /// </summary>
    private static void TestPlanNieDotyczyNieotwierajacychPolecen()
    {
        var radio = 0;
        var tidal = 0;
        var spotify = 0;
        OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPlan.None,
            () => radio++,
            () => { tidal++; return Task.CompletedTask; },
            () => { spotify++; return Task.CompletedTask; }).GetAwaiter().GetResult();
        Check(radio == 0 && tidal == 0 && spotify == 0, "Plan None ruszyl pisarza uslugi.");
        OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPlan.RadioStation,
            () => radio++,
            () => { tidal++; return Task.CompletedTask; },
            () => { spotify++; return Task.CompletedTask; }).GetAwaiter().GetResult();
        Check(radio == 1 && tidal == 0 && spotify == 0, "Plan radia trafil do zlego pisarza.");
    }

    /// <summary>
    /// Radio: czlonkostwo zyje w stanie AMC. Przy OFF nie wolno go ustawic, przy
    /// ON wolno — i nigdy nie wolno go zdjac istniejacej stacji.
    /// </summary>
    private static void TestRadioObaTryby()
    {
        var wylaczone = Station();
        OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.OpenWithoutLibrary, Radio, wylaczone),
            () => wylaczone.IsInLibrary = true,
            () => Task.CompletedTask,
            () => Task.CompletedTask).GetAwaiter().GetResult();
        Check(!wylaczone.IsInLibrary, "Domyslny Enter dopisal stacje do Biblioteki radia.");

        var wlaczone = Station();
        OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary, Radio, wlaczone),
            () => wlaczone.IsInLibrary = true,
            () => Task.CompletedTask,
            () => Task.CompletedTask).GetAwaiter().GetResult();
        Check(wlaczone.IsInLibrary, "Tryb dodawania nie dopisal otwartej stacji radia.");
    }

    /// <summary>
    /// TIDAL na PRODUKCYJNYM pisarzu. Przy OFF atrapa nie widzi zadnej mutacji,
    /// przy ON widzi POST i album zostaje w Bibliotece.
    /// </summary>
    private static async Task TidalObaTryby()
    {
        using var handler = new TidalMembershipStub();
        using var http = new HttpClient(handler);
        using var integration = new TidalIntegrationService(
            new TidalSettings(),
            new TidalApiClient(http),
            _ => Task.FromResult(new TidalTokenSet(
                "tylko-test", "tylko-test", DateTimeOffset.UtcNow.AddHours(1), "collection.write", "test")));
        integration.RestoreCachedCollection([]);

        var wylaczone = TidalAlbum();
        await OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.OpenWithoutLibrary, Tidal, wylaczone),
            () => throw new Exception("TIDAL trafil do sciezki radia."),
            () => integration.ChangeCollectionMembershipAsync([wylaczone], true, CancellationToken.None),
            () => throw new Exception("TIDAL trafil do sciezki Spotify."));
        Check(handler.Mutations.Count == 0, "Domyslny Enter wyslal zapytanie zapisu do TIDAL.");
        Check(!wylaczone.IsInLibrary, "Domyslny Enter zmienil czlonkostwo TIDAL.");

        var wlaczone = TidalAlbum();
        await OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary, Tidal, wlaczone),
            () => throw new Exception("TIDAL trafil do sciezki radia."),
            () => integration.ChangeCollectionMembershipAsync([wlaczone], true, CancellationToken.None),
            () => throw new Exception("TIDAL trafil do sciezki Spotify."));
        Check(
            handler.Mutations.SequenceEqual([HttpMethod.Post]),
            $"Tryb dodawania nie zapisal albumu TIDAL; zadania: {string.Join(',', handler.Mutations)}.");
        Check(wlaczone.IsInLibrary, "Potwierdzone dodanie nie dotarlo do pozycji TIDAL.");

        // Juz zapisany: plan None, wiec pisarz nie dostaje DELETE.
        var mutacjePrzed = handler.Mutations.Count;
        await OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary, Tidal, wlaczone),
            () => throw new Exception("TIDAL trafil do sciezki radia."),
            () => integration.ChangeCollectionMembershipAsync([wlaczone], true, CancellationToken.None),
            () => throw new Exception("TIDAL trafil do sciezki Spotify."));
        Check(
            handler.Mutations.Count == mutacjePrzed && wlaczone.IsInLibrary,
            "Powtorny Enter na zapisanym albumie TIDAL ruszyl konto.");
    }

    /// <summary>
    /// Spotify na PRODUKCYJNYM pisarzu (SpotifyLibraryWriteClient przez serwis).
    /// Atrapa udaje /v1/me/library i potwierdzenie /contains.
    /// </summary>
    private static async Task SpotifyObaTryby()
    {
        using var handler = new SpotifyLibraryStub();
        using var http = new HttpClient(handler);
        var settings = new SpotifySettings { ClientId = "test", GrantedScope = SpotifyScopes.Requested };
        using var integration = new SpotifyIntegrationService(settings, http, _ => Task.FromResult(
            new SpotifyTokenSet(
                "tylko-test", "tylko-test", DateTimeOffset.UtcNow.AddHours(1), SpotifyScopes.Requested, "test")));

        var wylaczone = SpotifyTrack();
        await OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.OpenWithoutLibrary, Spotify, wylaczone),
            () => throw new Exception("Spotify trafil do sciezki radia."),
            () => throw new Exception("Spotify trafil do sciezki TIDAL."),
            async () =>
            {
                var wynik = await integration.ChangeCollectionMembershipAsync(
                    [wylaczone], true, CancellationToken.None);
                foreach (var item in wynik.ConfirmedItems)
                    SpotifyCollectionSemantics.ApplyMembership(item, wynik.Added);
            });
        Check(handler.Writes.Count == 0, "Domyslny Enter wyslal zapis do konta Spotify.");
        Check(!wylaczone.IsFavorite, "Domyslny Enter zmienil Ulubione Spotify.");

        var wlaczone = SpotifyTrack();
        await OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary, Librespot, wlaczone),
            () => throw new Exception("Spotify trafil do sciezki radia."),
            () => throw new Exception("Spotify trafil do sciezki TIDAL."),
            async () =>
            {
                var wynik = await integration.ChangeCollectionMembershipAsync(
                    [wlaczone], true, CancellationToken.None);
                foreach (var item in wynik.ConfirmedItems)
                    SpotifyCollectionSemantics.ApplyMembership(item, wynik.Added);
            });
        Check(
            handler.Writes.Count == 1 && handler.Writes[0].Method == HttpMethod.Put,
            $"Tryb dodawania nie zapisal utworu Spotify; zadania: {handler.Writes.Count}.");
        Check(handler.Membership.GetValueOrDefault("spotify:track:77"), "Konto Spotify nie ma dodanego utworu.");
        Check(wlaczone.IsFavorite, "Potwierdzone dodanie nie dotarlo do pozycji Spotify.");

        // Juz zapisany: zadnego DELETE.
        var zapisyPrzed = handler.Writes.Count;
        await OpenedSearchResultLibraryPolicy.ExecuteAsync(
            OpenedSearchResultLibraryPolicy.ResolveForOpenedResult(
                SearchResultEnterBehavior.AddToLibrary, Spotify, wlaczone),
            () => throw new Exception("Spotify trafil do sciezki radia."),
            () => throw new Exception("Spotify trafil do sciezki TIDAL."),
            () => integration.ChangeCollectionMembershipAsync([wlaczone], true, CancellationToken.None));
        Check(
            handler.Writes.Count == zapisyPrzed && wlaczone.IsFavorite,
            "Powtorny Enter na polubionym utworze Spotify ruszyl konto.");
    }

    /// <summary>
    /// Odmowa zgody (403) przy ON: pisarz zglasza blad, a lokalna flaga NIE
    /// klamie o zapisie. Otwarcie w oknie glownym dzieje sie osobno i nie jest
    /// tu blokowane — ta sciezka tylko nie wolno jej udawac sukcesu.
    /// </summary>
    private static async Task SpotifyOdmowaNieKlamieOZapisie()
    {
        using var handler = new SpotifyLibraryStub { WriteStatus = HttpStatusCode.Forbidden };
        using var http = new HttpClient(handler);
        var settings = new SpotifySettings { ClientId = "test", GrantedScope = SpotifyScopes.Requested };
        using var integration = new SpotifyIntegrationService(settings, http, _ => Task.FromResult(
            new SpotifyTokenSet(
                "tylko-test", "tylko-test", DateTimeOffset.UtcNow.AddHours(1), SpotifyScopes.Requested, "test")));
        var utwor = SpotifyTrack();
        var zgloszono = false;
        try
        {
            var wynik = await integration.ChangeCollectionMembershipAsync([utwor], true, CancellationToken.None);
            foreach (var item in wynik.ConfirmedItems)
                SpotifyCollectionSemantics.ApplyMembership(item, wynik.Added);
            Check(wynik.ConfirmedItems.Count == 0, "Odmowa 403 zostala policzona jako potwierdzony zapis.");
        }
        catch (SpotifyWriteBlockedException)
        {
            zgloszono = true;
        }
        Check(!utwor.IsFavorite, "Po odmowie 403 pozycja udaje zapisana w koncie.");
        Check(
            zgloszono || handler.Writes.Count > 0,
            "Odmowa nie zostala ani zgloszona, ani nawet sprobowana.");
    }

    private static void Check(bool warunek, string komunikat)
    {
        if (!warunek) throw new Exception($"Enter i Biblioteka: {komunikat}");
    }

    /// <summary>Atrapa transportu TIDAL: liczy tylko mutacje kolekcji.</summary>
    private sealed class TidalMembershipStub : HttpMessageHandler
    {
        public List<HttpMethod> Mutations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get) Mutations.Add(request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[]}")
            });
        }
    }

    /// <summary>
    /// Atrapa /v1/me/library: PUT/DELETE zmienia stan, GET /contains go zwraca.
    /// Ten sam kontrakt, na ktorym stoi SpotifyMembershipWriteTests.
    /// </summary>
    private sealed class SpotifyLibraryStub : HttpMessageHandler
    {
        public Dictionary<string, bool> Membership { get; } = new(StringComparer.Ordinal);
        public List<(HttpMethod Method, string Url)> Writes { get; } = [];
        public HttpStatusCode WriteStatus { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var uris = ParseUris(url);
            if (request.Method == HttpMethod.Get)
            {
                var tablica = uris.Select(uri => Membership.GetValueOrDefault(uri) ? "true" : "false");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"[{string.Join(',', tablica)}]")
                });
            }

            Writes.Add((request.Method, url));
            if (WriteStatus != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(WriteStatus));
            var add = request.Method == HttpMethod.Put;
            foreach (var uri in uris) Membership[uri] = add;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static string[] ParseUris(string url)
        {
            var index = url.IndexOf("uris=", StringComparison.Ordinal);
            if (index < 0) return [];
            return url[(index + 5)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();
        }
    }
}
