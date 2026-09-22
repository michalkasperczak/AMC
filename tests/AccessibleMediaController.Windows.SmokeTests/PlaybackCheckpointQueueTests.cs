using System.Reflection;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Services;

internal static class PlaybackCheckpointQueueTests
{
    internal static void RunCoalescing()
    {
        using var fixture = new Fixture();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = 0;
        var queue = new StatePersistenceQueue(fixture.Store.CloneState, state =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Nie zwolniono zapisu kontrolnego.");
            }
            fixture.Store.Save(state);
        });
        try
        {
            queue.Queue(fixture.State);
            Check(started.Wait(TimeSpan.FromSeconds(5)), "Brak rozpoczęcia zapisu.");
            fixture.State.Podcasts.Episodes[0].ResumePositionTicks = 100;
            queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CapturePodcasts(fixture.State, ["ep-a"]));
            fixture.State.Podcasts.Episodes[1].ResumePositionTicks = 200;
            queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CapturePodcasts(fixture.State, ["ep-b"]));
            release.Set(); Wait(queue);
            fixture.AssertEquivalent();
        }
        finally { release.Set(); Wait(queue); }
        Console.WriteLine("OK: łączenie checkpointów zachowuje odcinek usunięty z późniejszego zestawu roboczego");
    }

    internal static void RunFullSaveOrdering()
    {
        using var fixture = new Fixture();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = 0;
        var queue = new StatePersistenceQueue(fixture.Store.CloneState, state =>
        {
            if (Interlocked.Increment(ref writes) == 1)
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            fixture.Store.Save(state);
        });
        try
        {
            queue.Queue(fixture.State); Check(started.Wait(TimeSpan.FromSeconds(5)), "Brak bazowego zapisu.");
            fixture.State.Podcasts.Episodes[0].ResumePositionTicks = 100;
            queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CapturePodcasts(fixture.State, ["ep-a"]));
            fixture.State.Settings.SpotifyPlayback.ItemsByKey["spotify:track:test"].PositionTicks = 999;
            queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CaptureSpotify(fixture.State));
            fixture.State.Podcasts.Episodes.RemoveAt(0);
            fixture.State.LocalMedia.Items.Clear();
            fixture.State.Spotify.CachedCollectionItems.Clear();
            fixture.State.Settings.SpotifyPlayback.ItemsByKey.Clear();
            fixture.State.Settings.PrefixTimeoutMilliseconds = 4321;
            queue.Queue(fixture.State);
            fixture.State.Podcasts.Episodes[0].ResumePositionTicks = 300;
            queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CapturePodcasts(fixture.State, ["ep-b"]));
            // The accepted checkpoint must not retain a reference to this live record.
            fixture.State.Podcasts.Episodes[0].ResumePositionTicks = 400;
            release.Set(); Wait(queue);
            fixture.State.Podcasts.Episodes[0].ResumePositionTicks = 300;
            fixture.AssertEquivalent();
            fixture.State.Podcasts.Episodes.Clear();
            Check(queue.Flush(fixture.State, TimeSpan.FromSeconds(10), out var failure), "Końcowy zapis nieudany: " + failure?.Message);
            fixture.AssertEquivalent();
        }
        finally { release.Set(); Wait(queue); }
        Console.WriteLine("OK: pełny zapis, celowe usunięcia, odłączony checkpoint i końcowy Flush zachowują całość danych");
    }

    internal static void RunFailureAndFirstCheckpoint()
    {
        using var fixture = new Fixture();
        using var failed = new ManualResetEventSlim();
        var clones = 0;
        var reject = false;
        var queue = new StatePersistenceQueue(s => { clones++; return fixture.Store.CloneState(s); }, s =>
        {
            if (reject) throw new IOException("Kontrolowany błąd zapisu");
            fixture.Store.Save(s);
        }, _ => failed.Set());
        queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CaptureSpotify(fixture.State));
        Wait(queue); Check(clones == 1, "Pierwszy checkpoint nie utworzył dokładnie jednej pełnej migawki.");
        fixture.AssertEquivalent();
        reject = true;
        fixture.State.Podcasts.Episodes[0].ResumePositionTicks = 500;
        queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CapturePodcasts(fixture.State, ["ep-a"]));
        Wait(queue); Check(failed.IsSet, "Awaria zapisu nie została zgłoszona.");
        reject = false;
        fixture.State.Podcasts.Episodes[1].ResumePositionTicks = 600;
        queue.QueueCheckpoint(fixture.State, PlaybackStateCheckpoint.CapturePodcasts(fixture.State, ["ep-b"]));
        Wait(queue); Check(clones == 1, "Ponowienie checkpointu kopiuje biblioteki.");
        fixture.AssertEquivalent();
        reject = true;
        Check(!queue.Flush(fixture.State, TimeSpan.FromSeconds(10), out var failure) && failure is IOException,
            "Flush ogłosił sukces mimo błędu końcowego zapisu.");
        reject = false;
        Check(queue.Flush(fixture.State, TimeSpan.FromSeconds(10), out _), "Flush nie odzyskał zapisu po błędzie.");
        fixture.AssertEquivalent();
        Console.WriteLine("OK: pierwszy checkpoint, zgłoszenie błędu, ponowienie bez utraty danych i końcowy błąd Flush");
    }

    private static void Wait(StatePersistenceQueue queue)
    {
        var task = (Task?)typeof(StatePersistenceQueue).GetField("_worker", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(queue);
        Check(task is null || task.Wait(TimeSpan.FromSeconds(15)), "Kolejka nie zakończyła pracy.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "amc-checkpoint-queue-" + Guid.NewGuid().ToString("N"));
        internal ConfigurationStore Store { get; }
        internal PersistedState State { get; }
        internal Fixture()
        {
            Directory.CreateDirectory(_root);
            Store = new ConfigurationStore(Path.Combine(_root, "actual", "state.json"));
            State = Store.LoadOrCreate();
            State.Podcasts.Subscriptions.Add(new PodcastSubscriptionSettings { Id = "sub", Title = "Test", FeedUrl = "https://example.test/feed" });
            foreach (var id in new[] { "ep-a", "ep-b" }) State.Podcasts.Episodes.Add(new PodcastEpisodeSettings
            { Id = id, SubscriptionId = "sub", Title = id, Description = "Opis zachowany w całości", MediaUrl = "https://example.test/" + id + ".mp3", IsNew = true });
            State.LocalMedia.Items.Add(new LocalMediaItemSettings { Id = "file", Title = "Test", Path = Path.Combine(_root, "audio.wav"), ClipStartTicks = 100, ClipEndTicks = 200 });
            State.Spotify.CachedCollectionItems.Add(TidalCachedCollectionItemSettings.FromMediaItem(new AccessibleMediaController.Core.Sessions.MediaItem
            { Id = "spotify-test", Title = "Test", Source = "spotify:track:test" }));
            State.Settings.SpotifyPlayback.ItemsByKey["spotify:track:test"] = new SpotifyItemPlaybackSettings { PositionTicks = 120 };
            State.SessionNavigation.Sessions["local"] = new SessionNavigationState { CurrentView = "Foldery", Filters = new() { ["Foldery"] = "test" } };
            State.SearchHistory.Entries["local"] = ["fraza testowa"];
        }
        internal void AssertEquivalent()
        {
            var expectedStore = new ConfigurationStore(Path.Combine(_root, "expected", "state.json"));
            expectedStore.Save(Store.CloneState(State));
            var expected = expectedStore.LoadOrCreate();
            var actual = new ConfigurationStore(Path.Combine(_root, "actual", "state.json")).LoadOrCreate();
            Check(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual),
                "Checkpoint nie zachował pełnej zgodności danych z równoważnym pełnym zapisem (nie tylko liczności).");
        }
        public void Dispose() => Directory.Delete(_root, true);
    }
}
