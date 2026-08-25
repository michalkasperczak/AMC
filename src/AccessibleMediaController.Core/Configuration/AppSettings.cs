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

public enum ResumePositionMode
{
    Inherit,
    Remember,
    StartFromBeginning
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
    public bool PausePlaybackWhenLeavingPlayer { get; set; } = true;
    public bool RememberLocalPlaybackPositions { get; set; } = true;
    public string LastSessionId { get; set; } = "tidal";
    public Dictionary<int, string> SessionSlots { get; set; } = SessionSlotOrder.CreateDefault();
    public ListDisplaySettings Lists { get; set; } = new();
    public MessageSettings Messages { get; set; } = MessageSettings.CreateDefault();
    public UpdateSettings Updates { get; set; } = new();
}

public static class SessionSlotOrder
{
    private static readonly (string Id, string DisplayName)[] KnownSessions =
    [
        ("local", "Pliki lokalne"),
        ("wiim", "WiiM"),
        ("tidal", "TIDAL"),
        ("appleMusic", "Apple Music")
    ];

    public static IReadOnlyList<string> DefaultSessionIds =>
        KnownSessions.Select(session => session.Id).ToArray();

    public static Dictionary<int, string> CreateDefault() => new()
    {
        [1] = "local",
        [2] = "wiim",
        [3] = "tidal",
        [4] = "appleMusic"
    };

    public static Dictionary<int, string> Normalize(IReadOnlyDictionary<int, string>? slots)
    {
        var orderedIds = (slots ?? new Dictionary<int, string>())
            .Where(pair => pair.Key is >= 1 and <= 9 && !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(9)
            .ToList();

        foreach (var session in KnownSessions)
        {
            if (orderedIds.Count >= 9) break;
            if (!orderedIds.Contains(session.Id, StringComparer.OrdinalIgnoreCase))
            {
                orderedIds.Add(session.Id);
            }
        }

        return orderedIds
            .Select((sessionId, index) => (Slot: index + 1, SessionId: sessionId))
            .ToDictionary(entry => entry.Slot, entry => entry.SessionId);
    }

    public static string GetDisplayName(string sessionId) =>
        KnownSessions.FirstOrDefault(session =>
            string.Equals(session.Id, sessionId, StringComparison.OrdinalIgnoreCase)).DisplayName
        ?? sessionId;
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
    public bool BookmarkNavigationMessages { get; set; } = true;
    public PercentageSeekAnnouncementMode PercentageSeekAnnouncement { get; set; } = PercentageSeekAnnouncementMode.Percent;
    public bool SessionMessages { get; set; } = true;
    public bool HistoryMessages { get; set; } = true;
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
    public int SchemaVersion { get; set; } = 25;
    public AppSettings Settings { get; set; } = new();
    public SearchHistorySettings SearchHistory { get; set; } = new();
    public PlaybackHistorySettings PlaybackHistory { get; set; } = new();
    public BookmarkSettings Bookmarks { get; set; } = new();
    public SessionNavigationSettings SessionNavigation { get; set; } = new();
    public CollectionOrderSettings CollectionOrders { get; set; } = new();
    public LocalMediaSettings LocalMedia { get; set; } = new();
    public List<Input.KeyboardProfile> KeyboardProfiles { get; set; } = [Input.KeyboardProfile.CreateDefault()];
}

public sealed class SearchHistorySettings
{
    public Dictionary<string, List<string>> Entries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlaybackHistorySettings
{
    public Dictionary<string, List<string>> ItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BookmarkSettings
{
    public List<BookmarkEntry> Entries { get; set; } = [];
}

public sealed class BookmarkEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PositionTicks { get; set; }
    public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public sealed class SessionNavigationSettings
{
    public Dictionary<string, SessionNavigationState> Sessions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SessionNavigationState
{
    public string CurrentView { get; set; } = "Multimedia";
    public Dictionary<string, string?> SelectedItemIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Filters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public string PlaybackContextView { get; set; } = "Multimedia";
    public List<string> PlaybackContextItemIds { get; set; } = [];
    public bool PlayerActive { get; set; }
}

public sealed class CollectionOrderSettings
{
    public Dictionary<string, List<string>> FavoriteItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LocalMediaSettings
{
    public List<LocalMediaItemSettings> Items { get; set; } = [];
    public List<LocalFolderSourceSettings> FolderSources { get; set; } = [];
    public List<LocalFolderPlaybackSettings> FolderPlaybackOptions { get; set; } = [];
    public List<string> ExcludedPaths { get; set; } = [];
    public List<string> CustomOrderItemIds { get; set; } = [];
    public string LibraryView { get; set; } = "Foldery";
    public string? CurrentFolderPath { get; set; }
    public string? CurrentItemId { get; set; }
    public int Volume { get; set; } = 35;
    public double PlaybackRate { get; set; } = 1d;
}

public sealed class LocalFolderSourceSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
}

public sealed class LocalFolderPlaybackSettings
{
    public string Path { get; set; } = string.Empty;
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public string? OutputDeviceId { get; set; }
}

public sealed class LocalMediaItemSettings
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool HasCustomTitle { get; set; }
    public string Path { get; set; } = string.Empty;
    public long DurationTicks { get; set; }
    public int? BitrateKbps { get; set; }
    public bool IsBitrateEstimated { get; set; }
    public int? SampleRateHz { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public string? OutputDeviceId { get; set; }
    public long ResumePositionTicks { get; set; }
    public long? FileLength { get; set; }
    public long? LastWriteUtcTicks { get; set; }
}
