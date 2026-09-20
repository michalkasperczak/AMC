using System.Globalization;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
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
        IReadOnlyList<MediaItem> Items,
        ArtistBrowseSection? ArtistSection = null,
        bool IsArtistOverview = false);

    private static string SpotifyContentsView(MediaItem container) =>
        $"{SpotifyContentsViewPrefix}{container.ExternalId}";

    /// <summary>
    /// Nazwa widoku kontenera dla KONKRETNEJ sesji Spotify. Druga sesja
    /// (Librespot) ma wlasny prefiks, inaczej nadpisalaby widok pierwszej.
    /// </summary>
    private static string SpotifyContainerViewName(string sessionId, MediaItem container) =>
        string.Equals(sessionId, "spotify", StringComparison.Ordinal)
            ? SpotifyContentsView(container)
            : $"{SpotifyContentsViewPrefix}{sessionId}:{container.ExternalId}";

    private static bool IsSpotifyContentsView(string viewName) =>
        viewName.StartsWith(SpotifyContentsViewPrefix, StringComparison.Ordinal);

    private static string SpotifyContainerLabel(MediaItemKind kind) => kind switch
    {
        MediaItemKind.Album => "Album Spotify",
        MediaItemKind.Playlist => "Playlista Spotify",
        MediaItemKind.Artist => "Wykonawca Spotify",
        MediaItemKind.Podcast => "Podcast Spotify",
        _ => "Spotify"
    };

    /// <summary>
    /// Czy strzalka w prawo ma co otworzyc. Utwor i odcinek nie sa kontenerami.
    /// </summary>
    private static bool CanOpenSpotifyContainer(MediaItem? item) =>
        item is { Kind: MediaItemKind.Album or MediaItemKind.Playlist or MediaItemKind.Artist
            or MediaItemKind.Podcast }
        && item.ExternalId is { Length: > 0 };

    /// <summary>
    /// Otwiera kontener Spotify. Dla wykonawcy bez wskazanej sekcji pokazuje
    /// PRZEGLAD (kategorie) tym samym mechanizmem co TIDAL - nie od razu albumy,
    /// bo uzytkownik ma wybrac, czy chce wydania czy utwory.
    /// </summary>
    private async Task OpenSpotifyContainerAsync(
        MediaItem container, ArtistBrowseSection? artistSection = null)
    {
        if (!CanOpenSpotifyContainer(container))
        {
            Announce(container.Kind is MediaItemKind.Track or MediaItemKind.Episode
                ? "Enter odtwarza tę pozycję"
                : "Tego elementu Spotify nie można otworzyć");
            return;
        }

        if (container.Kind == MediaItemKind.Artist && artistSection is null)
        {
            OpenSpotifyArtistOverview(container);
            return;
        }

        var label = artistSection?.Label() ?? SpotifyContainerLabel(container.Kind);
        var requestVersion = ++_spotifyNavigationVersion;
        var sessionAtStart = _sessions.Current.Id;
        var viewAtStart = _currentView;
        var itemAtStart = SelectedItem?.Id;

        try
        {
            var loadTask = LoadSpotifyContainerItemsAsync(container, artistSection);
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
                        : artistSection is not null
                            ? $"Spotify nie udostępnia kategorii {label} wykonawcy {container.Title}"
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

            var viewName = StoreSpotifyContainerForSession(sessionAtStart, container, items, artistSection);

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
                Announce(artistSection is not null
                    ? $"Kategoria {label}: Spotify nie zwrócił elementów dla wykonawcy {container.Title}"
                    : $"{label} {container.Title} nie zawiera dostępnych elementów");
                return;
            }

            NavigateTo(viewName);
            PrepareViewFocusContext($"{label}, {container.Title}");
            FocusMediaList();
            // Kategoria "Utwory" idzie przez wyszukiwanie katalogu (top-tracks
            // usuniete w lutym 2026), wiec lista jest PODGLADEM. Przemilczenie
            // tego kazaloby uznac niepelna liste za cala dyskografie.
            if (artistSection == ArtistBrowseSection.Tracks)
            {
                Announce($"Utwory wykonawcy {container.Title}: {items.Count}. "
                    + "Spotify udostępnia wybór z katalogu, nie pełną listę utworów");
            }
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

    /// <summary>
    /// Pokazuje kategorie wykonawcy Spotify (przeglad). Widok przegladu jest
    /// SPISEM kategorii - nie ma w nim elementow multimedialnych, wiec lista
    /// pozycji zostaje pusta i wiersze buduje wspolna fabryka kategorii.
    /// </summary>
    private void OpenSpotifyArtistOverview(MediaItem artist)
    {
        var sessionId = _sessions.Current.Id;
        if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId)) return;
        var view = SpotifyContainerViewName(sessionId, artist);
        _spotifyContainerViews[view] = new SpotifyContainerViewState(artist, [], IsArtistOverview: true);
        NavigateTo(view);
        PrepareViewFocusContext($"Wykonawca, {artist.Title}");
        FocusMediaList();
    }

    private string StoreSpotifyContainerForSession(
        string sessionId,
        MediaItem container,
        IReadOnlyList<MediaItem> items,
        ArtistBrowseSection? artistSection = null)
    {
        if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId))
            throw new ArgumentException("Nieznana sesja Spotify.", nameof(sessionId));
        var session = _sessions.FindSession(sessionId)
            ?? throw new InvalidOperationException("Sesja Spotify nie jest zarejestrowana.");
        var owned = items.Select(item => sessionId == "spotify"
            ? item : SpotifySessionItemCopies.ForSession(item, sessionId)).ToArray();
        var existing = session.Items.GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        for (var index = 0; index < owned.Length; index++)
        {
            if (!existing.TryGetValue(owned[index].Id, out var registered)) continue;
            var incoming = owned[index];
            // Zachowaj obiekt z jego kolejką; dopełnij jedynie pobrane relacje.
            if (!string.IsNullOrWhiteSpace(incoming.RelatedAlbumExternalId))
            {
                registered.RelatedAlbumExternalId = incoming.RelatedAlbumExternalId;
                registered.RelatedAlbumTitle = incoming.RelatedAlbumTitle;
            }
            if (!string.IsNullOrWhiteSpace(incoming.RelatedArtistExternalId))
            {
                registered.RelatedArtistExternalId = incoming.RelatedArtistExternalId;
                registered.RelatedArtistName = incoming.RelatedArtistName;
            }
            owned[index] = registered;
        }
        session.AddItemsById(owned);
        var viewName = SpotifySectionViewName(sessionId, container, artistSection);
        _spotifyContainerViews[viewName] =
            new SpotifyContainerViewState(container, owned, artistSection);
        RestoreSpotifyRememberedPositions();
        return viewName;
    }

    private async Task<IReadOnlyList<MediaItem>?> LoadSpotifyContainerItemsAsync(
        MediaItem container, ArtistBrowseSection? artistSection = null)
    {
        var credentials = await _spotifyIntegration
            .GetPlaybackCredentialsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        using var client = new SpotifyApiClient();
        var id = container.ExternalId ?? string.Empty;
        // Kategoria "Utwory" ma wlasna droge, bo Get Artist's Top Tracks zostalo
        // usuniete (luty 2026) - szczegoly w GetArtistTracksAsync.
        if (container.Kind == MediaItemKind.Artist && artistSection == ArtistBrowseSection.Tracks)
        {
            return await client.GetArtistTracksAsync(
                credentials.AccessToken,
                _spotifyIntegration.CountryCode,
                id,
                container.Title ?? string.Empty,
                CancellationToken.None).ConfigureAwait(false);
        }
        var items = container.Kind switch
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
            MediaItemKind.Podcast => await client
                .GetShowEpisodesAsync(credentials.AccessToken, id, CancellationToken.None)
                .ConfigureAwait(false),
            _ => null
        };
        // Opisy przychodza TYM SAMYM zapytaniem co odcinki. Bez przeniesienia
        // ich z klienta Alt+D na odcinku otwartym z podcastu nie mialby czego
        // przeczytac, mimo ze opis wlasnie przyszedl z Spotify.
        foreach (var pair in client.PodcastDescriptions)
            _spotifyPodcastDescriptions[pair.Key] = pair.Value;
        return items;
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
