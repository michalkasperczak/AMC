using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.LocalMedia;

public static class LocalPlaybackAudioSettingsResolver
{
    public static PlaybackAudioSettings Resolve(
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

        return new PlaybackAudioSettings
        {
            LoudnessNormalizationEnabled = itemSettings?.LoudnessNormalizationOverride
                ?? folders.Select(option => option.LoudnessNormalizationOverride)
                    .FirstOrDefault(value => value.HasValue)
                ?? globalSettings.LoudnessNormalizationEnabled,
            SmoothTrackTransitionsEnabled = itemSettings?.SmoothTrackTransitionsOverride
                ?? folders.Select(option => option.SmoothTrackTransitionsOverride)
                    .FirstOrDefault(value => value.HasValue)
                ?? globalSettings.SmoothTrackTransitionsEnabled,
            InterTrackSilenceMilliseconds = itemSettings?.InterTrackSilenceMillisecondsOverride
                ?? folders.Select(option => option.InterTrackSilenceMillisecondsOverride)
                    .FirstOrDefault(value => value.HasValue)
                ?? globalSettings.InterTrackSilenceMilliseconds
        };
    }
}
