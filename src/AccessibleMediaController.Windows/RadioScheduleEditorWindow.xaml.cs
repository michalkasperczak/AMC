using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class RadioScheduleEditorWindow : Window
{
    private readonly RadioRecordingScheduleSettings? _existing;
    private readonly IReadOnlyList<StationChoice> _stations;
    private readonly IReadOnlyList<DayChoice> _dayChoices;

    public RadioRecordingScheduleSettings? ResultSchedule { get; private set; }

    public RadioScheduleEditorWindow(
        IReadOnlyCollection<MediaItem> stations,
        RadioRecordingScheduleSettings? existing,
        string? preferredStationId,
        DateTime? initialStartUtc = null,
        bool offerImmediateStart = false,
        bool globalWakeEnabled = false)
    {
        InitializeComponent();
        _existing = existing;
        var choices = stations
            .Where(item => item.Kind == MediaItemKind.Station
                && Uri.TryCreate(item.Source, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(item => new StationChoice(item.Id, item.Title, item.Source!))
            .ToList();
        if (existing is not null && choices.All(choice => choice.Id != existing.StationId))
            choices.Insert(0, new StationChoice(existing.StationId, existing.StationName, existing.StreamUrl));
        _stations = choices;
        StationCombo.ItemsSource = _stations;
        RecurrenceCombo.ItemsSource = RecurrenceChoice.All;
        var wakeChoices = WakeChoice.Create(globalWakeEnabled);
        WakeCombo.ItemsSource = wakeChoices;
        _dayChoices =
        [
            new(DayOfWeek.Monday, "Poniedziałek"),
            new(DayOfWeek.Tuesday, "Wtorek"),
            new(DayOfWeek.Wednesday, "Środa"),
            new(DayOfWeek.Thursday, "Czwartek"),
            new(DayOfWeek.Friday, "Piątek"),
            new(DayOfWeek.Saturday, "Sobota"),
            new(DayOfWeek.Sunday, "Niedziela")
        ];
        DaysList.ItemsSource = _dayChoices;
        DaysList.SelectedIndex = 0;

        var initialUtc = existing is null
            ? initialStartUtc ?? DateTime.UtcNow.AddMinutes(5)
            : new DateTime(existing.NextStartUtcTicks, DateTimeKind.Utc);
        var zone = RadioScheduleCalculator.ResolveTimeZone(existing?.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(initialUtc, zone);
        DateTextBox.Text = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        TimeTextBox.Text = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        DurationTextBox.Text = (existing?.DurationMinutes ?? 60).ToString(CultureInfo.InvariantCulture);
        OutputFolderTextBox.Text = existing?.OutputFolder ?? string.Empty;
        var customOutputFolder = !string.IsNullOrWhiteSpace(existing?.OutputFolder);
        OutputFolderModeCombo.SelectedIndex = customOutputFolder ? 1 : 0;
        EnabledCheckBox.IsChecked = existing?.Enabled ?? true;
        RecurrenceCombo.SelectedItem = RecurrenceChoice.All.First(choice =>
            choice.Value == (existing?.Recurrence ?? RadioScheduleRecurrence.Once));
        WakeCombo.SelectedItem = wakeChoices.First(choice => choice.Value == existing?.WakeComputer);

        var stationId = existing?.StationId ?? preferredStationId;
        StationCombo.SelectedItem = _stations.FirstOrDefault(choice => choice.Id == stationId)
            ?? _stations.FirstOrDefault();
        ImmediateStartCheckBox.Visibility = offerImmediateStart && existing is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        ImmediateStartExplanation.Visibility = ImmediateStartCheckBox.Visibility;
        ImmediateStartCheckBox.IsChecked = offerImmediateStart && existing is null;
        var selectedDays = (existing?.ActiveDays ?? []).ToHashSet();
        foreach (var choice in _dayChoices)
        {
            choice.IsChecked = selectedDays.Contains(choice.Value);
        }
        if (existing is null)
        {
            var today = local.DayOfWeek;
            var todayChoice = _dayChoices.FirstOrDefault(choice => choice.Value == today);
            if (todayChoice is not null) todayChoice.IsChecked = true;
        }
        UpdateDaysEnabled();
        UpdateStartControlsEnabled();
        UpdateOutputFolderControls();
        Loaded += (_, _) =>
        {
            StationCombo.Focus();
            Keyboard.Focus(StationCombo);
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        if (StationCombo.SelectedItem is not StationChoice station)
        {
            ShowError("Wybierz stację do nagrania", StationCombo);
            return;
        }
        var immediateStart = ImmediateStartCheckBox.Visibility == Visibility.Visible
            && ImmediateStartCheckBox.IsChecked == true;
        var date = DateTime.Today;
        if (!immediateStart && !TryParseDate(DateTextBox.Text, out date))
        {
            ShowError("Wpisz prawidłową datę, na przykład 2026-08-30", DateTextBox);
            return;
        }
        var time = TimeSpan.Zero;
        if (!immediateStart
            && (!TimeSpan.TryParseExact(TimeTextBox.Text.Trim(), ["h\\:mm", "hh\\:mm"], CultureInfo.InvariantCulture, out time)
            || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
        )
        {
            ShowError("Wpisz godzinę w formacie godzina dwukropek minuta", TimeTextBox);
            return;
        }
        if (!int.TryParse(DurationTextBox.Text.Trim(), out var duration) || duration is < 1 or > 10_080)
        {
            ShowError("Długość musi wynosić od 1 do 10080 minut", DurationTextBox);
            return;
        }
        if (RecurrenceCombo.SelectedItem is not RecurrenceChoice recurrence)
        {
            ShowError("Wybierz sposób powtarzania", RecurrenceCombo);
            return;
        }
        var days = _dayChoices
            .Where(choice => choice.IsChecked)
            .Select(choice => choice.Value)
            .ToList();
        if (recurrence.Value == RadioScheduleRecurrence.SelectedDays && days.Count == 0)
        {
            ShowError("Wybierz co najmniej jeden dzień tygodnia", DaysList);
            return;
        }

        var timeZoneId = _existing?.TimeZoneId ?? TimeZoneInfo.Local.Id;
        var startUtc = immediateStart
            ? DateTime.UtcNow
            : RadioScheduleCalculator.ConvertLocalToUtc(date.Date + time, timeZoneId);
        var schedule = new RadioRecordingScheduleSettings
        {
            Id = _existing?.Id ?? Guid.NewGuid().ToString("N"),
            StationId = station.Id,
            StationName = station.Label,
            StreamUrl = station.StreamUrl,
            NextStartUtcTicks = startUtc.Ticks,
            TimeZoneId = timeZoneId,
            DurationMinutes = duration,
            Recurrence = recurrence.Value,
            ActiveDays = days,
            OutputFolder = UsesCustomOutputFolder
                ? OutputFolderTextBox.Text.Trim()
                : string.Empty,
            WakeComputer = (WakeCombo.SelectedItem as WakeChoice)?.Value,
            Enabled = EnabledCheckBox.IsChecked == true
        };
        if (!immediateStart && startUtc <= DateTime.UtcNow)
        {
            if (schedule.Recurrence == RadioScheduleRecurrence.Once)
            {
                ShowError("Jednorazowe nagranie musi rozpoczynać się w przyszłości", DateTextBox);
                return;
            }
            var next = RadioScheduleCalculator.FindNextStartUtc(schedule, DateTime.UtcNow);
            if (next is null)
            {
                ShowError("Nie można wyznaczyć następnego terminu", DateTextBox);
                return;
            }
            schedule.NextStartUtcTicks = next.Value.Ticks;
        }
        if (UsesCustomOutputFolder
            && (string.IsNullOrWhiteSpace(schedule.OutputFolder)
                || !Path.IsPathFullyQualified(schedule.OutputFolder)))
        {
            ShowError("Folder dla tego planu musi zawierać pełną ścieżkę", OutputFolderTextBox);
            return;
        }
        ResultSchedule = schedule;
        DialogResult = true;
    }

    private static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(
            value.Trim(),
            ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private void ShowError(string message, Control control)
    {
        ValidationText.Text = message;
        control.Focus();
        Keyboard.Focus(control);
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder nagrań",
            Multiselect = false
        };
        if (Directory.Exists(OutputFolderTextBox.Text)) dialog.InitialDirectory = OutputFolderTextBox.Text;
        if (dialog.ShowDialog(this) != true) return;
        OutputFolderModeCombo.SelectedIndex = 1;
        OutputFolderTextBox.Text = dialog.FolderName;
        OutputFolderTextBox.Focus();
        Keyboard.Focus(OutputFolderTextBox);
    }

    private void RecurrenceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateDaysEnabled();

    private void ImmediateStartCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateStartControlsEnabled();

    private void OutputFolderMode_Changed(object sender, RoutedEventArgs e) => UpdateOutputFolderControls();

    private void UpdateOutputFolderControls()
    {
        if (OutputFolderTextBox is null || BrowseOutputFolderButton is null) return;
        var custom = UsesCustomOutputFolder;
        OutputFolderTextBox.IsEnabled = custom;
        BrowseOutputFolderButton.IsEnabled = custom;
    }

    private bool UsesCustomOutputFolder =>
        (OutputFolderModeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Custom";

    private void UpdateStartControlsEnabled()
    {
        if (DateTextBox is null || TimeTextBox is null || EnabledCheckBox is null
            || ImmediateStartExplanation is null) return;
        var immediate = ImmediateStartCheckBox.Visibility == Visibility.Visible
            && ImmediateStartCheckBox.IsChecked == true;
        DateTextBox.IsEnabled = !immediate;
        TimeTextBox.IsEnabled = !immediate;
        EnabledCheckBox.IsEnabled = !immediate;
        if (immediate) EnabledCheckBox.IsChecked = true;
        ImmediateStartExplanation.Text = immediate
            ? "Pierwsze nagranie rozpocznie się po zapisaniu. Data i godzina są pomijane; plan cykliczny powtórzy się o godzinie rozpoczęcia pierwszego nagrania."
            : "Pierwsze nagranie rozpocznie się w podanej dacie i godzinie.";
    }

    private void UpdateDaysEnabled()
    {
        if (DaysGroup is null) return;
        DaysGroup.IsEnabled = (RecurrenceCombo.SelectedItem as RecurrenceChoice)?.Value
            == RadioScheduleRecurrence.SelectedDays;
    }

    private void DaysList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || DaysList.SelectedItem is not DayChoice choice) return;
        choice.IsChecked = !choice.IsChecked;
        DaysStatus.Text = $"{choice.Label}: {(choice.IsChecked ? "zaznaczony" : "odznaczony")}";
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        e.Handled = true;
    }

    private sealed record StationChoice(string Id, string Label, string StreamUrl)
    {
        public override string ToString() => Label;
    }

    private sealed record RecurrenceChoice(RadioScheduleRecurrence Value, string Label)
    {
        public static IReadOnlyList<RecurrenceChoice> All { get; } =
        [
            new(RadioScheduleRecurrence.Once, "Jednorazowo"),
            new(RadioScheduleRecurrence.Daily, "Codziennie"),
            new(RadioScheduleRecurrence.SelectedDays, "W wybrane dni tygodnia")
        ];
        public override string ToString() => Label;
    }

    private sealed record WakeChoice(bool? Value, string Label)
    {
        public static IReadOnlyList<WakeChoice> Create(bool globalWakeEnabled) =>
        [
            new(
                null,
                globalWakeEnabled
                    ? "Zgodnie z ustawieniem ogólnym: wybudzaj komputer"
                    : "Zgodnie z ustawieniem ogólnym: nie wybudzaj komputera"),
            new(true, "Wybudzaj komputer"),
            new(false, "Nie wybudzaj komputera")
        ];
        public override string ToString() => Label;
    }

    private sealed class DayChoice(DayOfWeek value, string label) : INotifyPropertyChanged
    {
        private bool _isChecked;

        public DayOfWeek Value { get; } = value;
        public string Label { get; } = label;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AccessibleLabel));
            }
        }
        public string AccessibleLabel => $"{Label}, {(IsChecked ? "zaznaczony" : "niezaznaczony")}";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public override string ToString() => AccessibleLabel;
    }
}
