using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AccessibleMediaController.Windows.Services;

internal sealed record PodcastDownloadProgress(long BytesReceived, long? TotalBytes);
internal sealed record PodcastDownloadResult(string Path, long BytesWritten);

/// <summary>
/// Streams one podcast enclosure into AMC's local staging area and publishes
/// the completed file atomically. The destination may be managed by a cloud
/// provider; an incomplete network response is never exposed under the final name.
/// </summary>
internal sealed class PodcastEpisodeDownloader : IDisposable
{
    internal const int MaximumRedirects = 5;
    internal const long MaximumResponseBytes = 16L * 1024 * 1024 * 1024;
    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(90);
    private readonly HttpClient _http;

    public PodcastEpisodeDownloader()
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

    internal PodcastEpisodeDownloader(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
    }

    public async Task<PodcastDownloadResult> DownloadAsync(
        Uri source,
        string destinationPath,
        IProgress<PodcastDownloadProgress>? progress,
        CancellationToken cancellationToken,
        bool overwrite = false)
    {
        ValidateAddress(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        if (!overwrite && File.Exists(fullDestinationPath))
            throw new IOException("Plik o tej nazwie już istnieje.");

        var stagingPath = RadioRecordingStagingStore.CreatePath(fullDestinationPath);
        var completedDownload = false;
        try
        {
            using var response = await SendWithRedirectsAsync(source, cancellationToken).ConfigureAwait(false);
            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength is > MaximumResponseBytes)
                throw new InvalidDataException("Odcinek jest zbyt duży, aby bezpiecznie go pobrać.");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                var buffer = new byte[128 * 1024];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)
                        .AsTask()
                        .WaitAsync(ReadTimeout, cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                    if (total > MaximumResponseBytes)
                        throw new InvalidDataException("Odcinek jest zbyt duży, aby bezpiecznie go pobrać.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report(new PodcastDownloadProgress(total, declaredLength));
                }
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }

            var bytesWritten = new FileInfo(stagingPath).Length;
            if (bytesWritten == 0) throw new InvalidDataException("Serwer nie zwrócił danych odcinka.");
            if (declaredLength is long expected && expected > 0 && bytesWritten != expected)
                throw new InvalidDataException("Pobieranie zakończyło się przed odebraniem całego odcinka.");

            completedDownload = true;
            RadioRecordingStagingStore.Publish(stagingPath, fullDestinationPath, overwrite);
            return new PodcastDownloadResult(fullDestinationPath, bytesWritten);
        }
        catch
        {
            // Network failures and cancellations cannot produce a usable file.
            // Once all bytes have arrived, retain staging if cloud publication
            // fails because it can be the only complete, recoverable copy.
            if (!completedDownload) RadioRecordingStagingStore.TryDelete(stagingPath);
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendWithRedirectsAsync(
        Uri source,
        CancellationToken cancellationToken)
    {
        var current = source;
        for (var redirect = 0; ; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/*"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream", 0.8));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HeaderTimeout);
            var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!IsRedirect(response.StatusCode))
            {
                if (response.IsSuccessStatusCode) return response;
                var statusCode = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException(
                    $"Serwer odcinka zwrócił kod {(int)statusCode}.",
                    null,
                    statusCode);
            }

            if (redirect >= MaximumRedirects)
            {
                response.Dispose();
                throw new InvalidDataException("Adres odcinka przekierowuje zbyt wiele razy.");
            }
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null)
                throw new InvalidDataException("Przekierowanie nie zawiera adresu docelowego.");
            current = location.IsAbsoluteUri ? location : new Uri(current, location.OriginalString);
            ValidateAddress(current);
        }
    }

    private static void ValidateAddress(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || address.Scheme is not ("http" or "https"))
            throw new ArgumentException("Adres audio musi rozpoczynać się od http:// albo https://.", nameof(address));
        if (!string.IsNullOrEmpty(address.UserInfo))
            throw new ArgumentException("Adres audio nie może zawierać nazwy użytkownika ani hasła.", nameof(address));
    }

    private static bool IsRedirect(HttpStatusCode status) => status is
        HttpStatusCode.MovedPermanently
        or HttpStatusCode.Found
        or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect
        or HttpStatusCode.PermanentRedirect;

    public void Dispose() => _http.Dispose();
}

internal static class PodcastDownloadFolderResolver
{
    public static string DefaultFolder()
    {
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return Path.Combine(music, "AMC — Pobrane podcasty");
    }

    public static string Resolve(string? configuredFolder)
    {
        var folder = string.IsNullOrWhiteSpace(configuredFolder)
            ? DefaultFolder()
            : Path.GetFullPath(configuredFolder);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string ResolveDialogInitialFolder(string? configuredFolder)
    {
        var candidates = new[]
        {
            configuredFolder,
            DefaultFolder(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var fullPath = Path.GetFullPath(candidate);
                if (Directory.Exists(fullPath)) return fullPath;
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
            }
        }
        return string.Empty;
    }
}
