using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace AccessibleMediaController.Windows.Services;

internal static partial class RadioStreamResolver
{
    private const int MaximumPlaylistBytes = 1024 * 1024;
    private static readonly HttpClient Client = CreateClient();

    public static IReadOnlyList<string> GetPlaybackCandidates(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return [source];
        var compatibility = TryGetCompatibilityMp3(uri);
        if (compatibility is null) return [source];

        // A genuine HLS address is the authoritative source.  The legacy MP3
        // endpoints used by some Polish Radio entries are useful fallbacks,
        // but they are intermittent and must not replace a working manifest.
        return IsHlsSource(source)
            ? [source, compatibility]
            : [compatibility, source];
    }

    public static bool IsHlsSource(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && Path.GetExtension(uri.AbsolutePath).Equals(".m3u8", StringComparison.OrdinalIgnoreCase);

    public static async Task<string> ResolveAsync(string source, CancellationToken cancellationToken)
    {
        var uri = new Uri(source, UriKind.Absolute);
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (!extension.Equals(".m3u", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".pls", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".xspf", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        using var response = await Client.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumPlaylistBytes)
        {
            throw new InvalidDataException("Lista stacji jest zbyt duża.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var limited = new StreamReader(stream);
        var characters = new char[MaximumPlaylistBytes];
        var count = await limited.ReadBlockAsync(characters.AsMemory(), cancellationToken).ConfigureAwait(false);
        var text = new string(characters, 0, count);
        if (text.Contains("#EXT-X-", StringComparison.OrdinalIgnoreCase))
        {
            // This is an HLS manifest, not a station list. Media Foundation
            // must receive the manifest itself so it can follow its segments.
            return source;
        }

        var candidate = extension.Equals(".pls", StringComparison.OrdinalIgnoreCase)
            ? PlsEntry().Match(text).Groups[1].Value.Trim()
            : extension.Equals(".xspf", StringComparison.OrdinalIgnoreCase)
                ? XspfEntry().Match(text).Groups[1].Value.Trim()
                : text.Split('\n')
                    .Select(line => line.Trim().TrimStart('\uFEFF'))
                    .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'))
                    ?? string.Empty;
        if (candidate.Length == 0)
        {
            throw new InvalidDataException("Lista nie zawiera adresu strumienia.");
        }
        if (!Uri.TryCreate(uri, System.Net.WebUtility.HtmlDecode(candidate), out var resolved)
            || resolved.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException("Lista zawiera nieprawidłowy adres strumienia.");
        }
        return resolved.AbsoluteUri;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
        return client;
    }

    private static string? TryGetCompatibilityMp3(Uri uri)
    {
        if (uri.Host.Equals("stream3.polskieradio.pl", StringComparison.OrdinalIgnoreCase))
        {
            var mp3Port = uri.Port switch
            {
                8950 => 8900,
                8952 => 8902,
                8954 => 8904,
                8956 => 8906,
                8960 => 8910,
                _ => 0
            };
            if (mp3Port > 0)
            {
                return mp3Port is 8900 or 8904 or 8910
                    ? $"http://mp3.polskieradio.pl:{mp3Port}/;.mp3"
                    : $"http://stream3.polskieradio.pl:{mp3Port}/;.mp3";
            }
        }

        if (uri.Host.EndsWith(".polskieradio.pl", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.EndsWith("/playlist.m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Host.ToLowerInvariant() switch
            {
                "stream11.polskieradio.pl" => "http://mp3.polskieradio.pl:8900/;.mp3",
                "stream12.polskieradio.pl" => "http://stream3.polskieradio.pl:8902/;.mp3",
                "stream13.polskieradio.pl" => "http://mp3.polskieradio.pl:8904/;.mp3",
                "stream14.polskieradio.pl" => "http://stream3.polskieradio.pl:8906/;.mp3",
                "stream15.polskieradio.pl" => "http://stream3.polskieradio.pl:8080/;.mp3",
                _ => null
            };
        }

        if (uri.Host.Equals("radio.stream.smcdn.pl", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Equals(
                "/icradio-p/2180-1.aac/playlist.m3u8",
                StringComparison.OrdinalIgnoreCase))
        {
            return "http://ic2.smcdn.pl/2180-1.mp3";
        }

        return null;
    }

    [GeneratedRegex("(?im)^File\\d+\\s*=\\s*(.+?)\\s*$")]
    private static partial Regex PlsEntry();

    [GeneratedRegex("(?is)<location>\\s*(.+?)\\s*</location>")]
    private static partial Regex XspfEntry();
}
