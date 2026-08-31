using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;

namespace AccessibleMediaController.Core.Presentation;

public static class LocalPlaybackAudioSettingsPresentation
{
    public static string FormatEffective(LocalPlaybackAudioSettingsResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var settings = resolution.Settings;
        return $"normalizacja {(settings.LoudnessNormalizationEnabled ? "włączona" : "wyłączona")}, "
            + $"{FormatSource(resolution.LoudnessNormalizationSource)}; "
            + $"przejścia {(settings.SmoothTrackTransitionsEnabled ? "włączone" : "wyłączone")}, "
            + $"{FormatSource(resolution.SmoothTrackTransitionsSource)}; "
            + $"cisza {PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(settings.InterTrackSilenceMilliseconds)}, "
            + FormatSource(resolution.InterTrackSilenceSource);
    }

    private static string FormatSource(LocalPlaybackAudioSettingSource source) => source switch
    {
        LocalPlaybackAudioSettingSource.Item => "ustawienie pliku",
        LocalPlaybackAudioSettingSource.Folder => "ustawienie folderu",
        _ => "ustawienie globalne"
    };
}
