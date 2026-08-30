using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Commands;

public static class CommandIds
{
    private const string SeekPercentPrefix = "transport.seekPercent.";
    private const string RadioPresetPrefix = "radio.preset.activate.";

    public const string PlayPause = "transport.playPause";
    public const string ActivateSelected = "transport.activateSelected";
    public const string Previous = "transport.previous";
    public const string Next = "transport.next";
    public const string SeekBackward10 = "transport.seekBackward10";
    public const string SeekForward10 = "transport.seekForward10";
    public const string SeekBackward30 = "transport.seekBackward30";
    public const string SeekForward30 = "transport.seekForward30";
    public const string SeekBackward60 = "transport.seekBackward60";
    public const string SeekForward60 = "transport.seekForward60";
    public const string VolumeUp5 = "transport.volumeUp5";
    public const string VolumeDown5 = "transport.volumeDown5";
    public const string VolumeUp1 = "transport.volumeUp1";
    public const string VolumeDown1 = "transport.volumeDown1";
    public const string ToggleMuteCurrentSession = "transport.mute.currentSession";
    public const string ToggleMuteAllSessions = "transport.mute.allSessions";
    public const string PlaybackRateDown = "transport.playbackRate.down";
    public const string PlaybackRateUp = "transport.playbackRate.up";
    public const string PlaybackRateReset = "transport.playbackRate.reset";
    public const string TrackStart = "transport.trackStart";
    public const string TrackEnd = "transport.trackEnd";
    public const string SeekToTime = "transport.seekToTime";
    public const string SeekToPercentage = "transport.seekToPercentage";

    public const string TimeElapsed = "information.timeElapsed";
    public const string TimeRemaining = "information.timeRemaining";
    public const string TimeTotal = "information.timeTotal";
    public const string ItemProperties = "information.itemProperties";
    public const string ItemPlaybackOptions = "settings.itemPlaybackOptions";

    public const string SessionList = "session.list";
    public const string SessionPrevious = "session.previous";
    public const string SessionNext = "session.next";

    public const string ViewFavorites = "view.favorites";
    public const string ToggleFavorite = "action.favorite.toggle";
    public const string ViewPlaylists = "view.playlists";
    public const string ManagePlaylists = "action.playlists.manageMembership";
    public const string SearchCurrent = "view.search.current";
    public const string SearchAll = "view.search.all";
    public const string FilterCurrent = "view.filter.current";
    public const string CommandPalette = "view.commandPalette";
    public const string ViewLibrary = "view.library";
    public const string ViewFolders = "view.folders";
    public const string ViewAllLocalFiles = "view.local.allFiles";
    public const string ViewCustomLocalOrder = "view.local.customOrder";
    public const string RefreshLocalLibrary = "local.library.refresh";
    public const string ManageLocalSources = "local.library.manageSources";
    public const string RenameLibraryItem = "local.library.renameItem";
    public const string RenameLocalFile = "local.file.rename";
    public const string MoveLocalLibraryItemUp = "local.library.moveItemUp";
    public const string MoveLocalLibraryItemDown = "local.library.moveItemDown";
    public const string ToggleLibrary = "action.library.toggle";
    public const string ViewQueue = "view.queue";
    public const string AddQueue = "action.queue.add";
    public const string TogglePlayNext = "action.queue.playNextToggle";
    public const string ViewAlbums = "view.albums";
    public const string ViewRadio = "view.radio";
    public const string StartRadio = "action.radio.start";
    public const string AddRadioStation = "radio.station.add";
    public const string ImportRadioPlaylist = "radio.playlist.import";
    public const string ToggleRadioRecording = "radio.recording.toggle";
    public const string ToggleRadioRecordingPause = "radio.recording.pauseToggle";
    public const string SplitRadioRecording = "radio.recording.split";
    public const string StopAllRadioRecordings = "radio.recording.stopAll";
    public const string AddRadioSchedule = "radio.recording.schedule.add";
    public const string ManageRadioSchedules = "radio.recording.schedules";
    public const string ViewActiveRadioRecordings = "radio.recording.active";
    public const string RadioJumpLive = "radio.timeshift.live";
    public const string ViewRadioPresets = "radio.presets.view";
    public const string AssignRadioPreset = "radio.presets.assign";
    public const string ViewMixes = "view.mixes";
    public const string ViewHistory = "view.history";
    public const string ViewBookmarks = "view.bookmarks";
    public const string AddBookmark = "action.bookmark.add";
    public const string AddNamedBookmark = "action.bookmark.addNamed";
    public const string PreviousBookmark = "transport.bookmark.previous";
    public const string NextBookmark = "transport.bookmark.next";
    public const string ViewNowPlaying = "view.nowPlaying";
    public const string OpenOfficialApp = "action.openOfficialApp";
    public const string ViewOutputs = "view.outputs";
    public const string ViewDownloads = "view.downloads";
    public const string DownloadInService = "action.download.inService";
    public const string DownloadToDisk = "action.download.toDisk";
    public const string Help = "view.help";
    public const string KeyboardHelp = "view.keyboardHelp";
    public const string OpenLocalFiles = "local.openFiles";
    public const string OpenLocalFolder = "local.openFolder";

    public const string SettingsGeneral = "settings.general";
    public const string SettingsLanguage = "settings.language";
    public const string SettingsStartupTarget = "settings.startupTarget";
    public const string SettingsSessionOrder = "settings.sessionOrder";
    public const string SettingsPausePlaybackWhenLeavingPlayer = "settings.playback.pauseWhenLeavingPlayer";
    public const string SettingsFollowPlaybackOnPlayerExit = "settings.playback.followOnPlayerExit";
    public const string SettingsOpenPlayerWhenActivatingPreset = "settings.playback.openPlayerForPreset";
    public const string SettingsRememberLocalPlaybackPositions = "settings.playback.rememberLocalPositions";
    public const string SettingsPrefix = "settings.prefix";
    public const string SettingsPrefixTimeout = "settings.prefixTimeout";
    public const string SettingsKeyboardProfile = "settings.keyboardProfile";
    public const string SettingsActivateKeyboardProfile = "settings.keyboardProfile.activate";
    public const string SettingsDuplicateKeyboardProfile = "settings.keyboardProfile.duplicate";
    public const string SettingsRenameKeyboardProfile = "settings.keyboardProfile.rename";
    public const string SettingsDeleteKeyboardProfile = "settings.keyboardProfile.delete";
    public const string SettingsImportKeyboardMap = "settings.keyboardMap.import";
    public const string SettingsExportKeyboardMap = "settings.keyboardMap.export";
    public const string SettingsKeyboardBindings = "settings.keyboardBindings";
    public const string SettingsChangeKeyboardBinding = "settings.keyboardBinding.change";
    public const string SettingsRemoveKeyboardBinding = "settings.keyboardBinding.remove";
    public const string SettingsListFieldOrder = "settings.listFieldOrder";
    public const string SettingsImportExport = "settings.importExport";
    public const string SettingsImportConfiguration = "settings.configuration.import";
    public const string SettingsExportConfiguration = "settings.configuration.export";
    public const string SettingsImportFullBackup = "settings.fullBackup.import";
    public const string SettingsExportFullBackup = "settings.fullBackup.export";
    public const string SettingsMessages = "settings.messages";
    public const string SettingsHistoryMessages = "settings.historyMessages";
    public const string SettingsToggleMessages = "settings.messages.toggle";
    public const string SettingsToggleDetailedHints = "settings.detailedHints.toggle";
    public const string SettingsToggleSeekMessages = "settings.seekMessages.toggle";
    public const string SettingsArrowSeekMessages = "settings.arrowSeekMessages";
    public const string SettingsPercentageSeekMessages = "settings.percentageSeekMessages";
    public const string SettingsBookmarkNavigationMessages = "settings.bookmarkNavigationMessages";
    public const string SettingsVolumeMessages = "settings.volumeMessages";
    public const string SettingsPlaybackMessages = "settings.playbackMessages";
    public const string SettingsPercentageSeekAnnouncement = "settings.percentageSeekAnnouncement";
    public const string SettingsMessageTemplates = "settings.messageTemplates";
    public const string SettingsUpdates = "settings.updates";

    public static string SessionSlot(int slot) => $"session.slot.{slot}";

    public static string SeekPercent(int percent)
    {
        if (percent is < 0 or > 90 || percent % 10 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Procent musi należeć do zakresu 0–90 i być wielokrotnością 10.");
        }

        return $"{SeekPercentPrefix}{percent}";
    }

    public static bool TryParseSeekPercent(string commandId, out int percent)
    {
        percent = 0;
        return commandId.StartsWith(SeekPercentPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(SeekPercentPrefix.Length), out percent)
            && percent is >= 0 and <= 90
            && percent % 10 == 0;
    }

    public static string RadioPreset(int slot)
    {
        if (slot is < 1 or > RadioPresetSlots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return $"{RadioPresetPrefix}{slot}";
    }

    public static bool TryParseRadioPreset(string commandId, out int slot)
    {
        slot = 0;
        return commandId.StartsWith(RadioPresetPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(RadioPresetPrefix.Length), out slot)
            && slot is >= 1 and <= RadioPresetSlots.Count;
    }
}
