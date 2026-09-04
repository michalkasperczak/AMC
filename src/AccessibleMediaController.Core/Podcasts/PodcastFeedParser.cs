using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public sealed record PodcastFeedDocument(
    string Id,
    string Title,
    string Author,
    string Description,
    Uri FeedUri,
    Uri? HomepageUri,
    IReadOnlyList<PodcastFeedEpisode> Episodes);

public sealed record PodcastFeedEpisode(
    string Id,
    string SourceIdentifier,
    string Title,
    string Author,
    string Description,
    DateTimeOffset? Published,
    TimeSpan Duration,
    Uri MediaUri,
    Uri? PageUri,
    string? MediaType,
    long? MediaLength,
    Uri? ChaptersUri = null,
    IReadOnlyList<ProviderChapterPoint>? Chapters = null);

/// <summary>
/// Parses already downloaded RSS and Atom metadata. Network retrieval is kept
/// outside this class so callers can independently enforce timeouts, redirect
/// limits and response-size limits before untrusted XML reaches the parser.
/// </summary>
public static partial class PodcastFeedParser
{
    public const long MaximumXmlCharacters = 5L * 1024 * 1024;
    public const int MaximumEpisodes = 1000;

    public static PodcastFeedDocument Parse(string xml, Uri feedUri)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(feedUri);
        using var textReader = new StringReader(xml);
        return ParseReader(textReader, feedUri);
    }

    public static PodcastFeedDocument Parse(Stream stream, Uri feedUri)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(feedUri);
        ValidateFeedUri(feedUri);
        using var reader = XmlReader.Create(stream, ReaderSettings());
        return ParseDocument(XDocument.Load(reader, LoadOptions.None), feedUri);
    }

    private static PodcastFeedDocument ParseReader(TextReader textReader, Uri feedUri)
    {
        ValidateFeedUri(feedUri);
        using var reader = XmlReader.Create(textReader, ReaderSettings());
        return ParseDocument(XDocument.Load(reader, LoadOptions.None), feedUri);
    }

    private static void ValidateFeedUri(Uri feedUri)
    {
        if (!IsHttpUri(feedUri))
        {
            throw new ArgumentException("Adres kanału musi używać protokołu HTTP albo HTTPS.", nameof(feedUri));
        }
        if (!string.IsNullOrEmpty(feedUri.UserInfo))
        {
            throw new ArgumentException("Adres kanału nie może zawierać nazwy użytkownika ani hasła.", nameof(feedUri));
        }
    }

    private static XmlReaderSettings ReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaximumXmlCharacters,
        MaxCharactersFromEntities = 0,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true
    };

    private static PodcastFeedDocument ParseDocument(XDocument document, Uri feedUri)
    {
        var root = document.Root ?? throw new InvalidDataException("Kanał podcastu jest pusty.");
        return root.Name.LocalName.ToLowerInvariant() switch
        {
            "rss" or "rdf" => ParseRss(root, feedUri),
            "feed" => ParseAtom(root, feedUri),
            _ => throw new InvalidDataException("Dokument nie jest obsługiwanym kanałem RSS ani Atom.")
        };
    }

    private static PodcastFeedDocument ParseRss(XElement root, Uri feedUri)
    {
        var channel = root.Elements().FirstOrDefault(element => IsNamed(element, "channel"))
            ?? root.Descendants().FirstOrDefault(element => IsNamed(element, "channel"))
            ?? throw new InvalidDataException("Kanał RSS nie zawiera sekcji channel.");
        var title = TextOf(channel, "title", "Podcast bez nazwy");
        var author = TextOfAny(channel, "author", "managingEditor", "creator");
        var description = DescriptionOfAny(channel, "description", "subtitle", "summary");
        var homepage = FirstUri(channel, feedUri, "link");
        var itemElements = channel.Elements().Any(element => IsNamed(element, "item"))
            ? channel.Elements().Where(element => IsNamed(element, "item"))
            : root.Elements().Where(element => IsNamed(element, "item"));
        var episodes = itemElements
            .Select(element => ParseRssEpisode(
                element,
                feedUri,
                string.IsNullOrWhiteSpace(author) ? title : author))
            .Where(episode => episode is not null)
            .Select(episode => episode!)
            .Take(MaximumEpisodes)
            .ToArray();
        return new PodcastFeedDocument(
            StableId("podcast", feedUri.AbsoluteUri),
            title,
            author,
            description,
            feedUri,
            homepage,
            episodes);
    }

    private static PodcastFeedEpisode? ParseRssEpisode(XElement item, Uri feedUri, string defaultAuthor)
    {
        var enclosure = item.Elements().FirstOrDefault(element =>
            IsNamed(element, "enclosure") && ResolveUri(feedUri, AttributeValue(element, "url")) is not null)
            ?? item.Elements().FirstOrDefault(element =>
                IsNamed(element, "content") && ResolveUri(feedUri, AttributeValue(element, "url")) is not null);
        var mediaUri = enclosure is null
            ? null
            : ResolveUri(feedUri, AttributeValue(enclosure, "url"));
        if (mediaUri is null || !IsHttpUri(mediaUri)) return null;

        var pageUri = FirstUri(item, feedUri, "link");
        var sourceIdentifier = TextOfAny(item, "guid", "id");
        if (string.IsNullOrWhiteSpace(sourceIdentifier))
            sourceIdentifier = mediaUri.AbsoluteUri;
        var title = TextOf(item, "title", "Odcinek bez tytułu");
        var author = TextOfAny(item, "author", "creator");
        if (string.IsNullOrWhiteSpace(author)) author = defaultAuthor;
        var description = DescriptionOfAny(item, "description", "summary", "encoded");
        var duration = ParseDuration(TextOfAny(item, "duration"));
        var chaptersUri = ExternalChaptersUri(item, feedUri);
        var chapters = InlineChapters(item, description, duration);
        return new PodcastFeedEpisode(
            StableId("podcast-episode", $"{feedUri.AbsoluteUri}\n{sourceIdentifier}"),
            sourceIdentifier,
            title,
            author,
            description,
            ParseDate(TextOfAny(item, "pubDate", "published", "updated")),
            duration,
            mediaUri,
            pageUri,
            NormalizeOptional(AttributeValue(enclosure, "type")),
            ParsePositiveLong(AttributeValue(enclosure, "length")),
            chaptersUri,
            chapters);
    }

    private static PodcastFeedDocument ParseAtom(XElement root, Uri feedUri)
    {
        var title = TextOf(root, "title", "Podcast bez nazwy");
        var author = AuthorOf(root);
        var description = DescriptionOfAny(root, "subtitle", "summary");
        var homepage = AtomLink(root, feedUri, "alternate") ?? FirstUri(root, feedUri, "link");
        var feedIdentifier = TextOfAny(root, "id");
        if (string.IsNullOrWhiteSpace(feedIdentifier)) feedIdentifier = feedUri.AbsoluteUri;
        var episodes = root.Elements()
            .Where(element => IsNamed(element, "entry"))
            .Select(element => ParseAtomEpisode(
                element,
                feedUri,
                string.IsNullOrWhiteSpace(author) ? title : author))
            .Where(episode => episode is not null)
            .Select(episode => episode!)
            .Take(MaximumEpisodes)
            .ToArray();
        return new PodcastFeedDocument(
            StableId("podcast", $"{feedUri.AbsoluteUri}\n{feedIdentifier}"),
            title,
            author,
            description,
            feedUri,
            homepage,
            episodes);
    }

    private static PodcastFeedEpisode? ParseAtomEpisode(XElement entry, Uri feedUri, string defaultAuthor)
    {
        var enclosure = entry.Elements().FirstOrDefault(element =>
            IsNamed(element, "link")
            && string.Equals(AttributeValue(element, "rel"), "enclosure", StringComparison.OrdinalIgnoreCase));
        var mediaUri = ResolveUri(feedUri, AttributeValue(enclosure, "href"));
        if (mediaUri is null || !IsHttpUri(mediaUri)) return null;
        var sourceIdentifier = TextOfAny(entry, "id");
        if (string.IsNullOrWhiteSpace(sourceIdentifier)) sourceIdentifier = mediaUri.AbsoluteUri;
        var author = AuthorOf(entry);
        if (string.IsNullOrWhiteSpace(author)) author = defaultAuthor;
        var description = DescriptionOfAny(entry, "summary", "content");
        var duration = ParseDuration(TextOfAny(entry, "duration"));
        var chaptersUri = ExternalChaptersUri(entry, feedUri);
        var chapters = InlineChapters(entry, description, duration);
        return new PodcastFeedEpisode(
            StableId("podcast-episode", $"{feedUri.AbsoluteUri}\n{sourceIdentifier}"),
            sourceIdentifier,
            TextOf(entry, "title", "Odcinek bez tytułu"),
            author,
            description,
            ParseDate(TextOfAny(entry, "published", "updated")),
            duration,
            mediaUri,
            AtomLink(entry, feedUri, "alternate"),
            NormalizeOptional(AttributeValue(enclosure, "type")),
            ParsePositiveLong(AttributeValue(enclosure, "length")),
            chaptersUri,
            chapters);
    }

    private static Uri? ExternalChaptersUri(XElement parent, Uri feedUri)
    {
        foreach (var element in parent.Elements().Where(element => IsNamed(element, "chapters")))
        {
            var type = AttributeValue(element, "type");
            if (!string.Equals(type, "application/json+chapters", StringComparison.OrdinalIgnoreCase)) continue;
            var uri = ResolveUri(feedUri, AttributeValue(element, "url"));
            if (uri is { Scheme: "https" } && string.IsNullOrEmpty(uri.UserInfo)) return uri;
        }
        return null;
    }

    private static IReadOnlyList<ProviderChapterPoint> InlineChapters(
        XElement parent,
        string description,
        TimeSpan duration)
    {
        var container = parent.Elements().FirstOrDefault(element =>
            IsNamed(element, "chapters") && AttributeValue(element, "url") is null);
        if (container is not null)
        {
            var points = container.Elements()
                .Where(element => IsNamed(element, "chapter"))
                .Select((element, index) =>
                {
                    var start = ParseDuration(AttributeValue(element, "start") ?? string.Empty);
                    var title = PodcastJsonChapterParser.NormalizeTitle(AttributeValue(element, "title"));
                    return new ProviderChapterPoint(
                        $"psc:{index + 1}:{start.Ticks}:{title}",
                        title.Length == 0 ? $"Rozdział {index + 1}" : title,
                        start);
                })
                .Where(point => duration <= TimeSpan.Zero || point.Start < duration)
                .ToArray();
            if (points.Length > 0) return PodcastJsonChapterParser.Normalize(points);
        }
        return PodcastDescriptionChapterParser.Parse(description, duration);
    }

    private static string AuthorOf(XElement parent)
    {
        var author = parent.Elements().FirstOrDefault(element => IsNamed(element, "author"));
        return author is null ? string.Empty : TextOfAny(author, "name", "email");
    }

    private static Uri? AtomLink(XElement parent, Uri baseUri, string relation)
    {
        var link = parent.Elements().FirstOrDefault(element =>
            IsNamed(element, "link")
            && string.Equals(AttributeValue(element, "rel") ?? "alternate", relation, StringComparison.OrdinalIgnoreCase));
        return ResolveUri(baseUri, AttributeValue(link, "href"));
    }

    private static Uri? FirstUri(XElement parent, Uri baseUri, string elementName)
    {
        foreach (var element in parent.Elements().Where(element => IsNamed(element, elementName)))
        {
            var uri = ResolveUri(baseUri, element.Value);
            if (uri is not null && IsHttpUri(uri)) return uri;
        }
        return null;
    }

    private static Uri? ResolveUri(Uri baseUri, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Uri.TryCreate(baseUri, WebUtility.HtmlDecode(value.Trim()), out var uri) ? uri : null;
    }

    private static bool IsHttpUri(Uri uri) =>
        uri.IsAbsoluteUri && uri.Scheme is "http" or "https";

    private static string TextOf(XElement parent, string localName, string fallback)
    {
        var value = parent.Elements().FirstOrDefault(element => IsNamed(element, localName))?.Value;
        var normalized = NormalizeText(value);
        return normalized.Length == 0 ? fallback : normalized;
    }

    private static string TextOfAny(XElement parent, params string[] localNames)
    {
        foreach (var localName in localNames)
        {
            var value = parent.Elements().FirstOrDefault(element => IsNamed(element, localName))?.Value;
            var normalized = NormalizeText(value);
            if (normalized.Length > 0) return normalized;
        }
        return string.Empty;
    }

    private static string DescriptionOfAny(XElement parent, params string[] localNames)
    {
        foreach (var localName in localNames)
        {
            var element = parent.Elements().FirstOrDefault(element => IsNamed(element, localName));
            var value = element is null
                ? null
                : element.HasElements
                    ? string.Concat(element.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting)))
                    : element.Value;
            var normalized = NormalizeDescription(value);
            if (normalized.Length > 0) return normalized;
        }
        return string.Empty;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decoded = WebUtility.HtmlDecode(value);
        var withoutMarkup = MarkupPattern().Replace(decoded, " ");
        var normalized = WhitespacePattern().Replace(withoutMarkup, " ").Trim();
        return SpaceBeforePunctuationPattern().Replace(normalized, "$1");
    }

    private static string NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decoded = WebUtility.HtmlDecode(value);
        decoded = AnchorPattern().Replace(decoded, match =>
        {
            var label = NormalizeText(match.Groups[2].Value);
            var address = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
            if (label.Length == 0) return address;
            if (address.Length == 0 || label.Contains(address, StringComparison.OrdinalIgnoreCase)) return label;
            return $"{label}: {address}";
        });
        decoded = BreakPattern().Replace(decoded, Environment.NewLine);
        decoded = MarkupPattern().Replace(decoded, " ");
        decoded = WebUtility.HtmlDecode(decoded);
        return string.Join(
            Environment.NewLine,
            decoded
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => SpaceBeforePunctuationPattern().Replace(
                    WhitespacePattern().Replace(line, " ").Trim(),
                    "$1"))
                .Where(line => line.Length > 0));
    }

    private static string? AttributeValue(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(attribute =>
            string.Equals(attribute.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static bool IsNamed(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;

    private static TimeSpan ParseDuration(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;
        var normalized = value.Trim();
        if (!normalized.Contains(':')
            && double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= TimeSpan.Zero
                ? parsed
                : TimeSpan.Zero;
    }

    private static long? ParsePositiveLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
        && parsed >= 0
            ? parsed
            : null;

    private static string? NormalizeOptional(string? value)
    {
        var normalized = NormalizeText(value);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string StableId(string prefix, string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"{prefix}:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex MarkupPattern();

    [GeneratedRegex("<a\\b[^>]*?href\\s*=\\s*[\\\"']([^\\\"']+)[\\\"'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorPattern();

    [GeneratedRegex("<(?:br\\s*/?|/p|/div|/li|/h[1-6])\\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakPattern();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("\\s+([.,;:!?])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationPattern();
}
