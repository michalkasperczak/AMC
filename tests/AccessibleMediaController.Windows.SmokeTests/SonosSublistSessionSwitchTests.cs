using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// POMIAR PRZELACZANIA SESJI Z WNETRZA PODLISTY SONOSA I POWROTU DO NIEJ.
///
/// MIERZYMY ZGLOSZONA DROGE, NIE JEJ ATRAPE:
///  - prawdziwe MainWindow (wlasciciel) + prawdziwe okno Biblioteki + prawdziwe,
///    ZAGNIEZDZONE okno "Moje stacje" otwarte produkcyjnym Enterem na kategorii;
///  - Ctrl+8 / Ctrl+5 ida ZYWA KLAWIATURA w tunelujacym PreviewKeyDown TEGO okna,
///    z PRAWDZIWIE wcisnietym Control (SetKeyboardState) - nie przez
///    PresentOverride i nie przez wolanie metody prywatnej z nazwy;
///  - numery sesji czytamy z KONFIGURACJI (FindSlot), nie z glowy.
///
/// CO MA WYJSC: Ctrl+numer_radia przelacza sesje BEZ recznego zamykania listy, a
/// Ctrl+numer_sonosa wraca do TEJ SAMEJ PODLISTY i na TEN SAM WIERSZ. Powrot do
/// samego korzenia kategorii to PORAZKA i test to jawnie rozdziela.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);

    internal static void RunSublistSessionSwitch()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                MeasureSublistSwitch();
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(180)))
        {
            throw new Exception("Limit pomiaru przełączania sesji z podlisty Sonosa");
        }

        if (failure is not null) throw failure;
        Console.WriteLine(
            "OK: Ctrl+cyfra z Moich stacji przełącza sesję bez ręcznego zamykania, "
            + "a powrót trafia w tę samą podlistę i wiersz");
    }

    private static void MeasureSublistSwitch()
    {
        using var h = RealHarness.Create();
        h.Enter();

        // DWIE stacje: powrot na wiersz DALSZY NIZ PIERWSZY jest jedynym, ktory
        // odrozni prawdziwe przywrocenie miejsca od "lista otwarta od zera".
        var streams = h.Window.StateForTests.Sonos.OwnStreams;
        streams.Clear();
        streams.Add(new SonosOwnStreamSettings
        { Id = "station-one", Name = "Pierwsza stacja", StreamUrl = "https://a.example.invalid/1" });
        streams.Add(new SonosOwnStreamSettings
        { Id = "station-two", Name = "Druga stacja", StreamUrl = "https://a.example.invalid/2" });

        var sonosSlot = h.Window.SessionsForTests.FindSlot("sonos")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji sonos.");
        var radioSlot = h.Window.SessionsForTests.FindSlot("radio")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji radio.");
        if (sonosSlot == radioSlot) throw new Exception("Sloty sesji się pokrywają - pomiar nic nie rozdzieli.");

        var postsBeforeSwitch = -1;
        var switched = false;
        Exception? inside = null;

        // FAZA 1: dojedz PRODUKCYJNA droga do Moich stacji, stan na DRUGIM wierszu
        // i wcisnij Ctrl+numer_radia BEZ zamykania listy.
        RunLibraryPhase(h, dialog =>
        {
            var list = (ListBox)dialog.FindName("StationsList")!;
            if (list.Items.Count < 2) throw new Exception("Lista własnych stacji ma mniej niż dwa wiersze.");
            list.SelectedIndex = 1;
            list.UpdateLayout();
            if (dialog.HighlightedStationIdForTests != "station-two")
                throw new Exception("Nie udało się stanąć na drugim wierszu przed przełączeniem.");

            postsBeforeSwitch = h.Handler.Posts.Count;
            SendCtrlDigit(dialog, list, radioSlot);
            switched = true;
        });

        if (inside is not null) throw inside;
        if (!switched) throw new Exception("Nie dojechano produkcyjną drogą do Moich stacji.");

        // Ctrl+cyfra SAM zamyka modal (zlecenie czeka na pusty stos modalny).
        h.PumpUntil(
            () => h.Window.OpenSonosOwnStreamsWindowForTests is null
                || h.Window.OpenSonosOwnStreamsWindowForTests?.IsVisible != true,
            "podlista nie zamknęła się po Ctrl+cyfra - wymagałaby ręcznego zamknięcia");
        h.PumpUntil(
            () => !string.Equals(h.Window.SessionsForTests.Current.Id, "sonos", StringComparison.Ordinal),
            "Ctrl+cyfra z wnętrza podlisty NIE przełączyło sesji");

        if (!string.Equals(h.Window.SessionsForTests.Current.Id, "radio", StringComparison.Ordinal))
        {
            throw new Exception("Przełączono do innej sesji niż żądana: "
                + h.Window.SessionsForTests.Current.Id);
        }

        if (h.Handler.Posts.Count != postsBeforeSwitch)
        {
            throw new Exception($"Przełączenie sesji wysłało {h.Handler.Posts.Count - postsBeforeSwitch} "
                + "dodatkowy POST - zmiana sesji nie ma prawa nic odtwarzać.");
        }

        // ZADANIE POWROTU musi opisywac KONKRETNE miejsce, nie samą kategorię.
        var pending = h.Window.SonosSublistReturnForTests
            ?? throw new Exception("Nie zapamiętano żadnej podlisty do powrotu.");
        if (!string.Equals(pending.CategoryId, SonosLibraryPresentation.OwnStreamsCategoryId, StringComparison.Ordinal))
            throw new Exception("Zapamiętano inną kategorię: " + pending.CategoryId);
        if (!string.Equals(pending.SelectedRowId, "station-two", StringComparison.Ordinal))
            throw new Exception("Zapamiętano inny wiersz: " + pending.SelectedRowId);

        // FAZA 2: Ctrl+numer_sonosa z OKNA GLOWNEGO ma wrocic do TEJ SAMEJ podlisty.
        var reopenedBefore = h.Window.SonosSublistReopenedForTests;
        SonosOwnStreamsWindow? returned = null;
        var watch = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(20) };
        watch.Tick += (_, _) =>
        {
            if (h.Window.OpenSonosOwnStreamsWindowForTests is { IsVisible: true } dialog)
            {
                returned = dialog;
                watch.Stop();
                dialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { dialog.Close(); } catch (InvalidOperationException) { }
                }));
            }
        };
        watch.Start();
        try
        {
            h.ReactivateOwnWindow();
            var mediaList = (ListBox)h.Window.FindName("MediaList")!;
            SendCtrlDigit(h.Window, mediaList, sonosSlot);
            h.PumpUntil(() => returned is not null,
                "powrót Ctrl+cyfra NIE otworzył ponownie podlisty (wylądowano w korzeniu sesji)");
        }
        finally { watch.Stop(); }

        if (h.Window.SonosSublistReopenedForTests != reopenedBefore + 1)
        {
            throw new Exception("Powrót nie przeszedł istniejącą drogą kategorii Biblioteki.");
        }

        if (returned!.HighlightedStationIdForTests != "station-two")
        {
            throw new Exception("Powrót otworzył podlistę, ale na wierszu "
                + returned.HighlightedStationIdForTests + " zamiast station-two.");
        }

        // ZADANIE JEST JEDNORAZOWE: po powrocie nie wolno mu wisieć i otwierać
        // listy przy każdym kolejnym wejściu w sesję.
        if (h.Window.SonosSublistReturnForTests is not null)
        {
            throw new Exception("Zadanie powrotu zostało wiszące po wykorzystaniu.");
        }
    }

    /// <summary>
    /// PRODUKCYJNA droga do Moich stacji: polecenie Biblioteki, wybor kategorii i
    /// Enter - dokladnie to, co robi uzytkownik. Kroki podlisty jada z timera, bo
    /// kategoria otwiera ZAGNIEZDZONY ShowDialog.
    /// </summary>
    private static void RunLibraryPhase(RealHarness h, Action<SonosOwnStreamsWindow> steps)
    {
        var phase = 0;
        var done = false;
        Exception? inside = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Brak postępu w fazie " + phase);
                if (phase == 0 && h.Window.OpenSonosLibraryWindowForTests is { IsVisible: true } library)
                {
                    phase = 1;
                    var index = library.CategoryNamesForTests
                        .Select((label, i) => (label, i))
                        .First(pair => pair.label.Contains("stacje", StringComparison.OrdinalIgnoreCase)).i;
                    library.SelectRowForTests(index);
                    // Oddaj tick: zagnieżdżony ShowDialog zablokowałby ten timer.
                    library.Dispatcher.BeginInvoke(new Action(library.OpenSelectedForTests));
                }
                else if (phase == 1 && h.Window.OpenSonosOwnStreamsWindowForTests is { IsVisible: true } dialog)
                {
                    phase = 2;
                    timer.Stop();
                    done = true;
                    steps(dialog);
                }
            }
            catch (Exception e)
            {
                inside = e;
                timer.Stop();
                foreach (Window owned in h.Window.OwnedWindows.Cast<Window>().ToArray())
                {
                    try { owned.Close(); } catch (InvalidOperationException) { }
                }
            }
        };
        timer.Start();
        try { h.ExecuteCommand(CommandIds.ViewLibrary); }
        finally { timer.Stop(); }
        if (inside is not null) throw inside;
        if (!done) throw new Exception("Nie otwarto Moich stacji rzeczywistą drogą Biblioteki.");
    }

    /// <summary>
    /// CTRL+CYFRA PRAWDZIWIE: stan klawiatury watku ustawiony tak, ze
    /// <c>Keyboard.Modifiers</c> RZECZYWISCIE widzi Control, i zdarzenie
    /// tunelujace na realnym zrodle prezentacji okna. Produkcja sprawdza
    /// modyfikator sama - gdyby test go udawal inaczej, mierzylby wlasna atrape.
    /// </summary>
    private static void SendCtrlDigit(Window window, ListBox list, int digit)
    {
        var key = digit switch
        {
            1 => Key.D1, 2 => Key.D2, 3 => Key.D3, 4 => Key.D4, 5 => Key.D5,
            6 => Key.D6, 7 => Key.D7, 8 => Key.D8, 9 => Key.D9,
            _ => throw new Exception("Slot sesji poza zakresem cyfr: " + digit)
        };

        if (!list.IsKeyboardFocusWithin)
        {
            list.Focus();
            Keyboard.Focus(list);
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(40));
        }

        var target = Keyboard.FocusedElement as UIElement ?? list;
        var source = PresentationSource.FromVisual(window)
            ?? throw new Exception("Okno nie ma powierzchni prezentacji.");

        var previous = new byte[256];
        if (!GetKeyboardState(previous)) throw new Exception("Nie można odczytać stanu klawiatury.");
        var pressed = new byte[256];
        pressed[0x11] = pressed[0xA2] = 0x80; // VK_CONTROL + VK_LCONTROL
        try
        {
            if (!SetKeyboardState(pressed)) throw new Exception("Nie można ustawić stanu klawiatury wątku.");
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            {
                throw new Exception("Aparatura nie wcisnęła Control - pomiar mierzyłby zwykłą cyfrę.");
            }

            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
        }
        finally { SetKeyboardState(previous); }

        LibraryFixture.Pump(TimeSpan.FromMilliseconds(60));
    }
}
