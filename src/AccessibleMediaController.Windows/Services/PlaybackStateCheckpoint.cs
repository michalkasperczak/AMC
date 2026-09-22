using System.Text.Json;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Detached playback data, captured by the UI thread. No live state or catalogue
/// reference crosses into the persistence worker. A full snapshot supersedes it.
/// </summary>
internal sealed class PlaybackStateCheckpoint
{
    private readonly string? _spotifyPlayback;
    private readonly Dictionary<string, long>? _localPositions;
    private readonly string? _currentItemId;
    private readonly int _volume;
    private readonly double _playbackRate;
    private readonly string? _navigation;
    private readonly Dictionary<string, PodcastProgress>? _podcastProgress;
    private sealed record PodcastProgress(long PositionTicks, bool IsNew, bool IsStarted, bool IsPlayed);

    private PlaybackStateCheckpoint(PersistedState state, IEnumerable<string> episodeIds)
    {
        SessionId = "podcasts";
        var ids = episodeIds.ToHashSet(StringComparer.Ordinal);
        _podcastProgress = state.Podcasts.Episodes.Where(episode => ids.Contains(episode.Id))
            .ToDictionary(episode => episode.Id, episode => new PodcastProgress(
                episode.ResumePositionTicks, episode.IsNew, episode.IsStarted, episode.IsPlayed), StringComparer.Ordinal);
        _currentItemId = state.Podcasts.CurrentItemId;
        _volume = state.Podcasts.Volume;
        _playbackRate = state.Podcasts.PlaybackRate;
    }

    private PlaybackStateCheckpoint(string spotifyPlayback)
    {
        SessionId = "spotify";
        _spotifyPlayback = spotifyPlayback;
    }

    private PlaybackStateCheckpoint(PersistedState state)
    {
        SessionId = "local";
        _localPositions = state.LocalMedia.Items.ToDictionary(item => item.Id, item => item.ResumePositionTicks, StringComparer.Ordinal);
        _currentItemId = state.LocalMedia.CurrentItemId;
        _volume = state.LocalMedia.Volume;
        _playbackRate = state.LocalMedia.PlaybackRate;
        _navigation = JsonSerializer.Serialize(state.SessionNavigation);
    }

    public string SessionId { get; }

    public static PlaybackStateCheckpoint CaptureSpotify(PersistedState state) =>
        new(JsonSerializer.Serialize(state.Settings.SpotifyPlayback));

    public static PlaybackStateCheckpoint CaptureLocal(PersistedState state) => new(state);

    public static PlaybackStateCheckpoint CapturePodcasts(PersistedState state, IEnumerable<string> episodeIds) => new(state, episodeIds);

    private PlaybackStateCheckpoint(PlaybackStateCheckpoint newer, PlaybackStateCheckpoint older)
    {
        SessionId = newer.SessionId;
        _currentItemId = newer._currentItemId;
        _volume = newer._volume;
        _playbackRate = newer._playbackRate;
        _podcastProgress = new Dictionary<string, PodcastProgress>(older._podcastProgress!, StringComparer.Ordinal);
        foreach (var pair in newer._podcastProgress!) _podcastProgress[pair.Key] = pair.Value;
    }

    public PlaybackStateCheckpoint MergeEarlier(PlaybackStateCheckpoint older) =>
        _podcastProgress is not null && older._podcastProgress is not null
            ? new PlaybackStateCheckpoint(this, older)
            : this;

    public void Apply(PersistedState snapshot)
    {
        if (_spotifyPlayback is not null)
        {
            snapshot.Settings.SpotifyPlayback =
                JsonSerializer.Deserialize<SpotifyPlaybackSettings>(_spotifyPlayback)!;
        }
        if (_podcastProgress is not null)
        {
            foreach (var episode in snapshot.Podcasts.Episodes)
            {
                if (!_podcastProgress.TryGetValue(episode.Id, out var progress)) continue;
                episode.ResumePositionTicks = progress.PositionTicks;
                episode.IsNew = progress.IsNew;
                episode.IsStarted = progress.IsStarted;
                episode.IsPlayed = progress.IsPlayed;
            }
            snapshot.Podcasts.CurrentItemId = _currentItemId;
            snapshot.Podcasts.Volume = _volume;
            snapshot.Podcasts.PlaybackRate = _playbackRate;
        }
        if (_localPositions is not null)
        {
            // Update existing records only. Never recreate a deleted catalogue item.
            foreach (var item in snapshot.LocalMedia.Items)
                if (_localPositions.TryGetValue(item.Id, out var ticks)) item.ResumePositionTicks = ticks;
            snapshot.LocalMedia.CurrentItemId = _currentItemId;
            snapshot.LocalMedia.Volume = _volume;
            snapshot.LocalMedia.PlaybackRate = _playbackRate;
            snapshot.SessionNavigation = JsonSerializer.Deserialize<SessionNavigationSettings>(_navigation!)!;
        }
    }
}
