using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class ShortcutCaptureWindow : Window
{
    public ShortcutCaptureWindow(string commandId)
    {
        InitializeComponent();
        CommandText.Text = $"Polecenie: {commandId}";
        Loaded += (_, _) => CapturedText.Focus();
    }

    public KeyChord? CapturedChord { get; private set; }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            DialogResult = false;
            return;
        }

        var effectiveKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (effectiveKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        CapturedChord = WindowsKeyMap.FromKeyEvent(e);
        CapturedText.Text = CapturedChord.Value.Canonical;
        e.Handled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CapturedChord is null)
        {
            MessageBox.Show("Najpierw naciśnij nowy skrót.", "Zmiana skrótu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
