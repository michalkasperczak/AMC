using System.Diagnostics;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.LiteHost.Protocol;
using Microsoft.Data.Sqlite;

namespace AccessibleMediaController.LiteHost;

internal sealed class LitePodcastProgressStoreDenied(string message) : Exception(message);

/// <summary>
/// Jedyny pisarz postepu Podcastow i YouTube dla jednego procesu wxPython.
/// Python nie otwiera <c>podcasts.db</c> do zapisu; host C# wykonuje waska
/// transakcje Core obejmujaca tylko jeden odcinek i biezacy element sesji.
/// </summary>
internal sealed class LitePodcastProgressStore : IDisposable
{
    public const string OwnerLockFileName = "amc-lite-podcast-owner.lock";

    private readonly FileStream _ownerLock;
    private readonly PodcastPlaybackCheckpointStore _store;
    private readonly Func<bool> _fullAmcIsRunning;

    private LitePodcastProgressStore(
        string databasePath,
        FileStream ownerLock,
        Func<bool> fullAmcIsRunning)
    {
        _ownerLock = ownerLock;
        _store = new PodcastPlaybackCheckpointStore(databasePath);
        _fullAmcIsRunning = fullAmcIsRunning;
    }

    public static LitePodcastProgressStore Open(
        string databasePath,
        Func<bool>? fullAmcIsRunning = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new LitePodcastProgressStoreDenied("Nie podano bazy Podcastów i YouTube.");
        var fullPath = Path.GetFullPath(databasePath);
        if (!File.Exists(fullPath))
            throw new LitePodcastProgressStoreDenied("Nie znaleziono bazy Podcastów i YouTube.");
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new LitePodcastProgressStoreDenied("Baza Podcastów nie ma katalogu.");
        FileStream ownerLock;
        try
        {
            ownerLock = new FileStream(
                Path.Combine(directory, OwnerLockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LitePodcastProgressStoreDenied(
                "Inne okno AMC z wxPython zapisuje już postęp podcastów.");
        }
        return new LitePodcastProgressStore(
            fullPath,
            ownerLock,
            fullAmcIsRunning ?? FullAmcIsRunning);
    }

    public object Save(string episodeId, TimeSpan position, TimeSpan duration, bool completed)
    {
        // Starszy WPF zapisuje cala migawke PodcastSettings i nie zna blokady
        // hosta Lite. Jezeli dziala, nawet poprawna waska transakcja moglaby
        // zostac pozniej nadpisana jego stanem z pamieci.
        if (_fullAmcIsRunning())
        {
            throw new LiteRequestException(
                "Zamknij najpierw główne AMC. Równoczesny zapis postępu z dwóch wersji mógłby utracić dane.");
        }
        try
        {
            var result = _store.Save(episodeId, position, duration, completed);
            return new
            {
                saved = true,
                changed = result.Changed,
                completed = result.Completed,
                positionSeconds = result.Position.TotalSeconds,
                durationSeconds = result.Duration.TotalSeconds,
                state = result.ListeningState
            };
        }
        catch (KeyNotFoundException exception)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (
            exception is IOException
                or InvalidDataException
                or UnauthorizedAccessException
                or SqliteException
                or JsonException)
        {
            Console.Error.WriteLine("[lite-host] zapis postępu podcastu: " + exception);
            throw new LiteRequestException(
                "Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public void Dispose() => _ownerLock.Dispose();

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
