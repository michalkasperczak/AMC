using System.Runtime.InteropServices;
using System.IO;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Win32.SafeHandles;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ScheduledRadioRecordingResult(
    bool Success,
    bool Cancelled,
    string? Path,
    string? Error);

/// <summary>
/// Records one scheduled station through a private, inaudible radio pipeline.
/// It never changes the station that the user is currently listening to.
/// </summary>
internal static class ScheduledRadioRecorder
{
    public static async Task<ScheduledRadioRecordingResult> RecordAsync(
        RadioRecordingScheduleSettings schedule,
        DateTime deadlineUtc,
        string defaultFolder,
        string systemFallbackFolder,
        RadioRecordingFormat recordingFormat,
        int recordingBitrateKbps,
        RadioRecordingControl control,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(control);
        deadlineUtc = deadlineUtc.Kind == DateTimeKind.Utc
            ? deadlineUtc
            : deadlineUtc.ToUniversalTime();
        using var power = WindowsPowerRequest.TryCreate(
            $"AMC nagrywa zaplanowaną stację {schedule.StationName}");

        string? lastError = null;
        while (!cancellationToken.IsCancellationRequested
               && deadlineUtc - DateTime.UtcNow > TimeSpan.FromSeconds(3))
        {
            using var output = new RadioMediaOutput(timeshiftMinutes: 1, audible: false);
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingFailed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            output.PlaybackStarted += (_, _) => started.TrySetResult(true);
            output.PlaybackFailed += (_, args) => failed.TrySetResult(args.Message);
            output.RecordingFailed += (_, _) => recordingFailed.TrySetResult(true);

            var item = new MediaItem
            {
                Id = string.IsNullOrWhiteSpace(schedule.StationId)
                    ? $"scheduled:{schedule.Id}"
                    : schedule.StationId,
                Title = schedule.StationName,
                Kind = MediaItemKind.Station,
                Source = schedule.StreamUrl,
                PublicUri = schedule.StreamUrl,
                IsAvailable = true,
                IsInLibrary = true
            };
            output.Play(item, TimeSpan.Zero, 0, 1d);

            var connectionTimeout = deadlineUtc - DateTime.UtcNow;
            if (connectionTimeout > TimeSpan.FromSeconds(30)) connectionTimeout = TimeSpan.FromSeconds(30);
            if (connectionTimeout <= TimeSpan.Zero) break;
            var timeoutTask = Task.Delay(connectionTimeout, cancellationToken);
            var connection = await Task.WhenAny(started.Task, failed.Task, timeoutTask).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return new ScheduledRadioRecordingResult(false, true, null, null);
            if (connection != started.Task)
            {
                lastError = connection == failed.Task
                    ? await failed.Task.ConfigureAwait(false)
                    : "Przekroczono czas łączenia ze stacją";
                output.Stop();
                var retryDelay = deadlineUtc - DateTime.UtcNow > TimeSpan.FromSeconds(8)
                    ? TimeSpan.FromSeconds(3)
                    : TimeSpan.Zero;
                if (retryDelay > TimeSpan.Zero)
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string? path = null;
            try
            {
                var folderResolution = RadioRecordingFolderResolver.Resolve(
                    schedule.OutputFolder,
                    defaultFolder,
                    systemFallbackFolder);
                if (folderResolution.UsedFallback)
                {
                    DiagnosticLog.Warning(
                        "radio-schedule",
                        $"Skonfigurowany folder nagrania jest niedostępny; użyto folderu zastępczego. Błąd {folderResolution.ErrorType}.");
                }
                path = output.StartRecording(
                    folderResolution.Path,
                    recordingFormat,
                    recordingBitrateKbps);
                control.Attach(
                    output,
                    folderResolution.Path,
                    recordingFormat,
                    recordingBitrateKbps,
                    path);
                while (true)
                {
                    var remaining = deadlineUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        var expiredPath = control.StopCurrentSegment(output);
                        return expiredPath is null
                            ? new ScheduledRadioRecordingResult(false, false, null, "Okno nagrywania już się zakończyło")
                            : new ScheduledRadioRecordingResult(true, false, expiredPath, null);
                    }

                    var wait = CalculateNextWait(remaining, schedule.SegmentMinutes);
                    var finished = await Task.WhenAny(
                        Task.Delay(wait, cancellationToken),
                        recordingFailed.Task).ConfigureAwait(false);
                    if (finished == recordingFailed.Task)
                    {
                        var failedPath = control.StopCurrentSegment(output);
                        return new ScheduledRadioRecordingResult(
                            false,
                            false,
                            failedPath,
                            "Koder nagrania przerwał zapis");
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        var cancelledPath = control.StopCurrentSegment(output);
                        var latestPath = cancelledPath ?? control.CompletedPaths.LastOrDefault();
                        return new ScheduledRadioRecordingResult(
                            latestPath is not null,
                            true,
                            latestPath,
                            null);
                    }

                    var timeLeft = deadlineUtc - DateTime.UtcNow;
                    if (ShouldStartNextSegment(wait, timeLeft, schedule.SegmentMinutes))
                    {
                        var split = control.SplitRecording();
                        if (split.Kind != RadioRecordingSplitChangeKind.Split)
                        {
                            var lastPath = control.StopCurrentSegment(output);
                            return new ScheduledRadioRecordingResult(
                                false,
                                false,
                                lastPath ?? split.CompletedPath,
                                split.Error ?? "Nie udało się rozpocząć następnej części nagrania");
                        }
                        path = split.CurrentPath;
                        continue;
                    }

                    var savedPath = control.StopCurrentSegment(output);
                    return savedPath is null
                        ? new ScheduledRadioRecordingResult(false, false, null, "Nie utworzono pliku nagrania")
                        : new ScheduledRadioRecordingResult(true, false, savedPath, null);
                }
            }
            catch (OperationCanceledException)
            {
                var savedPath = control.StopCurrentSegment(output);
                var latestPath = savedPath ?? control.CompletedPaths.LastOrDefault();
                return new ScheduledRadioRecordingResult(
                    latestPath is not null,
                    true,
                    latestPath,
                    null);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException
                or TimeoutException
                or COMException)
            {
                try
                {
                    _ = control.StopCurrentSegment(output);
                }
                catch (Exception) { }
                return new ScheduledRadioRecordingResult(false, false, path, exception.Message);
            }
            finally
            {
                control.Detach(output);
                output.Stop();
            }
        }

        return cancellationToken.IsCancellationRequested
            ? new ScheduledRadioRecordingResult(false, true, null, null)
            : new ScheduledRadioRecordingResult(
                false,
                false,
                null,
                lastError ?? "Nie udało się połączyć ze stacją przed końcem zaplanowanego czasu");
    }

    internal static TimeSpan CalculateNextWait(TimeSpan remaining, int segmentMinutes)
    {
        if (remaining <= TimeSpan.Zero) return TimeSpan.Zero;
        if (segmentMinutes <= 0) return remaining;
        var segment = TimeSpan.FromMinutes(segmentMinutes);
        return segment < remaining ? segment : remaining;
    }

    internal static bool ShouldStartNextSegment(
        TimeSpan completedWait,
        TimeSpan timeLeft,
        int segmentMinutes) =>
        segmentMinutes > 0
        && completedWait == TimeSpan.FromMinutes(segmentMinutes)
        && timeLeft > TimeSpan.FromSeconds(3);
}

internal sealed class WindowsPowerRequest : IDisposable
{
    private readonly SafeFileHandle _handle;
    private bool _active;

    private WindowsPowerRequest(SafeFileHandle handle)
    {
        _handle = handle;
        _active = PowerSetRequest(handle, PowerRequestType.SystemRequired);
    }

    public static WindowsPowerRequest? TryCreate(string reason)
    {
        try
        {
            var context = new ReasonContext
            {
                Version = 0,
                Flags = 1,
                SimpleReasonString = reason
            };
            var handle = PowerCreateRequest(ref context);
            return handle.IsInvalid ? null : new WindowsPowerRequest(handle);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            DiagnosticLog.Warning("radio-schedule", "Windows nie udostępnia blokady uśpienia dla nagrywania.");
            return null;
        }
    }

    public void Dispose()
    {
        if (_active)
        {
            _ = PowerClearRequest(_handle, PowerRequestType.SystemRequired);
            _active = false;
        }
        _handle.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)] public string SimpleReasonString;
    }

    private enum PowerRequestType
    {
        DisplayRequired,
        SystemRequired,
        AwayModeRequired,
        ExecutionRequired
    }

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle PowerCreateRequest(ref ReasonContext context);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool PowerSetRequest(SafeFileHandle powerRequest, PowerRequestType requestType);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool PowerClearRequest(SafeFileHandle powerRequest, PowerRequestType requestType);
}
