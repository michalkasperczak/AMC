using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.Core.Configuration;

public sealed class ConfigurationStore
{
    public const int CurrentSchemaVersion = 47;
    private const string Version1DefaultPrefix = "Ctrl+Alt+Space";
    private const string Version2DefaultPrefix = "Ctrl+Alt+Windows+Enter";
    private const string CurrentDefaultPrefix = "Ctrl+Alt+Windows+F12";
    private readonly string statePath;
    private readonly string migrationBackupPath;
    private readonly LocalLibraryDatabase libraryDatabase;
    private readonly object saveGate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ConfigurationStore(string statePath, string? libraryDatabasePath = null)
    {
        this.statePath = statePath;
        migrationBackupPath = Path.Combine(
            Path.GetDirectoryName(statePath) ?? string.Empty,
            "state.pre-sqlite-migration.json");
        libraryDatabase = new LocalLibraryDatabase(
            libraryDatabasePath
            ?? Path.Combine(Path.GetDirectoryName(statePath) ?? string.Empty, "library.db"));
    }

    public PersistedState LoadOrCreate()
    {
        var state = File.Exists(statePath)
            ? JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(statePath), JsonOptions)
                ?? throw new InvalidDataException("Nie udało się odczytać konfiguracji.")
            : CreateDefaultState();
        MigrateState(state);
        ValidateState(state);
        EnsureBuiltInProfile(state);

        try
        {
            if (!libraryDatabase.IsInitialized())
            {
                if (!HasLibraryPayload(state) && File.Exists(migrationBackupPath))
                {
                    var backup = JsonSerializer.Deserialize<PersistedState>(
                        File.ReadAllText(migrationBackupPath),
                        JsonOptions);
                    if (backup is not null)
                    {
                        MigrateState(backup);
                        CopyLibraryPayload(backup, state);
                    }
                }

                if (File.Exists(statePath)
                    && HasLibraryPayload(state)
                    && !File.Exists(migrationBackupPath))
                {
                    File.Copy(statePath, migrationBackupPath, false);
                }

                libraryDatabase.Initialize(state);
                WriteStateAtomically(CreateSettingsOnlyState(state));
            }
            else
            {
                libraryDatabase.LoadInto(state);
            }
        }
        catch (SqliteException exception)
        {
            throw new InvalidDataException("Nie udało się odczytać lokalnej bazy Biblioteki SQLite.", exception);
        }

        NormalizeLocalMedia(state, state.SchemaVersion);
        NormalizePlaybackHistory(state);
        NormalizeBookmarks(state);
        NormalizePlaylists(state);
        NormalizeSessionPresets(state);
        NormalizePlaybackVolumes(state);
        NormalizeRadio(state);
        NormalizePodcasts(state);
        ValidateState(state);
        return state;
    }

    public void Save(PersistedState state)
    {
        // MainWindow saves snapshots in the background. Settings and import
        // dialogs may still request an immediate save, so the database and the
        // atomic JSON replacement must be one serialized operation.
        lock (saveGate)
        {
            state.Settings.PrefixChord = NormalizePrefixChord(state.Settings.PrefixChord);
            NormalizeSessionSlots(state.Settings);
            NormalizeSearchHistory(state);
            NormalizeSessionNavigation(state);
            NormalizeLocalMedia(state, state.SchemaVersion);
            NormalizePlaybackHistory(state);
            NormalizeBookmarks(state);
            NormalizePlaylists(state);
            NormalizeSessionPresets(state);
            NormalizePlaybackVolumes(state);
            NormalizeRadio(state);
            NormalizePodcasts(state);
            ValidateState(state);
            try
            {
                libraryDatabase.Save(state);
            }
            catch (SqliteException exception)
            {
                throw new IOException("Nie udało się zapisać lokalnej bazy Biblioteki SQLite.", exception);
            }
            WriteStateAtomically(CreateSettingsOnlyState(state));
        }
    }

    public void ExportKeyboardMap(string path, KeyboardProfile profile)
    {
        var export = new KeyboardMapExport(CurrentSchemaVersion, "keyboardMap", profile.CreateEditableCopy(profile.Name));
        Write(path, export);
    }

    public KeyboardProfile ImportKeyboardMap(string path)
    {
        var export = Read<KeyboardMapExport>(path, "keyboardMap");
        return export.Profile.CreateEditableCopy($"{export.Profile.Name} — import");
    }

    public void ExportConfiguration(string path, AppSettings settings)
    {
        var copy = Clone(settings);
        copy.ActiveKeyboardProfileId = string.Empty;
        Write(path, new ConfigurationExport(CurrentSchemaVersion, "configuration", copy));
    }

    public AppSettings ImportConfiguration(string path, string activeKeyboardProfileId)
    {
        var export = Read<ConfigurationExport>(path, "configuration");
        export.Settings.ActiveKeyboardProfileId = activeKeyboardProfileId;
        MigrateSettings(export.Settings, export.SchemaVersion);
        ValidateSettings(export.Settings);
        return export.Settings;
    }

    public void ExportFullBackup(string path, PersistedState state)
    {
        ValidateState(state);
        Write(path, new FullBackupExport(CurrentSchemaVersion, "fullBackup", state));
    }

    public PersistedState ImportFullBackup(string path)
    {
        var export = Read<FullBackupExport>(path, "fullBackup");
        MigrateState(export.State);
        ValidateState(export.State);
        EnsureBuiltInProfile(export.State);
        return export.State;
    }

    public PersistedState CloneState(PersistedState state)
    {
        return JsonSerializer.Deserialize<PersistedState>(JsonSerializer.Serialize(state, JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("Nie udało się skopiować konfiguracji.");
    }

    public static PersistedState CreateDefaultState() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Settings = new AppSettings(),
        KeyboardProfiles = [KeyboardProfile.CreateDefault()]
    };

    private void WriteStateAtomically(PersistedState state)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = $"{statePath}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        if (File.Exists(statePath))
        {
            File.Replace(temporaryPath, statePath, $"{statePath}.bak", true);
        }
        else
        {
            File.Move(temporaryPath, statePath);
        }
    }

    private PersistedState CreateSettingsOnlyState(PersistedState state)
    {
        var copy = CloneState(state);
        copy.LocalMedia = new LocalMediaSettings();
        copy.Bookmarks = new BookmarkSettings();
        copy.PlaybackHistory = new PlaybackHistorySettings();
        copy.CollectionOrders = new CollectionOrderSettings();
        copy.Playlists = new PlaylistSettings();
        return copy;
    }

    private static bool HasLibraryPayload(PersistedState state) =>
        state.LocalMedia.Items.Count > 0
        || state.LocalMedia.FolderSources.Count > 0
        || state.LocalMedia.FolderPlaybackOptions.Count > 0
        || state.LocalMedia.ExcludedPaths.Count > 0
        || state.LocalMedia.CustomOrderItemIds.Count > 0
        || state.Bookmarks.Entries.Count > 0
        || state.PlaybackHistory.ItemIdsBySession.Count > 0
        || state.CollectionOrders.FavoriteItemIdsBySession.Count > 0
        || state.CollectionOrders.QueueItemIdsBySession.Count > 0
        || state.Playlists.Entries.Count > 0;

    private static void CopyLibraryPayload(PersistedState source, PersistedState destination)
    {
        destination.LocalMedia = source.LocalMedia;
        destination.Bookmarks = source.Bookmarks;
        destination.PlaybackHistory = source.PlaybackHistory;
        destination.CollectionOrders = source.CollectionOrders;
        destination.Playlists = source.Playlists;
    }

    private static void EnsureBuiltInProfile(PersistedState state)
    {
        const string legacyPlaySelectedCommandId = "transport.playSelected";
        string[] removedInformationCommandIds =
        [
            "information.playbackStatus",
            "view.itemInformation",
            "view.extendedInformation"
        ];
        var builtInIndex = state.KeyboardProfiles.FindIndex(profile => profile.Id == "default");
        if (builtInIndex < 0)
        {
            state.KeyboardProfiles.Insert(0, KeyboardProfile.CreateDefault());
        }
        else
        {
            // The protected built-in profile follows the application version.
            // Editable copies created by the user retain their own bindings.
            state.KeyboardProfiles[builtInIndex] = KeyboardProfile.CreateDefault();
        }

        foreach (var profile in state.KeyboardProfiles)
        {
            var normalizedBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var binding in profile.Bindings)
            {
                if (removedInformationCommandIds.Contains(binding.Value, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                normalizedBindings[KeyChord.Parse(binding.Key).Canonical] =
                    string.Equals(binding.Value, legacyPlaySelectedCommandId, StringComparison.OrdinalIgnoreCase)
                        ? CommandIds.ActivateSelected
                        : binding.Value;
            }
            profile.Bindings = normalizedBindings;
        }

        if (state.KeyboardProfiles.All(profile => profile.Id != state.Settings.ActiveKeyboardProfileId))
        {
            state.Settings.ActiveKeyboardProfileId = "default";
        }
    }

    private static void MigrateState(PersistedState state)
    {
        var sourceSchemaVersion = state.SchemaVersion;
        MigrateSettings(state.Settings, state.SchemaVersion);
        NormalizeSearchHistory(state);
        NormalizeSessionNavigation(state);
        NormalizeLocalMedia(state, sourceSchemaVersion);
        NormalizePlaybackHistory(state);
        NormalizeBookmarks(state);
        NormalizePlaylists(state);
        NormalizeSessionPresets(state);
        NormalizePlaybackVolumes(state);
        NormalizeRadio(state);
        NormalizePodcasts(state);
        MigrateLegacyPodcastInbox(state, sourceSchemaVersion);
        MigrateLegacyRadioPresets(state, sourceSchemaVersion);
        NormalizeSessionPresets(state);
        state.SchemaVersion = CurrentSchemaVersion;
    }

    private static void MigrateLegacyPodcastInbox(PersistedState state, int sourceSchemaVersion)
    {
        if (sourceSchemaVersion >= 43) return;

        foreach (var subscription in state.Podcasts.Subscriptions.Where(item => item.IsInLibrary))
        {
            var episodes = state.Podcasts.Episodes
                .Where(episode => string.Equals(
                    episode.SubscriptionId,
                    subscription.Id,
                    StringComparison.Ordinal))
                .ToArray();
            if (episodes.Any(episode => episode.IsNew && !episode.IsPlayed)) continue;

            var latest = episodes
                .OrderByDescending(episode => episode.PublishedUtcTicks)
                .FirstOrDefault();
            if (latest is null
                || latest.IsPlayed
                || latest.IsStarted
                || latest.ResumePositionTicks >= PodcastEpisodeProgress.StartedThreshold.Ticks)
            {
                continue;
            }

            // Early OPML imports deliberately marked the entire existing archive
            // as old. Seed only the newest untouched entry so migration cannot
            // flood the Inbox or resurrect an episode the user already started.
            latest.IsNew = true;
        }
    }

    private static void MigrateLegacyRadioPresets(PersistedState state, int sourceSchemaVersion)
    {
        if (sourceSchemaVersion >= 30 || state.Radio.Presets.Count == 0) return;

        if (!state.SessionPresets.EntriesBySession.TryGetValue("radio", out var entries))
        {
            entries = [];
            state.SessionPresets.EntriesBySession["radio"] = entries;
        }

        var occupiedSlots = entries.Select(entry => entry.Slot).ToHashSet();
        var stations = state.Radio.Stations
            .Where(station => !string.IsNullOrWhiteSpace(station.Id))
            .GroupBy(station => station.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var legacy in state.Radio.Presets.OrderBy(entry => entry.Slot))
        {
            if (occupiedSlots.Contains(legacy.Slot)
                || !stations.TryGetValue(legacy.StationId, out var station))
            {
                continue;
            }

            entries.Add(new SessionPresetEntry
            {
                Slot = legacy.Slot,
                TargetId = station.Id,
                TargetKind = "station",
                TargetTitle = station.Name,
                TargetLocation = station.StreamUrl
            });
            occupiedSlots.Add(legacy.Slot);
        }

        // Od wersji 30 istnieje tylko jeden magazyn presetów dla wszystkich sesji.
        state.Radio.Presets.Clear();
    }

    private static void NormalizeSearchHistory(PersistedState state)
    {
        state.SearchHistory ??= new SearchHistorySettings();
        new SearchQueryHistory(state.SearchHistory).Normalize();
    }

    private static void NormalizePlaybackHistory(PersistedState state)
    {
        state.PlaybackHistory ??= new PlaybackHistorySettings();
        var history = new PlaybackHistory(state.PlaybackHistory);
        history.Normalize();
        var localItemIds = state.LocalMedia.Items
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        history.Remove(
            "local",
            history.GetItemIds("local").Where(itemId => !localItemIds.Contains(itemId)));
        if (history.GetItemIds("local").Count == 0
            && !string.IsNullOrWhiteSpace(state.LocalMedia?.CurrentItemId)
            && localItemIds.Contains(state.LocalMedia.CurrentItemId))
        {
            history.Record("local", state.LocalMedia.CurrentItemId);
        }
    }

    private static void NormalizeBookmarks(PersistedState state)
    {
        state.Bookmarks ??= new BookmarkSettings();
        new BookmarkIndex(state.Bookmarks).Normalize();
    }

    private static void NormalizePlaylists(PersistedState state)
    {
        state.Playlists ??= new PlaylistSettings();
        _ = new PlaylistIndex(state.Playlists);
    }

    private static void NormalizeSessionPresets(PersistedState state)
    {
        state.SessionPresets ??= new SessionPresetSettings();
        var normalized = new Dictionary<string, List<SessionPresetEntry>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var session in state.SessionPresets.EntriesBySession
                     ?? new Dictionary<string, List<SessionPresetEntry>>())
        {
            var sessionId = session.Key?.Trim();
            if (string.IsNullOrWhiteSpace(sessionId)) continue;
            normalized[sessionId] = (session.Value ?? [])
                .Where(entry => entry.Slot is >= 1 and <= RadioPresetSlots.Count
                    && !string.IsNullOrWhiteSpace(entry.TargetId)
                    && !string.IsNullOrWhiteSpace(entry.TargetKind))
                .Select(entry => new SessionPresetEntry
                {
                    Slot = entry.Slot,
                    TargetId = entry.TargetId.Trim(),
                    TargetKind = entry.TargetKind.Trim(),
                    TargetTitle = string.IsNullOrWhiteSpace(entry.TargetTitle)
                        ? "Element bez nazwy"
                        : entry.TargetTitle.Trim(),
                    TargetLocation = string.IsNullOrWhiteSpace(entry.TargetLocation)
                        ? null
                        : entry.TargetLocation.Trim()
                })
                .GroupBy(entry => entry.Slot)
                .Select(group => group.First())
                .OrderBy(entry => entry.Slot)
                .ToList();
        }
        state.SessionPresets.EntriesBySession = normalized;
    }

    private static void NormalizeRadio(PersistedState state)
    {
        state.Radio ??= new RadioSettings();
        state.Radio.Volume = Math.Clamp(state.Radio.Volume, 0, 100);
        state.Radio.TimeshiftMinutes = Math.Clamp(state.Radio.TimeshiftMinutes, 1, 60);
        state.Radio.RecordingsFolder = state.Radio.RecordingsFolder?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(state.Radio.AutomaticTrackRecognitionScope))
            state.Radio.AutomaticTrackRecognitionScope = RadioRecognitionScope.CurrentStation;
        if (!Enum.IsDefined(state.Radio.RecordingFormat))
            state.Radio.RecordingFormat = RadioRecordingFormat.Mp3;
        state.Radio.RecordingBitrateKbps = NormalizeRadioRecordingBitrate(
            state.Radio.RecordingBitrateKbps);
        state.Radio.RecordingSchedules ??= [];
        state.Radio.RecognizedTracks ??= [];
        state.Radio.Stations = (state.Radio.Stations ?? [])
            .Where(station => Uri.TryCreate(station.StreamUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            .Select(station =>
            {
                station.Id = string.IsNullOrWhiteSpace(station.Id)
                    ? Guid.NewGuid().ToString("N")
                    : station.Id.Trim();
                station.Name = string.IsNullOrWhiteSpace(station.Name)
                    ? "Stacja bez nazwy"
                    : station.Name.Trim();
                station.StreamUrl = station.StreamUrl.Trim();
                station.HomepageUrl = string.IsNullOrWhiteSpace(station.HomepageUrl)
                    ? null
                    : station.HomepageUrl.Trim();
                station.Country = string.IsNullOrWhiteSpace(station.Country) ? null : station.Country.Trim();
                station.Language = string.IsNullOrWhiteSpace(station.Language) ? null : station.Language.Trim();
                station.Tags = string.IsNullOrWhiteSpace(station.Tags) ? null : station.Tags.Trim();
                station.Codec = string.IsNullOrWhiteSpace(station.Codec) ? null : station.Codec.Trim();
                station.DirectoryId = string.IsNullOrWhiteSpace(station.DirectoryId) ? null : station.DirectoryId.Trim();
                station.Volume = station.Volume.HasValue
                    ? Math.Clamp(station.Volume.Value, 0, 100)
                    : null;
                if (station.IsFavorite) station.IsInLibrary = true;
                return station;
            })
            .GroupBy(station => station.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var stationIds = state.Radio.Stations
            .Select(station => station.Id)
            .ToHashSet(StringComparer.Ordinal);
        state.Radio.Presets = RadioPresetSlots.Normalize(state.Radio.Presets, stationIds).ToList();
        state.Radio.RecordingSchedules = state.Radio.RecordingSchedules
            .Where(schedule => schedule is not null
                && Uri.TryCreate(schedule.StreamUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && schedule.NextStartUtcTicks > DateTime.UnixEpoch.Ticks)
            .Select(schedule =>
            {
                schedule.Id = string.IsNullOrWhiteSpace(schedule.Id)
                    ? Guid.NewGuid().ToString("N")
                    : schedule.Id.Trim();
                schedule.StationId = schedule.StationId?.Trim() ?? string.Empty;
                schedule.StationName = string.IsNullOrWhiteSpace(schedule.StationName)
                    ? "Stacja bez nazwy"
                    : schedule.StationName.Trim();
                schedule.StreamUrl = schedule.StreamUrl.Trim();
                schedule.DurationMinutes = Math.Clamp(schedule.DurationMinutes, 1, 10_080);
                schedule.SegmentMinutes = schedule.SegmentMinutes > 0
                    && schedule.SegmentMinutes < schedule.DurationMinutes
                        ? Math.Clamp(schedule.SegmentMinutes, 1, 10_080)
                        : 0;
                schedule.OutputFolder = schedule.OutputFolder?.Trim() ?? string.Empty;
                schedule.FileNameTemplate = RadioRecordingFileNameTemplate.NormalizeOrDefault(
                    schedule.FileNameTemplate);
                if (schedule.RecordingFormat.HasValue
                    && !Enum.IsDefined(schedule.RecordingFormat.Value))
                {
                    schedule.RecordingFormat = null;
                }
                schedule.RecordingBitrateKbps = schedule.RecordingBitrateKbps.HasValue
                    ? NormalizeRadioRecordingBitrate(schedule.RecordingBitrateKbps.Value)
                    : null;
                schedule.TimeZoneId = RadioScheduleCalculator.ResolveTimeZone(schedule.TimeZoneId).Id;
                schedule.ActiveDays = (schedule.ActiveDays ?? [])
                    .Where(day => day is >= DayOfWeek.Sunday and <= DayOfWeek.Saturday)
                    .Distinct()
                    .OrderBy(day => ((int)day + 6) % 7)
                    .ToList();
                if (schedule.Recurrence == RadioScheduleRecurrence.SelectedDays
                    && schedule.ActiveDays.Count == 0)
                {
                    var start = new DateTime(schedule.NextStartUtcTicks, DateTimeKind.Utc);
                    schedule.ActiveDays.Add(TimeZoneInfo.ConvertTimeFromUtc(
                        start,
                        RadioScheduleCalculator.ResolveTimeZone(schedule.TimeZoneId)).DayOfWeek);
                }
                return schedule;
            })
            .GroupBy(schedule => schedule.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(schedule => schedule.NextStartUtcTicks)
            .ToList();
        state.Radio.RecognizedTracks = state.Radio.RecognizedTracks
            .Where(entry => entry is not null
                && (!string.IsNullOrWhiteSpace(entry.Title)
                    || !string.IsNullOrWhiteSpace(entry.Artist)))
            .Select(entry =>
            {
                entry.Id = string.IsNullOrWhiteSpace(entry.Id)
                    ? Guid.NewGuid().ToString("N")
                    : entry.Id.Trim();
                entry.StationId = entry.StationId?.Trim() ?? string.Empty;
                entry.StationName = string.IsNullOrWhiteSpace(entry.StationName)
                    ? "Nieznana stacja"
                    : entry.StationName.Trim();
                entry.Title = entry.Title?.Trim() ?? string.Empty;
                entry.Artist = entry.Artist?.Trim() ?? string.Empty;
                entry.Album = entry.Album?.Trim() ?? string.Empty;
                entry.ReleaseDate = entry.ReleaseDate?.Trim() ?? string.Empty;
                entry.ProviderUri = string.IsNullOrWhiteSpace(entry.ProviderUri)
                    ? null
                    : entry.ProviderUri.Trim();
                if (entry.RecognizedUtcTicks <= DateTime.UnixEpoch.Ticks
                    || entry.RecognizedUtcTicks > DateTime.UtcNow.AddDays(1).Ticks)
                {
                    entry.RecognizedUtcTicks = DateTime.UtcNow.Ticks;
                }
                return entry;
            })
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.RecognizedUtcTicks)
            .Take(2_000)
            .ToList();
        if (state.Radio.CurrentItemId is not null
            && state.Radio.Stations.All(station => !string.Equals(
                station.Id,
                state.Radio.CurrentItemId,
                StringComparison.Ordinal)))
        {
            state.Radio.CurrentItemId = null;
        }
    }

    private static void NormalizeSessionNavigation(PersistedState state)
    {
        state.SessionNavigation ??= new SessionNavigationSettings();
        state.SessionNavigation.Sessions = new Dictionary<string, SessionNavigationState>(
            state.SessionNavigation.Sessions ?? new Dictionary<string, SessionNavigationState>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var session in state.SessionNavigation.Sessions.Values)
        {
            session.CurrentView = string.IsNullOrWhiteSpace(session.CurrentView)
                ? "Multimedia"
                : session.CurrentView;
            session.LastLibraryView = string.IsNullOrWhiteSpace(session.LastLibraryView)
                ? "Biblioteka"
                : session.LastLibraryView;
            session.SelectedItemIds = new Dictionary<string, string?>(
                session.SelectedItemIds ?? new Dictionary<string, string?>(),
                StringComparer.OrdinalIgnoreCase);
            session.Filters = new Dictionary<string, string>(
                session.Filters ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            session.CollectionSortModes = new Dictionary<string, CollectionSortMode>(
                session.CollectionSortModes ?? new Dictionary<string, CollectionSortMode>(),
                StringComparer.OrdinalIgnoreCase);
            session.PlaybackContextView = string.IsNullOrWhiteSpace(session.PlaybackContextView)
                ? "Multimedia"
                : session.PlaybackContextView;
            session.PlaybackContextItemIds = (session.PlaybackContextItemIds ?? [])
                .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        state.CollectionOrders ??= new CollectionOrderSettings();
        state.CollectionOrders.FavoriteAddedItemIdsBySession = NormalizeOrderDictionary(
            state.CollectionOrders.FavoriteAddedItemIdsBySession);
        state.CollectionOrders.FavoriteItemIdsBySession = new Dictionary<string, List<string>>(
            (state.CollectionOrders.FavoriteItemIdsBySession
                ?? new Dictionary<string, List<string>>())
            .ToDictionary(
                pair => pair.Key,
                pair => (pair.Value ?? [])
                    .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        state.CollectionOrders.LibraryAddedItemIdsBySession = NormalizeOrderDictionary(
            state.CollectionOrders.LibraryAddedItemIdsBySession);
        state.CollectionOrders.LibraryItemIdsBySession = NormalizeOrderDictionary(
            state.CollectionOrders.LibraryItemIdsBySession);
        state.CollectionOrders.QueueItemIdsBySession = new Dictionary<string, List<string>>(
            (state.CollectionOrders.QueueItemIdsBySession
                ?? new Dictionary<string, List<string>>())
            .ToDictionary(
                pair => pair.Key,
                pair => (pair.Value ?? [])
                    .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void NormalizePlaybackVolumes(PersistedState state)
    {
        state.PlaybackVolumes ??= new PlaybackVolumeMemorySettings();
        PlaybackVolumeMemory.Normalize(state.PlaybackVolumes);
    }

    private static Dictionary<string, List<string>> NormalizeOrderDictionary(
        Dictionary<string, List<string>>? source) =>
        new(
            (source ?? new Dictionary<string, List<string>>())
            .ToDictionary(
                pair => pair.Key,
                pair => (pair.Value ?? [])
                    .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

    private static int NormalizeRadioRecordingBitrate(int bitrateKbps)
    {
        int[] supported = [96, 128, 160, 192, 256, 320];
        return supported.MinBy(value => Math.Abs(value - bitrateKbps));
    }

    private static void NormalizeLocalMedia(PersistedState state, int schemaVersion)
    {
        state.LocalMedia ??= new LocalMediaSettings();
        state.LocalMedia.Items = (state.LocalMedia.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        foreach (var item in state.LocalMedia.Items)
        {
            if (!Enum.IsDefined(item.ResumePositionMode))
            {
                item.ResumePositionMode = ResumePositionMode.Inherit;
            }
            if (item.PlaybackRateOverride.HasValue)
            {
                item.PlaybackRateOverride = Math.Clamp(item.PlaybackRateOverride.Value, 0.50d, 2.00d);
            }
            if (item.InterTrackSilenceMillisecondsOverride.HasValue
                && !PlaybackAudioSettingsRules.IsSupportedSilence(
                    item.InterTrackSilenceMillisecondsOverride.Value))
            {
                item.InterTrackSilenceMillisecondsOverride = null;
            }
            if (item.ClipStartTicks.HasValue
                && (item.ClipStartTicks.Value < 0
                    || item.DurationTicks > 0 && item.ClipStartTicks.Value > item.DurationTicks))
            {
                item.ClipStartTicks = null;
            }
            if (!item.ClipStartTicks.HasValue
                || !item.ClipEndTicks.HasValue
                || item.ClipEndTicks.Value <= item.ClipStartTicks.Value
                || item.DurationTicks > 0 && item.ClipEndTicks.Value > item.DurationTicks)
            {
                item.ClipEndTicks = null;
            }
        }
        state.LocalMedia.Volume = Math.Clamp(state.LocalMedia.Volume, 0, 100);
        state.LocalMedia.PlaybackRate = Math.Clamp(state.LocalMedia.PlaybackRate, 0.50d, 2.00d);
        state.LocalMedia.FolderSources = (state.LocalMedia.FolderSources ?? [])
            .Where(source => !string.IsNullOrWhiteSpace(source.Path))
            .Select(source =>
            {
                source.Path = NormalizeFolderPath(source.Path);
                source.Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id;
                source.DisplayName = string.IsNullOrWhiteSpace(source.DisplayName)
                    ? GetFolderDisplayName(source.Path)
                    : source.DisplayName.Trim();
                if (!Enum.IsDefined(source.ResumePositionMode))
                {
                    source.ResumePositionMode = ResumePositionMode.Inherit;
                }
                return source;
            })
            .Where(source => !string.IsNullOrWhiteSpace(source.Path))
            .GroupBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        state.LocalMedia.FolderPlaybackOptions = (state.LocalMedia.FolderPlaybackOptions ?? [])
            .Where(option => !string.IsNullOrWhiteSpace(option.Path))
            .Select(option =>
            {
                option.Path = NormalizeFolderPath(option.Path);
                if (!Enum.IsDefined(option.ResumePositionMode))
                {
                    option.ResumePositionMode = ResumePositionMode.Inherit;
                }
                if (option.PlaybackRateOverride.HasValue)
                {
                    option.PlaybackRateOverride = Math.Clamp(
                        option.PlaybackRateOverride.Value,
                        0.50d,
                        2.00d);
                }
                option.OutputDeviceId = string.IsNullOrWhiteSpace(option.OutputDeviceId)
                    ? null
                    : option.OutputDeviceId.Trim();
                if (option.InterTrackSilenceMillisecondsOverride.HasValue
                    && !PlaybackAudioSettingsRules.IsSupportedSilence(
                        option.InterTrackSilenceMillisecondsOverride.Value))
                {
                    option.InterTrackSilenceMillisecondsOverride = null;
                }
                return option;
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.Path))
            .GroupBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(option => option.ResumePositionMode != ResumePositionMode.Inherit
                || option.PlaybackRateOverride.HasValue
                || option.OutputDeviceId is not null
                || option.LoudnessNormalizationOverride.HasValue
                || option.SmoothTrackTransitionsOverride.HasValue
                || option.InterTrackSilenceMillisecondsOverride.HasValue)
            .ToList();
        state.LocalMedia.LibraryView = state.LocalMedia.LibraryView is "Foldery" or "Wszystkie pliki" or "Kolejność własna"
            ? state.LocalMedia.LibraryView
            : "Foldery";
        state.LocalMedia.ExcludedPaths = (state.LocalMedia.ExcludedPaths ?? [])
            .Select(NormalizeFilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (schemaVersion < 18)
        {
            foreach (var item in state.LocalMedia.Items.Where(item => !item.IsInLibrary))
            {
                var path = NormalizeFilePath(item.Path);
                if (path.Length > 0
                    && state.LocalMedia.FolderSources.Any(source => IsSameOrDescendant(path, source.Path))
                    && !state.LocalMedia.ExcludedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    state.LocalMedia.ExcludedPaths.Add(path);
                }
            }

            if (state.SessionNavigation.Sessions.TryGetValue("local", out var navigation))
            {
                if (string.Equals(navigation.CurrentView, "Biblioteka", StringComparison.Ordinal))
                {
                    state.LocalMedia.LibraryView = "Wszystkie pliki";
                    navigation.CurrentView = "Wszystkie pliki";
                }
                else if (string.Equals(navigation.CurrentView, "Foldery", StringComparison.Ordinal))
                {
                    state.LocalMedia.LibraryView = "Foldery";
                }
                else if (string.Equals(navigation.CurrentView, "Multimedia", StringComparison.Ordinal))
                {
                    state.LocalMedia.LibraryView = state.LocalMedia.FolderSources.Count > 0
                        ? "Foldery"
                        : "Wszystkie pliki";
                    navigation.CurrentView = state.LocalMedia.LibraryView;
                }
            }
        }

        if (schemaVersion < 19)
        {
            // Alpha.80 could turn every legacy record below a newly registered
            // source into an exclusion. An entirely excluded source is not a
            // useful migrated state and made Cloud Files folders look empty.
            foreach (var source in state.LocalMedia.FolderSources)
            {
                var sourceItems = state.LocalMedia.Items
                    .Where(item => IsSameOrDescendant(NormalizeFilePath(item.Path), source.Path))
                    .ToArray();
                if (sourceItems.Length == 0
                    || sourceItems.Any(item => item.IsInLibrary)
                    || sourceItems.Any(item => !state.LocalMedia.ExcludedPaths.Contains(
                        NormalizeFilePath(item.Path), StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var sourcePaths = sourceItems
                    .Select(item => NormalizeFilePath(item.Path))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                state.LocalMedia.ExcludedPaths.RemoveAll(sourcePaths.Contains);
                foreach (var item in sourceItems) item.IsInLibrary = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(state.LocalMedia.CurrentFolderPath))
        {
            state.LocalMedia.CurrentFolderPath = NormalizeFolderPath(state.LocalMedia.CurrentFolderPath);
            if (!state.LocalMedia.FolderSources.Any(source =>
                    IsSameOrDescendant(state.LocalMedia.CurrentFolderPath, source.Path)))
            {
                state.LocalMedia.CurrentFolderPath = null;
            }
        }

        var knownIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in state.LocalMedia.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !knownIds.Add(item.Id))
            {
                item.Id = $"local-{Guid.NewGuid():N}";
                knownIds.Add(item.Id);
            }
            if (string.IsNullOrWhiteSpace(item.Title)) item.Title = Path.GetFileNameWithoutExtension(item.Path);
            item.Path = NormalizeFilePath(item.Path);
            item.DurationTicks = Math.Max(0, item.DurationTicks);
            item.ResumePositionTicks = Math.Max(0, item.ResumePositionTicks);
            if (!Enum.IsDefined(item.ResumePositionMode))
            {
                item.ResumePositionMode = ResumePositionMode.Inherit;
            }
            if (item.PlaybackRateOverride.HasValue)
            {
                item.PlaybackRateOverride = Math.Clamp(item.PlaybackRateOverride.Value, 0.50d, 2.00d);
            }
            if (item.DurationTicks > 0)
            {
                item.ResumePositionTicks = Math.Min(item.ResumePositionTicks, item.DurationTicks);
            }
        }

        state.LocalMedia.CustomOrderItemIds = LocalLibraryManualOrder.Normalize(
            state.LocalMedia.CustomOrderItemIds,
            state.LocalMedia.Items.Select(item => new MediaItem
            {
                Id = item.Id,
                Title = item.Title,
                Source = item.Path
            }),
            initializeAlphabetically: true);

        if (state.LocalMedia.CurrentItemId is { Length: > 0 } currentItemId
            && state.LocalMedia.Items.All(item => !string.Equals(item.Id, currentItemId, StringComparison.Ordinal)))
        {
            state.LocalMedia.CurrentItemId = null;
        }
    }

    private static void MigrateSettings(AppSettings settings, int schemaVersion)
    {
        settings.Audio ??= new PlaybackAudioSettings();
        settings.Audio.OutputDeviceIdsBySession =
            (settings.Audio.OutputDeviceIdsBySession ?? new Dictionary<string, string>())
            .Where(pair => SessionSlotOrder.DefaultSessionIds.Contains(
                pair.Key,
                StringComparer.OrdinalIgnoreCase))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);
        settings.Audio.SessionMutedById =
            (settings.Audio.SessionMutedById ?? new Dictionary<string, bool>())
            .Where(pair => pair.Value)
            .Where(pair => SessionSlotOrder.DefaultSessionIds.Contains(
                pair.Key,
                StringComparer.OrdinalIgnoreCase))
            .ToDictionary(
                pair => pair.Key.Trim(),
                _ => true,
                StringComparer.OrdinalIgnoreCase);
        settings.PrefixChord = NormalizePrefixChord(settings.PrefixChord);
        if (schemaVersion < 17 && IsLegacyDefaultSessionOrder(settings.SessionSlots))
        {
            settings.SessionSlots = SessionSlotOrder.CreateDefault();
        }
        NormalizeSessionSlots(settings);

        if (schemaVersion < 2
            && string.Equals(
                KeyChord.Parse(settings.PrefixChord).Canonical,
                Version1DefaultPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            settings.PrefixChord = Version2DefaultPrefix;
        }

        if (schemaVersion < 3)
        {
            if (string.Equals(
                    KeyChord.Parse(settings.PrefixChord).Canonical,
                    Version2DefaultPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                settings.PrefixChord = CurrentDefaultPrefix;
            }

            // Alpha.5 restores announcements once for configurations created by the prototype.
            // They can still be disabled explicitly after the migration.
            settings.Messages.Enabled = true;
        }

        if (schemaVersion < 4)
        {
            foreach (var pair in MessageSettings.CreateDefault().Templates)
            {
                settings.Messages.Templates.TryAdd(pair.Key, pair.Value);
            }
        }

        if (schemaVersion < 5)
        {
            ReplaceTemplateIfDefault(settings, "queue.added", "Dodano do kolejki — demonstracja", "Dodano do kolejki");
            ReplaceTemplateIfDefault(settings, "queue.removed", "Usunięto z kolejki — demonstracja", "Usunięto z kolejki");
            ReplaceTemplateIfDefault(settings, "playNext.added", "Ustawiono do odtworzenia jako następne — demonstracja", "Odtwarzaj jako następne");
            ReplaceTemplateIfDefault(settings, "playNext.removed", "Usunięto z odtwarzanych jako następne — demonstracja", "Usunięto z następnych");
        }

        if (schemaVersion < 6)
        {
            ReplaceTemplateIfDefault(settings, "queue.added", "Dodano do kolejki", "Dodano do kolejki: {item}");
            ReplaceTemplateIfDefault(settings, "queue.removed", "Usunięto z kolejki", "Usunięto z kolejki: {item}");
            ReplaceTemplateIfDefault(settings, "playNext.added", "Odtwarzaj jako następne", "Odtwarzaj jako następne: {item}");
            ReplaceTemplateIfDefault(settings, "playNext.removed", "Usunięto z następnych", "Usunięto z następnych: {item}");
        }

        if (schemaVersion < 7)
        {
            ReplaceTemplateIfDefault(settings, "favorite.added", "Dodano do ulubionych", "Dodano do ulubionych: {item}");
            ReplaceTemplateIfDefault(settings, "favorite.removed", "Usunięto z ulubionych", "Usunięto z ulubionych: {item}");
            settings.Messages.Templates.TryAdd("favorite.added", "Dodano do ulubionych: {item}");
            settings.Messages.Templates.TryAdd("favorite.removed", "Usunięto z ulubionych: {item}");
        }

        if (schemaVersion < 9)
        {
            // Alpha.40 broadens the existing seek-feedback switch to include
            // volume feedback. Preserve the user's previous on/off choice.
            settings.Messages.VolumeMessages = settings.Messages.SeekMessages;
        }

        if (schemaVersion < 10)
        {
            // Alpha.41 keeps the previous switch as a non-destructive master
            // and introduces independent categories beneath it.
            settings.Messages.ArrowSeekMessages = true;
            settings.Messages.PercentageSeekMessages = true;
        }

        if (schemaVersion < 15)
        {
            settings.Messages.BookmarkNavigationMessages = true;
        }
    }

    private static void ReplaceTemplateIfDefault(
        AppSettings settings,
        string eventId,
        string previousDefault,
        string currentDefault)
    {
        if (settings.Messages.Templates.TryGetValue(eventId, out var value)
            && string.Equals(value, previousDefault, StringComparison.Ordinal))
        {
            settings.Messages.Templates[eventId] = currentDefault;
        }
    }

    private static void ValidateState(PersistedState state)
    {
        if (state.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException("Plik został utworzony przez nowszą wersję programu.");
        }

        ValidateSettings(state.Settings);

        if (state.KeyboardProfiles.Count == 0)
        {
            throw new InvalidDataException("Konfiguracja nie zawiera profilu klawiatury.");
        }

        foreach (var profile in state.KeyboardProfiles)
        {
            foreach (var chord in profile.Bindings.Keys) _ = KeyChord.Parse(chord);
        }
    }

    private static void ValidateSettings(AppSettings settings)
    {
        if (settings.PrefixTimeoutMilliseconds is < 250 or > 30000)
        {
            throw new InvalidDataException("Czas prefiksu musi mieścić się między 250 a 30000 ms.");
        }

        if (settings.Audio is null)
        {
            throw new InvalidDataException("Brak ustawień przetwarzania dźwięku.");
        }
        if (!PlaybackAudioSettingsRules.IsSupportedSilence(
                settings.Audio.InterTrackSilenceMilliseconds))
        {
            throw new InvalidDataException(
                "Cisza między utworami musi mieć jedną z wartości dostępnych w Ustawieniach.");
        }
        if (settings.Audio.OutputDeviceIdsBySession.Any(pair =>
                !SessionSlotOrder.DefaultSessionIds.Contains(
                    pair.Key,
                    StringComparer.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(pair.Value)
                || pair.Value.Length > 2048))
        {
            throw new InvalidDataException("Wybór urządzeń audio sesji jest nieprawidłowy.");
        }
        if (settings.Audio.SessionMutedById.Any(pair =>
                !pair.Value
                || !SessionSlotOrder.DefaultSessionIds.Contains(
                    pair.Key,
                    StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Zapamiętane wyciszenia sesji są nieprawidłowe.");
        }

        var expectedFields = Enum.GetValues<MediaItemField>();
        var fieldOrder = settings.Lists?.FieldOrder;
        if (fieldOrder is null
            || fieldOrder.Count != expectedFields.Length
            || fieldOrder.Distinct().Count() != expectedFields.Length
            || expectedFields.Any(field => !fieldOrder.Contains(field)))
        {
            throw new InvalidDataException("Kolejność informacji na listach jest nieprawidłowa.");
        }

        if (settings.SessionSlots is null
            || settings.SessionSlots.Count == 0
            || settings.SessionSlots.Keys.Any(slot => slot is < 1 or > 9)
            || settings.SessionSlots.Values.Any(string.IsNullOrWhiteSpace)
            || settings.SessionSlots.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != settings.SessionSlots.Count)
        {
            throw new InvalidDataException("Kolejność sesji jest nieprawidłowa.");
        }
    }

    private static void NormalizeSessionSlots(AppSettings settings)
    {
        settings.SessionSlots = SessionSlotOrder.Normalize(settings.SessionSlots);
    }

    private static void NormalizePodcasts(PersistedState state)
    {
        state.Podcasts ??= new PodcastSettings();
        state.Podcasts.DownloadsFolder = string.IsNullOrWhiteSpace(state.Podcasts.DownloadsFolder)
            ? null
            : NormalizeFilePath(state.Podcasts.DownloadsFolder);
        if (state.Podcasts.DownloadsFolder?.Length == 0)
            state.Podcasts.DownloadsFolder = null;
        state.Podcasts.Volume = Math.Clamp(state.Podcasts.Volume, 0, 100);
        state.Podcasts.PlaybackRate = Math.Clamp(state.Podcasts.PlaybackRate, 0.50d, 2.00d);

        state.Podcasts.Subscriptions = (state.Podcasts.Subscriptions ?? [])
            .Where(subscription => IsHttpAddress(subscription.FeedUrl))
            .Select(subscription =>
            {
                subscription.Id = string.IsNullOrWhiteSpace(subscription.Id)
                    ? $"podcast-{Guid.NewGuid():N}"
                    : subscription.Id.Trim();
                subscription.Title = string.IsNullOrWhiteSpace(subscription.Title)
                    ? "Podcast bez nazwy"
                    : subscription.Title.Trim();
                subscription.Author = subscription.Author?.Trim() ?? string.Empty;
                subscription.Description = subscription.Description?.Trim() ?? string.Empty;
                subscription.FeedUrl = subscription.FeedUrl.Trim();
                subscription.HomepageUrl = IsHttpAddress(subscription.HomepageUrl)
                    ? subscription.HomepageUrl!.Trim()
                    : null;
                subscription.LastRefreshUtcTicks = NormalizeOptionalUtcTicks(
                    subscription.LastRefreshUtcTicks);
                subscription.RefreshIntervalMinutes = subscription.RefreshIntervalMinutes is
                    0 or 15 or 30 or 60 or 180 or 360 or 720 or 1440
                        ? subscription.RefreshIntervalMinutes
                        : 0;
                subscription.DownloadsFolder = string.IsNullOrWhiteSpace(subscription.DownloadsFolder)
                    ? null
                    : NormalizeFilePath(subscription.DownloadsFolder);
                if (subscription.DownloadsFolder?.Length == 0)
                    subscription.DownloadsFolder = null;
                subscription.ResumePositionMode = Enum.IsDefined(subscription.ResumePositionMode)
                    ? subscription.ResumePositionMode
                    : ResumePositionMode.Inherit;
                subscription.PlaybackRateOverride = NormalizePlaybackRateOverride(
                    subscription.PlaybackRateOverride);
                subscription.InterTrackSilenceMillisecondsOverride =
                    NormalizeSilenceOverride(subscription.InterTrackSilenceMillisecondsOverride);
                if (subscription.IsFavorite) subscription.IsInLibrary = true;
                return subscription;
            })
            .GroupBy(subscription => subscription.FeedUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .GroupBy(subscription => subscription.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        var subscriptionIds = state.Podcasts.Subscriptions
            .Select(subscription => subscription.Id)
            .ToHashSet(StringComparer.Ordinal);
        state.Podcasts.Episodes = (state.Podcasts.Episodes ?? [])
            .Where(episode => subscriptionIds.Contains(episode.SubscriptionId?.Trim() ?? string.Empty)
                && IsHttpAddress(episode.MediaUrl))
            .Select(episode =>
            {
                episode.Id = string.IsNullOrWhiteSpace(episode.Id)
                    ? $"podcast-episode-{Guid.NewGuid():N}"
                    : episode.Id.Trim();
                episode.SubscriptionId = episode.SubscriptionId.Trim();
                episode.SourceIdentifier = episode.SourceIdentifier?.Trim() ?? string.Empty;
                episode.Title = string.IsNullOrWhiteSpace(episode.Title)
                    ? "Odcinek bez tytułu"
                    : episode.Title.Trim();
                episode.Author = episode.Author?.Trim() ?? string.Empty;
                episode.Description = episode.Description?.Trim() ?? string.Empty;
                episode.MediaUrl = episode.MediaUrl.Trim();
                episode.PageUrl = IsHttpAddress(episode.PageUrl) ? episode.PageUrl!.Trim() : null;
                episode.MediaType = string.IsNullOrWhiteSpace(episode.MediaType)
                    ? null
                    : episode.MediaType.Trim();
                episode.MediaLength = episode.MediaLength is >= 0 ? episode.MediaLength : null;
                episode.PublishedUtcTicks = NormalizeOptionalUtcTicks(episode.PublishedUtcTicks);
                episode.DurationTicks = Math.Max(0, episode.DurationTicks);
                episode.ResumePositionTicks = Math.Max(0, episode.ResumePositionTicks);
                episode.ResumePositionMode = Enum.IsDefined(episode.ResumePositionMode)
                    ? episode.ResumePositionMode
                    : ResumePositionMode.Inherit;
                episode.PlaybackRateOverride = NormalizePlaybackRateOverride(
                    episode.PlaybackRateOverride);
                episode.InterTrackSilenceMillisecondsOverride =
                    NormalizeSilenceOverride(episode.InterTrackSilenceMillisecondsOverride);
                if (episode.DurationTicks > 0)
                {
                    episode.ResumePositionTicks = Math.Min(
                        episode.ResumePositionTicks,
                        episode.DurationTicks);
                }
                episode.DownloadPath = string.IsNullOrWhiteSpace(episode.DownloadPath)
                    ? null
                    : NormalizeFilePath(episode.DownloadPath);
                if (episode.DownloadPath?.Length == 0) episode.DownloadPath = null;
                PodcastEpisodeProgress.Normalize(episode);
                return episode;
            })
            .GroupBy(episode => episode.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        var knownItemIds = subscriptionIds
            .Concat(state.Podcasts.Episodes.Select(episode => episode.Id))
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(state.Podcasts.CurrentItemId)
            && !knownItemIds.Contains(state.Podcasts.CurrentItemId))
        {
            state.Podcasts.CurrentItemId = null;
        }
    }

    private static bool IsHttpAddress(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    private static double? NormalizePlaybackRateOverride(double? value) =>
        value.HasValue && double.IsFinite(value.Value)
            ? Math.Clamp(value.Value, 0.50d, 2.00d)
            : null;

    private static int? NormalizeSilenceOverride(int? value) =>
        value.HasValue && PlaybackAudioSettingsRules.IsSupportedSilence(value.Value)
            ? value
            : null;

    private static long NormalizeOptionalUtcTicks(long ticks) =>
        ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks ? ticks : 0;

    private static string NormalizePrefixChord(string? value)
    {
        try
        {
            return KeyChord.Parse(value ?? string.Empty).Canonical;
        }
        catch (FormatException)
        {
            // An invalid imported prefix must not prevent the remainder of the
            // user's library, schedules and settings from loading. The default
            // is explicit and can be changed again from the accessible capture
            // dialog.
            return CurrentDefaultPrefix;
        }
    }

    private static bool IsLegacyDefaultSessionOrder(IReadOnlyDictionary<int, string>? slots) =>
        slots is not null
        && slots.Count == 3
        && string.Equals(slots.GetValueOrDefault(1), "tidal", StringComparison.OrdinalIgnoreCase)
        && string.Equals(slots.GetValueOrDefault(2), "appleMusic", StringComparison.OrdinalIgnoreCase)
        && string.Equals(slots.GetValueOrDefault(3), "wiim", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeFolderPath(string path)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeFilePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string GetFolderDisplayName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return true;
        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static T Read<T>(string path, string expectedKind) where T : IExportEnvelope
    {
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Plik eksportu jest pusty lub uszkodzony.");
        if (!string.Equals(value.Kind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Oczekiwano pliku typu {expectedKind}, otrzymano {value.Kind}.");
        }
        if (value.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException("Plik eksportu pochodzi z nowszej wersji programu.");
        }
        return value;
    }

    private static void Write<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static AppSettings Clone(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Nie udało się skopiować ustawień.");
}

public interface IExportEnvelope
{
    int SchemaVersion { get; }
    string Kind { get; }
}

public sealed record KeyboardMapExport(int SchemaVersion, string Kind, KeyboardProfile Profile) : IExportEnvelope;
public sealed record ConfigurationExport(int SchemaVersion, string Kind, AppSettings Settings) : IExportEnvelope;
public sealed record FullBackupExport(int SchemaVersion, string Kind, PersistedState State) : IExportEnvelope;
