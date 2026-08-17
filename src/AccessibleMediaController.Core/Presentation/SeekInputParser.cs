using System.Globalization;

namespace AccessibleMediaController.Core.Presentation;

public static class SeekInputParser
{
    private const string TimeFormatMessage =
        "Wpisz minuty, minuty:sekundy albo godziny:minuty:sekundy. Sama liczba oznacza minuty.";

    public static bool TryParseTime(string? input, out TimeSpan position, out string error)
    {
        position = TimeSpan.Zero;
        error = string.Empty;
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            error = TimeFormatMessage;
            return false;
        }

        var parts = text.Split(':');
        if (parts.Length is < 1 or > 3 || parts.Any(part => part.Length == 0))
        {
            error = TimeFormatMessage;
            return false;
        }

        try
        {
            long ticks;
            if (parts.Length == 1)
            {
                if (!TryParseNonNegative(parts[0], out var minutes))
                {
                    error = TimeFormatMessage;
                    return false;
                }
                ticks = checked(minutes * TimeSpan.TicksPerMinute);
            }
            else if (parts.Length == 2)
            {
                if (!TryParseNonNegative(parts[0], out var minutes)
                    || !TryParseClockPart(parts[1], out var seconds))
                {
                    error = TimeFormatMessage;
                    return false;
                }
                ticks = checked(minutes * TimeSpan.TicksPerMinute + seconds * TimeSpan.TicksPerSecond);
            }
            else
            {
                if (!TryParseNonNegative(parts[0], out var hours)
                    || !TryParseClockPart(parts[1], out var minutes)
                    || !TryParseClockPart(parts[2], out var seconds))
                {
                    error = TimeFormatMessage;
                    return false;
                }
                ticks = checked(
                    hours * TimeSpan.TicksPerHour
                    + minutes * TimeSpan.TicksPerMinute
                    + seconds * TimeSpan.TicksPerSecond);
            }

            position = TimeSpan.FromTicks(ticks);
            return true;
        }
        catch (OverflowException)
        {
            error = "Podany czas jest zbyt duży.";
            return false;
        }
    }

    public static bool TryParsePercentage(string? input, out int percentage, out string error)
    {
        percentage = 0;
        error = string.Empty;
        var text = input?.Trim() ?? string.Empty;
        if (text.EndsWith('%')) text = text[..^1].TrimEnd();
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out percentage)
            || percentage is < 0 or > 100)
        {
            error = "Wpisz liczbę od 0 do 100.";
            percentage = 0;
            return false;
        }
        return true;
    }

    private static bool TryParseNonNegative(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    private static bool TryParseClockPart(string text, out long value) =>
        TryParseNonNegative(text, out value) && value <= 59;
}
