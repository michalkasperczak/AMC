using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// F2: DOSTEPNY PODGLAD ULUBIONYCH Sonos mierzony RZECZYWISTA droga uzytkownika.
///
/// Pomiar idzie przez prawdziwe <c>ExecuteCommand(CommandIds.ViewFavorites)</c>
/// prawdziwego <see cref="MainWindow"/>, a zaplecze jest SYNTETYCZNE: zero HTTP,
/// zero tokenu, zero prawdziwego konta i zero audio.
///
/// UCZCIWA GRANICA: to NIE jest dowod, ze NVDA cokolwiek wypowiedzial -
/// komunikaty zbieramy z <c>AnnouncementSinkForTests</c>, czyli z tresci ODDANEJ
/// czytnikowi.
///
/// TEN PLIK JEST NAJPIERW CZERWONY: w bazie 3c46a80 nie ma ani polecenia
/// otwierajacego liste ulubionych w sesji Sonos, ani okna listy. Wejscia, ktore
/// dopiero maja powstac, sa tu siegane REFLEKSJA - inaczej plik nie skompilowal
/// by sie na bazie i nie dalby sie zmierzyc jako RED.
/// </summary>
internal static class SonosFavoritesUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                void Stage(string name) => Console.Error.WriteLine("ETAP: " + name);
                Stage("F1"); checks += MeasureViewFavoritesOpensFreshListInSonosSession();
                Stage("KONIEC-STA");
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
        if (!thread.Join(TimeSpan.FromSeconds(100)))
        {
            throw new Exception("Limit czasu pomiaru ulubionych Sonos: patrz ostatni ETAP na stderr.");
        }

        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: podgląd ulubionych Sonos - istniejące Pokaż ulubione otwiera świeżą listę "
            + $"bieżącego domu ({checks} sprawdzeń)");
    }

    // ===== F1: Pokaz ulubione w sesji Sonos czyta SWIEZA liste i otwiera okno =====

    private static int MeasureViewFavoritesOpensFreshListInSonosSession()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"));
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";

        var window = harness.RunFavorites();
        if (window is null) throw new Exception("Pokaż ulubione nie otworzyło okna ulubionych Sonos.");
        if (harness.Backend.FavoriteReadsForTests != 1)
        {
            throw new Exception(
                $"Zaplecze dostało {harness.Backend.FavoriteReadsForTests} odczytów ulubionych zamiast 1.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Samo otwarcie podglądu ulubionych wysłało polecenie do Sonosa.");
        }
        return 3;
    }

    // ==================== harness ====================

    private sealed class Harness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

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

        internal static Harness Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-favorites-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = new ConfigurationStore(settingsPath);
            var state = store.LoadOrCreate();
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
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

        internal void EnterSonosSession()
        {
            ExecuteCommand(CommandIds.SessionSlot(8));
            PumpQuietly(TimeSpan.FromMilliseconds(120));
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
        /// PRAWDZIWA droga: istniejace polecenie "Pokaż ulubione". Punkt
        /// podstawienia POKAZANIA okna i samo okno siegane REFLEKSJA, bo w bazie
        /// jeszcze nie istnieja - to jest wlasnie mierzone wejscie.
        /// </summary>
        internal object? RunFavorites()
        {
            var overrideProperty = Window.GetType().GetProperty("PresentSonosFavoritesOverrideForTests", Instance)
                ?? throw new Exception(
                    "MainWindow nie ma punktu podstawienia pokazania okna ulubionych Sonos "
                    + "(PresentSonosFavoritesOverrideForTests).");
            var windowType = typeof(MainWindow).Assembly.GetType(
                "AccessibleMediaController.Windows.SonosFavoritesWindow")
                ?? throw new Exception("Nie ma okna SonosFavoritesWindow.");

            object? captured = null;
            var handlerType = typeof(Action<>).MakeGenericType(windowType);
            var handler = Delegate.CreateDelegate(
                handlerType,
                new Captor(this, dialog => captured = dialog),
                typeof(Captor).GetMethod(nameof(Captor.Present), Instance)!);
            overrideProperty.SetValue(Window, handler);
            try
            {
                ExecuteCommand(CommandIds.ViewFavorites);
                if (Window.GetType().GetProperty("LastSonosFavoritesTaskForTests", Instance)?
                        .GetValue(Window) is Task task)
                {
                    Pump(task);
                }
                else
                {
                    throw new Exception("Polecenie nie rozpoczęło zadania odczytu ulubionych Sonos.");
                }
            }
            finally
            {
                overrideProperty.SetValue(Window, null);
            }

            return captured;
        }

        /// <summary>
        /// ADAPTER pokazania: pokazujemy okno przez Show i pompujemy petle, bo
        /// ShowDialog zablokowalby watek pomiaru. To ten sam typ, ten sam XAML.
        /// </summary>
        private sealed class Captor(Harness harness, Action<object> capture)
        {
            internal void Present(object dialog)
            {
                capture(dialog);
                var window = (Window)dialog;
                window.ShowInTaskbar = false;
                window.Show();
                harness.PumpUntil(() => window.IsLoaded, "okno ulubionych Sonos się nie pokazało");
                window.Close();
                harness.PumpUntil(() => !window.IsVisible, "okno ulubionych Sonos się nie zamknęło");
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

        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: zadanie ulubionych Sonos.");
                DoEvents();
            }

            task.GetAwaiter().GetResult();
        }

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        public void Dispose()
        {
            try
            {
                Window.Close();
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// SYNTETYCZNE zaplecze sesji: grupy jak w istniejacych pomiarach Sonosa,
    /// plus LICZNIK odczytow ULUBIONYCH. Odczyt ulubionych jest tu wolany
    /// REFLEKSJA przez produkt (nowa, opcjonalna granica), wiec licznik rosnie
    /// tylko wtedy, gdy produkt NAPRAWDE po niego poszedl.
    /// </summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        private (string Id, string Name)[] _households = [("DOM-1", "Dom")];

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal int FavoriteReadsForTests { get; private set; }

        internal void SetHouseholds(params (string Id, string Name)[] households) => _households = households;

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        /// <summary>
        /// ODCZYT ULUBIONYCH. Podpis musi zgadzac sie z nowa granica produktu;
        /// w RED nikt tego nie wola, bo produkt jeszcze nie ma tej drogi.
        /// </summary>
        public Task<SonosFavoritesReadResult> ReadFavoritesAsync(
            string? householdId, CancellationToken cancellationToken)
        {
            FavoriteReadsForTests++;
            LastFavoritesHouseholdId = householdId;
            throw new NotSupportedException("RED: zaplecze ulubionych jeszcze nie jest wpięte.");
        }

        internal string? LastFavoritesHouseholdId { get; private set; }

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
            Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SeekRelative);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
        }

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosHouseholdsReadResult.Success(
                _households.Select(home => new SonosHousehold(home.Id, home.Name, null)).ToArray()));

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Idle)],
                [new SonosPlayer("P1", "Salon", null, null, null)],
                false)));
    }
}
