using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Podcasts;
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
                    SELECT downloads_folder, current_item_id, volume, playback_rate,
                           rss_refresh_interval_minutes, youtube_refresh_interval_minutes,
                           automatic_refresh_batch_size
                    FROM podcast_state WHERE singleton = 1;
                    """;
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    settings.DownloadsFolder = NullableString(reader, 0);
                    settings.CurrentItemId = NullableString(reader, 1);
                    settings.Volume = reader.GetInt32(2);
                    settings.PlaybackRate = reader.GetDouble(3);
                    settings.RssRefreshIntervalMinutes = reader.GetInt32(4);
                    settings.YouTubeRefreshIntervalMinutes = reader.GetInt32(5);
                    settings.AutomaticRefreshBatchSize = reader.GetInt32(6);
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
                singleton, downloads_folder, current_item_id, volume, playback_rate,
                rss_refresh_interval_minutes, youtube_refresh_interval_minutes,
                automatic_refresh_batch_size)
            VALUES(1, $downloads, $current, $volume, $rate, $rssInterval, $youTubeInterval, $batch)
            ON CONFLICT(singleton) DO UPDATE SET
                downloads_folder = excluded.downloads_folder,
                current_item_id = excluded.current_item_id,
                volume = excluded.volume,
                playback_rate = excluded.playback_rate,
                rss_refresh_interval_minutes = excluded.rss_refresh_interval_minutes,
                youtube_refresh_interval_minutes = excluded.youtube_refresh_interval_minutes,
                automatic_refresh_batch_size = excluded.automatic_refresh_batch_size;
            """,
            ("$downloads", settings.DownloadsFolder),
            ("$current", settings.CurrentItemId),
            ("$volume", settings.Volume),
            ("$rate", settings.PlaybackRate),
            ("$rssInterval", settings.RssRefreshIntervalMinutes),
            ("$youTubeInterval", settings.YouTubeRefreshIntervalMinutes),
            ("$batch", settings.AutomaticRefreshBatchSize));

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

    public PodcastPlaybackCheckpointResult SavePlaybackCheckpoint(
        string episodeId,
        TimeSpan position,
        TimeSpan duration,
        bool completed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(episodeId);
        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            using var transaction = connection.BeginTransaction();

            PodcastEpisodeSettings episode;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT payload_json FROM podcast_episodes WHERE id = $id;";
                read.Parameters.AddWithValue("$id", episodeId);
                var payload = read.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(payload))
                {
                    throw new KeyNotFoundException(
                        "Odcinka nie ma już w bibliotece Podcastów i YouTube.");
                }
                episode = JsonSerializer.Deserialize<PodcastEpisodeSettings>(
                    payload,
                    PayloadJsonOptions)
                    ?? throw new InvalidDataException(
                        "Nie można odczytać zapisanego stanu odcinka.");
            }

            var previousPosition = episode.ResumePositionTicks;
            var previousDuration = episode.DurationTicks;
            var previousNew = episode.IsNew;
            var previousStarted = episode.IsStarted;
            var previousPlayed = episode.IsPlayed;

            var normalizedDuration = duration > TimeSpan.Zero
                ? Math.Max(0, duration.Ticks)
                : episode.DurationTicks;
            episode.DurationTicks = normalizedDuration;
            if (completed)
            {
                // Ten sam skutek co DemoMediaSession.ContinueAfterPlaybackEnded:
                // odtworzony odcinek przy kolejnym jawnym uruchomieniu zaczyna
                // sie od poczatku, a jego stan na liscie brzmi "odtworzony".
                episode.ResumePositionTicks = 0;
                PodcastEpisodeProgress.MarkPlayed(episode);
            }
            else
            {
                var ticks = Math.Max(0, position.Ticks);
                if (normalizedDuration > 0) ticks = Math.Min(ticks, normalizedDuration);
                episode.ResumePositionTicks = ticks;
                PodcastEpisodeProgress.UpdateFromPosition(
                    episode,
                    TimeSpan.FromTicks(ticks));
            }

            var changed = previousPosition != episode.ResumePositionTicks
                || previousDuration != episode.DurationTicks
                || previousNew != episode.IsNew
                || previousStarted != episode.IsStarted
                || previousPlayed != episode.IsPlayed;
            if (changed)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    UPDATE podcast_episodes SET
                        is_new = $new,
                        is_started = $started,
                        is_played = $played,
                        content_hash = $hash,
                        payload_json = $payload
                    WHERE id = $id;
                    """,
                    ("$new", episode.IsNew),
                    ("$started", episode.IsStarted),
                    ("$played", episode.IsPlayed),
                    ("$hash", Fingerprint(episode)),
                    ("$payload", JsonSerializer.Serialize(episode, PayloadJsonOptions)),
                    ("$id", episode.Id));
            }

            Execute(
                connection,
                transaction,
                "UPDATE podcast_state SET current_item_id = $id WHERE singleton = 1;",
                ("$id", episode.Id));
            if (changed)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO metadata(key, value) VALUES('last_saved_utc', $value)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                    """,
                    ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }
            transaction.Commit();
            return new PodcastPlaybackCheckpointResult(
                changed,
                completed,
                TimeSpan.FromTicks(episode.ResumePositionTicks),
                TimeSpan.FromTicks(episode.DurationTicks),
                PodcastEpisodeProgress.GetLabel(episode));
        }
    }

    public IReadOnlyList<PodcastRefreshTarget> GetRefreshTargets(string? subscriptionId)
    {
        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                var selected = settings.Subscriptions.FirstOrDefault(subscription =>
                    subscription.IsInLibrary
                    && string.Equals(subscription.Id, subscriptionId, StringComparison.Ordinal));
                if (selected is null)
                    throw new KeyNotFoundException("Tego podcastu nie ma już w Bibliotece.");
                if (!IsRefreshable(selected.SourceKind))
                {
                    throw new InvalidOperationException(
                        "Publiczne medium internetowe jest sprawdzane ponownie przy każdym odtwarzaniu.");
                }
                return [ToRefreshTarget(selected)];
            }

            return settings.Subscriptions
                .Where(subscription => subscription.IsInLibrary && IsRefreshable(subscription.SourceKind))
                .Select(ToRefreshTarget)
                .ToArray();
        }
    }

    public int GetInboxCount()
    {
        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var librarySubscriptionIds = settings.Subscriptions
                .Where(subscription => subscription.IsInLibrary)
                .Select(subscription => subscription.Id)
                .ToHashSet(StringComparer.Ordinal);
            return settings.Episodes.Count(episode =>
                episode.IsNew
                && !episode.IsPlayed
                && librarySubscriptionIds.Contains(episode.SubscriptionId));
        }
    }

    public PodcastFavoriteToggleResult ToggleFavorites(
        IReadOnlyCollection<string> subscriptionIds,
        IReadOnlyCollection<string> episodeIds)
    {
        ArgumentNullException.ThrowIfNull(subscriptionIds);
        ArgumentNullException.ThrowIfNull(episodeIds);
        var requestedSubscriptions = subscriptionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var requestedEpisodes = episodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestedSubscriptions.Length + requestedEpisodes.Length == 0)
            throw new ArgumentException("Nie wybrano podcastu ani odcinka.");

        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            using var transaction = connection.BeginTransaction();
            var subscriptions = new List<PodcastSubscriptionSettings>();
            foreach (var id in requestedSubscriptions)
            {
                using var read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT payload_json FROM podcast_subscriptions WHERE id = $id AND is_in_library = 1;";
                read.Parameters.AddWithValue("$id", id);
                var payload = read.ExecuteScalar() as string;
                var item = string.IsNullOrWhiteSpace(payload)
                    ? null
                    : JsonSerializer.Deserialize<PodcastSubscriptionSettings>(
                        payload,
                        PayloadJsonOptions);
                if (item is null)
                    throw new KeyNotFoundException(
                        "Niektórych wybranych podcastów nie ma już w Bibliotece.");
                subscriptions.Add(item);
            }

            var episodes = new List<PodcastEpisodeSettings>();
            foreach (var id in requestedEpisodes)
            {
                using var read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT payload_json FROM podcast_episodes WHERE id = $id;";
                read.Parameters.AddWithValue("$id", id);
                var payload = read.ExecuteScalar() as string;
                var item = string.IsNullOrWhiteSpace(payload)
                    ? null
                    : JsonSerializer.Deserialize<PodcastEpisodeSettings>(
                        payload,
                        PayloadJsonOptions);
                if (item is null)
                    throw new KeyNotFoundException(
                        "Niektórych wybranych odcinków nie ma już w Bibliotece.");
                episodes.Add(item);
            }

            var favorite = !subscriptions.All(item => item.IsFavorite)
                || !episodes.All(item => item.IsFavorite);
            var changed = 0;
            foreach (var item in subscriptions)
            {
                if (item.IsFavorite == favorite) continue;
                item.IsFavorite = favorite;
                Execute(
                    connection,
                    transaction,
                    """
                    UPDATE podcast_subscriptions
                    SET content_hash = $hash, payload_json = $payload
                    WHERE id = $id;
                    """,
                    ("$hash", Fingerprint(item)),
                    ("$payload", JsonSerializer.Serialize(item, PayloadJsonOptions)),
                    ("$id", item.Id));
                changed++;
            }
            foreach (var item in episodes)
            {
                if (item.IsFavorite == favorite) continue;
                item.IsFavorite = favorite;
                Execute(
                    connection,
                    transaction,
                    """
                    UPDATE podcast_episodes
                    SET is_favorite = $favorite,
                        content_hash = $hash,
                        payload_json = $payload
                    WHERE id = $id;
                    """,
                    ("$favorite", favorite),
                    ("$hash", Fingerprint(item)),
                    ("$payload", JsonSerializer.Serialize(item, PayloadJsonOptions)),
                    ("$id", item.Id));
                changed++;
            }
            if (changed > 0)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO metadata(key, value) VALUES('last_saved_utc', $value)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                    """,
                    ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }
            transaction.Commit();
            return new PodcastFavoriteToggleResult(
                favorite,
                subscriptions.Count + episodes.Count,
                changed);
        }
    }

    public IReadOnlyList<PodcastOpmlEntry> GetOpmlEntries()
    {
        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var result = new List<PodcastOpmlEntry>();
            foreach (var subscription in settings.Subscriptions.Where(item =>
                         item.IsInLibrary && item.SourceKind == PodcastSourceKind.Rss))
            {
                if (!TryPublicHttpUri(subscription.FeedUrl, out var feed)) continue;
                var homepage = TryPublicHttpUri(subscription.HomepageUrl, out var page)
                    ? page
                    : null;
                result.Add(new PodcastOpmlEntry(subscription.Title, feed, homepage));
            }
            return result;
        }
    }

    public IReadOnlyList<YouTubeCollectionExportEntry> GetYouTubeCollectionsForExport()
    {
        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var result = new List<YouTubeCollectionExportEntry>();
            foreach (var subscription in settings.Subscriptions.Where(item =>
                         item.IsInLibrary
                         && item.SourceKind is PodcastSourceKind.YouTubeChannel
                             or PodcastSourceKind.YouTubePlaylist))
            {
                if (!YouTubeSubscriptionsExporter.TryParseSubscriptionId(
                        subscription.Id,
                        out var sourceIdentifier,
                        out var isChannel))
                {
                    continue;
                }
                result.Add(new YouTubeCollectionExportEntry(
                    subscription.Title,
                    sourceIdentifier,
                    isChannel,
                    subscription.HomepageUrl));
            }
            return result;
        }
    }

    public IReadOnlyList<PodcastDownloadTarget> GetDownloadTargets(
        IReadOnlyCollection<string> episodeIds)
    {
        ArgumentNullException.ThrowIfNull(episodeIds);
        var requestedIds = episodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestedIds.Length == 0) return [];

        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var episodes = settings.Episodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var subscriptions = settings.Subscriptions.ToDictionary(item => item.Id, StringComparer.Ordinal);
            var result = new List<PodcastDownloadTarget>(requestedIds.Length);
            foreach (var episodeId in requestedIds)
            {
                if (!episodes.TryGetValue(episodeId, out var episode)) continue;
                subscriptions.TryGetValue(episode.SubscriptionId, out var subscription);
                result.Add(new PodcastDownloadTarget(
                    episode.Id,
                    episode.Title,
                    episode.MediaUrl,
                    episode.MediaType,
                    episode.DownloadPath,
                    PodcastPlaybackSettingsResolver.ConfiguredDownloadFolder(
                        settings.DownloadsFolder,
                        subscription)));
            }
            return result;
        }
    }

    public PodcastDownloadPathResult SaveDownloadPath(
        string episodeId,
        string downloadPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(episodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadPath);
        var fullPath = System.IO.Path.GetFullPath(downloadPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Pobrany plik odcinka nie istnieje.", fullPath);

        lock (_gate)
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            using var transaction = connection.BeginTransaction();

            PodcastEpisodeSettings episode;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT payload_json FROM podcast_episodes WHERE id = $id;";
                read.Parameters.AddWithValue("$id", episodeId);
                var payload = read.ExecuteScalar() as string;
                if (string.IsNullOrWhiteSpace(payload))
                    throw new KeyNotFoundException(
                        "Odcinka nie ma już w bibliotece Podcastów i YouTube.");
                episode = JsonSerializer.Deserialize<PodcastEpisodeSettings>(
                    payload,
                    PayloadJsonOptions)
                    ?? throw new InvalidDataException(
                        "Nie można odczytać zapisanego stanu odcinka.");
            }

            var changed = !string.Equals(
                episode.DownloadPath,
                fullPath,
                StringComparison.OrdinalIgnoreCase);
            if (changed)
            {
                episode.DownloadPath = fullPath;
                Execute(
                    connection,
                    transaction,
                    """
                    UPDATE podcast_episodes SET
                        download_path = $download,
                        content_hash = $hash,
                        payload_json = $payload
                    WHERE id = $id;
                    """,
                    ("$download", fullPath),
                    ("$hash", Fingerprint(episode)),
                    ("$payload", JsonSerializer.Serialize(episode, PayloadJsonOptions)),
                    ("$id", episode.Id));
                Execute(
                    connection,
                    transaction,
                    """
                    INSERT INTO metadata(key, value) VALUES('last_saved_utc', $value)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                    """,
                    ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            }
            transaction.Commit();
            return new PodcastDownloadPathResult(episode.Id, fullPath, changed);
        }
    }

    public PodcastSourceAddResult AddSource(
        PodcastFeedDocument feed,
        string? titleOverride,
        PodcastSourceKind sourceKind,
        DateTime addedUtc,
        BookmarkSettings? bookmarks = null)
    {
        ArgumentNullException.ThrowIfNull(feed);
        if (sourceKind is not (PodcastSourceKind.Rss
            or PodcastSourceKind.YouTubeChannel
            or PodcastSourceKind.YouTubePlaylist))
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceKind),
                "Ten rodzaj źródła nie jest kanałem podcastu ani kolekcją YouTube.");
        }

        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var update = PodcastLibraryUpdater.Apply(
                settings,
                feed,
                titleOverride,
                addedUtc,
                bookmarks,
                sourceKind,
                addToLibrary: true);
            if (update.AddedSubscription)
            {
                update.Subscription.RefreshIntervalMinutes = sourceKind switch
                {
                    PodcastSourceKind.Rss => settings.RssRefreshIntervalMinutes,
                    PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist =>
                        settings.YouTubeRefreshIntervalMinutes,
                    _ => 0
                };
            }
            Save(settings);
            return new PodcastSourceAddResult(
                update.Subscription.Id,
                update.Subscription.Title,
                update.Subscription.SourceKind,
                update.AddedSubscription,
                update.RestoredSubscription,
                update.AddedEpisodes,
                update.UpdatedEpisodes,
                feed.Episodes.Count);
        }
    }

    public PodcastInternetMediaAddResult AddInternetMedia(
        PodcastInternetMediaSource media,
        string? titleOverride)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (!Uri.TryCreate(media.PageUrl.Trim(), UriKind.Absolute, out var page)
            || page.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(page.UserInfo))
        {
            throw new ArgumentException(
                "Adres medium musi używać protokołu HTTP albo HTTPS bez danych logowania.",
                nameof(media));
        }

        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var collection = settings.Subscriptions.FirstOrDefault(subscription =>
                string.Equals(
                    subscription.Id,
                    PublicInternetMediaCollections.SavedId,
                    StringComparison.Ordinal));
            if (collection is null)
            {
                collection = new PodcastSubscriptionSettings
                {
                    Id = PublicInternetMediaCollections.SavedId,
                    Title = PublicInternetMediaCollections.SavedTitle,
                    Description = "Publiczne materiały internetowe zapisane w Bibliotece.",
                    FeedUrl = "https://amc.invalid/public-internet-media",
                    SourceKind = PodcastSourceKind.PublicInternetMedia,
                    RefreshIntervalMinutes = 0,
                    IsInLibrary = true
                };
                settings.Subscriptions.Add(collection);
            }
            collection.SourceKind = PodcastSourceKind.PublicInternetMedia;
            collection.IsInLibrary = true;

            // Identyfikator musi być bitowo zgodny z pełnym AMC: ono haszuje
            // przycięty adres podany resolverowi, nie ponownie serializowany Uri.
            var stableAddress = media.PageUrl.Trim();
            var episodeId = "internet-media:"
                + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableAddress)))
                    .ToLowerInvariant();
            var episode = settings.Episodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, episodeId, StringComparison.Ordinal))
                ?? settings.Episodes.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.MediaUrl,
                        stableAddress,
                        StringComparison.OrdinalIgnoreCase));
            var added = episode is null;
            if (episode is null)
            {
                episode = new PodcastEpisodeSettings
                {
                    Id = episodeId,
                    IsNew = false
                };
                settings.Episodes.Add(episode);
            }
            episode.SubscriptionId = collection.Id;
            episode.SourceIdentifier = stableAddress;
            episode.Title = string.IsNullOrWhiteSpace(titleOverride)
                ? media.Title.Trim()
                : titleOverride.Trim();
            if (episode.Title.Length == 0)
                episode.Title = media.IsLive ? "YouTube na żywo" : "Materiał YouTube";
            episode.Author = media.Channel.Trim();
            episode.Description = media.IsLive
                ? "Publiczna transmisja YouTube. Adres audio jest sprawdzany ponownie przy każdym odtwarzaniu."
                : "Publiczny materiał YouTube. Adres audio jest sprawdzany ponownie przy każdym odtwarzaniu.";
            episode.MediaUrl = stableAddress;
            episode.PageUrl = stableAddress;
            episode.MediaType = "video/youtube";
            episode.DurationTicks = Math.Max(0, media.Duration.Ticks);
            Save(settings);
            return new PodcastInternetMediaAddResult(
                collection.Id,
                episode.Id,
                episode.Title,
                added);
        }
    }

    public PodcastRefreshResult ApplyRefresh(
        string subscriptionId,
        PodcastFeedDocument feed,
        DateTime refreshUtc,
        BookmarkSettings? bookmarks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(feed);
        lock (_gate)
        {
            var settings = new PodcastSettings();
            LoadInto(settings);
            var selected = settings.Subscriptions.FirstOrDefault(subscription =>
                subscription.IsInLibrary
                && string.Equals(subscription.Id, subscriptionId, StringComparison.Ordinal));
            if (selected is null)
                throw new KeyNotFoundException("Tego podcastu nie ma już w Bibliotece.");
            if (!IsRefreshable(selected.SourceKind))
            {
                throw new InvalidOperationException(
                    "Publiczne medium internetowe jest sprawdzane ponownie przy każdym odtwarzaniu.");
            }

            var update = PodcastLibraryUpdater.Apply(
                settings,
                feed,
                selected.HasCustomTitle ? selected.Title : null,
                refreshUtc,
                bookmarks,
                selected.SourceKind,
                addToLibrary: false);
            Save(settings);
            return new PodcastRefreshResult(
                update.Subscription.Title,
                update.AddedEpisodes,
                update.UpdatedEpisodes,
                update.RetainedEpisodesAbsentFromFeed,
                GetInboxCount());
        }
    }

    private static bool IsRefreshable(PodcastSourceKind sourceKind) => sourceKind is
        PodcastSourceKind.Rss
        or PodcastSourceKind.YouTubeChannel
        or PodcastSourceKind.YouTubePlaylist;

    private static bool TryPublicHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed)
            && parsed.Scheme is "http" or "https"
            && string.IsNullOrEmpty(parsed.UserInfo))
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }

    private static PodcastRefreshTarget ToRefreshTarget(PodcastSubscriptionSettings subscription) =>
        new(
            subscription.Id,
            subscription.Title,
            subscription.FeedUrl,
            subscription.SourceKind);

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

        // Baza Michala powstala przed tymi ustawieniami, wiec CREATE TABLE ich nie
        // doda - trzeba dolozyc kolumny osobno. ALTER TABLE ADD COLUMN na istniejacej
        // kolumnie rzuca blad, dlatego najpierw pytamy o uklad tabeli.
        EnsurePodcastStateColumn(connection, "rss_refresh_interval_minutes", "INTEGER NOT NULL DEFAULT 60");
        EnsurePodcastStateColumn(connection, "youtube_refresh_interval_minutes", "INTEGER NOT NULL DEFAULT 60");
        EnsurePodcastStateColumn(connection, "automatic_refresh_batch_size", "INTEGER NOT NULL DEFAULT 4");

        using var metadata = connection.CreateCommand();
        metadata.CommandText = """
            INSERT INTO metadata(key, value) VALUES('database_schema_version', $version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        metadata.Parameters.AddWithValue("$version", DatabaseSchemaVersion.ToString(CultureInfo.InvariantCulture));
        metadata.ExecuteNonQuery();
    }

    private static void EnsurePodcastStateColumn(
        SqliteConnection connection,
        string columnName,
        string definition)
    {
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT 1 FROM pragma_table_info('podcast_state') WHERE name = $name;";
            probe.Parameters.AddWithValue("$name", columnName);
            if (probe.ExecuteScalar() is not null) return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE podcast_state ADD COLUMN {columnName} {definition};";
        alter.ExecuteNonQuery();
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
