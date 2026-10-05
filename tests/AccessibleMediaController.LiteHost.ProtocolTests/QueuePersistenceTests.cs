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
            BiezacyUtworIPozycjaWRACAJAdoNOWEGOhosta();
            WznowienieGRAodZAPISANEGOniezerowegoCzasu();
            SamoWczytanieNIEodtwarzaNICZEGO();
            UstawienieZAWSZEodPOCZATKUnieWznawia();
            ZuzytaPUSTAkolejkaNIEwracaDoOstatniegoUtworu();
            WyjscieDoBEZPOSREDNIEGOplikuNIEprzypisujePozycjiInnemuId();
            PodmienionyPLIKpodTYMSAMYMIdNIEwznawia();
            GLOBALNEwylaczenieWznawianiaObowiazuje();
            STATUSpodajeCzasWznowieniaDlaOkna();
            CheckpointPozycjiNIEruszaPOZOSTALYCHtabel();
            OdmowaZapisuPozycjiNIEudajePowodzenia();
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

    // ------------------------------------------- WZNOWIENIE: utwor i pozycja

    /// <summary>
    /// RDZEN tego przyrostu: biezacy lokalny utwor i jego POZYCJA CZASU
    /// przezywaja zamkniecie wlasciciela i wracaja w NOWYM hoscie.
    ///
    /// Dane ida do PRAWDZIWYCH kolumn schematu pelnego AMC
    /// (<c>local_state.current_item_id</c>, <c>local_items.resume_position_ticks</c>),
    /// a nie do bocznego pliku: dokladnie tam, gdzie je trzyma i czyta
    /// <c>LocalLibraryDatabase</c>.
    /// </summary>
    private static void BiezacyUtworIPozycjaWRACAJAdoNOWEGOhosta()
    {
        var profile = NewProfile("wznowienie-cykl");
        // Ulamek sekundy jest tu celowy: zapis w SEKUNDACH zgubilby go i test
        // by to zobaczyl. AMC trzyma TICKI.
        var position = TimeSpan.FromMilliseconds(7_480);

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.NotePosition(position);
        Assert.True(queue.SaveResumeCheckpoint(), "checkpoint pozycji zapisal sie");
        first.Dispose();

        // Kolumny w bazie, a nie nasz wlasny plik obok.
        using (var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName)))
        {
            using var command = verify.CreateCommand();
            command.CommandText =
                "SELECT current_item_id FROM local_state WHERE singleton = 1;";
            Assert.Equal("file:B", (string?)command.ExecuteScalar() ?? "(null)",
                "local_state.current_item_id wskazuje biezacy utwor");
            Assert.True(
                Scalar(verify, "SELECT resume_position_ticks FROM local_items WHERE id = 'file:B';")
                    == position.Ticks,
                "resume_position_ticks trzyma DOKLADNA pozycje w tickach");
        }

        var second = LiteQueueStore.Open(profile, writable: true);
        using var reopened = second;
        var restored = new LiteQueueCoordinator(new SilentOutput(), second);
        Assert.True(restored.RestoredRows == 3, "nowy host wczytal kolejke");
        var status = restored.Status();
        Assert.Equal("file:B", status.CurrentId ?? "(null)",
            "NOWY host zna biezacy utwor poprzednika");
        Assert.True(Math.Abs(status.PositionSeconds - position.TotalSeconds) < 0.001d,
            $"NOWY host zna NIEZEROWA pozycje; podal {status.PositionSeconds}");
    }

    /// <summary>
    /// SWIADOME wznowienie w nowym hoscie oddaje silnikowi zapisany
    /// NIEZEROWY czas. Sprawdzamy PARAMETR wywolania <c>Play</c>, nie napis w
    /// odpowiedzi: to jedyny dowod, ze dekoder faktycznie dostal pozycje.
    /// </summary>
    private static void WznowienieGRAodZAPISANEGOniezerowegoCzasu()
    {
        var profile = NewProfile("wznowienie-gra");
        var position = TimeSpan.FromMilliseconds(12_250);

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:A","volume":0}"""));
        queue.NotePosition(position);
        queue.SaveResumeCheckpoint();
        first.Dispose();

        var second = LiteQueueStore.Open(profile, writable: true);
        using var reopened = second;
        var output = new SilentOutput();
        var restored = new LiteQueueCoordinator(output, second);
        Assert.True(output.Plays.Count == 0,
            "samo wczytanie profilu NIE zagralo niczego");

        restored.PlayAt(Args("""{"itemId":"file:A","volume":0}"""));
        Assert.True(output.Plays.Count == 1, "swiadome wznowienie zagralo raz");
        var play = output.Plays[0];
        Assert.Equal("file:A", play.Id, "zagral TEN utwor");
        Assert.True(Math.Abs((play.Position - position).TotalMilliseconds) < 1d,
            $"Play dostal ZAPISANY niezerowy czas; dostal {play.Position}");
        Assert.True(play.Position > TimeSpan.Zero, "czas startu NIE jest zerem");
    }

    /// <summary>
    /// Sam START hosta nie odtwarza niczego, takze gdy w profilu stoi zapisany
    /// biezacy utwor z pozycja. Autoodtwarzanie po starcie byloby bledem.
    /// </summary>
    private static void SamoWczytanieNIEodtwarzaNICZEGO()
    {
        var profile = NewProfile("bez-autostartu");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:C","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(9));
        queue.SaveResumeCheckpoint();
        first.Dispose();

        using var second = LiteQueueStore.Open(profile, writable: true);
        var output = new SilentOutput();
        var restored = new LiteQueueCoordinator(output, second);
        Assert.True(output.Plays.Count == 0, "zaden Play nie poszedl do silnika");
        Assert.True(output.LoadedItemId is null, "silnik nie ma zaladowanego materialu");
        var status = restored.Status();
        Assert.True(!status.Playing, "status NIE twierdzi odtwarzania po samym starcie");
        Assert.True(status.PositionSeconds > 0d,
            "pozycja jest ZNANA, mimo ze nic nie gra -- to jest wznowienie na zadanie");
    }

    /// <summary>
    /// Polityka AMC decyduje, nie my: dla pozycji z
    /// <c>resume_mode = StartFromBeginning</c> czas NIE jest pamietany i
    /// wznowienie startuje od zera. Nie wolno wymuszac wznowienia wbrew
    /// ustawieniu uzytkownika.
    /// </summary>
    private static void UstawienieZAWSZEodPOCZATKUnieWznawia()
    {
        var profile = NewProfile("od-poczatku");
        using (var seed = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName)))
        {
            // 2 == ResumePositionMode.StartFromBeginning (Inherit=0, Remember=1).
            Execute(seed, "UPDATE local_items SET resume_mode = 2 WHERE id = 'file:B';");
        }

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(11));
        queue.SaveResumeCheckpoint();
        first.Dispose();

        using (var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName)))
        {
            Assert.True(
                Scalar(verify, "SELECT resume_position_ticks FROM local_items WHERE id = 'file:B';") == 0,
                "pozycja z trybem 'zawsze od poczatku' zapisuje ZERO, jak w pelnym AMC");
        }

        using var second = LiteQueueStore.Open(profile, writable: true);
        var output = new SilentOutput();
        var restored = new LiteQueueCoordinator(output, second);
        restored.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        Assert.True(output.Plays[0].Position == TimeSpan.Zero,
            $"wznowienie wbrew ustawieniu NIE nastapilo; dostal {output.Plays[0].Position}");
    }

    /// <summary>
    /// Kolejka ZUZYTA do zera nie ma prawa przywrocic ostatniego utworu jako
    /// biezacego. Inaczej nowy host wygladalby, jakby mial co wznawiac.
    /// </summary>
    private static void ZuzytaPUSTAkolejkaNIEwracaDoOstatniegoUtworu()
    {
        var profile = NewProfile("zuzyta-bez-wznowienia");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(4));
        queue.SaveResumeCheckpoint();
        // Naturalny koniec az do wyczerpania kolejki.
        queue.HandlePlaybackEnded("file:B");
        queue.HandlePlaybackEnded("file:A");
        queue.HandlePlaybackEnded("file:C");
        queue.SaveResumeCheckpoint();
        first.Dispose();

        using var second = LiteQueueStore.Open(profile, writable: true);
        var output = new SilentOutput();
        var restored = new LiteQueueCoordinator(output, second);
        Assert.True(restored.RestoredRows == 0, "zuzyta kolejka zostaje pusta");
        var status = restored.Status();
        Assert.True(status.CurrentId is null,
            $"pusta kolejka NIE ma biezacego utworu; podala {status.CurrentId}");
        Assert.True(status.PositionSeconds == 0d, "nie ma czego wznawiac");
        Assert.True(output.Plays.Count == 0, "i nic nie zagralo");

        using var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName));
        using var command = verify.CreateCommand();
        command.CommandText = "SELECT current_item_id FROM local_state WHERE singleton = 1;";
        Assert.True(command.ExecuteScalar() is null or DBNull,
            "zuzyta kolejka wyczyscila current_item_id, a nie zostawila ostatniego utworu");
    }

    /// <summary>
    /// Wyjscie do BEZPOSREDNIEGO pliku (albo radia) odcina kolejke od
    /// transportu. Checkpoint po takim wyjsciu NIE MOZE przypisac czasu
    /// cudzego materialu Id pozycji kolejki.
    /// </summary>
    private static void WyjscieDoBEZPOSREDNIEGOplikuNIEprzypisujePozycjiInnemuId()
    {
        var profile = NewProfile("wyjscie-poza-kolejke");

        using var store = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), store);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(5));
        queue.SaveResumeCheckpoint();

        // Uzytkownik gra plik WPROST -- host odcina kolejke (files.play).
        queue.DetachFromDirectPlay();
        // Czas tamtego, obcego materialu NIE nalezy do kolejki.
        queue.NotePosition(TimeSpan.FromSeconds(300));
        queue.SaveResumeCheckpoint();

        using var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName));
        Assert.True(
            Scalar(verify, "SELECT resume_position_ticks FROM local_items WHERE id = 'file:B';")
                == TimeSpan.FromSeconds(5).Ticks,
            "pozycja kolejki zostala przy swoim czasie, a nie przy czasie obcego materialu");
        Assert.True(
            Scalar(verify, "SELECT COUNT(*) FROM local_items WHERE resume_position_ticks = " +
                TimeSpan.FromSeconds(300).Ticks.ToString(CultureInfo.InvariantCulture) + ";") == 0,
            "czas bezposredniego odtwarzania NIE trafil do ZADNEGO Id kolejki");
    }

    /// <summary>
    /// Odcisk pliku rozstrzyga: PODMIENIONY plik pod tym samym Id nie wznawia
    /// sie ze starego czasu. To jest regula <c>CanRestorePosition</c> z pelnego
    /// AMC, nie nasz wymysl.
    ///
    /// GRANICA: odcisk zapisuje PRZECHWYT KATALOGU pelnego AMC
    /// (<c>CaptureLocalMediaState</c>), a NIE checkpoint pozycji -- tak samo
    /// jest w <c>MainWindow.SaveLocalPlaybackCheckpoint</c>, ktory rusza tylko
    /// <c>ResumePositionTicks</c>. Dlatego profil ma tu odcisk zasiany, a nasz
    /// zapis go nie dotyka. Przy odcisku PUSTYM (NULL) AMC takze wznawia --
    /// kolumny nie ma, wiec nie ma czego porownac.
    /// </summary>
    private static void PodmienionyPLIKpodTYMSAMYMIdNIEwznawia()
    {
        var profile = NewProfile("podmieniony-plik");
        var mediaPath = MediaPath(profile, "A");
        using (var seed = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName)))
        {
            var info = new FileInfo(mediaPath);
            Execute(seed,
                "UPDATE local_items SET file_length = $len, last_write_utc_ticks = $ticks WHERE id = 'file:A';",
                ("$len", info.Length), ("$ticks", info.LastWriteTimeUtc.Ticks));
        }

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:A","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(8));
        queue.SaveResumeCheckpoint();
        first.Dispose();

        using (var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName)))
        {
            Assert.True(
                Scalar(verify, "SELECT resume_position_ticks FROM local_items WHERE id = 'file:A';")
                    == TimeSpan.FromSeconds(8).Ticks,
                "czas zostal zapisany, zanim plik sie zmienil");
            Assert.True(
                Scalar(verify, "SELECT COUNT(*) FROM local_items WHERE id = 'file:A' AND file_length IS NOT NULL;") == 1,
                "nasz checkpoint NIE wyczyscil odcisku zasianego przez AMC");
        }

        // Plik ROSNIE: inna dlugosc niz zapisany odcisk.
        File.WriteAllBytes(mediaPath, new byte[4096]);

        using var second = LiteQueueStore.Open(profile, writable: true);
        var output = new SilentOutput();
        var restored = new LiteQueueCoordinator(output, second);
        restored.PlayAt(Args("""{"itemId":"file:A","volume":0}"""));
        Assert.True(output.Plays[0].Position == TimeSpan.Zero,
            $"podmieniony plik startuje od zera; dostal {output.Plays[0].Position}");
    }

    /// <summary>
    /// Checkpoint pozycji jest tak samo WASKI jak zapis kolejnosci: pozostale
    /// tabele i kolumny zostaja co do TRESCI, nie tylko co do liczby rekordow.
    /// </summary>
    private static void CheckpointPozycjiNIEruszaPOZOSTALYCHtabel()
    {
        var profile = NewProfile("checkpoint-przed-po");
        var databasePath = Path.Combine(profile, LiteQueueStore.LibraryFileName);

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        first.Dispose();

        // Stan PO zapisie kolejnosci, PRZED checkpointem pozycji.
        var before = DumpAllExcept(databasePath);

        var second = LiteQueueStore.Open(profile, writable: true);
        var again = new LiteQueueCoordinator(new SilentOutput(), second);
        again.PlayAt(Args("""{"itemId":"file:C","volume":0}"""));
        again.NotePosition(TimeSpan.FromMilliseconds(3_125));
        Assert.True(again.SaveResumeCheckpoint(), "checkpoint poszedl");
        second.Dispose();

        var after = DumpAllExcept(databasePath);
        foreach (var (key, value) in before)
        {
            Assert.Equal(value, after[key],
                $"tresc poza jawnym checkpointem nietknieta: {key}");
        }

        // A to, co checkpoint MIAL zmienic, faktycznie sie zmienilo.
        using var verify = Connect(databasePath);
        Assert.True(
            Scalar(verify, "SELECT resume_position_ticks FROM local_items WHERE id = 'file:C';")
                == TimeSpan.FromMilliseconds(3_125).Ticks,
            "checkpoint zapisal pozycje biezacego utworu");
        using var command = verify.CreateCommand();
        command.CommandText = "SELECT volume, library_view FROM local_state WHERE singleton = 1;";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), "local_state nadal ma swoj wiersz");
        Assert.True(reader.GetInt32(0) == 0,
            "glosnosc 0 w profilu PRZETRWALA checkpoint pozycji");
        Assert.Equal("Wszystko", reader.GetString(1), "widok Biblioteki nietkniety");
    }

    /// <summary>
    /// Checkpoint w trybie TYLKO ODCZYT leci ODMOWA widoczna w stanie, nigdy
    /// cichym powodzeniem -- ta sama zasada, co dla zapisu kolejnosci.
    /// </summary>
    private static void OdmowaZapisuPozycjiNIEudajePowodzenia()
    {
        var profile = NewProfile("checkpoint-odmowa");
        using var store = LiteQueueStore.Open(profile, writable: false);
        var queue = new LiteQueueCoordinator(new SilentOutput(), store);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(6));

        Assert.True(!queue.SaveResumeCheckpoint(),
            "checkpoint w trybie odczytu NIE zglasza powodzenia");
        var status = queue.Status();
        Assert.True(status.PersistError is not null,
            "powod odmowy jest widoczny w stanie");

        using var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName));
        Assert.True(
            Scalar(verify, "SELECT COUNT(*) FROM local_items WHERE resume_position_ticks <> 0;") == 0,
            "baza NIE dostala ani jednej pozycji w trybie odczytu");
        using var command = verify.CreateCommand();
        command.CommandText = "SELECT current_item_id FROM local_state WHERE singleton = 1;";
        Assert.True(command.ExecuteScalar() is null or DBNull,
            "current_item_id tez nie zostal zapisany");
    }

    /// <summary>
    /// GLOBALNE wylaczenie wznawiania w AMC
    /// (<c>AppSettings.RememberLocalPlaybackPositions = false</c>) obowiazuje
    /// takze host: pozycja DZIEDZICZACA (Inherit) nie wznawia sie. Nie wolno
    /// wymuszac wznowienia wbrew ustawieniu uzytkownika.
    /// </summary>
    private static void GLOBALNEwylaczenieWznawianiaObowiazuje()
    {
        var profile = NewProfile("globalnie-wylaczone");
        File.WriteAllText(
            Path.Combine(profile, "state.json"),
            """{"settings":{"rememberLocalPlaybackPositions":false}}""");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:A","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(9));
        queue.SaveResumeCheckpoint();
        first.Dispose();

        using (var verify = Connect(Path.Combine(profile, LiteQueueStore.LibraryFileName)))
        {
            Assert.True(
                Scalar(verify, "SELECT resume_position_ticks FROM local_items WHERE id = 'file:A';") == 0,
                "globalne wylaczenie zapisuje ZERO, nie zostawia starego czasu");
        }

        using var second = LiteQueueStore.Open(profile, writable: true);
        var output = new SilentOutput();
        var restored = new LiteQueueCoordinator(output, second);
        restored.PlayAt(Args("""{"itemId":"file:A","volume":0}"""));
        Assert.True(output.Plays[0].Position == TimeSpan.Zero,
            $"przy globalnym wylaczeniu gra od zera; dostal {output.Plays[0].Position}");
    }

    /// <summary>
    /// STATUS podaje czas, z ktorego pojdzie swiadome wznowienie. To minimalne
    /// polaczenie dla okna: bez tego pola GUI nie wie, czy zapowiedziec
    /// "wznow od 00:12" czy "od poczatku". Pole liczy TA SAMA metode, ktorej
    /// uzywa <c>PlayAt</c>, wiec nie moze obiecac czasu niezgodnego z gra.
    /// </summary>
    private static void STATUSpodajeCzasWznowieniaDlaOkna()
    {
        var profile = NewProfile("status-wznowienia");

        var first = LiteQueueStore.Open(profile, writable: true);
        var queue = new LiteQueueCoordinator(new SilentOutput(), first);
        queue.Set(Args(ThreeRows(profile)));
        queue.PlayAt(Args("""{"itemId":"file:B","volume":0}"""));
        queue.NotePosition(TimeSpan.FromSeconds(11.25));
        queue.SaveResumeCheckpoint();
        first.Dispose();

        using var second = LiteQueueStore.Open(profile, writable: true);
        var restored = new LiteQueueCoordinator(new SilentOutput(), second);
        var status = restored.Status();
        Assert.True(status.CurrentId == "file:B",
            $"status oddaje wczytany biezacy utwor; dostal {status.CurrentId}");
        Assert.True(Math.Abs(status.ResumeSeconds - 11.25) < 0.01,
            $"status oddaje czas wznowienia 11,25 s; dostal {status.ResumeSeconds}");
        Assert.True(!status.Playing && !status.Paused,
            "sam odczyt statusu NIE zaczyna grac");
    }

    // ------------------------------------------------------------- narzedzia

    /// <summary>
    /// Zrzut CALEJ tresci bazy poza tym, co jawny checkpoint ma prawo zmienic
    /// (<c>local_state</c> i kolumna <c>resume_position_ticks</c>). Porownanie
    /// tresci, nie samej liczby rekordow: podmiana wartosci w miejscu tez jest
    /// zmiana.
    /// </summary>
    private static Dictionary<string, string> DumpAllExcept(string databasePath)
    {
        var dump = new Dictionary<string, string>(StringComparer.Ordinal);
        using var connection = Connect(databasePath);

        var tables = new List<string>();
        using (var list = connection.CreateCommand())
        {
            list.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
            using var reader = list.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
        }

        foreach (var table in tables)
        {
            if (string.Equals(table, "local_state", StringComparison.Ordinal)) continue;

            var columns = new List<string>();
            using (var info = connection.CreateCommand())
            {
                info.CommandText = $"PRAGMA table_info({table});";
                using var reader = info.ExecuteReader();
                while (reader.Read()) columns.Add(reader.GetString(1));
            }
            // Kolumna, ktora checkpoint ma prawo zmienic, jest WYLACZONA z
            // porownania; cala reszta local_items nadal jest porownywana.
            var compared = columns
                .Where(column => !string.Equals(column, "resume_position_ticks", StringComparison.Ordinal))
                .ToArray();
            if (compared.Length == 0) continue;

            using var rows = connection.CreateCommand();
            var projection = string.Join(", ", compared.Select(column => $"quote({column})"));
            rows.CommandText = $"SELECT {projection} FROM {table};";
            var lines = new List<string>();
            using var rowReader = rows.ExecuteReader();
            while (rowReader.Read())
            {
                var values = new string[compared.Length];
                for (var index = 0; index < compared.Length; index++)
                {
                    values[index] = rowReader.IsDBNull(index) ? "NULL" : rowReader.GetString(index);
                }
                lines.Add(string.Join("\u0001", values));
            }
            // Sortowanie, bo kolejnosc wierszy bez ORDER BY nie jest obiecana.
            lines.Sort(StringComparer.Ordinal);
            dump[table] = string.Join("\n", lines);
        }
        return dump;
    }

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
