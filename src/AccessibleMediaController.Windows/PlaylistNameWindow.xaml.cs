using System.Windows;

namespace AccessibleMediaController.Windows;

public partial class PlaylistNameWindow : Window
{
    public PlaylistNameWindow(string title, string currentName)
    {
        InitializeComponent();
        Title = title;
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string PlaylistName { get; private set; } = string.Empty;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Wpisz nazwę playlisty.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }
        PlaylistName = name;
        DialogResult = true;
    }
}
