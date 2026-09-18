using System.IO;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private const string SpotifyLibrespotSessionId = "spotifyLibrespot";
    private readonly SpotifyLibrespotMediaOutput _spotifyLibrespotOutput;
    private bool _choosingSpotifyLibrespotDevice;

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
        output.DurationAvailable += (_, e) => StreamingOutput_DurationAvailable(SpotifyLibrespotSessionId, e);
        output.PlaybackPreparing += (_, e) => StreamingOutput_PlaybackPreparing(SpotifyLibrespotSessionId, e);
        output.PlaybackStarted += (_, e) => StreamingOutput_PlaybackStarted(SpotifyLibrespotSessionId, e);
        output.PlaybackEnded += (_, e) => StreamingOutput_PlaybackEnded(SpotifyLibrespotSessionId, e);
        output.PlaybackFailed += (_, e) => StreamingOutput_PlaybackFailed(
            SpotifyLibrespotSessionId, "Spotify — Librespot", output.Position, e);
        return output;
    }

    private void PopulateSpotifyLibrespotCatalog(bool restoreQueue = false)
    {
        var session = _sessions.FindSession(SpotifyLibrespotSessionId);
        if (session is null) return;
        var catalogue = _spotifyItems.Select(item =>
            SpotifySessionItemCopies.ForSession(item, SpotifyLibrespotSessionId)).ToArray();
        var byIdentity = catalogue.Where(item => !string.IsNullOrWhiteSpace(item.ExternalId))
            .GroupBy(item => (item.Kind, item.ExternalId))
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var registered in session.Items)
        {
            if (byIdentity.TryGetValue((registered.Kind, registered.ExternalId), out var current))
            {
                registered.IsFavorite = current.IsFavorite;
                registered.IsInLibrary = current.IsInLibrary;
            }
            else
            {
                registered.IsFavorite = false;
                registered.IsInLibrary = false;
            }
        }
        session.AddItemsById(catalogue);
        if (restoreQueue)
        {
            var saved = _state.RemoteQueues.ItemsBySession.GetValueOrDefault(SpotifyLibrespotSessionId)?
                .Select(item => item.ToMediaItem()).ToArray() ?? [];
            session.AddItemsById(saved);
            RestorePersistedQueueMembership(SpotifyLibrespotSessionId, session.Items);
            EnsureQueueOrder(session);
        }
        RestoreSpotifyRememberedPositions();
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
