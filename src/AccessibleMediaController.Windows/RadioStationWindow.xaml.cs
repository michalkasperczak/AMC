using System.Windows;

namespace AccessibleMediaController.Windows;

public partial class RadioStationWindow : Window
{
    public RadioStationWindow(string? currentName = null, string? currentStreamUrl = null)
    {
        InitializeComponent();
        var editing = !string.IsNullOrWhiteSpace(currentName) || !string.IsNullOrWhiteSpace(currentStreamUrl);
        Title = editing ? "Edytuj stację radiową" : "Dodaj stację radiową";
        HeadingText.Text = Title;
        HelpText.Text = "Nazwa jest etykietą używaną przez AMC. Adres strumienia jest niezależnym źródłem odtwarzania i również można go później zaktualizować klawiszem F2.";
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
            MessageBox.Show("Nazwa stacji nie może być pusta.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }
        if (!Uri.TryCreate(StreamUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            MessageBox.Show(
                "Wpisz pełny adres strumienia rozpoczynający się od http:// lub https://.",
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
