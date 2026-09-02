using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Closes the narrow race between starting a WASAPI output and a concurrent
/// pause request. Some endpoints take longer to start than the default device,
/// so a pause issued during that interval must be repeated after Play().
/// </summary>
internal static class AudioOutputPauseGuard
{
    public static void Play(IWavePlayer? output, Func<bool> shouldRemainPaused)
    {
        ArgumentNullException.ThrowIfNull(shouldRemainPaused);
        if (output is null) return;

        output.Play();
        if (shouldRemainPaused()) output.Pause();
    }
}
