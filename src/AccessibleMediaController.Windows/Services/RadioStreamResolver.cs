using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace AccessibleMediaController.Windows.Services;

internal static partial class RadioStreamResolver
{
    private const int MaximumPlaylistBytes = 1024 * 1024;
    private static readonly HttpClient Client = CreateClient();

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

    [GeneratedRegex("(?im)^File\\d+\\s*=\\s*(.+?)\\s*$")]
    private static partial Regex PlsEntry();

    [GeneratedRegex("(?is)<location>\\s*(.+?)\\s*</location>")]
    private static partial Regex XspfEntry();
}
