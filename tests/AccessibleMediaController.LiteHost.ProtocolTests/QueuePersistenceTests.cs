using System.Globalization;
using System.Text.Json;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>
/// TRWALOSC lokalnej kolejki: zapis -> zamkniecie wlasciciela -> NOWY
/// wlasciciel -> odczyt. Takze po zuzyciu kolejki DO ZERA.
///
/// Co te testy faktycznie mierza
/// -----------------------------
/// Kazdy przebieg idzie przez PRAWDZIWY plik SQLite o schemacie pelnego AMC
/// (ten sam DDL co <c>LocalLibraryDatabase.EnsureSchema</c>, z kolacja
/// <c>AMC_PL</c>) i przez PRAWDZIWY <see cref="LiteQueueStore"/>. "Restart
/// hosta" jest tu zamknieciem (<c>Dispose</c>) jednego magazynu i otwarciem
/// drugiego -- czyli dokladnie tym, co dzieje sie miedzy dwoma procesami hosta,
/// bo magazyn nie trzyma zadnego stanu poza plikiem i blokada.
///
/// Peleny przebieg DWOMA PROCESAMI (osobne .exe na kopii profilu) jest osobno,
/// w kwitach: ten plik mierzy kontrakt, nie zastepuje tamtego pomiaru.
///
/// Czego te testy NIE dowodza: ochrony przed starym AMC (WPF). Stary AMC nie
/// zna naszej blokady i nie zostalo to zmierzone -- dlatego zapis do profilu
/// produkcyjnego jest ODMAWIANY, co tez tu sprawdzamy.
/// </summary>
internal static class QueuePersistenceTests
{
    private sealed class SilentOutput : IMediaOutput
    {
        public List<(string Id, TimeSpan Position, int Volume, double Rate)> Plays { get; } = [];
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; set; }
        public bool SupportsPlaybackRate => true;

        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
        {
            Plays.Add((item.Id, position, volume, playbackRate));
            LoadedItemId = item.Id;
            Position = position;
        }

        public void Pause() { }
        public void Stop() => LoadedItemId = null;
        public void Seek(TimeSpan position) => Position = position;
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double playbackRate) { }
    }

    private static string _root = string.Empty;

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Json(string value) => JsonSerializer.Serialize(value);

    public static void Run()
    {
        _root = Path.Combine(
            Path.GetTempPath(), "amc-queue-persist-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
        try
        {
            ZapisPrzetrwaZamkniecieWlascicielaIOdczytWNowym();
            KolejnoscICzlonkostwoWracajaDokladnie();
            ZuzycieNaturalnymKoncemDoZeraNieOdradzaPozycji();
            ZuzyciePrzezNastepnyZapisujeUbytek();
            BrakZapisuToNIEPustaKolejka();
            DrugiWlascicielTejSamejKopiiJestODMOWIONY();
            PoZamknieciuWlascicielaKopiaZNOWUprzyjmujeWlasciciela();
            TrybTylkoOdczytuNIEUdajeZapisu();
            ZapisDoProfiluPRODUKCYJNEGOJestOdmowiony();
            ZapisIdzieTYLKOpoFaktycznejZmianie();
            BledneZadanieNIEJestZapisem();
            PozostaleTabeleIReszaProfiluZOSTAJACoDoRekordu();
            ObcaSesjaNIEJestResetowana();
            PozycjaKtorejNieMaWBibliotecNieWraca();
            Console.WriteLine("QueuePersistenceTests: OK");
        }
        finally
        {
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        }
    }

    // ---------------------------------------------------------------- profil

    /// <summary>
    /// Buduje PRAWDZIWA kopie profilu: baza o schemacie pelnego AMC z
    /// wypelnionymi tabelami, ktorych trwalosc kolejki NIE MA prawa ruszyc
    /// (Ulubione, zakladki, playlisty, historia, foldery). Dzieki temu pomiar
    /// PRZED/PO ma co porownywac.
    /// </summary>
    private static string NewProfile(string name, int trackCount = 6)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        var media = Path.Combine(directory, "media");
        Directory.CreateDirectory(media);

        var databasePath = Path.Combine(directory, LiteQueueStore.LibraryFileName);
        using var connection = Connect(databasePath);
        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = Schema;
            schema.ExecuteNonQuery();
        }

        for (var index = 0; index < trackCount; index++)
        {
            var letter = ((char)('A' + index)).ToString();
            var path = Path.Combine(media, letter + ".wav");
            File.WriteAllBytes(path, []);
            Execute(connection,
                """
                INSERT INTO local_items(
                    id, title, has_custom_title, path, duration_ticks, bitrate_estimated,
                    is_favorite, is_in_library, is_available, is_in_queue, is_play_next,
                    resume_mode, resume_position_ticks)
                VALUES($id, $title, 0, $path, 0, 0, $fav, 1, 1, 0, 0, 0, 0);
                """,
                ("$id", "file:" + letter), ("$title", letter + " utwor"),
                ("$path", path), ("$fav", index % 2));
        }

        // Dane, ktore MUSZA przezyc zapis kolejki.
        Execute(connection,
            "INSERT INTO metadata(key, value) VALUES('library_initialized', '1');");
        Execute(connection,
            """
            INSERT INTO folder_sources(id, ordinal, path, display_name, resume_mode)
            VALUES('src-1', 0, $path, 'Muzyka', 0);
            """, ("$path", media));
        Execute(connection,
            """
            INSERT INTO bookmarks(id, ordinal, session_id, session_name, item_id,
                item_title, name, position_ticks, created_utc_ticks)
            VALUES('bm-1', 0, 'local', 'Biblioteka', 'file:A', 'A utwor', 'Zakladka', 100, 200);
            """);
        Execute(connection,
            "INSERT INTO playlists(id, session_id, ordinal, name, created_utc_ticks) VALUES('pl-1', 'local', 0, 'Lista', 1);");
        Execute(connection,
            "INSERT INTO playlist_items(playlist_id, ordinal, item_id) VALUES('pl-1', 0, 'file:A');");
        Execute(connection,
            "INSERT INTO playback_history(session_id, ordinal, item_id) VALUES('local', 0, 'file:A');");
        Execute(connection,
            "INSERT INTO favorite_order(session_id, ordinal, item_id) VALUES('local', 0, 'file:A');");
        Execute(connection,
            "INSERT INTO library_added_order(session_id, ordinal, item_id) VALUES('local', 0, 'file:A');");
        Execute(connection,
            "INSERT INTO local_state(singleton, library_view, volume, playback_rate) VALUES(1, 'Wszystko', 0, 1.0);");
        return directory;
    }

    private static string MediaPath(string profile, string letter) =>
        Path.Combine(profile, "media", letter + ".wav");

    /// <summary>Wsad trzech wierszy B, A, C -- ta sama kolejnosc, co w kwitach kolejki.</summary>
    private static string ThreeRows(string profile) => $$"""
        {"sessionId":"local","items":[
          {"id":"file:B","title":"B utwor","path":{{Json(MediaPath(profile, "B"))}},"isInQueue":true},
          {"id":"file:A","title":"A utwor","path":{{Json(MediaPath(profile, "A"))}},"isInQueue":true},
          {"id":"file:C","title":"C utwor","path":{{Json(MediaPath(profile, "C"))}},"isInQueue":true}
        ],"order":["file:B","file:A","file:C"]}
        """;

    // ----------------------------------------------------------------- testy

    /// <summary>
    /// RDZEN zadania: zapis -> zamkniecie wlasciciela -> NOWY wlasciciel ->
    /// odczyt. Nowy host zastaje kolejke poprzednika.
    /// </summary>
    private static void ZapisPrzetrwaZamkniecieWlascicielaIOdczytWNowym()
    {
        var profile = NewProfile("cykl");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        Assert.True(queue.RestoredRows == 0, "czysty profil nie ma zapisanej kolejki");
        queue.Set(Args(ThreeRows(profile)));
        Assert.True(queue.PersistedWrites == 1,
            $"queue.set zapisal kolejke raz; bylo {queue.PersistedWrites}");
        Assert.True(queue.LastPersistError is null,
            "zapis na wlasnej kopii nie zglasza bledu: " + queue.LastPersistError);
        // Zamkniecie WLASCICIELA = koniec procesu hosta.
        first.Dispose();

        var second = LiteQueueStore.Open(profile, writable: true);
        using var reopened = second;
        var restored = new LiteQueueCoordinator(new SilentOutput(), second);
        Assert.True(restored.RestoredRows == 3,
            $"NOWY host wczytal 3 wiersze z profilu; bylo {restored.RestoredRows}");
        var status = restored.Status();
        Assert.True(status.Initialized, "nowy host raportuje kolejke jako WCZYTANA");
        Assert.True(status.Persistent, "nowy host jest wlascicielem zapisu");
        Assert.True(status.RestoredRows == 3, "status oddaje liczbe wczytanych wierszy");
    }

    /// <summary>
    /// Kolejnosc i CZLONKOSTWO (zwykle kontra priorytet) musza wrocic dokladnie:
    /// to jest prawdziwy kontrakt kolejki, nie sam zbior Id.
    /// </summary>
    private static void KolejnoscICzlonkostwoWracajaDokladnie()
    {
        var profile = NewProfile("czlonkostwo");
        var wsad = $$"""
            {"sessionId":"local","items":[
              {"id":"file:B","title":"B utwor","path":{{Json(MediaPath(profile, "B"))}},"isInQueue":true},
              {"id":"file:D","title":"D utwor","path":{{Json(MediaPath(profile, "D"))}},"isPlayNext":true},
              {"id":"file:A","title":"A utwor","path":{{Json(MediaPath(profile, "A"))}},"isInQueue":true}
            ],"order":["file:D","file:B","file:A"]}
            """;

        var first = LiteQueueStore.Open(profile, writable: true);
        new LiteQueueCoordinator(new SilentOutput(), first).Set(Args(wsad));
        first.Dispose();

        using var second = LiteQueueStore.Open(profile, writable: false);
        var stored = second.Read(LiteQueueStore.LocalSessionId);
        Assert.True(stored.Saved, "profil wie, ze kolejka byla zapisana");
        Assert.Equal("file:D,file:B,file:A",
            string.Join(",", stored.Rows.Select(row => row.Id)),
            "kolejnosc wrocila dokladnie");
        var playNext = stored.Rows.Where(row => row.IsPlayNext).Select(row => row.Id).ToArray();
        Assert.Equal("file:D", string.Join(",", playNext), "priorytet wrocil na D");
        var regular = stored.Rows.Where(row => row.IsInQueue && !row.IsPlayNext)
            .Select(row => row.Id).ToArray();
        Assert.Equal("file:B,file:A", string.Join(",", regular), "zwykle czlonkostwo wrocilo");
        Assert.Equal("B utwor",
            stored.Rows.Single(row => row.Id == "file:B").Title,
            "tytul bierze sie z Biblioteki w profilu");
    }

    /// <summary>
    /// ZUZYCIE DO ZERA naturalnym koncem. Po RESTARCIE PROCESU kolejka zostaje
    /// pusta: zuzyte pozycje NIE WRACAJA. To byl wlasnie wymagany przypadek.
    /// </summary>
    private static void ZuzycieNaturalnymKoncemDoZeraNieOdradzaPozycji()
    {
        var profile = NewProfile("do-zera");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));

        // Naturalny koniec kazdego z trzech utworow -- az do zera.
        Assert.True(queue.HandlePlaybackEnded("file:B")?.Id == "file:A", "po B idzie A");
        Assert.True(queue.HandlePlaybackEnded("file:A")?.Id == "file:C", "po A idzie C");
        Assert.True(queue.HandlePlaybackEnded("file:C") is null, "po C kolejka jest wyczerpana");

        var afterDrain = queue.Status();
        Assert.True(afterDrain.Rows.Count == 0,
            $"zywa kolejka zeszla do zera; zostalo {afterDrain.Rows.Count}");
        Assert.True(afterDrain.PersistedWrites >= 2,
            $"zuzycie zapisalo sie w profilu; zapisow bylo {afterDrain.PersistedWrites}");
        first.Dispose();

        // RESTART: nowy magazyn, nowy koordynator, ten sam profil.
        var second = LiteQueueStore.Open(profile, writable: true);
        using var reopened = second;
        var stored = second.Read(LiteQueueStore.LocalSessionId);
        Assert.True(stored.Saved,
            "profil pamieta, ze kolejka ZOSTALA zapisana jako pusta (a nie ze nic nie zapisano)");
        Assert.True(stored.Rows.Count == 0,
            $"po restarcie kolejka jest pusta; wrocilo {stored.Rows.Count} pozycji");

        var restored = new LiteQueueCoordinator(new SilentOutput(), second);
        Assert.True(restored.RestoredRows == 0, "nowy host nie odrodzil zuzytych pozycji");
        var status = restored.Status();
        Assert.True(status.Initialized,
            "pusta kolejka po zuzyciu jest WCZYTANA, nie 'brak kolejki': inaczej frontend "
            + "mialby prawo wstawic stare pozycje z ekranu");
        Assert.True(status.Rows.Count == 0, "status po restarcie nie ma wierszy");

        // Flagi w Bibliotece tez musza byc zdjete -- inaczej pelne AMC
        // policzyloby te utwory jako nadal w kolejce.
        using var connection = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName));
        Assert.True(Scalar(connection, "SELECT COUNT(*) FROM local_items WHERE is_in_queue <> 0 OR is_play_next <> 0;") == 0,
            "zadna pozycja Biblioteki nie zostala oznaczona jako w kolejce");
    }

    /// <summary>
    /// Zuzycie przez Nastepny (nie tylko przez naturalny koniec) tez schodzi do
    /// profilu: po restarcie zdjeta pozycja nie wraca.
    /// </summary>
    private static void ZuzyciePrzezNastepnyZapisujeUbytek()
    {
        var profile = NewProfile("nastepny");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        Assert.True(queue.PlayRelative(1), "Nastepny przesuwa kolejke");
        first.Dispose();

        using var second = LiteQueueStore.Open(profile, writable: false);
        var stored = second.Read(LiteQueueStore.LocalSessionId);
        Assert.True(stored.Rows.Count < 3,
            $"zuzyta pozycja zniknela z profilu; nadal jest {stored.Rows.Count} z 3");
        Assert.True(stored.Rows.All(row => row.Id != "file:B"),
            "zuzyte B nie wraca po restarcie");
    }

    /// <summary>
    /// BRAK zapisu to NIE pusta kolejka. Bez tego rozroznienia swiezy profil
    /// wygladalby jak kolejka zuzyta do zera i frontend nie moglby wczytac
    /// wlasnej listy.
    /// </summary>
    private static void BrakZapisuToNIEPustaKolejka()
    {
        var profile = NewProfile("bez-zapisu");
        using var store = LiteQueueStore.Open(profile, writable: false);
        var stored = store.Read(LiteQueueStore.LocalSessionId);
        Assert.True(!stored.Saved, "swiezy profil nie zglasza zapisanej kolejki");

        var queue = new LiteQueueCoordinator(new SilentOutput(), store);
        Assert.True(queue.RestoredRows == 0, "nie ma czego wczytac");
        Assert.True(!queue.Status().Initialized,
            "host bez zapisanej kolejki zostaje NIEWCZYTANY -- frontend ma prawo podac swoja liste");
    }

    /// <summary>
    /// DWA wlascicieli tej samej kopii: drugi dostaje ODMOWE, nie cichy sukces.
    /// </summary>
    private static void DrugiWlascicielTejSamejKopiiJestODMOWIONY()
    {
        var profile = NewProfile("dwa-hosty");
        using var first = LiteQueueStore.Open(profile, writable: true);
        Assert.True(first.OwnsWriteLock, "pierwszy host trzyma blokade wlasnosci");

        var denied = false;
        string message = string.Empty;
        try
        {
            using var second = LiteQueueStore.Open(profile, writable: true);
        }
        catch (LiteQueueStoreDenied exception)
        {
            denied = true;
            message = exception.Message;
        }
        Assert.True(denied, "DRUGI host na tej samej kopii dostaje odmowe zapisu");
        Assert.True(message.Contains("wlasciciela", StringComparison.OrdinalIgnoreCase),
            "odmowa mowi o wlascicielu zapisu, a nie o anonimowym bledzie: " + message);

        // Odczyt rownolegly jest nadal dozwolony -- Python czyta ten sam profil.
        using var reader = LiteQueueStore.Open(profile, writable: false);
        Assert.True(!reader.IsWritable, "rownolegly czytelnik nie dostaje prawa zapisu");
    }

    /// <summary>
    /// Blokada MUSI padac z hostem. Inaczej trwalosc dzialalaby raz, a kazdy
    /// kolejny host dostawalby odmowe po juz zamknietym poprzedniku.
    /// </summary>
    private static void PoZamknieciuWlascicielaKopiaZNOWUprzyjmujeWlasciciela()
    {
        var profile = NewProfile("blokada-wraca");
        var first = LiteQueueStore.Open(profile, writable: true);
        first.Dispose();
        using var second = LiteQueueStore.Open(profile, writable: true);
        Assert.True(second.OwnsWriteLock,
            "po zamknieciu poprzednika NOWY host obejmuje wlasnosc zapisu");
        var third = LiteQueueStore.Open(profile, writable: false);
        third.Dispose();
        Assert.True(second.OwnsWriteLock,
            "zamkniecie CZYTELNIKA nie zabiera blokady wlascicielowi");
    }

    /// <summary>
    /// Tryb tylko do odczytu NIE MOZE skonczyc sie komunikatem "zapisano".
    /// To jest ta sama zasada, co dla wspolnego profilu: read-only z defaultu.
    /// </summary>
    private static void TrybTylkoOdczytuNIEUdajeZapisu()
    {
        var profile = NewProfile("tylko-odczyt");
        using var store = LiteQueueStore.Open(profile, writable: false);
        Assert.True(!store.IsWritable, "domyslny tryb to ODCZYT");
        Assert.True(!store.OwnsWriteLock, "czytelnik nie trzyma blokady wlasnosci");

        var denied = false;
        try
        {
            store.Write(LiteQueueStore.LocalSessionId,
                new QueuePersistenceSnapshot(["file:A"], ["file:A"], [], ["file:A"]));
        }
        catch (LiteQueueStoreDenied)
        {
            denied = true;
        }
        Assert.True(denied, "zapis w trybie odczytu leci ODMOWA, nie cichym powodzeniem");

        // Koordynator na magazynie tylko do odczytu: kolejka dziala, ale stan
        // JAWNIE mowi, ze nie jest trwala i podaje powod.
        var queue = new LiteQueueCoordinator(new SilentOutput(), store);
        queue.Set(Args(ThreeRows(profile)));
        var status = queue.Status();
        Assert.True(status.Rows.Count == 3, "kolejka dziala w pamieci takze bez trwalosci");
        Assert.True(!status.Persistent, "status NIE twierdzi trwalosci w trybie odczytu");
        Assert.True(status.PersistError is not null,
            "odmowa zapisu jest widoczna w stanie, a nie zglaszana jako zapisano");
        Assert.True(status.PersistedWrites == 0, "zaden zapis nie doszedl");

        using var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName));
        Assert.True(Scalar(verify, "SELECT COUNT(*) FROM queue_order;") == 0,
            "baza NIE dostala ani jednego wiersza kolejki w trybie odczytu");
    }

    /// <summary>
    /// Zapis do PRODUKCYJNEGO profilu AMC jest odmawiany zawsze. Stary WPF nie
    /// zna naszej blokady, wiec ochrona miedzy nimi nie zostala zmierzona i nie
    /// wolno jej ogloszac.
    /// </summary>
    private static void ZapisDoProfiluPRODUKCYJNEGOJestOdmowiony()
    {
        var production = LiteQueueStore.ProductionProfileDirectory;
        if (production.Length == 0 || !Directory.Exists(production)
            || !File.Exists(Path.Combine(production, LiteQueueStore.LibraryFileName)))
        {
            // Na tej maszynie nie ma profilu produkcyjnego -- nie udajemy pomiaru.
            Console.WriteLine(
                "  (pominieto: brak profilu produkcyjnego na tej maszynie -- "
                + "sprawdzenie odmowy wymaga jego istnienia)");
            return;
        }

        var denied = false;
        string message = string.Empty;
        try
        {
            using var store = LiteQueueStore.Open(production, writable: true);
        }
        catch (LiteQueueStoreDenied exception)
        {
            denied = true;
            message = exception.Message;
        }
        Assert.True(denied, "zapis do profilu PRODUKCYJNEGO jest odmowiony");
        Assert.True(message.Contains("PRODUKCYJNEGO", StringComparison.Ordinal),
            "odmowa nazywa powod wprost: " + message);

        // ODCZYT produkcyjnego profilu jest dozwolony i nie zostawia blokady.
        using var reader = LiteQueueStore.Open(production, writable: false);
        Assert.True(!reader.OwnsWriteLock, "odczyt produkcyjnego profilu nie zaklada blokady");
        Assert.True(!File.Exists(Path.Combine(production, LiteQueueStore.OwnerLockFileName)),
            "odczyt NIE zostawia pliku blokady w produkcyjnym profilu");
    }

    /// <summary>
    /// Zapis automatyczny TYLKO po faktycznej zmianie. Inaczej kazde
    /// <c>queue.set</c> z ta sama lista pisaloby po bazie bez powodu.
    /// </summary>
    private static void ZapisIdzieTYLKOpoFaktycznejZmianie()
    {
        var profile = NewProfile("bez-zmiany");
        using var store = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), store);

        queue.Set(Args(ThreeRows(profile)));
        Assert.True(queue.PersistedWrites == 1, "pierwszy wsad to jeden zapis");

        queue.Set(Args(ThreeRows(profile)));
        Assert.True(queue.PersistedWrites == 1,
            $"TEN SAM stan nie pisze po bazie drugi raz; zapisow {queue.PersistedWrites}");
        Assert.True(queue.PersistSkipped >= 1, "pominiecie zapisu jest policzone");

        // Zmiana kolejnosci to JUZ zmiana -- musi sie zapisac.
        queue.Set(Args($$"""
            {"sessionId":"local","items":[
              {"id":"file:A","title":"A utwor","path":{{Json(MediaPath(profile, "A"))}},"isInQueue":true},
              {"id":"file:B","title":"B utwor","path":{{Json(MediaPath(profile, "B"))}},"isInQueue":true}
            ],"order":["file:A","file:B"]}
            """));
        Assert.True(queue.PersistedWrites == 2,
            $"zmieniona kolejka zapisuje sie; zapisow {queue.PersistedWrites}");
    }

    /// <summary>
    /// ODRZUCONE zadanie nie zapisuje niczego. Inaczej blad wejscia kasowalby
    /// poprawna kolejke w profilu.
    /// </summary>
    private static void BledneZadanieNIEJestZapisem()
    {
        var profile = NewProfile("blad-wsadu");
        using var store = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), store);
        queue.Set(Args(ThreeRows(profile)));
        var before = queue.PersistedWrites;

        var rejected = false;
        try
        {
            // Wiersz bez Id jest odrzucany przez koordynator.
            queue.Set(Args("""{"sessionId":"local","items":[{"title":"bez id"}]}"""));
        }
        catch (LiteRequestException)
        {
            rejected = true;
        }
        Assert.True(rejected, "wsad bez Id jest odrzucony");
        Assert.True(queue.PersistedWrites == before,
            "odrzucone zadanie NIE zapisalo nic do profilu");

        using var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName));
        Assert.True(Scalar(verify, "SELECT COUNT(*) FROM queue_order WHERE session_id = 'local';") == 3,
            "w profilu nadal stoi poprawna, trzywierszowa kolejka");
    }

    /// <summary>
    /// POMIAR PRZED/PO: zadna inna tabela profilu nie traci rekordow. Zapis
    /// calego AppState z mala lista z frontendu skasowalby tu Biblioteke,
    /// Ulubione, zakladki i playlisty -- dlatego go nie ma.
    /// </summary>
    private static void PozostaleTabeleIReszaProfiluZOSTAJACoDoRekordu()
    {
        var profile = NewProfile("przed-po");
        var databasePath = Path.Combine(profile, LiteQueueStore.LibraryFileName);

        IReadOnlyDictionary<string, long> before;
        using (var probe = LiteQueueStore.Open(profile, writable: false))
        {
            before = probe.CountUntouchedTables();
        }
        Assert.True(before["local_items"] == 6, "profil startuje z 6 utworami");
        Assert.True(before["bookmarks"] == 1 && before["playlists"] == 1
            && before["playlist_items"] == 1 && before["playback_history"] == 1,
            "profil startuje z zakladka, playlista i historia");

        var store = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), store);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.HandlePlaybackEnded("file:B");
        queue.HandlePlaybackEnded("file:A");
        queue.HandlePlaybackEnded("file:C");
        store.Dispose();

        using var after = LiteQueueStore.Open(profile, writable: false);
        var counts = after.CountUntouchedTables();
        foreach (var (table, value) in before)
        {
            Assert.True(counts[table] == value,
                $"tabela {table} ma tyle samo rekordow co przed zapisem kolejki "
                + $"(bylo {value}, jest {counts[table]})");
        }

        // local_state tez nie moze zostac przepisane: 0 glosnosci jest JAWNA
        // nastawa uzytkownika, a nie wartoscia do zgubienia.
        using var connection = Connect(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT library_view, volume FROM local_state WHERE singleton = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), "local_state nadal ma swoj wiersz");
        Assert.Equal("Wszystko", reader.GetString(0), "widok Biblioteki nietkniety");
        Assert.True(reader.GetInt32(1) == 0, "glosnosc 0 w profilu przetrwala zapis kolejki");
    }

    /// <summary>
    /// Kolejka INNEJ sesji nie jest resetowana: zapis dotyka tylko wierszy
    /// sesji lokalnej.
    /// </summary>
    private static void ObcaSesjaNIEJestResetowana()
    {
        var profile = NewProfile("obca-sesja");
        var databasePath = Path.Combine(profile, LiteQueueStore.LibraryFileName);
        using (var seed = Connect(databasePath))
        {
            Execute(seed,
                "INSERT INTO queue_order(session_id, ordinal, item_id) VALUES('tidal', 0, 'tidal-1');");
            Execute(seed,
                "INSERT INTO queue_regular_order(session_id, ordinal, item_id) VALUES('tidal', 0, 'tidal-1');");
        }

        var store = LiteQueueStore.Open(profile, writable: true);
        new LiteQueueCoordinator(new SilentOutput(), store).Set(Args(ThreeRows(profile)));
        store.Dispose();

        using var verify = Connect(databasePath);
        Assert.True(Scalar(verify, "SELECT COUNT(*) FROM queue_order WHERE session_id = 'tidal';") == 1,
            "kolejka sesji tidal zostala nietknieta");
        Assert.True(Scalar(verify, "SELECT COUNT(*) FROM queue_order WHERE session_id = 'local';") == 3,
            "kolejka lokalna zapisala sie obok");

        // Zadanie trwalosci dla obcej sesji jest ODMOWIONE wprost.
        using var reader = LiteQueueStore.Open(profile, writable: false);
        var denied = false;
        try { reader.Read("tidal"); } catch (LiteQueueStoreDenied) { denied = true; }
        Assert.True(denied, "trwalosc obcej sesji jest odmawiana, a nie cicho obslugiwana");
    }

    /// <summary>
    /// Pozycja usunieta z Biblioteki nie wraca do kolejki tylko dlatego, ze
    /// stoi w zapisanej kolejnosci.
    /// </summary>
    private static void PozycjaKtorejNieMaWBibliotecNieWraca()
    {
        var profile = NewProfile("znikla-pozycja");
        var databasePath = Path.Combine(profile, LiteQueueStore.LibraryFileName);

        var first = LiteQueueStore.Open(profile, writable: true);
        new LiteQueueCoordinator(new SilentOutput(), first).Set(Args(ThreeRows(profile)));
        first.Dispose();

        using (var mutate = Connect(databasePath))
        {
            Execute(mutate, "DELETE FROM local_items WHERE id = 'file:A';");
        }

        using var second = LiteQueueStore.Open(profile, writable: false);
        var stored = second.Read(LiteQueueStore.LocalSessionId);
        Assert.Equal("file:B,file:C",
            string.Join(",", stored.Rows.Select(row => row.Id)),
            "pozycja bez wiersza w Bibliotece nie wraca, reszta kolejnosci zostaje");
    }

    // ------------------------------------------------------------- narzedzia

    private static SqliteConnection Connect(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        // Kolacja AMC_PL jest w DDL pelnego AMC: bez jej rejestracji SQLite
        // odmawia nawet INSERTow na kolumnach, ktore jej uzywaja.
        connection.CreateCollation(
            "AMC_PL",
            (left, right) => CultureInfo.GetCultureInfo("pl-PL").CompareInfo.Compare(
                left, right, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static void Execute(
        SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Schemat PRZEPISANY z <c>LocalLibraryDatabase.EnsureSchema</c> (tabele,
    /// ktore dotyczy trwalosc kolejki i pomiar PRZED/PO). Nie wolamy tam
    /// prywatnej metody, ale kolumny i kolacje sa te same -- gdyby sie
    /// rozjechaly, testy zapisu padna na SQL, a nie przejda po cichu.
    /// </summary>
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS metadata (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS local_items (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL COLLATE AMC_PL,
            has_custom_title INTEGER NOT NULL,
            path TEXT NOT NULL,
            duration_ticks INTEGER NOT NULL,
            bitrate_kbps INTEGER NULL,
            bitrate_estimated INTEGER NOT NULL,
            sample_rate_hz INTEGER NULL,
            is_favorite INTEGER NOT NULL,
            is_in_library INTEGER NOT NULL,
            is_available INTEGER NOT NULL,
            is_in_queue INTEGER NOT NULL,
            is_play_next INTEGER NOT NULL,
            resume_mode INTEGER NOT NULL,
            playback_rate_override REAL NULL,
            output_device_id TEXT NULL,
            resume_position_ticks INTEGER NOT NULL,
            file_length INTEGER NULL,
            last_write_utc_ticks INTEGER NULL
        );
        CREATE TABLE IF NOT EXISTS folder_sources (
            id TEXT PRIMARY KEY,
            ordinal INTEGER NOT NULL,
            path TEXT NOT NULL,
            display_name TEXT NOT NULL COLLATE AMC_PL,
            resume_mode INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS folder_playback_options (
            ordinal INTEGER NOT NULL,
            path TEXT PRIMARY KEY,
            resume_mode INTEGER NOT NULL,
            playback_rate_override REAL NULL,
            output_device_id TEXT NULL
        );
        CREATE TABLE IF NOT EXISTS excluded_paths (
            ordinal INTEGER PRIMARY KEY,
            path TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS custom_order (
            ordinal INTEGER PRIMARY KEY,
            item_id TEXT NOT NULL UNIQUE
        );
        CREATE TABLE IF NOT EXISTS local_state (
            singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
            library_view TEXT NOT NULL,
            current_folder_path TEXT NULL,
            current_item_id TEXT NULL,
            volume INTEGER NOT NULL,
            playback_rate REAL NOT NULL
        );
        CREATE TABLE IF NOT EXISTS bookmarks (
            id TEXT PRIMARY KEY,
            ordinal INTEGER NOT NULL,
            session_id TEXT NOT NULL,
            session_name TEXT NOT NULL,
            item_id TEXT NOT NULL,
            item_title TEXT NOT NULL COLLATE AMC_PL,
            name TEXT NOT NULL COLLATE AMC_PL,
            position_ticks INTEGER NOT NULL,
            created_utc_ticks INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS playback_history (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS favorite_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS favorite_added_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS library_added_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS library_custom_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS queue_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS queue_play_next_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS queue_regular_order (
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(session_id, ordinal)
        );
        CREATE TABLE IF NOT EXISTS playlists (
            id TEXT PRIMARY KEY,
            session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            name TEXT NOT NULL COLLATE AMC_PL,
            created_utc_ticks INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS playlist_items (
            playlist_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL,
            PRIMARY KEY(playlist_id, ordinal),
            UNIQUE(playlist_id, item_id),
            FOREIGN KEY(playlist_id) REFERENCES playlists(id) ON DELETE CASCADE
        );
        """;
}
