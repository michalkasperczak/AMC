using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DemoMediaSession> _sourceSessions;
    private readonly bool _allServices;
    private readonly Func<MediaItem, string> _formatItem;
    private readonly Func<SearchResult, SearchResultAction, bool, string?> _executeAction;
    private readonly string _resultHelpText;

    public SearchWindow(
        SessionManager sessions,
        bool allServices,
        Func<MediaItem, string> formatItem,
        Func<SearchResult, SearchResultAction, bool, string?> executeAction,
        bool detailedHints)
    {
        InitializeComponent();
        var modeName = allServices
            ? "Szukaj we wszystkich usługach"
            : $"Szukaj w usłudze {sessions.Current.DisplayName}";
        Title = $"{modeName} — AMC";
        HeadingText.Text = modeName;
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetName(SearchBox, $"{modeName}. Wyszukiwany tekst");
        ScopeText.Text = allServices
            ? "Wyniki mogą pochodzić ze wszystkich włączonych usług."
            : $"Zakres: {sessions.Current.DisplayName}.";

        _sourceSessions = allServices ? sessions.Sessions : [sessions.Current];
        _allServices = allServices;
        _formatItem = formatItem;
        _executeAction = executeAction;
        _resultHelpText = detailedHints
            ? "Strzałki wybierają wynik. Enter otwiera. Escape zamyka okno."
            : string.Empty;

        if (detailedHints)
        {
            AutomationProperties.SetHelpText(
                SearchBox,
                "Wpisz tekst i naciśnij Enter, aby rozpocząć wyszukiwanie. Escape zamyka okno.");
        }

        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        };
    }

    public SearchResult? SelectedResult { get; private set; }
    public SearchResult? LastDirectActionResult { get; private set; }
    public SearchResultAction SelectedAction { get; private set; } = SearchResultAction.Open;

    private void RunSearch()
    {
        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            SearchStatus.Announce("Wpisz tekst do wyszukania");
            SearchBox.Focus();
            return;
        }

        var results = MediaCatalogSearch.Search(_sourceSessions, query)
            .Select(result => new SearchResultRow(
                result.Session.Id,
                result.Item,
                result.Item.PrimaryText,
                _allServices
                    ? $"{_formatItem(result.Item)}, {result.Session.DisplayName}"
                    : _formatItem(result.Item),
                _resultHelpText))
            .ToList();
        ResultsList.ItemsSource = results;
        if (results.Count == 0)
        {
            ResultsList.SelectedIndex = -1;
            SearchStatus.Announce("Brak wyników. Zmień wyszukiwany tekst");
            SearchBox.Focus();
            SearchBox.SelectAll();
            return;
        }

        ResultsList.SelectedIndex = 0;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        ResultsList.Focus();
        Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.Loaded);
        // The focused ListBoxItem already exposes its label and position (for example
        // "1 z 3"). Keep the visible count without raising a second live announcement.
        SearchStatus.Text = results.Count == 1 ? "1 wynik" : $"{results.Count} wyników";
    }

    private void FocusSelectedResult()
    {
        if (ResultsList.ItemContainerGenerator.ContainerFromItem(ResultsList.SelectedItem) is not System.Windows.Controls.ListBoxItem item)
        {
            ResultsList.Focus();
            return;
        }
        item.Focus();
        Keyboard.Focus(item);
    }

    private void CompleteSelected(SearchResultAction action)
    {
        if (ResultsList.SelectedItem is not SearchResultRow row)
        {
            SearchStatus.Announce("Najpierw wykonaj wyszukiwanie i wybierz wynik");
            SearchBox.Focus();
            return;
        }

        var result = new SearchResult(row.SessionId, row.Item);
        if (action != SearchResultAction.Open)
        {
            var announcement = _executeAction(result, action, _allServices);
            LastDirectActionResult = result;
            if (!string.IsNullOrWhiteSpace(announcement)) SearchStatus.Announce(announcement);
            Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
            return;
        }

        SelectedResult = result;
        SelectedAction = action;
        DialogResult = true;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            FocusSelectedResult();
            e.Handled = true;
        }
    }

    private void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        SearchResultAction? action = null;

        if (key == Key.Enter && modifiers == ModifierKeys.None)
            action = SearchResultAction.Open;
        else if (key == Key.Enter && modifiers == ModifierKeys.Control)
            action = SearchResultAction.Play;
        else if (key == Key.Enter && modifiers == ModifierKeys.Shift)
            action = SearchResultAction.Queue;
        else if (key == Key.Enter && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.PlayNext;
        else if (key == Key.U && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.Favorite;
        else if (key == Key.Enter && modifiers == ModifierKeys.Alt)
            action = SearchResultAction.Information;

        if (action is null) return;
        CompleteSelected(action.Value);
        e.Handled = true;
    }

    private void Search_Click(object sender, RoutedEventArgs e) => RunSearch();
    private void Open_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Open);
    private void Play_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Play);
    private void PlayNext_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.PlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Queue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Favorite);
    private void Information_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Information);
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => CompleteSelected(SearchResultAction.Open);

    private void ResultsContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
    }

    public sealed record SearchResult(string SessionId, MediaItem Item);

    private sealed record SearchResultRow(
        string SessionId,
        MediaItem Item,
        string NavigationText,
        string Label,
        string Hint)
    {
        public override string ToString() => Label;
    }
}

public enum SearchResultAction
{
    Open,
    Play,
    PlayNext,
    Queue,
    Favorite,
    Information
}
