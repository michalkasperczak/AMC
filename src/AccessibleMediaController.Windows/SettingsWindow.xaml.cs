using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class SettingsWindow : Window
{
    private readonly ConfigurationStore _store;
    private PersistedState _workingState;
    private readonly ObservableCollection<BindingRow> _bindingRows = [];
    private readonly ObservableCollection<MessageTemplateRow> _messageRows = [];
    private readonly ObservableCollection<MediaFieldRow> _mediaFieldRows = [];
    private readonly ObservableCollection<SessionOrderRow> _sessionOrderRows = [];
    private readonly SettingsTarget _initialTarget;
    private bool _initialFocusApplied;

    public SettingsWindow(
        PersistedState state,
        ConfigurationStore store,
        SettingsTarget initialTarget = SettingsTarget.General)
    {
        InitializeComponent();
        _store = store;
        _initialTarget = initialTarget;
        _workingState = store.CloneState(state);
        BindingsList.ItemsSource = _bindingRows;
        MessageTemplatesList.ItemsSource = _messageRows;
        ListFieldOrderList.ItemsSource = _mediaFieldRows;
        SessionOrderList.ItemsSource = _sessionOrderRows;
        LoadControls();
    }

    public PersistedState? ResultState { get; private set; }

    private KeyboardProfile? SelectedProfile => ProfileCombo.SelectedItem as KeyboardProfile;

    private void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (_initialFocusApplied) return;
        _initialFocusApplied = true;
        Dispatcher.BeginInvoke(ApplyInitialFocus, DispatcherPriority.ContextIdle);
    }

    private void ApplyInitialFocus()
    {
        var (tab, target) = ResolveInitialFocus(_initialTarget);
        SettingsTabs.SelectedItem = tab;
        SettingsTabs.UpdateLayout();
        if (target.Focus())
        {
            Keyboard.Focus(target);
            return;
        }

        tab.Focus();
        Keyboard.Focus(tab);
    }

    private (TabItem Tab, FrameworkElement Target) ResolveInitialFocus(SettingsTarget target)
    {
        return target switch
        {
            SettingsTarget.Language => (GeneralTab, LanguageText),
            SettingsTarget.StartupTarget => (GeneralTab, StartupTargetCombo),
            SettingsTarget.SessionOrder => (GeneralTab, SessionOrderList),
            SettingsTarget.PausePlaybackWhenLeavingPlayer => (GeneralTab, PausePlaybackWhenLeavingPlayerCheck),
            SettingsTarget.FollowPlaybackOnPlayerExit => (GeneralTab, FollowPlaybackOnPlayerExitCheck),
            SettingsTarget.RememberLocalPlaybackPositions => (GeneralTab, RememberLocalPlaybackPositionsCheck),
            SettingsTarget.Prefix => (GeneralTab, PrefixBox),
            SettingsTarget.PrefixTimeout => (GeneralTab, TimeoutBox),
            SettingsTarget.RadioRecording => (RadioTab, RadioRecordingFormatCombo),
            SettingsTarget.RadioRecordingsFolder => (RadioTab, DefaultRadioFolderOption),
            SettingsTarget.RadioWakeScheduledRecordings => (RadioTab, WakeScheduledRadioRecordingsCheck),
            SettingsTarget.KeyboardProfile => (KeyboardProfilesTab, ProfileCombo),
            SettingsTarget.ActivateKeyboardProfile => (KeyboardProfilesTab, ActivateProfileButton),
            SettingsTarget.DuplicateKeyboardProfile => (KeyboardProfilesTab, DuplicateProfileButton),
            SettingsTarget.RenameKeyboardProfile => (KeyboardProfilesTab, RenameProfileButton),
            SettingsTarget.DeleteKeyboardProfile => (KeyboardProfilesTab, DeleteProfileButton),
            SettingsTarget.ImportKeyboardMap => (KeyboardProfilesTab, ImportKeyboardMapButton),
            SettingsTarget.ExportKeyboardMap => (KeyboardProfilesTab, ExportKeyboardMapButton),
            SettingsTarget.KeyboardBindings => (KeyboardProfilesTab, BindingsList),
            SettingsTarget.ChangeKeyboardBinding => (KeyboardProfilesTab, ChangeBindingButton),
            SettingsTarget.RemoveKeyboardBinding => (KeyboardProfilesTab, RemoveBindingButton),
            SettingsTarget.ListFieldOrder => (MessagesTab, ListFieldOrderList),
            SettingsTarget.ImportExport => (ImportExportTab, ImportExportTab),
            SettingsTarget.ImportConfiguration => (ImportExportTab, ImportConfigurationButton),
            SettingsTarget.ExportConfiguration => (ImportExportTab, ExportConfigurationButton),
            SettingsTarget.ImportFullBackup => (ImportExportTab, ImportFullBackupButton),
            SettingsTarget.ExportFullBackup => (ImportExportTab, ExportFullBackupButton),
            SettingsTarget.Messages => (MessagesTab, MessagesTab),
            SettingsTarget.HistoryMessages => (MessagesTab, HistoryMessagesCheck),
            SettingsTarget.ArrowSeekMessages => (MessagesTab, ArrowSeekMessagesCheck),
            SettingsTarget.PercentageSeekMessages => (MessagesTab, PercentageSeekMessagesCheck),
            SettingsTarget.BookmarkNavigationMessages => (MessagesTab, BookmarkNavigationMessagesCheck),
            SettingsTarget.VolumeMessages => (MessagesTab, VolumeMessagesCheck),
            SettingsTarget.PlaybackMessages => (MessagesTab, PlaybackMessagesCheck),
            SettingsTarget.PercentageSeekAnnouncement => (MessagesTab, PercentageSeekAnnouncementCombo),
            SettingsTarget.MessageTemplates => (MessagesTab, MessageTemplatesList),
            SettingsTarget.Updates => (UpdatesTab, UpdatesTab),
            _ => (GeneralTab, GeneralTab)
        };
    }

    private void LoadControls()
    {
        PrefixBox.Text = _workingState.Settings.PrefixChord;
        TimeoutBox.Text = _workingState.Settings.PrefixTimeoutMilliseconds.ToString();
        LanguageText.Text = _workingState.Settings.InterfaceLanguage == "pl-PL"
            ? "Polski — zmiana języka jest planowana"
            : _workingState.Settings.InterfaceLanguage;
        SelectComboByTag(StartupTargetCombo, _workingState.Settings.StartupTarget.ToString());
        PausePlaybackWhenLeavingPlayerCheck.IsChecked = _workingState.Settings.PausePlaybackWhenLeavingPlayer;
        FollowPlaybackOnPlayerExitCheck.IsChecked = _workingState.Settings.FollowPlaybackOnPlayerExit;
        RememberLocalPlaybackPositionsCheck.IsChecked = _workingState.Settings.RememberLocalPlaybackPositions;

        var customRadioFolder = !string.IsNullOrWhiteSpace(_workingState.Radio.RecordingsFolder);
        DefaultRadioFolderOption.IsChecked = !customRadioFolder;
        CustomRadioFolderOption.IsChecked = customRadioFolder;
        RadioRecordingsFolderBox.Text = _workingState.Radio.RecordingsFolder;
        SelectComboByTag(RadioRecordingFormatCombo, _workingState.Radio.RecordingFormat.ToString());
        SelectComboByTag(
            RadioRecordingBitrateCombo,
            _workingState.Radio.RecordingBitrateKbps.ToString());
        WakeScheduledRadioRecordingsCheck.IsChecked = _workingState.Radio.WakeScheduledRecordings;
        UpdateRadioRecordingControls();

        MessagesEnabledCheck.IsChecked = _workingState.Settings.Messages.Enabled;
        DetailedHintsCheck.IsChecked = _workingState.Settings.Messages.DetailedHints;
        SeekMessagesCheck.IsChecked = _workingState.Settings.Messages.SeekMessages;
        HistoryMessagesCheck.IsChecked = _workingState.Settings.Messages.HistoryMessages;
        ArrowSeekMessagesCheck.IsChecked = _workingState.Settings.Messages.ArrowSeekMessages;
        PercentageSeekMessagesCheck.IsChecked = _workingState.Settings.Messages.PercentageSeekMessages;
        BookmarkNavigationMessagesCheck.IsChecked = _workingState.Settings.Messages.BookmarkNavigationMessages;
        VolumeMessagesCheck.IsChecked = _workingState.Settings.Messages.VolumeMessages;
        PlaybackMessagesCheck.IsChecked = _workingState.Settings.Messages.PlaybackMessages;
        SelectComboByTag(
            PercentageSeekAnnouncementCombo,
            _workingState.Settings.Messages.PercentageSeekAnnouncement.ToString());
        _messageRows.Clear();
        foreach (var pair in _workingState.Settings.Messages.Templates
                     .OrderBy(pair => MessageTemplateSortOrder(pair.Key))
                     .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            _messageRows.Add(new MessageTemplateRow(pair.Key, pair.Value));
        }
        if (_messageRows.Count > 0) MessageTemplatesList.SelectedIndex = 0;

        UpdateCheckAutomatically.IsChecked = _workingState.Settings.Updates.CheckAutomatically;
        UpdateDownloadAutomatically.IsChecked = _workingState.Settings.Updates.DownloadAutomatically;
        UpdateInstallOnExit.IsChecked = _workingState.Settings.Updates.InstallOnExit;
        UpdateMetered.IsChecked = _workingState.Settings.Updates.AllowMeteredConnection;
        SelectComboByTag(UpdateChannelCombo, _workingState.Settings.Updates.Channel);

        RefreshProfiles(_workingState.Settings.ActiveKeyboardProfileId);
        LoadSessionOrder();
        LoadMediaFieldOrder();
    }

    private void ApplyControls()
    {
        if (Enum.TryParse<StartupTarget>(SelectedTag(StartupTargetCombo, nameof(StartupTarget.MediaList)), out var startupTarget))
        {
            _workingState.Settings.StartupTarget = startupTarget;
        }
        _workingState.Settings.PrefixChord = KeyChord.Parse(PrefixBox.Text).Canonical;
        _workingState.Settings.PausePlaybackWhenLeavingPlayer = PausePlaybackWhenLeavingPlayerCheck.IsChecked == true;
        _workingState.Settings.FollowPlaybackOnPlayerExit = FollowPlaybackOnPlayerExitCheck.IsChecked == true;
        _workingState.Settings.RememberLocalPlaybackPositions = RememberLocalPlaybackPositionsCheck.IsChecked == true;
        if (CustomRadioFolderOption.IsChecked == true)
        {
            var folder = RadioRecordingsFolderBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
                throw new InvalidDataException("Wybrany folder nagrań radia musi zawierać pełną ścieżkę.");
            _workingState.Radio.RecordingsFolder = Path.GetFullPath(folder);
        }
        else
        {
            _workingState.Radio.RecordingsFolder = string.Empty;
        }
        if (Enum.TryParse<RadioRecordingFormat>(
                SelectedTag(RadioRecordingFormatCombo, nameof(RadioRecordingFormat.Mp3)),
                out var recordingFormat))
        {
            _workingState.Radio.RecordingFormat = recordingFormat;
        }
        if (!int.TryParse(SelectedTag(RadioRecordingBitrateCombo, "192"), out var recordingBitrate))
            recordingBitrate = 192;
        _workingState.Radio.RecordingBitrateKbps = recordingBitrate;
        _workingState.Radio.WakeScheduledRecordings = WakeScheduledRadioRecordingsCheck.IsChecked == true;
        if (!int.TryParse(TimeoutBox.Text, out var timeout) || timeout is < 250 or > 30000)
        {
            throw new InvalidDataException("Czas prefiksu musi mieścić się między 250 a 30000 ms.");
        }
        _workingState.Settings.PrefixTimeoutMilliseconds = timeout;

        _workingState.Settings.Messages.Enabled = MessagesEnabledCheck.IsChecked == true;
        _workingState.Settings.Messages.DetailedHints = DetailedHintsCheck.IsChecked == true;
        _workingState.Settings.Messages.SeekMessages = SeekMessagesCheck.IsChecked == true;
        _workingState.Settings.Messages.HistoryMessages = HistoryMessagesCheck.IsChecked == true;
        _workingState.Settings.Messages.ArrowSeekMessages = ArrowSeekMessagesCheck.IsChecked == true;
        _workingState.Settings.Messages.PercentageSeekMessages = PercentageSeekMessagesCheck.IsChecked == true;
        _workingState.Settings.Messages.BookmarkNavigationMessages = BookmarkNavigationMessagesCheck.IsChecked == true;
        _workingState.Settings.Messages.VolumeMessages = VolumeMessagesCheck.IsChecked == true;
        _workingState.Settings.Messages.PlaybackMessages = PlaybackMessagesCheck.IsChecked == true;
        if (Enum.TryParse<PercentageSeekAnnouncementMode>(
                SelectedTag(PercentageSeekAnnouncementCombo, nameof(PercentageSeekAnnouncementMode.Percent)),
                out var percentageSeekAnnouncement))
        {
            _workingState.Settings.Messages.PercentageSeekAnnouncement = percentageSeekAnnouncement;
        }
        _workingState.Settings.Messages.Templates = _messageRows.ToDictionary(
            row => row.EventId,
            row => row.Template ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        _workingState.Settings.Updates.CheckAutomatically = UpdateCheckAutomatically.IsChecked == true;
        _workingState.Settings.Updates.DownloadAutomatically = UpdateDownloadAutomatically.IsChecked == true;
        _workingState.Settings.Updates.InstallOnExit = UpdateInstallOnExit.IsChecked == true;
        _workingState.Settings.Updates.AllowMeteredConnection = UpdateMetered.IsChecked == true;
        _workingState.Settings.Updates.Channel = SelectedTag(UpdateChannelCombo, "stable");
        _workingState.Settings.Lists.FieldOrder = _mediaFieldRows.Select(row => row.Field).ToList();
        _workingState.Settings.SessionSlots = _sessionOrderRows
            .Select((row, index) => (Slot: index + 1, row.SessionId))
            .ToDictionary(entry => entry.Slot, entry => entry.SessionId);
    }

    private void LoadSessionOrder()
    {
        _sessionOrderRows.Clear();
        var normalized = SessionSlotOrder.Normalize(_workingState.Settings.SessionSlots);
        foreach (var pair in normalized.OrderBy(pair => pair.Key))
        {
            _sessionOrderRows.Add(new SessionOrderRow(
                pair.Value,
                SessionSlotOrder.GetDisplayName(pair.Value),
                pair.Key));
        }
        if (_sessionOrderRows.Count > 0) SessionOrderList.SelectedIndex = 0;
    }

    private void MoveSession(int direction)
    {
        var index = SessionOrderList.SelectedIndex;
        if (index < 0) return;
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _sessionOrderRows.Count)
        {
            SessionOrderStatus.Announce(direction < 0
                ? "Ta sesja jest już pierwsza"
                : "Ta sesja jest już ostatnia");
            return;
        }

        var moved = _sessionOrderRows[index];
        SessionOrderList.Focus();
        Keyboard.Focus(SessionOrderList);
        _sessionOrderRows.Move(index, newIndex);
        UpdateSessionOrderSlots();
        SessionOrderList.SelectedIndex = newIndex;
        SessionOrderList.ScrollIntoView(moved);
        FocusSelectedSessionOrderItem();
        SessionOrderStatus.Announce($"{moved.DisplayName}: Ctrl+{moved.Slot}");
    }

    private void UpdateSessionOrderSlots()
    {
        for (var index = 0; index < _sessionOrderRows.Count; index++)
        {
            _sessionOrderRows[index].Slot = index + 1;
        }
    }

    private void FocusSelectedSessionOrderItem()
    {
        SessionOrderList.UpdateLayout();
        if (SessionOrderList.ItemContainerGenerator.ContainerFromItem(SessionOrderList.SelectedItem)
            is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        SessionOrderList.Focus();
        Keyboard.Focus(SessionOrderList);
    }

    private void LoadMediaFieldOrder()
    {
        _mediaFieldRows.Clear();
        foreach (var field in _workingState.Settings.Lists.FieldOrder)
        {
            _mediaFieldRows.Add(new MediaFieldRow(field));
        }
        if (_mediaFieldRows.Count > 0) ListFieldOrderList.SelectedIndex = 0;
        UpdateListFieldPreview();
    }

    private void MoveMediaField(int direction)
    {
        var index = ListFieldOrderList.SelectedIndex;
        if (index < 0) return;
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _mediaFieldRows.Count)
        {
            var edge = direction < 0 ? "pierwszym" : "ostatnim";
            FocusOrderListAndAnnounce($"{_mediaFieldRows[index].Label} jest już {edge} polem");
            return;
        }

        var moved = _mediaFieldRows[index];
        // Anchor keyboard focus in the list before WPF replaces the focused item container.
        // Otherwise focus can briefly fall through to the next tab stop (the order preview).
        ListFieldOrderList.Focus();
        Keyboard.Focus(ListFieldOrderList);
        _mediaFieldRows.Move(index, newIndex);
        ListFieldOrderList.SelectedIndex = newIndex;
        ListFieldOrderList.ScrollIntoView(ListFieldOrderList.SelectedItem);
        FocusSelectedOrderItem();
        UpdateListFieldPreview();
        var neighbor = _mediaFieldRows[direction < 0 ? newIndex + 1 : newIndex - 1];
        var relation = direction < 0 ? "nad" : "pod";
        FocusOrderListAndAnnounce($"{moved.Label} {relation} {neighbor.Label}. {FormatMediaFieldOrder()}");
    }

    private void UpdateListFieldPreview()
    {
        ListFieldPreviewText.Text = FormatMediaFieldOrder();
    }

    private string FormatMediaFieldOrder() =>
        $"Kolejność: {string.Join(", ", _mediaFieldRows.Select(row => row.Label))}";

    private void FocusOrderListAndAnnounce(string message)
    {
        FocusSelectedOrderItem();
        Dispatcher.BeginInvoke(() =>
        {
            ListOrderStatus.Announce(message);
        }, DispatcherPriority.ContextIdle);
    }

    private void FocusSelectedOrderItem()
    {
        ListFieldOrderList.UpdateLayout();
        if (ListFieldOrderList.ItemContainerGenerator.ContainerFromItem(ListFieldOrderList.SelectedItem)
            is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }

        ListFieldOrderList.Focus();
        Keyboard.Focus(ListFieldOrderList);
    }

    private void RefreshProfiles(string? selectedId = null)
    {
        ProfileCombo.ItemsSource = null;
        ProfileCombo.ItemsSource = _workingState.KeyboardProfiles;
        ProfileCombo.SelectedItem = _workingState.KeyboardProfiles.FirstOrDefault(profile => profile.Id == selectedId)
            ?? _workingState.KeyboardProfiles.FirstOrDefault(profile => profile.Id == _workingState.Settings.ActiveKeyboardProfileId)
            ?? _workingState.KeyboardProfiles.First();
        ActiveProfileText.Text = $"Aktywny profil: {_workingState.KeyboardProfiles.First(profile => profile.Id == _workingState.Settings.ActiveKeyboardProfileId).Name}";
        RefreshBindings();
    }

    private void RefreshBindings()
    {
        _bindingRows.Clear();
        if (SelectedProfile is null) return;
        foreach (var pair in SelectedProfile.Bindings.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            _bindingRows.Add(new BindingRow(pair.Key, pair.Value));
        }
    }

    private KeyboardProfile EnsureEditableProfile()
    {
        var profile = SelectedProfile ?? throw new InvalidOperationException("Nie wybrano profilu.");
        if (!profile.IsBuiltIn) return profile;

        var copy = profile.CreateEditableCopy("Mój profil");
        _workingState.KeyboardProfiles.Add(copy);
        RefreshProfiles(copy.Id);
        return copy;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyControls();
            ResultState = _workingState;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Nie można zapisać ustawień", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshBindings();
    private void RecordingFolderMode_Changed(object sender, RoutedEventArgs e) => UpdateRadioRecordingControls();
    private void RadioRecordingFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateRadioRecordingControls();

    private void UpdateRadioRecordingControls()
    {
        if (RadioRecordingsFolderBox is null || RadioRecordingBitrateCombo is null) return;
        var customFolder = CustomRadioFolderOption.IsChecked == true;
        RadioRecordingsFolderBox.IsEnabled = customFolder;
        BrowseRadioRecordingsFolderButton.IsEnabled = customFolder;
        RadioRecordingBitrateCombo.IsEnabled = !string.Equals(
            SelectedTag(RadioRecordingFormatCombo, nameof(RadioRecordingFormat.Mp3)),
            nameof(RadioRecordingFormat.Wav),
            StringComparison.Ordinal);
    }

    private void BrowseRadioRecordingsFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz ogólny folder nagrań radia",
            Multiselect = false
        };
        if (Directory.Exists(RadioRecordingsFolderBox.Text))
            dialog.InitialDirectory = RadioRecordingsFolderBox.Text;
        if (dialog.ShowDialog(this) != true) return;
        CustomRadioFolderOption.IsChecked = true;
        RadioRecordingsFolderBox.Text = dialog.FolderName;
        RadioRecordingsFolderBox.Focus();
        Keyboard.Focus(RadioRecordingsFolderBox);
    }
    private void ListFieldOrderList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateListFieldPreview();
    private void ListFieldOrderList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Up)
        {
            MoveMediaField(-1);
            e.Handled = true;
        }
        else if (key == Key.Down)
        {
            MoveMediaField(1);
            e.Handled = true;
        }
    }
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !SessionOrderList.IsKeyboardFocusWithin) return;
        HandleSessionOrderAltArrow(e);
    }
    private void SessionOrderList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        HandleSessionOrderAltArrow(e);
    }

    private void HandleSessionOrderAltArrow(KeyEventArgs e)
    {
        if (!e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt)) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Up)
        {
            MoveSession(-1);
            e.Handled = true;
        }
        else if (key == Key.Down)
        {
            MoveSession(1);
            e.Handled = true;
        }
    }
    private void MoveSessionUp_Click(object sender, RoutedEventArgs e) => MoveSession(-1);
    private void MoveSessionDown_Click(object sender, RoutedEventArgs e) => MoveSession(1);

    private void RestoreSessionOrder_Click(object sender, RoutedEventArgs e)
    {
        _workingState.Settings.SessionSlots = SessionSlotOrder.CreateDefault();
        LoadSessionOrder();
        FocusSelectedSessionOrderItem();
        SessionOrderStatus.Announce("Przywrócono domyślną kolejność: Ctrl+1 Pliki lokalne, Ctrl+2 WiiM, Ctrl+3 TIDAL, Ctrl+4 Apple Music");
    }
    private void MoveFieldUp_Click(object sender, RoutedEventArgs e) => MoveMediaField(-1);
    private void MoveFieldDown_Click(object sender, RoutedEventArgs e) => MoveMediaField(1);

    private void RestoreFieldOrder_Click(object sender, RoutedEventArgs e)
    {
        _mediaFieldRows.Clear();
        foreach (var field in ListDisplaySettings.CreateDefaultFieldOrder())
        {
            _mediaFieldRows.Add(new MediaFieldRow(field));
        }
        ListFieldOrderList.SelectedIndex = 0;
        UpdateListFieldPreview();
        FocusOrderListAndAnnounce($"Przywrócono domyślną kolejność. {FormatMediaFieldOrder()}");
    }

    private void ActivateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null) return;
        _workingState.Settings.ActiveKeyboardProfileId = SelectedProfile.Id;
        ActiveProfileText.Text = $"Aktywny profil: {SelectedProfile.Name}";
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is null) return;
        var copy = SelectedProfile.CreateEditableCopy($"{SelectedProfile.Name} — kopia");
        _workingState.KeyboardProfiles.Add(copy);
        RefreshProfiles(copy.Id);
    }

    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null || profile.IsBuiltIn)
        {
            MessageBox.Show("Najpierw utwórz edytowalną kopię profilu.", "Profile klawiatury", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ProfileNameWindow(profile.Name) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ProfileName)) return;
        profile.Name = dialog.ProfileName.Trim();
        RefreshProfiles(profile.Id);
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = SelectedProfile;
        if (profile is null || profile.IsBuiltIn)
        {
            MessageBox.Show("Wbudowanego profilu nie można usunąć.", "Profile klawiatury", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"Usunąć profil „{profile.Name}”?", "Profile klawiatury", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _workingState.KeyboardProfiles.Remove(profile);
        if (_workingState.Settings.ActiveKeyboardProfileId == profile.Id) _workingState.Settings.ActiveKeyboardProfileId = "default";
        RefreshProfiles("default");
    }

    private void ChangeBinding_Click(object sender, RoutedEventArgs e)
    {
        if (BindingsList.SelectedItem is not BindingRow row)
        {
            MessageBox.Show("Wybierz przypisanie do zmiany.", "Skróty", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ShortcutCaptureWindow(row.CommandId) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.CapturedChord is not KeyChord chord) return;

        var profile = EnsureEditableProfile();
        if (profile.Bindings.TryGetValue(chord.Canonical, out var conflictingCommand) && conflictingCommand != row.CommandId)
        {
            var replace = MessageBox.Show(
                $"Skrót {chord.Canonical} jest przypisany do polecenia „{CommandCatalog.GetDisplayName(conflictingCommand)}”. Zastąpić go?",
                "Konflikt skrótów",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (replace != MessageBoxResult.Yes) return;
        }

        profile.Bindings.Remove(row.Chord);
        profile.Bindings[chord.Canonical] = row.CommandId;
        RefreshBindings();
    }

    private void RemoveBinding_Click(object sender, RoutedEventArgs e)
    {
        if (BindingsList.SelectedItem is not BindingRow row) return;
        var profile = EnsureEditableProfile();
        profile.Bindings.Remove(row.Chord);
        RefreshBindings();
    }

    private void ImportKeyboardMap_Click(object sender, RoutedEventArgs e)
    {
        RunFileOperation(() =>
        {
            var dialog = OpenDialog("Mapa klawiszy|*.amckeys.json|Pliki JSON|*.json");
            if (dialog.ShowDialog(this) != true) return;
            var profile = _store.ImportKeyboardMap(dialog.FileName);
            _workingState.KeyboardProfiles.Add(profile);
            RefreshProfiles(profile.Id);
        });
    }

    private void ExportKeyboardMap_Click(object sender, RoutedEventArgs e)
    {
        RunFileOperation(() =>
        {
            var profile = SelectedProfile ?? throw new InvalidOperationException("Nie wybrano profilu.");
            var dialog = SaveDialog("Mapa klawiszy|*.amckeys.json", ".amckeys.json", "moja-mapa.amckeys.json");
            if (dialog.ShowDialog(this) == true) _store.ExportKeyboardMap(dialog.FileName, profile);
        });
    }

    private void ImportConfiguration_Click(object sender, RoutedEventArgs e)
    {
        RunFileOperation(() =>
        {
            var dialog = OpenDialog("Konfiguracja programu|*.amcsettings.json|Pliki JSON|*.json");
            if (dialog.ShowDialog(this) != true) return;
            _workingState.Settings = _store.ImportConfiguration(dialog.FileName, _workingState.Settings.ActiveKeyboardProfileId);
            LoadControls();
        });
    }

    private void ExportConfiguration_Click(object sender, RoutedEventArgs e)
    {
        RunFileOperation(() =>
        {
            ApplyControls();
            var dialog = SaveDialog("Konfiguracja programu|*.amcsettings.json", ".amcsettings.json", "konfiguracja.amcsettings.json");
            if (dialog.ShowDialog(this) == true) _store.ExportConfiguration(dialog.FileName, _workingState.Settings);
        });
    }

    private void ImportFullBackup_Click(object sender, RoutedEventArgs e)
    {
        RunFileOperation(() =>
        {
            var dialog = OpenDialog("Pełna kopia programu|*.amcbackup.json|Pliki JSON|*.json");
            if (dialog.ShowDialog(this) != true) return;
            _workingState = _store.ImportFullBackup(dialog.FileName);
            LoadControls();
        });
    }

    private void ExportFullBackup_Click(object sender, RoutedEventArgs e)
    {
        RunFileOperation(() =>
        {
            ApplyControls();
            var dialog = SaveDialog("Pełna kopia programu|*.amcbackup.json", ".amcbackup.json", "pelna-kopia.amcbackup.json");
            if (dialog.ShowDialog(this) == true) _store.ExportFullBackup(dialog.FileName, _workingState);
        });
    }

    private void RunFileOperation(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Import lub eksport", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static OpenFileDialog OpenDialog(string filter) => new() { Filter = filter, CheckFileExists = true };

    private static SaveFileDialog SaveDialog(string filter, string extension, string fileName) => new()
    {
        Filter = filter,
        DefaultExt = extension,
        AddExtension = true,
        FileName = fileName
    };

    private static string SelectedTag(ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private static void SelectComboByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            ?? (ComboBoxItem)comboBox.Items[0];
    }

    private static int MessageTemplateSortOrder(string eventId) => eventId switch
    {
        "session.changed" => 0,
        "session.unassigned" => 1,
        "favorite.added" => 10,
        "favorite.removed" => 11,
        "queue.added" => 20,
        "queue.removed" => 21,
        "playNext.added" => 30,
        "playNext.removed" => 31,
        "volume.changed" => 40,
        "time.elapsed" => 50,
        "time.remaining" => 51,
        "time.total" => 52,
        "command.unavailable" => 1000,
        _ => 500
    };

    private sealed record BindingRow(string Chord, string CommandId)
    {
        public string Label => $"{Chord} — {CommandCatalog.GetDisplayName(CommandId)}";
        public override string ToString() => Label;
    }

    private sealed record MediaFieldRow(MediaItemField Field)
    {
        public string Label => MediaItemFormatter.GetFieldDisplayName(Field);
        public override string ToString() => Label;
    }

    private sealed class SessionOrderRow(string sessionId, string displayName, int slot) : INotifyPropertyChanged
    {
        private int _slot = slot;
        public string SessionId { get; } = sessionId;
        public string DisplayName { get; } = displayName;
        public int Slot
        {
            get => _slot;
            set
            {
                if (_slot == value) return;
                _slot = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slot)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }
        public string Label => $"{Slot}, {DisplayName}, Ctrl+{Slot}";
        public event PropertyChangedEventHandler? PropertyChanged;
        public override string ToString() => Label;
    }

    private sealed class MessageTemplateRow(string eventId, string template)
    {
        public string EventId { get; } = eventId;
        public string EventLabel => EventId switch
        {
            "session.changed" => "Zmiana sesji",
            "session.unassigned" => "Nieprzypisana sesja",
            "favorite.added" => "Dodanie do ulubionych",
            "favorite.removed" => "Usunięcie z ulubionych",
            "queue.added" => "Dodanie do kolejki",
            "queue.removed" => "Usunięcie z kolejki",
            "playNext.added" => "Ustawienie odtwarzania jako następne",
            "playNext.removed" => "Usunięcie z odtwarzanych jako następne",
            "volume.changed" => "Zmiana głośności",
            "time.elapsed" => "Czas od początku",
            "time.remaining" => "Czas pozostały",
            "time.total" => "Czas całkowity",
            "command.unavailable" => "Błąd: polecenie niedostępne",
            _ => EventId
        };
        public string? Template { get; set; } = template;
        // WPF exposes ToString() as the UI Automation name for data items even
        // when DisplayMemberPath is set. Keep technical placeholders in the
        // separate edit box, but expose only the friendly event name in the list.
        public override string ToString() => EventLabel;
    }
}
