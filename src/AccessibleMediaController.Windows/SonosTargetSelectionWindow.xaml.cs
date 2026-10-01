using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// WYBOR CELU STEROWANIA SONOS (Ctrl+F5).
///
/// Po co to okno istnieje: Biblioteka (Ctrl+L) pokazuje teraz MATERIAL
/// (ulubione, playlisty), wiec glosniki i grupy musza miec WLASNE, jawne
/// miejsce - i to jest wlasnie ono. Bez tego po zastapieniu listy glosnikow
/// Biblioteka nie dalo by sie wybrac celu.
///
/// Czego to okno NIE ROBI, swiadomie i sprawdzalnie:
///  * NIE GRA. Potwierdzenie nie wysyla ZADNEGO polecenia odtwarzania - wynik
///    mowi "Wybrano cel: Biuro", nigdy "gra".
///  * NIE TWORZY, nie rozwiazuje i nie przebudowuje grup, nie rusza czlonkostwa
///    glosnikow. Wybiera sie WYLACZNIE grupe JUZ ISTNIEJACA w odczytanej
///    topologii. Przyciskow przyszlych funkcji (tworzenie grupy, "Wszystkie")
///    tu NIE MA - zero martwych przyciskow.
///  * NIE CZYTA sieci samo. Dostaje GOTOWA migawke topologii; odswiezenie jest
///    JAWNE i nalezy do wlasciciela.
///
/// RUCH ZAZNACZENIEM NIC NIE ZMIENIA: cel zapisuje sie dopiero na "Ustaw jako
/// cel" (albo Enter na liscie). Escape, Zamknij i krzyzyk sa z definicji
/// bezskutkowe - okno oddaje identyfikator TYLKO po potwierdzeniu.
///
/// Etykiety pochodza z <see cref="SonosTargetSelectionLabels"/>: PELNE nazwy
/// glosnikow z topologii, zadnego "Biuro +1" i zadnego identyfikatora w widoku.
/// </summary>
public partial class SonosTargetSelectionWindow : Window
{
    private readonly ObservableCollection<GroupTargetRow> _rows = [];

    /// <summary>
    /// WARIANT Z TOPOLOGIA. <paramref name="topology"/> to ODCZYTANA migawka;
    /// <paramref name="currentGroupId"/> to dotychczasowy cel (moze byc null).
    /// </summary>
    internal SonosTargetSelectionWindow(
        SonosHouseholdTopology topology,
        string? currentGroupId)
        : this(topology, currentGroupId, unavailableReason: null)
    {
        ArgumentNullException.ThrowIfNull(topology);
    }

    /// <summary>
    /// WARIANT BEZ TOPOLOGII: nie ma zalogowania, nie ma domu albo topologia nie
    /// byla jeszcze czytana. Okno nadal sie otwiera i UCZCIWIE mowi przyczyne -
    /// zamiast pustej listy udajacej dom bez glosnikow.
    /// </summary>
    internal SonosTargetSelectionWindow(string unavailableReason)
        : this(topology: null, currentGroupId: null, unavailableReason: unavailableReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unavailableReason);
    }

    private SonosTargetSelectionWindow(
        SonosHouseholdTopology? topology,
        string? currentGroupId,
        string? unavailableReason)
    {
        InitializeComponent();

        var parts = new List<string> { SonosTargetSelectionLabels.ViewIntroduction };

        if (topology is null)
        {
            // BRAK DANYCH: przyczyna w pierwszej tresci okna, lista pusta, a
            // potwierdzenie wylaczone. Nie wybieramy niczego "w zastepstwie".
            parts.Add(unavailableReason ?? SonosTargetSelectionLabels.TopologyUnknown);
        }
        else
        {
            // KOLEJNOSC ODCZYTU zachowana 1:1. Wiersz niesie identyfikator
            // grupy, wiec potwierdzenie nie szuka jej po nazwie - dwie grupy o
            // tej samej nazwie zostaja rozroznione wewnetrznie.
            var labels = SonosTargetSelectionLabels.DescribeGroupTargets(topology);
            for (var index = 0; index < topology.Groups.Count; index++)
            {
                _rows.Add(new GroupTargetRow(topology.Groups[index].Id, labels[index]));
            }

            parts.Add(SonosTargetSelectionLabels.SummarizeCount(_rows.Count));
            if (topology.Partial)
            {
                parts.Add(SonosTargetSelectionLabels.PartialTopology);
            }
        }

        // AKTUALNY CEL opisany PELNA etykieta z listy, nie identyfikatorem.
        var currentLabel = _rows
            .FirstOrDefault(row => string.Equals(row.Id, currentGroupId, StringComparison.Ordinal))
            ?.Label;
        parts.Add(SonosTargetSelectionLabels.DescribeCurrentTarget(currentLabel));

        IntroductionText.Text = string.Join(" ", parts);

        GroupsList.ItemsSource = _rows;

        // POCZATKOWE zaznaczenie po IDENTYFIKATORZE, nie po pozycji: zmiana
        // kolejnosci albo nazwy w topologii nie przestawia wyboru na sasiada.
        var currentIndex = string.IsNullOrWhiteSpace(currentGroupId)
            ? -1
            : _rows.ToList().FindIndex(row =>
                string.Equals(row.Id, currentGroupId, StringComparison.Ordinal));
        if (_rows.Count > 0)
        {
            GroupsList.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
        }

        ConfirmButton.IsEnabled = _rows.Count > 0;
        if (_rows.Count == 0)
        {
            AutomationProperties.SetHelpText(
                ConfirmButton,
                unavailableReason ?? SonosTargetSelectionLabels.EmptyState);
        }

        AutomationProperties.SetHelpText(GroupsList,
            "Strzałki czytają kolejne grupy wraz z nazwami ich głośników. Enter albo przycisk "
            + "Ustaw jako cel kieruje tam polecenia AMC; muzyka nie zostaje uruchomiona. "
            + "Escape albo Zamknij wychodzi bez zmiany celu.");

        Loaded += (_, _) => FocusInitialElement();
    }

    /// <summary>
    /// Identyfikator POTWIERDZONEJ grupy. Null gdy nic nie potwierdzono - sam
    /// ruch zaznaczeniem go NIE ustawia.
    /// </summary>
    internal string? SelectedGroupId { get; private set; }

    /// <summary>
    /// ETYKIETA potwierdzonej grupy - do komunikatu wlasciciela, zeby nie skladal
    /// jej z identyfikatora.
    /// </summary>
    internal string? SelectedGroupLabel { get; private set; }

    /// <summary>
    /// Czy uzytkownik POTWIERDZIL wybor. Wlasna flaga, bo DialogResult da sie
    /// ustawic TYLKO na oknie pokazanym przez ShowDialog, a pomiar pokazuje to
    /// samo okno przez Show.
    /// </summary>
    internal bool Confirmed { get; private set; }

    internal int RowCountForTests => _rows.Count;

    internal IReadOnlyList<string> RowLabelsForTests => _rows.Select(row => row.Label).ToArray();

    internal string IntroductionForTests => IntroductionText.Text;

    internal string StatusForTests => StatusText.Text;

    internal bool ConfirmEnabledForTests => ConfirmButton.IsEnabled;

    internal int SelectedIndexForTests => GroupsList.SelectedIndex;

    /// <summary>Zaznaczona grupa BEZ potwierdzenia. Sam ruch nic nie zmienia.</summary>
    internal string? HighlightedGroupIdForTests =>
        (GroupsList.SelectedItem as GroupTargetRow)?.Id;

    internal bool ListHasFocusForTests =>
        GroupsList.IsKeyboardFocusWithin || GroupsList.IsKeyboardFocused;

    /// <summary>Lista grup dla POMIARU drogi klawiatury (prawdziwe zdarzenia).</summary>
    internal ListBox ListForTests => GroupsList;

    internal string FocusedElementNameForTests => Keyboard.FocusedElement switch
    {
        null => "brak",
        var element when ReferenceEquals(element, ConfirmButton) => "ConfirmButton",
        var element when ReferenceEquals(element, CloseButton) => "CloseButton",
        var element when ReferenceEquals(element, GroupsList) => "GroupsList",
        ListBoxItem => "ListBoxItem",
        var element when ReferenceEquals(element, this) => "Window",
        var element => element.GetType().Name
    };

    /// <summary>PELNA droga przycisku "Ustaw jako cel" - ta sama, ktora wola Enter.</summary>
    internal void ConfirmForTests() => Confirm();

    internal void SelectRowForTests(int index)
    {
        if (index < 0 || index >= _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        GroupsList.SelectedIndex = index;
        GroupsList.UpdateLayout();
        FocusSelectedRow();
    }

    /// <summary>
    /// POTWIERDZENIE: zapisuje identyfikator i etykiete, oglasza "Wybrano cel"
    /// (nie "gra") i zamyka okno. ZERO polecen do Sonosa.
    /// </summary>
    private void Confirm()
    {
        if (GroupsList.SelectedItem is not GroupTargetRow row)
        {
            StatusText.Announce(SonosTargetSelectionLabels.EmptyState);
            return;
        }

        SelectedGroupId = row.Id;
        SelectedGroupLabel = row.Label;
        Confirmed = true;
        CloseSelf(true);
    }

    private void CancelChoice()
    {
        // ZERO mutacji: nie oddajemy identyfikatora i nie stawiamy potwierdzenia.
        SelectedGroupId = null;
        SelectedGroupLabel = null;
        Confirmed = false;
        CloseSelf(false);
    }

    /// <summary>
    /// Zamkniecie dziala i modalnie, i przy zwyklym Show. DialogResult poza
    /// ShowDialog rzuca, wiec wyjatek jest pochlaniany, a okno zamyka sie jawnie.
    /// </summary>
    private void CloseSelf(bool result)
    {
        try
        {
            DialogResult = result;
            return;
        }
        catch (InvalidOperationException)
        {
        }

        Close();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Close_Click(object sender, RoutedEventArgs e) => CancelChoice();

    /// <summary>
    /// Escape i Enter obsluguje WLASNY kod okna, wiec zachowuja sie tak samo na
    /// oknie modalnym i na oknie pokazanym przez Show. Autorepeat nie dubluje
    /// potwierdzenia.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.Key == Key.Escape)
        {
            CancelChoice();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || !GroupsList.IsKeyboardFocusWithin) return;

        e.Handled = true;
        if (e.IsRepeat) return;
        Confirm();
    }

    private void FocusInitialElement()
    {
        GroupsList.UpdateLayout();
        if (_rows.Count > 0 && FocusSelectedRow()) return;

        GroupsList.Focus();
        Keyboard.Focus(GroupsList);
    }

    private bool FocusSelectedRow()
    {
        if (GroupsList.ItemContainerGenerator.ContainerFromIndex(GroupsList.SelectedIndex)
            is not ListBoxItem item)
        {
            return false;
        }

        item.Focus();
        Keyboard.Focus(item);
        return true;
    }

    /// <summary>
    /// WIERSZ: identyfikator grupy WEWNETRZNIE, bezpieczna etykieta na widoku.
    /// Dzieki temu potwierdzenie oddaje IDENTYFIKATOR z wiersza, a nie nazwe -
    /// i dwie grupy o tej samej nazwie nie zamieniaja sie miejscami.
    /// </summary>
    internal sealed class GroupTargetRow(string id, string label)
    {
        internal string Id { get; } = id;

        public string Label { get; } = label;

        public override string ToString() => Label;
    }
}
