using System.Diagnostics;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Controls a user-installed, already paired Spotify Soloist through its
/// documented local ctl API. AMC does not own the user's key or distribute the
/// Soloist executable. The Python account dialog owns a process it starts.
/// </summary>
internal sealed class LiteSpotifySoloistOutput : ILiteSpotifyOutput
{
    private readonly string executable;
    private readonly string dataDirectory;
    private readonly Func<string[], (int ExitCode, string Output)> command;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private MediaItem? currentItem;
    private TimeSpan position;
    private long positionAt;
    private bool playing;
    private bool paused;
    private bool disposed;
    private long generation;

    internal LiteSpotifySoloistOutput(
        string executable,
        string dataDirectory,
        Func<string[], (int ExitCode, string Output)>? command = null)
    {
        this.executable = executable;
        this.dataDirectory = dataDirectory;
        this.command = command ?? RunCli;
    }

    internal static LiteSpotifySoloistOutput? TryCreateFromSettings(
        string? settingsPath, string? expectedDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(settingsPath)
            || string.IsNullOrWhiteSpace(expectedDataDirectory)) return null;
        try
        {
            if (!File.Exists(settingsPath)) return null;
            if (new FileInfo(settingsPath).Length > 16 * 1024) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != 1) return null;
            var executable = root.GetProperty("executable").GetString();
            var dataDirectory = root.GetProperty("data_directory").GetString();
            if (string.IsNullOrWhiteSpace(executable)
                || !Path.IsPathFullyQualified(executable)
                || !Path.GetFileName(executable).Equals(
                    "soloist.exe", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(executable)
                || string.IsNullOrWhiteSpace(dataDirectory)
                || !Path.IsPathFullyQualified(dataDirectory)
                || !string.Equals(Path.GetFullPath(dataDirectory),
                    Path.GetFullPath(expectedDataDirectory),
                    StringComparison.OrdinalIgnoreCase))
                return null;
            var output = new LiteSpotifySoloistOutput(executable, dataDirectory);
            if (output.IsReady()) return output;
            output.Dispose();
        }
        catch (Exception)
        {
            // A missing or malformed optional setup must leave Chrome usable.
        }
        return null;
    }

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;

    public TimeSpan Position
    {
        get
        {
            lock (gate)
                return playing && !paused
                    ? position + Stopwatch.GetElapsedTime(positionAt)
                    : position;
        }
    }

    public string? TakeDefaultOutputFallbackNotice() => null;

    internal bool IsReady()
    {
        var result = command(["now", "--json"]);
        if (result.ExitCode != 0) return false;
        try
        {
            using var document = JsonDocument.Parse(result.Output);
            return document.RootElement.TryGetProperty("type", out var kind)
                && kind.GetString() == "playback_state";
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void Play(MediaItem item, TimeSpan start, int volume, double rate)
    {
        var rejection = SpotifyPlaybackItemContract.Rejection(item);
        if (rejection is not null)
        {
            PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, rejection));
            return;
        }
        var uri = SpotifyPlaybackItemContract.PlaybackUri(item);
        if (string.IsNullOrWhiteSpace(uri))
        {
            PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(
                item, "Nie można odczytać identyfikatora materiału Spotify."));
            return;
        }
        long playGeneration;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            currentItem = item;
            position = TimeSpan.Zero;
            playing = false;
            paused = false;
            playGeneration = ++generation;
        }
        PlaybackPreparing?.Invoke(this, new MediaPlaybackPreparingEventArgs(item, false));
        if (command(["activate"]).ExitCode != 0
            || command(["play", uri]).ExitCode != 0)
        {
            Fail(item, playGeneration,
                "Soloist nie przyjął odtwarzania. Sprawdź połączenie w koncie Spotify.");
            return;
        }
        _ = Task.Run(() => MonitorAsync(item, uri, start, volume,
            playGeneration, lifetime.Token));
    }

    public void Pause()
    {
        Control("pause");
        lock (gate)
        {
            position = Position;
            paused = true;
            playing = false;
        }
    }

    public void Resume()
    {
        Control("play");
        lock (gate)
        {
            positionAt = Stopwatch.GetTimestamp();
            paused = false;
            playing = true;
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            ++generation;
            currentItem = null;
            position = TimeSpan.Zero;
            playing = false;
            paused = false;
        }
        // Soloist has pause, but no documented stop command.
        _ = command(["pause"]);
    }

    public void Seek(TimeSpan next)
    {
        var safe = next < TimeSpan.Zero ? TimeSpan.Zero : next;
        Control("seek", ((long)safe.TotalMilliseconds).ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        lock (gate)
        {
            position = safe;
            positionAt = Stopwatch.GetTimestamp();
        }
    }

    public void SetVolume(int next) =>
        Control("volume", Math.Clamp(next, 0, 100).ToString(
            System.Globalization.CultureInfo.InvariantCulture));

    public void SetPlaybackRate(double next)
    {
        if (Math.Abs(next - 1d) > 0.0001d)
            throw new LiteRequestException(
                "Spotify Soloist nie udostępnia zmiany prędkości odtwarzania.");
    }

    public Task<IReadOnlyList<LiteSpotifyChromeDevice>> GetOutputDevicesAsync() =>
        Task.FromResult<IReadOnlyList<LiteSpotifyChromeDevice>>([]);

    public Task<bool> TrySetOutputDeviceAsync(string? wanted) =>
        Task.FromResult(string.IsNullOrWhiteSpace(wanted));

    private void Control(params string[] args)
    {
        if (command(args).ExitCode != 0)
            throw new LiteRequestException(
                "Soloist nie wykonał polecenia. Sprawdź połączenie w koncie Spotify.");
    }

    private async Task MonitorAsync(MediaItem item, string uri, TimeSpan start,
        int volume, long playGeneration, CancellationToken cancellation)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        var started = false;
        var missing = 0;
        try
        {
            while (!cancellation.IsCancellationRequested && IsCurrent(item, playGeneration))
            {
                var snapshot = command(["now", "--json"]);
                if (snapshot.ExitCode == 0 && TryReadState(snapshot.Output,
                    out var stateUri, out var state, out var statePosition,
                    out var duration))
                {
                    if (stateUri == uri && state is "playing" or "paused" or "buffering")
                    {
                        missing = 0;
                        lock (gate)
                        {
                            if (!IsCurrentLocked(item, playGeneration)) return;
                            position = statePosition;
                            positionAt = Stopwatch.GetTimestamp();
                            playing = state == "playing";
                            paused = state == "paused";
                        }
                        if (!started && state != "buffering")
                        {
                            started = true;
                            if (start > TimeSpan.Zero)
                                Seek(start);
                            try { SetVolume(volume); }
                            catch (LiteRequestException) { /* Playback remains usable. */ }
                            PlaybackStarted?.Invoke(this,
                                new MediaPlaybackStartedEventArgs(item));
                        }
                        if (duration > TimeSpan.Zero && item.Duration != duration)
                            DurationAvailable?.Invoke(this,
                                new MediaDurationAvailableEventArgs(item, duration, 0));
                    }
                    else if (started && ++missing >= 2)
                    {
                        End(item, playGeneration);
                        return;
                    }
                }
                else if (started && ++missing >= 4)
                {
                    Fail(item, playGeneration,
                        "Utracono połączenie z odtwarzaczem Soloist.");
                    return;
                }
                if (!started && DateTime.UtcNow > deadline)
                {
                    Fail(item, playGeneration,
                        "Soloist nie potwierdził rozpoczęcia odtwarzania.");
                    return;
                }
                await Task.Delay(750, cancellation).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            Fail(item, playGeneration, "Nie można odczytać stanu Soloist.");
        }
    }

    private static bool TryReadState(string json, out string? uri,
        out string? state, out TimeSpan position, out TimeSpan duration)
    {
        uri = null;
        state = null;
        position = TimeSpan.Zero;
        duration = TimeSpan.Zero;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("type").GetString() != "playback_state") return false;
            state = root.GetProperty("status").GetString();
            if (root.TryGetProperty("item", out var item)
                && item.ValueKind == JsonValueKind.Object)
            {
                uri = item.TryGetProperty("uri", out var id) ? id.GetString() : null;
                if (item.TryGetProperty("decorations", out var decorations)
                    && decorations.TryGetProperty("playback", out var playback)
                    && playback.TryGetProperty("duration_ms", out var durationMs)
                    && durationMs.TryGetDouble(out var millis))
                    duration = TimeSpan.FromMilliseconds(Math.Max(0, millis));
            }
            if (root.TryGetProperty("position", out var anchor)
                && anchor.TryGetProperty("position_ms", out var positionMs)
                && positionMs.TryGetDouble(out var at))
                position = TimeSpan.FromMilliseconds(Math.Max(0, at));
            return true;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private bool IsCurrent(MediaItem item, long playGeneration)
    {
        lock (gate) return IsCurrentLocked(item, playGeneration);
    }

    private bool IsCurrentLocked(MediaItem item, long playGeneration) =>
        generation == playGeneration && ReferenceEquals(currentItem, item);

    private void End(MediaItem item, long playGeneration)
    {
        lock (gate)
        {
            if (!IsCurrentLocked(item, playGeneration)) return;
            ++generation;
            playing = false;
            paused = false;
        }
        PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(item));
    }

    private void Fail(MediaItem item, long playGeneration, string message)
    {
        lock (gate)
        {
            if (!IsCurrentLocked(item, playGeneration)) return;
            ++generation;
            playing = false;
            paused = false;
        }
        PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message));
    }

    private (int ExitCode, string Output) RunCli(string[] args)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            process.StartInfo.ArgumentList.Add("ctl");
            process.StartInfo.ArgumentList.Add("--data-dir");
            process.StartInfo.ArgumentList.Add(dataDirectory);
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            if (!process.Start()) return (2, "");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(8000))
            {
                process.Kill();
                return (2, "");
            }
            Task.WhenAll(output, errors).GetAwaiter().GetResult();
            return (process.ExitCode, output.Result);
        }
        catch (Exception)
        {
            // Never expose CLI output, command arguments, or account data.
            return (2, "");
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            ++generation;
        }
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
