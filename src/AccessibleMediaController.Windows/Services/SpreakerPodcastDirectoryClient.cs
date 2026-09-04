using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Searches Spreaker's public show directory. Public GET endpoints do not
/// require a user account. A result points at the show's documented RSS feed,
/// which is verified by PodcastFeedClient before it can enter the Library.
/// </summary>
internal sealed class SpreakerPodcastDirectoryClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, (DateTime StoredUtc, IReadOnlyList<MediaItem> Items)> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    public SpreakerPodcastDirectoryClient()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _ownsClient = true;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
    }

    internal SpreakerPodcastDirectoryClient(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        _client = new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(15)
        };
        _ownsClient = true;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMultimediaController/0.1");
    }

    public async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.Join(' ', query
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0) return [];
        lock (_cacheGate)
        {
            if (_cache.TryGetValue(normalized, out var cached)
                && DateTime.UtcNow - cached.StoredUtc <= CacheLifetime)
            {
                return cached.Items;
            }
        }

        var address = new Uri(
            "https://api.spreaker.com/v2/search"+
            $"?type=shows&q={Uri.EscapeDataString(normalized)}&limit=50");
        var response = await _client.GetFromJsonAsync<SpreakerEnvelope>(address, cancellationToken)
            .ConfigureAwait(false);
        var items = (response?.Response?.Items ?? [])
            .Where(result => result.ShowId > 0)
            .GroupBy(result => result.ShowId)
            .Select(group => group.First())
            .Take(50)
            .Select(ToMediaItem)
            .ToArray();
        lock (_cacheGate)
        {
            foreach (var staleKey in _cache
                         .Where(entry => DateTime.UtcNow - entry.Value.StoredUtc > CacheLifetime)
                         .Select(entry => entry.Key)
                         .ToArray())
            {
                _cache.Remove(staleKey);
            }
            _cache[normalized] = (DateTime.UtcNow, items);
        }
        return items;
    }

    private static MediaItem ToMediaItem(SpreakerShow result)
    {
        var id = result.ShowId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var feedUrl = $"https://www.spreaker.com/show/{id}/episodes/feed";
        return new MediaItem
        {
            Id = $"podcast-directory:spreaker:{id}",
            Title = string.IsNullOrWhiteSpace(result.Title)
                ? "Podcast bez nazwy"
                : result.Title.Trim(),
            Kind = MediaItemKind.Podcast,
            Source = feedUrl,
            PublicUri = TryPublicHttpUri(result.SiteUrl, out var pageUri)
                ? pageUri!.AbsoluteUri
                : $"https://www.spreaker.com/show/{id}",
            ExternalId = $"spreaker:{id}",
            IsAvailable = true,
            IsInLibrary = false
        };
    }

    private static bool TryPublicHttpUri(string? value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }
        uri = parsed;
        return true;
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private sealed class SpreakerEnvelope
    {
        [JsonPropertyName("response")]
        public SpreakerResponse? Response { get; set; }
    }

    private sealed class SpreakerResponse
    {
        [JsonPropertyName("items")]
        public SpreakerShow[]? Items { get; set; }
    }

    private sealed class SpreakerShow
    {
        [JsonPropertyName("show_id")]
        public long ShowId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("site_url")]
        public string? SiteUrl { get; set; }
    }
}
