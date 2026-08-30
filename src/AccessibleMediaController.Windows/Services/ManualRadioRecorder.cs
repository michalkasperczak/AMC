using System.IO;
using System.Runtime.InteropServices;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ManualRadioRecordingResult(
    bool Success,
    bool Cancelled,
    string? Path,
    string? Error);

/// <summary>
/// Records one station through a private, inaudible pipeline. The recording is
/// independent from the station heard in the main player and therefore remains
/// active when the user changes stations or leaves the player view.
/// </summary>
internal static class ManualRadioRecorder
{
    public static async Task<ManualRadioRecordingResult> RecordAsync(
        MediaItem station,
        string requestedFolder,
        string defaultFolder,
        string systemFallbackFolder,
        RadioRecordingFormat recordingFormat,
        int recordingBitrateKbps,
        RadioRecordingControl control,
        Action<string> recordingStarted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(recordingStarted);
        using var power = WindowsPowerRequest.TryCreate($"AMC nagrywa stację {station.Title}");
        using var output = new RadioMediaOutput(timeshiftMinutes: 1, audible: false);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var recordingFailed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStarted += (_, _) => started.TrySetResult(true);
        output.PlaybackFailed += (_, args) => failed.TrySetResult(args.Message);
        output.RecordingFailed += (_, _) => recordingFailed.TrySetResult(true);

        var privateStation = new MediaItem
        {
            Id = station.Id,
            Title = station.Title,
            Kind = MediaItemKind.Station,
            Source = station.Source,
            PublicUri = station.PublicUri,
            Codec = station.Codec,
            IsAvailable = true,
            IsInLibrary = true
        };
        output.Play(privateStation, TimeSpan.Zero, 0, 1d);

        try
        {
            var connection = await Task.WhenAny(
                started.Task,
                failed.Task,
                Task.Delay(TimeSpan.FromSeconds(30), cancellationToken)).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return new ManualRadioRecordingResult(false, true, null, null);
            if (connection != started.Task)
            {
                var error = connection == failed.Task
                    ? await failed.Task.ConfigureAwait(false)
                    : "Przekroczono czas łączenia ze stacją";
                return new ManualRadioRecordingResult(false, false, null, error);
            }

            var folderResolution = RadioRecordingFolderResolver.Resolve(
                requestedFolder,
                defaultFolder,
                systemFallbackFolder);
            if (folderResolution.UsedFallback)
            {
                DiagnosticLog.Warning(
                    "radio-recording",
                    $"Folder ręcznego nagrania jest niedostępny; użyto folderu zastępczego. Błąd {folderResolution.ErrorType}.");
            }
            var path = output.StartRecording(
                folderResolution.Path,
                recordingFormat,
                recordingBitrateKbps);
            control.Attach(output);
            recordingStarted(path);

            var finished = await Task.WhenAny(
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
                recordingFailed.Task).ConfigureAwait(false);
            if (finished == recordingFailed.Task)
            {
                output.StopRecording();
                return new ManualRadioRecordingResult(
                    false,
                    false,
                    null,
                    "Koder nagrania przerwał zapis");
            }

            var savedPath = output.StopRecording();
            return new ManualRadioRecordingResult(
                savedPath is not null,
                true,
                savedPath,
                savedPath is null ? "Nie utworzono pliku nagrania" : null);
        }
        catch (OperationCanceledException)
        {
            var savedPath = output.StopRecording();
            return new ManualRadioRecordingResult(
                savedPath is not null,
                true,
                savedPath,
                savedPath is null ? "Nie utworzono pliku nagrania" : null);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or TimeoutException
            or COMException)
        {
            try { output.StopRecording(); } catch (Exception) { }
            return new ManualRadioRecordingResult(false, false, null, exception.Message);
        }
        finally
        {
            control.Detach(output);
            output.Stop();
        }
    }
}
