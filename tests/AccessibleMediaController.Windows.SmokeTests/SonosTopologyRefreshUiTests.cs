using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// B2c1: JAWNE polecenie "Odśwież grupy Sonos" mierzone RZECZYWISTA droga
/// uzytkownika (prawdziwe <c>ExecuteCommand</c>), a nie wewnetrznym helperem.
///   * C1: odswiezenie czyta domy i grupy Z BACKENDU, nie z cache.
///   * C2: grupa znikniela po POTWIERDZONYM swiezym odczycie uniewaznia cel,
///         a spozniony GET w locie NIE odtwarza starych danych.
///   * C3: ten sam identyfikator po zmianie nazwy i kolejnosci ZOSTAJE.
///   * C4: BLAD odczytu to nie dowod zniknięcia - wybor przezywa.
///   * C5: dwa odswiezenia naraz nie mnoza GET.
///   * C6: prawdziwe menu, paleta i podlaczenie polecenia.
///
/// Pomiar jest SYNTETYCZNY: wlasne okno, wlasna konfiguracja w katalogu
/// tymczasowym, granica ISonosGroupSessionBackend. Zero konta, HTTP, DPAPI,
/// audio, NVDA i klawiszy systemowych.
/// </summary>
internal static class SonosTopologyRefreshUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Identyfikator polecenia jako TEKST, celowo bez stalej: dzieki temu faza
    /// RED jest RZECZYWISTYM niepowodzeniem zachowania, a nie bledem kompilacji.
    /// </summary>
    private const string RefreshCommandId = "sonos.groups.refresh";

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasureExplicitRefreshReadsFreshTopology();
                checks += MeasureVanishedGroupInvalidatesTargetAndStaleRead();
                checks += MeasureRenameAndReorderKeepSelectionById();
                checks += MeasureReadFailureDoesNotFakeDisappearance();
                checks += MeasureConcurrentRefreshDoesNotMultiplyReads();
                checks += MeasureAbandonedRefreshDoesNotBlockNextOne();
                checks += MeasureMissingSelectionIsNotVanishedHousehold();
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

        checks += MeasureCommandIsReachableInMenuAndPalette();

        Console.WriteLine(
            "OK: jawne odświeżanie grup Sonos - prawdziwe polecenie czyta świeżą topologię, "
            + "zniknięta grupa unieważnia cel i spóźniony odczyt, zmiana nazwy i kolejności "
            + $"zachowuje wybór po ID, błąd odczytu nie udaje zniknięcia ({checks} sprawdzeń)");
    }

    // ===== C1: prawdziwe polecenie robi SWIEZY odczyt domow i grup =====

    private static int MeasureExplicitRefreshReadsFreshTopology()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "wejście do sesji nie wypełniło listy grup");
        var householdReadsAfterEntry = harness.Backend.HouseholdReads;
        var groupReadsAfterEntry = harness.Backend.GroupReads;

        // Topologia w chmurze SIE ZMIENILA: doszla nowa grupa.
        harness.Backend.SetGroups(
            ("GRUPA-SALON", "Salon"),
            ("GRUPA-KUCHNIA", "Kuchnia"),
            ("GRUPA-SYPIALNIA", "Sypialnia"));

        var announcementsBefore = harness.Announcements.Count;
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpUntil(
            () => harness.MediaList.Items.Count == 3,
            "jawne polecenie odświeżenia nie pokazało świeżych grup w kontrolce listy");

        if (harness.Backend.HouseholdReads <= householdReadsAfterEntry)
        {
            throw new Exception(
                $"Odświeżenie nie odczytało domów z backendu (odczytów {harness.Backend.HouseholdReads}, "
                + $"przed {householdReadsAfterEntry}); wzięło dane z cache.");
        }
        if (harness.Backend.GroupReads <= groupReadsAfterEntry)
        {
            throw new Exception("Odświeżenie nie odczytało grup wybranego domu z backendu.");
        }
        var labels = harness.RowLabels();
        if (!labels.Any(label => label.Contains("Sypialnia", StringComparison.Ordinal)))
        {
            throw new Exception("Kontrolka listy nie pokazuje nowej grupy po odświeżeniu.");
        }
        if (window.SonosGroupRows.Count != 3)
        {
            throw new Exception($"Model wierszy ma {window.SonosGroupRows.Count} grup zamiast 3.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Odświeżenie grup wysłało POST do Sonosa.");
        }

        var added = harness.Announcements.Skip(announcementsBefore).ToArray();
        if (added.Length is 0 or > 2)
        {
            throw new Exception(
                $"Odświeżenie powiedziało {added.Length} komunikatów; oczekiwano ładowania i wyniku bez spamu: "
                + string.Join(" | ", added));
        }
        if (!added[^1].Contains("grup", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Ostatni komunikat odświeżenia nie mówi o grupach: " + added[^1]);
        }
        return 7;
    }

    // ===== C2: znikniecie aktywnej grupy uniewaznia cel i spozniony odczyt =====

    private static int MeasureVanishedGroupInvalidatesTargetAndStaleRead()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ShowOwnWindow();
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak listy grup przed pomiarem zniknięcia");

        // ODCZYT stanu grupy zostaje W LOCIE: aktywacja czeka na barierę.
        var release = new TaskCompletionSource();
        harness.Backend.ReadGate = release.Task;
        Task activation;
        try
        {
            harness.SelectAndFocusRow(harness.IndexOfGroup("Salon"));
            harness.PressKey(Key.Enter);
            harness.PumpUntil(() => harness.Backend.PlaybackReads > 0, "aktywacja grupy nie zaczęła odczytu");
            activation = window.LastSonosActivationTaskForTests
                ?? throw new Exception("Enter nie rozpoczął zadania aktywacji.");
            if (activation.IsCompleted) throw new Exception("Zadanie aktywacji skończyło się mimo wstrzymanego odczytu.");
            if (window.SonosSelectedGroupId != "GRUPA-SALON")
            {
                throw new Exception("Kontrolka pomiaru: aktywna grupa nie została wybrana przed odświeżeniem.");
            }

            // Grupa ZNIKA w chmurze; POTWIERDZONY świeży odczyt to pokaże.
            harness.Backend.SetGroups(("GRUPA-KUCHNIA", "Kuchnia"));
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpUntil(
                () => harness.Backend.GroupReads >= 2 && window.SonosSelectedGroupId is null,
                "odświeżenie nie unieważniło zniknionej aktywnej grupy");
        }
        finally
        {
            harness.Backend.ReadGate = null;
            release.TrySetResult();
        }

        if (window.SonosPlaybackForTests is not null
            || window.SonosMetadataForTests is not null
            || window.SonosVolumeForTests is not null)
        {
            throw new Exception("Po zniknięciu grupy zostały jej dane odtwarzania, metadanych lub głośności.");
        }
        var session = window.SessionsForTests.FindSession("sonos")
            ?? throw new Exception("Brak sesji Sonos.");
        if (session.Items.Any(item => string.Equals(item.Id, "GRUPA-SALON", StringComparison.Ordinal)))
        {
            throw new Exception("Rzeczywista lista sesji nadal zawiera zniknioną grupę.");
        }
        if (session.HasCurrentItem && session.CurrentItem.Id == "GRUPA-SALON")
        {
            throw new Exception("Bieżący element sesji nadal wskazuje zniknioną grupę.");
        }
        if (harness.MediaList.Items.Count != 1)
        {
            throw new Exception($"Widoczna kontrolka listy ma {harness.MediaList.Items.Count} wierszy zamiast 1.");
        }
        if (harness.PlayerViewActive)
        {
            throw new Exception("Odtwarzacz zniknionej grupy został otwarty mimo unieważnienia celu.");
        }
        if (window.SonosSelectedGroupId == "GRUPA-KUCHNIA")
        {
            throw new Exception("Wybór został po cichu przeniesiony na sąsiednią grupę.");
        }
        if (window.SonosSelectedHouseholdId != "DOM-1")
        {
            throw new Exception("Istniejący dom został skasowany przy zniknięciu samej grupy.");
        }

        // SPOZNIONY odczyt starej grupy konczy sie TERAZ: nie wolno mu nic opublikowac.
        harness.Pump(activation);
        if (window.SonosPlaybackForTests is not null || window.SonosMetadataForTests is not null)
        {
            throw new Exception("Spóźniony odczyt zniknionej grupy odtworzył jej dane po odświeżeniu.");
        }
        if (window.SonosSelectedGroupId is not null || harness.PlayerViewActive)
        {
            throw new Exception("Spóźniony odczyt przywrócił porzucony cel albo otworzył odtwarzacz.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Unieważnienie celu wysłało POST do Sonosa.");
        }
        return 10;
    }

    // ===== C3: ten sam identyfikator po zmianie nazwy i kolejnosci =====

    private static int MeasureRenameAndReorderKeepSelectionById()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak listy grup przed zmianą nazw");
        harness.Pump(window.ActivateSonosGroupForTests("GRUPA-KUCHNIA"));
        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception("Kontrolka pomiaru: grupa nie została wybrana przed odświeżeniem.");
        }

        // TA SAMA grupa, inna nazwa i inna kolejnosc.
        harness.Backend.SetGroups(
            ("GRUPA-KUCHNIA", "Kuchnia i jadalnia"),
            ("GRUPA-SALON", "Duży salon"));
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpUntil(
            () => harness.Backend.GroupReads >= 2
                && harness.RowLabels().Any(label => label.Contains("jadalnia", StringComparison.Ordinal)),
            "odświeżenie nie pokazało zmienionych nazw grup");

        if (window.SonosSelectedGroupId != "GRUPA-KUCHNIA")
        {
            throw new Exception(
                "Zmiana nazwy i kolejności zgubiła wybór po identyfikatorze: "
                + (window.SonosSelectedGroupId ?? "null"));
        }
        if (window.SonosActiveGroup?.Name != "Kuchnia i jadalnia")
        {
            throw new Exception("Aktywna grupa nie pokazuje świeżej nazwy.");
        }
        if (window.SonosSelectedHouseholdId != "DOM-1")
        {
            throw new Exception("Wybrany dom zniknął mimo obecności w świeżym odczycie.");
        }
        if (harness.Backend.Commands.Count != 0) throw new Exception("Odświeżenie wysłało POST do Sonosa.");
        return 5;
    }

    // ===== C4: blad odczytu NIE jest dowodem zniknięcia =====

    private static int MeasureReadFailureDoesNotFakeDisappearance()
    {
        using var harness = Harness.Create();
        var window = harness.Window;
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak listy grup przed błędem odczytu");
        harness.Pump(window.ActivateSonosGroupForTests("GRUPA-SALON"));

        harness.Backend.FailGroupsWith = SonosDeviceReadStatus.ServiceError;
        var announcementsBefore = harness.Announcements.Count;
        var refreshes = harness.Backend.GroupReads;
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpUntil(() => harness.Backend.GroupReads > refreshes, "odświeżenie nie spróbowało odczytu");
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));
        harness.Backend.FailGroupsWith = null;

        if (window.SonosSelectedGroupId != "GRUPA-SALON")
        {
            throw new Exception("Nieudany odczyt zniszczył poprawny identyfikator grupy.");
        }
        if (window.SonosSelectedHouseholdId != "DOM-1")
        {
            throw new Exception("Nieudany odczyt zniszczył poprawny identyfikator domu.");
        }
        if (harness.MediaList.Items.Count != 2)
        {
            throw new Exception("Nieudany odczyt opublikował pustkę jako wynik odświeżenia.");
        }
        var added = harness.Announcements.Skip(announcementsBefore).ToArray();
        if (added.Length == 0 || !added[^1].Contains("nie", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                "Po nieudanym odczycie użytkownik nie usłyszał braku świeżości: "
                + string.Join(" | ", added));
        }
        return 4;
    }

    // ===== C5: dwa odswiezenia naraz nie mnoza GET =====

    private static int MeasureConcurrentRefreshDoesNotMultiplyReads()
    {
        using var harness = Harness.Create();
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak listy grup przed podwójnym odświeżeniem");

        var release = new TaskCompletionSource();
        harness.Backend.HouseholdGate = release.Task;
        var householdReadsBefore = harness.Backend.HouseholdReads;
        try
        {
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpUntil(
                () => harness.Backend.HouseholdReads == householdReadsBefore + 1,
                "pierwsze odświeżenie nie zaczęło odczytu domów");
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
            if (harness.Backend.HouseholdReads != householdReadsBefore + 1)
            {
                throw new Exception(
                    $"Drugie odświeżenie w trakcie pierwszego zwielokrotniło GET: {harness.Backend.HouseholdReads}.");
            }
        }
        finally
        {
            harness.Backend.HouseholdGate = null;
            release.TrySetResult();
        }

        harness.PumpUntil(() => harness.Backend.GroupReads >= 2, "pierwsze odświeżenie się nie dokończyło");
        // Po zakonczeniu bramka musi byc ZWOLNIONA: kolejne odswiezenie dziala.
        var groupReadsAfterFirst = harness.Backend.GroupReads;
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpUntil(
            () => harness.Backend.GroupReads > groupReadsAfterFirst,
            "po zakończeniu pierwszego odświeżenia kolejne nie doszło do skutku");
        return 3;
    }

    // ===== C7 (L1): porzucone odswiezenie NIE blokuje nastepnego =====

    /// <summary>
    /// RZECZYWISTA droga uzytkownika: odswiezenie A czeka na odpowiedz, uzytkownik
    /// wychodzi do innej sesji i wraca, odswiezenie B MUSI wystartowac. Spoznione A
    /// nie zwalnia bramki B, powtorka C w trakcie B nie mnozy GET i mowi krotko,
    /// a zwolnione B faktycznie konczy sie swiezymi danymi.
    /// </summary>
    private static int MeasureAbandonedRefreshDoesNotBlockNextOne()
    {
        using var harness = Harness.Create();
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak listy grup przed porzuceniem odświeżenia");

        var releaseA = new TaskCompletionSource();
        harness.Backend.HouseholdGate = releaseA.Task;
        var readsBeforeA = harness.Backend.HouseholdReads;
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpUntil(
            () => harness.Backend.HouseholdReads == readsBeforeA + 1,
            "odświeżenie A nie zaczęło odczytu domów");
        if (!harness.RefreshInFlight) throw new Exception("Kontrolka pomiaru: odświeżenie A nie jest w locie.");

        // RZECZYWISTE wyjscie do innej, NIEPUSTEJ sesji i powrot - prawdziwe polecenia.
        harness.ExecuteCommand(CommandIds.SessionSlot(3));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
        harness.ExecuteCommand(CommandIds.SessionSlot(8));
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

        // Topologia w chmurze sie zmienila; B ma to pokazac PO zwolnieniu bariery.
        harness.Backend.SetGroups(
            ("GRUPA-SALON", "Salon"),
            ("GRUPA-KUCHNIA", "Kuchnia"),
            ("GRUPA-SYPIALNIA", "Sypialnia"));
        var releaseB = new TaskCompletionSource();
        harness.Backend.HouseholdGate = releaseB.Task;
        var readsBeforeB = harness.Backend.HouseholdReads;
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpUntil(
            () => harness.Backend.HouseholdReads == readsBeforeB + 1,
            "po powrocie do sesji porzucone odświeżenie A nadal blokuje rozpoczęcie B");

        // STARE A konczy sie TERAZ: nie wolno mu zwolnic bramki trwajacego B.
        releaseA.TrySetResult();
        harness.PumpQuietly(TimeSpan.FromMilliseconds(250));
        if (!harness.RefreshInFlight)
        {
            throw new Exception("Spóźnione odświeżenie A zwolniło bramkę trwającego odświeżenia B.");
        }
        if (harness.MediaList.Items.Count != 2)
        {
            throw new Exception("Porzucone odświeżenie A opublikowało wynik po wyjściu z sesji.");
        }

        // SWIADOMA powtorka C w trakcie B: zero GET i krotki komunikat.
        var readsBeforeC = harness.Backend.HouseholdReads;
        var announcementsBeforeC = harness.Announcements.Count;
        harness.ExecuteCommand(RefreshCommandId);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
        if (harness.Backend.HouseholdReads != readsBeforeC)
        {
            throw new Exception("Powtórka odświeżenia w trakcie B zwielokrotniła GET domów.");
        }
        var addedC = harness.Announcements.Skip(announcementsBeforeC).ToArray();
        if (addedC.Length != 1 || !addedC[0].Contains("już trwa", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception(
                "Powtórka w trakcie odświeżania nie powiedziała krótko, że odświeżanie już trwa: "
                + string.Join(" | ", addedC));
        }

        // ZWOLNIONE B faktycznie sie konczy i publikuje SWIEZE dane.
        harness.Backend.HouseholdGate = null;
        releaseB.TrySetResult();
        harness.PumpUntil(
            () => harness.MediaList.Items.Count == 3,
            "zwolnione odświeżenie B nie dokończyło publikacji świeżej topologii");
        if (harness.RefreshInFlight)
        {
            throw new Exception("Po zakończeniu B bramka odświeżania została zamknięta na zawsze.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Odświeżanie po porzuceniu wysłało POST do Sonosa.");
        }
        return 8;
    }

    // ===== C8 (L2): brak wyboru domu to NIE zniknięcie domu =====

    /// <summary>
    /// <c>household is null</c> ma DWIE rozne przyczyny. Swiadomy zapisany dom,
    /// ktory zniknal, uniewaznia cel i mowi o zniknięciu. Brak jakiegokolwiek
    /// wyboru przy dwoch domach albo braku domow NIE jest zniknięciem: nic nie
    /// uniewazniamy, domu nie wybieramy za uzytkownika i nie klamiemy.
    /// </summary>
    private static int MeasureMissingSelectionIsNotVanishedHousehold()
    {
        var checks = 0;

        // (a) DWA domy, ZERO zapisanego wyboru - uczciwy brak wybranego domu.
        using (var harness = Harness.Create())
        {
            harness.Backend.SetHouseholds(("DOM-1", "Dom"), ("DOM-2", "Domek nad morzem"));
            harness.ExecuteCommand(CommandIds.SessionSlot(8));
            harness.PumpUntil(() => harness.Backend.HouseholdReads > 0, "wejście nie odczytało domów");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
            if (harness.Window.SonosSelectedHouseholdId is not null)
            {
                throw new Exception("Kontrolka pomiaru: dom został wybrany po cichu przy dwóch domach.");
            }

            var before = harness.Announcements.Count;
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpUntil(
                () => harness.Announcements.Count >= before + 2,
                "odświeżenie bez wybranego domu nic nie powiedziało");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
            var added = harness.Announcements.Skip(before).ToArray();
            if (added.Any(message => message.Contains("nie istnieje", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception(
                    "Bez żadnego wyboru odświeżenie skłamało o zniknięciu domu: " + string.Join(" | ", added));
            }
            if (!added[^1].Contains("Nie wybrano domu", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    "Użytkownik nie usłyszał uczciwego braku wybranego domu: " + string.Join(" | ", added));
            }
            if (added[^1].Contains("Domek nad morzem", StringComparison.Ordinal))
            {
                throw new Exception("Komunikat podsuwa konkretny dom, choć okna wyboru jeszcze nie ma.");
            }
            if (harness.Window.SonosSelectedHouseholdId is not null)
            {
                throw new Exception("Odświeżenie wybrało dom za użytkownika przy dwóch domach.");
            }
            if (harness.Backend.Commands.Count != 0) throw new Exception("Odświeżenie wysłało POST do Sonosa.");
            checks += 5;
        }

        // (b) ZERO domow, ZERO wyboru - uczciwy brak dostepnych domow, nie wylogowanie.
        using (var harness = Harness.Create())
        {
            harness.Backend.SetHouseholds();
            harness.ExecuteCommand(CommandIds.SessionSlot(8));
            harness.PumpUntil(() => harness.Backend.HouseholdReads > 0, "wejście nie odczytało domów");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(200));

            var before = harness.Announcements.Count;
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpUntil(
                () => harness.Announcements.Count >= before + 2,
                "odświeżenie bez domów nic nie powiedziało");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
            var added = harness.Announcements.Skip(before).ToArray();
            if (added.Any(message => message.Contains("nie istnieje", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception(
                    "Brak domów został ogłoszony jako zniknięcie wybranego domu: " + string.Join(" | ", added));
            }
            if (!added[^1].Contains("dom", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Brak dostępnych domów nie został nazwany: " + string.Join(" | ", added));
            }
            if (harness.Backend.Commands.Count != 0) throw new Exception("Odświeżenie wysłało POST do Sonosa.");
            checks += 3;
        }

        // (c) JEDEN dom, ZERO wyboru - start pozostaje jednoznaczny.
        using (var harness = Harness.Create())
        {
            harness.ExecuteCommand(CommandIds.SessionSlot(8));
            harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "jeden dom nie dał listy grup");
            var before = harness.Announcements.Count;
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpUntil(
                () => harness.Backend.GroupReads >= 2,
                "odświeżenie przy jednym domu nie odczytało grup");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
            var added = harness.Announcements.Skip(before).ToArray();
            if (harness.Window.SonosSelectedHouseholdId != "DOM-1")
            {
                throw new Exception("Jednoznaczny jeden dom przestał być przyjmowany przy odświeżeniu.");
            }
            if (added.Any(message => message.Contains("Nie wybrano domu", StringComparison.OrdinalIgnoreCase)
                || message.Contains("nie istnieje", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("Jeden dom dał komunikat o braku wyboru: " + string.Join(" | ", added));
            }
            checks += 3;
        }

        // (d) SWIADOMY zapisany dom, ktory ZNIKNAL - dawne czyszczenie i uczciwy tekst.
        using (var harness = Harness.Create())
        {
            var window = harness.Window;
            harness.ExecuteCommand(CommandIds.SessionSlot(8));
            harness.PumpUntil(() => harness.MediaList.Items.Count == 2, "brak listy grup przed zniknięciem domu");
            harness.Pump(window.ActivateSonosGroupForTests("GRUPA-SALON"));
            if (window.SonosSelectedHouseholdId != "DOM-1" || window.SonosSelectedGroupId != "GRUPA-SALON")
            {
                throw new Exception("Kontrolka pomiaru: świadomy cel nie został ustawiony.");
            }

            // Zapisany dom ZNIKA, w chmurze sa DWA INNE domy.
            harness.Backend.SetHouseholds(("DOM-2", "Domek nad morzem"), ("DOM-3", "Chata"));
            var before = harness.Announcements.Count;
            harness.ExecuteCommand(RefreshCommandId);
            harness.PumpUntil(
                () => window.SonosSelectedHouseholdId is null,
                "zniknięcie świadomie wybranego domu nie unieważniło celu");
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
            var added = harness.Announcements.Skip(before).ToArray();
            if (!added[^1].Contains("już nie istnieje", StringComparison.Ordinal))
            {
                throw new Exception(
                    "Zniknięcie świadomego domu nie zostało nazwane: " + string.Join(" | ", added));
            }
            if (window.SonosSelectedGroupId is not null)
            {
                throw new Exception("Cel w znikniętym domu nie został wyczyszczony.");
            }
            if (window.SonosSelectedHouseholdId == "DOM-2")
            {
                throw new Exception("Zniknięty dom został po cichu podmieniony na sąsiada.");
            }
            if (harness.Backend.Commands.Count != 0) throw new Exception("Unieważnienie wysłało POST do Sonosa.");
            checks += 4;
        }

        return checks;
    }

    // ===== C6: menu, paleta i podlaczenie polecenia =====
    private static int MeasureCommandIsReachableInMenuAndPalette()
    {
        if (!CommandCatalog.GetAllCommandIds().Contains(RefreshCommandId, StringComparer.Ordinal))
        {
            throw new Exception("Polecenie odświeżania grup Sonos nie istnieje w katalogu poleceń.");
        }
        var displayName = CommandCatalog.GetDisplayName(RefreshCommandId);
        if (!displayName.Contains("Odśwież", StringComparison.OrdinalIgnoreCase)
            || !displayName.Contains("Sonos", StringComparison.Ordinal))
        {
            throw new Exception("Nazwa polecenia nie jest użytkowa: " + displayName);
        }

        // RZECZYWISTA lista palety powstaje z profilu klawiatury, nie z samego katalogu.
        var entries = CommandPaletteSearch.CreateEntries(KeyboardProfile.CreateDefault(), new AppSettings());
        var entry = entries.FirstOrDefault(candidate =>
            string.Equals(candidate.CommandId, RefreshCommandId, StringComparison.Ordinal))
            ?? throw new Exception("Polecenia nie ma na rzeczywistej liście palety.");
        if (!string.IsNullOrWhiteSpace(entry.PrefixShortcut) || !string.IsNullOrWhiteSpace(entry.LocalShortcut))
        {
            throw new Exception("Polecenie wzięło skrót klawiszowy, choć nowych skrótów nie dodajemy.");
        }

        // WIDOCZNOSC w palecie: tylko sesja Sonos.
        var visible = typeof(MainWindow).GetMethod("CommandVisibleInPalette", Instance)
            ?? throw new Exception("Nie ma prawdziwej bramki widoczności palety.");
        _ = visible;

        // PRAWDZIWA gałąź polecenia w oknie, a nie sam wpis w katalogu.
        var windowSource = File.ReadAllText(
            LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml.cs"));
        if (!windowSource.Contains("CommandIds.RefreshSonosGroups", StringComparison.Ordinal))
        {
            throw new Exception("ExecuteCommand nie zna polecenia odświeżania grup Sonos.");
        }

        var xaml = File.ReadAllText(LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml"));
        var index = xaml.IndexOf("x:Name=\"RefreshSonosGroupsMenuItem\"", StringComparison.Ordinal);
        if (index < 0) throw new Exception("Menu Plik nie ma pozycji odświeżania grup Sonos.");
        var end = xaml.IndexOf("/>", index, StringComparison.Ordinal);
        var block = xaml.Substring(index, end - index);
        if (!block.Contains("Click=\"RefreshSonosGroups_Click\"", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja menu nie ma podłączonej obsługi.");
        }
        if (block.Contains("InputGestureText", StringComparison.Ordinal))
        {
            throw new Exception("Pozycja menu ogłasza skrót, którego nie ma.");
        }

        var source = File.ReadAllText(LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.xaml.cs"));
        if (!source.Contains("RefreshSonosGroupsMenuItem.Visibility", StringComparison.Ordinal))
        {
            throw new Exception("Widoczność pozycji menu nie zależy od bieżącej sesji.");
        }
        var handler = typeof(MainWindow).GetMethod("RefreshSonosGroups_Click", Instance)
            ?? throw new Exception("Obsługa pozycji menu nie istnieje.");
        _ = handler;

        // Instrukcja odzyskania NIE moze odsylac do nieistniejacego F5.
        var sonosSource = File.ReadAllText(LocateRepositoryFile("src/AccessibleMediaController.Windows/MainWindow.Sonos.cs"));
        if (sonosSource.Contains("Odśwież listę grup\"", StringComparison.Ordinal))
        {
            throw new Exception("Instrukcja odzyskania nie nazywa istniejącego polecenia odświeżania.");
        }
        return 8;
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

        internal bool PlayerViewActive =>
            (bool)Window.GetType().GetField("_playerViewActive", Instance)!.GetValue(Window)!;

        /// <summary>Bramka odświeżania widziana z zewnątrz: pole produktu, nie kopia.</summary>
        internal bool RefreshInFlight =>
            (bool)Window.GetType().GetField("_sonosRefreshInFlight", Instance)!.GetValue(Window)!;

        internal static Harness Create()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-refresh-" + Guid.NewGuid().ToString("N"));
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

        internal string[] RowLabels() => MediaList.Items.Cast<object>()
            .Select(row => row.GetType().GetProperty("Label", Instance)?.GetValue(row) as string ?? string.Empty)
            .ToArray();

        internal int IndexOfGroup(string groupName) =>
            Enumerable.Range(0, MediaList.Items.Count).First(candidate =>
                RowLabels()[candidate].Contains(groupName, StringComparison.Ordinal));

        internal void SelectAndFocusRow(int index)
        {
            var list = MediaList;
            list.SelectedIndex = index;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container) container.Focus();
            else list.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.SelectedIndex != index) throw new Exception("Nie udało się zaznaczyć wiersza listy.");
        }

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
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: zadanie sesji Sonos się nie zakończyło.");
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
        private (string Id, string Name)[] _groups =
            [("GRUPA-SALON", "Salon"), ("GRUPA-KUCHNIA", "Kuchnia")];

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int HouseholdReads { get; private set; }

        internal int GroupReads { get; private set; }

        /// <summary>Wstrzymanie ODCZYTU STANU grupy: pozwala zmierzyć spóźniony wynik.</summary>
        internal Task? ReadGate { get; set; }

        /// <summary>Wstrzymanie ODCZYTU DOMÓW: pozwala zmierzyć dwa odświeżenia naraz.</summary>
        internal Task? HouseholdGate { get; set; }

        internal SonosDeviceReadStatus? FailGroupsWith { get; set; }

        internal void SetGroups(params (string Id, string Name)[] groups) => _groups = groups;

        private (string Id, string Name)[] _households = [("DOM-1", "Dom")];

        /// <summary>Zwięzłe sterowanie domami: 0, 1 albo wiele, bez osobnej atrapy.</summary>
        internal void SetHouseholds(params (string Id, string Name)[] households) => _households = households;

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
                SonosPlaybackState.Playing, null, null, "UTWOR-1", 12_000, null, null, null, _actions);
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
            // KONTRAKT ZADANIA: porzucony przelot ma byc odrzucony przez bramke
            // biletu, nawet gdy atrapa IGNORUJE cancellation - tak jak tutaj.
            return SonosHouseholdsReadResult.Success(
                _households.Select(home => new SonosHousehold(home.Id, home.Name, null)).ToArray());
        }

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken)
        {
            GroupReads++;
            if (FailGroupsWith is { } status)
            {
                return Task.FromResult(SonosGroupsReadResult.Failure(status));
            }
            return Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                _groups
                    .Select(group => new SonosGroup(
                        group.Id, group.Name, "P1", ["P1"], SonosPlaybackState.Idle))
                    .ToArray(),
                [new SonosPlayer("P1", "Salon", null, null, null)],
                false)));
        }
    }
}
