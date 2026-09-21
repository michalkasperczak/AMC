using System.Windows;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows;

internal sealed record SessionPlaybackCapabilities(
    bool SupportsAudioProcessing, bool SupportsPlaybackRate,
    bool SupportsPlayerExitPause, bool SupportsResumePosition)
{
    public bool HasOptions => SupportsAudioProcessing || SupportsPlayerExitPause || SupportsResumePosition;
}

/// <summary>Wspólna obsługa opcji sesji. Zmienia wyłącznie przekazane ustawienia, nie aktywną sesję.</summary>
internal static class SessionPlaybackOptionsEditor
{
    internal static SessionPlaybackCapabilities Describe(string? id)
    {
        bool local = string.Equals(id, "local", StringComparison.OrdinalIgnoreCase);
        bool podcasts = string.Equals(id, "podcasts", StringComparison.OrdinalIgnoreCase);
        bool spotify = SpotifyPlaybackSettingsResolver.IsSpotifySession(id);
        bool known = spotify || (id is not null && SessionSlotOrder.DefaultSessionIds.Contains(id, StringComparer.OrdinalIgnoreCase));
        return new(
            // Tylko te dwa wyjścia używają WindowsMediaOutput i jego resolvera DSP.
            SupportsAudioProcessing: local || podcasts,
            // Dialog sesji nie ma zapisu wyboru prędkości. Skróty tempa pozostają osobną funkcją.
            SupportsPlaybackRate: false,
            SupportsPlayerExitPause: known && !string.Equals(id, "wiim", StringComparison.OrdinalIgnoreCase),
            SupportsResumePosition: local || podcasts || spotify);
    }

    internal static ItemPlaybackOptionsWindow CreateDialog(AppSettings settings, string sessionId, string displayName)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var saved = settings.Audio.OverridesBySession.GetValueOrDefault(sessionId);
        var caps = Describe(sessionId);
        var podcasts = string.Equals(sessionId, "podcasts", StringComparison.OrdinalIgnoreCase);
        var dialog = new ItemPlaybackOptionsWindow(
            $"Sesja: {displayName}", ResumePositionPolicy.GetSessionMode(settings, sessionId), null,
            saved?.LoudnessNormalizationOverride, saved?.SmoothTrackTransitionsOverride,
            saved?.InterTrackSilenceMillisecondsOverride, target: ItemPlaybackOptionsTarget.Session,
            pausePlaybackWhenLeavingPlayerOverride: saved?.PausePlaybackWhenLeavingPlayerOverride,
            globalPausePlaybackWhenLeavingPlayer: settings.PausePlaybackWhenLeavingPlayer,
            showPlayerExitPauseOption: caps.SupportsPlayerExitPause,
            showAudioProcessingOptions: caps.SupportsAudioProcessing,
            showPlaybackRateOption: caps.SupportsPlaybackRate,
            // Sesja Podcasty nie czyta znacznika pozycji plikow lokalnych:
            // bez wlasnego wyboru odcinki pamietaja pozycje.
            resumeInheritedLabelOverride: podcasts
                ? "Domyślnie dla podcastów — pamiętaj pozycję odtwarzania"
                : null,
            resumeInheritedHelpText: podcasts
                ? "Dotyczy odcinków w sesji Podcasty i YouTube. Bez własnego wyboru odcinki "
                  + "pamiętają pozycję odtwarzania. Pojedynczy podcast i odcinek mogą to nadpisać."
                : null);
        if (!caps.SupportsResumePosition)
        {
            dialog.ResumeModeLabel.Visibility = Visibility.Collapsed;
            dialog.ResumeModeBox.Visibility = Visibility.Collapsed;
        }
        // Rzeczywisty wybór urządzenia ma własne polecenie Shift+A; ten dialog go nie zapisuje.
        dialog.OutputDeviceLabel.Visibility = Visibility.Collapsed;
        dialog.OutputDeviceBox.Visibility = Visibility.Collapsed;
        dialog.Loaded += (_, _) => new[] { dialog.ResumeModeBox, dialog.LoudnessNormalizationBox, dialog.PlayerExitPauseBox }
            .FirstOrDefault(box => box.IsVisible && box.IsEnabled)?.Focus();
        return dialog;
    }

    internal static SessionPlaybackAudioOverrides Apply(AppSettings settings, string sessionId, ItemPlaybackOptionsWindow dialog)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var saved = settings.Audio.OverridesBySession.GetValueOrDefault(sessionId);
        var caps = Describe(sessionId);
        if (caps.SupportsResumePosition)
            ResumePositionPolicy.SetSessionMode(settings, sessionId, dialog.SelectedResumePositionMode);
        // Niewidoczne pole nie jest decyzją użytkownika: zachowujemy jego wcześniejszą wartość.
        var result = new SessionPlaybackAudioOverrides
        {
            LoudnessNormalizationOverride = caps.SupportsAudioProcessing ? dialog.SelectedLoudnessNormalizationOverride : saved?.LoudnessNormalizationOverride,
            SmoothTrackTransitionsOverride = caps.SupportsAudioProcessing ? dialog.SelectedSmoothTrackTransitionsOverride : saved?.SmoothTrackTransitionsOverride,
            InterTrackSilenceMillisecondsOverride = caps.SupportsAudioProcessing ? dialog.SelectedInterTrackSilenceMillisecondsOverride : saved?.InterTrackSilenceMillisecondsOverride,
            PausePlaybackWhenLeavingPlayerOverride = caps.SupportsPlayerExitPause ? dialog.SelectedPausePlaybackWhenLeavingPlayerOverride : saved?.PausePlaybackWhenLeavingPlayerOverride
        };
        if (result.IsEmpty) settings.Audio.OverridesBySession.Remove(sessionId);
        else settings.Audio.OverridesBySession[sessionId] = result;
        return result;
    }

    internal static string DescribeSession(AppSettings settings, string sessionId, SessionPlaybackAudioOverrides saved)
    {
        var caps = Describe(sessionId);
        var parts = new List<string>();
        if (caps.SupportsAudioProcessing)
        {
            parts.Add("normalizacja " + (saved.LoudnessNormalizationOverride switch { true => "włączona", false => "wyłączona", _ => "według ustawienia ogólnego" }));
            parts.Add($"łagodne przejścia {Choice(saved.SmoothTrackTransitionsOverride)}");
            parts.Add("cisza: " + (saved.InterTrackSilenceMillisecondsOverride is { } value
                ? PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(value) : "według ustawienia ogólnego"));
        }
        if (caps.SupportsResumePosition)
            parts.Add(ResumePositionPolicy.GetSessionMode(settings, sessionId) switch
            {
                ResumePositionMode.Remember => "pozycja pamiętana",
                ResumePositionMode.StartFromBeginning => "zawsze od początku",
                // Podcasty maja wlasny domysl i NIE czytaja znacznika pozycji
                // plikow lokalnych ani ustawienia globalnego.
                _ when string.Equals(sessionId, "podcasts", StringComparison.OrdinalIgnoreCase)
                    => "pozycja domyślnie pamiętana dla podcastów",
                _ => "pozycja według ustawienia ogólnego"
            });
        if (caps.SupportsPlayerExitPause) parts.Add(PlayerExitPausePolicy.DescribeSessionMode(settings, sessionId));
        return string.Join("; ", parts);
    }

    private static string Choice(bool? value) => value switch
    { true => "włączone", false => "wyłączone", _ => "według ustawienia ogólnego" };
}
