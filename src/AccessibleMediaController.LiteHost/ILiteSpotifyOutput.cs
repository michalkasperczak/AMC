using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// The AMC Spotify player contract. Catalog OAuth stays in LiteSpotifyCatalog.
/// </summary>
internal interface ILiteSpotifyOutput : IDisposable
{
    event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;
    event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;

    TimeSpan Position { get; }
    string? TakeDefaultOutputFallbackNotice();
    void Play(MediaItem item, TimeSpan start, int volume, double rate);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);
    void SetVolume(int volume);
    void SetPlaybackRate(double rate);
    Task<IReadOnlyList<LiteSpotifyChromeDevice>> GetOutputDevicesAsync();
    Task<bool> TrySetOutputDeviceAsync(string? wanted);
}
