using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

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
    long? MediaLength);

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
        if (!IsHttpUri(feedUri))
        {
            throw new ArgumentException("Adres kanału musi używać protokołu HTTP albo HTTPS.", nameof(feedUri));
        }

        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumXmlCharacters,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });
        var document = XDocument.Load(reader, LoadOptions.None);
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
        var description = TextOfAny(channel, "description", "subtitle", "summary");
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
        return new PodcastFeedEpisode(
            StableId("podcast-episode", $"{feedUri.AbsoluteUri}\n{sourceIdentifier}"),
            sourceIdentifier,
            title,
            author,
            TextOfAny(item, "description", "summary", "encoded"),
            ParseDate(TextOfAny(item, "pubDate", "published", "updated")),
            ParseDuration(TextOfAny(item, "duration")),
            mediaUri,
            pageUri,
            NormalizeOptional(AttributeValue(enclosure, "type")),
            ParsePositiveLong(AttributeValue(enclosure, "length")));
    }

    private static PodcastFeedDocument ParseAtom(XElement root, Uri feedUri)
    {
        var title = TextOf(root, "title", "Podcast bez nazwy");
        var author = AuthorOf(root);
        var description = TextOfAny(root, "subtitle", "summary");
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
        return new PodcastFeedEpisode(
            StableId("podcast-episode", $"{feedUri.AbsoluteUri}\n{sourceIdentifier}"),
            sourceIdentifier,
            TextOf(entry, "title", "Odcinek bez tytułu"),
            author,
            TextOfAny(entry, "summary", "content"),
            ParseDate(TextOfAny(entry, "published", "updated")),
            ParseDuration(TextOfAny(entry, "duration")),
            mediaUri,
            AtomLink(entry, feedUri, "alternate"),
            NormalizeOptional(AttributeValue(enclosure, "type")),
            ParsePositiveLong(AttributeValue(enclosure, "length")));
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

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decoded = WebUtility.HtmlDecode(value);
        var withoutMarkup = MarkupPattern().Replace(decoded, " ");
        var normalized = WhitespacePattern().Replace(withoutMarkup, " ").Trim();
        return SpaceBeforePunctuationPattern().Replace(normalized, "$1");
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

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex("\\s+([.,;:!?])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationPattern();
}
