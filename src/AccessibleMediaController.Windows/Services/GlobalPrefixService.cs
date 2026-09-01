using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Windows.Services;

internal sealed class GlobalPrefixService : IDisposable
{
    private const int HotKeyIdA = 0x41C0;
    private const int HotKeyIdB = 0x41C1;
    private const int WmHotKey = 0x0312;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyReturn = 0x0D;
    private const uint LlkhfExtended = 0x00000001;

    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;
    private readonly Func<KeyChord, bool> _commandHandler;
    private readonly Func<KeyChord, bool>? _focusedShortcutHandler;
    private readonly Action _prefixActivated;
    private readonly DispatcherTimer _timer;
    private readonly LowLevelKeyboardProc _hookProcedure;
    private readonly HashSet<uint> _suppressedKeys = [];
    private IntPtr _hookHandle;
    private int _activeHotKeyId = HotKeyIdA;
    private bool _hotKeyRegistered;
    private KeyChord? _registeredPrefix;
    private KeyChord? _hookPrefix;
    private bool _layerActive;
    private int _standardTimeout;
    private int _continuationTimeout;

    public GlobalPrefixService(
        IntPtr windowHandle,
        Func<KeyChord, bool> commandHandler,
        Action prefixActivated,
        Func<KeyChord, bool>? focusedShortcutHandler = null)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("Brak źródła okna WPF.");
        _commandHandler = commandHandler;
        _prefixActivated = prefixActivated;
        _focusedShortcutHandler = focusedShortcutHandler;
        _hookProcedure = KeyboardHookCallback;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => DeactivateLayer();
        _source.AddHook(WindowProcedure);
        InstallKeyboardHook();
    }

    public void ConfigureTimeouts(int standardTimeoutMilliseconds, int continuationTimeoutMilliseconds)
    {
        _standardTimeout = standardTimeoutMilliseconds;
        _continuationTimeout = continuationTimeoutMilliseconds;
    }

    public void RegisterPrefix(KeyChord prefix)
    {
        prefix = new KeyChord(KeyChord.NormalizeKey(prefix.Key), prefix.Modifiers);
        if (_registeredPrefix is KeyChord current
            && string.Equals(current.Canonical, prefix.Canonical, StringComparison.OrdinalIgnoreCase)
            && (_hotKeyRegistered || _hookPrefix is not null))
        {
            return;
        }

        if (RequiresLowLevelHook(prefix))
        {
            if (_hotKeyRegistered) UnregisterHotKey(_windowHandle, _activeHotKeyId);
            _hotKeyRegistered = false;
            _hookPrefix = prefix;
            _registeredPrefix = prefix;
            DiagnosticLog.Info(
                "global-prefix",
                $"Zarejestrowano globalny prefiks {WindowsKeyMap.ToDisplayText(prefix)} przez bezpieczny hak klawiatury.");
            return;
        }

        if (!WindowsKeyMap.TryGetVirtualKey(prefix.Key, out var virtualKey))
        {
            throw new InvalidOperationException(
                $"Klawisz {WindowsKeyMap.ToDisplayText(prefix)} nie może być globalnym prefiksem AMC. {RegistrationFailureNextStep()}");
        }

        var modifiers = ModNoRepeat;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 0x0001;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Ctrl)) modifiers |= 0x0002;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 0x0004;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Windows)) modifiers |= 0x0008;

        var candidateHotKeyId = _activeHotKeyId == HotKeyIdA ? HotKeyIdB : HotKeyIdA;
        if (!RegisterHotKey(_windowHandle, candidateHotKeyId, modifiers, virtualKey))
        {
            var error = Marshal.GetLastWin32Error();
            var reason = error == 1409
                ? "Ta kombinacja jest już używana przez Windows, NVDA albo inny program."
                : "Windows odmówił zarejestrowania tej kombinacji.";
            throw new InvalidOperationException(
                $"Nie można ustawić globalnego prefiksu {WindowsKeyMap.ToDisplayText(prefix)}. {reason} {RegistrationFailureNextStep()}");
        }

        if (_hotKeyRegistered) UnregisterHotKey(_windowHandle, _activeHotKeyId);
        _activeHotKeyId = candidateHotKeyId;
        _hotKeyRegistered = true;
        _hookPrefix = null;
        _registeredPrefix = prefix;
        DiagnosticLog.Info(
            "global-prefix",
            $"Zarejestrowano globalny prefiks {WindowsKeyMap.ToDisplayText(prefix)} przez mechanizm skrótów Windows.");
    }

    private string RegistrationFailureNextStep() =>
        _hotKeyRegistered || _hookPrefix is not null
            ? "Poprzedni prefiks pozostaje aktywny."
            : "Wybierz inną kombinację.";

    public void Suspend()
    {
        DeactivateLayer();
        _suppressedKeys.Clear();
        if (_hotKeyRegistered) UnregisterHotKey(_windowHandle, _activeHotKeyId);
        _hotKeyRegistered = false;
        _hookPrefix = null;
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_hotKeyRegistered) UnregisterHotKey(_windowHandle, _activeHotKeyId);
        _hotKeyRegistered = false;
        _hookPrefix = null;
        if (_hookHandle != IntPtr.Zero) UnhookWindowsHookEx(_hookHandle);
        _source.RemoveHook(WindowProcedure);
    }

    private void ActivateLayer()
    {
        _layerActive = true;
        _timer.Stop();
        _timer.Interval = TimeSpan.FromMilliseconds(_standardTimeout <= 0 ? 3000 : _standardTimeout);
        _timer.Start();
        _prefixActivated();
    }

    private void DeactivateLayer()
    {
        _layerActive = false;
        _timer.Stop();
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && _hotKeyRegistered && wParam.ToInt32() == _activeHotKeyId)
        {
            ActivateLayer();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void InstallKeyboardHook()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProcedure, GetModuleHandle(module?.ModuleName), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Nie można uruchomić obsługi warstwy prefiksowej.");
        }
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return HandleKeyboardHook(code, wParam, lParam);
        }
        catch (Exception exception)
        {
            // A low-level keyboard hook must never propagate an exception. If it
            // does, other consumers of the keyboard chain, including NVDA, can
            // temporarily stop receiving input.
            try
            {
                _suppressedKeys.Clear();
                DeactivateLayer();
                DiagnosticLog.Error(
                    "global-prefix",
                    "Błąd obsługi globalnego prefiksu. AMC zwolnił klawiaturę i przekazał klawisz dalej.",
                    exception);
            }
            catch
            {
                // Nothing may escape from a native low-level hook callback.
            }
            return CallNextHookEx(_hookHandle, code, wParam, lParam);
        }
    }

    private IntPtr HandleKeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return CallNextHookEx(_hookHandle, code, wParam, lParam);

        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var keyDown = message is WmKeyDown or WmSysKeyDown;
        var keyUp = message is WmKeyUp or WmSysKeyUp;

        if (keyUp && _suppressedKeys.Remove(data.VirtualKeyCode)) return new IntPtr(1);
        if (!_layerActive)
        {
            if (keyDown && MatchesHookPrefix(data))
            {
                _suppressedKeys.Add(data.VirtualKeyCode);
                ActivateLayer();
                return new IntPtr(1);
            }
            if (keyDown
                && GetForegroundWindow() == _windowHandle
                && TryHandleFocusedShortcut(data.VirtualKeyCode))
            {
                _suppressedKeys.Add(data.VirtualKeyCode);
                return new IntPtr(1);
            }
            return CallNextHookEx(_hookHandle, code, wParam, lParam);
        }
        if (!keyDown) return CallNextHookEx(_hookHandle, code, wParam, lParam);

        _suppressedKeys.Add(data.VirtualKeyCode);
        if (IsModifier(data.VirtualKeyCode)) return new IntPtr(1);

        var keyName = WindowsKeyMap.FromKeyboardInput(
            data.VirtualKeyCode,
            (data.Flags & LlkhfExtended) != 0);
        if (keyName is null)
        {
            DeactivateLayer();
            return new IntPtr(1);
        }
        if (keyName == "Escape")
        {
            DeactivateLayer();
            return new IntPtr(1);
        }

        var chord = new KeyChord(keyName, ReadModifiers());
        var keepActive = _commandHandler(chord);
        if (keepActive)
        {
            _timer.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(_continuationTimeout <= 0 ? 2000 : _continuationTimeout);
            _timer.Start();
        }
        else
        {
            DeactivateLayer();
        }
        return new IntPtr(1);
    }

    private bool TryHandleFocusedShortcut(uint virtualKey)
    {
        if (_focusedShortcutHandler is null) return false;
        var keyName = WindowsKeyMap.FromVirtualKey(virtualKey);
        if (keyName is null) return false;
        var chord = new KeyChord(keyName, ReadModifiers());
        return IsFocusedDirectShortcutCandidate(chord) && _focusedShortcutHandler(chord);
    }

    internal static bool IsFocusedDirectShortcutCandidate(KeyChord chord) =>
        chord.Modifiers == (KeyModifiers.Ctrl | KeyModifiers.Shift)
        && chord.Key is "0" or "S";

    internal static bool RequiresLowLevelHook(KeyChord prefix)
    {
        var key = KeyChord.NormalizeKey(prefix.Key);
        // NVDA uses a low-level hook for desktop-layout numpad navigation.
        // RegisterHotKey can report success for an unmodified numpad key even
        // though the physical event never reaches AMC. The global prefix uses
        // AMC's already installed, exception-guarded hook for every physical
        // numpad key. ShortcutCaptureWindow remains unchanged: unambiguous
        // operators such as Plus still use normal WPF input while it is open.
        return key.StartsWith("Numpad", StringComparison.Ordinal);
    }

    internal static bool IsNumpadEnterInput(uint virtualKey, uint flags) =>
        virtualKey == VirtualKeyReturn && (flags & LlkhfExtended) != 0;

    private bool MatchesHookPrefix(KbdLlHookStruct data) =>
        _hookPrefix is KeyChord prefix
        && string.Equals(
            KeyChord.NormalizeKey(prefix.Key),
            WindowsKeyMap.FromKeyboardInput(
                data.VirtualKeyCode,
                (data.Flags & LlkhfExtended) != 0),
            StringComparison.Ordinal)
        && prefix.Modifiers == ReadModifiers();

    private static KeyModifiers ReadModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (IsDown(0x11)) modifiers |= KeyModifiers.Ctrl;
        if (IsDown(0x12)) modifiers |= KeyModifiers.Alt;
        if (IsDown(0x10)) modifiers |= KeyModifiers.Shift;
        if (IsDown(0x5B) || IsDown(0x5C)) modifiers |= KeyModifiers.Windows;
        return modifiers;
    }

    private static bool IsModifier(uint key) => key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
    private static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KbdLlHookStruct
    {
        public readonly uint VirtualKeyCode;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
