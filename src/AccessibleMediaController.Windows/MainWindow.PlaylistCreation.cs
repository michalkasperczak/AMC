using System.Windows;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private bool _creatingTidalPlaylist;

    private async Task CreateTidalPlaylistFromListAsync()
    {
        if (_creatingTidalPlaylist || _isClosing) return;
        var dialog = new PlaylistNameWindow("Nowa playlista TIDAL", string.Empty) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }
        _creatingTidalPlaylist = true;
        try
        {
            var playlist = await _tidalIntegration.CreatePlaylistAsync(dialog.PlaylistName, _tidalCancellation.Token);
            if (_isClosing) return;
            var updatedItems = _tidalItems
                .Where(item => !string.Equals(item.ExternalId, playlist.ExternalId, StringComparison.Ordinal))
                .Append(playlist).ToArray();
            ApplyTidalItems(updatedItems, _tidalCollectionOrderSnapshotComplete
                && updatedItems.All(item => item.CollectionAddedUtcTicks is not null));
            QueueStateSave(announceFailure: true);
            // Do not pull the user back from another session or view after a slow request.
            if (_sessions.Current.Id == "tidal" && _currentView == "Playlisty")
            {
                RefreshCurrentView(preferredItemId: playlist.Id);
                Announce($"Utworzono playlistę: {playlist.Title}");
                if (IsActive && !OwnedWindows.OfType<Window>().Any(window => window.IsVisible))
                    RestoreMediaListFocusAfterRefresh();
            }
        }
        catch (OperationCanceledException) when (_tidalCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            DiagnosticLog.Error("tidal-playlist-create", "Nie utworzono playlisty z listy TIDAL.", exception);
            Announce($"Nie udało się utworzyć playlisty TIDAL. {exception.Message}");
        }
        finally { _creatingTidalPlaylist = false; }
    }
}
