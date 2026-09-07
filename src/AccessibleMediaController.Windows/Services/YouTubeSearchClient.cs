using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Performs a bounded, public YouTube search through the separately managed
/// yt-dlp component. The search does not read browser cookies or account data
/// and returns stable page addresses rather than temporary playback URLs.
/// </summary>
internal sealed class YouTubeSearchClient
{
    private const int MaximumQueryLength = 300;
    private const int MaximumResultCount = 25;
    private const int MaximumChannelResultCount = 10;
    private const int MaximumJsonCharacters = 8 * 1024 * 1024;
    private const string SearchResultPrefix = "internet-media-search:youtube:";
    private const string ChannelSearchResultPrefix = "podcast-directory:youtube:";
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, (DateTime StoredUtc, IReadOnlyList<MediaItem> Items)> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    internal async Task<IReadOnlyList<MediaItem>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.Join(' ', query.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalized.Length == 0) return [];
        if (normalized.Length > MaximumQueryLength) normalized = normalized[..MaximumQueryLength].Trim();

        lock (_cacheGate)
        {
            if (_cache.TryGetValue(normalized, out var cached)
                && DateTime.UtcNow - cached.StoredUtc <= CacheLifetime)
            {
                return cached.Items;
            }
        }

        var executable = YouTubeSourceResolver.FindExecutable()
            ?? throw new NotSupportedException(
                "Wyszukiwanie YouTube wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź aktualizacje i składniki.");
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
            "--socket-timeout", "8",
            "--extractor-retries", "1",
            "--playlist-end", MaximumResultCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dump-single-json",
            "--",
            $"ytsearch{MaximumResultCount}:{normalized}"
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
            // Search is an interactive operation. An unavailable YouTube must
            // not hold the entire combined podcast search for nearly a minute.
            timeout.CancelAfter(TimeSpan.FromSeconds(18));
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
                throw new InvalidDataException("YouTube nie zwrócił obecnie wyników wyszukiwania.");
            if (output.Length == 0 || output.Length > MaximumJsonCharacters)
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane wyszukiwania.");
            var items = ParseResults(output);
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

    internal static IReadOnlyList<MediaItem> ParseResults(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("entries", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane wyszukiwania.");
            }

            var channelResults = new List<MediaItem>();
            var videoResults = new List<MediaItem>();
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            var knownChannelAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries.EnumerateArray())
            {
                if (videoResults.Count >= MaximumResultCount || entry.ValueKind != JsonValueKind.Object) break;
                var id = ReadString(entry, "id").Trim();
                if (!IsSafeVideoId(id) || !knownIds.Add(id)) continue;
                var title = CleanText(ReadString(entry, "title"), 500);
                if (title.Length == 0) title = "Materiał YouTube";
                var channel = CleanText(ReadString(entry, "channel"), 300);
                if (channel.Length == 0) channel = CleanText(ReadString(entry, "uploader"), 300);
                if (channelResults.Count < MaximumChannelResultCount
                    && TryCreateChannelResult(entry, channel, knownChannelAddresses, out var channelResult))
                {
                    channelResults.Add(channelResult);
                }
                var durationSeconds = ReadNumber(entry, "duration");
                var duration = durationSeconds is > 0 and < 365 * 24 * 60 * 60
                    ? TimeSpan.FromSeconds(durationSeconds.Value)
                    : TimeSpan.Zero;
                var liveStatus = ReadString(entry, "live_status");
                var isLive = ReadBoolean(entry, "is_live")
                             || liveStatus.Equals("is_live", StringComparison.OrdinalIgnoreCase);
                var pageUrl = $"https://www.youtube.com/watch?v={id}";
                videoResults.Add(new MediaItem
                {
                    Id = $"{SearchResultPrefix}{id}",
                    Title = title,
                    Artist = channel,
                    Kind = MediaItemKind.Episode,
                    Duration = duration,
                    Source = pageUrl,
                    PublicUri = pageUrl,
                    ExternalId = "internet-media:public",
                    Tags = isLive ? "YouTube, transmisja na żywo" : "YouTube",
                    IsAvailable = true,
                    IsInLibrary = false
                });
            }
            // A person looking for a named publisher normally wants to subscribe
            // to the channel first. Search videos are still retained below the
            // deduplicated channels for immediate playback.
            return channelResults.Concat(videoResults).ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane wyszukiwania.", exception);
        }
    }

    internal static bool IsSearchResult(MediaItem? item) =>
        item?.Id.StartsWith(SearchResultPrefix, StringComparison.Ordinal) == true;

    internal static bool IsChannelSearchResult(MediaItem? item) =>
        item?.Id.StartsWith(ChannelSearchResultPrefix, StringComparison.Ordinal) == true;

    internal static bool IsLiveSearchResult(MediaItem item) =>
        IsSearchResult(item)
        && item.Tags?.Contains("transmisja na żywo", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsSafeVideoId(string value) =>
        value.Length is >= 6 and <= 64
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static bool TryCreateChannelResult(
        JsonElement entry,
        string channelName,
        ISet<string> knownChannelAddresses,
        out MediaItem result)
    {
        result = null!;
        if (channelName.Length == 0) return false;

        var channelAddress = ReadString(entry, "channel_url").Trim();
        if (channelAddress.Length == 0) channelAddress = ReadString(entry, "uploader_url").Trim();
        if (!Uri.TryCreate(channelAddress, UriKind.Absolute, out var parsedAddress)
            || !YouTubeCollectionClient.TryNormalizeCollectionAddress(
                parsedAddress,
                out var normalizedAddress,
                out var sourceKind)
            || sourceKind != PodcastSourceKind.YouTubeChannel
            || !knownChannelAddresses.Add(normalizedAddress.AbsoluteUri))
        {
            return false;
        }

        var channelId = ReadString(entry, "channel_id").Trim();
        if (!IsSafeChannelId(channelId))
        {
            channelId = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(normalizedAddress.AbsoluteUri)))
                .ToLowerInvariant();
        }
        result = new MediaItem
        {
            Id = $"{ChannelSearchResultPrefix}{channelId}",
            Title = channelName,
            Kind = MediaItemKind.Podcast,
            Source = normalizedAddress.AbsoluteUri,
            PublicUri = channelAddress,
            ExternalId = $"youtube:{channelId}",
            Tags = "YouTube",
            IsAvailable = true,
            IsInLibrary = false
        };
        return true;
    }

    private static bool IsSafeChannelId(string value) =>
        value.Length is >= 6 and <= 160
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static string CleanText(string value, int maximumLength)
    {
        var normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength].Trim();
    }

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

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
