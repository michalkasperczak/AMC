using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DemoMediaSession> _sourceSessions;
    private readonly bool _allServices;
    private readonly Func<MediaItem, string> _formatItem;
    private readonly Func<IReadOnlyList<SearchResult>, SearchResultAction, bool, string?> _executeAction;
    private readonly string _resultHelpText;
    private readonly SearchQueryHistory _searchHistory;
    private readonly string _searchHistoryScope;
    private readonly Action _persistSearchHistory;
    private bool _isApplyingHistory;
    private bool _isBrowsingHistory;
    private int _historyIndex = -1;

    public SearchWindow(
        SessionManager sessions,
        bool allServices,
        Func<MediaItem, string> formatItem,
        Func<IReadOnlyList<SearchResult>, SearchResultAction, bool, string?> executeAction,
        SearchQueryHistory searchHistory,
        string searchHistoryScope,
        Action persistSearchHistory,
        bool detailedHints)
    {
        InitializeComponent();
        var modeName = allServices
            ? "Szukaj we wszystkich usługach"
            : $"Szukaj w {sessions.Current.DisplayName}";
        Title = $"{modeName} — AMC";
        HeadingText.Text = modeName;
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetName(SearchBox, modeName);
        ScopeText.Text = allServices
            ? "Wyniki mogą pochodzić ze wszystkich włączonych usług."
            : $"Zakres: {sessions.Current.DisplayName}.";

        _sourceSessions = allServices ? sessions.Sessions : [sessions.Current];
        _allServices = allServices;
        _formatItem = formatItem;
        _executeAction = executeAction;
        _searchHistory = searchHistory;
        _searchHistoryScope = searchHistoryScope;
        _persistSearchHistory = persistSearchHistory;
        _resultHelpText = detailedHints
            ? "Strzałki wybierają wynik. Enter otwiera. Escape zamyka okno."
            : string.Empty;

        if (detailedHints)
        {
            AutomationProperties.SetHelpText(
                SearchBox,
                "Wpisz tekst i naciśnij Enter, aby rozpocząć wyszukiwanie. Przy pustym polu strzałka w dół wybiera historię. Escape zamyka okno.");
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

        if (_searchHistory.Record(_searchHistoryScope, query))
        {
            _persistSearchHistory();
        }
        ResetHistoryBrowsing();

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
            var results = action is SearchResultAction.CopyName or SearchResultAction.CopyLocation
                ? ResultsList.Items
                    .OfType<SearchResultRow>()
                    .Where(candidate => ResultsList.SelectedItems.Contains(candidate))
                    .Select(candidate => new SearchResult(candidate.SessionId, candidate.Item))
                    .ToArray()
                : [result];
            var announcement = _executeAction(results, action, _allServices);
            if (action is not (SearchResultAction.CopyName
                or SearchResultAction.CopyLocation))
            {
                LastDirectActionResult = result;
            }
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
        else if (e.Key == Key.Down
                 && (SearchBox.Text.Length == 0 || _isBrowsingHistory)
                 && BrowseHistory(1))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Up && _isBrowsingHistory && BrowseHistory(-1))
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            FocusSelectedResult();
            e.Handled = true;
        }
    }

    private bool BrowseHistory(int direction)
    {
        var entries = _searchHistory.GetEntries(_searchHistoryScope);
        if (entries.Count == 0)
        {
            if (SearchBox.Text.Length == 0)
            {
                SearchStatus.Announce("Historia wyszukiwania jest pusta");
                return true;
            }
            return false;
        }

        if (!_isBrowsingHistory)
        {
            if (SearchBox.Text.Length != 0 || direction < 0) return false;
            _isBrowsingHistory = true;
            _historyIndex = 0;
        }
        else
        {
            _historyIndex += direction;
            if (_historyIndex < 0)
            {
                ApplyHistoryText(string.Empty);
                ResetHistoryBrowsing();
                SearchStatus.Text = "Puste pole wyszukiwania";
                return true;
            }
            _historyIndex = Math.Min(_historyIndex, entries.Count - 1);
        }

        ApplyHistoryText(entries[_historyIndex]);
        SearchStatus.Text = $"{_historyIndex + 1} z {entries.Count} w historii";
        return true;
    }

    private void ApplyHistoryText(string text)
    {
        _isApplyingHistory = true;
        try
        {
            SearchBox.Text = text;
            SearchBox.CaretIndex = text.Length;
            SearchBox.SelectAll();
        }
        finally
        {
            _isApplyingHistory = false;
        }
    }

    private void ResetHistoryBrowsing()
    {
        _isBrowsingHistory = false;
        _historyIndex = -1;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_isApplyingHistory) ResetHistoryBrowsing();
    }

    private void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && key is Key.X or Key.V)
        {
            SearchStatus.Announce(key == Key.X
                ? "Wycinanie plików nie działa na liście wyników wyszukiwania"
                : "Wklejanie plików nie działa na liście wyników wyszukiwania");
            Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
            e.Handled = true;
            return;
        }
        SearchResultAction? action = null;

        if (key == Key.Enter && modifiers == ModifierKeys.None)
            action = SearchResultAction.Open;
        else if (key == Key.Enter && modifiers == ModifierKeys.Control)
            action = SearchResultAction.TogglePlayback;
        else if (key == Key.Enter && modifiers == ModifierKeys.Shift)
            action = SearchResultAction.Queue;
        else if (key == Key.Enter && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.PlayNext;
        else if (key == Key.U && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.Favorite;
        else if (key == Key.Enter && modifiers == ModifierKeys.Alt)
            action = SearchResultAction.Information;
        else if (key == Key.C && modifiers == ModifierKeys.Control)
            action = SearchResultAction.CopyName;
        else if (key == Key.C && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.CopyLocation;

        if (action is null) return;
        CompleteSelected(action.Value);
        e.Handled = true;
    }

    private void Search_Click(object sender, RoutedEventArgs e) => RunSearch();
    private void Open_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Open);
    private void TogglePlayback_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.TogglePlayback);
    private void PlayNext_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.PlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Queue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Favorite);
    private void Information_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Information);
    private void CopyName_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.CopyName);
    private void CopyLocation_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.CopyLocation);
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => CompleteSelected(SearchResultAction.Open);

    private void ResultsContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        // The event is kept to make context-menu focus behavior symmetrical
        // with the main list. Clipboard commands operate on all selected rows.
    }

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
    TogglePlayback,
    PlayNext,
    Queue,
    Favorite,
    Information,
    CopyName,
    CopyLocation
}
