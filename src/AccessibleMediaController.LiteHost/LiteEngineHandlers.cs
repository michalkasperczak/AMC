using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Operacje hosta wykonywane na FAKTYCZNYM silniku AMC
/// (<see cref="WindowsMediaOutput"/> dla plikow,
/// <see cref="RadioMediaOutput"/> dla strumieni). Nic tu nie dekoduje
/// dzwieku samodzielnie i nie uruchamia zadnego okna.
/// </summary>
internal sealed class LiteEngineHandlers : IDisposable
{
    private readonly object _gate = new();
    private readonly WindowsMediaOutput _files = new();
    private readonly RadioMediaOutput _radio;
    private LiteEventSink? _events;

    /// <summary>
    /// Ktory silnik gra TERAZ. Dwie sesje maja osobne wyjscia, ale dzwiek
    /// wydaje jedna naraz, dokladnie jak w pelnym AMC.
    /// </summary>
    private string _activeEngine = "files";

    private MediaItem? _filesItem;
    private MediaItem? _radioItem;
    private int _volume = 35;
    private double _rate = 1d;
    private bool _paused;

    public LiteEngineHandlers(int timeshiftMinutes)
    {
        _radio = new RadioMediaOutput(timeshiftMinutes);

        _files.PlaybackFailed += (_, e) => Publish("playback.failed",
            new { engine = "files", message = e.Message, title = e.Item?.Title });
        _files.PlaybackStarted += (_, e) => Publish("playback.started",
            new { engine = "files", title = e.Item.Title, id = e.Item.Id,
                tempoFallbackReason = _files.TempoFallbackReason });
        _files.PlaybackEnded += (_, e) => Publish("playback.ended",
            new { engine = "files", title = e.Item.Title, id = e.Item.Id });
        _files.DurationAvailable += (_, e) => Publish("playback.duration",
            new { engine = "files", id = e.Item.Id, seconds = e.Duration.TotalSeconds, sampleRateHz = e.SampleRateHz });

        _radio.PlaybackFailed += (_, e) => Publish("playback.failed",
            new { engine = "radio", message = e.Message, title = e.Item?.Title });
        _radio.PlaybackStarted += (_, e) => Publish("playback.started",
            new { engine = "radio", title = e.Item.Title, id = e.Item.Id,
                tempoFallbackReason = _radio.TempoFallbackReason });
        _radio.NowPlayingChanged += (_, e) => Publish("radio.nowPlaying",
            new { id = e.Item.Id, station = e.Item.Title, streamTitle = e.StreamTitle });
    }

    private void Publish(string name, object data) => _events?.Publish(name, data);

    public IReadOnlyDictionary<string, Func<LiteRequest, LiteEventSink, object?>> Build()
    {
        return new Dictionary<string, Func<LiteRequest, LiteEventSink, object?>>(StringComparer.Ordinal)
        {
            ["host.hello"] = (_, events) =>
            {
                _events = events;
                return new
                {
                    host = "amc-lite-host",
                    protocol = 1,
                    engine = "AMC WindowsMediaOutput + RadioMediaOutput"
                };
            },
            ["host.shutdown"] = (_, _) => new { ok = true },
            ["files.listFolder"] = (request, _) => ListFolder(request.Args),
            ["files.play"] = (request, events) => PlayFile(request.Args, events),
            ["radio.play"] = (request, events) => PlayStation(request.Args, events),
            ["transport.pauseResume"] = (_, _) => PauseResume(),
            ["transport.stop"] = (_, _) => StopAll(),
            ["transport.seek"] = (request, _) => Seek(request.Args),
            ["transport.setVolume"] = (request, _) => SetVolume(request.Args),
            ["transport.setRate"] = (request, _) => SetRate(request.Args),
            ["transport.status"] = (_, _) => Status(),
            ["radio.importPlaylist"] = (request, _) => ImportPlaylist(request.Args),
            ["audio.configure"] = (request, _) => ConfigureAudio(request.Args),
            ["audio.outputs"] = (_, _) => ListOutputs()
        };
    }

    private object ListFolder(JsonElement args)
    {
        var folder = LiteArgs.RequirePath(args, "path");
        if (!Directory.Exists(folder))
        {
            throw new LiteRequestException($"Nie znaleziono folderu: {folder}");
        }

        // Katalogi i pliki osobno: lista w interfejsie pokazuje najpierw
        // podfoldery, potem utwory, jak w pelnym AMC.
        var folders = new List<object>();
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(folder).Order(StringComparer.OrdinalIgnoreCase))
            {
                folders.Add(new
                {
                    kind = "folder",
                    id = "dir:" + directory,
                    title = Path.GetFileName(directory),
                    path = directory
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Brak dostepu do czesci podfolderow nie moze wywrocic listy.
        }

        var tracks = LocalAudioFileDiscovery.FindFiles(folder)
            .Where(path => string.Equals(
                Path.GetDirectoryName(path),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),
                StringComparison.OrdinalIgnoreCase))
            .Select(path => (object)new
            {
                kind = "track",
                id = "file:" + path,
                title = Path.GetFileNameWithoutExtension(path),
                path
            })
            .ToArray();

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)));
        return new
        {
            path = Path.GetFullPath(folder),
            parent,
            items = folders.Concat(tracks).ToArray()
        };
    }

    private object PlayFile(JsonElement args, LiteEventSink events)
    {
        _events = events;
        var path = LiteArgs.RequirePath(args, "path");
        if (!File.Exists(path)) throw new LiteRequestException($"Nie znaleziono pliku: {path}");

        var position = TimeSpan.FromSeconds(LiteArgs.ReadDouble(args, "positionSeconds", 0d, 0d, 86_400d));
        var volume = LiteArgs.ReadInt(args, "volume", _volume, 0, 100);
        var rate = LiteArgs.ReadDouble(args, "rate", _rate, 0.5d, 2.0d);
        var item = new MediaItem
        {
            Id = "file:" + path,
            Title = LiteArgs.ReadText(args, "title") ?? Path.GetFileNameWithoutExtension(path),
            Kind = MediaItemKind.Track,
            Source = path
        };

        lock (_gate)
        {
            // Druga sesja milknie: jedno slyszalne zrodlo naraz.
            if (_activeEngine != "files") _radio.Stop();
            _activeEngine = "files";
            _filesItem = item;
            _volume = volume;
            _rate = rate;
            _paused = false;
        }
        _files.Play(item, position, volume, rate);
        return new { ok = true, engine = "files", id = item.Id, title = item.Title };
    }

    private object PlayStation(JsonElement args, LiteEventSink events)
    {
        _events = events;
        var url = LiteArgs.RequireText(args, "url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new LiteRequestException("Adres stacji musi być adresem HTTP lub HTTPS.");
        }

        var volume = LiteArgs.ReadInt(args, "volume", _volume, 0, 100);
        var item = new MediaItem
        {
            Id = LiteArgs.ReadText(args, "id") ?? "station:" + url,
            Title = LiteArgs.ReadText(args, "title") ?? uri.Host,
            Kind = MediaItemKind.Station,
            Source = url
        };

        lock (_gate)
        {
            if (_activeEngine != "radio") _files.Stop();
            _activeEngine = "radio";
            _radioItem = item;
            _volume = volume;
            _paused = false;
        }
        _radio.Play(item, TimeSpan.Zero, volume, 1d);
        return new { ok = true, engine = "radio", id = item.Id, title = item.Title };
    }

    private object PauseResume()
    {
        lock (_gate)
        {
            if (_paused)
            {
                _paused = false;
                if (_activeEngine == "radio" && _radioItem is { } station)
                {
                    _radio.Play(station, TimeSpan.Zero, _volume, 1d);
                }
                else if (_filesItem is { } track)
                {
                    _files.Play(track, _files.Position, _volume, _rate);
                }
                return new { paused = false, engine = _activeEngine };
            }

            _paused = true;
            if (_activeEngine == "radio") _radio.Pause();
            else _files.Pause();
            return new { paused = true, engine = _activeEngine };
        }
    }

    private object StopAll()
    {
        _files.Stop();
        _radio.Stop();
        lock (_gate) _paused = false;
        return new { ok = true };
    }

    private object Seek(JsonElement args)
    {
        var delta = LiteArgs.ReadDouble(args, "deltaSeconds", double.NaN, -86_400d, 86_400d);
        var absolute = LiteArgs.ReadDouble(args, "positionSeconds", double.NaN, 0d, 86_400d);
        string engine;
        lock (_gate) engine = _activeEngine;

        var current = engine == "radio" ? _radio.Position : _files.Position;
        var target = !double.IsNaN(absolute)
            ? TimeSpan.FromSeconds(absolute)
            : current + TimeSpan.FromSeconds(double.IsNaN(delta) ? 0d : delta);
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;

        if (engine == "radio") _radio.Seek(target);
        else _files.Seek(target);
        return new { engine, positionSeconds = target.TotalSeconds };
    }

    private object SetVolume(JsonElement args)
    {
        var volume = LiteArgs.ReadInt(args, "volume", _volume, 0, 100);
        lock (_gate) _volume = volume;
        _files.SetVolume(volume);
        _radio.SetVolume(volume);
        return new { volume };
    }

    private object SetRate(JsonElement args)
    {
        var rate = LiteArgs.ReadDouble(args, "rate", _rate, 0.5d, 2.0d);
        lock (_gate) _rate = rate;
        string engine;
        lock (_gate) engine = _activeEngine;
        if (engine == "radio") _radio.SetPlaybackRate(rate);
        else _files.SetPlaybackRate(rate);
        return new { rate, engine };
    }

    private object Status()
    {
        string engine;
        bool paused;
        MediaItem? item;
        lock (_gate)
        {
            engine = _activeEngine;
            paused = _paused;
            item = engine == "radio" ? _radioItem : _filesItem;
        }

        var position = engine == "radio" ? _radio.Position : _files.Position;
        return new
        {
            engine,
            paused,
            id = item?.Id,
            title = item?.Title,
            positionSeconds = position.TotalSeconds,
            durationSeconds = item?.Duration.TotalSeconds ?? 0d,
            volume = _volume,
            rate = _rate,
            // Czas transmisji istnieje tylko dla radia; dla plikow jest zerem.
            bufferedSeconds = engine == "radio" ? _radio.BufferedDuration.TotalSeconds : 0d,
            behindLiveSeconds = engine == "radio" ? _radio.BehindLive.TotalSeconds : 0d,
            loadedId = engine == "radio" ? _radio.LoadedItemId : _files.LoadedItemId
        };
    }

    private object ImportPlaylist(JsonElement args)
    {
        var path = LiteArgs.RequirePath(args, "path");
        if (!File.Exists(path)) throw new LiteRequestException($"Nie znaleziono pliku listy: {path}");
        // Uzywamy ISTNIEJACEGO importera AMC (M3U/PLS/XSPF/JSON), a nie
        // wlasnego parsera; dzieki temu pliki uzytkownika zachowuja sie
        // dokladnie tak samo jak w pelnym programie.
        var result = RadioPlaylistImporter.Import(path);
        return new
        {
            skipped = result.SkippedEntries,
            stations = result.Stations
                .Select(station => new { name = station.Name, url = station.StreamUrl })
                .ToArray()
        };
    }

    private object ConfigureAudio(JsonElement args)
    {
        var settings = LiteAudioSettings.Read(args);
        if (settings.TempoAlgorithm != PlaybackTempoAlgorithm.SoundTouch && !AmcTempoNativeLibrary.IsAvailable)
            throw new LiteRequestException("Nowe silniki tempa są niedostępne. Wybierz SoundTouch lub napraw pakiet programu.");
        _files.ConfigureAudioProcessing(settings);
        _radio.ConfigureTempoAlgorithm(settings.TempoAlgorithm);
        return new
        {
            loudnessNormalization = settings.LoudnessNormalizationEnabled,
            smoothTrackTransitions = settings.SmoothTrackTransitionsEnabled,
            interTrackSilenceMs = settings.InterTrackSilenceMilliseconds,
            tempoAlgorithm = (int)settings.TempoAlgorithm,
            tempoAlgorithmAvailable = true,
            appliesOnNextPlayback = true
        };
    }

    private static object ListOutputs() =>
        new
        {
            devices = AudioOutputDeviceCatalog.Enumerate(null)
                .Select(device => new { id = device.Id, name = device.Label, available = device.IsAvailable })
                .ToArray()
        };

    public void Dispose()
    {
        _files.Dispose();
        _radio.Dispose();
    }
}
