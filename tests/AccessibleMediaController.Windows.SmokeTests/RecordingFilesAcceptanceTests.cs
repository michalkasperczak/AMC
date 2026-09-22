using System.Reflection;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

internal static class RecordingFilesAcceptanceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly DateTime RecordedUtc = new(2026, 9, 20, 13, 42, 0, DateTimeKind.Utc);
    private sealed class Fixture(MainWindow window, ConfigurationStore store, string root, string recordingPath)
    {
        public MainWindow Window { get; private set; } = window;
        public ConfigurationStore Store { get; private set; } = store;
        public string Root { get; } = root;
        public string RecordingPath { get; } = recordingPath;
        public bool IsClosed { get; private set; }
        public void Reopen(Action<PersistedState>? configure = null)
        {
            Window.Close(); IsClosed = true;
            Store = new ConfigurationStore(Path.Combine(Root, "state.json"));
            if (configure is not null) { var state = Store.LoadOrCreate(); configure(state); Store.Save(state); }
            Window = CreateWindow(Store); IsClosed = false;
        }
    }

    internal static void Run()
    {
        var tests = new (string Name, Action<Fixture> Test)[] {
            ("wiersz nagrania zawiera czas i folder na końcu", LibraryRowIncludesTime),
            ("obcy plik o tej samej nazwie nie przejmuje historii", SameNameDoesNotProveMove),
            ("obserwator usuwa plik przez kolejkę UI", WatcherUsesUiThread),
            ("prawdziwy błąd nagrania nie znika przy deduplikacji", FailedRecordingSurvivesLibraryRow),
            ("usunięcie od razu zmienia dostępność widocznego wiersza", DeleteUpdatesVisibleRow),
            ("zmiana nazwy i usunięcie są trwałe po restarcie", RenameAndDeleteSurviveRestart),
            ("powrót z Eksploratora sprawdza nieobserwowany plik", UnwatchedDeleteOnActivation),
            ("ponowne otwarcie historii sprawdza plik bez obserwatora", UnwatchedDeleteOnReopen),
            ("niedostępny folder nie usuwa nagrania bez historii", UnavailableFolderRetainsRecording),
            ("obserwator rozpoznaje równoważną ścieżkę starej historii", WatcherNormalizesLegacyPath),
            ("obserwator nie opuszcza podglądu ani miejsca powrotu", WatcherRespectsPreviewScope),
            ("przerwane nagranie pozostaje opisane jako przerwane", InterruptedRecordingRetainsOutcome),
        };
        var failures = new List<string>();
        foreach (var (name, test) in tests)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                var root = Path.Combine(Path.GetTempPath(), "amc-recording-acceptance-" + Guid.NewGuid().ToString("N"));
                MainWindow? window = null;
                Fixture? fixture = null;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                    var recordings = Path.Combine(root, "Nagrania próby"); Directory.CreateDirectory(recordings);
                    var path = Path.Combine(recordings, "audycja.wav");
                    var format = new NAudio.Wave.WaveFormat(8000, 16, 1);
                    using (var writer = new NAudio.Wave.WaveFileWriter(path, format)) writer.Write(new byte[format.AverageBytesPerSecond * 10]);
                    var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                    var state = store.LoadOrCreate();
                    state.Settings.Updates.CheckAutomatically = state.Settings.Updates.InstallOnExit = false;
                    state.Settings.LastSessionId = "local";
                    state.Tidal.ClientId = state.Spotify.ClientId = "";
                    state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                    state.Podcasts.Subscriptions.Clear(); state.Podcasts.Episodes.Clear();
                    state.Podcasts.DownloadsFolder = Path.Combine(root, "downloads");
                    state.LocalMedia.Items.Clear(); state.LocalMedia.FolderSources.Clear();
                    state.WiiM.Devices.Clear(); state.Radio.Stations.Clear(); state.Radio.RecordingSchedules.Clear();
                    state.Radio.RecordingsFolder = recordings; state.Radio.WakeScheduledRecordings = false;
                    state.LocalMedia.Items.Add(new LocalMediaItemSettings {
                        Id = "recording-test", Title = "Audycja próby", Path = path, IsInLibrary = true,
                        DurationTicks = TimeSpan.FromSeconds(10).Ticks, IsRadioRecording = true,
                        RadioRecordingCompletedUtcTicks = RecordedUtc.Ticks
                    });
                    state.Radio.RecordingHistory.Add(new RadioRecordingHistorySettings {
                        Id = "history-test", StationId = "station-test", StationName = "Stacja próby", Path = path,
                        Outcome = RadioRecordingOutcome.Completed, SavedFileCount = 1,
                        StartedUtcTicks = RecordedUtc.AddSeconds(-10).Ticks, FinishedUtcTicks = RecordedUtc.Ticks
                    });
                    store.Save(state);
                    window = CreateWindow(store);
                    fixture = new Fixture(window, store, root, path);
                    test(fixture);
                }
                catch (Exception e) { failure = e; }
                finally
                {
                    if (fixture is null) window?.Close();
                    else if (!fixture.IsClosed) fixture.Window.Close();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                    try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(60))) failure = new Exception("Timeout");
            if (failure is null) Console.WriteLine("OK: " + name);
            else { failures.Add(name); Console.Error.WriteLine(name + ": " + failure); }
        }
        Console.WriteLine($"NAGRANIA: {tests.Length - failures.Count} OK / {failures.Count} BLAD / razem {tests.Length}");
        if (failures.Count != 0) throw new Exception("Nagrania: " + string.Join("; ", failures));
    }

    private static void InterruptedRecordingRetainsOutcome(Fixture fixture)
    {
        fixture.Reopen(state => state.Radio.RecordingHistory.Single().Outcome = RadioRecordingOutcome.Interrupted);
        Call(fixture.Window, "ShowRecordedRadioFiles");
        Check(fixture.Window.MediaList.Items.Cast<object>().Any(row => row.ToString()!.StartsWith("Przerwane", StringComparison.Ordinal)),
            "Biblioteczny plik ukrył prawdziwy wynik przerwanego nagrania");
        Check(fixture.Window.MediaList.Items.Count == 1, "Przerwane nagranie ma drugi, mylący wiersz udanego pliku");
    }

    private static void WatcherRespectsPreviewScope(Fixture fixture)
    {
        var window = fixture.Window;
        var sessions = (AccessibleMediaController.Core.Sessions.SessionManager)typeof(MainWindow).GetField("_sessions", Private)!.GetValue(window)!;
        foreach (var id in sessions.Sessions.Select(session => session.Id).ToArray())
        {
            sessions.SelectSession(id);
            Call(window, "NavigateTo", "Ulubione");
            window.FilterBox.Text = "filtr próby";
            Call(window, "ShowRecordedRadioFiles");
            Task.Run(() => Call(window, "LocalSourceWatcher_Changed", window,
                new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(fixture.RecordingPath)!, Path.GetFileName(fixture.RecordingPath))))
                .GetAwaiter().GetResult();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Check(sessions.Current.Id == "local" && (string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)! == "Historia nagrywania",
                "Obserwator opuścił podgląd z " + id);
            Check((bool)Call(window, "TryHandleTransientPreviewEscape")!, "Brak powrotu do " + id);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            Check(sessions.Current.Id == id && window.FilterBox.Text == "filtr próby", "Obserwator zgubił cel powrotu do " + id);
        }
    }

    private static void WatcherNormalizesLegacyPath(Fixture fixture)
    {
        var folder = Path.GetDirectoryName(fixture.RecordingPath)!;
        var alias = Path.Combine(folder, ".", Path.GetFileName(fixture.RecordingPath));
        fixture.Reopen(state => {
            state.LocalMedia.Items.Clear();
            state.Radio.RecordingHistory.Single().Path = alias;
        });
        Call(fixture.Window, "ShowRecordedRadioFiles");
        var before = fixture.Window.MediaList.Items.Cast<object>().Single();
        Check(before.ToString()!.StartsWith("Nagrane", StringComparison.Ordinal), "Kontrolka istniejącego pliku historii");
        Check(fixture.Store.LoadOrCreate().Radio.RecordingHistory.Single().Path == alias, "Kontrolka starego formatu ścieżki");
        File.Delete(fixture.RecordingPath);
        Task.Run(() => Call(fixture.Window, "LocalSourceWatcher_Changed", fixture.Window,
            new FileSystemEventArgs(WatcherChangeTypes.Deleted, folder, Path.GetFileName(fixture.RecordingPath))))
            .GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var row = fixture.Window.MediaList.Items.Cast<object>().Single();
        Check(row.ToString()!.StartsWith("Brak pliku nagrania", StringComparison.Ordinal), "Równoważna ścieżka zachowała starą dostępność: " + row);
        var probe = (AccessibleMediaController.Core.Presentation.RecordingPathProbe)typeof(MainWindow)
            .GetField("_recordingPathProbe", Private)!.GetValue(fixture.Window)!;
        Check(probe.IsMissing(alias), "Obserwator zapisał brak pod innym kluczem cache");
        Check(row.ToString()!.EndsWith("folder Nagrania próby", StringComparison.Ordinal), "Alias ścieżki zniekształcił nazwę folderu: " + row);
    }

    private static void UnavailableFolderRetainsRecording(Fixture fixture)
    {
        var state = (PersistedState)typeof(MainWindow).GetField("_state", Private)!.GetValue(fixture.Window)!;
        state.Radio.RecordingHistory.Clear(); // Starszy wpis oznaczony przez backfill, bez historii.
        Call(fixture.Window, "ShowRecordedRadioFiles");
        Check(fixture.Window.MediaList.Items.Count == 1, "Kontrolka: nagranie bez historii jest widoczne");
        var folder = Path.GetDirectoryName(fixture.RecordingPath)!;
        var away = folder + "-odłączony";
        Directory.Move(folder, away); // Niedostępny katalog, a nie potwierdzony Delete pliku.
        Call(fixture.Window, "Window_Activated", fixture.Window, EventArgs.Empty);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        Check(fixture.Window.MediaList.Items.Count == 1, "Niedostępny folder ukrył nagranie bez wpisu historii");
        var row = fixture.Window.MediaList.Items.Cast<object>().Single();
        Check(row.ToString()!.StartsWith("Plik nagrania niedostępny", StringComparison.Ordinal), "Brak rozróżnienia niedostępnego folderu: " + row);
        var item = (AccessibleMediaController.Core.Sessions.MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!;
        Check(!item.IsAvailable, "Niedostępne nagranie udaje gotowe do odtworzenia");
        fixture.Reopen();
        Call(fixture.Window, "ShowRecordedRadioFiles");
        Check(fixture.Window.MediaList.Items.Count == 1, "Restart zgubił niedostępne nagranie");
        Directory.Move(away, folder);
        Call(fixture.Window, "Window_Activated", fixture.Window, EventArgs.Empty);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        row = fixture.Window.MediaList.Items.Cast<object>().Single();
        item = (AccessibleMediaController.Core.Sessions.MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!;
        Check(item.IsAvailable && row.ToString()!.Contains("Audycja próby", StringComparison.Ordinal), "Powrót folderu nie przywrócił nagrania");
        Check(fixture.Store.LoadOrCreate().Radio.RecordingHistory.Count == 0, "Podgląd utworzył nieistniejący zapis historyczny");
    }

    private static void UnwatchedDeleteOnReopen(Fixture fixture)
    {
        Call(fixture.Window, "ShowRecordedRadioFiles");
        Call(fixture.Window, "ExecuteCommand", AccessibleMediaController.Core.Commands.CommandIds.ViewRadio);
        File.Delete(fixture.RecordingPath);
        Call(fixture.Window, "ShowRecordedRadioFiles");
        var label = fixture.Window.MediaList.Items.Cast<object>().Single().ToString()!;
        Check(label.StartsWith("Brak pliku nagrania", StringComparison.Ordinal), "Ponowne otwarcie użyło starej obserwacji: " + label);
    }

    private static void UnwatchedDeleteOnActivation(Fixture fixture)
    {
        Call(fixture.Window, "ShowRecordedRadioFiles");
        var probe = (AccessibleMediaController.Core.Presentation.RecordingPathProbe)typeof(MainWindow)
            .GetField("_recordingPathProbe", Private)!.GetValue(fixture.Window)!;
        Check(probe.Exists(fixture.RecordingPath), "Kontrolka: cache istniejącego nagrania");
        File.Delete(fixture.RecordingPath); // brak FolderSource i brak callbacku FileSystemWatcher
        Call(fixture.Window, "Window_Activated", fixture.Window, EventArgs.Empty);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var label = fixture.Window.MediaList.Items.Cast<object>().Single().ToString()!;
        Check(label.StartsWith("Brak pliku nagrania", StringComparison.Ordinal), "Po powrocie nadal użyto starej obserwacji: " + label);
    }

    private static MainWindow CreateWindow(ConfigurationStore store)
    {
        var window = new MainWindow(store.LoadOrCreate(), store) { SuppressDesktopIntegrationForTests = true };
        typeof(MainWindow).GetField("_applicationUpdateStartOverride", Private)!.SetValue(window,
            (Func<bool, bool>)(_ => throw new Exception("Test forbids installation")));
        return window;
    }

    private static void RenameAndDeleteSurviveRestart(Fixture fixture)
    {
        var destination = Path.Combine(fixture.Root, "Archiwum próby"); Directory.CreateDirectory(destination);
        var movedPath = Path.Combine(destination, "przeniesiona-audycja.wav");
        File.Move(fixture.RecordingPath, movedPath);
        Task.Run(() => Call(fixture.Window, "LocalSourceWatcher_Renamed", fixture.Window,
            new RenamedEventArgs(WatcherChangeTypes.Renamed, fixture.Root,
                Path.GetRelativePath(fixture.Root, movedPath), Path.GetRelativePath(fixture.Root, fixture.RecordingPath))))
            .GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        fixture.Reopen();
        var loaded = fixture.Store.LoadOrCreate();
        Check(loaded.Radio.RecordingHistory.Single().Path == movedPath, "Historia nie zachowała nowej ścieżki");
        Check(loaded.LocalMedia.Items.Single().Path == movedPath, "Biblioteka nie zachowała nowej ścieżki");
        Call(fixture.Window, "ShowRecordedRadioFiles");
        var rows = fixture.Window.MediaList.Items.Cast<object>().ToArray();
        Check(rows.Length == 1 && rows[0].ToString()!.EndsWith("folder Archiwum próby", StringComparison.Ordinal),
            "Restart zgubił nagranie lub pozostawił stary folder");
        File.Delete(movedPath);
        Task.Run(() => Call(fixture.Window, "LocalSourceWatcher_Changed", fixture.Window,
            new FileSystemEventArgs(WatcherChangeTypes.Deleted, destination, Path.GetFileName(movedPath))))
            .GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        fixture.Reopen();
        Call(fixture.Window, "ShowRecordedRadioFiles");
        rows = fixture.Window.MediaList.Items.Cast<object>().ToArray();
        Check(rows.Length == 1 && rows[0].ToString()!.StartsWith("Brak pliku nagrania", StringComparison.Ordinal),
            "Restart przywrócił usunięte nagranie jako dostępne");
        Check(!File.Exists(movedPath), "Restart nie może odtwarzać celowo usuniętego pliku");
    }

    private static void DeleteUpdatesVisibleRow(Fixture fixture)
    {
        Call(fixture.Window, "ShowRecordedRadioFiles");
        Check(fixture.Window.MediaList.Items.Count == 1, "Kontrolka: widoczne nagranie");
        File.Delete(fixture.RecordingPath);
        Task.Run(() => Call(fixture.Window, "LocalSourceWatcher_Changed", fixture.Window,
            new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(fixture.RecordingPath)!,
                Path.GetFileName(fixture.RecordingPath)))).GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var row = fixture.Window.MediaList.Items.Cast<object>().Single();
        var label = row.ToString()!;
        Check(label.StartsWith("Brak pliku nagrania", StringComparison.Ordinal), "Usunięty plik nadal wygląda na gotowy: " + label);
        Check(label.EndsWith("folder Nagrania próby", StringComparison.Ordinal), "Folder musi pozostać na końcu");
        var item = (AccessibleMediaController.Core.Sessions.MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!;
        Check(!item.IsAvailable, "Wiersz nadal deklaruje dostępność pliku");
    }

    private static void FailedRecordingSurvivesLibraryRow(Fixture fixture)
    {
        var state = (PersistedState)typeof(MainWindow).GetField("_state", Private)!.GetValue(fixture.Window)!;
        state.Radio.RecordingHistory.Add(new RadioRecordingHistorySettings {
            Id="failed-recording-test", StationName="Nieudana próba", Path=fixture.RecordingPath,
            Outcome=RadioRecordingOutcome.Failed, SavedFileCount=0, Reason="Błąd zapisu próby",
            StartedUtcTicks=RecordedUtc.Ticks, FinishedUtcTicks=RecordedUtc.AddSeconds(10).Ticks
        });
        Call(fixture.Window, "ShowRecordedRadioFiles");
        var labels = fixture.Window.MediaList.Items.Cast<object>().Select(row => row.ToString()!).ToArray();
        Check(labels.Any(label => label.StartsWith("Nieudane", StringComparison.Ordinal) && label.Contains("Nieudana próba", StringComparison.Ordinal)),
            "Wiersz biblioteki usunął z widoku prawdziwy nieudany wpis: " + string.Join(" | ", labels));
        Check(labels.Length == 2, "Powinny pozostać jeden plik i jeden błąd; nie trzeci duplikat udanego nagrania");
    }

    private static void WatcherUsesUiThread(Fixture fixture)
    {
        var probe = (AccessibleMediaController.Core.Presentation.RecordingPathProbe)typeof(MainWindow)
            .GetField("_recordingPathProbe", Private)!.GetValue(fixture.Window)!;
        Check(probe.Exists(fixture.RecordingPath), "Kontrolka: plik istnieje");
        File.Delete(fixture.RecordingPath);
        Task.Run(() => Call(fixture.Window, "LocalSourceWatcher_Changed", fixture.Window,
            new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(fixture.RecordingPath)!,
                Path.GetFileName(fixture.RecordingPath)))).GetAwaiter().GetResult();
        Check(probe.Exists(fixture.RecordingPath), "Wątek obserwatora mutował cache współdzielony z UI");
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        Check(probe.IsMissing(fixture.RecordingPath), "Kolejka UI nie zaznaczyła usunięcia");
    }

    private static void SameNameDoesNotProveMove(Fixture fixture)
    {
        File.Delete(fixture.RecordingPath);
        var otherFolder = Path.Combine(fixture.Root, "Inny folder"); Directory.CreateDirectory(otherFolder);
        var other = Path.Combine(otherFolder, Path.GetFileName(fixture.RecordingPath));
        File.WriteAllBytes(other, [1, 2, 3, 4]);
        // Rzeczywiste pliki i produkcyjny callback obserwatora. Samo Created
        // nie zawiera informacji, skąd pochodzi nowy plik.
        Task.Run(() => Call(fixture.Window, "LocalSourceWatcher_Changed", fixture.Window,
            new FileSystemEventArgs(WatcherChangeTypes.Created, otherFolder, Path.GetFileName(other)))).GetAwaiter().GetResult();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var state = (PersistedState)typeof(MainWindow).GetField("_state", Private)!.GetValue(fixture.Window)!;
        Check(state.Radio.RecordingHistory.Single().Path == fixture.RecordingPath,
            "Sama nazwa obcego pliku zmieniła powiązanie historii");
    }

    private static void LibraryRowIncludesTime(Fixture fixture)
    {
        Call(fixture.Window, "ShowRecordedRadioFiles");
        var rows = fixture.Window.MediaList.Items.Cast<object>().Select(row => row.ToString()!).ToArray();
        Check(rows.Length == 1, "Kontrolka: historia powinna mieć jeden deduplikowany wpis");
        var label = rows[0];
        Check(label.Contains("Audycja próby", StringComparison.Ordinal), "Brak nazwy nagrania: " + label);
        Check(label.Contains(RecordedUtc.ToLocalTime().ToString("HH:mm"), StringComparison.Ordinal), "Brak czasu nagrania: " + label);
        Check(label.EndsWith("folder Nagrania próby", StringComparison.Ordinal), "Folder nie jest na końcu: " + label);
    }

    private static object? Call(MainWindow window, string name, params object?[] args) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, args);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
