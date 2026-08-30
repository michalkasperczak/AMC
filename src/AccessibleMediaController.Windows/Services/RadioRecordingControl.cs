using System.IO;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

internal sealed record RadioRecordingPauseMarker(
    string Path,
    TimeSpan Position,
    DateTime CreatedUtc,
    string Name);

internal enum RadioRecordingSplitChangeKind
{
    Split,
    NotReady,
    Failed
}

internal sealed record RadioRecordingSplitChange(
    RadioRecordingSplitChangeKind Kind,
    string? CompletedPath = null,
    string? CurrentPath = null,
    string? Error = null);

internal enum RadioRecordingPauseChangeKind
{
    Paused,
    Resumed,
    NotReady,
    Unsupported,
    Failed
}

internal sealed record RadioRecordingPauseChange(
    RadioRecordingPauseChangeKind Kind,
    TimeSpan Position,
    string? Error = null);

internal interface IRadioRecordingBackend
{
    bool IsRecording { get; }
    bool CanPauseRecording { get; }
    bool IsRecordingPaused { get; }
    TimeSpan RecordingDuration { get; }
    string StartRecording(string folder, RadioRecordingFormat format, int bitrateKbps);
    string? StopRecording();
    void PauseRecording();
    void ResumeRecording();
}

internal sealed class RadioMediaRecordingBackend(RadioMediaOutput output) : IRadioRecordingBackend
{
    public RadioMediaOutput Output { get; } = output;
    public bool IsRecording => Output.IsRecording;
    public bool CanPauseRecording => Output.CanPauseRecording;
    public bool IsRecordingPaused => Output.IsRecordingPaused;
    public TimeSpan RecordingDuration => Output.RecordingDuration;
    public string StartRecording(string folder, RadioRecordingFormat format, int bitrateKbps) =>
        Output.StartRecording(folder, format, bitrateKbps);
    public string? StopRecording() => Output.StopRecording();
    public void PauseRecording() => Output.PauseRecording();
    public void ResumeRecording() => Output.ResumeRecording();
}

/// <summary>
/// Thread-safe bridge between the WPF command and a private recording output.
/// The output may be created and destroyed on a worker thread, while pause is
/// requested from the UI thread.
/// </summary>
internal sealed class RadioRecordingControl
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly List<RadioRecordingPauseMarker> _markers = [];
    private readonly List<string> _completedPaths = [];
    private IRadioRecordingBackend? _backend;
    private string? _folder;
    private RadioRecordingFormat _format;
    private int _bitrateKbps;
    private string? _currentPath;

    public bool IsReady
    {
        get
        {
            lock (_gate) return _backend?.IsRecording == true;
        }
    }

    public bool CanPause
    {
        get
        {
            lock (_gate) return _backend?.CanPauseRecording == true;
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_gate) return _backend?.IsRecordingPaused == true;
        }
    }

    public IReadOnlyList<RadioRecordingPauseMarker> Markers
    {
        get
        {
            lock (_gate) return _markers.ToArray();
        }
    }

    public IReadOnlyList<string> CompletedPaths
    {
        get
        {
            lock (_gate) return _completedPaths.ToArray();
        }
    }

    public string? CurrentPath
    {
        get
        {
            lock (_gate) return _currentPath;
        }
    }

    internal void Attach(
        RadioMediaOutput output,
        string folder,
        RadioRecordingFormat format,
        int bitrateKbps,
        string currentPath)
    {
        ArgumentNullException.ThrowIfNull(output);
        Attach(new RadioMediaRecordingBackend(output), folder, format, bitrateKbps, currentPath);
    }

    internal void Attach(
        IRadioRecordingBackend backend,
        string folder,
        RadioRecordingFormat format,
        int bitrateKbps,
        string currentPath)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPath);
        lock (_gate)
        {
            _backend = backend;
            _folder = folder;
            _format = format;
            _bitrateKbps = bitrateKbps;
            _currentPath = currentPath;
        }
    }

    internal void Detach(RadioMediaOutput output)
    {
        lock (_gate)
        {
            if (_backend is RadioMediaRecordingBackend adapter
                && ReferenceEquals(adapter.Output, output)) _backend = null;
        }
    }

    internal void Detach(IRadioRecordingBackend backend)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_backend, backend)) _backend = null;
        }
    }

    internal string? StopCurrentSegment(RadioMediaOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _operationGate.Wait();
        try
        {
            IRadioRecordingBackend? backend;
            lock (_gate)
            {
                backend = _backend is RadioMediaRecordingBackend adapter
                    && ReferenceEquals(adapter.Output, output)
                        ? _backend
                        : null;
            }
            return StopCurrentSegmentCore(backend);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    internal string? StopCurrentSegment(IRadioRecordingBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _operationGate.Wait();
        try
        {
            return StopCurrentSegmentCore(backend);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public RadioRecordingSplitChange SplitRecording()
    {
        _operationGate.Wait();
        try
        {
            IRadioRecordingBackend? backend;
            string? folder;
            string? currentPath;
            RadioRecordingFormat format;
            int bitrateKbps;
            lock (_gate)
            {
                backend = _backend;
                folder = _folder;
                currentPath = _currentPath;
                format = _format;
                bitrateKbps = _bitrateKbps;
            }
            if (backend?.IsRecording != true
                || string.IsNullOrWhiteSpace(folder)
                || string.IsNullOrWhiteSpace(currentPath))
            {
                return new RadioRecordingSplitChange(RadioRecordingSplitChangeKind.NotReady);
            }
            var wasPaused = backend.IsRecordingPaused;
            string? completedPath = null;
            try
            {
                completedPath = backend.StopRecording();
                lock (_gate)
                {
                    AddCompletedPath(completedPath);
                    _currentPath = null;
                }
                var nextPath = backend.StartRecording(folder, format, bitrateKbps);
                if (wasPaused && backend.CanPauseRecording) backend.PauseRecording();
                lock (_gate) _currentPath = nextPath;
                return new RadioRecordingSplitChange(
                    RadioRecordingSplitChangeKind.Split,
                    completedPath,
                    nextPath);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException
                or TimeoutException
                or ObjectDisposedException)
            {
                return new RadioRecordingSplitChange(
                    RadioRecordingSplitChangeKind.Failed,
                    completedPath,
                    null,
                    exception.Message);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public RadioRecordingPauseChange SetPaused(bool paused)
    {
        if (!_operationGate.Wait(0))
        {
            return new RadioRecordingPauseChange(
                RadioRecordingPauseChangeKind.NotReady,
                TimeSpan.Zero);
        }
        try
        {
            lock (_gate)
            {
                if (_backend?.IsRecording != true)
                {
                    return new RadioRecordingPauseChange(
                        RadioRecordingPauseChangeKind.NotReady,
                        TimeSpan.Zero);
                }
                if (!_backend.CanPauseRecording)
                {
                    return new RadioRecordingPauseChange(
                        RadioRecordingPauseChangeKind.Unsupported,
                        TimeSpan.Zero);
                }
                if (_backend.IsRecordingPaused == paused)
                {
                    return new RadioRecordingPauseChange(
                        paused
                            ? RadioRecordingPauseChangeKind.Paused
                            : RadioRecordingPauseChangeKind.Resumed,
                        _backend.RecordingDuration);
                }

                try
                {
                    if (paused)
                    {
                        _backend.PauseRecording();
                        var position = _backend.RecordingDuration;
                        _markers.Add(new RadioRecordingPauseMarker(
                            _currentPath ?? string.Empty,
                            position,
                            DateTime.UtcNow,
                            $"Pauza {_markers.Count + 1}"));
                        return new RadioRecordingPauseChange(
                            RadioRecordingPauseChangeKind.Paused,
                            position);
                    }

                    _backend.ResumeRecording();
                    return new RadioRecordingPauseChange(
                        RadioRecordingPauseChangeKind.Resumed,
                        _backend.RecordingDuration);
                }
                catch (Exception exception) when (exception is InvalidOperationException
                    or NotSupportedException
                    or ObjectDisposedException)
                {
                    return new RadioRecordingPauseChange(
                        RadioRecordingPauseChangeKind.Failed,
                        TimeSpan.Zero,
                        exception.Message);
                }
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private string? StopCurrentSegmentCore(IRadioRecordingBackend? backend)
    {
        if (backend is null) return null;
        lock (_gate)
        {
            if (!ReferenceEquals(_backend, backend)) return null;
        }
        var path = backend.StopRecording();
        lock (_gate)
        {
            AddCompletedPath(path);
            _currentPath = null;
        }
        return path;
    }

    private void AddCompletedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || _completedPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        _completedPaths.Add(path);
    }
}
