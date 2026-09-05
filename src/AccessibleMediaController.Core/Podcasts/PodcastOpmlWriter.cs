using System.Text;
using System.Xml;

namespace AccessibleMediaController.Core.Podcasts;

public static class PodcastOpmlWriter
{
    public static byte[] Write(
        IEnumerable<PodcastOpmlEntry> entries,
        string title = "Podcasty AMC")
    {
        ArgumentNullException.ThrowIfNull(entries);
        var uniqueEntries = entries
            .Where(entry => entry.FeedUri.Scheme is "http" or "https")
            .GroupBy(entry => entry.FeedUri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
            CloseOutput = false
        }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("opml");
            writer.WriteAttributeString("version", "2.0");
            writer.WriteStartElement("head");
            writer.WriteElementString("title", NormalizeTitle(title));
            writer.WriteEndElement();
            writer.WriteStartElement("body");
            foreach (var entry in uniqueEntries)
            {
                var entryTitle = NormalizeTitle(entry.Title);
                writer.WriteStartElement("outline");
                writer.WriteAttributeString("text", entryTitle);
                writer.WriteAttributeString("title", entryTitle);
                writer.WriteAttributeString("type", "rss");
                writer.WriteAttributeString("xmlUrl", entry.FeedUri.AbsoluteUri);
                if (entry.HomepageUri is { } homepage
                    && homepage.Scheme is "http" or "https")
                {
                    writer.WriteAttributeString("htmlUrl", homepage.AbsoluteUri);
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }
        return stream.ToArray();
    }

    private static string NormalizeTitle(string? value)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length == 0 ? "Podcast" : normalized;
    }
}
