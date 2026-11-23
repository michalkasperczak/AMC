using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Jedno zrodlo etykiet harmonogramu dla interfejsu WPF i wxPython.
/// Tekst jest przeznaczony wprost dla uzytkownika i czytnika ekranu: nie
/// zawiera identyfikatorow, nazw klas ani reprezentacji obiektow.
/// </summary>
public static class RadioSchedulePresentation
{
    public static string DisplayName(RadioRecordingScheduleSettings schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (!string.IsNullOrWhiteSpace(schedule.Name)) return schedule.Name.Trim();
        return string.IsNullOrWhiteSpace(schedule.StationName)
            ? "Plan nagrywania"
            : schedule.StationName.Trim();
    }

    public static string BuildLabel(RadioRecordingScheduleSettings schedule, bool active)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var utc = new DateTime(schedule.NextStartUtcTicks, DateTimeKind.Utc);
        var zone = RadioScheduleCalculator.ResolveTimeZone(schedule.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var state = schedule.Enabled ? "włączony" : "wyłączony";
        var activity = schedule.Enabled && active
            ? ", nagrywanie trwa"
            : !schedule.Enabled && active
                ? ", nagrywanie zostanie zatrzymane po zapisaniu"
                : schedule.Enabled
                    && RadioScheduleCalculator.IsCurrentOccurrenceSuppressed(schedule)
                        ? ", ten termin pominięty"
                        : string.Empty;
        var lastFailure = BuildLastFailureLabel(schedule, zone);
        var occurrence = local.ToString(
            "d MMMM yyyy, H:mm",
            System.Globalization.CultureInfo.GetCultureInfo("pl-PL"));
        return $"{DisplayName(schedule)}, {state}, {occurrence}{activity}{lastFailure}";
    }

    public static string BuildRecurrenceLabel(RadioRecordingScheduleSettings schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.Recurrence == RadioScheduleRecurrence.Once) return "jednorazowo";
        if (schedule.Recurrence == RadioScheduleRecurrence.Daily) return "codziennie";
        if (schedule.Recurrence != RadioScheduleRecurrence.SelectedDays) return "powtarzanie nieznane";

        var dayLabels = schedule.ActiveDays
            .Distinct()
            .OrderBy(DayOrder)
            .Select(PolishDayName)
            .ToArray();
        return dayLabels.Length == 0
            ? "wybrane dni: brak"
            : $"wybrane dni: {string.Join(", ", dayLabels)}";
    }

    public static string FormatDurationMinutes(int totalMinutes)
    {
        var normalized = Math.Max(0, totalMinutes);
        var hours = normalized / 60;
        var minutes = normalized % 60;
        if (hours == 0) return FormatPolishUnit(minutes, "minuta", "minuty", "minut");
        if (minutes == 0) return FormatPolishUnit(hours, "godzina", "godziny", "godzin");
        return $"{FormatPolishUnit(hours, "godzina", "godziny", "godzin")} "
            + FormatPolishUnit(minutes, "minuta", "minuty", "minut");
    }

    private static int DayOrder(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;

    private static string PolishDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "poniedziałek",
        DayOfWeek.Tuesday => "wtorek",
        DayOfWeek.Wednesday => "środa",
        DayOfWeek.Thursday => "czwartek",
        DayOfWeek.Friday => "piątek",
        DayOfWeek.Saturday => "sobota",
        DayOfWeek.Sunday => "niedziela",
        _ => "nieznany dzień"
    };

    private static string FormatPolishUnit(
        int value,
        string singular,
        string paucal,
        string plural)
    {
        var absolute = Math.Abs(value);
        var lastTwoDigits = absolute % 100;
        var unit = absolute == 1
            ? singular
            : absolute % 10 is >= 2 and <= 4 && lastTwoDigits is not (>= 12 and <= 14)
                ? paucal
                : plural;
        return $"{value} {unit}";
    }

    private static string BuildLastFailureLabel(
        RadioRecordingScheduleSettings schedule,
        TimeZoneInfo zone)
    {
        if (schedule.LastFailureUtcTicks is not > 0
            || string.IsNullOrWhiteSpace(schedule.LastFailureMessage))
        {
            return string.Empty;
        }
        var failedUtc = new DateTime(schedule.LastFailureUtcTicks.Value, DateTimeKind.Utc);
        var failedLocal = TimeZoneInfo.ConvertTimeFromUtc(failedUtc, zone);
        return $", ostatnie nagranie nieudane {failedLocal:dd.MM.yyyy HH:mm}: "
            + schedule.LastFailureMessage;
    }
}
