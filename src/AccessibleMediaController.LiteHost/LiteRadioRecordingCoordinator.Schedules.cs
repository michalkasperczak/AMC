using System.Text.Json;
using System.Text.Json.Serialization;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.LiteHost.Protocol;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.LiteHost;

/// <summary>
/// Wykonawcza czesc harmonogramow wxPython. Plany sa synchronizowane z
/// profilem AMC, ale postep pozostaje w procesie hosta: wspolnego state.json
/// nie zapisujemy obok starszej aplikacji WPF. Nagrywanie prowadzi ten sam
/// <see cref="ScheduledRadioRecorder"/> co glowne AMC.
/// </summary>
internal sealed partial class LiteRadioRecordingCoordinator
{
    private readonly Dictionary<string, ManagedSchedule> _schedules =
        new(StringComparer.Ordinal);
    private readonly SystemWakeTimer _wakeTimer = new();
    private System.Threading.Timer? _scheduleTimer;
    private ScheduleDefaults _scheduleDefaults = ScheduleDefaults.SystemDefault;
    private int _processingSchedules;

    public object SyncSchedules(JsonElement args, LiteEventSink events)
    {
        if (!args.TryGetProperty("schedules", out var source)
            || source.ValueKind != JsonValueKind.Array)
        {
            throw new LiteRequestException("Brak listy harmonogramów");
        }
        if (source.GetArrayLength() > 10_000)
            throw new LiteRequestException("Lista harmonogramów jest zbyt długa");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        var incoming = JsonSerializer.Deserialize<List<RadioRecordingScheduleSettings>>(
            source.GetRawText(), options) ?? [];
        var defaults = ReadScheduleDefaults(args);
        var now = DateTime.UtcNow;
        ActiveRecording[] cancelled;

        lock (_gate)
        {
            ThrowIfDisposed();
            _scheduleDefaults = defaults;
            var replacements = new Dictionary<string, ManagedSchedule>(StringComparer.Ordinal);
            foreach (var schedule in incoming.Where(IsUsableSchedule))
            {
                var sourceSnapshot = CloneSchedule(schedule);
                if (_schedules.TryGetValue(schedule.Id, out var previous)
                    && ScheduleSourceEquals(previous.Source, sourceSnapshot))
                {
                    previous.Events = events;
                    replacements[schedule.Id] = previous;
                    continue;
                }
                replacements[schedule.Id] = new ManagedSchedule(
                    sourceSnapshot,
                    NormalizeInitialSchedule(CloneSchedule(sourceSnapshot), now),
                    events);
            }

            cancelled = _active.Values
                .Where(active => active.ScheduleId.Length > 0
                    && (!replacements.TryGetValue(active.ScheduleId, out var replacement)
                        || replacement.Effective.NextStartUtcTicks != active.ExpectedStartUtcTicks))
                .ToArray();
            _schedules.Clear();
            foreach (var pair in replacements) _schedules.Add(pair.Key, pair.Value);
            _scheduleTimer ??= new System.Threading.Timer(
                _ => ProcessDueSchedules(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            RearmWakeTimerLocked();
        }

        foreach (var active in cancelled) RequestStop(active);
        ProcessDueSchedules();
        return ScheduleStatus();
    }

    public object ScheduleStatus()
    {
        lock (_gate)
        {
            var activeIds = _active.Values
                .Where(active => active.ScheduleId.Length > 0)
                .Select(active => active.ScheduleId)
                .ToHashSet(StringComparer.Ordinal);
            return new
            {
                activeIds = activeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                schedules = _schedules.Values
                    .Select(item => item.Effective)
                    .OrderBy(schedule => schedule.NextStartUtcTicks)
                    .Select(schedule => new
                    {
                        id = schedule.Id,
                        navigationText = RadioSchedulePresentation.DisplayName(schedule),
                        label = RadioSchedulePresentation.BuildLabel(
                            schedule, activeIds.Contains(schedule.Id)),
                        enabled = schedule.Enabled,
                        // Efektywny model wraca do wxPython, poniewaz host
                        // przesuwa terminy cykliczne i wylacza wykonany plan
                        // jednorazowy w pamieci. Bez tych pol edytor pokazywal
                        // stary termin z prywatnego pliku, a nastepny zapis
                        // mogl cofnac wykonawce do poprzedniego wystapienia.
                        name = schedule.Name,
                        stationId = schedule.StationId,
                        stationName = schedule.StationName,
                        streamUrl = schedule.StreamUrl,
                        nextStartUtcTicks = schedule.NextStartUtcTicks,
                        timeZoneId = schedule.TimeZoneId,
                        durationMinutes = schedule.DurationMinutes,
                        segmentMinutes = schedule.SegmentMinutes,
                        recurrence = schedule.Recurrence.ToString(),
                        activeDays = schedule.ActiveDays.Select(day => day.ToString()).ToArray(),
                        outputFolder = schedule.OutputFolder,
                        fileNameTemplate = schedule.FileNameTemplate,
                        recordingFormat = schedule.RecordingFormat?.ToString(),
                        recordingBitrateKbps = schedule.RecordingBitrateKbps,
                        wakeComputer = schedule.WakeComputer,
                        suppressedOccurrenceStartUtcTicks = schedule.SuppressedOccurrenceStartUtcTicks,
                        lastFailureUtcTicks = schedule.LastFailureUtcTicks,
                        lastFailureMessage = schedule.LastFailureMessage,
                        lastFailureAcknowledged = schedule.LastFailureAcknowledged
                    })
                    .ToArray()
            };
        }
    }

    private void ProcessDueSchedules()
    {
        if (Interlocked.Exchange(ref _processingSchedules, 1) != 0) return;
        try
        {
            PublishReadyRecordings();
            ManagedSchedule[] snapshot;
            lock (_gate)
            {
                if (_disposed) return;
                snapshot = _schedules.Values
                    .Where(item => item.Effective.Enabled)
                    .OrderBy(item => item.Effective.NextStartUtcTicks)
                    .ToArray();
            }

            var now = DateTime.UtcNow;
            foreach (var managed in snapshot)
            {
                RadioScheduleDueDecision decision;
                lock (_gate)
                {
                    if (_disposed || !_schedules.TryGetValue(managed.Effective.Id, out var current)
                        || !ReferenceEquals(current, managed)
                        || _active.Values.Any(active => active.ScheduleId == managed.Effective.Id))
                    {
                        continue;
                    }
                    decision = RadioScheduleCalculator.Evaluate(managed.Effective, now);
                    if (decision.Kind == RadioScheduleDueKind.Missed)
                    {
                        AdvanceScheduleLocked(managed, now);
                        RearmWakeTimerLocked();
                        continue;
                    }
                    if (decision.Kind == RadioScheduleDueKind.Future) continue;
                }
                StartScheduledRecording(managed);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("[amc-lite-host] harmonogram: " + exception);
        }
        finally
        {
            Volatile.Write(ref _processingSchedules, 0);
        }
    }

    private void StartScheduledRecording(ManagedSchedule managed)
    {
        var snapshot = CloneSchedule(managed.Effective);
        var cancellation = new CancellationTokenSource();
        var control = new RadioRecordingControl();
        ActiveRecording active;
        DateTime deadlineUtc;
        ScheduleDefaults defaults;

        lock (_gate)
        {
            if (_disposed || !_schedules.TryGetValue(snapshot.Id, out var current)
                || !ReferenceEquals(current, managed)
                || _active.Values.Any(item => item.ScheduleId == snapshot.Id))
            {
                cancellation.Dispose();
                return;
            }
            var startUtc = new DateTime(snapshot.NextStartUtcTicks, DateTimeKind.Utc);
            deadlineUtc = startUtc.AddMinutes(Math.Clamp(snapshot.DurationMinutes, 1, 10_080));
            defaults = _scheduleDefaults;
            var stationFolder = defaults.StationFolders.TryGetValue(snapshot.StationId, out var folder)
                ? folder
                : defaults.DefaultFolder;
            var format = snapshot.RecordingFormat ?? defaults.Format;
            var bitrate = snapshot.RecordingBitrateKbps ?? defaults.BitrateKbps;
            var id = $"schedule:{snapshot.Id}:{snapshot.NextStartUtcTicks}";
            active = new ActiveRecording(
                id,
                snapshot.StationId,
                snapshot.StationName,
                snapshot.StreamUrl,
                snapshot.OutputFolder,
                stationFolder,
                defaults.SystemFallbackFolder,
                format,
                bitrate,
                DateTime.UtcNow,
                cancellation,
                control,
                scheduleId: snapshot.Id,
                scheduleName: snapshot.Name,
                expectedStartUtcTicks: snapshot.NextStartUtcTicks,
                events: managed.Events);
            _active.Add(active.Id, active);
        }

        var task = Task.Run(() => ScheduledRadioRecorder.RecordAsync(
            snapshot,
            deadlineUtc,
            active.DefaultFolder,
            active.SystemFallbackFolder,
            active.Format,
            active.BitrateKbps,
            control,
            cancellation.Token));
        active.Task = task;
        _ = CompleteScheduledAsync(managed, active, task);
        managed.Events.Publish("radio.recordingScheduled", new
        {
            stationId = active.StationId,
            stationName = active.StationName,
            scheduleName = active.ScheduleName,
            remainingSeconds = Math.Max(0d, (deadlineUtc - DateTime.UtcNow).TotalSeconds)
        });
    }

    private void PublishReadyRecordings()
    {
        ActiveRecording[] ready;
        lock (_gate)
        {
            if (_disposed) return;
            ready = _active.Values
                .Where(active => active.ScheduleId.Length > 0
                    && active.Control.IsReady
                    && !active.StartAnnounced
                    && active.Events is not null)
                .ToArray();
        }
        foreach (var active in ready)
            RecordingStarted(active, active.Control.CurrentPath ?? active.Path ?? string.Empty, active.Events!);
    }

    private async Task CompleteScheduledAsync(
        ManagedSchedule managed,
        ActiveRecording active,
        Task<ScheduledRadioRecordingResult> task)
    {
        ScheduledRadioRecordingResult result;
        try
        {
            result = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = new ScheduledRadioRecordingResult(false, true, null, null);
        }
        catch (Exception exception)
        {
            result = new ScheduledRadioRecordingResult(false, false, null, exception.Message);
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
            active.ScheduleName,
            (active.StartedUtc ?? active.RequestedUtc).Ticks,
            DateTime.UtcNow.Ticks,
            paths.Count);
        bool publish;
        lock (_gate)
        {
            _active.Remove(active.Id);
            _history.Insert(0, completed);
            if (_history.Count > 1_000) _history.RemoveRange(1_000, _history.Count - 1_000);
            publish = !_disposed;
            if (_schedules.TryGetValue(managed.Effective.Id, out var current)
                && ReferenceEquals(current, managed))
            {
                AdvanceScheduleLocked(managed, DateTime.UtcNow);
                RearmWakeTimerLocked();
            }
        }
        active.Cancellation.Dispose();
        if (!publish || active.Events is null) return;
        var payload = new
        {
            recordingId = active.Id,
            stationId = active.StationId,
            stationName = active.StationName,
            scheduleName = active.ScheduleName,
            path,
            savedFileCount = paths.Count,
            cancelled = result.Cancelled,
            success = result.Success,
            error = result.Error,
            outcome,
            startedUtcTicks = completed.StartedUtcTicks,
            finishedUtcTicks = completed.FinishedUtcTicks
        };
        active.Events.Publish(result.Cancelled
            ? "radio.recordingStopped"
            : result.Success
                ? "radio.recordingFinished"
                : "radio.recordingFailed", payload);
    }

    private void AdvanceScheduleLocked(ManagedSchedule managed, DateTime afterUtc)
    {
        var schedule = managed.Effective;
        var next = RadioScheduleCalculator.FindNextStartUtc(schedule, afterUtc);
        if (next is null)
        {
            schedule.Enabled = false;
            return;
        }
        schedule.NextStartUtcTicks = next.Value.Ticks;
        schedule.SuppressedOccurrenceStartUtcTicks = null;
    }

    private void RearmWakeTimerLocked()
    {
        var now = DateTime.UtcNow;
        var next = _schedules.Values
            .Select(item => item.Effective)
            .Where(schedule => schedule.Enabled
                && schedule.NextStartUtcTicks > now.Ticks
                && (schedule.WakeComputer ?? _scheduleDefaults.WakeByDefault))
            .OrderBy(schedule => schedule.NextStartUtcTicks)
            .FirstOrDefault();
        if (next is null)
        {
            _wakeTimer.Cancel();
            return;
        }
        var start = new DateTime(next.NextStartUtcTicks, DateTimeKind.Utc);
        var wake = start - TimeSpan.FromMinutes(2);
        _wakeTimer.Arm(wake <= now ? start : wake);
    }

    private static RadioRecordingScheduleSettings NormalizeInitialSchedule(
        RadioRecordingScheduleSettings schedule,
        DateTime now)
    {
        if (!schedule.Enabled) return schedule;
        var decision = RadioScheduleCalculator.Evaluate(schedule, now);
        if (decision.Kind != RadioScheduleDueKind.Missed) return schedule;
        var next = RadioScheduleCalculator.FindNextStartUtc(schedule, now);
        if (next is null) schedule.Enabled = false;
        else schedule.NextStartUtcTicks = next.Value.Ticks;
        return schedule;
    }

    private static bool IsUsableSchedule(RadioRecordingScheduleSettings schedule) =>
        !string.IsNullOrWhiteSpace(schedule.Id)
        && !string.IsNullOrWhiteSpace(schedule.StationName)
        && Uri.TryCreate(schedule.StreamUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && schedule.NextStartUtcTicks > 0;

    private static ScheduleDefaults ReadScheduleDefaults(JsonElement args)
    {
        var systemRadioFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "AMC — Nagrania radia");
        var folderPreset = LiteArgs.ReadText(args, "folderPreset");
        var systemDefault = string.Equals(folderPreset, "podcasts", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AMC — Pobrane podcasty")
            : systemRadioFolder;
        var configured = LiteArgs.ReadText(args, "defaultFolder") ?? systemDefault;
        var formatText = LiteArgs.ReadText(args, "recordingFormat") ?? nameof(RadioRecordingFormat.Mp3);
        if (!Enum.TryParse<RadioRecordingFormat>(formatText, true, out var format)
            || !Enum.IsDefined(format)) format = RadioRecordingFormat.Mp3;
        var requestedBitrate = LiteArgs.ReadInt(args, "recordingBitrateKbps", 192, 1, 1000);
        int[] supported = [96, 128, 160, 192, 256, 320];
        var bitrate = supported.MinBy(value => Math.Abs(value - requestedBitrate));
        var wake = args.TryGetProperty("wakeScheduledRecordings", out var wakeValue)
            && wakeValue.ValueKind == JsonValueKind.True;
        var stationFolders = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.TryGetProperty("stationFolders", out var folders)
            && folders.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in folders.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is { Length: > 0 } path)
                {
                    stationFolders[property.Name] = path;
                }
            }
        }
        return new ScheduleDefaults(
            configured, systemRadioFolder, format, bitrate, wake, stationFolders);
    }

    private static bool ScheduleSourceEquals(
        RadioRecordingScheduleSettings left,
        RadioRecordingScheduleSettings right) =>
        left.Id == right.Id
        && left.Name == right.Name
        && left.StationId == right.StationId
        && left.StationName == right.StationName
        && left.StreamUrl == right.StreamUrl
        && left.NextStartUtcTicks == right.NextStartUtcTicks
        && left.TimeZoneId == right.TimeZoneId
        && left.DurationMinutes == right.DurationMinutes
        && left.SegmentMinutes == right.SegmentMinutes
        && left.Recurrence == right.Recurrence
        && left.ActiveDays.SequenceEqual(right.ActiveDays)
        && left.OutputFolder == right.OutputFolder
        && left.FileNameTemplate == right.FileNameTemplate
        && left.RecordingFormat == right.RecordingFormat
        && left.RecordingBitrateKbps == right.RecordingBitrateKbps
        && left.WakeComputer == right.WakeComputer
        && left.Enabled == right.Enabled;

    private static RadioRecordingScheduleSettings CloneSchedule(
        RadioRecordingScheduleSettings schedule) => new()
    {
        Id = schedule.Id,
        Name = schedule.Name,
        StationId = schedule.StationId,
        StationName = schedule.StationName,
        StreamUrl = schedule.StreamUrl,
        NextStartUtcTicks = schedule.NextStartUtcTicks,
        TimeZoneId = schedule.TimeZoneId,
        DurationMinutes = schedule.DurationMinutes,
        SegmentMinutes = schedule.SegmentMinutes,
        Recurrence = schedule.Recurrence,
        ActiveDays = [.. schedule.ActiveDays],
        OutputFolder = schedule.OutputFolder,
        FileNameTemplate = schedule.FileNameTemplate,
        RecordingFormat = schedule.RecordingFormat,
        RecordingBitrateKbps = schedule.RecordingBitrateKbps,
        WakeComputer = schedule.WakeComputer,
        Enabled = schedule.Enabled,
        SuppressedOccurrenceStartUtcTicks = schedule.SuppressedOccurrenceStartUtcTicks,
        LastFailureUtcTicks = schedule.LastFailureUtcTicks,
        LastFailureMessage = schedule.LastFailureMessage,
        LastFailureAcknowledged = schedule.LastFailureAcknowledged
    };

    private void DisposeSchedulesLocked()
    {
        _scheduleTimer?.Dispose();
        _scheduleTimer = null;
        _wakeTimer.Dispose();
    }

    private sealed record ScheduleDefaults(
        string DefaultFolder,
        string SystemFallbackFolder,
        RadioRecordingFormat Format,
        int BitrateKbps,
        bool WakeByDefault,
        IReadOnlyDictionary<string, string> StationFolders)
    {
        public static ScheduleDefaults SystemDefault { get; } = new(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AMC — Nagrania radia"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "AMC — Nagrania radia"),
            RadioRecordingFormat.Mp3,
            192,
            false,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed class ManagedSchedule(
        RadioRecordingScheduleSettings source,
        RadioRecordingScheduleSettings effective,
        LiteEventSink events)
    {
        public RadioRecordingScheduleSettings Source { get; } = source;
        public RadioRecordingScheduleSettings Effective { get; } = effective;
        public LiteEventSink Events { get; set; } = events;
    }
}
