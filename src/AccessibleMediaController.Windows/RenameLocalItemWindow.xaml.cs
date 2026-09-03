using System.Windows;
using System.Windows.Automation;

namespace AccessibleMediaController.Windows;

public partial class RenameLocalItemWindow : Window
{
    public RenameLocalItemWindow(string currentName, bool renameOnDisk)
        : this(
            currentName,
            renameOnDisk ? "Zmień nazwę pliku na dysku" : "Zmień nazwę w Bibliotece",
            renameOnDisk
                ? "Zmiana dotyczy rzeczywistego pliku. Rozszerzenie zostanie zachowane, a AMC zaktualizuje ścieżkę i zachowa powiązane dane."
                : "Zmiana dotyczy wyłącznie nazwy wyświetlanej przez AMC. Nazwa i ścieżka pliku na dysku pozostaną bez zmian.",
            renameOnDisk ? "_Nowa nazwa pliku bez rozszerzenia:" : "_Nowa nazwa w Bibliotece:")
    {
    }

    public RenameLocalItemWindow(
        string currentName,
        string title,
        string helpText,
        string fieldLabel)
    {
        InitializeComponent();
        Title = title;
        HeadingText.Text = Title;
        HelpText.Text = helpText;
        NameLabel.Content = fieldLabel;
        AutomationProperties.SetName(NameBox, fieldLabel.Replace("_", string.Empty).TrimEnd(':'));
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
