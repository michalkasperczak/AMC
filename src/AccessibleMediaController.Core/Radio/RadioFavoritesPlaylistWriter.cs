using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Core.Radio;

public sealed record RadioFavoritePlaylistEntry(string Name, string StreamUrl);

/// <summary>
/// Writes a portable extended M3U containing only a user-facing station name
/// and its stable public address. Temporary resolved playback URLs and AMC
/// identifiers never belong in an exported playlist.
/// </summary>
public static class RadioFavoritesPlaylistWriter
{
    private const int MaximumEntries = 5_000;
    private const int MaximumNameLength = 200;
    private const int MaximumUrlLength = 4_096;

    public static byte[] Write(IEnumerable<RadioFavoritePlaylistEntry> stations)
    {
        ArgumentNullException.ThrowIfNull(stations);
        var builder = new StringBuilder("#EXTM3U\r\n");
        var count = 0;
        var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var station in stations)
        {
            if (count >= MaximumEntries) break;
            if (!TryNormalizeAddress(station.StreamUrl, out var address)
                || !seenAddresses.Add(address))
            {
                continue;
            }

            builder.Append("#EXTINF:-1,")
                .Append(NormalizeName(station.Name, address))
                .Append("\r\n")
                .Append(address)
                .Append("\r\n");
            count++;
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(builder.ToString());
    }

    /// <summary>
    /// Writes the VRadio Favorites JSON shape that <c>RadioPlaylistImporter</c>
    /// already reads, so a list exported from AMC can be imported back by AMC
    /// and by VRadio itself. Only the user-facing station name and its stable
    /// public address are written - never AMC identifiers or resolved playback
    /// URLs, which are temporary and may carry private tokens.
    /// </summary>
    public static byte[] WriteVRadioJson(IEnumerable<RadioFavoritePlaylistEntry> stations)
    {
        ArgumentNullException.ThrowIfNull(stations);
        var accepted = new List<RadioFavoritePlaylistEntry>();
        var seenAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var station in stations)
        {
            if (accepted.Count >= MaximumEntries) break;
            if (!TryNormalizeAddress(station.StreamUrl, out var address)
                || !seenAddresses.Add(address))
            {
                continue;
            }

            accepted.Add(new RadioFavoritePlaylistEntry(NormalizeName(station.Name, address), address));
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("stations");
            foreach (var station in accepted)
            {
                writer.WriteStartObject();
                writer.WriteString("name", station.Name);
                writer.WriteStartArray("streams");
                writer.WriteStartObject();
                writer.WriteString("url", station.StreamUrl);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static bool TryNormalizeAddress(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumUrlLength) return false;
        var trimmed = value.Trim();
        if (trimmed.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))) return false;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return false;
        }

        normalized = new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri;
        return true;
    }

    private static string NormalizeName(string? value, string address)
    {
        var builder = new StringBuilder();
        var previousWasSpace = false;
        foreach (var character in value ?? string.Empty)
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasSpace) builder.Append(' ');
                previousWasSpace = true;
                continue;
            }
            if (character == '\uFFFC' || char.IsControl(character)) continue;

            builder.Append(character);
            previousWasSpace = false;
            if (builder.Length >= MaximumNameLength) break;
        }

        var name = builder.ToString().Trim();
        if (name.Length > 0) return name;
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? uri.Host
            : "Stacja radiowa";
    }
}
