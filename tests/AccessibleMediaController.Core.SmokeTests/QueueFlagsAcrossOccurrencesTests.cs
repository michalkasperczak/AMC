using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

/// <summary>
/// Kolejka TIDAL: JEDEN utwor ma wiele wierszy o roznych Id (wiersz kolekcji
/// "tidal:123" oraz wiersz wpisu playlisty "tidal:123:entry:e9"), a wszystkie
/// mapuja sie na ten sam klucz magazynu "tidal:&lt;ExternalId&gt;".
///
/// Mierzony jest OSIAGALNY przez produkcyjne wywolanie przypadek: filtr
/// <c>MainWindow.EnsureQueueOrder</c> (<c>IsInQueue || IsPlayNext</c>)
/// przepuszcza OBA wiersze, gdy kazdy z nich nosi INNA flage - zwykla kolejke
/// na wierszu kolekcji i priorytet "Odtworz jako nastepne" na wpisie playlisty.
/// Taki stan powstaje przez rzeczywiste akcje uzytkownika:
/// <c>CommandRouter.AddQueue</c> na jednym wierszu i
/// <c>CommandRouter.TogglePlayNext</c> na drugim (oba ustawiaja flagi TYLKO na
/// elementach dzialania, nie na pozostalych wystapieniach utworu).
///
/// Przypadek "kolekcja bez flag + wpis playlisty z flaga" NIE jest osiagalny
/// tym wywolaniem, bo filtr odsiewa wiersz bez zadnej flagi - dlatego nie jest
/// tu mierzony i nie wolno na nim opierac poprawki.
/// </summary>
internal static class QueueFlagsAcrossOccurrencesTests
{
    public static void Run()
    {
        FlagiRoznychWystapienPrzezyjaZapisIOdczyt();
        SesjaLokalnaBezZmianyZachowania();
        SesjaSpotifyBezZmianyZachowania();
        CeloweUsuniecieNieWraca();
        PowtorzeniaNaPlylistcieNieZmienialyLiczebnosci();
    }

    /// <summary>
    /// Produkcyjny obieg zapisu: Capture -&gt; state -&gt; ConfigurationStore.Save
    /// -&gt; LoadOrCreate -&gt; Restore, dokladnie jak
    /// <c>EnsureQueueOrder</c> + <c>RestorePersistedQueueMembership</c>.
    /// </summary>
    private static void FlagiRoznychWystapienPrzezyjaZapisIOdczyt()
    {
        // Wiersz kolekcji: uzytkownik dodal go do kolejki (AddQueue).
        var collectionRow = new MediaItem
        {
            Id = "tidal:tracks:123",
            ExternalId = "tracks:123",
            Title = "Utwor katalogowy",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };
        // Wiersz WPISU PLAYLISTY tego samego utworu: uzytkownik nadal mu
        // priorytet (TogglePlayNext). Rozne Id, ten sam ExternalId.
        var playlistEntryRow = new MediaItem
        {
            Id = "tidal:tracks:123:entry:e9",
            ExternalId = "tracks:123",
            ContainerEntryId = "e9",
            Title = "Utwor katalogowy",
            Kind = MediaItemKind.Track,
            IsPlayNext = true
        };
        // Druga, niezalezna pozycja kolejki. Bez niej zabezpieczenie
        // legacyRegularQueue w Restore maskuje wade.
        var otherRow = new MediaItem
        {
            Id = "tidal:tracks:777",
            ExternalId = "tracks:777",
            Title = "Inny utwor",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };

        var sessionItems = new[] { collectionRow, playlistEntryRow, otherRow };
        // Dokladnie filtr z EnsureQueueOrder:12747.
        var queueItems = sessionItems.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
        Assert(
            queueItems.Length == 3,
            "Reproduktor musi przejsc produkcyjny filtr kolejki; inaczej mierzy przypadek nieosiagalny.");

        var directory = Path.Combine(Path.GetTempPath(), $"amc-queue-flags-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new ConfigurationStore(Path.Combine(directory, "state.json"));
            var state = ConfigurationStore.CreateDefaultState();

            var snapshot = TransientQueuePersistence.Capture("tidal", queueItems, storedOrder: null);
            state.CollectionOrders.QueueItemIdsBySession["tidal"] = snapshot.StorageOrder.ToList();
            state.CollectionOrders.QueueRegularItemIdsBySession["tidal"] = snapshot.RegularItemIds.ToList();
            state.CollectionOrders.QueuePlayNextItemIdsBySession["tidal"] = snapshot.PlayNextItemIds.ToList();
            state.RemoteQueues.ItemsBySession["tidal"] = queueItems
                .GroupBy(item => TransientQueuePersistence.StorageItemId("tidal", item), StringComparer.Ordinal)
                .Select(group => group.FirstOrDefault(item =>
                        string.Equals(item.Id, group.Key, StringComparison.Ordinal))
                    ?? group.First())
                .Select(item => RemoteQueueItemSettings.FromMediaItem("tidal", item))
                .ToList();

            Assert(
                snapshot.StorageOrder.Count == 2,
                "Dwa wystapienia tego samego utworu musza dac jeden klucz magazynu; "
                    + $"zmierzono {snapshot.StorageOrder.Count}.");
            Assert(
                snapshot.PlayNextItemIds.Contains("tidal:tracks:123"),
                "Priorytet nadany na wierszu wpisu playlisty musi trafic do zapisu pod kluczem magazynu; "
                    + $"zapisano PlayNext=[{string.Join(", ", snapshot.PlayNextItemIds)}].");
            Assert(
                snapshot.RegularItemIds.Contains("tidal:tracks:123"),
                "Zwykla przynaleznosc wiersza kolekcji musi trafic do zapisu; "
                    + $"zapisano Regular=[{string.Join(", ", snapshot.RegularItemIds)}].");

            store.Save(state);
            var loaded = store.LoadOrCreate();

            // Restart: katalog przychodzi z nowymi Id wystapien (inny wpis playlisty).
            var afterRestart = new[]
            {
                new MediaItem
                {
                    Id = "tidal:tracks:123",
                    ExternalId = "tracks:123",
                    Title = "Utwor katalogowy",
                    Kind = MediaItemKind.Track
                },
                new MediaItem
                {
                    Id = "tidal:tracks:123:entry:e11",
                    ExternalId = "tracks:123",
                    ContainerEntryId = "e11",
                    Title = "Utwor katalogowy",
                    Kind = MediaItemKind.Track
                },
                new MediaItem
                {
                    Id = "tidal:tracks:777",
                    ExternalId = "tracks:777",
                    Title = "Inny utwor",
                    Kind = MediaItemKind.Track
                }
            };
            TransientQueuePersistence.Restore(
                "tidal",
                afterRestart,
                loaded.CollectionOrders.QueueItemIdsBySession.GetValueOrDefault("tidal"),
                loaded.CollectionOrders.QueueRegularItemIdsBySession.GetValueOrDefault("tidal"),
                loaded.CollectionOrders.QueuePlayNextItemIdsBySession.GetValueOrDefault("tidal"));

            var restored = afterRestart.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
            Assert(
                restored.Length == 2,
                "Po restarcie kolejka musi miec dwie pozycje (utwor z playlisty i drugi utwor); "
                    + $"zmierzono {restored.Length}: [{string.Join(" | ", restored.Select(item => item.Id))}].");
            var recovered = afterRestart.Single(item => item.Id == "tidal:tracks:123");
            Assert(
                recovered.IsPlayNext,
                "Priorytet nadany na wierszu wpisu playlisty musi przetrwac restart.");
            Assert(
                afterRestart.Single(item => item.Id == "tidal:tracks:777").IsInQueue,
                "Druga, niezalezna pozycja kolejki musi przetrwac restart jako zwykla.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Kontrola: sesja lokalna nie ma wielu wystapien jednego utworu
    /// (klucz magazynu = Id). Zachowanie musi zostac bez zmian.
    /// </summary>
    private static void SesjaLokalnaBezZmianyZachowania()
    {
        var first = new MediaItem { Id = "local-1", Title = "Plik pierwszy", IsInQueue = true };
        var second = new MediaItem { Id = "local-2", Title = "Plik drugi", IsPlayNext = true };
        var snapshot = TransientQueuePersistence.Capture("local", [first, second], storedOrder: null);
        Assert(
            snapshot.StorageOrder.SequenceEqual(["local-1", "local-2"]),
            $"Sesja lokalna musi zapisac oba Id; zmierzono [{string.Join(", ", snapshot.StorageOrder)}].");
        Assert(
            snapshot.RegularItemIds.SequenceEqual(["local-1"]),
            $"Sesja lokalna: Regular=[{string.Join(", ", snapshot.RegularItemIds)}] zamiast [local-1].");
        Assert(
            snapshot.PlayNextItemIds.SequenceEqual(["local-2"]),
            $"Sesja lokalna: PlayNext=[{string.Join(", ", snapshot.PlayNextItemIds)}] zamiast [local-2].");

        var reloaded = new[]
        {
            new MediaItem { Id = "local-1", Title = "Plik pierwszy" },
            new MediaItem { Id = "local-2", Title = "Plik drugi" }
        };
        TransientQueuePersistence.Restore(
            "local",
            reloaded,
            snapshot.StorageOrder,
            snapshot.RegularItemIds,
            snapshot.PlayNextItemIds);
        Assert(
            reloaded[0].IsInQueue && !reloaded[0].IsPlayNext,
            "Sesja lokalna: zwykla pozycja musi wrocic jako zwykla.");
        Assert(
            reloaded[1].IsPlayNext && !reloaded[1].IsInQueue,
            "Sesja lokalna: priorytet musi wrocic jako priorytet.");
    }

    /// <summary>
    /// Kontrola: sesja Spotify uzywa Id jako klucza magazynu (bez galezi
    /// ExternalId). Dwa rozne Id musza pozostac dwiema pozycjami - nie wolno
    /// ich scalac przy okazji poprawki TIDAL-a.
    /// </summary>
    private static void SesjaSpotifyBezZmianyZachowania()
    {
        var plain = new MediaItem
        {
            Id = "2dad73521f7b4d029c04e73a69367ca8",
            ExternalId = "2dad73521f7b4d029c04e73a69367ca8",
            Title = "Utwor Spotify",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };
        var enginePrefixed = new MediaItem
        {
            Id = "spotifyLibrespot:2dad73521f7b4d029c04e73a69367ca8",
            ExternalId = "2dad73521f7b4d029c04e73a69367ca8",
            Title = "Utwor Spotify",
            Kind = MediaItemKind.Track,
            IsPlayNext = true
        };
        var snapshot = TransientQueuePersistence.Capture("spotify", [plain, enginePrefixed], storedOrder: null);
        Assert(
            snapshot.StorageOrder.Count == 2,
            "Sesja Spotify identyfikuje pozycje po Id; poprawka TIDAL-a nie moze scalac roznych Id. "
                + $"Zmierzono StorageOrder=[{string.Join(", ", snapshot.StorageOrder)}].");
        Assert(
            snapshot.RegularItemIds.SequenceEqual([plain.Id]),
            $"Spotify: Regular=[{string.Join(", ", snapshot.RegularItemIds)}] zamiast samego golego Id.");
        Assert(
            snapshot.PlayNextItemIds.SequenceEqual([enginePrefixed.Id]),
            $"Spotify: PlayNext=[{string.Join(", ", snapshot.PlayNextItemIds)}] zamiast samego Id z prefiksem.");
    }

    /// <summary>
    /// Celowe usuniecie z kolejki i zdjecie priorytetu NIE moze wrocic po
    /// ponownym zapisie i odczycie - nawet gdy drugie wystapienie utworu
    /// istnieje w sesji.
    /// </summary>
    private static void CeloweUsuniecieNieWraca()
    {
        var collectionRow = new MediaItem
        {
            Id = "tidal:tracks:55",
            ExternalId = "tracks:55",
            Title = "Do usuniecia",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };
        var playlistEntryRow = new MediaItem
        {
            Id = "tidal:tracks:55:entry:z1",
            ExternalId = "tracks:55",
            ContainerEntryId = "z1",
            Title = "Do usuniecia",
            Kind = MediaItemKind.Track,
            IsPlayNext = true
        };
        var keptRow = new MediaItem
        {
            Id = "tidal:tracks:66",
            ExternalId = "tracks:66",
            Title = "Zostaje",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };
        var all = new[] { collectionRow, playlistEntryRow, keptRow };

        var first = TransientQueuePersistence.Capture(
            "tidal",
            all.Where(item => item.IsInQueue || item.IsPlayNext),
            storedOrder: null);

        // Uzytkownik zdejmuje priorytet (TogglePlayNext) i usuwa z kolejki
        // (AddQueue ponownie) - tak jak robi to CommandRouter na elementach
        // dzialania. Oba wystapienia przestaja byc czlonkami kolejki.
        playlistEntryRow.IsPlayNext = false;
        collectionRow.IsInQueue = false;

        var second = TransientQueuePersistence.Capture(
            "tidal",
            all.Where(item => item.IsInQueue || item.IsPlayNext),
            first.StorageOrder);
        Assert(
            second.StorageOrder.SequenceEqual(["tidal:tracks:66"]),
            "Po celowym usunieciu w zapisie ma zostac tylko zachowana pozycja; "
                + $"zmierzono [{string.Join(", ", second.StorageOrder)}].");
        Assert(
            !second.PlayNextItemIds.Contains("tidal:tracks:55")
                && !second.RegularItemIds.Contains("tidal:tracks:55"),
            "Usunieta pozycja nie moze wrocic jako czlonek kolejki.");

        var reloaded = new[]
        {
            new MediaItem { Id = "tidal:tracks:55", ExternalId = "tracks:55", Kind = MediaItemKind.Track },
            new MediaItem { Id = "tidal:tracks:55:entry:z2", ExternalId = "tracks:55", Kind = MediaItemKind.Track },
            new MediaItem { Id = "tidal:tracks:66", ExternalId = "tracks:66", Kind = MediaItemKind.Track }
        };
        TransientQueuePersistence.Restore(
            "tidal",
            reloaded,
            second.StorageOrder,
            second.RegularItemIds,
            second.PlayNextItemIds);
        Assert(
            reloaded.Count(item => item.IsInQueue || item.IsPlayNext) == 1,
            "Po restarcie ma wrocic dokladnie jedna pozycja kolejki; "
                + $"zmierzono {reloaded.Count(item => item.IsInQueue || item.IsPlayNext)}.");
        Assert(
            reloaded.Single(item => item.IsInQueue || item.IsPlayNext).Id == "tidal:tracks:66",
            "Po restarcie wrocic ma wylacznie pozycja, ktorej uzytkownik nie usunal.");
    }

    /// <summary>
    /// Celowe powtorzenia utworu na playliscie: liczebnosc zapisu i kontrakt
    /// pozostaja bez zmian (jeden klucz magazynu na utwor). Ta poprawka nie
    /// zmienia decyzji projektowej o powtorzeniach.
    /// </summary>
    private static void PowtorzeniaNaPlylistcieNieZmienialyLiczebnosci()
    {
        var firstOccurrence = new MediaItem
        {
            Id = "tidal:tracks:9:entry:a",
            ExternalId = "tracks:9",
            ContainerEntryId = "a",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };
        var secondOccurrence = new MediaItem
        {
            Id = "tidal:tracks:9:entry:b",
            ExternalId = "tracks:9",
            ContainerEntryId = "b",
            Kind = MediaItemKind.Track,
            IsInQueue = true
        };
        var snapshot = TransientQueuePersistence.Capture(
            "tidal",
            [firstOccurrence, secondOccurrence],
            storedOrder: null);
        Assert(
            snapshot.StorageOrder.SequenceEqual(["tidal:tracks:9"]),
            "Kontrakt zapisu (jeden klucz na utwor) musi zostac bez zmian; "
                + $"zmierzono [{string.Join(", ", snapshot.StorageOrder)}].");
        Assert(
            snapshot.RuntimeOrder.Count == 1,
            $"Liczebnosc kolejnosci wykonawczej bez zmian; zmierzono {snapshot.RuntimeOrder.Count}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
