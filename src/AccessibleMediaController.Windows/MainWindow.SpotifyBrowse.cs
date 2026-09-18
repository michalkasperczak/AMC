using System.Globalization;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Otwieranie zawartosci albumu, playlisty i wykonawcy Spotify (strzalka w prawo).
///
/// Zasady, ktorych trzeba sie tu trzymac:
/// - Widok zawartosci jest CACHE z sieci. Po ponownym uruchomieniu AMC nie moze
///   zostac przywrocony jako zapamietane miejsce, bo lista bylaby pusta pod
///   techniczna nazwa widoku.
/// - Wolna odpowiedz sieci NIE MA prawa przestawic listy, gdy uzytkownik w tym
///   czasie zmienil sesje, widok albo zaznaczenie. Czytnik przeczytalby wtedy
///   cos innego, niz uzytkownik wybral.
/// - Playlisty redakcyjne Spotify i playlisty innych osob sa zamkniete przez
///   Spotify (listopad 2024). Trzeba to powiedziec SPOKOJNIE i konkretnie,
///   a nie jako blad programu.
/// </summary>
public partial class MainWindow
{
    private const string SpotifyContentsViewPrefix = "Spotify:";

    private readonly Dictionary<string, SpotifyContainerViewState> _spotifyContainerViews =
        new(StringComparer.Ordinal);

    private long _spotifyNavigationVersion;

    private sealed record SpotifyContainerViewState(
        MediaItem Container,
        IReadOnlyList<MediaItem> Items);

    private static string SpotifyContentsView(MediaItem container) =>
        $"{SpotifyContentsViewPrefix}{container.ExternalId}";

    private static bool IsSpotifyContentsView(string viewName) =>
        viewName.StartsWith(SpotifyContentsViewPrefix, StringComparison.Ordinal);

    private static string SpotifyContainerLabel(MediaItemKind kind) => kind switch
    {
        MediaItemKind.Album => "Album Spotify",
        MediaItemKind.Playlist => "Playlista Spotify",
        MediaItemKind.Artist => "Wykonawca Spotify",
        _ => "Spotify"
    };

    /// <summary>
    /// Czy strzalka w prawo ma co otworzyc. Utwor i odcinek nie sa kontenerami.
    /// </summary>
    private static bool CanOpenSpotifyContainer(MediaItem? item) =>
        item is { Kind: MediaItemKind.Album or MediaItemKind.Playlist or MediaItemKind.Artist }
        && item.ExternalId is { Length: > 0 };

    private async Task OpenSpotifyContainerAsync(MediaItem container)
    {
        if (!CanOpenSpotifyContainer(container))
        {
            Announce(container.Kind is MediaItemKind.Track or MediaItemKind.Episode
                ? "To utwór. Enter go odtwarza"
                : "Tego elementu Spotify nie można otworzyć");
            return;
        }

        var label = SpotifyContainerLabel(container.Kind);
        var requestVersion = ++_spotifyNavigationVersion;
        var sessionAtStart = _sessions.Current.Id;
        var viewAtStart = _currentView;
        var itemAtStart = SelectedItem?.Id;

        try
        {
            var loadTask = LoadSpotifyContainerItemsAsync(container);
            var progressDelay = Task.Delay(TimeSpan.FromMilliseconds(1400));
            if (await Task.WhenAny(loadTask, progressDelay).ConfigureAwait(true) == progressDelay
                && CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
            {
                Announce(
                    $"Wczytywanie {label.ToLower(CultureInfo.CurrentCulture)}: {container.Title}");
            }
            var items = await loadTask.ConfigureAwait(true);
            if (_isClosing) return;

            // Spotify odmawia zawartosci playlist redakcyjnych i playlist innych
            // osob. To nie awaria - uzytkownik ma wiedziec, ZE tak jest i DLACZEGO.
            if (items is null)
            {
                if (CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
                {
                    Announce(container.Kind == MediaItemKind.Playlist
                        ? $"Spotify nie udostępnia zawartości tej playlisty: {container.Title}. "
                            + "Dotyczy playlist redakcyjnych Spotify i playlist innych osób"
                        : $"Spotify nie udostępnia zawartości: {label}, {container.Title}");
                }
                return;
            }

            foreach (var item in items)
            {
                if (container.Kind == MediaItemKind.Album && item.Kind == MediaItemKind.Track)
                {
                    item.RelatedAlbumExternalId = container.ExternalId;
                    item.RelatedAlbumTitle = container.Title;
                }
                if (container.Kind == MediaItemKind.Artist
                    && string.IsNullOrWhiteSpace(item.RelatedArtistExternalId))
                {
                    item.RelatedArtistExternalId = container.ExternalId;
                    item.RelatedArtistName = container.Title;
                }
            }

            var viewName = SpotifyContentsView(container);
            _spotifyContainerViews[viewName] = new SpotifyContainerViewState(container, items);
            // Bez dopisania do sesji Enter na utworze z tego widoku nie mialby
            // czego odtworzyc: sesja zna tylko wlasne pozycje.
            _sessions.FindSession("spotify")?.AddItemsById(items);

            if (!CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
            {
                DiagnosticLog.Info("spotify-navigation",
                    $"Zachowano dane bez zmiany widoku; żądanie: {requestVersion}; elementów: {items.Count}.");
                return;
            }

            if (items.Count == 0)
            {
                NavigateTo(viewName);
                FocusMediaList();
                Announce($"{label} {container.Title} nie zawiera dostępnych elementów");
                return;
            }

            NavigateTo(viewName);
            PrepareViewFocusContext($"{label}, {container.Title}");
            FocusMediaList();
            DiagnosticLog.Info("spotify-navigation",
                $"Otwarto {container.ExternalId}; elementów: {items.Count}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error(
                "spotify-container",
                $"Nie otwarto elementu {container.ExternalId}.",
                exception);
            if (CanPresentSpotifyResponse(requestVersion, sessionAtStart, viewAtStart, itemAtStart))
                Announce($"Nie udało się otworzyć: {label}, {container.Title}. {exception.Message}");
        }
    }

    private async Task<IReadOnlyList<MediaItem>?> LoadSpotifyContainerItemsAsync(MediaItem container)
    {
        var credentials = await _spotifyIntegration
            .GetPlaybackCredentialsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var client = new SpotifyApiClient();
        var id = container.ExternalId ?? string.Empty;
        return container.Kind switch
        {
            MediaItemKind.Album => await client
                .GetAlbumTracksAsync(credentials.AccessToken, id, CancellationToken.None)
                .ConfigureAwait(false),
            MediaItemKind.Playlist => await client
                .GetPlaylistTracksAsync(credentials.AccessToken, id, CancellationToken.None)
                .ConfigureAwait(false),
            MediaItemKind.Artist => await client
                .GetArtistAlbumsAsync(credentials.AccessToken, id, CancellationToken.None)
                .ConfigureAwait(false),
            _ => null
        };
    }

    /// <summary>
    /// Czy wolno jeszcze przestawic liste. Odpowiedz z sieci, ktora przyszla po
    /// tym, jak uzytkownik zmienil sesje, widok albo zaznaczenie, nie ma prawa
    /// przeniesc fokusu - czytnik przeczytalby nie to, co wybrano.
    /// </summary>
    private bool CanPresentSpotifyResponse(
        long requestVersion,
        string sessionAtStart,
        string viewAtStart,
        string? itemAtStart) =>
        !_isClosing
        && _spotifyNavigationVersion == requestVersion
        && string.Equals(_sessions.Current.Id, sessionAtStart, StringComparison.Ordinal)
        && string.Equals(_currentView, viewAtStart, StringComparison.Ordinal)
        && string.Equals(SelectedItem?.Id, itemAtStart, StringComparison.Ordinal);
}
