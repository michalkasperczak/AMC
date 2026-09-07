using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Coordinates an optional same-run resume of an occurrence stopped with R.
/// The durable suppression marker belongs to RadioRecordingScheduleSettings;
/// this helper only remembers whether a second R requested a resume before the
/// current process ends.
/// </summary>
internal sealed class ScheduledRadioRecordingInterruptionTracker
{
    private readonly Dictionary<string, long> _suspendedOccurrences =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _resumeRequests = new(StringComparer.Ordinal);

    public void Suspend(string scheduleId, long expectedStartUtcTicks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduleId);
        _suspendedOccurrences[scheduleId] = expectedStartUtcTicks;
        _resumeRequests.Remove(scheduleId);
    }

    public bool IsSuspended(string scheduleId, long expectedStartUtcTicks) =>
        _suspendedOccurrences.TryGetValue(scheduleId, out var storedStart)
        && storedStart == expectedStartUtcTicks;

    public bool RequestResume(string scheduleId, long expectedStartUtcTicks)
    {
        if (!IsSuspended(scheduleId, expectedStartUtcTicks)) return false;
        _resumeRequests.Add(scheduleId);
        return true;
    }

    public bool ConsumeResumeRequest(string scheduleId, long expectedStartUtcTicks)
    {
        if (!IsSuspended(scheduleId, expectedStartUtcTicks)
            || !_resumeRequests.Remove(scheduleId)) return false;
        _suspendedOccurrences.Remove(scheduleId);
        return true;
    }

    public bool TakeForResume(string scheduleId, long expectedStartUtcTicks)
    {
        if (!IsSuspended(scheduleId, expectedStartUtcTicks)) return false;
        _suspendedOccurrences.Remove(scheduleId);
        _resumeRequests.Remove(scheduleId);
        return true;
    }

    public void Clear(string scheduleId)
    {
        _suspendedOccurrences.Remove(scheduleId);
        _resumeRequests.Remove(scheduleId);
    }

    public void RetainMatching(IEnumerable<RadioRecordingScheduleSettings> schedules)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        var valid = schedules
            .Where(schedule => schedule.Enabled)
            .ToDictionary(schedule => schedule.Id, schedule => schedule.NextStartUtcTicks, StringComparer.Ordinal);
        foreach (var scheduleId in _suspendedOccurrences.Keys.ToArray())
        {
            if (valid.TryGetValue(scheduleId, out var start)
                && start == _suspendedOccurrences[scheduleId]) continue;
            Clear(scheduleId);
        }
    }
}
