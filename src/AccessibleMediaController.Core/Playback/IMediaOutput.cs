using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Playback;

/// <summary>
/// Platform-neutral boundary between session logic and the component that
/// produces sound. Service adapters and the future AMC.Host can implement the
/// same contract without moving playback code into the WPF interface.
/// </summary>
public interface IMediaOutput
{
    /// <summary>
    /// Identifier of the item currently loaded by the platform output. A
    /// session uses this to distinguish a decoder position from a remembered
    /// position restored before any file has been opened.
    /// </summary>
    string? LoadedItemId { get; }
    TimeSpan Position { get; }
    bool SupportsPlaybackRate { get; }
    void Play(MediaItem item, TimeSpan position, int volume, double playbackRate);
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(int volume);
    void SetPlaybackRate(double playbackRate);
}
