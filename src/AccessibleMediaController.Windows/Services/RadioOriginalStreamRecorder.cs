using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AccessibleMediaController.Windows.Services;

internal sealed record OriginalRadioRecordingTarget(
    string Extension,
    string Muxer,
    string Description);

/// <summary>
/// Opens a second, inaudible connection to the station and asks FFmpeg to copy
/// the audio packets without transcoding. ICY metadata is removed by FFmpeg;
/// HLS segments are joined and remuxed to an audio-only transport stream.
/// </summary>
internal sealed class RadioOriginalStreamRecorder : IRadioRecorder
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(30);
    private readonly Process _process;
    private readonly string _temporaryPath;
    private readonly object _errorGate = new();
    private readonly StringBuilder _error = new();
    private int _stopped;

    private RadioOriginalStreamRecorder(
        string finalPath,
        string source,
        OriginalRadioRecordingTarget target)
    {
        FinalPath = finalPath;
        _temporaryPath = finalPath + ".amc-partial";
        var executable = FfmpegRadioWaveProvider.FindExecutable()
            ?? throw new NotSupportedException(
                "Zapis w formacie oryginalnym wymaga komponentu FFmpeg.");

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
            "-rw_timeout", "15000000",
            "-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5",
            "-i", source,
            "-map", "0:a:0", "-vn", "-c:a", "copy",
            "-f", target.Muxer,
            _temporaryPath
        })
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            _process = Process.Start(start)
                ?? throw new InvalidOperationException("Nie udało się uruchomić komponentu FFmpeg.");
            _process.ErrorDataReceived += Process_ErrorDataReceived;
            _process.BeginErrorReadLine();
        }
        catch (Exception exception) when (exception is Win32Exception
            or IOException
            or InvalidOperationException)
        {
            TryDeleteTemporaryFile();
            throw new InvalidOperationException(
                "Nie udało się rozpocząć zapisu oryginalnego strumienia.",
                exception);
        }
    }

    public string FinalPath { get; }

    public static RadioOriginalStreamRecorder Start(
        string finalPath,
        string source,
        OriginalRadioRecordingTarget target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(target);
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "Oryginalny zapis radia wymaga adresu HTTP lub HTTPS.",
                nameof(source));
        }
        if (!string.Equals(Path.GetExtension(finalPath), target.Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Plik oryginalnego nagrania musi mieć rozszerzenie {target.Extension}.",
                nameof(finalPath));
        }
        if (File.Exists(finalPath) || File.Exists(finalPath + ".amc-partial"))
            throw new IOException("Plik nagrania o tej nazwie już istnieje.");
        return new RadioOriginalStreamRecorder(finalPath, source, target);
    }

    internal static OriginalRadioRecordingTarget Describe(string source, string? codec)
    {
        var normalizedCodec = (codec ?? string.Empty).Trim().ToUpperInvariant();
        if (LooksLikeHls(source))
        {
            return new OriginalRadioRecordingTarget(
                ".ts",
                "mpegts",
                "oryginalny dźwięk HLS, bez konwersji");
        }
        if (normalizedCodec.Contains("MP3", StringComparison.Ordinal)
            || normalizedCodec.Contains("MPEG LAYER", StringComparison.Ordinal))
        {
            return new OriginalRadioRecordingTarget(".mp3", "mp3", "oryginalny MP3, bez konwersji");
        }
        if (normalizedCodec.Contains("AAC", StringComparison.Ordinal))
        {
            return new OriginalRadioRecordingTarget(".aac", "adts", "oryginalny AAC, bez konwersji");
        }
        if (normalizedCodec.Contains("VORBIS", StringComparison.Ordinal)
            || normalizedCodec.Contains("OPUS", StringComparison.Ordinal)
            || normalizedCodec.Contains("OGG", StringComparison.Ordinal))
        {
            return new OriginalRadioRecordingTarget(".ogg", "ogg", "oryginalny OGG, bez konwersji");
        }
        if (normalizedCodec.Contains("FLAC", StringComparison.Ordinal))
        {
            return new OriginalRadioRecordingTarget(".flac", "flac", "oryginalny FLAC, bez konwersji");
        }

        var extension = Uri.TryCreate(source, UriKind.Absolute, out var uri)
            ? Path.GetExtension(uri.AbsolutePath).ToLowerInvariant()
            : string.Empty;
        return extension switch
        {
            ".mp3" => new OriginalRadioRecordingTarget(".mp3", "mp3", "oryginalny MP3, bez konwersji"),
            ".aac" => new OriginalRadioRecordingTarget(".aac", "adts", "oryginalny AAC, bez konwersji"),
            ".ogg" or ".opus" => new OriginalRadioRecordingTarget(".ogg", "ogg", "oryginalny OGG, bez konwersji"),
            ".flac" => new OriginalRadioRecordingTarget(".flac", "flac", "oryginalny FLAC, bez konwersji"),
            _ => new OriginalRadioRecordingTarget(
                ".mka",
                "matroska",
                "oryginalny dźwięk w kontenerze Matroska, bez konwersji")
        };
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopped) != 0, this);
        if (!_process.HasExited) return;
        throw new InvalidOperationException(BuildFailureMessage(
            "Połączenie zapisujące oryginalny strumień zostało przerwane."));
    }

    public string Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return FinalPath;
        try
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.StandardInput.WriteLine("q");
                    _process.StandardInput.Flush();
                    _process.StandardInput.Close();
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException)
                {
                    // The process may have ended between HasExited and the write.
                }
            }
            if (!_process.WaitForExit((int)StopTimeout.TotalMilliseconds))
            {
                TryKill();
                throw new TimeoutException(
                    "Komponent zapisujący strumień nie zakończył pliku w bezpiecznym czasie.");
            }
            if (_process.ExitCode != 0)
            {
                throw new InvalidOperationException(BuildFailureMessage(
                    "Nie udało się zakończyć zapisu oryginalnego strumienia."));
            }
            if (!File.Exists(_temporaryPath) || new FileInfo(_temporaryPath).Length == 0)
                throw new InvalidDataException("Nie odebrano danych oryginalnego strumienia.");
            File.Move(_temporaryPath, FinalPath);
            return FinalPath;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or InvalidDataException
            or TimeoutException)
        {
            TryDeleteTemporaryFile();
            throw new InvalidOperationException(
                "Nie zapisano uszkodzonego lub niekompletnego nagrania oryginalnego.",
                exception);
        }
        finally
        {
            _process.Dispose();
        }
    }

    public void Abort()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        TryKill();
        _process.Dispose();
        TryDeleteTemporaryFile();
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _stopped) != 0) return;
        try { Stop(); }
        catch (Exception exception)
        {
            DiagnosticLog.Error(
                "radio-recording",
                "Nie udało się zakończyć oryginalnego nagrania podczas zamykania toru radia.",
                exception);
        }
    }

    private static bool LooksLikeHls(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && (uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            || uri.Query.Contains("m3u8", StringComparison.OrdinalIgnoreCase));

    private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data)) return;
        lock (_errorGate)
        {
            if (_error.Length >= 2_000) return;
            if (_error.Length > 0) _error.Append(' ');
            _error.Append(e.Data.Trim());
        }
    }

    private string BuildFailureMessage(string message)
    {
        lock (_errorGate)
        {
            return _error.Length == 0 ? message : $"{message} {_error}";
        }
    }

    private void TryKill()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(true);
        }
        catch (Exception) { }
        try { _process.WaitForExit(3_000); } catch (Exception) { }
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
                $"Nie można usunąć niedokończonego pliku oryginalnego nagrania; błąd {exception.GetType().Name}.");
        }
    }
}
