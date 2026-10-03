using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// POMIAR trzech nowych zachowan "Moich stacji" i skumulowanej glosnosci.
/// Zero sieci, zero UI: tu mierzymy wylacznie logike kolejnosci i bufora.
/// </summary>
internal static class SonosOwnStreamsOrderTests
{
    internal static void Run()
    {
        var checks = 0;
        checks += SortowanieNieGubiDanych();
        checks += MigracjaNieOdwracaStacji();
        checks += PrzesuwanieAltStrzalkami();
        checks += WycinanieIWklejanie();
        checks += ZapisIOdczytKolejnosci();
        checks += SkumulowanaGlosnosc();
        checks += SwiezyOdczytPrzedSpacja();
        Console.WriteLine($"OK: Moje stacje Sonosa - sortowanie, przenoszenie, trwałość i powtórzenia głośności ({checks} sprawdzeń)");
    }

    private static List<SonosOwnStreamSettings> Stations() =>
    [
        new() { Id = "s1", Name = "Zet", StreamUrl = "https://a.invalid/1?x=A%2Fb" },
        new() { Id = "s2", Name = "Antyradio", StreamUrl = "https://a.invalid/2" },
        new() { Id = "s3", Name = "Meloradio", StreamUrl = "https://a.invalid/3" }
    ];

    private static int SortowanieNieGubiDanych()
    {
        var orders = new CollectionOrderSettings();
        var stations = Stations();

        var alpha = SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Alphabetical);
        Check(alpha.Select(s => s.Id).SequenceEqual(["s2", "s3", "s1"]), "Alt+2 nie ułożyło alfabetycznie");
        Check(alpha.Count == stations.Count, "Sortowanie zgubiło stację");
        Check(alpha.Single(s => s.Id == "s1").StreamUrl == "https://a.invalid/1?x=A%2Fb",
            "Sortowanie zmieniło dosłowny adres");

        var added = SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.AddedNewest);
        Check(added.Select(s => s.Id).SequenceEqual(["s3", "s2", "s1"]), "Alt+1 nie dało najnowszych na początku");

        var custom = SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom);
        Check(custom.Select(s => s.Id).SequenceEqual(["s1", "s2", "s3"]),
            "Alt+3 na starcie zmieniło kolejność zamiast ją zachować");
        Check(SonosOwnStreamsOrder.DescribeMode(CollectionSortMode.Custom) == "Kolejność własna",
            "Zmieniono krótką etykietę trybu");
        return 6;
    }

    private static int MigracjaNieOdwracaStacji()
    {
        // STARE USTAWIENIA: zadnej zapisanej kolejnosci. Pierwsze wejscie musi
        // pokazac liste TAK, JAK BYLA - dopiero jawny Alt+1 odwraca.
        var orders = new CollectionOrderSettings();
        var stations = Stations();
        var custom = SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom);
        Check(custom.Select(s => s.Id).SequenceEqual(["s1", "s2", "s3"]), "Migracja odwróciła stacje bez Alt+1");

        // DOPISANIE nowej stacji nie przestawia starych i laduje na koncu.
        stations.Add(new() { Id = "s4", Name = "Nowa", StreamUrl = "https://a.invalid/4" });
        var afterAdd = SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom);
        Check(afterAdd.Select(s => s.Id).SequenceEqual(["s1", "s2", "s3", "s4"]), "Dodanie przestawiło istniejące stacje");

        // USUNIECIE znika z kolejnosci i nie zostawia duchow.
        stations.RemoveAll(s => s.Id == "s2");
        var afterRemove = SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom);
        Check(afterRemove.Select(s => s.Id).SequenceEqual(["s1", "s3", "s4"]), "Usunięcie zepsuło kolejność");
        Check(!orders.LibraryItemIdsBySession[SonosOwnStreamsOrder.StorageKey].Contains("s2"), "Usunięta stacja została w zapisie kolejności");
        return 4;
    }

    private static int PrzesuwanieAltStrzalkami()
    {
        var orders = new CollectionOrderSettings();
        var stations = Stations();
        SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom);

        Check(SonosOwnStreamsOrder.Move(orders, stations, ["s3"], -1) == SonosOwnStreamsOrderResult.Moved,
            "Alt+strzałka w górę nie przesunęła stacji");
        Check(SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom)
            .Select(s => s.Id).SequenceEqual(["s1", "s3", "s2"]), "Przesunięcie dało zły wynik");

        Check(SonosOwnStreamsOrder.Move(orders, stations, ["s1"], -1) == SonosOwnStreamsOrderResult.Boundary,
            "Brzeg listy nie został rozpoznany");
        Check(SonosOwnStreamsOrder.Move(orders, stations, ["s1", "s2"], -1)
            == SonosOwnStreamsOrderResult.NonContiguousSelection, "Dziurawe zaznaczenie przeszło jako blok");
        Check(SonosOwnStreamsOrder.Move(orders, stations, [], 1) == SonosOwnStreamsOrderResult.InvalidSelection,
            "Pusty wybór nie został odrzucony");
        Check(SonosOwnStreamsOrder.Move(orders, [], ["s1"], 1) == SonosOwnStreamsOrderResult.InvalidSelection,
            "Pusta lista nie została odrzucona");

        // CIAGLY BLOK dwoch stacji przesuwa sie razem.
        Check(SonosOwnStreamsOrder.Move(orders, stations, ["s1", "s3"], 1) == SonosOwnStreamsOrderResult.Moved,
            "Ciągły blok nie dał się przesunąć");
        Check(SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom)
            .Select(s => s.Id).SequenceEqual(["s2", "s1", "s3"]), "Blok przesunął się źle");
        return 8;
    }

    private static int WycinanieIWklejanie()
    {
        var orders = new CollectionOrderSettings();
        var stations = Stations();
        SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom);

        Check(SonosOwnStreamsOrder.PlaceBefore(orders, stations, ["s3"], "s1") == SonosOwnStreamsOrderResult.Moved,
            "Ctrl+V nie przeniosło stacji przed cel");
        Check(SonosOwnStreamsOrder.Arrange(orders, stations, CollectionSortMode.Custom)
            .Select(s => s.Id).SequenceEqual(["s3", "s1", "s2"]), "Wklejenie trafiło w złe miejsce");
        Check(stations.Count == 3, "Ctrl+X skasował stację zamiast ją przenieść");

        Check(SonosOwnStreamsOrder.PlaceBefore(orders, stations, ["s3"], "s3")
            == SonosOwnStreamsOrderResult.TargetInSelection, "Własny cel nie został rozpoznany");
        Check(SonosOwnStreamsOrder.PlaceBefore(orders, stations, ["s3"], null)
            == SonosOwnStreamsOrderResult.InvalidSelection, "Brak celu nie został odrzucony");
        Check(SonosOwnStreamsOrder.PlaceBefore(orders, stations, ["s3"], "nie-ma")
            == SonosOwnStreamsOrderResult.TargetMissing, "Nieistniejący cel nie został rozpoznany");

        // STACJA USUNIETA MIEDZY Ctrl+X a Ctrl+V: zrodla juz nie ma.
        var mniej = stations.Where(s => s.Id != "s3").ToList();
        Check(SonosOwnStreamsOrder.PlaceBefore(orders, mniej, ["s3"], "s1")
            == SonosOwnStreamsOrderResult.InvalidSelection, "Wycięta i usunięta stacja udała przeniesienie");

        // TEN SAM cel co obecna pozycja: nic sie nie zmienia i trzeba to powiedziec.
        var orders2 = new CollectionOrderSettings();
        var stations2 = Stations();
        SonosOwnStreamsOrder.Arrange(orders2, stations2, CollectionSortMode.Custom);
        Check(SonosOwnStreamsOrder.PlaceBefore(orders2, stations2, ["s1"], "s2")
            == SonosOwnStreamsOrderResult.Unchanged, "Przeniesienie w to samo miejsce udało zmianę");
        return 8;
    }

    private static int ZapisIOdczytKolejnosci()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-own-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            var store = new ConfigurationStore(path);
            var state = store.LoadOrCreate();
            state.Sonos.OwnStreams = Stations();
            SonosOwnStreamsOrder.Arrange(
                state.CollectionOrders, state.Sonos.OwnStreams, CollectionSortMode.Custom);
            Check(SonosOwnStreamsOrder.Move(
                state.CollectionOrders, state.Sonos.OwnStreams, ["s3"], -1)
                == SonosOwnStreamsOrderResult.Moved, "Przesunięcie przed zapisem nie zadziałało");
            store.Save(state);

            // PRAWDZIWA trwalosc: nowy sklep, nowy odczyt z dysku.
            var restored = new ConfigurationStore(path).LoadOrCreate();
            var order = SonosOwnStreamsOrder.Arrange(
                restored.CollectionOrders, restored.Sonos.OwnStreams, CollectionSortMode.Custom);
            Check(order.Select(s => s.Id).SequenceEqual(["s1", "s3", "s2"]),
                "Kolejność własna nie przetrwała ponownego odczytu konfiguracji");
            Check(restored.Sonos.OwnStreams.Single(s => s.Id == "s1").StreamUrl == "https://a.invalid/1?x=A%2Fb",
                "Zapis kolejności zmienił dosłowny adres stacji");

            // TRWALOSC TRYBU tam, gdzie go naprawde trzyma okno glowne: w stanie
            // nawigacji sesji, a nie w osobnym, rownoleglym magazynie. Sam label
            // w oknie niczego nie dowodzi - dowodem jest ODCZYT Z DYSKU.
            var navigation = restored.SessionNavigation.Sessions.TryGetValue("sonos", out var existing)
                ? existing
                : restored.SessionNavigation.Sessions["sonos"] = new SessionNavigationState();
            navigation.CollectionSortModes[SonosOwnStreamsOrder.ViewName] = CollectionSortMode.Alphabetical;
            new ConfigurationStore(path).Save(restored);
            var afterMode = new ConfigurationStore(path).LoadOrCreate();
            Check(afterMode.SessionNavigation.Sessions["sonos"].CollectionSortModes
                    .GetValueOrDefault(SonosOwnStreamsOrder.ViewName) == CollectionSortMode.Alphabetical,
                "Tryb sortowania Moich stacji nie przetrwał ponownego odczytu konfiguracji");
            Check(SonosOwnStreamsOrder.Arrange(
                    afterMode.CollectionOrders, afterMode.Sonos.OwnStreams, CollectionSortMode.Custom)
                .Select(s => s.Id).SequenceEqual(["s1", "s3", "s2"]),
                "Zmiana trybu zgubiła zapisaną kolejność własną");
            return 6;
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static int SkumulowanaGlosnosc()
    {
        // KROK z MAPOWANIA, nie z zalozenia: dodatek NVDA wysyla volumeDown,
        // ktory NvdaCommandServer tlumaczy na VolumeDown5.
        Check(SonosVolumeRepeatBuffer.StepDelta(CommandIds.VolumeDown5) == -5, "Zły krok dla volumeDown");
        Check(SonosVolumeRepeatBuffer.StepDelta(CommandIds.VolumeDown1) == -1, "Zły krok dla kroku o 1");
        Check(!SonosVolumeRepeatBuffer.IsVolumeStep(CommandIds.PlayPause), "Pauza uznana za krok głośności");

        var buffer = new SonosVolumeRepeatBuffer();
        // Nic nie trwa: powtorzenie ma isc zwykla droga odmowy.
        Check(!buffer.TryAccumulate(CommandIds.VolumeDown5, "GROUP:1"), "Bufor przyjął krok bez trwającej operacji");

        buffer.Begin("GROUP:1");
        // SERIA jak w logu: 12 nacisniec w czasie jednego trwajacego SetVolume.
        for (var i = 0; i < 12; i++)
            Check(buffer.TryAccumulate(CommandIds.VolumeDown5, "GROUP:1"), $"Zgubiono powtórzenie numer {i + 1}");
        Check(buffer.HasPending, "Seria powtórzeń nie została zapamiętana");
        // JEDNA liczba, nie kolejka zadan: 12 krokow to jeden docelowy poziom.
        Check(buffer.PendingDelta == -60, "Powtórzenia nie zwinęły się w jedną docelową głośność");

        // INNA GRUPA w czasie oczekiwania: intencja tam nie leci.
        Check(!buffer.TryAccumulate(CommandIds.VolumeDown5, "GROUP:2"), "Intencja przeszła na inną grupę");
        // INNE POLECENIE nadal dostaje jawna odmowe - pauzy sie nie kumuluje.
        Check(!buffer.TryAccumulate(CommandIds.PlayPause, "GROUP:1"), "Pauza weszła do bufora głośności");

        Check(buffer.TakePendingDelta("GROUP:1") == -60, "Nie oddano nagromadzonej intencji");
        Check(!buffer.HasPending, "Intencja została oddana dwa razy");
        Check(buffer.TakePendingDelta("GROUP:1") == 0, "Pusty bufor oddał deltę");

        // ZMIANA CELU miedzy odbiorami porzuca intencje zamiast wysylac ja gdzie indziej.
        buffer.TryAccumulate(CommandIds.VolumeUp5, "GROUP:1");
        Check(buffer.TakePendingDelta("GROUP:9") == 0, "Intencja poszła do innej grupy przy odbiorze");
        Check(!buffer.HasPending, "Porzucona intencja została w buforze");

        // GRANICE 0 i 100 to poprawne wartosci, nie blad.
        Check(SonosVolumeRepeatBuffer.ResolveTarget(10, -60) == 0, "Dolna granica głośności policzona źle");
        Check(SonosVolumeRepeatBuffer.ResolveTarget(80, 60) == 100, "Górna granica głośności policzona źle");

        // Petla jest OGRANICZONA: po MaxDrains dolaczaniach konczymy.
        var limited = new SonosVolumeRepeatBuffer();
        limited.Begin("GROUP:1");
        var drains = 0;
        for (var i = 0; i < SonosVolumeRepeatBuffer.MaxDrains + 5; i++)
        {
            limited.TryAccumulate(CommandIds.VolumeDown5, "GROUP:1");
            if (limited.TakePendingDelta("GROUP:1") != 0) drains++;
        }
        Check(drains == SonosVolumeRepeatBuffer.MaxDrains, "Kolejka dosyłania okazała się nieograniczona");

        limited.End();
        Check(!limited.TryAccumulate(CommandIds.VolumeDown5, "GROUP:1"), "Bufor przyjmuje kroki po zakończeniu operacji");
        return 17;
    }

    private static int SwiezyOdczytPrzedSpacja()
    {
        // ZGLOSZENIE: pierwsza Spacja po uruchomieniu wlasnej stacji mowila
        // "Sonos nie zglasza mozliwosci zatrzymania tego materialu", a druga juz
        // dzialala. Tutaj mierzymy DECYZJE, czy przed odmowa nalezy odczytac stan.
        var buforowanie = new SonosPlaybackActions(
            canPlay: null, canSkip: null, canSkipBack: null, canSkipToPrevious: null,
            canSeek: null, canPause: null, canStop: null, canRepeat: null,
            canRepeatOne: null, canCrossfade: null, canShuffle: null);
        var graZeStopem = new SonosPlaybackActions(
            canPlay: true, canSkip: false, canSkipBack: false, canSkipToPrevious: false,
            canSeek: false, canPause: false, canStop: true, canRepeat: false,
            canRepeatOne: false, canCrossfade: false, canShuffle: false);

        // DOKLADNIE ten przypadek z relacji: kopia z chwili buforowania nie zna
        // ani canPause, ani canStop - i wlasnie ona odmawiala kategorycznie.
        var zCache = SonosCommandGating.Evaluate(
            CommandIds.PlayPause, SonosPlaybackState.Buffering, buforowanie, null);
        Check(!zCache.Allowed, "Buforowanie bez uprawnień nie powinno samo przepuszczać Spacji");
        Check(SonosPlayPauseRefresh.NeedsFreshRead(
                CommandIds.PlayPause, zCache.Allowed, SonosPlaybackState.Buffering, buforowanie),
            "Spacja odmówiła ze starej kopii z buforowania zamiast odczytać stan na nowo");

        // Stan nieodczytany to tez nie dowod odmowy.
        Check(SonosPlayPauseRefresh.NeedsFreshRead(
                CommandIds.PlayPause, false, SonosPlaybackState.Unknown, null),
            "Brak odczytanych uprawnień nie wymusił świeżego odczytu");

        // SZYBKA SCIEZKA: pewna zgoda na stanie ustalonym idzie prosto do POST-u.
        var graPewnie = SonosCommandGating.Evaluate(
            CommandIds.PlayPause, SonosPlaybackState.Playing, graZeStopem, null);
        Check(graPewnie.Allowed, "Granie z canStop musi przepuszczać Spację");
        Check(!SonosPlayPauseRefresh.NeedsFreshRead(
                CommandIds.PlayPause, graPewnie.Allowed, SonosPlaybackState.Playing, graZeStopem),
            "Pewna zgoda nie powinna dokładać kolejnego odczytu");

        // KROKI GLOSNOSCI zostaja szybkie: zadnego dodatkowego GET.
        Check(!SonosPlayPauseRefresh.NeedsFreshRead(
                CommandIds.VolumeDown5, false, SonosPlaybackState.Buffering, buforowanie),
            "Ściszanie dostało niepotrzebny odczyt stanu");
        Check(!SonosPlayPauseRefresh.NeedsFreshRead(
                CommandIds.VolumeUp5, false, SonosPlaybackState.Unknown, null),
            "Pogłaśnianie dostało niepotrzebny odczyt stanu");

        // ODMOWA PO SWIEZYM ODCZYCIE ZOSTAJE ODMOWA - nie udajemy zgody.
        var reklama = new SonosPlaybackActions(
            canPlay: false, canSkip: false, canSkipBack: false, canSkipToPrevious: false,
            canSeek: false, canPause: false, canStop: false, canRepeat: false,
            canRepeatOne: false, canCrossfade: false, canShuffle: false);
        var poOdczycie = SonosCommandGating.Evaluate(
            CommandIds.PlayPause, SonosPlaybackState.Playing, reklama, null);
        Check(!poOdczycie.Allowed, "Brak canPause i canStop po świeżym odczycie musi zostać odmową");
        Check(!string.IsNullOrWhiteSpace(poOdczycie.Refusal), "Odmowa musi mieć komunikat");

        // Nieudany odczyt mowi PRAWDE: polecenia nie wyslano.
        Check(SonosPlayPauseRefresh.ReadFailed.Contains("nie wysłano", StringComparison.Ordinal),
            "Komunikat nieudanego odczytu musi mówić, że polecenia nie wysłano");
        return 10;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
