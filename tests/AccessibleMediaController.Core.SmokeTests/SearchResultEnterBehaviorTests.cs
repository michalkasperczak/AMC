using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Zachowanie klawisza Enter na wyniku wyszukiwania. Michal ustalil (20.09.2026):
/// domyslnie wynik ma sie OTWORZYC i zagrac, bez dopisywania do Biblioteki.
/// Drugi wybor przywraca dawne automatyczne dodawanie. Jedno ustawienie globalne
/// dla wszystkich serwisow, nie osobne per serwis.
///
/// Mierzymy TRZY rzeczy osobno, bo kazda mogla zawiesc niezaleznie: domyslna
/// wartosc i jej trwalosc przez rzeczywisty ConfigurationStore, decyzje polityki
/// (w tym swiadome Ctrl+Shift+L) oraz rzeczywisty zapis Biblioteki podcastow.
/// </summary>
internal static class SearchResultEnterBehaviorTests
{
    internal static void Run()
    {
        DefaultOpensWithoutLibrary();
        ChoiceSurvivesSaveAndLoad();
        PolicyDecidesLibraryWrite();
        OpeningFromSearchDoesNotWriteLibrary();
    }

    /// <summary>
    /// Rzeczywista produkcyjna sciezka importu kanalu z wyszukiwania:
    /// <see cref="PodcastLibraryUpdater.Apply"/>. Domyslne otwarcie ma
    /// sprowadzic kanal i jego odcinki (zeby bylo co odtworzyc i gdzie wrocic
    /// fokusem), ale NIE wpisac go do Biblioteki. Widok „Biblioteka” filtruje po
    /// tej samej fladze, wiec sprawdzamy ja wprost.
    /// </summary>
    private static void OpeningFromSearchDoesNotWriteLibrary()
    {
        var feed = SampleFeed();

        var opened = new PodcastSettings();
        var openedResult = PodcastLibraryUpdater.Apply(
            opened,
            feed,
            null,
            DateTime.UtcNow,
            addToLibrary: false);
        Check(!openedResult.Subscription.IsInLibrary,
            "Otwarcie wyniku z wyszukiwania nie wpisuje kanału do Biblioteki");
        Check(opened.Episodes.Count == 1,
            "Otwarty kanał ma odcinek do odtworzenia mimo braku w Bibliotece");
        Check(opened.Subscriptions.Count == 1,
            "Otwarty kanał jest w pamięci sesji, więc fokus ma dokąd wrócić");

        var added = new PodcastSettings();
        var addedResult = PodcastLibraryUpdater.Apply(
            added,
            feed,
            null,
            DateTime.UtcNow,
            addToLibrary: true);
        Check(addedResult.Subscription.IsInLibrary,
            "Świadome dodanie wpisuje kanał do Biblioteki");

        // Zastane wywolania bez nowego parametru (odswiezanie, OPML, reczne
        // dodanie) musza dalej zapisywac Biblioteke.
        var legacy = new PodcastSettings();
        Check(PodcastLibraryUpdater.Apply(legacy, feed, null, DateTime.UtcNow)
                .Subscription.IsInLibrary,
            "Domyślne wywołanie aktualizatora nadal zapisuje Bibliotekę");

        // Czlonkostwa NIE zdejmujemy: kanal juz zapisany zostaje w Bibliotece,
        // nawet gdy uzytkownik otworzy go ponownie z wyszukiwania.
        var kept = PodcastLibraryUpdater.Apply(
            added,
            feed,
            null,
            DateTime.UtcNow,
            addToLibrary: false);
        Check(kept.Subscription.IsInLibrary,
            "Ponowne otwarcie z wyszukiwania nie usuwa kanału z Biblioteki");
    }

    private static PodcastFeedDocument SampleFeed() => new(
        "podcast-wyszukany",
        "Podcast z wyszukiwania",
        "Autor",
        "Opis",
        new Uri("https://example.test/szukany.xml"),
        new Uri("https://example.test/szukany"),
        [new PodcastFeedEpisode(
            "odcinek-1",
            "guid-1",
            "Odcinek z wyszukiwania",
            "Autor",
            "Opis odcinka",
            new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(20),
            new Uri("https://cdn.example.test/odcinek-1.mp3"),
            null,
            "audio/mpeg",
            100)]);

    /// <summary>
    /// Sama decyzja polityki: kiedy otwarcie wyniku ma dopisac go do Biblioteki.
    /// Ctrl+Shift+L (jawne polecenie Biblioteka) MUSI dopisywac niezaleznie od
    /// ustawienia, bo to swiadomy wybor uzytkownika.
    /// </summary>
    private static void PolicyDecidesLibraryWrite()
    {
        Check(!SearchResultEnterPolicy.ShouldAddToLibrary(
                SearchResultEnterBehavior.OpenWithoutLibrary,
                explicitLibraryRequest: false),
            "Domyślne otwarcie wyniku nie dopisuje go do Biblioteki");
        Check(SearchResultEnterPolicy.ShouldAddToLibrary(
                SearchResultEnterBehavior.AddToLibrary,
                explicitLibraryRequest: false),
            "Drugi wybór przywraca automatyczne dodawanie przy otwarciu");
        Check(SearchResultEnterPolicy.ShouldAddToLibrary(
                SearchResultEnterBehavior.OpenWithoutLibrary,
                explicitLibraryRequest: true),
            "Ctrl+Shift+L dodaje do Biblioteki także przy domyślnym otwieraniu");
        Check(SearchResultEnterPolicy.ShouldAddToLibrary(
                SearchResultEnterBehavior.AddToLibrary,
                explicitLibraryRequest: true),
            "Ctrl+Shift+L dodaje do Biblioteki również przy automatycznym dodawaniu");

        // Istniejacego czlonkostwa NIE zdejmujemy. Kanal juz w Bibliotece
        // zostaje w niej takze wtedy, gdy Enter otwiera bez dodawania.
        var existing = new PodcastSubscriptionSettings { Id = "kanal", IsInLibrary = true };
        SearchResultEnterPolicy.ApplyOpenedResultMembership(
            existing,
            SearchResultEnterBehavior.OpenWithoutLibrary,
            explicitLibraryRequest: false);
        Check(existing.IsInLibrary,
            "Wcześniej zapisany wynik zostaje w Bibliotece po zwykłym otwarciu");

        var fresh = new PodcastSubscriptionSettings { Id = "nowy", IsInLibrary = false };
        SearchResultEnterPolicy.ApplyOpenedResultMembership(
            fresh,
            SearchResultEnterBehavior.OpenWithoutLibrary,
            explicitLibraryRequest: false);
        Check(!fresh.IsInLibrary,
            "Nowy wynik otwarty domyślnie nie wchodzi do Biblioteki");

        SearchResultEnterPolicy.ApplyOpenedResultMembership(
            fresh,
            SearchResultEnterBehavior.OpenWithoutLibrary,
            explicitLibraryRequest: true);
        Check(fresh.IsInLibrary,
            "Świadome Ctrl+Shift+L wpisuje nowy wynik do Biblioteki");
    }

    private static void DefaultOpensWithoutLibrary()
    {
        Check(new AppSettings().SearchResultEnterBehavior == SearchResultEnterBehavior.OpenWithoutLibrary,
            "Domyślnie Enter na wyniku otwiera bez dodawania do Biblioteki");
        Check(ConfigurationStore.CreateDefaultState().Settings.SearchResultEnterBehavior
                == SearchResultEnterBehavior.OpenWithoutLibrary,
            "Nowy stan konfiguracji ma domyślne otwieranie bez dodawania");
    }

    private static void ChoiceSurvivesSaveAndLoad()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"amc-search-enter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var store = new ConfigurationStore(
                Path.Combine(directory, "state.json"),
                Path.Combine(directory, "library.db"),
                Path.Combine(directory, "podcasts.db"));
            var state = store.LoadOrCreate();
            Check(state.Settings.SearchResultEnterBehavior == SearchResultEnterBehavior.OpenWithoutLibrary,
                "Pierwsze uruchomienie czyta otwieranie bez dodawania");

            state.Settings.SearchResultEnterBehavior = SearchResultEnterBehavior.AddToLibrary;
            store.Save(state);

            var reopened = new ConfigurationStore(
                Path.Combine(directory, "state.json"),
                Path.Combine(directory, "library.db"),
                Path.Combine(directory, "podcasts.db"));
            Check(reopened.LoadOrCreate().Settings.SearchResultEnterBehavior
                    == SearchResultEnterBehavior.AddToLibrary,
                "Wybór automatycznego dodawania przetrwał zapis i ponowny odczyt");
        }
        finally
        {
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception($"NIE PRZESZŁO: {description}");
        Console.WriteLine($"OK: {description}");
    }
}
