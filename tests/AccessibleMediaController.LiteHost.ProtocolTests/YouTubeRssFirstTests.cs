using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.LiteHost;
using AccessibleMediaController.Windows.Services;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class YouTubeRssFirstTests
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "amc-youtube-rss-first-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "podcasts.db");
            const string channel = "youtube-channel:UC1234567890";
            var now = DateTime.UtcNow;
            var feed = new PodcastFeedDocument(
                channel, "Kanał testowy", "Autor", "Opis",
                new Uri("https://www.youtube.com/channel/UC1234567890/videos"),
                null,
                [new PodcastFeedEpisode(
                    channel + ":videoOld001", "videoOld001", "Znany film", "Autor", "Opis",
                    new DateTimeOffset(now.AddHours(-3)), TimeSpan.FromMinutes(3),
                    new Uri("https://www.youtube.com/watch?v=videoOld001"),
                    null, "video/youtube", null)]);
            var mutation = new PodcastLibraryMutationStore(database);
            mutation.AddSource(feed, null, PodcastSourceKind.YouTubeChannel, now.AddHours(-2));

            using var store = LitePodcastProgressStore.Open(database, () => false);
            var snapshot = store.GetYouTubeFeedSnapshot(channel)
                ?? throw new Exception("Brak zapisanego kanału dla próby RSS.");
            Assert.True(snapshot.KnownVideoIds.Count == 1 && snapshot.KnownVideoIds.Contains("videoOld001"),
                "wąski odczyt bazy musi zwrócić identyfikator filmu z zapisanego kanału");
            Assert.True(store.GetYouTubeFeedSnapshot("missing") is null,
                "brak kanału nie może uprawniać do pominięcia yt-dlp");

            var known = new Dictionary<string, YouTubeFeedEntry>(StringComparer.Ordinal)
            {
                ["videoOld001"] = new(null, "Znany film")
            };
            Assert.True(LitePodcastRefreshCoordinator.CanSkipYouTubeExtraction(snapshot, known, now),
                "aktualny RSS bez nowych ID pozwala pominąć yt-dlp");
            var unseen = new Dictionary<string, YouTubeFeedEntry>(known, StringComparer.Ordinal)
            {
                ["videoNew001"] = new(null, "Nowy film")
            };
            Assert.True(!LitePodcastRefreshCoordinator.CanSkipYouTubeExtraction(snapshot, unseen, now)
                && !LitePodcastRefreshCoordinator.CanSkipYouTubeExtraction(snapshot, new Dictionary<string, YouTubeFeedEntry>(), now)
                && !LitePodcastRefreshCoordinator.CanSkipYouTubeExtraction(snapshot, known, now.AddHours(25)),
                "nowy ID, brak RSS i pełny odczyt starszy niż dobę muszą uruchomić yt-dlp");

            var before = ReadPayload(database, "podcast_subscriptions", channel);
            var episodeBefore = ReadPayload(database, "podcast_episodes", channel + ":videoOld001");
            var fetchCalls = 0;
            var probeCalls = 0;
            using var coordinator = new LitePodcastRefreshCoordinator(
                store, null,
                (_, _) =>
                {
                    fetchCalls++;
                    return Task.FromResult(feed);
                },
                rssProbe: (_, _) =>
                {
                    probeCalls++;
                    return Task.FromResult(true);
                });
            var automatic = JsonSerializer.Serialize(coordinator.RefreshAutomatic());
            Assert.True(automatic.Contains("\"requested\":1", StringComparison.Ordinal)
                && automatic.Contains("\"succeeded\":1", StringComparison.Ordinal)
                && probeCalls == 1 && fetchCalls == 0,
                "automat sprawdza RSS, ale nie uruchamia pełnego pobrania przy znanych ID");
            Assert.True(ReadPayload(database, "podcast_subscriptions", channel) == before
                && ReadPayload(database, "podcast_episodes", channel + ":videoOld001") == episodeBefore,
                "brak nowości w RSS nie może przepisać metadanych ani historii odtwarzania");

            coordinator.Refresh(JsonSerializer.Deserialize<JsonElement>(
                "{\"subscriptionId\":\"youtube-channel:UC1234567890\"}"));
            Assert.True(fetchCalls == 1 && probeCalls == 1,
                "ręczne F5 musi zachować pełny odczyt, bez skrótu RSS");
        }
        finally
        {
            var fullRoot = Path.GetFullPath(root);
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (!fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullRoot).StartsWith("amc-youtube-rss-first-", StringComparison.Ordinal))
                throw new InvalidOperationException("Niebezpieczna ścieżka czyszczenia testu RSS.");
            Directory.Delete(fullRoot, recursive: true);
        }
    }

    private static string ReadPayload(string path, string table, string id)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT payload_json FROM {table} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return (string?)command.ExecuteScalar() ?? throw new Exception("Brak danych próby RSS.");
    }
}
