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
/// bajtow dla ustalonego modelu jest powtarzalna co do dziesiatych czesci
/// megabajta. Dlatego budzet jest wyrazony w bajtach na alokacjach watku.
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
        // (pelny obieg JSON) alokuje 13,4 MB, nowa 1,7 MB, za kazdym razem z
        // powtarzalnoscia do 0,1 MB. Budzet 6 MB lezy miedzy nimi z zapasem
        // ponad dwukrotnym w obie strony, wiec test nie jest ani kruchy, ani
        // pusty: stara implementacja przekracza go ponad dwukrotnie.
        const long Budget = 6L * 1024 * 1024;
        Check(
            median < Budget,
            $"CloneState alokuje {median / 1048576.0:F1} MB na modelu testowym, " +
            $"budzet to {Budget / 1048576.0:F1} MB. Pelny JSON w obie strony na watku UI " +
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
        Check(filled.Count > 150, $"straznik pol wypelnil tylko {filled.Count} wlasciwosci, model powinien miec ich znacznie wiecej");

        var expected = JsonSerializer.Serialize(state, Json);
        var actual = JsonSerializer.Serialize(store.CloneState(state), Json);
        if (expected != actual)
        {
            var at = FirstDifference(expected, actual);
            throw new InvalidOperationException(
                "klon zgubil lub zmienil wartosc wlasciwosci modelu. Pierwsza roznica: " + at);
        }
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
        var target = store.LoadOrCreate();
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
    /// </summary>
    private static void FillDistinctively(object target, Type type, string path, List<string> filled, int depth)
    {
        if (depth > 6) return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod is null || property.SetMethod is null || !property.SetMethod.IsPublic) continue;
            if (property.GetIndexParameters().Length > 0) continue;
            var propertyPath = path + "." + property.Name;
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
                foreach (var value in values)
                {
                    if (current is not null && value.Equals(current)) continue;
                    property.SetValue(target, value);
                    filled.Add(propertyPath);
                    break;
                }
            }
            else if (underlying == typeof(int) || underlying == typeof(long)
                || underlying == typeof(double) || underlying == typeof(decimal))
            {
                var seed = 7 + filled.Count;
                object value = underlying == typeof(int) ? seed
                    : underlying == typeof(long) ? (long)seed
                    : underlying == typeof(double) ? seed + 0.25d
                    : (decimal)seed;
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
                var item = CreateElement(element, filled.Count);
                add.Invoke(list, new[] { item });
                filled.Add(propertyPath + "[]");
                if (item is not null && !IsLeaf(element)) FillDistinctively(item, element, propertyPath + "[0]", filled, depth + 1);
            }
            else if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = propertyType.GetGenericArguments();
                if (args[0] != typeof(string)) continue;
                var map = property.GetValue(target);
                if (map is null)
                {
                    map = Activator.CreateInstance(propertyType)!;
                    property.SetValue(target, map);
                }
                var indexer = propertyType.GetProperty("Item")!;
                var value = CreateElement(args[1], filled.Count);
                indexer.SetValue(map, value, new object[] { "klucz-" + filled.Count });
                filled.Add(propertyPath + "{}");
                if (value is not null && !IsLeaf(args[1])) FillDistinctively(value, args[1], propertyPath + "{0}", filled, depth + 1);
            }
            else if (!IsLeaf(underlying) && underlying.IsClass)
            {
                var child = property.GetValue(target);
                if (child is null)
                {
                    child = Activator.CreateInstance(underlying);
                    if (child is null) continue;
                    property.SetValue(target, child);
                }
                FillDistinctively(child, underlying, propertyPath, filled, depth + 1);
            }
        }
    }

    private static object? CreateElement(Type type, int seed)
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
            type.GetMethod("Add")!.Invoke(list, new[] { CreateElement(element, seed + 1) });
            return list;
        }
        return Activator.CreateInstance(type);
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
