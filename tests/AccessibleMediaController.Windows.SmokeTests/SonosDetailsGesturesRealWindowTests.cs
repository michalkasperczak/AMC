using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// STRZALKA W LEWO oraz CTRL+C / CTRL+SHIFT+C w MOICH STACJACH i w ULUBIONYCH -
/// MIERZONE NA PRAWDZIWYCH MODALACH WPF i PRAWDZIWYM SCHOWKU WINDOWS.
///
/// Dlaczego nie na helperze: regule "co powiedziec" mierzy Core
/// (<c>--sonos-favorite-details</c>). Tutaj sprawdzamy to, czego Core sprawdzic
/// NIE MOZE: czy produkcyjny <c>Window_PreviewKeyDown</c> w ogole widzi te gesty,
/// czy do schowka IDZIE SAMA NAZWA (nie etykieta wiersza), czy brak adresu
/// ZOSTAWIA schowek nietkniety i czy spozniona odpowiedz po zmianie zaznaczenia
/// nie opisuje CUDZEJ pozycji.
///
/// CZEGO NIE DOWODZI: NICZEGO o prawdziwym glosniku Sonos ani o zadnym koncie -
/// transport i konto sa syntetyczne, a zadne POST/Play/Load tu nie leci.
/// Odsluchem to NIE JEST.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunDetailsGestures()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                MeasureOwnStationCopyAndParameters();
                MeasureOwnStationUrlChangeInvalidatesParameters();
                MeasureFavoriteCopyNameAndMissingLocation();
                MeasureFavoriteParametersIgnoreOtherMaterial();
                MeasureFavoriteParametersStayFreshInOpenWindow();
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(300)))
        {
            throw new Exception("Limit pomiaru gestów parametrów i kopiowania");
        }

        if (failure is not null) throw failure;
        Console.WriteLine(
            "OK: Strzałka w lewo i Ctrl+C/Ctrl+Shift+C w Moich stacjach i Ulubionych");
    }

    // ==================== MOJE STACJE ====================

    /// <summary>
    /// MOJE STACJE: CTRL+C oddaje SAMA NAZWE, CTRL+SHIFT+C DOSLOWNY adres ze
    /// wpisu, a STRZALKA W LEWO wola droge wlasciciela DOKLADNIE RAZ na stacje -
    /// takze gdy gest powtorzy sie przed koncem odczytu.
    /// </summary>
    private static void MeasureOwnStationCopyAndParameters()
    {
        Console.Error.WriteLine("MOJE STACJE: Ctrl+C, Ctrl+Shift+C, Strzałka w lewo");
        using var harness = RealHarness.Create();
        harness.Window.StateForTests.Sonos.OwnStreams =
        [
            new() { Id = "ID-1", Name = "Radio Pierwsze", StreamUrl = "http://przyklad.test/jeden" },
            new() { Id = "ID-2", Name = "Radio Drugie", StreamUrl = "http://przyklad.test/dwa" },
            new() { Id = "ID-3", Name = "Bez adresu", StreamUrl = "" },
        ];
        harness.EnterSonosSessionForMeasurement();

        WithOwnStreamsWindow(harness, window =>
        {
            var list = StationsListOf(window);

            // --- CTRL+C: SAMA NAZWA ---
            list.SelectedIndex = 1;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            SetClipboardMarker("ZNACZNIK-PRZED-NAZWA");
            PressOnList(harness, window, Key.C, ModifierKeys.Control);
            var copied = ReadClipboard();
            if (copied != "Radio Drugie")
            {
                throw new Exception("Ctrl+C nie skopiowało samej nazwy zaznaczonej stacji, "
                    + "schowek ma: " + Describe(copied));
            }

            // --- CTRL+SHIFT+C: DOSLOWNY ADRES ---
            SetClipboardMarker("ZNACZNIK-PRZED-ADRESEM");
            PressOnList(harness, window, Key.C, ModifierKeys.Control | ModifierKeys.Shift);
            copied = ReadClipboard();
            if (copied != "http://przyklad.test/dwa")
            {
                throw new Exception("Ctrl+Shift+C nie skopiowało dosłownego adresu wpisu, "
                    + "schowek ma: " + Describe(copied));
            }

            // --- BRAK ADRESU NIE CZYSCI SCHOWKA ---
            list.SelectedIndex = 2;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            const string keep = "ZNACZNIK-KTORY-MA-ZOSTAC";
            SetClipboardMarker(keep);
            PressOnList(harness, window, Key.C, ModifierKeys.Control | ModifierKeys.Shift);
            copied = ReadClipboard();
            if (copied != keep)
            {
                throw new Exception("Stacja BEZ adresu ruszyła schowek - zostało: " + Describe(copied));
            }

            if (window.StatusForTests is not { } status
                || !status.Contains("nie ma zapisanego adresu", StringComparison.CurrentCulture))
            {
                throw new Exception("Brak adresu nie został POWIEDZIANY, status: "
                    + Describe(window.StatusForTests));
            }

            // --- STRZALKA W LEWO: JEDNA droga wlasciciela na stacje ---
            // Callback jest SZTUCZNY (nie chcemy sieci w pomiarze), ale sciezka
            // gestu, ochrona zaznaczenia i pamiec wyniku sa PRODUKCYJNE.
            var calls = new List<string>();
            var release = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.DescribeStation = station =>
            {
                calls.Add(station.Id);
                return release.Task;
            };

            list.SelectedIndex = 0;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            if (calls.Count != 1)
            {
                throw new Exception("Trzy Strzałki w lewo na JEDNEJ stacji poszły do właściciela "
                    + calls.Count + " razy - serie gestów mają dzielić jeden odczyt.");
            }

            release.SetResult("Radio Pierwsze, audio/mpeg, mp3, 128 kb/s");
            harness.PumpUntil(
                () => window.StatusForTests?.Contains("mp3", StringComparison.Ordinal) == true,
                TimeSpan.FromSeconds(5), "parametry stacji nie zostały powiedziane");

            // --- POWTORZONY GEST KORZYSTA Z PAMIECI, nie z sieci ---
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            if (calls.Count != 1)
            {
                throw new Exception("Powtórzony gest na tej samej stacji znów zapytał sieć: "
                    + calls.Count + " wywołań.");
            }

            // --- SPOZNIONA ODPOWIEDZ NIE OPISUJE CUDZEJ STACJI ---
            var late = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            window.DescribeStation = station => { calls.Add(station.Id); return late.Task; };
            list.SelectedIndex = 1;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            // ZAZNACZENIE UCIEKA, dopiero potem przychodzi odpowiedz o POPRZEDNIEJ.
            list.SelectedIndex = 2;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            var statusBefore = window.StatusForTests;
            late.SetResult("PARAMETRY-STACJI-DRUGIEJ-ktore-nie-maja-prawa-wybrzmiec");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(250));
            if (window.StatusForTests?.Contains("DRUGIEJ", StringComparison.Ordinal) == true)
            {
                throw new Exception("Spóźnione parametry opisały CUDZĄ stację po zmianie zaznaczenia.");
            }

            _ = statusBefore;
        });
    }

    /// <summary>
    /// ZMIANA ADRESU POD TYM SAMYM ID UNIEWAZNIA PAMIEC PARAMETROW. Edycja wpisu
    /// zachowuje <c>Id</c>, wiec bez unieważnienia Strzalka w lewo czytalaby
    /// parametry STAREGO strumienia jako opis nowego adresu.
    /// </summary>
    private static void MeasureOwnStationUrlChangeInvalidatesParameters()
    {
        Console.Error.WriteLine("MOJE STACJE: zmiana adresu unieważnia pamięć parametrów");
        using var harness = RealHarness.Create();
        harness.Window.StateForTests.Sonos.OwnStreams =
            [new() { Id = "ID-1", Name = "Radio Pierwsze", StreamUrl = "http://przyklad.test/stary" }];
        harness.EnterSonosSessionForMeasurement();

        WithOwnStreamsWindow(harness, window =>
        {
            var list = StationsListOf(window);
            var seen = new List<string>();
            window.DescribeStation = station =>
            {
                seen.Add(station.StreamUrl);
                return Task.FromResult("Parametry dla " + station.StreamUrl);
            };

            list.SelectedIndex = 0;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            harness.PumpUntil(() => seen.Count == 1, TimeSpan.FromSeconds(5),
                "pierwszy odczyt parametrów nie doszedł");

            // ZMIANA ADRESU PRODUKCYJNA DROGA ZAPISU (CommitStation), z TYM SAMYM Id.
            var commit = typeof(SonosOwnStreamsWindow).GetMethod("CommitStation",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new Exception("Okno Moich stacji nie ma CommitStation - sprawdź źródło.");
            var rows = (System.Collections.IList)typeof(SonosOwnStreamsWindow)
                .GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window)!;
            var old = (SonosOwnStreamSettings)rows[0]!;
            commit.Invoke(window, [old, new SonosOwnStreamSettings
            {
                Id = old.Id, Name = old.Name, StreamUrl = "http://przyklad.test/NOWY"
            }]);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(80));

            list.SelectedIndex = 0;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            PressOnList(harness, window, Key.Left, ModifierKeys.None);
            harness.PumpUntil(() => seen.Count == 2, TimeSpan.FromSeconds(5),
                "po zmianie adresu parametry NIE zostały odczytane ponownie - "
                + "pamięć zwróciła opis starego strumienia");

            if (seen[1] != "http://przyklad.test/NOWY")
            {
                throw new Exception("Po zmianie adresu odczyt poszedł na: " + Describe(seen[1]));
            }
        });
    }

    // ==================== ULUBIONE ====================

    /// <summary>
    /// ULUBIONE: CTRL+C oddaje SAMA NAZWE (nie etykiete wiersza z usluga i
    /// opisem), a CTRL+SHIFT+C przy BRAKU adresu mowi to krotko i NIE RUSZA
    /// schowka. Adresu w <c>getFavorites</c> nie ma - to nie awaria.
    /// </summary>
    private static void MeasureFavoriteCopyNameAndMissingLocation()
    {
        Console.Error.WriteLine("ULUBIONE: Ctrl+C sama nazwa, Ctrl+Shift+C bez adresu");
        using var harness = RealHarness.Create(() => DescribedFavoritesBody);
        harness.Enter();

        harness.RunFavoritesModal(window =>
        {
            var list = FavoritesListOf(window);
            if (list.Items.Count < 2)
            {
                throw new Exception("Pomiar potrzebuje co najmniej dwóch ulubionych, jest: "
                    + list.Items.Count);
            }

            list.SelectedIndex = 1;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            var selected = window.SelectedFavoriteForTests
                ?? throw new Exception("Lista ulubionych nie oddała zaznaczonej pozycji.");

            // --- CTRL+C: SAMA NAZWA, NIE ETYKIETA WIERSZA ---
            SetClipboardMarker("ZNACZNIK-PRZED");
            PressOnFavorites(harness, window, Key.C, ModifierKeys.Control);
            var copied = ReadClipboard();
            if (copied != selected.Name)
            {
                throw new Exception("Ctrl+C nie skopiowało samej nazwy ulubionego, schowek ma: "
                    + Describe(copied));
            }

            var label = SonosFavoritesLabels.Describe(selected);
            // SPRAWDZENIE SAMEJ APARATURY: gdyby etykieta wiersza byla ROWNA
            // nazwie, porownanie ponizej nie mialoby jak upasc i pomiar byłby
            // pusty. Zmierzone: zastany FavoritesBody aparatury wlasnie tak
            // wygladal (same nazwy), dlatego ten zestaw ma wlasny payload.
            if (label == selected.Name)
            {
                throw new Exception("APARATURA BEZ RÓŻNICY: etykieta wiersza jest równa nazwie ("
                    + Describe(label) + "), więc pomiar \"Ctrl+C kopiuje samą nazwę\" nic nie mierzy.");
            }

            if (copied == label)
            {
                throw new Exception("Do schowka trafiła ETYKIETA wiersza (z usługą/opisem) "
                    + "zamiast samej nazwy.");
            }

            // --- CTRL+SHIFT+C: syntetyczna chmura NIE PODAJE mediaUrl ---
            // Trasa playbackMetadata aparatury oddaje INNY material bez mediaUrl,
            // wiec poprawna odpowiedz to KROTKA ODMOWA i NIETKNIETY schowek.
            const string keep = "ZNACZNIK-KTORY-MA-ZOSTAC";
            SetClipboardMarker(keep);
            PressOnFavorites(harness, window, Key.C, ModifierKeys.Control | ModifierKeys.Shift);
            harness.PumpUntil(
                () => window.StatusForTests.Contains("adres", StringComparison.CurrentCulture),
                TimeSpan.FromSeconds(5), "Ctrl+Shift+C nic nie powiedziało o adresie");

            copied = ReadClipboard();
            if (copied != keep)
            {
                throw new Exception("Brak adresu ulubionego RUSZYŁ schowek - zostało: "
                    + Describe(copied));
            }
        });
    }

    /// <summary>
    /// ULUBIONE: STRZALKA W LEWO opisuje ZAZNACZONA pozycje i NIE DOPINA do niej
    /// parametrow CUDZEGO, wlasnie grajacego materialu. Trasa metadanych
    /// aparatury oddaje INNA, kompletna trojke, wiec jedyna uczciwa odpowiedz to
    /// nazwa plus informacja o braku parametrow.
    /// </summary>
    private static void MeasureFavoriteParametersIgnoreOtherMaterial()
    {
        Console.Error.WriteLine("ULUBIONE: Strzałka w lewo nie opisuje cudzego materiału");
        using var harness = RealHarness.Create(() => DescribedFavoritesBody);
        harness.Enter();

        harness.RunFavoritesModal(window =>
        {
            var list = FavoritesListOf(window);
            list.SelectedIndex = 0;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            var selected = window.SelectedFavoriteForTests!;

            PressOnFavorites(harness, window, Key.Left, ModifierKeys.None);
            harness.PumpUntil(
                () => window.StatusForTests.StartsWith(selected.Name, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5), "Strzałka w lewo nie powiedziała nic o zaznaczonym ulubionym");

            var said = window.StatusForTests;
            if (said.Contains("Obcy", StringComparison.CurrentCulture))
            {
                throw new Exception("Opis ulubionego zawiera CUDZY, grający materiał: " + Describe(said));
            }

            if (!said.Contains(SonosFavoriteDetails.NoParameters, StringComparison.Ordinal))
            {
                throw new Exception("Brak potwierdzonych parametrów nie został powiedziany: "
                    + Describe(said));
            }
        });
    }

    /// <summary>
    /// ULUBIONE: PO POJAWIENIU SIE METADANYCH Strzalka w lewo W NADAL OTWARTYM
    /// OKNIE mowi SWIEZE parametry, a nie zapamietana odmowe.
    ///
    /// DLACZEGO TO MA ZNACZENIE: pierwsza odpowiedz na ulubionym radiu typowo
    /// NIE MA parametrow (grupa jeszcze nie zaladowala tego materialu), a
    /// metadane pojawiaja sie chwile pozniej. Okno pamietalo opis PO SAMYM Id,
    /// wiec uzytkownik do konca zycia modalu slyszalby "Sonos nie podał
    /// parametrów", mimo ze Sonos juz je podaje.
    ///
    /// POMIAR CELOWO NIE PRZYPISUJE PONOWNIE <c>DescribeFavorite</c>: setter
    /// czysci pamiec i ukrylby wlasnie ten blad. Zmieniamy WYNIK ISTNIEJACEGO
    /// callbacka, tak jak zmienia go zywy Sonos.
    /// </summary>
    private static void MeasureFavoriteParametersStayFreshInOpenWindow()
    {
        Console.Error.WriteLine("ULUBIONE: świeże parametry po pojawieniu się metadanych");
        using var harness = RealHarness.Create(() => DescribedFavoritesBody);
        harness.Enter();

        harness.RunFavoritesModal(window =>
        {
            var list = FavoritesListOf(window);
            list.SelectedIndex = 0;
            harness.PumpQuietly(TimeSpan.FromMilliseconds(40));
            var selected = window.SelectedFavoriteForTests
                ?? throw new Exception("Lista ulubionych nie oddała zaznaczonej pozycji.");

            // JEDEN callback na cale zycie okna. Jego WYNIK sie zmienia, tak jak
            // u zywego Sonosa: najpierw grupa nie ma tego materialu, potem ma.
            var calls = 0;
            var metadataArrived = false;
            window.DescribeFavorite = favorite =>
            {
                calls++;
                return Task.FromResult(metadataArrived
                    ? favorite.Name + ", TuneIn, audio/mpeg, mp3, 44,1 kHz"
                    : SonosFavoriteDetails.DescribeParameters(favorite, null));
            };

            // --- PIERWSZY ODCZYT: parametrow JESZCZE NIE MA ---
            PressOnFavorites(harness, window, Key.Left, ModifierKeys.None);
            harness.PumpUntil(
                () => window.StatusForTests.Contains(
                    SonosFavoriteDetails.NoParameters, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5),
                "pierwszy odczyt parametrów ulubionego nie doszedł");
            if (calls != 1)
            {
                throw new Exception("Pierwsza Strzałka w lewo wywołała właściciela "
                    + calls + " razy - pomiar świeżości nie ma punktu wyjścia.");
            }

            // --- METADANE SIE POJAWILY, wiersz i okno TE SAME ---
            metadataArrived = true;
            PressOnFavorites(harness, window, Key.Left, ModifierKeys.None);
            harness.PumpUntil(
                () => window.StatusForTests.Contains("mp3", StringComparison.Ordinal),
                TimeSpan.FromSeconds(5),
                "po pojawieniu się metadanych Strzałka w lewo POWTÓRZYŁA zapamiętaną "
                + "odmowę - okno trzyma opis po samym Id i nigdy nie odświeża parametrów");

            if (calls < 2)
            {
                throw new Exception("Powtórzony gest NIE zapytał właściciela o świeże "
                    + "parametry: " + calls + " wywołań.");
            }

            if (!window.StatusForTests.StartsWith(selected.Name, StringComparison.Ordinal))
            {
                throw new Exception("Świeży opis nie zaczyna się od nazwy ulubionego: "
                    + Describe(window.StatusForTests));
            }
        });
    }

    // ==================== APARATURA ====================

    /// <summary>
    /// ULUBIONE Z OPISEM I USLUGA - zastany <c>FavoritesBody</c> aparatury ma
    /// TYLKO nazwy, wiec etykieta wiersza bylaby tam ROWNA nazwie i pomiar
    /// "Ctrl+C kopiuje SAMA NAZWE, nie etykiete" nie mialby jak upasc.
    /// Zmierzone: po podmianie produkcji na <c>Describe(favorite)</c> ten
    /// zestaw ZAPALA sie na czerwono, zastany payload NIE zapalal.
    ///
    /// Pola sa z OFICJALNEGO kontraktu getFavorites (name, description, service).
    /// ADRESU tu NIE MA, bo <c>getFavorites</c> go NIE ZWRACA - to nie brak
    /// aparatury, tylko prawda o API.
    /// </summary>
    private const string DescribedFavoritesBody =
        "{\"version\":\"W1\",\"items\":["
        + "{\"id\":\"ULU-PIERWSZY\",\"name\":\"Radio Nasze\","
        + "\"description\":\"Muzyka klasyczna bez przerw\","
        + "\"service\":{\"name\":\"TuneIn\",\"id\":\"254\"},"
        + "\"resource\":{\"id\":{\"serviceId\":\"254\",\"objectId\":\"OBIEKT-PIERWSZY\","
        + "\"accountId\":\"KONTO-SYNTETYCZNE-9\"}}},"
        + "{\"id\":\"ULU-DRUGI\",\"name\":\"Nokturny Chopina\","
        + "\"description\":\"Nagranie z 1999 roku\","
        + "\"service\":{\"name\":\"Tidal\",\"id\":\"38\"},"
        + "\"resource\":{\"id\":{\"serviceId\":\"38\",\"objectId\":\"OBIEKT-DRUGI\","
        + "\"accountId\":\"KONTO-SYNTETYCZNE-9\"}}}]}";


    private static ListBox FavoritesListOf(SonosFavoritesWindow window) =>
        (ListBox)window.FindName("FavoritesList")!;

    /// <summary>
    /// FIZYCZNY gest na liscie ulubionych. Ta sama droga, co w Moich stacjach:
    /// PRAWDZIWE modyfikatory w stanie klawiatury watku i PRODUKCYJNY
    /// <c>Window_PreviewKeyDown</c>.
    /// </summary>
    private static void PressOnFavorites(RealHarness harness, SonosFavoritesWindow window,
        Key key, ModifierKeys modifiers)
    {
        var list = FavoritesListOf(window);
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
                ?? throw new Exception("Okno ulubionych nie ma powierzchni prezentacji.");
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
                Source = list,
            };
            typeof(SonosFavoritesWindow)
                .GetMethod("Window_PreviewKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, [window, args]);
        }
        finally { SetKeyboardState(previous); }

        harness.PumpQuietly(TimeSpan.FromMilliseconds(60));
    }

    /// <summary>
    /// ZNACZNIK W PRAWDZIWYM SCHOWKU WINDOWS przed gestem. Dzieki temu "schowek
    /// nietkniety" jest MIERZALNY, a nie zalozony - pusty schowek nie odrozniłby
    /// "nic nie zrobiono" od "wyczyszczono".
    /// </summary>
    private static void SetClipboardMarker(string marker)
    {
        if (!ClipboardSetWithRetry(marker))
        {
            throw new Exception("Pomiar nie mógł ustawić znacznika w schowku Windows.");
        }

        if (ReadClipboard() != marker)
        {
            throw new Exception("Schowek Windows nie przyjął znacznika pomiaru.");
        }
    }

    /// <summary>
    /// ODCZYT PRAWDZIWEGO schowka Windows z ponowieniami - schowek jest
    /// zasobem calego pulpitu i potrafi byc chwilowo zajety przez inny proces.
    /// </summary>
    private static string? ReadClipboard()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch (COMException) { Thread.Sleep(30); }
            catch (ExternalException) { Thread.Sleep(30); }
        }

        throw new Exception("Nie udało się odczytać schowka Windows po 10 próbach.");
    }

    private static bool ClipboardSetWithRetry(string text)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try { Clipboard.SetText(text); return true; }
            catch (COMException) { Thread.Sleep(30); }
            catch (ExternalException) { Thread.Sleep(30); }
        }

        return false;
    }

    /// <summary>Opis wartosci do komunikatu bledu - cisza musi byc widoczna.</summary>
    private static string Describe(string? value) =>
        value is null ? "(pusty schowek)" : "\"" + value + "\"";
}
