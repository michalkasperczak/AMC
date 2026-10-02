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

/// <summary>
/// WEJSCIE do sesji Sonos mierzone RZECZYWISTA droga uzytkownika, nie helperami:
///   * B1: prawdziwe <c>ExecuteCommand(session.slot.8)</c> (to, co robi Ctrl+8)
///     i pompa dispatchera - liczy sie zawartosc KONTROLKI MediaList, nie
///     session.Items. Bezpośrednie EnterSonosSessionAsync tego NIE pokrywa,
///     bo pomija odswiezenie widoku po asynchronicznym wejsciu.
///   * B2: prawdziwy, odebrany WYBOR CELU pod Ctrl+F5 - okno wyboru, PELNA
///     droga potwierdzenia i otwarcie odtwarzacza klawiszem F6.
///   * B3: spozniony odczyt + swiadoma zmiana sesji/grupy w trakcie NIE moze
///     ukrasc fokusu odtwarzaczem porzuconego celu.
///
/// ZMIENIONE OCZEKIWANIE (wymaganie korzenia tresci): widoczna lista korzenia
/// sesji Sonos pokazuje TRZY KATEGORIE MATERIALU z
/// <c>SonosLibraryPresentation.DescribeCategories()</c>, a GRUPY zyja osobno w
/// <c>Window.SonosGroupRows</c> i wybiera sie je WYLACZNIE pod Ctrl+F5.
/// Dawniej B1 zadal 2 wierszy GRUP w kontrolce, a B2 robil Enter na wierszu
/// grupy - to byl dokladnie uklad, ktory zostal odrzucony (glosniki nie sa
/// biblioteka muzyczna). Pomiary nie slabna: nadal pilnuja, ze samo wejscie i
/// sam wybor NIE wysylaja POST, ze cel jest ustawiony i ze porzucony cel nie
/// zabiera fokusu.
///
/// Ten pomiar POKAZUJE WLASNE okno (osobny przelacznik
/// <c>--sonos-session-entry-ui</c>). Nie wysyla klawiszy systemowych, nie rusza
/// NVDA, nie dotyka konta, sieci, DPAPI, audio ani cudzych okien.
/// </summary>
internal static class SonosSessionEntryUiTests
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
                checks += MeasureFirstEntryFillsRealList();
                checks += MeasureRealEnterOnSelectedRowOpensPlayer();
                checks += MeasureStaleActivationDoesNotStealFocus();
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
            "OK: wejscie do sesji Sonos rzeczywista droga uzytkownika - pierwsze Ctrl+8 wypelnia "
            + "kontrolke listy, Ctrl+F5 wybiera cel sterowania i F6 otwiera odtwarzacz, "
            + $"porzucony cel nie zabiera fokusu ({checks} sprawdzeń, WLASNE pokazane okno)");
    }

    // ===== B1: pierwsze wejscie musi wypelnic KONTROLKE listy KATEGORIAMI =====

    private static int MeasureFirstEntryFillsRealList()
    {
        // Bez pokazywania okna: sama zawartosc kontrolki listy jest mierzalna.
        using var harness = Harness.Create();
        var window = harness.Window;
        var slot = window.SessionsForTests.SessionSlots
            .First(pair => string.Equals(pair.Value, "sonos", StringComparison.Ordinal)).Key;
        if (slot != 8)
        {
            throw new Exception($"Sesja Sonos nie jest na slocie 8, lecz {slot}; pomiar mierzyl Ctrl+8.");
        }
        if (SonosSession(window).Items.Count != 0)
        {
            throw new Exception("Sesja Sonos ma materiał przed wejściem.");
        }

        // PRAWDZIWY caller: to samo, co robi Ctrl+8.
        harness.ExecuteCommand(CommandIds.SessionSlot(slot));
        harness.PumpUntil(
            () => window.SonosGroupRows.Count == 2,
            "wejście do sesji Sonos nie odczytało grup");

        // MODEL STEROWANIA: grupy sa odczytane i ZOSTAJA w SonosGroupRows. To
        // one sa celem Ctrl+F5, a nie trescia widocznej listy.
        var groupIds = window.SonosGroupRows.Select(row => row.GroupId).ToArray();
        if (!groupIds.Contains("GRUPA-SALON", StringComparer.Ordinal)
            || !groupIds.Contains("GRUPA-KUCHNIA", StringComparer.Ordinal))
        {
            throw new Exception(
                "Model sterowania nie ma odczytanych grup Sonos: " + string.Join(" | ", groupIds));
        }

        // WIDOCZNA LISTA: KATEGORIE MATERIALU z Core, nie glosniki. Liczba i
        // identyfikatory ida z produkcyjnego zrodla, zeby pomiar nie powtarzal
        // polskich nazw jako klucza.
        var expectedCategories = SonosLibraryPresentation.DescribeCategories();
        var list = harness.MediaList;
        if (list.Items.Count != expectedCategories.Count)
        {
            throw new Exception(
                $"Po PIERWSZYM wejściu kontrolka listy ma {list.Items.Count} wierszy zamiast "
                + $"{expectedCategories.Count} kategorii materiału: " + string.Join(" | ", harness.RowLabels()));
        }
        if (list.SelectedIndex < 0)
        {
            throw new Exception("Lista korzenia Sonos nie ma zaznaczenia po wejściu.");
        }

        var labels = harness.RowLabels();
        foreach (var category in expectedCategories)
        {
            if (!labels.Any(label => label.Contains(category.Name, StringComparison.Ordinal)))
            {
                throw new Exception(
                    $"Korzeń sesji Sonos nie ma kategorii \"{category.Name}\": " + string.Join(" | ", labels));
            }
        }

        // DYSKRYMINUJACA ASERCJA ZGLOSZENIA: zaden GLOSNIK/GRUPA nie wraca na
        // widoczna liste. Bez korzenia tresci lecialyby tu wiersze "Salon" i
        // "Kuchnia" z Session.Items i ten warunek by padl.
        if (labels.Any(label => label.Contains("Salon", StringComparison.Ordinal)
            || label.Contains("Kuchnia", StringComparison.Ordinal)))
        {
            throw new Exception(
                "Korzeń sesji Sonos pokazuje GŁOŚNIKI jako treść: " + string.Join(" | ", labels));
        }

        // KATEGORIE NIE WCHODZA do Session.Items - zaden ogolny tor odtwarzania
        // ani zapisu presetu nie ma czego zlapac.
        if (SonosSession(window).Items.Any(item => SonosLibraryPresentation.IsCategoryId(item.Id)))
        {
            throw new Exception("Kategoria Biblioteki Sonos trafiła do Session.Items jako materiał.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Samo wejście do sesji wysłało polecenie do Sonosa.");
        }
        return 7;
    }

    // ===== B2: WYBOR CELU pod Ctrl+F5, odtwarzacz pod F6 =====

    /// <summary>
    /// ZMIENIONE OCZEKIWANIE. Ten pomiar zadal wczesniej Entera na WIERSZU GRUPY
    /// w widocznej liscie. Grup tam juz nie ma (korzen pokazuje tresc), wiec
    /// mierzymy ODEBRANA droge wyboru celu: Ctrl+F5 otwiera okno wyboru, PELNA
    /// droga potwierdzenia ustawia cel, a odtwarzacz otwiera F6.
    ///
    /// Pomiar NIE SLABNIE: nadal pilnuje, ze sam wybor celu NIE wysyla POST, ze
    /// cel faktycznie sie zmienil (SonosActiveGroup i biezacy element sesji), ze
    /// wybor zrobil jawny odczyt stanu i glosnosci, i ze przelaczenie na DRUGA
    /// grupe te sama droga naprawde zmienia adresata polecen.
    /// </summary>
    private static int MeasureRealEnterOnSelectedRowOpensPlayer()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ShowOwnWindow();
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(
            () => window.SonosGroupRows.Count == 2,
            "pierwsze wejście nie odczytało grup w pokazanym oknie");

        // OKNO MUSI BYC AKTYWNE: produkcyjna brama okien potomnych Sonosa
        // odmawia otwarcia, gdy AMC nie jest na wierzchu. Pomiar respektuje te
        // brame, zamiast ja obchodzic.
        harness.ActivateOwnWindow();

        var checks = ChooseTargetThenOpenPlayer(harness, "Salon", "GRUPA-SALON");

        // Escape wraca na liste, a wyjscie z odtwarzacza NIE zatrzymuje muzyki.
        var commandsBeforeEscape = harness.Backend.Commands.Count;
        harness.PressKey(Key.Escape);
        harness.PumpUntil(() => !harness.PlayerViewActive, "Escape nie wrócił z odtwarzacza");
        if (harness.Backend.Commands.Count != commandsBeforeEscape)
        {
            throw new Exception("Wyjście z odtwarzacza Sonos wysłało polecenie transportu.");
        }
        checks++;

        // POWROT do korzenia tresci: lista znow pokazuje KATEGORIE, nie glosniki.
        harness.PumpUntil(
            () => harness.MediaList.Items.Count == SonosLibraryPresentation.DescribeCategories().Count,
            "korzeń treści nie wrócił po Escape");
        if (harness.RowLabels().Any(label => label.Contains("Salon", StringComparison.Ordinal)))
        {
            throw new Exception(
                "Po powrocie z odtwarzacza lista pokazuje głośniki: "
                + string.Join(" | ", harness.RowLabels()));
        }
        checks++;

        // DRUGA grupa TA SAMA droga: wybor celu musi przelaczyc adresata.
        checks += ChooseTargetThenOpenPlayer(harness, "Kuchnia", "GRUPA-KUCHNIA");
        return checks;
    }

    /// <summary>
    /// ODEBRANA droga wyboru celu: Ctrl+F5 -&gt; okno wyboru -&gt; potwierdzenie,
    /// a potem F6 na odtwarzacz. Pokazanie modala jest PODSTAWIONE (pomiar bez
    /// stawiania okna na pulpicie), lecz droga potwierdzenia i cala logika
    /// wlasciciela sa PRODUKCYJNE.
    /// </summary>
    private static int ChooseTargetThenOpenPlayer(Harness harness, string groupName, string groupId)
    {
        var window = harness.Window;
        var playbackReadsBefore = harness.Backend.PlaybackReads;
        var volumeReadsBefore = harness.Backend.VolumeReads;
        var commandsBefore = harness.Backend.Commands.Count;
        var appliedBefore = window.SonosTargetSelectionsAppliedForTests;

        // PRODUKCYJNY punkt podstawienia POKAZANIA okna. Zaznaczamy wiersz
        // szukanej grupy i potwierdzamy PELNA droga przycisku "Ustaw jako cel".
        SonosTargetSelectionWindow? opened = null;
        window.PresentSonosTargetOverrideForTests = targetWindow =>
        {
            opened = targetWindow;
            var labels = targetWindow.RowLabelsForTests;
            var index = Enumerable.Range(0, labels.Count).First(position =>
                labels[position].Contains(groupName, StringComparison.Ordinal));
            targetWindow.SelectRowForTests(index);
            targetWindow.ConfirmForTests();
        };
        try
        {
            harness.PressCtrl(Key.F5);
            harness.PumpUntil(
                () => window.SonosTargetSelectionsAppliedForTests > appliedBefore,
                $"wybór celu {groupName} pod Ctrl+F5 nie zmienił celu sterowania");
        }
        finally
        {
            window.PresentSonosTargetOverrideForTests = null;
        }

        if (opened is null)
        {
            throw new Exception($"Ctrl+F5 nie otworzyło okna wyboru celu dla grupy {groupName}.");
        }

        // CEL faktycznie ustawiony, i to po IDENTYFIKATORZE.
        if (window.SonosSelectedGroupId != groupId)
        {
            throw new Exception(
                $"Wybór celu {groupName} nie ustawił grupy: SelectedGroupId="
                + (window.SonosSelectedGroupId ?? "null"));
        }
        if (window.SonosActiveGroup?.Id != groupId)
        {
            throw new Exception($"Aktywna grupa po wyborze celu to nie {groupId}.");
        }
        if (!string.Equals(window.SonosActiveGroup?.Name, groupName, StringComparison.Ordinal))
        {
            throw new Exception(
                $"Cel sterowania ma inną nazwę niż wybrana: " + (window.SonosActiveGroup?.Name ?? "null"));
        }
        // Grupy NIE wchodza na widoczna liste przez wybor celu - tam jest tresc.
        if (harness.RowLabels().Any(label => label.Contains(groupName, StringComparison.Ordinal)))
        {
            throw new Exception(
                "Wybór celu wepchnął głośnik na listę treści: " + string.Join(" | ", harness.RowLabels()));
        }
        if (harness.Backend.PlaybackReads <= playbackReadsBefore
            || harness.Backend.VolumeReads <= volumeReadsBefore)
        {
            throw new Exception("Wybór celu nie zrobił jawnego odczytu stanu i głośności.");
        }
        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Sam WYBÓR celu pod Ctrl+F5 wysłał POST do Sonosa.");
        }

        // ODTWARZACZ prawdziwym klawiszem F6 - istniejaca, odebrana droga.
        harness.MediaList.Focus();
        harness.PressKey(Key.F6);
        harness.PumpUntil(
            () => harness.PlayerViewActive,
            $"F6 nie otworzyło odtwarzacza dla celu {groupName}");
        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Samo otwarcie odtwarzacza wysłało POST do Sonosa.");
        }
        if (string.Equals(harness.CurrentView, groupName, StringComparison.Ordinal))
        {
            throw new Exception(
                "Droga poszła ogólnym NavigateTo(tytuł) i otworzyła widok nazwany jak element.");
        }
        return 8;
    }

    // ===== B3: porzucony cel nie zabiera fokusu =====

    private static int MeasureStaleActivationDoesNotStealFocus()
    {
        // Najpierw niezalezna kontrola grup: mutacja guarda musi upasc TUTAJ,
        // nie wczesniej na odrebnym przypadku zmiany sesji.
        var checks = MeasureStaleActivationAfterGroupChange();
        checks += MeasureStaleActivationAfterSessionChange();
        return checks;
    }

    /// <summary>
    /// SWIADOME wyjscie do INNEJ, NIEPUSTEJ sesji w trakcie odczytu. Cel musi
    /// miec BIEZACY element: przy pustej sesji odtwarzacz i tak by sie nie
    /// otworzyl (!HasCurrentItem), wiec taki pomiar nie dotyka ochrony po await.
    /// Czekamy na RZECZYWISTE zakonczenie zadania aktywacji, nie na staly czas.
    /// </summary>
    private static int MeasureStaleActivationAfterSessionChange()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ShowOwnWindow();
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(
            () => window.SonosGroupRows.Count == 2,
            "brak odczytanych grup przed pomiarem B3");

        // Odczyt po aktywacji zostaje WSTRZYMANY: w tym czasie uzytkownik
        // swiadomie wychodzi z sesji.
        var release = new TaskCompletionSource();
        harness.Backend.ReadGate = release.Task;
        try
        {
            // SETUP, nie dowod klawisza: grupa jest tylko WEJSCIEM do wyscigu.
            // Uzywamy waskiego hooka aktywacji z odtwarzaczem - tej samej
            // produkcyjnej kolejnosci, ktora konczy wybor celu pod Ctrl+F5.
            // Dowod samego skrotu nalezy do B2 i SonosNavigationUxTests.
            var started = StartActivationWithPlayer(window, "GRUPA-SALON");
            harness.PumpUntil(
                () => harness.Backend.PlaybackReads > 0,
                "aktywacja grupy nie zaczęła odczytu");
            if (started.IsCompleted)
            {
                throw new Exception(
                    "Zadanie aktywacji zakończyło się mimo wstrzymanego odczytu; "
                    + "pomiar spóźnionej odpowiedzi byłby pozorny.");
            }

            // Cel wyjscia musi byc NIEPUSTA sesja z biezacym elementem.
            var otherSlot = window.SessionsForTests.SessionSlots
                .Where(pair => !string.Equals(pair.Value, "sonos", StringComparison.Ordinal))
                .Select(pair => (int?)pair.Key)
                .FirstOrDefault(slot =>
                    window.SessionsForTests.FindSession(
                        window.SessionsForTests.SessionSlots[slot!.Value])?.HasCurrentItem == true)
                ?? throw new Exception("Brak niepustej obcej sesji; pomiar B3 nie mierzyłby ochrony.");
            harness.ExecuteCommand(CommandIds.SessionSlot(otherSlot));
            if (!window.SessionsForTests.Current.HasCurrentItem)
            {
                throw new Exception(
                    "Sesja docelowa nie ma bieżącego elementu; odtwarzacz odmówiłby niezależnie od ochrony.");
            }
            if (harness.PlayerViewActive)
            {
                throw new Exception("Odtwarzacz był otwarty jeszcze przed zwolnieniem odczytu.");
            }

            release.SetResult();
            harness.Backend.ReadGate = null;

            // OBSERWUJEMY wynik rzeczywistego zadania, nie odczekany czas.
            harness.Pump(started);
        }
        finally
        {
            harness.Backend.ReadGate = null;
            release.TrySetResult();
        }

        if (SonosSessionUiTestsBridge.IsSonos(window.SessionsForTests.Current.Id))
        {
            throw new Exception("Świadome wyjście z sesji Sonos nie zostało utrzymane.");
        }
        if (harness.PlayerViewActive)
        {
            throw new Exception("Spóźniona aktywacja grupy Sonos otworzyła odtwarzacz i ukradła fokus.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Wyjście z sesji w trakcie odczytu wysłało polecenie do Sonosa.");
        }
        return 5;
    }

    /// <summary>
    /// Zmiana GRUPY A-&gt;B w trakcie odczytu: spozniona odpowiedz grupy A nie
    /// moze przestawic zaznaczenia ani kontekstu odtwarzacza na porzucony cel.
    /// </summary>
    private static int MeasureStaleActivationAfterGroupChange()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ShowOwnWindow();
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(
            () => window.SonosGroupRows.Count == 2,
            "brak odczytanych grup przed zmianą grupy");

        // ODDZIELNE bariery: konczymy A, gdy B NADAL czeka. Sam koncowy stan
        // po obu odpowiedziach nie wykrywal przedwczesnego otwarcia przez A.
        var releaseFirst = new TaskCompletionSource();
        var releaseSecond = new TaskCompletionSource();
        harness.Backend.ReadGate = releaseFirst.Task;
        Task first;
        Task second;
        try
        {
            // SETUP, nie dowod klawisza (patrz StartActivationWithPlayer).
            first = StartActivationWithPlayer(window, "GRUPA-SALON");
            harness.PumpUntil(() => harness.Backend.PlaybackReads > 0, "grupa A nie zaczęła odczytu");
            if (first.IsCompleted) throw new Exception("Zadanie grupy A zakończyło się mimo wstrzymanego odczytu.");

            // A przechwycilo juz swoja bariere w ReadGroupPlaybackAsync.
            // Nowe wywolanie B przechwyci druga, nadal zamknieta bariere.
            harness.Backend.ReadGate = releaseSecond.Task;
            var readsBeforeSecond = harness.Backend.PlaybackReads;
            second = StartActivationWithPlayer(window, "GRUPA-KUCHNIA");
            harness.PumpUntil(
                () => harness.Backend.PlaybackReads > readsBeforeSecond,
                "aktywacja grupy B nie zaczęła własnego odczytu");
            if (second.IsCompleted || harness.PlayerViewActive)
            {
                throw new Exception("Nieprawidłowa kontrolka: grupa B powinna nadal czekać na odczyt bez otwartego odtwarzacza.");
            }

            releaseFirst.SetResult();
            harness.Pump(first);
            if (second.IsCompleted || harness.PlayerViewActive)
            {
                throw new Exception("Porzucona aktywacja grupy A otworzyła odtwarzacz, gdy grupa B nadal czeka na odczyt.");
            }

            releaseSecond.SetResult();
            harness.Backend.ReadGate = null;
            harness.Pump(second);
        }
        finally
        {
            harness.Backend.ReadGate = null;
            releaseFirst.TrySetResult();
            releaseSecond.TrySetResult();
        }

        if (!harness.PlayerViewActive)
        {
            throw new Exception("Prawidłowa aktywacja grupy B nie otworzyła odtwarzacza po zakończeniu jej odczytu.");
        }
        if (window.SonosActiveGroup?.Id != "GRUPA-KUCHNIA")
        {
            throw new Exception(
                "Po zmianie grupy w trakcie odczytu aktywna jest "
                + (window.SonosActiveGroup?.Id ?? "null") + ", nie wybrana GRUPA-KUCHNIA.");
        }
        var session = SonosSession(window);
        if (!session.HasCurrentItem || session.CurrentItem.Id != "GRUPA-KUCHNIA")
        {
            throw new Exception("Spóźniona grupa A przestawiła bieżący element sesji na porzucony cel.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Zmiana grupy w trakcie odczytu wysłała polecenie do Sonosa.");
        }
        return 6;
    }

    /// <summary>
    /// SETUP wyscigu, NIE dowod klawisza. Pomiary B3 potrzebuja wylacznie
    /// ZACZETEJ aktywacji grupy wraz z otwarciem odtwarzacza - a dowod, ze
    /// uzytkownik dochodzi tu Ctrl+F5 i F6, nalezy do B2 oraz
    /// SonosNavigationUxTests. Uzywamy ISTNIEJACEGO, produkcyjnego waskiego
    /// hooka (tego samego, ktorym idzie <c>SonosPlayerUiTests</c>), zeby nie
    /// podstawiac glosnikow do widocznej listy tylko dla przejscia starej drogi.
    /// </summary>
    private static Task StartActivationWithPlayer(MainWindow window, string groupId) =>
        window.ActivateSonosGroupThenShowPlayerForTests(groupId);

    private static DemoMediaSession SonosSession(MainWindow window) =>
        window.SessionsForTests.FindSession("sonos")
        ?? throw new Exception("Sesja Sonos nie istnieje w prawdziwym SessionManagerze.");

    private static class SonosSessionUiTestsBridge
    {
        internal static bool IsSonos(string? sessionId) =>
            string.Equals(sessionId, "sonos", StringComparison.Ordinal);
    }

    // ==================== harness ====================

    /// <summary>
    /// WLASNE okno, WLASNA konfiguracja w katalogu tymczasowym, syntetyczna
    /// granica API. Pompa dispatchera jest RZECZYWISTA (PushFrame), a po limicie
    /// RZUCA: nieskonczone zadanie nie moze udawac zaliczenia.
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

        internal string CurrentView => (string)Field("_currentView")!;

        internal static Harness Create()
        {
            // JAWNY kontekst synchronizacji PRZED konstrukcja okna: bez tego
            // await w kodzie okna wracalby na pule watkow.
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-entry-" + Guid.NewGuid().ToString("N"));
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
        /// Pokazuje WYLACZNIE wlasne okno. Startowe zrodla z ContentRendered sa
        /// odlaczone; cala nawigacja, lista, klawiatura i odtwarzacz zostaja.
        /// </summary>
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
                "ExecuteCommand",
                Instance,
                binder: null,
                types: [typeof(string)],
                modifiers: null)
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

        internal void SelectAndFocusRow(int index)
        {
            var list = MediaList;
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
            if (list.SelectedIndex != index)
            {
                throw new Exception("Nie udało się zaznaczyć wiersza listy.");
            }
        }

        /// <summary>
        /// PRAWDZIWE zdarzenie klawiatury na elemencie, ktory ma fokus: tunelowany
        /// PreviewKeyDown przechodzi przez okno i liste tak, jak przy wcisnieciu
        /// klawisza. Zadnych klawiszy systemowych.
        /// </summary>
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
        /// ETYKIETY widocznych wierszy - to one niosa ZMIENIONE OCZEKIWANIE
        /// (kategorie materialu zamiast glosnikow).
        /// </summary>
        internal List<string> RowLabels() => MediaList.Items.Cast<object>()
            .Select(row => row.GetType().GetProperty("Label", Instance)?.GetValue(row) as string ?? string.Empty)
            .ToList();

        /// <summary>
        /// Stawia WLASNE okno na wierzch. Produkcyjna brama okien potomnych
        /// Sonosa (Ctrl+F5) odmawia otwarcia, gdy AMC nie jest na pierwszym
        /// planie - pomiar MUSI te brame spelnic, nie obejsc.
        /// </summary>
        internal void ActivateOwnWindow()
        {
            Window.Activate();
            Window.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(120));
            if (!Window.IsActive)
            {
                // Pulpit pomiarowy nie zawsze oddaje aktywacje. Podajemy
                // ISTNIEJACYM produkcyjnym hookiem WLASNY pid - brama nadal
                // odmawia obcemu pierwszemu planowi, bo porownuje z Environment.ProcessId.
                Window.ForegroundProcessIdOverrideForTests = Environment.ProcessId;
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

        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keys);

        [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] keys);

        /// <summary>Pompa oczekujaca na WARUNEK; po limicie RZUCA.</summary>
        internal void PumpUntil(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }
        }

        /// <summary>Pompa bez warunku: pozwala dokonczyc zaplanowane zadania.</summary>
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

        public void Dispose()
        {
            Window.CancelSonosPendingWork();

            // Zamykamy WYLACZNIE wlasne okno - takze gdy nigdy nie bylo Show.
            // IsVisible=false niepokazanego okna NIE dowodzi zamkniecia, wiec
            // bramka jest na zdarzeniu Closed.
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

            // Zaplanowane zadania okna konczymy PRZED usunieciem konfiguracji,
            // zeby zaden zapis nie trafil w usuniety katalog.
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

    /// <summary>SYNTETYCZNA granica API: zero HTTP, zero tokenu, zero magazynu.</summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int VolumeReads { get; private set; }

        internal SonosPlaybackState NextPlaybackState { get; set; } = SonosPlaybackState.Playing;

        /// <summary>Wstrzymanie ODCZYTU: pozwala zmierzyć spóźniony wynik.</summary>
        internal Task? ReadGate { get; set; }

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        public async Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReads++;
            if (ReadGate is { } gate) await gate.ConfigureAwait(true);
            var status = new SonosGroupPlaybackStatus(
                NextPlaybackState, null, null, "UTWOR-1", 12_000, null, null, null, _actions);
            return SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status);
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
            return Task.FromResult(
                SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));
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
}
