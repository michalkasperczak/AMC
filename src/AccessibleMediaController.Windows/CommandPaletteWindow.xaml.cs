using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Presentation;

namespace AccessibleMediaController.Windows;

public partial class CommandPaletteWindow : Window
{
    private readonly IReadOnlyList<CommandPaletteEntry> _allEntries;
    private string _lastFilterText = string.Empty;
    private bool _normalizingFilterText;

    public CommandPaletteWindow(IReadOnlyList<CommandPaletteEntry> entries)
    {
        _allEntries = entries;
        InitializeComponent();
        RefreshEntries();
    }

    public string? SelectedCommandId { get; private set; }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        CommandFilterBox.Focus();
        Keyboard.Focus(CommandFilterBox);
        CommandFilterBox.SelectAll();
    }

    private void CommandFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_normalizingFilterText) return;

        var enteredText = CommandFilterBox.Text;
        var normalizedText = enteredText;
        if (CommandPaletteSearch.Filter(_allEntries, enteredText).Count == 0 && enteredText.Length > 0)
        {
            var appendedText = enteredText.StartsWith(_lastFilterText, StringComparison.Ordinal)
                ? enteredText[_lastFilterText.Length..]
                : enteredText;
            normalizedText = CommandPaletteSearch.ContinueOrRestartListQuery(
                _allEntries,
                _lastFilterText,
                appendedText);
        }

        if (!string.Equals(normalizedText, enteredText, StringComparison.Ordinal))
        {
            _normalizingFilterText = true;
            CommandFilterBox.Text = normalizedText;
            CommandFilterBox.CaretIndex = normalizedText.Length;
            _normalizingFilterText = false;
        }

        _lastFilterText = normalizedText;
        RefreshEntries();
    }

    private void RefreshEntries()
    {
        var entries = CommandPaletteSearch.Filter(_allEntries, CommandFilterBox.Text);
        CommandsList.ItemsSource = entries;
        CommandsList.SelectedIndex = entries.Count > 0 ? 0 : -1;
        PaletteStatus.Text = FormatCommandCount(entries.Count);
    }

    private static string FormatCommandCount(int count) => count switch
    {
        0 => "Brak pasujących poleceń",
        1 => "1 polecenie",
        >= 2 and <= 4 => $"{count} polecenia",
        _ => $"{count} poleceń"
    };

    private void CommandFilterBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            FocusSelectedCommand();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
    }

    private void CommandsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && CommandsList.SelectedIndex == 0)
        {
            FocusFilterAtEnd();
            e.Handled = true;
        }
    }

    private void CommandsList_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        CommandFilterBox.Text = CommandPaletteSearch.ContinueOrRestartListQuery(
            _allEntries,
            CommandFilterBox.Text,
            e.Text);
        FocusFilterAtEnd();
        e.Handled = true;
    }

    private void FocusFilterAtEnd()
    {
        CommandFilterBox.Focus();
        Keyboard.Focus(CommandFilterBox);
        CommandFilterBox.CaretIndex = CommandFilterBox.Text.Length;
        CommandFilterBox.SelectionLength = 0;
    }

    private void FocusSelectedCommand()
    {
        if (CommandsList.SelectedItem is null)
        {
            PaletteStatus.Announce("Brak pasujących poleceń");
            return;
        }

        CommandsList.ScrollIntoView(CommandsList.SelectedItem);
        CommandsList.Focus();
        Dispatcher.BeginInvoke(() =>
        {
            if (CommandsList.ItemContainerGenerator.ContainerFromItem(CommandsList.SelectedItem) is ListBoxItem item)
            {
                item.Focus();
                Keyboard.Focus(item);
            }
        }, DispatcherPriority.Loaded);
    }

    private void ExecuteSelected()
    {
        if (CommandsList.SelectedItem is not CommandPaletteEntry entry)
        {
            PaletteStatus.Announce("Brak polecenia do wykonania");
            CommandFilterBox.Focus();
            return;
        }

        SelectedCommandId = entry.CommandId;
        DialogResult = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        e.Handled = true;
    }

    private void Execute_Click(object sender, RoutedEventArgs e) => ExecuteSelected();
    private void CommandsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();
}
