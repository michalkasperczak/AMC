using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class InformationWindow : AccessibleWindow
{
    private readonly string _information;
    private readonly System.Windows.Forms.RichTextBox _informationBox;

    public InformationWindow(string information)
    {
        InitializeComponent();
        _information = information;
        _informationBox = new System.Windows.Forms.RichTextBox
        {
            AccessibleName = "Właściwości i informacje",
            AccessibleDescription = "Tekst tylko do odczytu. Można poruszać się po znakach, słowach i wierszach oraz zaznaczać fragmenty.",
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
            DetectUrls = false,
            Dock = System.Windows.Forms.DockStyle.Fill,
            HideSelection = false,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical,
            ShortcutsEnabled = true,
            TabStop = true,
            Text = information,
            WordWrap = true
        };
        _informationBox.KeyDown += InformationBox_KeyDown;
        InformationHost.Child = _informationBox;
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        _informationBox.Select(0, 0);
        _informationBox.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void InformationBox_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.Modifiers != System.Windows.Forms.Keys.None
            || e.KeyCode != System.Windows.Forms.Keys.Escape)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        Dispatcher.BeginInvoke(Close);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (ClipboardRetry.TrySetText(_information, out var errorMessage))
        {
            CopyStatusText.Announce("Skopiowano wszystkie informacje");
            return;
        }
        CopyStatusText.Announce(errorMessage);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
