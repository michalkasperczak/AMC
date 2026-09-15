using System.Net;
using System.Text;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Sklada tresc okna informacji w dokument HTML. Dzieki temu czytnik ekranu
/// wchodzi w TRYB PRZEGLADANIA - jak na stronie internetowej - i dziala
/// nawigacja po naglowkach, akapitach, laczach oraz znajdowanie tekstu.
///
/// ZGLOSZENIE Michala 15.09.2026: "okienko nawigowalne takie jak w HTML".
///
/// Wejsciem jest CZYSTY TEKST (kanaly podcastow sa juz rozbierane z HTML w
/// PodcastFeedParser), wiec wszystko trzeba tu ucieczkowac - inaczej opis
/// zawierajacy znak mniejszosci rozjechalby dokument.
/// </summary>
internal static class InformationDocument
{
    public static string Build(
        string information,
        IReadOnlyList<InformationLink> links,
        string title)
    {
        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"pl\"><head><meta charset=\"utf-8\">");
        builder.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>").Append(Escape(title)).Append("</title>");
        // Kolory z systemu - okno ma wygladac jak reszta programu takze przy
        // wysokim kontrascie i motywie ciemnym.
        builder.Append("<style>");
        builder.Append("html{color-scheme:light dark}");
        builder.Append("body{font-family:Segoe UI,system-ui,sans-serif;font-size:1rem;");
        builder.Append("line-height:1.5;margin:0;padding:1rem;");
        builder.Append("background:Canvas;color:CanvasText}");
        builder.Append("h1{font-size:1.25rem;margin:0 0 .75rem}");
        builder.Append("h2{font-size:1.1rem;margin:1.25rem 0 .5rem}");
        builder.Append("p{margin:0 0 .75rem;white-space:pre-wrap}");
        builder.Append("a{color:LinkText}");
        builder.Append("ul{margin:0;padding-left:1.5rem}");
        builder.Append("li{margin-bottom:.35rem}");
        builder.Append("</style></head><body>");

        // Naglowek pierwszego poziomu daje skok klawiszem 1 w trybie przegladania.
        builder.Append("<h1>").Append(Escape(title)).Append("</h1>");

        builder.Append("<main>");
        foreach (var paragraph in Paragraphs(information))
        {
            builder.Append("<p>").Append(LinkifyAndEscape(paragraph)).Append("</p>");
        }
        builder.Append("</main>");

        if (links.Count > 0)
        {
            builder.Append("<h2>Łącza</h2><ul>");
            foreach (var link in links)
            {
                builder.Append("<li><a href=\"")
                    .Append(Escape(link.Uri))
                    .Append("\">")
                    .Append(Escape(link.Label))
                    .Append("</a></li>");
            }
            builder.Append("</ul>");
        }

        builder.Append("</body></html>");
        return builder.ToString();
    }

    /// <summary>
    /// Pusta linia konczy akapit. Pojedyncze zlamanie zostaje w akapicie -
    /// stad white-space:pre-wrap w stylu, zeby uklad opisu sie nie zgubil.
    /// </summary>
    internal static IReadOnlyList<string> Paragraphs(string information)
    {
        if (string.IsNullOrWhiteSpace(information)) return ["Brak treści."];
        var akapity = new List<string>();
        var biezacy = new StringBuilder();
        foreach (var line in information.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                if (biezacy.Length > 0)
                {
                    akapity.Add(biezacy.ToString());
                    biezacy.Clear();
                }
                continue;
            }
            if (biezacy.Length > 0) biezacy.Append('\n');
            biezacy.Append(line.TrimEnd());
        }
        if (biezacy.Length > 0) akapity.Add(biezacy.ToString());
        return akapity.Count == 0 ? ["Brak treści."] : akapity;
    }

    /// <summary>
    /// Zamienia adresy w tresci na prawdziwe lacza, resztę ucieczkuje.
    /// Kolejnosc jest istotna: ucieczkujemy KAZDY fragment osobno, wiec
    /// znaczniki, ktore sami tworzymy, nie zostaja zjedzone.
    /// </summary>
    internal static string LinkifyAndEscape(string text)
    {
        var builder = new StringBuilder();
        var ostatni = 0;
        foreach (System.Text.RegularExpressions.Match match in
            System.Text.RegularExpressions.Regex.Matches(
                text,
                "https?://[^\\s<>\"']+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            builder.Append(Escape(text[ostatni..match.Index]));
            var adres = match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']');
            var ogon = match.Value[adres.Length..];
            if (Uri.TryCreate(adres, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            {
                builder.Append("<a href=\"").Append(Escape(adres)).Append("\">")
                    .Append(Escape(adres)).Append("</a>");
            }
            else
            {
                builder.Append(Escape(adres));
            }
            builder.Append(Escape(ogon));
            ostatni = match.Index + match.Length;
        }
        builder.Append(Escape(text[ostatni..]));
        return builder.ToString();
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
