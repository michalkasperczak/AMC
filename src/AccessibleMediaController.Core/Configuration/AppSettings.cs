using AccessibleMediaController.Core.Presentation;

namespace AccessibleMediaController.Core.Configuration;

public enum StartupTarget
{
    MediaList,
    SessionList
}

public enum PercentageSeekAnnouncementMode
{
    Percent,
    Time,
    PercentAndTime
}

public sealed class AppSettings
{
    public string InterfaceLanguage { get; set; } = "pl-PL";
    public string PrefixChord { get; set; } = "Ctrl+Alt+Windows+F12";
    public int PrefixTimeoutMilliseconds { get; set; } = 3000;
    public int SessionContinuationMilliseconds { get; set; } = 2000;
    public StartupTarget StartupTarget { get; set; } = StartupTarget.MediaList;
    public string ActiveKeyboardProfileId { get; set; } = "default";
    public bool RememberLastSession { get; set; } = true;
    public string LastSessionId { get; set; } = "tidal";
    public Dictionary<int, string> SessionSlots { get; set; } = new()
    {
        [1] = "tidal",
        [2] = "appleMusic",
        [3] = "wiim"
    };
    public ListDisplaySettings Lists { get; set; } = new();
    public MessageSettings Messages { get; set; } = MessageSettings.CreateDefault();
    public UpdateSettings Updates { get; set; } = new();
}

public sealed class ListDisplaySettings
{
    public List<MediaItemField> FieldOrder { get; set; } = CreateDefaultFieldOrder();

    public static List<MediaItemField> CreateDefaultFieldOrder() =>
    [
        MediaItemField.Title,
        MediaItemField.Artist,
        MediaItemField.Duration,
        MediaItemField.Kind
    ];
}

public sealed class UpdateSettings
{
    public bool CheckAutomatically { get; set; } = true;
    public bool DownloadAutomatically { get; set; } = true;
    public bool InstallOnExit { get; set; } = true;
    public bool AllowMeteredConnection { get; set; }
    public string Channel { get; set; } = "stable";
}

public sealed class MessageSettings
{
    public bool Enabled { get; set; } = true;
    public bool DetailedHints { get; set; }
    public bool SeekMessages { get; set; } = true;
    public bool ArrowSeekMessages { get; set; } = true;
    public bool PercentageSeekMessages { get; set; } = true;
    public PercentageSeekAnnouncementMode PercentageSeekAnnouncement { get; set; } = PercentageSeekAnnouncementMode.Percent;
    public bool SessionMessages { get; set; } = true;
    public bool PlaybackMessages { get; set; } = true;
    public bool VolumeMessages { get; set; } = true;
    public bool LoadingMessages { get; set; } = true;
    public bool ErrorMessages { get; set; } = true;
    public Dictionary<string, string> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static MessageSettings CreateDefault()
    {
        return new MessageSettings
        {
            Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["session.changed"] = "{slot}, {service}",
                ["session.unassigned"] = "Sesja {slot} nieprzypisana",
                ["favorite.added"] = "Dodano do ulubionych: {item}",
                ["favorite.removed"] = "Usunięto z ulubionych: {item}",
                ["queue.added"] = "Dodano do kolejki: {item}",
                ["queue.removed"] = "Usunięto z kolejki: {item}",
                ["playNext.added"] = "Odtwarzaj jako następne: {item}",
                ["playNext.removed"] = "Usunięto z następnych: {item}",
                ["volume.changed"] = "{value}%",
                ["time.elapsed"] = "{elapsed}",
                ["time.remaining"] = "{remaining}",
                ["time.total"] = "{total}",
                ["command.unavailable"] = "Polecenie niedostępne"
            }
        };
    }
}

public sealed class PersistedState
{
    public int SchemaVersion { get; set; } = 10;
    public AppSettings Settings { get; set; } = new();
    public SearchHistorySettings SearchHistory { get; set; } = new();
    public List<Input.KeyboardProfile> KeyboardProfiles { get; set; } = [Input.KeyboardProfile.CreateDefault()];
}

public sealed class SearchHistorySettings
{
    public Dictionary<string, List<string>> Entries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
