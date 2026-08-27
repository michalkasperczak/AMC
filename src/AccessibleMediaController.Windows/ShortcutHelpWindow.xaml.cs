using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Presentation;

namespace AccessibleMediaController.Windows;

public partial class ShortcutHelpWindow : Window
{
    private readonly IReadOnlyList<ShortcutHelpSection> _allSections;
    private bool _refreshing;

    public ShortcutHelpWindow(IReadOnlyList<ShortcutHelpSection> sections)
    {
        _allSections = sections;
        InitializeComponent();
        ShowSections(_allSections);
    }

    public string? SelectedCommandId { get; private set; }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        ShortcutFilterBox.Focus();
        Keyboard.Focus(ShortcutFilterBox);
        ShortcutFilterBox.SelectAll();
    }

    private void ShortcutFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_refreshing) return;
        var query = ShortcutFilterBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowSections(_allSections);
            return;
        }

        var matches = ShortcutHelpCatalog.Filter(_allSections, query);
        ShowSections([
            new ShortcutHelpSection(
                "search-results",
                $"Wyniki wyszukiwania: {FormatCount(matches.Count)}",
                matches)
        ]);
    }

    private void ShowSections(IReadOnlyList<ShortcutHelpSection> sections)
    {
        _refreshing = true;
        SectionsList.ItemsSource = sections;
        SectionsList.SelectedIndex = sections.Count > 0 ? 0 : -1;
        RefreshSelectedSection();
        _refreshing = false;
    }

    private void SectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing) return;
        RefreshSelectedSection();
    }

    private void RefreshSelectedSection()
    {
        var section = SectionsList.SelectedItem as ShortcutHelpSection;
        ShortcutsHeading.Text = section is null ? "Skróty" : $"Skróty: {section.Label}";
        ShortcutsList.ItemsSource = section?.Entries ?? [];
        ShortcutsList.SelectedIndex = section?.Entries.Count > 0 ? 0 : -1;
        UpdateSelectedShortcut();
        HelpStatus.Text = section is null
            ? "Brak pasujących skrótów"
            : $"{section.Label}: {FormatCount(section.Entries.Count)}";
    }

    private void ShortcutsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSelectedShortcut();

    private void UpdateSelectedShortcut()
    {
        var entry = ShortcutsList.SelectedItem as ShortcutHelpEntry;
        ExecuteButton.IsEnabled = entry?.CanExecute == true;
        System.Windows.Automation.AutomationProperties.SetHelpText(
            ExecuteButton,
            entry?.CanExecute == true
                ? $"Wykonaj: {entry.DisplayName}"
                : "Wybrany wpis tylko objaśnia działanie klawisza.");
    }

    private void ShortcutFilterBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Enter)) return;
        FocusSections();
        e.Handled = true;
    }

    private void SectionsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Right)
        {
            FocusShortcuts();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && SectionsList.SelectedIndex == 0)
        {
            FocusFilterAtEnd();
            e.Handled = true;
        }
    }

    private void ShortcutsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Left || e.Key == Key.Up && ShortcutsList.SelectedIndex == 0)
        {
            FocusSections();
            e.Handled = true;
        }
    }

    private void FocusFilterAtEnd()
    {
        ShortcutFilterBox.Focus();
        Keyboard.Focus(ShortcutFilterBox);
        ShortcutFilterBox.CaretIndex = ShortcutFilterBox.Text.Length;
        ShortcutFilterBox.SelectionLength = 0;
    }

    private void FocusSections()
    {
        if (SectionsList.SelectedItem is null)
        {
            HelpStatus.Announce("Brak pasujących skrótów");
            FocusFilterAtEnd();
            return;
        }
        FocusSelectedListItem(SectionsList);
    }

    private void FocusShortcuts()
    {
        if (ShortcutsList.SelectedItem is null)
        {
            HelpStatus.Announce("Wybrana sekcja nie zawiera skrótów");
            return;
        }
        FocusSelectedListItem(ShortcutsList);
    }

    private void FocusSelectedListItem(ListBox list)
    {
        list.ScrollIntoView(list.SelectedItem);
        list.Focus();
        Dispatcher.BeginInvoke(() =>
        {
            if (list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is ListBoxItem item)
            {
                item.Focus();
                Keyboard.Focus(item);
            }
        }, DispatcherPriority.Loaded);
    }

    private void ExecuteSelected()
    {
        if (ShortcutsList.SelectedItem is not ShortcutHelpEntry entry)
        {
            HelpStatus.Announce("Brak skrótu do wykonania");
            return;
        }
        if (!entry.CanExecute)
        {
            HelpStatus.Announce("To jest opis klawisza. Nie uruchamia osobnego polecenia");
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
    private void ShortcutsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private static string FormatCount(int count) => count switch
    {
        0 => "brak skrótów",
        1 => "1 skrót",
        >= 2 and <= 4 => $"{count} skróty",
        _ => $"{count} skrótów"
    };
}
