using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// F2: DOSTEPNY PODGLAD ULUBIONYCH Sonos mierzony RZECZYWISTA droga uzytkownika.
///
/// Pomiar idzie przez prawdziwe <c>ExecuteCommand(CommandIds.ViewFavorites)</c>
/// prawdziwego <see cref="MainWindow"/>, a zaplecze jest SYNTETYCZNE: zero HTTP,
/// zero tokenu, zero prawdziwego konta i zero audio.
///
/// UCZCIWA GRANICA: to NIE jest dowod, ze NVDA cokolwiek wypowiedzial -
/// komunikaty zbieramy z <c>AnnouncementSinkForTests</c>, czyli z tresci ODDANEJ
/// czytnikowi.
///
/// TEN PLIK JEST NAJPIERW CZERWONY: w bazie 3c46a80 nie ma ani polecenia
/// otwierajacego liste ulubionych w sesji Sonos, ani okna listy. Wejscia, ktore
/// dopiero maja powstac, sa tu siegane REFLEKSJA - inaczej plik nie skompilowal
/// by sie na bazie i nie dalby sie zmierzyc jako RED.
/// </summary>
internal static class SonosFavoritesUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                void Stage(string name) => Console.Error.WriteLine("ETAP: " + name);
                Stage("F1"); checks += MeasureViewFavoritesOpensFreshListInSonosSession();
                Stage("F2"); checks += MeasureOwnerStaysLazyAtStartup();
                Stage("F3"); checks += MeasureEmptyListIsAccessibleState();
                Stage("F4"); checks += MeasureMissingHouseholdExplainsRecovery();
                Stage("F5"); checks += MeasureRefusalNeverLooksLikeEmptyList();
                Stage("F6"); checks += MeasureHouseholdChangeDuringReadRejectsStaleWindow();
                Stage("F7"); checks += MeasureBusyGateRefusesAndFreesForRetry();
                Stage("F8"); checks += MeasureOtherSessionKeepsOldFavoritesPath();
                Stage("F9"); checks += MeasureLeavingSessionFreesFavoritesGateForNewRead();
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
        if (!thread.Join(TimeSpan.FromSeconds(100)))
        {
            throw new Exception("Limit czasu pomiaru ulubionych Sonos: patrz ostatni ETAP na stderr.");
        }

        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: podgląd ulubionych Sonos - istniejące Pokaż ulubione otwiera świeżą listę "
            + $"bieżącego domu ({checks} sprawdzeń)");
    }

    // ===== F1: Pokaz ulubione w sesji Sonos czyta SWIEZA liste i otwiera okno =====

    private static int MeasureViewFavoritesOpensFreshListInSonosSession()
    {
        using var harness = Harness.Create();
        harness.Backend.SetHouseholds(("DOM-1", "Parter"));
        // DWIE TAKIE SAME NAZWY i ROZNE identyfikatory: obie pozycje zostaja, w
        // KOLEJNOSCI API, bez sortowania i bez scalania.
        harness.Backend.SetFavorites(
            "W1",
            ("ULU-3", "Radio Nowy Świat", "Sonos Radio", null),
            ("ULU-1", "Nokturny", null, "Chopin na noc"),
            ("ULU-2", "Radio Nowy Świat", null, null));
        harness.ShowOwnWindow();
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";

        var window = harness.RunFavorites();
        if (window is null) throw new Exception("Pokaż ulubione nie otworzyło okna ulubionych Sonos.");
        if (harness.Backend.FavoriteReadsForTests != 1)
        {
            throw new Exception(
                $"Zaplecze dostało {harness.Backend.FavoriteReadsForTests} odczytów ulubionych zamiast 1.");
        }
        if (!string.Equals(harness.Backend.LastFavoritesHouseholdId, "DOM-1", StringComparison.Ordinal))
        {
            throw new Exception(
                "Odczyt ulubionych poszedł dla \"" + harness.Backend.LastFavoritesHouseholdId
                + "\" zamiast dla wybranego domu DOM-1.");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Samo otwarcie podglądu ulubionych wysłało polecenie do Sonosa.");
        }

        var labels = Rows(window);
        if (labels.Count != 3) throw new Exception($"Okno ma {labels.Count} wierszy zamiast 3.");
        // KOLEJNOSC API 1:1.
        if (!labels[0].StartsWith("Radio Nowy Świat", StringComparison.Ordinal)
            || !labels[1].StartsWith("Nokturny", StringComparison.Ordinal)
            || !labels[2].StartsWith("Radio Nowy Świat", StringComparison.Ordinal))
        {
            throw new Exception("Kolejność API nie została zachowana: " + string.Join(" | ", labels));
        }
        if (!labels[0].Contains("Sonos Radio", StringComparison.Ordinal))
        {
            throw new Exception("Nazwa usługi nie znalazła się w etykiecie: " + labels[0]);
        }
        if (!labels[1].Contains("Chopin na noc", StringComparison.Ordinal))
        {
            throw new Exception("Podany opis nie znalazł się w etykiecie: " + labels[1]);
        }
        // ROZNE ID, TA SAMA nazwa: dwie pozycje, bez identyfikatora w etykiecie.
        if (!string.Equals(labels[2], "Radio Nowy Świat", StringComparison.Ordinal))
        {
            throw new Exception("Ulubione bez usługi i opisu ma dodatkową treść: " + labels[2]);
        }
        foreach (var label in labels)
        {
            AssertNoLeak(label);
        }
        AssertNoLeakInAnnouncements(harness);

        // POCZATKOWY FOKUS na liscie, nie na przycisku Zamknij.
        if (Property<bool>(window, "ListHasFocusForTests") is false)
        {
            throw new Exception("Po otwarciu fokus nie jest na liście ulubionych.");
        }
        if (Property<string>(window, "IntroductionForTests") is { } intro
            && !intro.Contains("podgląd", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Opis okna nie mówi jasno, że to podgląd: " + intro);
        }
        // ZADNEJ drogi uruchomienia w F2.
        if (window.GetType().GetProperty("OffersPlaybackForTests", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) is true)
        {
            throw new Exception("Okno podglądu deklaruje drogę odtwarzania w F2.");
        }
        // ZAMKNIETE i POWROT: okno nie zostaje, grupy bez zmian.
        if (harness.Window.OpenSonosFavoritesWindowForTests is not null)
        {
            throw new Exception("Okno ulubionych zostało zapamiętane jako otwarte po zamknięciu.");
        }
        if (harness.Window.SonosFavoritesInFlightForTests)
        {
            throw new Exception("Bramka odczytu ulubionych nie została zwolniona.");
        }
        if (harness.MediaList.Items.Count != 1)
        {
            throw new Exception("Podgląd ulubionych podmienił listę grup Sonos.");
        }
        return 16;
    }

    // ===== F2: wlasciciel konta jest LENIWY - start programu nie budzi konta =====

    private static int MeasureOwnerStaysLazyAtStartup()
    {
        using var harness = Harness.Create();
        // Zaden odczyt ulubionych nie ma prawa pojsc sam z siebie: ani przy
        // konstrukcji okna, ani w obcej sesji.
        harness.PumpQuietly(TimeSpan.FromMilliseconds(200));
        if (harness.Backend.FavoriteReadsForTests != 0)
        {
            throw new Exception("Start programu sam wywołał odczyt ulubionych Sonos.");
        }

        var owner = new SonosAccountOwner
        {
            GatewayFactory = _ => throw new Exception("Start nie ma prawa tworzyć bramy logowania."),
            StoreFactory = _ => throw new Exception("Start nie ma prawa otwierać magazynu poświadczeń.")
        };
        using (owner)
        {
            // SAMO ISTNIENIE wlasciciela nie tworzy ani koordynatora, ani klienta.
            if (owner.CoordinatorCreations != 0 || owner.ControlApiCreations != 0)
            {
                throw new Exception("Właściciel konta Sonos obudził konto bez żądania użytkownika.");
            }
        }

        return 2;
    }

    // ===== F3: PUSTA lista to dostepny stan, nie blad i nie udawany wiersz =====

    private static int MeasureEmptyListIsAccessibleState()
    {
        using var harness = Harness.Create();
        harness.Backend.SetFavorites("W-PUSTA");
        harness.ShowOwnWindow();
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";

        var window = harness.RunFavorites();
        if (window is null) throw new Exception("Pusta lista ulubionych nie otworzyła okna podglądu.");
        if (Rows(window).Count != 0)
        {
            throw new Exception("Pusty wynik dołożył udawaną pozycję na liście.");
        }
        var intro = Property<string>(window, "IntroductionForTests") ?? string.Empty;
        if (!intro.Contains("nie ma zapisanych ulubionych", StringComparison.Ordinal))
        {
            throw new Exception("Pusty stan nie jest wyjaśniony w treści okna: " + intro);
        }
        if (harness.Announcements.Any(text =>
                text.Contains("nie udało", StringComparison.OrdinalIgnoreCase)
                || text.Contains("błąd", StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("Poprawna pusta lista została ogłoszona jako błąd.");
        }
        return 4;
    }

    // ===== F4: brak wybranego domu - uczciwe wyjasnienie i ISTNIEJACA droga =====

    private static int MeasureMissingHouseholdExplainsRecovery()
    {
        using var harness = Harness.Create();
        harness.ShowOwnWindow();
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = null;

        harness.RunFavoritesExpectingNoWindow();
        if (harness.Backend.FavoriteReadsForTests != 0)
        {
            throw new Exception("Bez wybranego domu poszedł jednak odczyt ulubionych.");
        }
        var last = harness.Announcements.LastOrDefault() ?? string.Empty;
        if (!last.Contains("Wybierz dom Sonos", StringComparison.Ordinal)
            || !last.Contains("Control F5", StringComparison.Ordinal))
        {
            throw new Exception("Brak domu nie wskazał istniejącej drogi odzyskania: " + last);
        }
        if (harness.Window.SonosFavoritesWindowsCreatedForTests != 0)
        {
            throw new Exception("Odmowa utworzyła okno podglądu.");
        }
        return 4;
    }

    // ===== F5: ODMOWA nigdy nie udaje swiezej pustej listy =====

    private static int MeasureRefusalNeverLooksLikeEmptyList()
    {
        using var harness = Harness.Create();
        harness.ShowOwnWindow();
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        harness.Backend.FailFavoritesWith = SonosDeviceReadStatus.Forbidden;

        harness.RunFavoritesExpectingNoWindow();
        if (harness.Window.SonosFavoritesWindowsCreatedForTests != 0)
        {
            throw new Exception("Odmowa 403 otworzyła okno podglądu z pustką.");
        }
        var last = harness.Announcements.LastOrDefault() ?? string.Empty;
        if (last.Contains("nie ma zapisanych ulubionych", StringComparison.Ordinal))
        {
            throw new Exception("Odmowa została opowiedziana jako pusta lista ulubionych.");
        }
        if (!string.Equals(last, SonosFavoritesReadMessages.Describe(SonosDeviceReadStatus.Forbidden),
                StringComparison.Ordinal))
        {
            throw new Exception("Odmowa nie użyła stałego bezpiecznego komunikatu: " + last);
        }
        AssertNoLeakInAnnouncements(harness);

        // LIMIT 429 tak samo: nadal nie ma okna i nadal nie ma pustej listy.
        harness.Backend.FailFavoritesWith = SonosDeviceReadStatus.RateLimited;
        harness.RunFavoritesExpectingNoWindow();
        if (harness.Window.SonosFavoritesWindowsCreatedForTests != 0)
        {
            throw new Exception("Limit 429 otworzył okno podglądu.");
        }
        if (!string.Equals(harness.Announcements.LastOrDefault(),
                SonosFavoritesReadMessages.Describe(SonosDeviceReadStatus.RateLimited), StringComparison.Ordinal))
        {
            throw new Exception("Limit 429 nie użył stałego komunikatu.");
        }
        return 6;
    }

    // ===== F6: zmiana DOMU w trakcie odczytu odrzuca spozniony wynik =====

    private static int MeasureHouseholdChangeDuringReadRejectsStaleWindow()
    {
        using var harness = Harness.Create();
        harness.ShowOwnWindow();
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        var gate = new TaskCompletionSource();
        harness.Backend.FavoritesGate = gate.Task;

        var task = harness.StartFavorites();
        harness.PumpUntil(() => harness.Backend.FavoriteReadsForTests == 1, "odczyt ulubionych nie ruszył");
        if (!harness.Window.SonosFavoritesInFlightForTests)
        {
            throw new Exception("Trwający odczyt ulubionych nie trzyma bramki.");
        }

        // ZMIANA DOMU w trakcie trwajacego GET.
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-2";
        gate.SetResult();
        harness.Pump(task);

        if (harness.Window.SonosFavoritesWindowsCreatedForTests != 0)
        {
            throw new Exception("Spóźniona lista domu DOM-1 otworzyła okno po zmianie domu na DOM-2.");
        }
        if (harness.MediaList.Items.Count != 1)
        {
            throw new Exception("Spóźniony odczyt ulubionych zmienił listę grup.");
        }
        if (harness.Window.SonosFavoritesInFlightForTests)
        {
            throw new Exception("Bramka nie została zwolniona po odrzuceniu spóźnionego wyniku.");
        }
        return 4;
    }

    // ===== F7: BUSY odmawia bez drugiego GET, a potem da sie ponowic =====

    private static int MeasureBusyGateRefusesAndFreesForRetry()
    {
        using var harness = Harness.Create();
        harness.Backend.SetFavorites("W1", ("ULU-1", "Nokturny", null, null));
        harness.ShowOwnWindow();
        harness.EnterSonosSession();
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";
        var gate = new TaskCompletionSource();
        harness.Backend.FavoritesGate = gate.Task;

        var first = harness.StartFavorites();
        harness.PumpUntil(() => harness.Backend.FavoriteReadsForTests == 1, "pierwszy odczyt nie ruszył");

        // DRUGIE wejscie w trakcie: odmowa, ZERO dodatkowego GET.
        var second = harness.StartFavorites();
        harness.Pump(second);
        if (harness.Backend.FavoriteReadsForTests != 1)
        {
            throw new Exception("Powtórne Pokaż ulubione wysłało drugi odczyt w locie.");
        }
        if (!(harness.Announcements.LastOrDefault() ?? string.Empty)
                .Contains("już trwa", StringComparison.Ordinal))
        {
            throw new Exception("Zajętość nie została uczciwie ogłoszona.");
        }

        gate.SetResult();
        harness.Backend.FavoritesGate = null;
        harness.Pump(first);
        if (harness.Window.SonosFavoritesWindowsCreatedForTests != 1)
        {
            throw new Exception("Pierwszy odczyt nie otworzył dokładnie jednego okna.");
        }

        // PONOWIENIE po powrocie: NOWY, SWIEZY odczyt. Po zamknieciu modalu
        // wracamy fokusem do okna glownego - dokladnie to robi uzytkownik.
        harness.ReactivateOwnWindow();
        var third = harness.RunFavorites();
        if (third is null) throw new Exception("Po zamknięciu nie da się ponowić Pokaż ulubione.");
        if (harness.Backend.FavoriteReadsForTests != 2)
        {
            throw new Exception(
                $"Ponowne otwarcie dało {harness.Backend.FavoriteReadsForTests} odczytów zamiast 2 (brak świeżości).");
        }
        return 5;
    }

    // ===== F8: INNE sesje bez regresji - Pokaż ulubione dziala jak dotad =====

    private static int MeasureOtherSessionKeepsOldFavoritesPath()
    {
        using var harness = Harness.Create();
        harness.ShowOwnWindow();
        // Sesja LOKALNA, nie Sonos: polecenie idzie stara droga (router),
        // NIE otwiera okna Sonos i NIE czyta ulubionych Sonosa.
        harness.ExecuteCommand(CommandIds.ViewFavorites);
        harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (harness.Backend.FavoriteReadsForTests != 0)
        {
            throw new Exception("Pokaż ulubione w obcej sesji poszło po ulubione Sonosa.");
        }
        if (harness.Window.SonosFavoritesWindowsCreatedForTests != 0)
        {
            throw new Exception("Pokaż ulubione w obcej sesji otworzyło okno Sonosa.");
        }
        if (harness.Window.LastSonosFavoritesTaskForTests is not null)
        {
            throw new Exception("Obca sesja rozpoczęła zadanie ulubionych Sonos.");
        }
        return 3;
    }

    // ===== F9: WYJSCIE Z SESJI w trakcie odczytu A nie blokuje nowego odczytu B =====

    /// <summary>
    /// NAKLADAJACE SIE PRZELOTY A i B przez PRAWDZIWA zmiane sesji.
    ///
    /// Droga uzytkownika: trwa odczyt ulubionych A, uzytkownik wychodzi z sesji
    /// Sonos istniejacym skrotem slotu, wraca do niej i JESZCZE PRZED koncem A
    /// prosi o ulubione po raz drugi. Porzucony A NIE MA prawa odbic tego B
    /// komunikatem "juz trwa", a jego SPOZNIONE finally NIE MA prawa zwolnic
    /// bramki trwajacego B.
    ///
    /// Modal B jest tu PRAWDZIWY (<c>ShowDialog</c> z produkcyjnym wlascicielem),
    /// a zamyka go rzeczywisty przycisk Zamknij (<c>IsCancel</c>).
    /// </summary>
    private static int MeasureLeavingSessionFreesFavoritesGateForNewRead()
    {
        using var harness = Harness.Create();
        harness.Backend.SetFavorites("W1", ("ULU-1", "Nokturny", null, null));
        harness.ShowOwnWindow();
        // SLOTY z RZECZYWISTEGO SessionManager, nie zgadniete z kolejnosci nazw.
        var sonosSlot = harness.FindSlot("sonos");
        var otherSlot = harness.FindSlot("local");
        harness.ExecuteCommand(CommandIds.SessionSlot(sonosSlot));
        harness.PumpUntil(() => harness.MediaList.Items.Count == 1, "sesja Sonos nie pokazała grupy");
        harness.Window.StateForTests.Sonos.SelectedHouseholdId = "DOM-1";

        var gateA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? readA = null;
        Task? readB = null;
        harness.UseRealModalPresentation();
        try
        {
            // A: odczyt WSTRZYMANY na zapleczu.
            harness.Backend.FavoritesGate = gateA.Task;
            readA = harness.StartFavoritesKeepingPresentation();
            harness.PumpUntil(() => harness.Backend.FavoriteReadsForTests == 1, "odczyt A nie ruszył");
            if (readA.IsCompleted) throw new Exception("Odczyt A nie jest wstrzymany, pomiar nic nie mierzy.");
            if (!harness.Window.SonosFavoritesInFlightForTests)
            {
                throw new Exception("Trwający odczyt A nie trzyma bramki ulubionych.");
            }

            // PRAWDZIWE wyjscie z sesji i PRAWDZIWY powrot - nie bezposrednie
            // wywolanie anulowania.
            harness.ExecuteCommand(CommandIds.SessionSlot(otherSlot));
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));
            if (harness.IsSonosCurrent) throw new Exception("Polecenie slotu nie wyszło z sesji Sonos.");
            harness.ExecuteCommand(CommandIds.SessionSlot(sonosSlot));
            harness.PumpUntil(() => harness.MediaList.Items.Count == 1, "powrót do Sonosa nie pokazał grupy");
            if (!harness.IsSonosCurrent) throw new Exception("Powrót do sesji Sonos nie nastąpił.");
            if (!string.Equals(harness.Window.StateForTests.Sonos.SelectedHouseholdId, "DOM-1",
                    StringComparison.Ordinal))
            {
                throw new Exception("Powrót do sesji zgubił wybrany dom, pomiar nie dotarłby do odczytu.");
            }

            harness.ReactivateOwnWindow();

            // B: NOWE, JAWNE zadanie uzytkownika PRZED koncem A, na WLASNEJ
            // barierze zaplecza.
            harness.Backend.FavoritesGate = gateB.Task;
            readB = harness.StartFavoritesKeepingPresentation();
            harness.PumpUntil(() => harness.Backend.FavoriteReadsForTests == 2,
                "porzucony odczyt A zablokował nowy odczyt B po wyjściu z sesji i powrocie "
                + "(ostatni komunikat: " + (harness.Announcements.LastOrDefault() ?? "brak") + ")");
            if (readB.IsCompleted) throw new Exception("Odczyt B zakończył się, choć jego bariera trzyma.");
            if (readA.IsCompleted) throw new Exception("Odczyt A zakończył się przed zwolnieniem swojej bariery.");

            // ZWOLNIENIE SPOZNIONEGO A, gdy B NADAL trwa.
            gateA.TrySetResult();
            harness.Pump(readA);
            if (harness.Window.SonosFavoritesWindowsCreatedForTests != 0)
            {
                throw new Exception("Spóźniony odczyt A otworzył okno po wyjściu z sesji i powrocie.");
            }
            if (harness.Announcements.Any(text =>
                    text.Contains("Ulubione Sonos: ", StringComparison.Ordinal)
                    || string.Equals(text, SonosFavoritesLabels.EmptyState, StringComparison.Ordinal)))
            {
                throw new Exception("Spóźniony odczyt A oddał czytnikowi swój wynik: "
                    + string.Join(" | ", harness.Announcements));
            }
            if (!harness.Window.SonosFavoritesInFlightForTests)
            {
                throw new Exception("Spóźnione zakończenie A zwolniło bramkę trwającego odczytu B.");
            }
            if (readB.IsCompleted) throw new Exception("Odczyt B zakończył się razem z A.");

            // C: PONOWIENIE w trakcie B - uczciwa odmowa, ZERO trzeciego GET.
            var refusedC = harness.StartFavoritesKeepingPresentation();
            harness.Pump(refusedC);
            if (harness.Backend.FavoriteReadsForTests != 2)
            {
                throw new Exception(
                    $"Ponowienie w trakcie B dało {harness.Backend.FavoriteReadsForTests} odczytów zamiast 2.");
            }
            if (!(harness.Announcements.LastOrDefault() ?? string.Empty)
                    .Contains("już trwa", StringComparison.Ordinal))
            {
                throw new Exception("Zajętość trwającego B nie została uczciwie ogłoszona.");
            }

            // ZWOLNIENIE B: DOKLADNIE JEDEN prawdziwy modal, zamkniety
            // rzeczywistym przyciskiem Zamknij.
            gateB.TrySetResult();
            harness.Backend.FavoritesGate = null;
            harness.Pump(readB);
            if (harness.RealModalPresentations != 1)
            {
                throw new Exception(
                    $"Odczyt B pokazał {harness.RealModalPresentations} prawdziwych modali zamiast 1.");
            }
            if (harness.Window.SonosFavoritesWindowsCreatedForTests != 1)
            {
                throw new Exception(
                    $"Powstało {harness.Window.SonosFavoritesWindowsCreatedForTests} okien zamiast 1.");
            }
            if (harness.LastModalDialogResult is not false)
            {
                throw new Exception("Modal B nie został zamknięty rzeczywistym przyciskiem Zamknij.");
            }
            if (harness.Window.OpenSonosFavoritesWindowForTests is not null)
            {
                throw new Exception("Okno B zostało zapamiętane jako otwarte po zamknięciu.");
            }
            if (harness.Window.SonosFavoritesInFlightForTests)
            {
                throw new Exception("Bramka ulubionych nie została zwolniona po zakończeniu B.");
            }

            // JAWNE PONOWIENIE po wszystkim: NOWY, SWIEZY odczyt i drugi modal.
            harness.ReactivateOwnWindow();
            var readD = harness.StartFavoritesKeepingPresentation();
            harness.Pump(readD);
            if (harness.Backend.FavoriteReadsForTests != 3)
            {
                throw new Exception(
                    $"Ponowienie po B dało {harness.Backend.FavoriteReadsForTests} odczytów zamiast 3.");
            }
            if (harness.RealModalPresentations != 2)
            {
                throw new Exception("Ponowienie po B nie pokazało własnego prawdziwego modalu.");
            }
            if (harness.Window.SonosFavoritesInFlightForTests)
            {
                throw new Exception("Bramka ulubionych nie została zwolniona po ponowieniu.");
            }

            // ZADNEGO POST i ZADNEJ podmiany listy grup przez caly pomiar.
            if (harness.Backend.Commands.Count != 0)
            {
                throw new Exception("Podgląd ulubionych wysłał polecenie do Sonosa.");
            }
            if (harness.MediaList.Items.Count != 1)
            {
                throw new Exception("Nakładające się odczyty ulubionych podmieniły listę grup Sonos.");
            }
            AssertNoLeakInAnnouncements(harness);
            return 12;
        }
        finally
        {
            // ZAWSZE zwalniamy obie bariery i domykamy oba przeloty, zeby
            // negatywne wyjscie nie zostawilo okna ani wiszacego zadania.
            gateA.TrySetResult();
            gateB.TrySetResult();
            harness.Backend.FavoritesGate = null;
            harness.DrainQuietly(readA);
            harness.DrainQuietly(readB);
            harness.ClearPresentation();
        }
    }

    // ==================== pomocnicze asercje ====================

    private static IReadOnlyList<string> Rows(object window) =>
        Property<IReadOnlyList<string>>(window, "RowLabelsForTests") ?? [];

    private static T? Property<T>(object instance, string name) =>
        instance.GetType().GetProperty(name, Instance)?.GetValue(instance) is T value ? value : default;

    /// <summary>
    /// ZERO WYCIEKU: ani identyfikatora ulubionego, ani identyfikatora usługi,
    /// ani identyfikatora domu, ani nazwy typu .NET.
    /// </summary>
    private static void AssertNoLeak(string text)
    {
        foreach (var forbidden in new[] { "ULU-", "DOM-", "SVC-", "Sonos.Sonos", "SonosFavorite", "Kind" })
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                throw new Exception("Wyciek do etykiety/komunikatu (\"" + forbidden + "\"): " + text);
            }
        }
    }

    private static void AssertNoLeakInAnnouncements(Harness harness)
    {
        foreach (var text in harness.Announcements)
        {
            AssertNoLeak(text);
        }
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

        internal static Harness Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-favorites-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var settingsPath = Path.Combine(directory, "settings.json");
            var store = new ConfigurationStore(settingsPath);
            var state = store.LoadOrCreate();
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
            return new Harness(directory, window, backend, announcements);
        }

        /// <summary>
        /// POKAZANIE prawdziwego okna glownego - PRODUKCYJNY guard wymaga okna
        /// widocznego i AKTYWNEGO, wiec pomiar musi je naprawde pokazac.
        /// Produkcyjny ContentRendered odpinamy jak w istniejacych pomiarach
        /// Sonosa, zeby nie obudzic uslug pulpitu zainstalowanego AMC.
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
            foreach (var field in new[] { "_nvdaCommandServer", "_prefixService" })
            {
                if (Window.GetType().GetField(field, Instance)?.GetValue(Window) is not null)
                {
                    throw new Exception("Fixture wystartował produkcyjną usługę pulpitu: " + field);
                }
            }

            Window.Activate();
            PumpUntil(() => Window.IsActive, "własne okno główne nie stało się aktywne");
            MediaList.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(80));
        }

        /// <summary>
        /// NUMER SLOTU z RZECZYWISTEGO <see cref="SessionManager"/> okna, nie
        /// zgadniety z kolejnosci nazw: numery slotow to skroty uzytkownika.
        /// </summary>
        internal int FindSlot(string sessionId) =>
            Sessions.FindSlot(sessionId)
            ?? throw new Exception("Konfiguracja nie ma slotu sesji \"" + sessionId + "\".");

        internal bool IsSonosCurrent =>
            string.Equals(Sessions.Current.Id, "sonos", StringComparison.Ordinal);

        private SessionManager Sessions =>
            (SessionManager)(Window.GetType().GetField("_sessions", Instance)?.GetValue(Window)
                ?? throw new Exception("MainWindow nie ma pola _sessions."));

        /// <summary>Ile razy poszlo PRAWDZIWE modalne <c>ShowDialog</c>.</summary>
        internal int RealModalPresentations { get; private set; }

        /// <summary>Wynik OSTATNIEGO prawdziwego modalu po jego zamknieciu.</summary>
        internal bool? LastModalDialogResult { get; private set; }

        /// <summary>
        /// PRAWDZIWE POKAZANIE MODALU: punkt podstawienia uzbraja zamkniecie i
        /// wola <c>ShowDialog</c> - ten sam wlasciciel i ten sam modalny tor, co
        /// w produkcji. Zamyka RZECZYWISTY przycisk Zamknij (<c>IsCancel</c>)
        /// przez jego wlasny handler; klawisze zywego czytnika to osobny pomiar.
        /// </summary>
        internal void UseRealModalPresentation()
        {
            PresentOverride.SetValue(Window, MakeRealModalHandler());
        }

        internal void ClearPresentation() => PresentOverride.SetValue(Window, null);

        private Delegate MakeRealModalHandler()
        {
            var windowType = typeof(MainWindow).Assembly.GetType(
                "AccessibleMediaController.Windows.SonosFavoritesWindow")
                ?? throw new Exception("Nie ma okna SonosFavoritesWindow.");
            return Delegate.CreateDelegate(
                typeof(Action<>).MakeGenericType(windowType),
                new RealModalPresenter(this),
                typeof(RealModalPresenter).GetMethod(nameof(RealModalPresenter.Present), Instance)!);
        }

        private sealed class RealModalPresenter(Harness harness)
        {
            internal void Present(object dialog)
            {
                var window = (Window)dialog;
                window.ShowInTaskbar = false;
                // ZAMKNIECIE uzbrajamy PRZED modalem: ShowDialog zatrzymuje ten
                // watek na wlasnej petli komunikatow, a timer dziala w niej.
                var closer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(25)
                };
                closer.Tick += (_, _) =>
                {
                    if (!window.IsLoaded || !window.IsVisible) return;
                    closer.Stop();
                    var close = (Button?)window.FindName("CloseButton")
                        ?? throw new Exception("Okno ulubionych nie ma przycisku Zamknij.");
                    close.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                };
                closer.Start();
                harness.RealModalPresentations++;
                try
                {
                    window.ShowDialog();
                }
                finally
                {
                    closer.Stop();
                    harness.LastModalDialogResult = window.DialogResult;
                }
            }
        }

        /// <summary>
        /// ROZPOCZECIE bez czekania i BEZ ruszania wpietego punktu podstawienia:
        /// nakladajace sie przeloty musza dzielic ten sam sposob pokazania.
        /// </summary>
        internal Task StartFavoritesKeepingPresentation()
        {
            ExecuteCommand(CommandIds.ViewFavorites);
            return FavoritesTask
                ?? throw new Exception("Polecenie nie rozpoczęło zadania odczytu ulubionych Sonos.");
        }

        /// <summary>DOMKNIECIE przelotu w finally: bez asercji i bez rzucania.</summary>
        internal void DrainQuietly(Task? task)
        {
            if (task is null) return;
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted && DateTime.UtcNow < deadline) DoEvents();
        }

        /// <summary>POWROT do okna glownego po zamknietym modalu.</summary>
        internal void ReactivateOwnWindow()
        {
            Window.Activate();
            PumpUntil(() => Window.IsActive, "własne okno główne nie wróciło do aktywności");
        }

        internal void EnterSonosSession()
        {
            ExecuteCommand(CommandIds.SessionSlot(8));
            PumpQuietly(TimeSpan.FromMilliseconds(120));
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
        /// PRAWDZIWA droga: istniejace polecenie "Pokaż ulubione". Punkt
        /// podstawienia POKAZANIA okna i samo okno siegane REFLEKSJA, bo w bazie
        /// jeszcze nie istnieja - to jest wlasnie mierzone wejscie.
        /// </summary>
        internal object? RunFavorites()
        {
            object? captured = null;
            RunFavoritesCore(dialog => captured = dialog, present: true);
            return captured;
        }

        /// <summary>
        /// Droga, w ktorej okno NIE MA prawa powstac. Punkt podstawienia i tak
        /// jest wpiety: gdyby produkcja jednak okno pokazala, pomiar to zlapie.
        /// </summary>
        internal void RunFavoritesExpectingNoWindow()
        {
            RunFavoritesCore(
                dialog => throw new Exception("Otwarto okno ulubionych Sonos, choć nie było do tego prawa."),
                present: false);
        }

        private void RunFavoritesCore(Action<object> onPresent, bool present)
        {
            var handler = MakeHandler(onPresent, present);
            PresentOverride.SetValue(Window, handler);
            try
            {
                ExecuteCommand(CommandIds.ViewFavorites);
                if (FavoritesTask is { } task)
                {
                    Pump(task);
                }
                else
                {
                    throw new Exception("Polecenie nie rozpoczęło zadania odczytu ulubionych Sonos.");
                }
            }
            finally
            {
                PresentOverride.SetValue(Window, null);
            }
        }

        /// <summary>
        /// ROZPOCZECIE bez czekania: potrzebne do pomiaru wstrzymanego GET.
        /// Punkt podstawienia zostaje wpiety na czas calego przelotu.
        /// </summary>
        internal Task StartFavorites()
        {
            PresentOverride.SetValue(Window, MakeHandler(_ => { }, present: true));
            ExecuteCommand(CommandIds.ViewFavorites);
            return FavoritesTask
                ?? throw new Exception("Polecenie nie rozpoczęło zadania odczytu ulubionych Sonos.");
        }

        private PropertyInfo PresentOverride =>
            Window.GetType().GetProperty("PresentSonosFavoritesOverrideForTests", Instance)
            ?? throw new Exception(
                "MainWindow nie ma punktu podstawienia pokazania okna ulubionych Sonos "
                + "(PresentSonosFavoritesOverrideForTests).");

        private Task? FavoritesTask =>
            Window.GetType().GetProperty("LastSonosFavoritesTaskForTests", Instance)?.GetValue(Window) as Task;

        private Delegate MakeHandler(Action<object> capture, bool present)
        {
            var windowType = typeof(MainWindow).Assembly.GetType(
                "AccessibleMediaController.Windows.SonosFavoritesWindow")
                ?? throw new Exception("Nie ma okna SonosFavoritesWindow.");
            var handlerType = typeof(Action<>).MakeGenericType(windowType);
            return Delegate.CreateDelegate(
                handlerType,
                new Captor(this, capture, present),
                typeof(Captor).GetMethod(nameof(Captor.Present), Instance)!);
        }

        /// <summary>
        /// ADAPTER pokazania: pokazujemy okno przez Show i pompujemy petle, bo
        /// ShowDialog zablokowalby watek pomiaru. To ten sam typ, ten sam XAML.
        /// </summary>
        private sealed class Captor(Harness harness, Action<object> capture, bool present)
        {
            internal void Present(object dialog)
            {
                capture(dialog);
                if (!present) return;
                var window = (Window)dialog;
                window.ShowInTaskbar = false;
                window.Show();
                harness.PumpUntil(() => window.IsLoaded, "okno ulubionych Sonos się nie pokazało");
                window.Close();
                harness.PumpUntil(() => !window.IsVisible, "okno ulubionych Sonos się nie zamknęło");
            }
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
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: zadanie ulubionych Sonos.");
                DoEvents();
            }

            task.GetAwaiter().GetResult();
        }

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        public void Dispose()
        {
            try
            {
                Window.Close();
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// SYNTETYCZNE zaplecze sesji: grupy jak w istniejacych pomiarach Sonosa,
    /// plus LICZNIK odczytow ULUBIONYCH. Odczyt ulubionych jest tu wolany
    /// REFLEKSJA przez produkt (nowa, opcjonalna granica), wiec licznik rosnie
    /// tylko wtedy, gdy produkt NAPRAWDE po niego poszedl.
    /// </summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend, ISonosFavoritesSessionBackend
    {
        private (string Id, string Name)[] _households = [("DOM-1", "Dom")];

        private (string Id, string Name, string? Service, string? Description)[] _favorites =
        [
            ("ULU-1", "Nokturny", null, null)
        ];

        private string _favoritesVersion = "W1";

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal int FavoriteReadsForTests { get; private set; }

        internal string? LastFavoritesHouseholdId { get; private set; }

        /// <summary>WSTRZYMANIE odczytu ULUBIONYCH: pomiar spoznionego wyniku.</summary>
        internal Task? FavoritesGate { get; set; }

        internal SonosDeviceReadStatus? FailFavoritesWith { get; set; }

        internal void SetHouseholds(params (string Id, string Name)[] households) => _households = households;

        internal void SetFavorites(
            string version,
            params (string Id, string Name, string? Service, string? Description)[] favorites)
        {
            _favoritesVersion = version;
            _favorites = favorites;
        }

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        /// <summary>
        /// ODCZYT ULUBIONYCH przez OPCJONALNA granice sesji. Syntetyczne dane,
        /// zero HTTP, zero tokenu. Status niepowodzenia da sie wymusic.
        /// </summary>
        public Task<SonosFavoritesReadResult> ReadFavoritesAsync(
            string? householdId, CancellationToken cancellationToken)
        {
            FavoriteReadsForTests++;
            LastFavoritesHouseholdId = householdId;
            return ReadFavoritesCoreAsync(householdId);
        }

        private async Task<SonosFavoritesReadResult> ReadFavoritesCoreAsync(string? householdId)
        {
            if (FavoritesGate is { } gate) await gate.ConfigureAwait(true);
            if (FailFavoritesWith is { } status)
            {
                return SonosFavoritesReadResult.CreateForMeasurement(status, null);
            }

            var list = new SonosFavoritesList(
                householdId ?? "DOM-1",
                _favoritesVersion,
                _favorites
                    .Select(item => new SonosFavorite(
                        item.Id,
                        item.Name,
                        item.Description,
                        item.Service is null ? null : new SonosFavoriteService(item.Service, "SVC-9")))
                    .ToArray());
            return SonosFavoritesReadResult.CreateForMeasurement(SonosDeviceReadStatus.Success, list);
        }

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

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosHouseholdsReadResult.Success(
                _households.Select(home => new SonosHousehold(home.Id, home.Name, null)).ToArray()));

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Idle)],
                [new SonosPlayer("P1", "Salon", null, null, null)],
                false)));
    }
}
