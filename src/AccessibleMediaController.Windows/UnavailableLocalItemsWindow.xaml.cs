using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AccessibleMediaController.Windows;

public sealed record UnavailableLocalItemRow(string Id, string Title, string Path)
{
    public string Label => $"{Title}, {Path}";

    public override string ToString() => Label;
}

public partial class UnavailableLocalItemsWindow : Window
{
    private readonly string _sourceId;
    private readonly Func<string, IReadOnlyCollection<string>, LocalSourceActionResult> _forgetItems;
    private readonly ObservableCollection<UnavailableLocalItemRow> _rows;

    public UnavailableLocalItemsWindow(
        string sourceId,
        string sourceDisplayName,
        IReadOnlyList<UnavailableLocalItemRow> rows,
        Func<string, IReadOnlyCollection<string>, LocalSourceActionResult> forgetItems)
    {
        InitializeComponent();
        _sourceId = sourceId;
        _forgetItems = forgetItems;
        _rows = new ObservableCollection<UnavailableLocalItemRow>(rows);
        Title = $"Niedostępne pliki — {sourceDisplayName}";
        HeadingText.Text = $"Niedostępne pliki — {sourceDisplayName}";
        UnavailableItemsList.ItemsSource = _rows;
        Loaded += (_, _) => Dispatcher.BeginInvoke(FocusFirstItem, DispatcherPriority.ContextIdle);
        UpdateSelectionState();
    }

    public string LastResultMessage { get; private set; } = string.Empty;

    private void ForgetSelected_Click(object sender, RoutedEventArgs e) => ForgetSelectedItems();

    private void ForgetSelectedItems()
    {
        var selected = UnavailableItemsList.SelectedItems
            .OfType<UnavailableLocalItemRow>()
            .ToArray();
        if (selected.Length == 0)
        {
            OperationStatusText.Text = "Zaznacz co najmniej jeden niedostępny plik.";
            return;
        }

        var subject = selected.Length == 1
            ? $"„{selected[0].Title}”"
            : $"{selected.Length} plików";
        var answer = MessageBox.Show(
            this,
            $"Zapomnieć w AMC {subject}?\n\n"
            + "Z AMC zostaną usunięte: rekord Biblioteki, powiązania z Ulubionymi, Kolejką i playlistami, Historia, Zakładki oraz zapamiętana pozycja. "
            + "Żaden plik na dysku nie zostanie zmieniony. Jeżeli plik później wróci, zostanie dodany jako nowy rekord.",
            "Zapomnij niedostępne pliki w AMC",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            OperationStatusText.Text = "Anulowano. Dane AMC pozostały bez zmian.";
            return;
        }

        try
        {
            var result = _forgetItems(_sourceId, selected.Select(row => row.Id).ToArray());
            foreach (var row in selected) _rows.Remove(row);
            LastResultMessage = result.Message;
            OperationStatusText.Text = result.Message;
            UpdateSelectionState();
            Dispatcher.BeginInvoke(FocusFirstItem, DispatcherPriority.ContextIdle);
        }
        catch (Exception exception)
        {
            OperationStatusText.Text = $"Nie można zapomnieć rekordów w AMC: {exception.Message}";
        }
    }

    private void UnavailableItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSelectionState();

    private void UpdateSelectionState()
    {
        ForgetSelectedButton.IsEnabled = UnavailableItemsList.SelectedItems.Count > 0;
        if (_rows.Count == 0 && string.IsNullOrWhiteSpace(OperationStatusText.Text))
        {
            OperationStatusText.Text = "Brak niedostępnych plików w tym folderze.";
        }
    }

    private void FocusFirstItem()
    {
        if (_rows.Count == 0)
        {
            UnavailableItemsList.Focus();
            Keyboard.Focus(UnavailableItemsList);
            return;
        }

        if (UnavailableItemsList.SelectedIndex < 0) UnavailableItemsList.SelectedIndex = 0;
        UnavailableItemsList.ScrollIntoView(UnavailableItemsList.SelectedItem);
        UnavailableItemsList.UpdateLayout();
        if (UnavailableItemsList.ItemContainerGenerator.ContainerFromItem(UnavailableItemsList.SelectedItem)
            is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        UnavailableItemsList.Focus();
        Keyboard.Focus(UnavailableItemsList);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && UnavailableItemsList.IsKeyboardFocusWithin)
        {
            ForgetSelectedItems();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control
            && UnavailableItemsList.IsKeyboardFocusWithin)
        {
            UnavailableItemsList.SelectAll();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Escape) return;
        Close();
        e.Handled = true;
    }
}
