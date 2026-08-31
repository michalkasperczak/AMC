using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private const string ActiveRadioRecordingsViewName = "Nagrywane";
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
    private readonly ITrackRecognitionService _trackRecognitionService = new ShazamTrackRecognitionService();
    private readonly CancellationTokenSource _trackRecognitionCancellation = new();
    private bool _radioRecognitionMonitoring;
    private DateTime _nextRadioRecognitionUtc = DateTime.MaxValue;
    private int _trackRecognitionInProgress;
    private string? _cloudPreparingItemId;
    private readonly List<MediaItem> _localItems = [];
    private readonly List<MediaItem> _radioItems = [];
    private readonly Dictionary<string, string> _pendingExternalMoves =
        new(StringComparer.Ordinal);
    private PendingInternalListMove? _pendingInternalListMove;
    private readonly DispatcherTimer _playerUiTimer;
    private readonly DispatcherTimer _localSourceSyncTimer;
    private readonly DispatcherTimer _radioScheduleTimer;
    private readonly Dictionary<string, ActiveScheduledRadioRecording> _activeScheduledRadioRecordings =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ActiveManualRadioRecording> _activeManualRadioRecordings =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _bulkStoppedManualRadioRecordings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _bulkStoppedScheduledRadioRecordings = new(StringComparer.Ordinal);
    private readonly SystemWakeTimer _radioWakeTimer = new();
    private readonly Dictionary<string, FileSystemWatcher> _localSourceWatchers =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _localSourceSyncInProgress;
    private bool _localSourceSyncPending;
    private bool _isClosing;
    private bool _recordingCloseConfirmed;
    private bool _playerViewActive;
    private bool _keyboardHelpActive;
    private bool _restoringSessionNavigation;
    private string? _playerFocusContextPrefix;
    private DateTime _lastLocalStateSaveUtc;
    private long _lastSavedLocalPositionTicks = -1;
    private long _quickInformationRequestVersion;
    private string? _radioNowPlayingItemId;
    private string? _radioNowPlayingTitle;
    private readonly AccessiblePlaybackStatusStrip _playbackStatusBar;
    private readonly System.Windows.Forms.ToolStripStatusLabel _playbackStatusLabel;

    private sealed record PendingInternalListMove(
        string SessionId,
        string ViewName,
        IReadOnlyList<string> ItemIds);

    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyAlt = 0x12;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;
    private const int VirtualKeyC = 0x43;
    private const int VirtualKeyE = 0x45;
    private const int VirtualKeyG = 0x47;
    private const int VirtualKeyR = 0x52;
    private const int VirtualKeyT = 0x54;
    private const int VirtualKeyZ = 0x5A;
    private bool _nativeControlDown;
    private bool _nativeShiftDown;
    private bool _nativeAltDown;
    private bool _nativeWindowsDown;
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
        MenuAccessibility.NormalizeMainMenu(MainMenu);
        MenuAccessibility.NormalizeContextMenu(MediaList.ContextMenu);
        MenuAccessibility.NormalizeContextMenu(PlayerPanel.ContextMenu);
        const string initialStatus = "pauza, 0:00";
        _playbackStatusLabel = new System.Windows.Forms.ToolStripStatusLabel
        {
            AccessibleName = initialStatus,
            AccessibleRole = System.Windows.Forms.AccessibleRole.StaticText,
            Spring = true,
            Text = initialStatus,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };
        _playbackStatusBar = new AccessiblePlaybackStatusStrip
        {
            AccessibleRole = System.Windows.Forms.AccessibleRole.StatusBar,
            SpokenText = initialStatus,
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
        _radioScheduleTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _radioScheduleTimer.Tick += RadioScheduleTimer_Tick;
        _state = state;
        _store = store;
        _localOutput.ConfigureAudioProcessing(_state.Settings.Audio);
        _localOutput.ConfigureAudioProcessingResolver(GetEffectiveLocalAudioSettings);
        _radioOutput = new RadioMediaOutput(_state.Radio.TimeshiftMinutes);
        _radioOutput.PlaybackFailed += RadioOutput_PlaybackFailed;
        _radioOutput.PlaybackPreparing += RadioOutput_PlaybackPreparing;
        _radioOutput.PlaybackStarted += RadioOutput_PlaybackStarted;
        _radioOutput.NowPlayingChanged += RadioOutput_NowPlayingChanged;
        NormalizeTransientBookmarkViewsAtStartup();
        NormalizePlaylistViewsAtStartup();
        NormalizeLocalLibraryNavigationAtStartup();
        NormalizeRadioNavigationAtStartup();
        ClearPersistedListFiltersAtStartup();
        _playbackHistory = new PlaybackHistory(_state.PlaybackHistory);
        _bookmarkIndex = new BookmarkIndex(_state.Bookmarks);
        LoadPersistedLocalMedia();
        LoadPersistedRadio();
        NormalizeRadioSchedulesAtStartup();
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
        _radioScheduleTimer.Start();
        RearmRadioWakeTimer();
        Dispatcher.BeginInvoke(ProcessDueRadioSchedules, DispatcherPriority.Background);
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
        if (navigation.CurrentView is DefaultBrowserView
            or "Radio i rekomendacje"
            or "Kolejka"
            or "Albumy"
            or BookmarkViewName
            or ActiveRadioRecordingsViewName
            || TryGetPlaylistIdFromView(navigation.CurrentView, out var playlistId)
               && new PlaylistIndex(_state.Playlists).Find(playlistId) is not { SessionId: "radio" })
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
                .DistinctBy(item => item.Id, StringComparer.Ordinal)
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
                Announce("Foldery są dostępne po otwarciu lokalnego folderu z plikami multimedialnymi");
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
            if (dialog.SelectedAction == SearchResultAction.Preset)
            {
                ShowPresetAssignment();
                return;
            }
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

        UpdateFileMenuForCurrentSession();
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
        AnchorMediaListFocus();
        UpdateFileMenuForCurrentSession();
        var navigation = GetSessionNavigationState(_sessions.Current.Id);
        navigation.PlayerActive = false;
        _currentView = navigation.CurrentView;
        RestoreFilterForCurrentView(navigation);
        var preferredItemId = navigation.SelectedItemIds.GetValueOrDefault(_currentView);
        string? followedPlaybackItemId = null;
        if (_state.Settings.FollowPlaybackOnPlayerExit && _sessions.Current.HasCurrentItem)
        {
            followedPlaybackItemId = _sessions.Current.CurrentItem.Id;
            preferredItemId = followedPlaybackItemId;
        }
        RefreshCurrentView(preferredItemId: preferredItemId);
        if (followedPlaybackItemId is not null
            && string.Equals(SelectedItem?.Id, followedPlaybackItemId, StringComparison.Ordinal))
        {
            navigation.SelectedItemIds[_currentView] = followedPlaybackItemId;
        }
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
        if (session.IsMuted) state += ", wyciszono";
        if (isRadio && RadioRecordingStateLabel(item) is { } recordingState)
            state += $", {recordingState}";

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
        RadioRecordingButton.Content = IsRadioStationManuallyRecording(item)
            ? "_Zakończ nagrywanie"
            : "_Nagrywaj radio";
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
            return "Strzałki w lewo i w prawo poruszają się po buforze transmisji, Home przechodzi do początku bufora, End wraca na żywo, a strzałki w górę i w dół regulują głośność. R rozpoczyna lub kończy nagrywanie bieżącej stacji w tle, a Shift+Spacja wstrzymuje lub wznawia jej nagranie. S rozpoznaje utwór, a Shift+S włącza lub wyłącza obserwowanie rozpoznawania. Page Up i Page Down wybierają poprzednią lub następną stację bez zatrzymywania nagrań. " + exit;
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
        TryStartScheduledRadioRecognition();
    }

    private void TryStartScheduledRadioRecognition()
    {
        if (!_radioRecognitionMonitoring
            || DateTime.UtcNow < _nextRadioRecognitionUtc
            || Volatile.Read(ref _trackRecognitionInProgress) != 0)
        {
            return;
        }
        _nextRadioRecognitionUtc = DateTime.UtcNow.AddMinutes(1);
        _ = RecognizeCurrentRadioTrackAsync(automatic: true);
    }

    private async Task RecognizeCurrentRadioTrackAsync(bool automatic)
    {
        if (Interlocked.CompareExchange(ref _trackRecognitionInProgress, 1, 0) != 0)
        {
            if (!automatic) Announce("Rozpoznawanie już trwa");
            return;
        }

        try
        {
            var radioSession = _sessions.FindSession("radio");
            var loadedId = _radioOutput.LoadedItemId;
            var station = loadedId is null
                ? null
                : radioSession?.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, loadedId, StringComparison.Ordinal));
            if (station is null || radioSession?.IsPlaying != true)
            {
                if (!automatic) Announce("Najpierw uruchom stację radiową");
                else _nextRadioRecognitionUtc = DateTime.UtcNow.AddSeconds(15);
                return;
            }
            if (!_radioOutput.TryGetRecentPlaybackAudio(TimeSpan.FromSeconds(12), out var snapshot)
                || snapshot is null)
            {
                if (!automatic)
                    Announce("Za mało dźwięku w buforze. Poczekaj kilka sekund i spróbuj ponownie");
                else _nextRadioRecognitionUtc = DateTime.UtcNow.AddSeconds(15);
                return;
            }

            if (!automatic) Announce("Rozpoznaję utwór");
            DiagnosticLog.Info(
                "recognition",
                $"Rozpoczęto rozpoznawanie z {snapshot.Duration.TotalSeconds:0.0} sekundy odsłuchiwanego bufora stacji {station.Title}.");
            var result = await _trackRecognitionService.RecognizeAsync(
                snapshot,
                _trackRecognitionCancellation.Token);
            if (_isClosing) return;
            if (!result.Success)
            {
                DiagnosticLog.Info("recognition", result.Error ?? "Nie rozpoznano utworu.");
                if (!automatic && IsActive) AnnounceEssential(result.Error ?? "Nie rozpoznano utworu");
                return;
            }

            var label = RecognitionResultLabel(result);
            var duplicate = _state.Radio.RecognizedTracks
                .OrderByDescending(entry => entry.RecognizedUtcTicks)
                .FirstOrDefault(entry =>
                    string.Equals(entry.StationId, station.Id, StringComparison.Ordinal)
                    && string.Equals(entry.Title, result.Title, StringComparison.CurrentCultureIgnoreCase)
                    && string.Equals(entry.Artist, result.Artist, StringComparison.CurrentCultureIgnoreCase)
                    && entry.RecognizedUtcTicks >= DateTime.UtcNow.AddMinutes(-30).Ticks);
            if (duplicate is null)
            {
                _state.Radio.RecognizedTracks.Insert(0, new RadioRecognizedTrackSettings
                {
                    StationId = station.Id,
                    StationName = station.Title,
                    Title = result.Title,
                    Artist = result.Artist,
                    Album = result.Album,
                    ReleaseDate = result.ReleaseDate,
                    ProviderUri = result.ProviderUri,
                    RecognizedUtcTicks = DateTime.UtcNow.Ticks
                });
                if (_state.Radio.RecognizedTracks.Count > 2_000)
                    _state.Radio.RecognizedTracks.RemoveRange(2_000, _state.Radio.RecognizedTracks.Count - 2_000);
                _store.Save(_state);
                DiagnosticLog.Info("recognition", $"Rozpoznano {label} na stacji {station.Title}.");
                if (ShouldAnnounceRadioRecognitionResult(
                        automatic,
                        _state.Settings.Messages.Enabled,
                        _state.Settings.Messages.AutomaticRecognitionMessages,
                        IsActive))
                {
                    AnnounceEssential($"Rozpoznano: {label}");
                }
            }
            else if (!automatic && IsActive)
            {
                AnnounceEssential($"Rozpoznano: {label}. Ten utwór jest już w najnowszej historii");
            }
        }
        catch (OperationCanceledException) when (_trackRecognitionCancellation.IsCancellationRequested)
        {
            // Closing AMC cancels the optional network request without showing
            // a late message in a window that is already disappearing.
        }
        finally
        {
            Interlocked.Exchange(ref _trackRecognitionInProgress, 0);
            if (_radioRecognitionMonitoring && _nextRadioRecognitionUtc < DateTime.UtcNow)
                _nextRadioRecognitionUtc = DateTime.UtcNow.AddMinutes(1);
        }
    }

    private static string RecognitionResultLabel(TrackRecognitionResult result)
    {
        var parts = new[] { result.Title, result.Artist }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        var label = string.Join(" — ", parts);
        return string.IsNullOrWhiteSpace(label) ? "nieznany utwór" : label;
    }

    internal static bool ShouldAnnounceRadioRecognitionResult(
        bool automatic,
        bool messagesEnabled,
        bool automaticRecognitionMessagesEnabled,
        bool isWindowActive) =>
        isWindowActive && (!automatic || (messagesEnabled && automaticRecognitionMessagesEnabled));

    private void ToggleRadioRecognitionMonitoring()
    {
        if (_radioRecognitionMonitoring)
        {
            _radioRecognitionMonitoring = false;
            _nextRadioRecognitionUtc = DateTime.MaxValue;
            UpdateFileMenuForCurrentSession();
            AnnounceEssential("Wyłączono obserwowanie rozpoznawania utworów");
            return;
        }

        var radioSession = _sessions.FindSession("radio");
        if (_radioOutput.LoadedItemId is null || radioSession?.IsPlaying != true)
        {
            Announce("Najpierw uruchom stację radiową");
            return;
        }
        _radioRecognitionMonitoring = true;
        _nextRadioRecognitionUtc = DateTime.UtcNow;
        UpdateFileMenuForCurrentSession();
        AnnounceEssential("Włączono obserwowanie rozpoznawania utworów");
    }

    private void ShowRadioRecognitionHistory()
    {
        var dialog = new RadioRecognitionHistoryWindow(_state.Radio.RecognizedTracks)
        {
            Owner = this
        };
        dialog.ShowDialog();
        if (dialog.Changed) _store.Save(_state);
        Activate();
        if (_playerViewActive)
        {
            PlayerPlayPauseButton.Focus();
            Keyboard.Focus(PlayerPlayPauseButton);
        }
        else RestoreMediaListFocusAfterRefresh();
    }

    private void UpdatePlaybackStatusBar()
    {
        var text = BuildPlaybackStatusText();
        _playbackStatusLabel.Text = text;
        _playbackStatusLabel.AccessibleName = text;
        _playbackStatusBar.SpokenText = text;
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
        if (session.IsMuted) state += ", wyciszono";
        var time = isRadio
            ? _radioOutput.BehindLive < TimeSpan.FromSeconds(1)
                ? $"na żywo, bufor {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}"
                : $"{CommandRouter.FormatTime(_radioOutput.BehindLive)} za transmisją, bufor {CommandRouter.FormatTime(_radioOutput.BufferedDuration)}"
            : item.Duration > TimeSpan.Zero
            ? $"{CommandRouter.FormatTime(position)} z {CommandRouter.FormatTime(item.Duration)}"
            : $"{CommandRouter.FormatTime(position)}, czas całkowity nieznany";
        var parts = new List<string>();
        if (isRadio) parts.Add(item.Title);
        var audioParameters = AudioParametersFormatter.FormatCompact(item);
        if (!string.IsNullOrWhiteSpace(audioParameters)) parts.Add(audioParameters);
        var playbackState = state.ToLowerInvariant();
        if (Math.Abs(session.PlaybackRate - 1d) >= 0.001d)
        {
            playbackState += $", {CommandRouter.FormatPlaybackRate(session.PlaybackRate).ToLowerInvariant()}";
        }
        parts.Add(playbackState);
        parts.Add(time);
        if (!isRadio) parts.Add(item.Title);
        parts.Add(session.DisplayName);
        if (isRadio && RadioRecordingStateLabel(item) is { } recordingState)
            parts.Insert(1, recordingState);
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

    private List<SessionPresetEntry> SessionPresetEntries(string sessionId)
    {
        if (!_state.SessionPresets.EntriesBySession.TryGetValue(sessionId, out var entries))
        {
            entries = [];
            _state.SessionPresets.EntriesBySession[sessionId] = entries;
        }
        return entries;
    }

    private IReadOnlyList<RadioPresetChoice> SessionPresetChoices(DemoMediaSession session)
    {
        var presets = SessionPresetEntries(session.Id)
            .Where(preset => preset.Slot is >= 1 and <= RadioPresetSlots.Count)
            .GroupBy(preset => preset.Slot)
            .ToDictionary(group => group.Key, group => group.First());
        return Enumerable.Range(1, RadioPresetSlots.Count)
            .Select(slot =>
            {
                var preset = presets.GetValueOrDefault(slot);
                if (preset is null)
                {
                    return new RadioPresetChoice(
                        slot,
                        RadioPresetSlots.Label(slot),
                        RadioPresetSlots.SpokenShortcutLabel(slot),
                        null,
                        null,
                        null);
                }

                var currentItem = session.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, preset.TargetId, StringComparison.Ordinal));
                var currentTitle = currentItem?.Title ?? ResolvePresetContainerTitle(session.Id, preset);
                var currentLocation = currentItem is null
                    ? preset.TargetLocation
                    : GetShareableLocation(currentItem, session.Id);
                return new RadioPresetChoice(
                    slot,
                    RadioPresetSlots.Label(slot),
                    RadioPresetSlots.SpokenShortcutLabel(slot),
                    preset.TargetId,
                    string.IsNullOrWhiteSpace(currentTitle) ? preset.TargetTitle : currentTitle,
                    currentLocation);
            })
            .ToArray();
    }

    private string? ResolvePresetContainerTitle(string sessionId, SessionPresetEntry preset)
    {
        if (string.Equals(preset.TargetKind, "amcPlaylist", StringComparison.OrdinalIgnoreCase)
            && TryGetPresetPlaylistId(preset, out var playlistId))
        {
            return new PlaylistIndex(_state.Playlists).Find(playlistId) is { } playlist
                && string.Equals(playlist.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                    ? playlist.Name
                    : null;
        }
        if (string.Equals(sessionId, "local", StringComparison.Ordinal)
            && string.Equals(preset.TargetKind, "localAlbum", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(preset.TargetLocation))
        {
            return InferLocalAlbums().FirstOrDefault(album => string.Equals(
                NormalizeLocalFolderPath(album.FolderPath),
                NormalizeLocalFolderPath(preset.TargetLocation),
                StringComparison.OrdinalIgnoreCase))?.Title;
        }
        return null;
    }

    private bool TryGetSessionPresetTarget(
        out string targetId,
        out string targetKind,
        out string targetTitle,
        out string? targetLocation)
    {
        var row = _playerViewActive ? null : MediaList.SelectedItem as MediaItemRow;
        if (row?.PlaylistId is { Length: > 0 } playlistId)
        {
            targetId = $"playlist:{playlistId}";
            targetKind = "amcPlaylist";
            targetTitle = row.Item.Title;
            targetLocation = playlistId;
            return true;
        }
        if (row?.AlbumFolderPath is { Length: > 0 } albumFolderPath)
        {
            targetId = row.Item.Id;
            targetKind = "localAlbum";
            targetTitle = row.Item.Title;
            targetLocation = albumFolderPath;
            return true;
        }
        if (row?.FolderPath is { Length: > 0 } folderPath)
        {
            targetId = FolderRowId(folderPath);
            targetKind = "folder";
            targetTitle = row!.Item.Title;
            targetLocation = folderPath;
            return true;
        }

        var item = ActionItem;
        if (item is not null
            && string.Equals(ActionSession.Id, _sessions.Current.Id, StringComparison.OrdinalIgnoreCase))
        {
            targetId = item.Id;
            targetKind = item.Kind.ToString().ToLowerInvariant();
            targetTitle = item.Title;
            targetLocation = string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)
                && TryGetLocalPath(item.Source, out var localPath)
                    ? localPath
                    : GetShareableLocation(item, _sessions.Current.Id);
            return true;
        }

        targetId = string.Empty;
        targetKind = string.Empty;
        targetTitle = string.Empty;
        targetLocation = null;
        return false;
    }

    private void ShowPresets()
    {
        var session = _sessions.Current;
        _ = TryGetSessionPresetTarget(out var currentTargetId, out _, out _, out _);
        var dialog = new RadioPresetsWindow(
            SessionPresetChoices(session),
            string.IsNullOrWhiteSpace(currentTargetId) ? null : currentTargetId,
            session.DisplayName,
            copyLocalTargets: string.Equals(session.Id, "local", StringComparison.Ordinal))
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.SelectedSlot is int slot)
        {
            ActivatePreset(slot);
            return;
        }
        RestoreItemActionFocus();
    }

    private void ShowPresetAssignment()
    {
        if (!TryGetSessionPresetTarget(
                out var targetId,
                out var targetKind,
                out var targetTitle,
                out var targetLocation))
        {
            Announce($"Wybierz element sesji {_sessions.Current.DisplayName}, który chcesz przypisać do presetu");
            return;
        }

        var session = _sessions.Current;
        var choices = SessionPresetChoices(session);
        var existingTargetSlot = choices.FirstOrDefault(choice =>
            string.Equals(choice.StationId, targetId, StringComparison.Ordinal))?.Slot;
        var firstFree = choices.FirstOrDefault(choice => choice.StationId is null)?.Slot;
        var initialSlot = existingTargetSlot ?? firstFree ?? 1;
        var dialog = new RadioPresetAssignmentWindow(
            targetTitle,
            targetId,
            choices,
            firstFree,
            initialSlot,
            session.DisplayName)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            RestoreItemActionFocus();
            return;
        }

        var entries = SessionPresetEntries(session.Id);
        var existing = entries.FindIndex(preset => preset.Slot == dialog.SelectedSlot);
        var slotLabel = RadioPresetSlots.Label(dialog.SelectedSlot);
        if (dialog.SelectedAction == RadioPresetAssignmentAction.Remove)
        {
            if (existing >= 0) entries.RemoveAt(existing);
            SavePresetState(session.Id);
            Announce($"Usunięto preset {slotLabel}");
            RestoreItemActionFocus();
            return;
        }

        var preset = new SessionPresetEntry
        {
            Slot = dialog.SelectedSlot,
            TargetId = targetId,
            TargetKind = targetKind,
            TargetTitle = targetTitle,
            TargetLocation = targetLocation
        };
        if (existing >= 0) entries[existing] = preset;
        else entries.Add(preset);
        _state.SessionPresets.EntriesBySession[session.Id] = entries
            .OrderBy(entry => entry.Slot)
            .ToList();
        if (string.Equals(session.Id, "radio", StringComparison.Ordinal)
            && ActionItem is { Kind: MediaItemKind.Station } station)
        {
            station.IsInLibrary = true;
            if (_radioItems.All(item => !string.Equals(item.Id, station.Id, StringComparison.Ordinal)))
                _radioItems.Add(station);
        }
        SavePresetState(session.Id);
        Announce($"Zapisano preset {slotLabel}: {targetTitle}");
        RestoreItemActionFocus();
    }

    private void SavePresetState(string sessionId)
    {
        if (string.Equals(sessionId, "radio", StringComparison.Ordinal)) CaptureRadioState();
        if (string.Equals(sessionId, "local", StringComparison.Ordinal))
        {
            TrySaveLocalMediaState(true);
            return;
        }
        _store.Save(_state);
    }

    private void ActivatePreset(int slot, bool useDirectShortcutLabel = false)
    {
        var announcementSlotLabel = useDirectShortcutLabel
            ? RadioPresetKeyMap.DirectShortcutLabel(slot)
            : null;
        var slotLabel = announcementSlotLabel ?? RadioPresetSlots.Label(slot);
        var session = _sessions.Current;
        var preset = SessionPresetEntries(session.Id).FirstOrDefault(entry => entry.Slot == slot);
        if (preset is null)
        {
            Announce($"Preset {slotLabel} pusty. Ctrl+Alt+Shift+P przypisuje bieżący element");
            return;
        }

        if (string.Equals(session.Id, "local", StringComparison.Ordinal)
            && string.Equals(preset.TargetKind, "folder", StringComparison.OrdinalIgnoreCase))
        {
            var folderPath = preset.TargetLocation;
            var belongsToLibrary = !string.IsNullOrWhiteSpace(folderPath)
                && _state.LocalMedia.FolderSources.Any(source =>
                    IsSameOrDescendantPath(folderPath, source.Path));
            if (!belongsToLibrary)
            {
                Announce($"Preset {slotLabel} jest niedostępny. Folder nie należy już do Biblioteki");
                return;
            }

            CaptureCurrentSessionNavigationState();
            HidePlayerForBrowserNavigation();
            _state.LocalMedia.LibraryView = FolderViewName;
            _state.LocalMedia.CurrentFolderPath = folderPath;
            _currentView = FolderViewName;
            var navigation = GetSessionNavigationState("local");
            navigation.CurrentView = FolderViewName;
            navigation.PlayerActive = false;
            navigation.Filters[FolderViewName] = string.Empty;
            RestoreFilterForCurrentView(navigation);
            RefreshCurrentView();
            PrepareViewFocusContext($"Preset {slotLabel}, folder {preset.TargetTitle}");
            TrySaveLocalMediaState(false);
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        if (string.Equals(session.Id, "local", StringComparison.Ordinal)
            && string.Equals(preset.TargetKind, "localAlbum", StringComparison.OrdinalIgnoreCase))
        {
            var album = !string.IsNullOrWhiteSpace(preset.TargetLocation)
                ? InferLocalAlbums().FirstOrDefault(candidate => string.Equals(
                    NormalizeLocalFolderPath(candidate.FolderPath),
                    NormalizeLocalFolderPath(preset.TargetLocation),
                    StringComparison.OrdinalIgnoreCase))
                : null;
            if (album is null)
            {
                Announce($"Preset {slotLabel} jest niedostępny. Albumu nie ma już w Bibliotece");
                return;
            }
            OpenLocalAlbum(album.FolderPath, album.Title);
            return;
        }

        if (string.Equals(preset.TargetKind, "amcPlaylist", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetPresetPlaylistId(preset, out var playlistId)
                || new PlaylistIndex(_state.Playlists).Find(playlistId) is not { } playlist
                || !string.Equals(playlist.SessionId, session.Id, StringComparison.OrdinalIgnoreCase))
            {
                Announce($"Preset {slotLabel} jest niedostępny. Playlisty nie ma już w sesji {session.DisplayName}");
                return;
            }
            OpenPlaylist(playlist.Id, playlist.Name);
            return;
        }

        var item = session.Items.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, preset.TargetId, StringComparison.Ordinal));
        if (item is null)
        {
            Announce($"Preset {slotLabel} jest niedostępny w sesji {session.DisplayName}. Przypisz go ponownie skrótem Ctrl+Alt+Shift+P");
            return;
        }

        if (item.Kind is not (MediaItemKind.Track or MediaItemKind.Station))
        {
            SelectSessionBrowserItem(session.Id, item.Id);
            NavigateTo(item.Title);
            Announce($"{item.KindLabel}: {item.Title}");
            return;
        }

        var presetPlayableIds = SessionPresetEntries(session.Id)
            .OrderBy(entry => entry.Slot)
            .Select(entry => session.Items.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                entry.TargetId,
                StringComparison.Ordinal)))
            .Where(candidate => candidate?.Kind is MediaItemKind.Track or MediaItemKind.Station)
            .Select(candidate => candidate!.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        session.SetPlaybackContext(presetPlayableIds);
        var playbackNavigation = GetSessionNavigationState(session.Id);
        playbackNavigation.PlaybackContextView = "Presety";
        playbackNavigation.PlaybackContextItemIds = presetPlayableIds.ToList();
        session.Play(item);
        RecordPlayback(session, item);
        SavePresetState(session.Id);
        RefreshPlaybackIndicators();
        if (_playerViewActive || _state.Settings.OpenPlayerWhenActivatingPreset)
        {
            ShowPlayerView();
            return;
        }

        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
        if (!string.Equals(session.Id, "radio", StringComparison.Ordinal)
            || !_state.Settings.Messages.LoadingMessages)
        {
            Announce(item.Title);
        }
        RestoreItemActionFocus();
    }

    private static bool TryGetPresetPlaylistId(SessionPresetEntry preset, out string playlistId)
    {
        if (!string.IsNullOrWhiteSpace(preset.TargetLocation))
        {
            playlistId = preset.TargetLocation;
            return true;
        }
        const string prefix = "playlist:";
        if (preset.TargetId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            playlistId = preset.TargetId[prefix.Length..];
            return playlistId.Length > 0;
        }
        playlistId = string.Empty;
        return false;
    }

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
        var entries = CommandPaletteSearch.CreateEntries(ActiveKeyboardProfile(), _state.Settings)
            .Where(entry => CommandVisibleInPalette(entry.CommandId))
            .ToArray();
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

    private bool CommandVisibleInPalette(string commandId)
    {
        var radio = string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal);
        var local = string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal);
        if (!radio)
        {
            if (commandId is CommandIds.AddRadioStation
                or CommandIds.ImportRadioPlaylist
                or CommandIds.ToggleRadioRecording
                or CommandIds.ToggleRadioRecordingPause
                or CommandIds.SplitRadioRecording
                or CommandIds.StopAllRadioRecordings
                or CommandIds.AddRadioSchedule
                or CommandIds.ManageRadioSchedules
                or CommandIds.ViewActiveRadioRecordings
                or CommandIds.RadioJumpLive
                or CommandIds.RecognizeRadioTrack
                or CommandIds.ToggleRadioRecognitionMonitoring
                or CommandIds.ViewRadioRecognitionHistory)
            {
                return false;
            }

            return local
                || commandId is not (CommandIds.ViewRadioPresets or CommandIds.AssignRadioPreset)
                    && !CommandIds.TryParseRadioPreset(commandId, out _);
        }
        return commandId is not (CommandIds.AddQueue
            or CommandIds.TogglePlayNext
            or CommandIds.ViewQueue
            or CommandIds.ViewAlbums
            or CommandIds.ViewBookmarks
            or CommandIds.AddBookmark
            or CommandIds.AddNamedBookmark
            or CommandIds.PreviousBookmark
            or CommandIds.NextBookmark
            or CommandIds.SeekToTime
            or CommandIds.SeekToPercentage
            or CommandIds.PlaybackRateDown
            or CommandIds.PlaybackRateUp
            or CommandIds.PlaybackRateReset);
    }

    public void ShowItemProperties()
    {
        var item = ActionItem ?? _sessions.Current.CurrentItem;
        var activeOwner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive) ?? this;
        var links = item.Kind == MediaItemKind.Station
            ? new[]
            {
                string.IsNullOrWhiteSpace(item.Source)
                    ? null
                    : new InformationLink("Otwórz adres strumienia", item.Source),
                string.IsNullOrWhiteSpace(item.HomepageUri)
                    ? null
                    : new InformationLink("Otwórz stronę stacji", item.HomepageUri)
            }.Where(link => link is not null).Select(link => link!).ToArray()
            : [];
        var dialog = new InformationWindow(BuildItemPropertiesText(item), links) { Owner = activeOwner };
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
            saved.PlaybackRateOverride,
            saved.LoudnessNormalizationOverride,
            saved.SmoothTrackTransitionsOverride,
            saved.InterTrackSilenceMillisecondsOverride)
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
        saved.LoudnessNormalizationOverride = dialog.SelectedLoudnessNormalizationOverride;
        saved.SmoothTrackTransitionsOverride = dialog.SelectedSmoothTrackTransitionsOverride;
        saved.InterTrackSilenceMillisecondsOverride =
            dialog.SelectedInterTrackSilenceMillisecondsOverride;
        var local = _sessions.FindSession("local");
        if (local is not null
            && string.Equals(local.CurrentItem.Id, item.Id, StringComparison.Ordinal))
        {
            if (!ShouldRememberLocalPosition(item)) local.ClearRememberedPosition(item.Id);
            local.ApplyPlaybackRateForCurrentItem();
            _localOutput.ConfigureAudioProcessing(GetEffectiveLocalAudioSettings(item));
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
            saved?.LoudnessNormalizationOverride,
            saved?.SmoothTrackTransitionsOverride,
            saved?.InterTrackSilenceMillisecondsOverride,
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
        saved.LoudnessNormalizationOverride = dialog.SelectedLoudnessNormalizationOverride;
        saved.SmoothTrackTransitionsOverride = dialog.SelectedSmoothTrackTransitionsOverride;
        saved.InterTrackSilenceMillisecondsOverride =
            dialog.SelectedInterTrackSilenceMillisecondsOverride;
        var hasOverride = saved.ResumePositionMode != ResumePositionMode.Inherit
            || saved.PlaybackRateOverride.HasValue
            || saved.OutputDeviceId is not null
            || saved.LoudnessNormalizationOverride.HasValue
            || saved.SmoothTrackTransitionsOverride.HasValue
            || saved.InterTrackSilenceMillisecondsOverride.HasValue;
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
                _localOutput.ConfigureAudioProcessing(
                    GetEffectiveLocalAudioSettings(local.CurrentItem));
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
        if (item.Kind == MediaItemKind.Station
            && string.Equals(session.Id, "radio", StringComparison.Ordinal))
        {
            return BuildRadioStationPropertiesText(item, session);
        }
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
            playbackLines.Insert(2, $"Stan: {(session.IsPlaying ? "odtwarzanie" : "pauza")}{(session.IsMuted ? ", wyciszono" : string.Empty)}");
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
            if (LocalAudioFileDiscovery.IsVideoFile(localPath))
            {
                technicalLines.Add("Odtwarzanie: ścieżka audio z pliku wideo");
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
            technicalLines.Add($"Częstotliwość próbkowania: {(sampleRate / 1000d).ToString("0.##", CultureInfo.CurrentCulture)} kHz");
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

    private string BuildRadioStationPropertiesText(MediaItem item, DemoMediaSession session)
    {
        var isCurrent = string.Equals(session.CurrentItem.Id, item.Id, StringComparison.Ordinal);
        var sections = new List<string>
        {
            string.Join(Environment.NewLine,
                new string?[]
                {
                    $"Nazwa: {item.Title}",
                    "Rodzaj: stacja radiowa",
                    $"Usługa: {session.DisplayName}",
                    string.IsNullOrWhiteSpace(item.Country) ? null : $"Kraj: {item.Country}",
                    string.IsNullOrWhiteSpace(item.Language) ? null : $"Język: {item.Language}",
                    string.IsNullOrWhiteSpace(item.Tags) ? null : $"Kategorie: {item.Tags}"
                }.Where(value => value is not null).Select(value => value!))
        };

        var applicationLines = new List<string>
        {
            "W aplikacji",
            $"Aktualnie odtwarzana: {(isCurrent ? "tak" : "nie")}",
            $"Ulubiona: {(item.IsFavorite ? "tak" : "nie")}",
            $"W Bibliotece radia: {(item.IsInLibrary ? "tak" : "nie")}"
        };
        if (isCurrent)
        {
            applicationLines.Insert(2, $"Stan: {(session.IsPlaying ? "odtwarzanie" : "pauza")}{(session.IsMuted ? ", wyciszono" : string.Empty)}");
            applicationLines.Insert(3, _radioOutput.BehindLive < TimeSpan.FromSeconds(1)
                ? "Pozycja: na żywo"
                : $"Za transmisją: {CommandRouter.FormatTime(_radioOutput.BehindLive)}");
            applicationLines.Insert(4, $"Głośność: {session.Volume}%");
        }
        sections.Add(string.Join(Environment.NewLine, applicationLines));

        sections.Add(BuildRadioRecordingInformation(item));

        var technicalLines = new List<string> { "Dźwięk" };
        if (!string.IsNullOrWhiteSpace(item.Codec)) technicalLines.Add($"Format strumienia: {item.Codec}");
        if (item.BitrateKbps is int bitrate)
        {
            technicalLines.Add(item.IsBitrateEstimated
                ? $"Bitrate: około {bitrate} kb/s"
                : $"Bitrate: {bitrate} kb/s");
        }
        if (item.SampleRateHz is int sampleRate && sampleRate > 0)
        {
            technicalLines.Add(
                $"Częstotliwość próbkowania: {(sampleRate / 1000d).ToString("0.#", CultureInfo.CurrentCulture)} kHz");
        }
        if (technicalLines.Count > 1) sections.Add(string.Join(Environment.NewLine, technicalLines));

        var sourceLines = new List<string> { "Łącza" };
        if (!string.IsNullOrWhiteSpace(item.Source)) sourceLines.Add($"Adres strumienia: {item.Source}");
        if (!string.IsNullOrWhiteSpace(item.HomepageUri)) sourceLines.Add($"Strona stacji: {item.HomepageUri}");
        if (sourceLines.Count > 1) sections.Add(string.Join(Environment.NewLine, sourceLines));
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private string BuildRadioRecordingInformation(MediaItem item)
    {
        var lines = new List<string> { "Nagrywanie" };
        var manual = _activeManualRadioRecordings.Values
            .Where(active => SameRadioStation(item, active.StationId, active.StreamUrl))
            .OrderBy(active => active.StartedUtc ?? active.RequestedUtc)
            .ToArray();
        var scheduled = _activeScheduledRadioRecordings.Values
            .Where(active => SameRadioStation(item, active.StationId, active.StreamUrl))
            .OrderBy(active => active.StartedUtc)
            .ToArray();

        if (manual.Length == 0 && scheduled.Length == 0)
        {
            lines.Add("Stan: stacja nie jest nagrywana");
            return string.Join(Environment.NewLine, lines);
        }

        var allControls = manual.Select(active => active.Control)
            .Concat(scheduled.Select(active => active.Control))
            .ToArray();
        lines.Add(allControls.Length > 0 && allControls.All(control => control.IsPaused)
            ? "Stan: nagrywanie wstrzymane"
            : allControls.Any(control => control.IsPaused)
                ? "Stan: część nagrań jest wstrzymana"
                : "Stan: nagrywanie trwa");
        for (var index = 0; index < manual.Length; index++)
        {
            var active = manual[index];
            if (manual.Length + scheduled.Length > 1) lines.Add($"Nagranie ręczne {index + 1}");
            lines.Add("Rodzaj: nagrywanie ręczne");
            if (active.StartedUtc is DateTime startedUtc)
                lines.Add($"Rozpoczęto: {FormatRecordingDateTime(startedUtc)}");
            else
                lines.Add("Stan połączenia: przygotowywanie nagrania");
            lines.Add("Zakończenie: ręczne, klawiszem R");
            lines.Add($"Format: {FormatRadioRecordingOutput(active.RecordingFormat, active.RecordingBitrateKbps)}");
            lines.Add("Podział plików: ręcznie klawiszem T");
            if (active.Control.IsPaused) lines.Add("Pauza: włączona");
            if (active.Control.Markers.Count > 0)
                lines.Add($"Punkty pauzy: {active.Control.Markers.Count}");
            if (active.Control.CompletedPaths.Count > 0)
                lines.Add($"Zapisane części: {active.Control.CompletedPaths.Count}");
            var currentPath = active.Control.CurrentPath ?? active.Path;
            if (!string.IsNullOrWhiteSpace(currentPath)) lines.Add($"Bieżący plik: {currentPath}");
        }

        for (var index = 0; index < scheduled.Length; index++)
        {
            var active = scheduled[index];
            if (manual.Length > 0 || scheduled.Length > 1) lines.Add($"Plan {index + 1}");
            lines.Add("Rodzaj: nagrywanie z harmonogramu");
            lines.Add($"Rozpoczęto: {FormatRecordingDateTime(active.StartedUtc)}");
            lines.Add($"Planowane zakończenie: {FormatRecordingDateTime(active.DeadlineUtc)}");
            lines.Add($"Format: {FormatRadioRecordingOutput(active.RecordingFormat, active.RecordingBitrateKbps)}");
            lines.Add(active.SegmentMinutes > 0
                ? $"Podział plików: nowa część co {active.SegmentMinutes} min"
                : "Podział plików: jeden plik");
            if (active.Control.IsPaused) lines.Add("Pauza: włączona");
            if (active.Control.Markers.Count > 0)
                lines.Add($"Punkty pauzy: {active.Control.Markers.Count}");
            if (active.Control.CompletedPaths.Count > 0)
                lines.Add($"Zapisane części: {active.Control.CompletedPaths.Count}");
            if (!string.IsNullOrWhiteSpace(active.Control.CurrentPath))
                lines.Add($"Bieżący plik: {active.Control.CurrentPath}");
            lines.Add($"Planowany folder: {active.RequestedOutputFolder}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatRecordingDateTime(DateTime utc) =>
        utc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture);

    private static string FormatRadioRecordingOutput(
        RadioRecordingFormat format,
        int? bitrateKbps) => format switch
    {
        RadioRecordingFormat.Mp3 => $"MP3, {bitrateKbps ?? 192} kb/s",
        RadioRecordingFormat.Aac => $"M4A, AAC, {bitrateKbps ?? 192} kb/s",
        RadioRecordingFormat.Flac => "FLAC, bezstratny",
        RadioRecordingFormat.Original => "Oryginalny strumień, bez konwersji",
        RadioRecordingFormat.Wav => "WAV, bez kompresji",
        _ => "format nieznany"
    };

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
                    if (item.BitrateKbps is null
                        && item.Duration > TimeSpan.Zero
                        && !LocalAudioFileDiscovery.IsVideoFile(localPath))
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
        else if (string.Equals(ActionSession.Id, "radio", StringComparison.Ordinal)
                 && item.Source is { Length: > 0 } radioSource
                 && (item.BitrateKbps is null || string.IsNullOrWhiteSpace(item.Codec)))
        {
            var metadata = await RadioMediaOutput.TryReadStreamMetadataAsync(
                radioSource,
                TimeSpan.FromSeconds(6));
            if (requestVersion != Interlocked.Read(ref _quickInformationRequestVersion)
                || !string.Equals(ActionItem?.Id, item.Id, StringComparison.Ordinal))
            {
                return;
            }
            if (metadata is not null)
            {
                item.BitrateKbps ??= RadioAudioMetadataRules.NormalizeBitrateKbps(metadata.BitrateKbps);
                if (item.BitrateKbps is not null) item.IsBitrateEstimated = metadata.IsBitrateEstimated;
                item.SampleRateHz ??= metadata.SampleRateHz;
                item.Codec ??= metadata.Codec;
                CaptureRadioState();
                _store.Save(_state);
                UpdatePlaybackStatusBar();
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
        MenuAccessibility.SetPresentation(KeyboardHelpMenuItem, $"Pomoc klawiatury: {state}");
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
            "Ctrl+O dodaje lokalne pliki multimedialne, a Ctrl+Shift+O dodaje do Biblioteki synchronizowany folder wraz z podfolderami. Pliki wideo są odtwarzane jako dźwięk bez otwierania obrazu. " +
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
            Title = "Otwórz lokalne pliki multimedialne",
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
            Title = "Otwórz folder z plikami multimedialnymi",
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
            AnnounceEssential("W folderze nie znaleziono obsługiwanych plików multimedialnych");
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

    public async void ImportRadioPlaylist()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importuj stacje radiowe z playlisty",
            Filter = "Playlisty radia (*.m3u;*.m3u8;*.pls;*.xspf;*.json)|*.m3u;*.m3u8;*.pls;*.xspf;*.json|Wszystkie pliki (*.*)|*.*",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        RadioPlaylistImportResult import;
        try
        {
            import = await Task.Run(() => RadioPlaylistImporter.Import(dialog.FileName));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or System.Text.Json.JsonException
            or System.Xml.XmlException)
        {
            DiagnosticLog.Warning(
                "radio-import",
                $"Nie udało się zaimportować playlisty; błąd {exception.GetType().Name}.");
            AnnounceEssential($"Nie można zaimportować playlisty: {exception.Message}");
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        var merge = RadioLibraryMerge.Apply(_radioItems, import);
        var added = merge.Added;
        var promoted = merge.Promoted;
        var skipped = merge.SkippedEntries;

        if (added.Count == 0 && promoted.Count == 0)
        {
            DiagnosticLog.Info("radio-import", $"Import zakończony bez nowych stacji; pominięto {skipped} pozycji.");
            AnnounceEssential(skipped > 0
                ? "Playlista nie zawiera nowych prawidłowych stacji"
                : "Playlista nie zawiera stacji radiowych");
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        _radioItems.AddRange(added);
        _sessions.FindSession("radio")?.AddItems(added);
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            CaptureCurrentSessionNavigationState();
            HidePlayerForBrowserNavigation();
            _sessions.SelectSession("radio");
        }
        _currentView = "Biblioteka";
        var navigation = GetSessionNavigationState("radio");
        navigation.CurrentView = _currentView;
        navigation.PlayerActive = false;
        RefreshCurrentView(preferredItemId: promoted.FirstOrDefault()?.Id ?? added[0].Id);
        CaptureRadioState();
        _store.Save(_state);
        RestoreMediaListFocusAfterRefresh();
        DiagnosticLog.Info(
            "radio-import",
            $"Dodano {added.Count} nowych stacji; włączono w Bibliotece {promoted.Count}; zachowano dotychczasowe nazwy; pominięto {skipped} pozycji.");
        var message = $"Dodano nowe stacje: {added.Count}. Włączono istniejące w Bibliotece: {promoted.Count}. Zachowano dotychczasowe nazwy";
        if (skipped > 0) message += $". Pominięto: {skipped}";
        _ = Dispatcher.BeginInvoke(() => AnnounceEssential(message), DispatcherPriority.ContextIdle);
    }

    private void UpdateFileMenuForCurrentSession()
    {
        var local = string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal);
        var radio = string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal);
        CurrentSessionMuteMenuItem.IsChecked = _sessions.Current.IsSessionMuted;
        AllSessionsMuteMenuItem.IsChecked = _sessions.AllSessionsMuted;
        UpdatePlaybackAudioMenuPresentation(local);
        OpenLocalFilesMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        OpenLocalFolderMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        ManageLocalSourcesMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        ImportRadioPlaylistMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        AddRadioStationMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        FileActionsSeparator.Visibility = local || radio ? Visibility.Visible : Visibility.Collapsed;
        FoldersViewMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        AllLocalFilesViewMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        CustomLocalOrderViewMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        ActiveRadioRecordingsViewMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        RadioRecognitionHistoryViewMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        RefreshLocalLibraryMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        RenameLocalFileMainMenuItem.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        MenuAccessibility.SetPresentation(
            RenameLibraryItemMainMenuItem,
            radio ? "Edytuj nazwę i adres stacji…" : "Zmień nazwę w Bibliotece…");
        var movableView = !_playerViewActive
            && (string.Equals(_currentView, "Ulubione", StringComparison.Ordinal)
                || !radio && string.Equals(_currentView, "Kolejka", StringComparison.Ordinal)
                || local && string.Equals(_currentView, CustomLocalOrderViewName, StringComparison.Ordinal)
                || TryGetPlaylistIdFromView(_currentView, out _));
        MoveItemUpMainMenuItem.Visibility = movableView ? Visibility.Visible : Visibility.Collapsed;
        MoveItemDownMainMenuItem.Visibility = movableView ? Visibility.Visible : Visibility.Collapsed;

        PlaylistsViewMenuItem.Visibility = Visibility.Visible;
        PlaylistsViewMenuItem.Header = "Playlisty";
        PlaylistsViewMenuItem.InputGestureText = "Ctrl+P";
        AutomationProperties.SetName(PlaylistsViewMenuItem, "Playlisty");
        var presetsAvailable = CurrentSessionSupportsPresets();
        RadioPresetsViewMenuItem.Visibility = presetsAvailable ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(
            RadioPresetsViewMenuItem,
            $"Presety, {_sessions.Current.DisplayName}");
        RadioAssignPresetMenuItem.Visibility = presetsAvailable ? Visibility.Visible : Visibility.Collapsed;
        RadioAssignPresetMenuItem.Header = "Utwórz lub przypisz preset…";
        RadioAssignPresetMenuItem.InputGestureText = "Ctrl+Alt+Shift+P";
        AutomationProperties.SetName(
            RadioAssignPresetMenuItem,
            $"Utwórz lub przypisz preset, {_sessions.Current.DisplayName}");
        AlbumsViewMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        QueueViewMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        BookmarksViewMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        BrowserPlaylistsButton.Visibility = Visibility.Visible;
        BrowserPlaylistsButton.Content = "Zmień _playlisty…";
        AutomationProperties.SetName(
            BrowserPlaylistsButton,
            "Zmień przynależność do playlist, Ctrl+Shift+P");

        RadioRecordingMenuItem.Visibility = radio && _playerViewActive ? Visibility.Visible : Visibility.Collapsed;
        PauseRadioRecordingMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        var pauseActionItem = RadioScheduleActionStation();
        var pauseControls = pauseActionItem is null
            ? Array.Empty<RadioRecordingControl>()
            : RadioRecordingControlsFor(pauseActionItem);
        PauseRadioRecordingMenuItem.IsEnabled = pauseControls.Count > 0;
        SetContextMenuItemPresentation(
            PauseRadioRecordingMenuItem,
            pauseControls.Count > 0 && pauseControls.All(control => control.IsPaused)
                ? "Wznów wybrane nagranie"
                : "Wstrzymaj wybrane nagranie",
            "Shift+Spacja");
        SplitRadioRecordingMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        SplitRadioRecordingMenuItem.IsEnabled = ResolveManualRecordingForSplit() is { Control.IsReady: true };
        StopAllRadioRecordingsMenuItem.Visibility = Visibility.Visible;
        StopAllRadioRecordingsMenuItem.IsEnabled =
            _activeManualRadioRecordings.Count + _activeScheduledRadioRecordings.Count > 0;
        var currentRadioManuallyRecording = radio
            && _sessions.Current.HasCurrentItem
            && IsRadioStationManuallyRecording(_sessions.Current.CurrentItem);
        RadioRecordingMenuItem.Header = currentRadioManuallyRecording
            ? "Zakończ nagrywanie radia"
            : "Rozpocznij nagrywanie radia";
        AutomationProperties.SetName(
            RadioRecordingMenuItem,
            $"{(currentRadioManuallyRecording ? "Zakończ" : "Rozpocznij")} nagrywanie radia w odtwarzaczu radia");
        RadioAddScheduleMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        RadioAddScheduleMenuItem.IsEnabled = radio && RadioScheduleActionStation() is not null;
        RadioSchedulesMenuItem.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        RadioBeforeRecognitionSeparator.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        RecognizeRadioTrackMenuItem.Visibility = radio && _playerViewActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        RecognizeRadioTrackMenuItem.IsEnabled = radio && _radioOutput.LoadedItemId is not null;
        MonitorRadioRecognitionMenuItem.Visibility = radio && _playerViewActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        MonitorRadioRecognitionMenuItem.IsChecked = _radioRecognitionMonitoring;
        MonitorRadioRecognitionMenuItem.Header = _radioRecognitionMonitoring
            ? "Obserwuj rozpoznawanie: włączone"
            : "Obserwuj rozpoznawanie: wyłączone";
        AutomationProperties.SetName(
            MonitorRadioRecognitionMenuItem,
            $"Obserwowanie rozpoznawania utworów: {(_radioRecognitionMonitoring ? "włączone" : "wyłączone")}");
        PlaybackAfterRecordingSeparator.Visibility = radio ? Visibility.Visible : Visibility.Collapsed;
        PlaybackAddBookmarkMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackAddNamedBookmarkMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackPreviousBookmarkMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackNextBookmarkMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackBeforeSeekSeparator.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackSeekTimeMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackSeekPercentageMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackBeforeRateSeparator.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackRateDownMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackRateUpMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackRateResetMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackBeforeInformationSeparator.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
        PlaybackItemOptionsMenuItem.Visibility = radio ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdatePlaybackAudioMenuPresentation(bool local)
    {
        var visibility = local ? Visibility.Visible : Visibility.Collapsed;
        PlaybackAudioProcessingSeparator.Visibility = visibility;
        PlayerAudioProcessingSeparator.Visibility = visibility;

        UpdatePlaybackAudioMenuSet(
            LoudnessNormalizationMenuItem,
            SmoothTrackTransitionsMenuItem,
            InterTrackSilenceMenuItem,
            [
                (InterTrackSilenceNoneMenuItem, 0),
                (InterTrackSilenceHalfSecondMenuItem, 500),
                (InterTrackSilenceOneSecondMenuItem, 1000),
                (InterTrackSilenceTwoSecondsMenuItem, 2000),
                (InterTrackSilenceThreeSecondsMenuItem, 3000),
                (InterTrackSilenceFiveSecondsMenuItem, 5000)
            ],
            visibility);
        UpdatePlaybackAudioMenuSet(
            PlayerLoudnessNormalizationMenuItem,
            PlayerSmoothTrackTransitionsMenuItem,
            PlayerInterTrackSilenceMenuItem,
            [
                (PlayerInterTrackSilenceNoneMenuItem, 0),
                (PlayerInterTrackSilenceHalfSecondMenuItem, 500),
                (PlayerInterTrackSilenceOneSecondMenuItem, 1000),
                (PlayerInterTrackSilenceTwoSecondsMenuItem, 2000),
                (PlayerInterTrackSilenceThreeSecondsMenuItem, 3000),
                (PlayerInterTrackSilenceFiveSecondsMenuItem, 5000)
            ],
            visibility);
    }

    private void UpdatePlaybackAudioMenuSet(
        MenuItem loudnessItem,
        MenuItem transitionsItem,
        MenuItem silenceItem,
        IReadOnlyList<(MenuItem Item, int Milliseconds)> silenceChoices,
        Visibility visibility)
    {
        var audio = _state.Settings.Audio;
        loudnessItem.Visibility = visibility;
        transitionsItem.Visibility = visibility;
        silenceItem.Visibility = visibility;
        loudnessItem.IsChecked = audio.LoudnessNormalizationEnabled;
        transitionsItem.IsChecked = audio.SmoothTrackTransitionsEnabled;
        MenuAccessibility.SetPresentation(
            loudnessItem,
            $"Globalna normalizacja głośności lokalnych utworów: {(audio.LoudnessNormalizationEnabled ? "włączona" : "wyłączona")}");
        MenuAccessibility.SetPresentation(
            transitionsItem,
            $"Globalne łagodne przejścia między utworami: {(audio.SmoothTrackTransitionsEnabled ? "włączone" : "wyłączone")}");
        MenuAccessibility.SetPresentation(
            silenceItem,
            $"Globalna cisza między utworami: {PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(audio.InterTrackSilenceMilliseconds)}");
        foreach (var choice in silenceChoices)
        {
            choice.Item.IsChecked = choice.Milliseconds == audio.InterTrackSilenceMilliseconds;
        }
    }

    private void ToggleLoudnessNormalization()
    {
        _state.Settings.Audio.LoudnessNormalizationEnabled =
            !_state.Settings.Audio.LoudnessNormalizationEnabled;
        ApplyPlaybackAudioSetting(
            $"Globalna normalizacja głośności lokalnych utworów: {(_state.Settings.Audio.LoudnessNormalizationEnabled ? "włączona" : "wyłączona")}");
    }

    private void ToggleSmoothTrackTransitions()
    {
        _state.Settings.Audio.SmoothTrackTransitionsEnabled =
            !_state.Settings.Audio.SmoothTrackTransitionsEnabled;
        ApplyPlaybackAudioSetting(
            $"Globalne łagodne przejścia między utworami: {(_state.Settings.Audio.SmoothTrackTransitionsEnabled ? "włączone" : "wyłączone")}");
    }

    private void CycleInterTrackSilence()
    {
        var choices = PlaybackAudioSettingsRules.SupportedInterTrackSilenceMilliseconds;
        var currentIndex = -1;
        for (var index = 0; index < choices.Count; index++)
        {
            if (choices[index] == _state.Settings.Audio.InterTrackSilenceMilliseconds)
            {
                currentIndex = index;
                break;
            }
        }
        SetInterTrackSilence(choices[(currentIndex + 1) % choices.Count]);
    }

    private void SetInterTrackSilence(int milliseconds)
    {
        if (!PlaybackAudioSettingsRules.IsSupportedSilence(milliseconds)) return;
        _state.Settings.Audio.InterTrackSilenceMilliseconds = milliseconds;
        ApplyPlaybackAudioSetting(
            $"Globalna cisza między lokalnymi utworami: {PlaybackAudioSettingsRules.GetInterTrackSilenceLabel(milliseconds)}");
    }

    private void ApplyPlaybackAudioSetting(string announcement)
    {
        ApplyEffectiveAudioProcessingForCurrentLocalItem();
        UpdatePlaybackAudioMenuPresentation(
            string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal));
        var saved = TrySaveLocalMediaState(false);
        Announce(saved
            ? announcement
            : $"{announcement}. Zmiana działa teraz, ale nie została zapisana");
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
                BitrateKbps = LocalAudioFileDiscovery.IsVideoFile(saved.Path) ? null : saved.BitrateKbps,
                IsBitrateEstimated = !LocalAudioFileDiscovery.IsVideoFile(saved.Path) && saved.IsBitrateEstimated,
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
                BitrateKbps = RadioAudioMetadataRules.NormalizeBitrateKbps(saved.BitrateKbps),
                IsBitrateEstimated = saved.IsBitrateEstimated,
                SampleRateHz = saved.SampleRateHz,
                IsFavorite = saved.IsFavorite,
                IsInLibrary = saved.IsInLibrary,
                IsAvailable = true,
                IsInQueue = false,
                IsPlayNext = false
            });
        }
    }

    private void CaptureRadioState()
    {
        var radio = _sessions?.FindSession("radio");
        var savedById = (_state.Radio.Stations ?? [])
            .GroupBy(station => station.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var savedByStream = (_state.Radio.Stations ?? [])
            .Where(station => !string.IsNullOrWhiteSpace(station.StreamUrl))
            .GroupBy(station => station.StreamUrl, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        if (radio is not null)
        {
            _state.Radio.CurrentItemId = radio.HasCurrentItem ? radio.CurrentItem.Id : null;
            _state.Radio.Volume = radio.Volume;
        }
        _state.Radio.Stations = _radioItems.Select(item =>
        {
            savedById.TryGetValue(item.Id, out var saved);
            if (saved is null && item.Source is { Length: > 0 })
                savedByStream.TryGetValue(item.Source, out saved);
            var isCurrent = radio?.HasCurrentItem == true
                && SameRadioStation(item, radio.CurrentItem);
            return new RadioStationSettings
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
                BitrateKbps = RadioAudioMetadataRules.NormalizeBitrateKbps(item.BitrateKbps),
                IsBitrateEstimated = item.IsBitrateEstimated,
                SampleRateHz = item.SampleRateHz,
                Volume = isCurrent ? radio!.Volume : saved?.Volume,
                HasCustomTitle = item.HasCustomTitle,
                IsFavorite = item.IsFavorite,
                IsInLibrary = item.IsInLibrary,
                IsInQueue = false,
                IsPlayNext = false,
                IsCustom = string.IsNullOrWhiteSpace(item.ExternalId)
            };
        }).ToList();
    }

    private int? RadioStationVolumeOverride(MediaItem item)
    {
        var saved = _state.Radio.Stations.FirstOrDefault(station =>
            string.Equals(station.Id, item.Id, StringComparison.Ordinal))
            ?? _state.Radio.Stations.FirstOrDefault(station =>
                item.Source is { Length: > 0 }
                && string.Equals(station.StreamUrl, item.Source, StringComparison.OrdinalIgnoreCase));
        return saved?.Volume ?? _state.Radio.Volume;
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
            existing.BitrateKbps = RadioAudioMetadataRules.NormalizeBitrateKbps(candidate.BitrateKbps);
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
                LoudnessNormalizationOverride = previous?.LoudnessNormalizationOverride,
                SmoothTrackTransitionsOverride = previous?.SmoothTrackTransitionsOverride,
                InterTrackSilenceMillisecondsOverride =
                    previous?.InterTrackSilenceMillisecondsOverride,
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

    private PlaybackAudioSettings GetEffectiveLocalAudioSettings(MediaItem item) =>
        LocalPlaybackAudioSettingsResolver.Resolve(
            _state.Settings.Audio,
            item.Source,
            FindLocalItemSettings(item),
            _state.LocalMedia.FolderPlaybackOptions);

    private void ApplyEffectiveAudioProcessingForCurrentLocalItem()
    {
        var local = _sessions?.FindSession("local");
        _localOutput.ConfigureAudioProcessing(
            local is { HasCurrentItem: true }
                ? GetEffectiveLocalAudioSettings(local.CurrentItem)
                : _state.Settings.Audio);
    }

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
            _prefixService = new GlobalPrefixService(
                handle,
                HandleGlobalChord,
                PrefixActivated,
                HandleFocusedDirectShortcut);
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
            _ => false,
            volumeOverride: RadioStationVolumeOverride);
        if (radio.HasItems)
        {
            var restoredRadioId = previousRadioItemId ?? _state.Radio.CurrentItemId;
            var restoredRadio = radio.Items.FirstOrDefault(item => item.Id == restoredRadioId);
            if (restoredRadio is not null) radio.SelectItem(restoredRadio);
        }
        radio.SetVolume(radio.HasCurrentItem
            ? RadioStationVolumeOverride(radio.CurrentItem) ?? _state.Radio.Volume
            : previousRadio?.Volume ?? _state.Radio.Volume);
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
                e.Item.BitrateKbps = LocalAudioFileDiscovery.IsVideoFile(path)
                    ? null
                    : LocalAudioFileDiscovery.EstimateBitrateKbps(
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
        var completedAudioSettings = GetEffectiveLocalAudioSettings(e.Item);
        _localOutput.BeginAutomaticTrackContinuation();
        var nextItem = localSession?.ContinueAfterPlaybackEnded(e.Item);
        if (nextItem is null) _localOutput.CancelAutomaticTrackContinuation();
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
            : completedAudioSettings.InterTrackSilenceMilliseconds > 0
                ? $"Następny utwór po ciszy: {nextItem.Title}"
                : $"Odtwarzanie: {nextItem.Title}");
    }

    private void RadioOutput_PlaybackPreparing(object? sender, MediaPlaybackPreparingEventArgs e)
    {
        if (_state.Settings.Messages.LoadingMessages)
        {
            Announce(e.Item.Title);
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
        UpdateWindowTitle();
        if (_radioRecognitionMonitoring)
            _nextRadioRecognitionUtc = DateTime.UtcNow.AddSeconds(12);
        if (e.Item.BitrateKbps is null || string.IsNullOrWhiteSpace(e.Item.Codec))
        {
            _ = EnrichRadioMetadataAfterPlaybackStartedAsync(e.Item);
        }
    }

    private void RadioOutput_NowPlayingChanged(
        object? sender,
        RadioNowPlayingChangedEventArgs e)
    {
        if (_isClosing) return;
        if (e.StreamTitle is null)
        {
            _radioNowPlayingItemId = e.Item.Id;
            _radioNowPlayingTitle = null;
        }
        else
        {
            _radioNowPlayingItemId = e.Item.Id;
            _radioNowPlayingTitle = e.StreamTitle;
        }

        var session = _sessions.Current;
        if (string.Equals(session.Id, "radio", StringComparison.Ordinal)
            && string.Equals(session.CurrentItem.Id, e.Item.Id, StringComparison.Ordinal))
        {
            UpdateWindowTitle();
        }
    }

    private async Task EnrichRadioMetadataAfterPlaybackStartedAsync(MediaItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Source)) return;
        var metadata = await RadioMediaOutput.TryReadStreamMetadataAsync(
            item.Source,
            TimeSpan.FromSeconds(8));
        if (_isClosing || metadata is null
            || _radioItems.All(candidate => !string.Equals(candidate.Id, item.Id, StringComparison.Ordinal)))
        {
            return;
        }
        item.BitrateKbps ??= RadioAudioMetadataRules.NormalizeBitrateKbps(metadata.BitrateKbps);
        if (item.BitrateKbps is not null) item.IsBitrateEstimated = metadata.IsBitrateEstimated;
        item.SampleRateHz ??= metadata.SampleRateHz;
        item.Codec ??= metadata.Codec;
        CaptureRadioState();
        _store.Save(_state);
        UpdatePlaybackStatusBar();
    }

    private void RadioOutput_PlaybackFailed(object? sender, MediaOutputFailedEventArgs e)
    {
        var radio = _sessions.FindSession("radio");
        if (radio is not null) radio.StopPlayback();
        if (_state.Settings.Messages.ErrorMessages) AnnounceEssential(e.Message);
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
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
                Announce($"Folder {folderContext.FolderLabel} nie zawiera dostępnych plików multimedialnych");
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
        if (CommandIds.TryParseRadioPreset(commandId, out var radioPresetSlot))
        {
            ActivatePreset(radioPresetSlot);
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ViewRadioPresets)
        {
            ShowPresets();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.AssignRadioPreset)
        {
            ShowPresetAssignment();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ToggleLoudnessNormalization)
        {
            ToggleLoudnessNormalization();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ToggleSmoothTrackTransitions)
        {
            ToggleSmoothTrackTransitions();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.CycleInterTrackSilence)
        {
            CycleInterTrackSilence();
            return new CommandExecutionResult(true);
        }
        if (commandId is CommandIds.ViewFolders
                or CommandIds.ViewAllLocalFiles
                or CommandIds.ViewCustomLocalOrder
                or CommandIds.RefreshLocalLibrary
                or CommandIds.ManageLocalSources
            && !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            Announce("To polecenie jest dostępne tylko w sesji Pliki lokalne");
            return new CommandExecutionResult(false);
        }
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
        if (commandId == CommandIds.ToggleRadioRecordingPause)
        {
            ToggleSelectedRadioRecordingPause();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.SplitRadioRecording)
        {
            SplitSelectedManualRadioRecording();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.StopAllRadioRecordings)
        {
            StopAllRadioRecordings();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.AddRadioSchedule)
        {
            AddRadioScheduleForCurrentContext();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ManageRadioSchedules)
        {
            ShowRadioSchedules();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ViewActiveRadioRecordings)
        {
            if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
            {
                Announce("Widok nagrywanych stacji jest dostępny w sesji Radio internetowe");
                return new CommandExecutionResult(false);
            }
            NavigateTo(ActiveRadioRecordingsViewName);
            PrepareViewFocusContext("Nagrywane, Radio internetowe");
            RestoreMediaListFocusAfterRefresh();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.RadioJumpLive)
        {
            if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
            {
                Announce("Powrót na żywo jest dostępny w odtwarzaczu radia");
                return new CommandExecutionResult(false);
            }
            if (IsCurrentRadioStationRecording())
            {
                Announce("Timeshift jest zablokowany podczas nagrywania tej stacji");
                return new CommandExecutionResult(true);
            }
            _radioOutput.JumpToLive();
            Announce("Na żywo");
            UpdatePlayerView();
            UpdatePlaybackStatusBar();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.RecognizeRadioTrack)
        {
            _ = RecognizeCurrentRadioTrackAsync(automatic: false);
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ToggleRadioRecognitionMonitoring)
        {
            ToggleRadioRecognitionMonitoring();
            return new CommandExecutionResult(true);
        }
        if (commandId == CommandIds.ViewRadioRecognitionHistory)
        {
            ShowRadioRecognitionHistory();
            return new CommandExecutionResult(true);
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            if (IsRadioTimeshiftCommand(commandId) && IsCurrentRadioStationRecording())
            {
                Announce("Timeshift jest zablokowany podczas nagrywania tej stacji");
                return new CommandExecutionResult(true);
            }
            if (commandId is CommandIds.AddQueue
                or CommandIds.TogglePlayNext
                or CommandIds.ViewQueue
                or CommandIds.ViewAlbums)
            {
                Announce("Ta funkcja nie jest dostępna w Radiu internetowym");
                return new CommandExecutionResult(true);
            }
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
        var changedItems = changesListMembership
            ? ActionItems
                .DistinctBy(item => item.Id, StringComparer.Ordinal)
                .ToArray()
            : [];
        if (changesListMembership)
        {
            var selectedRows = _playerViewActive
                ? 1
                : MediaList.SelectedItems
                    .OfType<MediaItemRow>()
                    .Count(row => row.PlaylistId is null);
            DiagnosticLog.Info(
                "selection",
                $"Polecenie {commandId}; sesja: {_sessions.Current.Id}; widok: {_currentView}; " +
                $"widoczne wiersze: {MediaList.Items.Count}; zaznaczone wiersze: {selectedRows}; " +
                $"unikatowe elementy działania: {changedItems.Length}.");
        }
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
        var actionItemsBeforeExecution = _actionItemsOverride;
        if (changesListMembership) _actionItemsOverride = changedItems;
        try
        {
            result = folderContext is not null && IsFolderCollectionToggleCommand(commandId)
                ? ExecuteFolderCollectionToggle(commandId, folderContext.Items)
                : _router.Execute(commandId);
        }
        finally
        {
            _actionItemsOverride = actionItemsBeforeExecution;
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
        if (commandId is CommandIds.ToggleMuteCurrentSession or CommandIds.ToggleMuteAllSessions)
            UpdateFileMenuForCurrentSession();
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
        UpdateFileMenuForCurrentSession();
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

        if (string.Equals(_currentView, ActiveRadioRecordingsViewName, StringComparison.Ordinal))
        {
            if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
            {
                _currentView = "Biblioteka";
                GetSessionNavigationState(_sessions.Current.Id).CurrentView = _currentView;
                RefreshCurrentView(fallbackIndex, preferredItemId);
                return;
            }
            _unfilteredItems = CreateActiveRadioRecordingRows();
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

    private List<MediaItemRow> CreateActiveRadioRecordingRows()
    {
        var stationKeys = _activeManualRadioRecordings.Values
            .Select(active => (active.StationId, active.StationName, active.StreamUrl, active.RequestedUtc))
            .Concat(_activeScheduledRadioRecordings.Values.Select(active =>
                (active.StationId, active.StationName, active.StreamUrl, active.StartedUtc)))
            .OrderBy(entry => entry.Item4)
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.StationId)
                ? entry.StreamUrl
                : entry.StationId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var radioItems = _sessions.Current.Items.ToArray();
        return stationKeys.Select(entry =>
        {
            var item = radioItems.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, entry.StationId, StringComparison.Ordinal)
                    || candidate.Source is { Length: > 0 }
                        && string.Equals(candidate.Source, entry.StreamUrl, StringComparison.OrdinalIgnoreCase))
                ?? new MediaItem
                {
                    Id = entry.StationId,
                    Title = entry.StationName,
                    Kind = MediaItemKind.Station,
                    Source = entry.StreamUrl,
                    PublicUri = entry.StreamUrl,
                    IsAvailable = true,
                    IsInLibrary = false
                };
            return new MediaItemRow(item, FormatListItem(item), item.PrimaryText);
        }).ToList();
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
                var durationTicks = availableItems
                    .Where(item => item.Kind != MediaItemKind.Station)
                    .Aggregate(
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
                return new MediaItemRow(
                    item,
                    PlaylistPresentation.BuildLabel(
                        playlist.Name,
                        playlist.ItemIds.Count,
                        availableItems.Length,
                        availableItems),
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
        ClearTrackedNativeModifiers();
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

    private bool TryResolveSelectedPlaylistContents(out PlaylistContentsActionContext context)
    {
        context = null!;
        if (_playerViewActive
            || (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not { } playlistId
            || new PlaylistIndex(_state.Playlists).Find(playlistId) is not { } playlist
            || !string.Equals(playlist.SessionId, _sessions.Current.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var itemsById = _sessions.Current.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var items = playlist.ItemIds
            .Select(itemId => itemsById.GetValueOrDefault(itemId))
            .Where(item => item?.Kind is MediaItemKind.Track or MediaItemKind.Station)
            .Select(item => item!)
            .ToArray();
        context = new PlaylistContentsActionContext(playlist, items);
        return true;
    }

    private void PlaySelectedPlaylistNow()
    {
        if (!TryResolveSelectedPlaylistContents(out var context) || context.Items.Count == 0)
        {
            Announce("Ta playlista nie zawiera obecnie dostępnych elementów do odtworzenia");
            return;
        }

        NavigateTo(PlaylistContentsView(context.Playlist.Id));
        PrepareViewFocusContext($"Playlista, {context.Playlist.Name}");
        var session = _sessions.Current;
        var first = context.Items[0];
        PreparePlaybackContextForCurrentView(session, first);
        session.Play(first);
        RecordPlayback(session, first);
        if (string.Equals(session.Id, "radio", StringComparison.Ordinal)) CaptureRadioState();
        else if (string.Equals(session.Id, "local", StringComparison.Ordinal)) TrySaveLocalMediaState(false);
        else _store.Save(_state);
        RefreshPlaybackIndicators();
        ShowPlayerView();
    }

    private void ExecuteSelectedPlaylistContentsCommand(string commandId)
    {
        if (!TryResolveSelectedPlaylistContents(out var context) || context.Items.Count == 0)
        {
            Announce("Ta playlista nie zawiera obecnie dostępnych elementów");
            return;
        }

        var previousOverride = _actionItemsOverride;
        _actionItemsOverride = context.Items;
        try
        {
            ExecuteCommand(commandId);
        }
        finally
        {
            _actionItemsOverride = previousOverride;
        }
    }

    private void ShowSelectedPlaylistProperties()
    {
        if (!TryResolveSelectedPlaylistContents(out var context)) return;
        var label = PlaylistPresentation.BuildLabel(
            context.Playlist.Name,
            context.Playlist.ItemIds.Count,
            context.Items.Count,
            context.Items);
        var text = string.Join(
            Environment.NewLine,
            "Playlista",
            $"Nazwa: {context.Playlist.Name}",
            $"Sesja: {_sessions.Current.DisplayName}",
            $"Podsumowanie: {label}");
        var dialog = new InformationWindow(text, []) { Owner = this };
        dialog.ShowDialog();
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
        or ActiveRadioRecordingsViewName
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
        ApplyEffectiveAudioProcessingForCurrentLocalItem();
        ClearDisabledLocalResumePositions();
        _playbackHistory = new PlaybackHistory(_state.PlaybackHistory);
        _bookmarkIndex = new BookmarkIndex(_state.Bookmarks);
        _store.Save(_state);
        ApplyDetailedHints();
        RebuildCore();
        RearmRadioWakeTimer();
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
        foreach (var schedule in _state.Radio.RecordingSchedules.Where(schedule =>
                     string.Equals(schedule.StationId, item.Id, StringComparison.Ordinal)))
        {
            schedule.StationName = item.Title;
            schedule.StreamUrl = item.Source;
        }
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
            Announce("Nagrywanie ręczne jest dostępne dla wybranej stacji radia internetowego");
            return;
        }

        var station = !_playerViewActive && ActionItem?.Kind == MediaItemKind.Station
            ? ActionItem
            : _sessions.Current.CurrentItem;
        if (station.Kind != MediaItemKind.Station || string.IsNullOrWhiteSpace(station.Source))
        {
            Announce("Wybrany element nie jest stacją radiową");
            return;
        }

        var manual = _activeManualRadioRecordings.Values
            .Where(active => SameRadioStation(station, active.StationId, active.StreamUrl))
            .ToArray();
        var scheduled = _activeScheduledRadioRecordings.Values
            .Where(active => SameRadioStation(station, active.StationId, active.StreamUrl))
            .ToArray();
        if (manual.Length + scheduled.Length > 0)
        {
            foreach (var active in manual)
            {
                active.Control.RequestStop();
                active.Cancellation.Cancel();
            }
            foreach (var active in scheduled)
            {
                active.Control.RequestStop();
                AdvanceStoppedScheduledOccurrence(active, "zatrzymane dla wybranej stacji");
                active.Cancellation.Cancel();
            }
            if (scheduled.Length > 0)
            {
                _store.Save(_state);
                RearmRadioWakeTimer();
            }
            AnnounceEssential($"Zatrzymuję nagrywanie: {station.Title}");
            return;
        }

        StartManualRadioRecording(station);
    }

    private void ToggleSelectedRadioRecordingPause()
    {
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            Announce("Pauza nagrywania jest dostępna w sesji Radio internetowe");
            return;
        }

        var station = _playerViewActive && _sessions.Current.HasCurrentItem
            ? _sessions.Current.CurrentItem
            : ActionItem;
        if (station?.Kind != MediaItemKind.Station)
        {
            Announce("Wybierz nagrywaną stację");
            return;
        }

        var controls = RadioRecordingControlsFor(station);
        if (controls.Count == 0)
        {
            Announce($"Stacja nie jest nagrywana: {station.Title}");
            return;
        }

        var ready = controls.Where(control => control.IsReady).ToArray();
        if (ready.Length == 0)
        {
            Announce("Nagranie jeszcze się uruchamia");
            return;
        }

        var pausable = ready.Where(control => control.CanPause).ToArray();
        if (pausable.Length == 0)
        {
            Announce("Pauza nie jest dostępna przy zapisie oryginalnego strumienia bez konwersji");
            return;
        }

        var pause = pausable.Any(control => !control.IsPaused);
        var changes = pausable.Select(control => control.SetPaused(pause)).ToArray();
        if (changes.Any(change => change.Kind == RadioRecordingPauseChangeKind.NotReady))
        {
            Announce("Trwa finalizowanie albo rozpoczynanie części nagrania");
            return;
        }
        var failures = changes.Where(change => change.Kind == RadioRecordingPauseChangeKind.Failed).ToArray();
        if (failures.Length > 0)
        {
            Announce($"Nie udało się zmienić pauzy nagrania: {failures[0].Error}");
            return;
        }

        RefreshRadioRecordingPresentation();
        if (pause)
        {
            var position = changes
                .Where(change => change.Kind == RadioRecordingPauseChangeKind.Paused)
                .Select(change => change.Position)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Min();
            var suffix = ready.Length > pausable.Length
                ? ". Zapis oryginalnego strumienia pozostał aktywny"
                : string.Empty;
            AnnounceEssential($"Wstrzymano nagrywanie: {station.Title}, {CommandRouter.FormatTime(position)}{suffix}");
        }
        else
        {
            AnnounceEssential($"Wznowiono nagrywanie: {station.Title}");
        }
    }

    private async void SplitSelectedManualRadioRecording()
    {
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            Announce("Podział nagrania jest dostępny w sesji Radio internetowe");
            return;
        }

        var active = ResolveManualRecordingForSplit();
        if (active is null)
        {
            Announce(_activeManualRadioRecordings.Count > 0
                ? "Wybrana stacja nie jest nagrywana ręcznie. Wybierz właściwą stację w widoku Nagrywane"
                : "Brak ręcznego nagrania do podziału. Nagrywanie ręczne uruchamia klawisz R");
            return;
        }
        if (active.IsSplitting)
        {
            Announce("Trwa już rozpoczynanie nowej części nagrania");
            return;
        }
        if (!active.Control.IsReady)
        {
            Announce("Nagranie jeszcze się uruchamia");
            return;
        }

        active.IsSplitting = true;
        AnnounceEssential($"Zapisuję bieżącą część nagrania: {active.StationName}");
        RadioRecordingSplitChange change;
        try
        {
            change = await Task.Run(active.Control.SplitRecording);
        }
        catch (Exception exception)
        {
            change = new RadioRecordingSplitChange(
                RadioRecordingSplitChangeKind.Failed,
                Error: exception.Message);
        }
        finally
        {
            active.IsSplitting = false;
        }

        if (change.Kind == RadioRecordingSplitChangeKind.Split
            && !string.IsNullOrWhiteSpace(change.CurrentPath))
        {
            active.Path = change.CurrentPath;
            RefreshRadioRecordingPresentation();
            var partNumber = active.Control.CompletedPaths.Count + 1;
            AnnounceEssential(
                $"Rozpoczęto część {partNumber}: {active.StationName}, {Path.GetFileName(change.CurrentPath)}");
            return;
        }

        if (change.Kind == RadioRecordingSplitChangeKind.NotReady)
        {
            Announce("Nagranie nie jest jeszcze gotowe do podziału");
            return;
        }

        if (change.Kind == RadioRecordingSplitChangeKind.StopRequested)
        {
            RefreshRadioRecordingPresentation();
            return;
        }

        active.Cancellation.Cancel();
        AnnounceEssential($"Nie udało się rozpocząć nowej części nagrania: {change.Error}");
    }

    private ActiveManualRadioRecording? ResolveManualRecordingForSplit()
    {
        MediaItem? station = null;
        if (_playerViewActive && _sessions.Current.HasCurrentItem)
            station = _sessions.Current.CurrentItem;
        else if (ActionItem?.Kind == MediaItemKind.Station)
            station = ActionItem;

        if (station is not null)
        {
            return _activeManualRadioRecordings.Values.FirstOrDefault(active =>
                SameRadioStation(station, active.StationId, active.StreamUrl));
        }
        return _activeManualRadioRecordings.Count == 1
            ? _activeManualRadioRecordings.Values.Single()
            : null;
    }

    private IReadOnlyList<RadioRecordingControl> RadioRecordingControlsFor(MediaItem station) =>
        _activeManualRadioRecordings.Values
            .Where(active => SameRadioStation(station, active.StationId, active.StreamUrl))
            .Select(active => active.Control)
            .Concat(_activeScheduledRadioRecordings.Values
                .Where(active => SameRadioStation(station, active.StationId, active.StreamUrl))
                .Select(active => active.Control))
            .ToArray();

    private void StartManualRadioRecording(MediaItem station)
    {
        var id = Guid.NewGuid().ToString("N");
        var cancellation = new CancellationTokenSource();
        var control = new RadioRecordingControl();
        var active = new ActiveManualRadioRecording(
            id,
            station.Id,
            station.Title,
            station.Source!,
            DateTime.UtcNow,
            _state.Radio.RecordingFormat,
            _state.Radio.RecordingBitrateKbps,
            cancellation,
            control);
        _activeManualRadioRecordings[id] = active;
        active.Task = Task.Run(() => ManualRadioRecorder.RecordAsync(
            station,
            ResolveRadioRecordingsFolder(),
            ResolveRadioRecordingsFolder(),
            ResolveSystemRadioRecordingsFolder(),
            active.RecordingFormat,
            active.RecordingBitrateKbps,
            active.Control,
            path => Dispatcher.BeginInvoke(() =>
            {
                if (!_activeManualRadioRecordings.ContainsKey(id) || _isClosing) return;
                active.Path = path;
                active.StartedUtc = DateTime.UtcNow;
                RefreshRadioRecordingPresentation();
                DiagnosticLog.Info("radio-recording", $"Ręczne nagrywanie działa w tle: {active.StationName}; plik {path}.");
            }, DispatcherPriority.Background),
            cancellation.Token));
        RefreshRadioRecordingPresentation();
        AnnounceEssential($"Rozpoczynam nagrywanie w tle: {station.Title}");
        _ = CompleteManualRadioRecordingAsync(active);
    }

    private void StopAllRadioRecordings()
    {
        var manualRecordings = _activeManualRadioRecordings.Values.ToArray();
        var scheduledRecordings = _activeScheduledRadioRecordings.Values.ToArray();
        var count = manualRecordings.Length + scheduledRecordings.Length;
        if (count == 0)
        {
            AnnounceEssential("Brak trwających nagrań");
            return;
        }

        if (count > 1)
        {
            var answer = System.Windows.MessageBox.Show(
                this,
                $"Trwają {count} nagrania. Zatrzymać i zapisać wszystkie odebrane fragmenty? Harmonogramy cykliczne pozostaną aktywne dla następnych terminów.",
                "Zatrzymywanie nagrań",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                RestoreItemActionFocus();
                return;
            }
        }

        foreach (var active in manualRecordings)
        {
            _bulkStoppedManualRadioRecordings.Add(active.Id);
            active.Control.RequestStop();
            active.Cancellation.Cancel();
        }
        foreach (var active in scheduledRecordings)
        {
            _bulkStoppedScheduledRadioRecordings.Add(active.ScheduleId);
            active.Control.RequestStop();
            AdvanceStoppedScheduledOccurrence(active, "zatrzymane przez użytkownika");
            active.Cancellation.Cancel();
        }
        if (scheduledRecordings.Length > 0)
        {
            _store.Save(_state);
            RearmRadioWakeTimer();
        }
        AnnounceEssential(count == 1
            ? "Zatrzymuję nagrywanie"
            : $"Zatrzymuję wszystkie nagrania: {count}");
    }

    private void AdvanceStoppedScheduledOccurrence(ActiveScheduledRadioRecording active, string reason)
    {
        var schedule = _state.Radio.RecordingSchedules.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, active.ScheduleId, StringComparison.Ordinal)
            && candidate.NextStartUtcTicks == active.ExpectedStartUtcTicks);
        if (schedule is not null)
            _ = AdvanceOrRemoveRadioSchedule(schedule, DateTime.UtcNow, reason);
    }

    private async Task CompleteManualRadioRecordingAsync(ActiveManualRadioRecording active)
    {
        ManualRadioRecordingResult result;
        try
        {
            result = await active.Task!;
        }
        catch (OperationCanceledException)
        {
            result = new ManualRadioRecordingResult(false, true, null, null);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("radio-recording", "Nieobsłużony błąd ręcznego nagrywania w tle.", exception);
            result = new ManualRadioRecordingResult(false, false, null, exception.Message);
        }
        _activeManualRadioRecordings.Remove(active.Id);
        var suppressAnnouncement = _bulkStoppedManualRadioRecordings.Remove(active.Id);
        active.Cancellation.Dispose();
        PersistRadioRecordingPauseBookmarks(active.Control);
        if (_isClosing) return;
        RefreshRadioRecordingPresentation();
        if (suppressAnnouncement) return;
        if (result.Success && !string.IsNullOrWhiteSpace(result.Path))
        {
            DiagnosticLog.Info("radio-recording", $"Zakończono ręczne nagranie w tle: {active.StationName}; plik {result.Path}.");
            var fileCount = active.Control.CompletedPaths.Count;
            AnnounceEssential(fileCount > 1
                ? $"Zakończono nagrywanie {active.StationName}. Zapisano plików: {fileCount}"
                : $"Zakończono nagrywanie {active.StationName}: {Path.GetFileName(result.Path)}");
            return;
        }
        if (result.Cancelled && string.IsNullOrWhiteSpace(result.Error))
        {
            AnnounceEssential($"Zatrzymano nagrywanie: {active.StationName}");
            return;
        }
        DiagnosticLog.Warning("radio-recording", $"Nie utworzono ręcznego nagrania {active.StationName}: {result.Error}");
        var savedFileCount = active.Control.CompletedPaths.Count;
        AnnounceEssential(savedFileCount > 0
            ? $"Nagrywanie {active.StationName} zostało przerwane. Zapisano plików: {savedFileCount}. {result.Error}"
            : $"Nie udało się nagrać {active.StationName}: {result.Error}");
    }

    private void PersistRadioRecordingPauseBookmarks(RadioRecordingControl control)
    {
        var markers = control.Markers;
        if (markers.Count == 0) return;

        var importedAny = false;
        foreach (var group in markers
                     .Where(marker => !string.IsNullOrWhiteSpace(marker.Path))
                     .GroupBy(marker => marker.Path, StringComparer.OrdinalIgnoreCase))
        {
            var path = group.Key;
            if (!File.Exists(path)) continue;
            try
            {
                RemoveLocalExclusions([path]);
                var import = LocalLibraryImporter.Import(_localItems, [Path.GetFullPath(path)]);
                var item = import.ImportedItems.FirstOrDefault();
                if (item is null) continue;

                foreach (var marker in group)
                {
                    _bookmarkIndex.Add(
                        "local",
                        "Pliki lokalne",
                        item,
                        marker.Position,
                        marker.CreatedUtc,
                        marker.Name);
                }
                importedAny = true;
                DiagnosticLog.Info(
                    "radio-recording",
                    $"Zapisano {group.Count()} punktów pauzy jako zakładki AMC dla pliku {path}.");
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or InvalidDataException)
            {
                DiagnosticLog.Error(
                    "radio-recording",
                    $"Nie udało się zapisać punktów pauzy jako zakładek dla pliku {path}.",
                    exception);
            }
        }
        if (importedAny)
        {
            RefreshLocalSessionItems();
            TrySaveLocalMediaState(false);
        }
    }

    private void RefreshRadioRecordingPresentation()
    {
        RefreshPlaybackIndicators();
        if (string.Equals(_currentView, ActiveRadioRecordingsViewName, StringComparison.Ordinal)
            && !_playerViewActive)
        {
            var preferredItemId = SelectedItem?.Id;
            var hadFocus = MediaList.IsKeyboardFocusWithin;
            if (hadFocus) AnchorMediaListFocus();
            RefreshCurrentView(preferredItemId: preferredItemId);
            if (hadFocus) RestoreMediaListFocusAfterRefresh();
        }
        UpdateFileMenuForCurrentSession();
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        UpdateWindowTitle();
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

    private string ResolveSystemRadioRecordingsFolder()
    {
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return Path.Combine(music, "AMC — Nagrania radia");
    }

    private IReadOnlyList<MediaItem> VisibleRadioStations() => MediaList.Items
        .OfType<MediaItemRow>()
        .Select(row => row.ActionItem)
        .Where(item => item.Kind == MediaItemKind.Station)
        .ToArray();

    private IReadOnlyList<MediaItem> CurrentViewRadioScheduleStations(MediaItem selectedStation) =>
        RadioScheduleStationSelection.ForCurrentView(
            VisibleRadioStations(),
            selectedStation);

    private IReadOnlyList<MediaItem> AvailableRadioScheduleStations(MediaItem? additionalStation = null)
    {
        var radio = _sessions.FindSession("radio");
        return RadioScheduleStationSelection.ForScheduleManager(
            radio?.Items ?? [],
            VisibleRadioStations(),
            additionalStation ?? (radio?.HasCurrentItem == true ? radio.CurrentItem : null),
            _state.Radio.RecordingSchedules);
    }

    private MediaItem? RadioScheduleActionStation()
    {
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)) return null;
        if (_playerViewActive && _sessions.Current.HasCurrentItem)
            return _sessions.Current.CurrentItem.Kind == MediaItemKind.Station
                ? _sessions.Current.CurrentItem
                : null;
        return ActionItem?.Kind == MediaItemKind.Station ? ActionItem : null;
    }

    private void AddRadioScheduleForCurrentContext()
    {
        var station = RadioScheduleActionStation();
        if (station is null)
        {
            Announce("Wybierz stację radiową albo otwórz ją w odtwarzaczu, a następnie naciśnij Shift+R");
            return;
        }
        var dialog = new RadioScheduleEditorWindow(
            CurrentViewRadioScheduleStations(station),
            existing: null,
            preferredStationId: station.Id,
            initialStartUtc: DateTime.UtcNow.AddMinutes(5),
            offerImmediateStart: true,
            globalWakeEnabled: _state.Radio.WakeScheduledRecordings,
            defaultRecordingFormat: _state.Radio.RecordingFormat,
            defaultRecordingBitrateKbps: _state.Radio.RecordingBitrateKbps)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.ResultSchedule is null)
        {
            RestoreItemActionFocus();
            return;
        }
        _state.Radio.RecordingSchedules.Add(dialog.ResultSchedule);
        _state.Radio.RecordingSchedules = _state.Radio.RecordingSchedules
            .OrderBy(schedule => schedule.NextStartUtcTicks)
            .ToList();
        _store.Save(_state);
        RearmRadioWakeTimer();
        ProcessDueRadioSchedules();
        RestoreItemActionFocus();
        var utc = new DateTime(dialog.ResultSchedule.NextStartUtcTicks, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            utc,
            RadioScheduleCalculator.ResolveTimeZone(dialog.ResultSchedule.TimeZoneId));
        var fileDivision = dialog.ResultSchedule.SegmentMinutes > 0
            ? $"podział co {dialog.ResultSchedule.SegmentMinutes} min"
            : "jeden plik";
        Dispatcher.BeginInvoke(
            () => Announce($"Zaplanowano nagranie: {dialog.ResultSchedule.StationName}, {local:dd.MM.yyyy HH:mm}, {dialog.ResultSchedule.DurationMinutes} min, {fileDivision}"),
            DispatcherPriority.ContextIdle);
    }

    private void ShowRadioSchedules()
    {
        if (!string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            Announce("Harmonogram nagrywania jest dostępny w sesji Radio internetowe");
            return;
        }

        var selectedStation = RadioScheduleActionStation();
        var stations = AvailableRadioScheduleStations(selectedStation);
        var preferredStationId = selectedStation?.Kind == MediaItemKind.Station
            ? selectedStation.Id
            : _sessions.FindSession("radio")?.HasCurrentItem == true
                ? _sessions.FindSession("radio")!.CurrentItem.Id
                : null;
        var dialog = new RadioSchedulesWindow(
            stations,
            _state.Radio.RecordingSchedules,
            _activeScheduledRadioRecordings.Keys,
            preferredStationId,
            _state.Radio.WakeScheduledRecordings,
            _state.Radio.RecordingFormat,
            _state.Radio.RecordingBitrateKbps)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            RestoreItemActionFocus();
            return;
        }

        var replacements = dialog.ResultSchedules.Select(CloneRadioSchedule).ToList();
        foreach (var active in _activeScheduledRadioRecordings.Values.ToArray())
        {
            var replacement = replacements.FirstOrDefault(schedule => schedule.Id == active.ScheduleId);
            if (replacement is null
                || !replacement.Enabled
                || replacement.NextStartUtcTicks != active.ExpectedStartUtcTicks
                || !string.Equals(replacement.StreamUrl, active.StreamUrl, StringComparison.OrdinalIgnoreCase)
                || replacement.DurationMinutes != active.DurationMinutes
                || replacement.SegmentMinutes != active.SegmentMinutes)
            {
                active.Cancellation.Cancel();
            }
        }
        _state.Radio.RecordingSchedules = replacements;
        _state.Radio.WakeScheduledRecordings = dialog.ResultWakeScheduledRecordings;
        _store.Save(_state);
        RearmRadioWakeTimer();
        ProcessDueRadioSchedules();
        RestoreItemActionFocus();
        Dispatcher.BeginInvoke(
            () => Announce("Zapisano harmonogram nagrywania radia"),
            DispatcherPriority.ContextIdle);
    }

    private void NormalizeRadioSchedulesAtStartup()
    {
        var changed = false;
        var now = DateTime.UtcNow;
        foreach (var schedule in _state.Radio.RecordingSchedules.ToArray())
        {
            if (!schedule.Enabled) continue;
            if (RadioScheduleCalculator.Evaluate(schedule, now).Kind != RadioScheduleDueKind.Missed) continue;
            changed |= AdvanceOrRemoveRadioSchedule(schedule, now, "pominięte podczas zamknięcia programu");
        }
        if (changed) _store.Save(_state);
    }

    private void RadioScheduleTimer_Tick(object? sender, EventArgs e) => ProcessDueRadioSchedules();

    private void ProcessDueRadioSchedules()
    {
        if (_isClosing) return;
        var changed = false;
        var now = DateTime.UtcNow;
        foreach (var schedule in _state.Radio.RecordingSchedules
                     .Where(schedule => schedule.Enabled)
                     .OrderBy(schedule => schedule.NextStartUtcTicks)
                     .ToArray())
        {
            if (_activeScheduledRadioRecordings.ContainsKey(schedule.Id)) continue;
            var decision = RadioScheduleCalculator.Evaluate(schedule, now);
            if (decision.Kind == RadioScheduleDueKind.Future) continue;
            if (decision.Kind == RadioScheduleDueKind.Missed)
            {
                changed |= AdvanceOrRemoveRadioSchedule(schedule, now, "pominięte po upływie całego czasu");
                continue;
            }
            StartScheduledRadioRecording(schedule);
        }
        if (changed)
        {
            _store.Save(_state);
            RearmRadioWakeTimer();
        }
    }

    private void StartScheduledRadioRecording(RadioRecordingScheduleSettings schedule)
    {
        var snapshot = CloneRadioSchedule(schedule);
        var startUtc = new DateTime(snapshot.NextStartUtcTicks, DateTimeKind.Utc);
        var deadlineUtc = startUtc.AddMinutes(snapshot.DurationMinutes);
        var actualStartUtc = DateTime.UtcNow;
        var resumingOccurrence = actualStartUtc - startUtc >= TimeSpan.FromSeconds(10);
        var recordingFormat = snapshot.RecordingFormat ?? _state.Radio.RecordingFormat;
        var recordingBitrateKbps = snapshot.RecordingBitrateKbps ?? _state.Radio.RecordingBitrateKbps;
        var requestedOutputFolder = string.IsNullOrWhiteSpace(snapshot.OutputFolder)
            ? ResolveRadioRecordingsFolder()
            : snapshot.OutputFolder;
        var cancellation = new CancellationTokenSource();
        var control = new RadioRecordingControl();
        var task = Task.Run(() => ScheduledRadioRecorder.RecordAsync(
            snapshot,
            deadlineUtc,
            ResolveRadioRecordingsFolder(),
            ResolveSystemRadioRecordingsFolder(),
            recordingFormat,
            recordingBitrateKbps,
            control,
            cancellation.Token));
        _activeScheduledRadioRecordings[snapshot.Id] = new ActiveScheduledRadioRecording(
            snapshot.Id,
            snapshot.StationId,
            snapshot.StationName,
            snapshot.NextStartUtcTicks,
            snapshot.StreamUrl,
            snapshot.DurationMinutes,
            snapshot.SegmentMinutes,
            actualStartUtc,
            deadlineUtc,
            requestedOutputFolder,
            recordingFormat,
            recordingBitrateKbps,
            cancellation,
            control,
            task);
        RefreshRadioRecordingPresentation();
        DiagnosticLog.Info(
            "radio-schedule",
            $"{(resumingOccurrence ? "Wznowiono" : "Uruchomiono")} plan: {snapshot.StationName}; pozostało {(int)Math.Ceiling((deadlineUtc - DateTime.UtcNow).TotalSeconds)} s.");
        AnnounceEssential(resumingOccurrence
            ? $"Wznawiam zaplanowane nagrywanie: {snapshot.StationName}"
            : $"Rozpoczynam zaplanowane nagrywanie: {snapshot.StationName}");
        _ = CompleteScheduledRadioRecordingAsync(snapshot, task);
        RearmRadioWakeTimer();
    }

    private async Task CompleteScheduledRadioRecordingAsync(
        RadioRecordingScheduleSettings snapshot,
        Task<ScheduledRadioRecordingResult> task)
    {
        ScheduledRadioRecordingResult result;
        try
        {
            result = await task;
        }
        catch (OperationCanceledException)
        {
            result = new ScheduledRadioRecordingResult(false, true, null, null);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error("radio-schedule", "Nieobsłużony błąd zaplanowanego nagrania.", exception);
            result = new ScheduledRadioRecordingResult(false, false, null, exception.Message);
        }
        ActiveScheduledRadioRecording? completedActive = null;
        if (_activeScheduledRadioRecordings.Remove(snapshot.Id, out var active))
        {
            completedActive = active;
            active.Cancellation.Dispose();
        }
        if (completedActive is not null)
            PersistRadioRecordingPauseBookmarks(completedActive.Control);
        var suppressAnnouncement = _bulkStoppedScheduledRadioRecordings.Remove(snapshot.Id);
        if (_isClosing) return;
        RefreshRadioRecordingPresentation();

        var current = _state.Radio.RecordingSchedules.FirstOrDefault(schedule =>
            schedule.Id == snapshot.Id
            && schedule.NextStartUtcTicks == snapshot.NextStartUtcTicks);
        if (current is not null && !result.Cancelled)
        {
            _ = AdvanceOrRemoveRadioSchedule(current, DateTime.UtcNow, "zakończone");
            _store.Save(_state);
        }
        RearmRadioWakeTimer();

        if (suppressAnnouncement) return;

        if (result.Cancelled)
        {
            if (!string.IsNullOrWhiteSpace(result.Path))
            {
                var fileCount = completedActive?.Control.CompletedPaths.Count ?? 1;
                AnnounceEssential(fileCount > 1
                    ? $"Zatrzymano zaplanowane nagrywanie. Zapisano plików: {fileCount}"
                    : $"Zatrzymano zaplanowane nagrywanie. Zapisano: {Path.GetFileName(result.Path)}");
            }
            return;
        }
        if (result.Success && !string.IsNullOrWhiteSpace(result.Path))
        {
            DiagnosticLog.Info("radio-schedule", $"Zakończono plan: {snapshot.StationName}; plik {result.Path}.");
            var fileCount = completedActive?.Control.CompletedPaths.Count ?? 1;
            AnnounceEssential(fileCount > 1
                ? $"Zakończono zaplanowane nagrywanie {snapshot.StationName}. Zapisano plików: {fileCount}"
                : $"Zakończono zaplanowane nagrywanie: {Path.GetFileName(result.Path)}");
        }
        else
        {
            DiagnosticLog.Warning("radio-schedule", $"Plan {snapshot.StationName} nie utworzył nagrania: {result.Error}");
            var savedFileCount = completedActive?.Control.CompletedPaths.Count ?? 0;
            AnnounceEssential(savedFileCount > 0
                ? $"Zaplanowane nagrywanie {snapshot.StationName} zostało przerwane. Zapisano plików: {savedFileCount}. {result.Error}"
                : $"Nie udało się nagrać {snapshot.StationName}: {result.Error}");
        }
    }

    private bool AdvanceOrRemoveRadioSchedule(
        RadioRecordingScheduleSettings schedule,
        DateTime afterUtc,
        string reason)
    {
        var next = RadioScheduleCalculator.FindNextStartUtc(schedule, afterUtc);
        if (next is null)
        {
            _state.Radio.RecordingSchedules.Remove(schedule);
            DiagnosticLog.Info("radio-schedule", $"Usunięto zakończony plan {schedule.StationName}: {reason}.");
            return true;
        }
        schedule.NextStartUtcTicks = next.Value.Ticks;
        DiagnosticLog.Info(
            "radio-schedule",
            $"Przeniesiono plan {schedule.StationName} na {next.Value:O}: {reason}.");
        return true;
    }

    private void RearmRadioWakeTimer()
    {
        var now = DateTime.UtcNow;
        var next = _state.Radio.RecordingSchedules
            .Where(schedule => schedule.Enabled
                && schedule.NextStartUtcTicks > now.Ticks
                && (schedule.WakeComputer ?? _state.Radio.WakeScheduledRecordings))
            .OrderBy(schedule => schedule.NextStartUtcTicks)
            .FirstOrDefault();
        if (next is null)
        {
            _radioWakeTimer.Cancel();
            return;
        }
        var start = new DateTime(next.NextStartUtcTicks, DateTimeKind.Utc);
        var wake = start - TimeSpan.FromMinutes(2);
        if (wake <= now) wake = start;
        var armed = _radioWakeTimer.Arm(wake);
        DiagnosticLog.Info(
            "radio-schedule",
            armed
                ? $"Ustawiono wybudzenie na {wake:O} dla {next.StationName}."
                : "Windows odrzucił czasomierz wybudzania.");
    }

    private static RadioRecordingScheduleSettings CloneRadioSchedule(
        RadioRecordingScheduleSettings schedule) => new()
    {
        Id = schedule.Id,
        StationId = schedule.StationId,
        StationName = schedule.StationName,
        StreamUrl = schedule.StreamUrl,
        NextStartUtcTicks = schedule.NextStartUtcTicks,
        TimeZoneId = schedule.TimeZoneId,
        DurationMinutes = schedule.DurationMinutes,
        SegmentMinutes = schedule.SegmentMinutes,
        Recurrence = schedule.Recurrence,
        ActiveDays = [.. schedule.ActiveDays],
        OutputFolder = schedule.OutputFolder,
        RecordingFormat = schedule.RecordingFormat,
        RecordingBitrateKbps = schedule.RecordingBitrateKbps,
        WakeComputer = schedule.WakeComputer,
        Enabled = schedule.Enabled
    };

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
        var label = MediaItemFormatter.Format(item, fields);
        return RadioRecordingStateLabel(item) is { } recordingState
            ? $"{label}, {recordingState}"
            : label;
    }

    private string? RadioRecordingStateLabel(MediaItem item)
    {
        if (item.Kind != MediaItemKind.Station) return null;
        var controls = RadioRecordingControlsFor(item);
        if (controls.Count == 0) return null;
        var paused = controls.Count(control => control.IsPaused);
        if (paused == 0) return "nagrywanie";
        return paused == controls.Count
            ? "nagrywanie wstrzymane"
            : "część nagrań wstrzymana";
    }

    private bool IsRadioStationBeingRecorded(MediaItem item)
    {
        if (item.Kind != MediaItemKind.Station) return false;
        var manual = IsRadioStationManuallyRecording(item);
        return manual || _activeScheduledRadioRecordings.Values.Any(active =>
            SameRadioStation(item, active.StationId, active.StreamUrl));
    }

    private bool IsCurrentRadioStationRecording() =>
        string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
        && _sessions.Current.HasCurrentItem
        && IsRadioStationBeingRecorded(_sessions.Current.CurrentItem);

    private static bool IsRadioTimeshiftCommand(string commandId) => commandId is
        CommandIds.SeekBackward10
        or CommandIds.SeekForward10
        or CommandIds.SeekBackward30
        or CommandIds.SeekForward30
        or CommandIds.SeekBackward60
        or CommandIds.SeekForward60
        or CommandIds.TrackStart
        or CommandIds.TrackEnd
        or CommandIds.RadioJumpLive;

    private bool IsRadioStationManuallyRecording(MediaItem item) =>
        item.Kind == MediaItemKind.Station
        && _activeManualRadioRecordings.Values.Any(active =>
            SameRadioStation(item, active.StationId, active.StreamUrl));

    private static bool SameRadioStation(MediaItem item, string stationId, string streamUrl) =>
        string.Equals(item.Id, stationId, StringComparison.Ordinal)
        || item.Source is { Length: > 0 }
            && string.Equals(item.Source, streamUrl, StringComparison.OrdinalIgnoreCase);

    private static bool SameRadioStation(MediaItem left, MediaItem right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal)
        || left.Source is { Length: > 0 }
            && string.Equals(left.Source, right.Source, StringComparison.OrdinalIgnoreCase);

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
        var homogeneousView = _currentView is "Albumy" or "Playlisty"
            || string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
               && item.Kind == MediaItemKind.Station;
        var label = FormatItem(item, !homogeneousView);
        if (string.Equals(_currentView, "Kolejka", StringComparison.Ordinal) && item.IsPlayNext)
        {
            label = $"Następny, {label}";
        }
        var session = _sessions.Current;
        var isCurrent = string.Equals(session.CurrentItem.Id, item.Id, StringComparison.Ordinal);
        var stationFirst = string.Equals(session.Id, "radio", StringComparison.Ordinal)
            && item.Kind == MediaItemKind.Station;
        if (isCurrent && session.IsPlaying)
            return stationFirst
                ? session.IsMuted ? $"{label}, odtwarzany, wyciszono" : $"{label}, odtwarzany"
                : session.IsMuted ? $"Odtwarzany, wyciszono, {label}" : $"Odtwarzany, {label}";
        if (isCurrent && session.Position > TimeSpan.Zero)
            return stationFirst ? $"{label}, wstrzymany" : $"Wstrzymany, {label}";
        var lastPlayedId = _playbackHistory.GetItemIds(session.Id).FirstOrDefault();
        return string.Equals(lastPlayedId, item.Id, StringComparison.Ordinal)
            ? stationFirst ? $"{label}, ostatnio odtwarzany" : $"Ostatnio odtwarzany, {label}"
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
        var itemTitle = session.CurrentItem.Title;
        if (string.Equals(session.Id, "radio", StringComparison.Ordinal)
            && string.Equals(session.CurrentItem.Id, _radioNowPlayingItemId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(_radioNowPlayingTitle))
        {
            itemTitle = $"{itemTitle} — {_radioNowPlayingTitle}";
        }
        Title = !_playerViewActive && string.Equals(area, DefaultBrowserView, StringComparison.Ordinal)
            ? $"{itemTitle} — {session.DisplayName} — AMC {AppDisplayVersion}"
            : $"{itemTitle} — {area} — {session.DisplayName} — AMC {AppDisplayVersion}";
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
        if (message is not (WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp))
        {
            return IntPtr.Zero;
        }

        var virtualKey = wParam.ToInt32();
        var keyDown = message is WmKeyDown or WmSysKeyDown;
        TrackNativeModifierKey(virtualKey, keyDown);
        if (!keyDown
            || _keyboardHelpActive
            || Keyboard.FocusedElement is System.Windows.Controls.TextBox)
        {
            return IntPtr.Zero;
        }

        // Keyboard.Modifiers potrafi zgubić Shift dokładnie dla górnego klawisza 0
        // w niektórych układach klawiatury i przy aktywnym czytniku ekranu. Stan
        // klawiszy odczytujemy bezpośrednio z Win32, bo ten hook już pracuje na
        // granicy komunikatów okna.
        var modifiers = ReadEffectiveModifierKeys();
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && CurrentSessionSupportsPresets()
            && RadioPresetKeyMap.TryGetSlotFromVirtualKey(virtualKey, out var presetSlot))
        {
            DiagnosticLog.Info(
                "preset",
                $"Bezpośredni skrót presetu; klawisz wirtualny: {virtualKey}; slot: {presetSlot}; sesja: {_sessions.Current.Id}.");
            handled = true;
            Dispatcher.BeginInvoke(
                () => ActivatePreset(presetSlot, useDirectShortcutLabel: true),
                DispatcherPriority.Input);
            return IntPtr.Zero;
        }
        Action? action = (modifiers, virtualKey) switch
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

    private void TrackNativeModifierKey(int virtualKey, bool keyDown)
    {
        switch (virtualKey)
        {
            case VirtualKeyControl:
                _nativeControlDown = keyDown;
                break;
            case VirtualKeyShift:
                _nativeShiftDown = keyDown;
                break;
            case VirtualKeyAlt:
                _nativeAltDown = keyDown;
                break;
            case VirtualKeyLeftWindows:
            case VirtualKeyRightWindows:
                _nativeWindowsDown = keyDown;
                break;
        }
    }

    private ModifierKeys ReadEffectiveModifierKeys()
    {
        var modifiers = Keyboard.Modifiers | ReadNativeModifierKeys();
        if (_nativeControlDown) modifiers |= ModifierKeys.Control;
        if (_nativeShiftDown) modifiers |= ModifierKeys.Shift;
        if (_nativeAltDown) modifiers |= ModifierKeys.Alt;
        if (_nativeWindowsDown) modifiers |= ModifierKeys.Windows;
        return modifiers;
    }

    private void ClearTrackedNativeModifiers()
    {
        _nativeControlDown = false;
        _nativeShiftDown = false;
        _nativeAltDown = false;
        _nativeWindowsDown = false;
    }

    private static ModifierKeys ReadNativeModifierKeys()
    {
        var modifiers = ModifierKeys.None;
        if (IsNativeKeyDown(VirtualKeyControl)) modifiers |= ModifierKeys.Control;
        if (IsNativeKeyDown(VirtualKeyShift)) modifiers |= ModifierKeys.Shift;
        if (IsNativeKeyDown(VirtualKeyAlt)) modifiers |= ModifierKeys.Alt;
        if (IsNativeKeyDown(VirtualKeyLeftWindows) || IsNativeKeyDown(VirtualKeyRightWindows))
            modifiers |= ModifierKeys.Windows;
        return modifiers;
    }

    private static bool IsNativeKeyDown(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

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

        var effectiveModifiers = ReadEffectiveModifierKeys();
        if (windowKey == Key.M
            && effectiveModifiers is ModifierKeys.Control
                or (ModifierKeys.Control | ModifierKeys.Shift))
        {
            ExecuteCommand(effectiveModifiers == ModifierKeys.Control
                ? CommandIds.ToggleMuteCurrentSession
                : CommandIds.ToggleMuteAllSessions);
            e.Handled = true;
            return;
        }

        if (ReadEffectiveModifierKeys() == ModifierKeys.Shift
            && windowKey == Key.Space
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && (_playerViewActive || MediaList.IsKeyboardFocusWithin)
            && !MainMenu.IsKeyboardFocusWithin
            && Keyboard.FocusedElement is not MenuItem)
        {
            ToggleSelectedRadioRecordingPause();
            e.Handled = true;
            return;
        }

        if (ReadEffectiveModifierKeys() == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)
            && windowKey == Key.R)
        {
            StopAllRadioRecordings();
            e.Handled = true;
            return;
        }

        if (ReadEffectiveModifierKeys() == (ModifierKeys.Control | ModifierKeys.Shift)
            && windowKey == Key.H)
        {
            if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
                ShowRadioSchedules();
            else
                Announce("Harmonogram nagrywania jest dostępny w sesji Radio internetowe");
            e.Handled = true;
            return;
        }

        if (TryHandleDirectRadioPresetShortcut(e))
        {
            e.Handled = true;
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

        if (MediaList.IsKeyboardFocusWithin
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && e.Key == Key.R)
        {
            ToggleRadioRecording();
            e.Handled = true;
            return;
        }

        if (MediaList.IsKeyboardFocusWithin
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && string.Equals(_currentView, ActiveRadioRecordingsViewName, StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.None
            && e.Key == Key.R)
        {
            ToggleRadioRecording();
            e.Handled = true;
            return;
        }

        if (MediaList.IsKeyboardFocusWithin
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && string.Equals(_currentView, ActiveRadioRecordingsViewName, StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.None
            && e.Key == Key.T)
        {
            SplitSelectedManualRadioRecording();
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
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)) OpenLocalFolder();
            else Announce("Otwieranie folderu z plikami multimedialnymi jest dostępne w sesji Pliki lokalne");
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)) ImportRadioPlaylist();
            else if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal)) OpenLocalFiles();
            else Announce("To polecenie nie jest dostępne w bieżącej sesji");
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && e.Key == Key.S
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            ShowRadioRecognitionHistory();
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
            if (!TryPasteInternalListMove()) PasteClipboardFilesIntoCurrentView();
            e.Handled = true;
        }
        else if (itemCommandsAvailable
            && modifiers == ModifierKeys.Control
            && e.Key == Key.X
            && MediaList.IsKeyboardFocusWithin)
        {
            if (!TryStartInternalListMove()) Announce(CutLocalFilesForExternalMove(ActionItems));
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
        var modifiers = ReadEffectiveModifierKeys();
        var chord = WindowsKeyMap.FromKeyEvent(e, modifiers);
        var spokenShortcut = FormatShortcutForSpeech(chord);
        var context = KeyboardHelpContext();

        if (string.Equals(
                chord.Canonical,
                KeyChord.Parse(_state.Settings.PrefixChord).Canonical,
                StringComparison.OrdinalIgnoreCase))
        {
            return $"{spokenShortcut}: włącz warstwę prefiksową. W trybie Pomocy warstwa nie zostanie uruchomiona. Kontekst: {context}";
        }

        if (TryDescribeDirectShortcut(key, modifiers, out var directDescription))
        {
            return $"{spokenShortcut}: {directDescription}. Kontekst: {context}";
        }

        if (TryResolveKeyboardHelpCommand(key, modifiers, out var commandId))
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
        var localAudioCommand = MainWindowShortcutRouter.ResolveLocalPlayerAudioProcessing(
            key,
            modifiers,
            _playerViewActive
                && PlayerPanel.IsKeyboardFocusWithin
                && string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal));
        if (localAudioCommand is not null)
        {
            commandId = localAudioCommand;
            return true;
        }
        if (key == Key.M && modifiers == ModifierKeys.Control)
        {
            commandId = CommandIds.ToggleMuteCurrentSession;
            return true;
        }
        if (key == Key.M && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            commandId = CommandIds.ToggleMuteAllSessions;
            return true;
        }
        if (key == Key.Space
            && modifiers == ModifierKeys.Shift
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && (_playerViewActive || MediaList.IsKeyboardFocusWithin))
        {
            commandId = CommandIds.ToggleRadioRecordingPause;
            return true;
        }
        if (key == Key.T
            && modifiers == ModifierKeys.None
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && (_playerViewActive
                || string.Equals(_currentView, ActiveRadioRecordingsViewName, StringComparison.Ordinal)))
        {
            commandId = CommandIds.SplitRadioRecording;
            return true;
        }
        if (key == Key.R
            && modifiers == ModifierKeys.None
            && _playerViewActive
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            commandId = CommandIds.ToggleRadioRecording;
            return true;
        }
        if (key == Key.R
            && modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && (_playerViewActive || MediaList.IsKeyboardFocusWithin))
        {
            commandId = CommandIds.ToggleRadioRecording;
            return true;
        }
        if (key == Key.R
            && modifiers == ModifierKeys.Shift
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            commandId = CommandIds.AddRadioSchedule;
            return true;
        }
        if (key == Key.R
            && modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
        {
            commandId = CommandIds.StopAllRadioRecordings;
            return true;
        }
        if (key == Key.H
            && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            commandId = CommandIds.ManageRadioSchedules;
            return true;
        }
        if (key == Key.S
            && modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            commandId = CommandIds.ViewRadioRecognitionHistory;
            return true;
        }
        if (key == Key.S
            && modifiers == ModifierKeys.None
            && _playerViewActive
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            commandId = CommandIds.RecognizeRadioTrack;
            return true;
        }
        if (key == Key.S
            && modifiers == ModifierKeys.Shift
            && _playerViewActive
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            commandId = CommandIds.ToggleRadioRecognitionMonitoring;
            return true;
        }
        if (CurrentSessionSupportsPresets()
            && key == Key.P
            && modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            commandId = CommandIds.ViewRadioPresets;
            return true;
        }
        if (CurrentSessionSupportsPresets()
            && key == Key.P
            && modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
        {
            commandId = CommandIds.AssignRadioPreset;
            return true;
        }
        if (CurrentSessionSupportsPresets()
            && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && TryGetRadioPresetSlot(key, out var presetSlot))
        {
            commandId = CommandIds.RadioPreset(presetSlot);
            return true;
        }
        if (modifiers == ModifierKeys.Control && TryGetDigitKey(key, out var sessionSlot))
        {
            commandId = sessionSlot == 0 ? CommandIds.SessionList : CommandIds.SessionSlot(sessionSlot);
            return true;
        }

        if (modifiers == ModifierKeys.Control && key == Key.O)
        {
            commandId = string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
                ? CommandIds.ImportRadioPlaylist
                : CommandIds.OpenLocalFiles;
            return true;
        }
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.O
            && !string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
        {
            commandId = string.Empty;
            return false;
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
            (ModifierKeys.Control | ModifierKeys.Shift, Key.H) => CommandIds.ManageRadioSchedules,
            (ModifierKeys.Control, Key.B) => CommandIds.ViewBookmarks,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.B) => CommandIds.AddNamedBookmark,
            (ModifierKeys.Control, Key.K) => CommandIds.FilterCurrent,
            (ModifierKeys.Control, Key.F) => CommandIds.SearchCurrent,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.A) => CommandIds.ViewAlbums,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.F) => CommandIds.SearchAll,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.G) => CommandIds.SettingsToggleSeekMessages,
            (ModifierKeys.Control, Key.M) => CommandIds.ToggleMuteCurrentSession,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.M) => CommandIds.ToggleMuteAllSessions,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.K) => CommandIds.CommandPalette,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.E) => CommandIds.TimeElapsed,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.R) => CommandIds.TimeRemaining,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.T) => CommandIds.TimeTotal,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.O) => CommandIds.OpenLocalFolder,
            (ModifierKeys.Control, Key.OemComma) => CommandIds.SettingsGeneral,
            (ModifierKeys.Control, Key.F5) when string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) => CommandIds.ManageLocalSources,
            (ModifierKeys.Alt, Key.D1) when string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) => CommandIds.ViewFolders,
            (ModifierKeys.Alt, Key.D2) when string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) => CommandIds.ViewAllLocalFiles,
            (ModifierKeys.Alt, Key.D2) when string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal) => CommandIds.ViewActiveRadioRecordings,
            (ModifierKeys.Alt, Key.D3) when string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal) => CommandIds.ViewCustomLocalOrder,
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
        if (CurrentSessionSupportsPresets()
            && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && TryGetRadioPresetSlot(key, out var presetSlot))
            description = $"uruchom preset {RadioPresetSlots.Label(presetSlot)} aktywnej sesji";
        else if (modifiers == ModifierKeys.Alt && key == Key.F4)
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
        else if (modifiers == ModifierKeys.None
                 && key == Key.T
                 && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
                 && (_playerViewActive
                     || string.Equals(_currentView, ActiveRadioRecordingsViewName, StringComparison.Ordinal)))
            description = "zapisz bieżącą część i rozpocznij nowy plik ręcznego nagrania";
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
            or CommandIds.AddRadioSchedule
            or CommandIds.AddBookmark or CommandIds.AddNamedBookmark
            or CommandIds.PreviousBookmark or CommandIds.NextBookmark;
        if (needsItem && ActionItem is null) return "brak wybranego lub odtwarzanego elementu";
        if (commandId == CommandIds.SplitRadioRecording
            && ResolveManualRecordingForSplit() is null)
            return "brak ręcznego nagrania do podziału";
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
        // Ctrl+0 is the session list, but Ctrl+Shift+0 is preset 0. Use the
        // native modifier state as well so a transient WPF omission of Shift
        // cannot send the second shortcut to the first command.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var shortcut = MainWindowShortcutRouter.ResolveDigit(
            key,
            ReadEffectiveModifierKeys(),
            CurrentSessionSupportsPresets());
        if (shortcut.Kind is not (MainWindowDigitShortcutKind.SessionList
            or MainWindowDigitShortcutKind.SessionSlot)) return false;
        ExecuteCommand(shortcut.Kind == MainWindowDigitShortcutKind.SessionList
            ? CommandIds.SessionList
            : CommandIds.SessionSlot(shortcut.Slot));
        return true;
    }

    private bool TryHandleLocalLibraryViewShortcut(KeyEventArgs e)
    {
        if (_playerViewActive || Keyboard.FocusedElement is System.Windows.Controls.TextBox) return false;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Control && key == Key.F5)
        {
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
                ExecuteCommand(CommandIds.ManageLocalSources);
            else
                Announce("Foldery Biblioteki są dostępne tylko w sesji Pliki lokalne");
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D1)
        {
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
                ExecuteCommand(CommandIds.ViewFolders);
            else
                Announce("Alt+1 jest zarezerwowane dla widoku folderów w sesji Pliki lokalne");
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D2)
        {
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
                ExecuteCommand(CommandIds.ViewAllLocalFiles);
            else if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
                ExecuteCommand(CommandIds.ViewActiveRadioRecordings);
            else
                Announce("Alt+2 nie ma jeszcze widoku w bieżącej sesji");
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.D3)
        {
            if (string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal))
                ExecuteCommand(CommandIds.ViewCustomLocalOrder);
            else
                Announce("Alt+3 jest zarezerwowane dla kolejności własnej w sesji Pliki lokalne");
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

    private bool TryHandleDirectRadioPresetShortcut(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var shortcut = MainWindowShortcutRouter.ResolveDigit(
            key,
            ReadEffectiveModifierKeys(),
            CurrentSessionSupportsPresets());
        if (shortcut.Kind != MainWindowDigitShortcutKind.Preset) return false;
        ActivatePreset(shortcut.Slot, useDirectShortcutLabel: true);
        return true;
    }

    private bool HandleFocusedDirectShortcut(KeyChord chord)
    {
        if (!GlobalPrefixService.IsFocusedDirectShortcutCandidate(chord)) return false;
        if (string.Equals(
                chord.Canonical,
                KeyChord.Parse(_state.Settings.PrefixChord).Canonical,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox)
            return false;

        Dispatcher.BeginInvoke(
            () =>
            {
                if (_keyboardHelpActive)
                {
                    AnnounceEssential($"Ctrl+Shift+0: uruchom preset 0 aktywnej sesji. Kontekst: {KeyboardHelpContext()}");
                    return;
                }
                DiagnosticLog.Info(
                    "preset",
                    $"Niskopoziomowy skrót presetu 0; sesja: {_sessions.Current.Id}.");
                ActivatePreset(10, useDirectShortcutLabel: true);
            },
            DispatcherPriority.Input);
        return true;
    }

    private bool CurrentSessionSupportsPresets() => _sessions.Current is not null;

    private static bool TryGetRadioPresetSlot(Key key, out int slot) =>
        RadioPresetKeyMap.TryGetSlot(key, out slot);

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
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var commandId = (Keyboard.Modifiers, key) switch
        {
            (ModifierKeys.Control | ModifierKeys.Alt, Key.P)
                when CurrentSessionSupportsPresets() => CommandIds.ViewRadioPresets,
            (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Key.P)
                when CurrentSessionSupportsPresets() => CommandIds.AssignRadioPreset,
            (ModifierKeys.Control, Key.U) => CommandIds.ViewFavorites,
            (ModifierKeys.Control, Key.P) => CommandIds.ViewPlaylists,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.P) => CommandIds.ManagePlaylists,
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
        var localAudioCommand = MainWindowShortcutRouter.ResolveLocalPlayerAudioProcessing(
            key,
            Keyboard.Modifiers,
            string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal));
        if (localAudioCommand is not null)
        {
            ExecuteCommand(localAudioCommand);
            return true;
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.None
            && key == Key.R)
        {
            ToggleRadioRecording();
            return true;
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.None
            && key == Key.T)
        {
            SplitSelectedManualRadioRecording();
            return true;
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.Shift
            && key == Key.R)
        {
            AddRadioScheduleForCurrentContext();
            return true;
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.None
            && key == Key.S)
        {
            _ = RecognizeCurrentRadioTrackAsync(automatic: false);
            return true;
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == ModifierKeys.Shift
            && key == Key.S)
        {
            ToggleRadioRecognitionMonitoring();
            return true;
        }
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt)
            && key == Key.S)
        {
            ShowRadioRecognitionHistory();
            return true;
        }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) && key == Key.R)
        {
            ToggleRadioRecording();
            return true;
        }
        if (Keyboard.Modifiers == ModifierKeys.None
            && key == Key.End
            && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal))
        {
            ExecuteCommand(CommandIds.RadioJumpLive);
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
        if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
            && modifiers == ModifierKeys.Shift
            && e.Key == Key.R)
        {
            AddRadioScheduleForCurrentContext();
            return true;
        }
        if (!_playerViewActive
            && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null
            && modifiers == ModifierKeys.Alt
            && e.SystemKey == Key.Enter)
        {
            ShowSelectedPlaylistProperties();
            return true;
        }
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
            && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null)
        {
            var playlistCommand = (modifiers, e.Key) switch
            {
                (ModifierKeys.Control, Key.Enter) => "play",
                (ModifierKeys.Shift, Key.Enter) => CommandIds.AddQueue,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.Q) => CommandIds.AddQueue,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.Enter) => CommandIds.TogglePlayNext,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.U) => CommandIds.ToggleFavorite,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.L) => CommandIds.ToggleLibrary,
                (ModifierKeys.Control | ModifierKeys.Shift, Key.P) => "open-items-first",
                _ => null
            };
            if (playlistCommand is not null)
            {
                if (playlistCommand == "play") PlaySelectedPlaylistNow();
                else if (playlistCommand == "open-items-first")
                    Announce("Otwórz playlistę Enterem, aby zmieniać przynależność jej elementów do innych playlist");
                else if (string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
                         && playlistCommand is CommandIds.AddQueue or CommandIds.TogglePlayNext)
                {
                    Announce("Kolejka i odtwarzanie jako następne nie dotyczą sesji Radio internetowe");
                }
                else ExecuteSelectedPlaylistContentsCommand(playlistCommand);
                return true;
            }
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
        if (action is SearchResultAction.PlayNext or SearchResultAction.Queue
            && results.Any(result =>
                string.Equals(result.SessionId, "radio", StringComparison.OrdinalIgnoreCase)))
        {
            return "Ta funkcja nie jest dostępna w Radiu internetowym";
        }

        var changesMembership = action is SearchResultAction.PlayNext
            or SearchResultAction.Queue
            or SearchResultAction.Favorite
            or SearchResultAction.Library;
        if (changesMembership
            && results.Select(selected => selected.SessionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any())
        {
            return "Dla jednego działania wybierz wyniki z tej samej usługi";
        }

        var result = results[0];
        var session = SelectSessionBrowserItem(result.SessionId, result.Item.Id);
        if (session is null) return "Wybrana sesja nie jest już dostępna";

        var previousActionItemsOverride = _actionItemsOverride;
        if (changesMembership)
        {
            _actionItemsOverride = results
                .Select(selected => selected.Item)
                .DistinctBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            DiagnosticLog.Info(
                "selection",
                $"Polecenie {action} z wyników wyszukiwania; sesja: {session.Id}; " +
                $"zaznaczone wyniki: {results.Count}; unikatowe elementy działania: {_actionItemsOverride.Count}.");
        }

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
            _actionItemsOverride = previousActionItemsOverride;
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
        var activeRecordingCount =
            _activeManualRadioRecordings.Count + _activeScheduledRadioRecordings.Count;
        if (!_recordingCloseConfirmed && activeRecordingCount > 0)
        {
            var answer = System.Windows.MessageBox.Show(
                this,
                $"Aktywne nagrania: {activeRecordingCount}. Zamknięcie AMC zakończy nagrywanie i zapisze odebrane fragmenty. Przerwane nagrania nie zostaną wznowione po ponownym uruchomieniu. Zamknąć program?",
                "Trwające nagrania",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            _recordingCloseConfirmed = true;
        }

        _isClosing = true;
        _radioRecognitionMonitoring = false;
        _trackRecognitionCancellation.Cancel();
        CaptureCurrentSessionNavigationState();
        CaptureLocalMediaState();
        _playerUiTimer.Stop();
        _playerUiTimer.Tick -= PlayerUiTimer_Tick;
        _localSourceSyncTimer.Stop();
        _localSourceSyncTimer.Tick -= LocalSourceSyncTimer_Tick;
        _radioScheduleTimer.Stop();
        _radioScheduleTimer.Tick -= RadioScheduleTimer_Tick;
        var manualRecordings = _activeManualRadioRecordings.Values.ToArray();
        foreach (var active in manualRecordings) active.Control.RequestStop();
        foreach (var active in manualRecordings) active.Cancellation.Cancel();
        var manualTasks = manualRecordings
            .Select(active => active.Task)
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();
        if (manualTasks.Length > 0)
        {
            try
            {
                if (!Task.WaitAll(manualTasks, 5_000))
                {
                    DiagnosticLog.Warning(
                        "radio-recording",
                        "Nie wszystkie ręczne nagrania zakończyły finalizację przed zamknięciem programu.");
                }
            }
            catch (AggregateException exception)
            {
                DiagnosticLog.Warning(
                    "radio-recording",
                    $"Finalizacja ręcznych nagrań podczas zamykania zgłosiła błąd {exception.GetBaseException().GetType().Name}.");
            }
        }
        var scheduledRecordings = _activeScheduledRadioRecordings.Values.ToArray();
        foreach (var active in scheduledRecordings)
            AdvanceStoppedScheduledOccurrence(active, "przerwane przy zamknięciu programu");
        foreach (var active in scheduledRecordings) active.Control.RequestStop();
        foreach (var active in scheduledRecordings) active.Cancellation.Cancel();
        if (scheduledRecordings.Length > 0)
        {
            try
            {
                // Give each private recording pipeline a short, bounded chance
                // to close its selected encoder before Windows tears the process down.
                // This runs only while the application is already closing.
                if (!Task.WaitAll(scheduledRecordings.Select(active => active.Task).ToArray(), 5_000))
                {
                    DiagnosticLog.Warning(
                        "radio-schedule",
                        "Nie wszystkie zaplanowane nagrania zakończyły finalizację przed zamknięciem programu.");
                }
            }
            catch (AggregateException exception)
            {
                DiagnosticLog.Warning(
                    "radio-schedule",
                    $"Finalizacja planów podczas zamykania zgłosiła błąd {exception.GetBaseException().GetType().Name}.");
            }
        }
        _radioWakeTimer.Dispose();
        foreach (var watcher in _localSourceWatchers.Values) watcher.Dispose();
        _localSourceWatchers.Clear();
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        _prefixService?.Dispose();
        _localOutput.Dispose();
        _radioOutput.Dispose();
        _radioCatalog.Dispose();
        _trackRecognitionCancellation.Dispose();
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
    private void ToggleCurrentSessionMute_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.ToggleMuteCurrentSession);
    private void ToggleAllSessionsMute_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.ToggleMuteAllSessions);
    private void PlaybackMenuItem_SubmenuOpened(object sender, RoutedEventArgs e) =>
        UpdateFileMenuForCurrentSession();
    private void ToggleLoudnessNormalization_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.ToggleLoudnessNormalization);
    private void ToggleSmoothTrackTransitions_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.ToggleSmoothTrackTransitions);
    private void InterTrackSilenceChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem
            || !int.TryParse(
                Convert.ToString(menuItem.Tag, CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var milliseconds))
        {
            return;
        }
        SetInterTrackSilence(milliseconds);
    }
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
    private void PlayPlaylistNow_Click(object sender, RoutedEventArgs e) => PlaySelectedPlaylistNow();
    private void PlayNext_Click(object sender, RoutedEventArgs e) =>
        ExecutePlaylistContainerOrItemCommand(CommandIds.TogglePlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) =>
        ExecutePlaylistContainerOrItemCommand(CommandIds.AddQueue);
    private void Favorite_Click(object sender, RoutedEventArgs e) =>
        ExecutePlaylistContainerOrItemCommand(CommandIds.ToggleFavorite);
    private void Library_Click(object sender, RoutedEventArgs e) =>
        ExecutePlaylistContainerOrItemCommand(CommandIds.ToggleLibrary);

    private void ExecutePlaylistContainerOrItemCommand(string commandId)
    {
        if (!_playerViewActive && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null)
            ExecuteSelectedPlaylistContentsCommand(commandId);
        else
            ExecuteCommand(commandId);
    }
    private void Playlists_Click(object sender, RoutedEventArgs e)
        => ShowPlaylistManager();
    private void Information_Click(object sender, RoutedEventArgs e)
    {
        if (!_playerViewActive && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null)
            ShowSelectedPlaylistProperties();
        else
            ExecuteCommand(CommandIds.ItemProperties);
    }
    private void ItemPlaybackOptions_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ItemPlaybackOptions);
    private void GoToAlbum_Click(object sender, RoutedEventArgs e) => GoToRelatedAlbum();
    private void GoToArtist_Click(object sender, RoutedEventArgs e) => GoToRelatedArtist();
    private void CopyName_Click(object sender, RoutedEventArgs e) => CopyActionItemName();
    private void CopyLocation_Click(object sender, RoutedEventArgs e) => CopyActionItemLocation();
    private void CutFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!TryStartInternalListMove()) Announce(CutLocalFilesForExternalMove(ActionItems));
    }
    private void PasteFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPasteInternalListMove()) PasteClipboardFilesIntoCurrentView();
    }
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
    private void ImportRadioPlaylist_Click(object sender, RoutedEventArgs e) => ImportRadioPlaylist();
    private void AddRadioStation_Click(object sender, RoutedEventArgs e) => AddRadioStation();
    private void RadioRecording_Click(object sender, RoutedEventArgs e) => ToggleRadioRecording();
    private void PauseRadioRecording_Click(object sender, RoutedEventArgs e) => ToggleSelectedRadioRecordingPause();
    private void SplitRadioRecording_Click(object sender, RoutedEventArgs e) => SplitSelectedManualRadioRecording();
    private void StopAllRadioRecordings_Click(object sender, RoutedEventArgs e) => StopAllRadioRecordings();
    private void RadioAddSchedule_Click(object sender, RoutedEventArgs e) => AddRadioScheduleForCurrentContext();
    private void RadioSchedules_Click(object sender, RoutedEventArgs e) => ShowRadioSchedules();
    private void RecognizeRadioTrack_Click(object sender, RoutedEventArgs e) =>
        _ = RecognizeCurrentRadioTrackAsync(automatic: false);
    private void ToggleRadioRecognitionMonitoring_Click(object sender, RoutedEventArgs e) =>
        ToggleRadioRecognitionMonitoring();
    private void RadioRecognitionHistory_Click(object sender, RoutedEventArgs e) =>
        ShowRadioRecognitionHistory();
    private void ActiveRadioRecordingsView_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.ViewActiveRadioRecordings);
    private void Sessions_Click(object sender, RoutedEventArgs e) => ShowSessionList();
    private void MediaContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var items = ActionItems;
        var actionItem = ActionItem;
        var radioSession = string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal);
        var localSession = string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal);
        var playlistContainer = !_playerViewActive
            && (MediaList.SelectedItem as MediaItemRow)?.PlaylistId is not null;
        var playlistContext = playlistContainer
            && TryResolveSelectedPlaylistContents(out var resolvedPlaylistContext)
                ? resolvedPlaylistContext
                : null;
        var playlistContents = !_playerViewActive
            && TryGetPlaylistIdFromView(_currentView, out _);
        var localAlbumContainer = !_playerViewActive
            && (MediaList.SelectedItem as MediaItemRow)?.AlbumFolderPath is not null;
        var folderNavigationRow = !_playerViewActive
            && string.Equals(_currentView, FolderViewName, StringComparison.Ordinal)
            && (MediaList.SelectedItem as MediaItemRow)?.FolderPath is not null;
        var membershipItems = playlistContext?.Items
            ?? (folderNavigationRow
                && TryResolveSelectedFolderContents(out var folderContext)
                    ? folderContext.Items
                    : items);
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
        var radioStationSelected = radioSession
            && actionItem?.Kind == MediaItemKind.Station
            && !playlistContainer;
        SelectedRadioAddScheduleMenuItem.Visibility = radioStationSelected
            ? Visibility.Visible
            : Visibility.Collapsed;
        SelectedRadioRecordingMenuItem.Visibility = radioStationSelected
            ? Visibility.Visible
            : Visibility.Collapsed;
        var selectedRecordingControls = radioStationSelected && actionItem is not null
            ? RadioRecordingControlsFor(actionItem)
            : Array.Empty<RadioRecordingControl>();
        SelectedRadioPauseRecordingMenuItem.Visibility = selectedRecordingControls.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        var selectedManualRecording = radioStationSelected && actionItem is not null
            ? _activeManualRadioRecordings.Values.FirstOrDefault(active =>
                SameRadioStation(actionItem, active.StationId, active.StreamUrl))
            : null;
        SelectedRadioSplitRecordingMenuItem.Visibility = selectedManualRecording is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        SelectedRadioSplitRecordingMenuItem.IsEnabled = selectedManualRecording?.Control.IsReady == true;
        if (radioStationSelected && actionItem is not null)
        {
            SetContextMenuItemPresentation(
                SelectedRadioRecordingMenuItem,
                IsRadioStationManuallyRecording(actionItem)
                    ? "Zakończ nagrywanie tej stacji"
                    : "Nagrywaj tę stację w tle",
                "Ctrl+Alt+R");
            SetContextMenuItemPresentation(
                SelectedRadioPauseRecordingMenuItem,
                selectedRecordingControls.Count > 0
                    && selectedRecordingControls.All(control => control.IsPaused)
                        ? "Wznów nagrywanie tej stacji"
                        : "Wstrzymaj nagrywanie tej stacji",
                "Shift+Spacja");
        }
        SelectedRadioSchedulesMenuItem.Visibility = radioSession
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlayPlaylistNowMenuItem.Visibility = playlistContainer ? Visibility.Visible : Visibility.Collapsed;
        PlayPlaylistNowMenuItem.IsEnabled = playlistContext?.Items.Count > 0;
        SetContextMenuItemPresentation(
            InformationMenuItem,
            playlistContainer ? "Właściwości playlisty" : "Właściwości i informacje",
            "Alt+Enter");
        var playNextActive = membershipItems.Count > 0
            && (folderNavigationRow
                ? membershipItems.Any(item => item.IsPlayNext)
                : membershipItems.All(item => item.IsPlayNext));
        var playNextLabel = playNextActive
            ? playlistContainer
                ? "Usuń zawartość playlisty z odtwarzanych jako następne"
                : folderNavigationRow
                    ? "Usuń zawartość folderu z odtwarzanych jako następne"
                    : "Usuń z odtwarzanych jako następne"
            : playlistContainer
                ? "Odtwórz zawartość playlisty jako następną"
                : folderNavigationRow
                    ? "Odtwórz zawartość folderu jako następną"
                    : "Odtwórz jako następne";
        SetContextMenuItemPresentation(PlayNextMenuItem, playNextLabel, "Ctrl+Shift+Enter");
        var queueActive = membershipItems.Count > 0
            && (folderNavigationRow
                ? membershipItems.Any(item => item.IsInQueue || item.IsPlayNext)
                : membershipItems.All(item => item.IsInQueue || item.IsPlayNext));
        var queueLabel = queueActive
            ? playlistContainer
                ? "Usuń zawartość playlisty z kolejki"
                : folderNavigationRow
                    ? "Usuń zawartość folderu z kolejki"
                    : "Usuń z kolejki"
            : playlistContainer
                ? "Dodaj zawartość playlisty do kolejki"
                : folderNavigationRow
                    ? "Dodaj zawartość folderu do kolejki"
                    : "Dodaj do kolejki";
        SetContextMenuItemPresentation(QueueMenuItem, queueLabel, "Shift+Enter");
        var favoriteActive = membershipItems.Count > 0
            && (folderNavigationRow
                ? membershipItems.Any(item => item.IsFavorite)
                : membershipItems.All(item => item.IsFavorite));
        var favoriteLabel = favoriteActive
            ? playlistContainer
                ? "Usuń zawartość playlisty z ulubionych"
                : folderNavigationRow
                    ? "Usuń zawartość folderu z ulubionych"
                    : "Usuń z ulubionych"
            : playlistContainer
                ? "Dodaj zawartość playlisty do ulubionych"
                : folderNavigationRow
                    ? "Dodaj zawartość folderu do ulubionych"
                    : "Dodaj do ulubionych";
        SetContextMenuItemPresentation(FavoriteMenuItem, favoriteLabel, "Ctrl+Shift+U");
        var libraryLabel = membershipItems.Count > 0 && membershipItems.All(item => item.IsInLibrary)
            ? playlistContainer
                ? "Usuń zawartość playlisty z biblioteki"
                : "Usuń z biblioteki"
            : playlistContainer
                ? "Dodaj zawartość playlisty do biblioteki"
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
        PlayNextMenuItem.Visibility = radioSession
            || localAlbumContainer
            || (playlistContainer && membershipItems.Count == 0)
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        QueueMenuItem.Visibility = radioSession
            || localAlbumContainer
            || (playlistContainer && membershipItems.Count == 0)
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        FavoriteMenuItem.Visibility = localAlbumContainer
            || (playlistContainer && membershipItems.Count == 0)
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        LibraryMenuItem.Visibility = localAlbumContainer
            || folderNavigationRow
            || (playlistContainer && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        PlaylistMembershipMenuItem.Visibility = playlistContainer
            || localAlbumContainer
            || (folderNavigationRow && membershipItems.Count == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
        SetContextMenuItemPresentation(
            PlaylistMembershipMenuItem,
            "Zmień przynależność do playlist",
            "Ctrl+Shift+P");
        var presetTargetAvailable = CurrentSessionSupportsPresets()
            && TryGetSessionPresetTarget(out _, out _, out _, out _);
        RadioPresetMembershipMenuItem.Visibility = presetTargetAvailable
                ? Visibility.Visible
                : Visibility.Collapsed;
        SetContextMenuItemPresentation(
            RadioPresetMembershipMenuItem,
            "Utwórz lub przypisz preset",
            "Ctrl+Alt+Shift+P");
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
        var internalRadioMove = IsInternalRadioFavoriteMoveView();
        CutFilesMenuItem.Visibility = localItems || internalRadioMove ? Visibility.Visible : Visibility.Collapsed;
        PasteFilesMenuItem.Visibility = CanPasteFilesIntoCurrentView() || internalRadioMove
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (internalRadioMove)
        {
            SetContextMenuItemPresentation(CutFilesMenuItem, "Zaznacz do przeniesienia", "Ctrl+X");
            SetContextMenuItemPresentation(PasteFilesMenuItem, "Przenieś przed wybraną stację", "Ctrl+V");
            PasteFilesMenuItem.IsEnabled = _pendingInternalListMove is not null;
        }
        else
        {
            SetContextMenuItemPresentation(CutFilesMenuItem, "Wytnij pliki do przeniesienia", "Ctrl+X");
            SetContextMenuItemPresentation(PasteFilesMenuItem, "Wklej pliki ze schowka", "Ctrl+V");
            PasteFilesMenuItem.IsEnabled = true;
        }
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
        UpdatePlaybackAudioMenuPresentation(
            string.Equals(_sessions.Current.Id, "local", StringComparison.Ordinal));
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
        PlayerPlayNextMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerQueueMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerPlaylistMembershipMenuItem.Visibility = Visibility.Visible;
        SetContextMenuItemPresentation(
            PlayerPlaylistMembershipMenuItem,
            "Zmień przynależność do playlist",
            "Ctrl+Shift+P");
        PlayerRadioPresetMembershipMenuItem.Visibility = CurrentSessionSupportsPresets()
            && _sessions.Current.HasCurrentItem
                ? Visibility.Visible
                : Visibility.Collapsed;
        SetContextMenuItemPresentation(
            PlayerRadioPresetMembershipMenuItem,
            "Utwórz lub przypisz preset",
            "Ctrl+Alt+Shift+P");
        PlayerRadioRecordingMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        var playerRecordingControls = radioSession
            ? RadioRecordingControlsFor(item)
            : Array.Empty<RadioRecordingControl>();
        PlayerRadioPauseRecordingMenuItem.Visibility = playerRecordingControls.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        var playerManualRecording = radioSession
            ? _activeManualRadioRecordings.Values.FirstOrDefault(active =>
                SameRadioStation(item, active.StationId, active.StreamUrl))
            : null;
        PlayerRadioSplitRecordingMenuItem.Visibility = playerManualRecording is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
        PlayerRadioSplitRecordingMenuItem.IsEnabled = playerManualRecording?.Control.IsReady == true;
        PlayerRadioAddScheduleMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        PlayerRadioSchedulesMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        PlayerRecognizeRadioTrackMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        PlayerRecognizeRadioTrackMenuItem.IsEnabled = radioSession && _radioOutput.LoadedItemId is not null;
        PlayerMonitorRadioRecognitionMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        PlayerMonitorRadioRecognitionMenuItem.IsChecked = _radioRecognitionMonitoring;
        SetContextMenuItemPresentation(
            PlayerMonitorRadioRecognitionMenuItem,
            _radioRecognitionMonitoring
                ? "Obserwuj rozpoznawanie: włączone"
                : "Obserwuj rozpoznawanie: wyłączone",
            "Shift+S");
        PlayerRadioRecognitionHistoryMenuItem.Visibility = radioSession ? Visibility.Visible : Visibility.Collapsed;
        PlayerAddBookmarkMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerAddNamedBookmarkMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        PlayerBookmarksMenuItem.Visibility = radioSession ? Visibility.Collapsed : Visibility.Visible;
        if (radioSession)
        {
            SetContextMenuItemPresentation(
                PlayerRadioRecordingMenuItem,
                IsRadioStationManuallyRecording(item) ? "Zakończ nagrywanie radia" : "Rozpocznij nagrywanie radia",
                "R");
            SetContextMenuItemPresentation(
                PlayerRadioPauseRecordingMenuItem,
                playerRecordingControls.Count > 0
                    && playerRecordingControls.All(control => control.IsPaused)
                        ? "Wznów nagrywanie radia"
                        : "Wstrzymaj nagrywanie radia",
                "Shift+Spacja");
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
        MenuAccessibility.SetPresentation(menuItem, label);
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
        var entries = items
            .Select(item =>
            {
                var localPath = TryGetLocalPath(item.Source, out var path) ? path : null;
                return new ClipboardMediaEntry(
                    item.Title,
                    localPath,
                    localPath is null ? GetShareableLocation(item, sessionId) : null);
            })
            .ToArray();
        var localPaths = entries
            .Select(entry => entry.LocalPath)
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

        var text = string.Join(
            Environment.NewLine,
            entries.SelectMany(entry => entry.LocalPath is not null
                ? [entry.LocalPath]
                : new[] { entry.Title, entry.ShareableLocation! }));
        if (localPaths.Length > 0)
        {
            var fileDropList = new StringCollection();
            fileDropList.AddRange(localPaths);
            var data = new System.Windows.DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            data.SetFileDropList(fileDropList);
            if (!ClipboardRetry.TrySetDataObject(data, out var mixedClipboardError)) return mixedClipboardError;
        }
        else if (!ClipboardRetry.TrySetText(text, out var uriClipboardError))
        {
            return uriClipboardError;
        }

        var remoteCount = entries.Length - localPaths.Length;
        if (localPaths.Length > 0)
        {
            return $"Skopiowano pliki: {localPaths.Length}; nazwy i łącza: {remoteCount}";
        }
        return remoteCount == 1
            ? "Skopiowano nazwę i łącze"
            : $"Skopiowano nazwy i łącza: {remoteCount}";
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
                return new ClipboardMediaEntry(
                    result.Item.Title,
                    localPath,
                    localPath is null
                        ? GetShareableLocation(result.Item, result.SessionId)
                        : null);
            })
            .ToArray();
        var localPaths = clipboardEntries
            .Select(entry => entry.LocalPath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serviceCount = clipboardEntries.Count(entry => entry.LocalPath is null);
        var text = string.Join(
            Environment.NewLine,
            clipboardEntries.SelectMany(entry => entry.LocalPath is not null
                ? [entry.LocalPath]
                : new[] { entry.Title, entry.ShareableLocation! }));

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
            return $"Skopiowano pliki: {localPaths.Length}; nazwy i łącza: {serviceCount}";
        }
        if (localPaths.Length > 0)
        {
            return localPaths.Length == 1
                ? "Skopiowano plik i pełną ścieżkę"
                : $"Skopiowano pliki i pełne ścieżki: {FormatFileCount(localPaths.Length)}";
        }
        return results.Count == 1
            ? "Skopiowano nazwę i łącze"
            : $"Skopiowano nazwy i łącza: {results.Count}";
    }

    private static string GetShareableLocation(MediaItem item, string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(item.PublicUri)) return item.PublicUri;
        if (Uri.TryCreate(item.Source, UriKind.Absolute, out var sourceUri)
            && sourceUri.Scheme is "http" or "https")
        {
            return sourceUri.AbsoluteUri;
        }
        return $"demo://{sessionId}/{item.Id}";
    }

    private sealed record ClipboardMediaEntry(
        string Title,
        string? LocalPath,
        string? ShareableLocation);

    private bool IsInternalRadioFavoriteMoveView() =>
        !_playerViewActive
        && string.Equals(_sessions.Current.Id, "radio", StringComparison.Ordinal)
        && string.Equals(_currentView, "Ulubione", StringComparison.Ordinal);

    private bool TryStartInternalListMove()
    {
        if (!IsInternalRadioFavoriteMoveView()) return false;
        if (!string.IsNullOrWhiteSpace(FilterBox.Text))
        {
            Announce("Wyczyść filtr klawiszem Escape przed przenoszeniem stacji");
            return true;
        }

        var itemIds = MediaList.SelectedItems
            .OfType<MediaItemRow>()
            .Where(row => row.Bookmark is null && row.PlaylistId is null)
            .Select(row => row.ActionItem.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (itemIds.Length == 0)
        {
            Announce("Wybierz stację do przeniesienia");
            return true;
        }

        _pendingInternalListMove = new PendingInternalListMove(
            _sessions.Current.Id,
            _currentView,
            itemIds);
        Announce(itemIds.Length == 1
            ? "Zaznaczono stację do przeniesienia. Wybierz miejsce i naciśnij Ctrl+V"
            : $"Zaznaczono do przeniesienia: {itemIds.Length} stacji. Wybierz miejsce i naciśnij Ctrl+V");
        return true;
    }

    private bool TryPasteInternalListMove()
    {
        if (!IsInternalRadioFavoriteMoveView()) return false;
        if (!string.IsNullOrWhiteSpace(FilterBox.Text))
        {
            Announce("Wyczyść filtr klawiszem Escape przed przenoszeniem stacji");
            return true;
        }
        if (_pendingInternalListMove is not { } pending
            || !string.Equals(pending.SessionId, _sessions.Current.Id, StringComparison.Ordinal)
            || !string.Equals(pending.ViewName, _currentView, StringComparison.Ordinal))
        {
            Announce("Najpierw zaznacz stację lub grupę skrótem Ctrl+X");
            return true;
        }
        if (SelectedItem is not { } target)
        {
            Announce("Wybierz stację, przed którą chcesz przenieść zaznaczenie");
            return true;
        }

        var order = EnsureFavoriteOrder(_sessions.Current);
        var result = LocalLibraryManualOrder.PlaceItemsBefore(order, pending.ItemIds, target.Id);
        if (result != ManualOrderPlacementResult.Moved)
        {
            Announce(result switch
            {
                ManualOrderPlacementResult.TargetInSelection =>
                    "Wybierz inną stację jako miejsce docelowe",
                ManualOrderPlacementResult.Unchanged =>
                    "Stacje są już w tym miejscu",
                _ => "Nie można przenieść zaznaczonych stacji"
            });
            return true;
        }

        _pendingInternalListMove = null;
        CaptureRadioState();
        _store.Save(_state);
        RefreshCurrentView(preferredItemId: pending.ItemIds[0]);
        SelectMediaItems(pending.ItemIds);
        PrepareSelectedItemFocusContext(pending.ItemIds.Count == 1
            ? $"Przeniesiono przed {target.Title}"
            : $"Przeniesiono {pending.ItemIds.Count} stacji przed {target.Title}");
        RestoreMediaListFocusAfterRefresh();
        return true;
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
            Announce("Schowek nie zawiera obsługiwanych plików multimedialnych");
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
    private void RadioPresetsView_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ViewRadioPresets);
    private void RadioAssignPreset_Click(object sender, RoutedEventArgs e) =>
        ExecuteCommand(CommandIds.AssignRadioPreset);
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

    private sealed record PlaylistContentsActionContext(
        PlaylistEntry Playlist,
        IReadOnlyList<MediaItem> Items);

    private sealed record ActiveScheduledRadioRecording(
        string ScheduleId,
        string StationId,
        string StationName,
        long ExpectedStartUtcTicks,
        string StreamUrl,
        int DurationMinutes,
        int SegmentMinutes,
        DateTime StartedUtc,
        DateTime DeadlineUtc,
        string RequestedOutputFolder,
        RadioRecordingFormat RecordingFormat,
        int RecordingBitrateKbps,
        CancellationTokenSource Cancellation,
        RadioRecordingControl Control,
        Task<ScheduledRadioRecordingResult> Task);

    private sealed class ActiveManualRadioRecording(
        string id,
        string stationId,
        string stationName,
        string streamUrl,
        DateTime requestedUtc,
        RadioRecordingFormat recordingFormat,
        int recordingBitrateKbps,
        CancellationTokenSource cancellation,
        RadioRecordingControl control)
    {
        public string Id { get; } = id;
        public string StationId { get; } = stationId;
        public string StationName { get; } = stationName;
        public string StreamUrl { get; } = streamUrl;
        public DateTime RequestedUtc { get; } = requestedUtc;
        public DateTime? StartedUtc { get; set; }
        public string? Path { get; set; }
        public RadioRecordingFormat RecordingFormat { get; } = recordingFormat;
        public int RecordingBitrateKbps { get; } = recordingBitrateKbps;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public RadioRecordingControl Control { get; } = control;
        public Task<ManualRadioRecordingResult>? Task { get; set; }
        public bool IsSplitting { get; set; }
    }

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
