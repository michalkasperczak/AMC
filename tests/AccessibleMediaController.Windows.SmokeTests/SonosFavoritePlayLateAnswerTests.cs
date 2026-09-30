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
/// F3c: ZYCIE ZLECENIA URUCHOMIENIA a ZYCIE MODALA. Mierzy PRODUKCYJNA droge
/// <see cref="MainWindow"/> - prawdziwe polecenie "Pokaż ulubione", PRAWDZIWY
/// modal <c>ShowDialog</c> z produkcyjnym wlascicielem, PRAWDZIWY Enter i
/// PRAWDZIWY Escape - a zaplecze <c>loadFavorite</c> jest WSTRZYMANE, zeby
/// odpowiedz przyszla DOPIERO PO ZAMKNIECIU okna, ktore ja zlecilo.
///
/// CO TU JEST, A CZEGO NIE MA:
///  * liczniki <c>Loads</c>/<c>GroupReads</c> to liczba WYWOLAN SYNTETYCZNEGO
///    ZAPLECZA w tym procesie, a NIE zadania HTTP, nie POST do Sonosa i nie
///    dowod, ze cokolwiek gra,
///  * Enter i Escape to PRAWDZIWE zdarzenia WPF z zywego urzadzenia klawiatury,
///    a NIE fizyczna klawiatura i NIE mowa NVDA; "ogloszenie" znaczy tekst
///    oddany do <c>AnnouncementSinkForTests</c> albo do statusu okna.
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
            Measure("P6", "Enter z prawdziwego modalu wysyła JEDNO wywołanie loadFavorite zaplecza: dokładna grupa, "
                + "dokładny identyfikator, Insert, playOnCompletion=true", MeasureRealEnterSendsExactInsert),
            Measure("P7", "Escape podczas trwającego zlecenia anuluje TOKEN zaplecza, domyka WŁASNE zadanie próby "
                + "i NIC nie mówi w oknie głównym ani nie rusza fokusu", MeasureLateAnswerAfterEscapeStaysSilent),
            Measure("P8", "Zaplecze ŚWIADOMIE IGNORUJĄCE anulowanie: spóźniony sukces A NIE mówi w NOWO otwartym "
                + "oknie B i nie rusza jego fokusu", MeasureLateAnswerDoesNotSpeakInB),
            Measure("P9", "Gdy fokus jest w OBCYM oknie, a modal A nadal żyje: wynik nie ogłasza się w oknie głównym "
                + "i nie odbiera fokusu obcemu oknu", MeasureLateAnswerWithForeignFocus),
            Measure("P10", "Zaplecze IGNORUJĄCE anulowanie: spóźniony sukces po zamknięciu A daje 0 komunikatów okna "
                + "głównego i 0 odczytów stanu grupy po poleceniu", MeasureIgnoredCancelAfterCloseIsSilent),
            Measure("P11", "Spóźnione finally A nie zwalnia bramki WŁASNEGO zlecenia B, a zamknięcie i ponowne "
                + "otwarcie nie zostawia bramki zablokowanej na zawsze", MeasureLateFinallyKeepsOwnGate)
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

    // ===== P6: PRAWDZIWY Enter w PRAWDZIWYM modalu -> JEDNO jawne Insert =====

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
        if (posts.Count != 1) throw new Exception($"Enter dał {posts.Count} wywołań zaplecza zamiast 1.");
        var post = posts[0];
        if (post.GroupId != "GRUPA-SALON") throw new Exception("Zlecenie poszło do grupy \"" + post.GroupId + "\".");
        if (post.FavoriteId != "ULU-7")
        {
            throw new Exception("Zlecenie wysłało ulubione \"" + post.FavoriteId + "\" zamiast zaznaczonego ULU-7.");
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

        return "1 wywołanie zaplecza: GRUPA-SALON/ULU-7, Insert, playOnCompletion=true, zero dodatkowego Play";
    }

    // ===== P7: ESCAPE anuluje WLASNE oczekiwanie - cisza i zero ruchu fokusu =====

    private static string MeasureLateAnswerAfterEscapeStaysSilent()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        harness.Enter();

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = held.Task;
        var startedInFlight = false;
        Task? taskA = null;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                startedInFlight = harness.Backend.Loads.Count == 1;
                // REFERENCJA na zadanie proby ZANIM okno sie zamknie: po Close
                // okno jej nie odda, a pomiar musi domknac DOKLADNIE to zadanie.
                taskA = window.LastPlayTaskForTests;
                // ZAMKNIECIE PODCZAS trwajacego zlecenia: prawdziwy Escape.
                window.PressEscapeForTests();
            });

            if (!startedInFlight)
            {
                throw new Exception($"Zlecenie nie ruszyło przed Escape ({harness.Backend.Loads.Count}).");
            }
            if (taskA is null) throw new Exception("Modal A nie zapamiętał zadania próby.");
            if (harness.Window.OpenSonosFavoritesWindowForTests is not null)
            {
                throw new Exception("Okno ulubionych nadal jest zapamiętane jako otwarte po Escape.");
            }

            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            // WLASNE oczekiwanie MUSI byc anulowane u zaplecza - to ma zrobic
            // zamkniecie TEGO modalu, a nie anulowanie calej sesji Sonos.
            if (!harness.Backend.LastLoadTokenCanceled)
            {
                throw new Exception("Escape NIE anulował tokenu, z którym zlecenie poszło do zaplecza.");
            }
            var spokenBefore = harness.Announcements.Count;
            var focusBefore = Keyboard.FocusedElement;
            var childWindowsBefore = harness.VisibleOwnedWindows();

            // SPOZNIONA ODPOWIEDZ: dopiero TERAZ, gdy okna juz nie ma.
            held.TrySetResult();
            harness.PumpTask(taskA, "spóźnione zadanie próby A");
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

            return "token anulowany po Escape, własne zadanie domknięte, cisza w oknie głównym, fokus nieruszony";
        }
        finally
        {
            held.TrySetResult();
            harness.DrainFavoriteLoad();
        }
    }

    // ===== P8: zaplecze IGNORUJACE cancel - spozniony sukces A a NOWE okno B =====

    private static string MeasureLateAnswerDoesNotSpeakInB()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        // SWIADOME IGNOROWANIE anulowania: sam token NIE MOZE przykryc braku
        // powiazania spoznionej odpowiedzi ze zlecajacym modalem.
        harness.Backend.IgnoreCancellation = true;
        harness.Enter();

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = held.Task;
        Task? taskA = null;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                taskA = window.LastPlayTaskForTests;
                window.PressEscapeForTests();
            });
            if (harness.Backend.Loads.Count != 1) throw new Exception("Zlecenie A nie ruszyło, nic nie mierzymy.");
            if (taskA is null) throw new Exception("Modal A nie zapamiętał zadania próby.");

            harness.ReactivateOwnWindow();
            string? lateInB = null;
            string? focusMoved = null;
            // OKNO B otwarte, gdy A NADAL wisi. Spozniona odpowiedz A przychodzi
            // w trakcie zycia B: nie ma prawa mowic w CUDZYM oknie.
            harness.RunFavoritesModal(windowB =>
            {
                var before = windowB.StatusForTests;
                var focusBefore = Keyboard.FocusedElement;
                held.TrySetResult();
                // DOMKNIECIE zadania A jeszcze W CZASIE ZYCIA B.
                harness.PumpTask(taskA, "spóźnione zadanie A w czasie życia B");
                windowB.PumpForTests(TimeSpan.FromMilliseconds(150));
                var after = windowB.StatusForTests;
                if (!string.Equals(before, after, StringComparison.Ordinal)) lateInB = after;
                if (!ReferenceEquals(Keyboard.FocusedElement, focusBefore))
                {
                    focusMoved = Describe(Keyboard.FocusedElement);
                }

                windowB.PressEscapeForTests();
            });

            if (lateInB is not null)
            {
                throw new Exception("Spóźniona odpowiedź A odezwała się w oknie B: \"" + lateInB + "\".");
            }
            if (focusMoved is not null)
            {
                throw new Exception("Spóźniona odpowiedź A ruszyła fokus w oknie B na " + focusMoved + ".");
            }
            if (harness.Backend.Loads.Count != 1)
            {
                throw new Exception($"Otwarcie B wysłało dodatkowe zlecenie ({harness.Backend.Loads.Count}).");
            }

            return "okno B nie dostało komunikatu ani fokusu porzuconej próby A (zaplecze ignorowało anulowanie)";
        }
        finally
        {
            held.TrySetResult();
            harness.DrainFavoriteLoad();
        }
    }

    // ===== P9: OBCY fokus przy ZYWYM modalu A - zero mowy w oknie glownym =====

    private static string MeasureLateAnswerWithForeignFocus()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        harness.Enter();

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = held.Task;
        Window? foreign = null;
        string[] lateInMain = [];
        string? focusMoved = null;
        var foreignWasActive = false;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                if (harness.Backend.Loads.Count != 1) throw new Exception("Zlecenie A nie ruszyło.");

                // WLASNE okno pomiaru POZA modalem - nie cudza aplikacja. Modal A
                // NADAL zyje; fokus jest gdzie indziej.
                foreign = new Window
                {
                    Title = "Obce okno pomiaru", Width = 220, Height = 140, ShowInTaskbar = false
                };
                foreign.Show();
                foreign.Activate();
                window.PumpForTests(TimeSpan.FromMilliseconds(150));
                foreignWasActive = foreign.IsActive;

                var spokenBefore = harness.Announcements.Count;
                var focusBefore = Keyboard.FocusedElement;
                held.TrySetResult();
                harness.PumpTask(window.LastPlayTaskForTests, "zadanie próby przy obcym fokusie");
                window.PumpForTests(TimeSpan.FromMilliseconds(150));

                lateInMain = harness.Announcements.Skip(spokenBefore).ToArray();
                if (!ReferenceEquals(Keyboard.FocusedElement, focusBefore))
                {
                    focusMoved = Describe(Keyboard.FocusedElement);
                }

                window.PressEscapeForTests();
            });

            if (!foreignWasActive) throw new Exception("Obce okno pomiaru nie stało się aktywne, nic nie mierzymy.");
            if (lateInMain.Length != 0)
            {
                throw new Exception($"Wynik ogłosił {lateInMain.Length} komunikat(ów) w oknie głównym: \""
                    + string.Join(" | ", lateInMain) + "\".");
            }
            if (focusMoved is not null)
            {
                throw new Exception("Wynik odebrał fokus obcemu oknu na rzecz " + focusMoved + ".");
            }

            return "przy obcym fokusie: zero komunikatów okna głównego, fokus u obcego okna nietknięty";
        }
        finally
        {
            held.TrySetResult();
            harness.DrainFavoriteLoad();
            if (foreign is not null)
            {
                try { foreign.Close(); } catch (InvalidOperationException) { }
            }
        }
    }

    // ===== P10: IGNOROWANY cancel po zamknieciu - zero mowy, zero odczytu =====

    private static string MeasureIgnoredCancelAfterCloseIsSilent()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        harness.Backend.IgnoreCancellation = true;
        harness.Enter();

        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = held.Task;
        Task? taskA = null;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                taskA = window.LastPlayTaskForTests;
                window.PressEscapeForTests();
            });
            if (taskA is null) throw new Exception("Modal A nie zapamiętał zadania próby.");
            if (harness.Backend.Loads.Count != 1) throw new Exception("Zlecenie A nie ruszyło.");

            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            var spokenBefore = harness.Announcements.Count;
            var readsBefore = harness.Backend.GroupReadsForTests;

            held.TrySetResult();
            harness.PumpTask(taskA, "spóźnione zadanie A z zaplecza ignorującego anulowanie");
            harness.DrainFavoriteLoad();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

            var late = harness.Announcements.Skip(spokenBefore).ToArray();
            var reads = harness.Backend.GroupReadsForTests - readsBefore;
            if (late.Length != 0)
            {
                throw new Exception($"Spóźniony SUKCES ogłosił {late.Length} komunikat(ów) w oknie głównym: \""
                    + string.Join(" | ", late) + "\".");
            }
            if (reads != 0)
            {
                throw new Exception($"Spóźniony sukces wykonał {reads} odczyt(ów) stanu grupy po poleceniu.");
            }

            return "spóźniony sukces po zamknięciu: 0 komunikatów okna głównego, 0 odczytów stanu grupy";
        }
        finally
        {
            held.TrySetResult();
            harness.DrainFavoriteLoad();
        }
    }

    // ===== P11: WLASNA bramka zlecenia B a spoznione finally A =====

    private static string MeasureLateFinallyKeepsOwnGate()
    {
        using var harness = MainHarness.Create();
        harness.Backend.SetFavorites(("ULU-1", "Nokturny"));
        harness.Enter();

        var heldA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Backend.LoadGate = heldA.Task;
        Task? taskA = null;
        var gateAfterLateA = false;
        var statusInB = string.Empty;
        try
        {
            harness.RunFavoritesModal(window =>
            {
                window.SelectForTests(0);
                window.PressEnterForTests();
                window.PumpForTests(TimeSpan.FromMilliseconds(120));
                taskA = window.LastPlayTaskForTests;
                window.PressEscapeForTests();
            });
            if (taskA is null) throw new Exception("Modal A nie zapamiętał zadania próby.");

            // ZAMKNIECIE A domyka WLASNE oczekiwanie i zwalnia WLASNA bramke:
            // zamkniecie i ponowne otwarcie NIE MOZE zostawic bramki zajetej.
            harness.PumpTask(taskA, "zadanie A po Escape");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (harness.CommandGateBusy)
            {
                throw new Exception("Po zamknięciu A bramka polecenia została zajęta.");
            }

            var ticketOfA = harness.CommandGateTicket;
            harness.ReactivateOwnWindow();
            harness.Backend.LoadGate = heldB.Task;
            harness.RunFavoritesModal(windowB =>
            {
                // WLASNE zlecenie B bierze SWOJ bilet bramki.
                windowB.SelectForTests(0);
                windowB.PressEnterForTests();
                windowB.PumpForTests(TimeSpan.FromMilliseconds(120));
                if (harness.Backend.Loads.Count != 2)
                {
                    throw new Exception($"Enter w B dał {harness.Backend.Loads.Count} wywołań zaplecza zamiast 2.");
                }
                if (!harness.CommandGateBusy) throw new Exception("Trwające zlecenie B nie zajęło bramki.");

                // SPOZNIONE finally A: PRODUKCYJNA droga zwolnienia bramki ze
                // STARYM biletem nie ma prawa zwolnic bramki trwajacego B.
                harness.Window.ReleaseSonosCommandGateForTests(ticketOfA);
                windowB.PumpForTests(TimeSpan.FromMilliseconds(60));
                gateAfterLateA = harness.CommandGateBusy;
                statusInB = windowB.StatusForTests;

                heldB.TrySetResult();
                windowB.AwaitPlayForTests();
                windowB.PressEscapeForTests();
            });

            if (!gateAfterLateA)
            {
                throw new Exception("Spóźnione finally A zwolniło bramkę TRWAJĄCEGO zlecenia B.");
            }
            if (!statusInB.Contains("Wysyłam polecenie uruchomienia", StringComparison.Ordinal))
            {
                throw new Exception("Okno B nie pokazywało własnego oczekiwania: \"" + statusInB + "\".");
            }
            if (harness.CommandGateBusy)
            {
                throw new Exception("Po zakończeniu zlecenia B bramka polecenia została zajęta na zawsze.");
            }

            return "bramka B nietknięta spóźnionym zwolnieniem biletu A, po zakończeniu zwolniona";
        }
        finally
        {
            heldA.TrySetResult();
            heldB.TrySetResult();
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

        /// <summary>
        /// Stan PRODUKCYJNEJ bramki jednego polecenia Sonos, czytany z pola - bez
        /// wlasnej kopii warunku po stronie pomiaru.
        /// </summary>
        internal bool CommandGateBusy =>
            (bool)(Window.GetType().GetField("_sonosCommandInFlight", Instance)!.GetValue(Window)
                ?? throw new Exception("Brak pola bramki polecenia."));

        /// <summary>BIEZACY bilet bramki polecenia - do sprawdzenia spoznionego zwolnienia.</summary>
        internal int CommandGateTicket =>
            (int)(Window.GetType().GetField("_sonosCommandGateTicket", Instance)!.GetValue(Window)
                ?? throw new Exception("Brak pola biletu bramki polecenia."));

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
            // PODSTAWIENIE zaplecza idzie PO konstruktorze, przez wlasnosc
            // SonosBackendOverride - nie przez fabryke magazynu i nie przez
            // wlasciciela konta. EnsureSonosBackend zwraca override PRZED
            // wlascicielem, wiec zadne okno konta nie moze sie otworzyc.
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

        /// <summary>
        /// DOMKNIECIE DOKLADNIE TEGO zadania proby - z TWARDYM niepowodzeniem po
        /// limicie. Zwlok na zegarze nie uznajemy za dowod domkniecia.
        /// </summary>
        internal void PumpTask(Task? task, string what)
        {
            if (task is null) throw new Exception("Brak zadania do domknięcia: " + what + ".");
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }

            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// DOMKNIECIE spoznionych wywolan zaplecza: czekamy na LICZNIK zaplecza z
        /// TWARDYM niepowodzeniem, nie na zegar.
        /// </summary>
        internal void DrainFavoriteLoad()
        {
            var deadline = DateTime.UtcNow + Limit;
            while (Backend.OutstandingLoads > 0)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new Exception(
                        $"Limit czasu: {Backend.OutstandingLoads} wywołań zaplecza nie domknęło się.");
                }

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
