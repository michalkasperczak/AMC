namespace AccessibleMediaController.Core.Commands;

public static class CommandIds
{
    public const string PlayPause = "transport.playPause";
    public const string ActivateSelected = "transport.activateSelected";
    public const string Previous = "transport.previous";
    public const string Next = "transport.next";
    public const string SeekBackward10 = "transport.seekBackward10";
    public const string SeekForward10 = "transport.seekForward10";
    public const string SeekBackward60 = "transport.seekBackward60";
    public const string SeekForward60 = "transport.seekForward60";
    public const string VolumeUp5 = "transport.volumeUp5";
    public const string VolumeDown5 = "transport.volumeDown5";
    public const string VolumeUp1 = "transport.volumeUp1";
    public const string VolumeDown1 = "transport.volumeDown1";
    public const string TrackStart = "transport.trackStart";
    public const string TrackEnd = "transport.trackEnd";

    public const string TimeElapsed = "information.timeElapsed";
    public const string TimeRemaining = "information.timeRemaining";
    public const string TimeTotal = "information.timeTotal";

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
    public const string ToggleLibrary = "action.library.toggle";
    public const string ViewQueue = "view.queue";
    public const string AddQueue = "action.queue.add";
    public const string TogglePlayNext = "action.queue.playNextToggle";
    public const string ViewAlbums = "view.albums";
    public const string ViewRadio = "view.radio";
    public const string StartRadio = "action.radio.start";
    public const string ViewMixes = "view.mixes";
    public const string ViewHistory = "view.history";
    public const string ViewNowPlaying = "view.nowPlaying";
    public const string OpenOfficialApp = "action.openOfficialApp";
    public const string ItemInformation = "view.itemInformation";
    public const string ExtendedInformation = "view.extendedInformation";
    public const string ViewOutputs = "view.outputs";
    public const string ViewDownloads = "view.downloads";
    public const string DownloadInService = "action.download.inService";
    public const string DownloadToDisk = "action.download.toDisk";
    public const string Help = "view.help";

    public const string SettingsGeneral = "settings.general";
    public const string SettingsLanguage = "settings.language";
    public const string SettingsStartupTarget = "settings.startupTarget";
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
    public const string SettingsToggleMessages = "settings.messages.toggle";
    public const string SettingsToggleDetailedHints = "settings.detailedHints.toggle";
    public const string SettingsMessageTemplates = "settings.messageTemplates";
    public const string SettingsUpdates = "settings.updates";

    public static string SessionSlot(int slot) => $"session.slot.{slot}";
}
