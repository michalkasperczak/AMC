using System.Globalization;
using System.Text;

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
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"
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
        try
        {
            value = value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // A malformed external feed must not prevent saving an otherwise playable episode.
        }

        var result = new StringBuilder(value.Length);
        var pendingSpace = false;
        var pendingSeparator = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (IsQuotationMark(rune)) continue;
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = result.Length > 0;
                continue;
            }
            if (IsPortableSeparator(rune))
            {
                pendingSeparator = result.Length > 0;
                pendingSpace = false;
                continue;
            }
            if (Rune.IsControl(rune)
                || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format
                    or UnicodeCategory.LineSeparator
                    or UnicodeCategory.ParagraphSeparator)
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSeparator)
            {
                AppendSeparator(result);
            }
            else if (pendingSpace && result.Length > 0 && result[^1] != ' ')
            {
                result.Append(' ');
            }
            pendingSpace = false;
            pendingSeparator = false;
            result.Append(rune.ToString());
        }

        value = result.ToString().Trim().TrimStart('.').TrimEnd('.', ' ');
        if (value.Length > MaximumBaseNameLength)
        {
            var length = MaximumBaseNameLength;
            if (char.IsHighSurrogate(value[length - 1])) length--;
            value = value[..length].TrimEnd('.', ' ', '-');
        }
        if (value.Length == 0) value = "Odcinek podcastu";
        if (IsReservedBaseName(value)) value = $"_{value}";
        return value;
    }

    private static bool IsReservedBaseName(string value)
    {
        var firstSegment = value.Split('.', 2)[0].TrimEnd(' ');
        return ReservedNames.Contains(firstSegment);
    }

    private static void AppendSeparator(StringBuilder result)
    {
        while (result.Length > 0 && result[^1] == ' ') result.Length--;
        if (result.Length == 0 || result[^1] == '-') return;
        result.Append(" - ");
    }

    private static bool IsPortableSeparator(Rune rune)
    {
        return rune.Value is ',' or ';' or ':' or '/' or '\\' or '|' or '<' or '>' or '?' or '*'
            or 0x060C // Arabic comma
            or 0x3001 // Ideographic comma
            or 0xFF0C // Full-width comma
            or 0xFF1A // Full-width colon
            or 0xFF1B; // Full-width semicolon
    }

    private static bool IsQuotationMark(Rune rune)
    {
        return rune.Value is '\'' or '"' or '`'
            or 0x00AB or 0x00BB
            or 0x2018 or 0x2019 or 0x201A or 0x201B
            or 0x201C or 0x201D or 0x201E or 0x201F
            or 0x2032 or 0x2033 or 0x2039 or 0x203A
            or 0x275B or 0x275C or 0x275D or 0x275E
            or 0x301D or 0x301E or 0x301F
            or 0xFF02 or 0xFF07;
    }
}
