using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class PodcastChapterCoordinatorTests
{
    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "amc-podcast-chapters-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var podcastDatabase = Path.Combine(root, "podcasts.db");
            var bookmarkDatabase = Path.Combine(root, "library.db");
            CreateBookmarkDatabase(bookmarkDatabase);

            using var podcasts = LitePodcastProgressStore.Open(
                podcastDatabase,
                () => false);
            var feed = new PodcastFeedDocument(
                "podcast-chapters",
                "Podcast z rozdziałami",
                "Autor",
                "Opis",
                new Uri("https://example.invalid/feed.xml"),
                null,
                [
                    Episode(
                        "ep-json",
                        "Odcinek JSON",
                        "",
                        new Uri("https://example.invalid/ep-json"),
                        new Uri("https://example.invalid/ep-json.chapters.json")),
                    Episode(
                        "ep-description",
                        "Odcinek z opisem",
                        "Rozdziały:\r\r\n(00:00:00) PARTNERZY\r\r\n(00:05:00) Druga część",
                        null,
                        null),
                    Episode(
                        "ep-page",
                        "Odcinek ze stroną",
                        "Opis bez znaczników czasu",
                        new Uri("https://example.invalid/ep-page"),
                        null),
                    Episode(
                        "ep-youtube",
                        "Film z rozdziałami",
                        "",
                        new Uri("https://www.youtube.com/watch?v=fixture0001"),
                        null)
                ]);
            podcasts.AddSource(feed, null, PodcastSourceKind.Rss);

            var bookmarks = new LiteBookmarkStore(bookmarkDatabase, () => false);
            var providerCalls = 0;
            var pageCalls = 0;
            using var coordinator = new LitePodcastChapterCoordinator(
                podcasts,
                bookmarks,
                (address, _) =>
                {
                    providerCalls++;
                    Assert.Equal(
                        "https://example.invalid/ep-json.chapters.json",
                        address.AbsoluteUri,
                        "host pobiera URL Podcasting 2.0 zapisany dla bieżącego odcinka");
                    return Task.FromResult<IReadOnlyList<ProviderChapterPoint>>(
                    [
                        new("intro", "Wstęp", TimeSpan.Zero),
                        new("main", "Temat główny", TimeSpan.FromMinutes(4))
                    ]);
                },
                (address, _, _) =>
                {
                    pageCalls++;
                    if (address.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
                    {
                        // Zwykły parser strony celowo nie czyta skryptów watch YouTube.
                        return Task.FromResult<IReadOnlyList<ProviderChapterPoint>>([]);
                    }
                    Assert.Equal(
                        "https://example.invalid/ep-page",
                        address.AbsoluteUri,
                        "strona zapasowa dotyczy bieżącego odcinka");
                    return Task.FromResult<IReadOnlyList<ProviderChapterPoint>>(
                    [
                        new("page-1", "Otwarcie", TimeSpan.Zero),
                        new("page-2", "Podsumowanie", TimeSpan.FromMinutes(8))
                    ]);
                });

            var json = coordinator.Prepare(Item("ep-json", "Odcinek JSON"), TimeSpan.FromMinutes(20));
            Assert.True(json.ChapterCount == 2 && json.ImportedCount == 2,
                "jawne polecenie importuje rozdziały Podcasting 2.0 przed nawigacją");
            Assert.True(providerCalls == 1,
                "pierwsze użycie rozdziałów pobiera plik dostawcy dokładnie raz");
            Assert.Equal(
                "https://example.invalid/ep-json.chapters.json",
                podcasts.GetChapterTarget("ep-json")?.ProviderChaptersLoadedUrl,
                "udany import zapamiętuje dokładnie pobrany URL");
            coordinator.Prepare(Item("ep-json", "Odcinek JSON"), TimeSpan.FromMinutes(20));
            Assert.True(providerCalls == 1,
                "kolejne naciśnięcie korzysta z trwałego indeksu bez ponownego pobierania");

            var description = coordinator.Prepare(
                Item("ep-description", "Odcinek z opisem"),
                TimeSpan.FromMinutes(20));
            var descriptionChapters = bookmarks.GetChapters(
                "podcasts", "ep-description", TimeSpan.FromMinutes(20));
            Assert.True(description.ChapterCount == 2
                && descriptionChapters[0].Name == "PARTNERZY"
                && descriptionChapters[1].Name == "Druga część",
                "brakujący indeks jest odtwarzany z celowych znaczników czasu w opisie RSS");

            var page = coordinator.Prepare(
                Item("ep-page", "Odcinek ze stroną"),
                TimeSpan.FromMinutes(20));
            Assert.True(page.ChapterCount == 2 && pageCalls == 1,
                "gdy JSON i opis nie wystarczają, host używa strony bieżącego odcinka");

            var youtube = coordinator.Prepare(
                Item("ep-youtube", "Film z rozdziałami"),
                TimeSpan.FromMinutes(20));
            Assert.True(youtube.ChapterCount == 2,
                "film YouTube bez opisu importuje rozdziały twórcy, a nie pustą stronę watch");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PodcastFeedEpisode Episode(
        string id,
        string title,
        string description,
        Uri? page,
        Uri? chapters) => new(
            id,
            id,
            title,
            "Autor",
            description,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(20),
            new Uri($"https://example.invalid/{id}.mp3"),
            page,
            "audio/mpeg",
            null,
            chapters,
            null);

    private static MediaItem Item(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Kind = MediaItemKind.Episode,
        Duration = TimeSpan.FromMinutes(20)
    };

    private static void CreateBookmarkDatabase(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE bookmarks (
                id TEXT PRIMARY KEY,
                ordinal INTEGER NOT NULL,
                session_id TEXT NOT NULL,
                session_name TEXT NOT NULL,
                item_id TEXT NOT NULL,
                item_title TEXT NOT NULL,
                name TEXT NOT NULL,
                position_ticks INTEGER NOT NULL,
                created_utc_ticks INTEGER NOT NULL,
                purpose INTEGER NOT NULL DEFAULT 1,
                chapter_origin INTEGER NOT NULL DEFAULT 0,
                chapter_source_id TEXT NULL
            );
            CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
    }
}
