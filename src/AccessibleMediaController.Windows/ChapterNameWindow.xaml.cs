using System.Windows;
using AccessibleMediaController.Core.Commands;

namespace AccessibleMediaController.Windows;

public partial class ChapterNameWindow : Controls.AccessibleWindow
{
    public ChapterNameWindow(string itemTitle, TimeSpan position)
    {
        InitializeComponent();
        ContextText.Text = $"{itemTitle}, początek {CommandRouter.FormatTime(position)}";
        ContextText.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, ContextText.Text);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string ChapterName => NameBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(
                this,
                "Nazwa rozdziału nie może być pusta.",
                "Nowy rozdział",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }
        DialogResult = true;
    }
}
