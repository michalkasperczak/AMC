using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

internal static class NativeReaderGesturePassThroughTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-reader-keys-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var store = new ConfigurationStore(Path.Combine(root, "state.json"), Path.Combine(root, "library.db"), Path.Combine(root, "podcasts.db"));
                var state = store.LoadOrCreate();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.Messages.Enabled = true;
                state.Tidal.ClientId = state.Spotify.ClientId = string.Empty;
                state.Tidal.CachedCollectionItems.Clear(); state.Spotify.CachedCollectionItems.Clear();
                state.Podcasts.Subscriptions.Clear(); state.Podcasts.Episodes.Clear();
                state.LocalMedia.FolderSources.Clear(); state.LocalMedia.Items.Clear();
                state.Radio.Stations.Clear(); state.Radio.RecordingSchedules.Clear();
                state.Radio.AutomaticTrackRecognitionEnabled = false; state.Radio.WakeScheduledRecordings = false;
                state.WiiM.Devices.Clear();
                window = new MainWindow(state, store) { SuppressDesktopIntegrationForTests = true };
                // No Show/Activate, no IPC or global registration. Only the
                // native modifier INPUT is substituted; all handlers are real.
                var sessions = (SessionManager)Field("_sessions").GetValue(window)!;
                sessions.SelectSession("local");
                Field("_captureAnnouncements").SetValue(window, true);
                window.ScreenReaderModifierDownForTests = true;
                var failures = new List<string>();
                var checkedCases = 0;
                foreach (var session in sessions.Sessions)
                {
                    sessions.SelectSession(session.Id);
                foreach (var player in new[] { false, true })
                {
                    Field("_playerViewActive").SetValue(window, player);
                    window.PlayerPanel.Visibility = player ? Visibility.Visible : Visibility.Collapsed;
                    foreach (var key in new[] { Key.Up, Key.End, Key.Down })
                    {
                        CheckCase($"HWND player={player} key={key}", () =>
                        {
                            object[] values = [IntPtr.Zero, 0x0100, new IntPtr(KeyInterop.VirtualKeyFromKey(key)), IntPtr.Zero, false];
                            Method("WindowMessageHook").Invoke(window, values);
                            Check(!(bool)values[4], "native hook consumed the reader gesture");
                        });
                        CheckCase($"WPF player={player} key={key}", () =>
                        {
                            var input = Input(window, key);
                            Method("Window_PreviewKeyDown").Invoke(window, [window, input]);
                            Check(!input.Handled, "window handler consumed the reader gesture");
                        });
                        if (player) CheckCase($"transport key={key}", () =>
                        {
                            var handled = (bool)Method("TryHandlePlayerTransportShortcut").Invoke(window, [Input(window, key)])!;
                            Check(!handled, "player transport consumed the reader gesture");
                        });
                    }
                }
                }
                sessions.SelectSession("local");
                // Positive control: dropping all player arrows is not a fix.
                window.ScreenReaderModifierDownForTests = false;
                CheckCase("ordinary Up still changes volume", () =>
                {
                    var before = sessions.Current.Volume;
                    var handled = (bool)Method("TryHandlePlayerTransportShortcut").Invoke(window, [Input(window, Key.Up)])!;
                    Check(handled && sessions.Current.Volume > before, "ordinary player volume arrow stopped working");
                });
                if (failures.Count > 0) throw new Exception(string.Join(Environment.NewLine, failures));
                Console.WriteLine($"OK: {checkedCases} real input-handler cases; native reader gestures pass through, ordinary volume remains active. Spoken NVDA output is tested separately.");

                void CheckCase(string name, Action action)
                {
                    checkedCases++;
                    var protectReader = window.ScreenReaderModifierDownForTests == true;
                    var volumeBefore = sessions.Current.Volume;
                    var positionBefore = sessions.Current.Position;
                    Field("_capturedAnnouncement").SetValue(window, null);
                    try
                    {
                        action();
                        var frame = new DispatcherFrame();
                        Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.Background);
                        Dispatcher.PushFrame(frame);
                        if (protectReader)
                        {
                            Check(string.IsNullOrEmpty((string?)Field("_capturedAnnouncement").GetValue(window)), "reader gesture triggered an AMC announcement");
                            Check(sessions.Current.Volume == volumeBefore, "reader gesture changed volume");
                            Check(sessions.Current.Position == positionBefore, "reader gesture changed position");
                        }
                    }
                    catch (Exception e) { failures.Add(sessions.Current.Id + "/" + name + ": " + (e.InnerException ?? e).Message); }
                }
            }
            catch (Exception e) { failure = e; }
            finally
            {
                if (window is not null) window.ScreenReaderModifierDownForTests = null;
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(45))) throw new Exception("Reader input-handler test timed out.");
        if (failure is not null) throw new Exception("Native reader input routing failed.", failure);
    }

    private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, Private)!;
    private static MethodInfo Method(string name) => typeof(MainWindow).GetMethod(name, Private)!;
    private static KeyEventArgs Input(MainWindow window, Key key) => new(Keyboard.PrimaryDevice, new InputSource(), 0, key)
    { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = window.MediaList };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class InputSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
