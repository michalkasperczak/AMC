using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Extracts only the audio track from HLS broadcasts which also carry video.
/// The process is optional: ordinary radio keeps using the in-process decoders.
/// </summary>
internal sealed class FfmpegRadioWaveProvider : IWaveProvider, IDisposable
{
    private const int OutputSampleRate = 48_000;
    private const int OutputChannels = 2;
    private readonly Process _process;
    private readonly Stream _audio;
    private readonly CancellationTokenRegistration _cancellation;
    private int _disposed;

    private FfmpegRadioWaveProvider(Process process, CancellationToken cancellationToken)
    {
        _process = process;
        _audio = process.StandardOutput.BaseStream;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(OutputSampleRate, OutputChannels);
        _process.BeginErrorReadLine();
        _cancellation = cancellationToken.Register(
            static state => ((FfmpegRadioWaveProvider)state!).Dispose(),
            this);
    }

    public WaveFormat WaveFormat { get; }

    public static Task<FfmpegRadioWaveProvider?> TryOpenAsync(
        string source,
        CancellationToken cancellationToken,
        bool forceLiveHls = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executable = FindExecutable();
        if (executable is null) return Task.FromResult<FfmpegRadioWaveProvider?>(null);

        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error",
            "-rw_timeout", "15000000",
        })
        {
            start.ArgumentList.Add(argument);
        }
        if (forceLiveHls || RadioStreamResolver.IsHlsSource(source))
        {
            // HLS manifests commonly expose several already completed
            // segments.  Without these input options FFmpeg can emit that
            // backlog in a burst, so a two-second recording may unexpectedly
            // contain tens of seconds from before the command was invoked.
            start.ArgumentList.Add("-live_start_index");
            start.ArgumentList.Add("-1");
            start.ArgumentList.Add("-readrate");
            start.ArgumentList.Add("1");
        }
        foreach (var argument in new[]
        {
            "-i", source,
            "-map", "0:a:0", "-vn",
            "-f", "f32le", "-acodec", "pcm_f32le",
            "-ar", OutputSampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-ac", OutputChannels.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "pipe:1"
        })
        {
            start.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = Process.Start(start);
            if (process is null) return Task.FromResult<FfmpegRadioWaveProvider?>(null);
            return Task.FromResult<FfmpegRadioWaveProvider?>(
                new FfmpegRadioWaveProvider(process, cancellationToken));
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or IOException)
        {
            process?.Dispose();
            return Task.FromResult<FfmpegRadioWaveProvider?>(null);
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _audio.Read(buffer, offset, count);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try { _audio.Dispose(); } catch (Exception) { }
        try
        {
            if (!_process.HasExited) _process.Kill(true);
        }
        catch (Exception) { }
        try { _process.Dispose(); } catch (Exception) { }
        _cancellation.Dispose();
    }

    internal static string? FindExecutable() => EnumerateExecutableCandidates().FirstOrDefault();

    internal static IEnumerable<string> EnumerateExecutableCandidates()
    {
        var fileName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static bool TryAddExisting(string? candidate, ISet<string> known, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate)) return false;
            try
            {
                path = Path.GetFullPath(candidate);
                return known.Add(path);
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                return false;
            }
        }

        var managed = FfmpegComponentManager.FindInstalledExecutable();
        if (TryAddExisting(managed, known, out var managedPath)) yield return managedPath;

        var bundled = Path.Combine(AppContext.BaseDirectory, fileName);
        if (TryAddExisting(bundled, known, out var bundledPath)) yield return bundledPath;

        var configured = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (TryAddExisting(configured, known, out var configuredPath)) yield return configuredPath;

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? candidate = null;
            try
            {
                candidate = Path.Combine(directory, fileName);
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
                // Ignore malformed PATH entries and continue with the others.
            }
            if (TryAddExisting(candidate, known, out var candidatePath)) yield return candidatePath;
        }
    }
}

internal sealed record RadioAudioMetadata(
    int? BitrateKbps,
    int? SampleRateHz,
    string? Codec,
    bool IsBitrateEstimated = false,
    double? SegmentDurationSeconds = null,
    string? SegmentUri = null);

internal static class RadioAudioMetadataRules
{
    // Internet radio streams above 10 Mb/s are not credible here. This guard
    // also repairs values written by alpha.125-alpha.127, where the BASS
    // sample-rate attribute could be mistaken for bitrate.
    internal const int MaximumCredibleBitrateKbps = 10_000;

    internal static int? NormalizeBitrateKbps(int? bitrateKbps) =>
        bitrateKbps is > 0 and <= MaximumCredibleBitrateKbps
            ? bitrateKbps
            : null;
}
