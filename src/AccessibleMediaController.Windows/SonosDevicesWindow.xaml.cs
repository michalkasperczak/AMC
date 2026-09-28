using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// OKNO LISTY URZADZEN SONOS: domy, grupy i glosniki. TYLKO ODCZYT.
///
/// Granice tego okna, swiadome i sprawdzane testami:
///   * NIE jest odtwarzaczem. Enter i strzalki na listach niczego nie
///     uruchamiaja, nie ma zadnego przycisku sterowania odtwarzaniem,
///   * ZASOBY (koordynator, klient Control API) naleza do WLASCICIELA konta.
///     Zamkniecie okna anuluje WLASNA operacje i nic wiecej - zero Dispose,
///   * JEDNA operacja w toku. Zaden timer, zaden polling, zaden sleep,
///   * wynik SPOZNIONY - po zmianie wyboru domu albo po zamknieciu - NIE
///     podmienia listy. Pilnuje tego wlasny licznik zadan,
///   * przy zajetosci przyciski NIE znikaja: zostaja wylaczone - ale SAMO
///     wylaczenie skupionej kontrolki albo WIERSZA listy tez oddaje fokus oknu
///     (zmierzone zywym NVDA: czytnik czytal caly dialog). Dlatego przed
///     wylaczeniem fokus jest RATOWANY na pole instrukcji, a po wyniku wraca na
///     ten sam element - wiersz odszukany PO IDENTYFIKATORZE,
///   * pusty dom, lista NIEPELNA, blad i anulowanie mowia to WPROST.
/// </summary>
public partial class SonosDevicesWindow
{
    private readonly Func<CancellationToken, Task<SonosHouseholdsReadResult>> readHouseholds;
    private readonly Func<string, CancellationToken, Task<SonosGroupsReadResult>> readGroups;
    private readonly Action<string>? announcementSink;

    /// <summary>Anuluje WYLACZNIE prace tego okna. Nie dotyka zycia wlasciciela konta.</summary>
    private readonly CancellationTokenSource windowLifetime = new();

    private readonly List<SonosHousehold> households = new();

    /// <summary>
    /// TOZSAMOSCI wierszy obu list, w kolejnosci elementow. Etykiety pozostaja
    /// czystym tekstem, wiec identyfikator nie ma prawa trafic do czytnika -
    /// ale bez niego nie da sie wrocic fokusem na TEN SAM glosnik albo grupe po
    /// przebudowie listy.
    /// </summary>
    private readonly List<string> groupIds = new();

    private readonly List<string> playerIds = new();

    private bool busy;
    private bool closed;
    private bool suppressSelectionReload;

    /// <summary>
    /// Kontrolka, ktora miala fokus w chwili wejscia w zajetosc. Po zakonczeniu
    /// operacji fokus wraca DOKLADNIE do niej - o ile uzytkownik w miedzyczasie
    /// sam nie wybral czegos innego (np. Zamknij).
    /// </summary>
    private IInputElement? focusBeforeBusy;

    /// <summary>
    /// WIERSZ listy, ktory mial fokus przed zajetoscia, zapamietany PO
    /// IDENTYFIKATORZE. Kontenery wierszy sa niszczone przy przebudowie listy,
    /// wiec sam obiekt kontrolki nie nadaje sie do powrotu.
    /// </summary>
    private RowFocusTarget? rowBeforeBusy;

    /// <summary>Wiersz listy wskazany przez dom i identyfikator elementu, nie przez pozycje ani etykiete.</summary>
    private sealed record RowFocusTarget(string ListName, string HouseholdId, string ItemId);

    /// <summary>
    /// Identyfikator domu WYBRANEGO przez uzytkownika. Odswiezenie ma go
    /// odtworzyc PO ID, nawet gdy Sonos zwroci domy w innej kolejnosci.
    /// </summary>
    private string? requestedHouseholdId;

    /// <summary>
    /// Numer NAJNOWSZEGO zadania odczytu. Wynik ze starszym numerem jest
    /// porzucany, wiec przeterminowana odpowiedz po starym wyborze domu nie
    /// podmienia listy.
    /// </summary>
    private long requestSequence;

    public string LastAnnouncement { get; private set; } = string.Empty;

    public int AnnouncementCount { get; private set; }

    /// <summary>Ile razy okno oglosilo OCZEKIWANIE (kwit dla testow).</summary>
    public int LoadingAnnouncementCount { get; private set; }

    /// <summary>Identyfikator WYBRANEGO domu. Nigdy nie pokazywany w zwyklej etykiecie.</summary>
    internal string? SelectedHouseholdId { get; private set; }

    public SonosDevicesWindow(
        Func<CancellationToken, Task<SonosHouseholdsReadResult>> readHouseholds,
        Func<string, CancellationToken, Task<SonosGroupsReadResult>> readGroups,
        Action<string>? announcementSink = null)
    {
        ArgumentNullException.ThrowIfNull(readHouseholds);
        ArgumentNullException.ThrowIfNull(readGroups);
        this.readHouseholds = readHouseholds;
        this.readGroups = readGroups;
        this.announcementSink = announcementSink;

        InitializeComponent();

        InstructionBox.Text = SonosDeviceLabels.ViewIntroduction;
        HouseholdPanel.Visibility = Visibility.Collapsed;

        // Fokus startowy ustawiony przy BUDOWIE okna, nie przez opoznienie -
        // da sie go zmierzyc bez Show i bez odbierania fokusu pulpitowi.
        FocusManager.SetFocusedElement(this, InstructionBox);
    }

    // ================= widok =================

    /// <summary>
    /// Komunikat OCZEKIWANIA. Idzie ta sama droga co wynik, ale jest liczony
    /// osobno, zeby test widzial, ze ladowanie zostalo ogloszone RAZ i PRZED
    /// czekaniem - a nie zamiast wyniku.
    /// </summary>
    private void AnnounceLoading(string message)
    {
        LoadingAnnouncementCount++;
        SetInstruction(message);
        Announce(message);
    }

    private void Announce(string message)
    {
        LastAnnouncement = message;
        AnnouncementCount++;
        if (announcementSink is not null)
        {
            OperationStatusText.Text = message;
            announcementSink(message);
            return;
        }

        OperationStatusText.Announce(message);
    }

    /// <summary>
    /// Zajetosc WYLACZA przyciski, ale ich NIE CHOWA: schowanie kontrolki z
    /// fokusem kaze czytnikowi czytac cale okno od nowa.
    ///
    /// SAMO WYLACZENIE skupionej kontrolki tez gubi fokus (WPF oddaje go oknu,
    /// a czytnik czyta caly dialog). Dlatego PRZED wylaczeniem przenosimy fokus
    /// na pole instrukcji - zostaje wlaczone i czytelne - a po zakonczeniu
    /// przywracamy poprzednia kontrolke, o ile uzytkownik nie wybral innej.
    /// </summary>
    private void ApplyBusy()
    {
        if (busy)
        {
            RescueFocusBeforeDisabling();
        }

        RefreshButton.IsEnabled = !busy;
        HouseholdBox.IsEnabled = !busy && households.Count > 1;
        GroupsList.IsEnabled = !busy;
        PlayersList.IsEnabled = !busy;

        // Wyjscie dziala zawsze - inaczej uzytkownik zostaje uwieziony w czekaniu.
        CloseButton.IsEnabled = true;
        CloseButton.Visibility = Visibility.Visible;
        RefreshButton.Visibility = Visibility.Visible;

        if (!busy)
        {
            RestoreFocusAfterBusy();
        }
    }

    /// <summary>
    /// Zapamietuje skupiona kontrolke i przenosi fokus na instrukcje, ZANIM
    /// wylaczenie zabierze go oknu. Fokus na Zamknij zostaje tam, gdzie jest -
    /// ten przycisk dziala takze w czasie oczekiwania.
    /// </summary>
    private void RescueFocusBeforeDisabling()
    {
        if (focusBeforeBusy is not null || rowBeforeBusy is not null)
        {
            return;
        }

        var focused = FocusManager.GetFocusedElement(this);
        if (focused is null || ReferenceEquals(focused, CloseButton) || ReferenceEquals(focused, InstructionBox))
        {
            return;
        }

        // WIERSZ listy tez ma fokus, ktory trzeba uratowac: ZYWY NVDA pokazal,
        // ze po wylaczeniu listy czytnik czyta caly dialog. Kontener wiersza
        // ginie przy przebudowie, wiec zapamietujemy TOZSAMOSC elementu.
        if (DescribeFocusedRow(focused) is { } row)
        {
            rowBeforeBusy = row;
            FocusManager.SetFocusedElement(this, InstructionBox);
            return;
        }

        if (!ReferenceEquals(focused, RefreshButton)
            && !ReferenceEquals(focused, HouseholdBox)
            && !ReferenceEquals(focused, GroupsList)
            && !ReferenceEquals(focused, PlayersList))
        {
            return;
        }

        focusBeforeBusy = focused;
        FocusManager.SetFocusedElement(this, InstructionBox);
    }

    /// <summary>
    /// Tozsamosc wiersza pod fokusem: ktora lista, ktory dom i ktory element.
    /// Zwraca null dla wszystkiego, co nie jest wierszem NASZYCH dwoch list.
    /// </summary>
    private RowFocusTarget? DescribeFocusedRow(IInputElement focused)
    {
        if (focused is not DependencyObject candidate)
        {
            return null;
        }

        var container = candidate as ListBoxItem
            ?? ItemsControl.ContainerFromElement(GroupsList, candidate) as ListBoxItem
            ?? ItemsControl.ContainerFromElement(PlayersList, candidate) as ListBoxItem;
        if (container is null || SelectedHouseholdId is null)
        {
            return null;
        }

        var list = ItemsControl.ItemsControlFromItemContainer(container) as ListBox;
        var ids = ReferenceEquals(list, GroupsList) ? groupIds
            : ReferenceEquals(list, PlayersList) ? playerIds
            : null;
        if (list is null || ids is null)
        {
            return null;
        }

        var index = list.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0 || index >= ids.Count)
        {
            return null;
        }

        return new RowFocusTarget(list.Name, SelectedHouseholdId, ids[index]);
    }

    /// <summary>
    /// Wraca fokusem na kontrolke sprzed oczekiwania. NIE kradnie fokusu, gdy
    /// uzytkownik przeszedl w miedzyczasie gdzie indziej (np. na Zamknij).
    /// </summary>
    private void RestoreFocusAfterBusy()
    {
        var target = focusBeforeBusy;
        var row = rowBeforeBusy;
        focusBeforeBusy = null;
        rowBeforeBusy = null;
        if ((target is null && row is null) || closed)
        {
            return;
        }

        var focused = FocusManager.GetFocusedElement(this);
        if (focused is not null && !ReferenceEquals(focused, InstructionBox))
        {
            // Uzytkownik sam wybral inna kontrolke: to jego wybor wygrywa.
            return;
        }

        if (row is not null)
        {
            // SWIEZY kontener tego samego elementu; gdy element albo dom
            // zniknal, zostaje czytelna instrukcja - nigdy odlaczony wiersz.
            if (FindFreshRow(row) is { } fresh)
            {
                FocusManager.SetFocusedElement(this, fresh);
            }

            return;
        }

        if (target is UIElement element
            && (!element.IsEnabled || element.Visibility != Visibility.Visible))
        {
            // Kontrolka zniknela wraz z wynikiem (np. lista domow przy jednym
            // domu): zostaje czytelna instrukcja, nie puste okno. Sprawdzamy
            // Visibility, a nie IsVisible - to drugie jest falszywe takze w
            // oknie jeszcze nie pokazanym, wiec zablokowaloby powrot fokusu.
            return;
        }

        FocusManager.SetFocusedElement(this, target);
    }

    /// <summary>
    /// Kontener wiersza ZBUDOWANY PO przebudowie listy, odszukany po
    /// identyfikatorze elementu. Null, gdy dom sie zmienil, element zniknal,
    /// lista jest pusta albo kontener jeszcze nie istnieje.
    /// </summary>
    private ListBoxItem? FindFreshRow(RowFocusTarget row)
    {
        if (!string.Equals(SelectedHouseholdId, row.HouseholdId, StringComparison.Ordinal))
        {
            return null;
        }

        var list = row.ListName == GroupsList.Name
            ? GroupsList
            : row.ListName == PlayersList.Name ? PlayersList : null;
        var ids = list is null ? null : ReferenceEquals(list, GroupsList) ? groupIds : playerIds;
        if (list is null || ids is null || !list.IsEnabled || list.Visibility != Visibility.Visible)
        {
            return null;
        }

        var index = ids.IndexOf(row.ItemId);
        if (index < 0 || index >= list.Items.Count)
        {
            return null;
        }

        // Kontener powstaje dopiero przy przebiegu ukladu - wymuszamy go tutaj,
        // bez pokazywania okna, zadnego timera i zadnej aktywacji.
        list.UpdateLayout();
        return list.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
    }

    private void SetInstruction(string headline)
    {
        InstructionBox.Text = SonosDeviceLabels.ViewIntroduction
            + Environment.NewLine + Environment.NewLine + headline;
    }

    // ================= operacje =================

    /// <summary>
    /// Wspolna oslona: JEDNA operacja w toku, kazdy wyjatek obsluzony, a
    /// spozniona kontynuacja po zamknieciu nie rusza interfejsu.
    /// </summary>
    private async Task RunAsync(Func<CancellationToken, long, Task> operation, string failureMessage)
    {
        if (busy || closed)
        {
            return;
        }

        busy = true;
        ApplyBusy();
        var sequence = ++requestSequence;
        try
        {
            await operation(windowLifetime.Token, sequence).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (!closed)
            {
                Announce("Odczyt urządzeń Sonos został przerwany.");
            }
        }
        catch (ObjectDisposedException)
        {
            if (!closed)
            {
                Announce("Obsługa konta Sonos jest niedostępna. Zamknij to okno.");
            }
        }
        catch (Exception)
        {
            // Komunikat STALY: zaden wyjatek transportu nie cytuje adresu,
            // tokenu ani ciala odpowiedzi.
            if (!closed)
            {
                Announce(failureMessage);
            }
        }
        finally
        {
            busy = false;
            if (!closed)
            {
                ApplyBusy();
            }
        }
    }

    /// <summary>
    /// JAWNE, pierwsze wczytanie listy. Wolane przez wlasciciela PO zbudowaniu
    /// okna, zeby konstruktor nie wysylal zadan HTTP.
    /// </summary>
    internal Task LoadAsync() =>
        RunAsync(LoadHouseholdsAsync, "Nie udało się odczytać domów Sonos.");

    internal Task InvokeRefreshAsync() =>
        RunAsync(LoadHouseholdsAsync, "Nie udało się odświeżyć listy urządzeń Sonos.");

    private async Task LoadHouseholdsAsync(CancellationToken cancellationToken, long sequence)
    {
        // JEDEN jawny komunikat PRZED czekaniem. Bez niego dlugie oczekiwanie
        // wyglada jak zawieszenie, a czytnik powtarza STARY wynik jak nowy.
        AnnounceLoading(SonosDeviceLabels.LoadingHouseholds);

        var result = await readHouseholds(cancellationToken).ConfigureAwait(true);
        if (closed || sequence != requestSequence)
        {
            // Wynik PRZETERMINOWANY: nie podmienia listy.
            return;
        }

        if (!result.Succeeded)
        {
            // Blad NIGDY nie jest cichy i nie udaje pustej listy.
            households.Clear();
            HouseholdBox.Items.Clear();
            HouseholdPanel.Visibility = Visibility.Collapsed;
            ClearLists();
            SetInstruction(result.Message);
            Announce(result.Message);
            return;
        }

        households.Clear();
        households.AddRange(result.Households);
        var labels = SonosDeviceLabels.DescribeHouseholdChoices(result.Households);

        suppressSelectionReload = true;
        HouseholdBox.Items.Clear();
        foreach (var label in labels)
        {
            // Element listy to CZYSTY tekst - nigdy ToString obiektu ani identyfikator.
            HouseholdBox.Items.Add(label);
        }

        // ODSWIEZENIE NIE GUBI WYBORU: dom wskazany przez uzytkownika wraca PO
        // IDENTYFIKATORZE, takze gdy Sonos zwrocil domy w innej kolejnosci.
        // Dopiero gdy ten dom faktycznie zniknal, wracamy do pierwszego.
        var restored = requestedHouseholdId is null
            ? -1
            : households.FindIndex(h => string.Equals(h.Id, requestedHouseholdId, StringComparison.Ordinal));
        HouseholdBox.SelectedIndex = households.Count == 0
            ? -1
            : restored >= 0 ? restored : 0;
        suppressSelectionReload = false;

        HouseholdPanel.Visibility = households.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        var summary = SonosDeviceLabels.DescribeHouseholds(result);
        SetInstruction(summary);

        if (households.Count == 0)
        {
            // Pusta lista to KONIEC operacji, wiec tutaj wynik trzeba oglosic.
            Announce(summary);
            SelectedHouseholdId = null;
            requestedHouseholdId = null;
            ClearLists();
            return;
        }

        // Nie oglaszamy tu nic wiecej: operacja trwa dalej i konczy sie JEDNYM
        // komunikatem o topologii wybranego domu (bez podwojnego czytania).
        await LoadGroupsForSelectionAsync(cancellationToken, sequence).ConfigureAwait(true);
    }

    private async Task LoadGroupsForSelectionAsync(CancellationToken cancellationToken, long sequence)
    {
        var index = HouseholdBox.SelectedIndex;
        if (index < 0 || index >= households.Count)
        {
            return;
        }

        var household = households[index];
        var label = SonosDeviceLabels.DescribeHousehold(household, index + 1);
        SelectedHouseholdId = household.Id;

        // Wybor uzytkownika zapisany PO ID: odswiezenie ma go odtworzyc.
        requestedHouseholdId = household.Id;

        // Oczekiwanie na topologie tez jest jawne.
        AnnounceLoading(SonosDeviceLabels.LoadingTopology);

        var result = await readGroups(household.Id, cancellationToken).ConfigureAwait(true);
        if (closed || sequence != requestSequence)
        {
            // Odpowiedz po STARYM wyborze domu albo po zamknieciu: porzucona.
            return;
        }

        var summary = SonosDeviceLabels.DescribeTopology(result, label);
        if (!result.Succeeded || result.Topology is null)
        {
            ClearLists();
            SetInstruction(summary);
            Announce(summary);
            return;
        }

        var topology = result.Topology;
        GroupsList.Items.Clear();
        groupIds.Clear();
        foreach (var group in topology.Groups)
        {
            GroupsList.Items.Add(SonosDeviceLabels.DescribeGroup(group));
            groupIds.Add(group.Id);
        }

        PlayersList.Items.Clear();
        playerIds.Clear();
        foreach (var player in topology.Players)
        {
            PlayersList.Items.Add(SonosDeviceLabels.DescribePlayer(player, topology));
            playerIds.Add(player.Id);
        }

        SetInstruction(summary);
        Announce(summary);
    }

    private void ClearLists()
    {
        GroupsList.Items.Clear();
        PlayersList.Items.Clear();
        groupIds.Clear();
        playerIds.Clear();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(LoadHouseholdsAsync, "Nie udało się odświeżyć listy urządzeń Sonos.");

    private async void Household_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionReload || closed)
        {
            return;
        }

        await RunAsync(LoadGroupsForSelectionAsync, "Nie udało się odczytać grup Sonos.");
    }

    /// <summary>Ta sama droga, ktora idzie zmiana wyboru domu - do pomiaru bez GUI.</summary>
    internal Task InvokeSelectHouseholdAsync(int index)
    {
        suppressSelectionReload = true;
        HouseholdBox.SelectedIndex = index;
        suppressSelectionReload = false;
        return RunAsync(LoadGroupsForSelectionAsync, "Nie udało się odczytać grup Sonos.");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) => ShutdownOwnWork();

    /// <summary>
    /// Zamkniecie konczy WLASNA prace okna: anuluje wlasny odczyt. NIE wola
    /// Dispose na koordynatorze ani na kliencie Control API - zasoby naleza do
    /// wlasciciela konta, nie do okna. Anulowanie odczytu nie ma skutkow
    /// mutujacych: ten widok nie wysyla zadnej zmiany do Sonos.
    /// </summary>
    internal void ShutdownOwnWork()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        try
        {
            windowLifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Juz zwolniony token nie jest bledem zamykania.
        }
    }

    // ================= kwity dla testow i harnessu =================

    internal bool IsClosedForWork => closed;

    internal bool IsBusy => busy;

    internal string InstructionText => InstructionBox.Text;

    internal IReadOnlyList<string> GroupLabels => Snapshot(GroupsList);

    internal IReadOnlyList<string> PlayerLabels => Snapshot(PlayersList);

    internal IReadOnlyList<string> HouseholdChoices => Snapshot(HouseholdBox.Items);

    internal bool IsHouseholdChoiceVisible => HouseholdPanel.Visibility == Visibility.Visible;

    /// <summary>Nazwa kontrolki z fokusem - kwit fokusu dla testow bez GUI.</summary>
    internal string FocusedControlName =>
        FocusManager.GetFocusedElement(this) is FrameworkElement element ? element.Name : string.Empty;

    private static IReadOnlyList<string> Snapshot(ListBox list) => Snapshot(list.Items);

    private static IReadOnlyList<string> Snapshot(System.Collections.IEnumerable items)
    {
        var labels = new List<string>();
        foreach (var item in items)
        {
            labels.Add(item as string ?? string.Empty);
        }

        return labels;
    }
}
