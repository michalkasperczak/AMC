using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Presentation;
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
    private readonly LiteRadioRecordingCoordinator _recordings = new();
    private LiteEventSink? _events;

    /// <summary>
    /// ZYWA kolejka odtwarzania. Dostaje TO SAMO wyjscie <see cref="_files"/>,
    /// ktorego uzywa bezposrednie <c>files.play</c>: jeden silnik dzwieku, jedna
    /// droga odtwarzania. Kolejnosc nastepstwa liczy sesja Core w koordynatorze,
    /// nie ten plik.
    /// </summary>
    private readonly LiteQueueCoordinator _queue;

    /// <summary>
    /// Magazyn trwalosci kolejki (albo <c>null</c>). Host trzyma go, zeby
    /// zwolnic BLOKADE WLASNOSCI przy zamknieciu: inaczej nastepny host na tej
    /// samej kopii profilu dostalby odmowe po zamknietym poprzedniku.
    /// </summary>
    private readonly LiteQueueStore? _queueStore;
    private readonly LiteBookmarkStore? _bookmarkStore;
    private readonly LitePodcastProgressStore? _podcastProgressStore;
    private readonly LitePodcastRefreshCoordinator? _podcastRefresh;
    private readonly LitePodcastDownloadCoordinator? _podcastDownloads;
    private readonly LitePodcastAddCoordinator? _podcastAdd;
    private readonly LitePodcastOpmlCoordinator? _podcastOpml;
    private readonly LiteProfileMutationStore? _profileMutations;
    private string? _lastPodcastProgressError;
    private bool _currentPodcastCompleted;

    /// <summary>
    /// Ktory silnik gra TERAZ. Dwie sesje maja osobne wyjscia, ale dzwiek
    /// wydaje jedna naraz, dokladnie jak w pelnym AMC.
    /// </summary>
    private string _activeEngine = "files";

    private MediaItem? _filesItem;
    private MediaItem? _radioItem;
    private readonly Dictionary<string, string?> _outputDeviceIdsBySession =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Czas trwania ZMIERZONY przez dekoder, nie wziety z <see cref="MediaItem"/>.
    /// Pozycja na liscie nie zna dlugosci pliku (<c>MediaItem.Duration</c> jest
    /// zerem przy wpisie z folderu), a prawdziwa wartosc podaje silnik dopiero
    /// zdarzeniem <c>DurationAvailable</c> po otwarciu strumienia. Bez tego
    /// <c>transport.status</c> oddawal <c>durationSeconds=0</c> i okno nie mialo
    /// z czego policzyc czasu calkowitego ani pozostalego.
    ///
    /// Trzymamy czas RAZEM z Id, ktorego dotyczy. Inaczej kolejnosc zdarzen
    /// rozstrzygala o wyniku: <c>DurationAvailable</c> potrafi przyjsc, zanim
    /// host zapisze nowy biezacy material, i wtedy czas albo byl odrzucany (Id
    /// jeszcze stare), albo zerowany po fakcie. Z para (Id, czas) zadna
    /// kolejnosc nie moze podac dlugosci NIE TEGO utworu.
    /// </summary>
    private TimeSpan _filesDuration;
    private string? _filesDurationId;
    private int _volume = 35;
    private double _rate = 1d;
    private bool _paused;

    public LiteEngineHandlers(int timeshiftMinutes)
        : this(timeshiftMinutes, null, null, null, null)
    {
    }

    /// <summary>
    /// <paramref name="queueStore"/> jest JEDYNYM wlascicielem zapisu kolejki.
    /// <c>null</c> znaczy "host bez profilu": kolejka dziala w pamieci procesu,
    /// co jest stanem JAWNYM (<c>queue.status</c> oddaje <c>persistent=false</c>),
    /// a nie cichym brakiem trwalosci.
    /// </summary>
    public LiteEngineHandlers(
        int timeshiftMinutes,
        LiteQueueStore? queueStore,
        LiteBookmarkStore? bookmarkStore = null,
        LitePodcastProgressStore? podcastProgressStore = null,
        LiteProfileMutationStore? profileMutations = null)
    {
        _radio = new RadioMediaOutput(timeshiftMinutes);
        _queueStore = queueStore;
        _bookmarkStore = bookmarkStore;
        _podcastProgressStore = podcastProgressStore;
        _profileMutations = profileMutations;
        _podcastRefresh = podcastProgressStore is null
            ? null
            : new LitePodcastRefreshCoordinator(podcastProgressStore, bookmarkStore);
        _podcastDownloads = podcastProgressStore is null
            ? null
            : new LitePodcastDownloadCoordinator(podcastProgressStore);
        _podcastAdd = podcastProgressStore is null
            ? null
            : new LitePodcastAddCoordinator(podcastProgressStore, bookmarkStore);
        _podcastOpml = podcastProgressStore is null
            ? null
            : new LitePodcastOpmlCoordinator(podcastProgressStore, bookmarkStore);
        _queue = new LiteQueueCoordinator(_files, queueStore);

        _files.PlaybackFailed += (_, e) => Publish("playback.failed",
            new { engine = "files", message = e.Message, title = e.Item?.Title });
        _files.PlaybackStarted += (_, e) => Publish("playback.started",
            new { engine = "files", title = e.Item.Title, id = e.Item.Id,
                tempoFallbackReason = _files.TempoFallbackReason });
        _files.PlaybackEnded += (_, e) =>
        {
            if (e.Item.Kind == MediaItemKind.Episode)
            {
                TimeSpan duration;
                lock (_gate)
                {
                    _currentPodcastCompleted = true;
                    duration = string.Equals(_filesDurationId, e.Item.Id, StringComparison.Ordinal)
                        ? _filesDuration
                        : e.Item.Duration;
                }
                TrySavePodcastProgress(e.Item.Id, TimeSpan.Zero, duration, completed: true);
            }
            // Czy kolejka poprowadzi dalej? Pytamy PRZED ogloszeniem konca, zeby
            // okno nie mowilo "Koniec utworu" w chwili, gdy zaraz zacznie grac
            // nastepna pozycja. Samo pytanie nic nie zmienia w kolejce.
            var queueContinues = _queue.WouldAdvanceAfter(e.Item.Id);

            Publish("playback.ended",
                new { engine = "files", title = e.Item.Title, id = e.Item.Id, queueContinues });

            // NATURALNY koniec utworu. Nastepstwo liczy sesja Core w kolejce; tu
            // tylko podajemy jej zakonczony material. Gdy kolejka nie prowadzi
            // tego utworu albo zdarzenie jest spoznione, oddaje null i NIC sie
            // nie dzieje: bezposrednie files.play zachowuje sie jak dotad.
            MediaItem? next;
            try
            {
                next = _queue.HandlePlaybackEnded(e.Item.Id);
            }
            catch (Exception exception)
            {
                // Blad przejscia nie moze zabic hosta ani zapetlic kolejki.
                Publish("playback.failed",
                    new { engine = "files", message = exception.Message, title = e.Item.Title });
                return;
            }
            if (next is null) return;

            lock (_gate)
            {
                _activeEngine = "files";
                _filesItem = next;
                // Nowy strumien: dlugosc POPRZEDNIEGO przestaje obowiazywac.
                // Zerujemy tylko wtedy, gdy zapamietany czas nie dotyczy JUZ
                // nowego utworu -- inaczej skasowalibysmy wartosc, ktora dekoder
                // zdazyl podac przed tym miejscem.
                ForgetStaleFilesDurationLocked(next.Id);
                _paused = false;
            }
            Publish("queue.advanced", new
            {
                engine = "files",
                id = next.Id,
                title = next.Title,
                afterId = e.Item.Id
            });
        };
        _files.DurationAvailable += (_, e) =>
        {
            // Zapamietujemy czas Z DEKODERA razem z Id, do ktorego nalezy. Nie
            // porownujemy go z _filesItem, bo to zdarzenie potrafi wyprzedzic
            // zapis nowego biezacego materialu; para (Id, czas) jest odporna na
            // kolejnosc, a Status() i tak sprawdza zgodnosc Id.
            lock (_gate)
            {
                _filesDurationId = e.Item.Id;
                _filesDuration = e.Duration;
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
        _radio.OutputDeviceFallback += (_, e) => Publish("audio.outputFallback",
            new { engine = "radio", id = e.Item.Id, title = e.Item.Title });
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
            ["media.play"] = (request, events) => PlayMedia(request.Args, events),
            ["radio.play"] = (request, events) => PlayStation(request.Args, events),
            ["radio.currentBroadcastInformation"] = (_, _) =>
                CurrentRadioBroadcastInformation(),
            ["radio.recordingToggle"] = (request, events) =>
                _recordings.Toggle(request.Args, events),
            ["radio.recordingPauseToggle"] = (request, _) =>
                _recordings.TogglePause(request.Args),
            ["radio.recordingSplit"] = (request, _) =>
                _recordings.Split(request.Args),
            ["radio.recordingStopAll"] = (_, _) => _recordings.StopAll(),
            ["radio.recordingStatus"] = (_, _) => _recordings.Status(),
            ["radio.recordingHistory"] = (_, _) => _recordings.History(),
            ["radio.scheduleSync"] = (request, events) =>
                _recordings.SyncSchedules(request.Args, events),
            ["radio.scheduleStatus"] = (_, _) => _recordings.ScheduleStatus(),
            ["radio.scheduleLabels"] = (request, _) => RadioScheduleLabels(request.Args),
            ["transport.pauseResume"] = (_, _) => PauseResume(),
            ["transport.stop"] = (_, _) => StopAll(),
            ["transport.seek"] = (request, _) => Seek(request.Args),
            ["transport.setVolume"] = (request, _) => SetVolume(request.Args),
            ["transport.setRate"] = (request, _) => SetRate(request.Args),
            ["transport.status"] = (_, _) => Status(),
            ["podcast.checkpoint"] = (_, _) => SaveCurrentPodcastProgress(),
            [LitePodcastRefreshCoordinator.Operation] = (request, _) =>
                RefreshPodcasts(request.Args),
            [LitePodcastDownloadCoordinator.Operation] = (request, events) =>
                DownloadPodcastEpisodes(request.Args, events),
            [LitePodcastDownloadCoordinator.SaveAsInfoOperation] = (request, _) =>
                PodcastDownloadSaveAsInfo(request.Args),
            [LitePodcastDownloadCoordinator.SaveAsOperation] = (request, events) =>
                PodcastDownloadSaveAs(request.Args, events),
            [LitePodcastAddCoordinator.Operation] = (request, _) =>
                AddPodcastSource(request.Args),
            [LitePodcastOpmlCoordinator.InspectOperation] = (request, _) =>
                PodcastOpmlInspect(request.Args),
            [LitePodcastOpmlCoordinator.ImportOperation] = (request, _) =>
                PodcastOpmlImport(request.Args),
            [LitePodcastOpmlCoordinator.ExportOperation] = (request, _) =>
                PodcastOpmlExport(request.Args),
            [LitePodcastOpmlCoordinator.ExportYouTubeOperation] = (request, _) =>
                PodcastYouTubeExport(request.Args),
            ["podcast.toggleFavorite"] = (request, _) =>
                TogglePodcastFavorites(request.Args),
            ["library.renameTitle"] = (request, _) => RenameLibraryTitle(request.Args),
            ["library.renameFile"] = (request, _) => RenameLocalFile(request.Args),
            ["library.remove"] = (request, _) => RemoveProfileItems(request.Args),
            ["library.recycle"] = (request, _) => RecycleLocalFiles(request.Args),
            ["podcast.renameSubscription"] = (request, _) => RenamePodcastSubscription(request.Args),
            ["radio.editStation"] = (request, _) => EditRadioStation(request.Args),
            ["bookmark.add"] = (request, _) => AddBookmark(request.Args),
            ["radio.importPlaylist"] = (request, _) => ImportPlaylist(request.Args),
            ["audio.configure"] = (request, _) => ConfigureAudio(request.Args),
            ["audio.outputs"] = (request, _) => ListOutputs(request.Args),
            ["audio.selectOutput"] = (request, _) => SelectOutput(request.Args),
            ["audio.clipCapabilities"] = (request, _) =>
                LiteAudioClipOperations.Capabilities(request.Args),
            [LiteAudioClipOperations.RemoveCapabilitiesOperation] = (request, _) =>
                LiteAudioClipOperations.RemoveCapabilities(request.Args),
            [LiteAudioClipOperations.AppendCapabilitiesOperation] = (request, _) =>
                LiteAudioClipOperations.AppendCapabilities(request.Args),
            [LiteAudioClipOperations.ExportOperation] = (request, events) =>
                LiteAudioClipOperations.Export(request.Args, events),
            [LiteAudioClipOperations.RemoveOperation] = (request, events) =>
                RemoveAudioClip(request.Args, events),
            [LiteAudioClipOperations.AppendOperation] = (request, events) =>
                AppendAudioClip(request.Args, events),
            [LiteAudioClipOperations.CancelOperation] = (request, _) =>
                LiteAudioClipOperations.Cancel(request.Args),
            ["library.collationKeys"] = (request, _) => CollationKeys(request.Args),
            // LEWA STRZALKA na liscie: krotka informacja uzupelniajaca.
            // Port drogi ``AnnounceQuickMediaInformation`` (cs:5485-5541):
            // host MIERZY brakujace parametry PRAWDZIWYM silnikiem i sklada
            // napis PRAWDZIWYM formatterem Core. Nic nie odtwarza, nie zmienia
            // wyboru i NIE ZAPISUJE profilu -- wlascicielem zapisu zostaje
            // pelne AMC, a port wx czyta profil tylko do odczytu.
            [LiteQuickInformation.Operation] = (request, _) => QuickInformation(request.Args),
            // ZYWA kolejka. "set" tylko wczytuje stan i NIC nie odtwarza.
            ["queue.set"] = (request, _) => QueueSet(request.Args),
            ["queue.toggleMembership"] = (request, _) => QueueToggleMembership(request.Args, playNext: false),
            ["queue.togglePlayNext"] = (request, _) => QueueToggleMembership(request.Args, playNext: true),
            ["queue.status"] = (_, _) => QueueStatusPayload(),
            ["queue.playAt"] = (request, events) => QueuePlayAt(request.Args, events),
            ["queue.next"] = (_, events) => QueueRelative(1, events),
            ["queue.previous"] = (_, events) => QueueRelative(-1, events)
        };
    }

    private LiteProfileMutationStore RequireProfileMutations() =>
        _profileMutations ?? throw new LiteRequestException(
            "Host nie dostał pełnych ścieżek profilu AMC. Edycja jest niedostępna.");

    private object RenameLibraryTitle(JsonElement args) =>
        RequireProfileMutations().RenameLibraryItem(
            LiteArgs.RequireText(args, "itemId"),
            LiteArgs.RequireText(args, "title"));

    private object RenameLocalFile(JsonElement args)
    {
        var itemId = LiteArgs.RequireText(args, "itemId");
        if (IsCurrentFilesItem(itemId)) StopAll();
        return RequireProfileMutations().RenameLocalFile(
            itemId,
            LiteArgs.RequireText(args, "name"));
    }

    private object RenamePodcastSubscription(JsonElement args) =>
        RequireProfileMutations().RenamePodcastSubscription(
            LiteArgs.RequireText(args, "subscriptionId"),
            LiteArgs.RequireText(args, "title"));

    private object TogglePodcastFavorites(JsonElement args)
    {
        var subscriptions = ReadOptionalIds(args, "subscriptionIds");
        var episodes = ReadOptionalIds(args, "episodeIds");
        if (subscriptions.Length + episodes.Length == 0)
            throw new LiteRequestException("Wybierz podcast albo odcinek.");
        var store = _podcastProgressStore
            ?? throw new LiteRequestException(
                "Host nie dostał bazy Podcastów i YouTube. Zmiana ulubionych jest niedostępna.");
        var result = store.ToggleFavorites(subscriptions, episodes);
        return new
        {
            favorite = result.Favorite,
            requested = result.RequestedCount,
            changed = result.ChangedCount
        };
    }

    private object EditRadioStation(JsonElement args)
    {
        var stationId = LiteArgs.RequireText(args, "stationId");
        var result = RequireProfileMutations().EditRadioStation(
            stationId,
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));
        // Pełne AMC zatrzymuje aktualną stację tylko po zmianie adresu.
        // Nazwę można poprawić bez przerywania słuchania.
        if (result.StreamChanged && IsCurrentRadioItem(stationId)) StopAll();
        return result;
    }

    private object RemoveProfileItems(JsonElement args) =>
        RequireProfileMutations().Remove(
            LiteArgs.RequireText(args, "sessionId"),
            LiteArgs.RequireText(args, "view"),
            ReadIds(args));

    private object RecycleLocalFiles(JsonElement args)
    {
        var ids = ReadIds(args);
        if (ids.Any(IsCurrentFilesItem)) StopAll();
        return RequireProfileMutations().RecycleLocalFiles(ids);
    }

    private bool IsCurrentFilesItem(string itemId)
    {
        lock (_gate)
        {
            return string.Equals(_activeEngine, "files", StringComparison.Ordinal)
                && string.Equals(_filesItem?.Id, itemId, StringComparison.Ordinal);
        }
    }

    private bool IsCurrentRadioItem(string itemId)
    {
        lock (_gate)
        {
            return string.Equals(_activeEngine, "radio", StringComparison.Ordinal)
                && string.Equals(_radioItem?.Id, itemId, StringComparison.Ordinal);
        }
    }

    private static string[] ReadIds(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("itemIds", out var values)
            || values.ValueKind != JsonValueKind.Array)
            throw new LiteRequestException("Brak listy elementów.");
        var result = values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(1_000)
            .ToArray();
        if (result.Length == 0) throw new LiteRequestException("Brak listy elementów.");
        return result;
    }

    private static string[] ReadOptionalIds(JsonElement args, string propertyName)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(propertyName, out var values))
            return [];
        if (values.ValueKind != JsonValueKind.Array)
            throw new LiteRequestException($"Argument \"{propertyName}\" nie jest listą.");
        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString()?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(1_000)
            .ToArray();
    }

    private object RemoveAudioClip(JsonElement args, LiteEventSink events)
    {
        var requestedPath = LiteArgs.RequirePath(args, "sourcePath");
        string currentId;
        lock (_gate)
        {
            if (_activeEngine != "files"
                || _filesItem is null
                || string.IsNullOrWhiteSpace(_filesItem.Source)
                || !SameLocalPath(_filesItem.Source, requestedPath))
            {
                throw new LiteRequestException(
                    "Bieżący plik zmienił się przed rozpoczęciem edycji. Oryginalny plik nie został zmieniony.");
            }
            currentId = _filesItem.Id;
        }

        // Zwolnij uchwyt dekodera przed oczekiwaniem na wyłączny dostęp.
        // Edytor i tak sprawdza dostęp ponownie; ten krok odpowiada kolejności
        // pełnego AMC i nie pozwala własnemu odtwarzaczowi blokować pliku.
        StopAll();
        events.Publish("audio.clipRemoveStarted", new
        {
            operationId = LiteArgs.ReadText(args, "operationId") ?? string.Empty,
            name = Path.GetFileName(requestedPath)
        });
        var result = LiteAudioClipOperations.Remove(args, events);

        lock (_gate)
        {
            if (_filesItem is not null
                && string.Equals(_filesItem.Id, currentId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(_filesItem.Source)
                && SameLocalPath(_filesItem.Source, result.SourcePath))
            {
                var duration = TimeSpan.FromSeconds(result.DurationSeconds);
                _filesItem.Duration = duration;
                if (result.SampleRateHz > 0) _filesItem.SampleRateHz = result.SampleRateHz;
                _filesItem.BitrateKbps = LocalAudioFileDiscovery.EstimateBitrateKbps(
                    new FileInfo(result.SourcePath).Length,
                    duration);
                _filesItem.IsBitrateEstimated = _filesItem.BitrateKbps.HasValue;
                _filesDurationId = currentId;
                _filesDuration = duration;
                _paused = false;
            }
        }

        return new
        {
            operationId = result.OperationId,
            path = result.SourcePath,
            name = result.SourceName,
            backupPath = result.BackupPath,
            durationSeconds = result.DurationSeconds,
            sampleRateHz = result.SampleRateHz,
            keepBackup = result.KeepBackup
        };
    }

    private object AppendAudioClip(JsonElement args, LiteEventSink events)
    {
        var requestedSource = LiteArgs.RequirePath(args, "sourcePath");
        lock (_gate)
        {
            if (_activeEngine != "files"
                || _filesItem is null
                || string.IsNullOrWhiteSpace(_filesItem.Source)
                || !SameLocalPath(_filesItem.Source, requestedSource))
            {
                throw new LiteRequestException(
                    "Bieżący plik zmienił się przed rozpoczęciem dopisywania. Pliki nie zostały zmienione.");
            }
        }

        // Źródło pozostaje odtwarzane: dopisywanie zmienia wyłącznie osobny
        // plik docelowy, tak samo jak okno pełnego AMC. Wspólny appender sam
        // blokuje cel, buduje wynik obok niego i podmienia go dopiero po
        // sprawdzeniu długości i tożsamości.
        var result = LiteAudioClipOperations.Append(args, events);
        return new
        {
            operationId = result.OperationId,
            path = result.TargetPath,
            name = result.TargetName,
            backupPath = result.BackupPath,
            targetDurationBeforeSeconds = result.TargetDurationBeforeSeconds,
            appendedDurationSeconds = result.AppendedDurationSeconds,
            targetDurationAfterSeconds = result.TargetDurationAfterSeconds,
            targetWasReencoded = result.TargetWasReencoded,
            reencodeWarning = result.ReencodeWarning,
            keepBackup = result.KeepBackup
        };
    }

    private static bool SameLocalPath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private object AddBookmark(JsonElement args)
    {
        if (_bookmarkStore is null)
            throw new LiteRequestException("Zapisywanie zakładek nie ma dostępu do Biblioteki AMC.");

        var itemId = LiteArgs.RequireText(args, "itemId");
        var itemTitle = LiteArgs.RequireText(args, "itemTitle");
        TimeSpan position;
        TimeSpan duration;
        lock (_gate)
        {
            if (_activeEngine != "files" || _filesItem is null)
                throw new LiteRequestException("Zakładkę można dodać w odtwarzaczu otwartego pliku.");
            position = _files.Position;
            duration = string.Equals(_filesDurationId, _filesItem.Id, StringComparison.Ordinal)
                ? _filesDuration
                : _filesItem.Duration;
        }
        if (duration <= TimeSpan.Zero)
            throw new LiteRequestException("Nie można dodać zakładki: czas trwania materiału jest nieznany.");
        return _bookmarkStore.Add(itemId, itemTitle, position, DateTime.UtcNow);
    }

    /// <summary>
    /// Wczytanie zywej kolejki. Celowo NIE startuje odtwarzania: sam odczyt
    /// zapisanego stanu nie ma prawa niczego zagrac.
    /// </summary>
    private object QueueSet(JsonElement args)
    {
        var status = _queue.Set(args);
        return QueuePayload(status);
    }

    private object QueueStatusPayload() => QueuePayload(_queue.Status());

    /// <summary>
    /// Grupowa zmiana kolejki z listy wxPython. Nie dotyka transportu: aktywny
    /// utwor gra dalej, a zmiana obowiazuje przy kolejnym przejsciu.
    /// </summary>
    private object QueueToggleMembership(JsonElement args, bool playNext)
    {
        var result = _queue.ToggleMembership(args, playNext);
        return QueuePayload(result.Status, added: result.Added, changed: result.Changed);
    }

    private object QueuePlayAt(JsonElement args, LiteEventSink events)
    {
        _events = events;
        ConfigureOutputForSession("files");
        lock (_gate)
        {
            // Jedno slyszalne zrodlo naraz, jak w files.play.
            if (_activeEngine != "files") _radio.Stop();
            _activeEngine = "files";
            _paused = false;
        }
        // Dlugosc czyscimy PO starcie, znajac Id nowego utworu: zerowanie przed
        // Play kasowalo czas, ktory dekoder podawal w trakcie samego Play.
        var status = _queue.PlayAt(args);
        SyncCurrentFromQueue(status);
        // Glosnosc i tempo z ZADANIA musza stac sie stanem transportu hosta, bo
        // to z _volume/_rate odpowiada transport.status i od nich zaczynaja
        // transport.setVolume/setRate. Bez tego Enter z kolejki oddawal domyslne
        // 35 i tempo 1, mimo ze silnik dostal wartosci uzytkownika.
        lock (_gate)
        {
            _volume = _queue.CurrentVolume;
            _rate = _queue.CurrentRate;
        }
        return QueuePayload(status);
    }

    private object QueueRelative(int direction, LiteEventSink events)
    {
        _events = events;
        ConfigureOutputForSession("files");
        if (!_queue.PlayRelative(direction))
        {
            // Odmowa jest WYNIKIEM, nie bledem: na koncu kolejki nie ma gdzie
            // isc i nie zawijamy sie na druga strone.
            return QueuePayload(_queue.Status(), moved: false);
        }
        var status = _queue.Status();
        lock (_gate)
        {
            if (_activeEngine != "files") _radio.Stop();
            _activeEngine = "files";
        }
        SyncCurrentFromQueue(status);
        return QueuePayload(status, moved: true);
    }

    /// <summary>
    /// Zgranie biezacego materialu kolejki ze stanem transportu hosta, zeby
    /// <c>transport.status</c> podawal PRAWDZIWY biezacy utwor, a nie poprzedni.
    /// Material bierzemy Z KOLEJKI (ta sama instancja, ktora dostal silnik), a
    /// nie z pol odpowiedzi: zlozony na nowo obiekt gubil sciezke zrodla, gdy
    /// wiersz przestal byc "biezacy" w wyniku zuzycia pozycji.
    /// </summary>
    private void SyncCurrentFromQueue(LiteQueueCoordinator.QueueStatus status)
    {
        var current = _queue.CurrentItem;
        if (current is null) return;
        lock (_gate)
        {
            _filesItem = current;
            ForgetStaleFilesDurationLocked(current.Id);
            _paused = status.Paused;
        }
    }

    /// <summary>
    /// Kasuje zapamietana dlugosc, GDY nie dotyczy ona podanego utworu. Wywolac
    /// trzymajac <see cref="_gate"/>.
    /// </summary>
    private void ForgetStaleFilesDurationLocked(string currentId)
    {
        if (string.Equals(_filesDurationId, currentId, StringComparison.Ordinal)) return;
        _filesDuration = TimeSpan.Zero;
        _filesDurationId = null;
    }

    private static object QueuePayload(
        LiteQueueCoordinator.QueueStatus status,
        bool? moved = null,
        bool? added = null,
        int? changed = null) =>
        new
        {
            rows = status.Rows
                .Select(row => new
                {
                    id = row.Id,
                    title = row.Title,
                    playNext = row.IsPlayNext,
                    current = row.IsCurrent
                })
                .ToArray(),
            count = status.Rows.Count,
            currentId = status.CurrentId,
            currentTitle = status.CurrentTitle,
            playing = status.Playing,
            paused = status.Paused,
            positionSeconds = status.PositionSeconds,
            // TRWALOSC widziana przez frontend. Bez tych pol odmowa zapisu
            // byla CICHA: okno nie miało z czego poznac, ze kolejka zyje tylko
            // w pamieci procesu.
            persistent = status.Persistent,
            persistError = status.PersistError,
            persistedWrites = status.PersistedWrites,
            restoredRows = status.RestoredRows,
            // Czas, z ktorego pojdzie SWIADOME wznowienie biezacej pozycji.
            // Bez tego okno nie wie, czy zapowiedziec "wznow" czy "od poczatku".
            resumeSeconds = status.ResumeSeconds,
            // Jawny stan WCZYTANIA. Pusta lista po zuzyciu utworow to NIE to
            // samo, co kolejka nigdy nie wczytana: bez tego pola frontend bral
            // jedno za drugie i przywracal zapisany porzadek, czyli skonsumowane
            // utwory wracaly do kolejki.
            initialized = status.Initialized,
            moved,
            added,
            changed
        };

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

    private static object RadioScheduleLabels(JsonElement args)
    {
        if (!args.TryGetProperty("schedules", out var source)
            || source.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Brak listy harmonogramów");
        }
        if (source.GetArrayLength() > 10_000)
            throw new LiteRequestException("Lista harmonogramów jest zbyt długa");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        var schedules = JsonSerializer.Deserialize<List<RadioRecordingScheduleSettings>>(
            source.GetRawText(), options) ?? [];
        var activeIds = args.TryGetProperty("activeIds", out var activeSource)
            && activeSource.ValueKind == JsonValueKind.Array
            ? activeSource.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        return new
        {
            schedules = schedules
                .OrderBy(schedule => schedule.NextStartUtcTicks)
                .Select(schedule => new
                {
                    id = schedule.Id,
                    navigationText = RadioSchedulePresentation.DisplayName(schedule),
                    label = RadioSchedulePresentation.BuildLabel(
                        schedule,
                        activeIds.Contains(schedule.Id)),
                    enabled = schedule.Enabled
                })
                .ToArray()
        };
    }

    private const string AmcPlMode = "AMC_PL";
    private const string TitleIgnoreCaseMode = "TITLE_IGNORE_CASE";
    private const string OrdinalIgnoreCaseMode = "ORDINAL_IGNORE_CASE";

    /// <summary>
    /// Czas na pomiar metadanych. Takie same jak w pelnym AMC
    /// (<c>MainWindow.xaml.cs:5493</c> i <c>:5519</c>): plik 5 s, strumien 6 s.
    /// Granica jest potrzebna, bo plik w chmurze albo milczaca stacja potrafia
    /// zawiesic odczyt, a klawisz ma ODPOWIEDZIEC, nie zablokowac okna.
    /// </summary>
    private static readonly TimeSpan FileMetadataTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StreamMetadataTimeout = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Krotka informacja uzupelniajaca o ZAZNACZONYM wierszu (LEWA STRZALKA).
    /// Port <c>AnnounceQuickMediaInformation</c> (<c>cs:5485-5541</c>).
    ///
    /// Tu dzieje sie POMIAR: rozmiar z <see cref="FileInfo"/>, parametry pliku
    /// z <see cref="WindowsMediaOutput.TryReadMetadataAsync"/>, parametry stacji
    /// z <see cref="RadioMediaOutput.TryReadStreamMetadataAsync"/>, a atrybut
    /// chmury z <see cref="CloudFileAvailability.MayRequireRemoteAccess"/>.
    /// Skladanie napisu nalezy do <see cref="LiteQuickInformation"/>, ktory wola
    /// formatter Core.
    ///
    /// CZEGO TA DROGA NIE ROBI, swiadomie:
    ///   * nie siega po ZAZNACZENIE ani po to, co GRA -- material opisuje
    ///     wylacznie zadanie; mieszanie obu dalo by parametry nie tego wiersza,
    ///   * nie dotyka odtwarzania (zadnego Play/Pause/Seek),
    ///   * nie zapisuje profilu,
    ///   * nie HYDRATUJE pliku z chmury: placeholdera tylko oglasza.
    /// </summary>
    private object QuickInformation(JsonElement args)
    {
        var request = LiteQuickInformation.ReadRequest(args);
        var probe = ProbeQuickInformation(request);
        return new
        {
            // ID wraca NAPISEM i NIEZMIENIONE: po nim frontend rozpoznaje, czy
            // odpowiedz dotyczy wiersza, ktory JESZCZE jest zaznaczony.
            itemId = request.ItemId,
            session = request.Session,
            text = LiteQuickInformation.Build(request, probe, CultureInfo.CurrentCulture)
        };
    }

    private static LiteQuickInfoProbe ProbeQuickInformation(LiteQuickInfoRequest request)
    {
        if (!LiteQuickInformation.IsLocalSource(request.Source))
        {
            // Stacja radiowa. Pomiar strumienia TYLKO wtedy, gdy brakuje
            // bitrate albo kodeka -- warunek z cs:5513-5518. Inaczej kazde
            // nacisniecie klawisza siegalo by do sieci bez potrzeby.
            if (!LiteQuickInformation.RequiresMetadata(request))
            {
                // Kompletne dane stacji albo inne zdalne źródło (np. podcast
                // z rozmiarem i MIME z kanału) nie wymagają pomiaru radia.
                return new LiteQuickInfoProbe();
            }
            var stream = RadioMediaOutput
                .TryReadStreamMetadataAsync(request.Source, StreamMetadataTimeout)
                .GetAwaiter()
                .GetResult();
            return stream is null
                ? new LiteQuickInfoProbe()
                : new LiteQuickInfoProbe
                {
                    // NORMALIZACJA jest czescia wzorca (cs:5531): surowy bitrate
                    // ze strumienia bywa w bitach/s albo absurdalny, a
                    // ``RadioAudioMetadataRules`` odrzuca wartosci nie do
                    // uwierzenia. Bez tego kroku stacja oglaszalaby liczbe,
                    // ktorej pelne AMC nigdy nie wypowiada.
                    BitrateKbps = RadioAudioMetadataRules.NormalizeBitrateKbps(stream.BitrateKbps),
                    SampleRateHz = stream.SampleRateHz,
                    Codec = stream.Codec
                };
        }

        // Plik lokalny. Atrybuty chmury czytamy ZAWSZE: to jedyna informacja,
        // ktora nie wymaga otwarcia pliku.
        var localPath = LiteQuickInformation.LocalPath(request.Source);
        var mayRequireCloudDownload = CloudFileAvailability.MayRequireRemoteAccess(localPath);
        long? sizeBytes = null;
        var exists = false;
        try
        {
            var file = new FileInfo(localPath);
            exists = file.Exists;
            // Placeholdera chmury NIE mierzymy: ``FileInfo.Length`` dla niego
            // jest wielkoscia logiczna, ale pelne AMC i tak oglasza wtedy
            // ostrzezenie, a my nie wywolujemy pobierania w tle dla samego
            // odczytu rozmiaru.
            if (exists) sizeBytes = mayRequireCloudDownload ? null : file.Length;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // cs:5463-5466 -- pozostale dane nadal sa uzyteczne, gdy plik jest
            // chwilowo niedostepny. Brak rozmiaru zostaje BRAKIEM, nie zerem.
        }

        int? sampleRateHz = null;
        long? measuredDurationTicks = null;
        if (exists && !mayRequireCloudDownload && LiteQuickInformation.RequiresMetadata(request))
        {
            var metadata = WindowsMediaOutput
                .TryReadMetadataAsync(localPath, FileMetadataTimeout)
                .GetAwaiter()
                .GetResult();
            if (metadata.Success)
            {
                if (metadata.SampleRateHz > 0) sampleRateHz = metadata.SampleRateHz;
                // cs:5504-5508 -- zmierzony czas uzupelnia BRAK czasu w wierszu.
                // Jest potrzebny takze po to, zeby dalo sie oszacowac bitrate z
                // rozmiaru; bez niego plik bez znanej dlugosci milczalby o nim.
                if (metadata.Duration > TimeSpan.Zero) measuredDurationTicks = metadata.Duration.Ticks;
            }
        }

        return new LiteQuickInfoProbe
        {
            Exists = exists,
            SizeBytes = sizeBytes,
            SampleRateHz = sampleRateHz,
            DurationTicks = measuredDurationTicks,
            MayRequireCloudDownload = mayRequireCloudDownload
        };
    }

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
        TrySaveCurrentPodcastProgress();

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
            ForgetStaleFilesDurationLocked(item.Id);
            _volume = volume;
            _rate = rate;
            _paused = false;
        }
        // BEZPOSREDNIE odtworzenie wychodzi z kolejki: koniec tego utworu nie
        // moze jej przesunac. Zachowanie files.play pozostaje niezmienione.
        _queue.DetachFromDirectPlay();
        ConfigureOutputForSession("files");
        _files.Play(item, position, volume, rate);
        return new { ok = true, engine = "files", id = item.Id, title = item.Title };
    }

    /// <summary>
    /// Odtwarza material spoza lokalnego drzewa plikow (na poczatku odcinek
    /// podcastu lub material YouTube) tym samym WindowsMediaOutput, ktorego
    /// uzywa pelne AMC. Zachowuje stabilne Id z profilu; nie przerabia go na
    /// <c>file:path</c>, dzieki czemu zdarzenia mozna przypisac do odcinka.
    /// </summary>
    private object PlayMedia(JsonElement args, LiteEventSink events)
    {
        _events = events;
        var source = LiteArgs.RequireText(args, "source").Trim();
        var isRemote = Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
        if (!isRemote)
        {
            try
            {
                source = Path.GetFullPath(source);
            }
            catch (Exception exception) when (exception is ArgumentException
                                              or NotSupportedException
                                              or PathTooLongException)
            {
                throw new LiteRequestException("Źródło materiału jest nieprawidłowe.");
            }
            if (!File.Exists(source))
                throw new LiteRequestException("Nie znaleziono pliku materiału.");
        }
        TrySaveCurrentPodcastProgress();

        var position = TimeSpan.FromSeconds(LiteArgs.ReadDouble(
            args, "positionSeconds", 0d, 0d, 604_800d));
        var volume = LiteArgs.ReadInt(args, "volume", _volume, 0, 100);
        var rate = LiteArgs.ReadDouble(args, "rate", _rate, 0.5d, 2.0d);
        var item = new MediaItem
        {
            Id = LiteArgs.RequireText(args, "id"),
            Title = LiteArgs.ReadText(args, "title") ?? "Materiał",
            Kind = MediaItemKind.Episode,
            Source = source
        };

        lock (_gate)
        {
            if (_activeEngine != "files") _radio.Stop();
            _activeEngine = "files";
            _filesItem = item;
            _currentPodcastCompleted = false;
            ForgetStaleFilesDurationLocked(item.Id);
            _volume = volume;
            _rate = rate;
            _paused = false;
        }
        _queue.DetachFromDirectPlay();
        ConfigureOutputForSession("podcasts");
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
        TrySaveCurrentPodcastProgress();

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
        // Radio TEZ wychodzi z kolejki: dotad odlaczalo ja tylko files.play,
        // wiec koniec utworu z czasu przed stacja mogl ja cicho przesunac.
        _queue.DetachFromDirectPlay();
        ConfigureOutputForSession("radio");
        _radio.Play(item, TimeSpan.Zero, volume, 1d);
        return new { ok = true, engine = "radio", id = item.Id, title = item.Title };
    }

    private object PauseResume()
    {
        // Gdy transport prowadzi KOLEJKA, pauza idzie przez jej sesje: tylko ona
        // zapamietuje pozycje tak, jak reszta AMC. Pytamy PRZED wejsciem w _gate,
        // zeby nie trzymac dwoch blokad naraz.
        string engine;
        string? currentId;
        lock (_gate)
        {
            engine = _activeEngine;
            currentId = _filesItem?.Id;
        }
        if (engine == "files" && _queue.OwnsCurrent(currentId))
        {
            // Czas notujemy PRZED pauza, z zywego wyjscia.
            _queue.NotePosition(_files.Position);
            var status = _queue.PauseResume();
            lock (_gate) _paused = status.Paused;
            // Pauza to swiadome przerwanie: utrwalamy punkt wznowienia.
            // Wznowienie gry tez zapisuje -- czas jest wtedy wciaz wazny, a
            // zapis i tak pomija checkpoint bez zmiany.
            _queue.SaveResumeCheckpoint();
            return new { paused = status.Paused, engine = "files", queue = true };
        }

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
                return new { paused = false, engine = _activeEngine, queue = false };
            }

            _paused = true;
            if (_activeEngine == "radio") _radio.Pause();
            else _files.Pause();
            return new { paused = true, engine = _activeEngine, queue = false };
        }
    }

    /// <summary>
    /// Tekst Alt+D sklada host C# tym samym formatterem parametrow audio, co
    /// pelne AMC. Frontend dostaje gotowa, celowa etykiete dla uzytkownika,
    /// nigdy reprezentacje obiektu ani identyfikator urzadzenia lub stacji.
    /// </summary>
    private object CurrentRadioBroadcastInformation()
    {
        MediaItem? item;
        lock (_gate)
        {
            item = _activeEngine == "radio" ? _radioItem : null;
        }
        if (item is null)
        {
            throw new LiteRequestException("Najpierw uruchom stację radiową.");
        }

        var parts = new List<string> { item.Title };
        var audio = AudioParametersFormatter.FormatCompact(item);
        if (!string.IsNullOrWhiteSpace(audio)) parts.Add(audio);

        var streamTitle = _radio.CurrentStreamTitle;
        if (!string.IsNullOrWhiteSpace(streamTitle)
            && !NowPlayingParts.SameValue(item.Title, streamTitle))
        {
            parts.Add(streamTitle.Trim());
        }
        else
        {
            parts.Add("brak nazwy bieżącej audycji lub utworu");
        }
        return new { text = string.Join(", ", parts) };
    }

    private object StopAll()
    {
        TrySaveCurrentPodcastProgress();
        // Gdy transport prowadzi KOLEJKA, zatrzymanie idzie przez jej sesje.
        // Samo _files.Stop() zostawialo sesje w stanie "gra" (IsPlaying), wiec
        // kolejne pauza/wznowienie i queue.status klamaly o dzwieku.
        // Prowadzenia NIE gasimy: zatrzymanie nie jest wyjsciem poza kolejke,
        // wiec wznowienie wraca na zapamietana pozycje tego samego utworu.
        string engine;
        string? currentId;
        lock (_gate)
        {
            engine = _activeEngine;
            currentId = _filesItem?.Id;
        }
        if (engine == "files" && _queue.OwnsCurrent(currentId))
        {
            // Pozycje notujemy PRZED zatrzymaniem: po Stop wyjscie nie zna
            // juz czasu, a to jest moment, w ktorym uzytkownik przerwal.
            _queue.NotePosition(_files.Position);
            var status = _queue.Stop();
            _radio.Stop();
            lock (_gate) _paused = status.Paused;
            // Zatrzymanie to swiadome przerwanie -- utrwalamy punkt wznowienia
            // od razu, bez czekania na zamkniecie procesu.
            _queue.SaveResumeCheckpoint();
            return new { ok = true, queue = true, playing = status.Playing };
        }

        // Bezposrednie odtwarzanie: zatrzymanie konczy tez prowadzenie przez
        // kolejke, zeby stary koniec utworu jej nie przesunal.
        _queue.DetachFromDirectPlay();
        _files.Stop();
        _radio.Stop();
        lock (_gate) _paused = false;
        return new { ok = true, queue = false, playing = false };
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
        // Przesuniecie czasu to ZDARZENIE WLASCICIELA: jesli transport prowadzi
        // kolejka, nowa pozycja ma trafic do punktu wznowienia. Koordynator sam
        // odrzuci czas, gdy material nie nalezy do kolejki.
        _queue.NotePosition(target);
        return new { engine, positionSeconds = target.TotalSeconds };
    }

    private object SetVolume(JsonElement args)
    {
        var volume = LiteArgs.ReadInt(args, "volume", _volume, 0, 100);
        lock (_gate) _volume = volume;
        // Gdy transport prowadzi KOLEJKA, glosnosc idzie przez jej sesje: tylko
        // wtedy obowiazuje tez NASTEPNY utwor. Samo _files.SetVolume ruszalo
        // wyjscie, a kolejny Play wracal do wartosci sesji.
        var byQueue = _queue.SetVolume(volume);
        if (!byQueue) _files.SetVolume(volume);
        _radio.SetVolume(volume);
        return new { volume, queue = byQueue };
    }

    private object SetRate(JsonElement args)
    {
        var rate = LiteArgs.ReadDouble(args, "rate", _rate, 0.5d, 2.0d);
        string engine;
        lock (_gate)
        {
            _rate = rate;
            engine = _activeEngine;
        }
        if (engine == "radio")
        {
            _radio.SetPlaybackRate(rate);
            return new { rate, engine, queue = false };
        }
        // Jak przy glosnosci: przez sesje kolejki, zeby tempo przetrwalo przejscie.
        var byQueue = _queue.SetRate(rate);
        if (!byQueue) _files.SetPlaybackRate(rate);
        return new { rate, engine, queue = byQueue };
    }

    private object SaveCurrentPodcastProgress()
    {
        if (_podcastProgressStore is null)
            throw new LiteRequestException(
                "Zapisywanie postępu nie ma dostępu do biblioteki Podcastów i YouTube.");

        string episodeId;
        TimeSpan duration;
        bool completed;
        lock (_gate)
        {
            if (_activeEngine != "files" || _filesItem?.Kind != MediaItemKind.Episode)
                throw new LiteRequestException("Nie jest teraz odtwarzany odcinek podcastu.");
            episodeId = _filesItem.Id;
            duration = string.Equals(_filesDurationId, episodeId, StringComparison.Ordinal)
                ? _filesDuration
                : _filesItem.Duration;
            completed = _currentPodcastCompleted;
        }
        var position = completed ? TimeSpan.Zero : _files.Position;
        return _podcastProgressStore.Save(episodeId, position, duration, completed);
    }

    private object RefreshPodcasts(JsonElement args)
    {
        if (_podcastRefresh is null)
        {
            throw new LiteRequestException(
                "Odświeżanie nie ma dostępu do biblioteki Podcastów i YouTube.");
        }
        return _podcastRefresh.Refresh(args);
    }

    private object DownloadPodcastEpisodes(JsonElement args, LiteEventSink events)
    {
        if (_podcastDownloads is null)
        {
            throw new LiteRequestException(
                "Pobieranie nie ma dostępu do biblioteki Podcastów i YouTube.");
        }
        return _podcastDownloads.Download(args, events);
    }

    private object AddPodcastSource(JsonElement args)
    {
        if (_podcastAdd is null)
        {
            throw new LiteRequestException(
                "Dodawanie nie ma dostępu do biblioteki Podcastów i YouTube.");
        }
        return _podcastAdd.Add(args);
    }

    private void TrySaveCurrentPodcastProgress()
    {
        if (_podcastProgressStore is null) return;
        string? episodeId;
        TimeSpan duration;
        bool completed;
        lock (_gate)
        {
            if (_activeEngine != "files" || _filesItem?.Kind != MediaItemKind.Episode) return;
            episodeId = _filesItem.Id;
            duration = string.Equals(_filesDurationId, episodeId, StringComparison.Ordinal)
                ? _filesDuration
                : _filesItem.Duration;
            completed = _currentPodcastCompleted;
        }
        TrySavePodcastProgress(
            episodeId,
            completed ? TimeSpan.Zero : _files.Position,
            duration,
            completed);
    }

    private void TrySavePodcastProgress(
        string episodeId,
        TimeSpan position,
        TimeSpan duration,
        bool completed)
    {
        if (_podcastProgressStore is null) return;
        try
        {
            _podcastProgressStore.Save(episodeId, position, duration, completed);
            _lastPodcastProgressError = null;
        }
        catch (Exception exception)
        {
            var message = exception.Message;
            if (string.Equals(message, _lastPodcastProgressError, StringComparison.Ordinal)) return;
            _lastPodcastProgressError = message;
            Publish("podcast.progressSaveFailed", new { message });
            Console.Error.WriteLine("[lite-host] nie zapisano postępu podcastu: " + message);
        }
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
            // Oddajemy czas TYLKO wtedy, gdy zmierzono go dla tego samego Id.
            filesDuration = item is not null
                && string.Equals(_filesDurationId, item.Id, StringComparison.Ordinal)
                    ? _filesDuration
                    : TimeSpan.Zero;
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
            // Sciezka jest czescia WEWNETRZNEGO protokolu, potrzebna do
            // sprawdzenia tozsamosci zaznaczenia i wywolania wspolnego
            // eksportera. Frontend nie uzywa jej jako nazwy kontrolki.
            source = engine == "files" ? item?.Source : null,
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

    private object PodcastOpmlInspect(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).Inspect(args);

    private object PodcastDownloadSaveAsInfo(JsonElement args) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).SaveAsInfo(args);

    private object PodcastDownloadSaveAs(JsonElement args, LiteEventSink events) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).SaveAs(args, events);

    private object PodcastOpmlImport(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).Import(args);

    private object PodcastOpmlExport(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).Export(args);

    private object PodcastYouTubeExport(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).ExportYouTube(args);

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

    private void ConfigureOutputForSession(string sessionId)
    {
        string? deviceId;
        lock (_gate) _outputDeviceIdsBySession.TryGetValue(sessionId, out deviceId);
        if (string.Equals(sessionId, "radio", StringComparison.Ordinal))
        {
            _radio.ConfigureOutputDevice(deviceId);
        }
        else
        {
            _files.ConfigureOutputDevice(deviceId);
        }
    }

    private object SelectOutput(JsonElement args)
    {
        var sessionId = LiteArgs.RequireText(args, "sessionId");
        if (sessionId is not ("files" or "radio" or "podcasts"))
            throw new LiteRequestException("Ta sesja nie obsługuje wyboru urządzenia audio.");
        var deviceId = LiteArgs.ReadText(args, "deviceId")?.Trim();
        var restart = LiteArgs.ReadBool(args, "restart", true);

        bool active;
        bool paused;
        MediaItem? item;
        int volume;
        double rate;
        lock (_gate)
        {
            _outputDeviceIdsBySession[sessionId] = deviceId;
            active = sessionId switch
            {
                "radio" => _activeEngine == "radio",
                "podcasts" => _activeEngine == "files"
                    && _filesItem?.Kind == MediaItemKind.Episode,
                _ => _activeEngine == "files"
                    && _filesItem?.Kind != MediaItemKind.Episode
            };
            paused = _paused;
            item = sessionId == "radio" ? _radioItem : _filesItem;
            volume = _volume;
            rate = _rate;
        }

        var rebuilt = false;
        if (active)
        {
            ConfigureOutputForSession(sessionId);
            if (restart && item is not null)
            {
                if (sessionId == "radio")
                {
                    rebuilt = _radio.RestartPlaybackOutput();
                }
                else if (_files.LoadedItemId is not null || _files.IsPreparing)
                {
                    var position = _files.Position;
                    _files.Stop();
                    if (!paused) _files.Play(item, position, volume, rate);
                    // Przy pauzie stary tor zostal domkniety, a nowy wybor
                    // wejdzie przy wznowieniu bez krotkiego dzwieku.
                    rebuilt = true;
                }
            }
        }

        var available = AudioOutputDeviceCatalog.IsAvailable(deviceId);
        return new
        {
            sessionId,
            deviceId,
            available,
            active,
            rebuilt,
            usingDefault = string.IsNullOrWhiteSpace(deviceId) || !available
        };
    }

    private static object ListOutputs(JsonElement args)
    {
        var selectedDeviceId = LiteArgs.ReadText(args, "selectedDeviceId");
        return new
        {
            devices = AudioOutputDeviceCatalog.Enumerate(selectedDeviceId)
                .Select(device => new { id = device.Id, name = device.Label, available = device.IsAvailable })
                .ToArray()
        };
    }

    public void Dispose()
    {
        // ZWYKLE ZAMKNIECIE HOSTA tez jest momentem zapisu: uzytkownik, ktory
        // zamyka okno w trakcie gry, ma wrocic tam, gdzie skonczyl. Czas bierzemy
        // z ZYWEGO wyjscia, zanim je zamkniemy -- po Dispose nie ma juz pozycji.
        // Blad zapisu nie moze przewrocic zamykania procesu.
        try
        {
            if (_queue.OwnsCurrent(_filesItem?.Id)) _queue.NotePosition(_files.Position);
            _queue.SaveResumeCheckpoint();
            TrySaveCurrentPodcastProgress();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("[lite-host] nie zapisano pozycji wznowienia: " + exception.Message);
        }

        // Najpierw domykamy prywatne tory nagrywania. Sa niezalezne od
        // slyszalnego radia, ale nadal korzystaja z tych samych bibliotek.
        _recordings.Dispose();
        _files.Dispose();
        _radio.Dispose();
        // Blokada wlasnosci MUSI pasc razem z hostem. Bez tego kolejny host na
        // tej samej kopii profilu dostawalby odmowe po juz zamknietym procesie,
        // czyli trwalosc dzialalaby raz.
        _queueStore?.Dispose();
        _podcastRefresh?.Dispose();
        _podcastDownloads?.Dispose();
        _podcastAdd?.Dispose();
        _podcastOpml?.Dispose();
        _podcastProgressStore?.Dispose();
    }
}
