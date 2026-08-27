using System.Diagnostics;
using System.IO;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
    private sealed class PlaybackPipeline
    {
        public required MediaItem Item { get; init; }
        public required WasapiOut Output { get; init; }
        public required GuardedWaveStream DecoderGuard { get; init; }
        public required DecoderReadMonitorSampleProvider OutputReadMonitor { get; init; }
        public required SoundTouchWaveStream TempoStream { get; init; }
        public required VolumeSampleProvider VolumeProvider { get; init; }
        public required EventHandler<StoppedEventArgs> StoppedHandler { get; init; }
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
    private static readonly TimeSpan DecoderStallTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan SeekStallTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SlowSeekLogThreshold = TimeSpan.FromSeconds(1);
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
        PlaybackPipeline? reusable;
        bool sourceQuarantined;
        lock (_gate)
        {
            sourceQuarantined = _quarantinedSources.Contains(item.Source);
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

        var requiresHydration = CloudFileAvailability.MayRequireRemoteAccess(item.Source);
        DiagnosticLog.Info(
            "playback",
            $"Żądanie otwarcia: {item.Title}; chmura: {requiresHydration}; źródło: {item.Source}.");
        PlaybackPreparing?.Invoke(
            this,
            new MediaPlaybackPreparingEventArgs(item, requiresHydration));

        _ = Task.Run(() => PrepareAndStartPipeline(
            item,
            requestVersion,
            resolvedPosition,
            resolvedVolume,
            resolvedRate));
        _ = WatchPreparationTimeoutAsync(
            item,
            requestVersion,
            requiresHydration ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(30));
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
        double requestedRate)
    {
        PlaybackPipeline? pipeline = null;
        try
        {
            lock (_gate)
            {
                if (_disposed || requestVersion != _requestVersion) return;
            }
            pipeline = CreatePipeline(item, requestVersion);
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

    private PlaybackPipeline CreatePipeline(MediaItem item, long requestVersion)
    {
        DiagnosticLog.Info("playback", $"Otwieranie dekodera: {item.Title}; żądanie {requestVersion}.");
        var reader = CreateReader(item.Source!);
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
                StoppedHandler = handler
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

    private static GuardedWaveStream CreateReader(string path)
    {
        var extension = Path.GetExtension(path);
        WaveStream reader;
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
        }
        else
        {
            reader = new AudioFileReader(path);
        }

        try
        {
            return new GuardedWaveStream(reader, path);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

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
            using var reader = CreateReader(path);
            duration = reader.TotalTime;
            sampleRateHz = reader.WaveFormat.SampleRate;
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
                    if (stopwatch.Elapsed >= SeekStallTimeout)
                    {
                        StopUnresponsivePipeline(
                            pipeline,
                            $"Przewijanie nie zakończyło się przez {SeekStallTimeout}.");
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
                    && Stopwatch.GetElapsedTime(seekStarted) >= SeekStallTimeout)
                {
                    StopUnresponsivePipeline(
                        pipeline,
                        $"Przewijanie nie zakończyło się przez {SeekStallTimeout}.");
                    return;
                }
                continue;
            }
            if (!pipeline.DecoderGuard.IsReadStalled(DecoderStallTimeout)
                && !pipeline.OutputReadMonitor.IsReadStalled(DecoderStallTimeout)) continue;

            StopUnresponsivePipeline(
                pipeline,
                $"Dekoder nie zwrócił danych przez {DecoderStallTimeout}.");
            return;
        }
    }

    private void StopUnresponsivePipeline(PlaybackPipeline pipeline, string diagnosticReason)
    {
        var detached = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pendingPosition = pipeline.DecoderGuard.CachedCurrentTime;
                _quarantinedSources.Add(pipeline.DecoderGuard.SourcePath);
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
            DiagnosticLog.Error("playback", $"Błąd urządzenia audio: {pipeline.Item.Title}.", args.Exception);
            RaisePlaybackFailed(pipeline.Item, FriendlyPlaybackError(args.Exception));
            return;
        }

        lock (_gate) _pendingPosition = TimeSpan.Zero;
        DiagnosticLog.Info("playback", $"Koniec pliku: {pipeline.Item.Title}.");
        RaiseOnCapturedContext(() =>
            PlaybackEnded?.Invoke(this, new MediaPlaybackEndedEventArgs(pipeline.Item)));
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
        return exception.Message;
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
