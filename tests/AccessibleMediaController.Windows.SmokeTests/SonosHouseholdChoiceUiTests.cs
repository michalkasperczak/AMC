using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;


/// <summary>
/// B2c2: DOSTEPNY WYBOR DOMU Sonos mierzony RZECZYWISTA droga uzytkownika.
///
/// Kazdy pomiar idzie przez prawdziwe <c>ExecuteCommand</c> i prawdziwe okno
/// <see cref="SonosHouseholdSelectionWindow"/> (pokazane przez Show z pompa
/// petli, nie przez atrape), a zaplecze jest SYNTETYCZNE: zero HTTP, zero
/// tokenu, zero prawdziwego konta.
///
/// UCZCIWA GRANICA: suite pokazuje PRAWDZIWE okna WPF, wiec nie jest pomiarem
/// "bez GUI". Komunikaty zbieramy z AnnouncementSinkForTests - to jest tresc
/// ODDANA czytnikowi, a NIE dowod, ze NVDA cokolwiek wypowiedzial.
/// </summary>
internal static class SonosHouseholdChoiceUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private const string ChooseCommandId = CommandIds.ChooseSonosHousehold;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        // PRAWDZIWE okna WPF wymagaja STA; pomiar dostaje wlasny watek.
        var thread = new Thread(() =>
        {
            try
            {
                // ETAP na stderr: gdy pomiar zawisnie, wiadomo GDZIE, zamiast
                // zgadywac po pustym logu.
                void Stage(string name) => Console.Error.WriteLine("ETAP: " + name);
                Stage("B1"); checks += MeasureChoiceReadsFreshHouseholdsAndPresentsCurrent();
                Stage("B2"); checks += MeasureCancelMutatesNothing();
                Stage("B3"); checks += MeasureConfirmingOtherHouseholdSwitchesTargetSafely();
                Stage("B4"); checks += MeasureSameHouseholdKeepsTarget();
                Stage("B5"); checks += MeasureStaleHouseholdReadDoesNotShowWindow();
                Stage("B9"); checks += MeasureAbandonedChoiceReleasesGateForNewOne();
                Stage("B10"); checks += MeasureSuccessfulSwitchLeavesGateOpen();
                Stage("B11"); checks += MeasureLateChoiceNeverStealsForeignFocus();
                Stage("B6"); checks += MeasureGroupsFailureKeepsConsciousChoiceHonest();
                Stage("B7"); checks += MeasureChoiceSurvivesSaveAndSecondWindow();
                Stage("KONIEC-STA");
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
        // TWARDY limit calego pomiaru: zawieszony modal nie ma prawa zablokowac
        // runnera na zawsze. Watek jest tlem, wiec proces i tak sie domknie.
        if (!thread.Join(TimeSpan.FromSeconds(100)))
        {
            throw new Exception("Limit czasu pomiaru wyboru domu: patrz ostatni ETAP na stderr.");
        }

        if (failure is not null) throw failure;

        checks += MeasureCommandIsReachableInMenuAndPalette();

        Console.WriteLine(
            "OK: wybór domu Sonos - świeża lista i bieżący dom po ID, anulowanie bez mutacji, "
            + "bezpieczne przełączenie celu bez POST, ten sam dom bez restartu, spóźniony odczyt "
            + "bez okna, porzucony wybór nie zatrzaskuje bramki, udane przełączenie zostawia ją "
            + "otwartą, spóźniony modal nie kradnie fokusu obcemu oknu, błąd grup uczciwy, "
            + $"wybór przeżywa zapis i drugie okno ({checks} sprawdzeń)");
    }

    // ===== B1: swieza lista domow, zaznaczony BIEZACY dom po IDENTYFIKATORZE =====

    private static int MeasureChoiceReadsFreshHouseholdsAndPresentsCurrent()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"), ("DOM-3", "Garaż"));
        harness.EnterSonosSession();

        // Wybor poczatkowy: sesja nie wybrala domu za uzytkownika (3 domy).
        if (harness.Window.SonosSelectedHouseholdId is not null)
        {
            throw new Exception("Sesja wybrała dom za użytkownika przy wielu domach.");
        }

        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-2";
        var readsBefore = harness.Backend.HouseholdReads;

        // Chmura ZMIENILA kolejnosc i nazwy: wybor ma trzymac sie IDENTYFIKATORA.
        harness.Backend.SetHouseholds(("DOM-3", "Garaż"), ("DOM-2", "Piętro po remoncie"), ("DOM-1", "Parter"));

        var window = harness.RunChoice();
        if (harness.Backend.HouseholdReads <= readsBefore)
        {
            throw new Exception("Wybór domu nie przeczytał domów z zaplecza; wziął cache.");
        }
        if (window is null) throw new Exception("Polecenie nie pokazało okna wyboru domu.");
        if (window.RowCountForTests != 3)
        {
            throw new Exception($"Okno pokazuje {window.RowCountForTests} domów zamiast 3.");
        }
        if (window.HighlightedHouseholdIdForTests != "DOM-2")
        {
            throw new Exception(
                "Zaznaczony jest dom " + (window.HighlightedHouseholdIdForTests ?? "żaden")
                + " zamiast bieżącego DOM-2; wybór poszedł po indeksie, nie po identyfikatorze.");
        }
        var labels = window.RowLabelsForTests;
        if (!labels.Any(label => label.Contains("Piętro po remoncie", StringComparison.Ordinal)))
        {
            throw new Exception("Etykiety nie pochodzą ze świeżej listy zaplecza.");
        }
        if (labels.Any(label => label.Contains("SonosHousehold", StringComparison.Ordinal)))
        {
            throw new Exception("Etykieta pokazuje surowe ToString obiektu.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Samo otwarcie wyboru domu wysłało POST do Sonosa.");
        }
        if (!harness.Announcements.Any(text => text.Contains("Czytam domy", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Brak jawnego komunikatu ładowania przed oknem.");
        }
        return 8;
    }

    // ===== B2: ANULOWANIE nie mutuje NICZEGO =====

    private static int MeasureCancelMutatesNothing()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup domu A przed anulowaniem");

        var householdBefore = harness.Window.SonosSelectedHouseholdId;
        var groupBefore = harness.Window.SonosSelectedGroupId;
        var rowsBefore = harness.RowLabels();
        var metadataBefore = harness.Window.SonosMetadataForTests;
        var commandsBefore = harness.Backend.Commands.Count;
        var groupReadsBefore = harness.Backend.GroupReads;

        // RUCH zaznaczeniem na INNY dom, a potem ANULUJ.
        var window = harness.RunChoice(action: dialog =>
        {
            dialog.SelectRowForTests(1);
            if (dialog.HighlightedHouseholdIdForTests != "DOM-2")
            {
                throw new Exception("Ruch zaznaczeniem nie przestawił podświetlenia.");
            }
            // Sam ruch NIE MOZE nic zapisac.
            if (harness.Window.SonosSelectedHouseholdId != householdBefore)
            {
                throw new Exception("Ruch zaznaczeniem zmienił wybrany dom sesji.");
            }
            harness.PressEscape(dialog);
        });

        if (window is null) throw new Exception("Okno wyboru się nie pokazało.");
        if (window.Confirmed) throw new Exception("Escape potwierdził wybór.");
        if (window.SelectedHouseholdId is not null)
        {
            throw new Exception("Anulowane okno oddało identyfikator domu.");
        }
        if (harness.Window.SonosSelectedHouseholdId != householdBefore)
        {
            throw new Exception("Anulowanie zmieniło wybrany dom.");
        }
        if (harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception("Anulowanie zmieniło wybraną grupę.");
        }
        if (!harness.RowLabels().SequenceEqual(rowsBefore, StringComparer.Ordinal))
        {
            throw new Exception("Anulowanie zmieniło rzeczywiste wiersze listy.");
        }
        if (!ReferenceEquals(harness.Window.SonosMetadataForTests, metadataBefore))
        {
            throw new Exception("Anulowanie ruszyło metadane.");
        }
        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Anulowanie wysłało POST do Sonosa.");
        }
        if (harness.Backend.GroupReads != groupReadsBefore)
        {
            throw new Exception("Anulowanie przeczytało grupy, czyli jednak przełączyło cel.");
        }
        return 9;
    }

    // ===== B3: POTWIERDZENIE INNEGO domu przelacza cel BEZPIECZNIE =====

    private static int MeasureConfirmingOtherHouseholdSwitchesTargetSafely()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup domu A przed przełączeniem");

        // AKTYWNA grupa domu A z danymi odtwarzania w modelu.
        harness.SelectAndActivateFirstGroup();
        if (harness.Window.SonosSelectedGroupId is null)
        {
            throw new Exception("Grupa domu A nie stała się celem przed przełączeniem.");
        }

        // Dom B ma INNE grupy.
        harness.Backend.SetGroupsForHousehold("DOM-2", ("GRUPA-B1", "Sypialnia"), ("GRUPA-B2", "Łazienka"));
        var commandsBefore = harness.Backend.Commands.Count;

        var window = harness.RunChoice(action: dialog =>
        {
            dialog.SelectRowForTests(1);
            dialog.ConfirmForTests();
        });

        if (window is null || !window.Confirmed) throw new Exception("Potwierdzenie domu B nie doszło.");
        harness.PumpUntil(
            () => harness.RowLabels().Any(label => label.Contains("Sypialnia", StringComparison.Ordinal)),
            "lista nie pokazała grup domu B");

        if (harness.Window.SonosSelectedHouseholdId != "DOM-2")
        {
            throw new Exception("Wybrany dom to nie DOM-2.");
        }
        if (harness.Window.SonosSelectedGroupId is not null)
        {
            throw new Exception("Po przełączeniu domu cicho wybrano grupę; użytkownik ma wybrać sam.");
        }
        if (harness.Window.SonosPlaybackForTests is not null
            || harness.Window.SonosMetadataForTests is not null
            || harness.Window.SonosVolumeForTests is not null)
        {
            throw new Exception("Dane starego domu przetrwały przełączenie celu.");
        }
        var labels = harness.RowLabels();
        if (labels.Any(label => label.Contains("Salon", StringComparison.Ordinal)))
        {
            throw new Exception("Lista pokazuje grupy domu A pod nowym domem.");
        }
        if (labels.Length != 2) throw new Exception($"Lista ma {labels.Length} wierszy zamiast 2 grup domu B.");
        if (harness.PlayerViewActive) throw new Exception("Przełączenie domu samo otworzyło odtwarzacz.");
        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Przełączenie domu wysłało POST do Sonosa; muzyka miała zostać nietknięta.");
        }
        return 8;
    }

    // ===== B4: TEN SAM dom nie restartuje celu =====

    private static int MeasureSameHouseholdKeepsTarget()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed potwierdzeniem tego samego domu");
        harness.SelectAndActivateFirstGroup();

        var groupBefore = harness.Window.SonosSelectedGroupId
            ?? throw new Exception("Brak aktywnej grupy przed pomiarem.");
        var metadataBefore = harness.Window.SonosMetadataForTests;
        var playbackBefore = harness.Window.SonosPlaybackForTests;
        var groupReadsBefore = harness.Backend.GroupReads;
        var commandsBefore = harness.Backend.Commands.Count;

        var window = harness.RunChoice(action: dialog =>
        {
            if (dialog.HighlightedHouseholdIdForTests != "DOM-1")
            {
                throw new Exception("Nie zaznaczono bieżącego domu.");
            }
            dialog.ConfirmForTests();
        });

        if (window is null || !window.Confirmed) throw new Exception("Potwierdzenie tego samego domu nie doszło.");
        if (harness.Window.SonosSelectedHouseholdId != "DOM-1")
        {
            throw new Exception("Potwierdzenie tego samego domu zmieniło dom.");
        }
        if (harness.Window.SonosSelectedGroupId != groupBefore)
        {
            throw new Exception("Potwierdzenie tego samego domu zresetowało aktywną grupę.");
        }
        if (!ReferenceEquals(harness.Window.SonosMetadataForTests, metadataBefore)
            || !ReferenceEquals(harness.Window.SonosPlaybackForTests, playbackBefore))
        {
            throw new Exception("Potwierdzenie tego samego domu wyrzuciło poprawny model danych.");
        }
        if (harness.Backend.GroupReads != groupReadsBefore)
        {
            throw new Exception("Potwierdzenie tego samego domu zrestartowało odczyt grup.");
        }
        if (harness.Backend.Commands.Count != commandsBefore)
        {
            throw new Exception("Potwierdzenie tego samego domu wysłało POST.");
        }
        return 7;
    }

    // ===== B5: SPOZNIONY odczyt domow NIE pokazuje okna =====

    private static int MeasureStaleHouseholdReadDoesNotShowWindow()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed spóźnionym odczytem");

        var release = new TaskCompletionSource();
        harness.Backend.HouseholdGate = release.Task;
        var windowsBefore = harness.Window.SonosHouseholdWindowsCreatedForTests;
        harness.Window.PresentSonosHouseholdOverrideForTests = _ =>
            throw new Exception("Spóźniony odczyt pokazał okno wyboru domu.");

        harness.ExecuteCommand(ChooseCommandId);
        harness.PumpUntil(() => harness.Backend.HouseholdReads > 0, "polecenie nie zaczęło odczytu domów");

        // WYJSCIE do obcej sesji w trakcie odczytu.
        harness.ExecuteCommand(CommandIds.SessionSlot(3));
        // ASERCJA PRZY ODCZYCIE NADAL W LOCIE: bez tego test byl niefalsyfikowalny,
        // bo zwolnienie bariery pozwalalo A samemu oddac bramke w finally.
        if (harness.Window.SonosHouseholdChoiceInFlightForTests)
        {
            throw new Exception(
                "Bramka wyboru domu jest zatrzaśnięta, gdy porzucony odczyt NADAL trwa.");
        }
        release.SetResult();
        harness.Backend.HouseholdGate = null;
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));

        if (harness.Window.SonosHouseholdWindowsCreatedForTests != windowsBefore)
        {
            throw new Exception("Spóźniony przelot zbudował okno wyboru mimo wyjścia z sesji.");
        }
        if (harness.Window.SonosSelectedHouseholdId != "DOM-1")
        {
            throw new Exception("Spóźniony przelot zmienił wybrany dom.");
        }
        if (harness.Window.SonosHouseholdChoiceInFlightForTests)
        {
            throw new Exception("Bramka wyboru domu została zatrzaśnięta przez porzucony przelot.");
        }

        // POWROT do sesji: nowe polecenie MA dzialac, bramka nie jest martwa.
        harness.Window.PresentSonosHouseholdOverrideForTests = null;
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "powrót do sesji nie odbudował listy");
        var reopened = harness.RunChoice(action: dialog => harness.PressEscape(dialog));
        if (reopened is null)
        {
            throw new Exception("Po powrocie do sesji polecenie wyboru domu jest martwe.");
        }
        return 6;
    }

    // ===== B9: PORZUCONY wybor nie zatrzaskuje bramki (L1) =====

    /// <summary>
    /// A wisi na odczycie domow, uzytkownik wychodzi z sesji i wraca. B MUSI
    /// zaczac RZECZYWISCIE NOWY odczyt, zanim A sie skonczy - a potem spoznione
    /// finally A nie ma prawa zwolnic bramki trwajacego B.
    /// </summary>
    private static int MeasureAbandonedChoiceReleasesGateForNewOne()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed porzuconym wyborem");

        var holdA = harness.HoldHouseholdRead();
        var first = harness.StartChoice();
        if (first.IsCompleted) throw new Exception("Przelot A nie został wstrzymany na odczycie domów.");
        var readsAfterA = harness.Backend.HouseholdReads;

        // WYJSCIE z sesji: porzucenie A. Okna wyboru nie ma, bo A wisi w awaicie.
        harness.ExecuteCommand(CommandIds.SessionSlot(3));
        if (harness.Window.SonosHouseholdChoiceInFlightForTests)
        {
            throw new Exception(
                "Wyjście z sesji nie zwolniło bramki wyboru domu: porzucony przelot A nadal ją trzyma.");
        }

        // POWROT do sesji i DRUGIE polecenie - A NADAL wisi.
        harness.Backend.HouseholdGate = null;
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "powrót do sesji nie odbudował listy");
        var readsBeforeB = harness.Backend.HouseholdReads;
        var holdB = harness.HoldHouseholdRead();
        var announcementsBeforeB = harness.Announcements.Count;
        var second = harness.StartChoice();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

        if (first.IsCompleted)
        {
            throw new Exception("Kontrolka pomiaru: przelot A skończył się przed startem B.");
        }
        if (harness.Backend.HouseholdReads != readsBeforeB + 1)
        {
            throw new Exception(
                $"B nie zrobił NOWEGO odczytu domów (odczyty {readsBeforeB} -> {harness.Backend.HouseholdReads}); "
                + "porzucone A zablokowało polecenie.");
        }
        if (harness.Announcements.Skip(announcementsBeforeB)
            .Any(text => text.Contains("już trwa", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("B usłyszał 'już trwa' zamiast zacząć nowy odczyt.");
        }
        if (second.IsCompleted) throw new Exception("Kontrolka pomiaru: B nie czeka na transport.");
        if (!harness.Window.SonosHouseholdChoiceInFlightForTests)
        {
            throw new Exception("Trwające B nie trzyma bramki wyboru domu.");
        }
        _ = readsAfterA;

        // SPOZNIONE zakonczenie A: nie wolno mu zwolnic bramki trwajacego B.
        holdA.TrySetResult();
        harness.Pump(first);
        if (!harness.Window.SonosHouseholdChoiceInFlightForTests)
        {
            throw new Exception("Spóźnione finally porzuconego A zwolniło bramkę trwającego B.");
        }

        // B robimy nieaktualnym PRZED zwolnieniem, zeby zadne okno sie nie otwarlo.
        harness.ExecuteCommand(CommandIds.SessionSlot(3));
        harness.Backend.HouseholdGate = null;
        holdB.TrySetResult();
        harness.Pump(second);
        if (harness.Window.SonosHouseholdChoiceInFlightForTests)
        {
            throw new Exception("Po zakończeniu B bramka wyboru domu została zatrzaśnięta.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Porzucony wybór domu wysłał POST do Sonosa.");
        }
        return 8;
    }

    // ===== B10: UDANE przelaczenie domu nie zostawia zajetosci (L1, pulapka Switch) =====

    /// <summary>
    /// <c>SwitchSonosHouseholdAsync</c> sam wola <c>CancelSonosPendingWork</c>,
    /// ktory PODNOSI bilet bramki. Wlasciwe finally musi zwolnic rowniez TE nowa
    /// bramke - inaczej udany wybor zatrzaskuje polecenie na zawsze. Mierzone dla
    /// sukcesu grup, dla BLEDU grup i dla kazdego z nich z prawdziwym awaitem.
    /// </summary>
    private static int MeasureSuccessfulSwitchLeavesGateOpen()
    {
        var checks = 0;
        foreach (var failGroups in new bool?[] { false, true })
        {
            using var harness = Harness.Create();
            harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
            harness.Backend.SetGroupsForHousehold("DOM-2", ("GRUPA-B1", "Sypialnia"));
            harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
            harness.EnterSonosSession();
            harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup domu A przed przełączeniem");
            // Guard pokazania jest PRODUKCYJNY: dialog wyboru domu otwiera sie tu
            // za nim, wiec okno glowne musi byc NAPRAWDE pokazane i aktywne.
            harness.ShowOwnWindow();
            harness.ForegroundOwn(harness.Window);
            harness.PumpUntil(() => harness.Window.IsActive, "własne okno główne nie stało się aktywne");

            if (failGroups == true) harness.Backend.FailGroupsWith = SonosDeviceReadStatus.ServiceError;
            // GRUPY nowego domu tez idą przez PRAWDZIWY await.
            var groupsGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Backend.GroupsGate = groupsGate.Task;
            Task? operation = null;
            try
            {
                harness.Window.PresentSonosHouseholdOverrideForTests = dialog =>
                {
                    dialog.ShowInTaskbar = false;
                    dialog.Show();
                    harness.PumpUntil(
                        () => dialog.IsLoaded && PresentationSource.FromVisual(dialog) is not null,
                        "okno wyboru domu się nie pokazało");
                    dialog.SelectRowForTests(1);
                    dialog.ConfirmForTests();
                    harness.PumpUntil(() => !dialog.IsVisible, "okno wyboru domu się nie zamknęło");
                };
                operation = harness.StartChoice();
                harness.PumpUntil(() => harness.Backend.GroupReads > 0,
                    "przełączenie domu nie doszło do odczytu grup");
                if (operation.IsCompleted)
                {
                    throw new Exception("Odczyt grup nowego domu nie jest awaitowany.");
                }
            }
            finally
            {
                harness.Window.PresentSonosHouseholdOverrideForTests = null;
                harness.Backend.GroupsGate = null;
                groupsGate.TrySetResult();
            }

            harness.Pump(operation!);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

            if (harness.Window.SonosSelectedHouseholdId != "DOM-2")
            {
                throw new Exception("Świadomy wybór domu B nie przetrwał (błąd grup: " + failGroups + ").");
            }
            if (harness.Window.SonosHouseholdChoiceInFlightForTests)
            {
                throw new Exception(
                    "Po zakończonym przełączeniu domu bramka wyboru została zajęta (błąd grup: "
                    + failGroups + "); Cancel w Switch podniósł nową bramkę, a finally jej nie zwolniło.");
            }

            // PONOWNY wybor MUSI byc mozliwy: bramka nie jest martwa.
            harness.Backend.FailGroupsWith = null;
            var announcementsBefore = harness.Announcements.Count;
            var again = harness.RunChoice(action: dialog => harness.PressEscape(dialog));
            if (again is null)
            {
                throw new Exception("Po przełączeniu domu polecenie wyboru jest martwe (błąd grup: " + failGroups + ").");
            }
            if (harness.Announcements.Skip(announcementsBefore)
                .Any(text => text.Contains("już trwa", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("Ponowny wybór usłyszał 'już trwa' po zakończonym przełączeniu.");
            }
            if (harness.Backend.Commands.Count != 0)
            {
                throw new Exception("Przełączenie domu wysłało POST do Sonosa.");
            }
            checks += 5;
        }

        return checks;
    }

    // ===== B11: SPOZNIONY modal NIE kradnie fokusu obcemu oknu (L2) =====

    /// <summary>
    /// PRODUKCYJNA droga pokazania: zadnego override, prawdziwe ShowDialog.
    ///
    /// (a) KONTROLA DODATNIA: aktywny, pokazany wlasciciel RZECZYWISCIE dostaje
    ///     jeden modal z poprawnym Owner i natywnym pierwszym planem.
    /// (b) Uzytkownik przeszedl do INNEGO WLASNEGO okna podczas odczytu: modal NIE
    ///     powstaje, licznik Created nie rosnie, fokus zostaje tam, gdzie byl.
    /// (c) INNY RZECZYWISTY modal AMC (wlasne OwnedWindow) jest otwarty w chwili
    ///     spoznionej odpowiedzi: wybor domu go NIE przykrywa.
    /// (d) Niewidoczny/nieaktywny wlasciciel: odmowa, bez okna bez wlasciciela.
    /// </summary>
    private static int MeasureLateChoiceNeverStealsForeignFocus()
    {
        var checks = 0;

        // (a) KONTROLA DODATNIA - bez niej odmowa nie jest dowodem niczego.
        using (var harness = Harness.Create())
        {
            harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
            harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
            harness.ShowOwnWindow();
            harness.ForegroundOwn(harness.Window);
            harness.EnterSonosSession();
            harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed kontrolą dodatnią");

            var seen = harness.RunGuardedChoiceWithHeldRead(whileHeld: null);
            if (seen.Seen != 1)
            {
                throw new Exception(
                    $"Aktywny pokazany właściciel zobaczył {seen.Seen} modali wyboru domu zamiast 1.");
            }
            if (seen.OwnerIsMainWindow != true)
            {
                throw new Exception("Prawdziwy modal wyboru domu nie miał okna głównego jako właściciela.");
            }
            if (seen.NativeForeground != true && seen.Active != true)
            {
                throw new Exception(
                    "Modal wyboru domu nie stał się ani aktywny, ani natywnie pierwszoplanowy.");
            }
            if (seen.CreatedDelta != 1)
            {
                throw new Exception($"Licznik utworzonych okien wzrósł o {seen.CreatedDelta} zamiast o 1.");
            }
            checks += 4;
        }

        // (b) INNE WLASNE okno przejmuje pierwszy plan PODCZAS odczytu.
        using (var harness = Harness.Create())
        {
            harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
            harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
            harness.ShowOwnWindow();
            harness.ForegroundOwn(harness.Window);
            harness.EnterSonosSession();
            harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed odejściem do innego okna");

            Window? elsewhere = null;
            try
            {
                var seen = harness.RunGuardedChoiceWithHeldRead(whileHeld: () =>
                {
                    // WLASNE, niepowiazane okno pomiaru: zero cudzych procesow.
                    elsewhere = new Window
                    {
                        Title = "AMC pomiar: inne własne okno",
                        Width = 380,
                        Height = 200,
                        ShowInTaskbar = false,
                        Content = new TextBox { Text = "Kontrola fokusu: dane syntetyczne" }
                    };
                    elsewhere.Show();
                    harness.ForegroundOwn(elsewhere);
                    harness.PumpUntil(() => elsewhere.IsActive && !harness.Window.IsActive,
                        "kontrola fokusu nie stała się aktywna");
                    if (!harness.IsNativeForeground(elsewhere))
                    {
                        throw new Exception("Kontrola pomiaru: inne okno nie jest natywnie pierwszoplanowe.");
                    }
                });

                if (seen.Seen != 0)
                {
                    throw new Exception(
                        "Spóźniony odczyt domów pokazał modal nad oknem, do którego użytkownik przeszedł.");
                }
                if (seen.CreatedDelta != 0)
                {
                    throw new Exception(
                        $"Odmowa pokazania podniosła licznik utworzonych okien o {seen.CreatedDelta}.");
                }
                if (elsewhere is null || !elsewhere.IsActive)
                {
                    throw new Exception("Spóźniony wybór domu odebrał fokus innemu oknu użytkownika.");
                }
                if (harness.Window.SonosSelectedHouseholdId != "DOM-1")
                {
                    throw new Exception("Odrzucony spóźniony wybór jednak zmienił dom.");
                }

                // POWROT do aplikacji NIE odtwarza okna sam z siebie.
                elsewhere.Close();
                harness.PumpQuietly(TimeSpan.FromMilliseconds(80));
                harness.ForegroundOwn(harness.Window);
                harness.PumpQuietly(TimeSpan.FromMilliseconds(250));
                if (harness.Window.SonosHouseholdWindowsCreatedForTests != 0
                    || harness.Window.OpenSonosHouseholdWindowForTests is not null)
                {
                    throw new Exception("Powrót do aplikacji sam odtworzył porzucone okno wyboru domu.");
                }
                if (harness.Window.SonosHouseholdChoiceInFlightForTests)
                {
                    throw new Exception("Odmowa pokazania zatrzasnęła bramkę wyboru domu.");
                }
                checks += 6;
            }
            finally
            {
                elsewhere?.Close();
            }
        }

        // (c) INNY RZECZYWISTY modal AMC otwarty w chwili spoznionej odpowiedzi.
        using (var harness = Harness.Create())
        {
            harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
            harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
            harness.ShowOwnWindow();
            harness.ForegroundOwn(harness.Window);
            harness.EnterSonosSession();
            harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed innym modalem AMC");

            Window? other = null;
            try
            {
                var seen = harness.RunGuardedChoiceWithHeldRead(whileHeld: () =>
                {
                    // PRAWDZIWE wlasne OwnedWindow okna glownego - dokladnie to,
                    // co widzi istniejacy wzorzec OwnedWindows w MainWindow.Nvda.cs.
                    other = new Window
                    {
                        Title = "AMC pomiar: inny własny modal",
                        Width = 340,
                        Height = 180,
                        ShowInTaskbar = false,
                        Owner = harness.Window,
                        Content = new TextBox { Text = "Inny modal: dane syntetyczne" }
                    };
                    other.Show();
                    harness.PumpUntil(() => other.IsVisible, "inny własny modal się nie pokazał");
                    harness.ForegroundOwn(other);
                });

                if (seen.Seen != 0)
                {
                    throw new Exception("Spóźniony wybór domu przykrył inny rzeczywisty modal AMC.");
                }
                if (seen.CreatedDelta != 0)
                {
                    throw new Exception("Odmowa nad innym modalem AMC podniosła licznik utworzonych okien.");
                }
                if (other is null || !other.IsVisible)
                {
                    throw new Exception("Spóźniony wybór domu zamknął inny modal AMC.");
                }
                checks += 3;
            }
            finally
            {
                other?.Close();
            }
        }

        // (d) NIEPOKAZANY wlasciciel: odmowa, a nie okno bez wlasciciela.
        using (var harness = Harness.Create())
        {
            harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
            harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
            harness.EnterSonosSession();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
            if (harness.Window.IsVisible)
            {
                throw new Exception("Kontrola pomiaru: okno główne miało zostać niepokazane.");
            }

            var seen = harness.RunGuardedChoiceWithHeldRead(whileHeld: null, showOwner: false);
            if (seen.Seen != 0 || seen.CreatedDelta != 0)
            {
                throw new Exception(
                    "Niepokazane okno główne i tak pokazało modal wyboru domu bez właściciela.");
            }
            if (harness.Window.SonosHouseholdChoiceInFlightForTests)
            {
                throw new Exception("Odmowa przy niepokazanym oknie zatrzasnęła bramkę wyboru domu.");
            }
            if (!harness.Announcements.Any(text =>
                text.Contains("nie został otwarty", StringComparison.OrdinalIgnoreCase)
                && text.Contains("nie jest aktywne", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception(
                    "Odmowa pokazania nie powiedziała użytkownikowi NIC: "
                    + string.Join(" | ", harness.Announcements));
            }
            checks += 3;
        }

        return checks;
    }

    // ===== B6: BLAD grup domu B - wybor swiadomy, dane uczciwie puste =====

    private static int MeasureGroupsFailureKeepsConsciousChoiceHonest()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup domu A przed błędem domu B");

        harness.Backend.FailGroupsWith = SonosDeviceReadStatus.ServiceError;
        var announcementsBefore = harness.Announcements.Count;
        var window = harness.RunChoice(action: dialog =>
        {
            dialog.SelectRowForTests(1);
            dialog.ConfirmForTests();
        });

        if (window is null || !window.Confirmed) throw new Exception("Potwierdzenie domu B nie doszło.");
        harness.PumpUntil(() => harness.MediaList.Items.Count == 0, "lista nie opróżniła się po błędzie grup domu B");

        if (harness.Window.SonosSelectedHouseholdId != "DOM-2")
        {
            throw new Exception("Błąd grup unieważnił ŚWIADOMY wybór domu B.");
        }
        if (harness.RowLabels().Any(label => label.Contains("Salon", StringComparison.Ordinal)))
        {
            throw new Exception("Po błędzie grup domu B lista pokazuje stare grupy domu A jako B.");
        }
        var added = harness.Announcements.Skip(announcementsBefore).ToArray();
        if (!added.Any(text => text.Contains("nie udało się odczytać jego grup", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Brak uczciwego komunikatu o nieodczytanych grupach: " + string.Join(" | ", added));
        }
        if (added.Any(text => text.Contains("brak podłączonego konta", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Błąd odczytu grup nazwany brakiem konta.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Błąd grup wywołał POST do Sonosa.");
        }
        return 6;
    }

    // ===== B7: TRWALOSC - realny zapis i DRUGIE okno z tego samego pliku =====

    private static int MeasureChoiceSurvivesSaveAndSecondWindow()
    {
        // BEZ using: pierwsze okno musi sie ZAMKNAC, zanim drugie tknie ten sam
        // plik. Dwa MainWindow trzymajace jeden settings.json zderzaja sie na
        // zapisie, a to bylby artefakt pomiaru, nie wada produktu.
        var harness = Harness.Create();
        // Plik musi PRZEZYC pierwsze okno, bo drugie okno go czyta.
        harness.KeepDirectoryOnDispose = true;
        var firstDisposed = false;
        string? secondDirectory = null;
        try
        {
        harness.Backend.SetHouseholds(("DOM-1", "Parter"), ("DOM-2", "Piętro"));
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.EnterSonosSession();
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak grup przed pomiarem trwałości");

        // POKAZANIE okna przed snapshotem: samo Show zapisuje geometrie do pliku,
        // wiec gdyby padlo pozniej, roznica byla by artefaktem fixture, nie zapisem
        // anulowania. Guard L2 i tak wymaga pokazanego, aktywnego okna.
        harness.ShowOwnWindow();
        harness.ForegroundOwn(harness.Window);
        harness.PumpUntil(() => harness.Window.IsActive, "własne okno główne nie stało się aktywne");
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));

        // ANULOWANIE nie moze zapisac wyboru domu. Porownujemy SEKCJE sonos, bo
        // reszta pliku (nawigacja, ostatnia sesja) zmienia sie od pokazania okna.
        // Najpierw CZEKAMY, az wstepny DOM-1 z pamieci naprawde wyladuje w pliku:
        // zapis jest asynchroniczny i bez tego trafialby na dysk PO anulowaniu,
        // pozorujac zapis przez anulowanie.
        harness.PumpUntil(
            () => ReadSonosSection(harness.SettingsPath).Contains("DOM-1", StringComparison.Ordinal),
            "wstępny wybór domu nie trafił do pliku przed pomiarem anulowania");
        var beforeCancel = ReadSonosSection(harness.SettingsPath);
        harness.RunChoice(action: dialog =>
        {
            dialog.SelectRowForTests(1);
            harness.PressEscape(dialog);
        });
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        var afterCancel = ReadSonosSection(harness.SettingsPath);
        if (!string.Equals(beforeCancel, afterCancel, StringComparison.Ordinal))
        {
            throw new Exception(
                "Anulowanie zapisało wybór domu.\nPRZED: " + beforeCancel + "\nPO: " + afterCancel);
        }

        harness.RunChoice(action: dialog =>
        {
            dialog.SelectRowForTests(1);
            dialog.ConfirmForTests();
        });
        harness.PumpUntil(
            () => harness.Window.SonosSelectedHouseholdId == "DOM-2",
            "wybór domu B nie trafił do stanu");
        // SNAPSHOT po zapisie czytamy z AKTUALNEGO stanu okna, nie z kopii.
        harness.PumpUntil(
            () => ReadSettingsText(harness.SettingsPath).Contains("DOM-2", StringComparison.Ordinal),
            "świadomy wybór domu nie trafił do pliku");

        // DRUGIE okno czyta plik ZAPISANY przez pierwsze: realny Load z dysku,
        // nie pole w pamięci. Plik kopiujemy do osobnego katalogu, bo dwa
        // MainWindow na jednej ścieżce zderzają się na ZAPISIE - to byłby
        // artefakt pomiaru, nie wada produktu; mierzona treść jest ta sama.
        secondDirectory = Path.Combine(
            Path.GetTempPath(), "amc-sonos-household-2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(secondDirectory);
        var secondPath = Path.Combine(secondDirectory, "settings.json");
        File.WriteAllText(secondPath, ReadSettingsText(harness.SettingsPath));
        var reloadStore = new ConfigurationStore(secondPath);
        var reloaded = reloadStore.LoadOrCreate();
        if (reloaded.Sonos.SelectedHouseholdId != "DOM-2")
        {
            throw new Exception(
                "Po ponownym odczycie wybrany dom to "
                + (reloaded.Sonos.SelectedHouseholdId ?? "brak") + " zamiast DOM-2.");
        }

        // Pierwsze okno zostaje ZYWE do konca pomiaru: jego Dispose zamyka
        // dispatcher WSPOLNEGO watku STA, a wtedy druga pompa komunikatow nie
        // ma czego pompowac. Kolizji zapisu nie ma, bo drugie okno ma KOPIE.
        using var second = Harness.CreateOn(secondPath, reloadStore, reloaded);
        // ZNIKNIETY dom nie moze zostac podmieniony na sasiada, a zmiana nazwy
        // i kolejnosci nie moze ruszyc IDENTYFIKATORA.
        second.Backend.SetHouseholds(("DOM-2", "Piętro inaczej nazwane"), ("DOM-1", "Parter"));
        // Odtworzony wybor musi RZECZYWISCIE kierowac odczytem grup: jawne
        // odswiezenie grup w drugim oknie to ta sama droga zaplecza co sesja.
        second.EnterSonosSession();
        second.ExecuteCommand(CommandIds.RefreshSonosGroups);
        second.PumpUntil(
            () => second.Backend.LastGroupsHouseholdId is not null,
            "drugie okno nie doszło do odczytu grup odtworzonego domu");
        if (second.Window.SonosSelectedHouseholdId != "DOM-2")
        {
            throw new Exception("Drugie okno nie odtworzyło wyboru domu DOM-2.");
        }
        if (second.Backend.LastGroupsHouseholdId != "DOM-2")
        {
            throw new Exception(
                "Drugie okno czytało grupy domu " + (second.Backend.LastGroupsHouseholdId ?? "żadnego")
                + " zamiast odtworzonego DOM-2.");
        }
        return 6;
        }
        finally
        {
            if (!firstDisposed) harness.Dispose();
            foreach (var directory in new[]
                { Path.GetDirectoryName(harness.SettingsPath)!, secondDirectory })
            {
                try
                {
                    if (directory is not null && Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }

    // ===== B8: polecenie istnieje w RZECZYWISTYM menu i RZECZYWISTEJ palecie =====

    private static int MeasureCommandIsReachableInMenuAndPalette()
    {
        if (!CommandCatalog.GetAllCommandIds().Contains(ChooseCommandId, StringComparer.Ordinal))
        {
            throw new Exception("Polecenia wyboru domu Sonos nie ma w katalogu poleceń.");
        }
        var displayName = CommandCatalog.GetDisplayName(ChooseCommandId);
        if (!displayName.Contains("dom", StringComparison.OrdinalIgnoreCase)
            || !displayName.Contains("Sonos", StringComparison.Ordinal))
        {
            throw new Exception("Nazwa polecenia nie jest użytkowa: " + displayName);
        }

        var entries = CommandPaletteSearch.CreateEntries(KeyboardProfile.CreateDefault(), new AppSettings());
        var entry = entries.FirstOrDefault(candidate =>
            string.Equals(candidate.CommandId, ChooseCommandId, StringComparison.Ordinal))
            ?? throw new Exception("Polecenia nie ma na rzeczywistej liście palety.");
        _ = entry;

        var xaml = File.ReadAllText(LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml"));
        var index = xaml.IndexOf("x:Name=\"ChooseSonosHouseholdMenuItem\"", StringComparison.Ordinal);
        if (index < 0) throw new Exception("Menu Plik nie ma pozycji wyboru domu Sonos.");
        var end = xaml.IndexOf("/>", index, StringComparison.Ordinal);
        var block = xaml.Substring(index, end - index);
        if (!block.Contains("Click=\"ChooseSonosHousehold_Click\"", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja menu nie ma podłączonej obsługi.");
        }
        if (block.Contains("InputGestureText", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja menu ogłasza skrót, którego nie ma.");
        }

        var source = File.ReadAllText(
            LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml.cs"));
        if (!source.Contains("ChooseSonosHouseholdMenuItem.Visibility", StringComparison.Ordinal))
        {
            throw new Exception("Widoczność pozycji menu nie zależy od bieżącej sesji.");
        }
        if (typeof(MainWindow).GetMethod("ChooseSonosHousehold_Click", Instance) is null)
        {
            throw new Exception("Obsługa pozycji menu nie istnieje.");
        }

        // ZADNEGO globalnego skrotu i zadnej renumeracji cudzych poleceń.
        var profile = KeyboardProfile.CreateDefault();
        if (profile.Bindings.Values.Any(command =>
            string.Equals(command, ChooseCommandId, StringComparison.Ordinal)))
        {
            throw new Exception("Wybór domu Sonos zajął globalny skrót klawiszowy.");
        }
        return 7;
    }

    /// <summary>
    /// SEKCJA sonos z zapisanego pliku. Reszta pliku zmienia sie od pokazania
    /// okna (nawigacja, ostatnia sesja) i nie jest przedmiotem tego pomiaru.
    /// </summary>
    private static string ReadSonosSection(string path)
    {
        var text = ReadSettingsText(path);
        if (text.Length == 0) return string.Empty;
        using var document = System.Text.Json.JsonDocument.Parse(text);
        return document.RootElement.TryGetProperty("sonos", out var sonos)
            ? sonos.GetRawText()
            : string.Empty;
    }

    /// <summary>
    /// Odczyt pliku ustawien podczas dzialania okna. Produkt zapisuje plik
    /// ASYNCHRONICZNIE, wiec chwilowa blokada to normalny stan systemu plikow,
    /// a nie wynik pomiaru - ponawiamy, zamiast wywracac pomiar.
    /// </summary>
    private static string ReadSettingsText(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (true)
        {
            try
            {
                if (!File.Exists(path)) return string.Empty;
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }
        }
    }

    private static string LocateRepositoryFile(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new Exception("Nie znaleziono pliku repozytorium: " + relative);
    }

    // ==================== harness ====================

    private sealed class Harness : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

        private readonly string _directory;
        private readonly bool _ownsDirectory;

        /// <summary>Pomiar trwalosci potrzebuje pliku PO zamknieciu okna.</summary>
        internal bool KeepDirectoryOnDispose { get; set; }
        private readonly Dispatcher _dispatcher;

        private Harness(
            string directory,
            bool ownsDirectory,
            string settingsPath,
            MainWindow window,
            FakeBackend backend,
            List<string> announcements)
        {
            _directory = directory;
            _ownsDirectory = ownsDirectory;
            _dispatcher = Dispatcher.CurrentDispatcher;
            SettingsPath = settingsPath;
            Window = window;
            Backend = backend;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        internal string SettingsPath { get; }

        internal ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal bool PlayerViewActive =>
            (bool)Window.GetType().GetField("_playerViewActive", Instance)!.GetValue(Window)!;

        internal static Harness Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-household-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = new ConfigurationStore(settingsPath);
            return Build(directory, ownsDirectory: true, settingsPath, store, store.LoadOrCreate());
        }

        /// <summary>DRUGIE okno na TYM SAMYM pliku: realny Load, nie kopia stanu.</summary>
        internal static Harness CreateOn(
            string settingsPath,
            ConfigurationStore store,
            PersistedState state) =>
            Build(Path.GetDirectoryName(settingsPath)!, ownsDirectory: false, settingsPath, store, state);

        private static Harness Build(
            string directory,
            bool ownsDirectory,
            string settingsPath,
            ConfigurationStore store,
            PersistedState state)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

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
            return new Harness(directory, ownsDirectory, settingsPath, window, backend, announcements);
        }

        internal void EnterSonosSession()
        {
            ExecuteCommand(CommandIds.SessionSlot(8));
            PumpQuietly(TimeSpan.FromMilliseconds(120));
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        /// <summary>
        /// PRAWDZIWE pokazanie WLASNEGO okna glownego, wzorem
        /// SonosTopologyRefreshUiTests: odpinamy ContentRendered, zeby pomiar nie
        /// obudzil produkcyjnych uslug pulpitu (serwer NVDA, przedrostki).
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
            RequireNoProductionDesktopServices();
            MediaList.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(100));
        }

        /// <summary>
        /// IZOLACJA fixture: pomiar pokazuje prawdziwe okno, wiec musi udowodnic,
        /// ze NIE wystartowaly globalne uslugi pulpitu zainstalowanego AMC.
        /// </summary>
        internal void RequireNoProductionDesktopServices()
        {
            foreach (var field in new[] { "_nvdaCommandServer", "_prefixService" })
            {
                if (Window.GetType().GetField(field, Instance)?.GetValue(Window) is not null)
                {
                    throw new Exception("Fixture wystartował produkcyjną usługę pulpitu: " + field);
                }
            }
        }

        /// <summary>
        /// WLASNE okno staje sie NATYWNYM oknem pierwszoplanowym. Window.IsActive
        /// samo tego nie dowodzi. Minimalny Alt-down/SetForegroundWindow/Alt-up
        /// (wzorzec probe v2) i tylko przy WOLNYCH modyfikatorach - inaczej
        /// pomiar mieszalby sie z klawiszami uzytkownika.
        /// </summary>
        internal void ForegroundOwn(Window target)
        {
            foreach (var key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
            {
                if ((GetAsyncKeyState(key) & 0x8000) != 0)
                {
                    throw new Exception("Fixture: trzymany modyfikator; odmawiam aktywacji okna.");
                }
            }

            var handle = new WindowInteropHelper(target).Handle;
            // Sztuczny Alt jest potrzebny TYLKO, gdy pierwszy plan trzyma OBCY
            // watek: bez tego SetForegroundWindow bywa ignorowane. Gdy pierwszy
            // plan jest juz NASZ, Alt wpadalby w tryb menu okna glownego i modalna
            // petla menu zablokowalaby pomiar - wlasnie na tym zawisl B11-b.
            var foreign = GetWindowThreadProcessId(GetForegroundWindow(), out var pid) != 0
                && pid != (uint)Environment.ProcessId;
            if (foreign)
            {
                keybd_event(0x12, 0, 0, UIntPtr.Zero);
                try
                {
                    SetForegroundWindow(handle);
                }
                finally
                {
                    keybd_event(0x12, 0, 2, UIntPtr.Zero);
                }
            }
            else
            {
                target.Activate();
                SetForegroundWindow(handle);
            }

            PumpUntil(() => GetForegroundWindow() == handle && target.IsActive,
                "własne okno nie stało się natywnym oknem pierwszoplanowym");
        }

        internal bool IsNativeForeground(Window target) =>
            GetForegroundWindow() == new WindowInteropHelper(target).Handle;

        /// <summary>WSTRZYMANIE odczytu domow: prawdziwy await, nie atrapa.</summary>
        internal TaskCompletionSource HoldHouseholdRead()
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Backend.HouseholdGate = gate.Task;
            return gate;
        }

        /// <summary>PRAWDZIWE polecenie wyboru domu; oddaje jego zadanie.</summary>
        internal Task StartChoice()
        {
            ExecuteCommand(ChooseCommandId);
            return Window.LastSonosHouseholdChoiceTaskForTests
                ?? throw new Exception("Polecenie wyboru domu nie rozpoczęło zadania.");
        }

        /// <summary>Co RZECZYWISCIE zobaczylismy o prawdziwym modalu wyboru domu.</summary>
        internal sealed class DialogObservation
        {
            internal int Seen { get; set; }
            internal bool? Active { get; set; }
            internal bool? NativeForeground { get; set; }
            internal bool? OwnerIsMainWindow { get; set; }
            internal int CreatedDelta { get; set; }
        }

        /// <summary>
        /// Obserwacja PRODUKCYJNEGO guardu pokazania. Override siedzi ZA guardem
        /// (<c>CanPresentSonosHouseholdChoice</c>), wiec gdy guard odmowi, ta
        /// funkcja NIE zostanie wywolana - to mierzy rzeczywiste wiazanie
        /// wolajacy->guard, a nie sama metode pomocnicza. Prawdziwe, modalne
        /// <c>ShowDialog</c> bez override mierzy osobna sonda (run_probe.py):
        /// w tej sesji pulpitu modalna petla gubi aktywnosc okna glownego, wiec
        /// kontrola dodatnia przez ShowDialog nie dala sie tu zmierzyc.
        /// Wstrzymana odpowiedz jest zwalniana w finally, okna domykane zawsze.
        /// </summary>
        internal DialogObservation RunGuardedChoiceWithHeldRead(
            Action? whileHeld, bool showOwner = true)
        {
            var observation = new DialogObservation();
            var createdBefore = Window.SonosHouseholdWindowsCreatedForTests;

            // showOwner=false mierzy ODMOWE przy NIEPOKAZANYM oknie: fixture nie
            // ma prawa go wtedy pokazac, bo zatarlby mierzony warunek.
            if (showOwner)
            {
                ShowOwnWindow();
                ForegroundOwn(Window);
                PumpUntil(() => Window.IsActive, "własne okno główne nie stało się aktywne");
            }

            Window.PresentSonosHouseholdOverrideForTests = dialog =>
            {
                observation.Seen++;
                observation.OwnerIsMainWindow = ReferenceEquals(dialog.Owner, Window);
                dialog.ShowInTaskbar = false;
                dialog.Show();
                PumpUntil(() => dialog.IsLoaded, "okno wyboru domu się nie pokazało");
                observation.Active = dialog.IsActive;
                observation.NativeForeground = IsNativeForeground(dialog);
                dialog.Close();
                PumpUntil(() => !dialog.IsVisible, "okno wyboru domu się nie zamknęło");
            };

            var gate = HoldHouseholdRead();
            try
            {
                var operation = StartChoice();
                if (operation.IsCompleted) throw new Exception("Wybór domu nie zaczekał na transport.");
                whileHeld?.Invoke();

                Backend.HouseholdGate = null;
                gate.TrySetResult();
                Pump(operation);
                PumpQuietly(TimeSpan.FromMilliseconds(120));
            }
            finally
            {
                Backend.HouseholdGate = null;
                gate.TrySetResult();
                Window.PresentSonosHouseholdOverrideForTests = null;
                if (Window.OpenSonosHouseholdWindowForTests is { IsVisible: true } leftover) leftover.Close();
                PumpQuietly(TimeSpan.FromMilliseconds(60));
            }

            observation.CreatedDelta = Window.SonosHouseholdWindowsCreatedForTests - createdBefore;
            return observation;
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

        /// <summary>
        /// PRAWDZIWE polecenie, PRAWDZIWE okno. Okno pokazujemy przez Show i
        /// pompujemy petle - inaczej ShowDialog zablokowalby watek pomiaru.
        /// To NIE jest atrapa okna: to ten sam typ, ten sam XAML i ten sam kod.
        /// </summary>
        internal SonosHouseholdSelectionWindow? RunChoice(
            Action<SonosHouseholdSelectionWindow>? action = null)
        {
            // PRODUKCYJNY guard pokazania (widoczne + AKTYWNE okno glowne, brak
            // innych widocznych okien potomnych) obowiazuje TAKZE ten pomiar:
            // override siedzi ZA guardem, wiec fixture musi go spelnic NAPRAWDE.
            if (!Window.IsVisible)
            {
                ShowOwnWindow();
                ForegroundOwn(Window);
            }

            PumpUntil(() => Window.IsActive, "własne okno główne nie stało się aktywne");

            SonosHouseholdSelectionWindow? captured = null;
            Window.PresentSonosHouseholdOverrideForTests = dialog =>
            {
                captured = dialog;
                dialog.ShowInTaskbar = false;
                dialog.Owner = Window;
                dialog.Show();
                PumpUntil(
                    () => dialog.IsLoaded && PresentationSource.FromVisual(dialog) is not null,
                    "okno wyboru domu się nie pokazało");
                if (action is not null) action(dialog);
                else dialog.Close();
                PumpUntil(() => !dialog.IsVisible, "okno wyboru domu się nie zamknęło");
            };

            try
            {
                ExecuteCommand(ChooseCommandId);
                if (Window.LastSonosHouseholdChoiceTaskForTests is { } task) Pump(task);
            }
            finally
            {
                Window.PresentSonosHouseholdOverrideForTests = null;
            }

            return captured;
        }

        /// <summary>
        /// PELNA droga Escape: IsCancel woła prawdziwą obsługę okna, a nie
        /// nasze RaiseEvent udające kliknięcie.
        /// </summary>
        internal void PressEscape(SonosHouseholdSelectionWindow dialog)
        {
            var button = (Button)dialog.FindName("CancelButton")!;
            var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(button);
            var invoke = (System.Windows.Automation.Provider.IInvokeProvider)peer
                .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!;
            invoke.Invoke();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
        }

        internal string[] RowLabels() => MediaList.Items.Cast<object>()
            .Select(row => row.GetType().GetProperty("Label", Instance)?.GetValue(row) as string ?? string.Empty)
            .ToArray();

        internal void SelectAndActivateFirstGroup()
        {
            var list = MediaList;
            list.SelectedIndex = 0;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            var groupId = (Window.SonosGroupRows.Count > 0 ? Window.SonosGroupRows[0].GroupId : null)
                ?? throw new Exception("Brak grupy do aktywacji.");
            Pump(Window.ActivateSonosGroupForTests(groupId));
            PumpUntil(() => Window.SonosSelectedGroupId == groupId, "grupa nie stała się celem");
            PumpUntil(() => Window.SonosMetadataForTests is not null, "cel nie doczytał metadanych");
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

        internal void Pump(Task task)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: zadanie wyboru domu.");
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

        public void Dispose()
        {
            Window.CancelSonosPendingWork();
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
            PumpQuietly(TimeSpan.FromMilliseconds(200));
            if (!_ownsDirectory || KeepDirectoryOnDispose) return;
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
        private readonly Dictionary<string, (string Id, string Name)[]> _groupsByHousehold = new(StringComparer.Ordinal);

        private (string Id, string Name)[] _defaultGroups =
            [("GRUPA-SALON", "Salon"), ("GRUPA-KUCHNIA", "Kuchnia")];

        private (string Id, string Name)[] _households = [("DOM-1", "Dom")];

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal int HouseholdReads { get; private set; }

        internal int GroupReads { get; private set; }

        internal string? LastGroupsHouseholdId { get; private set; }

        internal Task? HouseholdGate { get; set; }

        /// <summary>WSTRZYMANIE odczytu GRUP: przelaczenie domu tez ma await.</summary>
        internal Task? GroupsGate { get; set; }

        internal SonosDeviceReadStatus? FailGroupsWith { get; set; }

        internal void SetHouseholds(params (string Id, string Name)[] households) => _households = households;

        internal void SetGroups(params (string Id, string Name)[] groups) => _defaultGroups = groups;

        internal void SetGroupsForHousehold(string householdId, params (string Id, string Name)[] groups) =>
            _groupsByHousehold[householdId] = groups;

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var status = new SonosGroupPlaybackStatus(
                SonosPlaybackState.Playing, null, null, "UTWOR-1", 12_000, null, null, null, _actions);
            return Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status));
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
            string? groupId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(new SonosGroupVolume(30, false, false)));

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SeekRelative);
            return Task.FromResult(
                SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
        }

        public async Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken)
        {
            HouseholdReads++;
            if (HouseholdGate is { } gate) await gate.ConfigureAwait(true);
            return SonosHouseholdsReadResult.Success(
                _households.Select(home => new SonosHousehold(home.Id, home.Name, null)).ToArray());
        }

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken)
        {
            GroupReads++;
            LastGroupsHouseholdId = householdId;
            return ReadGroupsCoreAsync(householdId);
        }

        private async Task<SonosGroupsReadResult> ReadGroupsCoreAsync(string householdId)
        {
            if (GroupsGate is { } gate) await gate.ConfigureAwait(true);
            if (FailGroupsWith is { } status)
            {
                return SonosGroupsReadResult.Failure(status);
            }

            var groups = _groupsByHousehold.TryGetValue(householdId, out var specific)
                ? specific
                : _defaultGroups;
            return SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                groups
                    .Select(group => new SonosGroup(
                        group.Id, group.Name, "P1", ["P1"], SonosPlaybackState.Idle))
                    .ToArray(),
                [new SonosPlayer("P1", "Salon", null, null, null)],
                false));
        }
    }
}
