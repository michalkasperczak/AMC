using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    // Keep category rows out of the media catalog, queues, synchronization and
    // persisted collections. Only their navigation IDs belong to view history.
    private bool IsTidalArtistOverview => !_playerViewActive && _sessions.Current.Id == "tidal"
        && _tidalContainerViews.TryGetValue(_currentView, out var view) && view.IsArtistOverview;

    private ArtistBrowseSection? SelectedArtistSection =>
        !_playerViewActive && _sessions.Current.Id == "tidal"
            ? (MediaList.SelectedItem as MediaItemRow)?.ArtistSection
            : null;

    private static string TidalSectionView(MediaItem artist, ArtistBrowseSection? section) =>
        section is null ? TidalContentsView(artist) : $"{TidalContentsView(artist)}:section:{section}";

    private static string TidalArtistSectionRowId(MediaItem artist, ArtistBrowseSection section) =>
        $"artist-category:{artist.ExternalId}:{section}";

    private static List<MediaItemRow> CreateTidalArtistSectionRows(MediaItem artist) =>
        new[] { ArtistBrowseSection.Albums, ArtistBrowseSection.Tracks, ArtistBrowseSection.SimilarArtists }
            .Select(section => new MediaItemRow(
                new MediaItem
                {
                    Id = TidalArtistSectionRowId(artist, section),
                    Title = section.Label(),
                    Kind = MediaItemKind.Folder
                },
                section.Label(), section.Label(), artistSection: section))
            .ToList();

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
        // F5 refreshes the current TIDAL category, never the local library.
        if (commandId == CommandIds.RefreshLocalLibrary && _sessions.Current.Id == "tidal"
            && _tidalContainerViews.TryGetValue(_currentView, out var view))
        {
            _ = OpenTidalContainerAsync(view.Container, view.ArtistSection);
            return true;
        }
        if (!IsTidalArtistOverview) return false;
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
