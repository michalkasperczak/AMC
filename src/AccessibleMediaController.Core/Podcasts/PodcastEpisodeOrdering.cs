using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

/// <summary>
/// Orders one opened podcast, YouTube channel or YouTube playlist. YouTube's
/// flat collection response does not guarantee publication dates, so its own
/// stable entry order must take precedence over an alphabetical fallback.
/// </summary>
public static class PodcastEpisodeOrdering
{
    public static IReadOnlyList<PodcastEpisodeSettings> Order(
        IEnumerable<PodcastEpisodeSettings> source,
        PodcastSourceKind sourceKind)
    {
        var episodes = source.ToArray();
        if (sourceKind is PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist)
        {
            return episodes
                .OrderBy(episode => episode.FeedOrdinal ?? int.MaxValue)
                .ToArray();
        }

        return episodes
            .OrderByDescending(episode => episode.PublishedUtcTicks)
            .ThenBy(episode => episode.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(episode => episode.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
