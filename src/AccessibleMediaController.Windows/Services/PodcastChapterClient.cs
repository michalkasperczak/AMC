using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.IO;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Retrieves Podcasting 2.0 JSON chapters only after the user explicitly asks
/// to see an episode's chapters. It never follows cookies or sends a referrer.
/// </summary>
internal sealed class PodcastChapterClient : IDisposable
{
    internal const int MaximumRedirects = 5;
    internal const int MaximumResponseBytes = PodcastJsonChapterParser.MaximumJsonCharacters;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    public PodcastChapterClient()
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

    internal PodcastChapterClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        _http = new HttpClient(handler, disposeHandler: true);
        _timeout = timeout ?? DefaultTimeout;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
    }

    public async Task<IReadOnlyList<ProviderChapterPoint>> FetchAsync(
        Uri address,
        CancellationToken cancellationToken)
    {
        ValidateAddress(address);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        var current = address;
        for (var redirect = 0; ; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json+chapters"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.9));
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect >= MaximumRedirects)
                    throw new InvalidDataException("Plik rozdziałów przekierowuje zbyt wiele razy.");
                var location = response.Headers.Location
                    ?? throw new InvalidDataException("Przekierowanie rozdziałów nie zawiera adresu docelowego.");
                current = location.IsAbsoluteUri ? location : new Uri(current, location.OriginalString);
                ValidateAddress(current);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Serwer rozdziałów zwrócił kod {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }
            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
                throw new InvalidDataException("Plik rozdziałów podcastu jest zbyt duży.");

            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var reader = new StreamReader(
                source,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 16 * 1024,
                leaveOpen: false);
            var buffer = new char[16 * 1024];
            var result = new StringBuilder();
            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                if (result.Length > MaximumResponseBytes - read)
                    throw new InvalidDataException("Plik rozdziałów podcastu jest zbyt duży.");
                result.Append(buffer, 0, read);
            }
            return PodcastJsonChapterParser.Parse(result.ToString());
        }
    }

    private static void ValidateAddress(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || address.Scheme != "https")
            throw new ArgumentException("Adres rozdziałów musi używać bezpiecznego protokołu HTTPS.", nameof(address));
        if (!string.IsNullOrEmpty(address.UserInfo))
            throw new ArgumentException("Adres rozdziałów nie może zawierać nazwy użytkownika ani hasła.", nameof(address));
    }

    private static bool IsRedirect(HttpStatusCode status) => status is
        HttpStatusCode.MovedPermanently
        or HttpStatusCode.Found
        or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    public void Dispose() => _http.Dispose();
}
