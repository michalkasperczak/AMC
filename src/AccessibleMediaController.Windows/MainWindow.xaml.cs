using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow : Window, IAnnouncementSink, IApplicationActions
{
    private PersistedState _state;
    private readonly ConfigurationStore _store;
    private SessionManager _sessions = null!;
    private CommandRouter _router = null!;
    private GlobalPrefixService? _prefixService;
    private string _currentView = "Teraz odtwarzane";
    private List<MediaItemRow> _unfilteredItems = [];
    private readonly Stack<string> _backHistory = [];
    private readonly Stack<string> _forwardHistory = [];
    private bool _initialFocusApplied;
    private bool _deferAnnouncements;
    private string? _deferredAnnouncement;

    public MainWindow(PersistedState state, ConfigurationStore store)
    {
        InitializeComponent();
        _state = state;
        _store = store;
        RebuildCore();
        RefreshCurrentView(false);
    }

    public MediaItem? SelectedItem => (MediaList.SelectedItem as MediaItemRow)?.Item;

    public void Announce(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Announce(message));
            return;
        }
        if (_deferAnnouncements)
        {
            _deferredAnnouncement = message;
            StatusText.Text = message;
            return;
        }
        if (!_state.Settings.Messages.Enabled)
        {
            StatusText.Text = message;
            return;
        }
        StatusText.Announce(message);
    }

    public void ShowCurrentSession(string viewName)
    {
        NavigateTo(viewName, true);
        Activate();
        FocusMediaList();
    }

    public void ShowSessionList()
    {
        var dialog = new SessionSelectionWindow(_sessions, _state.Settings) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedSlot is int slot)
        {
            ExecuteCommand(CommandIds.SessionSlot(slot));
        }
        FocusMediaList();
    }

    public void ShowPlaylistManager()
    {
        var item = SelectedItem ?? _sessions.Current.CurrentItem;
        var dialog = new PlaylistWindow(item) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Announce($"Zapisano zmiany playlist dla: {item.Title}");
        }
    }

    public void ShowItemInformation(bool extended)
    {
        var item = SelectedItem ?? _sessions.Current.CurrentItem;
        var text = $"{item.KindLabel}: {item.Title}\nWykonawca: {item.Artist}\nCzas: {CommandRouter.FormatTime(item.Duration)}";
        if (extended) text += $"\nIdentyfikator demonstracyjny: {item.Id}";
        MessageBox.Show(text, "Informacje o elemencie", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void OpenOfficialApplication()
    {
        Announce($"{_sessions.Current.DisplayName}: otwieranie zewnętrzne nie jest jeszcze połączone");
    }

    public void ShowHelp()
    {
        MessageBox.Show(
            "Domyślny prefiks: Ctrl+Alt+Windows+F12.\n\n" +
            "Po prefiksie: Ctrl+1–3 wybiera sesję, Page Up i Page Down ją zmieniają, " +
            "strzałki sterują czasem i głośnością, Ctrl+E/R/T podaje czas, F otwiera ulubione, " +
            "Shift+F zmienia stan ulubionych, P otwiera playlisty.\n\n" +
            "W aktywnym oknie: Ctrl+1–9 wybiera sesję bez prefiksu, Ctrl+0 otwiera listę sesji, " +
            "Ctrl+Page Up i Ctrl+Page Down zmieniają sesję. " +
            "Ctrl+P/L/Q otwiera odpowiednio: Playlisty, Bibliotekę i Kolejkę. " +
            "Ctrl+N i Ctrl+A pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko.\n\n" +
            "W oknie: Enter wykonuje działanie podstawowe, Alt+Enter pokazuje informacje, " +
            "Delete lub Backspace usuwa z bieżącego widoku, Alt+Strzałka w lewo wraca. " +
            "Escape na głównym przycisku wraca do ostatnio zaznaczonego elementu listy.",
            "Skróty prototypu",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            _prefixService = new GlobalPrefixService(handle, HandleGlobalChord, PrefixActivated);
            RegisterConfiguredPrefix();
        }
        catch (Exception exception)
        {
            Announce($"Globalny prefiks niedostępny: {exception.Message}");
        }
    }

    private void RebuildCore()
    {
        _sessions = new SessionManager(_state.Settings);
        _router = new CommandRouter(_sessions, _state.Settings, this, this);
    }

    private void RegisterConfiguredPrefix()
    {
        if (_prefixService is null) return;
        _prefixService.ConfigureTimeouts(
            _state.Settings.PrefixTimeoutMilliseconds,
            _state.Settings.SessionContinuationMilliseconds);
        _prefixService.RegisterPrefix(KeyChord.Parse(_state.Settings.PrefixChord));
    }

    private void PrefixActivated()
    {
        StatusText.Text = $"Prefiks aktywny. Sesja: {_sessions.Current.DisplayName}";
    }

    private bool HandleGlobalChord(KeyChord chord)
    {
        if (!Dispatcher.CheckAccess()) return Dispatcher.Invoke(() => HandleGlobalChord(chord));
        var profile = ActiveKeyboardProfile();
        var commandId = profile.Resolve(chord);
        if (commandId is null)
        {
            Announce($"Brak polecenia: {chord.Canonical}");
            return false;
        }

        var oldSession = _sessions.Current.Id;
        var result = _router.Execute(commandId);
        if (_sessions.Current.Id != oldSession) RefreshCurrentView(false);
        return result.KeepPrefixActive;
    }

    private KeyboardProfile ActiveKeyboardProfile() =>
        _state.KeyboardProfiles.FirstOrDefault(profile => profile.Id == _state.Settings.ActiveKeyboardProfileId)
        ?? _state.KeyboardProfiles[0];

    private void ExecuteCommand(string commandId)
    {
        var oldSession = _sessions.Current.Id;
        var previousIndex = MediaList.SelectedIndex;
        var restoreListFocus = MediaList.IsKeyboardFocusWithin || Keyboard.FocusedElement is MenuItem;
        var changesListMembership = commandId is CommandIds.ToggleFavorite
            or CommandIds.ToggleLibrary
            or CommandIds.AddQueue
            or CommandIds.TogglePlayNext;
        if (changesListMembership && restoreListFocus) AnchorMediaListFocus();
        if (changesListMembership)
        {
            _deferredAnnouncement = null;
            _deferAnnouncements = true;
        }
        try
        {
            _router.Execute(commandId);
        }
        finally
        {
            _deferAnnouncements = false;
        }
        if (_sessions.Current.Id != oldSession || changesListMembership)
        {
            RefreshCurrentView(false, changesListMembership ? previousIndex : null);
            if (restoreListFocus) RestoreMediaListFocusAfterRefresh();
        }
        if (_deferredAnnouncement is { } announcement)
        {
            _deferredAnnouncement = null;
            Dispatcher.BeginInvoke(
                () => Announce(announcement),
                DispatcherPriority.ContextIdle);
        }
    }

    private void RefreshCurrentView(bool announceSummary, int? fallbackIndex = null)
    {
        var preferredItemId = SelectedItem?.Id;
        SessionHeading.Text = _sessions.Current.DisplayName;
        ViewHeading.Text = _currentView;
        IEnumerable<MediaItem> items = _sessions.Current.Items;
        if (_currentView == "Ulubione") items = items.Where(item => item.IsFavorite);
        if (_currentView == "Biblioteka") items = items.Where(item => item.IsInLibrary);
        if (_currentView == "Kolejka") items = items.Where(item => item.IsInQueue || item.IsPlayNext);
        _unfilteredItems = items.Select(item => new MediaItemRow(item, FormatItem(item))).ToList();
        ApplyFilter(preferredItemId, fallbackIndex);

        if (announceSummary)
        {
            var duration = TimeSpan.FromTicks(_unfilteredItems.Sum(row => row.Item.Duration.Ticks));
            Announce($"{_currentView}. {FormatItemCount(_unfilteredItems.Count)}, {FormatDurationWords(duration)}");
        }
    }

    private void ApplyFilter(string? preferredItemId = null, int? fallbackIndex = null)
    {
        var query = FilterBox.Text.Trim();
        var filteredItems = string.IsNullOrEmpty(query)
            ? _unfilteredItems
            : _unfilteredItems.Where(row =>
                row.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        MediaList.ItemsSource = filteredItems;

        if (filteredItems.Count == 0)
        {
            MediaList.SelectedIndex = -1;
            return;
        }
        var preferredIndex = preferredItemId is null
            ? -1
            : filteredItems.FindIndex(row => row.Item.Id == preferredItemId);
        MediaList.SelectedIndex = preferredIndex >= 0
            ? preferredIndex
            : Math.Clamp(fallbackIndex ?? 0, 0, filteredItems.Count - 1);
    }

    private void FocusMediaList()
    {
        if (MediaList.Items.Count > 0 && MediaList.SelectedIndex < 0) MediaList.SelectedIndex = 0;
        if (MediaList.SelectedItem is not null) MediaList.ScrollIntoView(MediaList.SelectedItem);
        MediaList.UpdateLayout();
        if (MediaList.ItemContainerGenerator.ContainerFromItem(MediaList.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        MediaList.Focus();
        Keyboard.Focus(MediaList);
    }

    private void AnchorMediaListFocus()
    {
        // A focused ListBoxItem is destroyed when ItemsSource is replaced. Moving focus
        // to the parent first prevents WPF from falling through to the filter TextBox.
        MediaList.Focus();
        Keyboard.Focus(MediaList);
    }

    private void RestoreMediaListFocusAfterRefresh()
    {
        FocusMediaList();
        Dispatcher.BeginInvoke(FocusMediaList, DispatcherPriority.Loaded);
    }

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (_initialFocusApplied) return;
        _initialFocusApplied = true;
        if (_state.Settings.StartupTarget == StartupTarget.SessionList)
        {
            Dispatcher.BeginInvoke(ShowSessionList, DispatcherPriority.ContextIdle);
            return;
        }
        Dispatcher.BeginInvoke(FocusMediaList, DispatcherPriority.ContextIdle);
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        if (!_initialFocusApplied) return;
        if (Keyboard.FocusedElement is not null and not Menu and not MenuItem) return;
        Dispatcher.BeginInvoke(FocusMediaList, DispatcherPriority.ContextIdle);
    }

    private void ActivateSelected()
    {
        var item = SelectedItem;
        if (item is null) return;
        if (item.Kind is MediaItemKind.Track or MediaItemKind.Station)
        {
            ExecuteCommand(CommandIds.PlayPause);
            Announce(FormatItem(item));
            return;
        }
        NavigateTo(item.Title, false);
        Announce($"{item.KindLabel}: {item.Title}. {FormatItemCount(_unfilteredItems.Count)}, {FormatDurationWords(item.Duration)}");
    }

    private void RemoveSelected()
    {
        var item = SelectedItem;
        if (item is null) return;
        var previousIndex = MediaList.SelectedIndex;
        AnchorMediaListFocus();

        if (_currentView == "Ulubione") item.IsFavorite = false;
        else if (_currentView == "Biblioteka") item.IsInLibrary = false;
        else if (_currentView == "Kolejka")
        {
            item.IsInQueue = false;
            item.IsPlayNext = false;
        }
        else
        {
            Announce("Usuwanie jest niedostępne w tym widoku");
            return;
        }

        var title = item.Title;
        RefreshCurrentView(false, previousIndex);
        RestoreMediaListFocusAfterRefresh();
        var announcement = MediaList.Items.Count == 0
            ? $"Usunięto: {title}. Lista jest pusta"
            : $"Usunięto: {title}";
        Dispatcher.BeginInvoke(() => Announce(announcement), DispatcherPriority.ContextIdle);
    }

    private void OpenSettings()
    {
        var dialog = new SettingsWindow(_state, _store) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultState is null) return;
        _state = dialog.ResultState;
        _store.Save(_state);
        RebuildCore();
        RefreshCurrentView(false);
        try
        {
            RegisterConfiguredPrefix();
            Announce("Zapisano ustawienia");
        }
        catch (Exception exception)
        {
            Announce($"Ustawienia zapisane, ale prefiks jest niedostępny: {exception.Message}");
        }
    }

    private static string FormatDurationWords(TimeSpan duration)
    {
        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
        return $"{(int)duration.TotalMinutes} min {duration.Seconds} s";
    }

    private static string FormatItemCount(int count)
    {
        if (count == 1) return "1 element";
        var lastTwoDigits = count % 100;
        var lastDigit = count % 10;
        return lastDigit is >= 2 and <= 4 && lastTwoDigits is not (>= 12 and <= 14)
            ? $"{count} elementy"
            : $"{count} elementów";
    }

    private string FormatItem(MediaItem item) =>
        MediaItemFormatter.Format(item, _state.Settings.Lists.FieldOrder);

    private void NavigateTo(string viewName, bool announceSummary)
    {
        if (!string.Equals(viewName, _currentView, StringComparison.Ordinal))
        {
            _backHistory.Push(_currentView);
            _forwardHistory.Clear();
            _currentView = viewName;
        }
        RefreshCurrentView(announceSummary);
    }

    private void NavigateBack()
    {
        if (_backHistory.Count == 0)
        {
            Announce("Brak poprzedniego widoku");
            return;
        }
        _forwardHistory.Push(_currentView);
        _currentView = _backHistory.Pop();
        RefreshCurrentView(true);
    }

    private void NavigateForward()
    {
        if (_forwardHistory.Count == 0)
        {
            Announce("Brak następnego widoku");
            return;
        }
        _backHistory.Push(_currentView);
        _currentView = _forwardHistory.Pop();
        RefreshCurrentView(true);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (TryHandleLocalSessionShortcut(e))
        {
            e.Handled = true;
            return;
        }

        if (TryHandleLocalSessionNavigation(e))
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            if (e.Key == Key.Escape)
            {
                FilterBox.Clear();
                FocusMediaList();
                e.Handled = true;
            }
            else if (e.Key is Key.Enter or Key.Down)
            {
                FocusFilterResults();
                e.Handled = true;
            }
            else if (TryHandleLocalViewShortcut(e))
            {
                e.Handled = true;
            }
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (TryHandleLocalViewShortcut(e))
        {
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None && e.Key == Key.Escape && FilterBox.Text.Length > 0)
        {
            FilterBox.Clear();
            FocusMediaList();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None && e.Key == Key.Escape
                 && Keyboard.FocusedElement is Button)
        {
            FocusMediaList();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            FilterBox.Focus();
            FilterBox.SelectAll();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.OemComma)
        {
            OpenSettings();
            e.Handled = true;
        }
        else if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (MediaList.IsKeyboardFocusWithin
                 && MediaList.Items.Count == 0
                 && modifiers == ModifierKeys.None
                 && (e.Key is Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
        {
            Announce($"{_currentView}: lista jest pusta");
            e.Handled = true;
        }
        else if (!MediaList.IsKeyboardFocusWithin)
        {
            return;
        }
        else if (modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left)
        {
            NavigateBack();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Alt && e.SystemKey == Key.Right)
        {
            NavigateForward();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Alt && e.SystemKey == Key.Enter)
        {
            ShowItemInformation(false);
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.O)
        {
            OpenOfficialApplication();
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.F)
        {
            ExecuteCommand(CommandIds.ToggleFavorite);
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.P)
        {
            ShowPlaylistManager();
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Q)
        {
            ExecuteCommand(CommandIds.AddQueue);
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.L)
        {
            ExecuteCommand(CommandIds.ToggleLibrary);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.C && SelectedItem is not null)
        {
            Clipboard.SetText(SelectedItem.Title);
            Announce("Skopiowano nazwę");
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C && SelectedItem is not null)
        {
            Clipboard.SetText($"demo://{_sessions.Current.Id}/{SelectedItem.Id}");
            Announce("Skopiowano łącze demonstracyjne");
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None && e.Key is Key.Delete or Key.Back)
        {
            RemoveSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (modifiers == ModifierKeys.None) ActivateSelected();
            else if (modifiers == ModifierKeys.Control) ExecuteCommand(CommandIds.PlayPause);
            else if (modifiers == ModifierKeys.Shift) ExecuteCommand(CommandIds.AddQueue);
            else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) ExecuteCommand(CommandIds.TogglePlayNext);
            e.Handled = true;
        }
    }

    private bool TryHandleLocalSessionShortcut(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var isTopRowDigit = key is >= Key.D0 and <= Key.D9;
        var isNumberPadDigit = key is >= Key.NumPad0 and <= Key.NumPad9;
        if (!isTopRowDigit && !isNumberPadDigit) return false;

        var slot = isTopRowDigit
            ? (int)key - (int)Key.D0
            : (int)key - (int)Key.NumPad0;
        ExecuteCommand(slot == 0 ? CommandIds.SessionList : CommandIds.SessionSlot(slot));
        return true;
    }

    private bool TryHandleLocalSessionNavigation(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;
        if (e.Key == Key.PageUp)
        {
            ExecuteCommand(CommandIds.SessionPrevious);
            return true;
        }
        if (e.Key == Key.PageDown)
        {
            ExecuteCommand(CommandIds.SessionNext);
            return true;
        }
        return false;
    }

    private bool TryHandleLocalViewShortcut(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;
        var commandId = e.Key switch
        {
            Key.L => CommandIds.ViewLibrary,
            Key.P => CommandIds.ViewPlaylists,
            Key.Q => CommandIds.ViewQueue,
            _ => null
        };
        if (commandId is null) return false;
        ExecuteCommand(commandId);
        return true;
    }

    private void FocusFilterResults()
    {
        if (MediaList.Items.Count == 0)
        {
            Announce("Brak wyników filtrowania. Zmień tekst lub naciśnij Escape, aby wyczyścić filtr");
            return;
        }
        FocusMediaList();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _prefixService?.Dispose();
        _store.Save(_state);
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter(SelectedItem?.Id);
        StatusText.Text = string.IsNullOrWhiteSpace(FilterBox.Text)
            ? "Gotowy"
            : $"Wyniki filtrowania: {MediaList.Items.Count}";
    }
    private void MediaList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ActivateSelected();
    private void Play_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.PlayPause);
    private void PlayNext_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.TogglePlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.AddQueue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleFavorite);
    private void Playlists_Click(object sender, RoutedEventArgs e) => ShowPlaylistManager();
    private void Information_Click(object sender, RoutedEventArgs e) => ShowItemInformation(false);
    private void OfficialApp_Click(object sender, RoutedEventArgs e) => OpenOfficialApplication();
    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelected();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void Sessions_Click(object sender, RoutedEventArgs e) => ShowSessionList();
    private void MediaContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var item = SelectedItem;
        PlayNextMenuItem.Header = item?.IsPlayNext == true
            ? "Usuń z odtwarzanych jako następne"
            : "Odtwórz jako następne";
        QueueMenuItem.Header = item?.IsInQueue == true
            ? "Usuń z kolejki"
            : "Dodaj do kolejki";
    }
    private void PreviousSession_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SessionPrevious);
    private void NextSession_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SessionNext);
    private void NowPlayingView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewNowPlaying);
    private void FavoritesView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewFavorites);
    private void PlaylistsView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewPlaylists);
    private void LibraryView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewLibrary);
    private void QueueView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewQueue);
    private void AlbumsView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewAlbums);
    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        FilterBox.Focus();
        FilterBox.SelectAll();
    }
    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private async void Updates_Click(object sender, RoutedEventArgs e)
    {
        var result = await new UnconfiguredUpdateService().CheckAsync();
        Announce(result.Error ?? "Brak aktualizacji");
    }

    private sealed record MediaItemRow(MediaItem Item, string Label)
    {
        public override string ToString() => Label;
    }
}
