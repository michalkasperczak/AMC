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
    private const string CustomLocalOrderViewName = "Kolejność własna";
    private const string LocalAlbumContentsViewName = "Album";
    private const string PlaylistContentsViewPrefix = "Playlista:";
    private string? _currentLocalAlbumPath;
    private string? _currentLocalAlbumTitle;
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
    private readonly Stack<PlaylistStateUndo> _playlistHistory = [];
    private readonly Stack<QueueOrderUndo> _queueOrderHistory = [];
    private long _undoSequence;
    private bool _initialFocusApplied;
    private bool _deferAnnouncements;
    private string? _deferredAnnouncement;
    private bool _captureAnnouncements;
    private string? _capturedAnnouncement;
    private bool _preservePreparedPlaybackContext;
    private IReadOnlyList<MediaItem>? _actionItemsOverride;
    private HwndSource? _windowSource;
    private string _typeAheadText = string.Empty;
    private DateTime _lastTypeAheadInputUtc;
    private string? _focusContextItemId;
    private string? _focusContextPrefix;
    private ListBoxItem? _focusContextContainer;
    private readonly WindowsMediaOutput _localOutput = new();
    private readonly RadioBrowserClient _radioCatalog = new();
    private RadioMediaOutput _radioOutput = null!;
    private string? _cloudPreparingItemId;
    private readonly List<MediaItem> _localItems = [];
    private readonly List<MediaItem> _radioItems = [];
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
    private bool _keyboardHelpActive;
    private bool _restoringSessionNavigation;
    private string? _playerFocusContextPrefix;
    private DateTime _lastLocalStateSaveUtc;
    private long _lastSavedLocalPositionTicks = -1;
    private long _quickInformationRequestVersion;
    private readonly System.Windows.Forms.StatusStrip _playbackStatusBar;
    private readonly System.Windows.Forms.ToolStripStatusLabel _playbackStatusLabel;

    private const int WmKeyDown = 0x0100;
    private const int VirtualKeyC = 0x43;
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
        DiagnosticLog.Info("startup", "Tworzenie głównego okna.");
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
        _radioOutput = new RadioMediaOutput(_state.Radio.TimeshiftMinutes);
        _radioOutput.PlaybackFailed += RadioOutput_PlaybackFailed;
        _radioOutput.PlaybackPreparing += RadioOutput_PlaybackPreparing;
        _radioOutput.PlaybackStarted += RadioOutput_PlaybackStarted;
        _radioOutput.RecordingFailed += RadioOutput_RecordingFailed;
        NormalizeTransientBookmarkViewsAtStartup();
        NormalizePlaylistViewsAtStartup();
        NormalizeLocalLibraryNavigationAtStartup();
        NormalizeRadioNavigationAtStartup();
        ClearPersistedListFiltersAtStartup();
        _playbackHistory = new PlaybackHistory(_state.PlaybackHistory);
        _bookmarkIndex = new BookmarkIndex(_state.Bookmarks);
        LoadPersistedLocalMedia();
        LoadPersistedRadio();
        DiagnosticLog.Info("startup", $"Odtworzono w pamięci {state.LocalMedia.Items.Count} rekordów Biblioteki.");
        _localOutput.DurationAvailable += LocalOutput_DurationAvailable;
        _localOutput.PlaybackFailed += LocalOutput_PlaybackFailed;
        _localOutput.PlaybackEnded += LocalOutput_PlaybackEnded;
        _localOutput.PlaybackPreparing += LocalOutput_PlaybackPreparing;
        _localOutput.PlaybackStarted += LocalOutput_PlaybackStarted;
        ApplyDetailedHints();
        RebuildCore();
        DiagnosticLog.Info("startup", "Odtworzono sesje i kontekst odtwarzania.");
        RestoreCurrentSessionNavigationState();
        UpdatePlaybackStatusBar();
        _playerUiTimer.Start();
        DiagnosticLog.Info("startup", "Główne okno jest gotowe do pokazania.");
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
        if (string.Equals(navigation.CurrentView, LocalAlbumContentsViewName, StringComparison.Ordinal))
        {
            navigation.CurrentView = "Albumy";
        }
    }

    private void NormalizeRadioNavigationAtStartup()
    {
        if (!_state.SessionNavigation.Sessions.TryGetValue("radio", out var navigation))
        {
            _state.SessionNavigation.Sessions["radio"] = new SessionNavigationState
            {
                CurrentView = "Biblioteka"
            };
            return;
        }
        if (navigation.CurrentView is DefaultBrowserView or "Radio i rekomendacje")
        {
            navigation.CurrentView = "Biblioteka";
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

    private void NormalizePlaylistViewsAtStartup()
    {
        var playlists = new PlaylistIndex(_state.Playlists);
        foreach (var (sessionId, navigation) in _state.SessionNavigation.Sessions)
        {
            if (!TryGetPlaylistIdFromView(navigation.CurrentView, out var playlistId)) continue;
            var playlist = playlists.Find(playlistId);
            if (playlist is null
                || !string.Equals(playlist.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            {
                navigation.CurrentView = "Playlisty";
            }
        }
    }

    public MediaItem? SelectedItem => (MediaList.SelectedItem as MediaItemRow)?.Item;
    private BookmarkEntry? SelectedBookmark => (MediaList.SelectedItem as MediaItemRow)?.Bookmark;
    public MediaItem? ActionItem
    {
        get
        {
            if (_playerViewActive) return _sessions.Current.HasCurrentItem ? _sessions.Current.CurrentItem : null;
            var row = MediaList.SelectedItem as MediaItemRow;
            if (row?.PlaylistId is not null) return null;
            return row?.ActionItem ?? (_sessions.Current.HasCurrentItem ? _sessions.Current.CurrentItem : null);
        }
    }
    private DemoMediaSession ActionSession => !_playerViewActive && SelectedBookmark is { } bookmark
        ? _sessions.FindSession(bookmark.SessionId) ?? _sessions.Current
        : _sessions.Current;
    public IReadOnlyList<MediaItem> ActionItems
    {
        get
        {
            if (_actionItemsOverride is not null) return _actionItemsOverride;
            if (_playerViewActive)
                return _sessions.Current.HasCurrentItem ? [_sessions.Current.CurrentItem] : [];
            var selected = MediaList.SelectedItems
                .OfType<MediaItemRow>()
                .Where(row => row.PlaylistId is null)
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

        if ((viewName is FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName)
            && !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            var local = _sessions.FindSession("local");
            if (local is null)
            {
                Announce("Foldery są dostępne po otwarciu lokalnego folderu z plikami audio");
                return;
            }
            CaptureCurrentSessionNavigationState();
            ClearFilterForNavigation(
                GetSessionNavigationState(_sessions.Current.Id),
                _currentView);
            HidePlayerForBrowserNavigation();
            _sessions.SelectSession(local.Id);
            ClearFilterForNavigation(
                GetSessionNavigationState(local.Id),
                _state.LocalMedia.LibraryView);
            RestoreCurrentSessionNavigationState();
        }

        if (string.Equals(viewName, FolderViewName, StringComparison.Ordinal)
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
            && _currentView is AllLocalFilesViewName or CustomLocalOrderViewName
            && SelectedItem is { Source.Length: > 0 } selectedFlatItem
            && TryGetLocalPath(selectedFlatItem.Source, out var selectedPath))
        {
            var source = _state.LocalMedia.FolderSources
                .Where(candidate => IsSameOrDescendantPath(selectedPath, candidate.Path))
                .OrderByDescending(candidate => candidate.Path.Length)
                .FirstOrDefault();
            _state.LocalMedia.CurrentFolderPath = source is null
                ? null
                : Path.GetDirectoryName(selectedPath);
            var navigation = GetSessionNavigationState("local");
            navigation.SelectedItemIds[FolderViewName] = selectedFlatItem.Id;
            navigation.Filters[FolderViewName] = string.Empty;
        }

        if (viewName is FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName)
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
        PrepareViewFocusContext(string.Equals(viewName, "Kolejka", StringComparison.Ordinal)
            ? QueueFocusContext()
            : viewName);
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
            _state.Settings.Messages.DetailedHints,
            allServices || string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
                ? PrepareRadioSearchAsync
                : null)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.SelectedResult is { } result)
        {
            var selectedSession = SelectSessionBrowserItem(result.SessionId, result.Item.Id);
            if (allServices) PrepareSearchReturnContext(result.Item.Id);
            if (dialog.SelectedAction == SearchResultAction.Playlist && selectedSession is not null)
            {
                var items = dialog.SelectedResults
                    .Where(selected => string.Equals(
                        selected.SessionId,
                        selectedSession.Id,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(selected => selected.Item)
                    .ToArray();
                ShowPlaylistManager(items, selectedSession);
                return;
            }
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
        ClearFilterForNavigation(
            GetSessionNavigationState(_sessions.Current.Id),
            _currentView);
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
        ClearFilterForNavigation(navigation, targetView);
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
        if (!_sessions.Current.HasCurrentItem)
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
        ApplyPlaybackPolicyWhenLeavingPlayer(_sessions.Current);
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
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(false);
        }
        RestoreMediaListFocusAfterRefresh();
    }

    private void HidePlayerForBrowserNavigation()
    {
        if (!_playerViewActive) return;
        ApplyPlaybackPolicyWhenLeavingPlayer(_sessions.Current);
        _playerViewActive = false;
        PlayerPanel.Visibility = Visibility.Collapsed;
        BrowserHeaderPanel.Visibility = Visibility.Visible;
        BrowserActionPanel.Visibility = Visibility.Visible;
        MediaList.Visibility = Visibility.Visible;
        GetSessionNavigationState(_sessions.Current.Id).PlayerActive = false;
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(false);
        }
    }

    private void ApplyPlaybackPolicyWhenLeavingPlayer(DemoMediaSession session)
    {
        if (!_state.Settings.PausePlaybackWhenLeavingPlayer || !session.HasCurrentItem) return;

        if (session.IsPlaying) session.TogglePlayback();
        if (string.Equals(session.Id, "local", StringComparison.Ordinal)
            && !ShouldRememberLocalPosition(session.CurrentItem))
        {
            session.SetPosition(TimeSpan.Zero);
        }
        UpdatePlaybackStatusBar();
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
        var isRadio = string.Equals(session.Id, "radio", StringComparison.Ordinal);
        var preparing = string.Equals(session.Id, "local", StringComparison.Ordinal)
            ? _localOutput.IsPreparing
            : isRadio && _radioOutput.IsPreparing;
        var state = preparing
            ? (_cloudPreparingItemId is null ? "Otwieranie" : "Pobieranie z chmury")
            : session.IsPlaying ? "Odtwarzanie" : "Pauza";

        PlayerTitleText.Text = item.Title;
        PlayerArtistText.Text = string.IsNullOrWhiteSpace(item.Artist)
            ? item.KindLabel
            : item.Artist;
        PlayerSessionText.Text = session.DisplayName;
        PlayerStateText.Text = state;
        PlayerSpeedText.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        PlayerSpeedText.Text = $"Prędkość: {FormatPlaybackRateMultiplier(session.PlaybackRate)}";
        PlayerPreviousButton.Content = isRadio ? "_Poprzednia stacja" : "_Poprzedni utwór";
        PlayerNextButton.Content = isRadio ? "_Następna stacja" : "_Następny utwór";
        foreach (var control in new FrameworkElement[]
                 {
                     PlayerRateDownButton,
                     PlayerRateUpButton,
                     PlayerRateResetButton,
                     PlayerSeekTimeButton,
                     PlayerSeekPercentButton,
                     PlayerAddBookmarkButton,
                     PlayerAddNamedBookmarkButton,
                     PlayerBookmarksButton
                 })
        {
            control.Visibility = isRadio ? Visibility.Collapsed : Visibility.Visible;
        }
        PlayerTimeText.Text = isRadio
            ? _radioOutput.BehindLive < TimeSpan.FromSeconds(1)
                ? $"Na żywo, bufor {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}"
                : $"{CommandRouter.FormatTime(_radioOutput.BehindLive)} za transmisją, bufor {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}"
            : duration > TimeSpan.Zero
            ? $"{CommandRouter.FormatTime(position)} z {CommandRouter.FormatTime(duration)}"
            : CommandRouter.FormatTime(position);
        RadioRecordingButton.Visibility = isRadio ? Visibility.Visible : Visibility.Collapsed;
        RadioRecordingButton.Content = _radioOutput.IsRecording ? "_Zakończ nagrywanie" : "_Nagrywaj radio";
        PlayerHelpText.Text = PlayerKeyboardHelpText();
        PlayerPlayPauseButton.Content = preparing ? "_Anuluj" : session.IsPlaying ? "_Wstrzymaj" : "_Odtwórz";

        if (!updateAccessibleName) return;
        var artist = string.IsNullOrWhiteSpace(item.Artist) ? item.KindLabel : item.Artist;
        var action = preparing ? "Anuluj otwieranie" : session.IsPlaying ? "Wstrzymaj" : "Odtwórz";
        var focusContext = _playerFocusContextPrefix;
        _playerFocusContextPrefix = null;
        AutomationProperties.SetName(
            PlayerPlayPauseButton,
            focusContext is null
                ? isRadio
                    ? $"Odtwarzacz, {item.Title}, {session.DisplayName}, {state}. {action}"
                    : $"Odtwarzacz, {item.Title}, {artist}, {session.DisplayName}, {state}, prędkość {FormatPlaybackRateMultiplier(session.PlaybackRate)}. {action}"
                : isRadio
                    ? $"{focusContext}, Odtwarzacz, {item.Title}, {state}. {action}"
                    : $"{focusContext}, Odtwarzacz, {item.Title}, {artist}, {state}, prędkość {FormatPlaybackRateMultiplier(session.PlaybackRate)}. {action}");
        AutomationProperties.SetHelpText(
            PlayerPlayPauseButton,
            PlayerKeyboardHelpText());
    }

    private string PlayerKeyboardHelpText()
    {
        var exit = _state.Settings.PausePlaybackWhenLeavingPlayer
            ? "Escape wstrzymuje odtwarzanie i wraca do listy."
            : "Escape wraca do listy, a odtwarzanie trwa.";
        if (string.Equals(_sessions?.Current.Id, "radio", StringComparison.Ordinal))
        {
            return "Strzałki w lewo i w prawo poruszają się po buforze transmisji, Home przechodzi do początku bufora, End wraca na żywo, a strzałki w górę i w dół regulują głośność. Ctrl+Alt+R rozpoczyna lub kończy nagrywanie. Page Up i Page Down wybierają poprzednią lub następną stację. " + exit;
        }
        return "Strzałki sterują czasem i głośnością. Page Up i Page Down wybierają poprzedni lub następny utwór. "
            + "B dodaje szybką zakładkę, Ctrl+Shift+B dodaje nazwaną, a Shift+Page Up i Shift+Page Down przechodzą po zakładkach. "
            + "Shift+przecinek zwalnia, Shift+kropka przyspiesza, Ctrl+kropka przywraca normalną prędkość. "
            + exit;
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
        var isRadio = string.Equals(session.Id, "radio", StringComparison.Ordinal);
        var preparing = string.Equals(session.Id, "local", StringComparison.Ordinal)
            ? _localOutput.IsPreparing
            : isRadio && _radioOutput.IsPreparing;
        var state = preparing
            ? (_cloudPreparingItemId is null ? "Otwieranie" : "Pobieranie z chmury")
            : session.IsPlaying ? "Odtwarzanie" : "Pauza";
        var time = isRadio
            ? _radioOutput.BehindLive < TimeSpan.FromSeconds(1)
                ? $"na żywo, bufor {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}"
                : $"{CommandRouter.FormatTime(_radioOutput.BehindLive)} za transmisją, bufor {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}"
            : item.Duration > TimeSpan.Zero
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
        if (isRadio && _radioOutput.IsRecording) parts.Insert(0, "nagrywanie");
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

    public void ShowPlaylistManager() => ShowPlaylistManager(ActionItems, ActionSession);

    private void ShowPlaylistManager(
        IReadOnlyList<MediaItem> items,
        DemoMediaSession session)
    {
        if (items.Any(item => item.Kind is not (MediaItemKind.Track or MediaItemKind.Station)))
        {
            Announce("Do playlisty wybierz utwory albo stacje. Zawartość albumu lub folderu otwórz Enterem");
            if (_playerViewActive) FocusPlayerView();
            else RestoreMediaListFocusAfterRefresh();
            return;
        }
        var index = new PlaylistIndex(_state.Playlists);
        var previous = index.CloneSettings();
        var dialog = new PlaylistWindow(
            session.Id,
            session.DisplayName,
            index.GetForSession(session.Id),
            items) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            if (_playerViewActive) FocusPlayerView();
            else RestoreMediaListFocusAfterRefresh();
            return;
        }

        index.ReplaceSession(session.Id, dialog.ResultPlaylists);
        if (!PlaylistStatesEqual(previous, _state.Playlists))
        {
            RecordPlaylistUndo(previous, "Cofnięto zmiany playlist");
            EnsureCurrentPlaylistStillExists();
            RefreshCurrentView();
            SavePlaylistState();
        }
        var target = TryResolveSelectedFolderContents(out var folderContext)
            ? $"zawartość folderu {folderContext.FolderLabel}, {FormatFileCount(items.Count)}"
            : items.Count switch
            {
                0 => session.DisplayName,
                1 => items[0].Title,
                _ => FormatItemCount(items.Count)
            };
        Announce($"Zapisano zmiany playlist dla: {target}");
        if (_playerViewActive) FocusPlayerView();
        else RestoreMediaListFocusAfterRefresh();
    }

    private static bool PlaylistStatesEqual(PlaylistSettings left, PlaylistSettings right)
    {
        if (left.Entries.Count != right.Entries.Count) return false;
        for (var index = 0; index < left.Entries.Count; index++)
        {
            var first = left.Entries[index];
            var second = right.Entries[index];
            if (!string.Equals(first.Id, second.Id, StringComparison.Ordinal)
                || !string.Equals(first.SessionId, second.SessionId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(first.Name, second.Name, StringComparison.Ordinal)
                || first.CreatedUtcTicks != second.CreatedUtcTicks
                || !first.ItemIds.SequenceEqual(second.ItemIds, StringComparer.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private void RecordPlaylistUndo(PlaylistSettings previousState, string announcement) =>
        _playlistHistory.Push(new PlaylistStateUndo(++_undoSequence, previousState, announcement));

    private void SavePlaylistState()
    {
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(true);
            return;
        }
        try
        {
            _store.Save(_state);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            AnnounceEssential($"Nie udało się zapisać playlist: {exception.Message}");
        }
    }

    private void EnsureCurrentPlaylistStillExists()
    {
        if (!TryGetPlaylistIdFromView(_currentView, out var playlistId)) return;
        var playlist = new PlaylistIndex(_state.Playlists).Find(playlistId);
        if (playlist is not null
            && string.Equals(playlist.SessionId, _sessions.Current.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _currentView = "Playlisty";
        GetSessionNavigationState(_sessions.Current.Id).CurrentView = _currentView;
    }

    private PlaylistEntry? SelectedPlaylist()
    {
        var playlistId = (MediaList.SelectedItem as MediaItemRow)?.PlaylistId;
        return playlistId is null ? null : new PlaylistIndex(_state.Playlists).Find(playlistId);
    }

    private void CreatePlaylist()
    {
        var index = new PlaylistIndex(_state.Playlists);
        while (true)
        {
            var dialog = new PlaylistNameWindow("Nowa playlista", string.Empty) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                RestoreMediaListFocusAfterRefresh();
                return;
            }
            var previous = index.CloneSettings();
            try
            {
                var playlist = index.Create(_sessions.Current.Id, dialog.PlaylistName);
                RecordPlaylistUndo(previous, $"Cofnięto utworzenie playlisty: {playlist.Name}");
                if (!string.Equals(_currentView, "Playlisty", StringComparison.Ordinal))
                {
                    NavigateTo("Playlisty");
                }
                RefreshCurrentView(preferredItemId: $"playlist:{playlist.Id}");
                SavePlaylistState();
                PrepareSelectedItemFocusContext("Utworzono playlistę");
                RestoreMediaListFocusAfterRefresh();
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
        var playlist = SelectedPlaylist();
        if (playlist is null)
        {
            Announce("Wybierz playlistę do zmiany nazwy");
            RestoreMediaListFocusAfterRefresh();
            return;
        }
        var index = new PlaylistIndex(_state.Playlists);
        while (true)
        {
            var dialog = new PlaylistNameWindow("Zmień nazwę playlisty", playlist.Name) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                RestoreMediaListFocusAfterRefresh();
                return;
            }
            var previous = index.CloneSettings();
            try
            {
                var oldName = playlist.Name;
                index.Rename(playlist.Id, dialog.PlaylistName);
                RecordPlaylistUndo(previous, $"Przywrócono nazwę playlisty: {oldName}");
                RefreshCurrentView(preferredItemId: $"playlist:{playlist.Id}");
                SavePlaylistState();
                PrepareSelectedItemFocusContext("Zmieniono nazwę playlisty");
                RestoreMediaListFocusAfterRefresh();
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
        var playlist = SelectedPlaylist();
        if (playlist is null)
        {
            Announce("Wybierz playlistę do usunięcia");
            RestoreMediaListFocusAfterRefresh();
            return;
        }
        if (MessageBox.Show(
                this,
                $"Usunąć playlistę „{playlist.Name}”? Pliki multimedialne pozostaną bez zmian.",
                "Usuń playlistę",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        var previousIndex = MediaList.SelectedIndex;
        var index = new PlaylistIndex(_state.Playlists);
        var previous = index.CloneSettings();
        index.Remove(playlist.Id);
        RecordPlaylistUndo(previous, $"Przywrócono playlistę: {playlist.Name}");
        RefreshCurrentView(previousIndex);
        SavePlaylistState();
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(
            () => Announce($"Usunięto playlistę: {playlist.Name}. Pliki pozostały bez zmian"),
            DispatcherPriority.ContextIdle);
    }

    private void RemoveSelectedPlaylistItems(string playlistId)
    {
        var index = new PlaylistIndex(_state.Playlists);
        var playlist = index.Find(playlistId);
        if (playlist is null) return;
        var selectedIds = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .Select(row => row.ActionItem.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selectedIds.Length == 0)
        {
            Announce("Brak elementu do usunięcia z playlisty");
            return;
        }
        var previous = index.CloneSettings();
        var selectedSet = selectedIds.ToHashSet(StringComparer.Ordinal);
        var previousIndex = MediaList.SelectedIndex;
        var removed = playlist.ItemIds.RemoveAll(selectedSet.Contains);
        if (removed == 0) return;
        RecordPlaylistUndo(
            previous,
            removed == 1
                ? $"Przywrócono na playliście: {MediaList.SelectedItems.OfType<MediaItemRow>().First().ActionItem.Title}"
                : $"Przywrócono na playliście: {FormatItemCount(removed)}");
        RefreshCurrentView(previousIndex);
        SavePlaylistState();
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(
            () => Announce(removed == 1
                ? "Usunięto element z playlisty"
                : $"Usunięto z playlisty: {FormatItemCount(removed)}"),
            DispatcherPriority.ContextIdle);
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

    public void ShowItemPlaybackOptions()
    {
        var selectedRow = _playerViewActive ? null : MediaList.SelectedItem as MediaItemRow;
        var folderPath = selectedRow?.FolderPath ?? selectedRow?.AlbumFolderPath;
        if (string.Equals(ActionSession.Id, "local", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(folderPath))
        {
            ShowFolderPlaybackOptions(folderPath, selectedRow!.Item.Title);
            return;
        }

        var item = ActionItem ?? _sessions.Current.CurrentItem;
        if (!string.Equals(ActionSession.Id, "local", StringComparison.Ordinal)
            || item.Kind != MediaItemKind.Track
            || !TryGetLocalPath(item.Source, out _))
        {
            Announce("Opcje elementu zostaną udostępnione przez adapter tej usługi. Obecnie działają dla plików lokalnych");
            return;
        }

        CaptureLocalMediaState();
        var saved = FindLocalItemSettings(item);
        if (saved is null)
        {
            Announce("Nie można odnaleźć ustawień tego pliku w Bibliotece");
            return;
        }

        var dialog = new ItemPlaybackOptionsWindow(
            item.Title,
            saved.ResumePositionMode,
            saved.PlaybackRateOverride)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            RestoreItemActionFocus();
            return;
        }

        saved.ResumePositionMode = dialog.SelectedResumePositionMode;
        saved.PlaybackRateOverride = dialog.SelectedPlaybackRateOverride;
        var local = _sessions.FindSession("local");
        if (local is not null
            && string.Equals(local.CurrentItem.Id, item.Id, StringComparison.Ordinal))
        {
            if (!ShouldRememberLocalPosition(item)) local.ClearRememberedPosition(item.Id);
            local.ApplyPlaybackRateForCurrentItem();
        }
        TrySaveLocalMediaState(true);
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        Announce($"Zapisano opcje elementu: {item.Title}");
        RestoreItemActionFocus();
    }

    private void ShowFolderPlaybackOptions(string folderPath, string folderTitle)
    {
        var normalizedPath = NormalizeLocalFolderPath(folderPath);
        if (normalizedPath.Length == 0)
        {
            Announce("Nie można rozpoznać ścieżki tego folderu");
            return;
        }

        var saved = FindExactFolderPlaybackSettings(normalizedPath);
        var dialog = new ItemPlaybackOptionsWindow(
            $"Folder: {folderTitle}",
            saved?.ResumePositionMode ?? ResumePositionMode.Inherit,
            saved?.PlaybackRateOverride,
            folderTarget: true)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            RestoreItemActionFocus();
            return;
        }

        CaptureLocalMediaState();
        saved ??= new LocalFolderPlaybackSettings { Path = normalizedPath };
        saved.ResumePositionMode = dialog.SelectedResumePositionMode;
        saved.PlaybackRateOverride = dialog.SelectedPlaybackRateOverride;
        var hasOverride = saved.ResumePositionMode != ResumePositionMode.Inherit
            || saved.PlaybackRateOverride.HasValue
            || saved.OutputDeviceId is not null;
        var existingIndex = _state.LocalMedia.FolderPlaybackOptions.FindIndex(option =>
            string.Equals(NormalizeLocalFolderPath(option.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (hasOverride && existingIndex < 0)
        {
            _state.LocalMedia.FolderPlaybackOptions.Add(saved);
        }
        else if (!hasOverride && existingIndex >= 0)
        {
            _state.LocalMedia.FolderPlaybackOptions.RemoveAt(existingIndex);
        }

        var local = _sessions.FindSession("local");
        if (local is not null)
        {
            foreach (var localItem in local.Items.Where(item =>
                         TryGetLocalPath(item.Source, out var path)
                         && LocalFolderSourcePolicy.IsSameOrDescendant(path, normalizedPath)
                         && !ShouldRememberLocalPosition(item)))
            {
                local.ClearRememberedPosition(localItem.Id);
            }
            if (TryGetLocalPath(local.CurrentItem.Source, out var currentPath)
                && LocalFolderSourcePolicy.IsSameOrDescendant(currentPath, normalizedPath))
            {
                local.ApplyPlaybackRateForCurrentItem();
            }
        }
        TrySaveLocalMediaState(true);
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        Announce($"Zapisano opcje folderu: {folderTitle}");
        RestoreItemActionFocus();
    }

    private void RestoreItemActionFocus()
    {
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
                string.IsNullOrWhiteSpace(item.Country) ? null : $"Kraj: {item.Country}",
                string.IsNullOrWhiteSpace(item.Language) ? null : $"Język: {item.Language}",
                string.IsNullOrWhiteSpace(item.Tags) ? null : $"Kategorie: {item.Tags}",
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
        if (localPath is not null)
        {
            var saved = FindLocalItemSettings(item);
            playbackLines.Add($"Wznawianie: {FormatResumePositionMode(saved?.ResumePositionMode ?? ResumePositionMode.Inherit, item)}");
            playbackLines.Add($"Prędkość elementu: {FormatItemPlaybackRate(item, saved)}");
            playbackLines.Add("Wyjście audio: domyślne urządzenie systemowe, tryb współdzielony");
        }
        sections.Add(string.Join(Environment.NewLine, playbackLines));

        var technicalLines = new List<string> { "Techniczne" };
        if (!string.IsNullOrWhiteSpace(item.Codec))
        {
            technicalLines.Add($"Format strumienia: {item.Codec}");
        }
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
            if (CloudFileAvailability.MayRequireRemoteAccess(localPath))
            {
                technicalLines.Add("Dostępność: plik w chmurze, pobierany dopiero przy odtwarzaniu");
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

        if (item.Kind == MediaItemKind.Station && !string.IsNullOrWhiteSpace(item.Source))
        {
            var sourceLines = new List<string> { "Źródło", $"Adres strumienia: {item.Source}" };
            if (!string.IsNullOrWhiteSpace(item.HomepageUri))
            {
                sourceLines.Add($"Strona stacji: {item.HomepageUri}");
            }
            sections.Add(string.Join(Environment.NewLine, sourceLines));
        }
        else if (localPath is null && !string.IsNullOrWhiteSpace(item.PublicUri))
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

        var details = new List<string>();
        if (isLocal)
        {
            var extension = Path.GetExtension(localPath).TrimStart('.');
            if (!string.IsNullOrWhiteSpace(extension)) details.Add(extension.ToUpperInvariant());
            if (CloudFileAvailability.MayRequireRemoteAccess(localPath))
            {
                details.Add("plik w chmurze, pobierany przy odtwarzaniu");
            }
        }
        if (!string.IsNullOrWhiteSpace(item.Artist)) details.Add(item.Artist);
        if (!string.IsNullOrWhiteSpace(item.Country)) details.Add(item.Country);
        if (!string.IsNullOrWhiteSpace(item.Language)) details.Add(item.Language);
        if (!string.IsNullOrWhiteSpace(item.Codec)) details.Add(item.Codec);
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

    private async void AnnounceQuickMediaInformation(MediaItem item)
    {
        var requestVersion = Interlocked.Increment(ref _quickInformationRequestVersion);
        if (TryGetLocalPath(item.Source, out var localPath)
            && (item.Duration <= TimeSpan.Zero
                || item.BitrateKbps is null
                || item.SampleRateHz is null))
        {
            var metadata = await WindowsMediaOutput.TryReadMetadataAsync(
                localPath,
                TimeSpan.FromSeconds(5));
            if (requestVersion != Interlocked.Read(ref _quickInformationRequestVersion)
                || !string.Equals(ActionItem?.Id, item.Id, StringComparison.Ordinal))
            {
                return;
            }
            if (metadata.Success)
            {
                var changed = false;
                if (item.Duration <= TimeSpan.Zero && metadata.Duration > TimeSpan.Zero)
                {
                    item.Duration = metadata.Duration;
                    changed = true;
                }
                if (item.SampleRateHz is null && metadata.SampleRateHz > 0)
                {
                    item.SampleRateHz = metadata.SampleRateHz;
                    changed = true;
                }
                if (changed) TrySaveLocalMediaState(false);
            }
        }
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
        try
        {
            DiagnosticLog.Info("shortcut-help", "Otwieranie dostępnego spisu skrótów.");
            ClearFocusContext();
            var sections = ShortcutHelpCatalog.Create(ActiveKeyboardProfile(), _state.Settings);
            var dialog = new ShortcutHelpWindow(sections) { Owner = this };
            var accepted = dialog.ShowDialog() == true;
            var commandId = accepted ? dialog.SelectedCommandId : null;

            Activate();
            if (_playerViewActive) FocusPlayerView();
            else RestoreMediaListFocusAfterRefresh();

            if (commandId is not null)
            {
                Dispatcher.BeginInvoke(
                    () => ExecuteCommand(commandId),
                    DispatcherPriority.ContextIdle);
            }
            DiagnosticLog.Info("shortcut-help", "Zamknięto dostępny spis skrótów.");
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("shortcut-help", "Nie udało się otworzyć spisu skrótów.", exception);
            Activate();
            if (_playerViewActive) FocusPlayerView();
            else RestoreMediaListFocusAfterRefresh();
            AnnounceEssential("Nie udało się otworzyć spisu skrótów. Szczegóły zapisano w logu AMC");
        }
    }

    public void ToggleKeyboardHelp()
    {
        _keyboardHelpActive = !_keyboardHelpActive;
        if (_keyboardHelpActive)
        {
            _prefixService?.Suspend();
        }
        else
        {
            try
            {
                RegisterConfiguredPrefix();
            }
            catch (Exception exception)
            {
                DiagnosticLog.Error("keyboard-help", "Nie udało się ponownie zarejestrować prefiksu.", exception);
                AnnounceEssential("Pomoc klawiatury wyłączona. Nie udało się ponownie włączyć globalnego prefiksu");
                UpdateKeyboardHelpMenuItem();
                return;
            }
        }

        UpdateKeyboardHelpMenuItem();
        AnnounceEssential(_keyboardHelpActive
            ? "Pomoc klawiatury włączona. Naciśnij skrót, aby poznać jego działanie. Ctrl+F1 lub Escape wyłącza pomoc"
            : "Pomoc klawiatury wyłączona");
    }

    private void UpdateKeyboardHelpMenuItem()
    {
        var state = _keyboardHelpActive ? "włączona" : "wyłączona";
        KeyboardHelpMenuItem.Header = $"Pomoc _klawiatury: {state}";
        AutomationProperties.SetName(KeyboardHelpMenuItem, $"Pomoc klawiatury: {state}, Ctrl+F1");
    }

    private void ShowLegacyShortcutHelpText()
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
            "Ctrl+O dodaje lokalne pliki audio, a Ctrl+Shift+O dodaje do Biblioteki synchronizowany folder wraz z podfolderami. " +
            "W lokalnej Bibliotece Alt+1 pokazuje Foldery, Alt+2 Wszystkie pliki alfabetycznie, a Alt+3 Kolejność własną. W Kolejności własnej Alt+strzałka w górę lub w dół przenosi jeden element albo ciągły zaznaczony blok; aktywny filtr trzeba wcześniej wyczyścić. F5 odświeża Foldery Biblioteki, a Ctrl+F5 otwiera ich ustawienia. Enter wchodzi do folderu, a Backspace wraca o poziom wyżej. Na wierszu folderu Shift+Enter, Ctrl+Shift+Enter, Ctrl+Shift+U i Ctrl+Shift+P działają rekurencyjnie na jego zaindeksowanych plikach, nigdy na samym technicznym kontenerze. Żadne z tych poleceń nie uruchamia dźwięku automatycznie. " +
            "Ctrl+Shift+A otwiera Albumy; lokalnie numerowane pliki w folderze mogą utworzyć album nawet bez kompletnych tagów. Enter otwiera jego utwory, a Escape wraca do Albumów. Ctrl+U/P/L/Q otwiera odpowiednio: Ulubione, Playlisty, Bibliotekę i Kolejkę, " +
            "Ctrl+H otwiera trwałą Historię odtwarzania, Ctrl+B otwiera globalną listę Zakładek, a Ctrl+Shift+B dodaje nazwaną zakładkę w odtwarzaczu. Ctrl+K filtruje bieżącą listę. Ctrl+F otwiera okno " +
            "wyszukiwania w bieżącej usłudze, Ctrl+Shift+F otwiera wyszukiwanie globalne, " +
            "a Ctrl+Shift+K otwiera paletę poleceń. " +
            "Ctrl+N i Ctrl+A pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko.\n\n" +
            "W oknie: Enter na utworze lub stacji rozpoczyna odtwarzanie i otwiera odtwarzacz. " +
            "Ctrl+Enter odtwarza lub wstrzymuje zaznaczony element bez opuszczania listy, a Spacja steruje elementem faktycznie grającym. " +
            "Na listach Plików lokalnych lewa strzałka oznajmia wielkość i bitrate pliku. " +
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
            "Delete usuwa z bieżącego widoku. W Historii odtwarzania usuwa tylko wpis Historii, bez zmiany Biblioteki i pliku. W lokalnej Bibliotece i na pliku w widoku Foldery usuwa tylko wpis z Biblioteki AMC, a plik pozostawia na dysku; na wierszu folderu nie usuwa niczego. W odtwarzaczu lokalnym Delete również usuwa tylko wpis z AMC i pozostawia plik na dysku. Shift+Delete działa wyłącznie na listach i po potwierdzeniu przenosi zaznaczone pliki do systemowego Kosza. Backspace nigdy nie usuwa: wraca do poziomu nadrzędnego, a w polu tekstowym kasuje znak. " +
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

        if (!TryRegisterLocalFolderSource(
                dialog.FolderName,
                out var folderSource,
                out _,
                out var registrationMessage))
        {
            AnnounceEssential(registrationMessage);
            RestoreMediaListFocusAfterRefresh();
            return;
        }
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
        HidePlayerForBrowserNavigation();
        _sessions.SelectSession("local");
        _state.LocalMedia.CurrentFolderPath = folderSource.Path;
        _state.LocalMedia.LibraryView = FolderViewName;
        var navigation = GetSessionNavigationState("local");
        navigation.CurrentView = FolderViewName;
        navigation.PlayerActive = false;
        navigation.Filters[FolderViewName] = string.Empty;
        _currentView = FolderViewName;
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
            if (!TryRegisterLocalFolderSource(
                    folderSourcePath,
                    out folderSource,
                    out _,
                    out var registrationMessage))
            {
                throw new InvalidOperationException(registrationMessage);
            }
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

    private bool TryRegisterLocalFolderSource(
        string path,
        out LocalFolderSourceSettings source,
        out bool added,
        out string message)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var conflict = LocalFolderSourcePolicy.FindConflict(_state.LocalMedia.FolderSources, normalizedPath);
        if (conflict is { Kind: LocalFolderSourceConflictKind.SameSource })
        {
            source = conflict.ExistingSource;
            added = false;
            message = $"Folder „{source.DisplayName}” był już dodany do Biblioteki";
            return true;
        }
        if (conflict is not null)
        {
            source = null!;
            added = false;
            message = conflict.Kind == LocalFolderSourceConflictKind.CoveredByExistingSource
                ? $"Nie dodano folderu. Należy już do Folderu Biblioteki „{conflict.ExistingSource.DisplayName}”: {conflict.ExistingSource.Path}"
                : $"Nie dodano folderu. Obejmowałby już dodany Folder Biblioteki „{conflict.ExistingSource.DisplayName}”: {conflict.ExistingSource.Path}";
            return false;
        }

        var displayName = Path.GetFileName(normalizedPath);
        source = new LocalFolderSourceSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            Path = normalizedPath,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedPath : displayName
        };
        _state.LocalMedia.FolderSources.Add(source);
        added = true;
        message = $"Dodano Folder Biblioteki „{source.DisplayName}”";
        return true;
    }

    public async void RefreshLocalLibrary()
    {
        await SynchronizeLocalSourcesAsync(announceResult: true);
    }

    private async Task SynchronizeLocalSourcesAsync(
        bool announceResult,
        IReadOnlyCollection<string>? sourceIds = null)
    {
        if (_localSourceSyncInProgress)
        {
            _localSourceSyncPending = true;
            return;
        }

        var sources = _state.LocalMedia.FolderSources
            .Where(source => sourceIds is null
                || sourceIds.Count == 0
                || sourceIds.Contains(source.Id, StringComparer.Ordinal))
            .ToArray();
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
            var currentLocalItemId = localBeforeSync is { HasCurrentItem: true }
                ? localBeforeSync.CurrentItem.Id
                : null;
            var currentLocalWasPlaying = localBeforeSync?.IsPlaying == true;
            MediaReplacementResult? replacementResult = null;
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
                replacementResult = RefreshLocalSessionItems();
                if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) && !_playerViewActive)
                {
                    RefreshCurrentView(preferredItemId: selectedId);
                    if (hadListFocus) RestoreMediaListFocusAfterRefresh();
                }
                else if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) && _playerViewActive)
                {
                    if (_sessions.Current.HasCurrentItem) UpdatePlayerView(true);
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
                    + (currentLocalWasPlaying ? ". Odtwarzanie zatrzymano" : string.Empty)
                    + (replacementResult?.SelectedSuccessor is { } successor
                        ? $". Następny element z bieżącego widoku: {successor.Title}"
                        : ". Brak następnego elementu w bieżącym widoku"));
            }

            ConfigureLocalSourceWatchers();
            if (announceResult)
            {
                var parts = new List<string>();
                if (result.AddedItems.Count > 0) parts.Add($"dodano {FormatFileCount(result.AddedItems.Count)}");
                if (result.RestoredItems.Count > 0) parts.Add($"ponownie dostępne {FormatFileCount(result.RestoredItems.Count)}");
                if (result.BecameUnavailableItems.Count > 0)
                    parts.Add($"niedostępne {FormatFileCount(result.BecameUnavailableItems.Count)}");
                if (failed.Length > 0) parts.Add($"niedostępne foldery: {failed.Length}");
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
            if (!item.HasCustomTitle)
            {
                UpdateLocalItemTitle(item, Path.GetFileNameWithoutExtension(replacement), false);
            }
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
                HasCustomTitle = saved.HasCustomTitle,
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

    private void LoadPersistedRadio()
    {
        foreach (var saved in _state.Radio.Stations)
        {
            _radioItems.Add(new MediaItem
            {
                Id = saved.Id,
                Title = saved.Name,
                HasCustomTitle = saved.HasCustomTitle || saved.IsCustom,
                Kind = MediaItemKind.Station,
                Source = saved.StreamUrl,
                PublicUri = saved.StreamUrl,
                HomepageUri = saved.HomepageUrl,
                Country = saved.Country,
                Language = saved.Language,
                Tags = saved.Tags,
                Codec = saved.Codec,
                ExternalId = saved.DirectoryId,
                BitrateKbps = saved.BitrateKbps,
                IsFavorite = saved.IsFavorite,
                IsInLibrary = saved.IsInLibrary,
                IsAvailable = true,
                IsInQueue = saved.IsInQueue,
                IsPlayNext = saved.IsPlayNext
            });
        }
    }

    private void CaptureRadioState()
    {
        var radio = _sessions?.FindSession("radio");
        if (radio is not null)
        {
            _state.Radio.CurrentItemId = radio.HasCurrentItem ? radio.CurrentItem.Id : null;
            _state.Radio.Volume = radio.Volume;
        }
        _state.Radio.Stations = _radioItems.Select(item => new RadioStationSettings
        {
            Id = item.Id,
            Name = item.Title,
            StreamUrl = item.Source ?? string.Empty,
            HomepageUrl = item.HomepageUri,
            Country = item.Country,
            Language = item.Language,
            Tags = item.Tags,
            Codec = item.Codec,
            DirectoryId = item.ExternalId,
            BitrateKbps = item.BitrateKbps,
            HasCustomTitle = item.HasCustomTitle,
            IsFavorite = item.IsFavorite,
            IsInLibrary = item.IsInLibrary,
            IsInQueue = item.IsInQueue,
            IsPlayNext = item.IsPlayNext,
            IsCustom = string.IsNullOrWhiteSpace(item.ExternalId)
        }).ToList();
    }

    private async Task PrepareRadioSearchAsync(string query, CancellationToken cancellationToken)
    {
        var found = await _radioCatalog.SearchAsync(query, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var radio = _sessions.FindSession("radio");
        if (radio is null) return;

        foreach (var candidate in found)
        {
            var existing = _radioItems.FirstOrDefault(item =>
                string.Equals(item.Id, candidate.Id, StringComparison.Ordinal)
                || string.Equals(item.Source, candidate.Source, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _radioItems.Add(candidate);
                continue;
            }
            if (!existing.HasCustomTitle) existing.Title = candidate.Title;
            existing.HomepageUri = candidate.HomepageUri;
            existing.Country = candidate.Country;
            existing.Language = candidate.Language;
            existing.Tags = candidate.Tags;
            existing.Codec = candidate.Codec;
            existing.ExternalId ??= candidate.ExternalId;
            existing.BitrateKbps = candidate.BitrateKbps;
        }
        radio.AddItems(_radioItems);
        CaptureRadioState();
        _store.Save(_state);
    }

    private void CaptureLocalMediaState()
    {
        var local = _sessions?.FindSession("local");
        if (local is not null) local.RememberCurrentPosition();
        EnsureLocalCustomOrder();

        var savedById = _state.LocalMedia.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var savedByPath = _state.LocalMedia.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => NormalizeLocalFilePath(item.Path), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var rememberedPositions = local?.RememberedPositions;
        _state.LocalMedia.Items = _localItems.Select(item =>
        {
            savedById.TryGetValue(item.Id, out var previous);
            if (previous is null
                && TryGetLocalPath(item.Source, out var itemPath))
            {
                savedByPath.TryGetValue(NormalizeLocalFilePath(itemPath), out previous);
            }
            var (fileLength, lastWriteUtcTicks) = previous is null
                ? GetFileFingerprint(item.Source)
                : (previous.FileLength, previous.LastWriteUtcTicks);
            var position = ShouldRememberLocalPosition(item)
                ? rememberedPositions?.GetValueOrDefault(item.Id)
                  ?? (previous is null ? TimeSpan.Zero : TimeSpan.FromTicks(previous.ResumePositionTicks))
                : TimeSpan.Zero;
            return new LocalMediaItemSettings
            {
                Id = item.Id,
                Title = item.Title,
                HasCustomTitle = item.HasCustomTitle,
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
                ResumePositionMode = previous?.ResumePositionMode ?? ResumePositionMode.Inherit,
                PlaybackRateOverride = previous?.PlaybackRateOverride,
                OutputDeviceId = previous?.OutputDeviceId,
                ResumePositionTicks = Math.Max(0, position.Ticks),
                FileLength = fileLength,
                LastWriteUtcTicks = lastWriteUtcTicks
            };
        }).ToList();

        if (local is not null && local.HasCurrentItem)
        {
            _state.LocalMedia.CurrentItemId = local.CurrentItem.Id;
            _state.LocalMedia.Volume = local.Volume;
            if (FindLocalItemSettings(local.CurrentItem)?.PlaybackRateOverride is null)
            {
                _state.LocalMedia.PlaybackRate = local.PlaybackRate;
            }
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
        CaptureRadioState();
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
        // Reading FileInfo.Length for RecallOnDataAccess placeholders may ask a
        // cloud provider to hydrate data. Fingerprints for those files are
        // refreshed only after explicit playback has made the payload local.
        if (CloudFileAvailability.MayRequireRemoteAccess(path)) return (null, null);
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

    private bool ShouldRememberLocalPosition(MediaItem item) =>
        FindLocalItemSettings(item)?.ResumePositionMode switch
        {
            ResumePositionMode.Remember => true,
            ResumePositionMode.StartFromBeginning => false,
            _ => ShouldRememberLocalPosition(item.Source)
        };

    private LocalMediaItemSettings? FindLocalItemSettings(MediaItem item)
    {
        var byId = _state.LocalMedia.Items.FirstOrDefault(saved =>
            string.Equals(saved.Id, item.Id, StringComparison.Ordinal));
        if (byId is not null || !TryGetLocalPath(item.Source, out var itemPath)) return byId;
        var normalizedItemPath = NormalizeLocalFilePath(itemPath);
        return _state.LocalMedia.Items.FirstOrDefault(saved =>
            string.Equals(
                NormalizeLocalFilePath(saved.Path),
                normalizedItemPath,
                StringComparison.OrdinalIgnoreCase));
    }

    private double? GetLocalPlaybackRateOverride(MediaItem item) =>
        FindLocalItemSettings(item)?.PlaybackRateOverride
        ?? GetFolderPlaybackRateOverride(item.Source);

    private string FormatResumePositionMode(ResumePositionMode mode, MediaItem item) => mode switch
    {
            ResumePositionMode.Remember => "Pamiętaj pozycję odtwarzania",
            ResumePositionMode.StartFromBeginning => "Zawsze od początku",
            _ => ShouldRememberLocalPosition(item.Source)
            ? "Zgodnie z ustawieniem folderu lub globalnym — pamiętaj pozycję odtwarzania"
            : "Zgodnie z ustawieniem folderu lub globalnym — zawsze od początku"
    };

    private string FormatItemPlaybackRate(MediaItem item, LocalMediaItemSettings? saved)
    {
        if (saved?.PlaybackRateOverride is double itemRate)
        {
            return $"{FormatPlaybackRateMultiplier(itemRate)} — dla tego elementu";
        }
        if (GetFolderPlaybackRateOverride(item.Source) is double folderRate)
        {
            return $"{FormatPlaybackRateMultiplier(folderRate)} — według folderu";
        }
        return "według prędkości sesji";
    }

    private bool ShouldRememberLocalPosition(string? path)
    {
        if (!TryGetLocalPath(path, out var localPath))
        {
            return _state.Settings.RememberLocalPlaybackPositions;
        }

        var folderMode = _state.LocalMedia.FolderPlaybackOptions
            .Where(option => option.ResumePositionMode != ResumePositionMode.Inherit
                && LocalFolderSourcePolicy.IsSameOrDescendant(localPath, option.Path))
            .OrderByDescending(option => option.Path.Length)
            .Select(option => (ResumePositionMode?)option.ResumePositionMode)
            .FirstOrDefault();
        if (folderMode.HasValue)
        {
            return folderMode.Value == ResumePositionMode.Remember;
        }

        var source = _state.LocalMedia.FolderSources
            .Where(candidate => LocalFolderSourcePolicy.IsSameOrDescendant(localPath, candidate.Path))
            .OrderByDescending(candidate => candidate.Path.Length)
            .FirstOrDefault();
        return source?.ResumePositionMode switch
        {
            ResumePositionMode.Remember => true,
            ResumePositionMode.StartFromBeginning => false,
            _ => _state.Settings.RememberLocalPlaybackPositions
        };
    }

    private double? GetFolderPlaybackRateOverride(string? path)
    {
        if (!TryGetLocalPath(path, out var localPath)) return null;
        return _state.LocalMedia.FolderPlaybackOptions
            .Where(option => option.PlaybackRateOverride.HasValue
                && LocalFolderSourcePolicy.IsSameOrDescendant(localPath, option.Path))
            .OrderByDescending(option => option.Path.Length)
            .Select(option => option.PlaybackRateOverride)
            .FirstOrDefault();
    }

    private LocalFolderPlaybackSettings? FindExactFolderPlaybackSettings(string folderPath) =>
        _state.LocalMedia.FolderPlaybackOptions.FirstOrDefault(option =>
            string.Equals(
                NormalizeLocalFolderPath(option.Path),
                folderPath,
                StringComparison.OrdinalIgnoreCase));

    private static string NormalizeLocalFilePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeLocalFolderPath(string path)
    {
        var normalized = NormalizeLocalFilePath(path);
        return normalized.Length == 0
            ? string.Empty
            : Path.TrimEndingDirectorySeparator(normalized);
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
        var previousRadio = _sessions?.FindSession("radio");
        var previousRadioItemId = previousRadio?.HasCurrentItem == true ? previousRadio.CurrentItem.Id : null;
        var previousRadioWasPlaying = previousRadio?.IsPlaying == true;
        _membershipHistory.Clear();
        _localCatalogHistory.Clear();
        _playlistHistory.Clear();
        _queueOrderHistory.Clear();
        _playbackHistoryCursors.Clear();
        _undoSequence = 0;
        _sessions = new SessionManager(_state.Settings);
        var (local, _) = _sessions.AddOrUpdateTransientSession(
            "local",
            "Pliki lokalne",
            ActiveLocalItems(),
            _localOutput,
            1,
            ShouldRememberLocalPosition,
            GetLocalPlaybackRateOverride);
        if (local.HasItems)
        {
            foreach (var saved in _state.LocalMedia.Items
                         .Where(saved => ShouldRememberLocalPosition(saved.Path))
                         .Where(CanRestorePosition))
            {
                local.SetRememberedPosition(saved.Id, TimeSpan.FromTicks(saved.ResumePositionTicks));
            }
            var restoredItemId = previousLocalItemId ?? _state.LocalMedia.CurrentItemId;
            var restoredItem = local.Items.FirstOrDefault(item => item.Id == restoredItemId);
            if (restoredItem is not null) local.SelectItem(restoredItem);
            if (previousLocal is not null)
            {
                local.SetPosition(ShouldRememberLocalPosition(local.CurrentItem)
                    ? previousLocalPosition
                    : TimeSpan.Zero);
            }
        }
        local.SetVolume(previousLocal?.Volume ?? _state.LocalMedia.Volume);
        local.SetDefaultPlaybackRate(_state.LocalMedia.PlaybackRate);
        local.ApplyPlaybackRateForCurrentItem();
        RestorePlaybackContext(local);
        EnsureQueueOrder(local);
        if (previousLocalWasPlaying
            && previousLocalItemId is not null
            && local.Items.Any(item => string.Equals(item.Id, previousLocalItemId, StringComparison.Ordinal)))
        {
            local.Play(local.CurrentItem);
        }
        var (radio, _) = _sessions.AddOrUpdateTransientSession(
            "radio",
            "Radio internetowe",
            _radioItems,
            _radioOutput,
            5,
            _ => false);
        if (radio.HasItems)
        {
            var restoredRadioId = previousRadioItemId ?? _state.Radio.CurrentItemId;
            var restoredRadio = radio.Items.FirstOrDefault(item => item.Id == restoredRadioId);
            if (restoredRadio is not null) radio.SelectItem(restoredRadio);
        }
        radio.SetVolume(previousRadio?.Volume ?? _state.Radio.Volume);
        RestorePlaybackContext(radio);
        EnsureQueueOrder(radio);
        if (previousRadioWasPlaying && radio.HasCurrentItem) radio.Play(radio.CurrentItem);
        if (string.Equals(desiredSessionId, "local", StringComparison.Ordinal))
        {
            _sessions.SelectSession("local");
        }
        else if (string.Equals(desiredSessionId, "radio", StringComparison.Ordinal))
        {
            _sessions.SelectSession("radio");
        }
        _router = new CommandRouter(_sessions, _state.Settings, this, this);
        foreach (var session in _sessions.Sessions.Where(session =>
                     !string.Equals(session.Id, "local", StringComparison.Ordinal)))
        {
            RestorePlaybackContext(session);
            EnsureQueueOrder(session);
        }
    }

    private void RestorePlaybackContext(DemoMediaSession session)
    {
        var navigation = GetSessionNavigationState(session.Id);
        if (navigation.PlaybackContextItemIds.Count > 0)
        {
            session.SetPlaybackContext(
                navigation.PlaybackContextItemIds,
                string.Equals(navigation.PlaybackContextView, "Kolejka", StringComparison.Ordinal));
        }
    }

    private IEnumerable<MediaItem> ActiveLocalItems() =>
        _localItems.Where(item => item.IsAvailable && item.IsInLibrary);

    private MediaReplacementResult RefreshLocalSessionItems()
    {
        var local = _sessions.FindSession("local");
        if (local is null) return new(false, null);
        var result = local.ReplaceItems(ActiveLocalItems());
        EnsureQueueOrder(local);
        return result;
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

    private void ClearPersistedListFiltersAtStartup()
    {
        foreach (var navigation in _state.SessionNavigation.Sessions.Values)
        {
            navigation.Filters.Clear();
        }
    }

    private void ClearFilterForNavigation(
        SessionNavigationState navigation,
        params string[] viewNames)
    {
        foreach (var viewName in viewNames.Distinct(StringComparer.Ordinal))
        {
            navigation.Filters[viewName] = string.Empty;
        }

        _restoringSessionNavigation = true;
        try
        {
            FilterBox.Clear();
        }
        finally
        {
            _restoringSessionNavigation = false;
        }
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
        _cloudPreparingItemId = null;
        _sessions.FindSession("local")?.MarkPlaybackFailed();
        RefreshPlaybackIndicators();
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        TrySaveLocalMediaState(false);
        var title = e.Item?.Title ?? "plik";
        Announce($"Nie można odtworzyć: {title}. {e.Message}");
    }

    private void LocalOutput_PlaybackPreparing(object? sender, MediaPlaybackPreparingEventArgs e)
    {
        _cloudPreparingItemId = e.CloudDownloadRequired ? e.Item.Id : null;
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        if (e.CloudDownloadRequired)
        {
            AnnounceEssential($"Pobieranie z chmury: {e.Item.Title}. Interfejs pozostaje dostępny");
        }
    }

    private void LocalOutput_PlaybackStarted(object? sender, MediaPlaybackStartedEventArgs e)
    {
        var completedCloudDownload = string.Equals(
            _cloudPreparingItemId,
            e.Item.Id,
            StringComparison.Ordinal);
        _cloudPreparingItemId = null;
        RefreshPlaybackIndicators();
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        if (completedCloudDownload) Announce($"Odtwarzanie: {e.Item.Title}");
    }

    private void LocalOutput_PlaybackEnded(object? sender, MediaPlaybackEndedEventArgs e)
    {
        _cloudPreparingItemId = null;
        var localSession = _sessions.FindSession("local");
        var nextItem = localSession?.ContinueAfterPlaybackEnded(e.Item);
        if (localSession is not null) EnsureQueueOrder(localSession);
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

    private void RadioOutput_PlaybackPreparing(object? sender, MediaPlaybackPreparingEventArgs e)
    {
        if (_state.Settings.Messages.LoadingMessages)
        {
            Announce($"Łączenie: {e.Item.Title}");
        }
        UpdatePlaybackStatusBar();
    }

    private void RadioOutput_PlaybackStarted(object? sender, MediaPlaybackStartedEventArgs e)
    {
        var radio = _sessions.FindSession("radio");
        if (radio is not null && string.Equals(radio.CurrentItem.Id, e.Item.Id, StringComparison.Ordinal))
        {
            RecordPlayback(radio, e.Item);
        }
        CaptureRadioState();
        _store.Save(_state);
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
    }

    private void RadioOutput_PlaybackFailed(object? sender, MediaOutputFailedEventArgs e)
    {
        var radio = _sessions.FindSession("radio");
        if (radio is not null) radio.StopPlayback();
        if (_state.Settings.Messages.ErrorMessages) AnnounceEssential(e.Message);
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
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
        var previousOverride = _actionItemsOverride;
        FolderContentsActionContext? folderContext = null;
        if (IsFolderContentsCommand(commandId)
            && TryResolveSelectedFolderContents(out folderContext))
        {
            if (folderContext.Items.Count == 0)
            {
                Announce($"Folder {folderContext.FolderLabel} nie zawiera dostępnych plików audio");
                return new CommandExecutionResult(true);
            }
            _actionItemsOverride = folderContext.Items;
        }
        else if (commandId == CommandIds.ToggleLibrary && HasSelectedFolderRow())
        {
            Announce("Folder jest już częścią Biblioteki. Otwórz go Enterem, aby zmieniać przynależność pojedynczych plików");
            return new CommandExecutionResult(true);
        }

        try
        {
            return ExecuteCommandCore(commandId, folderContext);
        }
        finally
        {
            _actionItemsOverride = previousOverride;
        }
    }

    private CommandExecutionResult ExecuteCommandCore(
        string commandId,
        FolderContentsActionContext? folderContext)
    {
        if (commandId == CommandIds.ViewRadio)
        {
            CaptureCurrentSessionNavigationState();
            HidePlayerForBrowserNavigation();
            var radio = _sessions.SelectSession("radio");
            if (radio is null)
            {
                Announce("Sesja radia internetowego jest niedostępna");
                return new CommandExecutionResult(false);
            }
            _currentView = "Biblioteka";
            var navigation = GetSessionNavigationState("radio");
            navigation.CurrentView = _currentView;
            navigation.PlayerActive = false;
            RefreshCurrentView();
            PrepareViewFocusContext("Biblioteka, Radio internetowe");
            RestoreMediaListFocusAfterRefresh();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.AddRadioStation)
        {
            AddRadioStation();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ToggleRadioRecording)
        {
            ToggleRadioRecording();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.RadioJumpLive)
        {
            if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
            {
                Announce("Powrót na żywo jest dostępny w odtwarzaczu radia");
                return new CommandExecutionResult(false);
            }
            _radioOutput.JumpToLive();
            Announce("Na żywo");
            UpdatePlayerView();
            UpdatePlaybackStatusBar();
            return new CommandExecutionResult(true);
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            if (commandId is CommandIds.AddBookmark
                or CommandIds.AddNamedBookmark
                or CommandIds.ViewBookmarks
                or CommandIds.PreviousBookmark
                or CommandIds.NextBookmark
                or CommandIds.SeekToTime
                or CommandIds.SeekToPercentage
                or CommandIds.PlaybackRateDown
                or CommandIds.PlaybackRateUp
                or CommandIds.PlaybackRateReset
                || commandId.StartsWith("transport.seekPercent.", StringComparison.Ordinal))
            {
                Announce("Ta funkcja nie dotyczy transmisji radiowej na żywo");
                return new CommandExecutionResult(true);
            }
            if (commandId == CommandIds.TrackStart)
            {
                _sessions.Current.SetPosition(TimeSpan.Zero);
                Announce("Najstarsze dostępne miejsce bufora");
                return new CommandExecutionResult(true);
            }
            if (commandId == CommandIds.TrackEnd)
            {
                _radioOutput.JumpToLive();
                Announce("Na żywo");
                return new CommandExecutionResult(true);
            }
            if (commandId == CommandIds.TimeRemaining)
            {
                Announce(_radioOutput.BehindLive < TimeSpan.FromSeconds(1)
                    ? "Na żywo"
                    : $"Za transmisją: {CommandRouter.FormatTime(_radioOutput.BehindLive)}");
                return new CommandExecutionResult(true);
            }
            if (commandId == CommandIds.TimeElapsed)
            {
                var inBuffer = _radioOutput.BufferedDuration - _radioOutput.BehindLive;
                Announce($"Pozycja w buforze: {CommandRouter.FormatTime(inBuffer < TimeSpan.Zero ? TimeSpan.Zero : inBuffer)}");
                return new CommandExecutionResult(true);
            }
            if (commandId == CommandIds.TimeTotal)
            {
                Announce($"Bufor: {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}");
                return new CommandExecutionResult(true);
            }
        }
        if (commandId is not CommandIds.PreviousBookmark and not CommandIds.NextBookmark)
        {
            _bookmarkNavigationCursor = null;
        }
        var sessionBeforeCommand = _sessions.Current;
        if (commandId == CommandIds.ActivateSelected
            && !_playerViewActive
            && !_preservePreparedPlaybackContext
            && ActionItem is { } selectedForPlayback)
        {
            PreparePlaybackContextForCurrentView(sessionBeforeCommand, selectedForPlayback);
        }
        var oldSession = sessionBeforeCommand.Id;
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
        var orderSnapshot = changesListMembership && changedSession is not null
            ? CaptureMembershipOrderForCommand(changedSession, commandId, changedItems)
            : null;
        if (changesListMembership && restoreListFocus) AnchorMediaListFocus();
        if (changesListMembership || mergeSessionAnnouncementWithFocus)
        {
            _deferredAnnouncement = null;
            _deferAnnouncements = true;
        }
        CommandExecutionResult result;
        try
        {
            result = folderContext is not null && IsFolderCollectionToggleCommand(commandId)
                ? ExecuteFolderCollectionToggle(commandId, folderContext.Items)
                : _router.Execute(commandId);
        }
        finally
        {
            _deferAnnouncements = false;
        }
        if (folderContext is not null && result.Handled && changesListMembership)
        {
            _deferredAnnouncement = BuildFolderContentsActionAnnouncement(commandId, folderContext);
        }
        if (changedSession is not null
            && commandId is CommandIds.AddQueue or CommandIds.TogglePlayNext)
        {
            EnsureQueueOrder(changedSession);
        }
        var queuePlaybackChanged = result.Handled
            && commandId is CommandIds.ActivateSelected or CommandIds.Previous or CommandIds.Next
            && _sessions.Current.QueueNavigationActive;
        if (queuePlaybackChanged) EnsureQueueOrder(_sessions.Current);
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
        if (changesListMembership
            && string.Equals(changedSession?.Id, "radio", StringComparison.Ordinal))
        {
            foreach (var item in changedItems)
            {
                if (item.IsFavorite) item.IsInLibrary = true;
                if (!item.IsInLibrary)
                {
                    item.IsFavorite = false;
                    item.IsInQueue = false;
                    item.IsPlayNext = false;
                }
            }
            CaptureRadioState();
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
                ++_undoSequence,
                orderSnapshot);
        }
        var sessionChanged = _sessions.Current.Id != oldSession;
        if (sessionChanged)
        {
            if (_playerViewActive) ApplyPlaybackPolicyWhenLeavingPlayer(sessionBeforeCommand);
            ClearFilterForNavigation(
                GetSessionNavigationState(sessionBeforeCommand.Id),
                _currentView);
            var destinationNavigation = GetSessionNavigationState(_sessions.Current.Id);
            ClearFilterForNavigation(
                destinationNavigation,
                string.IsNullOrWhiteSpace(destinationNavigation.CurrentView)
                    ? DefaultBrowserView
                    : destinationNavigation.CurrentView);
            RestoreCurrentSessionNavigationState();
            if (string.Equals(sessionBeforeCommand.Id, "local", StringComparison.Ordinal))
            {
                TrySaveLocalMediaState(false);
            }
            else if (string.Equals(sessionBeforeCommand.Id, "radio", StringComparison.Ordinal))
            {
                CaptureRadioState();
                _store.Save(_state);
            }
        }
        else if (changesListMembership)
        {
            RefreshCurrentView(changesListMembership ? previousIndex : null);
            if (!_playerViewActive && changedItems.Length > 1 && folderContext is null)
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
        else if (queuePlaybackChanged
                 && !_playerViewActive
                 && string.Equals(_currentView, "Kolejka", StringComparison.Ordinal))
        {
            AnchorMediaListFocus();
            RefreshCurrentView(previousIndex);
            RestoreMediaListFocusAfterRefresh();
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
        if (savesPlaybackBoundary && result.Handled)
        {
            EnsureQueueOrder(_sessions.Current);
        }
        if (commandId is CommandIds.PlaybackRateDown
            or CommandIds.PlaybackRateUp
            or CommandIds.PlaybackRateReset
            && result.Handled
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            var localItemSettings = FindLocalItemSettings(_sessions.Current.CurrentItem);
            if (localItemSettings?.PlaybackRateOverride is not null)
            {
                localItemSettings.PlaybackRateOverride = _sessions.Current.PlaybackRate;
            }
            else
            {
                _state.LocalMedia.PlaybackRate = _sessions.Current.PlaybackRate;
                _sessions.Current.SetDefaultPlaybackRate(_state.LocalMedia.PlaybackRate);
            }
            TrySaveLocalMediaState(false);
        }
        if (commandId is CommandIds.VolumeUp5 or CommandIds.VolumeDown5
            or CommandIds.VolumeUp1 or CommandIds.VolumeDown1
            && result.Handled
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            CaptureRadioState();
            _store.Save(_state);
        }
        if (savesPlaybackBoundary && result.Handled && _sessions.Current.IsPlaying)
        {
            RecordPlayback(_sessions.Current, _sessions.Current.CurrentItem);
        }
        if ((changesListMembership && string.Equals(changedSession?.Id, "local", StringComparison.Ordinal))
            || (savesPlaybackBoundary && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)))
        {
            TrySaveLocalMediaState(false);
        }
        else if (changesListMembership && changedSession is not null)
        {
            if (string.Equals(changedSession.Id, "radio", StringComparison.Ordinal)) CaptureRadioState();
            _store.Save(_state);
        }
        else if (savesPlaybackBoundary && result.Handled)
        {
            if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)) CaptureRadioState();
            _store.Save(_state);
        }
        return result;
    }

    private static bool IsFolderContentsCommand(string commandId) => commandId is
        CommandIds.ToggleFavorite
        or CommandIds.AddQueue
        or CommandIds.TogglePlayNext
        or CommandIds.ManagePlaylists;

    private static bool IsFolderCollectionToggleCommand(string commandId) => commandId is
        CommandIds.ToggleFavorite
        or CommandIds.AddQueue
        or CommandIds.TogglePlayNext;

    private static CommandExecutionResult ExecuteFolderCollectionToggle(
        string commandId,
        IReadOnlyList<MediaItem> items)
    {
        switch (commandId)
        {
            case CommandIds.ToggleFavorite:
                FolderContentsMembership.ToggleFavorites(items);
                return new CommandExecutionResult(true);

            case CommandIds.AddQueue:
                FolderContentsMembership.ToggleQueue(items);
                return new CommandExecutionResult(true);

            case CommandIds.TogglePlayNext:
                FolderContentsMembership.TogglePlayNext(items);
                return new CommandExecutionResult(true);

            default:
                return new CommandExecutionResult(false);
        }
    }

    private bool HasSelectedFolderRow() => !_playerViewActive
        && MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .Any(row => row.FolderPath is not null);

    private bool TryResolveSelectedFolderContents(out FolderContentsActionContext context)
    {
        context = default!;
        if (_playerViewActive
            || !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            return false;
        }

        var selectedRows = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .ToArray();
        if (selectedRows.Length == 0 && MediaList.SelectedItem is MediaItemRow selectedRow)
        {
            selectedRows = [selectedRow];
        }
        var folderRows = selectedRows
            .Where(row => row.FolderPath is not null)
            .ToArray();
        if (folderRows.Length == 0) return false;

        var activeItems = ActiveLocalItems().ToArray();
        var resolved = new List<MediaItem>();
        var knownIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in selectedRows)
        {
            if (row.FolderPath is { } folderPath)
            {
                foreach (var item in activeItems
                             .Where(item => item.Kind is MediaItemKind.Track or MediaItemKind.Station
                                 && TryGetLocalPath(item.Source, out var itemPath)
                                 && IsSameOrDescendantPath(itemPath, folderPath))
                             .OrderBy(
                                 item => TryGetLocalPath(item.Source, out var itemPath)
                                     ? Path.GetRelativePath(folderPath, itemPath)
                                     : item.Title,
                                 StringComparer.CurrentCultureIgnoreCase))
                {
                    if (knownIds.Add(item.Id)) resolved.Add(item);
                }
                continue;
            }

            var actionItem = row.ActionItem;
            if (actionItem.Kind is MediaItemKind.Track or MediaItemKind.Station
                && knownIds.Add(actionItem.Id))
            {
                resolved.Add(actionItem);
            }
        }

        var folderLabel = folderRows.Length == 1
            ? $"„{folderRows[0].Item.Title}”"
            : $"{folderRows.Length} wybranych folderów";
        context = new FolderContentsActionContext(folderLabel, resolved);
        return true;
    }

    private static string BuildFolderContentsActionAnnouncement(
        string commandId,
        FolderContentsActionContext context)
    {
        var count = FormatFileCount(context.Items.Count);
        return commandId switch
        {
            CommandIds.ToggleFavorite when context.Items.All(item => item.IsFavorite) =>
                $"Dodano do ulubionych zawartość folderu {context.FolderLabel}: {count}",
            CommandIds.ToggleFavorite =>
                $"Usunięto z ulubionych zawartość folderu {context.FolderLabel}: {count}",
            CommandIds.AddQueue when context.Items.All(item => item.IsInQueue || item.IsPlayNext) =>
                $"Dodano do kolejki zawartość folderu {context.FolderLabel}: {count}",
            CommandIds.AddQueue =>
                $"Usunięto z kolejki zawartość folderu {context.FolderLabel}: {count}",
            CommandIds.TogglePlayNext when context.Items.All(item => item.IsPlayNext) =>
                $"Odtwarzaj jako następne zawartość folderu {context.FolderLabel}: {count}",
            CommandIds.TogglePlayNext =>
                $"Usunięto z następnych zawartość folderu {context.FolderLabel}: {count}",
            _ => $"Zmieniono zawartość folderu {context.FolderLabel}: {count}"
        };
    }

    private void RefreshCurrentView(int? fallbackIndex = null, string? preferredItemId = null)
    {
        ClearFocusContext();
        preferredItemId ??= SelectedItem?.Id;
        SessionHeading.Text = string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal)
            ? "Wszystkie sesje"
            : _sessions.Current.DisplayName;
        ViewHeading.Text = CurrentViewDisplayName();
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

        if (string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal)
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            EnsureLocalCustomOrder();
            ViewHeading.Text = "Biblioteka — Kolejność własna";
            _unfilteredItems = LocalLibraryManualOrder.Order(
                    ActiveLocalItems(),
                    _state.LocalMedia.CustomOrderItemIds)
                .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText))
                .ToList();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        if (string.Equals(_currentView, "Playlisty", StringComparison.Ordinal))
        {
            _unfilteredItems = CreatePlaylistRows();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        if (TryGetPlaylistIdFromView(_currentView, out var playlistId))
        {
            var playlist = new PlaylistIndex(_state.Playlists).Find(playlistId);
            if (playlist is null
                || !string.Equals(playlist.SessionId, _sessions.Current.Id, StringComparison.OrdinalIgnoreCase))
            {
                _currentView = "Playlisty";
                GetSessionNavigationState(_sessions.Current.Id).CurrentView = _currentView;
                RefreshCurrentView(fallbackIndex: fallbackIndex);
                return;
            }
            ViewHeading.Text = $"Playlista — {playlist.Name}";
            var itemsById = _sessions.Current.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
            _unfilteredItems = playlist.ItemIds
                .Select(itemId => itemsById.GetValueOrDefault(itemId))
                .Where(item => item is not null)
                .Select(item => new MediaItemRow(item!, FormatListItem(item!), item!.PrimaryText))
                .ToList();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        if (string.Equals(_currentView, "Albumy", StringComparison.Ordinal)
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            ViewHeading.Text = "Biblioteka — Albumy";
            _unfilteredItems = InferLocalAlbums()
                .Select(album =>
                {
                    var item = new MediaItem
                    {
                        Id = album.Id,
                        Title = album.Title,
                        Artist = album.Artist,
                        Kind = MediaItemKind.Album,
                        Duration = album.Duration,
                        IsInLibrary = true
                    };
                    var label = $"{FormatListItem(item)}, {FormatTrackCount(album.Tracks.Count)}";
                    return new MediaItemRow(
                        item,
                        label,
                        item.PrimaryText,
                        albumFolderPath: album.FolderPath);
                })
                .ToList();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        if (string.Equals(_currentView, LocalAlbumContentsViewName, StringComparison.Ordinal)
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            ViewHeading.Text = CurrentViewDisplayName();
            _unfilteredItems = LocalAlbumInference.OrderTracks(
                    ActiveLocalItems().Where(item => IsDirectChildOfAlbum(item, _currentLocalAlbumPath)))
                .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText))
                .ToList();
            ApplyFilter(preferredItemId, fallbackIndex);
            return;
        }

        IEnumerable<MediaItem> items = _sessions.Current.Items;
        if (_currentView == "Ulubione")
        {
            var favoriteItems = items.Where(item => item.IsFavorite).ToArray();
            var favoriteOrder = EnsureFavoriteOrder(_sessions.Current, favoriteItems);
            items = LocalLibraryManualOrder.Order(favoriteItems, favoriteOrder);
        }
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
        if (_currentView == "Kolejka") items = OrderedQueueItems(_sessions.Current);
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

    private List<MediaItemRow> CreatePlaylistRows()
    {
        ViewHeading.Text = "Playlisty";
        var itemsById = _sessions.Current.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return new PlaylistIndex(_state.Playlists)
            .GetForSession(_sessions.Current.Id)
            .Select(playlist =>
            {
                var availableItems = playlist.ItemIds
                    .Select(itemId => itemsById.GetValueOrDefault(itemId))
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .ToArray();
                var durationTicks = availableItems.Aggregate(
                    0L,
                    (total, item) => item.Duration.Ticks > long.MaxValue - total
                        ? long.MaxValue
                        : total + item.Duration.Ticks);
                var item = new MediaItem
                {
                    Id = $"playlist:{playlist.Id}",
                    Title = playlist.Name,
                    Kind = MediaItemKind.Playlist,
                    Duration = TimeSpan.FromTicks(durationTicks),
                    IsInLibrary = true
                };
                var availability = availableItems.Length == playlist.ItemIds.Count
                    ? FormatItemCount(playlist.ItemIds.Count)
                    : $"dostępne {availableItems.Length} z {playlist.ItemIds.Count}";
                var duration = durationTicks > 0 ? $", {FormatDurationWords(item.Duration)}" : string.Empty;
                return new MediaItemRow(
                    item,
                    $"{playlist.Name}, {availability}{duration}",
                    playlist.Name,
                    playlistId: playlist.Id);
            })
            .ToList();
    }

    private void EnsureLocalCustomOrder()
    {
        _state.LocalMedia.CustomOrderItemIds = LocalLibraryManualOrder.Normalize(
            _state.LocalMedia.CustomOrderItemIds,
            _localItems,
            initializeAlphabetically: true);
    }

    private List<string> EnsureFavoriteOrder(
        DemoMediaSession session,
        IEnumerable<MediaItem>? favoriteItems = null)
    {
        var items = (favoriteItems ?? session.Items.Where(item => item.IsFavorite)).ToArray();
        var stored = _state.CollectionOrders.FavoriteItemIdsBySession.GetValueOrDefault(session.Id);
        var normalized = LocalLibraryManualOrder.Normalize(stored, items);
        _state.CollectionOrders.FavoriteItemIdsBySession[session.Id] = normalized;
        return normalized;
    }

    private List<string> EnsureQueueOrder(
        DemoMediaSession session,
        IEnumerable<MediaItem>? queueItems = null)
    {
        var items = (queueItems ?? session.Items.Where(item => item.IsInQueue || item.IsPlayNext)).ToArray();
        var stored = _state.CollectionOrders.QueueItemIdsBySession.GetValueOrDefault(session.Id);
        var normalized = LocalLibraryManualOrder.Normalize(stored, items);
        _state.CollectionOrders.QueueItemIdsBySession[session.Id] = normalized;
        session.SetQueueOrder(normalized);
        return normalized;
    }

    private IReadOnlyList<MediaItem> OrderedQueueItems(DemoMediaSession session)
    {
        var items = session.Items.Where(item => item.IsInQueue || item.IsPlayNext).ToArray();
        var stored = EnsureQueueOrder(session, items);
        return LocalLibraryManualOrder.Order(items, stored)
            .OrderByDescending(item => item.IsPlayNext)
            .ToArray();
    }

    private IReadOnlyList<LocalAlbumGroup> InferLocalAlbums() =>
        LocalAlbumInference.Infer(
            ActiveLocalItems(),
            _state.LocalMedia.FolderSources.Select(source => source.Path));

    private static bool IsDirectChildOfAlbum(MediaItem item, string? albumPath)
    {
        if (string.IsNullOrWhiteSpace(albumPath)
            || !TryGetLocalPath(item.Source, out var itemPath))
        {
            return false;
        }
        return string.Equals(
            Path.GetDirectoryName(itemPath),
            albumPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatTrackCount(int count) => count switch
    {
        1 => "1 utwór",
        _ when count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) => $"{count} utwory",
        _ => $"{count} utworów"
    };

    public void MoveLocalLibrarySelection(int direction)
    {
        var isCustomLocalOrder = !_playerViewActive
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
            && string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal);
        var isFavoriteOrder = !_playerViewActive
            && string.Equals(_currentView, "Ulubione", StringComparison.Ordinal);
        var isQueueOrder = !_playerViewActive
            && string.Equals(_currentView, "Kolejka", StringComparison.Ordinal);
        var playlistId = string.Empty;
        var isPlaylistOrder = !_playerViewActive
            && TryGetPlaylistIdFromView(_currentView, out playlistId);
        if (!isCustomLocalOrder && !isFavoriteOrder && !isQueueOrder && !isPlaylistOrder)
        {
            Announce("Ręczne przenoszenie działa w Kolejności własnej, Ulubionych, Kolejce oraz otwartej playliście");
            return;
        }
        if (!string.IsNullOrWhiteSpace(FilterBox.Text))
        {
            Announce("Wyczyść filtr klawiszem Escape przed zmianą kolejności");
            return;
        }

        var selectedRows = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .ToArray();
        var selectedIds = selectedRows.Select(row => row.ActionItem.Id).ToArray();
        if (selectedIds.Length == 0)
        {
            Announce("Wybierz plik do przeniesienia");
            return;
        }

        IList<string> storedOrder;
        PlaylistSettings? previousPlaylists = null;
        IReadOnlyList<string>? previousQueueOrder = null;
        if (isCustomLocalOrder)
        {
            EnsureLocalCustomOrder();
            storedOrder = _state.LocalMedia.CustomOrderItemIds;
        }
        else if (isFavoriteOrder)
        {
            storedOrder = EnsureFavoriteOrder(_sessions.Current);
        }
        else if (isQueueOrder)
        {
            if (selectedRows.Select(row => row.ActionItem.IsPlayNext).Distinct().Count() > 1)
            {
                Announce("Elementy zwykłej kolejki i odtwarzane jako następne przenoś osobno");
                return;
            }
            storedOrder = EnsureQueueOrder(_sessions.Current);
            previousQueueOrder = storedOrder.ToArray();
        }
        else
        {
            var playlists = new PlaylistIndex(_state.Playlists);
            var playlist = playlists.Find(playlistId)
                ?? throw new InvalidOperationException("Nie znaleziono otwartej playlisty.");
            previousPlaylists = playlists.CloneSettings();
            storedOrder = playlist.ItemIds;
        }
        var visibleRows = (isQueueOrder
            ? _unfilteredItems.Where(row =>
                row.ActionItem.IsPlayNext == selectedRows[0].ActionItem.IsPlayNext)
            : _unfilteredItems)
            .ToArray();
        var visibleIds = visibleRows
            .Select(row => row.ActionItem.Id)
            .ToArray();
        var selectedIdSet = selectedIds.ToHashSet(StringComparer.Ordinal);
        var selectedVisibleIndices = visibleIds
            .Select((itemId, index) => (itemId, index))
            .Where(entry => selectedIdSet.Contains(entry.itemId))
            .Select(entry => entry.index)
            .ToArray();
        var result = LocalLibraryManualOrder.MoveVisibleBlock(
            storedOrder,
            visibleIds,
            selectedIds,
            direction);
        if (result != ManualOrderMoveResult.Moved)
        {
            Announce(result switch
            {
                ManualOrderMoveResult.Boundary => direction < 0
                    ? "To początek tej listy"
                    : "To koniec tej listy",
                ManualOrderMoveResult.NonContiguousSelection =>
                    "Do wspólnego przeniesienia zaznacz ciągły blok elementów",
                _ => "Nie można zmienić kolejności zaznaczenia"
            });
            return;
        }
        var adjacentItem = selectedVisibleIndices.Length == 0
            ? null
            : visibleRows[direction < 0
                ? selectedVisibleIndices[0] - 1
                : selectedVisibleIndices[^1] + 1].ActionItem;

        if (isQueueOrder) _sessions.Current.SetQueueOrder(storedOrder);
        var primaryId = selectedIds[0];
        RefreshCurrentView(preferredItemId: primaryId);
        SelectMediaItems(selectedIds);
        if (isPlaylistOrder)
        {
            RecordPlaylistUndo(
                previousPlaylists!,
                direction < 0 ? "Cofnięto przeniesienie wyżej" : "Cofnięto przeniesienie niżej");
            SavePlaylistState();
        }
        else if (isQueueOrder)
        {
            _queueOrderHistory.Push(new QueueOrderUndo(
                ++_undoSequence,
                _sessions.Current.Id,
                previousQueueOrder!,
                selectedIds,
                direction < 0 ? "Cofnięto przeniesienie wyżej" : "Cofnięto przeniesienie niżej"));
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
                TrySaveLocalMediaState(true);
            else
                _store.Save(_state);
        }
        else if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(true);
        }
        else
        {
            _store.Save(_state);
        }
        var movedLabel = selectedIds.Length == 1
            ? "Przeniesiono"
            : $"Przeniesiono {FormatItemCount(selectedIds.Length)}";
        var directionLabel = direction < 0 ? "w górę, nad" : "w dół, pod";
        PrepareSelectedItemFocusContext(adjacentItem is null
            ? $"{movedLabel} {directionLabel} sąsiedni element"
            : $"{movedLabel} {directionLabel} {adjacentItem.Title}");
        RestoreMediaListFocusAfterRefresh();
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
        if (row?.PlaylistId is { } playlistId)
        {
            OpenPlaylist(playlistId, row.Item.Title);
            return;
        }
        if (row?.AlbumFolderPath is { } albumFolderPath)
        {
            OpenLocalAlbum(albumFolderPath, row.Item.Title);
            return;
        }
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
            var opensFromQueue = string.Equals(_currentView, "Kolejka", StringComparison.Ordinal);
            PreparePlaybackContextForCurrentView(session, item);
            if (session.CurrentItem.Id != item.Id || !session.IsPlaying)
            {
                session.Play(item);
                RecordPlayback(session, item);
            }
            if (opensFromQueue)
            {
                EnsureQueueOrder(session);
                if (string.Equals(session.Id, "local", StringComparison.Ordinal))
                    TrySaveLocalMediaState(false);
                else
                    _store.Save(_state);
            }
            RefreshPlaybackIndicators();
            ShowPlayerView();
            return;
        }
        NavigateTo(item.Title);
        Announce($"{item.KindLabel}: {item.Title}. {FormatItemCount(_unfilteredItems.Count)}, {FormatDurationWords(item.Duration)}");
    }

    private void PreparePlaybackContextForCurrentView(DemoMediaSession session, MediaItem selectedItem)
    {
        if (_playerViewActive
            || _currentView is BookmarkViewName or "Historia odtwarzania")
        {
            return;
        }

        var sessionIds = session.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var itemIds = _unfilteredItems
            .Where(row => row.FolderPath is null && row.AlbumFolderPath is null && row.PlaylistId is null)
            .Select(row => row.ActionItem)
            .Where(item => item.Kind is MediaItemKind.Track or MediaItemKind.Station)
            .Select(item => item.Id)
            .Where(sessionIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!itemIds.Contains(selectedItem.Id, StringComparer.Ordinal)) return;

        session.SetPlaybackContext(
            itemIds,
            string.Equals(_currentView, "Kolejka", StringComparison.Ordinal));
        var navigation = GetSessionNavigationState(session.Id);
        navigation.PlaybackContextView = CurrentViewDisplayName();
        navigation.PlaybackContextItemIds = itemIds;
    }

    private void OpenPlaylist(string playlistId, string playlistName)
    {
        NavigateTo(PlaylistContentsView(playlistId));
        PrepareViewFocusContext($"Playlista, {playlistName}");
        RestoreMediaListFocusAfterRefresh();
    }

    private void OpenLocalAlbum(string folderPath, string albumTitle)
    {
        _currentLocalAlbumPath = folderPath;
        _currentLocalAlbumTitle = albumTitle;
        NavigateTo(LocalAlbumContentsViewName);
        PrepareViewFocusContext($"Album, {albumTitle}");
        RestoreMediaListFocusAfterRefresh();
    }

    private LocalAlbumGroup? FindRelatedLocalAlbum(MediaItem? item)
    {
        if (item is null || !string.Equals(ActionSession.Id, "local", StringComparison.Ordinal))
        {
            return null;
        }
        return InferLocalAlbums().FirstOrDefault(album =>
            album.Tracks.Any(track => string.Equals(track.Id, item.Id, StringComparison.Ordinal)));
    }

    private void GoToRelatedAlbum()
    {
        var album = FindRelatedLocalAlbum(ActionItem);
        if (album is null)
        {
            Announce("Dla tego elementu nie znaleziono powiązanego albumu");
            return;
        }
        OpenLocalAlbum(album.FolderPath, album.Title);
    }

    private void GoToRelatedArtist()
    {
        var album = FindRelatedLocalAlbum(ActionItem);
        var artistFolder = album is null || string.IsNullOrWhiteSpace(album.Artist)
            ? null
            : Directory.GetParent(album.FolderPath)?.FullName;
        if (album is null || string.IsNullOrWhiteSpace(artistFolder))
        {
            Announce("Dla tego elementu nie znaleziono powiązanego folderu wykonawcy");
            return;
        }

        _state.LocalMedia.CurrentFolderPath = artistFolder;
        NavigateTo(FolderViewName);
        SelectMediaItem(FolderRowId(album.FolderPath));
        PrepareViewFocusContext($"Wykonawca, {album.Artist}");
        TrySaveLocalMediaState(false);
        RestoreMediaListFocusAfterRefresh();
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

    private void NavigateToParentLevel()
    {
        if (_playerViewActive)
        {
            ReturnFromPlayerToList();
            return;
        }
        if (string.Equals(_currentView, FolderViewName, StringComparison.Ordinal))
        {
            NavigateToParentFolder();
            return;
        }
        if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            LeaveBookmarkView();
            return;
        }
        if (string.Equals(_currentView, LocalAlbumContentsViewName, StringComparison.Ordinal)
            || TryGetPlaylistIdFromView(_currentView, out _)
            || !IsTopLevelBrowserView(_currentView))
        {
            NavigateBack();
            return;
        }
        Announce("To najwyższy poziom tego widoku");
    }

    private static bool IsTopLevelBrowserView(string viewName) => viewName is
        DefaultBrowserView
        or FolderViewName
        or AllLocalFilesViewName
        or CustomLocalOrderViewName
        or "Biblioteka"
        or "Ulubione"
        or "Playlisty"
        or "Kolejka"
        or "Albumy"
        or "Historia odtwarzania"
        or BookmarkViewName
        or "Radio i rekomendacje"
        or "Miksy"
        or "Wyjścia i urządzenia"
        or "Pobrane";

    private void NavigateToParentFolder()
    {
        var currentPath = _state.LocalMedia.CurrentFolderPath;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            Announce("To jest lista Folderów Biblioteki");
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
            ? "Foldery Biblioteki"
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
        if (string.Equals(_currentView, "Historia odtwarzania", StringComparison.Ordinal))
        {
            RemoveSelectedPlaybackHistoryEntries();
            return;
        }
        if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
        {
            RemoveSelectedBookmarks();
            return;
        }
        if (string.Equals(_currentView, "Playlisty", StringComparison.Ordinal)
            && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null)
        {
            DeleteSelectedPlaylist();
            return;
        }
        if (TryGetPlaylistIdFromView(_currentView, out var playlistId))
        {
            RemoveSelectedPlaylistItems(playlistId);
            return;
        }

        var selectedRows = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .ToArray();
        if (selectedRows.Any(row => row.AlbumFolderPath is not null))
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Album jest widokiem folderu. Otwórz go Enterem, aby usuwać pojedyncze utwory z Biblioteki"),
                DispatcherPriority.ContextIdle);
            return;
        }
        if (string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
            && selectedRows.Any(row => row.FolderPath is not null))
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Delete nie usuwa folderu. Enter otwiera folder; Folderami Biblioteki zarządza Ctrl+F5"),
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

        if (_currentView is not ("Ulubione" or "Biblioteka" or "Kolejka" or FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName or LocalAlbumContentsViewName))
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Usuwanie jest dostępne w widokach Foldery Biblioteki, Wszystkie pliki, Kolejność własna, utwory albumu, Ulubione, Biblioteka i Kolejka"),
                DispatcherPriority.ContextIdle);
            return;
        }

        if (_currentView is FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName or LocalAlbumContentsViewName)
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
        var orderSnapshot = CaptureMembershipOrderForView(
            _sessions.Current,
            _currentView,
            items);
        AnchorMediaListFocus();

        foreach (var item in items)
        {
            if (_currentView == "Ulubione")
            {
                item.IsFavorite = false;
            }
            else if (_currentView is "Biblioteka" or FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName or LocalAlbumContentsViewName)
            {
                item.IsInLibrary = false;
                if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
                {
                    item.IsFavorite = false;
                }
            }
            else if (_currentView == "Kolejka")
            {
                item.IsInQueue = false;
                item.IsPlayNext = false;
            }
        }
        if (string.Equals(_currentView, "Kolejka", StringComparison.Ordinal))
        {
            EnsureQueueOrder(_sessions.Current);
        }
        var undoAnnouncement = items.Length == 1
            ? _currentView switch
            {
                "Ulubione" => $"Przywrócono w ulubionych: {items[0].Title}",
                "Biblioteka" => $"Przywrócono w bibliotece: {items[0].Title}",
                FolderViewName => $"Przywrócono w bibliotece: {items[0].Title}",
                AllLocalFilesViewName => $"Przywrócono w bibliotece: {items[0].Title}",
                CustomLocalOrderViewName => $"Przywrócono w bibliotece: {items[0].Title}",
                LocalAlbumContentsViewName => $"Przywrócono w bibliotece: {items[0].Title}",
                "Kolejka" => $"Przywrócono w kolejce: {items[0].Title}",
                _ => throw new InvalidOperationException($"Nieobsługiwany widok usuwania: {_currentView}")
            }
            : $"Przywrócono w widoku {_currentView}: {FormatItemCount(items.Length)}";
        _membershipHistory.RecordBatch(
            _sessions.Current.Id,
            previousMemberships,
            undoAnnouncement,
            ++_undoSequence,
            orderSnapshot);
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            if (_currentView is FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName or LocalAlbumContentsViewName)
            {
                AddLocalExclusions(items);
                RefreshLocalSessionItems();
            }
            TrySaveLocalMediaState(false);
        }
        else if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            CaptureRadioState();
            _store.Save(_state);
        }
        RefreshCurrentView(previousIndex);
        RestoreMediaListFocusAfterRefresh();
        var removedLabel = items.Length == 1 ? items[0].Title : FormatItemCount(items.Length);
        var announcement = _currentView is FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName or LocalAlbumContentsViewName
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
        var playlistCandidate = _playlistHistory.TryPeek(out var playlistUndo) ? playlistUndo : null;
        var queueOrderCandidate = _queueOrderHistory.TryPeek(out var queueOrderUndo) ? queueOrderUndo : null;
        if (queueOrderCandidate is not null
            && (membershipCandidate is null || queueOrderCandidate.Sequence > membershipCandidate.Sequence)
            && (catalogCandidate is null || queueOrderCandidate.Sequence > catalogCandidate.Sequence)
            && (playlistCandidate is null || queueOrderCandidate.Sequence > playlistCandidate.Sequence))
        {
            UndoQueueOrderChange(_queueOrderHistory.Pop());
            return;
        }
        if (playlistCandidate is not null
            && (membershipCandidate is null || playlistCandidate.Sequence > membershipCandidate.Sequence)
            && (catalogCandidate is null || playlistCandidate.Sequence > catalogCandidate.Sequence)
            && (queueOrderCandidate is null || playlistCandidate.Sequence > queueOrderCandidate.Sequence))
        {
            UndoPlaylistChange(_playlistHistory.Pop());
            return;
        }
        if (catalogCandidate is not null
            && (membershipCandidate is null || catalogCandidate.Sequence > membershipCandidate.Sequence)
            && (playlistCandidate is null || catalogCandidate.Sequence > playlistCandidate.Sequence)
            && (queueOrderCandidate is null || catalogCandidate.Sequence > queueOrderCandidate.Sequence))
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

        RestoreMembershipOrder(undo);

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
        else if (string.Equals(undo.SessionId, "radio", StringComparison.Ordinal))
        {
            CaptureRadioState();
            _store.Save(_state);
        }
        RestoreMediaListFocusAfterRefresh();

        Dispatcher.BeginInvoke(
            () => Announce(undo.Announcement),
            DispatcherPriority.ContextIdle);
    }

    private void UndoPlaylistChange(PlaylistStateUndo undo)
    {
        _state.Playlists = new PlaylistIndex(undo.PreviousState).CloneSettings();
        EnsureCurrentPlaylistStillExists();
        RefreshCurrentView();
        SavePlaylistState();
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(() => Announce(undo.Announcement), DispatcherPriority.ContextIdle);
    }

    private void UndoQueueOrderChange(QueueOrderUndo undo)
    {
        var restored = undo.PreviousOrder.ToList();
        _state.CollectionOrders.QueueItemIdsBySession[undo.SessionId] = restored;
        _sessions.FindSession(undo.SessionId)?.SetQueueOrder(restored);
        if (string.Equals(_sessions.Current.Id, undo.SessionId, StringComparison.OrdinalIgnoreCase))
        {
            RefreshCurrentView();
            SelectMediaItems(undo.SelectedItemIds);
        }
        if (string.Equals(undo.SessionId, "local", StringComparison.OrdinalIgnoreCase))
            TrySaveLocalMediaState(false);
        else
            _store.Save(_state);
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(() => Announce(undo.Announcement), DispatcherPriority.ContextIdle);
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
        LocalLibraryManualOrder.RestorePositions(
            _state.LocalMedia.CustomOrderItemIds,
            undo.CustomOrderPositions);
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

    private MediaMembershipOrderSnapshot? CaptureMembershipOrderForCommand(
        DemoMediaSession session,
        string commandId,
        IEnumerable<MediaItem> items) => commandId switch
        {
            CommandIds.ToggleFavorite => CaptureFavoriteOrder(session, items),
            CommandIds.AddQueue or CommandIds.TogglePlayNext => CaptureQueueOrder(session, items),
            CommandIds.ToggleLibrary when string.Equals(session.Id, "local", StringComparison.Ordinal) =>
                CaptureLocalCustomOrder(items),
            _ => null
        };

    private MediaMembershipOrderSnapshot? CaptureMembershipOrderForView(
        DemoMediaSession session,
        string viewName,
        IEnumerable<MediaItem> items) => viewName switch
        {
            "Ulubione" => CaptureFavoriteOrder(session, items),
            "Kolejka" => CaptureQueueOrder(session, items),
            "Biblioteka" or FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName or LocalAlbumContentsViewName
                when string.Equals(session.Id, "local", StringComparison.Ordinal) =>
                CaptureLocalCustomOrder(items),
            _ => null
        };

    private MediaMembershipOrderSnapshot CaptureFavoriteOrder(
        DemoMediaSession session,
        IEnumerable<MediaItem> items)
    {
        var order = EnsureFavoriteOrder(session);
        return CreateMembershipOrderSnapshot("favorites", order, items);
    }

    private MediaMembershipOrderSnapshot CaptureLocalCustomOrder(IEnumerable<MediaItem> items)
    {
        EnsureLocalCustomOrder();
        return CreateMembershipOrderSnapshot(
            "local-custom",
            _state.LocalMedia.CustomOrderItemIds,
            items);
    }

    private MediaMembershipOrderSnapshot CaptureQueueOrder(
        DemoMediaSession session,
        IEnumerable<MediaItem> items)
    {
        var order = EnsureQueueOrder(session);
        return CreateMembershipOrderSnapshot("queue", order, items);
    }

    private static MediaMembershipOrderSnapshot CreateMembershipOrderSnapshot(
        string collectionId,
        IReadOnlyList<string> order,
        IEnumerable<MediaItem> items)
    {
        var positions = LocalLibraryManualOrder.CapturePositions(
                order,
                items.Select(item => item.Id))
            .Select(entry => new MediaMembershipOrderPosition(entry.ItemId, entry.Index))
            .ToArray();
        return new MediaMembershipOrderSnapshot(collectionId, positions);
    }

    private void RestoreMembershipOrder(MediaMembershipUndo undo)
    {
        if (undo.OrderSnapshot is not { } snapshot) return;
        var positions = snapshot.Positions
            .Select(entry => new ManualOrderPosition(entry.ItemId, entry.Index));
        if (string.Equals(snapshot.CollectionId, "favorites", StringComparison.Ordinal))
        {
            if (!_state.CollectionOrders.FavoriteItemIdsBySession.TryGetValue(
                    undo.SessionId,
                    out var order))
            {
                order = [];
                _state.CollectionOrders.FavoriteItemIdsBySession[undo.SessionId] = order;
            }
            LocalLibraryManualOrder.RestorePositions(order, positions);
            return;
        }
        if (string.Equals(snapshot.CollectionId, "local-custom", StringComparison.Ordinal))
        {
            LocalLibraryManualOrder.RestorePositions(
                _state.LocalMedia.CustomOrderItemIds,
                positions);
            return;
        }
        if (string.Equals(snapshot.CollectionId, "queue", StringComparison.Ordinal))
        {
            if (!_state.CollectionOrders.QueueItemIdsBySession.TryGetValue(
                    undo.SessionId,
                    out var order))
            {
                order = [];
                _state.CollectionOrders.QueueItemIdsBySession[undo.SessionId] = order;
            }
            LocalLibraryManualOrder.RestorePositions(order, positions);
            _sessions.FindSession(undo.SessionId)?.SetQueueOrder(order);
        }
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
        ClearDisabledLocalResumePositions();
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

    private void RemoveSelectedPlaybackHistoryEntries()
    {
        var itemIds = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .Select(row => row.Item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (itemIds.Length == 0)
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Brak wpisu Historii do usunięcia"),
                DispatcherPriority.ContextIdle);
            return;
        }

        var previousIndex = MediaList.SelectedIndex;
        AnchorMediaListFocus();
        _playbackHistory.Remove(_sessions.Current.Id, itemIds);
        _playbackHistoryCursors.Remove(_sessions.Current.Id);

        var saved = true;
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            saved = TrySaveLocalMediaState(true);
        }
        else
        {
            try
            {
                _store.Save(_state);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                saved = false;
                DiagnosticLog.Error("storage", "Nie udało się zapisać Historii odtwarzania.", exception);
            }
        }

        RefreshCurrentView(previousIndex);
        RestoreMediaListFocusAfterRefresh();
        var message = itemIds.Length == 1
            ? "Usunięto wpis z Historii odtwarzania. Plik i Biblioteka pozostały bez zmian"
            : $"Usunięto wpisy z Historii odtwarzania: {itemIds.Length}. Pliki i Biblioteka pozostały bez zmian";
        Dispatcher.BeginInvoke(
            () =>
            {
                if (saved) Announce(message);
                else AnnounceEssential($"{message}. Nie udało się trwale zapisać zmiany");
            },
            DispatcherPriority.ContextIdle);
    }

    public void ShowLocalSourceManager()
    {
        TrySaveLocalMediaState(false);
        var dialog = new LocalSourcesWindow(
            BuildLocalSourceStatuses,
            AddManagedLocalSourceAsync,
            RefreshManagedLocalSourcesAsync,
            DetachManagedLocalSource,
            SetManagedSourceResumePositionMode,
            BuildUnavailableLocalItems,
            ForgetUnavailableLocalItems,
            ExportFullBackup)
        {
            Owner = this
        };
        dialog.ShowDialog();
        RestoreMediaListFocusAfterRefresh();
    }

    public void RenameLibraryItem()
    {
        if (string.Equals(ActionSession.Id, "radio", StringComparison.Ordinal)
            && ActionItem?.Kind == MediaItemKind.Station)
        {
            EditRadioStation();
            return;
        }
        if (!TryGetSingleLocalRenameItem(requireExistingFile: false, out var item, out var path)) return;
        var dialog = new RenameLocalItemWindow(item.Title, renameOnDisk: false) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        var newTitle = dialog.NewName;
        if (string.Equals(item.Title, newTitle, StringComparison.CurrentCulture))
        {
            RestoreMediaListFocusAfterRefresh();
            Announce("Nazwa w Bibliotece nie została zmieniona");
            return;
        }

        var fileTitle = Path.GetFileNameWithoutExtension(path);
        UpdateLocalItemTitle(
            item,
            newTitle,
            !string.Equals(newTitle, fileTitle, StringComparison.CurrentCulture));
        RefreshCurrentView(preferredItemId: item.Id);
        TrySaveLocalMediaState(true);
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(
            () => Announce($"Zmieniono nazwę w Bibliotece: {newTitle}"),
            DispatcherPriority.ContextIdle);
    }

    private void AddRadioStation()
    {
        var dialog = new RadioStationWindow { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        var duplicate = _radioItems.FirstOrDefault(item => string.Equals(
            item.Source,
            dialog.StreamUrl,
            StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            duplicate.Title = dialog.StationName;
            duplicate.HasCustomTitle = true;
            duplicate.IsInLibrary = true;
            RefreshCurrentView(preferredItemId: duplicate.Id);
            CaptureRadioState();
            _store.Save(_state);
            RestoreMediaListFocusAfterRefresh();
            Announce($"Zaktualizowano stację: {duplicate.Title}");
            return;
        }

        var item = new MediaItem
        {
            Id = $"radio:custom:{Guid.NewGuid():N}",
            Title = dialog.StationName,
            HasCustomTitle = true,
            Kind = MediaItemKind.Station,
            Source = dialog.StreamUrl,
            PublicUri = dialog.StreamUrl,
            IsInLibrary = true,
            IsAvailable = true
        };
        _radioItems.Add(item);
        var radio = _sessions.FindSession("radio");
        radio?.AddItems([item]);
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            CaptureCurrentSessionNavigationState();
            _sessions.SelectSession("radio");
        }
        _currentView = "Biblioteka";
        GetSessionNavigationState("radio").CurrentView = _currentView;
        RefreshCurrentView(preferredItemId: item.Id);
        CaptureRadioState();
        _store.Save(_state);
        RestoreMediaListFocusAfterRefresh();
        Dispatcher.BeginInvoke(
            () => Announce($"Dodano stację do Biblioteki: {item.Title}"),
            DispatcherPriority.ContextIdle);
    }

    private void EditRadioStation()
    {
        var item = ActionItem;
        if (item?.Kind != MediaItemKind.Station || string.IsNullOrWhiteSpace(item.Source))
        {
            Announce("Wybierz jedną stację radiową");
            return;
        }
        var dialog = new RadioStationWindow(item.Title, item.Source) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            RestoreItemActionFocus();
            return;
        }

        var streamChanged = !string.Equals(item.Source, dialog.StreamUrl, StringComparison.OrdinalIgnoreCase);
        if (streamChanged && string.Equals(_radioOutput.LoadedItemId, item.Id, StringComparison.Ordinal))
        {
            _sessions.FindSession("radio")?.StopPlayback();
        }
        item.Title = dialog.StationName;
        item.HasCustomTitle = true;
        item.Source = dialog.StreamUrl;
        item.PublicUri = dialog.StreamUrl;
        item.IsInLibrary = true;
        RefreshCurrentView(preferredItemId: item.Id);
        CaptureRadioState();
        _store.Save(_state);
        RestoreItemActionFocus();
        Dispatcher.BeginInvoke(
            () => Announce($"Zapisano nazwę i adres stacji: {item.Title}"),
            DispatcherPriority.ContextIdle);
    }

    private void ToggleRadioRecording()
    {
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            || !_sessions.Current.HasCurrentItem)
        {
            Announce("Nagrywanie jest dostępne podczas odtwarzania radia internetowego");
            return;
        }
        try
        {
            if (_radioOutput.IsRecording)
            {
                var savedPath = _radioOutput.StopRecording();
                AnnounceEssential(savedPath is null
                    ? "Nagrywanie nie było uruchomione"
                    : $"Zakończono nagrywanie: {Path.GetFileName(savedPath)}");
                return;
            }
            var folder = ResolveRadioRecordingsFolder();
            var path = _radioOutput.StartRecording(folder);
            AnnounceEssential($"Rozpoczęto nagrywanie: {Path.GetFileName(path)}");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or TimeoutException
            or System.Runtime.InteropServices.COMException)
        {
            DiagnosticLog.Error("radio-recording", "Nie udało się zmienić stanu nagrywania.", exception);
            AnnounceEssential($"Nie można nagrywać: {exception.Message}");
        }
    }

    private void RadioOutput_RecordingFailed(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        UpdatePlayerView();
        AnnounceEssential("Nagrywanie MP3 zostało przerwane. Nie zapisano uszkodzonego pliku");
    }

    private string ResolveRadioRecordingsFolder()
    {
        if (!string.IsNullOrWhiteSpace(_state.Radio.RecordingsFolder))
        {
            return _state.Radio.RecordingsFolder;
        }
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return Path.Combine(music, "AMC — Nagrania radia");
    }

    public void RenameLocalFile()
    {
        if (!TryGetSingleLocalRenameItem(requireExistingFile: true, out var item, out var path)) return;
        var dialog = new RenameLocalItemWindow(
            Path.GetFileNameWithoutExtension(path),
            renameOnDisk: true)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        if (!LocalFileRenamePolicy.TryBuildTargetPath(path, dialog.NewName, out var targetPath, out var error))
        {
            RestoreMediaListFocusAfterRefresh();
            AnnounceEssential($"Nie zmieniono nazwy pliku: {error}");
            return;
        }

        var localSession = _sessions.FindSession("local");
        var stoppedLoadedFile = string.Equals(_localOutput.LoadedItemId, item.Id, StringComparison.Ordinal);
        if (stoppedLoadedFile && localSession is not null)
        {
            localSession.StopPlayback();
        }

        try
        {
            File.Move(path, targetPath);
            ApplyRenamedLocalPath(path, targetPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            RestoreMediaListFocusAfterRefresh();
            AnnounceEssential($"Nie można zmienić nazwy pliku: {exception.Message}");
            return;
        }

        RefreshCurrentView(preferredItemId: item.Id);
        UpdatePlaybackStatusBar();
        TrySaveLocalMediaState(true);
        RestoreMediaListFocusAfterRefresh();
        var stoppedMessage = stoppedLoadedFile ? ". Odtwarzanie zatrzymano, a pozycję zapamiętano" : string.Empty;
        Dispatcher.BeginInvoke(
            () => AnnounceEssential($"Zmieniono nazwę pliku na dysku: {Path.GetFileName(targetPath)}{stoppedMessage}"),
            DispatcherPriority.ContextIdle);
    }

    private bool TryGetSingleLocalRenameItem(
        bool requireExistingFile,
        out MediaItem item,
        out string path)
    {
        item = null!;
        path = string.Empty;
        if (_playerViewActive
            || !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
            || SelectedBookmark is not null)
        {
            Announce("Zmiana nazwy jest dostępna na listach Plików lokalnych");
            return false;
        }

        var items = ActionItems;
        if (items.Count != 1)
        {
            Announce("Do zmiany nazwy wybierz jeden plik lokalny");
            return false;
        }

        item = items[0];
        if (item.Kind != MediaItemKind.Track || !TryGetLocalPath(item.Source, out path))
        {
            Announce("Wybrany element nie jest plikiem lokalnym");
            return false;
        }
        if (requireExistingFile && !File.Exists(path))
        {
            Announce("Nie można zmienić nazwy: plik jest obecnie niedostępny");
            return false;
        }
        return true;
    }

    private void UpdateLocalItemTitle(MediaItem item, string title, bool hasCustomTitle)
    {
        item.Title = title;
        item.HasCustomTitle = hasCustomTitle;
        foreach (var bookmark in _state.Bookmarks.Entries.Where(bookmark =>
                     string.Equals(bookmark.SessionId, "local", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(bookmark.ItemId, item.Id, StringComparison.Ordinal)))
        {
            bookmark.ItemTitle = title;
        }
    }

    private IReadOnlyList<LocalFolderSourceStatus> BuildLocalSourceStatuses()
    {
        CaptureLocalMediaState();
        return LocalFolderSourcePolicy.BuildStatuses(
                _state.LocalMedia.FolderSources,
                _state.LocalMedia.Items,
                _state.LocalMedia.ExcludedPaths)
            .OrderBy(status => status.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(status => status.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<UnavailableLocalItemRow> BuildUnavailableLocalItems(string sourceId)
    {
        CaptureLocalMediaState();
        return UnavailableLocalItemPolicy.GetForFolder(_state, sourceId)
            .Select(item => new UnavailableLocalItemRow(item.Id, item.Title, item.Path))
            .ToArray();
    }

    private LocalSourceActionResult ForgetUnavailableLocalItems(
        string sourceId,
        IReadOnlyCollection<string> itemIds)
    {
        CaptureLocalMediaState();
        var removed = UnavailableLocalItemPolicy.Forget(_state, sourceId, itemIds);
        if (removed.Count == 0)
        {
            return new("Nie znaleziono zaznaczonych niedostępnych rekordów. Biblioteka nie została zmieniona.", sourceId);
        }

        var removedIds = removed.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        _localItems.RemoveAll(item => removedIds.Contains(item.Id));
        _bookmarkIndex = new BookmarkIndex(_state.Bookmarks);
        _playbackHistory = new PlaybackHistory(_state.PlaybackHistory);
        _playbackHistoryCursors.Remove("local");
        TrySaveLocalMediaState(true);

        var description = removed.Count == 1
            ? $"Zapomniano w AMC: {removed[0].Title}."
            : $"Zapomniano w AMC: {removed.Count} plików.";
        return new($"{description} Żaden plik na dysku nie został zmieniony.", sourceId);
    }

    private async Task<LocalSourceActionResult> AddManagedLocalSourceAsync(string path)
    {
        if (!TryRegisterLocalFolderSource(path, out var source, out var added, out var message))
        {
            return new(message);
        }

        TrySaveLocalMediaState(true);
        await SynchronizeLocalSourcesAsync(announceResult: false, [source.Id]);
        return new(
            added
                ? $"{message}. Folder został zsynchronizowany z Biblioteką."
                : $"{message}. Folder został ponownie przeskanowany.",
            source.Id);
    }

    private async Task<LocalSourceActionResult> RefreshManagedLocalSourcesAsync(
        IReadOnlyCollection<string> sourceIds)
    {
        await SynchronizeLocalSourcesAsync(announceResult: false, sourceIds);
        return new(sourceIds.Count == 0
            ? "Odświeżono wszystkie Foldery Biblioteki. Niedostępne foldery nie spowodowały usunięcia zapisanych rekordów."
            : "Odświeżono wybrany Folder Biblioteki. Niedostępność folderu nie powoduje usunięcia zapisanych rekordów.",
            sourceIds.Count == 1 ? sourceIds.First() : null);
    }

    private LocalSourceActionResult DetachManagedLocalSource(string sourceId)
    {
        var source = _state.LocalMedia.FolderSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, sourceId, StringComparison.Ordinal));
        if (source is null) return new("Folder nie należy już do Biblioteki.");

        if (!string.IsNullOrWhiteSpace(_state.LocalMedia.CurrentFolderPath)
            && LocalFolderSourcePolicy.IsSameOrDescendant(_state.LocalMedia.CurrentFolderPath, source.Path))
        {
            _state.LocalMedia.CurrentFolderPath = null;
        }
        LocalFolderSourcePolicy.DetachSource(_state.LocalMedia.FolderSources, sourceId);
        ConfigureLocalSourceWatchers();
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) && !_playerViewActive)
        {
            RefreshCurrentView();
        }
        TrySaveLocalMediaState(true);
        return new($"Odłączono folder „{source.DisplayName}”. Pliki na dysku i wszystkie zapisane dane AMC pozostały bez zmian.");
    }

    private LocalSourceActionResult SetManagedSourceResumePositionMode(
        string sourceId,
        ResumePositionMode mode)
    {
        var source = _state.LocalMedia.FolderSources.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, sourceId, StringComparison.Ordinal));
        if (source is null) return new("Folder nie należy już do Biblioteki.");

        source.ResumePositionMode = mode;
        ClearDisabledLocalResumePositions();

        TrySaveLocalMediaState(false);
        var description = mode switch
        {
            ResumePositionMode.Remember => "Pamiętaj pozycję odtwarzania",
            ResumePositionMode.StartFromBeginning => "Zawsze od początku",
            _ => "Zgodnie z ustawieniem globalnym"
        };
        return new($"Folder „{source.DisplayName}”: {description}.", source.Id);
    }

    private void ClearDisabledLocalResumePositions()
    {
        var local = _sessions?.FindSession("local");
        foreach (var saved in _state.LocalMedia.Items.Where(saved =>
                     !ShouldRememberLocalPosition(saved.Path)))
        {
            saved.ResumePositionTicks = 0;
            local?.ClearRememberedPosition(saved.Id);
        }
    }

    private void ExportFullBackup(string path)
    {
        CaptureCurrentSessionNavigationState();
        CaptureLocalMediaState();
        _store.ExportFullBackup(path, _state);
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
        EnsureLocalCustomOrder();
        var customOrderPositions = LocalLibraryManualOrder.CapturePositions(
            _state.LocalMedia.CustomOrderItemIds,
            catalogEntries.Select(entry => entry.Item.Id));
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
        if (!recordUndo)
        {
            var removedIds = catalogEntries.Select(entry => entry.Item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var playlist in _state.Playlists.Entries)
            {
                playlist.ItemIds.RemoveAll(removedIds.Contains);
            }
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
                detachedSession,
                customOrderPositions));
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
        var nextItemNote = announceNextItem || currentRemoved
            ? detachedSession is null
                ? session.HasCurrentItem
                    ? $". Następny element z bieżącego widoku: {session.CurrentItem.Title}"
                    : ". Brak następnego elementu w bieżącym widoku"
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
        var orderSnapshot = CaptureLocalCustomOrder([item]);
        var wasPlaying = session.IsPlaying;
        item.IsInLibrary = false;
        AddLocalExclusions([item]);
        _membershipHistory.Record(
            "local",
            item,
            previous,
            $"Przywrócono w bibliotece: {item.Title}",
            ++_undoSequence,
            orderSnapshot);
        var replacementResult = RefreshLocalSessionItems();
        TrySaveLocalMediaState(false);
        if (session.HasCurrentItem)
        {
            UpdatePlayerView(true);
            UpdatePlaybackStatusBar();
            UpdateWindowTitle();
            Announce($"Wykluczono z Biblioteki: {item.Title}. Następny element z bieżącego widoku: {session.CurrentItem.Title}"
                + (wasPlaying ? ". Odtwarzanie zatrzymano" : string.Empty));
        }
        else
        {
            ReturnFromPlayerToList();
            Announce(replacementResult.CurrentItemRemoved && session.HasItems
                ? $"Wykluczono z Biblioteki: {item.Title}. Brak następnego elementu w bieżącym widoku"
                : $"Wykluczono z Biblioteki: {item.Title}. Biblioteka jest pusta");
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
            ClearFilterForNavigation(navigation, _currentView, viewName);
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
        var previousView = _currentView;
        _currentView = history.Back.Pop();
        ClearFilterForNavigation(navigation, previousView, _currentView);
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
        var previousView = _currentView;
        _currentView = history.Forward.Pop();
        ClearFilterForNavigation(navigation, previousView, _currentView);
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
        var viewName = CurrentViewDisplayName();
        var context = string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal)
            ? _sessions.Current.DisplayName
            : $"{viewName}, {_sessions.Current.DisplayName}";
        PrepareViewFocusContext(HistoryMessagesEnabled ? $"{direction}, {context}" : context);
    }

    private string QueueFocusContext()
    {
        var items = _sessions.Current.Items
            .Where(item => item.IsInQueue || item.IsPlayNext)
            .ToArray();
        var playNextCount = items.Count(item => item.IsPlayNext);
        var regularCount = items.Length - playNextCount;
        return $"Kolejka, jako następne {playNextCount}, pozostałe {regularCount}";
    }

    private string FormatListItem(MediaItem item)
    {
        var homogeneousView = _currentView is "Albumy" or "Playlisty";
        var label = FormatItem(item, !homogeneousView);
        if (string.Equals(_currentView, "Kolejka", StringComparison.Ordinal) && item.IsPlayNext)
        {
            label = $"Następny, {label}";
        }
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
            if (row.Bookmark is not null || row.PlaylistId is not null) continue;
            row.UpdateLabel(FormatListItem(row.Item));
        }
    }

    private void UpdateWindowTitle()
    {
        var session = _sessions.Current;
        var area = _playerViewActive ? "Odtwarzacz" : CurrentViewDisplayName();
        Title = !_playerViewActive && string.Equals(area, DefaultBrowserView, StringComparison.Ordinal)
            ? $"{session.CurrentItem.Title} — {session.DisplayName} — AMC {AppDisplayVersion}"
            : $"{session.CurrentItem.Title} — {area} — {session.DisplayName} — AMC {AppDisplayVersion}";
        AutomationProperties.SetName(this, Title);
    }

    private string CurrentViewDisplayName() =>
        string.Equals(_currentView, LocalAlbumContentsViewName, StringComparison.Ordinal)
            ? string.IsNullOrWhiteSpace(_currentLocalAlbumTitle)
                ? "Album"
                : $"Album — {_currentLocalAlbumTitle}"
            : TryGetPlaylistIdFromView(_currentView, out var playlistId)
                ? new PlaylistIndex(_state.Playlists).Find(playlistId) is { } playlist
                    ? $"Playlista — {playlist.Name}"
                    : "Playlista"
            : _currentView;

    private static string PlaylistContentsView(string playlistId) =>
        $"{PlaylistContentsViewPrefix}{playlistId}";

    private static bool TryGetPlaylistIdFromView(string viewName, out string playlistId)
    {
        if (viewName.StartsWith(PlaylistContentsViewPrefix, StringComparison.Ordinal)
            && viewName.Length > PlaylistContentsViewPrefix.Length)
        {
            playlistId = viewName[PlaylistContentsViewPrefix.Length..];
            return true;
        }
        playlistId = string.Empty;
        return false;
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
        if (_keyboardHelpActive
            || message != WmKeyDown
            || Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            return IntPtr.Zero;
        }

        var modifiers = Keyboard.Modifiers;
        Action? action = (modifiers, wParam.ToInt32()) switch
        {
            (ModifierKeys.Control, VirtualKeyZ) => UndoLastMembershipChange,
            (ModifierKeys.Control | ModifierKeys.Shift, VirtualKeyC)
                when _playerViewActive || MediaList.IsKeyboardFocusWithin => CopyActionItemLocation,
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
        if (windowKey == Key.F1 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ToggleKeyboardHelp();
            e.Handled = true;
            return;
        }

        if (_keyboardHelpActive)
        {
            if (IsModifierKey(windowKey))
            {
                e.Handled = true;
                return;
            }
            if (windowKey == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
            {
                ToggleKeyboardHelp();
                e.Handled = true;
                return;
            }

            AnnounceEssential(DescribeKeyboardShortcut(e));
            e.Handled = true;
            return;
        }

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

        if (Keyboard.Modifiers == ModifierKeys.None
            && e.Key == Key.Back
            && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            if (MainMenu.IsKeyboardFocusWithin || Keyboard.FocusedElement is MenuItem)
            {
                return;
            }
            e.Handled = true;
            NavigateToParentLevel();
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
        else if (e.Key == Key.F1 && modifiers == ModifierKeys.None)
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
            if ((MediaList.SelectedItem as MediaItemRow)?.AlbumFolderPath is not null)
                Announce("Album jest widokiem folderu. Otwórz go Enterem, aby przenosić pojedyncze pliki do Kosza");
            else if (string.Equals(_currentView, BookmarkViewName, StringComparison.Ordinal))
                Announce("Shift+Delete nie usuwa pliku z listy zakładek. Delete usuwa samą zakładkę");
            else
                MoveSelectedLocalFilesToRecycleBin();
            e.Handled = true;
        }
        else if (modifiers == ModifierKeys.None && e.Key == Key.Delete)
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
            var selectedRow = MediaList.SelectedItem as MediaItemRow;
            if ((selectedRow?.AlbumFolderPath is not null || selectedRow?.PlaylistId is not null)
                && modifiers != ModifierKeys.None)
            {
                Announce(selectedRow?.PlaylistId is not null
                    ? "Playlista otwiera się zwykłym Enterem. Działania na utworach są dostępne po jej otwarciu"
                    : "Album otwiera zwykły Enter. Działania kolejki, Ulubionych i Biblioteki są dostępne po otwarciu jego utworów");
                e.Handled = true;
                return;
            }
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

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!string.Equals(e.Text, "?", StringComparison.Ordinal)
            || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase
            || Keyboard.FocusedElement is PasswordBox)
        {
            return;
        }

        ShowHelp();
        e.Handled = true;
    }

    private string DescribeKeyboardShortcut(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var chord = WindowsKeyMap.FromKeyEvent(e);
        var spokenShortcut = FormatShortcutForSpeech(chord);
        var context = KeyboardHelpContext();

        if (string.Equals(
                chord.Canonical,
                KeyChord.Parse(_state.Settings.PrefixChord).Canonical,
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{spokenShortcut}: włącz warstwę prefiksową. W trybie Pomocy warstwa nie zostanie uruchomiona. Kontekst: {context}";
        }

        if (TryDescribeDirectShortcut(key, Keyboard.Modifiers, out var directDescription))
        {
            return $"{spokenShortcut}: {directDescription}. Kontekst: {context}";
        }

        if (TryResolveKeyboardHelpCommand(key, Keyboard.Modifiers, out var commandId))
        {
            var displayName = CommandPaletteSearch
                .CreateEntries(ActiveKeyboardProfile(), _state.Settings, includeCommandPalette: true)
                .FirstOrDefault(entry => string.Equals(entry.CommandId, commandId, StringComparison.Ordinal))?
                .DisplayName
                ?? CommandCatalog.GetDisplayName(commandId);
            var unavailable = KeyboardHelpUnavailableReason(commandId);
            return string.IsNullOrWhiteSpace(unavailable)
                ? $"{spokenShortcut}: {displayName}. Kontekst: {context}"
                : $"{spokenShortcut}: {displayName}. Niedostępne: {unavailable}. Kontekst: {context}";
        }

        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
        {
            return $"{spokenShortcut}: standardowe działanie pola tekstowego. Kontekst: {context}";
        }

        return $"{spokenShortcut}: brak polecenia w tym miejscu. Kontekst: {context}";
    }

    private bool TryResolveKeyboardHelpCommand(Key key, ModifierKeys modifiers, out string commandId)
    {
        if (modifiers == ModifierKeys.Control && TryGetDigitKey(key, out var sessionSlot))
        {
            commandId = sessionSlot == 0 ? CommandIds.SessionList : CommandIds.SessionSlot(sessionSlot);
            return true;
        }

        if (_playerViewActive && PlayerPanel.IsKeyboardFocusWithin)
        {
            if (modifiers == ModifierKeys.None && TryGetDigitKey(key, out var digit))
            {
                commandId = CommandIds.SeekPercent(digit * 10);
                return true;
            }

            commandId = (modifiers, key) switch
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
                _ => string.Empty
            };
            if (commandId.Length > 0) return true;
        }

        if (MediaList.IsKeyboardFocusWithin
            && modifiers == ModifierKeys.Alt
            && key is Key.Up or Key.Down
            && (string.Equals(_currentView, "Ulubione", StringComparison.Ordinal)
                || string.Equals(_currentView, "Kolejka", StringComparison.Ordinal)
                || string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
                   && string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal)
                || TryGetPlaylistIdFromView(_currentView, out _)))
        {
            commandId = key == Key.Up
                ? CommandIds.MoveLocalLibraryItemUp
                : CommandIds.MoveLocalLibraryItemDown;
            return true;
        }

        var itemCommandsAvailable = _playerViewActive || MediaList.IsKeyboardFocusWithin;
        if (itemCommandsAvailable)
        {
            commandId = (modifiers, key) switch
            {
                (ModifierKeys.Alt | ModifierKeys.Shift, Key.Enter) => CommandIds.ItemPlaybackOptions,
                (ModifierKeys.Alt, Key.Enter) => CommandIds.ItemProperties,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.U) => CommandIds.ToggleFavorite,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.P) => CommandIds.ManagePlaylists,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.Q) => CommandIds.AddQueue,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.L) => CommandIds.ToggleLibrary,
                (ModifierKeys.Shift, Key.Enter) => CommandIds.AddQueue,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.Enter) => CommandIds.TogglePlayNext,
                (ModifierKeys.Control, Key.Enter) => CommandIds.ActivateSelected,
                _ => string.Empty
            };
            if (commandId.Length > 0) return true;
        }

        commandId = (modifiers, key) switch
        {
            (ModifierKeys.None, Key.F1) => CommandIds.Help,
            (ModifierKeys.None, Key.F6) or (ModifierKeys.Shift, Key.F6) => CommandIds.ViewNowPlaying,
            (ModifierKeys.Control, Key.PageUp) => CommandIds.SessionPrevious,
            (ModifierKeys.Control, Key.PageDown) => CommandIds.SessionNext,
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
            (ModifierKeys.Control | ModifierKeys.Shift, Key.O) => CommandIds.OpenLocalFolder,
            (ModifierKeys.Control, Key.O) => CommandIds.OpenLocalFiles,
            (ModifierKeys.Control, Key.OemComma) => CommandIds.SettingsGeneral,
            (ModifierKeys.Control, Key.F5) => CommandIds.ManageLocalSources,
            (ModifierKeys.Alt, Key.D1) => CommandIds.ViewFolders,
            (ModifierKeys.Alt, Key.D2) => CommandIds.ViewAllLocalFiles,
            (ModifierKeys.Alt, Key.D3) => CommandIds.ViewCustomLocalOrder,
            (ModifierKeys.None, Key.F5) => CommandIds.RefreshLocalLibrary,
            (ModifierKeys.None, Key.F2) => CommandIds.RenameLibraryItem,
            (ModifierKeys.Shift, Key.F2) => CommandIds.RenameLocalFile,
            (ModifierKeys.None, Key.Space) => CommandIds.PlayPause,
            _ => string.Empty
        };
        return commandId.Length > 0;
    }

    private bool TryDescribeDirectShortcut(Key key, ModifierKeys modifiers, out string description)
    {
        description = string.Empty;
        if (modifiers == ModifierKeys.Alt && key == Key.F4)
            description = "zamknij aplikację";
        else if (modifiers == ModifierKeys.None && key == Key.Escape)
            description = _playerViewActive
                ? "wyjdź z odtwarzacza"
                : "wyczyść filtr i wróć do listy albo zamknij bieżący poziom";
        else if (modifiers == ModifierKeys.None && key == Key.Back)
            description = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase
                ? "usuń znak przed kursorem"
                : "wróć poziom wyżej";
        else if (_playerViewActive && modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
            description = key == Key.Down
                ? "przejdź do starszego elementu historii odtwarzania"
                : "przejdź do nowszego elementu historii odtwarzania";
        else if (_playerViewActive && modifiers == ModifierKeys.None && key == Key.Delete)
            description = "usuń bieżący element z Biblioteki AMC, pozostawiając plik na dysku";
        else if ((_playerViewActive || MediaList.IsKeyboardFocusWithin)
                 && modifiers == ModifierKeys.Control && key == Key.C)
            description = "kopiuj nazwy zaznaczonych elementów";
        else if ((_playerViewActive || MediaList.IsKeyboardFocusWithin)
                 && modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.C)
            description = "kopiuj lokalizacje i fizyczne pliki zaznaczonych elementów";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.Control && key == Key.X)
            description = "wytnij lokalne pliki do późniejszego przeniesienia";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.Control && key == Key.V)
            description = "dodaj do bieżącego widoku lokalne pliki ze schowka";
        else if (modifiers == ModifierKeys.Control && key == Key.Z)
            description = "cofnij ostatnią zmianę Ulubionych, Biblioteki, Kolejki albo playlisty";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.Control && key == Key.A)
            description = "zaznacz wszystkie elementy bieżącej listy";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.Shift && key == Key.Delete)
            description = "po potwierdzeniu przenieś zaznaczone lokalne pliki do Kosza";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.None && key == Key.Delete)
            description = "usuń zaznaczone elementy tylko z bieżącego widoku";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.None && key == Key.Left)
            description = string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
                ? "oznajmia wielkość i bitrate pliku"
                : "oznajmia bitrate i inne dostępne parametry elementu";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.None && key is Key.Up or Key.Down)
            description = "przejdź do poprzedniego lub następnego elementu listy";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.Shift && key is Key.Up or Key.Down)
            description = "rozszerz zaznaczenie o poprzedni lub następny element";
        else if (MediaList.IsKeyboardFocusWithin && modifiers == ModifierKeys.None && key == Key.Enter)
            description = "otwórz zaznaczony element albo rozpocznij jego odtwarzanie";
        else if (MediaList.IsKeyboardFocusWithin
                 && modifiers == ModifierKeys.None
                 && key == Key.Insert
                 && string.Equals(_currentView, "Playlisty", StringComparison.Ordinal))
            description = "utwórz playlistę";
        else if (MediaList.IsKeyboardFocusWithin
                 && modifiers == ModifierKeys.None
                 && key == Key.F2
                 && string.Equals(_currentView, "Playlisty", StringComparison.Ordinal))
            description = "zmień nazwę wybranej playlisty";
        else if (MediaList.IsKeyboardFocusWithin
                 && modifiers == ModifierKeys.None
                 && key is >= Key.A and <= Key.Z)
            description = "szybko przejdź do elementu zaczynającego się od wpisanych liter";
        else if (modifiers == ModifierKeys.Alt && key is Key.Left or Key.Right)
            description = key == Key.Left ? "poprzedni widok" : "następny widok";

        return description.Length > 0;
    }

    private string? KeyboardHelpUnavailableReason(string commandId)
    {
        var needsItem = commandId is CommandIds.ActivateSelected or CommandIds.ToggleFavorite
            or CommandIds.ToggleLibrary or CommandIds.AddQueue or CommandIds.TogglePlayNext
            or CommandIds.ItemProperties or CommandIds.ItemPlaybackOptions
            or CommandIds.AddBookmark or CommandIds.AddNamedBookmark
            or CommandIds.PreviousBookmark or CommandIds.NextBookmark;
        if (needsItem && ActionItem is null) return "brak wybranego lub odtwarzanego elementu";
        if (commandId.StartsWith("transport.", StringComparison.Ordinal)
            && !_sessions.Current.HasCurrentItem)
        {
            return "brak bieżącego elementu multimedialnego";
        }
        return null;
    }

    private string KeyboardHelpContext()
    {
        return _sessions.Current.DisplayName;
    }

    private static string FormatShortcutForSpeech(KeyChord chord)
    {
        var parts = new List<string>(5);
        if (chord.Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (chord.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (chord.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (chord.Modifiers.HasFlag(KeyModifiers.Windows)) parts.Add("Windows");
        parts.Add(chord.Key switch
        {
            "Left" => "strzałka w lewo",
            "Right" => "strzałka w prawo",
            "Up" => "strzałka w górę",
            "Down" => "strzałka w dół",
            "Space" => "Spacja",
            "Escape" => "Escape",
            "Backspace" => "Backspace",
            "OemComma" => "przecinek",
            "OemPeriod" => "kropka",
            _ => chord.Key
        });
        return string.Join('+', parts);
    }

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

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
        if (Keyboard.Modifiers == ModifierKeys.Control && key == Key.F5)
        {
            ExecuteCommand(CommandIds.ManageLocalSources);
            return true;
        }
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
        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D3)
        {
            ExecuteCommand(CommandIds.ViewCustomLocalOrder);
            return true;
        }
        if (MediaList.IsKeyboardFocusWithin
            && Keyboard.Modifiers == ModifierKeys.Alt
            && (string.Equals(_currentView, "Ulubione", StringComparison.Ordinal)
                || string.Equals(_currentView, "Kolejka", StringComparison.Ordinal)
                || string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
                   && string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal)
                || TryGetPlaylistIdFromView(_currentView, out _))
            && key is Key.Up or Key.Down)
        {
            ExecuteCommand(key == Key.Up
                ? CommandIds.MoveLocalLibraryItemUp
                : CommandIds.MoveLocalLibraryItemDown);
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.None
            && key == Key.F5
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            ExecuteCommand(CommandIds.RefreshLocalLibrary);
            return true;
        }
        if (MediaList.IsKeyboardFocusWithin
            && Keyboard.Modifiers == ModifierKeys.None
            && key == Key.Insert
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && string.Equals(_currentView, "Biblioteka", StringComparison.Ordinal))
        {
            AddRadioStation();
            return true;
        }
        if (MediaList.IsKeyboardFocusWithin
            && Keyboard.Modifiers == ModifierKeys.None
            && key == Key.Insert
            && string.Equals(_currentView, "Playlisty", StringComparison.Ordinal))
        {
            CreatePlaylist();
            return true;
        }
        if (MediaList.IsKeyboardFocusWithin
            && Keyboard.Modifiers == ModifierKeys.None
            && key == Key.F2
            && string.Equals(_currentView, "Playlisty", StringComparison.Ordinal)
            && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null)
        {
            RenameSelectedPlaylist();
            return true;
        }
        if (MediaList.IsKeyboardFocusWithin
            && key == Key.F2
            && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                    Announce("Stacja nie jest plikiem na dysku. F2 edytuje jej nazwę i adres strumienia");
                else
                    EditRadioStation();
                return true;
            }
            ExecuteCommand(Keyboard.Modifiers == ModifierKeys.Shift
                ? CommandIds.RenameLocalFile
                : CommandIds.RenameLibraryItem);
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
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && key == Key.R)
        {
            ToggleRadioRecording();
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.None
            && key == Key.End
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            _radioOutput.JumpToLive();
            if (_state.Settings.Messages.SeekMessages)
            {
                Announce("Na żywo");
            }
            UpdatePlayerView();
            UpdatePlaybackStatusBar();
            return true;
        }
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
        if (modifiers == (ModifierKeys.Alt | ModifierKeys.Shift) && e.SystemKey == Key.Enter)
        {
            ExecuteCommand(CommandIds.ItemPlaybackOptions);
            return true;
        }
        if (modifiers == ModifierKeys.Alt && e.SystemKey == Key.Enter)
        {
            ExecuteCommand(CommandIds.ItemProperties);
            return true;
        }

        if (!_playerViewActive
            && (MediaList.SelectedItem as MediaItemRow)?.AlbumFolderPath is not null
            && (modifiers, e.Key) is
                (ModifierKeys.Control | ModifierKeys.Shift, Key.U)
                or (ModifierKeys.Control | ModifierKeys.Shift, Key.P)
                or (ModifierKeys.Control | ModifierKeys.Shift, Key.Q)
                or (ModifierKeys.Control | ModifierKeys.Shift, Key.L)
                or (ModifierKeys.Shift, Key.Enter)
                or (ModifierKeys.Control | ModifierKeys.Shift, Key.Enter))
        {
            Announce("Otwórz album Enterem, aby wykonać działanie na jego utworach");
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
        PlayerHelpText.Text = PlayerKeyboardHelpText();
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
        if (string.Equals(_currentView, LocalAlbumContentsViewName, StringComparison.Ordinal)
            || TryGetPlaylistIdFromView(_currentView, out _))
        {
            NavigateBack();
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
        IReadOnlyList<SearchWindow.SearchResult> visibleResults,
        SearchResultAction action,
        bool _)
    {
        if (results.Count == 0) return "Brak zaznaczonego wyniku";
        if (action == SearchResultAction.CopyName)
        {
            _pendingExternalMoves.Clear();
            if (!ClipboardRetry.TrySetText(
                    string.Join(Environment.NewLine, results.Select(result => result.Item.Title)),
                    out var clipboardError))
            {
                return clipboardError;
            }
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
                    var playbackContextItemIds = visibleResults
                        .Where(candidate => string.Equals(
                            candidate.SessionId,
                            session.Id,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(candidate => candidate.Item.Id)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    session.SetPlaybackContext(playbackContextItemIds);
                    _preservePreparedPlaybackContext = true;
                    try
                    {
                        ExecuteCommand(CommandIds.ActivateSelected);
                    }
                    finally
                    {
                        _preservePreparedPlaybackContext = false;
                    }
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
                case SearchResultAction.Library:
                    ExecuteCommand(CommandIds.ToggleLibrary);
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
        _radioOutput.Dispose();
        _radioCatalog.Dispose();
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
        if (SelectedBookmark is not null
            || (!_playerViewActive && SelectedItem?.Kind is not (MediaItemKind.Track or MediaItemKind.Station)))
        {
            ActivateSelected();
        }
        else ExecuteCommand(CommandIds.ActivateSelected);
    }
    private void PlayNext_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.TogglePlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.AddQueue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleFavorite);
    private void Library_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleLibrary);
    private void Playlists_Click(object sender, RoutedEventArgs e) => ShowPlaylistManager();
    private void Information_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ItemProperties);
    private void ItemPlaybackOptions_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ItemPlaybackOptions);
    private void GoToAlbum_Click(object sender, RoutedEventArgs e) => GoToRelatedAlbum();
    private void GoToArtist_Click(object sender, RoutedEventArgs e) => GoToRelatedArtist();
    private void CopyName_Click(object sender, RoutedEventArgs e) => CopyActionItemName();
    private void CopyLocation_Click(object sender, RoutedEventArgs e) => CopyActionItemLocation();
    private void CutFiles_Click(object sender, RoutedEventArgs e) =>
        Announce(CutLocalFilesForExternalMove(ActionItems));
    private void PasteFiles_Click(object sender, RoutedEventArgs e) =>
        PasteClipboardFilesIntoCurrentView();
    private void OpenDefaultApplication_Click(object sender, RoutedEventArgs e) => OpenLocalInDefaultApplication();
    private void RenameLibraryItem_Click(object sender, RoutedEventArgs e) => RenameLibraryItem();
    private void RenameLocalFile_Click(object sender, RoutedEventArgs e) => RenameLocalFile();
    private void NewPlaylist_Click(object sender, RoutedEventArgs e) => CreatePlaylist();
    private void RenamePlaylist_Click(object sender, RoutedEventArgs e) => RenameSelectedPlaylist();
    private void DeletePlaylist_Click(object sender, RoutedEventArgs e) => DeleteSelectedPlaylist();
    private void MoveLocalItemUp_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.MoveLocalLibraryItemUp);
    private void MoveLocalItemDown_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.MoveLocalLibraryItemDown);
    private void OfficialApp_Click(object sender, RoutedEventArgs e) => OpenOfficialApplication();
    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelected();
    private void Recycle_Click(object sender, RoutedEventArgs e) => MoveSelectedLocalFilesToRecycleBin();
    private void PlayerRemoveLocalItem_Click(object sender, RoutedEventArgs e) => RemoveCurrentLocalItemFromPlayer();
    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastMembershipChange();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenLocalFiles_Click(object sender, RoutedEventArgs e) => OpenLocalFiles();
    private void OpenLocalFolder_Click(object sender, RoutedEventArgs e) => OpenLocalFolder();
    private void AddRadioStation_Click(object sender, RoutedEventArgs e) => AddRadioStation();
    private void RadioRecording_Click(object sender, RoutedEventArgs e) => ToggleRadioRecording();
    private void Sessions_Click(object sender, RoutedEventArgs e) => ShowSessionList();
    private void MediaContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var items = ActionItems;
        var actionItem = ActionItem;
        var playlistContainer = !_playerViewActive
            && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null;
        var playlistContents = !_playerViewActive
            && TryGetPlaylistIdFromView(_currentView, out _);
        var localAlbumContainer = !_playerViewActive
            && (MediaList.SelectedItem as MediaItemRow)?.AlbumFolderPath is not null;
        var folderNavigationRow = !_playerViewActive
            && string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
            && (MediaList.SelectedItem as MediaItemRow)?.FolderPath is not null;
        var membershipItems = folderNavigationRow
            && TryResolveSelectedFolderContents(out var folderContext)
                ? folderContext.Items
                : items;
        var playbackLabel = playlistContainer
            ? "Otwórz playlistę"
            : localAlbumContainer
            ? "Otwórz album"
            : folderNavigationRow
            ? "Otwórz folder"
            : actionItem is not null
            && string.Equals(actionItem.Id, _sessions.Current.CurrentItem.Id, StringComparison.Ordinal)
            && _sessions.Current.IsPlaying
                ? "Wstrzymaj"
                : "Odtwórz";
        SetContextMenuItemPresentation(
            PlaybackMenuItem,
            playbackLabel,
            localAlbumContainer || playlistContainer || folderNavigationRow ? "Enter" : "Ctrl+Enter");
        var playNextActive = membershipItems.Count > 0
            && (folderNavigationRow
                ? membershipItems.Any(item => item.IsPlayNext)
                : membershipItems.All(item => item.IsPlayNext));
        var playNextLabel = playNextActive
            ? folderNavigationRow
                ? "Usuń zawartość folderu z odtwarzanych jako następne"
                : "Usuń z odtwarzanych jako następne"
            : folderNavigationRow
                ? "Odtwórz zawartość folderu jako następną"
                : "Odtwórz jako następne";
        SetContextMenuItemPresentation(PlayNextMenuItem, playNextLabel, "Ctrl+Shift+Enter");
        var queueActive = membershipItems.Count > 0
            && (folderNavigationRow
                ? membershipItems.Any(item => item.IsInQueue || item.IsPlayNext)
                : membershipItems.All(item => item.IsInQueue || item.IsPlayNext));
        var queueLabel = queueActive
            ? folderNavigationRow
                ? "Usuń zawartość folderu z kolejki"
                : "Usuń z kolejki"
            : folderNavigationRow
                ? "Dodaj zawartość folderu do kolejki"
                : "Dodaj do kolejki";
        SetContextMenuItemPresentation(QueueMenuItem, queueLabel, "Shift+Enter");
        var favoriteActive = membershipItems.Count > 0
            && (folderNavigationRow
                ? membershipItems.Any(item => item.IsFavorite)
                : membershipItems.All(item => item.IsFavorite));
        var favoriteLabel = favoriteActive
            ? folderNavigationRow
                ? "Usuń zawartość folderu z ulubionych"
                : "Usuń z ulubionych"
            : folderNavigationRow
                ? "Dodaj zawartość folderu do ulubionych"
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
        NewPlaylistMenuItem.Visibility = string.Equals(_currentView, "Playlisty", StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
        RenamePlaylistMenuItem.Visibility = playlistContainer ? Visibility.Visible : Visibility.Collapsed;
        DeletePlaylistMenuItem.Visibility = playlistContainer ? Visibility.Visible : Visibility.Collapsed;
        PlayNextMenuItem.Visibility = localAlbumContainer
            || playlistContainer
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        QueueMenuItem.Visibility = localAlbumContainer
            || playlistContainer
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        FavoriteMenuItem.Visibility = localAlbumContainer
            || playlistContainer
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        LibraryMenuItem.Visibility = localAlbumContainer || playlistContainer || folderNavigationRow
            ? Visibility.Collapsed
            : Visibility.Visible;
        PlaylistMembershipMenuItem.Visibility = playlistContainer
            || localAlbumContainer
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        CopyLocationMenuItem.Visibility = localAlbumContainer || playlistContainer ? Visibility.Collapsed : Visibility.Visible;
        ItemPlaybackOptionsMenuItem.Visibility = playlistContainer
            || SelectedBookmark is null
               && actionItem?.Kind == MediaItemKind.Station
               && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            ? Visibility.Collapsed
            : Visibility.Visible;
        var relatedAlbum = FindRelatedLocalAlbum(actionItem);
        GoToAlbumMenuItem.Visibility = relatedAlbum is null ? Visibility.Collapsed : Visibility.Visible;
        GoToArtistMenuItem.Visibility = relatedAlbum is not null
            && !string.IsNullOrWhiteSpace(relatedAlbum.Artist)
                ? Visibility.Visible
                : Visibility.Collapsed;
        var localItems = SelectedBookmark is null
            && items.Count > 0
            && items.All(item => TryGetLocalPath(item.Source, out var path) && File.Exists(path));
        CutFilesMenuItem.Visibility = localItems ? Visibility.Visible : Visibility.Collapsed;
        PasteFilesMenuItem.Visibility = CanPasteFilesIntoCurrentView()
            ? Visibility.Visible
            : Visibility.Collapsed;
        var localItem = localItems && actionItem is not null;
        var localRenameItem = SelectedBookmark is null
            && items.Count == 1
            && actionItem?.Kind == MediaItemKind.Track
            && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
            && TryGetLocalPath(actionItem.Source, out _);
        var radioRenameItem = SelectedBookmark is null
            && items.Count == 1
            && actionItem?.Kind == MediaItemKind.Station
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal);
        RenameLibraryItemMenuItem.Visibility = localRenameItem || radioRenameItem
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (radioRenameItem)
            SetContextMenuItemPresentation(RenameLibraryItemMenuItem, "Edytuj nazwę i adres stacji", "F2");
        else
            SetContextMenuItemPresentation(RenameLibraryItemMenuItem, "Zmień nazwę w Bibliotece", "F2");
        RenameLocalFileMenuItem.Visibility = localRenameItem && localItem
            ? Visibility.Visible
            : Visibility.Collapsed;
        var movableCustomOrderItems = !_playerViewActive
            && (string.Equals(_currentView, "Ulubione", StringComparison.Ordinal)
                || string.Equals(_currentView, "Kolejka", StringComparison.Ordinal)
                || string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
                   && string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal)
                || playlistContents)
            && items.Count > 0;
        MoveLocalItemUpMenuItem.Visibility = movableCustomOrderItems
            ? Visibility.Visible
            : Visibility.Collapsed;
        MoveLocalItemDownMenuItem.Visibility = movableCustomOrderItems
            ? Visibility.Visible
            : Visibility.Collapsed;
        MoveLocalItemUpMenuItem.IsEnabled = string.IsNullOrWhiteSpace(FilterBox.Text);
        MoveLocalItemDownMenuItem.IsEnabled = string.IsNullOrWhiteSpace(FilterBox.Text);
        OpenDefaultApplicationMenuItem.Visibility = localItem ? Visibility.Visible : Visibility.Collapsed;
        OfficialApplicationMenuItem.Visibility = localItem || localAlbumContainer || playlistContainer
            ? Visibility.Collapsed
            : Visibility.Visible;
        RecycleMenuItem.Visibility = localItem ? Visibility.Visible : Visibility.Collapsed;
        var removeLabel = folderNavigationRow
            ? "Folder nawigacyjny — użyj Enter"
            : playlistContents
                ? "Usuń z playlisty"
            : SelectedBookmark is not null
            ? "Usuń zakładkę"
            : string.Equals(_currentView, "Historia odtwarzania", StringComparison.Ordinal)
                ? "Usuń z Historii odtwarzania"
            : localItem && string.Equals(_currentView, DefaultBrowserView, StringComparison.Ordinal)
                ? "Usuń z AMC, pozostaw plik na dysku"
                : localItem && string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
                    ? "Usuń z biblioteki, pozostaw plik w folderze"
                : localItem && string.Equals(_currentView, AllLocalFilesViewName, StringComparison.Ordinal)
                    ? "Wyklucz z biblioteki, pozostaw plik na dysku"
                : localItem && string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal)
                    ? "Wyklucz z biblioteki, pozostaw plik na dysku"
                : localItem && string.Equals(_currentView, LocalAlbumContentsViewName, StringComparison.Ordinal)
                    ? "Wyklucz z biblioteki, pozostaw plik na dysku"
                : "Usuń z bieżącego widoku";
        SetContextMenuItemPresentation(RemoveMenuItem, removeLabel, "Delete");
        RemoveMenuItem.Visibility = localAlbumContainer || playlistContainer ? Visibility.Collapsed : Visibility.Visible;
        RemoveMenuItem.IsEnabled = !folderNavigationRow
            && (_currentView is not (FolderViewName or AllLocalFilesViewName or CustomLocalOrderViewName)
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
        var radioSession = string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal);
        PlayerRadioRecordingMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        PlayerAddBookmarkMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerAddNamedBookmarkMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerBookmarksMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        if (radioSession)
        {
            SetContextMenuItemPresentation(
                PlayerRadioRecordingMenuItem,
                _radioOutput.IsRecording ? "Zakończ nagrywanie radia" : "Rozpocznij nagrywanie radia",
                "Ctrl+Alt+R");
        }
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
        PlayerItemPlaybackOptionsMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        var relatedAlbum = FindRelatedLocalAlbum(item);
        PlayerGoToAlbumMenuItem.Visibility = relatedAlbum is null ? Visibility.Collapsed : Visibility.Visible;
        PlayerGoToArtistMenuItem.Visibility = relatedAlbum is not null
            && !string.IsNullOrWhiteSpace(relatedAlbum.Artist)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
            if (!ClipboardRetry.TrySetText(
                    string.Join(Environment.NewLine, selectedRows.Select(row => row.Label)),
                    out var bookmarkClipboardError))
            {
                Announce(bookmarkClipboardError);
                return;
            }
            Announce(selectedRows.Length == 1
                ? "Skopiowano zakładkę"
                : $"Skopiowano zakładki: {selectedRows.Length}");
            return;
        }

        var items = ActionItems;
        if (items.Count == 0) return;
        _pendingExternalMoves.Clear();
        if (!ClipboardRetry.TrySetText(
                string.Join(Environment.NewLine, items.Select(item => item.Title)),
                out var nameClipboardError))
        {
            Announce(nameClipboardError);
            return;
        }
        Announce(items.Count == 1
            ? "Skopiowano nazwę"
            : $"Skopiowano nazwy: {FormatItemCount(items.Count)}");
    }

    private void CopyActionItemLocation()
    {
        DiagnosticLog.Info(
            "clipboard",
            $"Polecenie Ctrl+Shift+C; widok: {_currentView}; odtwarzacz: {_playerViewActive}; zaznaczenie: {ActionItems.Count}.");
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
            if (!ClipboardRetry.TrySetDataObject(data, out var fileClipboardError)) return fileClipboardError;
            return localPaths.Length == 1
                ? "Skopiowano plik i pełną ścieżkę"
                : $"Skopiowano pliki i pełne ścieżki: {FormatFileCount(localPaths.Length)}";
        }

        var item = items[0];
        var publicUri = string.IsNullOrWhiteSpace(item.PublicUri)
            ? $"demo://{sessionId}/{item.Id}"
            : item.PublicUri;
        if (!ClipboardRetry.TrySetText(publicUri, out var uriClipboardError)) return uriClipboardError;
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
            if (!ClipboardRetry.TrySetDataObject(data, out var fileClipboardError)) return fileClipboardError;
        }
        else
        {
            if (!ClipboardRetry.TrySetText(text, out var textClipboardError)) return textClipboardError;
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
        && _currentView is DefaultBrowserView or AllLocalFilesViewName or CustomLocalOrderViewName or "Kolejka" or "Ulubione";

    private void PasteClipboardFilesIntoCurrentView()
    {
        if (!CanPasteFilesIntoCurrentView())
        {
            Announce("Pliki można wkleić w lokalnych widokach Wszystkie pliki, Kolejność własna, Kolejka lub Ulubione");
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
        var clipboardRefreshError = ClipboardRetry.TrySetDataObject(copiedData, out var refreshError)
            ? string.Empty
            : $" {refreshError}";

        RefreshCurrentView(preferredItemId: targetItems[0].Id);
        SelectMediaItems(targetItems.Select(item => item.Id));
        TrySaveLocalMediaState(true);
        RestoreMediaListFocusAfterRefresh();

        var destination = _currentView switch
        {
            "Kolejka" => "do kolejki",
            "Ulubione" => "do ulubionych",
            AllLocalFilesViewName => "do biblioteki",
            CustomLocalOrderViewName => "do biblioteki",
            _ => "do multimediów lokalnych"
        };
        var newPart = addedItems.Count > 0
            ? $" Nowe w AMC: {FormatFileCount(addedItems.Count)}."
            : " Wszystkie pliki były już w AMC.";
        var skippedPart = skippedCount > 0 ? $" Pominięto: {skippedCount}." : string.Empty;
        Announce($"Dodano ze schowka {destination}: {FormatFileCount(targetItems.Count)}.{newPart}{skippedPart} Pliki pozostały w swoich folderach.{clipboardRefreshError}");
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
        if (!ClipboardRetry.TrySetDataObject(data, out var clipboardError)) return clipboardError;
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
        var replacementResult = RefreshLocalSessionItems();
        if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            if (_playerViewActive)
            {
                if (_sessions.Current.HasCurrentItem) UpdatePlayerView(true);
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
        if (replacementResult.CurrentItemRemoved)
        {
            message += replacementResult.SelectedSuccessor is { } successor
                ? $". Następny element z bieżącego widoku: {successor.Title}"
                : ". Brak następnego elementu w bieżącym widoku";
        }
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
    private void CustomLocalOrderView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewCustomLocalOrder);
    private void RefreshLocalLibrary_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.RefreshLocalLibrary);
    private void ManageLocalSources_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ManageLocalSources);
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
    private void KeyboardHelp_Click(object sender, RoutedEventArgs e) => ToggleKeyboardHelp();
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
        string? folderPath = null,
        string? albumFolderPath = null,
        string? playlistId = null) : INotifyPropertyChanged
    {
        public MediaItem Item { get; } = item;
        public MediaItem ActionItem { get; } = actionItem ?? item;
        public BookmarkEntry? Bookmark { get; } = bookmark;
        public string? FolderPath { get; } = folderPath;
        public string? AlbumFolderPath { get; } = albumFolderPath;
        public string? PlaylistId { get; } = playlistId;
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

    private sealed record FolderContentsActionContext(
        string FolderLabel,
        IReadOnlyList<MediaItem> Items);

    private sealed record PlaylistStateUndo(
        long Sequence,
        PlaylistSettings PreviousState,
        string Announcement);

    private sealed record QueueOrderUndo(
        long Sequence,
        string SessionId,
        IReadOnlyList<string> PreviousOrder,
        IReadOnlyList<string> SelectedItemIds,
        string Announcement);

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
        RemovedSessionRegistration? DetachedSession,
        IReadOnlyList<ManualOrderPosition> CustomOrderPositions);
}
