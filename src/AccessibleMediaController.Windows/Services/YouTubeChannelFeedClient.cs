using System.Net.Http;
using System.Xml.Linq;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Reads the public Atom feed that YouTube publishes for every channel and
/// playlist. The feed is used only to complete data that the bounded yt-dlp
/// listing reports imprecisely or not at all.
/// </summary>
/// <remarks>
/// Zmierzone 2026-09-11 (yt-dlp 2026.08.19, cztery kanaly z biblioteki
/// uzytkownika):
///   * "youtubetab:approximate_date" podaje date PRZYBLIZONA, liczona z opisu
///     "2 weeks ago". Roznica wobec prawdziwej daty publikacji dochodzila do
///     miesiaca (2026-03-12 zamiast 2026-02-13). Kolejnosc materialow zostaje
///     zachowana, ale data widoczna dla uzytkownika jest nieprawdziwa.
///   * Feed Atom kanalu podaje date DOKLADNA, co do sekundy, oraz ORYGINALNY
///     tytul. yt-dlp zwracal czasem tytul przetlumaczony automatycznie na
///     angielski ("Let Go of Yourself" zamiast "Zrezygnuj z siebie").
///   * Feed obejmuje 15 najnowszych materialow. Pokrycie dziesieciu
///     najnowszych pozycji z listy yt-dlp wynosilo 10, 10, 10 i 4 na 10.
/// Dlatego feed sluzy wylacznie do UZUPELNIANIA: brak feedu, brak sieci albo
/// brak materialu w feedzie zostawia dane z yt-dlp bez zmian.
/// </remarks>
internal sealed class YouTubeChannelFeedClient
{
    private const int MaximumFeedCharacters = 2 * 1024 * 1024;
    private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YouTubeNamespace = "http://www.youtube.com/xml/schemas/2015";

    private readonly Func<HttpClient> httpClientFactory;

    internal YouTubeChannelFeedClient(Func<HttpClient> httpClientFactory) =>
        this.httpClientFactory = httpClientFactory
            ?? throw new ArgumentNullException(nameof(httpClientFactory));

    /// <summary>
    /// Builds the feed address for a collection identifier taken from the
    /// yt-dlp result. Channel identifiers start with "UC"; playlist
    /// identifiers use the playlist parameter instead.
    /// </summary>
    internal static Uri? TryBuildFeedAddress(string sourceIdentifier, bool isChannel)
    {
        if (string.IsNullOrWhiteSpace(sourceIdentifier)) return null;
        var trimmed = sourceIdentifier.Trim();
        if (trimmed.Length is < 2 or > 160) return null;
        if (!trimmed.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '_' or '-'))
        {
            return null;
        }

        var parameter = isChannel ? "channel_id" : "playlist_id";
        return new Uri(
            $"https://www.youtube.com/feeds/videos.xml?{parameter}={Uri.EscapeDataString(trimmed)}",
            UriKind.Absolute);
    }

    /// <summary>
    /// Returns the exact publication moment and original title for every
    /// material listed in the feed, keyed by video identifier. Failures are
    /// reported as an empty result: the feed only completes existing data, so
    /// a missing feed must never fail a refresh.
    /// </summary>
    internal async Task<IReadOnlyDictionary<string, YouTubeFeedEntry>> FetchEntriesAsync(
        Uri feedAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feedAddress);
        try
        {
            var client = httpClientFactory();
            using var response = await client
                .GetAsync(feedAddress, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);
            }

            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            return payload.Length is 0 or > MaximumFeedCharacters
                ? new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal)
                : ParseEntries(payload);
        }
        catch (HttpRequestException)
        {
            return new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);
        }
    }

    internal static IReadOnlyDictionary<string, YouTubeFeedEntry> ParseEntries(string payload)
    {
        var entries = new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal);
        XDocument document;
        try
        {
            document = XDocument.Parse(payload, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            return entries;
        }

        var feed = document.Root;
        if (feed is null || feed.Name != AtomNamespace + "feed") return entries;
        foreach (var entry in feed.Elements(AtomNamespace + "entry"))
        {
            var videoId = (string?)entry.Element(YouTubeNamespace + "videoId");
            if (string.IsNullOrWhiteSpace(videoId)) continue;
            videoId = videoId.Trim();
            if (videoId.Length is < 2 or > 64) continue;
            if (!videoId.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '_' or '-'))
            {
                continue;
            }

            var publishedText = (string?)entry.Element(AtomNamespace + "published");
            DateTimeOffset? published = null;
            if (DateTimeOffset.TryParse(
                    publishedText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal
                        | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var parsed)
                && parsed.Year is >= 2005 and <= 9999)
            {
                published = parsed;
            }

            var title = ((string?)entry.Element(AtomNamespace + "title") ?? string.Empty).Trim();
            if (published is null && title.Length == 0) continue;
            entries[videoId] = new YouTubeFeedEntry(published, title);
        }

        return entries;
    }
}

/// <summary>
/// A single material described by the channel feed. Both members are optional:
/// the feed completes yt-dlp data and never replaces it with nothing.
/// </summary>
internal sealed record YouTubeFeedEntry(DateTimeOffset? Published, string Title);
