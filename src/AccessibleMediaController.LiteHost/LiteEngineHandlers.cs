using System.Globalization;
using System.Text;
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

    /// <summary>
    /// Czas trwania ZMIERZONY przez dekoder, nie wziety z <see cref="MediaItem"/>.
    /// Pozycja na liscie nie zna dlugosci pliku (<c>MediaItem.Duration</c> jest
    /// zerem przy wpisie z folderu), a prawdziwa wartosc podaje silnik dopiero
    /// zdarzeniem <c>DurationAvailable</c> po otwarciu strumienia. Bez tego
    /// <c>transport.status</c> oddawal <c>durationSeconds=0</c> i okno nie mialo
    /// z czego policzyc czasu calkowitego ani pozostalego.
    /// </summary>
    private TimeSpan _filesDuration;
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
        _files.DurationAvailable += (_, e) =>
        {
            // Zapamietujemy czas Z DEKODERA, bo tylko on go zna; dopiero
            // wtedy transport.status ma co oddac w durationSeconds.
            lock (_gate)
            {
                if (_filesItem is not null && _filesItem.Id == e.Item.Id) _filesDuration = e.Duration;
            }
            Publish("playback.duration",
                new { engine = "files", id = e.Item.Id, seconds = e.Duration.TotalSeconds, sampleRateHz = e.SampleRateHz });
        };

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
            ["audio.outputs"] = (_, _) => ListOutputs(),
            ["library.collationKeys"] = (request, _) => CollationKeys(request.Args)
        };
    }

    /// <summary>
    /// Klucze sortowania dla wsadu napisow, w zadanym TRYBIE.
    /// </summary>
    /// <remarks>
    /// Frontend w Pythonie nie ma ICU, a reczny port reguly pl-PL zostal
    /// zmierzony jako niezgodny na 93,9% pozycji pelnego korpusu. Zamiast
    /// pytac o KAZDA pare oddajemy jeden klucz na napis: porownanie bajtow
    /// tych kluczy odtwarza kolejnosc oryginalnego komparatora dokladnie
    /// (zmierzone: 0 roznic pozycji, 0 zerwanych remisow, identycznie na
    /// Windows i Linuksie).
    ///
    /// Tryby, bo widoki AMC NIE uzywaja jednej reguly:
    /// <list type="bullet">
    /// <item><c>AMC_PL</c> -- <c>IgnoreCase | IgnoreNonSpace</c>, kolacja
    /// schematu SQL. Foldery i indeks <c>AMC_PL</c>. Tryb DOMYSLNY, zeby
    /// starsi wolajacy dostali dokladnie to co dotad.</item>
    /// <item><c>TITLE_IGNORE_CASE</c> -- <c>IgnoreCase</c> samo, czyli
    /// <c>StringComparer.CurrentCultureIgnoreCase</c> z widokow "Wszystkie
    /// pliki" i alfabetycznych Ulubionych. Bez <c>IgnoreNonSpace</c>, bo ten
    /// zrownuje "e" z "é", a widok ich NIE zrownuje.</item>
    /// <item><c>ORDINAL_IGNORE_CASE</c> -- <c>StringComparer.OrdinalIgnoreCase</c>
    /// dla tie-breaka po <c>Source</c>. To NIE jest kolacja jezykowa, wiec nie
    /// idzie przez <c>CompareInfo</c>: oddajemy napis po
    /// <c>Rune.ToUpperInvariant</c> SKALAR PO SKALARZE, zakodowany w UTF-8.
    /// UTF-8 zachowuje porzadek punktow kodowych, wiec porownanie bajtow w
    /// Pythonie odtwarza <c>OrdinalIgnoreCase</c> co do znaku. Celowo NIE
    /// <c>string.ToUpperInvariant()</c> i NIE UTF-16: oba zmierzone jako
    /// niezgodne na parach zastepczych, a <c>str.upper()</c> w Pythonie
    /// dodatkowo rozwija "ß" do "SS", czego Ordinal nie robi.</item>
    /// </list>
    ///
    /// Operacja jest CZYSTO OBLICZENIOWA: nie dotyka bazy, dysku ani
    /// odtwarzania i niczego nie zapisuje do profilu.
    /// </remarks>
    private static object CollationKeys(JsonElement args)
    {
        const int maximumBatch = 50_000;

        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("titles", out var titles)
            || titles.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Brak wymaganego argumentu \"titles\" (tablica).");
        }
        if (titles.GetArrayLength() > maximumBatch)
        {
            throw new LiteRequestException($"Wsad przekracza {maximumBatch} tytulow.");
        }

        // Brak "mode" to STARY wolajacy -- dostaje AMC_PL, jak dotad.
        var mode = args.TryGetProperty("mode", out var modeElement)
            && modeElement.ValueKind == JsonValueKind.String
                ? modeElement.GetString() ?? AmcPlMode
                : AmcPlMode;

        var compare = CultureInfo.GetCultureInfo("pl-PL").CompareInfo;
        CompareOptions options;
        switch (mode)
        {
            case AmcPlMode:
                options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
                break;
            case TitleIgnoreCaseMode:
                options = CompareOptions.IgnoreCase;
                break;
            case OrdinalIgnoreCaseMode:
                // Obsluzone osobno nizej -- nie przechodzi przez CompareInfo.
                options = CompareOptions.None;
                break;
            default:
                // Nieznany tryb to blad, a nie cichy powrot do AMC_PL: cicha
                // zla kolejnosc wyglada jak dzialajaca funkcja.
                throw new LiteRequestException($"Nieznany tryb kolacji: \"{mode}\".");
        }

        var keys = new List<string>(titles.GetArrayLength());
        foreach (var element in titles.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new LiteRequestException("Kazdy tytul musi byc napisem.");
            }
            var title = element.GetString() ?? string.Empty;
            if (title.Length > LiteArgs.MaximumTextLength)
            {
                throw new LiteRequestException("Tytul jest zbyt dlugi.");
            }
            keys.Add(mode == OrdinalIgnoreCaseMode
                ? Convert.ToBase64String(OrdinalIgnoreCaseKey(title))
                : Convert.ToBase64String(compare.GetSortKey(title, options).KeyData));
        }

        return new { collation = mode, culture = "pl-PL", keys };
    }

    private const string AmcPlMode = "AMC_PL";
    private const string TitleIgnoreCaseMode = "TITLE_IGNORE_CASE";
    private const string OrdinalIgnoreCaseMode = "ORDINAL_IGNORE_CASE";

    /// <summary>
    /// Klucz odtwarzajacy <c>StringComparer.OrdinalIgnoreCase</c> bajt po bajcie.
    /// </summary>
    /// <remarks>
    /// Ordinal porzadkuje SKALARY Unicode, a nie jednostki kodowe UTF-16 ani
    /// wynik pelnego mapowania jezykowego. Dlatego: <c>EnumerateRunes</c>
    /// (skalary), <c>Rune.ToUpperInvariant</c> (mapowanie 1:1, bez rozwijania
    /// "ß" do "SS") i UTF-8 (koduje punkty kodowe zachowujac ich porzadek).
    /// Zmierzone wzgledem ORYGINALNEGO <c>StringComparer.OrdinalIgnoreCase</c>:
    /// 0 niezgodnych par.
    /// </remarks>
    private static byte[] OrdinalIgnoreCaseKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            builder.Append(Rune.ToUpperInvariant(rune).ToString());
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
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
            // Nowy strumien: stary czas przestaje obowiazywac, zanim dekoder
            // zdazy podac nowy. Inaczej okno pokazywalo dlugosc POPRZEDNIEGO pliku.
            _filesDuration = TimeSpan.Zero;
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
        TimeSpan filesDuration;
        lock (_gate)
        {
            engine = _activeEngine;
            paused = _paused;
            item = engine == "radio" ? _radioItem : _filesItem;
            filesDuration = _filesDuration;
        }

        var position = engine == "radio" ? _radio.Position : _files.Position;
        // Dla plikow bierzemy czas zmierzony przez dekoder; MediaItem z listy
        // folderu go nie zna. Radio nie ma dlugosci - zostaje zero.
        var duration = engine == "radio"
            ? item?.Duration ?? TimeSpan.Zero
            : filesDuration != TimeSpan.Zero ? filesDuration : item?.Duration ?? TimeSpan.Zero;
        return new
        {
            engine,
            paused,
            id = item?.Id,
            title = item?.Title,
            positionSeconds = position.TotalSeconds,
            durationSeconds = duration.TotalSeconds,
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
