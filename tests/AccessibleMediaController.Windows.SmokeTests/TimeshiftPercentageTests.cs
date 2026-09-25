using System.Reflection;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

internal static class TimeshiftPercentageTests
{
    internal static void Run()
    {
        var checkedPositions = 0;
        foreach (var written in new[] { 10, 35 })
        {
            using var fixture = new Fixture();
            fixture.WriteSeconds(written);
            var session = new DemoMediaSession("radio", "Próba bufora", new[] { fixture.Item }, fixture.Radio);
            for (var digit = 0; digit <= 9; digit++)
            {
                fixture.Radio.JumpToLive();
                var expected = Math.Max(0, written - 20) + Math.Min(written, 20) * digit / 10d;
                var target = Calculate(fixture.Radio, digit * 10);
                AssertNear(target.TotalSeconds, expected, "Procent aktualnego okna, zapisano=" + written);
                session.SetPosition(target);
                AssertNear(fixture.Radio.Position.TotalSeconds, expected, "Pozycja rzeczywistego bufora");
                var sample = new byte[4];
                (fixture.Stage as IWaveProvider ?? fixture.Source).Read(sample, 0, sample.Length);
                AssertNear(BitConverter.ToSingle(sample), (Math.Floor(expected) + .25) / 100d, "Próbka za torem tempa po skoku");
                checkedPositions++;
            }
            fixture.Radio.Pause();
            fixture.Radio.Seek(TimeSpan.FromSeconds(Math.Max(0, written - 20)));
            fixture.Radio.SetPlaybackRate(1.25);
            session.SetPosition(Calculate(fixture.Radio, 50));
            AssertNear(fixture.Radio.PlaybackRate, 1.25, "Skok zachowuje tempo");
            if (!(bool)typeof(RadioMediaOutput).GetField("_pauseRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Radio)!)
                throw new Exception("Skok wznowił wstrzymane wyjście");
            foreach (var invalid in new[] { -1, 101 })
                if (TryCalculate(fixture.Radio, invalid, out _)) throw new Exception("Przyjęto procent poza zakresem");
            typeof(RadioMediaOutput).GetField("_preparing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Radio, true);
            if (TryCalculate(fixture.Radio, 50, out _)) throw new Exception("Przewijanie starego bufora podczas zmiany stacji");
        }
        using (var empty = new Fixture())
            if (TryCalculate(empty.Radio, 50, out _)) throw new Exception("Pusty bufor potwierdził skok");
        using (var missing = new RadioMediaOutput(1, audible: false))
            if (TryCalculate(missing, 50, out _)) throw new Exception("Brak potoku potwierdził skok");
        var help = AccessibleMediaController.Core.Presentation.ShortcutHelpCatalog.Create(
            AccessibleMediaController.Core.Input.KeyboardProfile.CreateDefault(), new AccessibleMediaController.Core.Configuration.AppSettings());
        if (!help.Single(s => s.Id == "radio").Entries.Any(e => e.Shortcut == "0–9" && e.DisplayName.Contains("0–90% aktualnego bufora", StringComparison.Ordinal)))
            throw new Exception("Pomoc radia nie opisuje cyfr w aktualnym buforze");
        Console.WriteLine($"OK: {checkedPositions} pozycji i próbek, partial/wrap/live, tempo/pauza i odmowy oraz pomoc");
    }

    internal static void RunUi() => RunUiCore(null);
    internal static void RunNvda(string directory) => RunUiCore(Path.GetFullPath(directory));

    private static void RunUiCore(string? liveDirectory)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-percent-ui-" + Guid.NewGuid().ToString("N"));
            AccessibleMediaController.Windows.MainWindow? window = null;
            try
            {
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
                Directory.CreateDirectory(root);
                SQLitePCL.Batteries_V2.Init();
                var store = new AccessibleMediaController.Core.Configuration.ConfigurationStore(Path.Combine(root, "state.json"));
                var state = store.LoadOrCreate();
                state.Settings.Updates.CheckAutomatically = state.Settings.Updates.InstallOnExit = false;
                state.Settings.LastSessionId = "radio";
                state.Tidal.ClientId = state.Spotify.ClientId = "";
                state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                state.LocalMedia.Items.Clear(); state.LocalMedia.FolderSources.Clear();
                state.Podcasts.Episodes.Clear(); state.Podcasts.Subscriptions.Clear();
                state.Podcasts.DownloadsFolder = Path.Combine(root, "downloads");
                state.Radio.Stations.Clear(); state.Radio.RecordingSchedules.Clear(); state.Radio.WakeScheduledRecordings = false;
                state.Radio.RecordingsFolder = Path.Combine(root, "recordings"); state.WiiM.Devices.Clear();
                store.Save(state);
                window = new AccessibleMediaController.Windows.MainWindow(store.LoadOrCreate(), store) { SuppressDesktopIntegrationForTests = true };
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var type = window.GetType();
                window.ContentRendered -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), window, type.GetMethod("Window_ContentRendered", flags)!);
                using var fixture = new Fixture();
                fixture.WriteSeconds(10);
                var previous = (RadioMediaOutput)type.GetField("_radioOutput", flags)!.GetValue(window)!;
                type.GetField("_radioOutput", flags)!.SetValue(window, fixture.Radio);
                var sessions = (SessionManager)type.GetField("_sessions", flags)!.GetValue(window)!;
                var radio = sessions.SelectSession("radio")!;
                radio.Items.Clear(); radio.Items.Add(fixture.Item);
                typeof(DemoMediaSession).GetField("_output", flags)!.SetValue(radio, fixture.Radio);
                typeof(DemoMediaSession).GetField("_currentIndex", flags)!.SetValue(radio, 0);
                typeof(DemoMediaSession).GetField("_hasCurrentItem", flags)!.SetValue(radio, true);
                previous.Dispose();
                window.Show(); Pump();
                type.GetMethod("ShowPlayerView", flags)!.Invoke(window, null); Pump();
                window.PlayerPlayPauseButton.Focus(); Pump();
                if (!window.PlayerPanel.IsKeyboardFocusWithin) throw new Exception("Aparatura nie uzyskała fokusu odtwarzacza");
                SendDigit(window, System.Windows.Input.Key.D5);
                AssertNear(fixture.Radio.Position.TotalSeconds, 5, "Cyfra5 w rzeczywistym MainWindow");
                var checkedKeys = 0;
                foreach (var extra in new[] { 0, 25 })
                {
                    fixture.WriteSeconds(extra);
                    for (var digit = 0; digit <= 9; digit++)
                    {
                        fixture.Radio.JumpToLive();
                        SendDigit(window, (System.Windows.Input.Key)((int)System.Windows.Input.Key.D0 + digit));
                        var expected = extra == 0 ? digit : 15 + 2 * digit;
                        AssertNear(fixture.Radio.Position.TotalSeconds, expected, "Cyfra w MainWindow, zawinięcie=" + (extra != 0));
                        checkedKeys++;
                    }
                }
                SendDigit(window, System.Windows.Input.Key.NumPad5);
                AssertNear(fixture.Radio.Position.TotalSeconds, 25, "Cyfra klawiatury numerycznej");
                var panel = (System.Windows.Controls.Panel)window.PlayerPanel.Child;
                var edit = new System.Windows.Controls.TextBox();
                System.Windows.Automation.AutomationProperties.SetName(edit, "Pole kontrolne cyfr");
                panel.Children.Add(edit); edit.Focus(); Pump();
                if (!edit.IsKeyboardFocused) throw new Exception("Aparatura nie ustawiła fokusu edycji");
                if (SendDigit(window, System.Windows.Input.Key.D9)) throw new Exception("Pole edycji straciło cyfrę");
                AssertNear(fixture.Radio.Position.TotalSeconds, 25, "Edycja nie przewija bufora");
                panel.Children.Remove(edit); window.PlayerPlayPauseButton.Focus(); Pump();
                SendDigit(window, System.Windows.Input.Key.D9, System.Windows.Input.ModifierKeys.Control);
                AssertNear(fixture.Radio.Position.TotalSeconds, 25, "Ctrl+cyfra nie przewija bufora");
                sessions.SelectSession("radio");
                type.GetMethod("ShowPlayerView", flags)!.Invoke(window, null); Pump();
                window.PlayerPlayPauseButton.Focus(); Pump();
                Console.WriteLine($"OK: {checkedKeys} skoków cyframi w oknie, partial/wrap/live, NumPad5, edycja i Ctrl+cyfra");
                if (liveDirectory is not null)
                {
                    Directory.CreateDirectory(liveDirectory);
                    var liveState = (AccessibleMediaController.Core.Configuration.PersistedState)type.GetField("_state", flags)!.GetValue(window)!;
                    liveState.Settings.Messages.SeekMessages = liveState.Settings.Messages.PercentageSeekMessages = true;
                    liveState.Settings.Messages.PercentageSeekAnnouncement = AccessibleMediaController.Core.Configuration.PercentageSeekAnnouncementMode.PercentAndTime;
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    var started = DateTime.UtcNow;
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    timer.Tick += (_, _) =>
                    {
                        var data = System.Text.Json.JsonSerializer.Serialize(new { pid = Environment.ProcessId,
                            hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle.ToInt64(),
                            position = fixture.Radio.Position.TotalSeconds, buffered = fixture.Radio.BufferedDuration.TotalSeconds,
                            behind = fixture.Radio.BehindLive.TotalSeconds, at = DateTime.UtcNow });
                        var path = Path.Combine(liveDirectory, "state.json");
                        File.WriteAllText(path + ".tmp", data); File.Move(path + ".tmp", path, true);
                        if (File.Exists(Path.Combine(liveDirectory, "finish")) || DateTime.UtcNow - started > TimeSpan.FromMinutes(4))
                            frame.Continue = false;
                    };
                    window.Closed += (_, _) => frame.Continue = false;
                    timer.Start();
                    try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
                    finally { timer.Stop(); }
                }
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                window?.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(liveDirectory is null ? 40 : 280))) throw new Exception("Timeout prywatnego testu cyfr");
        if (failure is not null) throw new Exception("Cyfry w buforze: " + failure.Message, failure);
    }

    private static void Pump() => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    private static bool SendDigit(AccessibleMediaController.Windows.MainWindow window, System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers = System.Windows.Input.ModifierKeys.None)
    {
        var old = new byte[256];
        if (!GetKeyboardState(old)) throw new Exception("Brak stanu klawiatury w aparaturze");
        var handled = false;
        try
        {
            var keys = new byte[256];
            if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) keys[0x11] = keys[0xa2] = 0x80;
            if (!SetKeyboardState(keys)) throw new Exception("Nie ustawiono stanu wątku testu");
            var ev = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                System.Windows.PresentationSource.FromVisual(window), Environment.TickCount, key)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent, Source = System.Windows.Input.Keyboard.FocusedElement ?? window.PlayerPlayPauseButton };
            window.GetType().GetMethod("Window_PreviewKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { window, ev });
            handled = ev.Handled;
        }
        finally { SetKeyboardState(old); }
        Pump();
        return handled;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keys);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] keys);

    private static TimeSpan Calculate(RadioMediaOutput output, int percentage)
    {
        var method = typeof(RadioMediaOutput).GetMethod("TryGetBufferedSeekPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new Exception("Brak obliczenia procentowej pozycji w aktualnym buforze");
        object?[] arguments = [percentage, TimeSpan.Zero];
        if (!(bool)method.Invoke(output, arguments)!) throw new Exception("Gotowy bufor odrzucił skok procentowy");
        return (TimeSpan)arguments[1]!;
    }

    private static bool TryCalculate(RadioMediaOutput output, int percentage, out TimeSpan position)
    {
        var method = typeof(RadioMediaOutput).GetMethod("TryGetBufferedSeekPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        object?[] arguments = [percentage, TimeSpan.Zero];
        var accepted = (bool)method.Invoke(output, arguments)!;
        position = (TimeSpan)arguments[1]!;
        return accepted;
    }

    private static void AssertNear(double actual, double expected, string label)
    {
        if (Math.Abs(actual - expected) > .0002)
            throw new Exception($"{label}: oczekiwano {expected}, jest {actual}");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly object _buffer;
        private readonly MethodInfo _write;
        private int _seconds;
        public RadioMediaOutput Radio { get; } = new(1, audible: false);
        public MediaItem Item { get; } = new() { Id = "percentage-radio", Title = "Próba procentów", Source = "https://127.0.0.1:9/no-network", Kind = MediaItemKind.Station };
        public IWaveProvider Source { get; }
        public TimeshiftTempoStage? Stage { get; }

        public Fixture()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var format = WaveFormat.CreateIeeeFloatWaveFormat(8000, 1);
            var bufferType = typeof(RadioMediaOutput).GetNestedType("RadioTimeshiftWaveProvider", BindingFlags.NonPublic)!;
            _buffer = Activator.CreateInstance(bufferType, flags, null,
                new object[] { format, 1, (long)format.AverageBytesPerSecond * 20, (Action<Exception>)(e => throw e) }, null)!;
            Source = (IWaveProvider)_buffer;
            _write = bufferType.GetMethod("Write", flags)!;
            var behind = bufferType.GetProperty("BehindLive", flags)!;
            Stage = TimeshiftTempoStage.TryCreate(Source, () => (TimeSpan)behind.GetValue(_buffer)!);
            var pipelineType = typeof(RadioMediaOutput).GetNestedType("RadioPipeline", BindingFlags.NonPublic)!;
            var ctor = pipelineType.GetConstructors(flags).Single();
            var callbackType = ctor.GetParameters()[10].ParameterType;
            var callback = typeof(RadioMediaOutput).GetMethod("Pipeline_StreamTitleChanged", flags)!.CreateDelegate(callbackType, Radio);
            var samples = Stage is not null ? Stage.ToSampleProvider() : Source.ToSampleProvider();
            var pipeline = ctor.Invoke(new object?[] { Item, Source, new MemoryStream(), new ResolvedRadioSource(Item.Source!, false, false), _buffer, Stage, new VolumeSampleProvider(samples), null, new CancellationTokenSource(), null, callback });
            typeof(RadioMediaOutput).GetField("_pipeline", flags)!.SetValue(Radio, pipeline);
        }

        public void WriteSeconds(int count)
        {
            for (var n = 0; n < count; n++, _seconds++)
            {
                var bytes = new byte[Source.WaveFormat.AverageBytesPerSecond];
                for (var f = 0; f < Source.WaveFormat.SampleRate; f++)
                    BitConverter.TryWriteBytes(bytes.AsSpan(f * 4, 4), (float)((_seconds + .25) / 100d));
                _write.Invoke(_buffer, new object[] { bytes, 0, bytes.Length });
            }
        }
        public void Dispose() => Radio.Dispose();
    }
}
