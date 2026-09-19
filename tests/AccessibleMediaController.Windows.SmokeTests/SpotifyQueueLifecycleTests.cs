using System.Reflection;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

/// <summary>
/// Cykl zycia kolejki Spotify na RZECZYWISTYM oknie glownym: zmiana kolejki
/// wykonana prawdziwym poleceniem musi przezyc zapis i restart tak, jak ja
/// zostawil uzytkownik - bez zmartwychwstania usunietej pozycji i bez powrotu
/// starej flagi "odtworz nastepne". Drugi watek: pierwsze odswiezenie
/// biblioteki po starcie BEZ cache nie moze wyrzucic zywej kolejki.
/// </summary>
internal static class SpotifyQueueLifecycleTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        UsunietaPozycjaNieWracaPoRestarcie();
        ZwyklaKolejkaNieWracaJakoOdtworzNastepne();
        PierwszaSynchronizacjaBezCacheZachowujeKolejke();
        Console.WriteLine("OK: cykl zycia kolejki Spotify (usuniecie, zmiana flagi, pierwsza synchronizacja bez cache)");
    }

    /// <summary>
    /// Stan po migracji: jedna pozycja w kolejce, zapisana i w katalogu.
    /// Uzytkownik usuwa ja prawdziwym poleceniem kolejki. Po zapisie i restarcie
    /// kolejka MUSI byc pusta - inaczej program przywraca usuniete.
    /// </summary>
    private static void UsunietaPozycjaNieWracaPoRestarcie()
    {
        var pierwszePrzejscie = true;
        WOknie(
            state => StanZKolejka(state, playNext: false),
            (window, _) =>
            {
                var session = Sesja(window);
                var item = session.Items.Single(kandydat => kandydat.ExternalId == "ONE");
                if (pierwszePrzejscie)
                {
                    if (!item.IsInQueue)
                        throw new Exception("Zapisana kolejka nie wrocila do zywej sesji przed zmiana.");
                    Polecenie(window, session, item, CommandIds.AddQueue);
                    if (item.IsInQueue || item.IsPlayNext)
                        throw new Exception("Polecenie kolejki nie usunelo pozycji w biezacej sesji.");
                    pierwszePrzejscie = false;
                    return;
                }
                if (item.IsInQueue || item.IsPlayNext)
                {
                    throw new Exception(
                        "Po zapisie i restarcie usunieta pozycja wrocila do kolejki "
                        + $"(IsInQueue={item.IsInQueue}, IsPlayNext={item.IsPlayNext}).");
                }
                if (session.Items.Any(kandydat => kandydat.IsInQueue || kandydat.IsPlayNext))
                    throw new Exception("Po restarcie kolejka Spotify nie jest pusta.");
            });
    }

    /// <summary>
    /// Stan po migracji: pozycja oznaczona "odtworz nastepne". Uzytkownik
    /// zamienia ja na zwykla kolejke. Po restarcie nie wolno przywrocic starej
    /// flagi odtwarzania nastepnego.
    /// </summary>
    private static void ZwyklaKolejkaNieWracaJakoOdtworzNastepne()
    {
        var pierwszePrzejscie = true;
        WOknie(
            state => StanZKolejka(state, playNext: true),
            (window, _) =>
            {
                var session = Sesja(window);
                var item = session.Items.Single(kandydat => kandydat.ExternalId == "ONE");
                if (pierwszePrzejscie)
                {
                    if (!item.IsPlayNext)
                        throw new Exception("Zapisana flaga odtworz nastepne nie wrocila do zywej sesji.");
                    // Prawdziwe polecenia: zdejmij "odtworz nastepne", dodaj zwykla kolejke.
                    Polecenie(window, session, item, CommandIds.TogglePlayNext);
                    if (item.IsPlayNext)
                        throw new Exception("Polecenie nie zdjelo flagi odtworz nastepne.");
                    if (!item.IsInQueue) Polecenie(window, session, item, CommandIds.AddQueue);
                    if (!item.IsInQueue || item.IsPlayNext)
                        throw new Exception("Po zmianie pozycja nie jest zwykla kolejka.");
                    pierwszePrzejscie = false;
                    return;
                }
                if (!item.IsInQueue)
                    throw new Exception("Po restarcie zniknela zwykla kolejka uzytkownika.");
                if (item.IsPlayNext)
                    throw new Exception("Po restarcie wrocila stara flaga odtworz nastepne.");
            });
    }

    /// <summary>
    /// Start BEZ cache biblioteki, z zapisana kolejka (typowy stan po migracji
    /// albo po czyszczeniu cache). Zywa kolejka juz jest w sesji, ale katalogu
    /// jeszcze nie pobrano. Pierwsze pobranie biblioteki o INNEJ tresci nie moze
    /// wyrzucic kolejki uzytkownika.
    /// </summary>
    private static void PierwszaSynchronizacjaBezCacheZachowujeKolejke()
    {
        var pierwszePrzejscie = true;
        WOknie(
            state =>
            {
                Podstawa(state);
                var queued = Utwor("spotify:track:OLD", "OLD");
                queued.IsInQueue = true;
                state.RemoteQueues.ItemsBySession["spotify"] =
                    [RemoteQueueItemSettings.FromMediaItem("spotify", queued)];
                state.CollectionOrders.QueueItemIdsBySession["spotify"] = ["spotify:track:OLD"];
                state.CollectionOrders.QueueRegularItemIdsBySession["spotify"] = ["spotify:track:OLD"];
            },
            (window, _) =>
            {
                if (!pierwszePrzejscie) return;
                pierwszePrzejscie = false;
                var session = Sesja(window);
                if (!session.Items.Any(item => item.ExternalId == "OLD" && item.IsInQueue))
                    throw new Exception("Start bez cache nie odtworzyl zapisanej kolejki do zywej sesji.");
                var biblioteka = new[] { Utwor("spotify:track:INNY", "INNY") };
                typeof(MainWindow).GetMethod("ApplySpotifyItems", Flags)!
                    .Invoke(window, [biblioteka, true]);
                session = Sesja(window);
                if (!session.Items.Any(item => item.ExternalId == "INNY"))
                    throw new Exception("Pierwsza synchronizacja nie wstawila pobranej biblioteki.");
                var wKolejce = session.Items.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
                if (wKolejce.Length != 1 || wKolejce[0].ExternalId != "OLD")
                {
                    throw new Exception(
                        "Pierwsza synchronizacja biblioteki bez cache wyrzucila zywa kolejke uzytkownika "
                        + $"(pozycji w kolejce: {wKolejce.Length}).");
                }
            });
    }

    /// <summary>
    /// Wykonuje POLECENIE aplikacji na wskazanej pozycji - ta sama sciezka co
    /// skrot klawiszowy uzytkownika, razem z zapisem stanu.
    /// </summary>
    private static void Polecenie(MainWindow window, DemoMediaSession session, MediaItem item, string commandId)
    {
        var sesje = Sesje(window);
        sesje.SelectSession(session.Id);
        session.SelectItem(item);
        var override_ = typeof(MainWindow).GetField("_actionItemsOverride", Flags)!;
        override_.SetValue(window, new[] { item });
        try
        {
            typeof(MainWindow).GetMethod("ExecuteCommand", Flags, null, [typeof(string)], null)!
                .Invoke(window, [commandId]);
        }
        finally
        {
            override_.SetValue(window, null);
        }
    }

    private static void Podstawa(PersistedState state)
    {
        state.Settings.Updates.CheckAutomatically = false;
        state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
        // Migracja starej sesji jest juz za nami: badamy zwykly cykl zapisu.
        state.Settings.SpotifySessionUnificationVersion = 1;
        state.Settings.LastSessionId = "spotify";
    }

    private static void StanZKolejka(PersistedState state, bool playNext)
    {
        Podstawa(state);
        var item = Utwor("spotify:track:ONE", "ONE");
        state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(item));
        var queued = Utwor("spotify:track:ONE", "ONE");
        queued.IsInQueue = !playNext;
        queued.IsPlayNext = playNext;
        state.RemoteQueues.ItemsBySession["spotify"] =
            [RemoteQueueItemSettings.FromMediaItem("spotify", queued)];
        state.CollectionOrders.QueueItemIdsBySession["spotify"] = ["spotify:track:ONE"];
        if (playNext)
            state.CollectionOrders.QueuePlayNextItemIdsBySession["spotify"] = ["spotify:track:ONE"];
        else
            state.CollectionOrders.QueueRegularItemIdsBySession["spotify"] = ["spotify:track:ONE"];
    }

    private static MediaItem Utwor(string id, string externalId) => new()
    {
        Id = id, ExternalId = externalId, Source = "spotify:track:" + externalId,
        Title = "Utwór " + externalId, Kind = MediaItemKind.Track
    };

    private static DemoMediaSession Sesja(MainWindow window) =>
        Sesje(window).FindSession("spotify")
        ?? throw new Exception("Brak kanonicznej sesji Spotify.");

    private static SessionManager Sesje(MainWindow window) =>
        (SessionManager)typeof(MainWindow).GetField("_sessions", Flags)!.GetValue(window)!;

    /// <summary>
    /// Rzeczywiste okno glowne w watku STA, dwa uruchomienia przez Save/Load
    /// prawdziwego magazynu konfiguracji. Bez ShowDialog i bez audio.
    /// </summary>
    private static void WOknie(Action<PersistedState> przygotuj, Action<MainWindow, PersistedState> sprawdz)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-spotify-queue-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                przygotuj(state);
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                var options = (System.Text.Json.JsonSerializerOptions)typeof(ConfigurationStore)
                    .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                File.WriteAllText(
                    Path.Combine(root, "state.json"),
                    System.Text.Json.JsonSerializer.Serialize(state, options));
                state = store.LoadOrCreate();
                state = store.LoadOrCreate();
                for (var restart = 0; restart < 2; restart++)
                {
                    window = new MainWindow(state, store);
                    sprawdz(window, state);
                    window.Close();
                    window = null;
                    if (restart == 0)
                    {
                        store.Save(state);
                        state = store.LoadOrCreate();
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                window?.Close();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60)))
            throw new Exception("Start okna nie zakonczyl testu w 60 s.");
        if (failure is not null)
            throw new Exception("Cykl zycia kolejki Spotify.", failure);
    }
}
