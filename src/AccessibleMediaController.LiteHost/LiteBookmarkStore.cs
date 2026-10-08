using System.Diagnostics;
using AccessibleMediaController.LiteHost.Protocol;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Waski zapis szybkiej zakladki do tej samej tabeli, ktora czyta pelne AMC.
///
/// Nie zapisujemy calego <c>PersistedState</c>: host Lite nie ma kompletnego
/// stanu i taki zapis skasowalby cudze dane. Jedna transakcja dotyka tylko
/// jednego wiersza tabeli <c>bookmarks</c>.
/// </summary>
internal sealed class LiteBookmarkStore
{
    private const long TicksPerSecond = TimeSpan.TicksPerSecond;
    private readonly object _gate = new();
    private readonly string _databasePath;
    private readonly Func<bool> _fullAmcIsRunning;

    public LiteBookmarkStore(string databasePath, Func<bool>? fullAmcIsRunning = null)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _fullAmcIsRunning = fullAmcIsRunning ?? FullAmcIsRunning;
    }

    public object Add(
        string itemId,
        string itemTitle,
        TimeSpan position,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new LiteRequestException("Bieżący plik nie ma identyfikatora Biblioteki.");
        if (!File.Exists(_databasePath))
            throw new LiteRequestException("Nie znajduję bazy Biblioteki AMC.");

        // Zainstalowany starszy WPF nie zna jeszcze blokady hosta Lite i moze
        // zapisac cala tabele ze swojej pamieci. Wtedy nasz poprawny INSERT
        // zostalby pozniej utracony. Odmawiamy, dopoki ten proces dziala.
        if (_fullAmcIsRunning())
        {
            throw new LiteRequestException(
                "Zamknij najpierw główne AMC. Równoczesny zapis zakładki z dwóch wersji mógłby utracić dane.");
        }

        var roundedTicks = TimeSpan.FromSeconds(
            Math.Round(Math.Max(0d, position.TotalSeconds))).Ticks;
        var createdTicks = utcNow.ToUniversalTime().Ticks;
        lock (_gate)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared,
                // Jedno nacisniecie B = jedna krotka transakcja. Pula nie
                // moze trzymac uchwytu do bazy po operacji ani utrudniac
                // pozniejszego uruchomienia glownego AMC.
                Pooling = false
            }.ToString());
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var existing = connection.CreateCommand();
            existing.Transaction = transaction;
            existing.CommandText = """
                SELECT id, position_ticks
                FROM bookmarks
                WHERE session_id = 'local' COLLATE NOCASE
                  AND item_id = $item_id
                  AND (purpose & 1) != 0
                  AND ABS(position_ticks - $position_ticks) <= $tolerance
                ORDER BY ABS(position_ticks - $position_ticks), ordinal
                LIMIT 1;
                """;
            existing.Parameters.AddWithValue("$item_id", itemId);
            existing.Parameters.AddWithValue("$position_ticks", roundedTicks);
            existing.Parameters.AddWithValue("$tolerance", TicksPerSecond);
            string? existingId = null;
            long existingTicks = 0;
            using (var reader = existing.ExecuteReader())
            {
                if (reader.Read())
                {
                    existingId = reader.GetString(0);
                    existingTicks = reader.GetInt64(1);
                }
            }
            if (existingId is not null)
            {
                transaction.Commit();
                return new
                {
                    added = false,
                    bookmarkId = existingId,
                    positionSeconds = TimeSpan.FromTicks(existingTicks).TotalSeconds
                };
            }

            long ordinal;
            using (var order = connection.CreateCommand())
            {
                order.Transaction = transaction;
                order.CommandText = "SELECT COALESCE(MAX(ordinal), -1) + 1 FROM bookmarks;";
                ordinal = Convert.ToInt64(order.ExecuteScalar());
            }

            var bookmarkId = Guid.NewGuid().ToString("N");
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO bookmarks(
                    id, ordinal, session_id, session_name, item_id, item_title,
                    name, position_ticks, created_utc_ticks, purpose,
                    chapter_origin, chapter_source_id)
                VALUES(
                    $id, $ordinal, 'local', 'Pliki lokalne', $item_id, $item_title,
                    '', $position_ticks, $created_ticks, 1, 0, NULL);
                """;
            insert.Parameters.AddWithValue("$id", bookmarkId);
            insert.Parameters.AddWithValue("$ordinal", ordinal);
            insert.Parameters.AddWithValue("$item_id", itemId);
            insert.Parameters.AddWithValue("$item_title", itemTitle);
            insert.Parameters.AddWithValue("$position_ticks", roundedTicks);
            insert.Parameters.AddWithValue("$created_ticks", createdTicks);
            insert.ExecuteNonQuery();
            transaction.Commit();
            return new
            {
                added = true,
                bookmarkId,
                positionSeconds = TimeSpan.FromTicks(roundedTicks).TotalSeconds
            };
        }
    }

    private static bool FullAmcIsRunning()
    {
        foreach (var process in Process.GetProcessesByName("AccessibleMediaController"))
        {
            using (process)
            {
                if (process.Id != Environment.ProcessId && !process.HasExited) return true;
            }
        }
        return false;
    }
}
