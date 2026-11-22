using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows.Services;

internal sealed record YouTubeCollectionDocument(
    PodcastFeedDocument Feed,
    PodcastSourceKind SourceKind);

/// <summary>
/// Reads a bounded public YouTube channel or playlist without browser cookies
/// or account authorization. Only stable YouTube page addresses are persisted.
/// </summary>
internal sealed class YouTubeCollectionClient
{
    internal const int MaximumItems = 100;
    internal const string CollectionDateExtractorArguments =
        "youtubetab:approximate_date";
    internal const string CollectionLanguageExtractorArguments =
        "youtube:lang=pl";
    private const int MaximumJsonCharacters = 16 * 1024 * 1024;

    private static readonly HttpClient FeedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly YouTubeChannelFeedClient channelFeed;

    internal YouTubeCollectionClient()
        : this(new YouTubeChannelFeedClient(static () => FeedHttpClient))
    {
    }

    internal YouTubeCollectionClient(YouTubeChannelFeedClient channelFeed) =>
        this.channelFeed = channelFeed ?? throw new ArgumentNullException(nameof(channelFeed));

    internal async Task<YouTubeCollectionDocument> FetchAsync(
        Uri address,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeCollectionAddress(address, out var normalizedAddress, out var sourceKind))
        {
            throw new InvalidDataException(
                "Adres YouTube nie prowadzi do publicznego kanału ani playlisty.");
        }

        var executable = YouTubeSourceResolver.FindExecutable()
            ?? throw new NotSupportedException(
                "Kanały i playlisty YouTube wymagają składnika yt-dlp. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");
        try
        {
            var result = await RunYtDlpAsync(executable, normalizedAddress, cancellationToken)
                .ConfigureAwait(false);
            if (result.ExitCode != 0
                && TryBuildChannelFallbackAddress(
                    normalizedAddress,
                    sourceKind,
                    result.Error,
                    out var fallbackAddress))
            {
                result = await RunYtDlpAsync(executable, fallbackAddress, cancellationToken)
                    .ConfigureAwait(false);
            }
            YouTubeCollectionDocument? document = null;
            InvalidDataException? primaryFailure = null;
            if (result.ExitCode != 0)
            {
                primaryFailure = new InvalidDataException(
                    "YouTube nie udostępnił obecnie tej publicznej kolekcji.");
            }
            else if (result.Output.Length == 0 || result.Output.Length > MaximumJsonCharacters)
            {
                primaryFailure = new InvalidDataException(
                    "YouTube zwrócił nieprawidłowe dane kolekcji.");
            }
            else
            {
                try
                {
                    document = ParseResult(normalizedAddress, sourceKind, result.Output);
                }
                catch (InvalidDataException exception)
                {
                    primaryFailure = exception;
                }
            }

            IReadOnlyDictionary<string, YouTubeFeedEntry> feedEntries =
                new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);
            var feedRead = false;
            if (document is not null)
            {
                feedEntries = await FetchChannelFeedEntriesAsync(document, cancellationToken)
                    .ConfigureAwait(false);
                feedRead = true;
            }

            if (sourceKind == PodcastSourceKind.YouTubeChannel
                && (document is null || ShouldFetchArchivedStreams(document, feedEntries)))
            {
                var archivedStreams = await TryFetchArchivedStreamsAsync(
                        executable,
                        normalizedAddress,
                        cancellationToken)
                    .ConfigureAwait(false);
                document = document is null
                    ? archivedStreams
                    : archivedStreams is null
                        ? document
                        : MergeChannelDocuments(document, archivedStreams);
            }

            if (document is null)
                throw primaryFailure
                    ?? new InvalidDataException(
                        "Kanał albo playlista YouTube nie zawiera dostępnych materiałów.");
            if (!feedRead)
            {
                feedEntries = await FetchChannelFeedEntriesAsync(document, cancellationToken)
                    .ConfigureAwait(false);
            }
            return CompleteFromChannelFeed(document, feedEntries);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("YouTube nie odpowiedział w bezpiecznym czasie.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
    }

    internal Task<IReadOnlyDictionary<string, YouTubeFeedEntry>> FetchChannelFeedEntriesAsync(
        string channelId, CancellationToken cancellationToken)
    {
        var address = YouTubeChannelFeedClient.TryBuildFeedAddress(channelId, isChannel: true);
        return address is null
            ? Task.FromResult<IReadOnlyDictionary<string, YouTubeFeedEntry>>(
                new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal))
            : channelFeed.FetchEntriesAsync(address, cancellationToken);
    }

    private static async Task<YtDlpResult> RunYtDlpAsync(
        string executable,
        Uri address,
        CancellationToken cancellationToken,
        bool includeLocalizedMetadata = true)
    {
        var start = ExternalToolProcess.Create(executable);
        foreach (var argument in BuildYtDlpArguments(address, includeLocalizedMetadata))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.");
        process.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var termination = timeout.Token.Register(
            static state =>
            {
                try
                {
                    var running = (Process)state!;
                    if (!running.HasExited) running.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                }
            },
            process);
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        return new YtDlpResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    internal static string[] BuildYtDlpArguments(
        Uri address,
        bool includeLocalizedMetadata)
    {
        var arguments = new List<string>
        {
            "--ignore-config",
            "--flat-playlist",
            "--no-warnings",
            "--force-ipv4",
            "--socket-timeout", "15",
            "--extractor-retries", "2",
            "--playlist-end", MaximumItems.ToString(CultureInfo.InvariantCulture),
            // Korekta 2026-10-27: oficjalna skladnia wymaga osobnego
            // KEY:ARGS dla kazdego ekstraktora. Srednik rozdziela argumenty
            // jednego ekstraktora, nie dwie grupy. youtubetab odpowiada za
            // daty, a youtube za jezyk metadanych.
            "--extractor-args", CollectionDateExtractorArguments
        };
        if (includeLocalizedMetadata)
        {
            arguments.Add("--extractor-args");
            arguments.Add(CollectionLanguageExtractorArguments);
        }
        arguments.Add("--dump-single-json");
        arguments.Add("--");
        arguments.Add(address.AbsoluteUri);
        return arguments.ToArray();
    }

    private static async Task<YouTubeCollectionDocument?> TryFetchArchivedStreamsAsync(
        string executable,
        Uri normalizedAddress,
        CancellationToken cancellationToken)
    {
        if (!TryBuildChannelStreamsAddress(
                normalizedAddress,
                PodcastSourceKind.YouTubeChannel,
                out var streamsAddress))
            return null;
        try
        {
            var statusResult = await RunYtDlpAsync(
                    executable,
                    streamsAddress,
                    cancellationToken,
                    includeLocalizedMetadata: false)
                .ConfigureAwait(false);
            if (statusResult.ExitCode != 0
                || statusResult.Output.Length == 0
                || statusResult.Output.Length > MaximumJsonCharacters)
                return null;
            var archivedStreams = ParseResult(
                normalizedAddress,
                PodcastSourceKind.YouTubeChannel,
                statusResult.Output,
                archivedLiveStreamsOnly: true);

            try
            {
                var localizedResult = await RunYtDlpAsync(
                        executable,
                        streamsAddress,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (localizedResult.ExitCode != 0
                    || localizedResult.Output.Length == 0
                    || localizedResult.Output.Length > MaximumJsonCharacters)
                    return archivedStreams;
                var localizedMetadata = ParseResult(
                    normalizedAddress,
                    PodcastSourceKind.YouTubeChannel,
                    localizedResult.Output);
                return CompleteArchivedStreamsFromLocalizedMetadata(
                    archivedStreams,
                    localizedMetadata);
            }
            catch (InvalidDataException)
            {
                return archivedStreams;
            }
            catch (Win32Exception)
            {
                return archivedStreams;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return archivedStreams;
            }
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    internal static bool TryBuildChannelFallbackAddress(
        Uri normalizedAddress,
        PodcastSourceKind sourceKind,
        string error,
        out Uri fallbackAddress)
    {
        fallbackAddress = null!;
        const string videosSuffix = "/videos";
        if (sourceKind != PodcastSourceKind.YouTubeChannel
            || !normalizedAddress.AbsolutePath.EndsWith(videosSuffix, StringComparison.OrdinalIgnoreCase)
            || !error.Contains("does not have a videos tab", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new UriBuilder(normalizedAddress)
        {
            Path = normalizedAddress.AbsolutePath[..^videosSuffix.Length].TrimEnd('/') + "/",
            Query = string.Empty,
            Fragment = string.Empty
        };
        fallbackAddress = builder.Uri;
        return true;
    }

    internal static bool TryBuildChannelStreamsAddress(
        Uri normalizedAddress,
        PodcastSourceKind sourceKind,
        out Uri streamsAddress)
    {
        streamsAddress = null!;
        const string videosSuffix = "/videos";
        if (sourceKind != PodcastSourceKind.YouTubeChannel
            || !normalizedAddress.AbsolutePath.EndsWith(
                videosSuffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new UriBuilder(normalizedAddress)
        {
            Path = normalizedAddress.AbsolutePath[..^videosSuffix.Length].TrimEnd('/')
                + "/streams",
            Query = string.Empty,
            Fragment = string.Empty
        };
        streamsAddress = builder.Uri;
        return true;
    }

    internal static bool TryNormalizeCollectionAddress(
        Uri address,
        out Uri normalizedAddress,
        out PodcastSourceKind sourceKind)
    {
        normalizedAddress = null!;
        sourceKind = PodcastSourceKind.PublicInternetMedia;
        if (!YouTubeSourceResolver.IsYouTubeUrl(address.AbsoluteUri)
            || address.Scheme is not ("http" or "https")
            || !string.IsNullOrWhiteSpace(address.UserInfo))
        {
            return false;
        }

        var query = ParseQuery(address.Query);
        if (query.GetValueOrDefault("list") is { Length: >= 6 } playlistId
            && IsSafeIdentifier(playlistId, 160))
        {
            normalizedAddress = new Uri(
                $"https://www.youtube.com/playlist?list={Uri.EscapeDataString(playlistId)}",
                UriKind.Absolute);
            sourceKind = PodcastSourceKind.YouTubePlaylist;
            return true;
        }

        // ``Uri.AbsolutePath`` zachowuje kodowanie procentowe. Pierwsze
        // rozpoznanie uchwytu @ zwraca stabilny adres z ``%40``; FetchAsync
        // celowo normalizuje go ponownie przed uruchomieniem yt-dlp. Segmenty
        // muszą więc zostać odkodowane przed walidacją, aby normalizacja była
        // idempotentna. Walidatory poniżej nadal odrzucają odkodowane ukośniki,
        // znaki zapytania i pozostałe znaki spoza bezpiecznego alfabetu.
        var segments = address.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length == 0) return false;
        var first = segments[0];
        var channelAddress = first.StartsWith('@')
            || first.Equals("channel", StringComparison.OrdinalIgnoreCase)
            || first.Equals("c", StringComparison.OrdinalIgnoreCase)
            || first.Equals("user", StringComparison.OrdinalIgnoreCase);
        if (!channelAddress) return false;
        if (first.StartsWith('@') && !IsSafeHandle(first)
            || !first.StartsWith('@') && (segments.Length < 2 || !IsSafeIdentifier(segments[1], 160)))
        {
            return false;
        }

        var identitySegments = first.StartsWith('@')
            ? new[] { first }
            : new[] { first, segments[1] };
        var path = string.Join('/', identitySegments.Select(Uri.EscapeDataString));
        normalizedAddress = new Uri($"https://www.youtube.com/{path}/videos", UriKind.Absolute);
        sourceKind = PodcastSourceKind.YouTubeChannel;
        return true;
    }

    private sealed record YtDlpResult(int ExitCode, string Output, string Error);

    internal static YouTubeCollectionDocument ParseResult(
        Uri normalizedAddress,
        PodcastSourceKind sourceKind,
        string json,
        bool archivedLiveStreamsOnly = false)
    {
        if (sourceKind is not (PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist))
            throw new ArgumentOutOfRangeException(nameof(sourceKind));
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || ReadString(root, "_type") is not "playlist"
                || !root.TryGetProperty("entries", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Adres nie prowadzi do kanału ani playlisty YouTube.");
            }

            var sourceIdentifier = ReadString(root, "id").Trim();
            if (!IsSafeIdentifier(sourceIdentifier, 160))
                sourceIdentifier = StableId(normalizedAddress.AbsoluteUri);
            var kindLabel = sourceKind == PodcastSourceKind.YouTubeChannel ? "channel" : "playlist";
            var feedId = $"youtube-{kindLabel}:{sourceIdentifier}";
            var author = CleanText(
                FirstNonEmpty(ReadString(root, "channel"), ReadString(root, "uploader")),
                300);
            var title = sourceKind == PodcastSourceKind.YouTubeChannel
                ? author
                : CleanText(ReadString(root, "title"), 500);
            if (title.Length == 0)
            {
                title = sourceKind == PodcastSourceKind.YouTubeChannel
                    ? "Kanał YouTube"
                    : "Playlista YouTube";
            }
            var description = CleanDescription(ReadString(root, "description"), 20_000);
            var homepage = sourceKind == PodcastSourceKind.YouTubeChannel
                ? TryReadHttpUri(root, "channel_url")
                    ?? TryReadHttpUri(root, "webpage_url")
                    ?? normalizedAddress
                : TryReadHttpUri(root, "webpage_url")
                    ?? normalizedAddress;
            var episodes = new List<PodcastFeedEpisode>();
            var knownVideoIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries.EnumerateArray())
            {
                if (episodes.Count >= MaximumItems || entry.ValueKind != JsonValueKind.Object) break;
                if (archivedLiveStreamsOnly
                    && !string.Equals(
                        ReadString(entry, "live_status"),
                        "was_live",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                var videoId = ReadString(entry, "id").Trim();
                if (!IsSafeIdentifier(videoId, 64) || !knownVideoIds.Add(videoId)) continue;
                var pageUri = new Uri($"https://www.youtube.com/watch?v={videoId}", UriKind.Absolute);
                var episodeTitle = CleanText(ReadString(entry, "title"), 500);
                if (episodeTitle.Length == 0) episodeTitle = "Materiał YouTube";
                var episodeAuthor = CleanText(
                    FirstNonEmpty(ReadString(entry, "channel"), ReadString(entry, "uploader"), author),
                    300);
                var episodeDescription = CleanDescription(ReadString(entry, "description"), 20_000);
                var durationSeconds = ReadNumber(entry, "duration");
                var duration = durationSeconds is > 0 and < 365 * 24 * 60 * 60
                    ? TimeSpan.FromSeconds(durationSeconds.Value)
                    : TimeSpan.Zero;
                episodes.Add(new PodcastFeedEpisode(
                    $"{feedId}:{videoId}",
                    videoId,
                    episodeTitle,
                    episodeAuthor,
                    episodeDescription,
                    ReadPublished(entry),
                    duration,
                    pageUri,
                    pageUri,
                    "video/youtube",
                    null));
            }
            if (episodes.Count == 0)
                throw new InvalidDataException("Kanał albo playlista YouTube nie zawiera dostępnych materiałów.");
            return new YouTubeCollectionDocument(
                new PodcastFeedDocument(
                    feedId,
                    title,
                    author,
                    description,
                    normalizedAddress,
                    homepage,
                    episodes),
                sourceKind);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane kolekcji.", exception);
        }
    }

    internal static YouTubeCollectionDocument MergeChannelDocuments(
        YouTubeCollectionDocument standardVideos,
        YouTubeCollectionDocument archivedStreams)
    {
        if (standardVideos.SourceKind != PodcastSourceKind.YouTubeChannel
            || archivedStreams.SourceKind != PodcastSourceKind.YouTubeChannel)
            throw new ArgumentException("Scalanie dotyczy wyłącznie kanałów YouTube.");

        // Nie obcinamy tutaj dwóch okien po 100 pozycji. Filmy z /videos mogą
        // jeszcze nie mieć daty z yt-dlp, podczas gdy stare transmisje już ją
        // mają; cięcie przed CompleteFromChannelFeed wycinałoby filmy zanim
        // RSS poda ich właściwą datę. Końcowe sortowanie i limit wykonuje
        // CompleteFromChannelFeed już po nałożeniu danych RSS.
        var merged = archivedStreams.Feed.Episodes
            .Concat(standardVideos.Feed.Episodes)
            .GroupBy(episode => episode.SourceIdentifier, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        return new YouTubeCollectionDocument(
            standardVideos.Feed with { Episodes = merged },
            PodcastSourceKind.YouTubeChannel);
    }

    internal static YouTubeCollectionDocument CompleteArchivedStreamsFromLocalizedMetadata(
        YouTubeCollectionDocument archivedStreams,
        YouTubeCollectionDocument localizedMetadata)
    {
        if (archivedStreams.SourceKind != PodcastSourceKind.YouTubeChannel
            || localizedMetadata.SourceKind != PodcastSourceKind.YouTubeChannel)
            throw new ArgumentException("Uzupełnianie metadanych dotyczy wyłącznie kanałów YouTube.");

        var localizedByVideoId = localizedMetadata.Feed.Episodes
            .GroupBy(episode => episode.SourceIdentifier, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var completed = archivedStreams.Feed.Episodes.Select(episode =>
        {
            if (!localizedByVideoId.TryGetValue(episode.SourceIdentifier, out var localized))
                return episode;
            return episode with
            {
                Title = localized.Title.Length > 0 ? localized.Title : episode.Title,
                Published = localized.Published ?? episode.Published
            };
        }).ToArray();
        return new YouTubeCollectionDocument(
            archivedStreams.Feed with { Episodes = completed },
            PodcastSourceKind.YouTubeChannel);
    }

    private static PodcastFeedEpisode[] OrderChannelEpisodes(
        IEnumerable<PodcastFeedEpisode> episodes) =>
        episodes
            .Select((episode, index) => new { Episode = episode, Index = index })
            .OrderByDescending(item => item.Episode.Published.HasValue)
            .ThenByDescending(item => item.Episode.Published)
            .ThenBy(item => item.Index)
            .Take(MaximumItems)
            .Select(item => item.Episode)
            .ToArray();

    /// <summary>
    /// Replaces the approximate publication date and the possibly translated
    /// title with the exact values published in the channel feed. Materials
    /// missing from the feed, and every feed failure, leave the yt-dlp data
    /// untouched.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, YouTubeFeedEntry>>
        FetchChannelFeedEntriesAsync(
        YouTubeCollectionDocument document,
        CancellationToken cancellationToken)
    {
        var separator = document.Feed.Id.IndexOf(':');
        if (separator < 0 || separator + 1 >= document.Feed.Id.Length)
            return new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);
        var sourceIdentifier = document.Feed.Id[(separator + 1)..];
        var feedAddress = YouTubeChannelFeedClient.TryBuildFeedAddress(
            sourceIdentifier,
            document.SourceKind == PodcastSourceKind.YouTubeChannel);
        if (feedAddress is null)
            return new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);

        return await channelFeed
            .FetchEntriesAsync(feedAddress, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static bool ShouldFetchArchivedStreams(
        YouTubeCollectionDocument document,
        IReadOnlyDictionary<string, YouTubeFeedEntry> feedEntries)
    {
        if (document.SourceKind != PodcastSourceKind.YouTubeChannel)
            return false;
        if (feedEntries.Count == 0)
            return true;
        var known = document.Feed.Episodes
            .Select(episode => episode.SourceIdentifier)
            .ToHashSet(StringComparer.Ordinal);
        return feedEntries.Any(item => !item.Value.IsShort && !known.Contains(item.Key));
    }

    internal static YouTubeCollectionDocument CompleteFromChannelFeed(
        YouTubeCollectionDocument document,
        IReadOnlyDictionary<string, YouTubeFeedEntry> feedEntries)
    {
        if (feedEntries.Count == 0)
        {
            if (document.SourceKind != PodcastSourceKind.YouTubeChannel)
                return document;
            return new YouTubeCollectionDocument(
                document.Feed with { Episodes = OrderChannelEpisodes(document.Feed.Episodes) },
                document.SourceKind);
        }

        var completed = new List<PodcastFeedEpisode>(document.Feed.Episodes.Count);
        foreach (var episode in document.Feed.Episodes)
        {
            if (!feedEntries.TryGetValue(episode.SourceIdentifier, out var entry))
            {
                completed.Add(episode);
                continue;
            }

            var title = entry.Title.Length > 0
                ? CleanText(entry.Title, 500)
                : episode.Title;
            if (title.Length == 0) title = episode.Title;
            completed.Add(episode with
            {
                Title = title,
                Published = entry.Published ?? episode.Published
            });
        }

        var result = document.SourceKind == PodcastSourceKind.YouTubeChannel
            ? OrderChannelEpisodes(completed)
            : completed.ToArray();
        return new YouTubeCollectionDocument(
            document.Feed with { Episodes = result },
            document.SourceKind);
    }

    private static DateTimeOffset? ReadPublished(JsonElement entry)
    {
        var timestamp = ReadNumber(entry, "timestamp") ?? ReadNumber(entry, "release_timestamp");
        if (timestamp is >= 0 and <= 253402300799)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds((long)Math.Round(timestamp.Value));
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
        var uploadDate = ReadString(entry, "upload_date");
        return DateTimeOffset.TryParseExact(
            uploadDate,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
                ? parsed
                : null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var name = Uri.UnescapeDataString(separator < 0 ? part : part[..separator]);
            var value = Uri.UnescapeDataString(separator < 0 ? string.Empty : part[(separator + 1)..]);
            if (name.Length > 0 && !values.ContainsKey(name)) values[name] = value;
        }
        return values;
    }

    private static bool IsSafeHandle(string value) =>
        value.Length is >= 2 and <= 101
        && value[0] == '@'
        && value[1..].All(character => char.IsLetterOrDigit(character)
            || character is '_' or '-' or '.');

    private static bool IsSafeIdentifier(string value, int maximumLength) =>
        value.Length is >= 2
        && value.Length <= maximumLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static string StableId(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string CleanText(string value, int maximumLength)
    {
        var normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength].Trim();
    }

    private static string CleanDescription(string value, int maximumLength)
    {
        var normalized = value
            .Replace("\0", " ", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n')
            .Select(line => string.Join(' ', line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var result = string.Join('\n', lines);
        return result.Length <= maximumLength ? result : result[..maximumLength].TrimEnd();
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static Uri? TryReadHttpUri(JsonElement element, string name) =>
        Uri.TryCreate(ReadString(element, name), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.IsNullOrWhiteSpace(uri.UserInfo)
            ? uri
            : null;

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static double? ReadNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;
}
