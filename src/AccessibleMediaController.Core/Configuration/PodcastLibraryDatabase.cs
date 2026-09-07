using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Durable storage for podcast subscriptions and the retained episode archive.
/// Large collections live outside state.json. Rows are updated only when their
/// deterministic content fingerprint changes, so playback-position saves do not
/// rewrite tens of thousands of unchanged episodes.
/// </summary>
internal sealed class PodcastLibraryDatabase(string databasePath)
{
    private const int DatabaseSchemaVersion = 1;
    private readonly object _gate = new();
    private static readonly JsonSerializerOptions PayloadJsonOptions = new();

    public string Path { get; } = databasePath;

    public bool IsInitialized()
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM metadata WHERE key = 'podcasts_initialized';";
            return string.Equals(command.ExecuteScalar() as string, "1", StringComparison.Ordinal);
        }
    }

    public void Initialize(PodcastSettings settings)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            SaveCore(connection, settings);

            var storedSubscriptions = ReadCount(connection, "podcast_subscriptions");
            var storedEpisodes = ReadCount(connection, "podcast_episodes");
            if (storedSubscriptions != settings.Subscriptions.Count
                || storedEpisodes != settings.Episodes.Count)
            {
                throw new InvalidDataException(
                    "Migracja Podcastów do SQLite nie zachowała wszystkich danych. "
                    + $"Kanały: {storedSubscriptions} z {settings.Subscriptions.Count}; "
                    + $"odcinki: {storedEpisodes} z {settings.Episodes.Count}.");
            }
        }
    }

    public void Save(PodcastSettings settings)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            SaveCore(connection, settings);
        }
    }

    public void LoadInto(PodcastSettings settings)
    {
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            ValidateIntegrity(connection);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT downloads_folder, current_item_id, volume, playback_rate
                    FROM podcast_state WHERE singleton = 1;
                    """;
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    settings.DownloadsFolder = NullableString(reader, 0);
                    settings.CurrentItemId = NullableString(reader, 1);
                    settings.Volume = reader.GetInt32(2);
                    settings.PlaybackRate = reader.GetDouble(3);
                }
            }

            settings.Subscriptions = [];
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT payload_json FROM podcast_subscriptions ORDER BY ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var item = JsonSerializer.Deserialize<PodcastSubscriptionSettings>(
                        reader.GetString(0),
                        PayloadJsonOptions);
                    if (item is not null) settings.Subscriptions.Add(item);
                }
            }

            settings.Episodes = [];
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT payload_json FROM podcast_episodes ORDER BY ordinal;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var item = JsonSerializer.Deserialize<PodcastEpisodeSettings>(
                        reader.GetString(0),
                        PayloadJsonOptions);
                    if (item is not null) settings.Episodes.Add(item);
                }
            }
        }
    }

    private static void SaveCore(SqliteConnection connection, PodcastSettings settings)
    {
        using var transaction = connection.BeginTransaction();
        Execute(
            connection,
            transaction,
            """
            INSERT INTO podcast_state(
                singleton, downloads_folder, current_item_id, volume, playback_rate)
            VALUES(1, $downloads, $current, $volume, $rate)
            ON CONFLICT(singleton) DO UPDATE SET
                downloads_folder = excluded.downloads_folder,
                current_item_id = excluded.current_item_id,
                volume = excluded.volume,
                playback_rate = excluded.playback_rate;
            """,
            ("$downloads", settings.DownloadsFolder),
            ("$current", settings.CurrentItemId),
            ("$volume", settings.Volume),
            ("$rate", settings.PlaybackRate));

        var subscriptionHashes = ReadHashes(connection, transaction, "podcast_subscriptions");
        for (var ordinal = 0; ordinal < settings.Subscriptions.Count; ordinal++)
        {
            var item = settings.Subscriptions[ordinal];
            var hash = Fingerprint(item);
            if (!subscriptionHashes.Remove(item.Id, out var stored)
                || !string.Equals(stored.Hash, hash, StringComparison.Ordinal)
                || stored.Ordinal != ordinal)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO podcast_subscriptions(
                        id, ordinal, title, feed_url, is_in_library,
                        last_refresh_utc_ticks, content_hash, payload_json)
                    VALUES($id, $ordinal, $title, $feed, $library, $refresh, $hash, $payload)
                    ON CONFLICT(id) DO UPDATE SET
                        ordinal = excluded.ordinal,
                        title = excluded.title,
                        feed_url = excluded.feed_url,
                        is_in_library = excluded.is_in_library,
                        last_refresh_utc_ticks = excluded.last_refresh_utc_ticks,
                        content_hash = excluded.content_hash,
                        payload_json = excluded.payload_json;
                    """,
                    ("$id", item.Id),
                    ("$ordinal", ordinal),
                    ("$title", item.Title),
                    ("$feed", item.FeedUrl),
                    ("$library", item.IsInLibrary),
                    ("$refresh", item.LastRefreshUtcTicks),
                    ("$hash", hash),
                    ("$payload", JsonSerializer.Serialize(item, PayloadJsonOptions)));
            }
        }
        var episodeHashes = ReadHashes(connection, transaction, "podcast_episodes");
        for (var ordinal = 0; ordinal < settings.Episodes.Count; ordinal++)
        {
            var item = settings.Episodes[ordinal];
            var hash = Fingerprint(item);
            if (!episodeHashes.Remove(item.Id, out var stored)
                || !string.Equals(stored.Hash, hash, StringComparison.Ordinal)
                || stored.Ordinal != ordinal)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO podcast_episodes(
                        id, ordinal, subscription_id, title, published_utc_ticks,
                        is_new, is_started, is_played, is_favorite, is_in_queue,
                        is_play_next, download_path, content_hash, payload_json)
                    VALUES(
                        $id, $ordinal, $subscription, $title, $published,
                        $new, $started, $played, $favorite, $queue,
                        $playNext, $download, $hash, $payload)
                    ON CONFLICT(id) DO UPDATE SET
                        ordinal = excluded.ordinal,
                        subscription_id = excluded.subscription_id,
                        title = excluded.title,
                        published_utc_ticks = excluded.published_utc_ticks,
                        is_new = excluded.is_new,
                        is_started = excluded.is_started,
                        is_played = excluded.is_played,
                        is_favorite = excluded.is_favorite,
                        is_in_queue = excluded.is_in_queue,
                        is_play_next = excluded.is_play_next,
                        download_path = excluded.download_path,
                        content_hash = excluded.content_hash,
                        payload_json = excluded.payload_json;
                    """,
                    ("$id", item.Id),
                    ("$ordinal", ordinal),
                    ("$subscription", item.SubscriptionId),
                    ("$title", item.Title),
                    ("$published", item.PublishedUtcTicks),
                    ("$new", item.IsNew),
                    ("$started", item.IsStarted),
                    ("$played", item.IsPlayed),
                    ("$favorite", item.IsFavorite),
                    ("$queue", item.IsInQueue),
                    ("$playNext", item.IsPlayNext),
                    ("$download", item.DownloadPath),
                    ("$hash", hash),
                    ("$payload", JsonSerializer.Serialize(item, PayloadJsonOptions)));
            }
        }
        DeleteMissing(connection, transaction, "podcast_episodes", episodeHashes.Keys);
        DeleteMissing(connection, transaction, "podcast_subscriptions", subscriptionHashes.Keys);

        Execute(
            connection,
            transaction,
            """
            INSERT INTO metadata(key, value) VALUES('podcasts_initialized', '1')
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """);
        Execute(
            connection,
            transaction,
            """
            INSERT INTO metadata(key, value) VALUES('last_saved_utc', $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        transaction.Commit();
    }

    private static Dictionary<string, StoredFingerprint> ReadHashes(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table)
    {
        var result = new Dictionary<string, StoredFingerprint>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT id, ordinal, content_hash FROM {table};";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = new StoredFingerprint(reader.GetInt32(1), reader.GetString(2));
        }
        return result;
    }

    private static void DeleteMissing(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            Execute(connection, transaction, $"DELETE FROM {table} WHERE id = $id;", ("$id", id));
        }
    }

    private static string Fingerprint(PodcastSubscriptionSettings item)
    {
        var hash = new StableFingerprint();
        hash.Add(item.Id);
        hash.Add(item.Title);
        hash.Add(item.HasCustomTitle);
        hash.Add(item.Author);
        hash.Add(item.Description);
        hash.Add(item.FeedUrl);
        hash.Add(item.HomepageUrl);
        hash.Add(item.LastRefreshUtcTicks);
        hash.Add(item.RefreshIntervalMinutes);
        hash.Add(item.DownloadsFolder);
        hash.Add((int)item.ResumePositionMode);
        hash.Add(item.PlaybackRateOverride);
        hash.Add(item.LoudnessNormalizationOverride);
        hash.Add(item.SmoothTrackTransitionsOverride);
        hash.Add(item.InterTrackSilenceMillisecondsOverride);
        hash.Add(item.IsFavorite);
        hash.Add(item.IsInLibrary);
        return hash.ToString();
    }

    private static string Fingerprint(PodcastEpisodeSettings item)
    {
        var hash = new StableFingerprint();
        hash.Add(item.Id);
        hash.Add(item.SubscriptionId);
        hash.Add(item.SourceIdentifier);
        hash.Add(item.Title);
        hash.Add(item.Author);
        hash.Add(item.Description);
        hash.Add(item.MediaUrl);
        hash.Add(item.PageUrl);
        hash.Add(item.MediaType);
        hash.Add(item.MediaLength);
        hash.Add(item.ProviderChaptersUrl);
        hash.Add(item.ProviderChaptersLoadedUrl);
        hash.Add(item.EmbeddedChaptersSignature);
        hash.Add(item.HasFeedChapters);
        hash.Add(item.PublishedUtcTicks);
        hash.Add(item.FeedOrdinal);
        hash.Add(item.DurationTicks);
        hash.Add(item.ResumePositionTicks);
        hash.Add((int)item.ResumePositionMode);
        hash.Add(item.PlaybackRateOverride);
        hash.Add(item.LoudnessNormalizationOverride);
        hash.Add(item.SmoothTrackTransitionsOverride);
        hash.Add(item.InterTrackSilenceMillisecondsOverride);
        hash.Add(item.DownloadPath);
        hash.Add(item.IsNew);
        hash.Add(item.IsStarted);
        hash.Add(item.IsPlayed);
        hash.Add(item.IsFavorite);
        hash.Add(item.IsInQueue);
        hash.Add(item.IsPlayNext);
        hash.Add(item.ClipStartTicks);
        hash.Add(item.ClipEndTicks);
        return hash.ToString();
    }

    private SqliteConnection OpenConnection()
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS podcast_state (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                downloads_folder TEXT NULL,
                current_item_id TEXT NULL,
                volume INTEGER NOT NULL,
                playback_rate REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS podcast_subscriptions (
                id TEXT PRIMARY KEY,
                ordinal INTEGER NOT NULL,
                title TEXT NOT NULL,
                feed_url TEXT NOT NULL,
                is_in_library INTEGER NOT NULL,
                last_refresh_utc_ticks INTEGER NOT NULL,
                content_hash TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_podcast_subscriptions_library_title
                ON podcast_subscriptions(is_in_library, title COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS ix_podcast_subscriptions_feed
                ON podcast_subscriptions(feed_url COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS podcast_episodes (
                id TEXT PRIMARY KEY,
                ordinal INTEGER NOT NULL,
                subscription_id TEXT NOT NULL,
                title TEXT NOT NULL,
                published_utc_ticks INTEGER NOT NULL,
                is_new INTEGER NOT NULL,
                is_started INTEGER NOT NULL,
                is_played INTEGER NOT NULL,
                is_favorite INTEGER NOT NULL,
                is_in_queue INTEGER NOT NULL,
                is_play_next INTEGER NOT NULL,
                download_path TEXT NULL,
                content_hash TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                FOREIGN KEY(subscription_id) REFERENCES podcast_subscriptions(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS ix_podcast_episodes_subscription_date
                ON podcast_episodes(subscription_id, published_utc_ticks DESC);
            CREATE INDEX IF NOT EXISTS ix_podcast_episodes_inbox
                ON podcast_episodes(is_new, is_played, published_utc_ticks DESC);
            CREATE INDEX IF NOT EXISTS ix_podcast_episodes_progress
                ON podcast_episodes(is_started, is_played, published_utc_ticks DESC);
            CREATE INDEX IF NOT EXISTS ix_podcast_episodes_membership
                ON podcast_episodes(is_favorite, is_in_queue, is_play_next);
            CREATE INDEX IF NOT EXISTS ix_podcast_episodes_download
                ON podcast_episodes(download_path) WHERE download_path IS NOT NULL;
            PRAGMA user_version = 1;
            """;
        command.ExecuteNonQuery();
        using var metadata = connection.CreateCommand();
        metadata.CommandText = """
            INSERT INTO metadata(key, value) VALUES('database_schema_version', $version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        metadata.Parameters.AddWithValue("$version", DatabaseSchemaVersion.ToString(CultureInfo.InvariantCulture));
        metadata.ExecuteNonQuery();
    }

    private static int ReadCount(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void ValidateIntegrity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check(1);";
        var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Kontrola integralności bazy Podcastów nie powiodła się: {result ?? "brak wyniku"}.");
        }
    }

    private static string? NullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

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

    private readonly record struct StoredFingerprint(int Ordinal, string Hash);

    private struct StableFingerprint
    {
        private ulong _value;

        public void Add(string? value)
        {
            EnsureStarted();
            if (value is null)
            {
                AddByte(0xff);
                return;
            }
            foreach (var character in value)
            {
                AddByte((byte)character);
                AddByte((byte)(character >> 8));
            }
            AddByte(0);
        }

        public void Add(bool value) => Add(value ? 1L : 0L);
        public void Add(bool? value) => Add(value.HasValue ? value.Value ? 1L : 0L : long.MinValue);
        public void Add(int value) => Add((long)value);
        public void Add(int? value) => Add(value.HasValue ? value.Value : long.MinValue);
        public void Add(long value)
        {
            EnsureStarted();
            for (var shift = 0; shift < 64; shift += 8) AddByte((byte)(value >> shift));
        }
        public void Add(long? value) => Add(value ?? long.MinValue);
        public void Add(double? value) => Add(value.HasValue
            ? BitConverter.DoubleToInt64Bits(value.Value)
            : long.MinValue);

        public override readonly string ToString() => _value.ToString("x16", CultureInfo.InvariantCulture);

        private void EnsureStarted()
        {
            if (_value == 0) _value = 14695981039346656037UL;
        }

        private void AddByte(byte value)
        {
            _value ^= value;
            _value *= 1099511628211UL;
        }
    }
}
