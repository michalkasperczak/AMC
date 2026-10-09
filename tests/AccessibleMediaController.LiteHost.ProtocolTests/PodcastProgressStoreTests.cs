using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost.Protocol;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class PodcastProgressStoreTests
{
    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "amc-podcast-progress-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "podcasts.db");
            CreateDatabase(database);
            using (var store = LitePodcastProgressStore.Open(database, () => false))
            {
                var secondWriterDenied = false;
                try
                {
                    using var _ = LitePodcastProgressStore.Open(database, () => false);
                }
                catch (LitePodcastProgressStoreDenied)
                {
                    secondWriterDenied = true;
                }
                Assert.True(secondWriterDenied, "drugi host nie moze zostac drugim pisarzem postepu");

                store.Save("ep-1", TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10), false);
                var early = ReadEpisode(database);
                Assert.True(early.ResumePositionTicks == TimeSpan.FromSeconds(30).Ticks,
                    "pierwszy checkpoint zachowuje pozycje");
                Assert.True(early.IsNew && !early.IsStarted && !early.IsPlayed,
                    "przed minuta odcinek nadal jest nowy");

                store.Save("ep-1", TimeSpan.FromSeconds(75), TimeSpan.FromMinutes(10), false);
                var started = ReadEpisode(database);
                Assert.True(started.ResumePositionTicks == TimeSpan.FromSeconds(75).Ticks,
                    "kolejny checkpoint przesuwa pozycje");
                Assert.True(!started.IsNew && started.IsStarted && !started.IsPlayed,
                    "po minucie stan ma brzmiec w trakcie");

                store.Save("ep-1", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), true);
                var completed = ReadEpisode(database);
                Assert.True(completed.ResumePositionTicks == 0L,
                    "naturalny koniec zeruje punkt wznowienia");
                Assert.True(!completed.IsNew && completed.IsStarted && completed.IsPlayed,
                    "naturalny koniec oznacza odcinek jako odtworzony");

                var targets = store.GetRefreshTargets(null);
                Assert.True(targets.Count == 1 && targets[0].SubscriptionId == "sub-1",
                    "odswiezenie calej biblioteki wybiera tylko zapisane zrodla");
                var feed = new PodcastFeedDocument(
                    "sub-1",
                    "Podcast po odswiezeniu",
                    "Autor",
                    "Opis",
                    new Uri("https://example.invalid/feed.xml"),
                    null,
                    [new PodcastFeedEpisode(
                        "ep-2",
                        "source-2",
                        "Nowy odcinek",
                        "Autor",
                        "Opis",
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMinutes(12),
                        new Uri("https://example.invalid/episode-2.mp3"),
                        null,
                        "audio/mpeg",
                        null)]);
                var refreshed = store.ApplyRefresh("sub-1", feed);
                Assert.True(refreshed.AddedEpisodes == 1,
                    "odswiezenie dopisuje nowy odcinek");
                Assert.True(refreshed.RetainedEpisodesAbsentFromFeed == 1,
                    "odswiezenie zachowuje archiwalny odcinek nieobecny w RSS");
                Assert.True(refreshed.InboxCount == 1 && store.GetInboxCount() == 1,
                    "licznik skrzynki obejmuje nowy nieodtworzony odcinek");
                Assert.True(ReadEpisode(database, "ep-2").Title == "Nowy odcinek",
                    "nowy odcinek zostaje zapisany w tej samej bazie");

                var downloadTargets = store.GetDownloadTargets(["ep-1", "ep-1", "missing"]);
                Assert.True(downloadTargets.Count == 1,
                    "pobieranie zwraca tylko istniejace odcinki bez duplikatow");
                var target = downloadTargets[0];
                Assert.Equal("Odcinek", target.Title,
                    "cel pobrania ma wylacznie nazwe przeznaczona dla uzytkownika");
                Assert.Equal(
                    Path.Combine(root, "pobrane-podcastu"),
                    target.ConfiguredDownloadsFolder,
                    "folder zrodla ma pierwszenstwo przed folderem globalnym");

                var downloadedPath = Path.Combine(root, "pobrane-podcastu", "Odcinek.mp3");
                Directory.CreateDirectory(Path.GetDirectoryName(downloadedPath)!);
                File.WriteAllBytes(downloadedPath, [1, 2, 3]);
                var savedDownload = store.SaveDownloadPath("ep-1", downloadedPath);
                Assert.True(savedDownload.Changed,
                    "pierwszy zapis sciezki pobranego odcinka zmienia rekord");
                Assert.Equal(
                    Path.GetFullPath(downloadedPath),
                    ReadEpisode(database).DownloadPath,
                    "waska mutacja zapisuje sciezke takze w payloadzie odcinka");
                Assert.True(!store.SaveDownloadPath("ep-1", downloadedPath).Changed,
                    "powtorzenie tej samej sciezki jest idempotentne");

                var addedFeed = new PodcastFeedDocument(
                    "sub-new",
                    "Nowy podcast ze źródła",
                    "Nowy autor",
                    "Nowy opis",
                    new Uri("https://example.invalid/new-feed.xml"),
                    null,
                    [new PodcastFeedEpisode(
                        "ep-new",
                        "source-new",
                        "Pierwszy nowy odcinek",
                        "Nowy autor",
                        "Opis",
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMinutes(8),
                        new Uri("https://example.invalid/new-episode.mp3"),
                        null,
                        "audio/mpeg",
                        null)]);
                var addedSource = store.AddSource(
                    addedFeed,
                    "Moja nazwa podcastu",
                    PodcastSourceKind.Rss);
                Assert.True(addedSource.AddedSubscription && !addedSource.RestoredSubscription,
                    "jawne dodanie tworzy nowe zrodlo w Bibliotece");
                Assert.Equal("Moja nazwa podcastu", addedSource.Title,
                    "wlasna nazwa jest tekstem uzytkownika, nie identyfikatorem");
                var savedSource = ReadSubscription(database, "sub-new");
                Assert.True(savedSource.IsInLibrary && savedSource.RefreshIntervalMinutes == 60,
                    "nowy RSS dostaje czlonkostwo i domyslny odstep odswiezania");

                var youTubeFeed = new PodcastFeedDocument(
                    "youtube-channel:UCabc_DEF-123",
                    "Kanał testowy",
                    "Autor YouTube",
                    "Opis",
                    new Uri("https://www.youtube.com/feeds/videos.xml?channel_id=UCabc_DEF-123"),
                    new Uri("https://www.youtube.com/channel/UCabc_DEF-123"),
                    [new PodcastFeedEpisode(
                        "youtube-ep-1",
                        "youtube-source-1",
                        "Materiał kanału",
                        "Autor YouTube",
                        "Opis",
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMinutes(5),
                        new Uri("https://example.invalid/youtube-audio.m4a"),
                        null,
                        "audio/mp4",
                        null)]);
                store.AddSource(
                    youTubeFeed,
                    null,
                    PodcastSourceKind.YouTubeChannel);

                var internet = store.AddInternetMedia(
                    new PodcastInternetMediaSource(
                        "https://www.youtube.com/watch?v=test123",
                        "Publiczny materiał",
                        "Kanał testowy",
                        TimeSpan.FromMinutes(3),
                        false),
                    null);
                Assert.True(internet.AddedEpisode,
                    "jawne dodanie publicznego medium tworzy odcinek");
                var savedInternet = ReadEpisode(database, internet.EpisodeId);
                Assert.Equal(
                    PublicInternetMediaCollections.SavedId,
                    savedInternet.SubscriptionId,
                    "publiczne medium trafia do zapisanej kolekcji, nie do podgladow");
                Assert.Equal(
                    "https://www.youtube.com/watch?v=test123",
                    savedInternet.MediaUrl,
                    "w bazie zostaje stabilny adres strony, nie podpisany strumien");
                Assert.True(!store.AddInternetMedia(
                    new PodcastInternetMediaSource(
                        "https://www.youtube.com/watch?v=test123",
                        "Publiczny materiał",
                        "Kanał testowy",
                        TimeSpan.FromMinutes(3),
                        false),
                    null).AddedEpisode,
                    "powtorne dodanie tego samego medium aktualizuje zamiast dublowac");

                var opmlEntries = store.GetOpmlEntries();
                Assert.True(opmlEntries.Count == 2,
                    "eksport OPML obejmuje tylko zapisane podcasty RSS");
                Assert.True(opmlEntries.Any(entry =>
                        entry.Title == "Moja nazwa podcastu"
                        && entry.FeedUri.AbsoluteUri == "https://example.invalid/new-feed.xml"),
                    "eksport OPML zachowuje nazwe uzytkownika i adres RSS");
                Assert.True(opmlEntries.All(entry =>
                        !entry.FeedUri.AbsoluteUri.Contains("youtube", StringComparison.OrdinalIgnoreCase)),
                    "publiczne media internetowe nie moga udawac RSS w eksporcie");
                var youTubeCollections = store.GetYouTubeCollectionsForExport();
                Assert.True(youTubeCollections.Count == 1,
                    "eksport YouTube obejmuje tylko zapisane kanaly i playlisty");
                Assert.True(youTubeCollections[0].IsChannel
                    && youTubeCollections[0].SourceIdentifier == "UCabc_DEF-123"
                    && youTubeCollections[0].Title == "Kanał testowy",
                    "eksport YouTube oddziela identyfikator modelu od etykiety uzytkownika");
            }

            using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM untouched WHERE id = 1;";
                Assert.Equal("zostaje", Convert.ToString(command.ExecuteScalar()),
                    "waski zapis nie moze dotknac obcych danych");
            }

            using var blocked = LitePodcastProgressStore.Open(database, () => true);
            var refused = false;
            try
            {
                blocked.GetRefreshTargets(null);
            }
            catch (LiteRequestException)
            {
                refused = true;
            }
            Assert.True(refused, "dzialajace glowne AMC musi zablokowac odswiezenie");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PodcastEpisodeSettings ReadEpisode(string path, string episodeId = "ep-1")
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM podcast_episodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", episodeId);
        return JsonSerializer.Deserialize<PodcastEpisodeSettings>(
            Convert.ToString(command.ExecuteScalar())!)!;
    }

    private static PodcastSubscriptionSettings ReadSubscription(string path, string subscriptionId)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM podcast_subscriptions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", subscriptionId);
        return JsonSerializer.Deserialize<PodcastSubscriptionSettings>(
            Convert.ToString(command.ExecuteScalar())!)!;
    }

    private static void CreateDatabase(string path)
    {
        var subscription = new PodcastSubscriptionSettings
        {
            Id = "sub-1",
            Title = "Podcast",
            FeedUrl = "https://example.invalid/feed.xml",
            IsInLibrary = true,
            SourceKind = PodcastSourceKind.Rss,
            DownloadsFolder = Path.Combine(
                Path.GetDirectoryName(path)!, "pobrane-podcastu")
        };
        var episode = new PodcastEpisodeSettings
        {
            Id = "ep-1",
            SubscriptionId = "sub-1",
            Title = "Odcinek",
            MediaUrl = "https://example.invalid/episode.mp3",
            IsNew = true
        };
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE podcast_state (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                downloads_folder TEXT NULL,
                current_item_id TEXT NULL,
                volume INTEGER NOT NULL,
                playback_rate REAL NOT NULL,
                rss_refresh_interval_minutes INTEGER NOT NULL DEFAULT 60,
                youtube_refresh_interval_minutes INTEGER NOT NULL DEFAULT 60,
                automatic_refresh_batch_size INTEGER NOT NULL DEFAULT 4
            );
            INSERT INTO podcast_state(singleton, downloads_folder, volume, playback_rate)
            VALUES(1, $downloads, 35, 1.0);
            CREATE TABLE podcast_subscriptions (
                id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL, title TEXT NOT NULL,
                feed_url TEXT NOT NULL, is_in_library INTEGER NOT NULL,
                last_refresh_utc_ticks INTEGER NOT NULL, content_hash TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE podcast_episodes (
                id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL,
                subscription_id TEXT NOT NULL, title TEXT NOT NULL,
                published_utc_ticks INTEGER NOT NULL, is_new INTEGER NOT NULL,
                is_started INTEGER NOT NULL, is_played INTEGER NOT NULL,
                is_favorite INTEGER NOT NULL, is_in_queue INTEGER NOT NULL,
                is_play_next INTEGER NOT NULL, download_path TEXT NULL,
                content_hash TEXT NOT NULL, payload_json TEXT NOT NULL
            );
            CREATE TABLE untouched (id INTEGER PRIMARY KEY, value TEXT NOT NULL);
            INSERT INTO untouched(id, value) VALUES(1, 'zostaje');
            """;
        command.Parameters.AddWithValue(
            "$downloads", Path.Combine(Path.GetDirectoryName(path)!, "pobrane-globalne"));
        command.ExecuteNonQuery();
        using var insertSubscription = connection.CreateCommand();
        insertSubscription.CommandText = """
            INSERT INTO podcast_subscriptions(
                id, ordinal, title, feed_url, is_in_library,
                last_refresh_utc_ticks, content_hash, payload_json)
            VALUES('sub-1', 0, 'Podcast', 'https://example.invalid/feed.xml',
                   1, 0, 'seed', $payload);
            """;
        insertSubscription.Parameters.AddWithValue(
            "$payload", JsonSerializer.Serialize(subscription));
        insertSubscription.ExecuteNonQuery();
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO podcast_episodes(
                id, ordinal, subscription_id, title, published_utc_ticks,
                is_new, is_started, is_played, is_favorite, is_in_queue,
                is_play_next, download_path, content_hash, payload_json)
            VALUES('ep-1', 0, 'sub-1', 'Odcinek', 0, 1, 0, 0, 0, 0, 0, NULL, 'seed', $payload);
            """;
        insert.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(episode));
        insert.ExecuteNonQuery();
    }
}
