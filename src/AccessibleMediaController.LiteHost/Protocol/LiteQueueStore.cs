using System.Globalization;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// ODMOWA magazynu kolejki. Osobny typ, bo odmowa NIE MOZE skonczyc sie
/// komunikatem "zapisano": host oddaje ja jako blad zadania z trescia dla
/// uzytkownika, a nie jako ciche powodzenie.
/// </summary>
public sealed class LiteQueueStoreDenied(string message) : Exception(message);

/// <summary>Tryb magazynu. DOMYSLNY jest odczyt.</summary>
public enum LiteQueueStoreMode
{
    /// <summary>Tylko odczyt. Zaden zapis nie dojdzie do bazy.</summary>
    ReadOnly,

    /// <summary>Zapis dozwolony -- wylacznie na WLASNEJ kopii profilu.</summary>
    Writable
}

/// <summary>Jeden wiersz kolejki odczytany z profilu.</summary>
public sealed record LiteQueueStoredRow(
    string Id,
    string Title,
    string? Path,
    bool IsInQueue,
    bool IsPlayNext);

/// <summary>
/// Dane WZNOWIENIA jednej pozycji, czytane z profilu.
///
/// <paramref name="ResumePositionTicks"/> i <paramref name="ResumeMode"/> to
/// DOKLADNIE kolumny <c>local_items</c> pelnego AMC. Odcisk pliku
/// (<paramref name="FileLength"/>, <paramref name="LastWriteUtcTicks"/>) jest
/// tu po to, zeby powtorzyc regule <c>MainWindow.CanRestorePosition</c>:
/// podmieniony plik NIE wznawia sie ze starego czasu.
/// </summary>
public sealed record LiteResumeEntry(
    string ItemId,
    long ResumePositionTicks,
    int ResumeMode,
    long? FileLength,
    long? LastWriteUtcTicks);

/// <summary>
/// Stan WZNOWIENIA odczytany z profilu: ktory utwor byl biezacy i z jakim
/// czasem. Zrodlem sa <c>local_state.current_item_id</c> oraz
/// <c>local_items.resume_position_ticks</c> -- te same kolumny, ktorych uzywa
/// pelne AMC. Zadnego bocznego JSON-a obok.
/// </summary>
public sealed record LiteResumeState(
    string? CurrentItemId,
    IReadOnlyDictionary<string, LiteResumeEntry> Entries);

/// <summary>
/// To, co host chce utrwalic jako punkt wznowienia.
///
/// <paramref name="Positions"/> obejmuje WSZYSTKIE pozycje kolejki (takze
/// zerowe), bo wyzerowanie czasu po dograniu utworu do konca jest rownie
/// istotne, co jego zapamietanie.
/// </summary>
public sealed record LiteResumeCheckpoint(
    string? CurrentItemId,
    IReadOnlyDictionary<string, TimeSpan> Positions);

/// <summary>
/// Wynik odczytu zapisanej kolejki.
///
/// <paramref name="Saved"/> jest tu po to, by odroznic DWA stany, ktorych same
/// wiersze nie rozrozniaja: "nikt jeszcze nie zapisal kolejki" kontra
/// "zapisano kolejke PUSTA, bo zostala zuzyta do zera". Bez tego pola kolejka
/// zuzyta do zera wygladalaby jak brak zapisu i frontend mialby prawo
/// przywrocic stare pozycje z ekranu.
/// </summary>
public sealed record LiteQueueStoredState(
    bool Saved,
    IReadOnlyList<LiteQueueStoredRow> Rows);

/// <summary>
/// TRWALOSC lokalnej kolejki w profilu AMC -- JEDEN wlasciciel zapisu.
///
/// Dlaczego ta klasa istnieje, a nie zapis z Pythona
/// -------------------------------------------------
/// Frontend wxPython czyta wspolny profil TYLKO do odczytu (``mode=ro``).
/// Wlascicielem zapisu zostaje host C#: to on trzyma blokade, zna schemat i
/// uzywa tych samych tabel, co pelne AMC. Python nie dotyka plikow kolejki.
///
/// Co ta klasa zapisuje -- i czego NIE zapisuje
/// --------------------------------------------
/// Zapis jest WASKI: trzy tabele kolejnosci kolejki
/// (<c>queue_order</c>, <c>queue_regular_order</c>, <c>queue_play_next_order</c>)
/// plus flagi <c>is_in_queue</c>/<c>is_play_next</c> w <c>local_items</c>.
/// Dokladnie te pola zapisuje <c>LocalLibraryDatabase.SaveCore</c> dla kolejki.
///
/// Celowo NIE wolamy <c>ConfigurationStore.Save</c> ani
/// <c>LocalLibraryDatabase.Save</c>: oba kasuja KAZDA tabele i wpisuja na nowo
/// caly <c>PersistedState</c>. Host Lite nie ma w pamieci calego stanu profilu
/// (nie zna Zakladek, Ulubionych, historii, playlist), wiec taki zapis
/// skasowalby je z bazy. Zapis calego AppState z mala lista z frontendu jest
/// tu wprost zabroniony.
///
/// Czego ta klasa NIE obiecuje
/// ---------------------------
/// Blokada pliku chroni przed DRUGIM HOSTEM LITE na tej samej kopii. Stary
/// WPF AMC jej NIE zna i nie zostala zmierzona ochrona miedzy nimi -- dlatego
/// zapis do PRODUKCYJNEGO profilu jest twardo odmawiany, a nie "chroniony".
/// </summary>
public sealed class LiteQueueStore : IDisposable
{
    /// <summary>Plik blokady wlasnosci. Lezy OBOK bazy, w katalogu profilu.</summary>
    public const string OwnerLockFileName = "amc-lite-queue-owner.lock";

    public const string LibraryFileName = "library.db";

    /// <summary>Sesja lokalnej Biblioteki. Tylko ona mapuje sie na <c>local_items</c>.</summary>
    public const string LocalSessionId = "local";

    private readonly object _gate = new();
    private readonly FileStream? _ownerLock;

    /// <summary>
    /// Podpis stanu OSTATNIO ZAPISANEGO (albo odczytanego). Zapis automatyczny
    /// rusza TYLKO gdy stan faktycznie sie rozni -- inaczej kazde
    /// <c>queue.status</c> pisaloby po bazie bez powodu.
    /// </summary>
    private string? _persistedSignature;

    /// <summary>
    /// Podpis OSTATNIO ZAPISANEGO punktu wznowienia. Ta sama zasada, co przy
    /// kolejnosci: checkpoint bez faktycznej zmiany nie pisze po bazie.
    /// </summary>
    private string? _resumeSignature;

    private LiteQueueStore(
        string profileDirectory,
        string databasePath,
        LiteQueueStoreMode mode,
        FileStream? ownerLock)
    {
        ProfileDirectory = profileDirectory;
        DatabasePath = databasePath;
        Mode = mode;
        _ownerLock = ownerLock;
    }

    public string ProfileDirectory { get; }

    public string DatabasePath { get; }

    public LiteQueueStoreMode Mode { get; }

    public bool IsWritable => Mode == LiteQueueStoreMode.Writable;

    /// <summary>Czy TEN proces trzyma blokade wlasnosci zapisu.</summary>
    public bool OwnsWriteLock => _ownerLock is not null;

    public string? PersistedSignature
    {
        get { lock (_gate) return _persistedSignature; }
    }

    /// <summary>
    /// Sciezka PRODUKCYJNEGO profilu AMC. Zapis tam jest odmawiany zawsze:
    /// stary WPF nie zna naszej blokady, wiec wspolny zapis nie zostal
    /// zmierzony jako bezpieczny i nie wolno go ogloszac.
    /// </summary>
    public static string ProductionProfileDirectory
    {
        get
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(localData)
                ? string.Empty
                : Path.Combine(localData, "AccessibleMediaController");
        }
    }

    /// <summary>
    /// Otwiera magazyn na WSKAZANEJ kopii profilu.
    ///
    /// Zapis trzeba wlaczyc JAWNIE (<paramref name="writable"/>) i wskazuje on
    /// wylacznie ta kopie. Kazda przeszkoda -- brak bazy, katalog bez prawa
    /// zapisu, zajeta blokada, profil produkcyjny -- konczy sie
    /// <see cref="LiteQueueStoreDenied"/>, NIGDY cichym trybem odczytu
    /// udajacym zapis.
    /// </summary>
    public static LiteQueueStore Open(string profileDirectory, bool writable)
    {
        if (string.IsNullOrWhiteSpace(profileDirectory))
        {
            throw new LiteQueueStoreDenied("Nie podano katalogu profilu kolejki.");
        }

        var full = Path.GetFullPath(profileDirectory);
        if (!Directory.Exists(full))
        {
            throw new LiteQueueStoreDenied($"Katalog profilu nie istnieje: {full}");
        }

        var databasePath = Path.Combine(full, LibraryFileName);
        if (!File.Exists(databasePath))
        {
            throw new LiteQueueStoreDenied($"W profilu nie ma pliku {LibraryFileName}: {databasePath}");
        }

        if (!writable)
        {
            // Odczyt NIE bierze blokady: wielu czytelnikow nikomu nie szkodzi,
            // a frontend Python i tak czyta ta sama baze przez mode=ro.
            return new LiteQueueStore(full, databasePath, LiteQueueStoreMode.ReadOnly, null);
        }

        var production = ProductionProfileDirectory;
        if (production.Length > 0 && IsSameDirectory(full, production))
        {
            throw new LiteQueueStoreDenied(
                "Odmowa zapisu do PRODUKCYJNEGO profilu AMC. Stary AMC (WPF) nie zna blokady "
                + "tego hosta, wiec wspolny zapis nie jest zmierzony jako bezpieczny. "
                + "Wskaz wlasna kopie profilu.");
        }

        FileStream ownerLock;
        var lockPath = Path.Combine(full, OwnerLockFileName);
        try
        {
            // FileShare.None: DRUGI host Lite na TEJ SAMEJ kopii dostanie tu
            // wyjatek i nie wejdzie w tryb zapisu. To jest cala ochrona przed
            // dwoma wlascicielami -- i tylko miedzy hostami Lite.
            ownerLock = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
        }
        catch (IOException exception)
        {
            throw new LiteQueueStoreDenied(
                "Ta kopia profilu ma juz wlasciciela zapisu kolejki (inny host). "
                + "Drugi host nie dostaje prawa zapisu: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new LiteQueueStoreDenied(
                "Brak prawa zapisu w katalogu profilu: " + exception.Message);
        }

        try
        {
            // Samo zalozenie blokady nie dowodzi, ze BAZA da sie zapisac
            // (plik moze byc tylko do odczytu). Sprawdzamy to od razu, zeby
            // odmowa wyszla przy otwarciu, a nie przy pierwszym zapisie.
            using var probe = OpenConnection(databasePath, readOnly: false);
            using var command = probe.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            command.ExecuteScalar();
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            ownerLock.Dispose();
            TryDeleteLock(lockPath);
            throw new LiteQueueStoreDenied(
                "Baza profilu nie przyjmuje zapisu: " + exception.Message);
        }

        var store = new LiteQueueStore(full, databasePath, LiteQueueStoreMode.Writable, ownerLock);
        // Podpis startowy bierzemy ze STANU W BAZIE: dzieki temu pierwszy zapis
        // bez faktycznej zmiany nie zglosi "zapisano".
        store.PrimeSignature(LocalSessionId);
        return store;
    }

    /// <summary>
    /// Odczyt ZAPISANEJ kolejki. Dziala w obu trybach -- to jest ta sama
    /// droga, ktora po restarcie hosta odtwarza kolejnosc i czlonkostwo.
    /// </summary>
    public LiteQueueStoredState Read(string sessionId)
    {
        RequireLocalSession(sessionId);
        lock (_gate)
        {
            using var connection = OpenConnection(DatabasePath, readOnly: true);
            var saved = ReadSavedMarker(connection, sessionId);
            var order = ReadOrder(connection, "queue_order", sessionId);
            var regular = ReadOrder(connection, "queue_regular_order", sessionId).ToHashSet(StringComparer.Ordinal);
            var playNext = ReadOrder(connection, "queue_play_next_order", sessionId).ToHashSet(StringComparer.Ordinal);
            // Gdy NIC jeszcze nie zapisano, podpis zostaje pusty (null): inaczej
            // pierwszy zapis PUSTEJ kolejki wygladalby jak "bez zmian" i
            // znacznik by nie powstal, wiec kolejka zuzyta do zera nie dalaby
            // sie odroznic od braku zapisu.
            _persistedSignature = saved || order.Count > 0
                ? Signature(
                    order,
                    order.Where(regular.Contains).ToArray(),
                    order.Where(playNext.Contains).ToArray())
                : null;

            if (order.Count == 0) return new LiteQueueStoredState(saved, []);

            var catalog = ReadCatalog(connection, order);
            var rows = new List<LiteQueueStoredRow>(order.Count);
            foreach (var itemId in order)
            {
                // Pozycja, ktorej NIE MA juz w Bibliotece, nie wraca do kolejki:
                // zapisana kolejnosc nie jest zrodlem istnienia utworu.
                if (!catalog.TryGetValue(itemId, out var entry)) continue;
                var isPlayNext = playNext.Contains(itemId);
                var isInQueue = regular.Contains(itemId);
                // Zapis sprzed rozdzielenia czlonkostwa (puste obie listy) byl
                // zwykla kolejka -- ta sama regula, co w
                // TransientQueuePersistence.Restore.
                if (!isPlayNext && !isInQueue && regular.Count == 0 && playNext.Count == 0) isInQueue = true;
                if (!isPlayNext && !isInQueue) continue;
                rows.Add(new LiteQueueStoredRow(itemId, entry.Title, entry.Path, isInQueue, isPlayNext));
            }
            // Niepusty zapis jest sam swoim dowodem istnienia: profil zapisany
            // pelnym AMC (ktore nie zna naszego znacznika) tez musi sie wczytac.
            return new LiteQueueStoredState(saved || rows.Count > 0, rows);
        }
    }

    /// <summary>
    /// ZAPIS stanu kolejki. Zwraca <c>true</c> tylko gdy cos faktycznie
    /// poszlo do bazy; brak zmiany oddaje <c>false</c> i NIE jest bledem.
    /// Odmowa (tryb odczytu, brak wlasnosci) leci wyjatkiem -- nigdy nie
    /// udaje powodzenia.
    /// </summary>
    public bool Write(string sessionId, QueuePersistenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RequireLocalSession(sessionId);
        if (!IsWritable)
        {
            throw new LiteQueueStoreDenied(
                "Magazyn kolejki jest w trybie TYLKO ODCZYT. Zapis wymaga jawnego trybu "
                + "zapisu wskazujacego wlasna kopie profilu.");
        }
        if (!OwnsWriteLock)
        {
            throw new LiteQueueStoreDenied(
                "Ten host nie trzyma blokady wlasnosci zapisu kolejki.");
        }

        var order = snapshot.StorageOrder.ToArray();
        var regular = snapshot.RegularItemIds.ToArray();
        var playNext = snapshot.PlayNextItemIds.ToArray();
        var signature = Signature(order, regular, playNext);

        lock (_gate)
        {
            if (string.Equals(_persistedSignature, signature, StringComparison.Ordinal)) return false;

            using var connection = OpenConnection(DatabasePath, readOnly: false);
            using var transaction = connection.BeginTransaction();

            // 1. Tylko WIERSZE TEJ SESJI. Inne sesje (tidal, spotify) zostaja
            //    nietkniete -- nie resetujemy cudzej kolejki.
            foreach (var table in new[] { "queue_order", "queue_regular_order", "queue_play_next_order" })
            {
                Execute(connection, transaction,
                    $"DELETE FROM {table} WHERE session_id = $session;",
                    ("$session", sessionId));
            }

            InsertOrder(connection, transaction, "queue_order", sessionId, order);
            InsertOrder(connection, transaction, "queue_regular_order", sessionId, regular);
            InsertOrder(connection, transaction, "queue_play_next_order", sessionId, playNext);

            // ZNACZNIK zapisu. W tabeli metadata, bo ona juz istnieje w schemacie
            // pelnego AMC i jest czyszczona tylko przy pelnym SaveCore. Dzieki
            // niemu kolejka ZUZYTA DO ZERA jest widoczna jako "zapisano pustke",
            // a nie jako "nic nie zapisano".
            Execute(connection, transaction,
                """
                INSERT INTO metadata(key, value) VALUES($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """,
                ("$key", SavedMarkerKey(sessionId)),
                ("$value", order.Length.ToString(CultureInfo.InvariantCulture)));

            // 2. Flagi w local_items. Pelne AMC zapisuje je razem z tabelami
            //    kolejnosci i czyta przy starcie, wiec rozjazd tych dwoch zapisow
            //    konczylby sie pozycjami "w kolejce", ktorych nie ma w kolejnosci.
            //    Czyscimy WSZYSTKIE flagi, potem stawiamy nowe: to samo robi
            //    SaveCore, tylko bez kasowania reszty bazy.
            Execute(connection, transaction,
                "UPDATE local_items SET is_in_queue = 0, is_play_next = 0 WHERE is_in_queue <> 0 OR is_play_next <> 0;");
            SetFlag(connection, transaction, "is_in_queue", regular);
            SetFlag(connection, transaction, "is_play_next", playNext);

            transaction.Commit();
            _persistedSignature = signature;
            return true;
        }
    }

    /// <summary>
    /// Odczyt DANYCH WZNOWIENIA z profilu: biezacy utwor i czasy pozycji.
    ///
    /// Czytamy te same kolumny, co <c>LocalLibraryDatabase.LoadInto</c>, i nie
    /// interpretujemy ich tutaj -- decyzja "czy wolno wznowic" nalezy do
    /// polityki (tryb pozycji, odcisk pliku), nie do magazynu.
    /// </summary>
    public LiteResumeState ReadResume(string sessionId)
    {
        RequireLocalSession(sessionId);
        lock (_gate)
        {
            using var connection = OpenConnection(DatabasePath, readOnly: true);

            string? currentItemId = null;
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT current_item_id FROM local_state WHERE singleton = 1;";
                var value = command.ExecuteScalar();
                if (value is string text && text.Length > 0) currentItemId = text;
            }

            var entries = new Dictionary<string, LiteResumeEntry>(StringComparer.Ordinal);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT id, resume_position_ticks, resume_mode, file_length, last_write_utc_ticks
                    FROM local_items;
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetString(0);
                    entries[id] = new LiteResumeEntry(
                        id,
                        reader.GetInt64(1),
                        reader.GetInt32(2),
                        reader.IsDBNull(3) ? null : reader.GetInt64(3),
                        reader.IsDBNull(4) ? null : reader.GetInt64(4));
                }
            }

            return new LiteResumeState(currentItemId, entries);
        }
    }

    /// <summary>
    /// ZAPIS punktu wznowienia: biezacy utwor i czasy pozycji.
    ///
    /// Zapis jest tak samo WASKI jak zapis kolejnosci -- rusza WYLACZNIE
    /// kolumne <c>local_items.resume_position_ticks</c> dla WSKAZANYCH pozycji
    /// oraz <c>local_state.current_item_id</c>. Tak samo jak tam, nie wolamy
    /// <c>LocalLibraryDatabase.Save</c>: ono skasowaloby cala baze i wpisalo na
    /// nowo caly <c>PersistedState</c>, ktorego ten host nie ma w pamieci.
    ///
    /// POLITYKA AMC rozstrzyga PRZED tym wywolaniem: pozycje z trybem
    /// <c>StartFromBeginning</c> przychodza tu z czasem zerowym, dokladnie jak
    /// w <c>MainWindow.SaveLocalPlaybackCheckpoint</c>. Magazyn niczego nie
    /// wymusza i nie dopisuje wznawiania wbrew ustawieniu.
    ///
    /// Zwraca <c>true</c> tylko gdy cos naprawde poszlo do bazy; odmowa leci
    /// wyjatkiem i nigdy nie udaje powodzenia.
    /// </summary>
    public bool WriteResume(string sessionId, LiteResumeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        RequireLocalSession(sessionId);
        if (!IsWritable)
        {
            throw new LiteQueueStoreDenied(
                "Magazyn kolejki jest w trybie TYLKO ODCZYT. Zapis pozycji wznowienia wymaga "
                + "jawnego trybu zapisu wskazujacego wlasna kopie profilu.");
        }
        if (!OwnsWriteLock)
        {
            throw new LiteQueueStoreDenied(
                "Ten host nie trzyma blokady wlasnosci zapisu kolejki.");
        }

        var signature = ResumeSignature(checkpoint);
        lock (_gate)
        {
            if (string.Equals(_resumeSignature, signature, StringComparison.Ordinal)) return false;

            using var connection = OpenConnection(DatabasePath, readOnly: false);
            using var transaction = connection.BeginTransaction();

            foreach (var (itemId, position) in checkpoint.Positions)
            {
                // Tylko JEDNA kolumna i tylko dla pozycji, ktore host zna.
                // Pozycji spoza kolejki nie dotykamy -- ich czas nalezy do
                // pelnego AMC albo do innego kontekstu odtwarzania.
                Execute(connection, transaction,
                    "UPDATE local_items SET resume_position_ticks = $ticks WHERE id = $item;",
                    ("$ticks", Math.Max(0L, position.Ticks)), ("$item", itemId));
            }

            // Biezacy utwor w local_state. Wiersz singleton moze jeszcze nie
            // istniec w swiezym profilu -- wtedy go zakladamy z wartosciami
            // domyslnymi tego schematu, nie zgadujac cudzych pol.
            Execute(connection, transaction,
                """
                INSERT INTO local_state(singleton, library_view, volume, playback_rate)
                VALUES(1, 'Wszystko', 35, 1.0)
                ON CONFLICT(singleton) DO NOTHING;
                """);
            Execute(connection, transaction,
                "UPDATE local_state SET current_item_id = $item WHERE singleton = 1;",
                ("$item", checkpoint.CurrentItemId));

            transaction.Commit();
            _resumeSignature = signature;
            return true;
        }
    }

    private static string ResumeSignature(LiteResumeCheckpoint checkpoint) =>
        (checkpoint.CurrentItemId ?? "\u0003") + "\u0002" + string.Join(
            "\u0001",
            checkpoint.Positions
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value.Ticks.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Liczba rekordow w tabelach, ktorych ten magazyn NIE RUSZA. Sluzy do
    /// pomiaru PRZED/PO: biblioteka, Ulubione, zakladki, playlisty i historia
    /// musza zostac co do rekordu.
    /// </summary>
    public IReadOnlyDictionary<string, long> CountUntouchedTables()
    {
        var tables = new[]
        {
            "local_items", "folder_sources", "bookmarks", "playback_history",
            "playlists", "playlist_items", "favorite_order", "favorite_added_order",
            "library_added_order", "library_custom_order", "custom_order",
            "excluded_paths", "folder_playback_options", "local_state"
        };
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        lock (_gate)
        {
            using var connection = OpenConnection(DatabasePath, readOnly: true);
            foreach (var table in tables)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM {table};";
                result[table] = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }
        return result;
    }

    public void Dispose()
    {
        _ownerLock?.Dispose();
        if (_ownerLock is not null) TryDeleteLock(Path.Combine(ProfileDirectory, OwnerLockFileName));
    }

    private void PrimeSignature(string sessionId)
    {
        try
        {
            Read(sessionId);
        }
        catch (SqliteException)
        {
            // Nieczytelna baza nie moze ustawic falszywego podpisu "pusto".
            lock (_gate) _persistedSignature = null;
        }
    }

    private static void RequireLocalSession(string sessionId)
    {
        if (string.Equals(sessionId, LocalSessionId, StringComparison.Ordinal)) return;
        throw new LiteQueueStoreDenied(
            $"Trwalosc kolejki obsluguje w tym hoscie wylacznie sesje \"{LocalSessionId}\" "
            + $"(lokalna Biblioteka); zadano \"{sessionId}\". Sesje zdalne maja wlasna kopie "
            + "kolejki w pelnym AMC i nie sa tu zapisywane.");
    }

    private static bool IsSameDirectory(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void TryDeleteLock(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Signature(
        IReadOnlyList<string> order,
        IReadOnlyList<string> regular,
        IReadOnlyList<string> playNext) =>
        string.Join("\u0001", order) + "\u0002" + string.Join("\u0001", regular)
        + "\u0002" + string.Join("\u0001", playNext);

    private static string SavedMarkerKey(string sessionId) =>
        "lite_queue_saved:" + sessionId;

    private static bool ReadSavedMarker(SqliteConnection connection, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", SavedMarkerKey(sessionId));
        // RED-WITNESS: zwrot stalego false tu powoduje, ze kolejka zuzyta do zera
        // wyglada jak brak zapisu i stare pozycje maja prawo wrocic.
        return command.ExecuteScalar() is string;
    }

    private static List<string> ReadOrder(SqliteConnection connection, string table, string sessionId)
    {
        var values = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT item_id FROM {table} WHERE session_id = $session ORDER BY ordinal;";
        command.Parameters.AddWithValue("$session", sessionId);
        using var reader = command.ExecuteReader();
        while (reader.Read()) values.Add(reader.GetString(0));
        return values;
    }

    private static Dictionary<string, (string Title, string? Path)> ReadCatalog(
        SqliteConnection connection,
        IReadOnlyCollection<string> itemIds)
    {
        var catalog = new Dictionary<string, (string, string?)>(StringComparer.Ordinal);
        if (itemIds.Count == 0) return catalog;

        // Czytamy CALA kolumne raz, zamiast skladac IN (...) z setek parametrow:
        // kolejka miewa dziesiatki pozycji, a local_items jest i tak indeksowane
        // po id. Filtr robimy w pamieci, bo zbior Id jest maly.
        var wanted = itemIds.ToHashSet(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, path FROM local_items;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (!wanted.Contains(id)) continue;
            catalog[id] = (reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2));
        }
        return catalog;
    }

    private static void InsertOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string sessionId,
        IReadOnlyList<string> itemIds)
    {
        for (var index = 0; index < itemIds.Count; index++)
        {
            Execute(connection, transaction,
                $"INSERT INTO {table}(session_id, ordinal, item_id) VALUES($session, $ordinal, $item);",
                ("$session", sessionId), ("$ordinal", index), ("$item", itemIds[index]));
        }
    }

    private static void SetFlag(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string column,
        IReadOnlyList<string> itemIds)
    {
        foreach (var itemId in itemIds)
        {
            Execute(connection, transaction,
                $"UPDATE local_items SET {column} = 1 WHERE id = $item;",
                ("$item", itemId));
        }
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Polaczenie z baza profilu. Te same ustawienia, co
    /// <c>LocalLibraryDatabase.OpenConnection</c>: kolacja <c>AMC_PL</c> (bez
    /// niej SQLite odmawia zapytan na kolumnach z ta kolacja), WAL i limit
    /// czekania na zajety plik. <c>Pooling=false</c>, zeby po zamknieciu hosta
    /// nie zostal uchwyt do bazy.
    /// </summary>
    private static SqliteConnection OpenConnection(string databasePath, bool readOnly)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        connection.CreateCollation(
            "AMC_PL",
            (left, right) => CultureInfo.GetCultureInfo("pl-PL").CompareInfo.Compare(
                left,
                right,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = readOnly
            ? "PRAGMA busy_timeout=5000;"
            : "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return connection;
    }
}
