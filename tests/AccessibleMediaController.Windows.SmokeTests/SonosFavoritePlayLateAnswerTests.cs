using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// F3c TRIAGE, etap B: SPOZNIONY WYNIK. Mierzy PRODUKCYJNA droge
/// <see cref="MainWindow"/> - prawdziwe polecenie "Pokaż ulubione", PRAWDZIWY
/// modal <c>ShowDialog</c> z produkcyjnym wlascicielem, PRAWDZIWY Enter na
/// liscie i PRAWDZIWY Escape - a zaplecze <c>loadFavorite</c> jest WSTRZYMANE,
/// zeby odpowiedz przyszla DOPIERO PO ZAMKNIECIU okna.
///
/// Kazdy przypadek zwraca WERDYKT zamiast rzucac od razu: jeden przelot ma
/// rozstrzygnac WSZYSTKIE hipotezy, a nie zatrzymac sie na pierwszej.
/// </summary>
internal static class SonosFavoritePlayLateAnswerTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal sealed record Verdict(string Case, string Hypothesis, bool Passed, string Detail);

    internal static List<Verdict> RunAll()
    {
        var verdicts = new List<Verdict>
        {
            Measure("P6", "Enter z prawdziwego modalu wysyła JEDEN loadFavorite: dokładna grupa, "
                + "dokładny identyfikator, Insert, playOnCompletion=true", MeasureRealEnterSendsExactInsert),
            Measure("P7", "Po Escape podczas trwającego POST spóźniona odpowiedź NIE mówi w oknie głównym "
                + "i NIE rusza fokusu", MeasureLateAnswerAfterEscapeStaysSilent),
            Measure("P8", "Spóźniona odpowiedź A NIE mówi w NOWO otwartym oknie B", MeasureLateAnswerDoesNotSpeakInB)
        };

        return verdicts;
    }

    private static Verdict Measure(string name, string hypothesis, Func<string> body)
    {
        Console.Error.WriteLine("ETAP: " + name);
        try
        {
            return new Verdict(name, hypothesis, true, body());
        }
        catch (Exception exception)
        {
            return new Verdict(name, hypothesis, false, exception.Message);
        }
    }

    // ===== P6: PRAWDZIWY Enter w PRAWDZIWYM modalu -> JEDEN jawny Insert =====

    private static string MeasureRealEnterSendsExactInsert()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"), ("ULU-7", "Poranek"));
        harness.Enter();

        harness.RunFavoritesModal(window =>
        {
            window.SelectForTests(1);
            window.PressEnterForTests();
            window.AwaitPlayForTests();
            window.PressEscapeForTests();
        });

        var posts = harness.Backend.Loads;
        if (posts.Count != 1) throw new Exception($"Enter wysłał {posts.Count} POST zamiast 1.");
        var post = posts[0];
        if (post.GroupId != "GRUPA-SALON") throw new Exception("POST poszedł do grupy \"" + post.GroupId + "\".");
        if (post.FavoriteId != "ULU-7")
        {
            throw new Exception("POST wysłał ulubione \"" + post.FavoriteId + "\" zamiast zaznaczonego ULU-7.");
        }
        if (post.Action != SonosFavoriteQueueAction.Insert)
        {
            throw new Exception("Akcja kolejki to " + post.Action + ", nie JAWNY Insert.");
        }
        if (!post.PlayOnCompletion) throw new Exception("playOnCompletion nie jest true.");
        if (harness.Window.SonosFavoriteLoadsSentForTests != 1)
        {
            throw new Exception($"Produkt policzył {harness.Window.SonosFavoriteLoadsSentForTests} wysłań zamiast 1.");
        }
        if (harness.Backend.Commands.Contains(SonosGroupCommand.Play))
        {
            throw new Exception("Po loadFavorite poszło DODATKOWE polecenie Play.");
        }

        return "1 POST: GRUPA-SALON/ULU-7, Insert, playOnCompletion=true, zero dodatkowego Play";
    }

    // ===== P7: SPOZNIONA odpowiedz po ESCAPE - cisza i zero ruchu fokusu =====

    private static string MeasureLateAnswerAfterEscapeStaysSilent()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        harness.Enter();

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = held.Task;
        var startedInFlight = false;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                startedInFlight = harness.Backend.Loads.Count == 1;
                // ZAMKNIECIE PODCZAS trwajacego POST: prawdziwy Escape.
                window.PressEscapeForTests();
            });

            if (!startedInFlight)
            {
                throw new Exception($"POST nie ruszył przed Escape ({harness.Backend.Loads.Count}), nic nie mierzymy.");
            }
            if (harness.Window.OpenSonosFavoritesWindowForTests is not null)
            {
                throw new Exception("Okno ulubionych nadal jest zapamiętane jako otwarte po Escape.");
            }

            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            var spokenBefore = harness.Announcements.Count;
            var focusBefore = Keyboard.FocusedElement;
            var childWindowsBefore = harness.VisibleOwnedWindows();

            // SPOZNIONA ODPOWIEDZ: dopiero TERAZ, gdy okna juz nie ma.
            held.TrySetResult();
            harness.DrainFavoriteLoad();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

            var late = harness.Announcements.Skip(spokenBefore).ToArray();
            if (late.Length != 0)
            {
                throw new Exception($"Spóźniona odpowiedź ogłosiła {late.Length} komunikat(ów) w oknie głównym: \""
                    + string.Join(" | ", late) + "\".");
            }
            if (!ReferenceEquals(Keyboard.FocusedElement, focusBefore))
            {
                throw new Exception("Spóźniona odpowiedź ruszyła fokus na " + Describe(Keyboard.FocusedElement) + ".");
            }
            if (harness.VisibleOwnedWindows() != childWindowsBefore)
            {
                throw new Exception("Spóźniona odpowiedź pokazała okno potomne.");
            }

            return "cisza po zamknięciu, fokus nieruszony";
        }
        finally
        {
            held.TrySetResult();
            harness.DrainFavoriteLoad();
        }
    }

    // ===== P8: SPOZNIONA odpowiedz A a NOWO otwarte okno B =====

    private static string MeasureLateAnswerDoesNotSpeakInB()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        harness.Enter();

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = held.Task;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                window.PressEscapeForTests();
            });
            if (harness.Backend.Loads.Count != 1) throw new Exception("POST A nie ruszył, nic nie mierzymy.");

            harness.ReactivateOwnWindow();
            string[] lateInB = [];
            // OKNO B otwarte, gdy A NADAL wisi. Spozniona odpowiedz A przychodzi
            // w trakcie zycia B: nie ma prawa mowic w CUDZYM oknie.
            harness.RunFavoritesModal(windowB =>
            {
                var before = windowB.StatusForTests;
                held.TrySetResult();
                windowB.PumpForTests(TimeSpan.FromMilliseconds(250));
                var after = windowB.StatusForTests;
                lateInB = string.Equals(before, after, StringComparison.Ordinal) ? [] : [after];
                windowB.PressEscapeForTests();
            });

            if (lateInB.Length != 0)
            {
                throw new Exception("Spóźniona odpowiedź A odezwała się w oknie B: \"" + lateInB[0] + "\".");
            }
            if (harness.Backend.Loads.Count != 1)
            {
                throw new Exception($"Otwarcie B wysłało dodatkowy POST ({harness.Backend.Loads.Count}).");
            }

            return "okno B nie dostało komunikatu porzuconej próby A";
        }
        finally
        {
            held.TrySetResult();
            harness.DrainFavoriteLoad();
        }
    }

    private static string Describe(IInputElement? element) => element switch
    {
        null => "(brak)",
        FrameworkElement named when !string.IsNullOrEmpty(named.Name) =>
            named.GetType().Name + " \"" + named.Name + "\"",
        _ => element.GetType().Name
    };

    // ==================== aparatura okna glownego ====================

    private sealed class MainHarness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(25);
        private readonly string _directory;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        private MainHarness(string directory, MainWindow window, LoadFakeBackend backend, List<string> announcements)
        {
            _directory = directory;
            Window = window;
            Backend = backend;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal LoadFakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        private ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal static MainHarness Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "amc-f3c-late-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = store.LoadOrCreate();
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            state.Podcasts.Subscriptions.Clear();
            state.Podcasts.Episodes.Clear();
            state.WiiM.Devices.Clear();
            state.Radio.RecordingSchedules.Clear();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;

            var backend = new LoadFakeBackend();
            var announcements = new List<string>();
            var window = new MainWindow(state, store)
            {
                SuppressDesktopIntegrationForTests = true,
                SonosBackendOverride = backend,
                AnnouncementSinkForTests = announcements.Add
            };
            window.DenyApplicationUpdateStartForTests();
            return new MainHarness(directory, window, backend, announcements);
        }

        /// <summary>WEJSCIE w sesje Sonos z AKTYWNA grupa - produkcyjna droga.</summary>
        internal void Enter()
        {
            var rendered = (EventHandler)Delegate.CreateDelegate(
                typeof(EventHandler), Window, Window.GetType().GetMethod("Window_ContentRendered", Instance)!);
            Window.ContentRendered -= rendered;
            Window.ShowInTaskbar = false;
            Window.Show();
            PumpUntil(() => Window.IsLoaded && PresentationSource.FromVisual(Window) is not null,
                "okno główne się nie pokazało");
            foreach (var field in new[] { "_nvdaCommandServer", "_prefixService" })
            {
                if (Window.GetType().GetField(field, Instance)?.GetValue(Window) is not null)
                {
                    throw new Exception("Fixture wystartował produkcyjną usługę pulpitu: " + field);
                }
            }

            Window.Activate();
            PumpUntil(() => Window.IsActive, "okno główne nie stało się aktywne");
            var sessions = (SessionManagerLike)new SessionManagerLike(Window);
            ExecuteCommand(CommandIds.SessionSlot(sessions.Slot("sonos")));
            PumpUntil(() => MediaList.Items.Count >= 1, "sesja Sonos nie pokazała grupy");
            Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
            Pump(Window.ActivateSonosGroupForTests("GRUPA-SALON"));
            if (Window.SonosActiveGroup is null) throw new Exception("Aktywna grupa Sonos nie została ustawiona.");
            MediaList.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(80));
        }

        /// <summary>
        /// PRAWDZIWY modal: produkcyjne polecenie tworzy okno, punkt podstawienia
        /// wola <c>ShowDialog</c> u produkcyjnego wlasciciela, a KROKI pomiaru
        /// jada z timera W PETLI TEGO modalu - inaczej nikt by ich nie wykonal.
        /// </summary>
        internal void RunFavoritesModal(Action<SonosFavoritesWindow> steps)
        {
            Exception? inside = null;
            Window.PresentSonosFavoritesOverrideForTests = dialog =>
            {
                dialog.ShowInTaskbar = false;
                var timer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(25)
                };
                timer.Tick += (_, _) =>
                {
                    if (!dialog.IsLoaded || !dialog.IsVisible) return;
                    timer.Stop();
                    try
                    {
                        steps(dialog);
                    }
                    catch (Exception exception)
                    {
                        inside = exception;
                    }
                    finally
                    {
                        if (dialog.IsVisible)
                        {
                            try { dialog.Close(); } catch (InvalidOperationException) { }
                        }
                    }
                };
                timer.Start();
                try
                {
                    dialog.ShowDialog();
                }
                finally
                {
                    timer.Stop();
                }
            };
            try
            {
                ExecuteCommand(CommandIds.ViewFavorites);
                Pump(Window.LastSonosFavoritesTaskForTests
                    ?? throw new Exception("Polecenie nie rozpoczęło odczytu ulubionych."));
            }
            finally
            {
                Window.PresentSonosFavoritesOverrideForTests = null;
            }

            if (inside is not null) throw inside;
        }

        internal int VisibleOwnedWindows() => Window.OwnedWindows.OfType<Window>().Count(child => child.IsVisible);

        internal void ReactivateOwnWindow()
        {
            Window.Activate();
            PumpUntil(() => Window.IsActive, "okno główne nie wróciło do aktywności");
        }

        /// <summary>DOMKNIECIE spoznionego przelotu: czekamy na ZADANIE, nie na zegar.</summary>
        internal void DrainFavoriteLoad()
        {
            var deadline = DateTime.UtcNow + Limit;
            while (Window.SonosFavoriteLoadsSentForTests > 0
                && Backend.OutstandingLoads > 0
                && DateTime.UtcNow < deadline)
            {
                DoEvents();
            }
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

        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu zadania produkcyjnego.");
                DoEvents();
            }

            task.GetAwaiter().GetResult();
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
        }

        /// <summary>NUMER SLOTU z RZECZYWISTEGO menedzera sesji okna.</summary>
        private sealed class SessionManagerLike(MainWindow window)
        {
            internal int Slot(string sessionId)
            {
                var sessions = window.SessionsForTests;
                return sessions.FindSlot(sessionId)
                    ?? throw new Exception("Konfiguracja nie ma slotu sesji \"" + sessionId + "\".");
            }
        }

        public void Dispose()
        {
            try { Window.Close(); } catch (InvalidOperationException) { }
            try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
        }
    }
}
