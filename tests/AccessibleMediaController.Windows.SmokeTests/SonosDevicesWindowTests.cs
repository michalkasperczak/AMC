using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// LISTA GLOSNIKOW I GRUP SONOS mierzona na PRAWDZIWYCH kontrolkach WPF, BEZ
/// pokazywania okna.
///
/// Co mierzy prawdziwy kod produkcyjny:
///   * droga Konto Sonos -> "Głośniki i grupy…" -> okno listy, z callbackami od
///     WLASCICIELA konta (okno nie widzi koordynatora ani tokenow),
///   * przycisk dostepny przy dzialajacym koncie TAKZE przy nieudanym zapisie do
///     magazynu, a niedostepny bez poswiadczen,
///   * etykiety POLSKIE: nazwy bez identyfikatorow, dom bez nazwy jako "Dom
///     Sonos 1", stan odtwarzania po polsku,
///   * jeden dom jest naturalnie domyslny (bez listy wyboru), wiele domow daje
///     liste wyboru,
///   * pusty dom, lista NIEPELNA, blad i anulowanie NIE sa ciche,
///   * ZERO sterowania odtwarzaniem: zaden przycisk i zadne Enter/strzalki,
///   * jedna operacja w toku; przy zajetosci przyciski sa WYLACZONE, nie
///     schowane - ale SAMO wylaczenie tez gubi fokus, wiec osobno mierzymy, ze
///     fokus jest RATOWANY przed wylaczeniem i wraca po wyniku, takze z
///     PRAWDZIWEGO WIERSZA obu list (zmierzone zywym NVDA: z wiersza czytnik
///     czytal caly dialog),
///   * wynik SPOZNIONY po zmianie domu i po zamknieciu NIE podmienia listy,
///   * zamkniecie okna anuluje WLASNY odczyt i NIE zwalnia zasobow wlasciciela,
///   * ponowne otwarcie dziala, a guard nie tworzy drugiego okna.
///
/// SCISLA BRAMKA PULPITU: zero Show, ShowDialog, Activate, EnsureHandle i zero
/// NVDA. Okna sa KONSTRUOWANE na watku STA, pokazanie zastapione testowym
/// odpowiednikiem.
///
/// Czego NIE dowodzi: nie jest pomiarem zywej mowy czytnika ekranu ani
/// pierwszego fokusu w POKAZANYM oknie - to osobna czynnosc wlasciciela pulpitu.
/// Nie dotyka tez prawdziwego konta Sonos ani sieci.
/// </summary>
internal static class SonosDevicesWindowTests
{
    private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                // WPF przywraca kontynuacje przez kontekst dispatchera. Bez tego
                // ustawienia kontynuacja wrocilaby na watek puli i pomiar
                // wywracalby sie na dostepie miedzywatkowym zamiast mierzyc okno.
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                checks += MeasureLabels();
                checks += MeasureSingleHouseholdAndRefresh();
                checks += MeasureNoPlaybackControls();
                checks += MeasureBusyKeepsButtonsVisible();
                checks += MeasureBusyKeepsFocusUsable();
                checks += MeasureBusyKeepsRealRowFocus();
                checks += MeasureLoadingAnnouncedOnce();
                checks += MeasureRefreshKeepsHouseholdById();
                checks += MeasureStaleResultIgnored();
                checks += MeasureCloseCancelsOwnWorkOnly();
                checks += MeasureAccountButtonWiring();
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

        Console.WriteLine(
            "OK: lista głośników i grup Sonos - polskie etykiety bez identyfikatorów, jeden dom domyślny, "
            + "brak sterowania odtwarzaniem, spóźniony wynik odrzucony, zamknięcie bez zwalniania zasobów właściciela "
            + $"({checks} sprawdzeń, bez pokazywania GUI)");
    }

    // ================= 1. etykiety polskie =================

    private static int MeasureLabels()
    {
        var named = new SonosHousehold("Sonos_1.a", "Dom na Kwiatowej", null);
        var unnamed = new SonosHousehold("Sonos_2.b", null, null);

        if (SonosDeviceLabels.DescribeHousehold(named, 1) != "Dom na Kwiatowej")
        {
            throw new Exception("Nazwany dom nie jest opisany swoją nazwą.");
        }

        var fallback = SonosDeviceLabels.DescribeHousehold(unnamed, 1);
        if (fallback != "Dom Sonos 1")
        {
            throw new Exception($"Dom bez nazwy opisany jako \"{fallback}\" zamiast \"Dom Sonos 1\".");
        }
        if (fallback.Contains("Sonos_2.b", StringComparison.Ordinal))
        {
            throw new Exception("Etykieta domu zawiera identyfikator.");
        }

        // Dwa nienazwane domy NIE zlewaja sie w jedna nazwe.
        var choices = SonosDeviceLabels.DescribeHouseholdChoices(new[] { unnamed, new SonosHousehold("Sonos_3.c", null, null) });
        if (choices.Count != 2 || choices[0] == choices[1] || choices[1] != "Dom Sonos 2")
        {
            throw new Exception("Dwa domy bez nazwy dostały tę samą etykietę.");
        }

        var topology = Topology();
        var groupLabel = SonosDeviceLabels.DescribeGroup(topology.Groups[0]);
        if (!groupLabel.StartsWith("Salon", StringComparison.Ordinal)
            || !groupLabel.Contains("2 głośniki", StringComparison.Ordinal)
            || !groupLabel.Contains("odtwarza", StringComparison.Ordinal))
        {
            throw new Exception($"Etykieta grupy po polsku nie zgadza się: \"{groupLabel}\".");
        }
        if (groupLabel.Contains("RINCON", StringComparison.OrdinalIgnoreCase)
            || groupLabel.Contains("PLAYING", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Etykieta grupy pokazuje identyfikator albo angielski stan.");
        }

        var coordinatorLabel = SonosDeviceLabels.DescribePlayer(topology.Players[0], topology);
        if (!coordinatorLabel.Contains("w grupie Salon", StringComparison.Ordinal)
            || !coordinatorLabel.Contains("prowadzi grupę", StringComparison.Ordinal))
        {
            throw new Exception($"Głośnik prowadzący grupę nie jest tak opisany: \"{coordinatorLabel}\".");
        }

        var lonely = SonosDeviceLabels.DescribePlayer(new SonosPlayer("RINCON_X", "Garaż", null, null, null), topology);
        if (!lonely.Contains("poza grupami", StringComparison.Ordinal))
        {
            throw new Exception("Głośnik bez grupy nie ma jawnego opisu.");
        }

        if (SonosDeviceLabels.DescribePlaybackState(SonosPlaybackState.Unknown) != SonosDeviceLabels.PlaybackUnknown
            || SonosDeviceLabels.DescribePlaybackState(SonosPlaybackState.Paused) != "wstrzymane")
        {
            throw new Exception("Stan odtwarzania nie jest po polsku.");
        }

        return 8;
    }

    // ================= 2. jeden dom domyslny + Odswiez =================

    private static int MeasureSingleHouseholdAndRefresh()
    {
        var reader = new LiczacyOdczyt();
        var window = Build(reader);
        Pump(window.LoadAsync());

        if (reader.HouseholdCalls != 1 || reader.GroupCalls != 1)
        {
            throw new Exception(
                $"Jeden dom nie został wybrany naturalnie (domy {reader.HouseholdCalls}, grupy {reader.GroupCalls}).");
        }
        if (window.IsHouseholdChoiceVisible)
        {
            throw new Exception("Przy jednym domu pokazuje się lista wyboru domu.");
        }
        if (window.HouseholdChoices.Count != 1 || window.HouseholdChoices[0] != "Dom na Kwiatowej")
        {
            throw new Exception("Lista domów nie zawiera nazwy jedynego domu.");
        }
        if (window.GroupLabels.Count != 1 || window.PlayerLabels.Count != 2)
        {
            throw new Exception("Lista grup i głośników nie została zbudowana.");
        }
        if (!window.InstructionText.StartsWith(SonosDeviceLabels.ViewIntroduction, StringComparison.Ordinal))
        {
            throw new Exception("Pierwsza treść okna nie mówi, że widok nie zmienia odtwarzania.");
        }
        if (!window.LastAnnouncement.Contains("NIEPEŁNA", StringComparison.Ordinal))
        {
            throw new Exception("Lista NIEPEŁNA (partial) nie została zgłoszona.");
        }

        // ODSWIEZ: ta sama droga co przycisk, znowu domy i grupy.
        Pump(window.InvokeRefreshAsync());
        if (reader.HouseholdCalls != 2 || reader.GroupCalls != 2)
        {
            throw new Exception("Odśwież nie powtórzyło odczytu domów i grup.");
        }

        // PUSTY DOM mowi to wprost.
        reader.Topology = new SonosHouseholdTopology(Array.Empty<SonosGroup>(), Array.Empty<SonosPlayer>(), false);
        Pump(window.InvokeRefreshAsync());
        if (window.GroupLabels.Count != 0 || !window.LastAnnouncement.Contains("nie ma podłączonych głośników", StringComparison.Ordinal))
        {
            throw new Exception($"Pusty dom nie jest zgłoszony jawnie: \"{window.LastAnnouncement}\".");
        }

        // BLAD nie jest cichy i nie udaje pustej listy.
        reader.GroupFailure = "Sonos odmówił dostępu do tego domu.";
        var before = window.AnnouncementCount;
        Pump(window.InvokeRefreshAsync());
        if (window.AnnouncementCount <= before
            || !window.LastAnnouncement.Contains("odmówił", StringComparison.Ordinal))
        {
            throw new Exception("Błąd odczytu grup był cichy.");
        }

        // WIELE DOMOW: lista wyboru sie pokazuje.
        reader.GroupFailure = null;
        reader.Households = new[]
        {
            new SonosHousehold("Sonos_1.a", "Dom na Kwiatowej", null),
            new SonosHousehold("Sonos_2.b", null, null)
        };
        Pump(window.InvokeRefreshAsync());
        if (!window.IsHouseholdChoiceVisible || window.HouseholdChoices.Count != 2
            || window.HouseholdChoices[1] != "Dom Sonos 2")
        {
            throw new Exception("Przy wielu domach brak listy wyboru z nazwami zastępczymi.");
        }

        // ZMIANA DOMU czyta grupy tego domu - i tylko jego.
        var groupsBefore = reader.GroupCalls;
        Pump(window.InvokeSelectHouseholdAsync(1));
        if (reader.GroupCalls != groupsBefore + 1 || reader.LastHouseholdId != "Sonos_2.b")
        {
            throw new Exception("Zmiana domu nie odczytała grup wybranego domu.");
        }

        window.ShutdownOwnWork();
        return 8;
    }

    // ================= 3. zero sterowania odtwarzaniem =================

    private static int MeasureNoPlaybackControls()
    {
        var window = Build(new LiczacyOdczyt());
        Pump(window.LoadAsync());

        // Dokladnie DWIE operacje: Odswiez i Zamknij. Nic wiecej.
        var buttons = Descendants<Button>(window).ToList();
        if (buttons.Count != 2)
        {
            throw new Exception(
                "Okno listy ma " + buttons.Count.ToString() + " przycisków zamiast wyłącznie Odśwież i Zamknij: "
                + string.Join(", ", buttons.Select(button => button.Content as string ?? "?")));
        }

        foreach (var forbidden in new[] { "Odtwórz", "Odtwarzaj", "Pauza", "Wstrzymaj", "Stop", "Następny", "Poprzedni", "Głośność" })
        {
            if (buttons.Any(button => (button.Content as string ?? string.Empty).Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception($"Okno listy ma przycisk sterowania odtwarzaniem: {forbidden}.");
            }
        }

        // Listy NIE maja wlasnej obslugi aktywacji - Enter i dwuklik niczego nie uruchamiaja.
        foreach (var list in Descendants<ListBox>(window))
        {
            if (GetEventHandlerCount(list, "MouseDoubleClick") != 0
                || GetEventHandlerCount(list, "KeyDown") != 0
                || GetEventHandlerCount(list, "SelectionChanged") != 0)
            {
                throw new Exception("Lista urządzeń ma podpiętą aktywację elementu - może coś uruchomić.");
            }
        }

        window.ShutdownOwnWork();
        return 3;
    }

    // ================= 4. zajetosc nie chowa kontrolek =================

    private static int MeasureBusyKeepsButtonsVisible()
    {
        var gate = new ManualResetEventSlim(false);
        var reached = new ManualResetEventSlim(false);
        var reader = new LiczacyOdczyt
        {
            BeforeHouseholds = () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(5));
            }
        };

        var window = Build(reader);
        var task = window.LoadAsync();
        PumpUntil(() => reached.IsSet);

        var refreshButton = (Button)window.FindName("RefreshButton")!;
        var closeButton = (Button)window.FindName("CloseButton")!;
        if (refreshButton.Visibility != Visibility.Visible || closeButton.Visibility != Visibility.Visible)
        {
            throw new Exception("Zajętość SCHOWALA przycisk - fokus może uciec spod czytnika ekranu.");
        }
        if (refreshButton.IsEnabled)
        {
            throw new Exception("Przy zajętości Odśwież jest nadal włączony - można wysłać drugą operację.");
        }
        if (!closeButton.IsEnabled)
        {
            throw new Exception("Przy zajętości nie da się zamknąć okna.");
        }

        // DRUGA operacja w trakcie pierwszej NIE startuje.
        Pump(window.InvokeRefreshAsync());
        if (reader.HouseholdCalls > 1)
        {
            throw new Exception("Druga operacja wystartowała w trakcie pierwszej.");
        }

        gate.Set();
        Pump(task);
        if (!refreshButton.IsEnabled)
        {
            throw new Exception("Po zakończeniu operacji Odśwież pozostał wyłączony.");
        }

        window.ShutdownOwnWork();
        return 4;
    }

    // ================= 4b. zajetosc NIE GUBI fokusu =================

    /// <summary>
    /// ZYWY NVDA rodzica pokazal, ze po odswiezeniu fokus byl PUSTY, a czytnik
    /// czytal caly dialog. Powod: wylaczenie skupionego przycisku oddaje fokus
    /// oknu. Tu mierzymy, ze fokus zostaje na uzywalnej kontrolce w czasie
    /// czekania i WRACA na przycisk po zakonczeniu.
    /// </summary>
    private static int MeasureBusyKeepsFocusUsable()
    {
        var gate = new ManualResetEventSlim(false);
        var reached = new ManualResetEventSlim(false);
        var reader = new LiczacyOdczyt
        {
            BeforeHouseholds = () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(5));
            }
        };

        var window = Build(reader);
        Pump(window.LoadAsync());

        var refreshButton = (Button)window.FindName("RefreshButton")!;
        var closeButton = (Button)window.FindName("CloseButton")!;
        var instruction = (TextBox)window.FindName("InstructionBox")!;

        // Uzytkownik stoi na Odswiez i go uruchamia - jak przy zywym pomiarze.
        System.Windows.Input.FocusManager.SetFocusedElement(window, refreshButton);
        reached.Reset();
        gate.Reset();
        var task = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);

        var busyFocus = window.FocusedControlName;
        if (busyFocus.Length == 0)
        {
            throw new Exception("W czasie odświeżania fokus jest PUSTY - czytnik czyta cały dialog od nowa.");
        }
        if (busyFocus != "InstructionBox")
        {
            throw new Exception($"W czasie odświeżania fokus trafił na \"{busyFocus}\" zamiast na czytelną instrukcję.");
        }
        if (!instruction.IsEnabled || !instruction.IsReadOnly)
        {
            throw new Exception("Pole instrukcji musi zostać włączone i tylko do odczytu.");
        }

        gate.Set();
        Pump(task);
        if (window.FocusedControlName != "RefreshButton")
        {
            throw new Exception($"Po odświeżeniu fokus nie wrócił na Odśwież (jest \"{window.FocusedControlName}\").");
        }

        // Gdy uzytkownik sam przeszedl na Zamknij, okno NIE kradnie mu fokusu.
        reached.Reset();
        gate.Reset();
        var second = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);
        System.Windows.Input.FocusManager.SetFocusedElement(window, closeButton);
        gate.Set();
        Pump(second);
        if (window.FocusedControlName != "CloseButton")
        {
            throw new Exception("Okno ukradło fokus z przycisku Zamknij wybranego przez użytkownika.");
        }

        window.ShutdownOwnWork();
        return 5;
    }

    // ================= 4bb. fokus z PRAWDZIWEGO WIERSZA listy =================

    /// <summary>
    /// ZYWY NVDA rodzica: Tab na wiersz grupy (rola 15, ListBoxItem o pustej
    /// nazwie), potem Odswiez - i czytnik czytal CALY DIALOG (rola 4), takze po
    /// zakonczeniu. Stary ratunek fokusu porownywal skupiona kontrolke tylko z
    /// samymi listami, wiec WIERSZA nie rozpoznawal. Tu mierzymy:
    ///   * fokus z prawdziwego, WYGENEROWANEGO wiersza idzie na instrukcje PRZED
    ///     wylaczeniem listy,
    ///   * po wyniku wraca na SWIEZY kontener TEGO SAMEGO elementu (po ID),
    ///     takze po zmianie kolejnosci i etykiety - nie na liste, nie na
    ///     instrukcje, nie na stary, odlaczony kontener,
    ///   * to samo dla listy glosnikow,
    ///   * gdy element zniknal, zostaje czytelna instrukcja,
    ///   * swiadome przejscie uzytkownika na Zamknij ma pierwszenstwo.
    /// </summary>
    private static int MeasureBusyKeepsRealRowFocus()
    {
        var salon = new SonosGroup("GRUP_1", "Salon", "RINCON_A", new[] { "RINCON_A" }, SonosPlaybackState.Playing);
        var kuchnia = new SonosGroup("GRUP_2", "Kuchnia", "RINCON_C", new[] { "RINCON_C" }, SonosPlaybackState.Paused);
        var kuchniaNowa = new SonosGroup("GRUP_2", "Kuchnia nowa", "RINCON_C", new[] { "RINCON_C" }, SonosPlaybackState.Playing);
        var playerA = new SonosPlayer("RINCON_A", "Salon lewy", null, null, null);
        var playerC = new SonosPlayer("RINCON_C", "Kuchnia głośnik", null, null, null);
        var playerCNowy = new SonosPlayer("RINCON_C", "Kuchnia głośnik nowy", null, null, null);

        var gate = new ManualResetEventSlim(false);
        var reached = new ManualResetEventSlim(false);
        var reader = new LiczacyOdczyt
        {
            Topology = new SonosHouseholdTopology(new[] { salon, kuchnia }, new[] { playerA, playerC }, false)
        };

        var window = Build(reader);
        Pump(window.LoadAsync());

        var groups = (ListBox)window.FindName("GroupsList")!;
        var players = (ListBox)window.FindName("PlayersList")!;
        var instruction = (TextBox)window.FindName("InstructionBox")!;
        var closeButton = (Button)window.FindName("CloseButton")!;

        // 1) GRUPY: fokus na PRAWDZIWYM wierszu "Kuchnia" (indeks 1).
        var rowBefore = RealRow(groups, 1);
        if (rowBefore.Content as string != SonosDeviceLabels.DescribeGroup(kuchnia))
        {
            throw new Exception("Aparatura wzięła nie ten wiersz grupy - dalszy pomiar nie miałby sensu.");
        }

        System.Windows.Input.FocusManager.SetFocusedElement(window, rowBefore);
        if (!ReferenceEquals(System.Windows.Input.FocusManager.GetFocusedElement(window), rowBefore))
        {
            throw new Exception("Nie udało się ustawić fokusu na prawdziwym wierszu listy grup.");
        }

        reader.BeforeGroups = () =>
        {
            reached.Set();
            gate.Wait(TimeSpan.FromSeconds(5));
        };
        // Odswiezenie zwraca ten sam dom, ale grupy w INNEJ kolejnosci i z INNA
        // etykieta tego samego GRUP_2: tozsamosc moze isc tylko po ID.
        reader.Topology = new SonosHouseholdTopology(
            new[] { kuchniaNowa, salon }, new[] { playerC, playerA }, false);

        var task = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);

        var during = System.Windows.Input.FocusManager.GetFocusedElement(window);
        if (during is ListBoxItem)
        {
            throw new Exception(
                "W czasie odświeżania fokus został na WIERSZU listy, którą właśnie wyłączono - "
                + "czytnik czyta wtedy cały dialog.");
        }
        if (!ReferenceEquals(during, instruction))
        {
            throw new Exception(
                $"W czasie odświeżania fokus z wiersza grupy trafił na \"{Describe(during)}\" zamiast na czytelną instrukcję.");
        }

        gate.Set();
        Pump(task);

        var after = System.Windows.Input.FocusManager.GetFocusedElement(window);
        if (after is not ListBoxItem restoredGroup)
        {
            throw new Exception(
                $"Po odświeżeniu fokus nie wrócił na WIERSZ listy grup (jest \"{Describe(after)}\").");
        }
        if (ReferenceEquals(restoredGroup, rowBefore))
        {
            throw new Exception("Fokus trafił na STARY, odłączony kontener wiersza - lista została przebudowana.");
        }
        if (!ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(restoredGroup), groups))
        {
            throw new Exception("Przywrócony wiersz nie należy do listy grup.");
        }
        if (restoredGroup.Content as string != SonosDeviceLabels.DescribeGroup(kuchniaNowa))
        {
            throw new Exception(
                $"Po zmianie kolejności i etykiety fokus wrócił na \"{restoredGroup.Content}\" zamiast na ten sam dom/grupę po identyfikatorze.");
        }

        // 2) GLOSNIKI: ta sama droga na drugiej liscie.
        var playerRowBefore = RealRow(players, 0);
        var expectedPlayerId = playerRowBefore.Content as string == SonosDeviceLabels.DescribePlayer(playerC, reader.Topology!)
            ? "RINCON_C"
            : throw new Exception("Aparatura wzięła nie ten wiersz głośnika.");

        System.Windows.Input.FocusManager.SetFocusedElement(window, playerRowBefore);
        reached.Reset();
        gate.Reset();
        var afterTopology = new SonosHouseholdTopology(
            new[] { salon, kuchniaNowa }, new[] { playerA, playerCNowy }, false);
        reader.Topology = afterTopology;
        var playersTask = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);

        if (System.Windows.Input.FocusManager.GetFocusedElement(window) is ListBoxItem)
        {
            throw new Exception("W czasie odświeżania fokus został na WIERSZU listy głośników.");
        }

        gate.Set();
        Pump(playersTask);

        if (System.Windows.Input.FocusManager.GetFocusedElement(window) is not ListBoxItem restoredPlayer)
        {
            throw new Exception(
                $"Po odświeżeniu fokus nie wrócił na WIERSZ listy głośników (jest \"{Describe(System.Windows.Input.FocusManager.GetFocusedElement(window))}\").");
        }
        if (!ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(restoredPlayer), players))
        {
            throw new Exception("Przywrócony wiersz nie należy do listy głośników.");
        }
        if (restoredPlayer.Content as string != SonosDeviceLabels.DescribePlayer(playerCNowy, afterTopology))
        {
            throw new Exception(
                $"Fokus na liście głośników wrócił na \"{restoredPlayer.Content}\" zamiast na głośnik {expectedPlayerId} po identyfikatorze.");
        }

        // 3) ELEMENT ZNIKNAL: zostaje czytelna instrukcja, nie odlaczony kontener.
        var vanishing = RealRow(players, players.Items.Count - 1);
        System.Windows.Input.FocusManager.SetFocusedElement(window, vanishing);
        reached.Reset();
        gate.Reset();
        reader.Topology = new SonosHouseholdTopology(new[] { salon }, new[] { playerA }, false);
        var vanishTask = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);
        gate.Set();
        Pump(vanishTask);

        var afterVanish = System.Windows.Input.FocusManager.GetFocusedElement(window);
        if (afterVanish is ListBoxItem stale)
        {
            throw new Exception(
                $"Po zniknięciu elementu fokus celuje w wiersz \"{stale.Content}\" - kontener jest odłączony od listy.");
        }
        if (!ReferenceEquals(afterVanish, instruction))
        {
            throw new Exception(
                $"Po zniknięciu elementu fokus trafił na \"{Describe(afterVanish)}\" zamiast na czytelną instrukcję.");
        }

        // 4) ZAMKNIJ ma pierwszenstwo takze wtedy, gdy zajetosc zaczela sie z wiersza.
        reader.Topology = new SonosHouseholdTopology(new[] { salon, kuchnia }, new[] { playerA, playerC }, false);
        reached.Reset();
        gate.Reset();
        var rebuild = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);
        gate.Set();
        Pump(rebuild);

        var rowAgain = RealRow(groups, 1);
        System.Windows.Input.FocusManager.SetFocusedElement(window, rowAgain);
        reached.Reset();
        gate.Reset();
        var withClose = window.InvokeRefreshAsync();
        PumpUntil(() => reached.IsSet);
        System.Windows.Input.FocusManager.SetFocusedElement(window, closeButton);
        gate.Set();
        Pump(withClose);
        if (!ReferenceEquals(System.Windows.Input.FocusManager.GetFocusedElement(window), closeButton))
        {
            throw new Exception("Okno ukradło fokus z przycisku Zamknij wybranego przez użytkownika w czasie czekania.");
        }

        window.ShutdownOwnWork();
        return 12;
    }

    /// <summary>
    /// PRAWDZIWY, wygenerowany kontener wiersza - bez pokazywania okna. Sam
    /// szablon nie wystarcza: kontenery powstaja dopiero po przebiegu ukladu.
    /// </summary>
    private static ListBoxItem RealRow(ListBox list, int index)
    {
        list.ApplyTemplate();
        list.Measure(new Size(360, 600));
        list.Arrange(new Rect(0, 0, 360, 600));
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem row)
        {
            throw new Exception(
                $"Aparatura nie wygenerowała prawdziwego wiersza {index} listy {list.Name} - pomiar fokusu wiersza byłby pozorny.");
        }

        return row;
    }

    private static string Describe(IInputElement? element) => element switch
    {
        null => "<brak fokusu>",
        ListBoxItem row => $"wiersz \"{row.Content}\"",
        FrameworkElement named when named.Name.Length > 0 => named.Name,
        _ => element.GetType().Name
    };

    // ================= 4c. JEDEN komunikat o ladowaniu =================

    /// <summary>
    /// Slad z zywego NVDA: ZERO komunikatow o ladowaniu i PODWOJNY komunikat
    /// koncowy (lista domow + topologia). Tu mierzymy jawne "czekaj" PRZED
    /// oczekiwaniem i JEDEN komunikat wynikowy po nim.
    /// </summary>
    private static int MeasureLoadingAnnouncedOnce()
    {
        var gate = new ManualResetEventSlim(false);
        var reached = new ManualResetEventSlim(false);
        var reader = new LiczacyOdczyt
        {
            BeforeHouseholds = () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(5));
            }
        };

        var window = Build(reader);
        var task = window.LoadAsync();
        PumpUntil(() => reached.IsSet);

        if (window.LoadingAnnouncementCount != 1)
        {
            throw new Exception(
                $"Przed czekaniem okno ogłosiło ładowanie {window.LoadingAnnouncementCount} razy zamiast raz.");
        }
        if (window.LastAnnouncement != SonosDeviceLabels.LoadingHouseholds)
        {
            throw new Exception($"W czasie czekania czytnik dostał \"{window.LastAnnouncement}\" zamiast komunikatu ładowania.");
        }
        if (!window.InstructionText.Contains("Czekaj", StringComparison.Ordinal))
        {
            throw new Exception("Instrukcja w czasie czekania nie mówi o oczekiwaniu.");
        }
        // Pierwsze zdanie instrukcji - jawnie tylko do odczytu - zostaje.
        if (!window.InstructionText.Contains(SonosDeviceLabels.ViewIntroduction, StringComparison.Ordinal))
        {
            throw new Exception("Komunikat ładowania zgubił pierwsze zdanie o braku wpływu na odtwarzanie.");
        }

        gate.Set();
        Pump(task);

        // Wynik: JEDEN komunikat koncowy o topologii, nie dwa.
        if (window.LastAnnouncement == SonosDeviceLabels.LoadingHouseholds)
        {
            throw new Exception("Po zakończeniu okno zostało na komunikacie ładowania - wynik nie został ogłoszony.");
        }
        if (!window.LastAnnouncement.Contains("głośników", StringComparison.Ordinal))
        {
            throw new Exception($"Ostatni komunikat \"{window.LastAnnouncement}\" nie jest wynikiem odczytu urządzeń.");
        }
        // Dwa oczekiwania (domy + topologia) i DWA wyniki na tej samej drodze:
        // wynik listy domow NIE jest juz czytany osobno przy niepustej liscie.
        if (window.LoadingAnnouncementCount != 2)
        {
            throw new Exception($"Ładowanie ogłoszono {window.LoadingAnnouncementCount} razy zamiast dwóch (domy, topologia).");
        }
        if (window.AnnouncementCount != 3)
        {
            throw new Exception(
                $"Okno wypowiedziało {window.AnnouncementCount} komunikatów zamiast trzech (2 czekaj + 1 wynik).");
        }

        // Podwojny przedimek "Dom Dom Sonos 1" nie wraca przy domu bez nazwy.
        var unnamed = SonosDeviceLabels.DescribeTopology(
            SonosGroupsReadResult.Success(Topology()),
            SonosDeviceLabels.DescribeHousehold(new SonosHousehold("Sonos_9.z", null, null), 1));
        if (unnamed.Contains("Dom Dom", StringComparison.Ordinal))
        {
            throw new Exception($"Podsumowanie ma podwójny przedimek: \"{unnamed}\".");
        }
        if (!unnamed.StartsWith("Dom Sonos 1:", StringComparison.Ordinal))
        {
            throw new Exception($"Podsumowanie domu bez nazwy brzmi \"{unnamed}\".");
        }

        window.ShutdownOwnWork();
        return 8;
    }

    // ================= 4d. odswiezenie ZACHOWUJE wybrany dom =================

    /// <summary>
    /// Slad z zywego NVDA: po odswiezeniu wybor wracal z domu 2 na dom 1. Tu
    /// mierzymy, ze wybrany dom wraca PO IDENTYFIKATORZE takze po ZMIANIE
    /// KOLEJNOSCI listy, a gdy dom zniknie - dopiero wtedy fallback.
    /// </summary>
    private static int MeasureRefreshKeepsHouseholdById()
    {
        var first = new SonosHousehold("Sonos_1.a", "Dom na Kwiatowej", null);
        var second = new SonosHousehold("Sonos_2.b", "Domek letni", null);
        var reader = new LiczacyOdczyt { Households = new[] { first, second } };

        var window = Build(reader);
        Pump(window.LoadAsync());
        Pump(window.InvokeSelectHouseholdAsync(1));
        if (window.SelectedHouseholdId != "Sonos_2.b")
        {
            throw new Exception("Wybór drugiego domu nie zadziałał - dalszy pomiar nie miałby sensu.");
        }

        // 1) TA SAMA kolejnosc: stary kod zerowal wybor na pierwszy dom.
        Pump(window.InvokeRefreshAsync());
        if (window.SelectedHouseholdId != "Sonos_2.b")
        {
            throw new Exception(
                $"Odświeżenie przestawiło wybór na \"{window.SelectedHouseholdId}\" zamiast zachować wybrany dom.");
        }
        if (reader.LastHouseholdId != "Sonos_2.b")
        {
            throw new Exception("Po odświeżeniu grupy odczytano dla INNEGO domu niż wybrany.");
        }

        // 2) Sonos zwraca te same domy w INNEJ kolejnosci - wybor idzie PO ID,
        //    wiec zaznaczenie musi przeskoczyc na nowa pozycje tego samego domu.
        reader.Households = new[] { second, first };
        Pump(window.InvokeRefreshAsync());
        if (window.SelectedHouseholdId != "Sonos_2.b")
        {
            throw new Exception("Po zmianie kolejności listy wybór domu nie został zachowany po identyfikatorze.");
        }
        var box = (ComboBox)window.FindName("HouseholdBox")!;
        if (box.SelectedIndex != 0)
        {
            throw new Exception("Zaznaczenie na liście domów nie wskazuje wybranego domu po zmianie kolejności.");
        }
        if (window.HouseholdChoices[0] != "Domek letni")
        {
            throw new Exception("Etykiety listy domów nie odwzorowują nowej kolejności.");
        }

        // Dopiero ZNIKNIECIE wybranego domu cofa wybor na pierwszy dostepny.
        reader.Households = new[] { first };
        Pump(window.InvokeRefreshAsync());
        if (window.SelectedHouseholdId != "Sonos_1.a")
        {
            throw new Exception("Po zniknięciu wybranego domu okno nie wróciło do pierwszego dostępnego.");
        }

        // Pusta lista nie zostawia martwego wyboru.
        reader.Households = Array.Empty<SonosHousehold>();
        Pump(window.InvokeRefreshAsync());
        if (window.SelectedHouseholdId is not null)
        {
            throw new Exception("Pusta lista domów zostawiła wybrany dom.");
        }

        window.ShutdownOwnWork();
        return 6;
    }

    // ================= 5. spozniony wynik nie podmienia listy =================

    private static int MeasureStaleResultIgnored()
    {
        var gate = new ManualResetEventSlim(false);
        var reached = new ManualResetEventSlim(false);
        var reader = new LiczacyOdczyt
        {
            BeforeGroups = () =>
            {
                reached.Set();
                gate.Wait(TimeSpan.FromSeconds(5));
            }
        };
        reader.Households = new[]
        {
            new SonosHousehold("Sonos_1.a", "Dom na Kwiatowej", null),
            new SonosHousehold("Sonos_2.b", "Domek letni", null)
        };

        var window = Build(reader);
        var task = window.LoadAsync();
        PumpUntil(() => reached.IsSet);

        // W TRAKCIE wiszacego odczytu grup okno zostaje zamkniete.
        window.ShutdownOwnWork();
        gate.Set();
        Pump(task);

        if (window.GroupLabels.Count != 0 || window.PlayerLabels.Count != 0)
        {
            throw new Exception("Wynik spóźniony PO ZAMKNIĘCIU podmienił listę urządzeń.");
        }

        // ANULOWANIE nie jest ciche - ale po zamknieciu okno tez nie gada do nikogo.
        var cancelReader = new LiczacyOdczyt { CancelHouseholds = true };
        var second = Build(cancelReader);
        Pump(second.LoadAsync());
        if (!second.LastAnnouncement.Contains("przerwan", StringComparison.OrdinalIgnoreCase)
            && !second.LastAnnouncement.Contains("anulowan", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Anulowanie odczytu było ciche: \"{second.LastAnnouncement}\".");
        }
        second.ShutdownOwnWork();
        return 2;
    }

    // ================= 6. zamkniecie anuluje TYLKO wlasna prace =================

    private static int MeasureCloseCancelsOwnWorkOnly()
    {
        var store = new PustyMagazyn();
        using var owner = new SonosAccountOwner
        {
            StoreFactory = _ => store,
            GatewayFactory = _ => new MilczacaBramka()
        };
        var coordinator = owner.EnsureCoordinator();

        var reader = new LiczacyOdczyt();
        var window = Build(reader);
        Pump(window.LoadAsync());
        window.ShutdownOwnWork();

        if (!window.IsClosedForWork)
        {
            throw new Exception("Zamknięcie nie zakończyło pracy okna.");
        }
        if (store.Deletes != 0)
        {
            throw new Exception("Zamknięcie okna listy usunęło zapisane logowanie.");
        }

        // Zasoby WLASCICIELA zyja dalej: koordynator odpowiada.
        if (coordinator.Snapshot is null || !ReferenceEquals(owner.EnsureCoordinator(), coordinator))
        {
            throw new Exception("Zamknięcie okna listy zwolniło zasoby właściciela konta.");
        }

        // Po zamknieciu kolejny odczyt NIE startuje (operacja jest zamknieta).
        var before = reader.HouseholdCalls;
        Pump(window.InvokeRefreshAsync());
        if (reader.HouseholdCalls != before)
        {
            throw new Exception("Zamknięte okno nadal wysyła żądania odczytu.");
        }

        return 4;
    }

    // ================= 7. przycisk w oknie Konta Sonos =================

    private static int MeasureAccountButtonWiring()
    {
        var store = new PustyMagazyn();
        using var owner = new SonosAccountOwner
        {
            StoreFactory = _ => store,
            GatewayFactory = _ => new MilczacaBramka(),
            ControlApiConfigurationFactory = () => SonosControlApiConfiguration.CreateDefault("nieuzywany-w-tym-tescie")
        };
        var presenter = new SonosAccountPresenter(owner);

        SonosAccountWindow? accountWindow = null;
        presenter.PresentOverride = window => accountWindow = window;
        if (!presenter.Show(null)) throw new Exception("Okno konta nie powstało.");
        if (accountWindow is null) throw new Exception("Okno konta nie dotarło do testowego pokazania.");

        var devicesButton = (Button)accountWindow.FindName("DevicesButton")!;
        if (devicesButton.Content as string != "_Głośniki i grupy…")
        {
            throw new Exception("Przycisk listy urządzeń nie ma polskiej etykiety z akceleratorem.");
        }

        // BEZ poswiadczen: droga do listy jest NIEDOSTEPNA, a nie martwa.
        if (accountWindow.IsDevicesAvailable)
        {
            throw new Exception("Lista urządzeń jest dostępna bez zalogowanego konta.");
        }

        // KONTO DZIALA, ale ZAPIS SIE NIE UDAL: lista MA byc dostepna.
        var coordinator = owner.EnsureCoordinator();
        ApplyWorkingAccountWithFailedWrite(coordinator);
        accountWindow.RefreshView();
        if (!accountWindow.IsDevicesAvailable)
        {
            throw new Exception("Konto działa (przy nieudanym zapisie), a lista urządzeń jest niedostępna.");
        }

        // Klikniecie przechodzi produkcyjna droga prezentera do okna listy.
        SonosDevicesWindow? devices = null;
        presenter.PresentDevicesOverride = window => devices = window;
        accountWindow.InvokeShowDevices();
        if (devices is null || presenter.DevicesWindowsCreated != 1)
        {
            throw new Exception("Przycisk nie zbudował okna listy urządzeń przez prezentera.");
        }
        if (devices.Owner is not null && !ReferenceEquals(devices.Owner, accountWindow))
        {
            throw new Exception("Okno listy ma innego właściciela niż okno konta.");
        }

        // Okno listy dostaje WYLACZNIE callbacki - zadnego tokenu ani koordynatora.
        foreach (var field in typeof(SonosDevicesWindow).GetFields(Internal))
        {
            if (field.FieldType == typeof(SonosAccountCoordinator)
                || field.FieldType == typeof(SonosControlApiClient))
            {
                throw new Exception($"Okno listy trzyma {field.FieldType.Name} - widzi więcej niż odczyt.");
            }
            if (field.FieldType == typeof(string) && field.Name.Contains("token", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Okno listy ma pole tokenu.");
            }
        }

        // Klucz integracji jest STALA programu, a nie polem w interfejsie.
        if (Descendants<TextBox>(devices).Any(box => !box.IsReadOnly))
        {
            throw new Exception("Okno listy ma edytowalne pole - tester nie ma wpisywać żadnych kluczy.");
        }
        if (string.IsNullOrWhiteSpace(SonosAccountOwner.IntegrationApiKey))
        {
            throw new Exception("Wbudowany klucz integracji Sonos jest pusty.");
        }

        // Klient Control API jest LENIWY i tworzony NAJWYZEJ RAZ.
        if (owner.ControlApiCreations != 0)
        {
            throw new Exception("Klient Control API powstał bez żadnego odczytu.");
        }
        owner.EnsureDeviceApi();
        owner.EnsureDeviceApi();
        if (owner.ControlApiCreations != 1)
        {
            throw new Exception($"Klient Control API powstał {owner.ControlApiCreations} razy zamiast raz.");
        }

        // GUARD: drugie klikniecie nie tworzy drugiego okna listy.
        var reopened = presenter.ShowDevices(accountWindow);
        if (!reopened || presenter.DevicesWindowsCreated != 2)
        {
            throw new Exception("Ponowne otwarcie listy po zamknięciu nie zbudowało okna.");
        }

        accountWindow.Close();
        return 8;
    }

    /// <summary>
    /// Wstawia DZIALAJACE konto, ktorego NIE udalo sie zapisac - czyli dokladnie
    /// ten przypadek, w ktorym lista urzadzen nadal musi dzialac.
    /// </summary>
    private static void ApplyWorkingAccountWithFailedWrite(SonosAccountCoordinator coordinator)
    {
        var tokens = new SonosTokens("dostep-syntetyczny", SonosTokens.BearerTokenType, 3600, "odswiezanie-syntetyczne", null);
        var method = typeof(SonosAccountCoordinator).GetMethod("ApplyCredentialsForMeasurement", Internal)
            ?? throw new Exception(
                "Brak punktu wstawienia poświadczeń do pomiaru - dodaj internal ApplyCredentialsForMeasurement.");
        method.Invoke(coordinator, new object?[] { tokens, SonosCredentialWriteStatus.WriteFailure });
    }

    // ================= aparatura =================

    private static SonosDevicesWindow Build(LiczacyOdczyt reader) =>
        new(reader.ReadHouseholdsAsync, reader.ReadGroupsAsync);

    private static SonosHouseholdTopology Topology() => new(
        new[]
        {
            new SonosGroup("GRUP_1", "Salon", "RINCON_A", new[] { "RINCON_A", "RINCON_B" }, SonosPlaybackState.Playing)
        },
        new[]
        {
            new SonosPlayer("RINCON_A", "Salon lewy", null, null, null),
            new SonosPlayer("RINCON_B", "Salon prawy", null, null, null)
        },
        partial: true);

    /// <summary>
    /// MAGAZYN, ktory tylko liczy wywolania i NIGDY nie dotyka prawdziwego
    /// DPAPI. Wlasna kopia, bo atrapa z pomiaru konta jest tam prywatna.
    /// </summary>
    private sealed class PustyMagazyn : ISonosCredentialStore
    {
        internal int Reads;
        internal int Writes;
        internal int Deletes;

        public SonosCredentialReadOutcome Read()
        {
            Reads++;
            return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing);
        }

        public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
        {
            Writes++;
            return SonosCredentialWriteOutcome.Ok();
        }

        public bool Delete()
        {
            Deletes++;
            return true;
        }
    }

    /// <summary>Bramka logowania, ktora ma NIE zostac zawolana ani razu.</summary>
    private sealed class MilczacaBramka : ISonosLoginGateway
    {
        public Task<SonosLoginStartOutcome> StartAsync(CancellationToken cancellationToken) =>
            throw new Exception("Pomiar listy urządzeń nie ma prawa rozpoczynać logowania.");

        public Task<SonosLoginResultOutcome> FetchResultAsync(SonosLoginSession session, CancellationToken cancellationToken) =>
            throw new Exception("Pomiar listy urządzeń nie ma prawa odbierać wyniku logowania.");

        public Task<SonosRefreshOutcome> RefreshAsync(string? refreshToken, CancellationToken cancellationToken) =>
            throw new Exception("Pomiar listy urządzeń nie ma prawa odnawiać dostępu.");
    }

    /// <summary>
    /// ATRAPA ODCZYTU: podaje gotowe wyniki, liczy wywolania i umie zawisnac
    /// PRZED odpowiedzia, zeby dalo sie zmierzyc stan "w toku". Zero sieci.
    /// </summary>
    private sealed class LiczacyOdczyt
    {
        internal IReadOnlyList<SonosHousehold> Households { get; set; } =
            new[] { new SonosHousehold("Sonos_1.a", "Dom na Kwiatowej", null) };

        internal SonosHouseholdTopology? Topology { get; set; } = Topology();

        internal string? GroupFailure { get; set; }

        internal bool CancelHouseholds { get; set; }

        internal Action? BeforeHouseholds { get; set; }

        internal Action? BeforeGroups { get; set; }

        internal int HouseholdCalls { get; private set; }

        internal int GroupCalls { get; private set; }

        internal string? LastHouseholdId { get; private set; }

        internal async Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken)
        {
            HouseholdCalls++;
            // Oczekiwanie idzie do WATKU PULI: gdyby bramka blokowala watek okna,
            // pomiar zajetosci mierzylby zablokowany dispatcher, a nie zachowanie okna.
            if (BeforeHouseholds is { } before)
            {
                await Task.Run(before, CancellationToken.None).ConfigureAwait(true);
            }

            if (CancelHouseholds)
            {
                return SonosHouseholdsReadResult.Failure(
                    SonosDeviceReadStatus.Canceled,
                    "Odczyt domów Sonos został przerwany.");
            }

            return SonosHouseholdsReadResult.Success(Households);
        }

        internal async Task<SonosGroupsReadResult> ReadGroupsAsync(string householdId, CancellationToken cancellationToken)
        {
            GroupCalls++;
            LastHouseholdId = householdId;
            if (BeforeGroups is { } before)
            {
                await Task.Run(before, CancellationToken.None).ConfigureAwait(true);
            }

            if (GroupFailure is { } failure)
            {
                return SonosGroupsReadResult.Failure(SonosDeviceReadStatus.Forbidden, failure);
            }

            var topology = Topology ?? new SonosHouseholdTopology(
                Array.Empty<SonosGroup>(), Array.Empty<SonosPlayer>(), false);
            return SonosGroupsReadResult.Success(topology);
        }
    }

    // ================= pompowanie Dispatchera bez pokazywania okna =================

    private static void Pump(Task task)
    {
        PumpUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> done)
    {
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(5)
        };
        timer.Tick += (_, _) =>
        {
            if (done() || DateTime.UtcNow > deadline)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (!done())
        {
            throw new Exception("Operacja okna listy urządzeń nie zakończyła się w limicie czasu.");
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(current))
            {
                if (child is not DependencyObject dependency)
                {
                    continue;
                }

                if (dependency is T match)
                {
                    yield return match;
                }

                stack.Push(dependency);
            }
        }
    }

    /// <summary>
    /// Liczba handlerow podpietych do zdarzenia kontrolki. Sluzy do wykazania,
    /// ze listy NIE maja wlasnej aktywacji elementu.
    /// </summary>
    private static int GetEventHandlerCount(UIElement element, string eventName)
    {
        var store = typeof(UIElement)
            .GetProperty("EventHandlersStore", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(element);
        if (store is null)
        {
            return 0;
        }

        var routedEvent = typeof(Control).Assembly.GetType("System.Windows.Controls.ListBox") is not null
            ? FindRoutedEvent(element, eventName)
            : null;
        if (routedEvent is null)
        {
            return 0;
        }

        var handlers = store.GetType()
            .GetMethod("GetRoutedEventHandlers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.Invoke(store, new object?[] { routedEvent }) as Delegate[];
        return handlers?.Length ?? 0;
    }

    private static RoutedEvent? FindRoutedEvent(UIElement element, string eventName)
    {
        foreach (var candidate in EventManager.GetRoutedEvents())
        {
            if (candidate.Name == eventName && candidate.OwnerType.IsAssignableFrom(element.GetType()))
            {
                return candidate;
            }
        }

        return null;
    }
}
