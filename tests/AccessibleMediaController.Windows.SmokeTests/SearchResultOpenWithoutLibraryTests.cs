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
        // Cztery poprawki z przegladu YouTube mierzymy RAZEM, zeby jedna wpadka
        // nie zaslaniala pozostalych trzech - inaczej kazdy przebieg pokazuje
        // tylko pierwszy blad i reszta wychodzi dopiero po kolejnej poprawce.
        WszystkieNaraz(
            ("brak cichej democji", PonowneOtwarcieZapisanegoNieZdejmujeCzłonkostwa),
            ("jawne dodanie z wyszukiwania", BibliotekaZWyszukiwaniaDodajeNowyIPodgląd),
            ("metadane kolekcji", MetadaneKolekcjiPrzeżywajązmianęOstatniegoOdcinka),
            ("mieszane zaznaczenie", MieszaneZaznaczenieNiczegoNieZmienia),
            ("kolejka nie jest Enterem", KolejkaNieDodajePrzyWłączonymEnter),
            ("jawna intencja dodania", DodanieNieMaDomyślnejIntencji));
        Console.WriteLine("OK: otwieranie wyniku bez Biblioteki (podgląd, awans, odświeżenie, obieg zapisu, brak democji, jawne Library, metadane kolekcji, mieszane zaznaczenie)");
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

    /// <summary>
    /// L1 z review YouTube: ponowne otwarcie materialu JUZ zapisanego w
    /// Bibliotece przy globalnym OFF nie moze go cicho zdemotowac do podgladow.
    /// Mierzymy prawdziwe AddPublicInternetMedia, nie helper bool.
    /// </summary>
    private static void PonowneOtwarcieZapisanegoNieZdejmujeCzłonkostwa()
    {
        const string adres = "https://www.youtube.com/watch?v=ZAPISANY2";
        WOknie(
            state =>
            {
                Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary);
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
                    Id = "internet-media:zapisany-wcesniej-2",
                    SubscriptionId = PublicInternetMediaCollections.SavedId,
                    Title = "Zapisany materiał",
                    MediaUrl = adres,
                    PageUrl = adres,
                    MediaType = "video/youtube"
                });
            },
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var item = DodajPodgląd(window, adres, "Zapisany materiał ponownie");
                var odcinek = Odcinek(state, item.Id);
                Sprawdź(
                    PublicInternetMediaCollections.IsSaved(odcinek.SubscriptionId),
                    "Otwarcie zapisanego materiału bez Biblioteki ZDEMOTOWAŁO go do podglądów "
                    + $"(kolekcja: {odcinek.SubscriptionId}).");
            });
    }

    /// <summary>
    /// L2 z review: dzialanie Biblioteka z okna wyszukiwania musi JAWNIE dodac
    /// material do kolekcji zapisanej - i nowy wynik YouTube, i material juz
    /// otwarty jako podglad. Mierzymy prawdziwe ExecuteSearchResultAction.
    /// </summary>
    private static void BibliotekaZWyszukiwaniaDodajeNowyIPodgląd()
    {
        WOknie(
            state => Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary),
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var podgląd = DodajPodgląd(window, "https://www.youtube.com/watch?v=LIB_PODGLAD", "Podgląd do dodania");
                var nowy = WynikWyszukiwaniaYouTube("LIB_NOWY", "Nowy wynik");

                var komunikat = DziałanieWyniku(
                    window,
                    [nowy, podgląd],
                    SearchResultAction.Library);

                Sprawdź(
                    PublicInternetMediaCollections.IsSaved(Odcinek(state, podgląd.Id).SubscriptionId),
                    "Biblioteka nie dodała materiału otwartego wcześniej jako podgląd "
                    + $"(kolekcja: {Odcinek(state, podgląd.Id).SubscriptionId}).");
                var dodany = state.Podcasts.Episodes.FirstOrDefault(episode =>
                    string.Equals(episode.MediaUrl, AdresYouTube("LIB_NOWY"), StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception("Biblioteka nie utworzyła w ogóle nowego materiału.");
                Sprawdź(
                    PublicInternetMediaCollections.IsSaved(dodany.SubscriptionId),
                    "Biblioteka dodała NOWY wynik YouTube do podglądów, nie do kolekcji zapisanej "
                    + $"(kolekcja: {dodany.SubscriptionId}).");
                Sprawdź(
                    Subskrypcja(state, PublicInternetMediaCollections.SavedId).IsInLibrary,
                    "Kolekcja zapisana nie ma członkostwa w Bibliotece po jawnym dodaniu.");
                Sprawdź(
                    komunikat is not null && komunikat.Contains("Dodano", StringComparison.Ordinal),
                    $"Jawne dodanie nie zameldowało dodania (komunikat: {komunikat ?? "brak"}).");
            });
    }

    /// <summary>
    /// L3 z review: zmiana czlonkostwa OSTATNIEGO materialu w kolekcji nie moze
    /// kasowac kolekcji razem z jej metadanymi (wlasna nazwa, ulubiona).
    /// </summary>
    private static void MetadaneKolekcjiPrzeżywajązmianęOstatniegoOdcinka()
    {
        WOknie(
            state => Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary),
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var item = DodajPodgląd(window, "https://www.youtube.com/watch?v=METADANE1", "Metadane 1");
                // Metadane ustawiamy tak, jak robi to uzytkownik: na ZYWYM
                // wierszu kolekcji. CapturePodcastState przepisuje wlasnie z
                // niego stan trwaly, wiec ustawienie samego rekordu nie
                // odwzorowywaloby produkcji.
                var wierszKolekcji = PozycjeSesji(window).FirstOrDefault(row =>
                    string.Equals(
                        row.Id,
                        PublicInternetMediaCollections.PreviewId,
                        StringComparison.Ordinal));
                if (wierszKolekcji is not null)
                {
                    wierszKolekcji.Title = "Moje podglądy";
                    wierszKolekcji.HasCustomTitle = true;
                    wierszKolekcji.IsFavorite = true;
                }
                var podglądy = Subskrypcja(state, PublicInternetMediaCollections.PreviewId);
                podglądy.Title = "Moje podglądy";
                podglądy.HasCustomTitle = true;
                podglądy.IsFavorite = true;

                Polecenie(window, item, AccessibleMediaController.Core.Commands.CommandIds.ToggleLibrary);

                var poAwansie = state.Podcasts.Subscriptions.FirstOrDefault(subscription =>
                    string.Equals(
                        subscription.Id,
                        PublicInternetMediaCollections.PreviewId,
                        StringComparison.Ordinal));
                Sprawdź(
                    poAwansie is not null,
                    "Awans ostatniego materiału USUNĄŁ kolekcję podglądów razem z jej metadanymi.");
                Sprawdź(
                    poAwansie!.HasCustomTitle
                    && string.Equals(poAwansie.Title, "Moje podglądy", StringComparison.Ordinal),
                    $"Własna nazwa kolekcji przepadła (tytuł: {poAwansie.Title}, własny: {poAwansie.HasCustomTitle}).");
                Sprawdź(
                    poAwansie.IsFavorite,
                    "Kolekcja przestała być ulubiona po awansie ostatniego materiału.");
            });
    }

    /// <summary>
    /// L4 z review: Ctrl+Shift+L na MIESZANYM zaznaczeniu (publiczny material +
    /// zwykly podcast RSS) nie moze cicho pominac czesci zaznaczenia. Nic nie
    /// mutujemy i mowimy wprost, ze trzeba osobnych zaznaczen.
    /// </summary>
    private static void MieszaneZaznaczenieNiczegoNieZmienia()
    {
        WOknie(
            state =>
            {
                Podstawa(state, SearchResultEnterBehavior.OpenWithoutLibrary);
                state.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings
                {
                    Id = "kanal-rss-mieszany",
                    Title = "Kanał RSS",
                    FeedUrl = "https://example.test/rss-mieszany.xml",
                    SourceKind = PodcastSourceKind.Rss,
                    IsInLibrary = true
                });
                state.Podcasts.Episodes.Add(new PodcastEpisodeSettings
                {
                    Id = "odcinek-rss-mieszany",
                    SubscriptionId = "kanal-rss-mieszany",
                    Title = "Odcinek RSS",
                    MediaUrl = "https://example.test/rss-mieszany/1.mp3",
                    MediaType = "audio/mpeg"
                });
            },
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var publiczny = DodajPodgląd(window, "https://www.youtube.com/watch?v=MIESZANE1", "Mieszane 1");
                var rss = PozycjeSesji(window).FirstOrDefault(row =>
                    string.Equals(row.Id, "odcinek-rss-mieszany", StringComparison.Ordinal))
                    ?? throw new Exception("Brak pozycji odcinka RSS w żywej sesji.");

                var komunikat = PoleceniePrzechwytująceKomunikat(
                    window,
                    [publiczny, rss],
                    AccessibleMediaController.Core.Commands.CommandIds.ToggleLibrary);

                Sprawdź(
                    PublicInternetMediaCollections.IsPreview(Odcinek(state, publiczny.Id).SubscriptionId),
                    "Mieszane zaznaczenie zmieniło członkostwo publicznego materiału "
                    + $"(kolekcja: {Odcinek(state, publiczny.Id).SubscriptionId}).");
                Sprawdź(
                    Subskrypcja(state, "kanal-rss-mieszany").IsInLibrary,
                    "Mieszane zaznaczenie zmieniło członkostwo kanału RSS.");
                Sprawdź(
                    komunikat is not null
                    && komunikat.Contains("osobno", StringComparison.OrdinalIgnoreCase),
                    "Mieszane zaznaczenie nie powiedziało, że materiały internetowe i podcasty "
                    + $"trzeba zaznaczać osobno (komunikat: {komunikat ?? "brak"}).");
            });
    }

    private static void KolejkaNieDodajePrzyWłączonymEnter()
    {
        WOknie(
            state => Podstawa(state, SearchResultEnterBehavior.AddToLibrary),
            (window, state, przejscie) =>
            {
                if (przejscie != 0) return;
                var wynik = WynikWyszukiwaniaYouTube("QUEUE_WITH_ENTER_ON", "Tylko do kolejki");
                DziałanieWyniku(window, [wynik], SearchResultAction.Queue);
                var episode = state.Podcasts.Episodes.Single(e => e.MediaUrl == wynik.Source);
                Sprawdź(PublicInternetMediaCollections.IsPreview(episode.SubscriptionId),
                    "Kolejka dodała materiał do Biblioteki przez opcję przeznaczoną dla Enter.");
                Sprawdź(PozycjeSesji(window).Any(i => i.Id == episode.Id && i.IsInQueue),
                    "Próba nie wykonała rzeczywistego dodania do kolejki.");
            });
    }

    private static void DodanieNieMaDomyślnejIntencji()
    {
        // Wymóg strukturalny: każdy caller musi jawnie wybrać dodanie/podgląd.
        // Kompilator blokuje wtedy pominięcie flagi w ręcznym Dodaj podcast/YouTube.
        var method = typeof(MainWindow).GetMethod("AddPublicInternetMedia", Flags)!;
        Sprawdź(!method.GetParameters().Single(p => p.Name == "addToLibrary").HasDefaultValue,
            "Ręczne dodanie nadal może niejawnie odziedziczyć tryb podglądu.");
    }

    private static string AdresYouTube(string identyfikator) =>
        $"https://www.youtube.com/watch?v={identyfikator}";

    /// <summary>Wynik wyszukiwania YouTube taki, jaki daje okno wyszukiwania.</summary>
    private static MediaItem WynikWyszukiwaniaYouTube(string identyfikator, string title) =>
        new()
        {
            Id = $"internet-media-search:youtube:{identyfikator}",
            Kind = MediaItemKind.Episode,
            Title = title,
            Artist = "Kanał testowy",
            Duration = TimeSpan.FromMinutes(3),
            PublicUri = AdresYouTube(identyfikator),
            Source = AdresYouTube(identyfikator)
        };

    /// <summary>
    /// Prawdziwe ExecuteSearchResultAction - ta sama metoda, ktora wola okno
    /// wyszukiwania po wybraniu dzialania na zaznaczonych wynikach.
    /// </summary>
    private static string? DziałanieWyniku(
        MainWindow window,
        MediaItem[] items,
        SearchResultAction action)
    {
        var results = items
            .Select(item => new SearchWindow.SearchResult("podcasts", item))
            .ToArray();
        return (string?)typeof(MainWindow)
            .GetMethod("ExecuteSearchResultAction", Flags)!
            .Invoke(window, [results, results, action, false]);
    }

    /// <summary>Prawdziwe polecenie na kilku zaznaczonych pozycjach z komunikatem.</summary>
    private static string? PoleceniePrzechwytująceKomunikat(
        MainWindow window,
        MediaItem[] items,
        string commandId)
    {
        var sesje = (SessionManager)typeof(MainWindow).GetField("_sessions", Flags)!.GetValue(window)!;
        sesje.SelectSession("podcasts");
        var override_ = typeof(MainWindow).GetField("_actionItemsOverride", Flags)!;
        var capture = typeof(MainWindow).GetField("_captureAnnouncements", Flags)!;
        var captured = typeof(MainWindow).GetField("_capturedAnnouncement", Flags)!;
        override_.SetValue(window, items);
        capture.SetValue(window, true);
        captured.SetValue(window, null);
        try
        {
            typeof(MainWindow).GetMethod("ExecuteCommand", Flags, null, [typeof(string)], null)!
                .Invoke(window, [commandId]);
            return (string?)captured.GetValue(window);
        }
        finally
        {
            capture.SetValue(window, false);
            override_.SetValue(window, null);
        }
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

    /// <summary>
    /// Uruchamia wszystkie podane sprawdzenia i dopiero na koncu zglasza
    /// zbiorcza wpadke - raport pokazuje KAZDY niespelniony warunek.
    /// </summary>
    private static void WszystkieNaraz(params (string Nazwa, Action Sprawdzenie)[] sprawdzenia)
    {
        var wpadki = new List<string>();
        foreach (var (nazwa, sprawdzenie) in sprawdzenia)
        {
            try
            {
                sprawdzenie();
            }
            catch (Exception exception)
            {
                wpadki.Add($"[{nazwa}] {exception.InnerException?.Message ?? exception.Message}");
            }
        }
        if (wpadki.Count > 0)
            throw new Exception(string.Join(Environment.NewLine, wpadki));
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
