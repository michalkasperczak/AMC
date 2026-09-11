using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Reflection;
using System.Runtime.CompilerServices;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

internal static class NvdaBridgeSmokeTests
{
    internal static void Run()
    {
        // Earlier WPF tests can leave a DispatcherSynchronizationContext installed.
        // Transport tests must not capture it while this STA waits for completion.
        Task.Run(RunAsync).GetAwaiter().GetResult();
        Exception? failure = null;
        var uiThread = new Thread(() =>
        {
            try { TestExpiredDispatcherWork(); TestUiHandoff(); TestCurrentItemTarget(); TestPresetCreationControls(); }
            catch (Exception exception) { failure = exception; }
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        if (!uiThread.Join(TimeSpan.FromSeconds(15))) throw new TimeoutException("NVDA: test przekazania fokusa przekroczył limit.");
        if (failure is not null) throw new InvalidOperationException("NVDA: test izolowanego interfejsu", failure);
        TestPlaybackContext();
        Console.WriteLine("NVDA interaction smoke: OK (playback context, current item, asynchronous origin, single/expired UI handoff)");
    }

    private static void TestExpiredDispatcherWork()
    {
        for (var slot = 1; slot <= 12; slot++)
            Check(NvdaCommands.Resolve($"preset{slot}") == CommandIds.RadioPreset(slot), "All twelve slots use canonical preset routing");
        foreach (var invalid in new[] { "preset0", "preset13", "preset01", "preset-1", "preset1;delete" })
            Check(!NvdaCommands.IsAllowed(invalid), "Preset allow-list is exact and bounded");
        foreach (var kind in new[] { "folder", "localAlbum", "amcPlaylist", "album", "podcast", "artist", "playlist" })
            Check(NvdaPresetPolicy.NeedsBrowser(kind), "Container presets open a visible browser");
        foreach (var kind in new[] { "track", "station", "episode", "wiimNativePreset" })
            Check(!NvdaPresetPolicy.NeedsBrowser(kind), "Playable presets do not steal focus");
        TestDispatcherDeadline();
    }

    private static void TestPresetCreationControls()
    {
        var choices = Enumerable.Range(1, 12)
            .Select(slot => new RadioPresetChoice(slot, slot.ToString(), slot.ToString(), null, null, null)).ToArray();
        var creates = 0;
        var dialog = new RadioPresetsWindow(choices, "new", createPreset: owner =>
        {
            creates++;
            Check(owner.SelectedSlot is null, "Create cannot activate a preset");
            choices[9] = choices[9] with { StationId = "new", StationName = "Nowa stacja" };
        }, reloadChoices: () => choices, targetTitle: "Nowa stacja");
        var button = (Button)dialog.FindName("CreatePresetButton");
        Check(System.Windows.Automation.AutomationProperties.GetName(button) == "Utwórz nowy preset", "Intentional create label");
        button.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        var list = (ListBox)dialog.FindName("PresetList");
        Check(creates == 1 && list.SelectedIndex == 9 && dialog.SelectedSlot is null, "Create refreshes list, selects assigned slot, does not close or play");
        Check(list.SelectedItem.ToString()!.Contains("Nowa stacja"), "New preset has a human-readable label");
        var cancel = new RadioPresetsWindow(choices, "new", createPreset: _ => { }, reloadChoices: () => choices);
        var cancelList = (ListBox)cancel.FindName("PresetList");
        cancelList.SelectedIndex = 3;
        ((Button)cancel.FindName("CreatePresetButton")).RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        Check(cancelList.SelectedIndex == 3 && cancel.SelectedSlot is null, "Cancelled assignment retains list selection");
        CheckButtonKeysNotIntercepted(dialog, button);
        var assignment = new RadioPresetAssignmentWindow("Nowa stacja", "new", choices, 1, 1);
        CheckButtonKeysNotIntercepted(assignment, new Button());
        var native = new AccessibleMediaController.Core.Devices.WiiM.WiiMPresetInformation(4, "Radio testowe", "radio", null);
        var assignments = 0;
        var wiim = new WiiMDevicePresetsWindow("Test bez urządzenia", [native],
            assignShortcut: (owner, slot) =>
            {
                Check(slot == 4 && owner.SelectedPresetNumber is null, "WiiM assignment does not play");
                assignments++;
            }, reloadShortcuts: () => new Dictionary<int, int> { [4] = 10 });
        var wiimButton = (Button)wiim.FindName("AssignShortcutButton");
        CheckButtonKeysNotIntercepted(wiim, wiimButton);
        wiimButton.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        var wiimList = (ListBox)wiim.FindName("PresetList");
        Check(assignments == 1 && wiimList.SelectedIndex == 3
            && wiimList.SelectedItem.ToString()!.Contains("Ctrl+Shift+0")
            && wiim.SelectedPresetNumber is null, "WiiM assignment refreshes local shortcut and keeps list open");
        var membership = new PlaylistWindow("local", "Pliki", [], [new MediaItem { Id = "test", Title = "Test" }]);
        CheckButtonKeysNotIntercepted(membership, new Button());
        wiim.Close();
        membership.Close();
        assignment.Close();
        dialog.Close();
        cancel.Close();
    }

    private static void CheckButtonKeysNotIntercepted(System.Windows.Window window, Button button)
    {
        var preview = window.GetType().GetMethod("Window_PreviewKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var key in new[] { System.Windows.Input.Key.Enter, System.Windows.Input.Key.Space })
        {
            var input = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                new TestInputSource(), 0, key)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
                Source = button
            };
            preview.Invoke(window, [window, input]);
            Check(!input.Handled, "List handler must not swallow button Enter/Space");
        }
    }

    private sealed class TestInputSource : System.Windows.PresentationSource
    {
        public override System.Windows.Media.Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void TestDispatcherDeadline()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var calls = 0;
        using var deadline = new CancellationTokenSource(30);
        var operation = dispatcher.InvokeAsync(() => calls++, DispatcherPriority.Input, deadline.Token);
        // Simulate a busy UI without pumping it until the request expires.
        Check(deadline.Token.WaitHandle.WaitOne(1000), "Dispatcher deadline expired");
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
        Check(calls == 0 && operation.Task.IsCanceled, "Expired request cannot run after the UI recovers");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private static void Pump()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
    }

    private static void TestUiHandoff()
    {
        var handoff = new NvdaUiHandoff();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var calls = 0;
        Check(handoff.TrySchedule(dispatcher, () => true, () =>
        {
            calls++;
            Check(!handoff.TrySchedule(dispatcher, () => true, () => calls++), "No reentrant dialog");
        }), "UI request accepted");
        Check(calls == 0 && handoff.Pending, "UI request returns before a modal action");
        Check(!handoff.TrySchedule(dispatcher, () => true, () => calls++), "One pending window only");
        Pump();
        Check(calls == 1 && !handoff.Pending, "UI runs exactly once, releases guard");
        handoff.TrySchedule(dispatcher, () => false, () => calls++);
        Pump();
        Check(calls == 1 && !handoff.Pending, "Changed foreground/session cancels the handoff");
        handoff.TrySchedule(dispatcher, () => true, () => calls++, TimeSpan.Zero);
        Pump();
        Check(calls == 1 && !handoff.Pending, "Stale UI request expires without opening a window");
    }

    private static void TestCurrentItemTarget()
    {
        // Inspect the actual property routing without constructing the application,
        // loading user data, creating media outputs, or registering its real pipe.
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var sessions = new SessionManager(new AppSettings());
        sessions.AddOrUpdateTransientSession("local", "Pliki", [new MediaItem { Id = "playing", Title = "Otwarty plik" }], null, 4);
        sessions.SelectSession("local");
        typeof(MainWindow).GetField("_sessions", fields)!.SetValue(window, sessions);
        var rowType = typeof(MainWindow).GetNestedType("MediaItemRow", BindingFlags.NonPublic)!;
        var wrongItem = new MediaItem { Id = "hidden-selection", Title = "Niewybrany do odtwarzania" };
        var row = Activator.CreateInstance(rowType, fields, null,
            [wrongItem, wrongItem.Title, wrongItem.Title, null, null, "X:\\fictional-folder", null, null, null, null], null)!;
        var list = new ListBox { SelectionMode = SelectionMode.Extended };
        list.Items.Add(row);
        list.SelectedIndex = 0;
        typeof(MainWindow).GetField("MediaList", fields)!.SetValue(window, list);
        Check(window.ActionItem == wrongItem, "Ordinary list actions still use selection");
        typeof(MainWindow).GetField("_nvdaCurrentItemTarget", fields)!.SetValue(window, true);
        Check(window.ActionItem == sessions.Current.CurrentItem, "Remote action targets current playback");
        Check(window.ActionItems.Count == 1 && window.ActionItems[0] == sessions.Current.CurrentItem,
            "Remote action cannot mutate a hidden multi-selection");
        var folder = typeof(MainWindow).GetMethod("TryResolveSelectedFolderContents", fields)!;
        Check(!(bool)folder.Invoke(window, [null])!, "Remote command cannot expand a hidden folder");
        Check(!NvdaBackgroundScope.IsActive, "No leaked remote state");
    }

    private static void TestPlaybackContext()
    {
        var a = new MediaItem { Id = "a", Title = "Pierwsze", Kind = MediaItemKind.Station };
        var unrelated = new MediaItem { Id = "x", Title = "Spoza ulubionych", Kind = MediaItemKind.Station };
        var b = new MediaItem { Id = "b", Title = "Drugie", Kind = MediaItemKind.Station };
        var session = new DemoMediaSession("radio", "Radio", [a, unrelated, b]);
        session.SetPlaybackContext([a.Id, b.Id]);
        session.Play(a);
        session.TogglePlayback();
        Check(!session.IsPlaying, "Paused before relative navigation");
        Check(session.PlayRelative(1) && session.IsPlaying && session.CurrentItem == b,
            "Next from pause starts next favorite, not the adjacent catalog station");
        Check(NvdaPlaybackContext.Describe(session, "Ulubione") == "Ulubione, Radio, 2 z 2.",
            "Playback context has an intentional label and position");
        Check(!session.PlayRelative(1) && session.CurrentItem == b, "End does not leak into the catalog");
        Check(session.PlayRelative(-1) && session.CurrentItem == a, "Previous preserves source context");
        session.SetPlaybackContext([b.Id, a.Id]);
        Check(NvdaPlaybackContext.Describe(session, "Presety") == "Presety, Radio, 2 z 2.", "Presets report their own context");
    }

    private static async Task TestAsyncOrigin()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Later() { await release.Task; return NvdaBackgroundScope.IsActive; }
        Task<bool> delayed;
        using (new NvdaBackgroundScope())
        {
            Check(NvdaBackgroundScope.IsActive, "Synchronous background scope");
            delayed = Later();
        }
        Check(!NvdaBackgroundScope.IsActive, "Normal UI work is not marked as remote");
        release.SetResult();
        Check(await delayed, "Late provider completion preserves background origin");
        Check(!NvdaBackgroundScope.IsActive, "Await does not leak background origin to caller");
    }

    private static async Task RunAsync()
    {
        await TestAsyncOrigin();
        Check(NvdaCommands.Resolve("recordPause") == CommandIds.ToggleRadioRecordingPause, "Recording pause is not playback pause");
        Check(NvdaCommands.Resolve("showAudioOutput") == CommandIds.SelectAudioOutput, "Use canonical output-device selector");
        Check(NvdaCommands.OpensWindow("showSessions") && !NvdaCommands.OpensWindow("sessionNext"), "Explicit UI separated from background session control");
        Check(NvdaCommands.TargetsCurrentItem("favorite") && NvdaCommands.TargetsCurrentItem("queue"), "Collection actions target current playback");
        Check(NvdaCommands.Resolve("playPause") == CommandIds.PlayPause, "NVDA uses canonical pause command");
        Check(NvdaCommands.Resolve("sessionNext") == CommandIds.SessionNext, "NVDA uses canonical session command");
        foreach (var value in new[] { "transport.playPause", "delete", "record", "shell", "", "status\nnext" })
            Check(!NvdaCommands.IsAllowed(value), "Reject arbitrary commands");
        foreach (var wire in new[] { "{}\n", "[]\n", "{\"version\":\"1\",\"command\":\"next\"}\n",
                     "{\"version\":2,\"command\":\"next\"}\n", "{\"version\":1,\"command\":\"delete\"}\n",
                     "{\"version\":1,\"command\":\"next\"}", new string('a', 513) })
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(wire));
            Check(await NvdaCommandServer.ReadRequestAsync(input, CancellationToken.None) is null, "Reject invalid wire request");
        }
        var calls = new List<string>();
        var name = "AMC.NVDA.test." + Guid.NewGuid().ToString("N");
        using var server = new NvdaCommandServer((command, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            calls.Add(command);
            return Task.FromResult(new NvdaReply(true, "Żółć, głośność 35%"));
        }, name);
        var response = await SendAsync(name, "{\"version\":1,\"command\":\"status\"}\n");
        Check(response.GetProperty("message").GetString() == "Żółć, głośność 35%", "Unicode reply preserved");
        response = await SendAsync(name, "{\"version\":1,\"command\":\"delete\"}\n");
        Check(!response.GetProperty("ok").GetBoolean(), "Forbidden operation never dispatched");
        await SendAsync(name, "{\"version\":1,\"command\":\"playPause\"}\n{\"version\":1,\"command\":\"playPause\"}\n");
        Check(calls.SequenceEqual(new[] { "status", "playPause" }), "One connection cannot toggle twice");
        // A silent client must not keep the only listener forever.
        using (var stalled = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await stalled.ConnectAsync(3000);
            using var timeout = new CancellationTokenSource(4000);
            var read = await stalled.ReadAsync(new byte[1], timeout.Token);
            Check(read == 0, "Slow client disconnected by deadline");
        }
        await SendAsync(name, "{\"version\":1,\"command\":\"next\"}\n");
        Check(calls.Count == 3, "Listener recovers after stalled client");
        server.Dispose();
        await server.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Console.WriteLine("NVDA bridge smoke: OK (protocol, allow-list, Unicode, no duplicate execution, timeout, shutdown)");
    }

    private static async Task<JsonElement> SendAsync(string name, string wire)
    {
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(4000);
        await pipe.ConnectAsync(timeout.Token);
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(wire), timeout.Token);
        using var reader = new StreamReader(pipe, Encoding.UTF8);
        var line = await reader.ReadLineAsync(timeout.Token);
        return JsonDocument.Parse(line!).RootElement.Clone();
    }

    // Test-only host for Python <-> .NET transport; no real AMC state/audio involved.
    internal static void RunInteropHost(string pipeName)
    {
        using var server = new NvdaCommandServer((command, _) =>
            Task.FromResult(new NvdaReply(true, "Test połączenia: " + command)), pipeName);
        Console.WriteLine("READY");
        Console.ReadLine();
    }
}
