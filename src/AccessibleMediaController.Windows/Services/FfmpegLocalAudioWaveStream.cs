using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Tolerant, seekable audio-only view of a local container. It is used for
/// transport streams and explicitly opened recovery files that Windows Media
/// Foundation may reject because recording started between video key frames or
/// the container was never finalised. FFmpeg decodes only the audio track.
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

    private readonly object _gate = new();
    private readonly string _executable;
    private readonly string _path;
    private readonly long _length;
    private Process? _process;
    private Stream? _audio;
    private StringBuilder? _decoderError;
    private long _decoderBytesRead;
    private long _position;
    private bool _disposed;

    private FfmpegLocalAudioWaveStream(
        string executable,
        string path,
        TimeSpan duration)
    {
        _executable = executable;
        _path = path;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(OutputSampleRate, OutputChannels);
        _length = Math.Max(
            WaveFormat.BlockAlign,
            (long)Math.Min(
                duration.TotalSeconds * WaveFormat.AverageBytesPerSecond,
                long.MaxValue - WaveFormat.BlockAlign));
        StartDecoderLocked(TimeSpan.Zero);
    }

    public override WaveFormat WaveFormat { get; }
    public override long Length => _length;
    public override bool CanSeek => true;

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

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_audio is null || _position >= _length) return 0;
            var alignedCount = Math.Min(count, (int)Math.Min(int.MaxValue, _length - _position));
            alignedCount -= alignedCount % WaveFormat.BlockAlign;
            if (alignedCount <= 0) return 0;

            var read = _audio.Read(buffer, offset, alignedCount);
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
                if (_decoderBytesRead > 0)
                {
                    DiagnosticLog.Warning(
                        "ffmpeg-local",
                        $"Odtworzono dostępną część niepełnego pliku: {_path}; {detail}");
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
        var start = new ProcessStartInfo
        {
            FileName = _executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
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

    private void DisposeDecoderLocked()
    {
        try { _audio?.Dispose(); } catch (Exception) { }
        _audio = null;
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
