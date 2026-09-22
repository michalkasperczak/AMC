using System.Reflection;
using System.Runtime.CompilerServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

internal static class PeriodicPlaybackCheckpointTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Spotify", RunSpotify),
            ("Pliki lokalne", RunLocal),
            ("Zgodność lokalnego capture", RunLocalCaptureEquivalence),
            ("Podcasty", RunPodcasts),
            ("Powłoka zapisu bez kopiowania bibliotek", RunSettingsShell),
            ("Scalanie checkpointów", PlaybackCheckpointQueueTests.RunCoalescing),
            ("Pełny zapis i usuwanie", PlaybackCheckpointQueueTests.RunFullSaveOrdering),
            ("Awaria i pierwszy checkpoint", PlaybackCheckpointQueueTests.RunFailureAndFirstCheckpoint)
        };
        var failures = new List<string>();
        foreach (var test in tests)
        {
            try { test.Run(); }
            catch (Exception e) { failures.Add(test.Name + ": " + (e.InnerException ?? e).Message); }
        }
        Console.WriteLine($"Checkpointy: {tests.Length - failures.Count} OK / {failures.Count} BLAD / razem {tests.Length}");
        if (failures.Count > 0) throw new Exception(string.Join(Environment.NewLine, failures));
    }

    private static void RunSpotify()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-periodic-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ConfigurationStore(Path.Combine(root, "state.json"));
            var state = store.LoadOrCreate();
            var manager = new SessionManager(state.Settings);
            var item = new MediaItem
            {
                Id = "checkpoint-track", Title = "Utwór testowy", Kind = MediaItemKind.Track,
                Source = "spotify:track:checkpoint", Duration = TimeSpan.FromMinutes(10)
            };
            var (session, _) = manager.AddOrUpdateTransientSession("spotify", "Spotify", [item], output: null, preferredSlot: 7);
            session.SelectItem(item);
            session.SetPosition(TimeSpan.FromSeconds(75));
            state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(item));
            using var saved = new AutoResetEvent(false);
            var clones = 0;
            var queue = new StatePersistenceQueue(
                s => { Interlocked.Increment(ref clones); return store.CloneState(s); },
                s => { store.Save(s); saved.Set(); });
            queue.Queue(state);
            Check(saved.WaitOne(TimeSpan.FromSeconds(10)), "Nie zapisano bazowej migawki.");
            var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            Set(window, "_state", state);
            Set(window, "_sessions", manager);
            Set(window, "_statePersistence", queue);
            Set(window, "_lastSavedSpotifyPositions", new Dictionary<string, (string ItemId, long PositionTicks)>(StringComparer.Ordinal));
            var before = clones;
            Invoke(window, "SaveSpotifyStateIfDue");
            Check(saved.WaitOne(TimeSpan.FromSeconds(10)), "Timer Spotify nie zapisał pozycji.");
            var loaded = new ConfigurationStore(Path.Combine(root, "state.json")).LoadOrCreate();
            Check(SpotifyPlaybackSettingsResolver.ResolvePosition(loaded.Settings, item) == TimeSpan.FromSeconds(75),
                "Po restarcie utracono pozycję Spotify.");
            Check(clones == before, "Cykliczny zapis Spotify nadal kopiuje pełne biblioteki.");
            Console.WriteLine("OK: rzeczywisty timer Spotify zapisuje i odczytuje pozycję bez pełnej kopii bibliotek");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RunLocal()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-local-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ConfigurationStore(Path.Combine(root, "state.json"));
            var state = store.LoadOrCreate();
            var item = new MediaItem { Id = "local-test", Title = "Plik testowy", Kind = MediaItemKind.Track,
                Source = Path.Combine(root, "test.wav"), Duration = TimeSpan.FromMinutes(10), IsInLibrary = true };
            var savedItem = new LocalMediaItemSettings { Id = item.Id, Path = item.Source, Title = item.Title,
                DurationTicks = item.Duration.Ticks, ResumePositionMode = ResumePositionMode.Remember,
                ClipStartTicks = TimeSpan.FromSeconds(2).Ticks, ClipEndTicks = TimeSpan.FromSeconds(4).Ticks };
            state.LocalMedia.Items.Add(savedItem);
            var manager = new SessionManager(state.Settings);
            var (session, _) = manager.AddOrUpdateTransientSession("local", "Pliki lokalne", [item], null, 4);
            session.SelectItem(item); session.SetPosition(TimeSpan.FromSeconds(95));
            using var saved = new AutoResetEvent(false);
            var clones = 0;
            var queue = new StatePersistenceQueue(s => { clones++; return store.CloneState(s); }, s => { store.Save(s); saved.Set(); });
            queue.Queue(state);
            Check(saved.WaitOne(TimeSpan.FromSeconds(10)), "Nie zapisano lokalnej migawki bazowej.");
            var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            Set(window, "_state", state); Set(window, "_sessions", manager); Set(window, "_statePersistence", queue);
            Set(window, "_localItems", new List<MediaItem> { item });
            // Navigation has a separate real-window test; this headless case exercises the timer and storage.
            Set(window, "_restoringSessionNavigation", true);
            var before = clones;
            Invoke(window, "SaveLocalMediaStateIfDue");
            Check(saved.WaitOne(TimeSpan.FromSeconds(10)), "Timer lokalny nie zapisał pozycji.");
            var loaded = new ConfigurationStore(Path.Combine(root, "state.json")).LoadOrCreate();
            Check(loaded.LocalMedia.Items.Single().ResumePositionTicks == TimeSpan.FromSeconds(95).Ticks,
                "Po restarcie utracono pozycję pliku.");
            Check(clones == before, "Cykliczny zapis lokalny nadal kopiuje pełne biblioteki.");
            Check(ReferenceEquals(savedItem, state.LocalMedia.Items.Single()), "Timer przebudował niezmienioną bibliotekę lokalną.");
            Check(loaded.LocalMedia.Items.Single().ClipStartTicks == TimeSpan.FromSeconds(2).Ticks, "Checkpoint zmienił znacznik fragmentu.");
            Console.WriteLine("OK: rzeczywisty timer lokalny, pozycja i znaczniki po restarcie bez przebudowy biblioteki");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RunPodcasts()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-podcast-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new ConfigurationStore(Path.Combine(root, "state.json"));
            var state = store.LoadOrCreate();
            var subscription = new PodcastSubscriptionSettings { Id = "sub", Title = "Podcast testowy", FeedUrl = "https://example.test/feed" };
            var episode = new PodcastEpisodeSettings { Id = "episode", SubscriptionId = subscription.Id,
                Title = "Odcinek testowy", MediaUrl = "https://example.test/audio.mp3", IsNew = true,
                DurationTicks = TimeSpan.FromMinutes(10).Ticks };
            state.Podcasts.Subscriptions.Add(subscription); state.Podcasts.Episodes.Add(episode);
            var item = new MediaItem { Id = episode.Id, Title = episode.Title, Kind = MediaItemKind.Episode,
                ExternalId = subscription.Id, Source = episode.MediaUrl, Duration = TimeSpan.FromTicks(episode.DurationTicks) };
            var manager = new SessionManager(state.Settings);
            var (session, _) = manager.AddOrUpdateTransientSession("podcasts", "Podcasty", [item], null, 6);
            session.SelectItem(item); session.SetPosition(TimeSpan.FromSeconds(85));
            using var saved = new AutoResetEvent(false);
            var clones = 0;
            var queue = new StatePersistenceQueue(s => { clones++; return store.CloneState(s); }, s => { store.Save(s); saved.Set(); });
            queue.Queue(state); Check(saved.WaitOne(TimeSpan.FromSeconds(10)), "Brak bazowego zapisu podcastu.");
            var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            Set(window, "_state", state); Set(window, "_sessions", manager); Set(window, "_statePersistence", queue);
            Set(window, "_podcastItems", new List<MediaItem> { item });
            var before = clones;
            Invoke(window, "SavePodcastStateIfDue");
            Check(saved.WaitOne(TimeSpan.FromSeconds(10)), "Timer podcastu nie zapisał pozycji.");
            var loaded = new ConfigurationStore(Path.Combine(root, "state.json")).LoadOrCreate();
            var result = loaded.Podcasts.Episodes.Single();
            Check(result.ResumePositionTicks == TimeSpan.FromSeconds(85).Ticks, "Po restarcie utracono pozycję odcinka.");
            Check(result.IsStarted && !result.IsNew && !result.IsPlayed, "Po restarcie utracono postęp słuchania odcinka.");
            Check(clones == before, "Cykliczny zapis podcastu nadal kopiuje pełne biblioteki.");
            Console.WriteLine("OK: rzeczywisty timer podcastu, pozycja i postęp po restarcie bez pełnej kopii bibliotek");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RunLocalCaptureEquivalence()
    {
        var state = ConfigurationStore.CreateDefaultState();
        var current = new MediaItem { Id = "current", Title = "Bieżący", Source = "C:\\test\\current.wav", Duration = TimeSpan.FromMinutes(10), IsInLibrary = true };
        var other = new MediaItem { Id = "other", Title = "Drugi", Source = "C:\\test\\other.wav", Duration = TimeSpan.FromMinutes(10), IsInLibrary = true };
        foreach (var item in new[] { current, other }) state.LocalMedia.Items.Add(new LocalMediaItemSettings
        { Id = item.Id, Title = item.Title, Path = item.Source!, DurationTicks = item.Duration.Ticks, ResumePositionTicks = 123,
            ResumePositionMode = ResumePositionMode.Remember });
        state.LocalMedia.CustomOrderItemIds = [current.Id, other.Id];
        var store = new ConfigurationStore("unused-local-test.json");
        var expected = store.CloneState(state);
        var actual = store.CloneState(state);
        foreach (var (data, checkpoint) in new[] { (expected, false), (actual, true) })
        {
            var manager = new SessionManager(data.Settings);
            var (session, _) = manager.AddOrUpdateTransientSession("local", "Pliki", [current, other], null, 4);
            session.SelectItem(current); session.SetPosition(TimeSpan.FromSeconds(90));
            var queue = new StatePersistenceQueue(store.CloneState, _ => { });
            var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            Set(window, "_state", data); Set(window, "_sessions", manager); Set(window, "_statePersistence", queue);
            Set(window, "_localItems", new List<MediaItem> { current, other }); Set(window, "_restoringSessionNavigation", true);
            Invoke(window, checkpoint ? "SaveLocalMediaStateIfDue" : "CaptureLocalMediaState");
            Check(queue.Flush(data, TimeSpan.FromSeconds(5), out _), "Brak zakończenia testowej kolejki.");
        }
        if (System.Text.Json.JsonSerializer.Serialize(expected) != System.Text.Json.JsonSerializer.Serialize(actual))
        {
            Console.WriteLine("EXPECTED LOCAL " + System.Text.Json.JsonSerializer.Serialize(expected.LocalMedia));
            Console.WriteLine("ACTUAL LOCAL " + System.Text.Json.JsonSerializer.Serialize(actual.LocalMedia));
        }
        Check(System.Text.Json.JsonSerializer.Serialize(expected) == System.Text.Json.JsonSerializer.Serialize(actual),
            "Lokalny checkpoint zapisuje inne pola lub pozycje niż pełny CaptureLocalMediaState.");
        Console.WriteLine("OK: lokalny checkpoint zgodny z pełnym capture, także bez zapamiętanej pozycji drugiego pliku");
    }

    private static void RunSettingsShell()
    {
        var state = ConfigurationStore.CreateDefaultState();
        state.LocalMedia.Items.Add(new LocalMediaItemSettings { Id = "shell-local", Title = "Nagranie", Path = "C:\\test\\audio.wav" });
        state.Podcasts.Episodes.Add(new PodcastEpisodeSettings { Id = "shell-episode", Title = "Odcinek" });
        state.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(
            new MediaItem { Id = "shell-spotify", Title = "Utwór", Source = "spotify:track:shell" }));
        var original = System.Text.Json.JsonSerializer.Serialize(state);
        var store = new ConfigurationStore("unused-test-state.json");
        var shell = (PersistedState)typeof(ConfigurationStore).GetMethod("CreateSettingsOnlyState", Fields)!.Invoke(store, [state])!;
        Check(shell.LocalMedia.Items.Count == 0 && shell.Podcasts.Episodes.Count == 0,
            "Powłoka JSON dubluje dane baz SQLite.");
        Check(System.Text.Json.JsonSerializer.Serialize(state) == original, "Tworzenie powłoki zmieniło wejściowy stan.");
        Check(ReferenceEquals(shell.Spotify, state.Spotify) && ReferenceEquals(shell.Tidal, state.Tidal),
            "Zapis w tle ponownie kopiuje niezmienione katalogi przed ich serializacją.");
        Console.WriteLine("OK: zapis JSON nie kopiuje katalogów i nie zmienia prywatnej migawki");
    }

    private static void Set(MainWindow window, string name, object value) =>
        typeof(MainWindow).GetField(name, Fields)!.SetValue(window, value);

    private static void Invoke(MainWindow window, string name) =>
        typeof(MainWindow).GetMethod(name, Fields)!.Invoke(window, null);

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
