using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// BIBLIOTEKA SONOSA (Ctrl+L), URUCHAMIANIE PLAYLISTY i WYBOR CELU (Ctrl+F5).
///
/// ZAKRES SWIADOMIE MALY i proporcjonalny do przyrostu: mierzymy RZECZYWISTA
/// NOWA DROGE, a nie wszystko wokol. Zero HTTP, zero tokenu, zero konta, zero
/// audio, zero prawdziwego Sonosa - syntetyczne zaplecze liczy, ile razy
/// wywolano ATRAPE, a nie ile zapytan poszlo w siec.
///
/// Kazdy przypadek jedzie PRAWDZIWA droga WPF: pokazane okno, zywe urzadzenie
/// klawiatury, tunelujacy PreviewKeyDown i prawdziwe klikniecie przycisku.
/// </summary>
internal static class SonosLibraryUiTests
{
    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                void Stage(string name) => Console.Error.WriteLine("ETAP: " + name);

                Stage("B1"); checks += MeasureLibraryShowsCategoriesAndNoSpeakers();
                Stage("B2"); checks += MeasureLibraryEnterOpensCategoryByIdAndSendsNothing();
                Stage("B3"); checks += MeasureLibraryEscapeLeavesWithoutAction();
                Stage("B4"); checks += MeasureLibraryClearsItsFieldBeforeCallback();
                Stage("P1"); checks += MeasurePlaylistEnterSendsExactSelectedId();
                Stage("P2"); checks += MeasurePlaylistDuplicateTitlesStayDistinct();
                Stage("P3"); checks += MeasurePlaylistOpeningAndMovingNeverPost();
                Stage("P4"); checks += MeasurePlaylistEmptyCollectionIsSuccess();
                Stage("P5"); checks += MeasurePlaylistWithoutTargetRefusesAndExplains();
                Stage("P6"); checks += MeasurePlaylistLateAnswerKeepsOneAttempt();
                Stage("C1"); checks += MeasureTargetChoiceSelectsExistingGroupByFullNames();
                Stage("C2"); checks += MeasureTargetMovementAndCancelChangeNothing();
                Stage("C3"); checks += MeasureTargetWithoutTopologyExplainsHonestly();
                Stage("KONIEC");
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
        if (!thread.Join(TimeSpan.FromSeconds(120)))
        {
            throw new Exception("Limit czasu pomiaru Biblioteki Sonos: patrz ostatni ETAP na stderr.");
        }

        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: Biblioteka Sonos (Ctrl+L), uruchamianie playlisty i wybór celu (Ctrl+F5) "
            + $"({checks} sprawdzeń)");
    }

    // ===== B1: Biblioteka pokazuje MATERIAL, a NIE glosniki =====

    private static int MeasureLibraryShowsCategoriesAndNoSpeakers()
    {
        using var ui = LibraryFixture.ShowLibrary(open: null);
        var checks = 0;

        if (ui.Window.CategoryCountForTests != 2)
        {
            throw new Exception(
                "Biblioteka ma pokazywać dokładnie dwie kategorie, pokazała: "
                + ui.Window.CategoryCountForTests);
        }

        checks++;
        var names = ui.Window.CategoryNamesForTests;
        if (!names.Any(name => name.Contains("Ulubione", StringComparison.OrdinalIgnoreCase))
            || !names.Any(name => name.Contains("Playlisty", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Biblioteka nie nazywa kategorii Ulubione i Playlisty: "
                + string.Join(" | ", names));
        }

        checks++;

        // GLOSNIKI I GRUPY TO NIE BIBLIOTEKA MUZYCZNA. Wczesniej Ctrl+L mowil
        // wprost o "wszystkich odczytanych głośnikach i grupach" jako zawartosci.
        // Pilnujemy, zeby ta pomylka nie wrocila do NAZW KATEGORII, czyli do
        // tego, co uzytkownik moze otworzyc jako material.
        var categorySurface = string.Join(" | ", names);
        foreach (var forbidden in new[] { "głośnik", "Głośnik", "grup", "Grup" })
        {
            if (categorySurface.Contains(forbidden, StringComparison.Ordinal))
            {
                throw new Exception(
                    "Biblioteka materiału ponownie podaje głośniki/grupy jako kategorię materiału: "
                    + categorySurface);
            }
        }

        checks++;

        // WSTEP ma odeslac po cel sterowania do PRAWDZIWEJ drogi (Ctrl+F5).
        // Wzmianka o glosnikach JEST tu poprawna - to wyjasnienie, czego w
        // Bibliotece nie ma, a nie oferta materialu.
        if (!ui.Window.IntroductionForTests.Contains("F5", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Wstęp Biblioteki nie odsyła po cel sterowania do Control F5: "
                + ui.Window.IntroductionForTests);
        }

        checks++;

        // FOKUS startowy na liscie: czytnik ma od razu co czytac.
        if (!ui.Window.ListHasFocusForTests)
        {
            throw new Exception("Biblioteka nie ustawia początkowego fokusu na liście kategorii.");
        }

        checks++;

        // BEZ callbacka NIE MA martwego przycisku udajacego dzialanie.
        if (ui.Window.OpenEnabledForTests)
        {
            throw new Exception("Biblioteka bez drogi otwarcia trzyma włączony przycisk otwarcia.");
        }

        return checks + 1;
    }

    // ===== B2: Enter otwiera kategorie po IDENTYFIKATORZE i NIC nie wysyla =====

    private static int MeasureLibraryEnterOpensCategoryByIdAndSendsNothing()
    {
        var opened = new List<string>();
        using var ui = LibraryFixture.ShowLibrary(row => opened.Add(row.CategoryId));
        var checks = 0;

        // SAM RUCH po liscie nie ma prawa niczego otwierac.
        ui.Window.SelectRowForTests(1);
        if (opened.Count != 0)
        {
            throw new Exception("Sam ruch po kategoriach otworzył kategorię: " + opened.Count);
        }

        checks++;

        ui.Window.PressEnter();
        if (opened.Count != 1)
        {
            throw new Exception("Enter na kategorii nie otworzył dokładnie jednej kategorii: "
                + opened.Count);
        }

        checks++;

        // IDENTYFIKATOR, nie polska nazwa: rozpoznanie nie moze zalezec od tekstu
        // widzianego przez uzytkownika.
        if (!string.Equals(opened[0], SonosLibraryPresentation.PlaylistsCategoryId, StringComparison.Ordinal))
        {
            throw new Exception("Enter na drugiej kategorii oddał nie ten identyfikator: " + opened[0]);
        }

        return checks + 1;
    }

    // ===== B3: wyjscie z Biblioteki NIC nie robi =====

    private static int MeasureLibraryEscapeLeavesWithoutAction()
    {
        var opened = 0;
        using var ui = LibraryFixture.ShowLibrary(_ => opened++);
        ui.Window.PressEscape();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        if (opened != 0)
        {
            throw new Exception("Escape w Bibliotece otworzył kategorię.");
        }

        if (ui.Window.IsVisible)
        {
            throw new Exception("Escape nie zamknął Biblioteki.");
        }

        return 2;
    }

    // ===== B4: callback WIDZI zamkniete okno jako ZAMKNIETE =====

    /// <summary>
    /// ZMIERZONA USTERKA: okno kategorii zamyka sie PRZED wywolaniem akcji, ale
    /// pole <c>_sonosLibraryWindow</c> w oknie glownym czysci dopiero
    /// <c>finally</c> po powrocie z <c>ShowDialog</c>. W czasie callbacka pole
    /// wskazuje wiec okno, ktorego JUZ NIE MA na ekranie - a to pole jest
    /// bramka skrotu Ctrl+L ("Biblioteka Sonos jest już otwarta" + Activate()
    /// na zamknietym oknie).
    ///
    /// Mierzymy to po stronie OKNA: w chwili wywolania callbacka okno nie moze
    /// byc juz zywym celem wlasciciela. Istniejace <c>IsLiveOwnerTarget</c> jest
    /// dokladnie tym orzeczeniem (<c>!_closed &amp;&amp; IsVisible</c>), wiec
    /// wlasciciel ma czym odroczyc stan bez zgadywania.
    /// </summary>
    private static int MeasureLibraryClearsItsFieldBeforeCallback()
    {
        var liveDuringCallback = true;
        var visibleDuringCallback = true;
        SonosLibraryWindow? captured = null;

        using var ui = LibraryFixture.ShowLibrary(_ =>
        {
            liveDuringCallback = captured!.IsLiveOwnerTarget;
            visibleDuringCallback = captured.IsVisible;
        });

        captured = ui.Window;
        ui.Window.SelectRowForTests(0);
        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        if (visibleDuringCallback)
        {
            throw new Exception(
                "Callback kategorii dostał okno Biblioteki wciąż WIDOCZNE - brama prezentacji "
                + "okna głównego odmówi otwarcia listy kategorii.");
        }

        if (liveDuringCallback)
        {
            throw new Exception(
                "Zamknięte okno Biblioteki nadal podaje się za żywy cel właściciela - Ctrl+L "
                + "powie 'już otwarta' i wywoła Activate() na oknie, którego nie ma.");
        }

        return 2;
    }

    // ===== P1: Enter na playliscie wysyla DOKLADNIE JEDEN wlasciwy POST =====

    private static int MeasurePlaylistEnterSendsExactSelectedId()
    {
        var backend = new PlaylistLoadFake();
        using var ui = LibraryFixture.ShowPlaylists(
            backend,
            [("PL-1", "Poranek"), ("PL-2", "Wieczór")],
            groupName: "Biuro");
        var checks = 0;

        ui.Window.SelectRowForTests(1);
        ui.Window.PressEnter();
        ui.AwaitPlay();

        if (backend.Loads.Count != 1)
        {
            throw new Exception("Enter na playliście nie wysłał dokładnie jednego zlecenia: "
                + backend.Loads.Count);
        }

        checks++;
        var post = backend.Loads[0];

        // IDENTYFIKATOR Z WIERSZA, nie tytul i nie identyfikator domu.
        if (!string.Equals(post.PlaylistId, "PL-2", StringComparison.Ordinal))
        {
            throw new Exception("Zlecenie poszło z nie tym identyfikatorem playlisty: " + post.PlaylistId);
        }

        checks++;

        // JAWNE Insert + playOnCompletion, bez playModes i bez drugiego Play.
        if (post.Action != SonosFavoriteQueueAction.Insert || !post.PlayOnCompletion)
        {
            throw new Exception("Zlecenie playlisty nie użyło jawnego Insert + playOnCompletion.");
        }

        checks++;

        // ZADNEGO drugiego polecenia odtwarzania "na wszelki wypadek".
        if (backend.Commands.Count != 0)
        {
            throw new Exception("Po zleceniu playlisty poszły dodatkowe polecenia: "
                + string.Join(",", backend.Commands));
        }

        checks++;

        // UCZCIWY komunikat: PRZYJECIE zlecenia, nie potwierdzenie odtwarzania.
        // Nie szukamy naiwnie slowa "gra" - zdanie "AMC nie potwierdza, że już
        // gra" jest WLASNIE tym, czego chcemy. Mierzymy, czy komunikat JAWNIE
        // zastrzega brak potwierdzenia.
        var status = ui.Window.StatusForTests;
        if (!status.Contains("nie potwierdza", StringComparison.OrdinalIgnoreCase)
            && !status.Contains("Zlecono", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                "Komunikat playlisty nie odróżnia przyjęcia zlecenia od potwierdzonego "
                + "odtwarzania: " + status);
        }

        checks++;

        // FOKUS zostaje na liscie, zeby czytnik nie czytal dialogu od nowa.
        if (!ui.Window.ListHasFocusForTests)
        {
            throw new Exception("Po próbie playlisty fokus zszedł z listy: "
                + ui.Window.FocusedElementNameForTests);
        }

        return checks + 1;
    }

    // ===== P2: ten sam TYTUL, rozne IDENTYFIKATORY =====

    private static int MeasurePlaylistDuplicateTitlesStayDistinct()
    {
        var backend = new PlaylistLoadFake();
        using var ui = LibraryFixture.ShowPlaylists(
            backend,
            [("PL-A", "Poranek"), ("PL-B", "Poranek")],
            groupName: "Biuro");

        ui.Window.SelectRowForTests(1);
        if (!string.Equals(ui.Window.HighlightedPlaylistIdForTests, "PL-B", StringComparison.Ordinal))
        {
            throw new Exception("Dwie playlisty o tym samym tytule nie są rozróżnione wewnętrznie.");
        }

        ui.Window.PressEnter();
        ui.AwaitPlay();

        if (backend.Loads.Count != 1
            || !string.Equals(backend.Loads[0].PlaylistId, "PL-B", StringComparison.Ordinal))
        {
            throw new Exception("Przy identycznych tytułach poszedł zły identyfikator: "
                + string.Join(",", backend.Loads.Select(load => load.PlaylistId)));
        }

        return 2;
    }

    // ===== P3: otwarcie, ruch i Tab NIE wysylaja POST =====

    private static int MeasurePlaylistOpeningAndMovingNeverPost()
    {
        var backend = new PlaylistLoadFake();
        using var ui = LibraryFixture.ShowPlaylists(
            backend,
            [("PL-1", "Poranek"), ("PL-2", "Wieczór")],
            groupName: "Biuro");

        ui.Window.SelectRowForTests(0);
        ui.Window.SelectRowForTests(1);
        ui.Window.PressTab();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        if (backend.Loads.Count != 0)
        {
            throw new Exception("Samo otwarcie/ruch/Tab wysłało zlecenie: " + backend.Loads.Count);
        }

        return 1;
    }

    // ===== P4: PUSTA kolekcja to POPRAWNY wynik =====

    private static int MeasurePlaylistEmptyCollectionIsSuccess()
    {
        var backend = new PlaylistLoadFake();
        using var ui = LibraryFixture.ShowPlaylists(backend, [], groupName: "Biuro");
        var checks = 0;

        if (ui.Window.RowCountForTests != 0)
        {
            throw new Exception("Pusta kolekcja playlist dostała wiersze.");
        }

        checks++;

        // PUSTO nie znaczy BLAD - i nie wolno podstawiac udawanej pozycji.
        var intro = ui.Window.IntroductionForTests;
        if (intro.Contains("błąd", StringComparison.OrdinalIgnoreCase)
            || intro.Contains("nie udało", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Pusta lista playlist przedstawiona jako błąd: " + intro);
        }

        checks++;

        if (ui.Window.PlayEnabledForTests)
        {
            throw new Exception("Pusta lista playlist ma włączony przycisk Odtwórz.");
        }

        checks++;

        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));
        if (backend.Loads.Count != 0)
        {
            throw new Exception("Enter na pustej liście playlist wysłał zlecenie.");
        }

        return checks + 1;
    }

    // ===== P5: BRAK CELU - odmowa z WYJASNIENIEM, zero POST =====

    private static int MeasurePlaylistWithoutTargetRefusesAndExplains()
    {
        var backend = new PlaylistLoadFake();
        using var ui = LibraryFixture.ShowPlaylists(
            backend,
            [("PL-1", "Poranek")],
            groupName: null);
        var checks = 0;

        if (ui.Window.PlayEnabledForTests)
        {
            throw new Exception("Bez celu sterowania przycisk Odtwórz jest włączony.");
        }

        checks++;

        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        if (backend.Loads.Count != 0)
        {
            throw new Exception("Bez celu sterowania poszło zlecenie playlisty.");
        }

        checks++;

        // INSTRUKCJA ODZYSKANIA musi nazywac PRAWDZIWA droge: Ctrl+F5. Dawna
        // instrukcja mowila "wybierz grupę w Bibliotece Enterem" - po tej zmianie
        // Biblioteka pokazuje MATERIAL, wiec tamta droga nie istnieje.
        var status = ui.Window.StatusForTests;
        if (!status.Contains("F5", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                "Odmowa bez celu nie nazywa prawdziwej drogi wyboru celu (Control F5): " + status);
        }

        checks++;

        if (status.Contains("Bibliotek", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                "Odmowa bez celu wciąż odsyła do Biblioteki, gdzie nie ma już głośników: " + status);
        }

        return checks + 1;
    }

    // ===== P6: JEDNA istotna SPOZNIONA odpowiedz =====

    private static int MeasurePlaylistLateAnswerKeepsOneAttempt()
    {
        var backend = new PlaylistLoadFake();
        var gate = new TaskCompletionSource();
        backend.LoadGate = gate.Task;
        using var ui = LibraryFixture.ShowPlaylists(
            backend,
            [("PL-1", "Poranek")],
            groupName: "Biuro");
        var checks = 0;

        ui.Window.SelectRowForTests(0);
        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        // PRAWDZIWE OCZEKIWANIE: dopiero tu komunikat "Czekaj" jest uczciwy.
        if (!ui.Window.StatusForTests.Contains("Wysyłam", StringComparison.OrdinalIgnoreCase)
            && !ui.Window.StatusForTests.Contains("Czekaj", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Trwająca próba playlisty nie mówi o oczekiwaniu: "
                + ui.Window.StatusForTests);
        }

        checks++;

        // DRUGI Enter w trakcie proby NIE dubluje POST.
        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));
        if (backend.Loads.Count != 1)
        {
            throw new Exception("Druga próba w trakcie oczekiwania wysłała kolejne zlecenie: "
                + backend.Loads.Count);
        }

        checks++;

        gate.SetResult();
        ui.AwaitPlay();

        if (backend.Loads.Count != 1)
        {
            throw new Exception("Po zwolnieniu bramki liczba zleceń wzrosła: " + backend.Loads.Count);
        }

        checks++;

        // FOKUS przezyl cale oczekiwanie.
        if (!ui.Window.ListHasFocusForTests)
        {
            throw new Exception("Po spóźnionej odpowiedzi fokus zszedł z listy: "
                + ui.Window.FocusedElementNameForTests);
        }

        return checks + 1;
    }

    // ===== C1: wybor ISTNIEJACEJ grupy po PELNYCH nazwach glosnikow =====

    private static int MeasureTargetChoiceSelectsExistingGroupByFullNames()
    {
        using var ui = LibraryFixture.ShowTarget(TwoGroupTopology(), currentGroupId: null);
        var checks = 0;

        if (ui.Window.RowCountForTests != 2)
        {
            throw new Exception("Wybór celu nie pokazał dwóch istniejących grup: "
                + ui.Window.RowCountForTests);
        }

        checks++;

        // PELNE NAZWY GLOSNIKOW, nie "Biuro +1" i nie identyfikatory.
        var labels = string.Join(" | ", ui.Window.RowLabelsForTests);
        if (!labels.Contains("Biuro", StringComparison.Ordinal)
            || !labels.Contains("Sypialnia", StringComparison.Ordinal))
        {
            throw new Exception("Etykiety grup nie wymieniają pełnych nazw głośników: " + labels);
        }

        checks++;

        if (labels.Contains("+1", StringComparison.Ordinal)
            || labels.Contains("GRUPA-", StringComparison.Ordinal))
        {
            throw new Exception("Etykiety grup skracają nazwy albo pokazują identyfikatory: " + labels);
        }

        checks++;

        ui.Window.SelectRowForTests(1);
        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        if (!ui.Window.Confirmed
            || !string.Equals(ui.Window.SelectedGroupId, "GRUPA-SYPIALNIA", StringComparison.Ordinal))
        {
            throw new Exception("Potwierdzenie nie oddało identyfikatora wybranej grupy: "
                + ui.Window.SelectedGroupId);
        }

        return checks + 1;
    }

    // ===== C2: RUCH i ANULOWANIE nie zmieniaja celu =====

    private static int MeasureTargetMovementAndCancelChangeNothing()
    {
        using var ui = LibraryFixture.ShowTarget(TwoGroupTopology(), currentGroupId: "GRUPA-BIURO");
        var checks = 0;

        // POCZATKOWE zaznaczenie po IDENTYFIKATORZE dotychczasowego celu.
        if (!string.Equals(ui.Window.HighlightedGroupIdForTests, "GRUPA-BIURO", StringComparison.Ordinal))
        {
            throw new Exception("Okno celu nie zaznaczyło dotychczasowego celu.");
        }

        checks++;

        ui.Window.SelectRowForTests(1);
        if (ui.Window.Confirmed || ui.Window.SelectedGroupId is not null)
        {
            throw new Exception("Sam ruch zaznaczeniem zmienił cel sterowania.");
        }

        checks++;

        ui.Window.PressEscape();
        ui.Pump(TimeSpan.FromMilliseconds(80));

        if (ui.Window.Confirmed || ui.Window.SelectedGroupId is not null)
        {
            throw new Exception("Escape w oknie celu zmienił cel sterowania.");
        }

        return checks + 1;
    }

    // ===== C3: BRAK TOPOLOGII wyjasniony UCZCIWIE =====

    private static int MeasureTargetWithoutTopologyExplainsHonestly()
    {
        using var ui = LibraryFixture.ShowTargetUnavailable(SonosTargetSelectionLabels.NotSignedIn);
        var checks = 0;

        if (ui.Window.RowCountForTests != 0)
        {
            throw new Exception("Okno celu bez topologii pokazało grupy.");
        }

        checks++;

        if (ui.Window.ConfirmEnabledForTests)
        {
            throw new Exception("Okno celu bez grup trzyma włączone potwierdzenie.");
        }

        checks++;

        // PRZYCZYNA, nie pusta lista udajaca dom bez glosnikow.
        if (!ui.Window.IntroductionForTests.Contains("zalogow", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Okno celu nie podaje przyczyny braku grup: "
                + ui.Window.IntroductionForTests);
        }

        checks++;

        ui.Window.PressEnter();
        ui.Pump(TimeSpan.FromMilliseconds(80));
        if (ui.Window.Confirmed)
        {
            throw new Exception("Enter bez grup potwierdził wybór celu.");
        }

        return checks + 1;
    }

    private static SonosHouseholdTopology TwoGroupTopology() => new(
        [
            new SonosGroup("GRUPA-BIURO", "Biuro", "P-BIURO", ["P-BIURO", "P-KUCHNIA"], SonosPlaybackState.Idle),
            new SonosGroup("GRUPA-SYPIALNIA", "Sypialnia", "P-SYP", ["P-SYP"], SonosPlaybackState.Idle)
        ],
        [
            new SonosPlayer("P-BIURO", "Biuro", null, null, null),
            new SonosPlayer("P-KUCHNIA", "Kuchnia", null, null, null),
            new SonosPlayer("P-SYP", "Sypialnia", null, null, null)
        ],
        false);
}
