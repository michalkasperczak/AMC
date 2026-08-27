using System.Diagnostics;
using System.IO;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using NAudio.CoreAudioApi;
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
public sealed class WindowsMediaOutput : IMediaOutput, IDisposable
{
    private enum DecoderKind
    {
        System,
        ManagedMp3,
        Vorbis
    }

    private readonly record struct ReaderSelection(
        GuardedWaveStream Reader,
        DecoderKind DecoderKind);

    private sealed class PlaybackPipeline
    {
        public required MediaItem Item { get; init; }
        public required WasapiOut Output { get; init; }
        public required GuardedWaveStream DecoderGuard { get; init; }
        public required DecoderReadMonitorSampleProvider OutputReadMonitor { get; init; }
        public required SoundTouchWaveStream TempoStream { get; init; }
        public required VolumeSampleProvider VolumeProvider { get; init; }
        public required EventHandler<StoppedEventArgs> StoppedHandler { get; init; }
        public required bool MayRequireRemoteAccess { get; init; }
        public required DecoderKind DecoderKind { get; init; }
        public long SeekStartedTimestamp;
        public int SeekInProgress;
    }

    private sealed class SeekWorkerState(PlaybackPipeline pipeline)
    {
        public PlaybackPipeline Pipeline { get; } = pipeline;
    }

    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private readonly object _gate = new();
    private readonly HashSet<string> _quarantinedSources = new(StringComparer.OrdinalIgnoreCase);
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
    private const long ManagedMp3FallbackMaximumBytes = 512L * 1024 * 1024;
    private PlaybackPipeline? _pipeline;
    private SeekWorkerState? _seekWorker;
    private MediaItem? _requestedItem;
    private TimeSpan _pendingPosition;
    private double _playbackRate = 1d;
    private int _volume = 35;
    private long _requestVersion;
    private long _seekRequestVersion;
    private bool _preparing;
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

        var resolvedPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        var resolvedRate = Math.Clamp(playbackRate, 0.50d, 2.00d);
        var resolvedVolume = Math.Clamp(volume, 0, 100);
        var mayRequireRemoteAccess = CloudFileAvailability.MayRequireRemoteAccess(item.Source);
        PlaybackPipeline? reusable;
        bool sourceQuarantined;
        lock (_gate)
        {
            sourceQuarantined = !mayRequireRemoteAccess
                && _quarantinedSources.Contains(item.Source);
            reusable = _pipeline is not null
                && string.Equals(_pipeline.Item.Source, item.Source, StringComparison.OrdinalIgnoreCase)
                ? _pipeline
                : null;
            _requestedItem = item;
            _pendingPosition = resolvedPosition;
            _playbackRate = resolvedRate;
            _volume = resolvedVolume;
        }

        if (sourceQuarantined)
        {
            DiagnosticLog.Warning("decoder-watchdog", $"Zablokowano ponowne otwarcie pliku po zatrzymaniu dekodera: {item.Source}.");
            RaisePlaybackFailed(
                item,
                "Ten plik wcześniej zatrzymał dekoder. Uruchom program ponownie po zastąpieniu lub naprawieniu pliku.");
            return;
        }

        if (reusable is not null)
        {
            try
            {
                SeekPipeline(reusable, resolvedPosition);
                reusable.VolumeProvider.Volume = resolvedVolume / 100f;
                ApplyRate(reusable, resolvedRate);
                reusable.Output.Play();
                return;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Error("playback", $"Nie udało się wznowić: {item.Title}.", exception);
                RaisePlaybackFailed(item, exception.Message);
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
        if (previous is not null) QueuePipelineDisposal(previous);

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
            forceManagedMp3: false));
        _ = WatchPreparationTimeoutAsync(
            item,
            requestVersion,
            mayRequireRemoteAccess ? RemotePreparationTimeout : LocalPreparationTimeout);
    }

    private async Task WatchPreparationTimeoutAsync(
        MediaItem item,
        long requestVersion,
        TimeSpan timeout)
    {
        await Task.Delay(timeout).ConfigureAwait(false);
        var timedOut = false;
        lock (_gate)
        {
            if (!_disposed && _preparing && requestVersion == _requestVersion)
            {
                ++_requestVersion;
                _preparing = false;
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

    private void PrepareAndStartPipeline(
        MediaItem item,
        long requestVersion,
        TimeSpan requestedPosition,
        int requestedVolume,
        double requestedRate,
        bool forceManagedMp3)
    {
        PlaybackPipeline? pipeline = null;
        try
        {
            lock (_gate)
            {
                if (_disposed || requestVersion != _requestVersion) return;
            }
            pipeline = CreatePipeline(item, requestVersion, forceManagedMp3);
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
            }

            SeekPipeline(pipeline, position);
            pipeline.VolumeProvider.Volume = Math.Clamp(volume, 0, 100) / 100f;
            ApplyRate(pipeline, rate);
            pipeline.Output.Play();
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
            lock (_gate)
            {
                if (!_disposed && requestVersion == _requestVersion)
                {
                    _preparing = false;
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
        bool forceManagedMp3)
    {
        DiagnosticLog.Info("playback", $"Otwieranie dekodera: {item.Title}; żądanie {requestVersion}.");
        var mayRequireRemoteAccess = CloudFileAvailability.MayRequireRemoteAccess(item.Source!);
        var selection = CreateReader(
            item.Source!,
            forceManagedMp3,
            allowManagedMp3Fallback: true,
            mayRequireRemoteAccess);
        var reader = selection.Reader;
        SoundTouchWaveStream? tempoStream = null;
        WasapiOut? output = null;
        try
        {
            tempoStream = new SoundTouchWaveStream(reader)
            {
                Tempo = 1d,
                Pitch = 1d,
                Rate = 1d
            };
            var volumeProvider = new VolumeSampleProvider(tempoStream.ToSampleProvider());
            var outputReadMonitor = new DecoderReadMonitorSampleProvider(volumeProvider);
            output = new WasapiOut(AudioClientShareMode.Shared, true, 120);
            PlaybackPipeline? pipeline = null;
            EventHandler<StoppedEventArgs> handler = (_, args) =>
            {
                if (pipeline is not null) OutputDevicePlaybackStopped(pipeline, args);
            };
            pipeline = new PlaybackPipeline
            {
                Item = item,
                Output = output,
                DecoderGuard = reader,
                OutputReadMonitor = outputReadMonitor,
                TempoStream = tempoStream,
                VolumeProvider = volumeProvider,
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
            output?.Dispose();
            if (tempoStream is not null) tempoStream.Dispose();
            else reader.Dispose();
            throw;
        }
    }

    private static ReaderSelection CreateReader(
        string path,
        bool forceManagedMp3,
        bool allowManagedMp3Fallback,
        bool mayRequireRemoteAccess)
    {
        var extension = Path.GetExtension(path);
        WaveStream reader;
        var decoderKind = DecoderKind.System;
        if (extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".oga", StringComparison.OrdinalIgnoreCase))
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
            if (!mayRequireRemoteAccess)
            {
                probe = Mp3StructureProbe.Probe(path);
                if (!string.IsNullOrWhiteSpace(probe.Value.Warning))
                {
                    DiagnosticLog.Warning(
                        "mp3-probe",
                        $"Nietypowa struktura MP3: {path}; {probe.Value.Warning}");
                }
            }

            if (forceManagedMp3)
            {
                if (!CanUseManagedMp3Fallback(path, mayRequireRemoteAccess, probe))
                {
                    throw new InvalidDataException(
                        "Nie można bezpiecznie użyć awaryjnego dekodera dla tego pliku MP3.");
                }
                reader = CreateManagedMp3Reader(path);
                decoderKind = DecoderKind.ManagedMp3;
            }
            else
            {
                try
                {
                    reader = new AudioFileReader(path);
                }
                catch (Exception exception) when (
                    allowManagedMp3Fallback
                    && IsDecoderFailure(exception)
                    && CanUseManagedMp3Fallback(path, mayRequireRemoteAccess, probe))
                {
                    DiagnosticLog.Warning(
                        "mp3-fallback",
                        $"Dekoder systemowy odrzucił MP3; użyto dekodera zarządzanego: {path}; {exception.Message}");
                    reader = CreateManagedMp3Reader(path);
                    decoderKind = DecoderKind.ManagedMp3;
                }
            }
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

    private static WaveStream CreateManagedMp3Reader(string path)
    {
        var builder = new Mp3FileReader.FrameDecompressorBuilder(
            waveFormat => new Mp3FrameDecompressor(waveFormat));
        return new Mp3FileReaderBase(path, builder);
    }

    private static bool CanUseManagedMp3Fallback(
        string path,
        bool mayRequireRemoteAccess,
        Mp3StructureProbeResult? probe)
    {
        if (mayRequireRemoteAccess || probe is not { HasConsecutiveFrames: true }) return false;
        try
        {
            var length = new FileInfo(path).Length;
            return length is > 0 and <= ManagedMp3FallbackMaximumBytes;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsDecoderFailure(Exception exception) =>
        exception is IOException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException
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
        if (CloudFileAvailability.MayRequireRemoteAccess(path)) return false;
        try
        {
            using var selection = CreateReader(
                path,
                forceManagedMp3: false,
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
                        or System.Runtime.InteropServices.COMException)
                {
                    DiagnosticLog.Error(
                        "playback",
                        $"Przewijanie nie powiodło się: {pipeline.Item.Title}; cel {target}.",
                        exception);
                    if (TryBeginManagedMp3Recovery(pipeline, exception)) return;
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
                && !pipeline.OutputReadMonitor.IsReadStalled(decoderStallTimeout)) continue;

            StopUnresponsivePipeline(
                pipeline,
                $"Dekoder nie zwrócił danych przez {decoderStallTimeout}.");
            return;
        }
    }

    private void StopUnresponsivePipeline(PlaybackPipeline pipeline, string diagnosticReason)
    {
        if (TryBeginManagedMp3Recovery(pipeline, originalException: null))
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
            if (TryBeginManagedMp3Recovery(pipeline, args.Exception)) return;
            DiagnosticLog.Error("playback", $"Błąd urządzenia audio: {pipeline.Item.Title}.", args.Exception);
            RaisePlaybackFailed(pipeline.Item, FriendlyPlaybackError(args.Exception));
            return;
        }

        lock (_gate) _pendingPosition = TimeSpan.Zero;
        DiagnosticLog.Info("playback", $"Koniec pliku: {pipeline.Item.Title}.");
        RaiseOnCapturedContext(() =>
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(pipeline.Item)));
    }

    private bool TryBeginManagedMp3Recovery(
        PlaybackPipeline pipeline,
        Exception? originalException)
    {
        var path = pipeline.Item.Source;
        if (pipeline.DecoderKind != DecoderKind.System
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
        if (!CanUseManagedMp3Fallback(path, mayRequireRemoteAccess: false, probe)) return false;

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
            originalException is null
                ? $"Dekoder systemowy zatrzymał postęp; ponowna próba dekoderem zarządzanym: {path}."
                : $"Dekoder systemowy przerwał odtwarzanie; ponowna próba dekoderem zarządzanym: {path}; {originalException.Message}");
        QueuePipelineDisposal(pipeline);
        _ = Task.Run(() => PrepareAndStartPipeline(
            pipeline.Item,
            requestVersion,
            position,
            volume,
            rate,
            forceManagedMp3: true));
        _ = WatchPreparationTimeoutAsync(
            pipeline.Item,
            requestVersion,
            LocalPreparationTimeout);
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

    private static void QueuePipelineDisposal(PlaybackPipeline pipeline) =>
        _ = Task.Run(() => DisposePipeline(pipeline));

    private static void DisposePipeline(PlaybackPipeline pipeline)
    {
        try
        {
            pipeline.Output.PlaybackStopped -= pipeline.StoppedHandler;
            pipeline.Output.Stop();
            pipeline.Output.Dispose();
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
