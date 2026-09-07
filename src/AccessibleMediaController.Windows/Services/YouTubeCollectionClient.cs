using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private const int MaximumJsonCharacters = 16 * 1024 * 1024;

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
                "Kanały i playlisty YouTube wymagają składnika yt-dlp. Wybierz Pomoc, Sprawdź aktualizacje i składniki.");
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[]
        {
            "--ignore-config",
            "--flat-playlist",
            "--no-warnings",
            "--force-ipv4",
            "--socket-timeout", "15",
            "--extractor-retries", "2",
            "--playlist-end", MaximumItems.ToString(CultureInfo.InvariantCulture),
            "--extractor-args", "youtube:lang=pl",
            "--extractor-args", "youtubetab:approximate_date",
            "--dump-single-json",
            "--",
            normalizedAddress.AbsoluteUri
        })
        {
            start.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = Process.Start(start)
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
            var output = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidDataException("YouTube nie udostępnił obecnie tej publicznej kolekcji.");
            if (output.Length == 0 || output.Length > MaximumJsonCharacters)
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane kolekcji.");
            return ParseResult(normalizedAddress, sourceKind, output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("YouTube nie odpowiedział w bezpiecznym czasie.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            process?.Dispose();
        }
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

        var segments = address.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    internal static YouTubeCollectionDocument ParseResult(
        Uri normalizedAddress,
        PodcastSourceKind sourceKind,
        string json)
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
            var description = CleanText(ReadString(root, "description"), 4000);
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
                var videoId = ReadString(entry, "id").Trim();
                if (!IsSafeIdentifier(videoId, 64) || !knownVideoIds.Add(videoId)) continue;
                var pageUri = new Uri($"https://www.youtube.com/watch?v={videoId}", UriKind.Absolute);
                var episodeTitle = CleanText(ReadString(entry, "title"), 500);
                if (episodeTitle.Length == 0) episodeTitle = "Materiał YouTube";
                var episodeAuthor = CleanText(
                    FirstNonEmpty(ReadString(entry, "channel"), ReadString(entry, "uploader"), author),
                    300);
                var durationSeconds = ReadNumber(entry, "duration");
                var duration = durationSeconds is > 0 and < 365 * 24 * 60 * 60
                    ? TimeSpan.FromSeconds(durationSeconds.Value)
                    : TimeSpan.Zero;
                episodes.Add(new PodcastFeedEpisode(
                    $"{feedId}:{videoId}",
                    videoId,
                    episodeTitle,
                    episodeAuthor,
                    string.Empty,
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
