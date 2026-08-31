using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Playback;

[Flags]
public enum PlaybackAudioProcessingCapabilities
{
    None = 0,
    LoudnessNormalization = 1,
    SmoothTrackTransitions = 2,
    InterTrackSilence = 4,
    All = LoudnessNormalization | SmoothTrackTransitions | InterTrackSilence
}

/// <summary>
/// Optional playback-output capability. A service adapter implements this
/// interface only when it can apply the settings in its own audio path or
/// through an official service/device API.
/// </summary>
public interface IPlaybackAudioProcessingOutput
{
    PlaybackAudioProcessingCapabilities AudioProcessingCapabilities { get; }
    void ConfigureAudioProcessing(PlaybackAudioSettings settings);
}
