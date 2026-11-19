using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

internal sealed record LitePodcastChapterPreparation(
    int ChapterCount,
    int ImportedCount);

/// <summary>
/// Uzupełnia rozdziały bieżącego podcastu dopiero po jawnym poleceniu
/// użytkownika. Sieć działa poza szeregową kolejką transportu, a zapis trafia
/// do tych samych wąskich magazynów SQLite co odświeżanie podcastów.
/// </summary>
internal sealed class LitePodcastChapterCoordinator : IDisposable
{
    public const string Operation = "chapter.prepare";

    private readonly LitePodcastProgressStore _podcasts;
    private readonly LiteBookmarkStore _bookmarks;
    private readonly PodcastChapterClient? _client;
    private readonly Func<Uri, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>>
        _fetchProvider;
    private readonly Func<Uri, TimeSpan, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>>
        _fetchPage;
    private readonly Func<Uri, TimeSpan, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>>?
        _fetchYouTube;
    private readonly SemaphoreSlim _preparationGate = new(1, 1);
    private readonly HashSet<string> _emptyPageChecks = new(StringComparer.Ordinal);

    public LitePodcastChapterCoordinator(
        LitePodcastProgressStore podcasts,
        LiteBookmarkStore bookmarks,
        Func<Uri, TimeSpan, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>>? fetchYouTube = null)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _client = new PodcastChapterClient();
        _fetchProvider = _client.FetchAsync;
        _fetchPage = _client.FetchFromEpisodePageAsync;
        _fetchYouTube = fetchYouTube;
    }

    internal LitePodcastChapterCoordinator(
        LitePodcastProgressStore podcasts,
        LiteBookmarkStore bookmarks,
        Func<Uri, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>> fetchProvider,
        Func<Uri, TimeSpan, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>> fetchPage,
        Func<Uri, TimeSpan, CancellationToken, Task<IReadOnlyList<ProviderChapterPoint>>>? fetchYouTube = null)
    {
        _podcasts = podcasts ?? throw new ArgumentNullException(nameof(podcasts));
        _bookmarks = bookmarks ?? throw new ArgumentNullException(nameof(bookmarks));
        _fetchProvider = fetchProvider ?? throw new ArgumentNullException(nameof(fetchProvider));
        _fetchPage = fetchPage ?? throw new ArgumentNullException(nameof(fetchPage));
        _fetchYouTube = fetchYouTube;
    }

    public LitePodcastChapterPreparation Prepare(MediaItem item, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(item);
        _preparationGate.Wait();
        try
        {
            var current = _bookmarks.GetChapters("podcasts", item.Id, duration);
            var target = _podcasts.GetChapterTarget(item.Id);
            if (target is null)
                return new LitePodcastChapterPreparation(current.Count, 0);

            var imported = 0;
            var providerUrl = target.ProviderChaptersUrl?.Trim();
            var providerNeedsRefresh = providerUrl is { Length: > 0 }
                && (!string.Equals(
                        providerUrl,
                        target.ProviderChaptersLoadedUrl,
                        StringComparison.OrdinalIgnoreCase)
                    || current.Count == 0);
            if (providerNeedsRefresh
                && Uri.TryCreate(providerUrl, UriKind.Absolute, out var providerAddress))
            {
                try
                {
                    var chapters = _fetchProvider(providerAddress, CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                    if (chapters.Count > 0)
                    {
                        imported += _bookmarks.ReplaceProviderChapters(
                            "podcasts",
                            "Podcasty i YouTube",
                            ItemWithDuration(item, duration),
                            "podcast-json",
                            chapters,
                            DateTime.UtcNow);
                    }
                    _podcasts.MarkProviderChaptersLoaded(item.Id, providerUrl);
                }
                catch (Exception exception) when (IsRecoverableSourceFailure(exception))
                {
                    Console.Error.WriteLine(
                        $"[lite-host] nie pobrano rozdziałów JSON odcinka {item.Id}: "
                        + exception.GetType().Name);
                }
            }

            current = _bookmarks.GetChapters("podcasts", item.Id, duration);
            if (current.Count > 0)
                return new LitePodcastChapterPreparation(current.Count, imported);

            var descriptionChapters = PodcastDescriptionChapterParser.Parse(
                target.Description,
                duration);
            if (descriptionChapters.Count > 0)
            {
                imported += _bookmarks.ReplaceProviderChapters(
                    "podcasts",
                    "Podcasty i YouTube",
                    ItemWithDuration(item, duration),
                    "podcast-feed",
                    descriptionChapters,
                    DateTime.UtcNow);
                current = _bookmarks.GetChapters("podcasts", item.Id, duration);
                return new LitePodcastChapterPreparation(current.Count, imported);
            }

            var pageUrl = target.PageUrl?.Trim();
            var pageKey = $"{item.Id}\n{pageUrl}";
            if (pageUrl is { Length: > 0 }
                && !_emptyPageChecks.Contains(pageKey)
                && Uri.TryCreate(pageUrl, UriKind.Absolute, out var pageAddress))
            {
                // Odcinek z kanału YouTube nie ma opisu (lista kanału idzie przez
                // --flat-playlist), a strona watch trzyma opis w skryptach. Jego
                // rozdziały, takie jak pokazuje YouTube, daje dopiero yt-dlp.
                var youTube = _fetchYouTube is not null
                    && YouTubeSourceResolver.IsYouTubeUrl(pageAddress.AbsoluteUri);
                try
                {
                    var chapters = (youTube
                            ? _fetchYouTube!(pageAddress, duration, CancellationToken.None)
                            : _fetchPage(pageAddress, duration, CancellationToken.None))
                        .GetAwaiter()
                        .GetResult();
                    if (chapters.Count > 0)
                    {
                        imported += _bookmarks.ReplaceProviderChapters(
                            "podcasts",
                            "Podcasty i YouTube",
                            ItemWithDuration(item, duration),
                            youTube ? "youtube" : "podcast-page",
                            chapters,
                            DateTime.UtcNow);
                    }
                    else
                    {
                        _emptyPageChecks.Add(pageKey);
                    }
                }
                catch (Exception exception) when (IsRecoverableSourceFailure(exception))
                {
                    Console.Error.WriteLine(
                        $"[lite-host] nie odczytano strony rozdziałów odcinka {item.Id}: "
                        + exception.GetType().Name);
                }
            }

            current = _bookmarks.GetChapters("podcasts", item.Id, duration);
            return new LitePodcastChapterPreparation(current.Count, imported);
        }
        finally
        {
            _preparationGate.Release();
        }
    }

    private static MediaItem ItemWithDuration(MediaItem item, TimeSpan duration) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Artist = item.Artist,
        Kind = item.Kind,
        Duration = duration,
        Source = item.Source
    };

    private static bool IsRecoverableSourceFailure(Exception exception) => exception is
        HttpRequestException
        or IOException
        or InvalidDataException
        or ArgumentException
        or OperationCanceledException
        or TaskCanceledException
        or TimeoutException;

    public void Dispose()
    {
        _client?.Dispose();
        _preparationGate.Dispose();
    }
}
