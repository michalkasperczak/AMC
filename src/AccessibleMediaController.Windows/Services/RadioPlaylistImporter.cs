using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ImportedRadioStation(string Name, string StreamUrl);

internal sealed record RadioPlaylistImportResult(
    IReadOnlyList<ImportedRadioStation> Stations,
    int SkippedEntries);

internal static class RadioPlaylistImporter
{
    private const long MaximumPlaylistBytes = 4 * 1024 * 1024;
    private const int MaximumStations = 5_000;
    private const int MaximumStreamUrlLength = 4_096;
    private const int MaximumStationNameLength = 200;

    public static RadioPlaylistImportResult Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Nie znaleziono playlisty radia.", path);
        if (info.Length > MaximumPlaylistBytes)
        {
            throw new InvalidDataException("Playlista jest zbyt duża, aby bezpiecznie ją zaimportować.");
        }

        var text = File.ReadAllText(path, Encoding.UTF8);
        var extension = Path.GetExtension(path);
        IEnumerable<(string? Name, string? Url)> candidates = extension.ToLowerInvariant() switch
        {
            ".pls" => ParsePls(text),
            ".xspf" => ParseXspf(text),
            ".json" => ParseVRadioJson(text),
            _ => ParseM3u(text)
        };

        var stations = new List<ImportedRadioStation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;
        foreach (var (name, url) in candidates.Take(MaximumStations + 1))
        {
            if (stations.Count >= MaximumStations)
            {
                skipped++;
                continue;
            }
            if (!TryNormalizeStreamUrl(url, out var normalizedUrl) || !seen.Add(normalizedUrl))
            {
                skipped++;
                continue;
            }

            stations.Add(new ImportedRadioStation(
                NormalizeName(name, normalizedUrl),
                normalizedUrl));
        }
        return new RadioPlaylistImportResult(stations, skipped);
    }

    private static IEnumerable<(string? Name, string? Url)> ParseM3u(string text)
    {
        if (SplitLines(text).Any(line => line.TrimStart().StartsWith("#EXT-X-", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Wybrany plik jest manifestem jednej transmisji HLS, a nie listą stacji.");
        }
        string? pendingName = null;
        foreach (var rawLine in SplitLines(text))
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var comma = line.IndexOf(',');
                pendingName = comma >= 0 && comma + 1 < line.Length
                    ? line[(comma + 1)..]
                    : null;
                continue;
            }
            if (line.StartsWith('#')) continue;
            yield return (pendingName, line);
            pendingName = null;
        }
    }

    private static IEnumerable<(string? Name, string? Url)> ParsePls(string text)
    {
        var files = new Dictionary<int, string>();
        var titles = new Dictionary<int, string>();
        foreach (var rawLine in SplitLines(text))
        {
            var line = rawLine.Trim();
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (TryReadIndexedKey(key, "File", out var fileIndex)) files[fileIndex] = value;
            else if (TryReadIndexedKey(key, "Title", out var titleIndex)) titles[titleIndex] = value;
        }

        foreach (var (index, url) in files.OrderBy(pair => pair.Key))
        {
            yield return (titles.GetValueOrDefault(index), url);
        }
    }

    private static IEnumerable<(string? Name, string? Url)> ParseXspf(string text)
    {
        using var stringReader = new StringReader(text);
        using var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumPlaylistBytes
        });
        var document = XDocument.Load(xmlReader, LoadOptions.None);
        foreach (var track in document.Descendants().Where(element => element.Name.LocalName == "track"))
        {
            var location = track.Elements().FirstOrDefault(element => element.Name.LocalName == "location")?.Value;
            var title = track.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value;
            yield return (title, location);
        }
    }

    private static IEnumerable<(string? Name, string? Url)> ParseVRadioJson(string text)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 64
        });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("stations", out var stations)
            || stations.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Plik JSON nie zawiera listy stacji VRadio.");
        }

        foreach (var station in stations.EnumerateArray())
        {
            if (station.ValueKind != JsonValueKind.Object) continue;
            var name = TryGetString(station, "name");
            if (!station.TryGetProperty("streams", out var streams)
                || streams.ValueKind != JsonValueKind.Array)
            {
                yield return (name, null);
                continue;
            }

            string? selectedUrl = null;
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.ValueKind != JsonValueKind.Object) continue;
                var candidate = TryGetString(stream, "url");
                if (!TryNormalizeStreamUrl(candidate, out selectedUrl)) continue;
                break;
            }
            yield return (name, selectedUrl);
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryReadIndexedKey(string key, string prefix, out int index)
    {
        index = 0;
        return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key.AsSpan(prefix.Length), out index)
            && index > 0;
    }

    private static bool TryNormalizeStreamUrl(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumStreamUrlLength) return false;
        var trimmed = value.Trim();
        if (trimmed.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))) return false;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }
        normalized = uri.AbsoluteUri;
        return true;
    }

    private static string NormalizeName(string? value, string streamUrl)
    {
        var builder = new StringBuilder();
        var previousWasSpace = false;
        foreach (var character in value ?? string.Empty)
        {
            if (char.IsControl(character) || character == '\uFFFC') continue;
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasSpace) builder.Append(' ');
                previousWasSpace = true;
                continue;
            }
            builder.Append(character);
            previousWasSpace = false;
            if (builder.Length >= MaximumStationNameLength) break;
        }
        var name = builder.ToString().Trim();
        if (name.Length > 0) return name;
        return Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri) && uri.Host.Length > 0
            ? uri.Host
            : "Stacja radiowa";
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
}
