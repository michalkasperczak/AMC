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
///   * przy zajetosci przyciski NIE znikaja: zostaja wylaczone, wiec fokus nie
///     ucieka spod czytnika ekranu,
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

    private bool busy;
    private bool closed;
    private bool suppressSelectionReload;

    /// <summary>
    /// Numer NAJNOWSZEGO zadania odczytu. Wynik ze starszym numerem jest
    /// porzucany, wiec przeterminowana odpowiedz po starym wyborze domu nie
    /// podmienia listy.
    /// </summary>
    private long requestSequence;

    public string LastAnnouncement { get; private set; } = string.Empty;

    public int AnnouncementCount { get; private set; }

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
    /// </summary>
    private void ApplyBusy()
    {
        RefreshButton.IsEnabled = !busy;
        HouseholdBox.IsEnabled = !busy && households.Count > 1;
        GroupsList.IsEnabled = !busy;
        PlayersList.IsEnabled = !busy;

        // Wyjscie dziala zawsze - inaczej uzytkownik zostaje uwieziony w czekaniu.
        CloseButton.IsEnabled = true;
        CloseButton.Visibility = Visibility.Visible;
        RefreshButton.Visibility = Visibility.Visible;
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

        // Jeden dom jest naturalnie domyslny; przy wielu tez zaczynamy od
        // pierwszego, zeby lista grup nie byla pusta bez powodu.
        HouseholdBox.SelectedIndex = households.Count > 0 ? 0 : -1;
        suppressSelectionReload = false;

        HouseholdPanel.Visibility = households.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        var summary = SonosDeviceLabels.DescribeHouseholds(result);
        SetInstruction(summary);
        Announce(summary);

        if (households.Count == 0)
        {
            SelectedHouseholdId = null;
            ClearLists();
            return;
        }

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
        foreach (var group in topology.Groups)
        {
            GroupsList.Items.Add(SonosDeviceLabels.DescribeGroup(group));
        }

        PlayersList.Items.Clear();
        foreach (var player in topology.Players)
        {
            PlayersList.Items.Add(SonosDeviceLabels.DescribePlayer(player, topology));
        }

        SetInstruction(summary);
        Announce(summary);
    }

    private void ClearLists()
    {
        GroupsList.Items.Clear();
        PlayersList.Items.Clear();
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
