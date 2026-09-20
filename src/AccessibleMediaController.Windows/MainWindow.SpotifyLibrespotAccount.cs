using System.Windows.Threading;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private readonly SpotifyLibrespotAuthenticationService _spotifyLibrespotAuthentication = new();

    private Task<string> GetSpotifyLibrespotAccessTokenAsync(CancellationToken cancellationToken)
        => _spotifyLibrespotAuthentication.GetAccessTokenAsync(cancellationToken);

    private void ShowSpotifyLibrespotAccountManager(SpotifyAccountWindow catalogOwner)
    {
        if (_isClosing) return;
        try
        {
            var dialog = new SpotifyLibrespotAccountWindow(
                () => _spotifyLibrespotAuthentication.HasStoredLogin,
                async (ready, cancellationToken) =>
                {
                    var request = await _spotifyLibrespotAuthentication.RequestPairingAsync(cancellationToken);
                    ready(request.UserCode, request.VerificationUri);
                    await _spotifyLibrespotAuthentication.CompletePairingAsync(request, cancellationToken);
                },
                () =>
                {
                    _sessions.FindSession(SpotifySessionId)?.StopPlayback();
                    _spotifyLibrespotAuthentication.Disconnect();
                }) { Owner = catalogOwner };
            dialog.ShowDialog();
            if (!string.IsNullOrWhiteSpace(dialog.CompletionAnnouncement))
            {
                var announcement = dialog.CompletionAnnouncement;
                Dispatcher.BeginInvoke(() =>
                {
                    if (catalogOwner.IsVisible) catalogOwner.AnnouncePlaybackAccountResult(announcement);
                }, DispatcherPriority.ContextIdle);
            }
        }
        catch (Exception exception)
        {
            // Źródło błędu może być magazynem poświadczeń. Nie zapisujemy surowej treści.
            DiagnosticLog.Warning("spotify-librespot", $"Okno konta niedostępne; typ: {exception.GetType().Name}.");
            catalogOwner.AnnouncePlaybackAccountResult(
                "Nie udało się otworzyć konta Spotify — Librespot. Sprawdź dostęp do Menedżera poświadczeń Windows.");
        }
    }
}
