using System.Diagnostics;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;
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
    private readonly PodcastLibraryMutationStore _store;
    private readonly Func<bool> _fullAmcIsRunning;

    private LitePodcastProgressStore(
        string databasePath,
        FileStream ownerLock,
        Func<bool> fullAmcIsRunning)
    {
        _ownerLock = ownerLock;
        _store = new PodcastLibraryMutationStore(databasePath);
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
            var result = _store.SavePlaybackCheckpoint(
                episodeId, position, duration, completed);
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

    public IReadOnlyList<PodcastRefreshTarget> GetRefreshTargets(string? subscriptionId)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesne odświeżanie z dwóch wersji mogłoby utracić dane.");
        try
        {
            return _store.GetRefreshTargets(subscriptionId);
        }
        catch (Exception exception) when (exception is KeyNotFoundException
            or InvalidOperationException)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] odczyt źródeł podcastów: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public int GetInboxCount()
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesne odświeżanie z dwóch wersji mogłoby utracić dane.");
        try
        {
            return _store.GetInboxCount();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] licznik nowych podcastów: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastFavoriteToggleResult ToggleFavorites(
        IReadOnlyCollection<string> subscriptionIds,
        IReadOnlyCollection<string> episodeIds)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesna zmiana ulubionych z dwóch wersji mogłaby utracić dane.");
        try
        {
            return _store.ToggleFavorites(subscriptionIds, episodeIds);
        }
        catch (Exception exception) when (exception is ArgumentException
            or KeyNotFoundException)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] zmiana ulubionych podcastów: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastPlaybackOptionsSnapshot GetPlaybackOptions(
        PodcastPlaybackOptionsTarget target,
        string itemId)
    {
        try
        {
            return _store.GetPlaybackOptions(target, itemId);
        }
        catch (Exception exception) when (exception is ArgumentException
            or KeyNotFoundException)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] odczyt opcji podcastu: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastPlaybackOptionsSnapshot SetPlaybackOptions(
        PodcastPlaybackOptionsTarget target,
        string itemId,
        PodcastPlaybackOptionsChange change)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesna zmiana opcji podcastu z dwóch wersji mogłaby utracić dane.");
        try
        {
            return _store.SetPlaybackOptions(target, itemId, change);
        }
        catch (Exception exception) when (exception is ArgumentException
            or KeyNotFoundException)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] zapis opcji podcastu: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public IReadOnlyList<PodcastDownloadTarget> GetDownloadTargets(
        IReadOnlyCollection<string> episodeIds)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesne pobieranie z dwóch wersji mogłoby utracić dane.");
        try
        {
            return _store.GetDownloadTargets(episodeIds);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] odczyt odcinków do pobrania: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastDownloadPathResult SaveDownloadPath(string episodeId, string downloadPath)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesny zapis pobranego odcinka z dwóch wersji mógłby utracić dane.");
        try
        {
            return _store.SaveDownloadPath(episodeId, downloadPath);
        }
        catch (KeyNotFoundException exception)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] zapis pobranego odcinka: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastSourceAddResult AddSource(
        PodcastFeedDocument feed,
        string? titleOverride,
        PodcastSourceKind sourceKind,
        BookmarkSettings? bookmarks = null)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesne dodawanie źródeł z dwóch wersji mogłoby utracić dane.");
        try
        {
            return _store.AddSource(
                feed,
                titleOverride,
                sourceKind,
                DateTime.UtcNow,
                bookmarks);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] dodawanie źródła podcastów: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastInternetMediaAddResult AddInternetMedia(
        PodcastInternetMediaSource media,
        string? titleOverride)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesne dodawanie materiałów z dwóch wersji mogłoby utracić dane.");
        try
        {
            return _store.AddInternetMedia(media, titleOverride);
        }
        catch (ArgumentException exception)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] dodawanie medium internetowego: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public IReadOnlyList<PodcastOpmlEntry> GetOpmlEntries()
    {
        try
        {
            return _store.GetOpmlEntries();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] eksport OPML podcastów: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public IReadOnlyList<YouTubeCollectionExportEntry> GetYouTubeCollectionsForExport()
    {
        try
        {
            return _store.GetYouTubeCollectionsForExport();
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] eksport kanałów YouTube: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    public PodcastRefreshResult ApplyRefresh(
        string subscriptionId,
        PodcastFeedDocument feed,
        BookmarkSettings? bookmarks = null)
    {
        EnsureFullAmcIsClosed(
            "Zamknij najpierw główne AMC. Równoczesne odświeżanie z dwóch wersji mogłoby utracić dane.");
        try
        {
            return _store.ApplyRefresh(
                subscriptionId,
                feed,
                DateTime.UtcNow,
                bookmarks);
        }
        catch (Exception exception) when (exception is KeyNotFoundException
            or InvalidOperationException)
        {
            throw new LiteRequestException(exception.Message);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            Console.Error.WriteLine("[lite-host] zapis odświeżenia podcastów: " + exception);
            throw new LiteRequestException("Baza Podcastów jest chwilowo niedostępna.");
        }
    }

    private void EnsureFullAmcIsClosed(string message)
    {
        if (_fullAmcIsRunning()) throw new LiteRequestException(message);
    }

    private static bool IsDatabaseFailure(Exception exception) => exception is
        IOException
        or InvalidDataException
        or UnauthorizedAccessException
        or SqliteException
        or JsonException;

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
