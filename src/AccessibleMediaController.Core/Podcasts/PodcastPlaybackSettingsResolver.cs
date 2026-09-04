using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Podcasts;

public static class PodcastPlaybackSettingsResolver
{
    public static bool ShouldRememberPosition(
        PodcastEpisodeSettings? episode,
        PodcastSubscriptionSettings? subscription)
    {
        var mode = episode?.ResumePositionMode ?? ResumePositionMode.Inherit;
        if (mode == ResumePositionMode.Inherit)
            mode = subscription?.ResumePositionMode ?? ResumePositionMode.Inherit;
        return mode != ResumePositionMode.StartFromBeginning;
    }

    public static double? PlaybackRateOverride(
        PodcastEpisodeSettings? episode,
        PodcastSubscriptionSettings? subscription) =>
        episode?.PlaybackRateOverride ?? subscription?.PlaybackRateOverride;

    public static PlaybackAudioSettings ResolveAudio(
        PlaybackAudioSettings global,
        PodcastEpisodeSettings? episode,
        PodcastSubscriptionSettings? subscription) => new()
    {
        LoudnessNormalizationEnabled = episode?.LoudnessNormalizationOverride
            ?? subscription?.LoudnessNormalizationOverride
            ?? global.LoudnessNormalizationEnabled,
        SmoothTrackTransitionsEnabled = episode?.SmoothTrackTransitionsOverride
            ?? subscription?.SmoothTrackTransitionsOverride
            ?? global.SmoothTrackTransitionsEnabled,
        InterTrackSilenceMilliseconds = episode?.InterTrackSilenceMillisecondsOverride
            ?? subscription?.InterTrackSilenceMillisecondsOverride
            ?? global.InterTrackSilenceMilliseconds
    };

    public static string? ConfiguredDownloadFolder(
        string? globalFolder,
        PodcastSubscriptionSettings? subscription) =>
        string.IsNullOrWhiteSpace(subscription?.DownloadsFolder)
            ? globalFolder
            : subscription.DownloadsFolder;
}
