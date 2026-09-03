using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using System.Runtime.InteropServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AccessibleMediaController.Windows.Services;

public sealed class RadioNowPlayingChangedEventArgs(MediaItem item, string? streamTitle) : EventArgs
{
    public MediaItem Item { get; } = item;
    public string? StreamTitle { get; } = streamTitle;
}

internal sealed record RadioAudioSnapshot(byte[] Audio, WaveFormat Format, TimeSpan Duration);

/// <summary>
/// Live radio output with an in-memory decoded-audio ring. The decoder works
/// away from the WPF dispatcher, so a slow or broken station cannot freeze the
/// accessible interface. The same ring provides pause and time-shift without
/// creating unbounded temporary files.
/// </summary>
public sealed class RadioMediaOutput(int timeshiftMinutes, bool audible = true) : IMediaOutput, IDisposable
{
    private const int MaximumTimeshiftBytes = 256 * 1024 * 1024;
    private const int MaximumReconnectAttempts = 2;
    private static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan StableReceptionInterval = TimeSpan.FromSeconds(20);
    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private readonly object _gate = new();
    private readonly int _timeshiftMinutes = Math.Clamp(timeshiftMinutes, 1, 60);
    private RadioPipeline? _pipeline;
    private CancellationTokenSource? _preparationCancellation;
    private MediaItem? _requestedItem;
    private int _volume = 35;
    private string? _outputDeviceId;
    private long _requestVersion;
    private bool _preparing;
    private bool _pauseRequested;
    private bool _disposed;

    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;
    public event EventHandler<RadioNowPlayingChangedEventArgs>? NowPlayingChanged;
    public event EventHandler? RecordingFailed;

    public string? LoadedItemId
    {
        get
        {
            lock (_gate) return _pipeline?.Item.Id;
        }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.PlaybackPosition ?? TimeSpan.Zero;
        }
    }

    public bool SupportsPlaybackRate => false;

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

    public bool IsPreparing
    {
        get
        {
            lock (_gate) return _preparing;
        }
    }

    public bool IsRecording
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.IsRecording == true;
        }
    }

    public bool CanPauseRecording
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.CanPauseRecording == true;
        }
    }

    public bool IsRecordingPaused
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.IsRecordingPaused == true;
        }
    }

    public TimeSpan RecordingDuration
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.RecordingDuration ?? TimeSpan.Zero;
        }
    }

    public TimeSpan BufferedDuration
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.BufferedDuration ?? TimeSpan.Zero;
        }
    }

    public TimeSpan BehindLive
    {
        get
        {
            lock (_gate) return _pipeline?.Buffer.BehindLive ?? TimeSpan.Zero;
        }
    }

    internal bool TryGetRecentPlaybackAudio(TimeSpan duration, out RadioAudioSnapshot? snapshot)
    {
        lock (_gate)
        {
            if (_pipeline is null)
            {
                snapshot = null;
                return false;
            }
            return _pipeline.Buffer.TryGetRecentPlaybackAudio(duration, out snapshot);
        }
    }

    internal bool TryGetRecentCapturedAudio(TimeSpan duration, out RadioAudioSnapshot? snapshot)
    {
        lock (_gate)
        {
            if (_pipeline is null)
            {
                snapshot = null;
                return false;
            }
            return _pipeline.Buffer.TryGetRecentCapturedAudio(duration, out snapshot);
        }
    }

    internal static async Task<RadioAudioMetadata?> TryReadStreamMetadataAsync(
        string source,
        TimeSpan timeout)
    {
        if (!IsHttpStream(source)) return null;
        var httpMetadata = await RadioStreamMetadataProbe.TryReadAsync(source, timeout)
            .ConfigureAwait(false);
        if (RadioStreamResolver.IsHlsSource(source)
            || httpMetadata?.BitrateKbps is not null && httpMetadata.SampleRateHz is not null)
        {
            return httpMetadata;
        }
        using var cancellation = new CancellationTokenSource(timeout);
        BassRadioWaveProvider? reader = null;
        try
        {
            reader = await BassRadioWaveProvider.OpenAsync(source, cancellation.Token)
                .ConfigureAwait(false);
            var probe = new byte[16 * 1024];
            _ = reader.Read(probe, 0, probe.Length);
            var detectedBitrate = reader.BitrateKbps ?? httpMetadata?.BitrateKbps;
            return new RadioAudioMetadata(
                detectedBitrate,
                reader.WaveFormat.SampleRate,
                httpMetadata?.Codec,
                httpMetadata?.IsBitrateEstimated == true && reader.BitrateKbps is null);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException
            or OperationCanceledException)
        {
            return httpMetadata;
        }
        finally
        {
            reader?.Dispose();
        }
    }

    public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsHttpStream(item.Source))
        {
            RaisePlaybackFailed(item, "Stacja nie zawiera prawidłowego adresu HTTP lub HTTPS.");
            return;
        }

        RadioPipeline? reusable;
        lock (_gate)
        {
            _requestedItem = item;
            _volume = Math.Clamp(volume, 0, 100);
            _pauseRequested = false;
            reusable = _pipeline is not null
                && string.Equals(_pipeline.Item.Source, item.Source, StringComparison.OrdinalIgnoreCase)
                ? _pipeline
                : null;
        }
        if (reusable is not null)
        {
            reusable.Volume.Volume = _volume / 100f;
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
            PublishPipelineStreamTitle(reusable, reusable.StreamTitle);
            RaiseOnCapturedContext(() => PlaybackStarted?.Invoke(
                this,
                new MediaPlaybackStartedEventArgs(item)));
            return;
        }

        RadioPipeline? previous;
        CancellationTokenSource? previousPreparation;
        CancellationTokenSource preparationCancellation;
        long requestVersion;
        lock (_gate)
        {
            previous = DetachPipelineLocked();
            previousPreparation = _preparationCancellation;
            preparationCancellation = new CancellationTokenSource();
            _preparationCancellation = preparationCancellation;
            _preparing = true;
            requestVersion = ++_requestVersion;
        }
        CancelPreparation(previousPreparation);
        if (previous is not null) QueueDisposal(previous);
        RaiseOnCapturedContext(() => NowPlayingChanged?.Invoke(
            this,
            new RadioNowPlayingChangedEventArgs(item, null)));
        DiagnosticLog.Info("radio", $"Łączenie ze stacją: {item.Title}.");
        PlaybackPreparing?.Invoke(this, new MediaPlaybackPreparingEventArgs(item, false));
        _ = Task.Run(() => PrepareAndStartAsync(item, requestVersion, preparationCancellation));
        _ = WatchPreparationTimeoutAsync(item, requestVersion, preparationCancellation);
    }

    private async Task PrepareAndStartAsync(
        MediaItem item,
        long requestVersion,
        CancellationTokenSource preparationCancellation)
    {
        RadioPipeline? pipeline = null;
        OpenedRadioReader? openedReader = null;
        AudioOutputDeviceLease? preparedOutputLease = null;
        try
        {
            var cancellationToken = preparationCancellation.Token;
            openedReader = await OpenFirstWorkingReaderAsync(item.Source!, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var reader = openedReader.Reader;
            var buffer = new RadioTimeshiftWaveProvider(
                reader.WaveFormat,
                _timeshiftMinutes,
                MaximumTimeshiftBytes,
                RaiseRecordingFailed);
            // Do not announce a started station while the output still holds
            // only silence. The first decoded portion also proves that opening
            // the URL produced audio rather than headers followed by EOF.
            var initialAudio = new byte[Math.Max(
                16 * 1024,
                reader.WaveFormat.AverageBytesPerSecond / 10)];
            var initialRead = ReadInitialAudio(
                openedReader,
                initialAudio,
                cancellationToken);
            if (initialRead <= 0)
            {
                throw new EndOfStreamException("Serwer nie przesłał dźwięku po otwarciu strumienia.");
            }
            ApplyDetectedAudioMetadata(item, openedReader);
            buffer.Write(initialAudio, 0, initialRead);
            var volume = new VolumeSampleProvider(buffer.ToSampleProvider())
            {
                Volume = Math.Clamp(_volume, 0, 100) / 100f
            };
            if (audible)
            {
                string? outputDeviceId;
                lock (_gate) outputDeviceId = _outputDeviceId;
                preparedOutputLease = AudioOutputDeviceCatalog.CreateInitializedOutput(
                    outputDeviceId,
                    180,
                    output => output.Init(volume));
            }
            var cancellation = new CancellationTokenSource();
            var decoderName = openedReader.DecoderName;
            pipeline = new RadioPipeline(
                item,
                reader,
                openedReader.Lifetime,
                buffer,
                volume,
                preparedOutputLease,
                cancellation,
                reader as IRadioStreamTitleSource,
                Pipeline_StreamTitleChanged);
            pipeline.StartStreamTitleTracking();
            openedReader = null;
            preparedOutputLease = null;

            lock (_gate)
            {
                if (_disposed
                    || requestVersion != _requestVersion
                    || preparationCancellation.IsCancellationRequested)
                {
                    QueueDisposal(pipeline);
                    return;
                }
                _pipeline = pipeline;
                _preparing = false;
                if (ReferenceEquals(_preparationCancellation, preparationCancellation))
                {
                    _preparationCancellation = null;
                }
            }
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
            pipeline.CaptureTask = Task.Run(() => CaptureLoopAsync(pipeline), cancellation.Token);
            DiagnosticLog.Info(
                "radio",
                $"Rozpoczęto odbiór: {item.Title}; format {reader.WaveFormat}; dekoder {decoderName}.");
            RaiseOnCapturedContext(() => PlaybackStarted?.Invoke(
                this,
                new MediaPlaybackStartedEventArgs(item)));
            PublishPipelineStreamTitle(pipeline, pipeline.StreamTitle);
        }
        catch (OperationCanceledException)
        {
            preparedOutputLease?.Dispose();
            if (pipeline is not null) QueueDisposal(pipeline);
            lock (_gate)
            {
                if (!_disposed && requestVersion == _requestVersion)
                {
                    _preparing = false;
                }
                if (ReferenceEquals(_preparationCancellation, preparationCancellation))
                {
                    _preparationCancellation = null;
                }
            }
            DiagnosticLog.Info("radio", $"Anulowano nieaktualne łączenie ze stacją: {item.Title}.");
        }
        catch (Exception exception) when (exception is IOException
            or HttpRequestException
            or TaskCanceledException
            or InvalidOperationException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException
            or AuthenticationException
            or COMException
            or InvalidComObjectException)
        {
            preparedOutputLease?.Dispose();
            if (pipeline is not null) QueueDisposal(pipeline);
            var current = false;
            lock (_gate)
            {
                if (!_disposed && requestVersion == _requestVersion)
                {
                    _preparing = false;
                    current = true;
                }
                if (ReferenceEquals(_preparationCancellation, preparationCancellation))
                {
                    _preparationCancellation = null;
                }
            }
            DiagnosticLog.Warning("radio", $"Nie udało się otworzyć stacji {item.Title}; błąd {exception.GetType().Name}.");
            if (current) RaisePlaybackFailed(
                item,
                "Nie udało się odtworzyć tej stacji. Sprawdź adres strumienia lub spróbuj ponownie później.");
        }
        finally
        {
            try { openedReader?.Lifetime.Dispose(); } catch (Exception) { }
            preparationCancellation.Dispose();
        }
    }

    private static async Task<OpenedRadioReader> OpenReaderAsync(
        string source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RadioStreamResolver.IsHlsSource(source))
        {
            var ffmpeg = await FfmpegRadioWaveProvider.TryOpenAsync(source, cancellationToken)
                .ConfigureAwait(false);
            if (ffmpeg is not null)
            {
                return new OpenedRadioReader(ffmpeg, ffmpeg, "FFmpeg HLS", null, "AAC");
            }
        }
        var preferBass = ShouldPreferBass(source);
        if (preferBass)
        {
            var bass = await TryOpenBassReaderAsync(source, cancellationToken).ConfigureAwait(false);
            if (bass is not null) return bass;
        }
        try
        {
            var mediaFoundation = new MediaFoundationReader(source);
            if (cancellationToken.IsCancellationRequested)
            {
                mediaFoundation.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new OpenedRadioReader(mediaFoundation, mediaFoundation, "systemowy", null, null);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException
            or COMException
            or InvalidComObjectException)
        {
            DiagnosticLog.Info(
                "radio",
                $"Dekoder systemowy odrzucił strumień; rozpoznawanie starszego radia ICY ({exception.GetType().Name}).");

            if (RadioStreamResolver.IsHlsSource(source))
            {
                throw new NotSupportedException(
                    "Strumień HLS wymaga zgodnego wariantu albo dodatkowego komponentu dekodera.",
                    exception);
            }

            if (!preferBass)
            {
                var bass = await TryOpenBassReaderAsync(source, cancellationToken).ConfigureAwait(false);
                if (bass is not null) return bass;
            }

            var legacyAudio = await LegacyIcyAudioStream.OpenAsync(source, cancellationToken)
                .ConfigureAwait(false);
            if (legacyAudio.IsOgg)
            {
                var vorbis = new LiveVorbisWaveProvider(legacyAudio);
                return new OpenedRadioReader(
                    vorbis,
                    vorbis,
                    "zgodności ICY OGG/Vorbis",
                    vorbis.BitrateKbps,
                    "Vorbis");
            }
            if (legacyAudio.IsAac)
            {
                legacyAudio.Dispose();
                throw new NotSupportedException(
                    "Starszy strumień ICY AAC wymaga zgodnego wariantu albo dodatkowego komponentu dekodera.");
            }

            legacyAudio.Dispose();
            var legacyMp3 = await LegacyIcyMp3StreamReader.OpenAsync(source, cancellationToken)
                .ConfigureAwait(false);
            return new OpenedRadioReader(
                legacyMp3,
                legacyMp3,
                "zgodności ICY MP3",
                legacyMp3.BitrateKbps,
                "MP3");
        }
    }

    private static async Task<OpenedRadioReader?> TryOpenBassReaderAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (!BassRadioWaveProvider.IsAvailable || RadioStreamResolver.IsHlsSource(source)) return null;
        try
        {
            var bass = await BassRadioWaveProvider.OpenAsync(source, cancellationToken)
                .ConfigureAwait(false);
            return new OpenedRadioReader(bass, bass, "BASS", bass.BitrateKbps, null);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException)
        {
            DiagnosticLog.Info(
                "radio",
                $"Dekoder BASS odrzucił strumień; próba pozostałych dekoderów ({exception.GetType().Name}).");
            return null;
        }
    }

    internal static bool ShouldPreferBass(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || RadioStreamResolver.IsHlsSource(source))
        {
            return false;
        }

        return uri.AbsolutePath.Contains("/;", StringComparison.Ordinal)
            || uri.Host.Equals("stream3.polskieradio.pl", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("mp3.polskieradio.pl", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("stream.radioemaus.pl", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("emkielce.pl", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("194.181.177.253", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<OpenedRadioReader> OpenFirstWorkingReaderAsync(
        string source,
        CancellationToken cancellationToken)
    {
        Exception? lastFailure = null;
        var candidates = RadioStreamResolver.GetPlaybackCandidates(source);
        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var resolved = await RadioStreamResolver.ResolveAsync(candidates[index], cancellationToken)
                    .ConfigureAwait(false);
                var reader = await OpenReaderAsync(resolved, cancellationToken).ConfigureAwait(false);
                if (index > 0 || !string.Equals(candidates[index], source, StringComparison.OrdinalIgnoreCase))
                {
                    DiagnosticLog.Info("radio", "Użyto zgodnego wariantu strumienia stacji.");
                }
                return reader;
            }
            catch (Exception exception) when (exception is IOException
                or HttpRequestException
                or TaskCanceledException
                or InvalidOperationException
                or InvalidDataException
                or NotSupportedException
                or ArgumentException
                or AuthenticationException
                or COMException
                or InvalidComObjectException)
            {
                lastFailure = exception;
                if (index + 1 < candidates.Count)
                {
                    DiagnosticLog.Info(
                        "radio",
                        $"Wariant strumienia nie zadziałał; próba następnego ({exception.GetType().Name}).");
                }
            }
        }

        throw lastFailure ?? new InvalidDataException("Nie znaleziono obsługiwanego wariantu strumienia.");
    }

    private async Task CaptureLoopAsync(RadioPipeline pipeline)
    {
        var bytes = new byte[Math.Max(
            16 * 1024,
            pipeline.Buffer.WaveFormat.AverageBytesPerSecond / 4)];
        var reconnectAttempts = 0;
        var stableSince = Stopwatch.GetTimestamp();
        Exception? finalFailure = null;
        while (!pipeline.Cancellation.IsCancellationRequested)
        {
            try
            {
                var read = pipeline.Read(bytes, 0, bytes.Length);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Serwer zakończył strumień.");
                }
                pipeline.Buffer.Write(bytes, 0, read);
                if (Stopwatch.GetElapsedTime(stableSince) >= StableReceptionInterval)
                {
                    reconnectAttempts = 0;
                }
            }
            catch (Exception exception) when (pipeline.Cancellation.IsCancellationRequested
                && IsRecoverableRadioException(exception))
            {
                return;
            }
            catch (Exception exception) when (IsRecoverableRadioException(exception))
            {
                reconnectAttempts++;
                if (reconnectAttempts > MaximumReconnectAttempts)
                {
                    finalFailure = exception;
                    break;
                }

                DiagnosticLog.Warning(
                    "radio",
                    $"Odbiór stacji został przerwany: {pipeline.Item.Title}; "
                    + $"automatyczna próba ponownego połączenia {reconnectAttempts} z {MaximumReconnectAttempts}; "
                    + $"błąd {exception.GetType().Name}.");
                try
                {
                    await Task.Delay(ReconnectDelay, pipeline.Cancellation.Token).ConfigureAwait(false);
                    OpenedRadioReader? openedReader = null;
                    try
                    {
                        openedReader = await OpenFirstWorkingReaderAsync(
                                pipeline.Item.Source!,
                                pipeline.Cancellation.Token)
                            .ConfigureAwait(false);
                        if (!AreCompatibleRadioFormats(
                                pipeline.Buffer.WaveFormat,
                                openedReader.Reader.WaveFormat))
                        {
                            throw new InvalidDataException(
                                "Format stacji zmienił się podczas ponownego połączenia.");
                        }

                        var initialAudio = new byte[Math.Max(
                            16 * 1024,
                            openedReader.Reader.WaveFormat.AverageBytesPerSecond / 10)];
                        var initialRead = ReadInitialAudio(
                            openedReader,
                            initialAudio,
                            pipeline.Cancellation.Token);
                        if (initialRead <= 0)
                        {
                            throw new EndOfStreamException(
                                "Serwer nie przesłał dźwięku po ponownym połączeniu.");
                        }
                        ApplyDetectedAudioMetadata(pipeline.Item, openedReader);
                        if (!pipeline.TryReplaceReader(
                                openedReader.Reader,
                                openedReader.Lifetime,
                                openedReader.Reader as IRadioStreamTitleSource,
                                out var previousLifetime))
                        {
                            return;
                        }
                        var decoderName = openedReader.DecoderName;
                        openedReader = null;
                        try { previousLifetime?.Dispose(); } catch (Exception) { }
                        pipeline.Buffer.Write(initialAudio, 0, initialRead);
                        PublishPipelineStreamTitle(pipeline, pipeline.StreamTitle);
                        stableSince = Stopwatch.GetTimestamp();
                        DiagnosticLog.Info(
                            "radio",
                            $"Przywrócono odbiór: {pipeline.Item.Title}; dekoder {decoderName}.");
                    }
                    finally
                    {
                        openedReader?.Lifetime.Dispose();
                    }
                }
                catch (OperationCanceledException) when (pipeline.Cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception reconnectException) when (IsRecoverableRadioException(reconnectException))
                {
                    finalFailure = reconnectException;
                }
            }
        }

        if (pipeline.Cancellation.IsCancellationRequested) return;
        DiagnosticLog.Warning(
            "radio",
            $"Nie udało się przywrócić odbioru: {pipeline.Item.Title}; "
            + $"błąd {finalFailure?.GetType().Name ?? "nieznany"}.");
        var detached = false;
        lock (_gate)
        {
            if (!_disposed && ReferenceEquals(_pipeline, pipeline))
            {
                _pipeline = null;
                detached = true;
            }
        }
        if (detached)
        {
            QueueDisposal(pipeline);
            RaisePlaybackFailed(
                pipeline.Item,
                "Połączenie ze stacją zostało przerwane i nie udało się go automatycznie przywrócić.");
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    internal static bool AreCompatibleRadioFormats(WaveFormat expected, WaveFormat candidate) =>
        expected.SampleRate == candidate.SampleRate
        && expected.Channels == candidate.Channels
        && expected.BitsPerSample == candidate.BitsPerSample
        && expected.Encoding == candidate.Encoding
        && expected.BlockAlign == candidate.BlockAlign;

    private static int ReadInitialAudio(
        OpenedRadioReader openedReader,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var cancellationRegistration = cancellationToken.Register(
            static state =>
            {
                try { ((IDisposable)state!).Dispose(); }
                catch (Exception) { }
            },
            openedReader.Lifetime);
        try
        {
            var read = openedReader.Reader.Read(buffer, 0, buffer.Length);
            cancellationToken.ThrowIfCancellationRequested();
            return read;
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested
            && exception is ObjectDisposedException
                or IOException
                or InvalidOperationException
                or COMException
                or InvalidComObjectException)
        {
            throw new OperationCanceledException(
                "Anulowano przygotowywanie nieaktualnej stacji.",
                exception,
                cancellationToken);
        }
    }

    private static bool IsRecoverableRadioException(Exception exception) =>
        exception is ObjectDisposedException
            or IOException
            or EndOfStreamException
            or HttpRequestException
            or TaskCanceledException
            or InvalidDataException
            or InvalidOperationException
            or NotSupportedException
            or AuthenticationException
            or COMException
            or InvalidComObjectException;

    private static void ApplyDetectedAudioMetadata(MediaItem item, OpenedRadioReader reader)
    {
        var detectedBitrate = reader.Reader is BassRadioWaveProvider bass
            ? bass.BitrateKbps ?? reader.BitrateKbps
            : reader.BitrateKbps;
        detectedBitrate = RadioAudioMetadataRules.NormalizeBitrateKbps(detectedBitrate);
        if (item.BitrateKbps is null && detectedBitrate is not null)
        {
            item.BitrateKbps = detectedBitrate;
            item.IsBitrateEstimated = false;
        }
        if (reader.Reader.WaveFormat.SampleRate > 0)
        {
            item.SampleRateHz = reader.Reader.WaveFormat.SampleRate;
        }
        if (string.IsNullOrWhiteSpace(item.Codec) && !string.IsNullOrWhiteSpace(reader.Codec))
        {
            item.Codec = reader.Codec;
        }
    }

    private async Task WatchPreparationTimeoutAsync(
        MediaItem item,
        long requestVersion,
        CancellationTokenSource preparationCancellation)
    {
        await Task.Delay(PreparationTimeout).ConfigureAwait(false);
        var timedOut = false;
        lock (_gate)
        {
            if (!_disposed && _preparing && requestVersion == _requestVersion)
            {
                ++_requestVersion;
                _preparing = false;
                if (ReferenceEquals(_preparationCancellation, preparationCancellation))
                {
                    _preparationCancellation = null;
                }
                timedOut = true;
            }
        }
        if (!timedOut) return;
        CancelPreparation(preparationCancellation);
        DiagnosticLog.Warning("radio", $"Przekroczono czas łączenia: {item.Title}.");
        RaisePlaybackFailed(item, "Stacja nie odpowiedziała w bezpiecznym czasie.");
    }

    public void Pause()
    {
        WasapiOut? output;
        lock (_gate)
        {
            _pauseRequested = true;
            output = _pipeline?.Output;
        }
        try
        {
            output?.Pause();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent station or output-device change already detached it.
        }
    }

    public void Stop()
    {
        RadioPipeline? pipeline;
        MediaItem? stoppedItem;
        CancellationTokenSource? preparationCancellation;
        lock (_gate)
        {
            ++_requestVersion;
            _preparing = false;
            _pauseRequested = true;
            preparationCancellation = _preparationCancellation;
            _preparationCancellation = null;
            pipeline = DetachPipelineLocked();
            stoppedItem = pipeline?.Item ?? _requestedItem;
            _requestedItem = null;
        }
        CancelPreparation(preparationCancellation);
        if (pipeline is not null) QueueDisposal(pipeline);
        if (stoppedItem is not null)
        {
            RaiseOnCapturedContext(() => NowPlayingChanged?.Invoke(
                this,
                new RadioNowPlayingChangedEventArgs(stoppedItem, null)));
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_gate) _pipeline?.Buffer.Seek(position);
    }

    public void JumpToLive()
    {
        lock (_gate) _pipeline?.Buffer.JumpToLive();
    }

    public void SetVolume(int volume)
    {
        lock (_gate)
        {
            _volume = Math.Clamp(volume, 0, 100);
            if (_pipeline is not null) _pipeline.Volume.Volume = _volume / 100f;
        }
    }

    public void SetPlaybackRate(double playbackRate)
    {
        // Live radio deliberately stays at its original speed. Time-shift is
        // implemented by moving inside the buffer, not by stretching speech.
    }

    public string StartRecording(
        string folder,
        RadioRecordingFormat format = RadioRecordingFormat.Mp3,
        int bitRateKbps = RadioMp3Recorder.DesiredBitRate / 1000,
        string? preferredBaseName = null)
    {
        lock (_gate)
        {
            if (_pipeline is null) throw new InvalidOperationException("Najpierw uruchom stację.");
            if (_pipeline.Buffer.IsRecording) throw new InvalidOperationException("Nagrywanie już trwa.");
            Directory.CreateDirectory(folder);
            var safeName = string.Concat(_pipeline.Item.Title.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
            if (safeName.Length == 0) safeName = "Radio";
            DeleteStalePartialRecordings(folder);
            var recordingSource = _pipeline.Item.Source ?? string.Empty;
            if (format == RadioRecordingFormat.Original)
            {
                // Playback resolves ordinary PLS/M3U/XSPF wrappers before it
                // opens a decoder. The packet-copy recorder must receive that
                // same direct stream rather than asking FFmpeg to interpret a
                // text playlist as audio. Genuine HLS manifests remain intact.
                using var resolutionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                recordingSource = RadioStreamResolver.ResolveAsync(
                        recordingSource,
                        resolutionCancellation.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            var originalTarget = format == RadioRecordingFormat.Original
                ? RadioOriginalStreamRecorder.Describe(
                    recordingSource,
                    _pipeline.Item.Codec)
                : null;
            var extension = originalTarget?.Extension
                ?? RadioMp3Recorder.RecordingExtension(format);
            var baseName = string.IsNullOrWhiteSpace(preferredBaseName)
                ? $"{safeName} - {DateTime.Now:yyyy-MM-dd HH-mm-ss}"
                : RadioRecordingFileNameTemplate.SanitizeBaseName(preferredBaseName);
            var path = UniqueRecordingPath(
                folder,
                baseName,
                extension);
            _pipeline.Buffer.StartRecording(
                path,
                format,
                bitRateKbps,
                recordingSource,
                originalTarget);
            DiagnosticLog.Info("radio-recording", $"Rozpoczęto nagrywanie: {path}.");
            return path;
        }
    }

    public string? StopRecording()
    {
        lock (_gate)
        {
            var path = _pipeline?.Buffer.StopRecording();
            if (path is not null) DiagnosticLog.Info("radio-recording", $"Zakończono nagrywanie: {path}.");
            return path;
        }
    }

    public void PauseRecording()
    {
        lock (_gate)
        {
            if (_pipeline?.Buffer.IsRecording != true)
                throw new InvalidOperationException("Nagrywanie jeszcze się nie rozpoczęło.");
            _pipeline.Buffer.PauseRecording();
        }
    }

    public void ResumeRecording()
    {
        lock (_gate)
        {
            if (_pipeline?.Buffer.IsRecording != true)
                throw new InvalidOperationException("Nagrywanie jeszcze się nie rozpoczęło.");
            _pipeline.Buffer.ResumeRecording();
        }
    }

    private RadioPipeline? DetachPipelineLocked()
    {
        var pipeline = _pipeline;
        _pipeline = null;
        return pipeline;
    }

    private static bool IsHttpStream(string? source)
    {
        if (MediaSourceAccessPolicy.Classify(source).Kind
            != MediaSourceAccessKind.NetworkStream)
        {
            return false;
        }

        return Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }

    private static string UniqueRecordingPath(string folder, string baseName, string extension)
    {
        var path = Path.Combine(folder, baseName + extension);
        for (var suffix = 2; File.Exists(path) || File.Exists(path + ".amc-partial"); suffix++)
        {
            path = Path.Combine(folder, $"{baseName} ({suffix}){extension}");
        }
        return path;
    }

    private static void DeleteStalePartialRecordings(string folder)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.amc-partial"))
            {
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-1)) File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning(
                "radio-recording",
                $"Nie można uprzątnąć starego pliku tymczasowego; błąd {exception.GetType().Name}.");
        }
    }

    private static void QueueDisposal(RadioPipeline pipeline) =>
        _ = Task.Run(() => pipeline.Dispose());

    private static void CancelPreparation(CancellationTokenSource? cancellation)
    {
        if (cancellation is null) return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void RaisePlaybackFailed(MediaItem item, string message) =>
        RaiseOnCapturedContext(() =>
        {
            lock (_gate)
            {
                if (_disposed
                    || !string.Equals(_requestedItem?.Id, item.Id, StringComparison.Ordinal))
                {
                    return;
                }
            }
            NowPlayingChanged?.Invoke(this, new RadioNowPlayingChangedEventArgs(item, null));
            PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message));
        });

    private void Pipeline_StreamTitleChanged(RadioPipeline pipeline, string? streamTitle) =>
        PublishPipelineStreamTitle(pipeline, streamTitle);

    private void PublishPipelineStreamTitle(RadioPipeline pipeline, string? streamTitle)
    {
        RaiseOnCapturedContext(() =>
        {
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(_pipeline, pipeline)) return;
            }
            NowPlayingChanged?.Invoke(
                this,
                new RadioNowPlayingChangedEventArgs(pipeline.Item, streamTitle));
        });
    }

    private void RaiseRecordingFailed(Exception exception)
    {
        DiagnosticLog.Error(
            "radio-recording",
            "Nagrywanie radia zostało przerwane, ale odtwarzanie jest kontynuowane.",
            exception);
        RaiseOnCapturedContext(() => RecordingFailed?.Invoke(this, EventArgs.Empty));
    }

    private void RaiseOnCapturedContext(Action action)
    {
        if (_synchronizationContext is null || SynchronizationContext.Current == _synchronizationContext)
        {
            action();
            return;
        }
        _synchronizationContext.Post(_ => action(), null);
    }

    public void Dispose()
    {
        RadioPipeline? pipeline;
        CancellationTokenSource? preparationCancellation;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_requestVersion;
            _preparing = false;
            _pauseRequested = true;
            preparationCancellation = _preparationCancellation;
            _preparationCancellation = null;
            pipeline = DetachPipelineLocked();
        }
        CancelPreparation(preparationCancellation);
        pipeline?.Dispose();
    }

    private sealed class RadioPipeline(
        MediaItem item,
        IWaveProvider reader,
        IDisposable readerLifetime,
        RadioTimeshiftWaveProvider buffer,
        VolumeSampleProvider volume,
        AudioOutputDeviceLease? outputLease,
        CancellationTokenSource cancellation,
        IRadioStreamTitleSource? streamTitleSource,
        Action<RadioPipeline, string?> streamTitleChanged) : IDisposable
    {
        private readonly object _readerGate = new();
        private IWaveProvider _reader = reader;
        private IDisposable? _readerLifetime = readerLifetime;
        private IRadioStreamTitleSource? _streamTitleSource = streamTitleSource;
        private readonly Action<RadioPipeline, string?> _streamTitleChanged = streamTitleChanged;
        private int _disposed;
        public string? StreamTitle
        {
            get
            {
                lock (_readerGate) return _streamTitleSource?.StreamTitle;
            }
        }
        public MediaItem Item { get; } = item;
        public RadioTimeshiftWaveProvider Buffer { get; } = buffer;
        public VolumeSampleProvider Volume { get; } = volume;
        public WasapiOut? Output => outputLease?.Output;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task? CaptureTask { get; set; }

        public void StartStreamTitleTracking()
        {
            if (_streamTitleSource is not null)
            {
                _streamTitleSource.StreamTitleChanged += StreamTitleSource_StreamTitleChanged;
            }
        }

        public int Read(byte[] target, int offset, int count)
        {
            var current = Volatile.Read(ref _reader);
            return current.Read(target, offset, count);
        }

        public bool TryReplaceReader(
            IWaveProvider replacement,
            IDisposable replacementLifetime,
            IRadioStreamTitleSource? replacementStreamTitleSource,
            out IDisposable? previousLifetime)
        {
            lock (_readerGate)
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    previousLifetime = null;
                    return false;
                }
                previousLifetime = _readerLifetime;
                if (_streamTitleSource is not null)
                {
                    _streamTitleSource.StreamTitleChanged -= StreamTitleSource_StreamTitleChanged;
                }
                Volatile.Write(ref _reader, replacement);
                _readerLifetime = replacementLifetime;
                _streamTitleSource = replacementStreamTitleSource;
                if (_streamTitleSource is not null)
                {
                    _streamTitleSource.StreamTitleChanged += StreamTitleSource_StreamTitleChanged;
                }
                return true;
            }
        }

        private void StreamTitleSource_StreamTitleChanged(
            object? sender,
            RadioStreamTitleChangedEventArgs e) =>
            _streamTitleChanged(this, e.StreamTitle);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Cancellation.Cancel();
            IDisposable? currentLifetime;
            lock (_readerGate)
            {
                if (_streamTitleSource is not null)
                {
                    _streamTitleSource.StreamTitleChanged -= StreamTitleSource_StreamTitleChanged;
                    _streamTitleSource = null;
                }
                currentLifetime = _readerLifetime;
                _readerLifetime = null;
            }
            try { Buffer.StopRecording(); }
            catch (Exception exception)
            {
                DiagnosticLog.Error(
                    "radio-recording",
                    "Nie udało się zakończyć nagrania podczas zamykania stacji.",
                    exception);
            }
            try { Output?.Stop(); } catch (Exception) { }
            try { currentLifetime?.Dispose(); } catch (Exception) { }
            try { outputLease?.Dispose(); } catch (Exception) { }
            Cancellation.Dispose();
        }
    }

    private sealed record OpenedRadioReader(
        IWaveProvider Reader,
        IDisposable Lifetime,
        string DecoderName,
        int? BitrateKbps,
        string? Codec);

    private sealed class RadioTimeshiftWaveProvider : IWaveProvider
    {
        private readonly object _gate = new();
        private readonly byte[] _ring;
        private readonly int _blockAlign;
        private readonly Action<Exception> _recordingFailed;
        private long _totalWritten;
        private long _readPosition;
        private IRadioRecorder? _recording;
        private string? _recordingPath;

        public RadioTimeshiftWaveProvider(
            WaveFormat waveFormat,
            int minutes,
            int maximumBytes,
            Action<Exception> recordingFailed)
        {
            WaveFormat = waveFormat;
            _recordingFailed = recordingFailed;
            _blockAlign = Math.Max(1, waveFormat.BlockAlign);
            var requested = (long)waveFormat.AverageBytesPerSecond * Math.Max(1, minutes) * 60;
            var capacity = (int)Math.Min(maximumBytes, Math.Max(waveFormat.AverageBytesPerSecond * 5L, requested));
            capacity -= capacity % _blockAlign;
            _ring = new byte[Math.Max(_blockAlign, capacity)];
        }

        public WaveFormat WaveFormat { get; }

        public bool IsRecording
        {
            get
            {
                lock (_gate) return _recording is not null;
            }
        }

        public bool CanPauseRecording
        {
            get
            {
                lock (_gate) return _recording?.CanPause == true;
            }
        }

        public bool IsRecordingPaused
        {
            get
            {
                lock (_gate) return _recording?.IsPaused == true;
            }
        }

        public TimeSpan RecordingDuration
        {
            get
            {
                lock (_gate) return _recording?.RecordedDuration ?? TimeSpan.Zero;
            }
        }

        public TimeSpan PlaybackPosition
        {
            get
            {
                lock (_gate) return BytesToTime(_readPosition);
            }
        }

        public TimeSpan BufferedDuration
        {
            get
            {
                lock (_gate) return BytesToTime(Math.Min(_totalWritten, _ring.LongLength));
            }
        }

        public TimeSpan BehindLive
        {
            get
            {
                lock (_gate) return BytesToTime(Math.Max(0, _totalWritten - _readPosition));
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            count -= count % _blockAlign;
            if (count <= 0) return;
            if (count > _ring.Length)
            {
                offset += count - _ring.Length;
                count = _ring.Length;
                count -= count % _blockAlign;
            }

            IRadioRecorder? failedRecording = null;
            Exception? recordingException = null;
            lock (_gate)
            {
                try
                {
                    _recording?.Write(buffer, offset, count);
                }
                catch (Exception exception) when (exception is IOException
                    or InvalidOperationException
                    or ObjectDisposedException)
                {
                    failedRecording = _recording;
                    recordingException = exception;
                    _recording = null;
                    _recordingPath = null;
                }
                var writeIndex = (int)(_totalWritten % _ring.Length);
                var first = Math.Min(count, _ring.Length - writeIndex);
                Buffer.BlockCopy(buffer, offset, _ring, writeIndex, first);
                if (first < count)
                {
                    Buffer.BlockCopy(buffer, offset + first, _ring, 0, count - first);
                }
                _totalWritten += count;
                var oldest = Math.Max(0, _totalWritten - _ring.LongLength);
                if (_readPosition < oldest) _readPosition = oldest;
            }
            if (failedRecording is not null && recordingException is not null)
            {
                failedRecording.Abort();
                _recordingFailed(recordingException);
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            count -= count % _blockAlign;
            if (count <= 0) return 0;
            lock (_gate)
            {
                var available = (int)Math.Min(count, Math.Max(0, _totalWritten - _readPosition));
                available -= available % _blockAlign;
                if (available > 0)
                {
                    var readIndex = (int)(_readPosition % _ring.Length);
                    var first = Math.Min(available, _ring.Length - readIndex);
                    Buffer.BlockCopy(_ring, readIndex, buffer, offset, first);
                    if (first < available)
                    {
                        Buffer.BlockCopy(_ring, 0, buffer, offset + first, available - first);
                    }
                    _readPosition += available;
                }
                if (available < count) Array.Clear(buffer, offset + available, count - available);
                return count;
            }
        }

        public void Seek(TimeSpan position)
        {
            lock (_gate)
            {
                var target = TimeToBytes(position);
                var oldest = Math.Max(0, _totalWritten - _ring.LongLength);
                _readPosition = Align(Math.Clamp(target, oldest, _totalWritten));
            }
        }

        public void JumpToLive()
        {
            lock (_gate)
            {
                var safety = WaveFormat.AverageBytesPerSecond / 4L;
                var oldest = Math.Max(0, _totalWritten - _ring.LongLength);
                _readPosition = Align(Math.Max(oldest, _totalWritten - safety));
            }
        }

        public bool TryGetRecentPlaybackAudio(
            TimeSpan duration,
            out RadioAudioSnapshot? snapshot)
        {
            lock (_gate)
            {
                var end = Math.Min(_readPosition, _totalWritten);
                return TryGetRecentAudioCore(duration, end, out snapshot);
            }
        }

        public bool TryGetRecentCapturedAudio(
            TimeSpan duration,
            out RadioAudioSnapshot? snapshot)
        {
            lock (_gate)
            {
                return TryGetRecentAudioCore(duration, _totalWritten, out snapshot);
            }
        }

        private bool TryGetRecentAudioCore(
            TimeSpan duration,
            long end,
            out RadioAudioSnapshot? snapshot)
        {
            var requestedBytes = Align((long)Math.Ceiling(
                Math.Max(0, duration.TotalSeconds) * WaveFormat.AverageBytesPerSecond));
            var oldest = Math.Max(0, _totalWritten - _ring.LongLength);
            var available = Align(Math.Max(0, Math.Min(end, _totalWritten) - oldest));
            var length = Align(Math.Min(requestedBytes, available));
            if (length < Align(WaveFormat.AverageBytesPerSecond * 3L))
            {
                snapshot = null;
                return false;
            }

            var audio = new byte[checked((int)length)];
            var start = Math.Min(end, _totalWritten) - length;
            var readIndex = (int)(start % _ring.Length);
            var first = Math.Min(audio.Length, _ring.Length - readIndex);
            Buffer.BlockCopy(_ring, readIndex, audio, 0, first);
            if (first < audio.Length)
                Buffer.BlockCopy(_ring, 0, audio, first, audio.Length - first);
            snapshot = new RadioAudioSnapshot(audio, WaveFormat, BytesToTime(length));
            return true;
        }

        public void StartRecording(
            string path,
            RadioRecordingFormat format,
            int bitRateKbps,
            string? source,
            OriginalRadioRecordingTarget? originalTarget)
        {
            lock (_gate)
            {
                if (_recording is not null) throw new InvalidOperationException("Nagrywanie już trwa.");
                _recordingPath = path;
                _recording = format == RadioRecordingFormat.Original
                    ? RadioOriginalStreamRecorder.Start(
                        path,
                        source ?? throw new InvalidOperationException("Stacja nie ma adresu strumienia."),
                        originalTarget ?? throw new InvalidOperationException("Nie rozpoznano kontenera oryginalnego strumienia."))
                    : RadioMp3Recorder.Start(path, WaveFormat, format, bitRateKbps);
            }
        }

        public string? StopRecording()
        {
            IRadioRecorder? recording;
            lock (_gate)
            {
                if (_recording is null) return null;
                recording = _recording;
                _recording = null;
                _recordingPath = null;
            }
            return recording.Stop();
        }

        public void PauseRecording()
        {
            lock (_gate)
            {
                if (_recording is null)
                    throw new InvalidOperationException("Nagrywanie jeszcze się nie rozpoczęło.");
                _recording.Pause();
            }
        }

        public void ResumeRecording()
        {
            lock (_gate)
            {
                if (_recording is null)
                    throw new InvalidOperationException("Nagrywanie jeszcze się nie rozpoczęło.");
                _recording.Resume();
            }
        }

        private TimeSpan BytesToTime(long bytes) => WaveFormat.AverageBytesPerSecond <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)bytes / WaveFormat.AverageBytesPerSecond);

        private long TimeToBytes(TimeSpan time) => Align((long)Math.Max(
            0,
            time.TotalSeconds * WaveFormat.AverageBytesPerSecond));

        private long Align(long value) => value - value % _blockAlign;
    }
}
