using System.Globalization;

namespace AccessibleMediaController.Core.Podcasts;

public static class PodcastDownloadNaming
{
    private const int MaximumBaseNameLength = 140;
    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".m4a", ".aac", ".mp4", ".ogg", ".oga", ".opus", ".wav", ".wave",
        ".flac", ".wma", ".webm"
    };
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string SuggestedFileName(
        string? title,
        string? mediaUrl,
        string? mediaType)
    {
        var baseName = SanitizeBaseName(title);
        return baseName + ResolveExtension(mediaUrl, mediaType);
    }

    public static string UniquePath(string folder, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var fullFolder = Path.GetFullPath(folder);
        var safeFileName = Path.GetFileName(fileName);
        var candidate = Path.Combine(fullFolder, safeFileName);
        var baseName = Path.GetFileNameWithoutExtension(safeFileName);
        var extension = Path.GetExtension(safeFileName);
        for (var suffix = 2; File.Exists(candidate) || Directory.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(fullFolder, $"{baseName} ({suffix.ToString(CultureInfo.InvariantCulture)}){extension}");
        }
        return candidate;
    }

    public static string ResolveExtension(string? mediaUrl, string? mediaType)
    {
        if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(Uri.UnescapeDataString(uri.AbsolutePath));
            if (KnownExtensions.Contains(extension)) return extension.ToLowerInvariant();
        }

        var normalizedType = mediaType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "audio/aac" or "audio/aacp" => ".aac",
            "audio/mp4" or "audio/x-m4a" or "video/mp4" => ".m4a",
            "audio/ogg" or "application/ogg" => ".ogg",
            "audio/opus" => ".opus",
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            "audio/flac" or "audio/x-flac" => ".flac",
            "audio/x-ms-wma" => ".wma",
            "audio/webm" => ".webm",
            _ => ".mp3"
        };
    }

    private static string SanitizeBaseName(string? title)
    {
        var value = string.IsNullOrWhiteSpace(title) ? "Odcinek podcastu" : title.Trim();
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        value = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        value = value.Trim().TrimEnd('.', ' ');
        if (value.Length > MaximumBaseNameLength) value = value[..MaximumBaseNameLength].TrimEnd('.', ' ');
        if (value.Length == 0) value = "Odcinek podcastu";
        if (ReservedNames.Contains(value)) value += "_";
        return value;
    }
}
