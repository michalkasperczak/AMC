using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.LocalMedia;

public enum LocalPlaybackAudioSettingSource
{
    Global,
    Session,
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
    /// <summary>
    /// Kolejnosc waznosci: plik -> folder -> SESJA -> ustawienie ogolne.
    /// Pierwsze ustawienie, ktore cos narzuca, wygrywa; brak wartosci oznacza
    /// zejscie o poziom nizej.
    /// </summary>
    public static PlaybackAudioSettings Resolve(
        PlaybackAudioSettings globalSettings,
        string? itemPath,
        LocalMediaItemSettings? itemSettings,
        IEnumerable<LocalFolderPlaybackSettings>? folderSettings,
        SessionPlaybackAudioOverrides? sessionSettings = null) =>
        ResolveWithSources(globalSettings, itemPath, itemSettings, folderSettings, sessionSettings)
            .Settings;

    public static LocalPlaybackAudioSettingsResolution ResolveWithSources(
        PlaybackAudioSettings globalSettings,
        string? itemPath,
        LocalMediaItemSettings? itemSettings,
        IEnumerable<LocalFolderPlaybackSettings>? folderSettings,
        SessionPlaybackAudioOverrides? sessionSettings = null)
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
                ?? sessionSettings?.LoudnessNormalizationOverride
                ?? globalSettings.LoudnessNormalizationEnabled,
            SmoothTrackTransitionsEnabled = itemSettings?.SmoothTrackTransitionsOverride
                ?? transitionsFolder?.SmoothTrackTransitionsOverride
                ?? sessionSettings?.SmoothTrackTransitionsOverride
                ?? globalSettings.SmoothTrackTransitionsEnabled,
            InterTrackSilenceMilliseconds = itemSettings?.InterTrackSilenceMillisecondsOverride
                ?? silenceFolder?.InterTrackSilenceMillisecondsOverride
                ?? sessionSettings?.InterTrackSilenceMillisecondsOverride
                ?? globalSettings.InterTrackSilenceMilliseconds
        };

        return new LocalPlaybackAudioSettingsResolution(
            settings,
            ResolveSource(
                itemSettings?.LoudnessNormalizationOverride.HasValue == true,
                loudnessFolder is not null,
                sessionSettings?.LoudnessNormalizationOverride.HasValue == true),
            ResolveSource(
                itemSettings?.SmoothTrackTransitionsOverride.HasValue == true,
                transitionsFolder is not null,
                sessionSettings?.SmoothTrackTransitionsOverride.HasValue == true),
            ResolveSource(
                itemSettings?.InterTrackSilenceMillisecondsOverride.HasValue == true,
                silenceFolder is not null,
                sessionSettings?.InterTrackSilenceMillisecondsOverride.HasValue == true));
    }

    private static LocalPlaybackAudioSettingSource ResolveSource(
        bool itemOverride,
        bool folderOverride,
        bool sessionOverride) =>
        itemOverride
            ? LocalPlaybackAudioSettingSource.Item
            : folderOverride
                ? LocalPlaybackAudioSettingSource.Folder
                : sessionOverride
                    ? LocalPlaybackAudioSettingSource.Session
                    : LocalPlaybackAudioSettingSource.Global;
}
