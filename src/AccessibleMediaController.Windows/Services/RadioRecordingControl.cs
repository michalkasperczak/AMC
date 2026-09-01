using System.IO;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

internal sealed record RadioRecordingBookmarkMarker(
    string Path,
    TimeSpan Position,
    DateTime CreatedUtc,
    string Name);

internal sealed record RadioRecordingBookmarkTarget(
    string Path,
    TimeSpan Position,
    DateTime CreatedUtc);

internal enum RadioRecordingBookmarkChangeKind
{
    Added,
    NameChanged,
    Duplicate
}

internal sealed record RadioRecordingBookmarkChange(
    RadioRecordingBookmarkChangeKind Kind,
    RadioRecordingBookmarkMarker Marker);

internal enum RadioRecordingSplitChangeKind
{
    Split,
    StopRequested,
    NotReady,
    TooSoon,
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
    bool TryGetRecentAudio(TimeSpan duration, out RadioAudioSnapshot? snapshot);
    string StartRecording(
        string folder,
        RadioRecordingFormat format,
        int bitrateKbps,
        string? preferredBaseName = null);
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
    public bool TryGetRecentAudio(TimeSpan duration, out RadioAudioSnapshot? snapshot) =>
        Output.TryGetRecentCapturedAudio(duration, out snapshot);
    public string StartRecording(
        string folder,
        RadioRecordingFormat format,
        int bitrateKbps,
        string? preferredBaseName = null) =>
        Output.StartRecording(folder, format, bitrateKbps, preferredBaseName);
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
    private static readonly TimeSpan BookmarkDuplicateTolerance = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MinimumSplitSegmentDuration = TimeSpan.FromSeconds(5);
    private readonly List<RadioRecordingBookmarkMarker> _markers = [];
    private readonly List<string> _completedPaths = [];
    private IRadioRecordingBackend? _backend;
    private string? _folder;
    private RadioRecordingFormat _format;
    private int _bitrateKbps;
    private string? _currentPath;
    private Func<int, string?>? _fileBaseNameFactory;
    private int _partNumber;
    private int _pauseMarkerCount;
    private bool _stopRequested;

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

    public IReadOnlyList<RadioRecordingBookmarkMarker> Markers
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
        string currentPath,
        Func<int, string?>? fileBaseNameFactory = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        Attach(
            new RadioMediaRecordingBackend(output),
            folder,
            format,
            bitrateKbps,
            currentPath,
            fileBaseNameFactory);
    }

    internal void Attach(
        IRadioRecordingBackend backend,
        string folder,
        RadioRecordingFormat format,
        int bitrateKbps,
        string currentPath,
        Func<int, string?>? fileBaseNameFactory = null)
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
            _fileBaseNameFactory = fileBaseNameFactory;
            _partNumber = 1;
            _pauseMarkerCount = 0;
            _stopRequested = false;
        }
    }

    public bool TryGetRecentAudio(TimeSpan duration, out RadioAudioSnapshot? snapshot)
    {
        lock (_gate)
        {
            if (_backend?.IsRecording != true)
            {
                snapshot = null;
                return false;
            }
            return _backend.TryGetRecentAudio(duration, out snapshot);
        }
    }

    public RadioRecordingBookmarkTarget? CaptureBookmarkTarget()
    {
        if (!_operationGate.Wait(0)) return null;
        try
        {
            lock (_gate)
            {
                if (_backend?.IsRecording != true || string.IsNullOrWhiteSpace(_currentPath))
                    return null;
                var position = _backend.RecordingDuration;
                return new RadioRecordingBookmarkTarget(
                    _currentPath,
                    position < TimeSpan.Zero ? TimeSpan.Zero : position,
                    DateTime.UtcNow);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or ObjectDisposedException)
        {
            return null;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public RadioRecordingBookmarkChange AddBookmark(
        RadioRecordingBookmarkTarget target,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Path);
        var normalizedName = NormalizeBookmarkName(name);
        var position = target.Position < TimeSpan.Zero ? TimeSpan.Zero : target.Position;
        lock (_gate)
        {
            var existingIndex = _markers.FindIndex(marker =>
                string.Equals(marker.Path, target.Path, StringComparison.OrdinalIgnoreCase)
                && Math.Abs((marker.Position - position).Ticks) <= BookmarkDuplicateTolerance.Ticks);
            if (existingIndex >= 0)
            {
                var existing = _markers[existingIndex];
                if (normalizedName.Length > 0
                    && !string.Equals(existing.Name, normalizedName, StringComparison.CurrentCulture))
                {
                    var renamed = existing with { Name = normalizedName };
                    _markers[existingIndex] = renamed;
                    return new RadioRecordingBookmarkChange(
                        RadioRecordingBookmarkChangeKind.NameChanged,
                        renamed);
                }
                return new RadioRecordingBookmarkChange(
                    RadioRecordingBookmarkChangeKind.Duplicate,
                    existing);
            }

            var marker = new RadioRecordingBookmarkMarker(
                target.Path,
                position,
                target.CreatedUtc.ToUniversalTime(),
                normalizedName);
            _markers.Add(marker);
            return new RadioRecordingBookmarkChange(
                RadioRecordingBookmarkChangeKind.Added,
                marker);
        }
    }

    public void RequestStop()
    {
        lock (_gate) _stopRequested = true;
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
            Func<int, string?>? fileBaseNameFactory;
            int nextPartNumber;
            lock (_gate)
            {
                backend = _backend;
                folder = _folder;
                currentPath = _currentPath;
                format = _format;
                bitrateKbps = _bitrateKbps;
                fileBaseNameFactory = _fileBaseNameFactory;
                nextPartNumber = _partNumber + 1;
                if (_stopRequested)
                    return new RadioRecordingSplitChange(RadioRecordingSplitChangeKind.StopRequested);
            }
            if (backend?.IsRecording != true
                || string.IsNullOrWhiteSpace(folder)
                || string.IsNullOrWhiteSpace(currentPath))
            {
                return new RadioRecordingSplitChange(RadioRecordingSplitChangeKind.NotReady);
            }
            if (backend.RecordingDuration < MinimumSplitSegmentDuration)
            {
                return new RadioRecordingSplitChange(RadioRecordingSplitChangeKind.TooSoon);
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
                    if (_stopRequested)
                    {
                        return new RadioRecordingSplitChange(
                            RadioRecordingSplitChangeKind.StopRequested,
                            completedPath);
                    }
                }
                var nextPath = backend.StartRecording(
                    folder,
                    format,
                    bitrateKbps,
                    fileBaseNameFactory?.Invoke(nextPartNumber));
                if (wasPaused && backend.CanPauseRecording) backend.PauseRecording();
                lock (_gate)
                {
                    _currentPath = nextPath;
                    _partNumber = nextPartNumber;
                }
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
                        _pauseMarkerCount++;
                        _markers.Add(new RadioRecordingBookmarkMarker(
                            _currentPath ?? string.Empty,
                            position,
                            DateTime.UtcNow,
                            $"Pauza {_pauseMarkerCount}"));
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
            _stopRequested = true;
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

    private static string NormalizeBookmarkName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var normalized = string.Join(' ', name.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 200 ? normalized : normalized[..200].TrimEnd();
    }
}
