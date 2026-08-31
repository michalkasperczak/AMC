using System.Windows.Interop;
using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class ShortcutCaptureWindow : Window
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VirtualKeyReturn = 0x0D;
    private const long ExtendedKeyMask = 1L << 24;

    private HwndSource? _source;
    private bool _captureComplete;
    private bool _suppressNumpadEnterKeyUp;

    public ShortcutCaptureWindow(string commandDisplayName, KeyChord? currentChord = null)
    {
        InitializeComponent();
        CommandText.Text = $"Funkcja: {commandDisplayName}";
        if (currentChord is KeyChord current)
        {
            CapturedText.Text = WindowsKeyMap.ToDisplayText(current);
            CaptureStatus.Text = $"Obecny skrót: {WindowsKeyMap.ToDisplayText(current)}. Oczekiwanie na nową kombinację";
        }
        Loaded += (_, _) => CapturedText.Focus();
    }

    public KeyChord? CapturedChord { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WindowProcedure);
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(WindowProcedure);
        base.OnClosed(e);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            DialogResult = false;
            return;
        }

        var effectiveKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_captureComplete && effectiveKey == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            return;
        }
        if (effectiveKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        Capture(WindowsKeyMap.FromKeyEvent(e));
        e.Handled = true;
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var virtualKey = wParam.ToInt32();
        var extended = (lParam.ToInt64() & ExtendedKeyMask) != 0;
        if (virtualKey != VirtualKeyReturn || !extended) return IntPtr.Zero;

        if (message is WmKeyDown or WmSysKeyDown)
        {
            Capture(new KeyChord(
                WindowsKeyMap.NumpadEnterKey,
                WindowsKeyMap.FromModifierKeys(Keyboard.Modifiers)));
            _suppressNumpadEnterKeyUp = true;
            handled = true;
        }
        else if (_suppressNumpadEnterKeyUp && message is WmKeyUp or WmSysKeyUp)
        {
            _suppressNumpadEnterKeyUp = false;
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void Capture(KeyChord chord)
    {
        CapturedChord = chord;
        _captureComplete = true;
        var display = WindowsKeyMap.ToDisplayText(chord);
        CapturedText.Text = display;
        CaptureStatus.Announce($"Nowy skrót: {display}. Naciśnij zwykły Enter, aby zapisać, albo inną kombinację, aby ją zastąpić");
        Dispatcher.BeginInvoke(() =>
        {
            OkButton.Focus();
            Keyboard.Focus(OkButton);
        });
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
