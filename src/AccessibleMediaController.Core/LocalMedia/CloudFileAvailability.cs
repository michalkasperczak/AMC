using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security;

namespace AccessibleMediaController.Core.LocalMedia;

public enum CloudFileState
{
    Local,
    Placeholder,
    /// <summary>
    /// Metadata exists and gives no proof either way: an unrecognized reparse
    /// point, or a provider that marks retention intent without exposing Cloud
    /// Files completeness. Must never be treated as a guarantee of local data.
    /// </summary>
    Unknown,
    Unavailable
}

/// <summary>
/// What a caller that wants to rewrite a file in place is allowed to do, and
/// why. Deliberately distinguishes "not downloaded" from "not there at all",
/// "no permission" and "cannot be determined", so no message claims knowledge
/// the metadata did not give.
/// </summary>
public enum CloudEditOutcome
{
    /// <summary>Content is proven to be on this disk. Editing may proceed.</summary>
    Editable,
    /// <summary>Cloud metadata says the content is not (fully) here.</summary>
    NeedsDownload,
    /// <summary>The file is not there.</summary>
    Missing,
    /// <summary>The file is there, but its metadata could not be read.</summary>
    AccessDenied,
    /// <summary>
    /// The provider gave no proof either way. Must not lead to rewriting the
    /// original, and must not be reported as "not downloaded" either.
    /// </summary>
    Unknown
}

public readonly record struct CloudEditAvailability(CloudEditOutcome Outcome, CloudFileState State)
{
    public bool CanEdit => Outcome == CloudEditOutcome.Editable;

    /// <summary>
    /// One truthful Polish sentence per outcome, used by both the window and
    /// the services so a user never hears two different stories about the same
    /// file. Empty for <see cref="CloudEditOutcome.Editable"/>.
    /// </summary>
    public string Message => Outcome switch
    {
        CloudEditOutcome.Editable => string.Empty,
        CloudEditOutcome.NeedsDownload =>
            "Plik nie jest w pełni dostępny lokalnie. Pobierz go świadomie z chmury i spróbuj ponownie.",
        CloudEditOutcome.Missing =>
            "Nie znaleziono pliku.",
        CloudEditOutcome.AccessDenied =>
            "Nie udało się odczytać informacji o pliku. Sprawdź uprawnienia i spróbuj ponownie.",
        _ =>
            "Nie można potwierdzić, czy ten plik jest w całości na dysku. "
            + "Dostawca chmury nie podaje tej informacji, więc edycja oryginału została wstrzymana."
    };
}

/// <summary>
/// Reads only Windows file attributes. It never opens the payload, so checking
/// a cloud-backed folder cannot hydrate its audio files.
/// </summary>
public static class CloudFileAvailability
{
    // Windows SDK attributes not exposed by every target framework's enum.
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes Pinned = (FileAttributes)0x00080000;
    private const FileAttributes Unpinned = (FileAttributes)0x00100000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    // Attributes that really mean "the data may not be here". FILE_ATTRIBUTE_PINNED
    // and FILE_ATTRIBUTE_UNPINNED are deliberately NOT in this set: per the Win32
    // file attribute constants they state the user's RETENTION INTENT ("keep it
    // local" / "do not keep it local when unused"), not whether the content is
    // currently complete on disk. Treating UNPINNED as missing data is what
    // refused edits of ordinary, fully downloaded recordings.
    private const FileAttributes MissingDataAttributes =
        FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess;
    private const FileAttributes PlaceholderAttributes =
        MissingDataAttributes | Unpinned;
    private const uint CfPlaceholderStatePlaceholder = 0x00000001;
    private const uint CfPlaceholderStateSyncRoot = 0x00000002;
    private const uint CfPlaceholderStateEssentialPropertyPresent = 0x00000004;
    private const uint CfPlaceholderStateInSync = 0x00000008;
    private const uint CfPlaceholderStatePartial = 0x00000010;
    private const uint CfPlaceholderStatePartiallyOnDisk = 0x00000020;
    private const uint CfPlaceholderStateIncomplete =
        CfPlaceholderStatePartial | CfPlaceholderStatePartiallyOnDisk;
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
            return ClassifyMetadata(attributes, placeholderState);
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
        GetState(path) != CloudFileState.Local;

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
        // iCloud marks fully downloaded, pinned files as reparse points too.
        // The path name alone therefore cannot decide whether playback still
        // needs the cloud. Native placeholder metadata can make that
        // distinction without opening or hydrating the payload.
        if (segments.Any(IsICloudName))
        {
            return GetState(path) != CloudFileState.Local;
        }
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
            IsICloudName(segment)
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

    private static bool IsICloudName(string segment) =>
        segment.Equals("iCloudDrive", StringComparison.OrdinalIgnoreCase)
        || segment.StartsWith("iCloud~", StringComparison.OrdinalIgnoreCase);

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
        uint cloudFilesPlaceholderState = CfPlaceholderStateInvalid)
    {
        // 1. Attributes that state outright that the payload may be elsewhere.
        //    OFFLINE, RECALL_ON_OPEN and RECALL_ON_DATA_ACCESS are provider
        //    independent and always win.
        if ((attributes & MissingDataAttributes) != 0) return CloudFileState.Placeholder;

        var hasCloudFilesMetadata = cloudFilesPlaceholderState != CfPlaceholderStateInvalid
            && cloudFilesPlaceholderState != 0;
        if (hasCloudFilesMetadata)
        {
            // 2. Cloud Files answered. CF_PLACEHOLDER_STATE_PARTIAL means the
            //    content is not ready for use (PARTIALLY_ON_DISK never appears
            //    without it), so a pinned-but-partial file is still refused.
            if ((cloudFilesPlaceholderState & CfPlaceholderStateIncomplete) != 0)
                return CloudFileState.Placeholder;
            // 3. A complete placeholder: PLACEHOLDER|IN_SYNC without PARTIAL.
            //    PINNED/UNPINNED is retention policy, not completeness, so it is
            //    not consulted here.
            if ((cloudFilesPlaceholderState & CfPlaceholderStatePlaceholder) != 0)
                return CloudFileState.Local;
            // 4. Cloud Files reported something else (e.g. only SYNC_ROOT) about
            //    a file we cannot otherwise prove. Do not guess.
            return (attributes & FileAttributes.ReparsePoint) != 0
                ? CloudFileState.Unknown
                : CloudFileState.Local;
        }

        // 5. No Cloud Files metadata (older provider, or cldapi unavailable).
        //    FILE_ATTRIBUTE_PINNED is the provider saying "this one is kept
        //    locally"; with no recall or offline flag beside it, that is the
        //    shape a hydrated iCloud file has, and refusing it would be the
        //    very false refusal this seam exists to remove.
        if ((attributes & Pinned) != 0) return CloudFileState.Local;
        //    Anything else that is a reparse point, or merely marked "do not
        //    keep local", cannot prove the content is here. Report that
        //    honestly instead of claiming either answer.
        if ((attributes & (FileAttributes.ReparsePoint | Unpinned)) != 0)
            return CloudFileState.Unknown;

        // 6. An ordinary file with no cloud metadata at all.
        return CloudFileState.Local;
    }

    /// <summary>
    /// The ONE decision shared by every caller that is about to rewrite a file
    /// in place: the editing UI and the editing services alike. It is separate
    /// from <see cref="MayRequireRemoteAccess"/> on purpose — that one also
    /// guards playback timeouts, retries and seeking, where being cautious about
    /// a cloud-mounted path costs nothing. Refusing an edit, by contrast, blocks
    /// work on a file the user already has.
    ///
    /// Provider independent: no folder, drive label or environment variable of
    /// any brand takes part in it. Only documented Windows metadata does.
    /// </summary>
    public static CloudEditAvailability GetEditAvailability(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new CloudEditAvailability(CloudEditOutcome.Missing, CloudFileState.Unavailable);
        try
        {
            if (!File.Exists(path))
                return new CloudEditAvailability(CloudEditOutcome.Missing, CloudFileState.Unavailable);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new CloudEditAvailability(CloudEditOutcome.AccessDenied, CloudFileState.Unavailable);
        }

        FileAttributes attributes;
        uint placeholderState;
        var nativeRead = false;
        try
        {
            nativeRead = TryReadNativeMetadata(path, out attributes, out placeholderState);
            if (!nativeRead)
            {
                attributes = File.GetAttributes(path);
                placeholderState = CfPlaceholderStateInvalid;
            }
        }
        catch (FileNotFoundException)
        {
            return new CloudEditAvailability(CloudEditOutcome.Missing, CloudFileState.Unavailable);
        }
        catch (DirectoryNotFoundException)
        {
            return new CloudEditAvailability(CloudEditOutcome.Missing, CloudFileState.Unavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return new CloudEditAvailability(CloudEditOutcome.AccessDenied, CloudFileState.Unavailable);
        }
        catch (Exception exception) when (
            exception is IOException or ArgumentException or NotSupportedException)
        {
            return new CloudEditAvailability(CloudEditOutcome.Unknown, CloudFileState.Unavailable);
        }

        var state = ClassifyMetadata(attributes, placeholderState);

        // A link may itself look perfectly local while its target is not. Follow
        // it with metadata only; the payload is never opened.
        if (state == CloudFileState.Local
            && (attributes & FileAttributes.ReparsePoint) != 0)
        {
            var target = ResolveLinkTarget(path);
            if (target is null) return new CloudEditAvailability(CloudEditOutcome.Unknown, CloudFileState.Unknown);
            if (!string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
            {
                var targetState = GetState(target);
                if (targetState != CloudFileState.Local)
                    state = targetState == CloudFileState.Placeholder
                        ? CloudFileState.Placeholder
                        : CloudFileState.Unknown;
            }
        }

        return state switch
        {
            CloudFileState.Local => new CloudEditAvailability(CloudEditOutcome.Editable, state),
            CloudFileState.Placeholder => new CloudEditAvailability(CloudEditOutcome.NeedsDownload, state),
            CloudFileState.Unavailable => new CloudEditAvailability(CloudEditOutcome.AccessDenied, state),
            _ => new CloudEditAvailability(CloudEditOutcome.Unknown, state)
        };
    }

    private static string? ResolveLinkTarget(string path)
    {
        try
        {
            var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
            return target is null ? path : target.FullName;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or SecurityException)
        {
            return null;
        }
    }

    private static bool IsPlaceholder(FileAttributes attributes, uint placeholderState) =>
        ClassifyMetadata(attributes, placeholderState) != CloudFileState.Local;

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
