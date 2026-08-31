using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.LocalMedia;

public enum LocalPlaybackAudioSettingSource
{
    Global,
    Folder,
    Item
}

public sealed record LocalPlaybackAudioSettingsResolution(
    PlaybackAudioSettings Settings,
    LocalPlaybackAudioSettingSource LoudnessNormalizationSource,
    LocalPlaybackAudioSettingSource SmoothTrackTransitionsSource,
    LocalPlaybackAudioSettingSource InterTrackSilenceSource);

public static class LocalPlaybackAudioSettingsResolver
{
    public static PlaybackAudioSettings Resolve(
        PlaybackAudioSettings globalSettings,
        string? itemPath,
        LocalMediaItemSettings? itemSettings,
        IEnumerable<LocalFolderPlaybackSettings>? folderSettings) =>
        ResolveWithSources(globalSettings, itemPath, itemSettings, folderSettings).Settings;

    public static LocalPlaybackAudioSettingsResolution ResolveWithSources(
        PlaybackAudioSettings globalSettings,
        string? itemPath,
        LocalMediaItemSettings? itemSettings,
        IEnumerable<LocalFolderPlaybackSettings>? folderSettings)
    {
        ArgumentNullException.ThrowIfNull(globalSettings);

        var folders = string.IsNullOrWhiteSpace(itemPath)
            ? []
            : (folderSettings ?? [])
                .Where(option =>
                    !string.IsNullOrWhiteSpace(option.Path)
                    && LocalFolderSourcePolicy.IsSameOrDescendant(itemPath, option.Path))
                .OrderByDescending(option => option.Path.Length)
                .ToArray();

        var loudnessFolder = folders.FirstOrDefault(option =>
            option.LoudnessNormalizationOverride.HasValue);
        var transitionsFolder = folders.FirstOrDefault(option =>
            option.SmoothTrackTransitionsOverride.HasValue);
        var silenceFolder = folders.FirstOrDefault(option =>
            option.InterTrackSilenceMillisecondsOverride.HasValue);

        var settings = new PlaybackAudioSettings
        {
            LoudnessNormalizationEnabled = itemSettings?.LoudnessNormalizationOverride
                ?? loudnessFolder?.LoudnessNormalizationOverride
                ?? globalSettings.LoudnessNormalizationEnabled,
            SmoothTrackTransitionsEnabled = itemSettings?.SmoothTrackTransitionsOverride
                ?? transitionsFolder?.SmoothTrackTransitionsOverride
                ?? globalSettings.SmoothTrackTransitionsEnabled,
            InterTrackSilenceMilliseconds = itemSettings?.InterTrackSilenceMillisecondsOverride
                ?? silenceFolder?.InterTrackSilenceMillisecondsOverride
                ?? globalSettings.InterTrackSilenceMilliseconds
        };

        return new LocalPlaybackAudioSettingsResolution(
            settings,
            ResolveSource(
                itemSettings?.LoudnessNormalizationOverride.HasValue == true,
                loudnessFolder is not null),
            ResolveSource(
                itemSettings?.SmoothTrackTransitionsOverride.HasValue == true,
                transitionsFolder is not null),
            ResolveSource(
                itemSettings?.InterTrackSilenceMillisecondsOverride.HasValue == true,
                silenceFolder is not null));
    }

    private static LocalPlaybackAudioSettingSource ResolveSource(
        bool itemOverride,
        bool folderOverride) =>
        itemOverride
            ? LocalPlaybackAudioSettingSource.Item
            : folderOverride
                ? LocalPlaybackAudioSettingSource.Folder
                : LocalPlaybackAudioSettingSource.Global;
}
