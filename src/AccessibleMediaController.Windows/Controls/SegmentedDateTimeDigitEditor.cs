namespace AccessibleMediaController.Windows.Controls;

internal enum SegmentedDateTimeField
{
    Date,
    Time
}

internal readonly record struct SegmentDigitEditResult(
    bool IsComplete,
    bool IsValid,
    bool MoveNext,
    DateTime Value,
    int EnteredValue);

/// <summary>
/// Collects fixed-width numeric date and time segments independently from the
/// native DateTimePicker type-ahead implementation. This gives keyboard and
/// screen-reader users the same predictable behaviour for every segment.
/// </summary>
internal sealed class SegmentedDateTimeDigitEditor(SegmentedDateTimeField field)
{
    private static readonly TimeSpan InputTimeout = TimeSpan.FromSeconds(3);
    private string _digits = string.Empty;
    private int _segmentIndex = -1;
    private DateTime _lastInputUtc;

    public SegmentDigitEditResult EnterDigit(
        DateTime currentValue,
        int segmentIndex,
        int digit,
        DateTime inputUtc,
        DateTime minimum,
        DateTime maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digit, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        ArgumentOutOfRangeException.ThrowIfLessThan(segmentIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(segmentIndex, SegmentCount);

        if (_segmentIndex != segmentIndex
            || _lastInputUtc == default
            || inputUtc - _lastInputUtc > InputTimeout
            || inputUtc < _lastInputUtc)
        {
            Reset();
            _segmentIndex = segmentIndex;
        }

        _lastInputUtc = inputUtc;
        _digits += (char)('0' + digit);
        if (_digits.Length < SegmentWidth(segmentIndex))
        {
            return new SegmentDigitEditResult(
                IsComplete: false,
                IsValid: false,
                MoveNext: false,
                currentValue,
                EnteredValue: 0);
        }

        var enteredValue = int.Parse(_digits, System.Globalization.CultureInfo.InvariantCulture);
        Reset();
        if (!TryApply(currentValue, segmentIndex, enteredValue, minimum, maximum, out var result))
        {
            return new SegmentDigitEditResult(
                IsComplete: true,
                IsValid: false,
                MoveNext: false,
                currentValue,
                enteredValue);
        }

        return new SegmentDigitEditResult(
            IsComplete: true,
            IsValid: true,
            MoveNext: segmentIndex < SegmentCount - 1,
            result,
            enteredValue);
    }

    public void Reset()
    {
        _digits = string.Empty;
        _segmentIndex = -1;
        _lastInputUtc = default;
    }

    public static bool TryGetDigit(System.Windows.Forms.Keys key, out int digit)
    {
        if (key is >= System.Windows.Forms.Keys.D0 and <= System.Windows.Forms.Keys.D9)
        {
            digit = key - System.Windows.Forms.Keys.D0;
            return true;
        }

        if (key is >= System.Windows.Forms.Keys.NumPad0 and <= System.Windows.Forms.Keys.NumPad9)
        {
            digit = key - System.Windows.Forms.Keys.NumPad0;
            return true;
        }

        digit = 0;
        return false;
    }

    private int SegmentCount => field == SegmentedDateTimeField.Date ? 3 : 2;

    private int SegmentWidth(int segmentIndex) =>
        field == SegmentedDateTimeField.Date && segmentIndex == 2 ? 4 : 2;

    private bool TryApply(
        DateTime current,
        int segmentIndex,
        int entered,
        DateTime minimum,
        DateTime maximum,
        out DateTime result)
    {
        result = current;
        try
        {
            result = field switch
            {
                SegmentedDateTimeField.Date => ApplyDateSegment(current, segmentIndex, entered),
                _ => ApplyTimeSegment(current, segmentIndex, entered)
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return result >= minimum && result <= maximum;
    }

    private static DateTime ApplyDateSegment(DateTime current, int segmentIndex, int entered)
    {
        var year = current.Year;
        var month = current.Month;
        var day = current.Day;

        switch (segmentIndex)
        {
            case 0:
                if (entered < 1 || entered > DateTime.DaysInMonth(year, month))
                    throw new ArgumentOutOfRangeException(nameof(entered));
                day = entered;
                break;
            case 1:
                if (entered is < 1 or > 12)
                    throw new ArgumentOutOfRangeException(nameof(entered));
                month = entered;
                day = Math.Min(day, DateTime.DaysInMonth(year, month));
                break;
            default:
                if (entered is < 1 or > 9999)
                    throw new ArgumentOutOfRangeException(nameof(entered));
                year = entered;
                day = Math.Min(day, DateTime.DaysInMonth(year, month));
                break;
        }

        return new DateTime(
            year,
            month,
            day,
            current.Hour,
            current.Minute,
            current.Second,
            current.Millisecond,
            current.Kind).AddTicks(current.Ticks % TimeSpan.TicksPerMillisecond);
    }

    private static DateTime ApplyTimeSegment(DateTime current, int segmentIndex, int entered)
    {
        if (segmentIndex == 0 && entered is < 0 or > 23)
            throw new ArgumentOutOfRangeException(nameof(entered));
        if (segmentIndex == 1 && entered is < 0 or > 59)
            throw new ArgumentOutOfRangeException(nameof(entered));

        return new DateTime(
            current.Year,
            current.Month,
            current.Day,
            segmentIndex == 0 ? entered : current.Hour,
            segmentIndex == 1 ? entered : current.Minute,
            current.Second,
            current.Millisecond,
            current.Kind).AddTicks(current.Ticks % TimeSpan.TicksPerMillisecond);
    }
}
