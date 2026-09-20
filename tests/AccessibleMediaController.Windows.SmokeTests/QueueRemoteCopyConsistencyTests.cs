using System.Reflection;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

/// <summary>
/// Druga kopia kolejki (RemoteQueues) musi opisywac DOKLADNIE ten sam stan co
/// mapy CollectionOrders policzone przez TransientQueuePersistence.Capture.
/// Mierzone jest RZECZYWISTE, prywatne <c>MainWindow.EnsureQueueOrder</c> na
/// prawdziwym oknie glownym, a nie powtorzona w tescie regula.
///
/// Sedno: jeden utwor TIDAL ma wiele wierszy o roznych Id (wiersz kolekcji
/// "tidal:tracks:123" i wiersz wpisu playlisty "tidal:tracks:123:entry:e9"),
/// wszystkie o tym samym kluczu magazynu. Uzytkownik moze nadac zwykla kolejke
/// JEDNEMU wierszowi, a priorytet "Odtworz jako nastepne" DRUGIEMU - filtr
/// kolejki przepuszcza wtedy oba. Snapshot liczy czlonkostwo z calej grupy, a
/// kopia RemoteQueues brala flagi samego reprezentanta: dwie kopie tego samego
/// faktu przeczyly sobie (PlayNext w CollectionOrders, IsPlayNext=false w
/// RemoteQueues). Sprawdzane sa OBA porzadki akcji, zapis/ponowny start oraz
/// celowe usuniecie pozycji.
///
/// Bez audio, bez sieci, bez GUI produkcyjnego: okno w watku STA, prawdziwy
/// ConfigurationStore w katalogu tymczasowym.
/// </summary>
internal static class QueueRemoteCopyConsistencyTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        // Oba porzadki rzeczywistych akcji uzytkownika daja ten sam stan.
        RozneWystapieniaZgodneZSnapshotem(playNextNaWpisiePlaylisty: true);
        RozneWystapieniaZgodneZSnapshotem(playNextNaWpisiePlaylisty: false);
        CeloweUsuniecieNieWracaPoPonownymStarcie();
        Console.WriteLine(
            "OK: druga kopia kolejki (RemoteQueues) zgodna ze snapshotem Capture "
            + "w obu porzadkach akcji, po zapisie i po usunieciu");
    }

    /// <summary>
    /// <paramref name="playNextNaWpisiePlaylisty"/> = true: AddQueue na wierszu
    /// kolekcji, potem TogglePlayNext na wpisie playlisty. False: odwrotnie -
    /// priorytet na wierszu kolekcji, zwykla kolejka na wpisie playlisty.
    /// Kazda kolejnosc musi dac zgodne obie kopie zapisu.
    /// </summary>
    private static void RozneWystapieniaZgodneZSnapshotem(bool playNextNaWpisiePlaylisty)
    {
        var opis = playNextNaWpisiePlaylisty
            ? "priorytet na wpisie playlisty"
            : "priorytet na wierszu kolekcji";
        WOknie(
            Podstawa,
            (window, state, przejscie) =>
            {
                var session = SesjaTidal(window);
                if (przejscie == 0)
                {
                    var kolekcja = Utwor("tidal:tracks:123", "tracks:123", "Utwor katalogowy");
                    var wpisPlaylisty = Utwor(
                        "tidal:tracks:123:entry:e9",
                        "tracks:123",
                        "Utwor katalogowy",
                        containerEntryId: "e9");
                    var inny = Utwor("tidal:tracks:777", "tracks:777", "Inny utwor");
                    session.ReplaceItems([kolekcja, wpisPlaylisty, inny]);

                    // Prawdziwe polecenia aplikacji przez CommandRouter, po
                    // jednym na KAZDYM wystapieniu - dokladnie tak, jak robi to
                    // uzytkownik z listy i z playlisty.
                    var doKolejki = playNextNaWpisiePlaylisty ? kolekcja : wpisPlaylisty;
                    var doPriorytetu = playNextNaWpisiePlaylisty ? wpisPlaylisty : kolekcja;
                    Polecenie(window, session, doKolejki, CommandIds.AddQueue);
                    Polecenie(window, session, inny, CommandIds.AddQueue);
                    Polecenie(window, session, doPriorytetu, CommandIds.TogglePlayNext);
                    if (!doKolejki.IsInQueue)
                        throw new Exception($"AddQueue nie ustawil zwyklej kolejki ({opis}).");
                    if (!doPriorytetu.IsPlayNext)
                        throw new Exception($"TogglePlayNext nie ustawil priorytetu ({opis}).");

                    // Rzeczywiste, prywatne EnsureQueueOrder - to ono zapisuje
                    // obie kopie stanu kolejki.
                    EnsureQueueOrder(window, session);
                    ZgodneKopie(state, opis + ", przed zapisem");
                    var kopia = RemoteQueue(state);
                    if (!kopia.TryGetValue("tidal:tracks:123", out var utwor))
                        throw new Exception($"Brak utworu w drugiej kopii kolejki ({opis}).");
                    if (!utwor.IsPlayNext)
                    {
                        throw new Exception(
                            "Priorytet nadany na jednym wystapieniu utworu nie trafil do drugiej kopii "
                            + $"kolejki ({opis}): RemoteQueues.IsPlayNext=false, choc snapshot Capture "
                            + "zapisal ten utwor jako PlayNext.");
                    }
                    if (!utwor.IsInQueue)
                    {
                        throw new Exception(
                            "Zwykla przynaleznosc nadana na drugim wystapieniu utworu nie trafila do "
                            + $"drugiej kopii kolejki ({opis}).");
                    }
                    if (kopia.Count != 2)
                    {
                        throw new Exception(
                            "Druga kopia kolejki musi miec jeden wpis na utwor (klucz magazynu) i nie wolno "
                            + $"jej filtrowac ani deduplikowac pozycji playlist; zmierzono {kopia.Count} ({opis}).");
                    }
                    return;
                }

                // Ponowny start: zapis wrocil z pliku i musi byc nadal zgodny.
                ZgodneKopie(state, opis + ", po zapisie i ponownym starcie");
                var poRestarcie = RemoteQueue(state);
                if (!poRestarcie.TryGetValue("tidal:tracks:123", out var wrocil) || !wrocil.IsPlayNext)
                {
                    throw new Exception(
                        $"Po zapisie i ponownym starcie priorytet zniknal z drugiej kopii kolejki ({opis}).");
                }
            });
    }

    /// <summary>
    /// Celowe zdjecie calosci z kolejki. Po zapisie i ponownym starcie pozycja
    /// nie moze sie odrodzic z drugiej kopii, a kopia nie moze zostac z
    /// nieaktualna flaga.
    /// </summary>
    private static void CeloweUsuniecieNieWracaPoPonownymStarcie()
    {
        WOknie(
            Podstawa,
            (window, state, przejscie) =>
            {
                var session = SesjaTidal(window);
                if (przejscie == 0)
                {
                    var kolekcja = Utwor("tidal:tracks:123", "tracks:123", "Utwor katalogowy");
                    var wpisPlaylisty = Utwor(
                        "tidal:tracks:123:entry:e9",
                        "tracks:123",
                        "Utwor katalogowy",
                        containerEntryId: "e9");
                    var inny = Utwor("tidal:tracks:777", "tracks:777", "Inny utwor");
                    session.ReplaceItems([kolekcja, wpisPlaylisty, inny]);
                    Polecenie(window, session, kolekcja, CommandIds.AddQueue);
                    Polecenie(window, session, wpisPlaylisty, CommandIds.TogglePlayNext);
                    Polecenie(window, session, inny, CommandIds.AddQueue);
                    EnsureQueueOrder(window, session);
                    if (RemoteQueue(state).Count != 2)
                        throw new Exception("Stan wyjsciowy usuniecia nie ma dwoch pozycji kolejki.");

                    // Zdejmujemy CALOSC utworu: zwykla kolejke z wiersza
                    // kolekcji i priorytet z wpisu playlisty.
                    Polecenie(window, session, kolekcja, CommandIds.AddQueue);
                    Polecenie(window, session, wpisPlaylisty, CommandIds.TogglePlayNext);
                    if (wpisPlaylisty.IsPlayNext) Polecenie(window, session, wpisPlaylisty, CommandIds.AddQueue);
                    EnsureQueueOrder(window, session);
                    ZgodneKopie(state, "po celowym usunieciu");
                    var kopia = RemoteQueue(state);
                    if (kopia.ContainsKey("tidal:tracks:123"))
                    {
                        throw new Exception(
                            "Celowo usuniety utwor zostal w drugiej kopii kolejki "
                            + $"(wpisy: {string.Join(", ", kopia.Keys)}).");
                    }
                    if (!kopia.ContainsKey("tidal:tracks:777"))
                        throw new Exception("Usuniecie jednej pozycji wyrzucilo z zapisu druga pozycje kolejki.");
                    return;
                }

                ZgodneKopie(state, "po usunieciu, zapisie i ponownym starcie");
                if (RemoteQueue(state).ContainsKey("tidal:tracks:123"))
                    throw new Exception("Po ponownym starcie usuniety utwor wrocil do drugiej kopii kolejki.");
            });
    }

    /// <summary>
    /// Rozstrzygajace porownanie: flagi w RemoteQueues MUSZA byc identyczne z
    /// mapami Regular/PlayNext policzonymi przez Capture i zapisanymi w
    /// CollectionOrders. Mapy sa tu jedynym zrodlem prawdy.
    /// </summary>
    private static void ZgodneKopie(PersistedState state, string etap)
    {
        var order = state.CollectionOrders.QueueItemIdsBySession.GetValueOrDefault("tidal") ?? [];
        var regular = (state.CollectionOrders.QueueRegularItemIdsBySession.GetValueOrDefault("tidal") ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var playNext = (state.CollectionOrders.QueuePlayNextItemIdsBySession.GetValueOrDefault("tidal") ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var kopia = RemoteQueue(state);
        var wKolejnosci = order
            .Where(id => regular.Contains(id) || playNext.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        if (!kopia.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(wKolejnosci))
        {
            throw new Exception(
                $"Druga kopia kolejki opisuje inne pozycje niz snapshot Capture ({etap}): "
                + $"RemoteQueues=[{string.Join(", ", kopia.Keys)}], "
                + $"CollectionOrders=[{string.Join(", ", wKolejnosci)}].");
        }
        foreach (var (id, item) in kopia)
        {
            if (item.IsInQueue != regular.Contains(id) || item.IsPlayNext != playNext.Contains(id))
            {
                throw new Exception(
                    $"Flagi w drugiej kopii kolejki przecza snapshotowi Capture ({etap}), pozycja {id}: "
                    + $"RemoteQueues(IsInQueue={item.IsInQueue}, IsPlayNext={item.IsPlayNext}) "
                    + $"vs snapshot(Regular={regular.Contains(id)}, PlayNext={playNext.Contains(id)}).");
            }
            if (!item.IsInQueue && !item.IsPlayNext)
            {
                throw new Exception(
                    $"Druga kopia kolejki trzyma pozycje bez zadnej flagi ({etap}), pozycja {id} - "
                    + "po ponownym starcie wrocilaby jako nieokreslona.");
            }
        }
    }

    private static Dictionary<string, RemoteQueueItemSettings> RemoteQueue(PersistedState state) =>
        (state.RemoteQueues.ItemsBySession.GetValueOrDefault("tidal") ?? [])
        .ToDictionary(item => item.Id, StringComparer.Ordinal);

    /// <summary>Rzeczywiste, prywatne <c>MainWindow.EnsureQueueOrder</c>.</summary>
    private static void EnsureQueueOrder(MainWindow window, DemoMediaSession session) =>
        typeof(MainWindow)
            .GetMethod("EnsureQueueOrder", Flags, null, [typeof(DemoMediaSession), typeof(IEnumerable<MediaItem>)], null)!
            .Invoke(window, [session, null]);

    /// <summary>
    /// Wykonuje POLECENIE aplikacji na wskazanym wystapieniu - ta sama sciezka
    /// co skrot klawiszowy uzytkownika (CommandRouter przez ExecuteCommand).
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
        state.Settings.LastSessionId = "tidal";
        state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
        state.Settings.SpotifySessionUnificationVersion = SpotifySessionMigration.Version;
        // Bez ClientId TIDAL integracja nie jest skonfigurowana: zadnego
        // logowania ani pobierania katalogu z sieci w tym tescie.
    }

    private static MediaItem Utwor(
        string id,
        string externalId,
        string title,
        string? containerEntryId = null) => new()
    {
        Id = id,
        ExternalId = externalId,
        ContainerEntryId = containerEntryId,
        Title = title,
        Kind = MediaItemKind.Track
    };

    private static DemoMediaSession SesjaTidal(MainWindow window) =>
        Sesje(window).FindSession("tidal") ?? throw new Exception("Brak sesji TIDAL w oknie glownym.");

    private static SessionManager Sesje(MainWindow window) =>
        (SessionManager)typeof(MainWindow).GetField("_sessions", Flags)!.GetValue(window)!;

    /// <summary>
    /// Rzeczywiste okno glowne w watku STA; dwa uruchomienia przez Save/Load
    /// prawdziwego magazynu konfiguracji. Bez ShowDialog, bez audio, bez sieci.
    /// </summary>
    private static void WOknie(
        Action<PersistedState> przygotuj,
        Action<MainWindow, PersistedState, int> sprawdz)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-queue-remote-copy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = ConfigurationStore.CreateDefaultState();
                przygotuj(state);
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                store.Save(state);
                for (var przejscie = 0; przejscie < 2; przejscie++)
                {
                    var doOkna = store.LoadOrCreate();
                    // Ponowny start sprawdzamy na stanie WPROST Z PLIKU. Nowe
                    // okno bez pobranego katalogu TIDAL (test jest bez sieci)
                    // normalizuje zywa kopie do pustej sesji, wiec dowodem
                    // trwalosci zapisu jest tresc pliku, nie stan po starcie.
                    var doSprawdzenia = przejscie == 0 ? doOkna : store.LoadOrCreate();
                    window = new MainWindow(doOkna, store);
                    sprawdz(window, doSprawdzenia, przejscie);
                    window.Close();
                    window = null;
                    if (przejscie == 0) store.Save(doOkna);
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
            throw new Exception("Start okna nie zakonczyl testu kolejki w 60 s.");
        if (failure is not null)
            throw new Exception("Zgodnosc drugiej kopii kolejki ze snapshotem Capture.", failure);
    }
}
