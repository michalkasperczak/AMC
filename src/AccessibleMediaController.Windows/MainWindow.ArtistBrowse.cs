using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Przeglad wykonawcy: JEDEN mechanizm kategorii dla TIDAL i Spotify.
///
/// Wiersz kategorii nie jest elementem multimedialnym - nie wchodzi do katalogu,
/// kolejki, synchronizacji ani zapisanych kolekcji. Do historii widokow trafia
/// wylacznie jego identyfikator nawigacji, zeby powrot (Backspace) wracal na TE
/// SAMA kategorie, a nie na pierwszy wiersz listy.
///
/// Zestaw kategorii zalezy od USLUGI, nie od enuma: TIDAL ma trzy, Spotify dwie.
/// Spotify nie udostepnia juz ani related-artists (listopad 2024), ani
/// top-tracks (luty 2026), wiec martwy wiersz "Podobni wykonawcy" nie moze sie
/// tam pojawic - pokazanie kategorii, ktora zawsze konczy sie komunikatem o
/// braku elementow, czyta sie jak zepsuty program.
/// </summary>
public partial class MainWindow
{
    private static readonly ArtistBrowseSection[] TidalArtistSections =
        [ArtistBrowseSection.Albums, ArtistBrowseSection.Tracks, ArtistBrowseSection.SimilarArtists];

    private static readonly ArtistBrowseSection[] SpotifyArtistSections =
        [ArtistBrowseSection.Albums, ArtistBrowseSection.Tracks];

    private static IReadOnlyList<ArtistBrowseSection> ArtistSectionsForSession(string sessionId) =>
        SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId) ? SpotifyArtistSections
            : string.Equals(sessionId, "tidal", StringComparison.Ordinal) ? TidalArtistSections
            : [];

    /// <summary>Czy biezacy widok to przeglad wykonawcy - w dowolnej usludze.</summary>
    private bool IsArtistOverviewView
    {
        get
        {
            if (_playerViewActive) return false;
            if (string.Equals(_sessions.Current.Id, "tidal", StringComparison.Ordinal))
                return _tidalContainerViews.TryGetValue(_currentView, out var tidal) && tidal.IsArtistOverview;
            if (SpotifyPlaybackSettingsResolver.IsSpotifySession(_sessions.Current.Id))
                return _spotifyContainerViews.TryGetValue(_currentView, out var spotify) && spotify.IsArtistOverview;
            return false;
        }
    }

    private ArtistBrowseSection? SelectedArtistSection =>
        !_playerViewActive && ArtistSectionsForSession(_sessions.Current.Id).Count > 0
            ? (MediaList.SelectedItem as MediaItemRow)?.ArtistSection
            : null;

    private static string TidalSectionView(MediaItem artist, ArtistBrowseSection? section) =>
        AppendSectionSuffix(TidalContentsView(artist), section);

    /// <summary>
    /// Nazwa widoku sekcji Spotify. Prefiks sesji zostaje, bo DRUGA sesja Spotify
    /// (Librespot) ma wlasne widoki - bez tego nadpisalaby widok pierwszej.
    /// </summary>
    private static string SpotifySectionViewName(
        string sessionId, MediaItem container, ArtistBrowseSection? section) =>
        AppendSectionSuffix(SpotifyContainerViewName(sessionId, container), section);

    private static string AppendSectionSuffix(string viewName, ArtistBrowseSection? section) =>
        section is null ? viewName : $"{viewName}:section:{section}";

    private static string ArtistSectionRowId(MediaItem artist, ArtistBrowseSection section) =>
        $"artist-category:{artist.ExternalId}:{section}";

    // Zostawione pod dotychczasowa nazwa: ta sama tresc, uzywana przez TIDAL.
    private static string TidalArtistSectionRowId(MediaItem artist, ArtistBrowseSection section) =>
        ArtistSectionRowId(artist, section);

    private static List<MediaItemRow> CreateArtistSectionRows(
        MediaItem artist, IReadOnlyList<ArtistBrowseSection> sections) =>
        sections
            .Select(section => new MediaItemRow(
                new MediaItem
                {
                    Id = ArtistSectionRowId(artist, section),
                    Title = section.Label(),
                    Kind = MediaItemKind.Folder
                },
                section.Label(), section.Label(), artistSection: section))
            .ToList();

    private static List<MediaItemRow> CreateTidalArtistSectionRows(MediaItem artist) =>
        CreateArtistSectionRows(artist, TidalArtistSections);

    private void OpenTidalArtistOverview(MediaItem artist)
    {
        var view = TidalContentsView(artist);
        _tidalContainerViews[view] = new TidalContainerViewState(artist, [], IsArtistOverview: true);
        NavigateTo(view);
        PrepareViewFocusContext($"Wykonawca, {artist.Title}");
        FocusMediaList();
    }

    private bool TryHandleArtistSectionCommand(string commandId)
    {
        // F5 odswieza BIEZACA kategorie wykonawcy, nigdy lokalnej biblioteki.
        if (commandId == CommandIds.RefreshLocalLibrary)
        {
            if (string.Equals(_sessions.Current.Id, "tidal", StringComparison.Ordinal)
                && _tidalContainerViews.TryGetValue(_currentView, out var tidalView))
            {
                _ = OpenTidalContainerAsync(tidalView.Container, tidalView.ArtistSection);
                return true;
            }
            if (SpotifyPlaybackSettingsResolver.IsSpotifySession(_sessions.Current.Id)
                && _spotifyContainerViews.TryGetValue(_currentView, out var spotifyView)
                && (spotifyView.ArtistSection is not null || spotifyView.IsArtistOverview))
            {
                _ = OpenSpotifyContainerAsync(spotifyView.Container, spotifyView.ArtistSection);
                return true;
            }
        }
        if (!IsArtistOverviewView) return false;
        if (commandId == CommandIds.ActivateSelected)
        {
            ActivateSelected();
            return true;
        }
        if (commandId.StartsWith("action.", StringComparison.Ordinal)
            || commandId is CommandIds.ItemProperties or CommandIds.ItemPlaybackOptions
                or CommandIds.GoToAlbum or CommandIds.GoToArtist or CommandIds.AssignRadioPreset)
        {
            Announce("To kategoria wykonawcy. Otwórz ją Enterem, aby działać na albumach lub utworach");
            return true;
        }
        return false;
    }
}
