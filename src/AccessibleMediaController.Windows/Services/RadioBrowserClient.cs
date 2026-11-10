using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

public sealed record RadioBrowserCountry(string Code, string Name, int StationCount);
public sealed record RadioBrowserStationPage(IReadOnlyList<MediaItem> Items, bool HasMore);

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

    public async Task<IReadOnlyList<RadioBrowserCountry>> BrowseCountriesAsync(
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri($"{ApiRoot}/json/countrycodes?hidebroken=true");
        var response = await _client.GetFromJsonAsync<CountryCodeRecord[]>(uri, cancellationToken)
            .ConfigureAwait(false) ?? [];
        return response
            .Where(item => item.Name is { Length: 2 }
                && item.Name.All(char.IsLetter)
                && item.StationCount > 0)
            .Select(item => new RadioBrowserCountry(
                item.Name!.ToUpperInvariant(),
                CountryDisplayName(item.Name!),
                item.StationCount))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<RadioBrowserStationPage> BrowseStationsAsync(
        string countryCode,
        string order,
        int offset,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var code = countryCode.Trim().ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsLetter))
            throw new ArgumentException("Nieprawidłowy kraj katalogu", nameof(countryCode));
        if (order is not ("name" or "votes" or "changetimestamp"))
            throw new ArgumentException("Nieprawidłowy porządek stacji", nameof(order));
        if (offset < 0 || limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var reverse = order == "name" ? "false" : "true";
        var uri = new Uri($"{ApiRoot}/json/stations/search?countrycode={code}"
            + $"&hidebroken=true&order={order}&reverse={reverse}"
            + $"&offset={offset}&limit={limit}");
        var found = await ReadStationsAsync(uri, cancellationToken, requireSuccess: true)
            .ConfigureAwait(false);
        var items = found
            .Where(station => station.LastCheckOk != 0)
            .Where(station => Uri.TryCreate(
                FirstNonEmpty(station.UrlResolved, station.Url),
                UriKind.Absolute,
                out var streamUri)
                && streamUri.Scheme is "http" or "https")
            .Select(ToMediaItem)
            .ToArray();
        return new RadioBrowserStationPage(items, found.Count == limit);
    }

    private static string CountryDisplayName(string code)
    {
        if (code.Equals("XK", StringComparison.OrdinalIgnoreCase)) return "Kosowo";
        try { return new RegionInfo(code).DisplayName; }
        catch (ArgumentException) { return "Kraj bez nazwy w systemie"; }
    }

    private async Task<IReadOnlyList<RadioBrowserStation>> ReadStationsAsync(
        Uri uri,
        CancellationToken cancellationToken,
        bool requireSuccess = false)
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
                if (requireSuccess) throw;
                return [];
            }
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DiagnosticLog.Warning("radio-catalog", $"Przekroczono czas odpowiedzi katalogu {uri.Host}.");
            if (requireSuccess) throw;
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

    private sealed class CountryCodeRecord
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("stationcount")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int StationCount { get; set; }
    }
}
