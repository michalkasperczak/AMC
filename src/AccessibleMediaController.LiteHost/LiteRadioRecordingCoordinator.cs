using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Prowadzi reczne nagrania radia uruchomione przez interfejs wxPython.
/// Kazde nagranie ma prywatny, nieslyszalny tor <see cref="RadioMediaOutput"/>,
/// wiec zmiana odtwarzanej stacji ani wyjscie z odtwarzacza nie przerywa pliku.
/// Ten sam tor obsluguje zwykle strumienie HTTP i transmisje YouTube na zywo.
/// </summary>
internal sealed class LiteRadioRecordingCoordinator : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ActiveRecording> _active = new(StringComparer.Ordinal);
    private readonly List<CompletedRecording> _history = [];
    private bool _disposed;

    public object Toggle(JsonElement args, LiteEventSink events)
    {
        var request = ReadRequest(args);
        ActiveRecording[] matches;
        lock (_gate)
        {
            ThrowIfDisposed();
            matches = _active.Values
                .Where(active => SameStation(active, request.StationId, request.StreamUrl))
                .ToArray();
        }

        if (matches.Length > 0)
        {
            foreach (var active in matches) RequestStop(active);
            return new
            {
                action = "stopping",
                stationId = request.StationId,
                stationName = request.StationName,
                count = matches.Length
            };
        }

        var id = Guid.NewGuid().ToString("N");
        var cancellation = new CancellationTokenSource();
        var control = new RadioRecordingControl();
        var recording = new ActiveRecording(
            id,
            request.StationId,
            request.StationName,
            request.StreamUrl,
            request.OutputFolder,
            request.DefaultFolder,
            request.SystemFallbackFolder,
            request.Format,
            request.BitrateKbps,
            DateTime.UtcNow,
            cancellation,
            control);

        lock (_gate)
        {
            ThrowIfDisposed();
            _active.Add(id, recording);
        }

        var station = new MediaItem
        {
            Id = request.StationId,
            Title = request.StationName,
            Kind = MediaItemKind.Station,
            Source = request.StreamUrl,
            PublicUri = request.StreamUrl,
            IsAvailable = true,
            IsInLibrary = true
        };
        var task = Task.Run(() => ManualRadioRecorder.RecordAsync(
            station,
            request.OutputFolder,
            request.DefaultFolder,
            request.SystemFallbackFolder,
            request.Format,
            request.BitrateKbps,
            control,
            path => RecordingStarted(recording, path, events),
            cancellation.Token));
        recording.Task = task;
        _ = CompleteAsync(recording, task, events);

        return new
        {
            action = "starting",
            recordingId = id,
            stationId = request.StationId,
            stationName = request.StationName,
            format = request.Format.ToString(),
            bitrateKbps = request.BitrateKbps
        };
    }

    public object TogglePause(JsonElement args)
    {
        var recordings = FindForStation(args);
        if (recordings.Length == 0) return new { state = "notRecording", count = 0 };

        var ready = recordings.Where(active => active.Control.IsReady).ToArray();
        if (ready.Length == 0) return new { state = "starting", count = recordings.Length };
        var pausable = ready.Where(active => active.Control.CanPause).ToArray();
        if (pausable.Length == 0)
        {
            return new
            {
                state = "unsupported",
                count = ready.Length,
                reason = "Pauza nie jest dostępna przy zapisie oryginalnego strumienia bez konwersji."
            };
        }

        var pause = pausable.Any(active => !active.Control.IsPaused);
        var changes = pausable.Select(active => active.Control.SetPaused(pause)).ToArray();
        var failure = changes.FirstOrDefault(change =>
            change.Kind == RadioRecordingPauseChangeKind.Failed);
        if (failure is not null)
            throw new LiteRequestException(failure.Error ?? "Nie udało się zmienić pauzy nagrania.");
        if (changes.Any(change => change.Kind == RadioRecordingPauseChangeKind.NotReady))
            return new { state = "busy", count = pausable.Length };

        var position = changes
            .Where(change => change.Kind is RadioRecordingPauseChangeKind.Paused
                or RadioRecordingPauseChangeKind.Resumed)
            .Select(change => change.Position.TotalSeconds)
            .DefaultIfEmpty(0d)
            .Min();
        return new
        {
            state = pause ? "paused" : "recording",
            paused = pause,
            positionSeconds = position,
            count = pausable.Length,
            unpausableCount = ready.Length - pausable.Length
        };
    }

    public object Split(JsonElement args)
    {
        var recordings = FindForStation(args);
        if (recordings.Length == 0) return new { state = "notRecording", count = 0 };
        var ready = recordings.Where(active => active.Control.IsReady).ToArray();
        if (ready.Length == 0) return new { state = "starting", count = recordings.Length };

        var changes = ready.Select(active => (Active: active, Change: active.Control.SplitRecording())).ToArray();
        var completed = changes
            .Where(result => result.Change.Kind == RadioRecordingSplitChangeKind.Split)
            .ToArray();
        foreach (var result in completed)
        {
            lock (_gate) result.Active.Path = result.Change.CurrentPath;
        }
        if (completed.Length > 0)
        {
            return new
            {
                state = "split",
                count = completed.Length,
                currentPath = completed[0].Change.CurrentPath,
                completedPath = completed[0].Change.CompletedPath,
                completedFileCount = completed[0].Active.Control.CompletedPaths.Count
            };
        }

        var first = changes[0].Change;
        return first.Kind switch
        {
            RadioRecordingSplitChangeKind.TooSoon => new { state = "tooSoon", count = ready.Length },
            RadioRecordingSplitChangeKind.StopRequested => new { state = "stopping", count = ready.Length },
            RadioRecordingSplitChangeKind.NotReady => new { state = "busy", count = ready.Length },
            _ => throw new LiteRequestException(first.Error ?? "Nie udało się rozpocząć nowej części nagrania.")
        };
    }

    public object StopAll()
    {
        ActiveRecording[] recordings;
        lock (_gate) recordings = _active.Values.ToArray();
        foreach (var active in recordings) RequestStop(active);
        return new { stopping = recordings.Length };
    }

    public object Status()
    {
        ActiveRecording[] recordings;
        lock (_gate) recordings = _active.Values.ToArray();
        return new
        {
            count = recordings.Length,
            recordings = recordings.Select(Payload).ToArray()
        };
    }

    public object History()
    {
        CompletedRecording[] recordings;
        lock (_gate)
        {
            ThrowIfDisposed();
            recordings = _history.ToArray();
        }
        return new
        {
            // Produkcyjny profil WPF pozostaje tylko do odczytu: stary proces
            // nie zna blokady LiteHost. Frontend moze wiec uczciwie powiedziec,
            // ze te najnowsze wpisy zyja do zamkniecia biezacego procesu.
            persistent = false,
            recordings = recordings.Select(HistoryPayload).ToArray()
        };
    }

    private ActiveRecording[] FindForStation(JsonElement args)
    {
        var stationId = LiteArgs.ReadText(args, "stationId");
        var streamUrl = LiteArgs.ReadText(args, "url");
        if (stationId is null && streamUrl is null)
            throw new LiteRequestException("Wskaż stację, której nagranie ma zostać zmienione.");
        lock (_gate)
        {
            ThrowIfDisposed();
            return _active.Values
                .Where(active => SameStation(active, stationId, streamUrl))
                .ToArray();
        }
    }

    private static bool SameStation(ActiveRecording active, string? stationId, string? streamUrl) =>
        stationId is not null && string.Equals(active.StationId, stationId, StringComparison.Ordinal)
        || streamUrl is not null && string.Equals(active.StreamUrl, streamUrl, StringComparison.OrdinalIgnoreCase);

    private void RecordingStarted(ActiveRecording active, string path, LiteEventSink events)
    {
        lock (_gate)
        {
            if (!_active.ContainsKey(active.Id)) return;
            active.Path = path;
            active.StartedUtc = DateTime.UtcNow;
        }
        events.Publish("radio.recordingStarted", Payload(active));
    }

    private async Task CompleteAsync(
        ActiveRecording active,
        Task<ManualRadioRecordingResult> task,
        LiteEventSink events)
    {
        ManualRadioRecordingResult result;
        try
        {
            result = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new ManualRadioRecordingResult(false, true, null, null);
        }
        catch (Exception exception)
        {
            result = new ManualRadioRecordingResult(false, false, null, exception.Message);
        }

        var paths = active.Control.CompletedPaths;
        var path = result.Path ?? paths.LastOrDefault() ?? active.Path;
        var outcome = (result.Success, result.Cancelled, paths.Count) switch
        {
            (true, _, _) => "Completed",
            (false, true, _) when string.IsNullOrWhiteSpace(result.Error) => "Stopped",
            (false, _, > 0) => "Interrupted",
            _ => "Failed"
        };
        var completed = new CompletedRecording(
            active.Id,
            active.StationId,
            active.StationName,
            path ?? string.Empty,
            outcome,
            result.Error?.Trim() ?? string.Empty,
            (active.StartedUtc ?? active.RequestedUtc).Ticks,
            DateTime.UtcNow.Ticks,
            paths.Count);
        lock (_gate)
        {
            _active.Remove(active.Id);
            _history.Insert(0, completed);
            if (_history.Count > 1_000)
                _history.RemoveRange(1_000, _history.Count - 1_000);
        }
        active.Cancellation.Dispose();
        var payload = new
        {
            recordingId = active.Id,
            stationId = active.StationId,
            stationName = active.StationName,
            path,
            savedFileCount = paths.Count,
            cancelled = result.Cancelled,
            success = result.Success,
            error = result.Error,
            outcome,
            startedUtcTicks = completed.StartedUtcTicks,
            finishedUtcTicks = completed.FinishedUtcTicks
        };
        events.Publish(result.Success || result.Cancelled && paths.Count > 0
            ? "radio.recordingFinished"
            : result.Cancelled && string.IsNullOrWhiteSpace(result.Error)
                ? "radio.recordingStopped"
                : "radio.recordingFailed", payload);
    }

    private static object Payload(ActiveRecording active) => new
    {
        recordingId = active.Id,
        stationId = active.StationId,
        stationName = active.StationName,
        url = active.StreamUrl,
        state = active.Control.StopRequested
            ? "stopping"
            : active.Control.IsReady
                ? active.Control.IsPaused ? "paused" : "recording"
                : "starting",
        paused = active.Control.IsPaused,
        canPause = active.Control.CanPause,
        durationSeconds = active.Control.CaptureBookmarkTarget()?.Position.TotalSeconds ?? 0d,
        path = active.Control.CurrentPath ?? active.Path,
        completedFileCount = active.Control.CompletedPaths.Count,
        requestedUtc = active.RequestedUtc,
        startedUtc = active.StartedUtc,
        format = active.Format.ToString(),
        bitrateKbps = active.BitrateKbps
    };

    private static object HistoryPayload(CompletedRecording recording) => new
    {
        id = recording.Id,
        stationId = recording.StationId,
        stationName = recording.StationName,
        path = recording.Path,
        outcome = recording.Outcome,
        reason = recording.Reason,
        scheduleName = string.Empty,
        startedUtcTicks = recording.StartedUtcTicks,
        finishedUtcTicks = recording.FinishedUtcTicks,
        savedFileCount = recording.SavedFileCount
    };

    private static RecordingRequest ReadRequest(JsonElement args)
    {
        var stationId = LiteArgs.RequireText(args, "stationId");
        var stationName = LiteArgs.RequireText(args, "stationName");
        var streamUrl = LiteArgs.RequireText(args, "url");
        if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new LiteRequestException("Adres stacji musi być adresem HTTP lub HTTPS.");
        }

        var formatText = LiteArgs.ReadText(args, "format") ?? nameof(RadioRecordingFormat.Mp3);
        if (!Enum.TryParse<RadioRecordingFormat>(formatText, ignoreCase: true, out var format)
            || !Enum.IsDefined(format))
        {
            throw new LiteRequestException("Nieznany format nagrania.");
        }
        var requestedBitrate = LiteArgs.ReadInt(args, "bitrateKbps", 192, 1, 1000);
        int[] supportedBitrates = [96, 128, 160, 192, 256, 320];
        var bitrate = supportedBitrates.MinBy(value => Math.Abs(value - requestedBitrate));

        var systemRadioFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "AMC — Nagrania radia");
        var folderPreset = LiteArgs.ReadText(args, "folderPreset");
        var defaultFolder = string.Equals(folderPreset, "podcasts", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "AMC — Pobrane podcasty")
            : systemRadioFolder;
        var configuredFolder = ReadOptionalPath(args, "folder");
        return new RecordingRequest(
            stationId,
            stationName,
            streamUrl,
            configuredFolder ?? defaultFolder,
            defaultFolder,
            systemRadioFolder,
            format,
            bitrate);
    }

    private static string? ReadOptionalPath(JsonElement args, string name)
    {
        var value = LiteArgs.ReadText(args, name);
        if (value is null) return null;
        if (value.Length > LiteArgs.MaximumPathLength || value.Any(char.IsControl))
            throw new LiteRequestException($"Niepoprawna ścieżka w argumencie \"{name}\".");
        return value;
    }

    private static void RequestStop(ActiveRecording active)
    {
        active.Control.RequestStop();
        try { active.Cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        ActiveRecording[] recordings;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            recordings = _active.Values.ToArray();
        }
        foreach (var active in recordings) RequestStop(active);
        var tasks = recordings
            .Select(active => active.Task)
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();
        try { Task.WaitAll(tasks, TimeSpan.FromSeconds(10)); }
        catch (AggregateException) { }
    }

    private sealed record RecordingRequest(
        string StationId,
        string StationName,
        string StreamUrl,
        string OutputFolder,
        string DefaultFolder,
        string SystemFallbackFolder,
        RadioRecordingFormat Format,
        int BitrateKbps);

    private sealed record CompletedRecording(
        string Id,
        string StationId,
        string StationName,
        string Path,
        string Outcome,
        string Reason,
        long StartedUtcTicks,
        long FinishedUtcTicks,
        int SavedFileCount);

    private sealed class ActiveRecording(
        string id,
        string stationId,
        string stationName,
        string streamUrl,
        string outputFolder,
        string defaultFolder,
        string systemFallbackFolder,
        RadioRecordingFormat format,
        int bitrateKbps,
        DateTime requestedUtc,
        CancellationTokenSource cancellation,
        RadioRecordingControl control)
    {
        public string Id { get; } = id;
        public string StationId { get; } = stationId;
        public string StationName { get; } = stationName;
        public string StreamUrl { get; } = streamUrl;
        public string OutputFolder { get; } = outputFolder;
        public string DefaultFolder { get; } = defaultFolder;
        public string SystemFallbackFolder { get; } = systemFallbackFolder;
        public RadioRecordingFormat Format { get; } = format;
        public int BitrateKbps { get; } = bitrateKbps;
        public DateTime RequestedUtc { get; } = requestedUtc;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public RadioRecordingControl Control { get; } = control;
        public DateTime? StartedUtc { get; set; }
        public string? Path { get; set; }
        public Task<ManualRadioRecordingResult>? Task { get; set; }
    }
}
