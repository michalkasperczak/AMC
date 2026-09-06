using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ResolvedRadioSource(
    string Url,
    bool IsHls,
    bool IsYouTube,
    string? Title = null,
    string? Codec = null,
    int? BitrateKbps = null);

internal static partial class RadioStreamResolver
{
    private const int MaximumPlaylistBytes = 1024 * 1024;
    private const int MaximumPlaylistAddressLength = 8192;
    private const int MaximumPlaylistDepth = 4;
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

    public static async Task<string> ResolveAsync(string source, CancellationToken cancellationToken) =>
        (await ResolveSourceAsync(source, cancellationToken).ConfigureAwait(false)).Url;

    public static async Task<ResolvedRadioSource> ResolveSourceAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (YouTubeSourceResolver.IsYouTubeUrl(source))
        {
            var youtube = await YouTubeSourceResolver.ResolveLiveAudioAsync(source, cancellationToken)
                .ConfigureAwait(false);
            return new ResolvedRadioSource(
                youtube.StreamUrl,
                youtube.IsHls,
                true,
                youtube.Title,
                youtube.Codec,
                youtube.BitrateKbps);
        }

        var resolved = await ResolvePlaylistAsync(
                source,
                0,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                cancellationToken)
            .ConfigureAwait(false);
        return new ResolvedRadioSource(resolved, IsHlsSource(resolved), false);
    }

    private static async Task<string> ResolvePlaylistAsync(
        string source,
        int depth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException("Adres strumienia jest nieprawidłowy.");
        }
        if (!IsPlaylistAddress(uri))
        {
            return source;
        }
        if (depth >= MaximumPlaylistDepth)
        {
            throw new InvalidDataException("Lista stacji zawiera zbyt wiele zagnieżdżonych list.");
        }
        if (!visited.Add(uri.AbsoluteUri))
        {
            throw new InvalidDataException("Lista stacji zawiera odwołanie do samej siebie.");
        }

        using var response = await Client.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var responseUri = response.RequestMessage?.RequestUri ?? uri;
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (IsDirectAudioMediaType(mediaType))
        {
            // Some playlist addresses redirect straight to a live stream.
            // Do not consume audio while trying to parse it as text.
            return responseUri.AbsoluteUri;
        }
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
            return responseUri.AbsoluteUri;
        }

        var candidate = ExtractFirstEntry(text, responseUri, mediaType);
        if (candidate.Length == 0)
        {
            throw new InvalidDataException("Lista nie zawiera adresu strumienia.");
        }
        candidate = System.Net.WebUtility.HtmlDecode(candidate).Trim();
        if (candidate.Length > MaximumPlaylistAddressLength
            || candidate.Any(character => char.IsControl(character))
            || candidate.Contains('<')
            || candidate.Contains('>')
            || !Uri.TryCreate(responseUri, candidate, out var resolved)
            || resolved.Scheme is not ("http" or "https"))
        {
            throw new InvalidDataException("Lista zawiera nieprawidłowy adres strumienia.");
        }
        return IsPlaylistAddress(resolved)
            ? await ResolvePlaylistAsync(resolved.AbsoluteUri, depth + 1, visited, cancellationToken)
                .ConfigureAwait(false)
            : resolved.AbsoluteUri;
    }

    private static string ExtractFirstEntry(string text, Uri responseUri, string? mediaType)
    {
        var extension = Path.GetExtension(responseUri.AbsolutePath);
        var pls = PlsEntry().Match(text);
        if (pls.Success
            || extension.Equals(".pls", StringComparison.OrdinalIgnoreCase)
            || mediaType?.Equals("audio/x-scpls", StringComparison.OrdinalIgnoreCase) == true
            || mediaType?.Equals("application/pls+xml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return pls.Success ? pls.Groups[1].Value.Trim() : string.Empty;
        }

        var xspf = XspfEntry().Match(text);
        if (xspf.Success
            || extension.Equals(".xspf", StringComparison.OrdinalIgnoreCase)
            || mediaType?.Equals("application/xspf+xml", StringComparison.OrdinalIgnoreCase) == true)
        {
            return xspf.Success ? xspf.Groups[1].Value.Trim() : string.Empty;
        }

        return text.Split('\n')
            .Select(line => line.Trim().TrimStart('\uFEFF'))
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'))
            ?? string.Empty;
    }

    private static bool IsPlaylistAddress(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);
        return extension.Equals(".m3u", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pls", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xspf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDirectAudioMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)
            || !mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return !mediaType.Equals("audio/x-scpls", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("audio/mpegurl", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("audio/x-mpegurl", StringComparison.OrdinalIgnoreCase)
            && !mediaType.Equals("audio/vnd.apple.mpegurl", StringComparison.OrdinalIgnoreCase);
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
