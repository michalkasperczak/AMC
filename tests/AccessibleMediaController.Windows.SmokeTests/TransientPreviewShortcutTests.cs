using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows;

/// <summary>
/// ZGLOSZENIE Michala 16.09.2026: Alt+R, Alt+Shift+R i Ctrl+I maja dzialac
/// GLOBALNIE, czyli ze WSZYSTKICH sesji AMC (nie systemowym RegisterHotKey),
/// a Escape ma wracac dokladnie tam, skad podglad wywolano.
///
/// Pomiar idzie przez RZECZYWISTE okno glowne i jego FAKTYCZNE handlery
/// (routing skrotu, pozycje menu, dostepnosc polecen, obsluga Escape), nie
/// przez tekst zrodel. Bez kont, sieci i dzwieku: wlasny katalog tymczasowy.
/// </summary>
internal static class TransientPreviewShortcutTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-global-previews-gui-" + Guid.NewGuid().ToString("N"));
            MainWindow? window = null;
            try
            {
                Directory.CreateDirectory(root);
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var (state, store) = Prepare(root);
                window = new MainWindow(state, store) { SuppressDesktopIntegrationForTests = true };
                typeof(MainWindow).GetField("_applicationUpdateStartOverride", Private)!.SetValue(window,
                    (Func<bool, bool>)(_ => throw new Exception("Test forbids installation")));

                TestSkrotRozpoznawanyWKazdejSesji(window);
                TestMenuPokazujePodgladyWKazdejSesji(window);
                TestDostepnoscPolecenZgodnaZPrzelacznikiem(window, state);
                TestWylaczonyPrzelacznikZwezaDoSesjiMacierzystych(window, state);
                TestEscapeWracaDoSesjiIWidokuWywolania(window, state);
                TestOpisPomocyBezPodwojnegoSkrotu(window);
                TestPodgladNieRuszaTransportu(window);
                Console.WriteLine(
                    "OK: wspólne podglądy Alt+R, Alt+Shift+R i Ctrl+I ze wszystkich sesji, "
                    + "menu zgodne ze skrótami, Escape wraca do miejsca wywołania");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90)))
            throw new Exception("Pomiar wspólnych podglądów przekroczył czas.");
        if (failure is not null)
            throw new Exception("Wspólne podglądy Alt+R, Alt+Shift+R i Ctrl+I: " + failure.Message, failure);
    }

    // ---------------------------------------------------------------- przypadki

    /// <summary>
    /// Routing skrotu: FAKTYCZNY handler okna musi rozpoznac wszystkie trzy
    /// skroty w KAZDEJ sesji i zwrocic wlasciwe polecenie.
    /// </summary>
    private static void TestSkrotRozpoznawanyWKazdejSesji(MainWindow window)
    {
        var sessions = Sessions(window);
        foreach (var session in sessions.Sessions.Select(item => item.Id).ToArray())
        {
            sessions.SelectSession(session);
            foreach (var (key, modifiers, expected) in Shortcuts())
            {
                Check(
                    TryResolve(window, key, modifiers, out var commandId),
                    $"Skrót podglądu nie jest rozpoznawany w sesji {session}: {Describe(key, modifiers)}.");
                Check(
                    commandId == expected,
                    $"Sesja {session}: {Describe(key, modifiers)} dał polecenie {commandId} zamiast {expected}.");
            }
        }
    }

    /// <summary>
    /// Menu nie moze klamac: jesli skrot dziala w sesji, pozycja menu musi byc
    /// w niej widoczna.
    /// </summary>
    private static void TestMenuPokazujePodgladyWKazdejSesji(MainWindow window)
    {
        var sessions = Sessions(window);
        var items = new (string Name, string CommandId)[]
        {
            ("ActiveRadioRecordingsViewMenuItem", CommandIds.ViewActiveRadioRecordings),
            ("RecordedRadioFilesViewMenuItem", CommandIds.ViewRecordedRadioFiles),
            ("PodcastInboxViewMenuItem", CommandIds.ViewPodcastInbox)
        };

        foreach (var session in sessions.Sessions.Select(item => item.Id).ToArray())
        {
            sessions.SelectSession(session);
            Invoke(window, "UpdateFileMenuForCurrentSession");
            foreach (var (name, commandId) in items)
            {
                var item = (MenuItem)window.FindName(name);
                Check(
                    item.Visibility == Visibility.Visible,
                    $"Sesja {session}: pozycja menu {name} jest ukryta, choć skrót działa.");
                Check(
                    TryResolve(window, KeyOf(commandId), ModifiersOf(commandId), out _),
                    $"Sesja {session}: menu pokazuje {name}, ale skrót nie działa.");
            }
        }
    }

    /// <summary>
    /// Warstwa dostepnosci polecen (paleta, menu, pomoc) musi zgadzac sie z
    /// przelacznikiem, a nie z dawnym ograniczeniem do jednej sesji.
    /// </summary>
    private static void TestDostepnoscPolecenZgodnaZPrzelacznikiem(MainWindow window, PersistedState state)
    {
        state.Settings.GlobalTransientPreviews = true;
        var sessions = Sessions(window);
        foreach (var session in sessions.Sessions.Select(item => item.Id).ToArray())
        {
            sessions.SelectSession(session);
            foreach (var commandId in PreviewCommands())
            {
                Check(
                    (bool)Invoke(window, "IsTransientPreviewAvailable", commandId)!,
                    $"Polecenie {commandId} jest niedostępne w sesji {session} przy włączonym przełączniku.");
                Check(
                    (bool)Invoke(window, "CommandVisibleInPalette", commandId)!,
                    $"Paleta ukrywa {commandId} w sesji {session}, choć skrót działa.");
            }
        }
    }

    private static void TestWylaczonyPrzelacznikZwezaDoSesjiMacierzystych(MainWindow window, PersistedState state)
    {
        state.Settings.GlobalTransientPreviews = false;
        var sessions = Sessions(window);
        try
        {
            sessions.SelectSession("tidal");
            Check(
                !(bool)Invoke(window, "IsTransientPreviewAvailable", CommandIds.ViewActiveRadioRecordings)!,
                "Wyłączony przełącznik nadal udostępnia nagrywane stacje w TIDAL-u.");
            Check(
                !(bool)Invoke(window, "IsTransientPreviewAvailable", CommandIds.ViewPodcastInbox)!,
                "Wyłączony przełącznik nadal udostępnia nowe odcinki w TIDAL-u.");

            Invoke(window, "UpdateFileMenuForCurrentSession");
            Check(
                ((MenuItem)window.FindName("PodcastInboxViewMenuItem")).Visibility == Visibility.Collapsed,
                "Wyłączony przełącznik zostawił w menu TIDAL-a pozycję nowych odcinków.");

            sessions.SelectSession("radio");
            Check(
                (bool)Invoke(window, "IsTransientPreviewAvailable", CommandIds.ViewActiveRadioRecordings)!,
                "Nagrywane stacje muszą działać w sesji Radio nawet przy wyłączonym przełączniku.");
            sessions.SelectSession("local");
            Check(
                (bool)Invoke(window, "IsTransientPreviewAvailable", CommandIds.ViewRecordedRadioFiles)!,
                "Historia nagrywania musi działać w Plikach lokalnych przy wyłączonym przełączniku.");
        }
        finally
        {
            state.Settings.GlobalTransientPreviews = true;
            Invoke(window, "UpdateFileMenuForCurrentSession");
        }
    }

    /// <summary>
    /// Sedno zgloszenia: podglad wywolany z obcej sesji, a Escape wraca do tej
    /// samej sesji i widoku. Mierzymy FAKTYCZNE handlery okna.
    /// </summary>
    private static void TestEscapeWracaDoSesjiIWidokuWywolania(MainWindow window, PersistedState state)
    {
        state.Settings.GlobalTransientPreviews = true;
        var sessions = Sessions(window);
        sessions.SelectSession("tidal");
        Invoke(window, "UpdateFileMenuForCurrentSession");
        var viewBefore = CurrentView(window);

        // Wejscie w podglad przez prawdziwa sciezke polecenia.
        Invoke(window, "ExecuteCommand", CommandIds.ViewActiveRadioRecordings);
        Pump();

        var navigator = Navigator(window);
        Check(
            navigator.ActivePreview == TransientPreviewKind.ActiveRadioRecordings,
            "Wejście w podgląd nie zapamiętało aktywnego podglądu.");
        Check(
            navigator.Origin?.SessionId == "tidal",
            $"Podgląd zapamiętał sesję {navigator.Origin?.SessionId} zamiast tidal.");
        Check(
            sessions.Current.Id == "radio",
            $"Podgląd nagrywanych stacji nie przeszedł do sesji radio (jest {sessions.Current.Id}).");

        // Przelaczenie na drugi podglad NIE MOZE zgubic pierwotnego miejsca.
        Invoke(window, "ExecuteCommand", CommandIds.ViewPodcastInbox);
        Pump();
        Check(
            Navigator(window).Origin?.SessionId == "tidal",
            "Przełączenie między podglądami zgubiło pierwotne miejsce powrotu.");

        // Escape przez FAKTYCZNY handler okna.
        Check(
            (bool)Invoke(window, "TryHandleTransientPreviewEscape")!,
            "Escape nie został obsłużony przez wspólne podglądy.");
        Pump();
        Check(
            sessions.Current.Id == "tidal",
            $"Escape wrócił do sesji {sessions.Current.Id} zamiast tidal.");
        Check(
            CurrentView(window) == viewBefore,
            $"Escape wrócił do widoku {CurrentView(window)} zamiast {viewBefore}.");
        Check(
            !Navigator(window).HasPendingReturn,
            "Po powrocie cel Escape musi zniknąć.");

        // Jawna nawigacja uniewaznia cel powrotu.
        Invoke(window, "ExecuteCommand", CommandIds.ViewRecordedRadioFiles);
        Pump();
        Check(Navigator(window).HasPendingReturn, "Podgląd nie zapamiętał miejsca powrotu.");
        Invoke(window, "ExecuteCommand", CommandIds.SessionSlot(1));
        Pump();
        Check(
            !Navigator(window).HasPendingReturn,
            "Jawne Ctrl+cyfra nie unieważniło nieaktualnego celu powrotu.");
        Check(
            !(bool)Invoke(window, "TryHandleTransientPreviewEscape")!,
            "Po jawnej nawigacji Escape nadal skacze w nieaktualne miejsce.");
    }

    private static void TestOpisPomocyBezPodwojnegoSkrotu(MainWindow window)
    {
        Sessions(window).SelectSession("tidal");
        foreach (var (key, modifiers, commandId) in Shortcuts())
        {
            var args = new object?[] { key, modifiers, null };
            var handled = (bool)typeof(MainWindow)
                .GetMethod("TryDescribeTransientPreviewShortcut", Private)!
                .Invoke(window, args)!;
            Check(handled, $"Pomoc kontekstowa nie opisuje {Describe(key, modifiers)}.");
            var description = (string)args[2]!;
            Check(description.Length > 0, $"Pusty opis skrótu {Describe(key, modifiers)}.");
            Check(
                !description.Contains("Alt+", StringComparison.OrdinalIgnoreCase)
                && !description.Contains("Ctrl+", StringComparison.OrdinalIgnoreCase),
                $"Opis {commandId} powtarza skrót, który czytnik i tak przeczyta: „{description}”.");
        }

        Invoke(window, "UpdateFileMenuForCurrentSession");
        foreach (var name in new[]
                 {
                     "ActiveRadioRecordingsViewMenuItem",
                     "RecordedRadioFilesViewMenuItem",
                     "PodcastInboxViewMenuItem"
                 })
        {
            var item = (MenuItem)window.FindName(name);
            var automationName = AutomationProperties.GetName(item) ?? string.Empty;
            Check(
                !automationName.Contains("Alt+", StringComparison.OrdinalIgnoreCase)
                && !automationName.Contains("Ctrl+", StringComparison.OrdinalIgnoreCase),
                $"Nazwa pozycji {name} powtarza skrót obok AcceleratorKey: „{automationName}”.");
            Check(
                (AutomationProperties.GetAcceleratorKey(item) ?? string.Empty).Length > 0,
                $"Pozycja {name} zgubiła skrót dla czytnika.");
        }
    }

    /// <summary>
    /// Samo ogladanie podgladu nie moze ruszac transportu: zadnego stop, pause
    /// ani start.
    /// </summary>
    private static void TestPodgladNieRuszaTransportu(MainWindow window)
    {
        var body = SourceOf("MainWindow.TransientPreviews.cs");
        foreach (var forbidden in new[]
                 {
                     "StopPlayback", "PausePlayback", "TogglePlayPause",
                     "StartPlayback", "BeginPlayback", "PlayItem"
                 })
        {
            Check(
                !body.Contains(forbidden, StringComparison.Ordinal),
                $"Wspólne podglądy ruszają transport wywołaniem {forbidden}.");
        }

        var navigator = Navigator(window);
        navigator.Reset();
        Check(
            !navigator.HasPendingReturn && navigator.ActivePreview == TransientPreviewKind.None,
            "Reset pamięci powrotu nie wyczyścił stanu.");
    }

    // ------------------------------------------------------------- narzedzia

    private static (PersistedState State, ConfigurationStore Store) Prepare(string root)
    {
        var store = new ConfigurationStore(
            Path.Combine(root, "state.json"),
            Path.Combine(root, "library.db"),
            Path.Combine(root, "podcasts.db"));
        var state = store.LoadOrCreate();
        state.Settings.Updates.CheckAutomatically = false;
        state.Settings.Updates.InstallOnExit = false;
        state.Settings.GlobalTransientPreviews = true;
        // Zero kont i zero sieci: czyscimy poswiadczenia i migawki danych.
        state.Tidal.ClientId = string.Empty;
        state.Spotify.ClientId = string.Empty;
        state.Tidal.CachedCollectionItems.Clear();
        state.Spotify.CachedCollectionItems.Clear();
        state.Podcasts.Subscriptions.Clear();
        state.Podcasts.Episodes.Clear();
        state.LocalMedia.FolderSources.Clear();
        state.Radio.Stations.Clear();
        state.Podcasts.DownloadsFolder = Path.Combine(root, "podcasts");
        state.Radio.RecordingsFolder = Path.Combine(root, "recordings");
        return (state, store);
    }

    private static (Key Key, ModifierKeys Modifiers, string CommandId)[] Shortcuts() =>
    [
        (Key.R, ModifierKeys.Alt, CommandIds.ViewActiveRadioRecordings),
        (Key.R, ModifierKeys.Alt | ModifierKeys.Shift, CommandIds.ViewRecordedRadioFiles),
        (Key.I, ModifierKeys.Control, CommandIds.ViewPodcastInbox)
    ];

    private static string[] PreviewCommands() =>
    [
        CommandIds.ViewActiveRadioRecordings,
        CommandIds.ViewRecordedRadioFiles,
        CommandIds.ViewPodcastInbox
    ];

    private static Key KeyOf(string commandId) =>
        commandId == CommandIds.ViewPodcastInbox ? Key.I : Key.R;

    private static ModifierKeys ModifiersOf(string commandId) => commandId switch
    {
        CommandIds.ViewPodcastInbox => ModifierKeys.Control,
        CommandIds.ViewRecordedRadioFiles => ModifierKeys.Alt | ModifierKeys.Shift,
        _ => ModifierKeys.Alt
    };

    private static bool TryResolve(MainWindow window, Key key, ModifierKeys modifiers, out string commandId)
    {
        var args = new object?[] { key, modifiers, null };
        var handled = (bool)typeof(MainWindow)
            .GetMethod("TryResolveTransientPreviewShortcut", Private)!
            .Invoke(window, args)!;
        commandId = (string)(args[2] ?? string.Empty);
        return handled;
    }

    private static string Describe(Key key, ModifierKeys modifiers) =>
        $"{modifiers}+{key}";

    private static SessionManager Sessions(MainWindow window) =>
        (SessionManager)typeof(MainWindow).GetField("_sessions", Private)!.GetValue(window)!;

    private static TransientPreviewNavigator Navigator(MainWindow window) =>
        (TransientPreviewNavigator)typeof(MainWindow)
            .GetField("_transientPreviews", Private)!.GetValue(window)!;

    private static string CurrentView(MainWindow window) =>
        (string)typeof(MainWindow).GetField("_currentView", Private)!.GetValue(window)!;

    private static object? Invoke(MainWindow window, string name, params object?[] arguments) =>
        typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, arguments);

    /// <summary>Domyka zaległe operacje dyspozytora, żeby pomiar widział skutki.</summary>
    private static void Pump() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static string SourceOf(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var src = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(src))
            {
                var found = Directory.GetFiles(src, name, SearchOption.AllDirectories);
                if (found.Length > 0) return File.ReadAllText(found[0]);
            }
            directory = directory.Parent;
        }
        throw new Exception($"Nie znaleziono pliku źródłowego {name}.");
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
