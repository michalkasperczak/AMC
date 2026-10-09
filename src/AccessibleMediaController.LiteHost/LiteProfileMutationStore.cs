using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Wąska brama zmian profilu wykonywanych z interfejsu wxPython.
///
/// Python przekazuje wyłącznie identyfikator i zamierzoną wartość. Host C#
/// sprawdza rekord w prawdziwej bazie, wykonuje operację plikową i zmienia
/// tylko odpowiednie kolumny/sekcje. Dzięki temu frontend nie staje się
/// drugim, konkurencyjnym pisarzem profilu AMC.
/// </summary>
internal sealed class LiteProfileMutationStore
{
    private readonly object _gate = new();
    private readonly string _libraryDatabasePath;
    private readonly string _podcastsDatabasePath;
    private readonly string _statePath;
    private readonly Func<bool> _fullAmcIsRunning;

    public LiteProfileMutationStore(
        string libraryDatabasePath,
        string podcastsDatabasePath,
        string statePath,
        Func<bool>? fullAmcIsRunning = null)
    {
        _libraryDatabasePath = Path.GetFullPath(libraryDatabasePath);
        _podcastsDatabasePath = Path.GetFullPath(podcastsDatabasePath);
        _statePath = Path.GetFullPath(statePath);
        _fullAmcIsRunning = fullAmcIsRunning ?? FullAmcIsRunning;
    }

    public object RenameLibraryItem(string itemId, string requestedTitle)
    {
        var title = CleanRequiredText(requestedTitle, "Nowa nazwa nie może być pusta.");
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var item = ReadLocalItem(connection, transaction, itemId);
            var fileTitle = Path.GetFileNameWithoutExtension(item.Path);
            var custom = !string.Equals(title, fileTitle, StringComparison.CurrentCulture);
            Execute(connection, transaction,
                "UPDATE local_items SET title = $title, has_custom_title = $custom WHERE id = $id;",
                ("$title", title), ("$custom", custom), ("$id", itemId));
            Execute(connection, transaction,
                "UPDATE bookmarks SET item_title = $title WHERE session_id = 'local' COLLATE NOCASE AND item_id = $id;",
                ("$title", title), ("$id", itemId));
            transaction.Commit();
            return new { itemId, title, hasCustomTitle = custom };
        }
    }

    public object RenameLocalFile(string itemId, string requestedName)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var item = ReadLocalItem(connection, transaction, itemId);
            if (!File.Exists(item.Path))
                throw new LiteRequestException("Nie można zmienić nazwy: plik jest obecnie niedostępny.");
            if (!LocalFileRenamePolicy.TryBuildTargetPath(
                    item.Path, requestedName, out var targetPath, out var error))
                throw new LiteRequestException(error);

            var moved = false;
            try
            {
                File.Move(item.Path, targetPath);
                moved = true;
                var title = item.HasCustomTitle
                    ? item.Title
                    : Path.GetFileNameWithoutExtension(targetPath);
                Execute(connection, transaction,
                    "UPDATE local_items SET path = $path, title = $title WHERE id = $id;",
                    ("$path", targetPath), ("$title", title), ("$id", itemId));
                Execute(connection, transaction,
                    "UPDATE excluded_paths SET path = $path WHERE path = $old COLLATE NOCASE;",
                    ("$path", targetPath), ("$old", item.Path));
                if (!item.HasCustomTitle)
                {
                    Execute(connection, transaction,
                        "UPDATE bookmarks SET item_title = $title WHERE session_id = 'local' COLLATE NOCASE AND item_id = $id;",
                        ("$title", title), ("$id", itemId));
                }
                RewriteRecordingHistoryPath(item.Path, targetPath);
                transaction.Commit();
                return new
                {
                    itemId,
                    oldPath = item.Path,
                    path = targetPath,
                    title,
                    fileName = Path.GetFileName(targetPath)
                };
            }
            catch (LiteRequestException)
            {
                throw;
            }
            catch (Exception exception) when (IsExpectedFileFailure(exception))
            {
                if (moved && File.Exists(targetPath) && !File.Exists(item.Path))
                {
                    try { File.Move(targetPath, item.Path); }
                    catch { /* Raportujemy pierwotny błąd; stan dysku sprawdzi użytkownik. */ }
                }
                throw new LiteRequestException("Nie można zmienić nazwy pliku: " + exception.Message);
            }
        }
    }

    public object RenamePodcastSubscription(string subscriptionId, string requestedTitle)
    {
        var title = CleanRequiredText(requestedTitle, "Nowa nazwa podcastu nie może być pusta.");
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_podcastsDatabasePath);
            using var transaction = connection.BeginTransaction();
            string payloadText;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT payload_json FROM podcast_subscriptions WHERE id = $id AND is_in_library != 0;";
                read.Parameters.AddWithValue("$id", subscriptionId);
                payloadText = read.ExecuteScalar() as string
                    ?? throw new LiteRequestException("Nie można odnaleźć tego podcastu w Bibliotece.");
            }
            var payload = ParseObject(payloadText, "Dane podcastu są uszkodzone.");
            var oldTitle = ReadJsonText(payload, "Title", "title");
            payload["Title"] = title;
            payload["HasCustomTitle"] = true;
            Execute(connection, transaction,
                "UPDATE podcast_subscriptions SET title = $title, payload_json = $payload WHERE id = $id;",
                ("$title", title), ("$payload", payload.ToJsonString()), ("$id", subscriptionId));

            using var episodes = connection.CreateCommand();
            episodes.Transaction = transaction;
            episodes.CommandText = "SELECT id, payload_json FROM podcast_episodes WHERE subscription_id = $id;";
            episodes.Parameters.AddWithValue("$id", subscriptionId);
            var changedEpisodes = new List<(string Id, string Payload)>();
            using (var reader = episodes.ExecuteReader())
            {
                while (reader.Read())
                {
                    var episode = ParseObject(reader.GetString(1), "Dane odcinka są uszkodzone.");
                    var author = ReadJsonText(episode, "Author", "author");
                    if (author.Length > 0
                        && !string.Equals(author, oldTitle, StringComparison.CurrentCulture))
                        continue;
                    // Pełne AMC pozostawia puste Author, żeby formatter użył
                    // nowej nazwy źródła zamiast utrwalać jej kopię w odcinku.
                    episode["Author"] = string.Empty;
                    changedEpisodes.Add((reader.GetString(0), episode.ToJsonString()));
                }
            }
            foreach (var episode in changedEpisodes)
            {
                Execute(connection, transaction,
                    "UPDATE podcast_episodes SET payload_json = $payload WHERE id = $id;",
                    ("$payload", episode.Payload), ("$id", episode.Id));
            }
            transaction.Commit();
            return new { subscriptionId, title };
        }
    }

    public RadioStationEditResult EditRadioStation(
        string stationId,
        string requestedName,
        string requestedUrl)
    {
        var name = CleanRequiredText(requestedName, "Nazwa stacji nie może być pusta.");
        var url = CleanRequiredText(requestedUrl, "Adres stacji nie może być pusty.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            throw new LiteRequestException(
                "Wpisz pełny adres strumienia rozpoczynający się od http:// lub https://.");
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var station = FindRadioStation(root, stationId);
            var oldUrl = ReadJsonText(station, "streamUrl", "StreamUrl");
            var streamChanged = !string.Equals(oldUrl, url, StringComparison.OrdinalIgnoreCase);
            station["name"] = name;
            station["hasCustomTitle"] = true;
            station["streamUrl"] = url;
            station["isInLibrary"] = true;
            if (root["radio"]?["recordingSchedules"] is JsonArray schedules)
            {
                foreach (var schedule in schedules.OfType<JsonObject>().Where(candidate =>
                    string.Equals(ReadJsonText(candidate, "stationId", "StationId"), stationId, StringComparison.Ordinal)))
                {
                    schedule["stationName"] = name;
                    schedule["streamUrl"] = url;
                }
            }
            WriteState(root);
            return new RadioStationEditResult(stationId, name, url, streamChanged);
        }
    }

    public object Remove(string sessionId, string view, IReadOnlyList<string> itemIds)
    {
        var ids = itemIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) throw new LiteRequestException("Brak elementu do usunięcia.");
        EnsureMayWrite();
        lock (_gate)
        {
            if (string.Equals(sessionId, "radio", StringComparison.Ordinal))
                return RemoveRadio(view, ids);
            if (string.Equals(sessionId, "podcasts", StringComparison.Ordinal))
                return RemovePodcasts(view, ids);
            if (!string.Equals(sessionId, "files", StringComparison.Ordinal))
                throw new LiteRequestException("Usuwanie w tej sesji nie jest jeszcze dostępne.");
            return RemoveLocal(view, ids);
        }
    }

    public object RecycleLocalFiles(IReadOnlyList<string> itemIds)
    {
        var ids = itemIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) throw new LiteRequestException("Brak pliku do przeniesienia do Kosza.");
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            var items = ids.Select(id => ReadLocalItem(connection, null, id)).ToArray();
            var removed = new List<LocalItem>();
            var failures = new List<string>();
            foreach (var item in items)
            {
                try
                {
                    if (File.Exists(item.Path)) WindowsRecycleBin.MoveFile(item.Path, 0);
                    removed.Add(item);
                }
                catch (Exception exception) when (WindowsRecycleBin.IsExpectedFailure(exception))
                {
                    failures.Add($"{item.Title}: {exception.Message}");
                }
            }
            if (removed.Count > 0)
            {
                using var transaction = connection.BeginTransaction();
                foreach (var item in removed) RemoveLocalRecord(connection, transaction, item.Id);
                transaction.Commit();
            }
            return new
            {
                removed = removed.Select(item => new { item.Id, item.Title, item.Path }).ToArray(),
                failures
            };
        }
    }

    private object RemoveLocal(string view, IReadOnlyList<string> itemIds)
    {
        using var connection = OpenDatabase(_libraryDatabasePath);
        using var transaction = connection.BeginTransaction();
        var existing = itemIds.Select(id => ReadLocalItem(connection, transaction, id)).ToArray();
        if (string.Equals(view, "history", StringComparison.Ordinal))
        {
            foreach (var item in existing)
                Execute(connection, transaction,
                    "DELETE FROM playback_history WHERE session_id = 'local' COLLATE NOCASE AND item_id = $id;",
                    ("$id", item.Id));
        }
        else if (string.Equals(view, "favorites", StringComparison.Ordinal))
        {
            foreach (var item in existing)
            {
                Execute(connection, transaction,
                    "UPDATE local_items SET is_favorite = 0 WHERE id = $id;", ("$id", item.Id));
                DeleteOrderRows(connection, transaction, "favorite_order", "local", item.Id);
            }
        }
        else if (string.Equals(view, "savedQueue", StringComparison.Ordinal)
                 || string.Equals(view, "liveQueue", StringComparison.Ordinal))
        {
            foreach (var item in existing)
            {
                Execute(connection, transaction,
                    "UPDATE local_items SET is_in_queue = 0, is_play_next = 0 WHERE id = $id;", ("$id", item.Id));
                DeleteOrderRows(connection, transaction, "queue_order", "local", item.Id);
                DeleteOrderRows(connection, transaction, "queue_play_next_order", "local", item.Id);
                DeleteOrderRows(connection, transaction, "queue_regular_order", "local", item.Id);
            }
        }
        else
        {
            foreach (var item in existing)
            {
                Execute(connection, transaction,
                    "UPDATE local_items SET is_in_library = 0 WHERE id = $id;", ("$id", item.Id));
                AddExcludedPath(connection, transaction, item.Path);
            }
        }
        transaction.Commit();
        return new { removedCount = existing.Length, titles = existing.Select(item => item.Title).ToArray() };
    }

    private object RemoveRadio(string view, IReadOnlyList<string> itemIds)
    {
        if (string.Equals(view, "history", StringComparison.Ordinal))
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            foreach (var id in itemIds)
                Execute(connection, transaction,
                    "DELETE FROM playback_history WHERE session_id = 'radio' COLLATE NOCASE AND item_id = $id;",
                    ("$id", id));
            transaction.Commit();
            return new { removedCount = itemIds.Count };
        }

        var root = ReadState();
        var titles = new List<string>();
        foreach (var id in itemIds)
        {
            var station = FindRadioStation(root, id);
            titles.Add(ReadJsonText(station, "name", "Name"));
            if (string.Equals(view, "favorites", StringComparison.Ordinal))
            {
                station["isFavorite"] = false;
            }
            else
            {
                station["isInLibrary"] = false;
                station["isFavorite"] = false;
            }
        }
        WriteState(root);
        return new { removedCount = itemIds.Count, titles };
    }

    private object RemovePodcasts(string view, IReadOnlyList<string> itemIds)
    {
        if (string.Equals(view, "podcastHistory", StringComparison.Ordinal))
        {
            using var historyConnection = OpenDatabase(_libraryDatabasePath);
            using var historyTransaction = historyConnection.BeginTransaction();
            foreach (var id in itemIds)
            {
                Execute(
                    historyConnection,
                    historyTransaction,
                    "DELETE FROM playback_history WHERE session_id = 'podcasts' COLLATE NOCASE AND item_id = $id;",
                    ("$id", id));
            }
            historyTransaction.Commit();
            return new { removedCount = itemIds.Count };
        }
        if (!string.Equals(view, "podcastLibrary", StringComparison.Ordinal))
            throw new LiteRequestException("Usuń źródło z głównej listy Podcastów i YouTube.");
        using var connection = OpenDatabase(_podcastsDatabasePath);
        using var transaction = connection.BeginTransaction();
        var removed = 0;
        foreach (var id in itemIds)
        {
            string? payloadText;
            using (var read = connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = "SELECT payload_json FROM podcast_subscriptions WHERE id = $id AND is_in_library != 0;";
                read.Parameters.AddWithValue("$id", id);
                payloadText = read.ExecuteScalar() as string;
            }
            if (payloadText is null) continue;
            var payload = ParseObject(payloadText, "Dane podcastu są uszkodzone.");
            payload["IsInLibrary"] = false;
            Execute(connection, transaction,
                "UPDATE podcast_subscriptions SET is_in_library = 0, payload_json = $payload WHERE id = $id;",
                ("$payload", payload.ToJsonString()), ("$id", id));
            removed++;
        }
        transaction.Commit();
        return new { removedCount = removed };
    }

    private static void RemoveLocalRecord(
        SqliteConnection connection, SqliteTransaction transaction, string itemId)
    {
        foreach (var table in new[]
                 {
                     "custom_order", "favorite_order", "favorite_added_order",
                     "library_added_order", "library_custom_order", "queue_order",
                     "queue_play_next_order", "queue_regular_order", "playlist_items"
                 })
        {
            Execute(connection, transaction, $"DELETE FROM {table} WHERE item_id = $id;", ("$id", itemId));
        }
        Execute(connection, transaction,
            "DELETE FROM bookmarks WHERE session_id = 'local' COLLATE NOCASE AND item_id = $id;", ("$id", itemId));
        Execute(connection, transaction,
            "DELETE FROM playback_history WHERE session_id = 'local' COLLATE NOCASE AND item_id = $id;", ("$id", itemId));
        Execute(connection, transaction, "DELETE FROM local_items WHERE id = $id;", ("$id", itemId));
    }

    private static void DeleteOrderRows(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string sessionId,
        string itemId) =>
        Execute(connection, transaction,
            $"DELETE FROM {table} WHERE session_id = $session AND item_id = $id;",
            ("$session", sessionId), ("$id", itemId));

    private static void AddExcludedPath(
        SqliteConnection connection, SqliteTransaction transaction, string path)
    {
        using var exists = connection.CreateCommand();
        exists.Transaction = transaction;
        exists.CommandText = "SELECT 1 FROM excluded_paths WHERE path = $path COLLATE NOCASE LIMIT 1;";
        exists.Parameters.AddWithValue("$path", path);
        if (exists.ExecuteScalar() is not null) return;
        Execute(connection, transaction,
            "INSERT INTO excluded_paths(ordinal, path) VALUES((SELECT COALESCE(MAX(ordinal), -1) + 1 FROM excluded_paths), $path);",
            ("$path", path));
    }

    private void RewriteRecordingHistoryPath(string oldPath, string newPath)
    {
        if (!File.Exists(_statePath)) return;
        var root = ReadState();
        if (root["radio"]?["recordingHistory"] is not JsonArray history) return;
        var changed = false;
        foreach (var entry in history.OfType<JsonObject>())
        {
            var path = ReadJsonText(entry, "outputPath", "OutputPath");
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(oldPath), StringComparison.OrdinalIgnoreCase))
                continue;
            entry["outputPath"] = newPath;
            changed = true;
        }
        if (changed) WriteState(root);
    }

    private JsonObject ReadState()
    {
        if (!File.Exists(_statePath)) throw new LiteRequestException("Nie znajduję profilu AMC.");
        try
        {
            return ParseObject(File.ReadAllText(_statePath), "Profil AMC jest uszkodzony.");
        }
        catch (IOException exception)
        {
            throw new LiteRequestException("Nie można odczytać profilu AMC: " + exception.Message);
        }
    }

    private void WriteState(JsonObject root)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = _statePath + ".wx-lite.tmp";
        try
        {
            File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(_statePath))
                File.Replace(temporaryPath, _statePath, _statePath + ".bak", true);
            else
                File.Move(temporaryPath, _statePath);
        }
        catch (IOException exception)
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
            throw new LiteRequestException("Nie można zapisać profilu AMC: " + exception.Message);
        }
    }

    private static JsonObject FindRadioStation(JsonObject root, string stationId)
    {
        if (root["radio"]?["stations"] is not JsonArray stations)
            throw new LiteRequestException("Profil AMC nie zawiera listy stacji.");
        return stations.OfType<JsonObject>().FirstOrDefault(station =>
                   string.Equals(ReadJsonText(station, "id", "Id"), stationId, StringComparison.Ordinal))
               ?? throw new LiteRequestException("Nie można odnaleźć tej stacji w profilu AMC.");
    }

    private static JsonObject ParseObject(string json, string error)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? throw new LiteRequestException(error);
        }
        catch (JsonException)
        {
            throw new LiteRequestException(error);
        }
    }

    private static string ReadJsonText(JsonObject value, string first, string second) =>
        value[first]?.GetValue<string?>()?.Trim()
        ?? value[second]?.GetValue<string?>()?.Trim()
        ?? string.Empty;

    private static string CleanRequiredText(string value, string error)
    {
        var cleaned = value.Trim();
        if (cleaned.Length == 0) throw new LiteRequestException(error);
        return cleaned;
    }

    private void EnsureMayWrite()
    {
        if (_fullAmcIsRunning())
            throw new LiteRequestException(
                "Zamknij najpierw główne AMC. Równoczesny zapis z dwóch wersji mógłby utracić dane.");
    }

    private static bool FullAmcIsRunning()
    {
        foreach (var process in Process.GetProcessesByName("AccessibleMediaController"))
        {
            using (process)
            {
                if (!process.HasExited) return true;
            }
        }
        return false;
    }

    private static SqliteConnection OpenDatabase(string path)
    {
        if (!File.Exists(path)) throw new LiteRequestException("Nie znajduję bazy danych AMC.");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString());
        // library.db ma indeksy i kolumny używające tej samej polskiej
        // kolacji co pełne AMC. Bez rejestracji kolacji nawet zwykły UPDATE
        // tytułu kończyłby się błędem "no such collation sequence: AMC_PL".
        connection.CreateCollation(
            "AMC_PL",
            (left, right) => CultureInfo.GetCultureInfo("pl-PL").CompareInfo.Compare(
                left,
                right,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace));
        connection.Open();
        using var setup = connection.CreateCommand();
        setup.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        setup.ExecuteNonQuery();
        return connection;
    }

    private static LocalItem ReadLocalItem(
        SqliteConnection connection, SqliteTransaction? transaction, string itemId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, title, has_custom_title, path FROM local_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new LiteRequestException("Nie można odnaleźć tego pliku w Bibliotece AMC.");
        return new LocalItem(reader.GetString(0), reader.GetString(1), reader.GetInt64(2) != 0, reader.GetString(3));
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static bool IsExpectedFileFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException
            or NotSupportedException or PathTooLongException;

    private sealed record LocalItem(string Id, string Title, bool HasCustomTitle, string Path);

    internal sealed record RadioStationEditResult(
        [property: JsonPropertyName("stationId")] string StationId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("streamChanged")] bool StreamChanged);
}
