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
    private const long ExtendedKeyMask = 1L << 24;

    private HwndSource? _source;
    private bool _captureComplete;
    private readonly HashSet<uint> _suppressedNumpadKeyUps = [];

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
        _suppressedNumpadKeyUps.Clear();
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
        try
        {
            if (!TryReadExactNumpadMessage(
                    message,
                    wParam,
                    lParam,
                    out var virtualKey,
                    out var keyName,
                    out var keyDown))
            {
                return IntPtr.Zero;
            }

            if (keyDown)
            {
                if (_suppressedNumpadKeyUps.Add(virtualKey))
                {
                    Capture(new KeyChord(
                        keyName,
                        WindowsKeyMap.FromModifierKeys(Keyboard.Modifiers)));
                }
                handled = true;
            }
            else if (_suppressedNumpadKeyUps.Remove(virtualKey))
            {
                handled = true;
            }
        }
        catch (Exception exception)
        {
            // An HwndSource hook runs inside the native Windows message loop. No
            // capture defect may escape from here, because a failed UI thread can
            // also stall screen-reader keyboard hooks until Windows removes them.
            handled = false;
            _suppressedNumpadKeyUps.Clear();
            DiagnosticLog.Error(
                "shortcut-capture",
                "Nie udało się bezpiecznie przechwycić klawisza. Klawisz przekazano dalej do Windows.",
                exception);
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        CaptureStatus.Announce(
                            "Nie udało się odczytać tego klawisza. Poprzedni skrót nie został zmieniony");
                    }
                    catch (Exception announceException)
                    {
                        DiagnosticLog.Error(
                            "shortcut-capture",
                            "Nie udało się ogłosić błędu przechwytywania.",
                            announceException);
                    }
                });
            }
            catch (Exception dispatcherException)
            {
                DiagnosticLog.Error(
                    "shortcut-capture",
                    "Dyspozytor okna przechwytywania nie przyjął komunikatu o błędzie.",
                    dispatcherException);
            }
        }
        return IntPtr.Zero;
    }

    internal static bool TryReadExactNumpadMessage(
        int message,
        IntPtr wParam,
        IntPtr lParam,
        out uint virtualKey,
        out string keyName,
        out bool keyDown)
    {
        virtualKey = 0;
        keyName = string.Empty;
        keyDown = false;

        // HwndSource sends every window message through this hook. Pointer-sized
        // parameters of focus, UI Automation and accessibility messages are not
        // key codes and can exceed Int32 on 64-bit Windows.
        if (message is not (WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp))
            return false;

        virtualKey = unchecked((uint)wParam.ToInt64());
        var extended = (lParam.ToInt64() & ExtendedKeyMask) != 0;
        if (!WindowsKeyMap.TryGetExactNumpadKey(virtualKey, extended, out keyName)
            || !WindowsKeyMap.RequiresExactNumpadHook(keyName))
            return false;

        keyDown = message is WmKeyDown or WmSysKeyDown;
        return true;
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
