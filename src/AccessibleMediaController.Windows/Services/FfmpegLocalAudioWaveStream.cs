using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Tolerant, seekable audio-only view of a local container or a finite HTTP
/// resource. It is used for transport streams and explicitly opened recovery
/// files that Windows Media Foundation may reject, and for range-capable
/// YouTube audio where restarting FFmpeg at the requested timestamp is much
/// faster than linearly buffering through Media Foundation. FFmpeg decodes only
/// the audio track.
/// </summary>
internal sealed class FfmpegLocalAudioWaveStream : WaveStream
{
    private const int OutputSampleRate = 48_000;
    private const int OutputChannels = 2;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly Regex DurationPattern = new(
        @"Duration:\s*(?<hours>\d+):(?<minutes>\d{2}):(?<seconds>\d{2}(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BitratePattern = new(
        @"bitrate:\s*(?<bitrate>\d+)\s*kb/s",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> PreferredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".mts", ".m2ts", ".part", ".partial", ".amc-partial"
    };

    /// <summary>
    /// Ile dzwieku zywej transmisji trzymamy w zapasie. ZMIERZONE 15.09.2026 na
    /// komputerze Michala (TVP Info z YouTube, 90 s, ten sam ffmpeg co w AMC):
    /// po rozruchu transmisja przystaje REGULARNIE co okolo 7 sekund na 240-330
    /// ms - tyle trwa pobranie kolejnego segmentu. To nie jest awaria lacza,
    /// tylko normalny rytm HLS. Bez zapasu karta dzwiekowa czeka dokladnie te
    /// 300 ms i slychac zaciecie. Osiem sekund pokrywa z ogromnym marginesem
    /// zarowno rytm co 7 s, jak i dluzsze przestoje rozruchowe (zmierzone 1,1
    /// i 1,3 s).
    /// </summary>
    private static readonly TimeSpan LiveBufferTarget = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Ile zapasu zbieramy, ZANIM pojdzie pierwszy dzwiek. Krotkie, zeby
    /// transmisja nie startowala z odczuwalnym opoznieniem, ale wystarczajace,
    /// by pokryc pierwsza przerwe miedzy segmentami.
    /// </summary>
    private static readonly TimeSpan LivePrerollTarget = TimeSpan.FromSeconds(2.5);

    private readonly object _gate = new();
    private readonly string _executable;
    private readonly string _path;
    private readonly string _diagnosticSource;
    private readonly long _length;
    private readonly bool _liveStream;
    private Process? _process;
    private Stream? _audio;
    private StringBuilder? _decoderError;
    private byte[] _prefetchedAudio = [];
    private int _prefetchedAudioOffset;
    private long _decoderBytesRead;
    private long _position;
    private bool _disposed;

    // Zapas dzwieku zywej transmisji i watek, ktory go napelnia. Uzywane tylko
    // gdy _liveStream; dla plikow i skonczonych zrodel nic sie nie zmienia.
    private readonly object _liveGate = new();
    private readonly Queue<byte[]> _liveChunks = new();
    private Thread? _livePump;
    private CancellationTokenSource? _livePumpCancellation;
    private byte[] _liveCurrent = [];
    private int _liveCurrentOffset;
    private long _liveBufferedBytes;
    private bool _liveEnded;
    private Exception? _liveFailure;

    private FfmpegLocalAudioWaveStream(
        string executable,
        string path,
        TimeSpan duration,
        bool liveStream = false)
    {
        _executable = executable;
        _path = path;
        _liveStream = liveStream;
        _diagnosticSource = Uri.TryCreate(path, UriKind.Absolute, out var networkUri)
            && networkUri.Scheme is "http" or "https"
                ? networkUri.Host
                : path;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(OutputSampleRate, OutputChannels);
        // 2026-09-14: transmisja na zywo NIE MA konca. Podanie tu prawdziwej
        // dlugosci sprawia, ze warstwa wyzej traktuje strumien jak plik i po
        // dojsciu do "konca" wraca na poczatek - dzwiek cofal sie o kilka
        // sekund co kilka sekund. Dla zywej transmisji zglaszamy dlugosc
        // maksymalna, wiec nikt nie probuje przewijac ani zapetlac.
        _length = liveStream
            ? long.MaxValue - WaveFormat.BlockAlign
            : Math.Max(
            WaveFormat.BlockAlign,
            (long)Math.Min(
                duration.TotalSeconds * WaveFormat.AverageBytesPerSecond,
                long.MaxValue - WaveFormat.BlockAlign));
        StartDecoderLocked(TimeSpan.Zero);
    }

    public override WaveFormat WaveFormat { get; }
    public override long Length => _length;
    public override bool CanSeek => !_liveStream;

    public override long Position
    {
        get
        {
            lock (_gate) return _position;
        }
        set
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                // Zywej transmisji nie da sie przewinac - zignoruj zamiast
                // restartowac dekoder, bo restart gubi biezaca pozycje.
                if (_liveStream) return;
                var normalized = Math.Clamp(value, 0, _length);
                normalized -= normalized % WaveFormat.BlockAlign;
                if (normalized == _position) return;
                DisposeDecoderLocked();
                _position = normalized;
                if (_position < _length)
                {
                    StartDecoderLocked(TimeSpan.FromSeconds(
                        _position / (double)WaveFormat.AverageBytesPerSecond));
                }
            }
        }
    }

    internal static bool ShouldPrefer(string path) =>
        PreferredExtensions.Contains(Path.GetExtension(path));

    internal static bool TryOpen(string path, out FfmpegLocalAudioWaveStream reader)
    {
        reader = null!;
        var executable = FfmpegRadioWaveProvider.FindExecutable();
        if (executable is null || !File.Exists(path)) return false;

        try
        {
            var duration = ProbeDuration(executable, path);
            if (duration <= TimeSpan.Zero) return false;
            reader = new FfmpegLocalAudioWaveStream(executable, path, duration);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or NotSupportedException
            or TimeoutException
            or System.ComponentModel.Win32Exception)
        {
            DiagnosticLog.Warning(
                "ffmpeg-local",
                $"Awaryjny dekoder nie otworzył pliku: {path}; {exception.Message}");
            reader?.Dispose();
            reader = null!;
            return false;
        }
    }

    /// <summary>
    /// Otwiera ZYWA transmisje (HLS z przesuwajacym sie okienkiem segmentow).
    /// Nie zna dlugosci, nie pozwala przewijac i startuje od najnowszego
    /// segmentu - dokladnie tak, jak radio internetowe w tej samej aplikacji.
    /// </summary>
    internal static bool TryOpenLive(
        string address,
        out FfmpegLocalAudioWaveStream reader)
    {
        reader = null!;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return false;
        }

        foreach (var executable in FfmpegRadioWaveProvider.EnumerateExecutableCandidates())
        {
            try
            {
                reader = new FfmpegLocalAudioWaveStream(
                    executable,
                    uri.AbsoluteUri,
                    TimeSpan.Zero,
                    liveStream: true);
                return true;
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or InvalidOperationException
                or NotSupportedException
                or System.ComponentModel.Win32Exception)
            {
                DiagnosticLog.Warning(
                    "ffmpeg-live",
                    $"Dekoder {Path.GetFileName(executable)} nie otworzyl zywej "
                    + $"transmisji z hosta {uri.Host}; {exception.Message}");
                reader?.Dispose();
                reader = null!;
            }
        }
        return false;
    }

    internal static bool TryOpenNetwork(
        string address,
        TimeSpan duration,
        out FfmpegLocalAudioWaveStream reader)
    {
        reader = null!;
        if (duration <= TimeSpan.Zero
            || !Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return false;
        }

        foreach (var executable in FfmpegRadioWaveProvider.EnumerateExecutableCandidates())
        {
            try
            {
                reader = new FfmpegLocalAudioWaveStream(executable, uri.AbsoluteUri, duration);
                return true;
            }
            catch (Exception exception) when (exception is IOException
                or InvalidDataException
                or InvalidOperationException
                or NotSupportedException
                or System.ComponentModel.Win32Exception)
            {
                DiagnosticLog.Warning(
                    "ffmpeg-network",
                    $"Dekoder sieciowy {Path.GetFileName(executable)} nie otworzył "
                    + $"skończonego materiału z hosta {uri.Host}; {exception.Message}");
                reader?.Dispose();
                reader = null!;
            }
        }
        return false;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_audio is null || (!_liveStream && _position >= _length)) return 0;
            var alignedCount = _liveStream
                ? count
                : Math.Min(count, (int)Math.Min(int.MaxValue, _length - _position));
            alignedCount -= alignedCount % WaveFormat.BlockAlign;
            if (alignedCount <= 0) return 0;

            if (_prefetchedAudioOffset < _prefetchedAudio.Length)
            {
                var prefetched = Math.Min(
                    alignedCount,
                    _prefetchedAudio.Length - _prefetchedAudioOffset);
                prefetched -= prefetched % WaveFormat.BlockAlign;
                if (prefetched > 0)
                {
                    Buffer.BlockCopy(
                        _prefetchedAudio,
                        _prefetchedAudioOffset,
                        buffer,
                        offset,
                        prefetched);
                    _prefetchedAudioOffset += prefetched;
                    _position = Math.Min(_length, _position + prefetched);
                    if (_prefetchedAudioOffset >= _prefetchedAudio.Length)
                    {
                        _prefetchedAudio = [];
                        _prefetchedAudioOffset = 0;
                    }
                    return prefetched;
                }
            }

            var read = _liveStream
                ? ReadLiveFromBuffer(buffer, offset, alignedCount)
                : _audio.Read(buffer, offset, alignedCount);
            if (read > 0)
            {
                _decoderBytesRead += read;
                _position = Math.Min(_length, _position + read);
                return read;
            }

            if (_process is { HasExited: true, ExitCode: not 0 })
            {
                string? detail = null;
                if (_decoderError is { } decoderError)
                {
                    lock (decoderError) detail = decoderError.ToString().Trim();
                }
                if (!string.IsNullOrWhiteSpace(detail)
                    && !string.Equals(_diagnosticSource, _path, StringComparison.Ordinal))
                {
                    detail = detail.Replace(
                        _path,
                        _diagnosticSource,
                        StringComparison.OrdinalIgnoreCase);
                }
                if (_decoderBytesRead > 0)
                {
                    DiagnosticLog.Warning(
                        "ffmpeg-local",
                        $"Odtworzono dostępną część niepełnego źródła: {_diagnosticSource}; {detail}");
                    return 0;
                }
                throw new InvalidDataException(string.IsNullOrWhiteSpace(detail)
                    ? "Awaryjny dekoder zakończył się błędem."
                    : $"Awaryjny dekoder zakończył się błędem. {detail}");
            }
            return 0;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                DisposeDecoderLocked();
            }
        }
        base.Dispose(disposing);
    }

    private void StartDecoderLocked(TimeSpan position)
    {
        var networkSource = Uri.TryCreate(_path, UriKind.Absolute, out var networkUri)
            && networkUri.Scheme is "http" or "https";
        var attempts = networkSource ? 3 : 1;
        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                StartDecoderProcessLocked(position, networkSource);
                if (networkSource) PrimeNetworkDecoderLocked();
                // Zywa transmisja: od tej chwili dzwiek zbiera watek do zapasu,
                // a karta dzwiekowa bierze z zapasu, nie wprost z ffmpeg.
                if (_liveStream) StartLivePumpLocked();
                return;
            }
            catch (Exception exception) when (
                networkSource
                && exception is IOException
                    or InvalidDataException
                    or InvalidOperationException
                    or System.ComponentModel.Win32Exception)
            {
                lastFailure = exception;
                DisposeDecoderLocked();
                if (attempt < attempts)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(200 * attempt));
                }
            }
        }
        throw lastFailure ?? new InvalidDataException("Nie udało się otworzyć strumienia sieciowego.");
    }

    private void StartDecoderProcessLocked(TimeSpan position, bool networkSource)
    {
        var start = new ProcessStartInfo
        {
            FileName = _executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        ExternalToolProcess.ApplySafeEnvironment(start, _executable);
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "warning",
            "-fflags", "+discardcorrupt", "-err_detect", "ignore_err"
        })
        {
            start.ArgumentList.Add(argument);
        }
        if (position > TimeSpan.Zero)
        {
            start.ArgumentList.Add("-ss");
            start.ArgumentList.Add(position.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture));
        }
        if (networkSource)
        {
            foreach (var argument in new[]
            {
                "-rw_timeout", "20000000",
                "-reconnect", "1",
                "-reconnect_streamed", "1",
                "-reconnect_delay_max", "2"
            })
            {
                start.ArgumentList.Add(argument);
            }
        }
        if (_liveStream)
        {
            // Te same opcje, ktorych uzywa radio internetowe (FfmpegRadioWaveProvider):
            // manifest zywej transmisji wystawia kilka juz zakonczonych segmentow,
            // wiec bez "-live_start_index -1" ffmpeg wyrzuca zalegly material w
            // paczce, a odtwarzanie zaczyna sie od tego, co bylo wczesniej.
            foreach (var argument in new[]
            {
                "-live_start_index", "-1",
                "-readrate", "1"
            })
            {
                start.ArgumentList.Add(argument);
            }
        }
        foreach (var argument in new[]
        {
            "-i", _path,
            "-map", "0:a:0", "-vn",
            "-f", "f32le", "-acodec", "pcm_f32le",
            "-ar", OutputSampleRate.ToString(CultureInfo.InvariantCulture),
            "-ac", OutputChannels.ToString(CultureInfo.InvariantCulture),
            "pipe:1"
        })
        {
            start.ArgumentList.Add(argument);
        }

        var decoderError = new StringBuilder();
        _decoderError = decoderError;
        _decoderBytesRead = 0;
        _process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić awaryjnego dekodera.");
        _audio = _process.StandardOutput.BaseStream;
        _process.ErrorDataReceived += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.Data)) return;
            lock (decoderError)
            {
                if (decoderError.Length >= 2_000) return;
                if (decoderError.Length > 0) decoderError.Append(' ');
                decoderError.Append(args.Data.Trim());
            }
        };
        _process.BeginErrorReadLine();
    }

    private void PrimeNetworkDecoderLocked()
    {
        var buffer = new byte[16 * 1024];
        var read = _audio?.Read(buffer, 0, buffer.Length) ?? 0;
        read -= read % WaveFormat.BlockAlign;
        if (read > 0)
        {
            _prefetchedAudio = read == buffer.Length ? buffer : buffer[..read];
            _prefetchedAudioOffset = 0;
            _decoderBytesRead = read;
            return;
        }

        string? detail = null;
        if (_decoderError is { } decoderError)
        {
            lock (decoderError) detail = decoderError.ToString().Trim();
        }
        if (!string.IsNullOrWhiteSpace(detail))
        {
            detail = detail.Replace(
                _path,
                _diagnosticSource,
                StringComparison.OrdinalIgnoreCase);
        }
        throw new InvalidDataException(string.IsNullOrWhiteSpace(detail)
            ? "Dekoder sieciowy nie zwrócił dźwięku."
            : $"Dekoder sieciowy nie zwrócił dźwięku. {detail}");
    }

    private void StartLivePumpLocked()
    {
        // Watek zbierajacy dzwiek Z WYPRZEDZENIEM - to samo, co robi radio
        // internetowe w CaptureLoopAsync (RadioMediaOutput). Bez tego karta
        // dzwiekowa czyta wprost z ffmpeg i kazda przerwa miedzy segmentami
        // HLS jest slyszalna. Watek jest tlowy (IsBackground), wiec nie
        // wstrzymuje zamykania programu.
        var stream = _audio;
        if (stream is null) return;
        var cancellation = new CancellationTokenSource();
        _livePumpCancellation = cancellation;
        var target = (long)(LiveBufferTarget.TotalSeconds * WaveFormat.AverageBytesPerSecond);
        var chunkSize = Math.Max(16 * 1024, WaveFormat.AverageBytesPerSecond / 8);
        var pump = new Thread(() =>
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    // Nie czytamy w nieskonczonosc: gdy zapas jest pelny,
                    // czekamy. Inaczej ffmpeg z opcja -readrate 1 i tak by nas
                    // przytrzymal, ale pamiec rosla by bez gornej granicy.
                    lock (_liveGate)
                    {
                        while (_liveBufferedBytes >= target
                            && !cancellation.IsCancellationRequested)
                        {
                            Monitor.Wait(_liveGate, 50);
                        }
                    }
                    if (cancellation.IsCancellationRequested) return;

                    var chunk = new byte[chunkSize];
                    var read = stream.Read(chunk, 0, chunk.Length);
                    if (read <= 0)
                    {
                        lock (_liveGate)
                        {
                            _liveEnded = true;
                            Monitor.PulseAll(_liveGate);
                        }
                        return;
                    }
                    lock (_liveGate)
                    {
                        _liveChunks.Enqueue(read == chunk.Length ? chunk : chunk[..read]);
                        _liveBufferedBytes += read;
                        Monitor.PulseAll(_liveGate);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException
                or ObjectDisposedException
                or InvalidOperationException)
            {
                lock (_liveGate)
                {
                    _liveFailure = exception;
                    _liveEnded = true;
                    Monitor.PulseAll(_liveGate);
                }
            }
        })
        {
            IsBackground = true,
            Name = "AMC zapas zywej transmisji"
        };
        _livePump = pump;
        pump.Start();

        // Rozruch: czekamy na maly zapas, zeby pierwsza przerwa miedzy
        // segmentami nie trafila w pusty bufor. Czekanie jest ograniczone w
        // czasie - jesli transmisja jest wolna, ruszamy z tym, co jest.
        var preroll = (long)(LivePrerollTarget.TotalSeconds * WaveFormat.AverageBytesPerSecond);
        var deadline = Stopwatch.StartNew();
        lock (_liveGate)
        {
            while (_liveBufferedBytes < preroll
                && !_liveEnded
                && deadline.Elapsed < TimeSpan.FromSeconds(6))
            {
                Monitor.Wait(_liveGate, 100);
            }
        }
    }

    /// <summary>
    /// Czyta dzwiek zywej transmisji Z ZAPASU, nie wprost z ffmpeg. Gdy zapas
    /// chwilowo pustoszeje (dluga przerwa w sieci), czekamy krotko, a potem
    /// zwracamy CISZE zamiast blokowac karte dzwiekowa - cisza jest mniej
    /// szkodliwa niz zablokowany watek odtwarzania.
    /// </summary>
    private int ReadLiveFromBuffer(byte[] buffer, int offset, int count)
    {
        var written = 0;
        var waited = Stopwatch.StartNew();
        while (written < count)
        {
            if (_liveCurrentOffset >= _liveCurrent.Length)
            {
                lock (_liveGate)
                {
                    while (_liveChunks.Count == 0 && !_liveEnded)
                    {
                        if (written > 0 || waited.Elapsed > TimeSpan.FromSeconds(5)) break;
                        Monitor.Wait(_liveGate, 100);
                    }
                    if (_liveChunks.Count > 0)
                    {
                        _liveCurrent = _liveChunks.Dequeue();
                        _liveCurrentOffset = 0;
                        _liveBufferedBytes -= _liveCurrent.Length;
                        Monitor.PulseAll(_liveGate);
                    }
                    else
                    {
                        if (_liveEnded)
                        {
                            if (_liveFailure is { } failure && written == 0 && _decoderBytesRead == 0)
                            {
                                throw new InvalidDataException(
                                    "Zywa transmisja przerwala sie. " + failure.Message);
                            }
                            break;
                        }
                        break;
                    }
                }
            }

            var available = _liveCurrent.Length - _liveCurrentOffset;
            if (available <= 0) break;
            var take = Math.Min(available, count - written);
            Buffer.BlockCopy(_liveCurrent, _liveCurrentOffset, buffer, offset + written, take);
            _liveCurrentOffset += take;
            written += take;
        }
        return written;
    }

    private void StopLivePumpLocked()
    {
        try { _livePumpCancellation?.Cancel(); } catch (Exception) { }
        lock (_liveGate) Monitor.PulseAll(_liveGate);
        var pump = _livePump;
        _livePump = null;
        if (pump is not null && pump.IsAlive)
        {
            // Krotkie oczekiwanie - watek jest tlowy, wiec nawet gdyby wisial
            // na odczycie z ffmpeg, nie zablokuje zamkniecia programu.
            try { pump.Join(TimeSpan.FromMilliseconds(300)); } catch (Exception) { }
        }
        try { _livePumpCancellation?.Dispose(); } catch (Exception) { }
        _livePumpCancellation = null;
        lock (_liveGate)
        {
            _liveChunks.Clear();
            _liveBufferedBytes = 0;
            _liveCurrent = [];
            _liveCurrentOffset = 0;
            _liveEnded = false;
            _liveFailure = null;
        }
    }

    private void DisposeDecoderLocked()
    {
        if (_liveStream) StopLivePumpLocked();
        try { _audio?.Dispose(); } catch (Exception) { }
        _audio = null;
        _prefetchedAudio = [];
        _prefetchedAudioOffset = 0;
        try
        {
            if (_process is { HasExited: false }) _process.Kill(true);
        }
        catch (Exception) { }
        try { _process?.Dispose(); } catch (Exception) { }
        _process = null;
        _decoderError = null;
    }

    private static TimeSpan ProbeDuration(string executable, string path)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        ExternalToolProcess.ApplySafeEnvironment(start, executable);
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner",
            "-analyzeduration", "20000000", "-probesize", "20000000",
            "-i", path
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić analizy pliku.");
        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit((int)ProbeTimeout.TotalMilliseconds))
        {
            try { process.Kill(true); } catch (Exception) { }
            throw new TimeoutException("Analiza niedokończonego kontenera przekroczyła bezpieczny czas.");
        }
        if (!Task.WaitAll([errorTask, outputTask], ProbeTimeout))
            throw new TimeoutException("Nie zakończono odczytu wyniku analizy pliku.");
        var diagnostic = errorTask.GetAwaiter().GetResult();
        var durationMatch = DurationPattern.Match(diagnostic);
        if (durationMatch.Success)
        {
            var hours = int.Parse(durationMatch.Groups["hours"].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(durationMatch.Groups["minutes"].Value, CultureInfo.InvariantCulture);
            var seconds = double.Parse(durationMatch.Groups["seconds"].Value, CultureInfo.InvariantCulture);
            return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        }

        var bitrateMatch = BitratePattern.Match(diagnostic);
        if (bitrateMatch.Success
            && int.TryParse(bitrateMatch.Groups["bitrate"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bitrateKbps)
            && bitrateKbps > 0)
        {
            var length = new FileInfo(path).Length;
            return TimeSpan.FromSeconds(length * 8d / (bitrateKbps * 1000d));
        }
        throw new InvalidDataException("Nie udało się określić czasu dostępnej części pliku.");
    }
}
