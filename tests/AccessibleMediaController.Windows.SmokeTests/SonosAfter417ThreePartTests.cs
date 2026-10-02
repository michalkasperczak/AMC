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
        // UCZCIWE PODSUMOWANIE: oglaszamy WYLACZNIE zmierzone czesci. Stare,
        // stale zdanie o wszystkich pieciu punktach przy --parts=1c kazalo
        // czytelnikowi uwierzyc w odbior, ktorego pomiar nie wykonal.
        var measured = string.Join(", ", After417PartsToMeasure.Select(DescribeAfter417Part));
        Console.WriteLine($"({After417PartsToMeasure.Length} sprawdzeń zgłoszenia po 4.1.7)");
        Console.WriteLine("OK: zmierzone części "
            + string.Join("/", After417PartsToMeasure) + " - " + measured);
    }

    /// <summary>Co DOKLADNIE dowodzi dana czesc - zero obietnic o niemierzonych.</summary>
    private static string DescribeAfter417Part(string part) => part switch
    {
        "1a" => "(1a) start wczytuje grupy",
        "1b" => "(1b) F5 odświeża tym samym torem co menu Plik",
        "1c" => "(1c) jedno Ctrl+F5 daje używalne grupy po opóźnionym odczycie, F5 w oknie celu "
            + "odświeża bez utraty zaznaczenia, bez pętli odmów i bez spóźnionego okna",
        "1d" => "(1d) okno celu otwarte W TRAKCIE trwającego odczytu menu-F5 uzupełnia się BEZ "
            + "kolejnego F5; F5 w oknie zachowuje fokus na TYM SAMYM rzeczywistym wierszu, "
            + "nie kradnie fokusu z przycisku, a Cancel przed zwolnieniem GET nie wpuszcza wyniku",
        "2" => "(2) Spacja transport / Enter uruchomienie w podliście",
        "3" => "(3) Ctrl+Shift+cyfra preset z podlisty bez utraty wiersza i fokusu",
        _ => "(" + part + ") nieopisana część"
    };


    private static void MeasureAfter417ThreeParts()
    {
        foreach (var part in After417PartsToMeasure)
        {
            switch (part)
            {
                case "1a": MeasureStartupLoadsGroups(); break;
                case "1b": MeasureF5RefreshesSonosSameRouteAsMenu(); break;
                case "1c": MeasureCtrlF5StaysTargetChoiceWithoutRefusalLoop(); break;
                case "1d": MeasureTargetWindowJoinsRunningReadAndKeepsRowFocus(); break;
                case "2": MeasureSublistSpaceTransportAndEnterPlay(); break;
                case "3": MeasureSublistPresetShortcutKeepsList(); break;
                default: throw new Exception("Nieznana część pomiaru: " + part);
            }

            Console.Error.WriteLine("CZESC " + part + ": zmierzona - OK");
        }
    }

    /// <summary>
    /// KTORE CZESCI mierzyc. Domyslnie WSZYSTKIE po kolei; zawezenie sluzy tylko
    /// do ODTWORZENIA kazdego zgloszonego punktu OSOBNO, zeby pierwszy czerwony
    /// nie przykryl pozostalych.
    /// </summary>
    internal static string[] After417PartsToMeasure { get; set; } = ["1a", "1b", "1c", "1d", "2", "3"];


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
        try
        {
            harness.PumpUntil(
                () => window.SonosGroupRows.Any(row => row.GroupId == GroupId),
                TimeSpan.FromSeconds(20),
                "ZGŁOSZONY BŁĄD 1a ODTWORZONY: zwykłe wejście po starcie NIE wczytało grup Sonos "
                    + "(lista celów sterowania pozostała pusta)");
        }
        catch (Exception e)
        {
            // DIAGNOZA Z DANYCH, nie z domyslu: co faktycznie poleclalo i jaki
            // jest stan po odpowiedzi. Inaczej nie wiadomo, czy brakuje wejscia,
            // czy odczyt poszedl i odpadl na granicy konta/biletu.
            var seen = harness.Handler.Requests.Skip(before)
                .Select(w => w.Method + " " + w.Uri.AbsolutePath).ToList();
            throw new Exception(e.Message
                + " | ŻĄDANIA PO ContentRendered: "
                + (seen.Count == 0 ? "ŻADNYCH" : string.Join(" ; ", seen))
                + " | wiersze=" + window.SonosGroupRows.Count
                + " | porzucenia konta=" + window.SonosAccountChangeDropsForTests);
        }

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
    /// i w JEDNYM OTWARCIU daje UZYWALNY wybor grupy, nawet gdy topologii jeszcze
    /// nie ma - bez petli odmow odsylajacych znowu do Ctrl+F5.
    ///
    /// CO TEN POMIAR MIERZY, A CZEGO NIE: odczyt jest PRAWDZIWIE OPOZNIONY
    /// (wstrzymany GET /groups istniejaca bramka RecordingHandler.HoldNextGet),
    /// wiec sprawdzamy RZECZYWISTE WIERSZE W POKAZANYM OKNIE PO odpowiedzi, a nie
    /// sam fakt, ze jakis GET poleciał. Samo CountSonosReads przechodzilo takze
    /// wtedy, gdy po odczycie okno zostawalo puste - czyli przy bledzie, ktory
    /// uzytkownik zglosil.
    /// </summary>
    private static void MeasureCtrlF5StaysTargetChoiceWithoutRefusalLoop()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        harness.ClearSonosTargetForTests();
        harness.AssertNoSonosTargetForMeasurement();

        // PRAWDZIWIE ASYNCHRONICZNY ODCZYT: GET grup WISI, dopoki pomiar go nie
        // zwolni. Bez tego "poprawka", ktora nie czeka na odczyt, przechodzila
        // przypadkiem - syntetyczna chmura odpowiadala natychmiast.
        var heldGroups = harness.Handler.HoldNextGet("/groups");

        var readsBefore = harness.Handler.Requests.Count;
        var rowsWhileLoading = -1;
        var speakingWhileLoading = string.Empty;
        var confirmWhileLoading = true;
        var rowsAfterRead = -1;
        var confirmAfterRead = false;
        var highlightedAfterRead = string.Empty;
        var windowsWhileLoading = -1;
        var readsWhileLoading = -1;
        var refreshRequests = -1;
        var rowsAfterWindowF5 = -1;
        var highlightedAfterWindowF5 = string.Empty;
        var readsAfterWindowF5 = -1;
        var postsInWindow = -1;

        harness.Window.PresentSonosTargetOverrideForTests = dialog =>
        {
            // PRAWDZIWE ZRODLO PREZENTACJI: bez niego klawisz F5 w oknie celu nie
            // mialby gdzie pojsc, a pomiar mierzylby metode, nie gest.
            dialog.ShowInTaskbar = false;
            dialog.Show();
            harness.PumpUntil(
                () => dialog.IsLoaded && PresentationSource.FromVisual(dialog) is not null,
                TimeSpan.FromSeconds(10),
                "okno wyboru celu się nie pokazało");

            // ODCZYT W TOKU: okno JUZ JEST, lista jeszcze pusta, ale uzytkownik
            // ma slyszec, ze trwa wczytywanie - zamiast pustki i odeslania do
            // kolejnego Control F5.
            harness.PumpUntil(() => heldGroups.Arrived, TimeSpan.FromSeconds(15),
                "wstrzymany GET grup nie dotarł do transportu - pomiar nie dotyczyłby "
                    + "opóźnionego odczytu");
            rowsWhileLoading = dialog.RowCountForTests;
            confirmWhileLoading = dialog.ConfirmEnabledForTests;
            speakingWhileLoading = dialog.IntroductionForTests + " " + dialog.StatusForTests;

            // POWTORZONY SKROT W TRAKCIE ODCZYTU: ani drugie okno, ani drugi
            // odczyt. Produkcyjna droga, nie skrot do metody zaplecza.
            readsWhileLoading = CountSonosReads(harness, readsBefore);
            harness.Window.ShowSonosTargetSelectionForTests();
            harness.Window.ShowSonosTargetSelectionForTests();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
            windowsWhileLoading = harness.Window.SonosTargetWindowsCreatedForTests;
            if (CountSonosReads(harness, readsBefore) != readsWhileLoading)
            {
                throw new Exception("Powtórzony Ctrl+F5 w trakcie odczytu ZWIELOKROTNIŁ odczyty Sonos.");
            }

            // ODPOWIEDZ CHMURY: dopiero teraz. TO SAMO okno ma sie uzupelnic.
            heldGroups.Release();
            harness.PumpUntil(() => dialog.RowCountForTests > 0, TimeSpan.FromSeconds(20),
                "ZGŁOSZONY BŁĄD 1c ODTWORZONY: po zakończonym odczycie grup TO SAMO, wciąż otwarte "
                    + "okno wyboru celu NIE dostało żadnej grupy - jedno Ctrl+F5 nie daje używalnego "
                    + "wyboru, użytkownik musi powtórzyć skrót. Wiersze okna=" + dialog.RowCountForTests
                    + "; wiersze sesji=" + harness.Window.SonosGroupRows.Count);
            rowsAfterRead = dialog.RowCountForTests;
            confirmAfterRead = dialog.ConfirmEnabledForTests;
            highlightedAfterRead = dialog.HighlightedGroupIdForTests ?? "brak";

            // F5 W OKNIE CELU: TEN SAM backend, zachowane zaznaczenie po
            // IDENTYFIKATORZE. Fokus stawiamy na liscie, inaczej gest nie
            // dotarlby do okna wyboru.
            var list = dialog.ListForTests;
            list.Focus();
            Keyboard.Focus(list);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            var readsBeforeWindowF5 = harness.Handler.Requests.Count;
            SendKeyWithModifiers(dialog, Key.F5, ModifierKeys.None);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(900));
            if (harness.Window.LastSonosTargetRefreshTaskForTests is { } refresh) harness.Pump(refresh);
            refreshRequests = dialog.RefreshRequestsForTests;
            readsAfterWindowF5 = CountSonosReads(harness, readsBeforeWindowF5);
            rowsAfterWindowF5 = dialog.RowCountForTests;
            highlightedAfterWindowF5 = dialog.HighlightedGroupIdForTests ?? "brak";

            postsInWindow = harness.Handler.Posts.Count;
            dialog.Close();
        };
        try
        {
            harness.PressKeyOnMainWindow(Key.F5, ModifierKeys.Control);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(400));
            if (harness.Window.LastSonosLibraryTaskForTests is { } pending) harness.Pump(pending);
        }
        finally
        {
            harness.Window.PresentSonosTargetOverrideForTests = null;
            heldGroups.Release();
        }

        if (harness.Window.SonosTargetWindowsCreatedForTests == 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD 1c ODTWORZONY: Ctrl+F5 nie dało okna wyboru celu - "
                + "sama odmowa. Zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(4)));
        }

        // OKNO W TRAKCIE ODCZYTU: pusta lista JEST dopuszczalna, ale MUSI mowic,
        // ze trwa wczytywanie, i NIE MOZE udawac gotowego wyboru.
        if (string.IsNullOrWhiteSpace(speakingWhileLoading))
        {
            throw new Exception("W trakcie odczytu okno celu nie powiedziało NIC o wczytywaniu.");
        }

        if (rowsWhileLoading > 0 && !confirmWhileLoading)
        {
            throw new Exception("Okno celu miało wiersze, ale potwierdzenie było wyłączone.");
        }

        if (rowsWhileLoading == 0 && confirmWhileLoading)
        {
            throw new Exception("Okno celu włączyło potwierdzenie przy pustej liście.");
        }

        if (windowsWhileLoading != 1)
        {
            throw new Exception("Powtórzony Ctrl+F5 w trakcie odczytu ZWIELOKROTNIŁ okna celu: "
                + windowsWhileLoading + ".");
        }

        // UZYWALNY WYBOR PO ODCZYCIE: rzeczywiste grupy, wlaczone potwierdzenie i
        // JEDNOZNACZNIE zaznaczona grupa, nie sama niepusta lista.
        if (rowsAfterRead <= 0 || !confirmAfterRead)
        {
            throw new Exception("Po odczycie okno celu nie dało używalnego wyboru: wiersze="
                + rowsAfterRead + ", potwierdzenie=" + confirmAfterRead + ".");
        }

        if (!string.Equals(highlightedAfterRead, GroupId, StringComparison.Ordinal))
        {
            throw new Exception("Po odczycie okno celu nie wskazało jednoznacznie grupy z topologii "
                + "(jest: " + highlightedAfterRead + ", oczekiwano: " + GroupId + ").");
        }

        // F5 W OKNIE: jeden tor odczytu i zachowane zaznaczenie po IDENTYFIKATORZE.
        if (refreshRequests != 1)
        {
            throw new Exception("F5 w oknie wyboru celu nie poprosiło o odświeżenie (prośby: "
                + refreshRequests + ").");
        }

        if (readsAfterWindowF5 == 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD 1c ODTWORZONY: F5 w oknie wyboru celu nie wykonało "
                + "ŻADNEGO odczytu Sonos - trzeba było zamykać okno, żeby odświeżyć grupy.");
        }

        if (rowsAfterWindowF5 <= 0
            || !string.Equals(highlightedAfterWindowF5, GroupId, StringComparison.Ordinal))
        {
            throw new Exception("F5 w oknie celu zgubiło listę albo zaznaczenie: wiersze="
                + rowsAfterWindowF5 + ", zaznaczenie=" + highlightedAfterWindowF5 + ".");
        }

        // JEDEN TOR ODCZYTU i ZERO POST: wybor celu niczego nie zleca.
        if (postsInWindow != 0 || harness.Handler.Posts.Count != 0)
        {
            throw new Exception($"Ctrl+F5 wysłało {harness.Handler.Posts.Count} POST - wybór celu "
                + "nie ma prawa niczego zlecać.");
        }

        if (CountSonosReads(harness, readsBefore) == 0)
        {
            throw new Exception("ZGŁOSZONY BŁĄD 1c ODTWORZONY: Ctrl+F5 przy pustej topologii NIE "
                + "podjęło próby wczytania grup. Zapowiedzi: "
                + string.Join(" | ", harness.Announcements.TakeLast(4)));
        }

        // ZADNEGO SPOZNIONEGO OKNA PO WYJSCIU: po zamknieciu wyboru nic sie nie
        // otwiera samo, a licznik okien zostaje na jednym.
        harness.PumpQuietly(TimeSpan.FromMilliseconds(500));
        if (harness.Window.SonosTargetWindowsCreatedForTests != 1)
        {
            throw new Exception("Po wyjściu z wyboru celu otworzyło się SPÓŹNIONE okno (okien: "
                + harness.Window.SonosTargetWindowsCreatedForTests + ").");
        }

        if (harness.Window.OpenSonosTargetWindowForTests is not null)
        {
            throw new Exception("Okno wyboru celu zostało zapamiętane jako otwarte po zamknięciu.");
        }
    }

    /// <summary>
    /// CZESC 1d. DWIE GRANICE odswiezania okna celu, kazda zmierzona osobno.
    ///
    /// GRANICA A - ODCZYT JUZ TRWA. Menu Plik zaczyna odczyt, GET /groups WISI
    /// (istniejaca bramka RecordingHandler.HoldNextGet). DOPIERO WTEDY otwieramy
    /// wybor celu. Stara bramka oddawala w tym miejscu NATYCHMIASTOWY, pusty
    /// "sukces" - okno siadalo na starej (null) topologii i NIGDY jej nie
    /// uzupelnialo, az do kolejnego F5. Mierzymy WIERSZE W TYM SAMYM oknie po
    /// zwolnieniu GET, BEZ zadnego nastepnego gestu, oraz BRAK zwielokrotnienia
    /// odczytow.
    ///
    /// GRANICA B - FOKUS RZECZYWISTEGO WIERSZA. Fokus stawiamy na DRUGIM
    /// wierszu (nie na samej liscie!) i dopiero F5 w oknie. PublishSnapshot
    /// czysci _rows, wiec kontener trzymajacy fokus znika i fokus ucieka na
    /// okno. Mierzymy IDENTYFIKATOR grupy z wiersza, ktory PO odswiezeniu
    /// RZECZYWISCIE ma fokus klawiatury - sam IsKeyboardFocusWithin tego nie
    /// rozroznia. Osobno sprawdzamy BRAK KRADZIEZY: z fokusem na przycisku
    /// zamkniecia odswiezenie NIE przeciaga uzytkownika na liste.
    ///
    /// GRANICA C - CANCEL PRZED zwolnieniem GET: wyjscie z sesji uniewaznia
    /// oczekujacy wynik, wiec spozniona odpowiedz nie wpuszcza wierszy do
    /// zamknietego/porzuconego okna.
    /// </summary>
    private static void MeasureTargetWindowJoinsRunningReadAndKeepsRowFocus()
    {
        MeasureTargetWindowJoinsRunningRead();
        MeasureTargetWindowKeepsRealRowFocus();
        MeasureTargetWindowCancelBeforeReleaseDropsResult();
    }

    /// <summary>GRANICA A: okno otwarte W TRAKCIE trwajacego odczytu menu-F5.</summary>
    private static void MeasureTargetWindowJoinsRunningRead()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        harness.ClearSonosTargetForTests();
        harness.AssertNoSonosTargetForMeasurement();

        // MENU PLIK zaczyna odczyt PIERWSZE: jego GET /groups wisi.
        var heldGroups = harness.Handler.HoldNextGet("/groups");
        var readsBefore = harness.Handler.Requests.Count;
        harness.ExecuteCommand(CommandIds.RefreshSonosGroups);
        harness.PumpUntil(() => heldGroups.Arrived, TimeSpan.FromSeconds(15),
            "odczyt menu Plik nie dotarł do transportu - pomiar nie dotyczyłby TRWAJĄCEGO odczytu");

        var rowsWhileLoading = -1;
        var rowsAfterRelease = -1;
        var highlightedAfterRelease = string.Empty;
        var readsWhileLoading = -1;
        var readsAfterRelease = -1;
        var refreshRequestsInWindow = -1;

        harness.Window.PresentSonosTargetOverrideForTests = dialog =>
        {
            dialog.ShowInTaskbar = false;
            dialog.Show();
            harness.PumpUntil(
                () => dialog.IsLoaded && PresentationSource.FromVisual(dialog) is not null,
                TimeSpan.FromSeconds(10),
                "okno wyboru celu się nie pokazało");

            rowsWhileLoading = dialog.RowCountForTests;
            readsWhileLoading = CountSonosReads(harness, readsBefore);

            // ODPOWIEDZ: dopiero teraz. ZADNEGO kolejnego gestu - jesli okno ma
            // sie uzupelnic, musi to zrobic SAMO, przez wspoldzielony przelot.
            heldGroups.Release();
            harness.PumpUntil(() => dialog.RowCountForTests > 0, TimeSpan.FromSeconds(20),
                "ZGŁOSZONA GRANICA A ODTWORZONA: wybór celu otwarty W TRAKCIE trwającego odczytu "
                    + "menu-F5 dostał natychmiastowy pusty 'sukces' i po zakończeniu odczytu "
                    + "NIE uzupełnił listy - użytkownik musi nacisnąć F5 jeszcze raz. "
                    + "Wiersze okna=" + dialog.RowCountForTests
                    + "; wiersze sesji=" + harness.Window.SonosGroupRows.Count
                    + "; zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(5)));

            rowsAfterRelease = dialog.RowCountForTests;
            highlightedAfterRelease = dialog.HighlightedGroupIdForTests ?? "brak";
            readsAfterRelease = CountSonosReads(harness, readsBefore);
            refreshRequestsInWindow = dialog.RefreshRequestsForTests;
            dialog.Close();
        };
        try
        {
            harness.Window.ShowSonosTargetSelectionForTests();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(400));
            if (harness.Window.LastSonosLibraryTaskForTests is { } pending) harness.Pump(pending);
        }
        finally
        {
            harness.Window.PresentSonosTargetOverrideForTests = null;
            heldGroups.Release();
        }

        if (harness.Window.SonosTargetWindowsCreatedForTests == 0)
        {
            throw new Exception("Wybór celu w trakcie trwającego odczytu nie dał okna - sama odmowa. "
                + "Zapowiedzi: " + string.Join(" | ", harness.Announcements.TakeLast(4)));
        }

        if (rowsWhileLoading != 0)
        {
            throw new Exception("Pomiar nieważny: lista była już pełna PRZED zwolnieniem GET "
                + "(wiersze=" + rowsWhileLoading + "), więc nie mierzyliśmy trwającego odczytu.");
        }

        if (rowsAfterRelease <= 0
            || !string.Equals(highlightedAfterRelease, GroupId, StringComparison.Ordinal))
        {
            throw new Exception("Po dołączeniu do trwającego odczytu okno nie wskazało grupy: "
                + "wiersze=" + rowsAfterRelease + ", zaznaczenie=" + highlightedAfterRelease
                + ", oczekiwano " + GroupId + ".");
        }

        // JEDEN TOR GET: dolaczenie do trwajacego przelotu NIE mnozy odczytow i
        // NIE wymaga kolejnej prosby o odswiezenie z okna.
        if (readsAfterRelease != readsWhileLoading)
        {
            throw new Exception("Dołączenie do trwającego odczytu ZWIELOKROTNIŁO odczyty Sonos: "
                + readsWhileLoading + " -> " + readsAfterRelease + ".");
        }

        if (refreshRequestsInWindow != 0)
        {
            throw new Exception("Okno musiało samo poprosić o odświeżenie (" + refreshRequestsInWindow
                + ") - to znaczy, że nie dołączyło do trwającego odczytu.");
        }

        if (harness.Handler.Posts.Count != 0)
        {
            throw new Exception($"Wybór celu wysłał {harness.Handler.Posts.Count} POST.");
        }
    }

    /// <summary>GRANICA B: fokus na RZECZYWISTYM wierszu przetrwa odswiezenie.</summary>
    private static void MeasureTargetWindowKeepsRealRowFocus()
    {
        using var harness = RealHarness.Create();
        harness.Enter();

        // DWIE grupy, zeby "drugi wiersz" byl czyms innym niz pierwszy i niz
        // domyslne zaznaczenie - inaczej zachowanie fokusu byloby nierozstrzygalne.
        harness.Handler.RouteOverride = (request, _) =>
            request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal)
                ? Json(SpeakerGroupsThree)
                : null;

        var secondRowId = string.Empty;
        var focusedRowBefore = string.Empty;
        var focusedRowAfter = string.Empty;
        var focusedNameAfterButton = string.Empty;
        var rowsAfter = -1;

        harness.Window.PresentSonosTargetOverrideForTests = dialog =>
        {
            dialog.ShowInTaskbar = false;
            dialog.Show();
            harness.PumpUntil(
                () => dialog.IsLoaded && PresentationSource.FromVisual(dialog) is not null,
                TimeSpan.FromSeconds(10),
                "okno wyboru celu się nie pokazało");
            // PRZYGOTOWANIE, nie pomiar: wejscie w sesje odczytalo JEDNA grupe,
            // a RouteOverride dziala od NASTEPNEGO GET. Pierwsze F5 w oknie
            // sciaga trojke grup, zeby "drugi wiersz" w ogole istnial. Granice
            // fokusu mierzy DOPIERO drugie F5, nizej.
            SendKeyWithModifiers(dialog, Key.F5, ModifierKeys.None);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(600));
            if (harness.Window.LastSonosTargetRefreshTaskForTests is { } priming)
            {
                harness.Pump(priming);
            }
            harness.PumpUntil(() => dialog.RowCountForTests >= 2, TimeSpan.FromSeconds(20),
                "pomiar fokusu wiersza wymaga co najmniej dwóch grup w oknie");

            // FOKUS NA DRUGIM WIERSZU, nie na samej liscie. Istniejaca droga
            // okna: SelectRowForTests stawia fokus na KONTENERZE wiersza.
            dialog.SelectRowForTests(1);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
            secondRowId = dialog.HighlightedGroupIdForTests ?? "brak";
            focusedRowBefore = dialog.FocusedRowIdForTests ?? "brak";
            if (!string.Equals(focusedRowBefore, secondRowId, StringComparison.Ordinal))
            {
                throw new Exception("Pomiar nieważny: fokus nie stanął na DRUGIM wierszu przed "
                    + "odświeżeniem (fokus=" + focusedRowBefore + ", wiersz=" + secondRowId
                    + ", element=" + dialog.FocusedElementNameForTests + ").");
            }

            // F5 W OKNIE: prawdziwy gest, prawdziwe odswiezenie, PublishSnapshot
            // czysci wiersze razem z kontenerem trzymajacym fokus.
            SendKeyWithModifiers(dialog, Key.F5, ModifierKeys.None);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(900));
            if (harness.Window.LastSonosTargetRefreshTaskForTests is { } refresh) harness.Pump(refresh);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
            rowsAfter = dialog.RowCountForTests;
            focusedRowAfter = dialog.FocusedRowIdForTests ?? "brak";

            // BRAK KRADZIEZY: fokus na przycisku zamkniecia, odswiezenie NIE
            // przeciaga uzytkownika na liste.
            dialog.CloseButtonForTests.Focus();
            Keyboard.Focus(dialog.CloseButtonForTests);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
            SendKeyWithModifiers(dialog, Key.F5, ModifierKeys.None);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(900));
            if (harness.Window.LastSonosTargetRefreshTaskForTests is { } second) harness.Pump(second);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
            focusedNameAfterButton = dialog.FocusedElementNameForTests;
            dialog.Close();
        };
        try
        {
            harness.Window.ShowSonosTargetSelectionForTests();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(400));
            if (harness.Window.LastSonosLibraryTaskForTests is { } pending) harness.Pump(pending);
        }
        finally
        {
            harness.Window.PresentSonosTargetOverrideForTests = null;
            harness.Handler.RouteOverride = null;
        }

        if (rowsAfter < 2)
        {
            throw new Exception("Po odświeżeniu okno zgubiło listę (wiersze=" + rowsAfter + ").");
        }

        if (!string.Equals(focusedRowAfter, secondRowId, StringComparison.Ordinal))
        {
            throw new Exception("ZGŁOSZONA GRANICA B ODTWORZONA: po odświeżeniu fokus NIE wrócił na "
                + "TEN SAM rzeczywisty wiersz. Przed=" + focusedRowBefore + ", po=" + focusedRowAfter
                + " (oczekiwano " + secondRowId + "). Czyszczenie wierszy w PublishSnapshot "
                + "zgubiło fokus klawiatury.");
        }

        if (string.Equals(focusedNameAfterButton, "ListBoxItem", StringComparison.Ordinal))
        {
            throw new Exception("Odświeżenie UKRADŁO fokus z przycisku zamknięcia na wiersz listy - "
                + "użytkownik stojący na przycisku traci miejsce pracy.");
        }
    }

    /// <summary>GRANICA C: Cancel PRZED zwolnieniem GET nie wpuszcza wyniku.</summary>
    private static void MeasureTargetWindowCancelBeforeReleaseDropsResult()
    {
        using var harness = RealHarness.Create();
        harness.Enter();
        harness.ClearSonosTargetForTests();
        harness.AssertNoSonosTargetForMeasurement();

        var heldGroups = harness.Handler.HoldNextGet("/groups");
        var refreshesApplied = -1;
        var rowsAfterCancel = -1;

        harness.Window.PresentSonosTargetOverrideForTests = dialog =>
        {
            dialog.ShowInTaskbar = false;
            dialog.Show();
            harness.PumpUntil(
                () => dialog.IsLoaded && PresentationSource.FromVisual(dialog) is not null,
                TimeSpan.FromSeconds(10),
                "okno wyboru celu się nie pokazało");
            harness.PumpUntil(() => heldGroups.Arrived, TimeSpan.FromSeconds(15),
                "wstrzymany GET grup nie dotarł - pomiar nie dotyczyłby odczytu w locie");

            // CANCEL PRZED ZWOLNIENIEM: dokladnie ta kolejnosc jest granica.
            // Zamkniecie PO zwolnieniu nie dowodzi niczego o spóźnionym wyniku.
            harness.Window.CancelSonosPendingWork();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

            heldGroups.Release();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(900));
            if (harness.Window.LastSonosTargetRefreshTaskForTests is { } refresh) harness.Pump(refresh);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
            refreshesApplied = dialog.RefreshesAppliedForTests;
            rowsAfterCancel = dialog.RowCountForTests;
            dialog.Close();
        };
        try
        {
            harness.Window.ShowSonosTargetSelectionForTests();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(400));
            if (harness.Window.LastSonosLibraryTaskForTests is { } pending) harness.Pump(pending);
        }
        finally
        {
            harness.Window.PresentSonosTargetOverrideForTests = null;
            heldGroups.Release();
        }

        if (refreshesApplied != 0 || rowsAfterCancel != 0)
        {
            throw new Exception("Po CancelSonosPendingWork wykonanym PRZED zwolnieniem GET spóźniony "
                + "odczyt WPUŚCIŁ wynik do okna celu (publikacje=" + refreshesApplied
                + ", wiersze=" + rowsAfterCancel + ").");
        }

        if (harness.Handler.Posts.Count != 0)
        {
            throw new Exception($"Anulowana droga wysłała {harness.Handler.Posts.Count} POST.");
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
