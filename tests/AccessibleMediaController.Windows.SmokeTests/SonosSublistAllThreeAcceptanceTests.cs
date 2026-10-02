using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// ODBIOR NAWIGACJI PODLIST SONOSA DLA WSZYSTKICH TRZECH LIST.
///
/// CZEGO BRAKOWALO W 4c4ad86: pomiar dotykal TYLKO Moich stacji. Ulubione i
/// Playlisty byly wpiete w ten sam mechanizm BEZ ZADNEGO POMIARU, a otwieraja sie
/// DROGA ASYNCHRONICZNA (odczyt HTTP przed powstaniem okna) - czyli dokladnie tam,
/// gdzie zapamietany wiersz najlatwiej zginie. Pomiar tylko na Core by tego nie
/// wykryl, bo Core nie ma ani okna, ani kolejnosci zdarzen.
///
/// CO TU JEST MIERZONE NA KAZDEJ Z TRZECH LIST:
///  1) Enter PRODUKCYJNA droga Biblioteki otwiera podliste;
///  2) stoimy na DRUGIM wierszu (pierwszy nie odroznilby powrotu od otwarcia od zera);
///  3) Ctrl+numer_radia przelacza sesje BEZ recznego zamykania listy;
///  4) Ctrl+numer_sonosa wraca do TEJ SAMEJ podlisty, na TEN SAM wiersz, z fokusem
///     na tym wierszu i z tym samym celem;
///  5) ZERO dodatkowych POST na calej drodze.
///
/// ORAZ GRANICE, ktore stare zachowanie lamalo:
///  6) Ctrl+slot_JUZ_AKTYWNEJ_sesji NIE wyrzuca z podlisty;
///  7) Ctrl+slot_NIEPRZYPISANY NIE wyrzuca z podlisty;
///  8) Escape NIE zostawia zadania powrotu (zwykle zamkniecie to nie powrot);
///  9) zapis powrotu dla kategorii A NIE jest konsumowany przez otwarcie kategorii B.
///
/// KLAWISZ: ten sam sposob, co istniejacy pomiar przelaczania - PRAWDZIWIE
/// wcisniety Control przez SetKeyboardState i zdarzenie tunelujace na realnym
/// zrodle prezentacji. To nadal SYNTETYCZNE WPF, nie SendInput: nazwa pliku i ten
/// komentarz tego NIE UKRYWAJA. Fizyczny klawisz i odczyt NVDA ida osobno, przez
/// pokaz --sonos-final-gui.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    /// <summary>Opis jednej podlisty dla wspolnego pomiaru - bez kopiowania ciala testu.</summary>
    private sealed record SublistUnderTest(
        string CategoryId,
        string LibraryLabelFragment,
        Func<MainWindow, Window?> OpenWindow,
        Func<Window, string?> HighlightedRowId,
        Func<Window, int> RowCount,
        Func<Window, string?> FocusedRowId,
        string SecondRowId);

    internal static void RunSublistAllThree()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { MeasureAllThreeSublists(); }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(300)))
        {
            throw new Exception("Limit pomiaru odbioru nawigacji trzech podlist Sonosa");
        }

        if (failure is not null) throw failure;
        Console.WriteLine(
            "OK: wszystkie trzy podlisty Sonosa - Enter, drugi wiersz, Ctrl+cyfra tam i z powrotem, "
            + "ten sam wiersz i fokus, 0 dodatkowych POST, granice slotu/Escape/kategorii");
    }

    private static void MeasureAllThreeSublists()
    {
        foreach (var sublist in DescribeSublists())
        {
            Console.Error.WriteLine("PODLISTA: " + sublist.CategoryId);
            MeasureRoundTrip(sublist);
            Console.Error.WriteLine("  round-trip OK");
        }

        MeasureAlreadyActiveSlotKeepsSublist();
        Console.Error.WriteLine("GRANICA: slot juz aktywnej sesji - OK");
        MeasureUnassignedSlotKeepsSublist();
        Console.Error.WriteLine("GRANICA: slot nieprzypisany - OK");
        MeasureEscapeDropsReturn();
        Console.Error.WriteLine("GRANICA: Escape nie zostawia powrotu - OK");
        MeasurePendingRowIsNotStolenByOtherCategory();
        Console.Error.WriteLine("GRANICA: wiersz jednej kategorii nie trafia do innej - OK");
    }

    /// <summary>
    /// TRZY PODLISTY opisane danymi, nie trzema kopiami testu. Identyfikatory
    /// DRUGIEGO wiersza pochodza z tych samych cial odpowiedzi, ktorych uzywa
    /// istniejaca aparatura (FavoritesBody, PlaylistsAcceptanceBody) i z
    /// konfiguracji wlasnych stacji.
    /// </summary>
    private static SublistUnderTest[] DescribeSublists() =>
    [
        new(
            SonosLibraryPresentation.OwnStreamsCategoryId,
            "stacje",
            window => window.OpenSonosOwnStreamsWindowForTests,
            window => ((SonosOwnStreamsWindow)window).HighlightedStationIdForTests,
            window => ((ListBox)window.FindName("StationsList")!).Items.Count,
            window => FocusedRowIdOf(window, "StationsList"),
            "station-two"),
        new(
            SonosLibraryPresentation.FavoritesCategoryId,
            "ulubione",
            window => window.OpenSonosFavoritesWindowForTests,
            window => ((SonosFavoritesWindow)window).SelectedFavoriteForTests?.Id,
            window => ((SonosFavoritesWindow)window).RowCountForTests,
            window => FocusedRowIdOf(window, "FavoritesList"),
            "ULU-DRUGI"),
        new(
            SonosLibraryPresentation.PlaylistsCategoryId,
            "playlist",
            window => window.OpenSonosPlaylistsWindowForTests,
            window => ((SonosPlaylistsWindow)window).HighlightedPlaylistIdForTests,
            window => ((SonosPlaylistsWindow)window).RowCountForTests,
            window => FocusedRowIdOf(window, "PlaylistsList"),
            "LISTA-DRUGA"),
    ];

    /// <summary>
    /// CZY FOKUS KLAWIATURY STOI NA ZAZNACZONYM WIERSZU. Dla czytnika ekranu to
    /// nie ozdoba: samo SelectedIndex bez fokusu nie zostanie przeczytane, a
    /// uzytkownik nie uslyszy, gdzie wrocil.
    /// </summary>
    private static string? FocusedRowIdOf(Window window, string listName)
    {
        var list = (ListBox)window.FindName(listName)!;
        if (!list.IsKeyboardFocusWithin) return "<fokus poza listą>";
        return list.SelectedIndex >= 0 ? "<fokus na liście>" : "<brak zaznaczenia>";
    }

    /// <summary>
    /// CIALA ODPOWIEDZI dla playlist i ulubionych: DWA wiersze, zeby "drugi
    /// wiersz" mial sens. Ulubione maja JUZ cialo w istniejacej aparaturze
    /// (ULU-PIERWSZY/ULU-DRUGI) - tu dochodza tylko playlisty.
    /// </summary>
    private const string PlaylistsAcceptanceBody =
        "{\"version\":\"PL1\",\"playlists\":["
        + "{\"id\":\"LISTA-PIERWSZA\",\"name\":\"Pierwsza playlista\",\"type\":\"playlist\",\"trackCount\":3},"
        + "{\"id\":\"LISTA-DRUGA\",\"name\":\"Druga playlista\",\"type\":\"playlist\",\"trackCount\":5}]}";

    /// <summary>
    /// PELNA DROGA TAM I Z POWROTEM dla JEDNEJ podlisty. Kazda podlista dostaje
    /// SWOJA harness - stan sesji ma byc swiezy, zeby jedno przejscie nie
    /// podpieralo nastepnego.
    /// </summary>
    private static void MeasureRoundTrip(SublistUnderTest sublist)
    {
        using var h = CreateAcceptanceHarness();
        h.Enter();

        var sonosSlot = h.Window.SessionsForTests.FindSlot("sonos")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji sonos.");
        var radioSlot = h.Window.SessionsForTests.FindSlot("radio")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji radio.");
        if (sonosSlot == radioSlot)
            throw new Exception("Sloty sesji się pokrywają - pomiar nic nie rozdzieli.");

        var postsBefore = h.Handler.Posts.Count;
        var reachedSecondRow = false;

        RunLibraryCategoryPhase(h, sublist, dialog =>
        {
            if (sublist.RowCount(dialog) < 2)
                throw new Exception("Podlista ma mniej niż dwa wiersze - powrót byłby nierozróżnialny.");

            SelectSecondRow(dialog, sublist);
            if (!string.Equals(sublist.HighlightedRowId(dialog), sublist.SecondRowId, StringComparison.Ordinal))
            {
                throw new Exception("Nie udało się stanąć na drugim wierszu: "
                    + sublist.HighlightedRowId(dialog));
            }

            reachedSecondRow = true;
            SendCtrlDigit(dialog, ListOf(dialog, sublist), radioSlot);
        });

        if (!reachedSecondRow)
            throw new Exception("Nie dojechano produkcyjną drogą do podlisty " + sublist.CategoryId + ".");

        // PODLISTA ZAMYKA SIE SAMA: to byla cala tresc zgloszenia.
        h.PumpUntil(
            () => sublist.OpenWindow(h.Window) is null || sublist.OpenWindow(h.Window)?.IsVisible != true,
            "podlista nie zamknęła się po Ctrl+cyfra - wymagałaby ręcznego zamknięcia");
        h.PumpUntil(
            () => string.Equals(h.Window.SessionsForTests.Current.Id, "radio", StringComparison.Ordinal),
            "Ctrl+cyfra z wnętrza podlisty NIE przełączyło sesji na radio");

        // ZAPAMIETANE MIEJSCE musi opisywac KONKRETNY wiersz, nie sama kategorie.
        var pending = h.Window.SonosSublistReturnForTests
            ?? throw new Exception("Nie zapamiętano podlisty do powrotu.");
        if (!string.Equals(pending.CategoryId, sublist.CategoryId, StringComparison.Ordinal))
            throw new Exception("Zapamiętano inną kategorię: " + pending.CategoryId);
        if (!string.Equals(pending.SelectedRowId, sublist.SecondRowId, StringComparison.Ordinal))
            throw new Exception("Zapamiętano inny wiersz: " + pending.SelectedRowId);

        var groupBeforeReturn = h.Window.SonosActiveGroup?.Id;
        var reopenedBefore = h.Window.SonosSublistReopenedForTests;

        // POWROT: Ctrl+numer_sonosa z OKNA GLOWNEGO.
        Window? returned = null;
        string? returnedRow = null;
        string? returnedFocus = null;
        var watch = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(20) };
        watch.Tick += (_, _) =>
        {
            if (sublist.OpenWindow(h.Window) is { IsVisible: true } dialog && dialog.IsLoaded)
            {
                // CZYTAMY WIERSZ I FOKUS, DOPOKI OKNO ZYJE: po zamknieciu
                // SelectedItem nic by juz nie dowodzil.
                returned = dialog;
                returnedRow = sublist.HighlightedRowId(dialog);
                returnedFocus = sublist.FocusedRowId(dialog);
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
            SendCtrlDigit(h.Window, (ListBox)h.Window.FindName("MediaList")!, sonosSlot);
            h.PumpUntil(() => returned is not null,
                "powrót Ctrl+cyfra NIE otworzył ponownie podlisty (wylądowano w korzeniu sesji)");
        }
        finally { watch.Stop(); }

        if (h.Window.SonosSublistReopenedForTests != reopenedBefore + 1)
            throw new Exception("Powrót nie przeszedł istniejącą drogą kategorii Biblioteki.");

        if (!string.Equals(returnedRow, sublist.SecondRowId, StringComparison.Ordinal))
        {
            throw new Exception("Powrót otworzył podlistę, ale na wierszu " + returnedRow
                + " zamiast " + sublist.SecondRowId + ".");
        }

        if (returnedFocus != "<fokus na liście>")
            throw new Exception("Po powrocie fokus klawiatury nie stoi na liście: " + returnedFocus);

        // CEL STEROWANIA ten sam: powrot nie ma prawa przeadresowac Sonosa.
        if (!string.Equals(h.Window.SonosActiveGroup?.Id, groupBeforeReturn, StringComparison.Ordinal))
            throw new Exception("Powrót zmienił cel sterowania Sonos.");

        // ZADANIE JEDNORAZOWE: nie wisi po wykorzystaniu.
        if (h.Window.SonosSublistReturnForTests is not null)
            throw new Exception("Zadanie powrotu zostało wiszące po wykorzystaniu.");

        // ZERO DODATKOWYCH POST na calej drodze tam i z powrotem.
        if (h.Handler.Posts.Count != postsBefore)
        {
            throw new Exception($"Nawigacja wysłała {h.Handler.Posts.Count - postsBefore} dodatkowy POST - "
                + "ani przełączenie sesji, ani powrót nie mają prawa nic odtwarzać.");
        }
    }

    // ==================== GRANICE ====================

    /// <summary>
    /// Ctrl+SLOT JUZ AKTYWNEJ SESJI. Stare zachowanie zamykalo caly stos modalny
    /// BEZWARUNKOWO, a <c>ExecuteCommand</c> dla tego slotu niczego nie zmienial -
    /// uzytkownik tracil otwarta liste w zamian za nic.
    /// </summary>
    private static void MeasureAlreadyActiveSlotKeepsSublist()
    {
        using var h = CreateAcceptanceHarness();
        h.Enter();
        var sonosSlot = h.Window.SessionsForTests.FindSlot("sonos")!.Value;
        var sublist = DescribeSublists()[0];
        var stillOpen = false;
        var sessionAfter = string.Empty;

        RunLibraryCategoryPhase(h, sublist, dialog =>
        {
            SelectSecondRow(dialog, sublist);
            SendCtrlDigit(dialog, ListOf(dialog, sublist), sonosSlot);
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(300));
            stillOpen = dialog.IsVisible;
            sessionAfter = h.Window.SessionsForTests.Current.Id;
            // ODMOWA ZOSTAWIA OKNO OTWARTE - i o to chodzi. Musimy je zamknac sami,
            // inaczej zagniezdzony ShowDialog nigdy nie wroci i pomiar stanie.
            CloseAfterCapture(dialog);
        });

        if (!stillOpen)
            throw new Exception("Ctrl+slot JUŻ AKTYWNEJ sesji zamknął podlistę i wyrzucił do korzenia.");
        if (!string.Equals(sessionAfter, "sonos", StringComparison.Ordinal))
            throw new Exception("Slot własnej sesji zmienił sesję na " + sessionAfter + ".");
        if (h.Window.SonosSublistReturnForTests is not null)
            throw new Exception("Odmowa wyjścia zostawiła zadanie powrotu.");
    }

    /// <summary>
    /// Ctrl+SLOT NIEPRZYPISANY. Router mowi tylko "Sesja N nieprzypisana" i sesja
    /// zostaje - wiec zamkniecie listy bylo czysta strata.
    /// </summary>
    private static void MeasureUnassignedSlotKeepsSublist()
    {
        using var h = CreateAcceptanceHarness();
        h.Enter();

        var slots = h.Window.SessionsForTests.SessionSlots;
        var freeSlot = Enumerable.Range(1, 9).FirstOrDefault(slot => !slots.ContainsKey(slot));
        if (freeSlot == 0)
        {
            Console.Error.WriteLine("  (pominięte: konfiguracja nie ma wolnego slotu 1-9)");
            return;
        }

        var sublist = DescribeSublists()[0];
        var stillOpen = false;
        var sessionAfter = string.Empty;

        RunLibraryCategoryPhase(h, sublist, dialog =>
        {
            SelectSecondRow(dialog, sublist);
            SendCtrlDigit(dialog, ListOf(dialog, sublist), freeSlot);
            LibraryFixture.Pump(TimeSpan.FromMilliseconds(300));
            stillOpen = dialog.IsVisible;
            sessionAfter = h.Window.SessionsForTests.Current.Id;
            CloseAfterCapture(dialog);
        });

        if (!stillOpen)
            throw new Exception("Ctrl+slot NIEPRZYPISANY zamknął podlistę i wyrzucił do korzenia.");
        if (!string.Equals(sessionAfter, "sonos", StringComparison.Ordinal))
            throw new Exception("Nieprzypisany slot zmienił sesję na " + sessionAfter + ".");
        if (h.Window.SonosSublistReturnForTests is not null)
            throw new Exception("Odmowa wyjścia zostawiła zadanie powrotu.");
    }

    /// <summary>
    /// ESCAPE to ZWYKLE ZAMKNIECIE, nie odlozony powrot: nastepne wejscie w sesje
    /// Sonos NIE MA prawa otworzyc listy, ktora uzytkownik sam zamknal.
    /// </summary>
    private static void MeasureEscapeDropsReturn()
    {
        using var h = CreateAcceptanceHarness();
        h.Enter();
        var sublist = DescribeSublists()[0];

        RunLibraryCategoryPhase(h, sublist, dialog =>
        {
            SelectSecondRow(dialog, sublist);
            SendKey(dialog, ListOf(dialog, sublist), System.Windows.Input.Key.Escape);
        });

        h.PumpUntil(
            () => sublist.OpenWindow(h.Window) is null || sublist.OpenWindow(h.Window)?.IsVisible != true,
            "Escape nie zamknął podlisty");

        if (h.Window.SonosSublistReturnForTests is not null)
            throw new Exception("Escape zostawił zadanie powrotu - lista wróciłaby sama.");
        if (h.Window.SonosSublistPendingRowId is not null)
            throw new Exception("Escape zostawił wiszący wiersz do zaznaczenia.");
    }

    /// <summary>
    /// ZAPIS DLA KATEGORII A NIE MA PRAWA TRAFIC DO KATEGORII B.
    ///
    /// ZMIERZONA USTERKA: <c>_sonosSublistPendingRowId</c> bylo JEDNYM polem BEZ
    /// kategorii, a odbiorca czytal je BEZWARUNKOWO. Otwarcie podlisty potrafi
    /// ODMOWIC *przed* odbiorem wiersza - <c>ShowSonosOwnStreams</c> sprawdza
    /// <c>CanPresentSonosChildWindow()</c> i wychodzi, zanim dojdzie do
    /// <c>ConsumeSonosSublistPendingRowId</c>. Zapis zostawal wtedy wiszacy i
    /// konsumowalo go NASTEPNE, zwykle otwarcie INNEJ listy.
    ///
    /// DLATEGO ODMOWE WYWOLUJEMY PRAWDZIWA DROGA: ukrywamy okno glowne, zeby brama
    /// faktycznie odmowila, zamiast wpisywac stan reka.
    /// </summary>
    private static void MeasurePendingRowIsNotStolenByOtherCategory()
    {
        using var h = CreateAcceptanceHarness();
        h.Enter();

        var radioSlot = h.Window.SessionsForTests.FindSlot("radio")!.Value;

        // ZADANIE POWROTU do MOICH STACJI, zlozone produkcyjna droga podlisty.
        h.Window.RequestSessionSwitchFromSonosSublist(
            radioSlot, SonosLibraryPresentation.OwnStreamsCategoryId, "station-two");
        h.PumpUntil(
            () => string.Equals(h.Window.SessionsForTests.Current.Id, "radio", StringComparison.Ordinal),
            "zlecenie z podlisty nie przełączyło sesji");

        // ODMOWA OTWARCIA: niewidoczne okno glowne => CanPresentSonosChildWindow()
        // zwraca false i powrot konczy sie PRZED odbiorem wiersza.
        h.Window.Hide();
        LibraryFixture.Pump(TimeSpan.FromMilliseconds(100));
        var reopensBefore = h.Window.SonosSublistReopenedForTests;
        h.ExecuteCommand(CommandIds.SessionSlot(h.Window.SessionsForTests.FindSlot("sonos")!.Value));
        h.PumpUntil(() => h.Window.SonosSublistReopenedForTests > reopensBefore,
            "powrót do sesji Sonos nie spróbował ponownie otworzyć podlisty");
        LibraryFixture.Pump(TimeSpan.FromMilliseconds(300));

        if (h.Window.OpenSonosOwnStreamsWindowForTests is { IsVisible: true })
            throw new Exception("Przy niewidocznym oknie głównym podlista jednak się otworzyła.");

        // TERAZ pytamy jak INNA kategoria. Przed poprawka dostawalaby
        // "station-two" - wiersz Moich stacji - i stawala na nim.
        var stolen = h.Window.ConsumeSonosSublistPendingRowId(
            SonosLibraryPresentation.FavoritesCategoryId);
        if (stolen is not null)
            throw new Exception("Ulubione skonsumowały wiersz zapamiętany dla Moich stacji: " + stolen);

        var playlistsStole = h.Window.ConsumeSonosSublistPendingRowId(
            SonosLibraryPresentation.PlaylistsCategoryId);
        if (playlistsStole is not null)
            throw new Exception("Playlisty skonsumowały wiersz zapamiętany dla Moich stacji: " + playlistsStole);

        // WLASNA kategoria nadal swoj wiersz DOSTAJE: gate nie ma byc kasowaniem.
        var own = h.Window.ConsumeSonosSublistPendingRowId(
            SonosLibraryPresentation.OwnStreamsCategoryId);
        if (!string.Equals(own, "station-two", StringComparison.Ordinal))
            throw new Exception("Moje stacje nie dostały własnego wiersza po odmowie: " + own);
    }

    // ==================== APARATURA ====================

    /// <summary>
    /// HARNESS z odpowiedziami dla WSZYSTKICH TRZECH list: ulubione i playlisty
    /// musza miec po DWA wiersze, inaczej "drugi wiersz" nie istnieje. Wlasne
    /// stacje wpisujemy do konfiguracji, bo nie pochodza z chmury.
    /// </summary>
    private static RealHarness CreateAcceptanceHarness()
    {
        var h = RealHarness.Create();
        h.Handler.RouteOverride = (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get
                && path.EndsWith("/playlists", StringComparison.Ordinal))
            {
                return Json(PlaylistsAcceptanceBody);
            }

            return null;
        };

        var streams = h.Window.StateForTests.Sonos.OwnStreams;
        streams.Clear();
        streams.Add(new SonosOwnStreamSettings
        { Id = "station-one", Name = "Pierwsza stacja", StreamUrl = "https://a.example.invalid/1" });
        streams.Add(new SonosOwnStreamSettings
        { Id = "station-two", Name = "Druga stacja", StreamUrl = "https://a.example.invalid/2" });
        return h;
    }

    private static ListBox ListOf(Window dialog, SublistUnderTest sublist) => sublist.CategoryId switch
    {
        var id when id == SonosLibraryPresentation.OwnStreamsCategoryId =>
            (ListBox)dialog.FindName("StationsList")!,
        var id when id == SonosLibraryPresentation.FavoritesCategoryId =>
            (ListBox)dialog.FindName("FavoritesList")!,
        _ => (ListBox)dialog.FindName("PlaylistsList")!
    };

    /// <summary>
    /// ZAMKNIECIE OKNA PO ODCZYTANIU STANU. Odmowa wyjscia celowo zostawia
    /// podliste otwarta, wiec to pomiar musi zwinac zagniezdzony ShowDialog -
    /// inaczej faza Biblioteki nigdy nie wroci i caly przebieg stanie na limicie.
    /// Zamykamy PRZEZ kolejke, bo jestesmy w srodku obslugi zdarzenia tego okna.
    /// </summary>
    private static void CloseAfterCapture(Window dialog) =>
        dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { dialog.Close(); } catch (InvalidOperationException) { }
        }));

    private static void SelectSecondRow(Window dialog, SublistUnderTest sublist)
    {
        var list = ListOf(dialog, sublist);
        list.SelectedIndex = 1;
        list.UpdateLayout();
        LibraryFixture.Pump(TimeSpan.FromMilliseconds(40));
    }

    /// <summary>
    /// PRODUKCYJNA droga do WSKAZANEJ kategorii: polecenie Biblioteki, wybor
    /// wiersza po ETYKIECIE i Enter przez istniejacy punkt otwarcia. Kroki
    /// podlisty jada z timera, bo kategoria otwiera ZAGNIEZDZONY ShowDialog.
    ///
    /// ULUBIONE I PLAYLISTY sa ASYNCHRONICZNE: okno powstaje PO odczycie HTTP,
    /// wiec faza czeka na jego POJAWIENIE SIE, a nie na powrot z wywolania.
    /// </summary>
    private static void RunLibraryCategoryPhase(
        RealHarness h, SublistUnderTest sublist, Action<Window> steps)
    {
        var phase = 0;
        var done = false;
        Exception? inside = null;
        var deadline = DateTime.UtcNow.AddSeconds(60);
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (DateTime.UtcNow > deadline)
                    throw new Exception("Brak postępu w fazie " + phase + " dla " + sublist.CategoryId);

                if (phase == 0 && h.Window.OpenSonosLibraryWindowForTests is { IsVisible: true } library)
                {
                    phase = 1;
                    var index = library.CategoryNamesForTests
                        .Select((label, i) => (label, i))
                        .First(pair => pair.label.Contains(
                            sublist.LibraryLabelFragment, StringComparison.OrdinalIgnoreCase)).i;
                    library.SelectRowForTests(index);
                    // Oddaj tick: zagnieżdżony ShowDialog zablokowałby ten timer.
                    library.Dispatcher.BeginInvoke(new Action(library.OpenSelectedForTests));
                }
                else if (phase == 1
                    && sublist.OpenWindow(h.Window) is { IsVisible: true } dialog
                    && dialog.IsLoaded
                    && sublist.RowCount(dialog) > 0)
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
        try
        {
            h.ExecuteCommand(CommandIds.ViewLibrary);
            // ASYNCHRONICZNE kategorie: samo polecenie wraca natychmiast, wiec
            // pompujemy az faza sie domknie albo padnie.
            h.PumpUntil(() => done || inside is not null, TimeSpan.FromSeconds(60),
                "faza Biblioteki dla " + sublist.CategoryId + " nie ruszyła");
        }
        finally { timer.Stop(); }

        if (inside is not null) throw inside;
        if (!done) throw new Exception("Nie otwarto podlisty " + sublist.CategoryId + " drogą Biblioteki.");
    }
}
