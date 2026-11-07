using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Podcasts;
using AccessibleMediaController.Core.Sonos;
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
    private static readonly TimeSpan LocalFileReleaseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LocalFileReleaseRetryDelay = TimeSpan.FromMilliseconds(50);
    private readonly object _gate = new();
    private readonly string _libraryDatabasePath;
    private readonly string _podcastsDatabasePath;
    private readonly string _statePath;
    private readonly Func<bool> _fullAmcIsRunning;
    private readonly LocalFolderLibraryManager _localFolders;

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
        _localFolders = new LocalFolderLibraryManager(_libraryDatabasePath);
    }

    public object ListLocalFolderSources()
    {
        lock (_gate)
        {
            return LocalFolderPayload(_localFolders.List(), null, null);
        }
    }

    public bool RecordPlaybackHistory(string sessionId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)
            || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var changed = LitePlaybackHistoryMutation.Record(
                connection, transaction, sessionId, itemId);
            if (changed) TouchMetadata(connection, transaction);
            transaction.Commit();
            return changed;
        }
    }

    public object AddLocalFolderSource(string path)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var result = _localFolders.Add(path);
            return LocalFolderPayload(
                result.Sources, result.Message, result.SelectedSourceId);
        }
    }

    public object RefreshLocalFolderSources(IReadOnlyCollection<string> sourceIds)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var result = _localFolders.Refresh(sourceIds);
            return LocalFolderPayload(
                result.Sources, result.Message, result.SelectedSourceId);
        }
    }

    public object DetachLocalFolderSource(string sourceId)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var result = _localFolders.Detach(sourceId);
            return LocalFolderPayload(
                result.Sources, result.Message, result.SelectedSourceId);
        }
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

    public LocalFileRenameResult RenameLocalFile(
        string itemId,
        string requestedName,
        string? sourcePath = null)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var item = string.IsNullOrWhiteSpace(sourcePath)
                ? ReadLocalItem(connection, transaction, itemId)
                : ReadLocalItemByPath(connection, transaction, sourcePath);
            if (!File.Exists(item.Path))
                throw new LiteRequestException("Nie można zmienić nazwy: plik jest obecnie niedostępny.");
            if (!LocalFileRenamePolicy.TryBuildTargetPath(
                    item.Path, requestedName, out var targetPath, out var error))
                throw new LiteRequestException(error);

            var moved = false;
            try
            {
                MoveLocalFileAfterRelease(item.Path, targetPath);
                moved = true;
                var title = item.HasCustomTitle
                    ? item.Title
                    : Path.GetFileNameWithoutExtension(targetPath);
                // Wiersz Historii nagrywania może wskazywać świeży plik,
                // którego skaner Biblioteki jeszcze nie zdążył zindeksować.
                // Taki plik wolno przemianować po zweryfikowanej ścieżce,
                // ale nie udajemy wtedy aktualizacji rekordu o pustym Id.
                // Następny skan doda już plik pod nową nazwą.
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    Execute(connection, transaction,
                        "UPDATE local_items SET path = $path, title = $title WHERE id = $id;",
                        ("$path", targetPath), ("$title", title), ("$id", item.Id));
                }
                Execute(connection, transaction,
                    "UPDATE excluded_paths SET path = $path WHERE path = $old COLLATE NOCASE;",
                    ("$path", targetPath), ("$old", item.Path));
                if (!string.IsNullOrWhiteSpace(item.Id) && !item.HasCustomTitle)
                {
                    Execute(connection, transaction,
                        "UPDATE bookmarks SET item_title = $title WHERE session_id = 'local' COLLATE NOCASE AND item_id = $id;",
                        ("$title", title), ("$id", item.Id));
                }
                RewriteRecordingHistoryPath(item.Path, targetPath);
                transaction.Commit();
                return new LocalFileRenameResult(
                    string.IsNullOrWhiteSpace(item.Id) ? itemId : item.Id,
                    item.Path,
                    targetPath,
                    title,
                    Path.GetFileName(targetPath));
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

    private static void MoveLocalFileAfterRelease(string sourcePath, string targetPath)
    {
        // WindowsMediaOutput.Stop odczepia tor natychmiast, ale kosztowne
        // zamkniecie dekodera i uchwytu pliku wykonuje w tle. Shift+F2 idzie
        // zaraz potem ta sama droga hosta, wiec pojedyncze File.Move potrafilo
        // przegrac ten krotki wyscig mimo poprawnego zatrzymania odtwarzania.
        // Ponawiamy wylacznie przejsciowy IOException, dopoki zrodlo nadal
        // istnieje i cel nie zostal zajety przez inny wpis. Interfejs czeka
        // na hosta poza watkiem GUI, a limit jest krotszy od timeoutu klienta.
        var started = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                File.Move(sourcePath, targetPath);
                return;
            }
            catch (IOException) when (
                started.Elapsed < LocalFileReleaseTimeout
                && File.Exists(sourcePath)
                && (!File.Exists(targetPath) && !Directory.Exists(targetPath)
                    || string.Equals(
                        sourcePath,
                        targetPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                Thread.Sleep(LocalFileReleaseRetryDelay);
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

    /// <summary>
    /// Ctrl+Shift+L w sesji Podcasty. Odcinek jest wcześniej rozwiązany przez
    /// frontend do identyfikatora źródła nadrzędnego, więc zapis dotyka tylko
    /// członkostwa subskrypcji. Decyzja jest grupowa jak w pełnym AMC: jeżeli
    /// choć jednego źródła nie ma w Bibliotece, dodajemy wszystkie; dopiero
    /// gdy wszystkie już należą, usuwamy wszystkie.
    /// </summary>
    public object TogglePodcastLibrary(IReadOnlyList<string> subscriptionIds)
    {
        var ids = CleanDistinctIds(subscriptionIds, "Wybierz podcast albo odcinek.");
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_podcastsDatabasePath);
            using var transaction = connection.BeginTransaction();
            var rows = new List<(string Id, bool InLibrary, JsonObject Payload)>();
            foreach (var id in ids)
            {
                using var read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT is_in_library, payload_json FROM podcast_subscriptions WHERE id = $id;";
                read.Parameters.AddWithValue("$id", id);
                using var reader = read.ExecuteReader();
                if (!reader.Read())
                    throw new LiteRequestException("Nie można odnaleźć podcastu nadrzędnego.");
                rows.Add((id, reader.GetInt64(0) != 0,
                    ParseObject(reader.GetString(1), "Dane podcastu są uszkodzone.")));
            }

            var added = rows.Any(row => !row.InLibrary);
            var changed = 0;
            foreach (var row in rows)
            {
                if (row.InLibrary == added) continue;
                row.Payload["IsInLibrary"] = added;
                if (!added) row.Payload["IsFavorite"] = false;
                Execute(connection, transaction,
                    "UPDATE podcast_subscriptions SET is_in_library = $library, payload_json = $payload WHERE id = $id;",
                    ("$library", added), ("$payload", row.Payload.ToJsonString()), ("$id", row.Id));
                changed++;
            }
            TouchMetadata(connection, transaction);
            transaction.Commit();
            return new { added, requested = rows.Count, changed };
        }
    }

    /// <summary>
    /// Trwała kolejka podcastów. Flagi odcinka w podcasts.db i trzy porządki
    /// sesji w library.db zmieniają się w jednej transakcji na bazie z
    /// dołączonym magazynem. Dzięki temu Ctrl+Shift+Q i Ctrl+Shift+Enter nie
    /// tworzą stanu, którego pełne AMC nie potrafiłoby potem odtworzyć.
    /// </summary>
    public object TogglePodcastQueue(IReadOnlyList<string> episodeIds, bool playNext)
    {
        var ids = CleanDistinctIds(episodeIds, "Wybierz odcinek podcastu.");
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_podcastsDatabasePath);
            using (var attach = connection.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE $path AS library;";
                attach.Parameters.AddWithValue("$path", _libraryDatabasePath);
                attach.ExecuteNonQuery();
            }
            using var transaction = connection.BeginTransaction();
            var rows = new List<(string Id, bool InQueue, bool PlayNext, JsonObject Payload)>();
            foreach (var id in ids)
            {
                using var read = connection.CreateCommand();
                read.Transaction = transaction;
                read.CommandText = "SELECT is_in_queue, is_play_next, payload_json FROM podcast_episodes WHERE id = $id;";
                read.Parameters.AddWithValue("$id", id);
                using var reader = read.ExecuteReader();
                if (!reader.Read())
                    throw new LiteRequestException("Niektórych wybranych odcinków nie ma już w Bibliotece.");
                rows.Add((id, reader.GetInt64(0) != 0, reader.GetInt64(1) != 0,
                    ParseObject(reader.GetString(2), "Dane odcinka są uszkodzone.")));
            }

            var added = playNext
                ? rows.Any(row => !row.PlayNext)
                : rows.Any(row => !row.InQueue && !row.PlayNext);
            var final = new Dictionary<string, (bool InQueue, bool PlayNext)>(StringComparer.Ordinal);
            var changed = 0;
            foreach (var row in rows)
            {
                var inQueue = playNext ? row.InQueue : added;
                var isPlayNext = playNext ? added : added ? row.PlayNext : false;
                final[row.Id] = (inQueue, isPlayNext);
                if (row.InQueue == inQueue && row.PlayNext == isPlayNext) continue;
                row.Payload["IsInQueue"] = inQueue;
                row.Payload["IsPlayNext"] = isPlayNext;
                Execute(connection, transaction,
                    "UPDATE podcast_episodes SET is_in_queue = $queue, is_play_next = $next, payload_json = $payload WHERE id = $id;",
                    ("$queue", inQueue), ("$next", isPlayNext),
                    ("$payload", row.Payload.ToJsonString()), ("$id", row.Id));
                changed++;
            }

            var order = ReadAttachedOrder(connection, transaction, "queue_order");
            var regular = ReadAttachedOrder(connection, transaction, "queue_regular_order");
            var next = ReadAttachedOrder(connection, transaction, "queue_play_next_order");
            foreach (var id in ids)
            {
                var state = final[id];
                SetOrderedMembership(order, id, state.InQueue || state.PlayNext);
                SetOrderedMembership(regular, id, state.InQueue);
                SetOrderedMembership(next, id, state.PlayNext);
            }
            RewriteAttachedOrder(connection, transaction, "queue_order", order);
            RewriteAttachedOrder(connection, transaction, "queue_regular_order", regular);
            RewriteAttachedOrder(connection, transaction, "queue_play_next_order", next);
            TouchMetadata(connection, transaction);
            transaction.Commit();
            return new { added, requested = rows.Count, changed, playNext };
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

    public LiteRadioLibraryMutationResult AddRadioStation(
        string requestedName,
        string requestedUrl) =>
        MutateRadioLibrary(
            [NormalizeRadioStation(requestedName, requestedUrl)],
            parserSkipped: 0,
            updateExistingTitles: true);

    public LiteRadioLibraryMutationResult ImportRadioStations(
        IReadOnlyList<LiteRadioStationDraft> requestedStations,
        int parserSkipped)
    {
        if (requestedStations.Count > 10_000)
            throw new LiteRequestException("Playlista może zawierać najwyżej 10000 stacji.");
        var stations = requestedStations
            .Select(station => NormalizeRadioStation(station.Name, station.Url))
            .ToArray();
        return MutateRadioLibrary(
            stations,
            parserSkipped,
            updateExistingTitles: false);
    }

    public LiteRadioFavoritesExportResult ExportRadioFavorites(string destinationPath)
    {
        lock (_gate)
        {
            var root = ReadState();
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var customOrder = ReadOrder(connection, transaction, "favorite_order", "radio");
            var addedOrder = ReadOrder(connection, transaction, "favorite_added_order", "radio");
            try
            {
                return LiteRadioFavoritesExport.Write(
                    root,
                    customOrder,
                    addedOrder,
                    destinationPath);
            }
            catch (InvalidDataException exception)
            {
                throw new LiteRequestException(exception.Message);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                throw new LiteRequestException("Nie można zapisać playlisty: " + exception.Message);
            }
        }
    }

    public object AddWiiMNetworkStream(string requestedName, string requestedUrl)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteWiiMProfileMutation.Add(
                root,
                requestedName,
                requestedUrl);
            WriteState(root);
            return new
            {
                preferredStreamId = result.PreferredStreamId,
                title = result.Title,
                addedCount = result.AddedCount,
                updatedCount = result.UpdatedCount,
                message = result.UpdatedCount > 0
                    ? $"Zaktualizowano strumień: {result.Title}"
                    : $"Dodano strumień WiiM: {result.Title}"
            };
        }
    }

    public object EditWiiMNetworkStream(
        string streamId,
        string requestedName,
        string requestedUrl)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteWiiMProfileMutation.Edit(
                root,
                streamId,
                requestedName,
                requestedUrl);
            WriteState(root);
            return new
            {
                preferredStreamId = result.PreferredStreamId,
                title = result.Title,
                message = $"Zapisano strumień WiiM: {result.Title}"
            };
        }
    }

    public object RemoveWiiMNetworkStreams(IReadOnlyList<string> streamIds)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var titles = LiteWiiMProfileMutation.Remove(root, streamIds);
            WriteState(root);
            return new
            {
                removedCount = titles.Length,
                titles,
                message = titles.Length == 1
                    ? $"Usunięto z AMC strumień WiiM: {titles[0]}. WiiM Home pozostał bez zmian"
                    : $"Usunięto z AMC strumienie WiiM: {titles.Length}. WiiM Home pozostał bez zmian"
            };
        }
    }

    public object ImportWiiMNetworkStreams(
        IReadOnlyList<LiteWiiMNetworkStreamDraft> streams,
        int parserSkipped)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteWiiMProfileMutation.Import(
                root,
                streams,
                parserSkipped);
            if (result.AddedCount > 0) WriteState(root);
            return new
            {
                preferredStreamId = result.PreferredStreamId,
                addedCount = result.AddedCount,
                skippedCount = result.SkippedCount,
                message = result.AddedCount > 0
                    ? $"Zaimportowano strumienie do WiiM: {result.AddedCount}"
                    : result.InvalidCount > 0
                        ? "Playlista nie zawiera nowych prawidłowych strumieni WiiM"
                        : "Wszystkie strumienie z tej playlisty są już zapisane"
            };
        }
    }

    public object SetWiiMNetworkStreamSortMode(CollectionSortMode mode)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            LiteWiiMProfileMutation.SetSortMode(root, mode);
            WriteState(root);
            var label = mode switch
            {
                CollectionSortMode.Alphabetical => "alfabetycznie",
                CollectionSortMode.Custom => "kolejność własna",
                _ => "według dodania, najnowsze najpierw"
            };
            return new
            {
                sortMode = mode.ToString(),
                message = $"Strumienie WiiM: {label}"
            };
        }
    }

    public object MoveWiiMNetworkStreams(
        IReadOnlyList<string> streamIds,
        int direction)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteWiiMProfileMutation.Move(
                root, streamIds, direction);
            WriteState(root);
            return WiiMOrderMutationResponse(result, streamIds.Count);
        }
    }

    public object PlaceWiiMNetworkStreamsBefore(
        IReadOnlyList<string> streamIds,
        string targetId)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteWiiMProfileMutation.PlaceBefore(
                root, streamIds, targetId);
            WriteState(root);
            return WiiMOrderMutationResponse(result, streamIds.Count);
        }
    }

    public LiteWiiMNetworkStreamExportResult ExportWiiMNetworkStreams(
        string destinationPath)
    {
        lock (_gate)
        {
            try
            {
                return LiteWiiMNetworkStreamExport.Write(
                    ReadState(),
                    destinationPath);
            }
            catch (InvalidDataException exception)
            {
                throw new LiteRequestException(exception.Message);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                throw new LiteRequestException(
                    "Nie można zapisać playlisty: " + exception.Message);
            }
        }
    }

    public object AddSonosOwnStream(string requestedName, string requestedUrl)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteSonosOwnStreamsMutation.Add(
                root, requestedName, requestedUrl);
            WriteState(root);
            return new
            {
                preferredStreamId = result.PreferredStreamId,
                title = result.Title,
                message = result.UpdatedCount > 0
                    ? $"Zaktualizowano stację Sonos: {result.Title}"
                    : $"Dodano stację Sonos: {result.Title}"
            };
        }
    }

    public object SonosOwnStreamEditInfo(string streamId)
    {
        lock (_gate)
        {
            var stream = LiteSonosOwnStreamsMutation.ReadForEdit(
                ReadState(), streamId);
            return new
            {
                streamId = stream.Id,
                name = stream.Name,
                url = stream.StreamUrl
            };
        }
    }

    public object EditSonosOwnStream(
        string streamId,
        string requestedName,
        string requestedUrl)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteSonosOwnStreamsMutation.Edit(
                root, streamId, requestedName, requestedUrl);
            WriteState(root);
            return new
            {
                preferredStreamId = result.PreferredStreamId,
                title = result.Title,
                message = $"Zapisano stację Sonos: {result.Title}"
            };
        }
    }

    public object RemoveSonosOwnStreams(IReadOnlyList<string> streamIds)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var titles = LiteSonosOwnStreamsMutation.Remove(root, streamIds);
            WriteState(root);
            return new
            {
                removedCount = titles.Length,
                titles,
                message = titles.Length == 1
                    ? $"Usunięto z AMC stację Sonos: {titles[0]}"
                    : $"Usunięto z AMC stacje Sonos: {titles.Length}"
            };
        }
    }

    public object ImportSonosOwnStreams(
        IReadOnlyList<LiteSonosOwnStreamDraft> streams,
        int parserSkipped)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteSonosOwnStreamsMutation.Import(
                root, streams, parserSkipped);
            if (result.AddedCount > 0) WriteState(root);
            return new
            {
                preferredStreamId = result.PreferredStreamId,
                addedCount = result.AddedCount,
                skippedCount = result.SkippedCount,
                message = result.AddedCount > 0
                    ? $"Zaimportowano stacje Sonos: {result.AddedCount}"
                    : result.InvalidCount > 0
                        ? "Playlista nie zawiera nowych prawidłowych stacji Sonos"
                        : "Wszystkie stacje z tej playlisty są już zapisane"
            };
        }
    }

    public object SetSonosOwnStreamSortMode(CollectionSortMode mode)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            LiteSonosOwnStreamsMutation.SetSortMode(root, mode);
            WriteState(root);
            return new
            {
                sortMode = mode.ToString(),
                message = $"Moje stacje Sonos: {SonosOwnStreamsOrder.DescribeMode(mode)}"
            };
        }
    }

    public object MoveSonosOwnStreams(
        IReadOnlyList<string> streamIds,
        int direction)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteSonosOwnStreamsMutation.Move(
                root, streamIds, direction);
            WriteState(root);
            return SonosOrderMutationResponse(result, streamIds.Count);
        }
    }

    public object PlaceSonosOwnStreamsBefore(
        IReadOnlyList<string> streamIds,
        string targetId)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var result = LiteSonosOwnStreamsMutation.PlaceBefore(
                root, streamIds, targetId);
            WriteState(root);
            return SonosOrderMutationResponse(result, streamIds.Count);
        }
    }

    public LiteSonosOwnStreamsExportResult ExportSonosOwnStreams(
        string destinationPath)
    {
        lock (_gate)
        {
            try
            {
                return LiteSonosOwnStreamsExport.Write(
                    ReadState(), destinationPath);
            }
            catch (InvalidDataException exception)
            {
                throw new LiteRequestException(exception.Message);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                throw new LiteRequestException(
                    "Nie można zapisać playlisty: " + exception.Message);
            }
        }
    }

    private static object SonosOrderMutationResponse(
        LiteSonosOwnStreamOrderMutationResult result,
        int selectedCount) => new
        {
            preferredStreamId = result.PreferredStreamId,
            message = selectedCount == 1
                ? $"{result.Title}, pozycja {result.Position} z {result.Count}"
                : $"Przeniesiono {selectedCount} stacji, od pozycji "
                    + $"{result.Position} z {result.Count}"
        };

    private static object WiiMOrderMutationResponse(
        LiteWiiMNetworkStreamOrderMutationResult result,
        int selectedCount) => new
        {
            preferredStreamId = result.PreferredStreamId,
            message = selectedCount == 1
                ? $"{result.Title}, pozycja {result.Position} z {result.Count}"
                : $"Przeniesiono {selectedCount} strumieni, od pozycji "
                    + $"{result.Position} z {result.Count}"
        };

    private LiteRadioLibraryMutationResult MutateRadioLibrary(
        IReadOnlyList<LiteRadioStationDraft> stations,
        int parserSkipped,
        bool updateExistingTitles)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            LiteRadioLibraryMutationResult result;
            try
            {
                result = LiteRadioLibraryMutation.Apply(
                    root,
                    stations,
                    parserSkipped,
                    updateExistingTitles);
            }
            catch (InvalidDataException exception)
            {
                throw new LiteRequestException(exception.Message);
            }
            if (result.SourceStationIds.Count == 0) return result;

            if (result.Changed) WriteState(root);
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var customOrder = ReadOrder(connection, transaction, "library_custom_order", "radio");
            var addedOrder = ReadOrder(connection, transaction, "library_added_order", "radio");
            // Jedna współdzielona reguła dla zapisu i testu: lista AddedNewest
            // odwraca trwały porządek, ale nie może odwrócić samej playlisty.
            LiteRadioLibraryMutation.ApplyChangedOrder(
                customOrder,
                addedOrder,
                result.SourceStationIds);
            RewriteOrder(connection, transaction, "library_custom_order", "radio", customOrder);
            RewriteOrder(connection, transaction, "library_added_order", "radio", addedOrder);
            TouchMetadata(connection, transaction);
            transaction.Commit();
            return result;
        }
    }

    private static LiteRadioStationDraft NormalizeRadioStation(string requestedName, string requestedUrl)
    {
        var name = CleanRequiredText(requestedName, "Nazwa stacji nie może być pusta.");
        var url = CleanRequiredText(requestedUrl, "Adres stacji nie może być pusty.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
            throw new LiteRequestException(
                "Wpisz pełny adres strumienia rozpoczynający się od http:// lub https://.");
        return new LiteRadioStationDraft(name, url);
    }

    /// <summary>
    /// Ctrl+Shift+U dla plików lokalnych i radia. Całe zaznaczenie otrzymuje
    /// jeden stan, tak jak w CommandRouter pełnego AMC: jeśli choć jeden
    /// element nie jest ulubiony, dodajemy wszystkie; dopiero gdy wszystkie
    /// są ulubione, usuwamy wszystkie. Oba trwałe porządki są aktualizowane
    /// razem z członkostwem, żeby widoki „według dodania” i „własna” nie
    /// rozjechały się po ponownym uruchomieniu.
    /// </summary>
    public object ToggleFavorites(
        string sessionId,
        IReadOnlyList<string> itemIds,
        IReadOnlyList<string>? sourcePaths = null)
    {
        var ids = CleanDistinctIds(itemIds, "Wybierz plik albo stację radiową.");
        EnsureMayWrite();
        lock (_gate)
        {
            if (string.Equals(sessionId, "files", StringComparison.Ordinal))
                return ToggleLocalFavorites(ids, sourcePaths);
            if (string.Equals(sessionId, "radio", StringComparison.Ordinal))
                return ToggleRadioFavorites(ids);
            throw new LiteRequestException("Ulubione nie są dostępne w tej sesji.");
        }
    }

    /// <summary>
    /// Ctrl+Shift+L w Radiu zmienia członkostwo zaznaczonych stacji w
    /// Bibliotece. Jeden gest ustawia wspólny stan całego zaznaczenia:
    /// dodaje, jeżeli choć jednej stacji brakuje, a usuwa dopiero wtedy, gdy
    /// wszystkie już należą do Biblioteki.
    /// </summary>
    public object ToggleRadioLibrary(IReadOnlyList<string> itemIds)
    {
        var ids = CleanDistinctIds(itemIds, "Wybierz stację radiową.");
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var rows = ids.Select(id =>
            {
                var station = FindRadioStation(root, id);
                return (
                    Id: id,
                    Title: ReadJsonText(station, "name", "Name"),
                    InLibrary: ReadJsonBoolean(station, "isInLibrary", "IsInLibrary"),
                    Station: station);
            }).ToArray();
            var inLibrary = rows.Any(row => !row.InLibrary);
            var changed = 0;
            foreach (var row in rows)
            {
                if (row.InLibrary == inLibrary) continue;
                row.Station["isInLibrary"] = inLibrary;
                if (!inLibrary) row.Station["isFavorite"] = false;
                changed++;
            }
            if (changed > 0) WriteState(root);

            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var libraryCustom = ReadOrder(connection, transaction, "library_custom_order", "radio");
            var libraryAdded = ReadOrder(connection, transaction, "library_added_order", "radio");
            var favoriteCustom = ReadOrder(connection, transaction, "favorite_order", "radio");
            var favoriteAdded = ReadOrder(connection, transaction, "favorite_added_order", "radio");
            foreach (var row in rows)
            {
                SetOrderedMembership(libraryCustom, row.Id, inLibrary);
                if (!inLibrary)
                {
                    SetOrderedMembership(libraryAdded, row.Id, false);
                    SetOrderedMembership(favoriteCustom, row.Id, false);
                    SetOrderedMembership(favoriteAdded, row.Id, false);
                }
            }
            if (inLibrary)
            {
                foreach (var row in rows.Reverse())
                    SetOrderedMembership(libraryAdded, row.Id, true);
            }
            RewriteOrder(connection, transaction, "library_custom_order", "radio", libraryCustom);
            RewriteOrder(connection, transaction, "library_added_order", "radio", libraryAdded);
            RewriteOrder(connection, transaction, "favorite_order", "radio", favoriteCustom);
            RewriteOrder(connection, transaction, "favorite_added_order", "radio", favoriteAdded);
            TouchMetadata(connection, transaction);
            transaction.Commit();
            return new
            {
                inLibrary,
                requested = rows.Length,
                changed,
                titles = rows.Select(row => row.Title).ToArray()
            };
        }
    }

    public object SetRadioCollectionSortMode(string requestedView, CollectionSortMode mode)
    {
        var (view, profileView) = requestedView.Trim().ToLowerInvariant() switch
        {
            "library" => ("library", "Biblioteka"),
            "favorites" => ("favorites", "Ulubione"),
            _ => throw new LiteRequestException(
                "Sortowanie jest dostępne w Bibliotece i Ulubionych Radia.")
        };
        EnsureMayWrite();
        lock (_gate)
        {
            var root = ReadState();
            var navigation = EnsureObject(root, "sessionNavigation");
            var sessions = EnsureObject(navigation, "sessions");
            var radio = EnsureObject(sessions, "radio");
            var modes = EnsureObject(radio, "collectionSortModes");
            modes[profileView] = mode.ToString();
            WriteState(root);
            var label = mode switch
            {
                CollectionSortMode.Alphabetical => "alfabetycznie",
                CollectionSortMode.Custom => "kolejność własna",
                _ => "według dodania, najnowsze najpierw"
            };
            return new
            {
                view,
                sortMode = mode.ToString(),
                message = $"Radio — {profileView}: {label}"
            };
        }
    }

    /// <summary>
    /// Przesuwa ciągły blok stacji w ręcznej kolejności Biblioteki albo
    /// Ulubionych. Widoczna kolejność pochodzi z dokładnie tego widoku, który
    /// obsługuje klawiaturę; niewidoczne lub chwilowo niedostępne wpisy
    /// zachowują swoje miejsca w trwałej tabeli.
    /// </summary>
    public object MoveRadioCollectionItems(
        string requestedView,
        IReadOnlyList<string> selectedItemIds,
        IReadOnlyList<string> visibleItemIds,
        int direction)
    {
        var (view, table) = RadioCustomOrder(requestedView);
        var selected = CleanDistinctIds(
            selectedItemIds,
            "Wybierz stację do przeniesienia.");
        var visible = CleanDistinctIds(
            visibleItemIds,
            "Lista stacji jest pusta.");
        if (selected.Length > 10_000 || visible.Length > 10_000)
            throw new LiteRequestException("Lista może zawierać najwyżej 10000 stacji.");
        if (direction is not (-1 or 1))
            throw new LiteRequestException("Nieprawidłowy kierunek zmiany kolejności.");

        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var order = ReadOrder(connection, transaction, table, "radio");
            foreach (var itemId in visible)
                SetOrderedMembership(order, itemId, true);
            var result = LocalLibraryManualOrder.MoveVisibleBlock(
                order,
                visible,
                selected,
                direction);
            if (result == ManualOrderMoveResult.Moved)
            {
                RewriteOrder(connection, transaction, table, "radio", order);
                TouchMetadata(connection, transaction);
            }
            transaction.Commit();
            return new
            {
                view,
                status = result switch
                {
                    ManualOrderMoveResult.Moved => "moved",
                    ManualOrderMoveResult.Boundary => "boundary",
                    ManualOrderMoveResult.NonContiguousSelection => "nonContiguous",
                    _ => "invalid"
                },
                moved = result == ManualOrderMoveResult.Moved,
                count = selected.Length
            };
        }
    }

    /// <summary>
    /// Kończy Ctrl+X/Ctrl+V, wstawiając stacje bezpośrednio przed wskazanym
    /// elementem w jednej transakcji SQLite.
    /// </summary>
    public object PlaceRadioCollectionItemsBefore(
        string requestedView,
        IReadOnlyList<string> selectedItemIds,
        IReadOnlyList<string> visibleItemIds,
        string targetItemId)
    {
        var (view, table) = RadioCustomOrder(requestedView);
        var selected = CleanDistinctIds(
            selectedItemIds,
            "Wybierz stację do przeniesienia.");
        var visible = CleanDistinctIds(
            visibleItemIds,
            "Lista stacji jest pusta.");
        var target = CleanRequiredText(
            targetItemId,
            "Wybierz stację, przed którą chcesz przenieść zaznaczenie.");
        if (selected.Length > 10_000 || visible.Length > 10_000)
            throw new LiteRequestException("Lista może zawierać najwyżej 10000 stacji.");

        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var order = ReadOrder(connection, transaction, table, "radio");
            foreach (var itemId in visible)
                SetOrderedMembership(order, itemId, true);
            var result = LocalLibraryManualOrder.PlaceItemsBefore(
                order,
                selected,
                target);
            if (result == ManualOrderPlacementResult.Moved)
            {
                RewriteOrder(connection, transaction, table, "radio", order);
                TouchMetadata(connection, transaction);
            }
            transaction.Commit();
            return new
            {
                view,
                status = result switch
                {
                    ManualOrderPlacementResult.Moved => "moved",
                    ManualOrderPlacementResult.TargetInSelection => "targetInSelection",
                    ManualOrderPlacementResult.TargetMissing => "targetMissing",
                    ManualOrderPlacementResult.Unchanged => "unchanged",
                    _ => "invalid"
                },
                moved = result == ManualOrderPlacementResult.Moved,
                count = selected.Length
            };
        }
    }

    private static (string View, string Table) RadioCustomOrder(
        string requestedView) => requestedView.Trim().ToLowerInvariant() switch
        {
            "library" => ("library", "library_custom_order"),
            "favorites" => ("favorites", "favorite_order"),
            _ => throw new LiteRequestException(
                "Ręczna kolejność jest dostępna w Bibliotece i Ulubionych Radia.")
        };

    private object ToggleLocalFavorites(
        IReadOnlyList<string> itemIds,
        IReadOnlyList<string>? sourcePaths)
    {
        using var connection = OpenDatabase(_libraryDatabasePath);
        using var transaction = connection.BeginTransaction();
        var rows = new List<(string Id, string Title, bool Favorite, bool InLibrary)>();
        for (var index = 0; index < itemIds.Count; index++)
        {
            var id = itemIds[index];
            using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = "SELECT id, title, is_favorite, is_in_library "
                + "FROM local_items WHERE id = $id;";
            read.Parameters.AddWithValue("$id", id);
            using var reader = read.ExecuteReader();
            if (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetString(1),
                    reader.GetInt64(2) != 0, reader.GetInt64(3) != 0));
                continue;
            }
            reader.Close();

            var path = sourcePaths is not null && index < sourcePaths.Count
                ? sourcePaths[index]
                : null;
            if (!string.IsNullOrWhiteSpace(path))
            {
                string fullPath;
                try { fullPath = Path.GetFullPath(path); }
                catch (Exception exception) when (exception is ArgumentException
                                                  or NotSupportedException
                                                  or PathTooLongException)
                {
                    throw new LiteRequestException("Ścieżka wybranego pliku jest nieprawidłowa.");
                }
                using var byPath = connection.CreateCommand();
                byPath.Transaction = transaction;
                byPath.CommandText = "SELECT id, title, is_favorite, is_in_library "
                    + "FROM local_items WHERE path = $path COLLATE NOCASE LIMIT 1;";
                byPath.Parameters.AddWithValue("$path", fullPath);
                using var pathReader = byPath.ExecuteReader();
                if (pathReader.Read())
                {
                    var resolved = (
                        Id: pathReader.GetString(0),
                        Title: pathReader.GetString(1),
                        Favorite: pathReader.GetInt64(2) != 0,
                        InLibrary: pathReader.GetInt64(3) != 0);
                    if (!rows.Any(row => string.Equals(
                            row.Id, resolved.Id, StringComparison.Ordinal)))
                    {
                        rows.Add(resolved);
                    }
                    continue;
                }
            }
            throw new LiteRequestException(
                "Wybranego pliku nie ma w Bibliotece. Ctrl+O tylko otwiera folder. "
                + "Dodaj folder poleceniem Foldery Biblioteki, Ctrl+F5.");
        }

        var favorite = rows.Any(row => !row.Favorite);
        var changed = 0;
        var customOrder = ReadOrder(connection, transaction, "favorite_order", "local");
        var addedOrder = ReadOrder(connection, transaction, "favorite_added_order", "local");
        var libraryCustom = ReadOrder(connection, transaction, "library_custom_order", "local");
        var libraryAdded = ReadOrder(connection, transaction, "library_added_order", "local");
        foreach (var row in rows)
        {
            if (row.Favorite != favorite || (favorite && !row.InLibrary))
            {
                Execute(connection, transaction,
                    "UPDATE local_items SET is_favorite = $favorite, "
                    + "is_in_library = CASE WHEN $favorite THEN 1 ELSE is_in_library END "
                    + "WHERE id = $id;",
                    ("$favorite", favorite), ("$id", row.Id));
                changed++;
            }
            SetOrderedMembership(customOrder, row.Id, favorite);
            SetOrderedMembership(addedOrder, row.Id, favorite);
            if (favorite) SetOrderedMembership(libraryCustom, row.Id, true);
        }
        if (favorite)
        {
            for (var index = rows.Count - 1; index >= 0; index--)
                SetOrderedMembership(libraryAdded, rows[index].Id, true);
        }
        RewriteOrder(connection, transaction, "favorite_order", "local", customOrder);
        RewriteOrder(connection, transaction, "favorite_added_order", "local", addedOrder);
        RewriteOrder(connection, transaction, "library_custom_order", "local", libraryCustom);
        RewriteOrder(connection, transaction, "library_added_order", "local", libraryAdded);
        TouchMetadata(connection, transaction);
        transaction.Commit();
        return new
        {
            favorite,
            requested = rows.Count,
            changed,
            titles = rows.Select(row => row.Title).ToArray()
        };
    }

    private object ToggleRadioFavorites(IReadOnlyList<string> itemIds)
    {
        var root = ReadState();
        var rows = itemIds.Select(id =>
        {
            var station = FindRadioStation(root, id);
            return (
                Id: id,
                Title: ReadJsonText(station, "name", "Name"),
                Favorite: ReadJsonBoolean(station, "isFavorite", "IsFavorite"),
                Station: station);
        }).ToArray();
        var favorite = rows.Any(row => !row.Favorite);
        var changed = 0;
        foreach (var row in rows)
        {
            if (row.Favorite == favorite) continue;
            row.Station["isFavorite"] = favorite;
            if (favorite) row.Station["isInLibrary"] = true;
            changed++;
        }
        WriteState(root);

        using var connection = OpenDatabase(_libraryDatabasePath);
        using var transaction = connection.BeginTransaction();
        var customOrder = ReadOrder(connection, transaction, "favorite_order", "radio");
        var addedOrder = ReadOrder(connection, transaction, "favorite_added_order", "radio");
        foreach (var row in rows)
        {
            SetOrderedMembership(customOrder, row.Id, favorite);
            SetOrderedMembership(addedOrder, row.Id, favorite);
        }
        RewriteOrder(connection, transaction, "favorite_order", "radio", customOrder);
        RewriteOrder(connection, transaction, "favorite_added_order", "radio", addedOrder);
        TouchMetadata(connection, transaction);
        transaction.Commit();
        return new
        {
            favorite,
            requested = rows.Length,
            changed,
            titles = rows.Select(row => row.Title).ToArray()
        };
    }

    /// <summary>
    /// Migawka playlist jednej sesji dla natywnego okna wxPython. Id oraz
    /// członkostwo pozostają danymi wewnętrznymi; frontend buduje dla NVDA
    /// wyłącznie nazwę, jawny stan i liczbę elementów.
    /// </summary>
    public object GetPlaylists(string sessionId)
    {
        var normalizedSession = NormalizePlaylistSession(sessionId);
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            var entries = new List<LitePlaylistMutation>();
            using (var read = connection.CreateCommand())
            {
                read.CommandText =
                    "SELECT id, name, created_utc_ticks FROM playlists "
                    + "WHERE session_id = $session COLLATE NOCASE ORDER BY ordinal;";
                read.Parameters.AddWithValue("$session", normalizedSession);
                using var reader = read.ExecuteReader();
                while (reader.Read())
                {
                    entries.Add(new LitePlaylistMutation(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetInt64(2),
                        []));
                }
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var itemIds = new List<string>();
                using var items = connection.CreateCommand();
                items.CommandText =
                    "SELECT item_id FROM playlist_items WHERE playlist_id = $playlist ORDER BY ordinal;";
                items.Parameters.AddWithValue("$playlist", entry.Id);
                using var reader = items.ExecuteReader();
                while (reader.Read()) itemIds.Add(reader.GetString(0));
                entries[index] = entry with { ItemIds = itemIds };
            }

            return new
            {
                sessionId = normalizedSession,
                playlists = entries.Select(entry => new
                {
                    playlistId = entry.Id,
                    name = entry.Name,
                    createdUtcTicks = entry.CreatedUtcTicks,
                    itemIds = entry.ItemIds
                }).ToArray()
            };
        }
    }

    /// <summary>
    /// Atomowo zastępuje playlisty tylko jednej sesji, dokładnie jak
    /// <c>PlaylistIndex.ReplaceSession</c> w głównym AMC. Dzięki temu Nowa,
    /// F2, Delete i zmiany członkostwa pozostają lokalne w dialogu aż do
    /// przycisku Zapisz; Anuluj nie wykonuje żadnej częściowej zmiany.
    /// </summary>
    public object ReplacePlaylists(
        string sessionId,
        IReadOnlyList<LitePlaylistMutation> replacements)
    {
        var normalizedSession = NormalizePlaylistSession(sessionId);
        if (replacements.Count > 500)
            throw new LiteRequestException("Jedna sesja może zawierać najwyżej 500 playlist.");
        EnsureMayWrite();
        lock (_gate)
        {
            var normalized = new List<LitePlaylistMutation>(replacements.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var compare = CultureInfo.GetCultureInfo("pl-PL").CompareInfo;
            foreach (var source in replacements)
            {
                var id = CleanRequiredText(source.Id, "Playlista nie ma identyfikatora.");
                if (id.Length > 200 || !ids.Add(id))
                    throw new LiteRequestException("Lista playlist zawiera nieprawidłowy albo powtórzony wpis.");
                var name = CleanRequiredText(source.Name, "Nazwa playlisty jest pusta.");
                if (name.Length > 200)
                    throw new LiteRequestException("Nazwa playlisty może mieć najwyżej 200 znaków.");
                if (normalized.Any(entry => compare.Compare(
                        entry.Name,
                        name,
                        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0))
                {
                    throw new LiteRequestException($"Playlista „{name}” już istnieje w tej sesji.");
                }
                if (source.ItemIds.Count > 10_000)
                    throw new LiteRequestException("Jedna playlista może zawierać najwyżej 10000 elementów.");
                // Pusta playlista jest prawidłowa. Dla niepustej listy ten sam
                // normalizator usuwa białe i powtórzone identyfikatory.
                var itemIds = source.ItemIds.Count == 0
                    ? []
                    : CleanDistinctIds(source.ItemIds, "Playlista może być pusta.");
                normalized.Add(new LitePlaylistMutation(
                    id,
                    name,
                    source.CreatedUtcTicks > 0 ? source.CreatedUtcTicks : DateTime.UtcNow.Ticks,
                    itemIds));
            }

            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            foreach (var entry in normalized)
            {
                using var collision = connection.CreateCommand();
                collision.Transaction = transaction;
                collision.CommandText =
                    "SELECT session_id FROM playlists WHERE id = $id AND session_id != $session COLLATE NOCASE LIMIT 1;";
                collision.Parameters.AddWithValue("$id", entry.Id);
                collision.Parameters.AddWithValue("$session", normalizedSession);
                if (collision.ExecuteScalar() is not null)
                    throw new LiteRequestException("Identyfikator nowej playlisty koliduje z inną sesją.");
            }

            Execute(connection, transaction,
                "DELETE FROM playlist_items WHERE playlist_id IN "
                + "(SELECT id FROM playlists WHERE session_id = $session COLLATE NOCASE);",
                ("$session", normalizedSession));
            Execute(connection, transaction,
                "DELETE FROM playlists WHERE session_id = $session COLLATE NOCASE;",
                ("$session", normalizedSession));
            for (var ordinal = 0; ordinal < normalized.Count; ordinal++)
            {
                var entry = normalized[ordinal];
                Execute(connection, transaction,
                    "INSERT INTO playlists(id, session_id, ordinal, name, created_utc_ticks) "
                    + "VALUES($id, $session, $ordinal, $name, $created);",
                    ("$id", entry.Id), ("$session", normalizedSession),
                    ("$ordinal", ordinal), ("$name", entry.Name),
                    ("$created", entry.CreatedUtcTicks));
                for (var itemOrdinal = 0; itemOrdinal < entry.ItemIds.Count; itemOrdinal++)
                {
                    Execute(connection, transaction,
                        "INSERT INTO playlist_items(playlist_id, ordinal, item_id) "
                        + "VALUES($playlist, $ordinal, $item);",
                        ("$playlist", entry.Id), ("$ordinal", itemOrdinal),
                        ("$item", entry.ItemIds[itemOrdinal]));
                }
            }
            TouchMetadata(connection, transaction);
            transaction.Commit();
            return new
            {
                sessionId = normalizedSession,
                saved = normalized.Count,
                items = normalized.Sum(entry => entry.ItemIds.Count)
            };
        }
    }

    /// <summary>
    /// Przesuwa widoczny, ciągły blok o jedną pozycję. Frontend przekazuje
    /// kolejność widoczną, ponieważ zapis playlisty może zawierać także
    /// chwilowo niedostępne elementy. Algorytm Core zachowuje ich miejsca.
    /// </summary>
    public object MovePlaylistItems(
        string sessionId,
        string playlistId,
        IReadOnlyList<string> selectedItemIds,
        IReadOnlyList<string> visibleItemIds,
        int direction)
    {
        var normalizedSession = NormalizePlaylistSession(sessionId);
        var normalizedPlaylistId = CleanRequiredText(
            playlistId,
            "Nie wskazano playlisty do zmiany kolejności.");
        var selected = CleanDistinctIds(
            selectedItemIds,
            "Wybierz element do przeniesienia.");
        var visible = CleanDistinctIds(
            visibleItemIds,
            "Otwarta playlista nie zawiera widocznych elementów.");
        if (selected.Length > 10_000 || visible.Length > 10_000)
            throw new LiteRequestException("Playlista może zawierać najwyżej 10000 elementów.");
        if (direction is not (-1 or 1))
            throw new LiteRequestException("Nieprawidłowy kierunek zmiany kolejności.");

        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var order = ReadPlaylistOrder(
                connection,
                transaction,
                normalizedSession,
                normalizedPlaylistId);
            var result = LocalLibraryManualOrder.MoveVisibleBlock(
                order,
                visible,
                selected,
                direction);
            if (result == ManualOrderMoveResult.Moved)
            {
                RewritePlaylistOrder(connection, transaction, normalizedPlaylistId, order);
                TouchMetadata(connection, transaction);
            }
            transaction.Commit();
            return new
            {
                sessionId = normalizedSession,
                status = result switch
                {
                    ManualOrderMoveResult.Moved => "moved",
                    ManualOrderMoveResult.Boundary => "boundary",
                    ManualOrderMoveResult.NonContiguousSelection => "nonContiguous",
                    _ => "invalid"
                },
                moved = result == ManualOrderMoveResult.Moved,
                count = selected.Length
            };
        }
    }

    /// <summary>
    /// Kończy przenoszenie rozpoczęte przez Ctrl+X i umieszcza zaznaczone
    /// elementy bezpośrednio przed wskazanym celem, zachowując ich kolejność
    /// z playlisty. Cała operacja jest jedną transakcją SQLite.
    /// </summary>
    public object PlacePlaylistItemsBefore(
        string sessionId,
        string playlistId,
        IReadOnlyList<string> selectedItemIds,
        string targetItemId)
    {
        var normalizedSession = NormalizePlaylistSession(sessionId);
        var normalizedPlaylistId = CleanRequiredText(
            playlistId,
            "Nie wskazano playlisty do zmiany kolejności.");
        var selected = CleanDistinctIds(
            selectedItemIds,
            "Wybierz element do przeniesienia.");
        var target = CleanRequiredText(
            targetItemId,
            "Wybierz element, przed którym chcesz przenieść zaznaczenie.");
        if (selected.Length > 10_000)
            throw new LiteRequestException("Playlista może zawierać najwyżej 10000 elementów.");

        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var order = ReadPlaylistOrder(
                connection,
                transaction,
                normalizedSession,
                normalizedPlaylistId);
            var result = LocalLibraryManualOrder.PlaceItemsBefore(order, selected, target);
            if (result == ManualOrderPlacementResult.Moved)
            {
                RewritePlaylistOrder(connection, transaction, normalizedPlaylistId, order);
                TouchMetadata(connection, transaction);
            }
            transaction.Commit();
            return new
            {
                sessionId = normalizedSession,
                status = result switch
                {
                    ManualOrderPlacementResult.Moved => "moved",
                    ManualOrderPlacementResult.TargetInSelection => "targetInSelection",
                    ManualOrderPlacementResult.TargetMissing => "targetMissing",
                    ManualOrderPlacementResult.Unchanged => "unchanged",
                    _ => "invalid"
                },
                moved = result == ManualOrderPlacementResult.Moved,
                count = selected.Length
            };
        }
    }

    public object Remove(
        string sessionId,
        string view,
        IReadOnlyList<string> itemIds,
        string? playlistId = null)
    {
        var ids = itemIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) throw new LiteRequestException("Brak elementu do usunięcia.");
        EnsureMayWrite();
        lock (_gate)
        {
            if (string.Equals(sessionId, "radio", StringComparison.Ordinal))
                return RemoveRadio(view, ids, playlistId);
            if (string.Equals(sessionId, "podcasts", StringComparison.Ordinal))
                return RemovePodcasts(view, ids, playlistId);
            if (!string.Equals(sessionId, "files", StringComparison.Ordinal))
                throw new LiteRequestException("Usuwanie w tej sesji nie jest jeszcze dostępne.");
            return RemoveLocal(view, ids, playlistId);
        }
    }

    public LocalFileRecycleResult RecycleLocalFiles(
        IReadOnlyList<string> itemIds,
        IReadOnlyList<string>? sourcePaths = null)
    {
        var ids = itemIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) throw new LiteRequestException("Brak pliku do przeniesienia do Kosza.");
        EnsureMayWrite();
        lock (_gate)
        {
            using var connection = OpenDatabase(_libraryDatabasePath);
            var items = new List<LocalItem>();
            foreach (var path in (sourcePaths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var item = ReadLocalItemByPath(connection, null, path);
                if (!items.Any(existing => string.Equals(
                        existing.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
                    items.Add(item);
            }
            foreach (var id in ids)
            {
                if (items.Any(item => string.Equals(item.Id, id, StringComparison.Ordinal)))
                    continue;
                // Gdy podano ścieżki, pseudonim wiersza Historii nagrywania
                // nie musi istnieć w local_items. Sięgamy po Id tylko wtedy,
                // gdy żadna ścieżka nie opisała jeszcze tego żądania.
                if (sourcePaths is { Count: > 0 } && items.Count >= ids.Length)
                    break;
                items.Add(ReadLocalItem(connection, null, id));
            }
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
                foreach (var item in removed)
                {
                    if (!string.IsNullOrWhiteSpace(item.Id))
                        RemoveLocalRecord(connection, transaction, item.Id);
                }
                transaction.Commit();
                RemoveRecordingHistoryPaths(removed.Select(item => item.Path));
            }
            return new LocalFileRecycleResult(
                removed.Select(item => new LocalFileMutationItem(
                    item.Id, item.Title, item.Path)).ToArray(),
                failures.ToArray());
        }
    }

    public int ForgetRecordingHistoryPaths(IReadOnlyList<string> sourcePaths)
    {
        EnsureMayWrite();
        lock (_gate)
        {
            return RemoveRecordingHistoryPaths(sourcePaths);
        }
    }

    private object RemoveLocal(string view, IReadOnlyList<string> itemIds, string? playlistId)
    {
        using var connection = OpenDatabase(_libraryDatabasePath);
        using var transaction = connection.BeginTransaction();
        if (string.Equals(view, "playlistContents", StringComparison.Ordinal))
        {
            var normalizedPlaylistId = CleanRequiredText(
                playlistId ?? string.Empty, "Nie wskazano otwartej playlisty.");
            var order = ReadPlaylistOrder(connection, transaction, "local", normalizedPlaylistId);
            var removed = order.RemoveAll(itemIds.Contains);
            if (removed > 0)
            {
                RewritePlaylistOrder(connection, transaction, normalizedPlaylistId, order);
                TouchMetadata(connection, transaction);
            }
            transaction.Commit();
            return new { removedCount = removed };
        }
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

    private object RemoveRadio(string view, IReadOnlyList<string> itemIds, string? playlistId)
    {
        if (string.Equals(view, "playlistContents", StringComparison.Ordinal))
        {
            var normalizedPlaylistId = CleanRequiredText(
                playlistId ?? string.Empty, "Nie wskazano otwartej playlisty.");
            using var connection = OpenDatabase(_libraryDatabasePath);
            using var transaction = connection.BeginTransaction();
            var order = ReadPlaylistOrder(connection, transaction, "radio", normalizedPlaylistId);
            var removed = order.RemoveAll(itemIds.Contains);
            if (removed > 0)
            {
                RewritePlaylistOrder(connection, transaction, normalizedPlaylistId, order);
                TouchMetadata(connection, transaction);
            }
            transaction.Commit();
            return new { removedCount = removed };
        }
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

    private object RemovePodcasts(
        string view,
        IReadOnlyList<string> itemIds,
        string? playlistId)
    {
        if (string.Equals(view, "podcastEpisodes", StringComparison.Ordinal))
            return RemovePodcastEpisodes(itemIds);
        if (string.Equals(view, "podcastPlaylistContents", StringComparison.Ordinal))
        {
            var normalizedPlaylistId = CleanRequiredText(
                playlistId ?? string.Empty,
                "Nie wskazano otwartej playlisty.");
            using var playlistConnection = OpenDatabase(_libraryDatabasePath);
            using var playlistTransaction = playlistConnection.BeginTransaction();
            var order = ReadPlaylistOrder(
                playlistConnection,
                playlistTransaction,
                "podcasts",
                normalizedPlaylistId);
            var removedFromPlaylist = order.RemoveAll(itemIds.Contains);
            if (removedFromPlaylist > 0)
            {
                RewritePlaylistOrder(
                    playlistConnection,
                    playlistTransaction,
                    normalizedPlaylistId,
                    order);
                TouchMetadata(playlistConnection, playlistTransaction);
            }
            playlistTransaction.Commit();
            return new { removedCount = removedFromPlaylist };
        }
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
        if (string.Equals(view, "podcastQueue", StringComparison.Ordinal))
        {
            // Jedna transakcja obejmuje podcasts.db oraz dolaczony library.db,
            // aby flagi odcinka i trzy listy porzadku nie mogly sie rozjechac.
            using var queueConnection = OpenDatabase(_podcastsDatabasePath);
            using (var attach = queueConnection.CreateCommand())
            {
                attach.CommandText = "ATTACH DATABASE $path AS library;";
                attach.Parameters.AddWithValue("$path", _libraryDatabasePath);
                attach.ExecuteNonQuery();
            }
            using var queueTransaction = queueConnection.BeginTransaction();
            var removedFromQueue = 0;
            foreach (var id in itemIds)
            {
                string? payloadText;
                using (var read = queueConnection.CreateCommand())
                {
                    read.Transaction = queueTransaction;
                    read.CommandText = "SELECT payload_json FROM podcast_episodes WHERE id = $id;";
                    read.Parameters.AddWithValue("$id", id);
                    payloadText = read.ExecuteScalar() as string;
                }
                if (payloadText is null) continue;
                var payload = ParseObject(payloadText, "Dane odcinka są uszkodzone.");
                payload["IsInQueue"] = false;
                payload["IsPlayNext"] = false;
                Execute(queueConnection, queueTransaction,
                    "UPDATE podcast_episodes SET is_in_queue = 0, is_play_next = 0, payload_json = $payload WHERE id = $id;",
                    ("$payload", payload.ToJsonString()), ("$id", id));
                foreach (var table in new[]
                         {
                             "queue_order", "queue_regular_order", "queue_play_next_order"
                         })
                {
                    Execute(queueConnection, queueTransaction,
                        $"DELETE FROM library.{table} WHERE session_id = 'podcasts' COLLATE NOCASE AND item_id = $id;",
                        ("$id", id));
                }
                removedFromQueue++;
            }
            queueTransaction.Commit();
            return new { removedCount = removedFromQueue };
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

    private object RemovePodcastEpisodes(IReadOnlyList<string> itemIds)
    {
        using var connection = OpenDatabase(_podcastsDatabasePath);
        using (var attach = connection.CreateCommand())
        {
            attach.CommandText = "ATTACH DATABASE $path AS library;";
            attach.Parameters.AddWithValue("$path", _libraryDatabasePath);
            attach.ExecuteNonQuery();
        }
        using var transaction = connection.BeginTransaction();
        Execute(
            connection,
            transaction,
            "CREATE TABLE IF NOT EXISTS podcast_removed_episodes ("
            + "id TEXT PRIMARY KEY, removed_utc_ticks INTEGER NOT NULL);");
        var removed = new List<(string Id, string Title, string SubscriptionId)>();
        foreach (var id in itemIds)
        {
            using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText =
                "SELECT id, title, subscription_id FROM podcast_episodes WHERE id = $id;";
            read.Parameters.AddWithValue("$id", id);
            using var reader = read.ExecuteReader();
            if (!reader.Read()) continue;
            removed.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }
        foreach (var item in removed)
        {
            Execute(
                connection,
                transaction,
                "INSERT INTO podcast_removed_episodes(id, removed_utc_ticks) "
                + "VALUES($id, $ticks) ON CONFLICT(id) DO UPDATE SET "
                + "removed_utc_ticks = excluded.removed_utc_ticks;",
                ("$id", item.Id), ("$ticks", DateTime.UtcNow.Ticks));
            Execute(
                connection,
                transaction,
                "DELETE FROM podcast_episodes WHERE id = $id;",
                ("$id", item.Id));
            Execute(
                connection,
                transaction,
                "UPDATE podcast_state SET current_item_id = NULL "
                + "WHERE singleton = 1 AND current_item_id = $id;",
                ("$id", item.Id));
            foreach (var table in new[]
                     {
                         "favorite_order", "favorite_added_order", "queue_order",
                         "queue_regular_order", "queue_play_next_order"
                     })
            {
                Execute(
                    connection,
                    transaction,
                    $"DELETE FROM library.{table} "
                    + "WHERE session_id = 'podcasts' COLLATE NOCASE AND item_id = $id;",
                    ("$id", item.Id));
            }
            Execute(
                connection,
                transaction,
                "DELETE FROM library.playlist_items WHERE item_id = $id;",
                ("$id", item.Id));
            Execute(
                connection,
                transaction,
                "DELETE FROM library.bookmarks "
                + "WHERE session_id = 'podcasts' COLLATE NOCASE AND item_id = $id;",
                ("$id", item.Id));
            Execute(
                connection,
                transaction,
                "DELETE FROM library.playback_history "
                + "WHERE session_id = 'podcasts' COLLATE NOCASE AND item_id = $id;",
                ("$id", item.Id));
        }
        if (removed.Any(item => string.Equals(
                item.SubscriptionId,
                PublicInternetMediaCollections.SavedId,
                StringComparison.Ordinal)))
        {
            Execute(
                connection,
                transaction,
                "DELETE FROM podcast_subscriptions WHERE id = $id AND NOT EXISTS "
                + "(SELECT 1 FROM podcast_episodes WHERE subscription_id = $id);",
                ("$id", PublicInternetMediaCollections.SavedId));
        }
        transaction.Commit();
        return new
        {
            removedCount = removed.Count,
            titles = removed.Select(item => item.Title).ToArray()
        };
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
        var fullOldPath = Path.GetFullPath(oldPath);
        var changed = false;
        foreach (var entry in history.OfType<JsonObject>())
        {
            var path = ReadJsonText(entry, "path", "Path");
            if (string.IsNullOrWhiteSpace(path))
                path = ReadJsonText(entry, "outputPath", "OutputPath");
            // Historia może legalnie zawierać zatrzymane albo nieudane
            // nagranie bez utworzonego pliku. Taki wpis ma pustą ścieżkę i
            // nie dotyczy bieżącej zmiany nazwy. Dotychczas GetFullPath("")
            // przerywał całą transakcję już po File.Move; warstwa wyżej
            // cofała plik i użytkownik słyszał tylko „The path is empty”.
            // Pomijamy również pojedynczy uszkodzony wpis zamiast blokować
            // poprawny, istniejący plik wybrany przez użytkownika.
            if (string.IsNullOrWhiteSpace(path)) continue;
            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException
                                              or NotSupportedException
                                              or PathTooLongException)
            {
                continue;
            }
            if (!string.Equals(fullPath, fullOldPath, StringComparison.OrdinalIgnoreCase))
                continue;
            entry["path"] = newPath;
            changed = true;
        }
        if (changed) WriteState(root);
    }

    private int RemoveRecordingHistoryPaths(IEnumerable<string> sourcePaths)
    {
        if (!File.Exists(_statePath)) return 0;
        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (paths.Count == 0) return 0;
        var root = ReadState();
        if (root["radio"]?["recordingHistory"] is not JsonArray history) return 0;
        var removed = 0;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            if (history[index] is not JsonObject entry) continue;
            var path = ReadJsonText(entry, "path", "Path");
            if (string.IsNullOrWhiteSpace(path))
                path = ReadJsonText(entry, "outputPath", "OutputPath");
            if (string.IsNullOrWhiteSpace(path)) continue;
            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException
                                              or NotSupportedException
                                              or PathTooLongException)
            {
                continue;
            }
            if (!paths.Contains(fullPath)) continue;
            history.RemoveAt(index);
            removed++;
        }
        if (removed > 0) WriteState(root);
        return removed;
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

    private static JsonObject EnsureObject(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject current) return current;
        var created = new JsonObject();
        parent[name] = created;
        return created;
    }

    private static object LocalFolderPayload(
        IReadOnlyList<LocalFolderSourceStatus> sources,
        string? message,
        string? selectedSourceId) => new
    {
        message,
        selectedSourceId,
        sources = sources.Select(source => new
        {
            id = source.Id,
            label = source.Label,
            name = source.DisplayName,
            path = source.Path,
            reachable = source.IsReachable,
            activeCount = source.ActiveItemCount,
            unavailableCount = source.UnavailableItemCount,
            excludedCount = source.ExcludedItemCount
        }).ToArray()
    };

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

    private static bool ReadJsonBoolean(JsonObject value, string first, string second)
    {
        foreach (var name in new[] { first, second })
        {
            if (value[name] is JsonValue json && json.TryGetValue<bool>(out var result))
                return result;
        }
        return false;
    }

    private static string CleanRequiredText(string value, string error)
    {
        var cleaned = value.Trim();
        if (cleaned.Length == 0) throw new LiteRequestException(error);
        return cleaned;
    }

    private static string NormalizePlaylistSession(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "files" or "local" => "local",
            "radio" => "radio",
            "podcasts" => "podcasts",
            _ => throw new LiteRequestException("Playlisty nie są dostępne w tej sesji.")
        };

    private static string[] CleanDistinctIds(
        IReadOnlyList<string> values,
        string emptyError)
    {
        var ids = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0) throw new LiteRequestException(emptyError);
        return ids;
    }

    private static List<string> ReadAttachedOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT item_id FROM library.{table} WHERE session_id = 'podcasts' COLLATE NOCASE ORDER BY ordinal;";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (seen.Add(id)) result.Add(id);
        }
        return result;
    }

    private static List<string> ReadOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string sessionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT item_id FROM {table} WHERE session_id = $session COLLATE NOCASE ORDER BY ordinal;";
        command.Parameters.AddWithValue("$session", sessionId);
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (seen.Add(id)) result.Add(id);
        }
        return result;
    }

    private static List<string> ReadPlaylistOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        string playlistId)
    {
        using (var exists = connection.CreateCommand())
        {
            exists.Transaction = transaction;
            exists.CommandText =
                "SELECT 1 FROM playlists WHERE id = $playlist "
                + "AND session_id = $session COLLATE NOCASE LIMIT 1;";
            exists.Parameters.AddWithValue("$playlist", playlistId);
            exists.Parameters.AddWithValue("$session", sessionId);
            if (exists.ExecuteScalar() is null)
                throw new LiteRequestException("Nie znaleziono otwartej playlisty.");
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT item_id FROM playlist_items "
            + "WHERE playlist_id = $playlist ORDER BY ordinal;";
        command.Parameters.AddWithValue("$playlist", playlistId);
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (seen.Add(id)) result.Add(id);
        }
        return result;
    }

    private static void RewritePlaylistOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playlistId,
        IReadOnlyList<string> itemIds)
    {
        Execute(connection, transaction,
            "DELETE FROM playlist_items WHERE playlist_id = $playlist;",
            ("$playlist", playlistId));
        for (var ordinal = 0; ordinal < itemIds.Count; ordinal++)
        {
            Execute(connection, transaction,
                "INSERT INTO playlist_items(playlist_id, ordinal, item_id) "
                + "VALUES($playlist, $ordinal, $id);",
                ("$playlist", playlistId), ("$ordinal", ordinal),
                ("$id", itemIds[ordinal]));
        }
    }

    private static void RewriteOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string sessionId,
        IReadOnlyList<string> itemIds)
    {
        Execute(connection, transaction,
            $"DELETE FROM {table} WHERE session_id = $session COLLATE NOCASE;",
            ("$session", sessionId));
        for (var ordinal = 0; ordinal < itemIds.Count; ordinal++)
        {
            Execute(connection, transaction,
                $"INSERT INTO {table}(session_id, ordinal, item_id) VALUES($session, $ordinal, $id);",
                ("$session", sessionId), ("$ordinal", ordinal), ("$id", itemIds[ordinal]));
        }
    }

    private static void RewriteAttachedOrder(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        IReadOnlyList<string> itemIds)
    {
        Execute(connection, transaction,
            $"DELETE FROM library.{table} WHERE session_id = 'podcasts' COLLATE NOCASE;");
        for (var ordinal = 0; ordinal < itemIds.Count; ordinal++)
        {
            Execute(connection, transaction,
                $"INSERT INTO library.{table}(session_id, ordinal, item_id) VALUES('podcasts', $ordinal, $id);",
                ("$ordinal", ordinal), ("$id", itemIds[ordinal]));
        }
    }

    private static void SetOrderedMembership(List<string> order, string itemId, bool member)
    {
        var present = order.Any(candidate =>
            string.Equals(candidate, itemId, StringComparison.Ordinal));
        if (member)
        {
            if (!present) order.Add(itemId);
            return;
        }
        if (present)
            order.RemoveAll(candidate =>
                string.Equals(candidate, itemId, StringComparison.Ordinal));
    }

    private static void TouchMetadata(
        SqliteConnection connection,
        SqliteTransaction transaction) =>
        Execute(connection, transaction,
            """
            INSERT INTO metadata(key, value) VALUES('last_saved_utc', $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

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

    private static LocalItem ReadLocalItemByPath(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sourcePath)
    {
        string fullPath;
        try { fullPath = Path.GetFullPath(sourcePath); }
        catch (Exception exception) when (exception is ArgumentException
                                          or NotSupportedException
                                          or PathTooLongException)
        {
            throw new LiteRequestException("Ścieżka nagrania jest nieprawidłowa.");
        }
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, title, has_custom_title, path FROM local_items WHERE path = $path COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("$path", fullPath);
        using var reader = command.ExecuteReader();
        if (reader.Read())
            return new LocalItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2) != 0,
                reader.GetString(3));
        // Świeże nagranie może jeszcze nie być w indeksie Biblioteki. Sam
        // istniejący plik nadal da się bezpiecznie przemianować; kolejne
        // skanowanie doda go już pod nową ścieżką.
        return new LocalItem(
            "",
            Path.GetFileNameWithoutExtension(fullPath),
            false,
            fullPath);
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

    internal sealed record LocalFileRenameResult(
        [property: JsonPropertyName("itemId")] string ItemId,
        [property: JsonPropertyName("oldPath")] string OldPath,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("fileName")] string FileName);

    internal sealed record LocalFileMutationItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("path")] string Path);

    internal sealed record LocalFileRecycleResult(
        [property: JsonPropertyName("removed")]
        IReadOnlyList<LocalFileMutationItem> Removed,
        [property: JsonPropertyName("failures")]
        IReadOnlyList<string> Failures);

    internal sealed record LitePlaylistMutation(
        string Id,
        string Name,
        long CreatedUtcTicks,
        IReadOnlyList<string> ItemIds);

    internal sealed record RadioStationEditResult(
        [property: JsonPropertyName("stationId")] string StationId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("streamChanged")] bool StreamChanged);
}
