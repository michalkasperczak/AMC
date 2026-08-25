using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Input;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.Core.Configuration;

public sealed class ConfigurationStore
{
    public const int CurrentSchemaVersion = 25;
    private const string Version1DefaultPrefix = "Ctrl+Alt+Space";
    private const string Version2DefaultPrefix = "Ctrl+Alt+Windows+Enter";
    private const string CurrentDefaultPrefix = "Ctrl+Alt+Windows+F12";
    private readonly string statePath;
    private readonly string migrationBackupPath;
    private readonly LocalLibraryDatabase libraryDatabase;

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
        ValidateState(state);
        return state;
    }

    public void Save(PersistedState state)
    {
        NormalizeSessionSlots(state.Settings);
        NormalizeSearchHistory(state);
        NormalizeSessionNavigation(state);
        NormalizeLocalMedia(state, state.SchemaVersion);
        NormalizePlaybackHistory(state);
        NormalizeBookmarks(state);
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
        || state.CollectionOrders.FavoriteItemIdsBySession.Count > 0;

    private static void CopyLibraryPayload(PersistedState source, PersistedState destination)
    {
        destination.LocalMedia = source.LocalMedia;
        destination.Bookmarks = source.Bookmarks;
        destination.PlaybackHistory = source.PlaybackHistory;
        destination.CollectionOrders = source.CollectionOrders;
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
        state.SchemaVersion = CurrentSchemaVersion;
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
            session.SelectedItemIds = new Dictionary<string, string?>(
                session.SelectedItemIds ?? new Dictionary<string, string?>(),
                StringComparer.OrdinalIgnoreCase);
            session.Filters = new Dictionary<string, string>(
                session.Filters ?? new Dictionary<string, string>(),
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
    }

    private static void NormalizeLocalMedia(PersistedState state, int schemaVersion)
    {
        state.LocalMedia ??= new LocalMediaSettings();
        state.LocalMedia.Items = (state.LocalMedia.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
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
                return option;
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.Path))
            .GroupBy(option => option.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(option => option.ResumePositionMode != ResumePositionMode.Inherit
                || option.PlaybackRateOverride.HasValue
                || option.OutputDeviceId is not null)
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
