using System.Windows;

namespace AccessibleMediaController.Windows;

public partial class RadioStationWindow : Window
{
    private readonly bool _wiiMNetworkStream;

    public RadioStationWindow(
        string? currentName = null,
        string? currentStreamUrl = null,
        bool wiiMNetworkStream = false)
    {
        InitializeComponent();
        _wiiMNetworkStream = wiiMNetworkStream;
        var editing = !string.IsNullOrWhiteSpace(currentName) || !string.IsNullOrWhiteSpace(currentStreamUrl);
        Title = wiiMNetworkStream
            ? editing ? "Edytuj strumień WiiM" : "Dodaj strumień WiiM"
            : editing ? "Edytuj stację radiową" : "Nowa stacja radiowa";
        HeadingText.Text = Title;
        HelpText.Text = wiiMNetworkStream
            ? "Nazwa i adres zostaną zapisane na lokalnej liście AMC. Enter lub Ctrl+Alt+W wyśle wybrany strumień do aktywnego urządzenia WiiM."
            : "Nazwa jest etykietą używaną przez AMC. Podaj bezpośredni strumień, playlistę albo publiczny adres trwającej transmisji YouTube. Zwykłe filmy YouTube będą obsługiwane później w module Media internetowe.";
        NameLabel.Content = wiiMNetworkStream ? "_Nazwa strumienia:" : "_Nazwa stacji:";
        NameBox.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,
            wiiMNetworkStream ? "Nazwa strumienia" : "Nazwa stacji");
        StreamLabel.Content = wiiMNetworkStream
            ? "Adres _strumienia:"
            : "Adres _strumienia lub transmisji YouTube:";
        StreamBox.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,
            wiiMNetworkStream ? "Adres strumienia" : "Adres strumienia lub transmisji YouTube");
        StreamBox.SetValue(System.Windows.Automation.AutomationProperties.HelpTextProperty,
            wiiMNetworkStream
                ? "Wpisz bezpośredni adres strumienia HTTP lub HTTPS."
                : "Wpisz bezpośredni adres strumienia, playlisty M3U, M3U8 lub PLS albo publiczny adres trwającej transmisji YouTube.");
        NameBox.Text = currentName ?? string.Empty;
        StreamBox.Text = currentStreamUrl ?? string.Empty;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string StationName => NameBox.Text.Trim();
    public string StreamUrl => StreamBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(StationName))
        {
            MessageBox.Show(
                _wiiMNetworkStream ? "Nazwa strumienia nie może być pusta." : "Nazwa stacji nie może być pusta.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }
        if (!Uri.TryCreate(StreamUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(
                _wiiMNetworkStream
                    ? "Wpisz pełny adres strumienia rozpoczynający się od http:// lub https://."
                    : "Wpisz pełny adres strumienia lub transmisji YouTube rozpoczynający się od http:// lub https://.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            StreamBox.Focus();
            StreamBox.SelectAll();
            return;
        }
        DialogResult = true;
    }
}
