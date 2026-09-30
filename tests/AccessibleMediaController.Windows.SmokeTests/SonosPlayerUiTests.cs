using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// UZYTKOWY odtwarzacz Sonos mierzony na PRAWDZIWYM <see cref="MainWindow"/>:
/// RZECZYWISTE kontrolki widoku odtwarzacza, RZECZYWISTY przycisk i jego
/// dostepna nazwa oraz RZECZYWISTE polecenia czasu. Granica API jest
/// SYNTETYCZNA (<see cref="ISonosGroupSessionBackend"/>): zero sieci, konta,
/// DPAPI i audio.
///
/// Mierzone twierdzenia:
///   * D1: po wejsciu do odtwarzacza grupy WSZYSTKIE teksty odtwarzacza pochodza
///     z ODCZYTU Sonosa; zaden nie zostaje odziedziczony po poprzednim
///     odtwarzaczu innej sesji (sentinel w kontrolce musi zniknac),
///   * D2: RZECZYWISTY przycisk odtwarzania ma tresc i dostepna nazwe zgodna z
///     ODCZYTANYM stanem grupy, a zmiana Playing -> Paused ODCZYTEM aktualizuje
///     etykiete (nie zgadujemy z samego Accepted 200),
///   * D3: klikniecie RZECZYWISTEGO przycisku idzie do AKTYWNEJ GRUPY: dokladnie
///     JEDEN POST i potwierdzajacy GET, zadnej zmiany DemoMediaSession,
///   * D4: polecenia czasu (Ctrl+Shift+E/R/T) w sesji Sonos podaja ODCZYTANE
///     dane Sonosa, a nie pozycje 0 i dlugosc 0 z DemoMediaSession,
///   * D5: brak pozycji albo dlugosci to BRAK INFORMACJI, nie zero; radio bez
///     currentItem jest poprawnym stanem.
///
/// Czego to NIE dowodzi: nie ma tu odsluchu NVDA, prawdziwej mowy, klawiszy
/// systemowych, konta Sonos ani audio. Okno jest WLASNE i pokazywane.
/// </summary>
internal static class SonosPlayerUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasurePlayerTextsComeFromSonosRead();
                checks += MeasurePlayPauseButtonFollowsReadState();
                checks += MeasureRealButtonClickTargetsActiveGroup();
                checks += MeasureTimeCommandsUseSonosRead();
                checks += MeasureMissingTimeIsNotZero();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: uzytkowy odtwarzacz Sonos - kontrolki, przycisk i polecenia czasu z odczytu grupy "
            + $"({checks} sprawdzeń, WLASNE pokazane okno)");
    }

    // ===== D1: teksty odtwarzacza z ODCZYTU, bez dziedziczenia =====

    private static int MeasurePlayerTextsComeFromSonosRead()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;

        // SLAD po odtwarzaczu INNEJ sesji: kontrolki maja tresc, ktorej Sonos
        // nigdy by nie wypisal. Widok grupy MUSI ja zastapic wlasnym odczytem,
        // bo inaczej niewidomy uzytkownik slyszy czas i stan cudzej sesji.
        harness.StampLeftoverPlayerTexts();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        foreach (var (name, text) in harness.PlayerTexts())
        {
            if (text.Contains(Harness.Leftover, StringComparison.Ordinal))
            {
                throw new Exception(
                    $"Kontrolka {name} odtwarzacza Sonos zostawila tekst poprzedniego odtwarzacza: \"{text}\".");
            }
        }

        var time = harness.Text("PlayerTimeText");
        if (!time.Contains("0:12", StringComparison.Ordinal))
        {
            throw new Exception($"Kontrolka czasu nie pokazuje ODCZYTANEJ pozycji Sonosa; jest \"{time}\".");
        }
        if (!time.Contains("3:00", StringComparison.Ordinal))
        {
            throw new Exception($"Kontrolka czasu nie pokazuje ODCZYTANEJ dlugosci Sonosa; jest \"{time}\".");
        }
        if (!harness.Text("PlayerTitleText").Contains("Preludium", StringComparison.Ordinal))
        {
            throw new Exception("Tytul w odtwarzaczu nie pochodzi z odczytanych metadanych grupy.");
        }
        if (!harness.Text("PlayerArtistText").Contains("Sonos Radio", StringComparison.Ordinal))
        {
            throw new Exception("Zrodlo w odtwarzaczu nie pochodzi z odczytanych metadanych grupy.");
        }
        if (!harness.Text("PlayerSessionText").Contains("Salon", StringComparison.Ordinal))
        {
            throw new Exception("Odtwarzacz nie nazywa uzywanej grupy Sonos.");
        }
        var state = harness.Text("PlayerStateText");
        if (!state.Contains("Wstrzymane", StringComparison.Ordinal)
            || !state.Contains("procent", StringComparison.Ordinal))
        {
            throw new Exception($"Stan i glosnosc nie pochodza z odczytu grupy; jest \"{state}\".");
        }
        return 7;
    }

    // ===== D2: przycisk i jego dostepna nazwa ida za ODCZYTANYM stanem =====

    private static int MeasurePlayPauseButtonFollowsReadState()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Playing;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        var playing = harness.ButtonContent();
        if (!string.Equals(playing, "Wstrzymaj", StringComparison.Ordinal))
        {
            throw new Exception(
                $"Grupa ODTWARZA, a rzeczywisty przycisk podaje \"{playing}\" - uzytkownik nie wie, co zrobi Enter.");
        }
        var playingName = harness.ButtonAccessibleName();
        if (!playingName.Contains("Preludium", StringComparison.Ordinal)
            || !playingName.Contains("Wstrzymaj", StringComparison.Ordinal))
        {
            throw new Exception($"Dostepna nazwa przycisku nie opisuje materialu i akcji Sonosa; jest \"{playingName}\".");
        }

        // ZMIANA stanu poznana ODCZYTEM (nie z Accepted 200): kolejny odczyt tla
        // widzi Paused, wiec etykieta MUSI sie przestawic.
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
        harness.RefreshByBackgroundRead();

        var paused = harness.ButtonContent();
        if (!string.Equals(paused, "Odtwórz", StringComparison.Ordinal))
        {
            throw new Exception(
                $"Po odczytaniu stanu Wstrzymane rzeczywisty przycisk nadal podaje \"{paused}\".");
        }
        if (!harness.ButtonAccessibleName().Contains("Odtwórz", StringComparison.Ordinal))
        {
            throw new Exception("Dostepna nazwa przycisku nie nadazyla za odczytanym stanem grupy.");
        }
        return 4;
    }

    // ===== D3: rzeczywisty przycisk -> aktywna grupa, jeden POST i GET =====

    private static int MeasureRealButtonClickTargetsActiveGroup()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Playing;
        harness.OpenPlayerForGroup("GRUPA-KUCHNIA");

        var demoPositionBefore = harness.DemoSessionPosition;
        var playbackReadsBefore = harness.Backend.PlaybackReads;
        harness.Backend.Commands.Clear();
        harness.Backend.CommandGroupIds.Clear();

        // PRAWDZIWY handler przycisku z XAML, nie helper sesji.
        harness.ClickPlayPauseButton();
        harness.PumpUntil(
            () => harness.Backend.Commands.Count > 0,
            "klikniecie rzeczywistego przycisku nie wyslalo polecenia do grupy Sonos");
        harness.PumpUntil(
            () => harness.Backend.PlaybackReads > playbackReadsBefore,
            "po poleceniu nie poszedl potwierdzajacy odczyt stanu grupy");

        if (harness.Backend.Commands.Count != 1)
        {
            throw new Exception(
                $"Rzeczywisty przycisk wyslal {harness.Backend.Commands.Count} polecen zamiast jednego.");
        }
        // Sonos ma WLASNE playback/togglePlayPause i SPEC dopuszcza "Przelacz"
        // (wiersz "Odtwórz / Pauza / Przełącz"). Sprawdzamy wiec, ze poszlo
        // polecenie TRANSPORTU tej grupy, a nie zgadujemy kierunku za Sonosa.
        if (harness.Backend.Commands[0] is not SonosGroupCommand.TogglePlayPause
            and not SonosGroupCommand.Pause)
        {
            throw new Exception(
                $"Rzeczywisty przycisk wyslal {harness.Backend.Commands[0]} zamiast polecenia odtwarzania/pauzy.");
        }
        if (harness.Backend.CommandGroupIds.Any(id => !string.Equals(id, "GRUPA-KUCHNIA", StringComparison.Ordinal)))
        {
            throw new Exception("Polecenie z przycisku poszlo do innej grupy niz aktywna.");
        }
        if (harness.DemoSessionPosition != demoPositionBefore)
        {
            throw new Exception("Przycisk ruszyl DemoMediaSession zamiast sterowac wylacznie grupa Sonos.");
        }
        return 5;
    }

    // ===== D4: polecenia czasu czytaja Sonosa, nie DemoMediaSession =====

    private static int MeasureTimeCommandsUseSonosRead()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        // Pozycja 0:12 z dlugosci 3:00 - kazda z trzech odpowiedzi jest INNA,
        // wiec zadnej nie da sie zaliczyc przypadkiem.
        var elapsed = harness.AnnounceFor(CommandIds.TimeElapsed);
        if (!elapsed.Contains("0:12", StringComparison.Ordinal))
        {
            throw new Exception($"Czas od poczatku nie pochodzi z odczytu Sonosa; powiedziano \"{elapsed}\".");
        }
        var remaining = harness.AnnounceFor(CommandIds.TimeRemaining);
        if (!remaining.Contains("2:48", StringComparison.Ordinal))
        {
            throw new Exception($"Czas pozostaly nie pochodzi z odczytu Sonosa; powiedziano \"{remaining}\".");
        }
        var total = harness.AnnounceFor(CommandIds.TimeTotal);
        if (!total.Contains("3:00", StringComparison.Ordinal))
        {
            throw new Exception($"Czas calkowity nie pochodzi z odczytu Sonosa; powiedziano \"{total}\".");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Odczyt czasu wyslal polecenie do Sonosa.");
        }
        return 4;
    }

    // ===== D5: brak pozycji/dlugosci to brak informacji, nie zero =====

    private static int MeasureMissingTimeIsNotZero()
    {
        using var harness = Harness.Create();
        // RADIO: stacja bez currentItem, bez pozycji i bez dlugosci. To POPRAWNY
        // stan Sonosa, a nie blad - ale zero bylo by klamstwem.
        harness.Backend.RadioWithoutCurrentItem = true;
        harness.Backend.NextPlaybackState = SonosPlaybackState.Playing;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        foreach (var commandId in new[] { CommandIds.TimeElapsed, CommandIds.TimeRemaining, CommandIds.TimeTotal })
        {
            var said = harness.AnnounceFor(commandId);
            if (said.Contains("0:00", StringComparison.Ordinal))
            {
                throw new Exception(
                    $"Brak czasu w radiu Sonos zostal podany jako zero: \"{said}\" ({commandId}).");
            }
            if (!said.Contains("nieznan", StringComparison.OrdinalIgnoreCase)
                && !said.Contains("nie jest znan", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Brak czasu w radiu Sonos nie zostal nazwany brakiem informacji: \"{said}\" ({commandId}).");
            }
        }

        var time = harness.Text("PlayerTimeText");
        if (time.Contains("0:00", StringComparison.Ordinal))
        {
            throw new Exception($"Kontrolka czasu pokazuje zero mimo braku pozycji; jest \"{time}\".");
        }
        if (!harness.Text("PlayerTitleText").Contains("Radio Nowy Swiat", StringComparison.Ordinal))
        {
            throw new Exception("Stacja bez currentItem nie zostala uznana za poprawny tytul.");
        }
        return 5;
    }

    // ==================== aparatura ====================

    /// <summary>
    /// PRAWDZIWE okno, PRAWDZIWA droga wejscia (polecenie slotu + Enter) i
    /// SYNTETYCZNA granica API. Wzor aparatury jest przepisany z istniejacych
    /// pomiarow sesji Sonos, zeby nie budowac drugiej, innej maszynerii.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        internal const string Leftover = "SLAD-POPRZEDNIEGO-ODTWARZACZA";

        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

        private readonly string _directory;
        private readonly Dispatcher _dispatcher;

        private Harness(string directory, MainWindow window, FakeBackend backend, List<string> announcements)
        {
            _directory = directory;
            _dispatcher = Dispatcher.CurrentDispatcher;
            Window = window;
            Backend = backend;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        internal ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal Button PlayPauseButton => (Button)Window.FindName("PlayerPlayPauseButton")!;

        internal bool PlayerViewActive => (bool)Field("_playerViewActive")!;

        internal TimeSpan DemoSessionPosition
        {
            get
            {
                var sessions = Field("_sessions")!;
                var current = sessions.GetType().GetProperty("Current", Instance)!.GetValue(sessions)!;
                return (TimeSpan)current.GetType().GetProperty("Position", Instance)!.GetValue(current)!;
            }
        }

        internal static Harness Create()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-player-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = store.LoadOrCreate();
            state.Podcasts.Subscriptions.Clear();
            state.Podcasts.Episodes.Clear();
            state.WiiM.Devices.Clear();
            state.Radio.RecordingSchedules.Clear();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;

            var backend = new FakeBackend();
            var announcements = new List<string>();
            var window = new MainWindow(state, store)
            {
                SuppressDesktopIntegrationForTests = true,
                SonosBackendOverride = backend,
                AnnouncementSinkForTests = announcements.Add
            };
            window.DenyApplicationUpdateStartForTests();
            return new Harness(directory, window, backend, announcements);
        }

        internal void OpenPlayerForGroup(string groupId)
        {
            ShowOwnWindow();
            ExecuteCommand(CommandIds.SessionSlot(8));
            PumpUntil(() => MediaList.Items.Count == 2, "pierwsze wejście nie wypełniło kontrolki listy");
            SelectRowByGroupId(groupId);
            PressKey(Key.Enter);
            PumpUntil(
                () => PlayerViewActive
                    && string.Equals(Window.SonosActiveGroup?.Id, groupId, StringComparison.Ordinal),
                "Enter na wierszu grupy nie otworzył odtwarzacza tej grupy");
            PumpUntil(
                () => Backend.MetadataReads > 0 && Backend.VolumeReads > 0,
                "wejście do odtwarzacza nie domknęło odczytu metadanych i głośności");
            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        /// <summary>
        /// Wpisuje SLAD poprzedniego odtwarzacza wprost do kontrolek. To nie jest
        /// podmiana logiki produkcyjnej, tylko stan, jaki zostaje po odtwarzaczu
        /// INNEJ sesji - widok Sonosa ma go nadpisac wlasnym odczytem.
        /// </summary>
        internal void StampLeftoverPlayerTexts()
        {
            foreach (var name in TextControlNames)
            {
                ((TextBlock)Window.FindName(name)!).Text = Leftover;
            }
            PlayPauseButton.Content = Leftover;
        }

        private static readonly string[] TextControlNames =
        [
            "PlayerTitleText", "PlayerArtistText", "PlayerSessionText",
            "PlayerStateText", "PlayerTimeText"
        ];

        internal IEnumerable<(string Name, string Text)> PlayerTexts() =>
            TextControlNames.Select(name => (name, Text(name)))
                .Append(("PlayerPlayPauseButton", ButtonContent()));

        internal string Text(string controlName) =>
            ((TextBlock)Window.FindName(controlName)!).Text ?? string.Empty;

        internal string ButtonContent() => PlayPauseButton.Content as string ?? string.Empty;

        internal string ButtonAccessibleName() =>
            AutomationProperties.GetName(PlayPauseButton) ?? string.Empty;

        /// <summary>PRAWDZIWY handler kliknięcia przycisku z XAML.</summary>
        internal void ClickPlayPauseButton()
        {
            var method = Window.GetType().GetMethod(
                "PlayerPlayPause_Click", Instance, binder: null,
                types: [typeof(object), typeof(RoutedEventArgs)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwego handlera PlayerPlayPause_Click.");
            Invoke(method, [PlayPauseButton, new RoutedEventArgs()]);
        }

        /// <summary>
        /// ODCZYT tla PRAWDZIWA droga licznika odtwarzacza: to on ma przeniesc
        /// nowy stan grupy na kontrolki.
        /// </summary>
        internal void RefreshByBackgroundRead()
        {
            var playbackBefore = Backend.PlaybackReads;
            SetField("_sonosNextBackgroundReadUtc", DateTime.MinValue);
            var tick = Window.GetType().GetMethod(
                "PlayerUiTimer_Tick", Instance, binder: null,
                types: [typeof(object), typeof(EventArgs)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwego handlera PlayerUiTimer_Tick.");
            Invoke(tick, [null, EventArgs.Empty]);
            PumpUntil(
                () => Backend.PlaybackReads > playbackBefore
                    && Window.LastSonosBackgroundPollTaskForTests is { IsCompleted: true },
                "odczyt tła nie zakończył się nowym stanem grupy");
            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        /// <summary>Wykonuje polecenie i zwraca to, co RZECZYWIŚCIE powiedziano.</summary>
        internal string AnnounceFor(string commandId)
        {
            var before = Announcements.Count;
            ExecuteCommand(commandId);
            PumpUntil(() => Announcements.Count > before, $"polecenie {commandId} nic nie powiedziało");
            return string.Join(" | ", Announcements.Skip(before));
        }

        internal void SelectRowByGroupId(string groupId)
        {
            var list = MediaList;
            var index = Enumerable.Range(0, list.Items.Count).First(position =>
                string.Equals(RowId(list.Items[position]), groupId, StringComparison.Ordinal));
            list.SelectedIndex = index;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container) container.Focus();
            else list.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.SelectedIndex != index) throw new Exception("Nie udało się zaznaczyć wiersza grupy.");
        }

        private static string? RowId(object row) =>
            row.GetType().GetProperty("Item", Instance)?.GetValue(row) is AccessibleMediaController.Core.Sessions.MediaItem item
                ? item.Id
                : null;

        internal void ShowOwnWindow()
        {
            var rendered = (EventHandler)Delegate.CreateDelegate(
                typeof(EventHandler),
                Window,
                Window.GetType().GetMethod("Window_ContentRendered", Instance)!);
            Window.ContentRendered -= rendered;
            Window.ShowInTaskbar = false;
            Window.Show();
            PumpUntil(() => Window.IsLoaded && PresentationSource.FromVisual(Window) is not null,
                "własne okno się nie pokazało");
            MediaList.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(100));
        }

        internal void ExecuteCommand(string commandId)
        {
            var method = Window.GetType().GetMethod(
                "ExecuteCommand", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej metody ExecuteCommand(string).");
            Invoke(method, [commandId]);
        }

        internal void PressKey(Key key)
        {
            var target = Keyboard.FocusedElement as UIElement ?? MediaList;
            var source = PresentationSource.FromVisual(Window)
                ?? throw new Exception("Okno nie ma powierzchni prezentacji; pokaż je przed klawiszem.");
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            PumpQuietly(TimeSpan.FromMilliseconds(50));
        }

        private void Invoke(MethodInfo method, object?[] arguments)
        {
            try
            {
                method.Invoke(Window, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        internal void PumpUntil(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }
        }

        internal void PumpQuietly(TimeSpan duration)
        {
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline) DoEvents();
        }

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }

        private object? Field(string name) =>
            Window.GetType().GetField(name, Instance)?.GetValue(Window)
            ?? throw new Exception("Nie ma pola " + name + " w prawdziwym MainWindow.");

        private void SetField(string name, object? value) =>
            (Window.GetType().GetField(name, Instance)
                ?? throw new Exception("Nie ma pola " + name + " w prawdziwym MainWindow."))
            .SetValue(Window, value);

        public void Dispose()
        {
            // ZWALNIAMY WSZYSTKIE wlasne bramki PRZED zamknieciem, inaczej okno
            // czekaloby na zadanie, ktorego nikt juz nie dokonczy.
            Backend.ReleaseEverything();
            Window.CancelSonosPendingWork();

            var closed = false;
            void OnClosed(object? sender, EventArgs e) => closed = true;
            Window.Closed += OnClosed;
            try
            {
                Window.Close();
            }
            catch (InvalidOperationException)
            {
                closed = true;
            }

            var deadline = DateTime.UtcNow + Limit;
            while (!closed)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Window.Closed -= OnClosed;
                    throw new Exception("Limit czasu: własne okno pomiaru się nie zamknęło.");
                }
                DoEvents();
            }
            Window.Closed -= OnClosed;

            PumpQuietly(TimeSpan.FromMilliseconds(200));
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>SYNTETYCZNA granica API: zero HttpClient, tokenu i magazynu.</summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        private readonly List<TaskCompletionSource> _gates = [];

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int MetadataReads { get; private set; }

        internal int VolumeReads { get; private set; }

        internal SonosPlaybackState NextPlaybackState { get; set; } = SonosPlaybackState.Playing;

        /// <summary>Stacja bez currentItem: bez pozycji i bez dlugosci.</summary>
        internal bool RadioWithoutCurrentItem { get; set; }

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        internal void ReleaseEverything()
        {
            foreach (var gate in _gates) gate.TrySetResult();
        }

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReads++;
            var status = new SonosGroupPlaybackStatus(
                NextPlaybackState, null, null,
                RadioWithoutCurrentItem ? null : "UTWOR-1",
                RadioWithoutCurrentItem ? null : 12_000,
                null, null, null, _actions);
            return Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status));
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            MetadataReads++;
            var metadata = RadioWithoutCurrentItem
                ? new SonosGroupMetadata(
                    new SonosMetadataContainer(
                        "Radio Nowy Swiat", "station", new SonosMetadataService("Sonos Radio", "9")),
                    null, null, null, null)
                : new SonosGroupMetadata(
                    null,
                    new SonosQueueItem("UTWOR-1", new SonosTrackMetadata(
                        "track", "Preludium", "Chopin", "Nokturny", null,
                        new SonosMetadataService("Sonos Radio", "9"), 180_000), null),
                    null, null, null);
            return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(metadata));
        }

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            VolumeReads++;
            return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(
                new SonosGroupVolume(30, false, false)));
        }

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SeekRelative);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
        }

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosHouseholdsReadResult.Success([new SonosHousehold("DOM-1", "Dom", null)]));

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [
                    new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Playing),
                    new SonosGroup("GRUPA-KUCHNIA", "Kuchnia", "P2", ["P2"], SonosPlaybackState.Playing)
                ],
                [
                    new SonosPlayer("P1", "Salon", null, null, null),
                    new SonosPlayer("P2", "Kuchnia", null, null, null)
                ],
                false)));
    }
}
