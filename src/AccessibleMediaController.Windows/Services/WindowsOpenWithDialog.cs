using System.Runtime.InteropServices;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Opens the native Windows application picker without requiring an existing
/// file association.
/// </summary>
internal static class WindowsOpenWithDialog
{
    public static void Show(nint ownerHandle, string filePath)
    {
        var info = new OpenAsInfo
        {
            FilePath = filePath,
            FileClass = null,
            Flags = OpenAsInfoFlags.Execute
        };
        Marshal.ThrowExceptionForHR(SHOpenWithDialog(ownerHandle, ref info));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenAsInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FilePath;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? FileClass;

        public OpenAsInfoFlags Flags;
    }

    [Flags]
    private enum OpenAsInfoFlags : uint
    {
        Execute = 0x00000004
    }

    [DllImport("shell32.dll")]
    private static extern int SHOpenWithDialog(nint ownerHandle, ref OpenAsInfo openAsInfo);
}
