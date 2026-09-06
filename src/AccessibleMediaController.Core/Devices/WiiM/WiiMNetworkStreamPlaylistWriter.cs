using System.Text;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Devices.WiiM;

public static class WiiMNetworkStreamPlaylistWriter
{
    private const int MaximumEntries = 5_000;
    private const int MaximumNameLength = 200;

    public static byte[] Write(IEnumerable<WiiMNetworkStreamSettings> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);
        var builder = new StringBuilder("#EXTM3U\r\n");
        var normalizedStreams = new List<(string Name, string Url)>();
        foreach (var stream in streams)
        {
            if (normalizedStreams.Count >= MaximumEntries) break;
            if (!WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out var normalizedUrl)) continue;
            normalizedStreams.Add((NormalizeName(stream.Name, normalizedUrl), normalizedUrl));
        }

        // Preserve the exact user-visible order supplied by AMC. Real-device
        // testing showed that importing an M3U in WiiM Home keeps the file order;
        // reversing it here therefore reversed the list a second time.
        for (var index = 0; index < normalizedStreams.Count; index++)
        {
            var stream = normalizedStreams[index];
            builder.Append("#EXTINF:-1,")
                .Append(stream.Name)
                .Append("\r\n")
                .Append(stream.Url)
                .Append("\r\n");
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(builder.ToString());
    }

    private static string NormalizeName(string? value, string streamUrl)
    {
        var builder = new StringBuilder();
        var previousWasSpace = false;
        foreach (var character in value ?? string.Empty)
        {
            if (character == '\uFFFC') continue;
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasSpace) builder.Append(' ');
                previousWasSpace = true;
                continue;
            }
            if (char.IsControl(character)) continue;

            builder.Append(character);
            previousWasSpace = false;
            if (builder.Length >= MaximumNameLength) break;
        }

        var name = builder.ToString().Trim();
        if (name.Length > 0) return name;
        return Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "Strumień WiiM";
    }
}
