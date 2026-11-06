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
            var freshDatabase = Path.Combine(root, "fresh", "podcasts.db");
            using (var freshStore = LitePodcastProgressStore.Open(freshDatabase, () => false))
            {
                var defaults = freshStore.GetGlobalSettings();
                Assert.True(File.Exists(freshDatabase),
                    "host powinien utworzyc baze Podcastow na swiezym profilu");
                Assert.True(defaults.DownloadsFolder is null
                    && defaults.RssRefreshIntervalMinutes == 60
                    && defaults.YouTubeRefreshIntervalMinutes == 60
                    && defaults.AutomaticRefreshBatchSize == 4,
                    "swieza baza powinna miec jawne ustawienia domyslne");
            }

            var database = Path.Combine(root, "podcasts.db");
            CreateDatabase(database);
            TestInboxSourceChoice(root);
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

                var madeFavorite = store.ToggleFavorites(["sub-1"], ["ep-1"]);
                Assert.True(madeFavorite.Favorite && madeFavorite.RequestedCount == 2
                    && madeFavorite.ChangedCount == 2,
                    "pierwsze polecenie ustawia caly mieszany wybor jako ulubiony");
                Assert.True(ReadSubscription(database, "sub-1").IsFavorite
                    && ReadEpisode(database).IsFavorite
                    && ReadEpisodeFavoriteColumn(database, "ep-1"),
                    "stan ulubionego jest spojny w payloadzie i kolumnie odcinka");

                var clearedEpisode = store.ToggleFavorites([], ["ep-1"]);
                Assert.True(!clearedEpisode.Favorite && clearedEpisode.ChangedCount == 1,
                    "wybor zlozony z samych ulubionych jest czyszczony");
                var normalizedMixed = store.ToggleFavorites(["sub-1"], ["ep-1"]);
                Assert.True(normalizedMixed.Favorite && normalizedMixed.ChangedCount == 1,
                    "mieszany stan zaznaczenia ustawia wszystkie elementy jako ulubione");
                var clearedAll = store.ToggleFavorites(["sub-1"], ["ep-1"]);
                Assert.True(!clearedAll.Favorite && clearedAll.ChangedCount == 2,
                    "ponowienie na calym ulubionym zaznaczeniu usuwa stan wszystkim");
                Assert.True(!ReadSubscription(database, "sub-1").IsFavorite
                    && !ReadEpisode(database).IsFavorite
                    && !ReadEpisodeFavoriteColumn(database, "ep-1"),
                    "czyszczenie pozostawia oba reprezentowane stany spojne");

                var initialPodcastOptions = store.GetPlaybackOptions(
                    PodcastPlaybackOptionsTarget.Podcast, "sub-1");
                Assert.Equal("Podcast", initialPodcastOptions.Title,
                    "odczyt opcji zwraca wylacznie celowy tytul uzytkownika");
                var podcastOptions = store.SetPlaybackOptions(
                    PodcastPlaybackOptionsTarget.Podcast,
                    "sub-1",
                    new PodcastPlaybackOptionsChange(
                        ResumePositionMode.StartFromBeginning,
                        1.25d,
                        true,
                        false,
                        1000,
                        PlaybackTempoAlgorithm.Speech,
                        30,
                        Path.Combine(root, "pobrane-podcastu")));
                Assert.True(
                    podcastOptions.ResumePositionMode == ResumePositionMode.StartFromBeginning
                    && podcastOptions.PlaybackRateOverride == 1.25d
                    && podcastOptions.TempoAlgorithmOverride == PlaybackTempoAlgorithm.Speech
                    && podcastOptions.RefreshIntervalMinutes == 30
                    && !podcastOptions.ShouldRememberPosition
                    && podcastOptions.ResolvedResumePositionMode
                        == ResumePositionMode.StartFromBeginning,
                    "opcje calego podcastu wracaja po zapisie bez utraty wartosci");
                Assert.True(
                    ReadSubscription(database, "sub-1").TempoAlgorithmOverride
                        == PlaybackTempoAlgorithm.Speech,
                    "zmiana samego algorytmu tempa jest czescia odcisku i trafia do bazy");

                var inheritedEpisodeOptions = store.GetPlaybackOptions(
                    PodcastPlaybackOptionsTarget.Episode, "ep-1");
                Assert.True(
                    inheritedEpisodeOptions.PlaybackRateOverride is null
                    && inheritedEpisodeOptions.ResolvedPlaybackRateOverride == 1.25d
                    && inheritedEpisodeOptions.ResolvedTempoAlgorithmOverride
                        == PlaybackTempoAlgorithm.Speech
                    && !inheritedEpisodeOptions.ShouldRememberPosition
                    && inheritedEpisodeOptions.ResolvedResumePositionMode
                        == ResumePositionMode.StartFromBeginning,
                    "odcinek dziedziczy rozstrzygniete opcje podcastu bez udawania wlasnych");
                var episodeOptions = store.SetPlaybackOptions(
                    PodcastPlaybackOptionsTarget.Episode,
                    "ep-1",
                    new PodcastPlaybackOptionsChange(
                        ResumePositionMode.Remember,
                        0.75d,
                        null,
                        true,
                        0,
                        PlaybackTempoAlgorithm.Music,
                        null,
                        null));
                Assert.True(
                    episodeOptions.ShouldRememberPosition
                    && episodeOptions.ResolvedResumePositionMode == ResumePositionMode.Remember
                    && episodeOptions.ResolvedPlaybackRateOverride == 0.75d
                    && episodeOptions.ResolvedTempoAlgorithmOverride
                        == PlaybackTempoAlgorithm.Music,
                    "wlasne opcje odcinka maja pierwszenstwo przed podcastem");

                var targets = store.GetRefreshTargets(null);
                Assert.True(targets.Count == 1 && targets[0].SubscriptionId == "sub-1",
                    "odswiezenie calej biblioteki wybiera tylko zapisane zrodla");
                var configuredPodcastFolder = Path.Combine(root, "pobrane-globalne");
                var configured = store.ConfigureGlobalSettings(
                    configuredPodcastFolder,
                    rssRefreshIntervalMinutes: 45,
                    youTubeRefreshIntervalMinutes: 180,
                    automaticRefreshBatchSize: 2);
                Assert.True(
                    configured.DownloadsFolder == Path.GetFullPath(configuredPodcastFolder)
                    && configured.RssRefreshIntervalMinutes == 45
                    && configured.YouTubeRefreshIntervalMinutes == 180
                    && configured.AutomaticRefreshBatchSize == 2,
                    "globalne ustawienia wracaja jako celowe wartosci uzytkownika");
                Assert.True(store.GetGlobalSettings() == configured,
                    "zapis globalnych ustawien przezywa ponowny odczyt bazy");
                var dueTargets = store.GetDueRefreshTargets();
                Assert.True(dueTargets.Count == 1
                    && dueTargets[0].SubscriptionId == "sub-1",
                    "automat wybiera zrodlo bez poprzedniego odswiezenia");
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
                Assert.True(savedSource.IsInLibrary && savedSource.RefreshIntervalMinutes == 45,
                    "nowy RSS dostaje czlonkostwo i aktualny domyslny odstep odswiezania");

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
                Assert.True(
                    ReadSubscription(database, "youtube-channel:UCabc_DEF-123")
                        .RefreshIntervalMinutes == 180,
                    "nowy kanal YouTube dostaje osobny aktualny odstep odswiezania");
                HideEpisode(database, "youtube-ep-1");
                store.ApplyRefresh("youtube-channel:UCabc_DEF-123", youTubeFeed);
                Assert.True(!EpisodeExists(database, "youtube-ep-1"),
                    "usuniety material kanalu YouTube nie wraca po odswiezeniu");

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
                HideEpisode(database, internet.EpisodeId);
                var restoredInternet = store.AddInternetMedia(
                    new PodcastInternetMediaSource(
                        "https://www.youtube.com/watch?v=test123",
                        "Publiczny materiał",
                        "Kanał testowy",
                        TimeSpan.FromMinutes(3),
                        false),
                    null);
                Assert.True(restoredInternet.AddedEpisode
                    && EpisodeExists(database, internet.EpisodeId),
                    "jawne ponowne dodanie usunietego medium przywraca je do Biblioteki");

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
            var configureRefused = false;
            try
            {
                blocked.ConfigureGlobalSettings(null, 60, 60, 4);
            }
            catch (LiteRequestException)
            {
                configureRefused = true;
            }
            Assert.True(configureRefused,
                "dzialajace glowne AMC musi zablokowac zmiane ustawien podcastow");
            TestAutomaticRefreshVisitsEveryDueSource(root);
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

    private static bool ReadEpisodeFavoriteColumn(string path, string episodeId)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_favorite FROM podcast_episodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", episodeId);
        return Convert.ToInt32(command.ExecuteScalar()) != 0;
    }

    private static bool EpisodeExists(string path, string episodeId)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM podcast_episodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", episodeId);
        return Convert.ToInt32(command.ExecuteScalar()) != 0;
    }

    private static void HideEpisode(string path, string episodeId)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS podcast_removed_episodes (
                id TEXT PRIMARY KEY,
                removed_utc_ticks INTEGER NOT NULL
            );
            INSERT INTO podcast_removed_episodes(id, removed_utc_ticks)
            VALUES($id, $ticks)
            ON CONFLICT(id) DO UPDATE SET removed_utc_ticks = excluded.removed_utc_ticks;
            DELETE FROM podcast_episodes WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", episodeId);
        command.Parameters.AddWithValue("$ticks", DateTime.UtcNow.Ticks);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void TestAutomaticRefreshVisitsEveryDueSource(string root)
    {
        var database = Path.Combine(root, "automatic-refresh", "podcasts.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        CreateDatabase(database);
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using (var clear = connection.CreateCommand())
            {
                clear.CommandText = "DELETE FROM podcast_episodes; DELETE FROM podcast_subscriptions;";
                clear.ExecuteNonQuery();
            }
            for (var number = 1; number <= 5; number++)
            {
                var subscription = new PodcastSubscriptionSettings
                {
                    Id = $"fair-{number}",
                    Title = $"Źródło {number}",
                    FeedUrl = $"https://example.invalid/feed-{number}.xml",
                    IsInLibrary = true,
                    SourceKind = PodcastSourceKind.Rss,
                    RefreshIntervalMinutes = 60
                };
                using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO podcast_subscriptions(
                        id, ordinal, title, feed_url, is_in_library,
                        last_refresh_utc_ticks, content_hash, payload_json)
                    VALUES($id, $ordinal, $title, $feed, 1, 0, $hash, $payload);
                    """;
                insert.Parameters.AddWithValue("$id", subscription.Id);
                insert.Parameters.AddWithValue("$ordinal", number - 1);
                insert.Parameters.AddWithValue("$title", subscription.Title);
                insert.Parameters.AddWithValue("$feed", subscription.FeedUrl);
                insert.Parameters.AddWithValue("$hash", $"fair-{number}");
                insert.Parameters.AddWithValue(
                    "$payload", JsonSerializer.Serialize(subscription));
                insert.ExecuteNonQuery();
            }
        }

        using var store = LitePodcastProgressStore.Open(database, () => false);
        store.ConfigureGlobalSettings(null, 60, 60, 2);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var batchSizes = new List<int>();
        for (var pass = 0; pass < 3; pass++)
        {
            var batch = store.GetDueRefreshTargets();
            batchSizes.Add(batch.Count);
            foreach (var target in batch)
            {
                Assert.True(visited.Add(target.SubscriptionId),
                    "automat nie powinien wracac do zrodla przed przejsciem calej kolejki");
                store.MarkRefreshAttempt(target.SubscriptionId);
            }
        }

        Assert.True(batchSizes.SequenceEqual([2, 2, 1]) && visited.Count == 5,
            "automat powinien przejsc przez wszystkie nalezne zrodla kolejnymi partiami");
        Assert.True(store.GetDueRefreshTargets().Count == 0,
            "po pelnym przebiegu zadne swiezo sprawdzone zrodlo nie powinno byc od razu nalezne");
    }

    private static void TestInboxSourceChoice(string root)
    {
        var database = Path.Combine(root, "inbox-choice", "podcasts.db");
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        CreateDatabase(database);
        using var store = LitePodcastProgressStore.Open(database, () => false);
        Assert.True(store.GetInboxCount() == 1, "wybrane źródło jest w skrzynce");
        Assert.True(store.SetInboxSources([], []) == 0 && store.GetInboxCount() == 1,
            "źródło dodane po otwarciu wyboru nie jest zmieniane");
        Assert.True(store.SetInboxSources([], ["sub-1"]) == 0, "można wyłączyć wszystkie źródła");
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT inbox_selection_customized FROM podcast_state WHERE singleton = 1;";
            Assert.True(Convert.ToInt32(command.ExecuteScalar()) == 1,
                "po pierwszym wyborze nowe źródła czekają na zaznaczenie");
        }
        Assert.True(store.GetInboxCount() == 0 && store.GetDueRefreshTargets().Count == 0,
            "odznaczone źródło znika ze skrzynki i kolejki automatycznej");
        Assert.True(store.GetRefreshTargets("sub-1").Count == 1,
            "odznaczone źródło nadal można odświeżyć ręcznie");
        Assert.True(ReadEpisode(database).Id == "ep-1",
            "odznaczenie nie usuwa zapisanych odcinków");
        Assert.True(store.SetInboxSources(["sub-1"], ["sub-1"]) == 1,
            "można ponownie zaznaczyć źródło");
        Assert.True(store.GetInboxCount() == 1
            && ReadSubscription(database, "sub-1").RefreshIntervalMinutes == 60,
            "ponowny wybór przywraca skrzynkę i poprzedni odstęp");
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
            RefreshIntervalMinutes = 60,
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
