using System.Globalization;
using AccessibleMediaController.Core.LocalMedia;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Durable, embedded storage for the media catalog and its relationships.
/// The database is local to the computer; source audio remains in its original
/// folders and is never copied into SQLite.
/// </summary>
internal sealed class LocalLibraryDatabase(string databasePath)
{
    private const int DatabaseSchemaVersion = 8;
    private readonly object _gate = new();

    public string Path { get; } = databasePath;

    public bool IsInitialized()
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM metadata WHERE key = 'library_initialized';";
            return string.Equals(command.ExecuteScalar() as string, "1", StringComparison.Ordinal);
        }
    }

    public void Initialize(PersistedState state)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            SaveCore(connection, state);

            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM local_items;";
            var storedCount = Convert.ToInt32(countCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (storedCount != state.LocalMedia.Items.Count)
            {
                throw new InvalidDataException(
                    $"Migracja Biblioteki do SQLite zapisała {storedCount} z {state.LocalMedia.Items.Count} elementów.");
            }
        }
    }

    public void Save(PersistedState state)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            SaveCore(connection, state);
        }
    }

    public void LoadInto(PersistedState state)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            var local = new LocalMediaSettings();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT id, title, has_custom_title, path, duration_ticks,
                           bitrate_kbps, bitrate_estimated, sample_rate_hz,
                           is_favorite, is_in_library, is_available, is_in_queue,
                           is_play_next, resume_mode, playback_rate_override,
                           output_device_id, resume_position_ticks, file_length,
                           last_write_utc_ticks, loudness_normalization_override,
                           smooth_track_transitions_override,
                           inter_track_silence_ms_override, clip_start_ticks,
                           clip_end_ticks, is_radio_recording,
                           radio_recording_completed_utc_ticks
                    FROM local_items
                    ORDER BY rowid;
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    local.Items.Add(new LocalMediaItemSettings
                    {
                        Id = reader.GetString(0),
                        Title = reader.GetString(1),
                        HasCustomTitle = reader.GetInt64(2) != 0,
                        Path = reader.GetString(3),
                        DurationTicks = reader.GetInt64(4),
                        BitrateKbps = NullableInt32(reader, 5),
                        IsBitrateEstimated = reader.GetInt64(6) != 0,
                        SampleRateHz = NullableInt32(reader, 7),
                        IsFavorite = reader.GetInt64(8) != 0,
                        IsInLibrary = reader.GetInt64(9) != 0,
                        IsAvailable = reader.GetInt64(10) != 0,
                        IsInQueue = reader.GetInt64(11) != 0,
                        IsPlayNext = reader.GetInt64(12) != 0,
                        ResumePositionMode = (ResumePositionMode)reader.GetInt32(13),
                        PlaybackRateOverride = NullableDouble(reader, 14),
                        OutputDeviceId = NullableString(reader, 15),
                        ResumePositionTicks = reader.GetInt64(16),
                        FileLength = NullableInt64(reader, 17),
                        LastWriteUtcTicks = NullableInt64(reader, 18),
                        LoudnessNormalizationOverride = NullableBool(reader, 19),
                        SmoothTrackTransitionsOverride = NullableBool(reader, 20),
                        InterTrackSilenceMillisecondsOverride = NullableInt32(reader, 21),
                        ClipStartTicks = NullableInt64(reader, 22),
                        ClipEndTicks = NullableInt64(reader, 23),
                        IsRadioRecording = reader.GetInt64(24) != 0,
                        RadioRecordingCompletedUtcTicks = reader.GetInt64(25)
                    });
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, path, display_name, resume_mode FROM folder_sources ORDER BY ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    local.FolderSources.Add(new LocalFolderSourceSettings
                    {
                        Id = reader.GetString(0),
                        Path = reader.GetString(1),
                        DisplayName = reader.GetString(2),
                        ResumePositionMode = (ResumePositionMode)reader.GetInt32(3)
                    });
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT path, resume_mode, playback_rate_override, output_device_id,
                           loudness_normalization_override,
                           smooth_track_transitions_override,
                           inter_track_silence_ms_override
                    FROM folder_playback_options ORDER BY ordinal;
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    local.FolderPlaybackOptions.Add(new LocalFolderPlaybackSettings
                    {
                        Path = reader.GetString(0),
                        ResumePositionMode = (ResumePositionMode)reader.GetInt32(1),
                        PlaybackRateOverride = NullableDouble(reader, 2),
                        OutputDeviceId = NullableString(reader, 3),
                        LoudnessNormalizationOverride = NullableBool(reader, 4),
                        SmoothTrackTransitionsOverride = NullableBool(reader, 5),
                        InterTrackSilenceMillisecondsOverride = NullableInt32(reader, 6)
                    });
                }
            }

            local.ExcludedPaths = ReadOrderedStrings(connection, "excluded_paths", "path");
            local.CustomOrderItemIds = ReadOrderedStrings(connection, "custom_order", "item_id");

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT library_view, current_folder_path, current_item_id, volume, playback_rate
                    FROM local_state WHERE singleton = 1;
                    """;
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    local.LibraryView = reader.GetString(0);
                    local.CurrentFolderPath = NullableString(reader, 1);
                    local.CurrentItemId = NullableString(reader, 2);
                    local.Volume = reader.GetInt32(3);
                    local.PlaybackRate = reader.GetDouble(4);
                }
            }
            state.LocalMedia = local;

            state.Bookmarks = new BookmarkSettings();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT id, session_id, session_name, item_id, item_title, name,
                           position_ticks, created_utc_ticks, purpose, chapter_origin,
                           chapter_source_id
                    FROM bookmarks ORDER BY ordinal;
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    state.Bookmarks.Entries.Add(new BookmarkEntry
                    {
                        Id = reader.GetString(0),
                        SessionId = reader.GetString(1),
                        SessionName = reader.GetString(2),
                        ItemId = reader.GetString(3),
                        ItemTitle = reader.GetString(4),
                        Name = reader.GetString(5),
                        PositionTicks = reader.GetInt64(6),
                        CreatedUtcTicks = reader.GetInt64(7),
                        Purpose = (BookmarkPurpose)reader.GetInt32(8),
                        ChapterOrigin = (ChapterOrigin)reader.GetInt32(9),
                        ChapterSourceId = NullableString(reader, 10)
                    });
                }
            }

            state.PlaybackHistory = new PlaybackHistorySettings();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT session_id, item_id FROM playback_history ORDER BY session_id, ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var sessionId = reader.GetString(0);
                    if (!state.PlaybackHistory.ItemIdsBySession.TryGetValue(sessionId, out var ids))
                    {
                        ids = [];
                        state.PlaybackHistory.ItemIdsBySession[sessionId] = ids;
                    }
                    ids.Add(reader.GetString(1));
                }
            }

            state.CollectionOrders = new CollectionOrderSettings();
            ReadSessionOrders(
                connection,
                "favorite_added_order",
                state.CollectionOrders.FavoriteAddedItemIdsBySession);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT session_id, item_id FROM favorite_order ORDER BY session_id, ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var sessionId = reader.GetString(0);
                    if (!state.CollectionOrders.FavoriteItemIdsBySession.TryGetValue(sessionId, out var ids))
                    {
                        ids = [];
                        state.CollectionOrders.FavoriteItemIdsBySession[sessionId] = ids;
                    }
                    ids.Add(reader.GetString(1));
                }
            }

            ReadSessionOrders(
                connection,
                "library_added_order",
                state.CollectionOrders.LibraryAddedItemIdsBySession);
            ReadSessionOrders(
                connection,
                "library_custom_order",
                state.CollectionOrders.LibraryItemIdsBySession);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT session_id, item_id FROM queue_order ORDER BY session_id, ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var sessionId = reader.GetString(0);
                    if (!state.CollectionOrders.QueueItemIdsBySession.TryGetValue(sessionId, out var ids))
                    {
                        ids = [];
                        state.CollectionOrders.QueueItemIdsBySession[sessionId] = ids;
                    }
                    ids.Add(reader.GetString(1));
                }
            }

            state.Playlists = new PlaylistSettings();
            var playlistsById = new Dictionary<string, PlaylistEntry>(StringComparer.Ordinal);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, session_id, name, created_utc_ticks FROM playlists ORDER BY session_id, ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var playlist = new PlaylistEntry
                    {
                        Id = reader.GetString(0),
                        SessionId = reader.GetString(1),
                        Name = reader.GetString(2),
                        CreatedUtcTicks = reader.GetInt64(3)
                    };
                    state.Playlists.Entries.Add(playlist);
                    playlistsById[playlist.Id] = playlist;
                }
            }
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT playlist_id, item_id FROM playlist_items ORDER BY playlist_id, ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (playlistsById.TryGetValue(reader.GetString(0), out var playlist))
                    {
                        playlist.ItemIds.Add(reader.GetString(1));
                    }
                }
            }
        }
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var directory = System.IO.Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            // The application opens the database only for short transactions.
            // Avoid retaining a file handle after save, which also makes
            // backups, portable test directories and clean shutdown reliable.
            Pooling = false
        }.ToString());
        connection.CreateCollation(
            "AMC_PL",
            (left, right) => CultureInfo.GetCultureInfo("pl-PL").CompareInfo.Compare(
                left,
                right,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private SqliteConnection OpenConnection() => OpenConnection(Path);

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS local_items (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL COLLATE AMC_PL,
                has_custom_title INTEGER NOT NULL,
                path TEXT NOT NULL,
                duration_ticks INTEGER NOT NULL,
                bitrate_kbps INTEGER NULL,
                bitrate_estimated INTEGER NOT NULL,
                sample_rate_hz INTEGER NULL,
                is_favorite INTEGER NOT NULL,
                is_in_library INTEGER NOT NULL,
                is_available INTEGER NOT NULL,
                is_in_queue INTEGER NOT NULL,
                is_play_next INTEGER NOT NULL,
                resume_mode INTEGER NOT NULL,
                playback_rate_override REAL NULL,
                output_device_id TEXT NULL,
                resume_position_ticks INTEGER NOT NULL,
                file_length INTEGER NULL,
                last_write_utc_ticks INTEGER NULL,
                loudness_normalization_override INTEGER NULL,
                smooth_track_transitions_override INTEGER NULL,
                inter_track_silence_ms_override INTEGER NULL,
                clip_start_ticks INTEGER NULL,
                clip_end_ticks INTEGER NULL,
                is_radio_recording INTEGER NOT NULL DEFAULT 0,
                radio_recording_completed_utc_ticks INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_local_items_title ON local_items(title COLLATE AMC_PL);
            CREATE INDEX IF NOT EXISTS ix_local_items_path ON local_items(path COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_local_items_membership ON local_items(is_in_library, is_available, is_favorite, is_in_queue);
            CREATE TABLE IF NOT EXISTS folder_sources (
                id TEXT PRIMARY KEY,
                ordinal INTEGER NOT NULL,
                path TEXT NOT NULL,
                display_name TEXT NOT NULL COLLATE AMC_PL,
                resume_mode INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS folder_playback_options (
                ordinal INTEGER NOT NULL,
                path TEXT PRIMARY KEY,
                resume_mode INTEGER NOT NULL,
                playback_rate_override REAL NULL,
                output_device_id TEXT NULL,
                loudness_normalization_override INTEGER NULL,
                smooth_track_transitions_override INTEGER NULL,
                inter_track_silence_ms_override INTEGER NULL
            );
            CREATE TABLE IF NOT EXISTS excluded_paths (
                ordinal INTEGER PRIMARY KEY,
                path TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS custom_order (
                ordinal INTEGER PRIMARY KEY,
                item_id TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS local_state (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                library_view TEXT NOT NULL,
                current_folder_path TEXT NULL,
                current_item_id TEXT NULL,
                volume INTEGER NOT NULL,
                playback_rate REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS bookmarks (
                id TEXT PRIMARY KEY,
                ordinal INTEGER NOT NULL,
                session_id TEXT NOT NULL,
                session_name TEXT NOT NULL,
                item_id TEXT NOT NULL,
                item_title TEXT NOT NULL COLLATE AMC_PL,
                name TEXT NOT NULL COLLATE AMC_PL,
                position_ticks INTEGER NOT NULL,
                created_utc_ticks INTEGER NOT NULL,
                purpose INTEGER NOT NULL DEFAULT 1,
                chapter_origin INTEGER NOT NULL DEFAULT 0,
                chapter_source_id TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_bookmarks_item ON bookmarks(session_id, item_id, position_ticks);
            CREATE TABLE IF NOT EXISTS playback_history (
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(session_id, ordinal)
            );
            CREATE INDEX IF NOT EXISTS ix_playback_history_item ON playback_history(session_id, item_id);
            CREATE TABLE IF NOT EXISTS favorite_order (
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(session_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS favorite_added_order (
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(session_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS library_added_order (
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(session_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS library_custom_order (
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(session_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS queue_order (
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(session_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS playlists (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                name TEXT NOT NULL COLLATE AMC_PL,
                created_utc_ticks INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_playlists_session_name
                ON playlists(session_id, name COLLATE AMC_PL);
            CREATE TABLE IF NOT EXISTS playlist_items (
                playlist_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(playlist_id, ordinal),
                UNIQUE(playlist_id, item_id),
                FOREIGN KEY(playlist_id) REFERENCES playlists(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_playlist_items_item ON playlist_items(item_id);
            """;
        command.ExecuteNonQuery();

        EnsureColumn(connection, "local_items", "loudness_normalization_override", "INTEGER NULL");
        EnsureColumn(connection, "local_items", "smooth_track_transitions_override", "INTEGER NULL");
        EnsureColumn(connection, "local_items", "inter_track_silence_ms_override", "INTEGER NULL");
        EnsureColumn(connection, "local_items", "clip_start_ticks", "INTEGER NULL");
        EnsureColumn(connection, "local_items", "clip_end_ticks", "INTEGER NULL");
        EnsureColumn(connection, "local_items", "is_radio_recording", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "local_items", "radio_recording_completed_utc_ticks", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "bookmarks", "purpose", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(connection, "bookmarks", "chapter_origin", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, "bookmarks", "chapter_source_id", "TEXT NULL");
        EnsureColumn(connection, "folder_playback_options", "loudness_normalization_override", "INTEGER NULL");
        EnsureColumn(connection, "folder_playback_options", "smooth_track_transitions_override", "INTEGER NULL");
        EnsureColumn(connection, "folder_playback_options", "inter_track_silence_ms_override", "INTEGER NULL");
        using (var version = connection.CreateCommand())
        {
            version.CommandText = $"PRAGMA user_version = {DatabaseSchemaVersion};";
            version.ExecuteNonQuery();
        }

        using var metadata = connection.CreateCommand();
        metadata.CommandText = """
            INSERT INTO metadata(key, value) VALUES('database_schema_version', $version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        metadata.Parameters.AddWithValue("$version", DatabaseSchemaVersion.ToString(CultureInfo.InvariantCulture));
        metadata.ExecuteNonQuery();
    }

    private static void SaveCore(SqliteConnection connection, PersistedState state)
    {
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[]
                 {
                     "local_items", "folder_sources", "folder_playback_options",
                     "excluded_paths", "custom_order", "local_state", "bookmarks",
                     "playback_history", "favorite_added_order", "favorite_order",
                     "library_added_order", "library_custom_order", "queue_order",
                     "playlist_items", "playlists"
                 })
        {
            Execute(connection, transaction, $"DELETE FROM {table};");
        }

        foreach (var item in state.LocalMedia.Items)
        {
            Execute(
                connection,
                transaction,
                """
                INSERT INTO local_items(
                    id, title, has_custom_title, path, duration_ticks, bitrate_kbps,
                    bitrate_estimated, sample_rate_hz, is_favorite, is_in_library,
                    is_available, is_in_queue, is_play_next, resume_mode,
                    playback_rate_override, output_device_id, resume_position_ticks,
                    file_length, last_write_utc_ticks, loudness_normalization_override,
                    smooth_track_transitions_override, inter_track_silence_ms_override,
                    clip_start_ticks, clip_end_ticks, is_radio_recording,
                    radio_recording_completed_utc_ticks)
                VALUES(
                    $id, $title, $custom, $path, $duration, $bitrate, $estimated,
                    $sampleRate, $favorite, $library, $available, $queue, $playNext,
                    $resumeMode, $rate, $device, $resumePosition, $fileLength, $lastWrite,
                    $normalize, $transitions, $silence, $clipStart, $clipEnd,
                    $radioRecording, $radioRecordingCompleted);
                """,
                ("$id", item.Id), ("$title", item.Title), ("$custom", item.HasCustomTitle),
                ("$path", item.Path), ("$duration", item.DurationTicks),
                ("$bitrate", item.BitrateKbps), ("$estimated", item.IsBitrateEstimated),
                ("$sampleRate", item.SampleRateHz), ("$favorite", item.IsFavorite),
                ("$library", item.IsInLibrary), ("$available", item.IsAvailable),
                ("$queue", item.IsInQueue), ("$playNext", item.IsPlayNext),
                ("$resumeMode", (int)item.ResumePositionMode),
                ("$rate", item.PlaybackRateOverride), ("$device", item.OutputDeviceId),
                ("$resumePosition", item.ResumePositionTicks), ("$fileLength", item.FileLength),
                ("$lastWrite", item.LastWriteUtcTicks),
                ("$normalize", item.LoudnessNormalizationOverride),
                ("$transitions", item.SmoothTrackTransitionsOverride),
                ("$silence", item.InterTrackSilenceMillisecondsOverride),
                ("$clipStart", item.ClipStartTicks),
                ("$clipEnd", item.ClipEndTicks),
                ("$radioRecording", item.IsRadioRecording),
                ("$radioRecordingCompleted", item.RadioRecordingCompletedUtcTicks));
        }

        for (var index = 0; index < state.LocalMedia.FolderSources.Count; index++)
        {
            var source = state.LocalMedia.FolderSources[index];
            Execute(connection, transaction,
                "INSERT INTO folder_sources(id, ordinal, path, display_name, resume_mode) VALUES($id, $ordinal, $path, $name, $mode);",
                ("$id", source.Id), ("$ordinal", index), ("$path", source.Path),
                ("$name", source.DisplayName), ("$mode", (int)source.ResumePositionMode));
        }

        for (var index = 0; index < state.LocalMedia.FolderPlaybackOptions.Count; index++)
        {
            var option = state.LocalMedia.FolderPlaybackOptions[index];
            Execute(connection, transaction,
                """
                INSERT INTO folder_playback_options(
                    ordinal, path, resume_mode, playback_rate_override, output_device_id,
                    loudness_normalization_override, smooth_track_transitions_override,
                    inter_track_silence_ms_override)
                VALUES($ordinal, $path, $mode, $rate, $device, $normalize, $transitions, $silence);
                """,
                ("$ordinal", index), ("$path", option.Path), ("$mode", (int)option.ResumePositionMode),
                ("$rate", option.PlaybackRateOverride), ("$device", option.OutputDeviceId),
                ("$normalize", option.LoudnessNormalizationOverride),
                ("$transitions", option.SmoothTrackTransitionsOverride),
                ("$silence", option.InterTrackSilenceMillisecondsOverride));
        }

        InsertOrderedStrings(connection, transaction, "excluded_paths", "path", state.LocalMedia.ExcludedPaths);
        InsertOrderedStrings(connection, transaction, "custom_order", "item_id", state.LocalMedia.CustomOrderItemIds);
        Execute(connection, transaction,
            "INSERT INTO local_state(singleton, library_view, current_folder_path, current_item_id, volume, playback_rate) VALUES(1, $view, $folder, $item, $volume, $rate);",
            ("$view", state.LocalMedia.LibraryView), ("$folder", state.LocalMedia.CurrentFolderPath),
            ("$item", state.LocalMedia.CurrentItemId), ("$volume", state.LocalMedia.Volume),
            ("$rate", state.LocalMedia.PlaybackRate));

        for (var index = 0; index < state.Bookmarks.Entries.Count; index++)
        {
            var bookmark = state.Bookmarks.Entries[index];
            Execute(connection, transaction,
                """
                INSERT INTO bookmarks(id, ordinal, session_id, session_name, item_id,
                                      item_title, name, position_ticks, created_utc_ticks,
                                      purpose, chapter_origin, chapter_source_id)
                VALUES($id, $ordinal, $sessionId, $sessionName, $itemId, $itemTitle,
                       $name, $position, $created, $purpose, $chapterOrigin, $chapterSourceId);
                """,
                ("$id", bookmark.Id), ("$ordinal", index), ("$sessionId", bookmark.SessionId),
                ("$sessionName", bookmark.SessionName), ("$itemId", bookmark.ItemId),
                ("$itemTitle", bookmark.ItemTitle), ("$name", bookmark.Name),
                ("$position", bookmark.PositionTicks), ("$created", bookmark.CreatedUtcTicks),
                ("$purpose", (int)bookmark.Purpose), ("$chapterOrigin", (int)bookmark.ChapterOrigin),
                ("$chapterSourceId", bookmark.ChapterSourceId));
        }

        foreach (var pair in state.PlaybackHistory.ItemIdsBySession)
        {
            for (var index = 0; index < pair.Value.Count; index++)
            {
                Execute(connection, transaction,
                    "INSERT INTO playback_history(session_id, ordinal, item_id) VALUES($session, $ordinal, $item);",
                    ("$session", pair.Key), ("$ordinal", index), ("$item", pair.Value[index]));
            }
        }

        InsertSessionOrders(
            connection,
            transaction,
            "favorite_added_order",
            state.CollectionOrders.FavoriteAddedItemIdsBySession);

        foreach (var pair in state.CollectionOrders.FavoriteItemIdsBySession)
        {
            for (var index = 0; index < pair.Value.Count; index++)
            {
                Execute(connection, transaction,
                    "INSERT INTO favorite_order(session_id, ordinal, item_id) VALUES($session, $ordinal, $item);",
                    ("$session", pair.Key), ("$ordinal", index), ("$item", pair.Value[index]));
            }
        }

        InsertSessionOrders(
            connection,
            transaction,
            "library_added_order",
            state.CollectionOrders.LibraryAddedItemIdsBySession);
        InsertSessionOrders(
            connection,
            transaction,
            "library_custom_order",
            state.CollectionOrders.LibraryItemIdsBySession);

        foreach (var pair in state.CollectionOrders.QueueItemIdsBySession)
        {
            for (var index = 0; index < pair.Value.Count; index++)
            {
                Execute(connection, transaction,
                    "INSERT INTO queue_order(session_id, ordinal, item_id) VALUES($session, $ordinal, $item);",
                    ("$session", pair.Key), ("$ordinal", index), ("$item", pair.Value[index]));
            }
        }

        var playlistOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var playlist in state.Playlists.Entries)
        {
            var ordinal = playlistOrdinals.GetValueOrDefault(playlist.SessionId);
            playlistOrdinals[playlist.SessionId] = ordinal + 1;
            Execute(connection, transaction,
                "INSERT INTO playlists(id, session_id, ordinal, name, created_utc_ticks) VALUES($id, $session, $ordinal, $name, $created);",
                ("$id", playlist.Id), ("$session", playlist.SessionId), ("$ordinal", ordinal),
                ("$name", playlist.Name), ("$created", playlist.CreatedUtcTicks));
            for (var itemIndex = 0; itemIndex < playlist.ItemIds.Count; itemIndex++)
            {
                Execute(connection, transaction,
                    "INSERT INTO playlist_items(playlist_id, ordinal, item_id) VALUES($playlist, $ordinal, $item);",
                    ("$playlist", playlist.Id), ("$ordinal", itemIndex), ("$item", playlist.ItemIds[itemIndex]));
            }
        }

        Execute(connection, transaction,
            """
            INSERT INTO metadata(key, value) VALUES('library_initialized', '1')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """);
        Execute(connection, transaction,
            """
            INSERT INTO metadata(key, value) VALUES('last_saved_utc', $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        transaction.Commit();
    }

    private static void ReadSessionOrders(
        SqliteConnection connection,
        string table,
        IDictionary<string, List<string>> destination)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT session_id, item_id FROM {table} ORDER BY session_id, ordinal;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var sessionId = reader.GetString(0);
            if (!destination.TryGetValue(sessionId, out var ids))
            {
                ids = [];
                destination[sessionId] = ids;
            }
            ids.Add(reader.GetString(1));
        }
    }

    private static void InsertSessionOrders(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        IReadOnlyDictionary<string, List<string>> source)
    {
        foreach (var pair in source)
        {
            for (var index = 0; index < pair.Value.Count; index++)
            {
                Execute(
                    connection,
                    transaction,
                    $"INSERT INTO {table}(session_id, ordinal, item_id) VALUES($session, $ordinal, $item);",
                    ("$session", pair.Key),
                    ("$ordinal", index),
                    ("$item", pair.Value[index]));
            }
        }
    }

    private static void InsertOrderedStrings(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string column,
        IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            Execute(connection, transaction,
                $"INSERT INTO {table}(ordinal, {column}) VALUES($ordinal, $value);",
                ("$ordinal", index), ("$value", values[index]));
        }
    }

    private static List<string> ReadOrderedStrings(
        SqliteConnection connection,
        string table,
        string column)
    {
        var values = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM {table} ORDER BY ordinal;";
        using var reader = command.ExecuteReader();
        while (reader.Read()) values.Add(reader.GetString(0));
        return values;
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            var value = parameter.Value switch
            {
                null => DBNull.Value,
                bool boolean => boolean ? 1 : 0,
                _ => parameter.Value
            };
            command.Parameters.AddWithValue(parameter.Name, value);
        }
        command.ExecuteNonQuery();
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        string table,
        string column,
        string declaration)
    {
        using var inspect = connection.CreateCommand();
        inspect.CommandText = $"PRAGMA table_info({table});";
        using (var reader = inspect.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {declaration};";
        alter.ExecuteNonQuery();
    }

    private static bool? NullableBool(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt64(index) != 0;

    private static int? NullableInt32(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt32(index);

    private static long? NullableInt64(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt64(index);

    private static double? NullableDouble(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetDouble(index);

    private static string? NullableString(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);
}
