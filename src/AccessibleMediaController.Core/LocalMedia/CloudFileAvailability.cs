using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace AccessibleMediaController.Core.LocalMedia;

public enum CloudFileState
{
    Local,
    Placeholder,
    Unavailable
}

/// <summary>
/// What a caller that wants to rewrite a file IN PLACE is allowed to do, and
/// why. Deliberately distinguishes "not downloaded" from "not there at all",
/// "no permission" and "cannot be determined", so no message claims knowledge
/// the metadata did not give.
///
/// This is an EDIT-ONLY vocabulary. It is intentionally separate from
/// <see cref="CloudFileState"/>, which keeps its original two-way playback
/// meaning: adding a third state there would have changed every playback and
/// risk decision that reads it.
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
    /// Nothing proved the content is here and nothing proved it is missing.
    /// Must not lead to rewriting the original, and must not be reported as
    /// "not downloaded" either.
    /// </summary>
    Unknown
}

public readonly record struct CloudEditAvailability(CloudEditOutcome Outcome)
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
        // Neutral on purpose: the cause may be an unrecognized reparse point, a
        // volume we cannot identify or a read error — not necessarily a cloud
        // provider staying silent.
        _ =>
            "Nie można potwierdzić, że ten plik jest w całości na tym dysku, "
            + "więc edycja oryginału została wstrzymana."
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
    private const FileAttributes PlaceholderAttributes =
        FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess | Unpinned;
    // Attributes that state outright that the payload may be elsewhere. PINNED
    // and UNPINNED are NOT here: per the Win32 file attribute constants they
    // state the user's RETENTION INTENT, not whether the content is complete.
    private const FileAttributes MissingDataAttributes =
        FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess;
    private const uint CfPlaceholderStatePlaceholder = 0x00000001;
    private const uint CfPlaceholderStatePartial = 0x00000010;
    private const uint CfPlaceholderStatePartiallyOnDisk = 0x00000020;
    private const uint CfPlaceholderStateIncomplete =
        CfPlaceholderStatePartial | CfPlaceholderStatePartiallyOnDisk;
    private const uint CfPlaceholderStateInvalid = 0xFFFFFFFF;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    // Keeps the probe on the link / placeholder itself: the payload is never
    // opened, so a metadata read can never hydrate an unpinned cloud file.
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidName = 123;
    private const int ErrorBadPathname = 161;
    private const int MaximumLinkHops = 4;

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
    ///
    /// Unchanged legacy two-way classification used by playback and risk
    /// decisions. Editing must NOT use it — see
    /// <see cref="ClassifyMetadataForEdit"/>.
    /// </summary>
    public static CloudFileState ClassifyMetadata(
        FileAttributes attributes,
        uint cloudFilesPlaceholderState = CfPlaceholderStateInvalid) =>
        IsPlaceholder(attributes, cloudFilesPlaceholderState)
            ? CloudFileState.Placeholder
            : CloudFileState.Local;

    /// <summary>
    /// Edit-only classification of already-read metadata. Separate from
    /// <see cref="ClassifyMetadata"/> so that making edits stricter cannot
    /// change playback behavior.
    ///
    /// <paramref name="localStorageProven"/> must be true only when the caller
    /// actually confirmed that the file sits on a local, identified file system
    /// (see <see cref="TryProveLocalStorage"/>). Callers that did not look must
    /// pass false; this method will then refuse to call an attribute-less file
    /// local, instead of guessing.
    /// </summary>
    public static CloudEditOutcome ClassifyMetadataForEdit(
        FileAttributes attributes,
        uint cloudFilesPlaceholderState,
        bool localStorageProven)
    {
        // 1. Attributes that state outright that the payload may be elsewhere.
        //    OFFLINE, RECALL_ON_OPEN and RECALL_ON_DATA_ACCESS are provider
        //    independent and always win, pinned or not.
        if ((attributes & MissingDataAttributes) != 0) return CloudEditOutcome.NeedsDownload;

        var hasCloudFilesMetadata = cloudFilesPlaceholderState != CfPlaceholderStateInvalid
            && cloudFilesPlaceholderState != 0;
        if (hasCloudFilesMetadata)
        {
            // 2. Cloud Files answered. CF_PLACEHOLDER_STATE_PARTIAL means the
            //    content is not ready for use (PARTIALLY_ON_DISK never appears
            //    without it), so a pinned-but-partial file is still refused.
            if ((cloudFilesPlaceholderState & CfPlaceholderStateIncomplete) != 0)
                return CloudEditOutcome.NeedsDownload;
            // 3. A complete Cloud Files placeholder: PLACEHOLDER without
            //    PARTIAL, with no recall or offline flag. This is positive
            //    evidence from the sync engine itself, so PINNED/UNPINNED —
            //    retention policy, not completeness — is not consulted.
            if ((cloudFilesPlaceholderState & CfPlaceholderStatePlaceholder) != 0)
                return CloudEditOutcome.Editable;
            // 4. Cloud Files reported something else (for example only
            //    SYNC_ROOT) about a reparse point. That proves nothing about
            //    this file's content.
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return CloudEditOutcome.Unknown;
        }

        // 5. No usable Cloud Files proof. A reparse point is NOT local evidence:
        //    the caller must follow it and judge the target instead. PINNED is
        //    deliberately NOT a shortcut here — "keep this one locally" is an
        //    intent the provider may not have fulfilled yet, and in 6a2b914 it
        //    let an unknown reparse tag through before it was even examined.
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return CloudEditOutcome.Unknown;

        // 6. Retention attributes (PINNED / UNPINNED) are never consulted as
        //    evidence in EITHER direction, and that cuts both ways. They do not
        //    prove the payload is here — that was the hole in 6a2b914 — but a
        //    file that is not a reparse point has no mechanism to redirect a
        //    read somewhere else, so marking it pinned cannot make it less
        //    present. Measured natively: treating PINNED alone as grounds for
        //    refusal produced a false refusal on an ordinary resident NTFS file.
        //
        // 7. So the decision rests on evidence only: no missing-data attribute
        //    above, no redirect, and storage that was actually identified as
        //    local. Anything less is reported as unproven rather than guessed.
        return localStorageProven ? CloudEditOutcome.Editable : CloudEditOutcome.Unknown;
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
            return new CloudEditAvailability(CloudEditOutcome.Missing);
        return new CloudEditAvailability(ProbeForEdit(path, MaximumLinkHops));
    }

    private static CloudEditOutcome ProbeForEdit(string path, int hopsLeft)
    {
        if (hopsLeft <= 0) return CloudEditOutcome.Unknown;

        FileAttributes attributes;
        uint placeholderState;
        bool localStorageProven;
        if (OperatingSystem.IsWindows())
        {
            // Deliberately no File.Exists: it answers false for a file that
            // exists but cannot be examined, which is how a permission problem
            // used to be reported as a missing file. The Win32 error code from
            // the one metadata open we need says which of the two happened.
            var outcome = TryReadForEdit(
                path, out attributes, out placeholderState, out localStorageProven);
            if (outcome is not null) return outcome.Value;
        }
        else
        {
            try
            {
                attributes = File.GetAttributes(path);
            }
            catch (FileNotFoundException) { return CloudEditOutcome.Missing; }
            catch (DirectoryNotFoundException) { return CloudEditOutcome.Missing; }
            catch (UnauthorizedAccessException) { return CloudEditOutcome.AccessDenied; }
            catch (Exception exception) when (
                exception is IOException or ArgumentException or NotSupportedException)
            {
                return CloudEditOutcome.Unknown;
            }
            if ((attributes & FileAttributes.Directory) != 0) return CloudEditOutcome.Unknown;
            placeholderState = CfPlaceholderStateInvalid;
            // Cloud Files placeholders and virtual Windows volumes do not exist
            // here; this branch only serves non-Windows test and build hosts.
            localStorageProven = true;
        }

        var verdict = ClassifyMetadataForEdit(attributes, placeholderState, localStorageProven);
        if (verdict == CloudEditOutcome.Unknown
            && (attributes & FileAttributes.ReparsePoint) != 0)
        {
            // An unrecognized reparse point may simply be an ordinary symbolic
            // link or a file reached through one. Follow it with metadata only
            // and let the real target decide — never with the permissive legacy
            // GetState, which is playback policy, not edit policy.
            var target = ResolveLinkTarget(path);
            if (target is not null
                && !string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
            {
                return ProbeForEdit(target, hopsLeft - 1);
            }
        }
        return verdict;
    }

    /// <summary>
    /// Single metadata-only open. Returns a non-null outcome when the open or
    /// the query itself already decided the answer (missing, denied, unreadable).
    /// </summary>
    private static CloudEditOutcome? TryReadForEdit(
        string path,
        out FileAttributes attributes,
        out uint placeholderState,
        out bool localStorageProven)
    {
        attributes = 0;
        placeholderState = CfPlaceholderStateInvalid;
        localStorageProven = false;
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
            if (handle.IsInvalid)
            {
                return Marshal.GetLastWin32Error() switch
                {
                    ErrorFileNotFound or ErrorPathNotFound or ErrorInvalidName or ErrorBadPathname =>
                        CloudEditOutcome.Missing,
                    ErrorAccessDenied => CloudEditOutcome.AccessDenied,
                    _ => CloudEditOutcome.Unknown
                };
            }

            if (!GetFileInformationByHandleEx(
                    handle,
                    FileInfoByHandleClass.FileAttributeTagInfo,
                    out var information,
                    (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
            {
                return Marshal.GetLastWin32Error() == ErrorAccessDenied
                    ? CloudEditOutcome.AccessDenied
                    : CloudEditOutcome.Unknown;
            }

            attributes = (FileAttributes)information.FileAttributes;
            if ((attributes & FileAttributes.Directory) != 0) return CloudEditOutcome.Unknown;
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
            localStorageProven = TryProveLocalStorage(handle, path);
            return null;
        }
        catch (Exception exception) when (
            exception is IOException
                or ArgumentException
                or NotSupportedException)
        {
            return CloudEditOutcome.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return CloudEditOutcome.AccessDenied;
        }
    }

    /// <summary>
    /// Confirms that the handle refers to a local, identified file system, using
    /// only documented read-only metadata: GetVolumeInformationByHandleW for the
    /// file system name and DriveType for the kind of volume. Returns false
    /// whenever that cannot be established, so an unidentified virtual or remote
    /// volume is never silently called local.
    ///
    /// Known limit, stated instead of hidden: a virtual file system that reports
    /// itself as NTFS/FAT on a fixed drive is indistinguishable from real local
    /// storage by this metadata alone.
    /// </summary>
    private static bool TryProveLocalStorage(SafeFileHandle handle, string path)
    {
        var fileSystem = new StringBuilder(64);
        try
        {
            if (!GetVolumeInformationByHandleW(
                    handle,
                    null,
                    0,
                    out _,
                    out _,
                    out _,
                    fileSystem,
                    (uint)fileSystem.Capacity))
            {
                return false;
            }
        }
        catch (Exception exception) when (
            exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }

        var name = fileSystem.ToString();
        var localFileSystem = name.Equals("NTFS", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ReFS", StringComparison.OrdinalIgnoreCase)
            || name.Equals("exFAT", StringComparison.OrdinalIgnoreCase)
            || name.Equals("FAT32", StringComparison.OrdinalIgnoreCase)
            || name.Equals("FAT", StringComparison.OrdinalIgnoreCase);
        if (!localFileSystem) return false;

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root)) return false;
            if (root.StartsWith("\\\\", StringComparison.Ordinal)) return false;
            var type = new DriveInfo(root).DriveType;
            return type is DriveType.Fixed or DriveType.Removable;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return false;
        }
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(
        SafeFileHandle fileHandle,
        StringBuilder? volumeNameBuffer,
        uint volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        uint fileSystemNameSize);

    [DllImport("cldapi.dll", ExactSpelling = true)]
    private static extern uint CfGetPlaceholderStateFromAttributeTag(
        uint fileAttributes,
        uint reparseTag);
}
