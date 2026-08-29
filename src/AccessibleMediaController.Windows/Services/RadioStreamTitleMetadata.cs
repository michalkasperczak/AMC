using System.Text;

namespace AccessibleMediaController.Windows.Services;

internal sealed class RadioStreamTitleChangedEventArgs(string? streamTitle) : EventArgs
{
    public string? StreamTitle { get; } = streamTitle;
}

internal interface IRadioStreamTitleSource
{
    string? StreamTitle { get; }
    event EventHandler<RadioStreamTitleChangedEventArgs>? StreamTitleChanged;
}

/// <summary>
/// Normalizes the small, untrusted text fields carried inside ICY/Shoutcast
/// metadata. The result is suitable for a window title but is never persisted
/// as the station's own name.
/// </summary>
internal static class RadioStreamTitleMetadata
{
    private const int MaximumDisplayLength = 300;

    internal static string? ParseIcyBlock(ReadOnlySpan<byte> bytes) =>
        TryParseIcyText(Decode(bytes), out var title) ? title : null;

    internal static string? ParseIcyText(string? metadata)
        => TryParseIcyText(metadata, out var title) ? title : null;

    internal static bool TryParseIcyBlock(ReadOnlySpan<byte> bytes, out string? title) =>
        TryParseIcyText(Decode(bytes), out title);

    internal static bool TryParseIcyText(string? metadata, out string? title)
    {
        title = null;
        if (string.IsNullOrWhiteSpace(metadata)) return false;
        const string key = "StreamTitle=";
        var start = metadata.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return false;
        start += key.Length;
        if (start >= metadata.Length) return true;

        var quote = metadata[start] is '\'' or '"' ? metadata[start++] : '\0';
        var end = quote == '\0'
            ? metadata.IndexOf(';', start)
            : metadata.IndexOf(quote, start);
        if (end < 0) end = metadata.Length;
        title = Normalize(metadata[start..end]);
        return true;
    }

    internal static string? ParseOggTags(IEnumerable<string> tags)
    {
        string? artist = null;
        string? title = null;
        foreach (var tag in tags)
        {
            var separator = tag.IndexOf('=');
            if (separator <= 0) continue;
            var key = tag[..separator].Trim();
            var value = Normalize(tag[(separator + 1)..]);
            if (value is null) continue;
            if (key.Equals("ARTIST", StringComparison.OrdinalIgnoreCase)) artist = value;
            else if (key.Equals("TITLE", StringComparison.OrdinalIgnoreCase)) title = value;
        }

        return artist is not null && title is not null
            ? $"{artist} — {title}"
            : title ?? artist;
    }

    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var builder = new StringBuilder(Math.Min(value.Length, MaximumDisplayLength));
        var previousWasSpace = false;
        foreach (var character in value)
        {
            if (builder.Length >= MaximumDisplayLength) break;
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
                continue;
            }
            builder.Append(character);
            previousWasSpace = false;
        }
        return builder.ToString().Trim() is { Length: > 0 } normalized ? normalized : null;
    }

    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        var terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0) bytes = bytes[..terminator];
        if (bytes.IsEmpty) return string.Empty;
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Older ICY servers often use an unspecified single-byte Western
            // encoding. Latin-1 is lossless and avoids leaking replacement
            // glyphs into the accessible window title.
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
