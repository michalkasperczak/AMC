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
        var written = 0;
        foreach (var stream in streams)
        {
            if (written >= MaximumEntries) break;
            if (!WiiMPlaybackUriPolicy.TryNormalize(stream.StreamUrl, out var normalizedUrl)) continue;

            builder.Append("#EXTINF:-1,")
                .Append(NormalizeName(stream.Name, normalizedUrl))
                .Append("\r\n")
                .Append(normalizedUrl)
                .Append("\r\n");
            written++;
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
