using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Sprawdza i dodaje RSS, kolekcje YouTube albo pojedyncze publiczne medium
/// tym samym torem co główne AMC. Do bazy trafia wyłącznie stabilny adres
/// strony; podpisany adres strumienia z yt-dlp nigdy nie jest zapisywany.
/// </summary>
internal sealed class LitePodcastAddCoordinator : IDisposable
{
    public const string Operation = "podcast.add";

    private readonly LitePodcastProgressStore _podcasts;
    private readonly LiteBookmarkStore? _bookmarks;
    private readonly PodcastFeedClient _rssClient = new();
    private readonly YouTubeCollectionClient _youTubeClient = new();
    private int _addInProgress;

    public LitePodcastAddCoordinator(
        LitePodcastProgressStore podcasts,
        LiteBookmarkStore? bookmarks)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
        _bookmarks = bookmarks;
    }

    public object Add(JsonElement args)
    {
        if (Interlocked.CompareExchange(ref _addInProgress, 1, 0) != 0)
            throw new LiteRequestException("Dodawanie źródła już trwa.");

        try
        {
            var addressText = (LiteArgs.ReadText(args, "address") ?? string.Empty).Trim();
            var customTitle = LiteArgs.ReadText(args, "title")?.Trim();
            if (!Uri.TryCreate(addressText, UriKind.Absolute, out var address)
                || address.Scheme is not ("http" or "https")
                || !string.IsNullOrEmpty(address.UserInfo))
            {
                throw new LiteRequestException(
                    "Wpisz pełny adres HTTP lub HTTPS bez nazwy użytkownika i hasła.");
            }

            if (YouTubeSourceResolver.IsYouTubeUrl(addressText))
            {
                if (YouTubeCollectionClient.TryNormalizeCollectionAddress(
                        address,
                        out var collectionAddress,
                        out _))
                {
                    var collection = _youTubeClient.FetchAsync(
                            collectionAddress,
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    return SourceResult(AddFeed(
                        collection.Feed,
                        customTitle,
                        collection.SourceKind));
                }

                var resolved = YouTubeSourceResolver.ResolveAudioAsync(
                        addressText,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                var result = _podcasts.AddInternetMedia(
                    new PodcastInternetMediaSource(
                        resolved.PageUrl,
                        resolved.Title,
                        resolved.Channel,
                        resolved.Duration,
                        resolved.IsLive),
                    customTitle);
                return new
                {
                    subscriptionId = result.SubscriptionId,
                    preferredEpisodeId = result.EpisodeId,
                    title = result.Title,
                    sourceKind = "internetMedia",
                    sourceLabel = resolved.IsLive
                        ? "transmisja YouTube"
                        : "materiał YouTube",
                    added = result.AddedEpisode,
                    restored = false,
                    itemCount = 1
                };
            }

            var feed = _rssClient.FetchAsync(address, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            if (feed.Episodes.Count == 0)
            {
                throw new LiteRequestException(
                    $"Kanał {feed.Title} nie zawiera odtwarzalnych odcinków audio ani wideo.");
            }
            return SourceResult(AddFeed(feed, customTitle, PodcastSourceKind.Rss));
        }
        catch (LiteRequestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or InvalidDataException
            or System.Xml.XmlException
            or ArgumentException
            or OperationCanceledException
            or NotSupportedException
            or TimeoutException)
        {
            throw new LiteRequestException("Nie można dodać źródła: " + exception.Message);
        }
        finally
        {
            Volatile.Write(ref _addInProgress, 0);
        }
    }

    private PodcastSourceAddResult AddFeed(
        PodcastFeedDocument feed,
        string? customTitle,
        PodcastSourceKind sourceKind)
    {
        if (_bookmarks is not null
            && feed.Episodes.Any(episode => episode.Chapters is { Count: > 0 }))
        {
            return _bookmarks.MutateBookmarks(bookmarks =>
                _podcasts.AddSource(feed, customTitle, sourceKind, bookmarks));
        }
        return _podcasts.AddSource(feed, customTitle, sourceKind);
    }

    private static object SourceResult(PodcastSourceAddResult result) => new
    {
        subscriptionId = result.SubscriptionId,
        preferredEpisodeId = (string?)null,
        title = result.Title,
        sourceKind = result.SourceKind switch
        {
            PodcastSourceKind.YouTubeChannel => "youtubeChannel",
            PodcastSourceKind.YouTubePlaylist => "youtubePlaylist",
            _ => "rss"
        },
        sourceLabel = result.SourceKind switch
        {
            PodcastSourceKind.YouTubeChannel => "kanał YouTube",
            PodcastSourceKind.YouTubePlaylist => "playlista YouTube",
            _ => "podcast"
        },
        added = result.AddedSubscription,
        restored = result.RestoredSubscription,
        itemCount = result.AvailableEpisodes
    };

    public void Dispose() => _rssClient.Dispose();
}
