using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;

namespace AccessibleMediaController.Windows;

public partial class InformationWindow : AccessibleWindow
{
    private readonly string _information;
    private readonly string[] _lines;

    public InformationWindow(string information)
    {
        InitializeComponent();
        _information = information;
        _lines = information
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        InformationList.ItemsSource = _lines;
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (_lines.Length > 0) InformationList.SelectedIndex = 0;
        InformationList.Focus();
        Keyboard.Focus(InformationList);
        if (InformationList.SelectedItem is not null)
        {
            InformationList.ScrollIntoView(InformationList.SelectedItem);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A)
        {
            InformationList.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            var selectedLines = InformationList.SelectedItems.Cast<string>().ToArray();
            if (selectedLines.Length > 0)
            {
                Clipboard.SetText(string.Join(Environment.NewLine, selectedLines));
            }
            e.Handled = true;
            return;
        }

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
