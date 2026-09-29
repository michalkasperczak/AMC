using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// MALY dostepny wybor domu Sonos. Okno NIE zna konta, klienta HTTP ani
/// magazynu: dostaje GOTOWA liste domow i wybrany identyfikator, a oddaje
/// wylacznie identyfikator potwierdzonego domu.
///
/// RUCH ZAZNACZENIEM NIC NIE ZAPISUJE: okno trzyma tylko swoje wiersze, wiec
/// Anuluj, Escape i zamkniecie krzyzykiem sa z definicji bezskutkowe.
///
/// Etykiety pochodza z ISTNIEJACEGO bezpiecznego formattera
/// <see cref="SonosDeviceLabels.DescribeHouseholdChoices"/> - zadnego
/// surowego ToString i zadnej wymyslonej "nazwy konta".
/// </summary>
public partial class SonosHouseholdSelectionWindow : Window
{
    private readonly ObservableCollection<HouseholdRow> _rows = [];

    internal SonosHouseholdSelectionWindow(
        IReadOnlyList<SonosHousehold> households,
        string? currentHouseholdId)
    {
        ArgumentNullException.ThrowIfNull(households);
        InitializeComponent();

        var labels = SonosDeviceLabels.DescribeHouseholdChoices(households);
        for (var index = 0; index < households.Count; index++)
        {
            _rows.Add(new HouseholdRow(households[index].Id, labels[index]));
        }

        HouseholdList.ItemsSource = _rows;
        // POCZATKOWE zaznaczenie po IDENTYFIKATORZE, nie po pozycji: zmiana
        // kolejnosci albo nazwy w chmurze nie przestawia wyboru na sasiada.
        var currentIndex = string.IsNullOrWhiteSpace(currentHouseholdId)
            ? -1
            : _rows.ToList().FindIndex(row =>
                string.Equals(row.Id, currentHouseholdId, StringComparison.Ordinal));
        HouseholdList.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;

        Loaded += (_, _) => FocusSelectedItem();
    }

    /// <summary>Identyfikator POTWIERDZONEGO domu. Null gdy nic nie potwierdzono.</summary>
    internal string? SelectedHouseholdId { get; private set; }

    /// <summary>
    /// Czy uzytkownik POTWIERDZIL wybor. Wlasna flaga, bo <c>DialogResult</c>
    /// da sie ustawic TYLKO na oknie pokazanym przez ShowDialog, a pomiar
    /// pokazuje to samo okno przez Show.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>Zaznaczony dom BEZ potwierdzenia. Sam ruch nic nie zmienia.</summary>
    internal string? HighlightedHouseholdIdForTests =>
        (HouseholdList.SelectedItem as HouseholdRow)?.Id;

    internal int RowCountForTests => _rows.Count;

    internal IReadOnlyList<string> RowLabelsForTests =>
        _rows.Select(row => row.Label).ToArray();

    internal void SelectRowForTests(int index)
    {
        if (index < 0 || index >= _rows.Count) throw new ArgumentOutOfRangeException(nameof(index));
        HouseholdList.SelectedIndex = index;
        HouseholdList.UpdateLayout();
        FocusSelectedItem();
    }

    /// <summary>
    /// PELNA droga przycisku "Wybierz" - ta sama metoda, ktora wola Click i
    /// Enter. Zadnego skrotu obok obslugi okna.
    /// </summary>
    internal void ConfirmForTests() => Confirm();

    private void Confirm()
    {
        if (HouseholdList.SelectedItem is not HouseholdRow row) return;
        SelectedHouseholdId = row.Id;
        Confirmed = true;
        CloseSelf(true);
    }

    private void CancelChoice()
    {
        // ZERO mutacji: nie oddajemy identyfikatora i nie stawiamy potwierdzenia.
        SelectedHouseholdId = null;
        Confirmed = false;
        CloseSelf(false);
    }

    /// <summary>
    /// Zamkniecie dziala i modalnie, i przy zwyklym Show. DialogResult poza
    /// ShowDialog rzuca, wiec wyjatek jest pochlaniany, a okno zamyka sie
    /// jawnie - inaczej pomiar zawisalby na oknie bez wyniku.
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

    private void Select_Click(object sender, RoutedEventArgs e) => Confirm();

    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelChoice();

    private void HouseholdList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Confirm();

    /// <summary>
    /// Escape i Enter obsluguje WLASNY kod okna, wiec zachowuja sie tak samo na
    /// oknie modalnym i na oknie pokazanym przez Show.
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

        if (e.Key == Key.Enter && HouseholdList.IsKeyboardFocusWithin)
        {
            Confirm();
            e.Handled = true;
        }
    }

    private void FocusSelectedItem()
    {
        HouseholdList.UpdateLayout();
        if (HouseholdList.ItemContainerGenerator.ContainerFromIndex(HouseholdList.SelectedIndex)
            is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }

        HouseholdList.Focus();
        Keyboard.Focus(HouseholdList);
    }

    internal sealed class HouseholdRow(string id, string label)
    {
        internal string Id { get; } = id;
        public string Label { get; } = label;
        public override string ToString() => Label;
    }
}
