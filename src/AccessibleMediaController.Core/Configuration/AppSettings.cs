using System.Globalization;
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

public enum CollectionSortMode
{
    AddedNewest,
    Alphabetical,
    Custom
}

public enum RadioRecognitionScope
{
    CurrentStation,
    RecordingStations,
    CurrentAndRecordingStations
}

public static class RadioRecognitionScopeRules
{
    public static bool IncludesCurrentStation(RadioRecognitionScope scope) =>
        scope is RadioRecognitionScope.CurrentStation
            or RadioRecognitionScope.CurrentAndRecordingStations;

    public static bool IncludesRecordingStations(RadioRecognitionScope scope) =>
        scope is RadioRecognitionScope.RecordingStations
            or RadioRecognitionScope.CurrentAndRecordingStations;

    public static string GetLabel(RadioRecognitionScope scope) => scope switch
    {
        RadioRecognitionScope.RecordingStations => "tylko stacje nagrywane w tle",
        RadioRecognitionScope.CurrentAndRecordingStations =>
            "aktualnie odtwarzana stacja i wszystkie stacje nagrywane w tle",
        _ => "tylko aktualnie odtwarzana stacja"
    };
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
    public bool FollowPlaybackOnPlayerExit { get; set; } = true;
    public bool OpenPlayerWhenActivatingPreset { get; set; }
    public bool RememberLocalPlaybackPositions { get; set; } = true;
    public PlaybackAudioSettings Audio { get; set; } = new();
    public string LastSessionId { get; set; } = "tidal";
    public Dictionary<int, string> SessionSlots { get; set; } = SessionSlotOrder.CreateDefault();
    public ListDisplaySettings Lists { get; set; } = new();
    public MessageSettings Messages { get; set; } = MessageSettings.CreateDefault();
    public UpdateSettings Updates { get; set; } = new();
}

public sealed class PlaybackAudioSettings
{
    public bool LoudnessNormalizationEnabled { get; set; }
    public bool SmoothTrackTransitionsEnabled { get; set; }
    public int InterTrackSilenceMilliseconds { get; set; }
    public bool AllSessionsMuted { get; set; }
    public Dictionary<string, bool> SessionMutedById { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> OutputDeviceIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class PlaybackAudioSettingsRules
{
    public static readonly IReadOnlyList<int> SupportedInterTrackSilenceMilliseconds =
        [0, 500, 1000, 2000, 3000, 5000];

    public static bool IsSupportedSilence(int milliseconds) =>
        SupportedInterTrackSilenceMilliseconds.Contains(milliseconds);

    public static string GetInterTrackSilenceLabel(int milliseconds) => milliseconds switch
    {
        500 => "pół sekundy",
        1000 => "1 sekunda",
        2000 => "2 sekundy",
        3000 => "3 sekundy",
        5000 => "5 sekund",
        _ => "bez dodatkowej ciszy"
    };
}

public static class SessionSlotOrder
{
    private static readonly (string Id, string DisplayName)[] KnownSessions =
    [
        ("local", "Pliki lokalne"),
        ("wiim", "WiiM"),
        ("tidal", "TIDAL"),
        ("appleMusic", "Apple Music"),
        ("radio", "Radio internetowe"),
        ("podcasts", "Podcasty")
    ];

    public static IReadOnlyList<string> DefaultSessionIds =>
        KnownSessions.Select(session => session.Id).ToArray();

    public static Dictionary<int, string> CreateDefault() => new()
    {
        [1] = "local",
        [2] = "wiim",
        [3] = "tidal",
        [4] = "appleMusic",
        [5] = "radio",
        [6] = "podcasts"
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
    public bool AutomaticRecognitionMessages { get; set; } = true;
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
    public int SchemaVersion { get; set; } = 48;
    public AppSettings Settings { get; set; } = new();
    public SearchHistorySettings SearchHistory { get; set; } = new();
    public PlaybackHistorySettings PlaybackHistory { get; set; } = new();
    public BookmarkSettings Bookmarks { get; set; } = new();
    public SessionNavigationSettings SessionNavigation { get; set; } = new();
    public CollectionOrderSettings CollectionOrders { get; set; } = new();
    public PlaylistSettings Playlists { get; set; } = new();
    public SessionPresetSettings SessionPresets { get; set; } = new();
    public PlaybackVolumeMemorySettings PlaybackVolumes { get; set; } = new();
    public LocalMediaSettings LocalMedia { get; set; } = new();
    public RadioSettings Radio { get; set; } = new();
    public PodcastSettings Podcasts { get; set; } = new();
    public WiiMSettings WiiM { get; set; } = new();
    public List<Input.KeyboardProfile> KeyboardProfiles { get; set; } = [Input.KeyboardProfile.CreateDefault()];
}

public sealed class WiiMSettings
{
    public List<WiiMDeviceSettings> Devices { get; set; } = [];
    public string? SelectedDeviceId { get; set; }
}

public sealed class WiiMDeviceSettings
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Firmware { get; set; } = string.Empty;
    public long LastSeenUtcTicks { get; set; }
    public int LastActivatedPresetNumber { get; set; }
}

public sealed class PlaybackVolumeMemorySettings
{
    public List<PlaybackVolumeMemoryEntry> Entries { get; set; } = [];
}

public sealed class PlaybackVolumeMemoryEntry
{
    public string SessionId { get; set; } = string.Empty;
    public string ContextId { get; set; } = string.Empty;
    public string OutputDeviceId { get; set; } = string.Empty;
    public int Volume { get; set; } = 35;
}

public sealed class PodcastSettings
{
    public List<PodcastSubscriptionSettings> Subscriptions { get; set; } = [];
    public List<PodcastEpisodeSettings> Episodes { get; set; } = [];
    public string? DownloadsFolder { get; set; }
    public string? CurrentItemId { get; set; }
    public int Volume { get; set; } = 35;
    public double PlaybackRate { get; set; } = 1d;
}

public sealed class PodcastSubscriptionSettings
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool HasCustomTitle { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string? HomepageUrl { get; set; }
    public long LastRefreshUtcTicks { get; set; }
    public int RefreshIntervalMinutes { get; set; }
    public string? DownloadsFolder { get; set; }
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; } = true;
}

public sealed class PodcastEpisodeSettings
{
    public string Id { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string SourceIdentifier { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public string? MediaType { get; set; }
    public long? MediaLength { get; set; }
    public string? ProviderChaptersUrl { get; set; }
    public string? ProviderChaptersLoadedUrl { get; set; }
    public string? EmbeddedChaptersSignature { get; set; }
    public bool HasFeedChapters { get; set; }
    public long PublishedUtcTicks { get; set; }
    public long DurationTicks { get; set; }
    public long ResumePositionTicks { get; set; }
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
    public string? DownloadPath { get; set; }
    public bool IsNew { get; set; } = true;
    public bool IsStarted { get; set; }
    public bool IsPlayed { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }
    public long? ClipStartTicks { get; set; }
    public long? ClipEndTicks { get; set; }
}

public sealed class SessionPresetSettings
{
    public Dictionary<string, List<SessionPresetEntry>> EntriesBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SessionPresetEntry
{
    public int Slot { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string TargetKind { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public string? TargetLocation { get; set; }
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

[Flags]
public enum BookmarkPurpose
{
    None = 0,
    Bookmark = 1,
    Chapter = 2
}

public enum ChapterOrigin
{
    User,
    Provider
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
    public BookmarkPurpose Purpose { get; set; } = BookmarkPurpose.Bookmark;
    public ChapterOrigin ChapterOrigin { get; set; } = ChapterOrigin.User;
    public string? ChapterSourceId { get; set; }
}

public sealed class SessionNavigationSettings
{
    public Dictionary<string, SessionNavigationState> Sessions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SessionNavigationState
{
    public string CurrentView { get; set; } = "Multimedia";
    public string LastLibraryView { get; set; } = "Biblioteka";
    public Dictionary<string, string?> SelectedItemIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Filters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CollectionSortMode> CollectionSortModes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public string PlaybackContextView { get; set; } = "Multimedia";
    public List<string> PlaybackContextItemIds { get; set; } = [];
    public bool PlayerActive { get; set; }
}

public sealed class CollectionOrderSettings
{
    public Dictionary<string, List<string>> FavoriteAddedItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> FavoriteItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> LibraryAddedItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> LibraryItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> QueueItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlaylistSettings
{
    public List<PlaylistEntry> Entries { get; set; } = [];
}

public sealed class PlaylistEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public List<string> ItemIds { get; set; } = [];
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
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
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
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
    public long ResumePositionTicks { get; set; }
    public long? ClipStartTicks { get; set; }
    public long? ClipEndTicks { get; set; }
    public long? FileLength { get; set; }
    public long? LastWriteUtcTicks { get; set; }
}

public sealed class RadioSettings
{
    public List<RadioStationSettings> Stations { get; set; } = [];
    public List<RadioPresetSettings> Presets { get; set; } = [];
    public string? CurrentItemId { get; set; }
    public int Volume { get; set; } = 35;
    public int TimeshiftMinutes { get; set; } = 10;
    public string RecordingsFolder { get; set; } = string.Empty;
    public bool UsePodcastDownloadsFolderForRecordings { get; set; }
    public RadioRecordingFormat RecordingFormat { get; set; } = RadioRecordingFormat.Mp3;
    public int RecordingBitrateKbps { get; set; } = 192;
    public bool WakeScheduledRecordings { get; set; }
    public bool AutomaticTrackRecognitionEnabled { get; set; }
    public RadioRecognitionScope AutomaticTrackRecognitionScope { get; set; } =
        RadioRecognitionScope.CurrentStation;
    public List<RadioRecordingScheduleSettings> RecordingSchedules { get; set; } = [];
    public List<RadioRecognizedTrackSettings> RecognizedTracks { get; set; } = [];
}

public sealed class RadioRecognizedTrackSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string? ProviderUri { get; set; }
    public long RecognizedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public enum RadioRecordingFormat
{
    Mp3,
    Aac,
    Flac,
    Original,
    Wav
}

public enum RadioScheduleRecurrence
{
    Once,
    Daily,
    SelectedDays
}

public sealed class RadioRecordingScheduleSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public long NextStartUtcTicks { get; set; }
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public int DurationMinutes { get; set; } = 60;
    public int SegmentMinutes { get; set; }
    public RadioScheduleRecurrence Recurrence { get; set; }
    public List<DayOfWeek> ActiveDays { get; set; } = [];
    public string OutputFolder { get; set; } = string.Empty;
    public string FileNameTemplate { get; set; } = RadioRecordingFileNameTemplate.DefaultTemplate;
    public RadioRecordingFormat? RecordingFormat { get; set; }
    public int? RecordingBitrateKbps { get; set; }
    public bool? WakeComputer { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class RadioPresetSettings
{
    public int Slot { get; set; }
    public string StationId { get; set; } = string.Empty;
}

public static class RadioPresetSlots
{
    public const int Count = 12;

    public static string Label(int slot)
    {
        if (slot is < 1 or > Count) throw new ArgumentOutOfRangeException(nameof(slot));
        return slot.ToString(CultureInfo.InvariantCulture);
    }

    public static string ShortcutLabel(int slot) => slot switch
    {
        >= 1 and <= 9 => slot.ToString(CultureInfo.InvariantCulture),
        10 => "0",
        11 => "-",
        12 => "=",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string SpokenShortcutLabel(int slot) => slot switch
    {
        >= 1 and <= 10 => ShortcutLabel(slot),
        11 => "minus",
        12 => "znak równości",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static IReadOnlyList<RadioPresetSettings> Normalize(
        IEnumerable<RadioPresetSettings>? presets,
        IReadOnlySet<string> stationIds) =>
        (presets ?? [])
            .Where(preset => preset.Slot is >= 1 and <= Count
                && !string.IsNullOrWhiteSpace(preset.StationId)
                && stationIds.Contains(preset.StationId.Trim()))
            .Select(preset => new RadioPresetSettings
            {
                Slot = preset.Slot,
                StationId = preset.StationId.Trim()
            })
            .GroupBy(preset => preset.Slot)
            .Select(group => group.First())
            .OrderBy(preset => preset.Slot)
            .ToArray();
}

public sealed class RadioStationSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? HomepageUrl { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Tags { get; set; }
    public string? Codec { get; set; }
    public string? DirectoryId { get; set; }
    public int? BitrateKbps { get; set; }
    public bool IsBitrateEstimated { get; set; }
    public int? SampleRateHz { get; set; }
    public int? Volume { get; set; }
    public bool HasCustomTitle { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; }
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }
    public bool IsCustom { get; set; }
}
