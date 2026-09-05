namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Keeps artwork and other non-playable RSS attachments out of the episode
/// catalogue without rejecting legitimate feeds whose audio URL has no file
/// extension or whose server uses a generic MIME type.
/// </summary>
public static class PodcastMediaSourceRules
{
    private static readonly HashSet<string> NonPlayableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif", ".bmp", ".gif", ".heic", ".heif", ".ico", ".jfif",
        ".jpeg", ".jpg", ".json", ".pdf", ".png", ".svg", ".tif", ".tiff",
        ".txt", ".webp", ".xml", ".zip"
    };

    private static readonly HashSet<string> PlayableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".aif", ".aiff", ".alac", ".flac", ".m4a", ".m4b",
        ".m4v", ".mka", ".mkv", ".mov", ".mp3", ".mp4", ".mpeg",
        ".mpg", ".oga", ".ogg", ".opus", ".ts", ".wav", ".webm", ".wma"
    };

    public static bool IsDefinitelyNonPlayable(string? address, string? mediaType)
    {
        var normalizedType = NormalizeMediaType(mediaType);
        if (normalizedType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return true;
        if (normalizedType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;
        if (normalizedType is "application/json"
            or "application/pdf"
            or "application/rss+xml"
            or "application/xml"
            or "application/zip")
        {
            return true;
        }
        if (!TryGetExtension(address, out var extension)) return false;
        return NonPlayableExtensions.Contains(extension);
    }

    public static int CandidateScore(Uri address, string? mediaType)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (IsDefinitelyNonPlayable(address.AbsoluteUri, mediaType)) return -1;

        var normalizedType = NormalizeMediaType(mediaType);
        if (normalizedType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return 500;
        if (normalizedType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return 450;
        if (normalizedType is "application/ogg"
            or "application/vnd.apple.mpegurl"
            or "application/x-mpegurl"
            or "application/dash+xml")
        {
            return 425;
        }
        if (TryGetExtension(address.AbsoluteUri, out var extension)
            && PlayableExtensions.Contains(extension))
        {
            return 400;
        }
        if (normalizedType is "application/octet-stream") return 300;

        // Many older podcast feeds omit both MIME type and extension and rely
        // on an HTTP redirect. Preserve that compatible path unless the source
        // is positively identifiable as artwork.
        return normalizedType.Length == 0 ? 200 : 100;
    }

    private static string NormalizeMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)) return string.Empty;
        var separator = mediaType.IndexOf(';');
        return (separator >= 0 ? mediaType[..separator] : mediaType).Trim().ToLowerInvariant();
    }

    private static bool TryGetExtension(string? address, out string extension)
    {
        extension = string.Empty;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return false;
        try
        {
            extension = Path.GetExtension(Uri.UnescapeDataString(uri.AbsolutePath));
            return extension.Length > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            return false;
        }
    }
}
