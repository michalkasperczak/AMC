using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows;

public partial class PodcastSourceWindow : Window
{
    private readonly Func<Uri, CancellationToken, Task<PodcastFeedDocument>> _fetch;
    private CancellationTokenSource? _checkCancellation;
    private string? _checkedAddress;

    public PodcastSourceWindow(Func<Uri, CancellationToken, Task<PodcastFeedDocument>> fetch)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        _fetch = fetch;
        InitializeComponent();
        Title = "Nowy podcast";
        FeedBox.TextChanged += (_, _) => InvalidateCheckedFeed();
        Loaded += (_, _) =>
        {
            FeedBox.Focus();
            Keyboard.Focus(FeedBox);
        };
    }

    public PodcastFeedDocument? Feed { get; private set; }
    public string CustomTitle => NameBox.Text.Trim();

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetFeedUri(out var address)) return;
        _checkCancellation?.Cancel();
        _checkCancellation?.Dispose();
        _checkCancellation = new CancellationTokenSource();
        SetBusy(true);
        StatusText.Text = "Sprawdzanie kanału…";
        try
        {
            var feed = await _fetch(address, _checkCancellation.Token);
            Feed = feed;
            _checkedAddress = FeedBox.Text.Trim();
            AddButton.IsEnabled = true;
            var author = string.IsNullOrWhiteSpace(feed.Author) ? string.Empty : $", autor: {feed.Author}";
            StatusText.Text = $"Kanał działa. {feed.Title}{author}. Odcinki: {feed.Episodes.Count}.";
            AddButton.Focus();
            Keyboard.Focus(AddButton);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Sprawdzanie kanału anulowano.";
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or System.Xml.XmlException
            or ArgumentException)
        {
            StatusText.Text = $"Nie można odczytać kanału: {exception.Message}";
            FeedBox.Focus();
            Keyboard.Focus(FeedBox);
            FeedBox.SelectAll();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (Feed is null
            || !string.Equals(_checkedAddress, FeedBox.Text.Trim(), StringComparison.Ordinal))
        {
            StatusText.Text = "Najpierw sprawdź aktualny adres kanału.";
            return;
        }
        DialogResult = true;
    }

    private bool TryGetFeedUri(out Uri address)
    {
        if (Uri.TryCreate(FeedBox.Text.Trim(), UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https"
            && string.IsNullOrEmpty(parsed.UserInfo))
        {
            address = parsed;
            return true;
        }
        address = null!;
        StatusText.Text = "Wpisz pełny adres HTTP lub HTTPS bez nazwy użytkownika i hasła.";
        FeedBox.Focus();
        Keyboard.Focus(FeedBox);
        FeedBox.SelectAll();
        return false;
    }

    private void InvalidateCheckedFeed()
    {
        if (string.Equals(_checkedAddress, FeedBox.Text.Trim(), StringComparison.Ordinal)) return;
        Feed = null;
        _checkedAddress = null;
        AddButton.IsEnabled = false;
    }

    private void SetBusy(bool busy)
    {
        CheckButton.IsEnabled = !busy;
        FeedBox.IsEnabled = !busy;
        NameBox.IsEnabled = !busy;
        if (busy) AddButton.IsEnabled = false;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _checkCancellation?.Cancel();
        _checkCancellation?.Dispose();
        _checkCancellation = null;
    }
}
