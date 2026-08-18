using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;

namespace AccessibleMediaController.Windows;

public partial class InformationWindow : AccessibleWindow
{
    private readonly string _information;

    public InformationWindow(string information)
    {
        InitializeComponent();
        _information = information;
        InformationTextBox.Text = information;
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        InformationTextBox.Focus();
        Keyboard.Focus(InformationTextBox);
        InformationTextBox.CaretIndex = 0;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Escape or Key.Enter)
        {
            e.Handled = true;
            Close();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_information);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
