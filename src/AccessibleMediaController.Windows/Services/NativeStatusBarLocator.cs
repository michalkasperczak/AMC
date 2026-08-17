using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Gives NVDA's positional status-bar lookup a real native status-bar object
/// at the lower-left frame coordinate of the owner window.
/// </summary>
internal sealed class NativeStatusBarLocator : IDisposable
{
    private const int IccBarClasses = 0x00000004;
    private const int DwmwaExtendedFrameBounds = 9;
    private const int SbSetTextW = 0x040B;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const uint LwaAlpha = 0x00000002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExLayered = 0x00080000;

    private readonly IntPtr _ownerHandle;
    private IntPtr _handle;

    public NativeStatusBarLocator(IntPtr ownerHandle)
    {
        _ownerHandle = ownerHandle;
        var controls = new InitCommonControls
        {
            Size = Marshal.SizeOf<InitCommonControls>(),
            Classes = IccBarClasses
        };
        InitCommonControlsEx(ref controls);

        _handle = CreateWindowEx(
            WsExToolWindow | WsExNoActivate | WsExLayered,
            "msctls_statusbar32",
            string.Empty,
            WsPopup | WsVisible,
            0,
            0,
            1,
            1,
            ownerHandle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (_handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Nie udało się utworzyć punktu paska stanu.");
        }

        // Almost transparent but still present for hit testing and accessibility.
        SetLayeredWindowAttributes(_handle, 0, 1, LwaAlpha);
        UpdatePosition();
    }

    public void SetText(string text)
    {
        if (_handle == IntPtr.Zero) return;
        SetWindowText(_handle, text);
        SendMessage(_handle, SbSetTextW, IntPtr.Zero, text);
    }

    public void UpdatePosition()
    {
        if (_handle == IntPtr.Zero) return;
        if (!IsWindowVisible(_ownerHandle) || IsIconic(_ownerHandle))
        {
            ShowWindow(_handle, SwHide);
            return;
        }

        if (!GetWindowRect(_ownerHandle, out var windowRect)) return;
        var frameRect = windowRect;
        if (DwmGetWindowAttribute(
                _ownerHandle,
                DwmwaExtendedFrameBounds,
                out var extendedFrameRect,
                Marshal.SizeOf<NativeRect>()) == 0)
        {
            frameRect = extendedFrameRect;
        }

        // UIA and IAccessible can differ by several pixels in whether an
        // invisible resize frame belongs to the window bounds. Cover both
        // lower-left candidates while remaining visually imperceptible.
        var left = Math.Min(windowRect.Left, frameRect.Left) - 8;
        var bottomTop = Math.Min(windowRect.Bottom, frameRect.Bottom) - 16;
        var width = Math.Abs(windowRect.Left - frameRect.Left) + 24;
        var height = Math.Abs(windowRect.Bottom - frameRect.Bottom) + 24;
        SetWindowPos(
            _handle,
            IntPtr.Zero,
            left,
            bottomTop,
            width,
            height,
            SwpNoActivate | SwpShowWindow);
        ShowWindow(_handle, SwShowNoActivate);
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        DestroyWindow(_handle);
        _handle = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InitCommonControls
    {
        public int Size;
        public int Classes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitCommonControlsEx(ref InitCommonControls controls);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window,
        int attribute,
        out NativeRect value,
        int valueSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowText(IntPtr window, string text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
