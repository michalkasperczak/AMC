using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class TransientPreviewAcceptanceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        var tests = new (string Name, Action<MainWindow> Test)[]
        {
            ("jawne przejście do Radia unieważnia powrót", ExplicitRadioNavigation),
            ("nowe odcinki nie zatrzymują bieżącego podglądu audio", InboxPreservesPlayback),
            ("powrót do filtra zachowuje fokus i zaznaczenie", ReturnToFilter),
            ("trzy podglądy wracają z każdej sesji", AllSessionsRoundTrip),
            ("jawny wybór innego widoku unieważnia powrót", ExplicitViewNavigation),
            ("dwa Escape po nagraniu przywracają widok bez restartu audio", RecordingPlayerReturn),
        };
        var failures = new List<string>();
        foreach (var (name, test) in tests)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                var root = Path.Combine(Path.GetTempPath(), "amc-preview-acceptance-" + Guid.NewGuid().ToString("N"));
                MainWindow? window = null;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                    var state = store.LoadOrCreate();
                    state.Settings.Updates.CheckAutomatically = false;
                    state.Settings.Updates.InstallOnExit = false;
                    state.Settings.LastSessionId = "tidal";
                    state.Tidal.ClientId = state.Spotify.ClientId = "";
                    state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                    state.Podcasts.Subscriptions.Clear(); state.Podcasts.Episodes.Clear();
                    state.LocalMedia.Items.Clear(); state.LocalMedia.FolderSources.Clear();
                    state.WiiM.Devices.Clear(); state.Radio.RecordingSchedules.Clear();
                    state.Radio.Stations.Clear(); state.Radio.WakeScheduledRecordings = false;
                    state.Podcasts.DownloadsFolder = Path.Combine(root, "downloads");
                    state.Radio.RecordingsFolder = Path.Combine(root, "recordings");
                    Directory.CreateDirectory(state.Radio.RecordingsFolder);
                    var recordingPath = Path.Combine(state.Radio.RecordingsFolder, "nagranie-proby.wav");
                    var format = new NAudio.Wave.WaveFormat(8000, 16, 1);
                    using (var writer = new NAudio.Wave.WaveFileWriter(recordingPath, format))
                        writer.Write(new byte[format.AverageBytesPerSecond * 10]);
                    state.LocalMedia.Items.Add(new LocalMediaItemSettings {
                        Id = "recording-test", Title = "Nagranie próby", Path = recordingPath,
                        IsRadioRecording = true, RadioRecordingCompletedUtcTicks = DateTime.UtcNow.Ticks,
                        DurationTicks = TimeSpan.FromSeconds(10).Ticks, IsInLibrary = true
                    });
                    store.Save(state);
                    window = new MainWindow(store.LoadOrCreate(), store) { SuppressDesktopIntegrationForTests = true };
                    typeof(MainWindow).GetField("_applicationUpdateStartOverride", Private)!.SetValue(window,
                        (Func<bool, bool>)(_ => throw new Exception("Test forbids installation")));
                    test(window);
                }
                catch (Exception e) { failure = e; }
                finally
                {
                    window?.Close();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                    try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(60))) failure = new Exception("Timeout");
            if (failure is null) Console.WriteLine("OK: " + name);
            else { failures.Add(name); Console.Error.WriteLine(name + ": " + failure); }
        }
        Console.WriteLine($"PODGLADY: {tests.Length - failures.Count} OK / {failures.Count} BLAD / razem {tests.Length}");
        if (failures.Count > 0) throw new Exception("Podglądy: " + string.Join("; ", failures));
    }

    private static void AllSessionsRoundTrip(MainWindow window)
    {
        var sessions = Sessions(window);
        var ids = sessions.Sessions.Select(s => s.Id).ToArray();
        Check(ids.Contains("spotify") && ids.Contains("appleMusic") && ids.Contains("wiim"), "Brak wszystkich sesji w pomiarze");
        var previews = new[] {
            (CommandIds.ViewActiveRadioRecordings, "radio", "Nagrywane"),
            (CommandIds.ViewRecordedRadioFiles, "local", "Historia nagrywania"),
            (CommandIds.ViewPodcastInbox, "podcasts", "Nowe odcinki")
        };
        foreach (var id in ids)
        foreach (var (command, host, view) in previews)
        {
            sessions.SelectSession(id);
            Call(window, "NavigateTo", "Ulubione");
            window.FilterBox.Text = "zapamiętany filtr " + id;
            Call(window, "ExecuteCommand", command);
            Pump();
            Check(sessions.Current.Id == host && (string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)! == view,
                $"{id}/{command}: zły cel podglądu");
            Check((bool)Call(window, "TryHandleTransientPreviewEscape")!, $"{id}/{command}: brak powrotu");
            Pump();
            Check(sessions.Current.Id == id && window.FilterBox.Text == "zapamiętany filtr " + id,
                $"{id}/{command}: utracona sesja lub filtr");
            Check((string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)! == "Ulubione", $"{id}/{command}: utracony widok");
        }
        Console.WriteLine($"Macierz podglądów: {ids.Length} sesji x {previews.Length} polecenia");
    }

    private static void ReturnToFilter(MainWindow window)
    {
        window.ContentRendered -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), window,
            typeof(MainWindow).GetMethod("Window_ContentRendered", Private)!);
        var radio = Sessions(window).SelectSession("radio")!;
        radio.ReplaceItems([
            new MediaItem { Id = "test-a", Title = "Test A", Kind = MediaItemKind.Station, IsInLibrary = true },
            new MediaItem { Id = "test-b", Title = "Test B", Kind = MediaItemKind.Station, IsInLibrary = true }
        ]);
        Call(window, "NavigateTo", "Biblioteka");
        window.Show();
        Pump();
        window.FilterBox.Text = "Test";
        Pump();
        window.MediaList.SelectedIndex = 1;
        var selected = window.MediaList.SelectedItem?.ToString();
        Check(selected is not null && window.MediaList.Items.Count == 2, "Kontrolka: brak wierszy");
        window.FilterBox.Focus();
        System.Windows.Input.Keyboard.Focus(window.FilterBox);
        window.FilterBox.Select(1, 2);
        Check(window.FilterBox.IsKeyboardFocused, "Kontrolka: filtr nie ma fokusu");
        var keys = new byte[256];
        Check(GetKeyboardState(keys), "Nie odczytano stanu klawiszy próby");
        var controlKeys = (byte[])keys.Clone(); controlKeys[0x11] = controlKeys[0xA2] = 0x80;
        try
        {
            Check(SetKeyboardState(controlKeys), "Nie ustawiono Ctrl w wątku próby");
            var input = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(window), Environment.TickCount, System.Windows.Input.Key.I)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent, Source = window.FilterBox };
            Call(window, "Window_PreviewKeyDown", window, input);
            Check(input.Handled && Sessions(window).Current.Id == "podcasts", "Ctrl+I z filtra nie otworzył podglądu");
        }
        finally { SetKeyboardState(keys); }
        Pump();
        Check((bool)Call(window, "TryHandleTransientPreviewEscape")!, "Brak powrotu z podglądu");
        Pump();
        Check(Sessions(window).Current.Id == "radio" && window.FilterBox.Text == "Test", "Powrót zgubił sesję lub filtr");
        Check(window.MediaList.SelectedItem?.ToString() == selected, "Powrót zgubił zaznaczony wiersz");
        Check(window.FilterBox.IsKeyboardFocused, "Powrót zgubił fokus filtra");
        Check(window.FilterBox.SelectionStart == 1 && window.FilterBox.SelectionLength == 2, "Powrót zgubił zaznaczenie tekstu filtra");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] keys);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetKeyboardState(byte[] keys);

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static void InboxPreservesPlayback(MainWindow window)
    {
        var session = Sessions(window).FindSession("podcasts")!;
        var output = new ObservedOutput();
        typeof(DemoMediaSession).GetField("_output", Private)!.SetValue(session, output);
        var item = new MediaItem { Id = "preview-playing", Title = "Odcinek podglądu", Kind = MediaItemKind.Episode,
            Source = "https://example.invalid/never-requested", Duration = TimeSpan.FromMinutes(10) };
        session.AddItemsById([item]);
        session.Play(item);
        session.SetPosition(TimeSpan.FromSeconds(47));
        var callsBefore = output.Calls;
        var currentBefore = session.CurrentItem;
        Sessions(window).SelectSession("tidal");
        Call(window, "ExecuteCommand", CommandIds.ViewPodcastInbox);
        Check(session.IsPlaying && ReferenceEquals(session.CurrentItem, currentBefore), "Podgląd zmienił grający odcinek");
        Check(output.Calls == callsBefore && session.Position == TimeSpan.FromSeconds(47), "Podgląd wykonał operację audio albo zmienił czas");
        Check((bool)Call(window, "TryHandleTransientPreviewEscape")!, "Nie wrócono z podglądu");
        Check(session.IsPlaying && output.Calls == callsBefore, "Powrót z podglądu naruszył odsłuch");
    }

    private sealed class ObservedOutput : AccessibleMediaController.Core.Playback.IMediaOutput
    {
        public string? LoadedItemId { get; private set; }
        public TimeSpan Position { get; private set; }
        public bool SupportsPlaybackRate => false;
        public int Calls { get; private set; }
        public void Play(MediaItem item, TimeSpan position, int volume, double rate) { LoadedItemId = item.Id; Position = position; Calls++; }
        public void Pause() { Calls++; }
        public void Stop() { LoadedItemId = null; Calls++; }
        public void Seek(TimeSpan position) { Position = position; Calls++; }
        public void SetVolume(int volume) { Calls++; }
        public void SetPlaybackRate(double rate) { Calls++; }
    }

    private static void RecordingPlayerReturn(MainWindow window)
    {
        var sessions = Sessions(window);
        var radio = sessions.SelectSession("radio")!;
        var radioOutput = new ObservedOutput();
        typeof(DemoMediaSession).GetField("_output", Private)!.SetValue(radio, radioOutput);
        var station = new MediaItem { Id = "station-origin", Title = "Stacja próby", Kind = MediaItemKind.Station, IsInLibrary = true };
        radio.AddItemsById([station]); radio.Play(station);
        Call(window, "NavigateTo", "Biblioteka");
        Call(window, "ShowPlayerView");
        var radioCalls = radioOutput.Calls;
        var local = sessions.FindSession("local")!;
        var localOutput = new ObservedOutput();
        typeof(DemoMediaSession).GetField("_output", Private)!.SetValue(local, localOutput);
        Call(window, "ExecuteCommand", CommandIds.ViewRecordedRadioFiles);
        Check(window.MediaList.Items.Count == 1, "Kontrolka: brak nagrania na liście");
        window.MediaList.SelectedIndex = 0;
        Call(window, "ActivateSelected");
        Check(local.IsPlaying && local.CurrentItem.Id == "recording-test", "Kontrolka: Enter nie otworzył nagrania");
        var localCalls = localOutput.Calls;
        Check((bool)typeof(MainWindow).GetField("_playerViewActive", Private)!.GetValue(window)!, "Kontrolka: odtwarzacz niewidoczny");
        Check((bool)Call(window, "TryHandleTransientPreviewEscape")!, "Pierwszy Escape nieobsłużony");
        Check(sessions.Current.Id == "local" && (string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)! == "Historia nagrywania",
            "Pierwszy Escape nie wrócił do historii");
        Check((bool)Call(window, "TryHandleTransientPreviewEscape")!, "Drugi Escape nieobsłużony");
        Check(sessions.Current.Id == "radio", "Drugi Escape nie wrócił do sesji źródłowej");
        Check((bool)typeof(MainWindow).GetField("_playerViewActive", Private)!.GetValue(window)!, "Drugi Escape zgubił widok odtwarzacza źródła");
        Check(radio.IsPlaying && local.IsPlaying && radioOutput.Calls == radioCalls && localOutput.Calls == localCalls,
            "Powrót zmienił odsłuch zamiast tylko widoku");
    }

    private static void ExplicitViewNavigation(MainWindow window)
    {
        Sessions(window).SelectSession("tidal");
        Call(window, "ExecuteCommand", CommandIds.ViewPodcastInbox);
        Call(window, "ExecuteCommand", CommandIds.ViewPodcastInProgress);
        Check((string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)! == "W trakcie słuchania", "Nie zmieniono widoku");
        Check(!(bool)Call(window, "TryHandleTransientPreviewEscape")!, "Inny widok nie unieważnił powrotu");
    }

    private static void ExplicitRadioNavigation(MainWindow window)
    {
        Sessions(window).SelectSession("tidal");
        Call(window, "ExecuteCommand", CommandIds.ViewPodcastInbox);
        Check(Sessions(window).Current.Id == "podcasts", "Kontrolka: nie otwarto podglądu");
        Call(window, "ExecuteCommand", CommandIds.ViewRadio);
        Check(Sessions(window).Current.Id == "radio", "Kontrolka: nie wybrano Radia");
        Check(!(bool)Call(window, "TryHandleTransientPreviewEscape")!, "Escape nadal wraca po jawnym wyborze Radia");
        Check(Sessions(window).Current.Id == "radio", "Escape zmienił świadomie wybraną sesję");
    }

    private static SessionManager Sessions(MainWindow window) => (SessionManager)typeof(MainWindow).GetField("_sessions", Private)!.GetValue(window)!;
    private static object? Call(MainWindow window, string name, params object?[] args) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, args);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
