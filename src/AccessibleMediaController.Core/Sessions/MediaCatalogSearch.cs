using System.Globalization;

namespace AccessibleMediaController.Core.Sessions;

public static class MediaCatalogSearch
{
    public static IReadOnlyList<MediaSearchResult> Search(
        IEnumerable<DemoMediaSession> sessions,
        string query)
    {
        var words = query.Trim().Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return [];

        var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
        var results = new List<MediaSearchResult>();
        foreach (var session in sessions)
        {
            foreach (var item in session.Items)
            {
                var searchableText = $"{item.Title} {item.Artist} {item.KindLabel} {session.DisplayName}";
                if (words.All(word => compareInfo.IndexOf(
                        searchableText,
                        word,
                        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0))
                {
                    results.Add(new MediaSearchResult(session, item));
                }
            }
        }
        return results;
    }
}

public sealed record MediaSearchResult(DemoMediaSession Session, MediaItem Item);
