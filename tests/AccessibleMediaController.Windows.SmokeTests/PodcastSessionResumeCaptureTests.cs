using System.Reflection;
using System.Runtime.CompilerServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

/// <summary>
/// Zapis stanu Podcastow musi stosowac TE SAMA polityke pamieci pozycji, co
/// odtwarzanie. Odtwarzanie pyta <c>ShouldRememberPodcastPosition</c> (odcinek,
/// podcast, sesja Podcasty), a <c>CapturePodcastState</c> pomijalo warstwe
/// sesji, wiec wybor "Zawsze od poczatku" dla sesji Podcasty nie usuwal
/// zapisanych pozycji odcinkow INNYCH niz biezacy - po restarcie AMC wracaly.
///
/// Pomiar NIE tworzy ani nie pokazuje okna: uzywa nieinicjalizowanego
/// <see cref="MainWindow"/> i wola dokladnie ten prywatny szew, ktory dziala w
/// programie, razem z prawdziwym <see cref="SessionManager"/> i prawdziwa
/// polityka pamieci pozycji sesji Podcasty.
/// </summary>
internal static class PodcastSessionResumeCaptureTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        var state = ConfigurationStore.CreateDefaultState();
        var subscription = new PodcastSubscriptionSettings { Id = "sub-1", Title = "Audycja", FeedUrl = "https://example.test/feed.xml" };
        var first = new PodcastEpisodeSettings { Id = "ep-1", SubscriptionId = "sub-1", Title = "Odcinek 1", MediaUrl = "https://example.test/1.mp3" };
        var second = new PodcastEpisodeSettings { Id = "ep-2", SubscriptionId = "sub-1", Title = "Odcinek 2", MediaUrl = "https://example.test/2.mp3" };
        state.Podcasts.Subscriptions.Add(subscription);
        state.Podcasts.Episodes.Add(first);
        state.Podcasts.Episodes.Add(second);

        var firstItem = new MediaItem { Id = "ep-1", Title = "Odcinek 1", Kind = MediaItemKind.Episode, ExternalId = "sub-1", Duration = TimeSpan.FromMinutes(30) };
        var secondItem = new MediaItem { Id = "ep-2", Title = "Odcinek 2", Kind = MediaItemKind.Episode, ExternalId = "sub-1", Duration = TimeSpan.FromMinutes(30) };

        // Uzytkownik najpierw ma pamietanie pozycji w sesji Podcasty.
        ResumePositionPolicy.SetSessionMode(state.Settings, "podcasts", ResumePositionMode.Remember);

        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        typeof(MainWindow).GetField("_state", Flags)!.SetValue(window, state);
        // GetUninitializedObject nie uruchamia inicjalizatorow pol, wiec liste
        // elementow sesji Podcasty podstawiamy jawnie.
        typeof(MainWindow).GetField("_podcastItems", Flags)!
            .SetValue(window, new List<MediaItem> { firstItem, secondItem });

        var manager = new SessionManager(state.Settings);
        typeof(MainWindow).GetField("_sessions", Flags)!.SetValue(window, manager);
        var rememberPolicy = typeof(MainWindow).GetMethod("ShouldRememberPodcastPosition", Flags)!;
        var (session, _) = manager.AddOrUpdateTransientSession(
            "podcasts",
            "Podcasty i YouTube",
            new[] { firstItem, secondItem },
            output: null,
            preferredSlot: 6,
            rememberPosition: item => (bool)rememberPolicy.Invoke(window, [item])!);

        // Rzeczywisty przebieg: odcinek 2 sluchany i zapamietany, potem powrot
        // do odcinka 1. Odcinek 2 przestaje byc biezacy, ale ma zapisana pozycje.
        session.SelectItem(secondItem);
        session.SetPosition(TimeSpan.FromMinutes(6));
        session.SelectItem(firstItem);
        session.SetPosition(TimeSpan.FromMinutes(3));

        var capture = typeof(MainWindow).GetMethod("CapturePodcastState", Flags)!;
        capture.Invoke(window, null);
        if (second.ResumePositionTicks != TimeSpan.FromMinutes(6).Ticks)
            throw new Exception("Zapis stanu Podcastow gubi pozycje odcinka innego niz biezacy przy pamietaniu pozycji.");

        AssertPersistedPositions(state, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(6));

        // Uzytkownik wybiera "Zawsze od poczatku" dla sesji Podcasty.
        ResumePositionPolicy.SetSessionMode(state.Settings, "podcasts", ResumePositionMode.StartFromBeginning);
        capture.Invoke(window, null);
        if (second.ResumePositionTicks != 0)
            throw new Exception(
                "Wybor „Zawsze od początku” dla sesji Podcasty nie usuwa zapisanej pozycji odcinka "
                + "innego niż bieżący: zapis stanu pomija warstwę sesji, więc po restarcie AMC pozycja wraca.");
        if (PodcastPlaybackSettingsResolver.ShouldRememberPosition(second, subscription, state.Settings))
            throw new Exception("Polityka sesji Podcasty nie obowiązuje przy zapisie stanu.");

        AssertPersistedPositions(state, TimeSpan.Zero, TimeSpan.Zero);

        // Wlasny wybor odcinka nadal wygrywa z ustawieniem sesji.
        second.ResumePositionMode = ResumePositionMode.Remember;
        session.SelectItem(secondItem);
        session.SetPosition(TimeSpan.FromMinutes(4));
        session.SelectItem(firstItem);
        capture.Invoke(window, null);
        if (second.ResumePositionTicks != TimeSpan.FromMinutes(4).Ticks)
            throw new Exception("Własne ustawienie odcinka „Pamiętaj pozycję” przestało działać przy zapisie stanu.");
        AssertPersistedPositions(state, TimeSpan.Zero, TimeSpan.FromMinutes(4));
    }

    private static void AssertPersistedPositions(PersistedState state, TimeSpan first, TimeSpan second)
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-podcast-resume-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "state.json");
            new ConfigurationStore(path).Save(state);
            var loaded = new ConfigurationStore(path).LoadOrCreate();
            if (loaded.Podcasts.Episodes.Count != 2
                || loaded.Podcasts.Episodes.Single(episode => episode.Id == "ep-1").ResumePositionTicks != first.Ticks
                || loaded.Podcasts.Episodes.Single(episode => episode.Id == "ep-2").ResumePositionTicks != second.Ticks)
                throw new Exception("Zapis i ponowny odczyt Podcastów przywrócił nieprawidłowe pozycje bieżącego lub wcześniejszego odcinka.");
            if (ResumePositionPolicy.GetSessionMode(loaded.Settings, "podcasts")
                != ResumePositionPolicy.GetSessionMode(state.Settings, "podcasts"))
                throw new Exception("Zapis zgubił regułę pamięci sesji Podcasty.");
        }
        finally { Directory.Delete(root, true); }
    }
}
