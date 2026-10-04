using System.Collections.ObjectModel;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// MOJE STACJE - KOLEJNOSC MIERZONA NA PRAWDZIWYM MODALU, nie na helperze.
///
/// Dotychczasowe pomiary kolejnosci siedzialy w Core i wolaly
/// <c>SonosOwnStreamsOrder</c> bezposrednio. To sprawdzalo regule, ale NIE
/// sprawdzalo, czy produkcyjne okno w ogole ja wola, czy zapisuje wynik
/// produkcyjna kolejka i czy porzadek przezywa zamkniecie okna.
///
/// Tutaj otwieramy PRODUKCYJNE <c>ShowSonosOwnStreams</c>, naciskamy PRAWDZIWE
/// gesty (Alt+1/2/3, Alt+strzalki, Ctrl+X/Ctrl+V), zamykamy okno, otwieramy je
/// PONOWNIE i porownujemy kolejnosc oraz identyfikatory. Zapis idzie TA SAMA
/// kolejka stanu co w zyciu - pomiar NIE dokłada wlasnego Save.
///
/// CZEGO NIE DOWODZI: niczego o prawdziwym glosniku - zadna stacja nie jest tu
/// uruchamiana, a konto i transport sa syntetyczne.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunOwnStreamsOrderInRealWindow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { MeasureOwnStreamsOrder(); }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(300)))
        {
            throw new Exception("Limit pomiaru kolejności Moich stacji");
        }

        if (failure is not null) throw failure;
        Console.WriteLine("OK: Moje stacje - tryby, przenoszenie, Ctrl+X/V i trwałość po ponownym otwarciu");
    }

    private static void MeasureOwnStreamsOrder()
    {
        MeasureSortModesAndPersistence();
        MeasureMoveAndCutPastePersistence();
        MeasureAddKeepsActiveSortMode();
    }

    /// <summary>Stacje dla pomiaru: nazwy i czas dodania ROZNE, zeby tryby sie rozchodzily.</summary>
    private static List<SonosOwnStreamSettings> SeedStations() =>
    [
        new() { Id = "ID-C", Name = "Cecylia", StreamUrl = "http://przyklad.test/c" },
        new() { Id = "ID-A", Name = "Alicja", StreamUrl = "http://przyklad.test/a" },
        new() { Id = "ID-B", Name = "Barbara", StreamUrl = "http://przyklad.test/b" },
    ];

    /// <summary>
    /// OTWARCIE PRODUKCYJNEGO MODALU i wykonanie na nim pracy. Uzywamy
    /// <c>PresentSonosOwnStreamsOverrideForTests</c>, bo ShowDialog zablokowalby
    /// watek pomiaru - ale CALA RESZTA (budowa okna, wiazanie zapisu, tryb z
    /// magazynu kolekcji) to produkcyjna sciezka ShowSonosOwnStreams.
    /// </summary>
    private static void WithOwnStreamsWindow(RealHarness harness,
        Action<SonosOwnStreamsWindow> work)
    {
        Exception? inside = null;
        var presented = false;
        // PIERWSZY PLAN: pomiar nie walczy o aktywacje z czytnikiem ekranu ani z
        // innym oknem pulpitu, wiec uzywamy ISTNIEJACEGO wstrzyknięcia bramy.
        // Sama brama, sesja Sonosa i budowa okna jada produkcyjna droga.
        harness.Window.ForegroundProcessIdOverrideForTests = Environment.ProcessId;
        harness.Window.PresentSonosOwnStreamsOverrideForTests = window =>
        {
            presented = true;
            try
            {
                // POKAZUJEMY okno bez modalnej petli: gesty i fokus dzialaja,
                // a pomiar zachowuje kontrole nad watkiem.
                window.Show();
                harness.PumpUntil(() => window.IsVisible, TimeSpan.FromSeconds(10),
                    "okno Moich stacji się nie pokazało");
                work(window);
            }
            catch (Exception e) { inside = e; }
            finally
            {
                if (window.IsVisible) window.Close();
                harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
            }
        };

        try { harness.Window.ShowSonosOwnStreamsForTests(); }
        finally { harness.Window.PresentSonosOwnStreamsOverrideForTests = null; }
        if (inside is not null) throw inside;
        if (!presented)
        {
            // PRODUKCJA ODMOWILA OTWARCIA (brama okien potomnych). Bez tego
            // sprawdzenia pomiar przechodzilby przez pusty modal i klamal
            // o zmierzonych gestach.
            throw new Exception("Produkcja nie otworzyła Moich stacji - brama okien "
                + "potomnych odmówiła. Ostatnia wypowiedź: "
                + (harness.Announcements.LastOrDefault() ?? "(cisza)"));
        }
    }

    private static ListBox StationsListOf(SonosOwnStreamsWindow window) =>
        (ListBox)window.FindName("StationsList")!;

    /// <summary>
    /// FIZYCZNY gest klawiatury na liscie stacji - tak jak u uzytkownika.
    ///
    /// MODYFIKATORY musza byc PRAWDZIWE: produkcyjny handler czyta
    /// <c>Keyboard.Modifiers</c>, a nie pole w zdarzeniu, wiec ustawiamy stan
    /// klawiatury WATKU pomiaru (tak samo jak pomiar skrotow klipow audio) i
    /// przywracamy go w finally. Zdarzenie leci PRODUKCYJNA droga
    /// <c>Window_PreviewKeyDown</c>. P/Invoke stanu klawiatury jest juz
    /// zadeklarowany w tej klasie czesciowej (pomiar przelaczania podlist).
    /// </summary>
    private static void PressOnList(RealHarness harness, SonosOwnStreamsWindow window,
        Key key, ModifierKeys modifiers)
    {
        var list = StationsListOf(window);
        list.Focus();
        Keyboard.Focus(list);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(40));

        var previous = new byte[256];
        if (!GetKeyboardState(previous)) throw new Exception("Brak stanu klawiatury wątku pomiaru.");
        var keys = new byte[256];
        if (modifiers.HasFlag(ModifierKeys.Control)) keys[0x11] = keys[0xA2] = 0x80;
        if (modifiers.HasFlag(ModifierKeys.Shift)) keys[0x10] = keys[0xA0] = 0x80;
        if (modifiers.HasFlag(ModifierKeys.Alt)) keys[0x12] = keys[0xA4] = 0x80;
        try
        {
            if (!SetKeyboardState(keys)) throw new Exception("Nie udało się ustawić modyfikatorów.");
            var source = PresentationSource.FromVisual(window)
                ?? throw new Exception("Okno Moich stacji nie ma powierzchni prezentacji.");
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
                Source = list,
            };
            typeof(SonosOwnStreamsWindow)
                .GetMethod("Window_PreviewKeyDown",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [window, args]);
        }
        finally { SetKeyboardState(previous); }

        harness.PumpQuietly(TimeSpan.FromMilliseconds(60));
    }

    /// <summary>
    /// ALT+1/2/3 na prawdziwym oknie oraz TRWALOSC trybu i kolejnosci po
    /// zamknieciu i ponownym otwarciu listy.
    /// </summary>
    private static void MeasureSortModesAndPersistence()
    {
        Console.Error.WriteLine("MOJE STACJE: tryby Alt+1/2/3 i trwałość");
        using var harness = RealHarness.Create();
        harness.Window.StateForTests.Sonos.OwnStreams = SeedStations();
        harness.EnterSonosSessionForMeasurement();

        IReadOnlyList<string> alphabetical = [];
        var lastStatus = "";
        WithOwnStreamsWindow(harness, window =>
        {
            // ALT+2 - ALFABETYCZNIE.
            PressOnList(harness, window, Key.D2, ModifierKeys.Alt);
            alphabetical = window.OrderedLabelsForTests;
            lastStatus = window.StatusForTests;
            if (alphabetical.Count != 3)
            {
                throw new Exception("Lista stacji nie ma trzech wierszy: " + alphabetical.Count);
            }

            var sortedByName = alphabetical.OrderBy(x => x, StringComparer.CurrentCulture).ToArray();
            if (!alphabetical.SequenceEqual(sortedByName, StringComparer.Ordinal))
            {
                throw new Exception("Alt+2 nie ułożyło stacji alfabetycznie: "
                    + string.Join(" | ", alphabetical));
            }
        });

        // TRYB MUSI PRZEZYC ZAMKNIECIE OKNA i trafic do magazynu kolekcji sesji.
        var mode = harness.SonosSortModeForTests(CollectionSortMode.Custom);
        if (mode != CollectionSortMode.Alphabetical)
        {
            throw new Exception($"Tryb alfabetyczny nie zapisał się w stanie sesji: {mode}. "
                + $"Kolejność po Alt+2: {string.Join(" | ", alphabetical)}. "
                + $"Komunikat okna: {lastStatus}");
        }

        WithOwnStreamsWindow(harness, window =>
        {
            var again = window.OrderedLabelsForTests;
            if (!again.SequenceEqual(alphabetical, StringComparer.Ordinal))
            {
                throw new Exception("Po ponownym otwarciu kolejność alfabetyczna się rozjechała: "
                    + string.Join(" | ", again));
            }

            // ALT+3 - WLASNA kolejnosc.
            PressOnList(harness, window, Key.D3, ModifierKeys.Alt);
        });

        var custom = harness.SonosSortModeForTests(CollectionSortMode.Alphabetical);
        if (custom != CollectionSortMode.Custom)
        {
            throw new Exception("Alt+3 nie zapisało kolejności własnej: " + custom);
        }
    }

    /// <summary>
    /// ALT+STRZALKI i CTRL+X -> CTRL+V na prawdziwym oknie: kolejnosc, ID,
    /// fokus i TRWALOSC po ponownym otwarciu. Zapis idzie produkcyjna kolejka.
    /// </summary>
    private static void MeasureMoveAndCutPastePersistence()
    {
        Console.Error.WriteLine("MOJE STACJE: Alt+strzałki, Ctrl+X/V i trwałość");
        using var harness = RealHarness.Create();
        harness.Window.StateForTests.Sonos.OwnStreams = SeedStations();
        // WLASNA kolejnosc - tylko w niej przenoszenie ma sens.
        harness.SetSonosSortModeForTests(CollectionSortMode.Custom);
        harness.EnterSonosSessionForMeasurement();

        IReadOnlyList<string> afterMove = [];
        WithOwnStreamsWindow(harness, window =>
        {
            var before = window.OrderedLabelsForTests.ToArray();
            var list = StationsListOf(window);
            list.SelectedIndex = 2;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            var movedId = window.HighlightedStationIdForTests
                ?? throw new Exception("Brak zaznaczonej stacji przed przeniesieniem.");

            // ALT+GORA: wiersz idzie o jedno miejsce wyzej.
            PressOnList(harness, window, Key.Up, ModifierKeys.Alt);
            afterMove = window.OrderedLabelsForTests;
            if (afterMove.SequenceEqual(before, StringComparer.Ordinal))
            {
                throw new Exception("Alt+góra nie zmieniło kolejności: "
                    + string.Join(" | ", afterMove));
            }

            // FOKUS ZOSTAJE NA PRZENOSZONEJ STACJI, nie skacze na inny materiał.
            if (!string.Equals(window.HighlightedStationIdForTests, movedId, StringComparison.Ordinal))
            {
                throw new Exception("Po Alt+góra zaznaczenie uciekło z przenoszonej stacji.");
            }

            // CTRL+X na pierwszym wierszu, CTRL+V PRZED ostatnim.
            list.SelectedIndex = 0;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            var cutId = window.HighlightedStationIdForTests
                ?? throw new Exception("Brak zaznaczenia przed Ctrl+X.");
            PressOnList(harness, window, Key.X, ModifierKeys.Control);
            if (!window.HasPendingMoveForTests)
            {
                throw new Exception("Ctrl+X nie zapamiętało stacji do przeniesienia.");
            }

            list.SelectedIndex = 2;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            PressOnList(harness, window, Key.V, ModifierKeys.Control);

            var pasted = window.OrderedLabelsForTests;
            var pastedIndex = window.RowIdsForTests.ToList().IndexOf(cutId);
            if (pastedIndex != 1)
            {
                throw new Exception($"Ctrl+V miało wstawić stację PRZED wierszem docelowym "
                    + $"(pozycja 2 z 3), a wyszła pozycja {pastedIndex + 1}. "
                    + "Kolejność: " + string.Join(" | ", pasted));
            }

            if (!string.Equals(window.HighlightedStationIdForTests, cutId, StringComparison.Ordinal))
            {
                throw new Exception("Po Ctrl+V zaznaczenie nie jest na przeniesionej stacji.");
            }

            afterMove = pasted;
        });

        // TRWALOSC: ZADNEGO dodatkowego Save w pomiarze - jesli produkcja nie
        // zakolejkowala zapisu, to tutaj WYJDZIE.
        harness.WaitForQueuedStateSave();

        WithOwnStreamsWindow(harness, window =>
        {
            var reopened = window.OrderedLabelsForTests;
            if (!reopened.SequenceEqual(afterMove, StringComparer.Ordinal))
            {
                throw new Exception("Kolejność nie przeżyła ponownego otwarcia. Było: "
                    + string.Join(" | ", afterMove) + " / jest: " + string.Join(" | ", reopened));
            }

            // ID I ADRESY NIETKNIETE: przenoszenie zmienia PORZADEK, nie dane.
            var ids = window.RowIdsForTests.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (!ids.SequenceEqual(new[] { "ID-A", "ID-B", "ID-C" }, StringComparer.Ordinal))
            {
                throw new Exception("Przenoszenie zgubiło albo zmieniło identyfikatory: "
                    + string.Join(", ", ids));
            }
        });
    }

    /// <summary>
    /// DODANIE STACJI PRZY AKTYWNYM TRYBIE SORTOWANIA. EditStation dopisuje
    /// wiersz na koniec BEZ przebudowy, wiec przy trybie alfabetycznym nowa
    /// stacja moze wyladowac w zlym miejscu i lista przestaje byc tym, co
    /// obiecuje tryb.
    /// </summary>
    private static void MeasureAddKeepsActiveSortMode()
    {
        Console.Error.WriteLine("MOJE STACJE: dodanie stacji przy trybie alfabetycznym");
        using var harness = RealHarness.Create();
        harness.Window.StateForTests.Sonos.OwnStreams = SeedStations();
        harness.SetSonosSortModeForTests(CollectionSortMode.Alphabetical);
        harness.EnterSonosSessionForMeasurement();

        WithOwnStreamsWindow(harness, window =>
        {
            // DODANIE idzie produkcyjna droga wiersza, z pominieciem okna
            // edytora (RadioStationWindow jest modalne i nie ma go kto obsluzyc).
            window.AppendStationForTests(new SonosOwnStreamSettings
            {
                Id = "ID-AB",
                Name = "Aneta",
                StreamUrl = "http://przyklad.test/ab",
            });
            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));

            var labels = window.OrderedLabelsForTests;
            var sorted = labels.OrderBy(x => x, StringComparer.CurrentCulture).ToArray();
            if (!labels.SequenceEqual(sorted, StringComparer.Ordinal))
            {
                throw new Exception("Po dodaniu stacji lista przestała być alfabetyczna: "
                    + string.Join(" | ", labels));
            }

            // NOWA STACJA MUSI BYC WIDOCZNA I ZAZNACZONA - inaczej uzytkownik nie wie,
            // gdzie wyladowala.
            if (!string.Equals(window.HighlightedStationIdForTests, "ID-AB", StringComparison.Ordinal))
            {
                throw new Exception("Po dodaniu zaznaczenie nie stoi na nowej stacji.");
            }
        });
    }
}
