namespace AccessibleMediaController.Core.Configuration;

public enum RadioScheduleDueKind
{
    Future,
    StartRemaining,
    Missed
}

public sealed record RadioScheduleDueDecision(
    RadioScheduleDueKind Kind,
    TimeSpan Remaining);

public static class RadioScheduleCalculator
{
    public static RadioScheduleDueDecision Evaluate(
        RadioRecordingScheduleSettings schedule,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        utcNow = EnsureUtc(utcNow);
        var start = new DateTime(schedule.NextStartUtcTicks, DateTimeKind.Utc);
        if (utcNow < start) return new RadioScheduleDueDecision(RadioScheduleDueKind.Future, TimeSpan.Zero);

        var end = start.AddMinutes(Math.Clamp(schedule.DurationMinutes, 1, 10_080));
        return utcNow < end
            ? new RadioScheduleDueDecision(RadioScheduleDueKind.StartRemaining, end - utcNow)
            : new RadioScheduleDueDecision(RadioScheduleDueKind.Missed, TimeSpan.Zero);
    }

    public static DateTime? FindNextStartUtc(
        RadioRecordingScheduleSettings schedule,
        DateTime afterUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        afterUtc = EnsureUtc(afterUtc);
        if (schedule.Recurrence == RadioScheduleRecurrence.Once) return null;

        var zone = ResolveTimeZone(schedule.TimeZoneId);
        var currentUtc = new DateTime(schedule.NextStartUtcTicks, DateTimeKind.Utc);
        var currentLocal = TimeZoneInfo.ConvertTimeFromUtc(currentUtc, zone);
        var localTime = currentLocal.TimeOfDay;
        var activeDays = schedule.ActiveDays.Count == 0
            ? new HashSet<DayOfWeek> { currentLocal.DayOfWeek }
            : schedule.ActiveDays.ToHashSet();
        var afterLocal = TimeZoneInfo.ConvertTimeFromUtc(afterUtc, zone);
        var firstDate = currentLocal.Date > afterLocal.Date ? currentLocal.Date : afterLocal.Date;

        for (var offset = 0; offset <= 3700; offset++)
        {
            var date = firstDate.AddDays(offset);
            if (schedule.Recurrence == RadioScheduleRecurrence.SelectedDays
                && !activeDays.Contains(date.DayOfWeek))
            {
                continue;
            }

            var localCandidate = DateTime.SpecifyKind(date + localTime, DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(localCandidate)) localCandidate = localCandidate.AddMinutes(30);
            var utcCandidate = TimeZoneInfo.ConvertTimeToUtc(localCandidate, zone);
            if (utcCandidate > afterUtc && utcCandidate > currentUtc) return utcCandidate;
        }
        return null;
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }

    public static DateTime ConvertLocalToUtc(DateTime local, string? timeZoneId)
    {
        var zone = ResolveTimeZone(timeZoneId);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
