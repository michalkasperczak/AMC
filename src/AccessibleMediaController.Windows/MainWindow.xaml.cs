using System.ComponentModel;
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
    private SessionManager _sessions = null!;
    private CommandRouter _router = null!;
    private GlobalPrefixService? _prefixService;
    private const string DefaultBrowserView = "Multimedia";
    private const string PlayerViewName = "Teraz odtwarzane";
    private string _currentView = DefaultBrowserView;
    private List<MediaItemRow> _unfilteredItems = [];
    private readonly Dictionary<string, SessionViewHistory> _viewHistories =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly MediaMembershipHistory _membershipHistory = new();
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
    private readonly DispatcherTimer _playerUiTimer;
    private bool _playerViewActive;
    private bool _restoringSessionNavigation;
    private string? _playerFocusContextPrefix;
    private readonly System.Windows.Forms.StatusStrip _playbackStatusBar;
    private readonly System.Windows.Forms.ToolStripStatusLabel _playbackStatusLabel;

    private const int WmKeyDown = 0x0100;
    private const int VirtualKeyE = 0x45;
    private const int VirtualKeyG = 0x47;
    private const int VirtualKeyR = 0x52;
    private const int VirtualKeyT = 0x54;
    private const int VirtualKeyZ = 0x5A;
    private static readonly TimeSpan TypeAheadTimeout = TimeSpan.FromMilliseconds(1200);
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
        _state = state;
        _store = store;
        _localOutput.DurationAvailable += LocalOutput_DurationAvailable;
        _localOutput.PlaybackFailed += LocalOutput_PlaybackFailed;
        _localOutput.PlaybackEnded += LocalOutput_PlaybackEnded;
        ApplyDetailedHints();
        RebuildCore();
        RestoreCurrentSessionNavigationState();
        UpdatePlaybackStatusBar();
        _playerUiTimer.Start();
    }

    public MediaItem? SelectedItem => (MediaList.SelectedItem as MediaItemRow)?.Item;
    public MediaItem? ActionItem => _playerViewActive
        ? _sessions.Current.CurrentItem
        : SelectedItem ?? _sessions.Current.CurrentItem;
    public IReadOnlyList<MediaItem> ActionItems
    {
        get
        {
            if (_playerViewActive) return [_sessions.Current.CurrentItem];
            var selected = MediaList.SelectedItems
                .OfType<MediaItemRow>()
                .Select(row => row.Item)
                .DistinctBy(item => item.Id)
                .ToArray();
            return selected.Length > 0 ? selected : [ActionItem ?? _sessions.Current.CurrentItem];
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
        navigation.CurrentView = DefaultBrowserView;
        navigation.PlayerActive = false;
        navigation.SelectedItemIds[DefaultBrowserView] = itemId;
        _currentView = DefaultBrowserView;
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

    private void ShowPlayerView()
    {
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
        Dispatcher.BeginInvoke(FocusPlayerView, DispatcherPriority.ContextIdle);
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
            "Strzałki sterują czasem i głośnością. Shift+przecinek zwalnia, Shift+kropka przyspiesza, Ctrl+kropka przywraca normalną prędkość. Escape wraca do listy.");
    }

    private void PlayerUiTimer_Tick(object? sender, EventArgs e)
    {
        if (_playerViewActive) UpdatePlayerView();
        UpdatePlaybackStatusBar();
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
        var session = _sessions.Current;
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

    public void OpenOfficialApplication()
    {
        Announce($"{_sessions.Current.DisplayName}: otwieranie zewnętrzne nie jest jeszcze połączone");
    }

    public void ShowHelp()
    {
        MessageBox.Show(
            "Domyślny prefiks: Ctrl+Alt+Windows+F12.\n\n" +
            "Po prefiksie: 1–9 wybiera sesję, 0 otwiera ich listę, Page Up i Page Down zmieniają sesję, " +
            "strzałki sterują czasem i głośnością, Ctrl+E/R/T podaje czas. " +
            "U otwiera Ulubione, Shift+U zmienia stan ulubionych, A otwiera Albumy, P otwiera Playlisty. " +
            "K filtruje bieżącą listę, F wyszukuje w bieżącej usłudze; warianty z Shift otwierają " +
            "paletę poleceń i wyszukiwanie globalne.\n\n" +
            "W aktywnym oknie: Ctrl+1–9 wybiera sesję bez prefiksu, Ctrl+0 otwiera listę sesji, " +
            "Ctrl+Page Up i Ctrl+Page Down zmieniają sesję. " +
            "Ctrl+O otwiera lokalne pliki audio, a Ctrl+Shift+O otwiera folder wraz z podfolderami. " +
            "Oba polecenia tworzą tymczasową sesję bez automatycznego odtwarzania. " +
            "Ctrl+U/P/L/Q otwiera odpowiednio: Ulubione, Playlisty, Bibliotekę i Kolejkę, " +
            "a Ctrl+Shift+A otwiera Albumy. Ctrl+K filtruje bieżącą listę. Ctrl+F otwiera okno " +
            "wyszukiwania w bieżącej usłudze, Ctrl+Shift+F otwiera wyszukiwanie globalne, " +
            "a Ctrl+Shift+K otwiera paletę poleceń. " +
            "Ctrl+N i Ctrl+A pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko.\n\n" +
            "W oknie: Enter na utworze lub stacji rozpoczyna odtwarzanie i otwiera odtwarzacz. " +
            "Ctrl+Enter odtwarza lub wstrzymuje zaznaczony element bez opuszczania listy, a Spacja steruje elementem faktycznie grającym. " +
            "F6 otwiera odtwarzacz. W odtwarzaczu strzałki w lewo i w prawo przewijają o 10 sekund, z Shiftem o 30 sekund, a z Ctrl o minutę, " +
            "strzałki w górę i w dół zmieniają głośność, Home i End przechodzą na początek i w pobliże końca, " +
            "a cyfry od 0 do 9 przechodzą odpowiednio do 0, 10, 20 i kolejnych procent długości utworu oraz domyślnie oznajmiają tylko procent. " +
            "Shift+przecinek zmniejsza prędkość, Shift+kropka ją zwiększa, a Ctrl+kropka przywraca 1,00 razy; tempo zmienia się bez zmiany wysokości dźwięku. " +
            "Escape wraca do wcześniejszej listy. Ctrl+Shift+E, Ctrl+Shift+R i Ctrl+Shift+T podają czas od początku, pozostały i całkowity. " +
            "Ctrl+Shift+G chwilowo włącza lub wyłącza wszystkie automatyczne komunikaty odtwarzacza; ich kategorie wybiera się osobno w Ustawieniach. " +
            "NVDA+End odczytuje pasek stanu z bieżącym czasem i parametrami audio. " +
            "Alt+Enter otwiera jedno dostępne okno Właściwości i informacje. " +
            "Ctrl+K, Ctrl+F i Ctrl+Shift+F nie opuszczają odtwarzacza; wyszukiwanie jest dostępne po powrocie do listy. " +
            "Skróty widoków opuszczają odtwarzacz, a F6 wraca do niego. " +
            "Delete lub Backspace usuwa z bieżącego widoku, Alt+Strzałka w lewo wraca. " +
            "Ctrl+Z cofa ostatnią zmianę Ulubionych, Biblioteki lub Kolejki. " +
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

        AnnounceEssential("Wczytywanie folderu");
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
            RestoreMediaListFocusAfterRefresh();
            return;
        }

        AddLocalFiles(fileNames);
    }

    private void AddLocalFiles(IEnumerable<string> fileNames)
    {
        var paths = fileNames.ToArray();

        var knownPaths = _localItems
            .Select(item => item.Source)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var addedItems = paths
            .Where(path => knownPaths.Add(path))
            .Select(path => new MediaItem
            {
                Id = $"local-{Guid.NewGuid():N}",
                Title = Path.GetFileNameWithoutExtension(path),
                Kind = MediaItemKind.Track,
                Source = path,
                IsInLibrary = true
            })
            .ToList();
        _localItems.AddRange(addedItems);

        var (session, slot) = _sessions.AddOrUpdateTransientSession(
            "local",
            "Lokalne multimedia",
            addedItems,
            _localOutput,
            4);

        var selected = addedItems.FirstOrDefault()
            ?? session.Items.FirstOrDefault(item => string.Equals(
                item.Source,
                paths.FirstOrDefault(),
                StringComparison.OrdinalIgnoreCase))
            ?? session.CurrentItem;
        SelectSessionBrowserItem(session.Id, selected.Id);
        var countText = addedItems.Count == 0
            ? "pliki były już na liście"
            : $"dodano {FormatFileCount(addedItems.Count)}";
        var slotText = slot is > 0 ? $", sesja {slot}" : string.Empty;
        PrepareSelectedItemFocusContext($"Lokalne multimedia{slotText}, {countText}");
        RestoreMediaListFocusAfterRefresh();
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
        var previousLocal = _sessions?.FindSession("local");
        var previousLocalItemId = previousLocal?.CurrentItem.Id;
        var previousLocalPosition = previousLocal?.Position ?? TimeSpan.Zero;
        var previousLocalVolume = previousLocal?.Volume ?? 35;
        var previousLocalPlaybackRate = previousLocal?.PlaybackRate ?? 1d;
        var previousLocalWasPlaying = previousLocal?.IsPlaying == true;
        _membershipHistory.Clear();
        _sessions = new SessionManager(_state.Settings);
        if (_localItems.Count > 0)
        {
            var (local, _) = _sessions.AddOrUpdateTransientSession(
                "local",
                "Lokalne multimedia",
                _localItems,
                _localOutput,
                4);
            var previousItem = local.Items.FirstOrDefault(item => item.Id == previousLocalItemId);
            if (previousItem is not null) local.SelectItem(previousItem);
            local.SetVolume(previousLocalVolume);
            local.SetPosition(previousLocalPosition);
            local.SetPlaybackRate(previousLocalPlaybackRate);
            if (previousLocalWasPlaying) local.Play(local.CurrentItem);
            if (string.Equals(previousSessionId, "local", StringComparison.Ordinal)
                || string.Equals(_state.Settings.LastSessionId, "local", StringComparison.Ordinal))
            {
                _sessions.SelectSession("local");
            }
        }
        _router = new CommandRouter(_sessions, _state.Settings, this, this);
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
        var title = e.Item?.Title ?? "plik";
        Announce($"Nie można odtworzyć: {title}. {e.Message}");
    }

    private void LocalOutput_PlaybackEnded(object? sender, MediaPlaybackEndedEventArgs e)
    {
        var localSession = _sessions.FindSession("local");
        var nextItem = localSession?.ContinueAfterPlaybackEnded(e.Item);
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
        var changedSession = changesListMembership ? _sessions.Current : null;
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
                undoAnnouncement);
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
                    PrepareSelectedItemFocusContext($"{sessionContext}, {_currentView}");
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
        return result;
    }

    private void RefreshCurrentView(int? fallbackIndex = null, string? preferredItemId = null)
    {
        ClearFocusContext();
        preferredItemId ??= SelectedItem?.Id;
        SessionHeading.Text = _sessions.Current.DisplayName;
        ViewHeading.Text = _currentView;
        UpdateWindowTitle();
        IEnumerable<MediaItem> items = _sessions.Current.Items;
        if (_currentView == "Ulubione") items = items.Where(item => item.IsFavorite);
        if (_currentView == "Playlisty") items = items.Where(item => item.Kind == MediaItemKind.Playlist);
        if (_currentView == "Biblioteka") items = items.Where(item => item.IsInLibrary);
        if (_currentView == "Kolejka") items = items.Where(item => item.IsInQueue || item.IsPlayNext);
        if (_currentView == "Albumy") items = items.Where(item => item.Kind == MediaItemKind.Album);
        _unfilteredItems = items
            .Select(item => new MediaItemRow(item, FormatListItem(item), item.PrimaryText))
            .ToList();
        ApplyFilter(preferredItemId, fallbackIndex);
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
            var session = _sessions.Current;
            if (session.CurrentItem.Id != item.Id || !session.IsPlaying)
            {
                session.Play(item);
            }
            RefreshPlaybackIndicators();
            ShowPlayerView();
            return;
        }
        NavigateTo(item.Title);
        Announce($"{item.KindLabel}: {item.Title}. {FormatItemCount(_unfilteredItems.Count)}, {FormatDurationWords(item.Duration)}");
    }

    private void RemoveSelected()
    {
        if (_currentView is not ("Ulubione" or "Biblioteka" or "Kolejka"))
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Usuwanie jest dostępne tylko w widokach Ulubione, Biblioteka i Kolejka"),
                DispatcherPriority.ContextIdle);
            return;
        }

        var items = MediaList.SelectedItems
            .OfType<MediaItemRow>()
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
            else if (_currentView == "Biblioteka")
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
                "Kolejka" => $"Przywrócono w kolejce: {items[0].Title}",
                _ => throw new InvalidOperationException($"Nieobsługiwany widok usuwania: {_currentView}")
            }
            : $"Przywrócono w widoku {_currentView}: {FormatItemCount(items.Length)}";
        _membershipHistory.RecordBatch(
            _sessions.Current.Id,
            previousMemberships,
            undoAnnouncement);
        RefreshCurrentView(previousIndex);
        RestoreMediaListFocusAfterRefresh();
        var removedLabel = items.Length == 1 ? items[0].Title : FormatItemCount(items.Length);
        var announcement = MediaList.Items.Count == 0
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

        var undo = _membershipHistory.Undo();
        if (undo is null)
        {
            RestoreMediaListFocusAfterRefresh();
            Dispatcher.BeginInvoke(
                () => Announce("Brak zmian do cofnięcia"),
                DispatcherPriority.ContextIdle);
            return;
        }

        if (_sessions.Current.Id == undo.SessionId)
        {
            RefreshCurrentView();
            SelectMediaItems(undo.Items.Select(item => item.Item.Id));
        }
        RestoreMediaListFocusAfterRefresh();

        Dispatcher.BeginInvoke(
            () => Announce(undo.Announcement),
            DispatcherPriority.ContextIdle);
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
        var dialog = new SettingsWindow(_state, _store, initialTarget) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultState is null)
        {
            RestoreMediaListFocusAfterRefresh();
            return;
        }
        _state = dialog.ResultState;
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
        if (!HistoryMessagesEnabled) return;
        PrepareViewFocusContext($"{direction}, {_currentView}, {_sessions.Current.DisplayName}");
    }

    private string FormatListItem(MediaItem item)
    {
        var homogeneousView = _currentView is "Albumy" or "Playlisty";
        var label = FormatItem(item, !homogeneousView);
        var session = _sessions.Current;
        if (!string.Equals(session.CurrentItem.Id, item.Id, StringComparison.Ordinal)) return label;
        if (session.IsPlaying) return $"Odtwarzany, {label}";
        return session.Position > TimeSpan.Zero ? $"Wstrzymany, {label}" : label;
    }

    private void RefreshPlaybackIndicators()
    {
        foreach (var row in _unfilteredItems)
        {
            row.UpdateLabel(FormatListItem(row.Item));
        }
    }

    private void UpdateWindowTitle()
    {
        var session = _sessions.Current;
        var area = _playerViewActive ? "Odtwarzacz" : _currentView;
        Title = $"{session.CurrentItem.Title} — {session.DisplayName} — {area} — AMC {AppDisplayVersion}";
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
            if (modifiers == ModifierKeys.None) ActivateSelected();
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
            (ModifierKeys.Control, Key.K) => CommandIds.FilterCurrent,
            (ModifierKeys.Control, Key.F) => CommandIds.SearchCurrent,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.A) => CommandIds.ViewAlbums,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.F) => CommandIds.SearchAll,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.G) => CommandIds.SettingsToggleSeekMessages,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.K) => CommandIds.CommandPalette,
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

        if (Keyboard.Modifiers == ModifierKeys.None && TryGetDigitKey(e.Key, out var digit))
        {
            ExecuteCommand(CommandIds.SeekPercent(digit * 10));
            return true;
        }

        var commandId = (Keyboard.Modifiers, e.Key) switch
        {
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
        if (commandId is null) return false;
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
        }
        RestoreMediaListFocusAfterRefresh();
    }

    private string? ExecuteSearchResultAction(
        SearchWindow.SearchResult result,
        SearchResultAction action,
        bool _)
    {
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
        if (Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
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
            || !e.Text.All(char.IsLetterOrDigit)
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
        CaptureCurrentSessionNavigationState();
        _playerUiTimer.Stop();
        _playerUiTimer.Tick -= PlayerUiTimer_Tick;
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
    private void ToggleSelectedPlayback_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ActivateSelected);
    private void PlayNext_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.TogglePlayNext);
    private void Queue_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.AddQueue);
    private void Favorite_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleFavorite);
    private void Library_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ToggleLibrary);
    private void Playlists_Click(object sender, RoutedEventArgs e) => ShowPlaylistManager();
    private void Information_Click(object sender, RoutedEventArgs e) => ExecuteCommand(CommandIds.ItemProperties);
    private void CopyName_Click(object sender, RoutedEventArgs e) => CopyActionItemName();
    private void CopyLocation_Click(object sender, RoutedEventArgs e) => CopyActionItemLocation();
    private void OfficialApp_Click(object sender, RoutedEventArgs e) => OpenOfficialApplication();
    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveSelected();
    private void Undo_Click(object sender, RoutedEventArgs e) => UndoLastMembershipChange();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void OpenLocalFiles_Click(object sender, RoutedEventArgs e) => OpenLocalFiles();
    private void OpenLocalFolder_Click(object sender, RoutedEventArgs e) => OpenLocalFolder();
    private void Sessions_Click(object sender, RoutedEventArgs e) => ShowSessionList();
    private void MediaContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var items = ActionItems;
        var actionItem = ActionItem;
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
        if (ActionItem is not { } item) return;
        Clipboard.SetText(item.Title);
        Announce("Skopiowano nazwę");
    }

    private void CopyActionItemLocation()
    {
        if (ActionItem is not { } item) return;
        if (TryGetLocalPath(item.Source, out var localPath))
        {
            Clipboard.SetText(localPath);
            Announce("Skopiowano pełną ścieżkę");
            return;
        }

        var publicUri = string.IsNullOrWhiteSpace(item.PublicUri)
            ? $"demo://{_sessions.Current.Id}/{item.Id}"
            : item.PublicUri;
        Clipboard.SetText(publicUri);
        Announce("Skopiowano łącze do elementu");
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

    private sealed class MediaItemRow(MediaItem item, string label, string navigationText) : INotifyPropertyChanged
    {
        public MediaItem Item { get; } = item;
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
}
