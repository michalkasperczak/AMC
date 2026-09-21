using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;

namespace AccessibleMediaController.Core.Podcasts;

public static class PodcastPlaybackSettingsResolver
{
    /// <summary>
    /// Kolejnosc rozstrzygania, od najbardziej szczegolowej: odcinek, podcast,
    /// JAWNE ustawienie sesji Podcasty, a na koncu domysl podcastow.
    ///
    /// Domyslem podcastow jest PAMIETANIE pozycji i tak bylo przed dodaniem
    /// opcji odtwarzania sesji. Nie wolno tu siegac do
    /// <see cref="AppSettings.RememberLocalPlaybackPositions"/>: to znacznik
    /// plikow LOKALNYCH, wiec jego wylaczenie cicho przestawialoby odcinki
    /// podcastow na odtwarzanie od poczatku, czego uzytkownik nie wybral.
    /// </summary>
    public static bool ShouldRememberPosition(
        PodcastEpisodeSettings? episode,
        PodcastSubscriptionSettings? subscription,
        AppSettings? settings = null)
    {
        var mode = episode?.ResumePositionMode ?? ResumePositionMode.Inherit;
        if (mode == ResumePositionMode.Inherit)
            mode = subscription?.ResumePositionMode ?? ResumePositionMode.Inherit;
        if (mode == ResumePositionMode.Inherit && settings is not null)
            mode = ResumePositionPolicy.GetSessionMode(settings, "podcasts");
        return mode != ResumePositionMode.StartFromBeginning;
    }

    public static double? PlaybackRateOverride(
        PodcastEpisodeSettings? episode,
        PodcastSubscriptionSettings? subscription) =>
        episode?.PlaybackRateOverride ?? subscription?.PlaybackRateOverride;

    public static PlaybackAudioSettings ResolveAudio(
        PlaybackAudioSettings global,
        PodcastEpisodeSettings? episode,
        PodcastSubscriptionSettings? subscription)
    {
        var session = global.OverridesBySession.GetValueOrDefault("podcasts");
        return new PlaybackAudioSettings
        {
            LoudnessNormalizationEnabled = episode?.LoudnessNormalizationOverride
                ?? subscription?.LoudnessNormalizationOverride
                ?? session?.LoudnessNormalizationOverride
                ?? global.LoudnessNormalizationEnabled,
            SmoothTrackTransitionsEnabled = episode?.SmoothTrackTransitionsOverride
                ?? subscription?.SmoothTrackTransitionsOverride
                ?? session?.SmoothTrackTransitionsOverride
                ?? global.SmoothTrackTransitionsEnabled,
            InterTrackSilenceMilliseconds = episode?.InterTrackSilenceMillisecondsOverride
                ?? subscription?.InterTrackSilenceMillisecondsOverride
                ?? session?.InterTrackSilenceMillisecondsOverride
                ?? global.InterTrackSilenceMilliseconds
        };
    }

    public static string? ConfiguredDownloadFolder(
        string? globalFolder,
        PodcastSubscriptionSettings? subscription) =>
        string.IsNullOrWhiteSpace(subscription?.DownloadsFolder)
            ? globalFolder
            : subscription.DownloadsFolder;
}
