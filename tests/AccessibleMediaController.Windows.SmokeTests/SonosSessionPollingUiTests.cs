using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// ODSWIEZANIE sesji Sonos mierzone na PRAWDZIWYM <see cref="MainWindow"/> i na
/// PRAWDZIWYM tyknieciu licznika odtwarzacza (<c>PlayerUiTimer_Tick</c>), nie na
/// samym pomocniczym helperze. Granica API/transportu jest SYNTETYCZNA
/// (<see cref="ISonosGroupSessionBackend"/>): zero sieci, konta, DPAPI i audio.
///
/// Mierzone twierdzenia:
///   * C1: rzeczywiste tykniecie licznika wykonuje odczyt UZYWANEJ grupy, nic
///     nie mowi do czytnika i nie wysyla POST,
///   * C2: gdy pierwszy odczyt trwa, dalsze tykniecia NIE zwiekszaja ruchu
///     (prawdziwa bariera Task, nie sam termin), a bilet aktywacji zostaje
///     nietkniety, wiec Enter po przebudzeniu licznika dalej dziala,
///   * C3: sukces planuje kolejny termin, a blad KAZDEGO z trzech odczytow
///     (playback, metadata, volume; takze 429 i timeout) daje backoff; tykniecie
///     przed terminem nie robi zadnego ruchu,
///   * C4: spozniony odczyt tla NIE nadpisuje danych nowszego odczytu tej SAMEJ
///     grupy zrobionego po poleceniu, a odczyt po poleceniu naprawde sie
///     wykonuje,
///   * C5: polecenie A w locie + zmiana grupy na B nie zostawia B trwale
///     zajetego; spoznione zakonczenie A nie zwalnia blokady nowszego polecenia.
///
/// Czego to NIE dowodzi: nie ma tu odsluchu NVDA, prawdziwej mowy, konta Sonos
/// ani odbioru produktu. Okno jest WLASNE i pokazywane na zarezerwowanym pulpicie.
/// </summary>
internal static class SonosSessionPollingUiTests
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
                checks += MeasureRealTickReadsActiveGroup();
                checks += MeasureOverlappingTicksDoNotMultiplyTraffic();
                checks += MeasureDueDateAndBackoffPerReadKind();
                checks += MeasureStaleBackgroundReadDoesNotOverwriteCommandRead();
                checks += MeasureCommandGateReleasedAfterGroupChange();
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
            "OK: odswiezanie sesji Sonos na prawdziwym tyknieciu licznika - odczyt uzywanej grupy "
            + $"bez mowy i bez POST ({checks} sprawdzeń, WLASNE pokazane okno)");
    }

    // ===== C1: PRAWDZIWY PlayerUiTimer_Tick czyta uzywana grupe =====

    private static int MeasureRealTickReadsActiveGroup()
    {
        using var harness = Harness.Create();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        var playbackBefore = harness.Backend.PlaybackReads;
        var metadataBefore = harness.Backend.MetadataReads;
        var volumeBefore = harness.Backend.VolumeReads;
        var announcementsBefore = harness.Announcements.Count;
        if (playbackBefore == 0)
        {
            throw new Exception("Wejście do odtwarzacza nie zrobiło jawnego odczytu; pomiar nie ma punktu odniesienia.");
        }

        // Termin kolejnego odczytu tla jest POLITYKA AMC (10 s). Pomiar nie czeka
        // realnych sekund: przestawia sam termin na przeszlosc, wiec tykniecie
        // jest wymagalne. Zadnego innego stanu nie ruszamy.
        harness.MakeBackgroundReadDue();

        // PRAWDZIWY caller: to samo zdarzenie, ktore wola licznik odtwarzacza.
        harness.RaisePlayerUiTimerTick();
        harness.PumpUntil(
            () => harness.Backend.PlaybackReads > playbackBefore,
            "prawdziwe tyknięcie licznika nie wykonało odczytu grupy Sonos");

        if (harness.Backend.MetadataReads <= metadataBefore
            || harness.Backend.VolumeReads <= volumeBefore)
        {
            throw new Exception("Odczyt w tle nie domknął stanu, metadanych i głośności.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Odczyt w tle wysłał polecenie do Sonosa.");
        }
        if (harness.Announcements.Count != announcementsBefore)
        {
            throw new Exception("Odczyt w tle powiedział coś użytkownikowi.");
        }
        if (harness.Backend.ReadGroupIds.Any(id => !string.Equals(id, "GRUPA-SALON", StringComparison.Ordinal)))
        {
            throw new Exception("Odczyt w tle dotyczył innej grupy niż używana.");
        }
        return 5;
    }

    // ==================== aparatura ====================
    // ===== C2: bariera odczytu - dalsze tykniecia nie mnoza ruchu =====

    private static int MeasureOverlappingTicksDoNotMultiplyTraffic()
    {
        using var harness = Harness.Create();
        harness.OpenPlayerForGroup("GRUPA-SALON");
        var activationTicket = harness.TargetTicket;

        // Pierwszy odczyt tla ZATRZYMANY na barierze w ReadGroupPlaybackAsync.
        var gate = harness.Backend.CreateGate();
        harness.Backend.PlaybackGate = gate.Task;
        Task poll;
        try
        {
            harness.MakeBackgroundReadDue();
            harness.RaisePlayerUiTimerTick();
            harness.PumpUntil(
                () => harness.Window.LastSonosBackgroundPollTaskForTests is { } started && !started.IsCompleted,
                "tyknięcie nie rozpoczęło odczytu tła albo odczyt skończył się mimo bariery");
            poll = harness.Window.LastSonosBackgroundPollTaskForTests!;
            var playbackDuringFirst = harness.Backend.PlaybackReads;

            // Termin jest wymagalny, ale odczyt NADAL trwa: 5 kolejnych tyknieć
            // nie moze wyslac ani jednego dodatkowego GET-u.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                harness.MakeBackgroundReadDue();
                harness.RaisePlayerUiTimerTick();
                harness.PumpQuietly(TimeSpan.FromMilliseconds(30));
            }
            if (harness.Backend.PlaybackReads != playbackDuringFirst)
            {
                throw new Exception(
                    $"Tykniecia w trakcie odczytu zwiekszyly ruch z {playbackDuringFirst} "
                    + $"do {harness.Backend.PlaybackReads} GET stanu.");
            }
            if (harness.Backend.MetadataReads != 1 && harness.Backend.MetadataReads != 0)
            {
                // Pierwszy odczyt stoi PRZED metadanymi; wejscie dalo jeden.
                throw new Exception(
                    $"Nieoczekiwana liczba GET metadanych w trakcie bariery: {harness.Backend.MetadataReads}.");
            }

            // Bilet AKTYWACJI nietkniety: Enter po przebudzeniu licznika dziala.
            if (harness.TargetTicket != activationTicket)
            {
                throw new Exception(
                    "Odczyt w tle podniósł bilet aktywacji; spóźniona aktywacja zostałaby odrzucona bez potrzeby.");
            }
        }
        finally
        {
            harness.Backend.PlaybackGate = null;
            gate.TrySetResult();
        }

        // OBSERWUJEMY zadanie: nieskonczone albo bledne nie jest zaliczeniem.
        harness.Pump(poll);
        if (harness.Backend.MetadataReads == 0 || harness.Backend.VolumeReads == 0)
        {
            throw new Exception("Po zwolnieniu bariery odczyt nie domknął metadanych i głośności.");
        }
        return 4;
    }

    // ===== C3: termin po sukcesie, backoff po bledzie KAZDEGO rodzaju =====

    private static int MeasureDueDateAndBackoffPerReadKind()
    {
        var checks = 0;

        // Sukces: kolejny termin PLANOWANY na zwykly interwal.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            harness.MakeBackgroundReadDue();
            harness.RaisePlayerUiTimerTick();
            harness.Pump(harness.Window.LastSonosBackgroundPollTaskForTests!);
            var planned = harness.NextBackgroundReadUtc;
            if (planned <= DateTime.UtcNow)
            {
                throw new Exception("Udany odczyt nie zaplanował kolejnego terminu.");
            }
            if (planned > DateTime.UtcNow + MainWindow.SonosBackgroundReadInterval + TimeSpan.FromSeconds(5))
            {
                throw new Exception(
                    "Po sukcesie termin jest dalszy niż polityka odczytu w tle; oszczędność chmury zmieniona.");
            }

            // Tykniecie PRZED terminem nie robi zadnego ruchu.
            var before = harness.Backend.PlaybackReads;
            harness.RaisePlayerUiTimerTick();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (harness.Backend.PlaybackReads != before)
            {
                throw new Exception("Tyknięcie przed terminem wysłało odczyt.");
            }
            checks += 3;
        }

        // Kazdy z TRZECH rodzajow odczytu osobno: 429 daje BACKOFF.
        foreach (var kind in new[] { "playback", "metadata", "volume" })
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            switch (kind)
            {
                case "playback": harness.Backend.PlaybackFailure = SonosDeviceReadStatus.RateLimited; break;
                case "metadata": harness.Backend.MetadataFailure = SonosDeviceReadStatus.RateLimited; break;
                default: harness.Backend.VolumeFailure = SonosDeviceReadStatus.RateLimited; break;
            }

            harness.MakeBackgroundReadDue();
            harness.RaisePlayerUiTimerTick();
            harness.Pump(harness.Window.LastSonosBackgroundPollTaskForTests!);

            var planned = harness.NextBackgroundReadUtc;
            var minimum = DateTime.UtcNow + MainWindow.SonosBackgroundReadInterval + TimeSpan.FromSeconds(1);
            if (planned < minimum)
            {
                throw new Exception(
                    $"Błąd 429 odczytu „{kind}” nie dał backoffu: termin {planned:O} nie jest dalszy niż zwykły interwał.");
            }
            checks++;
        }

        // Syntetyczny TIMEOUT: wyjatek MUSI polecieć do obserwatora zadania, a
        // termin i tak wchodzi w backoff. Zadnego POST ani retry.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            harness.Backend.PlaybackThrows = new TimeoutException("syntetyczny timeout odczytu");
            harness.MakeBackgroundReadDue();
            harness.RaisePlayerUiTimerTick();
            var poll = harness.Window.LastSonosBackgroundPollTaskForTests
                ?? throw new Exception("Tyknięcie nie rozpoczęło odczytu przed timeoutem.");
            var threw = false;
            try
            {
                harness.Pump(poll);
            }
            catch (TimeoutException)
            {
                threw = true;
            }
            if (!threw)
            {
                throw new Exception("Timeout odczytu został zjedzony: zadanie nie rzuciło wyjątku.");
            }
            harness.Backend.PlaybackThrows = null;
            if (harness.NextBackgroundReadUtc
                < DateTime.UtcNow + MainWindow.SonosBackgroundReadInterval + TimeSpan.FromSeconds(1))
            {
                throw new Exception("Timeout odczytu nie dał backoffu.");
            }
            if (harness.Backend.Commands.Count != 0)
            {
                throw new Exception("Nieudany odczyt wysłał polecenie do Sonosa.");
            }
            checks += 3;
        }

        return checks;
    }

    // ===== C4: stary odczyt tla nie nadpisuje odczytu po poleceniu =====

    private static int MeasureStaleBackgroundReadDoesNotOverwriteCommandRead()
    {
        using var harness = Harness.Create();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        // Odczyt TLA startuje i zostaje na barierze ze STARYM tytulem.
        var staleGate = harness.Backend.CreateGate();
        harness.Backend.PlaybackGate = staleGate.Task;
        harness.Backend.NextTrackTitle = "STARY-TYTUL";
        harness.MakeBackgroundReadDue();
        harness.RaisePlayerUiTimerTick();
        harness.PumpUntil(
            () => harness.Window.LastSonosBackgroundPollTaskForTests is { IsCompleted: false },
            "odczyt tła nie stanął na barierze");
        var stalePoll = harness.Window.LastSonosBackgroundPollTaskForTests!;

        // Bramka polecenia nie jest zajeta przez odczyt tla, wiec polecenie
        // przechodzi; jego JAWNY odczyt jest NOWSZY i ma NOWY tytul.
        harness.Backend.PlaybackGate = null;
        harness.Backend.NextTrackTitle = "NOWY-TYTUL";
        var command = harness.StartCommand(CommandIds.Next);
        harness.Pump(command);
        if (harness.Backend.Commands.Count != 1)
        {
            throw new Exception(
                $"Polecenie nie dotarło do backendu dokładnie raz, lecz {harness.Backend.Commands.Count} razy.");
        }
        if (harness.Metadata?.CurrentTrack?.Name != "NOWY-TYTUL")
        {
            throw new Exception(
                "Odczyt po poleceniu nie wykonał się (zwrócił tylko busy): tytuł to "
                + (harness.Metadata?.CurrentTrack?.Name ?? "brak") + ".");
        }

        // TERAZ zwalniamy SPOZNIONY odczyt tla TEJ SAMEJ grupy.
        staleGate.SetResult();
        harness.Pump(stalePoll);
        if (harness.Metadata?.CurrentTrack?.Name != "NOWY-TYTUL")
        {
            throw new Exception(
                "Spóźniony odczyt tła nadpisał świeższy wynik po poleceniu: tytuł to "
                + (harness.Metadata?.CurrentTrack?.Name ?? "brak") + ".");
        }
        if (harness.Backend.Commands.Count != 1)
        {
            throw new Exception("Odczyt tła albo jego domknięcie powtórzyło POST.");
        }
        return 4;
    }

    // ===== C5: bramka polecenia zwolniona po zmianie grupy =====

    private static int MeasureCommandGateReleasedAfterGroupChange()
    {
        using var harness = Harness.Create();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        // Polecenie A wisi w POST.
        var commandGate = harness.Backend.CreateGate();
        harness.Backend.CommandGate = commandGate.Task;
        var commandA = harness.StartCommand(CommandIds.Next);
        harness.PumpUntil(
            () => harness.Backend.Commands.Count == 1,
            "polecenie A nie dotarło do backendu");
        if (commandA.IsCompleted) throw new Exception("Polecenie A zakończyło się mimo wstrzymanego POST.");

        // Uzytkownik zmienia grupe na B (to podnosi bilet CELU).
        harness.Pump(harness.Window.ActivateSonosGroupForTests("GRUPA-KUCHNIA"));
        if (harness.Window.SonosActiveGroup?.Id != "GRUPA-KUCHNIA")
        {
            throw new Exception("Zmiana grupy na B się nie zapisała.");
        }

        // SPOZNIONE zakonczenie A: nie moze zostawic bramki zamknietej.
        harness.Backend.CommandGate = null;
        commandGate.SetResult();
        harness.Pump(commandA);

        // Kolejne polecenie grupy B MUSI dotrzec do backendu.
        var before = harness.Backend.Commands.Count;
        var commandB = harness.StartCommand(CommandIds.Next);
        harness.Pump(commandB);
        if (harness.Backend.Commands.Count != before + 1)
        {
            throw new Exception(
                "Po zmianie grupy bramka polecenia została trwale zajęta: nowe polecenie nie doszło do backendu "
                + $"(POST-y: {before} -> {harness.Backend.Commands.Count}).");
        }
        if (harness.Backend.CommandGroupIds.Last() != "GRUPA-KUCHNIA")
        {
            throw new Exception("Polecenie po zmianie grupy poszło do innej grupy niż wybrana.");
        }
        if (harness.Announcements.Any(text => text.Contains("jeszcze się nie zakończyło", StringComparison.Ordinal)))
        {
            throw new Exception("Użytkownik usłyszał odmowę „poprzednie polecenie jeszcze się nie zakończyło”.");
        }

        // SPOZNIONE finally A nie moze zwolnic blokady NOWSZEGO polecenia.
        var secondGate = harness.Backend.CreateGate();
        harness.Backend.CommandGate = secondGate.Task;
        var pending = harness.StartCommand(CommandIds.Next);
        harness.PumpUntil(() => harness.CommandInFlight, "nowsze polecenie nie zajęło bramki");
        harness.ReleaseCommandGateAsStalePass();
        if (!harness.CommandInFlight)
        {
            throw new Exception("Spóźnione finally starszego przelotu zwolniło bramkę nowszego polecenia.");
        }
        harness.Backend.CommandGate = null;
        secondGate.SetResult();
        harness.Pump(pending);
        return 5;
    }


    /// <summary>
    /// PRAWDZIWE okno, PRAWDZIWA droga wejscia (polecenie slotu + Enter) i
    /// SYNTETYCZNA granica API. Pompa dispatchera jest rzeczywista (PushFrame),
    /// a po limicie RZUCA: nieskonczone zadanie nie moze udawac zaliczenia.
    /// </summary>
    private sealed class Harness : IDisposable
    {
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

        internal bool PlayerViewActive => (bool)Field("_playerViewActive")!;

        internal static Harness Create()
        {
            // JAWNY kontekst synchronizacji PRZED konstrukcja okna: bez tego
            // await w kodzie okna wracalby na pule watkow.
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-poll-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>
        /// Rzeczywista droga uzytkownika do odtwarzacza grupy: polecenie slotu
        /// sesji (to, co robi Ctrl+8) i Enter na ZAZNACZONYM wierszu.
        /// </summary>
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
        }

        internal void SelectRowByGroupId(string groupId)
        {
            var list = MediaList;
            var index = Enumerable.Range(0, list.Items.Count).First(position =>
                string.Equals(RowId(list.Items[position]), groupId, StringComparison.Ordinal));
            list.SelectedIndex = index;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
            {
                container.Focus();
            }
            else
            {
                list.Focus();
            }
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.SelectedIndex != index) throw new Exception("Nie udało się zaznaczyć wiersza grupy.");
        }

        private static string? RowId(object row) =>
            row.GetType().GetProperty("Item", Instance)?.GetValue(row) is MediaItem item ? item.Id : null;

        /// <summary>Pokazuje WYLACZNIE wlasne okno; startowe zrodla odlaczone.</summary>
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
            try
            {
                method.Invoke(Window, [commandId]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        /// <summary>PRAWDZIWE zdarzenie klawiatury na elemencie z fokusem.</summary>
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

        /// <summary>
        /// PRAWDZIWY handler licznika odtwarzacza. Mierzymy CALLERA, nie sam
        /// pomocniczy helper sesji Sonos.
        /// </summary>
        internal void RaisePlayerUiTimerTick()
        {
            var method = Window.GetType().GetMethod(
                "PlayerUiTimer_Tick", Instance, binder: null,
                types: [typeof(object), typeof(EventArgs)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwego handlera PlayerUiTimer_Tick.");
            try
            {
                method.Invoke(Window, [null, EventArgs.Empty]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        /// <summary>
        /// Przestawia SAM termin kolejnego odczytu tla na przeszlosc. Zegara
        /// produkcyjnego nie podmieniamy i zadnego innego stanu nie ruszamy.
        /// </summary>
        internal void MakeBackgroundReadDue() =>
            SetField("_sonosNextBackgroundReadUtc", DateTime.MinValue);

        internal DateTime NextBackgroundReadUtc => (DateTime)Field("_sonosNextBackgroundReadUtc")!;

        internal int TargetTicket => (int)Field("_sonosTargetTicket")!;

        internal bool CommandInFlight => (bool)Field("_sonosCommandInFlight")!;

        internal SonosGroupMetadata? Metadata =>
            Window.GetType().GetField("_sonosMetadata", Instance)!.GetValue(Window) as SonosGroupMetadata;

        /// <summary>Polecenie PRAWDZIWA droga sesji Sonos; zwraca jego zadanie.</summary>
        internal Task StartCommand(string commandId) => Window.ExecuteSonosCommandForTests(commandId);

        /// <summary>
        /// Symuluje SPOZNIONE <c>finally</c> STARSZEGO przelotu polecenia:
        /// wywoluje prawdziwe zwolnienie bramki z biletem, ktory JUZ nie jest
        /// biezacy. Poprawna implementacja MUSI to zignorowac.
        /// </summary>
        internal void ReleaseCommandGateAsStalePass()
        {
            var current = (int)Field("_sonosCommandGateTicket")!;
            Window.ReleaseSonosCommandGateForTests(current - 1);
            PumpQuietly(TimeSpan.FromMilliseconds(30));
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

        /// <summary>
        /// Pompa czekajaca na ZAKONCZENIE zadania i OBSERWUJACA jego wynik.
        /// IsFaulted=false przy zadaniu nieskonczonym nie jest zaliczeniem.
        /// </summary>
        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new Exception("Limit czasu: zadanie sesji Sonos się nie zakończyło.");
                }
                DoEvents();
            }

            task.GetAwaiter().GetResult();
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

    /// <summary>
    /// SYNTETYCZNA granica API: zero HttpClient, zero tokenu, zero magazynu.
    /// Bramki sa jawnymi <see cref="TaskCompletionSource"/>, wiec pomiar steruje
    /// PORZADKIEM bez usypiania watku.
    /// </summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        private readonly List<TaskCompletionSource> _gates = [];

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        internal List<string?> ReadGroupIds { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int MetadataReads { get; private set; }

        internal int VolumeReads { get; private set; }

        internal SonosPlaybackState NextPlaybackState { get; set; } = SonosPlaybackState.Playing;

        internal string NextItemId { get; set; } = "UTWOR-1";

        internal string NextTrackTitle { get; set; } = "Preludium";

        internal int NextVolume { get; set; } = 30;

        /// <summary>Wymuszony status ODCZYTU danego rodzaju albo null = sukces.</summary>
        internal SonosDeviceReadStatus? PlaybackFailure { get; set; }

        internal SonosDeviceReadStatus? MetadataFailure { get; set; }

        internal SonosDeviceReadStatus? VolumeFailure { get; set; }

        /// <summary>Wyjatek zamiast odpowiedzi - syntetyczny timeout odczytu.</summary>
        internal Exception? PlaybackThrows { get; set; }

        internal Task? PlaybackGate { get; set; }

        internal Task? CommandGate { get; set; }

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        internal TaskCompletionSource CreateGate()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gates.Add(gate);
            return gate;
        }

        /// <summary>Zwalnia WSZYSTKIE bramki, zeby zamkniecie pomiaru nie wisialo.</summary>
        internal void ReleaseEverything()
        {
            foreach (var gate in _gates) gate.TrySetResult();
        }

        public async Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReads++;
            ReadGroupIds.Add(groupId);
            if (PlaybackGate is { } gate) await gate.ConfigureAwait(false);
            if (PlaybackThrows is { } failure) throw failure;
            if (PlaybackFailure is { } status)
            {
                return SonosGroupReadResult<SonosGroupPlaybackStatus>.Failure(status);
            }

            var state = new SonosGroupPlaybackStatus(
                NextPlaybackState, null, null, NextItemId, 12_000, null, null, null, _actions);
            return SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(state);
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            MetadataReads++;
            ReadGroupIds.Add(groupId);
            if (MetadataFailure is { } status)
            {
                return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Failure(status));
            }

            var track = new SonosTrackMetadata(
                "track", NextTrackTitle, "Chopin", "Nokturny", null,
                new SonosMetadataService("Sonos Radio", "9"), 180_000);
            var metadata = new SonosGroupMetadata(
                null, new SonosQueueItem(NextItemId, track, null), null, null, null);
            return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(metadata));
        }

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            VolumeReads++;
            ReadGroupIds.Add(groupId);
            if (VolumeFailure is { } status)
            {
                return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Failure(status));
            }

            return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(
                new SonosGroupVolume(NextVolume, false, false)));
        }

        public async Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            CommandGroupIds.Add(groupId);
            if (CommandGate is { } gate) await gate.ConfigureAwait(false);
            return SonosGroupCommandResult.CreateAcceptedForMeasurement(command);
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
                    new SonosGroup("GRUPA-KUCHNIA", "Kuchnia", "P2", ["P2"], SonosPlaybackState.Idle)
                ],
                [
                    new SonosPlayer("P1", "Salon", null, null, null),
                    new SonosPlayer("P2", "Kuchnia", null, null, null)
                ],
                false)));
    }
}
