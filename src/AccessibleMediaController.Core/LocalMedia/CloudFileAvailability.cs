using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace AccessibleMediaController.Core.LocalMedia;

public enum CloudFileState
{
    Local,
    Placeholder,
    Unavailable
}

/// <summary>
/// Reads only Windows file attributes. It never opens the payload, so checking
/// a cloud-backed folder cannot hydrate its audio files.
/// </summary>
public static class CloudFileAvailability
{
    // Windows SDK attributes not exposed by every target framework's enum.
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes Unpinned = (FileAttributes)0x00100000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    private const FileAttributes PlaceholderAttributes =
        FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess | Unpinned;
    private const uint CfPlaceholderStatePlaceholder = 0x00000001;
    private const uint CfPlaceholderStatePartial = 0x00000010;
    private const uint CfPlaceholderStatePartiallyOnDisk = 0x00000020;
    private const uint CfPlaceholderStateInvalid = 0xFFFFFFFF;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;

    public static CloudFileState GetState(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return CloudFileState.Unavailable;
        try
        {
            if (!TryReadNativeMetadata(path, out var attributes, out var placeholderState))
            {
                attributes = File.GetAttributes(path);
                placeholderState = CfPlaceholderStateInvalid;
            }
            return IsPlaceholder(attributes, placeholderState)
                ? CloudFileState.Placeholder
                : CloudFileState.Local;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return CloudFileState.Unavailable;
        }
    }

    public static bool RequiresHydration(string path) =>
        GetState(path) == CloudFileState.Placeholder;

    /// <summary>
    /// Recognizes both Cloud Files placeholders and common mounted cloud
    /// locations. Google Drive can expose a streamed file without the Windows
    /// placeholder attributes, even though opening or seeking it may still
    /// require network access.
    /// </summary>
    public static bool MayRequireRemoteAccess(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return RequiresHydration(path);
        }

        foreach (var variable in new[]
                 {
                     "OneDrive",
                     "OneDriveConsumer",
                     "OneDriveCommercial",
                     "Dropbox"
                 })
        {
            var configuredRoot = Environment.GetEnvironmentVariable(variable);
            if (IsWithinRoot(fullPath, configuredRoot)) return true;
        }

        var segments = fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var knownCloudLocation = segments.Any(IsKnownCloudName);
        if (knownCloudLocation || IsNetworkOrCloudDrive(fullPath)) return true;
        try
        {
            if (TryReadNativeMetadata(path, out var attributes, out var placeholderState))
            {
                return IsPlaceholder(attributes, placeholderState)
                    || (attributes & FileAttributes.ReparsePoint) != 0
                    || (placeholderState != CfPlaceholderStateInvalid
                        && (placeholderState & CfPlaceholderStatePlaceholder) != 0);
            }

            attributes = File.GetAttributes(path);
            return (attributes & (PlaceholderAttributes | FileAttributes.ReparsePoint)) != 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsKnownCloudName(string segment) =>
            segment.Equals("iCloudDrive", StringComparison.OrdinalIgnoreCase)
            || segment.StartsWith("iCloud~", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("OneDrive", StringComparison.OrdinalIgnoreCase)
            || segment.StartsWith("OneDrive - ", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Dropbox", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Box", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Box Drive", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("pCloud Drive", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("MEGA", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Proton Drive", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Nextcloud", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("ownCloud", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Sync", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Google Drive", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("My Drive", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Mój dysk", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Shared drives", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("Dyski współdzielone", StringComparison.OrdinalIgnoreCase);

    private static bool IsNetworkOrCloudDrive(string fullPath)
    {
        if (fullPath.StartsWith("\\\\", StringComparison.Ordinal)) return true;
        try
        {
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root)) return false;
            var drive = new DriveInfo(root);
            if (drive.DriveType == DriveType.Network) return true;

            // Google Drive for desktop and similar providers can expose a
            // virtual fixed drive. Reading its volume label is metadata-only
            // and avoids relying exclusively on a particular mount path.
            return IsKnownCloudName(drive.VolumeLabel)
                || drive.VolumeLabel.Contains("Google Drive", StringComparison.OrdinalIgnoreCase)
                || drive.VolumeLabel.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)
                || drive.VolumeLabel.Contains("Dropbox", StringComparison.OrdinalIgnoreCase)
                || drive.VolumeLabel.Contains("iCloud", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Classifies attributes without opening file data. This public overload is
    /// also useful to verify provider-independent placeholder behavior in
    /// automated tests where a real cloud sync root is unavailable.
    /// </summary>
    public static CloudFileState ClassifyMetadata(
        FileAttributes attributes,
        uint cloudFilesPlaceholderState = CfPlaceholderStateInvalid) =>
        IsPlaceholder(attributes, cloudFilesPlaceholderState)
            ? CloudFileState.Placeholder
            : CloudFileState.Local;

    private static bool IsPlaceholder(FileAttributes attributes, uint placeholderState) =>
        (attributes & PlaceholderAttributes) != 0
        || (placeholderState != CfPlaceholderStateInvalid
            && (placeholderState & (CfPlaceholderStatePartial | CfPlaceholderStatePartiallyOnDisk)) != 0);

    private static bool TryReadNativeMetadata(
        string path,
        out FileAttributes attributes,
        out uint placeholderState)
    {
        attributes = 0;
        placeholderState = CfPlaceholderStateInvalid;
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var handle = CreateFileW(
                path,
                0,
                FileShareRead | FileShareWrite | FileShareDelete,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOpenReparsePoint | FileFlagBackupSemantics,
                IntPtr.Zero);
            if (handle.IsInvalid) return false;

            if (!GetFileInformationByHandleEx(
                    handle,
                    FileInfoByHandleClass.FileAttributeTagInfo,
                    out var information,
                    (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
            {
                return false;
            }

            attributes = (FileAttributes)information.FileAttributes;
            try
            {
                placeholderState = CfGetPlaceholderStateFromAttributeTag(
                    information.FileAttributes,
                    information.ReparseTag);
            }
            catch (Exception exception) when (
                exception is DllNotFoundException or EntryPointNotFoundException)
            {
                placeholderState = CfPlaceholderStateInvalid;
            }
            return true;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsWithinRoot(string fullPath, string? configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot)) return false;
        try
        {
            var root = Path.GetFullPath(configuredRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return false;
        }
    }

    private enum FileInfoByHandleClass
    {
        FileAttributeTagInfo = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileAttributeTagInfo
    {
        public readonly uint FileAttributes;
        public readonly uint ReparseTag;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle fileHandle,
        FileInfoByHandleClass fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

    [DllImport("cldapi.dll", ExactSpelling = true)]
    private static extern uint CfGetPlaceholderStateFromAttributeTag(
        uint fileAttributes,
        uint reparseTag);
}
