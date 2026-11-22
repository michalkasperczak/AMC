using System.Collections.Concurrent;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.YouTube;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>Publiczny odczyt transmisji przez yt-dlp, bez OAuth i ciasteczek.</summary>
internal sealed class LiteYouTubePublicBroadcastBackend : ILiteYouTubePublicBroadcastBackend
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);
    private readonly PodcastLibraryMutationStore _podcasts;
    private readonly YouTubePublicBroadcastClient _client = new();
    private readonly ConcurrentDictionary<string, Snapshot> _cache = new(StringComparer.Ordinal);

    internal LiteYouTubePublicBroadcastBackend(string podcastsDatabasePath)
    {
        _podcasts = new PodcastLibraryMutationStore(Path.GetFullPath(podcastsDatabasePath));
    }

    public bool IsAvailable => YouTubeSourceResolver.FindExecutable() is not null;
    public string AvailabilityMessage => IsAvailable
        ? "Publiczne transmisje YouTube są dostępne bez konta Google."
        : "Publiczne transmisje YouTube wymagają składnika yt-dlp. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.";

    public async Task<IReadOnlyList<YouTubeBroadcast>> ReadBroadcastsAsync(
        string subscriptionId, bool forceRefresh, CancellationToken cancellationToken)
    {
        subscriptionId = subscriptionId?.Trim() ?? string.Empty;
        if (!forceRefresh && _cache.TryGetValue(subscriptionId, out var cached)
            && DateTimeOffset.UtcNow - cached.CreatedAtUtc <= CacheLifetime)
            return cached.Rows;
        var target = _podcasts.GetRefreshTargets(subscriptionId).Single();
        if (target.SourceKind != PodcastSourceKind.YouTubeChannel)
            throw new InvalidOperationException("To źródło nie jest kanałem YouTube.");
        if (!Uri.TryCreate(target.FeedUrl, UriKind.Absolute, out var address)
            || !YouTubeCollectionClient.TryNormalizeCollectionAddress(
                address, out var normalized, out var kind)
            || kind != PodcastSourceKind.YouTubeChannel
            || !YouTubeCollectionClient.TryBuildChannelStreamsAddress(normalized, kind, out var streams))
        {
            throw new InvalidDataException("Ten kanał YouTube nie ma publicznego adresu potrzebnego do odczytu transmisji.");
        }
        var rows = await _client.FetchAsync(streams, cancellationToken).ConfigureAwait(false);
        _cache[subscriptionId] = new Snapshot(rows, DateTimeOffset.UtcNow);
        return rows;
    }

    public async Task<LiteYouTubeRadioBroadcastResult> ReadRadioBroadcastsAsync(
        string sourceUrl, string? subscriptionId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(subscriptionId))
            return new LiteYouTubeRadioBroadcastResult(
                await ReadBroadcastsAsync(subscriptionId, true, cancellationToken).ConfigureAwait(false), null);
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source)
            || !YouTubeSourceResolver.IsYouTubeUrl(sourceUrl))
            throw new LiteRequestException("Ta stacja nie ma adresu YouTube.");
        var segments = source.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var liveUrl = segments.Length >= 2
                      && segments[0].Equals("channel", StringComparison.OrdinalIgnoreCase)
            ? YouTubeSourceResolver.TryBuildChannelLiveUrl(
                Uri.UnescapeDataString(segments[1]))
            : null;
        if (liveUrl is null)
        {
            var executable = YouTubeSourceResolver.FindExecutable()
                ?? throw new LiteRequestException(AvailabilityMessage);
            liveUrl = await YouTubeSourceResolver.TryResolveChannelLiveUrlAsync(
                executable, source.AbsoluteUri, cancellationToken).ConfigureAwait(false);
        }
        if (liveUrl is null || !Uri.TryCreate(liveUrl, UriKind.Absolute, out var liveAddress))
            throw new LiteRequestException("Nie udało się ustalić kanału YouTube tej stacji.");
        var path = liveAddress.AbsolutePath;
        if (!path.EndsWith("/live", StringComparison.OrdinalIgnoreCase))
            throw new LiteRequestException("Nie udało się ustalić kanału YouTube tej stacji.");
        var streams = new UriBuilder(liveAddress) { Path = path[..^"/live".Length] + "/streams" }.Uri;
        var rows = await _client.FetchAsync(streams, cancellationToken).ConfigureAwait(false);
        return new LiteYouTubeRadioBroadcastResult(rows, liveUrl);
    }

    public void Dispose() => _cache.Clear();
    private sealed record Snapshot(IReadOnlyList<YouTubeBroadcast> Rows, DateTimeOffset CreatedAtUtc);
}
