using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Live radio output with an in-memory decoded-audio ring. The decoder works
/// away from the WPF dispatcher, so a slow or broken station cannot freeze the
/// accessible interface. The same ring provides pause and time-shift without
/// creating unbounded temporary files.
/// </summary>
public sealed class RadioMediaOutput(int timeshiftMinutes) : IMediaOutput, IDisposable
{
    private const int MaximumTimeshiftBytes = 256 * 1024 * 1024;
    private static readonly TimeSpan PreparationTimeout = TimeSpan.FromSeconds(25);
    private readonly SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private readonly object _gate = new();
    private readonly int _timeshiftMinutes = Math.Clamp(timeshiftMinutes, 1, 60);
    private RadioPipeline? _pipeline;
    private MediaItem? _requestedItem;
    private int _volume = 35;
    private long _requestVersion;
    private bool _preparing;
    private bool _disposed;

    public event EventHandler<MediaOutputFailedEventArgs>? PlaybackFailed;
    public event EventHandler<MediaPlaybackPreparingEventArgs>? PlaybackPreparing;
    public event EventHandler<MediaPlaybackStartedEventArgs>? PlaybackStarted;
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
            reusable = _pipeline is not null
                && string.Equals(_pipeline.Item.Source, item.Source, StringComparison.OrdinalIgnoreCase)
                ? _pipeline
                : null;
        }
        if (reusable is not null)
        {
            reusable.Volume.Volume = _volume / 100f;
            reusable.Output.Play();
            RaiseOnCapturedContext(() => PlaybackStarted?.Invoke(
                this,
                new MediaPlaybackStartedEventArgs(item)));
            return;
        }

        RadioPipeline? previous;
        long requestVersion;
        lock (_gate)
        {
            previous = DetachPipelineLocked();
            _preparing = true;
            requestVersion = ++_requestVersion;
        }
        if (previous is not null) QueueDisposal(previous);
        DiagnosticLog.Info("radio", $"Łączenie ze stacją: {item.Title}.");
        PlaybackPreparing?.Invoke(this, new MediaPlaybackPreparingEventArgs(item, false));
        _ = Task.Run(() => PrepareAndStartAsync(item, requestVersion));
        _ = WatchPreparationTimeoutAsync(item, requestVersion);
    }

    private async Task PrepareAndStartAsync(MediaItem item, long requestVersion)
    {
        RadioPipeline? pipeline = null;
        try
        {
            var resolvedSource = await RadioStreamResolver.ResolveAsync(item.Source!, CancellationToken.None)
                .ConfigureAwait(false);
            var openedReader = await OpenReaderAsync(resolvedSource).ConfigureAwait(false);
            var reader = openedReader.Reader;
            var buffer = new RadioTimeshiftWaveProvider(
                reader.WaveFormat,
                _timeshiftMinutes,
                MaximumTimeshiftBytes,
                RaiseRecordingFailed);
            var volume = new VolumeSampleProvider(buffer.ToSampleProvider())
            {
                Volume = Math.Clamp(_volume, 0, 100) / 100f
            };
            var output = new WasapiOut(AudioClientShareMode.Shared, true, 180);
            output.Init(volume);
            var cancellation = new CancellationTokenSource();
            pipeline = new RadioPipeline(
                item,
                reader,
                openedReader.Lifetime,
                buffer,
                volume,
                output,
                cancellation);

            lock (_gate)
            {
                if (_disposed || requestVersion != _requestVersion)
                {
                    QueueDisposal(pipeline);
                    return;
                }
                _pipeline = pipeline;
                _preparing = false;
            }
            output.Play();
            pipeline.CaptureTask = Task.Run(() => CaptureLoopAsync(pipeline), cancellation.Token);
            DiagnosticLog.Info(
                "radio",
                $"Rozpoczęto odbiór: {item.Title}; format {reader.WaveFormat}; dekoder {openedReader.DecoderName}.");
            RaiseOnCapturedContext(() => PlaybackStarted?.Invoke(
                this,
                new MediaPlaybackStartedEventArgs(item)));
        }
        catch (Exception exception) when (exception is IOException
            or HttpRequestException
            or TaskCanceledException
            or InvalidOperationException
            or InvalidDataException
            or NotSupportedException
            or ArgumentException
            or AuthenticationException
            or System.Runtime.InteropServices.COMException)
        {
            if (pipeline is not null) QueueDisposal(pipeline);
            var current = false;
            lock (_gate)
            {
                if (!_disposed && requestVersion == _requestVersion)
                {
                    _preparing = false;
                    current = true;
                }
            }
            DiagnosticLog.Warning("radio", $"Nie udało się otworzyć stacji {item.Title}; błąd {exception.GetType().Name}.");
            if (current) RaisePlaybackFailed(
                item,
                "Nie udało się odtworzyć tej stacji. Sprawdź adres strumienia lub spróbuj ponownie później.");
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task<OpenedRadioReader> OpenReaderAsync(string source)
    {
        try
        {
            var mediaFoundation = new MediaFoundationReader(source);
            return new OpenedRadioReader(mediaFoundation, mediaFoundation, "systemowy");
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or NotSupportedException
            or ArgumentException
            or System.Runtime.InteropServices.COMException)
        {
            DiagnosticLog.Info(
                "radio",
                $"Dekoder systemowy odrzucił strumień; próba zgodności ze starszym radiem MP3 ({exception.GetType().Name}).");
            var legacy = await LegacyIcyMp3StreamReader.OpenAsync(source, CancellationToken.None)
                .ConfigureAwait(false);
            return new OpenedRadioReader(legacy, legacy, "zgodności ICY MP3");
        }
    }

    private async Task CaptureLoopAsync(RadioPipeline pipeline)
    {
        var bytes = new byte[Math.Max(16 * 1024, pipeline.Reader.WaveFormat.AverageBytesPerSecond / 4)];
        try
        {
            while (!pipeline.Cancellation.IsCancellationRequested)
            {
                var read = pipeline.Reader.Read(bytes, 0, bytes.Length);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Serwer zakończył strumień.");
                }
                pipeline.Buffer.Write(bytes, 0, read);
            }
        }
        catch (Exception exception) when (pipeline.Cancellation.IsCancellationRequested
            && exception is ObjectDisposedException or IOException or InvalidOperationException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException
            or EndOfStreamException
            or InvalidDataException
            or InvalidOperationException
            or System.Runtime.InteropServices.COMException)
        {
            DiagnosticLog.Warning("radio", $"Odbiór stacji został przerwany: {pipeline.Item.Title}; błąd {exception.GetType().Name}.");
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
                    "Połączenie ze stacją zostało przerwane. Uruchom ją ponownie, aby połączyć się jeszcze raz.");
            }
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task WatchPreparationTimeoutAsync(MediaItem item, long requestVersion)
    {
        await Task.Delay(PreparationTimeout).ConfigureAwait(false);
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
        DiagnosticLog.Warning("radio", $"Przekroczono czas łączenia: {item.Title}.");
        RaisePlaybackFailed(item, "Stacja nie odpowiedziała w bezpiecznym czasie.");
    }

    public void Pause()
    {
        lock (_gate) _pipeline?.Output.Pause();
    }

    public void Stop()
    {
        RadioPipeline? pipeline;
        lock (_gate)
        {
            ++_requestVersion;
            _preparing = false;
            pipeline = DetachPipelineLocked();
        }
        if (pipeline is not null) QueueDisposal(pipeline);
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

    public string StartRecording(string folder)
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
            var path = UniqueRecordingPath(folder, $"{safeName} - {DateTime.Now:yyyy-MM-dd HH-mm-ss}");
            _pipeline.Buffer.StartRecording(path);
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

    private RadioPipeline? DetachPipelineLocked()
    {
        var pipeline = _pipeline;
        _pipeline = null;
        return pipeline;
    }

    private static bool IsHttpStream(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    private static string UniqueRecordingPath(string folder, string baseName)
    {
        var path = Path.Combine(folder, baseName + ".mp3");
        for (var suffix = 2; File.Exists(path) || File.Exists(path + ".amc-partial"); suffix++)
        {
            path = Path.Combine(folder, $"{baseName} ({suffix}).mp3");
        }
        return path;
    }

    private static void DeleteStalePartialRecordings(string folder)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.mp3.amc-partial"))
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

    private void RaisePlaybackFailed(MediaItem item, string message) =>
        RaiseOnCapturedContext(() => PlaybackFailed?.Invoke(this, new MediaOutputFailedEventArgs(item, message)));

    private void RaiseRecordingFailed(Exception exception)
    {
        DiagnosticLog.Error(
            "radio-recording",
            "Nagrywanie MP3 zostało przerwane, ale odtwarzanie radia jest kontynuowane.",
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
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_requestVersion;
            _preparing = false;
            pipeline = DetachPipelineLocked();
        }
        pipeline?.Dispose();
    }

    private sealed class RadioPipeline(
        MediaItem item,
        IWaveProvider reader,
        IDisposable readerLifetime,
        RadioTimeshiftWaveProvider buffer,
        VolumeSampleProvider volume,
        WasapiOut output,
        CancellationTokenSource cancellation) : IDisposable
    {
        private int _disposed;
        public MediaItem Item { get; } = item;
        public IWaveProvider Reader { get; } = reader;
        public IDisposable ReaderLifetime { get; } = readerLifetime;
        public RadioTimeshiftWaveProvider Buffer { get; } = buffer;
        public VolumeSampleProvider Volume { get; } = volume;
        public WasapiOut Output { get; } = output;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task? CaptureTask { get; set; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Cancellation.Cancel();
            try { Buffer.StopRecording(); }
            catch (Exception exception)
            {
                DiagnosticLog.Error(
                    "radio-recording",
                    "Nie udało się zakończyć nagrania MP3 podczas zamykania stacji.",
                    exception);
            }
            try { Output.Stop(); } catch (Exception) { }
            try { ReaderLifetime.Dispose(); } catch (Exception) { }
            try { Output.Dispose(); } catch (Exception) { }
            Cancellation.Dispose();
        }
    }

    private sealed record OpenedRadioReader(
        IWaveProvider Reader,
        IDisposable Lifetime,
        string DecoderName);

    private sealed class RadioTimeshiftWaveProvider : IWaveProvider
    {
        private readonly object _gate = new();
        private readonly byte[] _ring;
        private readonly int _blockAlign;
        private readonly Action<Exception> _recordingFailed;
        private long _totalWritten;
        private long _readPosition;
        private RadioMp3Recorder? _recording;
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

            RadioMp3Recorder? failedRecording = null;
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

        public void StartRecording(string path)
        {
            lock (_gate)
            {
                if (_recording is not null) throw new InvalidOperationException("Nagrywanie już trwa.");
                _recordingPath = path;
                _recording = RadioMp3Recorder.Start(path, WaveFormat);
            }
        }

        public string? StopRecording()
        {
            RadioMp3Recorder? recording;
            lock (_gate)
            {
                if (_recording is null) return null;
                recording = _recording;
                _recording = null;
                _recordingPath = null;
            }
            return recording.Stop();
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
