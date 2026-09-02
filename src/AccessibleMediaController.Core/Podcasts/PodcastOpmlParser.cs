using System.Xml;
using System.Xml.Linq;

namespace AccessibleMediaController.Core.Podcasts;

public sealed record PodcastOpmlEntry(
    string Title,
    Uri FeedUri,
    Uri? HomepageUri)
{
    public string Label => HomepageUri is null
        ? $"{Title}, {FeedUri.Host}"
        : $"{Title}, {HomepageUri.Host}";

    public override string ToString() => Label;
}

public static class PodcastOpmlParser
{
    public const long MaximumXmlCharacters = 2L * 1024 * 1024;
    public const int MaximumEntries = 1000;

    public static IReadOnlyList<PodcastOpmlEntry> Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
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
        if (!string.Equals(document.Root?.Name.LocalName, "opml", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Plik nie jest dokumentem OPML.");
        }

        var entries = new List<PodcastOpmlEntry>();
        var knownFeeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var outline in document.Descendants().Where(element =>
                     string.Equals(element.Name.LocalName, "outline", StringComparison.OrdinalIgnoreCase)))
        {
            var feedText = AttributeValue(outline, "xmlUrl");
            if (!TryHttpUri(feedText, out var feedUri)
                || !string.IsNullOrEmpty(feedUri.UserInfo)
                || !knownFeeds.Add(feedUri.AbsoluteUri))
            {
                continue;
            }

            var title = AttributeValue(outline, "text")
                ?? AttributeValue(outline, "title")
                ?? feedUri.Host;
            title = NormalizeLabel(title, feedUri.Host);
            var homepage = TryHttpUri(AttributeValue(outline, "htmlUrl"), out var homepageUri)
                && string.IsNullOrEmpty(homepageUri.UserInfo)
                    ? homepageUri
                    : null;
            entries.Add(new PodcastOpmlEntry(title, feedUri, homepage));
            if (entries.Count == MaximumEntries) break;
        }
        return entries;
    }

    private static string? AttributeValue(XElement element, string name) =>
        element.Attributes().FirstOrDefault(attribute =>
            string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

    private static bool TryHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https")
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }

    private static string NormalizeLabel(string? value, string fallback)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length == 0 ? fallback : normalized;
    }
}
