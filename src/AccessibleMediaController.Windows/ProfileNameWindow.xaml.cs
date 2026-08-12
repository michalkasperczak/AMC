using System.Windows;

namespace AccessibleMediaController.Windows;

public partial class ProfileNameWindow : Window
{
    public ProfileNameWindow(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string ProfileName => NameBox.Text;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Nazwa profilu nie może być pusta.", "Nazwa profilu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
