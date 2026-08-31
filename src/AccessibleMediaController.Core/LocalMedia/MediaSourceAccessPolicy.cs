namespace AccessibleMediaController.Core.LocalMedia;

/// <summary>
/// Describes where bytes must be obtained from. The classification is shared
/// by local playback, radio and future session/download adapters so that none
/// of them has to special-case one cloud provider.
/// </summary>
public enum MediaSourceAccessKind
{
    Unknown,
    LocalFile,
    RemoteFile,
    NetworkStream
}

public readonly record struct MediaSourceAccessPolicy(MediaSourceAccessKind Kind)
{
    public bool RequiresRemoteAccess =>
        Kind is MediaSourceAccessKind.RemoteFile or MediaSourceAccessKind.NetworkStream;

    /// <summary>
    /// Even a nominally local file can be delayed by a filesystem filter,
    /// removable drive or security scanner. Payload I/O must therefore never
    /// run on the accessible UI thread for any recognized media source.
    /// </summary>
    public bool RequiresBackgroundIo => Kind != MediaSourceAccessKind.Unknown;

    public static MediaSourceAccessPolicy Classify(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return new MediaSourceAccessPolicy(MediaSourceAccessKind.Unknown);

        var trimmed = source.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && !LooksLikeWindowsDrivePath(trimmed))
        {
            if (!uri.IsFile)
                return new MediaSourceAccessPolicy(MediaSourceAccessKind.NetworkStream);

            trimmed = uri.LocalPath;
        }

        return new MediaSourceAccessPolicy(
            CloudFileAvailability.MayRequireRemoteAccess(trimmed)
                ? MediaSourceAccessKind.RemoteFile
                : MediaSourceAccessKind.LocalFile);
    }

    private static bool LooksLikeWindowsDrivePath(string source) =>
        source.Length >= 3
        && char.IsLetter(source[0])
        && source[1] == ':'
        && source[2] is '\\' or '/';
}
