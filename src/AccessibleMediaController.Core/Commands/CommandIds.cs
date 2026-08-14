namespace AccessibleMediaController.Core.Commands;

public static class CommandIds
{
    public const string PlayPause = "transport.playPause";
    public const string PlaySelected = "transport.playSelected";
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

    public static string SessionSlot(int slot) => $"session.slot.{slot}";
}
