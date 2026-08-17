using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Interop;

namespace AccessibleMediaController.Windows.Controls;

/// <summary>
/// Exposes the client bounds of a WPF window to accessibility clients.
/// This keeps positional status-bar lookup inside the actual content without
/// creating a second top-level window or changing keyboard focus.
/// </summary>
public class AccessibleWindow : Window
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ClientBoundsWindowAutomationPeer(this);

    private sealed class ClientBoundsWindowAutomationPeer : WindowAutomationPeer
    {
        private readonly Window _owner;

        public ClientBoundsWindowAutomationPeer(Window owner) : base(owner)
        {
            _owner = owner;
        }

        protected override Rect GetBoundingRectangleCore()
        {
            var handle = new WindowInteropHelper(_owner).Handle;
            if (handle == IntPtr.Zero || !GetClientRect(handle, out var clientRect))
            {
                return base.GetBoundingRectangleCore();
            }

            var topLeft = new NativePoint(clientRect.Left, clientRect.Top);
            var bottomRight = new NativePoint(clientRect.Right, clientRect.Bottom);
            if (!ClientToScreen(handle, ref topLeft) || !ClientToScreen(handle, ref bottomRight))
            {
                return base.GetBoundingRectangleCore();
            }

            return new Rect(
                topLeft.X,
                topLeft.Y,
                Math.Max(0, bottomRight.X - topLeft.X),
                Math.Max(0, bottomRight.Y - topLeft.Y));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rect);
}
