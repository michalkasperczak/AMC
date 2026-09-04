using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public static partial class PodcastJsonChapterParser
{
    public const int MaximumJsonCharacters = 1 * 1024 * 1024;

    public static IReadOnlyList<ProviderChapterPoint> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumJsonCharacters)
            throw new InvalidDataException("Plik rozdziałów podcastu jest zbyt duży.");

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32
        });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("version", out var version)
            || version.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(version.GetString())
            || !root.TryGetProperty("chapters", out var chapters)
            || chapters.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Plik nie ma prawidłowego formatu rozdziałów Podcasting 2.0.");
        }

        var result = new List<ProviderChapterPoint>();
        var number = 0;
        foreach (var chapter in chapters.EnumerateArray())
        {
            if (result.Count >= ChapterIndex.MaximumProviderChaptersPerItem) break;
            number++;
            if (chapter.ValueKind != JsonValueKind.Object
                || !chapter.TryGetProperty("startTime", out var startElement)
                || startElement.ValueKind != JsonValueKind.Number
                || !startElement.TryGetDouble(out var seconds)
                || !double.IsFinite(seconds)
                || seconds < 0
                || seconds > TimeSpan.MaxValue.TotalSeconds)
            {
                continue;
            }
            if (chapter.TryGetProperty("toc", out var toc)
                && toc.ValueKind == JsonValueKind.False)
            {
                continue;
            }

            var title = chapter.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String
                    ? NormalizeTitle(titleElement.GetString())
                    : string.Empty;
            if (title.Length == 0) title = $"Rozdział {number}";
            result.Add(new ProviderChapterPoint(
                $"json:{number}:{seconds.ToString("R", CultureInfo.InvariantCulture)}:{title}",
                title,
                TimeSpan.FromSeconds(seconds)));
        }

        return Normalize(result);
    }

    public static IReadOnlyList<ProviderChapterPoint> Normalize(
        IEnumerable<ProviderChapterPoint> chapters) => chapters
        .Where(chapter => chapter.Start >= TimeSpan.Zero)
        .OrderBy(chapter => chapter.Start)
        .ThenBy(chapter => chapter.Name, StringComparer.CurrentCultureIgnoreCase)
        .GroupBy(chapter => Math.Round(chapter.Start.TotalMilliseconds / 100d))
        .Select(group => group.First())
        .Take(ChapterIndex.MaximumProviderChaptersPerItem)
        .ToArray();

    public static string NormalizeTitle(string? value)
    {
        var normalized = string.Join(' ', (value ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 200 ? normalized : normalized[..200].TrimEnd();
    }
}

public static partial class PodcastDescriptionChapterParser
{
    public static IReadOnlyList<ProviderChapterPoint> Parse(string description, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(description)) return [];
        var result = new List<ProviderChapterPoint>();
        var lineNumber = 0;
        foreach (var line in description.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            lineNumber++;
            var match = ChapterLinePattern().Match(line.Trim());
            if (!match.Success) match = ChapterLineWithTrailingTimePattern().Match(line.Trim());
            if (!match.Success || !TryParseTime(match.Groups["time"].Value, out var start)) continue;
            if (duration > TimeSpan.Zero && start >= duration) continue;
            var title = PodcastJsonChapterParser.NormalizeTitle(match.Groups["title"].Value.Trim(' ', '-', '–', '—', ':', '|'));
            if (title.Length == 0) title = $"Rozdział {result.Count + 1}";
            result.Add(new ProviderChapterPoint($"description:{lineNumber}:{start.Ticks}:{title}", title, start));
        }

        // A single timestamp is commonly just a stray duration or link marker,
        // not a table of contents. Requiring two prevents false chapters.
        return result.Count >= 2 ? PodcastJsonChapterParser.Normalize(result) : [];
    }

    private static bool TryParseTime(string value, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        var parts = value.Split(':');
        if (parts.Length is not (2 or 3)
            || parts.Any(part => !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }
        var numbers = parts.Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToArray();
        var hours = parts.Length == 3 ? numbers[0] : 0;
        var minutes = parts.Length == 3 ? numbers[1] : numbers[0];
        var seconds = numbers[^1];
        if (minutes < 0 || seconds is < 0 or > 59 || (parts.Length == 3 && minutes > 59)) return false;
        try
        {
            time = new TimeSpan(hours, minutes, seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^\s*[\[(]?(?<time>\d{1,3}:\d{2}(?::\d{2})?)[\])]?\s*(?:[-–—:|]\s*)?(?<title>\S.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ChapterLinePattern();

    [GeneratedRegex(@"^\s*(?<title>\S.*?)\s+(?:[-–—:|]\s*)?(?<time>\d{1,3}:\d{2}(?::\d{2})?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ChapterLineWithTrailingTimePattern();
}

/// <summary>
/// Extracts an explicitly labelled chapter list from an episode web page.
/// It intentionally ignores arbitrary timestamps outside a chapter section,
/// because dates, player durations and comment times are not chapters.
/// </summary>
public static partial class PodcastEpisodePageChapterParser
{
    public const int MaximumHtmlCharacters = 2 * 1024 * 1024;

    public static IReadOnlyList<ProviderChapterPoint> Parse(string html, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (html.Length > MaximumHtmlCharacters)
            throw new InvalidDataException("Strona odcinka jest zbyt duża.");
        if (string.IsNullOrWhiteSpace(html)) return [];

        var text = ScriptAndStylePattern().Replace(html, string.Empty);
        text = BlockEndPattern().Replace(text, "\n");
        text = TagPattern().Replace(text, " ");
        text = WebUtility.HtmlDecode(text).Replace('\u00a0', ' ');
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0)
            .ToArray();

        if (!TryFindChapterSection(lines, out var marker, out var firstLine)) return [];
        var sectionLines = new List<string>(501);
        if (!string.IsNullOrWhiteSpace(firstLine)) sectionLines.Add(firstLine);
        sectionLines.AddRange(lines.Skip(marker + 1).Take(500));
        var section = string.Join(Environment.NewLine, sectionLines);
        return PodcastDescriptionChapterParser.Parse(section, duration);
    }

    private static bool TryFindChapterSection(
        IReadOnlyList<string> lines,
        out int marker,
        out string firstLine)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var match = ChapterHeadingPattern().Match(lines[index]);
            if (!match.Success) continue;
            marker = index;
            firstLine = lines[index][(match.Index + match.Length)..].Trim();
            return true;
        }
        marker = -1;
        firstLine = string.Empty;
        return false;
    }

    [GeneratedRegex(@"<(script|style|noscript)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptAndStylePattern();

    [GeneratedRegex(@"<\s*br\s*/?\s*>|</\s*(?:p|div|li|tr|h[1-6]|section|article)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockEndPattern();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"(?:^|\s)(?:znaczniki\s+czasu|spis\s+rozdziałów|rozdziały|chapters|chapter\s+list)(?:\s*:\s*|\s*$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChapterHeadingPattern();
}
