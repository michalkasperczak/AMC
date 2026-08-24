using System.Windows;
using System.Windows.Automation;

namespace AccessibleMediaController.Windows;

public partial class RenameLocalItemWindow : Window
{
    public RenameLocalItemWindow(string currentName, bool renameOnDisk)
    {
        InitializeComponent();
        Title = renameOnDisk ? "Zmień nazwę pliku na dysku" : "Zmień nazwę w Bibliotece";
        HeadingText.Text = Title;
        HelpText.Text = renameOnDisk
            ? "Zmiana dotyczy rzeczywistego pliku. Rozszerzenie zostanie zachowane, a AMC zaktualizuje ścieżkę i zachowa powiązane dane."
            : "Zmiana dotyczy wyłącznie nazwy wyświetlanej przez AMC. Nazwa i ścieżka pliku na dysku pozostaną bez zmian.";
        NameLabel.Content = renameOnDisk ? "_Nowa nazwa pliku bez rozszerzenia:" : "_Nowa nazwa w Bibliotece:";
        AutomationProperties.SetHelpText(
            NameBox,
            $"{HelpText.Text} Enter zatwierdza, Escape anuluje.");
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string NewName => NameBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(
                "Nazwa nie może być pusta.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
