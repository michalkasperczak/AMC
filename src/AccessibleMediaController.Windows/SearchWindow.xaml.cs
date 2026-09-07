using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using AccessibleMediaController.Windows.Services;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DemoMediaSession> _sourceSessions;
    private readonly bool _allServices;
    private readonly Func<MediaItem, string> _formatItem;
    private readonly Func<MediaItem, string> _formatQuickInformation;
    private readonly Func<
        IReadOnlyList<SearchResult>,
        IReadOnlyList<SearchResult>,
        SearchResultAction,
        bool,
        string?> _executeAction;
    private readonly string _resultHelpText;
    private readonly SearchQueryHistory _searchHistory;
    private readonly string _searchHistoryScope;
    private readonly Action _persistSearchHistory;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<SearchResult>>>? _prepareRemoteSearch;
    private readonly string _remoteSearchLabel;
    private CancellationTokenSource? _searchCancellation;
    private bool _isApplyingHistory;
    private bool _isBrowsingHistory;
    private int _historyIndex = -1;
    private int _selectionAnchorIndex = -1;
    private int _selectionLeadIndex = -1;

    public SearchWindow(
        SessionManager sessions,
        bool allServices,
        Func<MediaItem, string> formatItem,
        Func<MediaItem, string> formatQuickInformation,
        Func<
            IReadOnlyList<SearchResult>,
            IReadOnlyList<SearchResult>,
            SearchResultAction,
            bool,
            string?> executeAction,
        SearchQueryHistory searchHistory,
        string searchHistoryScope,
        Action persistSearchHistory,
        bool detailedHints,
        Func<string, CancellationToken, Task<IReadOnlyList<SearchResult>>>? prepareRemoteSearch = null,
        string remoteSearchLabel = "katalogu")
    {
        InitializeComponent();
        MenuAccessibility.NormalizeContextMenu(ResultsList.ContextMenu);
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
        _formatQuickInformation = formatQuickInformation;
        _executeAction = executeAction;
        _searchHistory = searchHistory;
        _searchHistoryScope = searchHistoryScope;
        _persistSearchHistory = persistSearchHistory;
        _prepareRemoteSearch = prepareRemoteSearch;
        _remoteSearchLabel = remoteSearchLabel;
        _resultHelpText = detailedHints
            ? "Strzałki wybierają wynik. Enter przechodzi do wyniku na właściwej liście. Escape zamyka okno."
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
        Closed += (_, _) =>
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
        };
    }

    public SearchResult? SelectedResult { get; private set; }
    public IReadOnlyList<SearchResult> SelectedResults { get; private set; } = [];
    public SearchResult? LastDirectActionResult { get; private set; }
    public SearchResultAction SelectedAction { get; private set; } = SearchResultAction.Open;

    private async void RunSearch()
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

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        IReadOnlyList<SearchResult> remoteResults = [];
        if (_prepareRemoteSearch is not null)
        {
            SearchButton.IsEnabled = false;
            SearchStatus.Announce($"Wyszukiwanie w {_remoteSearchLabel}");
            try
            {
                remoteResults = await _prepareRemoteSearch(query, _searchCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                SearchStatus.Announce($"Wyszukiwanie w {_remoteSearchLabel} jest chwilowo niedostępne. Pokazuję zapisane elementy");
            }
            finally
            {
                SearchButton.IsEnabled = true;
            }
        }

        var podcastOnly = !_allServices
            && _sourceSessions.Count == 1
            && string.Equals(_sourceSessions[0].Id, "podcasts", StringComparison.OrdinalIgnoreCase);
        var combinedResults = MergeSearchResults(
            MediaCatalogSearch.Search(_sourceSessions, query),
            remoteResults,
            _sourceSessions,
            podcastOnly);
        var results = combinedResults
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
        _selectionAnchorIndex = 0;
        _selectionLeadIndex = 0;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        ResultsList.Focus();
        _ = Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.Loaded);
        // The focused ListBoxItem already exposes its label and position (for example
        // "1 z 3"). Keep the visible count without raising a second live announcement.
        SearchStatus.Text = results.Count == 1 ? "1 wynik" : $"{results.Count} wyników";
    }

    internal static IReadOnlyList<MediaSearchResult> MergeSearchResults(
        IEnumerable<MediaSearchResult> localResults,
        IEnumerable<SearchResult> remoteResults,
        IEnumerable<DemoMediaSession> sourceSessions,
        bool podcastOnly)
    {
        var sessionsById = sourceSessions
            .GroupBy(session => session.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var combined = localResults
            .Concat(remoteResults
                .Where(result => sessionsById.ContainsKey(result.SessionId))
                .Select(result => new MediaSearchResult(sessionsById[result.SessionId], result.Item)))
            .DistinctBy(result => (result.Session.Id.ToUpperInvariant(), result.Item.Id))
            .ToList();

        if (!podcastOnly) return combined;

        return combined
            .OrderBy(result => result.Item.Kind switch
            {
                MediaItemKind.Podcast => 0,
                MediaItemKind.Episode => 1,
                _ => 2
            })
            .ThenBy(result => result.Item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(result => result.Item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private void FocusSelectedResult()
    {
        var index = _selectionLeadIndex >= 0
            && _selectionLeadIndex < ResultsList.Items.Count
            && ResultsList.SelectedItems.Contains(ResultsList.Items[_selectionLeadIndex])
                ? _selectionLeadIndex
                : ResultsList.SelectedIndex;
        if (index < 0
            || ResultsList.ItemContainerGenerator.ContainerFromIndex(index) is not System.Windows.Controls.ListBoxItem item)
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
        if (action is SearchResultAction.PlayNext
                or SearchResultAction.Queue
            && GetSelectedResults().Any(selected =>
                string.Equals(selected.SessionId, "radio", StringComparison.OrdinalIgnoreCase)))
        {
            SearchStatus.Announce("Ta funkcja nie jest dostępna w Radiu internetowym");
            Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
            return;
        }
        if (action is not (SearchResultAction.Open or SearchResultAction.Playlist
                or SearchResultAction.Preset or SearchResultAction.GoToPodcast))
        {
            var results = action is SearchResultAction.CopyName
                or SearchResultAction.CopyLocation
                or SearchResultAction.Download
                or SearchResultAction.SaveAs
                or SearchResultAction.PlayNext
                or SearchResultAction.Queue
                or SearchResultAction.Favorite
                or SearchResultAction.Library
                ? GetSelectedResults()
                : [result];
            if (action is SearchResultAction.PlayNext
                    or SearchResultAction.Queue
                    or SearchResultAction.Favorite
                    or SearchResultAction.Library
                && results.Select(selected => selected.SessionId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Skip(1)
                    .Any())
            {
                SearchStatus.Announce("Dla jednego działania wybierz wyniki z tej samej usługi");
                Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
                return;
            }
            var visibleResults = ResultsList.Items
                .OfType<SearchResultRow>()
                .Select(candidate => new SearchResult(candidate.SessionId, candidate.Item))
                .ToArray();
            var announcement = _executeAction(results, visibleResults, action, _allServices);
            if (action is not (SearchResultAction.CopyName
                or SearchResultAction.CopyLocation))
            {
                LastDirectActionResult = result;
            }
            if (!string.IsNullOrWhiteSpace(announcement)) SearchStatus.Announce(announcement);
            Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
            return;
        }

        var selectedResults = action == SearchResultAction.Playlist
            ? GetSelectedResults()
            : [result];
        if (action == SearchResultAction.Playlist
            && selectedResults.Select(selected => selected.SessionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any())
        {
            SearchStatus.Announce("Do jednej playlisty wybierz wyniki z tej samej usługi");
            Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
            return;
        }
        SelectedResults = selectedResults;
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
        if (modifiers == ModifierKeys.Shift && key is Key.Up or Key.Down)
        {
            ExtendResultSelection(key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None && key is Key.Up or Key.Down)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _selectionAnchorIndex = ResultsList.SelectedIndex;
                _selectionLeadIndex = ResultsList.SelectedIndex;
            }, DispatcherPriority.ContextIdle);
        }
        if (modifiers == ModifierKeys.Control && key is Key.X or Key.V)
        {
            SearchStatus.Announce(key == Key.X
                ? "Wycinanie plików nie działa na liście wyników wyszukiwania"
                : "Wklejanie plików nie działa na liście wyników wyszukiwania");
            Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
            e.Handled = true;
            return;
        }
        if (modifiers == ModifierKeys.None
            && key == Key.Left
            && ResultsList.SelectedItem is SearchResultRow selectedRow)
        {
            SearchStatus.Announce(_formatQuickInformation(selectedRow.Item));
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
        else if (key == Key.L && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.Library;
        else if (key == Key.P && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.Playlist;
        else if (key == Key.P && modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
            action = SearchResultAction.Preset;
        else if (key == Key.Enter && modifiers == ModifierKeys.Alt)
            action = SearchResultAction.Information;
        else if (key == Key.C && modifiers == ModifierKeys.Control)
            action = SearchResultAction.CopyName;
        else if (key == Key.C && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            action = SearchResultAction.CopyLocation;
        else if (key == Key.D && modifiers == ModifierKeys.Control)
            action = SearchResultAction.Download;
        else if (key == Key.S && modifiers == ModifierKeys.Control)
            action = SearchResultAction.SaveAs;

        if (action is null) return;
        CompleteSelected(action.Value);
        e.Handled = true;
    }

    private SearchResult[] GetSelectedResults()
    {
        var selected = ResultsList.SelectedItems
            .OfType<SearchResultRow>()
            .OrderBy(row => ResultsList.Items.IndexOf(row))
            .Select(row => new SearchResult(row.SessionId, row.Item))
            .DistinctBy(result => (result.SessionId.ToUpperInvariant(), result.Item.Id))
            .ToArray();
        if (selected.Length > 0) return selected;
        return ResultsList.SelectedItem is SearchResultRow row
            ? [new SearchResult(row.SessionId, row.Item)]
            : [];
    }

    private void ExtendResultSelection(int direction)
    {
        if (ResultsList.Items.Count == 0) return;
        if (_selectionAnchorIndex < 0 || _selectionAnchorIndex >= ResultsList.Items.Count)
        {
            _selectionAnchorIndex = Math.Max(0, ResultsList.SelectedIndex);
        }
        if (_selectionLeadIndex < 0 || _selectionLeadIndex >= ResultsList.Items.Count)
        {
            _selectionLeadIndex = Math.Max(0, ResultsList.SelectedIndex);
        }

        _selectionLeadIndex = Math.Clamp(
            _selectionLeadIndex + direction,
            0,
            ResultsList.Items.Count - 1);
        var first = Math.Min(_selectionAnchorIndex, _selectionLeadIndex);
        var last = Math.Max(_selectionAnchorIndex, _selectionLeadIndex);
        ResultsList.SelectedItems.Clear();
        for (var index = first; index <= last; index++)
        {
            ResultsList.SelectedItems.Add(ResultsList.Items[index]);
        }

        ResultsList.ScrollIntoView(ResultsList.Items[_selectionLeadIndex]);
        Dispatcher.BeginInvoke(FocusSelectedResult, DispatcherPriority.ContextIdle);
    }

    private void Search_Click(object sender, RoutedEventArgs e) => RunSearch();
    private void Open_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Open);
    private void TogglePlayback_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.TogglePlayback);
    private void PlayNext_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.PlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Queue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Favorite);
    private void Library_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Library);
    private void Playlist_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Playlist);
    private void Preset_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Preset);
    private void Information_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Information);
    private void GoToPodcast_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.GoToPodcast);
    private void CopyName_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.CopyName);
    private void CopyLocation_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.CopyLocation);
    private void DownloadPodcast_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.Download);
    private void SavePodcastAs_Click(object sender, RoutedEventArgs e) => CompleteSelected(SearchResultAction.SaveAs);
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => CompleteSelected(SearchResultAction.Open);

    private void ResultsContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var containsRadio = GetSelectedResults().Any(result =>
            string.Equals(result.SessionId, "radio", StringComparison.OrdinalIgnoreCase));
        var visibility = containsRadio ? Visibility.Collapsed : Visibility.Visible;
        SearchPlayNextMenuItem.Visibility = visibility;
        SearchQueueMenuItem.Visibility = visibility;
        SearchQueueSeparator.Visibility = visibility;
        SearchPlaylistMenuItem.Visibility = Visibility.Visible;
        var selected = GetSelectedResults();
        var podcastsOnly = selected.Length > 0 && selected.All(result =>
            string.Equals(result.SessionId, "podcasts", StringComparison.OrdinalIgnoreCase));
        var podcastEpisodesOnly = podcastsOnly
            && selected.All(result => result.Item.Kind == MediaItemKind.Episode);
        var youtubeOnly = selected.Length > 0
            && selected.All(result => YouTubeSearchClient.IsSearchResult(result.Item)
                || string.Equals(
                    result.Item.ExternalId,
                    "internet-media:public",
                    StringComparison.Ordinal));
        SearchDownloadPodcastMenuItem.Visibility = podcastEpisodesOnly
            ? Visibility.Visible
            : Visibility.Collapsed;
        SearchSavePodcastAsMenuItem.Visibility = podcastEpisodesOnly && selected.Length == 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        MenuAccessibility.SetPresentation(
            SearchLibraryMenuItem,
            youtubeOnly
                ? "Dodaj do Mediów internetowych"
                : "Dodaj lub usuń z Biblioteki");
        SearchLibraryMenuItem.InputGestureText = "Ctrl+Shift+L";
        AutomationProperties.SetAcceleratorKey(SearchLibraryMenuItem, "Ctrl+Shift+L");
        MenuAccessibility.SetPresentation(
            SearchCopyNameMenuItem,
            youtubeOnly
                ? "Kopiuj nazwy i strony YouTube"
                : podcastsOnly ? "Kopiuj opisy i strony odcinków" : "Kopiuj nazwy");
        SearchCopyNameMenuItem.InputGestureText = "Ctrl+C";
        AutomationProperties.SetAcceleratorKey(SearchCopyNameMenuItem, "Ctrl+C");
        MenuAccessibility.SetPresentation(
            SearchCopyLocationMenuItem,
            youtubeOnly
                ? "Kopiuj adresy YouTube"
                : podcastsOnly ? "Kopiuj bezpośrednie adresy audio" : "Kopiuj ścieżki lub łącza");
        SearchCopyLocationMenuItem.InputGestureText = "Ctrl+Shift+C";
        AutomationProperties.SetAcceleratorKey(SearchCopyLocationMenuItem, "Ctrl+Shift+C");
        SearchGoToPodcastMenuItem.Visibility = selected.Length == 1
            && string.Equals(selected[0].SessionId, "podcasts", StringComparison.OrdinalIgnoreCase)
            && selected[0].Item.Kind == MediaItemKind.Episode
            && !youtubeOnly
            && !string.IsNullOrWhiteSpace(selected[0].Item.ExternalId)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
    Library,
    Playlist,
    Preset,
    GoToPodcast,
    Information,
    CopyName,
    CopyLocation,
    Download,
    SaveAs
}
