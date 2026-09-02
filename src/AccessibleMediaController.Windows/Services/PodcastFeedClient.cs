using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows.Services;

internal sealed class PodcastFeedClient : IDisposable
{
    internal const int MaximumRedirects = 5;
    internal const int MaximumResponseBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    public PodcastFeedClient()
        : this(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            UseCookies = false
        })
    {
    }

    internal PodcastFeedClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        _http = new HttpClient(handler, disposeHandler: true);
        _timeout = timeout ?? DefaultTimeout;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
    }

    public async Task<PodcastFeedDocument> FetchAsync(Uri address, CancellationToken cancellationToken)
    {
        ValidateAddress(address);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var current = address;
        for (var redirect = 0; ; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml", 0.9));
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect >= MaximumRedirects)
                    throw new InvalidDataException("Kanał przekierowuje zbyt wiele razy.");
                var location = response.Headers.Location
                    ?? throw new InvalidDataException("Przekierowanie kanału nie zawiera adresu docelowego.");
                current = location.IsAbsoluteUri ? location : new Uri(current, location.OriginalString);
                ValidateAddress(current);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Serwer kanału zwrócił kod {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }
            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
                throw new InvalidDataException("Kanał podcastu jest zbyt duży.");

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            await using var limited = new MemoryStream();
            var buffer = new byte[16 * 1024];
            var total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                total += read;
                if (total > MaximumResponseBytes)
                    throw new InvalidDataException("Kanał podcastu jest zbyt duży.");
                await limited.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
            }
            limited.Position = 0;
            return PodcastFeedParser.Parse(limited, current);
        }
    }

    private static void ValidateAddress(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || address.Scheme is not ("http" or "https"))
            throw new ArgumentException("Adres kanału musi rozpoczynać się od http:// albo https://.", nameof(address));
        if (!string.IsNullOrEmpty(address.UserInfo))
            throw new ArgumentException("Adres kanału nie może zawierać nazwy użytkownika ani hasła.", nameof(address));
    }

    private static bool IsRedirect(HttpStatusCode status) => status is
        HttpStatusCode.MovedPermanently
        or HttpStatusCode.Found
        or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    public void Dispose() => _http.Dispose();
}
