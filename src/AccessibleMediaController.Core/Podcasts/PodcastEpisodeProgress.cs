using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public enum PodcastEpisodeListeningState
{
    New,
    InProgress,
    Unplayed,
    Played
}

public static class PodcastEpisodeProgress
{
    public static readonly TimeSpan StartedThreshold = TimeSpan.FromMinutes(1);

    public static PodcastEpisodeListeningState GetState(PodcastEpisodeSettings episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.IsPlayed) return PodcastEpisodeListeningState.Played;
        if (episode.IsStarted) return PodcastEpisodeListeningState.InProgress;
        return episode.IsNew ? PodcastEpisodeListeningState.New : PodcastEpisodeListeningState.Unplayed;
    }

    public static bool UpdateFromPosition(PodcastEpisodeSettings episode, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.IsPlayed || episode.IsStarted || position < StartedThreshold) return false;
        episode.IsStarted = true;
        episode.IsNew = false;
        return true;
    }

    public static void MarkPlayed(PodcastEpisodeSettings episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        episode.IsNew = false;
        episode.IsStarted = true;
        episode.IsPlayed = true;
    }

    public static void Normalize(PodcastEpisodeSettings episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.IsPlayed)
        {
            episode.IsNew = false;
            episode.IsStarted = true;
            return;
        }
        if (episode.ResumePositionTicks >= StartedThreshold.Ticks)
        {
            episode.IsNew = false;
            episode.IsStarted = true;
        }
    }

    public static string GetLabel(PodcastEpisodeSettings episode) => GetState(episode) switch
    {
        PodcastEpisodeListeningState.New => "nowy",
        PodcastEpisodeListeningState.InProgress => "w trakcie",
        PodcastEpisodeListeningState.Unplayed => "nieodtworzony",
        PodcastEpisodeListeningState.Played => "odtworzony",
        _ => "nieznany"
    };
}
