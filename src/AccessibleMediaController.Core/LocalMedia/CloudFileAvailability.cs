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

    public static CloudFileState GetState(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return CloudFileState.Unavailable;
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & PlaceholderAttributes) != 0
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
        var knownCloudLocation = segments.Any(segment =>
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
            || segment.Equals("Dyski współdzielone", StringComparison.OrdinalIgnoreCase));
        if (knownCloudLocation || IsNetworkLocation(fullPath)) return true;
        try
        {
            var attributes = File.GetAttributes(path);
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

    private static bool IsNetworkLocation(string fullPath)
    {
        if (fullPath.StartsWith("\\\\", StringComparison.Ordinal)) return true;
        try
        {
            var root = Path.GetPathRoot(fullPath);
            return !string.IsNullOrWhiteSpace(root)
                && new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException)
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
}
