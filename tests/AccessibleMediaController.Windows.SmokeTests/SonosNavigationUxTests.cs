using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
using AccessibleMediaController.Windows.Services;

/// <summary>
/// UKLAD interfejsu sesji Sonos mierzony RZECZYWISTA droga uzytkownika.
///
///   * U1 BIBLIOTEKA: istniejace Ctrl+L (CommandIds.ViewLibrary) pokazuje grupy
///     jako CELE sterowania. Mierzymy zawartosc KONTROLKI MediaList, nie flagi.
///   * U2 ULUBIONE: zaden tor nie ma prawa wstawic glosnika/grupy do Ulubionych.
///     ToggleFavorite na wierszu grupy (takze przy PUSTEJ liscie z CurrentItem -
///     przypadek z logu Michala) musi ODMOWIC, a sterowanie zostaje.
///   * U3 Ctrl+F5: PRAWDZIWY handler klawiatury w sesji Sonos otwiera ISTNIEJACE
///     okno Konta Sonos. Modyfikator bierzemy ze stanu klawiatury WLASNEGO watku.
///   * U4 KROTKI ODCZYT: wiersz grupy w liscie to krotka nazwa, bez powtorzonej
///     nazwy i technicznych liczb - szczegoly sa w oknie Glosniki i grupy.
///
/// Pomiar ma WLASNE okno (przelacznik --sonos-navigation-ux). Zero HTTP, zero
/// DPAPI, zero konta, zero NVDA, zero audio, zero cudzych okien.
///
/// GRANICA KONTA (uzupelnienie po przegladzie SEC1): U3 prowadzi PRAWDZIWA
/// sciezke Ctrl+F5 -> ShowSonosAccountManager -> EnsureCoordinator ->
/// RestoreOnce. Samo podstawienie prezentera NIE omija tego odtworzenia, wiec
/// TEN SAM wlasciciel konta, ktorego uzywa prawdziwe okno, dostaje syntetyczny
/// magazyn i syntetyczna bramke (wzor: SonosSessionAccountUiTests.Harness).
/// Bez tego RestoreOnce poszedlby do DOMYSLNEGO SonosDpapiCredentialStore na
/// prawdziwej sciezce uzytkownika. Asercja <see cref="Harness.AssertAccountBoundariesAreSynthetic"/>
/// jest sprawdzana PRZED klawiszem, wiec niezastapione fabryki zatrzymuja
/// pomiar BEZ zadnego I/O.
/// </summary>
internal static class SonosNavigationUxTests
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
                checks += MeasureLibraryShowsGroupsAsTargets();
                checks += MeasureFavoritesRefuseSpeakers();
                checks += MeasureCtrlF5OpensExistingAccountWindow();
                checks += MeasureShortGroupRowLabel();
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
            "OK: uklad interfejsu Sonos - Biblioteka pokazuje grupy jako cele, Ulubione odmawiaja "
            + "glosnika, Ctrl+F5 otwiera istniejace Konto Sonos, wiersz grupy ma krotka nazwe "
            + $"({checks} sprawdzeń, WLASNE pokazane okno)");
    }

    // ===== U1: Biblioteka pokazuje grupy jako CELE =====

    private static int MeasureLibraryShowsGroupsAsTargets()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.EnterSonosSession();

        // ISTNIEJACE polecenie Ctrl+L. Zadnego nowego panelu.
        harness.ExecuteCommand(CommandIds.ViewLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

        if (harness.CurrentView != "Biblioteka")
        {
            throw new Exception($"Ctrl+L w sesji Sonos nie ustawiło widoku Biblioteka: {harness.CurrentView}.");
        }

        var labels = harness.RowLabels();
        if (labels.Count != 2)
        {
            throw new Exception(
                $"Biblioteka Sonos ma {labels.Count} wierszy zamiast 2 odczytanych grup. "
                + "Głośniki i grupy nie są dostępnymi celami w Bibliotece.");
        }
        if (!labels.Any(label => label.Contains("Salon", StringComparison.Ordinal))
            || !labels.Any(label => label.Contains("Kuchnia", StringComparison.Ordinal)))
        {
            throw new Exception("Biblioteka nie pokazuje odczytanych grup: " + string.Join(" | ", labels));
        }

        // Zaden glosnik nie zostal RECZNIE dodany do Biblioteki: cele sa
        // dostepne z samego odczytu topologii.
        var sonosItems = harness.SonosSession.Items;
        if (sonosItems.Any(item => item.IsInLibrary))
        {
            throw new Exception("Grupy Sonos dostały trwałą flagę IsInLibrary - to ręczne dodanie do Biblioteki.");
        }

        // ODZIEDZICZONY widok Ulubione nie ma prawa zostac fałszywą lista grup.
        harness.ExecuteCommand(CommandIds.ViewFavorites);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (string.Equals(harness.CurrentView, "Ulubione", StringComparison.Ordinal)
            && harness.RowLabels().Any(label => label.Contains("Salon", StringComparison.Ordinal)))
        {
            throw new Exception("Widok Ulubione pokazuje głośniki Sonos jako materiał.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Sama nawigacja po widokach wysłała polecenie do Sonosa.");
        }
        return 5;
    }

    // ===== U2: Ulubione odmawiaja glosnika =====

    private static int MeasureFavoritesRefuseSpeakers()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.EnterSonosSession();
        harness.ExecuteCommand(CommandIds.ViewLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

        // Przypadek A: uzytkownik ma ZAZNACZONY wiersz grupy.
        harness.SelectRow(0);
        harness.Announcements.Clear();
        harness.ExecuteCommand(CommandIds.ToggleFavorite);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.SonosSession.Items.Any(item => item.IsFavorite))
        {
            throw new Exception("ToggleFavorite dodał głośnik/grupę Sonos do Ulubionych.");
        }
        if (harness.Announcements.Count == 0)
        {
            throw new Exception("Odmowa dodania głośnika do Ulubionych przeszła w ciszy.");
        }

        // Przypadek B z LOGU: lista PUSTA, ale sesja ma CurrentItem. Wtedy
        // ActionItems bierze CurrentItem i guard po zaznaczeniu by nie zadzialal.
        harness.ClearListSelection();
        harness.Announcements.Clear();
        harness.ExecuteCommand(CommandIds.ToggleFavorite);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.SonosSession.Items.Any(item => item.IsFavorite))
        {
            throw new Exception(
                "ToggleFavorite bez zaznaczenia (pusta lista, obecny CurrentItem) dodał głośnik do Ulubionych.");
        }

        // ToggleLibrary nie moze UKRYC celu sterowania.
        harness.SelectRow(0);
        harness.ExecuteCommand(CommandIds.ToggleLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.RowLabels().Count != 2)
        {
            throw new Exception("ToggleLibrary ukrył cel sterowania z Biblioteki Sonos.");
        }

        // STEROWANIE zostaje nietkniete.
        harness.SelectRow(0);
        harness.Pump(window.ActivateSonosGroupForTests("GRUPA-SALON"));
        harness.Pump(window.ExecuteSonosCommandForTests(CommandIds.PlayPause));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.Backend.Commands.Count == 0)
        {
            throw new Exception("Guard Ulubionych zablokował sterowanie odtwarzaniem Sonos.");
        }
        return 5;
    }

    // ===== U3: Ctrl+F5 otwiera ISTNIEJACE okno Konta Sonos =====

    private static int MeasureCtrlF5OpensExistingAccountWindow()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.EnterSonosSession();
        harness.ShowOwnWindow();
        harness.ExecuteCommand(CommandIds.ViewLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        harness.SelectRow(0);

        // PRODUKCYJNY punkt podstawienia POKAZANIA okna: mierzymy, ze powstalo
        // ISTNIEJACE okno konta z przyciskiem Glosniki i grupy, bez ShowDialog
        // na pulpicie.
        var presenter = window.SonosAccountPresenterForTests;
        var shown = 0;
        var hasDevicesButton = false;
        presenter.PresentOverride = accountWindow =>
        {
            shown++;
            hasDevicesButton = accountWindow.FindName("DevicesButton") is Button;
        };

        harness.Announcements.Clear();
        // GRANICA PRZED KLAWISZEM: fixture MUSI mieć odcięty prawdziwy magazyn
        // konta, bo dalej idzie produkcyjne RestoreOnce. Ta asercja nie robi
        // zadnego I/O - pada, gdy fabryki nie sa zastapione.
        harness.AssertAccountBoundariesAreSynthetic();
        harness.PressCtrl(Key.F5);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        // KONTROLKA ODCIECIA: produkcyjne RestoreOnce naprawde poszlo, ale do
        // SYNTETYCZNEGO magazynu w pamieci. Zaden prawdziwy klient Control API
        // nie powstal i zadne logowanie nie ruszylo.
        if (harness.AccountStore.Reads != 1)
        {
            throw new Exception(
                $"Odtworzenie konta odczytało syntetyczny magazyn {harness.AccountStore.Reads} razy zamiast 1 "
                + "- pomiar nie dowodzi, że prawdziwy magazyn DPAPI został odcięty.");
        }
        if (harness.AccountStore.Writes != 0 || harness.AccountStore.Deletes != 0)
        {
            throw new Exception("Ctrl+F5 zapisał albo skasował poświadczenia Sonos.");
        }
        if (harness.Gateway.Calls != 0)
        {
            throw new Exception("Ctrl+F5 ruszył bramkę logowania Sonos bez polecenia użytkownika.");
        }
        if (harness.AccountOwner.ControlApiCreations != 0)
        {
            throw new Exception("Ctrl+F5 utworzył klienta Control API Sonos - to droga do prawdziwego HTTP.");
        }

        if (shown != 1)
        {
            throw new Exception(
                $"Ctrl+F5 w sesji Sonos nie otworzyło okna Konto Sonos (otwarć: {shown}; "
                + "komunikaty: " + string.Join(" | ", harness.Announcements) + ").");
        }
        if (!hasDevicesButton)
        {
            throw new Exception("Okno konta nie ma istniejącego przycisku Głośniki i grupy.");
        }
        if (harness.Announcements.Any(m => m.Contains("nie ma polecenia", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Ctrl+F5 nadal mówi, że nie ma polecenia w bieżącej sesji.");
        }

        // Powrot z okna konta zachowuje miejsce i wybor.
        if (harness.MediaList.SelectedIndex != 0)
        {
            throw new Exception("Powrót z okna konta zgubił zaznaczenie w liście.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Ctrl+F5 wysłał polecenie sterujące do Sonosa.");
        }
        return 9;
    }

    // ===== U4: krotki wiersz grupy =====

    private static int MeasureShortGroupRowLabel()
    {
        using var harness = Harness.Create();
        harness.EnterSonosSession();
        harness.ExecuteCommand(CommandIds.ViewLibrary);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

        var labels = harness.RowLabels();
        var salon = labels.FirstOrDefault(label => label.Contains("Salon", StringComparison.Ordinal))
            ?? throw new Exception("Brak wiersza grupy Salon: " + string.Join(" | ", labels));

        var occurrences = salon.Split("Salon", StringSplitOptions.None).Length - 1;
        if (occurrences > 1)
        {
            throw new Exception($"Wiersz grupy POWTARZA nazwę {occurrences} razy: \"{salon}\".");
        }
        if (salon.Contains("głośników:", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Wiersz grupy niesie techniczne liczby zamiast krótkiej nazwy: \"{salon}\".");
        }
        if (salon.Length > 40)
        {
            throw new Exception($"Wiersz grupy jest długi ({salon.Length} znaków): \"{salon}\".");
        }
        return 3;
    }

    // ===== aparatura =====

    private sealed class Harness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

        private readonly string _directory;
        private readonly Dispatcher _dispatcher;

        private Harness(string directory, MainWindow window, FakeBackend backend, List<string> announcements,
            SonosAccountOwner accountOwner, PamieciowyMagazynKonta accountStore,
            NieuzywanaBramkaLogowania gateway)
        {
            _directory = directory;
            _dispatcher = Dispatcher.CurrentDispatcher;
            Window = window;
            Backend = backend;
            Announcements = announcements;
            AccountOwner = accountOwner;
            AccountStore = accountStore;
            Gateway = gateway;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        /// <summary>TEN SAM wlasciciel konta, ktorego uzywa prawdziwe okno.</summary>
        internal SonosAccountOwner AccountOwner { get; }

        /// <summary>Syntetyczny magazyn w PAMIECI - kwit, ze DPAPI nie bylo czytane.</summary>
        internal PamieciowyMagazynKonta AccountStore { get; }

        internal NieuzywanaBramkaLogowania Gateway { get; }

        /// <summary>
        /// ASERCJA PRZED JAKIMKOLWIEK I/O: oba punkty podstawienia wlasciciela
        /// konta MUSZA byc zastapione, a konto nie moze byc jeszcze obudzone.
        /// Fixture bez tego prowadzilby RestoreOnce do PRAWDZIWEGO magazynu
        /// DPAPI uzytkownika; ta asercja pada BEZ dotkniecia dysku.
        /// </summary>
        internal void AssertAccountBoundariesAreSynthetic()
        {
            if (AccountOwner.StoreFactory is null)
            {
                throw new Exception(
                    "Fixture NIE odciął magazynu konta: StoreFactory właściciela jest pusty, więc "
                    + "produkcyjne RestoreOnce poszłoby do domyślnego SonosDpapiCredentialStore.");
            }
            if (AccountOwner.GatewayFactory is null)
            {
                throw new Exception(
                    "Fixture NIE odciął transportu logowania: GatewayFactory właściciela jest pusty.");
            }
            if (AccountOwner.HasCoordinator)
            {
                throw new Exception("Konto Sonos zostało obudzone przed mierzonym Ctrl+F5.");
            }
            if (AccountStore.Reads != 0 || AccountStore.Writes != 0 || AccountStore.Deletes != 0)
            {
                throw new Exception("Syntetyczny magazyn był już użyty przed mierzonym Ctrl+F5.");
            }
        }

        internal ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal string CurrentView => (string)Field("_currentView")!;

        internal AccessibleMediaController.Core.Sessions.DemoMediaSession SonosSession =>
            Window.SessionsForTests.FindSession("sonos")
            ?? throw new Exception("Nie ma sesji Sonos w prawdziwym oknie.");

        internal static Harness Create()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-nav-" + Guid.NewGuid().ToString("N"));
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

            // ODCIECIE PRAWDZIWEGO MAGAZYNU KONTA. Ctrl+F5 prowadzi produkcyjne
            // ShowSonosAccountManager -> EnsureCoordinator -> RestoreOnce, ktore
            // BEZ tego poszloby do domyslnego SonosDpapiCredentialStore na
            // prawdziwej sciezce uzytkownika. Bierzemy TEGO SAMEGO wlasciciela,
            // ktorego uzywa prawdziwe okno (wzor: SonosSessionAccountUiTests),
            // i podstawiamy magazyn w PAMIECI oraz bramke, ktora ma nie ruszyc.
            var owner = (SonosAccountOwner)(window.GetType().GetField("_sonosAccount", Instance)
                ?? throw new Exception("Nie ma pola _sonosAccount w prawdziwym MainWindow."))
                .GetValue(window)!;
            if (owner.HasCoordinator)
            {
                throw new Exception("Konstrukcja okna zainicjowała konto Sonos przed jawnym wejściem.");
            }

            var accountStore = new PamieciowyMagazynKonta();
            var gateway = new NieuzywanaBramkaLogowania();
            owner.StoreFactory = _ => accountStore;
            owner.GatewayFactory = _ => gateway;
            // Gdyby ktorakolwiek sciezka tego pomiaru dotknela Control API,
            // wartownik ma to ZATRZYMAC zamiast wypuscic prawdziwy HTTP.
            owner.ControlApiConfigurationFactory = () =>
                throw new Exception(
                    "Pomiar układu nawigacji nie ma prawa tworzyć klienta Control API Sonos.");

            return new Harness(directory, window, backend, announcements, owner, accountStore, gateway);
        }

        internal void EnterSonosSession()
        {
            var slot = Window.SessionsForTests.SessionSlots
                .First(pair => string.Equals(pair.Value, "sonos", StringComparison.Ordinal)).Key;
            ExecuteCommand(CommandIds.SessionSlot(slot));
            PumpUntil(() => Window.SonosGroupRows.Count == 2, "wejście do sesji Sonos nie odczytało grup");
            PumpQuietly(TimeSpan.FromMilliseconds(120));
        }

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

        internal List<string> RowLabels() => MediaList.Items.Cast<object>()
            .Select(row => row.GetType().GetProperty("Label", Instance)?.GetValue(row) as string ?? string.Empty)
            .ToList();

        internal void SelectRow(int index)
        {
            var list = MediaList;
            if (list.Items.Count <= index) throw new Exception($"Lista nie ma wiersza {index}.");
            list.SelectedIndex = index;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
        }

        internal void ClearListSelection()
        {
            MediaList.SelectedIndex = -1;
            MediaList.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
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

        /// <summary>
        /// PRAWDZIWY handler klawiatury okna z MODYFIKATOREM. Stan klawiatury
        /// WLASNEGO watku ustawiamy jawnie: KeyEventArgs bez tego nie jest Ctrl+F5.
        /// </summary>
        internal void PressCtrl(Key key)
        {
            var source = PresentationSource.FromVisual(Window)
                ?? throw new Exception("Okno nie ma powierzchni prezentacji; pokaż je przed klawiszem.");
            var previous = new byte[256];
            if (!GetKeyboardState(previous)) throw new Exception("Nie da się odczytać stanu klawiatury wątku.");
            var keys = new byte[256];
            keys[0x11] = 0x80;
            keys[0xA2] = 0x80;
            try
            {
                if (!SetKeyboardState(keys)) throw new Exception("Nie da się ustawić stanu klawiatury wątku.");
                if (Keyboard.Modifiers != ModifierKeys.Control)
                {
                    throw new Exception("Stan wątku nie dał modyfikatora Control; pomiar nie byłby Ctrl+F5.");
                }
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                var handler = Window.GetType().GetMethod("Window_PreviewKeyDown", Instance)
                    ?? throw new Exception("Nie ma prawdziwego handlera Window_PreviewKeyDown.");
                try
                {
                    handler.Invoke(Window, [Window, args]);
                }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                {
                    throw exception.InnerException;
                }
            }
            finally
            {
                SetKeyboardState(previous);
            }
            PumpQuietly(TimeSpan.FromMilliseconds(80));
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

        internal void Pump<T>(Task<T> task) => Pump((Task)task);

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

        public void Dispose()
        {
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

        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keys);

        [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] keys);
    }

    /// <summary>SYNTETYCZNA granica API: zero HTTP, zero tokenu, zero magazynu.</summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var status = new SonosGroupPlaybackStatus(
                SonosPlaybackState.Playing, null, null, "UTWOR-1", 12_000, null, null, null, _actions);
            return Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status));
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var track = new SonosTrackMetadata(
                "track", "Preludium", "Chopin", "Nokturny", null,
                new SonosMetadataService("Sonos Radio", "9"), 180_000);
            var metadata = new SonosGroupMetadata(
                null, new SonosQueueItem("UTWOR-1", track, null), null, null, null);
            return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(metadata));
        }

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken) =>
            Task.FromResult(
                SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));

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
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
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

    /// <summary>
    /// Magazyn konta w PAMIECI: oddaje jeden syntetyczny zestaw i liczy
    /// operacje. Nic nie dotyka DPAPI ani dysku uzytkownika, wiec produkcyjne
    /// RestoreOnce z Ctrl+F5 nie ma jak przeczytac prawdziwego sekretu.
    /// Tokeny sa SYNTETYCZNE i zyja tylko w pamieci procesu pomiaru.
    /// </summary>
    private sealed class PamieciowyMagazynKonta : ISonosCredentialStore
    {
        internal int Reads;
        internal int Writes;
        internal int Deletes;

        public SonosCredentialReadOutcome Read()
        {
            Reads++;
            var tokens = new SonosTokens(
                "ACCESS-SYNTETYCZNY", "Bearer", 3600, "RT-SYNTETYCZNY", "playback-control-all");
            return SonosCredentialReadOutcome.Ok(
                new SonosStoredCredentials("https://broker-testowy.invalid/", tokens, DateTimeOffset.UtcNow));
        }

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            return true;
        }
    }

    /// <summary>
    /// Bramka logowania, ktora ma NIE zostac zawolana: ten pomiar nie loguje sie
    /// i nie otwiera przegladarki. Kazde wywolanie zatrzymuje pomiar.
    /// </summary>
    private sealed class NieuzywanaBramkaLogowania : ISonosLoginGateway
    {
        internal int Calls;

        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar układu nawigacji nie ma prawa rozpoczynać logowania Sonos.");
        }

        public Task<SonosLoginResultOutcome> FetchResultAsync(
            SonosLoginSession session, CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar układu nawigacji nie ma prawa odbierać wyniku logowania Sonos.");
        }

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            throw new Exception("Pomiar układu nawigacji nie ma prawa odnawiać dostępu Sonos.");
        }
    }
}
