using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class TidalPlaylistPickerWindow : Window
{
    private readonly IReadOnlyList<PlaylistChoice> choices;

    public TidalPlaylistPickerWindow(
        IReadOnlyList<MediaItem> playlists,
        IReadOnlyList<MediaItem> items,
        string? preferredPlaylistExternalId = null)
    {
        InitializeComponent();
        choices = playlists
            .Where(playlist => playlist.Kind == MediaItemKind.Playlist
                && playlist.ExternalId is { Length: > 0 })
            .DistinctBy(playlist => playlist.ExternalId, StringComparer.Ordinal)
            .OrderBy(playlist => playlist.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(playlist => new PlaylistChoice(playlist))
            .ToArray();
        DescriptionText.Text = items.Count switch
        {
            1 when items[0].Kind == MediaItemKind.Album =>
                $"Dodaj utwory albumu „{items[0].Title}” do playlisty TIDAL.",
            1 => $"Dodaj „{items[0].Title}” do playlisty TIDAL.",
            _ => $"Dodaj zaznaczone elementy do playlisty TIDAL: {items.Count}."
        };
        PlaylistList.ItemsSource = choices;
        var preferredIndex = string.IsNullOrWhiteSpace(preferredPlaylistExternalId)
            ? -1
            : choices.ToList().FindIndex(choice => string.Equals(
                choice.Playlist.ExternalId,
                preferredPlaylistExternalId,
                StringComparison.Ordinal));
        PlaylistList.SelectedIndex = preferredIndex >= 0
            ? preferredIndex
            : choices.Count > 0 ? 0 : -1;
        Loaded += (_, _) => FocusSelectedPlaylist();
    }

    public IReadOnlyList<MediaItem> SelectedPlaylists { get; private set; } = [];
    public string? RequestedNewPlaylistName { get; private set; }
    public string? LastSelectedPlaylistExternalId { get; private set; }

    private void Add_Click(object sender, RoutedEventArgs e) => Complete();

    private void New_Click(object sender, RoutedEventArgs e) => RequestNewPlaylist();

    private void RequestNewPlaylist()
    {
        var dialog = new PlaylistNameWindow("Nowa playlista TIDAL", string.Empty) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            FocusSelectedPlaylist();
            return;
        }
        RequestedNewPlaylistName = dialog.PlaylistName;
        DialogResult = true;
    }

    private void Complete()
    {
        var selected = PlaylistList.SelectedItems
            .OfType<PlaylistChoice>()
            .OrderBy(choice => PlaylistList.Items.IndexOf(choice))
            .Select(choice => choice.Playlist)
            .ToArray();
        if (selected.Length == 0)
        {
            StatusText.Announce("Wybierz co najmniej jedną playlistę TIDAL albo utwórz nową");
            FocusSelectedPlaylist();
            return;
        }
        SelectedPlaylists = selected;
        LastSelectedPlaylistExternalId = (PlaylistList.SelectedItem as PlaylistChoice)?.Playlist.ExternalId
            ?? selected[0].ExternalId;
        DialogResult = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
            || (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control))
        {
            RequestNewPlaylist();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter
            && Keyboard.Modifiers == ModifierKeys.None
            && PlaylistList.IsKeyboardFocusWithin)
        {
            Complete();
            e.Handled = true;
        }
    }

    private void FocusSelectedPlaylist()
    {
        if (choices.Count == 0)
        {
            NewPlaylistButton.Focus();
            Keyboard.Focus(NewPlaylistButton);
            return;
        }
        if (PlaylistList.SelectedIndex < 0 && choices.Count > 0) PlaylistList.SelectedIndex = 0;
        PlaylistList.ScrollIntoView(PlaylistList.SelectedItem);
        PlaylistList.UpdateLayout();
        if (PlaylistList.ItemContainerGenerator.ContainerFromItem(PlaylistList.SelectedItem)
            is System.Windows.Controls.ListBoxItem row)
        {
            row.Focus();
            Keyboard.Focus(row);
            return;
        }
        PlaylistList.Focus();
        Keyboard.Focus(PlaylistList);
    }

    private sealed class PlaylistChoice
    {
        public PlaylistChoice(MediaItem playlist)
        {
            Playlist = playlist;
            NavigationText = playlist.Title;
            Label = playlist.Title;
        }

        public MediaItem Playlist { get; }
        public string NavigationText { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }
}
