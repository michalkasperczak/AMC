using System.Net;
using System.Net.Http.Headers;
using AccessibleMediaController.Core.LocalMedia;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Bounded, metadata-only measurement for a remote audio file already being
/// played. Never reads the body and never logs a URL or response headers.
/// </summary>
internal static class LiteRemoteMediaBitrate
{
    internal static async Task<int?> TryReadAsync(
        string source,
        TimeSpan duration,
        TimeSpan timeout,
        HttpMessageHandler? handler = null)
    {
        if (duration <= TimeSpan.Zero || timeout <= TimeSpan.Zero
            || !Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0
            || uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
            return null;

        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, uri);
            using var headResponse = await client.SendAsync(
                head, HttpCompletionOption.ResponseHeadersRead,
                deadline.Token).ConfigureAwait(false);
            var length = headResponse.IsSuccessStatusCode
                && IsAudio(uri, headResponse.Content.Headers.ContentType)
                    ? headResponse.Content.Headers.ContentLength : null;
            if (length is > 0) return Estimate(length.Value, duration);

            using var range = new HttpRequestMessage(HttpMethod.Get, uri);
            range.Headers.Range = new RangeHeaderValue(0, 0);
            using var rangeResponse = await client.SendAsync(
                range, HttpCompletionOption.ResponseHeadersRead,
                deadline.Token).ConfigureAwait(false);
            length = rangeResponse.StatusCode == HttpStatusCode.PartialContent
                && IsAudio(uri, rangeResponse.Content.Headers.ContentType)
                    ? rangeResponse.Content.Headers.ContentRange?.Length : null;
            return length is > 0 ? Estimate(length.Value, duration) : null;
        }
        catch (Exception error) when (error is HttpRequestException or IOException
            or OperationCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    internal static int? Estimate(long totalBytes, TimeSpan duration)
    {
        if (totalBytes <= 0 || duration <= TimeSpan.Zero) return null;
        var bitrate = LocalAudioFileDiscovery.EstimateBitrateKbps(totalBytes, duration);
        return bitrate is >= 16 and <= 30_000 ? bitrate : null;
    }

    private static bool IsAudio(Uri uri, MediaTypeHeaderValue? contentType)
    {
        if (contentType?.MediaType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (contentType?.MediaType is "text/html" or "application/json") return false;
        return Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() is
            ".mp3" or ".m4a" or ".aac" or ".ogg" or ".opus" or ".flac" or ".wav" or ".wma";
    }
}
