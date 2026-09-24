using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;

/// <summary>
/// Pilnuje KOSZTU i WIERNOSCI publicznego <c>ConfigurationStore.CloneState</c>.
///
/// Dlaczego akurat alokacje, a nie stoper: migawka powstaje na watku UI, a
/// zmierzone zaciecia braly sie z tego, ze pelny model byl serializowany do
/// JSON i parsowany z powrotem. Stoper na maszynie budujacej jest kruchy
/// (rdzenie, throttling, GC innych testow), natomiast liczba zaalokowanych
/// bajtow dla ustalonego modelu jest powtarzalna co do dziesiatych czesci MiB.
/// Dlatego budzet jest wyrazony w bajtach na alokacjach watku.
///
/// Ten zestaw NIE twierdzi, ze usuwa wszystkie zaciecia interfejsu. Mierzy
/// wylacznie koszt samego CloneState dla modelu zbudowanego w tescie.
/// </summary>
internal static class CloneStateCostTests
{
    // Skala dobrana tak, by test byl krotki, a jednocześnie rozroznial stara i
    // nowa implementacje z ogromnym zapasem (patrz komentarz przy budzecie).
    private const int Episodes = 3000;
    private const int Subscriptions = 200;
    private const int LocalItems = 2000;
    private const int SpotifyItems = 2000;
    private const int TidalItems = 400;
    private const int Bookmarks = 500;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Run()
    {
        TestAllocationBudget();
        TestFullJsonFidelityOnRichModel();
        TestEmptyAndDefaultModel();
        TestDeepIndependenceBothDirections();
        TestDictionaryComparersSurvive();
        TestEveryPropertyIsCopied();
        TestModelHasNoHiddenState();
        TestUnsupportedShapesAreRefused();
        TestSaveReloadRoundTripMatches();
    }

    // ---------------------------------------------------------------- koszt

    private static void TestAllocationBudget()
    {
        using var workspace = new TemporaryWorkspace();
        var store = workspace.Store;
        var state = BuildRichModel(store.LoadOrCreate());

        // Rozgrzewka: JIT, tablice typow, bufory serializatora. Bez niej
        // pierwszy przebieg obciazalby wynik kosztem jednorazowym.
        for (var i = 0; i < 3; i++) store.CloneState(state);

        // Mediana z kilku prob. Alokacje sa deterministyczne, ale mediana
        // chroni przed pojedynczym przebiegiem zaklóconym przez inny watek.
        var samples = new long[5];
        for (var i = 0; i < samples.Length; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var clone = store.CloneState(state);
            var after = GC.GetAllocatedBytesForCurrentThread();
            samples[i] = after - before;
            // Uzycie wyniku, zeby JIT nie usunal wywolania jako martwego.
            Check(clone.Podcasts.Episodes.Count == Episodes, "klon zgubil odcinki w pomiarze kosztu");
        }
        Array.Sort(samples);
        var median = samples[samples.Length / 2];

        // Zmierzone na tym modelu na tej samej maszynie: stara implementacja
        // (pelny obieg JSON) alokuje 13,4 MiB, nowa 1,7 MiB, za kazdym razem z
        // powtarzalnoscia do 0,1 MiB. Budzet 6 MiB lezy miedzy nimi z zapasem
        // ponad dwukrotnym w obie strony, wiec test nie jest ani kruchy, ani
        // pusty: stara implementacja przekracza go ponad dwukrotnie.
        const long Budget = 6L * 1024 * 1024;
        Check(
            median < Budget,
            $"CloneState alokuje {median / 1048576.0:F1} MiB na modelu testowym, " +
            $"budzet to {Budget / 1048576.0:F1} MiB. Pelny JSON w obie strony na watku UI " +
            "jest wlasnie tym kosztem, ktory ta zmiana usuwa.");
    }

    // ------------------------------------------------------------- wiernosc

    private static void TestFullJsonFidelityOnRichModel()
    {
        using var workspace = new TemporaryWorkspace();
        var store = workspace.Store;
        var state = BuildRichModel(store.LoadOrCreate());

        var before = JsonSerializer.Serialize(state, Json);
        var clone = store.CloneState(state);
        var cloneJson = JsonSerializer.Serialize(clone, Json);
        var after = JsonSerializer.Serialize(state, Json);

        Check(cloneJson == before, "pelny JSON klonu rozni sie od zrodla na bogatym modelu");
        Check(after == before, "CloneState zmienil model zrodlowy");
    }

    private static void TestEmptyAndDefaultModel()
    {
        using var workspace = new TemporaryWorkspace();
        var store = workspace.Store;

        var fresh = store.LoadOrCreate();
        var freshJson = JsonSerializer.Serialize(fresh, Json);
        Check(
            JsonSerializer.Serialize(store.CloneState(fresh), Json) == freshJson,
            "klon swiezego, domyslnego modelu rozni sie od zrodla");

        // Model nietypowy: puste kolekcje podmienione na nowe instancje oraz
        // jawne null tam, gdzie typ na to pozwala.
        var odd = ConfigurationStore.CreateDefaultState();
        odd.KeyboardProfiles = new List<KeyboardProfile>();
        odd.Podcasts.Episodes = new List<PodcastEpisodeSettings>();
        odd.Podcasts.Subscriptions = new List<PodcastSubscriptionSettings>();
        odd.Podcasts.CurrentItemId = null;
        odd.Podcasts.DownloadsFolder = null;
        var oddJson = JsonSerializer.Serialize(odd, Json);
        Check(
            JsonSerializer.Serialize(store.CloneState(odd), Json) == oddJson,
            "klon pustego/nietypowego modelu rozni sie od zrodla");
    }

    // --------------------------------------------------------- niezaleznosc

    private static void TestDeepIndependenceBothDirections()
    {
        using var workspace = new TemporaryWorkspace();
        var store = workspace.Store;
        var state = BuildRichModel(store.LoadOrCreate());
        var clone = store.CloneState(state);

        // Same listy nie moga byc tym samym obiektem...
        Check(!ReferenceEquals(clone.Podcasts.Episodes, state.Podcasts.Episodes), "lista odcinkow wspoldzielona");
        Check(!ReferenceEquals(clone.LocalMedia.Items, state.LocalMedia.Items), "lista plikow lokalnych wspoldzielona");
        Check(!ReferenceEquals(clone.Spotify.CachedCollectionItems, state.Spotify.CachedCollectionItems), "katalog Spotify wspoldzielony");
        Check(!ReferenceEquals(clone.Tidal.CachedCollectionItems, state.Tidal.CachedCollectionItems), "katalog TIDAL wspoldzielony");
        Check(!ReferenceEquals(clone.Bookmarks.Entries, state.Bookmarks.Entries), "zakladki wspoldzielone");
        Check(!ReferenceEquals(clone.Settings, state.Settings), "ustawienia wspoldzielone");
        Check(!ReferenceEquals(clone.PlaybackHistory.ItemIdsBySession, state.PlaybackHistory.ItemIdsBySession), "historia wspoldzielona");

        // ...ale to za malo. Licza sie ELEMENTY w srodku, w OBU kierunkach.
        Check(!ReferenceEquals(clone.Podcasts.Episodes[0], state.Podcasts.Episodes[0]), "odcinek wspoldzielony jako obiekt");
        Check(!ReferenceEquals(clone.LocalMedia.Items[0], state.LocalMedia.Items[0]), "plik lokalny wspoldzielony jako obiekt");
        Check(!ReferenceEquals(clone.Spotify.CachedCollectionItems[0], state.Spotify.CachedCollectionItems[0]), "pozycja Spotify wspoldzielona jako obiekt");

        // Zrodlo -> klon
        state.Podcasts.Episodes[0].Title = "ZMIANA W ZRODLE";
        state.Podcasts.Episodes.Add(new PodcastEpisodeSettings { Id = "dodany-w-zrodle" });
        state.LocalMedia.Items[0].Title = "ZMIANA W ZRODLE";
        state.Spotify.CachedCollectionItems[0].Title = "ZMIANA W ZRODLE";
        state.Settings.PrefixChord = "Ctrl+Alt+Z";
        state.PlaybackHistory.ItemIdsBySession["lokalne"].Add("dodany-w-zrodle");
        state.PlaybackHistory.ItemIdsBySession["nowa-sesja-zrodla"] = new List<string> { "x" };
        state.Bookmarks.Entries[0].Name = "ZMIANA W ZRODLE";

        Check(clone.Podcasts.Episodes[0].Title != "ZMIANA W ZRODLE", "zmiana tytulu odcinka w zrodle dotknela klon");
        Check(clone.Podcasts.Episodes.Count == Episodes, "dodanie odcinka w zrodle dotknelo klon");
        Check(clone.LocalMedia.Items[0].Title != "ZMIANA W ZRODLE", "zmiana pliku lokalnego w zrodle dotknela klon");
        Check(clone.Spotify.CachedCollectionItems[0].Title != "ZMIANA W ZRODLE", "zmiana pozycji Spotify w zrodle dotknela klon");
        Check(clone.Settings.PrefixChord != "Ctrl+Alt+Z", "zmiana ustawien w zrodle dotknela klon");
        Check(!clone.PlaybackHistory.ItemIdsBySession["lokalne"].Contains("dodany-w-zrodle"), "dopisanie do listy w slowniku zrodla dotknelo klon");
        Check(!clone.PlaybackHistory.ItemIdsBySession.ContainsKey("nowa-sesja-zrodla"), "nowy klucz w slowniku zrodla dotknal klon");
        Check(clone.Bookmarks.Entries[0].Name != "ZMIANA W ZRODLE", "zmiana zakladki w zrodle dotknela klon");

        // Klon -> zrodlo
        clone.Podcasts.Episodes[1].Title = "ZMIANA W KLONIE";
        clone.LocalMedia.Items[1].Title = "ZMIANA W KLONIE";
        clone.Spotify.CachedCollectionItems[1].Title = "ZMIANA W KLONIE";
        clone.Settings.PrefixChord = "Ctrl+Alt+K";
        clone.PlaybackHistory.ItemIdsBySession["lokalne"].Add("dodany-w-klonie");
        clone.PlaybackHistory.ItemIdsBySession["nowa-sesja-klonu"] = new List<string> { "y" };
        clone.Tidal.CachedCollectionItems[0].Title = "ZMIANA W KLONIE";

        Check(state.Podcasts.Episodes[1].Title != "ZMIANA W KLONIE", "zmiana odcinka w klonie dotknela zrodlo");
        Check(state.LocalMedia.Items[1].Title != "ZMIANA W KLONIE", "zmiana pliku lokalnego w klonie dotknela zrodlo");
        Check(state.Spotify.CachedCollectionItems[1].Title != "ZMIANA W KLONIE", "zmiana pozycji Spotify w klonie dotknela zrodlo");
        Check(state.Settings.PrefixChord != "Ctrl+Alt+K", "zmiana ustawien w klonie dotknela zrodlo");
        Check(!state.PlaybackHistory.ItemIdsBySession["lokalne"].Contains("dodany-w-klonie"), "dopisanie w klonie dotknelo zrodlo");
        Check(!state.PlaybackHistory.ItemIdsBySession.ContainsKey("nowa-sesja-klonu"), "nowy klucz w klonie dotknal zrodlo");
        Check(state.Tidal.CachedCollectionItems[0].Title != "ZMIANA W KLONIE", "zmiana pozycji TIDAL w klonie dotknela zrodlo");
    }

    private static void TestDictionaryComparersSurvive()
    {
        using var workspace = new TemporaryWorkspace();
        var store = workspace.Store;
        var state = store.LoadOrCreate();
        state.PlaybackHistory.ItemIdsBySession["Lokalne"] = new List<string> { "a" };

        var clone = store.CloneState(state);

        // Model celowo uzywa slownikow bez rozroznienia wielkosci liter.
        // Migawka, na ktorej pracuje watek zapisu (i punkty kontrolne
        // odtwarzania), musi zachowywac sie tak samo jak stan zywy.
        Check(
            state.PlaybackHistory.ItemIdsBySession.ContainsKey("lokalne"),
            "zalozenie testu: zrodlowy slownik ignoruje wielkosc liter");
        Check(
            clone.PlaybackHistory.ItemIdsBySession.ContainsKey("lokalne"),
            "klon zgubil komparator slownika (trafienie zalezy od wielkosci liter)");
        Check(
            clone.CollectionOrders.FavoriteItemIdsBySession.Comparer.Equals(
                state.CollectionOrders.FavoriteItemIdsBySession.Comparer),
            "klon zgubil komparator slownika kolejnosci kolekcji");
    }

    /// <summary>
    /// Straznik na przyszlosc. Recznie pisane kopiowanie pol cicho gubi kazde
    /// NOWE pole dodane do modelu. Ten test wypelnia KAZDA publiczna wlasciwosc
    /// w calym domknieciu typow wartoscia inna od domyslnej i sprawdza, ze
    /// przetrwala klonowanie. Gdy ktos doda pole i nie obsluzy go w kopiowaniu,
    /// test wskaze je z nazwy.
    /// </summary>
    private static void TestEveryPropertyIsCopied()
    {
        using var workspace = new TemporaryWorkspace();
        var store = workspace.Store;

        var state = ConfigurationStore.CreateDefaultState();
        var filled = new List<string>();
        FillDistinctively(state, typeof(PersistedState), "PersistedState", filled, 0);

        // ROZLICZENIE, nie prog. Kazda wlasciwosc modelu z publicznym ustawiaczem
        // musi trafic na liste wypelnionych; wlasciwosci bez ustawiacza musza byc
        // wymienione swiadomie. Sam prog "wiecej niz 150" przepuszczal cala
        // galez modelu pominieta po cichu przez pomocnika.
        Check(
            filled.Contains("PersistedState.Settings.SessionSlots{}"),
            "straznik nie wypelnil slownika o kluczu nie-string (Settings.SessionSlots, Dictionary<int, string>); " +
            "pomocnik cicho pomijal takie slowniki, wiec ich wartosci i komparator nie byly sprawdzane");

        var expected = JsonSerializer.Serialize(state, Json);
        var actual = JsonSerializer.Serialize(store.CloneState(state), Json);
        if (expected != actual)
        {
            var at = FirstDifference(expected, actual);
            throw new InvalidOperationException(
                "klon zgubil lub zmienil wartosc wlasciwosci modelu. Pierwsza roznica: " + at);
        }
    }

    // ----------------------------------------------- brak ukrytego stanu w modelu

    /// <summary>
    /// TEST KSZTALTU MODELU, nie zachowania kopii. Zamyka luke, ktorej nie widzi
    /// ani porownanie JSON, ani straznik wlasciwosci: PUBLICZNE POLE.
    ///
    /// Domyslny System.Text.Json NIE serializuje publicznych pol (bez
    /// IncludeFields albo [JsonInclude]), a StateSnapshotCopier kopiuje tylko
    /// wlasciwosci. Publiczne pole byloby wiec pominiete PO OBU STRONACH
    /// porownania i nie zglosilby go zaden inny test - a mimo to gubiloby stan
    /// migawki. Dlatego sprawdzamy sam kszalt typow modelu: zadne publiczne pole
    /// (poza stalymi, ktore nie maja stanu instancji).
    ///
    /// Atrybuty JSON przy polu pokazujemy dodatkowo w diagnostyce; sama nazwa
    /// atrybutu nie rozstrzyga, czy serializer wlaczy pole do zapisu. Test NIE
    /// zmienia formatu, tylko wymaga swiadomej decyzji o nowym ksztalcie.
    /// </summary>
    private static void TestModelHasNoHiddenState()
    {
        var visited = new HashSet<Type>();
        var offenders = new List<string>();
        CollectHiddenState(typeof(PersistedState), visited, offenders, "PersistedState");

        Check(
            offenders.Count == 0,
            "model migawki zawiera stan, ktorego kopia nie przenosi: " + string.Join("; ", offenders) +
            ". StateSnapshotCopier kopiuje WYLACZNIE publiczne wlasciwosci z ustawiaczem. " +
            "Zamien takie pole na wlasciwosc { get; set; } albo swiadomie ogranicz gwarancje migawki.");
    }

    private static void CollectHiddenState(Type type, HashSet<Type> visited, List<string> offenders, string path)
    {
        if (!visited.Add(type)) return;

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            // Pola generowane przez kompilator (backing fields) sa prywatne, wiec
            // tu nie trafiaja. Liczy sie tylko pole napisane recznie.
            var marked = field.GetCustomAttributes()
                .Any(attribute => attribute.GetType().Name is "JsonIncludeAttribute" or "JsonPropertyNameAttribute");
            offenders.Add($"{path}.{field.Name} ({field.FieldType.Name}) to publiczne POLE, nie wlasciwosc" +
                (marked ? " i jest oznaczone atrybutem JSON, co wymaga sprawdzenia kontraktu zapisu" : string.Empty));
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            foreach (var candidate in RelevantModelTypes(property.PropertyType))
            {
                CollectHiddenState(candidate, visited, offenders, path + "." + property.Name);
            }
        }
    }

    /// <summary>
    /// Typy warte dalszego zejscia: wlasne klasy modelu oraz argumenty list i
    /// slownikow. Typy wbudowane i niezmienne pomijamy - nie maja wlasnego stanu
    /// modelu do zgubienia.
    /// </summary>
    private static IEnumerable<Type> RelevantModelTypes(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsGenericType)
        {
            foreach (var argument in underlying.GetGenericArguments())
            {
                foreach (var nested in RelevantModelTypes(argument)) yield return nested;
            }
            yield break;
        }
        if (underlying.IsClass && underlying != typeof(string)
            && underlying.Namespace?.StartsWith("AccessibleMediaController", StringComparison.Ordinal) == true)
        {
            yield return underlying;
        }
    }

    // -------------------------------------------- odmowa nieobslugiwanych ksztaltow

    /// <summary>
    /// Kopiowacz ma ODMAWIAC jawnie, a nie oddawac cicho plytka albo pusta
    /// kopie. Aktualny model nie zawiera zadnego z ponizszych ksztaltow, wiec
    /// bez tego testu kontrakt bylby wylacznie deklaracja w komentarzu.
    ///
    /// Dlaczego wlasnie te trzy: <c>HashSet&lt;T&gt;</c> byl obslugiwany plytko
    /// (elementy wspoldzielone), slownik o kluczu mutowalnym wspoldzielilby
    /// klucz miedzy stanem zywym a migawka, a kazda inna kolekcja
    /// (<c>IList</c>, <c>ObservableCollection</c>, tablica) trafialaby do
    /// sciezki obiektu i dawala pusty wynik zamiast bledu.
    /// </summary>
    private static void TestUnsupportedShapesAreRefused()
    {
        ExpectRefusal(
            () => StateSnapshotCopier.Copy(new SetHolder { Tags = new HashSet<string> { "a" } }),
            "HashSet",
            "HashSet nie jest odrzucany: plytka kopia zbioru z mutowalnym elementem zlamalaby niezaleznosc migawki po cichu");

        ExpectRefusal(
            () => StateSnapshotCopier.Copy(new MutableKeyHolder
            {
                Map = new Dictionary<MutableKey, string> { [new MutableKey()] = "x" }
            }),
            "klucz",
            "slownik o mutowalnym kluczu nie jest odrzucany: klucz bylby WSPOLDZIELONY miedzy stanem zywym a migawka");

        ExpectRefusal(
            () => StateSnapshotCopier.Copy(new OtherCollectionHolder { Items = new Stack<string>() }),
            "Stack",
            "nieobslugiwana kolekcja nie jest odrzucana: trafia do sciezki obiektu i daje pusta kolekcje zamiast bledu");

        // Kontrola pozytywna: ksztalty, ktore model UZYWA, nadal przechodza.
        // Bez niej odmowa mogloby byc zrealizowana przez odrzucenie wszystkiego.
        var supported = StateSnapshotCopier.Copy(new SupportedShapes
        {
            ByString = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["A"] = "1" },
            ByInt = new Dictionary<int, string> { [3] = "trzy" },
            Items = new List<string> { "a" }
        });
        Check(supported.ByString.ContainsKey("a"), "kontrola pozytywna: slownik o kluczu string zgubil komparator");
        Check(supported.ByInt[3] == "trzy", "kontrola pozytywna: slownik o kluczu int nie przetrwal kopiowania");
        Check(supported.Items[0] == "a", "kontrola pozytywna: lista napisow nie przetrwala kopiowania");
    }

    /// <summary>
    /// Sprawdza, ze operacja ODMAWIA z czytelnym powodem. Odmowa powstaje przy
    /// budowie planu typu, czyli w statycznym konstruktorze — CLR opakowuje ja
    /// wtedy w <see cref="TypeInitializationException"/>. Szukamy wiec powodu
    /// takze w lancuchu wyjatkow wewnetrznych, bo to opakowanie jest faktem
    /// o kopiowaczu, nie luka w tescie.
    /// </summary>
    private static void ExpectRefusal(Action action, string expectedFragment, string failureMessage)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is InvalidOperationException
                    && current.Message.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            throw new InvalidOperationException(
                failureMessage + $" (zamiast jasnej odmowy z '{expectedFragment}' padlo: {exception.GetType().Name}: {exception.Message})",
                exception);
        }
        throw new InvalidOperationException(failureMessage);
    }

    private sealed class SetHolder { public HashSet<string> Tags { get; set; } = new(); }

    private sealed class MutableKey { public string Name { get; set; } = string.Empty; }

    private sealed class MutableKeyHolder { public Dictionary<MutableKey, string> Map { get; set; } = new(); }

    private sealed class OtherCollectionHolder { public Stack<string> Items { get; set; } = new(); }

    private sealed class SupportedShapes
    {
        public Dictionary<string, string> ByString { get; set; } = new();
        public Dictionary<int, string> ByInt { get; set; } = new();
        public List<string> Items { get; set; } = new();
    }

    // ------------------------------------------------------- zapis i odczyt

    private static void TestSaveReloadRoundTripMatches()
    {
        using var direct = new TemporaryWorkspace();
        using var viaClone = new TemporaryWorkspace();

        var state = BuildRichModel(direct.Store.LoadOrCreate());

        // Sciezka produkcyjna: zapis dostaje MIGAWKE, nie stan zywy.
        direct.Store.Save(state);
        viaClone.Store.Save(viaClone.Store.CloneState(CopyIntoFreshStore(viaClone.Store, state)));

        var reloadedDirect = new ConfigurationStore(direct.StatePath, direct.LibraryPath, direct.PodcastPath).LoadOrCreate();
        var reloadedClone = new ConfigurationStore(viaClone.StatePath, viaClone.LibraryPath, viaClone.PodcastPath).LoadOrCreate();

        Check(
            JsonSerializer.Serialize(reloadedClone, Json) == JsonSerializer.Serialize(reloadedDirect, Json),
            "zapis migawki i odczyt daja inne dane niz zapis stanu zywego");

        // Usuniecia i kolejnosc musza przetrwac droge przez migawke.
        var live = reloadedClone;
        live.Podcasts.Episodes.RemoveAt(5);
        live.LocalMedia.Items.Reverse();
        var expectedOrder = live.LocalMedia.Items.Select(i => i.Id).ToList();
        var expectedCount = live.Podcasts.Episodes.Count;

        viaClone.Store.Save(viaClone.Store.CloneState(live));
        var afterRemoval = new ConfigurationStore(viaClone.StatePath, viaClone.LibraryPath, viaClone.PodcastPath).LoadOrCreate();

        Check(afterRemoval.Podcasts.Episodes.Count == expectedCount, "usuniecie odcinka nie przetrwalo zapisu migawki");
        Check(
            afterRemoval.LocalMedia.Items.Select(i => i.Id).SequenceEqual(expectedOrder),
            "kolejnosc plikow lokalnych nie przetrwala zapisu migawki");
    }

    private static PersistedState CopyIntoFreshStore(ConfigurationStore store, PersistedState template)
    {
        // Wolanie potrzebne dla EFEKTU: zaklada pliki stanu i bazy w nowym
        // katalogu roboczym, zeby pozniejszy Save trafil w przygotowane
        // srodowisko. Zwrocony model jest celowo nieuzywany - testujemy kopie
        // TEGO SAMEGO szablonu w obu magazynach.
        store.LoadOrCreate();
        var json = JsonSerializer.Serialize(template, Json);
        return JsonSerializer.Deserialize<PersistedState>(json, Json)
            ?? throw new InvalidOperationException("nie udalo sie przygotowac modelu testowego");
    }

    // ------------------------------------------------------------- pomocnicze

    private static PersistedState BuildRichModel(PersistedState state)
    {
        for (var i = 0; i < Subscriptions; i++)
        {
            state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
            {
                Id = "sub-" + i,
                Title = "Kanal numer " + i,
                Author = "Autor " + i,
                Description = new string('k', 200),
                FeedUrl = "https://przyklad.test/kanal/" + i + ".xml",
                IsInLibrary = i % 2 == 0,
                IsFavorite = i % 3 == 0
            });
        }

        for (var i = 0; i < Episodes; i++)
        {
            state.Podcasts.Episodes.Add(new PodcastEpisodeSettings
            {
                Id = "ep-" + i,
                SubscriptionId = "sub-" + (i % Subscriptions),
                Title = "Odcinek numer " + i,
                Author = "Autor " + (i % 50),
                Description = new string('o', 300),
                MediaUrl = "https://przyklad.test/media/" + i + ".mp3",
                PublishedUtcTicks = DateTime.UtcNow.Ticks - i,
                FeedOrdinal = i,
                DurationTicks = TimeSpan.FromMinutes(30).Ticks,
                IsNew = i % 4 == 0,
                IsPlayed = i % 5 == 0
            });
        }

        for (var i = 0; i < LocalItems; i++)
        {
            state.LocalMedia.Items.Add(new LocalMediaItemSettings
            {
                Id = "local-" + i,
                Path = "C:/Muzyka/Album" + (i % 100) + "/Utwor" + i + ".mp3",
                Title = "Utwor lokalny " + i,
                DurationTicks = TimeSpan.FromMinutes(3).Ticks
            });
        }

        for (var i = 0; i < SpotifyItems; i++)
        {
            state.Spotify.CachedCollectionItems.Add(new TidalCachedCollectionItemSettings
            {
                Id = "spotify-" + i,
                Title = "Utwor Spotify " + i
            });
        }

        for (var i = 0; i < TidalItems; i++)
        {
            state.Tidal.CachedCollectionItems.Add(new TidalCachedCollectionItemSettings
            {
                Id = "tidal-" + i,
                Title = "Utwor TIDAL " + i
            });
        }

        for (var i = 0; i < Bookmarks; i++)
        {
            state.Bookmarks.Entries.Add(new BookmarkEntry
            {
                Id = "bookmark-" + i,
                ItemId = "local-" + (i % LocalItems),
                Name = "Zakladka " + i
            });
        }

        state.PlaybackHistory.ItemIdsBySession["lokalne"] = Enumerable.Range(0, 200).Select(i => "local-" + i).ToList();
        state.PlaybackHistory.ItemIdsBySession["podcasty"] = Enumerable.Range(0, 200).Select(i => "ep-" + i).ToList();
        state.CollectionOrders.FavoriteItemIdsBySession["lokalne"] = Enumerable.Range(0, 100).Select(i => "local-" + i).ToList();
        return state;
    }

    /// <summary>
    /// Rekurencyjnie wpisuje w model wartosci rozne od domyslnych, zeby zadna
    /// wlasciwosc nie przeszla testu tylko dlatego, ze i tu, i tam jest zero.
    ///
    /// ZAKRES JEST JAWNY, NIE DOMYSLNY. Pomocnik umie wypelnic dokladnie te
    /// ksztalty, ktore wystepuja w AKTUALNYM modelu: napisy, wartosci logiczne,
    /// wyliczenia, int/long/double (takze w wersji Nullable), listy tych typow i
    /// obiektow, slowniki o kluczu <c>string</c> albo <c>int</c> oraz zagniezdzone
    /// obiekty danych. KAZDY inny ksztalt i przekroczenie glebokosci ZGLASZA BLAD
    /// z pelna sciezka wlasciwosci - nie jest po cichu pomijany. Dzieki temu
    /// pierwsze pole nowego rodzaju (np. <c>DateTime</c>, <c>Guid</c>, tablica,
    /// slownik o kluczu obiektowym) wywali ten test z nazwy, zamiast wpisac
    /// falszywa zielen. To swiadomie NIE jest uniwersalny generator modelu.
    /// </summary>
    private const int MaxFillDepth = 8;

    private static void FillDistinctively(object target, Type type, string path, List<string> filled, int depth)
    {
        // Najglebsza sciezka aktualnego modelu ma 5 poziomow. Przekroczenie
        // limitu to sygnal, ze model sie poglebil i straznik przestal go
        // obchodzic - wtedy ma padac, a nie milczaco wracac.
        if (depth > MaxFillDepth)
        {
            throw new InvalidOperationException(
                $"straznik pol przekroczyl limit glebokosci {MaxFillDepth} na sciezce {path}. " +
                "Model sie poglebil: podnies limit swiadomie, zamiast pozwolic na cichy skip galezi.");
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            var propertyPath = path + "." + property.Name;

            // Wlasciwosci bez publicznego ustawiacza omija zarowno kopiowacz,
            // jak i ten straznik. Musza byc wymienione SWIADOMIE - inaczej
            // pierwsza nowa wlasciwosc { get; } albo { get; private set; } zniknelaby
            // z pola widzenia po obu stronach porownania JSON.
            if (property.GetMethod is null || property.SetMethod is null || !property.SetMethod.IsPublic)
            {
                if (IsAccountedReadOnly(property)) continue;
                throw new InvalidOperationException(
                    $"wlasciwosc {propertyPath} ({property.PropertyType.Name}) nie ma publicznego ustawiacza, " +
                    "wiec nie kopiuje jej StateSnapshotCopier ani nie sprawdza ten straznik. " +
                    "Dopisz ja do AccountedReadOnlyProperties, jesli jest wyliczana z innych pol, " +
                    "albo daj jej publiczny ustawiacz.");
            }

            var propertyType = property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlying == typeof(string))
            {
                property.SetValue(target, "wartosc-" + filled.Count);
                filled.Add(propertyPath);
            }
            else if (underlying == typeof(bool))
            {
                property.SetValue(target, !(bool)(property.GetValue(target) as bool? ?? false));
                filled.Add(propertyPath);
            }
            else if (underlying.IsEnum)
            {
                var values = Enum.GetValues(underlying);
                var current = property.GetValue(target);
                var changed = false;
                foreach (var value in values)
                {
                    if (current is not null && value.Equals(current)) continue;
                    property.SetValue(target, value);
                    filled.Add(propertyPath);
                    changed = true;
                    break;
                }
                if (!changed)
                {
                    throw new InvalidOperationException(
                        $"wlasciwosc {propertyPath} typu wyliczeniowego {underlying.Name} nie dostala wartosci " +
                        "innej od domyslnej (wyliczenie ma tylko jedna wartosc), wiec jej zgubienie przeszloby niezauwazone.");
                }
            }
            else if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(double))
            {
                var seed = 7 + filled.Count;
                // Kazda galez pudelkowana OSOBNO. Wspolny typ wyrazenia
                // warunkowego sprowadzilby int i long do double i refleksja
                // odrzucilaby wartosc przy ustawianiu wlasciwosci int.
                object value;
                if (underlying == typeof(int)) value = seed;
                else if (underlying == typeof(long)) value = (long)seed;
                else value = seed + 0.25d;
                property.SetValue(target, value);
                filled.Add(propertyPath);
            }
            else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var element = propertyType.GetGenericArguments()[0];
                var list = property.GetValue(target);
                if (list is null)
                {
                    list = Activator.CreateInstance(propertyType)!;
                    property.SetValue(target, list);
                }
                var add = propertyType.GetMethod("Add")!;
                var item = CreateElement(element, filled.Count, propertyPath + "[]");
                add.Invoke(list, new[] { item });
                filled.Add(propertyPath + "[]");
                RecurseInto(item, element, propertyPath + "[0]", filled, depth + 1);
            }
            else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = propertyType.GetGenericArguments();
                // Aktualny model ma klucze string oraz jeden slownik o kluczu int
                // (AppSettings.SessionSlots). Kazdy inny rodzaj klucza ma padac.
                object key = args[0] == typeof(string) ? "klucz-" + filled.Count
                    : args[0] == typeof(int) ? 900 + filled.Count
                    : throw new InvalidOperationException(
                        $"slownik {propertyPath} ma klucz {args[0].Name}, ktorego ten straznik nie umie wypelnic. " +
                        "Kopiowanie migawki wspiera wylacznie klucze niezmienne (string, int); dopisz obsluge " +
                        "swiadomie albo odrzuc taki klucz w modelu.");
                var map = property.GetValue(target);
                if (map is null)
                {
                    map = Activator.CreateInstance(propertyType)!;
                    property.SetValue(target, map);
                }
                var indexer = propertyType.GetProperty("Item")!;
                var value = CreateElement(args[1], filled.Count, propertyPath + "{}");
                indexer.SetValue(map, value, new[] { key });
                filled.Add(propertyPath + "{}");
                RecurseInto(value, args[1], propertyPath + "{0}", filled, depth + 1);
            }
            else if (!IsLeaf(underlying) && underlying.IsClass && underlying.GetConstructor(Type.EmptyTypes) is not null)
            {
                var child = property.GetValue(target);
                if (child is null)
                {
                    child = Activator.CreateInstance(underlying);
                    if (child is null)
                    {
                        throw new InvalidOperationException(
                            $"nie udalo sie utworzyc obiektu dla wlasciwosci {propertyPath} typu {underlying.FullName}.");
                    }
                    property.SetValue(target, child);
                }
                FillDistinctively(child, underlying, propertyPath, filled, depth + 1);
            }
            else
            {
                throw new InvalidOperationException(
                    $"straznik pol nie umie wypelnic wlasciwosci {propertyPath} typu {propertyType.FullName}. " +
                    "Cichy skip zamienilby ten test w falszywa zielen: dopisz obsluge tego ksztaltu " +
                    "razem z polem, ktore go wprowadza.");
            }
        }
    }

    /// <summary>
    /// Wchodzi w element listy albo wartosc slownika. Liscie zostaja bez zmian
    /// (maja juz wartosc z <see cref="CreateElement"/>), a KOLEKCJA w srodku
    /// kolekcji nie jest obchodzona jak obiekt danych - inaczej straznik
    /// probowalby wypelniac <c>List&lt;T&gt;.Count</c> i <c>Capacity</c>.
    /// </summary>
    private static void RecurseInto(object? value, Type type, string path, List<string> filled, int depth)
    {
        if (value is null || IsLeaf(type)) return;
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            // Zagniezdzona kolekcja: jej element powstal juz w CreateElement.
            if (definition == typeof(List<>) || definition == typeof(Dictionary<,>)) return;
        }
        FillDistinctively(value, type, path, filled, depth);
    }

    /// <summary>
    /// KONTROLOWANA lista wlasciwosci bez publicznego ustawiacza. Kazda pozycja
    /// jest wyliczana z innych pol i nie ma wlasnego stanu do przeniesienia,
    /// wiec ani kopiowacz, ani straznik nie musza jej odwiedzac. Lista jest
    /// zamknieta celowo: nowa wlasciwosc tylko do odczytu ma wywalic test,
    /// zamiast zniknac z obu stron porownania.
    /// </summary>
    private static readonly (string Type, string Property)[] AccountedReadOnlyProperties =
    [
        (nameof(SessionPlaybackAudioOverrides), nameof(SessionPlaybackAudioOverrides.IsEmpty))
    ];

    private static bool IsAccountedReadOnly(PropertyInfo property) =>
        AccountedReadOnlyProperties.Any(entry =>
            entry.Type == property.DeclaringType?.Name && entry.Property == property.Name);

    private static object? CreateElement(Type type, int seed, string path)
    {
        if (type == typeof(string)) return "element-" + seed;
        if (type == typeof(int)) return seed;
        if (type == typeof(long)) return (long)seed;
        if (type == typeof(double)) return seed + 0.5d;
        if (type == typeof(bool)) return true;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = Activator.CreateInstance(type)!;
            var element = type.GetGenericArguments()[0];
            type.GetMethod("Add")!.Invoke(list, new[] { CreateElement(element, seed + 1, path + "[]") });
            return list;
        }
        if (type.IsClass && type.GetConstructor(Type.EmptyTypes) is not null) return Activator.CreateInstance(type);
        throw new InvalidOperationException(
            $"straznik pol nie umie utworzyc elementu typu {type.FullName} dla {path}. " +
            "Dopisz obsluge razem z polem, ktore ten ksztalt wprowadza.");
    }

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan)
        || type == typeof(Guid) || type == typeof(Uri);

    private static string FirstDifference(string expected, string actual)
    {
        var limit = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < limit; i++)
        {
            if (expected[i] == actual[i]) continue;
            var from = Math.Max(0, i - 80);
            return $"pozycja {i}\n  oczekiwano: ...{expected[from..Math.Min(expected.Length, i + 80)]}...\n  otrzymano:  ...{actual[from..Math.Min(actual.Length, i + 80)]}...";
        }
        return $"rozna dlugosc: oczekiwano {expected.Length}, otrzymano {actual.Length}";
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "amc-clone-cost-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            StatePath = Path.Combine(Root, "state.json");
            LibraryPath = Path.Combine(Root, "library.db");
            PodcastPath = Path.Combine(Root, "podcasts.db");
            Store = new ConfigurationStore(StatePath, LibraryPath, PodcastPath);
        }

        public string Root { get; }
        public string StatePath { get; }
        public string LibraryPath { get; }
        public string PodcastPath { get; }
        public ConfigurationStore Store { get; }

        public void Dispose()
        {
            try { Directory.Delete(Root, true); } catch { /* sprzatanie nie moze psuc testu */ }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
