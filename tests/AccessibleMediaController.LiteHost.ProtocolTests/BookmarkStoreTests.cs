using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.LiteHost.Protocol;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

/// <summary>Waski zapis B: jedna tabela, duplikat i bramka starego WPF.</summary>
internal static class BookmarkStoreTests
{
    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "amc-bookmark-store-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "library.db");
            CreateDatabase(database);
            var store = new LiteBookmarkStore(database, () => false);

            store.Add("plik-1", "Nagranie", TimeSpan.FromSeconds(12.4), DateTime.UtcNow);
            store.Add("plik-1", "Nagranie", TimeSpan.FromSeconds(12.8), DateTime.UtcNow);
            store.MutateBookmarks(settings =>
            {
                settings.Entries.Add(new BookmarkEntry
                {
                    Id = "chapter-1",
                    SessionId = "podcasts",
                    SessionName = "Podcasty i YouTube",
                    ItemId = "episode-1",
                    ItemTitle = "Odcinek",
                    Name = "Rozdział pierwszy",
                    PositionTicks = TimeSpan.FromMinutes(1).Ticks,
                    CreatedUtcTicks = DateTime.UtcNow.Ticks,
                    Purpose = BookmarkPurpose.Chapter,
                    ChapterOrigin = ChapterOrigin.Provider,
                    ChapterSourceId = "podcast-feed"
                });
                return true;
            });

            using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using var count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM bookmarks;";
                Assert.True(Convert.ToInt64(count.ExecuteScalar()) == 2L,
                    "duplikat pozostaje jeden, a rozdzial jest zapisany osobno");
                using var sentinel = connection.CreateCommand();
                sentinel.CommandText = "SELECT value FROM untouched WHERE id = 1;";
                Assert.Equal("zostaje", Convert.ToString(sentinel.ExecuteScalar()),
                    "waski zapis nie moze dotknac innych tabel");
            }

            var blocked = new LiteBookmarkStore(database, () => true);
            var refused = false;
            try
            {
                blocked.Add("plik-1", "Nagranie", TimeSpan.FromSeconds(30), DateTime.UtcNow);
            }
            catch (LiteRequestException)
            {
                refused = true;
            }
            Assert.True(refused, "dzialajace stare AMC musi zablokowac zapis");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateDatabase(string path)
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
            CREATE TABLE untouched (id INTEGER PRIMARY KEY, value TEXT NOT NULL);
            INSERT INTO untouched(id, value) VALUES (1, 'zostaje');
            """;
        command.ExecuteNonQuery();
    }
}
