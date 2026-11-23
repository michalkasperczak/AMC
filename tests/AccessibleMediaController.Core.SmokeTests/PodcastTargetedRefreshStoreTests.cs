using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using Microsoft.Data.Sqlite;

internal static class PodcastTargetedRefreshStoreTests
{
    internal static void Run()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "amc-targeted-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "podcasts.db");
            var current = new PodcastEpisodeSettings
            {
                Id = "target-current",
                SubscriptionId = "target",
                SourceIdentifier = "target-current-source",
                Title = "Przed odświeżeniem",
                Description = "Stary opis",
                MediaUrl = "https://example.invalid/target-current.mp3",
                IsNew = true,
                IsStarted = true,
                IsFavorite = true,
                IsInQueue = true,
                IsPlayNext = true,
                ResumePositionTicks = TimeSpan.FromMinutes(2).Ticks,
                DurationTicks = TimeSpan.FromMinutes(10).Ticks
            };
            var retained = new PodcastEpisodeSettings
            {
                Id = "target-retained",
                SubscriptionId = "target",
                SourceIdentifier = "target-retained-source",
                Title = "Poza bieżącym RSS",
                MediaUrl = "https://example.invalid/target-retained.mp3",
                IsNew = false,
                IsFavorite = true,
                ResumePositionTicks = TimeSpan.FromMinutes(4).Ticks
            };
            var settings = new PodcastSettings
            {
                CurrentItemId = "target-current",
                Volume = 61,
                PlaybackRate = 1.25,
                Subscriptions =
                [
                    new PodcastSubscriptionSettings
                    {
                        Id = "target",
                        Title = "Własna nazwa",
                        HasCustomTitle = true,
                        FeedUrl = "https://example.invalid/target.xml",
                        SourceKind = PodcastSourceKind.Rss,
                        IsInLibrary = true,
                        RefreshIntervalMinutes = 60
                    },
                    new PodcastSubscriptionSettings
                    {
                        Id = "off-target",
                        Title = "Nie wolno deserializować",
                        FeedUrl = "https://example.invalid/off-target.xml",
                        SourceKind = PodcastSourceKind.Rss,
                        IsInLibrary = true,
                        RefreshIntervalMinutes = 60
                    }
                ],
                Episodes =
                [
                    current,
                    retained,
                    new PodcastEpisodeSettings
                    {
                        Id = "off-target-episode",
                        SubscriptionId = "off-target",
                        Title = "Nie wolno deserializować",
                        MediaUrl = "https://example.invalid/off-target.mp3",
                        IsNew = true
                    }
                ]
            };
            var database = new PodcastLibraryDatabase(path);
            database.Initialize(settings);

            var positionStore = new PodcastLibraryMutationStore(path);
            var originalResume = positionStore.GetPlaybackOptions(
                PodcastPlaybackOptionsTarget.Episode, current.Id);
            Assert(originalResume.ResumePosition == TimeSpan.FromMinutes(2),
                "Opcje odcinka nie odczytały zapamiętanej pozycji.");
            positionStore.SavePlaybackCheckpoint(
                current.Id, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(10), completed: false);
            var refreshedResume = positionStore.GetPlaybackOptions(
                PodcastPlaybackOptionsTarget.Episode, current.Id);
            Assert(refreshedResume.ResumePosition == TimeSpan.FromMinutes(3),
                "Ponowny Enter użyłby starej pozycji zamiast ostatniego zapisu.");
            positionStore.SavePlaybackCheckpoint(
                current.Id, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), completed: false);

            const string unreadableSubscription = "{nie jest poprawnym JSON-em subskrypcji";
            const string unreadableEpisode = "{nie jest poprawnym JSON-em odcinka";
            const int offTargetSubscriptionOrdinal = 41;
            const int offTargetEpisodeOrdinal = 73;
            using (var connection = Open(path))
            {
                Execute(connection, """
                    UPDATE podcast_subscriptions
                    SET ordinal = $subscriptionOrdinal, content_hash = 'off-target-sub-hash',
                        payload_json = $subscriptionPayload
                    WHERE id = 'off-target';
                    UPDATE podcast_episodes
                    SET ordinal = $episodeOrdinal, content_hash = 'off-target-episode-hash',
                        payload_json = $episodePayload
                    WHERE id = 'off-target-episode';
                    """,
                    ("$subscriptionOrdinal", offTargetSubscriptionOrdinal),
                    ("$subscriptionPayload", unreadableSubscription),
                    ("$episodeOrdinal", offTargetEpisodeOrdinal),
                    ("$episodePayload", unreadableEpisode));
            }

            var store = new PodcastLibraryMutationStore(path);
            var refresh = store.ApplyRefresh(
                "target",
                new PodcastFeedDocument(
                    "target",
                    "Nazwa z feedu nie może nadpisać własnej",
                    "Autor po odświeżeniu",
                    "Opis po odświeżeniu",
                    new Uri("https://example.invalid/target.xml"),
                    null,
                    [
                        new PodcastFeedEpisode(
                            "target-current",
                            "target-current-source",
                            "Aktualny tytuł",
                            "Autor po odświeżeniu",
                            "Nowy opis",
                            DateTimeOffset.Parse("2026-11-19T12:00:00Z"),
                            TimeSpan.FromMinutes(10),
                            new Uri("https://example.invalid/target-current.mp3"),
                            null,
                            "audio/mpeg",
                            null),
                        new PodcastFeedEpisode(
                            "target-new",
                            "target-new-source",
                            "Nowy odcinek",
                            "Autor po odświeżeniu",
                            "Opis nowego",
                            DateTimeOffset.Parse("2026-11-19T13:00:00Z"),
                            TimeSpan.FromMinutes(8),
                            new Uri("https://example.invalid/target-new.mp3"),
                            null,
                            "audio/mpeg",
                            null)
                    ]),
                new DateTime(2026, 11, 19, 14, 0, 0, DateTimeKind.Utc));

            Assert(refresh.AddedEpisodes == 1 && refresh.UpdatedEpisodes == 1
                   && refresh.RetainedEpisodesAbsentFromFeed == 1,
                "Wąskie odświeżenie nie podało poprawnego wyniku celu.");

            using (var connection = Open(path))
            {
                var savedCurrent = ReadEpisode(connection, "target-current");
                var savedRetained = ReadEpisode(connection, "target-retained");
                var savedSubscription = ReadSubscription(connection, "target");
                Assert(savedCurrent.Title == "Aktualny tytuł"
                       && savedCurrent.Description == "Nowy opis"
                       && savedCurrent.IsNew && savedCurrent.IsStarted && savedCurrent.IsFavorite
                       && savedCurrent.IsInQueue && savedCurrent.IsPlayNext
                       && savedCurrent.ResumePositionTicks == TimeSpan.FromMinutes(2).Ticks,
                    "Odświeżenie celu utraciło flagi kolejki, ulubione lub postęp bieżącego odcinka.");
                Assert(savedRetained.Id == "target-retained"
                       && savedRetained.IsFavorite
                       && savedRetained.ResumePositionTicks == TimeSpan.FromMinutes(4).Ticks,
                    "Odświeżenie celu usunęło albo zmieniło archiwalny odcinek nieobecny w feedzie.");
                Assert(savedSubscription.Title == "Własna nazwa"
                       && savedSubscription.HasCustomTitle
                       && savedSubscription.LastRefreshUtcTicks > 0,
                    "Odświeżenie celu nie zachowało własnego tytułu lub trwałej daty sukcesu.");
                Assert(ScalarInt(connection,
                           "SELECT ordinal FROM podcast_subscriptions WHERE id = 'off-target';")
                       == offTargetSubscriptionOrdinal
                       && ScalarText(connection,
                           "SELECT payload_json FROM podcast_subscriptions WHERE id = 'off-target';")
                       == unreadableSubscription
                       && ScalarText(connection,
                           "SELECT content_hash FROM podcast_subscriptions WHERE id = 'off-target';")
                       == "off-target-sub-hash"
                       && ScalarInt(connection,
                           "SELECT ordinal FROM podcast_episodes WHERE id = 'off-target-episode';")
                       == offTargetEpisodeOrdinal
                       && ScalarText(connection,
                           "SELECT payload_json FROM podcast_episodes WHERE id = 'off-target-episode';")
                       == unreadableEpisode
                       && ScalarText(connection,
                           "SELECT content_hash FROM podcast_episodes WHERE id = 'off-target-episode';")
                       == "off-target-episode-hash",
                    "Wąska transakcja odczytała, zdeserializowała albo zapisała rekord poza wskazanym celem.");
                Assert(ScalarText(connection,
                           "SELECT current_item_id FROM podcast_state WHERE singleton = 1;")
                       == "target-current"
                       && ScalarInt(connection,
                           "SELECT volume FROM podcast_state WHERE singleton = 1;") == 61,
                    "Odświeżenie celu zmieniło globalny stan podcastów poza celem.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        return connection;
    }

    private static PodcastEpisodeSettings ReadEpisode(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM podcast_episodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return JsonSerializer.Deserialize<PodcastEpisodeSettings>(
                   Convert.ToString(command.ExecuteScalar())!)
               ?? throw new InvalidOperationException("Brak zapisanego odcinka celu.");
    }

    private static PodcastSubscriptionSettings ReadSubscription(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM podcast_subscriptions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return JsonSerializer.Deserialize<PodcastSubscriptionSettings>(
                   Convert.ToString(command.ExecuteScalar())!)
               ?? throw new InvalidOperationException("Brak zapisanej subskrypcji celu.");
    }

    private static void Execute(SqliteConnection connection, string sql,
        params (string Name, object Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private static int ScalarInt(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string? ScalarText(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar());
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
