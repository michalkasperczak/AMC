using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AccessibleMediaController.Core.Configuration;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Encodes a live PCM stream to MP3, M4A/AAC, FLAC or WAV without blocking playback on file I/O.
/// The encoder writes to an AMC-owned temporary file and publishes the final
/// recording only after the selected encoder has closed the stream successfully.
/// </summary>
internal sealed class RadioMp3Recorder : IRadioRecorder
{
    internal const int DesiredBitRate = 192_000;
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);
    private readonly QueuedWaveProvider _queue;
    private readonly Task _encoderTask;
    private readonly string _finalPath;
    private readonly string _temporaryPath;
    private readonly object _writeGate = new();
    private readonly int _averageBytesPerSecond;
    private long _acceptedBytes;
    private bool _paused;
    private int _stopped;

    private RadioMp3Recorder(
        string finalPath,
        WaveFormat sourceFormat,
        RadioRecordingFormat recordingFormat,
        int bitRate)
    {
        if (sourceFormat.Channels is < 1 or > 2)
        {
            throw new NotSupportedException("Nagrywanie obsługuje stacje mono i stereo.");
        }

        _finalPath = finalPath;
        _temporaryPath = finalPath + ".amc-partial";
        _averageBytesPerSecond = sourceFormat.AverageBytesPerSecond;
        _queue = new QueuedWaveProvider(sourceFormat);

        IWaveProvider sourceInput = sourceFormat.Encoding == WaveFormatEncoding.Pcm
            && sourceFormat.BitsPerSample == 16
            ? _queue
            : new SampleToWaveProvider16(_queue.ToSampleProvider());

        _encoderTask = Task.Run(() =>
        {
            using var resampler = CreateLossyEncodingResampler(sourceInput, recordingFormat);
            var encoderInput = (IWaveProvider?)resampler ?? sourceInput;
            if (recordingFormat == RadioRecordingFormat.Flac)
            {
                EncodeToFlac(encoderInput, _temporaryPath);
                return;
            }

            using var output = new FileStream(
                _temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            switch (recordingFormat)
            {
                case RadioRecordingFormat.Mp3:
                    MediaFoundationEncoder.EncodeToMp3(encoderInput, output, bitRate);
                    break;
                case RadioRecordingFormat.Aac:
                    MediaFoundationEncoder.EncodeToAac(encoderInput, output, bitRate);
                    break;
                case RadioRecordingFormat.Wav:
                    WaveFileWriter.WriteWavFileToStream(output, encoderInput);
                    break;
                default:
                    throw new NotSupportedException("Wybrany format nagrania nie jest obsługiwany.");
            }
        });

        var encoderWaitHandle = ((IAsyncResult)_encoderTask).AsyncWaitHandle;
        var ready = WaitHandle.WaitAny(
            [_queue.FirstReadHandle, encoderWaitHandle],
            StartTimeout);
        if (ready == 0) return;

        AbortAndDeleteTemporaryFile();
        if (ready == 1)
        {
            ThrowEncoderFailure("Nie udało się uruchomić systemowego kodera nagrania.");
        }
        throw new TimeoutException("Systemowy koder nagrania nie odpowiedział w bezpiecznym czasie.");
    }

    public string FinalPath => _finalPath;
    public bool CanPause => true;
    public bool IsPaused
    {
        get
        {
            lock (_writeGate) return _paused;
        }
    }
    public TimeSpan RecordedDuration
    {
        get
        {
            var bytes = Interlocked.Read(ref _acceptedBytes);
            return _averageBytesPerSecond <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds((double)bytes / _averageBytesPerSecond);
        }
    }

    public static RadioMp3Recorder Start(string finalPath, WaveFormat sourceFormat) =>
        Start(finalPath, sourceFormat, RadioRecordingFormat.Mp3, DesiredBitRate / 1000);

    public static RadioMp3Recorder Start(
        string finalPath,
        WaveFormat sourceFormat,
        RadioRecordingFormat recordingFormat,
        int bitRateKbps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        ArgumentNullException.ThrowIfNull(sourceFormat);
        var expectedExtension = RecordingExtension(recordingFormat);
        if (!string.Equals(Path.GetExtension(finalPath), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Plik nagrania musi mieć rozszerzenie {expectedExtension}.",
                nameof(finalPath));
        }
        if (File.Exists(finalPath) || File.Exists(finalPath + ".amc-partial"))
        {
            throw new IOException("Plik nagrania o tej nazwie już istnieje.");
        }
        var bitRate = Math.Clamp(bitRateKbps, 64, 320) * 1000;
        return new RadioMp3Recorder(finalPath, sourceFormat, recordingFormat, bitRate);
    }

    private static MediaFoundationResampler? CreateLossyEncodingResampler(
        IWaveProvider input,
        RadioRecordingFormat recordingFormat)
    {
        if (recordingFormat is not (RadioRecordingFormat.Mp3 or RadioRecordingFormat.Aac))
            return null;

        var sourceRate = input.WaveFormat.SampleRate;
        var targetRate = sourceRate switch
        {
            32_000 or 44_100 or 48_000 => sourceRate,
            11_025 or 22_050 => 44_100,
            _ => 48_000
        };
        if (targetRate == sourceRate) return null;

        // Some Windows Media Foundation MP3 encoders silently reduce a requested
        // 128 kb/s stream to 80 kb/s when the decoded station is 22.05 or 24 kHz.
        // Resampling only the lossy export to a standard encoder rate makes the
        // selected bitrate deterministic. It does not manufacture detail; WAV,
        // FLAC and original-stream recording continue to preserve the source rate.
        return new MediaFoundationResampler(
            input,
            new WaveFormat(targetRate, 16, input.WaveFormat.Channels))
        {
            ResamplerQuality = 60
        };
    }

    internal static string RecordingExtension(RadioRecordingFormat format) => format switch
    {
        RadioRecordingFormat.Mp3 => ".mp3",
        RadioRecordingFormat.Aac => ".m4a",
        RadioRecordingFormat.Flac => ".flac",
        RadioRecordingFormat.Wav => ".wav",
        _ => throw new NotSupportedException("Wybrany format nagrania nie jest obsługiwany.")
    };

    public void Write(byte[] buffer, int offset, int count)
    {
        lock (_writeGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopped) != 0, this);
            if (_paused) return;
            if (_encoderTask.IsCompleted)
            {
                ThrowEncoderFailure("Kodowanie nagrania zostało nieoczekiwanie przerwane.");
            }
            if (!_queue.TryWrite(buffer, offset, count, TimeSpan.FromSeconds(1)))
            {
                throw new IOException("Koder nie nadąża z zapisem nagrania.");
            }
            Interlocked.Add(ref _acceptedBytes, count);
        }
    }

    public void Pause()
    {
        lock (_writeGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopped) != 0, this);
            _paused = true;
        }
    }

    public void Resume()
    {
        lock (_writeGate)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopped) != 0, this);
            _paused = false;
        }
    }

    public string Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return _finalPath;
        }

        _queue.Complete();
        try
        {
            if (!_encoderTask.Wait(StopTimeout))
            {
                throw new TimeoutException("Systemowy koder nie zakończył pliku w bezpiecznym czasie.");
            }
            _encoderTask.GetAwaiter().GetResult();
            if (!File.Exists(_temporaryPath) || new FileInfo(_temporaryPath).Length == 0)
            {
                throw new InvalidDataException("Koder nie utworzył danych nagrania.");
            }
            File.Move(_temporaryPath, _finalPath);
            return _finalPath;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or InvalidDataException
            or TimeoutException
            or System.Runtime.InteropServices.COMException
            or AggregateException)
        {
            TryDeleteTemporaryFile();
            throw new InvalidOperationException(
                "Nie udało się prawidłowo zakończyć pliku. Nie zapisano uszkodzonego nagrania.",
                exception);
        }
        finally
        {
            _queue.Dispose();
        }
    }

    public void Abort()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        AbortAndDeleteTemporaryFile();
        _queue.Dispose();
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _stopped) != 0) return;
        try
        {
            Stop();
        }
        catch (Exception exception)
        {
            DiagnosticLog.Error(
                "radio-recording",
                "Nie udało się zakończyć nagrania podczas zamykania toru radia.",
                exception);
        }
    }

    private void AbortAndDeleteTemporaryFile()
    {
        _queue.Complete();
        try { _encoderTask.Wait(TimeSpan.FromSeconds(3)); } catch (Exception) { }
        TryDeleteTemporaryFile();
    }

    private void ThrowEncoderFailure(string message)
    {
        if (_encoderTask.IsFaulted)
        {
            throw new InvalidOperationException(
                message,
                _encoderTask.Exception?.GetBaseException());
        }
        if (_encoderTask.IsCanceled) throw new InvalidOperationException(message);
        throw new InvalidOperationException(message);
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            if (File.Exists(_temporaryPath)) File.Delete(_temporaryPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning(
                "radio-recording",
                $"Nie można usunąć niedokończonego pliku nagrania: {_temporaryPath}; błąd {exception.GetType().Name}.");
        }
    }

    private static void EncodeToFlac(IWaveProvider input, string outputPath)
    {
        var executable = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new NotSupportedException("Nagrywanie FLAC wymaga komponentu FFmpeg.");
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error",
            "-f", "s16le",
            "-ar", input.WaveFormat.SampleRate.ToString(CultureInfo.InvariantCulture),
            "-ac", input.WaveFormat.Channels.ToString(CultureInfo.InvariantCulture),
            "-i", "pipe:0",
            "-vn", "-c:a", "flac", "-compression_level", "5",
            "-f", "flac", outputPath
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić kodera FLAC.");
        var error = new StringBuilder();
        process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data) || error.Length >= 2_000) return;
            if (error.Length > 0) error.Append(' ');
            error.Append(args.Data.Trim());
        };
        process.BeginErrorReadLine();
        try
        {
            using (var destination = process.StandardInput.BaseStream)
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    destination.Write(buffer, 0, read);
            }
            if (!process.WaitForExit((int)StopTimeout.TotalMilliseconds))
            {
                try { process.Kill(true); } catch (Exception) { }
                throw new TimeoutException("Koder FLAC nie zakończył pliku w bezpiecznym czasie.");
            }
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(error.Length == 0
                    ? "Koder FLAC zakończył się błędem."
                    : $"Koder FLAC zakończył się błędem. {error}");
            }
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or TimeoutException
            or Win32Exception)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch (Exception) { }
            throw new InvalidOperationException("Nie udało się zakodować nagrania FLAC.", exception);
        }
    }

    private sealed class QueuedWaveProvider(WaveFormat waveFormat) : IWaveProvider, IDisposable
    {
        private const int QueueCapacity = 128;
        private readonly BlockingCollection<byte[]> _buffers = new(
            new ConcurrentQueue<byte[]>(),
            QueueCapacity);
        private readonly ManualResetEvent _firstRead = new(false);
        private byte[]? _current;
        private int _currentOffset;
        private bool _disposed;

        public WaveFormat WaveFormat { get; } = waveFormat;
        public WaitHandle FirstReadHandle => _firstRead;

        public bool TryWrite(byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (count <= 0) return true;
            var copy = new byte[count];
            Buffer.BlockCopy(buffer, offset, copy, 0, count);
            try
            {
                return _buffers.TryAdd(copy, timeout);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            _firstRead.Set();
            var written = 0;
            while (written < count)
            {
                if (_current is not null)
                {
                    var available = _current.Length - _currentOffset;
                    var toCopy = Math.Min(available, count - written);
                    Buffer.BlockCopy(_current, _currentOffset, buffer, offset + written, toCopy);
                    _currentOffset += toCopy;
                    written += toCopy;
                    if (_currentOffset >= _current.Length)
                    {
                        _current = null;
                        _currentOffset = 0;
                    }
                    continue;
                }

                if (written > 0 && !_buffers.TryTake(out _current)) return written;
                if (_current is not null) continue;
                try
                {
                    if (!_buffers.TryTake(out _current, Timeout.Infinite)) return written;
                }
                catch (InvalidOperationException)
                {
                    return written;
                }
            }
            return written;
        }

        public void Complete()
        {
            if (!_buffers.IsAddingCompleted) _buffers.CompleteAdding();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Complete();
            _buffers.Dispose();
            _firstRead.Dispose();
        }
    }
}
