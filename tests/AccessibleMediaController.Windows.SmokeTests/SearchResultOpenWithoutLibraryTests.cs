using System.Reflection;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Otwieranie wyniku wyszukiwania BEZ Biblioteki na RZECZYWISTYM oknie glownym.
/// Michal ustalil 20.09.2026: Enter otwiera i gra, zapis zostaje pod jawnym
/// Ctrl+Shift+L. Poprzednie podejscie mierzylo same helpery bool, wiec dwie
/// rzeczy przeszly przez sito:
///
/// L1 - produkcyjny refresh (F5 / Ctrl+F5 / timer) wolal
/// PodcastLibraryUpdater.Apply BEZ addToLibrary, czyli z domyslnym true, i tym
/// samym wpisywal do Biblioteki kanal otwarty wczesniej bez zapisu.
///
/// L2 - wszystkie publiczne materialy YouTube dzielily JEDNA kolekcje
/// internet-media:public. Gdy raz znalazla sie w Bibliotece, kazdy nastepny
/// podglad ladowal w kolekcji zapisanej.
///
/// Dlatego mierzymy prawdziwe wejscia: AddPublicInternetMedia, prawdziwe
/// polecenie Ctrl+Shift+L przez ExecuteCommand oraz produkcyjna sciezke
/// RefreshPodcastSubscriptionsAsync ze stubem HTTP (bez sieci).
/// </summary>
internal static class SearchResultOpenWithoutLibraryTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        PodglądYouTubeNieWchodziDoZapisanejKolekcji();
        JawneDodanieAwansujeBezZmianyIdentyfikatora();
        ZdjęcieCzłonkostwaNieUsuwaMateriału();
        OdświeżenieNieZapisujeCzłonkostwa();
        PełnyObiegZapisOdczyt();
        Console.WriteLine("OK: otwieranie wyniku bez Biblioteki (podgląd, awans, odświeżenie, obieg zapisu)");
    }

    /// <summary>
    /// GLOBALNE OFF: podglad YouTube nie moze wejsc do kolekcji zapisanej, nawet
    /// gdy ta juz jest w Bibliotece. Wczesniej wspolna kolekcja przenosila
    /// czlonkostwo na kazdy kolejny podglad.
    /// </summary>
    private static void PodglądYouTubeNieWchodziDoZapisanejKolekcji()
    {
        WOknie(
            state =>
            {
                Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary);
                // Kolekcja zapisana JUZ istnieje, JEST w Bibliotece i ma w sobie
                // material - dokladnie stan, w ktorym stara wspolna kolekcja
                // psula kazdy nastepny podglad.
                state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
                {
                    Id = PublicInternetMediaCollections.SavedId,
                    Title = PublicInternetMediaCollections.SavedTitle,
                    FeedUrl = "https://amc.invalid/public-internet-media",
                    SourceKind = PodcastSourceKind.PublicInternetMedia,
                    IsInLibrary = true
                });
                state.Podcasts.Episodes.Add(new PodcastEpisodeSettings
                {
                    Id = "internet-media:wczesniej-zapisany",
                    SubscriptionId = PublicInternetMediaCollections.SavedId,
                    Title = "Wcześniej zapisany materiał",
                    MediaUrl = "https://www.youtube.com/watch?v=ZAPISANY",
                    PageUrl = "https://www.youtube.com/watch?v=ZAPISANY",
                    MediaType = "video/youtube"
                });
            },
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var item = DodajPodgląd(window, "https://www.youtube.com/watch?v=PODGLAD1", "Podgląd 1");
                var episode = Odcinek(state, item.Id);
                Sprawdź(
                    PublicInternetMediaCollections.IsPreview(episode.SubscriptionId),
                    "Podgląd YouTube trafia do kolekcji podglądów, nie do zapisanej "
                    + $"(kolekcja: {episode.SubscriptionId}).");
                var preview = Subskrypcja(state, PublicInternetMediaCollections.PreviewId);
                Sprawdź(
                    !preview.IsInLibrary,
                    "Kolekcja podglądów nie może mieć członkostwa w Bibliotece.");
                Sprawdź(
                    Subskrypcja(state, PublicInternetMediaCollections.SavedId).IsInLibrary,
                    "Podgląd zdjął członkostwo wcześniej zapisanej kolekcji.");
                Sprawdź(
                    !state.Podcasts.Episodes.Any(candidate =>
                        PublicInternetMediaCollections.IsSaved(candidate.SubscriptionId)
                        && string.Equals(candidate.Id, item.Id, StringComparison.Ordinal)),
                    "Podgląd YouTube pojawił się w zapisanej kolekcji przy globalnym OFF.");
                Sprawdź(
                    PozycjeSesji(window).Any(row =>
                        string.Equals(row.Id, item.Id, StringComparison.Ordinal)),
                    "Podgląd nie jest w żywej sesji, więc nie ma czym zagrać ani gdzie wrócić fokusem.");
            });
    }

    /// <summary>
    /// Jawne Ctrl+Shift+L na podgladzie AWANSUJE material do kolekcji zapisanej.
    /// Identyfikator odcinka MUSI zostac - na nim wisi odtwarzanie, historia,
    /// kolejka i zakladki.
    /// </summary>
    private static void JawneDodanieAwansujeBezZmianyIdentyfikatora()
    {
        WOknie(
            state => Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary),
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var item = DodajPodgląd(window, "https://www.youtube.com/watch?v=AWANS1", "Awans 1");
                var idPrzedAwansem = item.Id;
                // Stan odtwarzania ustawiamy na ZYWEJ pozycji sesji - to ta
                // sama droga, ktora ma uzytkownik, i CapturePodcastState wlasnie
                // z niej przepisuje stan trwaly.
                item.IsInQueue = true;
                var odcinek = Odcinek(state, idPrzedAwansem);
                Sprawdź(
                    PublicInternetMediaCollections.IsPreview(odcinek.SubscriptionId),
                    "Materiał przed awansem nie jest podglądem.");

                Polecenie(window, item, AccessibleMediaController.Core.Commands.CommandIds.ToggleLibrary);

                var poAwansie = Odcinek(state, idPrzedAwansem);
                Sprawdź(
                    PublicInternetMediaCollections.IsSaved(poAwansie.SubscriptionId),
                    "Jawne Ctrl+Shift+L nie awansowało materiału do zapisanej kolekcji "
                    + $"(kolekcja: {poAwansie.SubscriptionId}).");
                Sprawdź(
                    string.Equals(poAwansie.Id, idPrzedAwansem, StringComparison.Ordinal),
                    "Awans zmienił identyfikator odcinka, więc odtwarzanie i zakładki się rozjechały.");
                Sprawdź(
                    poAwansie.IsInQueue,
                    "Awans zgubił kolejkę, więc odtwarzanie i powrót się rozjechały.");
                Sprawdź(
                    Subskrypcja(state, PublicInternetMediaCollections.SavedId).IsInLibrary,
                    "Zapisana kolekcja nie weszła do Biblioteki po jawnym dodaniu.");
                Sprawdź(
                    !state.Podcasts.Episodes.Any(candidate =>
                        PublicInternetMediaCollections.IsPreview(candidate.SubscriptionId)),
                    "Po awansie materiał został też w podglądach, czyli jest podwójnie.");
            });
    }

    /// <summary>
    /// Ctrl+Shift+L na JUZ zapisanym materiale zdejmuje czlonkostwo, ale NIE
    /// wyrzuca pozycji - wraca do podgladow razem ze stanem odtwarzania.
    /// L5 z review: usuniecie REKORDU to inna operacja niz zdjecie czlonkostwa.
    /// </summary>
    private static void ZdjęcieCzłonkostwaNieUsuwaMateriału()
    {
        WOknie(
            state => Podstawa(state, SearchResultEnterBehavior.AddToLibrary),
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var item = DodajPodgląd(window, "https://www.youtube.com/watch?v=ZDJECIE1", "Zdjęcie 1");
                var id = item.Id;
                Sprawdź(
                    PublicInternetMediaCollections.IsSaved(Odcinek(state, id).SubscriptionId),
                    "Przy globalnym ON materiał od razu należy do zapisanej kolekcji.");

                Polecenie(window, item, AccessibleMediaController.Core.Commands.CommandIds.ToggleLibrary);

                var odcinek = state.Podcasts.Episodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, id, StringComparison.Ordinal));
                Sprawdź(
                    odcinek is not null,
                    "Zdjęcie członkostwa USUNĘŁO rekord materiału - to inna operacja niż usuwanie.");
                Sprawdź(
                    PublicInternetMediaCollections.IsPreview(odcinek!.SubscriptionId),
                    "Po zdjęciu członkostwa materiał nie wrócił do podglądów "
                    + $"(kolekcja: {odcinek.SubscriptionId}).");
            });
    }

    /// <summary>
    /// L1 z review: PRODUKCYJNE odswiezanie (F5, Ctrl+F5 i timer chodza tym samym
    /// RefreshPodcastSubscriptionsAsync) nie moze zmieniac czlonkostwa - ani przy
    /// globalnym OFF, ani ON. Kanal HTTP podajemy stubem, bez sieci.
    /// </summary>
    private static void OdświeżenieNieZapisujeCzłonkostwa()
    {
        foreach (var zachowanie in new[]
                 {
                     SearchResultEnterBehavior.OpenWithoutLibrary,
                     SearchResultEnterBehavior.AddToLibrary
                 })
        {
            var tryb = zachowanie;
            WOknie(
                state =>
                {
                    Podstawa(state, tryb);
                    // Kanal otwarty wczesniej z wyszukiwania BEZ zapisu.
                    state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
                    {
                        Id = "kanal-bez-biblioteki",
                        Title = "Kanał otwarty bez zapisu",
                        FeedUrl = StubFeedAddress,
                        SourceKind = PodcastSourceKind.Rss,
                        IsInLibrary = false
                    });
                    // Kanal jawnie zapisany - jego czlonkostwo tez ma przezyc.
                    state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
                    {
                        Id = "kanal-zapisany",
                        Title = "Kanał zapisany",
                        FeedUrl = SavedStubFeedAddress,
                        SourceKind = PodcastSourceKind.Rss,
                        IsInLibrary = true
                    });
                },
                (window, state, przejscie) =>
                {
                    if (przejscie != 0) return;
                    PodstawStubKanału(window);
                    var subskrypcje = state.Podcasts.Subscriptions
                        .Where(subscription => subscription.SourceKind == PodcastSourceKind.Rss)
                        .ToArray();
                    var zadanie = (Task)typeof(MainWindow)
                        .GetMethod("RefreshPodcastSubscriptionsAsync", Flags)!
                        .Invoke(window, [subskrypcje, false, false, true])!;
                    PoczekajNaDispatcherze(window, zadanie);

                    Sprawdź(
                        !Subskrypcja(state, "kanal-bez-biblioteki").IsInLibrary,
                        $"Odświeżenie (tryb {tryb}) wpisało do Biblioteki kanał otwarty bez zapisu.");
                    Sprawdź(
                        Subskrypcja(state, "kanal-zapisany").IsInLibrary,
                        $"Odświeżenie (tryb {tryb}) zdjęło członkostwo kanału zapisanego.");
                    Sprawdź(
                        state.Podcasts.Episodes.Any(episode => string.Equals(
                            episode.SubscriptionId,
                            "kanal-bez-biblioteki",
                            StringComparison.Ordinal)),
                        "Odświeżenie nie sprowadziło odcinków kanału bez Biblioteki, "
                        + "więc nie ma czego odtworzyć.");
                });
        }
    }

    /// <summary>
    /// Pelny obieg: podglad i material zapisany przezywaja Save, Load i restart
    /// okna w swoich kolekcjach. Zaden nie zmienia strony przy ponownym odczycie.
    /// </summary>
    private static void PełnyObiegZapisOdczyt()
    {
        string? idPodglądu = null;
        string? idZapisanego = null;
        WOknie(
            state => Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary),
            (window, state, przejscie) =>
            {
                if (przejscie == 0)
                {
                    var podgląd = DodajPodgląd(window, "https://www.youtube.com/watch?v=OBIEG1", "Obieg podglądu");
                    idPodglądu = podgląd.Id;
                    var zapisany = DodajPodgląd(window, "https://www.youtube.com/watch?v=OBIEG2", "Obieg zapisany");
                    idZapisanego = zapisany.Id;
                    Polecenie(window, zapisany, AccessibleMediaController.Core.Commands.CommandIds.ToggleLibrary);
                    Sprawdź(
                        PublicInternetMediaCollections.IsSaved(Odcinek(state, idZapisanego!).SubscriptionId),
                        "Jawne dodanie przed zapisem nie awansowało materiału.");
                    return;
                }

                var poRestarciePodgląd = Odcinek(state, idPodglądu!);
                var poRestartieZapisany = Odcinek(state, idZapisanego!);
                Sprawdź(
                    PublicInternetMediaCollections.IsPreview(poRestarciePodgląd.SubscriptionId),
                    "Po zapisie i restarcie podgląd zmienił kolekcję na "
                    + poRestarciePodgląd.SubscriptionId + ".");
                Sprawdź(
                    PublicInternetMediaCollections.IsSaved(poRestartieZapisany.SubscriptionId),
                    "Po zapisie i restarcie zapisany materiał wypadł z zapisanej kolekcji.");
                Sprawdź(
                    !Subskrypcja(state, PublicInternetMediaCollections.PreviewId).IsInLibrary,
                    "Po restarcie kolekcja podglądów ma członkostwo w Bibliotece.");
                Sprawdź(
                    Subskrypcja(state, PublicInternetMediaCollections.SavedId).IsInLibrary,
                    "Po restarcie zapisana kolekcja wypadła z Biblioteki.");
                Sprawdź(
                    state.Podcasts.Subscriptions.Any(subscription =>
                        PublicInternetMediaCollections.IsPreview(subscription.Id)),
                    "Po restarcie zniknęła kolekcja podglądów, więc nie ma jak wrócić do materiału.");
            });
    }

    private const string StubFeedAddress = "https://example.test/stub-kanal.xml";
    private const string SavedStubFeedAddress = "https://example.test/stub-kanal-zapisany.xml";

    /// <summary>
    /// Prawdziwe wejscie AddPublicInternetMedia - ta sama metoda, ktora wola
    /// Enter na wyniku wyszukiwania YouTube.
    /// </summary>
    private static MediaItem DodajPodgląd(MainWindow window, string pageUrl, string title)
    {
        var media = new ResolvedYouTubeAudioSource(
            pageUrl,
            string.Empty,
            title,
            "Kanał testowy",
            TimeSpan.FromMinutes(4),
            false,
            false,
            null,
            null);
        var addToLibrary = SearchResultEnterPolicy.ShouldAddToLibrary(
            StanOkna(window).Settings.SearchResultEnterBehavior,
            explicitLibraryRequest: false);
        return (MediaItem)typeof(MainWindow)
            .GetMethod("AddPublicInternetMedia", Flags)!
            .Invoke(window, [media, null, false, addToLibrary])!;
    }

    /// <summary>
    /// Prawdziwe polecenie aplikacji - ta sama sciezka co skrot klawiszowy.
    /// </summary>
    private static void Polecenie(MainWindow window, MediaItem item, string commandId)
    {
        var sesje = (SessionManager)typeof(MainWindow).GetField("_sessions", Flags)!.GetValue(window)!;
        sesje.SelectSession("podcasts");
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

    /// <summary>
    /// Stub HTTP zamiast sieci: PodcastFeedClient dostaje handler zwracajacy
    /// staly kanal RSS, wiec produkcyjna sciezka odswiezania dziala offline.
    /// </summary>
    private static void PodstawStubKanału(MainWindow window)
    {
        var clientType = typeof(MainWindow).Assembly.GetType(
            "AccessibleMediaController.Windows.Services.PodcastFeedClient",
            throwOnError: true)!;
        var client = Activator.CreateInstance(
            clientType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [new StubHandler(), (TimeSpan?)TimeSpan.FromSeconds(5)],
            null)!;
        typeof(MainWindow).GetField("_podcastFeedClient", Flags)!.SetValue(window, client);
    }

    private sealed class StubHandler : System.Net.Http.HttpMessageHandler
    {
        // Kanal zalezy od ADRESU, inaczej oba kanaly mialyby ten sam
        // identyfikator kanalu i aktualizator zlalby je w jeden.
        private static string Feed(string address) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <rss version="2.0"><channel>
              <title>Kanał stub {address}</title>
              <link>{address}</link>
              <description>Kanał bez sieci</description>
              <item>
                <guid>{address}#odcinek-1</guid>
                <title>Odcinek stub 1</title>
                <pubDate>Sun, 20 Sep 2026 12:00:00 +0000</pubDate>
                <enclosure url="{address}/stub-1.mp3" type="audio/mpeg" length="1000"/>
              </item>
            </channel></rss>
            """;

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(
                    Feed(request.RequestUri!.AbsoluteUri),
                    System.Text.Encoding.UTF8,
                    "application/rss+xml")
            });
    }

    /// <summary>
    /// Czeka na zadanie odswiezania, pompujac kolejke dispatchera - inaczej
    /// kontynuacje na watku UI nigdy by sie nie wykonaly.
    /// </summary>
    private static void PoczekajNaDispatcherze(MainWindow window, Task task)
    {
        var termin = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > termin)
                throw new Exception("Odświeżanie nie zakończyło się w 30 s.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            window.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => frame.Continue = false);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }

    private static PersistedState StanOkna(MainWindow window) =>
        (PersistedState)typeof(MainWindow).GetField("_state", Flags)!.GetValue(window)!;

    private static IReadOnlyList<MediaItem> PozycjeSesji(MainWindow window) =>
        (List<MediaItem>)typeof(MainWindow).GetField("_podcastItems", Flags)!.GetValue(window)!;

    private static PodcastEpisodeSettings Odcinek(PersistedState state, string id) =>
        state.Podcasts.Episodes.FirstOrDefault(episode =>
            string.Equals(episode.Id, id, StringComparison.Ordinal))
        ?? throw new Exception($"Brak odcinka {id} w stanie trwałym.");

    private static PodcastSubscriptionSettings Subskrypcja(PersistedState state, string id) =>
        state.Podcasts.Subscriptions.FirstOrDefault(subscription =>
            string.Equals(subscription.Id, id, StringComparison.Ordinal))
        ?? throw new Exception($"Brak kolekcji {id} w stanie trwałym.");

    private static void Podstawa(PersistedState state, SearchResultEnterBehavior behavior)
    {
        state.Settings.Updates.CheckAutomatically = false;
        state.Settings.SearchResultEnterBehavior = behavior;
        state.Settings.LastSessionId = "podcasts";
    }

    private static void Sprawdź(bool warunek, string opis)
    {
        if (!warunek) throw new Exception("NIE PRZESZŁO: " + opis);
    }

    /// <summary>
    /// Rzeczywiste okno glowne w watku STA, dwa uruchomienia przez Save/Load
    /// prawdziwego ConfigurationStore. Bez ShowDialog, bez audio, bez sieci.
    /// </summary>
    private static void WOknie(
        Action<PersistedState> przygotuj,
        Action<MainWindow, PersistedState, int> sprawdz)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-search-open-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                przygotuj(state);
                var store = new ConfigurationStore(
                    Path.Combine(root, "state.json"),
                    Path.Combine(root, "library.db"),
                    Path.Combine(root, "podcasts.db"));
                var options = (System.Text.Json.JsonSerializerOptions)typeof(ConfigurationStore)
                    .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)!
                    .GetValue(null)!;
                File.WriteAllText(
                    Path.Combine(root, "state.json"),
                    System.Text.Json.JsonSerializer.Serialize(state, options));
                state = store.LoadOrCreate();
                for (var restart = 0; restart < 2; restart++)
                {
                    window = new MainWindow(state, store);
                    sprawdz(window, state, restart);
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
        if (!thread.Join(TimeSpan.FromSeconds(120)))
            throw new Exception("Test okna nie zakończył się w 120 s.");
        if (failure is not null)
            throw new Exception("Otwieranie wyniku bez Biblioteki.", failure);
    }
}
