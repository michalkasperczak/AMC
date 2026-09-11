using System.Windows;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class PlaylistWindow : Window
{
    private readonly string sessionId;
    private readonly string sessionName;
    private readonly string[] selectedItemIds;
    private readonly PlaylistIndex index;
    private List<PlaylistChoice> choices = [];

    public PlaylistWindow(
        string sessionId,
        string sessionName,
        IReadOnlyList<PlaylistEntry> playlists,
        IReadOnlyList<MediaItem> items)
    {
        InitializeComponent();
        this.sessionId = sessionId;
        this.sessionName = sessionName;
        selectedItemIds = items.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToArray();
        var working = new PlaylistSettings
        {
            Entries = playlists.Select(PlaylistIndex.CloneEntry).ToList()
        };
        index = new PlaylistIndex(working);
        DescriptionText.Text = selectedItemIds.Length switch
        {
            0 => $"Zarządzaj playlistami sesji {sessionName}. Insert tworzy playlistę, F2 zmienia nazwę, Delete usuwa.",
            1 => $"Zmień playlisty dla: {items[0].Title}. Spacja zmienia stan, Enter zapisuje.",
            _ => $"Zmień playlisty dla {selectedItemIds.Length} elementów. Stan mieszany oznacza, że playlista zawiera tylko część zaznaczenia."
        };
        RebuildChoices();
        Loaded += (_, _) => PlaylistList.Focus();
    }

    public IReadOnlyList<PlaylistEntry> ResultPlaylists { get; private set; } = [];

    private void RebuildChoices(string? preferredId = null)
    {
        choices = index.GetForSession(sessionId)
            .Select(entry => new PlaylistChoice(
                entry,
                selectedItemIds.Length == 0
                    ? null
                    : index.GetMembership(entry.Id, selectedItemIds) switch
                    {
                        PlaylistMembershipState.All => true,
                        PlaylistMembershipState.None => false,
                        _ => null
                    },
                selectedItemIds.Length > 0))
            .ToList();
        ApplyFilter(preferredId);
    }

    private void ApplyFilter(string? preferredId = null)
    {
        var previousId = preferredId ?? (PlaylistList.SelectedItem as PlaylistChoice)?.Id;
        var filter = FilterBox.Text.Trim();
        var visible = filter.Length == 0
            ? choices
            : choices
                .Where(choice => choice.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        PlaylistList.ItemsSource = visible;
        var preferredIndex = preferredId is null
            ? visible.FindIndex(choice => string.Equals(choice.Id, previousId, StringComparison.Ordinal))
            : visible.FindIndex(choice => string.Equals(choice.Id, preferredId, StringComparison.Ordinal));
        PlaylistList.SelectedIndex = preferredIndex >= 0 ? preferredIndex : visible.Count > 0 ? 0 : -1;
    }

    private void ApplyChoices()
    {
        if (selectedItemIds.Length == 0) return;
        foreach (var choice in choices)
        {
            if (choice.Membership.HasValue)
            {
                index.SetMembership(choice.Id, selectedItemIds, choice.Membership.Value);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ApplyChoices();
        ResultPlaylists = index.CloneSettings().Entries;
        DialogResult = true;
    }

    private void New_Click(object sender, RoutedEventArgs e) => CreatePlaylist();

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameSelectedPlaylist();

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelectedPlaylist();

    private void CreatePlaylist()
    {
        ApplyChoices();
        while (true)
        {
            var dialog = new PlaylistNameWindow("Nowa playlista", string.Empty) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var entry = index.Create(sessionId, dialog.PlaylistName);
                if (selectedItemIds.Length > 0) index.SetMembership(entry.Id, selectedItemIds, true);
                FilterBox.Clear();
                RebuildChoices(entry.Id);
                PlaylistList.Focus();
                return;
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(this, exception.Message, "Nowa playlista", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void RenameSelectedPlaylist()
    {
        if (PlaylistList.SelectedItem is not PlaylistChoice choice) return;
        ApplyChoices();
        while (true)
        {
            var dialog = new PlaylistNameWindow("Zmień nazwę playlisty", choice.Name) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                index.Rename(choice.Id, dialog.PlaylistName);
                FilterBox.Clear();
                RebuildChoices(choice.Id);
                PlaylistList.Focus();
                return;
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(this, exception.Message, "Zmień nazwę playlisty", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void DeleteSelectedPlaylist()
    {
        if (PlaylistList.SelectedItem is not PlaylistChoice choice) return;
        if (MessageBox.Show(
                this,
                $"Usunąć playlistę „{choice.Name}”? Pliki multimedialne pozostaną bez zmian.",
                "Usuń playlistę",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            PlaylistList.Focus();
            return;
        }
        ApplyChoices();
        index.Remove(choice.Id);
        RebuildChoices();
        PlaylistList.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FilterBox.Focus();
            FilterBox.SelectAll();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape
            && Keyboard.Modifiers == ModifierKeys.None
            && FilterBox.Text.Length > 0)
        {
            FilterBox.Clear();
            PlaylistList.Focus();
            e.Handled = true;
            return;
        }
        if ((e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
            || (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control))
        {
            CreatePlaylist();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            RenameSelectedPlaylist();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
        {
            DeleteSelectedPlaylist();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Space
            && Keyboard.Modifiers == ModifierKeys.None
            && PlaylistList.IsKeyboardFocusWithin
            && selectedItemIds.Length > 0
            && PlaylistList.SelectedItem is PlaylistChoice choice)
        {
            choice.Membership = choice.Membership == true ? false : true;
            PlaylistList.Items.Refresh();
            PlaylistStatus.Announce(choice.Membership == true
                ? $"Zaznaczona: {choice.Name}"
                : $"Niezaznaczona: {choice.Name}");
            e.Handled = true;
        }
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private sealed class PlaylistChoice(
        PlaylistEntry entry,
        bool? membership,
        bool showMembership)
    {
        public string Id => entry.Id;
        public string Name => entry.Name;
        public bool? Membership { get; set; } = membership;
        public string Label => showMembership
            ? $"{Name}, {Membership switch { true => "zaznaczona", false => "niezaznaczona", null => "stan mieszany" }}, {FormatCount(entry.ItemIds.Count)}"
            : $"{Name}, {FormatCount(entry.ItemIds.Count)}";

        public override string ToString() => Label;

        private static string FormatCount(int count) => count == 1
            ? "1 element"
            : count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14)
                ? $"{count} elementy"
                : $"{count} elementów";
    }
}
