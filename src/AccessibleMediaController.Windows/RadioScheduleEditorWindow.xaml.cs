using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Controls;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class RadioScheduleEditorWindow : Window
{
    private readonly RadioRecordingScheduleSettings? _existing;
    private readonly IReadOnlyList<StationChoice> _stations;
    private readonly IReadOnlyList<DayChoice> _dayChoices;
    private readonly System.Windows.Forms.DateTimePicker _datePicker;
    private readonly System.Windows.Forms.DateTimePicker _timePicker;
    private readonly System.Windows.Forms.NumericUpDown _durationHoursPicker;
    private readonly System.Windows.Forms.NumericUpDown _durationMinutesPicker;
    private readonly System.Windows.Forms.NumericUpDown _splitMinutesPicker;
    private readonly SegmentedDateTimeDigitEditor _dateDigitEditor =
        new(SegmentedDateTimeField.Date);
    private readonly SegmentedDateTimeDigitEditor _timeDigitEditor =
        new(SegmentedDateTimeField.Time);
    private int _dateSegmentIndex;
    private int _timeSegmentIndex;
    private bool _movingSegmentProgrammatically;
    private bool _followStartDateWithDefaultDay;

    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const int VirtualKeyRight = 0x27;

    public RadioRecordingScheduleSettings? ResultSchedule { get; private set; }

    public RadioScheduleEditorWindow(
        IReadOnlyCollection<MediaItem> stations,
        RadioRecordingScheduleSettings? existing,
        string? preferredStationId,
        DateTime? initialStartUtc = null,
        bool offerImmediateStart = false,
        bool globalWakeEnabled = false,
        RadioRecordingFormat defaultRecordingFormat = RadioRecordingFormat.Mp3,
        int defaultRecordingBitrateKbps = 192)
    {
        InitializeComponent();
        _datePicker = CreateDatePicker();
        _timePicker = CreateTimePicker();
        _durationHoursPicker = CreateDurationHoursPicker();
        _durationMinutesPicker = CreateDurationMinutesPicker();
        _splitMinutesPicker = CreateSplitMinutesPicker();
        DatePickerHost.Child = _datePicker;
        TimePickerHost.Child = _timePicker;
        DurationHoursPickerHost.Child = _durationHoursPicker;
        DurationMinutesPickerHost.Child = _durationMinutesPicker;
        SplitMinutesPickerHost.Child = _splitMinutesPicker;
        _datePicker.KeyDown += HostedInput_KeyDown;
        _timePicker.KeyDown += HostedInput_KeyDown;
        _durationHoursPicker.KeyDown += HostedInput_KeyDown;
        _durationMinutesPicker.KeyDown += HostedInput_KeyDown;
        _splitMinutesPicker.KeyDown += HostedInput_KeyDown;
        _datePicker.Leave += (_, _) => _dateDigitEditor.Reset();
        _timePicker.Leave += (_, _) => _timeDigitEditor.Reset();
        EditableFieldSelection.Attach(_durationHoursPicker);
        EditableFieldSelection.Attach(_durationMinutesPicker);
        EditableFieldSelection.Attach(_splitMinutesPicker);
        _datePicker.ValueChanged += DatePicker_ValueChanged;
        _timePicker.ValueChanged += (_, _) => UpdateFileNamePreview();
        StationCombo.SelectionChanged += (_, _) => UpdateFileNamePreview();
        _existing = existing;
        _followStartDateWithDefaultDay = existing is null;
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
        SplitModeCombo.ItemsSource = SplitModeChoice.All;
        RecordingFormatCombo.ItemsSource = RecordingFormatChoice.All;
        RecordingBitrateCombo.ItemsSource = RecordingBitrateChoice.All;
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
        _datePicker.Value = local.Date;
        _timePicker.Value = DateTime.Today + local.TimeOfDay;
        var duration = SplitDurationMinutes(existing?.DurationMinutes ?? 60);
        _durationHoursPicker.Value = duration.Hours;
        _durationMinutesPicker.Value = duration.Minutes;
        var segmentMinutes = existing?.SegmentMinutes ?? 0;
        _splitMinutesPicker.Value = Math.Clamp(segmentMinutes > 0 ? segmentMinutes : 30, 1, 10_080);
        SplitModeCombo.SelectedItem = SplitModeChoice.All.First(choice =>
            choice.Split == (segmentMinutes > 0));
        OutputFolderTextBox.Text = existing?.OutputFolder ?? string.Empty;
        var customOutputFolder = !string.IsNullOrWhiteSpace(existing?.OutputFolder);
        OutputFolderModeCombo.SelectedIndex = customOutputFolder ? 1 : 0;
        EnabledCheckBox.IsChecked = existing?.Enabled ?? true;
        RecurrenceCombo.SelectedItem = RecurrenceChoice.All.First(choice =>
            choice.Value == (existing?.Recurrence ?? RadioScheduleRecurrence.Once));
        WakeCombo.SelectedItem = wakeChoices.First(choice => choice.Value == existing?.WakeComputer);
        var recordingFormat = existing?.RecordingFormat ?? defaultRecordingFormat;
        RecordingFormatCombo.SelectedItem = RecordingFormatChoice.All.First(choice =>
            choice.Value == recordingFormat);
        var recordingBitrate = existing?.RecordingBitrateKbps ?? defaultRecordingBitrateKbps;
        RecordingBitrateCombo.SelectedItem = RecordingBitrateChoice.All.MinBy(choice =>
            Math.Abs(choice.Value - recordingBitrate));
        FileNameTemplateTextBox.Text = RadioRecordingFileNameTemplate.NormalizeOrDefault(
            existing?.FileNameTemplate);

        var stationId = existing?.StationId ?? preferredStationId;
        StationCombo.SelectedItem = _stations.FirstOrDefault(choice => choice.Id == stationId)
            ?? _stations.FirstOrDefault();
        var showStartMode = existing is null;
        StartModeLabel.Visibility = showStartMode ? Visibility.Visible : Visibility.Collapsed;
        StartModeCombo.Visibility = showStartMode ? Visibility.Visible : Visibility.Collapsed;
        ImmediateStartExplanation.Visibility = showStartMode ? Visibility.Visible : Visibility.Collapsed;
        StartModeCombo.SelectedIndex = offerImmediateStart ? 0 : 1;
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
        UpdateSplitControls();
        UpdateRecordingBitrateEnabled();
        UpdateOutputFolderControls();
        UpdateFileNamePreview();
        Loaded += (_, _) =>
        {
            if (_existing is null)
            {
                StationCombo.Focus();
                Keyboard.Focus(StationCombo);
            }
            else
            {
                _datePicker.Focus();
            }
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
        var immediateStart = IsImmediateStart;
        var date = _datePicker.Value.Date;
        var time = ScheduleTimeWithoutHiddenSeconds(_timePicker.Value);
        var duration = CombineDurationMinutes(
            ReadDurationPickerValue(_durationHoursPicker),
            ReadDurationPickerValue(_durationMinutesPicker));
        if (duration <= 0)
        {
            ShowHostedError(
                "Długość nagrania musi wynosić co najmniej jedną minutę",
                _durationMinutesPicker);
            return;
        }
        var segmentMinutes = UsesSplit
            ? decimal.ToInt32(_splitMinutesPicker.Value)
            : 0;
        if (segmentMinutes >= duration)
        {
            ShowHostedError(
                "Długość części musi być krótsza niż całe nagranie",
                _splitMinutesPicker);
            return;
        }
        if (RecurrenceCombo.SelectedItem is not RecurrenceChoice recurrence)
        {
            ShowError("Wybierz sposób powtarzania", RecurrenceCombo);
            return;
        }
        if (RecordingFormatCombo.SelectedItem is not RecordingFormatChoice recordingFormat)
        {
            ShowError("Wybierz format nagrania", RecordingFormatCombo);
            return;
        }
        if (RecordingBitrateCombo.SelectedItem is not RecordingBitrateChoice recordingBitrate)
        {
            ShowError("Wybierz bitrate nagrania", RecordingBitrateCombo);
            return;
        }
        var fileNameTemplate = FileNameTemplateTextBox.Text.Trim();
        if (!RadioRecordingFileNameTemplate.TryValidate(fileNameTemplate, out var fileNameError))
        {
            ShowError(fileNameError, FileNameTemplateTextBox);
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
            SegmentMinutes = segmentMinutes,
            Recurrence = recurrence.Value,
            ActiveDays = days,
            OutputFolder = UsesCustomOutputFolder
                ? OutputFolderTextBox.Text.Trim()
                : string.Empty,
            FileNameTemplate = fileNameTemplate,
            RecordingFormat = recordingFormat.Value,
            RecordingBitrateKbps = recordingBitrate.Value,
            WakeComputer = (WakeCombo.SelectedItem as WakeChoice)?.Value,
            Enabled = EnabledCheckBox.IsChecked == true
        };
        if (!immediateStart && startUtc <= DateTime.UtcNow)
        {
            if (schedule.Recurrence == RadioScheduleRecurrence.Once)
            {
                ShowHostedError("Jednorazowe nagranie musi rozpoczynać się w przyszłości", _datePicker);
                return;
            }
            var next = RadioScheduleCalculator.FindNextStartUtc(schedule, DateTime.UtcNow);
            if (next is null)
            {
                ShowHostedError("Nie można wyznaczyć następnego terminu", _datePicker);
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

    private void ShowError(string message, Control control)
    {
        ValidationText.Text = message;
        control.Focus();
        Keyboard.Focus(control);
    }

    private void ShowHostedError(string message, System.Windows.Forms.Control control)
    {
        ValidationText.Text = message;
        control.Focus();
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

    private void DatePicker_ValueChanged(object? sender, EventArgs e)
    {
        UpdateFileNamePreview();
        if (!_followStartDateWithDefaultDay || _existing is not null || _dayChoices is null) return;

        SelectOnlyStartDateDay(_datePicker.Value.DayOfWeek);
    }

    private void SelectOnlyStartDateDay(DayOfWeek day)
    {
        DayChoice? selected = null;
        foreach (var choice in _dayChoices)
        {
            choice.IsChecked = choice.Value == day;
            if (choice.IsChecked) selected = choice;
        }

        if (selected is not null && DaysList is not null)
        {
            DaysList.SelectedItem = selected;
            DaysList.ScrollIntoView(selected);
        }
    }

    private void StartModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateStartControlsEnabled();

    private void SplitModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSplitControls();

    private void RecordingFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRecordingBitrateEnabled();
        UpdateFileNamePreview();
    }

    private void FileNameTemplateTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateFileNamePreview();

    private void FileNameMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileNameMenuButton.ContextMenu is null) return;
        FileNameMenuButton.ContextMenu.PlacementTarget = FileNameMenuButton;
        FileNameMenuButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        FileNameMenuButton.ContextMenu.IsOpen = true;
    }

    private void FileNameContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        Dispatcher.BeginInvoke(
            () => (menu.Items.OfType<MenuItem>().FirstOrDefault())?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void InsertFileNameToken_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string token }) return;
        var start = FileNameTemplateTextBox.SelectionStart;
        var length = FileNameTemplateTextBox.SelectionLength;
        var current = FileNameTemplateTextBox.Text;
        FileNameTemplateTextBox.Text = current.Remove(start, length).Insert(start, token);
        RestoreFileNameTemplateFocus(start + token.Length);
    }

    private void UseFileNameTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string template }) return;
        FileNameTemplateTextBox.Text = template;
        RestoreFileNameTemplateFocus(template.Length);
    }

    private void RestoreFileNameTemplateFocus(int caretIndex)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                FileNameTemplateTextBox.Focus();
                Keyboard.Focus(FileNameTemplateTextBox);
                var safeCaretIndex = Math.Clamp(
                    caretIndex,
                    0,
                    FileNameTemplateTextBox.Text.Length);
                FileNameTemplateTextBox.Select(safeCaretIndex, 0);
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void UpdateFileNamePreview()
    {
        if (FileNamePreview is null || FileNameTemplateTextBox is null) return;
        if (!RadioRecordingFileNameTemplate.TryValidate(FileNameTemplateTextBox.Text, out var error))
        {
            FileNamePreview.Text = $"Podgląd niedostępny: {error}.";
            return;
        }

        var stationName = (StationCombo?.SelectedItem as StationChoice)?.Label ?? "Nazwa stacji";
        var date = _datePicker?.Value.Date ?? DateTime.Today;
        var time = _timePicker?.Value.TimeOfDay ?? DateTime.Now.TimeOfDay;
        var baseName = RadioRecordingFileNameTemplate.Expand(
            FileNameTemplateTextBox.Text,
            stationName,
            date + time,
            partNumber: 1);
        var format = (RecordingFormatCombo?.SelectedItem as RecordingFormatChoice)?.Value;
        FileNamePreview.Text = format switch
        {
            RadioRecordingFormat.Mp3 => $"Przykład: {baseName}.mp3",
            RadioRecordingFormat.Aac => $"Przykład: {baseName}.m4a",
            RadioRecordingFormat.Flac => $"Przykład: {baseName}.flac",
            RadioRecordingFormat.Wav => $"Przykład: {baseName}.wav",
            RadioRecordingFormat.Original => $"Przykład: {baseName}; rozszerzenie strumienia zostanie dodane automatycznie.",
            _ => $"Przykład: {baseName}; rozszerzenie zostanie dodane automatycznie."
        };
    }

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

    private bool UsesSplit =>
        (SplitModeCombo?.SelectedItem as SplitModeChoice)?.Split == true;

    private bool IsImmediateStart =>
        StartModeCombo.Visibility == Visibility.Visible
        && (StartModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Immediate";

    private void UpdateStartControlsEnabled()
    {
        if (DatePickerHost is null || TimePickerHost is null || EnabledCheckBox is null
            || ImmediateStartExplanation is null) return;
        var immediate = IsImmediateStart;
        DatePickerHost.IsEnabled = !immediate;
        TimePickerHost.IsEnabled = !immediate;
        _datePicker.Enabled = !immediate;
        _timePicker.Enabled = !immediate;
        EnabledCheckBox.IsEnabled = !immediate;
        if (immediate) EnabledCheckBox.IsChecked = true;
        ImmediateStartExplanation.Text = immediate
            ? "Nagrywanie rozpocznie się natychmiast po wybraniu Zapisz. Data i godzina są pomijane; harmonogram cykliczny powtórzy się o godzinie rozpoczęcia pierwszego nagrania."
            : "Nagrywanie rozpocznie się później, w podanej dacie i godzinie. Można wpisać kolejno cyfry całej daty lub czasu; po ukończeniu dnia, miesiąca albo godziny program przechodzi do następnej części. Lewo lub prawo wybiera część, a góra lub dół zmienia jej wartość.";
    }

    private void UpdateSplitControls()
    {
        if (SplitMinutesPanel is null || _splitMinutesPicker is null) return;
        SplitMinutesPanel.IsEnabled = UsesSplit;
        _splitMinutesPicker.Enabled = UsesSplit;
    }

    private void UpdateRecordingBitrateEnabled()
    {
        if (RecordingBitrateCombo is null) return;
        RecordingBitrateCombo.IsEnabled = (RecordingFormatCombo?.SelectedItem as RecordingFormatChoice)?.Value
            is RadioRecordingFormat.Mp3 or RadioRecordingFormat.Aac;
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
        _followStartDateWithDefaultDay = false;
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

    private void HostedInput_KeyDown(object? sender, System.Windows.Forms.KeyEventArgs e)
    {
        if (e.KeyCode == System.Windows.Forms.Keys.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }
        if (e.KeyCode == System.Windows.Forms.Keys.Enter)
        {
            Save_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (!_movingSegmentProgrammatically
            && e.Modifiers == System.Windows.Forms.Keys.None
            && SegmentedDateTimeDigitEditor.TryGetDigit(e.KeyCode, out var digit))
        {
            if (sender == _datePicker)
            {
                HandleSegmentDigit(
                    _datePicker,
                    _dateDigitEditor,
                    ref _dateSegmentIndex,
                    digit,
                    FormatDateSegment,
                    isDate: true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (sender == _timePicker)
            {
                HandleSegmentDigit(
                    _timePicker,
                    _timeDigitEditor,
                    ref _timeSegmentIndex,
                    digit,
                    FormatTimeSegment,
                    isDate: false);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
        }

        if (sender == _datePicker && IsSegmentNavigationKey(e.KeyCode))
        {
            _dateDigitEditor.Reset();
            _dateSegmentIndex = MoveSegment(
                _dateSegmentIndex,
                segmentCount: 3,
                e.KeyCode);
            var includeSegmentName = IsSegmentSelectionKey(e.KeyCode);
            AnnounceHostedValueAfterKey(() => includeSegmentName
                ? FormatDateSegment(
                    _datePicker.Value,
                    _dateSegmentIndex,
                    includeSegmentName: true)
                : FormatAdjustedDate(_datePicker.Value, _dateSegmentIndex));
            return;
        }

        if (sender == _timePicker && IsSegmentNavigationKey(e.KeyCode))
        {
            _timeDigitEditor.Reset();
            _timeSegmentIndex = MoveSegment(
                _timeSegmentIndex,
                segmentCount: 2,
                e.KeyCode);
            var includeSegmentName = IsSegmentSelectionKey(e.KeyCode);
            AnnounceHostedValueAfterKey(() => FormatTimeSegment(
                _timePicker.Value,
                _timeSegmentIndex,
                includeSegmentName));
        }
    }

    private void HandleSegmentDigit(
        System.Windows.Forms.DateTimePicker picker,
        SegmentedDateTimeDigitEditor editor,
        ref int segmentIndex,
        int digit,
        Func<DateTime, int, bool, string> segmentFormatter,
        bool isDate)
    {
        var result = editor.EnterDigit(
            picker.Value,
            segmentIndex,
            digit,
            DateTime.UtcNow,
            picker.MinDate,
            picker.MaxDate);
        if (!result.IsComplete) return;

        if (!result.IsValid)
        {
            DateTimeStatus.Announce(FormatInvalidSegment(
                segmentIndex,
                result.EnteredValue,
                isDate));
            return;
        }

        picker.Value = result.Value;
        if (result.MoveNext)
        {
            segmentIndex++;
            MoveNativeSegmentRight(picker);
        }

        var announcedSegment = segmentIndex;
        AnnounceHostedValueAfterKey(() => segmentFormatter(
            picker.Value,
            announcedSegment,
            true));
    }

    private void MoveNativeSegmentRight(System.Windows.Forms.DateTimePicker picker)
    {
        _movingSegmentProgrammatically = true;
        try
        {
            SendMessage(picker.Handle, WmKeyDown, (nint)VirtualKeyRight, 0);
            SendMessage(picker.Handle, WmKeyUp, (nint)VirtualKeyRight, 0);
        }
        finally
        {
            _movingSegmentProgrammatically = false;
        }
    }

    private static string FormatInvalidSegment(int segmentIndex, int enteredValue, bool isDate)
    {
        if (!isDate)
        {
            return segmentIndex == 0
                ? $"Nieprawidłowa godzina: {enteredValue:D2}. Wpisz dwie cyfry od 00 do 23."
                : $"Nieprawidłowe minuty: {enteredValue:D2}. Wpisz dwie cyfry od 00 do 59.";
        }

        return segmentIndex switch
        {
            0 => $"Nieprawidłowy dzień: {enteredValue:D2}. Wpisz dwie cyfry dnia istniejącego w wybranym miesiącu.",
            1 => $"Nieprawidłowy miesiąc: {enteredValue:D2}. Wpisz dwie cyfry od 01 do 12.",
            _ => $"Nieprawidłowy rok: {enteredValue:D4}. Wpisz cztery cyfry roku obsługiwanego przez kalendarz."
        };
    }

    private static bool IsSegmentNavigationKey(System.Windows.Forms.Keys key) =>
        key is System.Windows.Forms.Keys.Left
            or System.Windows.Forms.Keys.Right
            or System.Windows.Forms.Keys.Up
            or System.Windows.Forms.Keys.Down;

    private static bool IsSegmentSelectionKey(System.Windows.Forms.Keys key) =>
        key is System.Windows.Forms.Keys.Left or System.Windows.Forms.Keys.Right;

    private static int MoveSegment(int current, int segmentCount, System.Windows.Forms.Keys key) =>
        key switch
        {
            System.Windows.Forms.Keys.Left => Math.Max(0, current - 1),
            System.Windows.Forms.Keys.Right => Math.Min(segmentCount - 1, current + 1),
            _ => current
        };

    private void AnnounceHostedValueAfterKey(Func<string> messageFactory)
    {
        Dispatcher.BeginInvoke(
            () => DateTimeStatus.Announce(messageFactory()),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private static string FormatDateSegment(DateTime value, int segmentIndex, bool includeSegmentName)
    {
        if (!includeSegmentName)
        {
            return segmentIndex switch
            {
                0 => value.Day.ToString(CultureInfo.InvariantCulture),
                1 => value.Month.ToString(CultureInfo.InvariantCulture),
                _ => value.Year.ToString(CultureInfo.InvariantCulture)
            };
        }

        return segmentIndex switch
        {
            0 => $"Dzień: {value.Day}",
            1 => $"Miesiąc: {value.Month}, {value.ToString("MMMM", CultureInfo.GetCultureInfo("pl-PL"))}",
            _ => $"Rok: {value.Year}"
        };
    }

    internal static string FormatAdjustedDate(DateTime value, int segmentIndex)
    {
        var culture = CultureInfo.GetCultureInfo("pl-PL");
        var date = segmentIndex switch
        {
            0 => value.ToString("dd.MM", CultureInfo.InvariantCulture),
            1 => value.ToString("d MMMM", culture),
            _ => value.ToString("d MMMM yyyy", culture)
        };
        return $"{date}, {value.ToString("dddd", culture)}";
    }

    private static string FormatTimeSegment(DateTime value, int segmentIndex, bool includeSegmentName)
    {
        if (!includeSegmentName)
        {
            return (segmentIndex == 0 ? value.Hour : value.Minute)
                .ToString(CultureInfo.InvariantCulture);
        }

        return segmentIndex switch
        {
            0 => $"Godzina: {value.Hour}",
            _ => $"Minuty: {value.Minute}"
        };
    }

    private static System.Windows.Forms.DateTimePicker CreateDatePicker() => new()
    {
        AccessibleName = "Data pierwszego nagrania",
        AccessibleDescription = "Wpisz kolejno dwie cyfry dnia, dwie miesiąca i cztery roku. Po ukończeniu części program przechodzi dalej. Lewo i prawo wybiera część. Góra i dół zmienia jej wartość oraz podaje wybraną datę i dzień tygodnia.",
        AccessibleRole = System.Windows.Forms.AccessibleRole.SpinButton,
        CustomFormat = "dd.MM.yyyy",
        Format = System.Windows.Forms.DateTimePickerFormat.Custom,
        ShowUpDown = true,
        Dock = System.Windows.Forms.DockStyle.Fill,
        TabStop = true
    };

    private static System.Windows.Forms.DateTimePicker CreateTimePicker() => new()
    {
        AccessibleName = "Godzina rozpoczęcia",
        AccessibleDescription = "Wpisz cztery cyfry bez dwukropka, na przykład 2310. Po dwóch cyfrach godziny program przechodzi do minut. Lewo i prawo wybiera część. Góra i dół zmienia jej wartość.",
        AccessibleRole = System.Windows.Forms.AccessibleRole.SpinButton,
        CustomFormat = "HH:mm",
        Format = System.Windows.Forms.DateTimePickerFormat.Custom,
        ShowUpDown = true,
        Dock = System.Windows.Forms.DockStyle.Fill,
        TabStop = true
    };

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, uint message, nint wordParameter, nint longParameter);

    private static System.Windows.Forms.NumericUpDown CreateDurationHoursPicker() => new EmptyMeansZeroNumericUpDown
    {
        AccessibleName = "Długość nagrania, godziny",
        AccessibleDescription = "Wpisz liczbę pełnych godzin albo zmień ją strzałkami w górę i w dół. Dla nagrania krótszego niż godzina pozostaw zero.",
        AccessibleRole = System.Windows.Forms.AccessibleRole.SpinButton,
        Minimum = 0,
        Maximum = 168,
        Value = 1,
        Dock = System.Windows.Forms.DockStyle.Fill,
        TabStop = true,
        ThousandsSeparator = false
    };

    private static System.Windows.Forms.NumericUpDown CreateDurationMinutesPicker() => new EmptyMeansZeroNumericUpDown
    {
        AccessibleName = "Długość nagrania, minuty",
        AccessibleDescription = "Wpisz minuty od zera do pięćdziesięciu dziewięciu albo zmień je strzałkami w górę i w dół.",
        AccessibleRole = System.Windows.Forms.AccessibleRole.SpinButton,
        Minimum = 0,
        Maximum = 59,
        Value = 0,
        Dock = System.Windows.Forms.DockStyle.Fill,
        TabStop = true,
        ThousandsSeparator = false
    };

    internal static (int Hours, int Minutes) SplitDurationMinutes(int totalMinutes)
    {
        var normalized = Math.Clamp(totalMinutes, 1, 10_080);
        return (normalized / 60, normalized % 60);
    }

    internal static int CombineDurationMinutes(int hours, int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hours);
        if (minutes is < 0 or > 59) throw new ArgumentOutOfRangeException(nameof(minutes));
        return checked(hours * 60 + minutes);
    }

    internal static TimeSpan ScheduleTimeWithoutHiddenSeconds(DateTime pickerValue) =>
        new(pickerValue.Hour, pickerValue.Minute, 0);

    internal static int ReadDurationPickerValue(System.Windows.Forms.NumericUpDown picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        return string.IsNullOrWhiteSpace(picker.Text)
            ? 0
            : decimal.ToInt32(picker.Value);
    }

    private static System.Windows.Forms.NumericUpDown CreateSplitMinutesPicker() => new()
    {
        AccessibleName = "Długość jednej części w minutach",
        AccessibleDescription = "Pole jest dostępne po wybraniu dzielenia na części. Wpisz liczbę minut albo zmień ją strzałkami w górę i w dół.",
        AccessibleRole = System.Windows.Forms.AccessibleRole.SpinButton,
        Minimum = 1,
        Maximum = 10_080,
        Value = 30,
        Dock = System.Windows.Forms.DockStyle.Fill,
        TabStop = true,
        ThousandsSeparator = false
    };

    private sealed class EmptyMeansZeroNumericUpDown : System.Windows.Forms.NumericUpDown
    {
        protected override void ValidateEditText()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                Value = 0;
                return;
            }

            base.ValidateEditText();
        }
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

    private sealed record SplitModeChoice(bool Split, string Label)
    {
        public static IReadOnlyList<SplitModeChoice> All { get; } =
        [
            new(false, "Jeden plik"),
            new(true, "Dziel na części")
        ];
        public override string ToString() => Label;
    }

    private sealed record RecordingFormatChoice(RadioRecordingFormat Value, string Label)
    {
        public static IReadOnlyList<RecordingFormatChoice> All { get; } =
        [
            new(RadioRecordingFormat.Mp3, "MP3"),
            new(RadioRecordingFormat.Aac, "M4A, dźwięk AAC"),
            new(RadioRecordingFormat.Flac, "FLAC, bezstratny"),
            new(RadioRecordingFormat.Original, "Oryginalny strumień, bez konwersji"),
            new(RadioRecordingFormat.Wav, "WAV, bez kompresji")
        ];
        public override string ToString() => Label;
    }

    private sealed record RecordingBitrateChoice(int Value, string Label)
    {
        public static IReadOnlyList<RecordingBitrateChoice> All { get; } =
        [
            new(96, "96 kb/s"),
            new(128, "128 kb/s"),
            new(160, "160 kb/s"),
            new(192, "192 kb/s"),
            new(256, "256 kb/s"),
            new(320, "320 kb/s")
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
