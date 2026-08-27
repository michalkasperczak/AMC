using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

public sealed class RadioBrowserClient : IDisposable
{
    private const string ApiRoot = "https://all.api.radio-browser.info";
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public RadioBrowserClient(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
        }
    }

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalized = query.Trim();
        if (normalized.Length == 0) return [];

        var searches = new[]
        {
            BuildSearchUri("name", normalized),
            BuildSearchUri("tag", normalized),
            BuildSearchUri("country", normalized)
        };
        var responses = await Task.WhenAll(searches.Select(uri =>
            ReadStationsAsync(uri, cancellationToken))).ConfigureAwait(false);

        return responses
            .SelectMany(stations => stations)
            .Where(station => station.LastCheckOk != 0)
            .Where(station => Uri.TryCreate(
                FirstNonEmpty(station.UrlResolved, station.Url),
                UriKind.Absolute,
                out var streamUri)
                && streamUri.Scheme is "http" or "https")
            .GroupBy(station => FirstNonEmpty(station.UrlResolved, station.Url, station.StationUuid), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(station => station.Votes)
            .ThenBy(station => station.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(100)
            .Select(ToMediaItem)
            .ToArray();
    }

    private async Task<IReadOnlyList<RadioBrowserStation>> ReadStationsAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetFromJsonAsync<RadioBrowserStation[]>(uri, cancellationToken)
                .ConfigureAwait(false) ?? [];
        }
        catch (HttpRequestException exception)
        {
            DiagnosticLog.Warning("radio-catalog", $"Szyfrowany katalog {uri.Host} nie odpowiedział: {exception.Message}");
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return [];
            var fallback = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttp, Port = -1 }.Uri;
            try
            {
                return await _client.GetFromJsonAsync<RadioBrowserStation[]>(fallback, cancellationToken)
                    .ConfigureAwait(false) ?? [];
            }
            catch (HttpRequestException fallbackException)
            {
                DiagnosticLog.Warning("radio-catalog", $"Katalog {fallback.Host} nie odpowiedział: {fallbackException.Message}");
                return [];
            }
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Warning("radio-catalog", $"Przekroczono czas odpowiedzi katalogu {uri.Host}.");
            return [];
        }
    }

    private static Uri BuildSearchUri(string field, string value)
    {
        var query = $"{field}={Uri.EscapeDataString(value)}&hidebroken=true&order=votes&reverse=true&limit=50";
        return new Uri($"{ApiRoot}/json/stations/search?{query}");
    }

    private static MediaItem ToMediaItem(RadioBrowserStation station)
    {
        var streamUrl = FirstNonEmpty(station.UrlResolved, station.Url);
        var directoryId = FirstNonEmpty(station.StationUuid, streamUrl);
        return new MediaItem
        {
            Id = $"radio:{directoryId}",
            Title = string.IsNullOrWhiteSpace(station.Name) ? "Stacja bez nazwy" : station.Name.Trim(),
            Kind = MediaItemKind.Station,
            Source = streamUrl,
            PublicUri = streamUrl,
            HomepageUri = EmptyToNull(station.Homepage),
            Country = EmptyToNull(station.Country),
            Language = EmptyToNull(station.Language),
            Tags = EmptyToNull(station.Tags),
            Codec = EmptyToNull(station.Codec),
            ExternalId = directoryId,
            BitrateKbps = station.Bitrate > 0 ? station.Bitrate : null,
            IsAvailable = true
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private sealed class RadioBrowserStation
    {
        [JsonPropertyName("stationuuid")]
        public string? StationUuid { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("url_resolved")]
        public string? UrlResolved { get; set; }

        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("codec")]
        public string? Codec { get; set; }

        [JsonPropertyName("bitrate")]
        public int Bitrate { get; set; }

        [JsonPropertyName("votes")]
        public int Votes { get; set; }

        [JsonPropertyName("lastcheckok")]
        public int LastCheckOk { get; set; }
    }
}
