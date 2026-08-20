using System.Windows;
using AccessibleMediaController.Core.Commands;

namespace AccessibleMediaController.Windows;

public partial class BookmarkNameWindow : Window
{
    public BookmarkNameWindow(string itemTitle, TimeSpan position)
    {
        InitializeComponent();
        ContextText.Text = $"{itemTitle}, {CommandRouter.FormatTime(position)}";
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string BookmarkName => NameBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(
                "Nazwa zakładki nie może być pusta.",
                "Nazwa zakładki",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
