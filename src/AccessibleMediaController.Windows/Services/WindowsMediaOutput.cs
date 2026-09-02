using System.Diagnostics;
using System.IO;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;
using SoundTouch.Net.NAudioSupport;

namespace AccessibleMediaController.Windows.Services;

public sealed class MediaDurationAvailableEventArgs(
    MediaItem item,
    TimeSpan duration,
    int sampleRateHz) : EventArgs
{
    public MediaItem Item { get; } = item;
    public TimeSpan Duration { get; } = duration;
    public int SampleRateHz { get; } = sampleRateHz;
}

public sealed class MediaOutputFailedEventArgs(MediaItem? item, string message) : EventArgs
{
    public MediaItem? Item { get; } = item;
    public string Message { get; } = message;
}

public sealed class MediaPlaybackEndedEventArgs(MediaItem item) : EventArgs
{
    public MediaItem Item { get; } = item;
}

public sealed class MediaPlaybackPreparingEventArgs(MediaItem item, bool cloudDownloadRequired) : EventArgs
{
    public MediaItem Item { get; } = item;
    public bool CloudDownloadRequired { get; } = cloudDownloadRequired;
}

public sealed class MediaPlaybackStartedEventArgs(MediaItem item) : EventArgs
{
    public MediaItem Item { get; } = item;
}

public readonly record struct MediaMetadataReadResult(
    bool Success,
    TimeSpan Duration,
    int SampleRateHz,
    bool TimedOut);

/// <summary>
/// Windows output based on NAudio, shared WASAPI and SoundTouch. Opening and
/// disposing pipelines happens away from the WPF dispatcher. This is essential
/// for cloud placeholders: Windows may block a file open while iCloud,
/// OneDrive or Google Drive hydrates the selected file.
/// </summary>
public sealed class WindowsMediaOutput : IMediaOutput, IPlaybackAudioProcessingOutput, IDisposable
{
    private enum DecoderKind
    {
        System,
        SanitizedSystemMp3,
        ManagedMp3,
        Vorbis,
        FfmpegLocal
    }

    private enum Mp3DecoderMode
    {
        Automatic,
        SanitizedSystem,
        Managed
    }

    private readonly record struct ReaderSelection(
        GuardedWaveStream Reader,
        DecoderKind DecoderKind);

    private sealed class OwnedWaveStream(WaveStream inner, IDisposable owner) : WaveStream
    {
        private bool _disposed;
        public override WaveFormat WaveFormat => inner.WaveFormat;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
                try
                {
                    inner.Dispose();
                }
                finally
                {
                    owner.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }

    private sealed class PlaybackPipeline
    {
        public required MediaItem Item { get; init; }
        public required AudioOutputDeviceLease OutputLease { get; init; }
        public WasapiOut Output => OutputLease.Output;
        public required GuardedWaveStream DecoderGuard { get; init; }
        public required DecoderReadMonitorSampleProvider OutputReadMonitor { get; init; }
        public required SoundTouchWaveStream TempoStream { get; init; }
        public required LoudnessNormalizationSampleProvider LoudnessNormalizer { get; init; }
        public required VolumeSampleProvider VolumeProvider { get; init; }
        public required TrackTransitionSampleProvider TransitionProvider { get; init; }
        public required EventHandler<StoppedEventArgs> StoppedHandler { get; init; }
        public required bool MayRequireRemoteAccess { get; init; }
        public required DecoderKind DecoderKind { get; init; }
        public long SeekStartedTimestamp;
        public int SeekInProgress;
        public long LastObservedPositionTicks;
        public long LastProgressTimestamp;
    }

    private sealed class SeekWorkerState(PlaybackPipeline pipeline)
    {
        public PlaybackPipeline Pipeline { get; } = pipeline;
    }

    private readonly record struct RemoteFailureState(
        int Count,
        DateTimeOffset LastFailure,
        DateTimeOffset RetryAfter);

    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private readonly object _gate = new();
    private readonly HashSet<string> _quarantinedSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RemoteFailureState> _remoteFailures =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte>
        MetadataTimeoutSources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<MediaMetadataReadResult>>
        MetadataReadTasks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan LocalDecoderStallTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RemoteDecoderStallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LocalSeekStallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RemoteSeekStallTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LocalPreparationTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RemotePreparationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SlowSeekLogThreshold = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan EndOfFileGracePeriod = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan EndOfFilePositionTolerance = TimeSpan.FromMilliseconds(350);
    internal static readonly TimeSpan SmoothTrackTransitionDuration = TimeSpan.FromSeconds(4);
    private const long ManagedMp3FallbackMaximumBytes = 512L * 1024 * 1024;
    private PlaybackPipeline? _pipeline;
    private SeekWorkerState? _seekWorker;
    private MediaItem? _requestedItem;
    private TimeSpan _pendingPosition;
    private double _playbackRate = 1d;
    private int _volume = 35;
    private bool _loudnessNormalizationEnabled;
    private bool _smoothTrackTransitionsEnabled;
    private int _interTrackSilenceMilliseconds;
    private string? _outputDeviceId;
    private Func<MediaItem, PlaybackAudioSettings>? _audioProcessingResolver;
    private DateTimeOffset _nextAutomaticStartNotBeforeUtc;
    private long _requestVersion;
    private long _seekRequestVersion;
    private bool _preparing;
    private bool _pauseRequested;
    private bool _disposed;

    public event EventHandler<MediaDurationAvailableEventArgs>? DurationAvailable;
    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackEndedEventArgs>? PlaybackEnded;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;

    public string? LoadedItemId
    {
        get
        {
            lock (_gate) return _pipeline?.Item.Id;
        }
    }

    public bool IsPreparing
    {
        get
        {
            lock (_gate) return _preparing;
        }
    }

    public bool SupportsPlaybackRate => true;
    public PlaybackAudioProcessingCapabilities AudioProcessingCapabilities =>
        PlaybackAudioProcessingCapabilities.All;

    public void ConfigureAudioProcessing(PlaybackAudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!PlaybackAudioSettingsRules.IsSupportedSilence(settings.InterTrackSilenceMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Nieobsługiwana długość ciszy między utworami.");
        }

        PlaybackPipeline? pipeline;
        var fadeMilliseconds = settings.SmoothTrackTransitionsEnabled
            ? (int)SmoothTrackTransitionDuration.TotalMilliseconds
            : 0;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _loudnessNormalizationEnabled = settings.LoudnessNormalizationEnabled;
            _smoothTrackTransitionsEnabled = settings.SmoothTrackTransitionsEnabled;
            _interTrackSilenceMilliseconds = settings.InterTrackSilenceMilliseconds;
            pipeline = _pipeline;
        }
        if (pipeline is null) return;
        pipeline.LoudnessNormalizer.Enabled = settings.LoudnessNormalizationEnabled;
        pipeline.TransitionProvider.FadeDurationMilliseconds = fadeMilliseconds;
    }

    public void ConfigureAudioProcessingResolver(
        Func<MediaItem, PlaybackAudioSettings>? resolver)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _audioProcessingResolver = resolver;
        }
    }

    public void ConfigureOutputDevice(string? deviceId)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _outputDeviceId = string.IsNullOrWhiteSpace(deviceId)
                ? null
                : deviceId.Trim();
        }
    }

    public void BeginAutomaticTrackContinuation()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _nextAutomaticStartNotBeforeUtc = DateTimeOffset.UtcNow.AddMilliseconds(
                _interTrackSilenceMilliseconds);
        }
    }

    public void CancelAutomaticTrackContinuation()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _nextAutomaticStartNotBeforeUtc = default;
        }
    }

    public TimeSpan Position
    {
        get
        {
            PlaybackPipeline? pipeline;
            TimeSpan pending;
            lock (_gate)
            {
                pipeline = _pipeline;
                pending = _pendingPosition;
                if (pipeline is not null && ReferenceEquals(_seekWorker?.Pipeline, pipeline))
                {
                    return pending;
                }
            }
            if (pipeline is null) return pending;
            try
            {
                var position = pipeline.DecoderGuard.CachedCurrentTime;
                return position < TimeSpan.Zero ? TimeSpan.Zero : position;
            }
            catch (ObjectDisposedException)
            {
                return pending;
            }
        }
    }

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(item.Source))
        {
            throw new InvalidOperationException("Element nie zawiera lokalnego źródła dźwięku.");
        }

        Func<MediaItem, PlaybackAudioSettings>? audioProcessingResolver;
        lock (_gate) audioProcessingResolver = _audioProcessingResolver;
        if (audioProcessingResolver is not null)
        {
            ConfigureAudioProcessing(audioProcessingResolver(item));
        }

        var resolvedPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        var resolvedRate = Math.Clamp(playbackRate, 0.50d, 2.00d);
        var resolvedVolume = Math.Clamp(volume, 0, 100);
        var sourceAccess = MediaSourceAccessPolicy.Classify(item.Source);
        var mayRequireRemoteAccess = sourceAccess.RequiresRemoteAccess;
        PlaybackPipeline? reusable;
        bool sourceQuarantined;
        TimeSpan? remoteRetryDelay;
        DateTimeOffset startNotBeforeUtc;
        bool smoothTrackTransitions;
        lock (_gate)
        {
            startNotBeforeUtc = _nextAutomaticStartNotBeforeUtc;
            _nextAutomaticStartNotBeforeUtc = default;
            smoothTrackTransitions = _smoothTrackTransitionsEnabled;
            sourceQuarantined = !mayRequireRemoteAccess
                && _quarantinedSources.Contains(item.Source);
            reusable = _pipeline is not null
                && startNotBeforeUtc <= DateTimeOffset.UtcNow
                && string.Equals(_pipeline.Item.Source, item.Source, StringComparison.OrdinalIgnoreCase)
                ? _pipeline
                : null;
            remoteRetryDelay = mayRequireRemoteAccess && reusable is null
                ? GetRemoteRetryDelayLocked(item.Source)
                : null;
            _requestedItem = item;
            _pendingPosition = resolvedPosition;
            _playbackRate = resolvedRate;
            _volume = resolvedVolume;
            _pauseRequested = false;
        }

        if (sourceQuarantined)
        {
            DiagnosticLog.Warning("decoder-watchdog", $"Zablokowano ponowne otwarcie pliku po zatrzymaniu dekodera: {item.Source}.");
            RaisePlaybackFailed(
                item,
                "Ten plik wcześniej zatrzymał dekoder. Uruchom program ponownie po zastąpieniu lub naprawieniu pliku.");
            return;
        }

        if (remoteRetryDelay is { } delay)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
            RaisePlaybackFailed(
                item,
                $"Usługa chmurowa niedawno nie odpowiedziała. Ponów próbę za {seconds} s.");
            return;
        }

        if (reusable is not null)
        {
            try
            {
                SeekPipeline(reusable, resolvedPosition);
                reusable.VolumeProvider.Volume = resolvedVolume / 100f;
                ApplyRate(reusable, resolvedRate);
                AudioOutputPauseGuard.Play(
                    reusable.Output,
                    () =>
                    {
                        lock (_gate)
                        {
                            return _disposed
                                || !ReferenceEquals(_pipeline, reusable)
                                || _pauseRequested;
                        }
                    });
                return;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Error("playback", $"Nie udało się wznowić: {item.Title}.", exception);
                if (TryBeginMp3Recovery(reusable, exception)) return;
                RestartPipelineAfterResumeFailure(
                    reusable,
                    resolvedPosition,
                    resolvedVolume,
                    resolvedRate,
                    exception);
                return;
            }
        }

        PlaybackPipeline? previous;
        long requestVersion;
        lock (_gate)
        {
            requestVersion = ++_requestVersion;
            previous = DetachPipelineLocked();
            _preparing = true;
        }
        if (previous is not null)
        {
            var fadePrevious = smoothTrackTransitions
                && previous.Output.PlaybackState == PlaybackState.Playing;
            if (fadePrevious) previous.TransitionProvider.BeginManualFadeOut();
            QueuePipelineDisposal(
                previous,
                fadePrevious ? SmoothTrackTransitionDuration : TimeSpan.Zero);
        }

        DiagnosticLog.Info(
            "playback",
            $"Żądanie otwarcia: {item.Title}; dostęp zdalny: {mayRequireRemoteAccess}; źródło: {item.Source}.");
        PlaybackPreparing?.Invoke(
            this,
            new MediaPlaybackPreparingEventArgs(item, mayRequireRemoteAccess));

        _ = Task.Run(() => PrepareAndStartPipeline(
            item,
            requestVersion,
            resolvedPosition,
            resolvedVolume,
            resolvedRate,
            mp3DecoderMode: Mp3DecoderMode.Automatic,
            startNotBeforeUtc: startNotBeforeUtc));
        _ = WatchPreparationTimeoutAsync(
            item,
            requestVersion,
            mayRequireRemoteAccess ? RemotePreparationTimeout : LocalPreparationTimeout,
            mayRequireRemoteAccess);
    }

    private async Task WatchPreparationTimeoutAsync(
        MediaItem item,
        long requestVersion,
        TimeSpan timeout,
        bool mayRequireRemoteAccess)
    {
        await Task.Delay(timeout).ConfigureAwait(false);
        var timedOut = false;
        lock (_gate)
        {
            if (!_disposed && _preparing && requestVersion == _requestVersion)
            {
                ++_requestVersion;
                _preparing = false;
                if (!mayRequireRemoteAccess && !string.IsNullOrWhiteSpace(item.Source))
                {
                    _quarantinedSources.Add(item.Source);
                }
                else if (!string.IsNullOrWhiteSpace(item.Source))
                {
                    RecordRemoteFailureLocked(item.Source);
                }
                timedOut = true;
            }
        }
        if (!timedOut) return;

        DiagnosticLog.Warning(
            "playback",
            $"Przekroczono czas otwierania: {item.Title}; limit {timeout}.");
        RaisePlaybackFailed(
            item,
            "Przekroczono czas oczekiwania na plik. Sprawdź połączenie i stan usługi chmurowej.");
    }

    private async Task PrepareAndStartPipeline(
        MediaItem item,
        long requestVersion,
        TimeSpan requestedPosition,
        int requestedVolume,
        double requestedRate,
        Mp3DecoderMode mp3DecoderMode,
        DateTimeOffset startNotBeforeUtc = default)
    {
        PlaybackPipeline? pipeline = null;
        try
        {
            lock (_gate)
            {
                if (_disposed || requestVersion != _requestVersion) return;
            }
            pipeline = CreatePipeline(item, requestVersion, mp3DecoderMode);
            var startDelay = startNotBeforeUtc - DateTimeOffset.UtcNow;
            if (startDelay > TimeSpan.Zero)
            {
                await Task.Delay(startDelay).ConfigureAwait(false);
            }
            TimeSpan position;
            int volume;
            double rate;
            lock (_gate)
            {
                if (_disposed || requestVersion != _requestVersion)
                {
                    QueuePipelineDisposal(pipeline);
                    return;
                }

                position = _requestedItem?.Id == item.Id ? _pendingPosition : requestedPosition;
                volume = _requestedItem?.Id == item.Id ? _volume : requestedVolume;
                rate = _requestedItem?.Id == item.Id ? _playbackRate : requestedRate;
                _pipeline = pipeline;
                _preparing = false;
                if (pipeline.MayRequireRemoteAccess && !string.IsNullOrWhiteSpace(item.Source))
                {
                    _remoteFailures.Remove(item.Source);
                }
            }

            SeekPipeline(pipeline, position);
            pipeline.VolumeProvider.Volume = Math.Clamp(volume, 0, 100) / 100f;
            ApplyRate(pipeline, rate);
            AudioOutputPauseGuard.Play(
                pipeline.Output,
                () =>
                {
                    lock (_gate)
                    {
                        return _disposed
                            || !ReferenceEquals(_pipeline, pipeline)
                            || _pauseRequested;
                    }
                });
            Interlocked.Exchange(
                ref pipeline.LastObservedPositionTicks,
                pipeline.DecoderGuard.CachedCurrentTime.Ticks);
            Interlocked.Exchange(ref pipeline.LastProgressTimestamp, Stopwatch.GetTimestamp());
            _ = MonitorDecoderAsync(pipeline);
            var duration = pipeline.DecoderGuard.TotalTime;
            var sampleRateHz = pipeline.TempoStream.WaveFormat.SampleRate;
            DiagnosticLog.Info(
                "playback",
                $"Rozpoczęto: {item.Title}; czas {duration}; częstotliwość {sampleRateHz} Hz.");
            RaiseOnCapturedContext(() =>
            {
                DurationAvailable?.Invoke(
                    this,
                    new MediaDurationAvailableEventArgs(
                        item,
                        duration,
                        sampleRateHz));
                PlaybackStarted?.Invoke(this, new MediaPlaybackStartedEventArgs(item));
            });
        }
        catch (Exception exception)
        {
            if (pipeline is not null)
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_pipeline, pipeline)) _pipeline = null;
                }
                QueuePipelineDisposal(pipeline);
            }

            var currentRequest = false;
            var failedRemote = MediaSourceAccessPolicy
                .Classify(item.Source)
                .RequiresRemoteAccess;
            lock (_gate)
            {
                if (!_disposed && requestVersion == _requestVersion)
                {
                    _preparing = false;
                    if (failedRemote)
                    {
                        RecordRemoteFailureLocked(item.Source!);
                    }
                    currentRequest = true;
                }
            }
            DiagnosticLog.Error("playback", $"Nie udało się otworzyć: {item.Title}; źródło: {item.Source}.", exception);
            if (currentRequest) RaisePlaybackFailed(item, FriendlyPlaybackError(exception));
        }
    }

    private PlaybackPipeline CreatePipeline(
        MediaItem item,
        long requestVersion,
        Mp3DecoderMode mp3DecoderMode)
    {
        DiagnosticLog.Info("playback", $"Otwieranie dekodera: {item.Title}; żądanie {requestVersion}.");
        var mayRequireRemoteAccess = MediaSourceAccessPolicy
            .Classify(item.Source)
            .RequiresRemoteAccess;
        var selection = CreateReader(
            item.Source!,
            mp3DecoderMode,
            allowManagedMp3Fallback: true,
            mayRequireRemoteAccess);
        var reader = selection.Reader;
        SoundTouchWaveStream? tempoStream = null;
        AudioOutputDeviceLease? outputLease = null;
        try
        {
            tempoStream = new SoundTouchWaveStream(reader)
            {
                Tempo = 1d,
                Pitch = 1d,
                Rate = 1d
            };
            bool normalizeLoudness;
            bool smoothTrackTransitions;
            lock (_gate)
            {
                normalizeLoudness = _loudnessNormalizationEnabled;
                smoothTrackTransitions = _smoothTrackTransitionsEnabled;
            }
            var loudnessNormalizer = new LoudnessNormalizationSampleProvider(
                tempoStream.ToSampleProvider(),
                normalizeLoudness);
            var volumeProvider = new VolumeSampleProvider(loudnessNormalizer);
            var transitionProvider = new TrackTransitionSampleProvider(
                volumeProvider,
                () => reader.CachedCurrentTime,
                () => reader.TotalTime,
                smoothTrackTransitions
                    ? (int)SmoothTrackTransitionDuration.TotalMilliseconds
                    : 0);
            var outputReadMonitor = new DecoderReadMonitorSampleProvider(
                transitionProvider,
                item.Source);
            string? outputDeviceId;
            lock (_gate) outputDeviceId = _outputDeviceId;
            outputLease = AudioOutputDeviceCatalog.CreateOutput(outputDeviceId, 120);
            var output = outputLease.Output;
            PlaybackPipeline? pipeline = null;
            EventHandler<StoppedEventArgs> handler = (_, args) =>
            {
                if (pipeline is not null) OutputDevicePlaybackStopped(pipeline, args);
            };
            pipeline = new PlaybackPipeline
            {
                Item = item,
                OutputLease = outputLease,
                DecoderGuard = reader,
                OutputReadMonitor = outputReadMonitor,
                TempoStream = tempoStream,
                LoudnessNormalizer = loudnessNormalizer,
                VolumeProvider = volumeProvider,
                TransitionProvider = transitionProvider,
                StoppedHandler = handler,
                MayRequireRemoteAccess = mayRequireRemoteAccess,
                DecoderKind = selection.DecoderKind
            };
            output.PlaybackStopped += handler;
            output.Init(outputReadMonitor.ToWaveProvider());
            return pipeline;
        }
        catch
        {
            outputLease?.Dispose();
            if (tempoStream is not null) tempoStream.Dispose();
            else reader.Dispose();
            throw;
        }
    }

    internal static WaveStream OpenReaderForExport(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var mayRequireRemoteAccess = MediaSourceAccessPolicy
            .Classify(path)
            .RequiresRemoteAccess;
        return CreateReader(
            path,
            Mp3DecoderMode.Automatic,
            allowManagedMp3Fallback: true,
            mayRequireRemoteAccess).Reader;
    }

    private static ReaderSelection CreateReader(
        string path,
        Mp3DecoderMode mp3DecoderMode,
        bool allowManagedMp3Fallback,
        bool mayRequireRemoteAccess)
    {
        if (IsSupportedNetworkMediaSource(path))
        {
            // SoundTouch accepts IEEE-float samples. Local AudioFileReader and
            // the managed fallbacks already provide that format, but the
            // default MediaFoundationReader output for an HTTP resource is
            // usually 16-bit PCM. Requesting float output here keeps podcast
            // episodes on the same playback path as local media, including
            // volume, speed, seeking and the output-device selection.
            var networkReader = new MediaFoundationReader(
                path,
                CreateNetworkMediaFoundationReaderSettings());
            try
            {
                DiagnosticLog.Info("podcast-playback", $"Otwarto skończony materiał HTTP: {path}.");
                return new ReaderSelection(
                    new GuardedWaveStream(networkReader, path),
                    DecoderKind.System);
            }
            catch
            {
                networkReader.Dispose();
                throw;
            }
        }

        var extension = Path.GetExtension(path);
        MediaContainerProbeResult? containerProbe = null;
        if (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                containerProbe = MediaContainerProbe.Probe(path);
                if (!string.IsNullOrWhiteSpace(containerProbe.Value.Warning))
                {
                    DiagnosticLog.Warning(
                        "container-probe",
                        $"Nietypowy kontener: {path}; {containerProbe.Value.Warning}");
                }
                else
                {
                    DiagnosticLog.Info(
                        "container-probe",
                        $"Rozpoznano kontener {containerProbe.Value.Kind}: {path}.");
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or NotSupportedException
                    or ArgumentException)
            {
                DiagnosticLog.Warning(
                    "container-probe",
                    $"Nie udało się sprawdzić nagłówka bez pełnego odczytu: {path}; {exception.Message}");
            }
        }

        WaveStream reader;
        var decoderKind = DecoderKind.System;
        var hasOggExtension = extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".oga", StringComparison.OrdinalIgnoreCase);
        var useManagedVorbis = hasOggExtension
            && (containerProbe is null
                || containerProbe.Value.Kind == MediaContainerKind.OggVorbis);
        if (useManagedVorbis)
        {
            var vorbisReader = new NormalizedVorbisWaveReader(path);
            if (vorbisReader.HasNormalizedTimeline)
            {
                DiagnosticLog.Info(
                    "playback",
                    $"Znormalizowano oś czasu fragmentu OGG; początkowa próbka: {vorbisReader.SampleOrigin}.");
            }
            reader = vorbisReader;
            decoderKind = DecoderKind.Vorbis;
        }
        else if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            Mp3StructureProbeResult? probe = null;
            try
            {
                probe = Mp3StructureProbe.Probe(path);
                if (!string.IsNullOrWhiteSpace(probe.Value.Warning))
                {
                    DiagnosticLog.Warning(
                        "mp3-probe",
                        $"Nietypowa struktura MP3: {path}; {probe.Value.Warning}");
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or NotSupportedException
                    or ArgumentException)
            {
                DiagnosticLog.Warning(
                    "mp3-probe",
                    $"Nie udało się w sposób ograniczony sprawdzić początku MP3: {path}; {exception.Message}");
            }

            if (mp3DecoderMode == Mp3DecoderMode.Managed)
            {
                if (!CanUseManagedMp3Fallback(mayRequireRemoteAccess, probe))
                {
                    throw new InvalidDataException(
                        "Nie można bezpiecznie użyć awaryjnego dekodera dla tego pliku MP3.");
                }
                reader = CreateManagedMp3Reader(path);
                decoderKind = DecoderKind.ManagedMp3;
            }
            else
            {
                if (mp3DecoderMode == Mp3DecoderMode.SanitizedSystem)
                {
                    if (probe is not { HasConsecutiveFrames: true })
                    {
                        throw new InvalidDataException(
                            "Nie można bezpiecznie utworzyć oczyszczonego strumienia tego pliku MP3.");
                    }
                    try
                    {
                        reader = CreateSanitizedMp3Reader(path, probe.Value.AudioStartOffset);
                        decoderKind = DecoderKind.SanitizedSystemMp3;
                    }
                    catch (Exception exception) when (
                        allowManagedMp3Fallback
                        && IsDecoderFailure(exception)
                        && CanUseManagedMp3Fallback(mayRequireRemoteAccess, probe))
                    {
                        DiagnosticLog.Warning(
                            "mp3-fallback",
                            $"Oczyszczony strumień MP3 został odrzucony; użyto dekodera zarządzanego: {path}; {exception.Message}");
                        reader = CreateManagedMp3Reader(path);
                        decoderKind = DecoderKind.ManagedMp3;
                    }
                }
                else if (ShouldPreferManagedMp3ForPlayback(
                    allowManagedMp3Fallback,
                    mayRequireRemoteAccess,
                    probe))
                {
                    reader = CreateManagedMp3Reader(path);
                    decoderKind = DecoderKind.ManagedMp3;
                    DiagnosticLog.Warning(
                        "mp3-managed",
                        $"Nietypowy lokalny MP3 otwarto od razu odpornym dekoderem z indeksem ramek: {path}; początek {probe.GetValueOrDefault().AudioStartOffset}.");
                }
                else if (probe is { ShouldUseSanitizedStream: true })
                {
                    try
                    {
                        reader = CreateSanitizedMp3Reader(path, probe.Value.AudioStartOffset);
                        decoderKind = DecoderKind.SanitizedSystemMp3;
                        DiagnosticLog.Warning(
                            "mp3-sanitized",
                            $"Pominięto nietypowe dane poprzedzające audio MP3: {path}; początek {probe.Value.AudioStartOffset}.");
                    }
                    catch (Exception sanitizedException) when (IsDecoderFailure(sanitizedException))
                    {
                        DiagnosticLog.Warning(
                            "mp3-sanitized",
                            $"Oczyszczony strumień MP3 został odrzucony: {path}; {sanitizedException.Message}");
                        reader = CreateSystemOrManagedMp3Reader(
                            path,
                            allowManagedMp3Fallback,
                            mayRequireRemoteAccess,
                            probe,
                            out decoderKind);
                    }
                }
                else
                {
                    reader = CreateSystemOrManagedMp3Reader(
                        path,
                        allowManagedMp3Fallback,
                        mayRequireRemoteAccess,
                        probe,
                        out decoderKind);
                }
            }
        }
        else if (FfmpegLocalAudioWaveStream.ShouldPrefer(path)
            && FfmpegLocalAudioWaveStream.TryOpen(path, out var ffmpegReader))
        {
            reader = ffmpegReader;
            decoderKind = DecoderKind.FfmpegLocal;
            DiagnosticLog.Info(
                "ffmpeg-local",
                $"Użyto odpornego dekodera audio dla kontenera: {path}.");
        }
        else
        {
            reader = new AudioFileReader(path);
        }

        try
        {
            return new ReaderSelection(
                new GuardedWaveStream(reader, path),
                decoderKind);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    internal static bool IsSupportedNetworkMediaSource(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.UserInfo);

    internal static MediaFoundationReader.MediaFoundationReaderSettings
        CreateNetworkMediaFoundationReaderSettings() => new()
        {
            RequestFloatOutput = true,
            RepositionInRead = false,
            SingleReaderObject = true
        };

    private static WaveStream CreateManagedMp3Reader(string path)
    {
        var builder = new Mp3FileReader.FrameDecompressorBuilder(
            waveFormat => new Mp3FrameDecompressor(waveFormat));
        var decoder = new Mp3FileReaderBase(path, builder);
        try
        {
            return new WaveChannel32(decoder) { PadWithZeroes = false };
        }
        catch
        {
            decoder.Dispose();
            throw;
        }
    }

    private static WaveStream CreateSystemOrManagedMp3Reader(
        string path,
        bool allowManagedMp3Fallback,
        bool mayRequireRemoteAccess,
        Mp3StructureProbeResult? probe,
        out DecoderKind decoderKind)
    {
        try
        {
            decoderKind = DecoderKind.System;
            return new AudioFileReader(path);
        }
        catch (Exception systemException) when (IsDecoderFailure(systemException))
        {
            if (probe is { HasConsecutiveFrames: true })
            {
                try
                {
                    var sanitized = CreateSanitizedMp3Reader(path, probe.Value.AudioStartOffset);
                    decoderKind = DecoderKind.SanitizedSystemMp3;
                    DiagnosticLog.Warning(
                        "mp3-sanitized",
                        $"Dekoder ścieżki odrzucił MP3; użyto zweryfikowanego strumienia audio: {path}; {systemException.Message}");
                    return sanitized;
                }
                catch (Exception sanitizedException) when (IsDecoderFailure(sanitizedException))
                {
                    DiagnosticLog.Warning(
                        "mp3-sanitized",
                        $"Zweryfikowany strumień MP3 także został odrzucony: {path}; {sanitizedException.Message}");
                }
            }

            if (allowManagedMp3Fallback
                && CanUseManagedMp3Fallback(mayRequireRemoteAccess, probe))
            {
                DiagnosticLog.Warning(
                    "mp3-fallback",
                    $"Dekoder systemowy odrzucił MP3; użyto dekodera zarządzanego: {path}; {systemException.Message}");
                decoderKind = DecoderKind.ManagedMp3;
                return CreateManagedMp3Reader(path);
            }
            throw;
        }
    }

    private static WaveStream CreateSanitizedMp3Reader(string path, long audioStartOffset)
    {
        var source = BoundedSubrangeStream.OpenFile(path, audioStartOffset);
        try
        {
            var settings = new MediaFoundationReader.MediaFoundationReaderSettings
            {
                RequestFloatOutput = true,
                RepositionInRead = false,
                SingleReaderObject = true
            };
            var decoder = new StreamMediaFoundationReader(source, settings);
            return new OwnedWaveStream(decoder, source);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private static bool CanUseManagedMp3Fallback(
        bool mayRequireRemoteAccess,
        Mp3StructureProbeResult? probe) =>
        !mayRequireRemoteAccess
        && probe is { HasConsecutiveFrames: true }
        && probe.Value.FileLength is > 0 and <= ManagedMp3FallbackMaximumBytes;

    internal static bool ShouldPreferManagedMp3ForPlayback(
        bool allowManagedMp3Fallback,
        bool mayRequireRemoteAccess,
        Mp3StructureProbeResult? probe) =>
        allowManagedMp3Fallback
        && probe is { ShouldUseSanitizedStream: true }
        && CanUseManagedMp3Fallback(mayRequireRemoteAccess, probe);

    private static bool IsDecoderFailure(Exception exception) =>
        exception is IOException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException
            or InvalidCastException
            or System.Runtime.InteropServices.COMException;

    public static bool TryReadMetadata(
        string path,
        out TimeSpan duration,
        out int sampleRateHz)
    {
        duration = TimeSpan.Zero;
        sampleRateHz = 0;
        // Quick information must never trigger a cloud download. Metadata for
        // a placeholder is populated after the user explicitly plays it.
        if (MediaSourceAccessPolicy.Classify(path).RequiresRemoteAccess) return false;
        try
        {
            using var selection = CreateReader(
                path,
                mp3DecoderMode: Mp3DecoderMode.Automatic,
                allowManagedMp3Fallback: false,
                mayRequireRemoteAccess: false).Reader;
            duration = selection.TotalTime;
            sampleRateHz = selection.WaveFormat.SampleRate;
            return duration > TimeSpan.Zero || sampleRateHz > 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException
                or ArgumentException
                or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    public static async Task<MediaMetadataReadResult> TryReadMetadataAsync(
        string path,
        TimeSpan timeout)
    {
        if (MetadataTimeoutSources.ContainsKey(path))
        {
            return new MediaMetadataReadResult(false, TimeSpan.Zero, 0, true);
        }

        var task = MetadataReadTasks.GetOrAdd(
            path,
            static sourcePath => Task.Run(() =>
            {
                var success = TryReadMetadata(sourcePath, out var duration, out var sampleRateHz);
                return new MediaMetadataReadResult(success, duration, sampleRateHz, false);
            }));
        _ = task.ContinueWith(
            completedTask => MetadataReadTasks.TryRemove(path, out var removedTask),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        try
        {
            return await task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            MetadataTimeoutSources.TryAdd(path, 0);
            DiagnosticLog.Warning(
                "metadata-watchdog",
                $"Przerwano oczekiwanie na metadane po {timeout}: {path}.");
            return new MediaMetadataReadResult(false, TimeSpan.Zero, 0, true);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Warning(
                "metadata-watchdog",
                $"Odczyt metadanych nie powiódł się: {path}; {exception.Message}");
            return new MediaMetadataReadResult(false, TimeSpan.Zero, 0, false);
        }
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PlaybackPipeline? pipeline;
        var position = Position;
        lock (_gate)
        {
            _pendingPosition = position;
            _pauseRequested = true;
            if (_preparing)
            {
                ++_requestVersion;
                _preparing = false;
            }
            pipeline = _pipeline;
        }
        if (pipeline is null) return;
        try
        {
            pipeline.Output.Pause();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent track change already detached this pipeline.
        }
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var position = Position;
        PlaybackPipeline? pipeline;
        lock (_gate)
        {
            _pendingPosition = position;
            ++_requestVersion;
            _preparing = false;
            _pauseRequested = true;
            _requestedItem = null;
            pipeline = DetachPipelineLocked();
        }
        if (pipeline is not null) QueuePipelineDisposal(pipeline);
    }

    public void Seek(TimeSpan position)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var resolved = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        PlaybackPipeline? pipeline;
        SeekWorkerState? worker = null;
        lock (_gate)
        {
            _pendingPosition = resolved;
            ++_seekRequestVersion;
            pipeline = _pipeline;
            if (pipeline is not null && !ReferenceEquals(_seekWorker?.Pipeline, pipeline))
            {
                worker = new SeekWorkerState(pipeline);
                _seekWorker = worker;
                Interlocked.Exchange(ref pipeline.SeekStartedTimestamp, Stopwatch.GetTimestamp());
                Volatile.Write(ref pipeline.SeekInProgress, 1);
            }
        }
        if (worker is null) return;
        _ = Task.Run(() => ProcessSeekRequestsAsync(worker));
    }

    private async Task ProcessSeekRequestsAsync(SeekWorkerState worker)
    {
        var pipeline = worker.Pipeline;
        var seekStallTimeout = pipeline.MayRequireRemoteAccess
            ? RemoteSeekStallTimeout
            : LocalSeekStallTimeout;
        var stopwatch = Stopwatch.StartNew();
        var completedTarget = TimeSpan.Zero;
        try
        {
            while (true)
            {
                TimeSpan target;
                long requestVersion;
                lock (_gate)
                {
                    if (_disposed
                        || !ReferenceEquals(_pipeline, pipeline)
                        || !ReferenceEquals(_seekWorker, worker))
                    {
                        return;
                    }
                    target = _pendingPosition;
                    requestVersion = _seekRequestVersion;
                }

                try
                {
                    SeekPipeline(pipeline, target);
                }
                catch (TimeoutException)
                {
                    if (stopwatch.Elapsed >= seekStallTimeout)
                    {
                        StopUnresponsivePipeline(
                            pipeline,
                            $"Przewijanie nie zakończyło się przez {seekStallTimeout}.");
                        return;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(75)).ConfigureAwait(false);
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception exception) when (
                    exception is IOException
                        or UnauthorizedAccessException
                        or InvalidDataException
                        or NotSupportedException
                        or ArgumentException
                        or InvalidCastException
                        or System.Runtime.InteropServices.COMException)
                {
                    DiagnosticLog.Error(
                        "playback",
                        $"Przewijanie nie powiodło się: {pipeline.Item.Title}; cel {target}.",
                        exception);
                    if (TryBeginMp3Recovery(pipeline, exception)) return;
                    StopPipelineAfterSeekFailure(pipeline, FriendlyPlaybackError(exception));
                    return;
                }

                lock (_gate)
                {
                    if (_disposed || !ReferenceEquals(_pipeline, pipeline)) return;
                    if (requestVersion == _seekRequestVersion)
                    {
                        completedTarget = target;
                        if (ReferenceEquals(_seekWorker, worker))
                        {
                            _seekWorker = null;
                        }
                        break;
                    }
                }
            }

            if (stopwatch.Elapsed >= SlowSeekLogThreshold)
            {
                DiagnosticLog.Info(
                    "playback",
                    $"Przewinięto po doczytaniu: {pipeline.Item.Title}; cel {completedTarget}; czas {stopwatch.Elapsed}.");
            }
        }
        finally
        {
            var newerWorkerUsesPipeline = false;
            lock (_gate)
            {
                if (ReferenceEquals(_seekWorker, worker))
                {
                    _seekWorker = null;
                }
                newerWorkerUsesPipeline = ReferenceEquals(_seekWorker?.Pipeline, pipeline);
            }
            if (!newerWorkerUsesPipeline)
            {
                Volatile.Write(ref pipeline.SeekInProgress, 0);
                Interlocked.Exchange(ref pipeline.SeekStartedTimestamp, 0);
            }
        }
    }

    public void SetVolume(int volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PlaybackPipeline? pipeline;
        var resolved = Math.Clamp(volume, 0, 100);
        lock (_gate)
        {
            _volume = resolved;
            pipeline = _pipeline;
        }
        if (pipeline is not null) pipeline.VolumeProvider.Volume = resolved / 100f;
    }

    public void SetPlaybackRate(double playbackRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        PlaybackPipeline? pipeline;
        var resolved = Math.Clamp(playbackRate, 0.50d, 2.00d);
        lock (_gate)
        {
            _playbackRate = resolved;
            pipeline = _pipeline;
        }
        if (pipeline is not null) ApplyRate(pipeline, resolved);
    }

    private static void SeekPipeline(PlaybackPipeline pipeline, TimeSpan position)
    {
        var resolved = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        if (pipeline.TempoStream.TotalTime > TimeSpan.Zero && resolved > pipeline.TempoStream.TotalTime)
        {
            resolved = pipeline.TempoStream.TotalTime;
        }
        pipeline.TempoStream.CurrentTime = resolved;
    }

    private async Task MonitorDecoderAsync(PlaybackPipeline pipeline)
    {
        var decoderStallTimeout = pipeline.MayRequireRemoteAccess
            ? RemoteDecoderStallTimeout
            : LocalDecoderStallTimeout;
        var seekStallTimeout = pipeline.MayRequireRemoteAccess
            ? RemoteSeekStallTimeout
            : LocalSeekStallTimeout;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(_pipeline, pipeline)) return;
            }
            if (Volatile.Read(ref pipeline.SeekInProgress) != 0)
            {
                var seekStarted = Interlocked.Read(ref pipeline.SeekStartedTimestamp);
                if (seekStarted != 0
                    && Stopwatch.GetElapsedTime(seekStarted) >= seekStallTimeout)
                {
                    StopUnresponsivePipeline(
                        pipeline,
                        $"Przewijanie nie zakończyło się przez {seekStallTimeout}.");
                    return;
                }
                continue;
            }
            if (!pipeline.DecoderGuard.IsReadStalled(decoderStallTimeout)
                && !pipeline.OutputReadMonitor.IsReadStalled(decoderStallTimeout))
            {
                PlaybackState playbackState;
                try
                {
                    playbackState = pipeline.Output.PlaybackState;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                var position = pipeline.DecoderGuard.CachedCurrentTime;
                var positionTicks = Math.Max(0, position.Ticks);
                var previousTicks = Interlocked.Read(ref pipeline.LastObservedPositionTicks);
                if (playbackState != PlaybackState.Playing
                    || positionTicks != previousTicks)
                {
                    Interlocked.Exchange(ref pipeline.LastObservedPositionTicks, positionTicks);
                    Interlocked.Exchange(ref pipeline.LastProgressTimestamp, Stopwatch.GetTimestamp());
                    continue;
                }

                var lastProgress = Interlocked.Read(ref pipeline.LastProgressTimestamp);
                if (lastProgress == 0)
                {
                    Interlocked.Exchange(ref pipeline.LastProgressTimestamp, Stopwatch.GetTimestamp());
                    continue;
                }
                var withoutProgress = Stopwatch.GetElapsedTime(lastProgress);
                var totalTime = pipeline.DecoderGuard.TotalTime;
                var nearEnd = totalTime > TimeSpan.Zero
                    && totalTime - position <= EndOfFilePositionTolerance;
                if (nearEnd && withoutProgress >= EndOfFileGracePeriod)
                {
                    CompletePlaybackAfterMissingEndSignal(pipeline, position, totalTime);
                    return;
                }
                if (withoutProgress < decoderStallTimeout) continue;

                StopUnresponsivePipeline(
                    pipeline,
                    $"Tor odtwarzania nie przesunął pozycji przez {decoderStallTimeout}.");
                return;
            }

            StopUnresponsivePipeline(
                pipeline,
                $"Dekoder nie zwrócił danych przez {decoderStallTimeout}.");
            return;
        }
    }

    private void StopUnresponsivePipeline(PlaybackPipeline pipeline, string diagnosticReason)
    {
        if (TryBeginMp3Recovery(pipeline, originalException: null))
        {
            DiagnosticLog.Warning(
                "mp3-fallback",
                $"{diagnosticReason} Uruchomiono awaryjny dekoder dla: {pipeline.Item.Title}.");
            return;
        }

        var detached = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pendingPosition = pipeline.DecoderGuard.CachedCurrentTime;
                if (!pipeline.MayRequireRemoteAccess)
                {
                    _quarantinedSources.Add(pipeline.DecoderGuard.SourcePath);
                }
                else
                {
                    RecordRemoteFailureLocked(pipeline.DecoderGuard.SourcePath);
                }
                ++_requestVersion;
                _pipeline = null;
                pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
                detached = true;
            }
        }
        if (!detached) return;

        DiagnosticLog.Error(
            "decoder-watchdog",
            $"{diagnosticReason} Element: {pipeline.Item.Title}; źródło: {pipeline.DecoderGuard.SourcePath}.");
        QueuePipelineDisposal(pipeline);
        RaisePlaybackFailed(
            pipeline.Item,
            "Dekoder przestał odpowiadać. Odtwarzanie tego pliku zostało bezpiecznie zatrzymane.");
    }

    private void StopPipelineAfterSeekFailure(PlaybackPipeline pipeline, string userMessage)
    {
        var detached = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pendingPosition = pipeline.DecoderGuard.CachedCurrentTime;
                if (pipeline.MayRequireRemoteAccess)
                {
                    RecordRemoteFailureLocked(pipeline.DecoderGuard.SourcePath);
                }
                ++_requestVersion;
                _pipeline = null;
                if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
                pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
                detached = true;
            }
        }
        if (!detached) return;

        QueuePipelineDisposal(pipeline);
        RaisePlaybackFailed(pipeline.Item, userMessage);
    }

    private static void ApplyRate(PlaybackPipeline pipeline, double playbackRate)
    {
        pipeline.TempoStream.Tempo = Math.Clamp(playbackRate, 0.50d, 2.00d);
        pipeline.TempoStream.Pitch = 1d;
        pipeline.TempoStream.Rate = 1d;
    }

    private void OutputDevicePlaybackStopped(PlaybackPipeline pipeline, StoppedEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_pipeline, pipeline)) return;
        }
        if (args.Exception is not null)
        {
            if (TryBeginMp3Recovery(pipeline, args.Exception)) return;
            DiagnosticLog.Error("playback", $"Błąd toru audio: {pipeline.Item.Title}.", args.Exception);
            StopPipelineAfterPlaybackFailure(
                pipeline,
                FriendlyPlaybackError(args.Exception),
                quarantineSource: !pipeline.MayRequireRemoteAccess
                    && args.Exception is InvalidDataException
                        or NotSupportedException
                        or ArgumentException);
            return;
        }

        var completed = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pendingPosition = TimeSpan.Zero;
                ++_requestVersion;
                _pipeline = null;
                if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
                pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
                completed = true;
            }
        }
        if (!completed) return;
        DiagnosticLog.Info("playback", $"Koniec pliku: {pipeline.Item.Title}.");
        QueuePipelineDisposal(pipeline);
        RaiseOnCapturedContext(() =>
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(pipeline.Item)));
    }

    private void CompletePlaybackAfterMissingEndSignal(
        PlaybackPipeline pipeline,
        TimeSpan position,
        TimeSpan totalTime)
    {
        var completed = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pendingPosition = TimeSpan.Zero;
                ++_requestVersion;
                _pipeline = null;
                if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
                pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
                completed = true;
            }
        }
        if (!completed) return;

        DiagnosticLog.Warning(
            "decoder-watchdog",
            $"Tor wyjściowy nie zgłosił końca; rozpoznano koniec po braku postępu: "
            + $"{pipeline.Item.Title}; pozycja {position}; czas {totalTime}.");
        QueuePipelineDisposal(pipeline);
        RaiseOnCapturedContext(() =>
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(pipeline.Item)));
    }

    private void StopPipelineAfterPlaybackFailure(
        PlaybackPipeline pipeline,
        string userMessage,
        bool quarantineSource)
    {
        var detached = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pendingPosition = pipeline.DecoderGuard.CachedCurrentTime;
                if (quarantineSource)
                {
                    _quarantinedSources.Add(pipeline.DecoderGuard.SourcePath);
                }
                if (pipeline.MayRequireRemoteAccess)
                {
                    RecordRemoteFailureLocked(pipeline.DecoderGuard.SourcePath);
                }
                ++_requestVersion;
                _pipeline = null;
                if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
                pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
                detached = true;
            }
        }
        if (!detached) return;

        QueuePipelineDisposal(pipeline);
        RaisePlaybackFailed(pipeline.Item, userMessage);
    }

    private bool TryBeginMp3Recovery(
        PlaybackPipeline pipeline,
        Exception? originalException)
    {
        var path = pipeline.Item.Source;
        if (pipeline.DecoderKind is not (DecoderKind.System or DecoderKind.SanitizedSystemMp3)
            || pipeline.MayRequireRemoteAccess
            || string.IsNullOrWhiteSpace(path)
            || !Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || (originalException is not null && !IsDecoderFailure(originalException)))
        {
            return false;
        }

        Mp3StructureProbeResult probe;
        try
        {
            probe = Mp3StructureProbe.Probe(path);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
        var recoveryMode = pipeline.DecoderKind == DecoderKind.System
            && probe.HasConsecutiveFrames
                ? Mp3DecoderMode.SanitizedSystem
                : Mp3DecoderMode.Managed;
        if (recoveryMode == Mp3DecoderMode.Managed
            && !CanUseManagedMp3Fallback(mayRequireRemoteAccess: false, probe))
        {
            return false;
        }

        long requestVersion;
        TimeSpan position;
        int volume;
        double rate;
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_pipeline, pipeline)) return false;
            position = pipeline.DecoderGuard.CachedCurrentTime;
            _pendingPosition = position;
            volume = _volume;
            rate = _playbackRate;
            requestVersion = ++_requestVersion;
            _pipeline = null;
            if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
            pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
            _preparing = true;
        }

        DiagnosticLog.Warning(
            "mp3-fallback",
            $"Dekoder MP3 przerwał postęp; ponowna próba trybem {recoveryMode}: {path}"
            + (originalException is null ? "." : $"; {originalException.Message}"));
        QueuePipelineDisposal(pipeline);
        _ = Task.Run(() => PrepareAndStartPipeline(
            pipeline.Item,
            requestVersion,
            position,
            volume,
            rate,
            mp3DecoderMode: recoveryMode));
        _ = WatchPreparationTimeoutAsync(
            pipeline.Item,
            requestVersion,
            LocalPreparationTimeout,
            mayRequireRemoteAccess: false);
        return true;
    }

    private bool RestartPipelineAfterResumeFailure(
        PlaybackPipeline pipeline,
        TimeSpan position,
        int volume,
        double rate,
        Exception exception)
    {
        long requestVersion;
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_pipeline, pipeline)) return false;
            _pendingPosition = position;
            _requestedItem = pipeline.Item;
            _volume = volume;
            _playbackRate = rate;
            requestVersion = ++_requestVersion;
            _pipeline = null;
            if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
            pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
            _preparing = true;
        }

        DiagnosticLog.Warning(
            "playback-recovery",
            $"Uszkodzony tor wznowienia zostanie otwarty ponownie: {pipeline.Item.Title}; "
            + exception.Message);
        QueuePipelineDisposal(pipeline);
        PlaybackPreparing?.Invoke(
            this,
            new MediaPlaybackPreparingEventArgs(
                pipeline.Item,
                pipeline.MayRequireRemoteAccess));
        _ = Task.Run(() => PrepareAndStartPipeline(
            pipeline.Item,
            requestVersion,
            position,
            volume,
            rate,
            mp3DecoderMode: Mp3DecoderMode.Automatic));
        _ = WatchPreparationTimeoutAsync(
            pipeline.Item,
            requestVersion,
            pipeline.MayRequireRemoteAccess
                ? RemotePreparationTimeout
                : LocalPreparationTimeout,
            pipeline.MayRequireRemoteAccess);
        return true;
    }

    private PlaybackPipeline? DetachPipelineLocked()
    {
        var pipeline = _pipeline;
        _pipeline = null;
        if (ReferenceEquals(_seekWorker?.Pipeline, pipeline)) _seekWorker = null;
        if (pipeline is not null) pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
        return pipeline;
    }

    private TimeSpan? GetRemoteRetryDelayLocked(string path)
    {
        if (!_remoteFailures.TryGetValue(path, out var state)) return null;
        var remaining = state.RetryAfter - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    private void RecordRemoteFailureLocked(string path)
    {
        var now = DateTimeOffset.UtcNow;
        var previousCount = _remoteFailures.TryGetValue(path, out var previous)
            && now - previous.LastFailure < TimeSpan.FromMinutes(30)
                ? previous.Count
                : 0;
        var count = Math.Min(previousCount + 1, 6);
        var delaySeconds = Math.Min(300, 10 * Math.Pow(3, count - 1));
        _remoteFailures[path] = new RemoteFailureState(
            count,
            now,
            now.AddSeconds(delaySeconds));
    }

    private static void QueuePipelineDisposal(
        PlaybackPipeline pipeline,
        TimeSpan delay = default) =>
        _ = Task.Run(async () =>
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay).ConfigureAwait(false);
            DisposePipeline(pipeline);
        });

    private static void DisposePipeline(PlaybackPipeline pipeline)
    {
        try
        {
            pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
            pipeline.Output.Stop();
            pipeline.OutputLease.Dispose();
            // SoundTouchWaveStream owns and disposes the underlying reader.
            pipeline.TempoStream.Dispose();
        }
        catch (Exception exception)
        {
            DiagnosticLog.Warning("playback", $"Zamykanie poprzedniego strumienia nie powiodło się: {exception.Message}");
        }
    }

    private void RaisePlaybackFailed(MediaItem? item, string message) =>
        RaiseOnCapturedContext(
            () => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));

    private void RaiseOnCapturedContext(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }
        _synchronizationContext.Post(_ => action(), null);
    }

    private static string FriendlyPlaybackError(Exception exception)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return "Plik nie jest obecnie dostępny.";
        }
        if (exception is UnauthorizedAccessException)
        {
            return "Brak dostępu do pliku.";
        }
        if (exception is IOException)
        {
            return "Nie udało się pobrać lub odczytać pliku. Sprawdź połączenie i stan usługi chmurowej.";
        }
        if (exception is TimeoutException)
        {
            return "Dekoder nie odpowiedział w bezpiecznym czasie. Odtwarzanie zostało zatrzymane.";
        }
        if (exception is InvalidDataException
            or NotSupportedException
            or ArgumentException
            or InvalidCastException
            or OverflowException
            or OutOfMemoryException
            or System.Runtime.InteropServices.COMException)
        {
            return "Plik ma nieobsługiwany albo uszkodzony format dźwięku.";
        }
        return "Nie udało się uruchomić odtwarzania tego pliku.";
    }

    public void Dispose()
    {
        PlaybackPipeline? pipeline;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_requestVersion;
            _preparing = false;
            pipeline = DetachPipelineLocked();
        }
        if (pipeline is not null) QueuePipelineDisposal(pipeline);
    }
}
