using System.Windows;
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
        RefreshRows();
        Loaded += (_, _) =>
        {
            SchedulesList.Focus();
            Keyboard.Focus(SchedulesList);
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
        var recurrence = schedule.Recurrence switch
        {
            RadioScheduleRecurrence.Once => "jednorazowo",
            RadioScheduleRecurrence.Daily => "codziennie",
            RadioScheduleRecurrence.SelectedDays => "wybrane dni",
            _ => "powtarzanie nieznane"
        };
        var state = schedule.Enabled
            ? active ? "włączony, nagrywanie trwa" : "włączony"
            : active ? "wyłączony, nagrywanie zostanie zatrzymane po zapisaniu" : "wyłączony";
        var fileDivision = schedule.SegmentMinutes > 0
            ? $"części co {schedule.SegmentMinutes} min"
            : "jeden plik";
        return $"{schedule.StationName}, {local:dd.MM.yyyy HH:mm}, {schedule.DurationMinutes} min, {fileDivision}, {recurrence}, {state}";
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
        RefreshRows(editor.ResultSchedule.Id);
        FocusSchedulesList();
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e) => ToggleSelectedEnabled();

    private void ToggleSelectedEnabled()
    {
        if (SchedulesList.SelectedItem is not ScheduleRow row) return;
        row.Schedule.Enabled = !row.Schedule.Enabled;
        var message = row.Schedule.Enabled
            ? $"Włączono plan: {row.Schedule.StationName}"
            : $"Wyłączono plan: {row.Schedule.StationName}";
        RefreshRows(row.Schedule.Id);
        SchedulesList.Focus();
        Keyboard.Focus(SchedulesList);
        ScheduleStatus.Text = $"{message}. Wybierz Zapisz, aby zatwierdzić zmianę.";
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
        RefreshRows();
        FocusSchedulesList();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ResultSchedules = _schedules.Select(Clone).ToList();
        ResultWakeScheduledRecordings = GlobalWakeCheckBox.IsChecked == true;
        DialogResult = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Insert)
        {
            New_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && SchedulesList.IsKeyboardFocusWithin)
        {
            EditSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Space && SchedulesList.IsKeyboardFocusWithin)
        {
            ToggleSelectedEnabled();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && SchedulesList.IsKeyboardFocusWithin)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    private void SchedulesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void FocusSchedulesList()
    {
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
        RecordingFormat = schedule.RecordingFormat,
        RecordingBitrateKbps = schedule.RecordingBitrateKbps,
        WakeComputer = schedule.WakeComputer,
        Enabled = schedule.Enabled
    };

    private sealed record ScheduleRow(RadioRecordingScheduleSettings Schedule, string Label)
    {
        public string AccessibleLabel => $"{Label}, {(Schedule.Enabled ? "zaznaczony" : "niezaznaczony")}";
        public override string ToString() => Label;
    }
}
