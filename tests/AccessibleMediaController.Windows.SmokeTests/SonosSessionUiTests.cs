using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// SESJA SONOS mierzona na PRAWDZIWYM <see cref="MainWindow"/> i PRAWDZIWYM
/// <c>ExecuteCommand</c>, przez SYNTETYCZNA granice API/transportu
/// (<see cref="ISonosGroupSessionBackend"/>). Zero sieci, zero konta, zero
/// DPAPI i zero GUI: okno jest KONSTRUOWANE, nigdy pokazywane.
///
/// Co jest tu naprawde dowiedzione:
///   * sesja "sonos" ISTNIEJE w prawdziwym SessionManagerze i jej lista to
///     GRUPY, nie material demonstracyjny,
///   * Enter na grupie czyni ja AKTYWNA i NIE wysyla zadnego polecenia,
///   * ApplyPlaybackPolicyWhenLeavingPlayer nie robi NIC dla Sonosa (zero POST
///     przy wyjsciu, zmianie sesji i zamykaniu),
///   * ExecuteCommand(PlayPause) idzie przez grupe: JEDEN POST i JAWNE odczyty,
///   * bramka niedostepnego polecenia ODMAWIA bez POST,
///   * jeden przelot naraz: drugie polecenie jest odrzucone, nie kolejkowane,
///   * zniknieta grupa NIE jest po cichu zastepowana inna.
///
/// Czego NIE dowodzi: nie ma tu odsluchu NVDA, prawdziwej klawiatury ani
/// pokazanego okna - to osobna czynnosc wlasciciela pulpitu.
/// </summary>
internal static class SonosSessionUiTests
{
    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasureSessionListShowsGroups();
                checks += MeasureActivateGroupSendsNothing();
                checks += MeasureLeavingPlayerNeverStops();
                checks += MeasurePlayPauseGoesThroughGroupCommand();
                checks += MeasureUnavailableCommandRefusedWithoutPost();
                checks += MeasureSingleFlightRefusesSecondCommand();
                checks += MeasureVanishedGroupIsNotSilentlyReplaced();
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
            "OK: sesja Sonos na prawdziwym MainWindow - lista grup, aktywna grupa bez POST, "
            + "wyjscie nie zatrzymuje muzyki, polecenia przez grupe z jawnym odczytem "
            + $"({checks} sprawdzeń, bez pokazywania GUI)");
    }

    // ==================== 1. lista sesji to GRUPY ====================

    private static int MeasureSessionListShowsGroups()
    {
        using var harness = Harness.Create();
        var session = harness.Window.SessionsForTests.FindSession("sonos")
            ?? throw new Exception("Sesja Sonos nie istnieje w prawdziwym SessionManagerze.");
        if (session.Items.Count != 0)
        {
            throw new Exception("Sesja Sonos ma materiał przed odczytem konta.");
        }

        harness.Pump(harness.Window.EnterSonosSessionAsync());
        var rows = harness.Window.SonosGroupRows;
        if (rows.Count != 2 || rows[0].GroupId != "GRUPA-SALON" || rows[1].GroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception("Lista sesji Sonos nie pokazuje odczytanych grup.");
        }
        if (!rows[0].Text.Contains("Salon", StringComparison.Ordinal)
            || rows[0].Text.Contains("RINCON", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Wiersz grupy nie jest czytelny albo wypuszcza surowy identyfikator.");
        }
        if (session.Items.Any(item => item.Kind != MediaItemKind.Device))
        {
            throw new Exception("Sesja Sonos dostała odtwarzalny materiał zamiast grup.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Samo wejście do sesji wysłało polecenie.");
        }

        // Pusty stan ma DROGE DO KONTA, nie sama cisze.
        var empty = SonosSessionListPresentation.DescribeEmptyState(SonosSessionEmptyReason.NoAccount);
        if (!empty.Contains("konto", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Pusty stan bez konta nie prowadzi do konta.");
        }
        return 5;
    }

    // ============ 2. wybor grupy NIE zmienia muzyki ============

    private static int MeasureActivateGroupSendsNothing()
    {
        using var harness = Harness.Create();
        harness.Pump(harness.Window.EnterSonosSessionAsync());
        harness.Pump(harness.Window.ActivateSonosGroupAsync("GRUPA-KUCHNIA"));

        if (harness.Window.SonosActiveGroup?.Id != "GRUPA-KUCHNIA")
        {
            throw new Exception("Wybór grupy nie uczynił jej aktywną.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Sam WYBÓR grupy wysłał polecenie do Sonosa.");
        }
        // Wybor jest zapamietany PO IDENTYFIKATORZE i bez tokenow.
        if (harness.Window.SonosSelectedGroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception("Wybór grupy nie trafił do ustawień.");
        }
        if (harness.Backend.PlaybackReads == 0 || harness.Backend.VolumeReads == 0)
        {
            throw new Exception("Aktywacja grupy nie zrobiła jawnego odczytu stanu.");
        }
        return 4;
    }

    // ======= 3. wyjscie z odtwarzacza NIE zatrzymuje muzyki =======

    private static int MeasureLeavingPlayerNeverStops()
    {
        using var harness = Harness.Create();
        harness.Pump(harness.Window.EnterSonosSessionAsync());
        harness.Pump(harness.Window.ActivateSonosGroupAsync("GRUPA-SALON"));
        var commandsBefore = harness.Backend.Commands.Count;

        var session = harness.Window.SessionsForTests.FindSession("sonos")!;
        foreach (var reason in Enum.GetValues<PlayerDepartureReason>())
        {
            harness.Window.ApplyPlaybackPolicyWhenLeavingPlayerForTests(session, reason);
        }

        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Wyjście z odtwarzacza Sonos wysłało polecenie transportu.");
        }
        if (session.IsPlaying)
        {
            throw new Exception("Sesja Sonos udaje własne odtwarzanie AMC.");
        }
        return 2;
    }

    // ========== 4. play/pause przez POLECENIE GRUPY ==========

    private static int MeasurePlayPauseGoesThroughGroupCommand()
    {
        using var harness = Harness.Create();
        harness.Pump(harness.Window.EnterSonosSessionAsync());
        harness.Pump(harness.Window.ActivateSonosGroupAsync("GRUPA-SALON"));
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;

        harness.Pump(harness.Window.ExecuteSonosCommandAsync(CommandIds.PlayPause));

        if (harness.Backend.Commands.Count != 1
            || harness.Backend.Commands[0] != SonosGroupCommand.TogglePlayPause)
        {
            throw new Exception("PlayPause nie poszedł jako jedno polecenie togglePlayPause grupy.");
        }
        if (harness.Backend.CommandGroupIds[0] != "GRUPA-SALON")
        {
            throw new Exception("Polecenie poszło do innej grupy niż aktywna.");
        }
        // Po poleceniu MUSI byc jawny odczyt stanu - 200 to nie wykonanie.
        if (harness.Backend.PlaybackReads < 2)
        {
            throw new Exception("Po poleceniu nie było jawnego odczytu stanu.");
        }
        if (!harness.Announcements.Any(text => text.Contains("Pauza", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Werdykt nie opiera się na odczytanym stanie.");
        }
        return 4;
    }

    // ====== 5. niedostepne polecenie: ODMOWA bez POST ======

    private static int MeasureUnavailableCommandRefusedWithoutPost()
    {
        using var harness = Harness.Create();
        // Grupa bez prawa pomijania: availablePlaybackActions.canSkip = false.
        harness.Backend.Actions = new SonosPlaybackActions(
            canPlay: true, canSkip: false, canSkipBack: false, canSkipToPrevious: false,
            canSeek: false, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);
        harness.Pump(harness.Window.EnterSonosSessionAsync());
        harness.Pump(harness.Window.ActivateSonosGroupAsync("GRUPA-SALON"));

        harness.Pump(harness.Window.ExecuteSonosCommandAsync(CommandIds.Next));
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Niedostępne polecenie mimo to wysłało POST.");
        }

        harness.Pump(harness.Window.ExecuteSonosCommandAsync(CommandIds.SeekForward30));
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Przewijanie bez canSeek mimo to wysłało POST.");
        }
        if (!harness.Announcements.Any(text => text.Contains("nie zgłasza", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Odmowa nie jest czytelna.");
        }
        return 3;
    }

    // ====== 6. JEDEN przelot naraz, bez ukrytej kolejki ======

    private static int MeasureSingleFlightRefusesSecondCommand()
    {
        using var harness = Harness.Create();
        harness.Pump(harness.Window.EnterSonosSessionAsync());
        harness.Pump(harness.Window.ActivateSonosGroupAsync("GRUPA-SALON"));

        var release = new TaskCompletionSource();
        harness.Backend.CommandGate = release.Task;
        var first = harness.Window.ExecuteSonosCommandAsync(CommandIds.PlayPause);
        harness.PumpUntil(() => harness.Backend.Commands.Count == 1);

        var second = harness.Window.ExecuteSonosCommandAsync(CommandIds.PlayPause);
        harness.Pump(second);
        if (harness.Backend.Commands.Count != 1)
        {
            throw new Exception("Drugie polecenie weszło mimo trwającego przelotu.");
        }
        if (!harness.Announcements.Any(text =>
                text.Contains("jeszcze się nie zakończyło", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Zajętość nie została zgłoszona; wygląda na cichą kolejkę.");
        }

        release.SetResult();
        harness.Pump(first);
        if (harness.Backend.Commands.Count != 1)
        {
            throw new Exception("Odrzucone polecenie zostało jednak wysłane później.");
        }
        return 3;
    }

    // ====== 7. zniknieta grupa nie jest podmieniana po cichu ======

    private static int MeasureVanishedGroupIsNotSilentlyReplaced()
    {
        using var harness = Harness.Create();
        harness.Pump(harness.Window.EnterSonosSessionAsync());
        harness.Pump(harness.Window.ActivateSonosGroupAsync("GRUPA-KUCHNIA"));

        harness.Window.ApplySonosTopology(new SonosHouseholdTopology(
            [new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Playing)],
            [new SonosPlayer("P1", "Salon", null, null, null)],
            false));

        if (harness.Window.SonosActiveGroup is not null)
        {
            throw new Exception("Po zniknięciu grupy sterowanie po cichu przeszło na inną.");
        }
        if (harness.Window.SonosSelectedGroupId is not null)
        {
            throw new Exception("Zapamiętany wybór wskazuje nieistniejącą grupę.");
        }

        harness.Pump(harness.Window.ExecuteSonosCommandAsync(CommandIds.PlayPause));
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Polecenie poszło mimo braku aktywnej grupy.");
        }
        return 3;
    }

    // ==================== harness ====================

    private sealed class Harness : IDisposable
    {
        private readonly string _directory;

        private Harness(string directory, MainWindow window, FakeBackend backend, List<string> announcements)
        {
            _directory = directory;
            Window = window;
            Backend = backend;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        internal static Harness Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = store.LoadOrCreate();
            // Zadnych kont, podcastow, urzadzen i harmonogramow; aktualizacje wylaczone.
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

        internal void Pump(Task task)
        {
            var frame = new DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (frame.Continue && DateTime.UtcNow < deadline)
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(1);
            }

            if (task.IsFaulted) throw task.Exception!.GetBaseException();
        }

        internal void PumpUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                Thread.Sleep(1);
            }
        }

        public void Dispose()
        {
            Window.CancelSonosPendingWork();
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
    /// SYNTETYCZNA granica API. Nie ma tu HttpClient, tokenu ani magazynu, wiec
    /// zaden pomiar nie puka do Sonosa i nie dotyka poswiadczen.
    /// </summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int VolumeReads { get; private set; }

        internal SonosPlaybackState NextPlaybackState { get; set; } = SonosPlaybackState.Playing;

        internal SonosPlaybackActions? Actions { get; set; } = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        internal Task? CommandGate { get; set; }

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReads++;
            var status = new SonosGroupPlaybackStatus(
                NextPlaybackState, null, null, "UTWOR-1", 12_000, null, null, null, Actions);
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
            string? groupId, CancellationToken cancellationToken)
        {
            VolumeReads++;
            return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));
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
            Task.FromResult(SonosHouseholdsReadResult.Success(
                [new SonosHousehold("DOM-1", "Dom", null)]));

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
