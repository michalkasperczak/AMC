using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Sends files to the Windows Recycle Bin through the modern shell operation
/// API. Unlike the legacy SHFileOperation wrapper, IFileOperation understands
/// Cloud Files placeholders exposed by providers such as iCloud Drive.
/// </summary>
internal static class WindowsRecycleBin
{
    private const uint FofNoConfirmation = 0x0010;
    private const uint FofAllowUndo = 0x0040;
    private const uint FofNoErrorUi = 0x0400;
    private const uint FofxRecycleOnDelete = 0x00080000;
    private const uint FofxEarlyFailure = 0x00100000;

    public static void MoveFile(string path, nint ownerWindow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Plik już nie istnieje.", path);

        IFileOperation? operation = null;
        IShellItem? shellItem = null;
        try
        {
            operation = (IFileOperation)(object)new FileOperationComObject();
            operation.SetOperationFlags(
                FofNoConfirmation
                | FofAllowUndo
                | FofNoErrorUi
                | FofxRecycleOnDelete
                | FofxEarlyFailure);
            if (ownerWindow != 0) operation.SetOwnerWindow(ownerWindow);

            var shellItemId = typeof(IShellItem).GUID;
            SHCreateItemFromParsingName(path, 0, ref shellItemId, out shellItem);
            operation.DeleteItem(shellItem, 0);
            operation.PerformOperations();
            if (operation.GetAnyOperationsAborted())
            {
                throw new OperationCanceledException("Operacja Kosza została anulowana przez system.");
            }
        }
        finally
        {
            if (shellItem is not null && Marshal.IsComObject(shellItem))
                Marshal.FinalReleaseComObject(shellItem);
            if (operation is not null && Marshal.IsComObject(operation))
                Marshal.FinalReleaseComObject(operation);
        }
    }

    public static bool IsExpectedFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or OperationCanceledException
            or COMException
            or Win32Exception
            or SecurityException
            or InvalidOperationException
            or NotSupportedException;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [ComImport]
    [Guid("3AD05575-8857-4850-9277-11B85BDB8E09")]
    private sealed class FileOperationComObject;

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint bindContext, ref Guid handlerId, ref Guid interfaceId, out nint result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint displayNameType, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint attributeMask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        void Advise(nint progressSink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint operationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(nint progressDialog);
        void SetProperties(nint propertyChangeArray);
        void SetOwnerWindow(nint ownerWindow);
        void ApplyPropertiesToItem(IShellItem item);
        void ApplyPropertiesToItems(nint items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string newName, nint progressSink);
        void RenameItems(nint items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        void MoveItem(IShellItem item, IShellItem destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string newName, nint progressSink);
        void MoveItems(nint items, IShellItem destinationFolder);
        void CopyItem(IShellItem item, IShellItem destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string copyName, nint progressSink);
        void CopyItems(nint items, IShellItem destinationFolder);
        void DeleteItem(IShellItem item, nint progressSink);
        void DeleteItems(nint items);
        void NewItem(IShellItem destinationFolder, uint fileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string templateName, nint progressSink);
        void PerformOperations();
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetAnyOperationsAborted();
    }
}
