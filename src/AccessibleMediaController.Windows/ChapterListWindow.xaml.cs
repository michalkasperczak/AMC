using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows;

public enum ChapterListAction
{
    None,
    Play,
    Save,
    Remove
}

public sealed class ChapterListRow
{
    public ChapterListRow(ChapterSegment segment, int number)
    {
        Segment = segment;
        var origin = segment.Entry.ChapterOrigin == ChapterOrigin.Provider ? ", rozdział dostawcy" : string.Empty;
        AccessibleLabel = $"{number}. {segment.Name}, początek {FormatTime(segment.Start)}, "
            + $"długość {FormatTime(segment.Duration)}{origin}";
    }

    public ChapterSegment Segment { get; }
    public string AccessibleLabel { get; }
    public string SelectedAccessibleLabel => $"Zaznaczony, {AccessibleLabel}";
    public string UnselectedAccessibleLabel => $"Niezaznaczony, {AccessibleLabel}";
    public override string ToString() => AccessibleLabel;

    private static string FormatTime(TimeSpan value) => CommandRouter.FormatTime(value);
}

public partial class ChapterListWindow : Controls.AccessibleWindow
{
    private readonly ObservableCollection<ChapterListRow> _rows;
    private readonly int _initialIndex;

    public ChapterListWindow(string itemTitle, IReadOnlyList<ChapterSegment> chapters, TimeSpan position)
    {
        InitializeComponent();
        Title = $"Rozdziały — {itemTitle}";
        HeadingText.Text = $"Rozdziały: {itemTitle}. {chapters.Count} elementów.";
        System.Windows.Automation.AutomationProperties.SetName(HeadingText, HeadingText.Text);
        System.Windows.Automation.AutomationProperties.SetName(ChapterList, $"Rozdziały: {itemTitle}");
        _rows = new ObservableCollection<ChapterListRow>(
            chapters.Select((chapter, index) => new ChapterListRow(chapter, index + 1)));
        ChapterList.ItemsSource = _rows;
        _initialIndex = FindInitialIndex(chapters, position);
    }

    public ChapterListAction Action { get; private set; }
    public IReadOnlyList<ChapterSegment> SelectedChapters => ChapterList.SelectedItems
        .OfType<ChapterListRow>()
        .Select(row => row.Segment)
        .OrderBy(segment => segment.Start)
        .ToArray();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_rows.Count > 0)
        {
            ChapterList.SelectedIndex = Math.Clamp(_initialIndex, 0, _rows.Count - 1);
            ChapterList.ScrollIntoView(ChapterList.SelectedItem);
        }
        ChapterList.Focus();
    }

    private void Play_Click(object sender, RoutedEventArgs e) => Complete(ChapterListAction.Play);
    private void Save_Click(object sender, RoutedEventArgs e) => Complete(ChapterListAction.Save);
    private void Remove_Click(object sender, RoutedEventArgs e) => Complete(ChapterListAction.Remove);

    private void ChapterList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space
            && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Control)
        {
            ToggleSelectionAt(FindFocusedIndex());
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ChapterList.SelectAll();
            SetSelectionStatus($"Zaznaczono wszystkie rozdziały: {SelectedChapters.Count}");
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Complete(ChapterListAction.Play);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            Complete(ChapterListAction.Remove);
            e.Handled = true;
        }
    }

    internal bool ToggleSelectionAt(int index)
    {
        if (index < 0 || index >= _rows.Count) return false;
        var row = _rows[index];
        var selected = ChapterList.SelectedItems.Contains(row);
        if (selected) ChapterList.SelectedItems.Remove(row);
        else ChapterList.SelectedItems.Add(row);
        if (ChapterList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
        {
            container.Focus();
            Keyboard.Focus(container);
        }
        SetSelectionStatus(
            selected
                ? $"Odznaczono: {row.Segment.Name}. Wybrano {SelectedChapters.Count}"
                : $"Zaznaczono: {row.Segment.Name}. Wybrano {SelectedChapters.Count}");
        return true;
    }

    private int FindFocusedIndex()
    {
        if (Keyboard.FocusedElement is DependencyObject focused
            && ItemsControl.ContainerFromElement(ChapterList, focused) is ListBoxItem container)
        {
            return ChapterList.ItemContainerGenerator.IndexFromContainer(container);
        }
        return ChapterList.SelectedIndex;
    }

    private void SetSelectionStatus(string message)
    {
        SelectionStatusText.Announce(message);
    }

    private void Complete(ChapterListAction action)
    {
        if (SelectedChapters.Count == 0)
        {
            MessageBox.Show(this, "Wybierz co najmniej jeden rozdział.", "Rozdziały", MessageBoxButton.OK, MessageBoxImage.Information);
            ChapterList.Focus();
            return;
        }
        Action = action;
        DialogResult = true;
    }

    private static int FindInitialIndex(IReadOnlyList<ChapterSegment> chapters, TimeSpan position)
    {
        if (chapters.Count == 0) return -1;
        for (var index = chapters.Count - 1; index >= 0; index--)
        {
            if (chapters[index].Start <= position) return index;
        }
        return 0;
    }
}
