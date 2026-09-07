using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Windows;

public partial class RadioSchedulesWindow : Window
{
    private readonly IReadOnlyCollection<MediaItem> _stations;
    private readonly string? _preferredStationId;
    private readonly HashSet<string> _activeIds;
    private readonly List<RadioRecordingScheduleSettings> _schedules;
    private readonly RadioRecordingFormat _defaultRecordingFormat;
    private readonly int _defaultRecordingBitrateKbps;

    public IReadOnlyList<RadioRecordingScheduleSettings> ResultSchedules { get; private set; } = [];
    public bool ResultWakeScheduledRecordings { get; private set; }
    public bool HasCommittedChanges { get; private set; }
    public event EventHandler? CommittedChanges;

    public RadioSchedulesWindow(
        IReadOnlyCollection<MediaItem> stations,
        IEnumerable<RadioRecordingScheduleSettings> schedules,
        IEnumerable<string> activeIds,
        string? preferredStationId,
        bool wakeScheduledRecordings,
        RadioRecordingFormat defaultRecordingFormat,
        int defaultRecordingBitrateKbps)
    {
        InitializeComponent();
        _stations = stations;
        _preferredStationId = preferredStationId;
        _activeIds = activeIds.ToHashSet(StringComparer.Ordinal);
        _schedules = schedules.Select(Clone).ToList();
        _defaultRecordingFormat = defaultRecordingFormat;
        _defaultRecordingBitrateKbps = defaultRecordingBitrateKbps;
        GlobalWakeCheckBox.IsChecked = wakeScheduledRecordings;
        UpdateResults();
        RefreshRows();
        Loaded += (_, _) =>
        {
            FocusSelectedSchedule();
        };
    }

    private void RefreshRows(string? preferredId = null)
    {
        var rows = _schedules
            .OrderBy(schedule => schedule.NextStartUtcTicks)
            .Select(schedule => new ScheduleRow(schedule, BuildLabel(schedule, _activeIds.Contains(schedule.Id))))
            .ToList();
        SchedulesList.ItemsSource = rows;
        SchedulesList.SelectedItem = rows.FirstOrDefault(row => row.Schedule.Id == preferredId)
            ?? rows.FirstOrDefault();
    }

    private static string BuildLabel(RadioRecordingScheduleSettings schedule, bool active)
    {
        var utc = new DateTime(schedule.NextStartUtcTicks, DateTimeKind.Utc);
        var zone = RadioScheduleCalculator.ResolveTimeZone(schedule.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var recurrence = BuildRecurrenceLabel(schedule);
        var state = schedule.Enabled ? "włączone" : "wyłączone";
        var activity = schedule.Enabled && active
            ? ", nagrywanie trwa"
            : !schedule.Enabled && active
                ? ", nagrywanie zostanie zatrzymane po zapisaniu"
                : schedule.Enabled
                    && schedule.SuppressedOccurrenceStartUtcTicks == schedule.NextStartUtcTicks
                        ? ", bieżące wystąpienie zatrzymane"
                        : string.Empty;
        var lastFailure = BuildLastFailureLabel(schedule, zone);
        var fileDivision = schedule.SegmentMinutes > 0
            ? $"części co {schedule.SegmentMinutes} min"
            : "jeden plik";
        var exampleFileName = RadioRecordingFileNameTemplate.Expand(
            schedule.FileNameTemplate,
            schedule.StationName,
            local,
            partNumber: 1);
        return $"{schedule.StationName}, {state}, {local:dd.MM.yyyy HH:mm}, długość nagrania: {FormatDurationMinutes(schedule.DurationMinutes)}, {fileDivision}, nazwa pliku: {exampleFileName}, {recurrence}{activity}{lastFailure}";
    }

    internal static string BuildRecurrenceLabel(RadioRecordingScheduleSettings schedule)
    {
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
        _ => day.ToString()
    };

    internal static string FormatDurationMinutes(int totalMinutes)
    {
        var normalized = Math.Max(0, totalMinutes);
        var hours = normalized / 60;
        var minutes = normalized % 60;
        if (hours == 0) return FormatPolishUnit(minutes, "minuta", "minuty", "minut");
        if (minutes == 0) return FormatPolishUnit(hours, "godzina", "godziny", "godzin");
        return $"{FormatPolishUnit(hours, "godzina", "godziny", "godzin")} "
            + FormatPolishUnit(minutes, "minuta", "minuty", "minut");
    }

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
        return $", ostatnie nagranie nieudane {failedLocal:dd.MM.yyyy HH:mm}: {schedule.LastFailureMessage}";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var editor = new RadioScheduleEditorWindow(
            _stations,
            null,
            _preferredStationId,
            globalWakeEnabled: GlobalWakeCheckBox.IsChecked == true,
            defaultRecordingFormat: _defaultRecordingFormat,
            defaultRecordingBitrateKbps: _defaultRecordingBitrateKbps) { Owner = this };
        if (editor.ShowDialog() != true || editor.ResultSchedule is null) return;
        _schedules.Add(editor.ResultSchedule);
        CommitChanges();
        RefreshRows(editor.ResultSchedule.Id);
        FocusSchedulesList();
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e) => ToggleSelectedEnabled();

    internal bool ToggleSelectedEnabled()
    {
        if (SchedulesList.SelectedItem is not ScheduleRow row) return false;
        var listHadKeyboardFocus = SchedulesList.IsKeyboardFocusWithin;
        row.Schedule.Enabled = !row.Schedule.Enabled;
        row.UpdateLabel(BuildLabel(row.Schedule, _activeIds.Contains(row.Schedule.Id)));
        CommitChanges();
        var message = row.AccessibleLabel;
        Dispatcher.BeginInvoke(
            () =>
            {
                SchedulesList.SelectedItem = row;
                SchedulesList.ScrollIntoView(row);
                SchedulesList.UpdateLayout();
                if (SchedulesList.ItemContainerGenerator.ContainerFromItem(row) is ListBoxItem item)
                {
                    item.Focus();
                    Keyboard.Focus(item);
                }
                else if (!listHadKeyboardFocus)
                {
                    SchedulesList.Focus();
                    Keyboard.Focus(SchedulesList);
                }
                ScheduleStatus.Announce(message);
            },
            System.Windows.Threading.DispatcherPriority.ContextIdle);
        return true;
    }

    private void EditSelected()
    {
        if (SchedulesList.SelectedItem is not ScheduleRow row) return;
        var editor = new RadioScheduleEditorWindow(
            _stations,
            row.Schedule,
            row.Schedule.StationId,
            globalWakeEnabled: GlobalWakeCheckBox.IsChecked == true,
            defaultRecordingFormat: _defaultRecordingFormat,
            defaultRecordingBitrateKbps: _defaultRecordingBitrateKbps) { Owner = this };
        if (editor.ShowDialog() != true || editor.ResultSchedule is null) return;
        var index = _schedules.FindIndex(schedule => schedule.Id == row.Schedule.Id);
        if (index >= 0) _schedules[index] = editor.ResultSchedule;
        CommitChanges();
        RefreshRows(editor.ResultSchedule.Id);
        FocusSchedulesList();
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (SchedulesList.SelectedItem is not ScheduleRow row) return;
        var result = MessageBox.Show(
            this,
            _activeIds.Contains(row.Schedule.Id)
                ? $"Zatrzymać nagrywanie i usunąć plan {row.Schedule.StationName}?"
                : $"Usunąć plan nagrywania {row.Schedule.StationName}?",
            "Usuń plan nagrywania",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        _schedules.RemoveAll(schedule => schedule.Id == row.Schedule.Id);
        CommitChanges();
        RefreshRows();
        FocusSchedulesList();
    }

    private void GlobalWakeCheckBox_Click(object sender, RoutedEventArgs e) => CommitChanges();

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CommitChanges()
    {
        UpdateResults();
        HasCommittedChanges = true;
        CommittedChanges?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateResults()
    {
        ResultSchedules = _schedules.Select(Clone).ToList();
        ResultWakeScheduledRecordings = GlobalWakeCheckBox.IsChecked == true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Insert)
        {
            New_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void SchedulesList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Enter)
        {
            EditSelected();
            e.Handled = true;
        }
        else if (key == Key.Space)
        {
            e.Handled = ToggleSelectedEnabled();
        }
        else if (key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    private void SchedulesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void FocusSchedulesList() => FocusSelectedSchedule();

    private void FocusSelectedSchedule()
    {
        SchedulesList.UpdateLayout();
        var selected = SchedulesList.SelectedItem;
        if (selected is not null
            && SchedulesList.ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }

        SchedulesList.Focus();
        Keyboard.Focus(SchedulesList);
    }

    private static RadioRecordingScheduleSettings Clone(RadioRecordingScheduleSettings schedule) => new()
    {
        Id = schedule.Id,
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

    private sealed class ScheduleRow(
        RadioRecordingScheduleSettings schedule,
        string label) : INotifyPropertyChanged
    {
        private string _label = label;

        public event PropertyChangedEventHandler? PropertyChanged;

        public RadioRecordingScheduleSettings Schedule { get; } = schedule;
        public string Label => _label;
        public string NavigationText => Schedule.StationName;
        public bool IsEnabled => Schedule.Enabled;
        public string AccessibleLabel => Label;

        public void UpdateLabel(string value)
        {
            if (_label == value) return;
            _label = value;
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(AccessibleLabel));
        }

        public override string ToString() => Label;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
