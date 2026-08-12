using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Input;

namespace AccessibleMediaController.Windows.Services;

internal sealed class GlobalPrefixService : IDisposable
{
    private const int HotKeyId = 0x41C0;
    private const int WmHotKey = 0x0312;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;
    private readonly Func<KeyChord, bool> _commandHandler;
    private readonly Action _prefixActivated;
    private readonly DispatcherTimer _timer;
    private readonly LowLevelKeyboardProc _hookProcedure;
    private readonly HashSet<uint> _suppressedKeys = [];
    private IntPtr _hookHandle;
    private bool _layerActive;
    private int _standardTimeout;
    private int _continuationTimeout;

    public GlobalPrefixService(IntPtr windowHandle, Func<KeyChord, bool> commandHandler, Action prefixActivated)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("Brak źródła okna WPF.");
        _commandHandler = commandHandler;
        _prefixActivated = prefixActivated;
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
        UnregisterHotKey(_windowHandle, HotKeyId);
        if (!WindowsKeyMap.TryGetVirtualKey(prefix.Key, out var virtualKey))
        {
            throw new InvalidOperationException($"Nieobsługiwany klawisz prefiksu: {prefix.Key}");
        }

        var modifiers = ModNoRepeat;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Alt)) modifiers |= 0x0001;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Ctrl)) modifiers |= 0x0002;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Shift)) modifiers |= 0x0004;
        if (prefix.Modifiers.HasFlag(KeyModifiers.Windows)) modifiers |= 0x0008;

        if (!RegisterHotKey(_windowHandle, HotKeyId, modifiers, virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Nie można zarejestrować prefiksu {prefix.Canonical}.");
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        UnregisterHotKey(_windowHandle, HotKeyId);
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
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
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
        if (code < 0) return CallNextHookEx(_hookHandle, code, wParam, lParam);

        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var keyDown = message is WmKeyDown or WmSysKeyDown;
        var keyUp = message is WmKeyUp or WmSysKeyUp;

        if (keyUp && _suppressedKeys.Remove(data.VirtualKeyCode)) return new IntPtr(1);
        if (!_layerActive || !keyDown) return CallNextHookEx(_hookHandle, code, wParam, lParam);

        _suppressedKeys.Add(data.VirtualKeyCode);
        if (IsModifier(data.VirtualKeyCode)) return new IntPtr(1);

        var keyName = WindowsKeyMap.FromVirtualKey(data.VirtualKeyCode);
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

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
