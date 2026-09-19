using System.IO;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    /// <summary>
    /// Jedyna sesja Spotify. Host Librespot nie ma wlasnej sesji od czasu
    /// scalenia: jest jednym z dwoch silnikow TEJ sesji, wiec wszystkie
    /// zdarzenia i zapisy ida na kanoniczny identyfikator.
    /// </summary>
    private const string SpotifySessionId = SpotifyPlaybackSettingsResolver.SessionId;

    private readonly SpotifyLibrespotMediaOutput _spotifyLibrespotOutput;

    /// <summary>
    /// Silnik, ktorym sesja Spotify GRA w tym uruchomieniu. Ustalany raz w
    /// konstruktorze okna. Zmiana w Ustawieniach zapisuje sie do pliku i dziala
    /// po restarcie - swiadomie, zeby zapis ustawien nie przerywal odtwarzania.
    /// </summary>
    private readonly SpotifyPlaybackEngine _activeSpotifyEngine;

    private bool _choosingSpotifyLibrespotDevice;

    /// <summary>Czy sesja Spotify gra przez wbudowany host Librespot.</summary>
    private bool SpotifyUsesLibrespotEngine =>
        _activeSpotifyEngine == SpotifyPlaybackEngine.Librespot;

    /// <summary>
    /// Czy BIEZACA sesja to Spotify grajace przez Librespot. Warunki urzadzen i
    /// zdarzen musza pytac o SILNIK, nie tylko o identyfikator sesji: po
    /// scaleniu jeden identyfikator obsluguje oba tory odtwarzania.
    /// </summary>
    private bool CurrentSessionIsLibrespotSpotify =>
        SpotifyUsesLibrespotEngine
        && SpotifyPlaybackSettingsResolver.IsSpotifySession(_sessions.Current.Id);

    private SpotifyLibrespotMediaOutput CreateSpotifyLibrespotOutput()
    {
        var output = new SpotifyLibrespotMediaOutput(
            () => new LibrespotHostClient(
                () => LibrespotHostProcess.Start(Path.Combine(
                    AppContext.BaseDirectory, "LibrespotHost", "amc_spotify_librespot_host.exe")),
                credentialProvider: GetSpotifyLibrespotAccessTokenAsync),
            action =>
            {
                if (_isClosing || Dispatcher.HasShutdownStarted) return;
                Dispatcher.BeginInvoke(() => { if (!_isClosing) action(); });
            },
            _state.Settings.SpotifyLibrespotDeviceName);
        // Zdarzenia hosta trafiaja do KANONICZNEJ sesji Spotify. Adapter zyje
        // takze wtedy, gdy wybrano SDK (nie startuje procesu), wiec kazde
        // zdarzenie jest dodatkowo odsiane po aktywnym silniku - sesja nie moze
        // dostac powiadomienia od toru, ktorym w tym uruchomieniu nie gra.
        output.DurationAvailable += (_, e) =>
        {
            if (SpotifyUsesLibrespotEngine) StreamingOutput_DurationAvailable(SpotifySessionId, e);
        };
        output.PlaybackPreparing += (_, e) =>
        {
            if (SpotifyUsesLibrespotEngine) StreamingOutput_PlaybackPreparing(SpotifySessionId, e);
        };
        output.PlaybackStarted += (_, e) =>
        {
            if (SpotifyUsesLibrespotEngine) StreamingOutput_PlaybackStarted(SpotifySessionId, e);
        };
        output.PlaybackEnded += (_, e) =>
        {
            if (SpotifyUsesLibrespotEngine) StreamingOutput_PlaybackEnded(SpotifySessionId, e);
        };
        output.PlaybackFailed += (_, e) =>
        {
            if (SpotifyUsesLibrespotEngine)
                StreamingOutput_PlaybackFailed(SpotifySessionId, "Spotify", output.Position, e);
        };
        return output;
    }

    private async Task ChooseSpotifyLibrespotDeviceAsync()
    {
        if (_choosingSpotifyLibrespotDevice) return;
        _choosingSpotifyLibrespotDevice = true;
        var session = _sessions.Current;
        try
        {
            var devices = await _spotifyLibrespotOutput.GetOutputDevicesAsync();
            if (_isClosing || _sessions.Current.Id != session.Id) return;
            var choices = new List<AudioOutputDeviceChoice> { new(null, "Domyślne urządzenie systemowe") };
            foreach (var group in devices.GroupBy(device => device.Name, StringComparer.Ordinal))
            {
                var device = group.First();
                var unique = group.Count() == 1;
                var label = device.Name + (unique
                    ? device.IsDefault ? " — obecnie domyślne w Windows" : string.Empty
                    : " — nie można rozróżnić urządzeń o tej samej nazwie");
                choices.Add(new AudioOutputDeviceChoice(device.Name, label, unique));
            }
            var selected = _state.Settings.SpotifyLibrespotDeviceName;
            if (selected is not null && choices.All(choice => choice.Id != selected))
                choices.Add(new AudioOutputDeviceChoice(selected, selected + " — niedostępne", false));
            var dialog = new AudioOutputDeviceWindow(session.DisplayName, selected, choices) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            if (!dialog.SelectedDeviceIsAvailable)
            {
                AnnounceEssential("Nie można użyć tego wyjścia. Wybierz dostępne, jednoznaczne urządzenie.");
                return;
            }
            if (!await _spotifyLibrespotOutput.TrySetOutputDeviceAsync(dialog.SelectedDeviceId)) return;
            _state.Settings.SpotifyLibrespotDeviceName = dialog.SelectedDeviceId;
            QueueStateSave(announceFailure: true);
            AnnounceEssential($"Wyjście {session.DisplayName}: {dialog.SelectedDeviceLabel}");
        }
        catch (Exception exception)
        {
            DiagnosticLog.Warning("spotify-librespot", $"Nie otwarto wyboru wyjścia: {exception.GetType().Name}.");
            AnnounceEssential(exception is LibrespotHostException hostError
                ? SpotifyLibrespotMediaOutput.FriendlyFailure(hostError)
                : "Nie udało się odczytać wyjść dźwięku Librespot. Sprawdź instalację składnika.");
        }
        finally
        {
            _choosingSpotifyLibrespotDevice = false;
            if (!_isClosing && _sessions.Current.Id == session.Id && IsActive) RestoreMediaListFocusAfterRefresh();
        }
    }
}
