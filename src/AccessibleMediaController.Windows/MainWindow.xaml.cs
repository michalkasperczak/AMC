using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Updates;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class MainWindow : AccessibleWindow, IAnnouncementSink, IApplicationActions
{
    private PersistedState _state;
    private readonly ConfigurationStore _store;
    private PlaybackHistory _playbackHistory;
    private BookmarkIndex _bookmarkIndex;
    private SessionManager _sessions = null!;
    private CommandRouter _router = null!;
    private GlobalPrefixService? _prefixService;
    private const string DefaultBrowserView = "Multimedia";
    private const string PlayerViewName = "Teraz odtwarzane";
    private const string BookmarkViewName = "Zakładki";
    private const string FolderViewName = "Foldery";
    private const string AllLocalFilesViewName = "Wszystkie pliki";
    private string _currentView = DefaultBrowserView;
    private List<MediaItemRow> _unfilteredItems = [];
    private readonly Dictionary<string, SessionViewHistory> _viewHistories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlaybackHistoryCursor> _playbackHistoryCursors =
        new(StringComparer.OrdinalIgnoreCase);
    private BookmarkNavigationCursor? _bookmarkNavigationCursor;
    private BookmarkReturnContext? _bookmarkReturnContext;
    private readonly MediaMembershipHistory _membershipHistory = new();
    private readonly Stack<LocalCatalogUndo> _localCatalogHistory = [];
    private long _undoSequence;
    private bool _initialFocusApplied;
    private bool _deferAnnouncements;
    private string? _deferredAnnouncement;
    private bool _captureAnnouncements;
    private string? _capturedAnnouncement;
    private HwndSource? _windowSource;
    private string _typeAheadText = string.Empty;
    private DateTime _lastTypeAheadInputUtc;
    private string? _focusContextItemId;
    private string? _focusContextPrefix;
    private ListBoxItem? _focusContextContainer;
    private readonly WindowsMediaOutput _localOutput = new();
    private readonly List<MediaItem> _localItems = [];
    private readonly Dictionary<string, string> _pendingExternalMoves =
        new(StringComparer.Ordinal);
    private readonly DispatcherTimer _playerUiTimer;
    private readonly DispatcherTimer _localSourceSyncTimer;
    private readonly Dictionary<string, FileSystemWatcher> _localSourceWatchers =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _localSourceSyncInProgress;
    private bool _localSourceSyncPending;
    private bool _isClosing;
    private bool _playerViewActive;
    private bool _restoringSessionNavigation;
    private string? _playerFocusContextPrefix;
    private DateTime _lastLocalStateSaveUtc;
    private long _lastSavedLocalPositionTicks = -1;
    private readonly System.Windows.Forms.StatusStrip _playbackStatusBar;
    private readonly System.Windows.Forms.ToolStripStatusLabel _playbackStatusLabel;

    private const int WmKeyDown = 0x0100;
    private const int VirtualKeyE = 0x45;
    private const int VirtualKeyG = 0x47;
    private const int VirtualKeyR = 0x52;
    private const int VirtualKeyT = 0x54;
    private const int VirtualKeyZ = 0x5A;
    private static readonly TimeSpan TypeAheadTimeout = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan BookmarkNavigationContinuationWindow = TimeSpan.FromSeconds(5);
    private static readonly string AppDisplayVersion =
        typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? typeof(MainWindow).Assembly.GetName().Version?.ToString()
        ?? "wersja nieznana";

    public MainWindow(PersistedState state, ConfigurationStore store)
    {
        InitializeComponent();
        const string initialStatus = "pauza, 0:00";
        _playbackStatusLabel = new System.Windows.Forms.ToolStripStatusLabel
        {
            AccessibleName = initialStatus,
            AccessibleRole = System.Windows.Forms.AccessibleRole.StaticText,
            Spring = true,
            Text = initialStatus,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };
        _playbackStatusBar = new System.Windows.Forms.StatusStrip
        {
            AccessibleRole = System.Windows.Forms.AccessibleRole.StatusBar,
            AutoSize = false,
            CanOverflow = false,
            Dock = System.Windows.Forms.DockStyle.Fill,
            GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden,
            SizingGrip = false,
            TabStop = false
        };
        _playbackStatusBar.Items.Add(_playbackStatusLabel);
        PlaybackStatusHost.Child = _playbackStatusBar;
        _playerUiTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _playerUiTimer.Tick += PlayerUiTimer_Tick;
        _localSourceSyncTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _localSourceSyncTimer.Tick += LocalSourceSyncTimer_Tick;
        _state = state;
        _store = store;
        NormalizeTransientBookmarkViewsAtStartup();
        NormalizeLocalLibraryNavigationAtStartup();
        _playbackHistory = new PlaybackHistory(_state.PlaybackHistory);
        _bookmarkIndex = new BookmarkIndex(_state.Bookmarks);
        LoadPersistedLocalMedia();
        _localOutput.DurationAvailable += LocalOutput_DurationAvailable;
        _localOutput.PlaybackFailed += LocalOutput_PlaybackFailed;
        _localOutput.PlaybackEnded += LocalOutput_PlaybackEnded;
        ApplyDetailedHints();
        RebuildCore();
        RestoreCurrentSessionNavigationState();
        UpdatePlaybackStatusBar();
        _playerUiTimer.Start();
    }

    private void NormalizeLocalLibraryNavigationAtStartup()
    {
        if (!_state.SessionNavigation.Sessions.TryGetValue("local", out var navigation))
        {
            navigation = new SessionNavigationState
            {
                CurrentView = _state.LocalMedia.LibraryView
            };
            _state.SessionNavigation.Sessions["local"] = navigation;
        }
        if (navigation.CurrentView is DefaultBrowserView or "Biblioteka")
        {
            navigation.CurrentView = _state.LocalMedia.LibraryView;
        }
    }

    private void NormalizeTransientBookmarkViewsAtStartup()
    {
        foreach (var navigation in _state.SessionNavigation.Sessions.Values)
        {
            if (string.Equals(navigation.CurrentView, BookmarkViewName, StringComparison.Ordinal))
            {
                navigation.CurrentView = DefaultBrowserView;
            }
        }
    }

    public MediaItem? SelectedItem => (MediaList.SelectedItem as MediaItemRow)?.Item;
    private BookmarkEntry? SelectedBookmark => (MediaList.SelectedItem as MediaItemRow)?.Bookmark;
    public MediaItem? ActionItem => _playerViewActive
        ? (_sessions.Current.HasItems ? _sessions.Current.CurrentItem : null)
        : (MediaList.SelectedItem as MediaItemRow)?.ActionItem
            ?? (_sessions.Current.HasItems ? _sessions.Current.CurrentItem : null);
    private DemoMediaSession ActionSession => !_playerViewActive && SelectedBookmark is { } bookmark
        ? _sessions.FindSession(bookmark.SessionId) ?? _sessions.Current
        : _sessions.Current;
    public IReadOnlyList<MediaItem> ActionItems
    {
        get
        {
            if (_playerViewActive) return [_sessions.Current.CurrentItem];
            var selected = MediaList.SelectedItems
                .OfType<MediaItemRow>()
                .Select(row => row.ActionItem)
                .Distinct()
                .ToArray();
            if (selected.Length > 0) return selected;
            return ActionItem is { } actionItem ? [actionItem] : [];
        }
    }

    public void Announce(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Announce(message));
            return;
        }
        if (_captureAnnouncements)
        {
            if (_state.Settings.Messages.Enabled) _capturedAnnouncement = message;
            return;
        }
        if (_deferAnnouncements)
        {
            _deferredAnnouncement = message;
            return;
        }
        if (!_state.Settings.Messages.Enabled)
        {
            StatusText.Text = message;
            return;
        }
        StatusText.Announce(message);
    }

    private void AnnounceEssential(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AnnounceEssential(message));
            return;
        }
        StatusText.Announce(message);
    }

    public void ShowCurrentSession(string viewName)
    {
        if (string.Equals(viewName, PlayerViewName, StringComparison.Ordinal))
        {
            ShowPlayerView();
            return;
        }

        if (IsSearchView(viewName))
        {
            if (_playerViewActive)
            {
                Announce("Wyszukiwanie jest dostępne na listach");
                return;
            }
            ShowSearch(string.Equals(viewName, "Szukaj we wszystkich usługach", StringComparison.Ordinal));
            return;
        }

        if (string.Equals(viewName, "Biblioteka", StringComparison.Ordinal)
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            viewName = _state.LocalMedia.LibraryView;
        }

        if ((viewName is FolderViewName or AllLocalFilesViewName)
            && !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            var local = _sessions.FindSession("local");
            if (local is null)
            {
                Announce("Foldery są dostępne po otwarciu lokalnego folderu z plikami audio");
                return;
            }
            CaptureCurrentSessionNavigationState();
            _sessions.SelectSession(local.Id);
            RestoreCurrentSessionNavigationState();
        }

        if (viewName is FolderViewName or AllLocalFilesViewName)
        {
            _state.LocalMedia.LibraryView = viewName;
        }

        if (string.Equals(viewName, BookmarkViewName, StringComparison.Ordinal)
            && !string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            _bookmarkReturnContext = new BookmarkReturnContext(
                _sessions.Current.Id,
                _currentView,
                SelectedItem?.Id);
        }
        NavigateTo(viewName);
        PrepareViewFocusContext(viewName);
        Activate();
        FocusMediaList();
    }

    private void ShowSearch(bool allServices)
    {
        ClearFocusContext();
        Activate();
        var sessionBeforeSearch = _sessions.Current.Id;
        var searchHistory = new SearchQueryHistory(_state.SearchHistory);
        var searchHistoryScope = allServices
            ? SearchQueryHistory.GlobalScope
            : _sessions.Current.Id;
        var dialog = new SearchWindow(
            _sessions,
            allServices,
            FormatItem,
            BuildQuickMediaInformation,
            ExecuteSearchResultAction,
            searchHistory,
            searchHistoryScope,
            () => _store.Save(_state),
            _state.Settings.Messages.DetailedHints)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.SelectedResult is { } result)
        {
            SelectSessionBrowserItem(result.SessionId, result.Item.Id);
            if (allServices) PrepareSearchReturnContext(result.Item.Id);
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        if (allServices && dialog.LastDirectActionResult is { } lastDirectResult)
        {
            SelectSessionBrowserItem(lastDirectResult.SessionId, lastDirectResult.Item.Id);
            PrepareSearchReturnContext(lastDirectResult.Item.Id);
        }
        else if (allServices && _sessions.Current.Id != sessionBeforeSearch)
        {
            PrepareSearchReturnContext((SelectedItem ?? _sessions.Current.CurrentItem).Id);
        }
        RestoreMediaListFocusAfterRefresh();
    }

    private DemoMediaSession? SelectSessionBrowserItem(string sessionId, string itemId)
    {
        CaptureCurrentSessionNavigationState();
        var session = _sessions.SelectSession(sessionId);
        if (session is null) return null;

        var navigation = GetSessionNavigationState(session.Id);
        var targetView = string.Equals(session.Id, "local", StringComparison.Ordinal)
            ? _state.LocalMedia.LibraryView
            : DefaultBrowserView;
        if (string.Equals(targetView, FolderViewName, StringComparison.Ordinal))
        {
            var targetItem = session.Items.FirstOrDefault(item =>
                string.Equals(item.Id, itemId, StringComparison.Ordinal));
            if (targetItem?.Source is { Length: > 0 } path)
            {
                _state.LocalMedia.CurrentFolderPath = Path.GetDirectoryName(path);
            }
        }
        navigation.CurrentView = targetView;
        navigation.PlayerActive = false;
        navigation.SelectedItemIds[targetView] = itemId;
        _currentView = targetView;
        _playerViewActive = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
        BrowserHeaderPanel.Visibility = Visibility.Visible;
        BrowserActionPanel.Visibility = Visibility.Visible;
        MediaList.Visibility = Visibility.Visible;
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: itemId);
        SelectMediaItem(itemId);
        return session;
    }

    public void ShowFilter()
    {
        if (_playerViewActive)
        {
            Announce("Wyszukiwanie jest dostępne na listach");
            return;
        }
        Activate();
        FocusFilter();
    }

    public void AddBookmark()
    {
        if (!_playerViewActive)
        {
            Announce("Zakładkę można dodać w otwartym odtwarzaczu");
            return;
        }

        var session = ActionSession;
        var item = session.CurrentItem;
        if (item.Duration <= TimeSpan.Zero)
        {
            Announce("Nie można dodać zakładki: czas trwania materiału jest nieznany");
            return;
        }

        var result = _bookmarkIndex.Add(
            session.Id,
            session.DisplayName,
            item,
            session.Position,
            DateTime.UtcNow);
        _store.Save(_state);
        var time = CommandRouter.FormatTime(TimeSpan.FromTicks(result.Entry.PositionTicks));
        Announce(result.Added
            ? $"Dodano zakładkę: {time}"
            : $"Zakładka już istnieje: {time}");
    }

    public void AddNamedBookmark()
    {
        if (!_playerViewActive)
        {
            Announce("Nazwaną zakładkę można dodać w otwartym odtwarzaczu");
            return;
        }

        var session = ActionSession;
        var item = session.CurrentItem;
        if (item.Duration <= TimeSpan.Zero)
        {
            Announce("Nie można dodać zakładki: czas trwania materiału jest nieznany");
            return;
        }

        var position = session.Position;
        var dialog = new BookmarkNameWindow(item.Title, position) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var result = _bookmarkIndex.Add(
            session.Id,
            session.DisplayName,
            item,
            position,
            DateTime.UtcNow,
            dialog.BookmarkName);
        _store.Save(_state);
        var time = CommandRouter.FormatTime(TimeSpan.FromTicks(result.Entry.PositionTicks));
        Announce(result.Added
            ? $"Dodano zakładkę {result.Entry.Name}: {time}"
            : result.NameChanged
                ? $"Nadano nazwę zakładce {result.Entry.Name}: {time}"
                : $"Zakładka {result.Entry.Name} już istnieje: {time}");
    }

    public void NavigateBookmark(int direction)
    {
        if (!_playerViewActive)
        {
            Announce("Nawigacja po zakładkach działa w otwartym odtwarzaczu");
            return;
        }

        var session = _sessions.Current;
        var itemId = session.CurrentItem.Id;
        BookmarkEntry? bookmark = null;
        var now = DateTime.UtcNow;
        if (_bookmarkNavigationCursor is { } cursor
            && string.Equals(cursor.SessionId, session.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(cursor.ItemId, itemId, StringComparison.Ordinal)
            && now - cursor.LastNavigationUtc <= BookmarkNavigationContinuationWindow)
        {
            bookmark = _bookmarkIndex.FindAdjacent(session.Id, itemId, cursor.BookmarkId, direction);
        }
        else
        {
            bookmark = _bookmarkIndex.FindRelative(session.Id, itemId, session.Position, direction);
        }
        if (bookmark is null)
        {
            Announce(direction < 0
                ? "Brak poprzedniej zakładki w tym materiale"
                : "Brak następnej zakładki w tym materiale");
            return;
        }

        var position = TimeSpan.FromTicks(bookmark.PositionTicks);
        _bookmarkNavigationCursor = new BookmarkNavigationCursor(
            session.Id,
            itemId,
            bookmark.Id,
            now);
        session.SetPosition(position);
        UpdatePlayerView();
        UpdatePlaybackStatusBar();
        if (string.Equals(session.Id, "local", StringComparison.Ordinal)) TrySaveLocalMediaState(false);
        if (_state.Settings.Messages.SeekMessages
            && _state.Settings.Messages.BookmarkNavigationMessages)
        {
            var time = CommandRouter.FormatTime(position);
            Announce(string.IsNullOrWhiteSpace(bookmark.Name)
                ? time
                : $"{bookmark.Name}, {time}");
        }
    }

    private void ShowPlayerView()
    {
        if (!_sessions.Current.HasItems)
        {
            AnnounceEssential($"Brak elementów w sesji {_sessions.Current.DisplayName}");
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        if (!_playerViewActive)
        {
            CaptureCurrentSessionNavigationState();
            _playerViewActive = true;
            BrowserHeaderPanel.Visibility = Visibility.Collapsed;
            BrowserActionPanel.Visibility = Visibility.Collapsed;
            MediaList.Visibility = Visibility.Collapsed;
            PlayerPanel.Visibility = Visibility.Visible;
        }
        GetSessionNavigationState(_sessions.Current.Id).PlayerActive = true;

        UpdatePlayerView(true);
        UpdateWindowTitle();
        _playerUiTimer.Start();
        Activate();
        Dispatcher.BeginInvoke(FocusPlayerView, DispatcherPriority.Loaded);
    }

    private void ReturnFromPlayerToList()
    {
        if (!_playerViewActive) return;
        _playerViewActive = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
        BrowserHeaderPanel.Visibility = Visibility.Visible;
        BrowserActionPanel.Visibility = Visibility.Visible;
        MediaList.Visibility = Visibility.Visible;
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        navigation.PlayerActive = false;
        _currentView = navigation.CurrentView;
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: navigation.SelectedItemIds.GetValueOrDefault(_currentView));
        UpdateWindowTitle();
        RestoreMediaListFocusAfterRefresh();
    }

    private void HidePlayerForBrowserNavigation()
    {
        if (!_playerViewActive) return;
        _playerViewActive = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
        BrowserHeaderPanel.Visibility = Visibility.Visible;
        BrowserActionPanel.Visibility = Visibility.Visible;
        MediaList.Visibility = Visibility.Visible;
        GetSessionNavigationState(_sessions.Current.Id).PlayerActive = false;
    }

    private void FocusPlayerView()
    {
        UpdatePlayerView(true);
        PlayerPlayPauseButton.Focus();
        Keyboard.Focus(PlayerPlayPauseButton);
    }

    private void UpdatePlayerView(bool updateAccessibleName = false)
    {
        if (!_playerViewActive) return;
        var session = _sessions.Current;
        var item = session.CurrentItem;
        var position = session.Position;
        var duration = item.Duration;
        var state = session.IsPlaying ? "Odtwarzanie" : "Pauza";

        PlayerTitleText.Text = item.Title;
        PlayerArtistText.Text = string.IsNullOrWhiteSpace(item.Artist)
            ? item.KindLabel
            : item.Artist;
        PlayerSessionText.Text = session.DisplayName;
        PlayerStateText.Text = state;
        PlayerSpeedText.Text = $"Prędkość: {FormatPlaybackRateMultiplier(session.PlaybackRate)}";
        PlayerTimeText.Text = duration > TimeSpan.Zero
            ? $"{CommandRouter.FormatTime(position)} z {CommandRouter.FormatTime(duration)}"
            : CommandRouter.FormatTime(position);
        PlayerPlayPauseButton.Content = session.IsPlaying ? "_Wstrzymaj" : "_Odtwórz";

        if (!updateAccessibleName) return;
        var artist = string.IsNullOrWhiteSpace(item.Artist) ? item.KindLabel : item.Artist;
        var action = session.IsPlaying ? "Wstrzymaj" : "Odtwórz";
        var focusContext = _playerFocusContextPrefix;
        _playerFocusContextPrefix = null;
        AutomationProperties.SetName(
            PlayerPlayPauseButton,
            focusContext is null
                ? $"Odtwarzacz, {item.Title}, {artist}, {session.DisplayName}, {state}, prędkość {FormatPlaybackRateMultiplier(session.PlaybackRate)}. {action}"
                : $"{focusContext}, Odtwarzacz, {item.Title}, {artist}, {state}, prędkość {FormatPlaybackRateMultiplier(session.PlaybackRate)}. {action}");
        AutomationProperties.SetHelpText(
            PlayerPlayPauseButton,
            "Strzałki sterują czasem i głośnością. Page Up i Page Down wybierają poprzedni lub następny utwór. B dodaje szybką zakładkę, Ctrl+Shift+B dodaje nazwaną, a Shift+Page Up i Shift+Page Down przechodzą po zakładkach. Shift+przecinek zwalnia, Shift+kropka przyspiesza, Ctrl+kropka przywraca normalną prędkość. Escape wraca do listy.");
    }

    private void PlayerUiTimer_Tick(object? sender, EventArgs e)
    {
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
        SaveLocalMediaStateIfDue();
    }

    private void UpdatePlaybackStatusBar()
    {
        var text = BuildPlaybackStatusText();
        _playbackStatusLabel.Text = text;
        _playbackStatusLabel.AccessibleName = text;
    }

    private string BuildPlaybackStatusText()
    {
        var session = _sessions.Current;
        var item = session.CurrentItem;
        var position = session.Position;
        var state = session.IsPlaying ? "Odtwarzanie" : "Pauza";
        var time = item.Duration > TimeSpan.Zero
            ? $"{CommandRouter.FormatTime(position)} z {CommandRouter.FormatTime(item.Duration)}"
            : $"{CommandRouter.FormatTime(position)}, czas całkowity nieznany";
        var parts = new List<string>();
        var audioParameters = AudioParametersFormatter.FormatCompact(item);
        if (!string.IsNullOrWhiteSpace(audioParameters)) parts.Add(audioParameters);
        var playbackState = state.ToLowerInvariant();
        if (Math.Abs(session.PlaybackRate - 1d) >= 0.001d)
        {
            playbackState += $", {CommandRouter.FormatPlaybackRate(session.PlaybackRate).ToLowerInvariant()}";
        }
        parts.Add(playbackState);
        parts.Add(time);
        parts.Add(item.Title);
        parts.Add(session.DisplayName);
        return string.Join(", ", parts);
    }

    private static string FormatPlaybackRateMultiplier(double playbackRate) =>
        $"{playbackRate.ToString("0.00", CultureInfo.CurrentCulture)} razy";

    public void ShowSeekToTime()
    {
        if (!EnsurePlayerViewForSeek()) return;
        ShowSeekPositionDialog(SeekInputMode.Time);
    }

    public void ShowSeekToPercentage()
    {
        if (!EnsurePlayerViewForSeek()) return;
        ShowSeekPositionDialog(SeekInputMode.Percentage);
    }

    private bool EnsurePlayerViewForSeek()
    {
        if (_playerViewActive) return true;
        Announce("Skok jest dostępny tylko w odtwarzaczu. Naciśnij F6");
        return false;
    }

    private void ShowSeekPositionDialog(SeekInputMode mode)
    {
        var session = _sessions.Current;
        var duration = session.CurrentItem.Duration;
        if (duration <= TimeSpan.Zero)
        {
            Announce(mode == SeekInputMode.Time
                ? "Skok do czasu niedostępny: czas trwania jest nieznany"
                : "Skok procentowy niedostępny: czas trwania jest nieznany");
            return;
        }

        var dialog = new SeekPositionWindow(mode, duration) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (mode == SeekInputMode.Time)
        {
            session.SetPosition(dialog.Position);
            Announce(CommandRouter.FormatTime(dialog.Position));
        }
        else
        {
            var position = TimeSpan.FromTicks(
                (long)Math.Round(duration.Ticks * (dialog.Percentage / 100d)));
            session.SetPosition(position);
            Announce($"{dialog.Percentage}%, {CommandRouter.FormatTime(position)}");
        }

        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
        if (string.Equals(session.Id, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(false);
        }
    }

    public void ShowSessionList()
    {
        var dialog = new SessionSelectionWindow(_sessions) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedSlot is int slot)
        {
            ExecuteCommand(CommandIds.SessionSlot(slot));
        }
        FocusMediaList();
    }

    public void ShowPlaylistManager()
    {
        var items = ActionItems;
        var dialog = new PlaylistWindow(items) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            var target = items.Count == 1 ? items[0].Title : FormatItemCount(items.Count);
            Announce($"Zapisano zmiany playlist dla: {target}");
        }
    }

    public void ShowCommandPalette()
    {
        ClearFocusContext();
        var entries = CommandPaletteSearch.CreateEntries(ActiveKeyboardProfile(), _state.Settings);
        var dialog = new CommandPaletteWindow(entries) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedCommandId is { } commandId)
        {
            Activate();
            FocusMediaList();
            Dispatcher.BeginInvoke(
                () => ExecuteCommand(commandId),
                DispatcherPriority.ContextIdle);
            return;
        }

        Activate();
        RestoreMediaListFocusAfterRefresh();
    }

    public void ShowItemProperties()
    {
        var item = ActionItem ?? _sessions.Current.CurrentItem;
        var activeOwner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive) ?? this;
        var dialog = new InformationWindow(BuildItemPropertiesText(item)) { Owner = activeOwner };
        dialog.ShowDialog();
        if (!ReferenceEquals(activeOwner, this)) return;
        Activate();
        if (_playerViewActive) FocusPlayerView();
        else RestoreMediaListFocusAfterRefresh();
    }

    private string BuildItemPropertiesText(MediaItem item)
    {
        var session = ActionSession;
        var isCurrent = string.Equals(session.CurrentItem.Id, item.Id, StringComparison.Ordinal);
        var localPath = TryGetLocalPath(item.Source, out var resolvedPath)
            ? resolvedPath
            : null;
        var sections = new List<string>
        {
            string.Join(Environment.NewLine,
            new string?[]
            {
                $"Tytuł: {item.Title}",
                string.IsNullOrWhiteSpace(item.Artist) ? null : $"Wykonawca: {item.Artist}",
                $"Rodzaj: {item.KindLabel}",
                $"Usługa: {session.DisplayName}",
                localPath is null ? null : $"Plik: {localPath}"
            }.Where(value => value is not null).Select(value => value!))
        };

        var playbackLines = new List<string>
        {
            "W aplikacji",
            $"Aktualnie odtwarzany: {(isCurrent ? "tak" : "nie")}",
            $"Ulubiony: {(item.IsFavorite ? "tak" : "nie")}",
            $"W bibliotece: {(item.IsInLibrary ? "tak" : "nie")}",
            $"W kolejce: {(item.IsInQueue ? "tak" : "nie")}",
            $"Odtwarzaj jako następne: {(item.IsPlayNext ? "tak" : "nie")}",
        };
        if (isCurrent)
        {
            playbackLines.Insert(2, $"Stan: {(session.IsPlaying ? "odtwarzanie" : "pauza")}");
            playbackLines.Insert(3, $"Pozycja: {CommandRouter.FormatTime(session.Position)}");
            playbackLines.Insert(4, $"Prędkość: {FormatPlaybackRateMultiplier(session.PlaybackRate)}");
            playbackLines.Insert(5, $"Głośność: {session.Volume}%");
        }
        sections.Add(string.Join(Environment.NewLine, playbackLines));

        var technicalLines = new List<string> { "Techniczne" };
        if (item.Duration > TimeSpan.Zero)
        {
            technicalLines.Add($"Czas: {CommandRouter.FormatTime(item.Duration)}");
        }
        if (localPath is not null)
        {
            var extension = Path.GetExtension(localPath).TrimStart('.');
            if (!string.IsNullOrWhiteSpace(extension))
            {
                technicalLines.Add($"Format: {extension.ToUpperInvariant()}");
            }
            try
            {
                technicalLines.Add($"Rozmiar: {FormatFileSize(new FileInfo(localPath).Length)}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // File details can disappear between opening the list and the dialog.
            }
        }
        if (item.BitrateKbps is int bitrate)
        {
            technicalLines.Add($"Bitrate: {bitrate} kb/s{(item.IsBitrateEstimated ? " (wartość obliczona)" : string.Empty)}");
        }
        if (item.SampleRateHz is int sampleRate && sampleRate > 0)
        {
            technicalLines.Add($"Częstotliwość próbkowania: {(sampleRate / 1000d).ToString("0.#", CultureInfo.CurrentCulture)} kHz");
        }
        if (technicalLines.Count > 1) sections.Add(string.Join(Environment.NewLine, technicalLines));

        if (localPath is null && !string.IsNullOrWhiteSpace(item.PublicUri))
        {
            sections.Add($"Źródło{Environment.NewLine}Łącze publiczne: {item.PublicUri}");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static bool TryGetLocalPath(string? source, out string localPath)
    {
        localPath = string.Empty;
        if (string.IsNullOrWhiteSpace(source)
            || Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return false;
        }

        if (!Path.IsPathFullyQualified(source)) return false;
        localPath = source;
        return true;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value.ToString(unit == 0 ? "0" : "0.##", CultureInfo.CurrentCulture)} {units[unit]}";
    }

    private string BuildQuickMediaInformation(MediaItem item)
    {
        var isLocal = TryGetLocalPath(item.Source, out var localPath);
        var metadataChanged = false;
        if (isLocal
            && (item.Duration <= TimeSpan.Zero
                || item.BitrateKbps is null
                || item.SampleRateHz is null)
            && WindowsMediaOutput.TryReadMetadata(localPath, out var duration, out var sampleRateHz))
        {
            if (item.Duration <= TimeSpan.Zero && duration > TimeSpan.Zero)
            {
                item.Duration = duration;
                metadataChanged = true;
            }
            if (item.SampleRateHz is null && sampleRateHz > 0)
            {
                item.SampleRateHz = sampleRateHz;
                metadataChanged = true;
            }
        }

        var details = new List<string>();
        if (isLocal)
        {
            var extension = Path.GetExtension(localPath).TrimStart('.');
            if (!string.IsNullOrWhiteSpace(extension)) details.Add(extension.ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(item.Artist)) details.Add(item.Artist);
        if (item.Duration > TimeSpan.Zero) details.Add(CommandRouter.FormatTime(item.Duration));
        if (isLocal)
        {
            try
            {
                var file = new FileInfo(localPath);
                if (file.Exists)
                {
                    if (item.BitrateKbps is null && item.Duration > TimeSpan.Zero)
                    {
                        item.BitrateKbps = LocalAudioFileDiscovery.EstimateBitrateKbps(
                            file.Length,
                            item.Duration);
                        item.IsBitrateEstimated = item.BitrateKbps.HasValue;
                        metadataChanged |= item.BitrateKbps.HasValue;
                    }
                    details.Add(FormatFileSize(file.Length));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The other cached details remain useful if the file is temporarily unavailable.
            }
        }

        var audio = AudioParametersFormatter.FormatCompact(item, CultureInfo.CurrentCulture);
        if (!string.IsNullOrWhiteSpace(audio))
        {
            var sizeIndex = details.Count > 0 && details[^1].EndsWith("B", StringComparison.Ordinal)
                ? details.Count - 1
                : details.Count;
            details.Insert(sizeIndex, audio);
        }
        if (metadataChanged) TrySaveLocalMediaState(false);

        return details.Count == 0
            ? $"{item.Title}: brak zapisanych informacji technicznych"
            : $"{item.Title}: {string.Join(", ", details)}";
    }

    private void AnnounceQuickMediaInformation(MediaItem item)
    {
        Announce(BuildQuickMediaInformation(item));
    }

    public void OpenOfficialApplication()
    {
        if (ActionItem is { } localItem && TryGetLocalPath(localItem.Source, out _))
        {
            OpenLocalInDefaultApplication();
            return;
        }
        Announce($"{ActionSession.DisplayName}: otwieranie zewnętrzne nie jest jeszcze połączone");
    }

    private void OpenLocalInDefaultApplication()
    {
        if (ActionItem is not { } item || !TryGetLocalPath(item.Source, out var localPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or IOException)
        {
            AnnounceEssential($"Nie można otworzyć pliku: {exception.Message}");
        }
    }

    public void ShowHelp()
    {
        MessageBox.Show(
            "Domyślny prefiks: Ctrl+Alt+Windows+F12.\n\n" +
            "Po prefiksie: 1–9 wybiera sesję, 0 otwiera ich listę, Page Up i Page Down zmieniają sesję, " +
            "strzałki sterują czasem i głośnością, Ctrl+E/R/T podaje czas. " +
            "U otwiera Ulubione, Shift+U zmienia stan ulubionych, B otwiera Zakładki, Shift+B dodaje zakładkę w odtwarzaczu, A otwiera Albumy, P otwiera Playlisty. " +
            "K filtruje bieżącą listę, F wyszukuje w bieżącej usłudze; warianty z Shift otwierają " +
            "paletę poleceń i wyszukiwanie globalne.\n\n" +
            "W aktywnym oknie: Ctrl+1–9 wybiera sesję bez prefiksu, Ctrl+0 otwiera listę sesji, a kolejność można zmienić w Ustawieniach Ogólnych. " +
            "Ctrl+Page Up i Ctrl+Page Down zmieniają sesję. " +
            "Ctrl+O dodaje lokalne pliki audio, a Ctrl+Shift+O rejestruje synchronizowane źródło Biblioteki wraz z podfolderami. " +
            "W lokalnej Bibliotece Alt+1 pokazuje Foldery, Alt+2 Wszystkie pliki, a F5 wykonuje pełne odświeżenie źródeł. Enter wchodzi do folderu, a Backspace wraca o poziom wyżej. Żadne z tych poleceń nie uruchamia dźwięku automatycznie. " +
            "Ctrl+U/P/L/Q otwiera odpowiednio: Ulubione, Playlisty, Bibliotekę i Kolejkę, " +
            "Ctrl+H otwiera trwałą Historię odtwarzania, Ctrl+B otwiera globalną listę Zakładek, Ctrl+Shift+B dodaje nazwaną zakładkę w odtwarzaczu, a Ctrl+Shift+A otwiera Albumy. Ctrl+K filtruje bieżącą listę. Ctrl+F otwiera okno " +
            "wyszukiwania w bieżącej usłudze, Ctrl+Shift+F otwiera wyszukiwanie globalne, " +
            "a Ctrl+Shift+K otwiera paletę poleceń. " +
            "Ctrl+N i Ctrl+A pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko.\n\n" +
            "W oknie: Enter na utworze lub stacji rozpoczyna odtwarzanie i otwiera odtwarzacz. " +
            "Ctrl+Enter odtwarza lub wstrzymuje zaznaczony element bez opuszczania listy, a Spacja steruje elementem faktycznie grającym. " +
            "Na listach Plików lokalnych lewa strzałka podaje krótkie informacje. " +
            "F6 otwiera odtwarzacz. W odtwarzaczu strzałki w lewo i w prawo przewijają o 10 sekund, z Shiftem o 30 sekund, a z Ctrl o minutę, " +
            "strzałki w górę i w dół zmieniają głośność, Home i End przechodzą na początek i w pobliże końca, " +
            "a cyfry od 0 do 9 przechodzą odpowiednio do 0, 10, 20 i kolejnych procent długości utworu oraz domyślnie oznajmiają tylko procent. " +
            "Shift+przecinek zmniejsza prędkość, Shift+kropka ją zwiększa, a Ctrl+kropka przywraca 1,00 razy; tempo zmienia się bez zmiany wysokości dźwięku. " +
            "Escape wraca do wcześniejszej listy. Alt+strzałka w dół przechodzi do starszego odtwarzanego elementu, a Alt+strzałka w górę do nowszego; pozycje są pamiętane. " +
            "Page Up i Page Down nadal wybierają poprzedni lub następny element listy źródłowej, a nie historii. B dodaje szybką zakładkę w bieżącym miejscu, Ctrl+Shift+B dodaje zakładkę z nazwą, a Shift+Page Up i Shift+Page Down przechodzą do poprzedniej lub następnej zakładki w tym samym materiale. Ctrl+Shift+E, Ctrl+Shift+R i Ctrl+Shift+T podają czas od początku, pozostały i całkowity. " +
            "Ctrl+Shift+G chwilowo włącza lub wyłącza wszystkie automatyczne komunikaty odtwarzacza; ich kategorie wybiera się osobno w Ustawieniach. " +
            "NVDA+End odczytuje pasek stanu z bieżącym czasem i parametrami audio. " +
            "Alt+Enter otwiera jedno dostępne okno Właściwości i informacje. " +
            "Ctrl+K, Ctrl+F i Ctrl+Shift+F nie opuszczają odtwarzacza; wyszukiwanie jest dostępne po powrocie do listy. " +
            "Skróty widoków opuszczają odtwarzacz, a F6 wraca do niego. " +
            "Ctrl+C kopiuje nazwy wszystkich zaznaczonych elementów, po jednej w wierszu; Ctrl+Shift+C kopiuje pełne ścieżki i fizyczne pliki lokalne. " +
            "Delete lub Backspace usuwa z bieżącego widoku. W lokalnej Bibliotece i na pliku w widoku Foldery usuwa tylko wpis z Biblioteki AMC, a plik pozostawia na dysku; na wierszu folderu nie usuwa niczego. W odtwarzaczu lokalnym Delete również usuwa tylko wpis z AMC i pozostawia plik na dysku. Shift+Delete działa wyłącznie na listach i po potwierdzeniu przenosi zaznaczone pliki do systemowego Kosza. " +
            "Alt+strzałka w lewo i w prawo przechodzi po osobnej historii widoków. " +
            "Ctrl+Z cofa ostatnią zmianę Ulubionych, Biblioteki lub Kolejki. " +
            "Alt+F4 zawsze zamyka całe główne okno i aplikację, również z widoku odtwarzacza. " +
            "Escape w filtrze lub na głównym przycisku wraca do listy; aktywny filtr jest wtedy czyszczony. " +
            "W menu Escape standardowo wychodzi o jeden poziom.",
            "Skróty prototypu",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public void ShowSettings(SettingsTarget target) => OpenSettings(target);

    public void ToggleAccessibilityMessages()
    {
        _state.Settings.Messages.Enabled = !_state.Settings.Messages.Enabled;
        _store.Save(_state);
        AnnounceEssential(_state.Settings.Messages.Enabled
            ? "Komunikaty dostępności włączone"
            : "Komunikaty dostępności wyłączone");
    }

    public void ToggleDetailedHints()
    {
        _state.Settings.Messages.DetailedHints = !_state.Settings.Messages.DetailedHints;
        ApplyDetailedHints();
        _store.Save(_state);
        AnnounceEssential(_state.Settings.Messages.DetailedHints
            ? "Szczegółowe podpowiedzi klawiatury włączone"
            : "Szczegółowe podpowiedzi klawiatury wyłączone");
    }

    public void ToggleSeekMessages()
    {
        var enabled = !_state.Settings.Messages.SeekMessages;
        _state.Settings.Messages.SeekMessages = enabled;
        _store.Save(_state);
        AnnounceEssential(enabled
            ? "Automatyczne komunikaty odtwarzacza włączone"
            : "Automatyczne komunikaty odtwarzacza wyłączone");
    }

    public void OpenLocalFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Otwórz lokalne pliki audio",
            Filter = LocalAudioFileDiscovery.DialogFilter,
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        AddLocalFiles(dialog.FileNames);
    }

    public async void OpenLocalFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Otwórz folder z plikami audio",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        var folderSource = RegisterLocalFolderSource(dialog.FolderName);
        ShowLocalFolderWhileLoading(folderSource);
        TrySaveLocalMediaState(true);
        AnnounceEssential($"Wczytywanie folderu: {folderSource.DisplayName}");
        IReadOnlyList<string> fileNames;
        try
        {
            fileNames = await Task.Run(() => LocalAudioFileDiscovery.FindFiles(dialog.FolderName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            AnnounceEssential($"Nie można otworzyć folderu: {exception.Message}");
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        if (fileNames.Count == 0)
        {
            AnnounceEssential("W folderze nie znaleziono obsługiwanych plików audio");
        }

        var sync = LocalLibrarySynchronizer.Synchronize(
            _localItems,
            [folderSource.Path],
            fileNames,
            _state.LocalMedia.ExcludedPaths);
        RefreshLocalSessionItems();
        ConfigureLocalSourceWatchers();
        var selected = fileNames
            .Select(path => _localItems.FirstOrDefault(item =>
                string.Equals(item.Source, path, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(item => item is { IsAvailable: true, IsInLibrary: true });
        NavigateTo(FolderViewName);
        if (selected is not null) SelectMediaItem(selected.Id);
        var countParts = new List<string>();
        if (sync.AddedItems.Count > 0) countParts.Add($"dodano {FormatFileCount(sync.AddedItems.Count)}");
        if (sync.RestoredItems.Count > 0) countParts.Add($"ponownie dostępne {FormatFileCount(sync.RestoredItems.Count)}");
        if (countParts.Count == 0) countParts.Add("biblioteka była aktualna");
        PrepareSelectedItemFocusContext($"Foldery Biblioteki, {folderSource.DisplayName}, {string.Join(", ", countParts)}");
        TrySaveLocalMediaState(true);
        RestoreMediaListFocusAfterRefresh();
    }

    private void ShowLocalFolderWhileLoading(LocalFolderSourceSettings folderSource)
    {
        CaptureCurrentSessionNavigationState();
        _sessions.SelectSession("local");
        _state.LocalMedia.CurrentFolderPath = folderSource.Path;
        _state.LocalMedia.LibraryView = FolderViewName;
        var navigation = GetSessionNavigationState("local");
        navigation.CurrentView = FolderViewName;
        navigation.PlayerActive = false;
        navigation.Filters[FolderViewName] = string.Empty;
        _currentView = FolderViewName;
        HidePlayerForBrowserNavigation();
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView();
        PrepareViewFocusContext($"Foldery Biblioteki, {folderSource.DisplayName}, wczytywanie");
        RestoreMediaListFocusAfterRefresh();
    }

    private void AddLocalFiles(IEnumerable<string> fileNames, string? folderSourcePath = null)
    {
        var paths = fileNames.Select(Path.GetFullPath).ToArray();
        RemoveLocalExclusions(paths);
        var import = LocalLibraryImporter.Import(_localItems, paths);
        var addedItems = import.AddedItems;

        RefreshLocalSessionItems();
        var session = _sessions.FindSession("local")!;
        var slot = _sessions.FindSlot("local");

        var selected = import.ImportedItems.FirstOrDefault()
            ?? session.CurrentItem;
        CaptureCurrentSessionNavigationState();
        _sessions.SelectSession(session.Id);
        LocalFolderSourceSettings? folderSource = null;
        if (!string.IsNullOrWhiteSpace(folderSourcePath))
        {
            folderSource = RegisterLocalFolderSource(folderSourcePath);
            _state.LocalMedia.CurrentFolderPath = folderSource.Path;
            _state.LocalMedia.LibraryView = FolderViewName;
            NavigateTo(FolderViewName);
            SelectMediaItem(selected.Id);
        }
        else
        {
            _state.LocalMedia.LibraryView = AllLocalFilesViewName;
            NavigateTo(AllLocalFilesViewName);
            SelectMediaItem(selected.Id);
        }
        var countParts = new List<string>();
        if (addedItems.Count > 0) countParts.Add($"dodano {FormatFileCount(addedItems.Count)}");
        if (import.RestoredItems.Count > 0)
            countParts.Add($"przywrócono w bibliotece {FormatFileCount(import.RestoredItems.Count)}");
        var countText = countParts.Count == 0
            ? "pliki były już w bibliotece"
            : string.Join(", ", countParts);
        var slotText = slot is > 0 ? $", sesja {slot}" : string.Empty;
        PrepareSelectedItemFocusContext(folderSource is null
            ? $"Pliki lokalne{slotText}, {countText}"
            : $"Foldery, {folderSource.DisplayName}{slotText}, {countText}");
        TrySaveLocalMediaState(true);
        RestoreMediaListFocusAfterRefresh();
    }

    private void AddLocalExclusions(IEnumerable<MediaItem> items)
    {
        var paths = items
            .Select(item => item.Source)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!));
        foreach (var path in paths)
        {
            if (!_state.LocalMedia.ExcludedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _state.LocalMedia.ExcludedPaths.Add(path);
            }
        }
    }

    private void RemoveLocalExclusions(IEnumerable<string> paths)
    {
        var normalized = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _state.LocalMedia.ExcludedPaths.RemoveAll(path => normalized.Contains(path));
    }

    private LocalFolderSourceSettings RegisterLocalFolderSource(string path)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var existing = _state.LocalMedia.FolderSources.FirstOrDefault(source =>
            string.Equals(source.Path, normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var displayName = Path.GetFileName(normalizedPath);
        var source = new LocalFolderSourceSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            Path = normalizedPath,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedPath : displayName
        };
        _state.LocalMedia.FolderSources.Add(source);
        return source;
    }

    public async void RefreshLocalLibrary()
    {
        await SynchronizeLocalSourcesAsync(announceResult: true);
    }

    private async Task SynchronizeLocalSourcesAsync(bool announceResult)
    {
        if (_localSourceSyncInProgress)
        {
            _localSourceSyncPending = true;
            return;
        }

        var sources = _state.LocalMedia.FolderSources.ToArray();
        if (sources.Length == 0)
        {
            if (announceResult) Announce("Brak zarejestrowanych źródeł folderowych");
            return;
        }

        _localSourceSyncInProgress = true;
        try
        {
            var scans = await Task.WhenAll(sources.Select(source => Task.Run(() => ScanLocalSource(source))));
            if (_isClosing) return;
            var successful = scans.Where(scan => scan.Error is null).ToArray();
            var failed = scans.Where(scan => scan.Error is not null).ToArray();
            var localBeforeSync = _sessions.FindSession("local");
            var currentLocalItemId = localBeforeSync is { HasItems: true }
                ? localBeforeSync.CurrentItem.Id
                : null;
            var currentLocalWasPlaying = localBeforeSync?.IsPlaying == true;
            var result = LocalLibrarySynchronizer.Synchronize(
                _localItems,
                successful.Select(scan => scan.Source.Path),
                successful.SelectMany(scan => scan.Files),
                _state.LocalMedia.ExcludedPaths);

            if (result.Changed)
            {
                var selectedId = SelectedItem?.Id;
                var hadListFocus = MediaList.IsKeyboardFocusWithin;
                if (hadListFocus) AnchorMediaListFocus();
                RefreshLocalSessionItems();
                if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) && !_playerViewActive)
                {
                    RefreshCurrentView(preferredItemId: selectedId);
                    if (hadListFocus) RestoreMediaListFocusAfterRefresh();
                }
                else if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) && _playerViewActive)
                {
                    if (_sessions.Current.HasItems) UpdatePlayerView(true);
                    else ReturnFromPlayerToList();
                    UpdatePlaybackStatusBar();
                    UpdateWindowTitle();
                }
                TrySaveLocalMediaState(announceResult);
            }

            if (currentLocalItemId is not null
                && result.BecameUnavailableItems.Any(item =>
                    string.Equals(item.Id, currentLocalItemId, StringComparison.Ordinal)))
            {
                var unavailableTitle = result.BecameUnavailableItems
                    .First(item => string.Equals(item.Id, currentLocalItemId, StringComparison.Ordinal))
                    .Title;
                AnnounceEssential($"Plik stał się niedostępny: {unavailableTitle}"
                    + (currentLocalWasPlaying ? ". Odtwarzanie zatrzymano" : string.Empty));
            }

            ConfigureLocalSourceWatchers();
            if (announceResult)
            {
                var parts = new List<string>();
                if (result.AddedItems.Count > 0) parts.Add($"dodano {FormatFileCount(result.AddedItems.Count)}");
                if (result.RestoredItems.Count > 0) parts.Add($"ponownie dostępne {FormatFileCount(result.RestoredItems.Count)}");
                if (result.BecameUnavailableItems.Count > 0)
                    parts.Add($"niedostępne {FormatFileCount(result.BecameUnavailableItems.Count)}");
                if (failed.Length > 0) parts.Add($"niedostępne źródła: {failed.Length}");
                Announce(parts.Count == 0
                    ? "Biblioteka lokalna jest aktualna"
                    : $"Odświeżono Bibliotekę: {string.Join(", ", parts)}");
            }
        }
        finally
        {
            _localSourceSyncInProgress = false;
            if (_localSourceSyncPending)
            {
                _localSourceSyncPending = false;
                ScheduleLocalSourceSync();
            }
        }
    }

    private static LocalSourceScanResult ScanLocalSource(LocalFolderSourceSettings source)
    {
        try
        {
            if (!Directory.Exists(source.Path))
            {
                return new(source, [], "folder jest niedostępny");
            }
            return new(source, LocalAudioFileDiscovery.FindFiles(source.Path), null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new(source, [], exception.Message);
        }
    }

    private void ConfigureLocalSourceWatchers()
    {
        if (_isClosing) return;
        foreach (var watcher in _localSourceWatchers.Values) watcher.Dispose();
        _localSourceWatchers.Clear();
        foreach (var source in _state.LocalMedia.FolderSources)
        {
            if (!Directory.Exists(source.Path)) continue;
            try
            {
                var watcher = new FileSystemWatcher(source.Path)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size
                };
                watcher.Created += LocalSourceWatcher_Changed;
                watcher.Deleted += LocalSourceWatcher_Changed;
                watcher.Changed += LocalSourceWatcher_Changed;
                watcher.Renamed += LocalSourceWatcher_Renamed;
                watcher.Error += LocalSourceWatcher_Error;
                watcher.EnableRaisingEvents = true;
                _localSourceWatchers[source.Path] = watcher;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // F5 and the startup scan remain available when a provider does
                // not support reliable change notifications.
            }
        }
    }

    private void LocalSourceWatcher_Changed(object sender, FileSystemEventArgs e) =>
        ScheduleLocalSourceSync();

    private void LocalSourceWatcher_Renamed(object sender, RenamedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                ApplyRenamedLocalPath(e.OldFullPath, e.FullPath);
                ScheduleLocalSourceSync();
            },
            DispatcherPriority.Background);
    }

    private void ApplyRenamedLocalPath(string oldPath, string newPath)
    {
        var normalizedOld = Path.GetFullPath(oldPath);
        var normalizedNew = Path.GetFullPath(newPath);
        var changed = false;
        foreach (var item in _localItems.Where(item => item.Source is { Length: > 0 }).ToArray())
        {
            var itemPath = Path.GetFullPath(item.Source!);
            string? replacement = null;
            if (string.Equals(itemPath, normalizedOld, StringComparison.OrdinalIgnoreCase))
            {
                replacement = normalizedNew;
            }
            else if (IsSameOrDescendantPath(itemPath, normalizedOld))
            {
                replacement = Path.Combine(normalizedNew, Path.GetRelativePath(normalizedOld, itemPath));
            }
            if (replacement is null || !File.Exists(replacement) || !LocalAudioFileDiscovery.IsAudioFile(replacement))
            {
                continue;
            }

            item.Source = replacement;
            item.Title = Path.GetFileNameWithoutExtension(replacement);
            item.IsAvailable = true;
            changed = true;
        }

        var exclusions = _state.LocalMedia.ExcludedPaths.ToArray();
        foreach (var excludedPath in exclusions)
        {
            string? replacement = null;
            if (string.Equals(excludedPath, normalizedOld, StringComparison.OrdinalIgnoreCase))
            {
                replacement = normalizedNew;
            }
            else if (IsSameOrDescendantPath(excludedPath, normalizedOld))
            {
                replacement = Path.Combine(normalizedNew, Path.GetRelativePath(normalizedOld, excludedPath));
            }
            if (replacement is null) continue;
            _state.LocalMedia.ExcludedPaths.RemoveAll(path =>
                string.Equals(path, excludedPath, StringComparison.OrdinalIgnoreCase));
            if (!_state.LocalMedia.ExcludedPaths.Contains(replacement, StringComparer.OrdinalIgnoreCase))
            {
                _state.LocalMedia.ExcludedPaths.Add(replacement);
            }
            changed = true;
        }

        if (!changed) return;
        RefreshLocalSessionItems();
        TrySaveLocalMediaState(false);
    }

    private void LocalSourceWatcher_Error(object sender, ErrorEventArgs e) =>
        ScheduleLocalSourceSync();

    private void ScheduleLocalSourceSync()
    {
        if (_isClosing) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ScheduleLocalSourceSync, DispatcherPriority.Background);
            return;
        }
        _localSourceSyncTimer.Stop();
        _localSourceSyncTimer.Start();
    }

    private async void LocalSourceSyncTimer_Tick(object? sender, EventArgs e)
    {
        _localSourceSyncTimer.Stop();
        await SynchronizeLocalSourcesAsync(announceResult: false);
    }

    private void LoadPersistedLocalMedia()
    {
        foreach (var saved in _state.LocalMedia.Items)
        {
            _localItems.Add(new MediaItem
            {
                Id = saved.Id,
                Title = saved.Title,
                Kind = MediaItemKind.Track,
                Duration = TimeSpan.FromTicks(saved.DurationTicks),
                BitrateKbps = saved.BitrateKbps,
                IsBitrateEstimated = saved.IsBitrateEstimated,
                SampleRateHz = saved.SampleRateHz,
                Source = saved.Path,
                IsFavorite = saved.IsFavorite,
                IsInLibrary = saved.IsInLibrary,
                IsAvailable = saved.IsAvailable,
                IsInQueue = saved.IsInQueue,
                IsPlayNext = saved.IsPlayNext
            });
        }
    }

    private void CaptureLocalMediaState()
    {
        var local = _sessions?.FindSession("local");
        if (local is not null) local.RememberCurrentPosition();

        var savedById = _state.LocalMedia.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var rememberedPositions = local?.RememberedPositions;
        _state.LocalMedia.Items = _localItems.Select(item =>
        {
            savedById.TryGetValue(item.Id, out var previous);
            var (fileLength, lastWriteUtcTicks) = previous is null
                ? GetFileFingerprint(item.Source)
                : (previous.FileLength, previous.LastWriteUtcTicks);
            var position = rememberedPositions?.GetValueOrDefault(item.Id)
                ?? (previous is null ? TimeSpan.Zero : TimeSpan.FromTicks(previous.ResumePositionTicks));
            return new LocalMediaItemSettings
            {
                Id = item.Id,
                Title = item.Title,
                Path = item.Source ?? string.Empty,
                DurationTicks = item.Duration.Ticks,
                BitrateKbps = item.BitrateKbps,
                IsBitrateEstimated = item.IsBitrateEstimated,
                SampleRateHz = item.SampleRateHz,
                IsFavorite = item.IsFavorite,
                IsInLibrary = item.IsInLibrary,
                IsAvailable = item.IsAvailable,
                IsInQueue = item.IsInQueue,
                IsPlayNext = item.IsPlayNext,
                ResumePositionTicks = Math.Max(0, position.Ticks),
                FileLength = fileLength,
                LastWriteUtcTicks = lastWriteUtcTicks
            };
        }).ToList();

        if (local is not null && local.HasItems)
        {
            _state.LocalMedia.CurrentItemId = local.CurrentItem.Id;
            _state.LocalMedia.Volume = local.Volume;
            _state.LocalMedia.PlaybackRate = local.PlaybackRate;
        }
        else
        {
            _state.LocalMedia.CurrentItemId = null;
        }
    }

    private void SaveLocalMediaState()
    {
        CaptureCurrentSessionNavigationState();
        CaptureLocalMediaState();
        _store.Save(_state);
        var local = _sessions.FindSession("local");
        _lastSavedLocalPositionTicks = local?.Position.Ticks ?? 0;
        _lastLocalStateSaveUtc = DateTime.UtcNow;
    }

    private bool TrySaveLocalMediaState(bool announceFailure)
    {
        try
        {
            SaveLocalMediaState();
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (announceFailure)
            {
                AnnounceEssential($"Nie udało się zapisać stanu lokalnych multimediów: {exception.Message}");
            }
            return false;
        }
    }

    private void SaveLocalMediaStateIfDue()
    {
        var local = _sessions.FindSession("local");
        if (local is null) return;
        var currentTicks = local.Position.Ticks;
        if (currentTicks == _lastSavedLocalPositionTicks) return;
        if (DateTime.UtcNow - _lastLocalStateSaveUtc < TimeSpan.FromSeconds(15)) return;

        TrySaveLocalMediaState(false);
    }

    private void RecordPlayback(
        DemoMediaSession session,
        MediaItem item,
        bool resetHistoryNavigation = true)
    {
        if (resetHistoryNavigation) _playbackHistoryCursors.Remove(session.Id);
        if (!_playbackHistory.Record(session.Id, item.Id)) return;
        try
        {
            _store.Save(_state);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Playback remains available even if history cannot be persisted.
        }
    }

    private void NavigatePlaybackHistory(int direction)
    {
        var session = _sessions.Current;
        var itemsById = session.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (!_playbackHistoryCursors.TryGetValue(session.Id, out var cursor))
        {
            var itemIds = _playbackHistory.GetItemIds(session.Id)
                .Where(itemsById.ContainsKey)
                .ToArray();
            var currentIndex = Array.FindIndex(
                itemIds,
                itemId => string.Equals(itemId, session.CurrentItem.Id, StringComparison.Ordinal));
            cursor = new PlaybackHistoryCursor(itemIds, currentIndex);
            _playbackHistoryCursors[session.Id] = cursor;
        }
        if (cursor.ItemIds.Count == 0)
        {
            Announce("Historia odtwarzania jest pusta");
            return;
        }

        var targetIndex = cursor.Index < 0
            ? (direction > 0 ? 0 : -1)
            : cursor.Index + Math.Sign(direction);
        while (targetIndex >= 0
               && targetIndex < cursor.ItemIds.Count
               && !itemsById.ContainsKey(cursor.ItemIds[targetIndex]))
        {
            targetIndex += Math.Sign(direction);
        }
        if (targetIndex < 0 || targetIndex >= cursor.ItemIds.Count)
        {
            Announce(direction > 0
                ? "Brak starszego elementu w historii"
                : "Brak nowszego elementu w historii");
            return;
        }

        cursor.Index = targetIndex;
        var target = itemsById[cursor.ItemIds[targetIndex]];
        session.Play(target);
        RecordPlayback(session, target, resetHistoryNavigation: false);
        RefreshPlaybackIndicators();
        UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        if (string.Equals(session.Id, "local", StringComparison.Ordinal)) TrySaveLocalMediaState(false);
        Announce($"Historia: {target.Title}");
    }

    private static (long? FileLength, long? LastWriteUtcTicks) GetFileFingerprint(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (null, null);
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? (file.Length, file.LastWriteTimeUtc.Ticks) : (null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return (null, null);
        }
    }

    private static bool CanRestorePosition(LocalMediaItemSettings saved)
    {
        if (saved.ResumePositionTicks <= 0) return false;
        var (fileLength, lastWriteUtcTicks) = GetFileFingerprint(saved.Path);
        if (!fileLength.HasValue || !lastWriteUtcTicks.HasValue) return true;
        return (!saved.FileLength.HasValue || saved.FileLength == fileLength)
            && (!saved.LastWriteUtcTicks.HasValue || saved.LastWriteUtcTicks == lastWriteUtcTicks);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        try
        {
            _windowSource = HwndSource.FromHwnd(handle);
            _windowSource?.AddHook(WindowMessageHook);
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
        var previousSessionId = _sessions is null ? null : _sessions.Current.Id;
        var desiredSessionId = previousSessionId ?? _state.Settings.LastSessionId;
        var previousLocal = _sessions?.FindSession("local");
        var previousLocalItemId = previousLocal?.CurrentItem.Id;
        var previousLocalPosition = previousLocal?.Position ?? TimeSpan.Zero;
        var previousLocalWasPlaying = previousLocal?.IsPlaying == true;
        _membershipHistory.Clear();
        _localCatalogHistory.Clear();
        _playbackHistoryCursors.Clear();
        _undoSequence = 0;
        _sessions = new SessionManager(_state.Settings);
        var (local, _) = _sessions.AddOrUpdateTransientSession(
            "local",
            "Pliki lokalne",
            ActiveLocalItems(),
            _localOutput,
            1);
        if (local.HasItems)
        {
            foreach (var saved in _state.LocalMedia.Items.Where(CanRestorePosition))
            {
                local.SetRememberedPosition(saved.Id, TimeSpan.FromTicks(saved.ResumePositionTicks));
            }
            var restoredItemId = previousLocalItemId ?? _state.LocalMedia.CurrentItemId;
            var restoredItem = local.Items.FirstOrDefault(item => item.Id == restoredItemId);
            if (restoredItem is not null) local.SelectItem(restoredItem);
            if (previousLocal is not null) local.SetPosition(previousLocalPosition);
        }
        local.SetVolume(previousLocal?.Volume ?? _state.LocalMedia.Volume);
        local.SetPlaybackRate(previousLocal?.PlaybackRate ?? _state.LocalMedia.PlaybackRate);
        if (previousLocalWasPlaying
            && previousLocalItemId is not null
            && local.Items.Any(item => string.Equals(item.Id, previousLocalItemId, StringComparison.Ordinal)))
        {
            local.Play(local.CurrentItem);
        }
        if (string.Equals(desiredSessionId, "local", StringComparison.Ordinal))
        {
            _sessions.SelectSession("local");
        }
        _router = new CommandRouter(_sessions, _state.Settings, this, this);
    }

    private IEnumerable<MediaItem> ActiveLocalItems() =>
        _localItems.Where(item => item.IsAvailable && item.IsInLibrary);

    private void RefreshLocalSessionItems()
    {
        var local = _sessions.FindSession("local");
        if (local is null) return;
        local.ReplaceItems(ActiveLocalItems());
    }

    private SessionNavigationState GetSessionNavigationState(string sessionId)
    {
        if (!_state.SessionNavigation.Sessions.TryGetValue(sessionId, out var navigation))
        {
            navigation = new SessionNavigationState();
            _state.SessionNavigation.Sessions[sessionId] = navigation;
        }
        return navigation;
    }

    private SessionViewHistory GetSessionViewHistory(string sessionId)
    {
        if (!_viewHistories.TryGetValue(sessionId, out var history))
        {
            history = new SessionViewHistory();
            _viewHistories[sessionId] = history;
        }
        return history;
    }

    private void CaptureCurrentSessionNavigationState()
    {
        if (_sessions is null || _restoringSessionNavigation) return;
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        navigation.CurrentView = _currentView;
        navigation.PlayerActive = _playerViewActive;
        navigation.Filters[_currentView] = FilterBox.Text;
        if (SelectedItem is { } selected)
        {
            navigation.SelectedItemIds[_currentView] = selected.Id;
        }
    }

    private void RestoreCurrentSessionNavigationState()
    {
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        _currentView = string.IsNullOrWhiteSpace(navigation.CurrentView)
            ? DefaultBrowserView
            : navigation.CurrentView;
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: navigation.SelectedItemIds.GetValueOrDefault(_currentView));
        _playerViewActive = navigation.PlayerActive;
        BrowserHeaderPanel.Visibility = _playerViewActive ? Visibility.Collapsed : Visibility.Visible;
        BrowserActionPanel.Visibility = _playerViewActive ? Visibility.Collapsed : Visibility.Visible;
        MediaList.Visibility = _playerViewActive ? Visibility.Collapsed : Visibility.Visible;
        PlayerPanel.Visibility = _playerViewActive ? Visibility.Visible : Visibility.Collapsed;
        if (_playerViewActive) UpdatePlayerView(true);
        UpdateWindowTitle();
    }

    private void RestoreFilterForCurrentView(SessionNavigationState navigation)
    {
        _restoringSessionNavigation = true;
        try
        {
            FilterBox.Text = navigation.Filters.GetValueOrDefault(_currentView) ?? string.Empty;
        }
        finally
        {
            _restoringSessionNavigation = false;
        }
    }

    private void LocalOutput_DurationAvailable(object? sender, MediaDurationAvailableEventArgs e)
    {
        e.Item.Duration = e.Duration;
        e.Item.SampleRateHz = e.SampleRateHz > 0 ? e.SampleRateHz : null;
        if (e.Item.Source is { Length: > 0 } path)
        {
            try
            {
                e.Item.BitrateKbps = LocalAudioFileDiscovery.EstimateBitrateKbps(
                    new FileInfo(path).Length,
                    e.Duration);
                e.Item.IsBitrateEstimated = e.Item.BitrateKbps.HasValue;
            }
            catch (IOException)
            {
                e.Item.BitrateKbps = null;
                e.Item.IsBitrateEstimated = false;
            }
            catch (UnauthorizedAccessException)
            {
                e.Item.BitrateKbps = null;
                e.Item.IsBitrateEstimated = false;
            }
        }
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
        TrySaveLocalMediaState(false);
        // Do not rebuild the focused list when asynchronous metadata arrives.
        // Commands use the new duration immediately; the row is reformatted on
        // the next ordinary refresh without causing an extra focus event.
    }

    private void LocalOutput_PlaybackFailed(object? sender, MediaOutputFailedEventArgs e)
    {
        _sessions.FindSession("local")?.MarkPlaybackFailed();
        RefreshPlaybackIndicators();
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        TrySaveLocalMediaState(false);
        var title = e.Item?.Title ?? "plik";
        Announce($"Nie można odtworzyć: {title}. {e.Message}");
    }

    private void LocalOutput_PlaybackEnded(object? sender, MediaPlaybackEndedEventArgs e)
    {
        var localSession = _sessions.FindSession("local");
        var nextItem = localSession?.ContinueAfterPlaybackEnded(e.Item);
        if (localSession is not null && nextItem is not null) RecordPlayback(localSession, nextItem);
        if (string.Equals(_currentView, "Kolejka", StringComparison.Ordinal))
        {
            var restoreListFocus = !_playerViewActive && MediaList.IsKeyboardFocusWithin;
            var previousIndex = MediaList.SelectedIndex;
            if (restoreListFocus) AnchorMediaListFocus();
            RefreshCurrentView(previousIndex);
            if (restoreListFocus) RestoreMediaListFocusAfterRefresh();
        }
        else
        {
            RefreshPlaybackIndicators();
        }
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        TrySaveLocalMediaState(false);
        Announce(nextItem is null
            ? $"Koniec: {e.Item.Title}"
            : $"Odtwarzanie: {nextItem.Title}");
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

        return ExecuteCommand(commandId).KeepPrefixActive;
    }

    private KeyboardProfile ActiveKeyboardProfile() =>
        _state.KeyboardProfiles.FirstOrDefault(profile => profile.Id == _state.Settings.ActiveKeyboardProfileId)
        ?? _state.KeyboardProfiles[0];

    private CommandExecutionResult ExecuteCommand(string commandId)
    {
        if (commandId is not CommandIds.PreviousBookmark and not CommandIds.NextBookmark)
        {
            _bookmarkNavigationCursor = null;
        }
        var oldSession = _sessions.Current.Id;
        CaptureCurrentSessionNavigationState();
        var previousIndex = MediaList.SelectedIndex;
        var restoreListFocus = MediaList.IsKeyboardFocusWithin || Keyboard.FocusedElement is MenuItem;
        var navigatesSession = commandId is CommandIds.SessionPrevious or CommandIds.SessionNext
            || commandId.StartsWith("session.slot.", StringComparison.Ordinal);
        var mergeSessionAnnouncementWithFocus = navigatesSession;
        var changesListMembership = commandId is CommandIds.ToggleFavorite
            or CommandIds.ToggleLibrary
            or CommandIds.AddQueue
            or CommandIds.TogglePlayNext;
        var changedSession = changesListMembership ? ActionSession : null;
        var changedItems = changesListMembership ? ActionItems.ToArray() : [];
        var previousMemberships = changedItems
            .Select(item => (Item: item, Previous: MediaMembershipState.From(item)))
            .ToArray();
        if (changesListMembership && restoreListFocus) AnchorMediaListFocus();
        if (changesListMembership || mergeSessionAnnouncementWithFocus)
        {
            _deferredAnnouncement = null;
            _deferAnnouncements = true;
        }
        CommandExecutionResult result;
        try
        {
            result = _router.Execute(commandId);
        }
        finally
        {
            _deferAnnouncements = false;
        }
        if (commandId == CommandIds.ToggleLibrary
            && string.Equals(changedSession?.Id, "local", StringComparison.Ordinal))
        {
            foreach (var item in changedItems)
            {
                if (item.IsInLibrary) RemoveLocalExclusions([item.Source ?? string.Empty]);
                else AddLocalExclusions([item]);
            }
            RefreshLocalSessionItems();
        }
        if (changedSession is not null)
        {
            var undoAnnouncement = previousMemberships.Length == 1
                ? BuildUndoAnnouncement(
                    commandId,
                    previousMemberships[0].Item,
                    previousMemberships[0].Previous)
                : $"Cofnięto zmianę dla: {FormatItemCount(previousMemberships.Length)}";
            _membershipHistory.RecordBatch(
                changedSession.Id,
                previousMemberships,
                undoAnnouncement,
                ++_undoSequence);
        }
        var sessionChanged = _sessions.Current.Id != oldSession;
        if (sessionChanged)
        {
            RestoreCurrentSessionNavigationState();
        }
        else if (changesListMembership)
        {
            RefreshCurrentView(changesListMembership ? previousIndex : null);
            if (!_playerViewActive && changedItems.Length > 1)
            {
                SelectMediaItems(changedItems.Select(item => item.Id));
            }
        }
        if (sessionChanged || changesListMembership)
        {
            if (sessionChanged
                && mergeSessionAnnouncementWithFocus
                && _deferredAnnouncement is { } sessionContext)
            {
                if (_playerViewActive)
                {
                    _playerFocusContextPrefix = sessionContext;
                }
                else
                {
                    PrepareSelectedItemFocusContext(
                        string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal)
                            ? sessionContext
                            : $"{sessionContext}, {_currentView}");
                }
                _deferredAnnouncement = null;
            }
            if (sessionChanged)
            {
                if (_playerViewActive) Dispatcher.BeginInvoke(FocusPlayerView, DispatcherPriority.Loaded);
                else RestoreMediaListFocusAfterRefresh();
            }
            else if (restoreListFocus)
            {
                RestoreMediaListFocusAfterRefresh();
            }
        }
        if (_deferredAnnouncement is { } announcement)
        {
            _deferredAnnouncement = null;
            Dispatcher.BeginInvoke(
                () => Announce(announcement),
                DispatcherPriority.ContextIdle);
        }
        RefreshPlaybackIndicators();
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        var savesPlaybackBoundary = commandId is CommandIds.PlayPause
            or CommandIds.ActivateSelected
            or CommandIds.Previous
            or CommandIds.Next;
        if (savesPlaybackBoundary && result.Handled && _sessions.Current.IsPlaying)
        {
            RecordPlayback(_sessions.Current, _sessions.Current.CurrentItem);
        }
        if ((changesListMembership && string.Equals(changedSession?.Id, "local", StringComparison.Ordinal))
            || (savesPlaybackBoundary && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)))
        {
            TrySaveLocalMediaState(false);
        }
        return result;
    }

    private void RefreshCurrentView(int? fallbackIndex = null, string? preferredItemId = null)
    {
        ClearFocusContext();
        preferredItemId ??= SelectedItem?.Id;
        SessionHeading.Text = string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal)
            ? "Wszystkie sesje"
            : _sessions.Current.DisplayName;
        ViewHeading.Text = _currentView;
        UpdateWindowTitle();
        if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            _unfilteredItems = _bookmarkIndex.GetForDisplay(
                    _sessions.Current.Id,
                    _sessions.Current.CurrentItem.Id)
                .Select(CreateBookmarkRow)
                .ToList();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        if (string.Equals(_currentView, FolderViewName, StringComparison.Ordinal))
        {
            _unfilteredItems = CreateFolderRows();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        if (string.Equals(_currentView, AllLocalFilesViewName, StringComparison.Ordinal)
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            ViewHeading.Text = "Biblioteka — Wszystkie pliki";
            _unfilteredItems = ActiveLocalItems()
                .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Source ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText))
                .ToList();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        IEnumerable<MediaItem> items = _sessions.Current.Items;
        if (_currentView == "Ulubione") items = items.Where(item => item.IsFavorite);
        if (_currentView == "Playlisty") items = items.Where(item => item.Kind == MediaItemKind.Playlist);
        if (_currentView == "Biblioteka")
        {
            items = items.Where(item => item.IsInLibrary);
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
            {
                items = items
                    .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase);
            }
        }
        if (_currentView == "Kolejka") items = items.Where(item => item.IsInQueue || item.IsPlayNext);
        if (_currentView == "Albumy") items = items.Where(item => item.Kind == MediaItemKind.Album);
        if (_currentView == "Historia odtwarzania")
        {
            var itemsById = _sessions.Current.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
            items = _playbackHistory.GetItemIds(_sessions.Current.Id)
                .Select(itemId => itemsById.GetValueOrDefault(itemId))
                .Where(item => item is not null)
                .Select(item => item!);
        }
        _unfilteredItems = items
            .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText))
            .ToList();
        ApplyFilter(preferredItemId, fallbackIndex);
    }

    private List<MediaItemRow> CreateFolderRows()
    {
        if (!string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            ViewHeading.Text = "Foldery Biblioteki";
            return [];
        }

        var sources = _state.LocalMedia.FolderSources
            .OrderBy(source => source.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var currentPath = _state.LocalMedia.CurrentFolderPath;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            ViewHeading.Text = "Biblioteka — Foldery";
            var rootRows = sources
                .Select(source => CreateFolderRow(source.Path, source.DisplayName))
                .ToList();
            rootRows.AddRange(ActiveLocalItems()
                .Where(item => item.Source is { Length: > 0 } path
                    && !sources.Any(source => IsSameOrDescendantPath(path, source.Path)))
                .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText)));
            return rootRows;
        }

        var sourceRoot = sources
            .Where(source => IsSameOrDescendantPath(currentPath, source.Path))
            .OrderByDescending(source => source.Path.Length)
            .FirstOrDefault();
        if (sourceRoot is null)
        {
            _state.LocalMedia.CurrentFolderPath = null;
            ViewHeading.Text = "Biblioteka — Foldery";
            return CreateFolderRows();
        }

        ViewHeading.Text = $"Biblioteka — Foldery — {GetFolderDisplayName(currentPath)}";
        var childFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directFiles = new List<MediaItem>();
        foreach (var item in ActiveLocalItems())
        {
            if (!TryGetLocalPath(item.Source, out var itemPath)
                || !IsSameOrDescendantPath(itemPath, currentPath))
            {
                continue;
            }

            var parent = Path.GetDirectoryName(itemPath);
            if (string.Equals(parent, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                directFiles.Add(item);
                continue;
            }

            var relative = Path.GetRelativePath(currentPath, itemPath);
            var firstSeparator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            if (firstSeparator <= 0) continue;
            var childName = relative[..firstSeparator];
            var childPath = Path.Combine(currentPath, childName);
            childFolders.TryAdd(childPath, childName);
        }

        var rows = childFolders
            .OrderBy(pair => pair.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(pair => CreateFolderRow(pair.Key, pair.Value))
            .ToList();
        rows.AddRange(directFiles
            .OrderBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText)));
        return rows;
    }

    private static MediaItemRow CreateFolderRow(string path, string displayName)
    {
        var item = new MediaItem
        {
            Id = FolderRowId(path),
            Title = displayName,
            Kind = MediaItemKind.Folder,
            Source = path
        };
        return new MediaItemRow(item, $"{displayName}, folder", displayName, folderPath: path);
    }

    private static string FolderRowId(string path) => $"folder:{path.ToUpperInvariant()}";

    private static string GetFolderDisplayName(string path)
    {
        var displayName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.IsNullOrWhiteSpace(displayName) ? path : displayName;
    }

    private static bool IsSameOrDescendantPath(string candidate, string root)
    {
        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private MediaItemRow CreateBookmarkRow(BookmarkEntry bookmark)
    {
        var targetSession = _sessions.FindSession(bookmark.SessionId);
        var targetItem = targetSession?.Items.FirstOrDefault(item =>
            string.Equals(item.Id, bookmark.ItemId, StringComparison.Ordinal));
        targetItem ??= new MediaItem
        {
            Id = bookmark.ItemId,
            Title = bookmark.ItemTitle
        };
        var rowItem = new MediaItem
        {
            Id = $"bookmark:{bookmark.Id}",
            Title = bookmark.ItemTitle
        };
        var sessionName = string.IsNullOrWhiteSpace(bookmark.SessionName)
            ? bookmark.SessionId
            : bookmark.SessionName;
        var namePart = string.IsNullOrWhiteSpace(bookmark.Name) ? string.Empty : $", {bookmark.Name}";
        var label = $"{bookmark.ItemTitle}, {FormatBookmarkCreatedDate(bookmark.CreatedUtcTicks)}, {CommandRouter.FormatTime(TimeSpan.FromTicks(bookmark.PositionTicks))}{namePart}, {sessionName}, zakładka";
        return new MediaItemRow(rowItem, label, bookmark.ItemTitle, targetItem, bookmark);
    }

    private static string FormatBookmarkCreatedDate(long utcTicks)
    {
        if (utcTicks <= 0) return "data utworzenia nieznana";
        try
        {
            var localDate = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
            return $"utworzono {localDate.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("pl-PL"))}";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "data utworzenia nieznana";
        }
    }

    private void ApplyFilter(string? preferredItemId = null, int? fallbackIndex = null)
    {
        ResetTypeAhead();
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
        if (_playerViewActive)
        {
            FocusPlayerView();
            return;
        }
        if (MediaList.Items.Count > 0 && MediaList.SelectedIndex < 0) MediaList.SelectedIndex = 0;
        if (MediaList.SelectedItem is not null) MediaList.ScrollIntoView(MediaList.SelectedItem);
        MediaList.UpdateLayout();
        if (MediaList.ItemContainerGenerator.ContainerFromItem(MediaList.SelectedItem) is ListBoxItem item)
        {
            ApplyFocusContext(item);
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        MediaList.Focus();
        Keyboard.Focus(MediaList);
    }

    private void PrepareSearchReturnContext(string itemId)
    {
        ClearFocusContext();
        _focusContextItemId = itemId;
        _focusContextPrefix = _sessions.Current.DisplayName;
    }

    private void PrepareViewFocusContext(string viewName)
    {
        PrepareSelectedItemFocusContext(viewName);
    }

    private void PrepareSelectedItemFocusContext(string prefix)
    {
        ClearFocusContext();
        if (SelectedItem is { } item)
        {
            _focusContextItemId = item.Id;
            _focusContextPrefix = prefix;
            return;
        }

        AutomationProperties.SetName(MediaList, $"{prefix}, lista pusta");
    }

    private void ApplyFocusContext(ListBoxItem container)
    {
        if (_focusContextItemId is null
            || _focusContextPrefix is null
            || container.Content is not MediaItemRow row
            || row.Item.Id != _focusContextItemId)
        {
            return;
        }

        AutomationProperties.SetName(container, $"{_focusContextPrefix}, {row.Label}");
        _focusContextContainer = container;
    }

    private void ClearFocusContext()
    {
        _focusContextContainer?.ClearValue(AutomationProperties.NameProperty);
        MediaList.ClearValue(AutomationProperties.NameProperty);
        _focusContextContainer = null;
        _focusContextItemId = null;
        _focusContextPrefix = null;
    }

    private void MediaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_restoringSessionNavigation && !_playerViewActive && SelectedItem is { } selected)
        {
            GetSessionNavigationState(_sessions.Current.Id).SelectedItemIds[_currentView] = selected.Id;
        }
        if (_focusContextContainer is null) return;
        if (SelectedItem?.Id == _focusContextItemId) return;
        ClearFocusContext();
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
        ConfigureLocalSourceWatchers();
        _ = SynchronizeLocalSourcesAsync(announceResult: false);
        if (_state.Settings.StartupTarget == StartupTarget.SessionList)
        {
            Dispatcher.BeginInvoke(ShowSessionList, DispatcherPriority.ContextIdle);
            return;
        }
        if (!_playerViewActive)
        {
            var context = string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal)
                ? _sessions.Current.DisplayName
                : $"{_currentView}, {_sessions.Current.DisplayName}";
            PrepareViewFocusContext(context);
        }
        Dispatcher.BeginInvoke(FocusMediaList, DispatcherPriority.ContextIdle);
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        ReconcileCompletedExternalMoves();
        if (!_initialFocusApplied) return;
        if (Keyboard.FocusedElement is not null and not Menu and not MenuItem) return;
        Dispatcher.BeginInvoke(FocusMediaList, DispatcherPriority.ContextIdle);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // Persist when the user switches away or a modal dialog opens. This
        // supplements the timer without writing after every repeated seek.
        if (IsLoaded) TrySaveLocalMediaState(false);
    }

    private void ActivateSelected()
    {
        if (SelectedBookmark is { } bookmark)
        {
            ActivateBookmark(bookmark, SelectedItem?.Id);
            return;
        }

        var row = MediaList.SelectedItem as MediaItemRow;
        if (row?.FolderPath is { } folderPath)
        {
            OpenFolderPath(folderPath);
            return;
        }

        var item = row?.Item;
        if (item is null) return;
        if (item.Kind is MediaItemKind.Track or MediaItemKind.Station)
        {
            var session = _sessions.Current;
            if (session.CurrentItem.Id != item.Id || !session.IsPlaying)
            {
                session.Play(item);
                RecordPlayback(session, item);
            }
            RefreshPlaybackIndicators();
            ShowPlayerView();
            return;
        }
        NavigateTo(item.Title);
        Announce($"{item.KindLabel}: {item.Title}. {FormatItemCount(_unfilteredItems.Count)}, {FormatDurationWords(item.Duration)}");
    }

    private void OpenFolderPath(string folderPath)
    {
        _state.LocalMedia.CurrentFolderPath = folderPath;
        GetSessionNavigationState("local").Filters[FolderViewName] = string.Empty;
        RestoreFilterForCurrentView(GetSessionNavigationState("local"));
        RefreshCurrentView();
        PrepareViewFocusContext($"Foldery, {GetFolderDisplayName(folderPath)}");
        TrySaveLocalMediaState(false);
        RestoreMediaListFocusAfterRefresh();
    }

    private void NavigateToParentFolder()
    {
        var currentPath = _state.LocalMedia.CurrentFolderPath;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            Announce("To jest lista źródeł folderów");
            return;
        }

        var source = _state.LocalMedia.FolderSources
            .Where(candidate => IsSameOrDescendantPath(currentPath, candidate.Path))
            .OrderByDescending(candidate => candidate.Path.Length)
            .FirstOrDefault();
        var exitedFolderId = FolderRowId(currentPath);
        _state.LocalMedia.CurrentFolderPath = source is null
            || string.Equals(currentPath, source.Path, StringComparison.OrdinalIgnoreCase)
                ? null
                : Path.GetDirectoryName(currentPath);
        GetSessionNavigationState("local").Filters[FolderViewName] = string.Empty;
        RestoreFilterForCurrentView(GetSessionNavigationState("local"));
        RefreshCurrentView(preferredItemId: exitedFolderId);
        PrepareViewFocusContext(_state.LocalMedia.CurrentFolderPath is null
            ? "Foldery, źródła"
            : $"Foldery, {GetFolderDisplayName(_state.LocalMedia.CurrentFolderPath)}");
        TrySaveLocalMediaState(false);
        RestoreMediaListFocusAfterRefresh();
    }

    private void ActivateBookmark(BookmarkEntry bookmark, string? bookmarkRowId)
    {
        var session = _sessions.FindSession(bookmark.SessionId);
        var item = session?.Items.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, bookmark.ItemId, StringComparison.Ordinal));
        if (session is null || item is null)
        {
            Announce($"Materiał zakładki jest obecnie niedostępny: {bookmark.ItemTitle}");
            return;
        }

        CaptureCurrentSessionNavigationState();
        _sessions.SelectSession(session.Id);
        var navigation = GetSessionNavigationState(session.Id);
        _currentView = BookmarkViewName;
        navigation.CurrentView = BookmarkViewName;
        navigation.PlayerActive = false;
        if (!string.IsNullOrWhiteSpace(bookmarkRowId))
        {
            navigation.SelectedItemIds[BookmarkViewName] = bookmarkRowId;
        }
        session.Play(item);
        var target = TimeSpan.FromTicks(bookmark.PositionTicks);
        if (item.Duration > TimeSpan.Zero && target > item.Duration) target = item.Duration;
        session.SetPosition(target);
        RecordPlayback(session, item);
        if (string.Equals(session.Id, "local", StringComparison.Ordinal)) TrySaveLocalMediaState(false);
        ShowPlayerView();
    }

    private void RemoveSelected()
    {
        if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            RemoveSelectedBookmarks();
            return;
        }

        var selectedRows = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .ToArray();
        if (string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
            && selectedRows.Any(row => row.FolderPath is not null))
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Delete nie usuwa folderu ani źródła. Enter otwiera folder; zarządzanie źródłami będzie osobnym poleceniem"),
                DispatcherPriority.ContextIdle);
            return;
        }

        var items = selectedRows
            .Select(row => row.Item)
            .DistinctBy(item => item.Id)
            .ToArray();
        if (items.Length == 0)
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Brak elementu do usunięcia"),
                DispatcherPriority.ContextIdle);
            return;
        }
        var previousIndex = MediaList.SelectedIndex;

        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
            && string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal))
        {
            RemoveLocalCatalogItems(items, previousIndex);
            return;
        }

        if (_currentView is not ("Ulubione" or "Biblioteka" or "Kolejka" or FolderViewName or AllLocalFilesViewName))
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Usuwanie jest dostępne w widokach Foldery Biblioteki, Wszystkie pliki, Ulubione, Biblioteka i Kolejka"),
                DispatcherPriority.ContextIdle);
            return;
        }

        if (_currentView is FolderViewName or AllLocalFilesViewName)
        {
            items = items.Where(item => item.IsInLibrary).ToArray();
            if (items.Length == 0)
            {
                RestoreMediaListFocusAfterRefresh();
                Dispatcher.BeginInvoke(
                    () => Announce("Zaznaczone pliki są już poza biblioteką. Pozostają dostępne w swoich folderach"),
                    DispatcherPriority.ContextIdle);
                return;
            }
        }

        var previousMemberships = items
            .Select(item => (Item: item, Previous: MediaMembershipState.From(item)))
            .ToArray();
        AnchorMediaListFocus();

        foreach (var item in items)
        {
            if (_currentView == "Ulubione")
            {
                item.IsFavorite = false;
            }
            else if (_currentView is "Biblioteka" or FolderViewName or AllLocalFilesViewName)
            {
                item.IsInLibrary = false;
            }
            else if (_currentView == "Kolejka")
            {
                item.IsInQueue = false;
                item.IsPlayNext = false;
            }
        }
        var undoAnnouncement = items.Length == 1
            ? _currentView switch
            {
                "Ulubione" => $"Przywrócono w ulubionych: {items[0].Title}",
                "Biblioteka" => $"Przywrócono w bibliotece: {items[0].Title}",
                FolderViewName => $"Przywrócono w bibliotece: {items[0].Title}",
                AllLocalFilesViewName => $"Przywrócono w bibliotece: {items[0].Title}",
                "Kolejka" => $"Przywrócono w kolejce: {items[0].Title}",
                _ => throw new InvalidOperationException($"Nieobsługiwany widok usuwania: {_currentView}")
            }
            : $"Przywrócono w widoku {_currentView}: {FormatItemCount(items.Length)}";
        _membershipHistory.RecordBatch(
            _sessions.Current.Id,
            previousMemberships,
            undoAnnouncement,
            ++_undoSequence);
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            if (_currentView is FolderViewName or AllLocalFilesViewName)
            {
                AddLocalExclusions(items);
                RefreshLocalSessionItems();
            }
            TrySaveLocalMediaState(false);
        }
        RefreshCurrentView(previousIndex);
        RestoreMediaListFocusAfterRefresh();
        var removedLabel = items.Length == 1 ? items[0].Title : FormatItemCount(items.Length);
        var announcement = _currentView is FolderViewName or AllLocalFilesViewName
            ? items.Length == 1
                ? $"Usunięto z biblioteki: {removedLabel}. Plik pozostaje w folderze"
                : $"Usunięto z biblioteki: {removedLabel}. Pliki pozostają w folderach"
            : MediaList.Items.Count == 0
                ? $"Usunięto: {removedLabel}. Lista jest pusta"
                : $"Usunięto: {removedLabel}";
        Dispatcher.BeginInvoke(() => Announce(announcement), DispatcherPriority.ContextIdle);
    }

    private static string BuildUndoAnnouncement(
        string commandId,
        MediaItem item,
        MediaMembershipState previousState) => commandId switch
        {
            CommandIds.ToggleFavorite when previousState.IsFavorite =>
                $"Przywrócono w ulubionych: {item.Title}",
            CommandIds.ToggleFavorite =>
                $"Cofnięto dodanie do ulubionych: {item.Title}",
            CommandIds.ToggleLibrary when previousState.IsInLibrary =>
                $"Przywrócono w bibliotece: {item.Title}",
            CommandIds.ToggleLibrary =>
                $"Cofnięto dodanie do biblioteki: {item.Title}",
            CommandIds.AddQueue when previousState.IsInQueue || previousState.IsPlayNext =>
                $"Przywrócono w kolejce: {item.Title}",
            CommandIds.AddQueue =>
                $"Cofnięto dodanie do kolejki: {item.Title}",
            CommandIds.TogglePlayNext when previousState.IsPlayNext =>
                $"Przywrócono jako następne: {item.Title}",
            CommandIds.TogglePlayNext =>
                $"Cofnięto odtwarzanie jako następne: {item.Title}",
            _ => $"Cofnięto ostatnią zmianę: {item.Title}"
        };

    private void UndoLastMembershipChange()
    {
        // Ctrl+Z is a list command everywhere except inside the filter editor.
        // Anchor unconditionally so an empty undo history cannot leave focus on
        // an action button or allow WPF to move it into the main menu.
        AnchorMediaListFocus();

        var membershipCandidate = _membershipHistory.Peek();
        var catalogCandidate = _localCatalogHistory.TryPeek(out var localUndo) ? localUndo : null;
        if (catalogCandidate is not null
            && (membershipCandidate is null || catalogCandidate.Sequence > membershipCandidate.Sequence))
        {
            UndoLocalCatalogRemoval(_localCatalogHistory.Pop());
            return;
        }

        var undo = _membershipHistory.Undo();
        if (undo is null)
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Brak zmian do cofnięcia"),
                DispatcherPriority.ContextIdle);
            return;
        }

        if (string.Equals(undo.SessionId, "local", StringComparison.Ordinal))
        {
            foreach (var entry in undo.Items)
            {
                if (entry.Item.IsInLibrary)
                    RemoveLocalExclusions([entry.Item.Source ?? string.Empty]);
                else
                    AddLocalExclusions([entry.Item]);
            }
            RefreshLocalSessionItems();
        }

        if (_sessions.Current.Id == undo.SessionId)
        {
            RefreshCurrentView();
            SelectMediaItems(undo.Items.Select(item => item.Item.Id));
        }
        if (string.Equals(undo.SessionId, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(false);
        }
        RestoreMediaListFocusAfterRefresh();

        Dispatcher.BeginInvoke(
            () => Announce(undo.Announcement),
            DispatcherPriority.ContextIdle);
    }

    private void UndoLocalCatalogRemoval(LocalCatalogUndo undo)
    {
        var local = _sessions.FindSession("local");
        var restoredDetachedSession = false;
        if (local is null && undo.DetachedSession is not null)
        {
            local = _sessions.RestoreTransientSession(undo.DetachedSession, makeCurrent: true);
            restoredDetachedSession = true;
        }
        if (local is null)
        {
            _localCatalogHistory.Push(undo);
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Nie można przywrócić plików: sesja lokalna jest niedostępna"),
                DispatcherPriority.ContextIdle);
            return;
        }

        foreach (var entry in undo.CatalogItems.OrderBy(entry => entry.Index))
        {
            if (_localItems.All(item => !string.Equals(item.Id, entry.Item.Id, StringComparison.Ordinal)))
            {
                _localItems.Insert(Math.Clamp(entry.Index, 0, _localItems.Count), entry.Item);
            }
        }
        RemoveLocalExclusions(undo.CatalogItems.Select(entry => entry.Item.Source ?? string.Empty));
        if (!restoredDetachedSession)
        {
            local.RestoreItems(undo.SessionItems);
            if (undo.DetachedSession is { Session: { } detached })
            {
                foreach (var entry in undo.SessionItems)
                {
                    if (detached.RememberedPositions.TryGetValue(entry.Item.Id, out var position))
                    {
                        local.SetRememberedPosition(entry.Item.Id, position);
                    }
                }
            }
        }

        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            if (restoredDetachedSession) RestoreCurrentSessionNavigationState();
            else RefreshCurrentView();
            SelectMediaItems(undo.CatalogItems.Select(entry => entry.Item.Id));
        }
        TrySaveLocalMediaState(false);
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(() => Announce(undo.Announcement), DispatcherPriority.ContextIdle);
    }

    private void SelectMediaItem(string itemId)
    {
        var row = MediaList.Items
            .OfType<MediaItemRow>()
            .FirstOrDefault(candidate => candidate.Item.Id == itemId);
        if (row is null) return;
        MediaList.SelectedItem = row;
        MediaList.ScrollIntoView(row);
    }

    private void SelectMediaItems(IEnumerable<string> itemIds)
    {
        var ids = itemIds.ToHashSet(StringComparer.Ordinal);
        var rows = MediaList.Items
            .OfType<MediaItemRow>()
            .Where(row => ids.Contains(row.Item.Id))
            .ToArray();
        if (rows.Length == 0) return;

        MediaList.SelectedItems.Clear();
        foreach (var row in rows) MediaList.SelectedItems.Add(row);
        MediaList.ScrollIntoView(rows[0]);
    }

    private void OpenSettings(SettingsTarget initialTarget = SettingsTarget.General)
    {
        TrySaveLocalMediaState(false);
        var dialog = new SettingsWindow(_state, _store, initialTarget) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultState is null)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }
        _state = dialog.ResultState;
        _playbackHistory = new PlaybackHistory(_state.PlaybackHistory);
        _bookmarkIndex = new BookmarkIndex(_state.Bookmarks);
        _store.Save(_state);
        ApplyDetailedHints();
        RebuildCore();
        RestoreCurrentSessionNavigationState();
        string announcement;
        try
        {
            RegisterConfiguredPrefix();
            announcement = "Zapisano ustawienia";
        }
        catch (Exception exception)
        {
            announcement = $"Ustawienia zapisane, ale prefiks jest niedostępny: {exception.Message}";
        }
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(() => Announce(announcement), DispatcherPriority.ContextIdle);
    }

    private void RemoveSelectedBookmarks()
    {
        var rows = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .Where(row => row.Bookmark is not null)
            .ToArray();
        if (rows.Length == 0)
        {
            Announce("Brak zakładki do usunięcia");
            return;
        }

        var previousIndex = MediaList.SelectedIndex;
        AnchorMediaListFocus();
        var removed = _bookmarkIndex.Remove(rows.Select(row => row.Bookmark!.Id));
        _store.Save(_state);
        RefreshCurrentView(previousIndex);
        RestoreMediaListFocusAfterRefresh();
        var message = removed == 1 ? "Usunięto zakładkę" : $"Usunięto zakładki: {removed}";
        Dispatcher.BeginInvoke(() => Announce(message), DispatcherPriority.ContextIdle);
    }

    private void MoveSelectedLocalFilesToRecycleBin()
    {
        if (!string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            Announce("Przenoszenie do Kosza jest dostępne tylko dla plików lokalnych");
            return;
        }

        var items = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .Select(row => row.Item)
            .Where(item => TryGetLocalPath(item.Source, out _))
            .DistinctBy(item => item.Id)
            .ToArray();
        if (items.Length == 0)
        {
            Announce("Brak pliku do przeniesienia do Kosza");
            return;
        }

        var label = items.Length == 1 ? items[0].Title : FormatFileCount(items.Length);
        var confirmation = MessageBox.Show(
            $"Przenieść do Kosza: {label}?\n\nPliki zostaną usunięte z AMC. Tej operacji nie można cofnąć skrótem Ctrl+Z; można użyć systemowego Kosza.",
            "Przenieś pliki do Kosza",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        var session = _sessions.Current;
        if (items.Any(item => string.Equals(item.Id, session.CurrentItem.Id, StringComparison.Ordinal)))
        {
            session.StopPlayback();
        }

        var removed = new List<MediaItem>();
        var failures = new List<string>();
        foreach (var item in items)
        {
            if (!TryGetLocalPath(item.Source, out var path)) continue;
            try
            {
                if (File.Exists(path))
                {
                    WindowsRecycleBin.MoveFile(path, new WindowInteropHelper(this).Handle);
                }
                removed.Add(item);
            }
            catch (Exception exception) when (WindowsRecycleBin.IsExpectedFailure(exception))
            {
                failures.Add($"{item.Title}: {exception.Message} (kod 0x{exception.HResult:X8})");
            }
        }

        if (removed.Count > 0)
        {
            _playbackHistory.Remove("local", removed.Select(item => item.Id));
            _playbackHistoryCursors.Remove("local");
            RemoveLocalCatalogItems(
                removed,
                MediaList.SelectedIndex,
                recordUndo: false,
                filesRemainOnDisk: false);
        }
        else
        {
            RestoreMediaListFocusAfterRefresh();
        }

        if (failures.Count > 0)
        {
            Dispatcher.BeginInvoke(
                () => AnnounceEssential($"Nie przeniesiono do Kosza: {string.Join("; ", failures)}"),
                DispatcherPriority.ContextIdle);
        }
    }

    private void RemoveLocalCatalogItems(
        IReadOnlyList<MediaItem> items,
        int previousIndex,
        bool recordUndo = true,
        bool filesRemainOnDisk = true,
        bool announceNextItem = false)
    {
        var session = _sessions.Current;
        CaptureCurrentSessionNavigationState();
        var catalogEntries = items
            .Select(item => new RemovedMediaItem(item, _localItems.FindIndex(candidate => candidate.Id == item.Id)))
            .Where(entry => entry.Index >= 0)
            .OrderBy(entry => entry.Index)
            .ToArray();
        if (catalogEntries.Length == 0) return;
        if (filesRemainOnDisk) AddLocalExclusions(items);
        else RemoveLocalExclusions(items.Select(item => item.Source ?? string.Empty));

        var wasPlaying = session.IsPlaying;
        var currentRemoved = items.Any(item => string.Equals(item.Id, session.CurrentItem.Id, StringComparison.Ordinal));
        var removeWholeSession = items.Count >= session.Items.Count;
        AnchorMediaListFocus();
        RemovedSessionRegistration? detachedSession = null;
        IReadOnlyList<RemovedMediaItem> sessionEntries;
        if (removeWholeSession)
        {
            session.RememberCurrentPosition();
            if (session.IsPlaying) session.TogglePlayback();
            sessionEntries = session.Items
                .Select((item, index) => new RemovedMediaItem(item, index))
                .ToArray();
            detachedSession = _sessions.RemoveTransientSession(session.Id);
        }
        else
        {
            sessionEntries = session.RemoveItems(items.Select(item => item.Id));
        }
        if (sessionEntries.Count == 0 || removeWholeSession && detachedSession is null)
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Nie udało się usunąć zaznaczonych plików z AMC"),
                DispatcherPriority.ContextIdle);
            return;
        }

        foreach (var entry in catalogEntries.OrderByDescending(entry => entry.Index))
        {
            _localItems.RemoveAt(entry.Index);
        }
        if (recordUndo)
        {
            _localCatalogHistory.Push(new LocalCatalogUndo(
                ++_undoSequence,
                catalogEntries,
                sessionEntries,
                items.Count == 1
                    ? $"Przywrócono w AMC: {items[0].Title}"
                    : $"Przywrócono w AMC: {FormatItemCount(items.Count)}",
                detachedSession));
        }

        if (detachedSession is not null)
        {
            RestoreCurrentSessionNavigationState();
        }
        else
        {
            RefreshCurrentView(previousIndex);
        }
        TrySaveLocalMediaState(false);
        RefreshPlaybackIndicators();
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        RestoreMediaListFocusAfterRefresh();

        var removedLabel = items.Count == 1 ? items[0].Title : FormatItemCount(items.Count);
        var playbackNote = wasPlaying && currentRemoved ? ". Odtwarzanie wstrzymano" : string.Empty;
        var nextItemNote = announceNextItem
            ? detachedSession is null
                ? $". Następny element: {session.CurrentItem.Title}"
                : $". Przejście do sesji: {_sessions.Current.DisplayName}"
            : string.Empty;
        var retainedOnDisk = items.Count == 1 ? "Plik pozostał na dysku" : "Pliki pozostały na dysku";
        var actionAnnouncement = filesRemainOnDisk
            ? $"Usunięto z AMC: {removedLabel}. {retainedOnDisk}{playbackNote}{nextItemNote}"
            : $"Przeniesiono do Kosza i usunięto z AMC: {removedLabel}{playbackNote}{nextItemNote}";
        Dispatcher.BeginInvoke(
            () => Announce(actionAnnouncement),
            DispatcherPriority.ContextIdle);
    }

    private void RemoveCurrentLocalItemFromPlayer()
    {
        if (!_playerViewActive
            || !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
            || !TryGetLocalPath(_sessions.Current.CurrentItem.Source, out _))
        {
            return;
        }

        var session = _sessions.Current;
        var item = session.CurrentItem;
        var previous = MediaMembershipState.From(item);
        var wasPlaying = session.IsPlaying;
        item.IsInLibrary = false;
        AddLocalExclusions([item]);
        _membershipHistory.Record(
            "local",
            item,
            previous,
            $"Przywrócono w bibliotece: {item.Title}",
            ++_undoSequence);
        RefreshLocalSessionItems();
        TrySaveLocalMediaState(false);
        if (session.HasItems)
        {
            UpdatePlayerView(true);
            UpdatePlaybackStatusBar();
            UpdateWindowTitle();
            Announce($"Wykluczono z Biblioteki: {item.Title}. Następny element: {session.CurrentItem.Title}"
                + (wasPlaying ? ". Odtwarzanie zatrzymano" : string.Empty));
        }
        else
        {
            ReturnFromPlayerToList();
            Announce($"Wykluczono z Biblioteki: {item.Title}. Biblioteka jest pusta");
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

    private string FormatItem(MediaItem item) => FormatItem(item, true);

    private string FormatItem(MediaItem item, bool includeKind)
    {
        var fields = includeKind
            ? _state.Settings.Lists.FieldOrder
            : _state.Settings.Lists.FieldOrder.Where(field => field != MediaItemField.Kind);
        return MediaItemFormatter.Format(item, fields);
    }

    private static string FormatFileCount(int count)
    {
        if (count == 1) return "1 plik";
        var lastTwoDigits = count % 100;
        var lastDigit = count % 10;
        return lastDigit is >= 2 and <= 4 && lastTwoDigits is not (>= 12 and <= 14)
            ? $"{count} pliki"
            : $"{count} plików";
    }

    private void NavigateTo(string viewName)
    {
        CaptureCurrentSessionNavigationState();
        HidePlayerForBrowserNavigation();
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        var history = GetSessionViewHistory(_sessions.Current.Id);
        if (!string.Equals(viewName, _currentView, StringComparison.Ordinal))
        {
            history.Back.Push(_currentView);
            history.Forward.Clear();
            _currentView = viewName;
        }
        navigation.CurrentView = _currentView;
        navigation.PlayerActive = false;
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: navigation.SelectedItemIds.GetValueOrDefault(_currentView));
    }

    private void NavigateBack()
    {
        if (_playerViewActive)
        {
            ReturnFromPlayerToList();
            return;
        }
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        var history = GetSessionViewHistory(_sessions.Current.Id);
        CaptureCurrentSessionNavigationState();
        if (history.Back.Count == 0)
        {
            if (HistoryMessagesEnabled)
            {
                Announce($"Brak poprzedniego widoku w sesji {_sessions.Current.DisplayName}");
            }
            return;
        }
        history.Forward.Push(_currentView);
        _currentView = history.Back.Pop();
        navigation.CurrentView = _currentView;
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: navigation.SelectedItemIds.GetValueOrDefault(_currentView));
        PrepareHistoryFocusContext("Wstecz");
        RestoreMediaListFocusAfterRefresh();
    }

    private void NavigateForward()
    {
        if (_playerViewActive) return;
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        var history = GetSessionViewHistory(_sessions.Current.Id);
        CaptureCurrentSessionNavigationState();
        if (history.Forward.Count == 0)
        {
            if (HistoryMessagesEnabled)
            {
                Announce($"Brak następnego widoku w sesji {_sessions.Current.DisplayName}");
            }
            return;
        }
        history.Back.Push(_currentView);
        _currentView = history.Forward.Pop();
        navigation.CurrentView = _currentView;
        RestoreFilterForCurrentView(navigation);
        RefreshCurrentView(preferredItemId: navigation.SelectedItemIds.GetValueOrDefault(_currentView));
        PrepareHistoryFocusContext("Naprzód");
        RestoreMediaListFocusAfterRefresh();
    }

    private bool HistoryMessagesEnabled =>
        _state.Settings.Messages.Enabled && _state.Settings.Messages.HistoryMessages;

    private void PrepareHistoryFocusContext(string direction)
    {
        if (!_state.Settings.Messages.Enabled) return;
        var context = string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal)
            ? _sessions.Current.DisplayName
            : $"{_currentView}, {_sessions.Current.DisplayName}";
        PrepareViewFocusContext(HistoryMessagesEnabled ? $"{direction}, {context}" : context);
    }

    private string FormatListItem(MediaItem item)
    {
        var homogeneousView = _currentView is "Albumy" or "Playlisty";
        var label = FormatItem(item, !homogeneousView);
        var session = _sessions.Current;
        var isCurrent = string.Equals(session.CurrentItem.Id, item.Id, StringComparison.Ordinal);
        if (isCurrent && session.IsPlaying) return $"Odtwarzany, {label}";
        if (isCurrent && session.Position > TimeSpan.Zero) return $"Wstrzymany, {label}";
        var lastPlayedId = _playbackHistory.GetItemIds(session.Id).FirstOrDefault();
        return string.Equals(lastPlayedId, item.Id, StringComparison.Ordinal)
            ? $"Ostatnio odtwarzany, {label}"
            : label;
    }

    private void RefreshPlaybackIndicators()
    {
        foreach (var row in _unfilteredItems)
        {
            if (row.Bookmark is not null) continue;
            row.UpdateLabel(FormatListItem(row.Item));
        }
    }

    private void UpdateWindowTitle()
    {
        var session = _sessions.Current;
        var area = _playerViewActive ? "Odtwarzacz" : _currentView;
        Title = !_playerViewActive && string.Equals(area, DefaultBrowserView, StringComparison.Ordinal)
            ? $"{session.CurrentItem.Title} — {session.DisplayName} — AMC {AppDisplayVersion}"
            : $"{session.CurrentItem.Title} — {area} — {session.DisplayName} — AMC {AppDisplayVersion}";
        AutomationProperties.SetName(this, Title);
    }

    private static bool IsSearchView(string viewName) =>
        viewName is "Szukaj w bieżącej usłudze" or "Szukaj we wszystkich usługach";

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmKeyDown
            || Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            return IntPtr.Zero;
        }

        var modifiers = Keyboard.Modifiers;
        Action? action = (modifiers, wParam.ToInt32()) switch
        {
            (ModifierKeys.Control, VirtualKeyZ) => UndoLastMembershipChange,
            (ModifierKeys.Control | ModifierKeys.Shift, VirtualKeyE) => () => ExecuteCommand(CommandIds.TimeElapsed),
            (ModifierKeys.Control | ModifierKeys.Shift, VirtualKeyG) => () => ExecuteCommand(CommandIds.SettingsToggleSeekMessages),
            (ModifierKeys.Control | ModifierKeys.Shift, VirtualKeyR) => () => ExecuteCommand(CommandIds.TimeRemaining),
            (ModifierKeys.Control | ModifierKeys.Shift, VirtualKeyT) => () => ExecuteCommand(CommandIds.TimeTotal),
            _ => null
        };
        if (action is null) return IntPtr.Zero;

        // These commands are caught at the window-message boundary so that
        // framework commands and screen-reader event ordering cannot consume
        // them before AMC. Text boxes retain their standard editing commands.
        handled = true;
        Dispatcher.BeginInvoke(action, DispatcherPriority.Input);
        return IntPtr.Zero;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var windowKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && windowKey == Key.F4)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (TryHandleLocalLibraryViewShortcut(e))
        {
            e.Handled = true;
            return;
        }

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

        if (e.Key == Key.F6 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            if (_playerViewActive && Keyboard.Modifiers == ModifierKeys.Shift)
                ReturnFromPlayerToList();
            else
                ShowPlayerView();
            e.Handled = true;
            return;
        }

        if (_playerViewActive
            && Keyboard.Modifiers == ModifierKeys.None
            && e.Key == Key.Delete)
        {
            RemoveCurrentLocalItemFromPlayer();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            // Menus retain their standard hierarchical Escape behavior: close
            // one submenu level at a time and restore the previous focus.
            if (MainMenu.IsKeyboardFocusWithin || Keyboard.FocusedElement is MenuItem)
            {
                return;
            }

            if (_playerViewActive)
            {
                e.Handled = true;
                ReturnFromPlayerToList();
                return;
            }

            // One predictable Escape rule for the whole main window: clear an
            // active filter, if any, and return to the media list.
            e.Handled = true;
            ReturnToMediaListFromEscape();
            return;
        }

        if (TryHandleLocalNavigationShortcut(e))
        {
            e.Handled = true;
            return;
        }

        if (TryHandlePlayerTransportShortcut(e))
        {
            e.Handled = true;
            return;
        }

        if (_playerViewActive && Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Space)
        {
            ExecuteCommand(CommandIds.PlayPause);
            e.Handled = true;
            return;
        }

        if (TryHandleItemActionShortcut(e))
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.O)
        {
            OpenLocalFolder();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            OpenLocalFiles();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt
            && Keyboard.FocusedElement is not MenuItem
            && !MainMenu.IsKeyboardFocusWithin
            && e.SystemKey is Key.Left or Key.Right)
        {
            if (e.SystemKey == Key.Left) NavigateBack();
            else NavigateForward();
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            if (e.Key is Key.Enter or Key.Down)
            {
                FocusFilterResults();
                e.Handled = true;
            }
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var itemCommandsAvailable = _playerViewActive || MediaList.IsKeyboardFocusWithin;
        if (itemCommandsAvailable
            && modifiers == ModifierKeys.Control
            && e.Key == Key.V
            && MediaList.IsKeyboardFocusWithin)
        {
            PasteClipboardFilesIntoCurrentView();
            e.Handled = true;
        }
        else if (itemCommandsAvailable
            && modifiers == ModifierKeys.Control
            && e.Key == Key.X
            && MediaList.IsKeyboardFocusWithin)
        {
            Announce(CutLocalFilesForExternalMove(ActionItems));
            e.Handled = true;
        }
        else if (itemCommandsAvailable
            && modifiers == ModifierKeys.Control
            && e.Key == Key.C
            && ActionItem is not null)
        {
            CopyActionItemName();
            e.Handled = true;
        }
        else if (itemCommandsAvailable
                 && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
                 && e.Key == Key.C
                 && ActionItem is not null)
        {
            CopyActionItemLocation();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Control && e.Key == Key.Z)
        {
            // Mark the keystroke handled before changing focus or raising the
            // live-region message, preventing WPF's built-in Undo command from
            // producing an additional English "Undo" announcement.
            e.Handled = true;
            UndoLastMembershipChange();
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
            if (_playerViewActive
                && (modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // Consume otherwise unassigned Control combinations inside the
                // player so framework focus navigation cannot expose a random
                // heading or button (for example after Ctrl+W).
                e.Handled = true;
            }
            return;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.E)
        {
            ExecuteCommand(CommandIds.TimeElapsed);
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.R)
        {
            ExecuteCommand(CommandIds.TimeRemaining);
            e.Handled = true;
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.T)
        {
            ExecuteCommand(CommandIds.TimeTotal);
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.Shift && e.Key == Key.Delete)
        {
            if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
                Announce("Shift+Delete nie usuwa pliku z listy zakładek. Delete usuwa samą zakładkę");
            else
                MoveSelectedLocalFilesToRecycleBin();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None
                 && e.Key == Key.Back
                 && string.Equals(_currentView, FolderViewName, StringComparison.Ordinal))
        {
            NavigateToParentFolder();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None && e.Key is Key.Delete or Key.Back)
        {
            RemoveSelected();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None && e.Key == Key.Space)
        {
            ExecuteCommand(CommandIds.PlayPause);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (modifiers == ModifierKeys.None
                || (modifiers == ModifierKeys.Control
                    && string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal)))
            {
                ActivateSelected();
            }
            else if (modifiers == ModifierKeys.Control) ExecuteCommand(CommandIds.ActivateSelected);
            else if (modifiers == ModifierKeys.Shift) ExecuteCommand(CommandIds.AddQueue);
            else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) ExecuteCommand(CommandIds.TogglePlayNext);
            e.Handled = true;
        }
    }

    private bool TryHandleLocalSessionShortcut(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!TryGetDigitKey(key, out var slot)) return false;
        ExecuteCommand(slot == 0 ? CommandIds.SessionList : CommandIds.SessionSlot(slot));
        return true;
    }

    private bool TryHandleLocalLibraryViewShortcut(KeyEventArgs e)
    {
        if (_playerViewActive || Keyboard.FocusedElement is System.Windows.Controls.TextBox) return false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D1)
        {
            ExecuteCommand(CommandIds.ViewFolders);
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D2)
        {
            ExecuteCommand(CommandIds.ViewAllLocalFiles);
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.None
            && key == Key.F5
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            ExecuteCommand(CommandIds.RefreshLocalLibrary);
            return true;
        }
        return false;
    }

    private static bool TryGetDigitKey(Key key, out int digit)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            digit = (int)key - (int)Key.D0;
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            digit = (int)key - (int)Key.NumPad0;
            return true;
        }

        digit = 0;
        return false;
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

    private bool TryHandleLocalNavigationShortcut(KeyEventArgs e)
    {
        var commandId = (Keyboard.Modifiers, e.Key) switch
        {
            (ModifierKeys.Control, Key.U) => CommandIds.ViewFavorites,
            (ModifierKeys.Control, Key.P) => CommandIds.ViewPlaylists,
            (ModifierKeys.Control, Key.L) => CommandIds.ViewLibrary,
            (ModifierKeys.Control, Key.Q) => CommandIds.ViewQueue,
            (ModifierKeys.Control, Key.H) => CommandIds.ViewHistory,
            (ModifierKeys.Control, Key.B) => CommandIds.ViewBookmarks,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.B) => CommandIds.AddNamedBookmark,
            (ModifierKeys.Control, Key.K) => CommandIds.FilterCurrent,
            (ModifierKeys.Control, Key.F) => CommandIds.SearchCurrent,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.A) => CommandIds.ViewAlbums,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.F) => CommandIds.SearchAll,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.G) => CommandIds.SettingsToggleSeekMessages,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.K) => CommandIds.CommandPalette,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.E) => CommandIds.TimeElapsed,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.R) => CommandIds.TimeRemaining,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.T) => CommandIds.TimeTotal,
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

    private void FocusFilter()
    {
        FilterBox.Focus();
        FilterBox.SelectAll();
        var announcement = _state.Settings.Messages.DetailedHints
            ? "Filtr listy. Wpisz tekst. Enter lub strzałka w dół przechodzi do wyników. Escape czyści filtr i wraca do listy"
            : "Filtr listy";
        Dispatcher.BeginInvoke(
            () => Announce(announcement),
            DispatcherPriority.ContextIdle);
    }

    private bool TryHandlePlayerTransportShortcut(KeyEventArgs e)
    {
        if (!_playerViewActive || !PlayerPanel.IsKeyboardFocusWithin) return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            NavigatePlaybackHistory(key == Key.Down ? 1 : -1);
            return true;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && TryGetDigitKey(key, out var digit))
        {
            ExecuteCommand(CommandIds.SeekPercent(digit * 10));
            return true;
        }

        var commandId = (Keyboard.Modifiers, key) switch
        {
            (ModifierKeys.None, Key.B) => CommandIds.AddBookmark,
            (ModifierKeys.Shift, Key.PageUp) => CommandIds.PreviousBookmark,
            (ModifierKeys.Shift, Key.PageDown) => CommandIds.NextBookmark,
            (ModifierKeys.None, Key.PageUp) => CommandIds.Previous,
            (ModifierKeys.None, Key.PageDown) => CommandIds.Next,
            (ModifierKeys.Control, Key.J) => CommandIds.SeekToTime,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.J) => CommandIds.SeekToPercentage,
            (ModifierKeys.None, Key.Left) => CommandIds.SeekBackward10,
            (ModifierKeys.None, Key.Right) => CommandIds.SeekForward10,
            (ModifierKeys.Shift, Key.Left) => CommandIds.SeekBackward30,
            (ModifierKeys.Shift, Key.Right) => CommandIds.SeekForward30,
            (ModifierKeys.Control, Key.Left) => CommandIds.SeekBackward60,
            (ModifierKeys.Control, Key.Right) => CommandIds.SeekForward60,
            (ModifierKeys.None, Key.Up) => CommandIds.VolumeUp5,
            (ModifierKeys.None, Key.Down) => CommandIds.VolumeDown5,
            (ModifierKeys.Shift, Key.Up) => CommandIds.VolumeUp1,
            (ModifierKeys.Shift, Key.Down) => CommandIds.VolumeDown1,
            (ModifierKeys.Shift, Key.OemComma) => CommandIds.PlaybackRateDown,
            (ModifierKeys.Shift, Key.OemPeriod) => CommandIds.PlaybackRateUp,
            (ModifierKeys.Control, Key.OemPeriod) => CommandIds.PlaybackRateReset,
            (ModifierKeys.None, Key.Home) => CommandIds.TrackStart,
            (ModifierKeys.None, Key.End) => CommandIds.TrackEnd,
            _ => null
        };
        if (commandId is null)
        {
            // Do not let unsupported arrow combinations invoke WPF spatial
            // focus navigation between player buttons. They have no transport
            // meaning until AMC assigns one explicitly.
            return key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown
                && Keyboard.Modifiers != ModifierKeys.None;
        }
        ExecuteCommand(commandId);
        return true;
    }

    private bool TryHandleItemActionShortcut(KeyEventArgs e)
    {
        if (!_playerViewActive && !MediaList.IsKeyboardFocusWithin) return false;

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Alt && e.SystemKey == Key.Enter)
        {
            ExecuteCommand(CommandIds.ItemProperties);
            return true;
        }

        var commandId = (modifiers, e.Key) switch
        {
            (ModifierKeys.Control | ModifierKeys.Shift, Key.U) => CommandIds.ToggleFavorite,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.P) => CommandIds.ManagePlaylists,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.Q) => CommandIds.AddQueue,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.L) => CommandIds.ToggleLibrary,
            (ModifierKeys.Shift, Key.Enter) => CommandIds.AddQueue,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.Enter) => CommandIds.TogglePlayNext,
            _ => null
        };
        if (commandId is null) return false;
        ExecuteCommand(commandId);
        return true;
    }

    private void ApplyDetailedHints()
    {
        var helpText = _state.Settings.Messages.DetailedHints
            ? "Wpisz tekst. Enter lub strzałka w dół przechodzi do wyników. Escape czyści filtr i wraca do listy."
            : string.Empty;
        System.Windows.Automation.AutomationProperties.SetHelpText(FilterBox, helpText);
    }

    private void ReturnToMediaListFromEscape()
    {
        AnchorMediaListFocus();
        if (FilterBox.Text.Length > 0)
        {
            FilterBox.Clear();
            StatusText.Text = "Filtr wyczyszczony";
            RestoreMediaListFocusAfterRefresh();
            return;
        }
        if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            LeaveBookmarkView();
            return;
        }
        RestoreMediaListFocusAfterRefresh();
    }

    private void LeaveBookmarkView()
    {
        var bookmarkNavigation = GetSessionNavigationState(_sessions.Current.Id);
        bookmarkNavigation.Filters[BookmarkViewName] = FilterBox.Text;
        if (SelectedItem is { } selectedBookmark)
        {
            bookmarkNavigation.SelectedItemIds[BookmarkViewName] = selectedBookmark.Id;
        }
        bookmarkNavigation.CurrentView = DefaultBrowserView;
        bookmarkNavigation.PlayerActive = false;

        var returnContext = _bookmarkReturnContext;
        _bookmarkReturnContext = null;
        var returnSession = returnContext is null
            ? _sessions.Current
            : _sessions.SelectSession(returnContext.SessionId) ?? _sessions.Current;
        var returnNavigation = GetSessionNavigationState(returnSession.Id);
        var returnView = returnContext?.ViewName;
        if (string.IsNullOrWhiteSpace(returnView)
            || string.Equals(returnView, BookmarkViewName, StringComparison.Ordinal))
        {
            returnView = DefaultBrowserView;
        }

        _currentView = returnView;
        returnNavigation.CurrentView = returnView;
        returnNavigation.PlayerActive = false;
        var history = GetSessionViewHistory(returnSession.Id);
        if (history.Back.Count > 0
            && string.Equals(history.Back.Peek(), returnView, StringComparison.Ordinal))
        {
            history.Back.Pop();
        }
        history.Forward.Clear();
        RestoreFilterForCurrentView(returnNavigation);
        var preferredItemId = returnContext?.SelectedItemId
            ?? returnNavigation.SelectedItemIds.GetValueOrDefault(returnView);
        RefreshCurrentView(preferredItemId: preferredItemId);
        var focusContext = string.Equals(returnView, DefaultBrowserView, StringComparison.Ordinal)
            ? returnSession.DisplayName
            : $"{returnView}, {returnSession.DisplayName}";
        PrepareViewFocusContext(focusContext);
        RestoreMediaListFocusAfterRefresh();
    }

    private string? ExecuteSearchResultAction(
        IReadOnlyList<SearchWindow.SearchResult> results,
        SearchResultAction action,
        bool _)
    {
        if (results.Count == 0) return "Brak zaznaczonego wyniku";
        if (action == SearchResultAction.CopyName)
        {
            _pendingExternalMoves.Clear();
            Clipboard.SetText(string.Join(Environment.NewLine, results.Select(result => result.Item.Title)));
            return results.Count == 1
                ? "Skopiowano nazwę"
                : $"Skopiowano nazwy: {FormatItemCount(results.Count)}";
        }
        if (action == SearchResultAction.CopyLocation)
        {
            return CopySearchResultLocations(results);
        }

        var result = results[0];
        var session = SelectSessionBrowserItem(result.SessionId, result.Item.Id);
        if (session is null) return "Wybrana sesja nie jest już dostępna";

        _capturedAnnouncement = null;
        _captureAnnouncements = true;
        try
        {
            switch (action)
            {
                case SearchResultAction.TogglePlayback:
                    ExecuteCommand(CommandIds.ActivateSelected);
                    break;
                case SearchResultAction.PlayNext:
                    ExecuteCommand(CommandIds.TogglePlayNext);
                    break;
                case SearchResultAction.Queue:
                    ExecuteCommand(CommandIds.AddQueue);
                    break;
                case SearchResultAction.Favorite:
                    ExecuteCommand(CommandIds.ToggleFavorite);
                    break;
                case SearchResultAction.Information:
                    ShowItemProperties();
                    break;
            }
        }
        finally
        {
            _captureAnnouncements = false;
        }

        var announcement = _capturedAnnouncement;
        _capturedAnnouncement = null;
        if (!string.IsNullOrWhiteSpace(announcement))
        {
            announcement = $"{announcement}, {session.DisplayName}";
        }
        return announcement;
    }

    private void MediaList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.None
            && SelectedItem is { } item
            && key == Key.Left)
        {
            if ((MediaList.SelectedItem as MediaItemRow)?.FolderPath is { } folderPath)
                Announce($"{item.Title}: {folderPath}");
            else
                AnnounceQuickMediaInformation(item);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)) return;

        // Shift with the number row produces punctuation on the active keyboard
        // layout. Let PreviewTextInput deliver the actual character instead of
        // incorrectly treating it as the unshifted digit.
        if (Keyboard.Modifiers == ModifierKeys.Shift && key is >= Key.D0 and <= Key.D9) return;

        var text = TypeAheadTextFromKey(key);
        if (text is null) return;

        RunTypeAhead(text);
        e.Handled = true;
    }

    private static string? TypeAheadTextFromKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
            return ((char)('A' + ((int)key - (int)Key.A))).ToString();
        if (key is >= Key.D0 and <= Key.D9)
            return ((char)('0' + ((int)key - (int)Key.D0))).ToString();
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return ((char)('0' + ((int)key - (int)Key.NumPad0))).ToString();
        return null;
    }

    private void MediaList_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)
            || string.IsNullOrEmpty(e.Text)
            || e.Text.Any(char.IsControl)
            || MediaList.Items.Count == 0)
        {
            return;
        }

        RunTypeAhead(e.Text);
        e.Handled = true;
    }

    private void RunTypeAhead(string input)
    {
        if (MediaList.Items.Count == 0) return;

        var now = DateTime.UtcNow;
        if (now - _lastTypeAheadInputUtc > TypeAheadTimeout) ResetTypeAhead();
        _lastTypeAheadInputUtc = now;

        var continuedText = _typeAheadText + input;
        var continuedStart = _typeAheadText.Length == 0
            ? MediaList.SelectedIndex + 1
            : Math.Max(MediaList.SelectedIndex, 0);
        var matchIndex = FindTypeAheadMatch(continuedText, continuedStart);

        // If the accumulated phrase no longer matches, immediately begin a new
        // phrase with the latest letter. This makes repeated searches reliable
        // without forcing the user to wait for an invisible timeout.
        if (matchIndex < 0)
        {
            continuedText = input;
            matchIndex = FindTypeAheadMatch(continuedText, MediaList.SelectedIndex + 1);
        }

        _typeAheadText = continuedText;
        if (matchIndex < 0) return;

        MediaList.SelectedIndex = matchIndex;
        MediaList.ScrollIntoView(MediaList.SelectedItem);
        FocusMediaList();
    }

    private int FindTypeAheadMatch(string query, int startIndex)
    {
        if (MediaList.Items.Count == 0) return -1;
        var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
        var firstIndex = ((startIndex % MediaList.Items.Count) + MediaList.Items.Count) % MediaList.Items.Count;
        for (var offset = 0; offset < MediaList.Items.Count; offset++)
        {
            var index = (firstIndex + offset) % MediaList.Items.Count;
            if (MediaList.Items[index] is not MediaItemRow row) continue;
            if (compareInfo.IsPrefix(
                    row.NavigationText.TrimStart(),
                    query,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace))
            {
                return index;
            }
        }
        return -1;
    }

    private void ResetTypeAhead()
    {
        _typeAheadText = string.Empty;
        _lastTypeAheadInputUtc = default;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        CaptureCurrentSessionNavigationState();
        CaptureLocalMediaState();
        _playerUiTimer.Stop();
        _playerUiTimer.Tick -= PlayerUiTimer_Tick;
        _localSourceSyncTimer.Stop();
        _localSourceSyncTimer.Tick -= LocalSourceSyncTimer_Tick;
        foreach (var watcher in _localSourceWatchers.Values) watcher.Dispose();
        _localSourceWatchers.Clear();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        _prefixService?.Dispose();
        _localOutput.Dispose();
        _store.Save(_state);
    }

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_restoringSessionNavigation) return;
        GetSessionNavigationState(_sessions.Current.Id).Filters[_currentView] = FilterBox.Text;
        ApplyFilter(SelectedItem?.Id);
        StatusText.Text = string.IsNullOrWhiteSpace(FilterBox.Text)
            ? "Gotowy"
            : $"Wyniki filtrowania: {MediaList.Items.Count}";
    }
    private void MediaList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ActivateSelected();
    private void PlayerPlayPause_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.PlayPause);
    private void PlayerPrevious_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.Previous);
    private void PlayerNext_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.Next);
    private void PlayerBackward_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SeekBackward10);
    private void PlayerForward_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SeekForward10);
    private void PlayerVolumeDown_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.VolumeDown5);
    private void PlayerVolumeUp_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.VolumeUp5);
    private void PlaybackRateDown_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.PlaybackRateDown);
    private void PlaybackRateUp_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.PlaybackRateUp);
    private void PlaybackRateReset_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.PlaybackRateReset);
    private void SeekToTime_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SeekToTime);
    private void SeekToPercentage_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SeekToPercentage);
    private void PlayerBack_Click(object sender, RoutedEventArgs e) => ReturnFromPlayerToList();
    private void AddBookmark_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.AddBookmark);
    private void AddNamedBookmark_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.AddNamedBookmark);
    private void BookmarksView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewBookmarks);
    private void PreviousBookmark_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.PreviousBookmark);
    private void NextBookmark_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.NextBookmark);
    private void ToggleSelectedPlayback_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedBookmark is not null) ActivateSelected();
        else ExecuteCommand(CommandIds.ActivateSelected);
    }
    private void PlayNext_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.TogglePlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.AddQueue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleFavorite);
    private void Library_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleLibrary);
    private void Playlists_Click(object sender, RoutedEventArgs e) => ShowPlaylistManager();
    private void Information_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ItemProperties);
    private void CopyName_Click(object sender, RoutedEventArgs e) => CopyActionItemName();
    private void CopyLocation_Click(object sender, RoutedEventArgs e) => CopyActionItemLocation();
    private void CutFiles_Click(object sender, RoutedEventArgs e) =>
        Announce(CutLocalFilesForExternalMove(ActionItems));
    private void PasteFiles_Click(object sender, RoutedEventArgs e) =>
        PasteClipboardFilesIntoCurrentView();
    private void OpenDefaultApplication_Click(object sender, RoutedEventArgs e) => OpenLocalInDefaultApplication();
    private void OfficialApp_Click(object sender, RoutedEventArgs e) => OpenOfficialApplication();
    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelected();
    private void Recycle_Click(object sender, RoutedEventArgs e) => MoveSelectedLocalFilesToRecycleBin();
    private void PlayerRemoveLocalItem_Click(object sender, RoutedEventArgs e) => RemoveCurrentLocalItemFromPlayer();
    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastMembershipChange();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenLocalFiles_Click(object sender, RoutedEventArgs e) => OpenLocalFiles();
    private void OpenLocalFolder_Click(object sender, RoutedEventArgs e) => OpenLocalFolder();
    private void Sessions_Click(object sender, RoutedEventArgs e) => ShowSessionList();
    private void MediaContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var items = ActionItems;
        var actionItem = ActionItem;
        var folderNavigationRow = !_playerViewActive
            && string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
            && (MediaList.SelectedItem as MediaItemRow)?.FolderPath is not null;
        var playbackLabel = actionItem is not null
            && string.Equals(actionItem.Id, _sessions.Current.CurrentItem.Id, StringComparison.Ordinal)
            && _sessions.Current.IsPlaying
                ? "Wstrzymaj"
                : "Odtwórz";
        SetContextMenuItemPresentation(PlaybackMenuItem, playbackLabel, "Ctrl+Enter");
        var playNextLabel = items.Count > 0 && items.All(item => item.IsPlayNext)
            ? "Usuń z odtwarzanych jako następne"
            : "Odtwórz jako następne";
        SetContextMenuItemPresentation(PlayNextMenuItem, playNextLabel, "Ctrl+Shift+Enter");
        var queueLabel = items.Count > 0 && items.All(item => item.IsInQueue || item.IsPlayNext)
            ? "Usuń z kolejki"
            : "Dodaj do kolejki";
        SetContextMenuItemPresentation(QueueMenuItem, queueLabel, "Shift+Enter");
        var favoriteLabel = items.Count > 0 && items.All(item => item.IsFavorite)
            ? "Usuń z ulubionych"
            : "Dodaj do ulubionych";
        SetContextMenuItemPresentation(FavoriteMenuItem, favoriteLabel, "Ctrl+Shift+U");
        var libraryLabel = items.Count > 0 && items.All(item => item.IsInLibrary)
            ? "Usuń z biblioteki"
            : "Dodaj do biblioteki";
        SetContextMenuItemPresentation(LibraryMenuItem, libraryLabel, "Ctrl+Shift+L");
        var copyLocationLabel = actionItem is not null && TryGetLocalPath(actionItem.Source, out _)
            ? "Kopiuj pełną ścieżkę"
            : "Kopiuj łącze do elementu";
        SetContextMenuItemPresentation(CopyLocationMenuItem, copyLocationLabel, "Ctrl+Shift+C");
        var localItems = SelectedBookmark is null
            && items.Count > 0
            && items.All(item => TryGetLocalPath(item.Source, out var path) && File.Exists(path));
        CutFilesMenuItem.Visibility = localItems ? Visibility.Visible : Visibility.Collapsed;
        PasteFilesMenuItem.Visibility = CanPasteFilesIntoCurrentView()
            ? Visibility.Visible
            : Visibility.Collapsed;
        var localItem = localItems && actionItem is not null;
        OpenDefaultApplicationMenuItem.Visibility = localItem ? Visibility.Visible : Visibility.Collapsed;
        OfficialApplicationMenuItem.Visibility = localItem ? Visibility.Collapsed : Visibility.Visible;
        RecycleMenuItem.Visibility = localItem ? Visibility.Visible : Visibility.Collapsed;
        var removeLabel = folderNavigationRow
            ? "Folder nawigacyjny — użyj Enter"
            : SelectedBookmark is not null
            ? "Usuń zakładkę"
            : localItem && string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal)
                ? "Usuń z AMC, pozostaw plik na dysku"
                : localItem && string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
                    ? "Usuń z biblioteki, pozostaw plik w folderze"
                : localItem && string.Equals(_currentView, AllLocalFilesViewName, StringComparison.Ordinal)
                    ? "Wyklucz z biblioteki, pozostaw plik na dysku"
                : "Usuń z bieżącego widoku";
        SetContextMenuItemPresentation(RemoveMenuItem, removeLabel, "Delete");
        RemoveMenuItem.IsEnabled = !folderNavigationRow
            && (_currentView is not (FolderViewName or AllLocalFilesViewName)
                || items.Any(item => item.IsInLibrary));
    }
    private void MediaContextMenu_Closed(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(FocusMediaList, DispatcherPriority.Loaded);
    private void PlayerContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var item = _sessions.Current.CurrentItem;
        SetContextMenuItemPresentation(
            PlayerPlayPauseMenuItem,
            _sessions.Current.IsPlaying ? "Wstrzymaj" : "Odtwórz",
            "Spacja");
        var playNextLabel = item.IsPlayNext
            ? "Usuń z odtwarzanych jako następne"
            : "Odtwórz jako następne";
        SetContextMenuItemPresentation(PlayerPlayNextMenuItem, playNextLabel, "Ctrl+Shift+Enter");
        var queueLabel = item.IsInQueue || item.IsPlayNext
            ? "Usuń z kolejki"
            : "Dodaj do kolejki";
        SetContextMenuItemPresentation(PlayerQueueMenuItem, queueLabel, "Shift+Enter");
        var favoriteLabel = item.IsFavorite
            ? "Usuń z ulubionych"
            : "Dodaj do ulubionych";
        SetContextMenuItemPresentation(PlayerFavoriteMenuItem, favoriteLabel, "Ctrl+Shift+U");
        var libraryLabel = item.IsInLibrary
            ? "Usuń z biblioteki"
            : "Dodaj do biblioteki";
        SetContextMenuItemPresentation(PlayerLibraryMenuItem, libraryLabel, "Ctrl+Shift+L");
        var copyLocationLabel = TryGetLocalPath(item.Source, out _)
            ? "Kopiuj pełną ścieżkę"
            : "Kopiuj łącze do elementu";
        SetContextMenuItemPresentation(PlayerCopyLocationMenuItem, copyLocationLabel, "Ctrl+Shift+C");
        var localItem = TryGetLocalPath(item.Source, out _);
        PlayerOpenDefaultApplicationMenuItem.Visibility = localItem ? Visibility.Visible : Visibility.Collapsed;
        PlayerOfficialApplicationMenuItem.Visibility = localItem ? Visibility.Collapsed : Visibility.Visible;
        PlayerRemoveLocalItemMenuItem.Visibility = localItem
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private static void SetContextMenuItemPresentation(
        MenuItem menuItem,
        string label,
        string shortcut)
    {
        menuItem.Header = label;
        AutomationProperties.SetName(menuItem, $"{label}, {shortcut}");
    }

    private void CopyActionItemName()
    {
        if (!_playerViewActive && string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            var selectedRows = MediaList.Items
                .OfType<MediaItemRow>()
                .Where(row => MediaList.SelectedItems.Contains(row))
                .ToArray();
            if (selectedRows.Length == 0) return;
            _pendingExternalMoves.Clear();
            Clipboard.SetText(string.Join(Environment.NewLine, selectedRows.Select(row => row.Label)));
            Announce(selectedRows.Length == 1
                ? "Skopiowano zakładkę"
                : $"Skopiowano zakładki: {selectedRows.Length}");
            return;
        }

        var items = ActionItems;
        if (items.Count == 0) return;
        _pendingExternalMoves.Clear();
        Clipboard.SetText(string.Join(Environment.NewLine, items.Select(item => item.Title)));
        Announce(items.Count == 1
            ? "Skopiowano nazwę"
            : $"Skopiowano nazwy: {FormatItemCount(items.Count)}");
    }

    private void CopyActionItemLocation()
    {
        var message = CopyItemLocations(ActionItems, _sessions.Current.Id);
        if (message.Length > 0) Announce(message);
    }

    private string CopyItemLocations(IReadOnlyList<MediaItem> items, string sessionId)
    {
        if (items.Count == 0) return string.Empty;
        _pendingExternalMoves.Clear();
        var localPaths = items
            .Select(item => TryGetLocalPath(item.Source, out var path) ? path : null)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (localPaths.Length == items.Count)
        {
            var fileDropList = new StringCollection();
            fileDropList.AddRange(localPaths);
            var data = new System.Windows.DataObject();
            data.SetData(DataFormats.UnicodeText, string.Join(Environment.NewLine, localPaths));
            data.SetFileDropList(fileDropList);
            Clipboard.SetDataObject(data, true);
            return localPaths.Length == 1
                ? "Skopiowano plik i pełną ścieżkę"
                : $"Skopiowano pliki i pełne ścieżki: {FormatFileCount(localPaths.Length)}";
        }

        var item = items[0];
        var publicUri = string.IsNullOrWhiteSpace(item.PublicUri)
            ? $"demo://{sessionId}/{item.Id}"
            : item.PublicUri;
        Clipboard.SetText(publicUri);
        return "Skopiowano łącze do elementu";
    }

    private string CopySearchResultLocations(IReadOnlyList<SearchWindow.SearchResult> results)
    {
        _pendingExternalMoves.Clear();
        var clipboardEntries = results
            .Select(result =>
            {
                var localPath = TryGetLocalPath(result.Item.Source, out var path)
                    && File.Exists(path)
                        ? path
                        : null;
                var text = localPath
                    ?? (string.IsNullOrWhiteSpace(result.Item.PublicUri)
                        ? $"demo://{result.SessionId}/{result.Item.Id}"
                        : result.Item.PublicUri);
                return (LocalPath: localPath, Text: text);
            })
            .ToArray();
        var localPaths = clipboardEntries
            .Select(entry => entry.LocalPath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serviceCount = clipboardEntries.Count(entry => entry.LocalPath is null);
        var text = string.Join(Environment.NewLine, clipboardEntries.Select(entry => entry.Text));

        if (localPaths.Length > 0)
        {
            var fileDropList = new StringCollection();
            fileDropList.AddRange(localPaths);
            var data = new System.Windows.DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            data.SetFileDropList(fileDropList);
            Clipboard.SetDataObject(data, true);
        }
        else
        {
            Clipboard.SetText(text);
        }

        if (localPaths.Length > 0 && serviceCount > 0)
        {
            return $"Skopiowano pliki: {localPaths.Length}; łącza: {serviceCount}";
        }
        if (localPaths.Length > 0)
        {
            return localPaths.Length == 1
                ? "Skopiowano plik i pełną ścieżkę"
                : $"Skopiowano pliki i pełne ścieżki: {FormatFileCount(localPaths.Length)}";
        }
        return results.Count == 1
            ? "Skopiowano łącze do elementu"
            : $"Skopiowano łącza: {results.Count}";
    }

    private bool CanPasteFilesIntoCurrentView() =>
        !_playerViewActive
        && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
        && _currentView is DefaultBrowserView or AllLocalFilesViewName or "Kolejka" or "Ulubione";

    private void PasteClipboardFilesIntoCurrentView()
    {
        if (!CanPasteFilesIntoCurrentView())
        {
            Announce("Pliki można wkleić w lokalnych widokach Wszystkie pliki, Kolejka lub Ulubione");
            return;
        }
        StringCollection clipboardFiles;
        try
        {
            if (!Clipboard.ContainsFileDropList())
            {
                Announce("Schowek nie zawiera plików");
                return;
            }
            clipboardFiles = Clipboard.GetFileDropList();
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            Announce($"Nie można odczytać plików ze schowka: {exception.Message}");
            return;
        }

        var existingPaths = clipboardFiles
            .Cast<string>()
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var audioPaths = existingPaths
            .Where(LocalAudioFileDiscovery.IsAudioFile)
            .ToArray();
        var skippedCount = clipboardFiles.Count - audioPaths.Length;
        if (audioPaths.Length == 0)
        {
            Announce("Schowek nie zawiera obsługiwanych plików audio");
            return;
        }

        var knownByPath = _localItems
            .Where(item => TryGetLocalPath(item.Source, out _))
            .GroupBy(
                item => TryGetLocalPath(item.Source, out var path) ? path : item.Source!,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        var targetItems = new List<MediaItem>();
        var addedItems = new List<MediaItem>();
        foreach (var path in audioPaths)
        {
            if (!knownByPath.TryGetValue(path, out var item))
            {
                item = new MediaItem
                {
                    Id = $"local-{Guid.NewGuid():N}",
                    Title = Path.GetFileNameWithoutExtension(path),
                    Kind = MediaItemKind.Track,
                    Source = path,
                    IsInLibrary = true,
                    IsAvailable = true
                };
                knownByPath[path] = item;
                _localItems.Add(item);
                addedItems.Add(item);
            }

            item.IsInLibrary = true;
            item.IsAvailable = true;
            if (string.Equals(_currentView, "Kolejka", StringComparison.Ordinal)) item.IsInQueue = true;
            if (string.Equals(_currentView, "Ulubione", StringComparison.Ordinal)) item.IsFavorite = true;
            targetItems.Add(item);
        }

        var localSession = _sessions.FindSession("local");
        if (localSession is null)
        {
            Announce("Sesja Pliki lokalne nie jest dostępna");
            return;
        }
        RemoveLocalExclusions(audioPaths);
        RefreshLocalSessionItems();
        _pendingExternalMoves.Clear();
        var copiedFiles = new StringCollection();
        copiedFiles.AddRange(audioPaths);
        var copiedData = new System.Windows.DataObject();
        copiedData.SetData(DataFormats.UnicodeText, string.Join(Environment.NewLine, audioPaths));
        copiedData.SetFileDropList(copiedFiles);
        Clipboard.SetDataObject(copiedData, true);

        RefreshCurrentView(preferredItemId: targetItems[0].Id);
        SelectMediaItems(targetItems.Select(item => item.Id));
        TrySaveLocalMediaState(true);
        RestoreMediaListFocusAfterRefresh();

        var destination = _currentView switch
        {
            "Kolejka" => "do kolejki",
            "Ulubione" => "do ulubionych",
            AllLocalFilesViewName => "do biblioteki",
            _ => "do multimediów lokalnych"
        };
        var newPart = addedItems.Count > 0
            ? $" Nowe w AMC: {FormatFileCount(addedItems.Count)}."
            : " Wszystkie pliki były już w AMC.";
        var skippedPart = skippedCount > 0 ? $" Pominięto: {skippedCount}." : string.Empty;
        Announce($"Dodano ze schowka {destination}: {FormatFileCount(targetItems.Count)}.{newPart}{skippedPart} Pliki pozostały w swoich folderach");
    }

    private string CutLocalFilesForExternalMove(IReadOnlyList<MediaItem> items)
    {
        if (items.Count == 0) return "Brak pliku do wycięcia";
        if (!_playerViewActive
            && string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            return "Wycinanie plików nie działa na liście zakładek";
        }

        var localFiles = items
            .Select(item => (Item: item, Path: TryGetLocalPath(item.Source, out var path) ? path : null))
            .ToArray();
        if (localFiles.Any(entry => entry.Path is null || !File.Exists(entry.Path)))
        {
            return "Wycinanie jest dostępne tylko dla istniejących plików lokalnych";
        }

        var paths = localFiles
            .Select(entry => entry.Path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fileDropList = new StringCollection();
        fileDropList.AddRange(paths);
        var data = new System.Windows.DataObject();
        data.SetData(DataFormats.UnicodeText, string.Join(Environment.NewLine, paths));
        data.SetFileDropList(fileDropList);
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(2)));
        Clipboard.SetDataObject(data, true);
        _pendingExternalMoves.Clear();
        foreach (var entry in localFiles)
        {
            _pendingExternalMoves[entry.Item.Id] = entry.Path!;
        }
        return paths.Length == 1
            ? "Plik gotowy do przeniesienia. Wklej go w folderze docelowym"
            : $"Pliki gotowe do przeniesienia: {FormatFileCount(paths.Length)}. Wklej je w folderze docelowym";
    }

    private void ReconcileCompletedExternalMoves()
    {
        if (_pendingExternalMoves.Count == 0 || _sessions is null) return;
        var movedIds = _pendingExternalMoves
            .Where(entry => !File.Exists(entry.Value))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (movedIds.Count == 0) return;

        var movedItems = _localItems
            .Where(item => movedIds.Contains(item.Id))
            .ToArray();
        foreach (var id in movedIds) _pendingExternalMoves.Remove(id);
        if (movedItems.Length == 0) return;

        var localSession = _sessions.FindSession("local");
        if (localSession is not null
            && movedIds.Contains(localSession.CurrentItem.Id))
        {
            if (localSession.IsPlaying) localSession.StopPlayback();
            localSession.RememberCurrentPosition();
        }

        foreach (var item in movedItems)
        {
            item.IsAvailable = false;
        }
        var selectedId = SelectedItem?.Id;
        var hadListFocus = MediaList.IsKeyboardFocusWithin;
        if (hadListFocus) AnchorMediaListFocus();
        RefreshLocalSessionItems();
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            if (_playerViewActive)
            {
                if (_sessions.Current.HasItems) UpdatePlayerView(true);
                else ReturnFromPlayerToList();
            }
            else
            {
                RefreshCurrentView(preferredItemId: selectedId);
                if (hadListFocus) RestoreMediaListFocusAfterRefresh();
            }
            UpdatePlaybackStatusBar();
            UpdateWindowTitle();
        }
        TrySaveLocalMediaState(true);
        var message = movedItems.Length == 1
            ? $"Plik przeniesiono poza AMC. Zachowano historię i dane: {movedItems[0].Title}"
            : $"Pliki przeniesiono poza AMC. Zachowano historię i dane: {FormatFileCount(movedItems.Length)}";
        Dispatcher.BeginInvoke(() => AnnounceEssential(message), DispatcherPriority.ContextIdle);
        ScheduleLocalSourceSync();
    }
    private void PreviousSession_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SessionPrevious);
    private void NextSession_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SessionNext);
    private void NowPlayingView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewNowPlaying);
    private void FavoritesView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewFavorites);
    private void PlaylistsView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewPlaylists);
    private void LibraryView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewLibrary);
    private void FoldersView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewFolders);
    private void AllLocalFilesView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewAllLocalFiles);
    private void RefreshLocalLibrary_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.RefreshLocalLibrary);
    private void QueueView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewQueue);
    private void HistoryView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewHistory);
    private void BookmarksViewMenu_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewBookmarks);
    private void AlbumsView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewAlbums);
    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        ShowFilter();
    }
    private void SearchCurrent_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SearchCurrent);
    private void SearchAll_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.SearchAll);
    private void CommandPalette_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.CommandPalette);
    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private async void Updates_Click(object sender, RoutedEventArgs e)
    {
        var result = await new UnconfiguredUpdateService().CheckAsync();
        Announce(result.Error ?? "Brak aktualizacji");
    }

    private sealed class MediaItemRow(
        MediaItem item,
        string label,
        string navigationText,
        MediaItem? actionItem = null,
        BookmarkEntry? bookmark = null,
        string? folderPath = null) : INotifyPropertyChanged
    {
        public MediaItem Item { get; } = item;
        public MediaItem ActionItem { get; } = actionItem ?? item;
        public BookmarkEntry? Bookmark { get; } = bookmark;
        public string? FolderPath { get; } = folderPath;
        public string Label { get; private set; } = label;
        public string NavigationText { get; } = navigationText;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void UpdateLabel(string label)
        {
            if (string.Equals(Label, label, StringComparison.Ordinal)) return;
            Label = label;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }

        public override string ToString() => Label;
    }

    private sealed class SessionViewHistory
    {
        public Stack<string> Back { get; } = [];
        public Stack<string> Forward { get; } = [];
    }

    private sealed class PlaybackHistoryCursor(IReadOnlyList<string> itemIds, int index)
    {
        public IReadOnlyList<string> ItemIds { get; } = itemIds;
        public int Index { get; set; } = index;
    }

    private sealed record BookmarkNavigationCursor(
        string SessionId,
        string ItemId,
        string BookmarkId,
        DateTime LastNavigationUtc);

    private sealed record BookmarkReturnContext(
        string SessionId,
        string ViewName,
        string? SelectedItemId);

    private sealed record LocalSourceScanResult(
        LocalFolderSourceSettings Source,
        IReadOnlyList<string> Files,
        string? Error);

    private sealed record LocalCatalogUndo(
        long Sequence,
        IReadOnlyList<RemovedMediaItem> CatalogItems,
        IReadOnlyList<RemovedMediaItem> SessionItems,
        string Announcement,
        RemovedSessionRegistration? DetachedSession = null);
}
