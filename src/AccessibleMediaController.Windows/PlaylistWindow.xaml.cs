using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class PlaylistWindow : Window
{
    public PlaylistWindow(IReadOnlyList<MediaItem> items)
    {
        InitializeComponent();
        var target = items.Count == 1 ? items[0].Title : $"{items.Count} elementów";
        DescriptionText.Text = $"Zmień playlisty dla: {target}. Spacja zmienia stan, Enter zapisuje.";
        PlaylistList.ItemsSource = new List<PlaylistChoice>
        {
            new("Do odsłuchu", true),
            new("Ulubione albumy", false),
            new("Nowości", false)
        };
        PlaylistList.SelectedIndex = 0;
        Loaded += (_, _) => PlaylistList.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && PlaylistList.SelectedItem is PlaylistChoice choice)
        {
            choice.ContainsItem = !choice.ContainsItem;
            PlaylistList.Items.Refresh();
            e.Handled = true;
        }
    }

    private sealed class PlaylistChoice(string name, bool containsItem)
    {
        public string Name { get; } = name;
        public bool ContainsItem { get; set; } = containsItem;
    }
}
