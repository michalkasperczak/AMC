using System.Globalization;
using System.Text;

namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// One YouTube collection kept in the AMC library: a channel or a playlist.
/// <paramref name="SourceIdentifier"/> is the bare YouTube identifier
/// (<c>UC...</c> for channels, <c>PL...</c> and similar for playlists), taken
/// from the subscription identifier, never a resolved playback address.
/// </summary>
public sealed record YouTubeCollectionExportEntry(
    string Title,
    string SourceIdentifier,
    bool IsChannel,
    string? HomepageUrl);

/// <summary>
/// Writes the AMC list of YouTube channels and playlists in the two shapes that
/// other programs actually accept:
/// <list type="bullet">
///   <item>the Google Takeout <c>subscriptions.csv</c> column layout
///     (<c>Channel Id,Channel Url,Channel Title</c>), which YouTube itself and
///     the subscription-transfer tools read;</item>
///   <item>OPML pointing at <c>youtube.com/feeds/videos.xml</c>, which podcast
///     programs and feed readers read.</item>
/// </list>
/// Playlists have no channel identifier, so they are absent from the CSV by
/// design and exported only as feeds - a playlist written into a subscriptions
/// file would be silently dropped by the importer instead.
/// </summary>
public static class YouTubeSubscriptionsExporter
{
    private const int MaximumEntries = 5_000;
    private const int MaximumTitleLength = 300;

    public static byte[] WriteTakeoutCsv(IEnumerable<YouTubeCollectionExportEntry> collections)
    {
        ArgumentNullException.ThrowIfNull(collections);
        // Naglowki dokladnie jak w eksporcie Google Takeout - importery
        // dopasowuja kolumny po nazwie, wiec wlasne brzmienie zepsulo by import.
        var builder = new StringBuilder("Channel Id,Channel Url,Channel Title\r\n");
        foreach (var entry in Accepted(collections, channelsOnly: true))
        {
            builder
                .Append(CsvField(entry.SourceIdentifier))
                .Append(',')
                .Append(CsvField($"http://www.youtube.com/channel/{entry.SourceIdentifier}"))
                .Append(',')
                .Append(CsvField(entry.Title))
                .Append("\r\n");
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(builder.ToString());
    }

    public static byte[] WriteFeedOpml(
        IEnumerable<YouTubeCollectionExportEntry> collections,
        string title = "Kanały YouTube AMC")
    {
        ArgumentNullException.ThrowIfNull(collections);
        var entries = Accepted(collections, channelsOnly: false)
            .Select(entry => new PodcastOpmlEntry(
                entry.Title,
                FeedAddress(entry),
                Uri.TryCreate(entry.HomepageUrl, UriKind.Absolute, out var homepage)
                    && homepage.Scheme is "http" or "https"
                        ? homepage
                        : null));
        return PodcastOpmlWriter.Write(entries, title);
    }

    /// <summary>
    /// Reads the bare YouTube identifier out of an AMC subscription identifier
    /// such as <c>youtube-channel:UC...</c>. Anything else returns false, so a
    /// plain RSS podcast never leaks into a YouTube export.
    /// </summary>
    public static bool TryParseSubscriptionId(
        string? subscriptionId,
        out string sourceIdentifier,
        out bool isChannel)
    {
        sourceIdentifier = string.Empty;
        isChannel = false;
        if (string.IsNullOrWhiteSpace(subscriptionId)) return false;
        var trimmed = subscriptionId.Trim();
        string candidate;
        if (trimmed.StartsWith("youtube-channel:", StringComparison.Ordinal))
        {
            isChannel = true;
            candidate = trimmed["youtube-channel:".Length..];
        }
        else if (trimmed.StartsWith("youtube-playlist:", StringComparison.Ordinal))
        {
            candidate = trimmed["youtube-playlist:".Length..];
        }
        else
        {
            return false;
        }

        if (!IsSafeIdentifier(candidate)) return false;
        sourceIdentifier = candidate;
        return true;
    }

    private static List<YouTubeCollectionExportEntry> Accepted(
        IEnumerable<YouTubeCollectionExportEntry> collections,
        bool channelsOnly)
    {
        var accepted = new List<YouTubeCollectionExportEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in collections)
        {
            if (accepted.Count >= MaximumEntries) break;
            if (channelsOnly && !entry.IsChannel) continue;
            if (!IsSafeIdentifier(entry.SourceIdentifier)) continue;
            if (!seen.Add($"{(entry.IsChannel ? "c" : "p")}:{entry.SourceIdentifier}")) continue;

            accepted.Add(entry with { Title = NormalizeTitle(entry.Title, entry.IsChannel) });
        }

        return accepted;
    }

    private static Uri FeedAddress(YouTubeCollectionExportEntry entry) =>
        new(string.Create(
                CultureInfo.InvariantCulture,
                $"https://www.youtube.com/feeds/videos.xml?{(entry.IsChannel ? "channel_id" : "playlist_id")}={Uri.EscapeDataString(entry.SourceIdentifier)}"),
            UriKind.Absolute);

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
            && value.Length is >= 2 and <= 160
            && value.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '_' or '-');

    private static string NormalizeTitle(string? value, bool isChannel)
    {
        var builder = new StringBuilder();
        var previousWasSpace = false;
        foreach (var character in value ?? string.Empty)
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasSpace) builder.Append(' ');
                previousWasSpace = true;
                continue;
            }
            if (char.IsControl(character)) continue;

            builder.Append(character);
            previousWasSpace = false;
            if (builder.Length >= MaximumTitleLength) break;
        }

        var title = builder.ToString().Trim();
        if (title.Length > 0) return title;
        return isChannel ? "Kanał YouTube" : "Playlista YouTube";
    }

    private static string CsvField(string value) =>
        value.Any(character => character is ',' or '"' or '\r' or '\n')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
}
