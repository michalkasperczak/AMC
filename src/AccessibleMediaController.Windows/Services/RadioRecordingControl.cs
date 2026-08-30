namespace AccessibleMediaController.Windows.Services;

internal sealed record RadioRecordingPauseMarker(
    TimeSpan Position,
    DateTime CreatedUtc,
    string Name);

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

/// <summary>
/// Thread-safe bridge between the WPF command and a private recording output.
/// The output may be created and destroyed on a worker thread, while pause is
/// requested from the UI thread.
/// </summary>
internal sealed class RadioRecordingControl
{
    private readonly object _gate = new();
    private readonly List<RadioRecordingPauseMarker> _markers = [];
    private RadioMediaOutput? _output;

    public bool IsReady
    {
        get
        {
            lock (_gate) return _output?.IsRecording == true;
        }
    }

    public bool CanPause
    {
        get
        {
            lock (_gate) return _output?.CanPauseRecording == true;
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_gate) return _output?.IsRecordingPaused == true;
        }
    }

    public IReadOnlyList<RadioRecordingPauseMarker> Markers
    {
        get
        {
            lock (_gate) return _markers.ToArray();
        }
    }

    internal void Attach(RadioMediaOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (_gate) _output = output;
    }

    internal void Detach(RadioMediaOutput output)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_output, output)) _output = null;
        }
    }

    public RadioRecordingPauseChange SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_output?.IsRecording != true)
            {
                return new RadioRecordingPauseChange(
                    RadioRecordingPauseChangeKind.NotReady,
                    TimeSpan.Zero);
            }
            if (!_output.CanPauseRecording)
            {
                return new RadioRecordingPauseChange(
                    RadioRecordingPauseChangeKind.Unsupported,
                    TimeSpan.Zero);
            }
            if (_output.IsRecordingPaused == paused)
            {
                return new RadioRecordingPauseChange(
                    paused
                        ? RadioRecordingPauseChangeKind.Paused
                        : RadioRecordingPauseChangeKind.Resumed,
                    _output.RecordingDuration);
            }

            try
            {
                if (paused)
                {
                    _output.PauseRecording();
                    var position = _output.RecordingDuration;
                    _markers.Add(new RadioRecordingPauseMarker(
                        position,
                        DateTime.UtcNow,
                        $"Pauza {_markers.Count + 1}"));
                    return new RadioRecordingPauseChange(
                        RadioRecordingPauseChangeKind.Paused,
                        position);
                }

                _output.ResumeRecording();
                return new RadioRecordingPauseChange(
                    RadioRecordingPauseChangeKind.Resumed,
                    _output.RecordingDuration);
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
}
