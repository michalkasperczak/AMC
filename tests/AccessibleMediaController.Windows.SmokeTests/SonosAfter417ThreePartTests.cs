using System.Net.Http;
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
/// ZGLOSZENIE PO 4.1.7, TRZY CZESCI. Pomiar odtwarza DOKLADNIE to, co zglosil
/// uzytkownik, na PRAWDZIWYM oknie i PRAWDZIWYCH klawiszach - nie na metodach
/// zaplecza.
///
/// CZESC 1 - PIERWSZE WCZYTANIE GRUP I F5:
///   1a) ZWYKLE WEJSCIE PO STARCIE (produkcyjna droga Window_ContentRendered,
///       nie recznie wolane EnterSonosSessionAsync) ma wczytac grupy. Zmierzone
///       jest RZECZYWISTE ZADANIE HTTP (households + groups) i stan po
///       odpowiedzi, a nie sama mowa.
///   1b) F5 w sesji Sonos ma odswiezyc TYM SAMYM torem, co menu Plik
///       (CommandIds.RefreshSonosGroups), a nie wpadac w RefreshLocalLibrary.
///   1c) Ctrl+F5 POZOSTAJE wyborem celu i przy pustej topologii NIE zapetla
///       odmow - okno celu powstaje z informacja.
///
/// CZESC 2 - TRANSPORT SPACJA/ENTER W PODLISCIE:
///   Spacja na liscie modalu ma pauzowac/wznawiac AKTUALNY material istniejaca
///   droga ExecuteSonosCommandAsync(PlayPause). Enter ZOSTAJE uruchomieniem
///   wskazanej pozycji - nie zamieniamy go w toggle.
///
/// CZESC 3 - PRESETY CTRL+SHIFT+CYFRA Z PODLISTY:
///   Modal wylacza okno glowne, wiec router presetow go nie widzi. Gest ma
///   dojsc do ISTNIEJACEGO ExecuteCommand(RadioPreset(slot)) i NIE zwijac
///   podlisty: wiersz, zaznaczenie i fokus zostaja.
///
/// CZEGO TEN POMIAR NIE DOWODZI: niczego o prawdziwym koncie ani glosniku
/// Michala. Transport jest syntetyczny (RecordingHandler), klawisze sa
/// SYNTETYCZNE WPF na realnym zrodle prezentacji z RZECZYWISCIE wcisnietym
/// modyfikatorem przez SetKeyboardState. Mowa NVDA idzie osobno.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunAfter417ThreeParts()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { MeasureAfter417ThreeParts(); }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(420)))
        {
            throw new Exception("Limit pomiaru zgłoszenia po 4.1.7");
        }

        if (failure is not null) throw failure;
        Console.WriteLine(
            "OK: (1) start wczytuje grupy + F5 tym samym torem co menu + Ctrl+F5 bez pętli odmów, "
            + "(2) Spacja transport / Enter uruchomienie w podliście, "
            + "(3) Ctrl+Shift+cyfra preset z podlisty bez utraty wiersza i fokusu");
    }

    private static void MeasureAfter417ThreeParts()
    {
        MeasureStartupLoadsGroups();
        Console.Error.WriteLine("CZESC 1a: zwykle wejscie po starcie wczytuje grupy - OK");
        MeasureF5RefreshesSonosSameRouteAsMenu();
        Console.Error.WriteLine("CZESC 1b: F5 odswieza tym samym torem co menu - OK");
        MeasureCtrlF5StaysTargetChoiceWithoutRefusalLoop();
        Console.Error.WriteLine("CZESC 1c: Ctrl+F5 to wybor celu, bez petli odmow - OK");
        MeasureSublistSpaceTransportAndEnterPlay();
        Console.Error.WriteLine("CZESC 2: Spacja transport, Enter uruchomienie - OK");
        MeasureSublistPresetShortcutKeepsList();
        Console.Error.WriteLine("CZESC 3: Ctrl+Shift+cyfra preset z podlisty - OK");
    }

    /// <summary>
    /// CZESC 1a. ZWYKLE WEJSCIE PO STARCIE: ostatnia sesja to Sonos, okno
    /// pokazuje sie i odpala PRODUKCYJNY Window_ContentRendered. Nikt tu nie
    /// wola EnterSonosSessionAsync recznie - wlasnie tego brakowalo.
    ///
    /// MIERZYMY ZADANIA I STAN, NIE MOWE: GET households, GET groups oraz
    /// niepusta lista grup w oknie.
    /// </summary>
    private static void MeasureStartupLoadsGroups()
    {
        using var harness = RealHarness.Create();
        var window = harness.Window;

        // OSTATNIA SESJA = SONOS i WYBRANY DOM, jak u uzytkownika po restarcie.
        window.StateForTests.Settings.LastSessionId = "sonos";
        window.StateForTests.Sonos.SelectedHouseholdId = HouseholdId;
        // Przebudowa sesji z zapisanego stanu - ta sama droga, ktora startup
        // wykonuje PRZED ContentRendered.
        typeof(MainWindow).GetMethod("RebuildCore", Instance)!.Invoke(window, null);

        window.ShowInTaskbar = false;
        window.Show();
        harness.PumpUntil(
            () => window.IsLoaded && PresentationSource.FromVisual(window) is not null,
            "okno główne się nie pokazało");
        window.Activate();
        harness.PumpUntil(() => window.IsActive, "okno główne nie stało się aktywne");

        if (!MainWindow.IsSonosSession(window.SessionsForTests.Current.Id))
        {
            throw new Exception("Aparatura nie wystartowała w sesji Sonos - pomiar nie dotyczyłby "
                + "zgłoszenia. Bieżąca sesja: " + window.SessionsForTests.Current.Id);
        }

        var before = harness.Handler.Requests.Count;

        // PRODUKCYJNY ContentRendered - dokladnie to, co odpala sie po starcie.
        (typeof(MainWindow).GetMethod("Window_ContentRendered", Instance)
            ?? throw new Exception("Nie ma prawdziwego Window_ContentRendered."))
            .Invoke(window, [window, EventArgs.Empty]);

        // Czekamy na RZECZYWISTE grupy z RZECZYWISTEGO odczytu, nie na mowe.
        harness.PumpUntil(
            () => window.SonosGroupRows.Any(row => row.GroupId == GroupId),
            TimeSpan.FromSeconds(20),
            "ZGŁOSZONY BŁĄD 1a ODTWORZONY: zwykłe wejście po starcie NIE wczytało grup Sonos "
                + "(lista celów sterowania pozostała pusta)");

        var fresh = harness.Handler.Requests.Skip(before).ToList();
        if (!fresh.Any(w => w.Uri.AbsoluteUri.Contains("households", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Start nie wykonał GET households. Żądania: "
                + string.Join(" | ", fresh.Select(w => w.Method + " " + w.Uri.AbsolutePath)));
        }

        if (!fresh.Any(w => w.Uri.AbsoluteUri.Contains("groups", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Start nie wykonał GET groups. Żądania: "
                + string.Join(" | ", fresh.Select(w => w.Method + " " + w.Uri.AbsolutePath)));
        }

        if (harness.Handler.Posts.Count != 0)
        {
            throw new Exception($"Samo wczytanie grup po starcie wysłało {harness.Handler.Posts.Count} POST.");
        }

        // BEZ DUPLIKATOW: jedno wejscie to JEDEN odczyt listy domow.
        var householdGets = fresh.Count(w =>
            w.Uri.AbsolutePath.EndsWith("households", StringComparison.OrdinalIgnoreCase));
        if (householdGets > 1)
        {
            throw new Exception($"Start zdublował odczyt domów: {householdGets} GET households.");
        }
    }

    /// <summary>
    /// CZESC 1b. F5 w sesji Sonos. PRAWDZIWY handler okna glownego dostaje
    /// PRAWDZIWY klawisz bez modyfikatorow.
    ///
    /// MIERZYMY TOR: rzeczywiste GET-y Sonos po F5, zestawione z tym, co robi
    /// MENU PLIK. Bez poprawki F5 wpada w RefreshLocalLibrary i Sonos nie
    /// dostaje ani jednego zadania.
    /// </summary>
    private static void MeasureF5RefreshesSonosSameRouteAsMenu()
    {
        using var harness = RealHarness.Create();
        harness.Enter();

        // ODNIESIENIE: co robi MENU PLIK (istniejacy, odebrany tor).
        var beforeMenu = harness.Handler.Requests.Count;
        harness.ExecuteCommand(CommandIds.RefreshSonosGroups);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
        var menuReads = CountSonosReads(harness, beforeMenu);
        if (menuReads == 0)
        {
            throw new Exception("Odniesienie nieważne: menu Plik nie wykonało żadnego odczytu Sonos.");
        }

        // TERAZ F5 - FOKUS NA LISCIE, zadnych modyfikatorow, prawdziwy handler.
        harness.FocusMediaListForMeasurement();
        var beforeF5 = harness.Handler.Requests.Count;
        harness.PressKeyOnMainWindow(Key.F5, ModifierKeys.None);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(800));

        var f5Reads = CountSonosReads(harness, beforeF5);
        if (f5Reads == 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD 1b ODTWORZONY: F5 w sesji Sonos NIE odświeżyło grup - "
                + "zero odczytów Sonos, gest poszedł do Biblioteki lokalnej. "
                + "Ostatnie zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(4)));
        }

        // F5 NIE MA PRAWA wysylac POST ani otwierac wyboru celu.
        if (harness.Handler.Posts.Count != 0)
        {
            throw new Exception($"F5 wysłało {harness.Handler.Posts.Count} POST - odświeżenie nie gra.");
        }

        if (harness.Window.SonosTargetWindowsCreatedForTests != 0)
        {
            throw new Exception("F5 otworzyło okno wyboru celu - to należy do Ctrl+F5, nie do F5.");
        }
    }

    private static int CountSonosReads(RealHarness harness, int skip) =>
        harness.Handler.Requests.Skip(skip).Count(w =>
            w.Method == "GET"
            && (w.Uri.AbsoluteUri.Contains("households", StringComparison.OrdinalIgnoreCase)
                || w.Uri.AbsoluteUri.Contains("groups", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// CZESC 1c. Ctrl+F5 POZOSTAJE wyborem celu (znaczenia skrotu nie zmieniamy)
    /// i przy BRAKU topologii daje UZYWALNE okno z informacja, a nie petle odmow
    /// odsylajacych znowu do Ctrl+F5.
    /// </summary>
    private static void MeasureCtrlF5StaysTargetChoiceWithoutRefusalLoop()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        harness.ClearSonosTargetForTests();
        harness.AssertNoSonosTargetForMeasurement();

        var rowsShown = -1;
        var introShown = string.Empty;
        harness.Window.PresentSonosTargetOverrideForTests = dialog =>
        {
            dialog.ShowInTaskbar = false;
            rowsShown = dialog.RowCountForTests;
            introShown = dialog.IntroductionForTests + " " + dialog.StatusForTests;
        };
        try
        {
            harness.PressKeyOnMainWindow(Key.F5, ModifierKeys.Control);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(800));
            if (harness.Window.LastSonosLibraryTaskForTests is { } pending) harness.Pump(pending);
        }
        finally
        {
            harness.Window.PresentSonosTargetOverrideForTests = null;
        }

        if (harness.Window.SonosTargetWindowsCreatedForTests == 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD 1c ODTWORZONY: Ctrl+F5 nie dało okna wyboru celu - "
                + "sama odmowa. Zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(4)));
        }

        // UZYWALNY WYBOR: albo sa wiersze, albo okno MOWI, co jest nie tak.
        if (rowsShown <= 0 && string.IsNullOrWhiteSpace(introShown))
        {
            throw new Exception("Ctrl+F5 pokazało PUSTĄ listę bez żadnej informacji o wczytywaniu/błędzie.");
        }
    }

    /// <summary>
    /// CZESC 2. SPACJA w podlisce ulubionych. Modal wylacza okno glowne, wiec
    /// bez przekazania gestu nic sie nie dzieje.
    ///
    /// MIERZYMY RZECZYWISTE ZADANIE: POST transportu grupy, a nie zapowiedz.
    /// ENTER mierzymy OSOBNO i musi zostac uruchomieniem wskazanej pozycji,
    /// zeby nikt go nie zamienil w toggle.
    /// </summary>
    private static void MeasureSublistSpaceTransportAndEnterPlay()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        // STAN ODCZYTANY: bramka polecen liczy sie z ODCZYTU, nie z nazwy gestu.
        // Bez tego Spacja odmowilaby "Stan Sonos nie został odczytany".
        harness.PrimeSonosPlaybackStateForMeasurement();

        var transportPosts = 0;
        var playPosts = 0;
        var listAlive = false;
        var focusInList = false;

        harness.RunFavoritesModal(dialog =>
        {
            var list = (ListBox)dialog.FindName("FavoritesList")!;
            harness.PumpUntil(() => list.Items.Count >= 2, "modal ulubionych nie wczytał wierszy");
            list.SelectedIndex = 1;
            list.Focus();
            Keyboard.Focus(list);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            var selectedBefore = dialog.SelectedFavoriteForTests?.Id;

            // --- SPACJA: TRANSPORT AKTUALNEGO MATERIALU ---
            var postsBefore = harness.Handler.Posts.Count;
            SendKeyWithModifiers(dialog, Key.Space, ModifierKeys.None);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(600));

            transportPosts = harness.Handler.Posts.Skip(postsBefore)
                .Count(p => p.Uri.AbsolutePath.Contains("playback", StringComparison.OrdinalIgnoreCase));
            listAlive = dialog.IsVisible;
            focusInList = list.IsKeyboardFocusWithin;

            if (!string.Equals(dialog.SelectedFavoriteForTests?.Id, selectedBefore, StringComparison.Ordinal))
            {
                throw new Exception("Spacja zmieniła zaznaczony wiersz podlisty.");
            }

            // --- ENTER: URUCHOMIENIE WSKAZANEJ POZYCJI (bez zmiany znaczenia) ---
            var beforeEnter = harness.Handler.Posts.Count;
            SendKeyWithModifiers(dialog, Key.Enter, ModifierKeys.None);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
            if (dialog.LastPlayTaskForTests is { } play) harness.Pump(play);
            playPosts = harness.Handler.Posts.Count - beforeEnter;
        });

        if (transportPosts == 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD 2 ODTWORZONY: Spacja w podliście Sonos NIE wysłała "
                + "żadnego polecenia transportu (zero POST playback). "
                + "Zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(5)));
        }

        if (transportPosts > 1)
        {
            throw new Exception($"Spacja wysłała {transportPosts} POST transportu - duplikat.");
        }

        if (!listAlive) throw new Exception("Spacja zamknęła podlistę - transport nie zwija listy.");
        if (!focusInList) throw new Exception("Spacja zabrała fokus z listy podlisty.");

        if (playPosts == 0)
        {
            throw new Exception("REGRESJA: Enter przestał uruchamiać wskazaną pozycję ulubionych "
                + "(zero POST) - Enter nie może zamienić się w toggle transportu.");
        }
    }

    /// <summary>
    /// CZESC 3. CTRL+SHIFT+CYFRA w podlisce. Slot presetu jest NIEPUSTY, wiec
    /// gest ma realny skutek; mierzymy RZECZYWISTE wykonanie istniejacego
    /// polecenia presetu, zachowanie wiersza, zywa liste i fokus.
    ///
    /// SLOT 10 (Ctrl+Shift+0) i 11 (Ctrl+Shift+minus) mierzymy OSOBNO: mapa
    /// presetow ma 0, minus i plus, a sama cyfra 1-9 nie dowodzilaby ich obslugi.
    /// </summary>
    private static void MeasureSublistPresetShortcutKeepsList()
    {
        foreach (var (key, slot, label) in new[]
        {
            (Key.D3, 3, "Ctrl+Shift+3"),
            (Key.D0, 10, "Ctrl+Shift+0"),
            (Key.OemMinus, 11, "Ctrl+Shift+minus")
        })
        {
            using var harness = RealHarness.Create();
            harness.Enter();
            var expectedUri = harness.AssignSonosFavoritePresetToSlotForMeasurement(slot);

            var listAlive = false;
            var focusInList = false;
            var rowKept = false;
            var presetPosts = Array.Empty<string>();

            harness.RunFavoritesModal(dialog =>
            {
                var list = (ListBox)dialog.FindName("FavoritesList")!;
                harness.PumpUntil(() => list.Items.Count >= 2, "modal ulubionych nie wczytał wierszy");
                list.SelectedIndex = 1;
                list.Focus();
                Keyboard.Focus(list);
                harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
                var before = dialog.SelectedFavoriteForTests?.Id;

                var postsBefore = harness.Handler.Posts.Count;
                SendKeyWithModifiers(dialog, key, ModifierKeys.Control | ModifierKeys.Shift);
                harness.PumpQuietly(TimeSpan.FromMilliseconds(700));

                presetPosts = harness.Handler.Posts.Skip(postsBefore)
                    .Select(p => p.Uri.AbsolutePath).ToArray();
                listAlive = dialog.IsVisible;
                focusInList = list.IsKeyboardFocusWithin;
                rowKept = string.Equals(
                    dialog.SelectedFavoriteForTests?.Id, before, StringComparison.Ordinal);
            });

            if (presetPosts.Length == 0)
            {
                throw new Exception($"ZGŁOSZONY BŁĄD 3 ODTWORZONY: {label} w podliście Sonos NIE wykonało "
                    + "polecenia presetu - gest nie opuścił modalu (zero POST). "
                    + "Zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(5)));
            }

            if (!presetPosts.Any(p => p.Contains(expectedUri, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception($"{label} wysłało POST, ale nie pod adres presetu slotu {slot}. "
                    + "Adresy: " + string.Join(" | ", presetPosts) + "; oczekiwano fragmentu: " + expectedUri);
            }

            if (!listAlive) throw new Exception($"{label} zamknęło podlistę - preset nie zwija listy.");
            if (!focusInList) throw new Exception($"{label} zabrało fokus z listy.");
            if (!rowKept) throw new Exception($"{label} zmieniło zaznaczony wiersz podlisty.");
        }
    }

    /// <summary>
    /// KLAWISZ do PRAWDZIWEGO handlera okna, z RZECZYWISCIE wcisnietymi
    /// modyfikatorami w stanie watku. Bez SetKeyboardState KeyEventArgs NIE
    /// jest tym gestem i pomiar mierzylby samą cyfrę.
    /// </summary>
    internal static void SendKeyWithModifiers(Window window, Key key, ModifierKeys modifiers)
    {
        var source = PresentationSource.FromVisual(window)
            ?? throw new Exception("Okno nie ma powierzchni prezentacji.");
        var target = Keyboard.FocusedElement as UIElement ?? window;
        var previous = new byte[256];
        if (!GetKeyboardState(previous)) throw new Exception("Nie można odczytać stanu klawiatury.");
        var pressed = new byte[256];
        if ((modifiers & ModifierKeys.Control) != 0) pressed[0x11] = pressed[0xA2] = 0x80;
        if ((modifiers & ModifierKeys.Shift) != 0) pressed[0x10] = pressed[0xA0] = 0x80;
        try
        {
            if (!SetKeyboardState(pressed)) throw new Exception("Nie można ustawić stanu klawiatury wątku.");
            if (Keyboard.Modifiers != modifiers)
            {
                throw new Exception($"Aparatura nie dała modyfikatorów {modifiers} "
                    + $"(jest {Keyboard.Modifiers}) - pomiar mierzyłby inny gest.");
            }

            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
        }
        finally { SetKeyboardState(previous); }
    }

}
