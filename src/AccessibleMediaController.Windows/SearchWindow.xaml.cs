using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class SearchWindow : Window
{
    private readonly IReadOnlyList<DemoMediaSession> _sourceSessions;
    private readonly bool _allServices;
    private readonly Func<MediaItem, string> _formatItem;

    public SearchWindow(
        SessionManager sessions,
        bool allServices,
        Func<MediaItem, string> formatItem)
    {
        InitializeComponent();
        Title = allServices
            ? "Szukaj we wszystkich usługach"
            : $"Szukaj w usłudze {sessions.Current.DisplayName}";
        HeadingText.Text = Title;
        ScopeText.Text = allServices
            ? "Wyniki mogą pochodzić ze wszystkich włączonych usług."
            : $"Zakres: {sessions.Current.DisplayName}.";

        _sourceSessions = allServices ? sessions.Sessions : [sessions.Current];
        _allServices = allServices;
        _formatItem = formatItem;

        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        };
    }

    public SearchResult? SelectedResult { get; private set; }

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
                _allServices
                    ? $"{_formatItem(result.Item)}, {result.Session.DisplayName}"
                    : _formatItem(result.Item)))
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
        SearchStatus.Announce(results.Count == 1 ? "1 wynik" : $"{results.Count} wyników");
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

    private void OpenSelected()
    {
        if (ResultsList.SelectedItem is not SearchResultRow row)
        {
            SearchStatus.Announce("Najpierw wykonaj wyszukiwanie i wybierz wynik");
            SearchBox.Focus();
            return;
        }

        SelectedResult = new SearchResult(row.SessionId, row.Item);
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
        if (e.Key != Key.Enter) return;
        OpenSelected();
        e.Handled = true;
    }

    private void Search_Click(object sender, RoutedEventArgs e) => RunSearch();
    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    public sealed record SearchResult(string SessionId, MediaItem Item);

    private sealed record SearchResultRow(
        string SessionId,
        MediaItem Item,
        string Label)
    {
        public override string ToString() => Label;
    }
}
