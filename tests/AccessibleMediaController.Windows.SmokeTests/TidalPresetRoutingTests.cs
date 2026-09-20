using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

internal static class TidalPresetRoutingTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    internal static void Run()
    {
        WithWindow((window, session, desktop, preview) =>
        {
            var view = Field(window, "_currentView");
            var selected = window.MediaList.SelectedItem;
            var rows = window.MediaList.Items.Cast<object>().ToArray();
            var focus = Keyboard.FocusedElement;
            Activate(window, 5);
            Check(preview.Plays == 0, "Preset TIDAL uruchomil wbudowany odtwarzacz probek.");
            Check(desktop.Requests.Count == 1, "Preset TIDAL nie przekazal zadania oryginalnemu odtwarzaczowi.");
            Check(desktop.Requests[0].PageUri == "https://desktop.tidal.com/album/1564288"
                && desktop.Requests[0].TrackTitle == "Hard Times", "Preset wskazal niewlasciwy utwor lub album.");
            var queue = (TidalDesktopTrackQueue)Field(window, "_tidalTrackQueue")!;
            Check(queue.Count == 2 && queue.HumanPosition == 2 && queue.SourceName == "Presety",
                "Kolejka desktop nie odpowiada kolejnosci slotow presetow.");
            Check(session.PlaybackContextItemIds.SequenceEqual(new[] { "first", "hard" }), "Zmieniona kolejnosc kontekstu presetow.");
            Check(Equals(view, Field(window, "_currentView")) && ReferenceEquals(selected, window.MediaList.SelectedItem)
                && rows.SequenceEqual(window.MediaList.Items.Cast<object>()) && ReferenceEquals(focus, Keyboard.FocusedElement),
                "Preset przestawil widok, wiersze, zaznaczenie lub fokus.");
            desktop.AlreadyCurrent = true;
            session.SetPlaybackContext(new[] { "hard", "first" });
            queue.Capture(session.Items.AsEnumerable().Reverse(), session.Items.Single(i => i.Id == "hard"), "Zachowana kolejka");
            Activate(window, 5);
            Check(desktop.Requests.Count == 2 && preview.Plays == 0, "Powtorzenie presetu wraca do probek.");
            Check(queue.SourceName == "Zachowana kolejka" && session.PlaybackContextItemIds.SequenceEqual(new[] { "hard", "first" }),
                "Juz grajacy preset TIDAL zmienil kolejke.");
            Check((string?)Field(window, "_capturedAnnouncement") == "Hard Times", "Powtorzenie TIDAL nie mowi samej nazwy.");
        });
        foreach (var missing in new[] { "album", "id" })
            WithWindow((window, session, desktop, preview) =>
            {
                var item = session.Items.Single(i => i.Id == "hard");
                if (missing == "album") item.RelatedAlbumExternalId = null;
                else item.ExternalId = null;
                Activate(window, 5);
                Check(preview.Plays == 0 && desktop.Requests.Count == 0, "Niepelne dane utworu uruchomily niewlasciwy odtwarzacz.");
                Check(!string.IsNullOrWhiteSpace((string?)Field(window, "_capturedAnnouncement")), "Brak komunikatu o niepelnych danych.");
            });
        WithWindow((window, session, desktop, preview) =>
        {
            desktop.NeedsRestart = true;
            Activate(window, 5, background: true);
            Check(desktop.Requests.Count == 1 && preview.Plays == 0, "Brak zgody na restart nie moze uruchamiac probki.");
            Check(((string?)Field(window, "_capturedAnnouncement"))?.Contains("AMC", StringComparison.Ordinal) == true,
                "Skrot z tla ma poprosic o potwierdzenie w AMC, nie otwierac okna pytania.");
        });
        foreach (var sessionId in new[] { "local", "radio", "podcasts", "spotify" })
            WithWindow((window, session, desktop, output) =>
            {
                Activate(window, 5);
                Check(output.Plays == 1, "Pierwsze uruchomienie presetu nie gra: " + sessionId);
                output.Position = TimeSpan.FromSeconds(37);
                session.SetPlaybackContext(new[] { "hard", "first" });
                var context = session.PlaybackContextItemIds.ToArray();
                var selected = window.MediaList.SelectedItem;
                var view = Field(window, "_currentView");
                Activate(window, 5);
                Check(output.Plays == 1, "Powtorzony preset ponownie uruchomil grajacy material: " + sessionId);
                Check(session.PlaybackContextItemIds.SequenceEqual(context), "Powtorzenie zmienilo kolejke: " + sessionId);
                Check(ReferenceEquals(selected, window.MediaList.SelectedItem) && Equals(view, Field(window, "_currentView")), "Powtorzenie zmienilo widok: " + sessionId);
                Check((string?)Field(window, "_capturedAnnouncement") == "Hard Times", "Powtorzenie nie podalo samej nazwy: " + sessionId);
                session.TogglePlayback();
                Activate(window, 5);
                Check(output.Plays == 2 && output.LastStart == TimeSpan.FromSeconds(37) && session.IsPlaying,
                    "Preset w pauzie nie wznowil biezacego miejsca: " + sessionId);
                Check(session.PlaybackContextItemIds.SequenceEqual(context), "Wznowienie zmienilo kolejke: " + sessionId);
                Activate(window, 3);
                Check(output.Plays == 3 && output.LoadedItemId == "first", "Inny preset nie rozpoczal odtwarzania: " + sessionId);
            }, sessionId);
        Console.WriteLine("OK: TIDAL preset -> desktop; powtorzenie utworu bez restartu/kolejki, pauza wznawia pozycje");
    }

    private static void Activate(MainWindow window, int slot, bool background = false) =>
        typeof(MainWindow).GetMethod("ActivatePreset", Private)!.Invoke(window, [slot, true, background]);
    private static object? Field(MainWindow window, string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
    private static void Check(bool good, string message) { if (!good) throw new Exception(message); }
    private static MediaItem Track(string id, string external, string title) => new()
    {
        Id = id, ExternalId = external, Title = title, Kind = MediaItemKind.Track,
        RelatedAlbumExternalId = "albums:1564288", RelatedAlbumTitle = "Album probny",
        IsFavorite = true, IsAvailable = true
    };
    private static void WithWindow(Action<MainWindow, DemoMediaSession, Desktop, Preview> test, string sessionId = "tidal")
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-tidal-preset-test-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var store = new ConfigurationStore(Path.Combine(root, "state.json"), Path.Combine(root, "library.db"), Path.Combine(root, "podcasts.db"));
                var state = store.LoadOrCreate();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = sessionId;
                state.Tidal.ClientId = string.Empty;
                state.Spotify.ClientId = string.Empty;
                state.Podcasts.Subscriptions.Clear(); state.Podcasts.Episodes.Clear();
                state.Radio.Stations.Clear(); state.LocalMedia.FolderSources.Clear();
                var first = Track("first", "tracks:1564298", "Pierwszy");
                var target = Track("hard", "tracks:1564299", "Hard Times");
                state.Tidal.CachedCollectionItems = [TidalCachedCollectionItemSettings.FromMediaItem(first), TidalCachedCollectionItemSettings.FromMediaItem(target)];
                state.SessionPresets.EntriesBySession[sessionId] =
                [
                    new() { Slot = 5, TargetId = target.Id, TargetKind = "track", TargetTitle = target.Title },
                    new() { Slot = 3, TargetId = first.Id, TargetKind = "track", TargetTitle = first.Title }
                ];
                window = new MainWindow(state, store);
                typeof(MainWindow).GetField("_captureAnnouncements", Private)!.SetValue(window, true);
                var desktop = new Desktop();
                typeof(MainWindow).GetField("_tidalDesktop", Private)!.SetValue(window, desktop);
                var manager = (SessionManager)Field(window, "_sessions")!;
                manager.SelectSession(sessionId);
                var session = manager.Current;
                session.ReplaceItems([first, target]);
                var preview = new Preview();
                typeof(DemoMediaSession).GetField("_output", Private)!.SetValue(session, preview);
                typeof(MainWindow).GetField("_currentView", Private)!.SetValue(window, "Ulubione");
                typeof(MainWindow).GetMethod("RefreshCurrentView", Private)!.Invoke(window, [null, null]);
                window.MediaList.SelectedIndex = 0;
                test(window, session, desktop, preview);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(45))) throw new Exception("Tidal preset test timed out.");
        if (failure is not null) throw new Exception("Tidal preset routing test failed.", failure);
    }
    private sealed class Desktop : ITidalDesktopPlayback
    {
        public List<TidalDesktopPlayRequest> Requests { get; } = [];
        public bool NeedsRestart { get; set; }
        public bool AlreadyCurrent { get; set; }
        public Task<TidalDesktopPlayResult> PlayAsync(TidalDesktopPlayRequest request, bool restartConsent = false, CancellationToken token = default)
        {
            Requests.Add(request);
            return Task.FromResult(new TidalDesktopPlayResult { Success = !NeedsRestart, WasAlreadyCurrent = AlreadyCurrent, NeedsRestartConsent = NeedsRestart, Message = NeedsRestart ? "Wymagane potwierdzenie restartu" : "Test przekazania" });
        }
    }
    private sealed class Preview : IMediaOutput
    {
        public int Plays { get; private set; }
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan LastStart { get; private set; }
        public bool SupportsPlaybackRate => false;
        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate) { Plays++; LoadedItemId = item.Id; LastStart = position; }
        public void Pause() { }
        public void Stop() { }
        public void Seek(TimeSpan position) { }
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double playbackRate) { }
    }
}
