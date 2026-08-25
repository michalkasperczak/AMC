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
}
