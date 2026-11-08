using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Jeden tor odtwarzania Spotify dla interfejsu Python. Oficjalny Web Playback
/// SDK działa w osobnym, ukrytym Chrome i publikuje ten sam kontrakt zdarzeń.
/// </summary>
internal sealed class LiteSpotifyPlaybackCoordinator(
    Func<CancellationToken, Task<LiteSpotifyAccessToken>>? credentials = null,
    string? soloistSettingsPath = null,
    string? soloistDataDirectory = null) : IDisposable
{
    private const string SessionId = LiteSpotifyCatalogContract.SessionId;
    private readonly object gate = new();
    private ILiteSpotifyOutput? output;
    private LiteEventSink? events;
    private MediaItem? currentItem;
    private string? selectedDeviceName;
    private int volume = 35;
    private double playbackRate = 1d;
    private bool muted;
    private bool paused;
    private bool playing;
    private bool preparing;
    private bool disposed;

    internal object Play(JsonElement args, LiteEventSink eventSink)
    {
        var request = LiteSpotifyPlaybackContract.ReadPlay(args);
        ILiteSpotifyOutput target;
        int effectiveVolume;
        double effectiveRate;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            events = eventSink;
            currentItem = request.Item;
            volume = request.Volume;
            paused = false;
            playing = false;
            preparing = true;
            RefreshOutputLocked();
            target = EnsureOutputLocked();
            effectiveVolume = muted ? 0 : request.Volume;
            effectiveRate = target is LiteSpotifySoloistOutput
                ? 1d : request.PlaybackRate;
            playbackRate = effectiveRate;
        }
        target.Play(
            request.Item,
            request.Position,
            effectiveVolume,
            effectiveRate);
        return new
        {
            ok = true,
            engine = SessionId,
            sessionId = SessionId,
            id = request.Item.Id,
            title = request.Item.Title
        };
    }

    internal object PauseResume()
    {
        ILiteSpotifyOutput target;
        bool nextPaused;
        lock (gate)
        {
            if (currentItem is null)
                throw new LiteRequestException("Najpierw uruchom materiał w Spotify.");
            target = EnsureOutputLocked();
            nextPaused = !paused;
            paused = nextPaused;
            if (nextPaused)
            {
                playing = false;
                preparing = false;
            }
            else
            {
                playing = true;
            }
        }
        if (nextPaused) target.Pause();
        else target.Resume();
        return new
        {
            paused = nextPaused,
            engine = SessionId,
            sessionId = SessionId,
            queue = false
        };
    }

    internal object Stop()
    {
        ILiteSpotifyOutput? target;
        lock (gate)
        {
            target = output;
            currentItem = null;
            paused = false;
            playing = false;
            preparing = false;
        }
        target?.Stop();
        return new
        {
            ok = true,
            engine = SessionId,
            sessionId = SessionId,
            queue = false,
            playing = false
        };
    }

    internal object Seek(JsonElement args)
    {
        var delta = LiteArgs.ReadDouble(
            args, "deltaSeconds", double.NaN, -86_400d, 86_400d);
        var absolute = LiteArgs.ReadDouble(
            args, "positionSeconds", double.NaN, 0d, 86_400d);
        ILiteSpotifyOutput target;
        lock (gate) target = RequireCurrentOutputLocked();
        var position = !double.IsNaN(absolute)
            ? TimeSpan.FromSeconds(absolute)
            : target.Position + TimeSpan.FromSeconds(double.IsNaN(delta) ? 0d : delta);
        position = SeekTo(position);
        return new
        {
            engine = SessionId,
            sessionId = SessionId,
            positionSeconds = position.TotalSeconds
        };
    }

    /// <summary>
    /// Migawka stabilnej osi czasu Spotify. Id zapisu nie zawiera sufiksu
    /// wystąpienia z playlisty, dzięki czemu ten sam odcinek lub utwór ma te
    /// same zakładki niezależnie od listy, z której został uruchomiony.
    /// </summary>
    internal LiteSpotifyTimelineSnapshot Timeline()
    {
        MediaItem item;
        ILiteSpotifyOutput target;
        lock (gate)
        {
            target = RequireCurrentOutputLocked();
            item = currentItem!;
        }
        var serviceType = item.Kind switch
        {
            MediaItemKind.Track => "track",
            MediaItemKind.Episode => "episode",
            _ => throw new LiteRequestException(
                "Zakładki i rozdziały Spotify dotyczą utworów i odcinków.")
        };
        var externalId = item.ExternalId?.Trim();
        if (string.IsNullOrWhiteSpace(externalId))
            throw new LiteRequestException(
                "Bieżący materiał Spotify nie ma trwałego identyfikatora.");
        var stored = new MediaItem
        {
            Id = $"spotify:{serviceType}:{externalId}",
            Title = item.Title,
            Artist = item.Artist,
            Kind = item.Kind,
            ExternalId = externalId,
            Source = $"spotify:{serviceType}:{externalId}",
            Duration = item.Duration,
            IsAvailable = item.IsAvailable
        };
        return new LiteSpotifyTimelineSnapshot(
            item.Id,
            stored,
            target.Position,
            item.Duration);
    }

    internal TimeSpan SeekTo(TimeSpan position)
    {
        ILiteSpotifyOutput target;
        lock (gate) target = RequireCurrentOutputLocked();
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        target.Seek(position);
        return position;
    }

    internal object SetVolume(JsonElement args)
    {
        ILiteSpotifyOutput? target;
        int nextVolume;
        bool isMuted;
        lock (gate)
        {
            nextVolume = LiteArgs.ReadInt(args, "volume", volume, 0, 100);
            volume = nextVolume;
            isMuted = muted;
            target = output;
        }
        target?.SetVolume(isMuted ? 0 : nextVolume);
        return new
        {
            volume = nextVolume,
            engine = SessionId,
            sessionId = SessionId,
            queue = false
        };
    }

    internal object SetRate(JsonElement args)
    {
        ILiteSpotifyOutput? target;
        double rate;
        lock (gate)
        {
            rate = LiteArgs.ReadDouble(
                args, "rate", playbackRate, 0.5d, 2d);
            target = output;
            if (target is LiteSpotifySoloistOutput && Math.Abs(rate - 1d) > 0.0001d)
                throw new LiteRequestException(
                    "Spotify Soloist nie udostępnia zmiany prędkości odtwarzania.");
            playbackRate = rate;
        }
        target?.SetPlaybackRate(rate);
        return new
        {
            rate,
            engine = SessionId,
            sessionId = SessionId,
            queue = false
        };
    }

    internal object ToggleMute()
    {
        ILiteSpotifyOutput? target;
        bool nextMuted;
        int currentVolume;
        lock (gate)
        {
            muted = !muted;
            nextMuted = muted;
            currentVolume = volume;
            target = output;
        }
        target?.SetVolume(nextMuted ? 0 : currentVolume);
        return new
        {
            engine = SessionId,
            sessionId = SessionId,
            muted = nextMuted,
            volume = currentVolume
        };
    }

    internal object Status()
    {
        MediaItem? item;
        ILiteSpotifyOutput? target;
        bool isPlaying;
        bool isPaused;
        bool isMuted;
        bool isPreparing;
        int currentVolume;
        double currentRate;
        lock (gate)
        {
            item = currentItem;
            target = output;
            isPlaying = playing;
            isPaused = paused;
            isMuted = muted;
            isPreparing = preparing;
            currentVolume = volume;
            currentRate = playbackRate;
        }
        return new
        {
            engine = SessionId,
            sessionId = SessionId,
            playing = isPlaying,
            paused = isPaused,
            muted = isMuted,
            preparing = isPreparing,
            id = item?.Id,
            title = item?.Title,
            // Web Playback SDK nie udostępnia pomiaru pojedynczego pliku.
            // Spotify deklaruje AAC 256 kb/s dla Premium Web Player i
            // około 128 kb/s dla odcinków w Web Player. Mówimy o jakości
            // usługi, nie o zmierzonej przepływności tego materiału.
            currentPlaybackText = item is null ? "Nic nie jest odtwarzane"
                : target is LiteSpotifySoloistOutput
                    ? $"{item.Title}, Spotify Soloist, parametry strumienia niedostępne"
                : item.Kind == MediaItemKind.Episode
                    ? $"{item.Title}, jakość Spotify Web: około 128 kb/s"
                    : $"{item.Title}, jakość Spotify Web: AAC 256 kb/s",
            positionSeconds = target?.Position.TotalSeconds ?? 0d,
            durationSeconds = item?.Duration.TotalSeconds ?? 0d,
            volume = currentVolume,
            rate = currentRate,
            bufferedSeconds = 0d,
            behindLiveSeconds = 0d
        };
    }

    internal object ListOutputs(JsonElement args)
    {
        var requested = LiteArgs.ReadText(args, "selectedDeviceId")?.Trim();
        ILiteSpotifyOutput target;
        lock (gate) target = EnsureOutputLocked();
        var devices = target.GetOutputDevicesAsync().GetAwaiter().GetResult();
        var choices = new List<LiteSpotifyOutputChoice>
        {
            new(null, "Domyślne urządzenie audio", true)
        };
        foreach (var device in devices
            .Where(device => !string.IsNullOrWhiteSpace(device.Name))
            .DistinctBy(device => device.Id, StringComparer.OrdinalIgnoreCase))
        {
            choices.Add(new(device.Id, device.Name, true));
        }
        if (!string.IsNullOrWhiteSpace(requested)
            && !choices.Any(choice => string.Equals(
                choice.Id, requested, StringComparison.OrdinalIgnoreCase)))
        {
            choices.Add(new(requested, $"{requested} (niedostępne)", false));
        }
        return new { devices = choices };
    }

    internal object SelectOutput(JsonElement args)
    {
        var deviceName = LiteArgs.ReadText(args, "deviceId")?.Trim();
        var restart = LiteArgs.ReadBool(args, "restart", true);
        ILiteSpotifyOutput? target;
        bool active;
        string? previousDeviceName;
        lock (gate)
        {
            previousDeviceName = selectedDeviceName;
            selectedDeviceName = deviceName;
            target = output;
            active = currentItem is not null && (playing || paused || preparing);
            // Dla wyjścia domyślnego konfiguracja startowa pozostaje całkiem
            // leniwa. Nazwane wyjście trzeba jednak sprawdzić przed Play:
            // inaczej nieobecny endpoint był zwracany jako available=true, a
            // dopiero pierwsze odtwarzanie próbowało ratować sesję.
            if (!restart && target is null)
            {
                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    return new
                    {
                        sessionId = SessionId,
                        deviceId = deviceName,
                        available = true,
                        active = false,
                        rebuilt = false,
                        usingDefault = true
                    };
                }
                target = EnsureOutputLocked();
            }
            target ??= EnsureOutputLocked();
        }

        if (!restart)
        {
            var configured = target.TrySetOutputDeviceAsync(deviceName)
                .GetAwaiter().GetResult();
            if (!configured)
            {
                target.TrySetOutputDeviceAsync(null).GetAwaiter().GetResult();
                lock (gate) selectedDeviceName = null;
            }
            return new
            {
                sessionId = SessionId,
                deviceId = deviceName,
                available = configured,
                active = false,
                rebuilt = false,
                usingDefault = !configured || string.IsNullOrWhiteSpace(deviceName)
            };
        }

        var changed = target.TrySetOutputDeviceAsync(deviceName)
            .GetAwaiter().GetResult();
        if (!changed)
        {
            lock (gate) selectedDeviceName = previousDeviceName;
            throw new LiteRequestException("Nie udało się zmienić urządzenia audio Spotify.");
        }
        return new
        {
            sessionId = SessionId,
            deviceId = deviceName,
            available = true,
            active,
            rebuilt = false,
            usingDefault = string.IsNullOrWhiteSpace(deviceName)
        };
    }

    private ILiteSpotifyOutput EnsureOutputLocked()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (output is not null) return output;
        var soloist = LiteSpotifySoloistOutput.TryCreateFromSettings(
            soloistSettingsPath, soloistDataDirectory);
        if (soloist is not null)
        {
            AttachOutputLocked(soloist);
            return soloist;
        }
        if (credentials is null)
            throw new LiteRequestException("Najpierw skonfiguruj i zaloguj konto Spotify.");
        var chrome = new LiteSpotifyChromeOutput(credentials, selectedDeviceName);
        AttachOutputLocked(chrome);
        return chrome;
    }

    private void RefreshOutputLocked()
    {
        var soloist = LiteSpotifySoloistOutput.TryCreateFromSettings(
            soloistSettingsPath, soloistDataDirectory);
        if (soloist is not null)
        {
            if (output is LiteSpotifySoloistOutput)
                soloist.Dispose();
            else
            {
                output?.Dispose();
                AttachOutputLocked(soloist);
            }
        }
        else if (output is LiteSpotifySoloistOutput)
        {
            output.Dispose();
            output = null;
        }
    }

    private void AttachOutputLocked(ILiteSpotifyOutput selected)
    {
        output = selected;
        output.PlaybackPreparing += OnPlaybackPreparing;
        output.PlaybackStarted += OnPlaybackStarted;
        output.DurationAvailable += OnDurationAvailable;
        output.PlaybackEnded += OnPlaybackEnded;
        output.PlaybackFailed += OnPlaybackFailed;
    }

    private ILiteSpotifyOutput RequireCurrentOutputLocked()
    {
        if (currentItem is null)
            throw new LiteRequestException("Najpierw uruchom materiał w Spotify.");
        return EnsureOutputLocked();
    }

    private void OnPlaybackPreparing(object? sender, MediaPlaybackPreparingEventArgs e)
    {
        lock (gate)
        {
            if (!IsCurrentLocked(e.Item)) return;
            preparing = true;
            playing = false;
            paused = false;
        }
    }

    private void OnPlaybackStarted(object? sender, MediaPlaybackStartedEventArgs e)
    {
        LiteEventSink? sink;
        lock (gate)
        {
            if (!IsCurrentLocked(e.Item)) return;
            preparing = false;
            playing = !paused;
            sink = events;
        }
        var fallbackDeviceName = (sender as ILiteSpotifyOutput)?
            .TakeDefaultOutputFallbackNotice();
        sink?.Publish("playback.started", new
        {
            engine = SessionId,
            sessionId = SessionId,
            id = e.Item.Id,
            title = e.Item.Title,
            audioOutputFallbackDeviceName = fallbackDeviceName
        });
    }

    private void OnDurationAvailable(object? sender, MediaDurationAvailableEventArgs e)
    {
        lock (gate)
        {
            if (!IsCurrentLocked(e.Item)) return;
            currentItem!.Duration = e.Duration;
        }
    }

    private void OnPlaybackEnded(object? sender, MediaPlaybackEndedEventArgs e)
    {
        LiteEventSink? sink;
        lock (gate)
        {
            if (!IsCurrentLocked(e.Item)) return;
            preparing = false;
            playing = false;
            paused = false;
            sink = events;
        }
        sink?.Publish("playback.ended", new
        {
            engine = SessionId,
            sessionId = SessionId,
            id = e.Item.Id,
            title = e.Item.Title,
            queueContinues = false
        });
    }

    private void OnPlaybackFailed(object? sender, MediaOutputFailedEventArgs e)
    {
        LiteEventSink? sink;
        lock (gate)
        {
            // Zmiana urządzenia bez materiału ma własną odpowiedź operacji;
            // nie udaje awarii bieżącego utworu.
            if (e.Item is null || !IsCurrentLocked(e.Item)) return;
            preparing = false;
            playing = false;
            paused = false;
            sink = events;
        }
        sink?.Publish("playback.failed", new
        {
            engine = SessionId,
            sessionId = SessionId,
            id = e.Item.Id,
            title = e.Item.Title,
            message = e.Message
        });
    }

    private bool IsCurrentLocked(MediaItem item) =>
        currentItem is not null
        && string.Equals(currentItem.Id, item.Id, StringComparison.Ordinal);

    public void Dispose()
    {
        ILiteSpotifyOutput? target;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            target = output;
            output = null;
            events = null;
        }
        target?.Dispose();
    }
}

internal sealed record LiteSpotifyOutputChoice(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("available")] bool Available);

internal sealed record LiteSpotifyTimelineSnapshot(
    string PlaybackItemId,
    MediaItem Item,
    TimeSpan Position,
    TimeSpan Duration);
