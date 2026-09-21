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
        TestNowPlaying();
        TestPluginGesturesMatchBridge();
        Console.WriteLine("NVDA interaction smoke: OK (playback context, current item, asynchronous origin, single/expired UI handoff)");
    }

    private static void TestPluginGesturesMatchBridge()
    {
        // Wtyczka NVDA i mostek AMC to dwa zrodla prawdy w dwoch jezykach.
        // Gdy wtyczka wysyla nazwe, ktorej mostek nie zna, uzytkownik slyszy
        // CISZE - najgorszy mozliwy objaw przy czytniku ekranu.
        var pluginFile = FindPluginFile();
        if (pluginFile is null)
        {
            Console.WriteLine("NVDA: pominieto porownanie gestow - brak zrodla wtyczki obok testow.");
            return;
        }

        var source = File.ReadAllText(pluginFile);
        var sent = System.Text.RegularExpressions.Regex
            .Matches(source, @"self\._send\(""(?<name>[A-Za-z0-9_]+)""\)")
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Check(sent.Length > 0, "Plugin source exposes commands");
        foreach (var command in sent)
        {
            Check(
                NvdaCommands.IsAllowed(command),
                $"Bridge understands plugin command '{command}'");
        }

        Check(
            sent.Contains("refreshPodcastLibrary", StringComparer.Ordinal),
            "Plugin offers the global podcast refresh gesture");
        Check(
            source.Contains("kb:control+windows+f5", StringComparison.Ordinal),
            "Global podcast refresh keeps its documented gesture");

        Check(FindAddonFile("appModules", "accessiblemediacontroller.py") is null,
            "AMC must not install an app module that overrides native reader gestures");
        var gestures = System.Text.RegularExpressions.Regex.Matches(source, @"kb:[^""\r\n]+")
            .Select(match => match.Value).ToArray();
        Check(gestures.Length > 0 && gestures.All(gesture =>
                gesture.StartsWith("kb:control+windows+", StringComparison.OrdinalIgnoreCase)),
            "All default addon gestures stay within Ctrl+Windows");

    }

    private static string? FindPluginFile() =>
        FindAddonFile("globalPlugins", "amcController", "__init__.py");

    private static string? FindAddonFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "nvda-addon", "addon" }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        return null;
    }

    private static void TestExpiredDispatcherWork()
    {
        TestCommandsAllowedWithOpenWindow();
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

    /// <summary>
    /// ZGLOSZENIE Michala 17.09.2026: przy otwartym oknie harmonogramu skrot glosnosci
    /// (Ctrl+Windows+strzalka w gore/w dol) odpowiadal "AMC jest zajety. Zamknij otwarte
    /// okno dialogowe" - brzmialo jak zawieszenie programu.
    ///
    /// Glosnosc, wyciszenie, pauza, przewijanie i pytania o stan nie ruszaja interfejsem,
    /// wiec MUSZA dzialac takze wtedy, gdy w AMC stoi otwarte okno. Blokada zostaje tylko
    /// dla polecen zmieniajacych widok albo fokus.
    /// </summary>
    private static void TestCommandsAllowedWithOpenWindow()
    {
        foreach (var command in new[]
                 { "volumeUp", "volumeDown", "mute", "playPause", "seekBack", "seekForward",
                   "status", "context", "nowPlaying" })
        {
            Check(NvdaCommands.WorksWithOpenWindow(command),
                $"Polecenie {command} dziala przy otwartym oknie AMC");
            Check(NvdaCommands.IsAllowed(command) || command is "status" or "context" or "nowPlaying",
                $"Polecenie {command} jest znane mostkowi");
        }

        // Polecenia otwierajace widok nadal wymagaja zamkniecia okna - inaczej
        // wyrwalyby fokus z okna, w ktorym uzytkownik wlasnie pracuje.
        foreach (var command in new[] { "showPlayer", "showLibrary", "showSchedules", "showSearch" })
            Check(!NvdaCommands.WorksWithOpenWindow(command),
                $"Polecenie {command} zmienia widok, wiec czeka na zamkniecie okna");
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
        // 2026-09-12: uniewaznienie jawne, nie zegarkiem. Wczesniej stalo tu
        // CancellationTokenSource(30) i czekanie na uplyniecie 30 ms. Gdy maszyna
        // zdazyla obsluzyc zlecenie przed wygasnieciem, test padal na "Expired
        // request cannot run after the UI recovers" - ZMIERZONE 27 razy na 200
        // przebiegow (13,5%). Jawne Cancel() bada te sama regule (wygasle
        // zlecenie nie wykonuje sie po odblokowaniu interfejsu), ale bez
        // zaleznosci od szybkosci maszyny: 0 bledow na 300 przebiegow.
        using var deadline = new CancellationTokenSource();
        var operation = dispatcher.InvokeAsync(() => calls++, DispatcherPriority.Input, deadline.Token);
        deadline.Cancel();
        Check(deadline.Token.IsCancellationRequested, "Dispatcher deadline expired");
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
        // Podstaw znane argumenty po nazwach, a nowe opcjonalne pola pozostaw
        // z deklarowanymi wartosciami domyslnymi. Nie przywiazuj testu wyboru
        // biezacego pliku do liczby niezaleznych rodzajow wierszy listy.
        var constructor = rowType.GetConstructors(fields).Single();
        var rowArguments = constructor.GetParameters().Select(parameter => parameter.Name switch
        {
            "item" => (object)wrongItem,
            "label" or "navigationText" => wrongItem.Title,
            "folderPath" => "X:\\fictional-folder",
            _ when parameter.HasDefaultValue => parameter.DefaultValue,
            _ => throw new InvalidOperationException("Nowy wymagany parametr wiersza: " + parameter.Name)
        }).ToArray();
        var row = constructor.Invoke(rowArguments);
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
        // 2026-09-12: pauza przetrwa przeskok elementu (poprawka 1 z wersji 345,
        // zgloszona przez uzytkownika). Przejscie na nastepny ulubiony ustawia go
        // jako biezacy, ale NIE wznawia grania. Test wczesniej wymagal IsPlaying,
        // czyli zachowania, ktore uzytkownik kazal zmienic.
        Check(session.PlayRelative(1) && !session.IsPlaying && session.CurrentItem == b,
            "Next from pause selects next favorite without leaving pause");
        Check(NvdaPlaybackContext.Describe(session, "Ulubione") == "Ulubione, Radio, 2 z 2.",
            "Playback context has an intentional label and position");
        Check(!session.PlayRelative(1) && session.CurrentItem == b, "End does not leak into the catalog");
        Check(session.PlayRelative(-1) && session.CurrentItem == a, "Previous preserves source context");
        session.SetPlaybackContext([b.Id, a.Id]);
        Check(NvdaPlaybackContext.Describe(session, "Presety") == "Presety, Radio, 2 z 2.", "Presets report their own context");
    }

    private static void TestNowPlaying()
    {
        // Skrot "co teraz leci" (NVDA plus strzalka w gore w modulze aplikacji)
        // musi w radiu podac OBA pola: nazwe stacji i rozpoznany tytul utworu.
        // BEZ stanu odtwarzania - Michal chce dokladnie tego, co mowi WiiM:
        // "stacja, utwor - wykonawca".
        var stacja = new MediaItem { Id = "s", Title = "Radio Nowy Swiat", Kind = MediaItemKind.Station };
        var radio = new DemoMediaSession("radio", "Radio", [stacja]);
        radio.Play(stacja);
        Check(NvdaNowPlaying.Describe(radio, "Kwartet Jorgi - Kolysanka", true)
            == "Radio Nowy Swiat, Kwartet Jorgi - Kolysanka.",
            "Radio mowi i stacje, i utwor");
        // Stan odtwarzania NIE moze sie tu doklejac - to nie jest polecenie status.
        radio.TogglePlayback();
        Check(!NvdaNowPlaying.Describe(radio, "Kwartet Jorgi - Kolysanka", true).Contains("pauza"),
            "Skrot nie dokleja stanu odtwarzania");
        radio.TogglePlayback();
        // Metadane z INNEJ stacji nie moga wyciec do biezacej. Gdy nie ma czego
        // powiedziec poza stacja, zostaje SAMA STACJA - bez dopowiadania
        // "stacja nie podaje tytulu utworu" (ZGLOSZENIE Michala 17.09.2026:
        // ten komunikat byl zbedny).
        Check(NvdaNowPlaying.Describe(radio, "Utwor ze starej stacji", false)
            == "Radio Nowy Swiat.",
            "Nieaktualne metadane radia nie sa czytane");
        // Powielony tytul (stacja podaje wlasna nazwe jako utwor) tez nie.
        Check(NvdaNowPlaying.Describe(radio, "  radio nowy swiat ", true)
            == "Radio Nowy Swiat.",
            "Powtorzona nazwa stacji nie jest czytana dwa razy");
        Check(!NvdaNowPlaying.Describe(radio, "Utwor ze starej stacji", false)
            .Contains("nie podaje", StringComparison.CurrentCultureIgnoreCase),
            "Skrot nie dopowiada, ze stacja nie podaje tytulu");

        // ZGLOSZENIE Michala 17.09.2026 (druga czesc): Radio Poznan NIE wysyla
        // tytulu w transmisji, ale program sam rozpoznaje utwor i ma go w
        // historii pod Ctrl+Alt+S. Skoro juz go znamy, skrot ma go powiedziec.
        var teraz = DateTime.UtcNow;
        var rozpoznaneTejStacji = new List<RadioRecognizedTrackSettings>
        {
            new()
            {
                StationId = "s",
                StationName = "Radio Nowy Swiat",
                Title = "Nastolatek",
                Artist = "Krzysztof Zalewski",
                RecognizedUtcTicks = teraz.AddMinutes(-2).Ticks
            }
        };
        Check(NvdaNowPlaying.Describe(radio, null, false, rozpoznaneTejStacji, teraz)
            == "Radio Nowy Swiat, Nastolatek, Krzysztof Zalewski.",
            "Milczaca stacja: skrot podaje utwor rozpoznany przez program");

        // Tytul z SAMEJ transmisji ma pierwszenstwo - rozpoznanie jest tylko
        // zastepnikiem, nie moze przeslonic prawdziwych metadanych stacji.
        Check(NvdaNowPlaying.Describe(radio, "Kwartet Jorgi - Kolysanka", true, rozpoznaneTejStacji, teraz)
            == "Radio Nowy Swiat, Kwartet Jorgi - Kolysanka.",
            "Tytul z transmisji ma pierwszenstwo nad rozpoznaniem");

        // Rozpoznanie z INNEJ stacji nie moze wyciec do biezacego odsluchu -
        // rozpoznawanie chodzi tez dla stacji nagrywanych w tle.
        var rozpoznaneObcej = new List<RadioRecognizedTrackSettings>
        {
            new()
            {
                StationId = "inna",
                StationName = "Radio Jazz",
                Title = "So What",
                Artist = "Miles Davis",
                RecognizedUtcTicks = teraz.AddMinutes(-1).Ticks
            }
        };
        Check(NvdaNowPlaying.Describe(radio, null, false, rozpoznaneObcej, teraz)
            == "Radio Nowy Swiat.",
            "Rozpoznanie z innej stacji nie wycieka do biezacej");

        // Stare rozpoznanie to juz nie "teraz" - podanie go byloby klamstwem,
        // ktorego czytnik ekranu nie odsieje.
        var rozpoznaneStare = new List<RadioRecognizedTrackSettings>
        {
            new()
            {
                StationId = "s",
                StationName = "Radio Nowy Swiat",
                Title = "Utwor sprzed godziny",
                Artist = "Ktokolwiek",
                RecognizedUtcTicks = teraz.AddHours(-1).Ticks
            }
        };
        Check(NvdaNowPlaying.Describe(radio, null, false, rozpoznaneStare, teraz)
            == "Radio Nowy Swiat.",
            "Przestarzale rozpoznanie nie jest podawane jako biezace");

        // Album stacji tez ma byc slyszalny, dokladnie jak w sesji WiiM.
        var stacjaZAlbumem = new MediaItem
        {
            Id = "s2", Title = "Radio Jazz", Artist = "Miles Davis", RelatedAlbumTitle = "Kind of Blue",
            Kind = MediaItemKind.Station
        };
        var radioPelne = new DemoMediaSession("radio", "Radio", [stacjaZAlbumem]);
        radioPelne.Play(stacjaZAlbumem);
        Check(NvdaNowPlaying.Describe(radioPelne, "So What", true)
            == "Radio Jazz, So What, Miles Davis, Kind of Blue.",
            "Radio mowi stacje, utwor, wykonawce i album jak WiiM");

        var utwor = new MediaItem { Id = "u", Title = "Kolysanka", Artist = "Kwartet Jorgi" };
        var lokalna = new DemoMediaSession("local", "Pliki lokalne", [utwor]);
        lokalna.Play(utwor);
        lokalna.TogglePlayback();
        Check(NvdaNowPlaying.Describe(lokalna, null, false)
            == "Kolysanka, Kwartet Jorgi.",
            "Plik lokalny mowi tytul i wykonawce");

        var pusta = new DemoMediaSession("podcasts", "Podcasty", []);
        Check(NvdaNowPlaying.Describe(pusta, null, false) == "Podcasty, nic nie jest otwarte.",
            "Pusta sesja mowi wprost, ze nic nie leci");
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
