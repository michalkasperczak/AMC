namespace AccessibleMediaController.Core.Playback;

/// <summary>
/// Optional readback for outputs whose effective rate can differ from the
/// requested rate, for example when a live buffer runs out of delayed audio.
/// Outputs without this contract retain the existing requested-rate behavior.
/// </summary>
public interface IPlaybackRateStateOutput
{
    double PlaybackRate { get; }
}
