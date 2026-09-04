using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public enum ChapterListAction
{
    None,
    Play,
    Save,
    Remove
}

public sealed class ChapterListRow : INotifyPropertyChanged
{
    private bool _isChosen;

    public ChapterListRow(ChapterSegment segment, int number)
    {
        Segment = segment;
        var origin = segment.Entry.ChapterOrigin == ChapterOrigin.Provider ? ", rozdział dostawcy" : string.Empty;
        AccessibleLabel = $"{number}. {segment.Name}, początek {FormatTime(segment.Start)}, "
            + $"długość {FormatTime(segment.Duration)}{origin}";
    }

    public ChapterSegment Segment { get; }
    public string AccessibleLabel { get; }
    public bool IsChosen
    {
        get => _isChosen;
        set
        {
            if (_isChosen == value) return;
            _isChosen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectionAccessibleLabel));
        }
    }
    public string SelectionAccessibleLabel => IsChosen
        ? $"Wybrany, {AccessibleLabel}"
        : $"Niewybrany, {AccessibleLabel}";
    public override string ToString() => AccessibleLabel;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
    public IReadOnlyList<ChapterSegment> SelectedChapters => _rows
        .Where(row => row.IsChosen)
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
        else if (e.Key is Key.Up or Key.Down
                 && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            var currentIndex = FindFocusedIndex();
            if (_rows.Count == 0 || currentIndex < 0)
            {
                e.Handled = true;
                return;
            }
            var targetIndex = Math.Clamp(
                currentIndex + (e.Key == Key.Up ? -1 : 1),
                0,
                _rows.Count - 1);
            AddRangeToSelection(currentIndex, targetIndex);
            FocusRow(targetIndex);
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            foreach (var row in _rows) row.IsChosen = true;
            SetSelectionStatus($"Wybrano wszystkie rozdziały: {SelectedChapters.Count}");
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
        var wasChosen = row.IsChosen;
        row.IsChosen = !wasChosen;
        FocusRow(index);
        SetSelectionStatus(
            wasChosen
                ? $"Niewybrany: {row.Segment.Name}. Razem {SelectedChapters.Count}"
                : $"Wybrany: {row.Segment.Name}. Razem {SelectedChapters.Count}");
        return true;
    }

    internal bool AddRangeToSelection(int firstIndex, int secondIndex)
    {
        if (firstIndex < 0 || secondIndex < 0
            || firstIndex >= _rows.Count || secondIndex >= _rows.Count)
        {
            return false;
        }
        var start = Math.Min(firstIndex, secondIndex);
        var end = Math.Max(firstIndex, secondIndex);
        for (var index = start; index <= end; index++) _rows[index].IsChosen = true;
        SetSelectionStatus($"Wybrano zakres. Razem {SelectedChapters.Count}");
        return true;
    }

    internal bool ChooseFocusedChapterWhenNone()
    {
        if (SelectedChapters.Count > 0) return true;
        var index = FindFocusedIndex();
        if (index < 0 || index >= _rows.Count) return false;
        _rows[index].IsChosen = true;
        return true;
    }

    private void FocusRow(int index)
    {
        if (index < 0 || index >= _rows.Count) return;
        ChapterList.SelectedIndex = index;
        ChapterList.ScrollIntoView(ChapterList.SelectedItem);
        if (ChapterList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
        {
            container.Focus();
            Keyboard.Focus(container);
        }
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
        if (!ChooseFocusedChapterWhenNone())
        {
            MessageBox.Show(this, "Wybierz co najmniej jeden rozdział.", "Rozdziały", MessageBoxButton.OK, MessageBoxImage.Information);
            ChapterList.Focus();
            return;
        }
        var selected = SelectedChapters;
        DiagnosticLog.Info(
            "chapters",
            $"Zatwierdzono listę rozdziałów; działanie: {action}; liczba: {selected.Count}; "
            + $"rozdziały: {string.Join(" | ", selected.Select(chapter => $"{chapter.Name} @ {chapter.Start:c}"))}.");
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
