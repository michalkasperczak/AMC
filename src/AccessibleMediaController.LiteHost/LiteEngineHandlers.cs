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
    private sealed record BookmarkNavigationCursor(
        string ItemId,
        string BookmarkId,
        bool NamedOnly,
        DateTime LastNavigationUtc);

    private sealed record TimelineContext(
        string Engine,
        string PlaybackItemId,
        MediaItem Item,
        TimeSpan Position,
        TimeSpan Duration,
        string StorageSessionId,
        string SessionName);

    private readonly object _gate = new();
    private readonly object _audioClipEditGate = new();
    private static readonly TimeSpan AudioClipPlaybackReleaseTimeout = TimeSpan.FromSeconds(15);
    private bool _audioClipRemovalInProgress;
    private readonly WindowsMediaOutput _files = new();
    private readonly RadioMediaOutput _radio;
    private readonly LiteRadioRecordingCoordinator _recordings;
    private readonly LiteRadioRecognitionCoordinator _recognition;
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
    private static readonly TimeSpan BookmarkNavigationContinuationWindow =
        TimeSpan.FromSeconds(5);
    private BookmarkNavigationCursor? _bookmarkNavigationCursor;
    private readonly LitePodcastProgressStore? _podcastProgressStore;
    private readonly LitePodcastRefreshCoordinator? _podcastRefresh;
    private readonly LitePodcastDownloadCoordinator? _podcastDownloads;
    private readonly LitePodcastAddCoordinator? _podcastAdd;
    private readonly LitePodcastOpmlCoordinator? _podcastOpml;
    private readonly LitePodcastChapterCoordinator? _podcastChapters;
    private readonly LitePodcastDirectorySearchCoordinator _podcastDirectorySearch = new();
    private readonly LiteRadioDirectorySearchCoordinator _radioDirectorySearch = new();
    private readonly LiteProfileMutationStore? _profileMutations;
    private readonly LiteTidalCatalogCoordinator? _tidalCatalog;
    private readonly LiteSpotifyCatalogCoordinator? _spotifyCatalog;
    private readonly LiteSpotifyPlaybackCoordinator? _spotifyPlayback;
    private readonly LiteCommonDataSnapshotCoordinator? _commonDataSnapshot;
    private readonly LiteTidalDesktopCoordinator _tidalDesktop = new();
    private readonly LiteWiiMCoordinator? _wiim;
    private readonly LiteFrontierSmartCoordinator? _frontierSmart;
    private readonly LiteSonosCoordinator? _sonos;
    private string? _lastPodcastProgressError;
    private bool _currentPodcastCompleted;

    /// <summary>
    /// Ostatni silnik wskazany przez polecenie bez sessionId. To wyłącznie
    /// zgodność ze starszym klientem protokołu; radio i pliki mogą grać
    /// równolegle, a nowy klient zawsze podaje sesję jawnie.
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
    private int _filesVolume = 35;
    private int _radioVolume = 35;
    private double _filesRate = 1d;
    private double _radioRate = 1d;
    // Tempo zadane przez sesje jest baza dla materialu. _filesRate moze byc
    // chwilowo zastapione indywidualnym ustawieniem podcastu lub odcinka,
    // dlatego nie wolno uzywac go do powrotu z takiego nadpisania.
    private double _requestedRate = 1d;
    private bool _filesPaused;
    private bool _radioPaused;
    private bool _filesMuted;
    private bool _radioMuted;
    private bool _filesPlaying;
    private bool _radioPlaying;
    private ResumePositionMode _podcastSessionResumeMode = ResumePositionMode.Inherit;
    private bool _pauseRadioAfterOutputFallback;
    private PlaybackAudioSettings _configuredAudioSettings = new();

    public LiteEngineHandlers(int timeshiftMinutes)
        : this(timeshiftMinutes, null, null, null, null, null)
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
        LiteProfileMutationStore? profileMutations = null,
        LiteTidalCatalogCoordinator? tidalCatalog = null,
        LiteWiiMCoordinator? wiim = null,
        LiteFrontierSmartCoordinator? frontierSmart = null,
        LiteSonosCoordinator? sonos = null,
        LiteSpotifyCatalogCoordinator? spotifyCatalog = null,
        LiteSpotifyPlaybackCoordinator? spotifyPlayback = null,
        LiteCommonDataSnapshotCoordinator? commonDataSnapshot = null)
    {
        _radio = new RadioMediaOutput(timeshiftMinutes);
        _queueStore = queueStore;
        _bookmarkStore = bookmarkStore;
        _recordings = new LiteRadioRecordingCoordinator(bookmarkStore);
        _podcastProgressStore = podcastProgressStore;
        _profileMutations = profileMutations;
        _tidalCatalog = tidalCatalog;
        _spotifyCatalog = spotifyCatalog;
        _spotifyPlayback = spotifyPlayback;
        _commonDataSnapshot = commonDataSnapshot;
        _wiim = wiim;
        _frontierSmart = frontierSmart;
        _sonos = sonos;
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
        _podcastChapters = podcastProgressStore is null || bookmarkStore is null
            ? null
            : new LitePodcastChapterCoordinator(podcastProgressStore, bookmarkStore);
        _queue = new LiteQueueCoordinator(_files, queueStore);
        _recognition = new LiteRadioRecognitionCoordinator(
            _radio,
            () =>
            {
                lock (_gate) return _radioPlaying ? _radioItem : null;
            },
            _recordings);

        _files.PlaybackFailed += (_, e) =>
        {
            lock (_gate) _filesPlaying = false;
            Publish("playback.failed",
                new { engine = "files", message = e.Message, title = e.Item?.Title });
        };
        _files.PlaybackStarted += (_, e) =>
        {
            lock (_gate)
            {
                _filesPlaying = true;
                _filesPaused = false;
            }
            var historyError = TryRecordPlaybackHistory(
                e.Item.Kind == MediaItemKind.Episode ? "podcasts" : "local",
                e.Item.Id);
            Publish("playback.started",
                new { engine = "files", title = e.Item.Title, id = e.Item.Id,
                    tempoFallbackReason = _files.TempoFallbackReason,
                    historyError });
        };
        _files.PlaybackEnded += (_, e) =>
        {
            lock (_gate) _filesPlaying = false;
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
            else if (!_queue.OwnsCurrent(e.Item.Id))
            {
                // Bezposredni lokalny plik zakonczony naturalnie nie moze
                // wrocic przy kolejnym Enterze na stary, koncowy czas.
                if (!string.IsNullOrWhiteSpace(e.Item.Source))
                {
                    _queue.SaveDirectResume(
                        e.Item.Id,
                        e.Item.Source,
                        TimeSpan.Zero,
                        completed: true);
                }
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
                lock (_audioClipEditGate)
                {
                    // Spoznione zdarzenie naturalnego konca nie moze ominac
                    // blokady i uruchomic kolejnego pliku podczas wycinania.
                    if (_audioClipRemovalInProgress) return;
                    next = _queue.HandlePlaybackEnded(e.Item.Id);
                }
            }
            catch (Exception exception)
            {
                // Blad przejscia nie moze zabic hosta ani zapetlic kolejki.
                Publish("playback.failed",
                    new { engine = "files", message = exception.Message, title = e.Item.Title });
                return;
            }
            if (next is null) return;

            if (_filesMuted) _files.SetVolume(0);

            lock (_gate)
            {
                _activeEngine = "files";
                _filesItem = next;
                _bookmarkNavigationCursor = null;
                // Nowy strumien: dlugosc POPRZEDNIEGO przestaje obowiazywac.
                // Zerujemy tylko wtedy, gdy zapamietany czas nie dotyczy JUZ
                // nowego utworu -- inaczej skasowalibysmy wartosc, ktora dekoder
                // zdazyl podac przed tym miejscem.
                ForgetStaleFilesDurationLocked(next.Id);
                _filesPaused = false;
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
                if (_filesItem is not null && string.Equals(_filesItem.Id, e.Item.Id, StringComparison.Ordinal))
                {
                    if (e.SampleRateHz > 0) _filesItem.SampleRateHz = e.SampleRateHz;
                    if (e.Duration > TimeSpan.Zero && File.Exists(_filesItem.Source))
                    {
                        try
                        {
                            _filesItem.BitrateKbps = LocalAudioFileDiscovery.EstimateBitrateKbps(
                                new FileInfo(_filesItem.Source).Length, e.Duration);
                            _filesItem.IsBitrateEstimated = _filesItem.BitrateKbps.HasValue;
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            Publish("playback.duration",
                new { engine = "files", id = e.Item.Id, seconds = e.Duration.TotalSeconds, sampleRateHz = e.SampleRateHz });
        };

        _radio.PlaybackFailed += (_, e) =>
        {
            lock (_gate) _radioPlaying = false;
            Publish("playback.failed",
                new { engine = "radio", message = e.Message, title = e.Item?.Title });
        };
        _radio.PlaybackStarted += (_, e) =>
        {
            lock (_gate)
            {
                _radioPlaying = true;
                _radioPaused = false;
            }
            var historyError = TryRecordPlaybackHistory("radio", e.Item.Id);
            Publish("playback.started",
                new { engine = "radio", title = e.Item.Title, id = e.Item.Id,
                    tempoFallbackReason = _radio.TempoFallbackReason,
                    historyError });
        };
        _radio.NowPlayingChanged += (_, e) => Publish("radio.nowPlaying",
            new { id = e.Item.Id, station = e.Item.Title, streamTitle = e.StreamTitle });
        _radio.OutputDeviceFallback += (_, e) =>
        {
            bool paused;
            lock (_gate)
            {
                paused = _pauseRadioAfterOutputFallback;
                if (paused) _radioPaused = true;
            }
            Publish("audio.outputFallback",
                new { engine = "radio", id = e.Item.Id, title = e.Item.Title, paused });
        };
    }

    private void Publish(string name, object data) => _events?.Publish(name, data);

    private string? TryRecordPlaybackHistory(string sessionId, string itemId)
    {
        if (_profileMutations is null) return null;
        try
        {
            _profileMutations.RecordPlaybackHistory(sessionId, itemId);
            return null;
        }
        catch (Exception exception)
        {
            // Start dźwięku już został potwierdzony przez prawdziwy silnik.
            // Awaria zapisu historii nie może zmienić go w fałszywy błąd
            // odtwarzania ani przerwać działającego materiału. Przekazujemy
            // przyczynę interfejsowi do logu i jednego komunikatu użytkowego.
            return exception.Message;
        }
    }

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
                    engine = "AMC WindowsMediaOutput + RadioMediaOutput + Spotify Librespot"
                };
            },
            ["host.shutdown"] = (_, _) => new { ok = true },
            [LiteCommonDataSnapshotCoordinator.ExportOperation] = (request, _) =>
                RequireCommonDataSnapshot().Export(request.Args),
            [LiteCommonDataSnapshotCoordinator.StatusOperation] = (_, _) =>
                RequireCommonDataSnapshot().Status(),
            [LiteCommonDataSnapshotCoordinator.ImportOperation] = (request, _) =>
                ImportCommonDataSnapshot(request.Args),
            [LiteCommonDataSnapshotCoordinator.BackupExportOperation] = (request, _) =>
                ExportBackup(request.Args),
            [LiteCommonDataSnapshotCoordinator.BackupInspectOperation] = (request, _) =>
                RequireCommonDataSnapshot().InspectBackup(request.Args),
            [LiteCommonDataSnapshotCoordinator.BackupImportOperation] = (request, _) =>
                ImportBackup(request.Args),
            ["files.listFolder"] = (request, _) => ListFolder(request.Args),
            ["files.play"] = (request, events) =>
                RunLocalPlaybackStart(() => PlayFile(request.Args, events)),
            ["media.play"] = (request, events) =>
                RunLocalPlaybackStart(() => PlayMedia(request.Args, events)),
            ["radio.play"] = (request, events) => PlayStation(request.Args, events),
            ["radio.currentBroadcastInformation"] = (_, _) =>
                CurrentRadioBroadcastInformation(),
            ["radio.recordingToggle"] = (request, events) =>
                _recordings.Toggle(request.Args, events),
            ["radio.recordingPauseToggle"] = (request, _) =>
                _recordings.TogglePause(request.Args),
            ["radio.recordingSplit"] = (request, _) =>
                _recordings.Split(request.Args),
            ["radio.recordingBookmark"] = (request, _) =>
                _recordings.AddBookmark(request.Args),
            ["radio.recordingStopAll"] = (_, _) => _recordings.StopAll(),
            ["radio.recordingStatus"] = (_, _) => _recordings.Status(),
            ["radio.recordingHistory"] = (_, _) => _recordings.History(),
            ["radio.scheduleSync"] = (request, events) =>
                _recordings.SyncSchedules(request.Args, events),
            ["radio.scheduleStatus"] = (_, _) => _recordings.ScheduleStatus(),
            ["radio.scheduleLabels"] = (request, _) => RadioScheduleLabels(request.Args),
            [LiteRadioRecognitionCoordinator.Operation] = (request, _) =>
                _recognition.Run(request.Args),
            ["transport.pauseResume"] = (request, _) => PauseResume(request.Args),
            ["transport.toggleMute"] = (request, _) => ToggleMute(request.Args),
            ["transport.stop"] = (request, _) => StopTransport(request.Args),
            ["transport.seek"] = (request, _) => Seek(request.Args),
            ["radio.jumpLive"] = (request, _) => JumpRadioToLive(request.Args),
            ["radio.seekPercent"] = (request, _) => SeekRadioPercent(request.Args),
            ["transport.setVolume"] = (request, _) => SetVolume(request.Args),
            ["transport.setRate"] = (request, _) => SetRate(request.Args),
            ["transport.status"] = (request, _) => Status(request.Args),
            ["podcast.checkpoint"] = (_, _) => SaveCurrentPodcastProgress(),
            ["local.checkpoint"] = (_, _) => SaveCurrentLocalResume(),
            ["local.resumeConfigure"] = (request, _) =>
                ConfigureLocalResume(request.Args),
            [LitePodcastRefreshCoordinator.Operation] = (request, _) =>
                RefreshPodcasts(request.Args),
            [LitePodcastRefreshCoordinator.AutomaticOperation] = (_, _) =>
                RefreshPodcastsAutomatically(),
            ["podcast.settingsGet"] = (_, _) => PodcastGlobalSettings(),
            ["podcast.settingsSet"] = (request, _) =>
                ConfigurePodcastGlobalSettings(request.Args),
            [LitePodcastDownloadCoordinator.Operation] = (request, events) =>
                DownloadPodcastEpisodes(request.Args, events),
            [LitePodcastDownloadCoordinator.SaveAsInfoOperation] = (request, _) =>
                PodcastDownloadSaveAsInfo(request.Args),
            [LitePodcastDownloadCoordinator.SaveAsOperation] = (request, events) =>
                PodcastDownloadSaveAs(request.Args, events),
            [LitePodcastDownloadCoordinator.VideoOperation] = (request, events) =>
                PodcastVideoDownload(request.Args, events),
            [LitePodcastDownloadCoordinator.VideoSaveAsInfoOperation] = (request, _) =>
                PodcastVideoSaveAsInfo(request.Args),
            [LitePodcastDownloadCoordinator.VideoSaveAsOperation] = (request, events) =>
                PodcastVideoSaveAs(request.Args, events),
            [LitePodcastAddCoordinator.Operation] = (request, _) =>
                AddPodcastSource(request.Args),
            [LiteYouTubeDescriptionCoordinator.Operation] = (request, _) =>
                LiteYouTubeDescriptionCoordinator.Read(request.Args),
            [LitePodcastDirectorySearchCoordinator.Operation] = (request, _) =>
                _podcastDirectorySearch.Search(request.Args),
            [LiteRadioDirectorySearchCoordinator.Operation] = (request, _) =>
                _radioDirectorySearch.Search(request.Args),
            [LitePodcastOpmlCoordinator.InspectOperation] = (request, _) =>
                PodcastOpmlInspect(request.Args),
            [LitePodcastOpmlCoordinator.ImportOperation] = (request, _) =>
                PodcastOpmlImport(request.Args),
            [LitePodcastOpmlCoordinator.ExportOperation] = (request, _) =>
                PodcastOpmlExport(request.Args),
            [LitePodcastOpmlCoordinator.ExportYouTubeOperation] = (request, _) =>
                PodcastYouTubeExport(request.Args),
            [LiteTidalCatalogContract.SearchOperation] = (request, _) =>
                TidalSearch(request.Args),
            [LiteTidalCatalogContract.AccountStatusOperation] = (_, _) =>
                TidalAccountStatus(),
            [LiteTidalCatalogContract.LoginOperation] = (_, _) =>
                TidalLogin(),
            [LiteTidalCatalogContract.ContainerItemsOperation] = (request, _) =>
                TidalContainerItems(request.Args),
            [LiteTidalCatalogContract.CollectionViewOperation] = (request, _) =>
                TidalCollectionView(request.Args),
            [LiteTidalCatalogContract.MembershipOperation] = (request, _) =>
                TidalCollectionMembership(request.Args),
            [LiteSpotifyCatalogContract.CapabilitiesOperation] = (_, _) =>
                SpotifyCapabilities(),
            [LiteSpotifyCatalogContract.AccountStatusOperation] = (_, _) =>
                SpotifyAccountStatus(),
            [LiteSpotifyCatalogContract.LoginOperation] = (_, _) =>
                SpotifyLogin(),
            [LiteSpotifyCatalogContract.CollectionRootOperation] = (_, _) =>
                SpotifyCollectionRoot(),
            [LiteSpotifyCatalogContract.CollectionViewOperation] = (request, _) =>
                SpotifyCollectionView(request.Args),
            [LiteSpotifyCatalogContract.ContainerItemsOperation] = (request, _) =>
                SpotifyContainerItems(request.Args),
            [LiteSpotifyCatalogContract.SearchOperation] = (request, _) =>
                SpotifySearch(request.Args),
            [LiteSpotifyCatalogContract.DescriptionOperation] = (request, _) =>
                SpotifyDescription(request.Args),
            [LiteSpotifyCatalogContract.MembershipOperation] = (request, _) =>
                SpotifyCollectionMembership(request.Args),
            [LiteSpotifyCatalogContract.PlaylistTargetsOperation] = (_, _) =>
                SpotifyPlaylistTargets(),
            [LiteSpotifyCatalogContract.PlaylistCreateOperation] = (request, _) =>
                SpotifyPlaylistCreate(request.Args),
            [LiteSpotifyCatalogContract.PlaylistAddItemsOperation] = (request, _) =>
                SpotifyPlaylistAddItems(request.Args),
            [LiteSpotifyPlaybackContract.PlayOperation] = (request, events) =>
                RequireSpotifyPlayback().Play(request.Args, events),
            [LiteTidalDesktopContract.PlayOperation] = (request, _) =>
                _tidalDesktop.Play(request.Args),
            [LiteTidalDesktopContract.TransportOperation] = (request, _) =>
                _tidalDesktop.Transport(request.Args),
            [LiteWiiMContract.DevicesOperation] = (_, _) =>
                RequireWiiM().ListDevices(),
            [LiteWiiMContract.SnapshotOperation] = (request, _) =>
                RequireWiiM().Snapshot(request.Args),
            [LiteWiiMContract.TransportOperation] = (request, _) =>
                RequireWiiM().Transport(request.Args),
            [LiteWiiMContract.PresetsOperation] = (request, _) =>
                RequireWiiM().Presets(request.Args),
            [LiteWiiMContract.ActivatePresetOperation] = (request, _) =>
                RequireWiiM().ActivatePreset(request.Args),
            [LiteWiiMContract.OptionsOperation] = (request, _) =>
                RequireWiiM().Options(request.Args),
            [LiteWiiMContract.SetOptionOperation] = (request, _) =>
                RequireWiiM().SetOption(request.Args),
            [LiteWiiMContract.ToggleShuffleOperation] = (request, _) =>
                RequireWiiM().ToggleShuffle(request.Args),
            [LiteWiiMContract.NavigateSourceOperation] = (request, _) =>
                RequireWiiM().NavigateSource(request.Args),
            [LiteFrontierSmartContract.DevicesOperation] = (_, _) =>
                RequireFrontierSmart().ListDevices(),
            [LiteFrontierSmartContract.DiscoverOperation] = (_, _) =>
                RequireFrontierSmart().Discover(),
            [LiteFrontierSmartContract.IdentifyOperation] = (request, _) =>
                RequireFrontierSmart().Identify(request.Args),
            [LiteFrontierSmartContract.AddOperation] = (request, _) =>
                RequireFrontierSmart().AddOrUpdate(request.Args),
            [LiteFrontierSmartContract.RemoveOperation] = (request, _) =>
                RequireFrontierSmart().Remove(request.Args),
            [LiteFrontierSmartContract.SelectOperation] = (request, _) =>
                RequireFrontierSmart().Select(request.Args),
            [LiteFrontierSmartContract.SnapshotOperation] = (request, _) =>
                RequireFrontierSmart().Snapshot(request.Args),
            [LiteFrontierSmartContract.TransportOperation] = (request, _) =>
                RequireFrontierSmart().Transport(request.Args),
            [LiteFrontierSmartContract.ModesOperation] = (request, _) =>
                RequireFrontierSmart().Modes(request.Args),
            [LiteFrontierSmartContract.SetModeOperation] = (request, _) =>
                RequireFrontierSmart().SetMode(request.Args),
            [LiteFrontierSmartContract.PresetsOperation] = (request, _) =>
                RequireFrontierSmart().Presets(request.Args),
            [LiteFrontierSmartContract.ActivatePresetOperation] = (request, _) =>
                RequireFrontierSmart().ActivatePreset(request.Args),
            [LiteWiiMContract.NetworkStreamsOperation] = (_, _) =>
                RequireWiiM().ListNetworkStreams(),
            [LiteWiiMContract.PlayNetworkStreamOperation] = (request, _) =>
                RequireWiiM().PlayNetworkStream(request.Args),
            [LiteWiiMContract.NetworkStreamEditInfoOperation] = (request, _) =>
                RequireWiiM().NetworkStreamEditInfo(request.Args),
            [LiteWiiMContract.AddNetworkStreamOperation] = (request, _) =>
                AddWiiMNetworkStream(request.Args),
            [LiteWiiMContract.EditNetworkStreamOperation] = (request, _) =>
                EditWiiMNetworkStream(request.Args),
            [LiteWiiMContract.RemoveNetworkStreamsOperation] = (request, _) =>
                RemoveWiiMNetworkStreams(request.Args),
            [LiteWiiMContract.ImportNetworkStreamsOperation] = (request, _) =>
                ImportWiiMNetworkStreams(request.Args),
            [LiteWiiMContract.ExportNetworkStreamsOperation] = (request, _) =>
                ExportWiiMNetworkStreams(request.Args),
            [LiteWiiMContract.SortNetworkStreamsOperation] = (request, _) =>
                SetWiiMNetworkStreamSortMode(request.Args),
            [LiteWiiMContract.MoveNetworkStreamsOperation] = (request, _) =>
                MoveWiiMNetworkStreams(request.Args),
            [LiteWiiMContract.PlaceNetworkStreamsOperation] = (request, _) =>
                PlaceWiiMNetworkStreams(request.Args),
            [LiteSonosContract.TargetsOperation] = (_, _) =>
                RequireSonos().ListTargets(),
            [LiteSonosContract.AccountStatusOperation] = (_, _) =>
                RequireSonos().AccountStatus(),
            [LiteSonosContract.AccountLoginStartOperation] = (_, _) =>
                RequireSonos().BeginAccountLogin(),
            [LiteSonosContract.AccountLoginCheckOperation] = (_, _) =>
                RequireSonos().CheckAccountLogin(),
            [LiteSonosContract.AccountLoginCancelOperation] = (_, _) =>
                RequireSonos().CancelAccountLogin(),
            [LiteSonosContract.AccountRefreshOperation] = (_, _) =>
                RequireSonos().RefreshAccount(),
            [LiteSonosContract.AccountPersistRetryOperation] = (_, _) =>
                RequireSonos().RetryAccountPersist(),
            [LiteSonosContract.AccountDisconnectOperation] = (_, _) =>
                RequireSonos().DisconnectAccount(),
            [LiteSonosContract.SnapshotOperation] = (request, _) =>
                RequireSonos().Snapshot(request.Args),
            [LiteSonosContract.TransportOperation] = (request, _) =>
                RequireSonos().Transport(request.Args),
            [LiteSonosContract.LibraryOperation] = (_, _) =>
                RequireSonos().Library(),
            [LiteSonosContract.MaterialsOperation] = (request, _) =>
                RequireSonos().Materials(request.Args),
            [LiteSonosContract.LoadMaterialOperation] = (request, _) =>
                RequireSonos().LoadMaterial(request.Args),
            [LiteSonosContract.OwnStreamEditInfoOperation] = (request, _) =>
                SonosOwnStreamEditInfo(request.Args),
            [LiteSonosContract.AddOwnStreamOperation] = (request, _) =>
                AddSonosOwnStream(request.Args),
            [LiteSonosContract.EditOwnStreamOperation] = (request, _) =>
                EditSonosOwnStream(request.Args),
            [LiteSonosContract.RemoveOwnStreamsOperation] = (request, _) =>
                RemoveSonosOwnStreams(request.Args),
            [LiteSonosContract.ImportOwnStreamsOperation] = (request, _) =>
                ImportSonosOwnStreams(request.Args),
            [LiteSonosContract.SortOwnStreamsOperation] = (request, _) =>
                SetSonosOwnStreamSortMode(request.Args),
            [LiteSonosContract.MoveOwnStreamsOperation] = (request, _) =>
                MoveSonosOwnStreams(request.Args),
            [LiteSonosContract.PlaceOwnStreamsOperation] = (request, _) =>
                PlaceSonosOwnStreams(request.Args),
            [LiteSonosContract.ExportOwnStreamsOperation] = (request, _) =>
                ExportSonosOwnStreams(request.Args),
            ["podcast.toggleFavorite"] = (request, _) =>
                TogglePodcastFavorites(request.Args),
            ["podcast.toggleLibrary"] = (request, _) =>
                TogglePodcastLibrary(request.Args),
            ["podcast.toggleQueue"] = (request, _) =>
                TogglePodcastQueue(request.Args),
            ["podcast.playbackOptions"] = (request, _) =>
                GetPodcastPlaybackOptions(request.Args),
            ["podcast.playbackOptions.set"] = (request, _) =>
                SetPodcastPlaybackOptions(request.Args),
            ["library.renameTitle"] = (request, _) => RenameLibraryTitle(request.Args),
            ["library.renameFile"] = (request, _) => RenameLocalFile(request.Args),
            ["library.folderSources"] = (_, _) =>
                RequireProfileMutations().ListLocalFolderSources(),
            ["library.folderSource.add"] = (request, _) =>
                RequireProfileMutations().AddLocalFolderSource(
                    LiteArgs.RequirePath(request.Args, "path")),
            ["library.folderSource.refresh"] = (request, _) =>
                RequireProfileMutations().RefreshLocalFolderSources(
                    ReadOptionalIds(request.Args, "sourceIds")),
            ["library.folderSource.detach"] = (request, _) =>
                RequireProfileMutations().DetachLocalFolderSource(
                    LiteArgs.RequireText(request.Args, "sourceId")),
            ["library.toggleFavorite"] = (request, _) => ToggleProfileFavorites(request.Args),
            ["radio.toggleLibrary"] = (request, _) => ToggleRadioLibrary(request.Args),
            ["library.playlists"] = (request, _) => GetProfilePlaylists(request.Args),
            ["library.playlists.replace"] = (request, _) => ReplaceProfilePlaylists(request.Args),
            ["library.playlist.move"] = (request, _) => MoveProfilePlaylistItems(request.Args),
            ["library.playlist.placeBefore"] = (request, _) => PlaceProfilePlaylistItems(request.Args),
            ["library.remove"] = (request, _) => RemoveProfileItems(request.Args),
            ["library.recycle"] = (request, _) => RecycleLocalFiles(request.Args),
            ["podcast.renameSubscription"] = (request, _) => RenamePodcastSubscription(request.Args),
            ["radio.addStation"] = (request, _) => AddRadioStation(request.Args),
            ["radio.editStation"] = (request, _) => EditRadioStation(request.Args),
            ["radio.recordingHistoryForgetPaths"] = (request, _) =>
                ForgetRecordingHistoryPaths(request.Args),
            ["bookmark.add"] = (request, _) => AddBookmark(request.Args),
            ["bookmark.remove"] = (request, _) => RemoveBookmarks(request.Args),
            ["bookmark.navigate"] = (request, _) => NavigateBookmark(request.Args),
            ["chapter.add"] = (request, _) => AddChapter(request.Args),
            [LitePodcastChapterCoordinator.Operation] = (request, _) =>
                PrepareChapters(request.Args),
            ["chapter.list"] = (request, _) => ListChapters(request.Args),
            ["chapter.navigate"] = (request, _) => NavigateChapter(request.Args),
            ["radio.importPlaylist"] = (request, _) => ImportPlaylist(request.Args),
            ["radio.importPlaylistToLibrary"] = (request, _) => ImportPlaylistToLibrary(request.Args),
            ["radio.exportFavorites"] = (request, _) => ExportRadioFavorites(request.Args),
            ["radio.collectionSort"] = (request, _) => SetRadioCollectionSort(request.Args),
            ["radio.collectionMove"] = (request, _) => MoveRadioCollectionItems(request.Args),
            ["radio.collectionPlaceBefore"] = (request, _) => PlaceRadioCollectionItems(request.Args),
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
            ["queue.addMembership"] = (request, _) => QueueAddMembership(request.Args),
            ["queue.togglePlayNext"] = (request, _) => QueueToggleMembership(request.Args, playNext: true),
            ["queue.status"] = (_, _) => QueueStatusPayload(),
            ["queue.playAt"] = (request, events) =>
                RunLocalPlaybackStart(() => QueuePlayAt(request.Args, events)),
            ["queue.next"] = (_, events) =>
                RunLocalPlaybackStart(() => QueueRelative(1, events)),
            ["queue.previous"] = (_, events) =>
                RunLocalPlaybackStart(() => QueueRelative(-1, events))
        };
    }

    private object RunLocalPlaybackStart(Func<object> action)
    {
        lock (_audioClipEditGate)
        {
            EnsureAudioClipRemovalIsNotInProgressLocked();
            return action();
        }
    }

    private void EnsureAudioClipRemovalIsNotInProgressLocked()
    {
        if (_audioClipRemovalInProgress)
        {
            throw new LiteRequestException(
                "Trwa usuwanie fragmentu. Poczekaj na zakończenie edycji przed ponownym odtworzeniem pliku.");
        }
    }

    private LiteCommonDataSnapshotCoordinator RequireCommonDataSnapshot() =>
        _commonDataSnapshot
        ?? throw new LiteRequestException("Synchronizacja danych profili nie jest dostepna w tym uruchomieniu.");

    private object ImportCommonDataSnapshot(JsonElement args)
    {
        if (_recordings.HasActiveRecordings)
        {
            throw new LiteRequestException(
                "Nie odswiezono danych, poniewaz trwa nagrywanie. Sprobuj po jego zakonczeniu.");
        }
        return RequireCommonDataSnapshot().Import(args);
    }

    private object ExportBackup(JsonElement args)
    {
        EnsureNoActiveRecordingForProfileTransfer("Nie wyeksportowano kopii danych");
        return RequireCommonDataSnapshot().ExportBackup(args);
    }

    private object ImportBackup(JsonElement args)
    {
        EnsureNoActiveRecordingForProfileTransfer("Nie zaimportowano kopii danych");
        return RequireCommonDataSnapshot().ImportBackup(args);
    }

    private void EnsureNoActiveRecordingForProfileTransfer(string lead)
    {
        if (_recordings.HasActiveRecordings)
        {
            throw new LiteRequestException(
                $"{lead}, ponieważ trwa nagrywanie. Spróbuj po jego zakończeniu.");
        }
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
        var sourcePath = LiteArgs.ReadText(args, "path");
        if (IsCurrentFilesItem(itemId) || IsCurrentFilesPath(sourcePath))
            StopFilesPlayback();
        var result = RequireProfileMutations().RenameLocalFile(
            itemId,
            LiteArgs.RequireText(args, "name"),
            sourcePath);
        _recordings.RewriteHistoryPath(result.OldPath, result.Path);
        return result;
    }

    private object RenamePodcastSubscription(JsonElement args) =>
        RequireProfileMutations().RenamePodcastSubscription(
            LiteArgs.RequireText(args, "subscriptionId"),
            LiteArgs.RequireText(args, "title"));

    private object GetProfilePlaylists(JsonElement args) =>
        RequireProfileMutations().GetPlaylists(
            LiteArgs.RequireText(args, "sessionId"));

    private object ReplaceProfilePlaylists(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("playlists", out var source)
            || source.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Brak listy playlist do zapisania.");
        }
        if (source.GetArrayLength() > 500)
            throw new LiteRequestException("Jedna sesja może zawierać najwyżej 500 playlist.");

        var playlists = new List<LiteProfileMutationStore.LitePlaylistMutation>(
            source.GetArrayLength());
        foreach (var raw in source.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object)
                throw new LiteRequestException("Każda playlista musi być obiektem.");
            var itemIds = ReadPlaylistItemIds(raw);
            var created = raw.TryGetProperty("createdUtcTicks", out var createdSource)
                && createdSource.ValueKind == JsonValueKind.Number
                && createdSource.TryGetInt64(out var parsedCreated)
                ? parsedCreated
                : 0L;
            playlists.Add(new LiteProfileMutationStore.LitePlaylistMutation(
                LiteArgs.RequireText(raw, "playlistId"),
                LiteArgs.RequireText(raw, "name"),
                created,
                itemIds));
        }
        return RequireProfileMutations().ReplacePlaylists(
            LiteArgs.RequireText(args, "sessionId"),
            playlists);
    }

    private static string[] ReadPlaylistItemIds(JsonElement playlist)
        => ReadRequiredIdArray(
            playlist,
            "itemIds",
            "Playlista nie zawiera listy elementów.");

    private object MoveProfilePlaylistItems(JsonElement args) =>
        RequireProfileMutations().MovePlaylistItems(
            LiteArgs.RequireText(args, "sessionId"),
            LiteArgs.RequireText(args, "playlistId"),
            ReadRequiredIdArray(
                args,
                "selectedItemIds",
                "Nie wskazano elementów do przeniesienia."),
            ReadRequiredIdArray(
                args,
                "visibleItemIds",
                "Nie przekazano widocznej kolejności playlisty."),
            ReadMoveDirection(args));

    private object PlaceProfilePlaylistItems(JsonElement args) =>
        RequireProfileMutations().PlacePlaylistItemsBefore(
            LiteArgs.RequireText(args, "sessionId"),
            LiteArgs.RequireText(args, "playlistId"),
            ReadRequiredIdArray(
                args,
                "selectedItemIds",
                "Nie wskazano elementów do przeniesienia."),
            LiteArgs.RequireText(args, "targetItemId"));

    private static int ReadMoveDirection(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("direction", out var source)
            || source.ValueKind != JsonValueKind.Number
            || !source.TryGetInt32(out var direction)
            || direction is not (-1 or 1))
        {
            throw new LiteRequestException("Kierunek zmiany kolejności musi wynosić minus jeden albo jeden.");
        }
        return direction;
    }

    private static string[] ReadRequiredIdArray(
        JsonElement value,
        string propertyName,
        string missingMessage)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty(propertyName, out var source)
            || source.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException(missingMessage);
        }
        if (source.GetArrayLength() > 10_000)
            throw new LiteRequestException("Jedna playlista może zawierać najwyżej 10000 elementów.");
        var ids = new List<string>(source.GetArrayLength());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in source.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String)
                throw new LiteRequestException("Identyfikator elementu playlisty musi być napisem.");
            var id = entry.GetString()?.Trim() ?? string.Empty;
            if (id.Length == 0 || id.Length > LiteArgs.MaximumTextLength)
                throw new LiteRequestException("Playlista zawiera nieprawidłowy identyfikator elementu.");
            if (seen.Add(id)) ids.Add(id);
        }
        return ids.ToArray();
    }

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

    private object TogglePodcastLibrary(JsonElement args)
    {
        var subscriptions = ReadOptionalIds(args, "subscriptionIds");
        if (subscriptions.Length == 0)
            throw new LiteRequestException("Wybierz podcast albo odcinek.");
        return RequireProfileMutations().TogglePodcastLibrary(subscriptions);
    }

    private object TogglePodcastQueue(JsonElement args)
    {
        var episodes = ReadOptionalIds(args, "episodeIds");
        if (episodes.Length == 0)
            throw new LiteRequestException("Wybierz odcinek podcastu.");
        return RequireProfileMutations().TogglePodcastQueue(
            episodes,
            LiteArgs.ReadBool(args, "playNext", false));
    }

    private object GetPodcastPlaybackOptions(JsonElement args)
    {
        var snapshot = RequirePodcastStore().GetPlaybackOptions(
            ReadPodcastPlaybackOptionsTarget(args),
            LiteArgs.RequireText(args, "itemId"));
        return PodcastPlaybackOptionsPayload(snapshot);
    }

    private object SetPodcastPlaybackOptions(JsonElement args)
    {
        var target = ReadPodcastPlaybackOptionsTarget(args);
        var itemId = LiteArgs.RequireText(args, "itemId");
        var refreshInterval = target == PodcastPlaybackOptionsTarget.Podcast
            ? ReadRequiredInt(args, "refreshIntervalMinutes")
            : (int?)null;
        var snapshot = RequirePodcastStore().SetPlaybackOptions(
            target,
            itemId,
            new PodcastPlaybackOptionsChange(
                ReadResumePositionMode(args),
                ReadNullableDouble(args, "playbackRate"),
                ReadNullableBool(args, "loudnessNormalization"),
                ReadNullableBool(args, "smoothTrackTransitions"),
                ReadNullableInt(args, "interTrackSilenceMs"),
                ReadNullableTempoAlgorithm(args),
                refreshInterval,
                target == PodcastPlaybackOptionsTarget.Podcast
                    ? LiteArgs.ReadText(args, "downloadsFolder")
                    : null));

        var appliesOnNextPlayback = true;
        lock (_gate)
        {
            if (_filesItem?.Kind == MediaItemKind.Episode
                && string.Equals(_filesItem.Id, itemId, StringComparison.Ordinal))
            {
                var effectiveAudio = ResolvePodcastAudio(snapshot);
                EnsureTempoAlgorithmAvailable(effectiveAudio.TempoAlgorithm);
                _files.ConfigureAudioProcessing(effectiveAudio);
                var rate = snapshot.ResolvedPlaybackRateOverride ?? _requestedRate;
                _filesRate = rate;
                _files.SetPlaybackRate(rate);
            }
        }
        return new
        {
            options = PodcastPlaybackOptionsPayload(snapshot),
            appliesOnNextPlayback
        };
    }

    private LitePodcastProgressStore RequirePodcastStore() =>
        _podcastProgressStore ?? throw new LiteRequestException(
            "Host nie dostał bazy Podcastów i YouTube. Opcje odtwarzania są niedostępne.");

    private static PodcastPlaybackOptionsTarget ReadPodcastPlaybackOptionsTarget(
        JsonElement args) => LiteArgs.RequireText(args, "target") switch
        {
            "podcast" => PodcastPlaybackOptionsTarget.Podcast,
            "episode" => PodcastPlaybackOptionsTarget.Episode,
            _ => throw new LiteRequestException(
                "Opcje odtwarzania dotyczą podcastu albo odcinka.")
        };

    private static ResumePositionMode ReadResumePositionMode(JsonElement args)
    {
        var value = ReadRequiredInt(args, "resumePositionMode");
        return Enum.IsDefined(typeof(ResumePositionMode), value)
            ? (ResumePositionMode)value
            : throw new LiteRequestException("Nieznany sposób wznawiania odtwarzania.");
    }

    private static int ReadRequiredInt(JsonElement args, string name)
    {
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value))
        {
            return value;
        }
        throw new LiteRequestException($"Brak prawidłowej wartości \"{name}\".");
    }

    private static int? ReadNullableInt(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)) return value;
        throw new LiteRequestException($"Nieprawidłowa wartość \"{name}\".");
    }

    private static double? ReadNullableDouble(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out var value)
            && double.IsFinite(value)) return value;
        throw new LiteRequestException($"Nieprawidłowa wartość \"{name}\".");
    }

    private static bool? ReadNullableBool(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new LiteRequestException($"Nieprawidłowa wartość \"{name}\".")
        };
    }

    private static PlaybackTempoAlgorithm? ReadNullableTempoAlgorithm(JsonElement args)
    {
        var value = ReadNullableInt(args, "tempoAlgorithm");
        if (value is null) return null;
        return Enum.IsDefined(typeof(PlaybackTempoAlgorithm), value.Value)
            ? (PlaybackTempoAlgorithm)value.Value
            : throw new LiteRequestException("Nieznany sposób przeliczania tempa.");
    }

    private static object PodcastPlaybackOptionsPayload(
        PodcastPlaybackOptionsSnapshot snapshot) => new
    {
        target = snapshot.Target == PodcastPlaybackOptionsTarget.Podcast
            ? "podcast"
            : "episode",
        itemId = snapshot.ItemId,
        title = snapshot.Title,
        resumePositionMode = (int)snapshot.ResumePositionMode,
        playbackRate = snapshot.PlaybackRateOverride,
        loudnessNormalization = snapshot.LoudnessNormalizationOverride,
        smoothTrackTransitions = snapshot.SmoothTrackTransitionsOverride,
        interTrackSilenceMs = snapshot.InterTrackSilenceMillisecondsOverride,
        tempoAlgorithm = (int?)snapshot.TempoAlgorithmOverride,
        refreshIntervalMinutes = snapshot.RefreshIntervalMinutes,
        downloadsFolder = snapshot.DownloadsFolder
    };

    private object EditRadioStation(JsonElement args)
    {
        var stationId = LiteArgs.RequireText(args, "stationId");
        var result = RequireProfileMutations().EditRadioStation(
            stationId,
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));
        // Pełne AMC zatrzymuje aktualną stację tylko po zmianie adresu.
        // Nazwę można poprawić bez przerywania słuchania.
        if (result.StreamChanged && IsCurrentRadioItem(stationId)) StopRadioPlayback();
        return result;
    }

    private object AddRadioStation(JsonElement args) =>
        RequireProfileMutations().AddRadioStation(
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));

    private object AddWiiMNetworkStream(JsonElement args) =>
        RequireProfileMutations().AddWiiMNetworkStream(
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));

    private object EditWiiMNetworkStream(JsonElement args) =>
        RequireProfileMutations().EditWiiMNetworkStream(
            LiteArgs.RequireText(args, "streamId"),
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));

    private object RemoveWiiMNetworkStreams(JsonElement args) =>
        RequireProfileMutations().RemoveWiiMNetworkStreams(ReadIds(args));

    private object ImportWiiMNetworkStreams(JsonElement args)
    {
        var path = LiteArgs.RequirePath(args, "path");
        if (!File.Exists(path))
            throw new LiteRequestException($"Nie znaleziono pliku listy: {path}");
        var imported = RadioPlaylistImporter.Import(path);
        return RequireProfileMutations().ImportWiiMNetworkStreams(
            imported.Stations
                .Select(station => new LiteWiiMNetworkStreamDraft(
                    station.Name,
                    station.StreamUrl))
                .ToArray(),
            imported.SkippedEntries);
    }

    private object ExportWiiMNetworkStreams(JsonElement args) =>
        RequireProfileMutations().ExportWiiMNetworkStreams(
            LiteArgs.RequirePath(args, "path"));

    private object SetWiiMNetworkStreamSortMode(JsonElement args) =>
        RequireProfileMutations().SetWiiMNetworkStreamSortMode(
            LiteWiiMContract.ReadNetworkStreamSortMode(args));

    private object MoveWiiMNetworkStreams(JsonElement args) =>
        RequireProfileMutations().MoveWiiMNetworkStreams(
            ReadIds(args),
            LiteWiiMContract.ReadNetworkStreamMoveDirection(args));

    private object PlaceWiiMNetworkStreams(JsonElement args) =>
        RequireProfileMutations().PlaceWiiMNetworkStreamsBefore(
            ReadIds(args),
            LiteArgs.RequireText(args, "targetId"));

    private object SonosOwnStreamEditInfo(JsonElement args) =>
        RequireProfileMutations().SonosOwnStreamEditInfo(
            LiteSonosContract.ReadOwnStreamId(args));

    private object AddSonosOwnStream(JsonElement args) =>
        RequireProfileMutations().AddSonosOwnStream(
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));

    private object EditSonosOwnStream(JsonElement args) =>
        RequireProfileMutations().EditSonosOwnStream(
            LiteSonosContract.ReadOwnStreamId(args),
            LiteArgs.RequireText(args, "name"),
            LiteArgs.RequireText(args, "url"));

    private object RemoveSonosOwnStreams(JsonElement args) =>
        RequireProfileMutations().RemoveSonosOwnStreams(ReadIds(args));

    private object ImportSonosOwnStreams(JsonElement args)
    {
        var path = LiteArgs.RequirePath(args, "path");
        if (!File.Exists(path))
            throw new LiteRequestException($"Nie znaleziono pliku listy: {path}");
        var imported = RadioPlaylistImporter.Import(path);
        return RequireProfileMutations().ImportSonosOwnStreams(
            imported.Stations
                .Select(station => new LiteSonosOwnStreamDraft(
                    station.Name,
                    station.StreamUrl))
                .ToArray(),
            imported.SkippedEntries);
    }

    private object SetSonosOwnStreamSortMode(JsonElement args) =>
        RequireProfileMutations().SetSonosOwnStreamSortMode(
            LiteSonosContract.ReadOwnStreamSortMode(args));

    private object MoveSonosOwnStreams(JsonElement args) =>
        RequireProfileMutations().MoveSonosOwnStreams(
            ReadIds(args),
            LiteSonosContract.ReadOwnStreamMoveDirection(args));

    private object PlaceSonosOwnStreams(JsonElement args) =>
        RequireProfileMutations().PlaceSonosOwnStreamsBefore(
            ReadIds(args),
            LiteArgs.RequireText(args, "targetId"));

    private object ExportSonosOwnStreams(JsonElement args) =>
        RequireProfileMutations().ExportSonosOwnStreams(
            LiteArgs.RequirePath(args, "path"));

    private object RemoveProfileItems(JsonElement args)
    {
        var sessionId = LiteArgs.RequireText(args, "sessionId");
        var view = LiteArgs.RequireText(args, "view");
        var itemIds = ReadIds(args);
        if (string.Equals(sessionId, "podcasts", StringComparison.Ordinal)
            && string.Equals(view, "podcastEpisodes", StringComparison.Ordinal)
            && itemIds.Any(IsCurrentPodcastItem))
        {
            // Najpierw zachowaj punkt wznowienia i zwolnij dekoder. Po
            // skasowaniu rekordu spóźniony checkpoint nie może go odtworzyć
            // ani zgłosić mylącego błędu w tle.
            StopFilesPlayback();
        }
        return RequireProfileMutations().Remove(
            sessionId,
            view,
            itemIds,
            LiteArgs.ReadText(args, "playlistId"));
    }

    private object ToggleProfileFavorites(JsonElement args) =>
        RequireProfileMutations().ToggleFavorites(
            LiteArgs.RequireText(args, "sessionId"),
            ReadIds(args),
            ReadOptionalTextList(args, "paths"));

    private object ToggleRadioLibrary(JsonElement args) =>
        RequireProfileMutations().ToggleRadioLibrary(ReadIds(args));

    private object SetRadioCollectionSort(JsonElement args)
    {
        var text = LiteArgs.RequireText(args, "mode").Trim();
        if (!Enum.TryParse<CollectionSortMode>(text, ignoreCase: true, out var mode)
            || !Enum.IsDefined(mode))
            throw new LiteRequestException("Nieznany sposób sortowania stacji Radia.");
        return RequireProfileMutations().SetRadioCollectionSortMode(
            LiteArgs.RequireText(args, "view"),
            mode);
    }

    private object MoveRadioCollectionItems(JsonElement args) =>
        RequireProfileMutations().MoveRadioCollectionItems(
            LiteArgs.RequireText(args, "view"),
            ReadRequiredIdArray(
                args,
                "selectedItemIds",
                "Nie wskazano stacji do przeniesienia."),
            ReadRequiredIdArray(
                args,
                "visibleItemIds",
                "Nie przekazano widocznej kolejności stacji."),
            ReadMoveDirection(args));

    private object PlaceRadioCollectionItems(JsonElement args) =>
        RequireProfileMutations().PlaceRadioCollectionItemsBefore(
            LiteArgs.RequireText(args, "view"),
            ReadRequiredIdArray(
                args,
                "selectedItemIds",
                "Nie wskazano stacji do przeniesienia."),
            ReadRequiredIdArray(
                args,
                "visibleItemIds",
                "Nie przekazano widocznej kolejności stacji."),
            LiteArgs.RequireText(args, "targetItemId"));

    private object RecycleLocalFiles(JsonElement args)
    {
        var ids = ReadIds(args);
        var paths = ReadOptionalIds(args, "paths");
        if (ids.Any(IsCurrentFilesItem) || paths.Any(IsCurrentFilesPath))
            StopFilesPlayback();
        var result = RequireProfileMutations().RecycleLocalFiles(
            ids,
            paths);
        _recordings.ForgetHistoryPaths(result.Removed.Select(item => item.Path));
        return result;
    }

    private object ForgetRecordingHistoryPaths(JsonElement args)
    {
        var paths = ReadOptionalIds(args, "paths");
        var storedRemoved = RequireProfileMutations().ForgetRecordingHistoryPaths(paths);
        var currentRemoved = _recordings.ForgetHistoryPaths(paths);
        return new { removedCount = Math.Max(storedRemoved, currentRemoved) };
    }

    private bool IsCurrentFilesItem(string itemId)
    {
        lock (_gate)
        {
            return string.Equals(_filesItem?.Id, itemId, StringComparison.Ordinal)
                && (_files.LoadedItemId is not null || _files.IsPreparing);
        }
    }

    private bool IsCurrentPodcastItem(string itemId)
    {
        lock (_gate)
        {
            return _filesItem?.Kind == MediaItemKind.Episode
                && string.Equals(_filesItem.Id, itemId, StringComparison.Ordinal)
                && (_files.LoadedItemId is not null || _files.IsPreparing);
        }
    }

    private bool IsCurrentFilesPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        lock (_gate)
        {
            return _filesItem?.Source is { Length: > 0 } source
                && SameLocalPath(source, path)
                && (_files.LoadedItemId is not null || _files.IsPreparing);
        }
    }

    private bool IsCurrentRadioItem(string itemId)
    {
        lock (_gate)
        {
            return string.Equals(_radioItem?.Id, itemId, StringComparison.Ordinal)
                && (_radio.LoadedItemId is not null || _radio.IsPreparing);
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

    private static string[] ReadOptionalTextList(JsonElement args, string propertyName)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(propertyName, out var values))
            return [];
        if (values.ValueKind != JsonValueKind.Array)
            throw new LiteRequestException($"Argument \"{propertyName}\" nie jest listą.");
        // Zachowujemy puste pozycje i duplikaty: lista jest wyrównana indeksem
        // z itemIds, więc filtrowanie przesunęłoby ścieżkę do innego wiersza.
        return values.EnumerateArray()
            .Take(1_000)
            .Select(value => value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim() ?? string.Empty
                : string.Empty)
            .ToArray();
    }

    private object RemoveAudioClip(JsonElement args, LiteEventSink events)
    {
        var requestedPath = LiteArgs.RequirePath(args, "sourcePath");
        string currentId;
        lock (_audioClipEditGate)
        {
            EnsureAudioClipRemovalIsNotInProgressLocked();
            lock (_gate)
            {
                if (_filesItem is null
                    || string.IsNullOrWhiteSpace(_filesItem.Source)
                    || !SameLocalPath(_filesItem.Source, requestedPath))
                {
                    throw new LiteRequestException(
                        "Bieżący plik zmienił się przed rozpoczęciem edycji. Oryginalny plik nie został zmieniony.");
                }
                currentId = _filesItem.Id;
            }

            // Od tego miejsca zadne polecenie lokalnego odtwarzania nie moze
            // ponownie otworzyc pliku, zanim edytor skonczy lub zglosi blad.
            _audioClipRemovalInProgress = true;
            try
            {
                StopFilesPlayback();
            }
            catch
            {
                _audioClipRemovalInProgress = false;
                throw;
            }
        }

        try
        {
            events.Publish("audio.clipRemoveStarted", new
            {
                operationId = LiteArgs.ReadText(args, "operationId") ?? string.Empty,
                name = Path.GetFileName(requestedPath)
            });

            // StopFilesPlayback wycisza natychmiast, lecz dekoder i uchwyt
            // pliku sa zwalniane w tle. Przed uruchomieniem FFmpeg czekamy na
            // faktyczne domkniecie wlasnego toru AMC; edytor nadal wykonuje
            // niezalezna probe wylacznego dostepu dla obcych procesow.
            _files.WaitForPendingPipelineDisposalAsync(AudioClipPlaybackReleaseTimeout)
                .GetAwaiter()
                .GetResult();
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
                    _filesPaused = false;
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
        finally
        {
            lock (_audioClipEditGate) _audioClipRemovalInProgress = false;
        }
    }

    private object AppendAudioClip(JsonElement args, LiteEventSink events)
    {
        var requestedSource = LiteArgs.RequirePath(args, "sourcePath");
        lock (_gate)
        {
            if (_filesItem is null
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
        _ = LiteArgs.RequireText(args, "itemTitle");
        var name = LiteArgs.ReadText(args, "name");
        var context = CurrentTimelineContext(args);
        if (!string.Equals(context.PlaybackItemId, itemId, StringComparison.Ordinal)
            && !string.Equals(context.Item.Id, itemId, StringComparison.Ordinal))
            throw new LiteRequestException(
                "W trakcie dodawania zmienił się odtwarzany materiał. Powtórz skrót.");
        if (context.Duration <= TimeSpan.Zero)
            throw new LiteRequestException("Nie można dodać zakładki: czas trwania materiału jest nieznany.");
        return _bookmarkStore.Add(
            context.StorageSessionId,
            context.SessionName,
            context.Item.Id,
            context.Item.Title,
            context.Position,
            DateTime.UtcNow,
            name);
    }

    private object RemoveBookmarks(JsonElement args)
    {
        if (_bookmarkStore is null)
            throw new LiteRequestException("Usuwanie zakładek nie ma dostępu do Biblioteki AMC.");
        return _bookmarkStore.Remove(ReadIds(args));
    }

    private object NavigateBookmark(JsonElement args)
    {
        if (_bookmarkStore is null)
            throw new LiteRequestException("Nawigacja po zakładkach nie ma dostępu do Biblioteki AMC.");

        var target = ResolveTransportTarget(args);
        if (target.SessionId is not ("files" or "podcasts" or "spotify" or "radioarchiwista"))
            throw new LiteRequestException(
                "Nawigacja po zakładkach nie jest dostępna w tej sesji.");
        var direction = Math.Sign(LiteArgs.ReadInt(args, "direction", 0, -1, 1));
        var namedOnly = LiteArgs.ReadBool(args, "namedOnly", false);
        if (direction == 0)
            throw new LiteRequestException("Nie podano kierunku nawigacji po zakładkach.");

        var context = CurrentTimelineContext(args);
        BookmarkNavigationCursor? cursor;
        lock (_gate)
        {
            cursor = _bookmarkNavigationCursor;
        }

        var now = DateTime.UtcNow;
        var bookmark = cursor is not null
            && string.Equals(cursor.ItemId, context.Item.Id, StringComparison.Ordinal)
            && cursor.NamedOnly == namedOnly
            && now - cursor.LastNavigationUtc <= BookmarkNavigationContinuationWindow
                ? namedOnly
                    ? _bookmarkStore.FindNamedAdjacent(
                        context.StorageSessionId, context.Item.Id, cursor.BookmarkId, direction)
                    : _bookmarkStore.FindAdjacent(
                        context.StorageSessionId, context.Item.Id, cursor.BookmarkId, direction)
                : namedOnly
                    ? _bookmarkStore.FindNamedRelative(
                        context.StorageSessionId, context.Item.Id, context.Position, direction)
                    : _bookmarkStore.FindRelative(
                        context.StorageSessionId, context.Item.Id, context.Position, direction);
        if (bookmark is null)
        {
            return new
            {
                found = false,
                direction
            };
        }

        var position = TimeSpan.FromTicks(Math.Max(0L, bookmark.PositionTicks));
        SeekTimeline(context, position);
        lock (_gate)
        {
            _bookmarkNavigationCursor = new BookmarkNavigationCursor(
                context.Item.Id, bookmark.Id, namedOnly, now);
        }
        return new
        {
            found = true,
            direction,
            name = bookmark.Name,
            positionSeconds = position.TotalSeconds
        };
    }

    private object AddChapter(JsonElement args)
    {
        if (_bookmarkStore is null)
            throw new LiteRequestException("Zapisywanie rozdziałów nie ma dostępu do Biblioteki AMC.");
        var name = LiteArgs.RequireText(args, "name").Trim();
        var context = CurrentTimelineContext(args);
        if (context.Duration <= TimeSpan.Zero)
            throw new LiteRequestException("Nie można dodać rozdziału: czas trwania materiału jest nieznany.");
        var result = _bookmarkStore.AddUserChapter(
            context.StorageSessionId,
            context.SessionName,
            context.Item,
            context.Position,
            DateTime.UtcNow,
            name);
        return new
        {
            added = result.Added,
            nameChanged = result.NameChanged,
            name = result.Entry.Name,
            positionSeconds = TimeSpan.FromTicks(result.Entry.PositionTicks).TotalSeconds
        };
    }

    private object ListChapters(JsonElement args)
    {
        var context = CurrentTimelineContext(args);
        var chapters = _bookmarkStore?.GetChapters(
            context.StorageSessionId,
            context.Item.Id,
            context.Duration) ?? [];
        return new
        {
            title = context.Item.Title,
            currentPositionSeconds = context.Position.TotalSeconds,
            chapters = chapters.Select(chapter => new
            {
                chapterId = chapter.Entry.Id,
                name = chapter.Name,
                positionSeconds = chapter.Start.TotalSeconds,
                durationSeconds = chapter.Duration.TotalSeconds,
                userCreated = chapter.Entry.ChapterOrigin == ChapterOrigin.User
            }).ToArray()
        };
    }

    private object PrepareChapters(JsonElement args)
    {
        var context = CurrentTimelineContext(args);
        var current = _bookmarkStore?.GetChapters(
            context.StorageSessionId,
            context.Item.Id,
            context.Duration) ?? [];
        if (context.StorageSessionId == "spotify")
        {
            if (context.Item.Kind != MediaItemKind.Episode || _spotifyCatalog is null)
            {
                return new { chapterCount = current.Count, importedCount = 0 };
            }
            IReadOnlyList<ProviderChapterPoint> points;
            try
            {
                points = _spotifyCatalog.ReadEpisodeChapterPoints(
                    context.Item,
                    context.Duration);
            }
            catch (LiteRequestException)
            {
                // Brak sieci lub katalogu nie może zablokować rozdziałów
                // dodanych przez użytkownika ani wcześniej zapisanej listy.
                return new { chapterCount = current.Count, importedCount = 0 };
            }
            var imported = _bookmarkStore?.ReplaceProviderChapters(
                "spotify",
                "Spotify",
                context.Item,
                "spotify-description",
                points,
                DateTime.UtcNow) ?? 0;
            var refreshed = _bookmarkStore?.GetChapters(
                context.StorageSessionId,
                context.Item.Id,
                context.Duration) ?? [];
            return new
            {
                chapterCount = refreshed.Count,
                importedCount = imported
            };
        }
        if (context.StorageSessionId != "podcasts" || _podcastChapters is null)
        {
            return new
            {
                chapterCount = current.Count,
                importedCount = 0
            };
        }

        var prepared = _podcastChapters.Prepare(context.Item, context.Duration);
        return new
        {
            chapterCount = prepared.ChapterCount,
            importedCount = prepared.ImportedCount
        };
    }

    private object NavigateChapter(JsonElement args)
    {
        if (_bookmarkStore is null)
            throw new LiteRequestException("Nawigacja po rozdziałach nie ma dostępu do Biblioteki AMC.");
        var direction = Math.Sign(LiteArgs.ReadInt(args, "direction", 0, -1, 1));
        if (direction == 0)
            throw new LiteRequestException("Nie podano kierunku nawigacji po rozdziałach.");
        var context = CurrentTimelineContext(args);
        var chapter = _bookmarkStore.FindChapter(
            context.StorageSessionId,
            context.Item.Id,
            context.Duration,
            context.Position,
            direction);
        if (chapter is null) return new { found = false, direction };
        SeekTimeline(context, chapter.Start);
        return new
        {
            found = true,
            direction,
            name = chapter.Name,
            positionSeconds = chapter.Start.TotalSeconds
        };
    }

    private TimelineContext CurrentTimelineContext(JsonElement args)
    {
        if (_bookmarkStore is null)
            throw new LiteRequestException(
                "Zakładki i rozdziały nie mają dostępu do Biblioteki AMC.");
        var target = ResolveTransportTarget(args);
        if (target.SessionId == "spotify")
        {
            var snapshot = RequireSpotifyPlayback().Timeline();
            if (snapshot.Duration <= TimeSpan.Zero)
                throw new LiteRequestException("Czas trwania materiału jest nieznany.");
            return new TimelineContext(
                "spotify",
                snapshot.PlaybackItemId,
                snapshot.Item,
                snapshot.Position,
                snapshot.Duration,
                "spotify",
                "Spotify");
        }
        if (target.SessionId is not ("files" or "podcasts" or "radioarchiwista"))
            throw new LiteRequestException(
                "Zakładki i rozdziały nie są dostępne w tej sesji.");
        lock (_gate)
        {
            RequireTargetOwnsFilesItemLocked(target);
            var item = _filesItem
                ?? throw new LiteRequestException("Najpierw otwórz materiał w odtwarzaczu.");
            var duration = string.Equals(_filesDurationId, item.Id, StringComparison.Ordinal)
                ? _filesDuration
                : item.Duration;
            if (duration <= TimeSpan.Zero)
                throw new LiteRequestException("Czas trwania materiału jest nieznany.");
            var podcast = target.SessionId == "podcasts";
            var archive = target.SessionId == "radioarchiwista";
            return new TimelineContext(
                "files",
                item.Id,
                new MediaItem
                {
                    Id = item.Id,
                    Title = item.Title,
                    Kind = item.Kind,
                    Source = item.Source,
                    Duration = duration
                },
                _files.Position,
                duration,
                archive ? "radioarchiwista" : podcast ? "podcasts" : "local",
                archive ? "Radio Archiwista" : podcast ? "Podcasty i YouTube" : "Pliki lokalne");
        }
    }

    private void SeekTimeline(TimelineContext context, TimeSpan position)
    {
        if (context.Engine == "spotify")
        {
            var current = RequireSpotifyPlayback().Timeline();
            if (!string.Equals(
                    current.PlaybackItemId,
                    context.PlaybackItemId,
                    StringComparison.Ordinal))
                throw new LiteRequestException(
                    "W trakcie nawigacji zmienił się odtwarzany materiał. Powtórz skrót.");
            RequireSpotifyPlayback().SeekTo(position);
            return;
        }
        lock (_gate)
        {
            if (_filesItem is null
                || !string.Equals(
                    _filesItem.Id,
                    context.PlaybackItemId,
                    StringComparison.Ordinal))
                throw new LiteRequestException(
                    "W trakcie nawigacji zmienił się odtwarzany materiał. Powtórz skrót.");
        }
        _files.Seek(position);
        if (!_queue.NotePosition(position)) TrySaveCurrentLocalResume();
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

    private object QueueAddMembership(JsonElement args)
    {
        var result = _queue.AddMembership(args);
        return QueuePayload(result.Status, added: result.Added, changed: result.Changed);
    }

    private object QueuePlayAt(JsonElement args, LiteEventSink events)
    {
        _events = events;
        TrySaveCurrentLocalResume();
        ConfigureOutputForSession("files");
        lock (_gate)
        {
            _activeEngine = "files";
            _filesPaused = false;
        }
        // Dlugosc czyscimy PO starcie, znajac Id nowego utworu: zerowanie przed
        // Play kasowalo czas, ktory dekoder podawal w trakcie samego Play.
        var status = _queue.PlayAt(args);
        SyncCurrentFromQueue(status);
        // Glosnosc i tempo z ZADANIA musza stac sie stanem transportu hosta, bo
        // to z _filesVolume/_filesRate odpowiada transport.status i od nich zaczynaja
        // transport.setVolume/setRate. Bez tego Enter z kolejki oddawal domyslne
        // 35 i tempo 1, mimo ze silnik dostal wartosci uzytkownika.
        lock (_gate)
        {
            _filesVolume = _queue.CurrentVolume;
            _filesRate = _queue.CurrentRate;
            _requestedRate = _queue.CurrentRate;
        }
        if (_filesMuted) _files.SetVolume(0);
        return QueuePayload(status);
    }

    private object QueueRelative(int direction, LiteEventSink events)
    {
        _events = events;
        TrySaveCurrentLocalResume();
        ConfigureOutputForSession("files");
        if (!_queue.PlayRelative(direction))
        {
            // Odmowa jest WYNIKIEM, nie bledem: na koncu kolejki nie ma gdzie
            // isc i nie zawijamy sie na druga strone.
            return QueuePayload(_queue.Status(), moved: false);
        }
        var status = _queue.Status();
        if (_filesMuted) _files.SetVolume(0);
        lock (_gate)
        {
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
            _filesPaused = status.Paused;
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
        TrySaveCurrentLocalResume();

        var requestedId = LiteArgs.ReadText(args, "id") ?? "file:" + path;
        var hasExplicitPosition = args.TryGetProperty("positionSeconds", out _);
        var prepared = _queue.PrepareDirectResume(
            requestedId,
            path,
            restorePosition: !hasExplicitPosition);
        var position = hasExplicitPosition
            ? TimeSpan.FromSeconds(LiteArgs.ReadDouble(
                args, "positionSeconds", 0d, 0d, 86_400d))
            : prepared.Position;
        var volume = LiteArgs.ReadInt(args, "volume", _filesVolume, 0, 100);
        var rate = LiteArgs.ReadDouble(args, "rate", _filesRate, 0.5d, 2.0d);
        var item = new MediaItem
        {
            Id = prepared.ItemId,
            Title = LiteArgs.ReadText(args, "title") ?? Path.GetFileNameWithoutExtension(path),
            Kind = MediaItemKind.Track,
            Source = path
        };

        lock (_gate)
        {
            _activeEngine = "files";
            _filesItem = item;
            _bookmarkNavigationCursor = null;
            // Nowy strumien: stary czas przestaje obowiazywac, zanim dekoder
            // zdazy podac nowy. Inaczej okno pokazywalo dlugosc POPRZEDNIEGO pliku.
            ForgetStaleFilesDurationLocked(item.Id);
            _filesVolume = volume;
            _filesRate = rate;
            _requestedRate = rate;
            _filesPaused = false;
        }
        // BEZPOSREDNIE odtworzenie wychodzi z kolejki: koniec tego utworu nie
        // moze jej przesunac. Zachowanie files.play pozostaje niezmienione.
        _queue.DetachFromDirectPlay();
        ConfigureOutputForSession("files");
        _files.Play(item, position, _filesMuted ? 0 : volume, rate);
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
        TrySaveCurrentLocalResume();

        var position = TimeSpan.FromSeconds(LiteArgs.ReadDouble(
            args, "positionSeconds", 0d, 0d, 604_800d));
        var volume = LiteArgs.ReadInt(args, "volume", _filesVolume, 0, 100);
        var requestedRate = LiteArgs.ReadDouble(args, "rate", _filesRate, 0.5d, 2.0d);
        var item = new MediaItem
        {
            Id = LiteArgs.RequireText(args, "id"),
            Title = LiteArgs.ReadText(args, "title") ?? "Materiał",
            Kind = MediaItemKind.Episode,
            Source = source
        };
        PodcastPlaybackOptionsSnapshot? playbackOptions = null;
        if (_podcastProgressStore is not null)
        {
            try
            {
                playbackOptions = _podcastProgressStore.GetPlaybackOptions(
                    PodcastPlaybackOptionsTarget.Episode,
                    item.Id);
            }
            catch (LiteRequestException)
            {
                // Publiczny material moze byc odtwarzany przed dodaniem do
                // Biblioteki. Brak rekordu oznacza wtedy zwykle ustawienia
                // sesji, nie odmowe odtwarzania.
            }
        }
        var resolvedResumeMode = playbackOptions?.ResolvedResumePositionMode
            ?? ResumePositionMode.Inherit;
        var shouldRememberPosition = resolvedResumeMode switch
        {
            ResumePositionMode.Remember => true,
            ResumePositionMode.StartFromBeginning => false,
            _ => _podcastSessionResumeMode != ResumePositionMode.StartFromBeginning
        };
        if (!shouldRememberPosition)
            position = TimeSpan.Zero;
        var rate = playbackOptions?.ResolvedPlaybackRateOverride ?? requestedRate;
        var audio = playbackOptions is null
            ? CloneAudioSettings(_configuredAudioSettings)
            : ResolvePodcastAudio(playbackOptions);
        EnsureTempoAlgorithmAvailable(audio.TempoAlgorithm);

        lock (_gate)
        {
            _activeEngine = "files";
            _filesItem = item;
            _bookmarkNavigationCursor = null;
            _currentPodcastCompleted = false;
            ForgetStaleFilesDurationLocked(item.Id);
            _filesVolume = volume;
            _filesRate = rate;
            _requestedRate = requestedRate;
            _filesPaused = false;
        }
        _queue.DetachFromDirectPlay();
        ConfigureOutputForSession("podcasts");
        _files.ConfigureAudioProcessing(audio);
        _files.Play(item, position, _filesMuted ? 0 : volume, rate);
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
        var volume = LiteArgs.ReadInt(args, "volume", _radioVolume, 0, 100);
        var item = new MediaItem
        {
            Id = LiteArgs.ReadText(args, "id") ?? "station:" + url,
            Title = LiteArgs.ReadText(args, "title") ?? uri.Host,
            Kind = MediaItemKind.Station,
            Source = url
        };

        lock (_gate)
        {
            _activeEngine = "radio";
            _radioItem = item;
            _radioVolume = volume;
            _radioPaused = false;
        }
        // Radio ma osobny tor. Nie odłącza kolejki plików i nie zapisuje
        // podcastu, ponieważ oba źródła mogą nadal grać równolegle.
        ConfigureOutputForSession("radio");
        _radio.Play(item, TimeSpan.Zero, _radioMuted ? 0 : volume, 1d);
        return new { ok = true, engine = "radio", id = item.Id, title = item.Title };
    }

    private LiteTransportTarget ResolveTransportTarget(JsonElement args)
    {
        var requested = LiteArgs.ReadText(args, "sessionId")?.Trim();
        lock (_gate)
        {
            return LiteTransportRouting.Resolve(
                requested,
                _activeEngine,
                _filesItem?.Kind == MediaItemKind.Episode);
        }
    }

    private bool TargetOwnsFilesItemLocked(string sessionId) =>
        _filesItem is not null
        && (sessionId is "podcasts" or "radioarchiwista"
            ? _filesItem.Kind == MediaItemKind.Episode
            : _filesItem.Kind != MediaItemKind.Episode);

    private void RequireTargetOwnsFilesItemLocked(LiteTransportTarget target)
    {
        if (target.Engine != "files" || TargetOwnsFilesItemLocked(target.SessionId))
            return;

        throw new LiteRequestException(
            $"Najpierw uruchom materiał w sesji {(target.SessionId == "podcasts" ? "Podcasty" : "Pliki lokalne")}.");
    }

    private object PauseResume(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine == "spotify")
        {
            lock (_gate) _activeEngine = target.Engine;
            return RequireSpotifyPlayback().PauseResume();
        }
        return target.Engine == "files"
            ? RunLocalPlaybackStart(() => PauseResumeResolved(target))
            : PauseResumeResolved(target);
    }

    private object PauseResumeResolved(LiteTransportTarget target)
    {
        // Gdy transport prowadzi KOLEJKA, pauza idzie przez jej sesje: tylko ona
        // zapamietuje pozycje tak, jak reszta AMC. Pytamy PRZED wejsciem w _gate,
        // zeby nie trzymac dwoch blokad naraz.
        string? currentId;
        lock (_gate)
        {
            _activeEngine = target.Engine;
            RequireTargetOwnsFilesItemLocked(target);
            currentId = _filesItem?.Id;
        }
        if (target.Engine == "files" && _queue.OwnsCurrent(currentId))
        {
            // Czas notujemy PRZED pauza, z zywego wyjscia.
            _queue.NotePosition(_files.Position);
            var status = _queue.PauseResume();
            lock (_gate)
            {
                _filesPaused = status.Paused;
                _filesPlaying = !status.Paused;
            }
            // Pauza to swiadome przerwanie: utrwalamy punkt wznowienia.
            // Wznowienie gry tez zapisuje -- czas jest wtedy wciaz wazny, a
            // zapis i tak pomija checkpoint bez zmiany.
            _queue.SaveResumeCheckpoint();
            return new
            {
                paused = status.Paused,
                engine = "files",
                sessionId = target.SessionId,
                queue = true
            };
        }

        object result;
        var saveLocalResume = false;
        lock (_gate)
        {
            if (target.Engine == "radio")
            {
                var station = _radioItem
                    ?? throw new LiteRequestException("Najpierw uruchom stację radiową.");
                if (_radioPaused)
                {
                    _radioPaused = false;
                    _radioPlaying = true;
                    _radio.Play(
                        station,
                        TimeSpan.Zero,
                        _radioMuted ? 0 : _radioVolume,
                        _radioRate);
                }
                else
                {
                    _radioPaused = true;
                    _radio.Pause();
                }
                return new
                {
                    paused = _radioPaused,
                    engine = "radio",
                    sessionId = target.SessionId,
                    queue = false
                };
            }

            var track = _filesItem!;
            if (_filesPaused)
            {
                _filesPaused = false;
                _filesPlaying = true;
                _files.Play(
                    track,
                    _files.Position,
                    _filesMuted ? 0 : _filesVolume,
                    _filesRate);
            }
            else
            {
                _filesPaused = true;
                _files.Pause();
            }
            saveLocalResume = track.Kind == MediaItemKind.Track;
            result = new
            {
                paused = _filesPaused,
                engine = "files",
                sessionId = target.SessionId,
                queue = false
            };
        }
        if (saveLocalResume) TrySaveCurrentLocalResume();
        return result;
    }

    /// <summary>
    /// Tekst Alt+D sklada host C# tym samym formatterem parametrow audio, co
    /// pelne AMC. Frontend dostaje gotowa, celowa etykiete dla uzytkownika,
    /// nigdy reprezentacje obiektu ani identyfikator urzadzenia lub stacji.
    /// </summary>
    private string BuildCurrentRadioBroadcastText(MediaItem item)
    {
        var parts = new List<string> { item.Title };
        var audio = AudioParametersFormatter.FormatCompact(item);
        if (!string.IsNullOrWhiteSpace(audio)) parts.Add(audio);

        parts.Add(CurrentRadioStreamTitle(item) ?? "brak nazwy bieżącej audycji lub utworu");
        return string.Join(", ", parts);
    }

    /// <summary>Bieżący utwór z ICY, jeśli stacja go podaje i nie jest samą nazwą stacji.</summary>
    private string? CurrentRadioStreamTitle(MediaItem item)
    {
        var streamTitle = _radio.CurrentStreamTitle?.Trim();
        return string.IsNullOrWhiteSpace(streamTitle) || NowPlayingParts.SameValue(item.Title, streamTitle)
            ? null
            : streamTitle;
    }

    private object CurrentRadioBroadcastInformation()
    {
        MediaItem? item;
        lock (_gate)
        {
            item = _radioItem;
        }
        if (item is null)
        {
            throw new LiteRequestException("Najpierw uruchom stację radiową.");
        }
        return new { text = BuildCurrentRadioBroadcastText(item) };
    }

    private object StopTransport(JsonElement args)
    {
        var requested = LiteArgs.ReadText(args, "sessionId")?.Trim();
        if (string.IsNullOrWhiteSpace(requested))
        {
            StopFilesPlayback();
            StopRadioPlayback();
            _spotifyPlayback?.Stop();
            return new { ok = true, queue = false, playing = false };
        }

        var target = ResolveTransportTarget(args);
        if (target.Engine == "spotify")
        {
            lock (_gate) _activeEngine = target.Engine;
            return RequireSpotifyPlayback().Stop();
        }
        if (target.Engine == "files")
        {
            lock (_gate)
            {
                // Pliki i Podcasty współdzielą dekoder, ale polecenie jednej
                // sesji nie może zatrzymać materiału należącego do drugiej.
                if (!TargetOwnsFilesItemLocked(target.SessionId))
                {
                    return new
                    {
                        ok = true,
                        engine = "files",
                        sessionId = target.SessionId,
                        queue = false,
                        playing = false
                    };
                }
            }
        }
        return target.Engine == "radio"
            ? StopRadioPlayback()
            : StopFilesPlayback();
    }

    private object StopFilesPlayback()
    {
        TrySaveCurrentPodcastProgress();
        // Gdy transport prowadzi KOLEJKA, zatrzymanie idzie przez jej sesje.
        // Samo _files.Stop() zostawialo sesje w stanie "gra" (IsPlaying), wiec
        // kolejne pauza/wznowienie i queue.status klamaly o dzwieku.
        // Prowadzenia NIE gasimy: zatrzymanie nie jest wyjsciem poza kolejke,
        // wiec wznowienie wraca na zapamietana pozycje tego samego utworu.
        string? currentId;
        lock (_gate)
        {
            _activeEngine = "files";
            currentId = _filesItem?.Id;
        }
        if (_queue.OwnsCurrent(currentId))
        {
            // Pozycje notujemy PRZED zatrzymaniem: po Stop wyjscie nie zna
            // juz czasu, a to jest moment, w ktorym uzytkownik przerwal.
            _queue.NotePosition(_files.Position);
            var status = _queue.Stop();
            lock (_gate)
            {
                _filesPaused = status.Paused;
                _filesPlaying = false;
            }
            // Zatrzymanie to swiadome przerwanie -- utrwalamy punkt wznowienia
            // od razu, bez czekania na zamkniecie procesu.
            _queue.SaveResumeCheckpoint();
            return new
            {
                ok = true,
                engine = "files",
                sessionId = _filesItem?.Kind == MediaItemKind.Episode ? "podcasts" : "files",
                queue = true,
                playing = status.Playing
            };
        }

        // Bezposrednie odtwarzanie: zatrzymanie konczy tez prowadzenie przez
        // kolejke, zeby stary koniec utworu jej nie przesunal.
        TrySaveCurrentLocalResume();
        _queue.DetachFromDirectPlay();
        _files.Stop();
        lock (_gate)
        {
            _filesPaused = false;
            _filesPlaying = false;
        }
        return new
        {
            ok = true,
            engine = "files",
            sessionId = _filesItem?.Kind == MediaItemKind.Episode ? "podcasts" : "files",
            queue = false,
            playing = false
        };
    }

    private object StopRadioPlayback()
    {
        _radio.Stop();
        lock (_gate)
        {
            _activeEngine = "radio";
            _radioPaused = false;
            _radioPlaying = false;
        }
        return new
        {
            ok = true,
            engine = "radio",
            sessionId = "radio",
            queue = false,
            playing = false
        };
    }

    private object Seek(JsonElement args)
    {
        var targetSession = ResolveTransportTarget(args);
        if (targetSession.Engine == "spotify")
        {
            lock (_gate) _activeEngine = targetSession.Engine;
            return RequireSpotifyPlayback().Seek(args);
        }
        var delta = LiteArgs.ReadDouble(args, "deltaSeconds", double.NaN, -86_400d, 86_400d);
        var absolute = LiteArgs.ReadDouble(args, "positionSeconds", double.NaN, 0d, 86_400d);
        var engine = targetSession.Engine;
        lock (_gate)
        {
            _activeEngine = engine;
            RequireTargetOwnsFilesItemLocked(targetSession);
            if (engine == "files") _bookmarkNavigationCursor = null;
        }

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
        if (engine == "files")
        {
            if (!_queue.NotePosition(target)) TrySaveCurrentLocalResume();
        }
        return new
        {
            engine,
            sessionId = targetSession.SessionId,
            positionSeconds = target.TotalSeconds
        };
    }

    private object JumpRadioToLive(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine != "radio")
            throw new LiteRequestException(
                "Powrót do transmisji na żywo jest dostępny tylko w Radiu.");

        MediaItem item;
        bool paused;
        bool playing;
        lock (_gate)
        {
            item = _radioItem ?? throw new LiteRequestException(
                "Radio nie ma otwartej transmisji.");
            if (!_radioPlaying)
                throw new LiteRequestException("Radio nie odtwarza transmisji.");
            _activeEngine = "radio";
            _radioRate = 1d;
            paused = _radioPaused;
            playing = _radioPlaying;
        }

        _radio.JumpToLive();
        return new
        {
            engine = "radio",
            sessionId = target.SessionId,
            id = item.Id,
            title = item.Title,
            playing,
            paused,
            queue = false,
            positionSeconds = _radio.Position.TotalSeconds,
            durationSeconds = 0d,
            bufferedSeconds = _radio.BufferedDuration.TotalSeconds,
            behindLiveSeconds = _radio.BehindLive.TotalSeconds,
            rate = 1d
        };
    }

    private object SeekRadioPercent(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine != "radio")
            throw new LiteRequestException(
                "Skok po buforze Time Shift jest dostępny tylko w Radiu.");

        var percentage = LiteArgs.ReadInt(args, "percentage", -1, 0, 100);
        MediaItem item;
        lock (_gate)
        {
            item = _radioItem ?? throw new LiteRequestException(
                "Radio nie ma otwartej transmisji.");
            if (!_radioPlaying)
                throw new LiteRequestException("Radio nie odtwarza transmisji.");
            _activeEngine = "radio";
        }

        if (!_radio.TryGetBufferedSeekPosition(percentage, out var position))
            throw new LiteRequestException(
                "Skok procentowy niedostępny: bufor transmisji jest pusty lub niegotowy.");

        _radio.Seek(position);
        var buffered = _radio.BufferedDuration;
        var offset = buffered - _radio.BehindLive;
        if (offset < TimeSpan.Zero) offset = TimeSpan.Zero;
        return new
        {
            engine = "radio",
            sessionId = target.SessionId,
            id = item.Id,
            title = item.Title,
            percentage,
            positionSeconds = _radio.Position.TotalSeconds,
            bufferPositionSeconds = offset.TotalSeconds,
            bufferedSeconds = buffered.TotalSeconds,
            behindLiveSeconds = _radio.BehindLive.TotalSeconds,
            rate = _radioRate
        };
    }

    private object SetVolume(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine == "spotify")
        {
            lock (_gate) _activeEngine = target.Engine;
            return RequireSpotifyPlayback().SetVolume(args);
        }
        int current;
        lock (_gate) current = target.Engine == "radio" ? _radioVolume : _filesVolume;
        var volume = LiteArgs.ReadInt(args, "volume", current, 0, 100);
        lock (_gate)
        {
            _activeEngine = target.Engine;
            if (target.Engine == "radio") _radioVolume = volume;
            else _filesVolume = volume;
        }
        if (target.Engine == "radio")
        {
            _radio.SetVolume(_radioMuted ? 0 : volume);
            return new
            {
                volume,
                engine = "radio",
                sessionId = target.SessionId,
                queue = false
            };
        }
        // Gdy transport prowadzi KOLEJKA, glosnosc idzie przez jej sesje: tylko
        // wtedy obowiazuje tez NASTEPNY utwor. Samo _files.SetVolume ruszalo
        // wyjscie, a kolejny Play wracal do wartosci sesji.
        var byQueue = _queue.SetVolume(volume);
        if (!byQueue) _files.SetVolume(volume);
        if (_filesMuted) _files.SetVolume(0);
        return new
        {
            volume,
            engine = "files",
            sessionId = target.SessionId,
            queue = byQueue
        };
    }

    private object ToggleMute(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine == "spotify")
        {
            lock (_gate) _activeEngine = target.Engine;
            return RequireSpotifyPlayback().ToggleMute();
        }
        bool muted;
        int volume;
        lock (_gate)
        {
            _activeEngine = target.Engine;
            if (target.Engine == "radio")
            {
                _radioMuted = !_radioMuted;
                muted = _radioMuted;
                volume = _radioVolume;
            }
            else
            {
                RequireTargetOwnsFilesItemLocked(target);
                _filesMuted = !_filesMuted;
                muted = _filesMuted;
                volume = _filesVolume;
            }
        }

        if (target.Engine == "radio")
        {
            _radio.SetVolume(muted ? 0 : volume);
        }
        else
        {
            // Nie zmieniamy zapamiętanej głośności ani kolejki. Wyciszenie
            // jest stanem toru, a ponowienie przywraca dokładnie poprzedni
            // poziom bieżącej sesji.
            _files.SetVolume(muted ? 0 : volume);
        }
        return new
        {
            engine = target.Engine,
            sessionId = target.SessionId,
            muted,
            volume
        };
    }

    private object SetRate(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine == "spotify")
        {
            return RequireSpotifyPlayback().SetRate(args);
        }
        double current;
        lock (_gate) current = target.Engine == "radio" ? _radioRate : _filesRate;
        var rate = LiteArgs.ReadDouble(args, "rate", current, 0.5d, 2.0d);
        lock (_gate)
        {
            _activeEngine = target.Engine;
            if (target.Engine == "radio") _radioRate = rate;
            else
            {
                _filesRate = rate;
                _requestedRate = rate;
            }
        }
        if (target.Engine == "radio")
        {
            _radio.SetPlaybackRate(rate);
            return new
            {
                rate,
                engine = "radio",
                sessionId = target.SessionId,
                queue = false
            };
        }
        // Jak przy glosnosci: przez sesje kolejki, zeby tempo przetrwalo przejscie.
        var byQueue = _queue.SetRate(rate);
        if (!byQueue) _files.SetPlaybackRate(rate);
        return new
        {
            rate,
            engine = "files",
            sessionId = target.SessionId,
            queue = byQueue
        };
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
            if (_filesItem?.Kind != MediaItemKind.Episode)
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

    private object RefreshPodcastsAutomatically()
    {
        if (_podcastRefresh is null)
        {
            throw new LiteRequestException(
                "Host nie dostał bazy Podcastów i YouTube. Automatyczne odświeżanie jest niedostępne.");
        }
        return _podcastRefresh.RefreshAutomatic();
    }

    private object PodcastGlobalSettings() =>
        PodcastGlobalSettingsPayload(RequirePodcastStore().GetGlobalSettings());

    private object ConfigurePodcastGlobalSettings(JsonElement args)
    {
        var snapshot = RequirePodcastStore().ConfigureGlobalSettings(
            LiteArgs.ReadText(args, "downloadsFolder"),
            ReadRequiredInt(args, "rssRefreshIntervalMinutes"),
            ReadRequiredInt(args, "youTubeRefreshIntervalMinutes"),
            ReadRequiredInt(args, "automaticRefreshBatchSize"));
        return PodcastGlobalSettingsPayload(snapshot);
    }

    private static object PodcastGlobalSettingsPayload(
        PodcastGlobalSettingsSnapshot snapshot) => new
        {
            downloadsFolder = snapshot.DownloadsFolder,
            rssRefreshIntervalMinutes = snapshot.RssRefreshIntervalMinutes,
            youTubeRefreshIntervalMinutes = snapshot.YouTubeRefreshIntervalMinutes,
            automaticRefreshBatchSize = snapshot.AutomaticRefreshBatchSize
        };

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
            if (_filesItem?.Kind != MediaItemKind.Episode) return;
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

    private object ConfigureLocalResume(JsonElement args)
    {
        var remember = LiteArgs.ReadBool(args, "remember", true);
        var localMode = ReadConfiguredResumeMode(args, "localResumeMode");
        var podcastMode = ReadConfiguredResumeMode(args, "podcastResumeMode");
        _queue.ConfigureResumePreferences(remember, localMode);
        lock (_gate) _podcastSessionResumeMode = podcastMode;
        return new
        {
            remember,
            localResumeMode = (int)localMode,
            podcastResumeMode = (int)podcastMode
        };
    }

    private static ResumePositionMode ReadConfiguredResumeMode(
        JsonElement args,
        string name)
    {
        var value = ReadNullableInt(args, name) ?? (int)ResumePositionMode.Inherit;
        return Enum.IsDefined(typeof(ResumePositionMode), value)
            ? (ResumePositionMode)value
            : throw new LiteRequestException("Nieznany sposób wznawiania sesji.");
    }

    private object SaveCurrentLocalResume() => new
    {
        saved = TrySaveCurrentLocalResume(),
        persistError = _queue.LastPersistError
    };

    /// <summary>
    /// Zapis pozycji pliku granego WPROST, poza kolejka i poza Podcastami.
    /// Pozycje kolejki i odcinkow maja wlasnych wlascicieli i nie wolno ich
    /// zapisac drugi raz ta droga.
    /// </summary>
    private bool TrySaveCurrentLocalResume(bool completed = false)
    {
        MediaItem? item;
        lock (_gate) item = _filesItem;
        if (item is null
            || item.Kind != MediaItemKind.Track
            || _queue.OwnsCurrent(item.Id)
            || string.IsNullOrWhiteSpace(item.Source))
        {
            return false;
        }
        return _queue.SaveDirectResume(
            item.Id,
            item.Source,
            completed ? TimeSpan.Zero : _files.Position,
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

    private object Status(JsonElement args)
    {
        var target = ResolveTransportTarget(args);
        if (target.Engine == "spotify")
        {
            lock (_gate) _activeEngine = target.Engine;
            return RequireSpotifyPlayback().Status();
        }
        var engine = target.Engine;
        bool paused;
        bool playing;
        bool muted;
        MediaItem? item;
        TimeSpan filesDuration;
        int volume;
        double rate;
        lock (_gate)
        {
            _activeEngine = engine;
            if (engine == "radio")
            {
                paused = _radioPaused;
                playing = _radioPlaying;
                muted = _radioMuted;
                item = _radioItem;
                volume = _radioVolume;
                rate = _radioRate;
            }
            else
            {
                paused = _filesPaused;
                playing = _filesPlaying;
                muted = _filesMuted;
                item = TargetOwnsFilesItemLocked(target.SessionId) ? _filesItem : null;
                if (item is null)
                {
                    paused = false;
                    playing = false;
                    muted = false;
                }
                volume = _filesVolume;
                rate = _filesRate;
            }
            // Oddajemy czas TYLKO wtedy, gdy zmierzono go dla tego samego Id.
            filesDuration = item is not null
                && string.Equals(_filesDurationId, item.Id, StringComparison.Ordinal)
                    ? _filesDuration
                    : TimeSpan.Zero;
        }

        var position = item is null
            ? TimeSpan.Zero
            : engine == "radio" ? _radio.Position : _files.Position;
        // Dla plikow bierzemy czas zmierzony przez dekoder; MediaItem z listy
        // folderu go nie zna. Radio nie ma dlugosci - zostaje zero.
        var duration = engine == "radio"
            ? item?.Duration ?? TimeSpan.Zero
            : filesDuration != TimeSpan.Zero ? filesDuration : item?.Duration ?? TimeSpan.Zero;
        // Gotowy, uzytkowy tekst dla natywnego paska stanu. Frontend nie
        // rekonstruuje bitrate'u ani metadanych z technicznych pol protokolu.
        // Ta sama funkcja obsluguje Alt+D, wiec oba odczyty pozostaja zgodne.
        var currentBroadcastText = engine == "radio" && item is not null
            ? BuildCurrentRadioBroadcastText(item)
            : null;
        var currentPlaybackText = item is null
            ? "Nic nie jest odtwarzane"
            : currentBroadcastText ?? string.Join(", ",
                new[] { item.Title, AudioParametersFormatter.FormatCompact(item) }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
        return new
        {
            engine,
            sessionId = target.SessionId,
            playing,
            paused,
            muted,
            id = item?.Id,
            title = item?.Title,
            // Sciezka jest czescia WEWNETRZNEGO protokolu, potrzebna do
            // sprawdzenia tozsamosci zaznaczenia i wywolania wspolnego
            // eksportera. Frontend nie uzywa jej jako nazwy kontrolki.
            source = engine == "files" ? item?.Source : null,
            positionSeconds = position.TotalSeconds,
            durationSeconds = duration.TotalSeconds,
            volume,
            rate,
            // Czas transmisji istnieje tylko dla radia; dla plikow jest zerem.
            bufferedSeconds = engine == "radio" ? _radio.BufferedDuration.TotalSeconds : 0d,
            behindLiveSeconds = engine == "radio" ? _radio.BehindLive.TotalSeconds : 0d,
            loadedId = item is null
                ? null
                : engine == "radio" ? _radio.LoadedItemId : _files.LoadedItemId,
            currentBroadcastText,
            currentPlaybackText,
            // Sam utwór dla tytułu okna (NVDA+T); pasek stanu ma pełny opis wyżej.
            streamTitle = engine == "radio" && item is not null ? CurrentRadioStreamTitle(item) : null
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

    private object ImportPlaylistToLibrary(JsonElement args)
    {
        var path = LiteArgs.RequirePath(args, "path");
        if (!File.Exists(path)) throw new LiteRequestException($"Nie znaleziono pliku listy: {path}");
        var imported = RadioPlaylistImporter.Import(path);
        return RequireProfileMutations().ImportRadioStations(
            imported.Stations
                .Select(station => new LiteRadioStationDraft(station.Name, station.StreamUrl))
                .ToArray(),
            imported.SkippedEntries);
    }

    private object ExportRadioFavorites(JsonElement args) =>
        RequireProfileMutations().ExportRadioFavorites(
            LiteArgs.RequirePath(args, "path"));

    private object PodcastOpmlInspect(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).Inspect(args);

    private object PodcastDownloadSaveAsInfo(JsonElement args) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).SaveAsInfo(args);

    private object PodcastDownloadSaveAs(JsonElement args, LiteEventSink events) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).SaveAs(args, events);

    private object PodcastVideoDownload(JsonElement args, LiteEventSink events) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).DownloadVideos(args, events);

    private object PodcastVideoSaveAsInfo(JsonElement args) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).VideoSaveAsInfo(args);

    private object PodcastVideoSaveAs(JsonElement args, LiteEventSink events) =>
        (_podcastDownloads ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).SaveVideoAs(args, events);

    private object PodcastOpmlImport(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).Import(args);

    private object PodcastOpmlExport(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).Export(args);

    private object PodcastYouTubeExport(JsonElement args) =>
        (_podcastOpml ?? throw new LiteRequestException(
            "Host nie ma dostępu do bazy Podcastów i YouTube.")).ExportYouTube(args);

    private object TidalSearch(JsonElement args) =>
        (_tidalCatalog ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do katalogu TIDAL."))
        .Search(args);

    private LiteTidalCatalogCoordinator RequireTidal() =>
        _tidalCatalog ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do katalogu TIDAL.");

    private object TidalAccountStatus() => RequireTidal().AccountStatus();

    private object TidalLogin() => RequireTidal().Login();

    private object TidalContainerItems(JsonElement args) =>
        (_tidalCatalog ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do katalogu TIDAL."))
        .GetContainerItems(args);

    private object TidalCollectionView(JsonElement args) =>
        (_tidalCatalog ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do katalogu TIDAL."))
        .GetCollectionView(args);

    private object TidalCollectionMembership(JsonElement args) =>
        (_tidalCatalog ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do katalogu TIDAL."))
        .ChangeMembership(args);

    private LiteSpotifyCatalogCoordinator RequireSpotify() =>
        _spotifyCatalog ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do katalogu Spotify.");

    private LiteSpotifyPlaybackCoordinator RequireSpotifyPlayback() =>
        _spotifyPlayback ?? throw new LiteRequestException(
            "Host nie ma dostępu do natywnego odtwarzania Spotify.");

    private object SpotifyCapabilities() => RequireSpotify().Capabilities();

    private object SpotifyAccountStatus() => RequireSpotify().AccountStatus();

    private object SpotifyLogin() => RequireSpotify().Login();

    private object SpotifyCollectionRoot() => RequireSpotify().CollectionRoot();

    private object SpotifyCollectionView(JsonElement args) =>
        RequireSpotify().CollectionView(args);

    private object SpotifyContainerItems(JsonElement args) =>
        RequireSpotify().ContainerItems(args);

    private object SpotifySearch(JsonElement args) => RequireSpotify().Search(args);

    private object SpotifyDescription(JsonElement args) =>
        RequireSpotify().Description(args);

    private object SpotifyCollectionMembership(JsonElement args) =>
        RequireSpotify().ChangeMembership(args);

    private object SpotifyPlaylistTargets() => RequireSpotify().PlaylistTargets();

    private object SpotifyPlaylistCreate(JsonElement args) =>
        RequireSpotify().CreatePlaylist(args);

    private object SpotifyPlaylistAddItems(JsonElement args) =>
        RequireSpotify().AddPlaylistItems(args);

    private LiteWiiMCoordinator RequireWiiM() =>
        _wiim ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do obsługi WiiM.");

    private LiteFrontierSmartCoordinator RequireFrontierSmart() =>
        _frontierSmart ?? throw new LiteRequestException(
            "Host nie ma dostępu do profilu AMC potrzebnego do obsługi Frontier Smart.");

    private LiteSonosCoordinator RequireSonos() =>
        _sonos ?? throw new LiteRequestException(
            "Host nie ma dostępu do konta potrzebnego do obsługi Sonos.");

    private object ConfigureAudio(JsonElement args)
    {
        var settings = LiteAudioSettings.Read(args);
        var pauseAfterOutputFallback = LiteArgs.ReadBool(
            args,
            "pauseOnOutputFallback",
            false);
        EnsureTempoAlgorithmAvailable(settings.TempoAlgorithm);
        lock (_gate)
        {
            _configuredAudioSettings = CloneAudioSettings(settings);
            _pauseRadioAfterOutputFallback = pauseAfterOutputFallback;
        }
        _files.ConfigureAudioProcessing(settings);
        _radio.ConfigureTempoAlgorithm(settings.TempoAlgorithm);
        _radio.ConfigureOutputFallbackBehavior(pauseAfterOutputFallback);
        return new
        {
            loudnessNormalization = settings.LoudnessNormalizationEnabled,
            smoothTrackTransitions = settings.SmoothTrackTransitionsEnabled,
            interTrackSilenceMs = settings.InterTrackSilenceMilliseconds,
            tempoAlgorithm = (int)settings.TempoAlgorithm,
            tempoAlgorithmAvailable = true,
            pauseOnOutputFallback = pauseAfterOutputFallback,
            appliesOnNextPlayback = true
        };
    }

    private PlaybackAudioSettings ResolvePodcastAudio(
        PodcastPlaybackOptionsSnapshot options)
    {
        PlaybackAudioSettings configured;
        lock (_gate) configured = CloneAudioSettings(_configuredAudioSettings);
        if (options.ResolvedLoudnessNormalizationOverride is bool loudness)
            configured.LoudnessNormalizationEnabled = loudness;
        if (options.ResolvedSmoothTrackTransitionsOverride is bool transitions)
            configured.SmoothTrackTransitionsEnabled = transitions;
        if (options.ResolvedInterTrackSilenceMillisecondsOverride is int silence)
            configured.InterTrackSilenceMilliseconds = silence;
        if (options.ResolvedTempoAlgorithmOverride is PlaybackTempoAlgorithm tempo)
            configured.TempoAlgorithm = tempo;
        return configured;
    }

    private static PlaybackAudioSettings CloneAudioSettings(
        PlaybackAudioSettings source) => new()
    {
        LoudnessNormalizationEnabled = source.LoudnessNormalizationEnabled,
        SmoothTrackTransitionsEnabled = source.SmoothTrackTransitionsEnabled,
        InterTrackSilenceMilliseconds = source.InterTrackSilenceMilliseconds,
        TempoAlgorithm = source.TempoAlgorithm
    };

    private static void EnsureTempoAlgorithmAvailable(PlaybackTempoAlgorithm algorithm)
    {
        if (algorithm != PlaybackTempoAlgorithm.SoundTouch
            && !AmcTempoNativeLibrary.IsAvailable)
        {
            throw new LiteRequestException(
                "Nowe silniki tempa są niedostępne. Wybierz SoundTouch lub napraw pakiet programu.");
        }
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
        if (sessionId == LiteSpotifyCatalogContract.SessionId)
            return RequireSpotifyPlayback().SelectOutput(args);
        if (sessionId is not ("files" or "radio" or "podcasts"))
            throw new LiteRequestException("Ta sesja nie obsługuje wyboru urządzenia audio.");
        return sessionId is "files" or "podcasts"
            ? RunLocalPlaybackStart(() => SelectOutputResolved(args, sessionId))
            : SelectOutputResolved(args, sessionId);
    }

    private object SelectOutputResolved(JsonElement args, string sessionId)
    {
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
                "radio" => _radioItem is not null
                    && (_radio.LoadedItemId is not null || _radio.IsPreparing),
                "podcasts" => _filesItem?.Kind == MediaItemKind.Episode
                    && (_files.LoadedItemId is not null || _files.IsPreparing),
                _ => _filesItem is not null
                    && _filesItem.Kind != MediaItemKind.Episode
                    && (_files.LoadedItemId is not null || _files.IsPreparing)
            };
            item = sessionId == "radio" ? _radioItem : _filesItem;
            paused = sessionId == "radio" ? _radioPaused : _filesPaused;
            volume = sessionId == "radio" ? _radioVolume : _filesVolume;
            rate = sessionId == "radio" ? _radioRate : _filesRate;
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
                    if (!paused) _files.Play(
                        item,
                        position,
                        _filesMuted ? 0 : volume,
                        rate);
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

    private object ListOutputs(JsonElement args)
    {
        if (string.Equals(
            LiteArgs.ReadText(args, "sessionId")?.Trim(),
            LiteSpotifyCatalogContract.SessionId,
            StringComparison.Ordinal))
        {
            return RequireSpotifyPlayback().ListOutputs(args);
        }
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
            TrySaveCurrentLocalResume();
            TrySaveCurrentPodcastProgress();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("[lite-host] nie zapisano pozycji wznowienia: " + exception.Message);
        }

        // Najpierw domykamy prywatne tory nagrywania. Sa niezalezne od
        // slyszalnego radia, ale nadal korzystaja z tych samych bibliotek.
        _recordings.Dispose();
        // Librespot jest osobnym procesem potomnym. Odłączamy jego zdarzenia i
        // kończymy go, zanim wspólny kanał protokołu przestanie istnieć.
        _spotifyPlayback?.Dispose();
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
        _podcastChapters?.Dispose();
        _podcastDirectorySearch.Dispose();
        _radioDirectorySearch.Dispose();
        _podcastProgressStore?.Dispose();
        _tidalDesktop.Dispose();
    }
}
