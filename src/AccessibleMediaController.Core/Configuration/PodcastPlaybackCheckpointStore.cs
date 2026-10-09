namespace AccessibleMediaController.Core.Configuration;

/// <summary>
/// Publiczna, waska brama zapisu postepu jednego odcinka. Nie przyjmuje calego
/// <see cref="PersistedState"/>, wiec klient nie moze przez niepelna migawke
/// nadpisac subskrypcji, pobran, ulubionych ani innych odcinkow.
/// </summary>
public sealed class PodcastPlaybackCheckpointStore(string databasePath)
{
    private readonly PodcastLibraryDatabase _database = new(databasePath);

    public PodcastPlaybackCheckpointResult Save(
        string episodeId,
        TimeSpan position,
        TimeSpan duration,
        bool completed) =>
        _database.SavePlaybackCheckpoint(episodeId, position, duration, completed);
}

public sealed record PodcastPlaybackCheckpointResult(
    bool Changed,
    bool Completed,
    TimeSpan Position,
    TimeSpan Duration,
    string ListeningState);
