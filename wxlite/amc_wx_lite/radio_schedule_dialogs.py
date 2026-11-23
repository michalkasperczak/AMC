"""Natywne okna zarzadzania harmonogramami nagrywania radia.

Listy i pola dostaja wylacznie gotowe polskie napisy. Surowe slowniki,
identyfikatory i nazwy enumow pozostaja w modelu i nie sa elementami kontrolek
dostepnosciowych.
"""

from __future__ import annotations

import os
import uuid
from collections.abc import Callable, Mapping, Sequence
from datetime import datetime, timedelta, timezone

import wx

from . import i18n
from .live_region import announce_status
from .radio_schedule_settings import (
    BITRATES,
    DAY_LABELS,
    DAY_NAMES,
    DEFAULT_FILE_NAME_TEMPLATE,
    ScheduleValidationError,
    clone_schedules,
    DOTNET_UNIX_EPOCH_TICKS,
    duplicate_schedule,
    local_start_from_utc_ticks,
    output_folder_is_valid,
    sanitize_schedule,
    schedules_from_host_status,
    utc_ticks_from_local,
    validate_file_name_template,
)
from .schedule_segments import (
    DATE_SEGMENTS,
    MONTH_NAMES,
    MONTH_NAMES_EN,
    TIME_SEGMENTS,
    DateSegments,
    TimeSegments,
    duration_from_fields,
)
from .state_store import Station
from .radio_recording import scheduled_recording_output_description

_RECURRENCE_CHOICES = (
    ("Jednorazowo", "Once"),
    ("Codziennie", "Daily"),
    ("W wybrane dni tygodnia", "SelectedDays"),
)
_FORMAT_CHOICES = (
    ("MP3", "Mp3"),
    ("AAC", "Aac"),
    ("FLAC", "Flac"),
    ("Oryginalny strumień bez konwersji", "Original"),
    ("WAV", "Wav"),
)
_WAKE_INHERIT = object()

# Jeden opis polecenia zasila menu listy, pomoc i cel wykonania klawisza.
_LIST_ACTIONS = (
    ("Insert", "Nowy plan", "dodaje plan", "_new", False),
    ("Enter", "Edytuj plan", "edytuje wybrany plan", "_edit", True),
    ("Ctrl+D", "Powiel plan", "tworzy wyłączoną kopię planu", "_duplicate", True),
    ("Spacja", "Włącz lub wyłącz plan", "przełącza cały plan", "_toggle", True),
    ("Shift+Spacja", "Pomiń najbliższy termin lub cofnij pominięcie",
     "pomija najbliższy przyszły termin włączonego planu lub cofa pominięcie",
     "_toggle_next_occurrence", True),
    ("strzałka w lewo", "Krótka informacja", "powtarza stan i termin",
     "_announce_short_information", True),
    ("strzałka w prawo", "Ustawienia pliku", "czyta ustawienia pliku",
     "_announce_full_information", True),
    ("Delete", "Usuń plan", "usuwa plan po potwierdzeniu", "_delete", True),
)


class _SegmentedDateControl(wx.Panel):
    """Trzy jawnie nazwane segmenty daty, bez tekstu ``RRRR-MM-DD``."""

    def __init__(self, parent, value, on_change: Callable | None = None) -> None:
        super().__init__(parent)
        self._state = DateSegments(value)
        self._on_change = on_change
        self._buffers = {segment: "" for segment in DATE_SEGMENTS}
        layout = wx.BoxSizer(wx.HORIZONTAL)
        values = (value.day, value.month, value.year)
        ranges = ((1, 31), (1, 12), (1900, 9999))
        self._controls = []
        for index, (segment, current, bounds) in enumerate(
            zip(DATE_SEGMENTS, values, ranges)
        ):
            control = wx.SpinCtrl(
                self,
                min=bounds[0],
                max=bounds[1],
                initial=current,
            )
            control.Bind(wx.EVT_CHAR_HOOK, self._on_key)
            control.Bind(wx.EVT_SPINCTRL, self._on_spin)
            control.Bind(wx.EVT_SET_FOCUS, self._on_focus)
            self._controls.append(control)
            layout.Add(control, 1, wx.EXPAND)
            if index < 2:
                layout.Add(wx.StaticText(self, label=" / "), 0, wx.ALIGN_CENTER_VERTICAL)
        self.SetSizer(layout)
        self._sync()

    def GetValue(self):
        return self._state.value

    def SetFocus(self) -> None:
        self._controls[0].SetFocus()

    def _index(self, event) -> int:
        control = event.GetEventObject()
        return self._controls.index(control) if control in self._controls else 0

    def _sync(self) -> None:
        values = (self._state.value.day, self._state.value.month, self._state.value.year)
        for control, segment, value in zip(self._controls, DATE_SEGMENTS, values):
            control.SetValue(value)
            control.SetName(i18n.ui_text(self._state.label(segment)))

    def _changed(self) -> None:
        self._sync()
        if self._on_change is not None:
            self._on_change(self._state.value)

    def _on_focus(self, event) -> None:
        self._buffers[DATE_SEGMENTS[self._index(event)]] = ""
        event.Skip()

    def _on_spin(self, event) -> None:
        index = self._index(event)
        segment = DATE_SEGMENTS[index]
        try:
            self._state = self._state.replace_segment(
                segment, self._controls[index].GetValue()
            )
        except ValueError:
            self._sync()
        else:
            self._changed()

    def _on_key(self, event) -> None:
        index = self._index(event)
        segment = DATE_SEGMENTS[index]
        code = event.GetKeyCode()
        if event.ControlDown() or event.AltDown():
            event.Skip()
            return
        if code in (wx.WXK_LEFT, wx.WXK_RIGHT):
            target = index + (-1 if code == wx.WXK_LEFT else 1)
            if 0 <= target < len(self._controls):
                self._controls[target].SetFocus()
            return
        if code in (wx.WXK_UP, wx.WXK_DOWN):
            self._state = self._state.adjust(
                segment, 1 if code == wx.WXK_UP else -1
            )
            self._buffers[segment] = ""
            self._changed()
            return
        if ord("0") <= code <= ord("9") and not event.ShiftDown():
            digit = chr(code)
            maximum = 4 if segment == "year" else 2
            prefix = (self._buffers[segment] + digit)[-maximum:]
            self._buffers[segment] = prefix
            try:
                self._state = self._state.replace_segment(segment, int(prefix))
            except ValueError:
                pass
            else:
                self._changed()
            if not self._state.may_extend(segment, prefix):
                self._buffers[segment] = ""
                if index + 1 < len(self._controls):
                    self._controls[index + 1].SetFocus()
            return
        event.Skip()


class _SegmentedTimeControl(wx.Panel):
    """Dwa jawnie nazwane segmenty czasu: godzina i minuty."""

    def __init__(self, parent, value) -> None:
        super().__init__(parent)
        self._state = TimeSegments(value)
        self._buffers = {segment: "" for segment in TIME_SEGMENTS}
        layout = wx.BoxSizer(wx.HORIZONTAL)
        self._controls = []
        for index, (segment, current, bounds) in enumerate((
            ("hour", value.hour, (0, 23)),
            ("minute", value.minute, (0, 59)),
        )):
            control = wx.SpinCtrl(
                self, min=bounds[0], max=bounds[1], initial=current
            )
            control.Bind(wx.EVT_CHAR_HOOK, self._on_key)
            control.Bind(wx.EVT_SPINCTRL, self._on_spin)
            control.Bind(wx.EVT_SET_FOCUS, self._on_focus)
            self._controls.append(control)
            layout.Add(control, 1, wx.EXPAND)
            if index == 0:
                layout.Add(wx.StaticText(self, label=" : "), 0, wx.ALIGN_CENTER_VERTICAL)
        self.SetSizer(layout)
        self._sync()

    def GetValue(self):
        return self._state.value

    def SetFocus(self) -> None:
        self._controls[0].SetFocus()

    def _index(self, event) -> int:
        control = event.GetEventObject()
        return self._controls.index(control) if control in self._controls else 0

    def _sync(self) -> None:
        values = (self._state.value.hour, self._state.value.minute)
        for control, segment, value in zip(self._controls, TIME_SEGMENTS, values):
            control.SetValue(value)
            control.SetName(i18n.ui_text(self._state.label(segment)))

    def _on_focus(self, event) -> None:
        self._buffers[TIME_SEGMENTS[self._index(event)]] = ""
        event.Skip()

    def _on_spin(self, event) -> None:
        index = self._index(event)
        segment = TIME_SEGMENTS[index]
        try:
            self._state = self._state.replace_segment(
                segment, self._controls[index].GetValue()
            )
        except ValueError:
            self._sync()
        else:
            self._sync()

    def _on_key(self, event) -> None:
        index = self._index(event)
        segment = TIME_SEGMENTS[index]
        code = event.GetKeyCode()
        if event.ControlDown() or event.AltDown():
            event.Skip()
            return
        if code in (wx.WXK_LEFT, wx.WXK_RIGHT):
            target = index + (-1 if code == wx.WXK_LEFT else 1)
            if 0 <= target < len(self._controls):
                self._controls[target].SetFocus()
            return
        if code in (wx.WXK_UP, wx.WXK_DOWN):
            self._state = self._state.adjust(
                segment, 1 if code == wx.WXK_UP else -1
            )
            self._buffers[segment] = ""
            self._sync()
            return
        if ord("0") <= code <= ord("9") and not event.ShiftDown():
            digit = chr(code)
            prefix = (self._buffers[segment] + digit)[-2:]
            self._buffers[segment] = prefix
            try:
                self._state = self._state.replace_segment(segment, int(prefix))
            except ValueError:
                pass
            else:
                self._sync()
            if not self._state.may_extend(segment, prefix):
                self._buffers[segment] = ""
                if index + 1 < len(self._controls):
                    self._controls[index + 1].SetFocus()
            return
        event.Skip()


def _choice_index(values: Sequence[object], wanted: object, default: int = 0) -> int:
    return next(
        (index for index, value in enumerate(values) if value == wanted), default
    )


def _display_name(schedule: Mapping) -> str:
    name = schedule.get("name")
    if isinstance(name, str) and name.strip():
        return name.strip()
    station = schedule.get("stationName")
    return (
        station.strip()
        if isinstance(station, str) and station.strip()
        else i18n.ui_text("Plan nagrywania")
    )


def _schedule_when(schedule: Mapping) -> tuple[str, str]:
    start = local_start_from_utc_ticks(int(schedule.get("nextStartUtcTicks") or 0))
    months = (
        MONTH_NAMES_EN
        if i18n.active_language() == i18n.ENGLISH
        else MONTH_NAMES
    )
    date_text = f"{start.day} {months[start.month - 1]} {start.year}"
    return date_text, f"{start.hour}:{start.minute:02d}"


def _duration_label(minutes: int) -> str:
    hours, rest = divmod(max(0, int(minutes)), 60)
    if i18n.active_language() == i18n.ENGLISH:
        parts = []
        if hours:
            parts.append(f"{hours} hour" if hours == 1 else f"{hours} hours")
        if rest:
            parts.append(f"{rest} minute" if rest == 1 else f"{rest} minutes")
        return " ".join(parts) or "zero minutes"
    parts = []
    if hours:
        hour_word = "godzina" if hours == 1 else "godziny" if 2 <= hours <= 4 else "godzin"
        parts.append(f"{hours} {hour_word}")
    if rest:
        minute_word = (
            "minuta"
            if rest == 1
            else "minuty"
            if 2 <= rest % 10 <= 4 and not 12 <= rest % 100 <= 14
            else "minut"
        )
        parts.append(f"{rest} {minute_word}")
    return " ".join(parts) or "zero minut"


def _short_schedule_information(schedule: Mapping) -> str:
    date_text, time_text = _schedule_when(schedule)
    if i18n.active_language() == i18n.ENGLISH:
        state = "enabled" if schedule.get("enabled") else "disabled"
    else:
        state = "włączony" if schedule.get("enabled") else "wyłączony"
    skipped = (
        schedule.get("enabled")
        and schedule.get("suppressedOccurrenceStartUtcTicks")
        == schedule.get("nextStartUtcTicks")
    )
    if skipped:
        state += (", this occurrence skipped" if i18n.active_language() == i18n.ENGLISH
                  else ", najbliższy termin pominięty")
    return f"{state}, {date_text}, {time_text}"


def _full_schedule_information(schedule: Mapping, recording=None) -> str:
    if recording is not None:
        return i18n.ui_text(
            scheduled_recording_output_description(schedule, recording)
        )
    english = i18n.active_language() == i18n.ENGLISH
    recurrence = str(schedule.get("recurrence") or "Once")
    if recurrence == "Daily":
        recurrence_text = "daily" if english else "codziennie"
    elif recurrence == "SelectedDays":
        selected = set(schedule.get("activeDays") or [])
        day_labels = (
            DAY_NAMES if english else DAY_LABELS
        )
        days = [label for name, label in zip(DAY_NAMES, day_labels) if name in selected]
        recurrence_text = (
            f"repeats: {', '.join(days)}"
            if english and days
            else f"powtarzanie: {', '.join(days)}"
            if days
            else "selected weekdays"
            if english
            else "wybrane dni tygodnia"
        )
    else:
        recurrence_text = "once" if english else "jednorazowo"
    format_value = str(schedule.get("recordingFormat") or "").strip()
    format_label = next(
        (label for label, value in _FORMAT_CHOICES if value == format_value),
        format_value or "format domyślny",
    )
    format_label = i18n.ui_text(format_label)
    folder = str(schedule.get("outputFolder") or "").strip()
    folder_text = (
        f"folder {folder}"
        if folder
        else "default recordings folder"
        if english
        else "domyślny folder nagrań"
    )
    station_name = str(
        schedule.get("stationName") or ("unknown" if english else "nieznane")
    ).strip()
    if english:
        return ", ".join((
            f"source {station_name}",
            recurrence_text,
            f"duration {_duration_label(int(schedule.get('durationMinutes') or 0))}",
            f"format {format_label}",
            folder_text,
        ))
    return ", ".join((
        f"źródło {station_name}",
        recurrence_text,
        f"długość {_duration_label(int(schedule.get('durationMinutes') or 0))}",
        f"format {format_label}",
        folder_text,
    ))


class RadioScheduleEditorDialog(wx.Dialog):
    """Dodawanie i edycja jednego planu w zwyklych kontrolkach Windows."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        stations: Sequence[Station],
        recording,
        global_wake: bool,
        existing: Mapping | None = None,
        preferred_station_id: str | None = None,
        initial_start: datetime | None = None,
    ) -> None:
        title = (
            "Edytuj plan nagrywania" if existing is not None else "Nowy plan nagrywania"
        )
        super().__init__(
            parent, title=title, style=wx.DEFAULT_DIALOG_STYLE | wx.RESIZE_BORDER
        )
        self._existing = (
            sanitize_schedule(dict(existing)) if existing is not None else None
        )
        self._stations = list(stations)
        if self._existing is not None and all(
            station.id != self._existing["stationId"] for station in self._stations
        ):
            self._stations.insert(
                0,
                Station(
                    id=self._existing["stationId"],
                    name=self._existing["stationName"],
                    url=self._existing["streamUrl"],
                ),
            )
        self._recording = recording
        self._global_wake = bool(global_wake)
        self._preferred_station_id = preferred_station_id
        self.result_schedule: dict | None = None
        self._folder_values: list[str] = []
        self._days_user_changed = False

        scroll = wx.ScrolledWindow(self, style=wx.VSCROLL)
        scroll.SetScrollRate(0, 12)
        grid = wx.FlexGridSizer(0, 2, 8, 10)
        grid.AddGrowableCol(1, 1)

        self.station = self._add_choice(
            scroll,
            grid,
            "&Stacja:",
            "Stacja do nagrania",
            [station.name for station in self._stations],
        )
        self.name = self._add_text(
            scroll,
            grid,
            "&Nazwa planu:",
            "Nazwa planu nagrywania",
            str((self._existing or {}).get("name") or ""),
        )

        if self._existing is not None:
            initial = local_start_from_utc_ticks(
                self._existing["nextStartUtcTicks"]
            )
        elif initial_start is not None:
            initial = initial_start.astimezone().replace(second=0, microsecond=0)
        else:
            initial = (
                datetime.now().astimezone().replace(second=0, microsecond=0)
                + timedelta(minutes=5)
            )
        self.start_date = self._add_segmented_date(
            scroll,
            grid,
            "&Data rozpoczęcia:",
            initial.date(),
            self._on_start_date_changed,
        )
        self.start_time = self._add_segmented_time(
            scroll,
            grid,
            "&Czas rozpoczęcia:",
            initial.time().replace(second=0, microsecond=0),
        )

        duration = int((self._existing or {}).get("durationMinutes") or 60)
        hours, minutes = divmod(duration, 60)
        self.duration_hours = self._add_text(
            scroll,
            grid,
            "Długość, &godziny:",
            "Długość nagrania w godzinach, puste oznacza zero godzin",
            str(hours) if hours else "",
        )
        self.duration_minutes = self._add_spin(
            scroll,
            grid,
            "Długość, &minuty:",
            "Dodatkowe minuty nagrania",
            minutes,
            0,
            59,
        )

        segment = int((self._existing or {}).get("segmentMinutes") or 0)
        self.split_mode = self._add_choice(
            scroll,
            grid,
            "Sposób &zapisu:",
            "Sposób zapisu nagrania",
            ["Jedno nagranie w jednym pliku", "Dziel nagranie na części"],
        )
        self.split_mode.SetSelection(1 if segment > 0 else 0)
        self.split_minutes = self._add_spin(
            scroll,
            grid,
            "Długość &części w minutach:",
            "Długość jednej części w minutach",
            segment or 30,
            1,
            10_080,
        )

        current_format = (self._existing or {}).get(
            "recordingFormat"
        ) or recording.format
        self.recording_format = self._add_choice(
            scroll,
            grid,
            "&Format pliku:",
            "Format tego nagrania",
            [label for label, _value in _FORMAT_CHOICES],
        )
        self._format_values = [value for _label, value in _FORMAT_CHOICES]
        self.recording_format.SetSelection(
            _choice_index(self._format_values, current_format)
        )
        current_bitrate = (self._existing or {}).get(
            "recordingBitrateKbps"
        ) or recording.bitrate_kbps
        self.bitrate = self._add_choice(
            scroll,
            grid,
            "&Bitrate MP3 lub AAC:",
            "Bitrate tego nagrania MP3 lub AAC",
            [f"{value} kilobitów na sekundę" for value in BITRATES],
        )
        self.bitrate.SetSelection(
            min(
                range(len(BITRATES)),
                key=lambda index: abs(BITRATES[index] - int(current_bitrate)),
            )
        )

        self.file_template = self._add_text(
            scroll,
            grid,
            "Nazwa &pliku:",
            "Szablon nazwy pliku nagrania",
            str(
                (self._existing or {}).get("fileNameTemplate")
                or DEFAULT_FILE_NAME_TEMPLATE
            ),
        )

        recurrence = str((self._existing or {}).get("recurrence") or "Once")
        self.recurrence = self._add_choice(
            scroll,
            grid,
            "&Powtarzanie:",
            "Powtarzanie nagrania",
            [label for label, _value in _RECURRENCE_CHOICES],
        )
        self._recurrence_values = [value for _label, value in _RECURRENCE_CHOICES]
        self.recurrence.SetSelection(_choice_index(self._recurrence_values, recurrence))

        days_label = wx.StaticText(scroll, label="Dni &tygodnia:")
        self.days = wx.CheckListBox(
            scroll,
            choices=[i18n.ui_text(label) for label in DAY_LABELS],
            style=wx.LB_SINGLE,
        )
        self.days.SetName("Dni tygodnia dla powtarzanego nagrania")
        selected_days = set((self._existing or {}).get("activeDays") or [])
        if existing is None:
            selected_days.add(DAY_NAMES[initial.weekday()])
        for index, day in enumerate(DAY_NAMES):
            self.days.Check(index, day in selected_days)
        grid.Add(days_label, 0, wx.ALIGN_TOP)
        grid.Add(self.days, 1, wx.EXPAND)

        self.folder_mode = self._add_choice(
            scroll, grid, "&Miejsce zapisu:", "Miejsce zapisu tego nagrania", []
        )
        self.folder_path = self._add_text(
            scroll,
            grid,
            "Ś&cieżka folderu:",
            "Ścieżka folderu nagrania",
            str((self._existing or {}).get("outputFolder") or ""),
        )
        self._custom_output_folder = self.folder_path.GetValue().strip()
        browse_label = wx.StaticText(scroll, label="")
        self.browse = wx.Button(scroll, label="&Wybierz folder…")
        self.browse.SetName("Wybierz folder nagrania")
        grid.Add(browse_label, 0)
        grid.Add(self.browse, 0, wx.ALIGN_LEFT)

        inherit_label = (
            "Jak ustawienie ogólne: wybudzaj komputer"
            if self._global_wake
            else "Jak ustawienie ogólne: nie wybudzaj komputera"
        )
        self.wake = self._add_choice(
            scroll,
            grid,
            "&Wybudzanie komputera:",
            "Wybudzanie komputera dla tego harmonogramu",
            [inherit_label, "Wybudzaj komputer", "Nie wybudzaj komputera"],
        )
        self._wake_values = [_WAKE_INHERIT, True, False]
        current_wake = (self._existing or {}).get("wakeComputer")
        self.wake.SetSelection(
            1 if current_wake is True else 2 if current_wake is False else 0
        )

        enabled_label = wx.StaticText(scroll, label="")
        self.enabled = wx.CheckBox(scroll, label="Plan &włączony")
        self.enabled.SetName("Plan włączony")
        self.enabled.SetValue((self._existing or {}).get("enabled") is not False)
        grid.Add(enabled_label, 0)
        grid.Add(self.enabled, 0, wx.EXPAND)

        self.validation = wx.StaticText(scroll, label="Gotowe")
        self.validation.SetName("Wynik sprawdzenia planu")
        grid.Add(wx.StaticText(scroll, label=""), 0)
        grid.Add(self.validation, 0, wx.EXPAND)

        scroll.SetSizer(grid)
        self._select_initial_station()
        self._update_enabled_controls()

        buttons = wx.StdDialogButtonSizer()
        self.save = wx.Button(self, wx.ID_OK, label="&Zapisz")
        self.save.SetName("Zapisz plan nagrywania")
        self.save.SetDefault()
        cancel = wx.Button(self, wx.ID_CANCEL, label="&Anuluj")
        cancel.SetName("Anuluj edycję planu nagrywania")
        buttons.AddButton(self.save)
        buttons.AddButton(cancel)
        buttons.Realize()

        shell = wx.BoxSizer(wx.VERTICAL)
        shell.Add(scroll, 1, wx.ALL | wx.EXPAND, 12)
        shell.Add(buttons, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self.SetMinSize((720, 680))
        self.SetSize((780, 760))

        self.station.Bind(wx.EVT_CHOICE, self._on_station_changed)
        self.days.Bind(wx.EVT_CHECKLISTBOX, self._on_days_changed)
        self.recurrence.Bind(wx.EVT_CHOICE, self._on_mode_changed)
        self.split_mode.Bind(wx.EVT_CHOICE, self._on_mode_changed)
        self.recording_format.Bind(wx.EVT_CHOICE, self._on_mode_changed)
        self.folder_mode.Bind(wx.EVT_CHOICE, self._on_folder_mode_changed)
        self.browse.Bind(wx.EVT_BUTTON, self._browse_folder)
        self.save.Bind(wx.EVT_BUTTON, self._save)
        if self._existing is None:
            self.station.SetFocus()
        else:
            self.start_date.SetFocus()

    @staticmethod
    def _add_choice(
        parent, grid, label: str, name: str, choices: Sequence[str]
    ) -> wx.Choice:
        static = wx.StaticText(parent, label=label)
        control = wx.Choice(
            parent,
            choices=[i18n.ui_text(choice) for choice in choices],
        )
        control.SetName(name)
        if choices:
            control.SetSelection(0)
        grid.Add(static, 0, wx.ALIGN_CENTER_VERTICAL)
        grid.Add(control, 1, wx.EXPAND)
        return control

    @staticmethod
    def _add_text(parent, grid, label: str, name: str, value: str) -> wx.TextCtrl:
        static = wx.StaticText(parent, label=label)
        control = wx.TextCtrl(parent, value=value)
        control.SetName(name)
        grid.Add(static, 0, wx.ALIGN_CENTER_VERTICAL)
        grid.Add(control, 1, wx.EXPAND)
        return control

    @staticmethod
    def _add_spin(
        parent, grid, label: str, name: str, value: int, minimum: int, maximum: int
    ) -> wx.SpinCtrl:
        static = wx.StaticText(parent, label=label)
        control = wx.SpinCtrl(parent, min=minimum, max=maximum, initial=value)
        control.SetName(name)
        grid.Add(static, 0, wx.ALIGN_CENTER_VERTICAL)
        grid.Add(control, 1, wx.EXPAND)
        return control

    @staticmethod
    def _add_segmented_date(parent, grid, label: str, value, on_change):
        static = wx.StaticText(parent, label=label)
        control = _SegmentedDateControl(parent, value, on_change)
        grid.Add(static, 0, wx.ALIGN_CENTER_VERTICAL)
        grid.Add(control, 1, wx.EXPAND)
        return control

    @staticmethod
    def _add_segmented_time(parent, grid, label: str, value):
        static = wx.StaticText(parent, label=label)
        control = _SegmentedTimeControl(parent, value)
        grid.Add(static, 0, wx.ALIGN_CENTER_VERTICAL)
        grid.Add(control, 1, wx.EXPAND)
        return control

    def _selected_station(self) -> Station | None:
        index = self.station.GetSelection()
        return self._stations[index] if 0 <= index < len(self._stations) else None

    def _select_initial_station(self) -> None:
        wanted = (self._existing or {}).get("stationId") or self._preferred_station_id
        index = next(
            (
                index
                for index, station in enumerate(self._stations)
                if station.id == wanted
            ),
            0 if self._stations else -1,
        )
        if index >= 0:
            self.station.SetSelection(index)
        self._rebuild_folder_choices()

    def _rebuild_folder_choices(self) -> None:
        station = self._selected_station()
        previous_index = self.folder_mode.GetSelection()
        previous_mode = (
            self._folder_values[previous_index]
            if 0 <= previous_index < len(self._folder_values)
            else None
        )
        if previous_mode == "custom":
            self._custom_output_folder = self.folder_path.GetValue().strip()
        default = self._recording.default_folder or "folder nagrań AMC"
        if i18n.active_language() == i18n.ENGLISH:
            labels = [f"Default recordings folder — {default}"]
        else:
            labels = [f"Domyślny folder nagrań — {default}"]
        values = ["default"]
        station_folder = (
            self._recording.station_folders.get(station.id)
            if station is not None
            else None
        )
        if station_folder:
            labels.append(
                f"This station's folder — {station_folder}"
                if i18n.active_language() == i18n.ENGLISH
                else f"Folder tej stacji — {station_folder}"
            )
            values.append("station")
        labels.append(
            "Custom folder for this schedule"
            if i18n.active_language() == i18n.ENGLISH
            else "Własny folder tego planu"
        )
        values.append("custom")

        existing_path = str((self._existing or {}).get("outputFolder") or "").strip()
        self._folder_values = values
        self.folder_mode.Set(labels)
        if previous_mode == "custom":
            self.folder_mode.SetSelection(len(values) - 1)
        elif previous_mode == "station" and station_folder:
            self.folder_mode.SetSelection(1)
        elif previous_mode == "default":
            self.folder_mode.SetSelection(0)
        elif (
            existing_path
            and station_folder
            and os.path.normcase(existing_path) == os.path.normcase(station_folder)
        ):
            self.folder_mode.SetSelection(1)
            self._custom_output_folder = ""
        elif existing_path:
            self.folder_mode.SetSelection(len(values) - 1)
            self._custom_output_folder = existing_path
        elif station_folder and self._recording.prefer_station_folder_in_new_schedules:
            self.folder_mode.SetSelection(1)
        else:
            self.folder_mode.SetSelection(0)
        self._update_folder_path()

    def _update_folder_path(self) -> None:
        station = self._selected_station()
        index = self.folder_mode.GetSelection()
        mode = (
            self._folder_values[index]
            if 0 <= index < len(self._folder_values)
            else "default"
        )
        custom = mode == "custom"
        if custom:
            self.folder_path.ChangeValue(self._custom_output_folder)
        else:
            if mode == "station" and station is not None:
                value = self._recording.station_folders.get(station.id, "")
            else:
                value = self._recording.default_folder or ""
            self.folder_path.ChangeValue(value)
        self.folder_path.Enable(custom)
        self.browse.Enable(custom)

    def _on_station_changed(self, _event) -> None:
        self._rebuild_folder_choices()

    def _on_days_changed(self, _event) -> None:
        self._days_user_changed = True

    def _on_start_date_changed(self, chosen_date) -> None:
        if self._existing is not None or self._days_user_changed:
            return
        wanted = chosen_date.weekday()
        for index in range(len(DAY_NAMES)):
            self.days.Check(index, index == wanted)

    def _on_folder_mode_changed(self, _event) -> None:
        self._update_folder_path()

    def _on_mode_changed(self, _event) -> None:
        self._update_enabled_controls()

    def _update_enabled_controls(self) -> None:
        recurrence = self._recurrence_values[self.recurrence.GetSelection()]
        self.days.Enable(recurrence == "SelectedDays")
        self.split_minutes.Enable(self.split_mode.GetSelection() == 1)
        selected_format = self._format_values[self.recording_format.GetSelection()]
        self.bitrate.Enable(selected_format in ("Mp3", "Aac"))

    def _browse_folder(self, _event) -> None:
        start = self.folder_path.GetValue().strip()
        style = getattr(wx, "DD_DIR_MUST_EXIST", 0)
        with wx.DirDialog(
            self,
            "Wybierz folder nagrań",
            defaultPath=start if start and os.path.isdir(start) else "",
            style=style,
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK:
                return
            self.folder_path.SetValue(dialog.GetPath())
        self.browse.SetFocus()

    def _show_error(self, message: str, control=None) -> None:
        message = i18n.ui_text(message)
        self.validation.SetLabel(message)
        self.validation.SetName(message)
        if control is not None:
            control.SetFocus()

    def _save(self, _event) -> None:
        station = self._selected_station()
        if station is None:
            self._show_error("Wybierz stację do nagrania", self.station)
            return
        try:
            local_start = datetime.combine(
                self.start_date.GetValue(), self.start_time.GetValue()
            )
            try:
                duration = duration_from_fields(
                    self.duration_hours.GetValue(),
                    self.duration_minutes.GetValue(),
                )
            except ValueError as error:
                raise ScheduleValidationError(str(error)) from error
            segment = (
                self.split_minutes.GetValue()
                if self.split_mode.GetSelection() == 1
                else 0
            )
            if segment >= duration:
                raise ScheduleValidationError(
                    "Długość części musi być krótsza niż całe nagranie"
                )
            recurrence = self._recurrence_values[self.recurrence.GetSelection()]
            active_days = [
                DAY_NAMES[index]
                for index in range(len(DAY_NAMES))
                if self.days.IsChecked(index)
            ]
            if recurrence == "SelectedDays" and not active_days:
                raise ScheduleValidationError(
                    "Wybierz co najmniej jeden dzień tygodnia"
                )
            ticks = utc_ticks_from_local(local_start, recurrence, active_days)
            template = validate_file_name_template(self.file_template.GetValue())
            folder_index = self.folder_mode.GetSelection()
            folder_mode = (
                self._folder_values[folder_index]
                if 0 <= folder_index < len(self._folder_values)
                else ""
            )
            if folder_mode == "custom":
                output_folder = self.folder_path.GetValue().strip()
            elif folder_mode == "station":
                output_folder = self._recording.station_folders.get(station.id, "")
            else:
                output_folder = ""
            if not output_folder_is_valid(output_folder):
                raise ScheduleValidationError(
                    "Folder dla tego planu musi zawierać pełną ścieżkę"
                )
        except ScheduleValidationError as error:
            self._show_error(str(error))
            return

        old_ticks = (self._existing or {}).get("nextStartUtcTicks")
        wake_index = self.wake.GetSelection()
        wake_value = self._wake_values[wake_index]
        schedule = {
            "id": (self._existing or {}).get("id") or uuid.uuid4().hex,
            "name": self.name.GetValue().strip(),
            "stationId": station.id,
            "stationName": station.name,
            "streamUrl": station.url,
            "nextStartUtcTicks": ticks,
            # Edytor pokazuje czas lokalny tego komputera. Pusty identyfikator
            # jest jawnym kontraktem hosta: ResolveTimeZone wybiera strefe
            # lokalna. Zachowanie starego, obcego identyfikatora po pokazaniu
            # czasu w naszej strefie przesuneloby kolejne wystapienia.
            "timeZoneId": "",
            "durationMinutes": duration,
            "segmentMinutes": segment,
            "recurrence": recurrence,
            "activeDays": active_days if recurrence == "SelectedDays" else [],
            "outputFolder": output_folder,
            "fileNameTemplate": template,
            "recordingFormat": self._format_values[
                self.recording_format.GetSelection()
            ],
            "recordingBitrateKbps": BITRATES[self.bitrate.GetSelection()],
            "wakeComputer": None if wake_value is _WAKE_INHERIT else wake_value,
            "enabled": self.enabled.GetValue(),
            "suppressedOccurrenceStartUtcTicks": (
                (self._existing or {}).get("suppressedOccurrenceStartUtcTicks")
                if old_ticks == ticks
                else None
            ),
            "lastFailureUtcTicks": None,
            "lastFailureMessage": "",
            "lastFailureAcknowledged": True,
        }
        self.result_schedule = sanitize_schedule(schedule)
        if self.result_schedule is None:
            self._show_error("Nie udało się przygotować planu nagrywania")
            return
        self.EndModal(wx.ID_OK)


class RadioSchedulesDialog(wx.Dialog):
    """Lista planow zgodna ze skrotami glownego AMC."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        stations: Sequence[Station],
        recording,
        schedules: Sequence[Mapping],
        wake_scheduled_recordings: bool,
        labels_payload: object,
        commit: Callable[[list[dict], bool], object],
        initial_station_id: str | None = None,
    ) -> None:
        super().__init__(
            parent,
            title="Harmonogram nagrywania radia",
            style=wx.DEFAULT_DIALOG_STYLE | wx.RESIZE_BORDER,
        )
        self._stations = list(stations)
        self._recording = recording
        self._schedules = clone_schedules(schedules)
        self._wake = bool(wake_scheduled_recordings)
        self._commit_callback = commit
        self._shown_ids: list[str] = []
        self._active_ids: set[str] = set()

        panel = wx.Panel(self)
        layout = wx.BoxSizer(wx.VERTICAL)
        help_text = " ".join(
            f"{shortcut} {description}."
            for shortcut, _name, description, _handler, _needs_selection
            in _LIST_ACTIONS
        ) + (
            " Shift+F10 lub Ctrl+Shift+K otwiera listę poleceń. "
            "Ctrl+F1 powtarza tę pomoc. Zapisane zmiany działają od razu."
        )
        self._help_text = help_text
        help_label = wx.StaticText(panel, label=help_text)
        help_label.SetName(help_text)
        layout.Add(help_label, 0, wx.ALL | wx.EXPAND, 12)

        self.schedule_list = wx.ListBox(panel, choices=[], style=wx.LB_SINGLE)
        self.schedule_list.SetName("Zaplanowane nagrania radia")
        self.schedule_list.SetToolTip(
            "Góra i dół wybierają plan. Lewo powtarza stan i termin, prawo "
            "czyta ustawienia pliku. "
            "Enter edytuje, Spacja włącza lub wyłącza cały plan, "
            "Shift+Spacja pomija najbliższy termin lub cofa pominięcie."
        )
        layout.Add(self.schedule_list, 1, wx.LEFT | wx.RIGHT | wx.EXPAND, 12)

        self.global_wake = wx.CheckBox(
            panel,
            label="Domyślnie &wybudzaj komputer przed zaplanowanym nagraniem",
        )
        self.global_wake.SetName(
            "Domyślnie wybudzaj komputer przed zaplanowanym nagraniem"
        )
        self.global_wake.SetValue(self._wake)
        layout.Add(self.global_wake, 0, wx.ALL | wx.EXPAND, 12)

        self.status = wx.StaticText(panel, label="Gotowe")
        self.status.SetName("Wynik zmiany harmonogramu")
        layout.Add(self.status, 0, wx.LEFT | wx.RIGHT | wx.BOTTOM | wx.EXPAND, 12)
        panel.SetSizer(layout)

        buttons = wx.BoxSizer(wx.HORIZONTAL)
        self.new_button = wx.Button(self, label="&Nowy…")
        self.edit_button = wx.Button(self, label="&Edytuj…")
        self.duplicate_button = wx.Button(self, label="&Powiel")
        self.toggle_button = wx.Button(self, label="Włącz lub w&yłącz")
        self.skip_button = wx.Button(self, label="&Pomiń najbliższy termin")
        self.delete_button = wx.Button(self, label="&Usuń")
        close = wx.Button(self, wx.ID_CANCEL, label="&Zamknij")
        for button, name in (
            (self.new_button, "Nowy plan nagrywania"),
            (self.edit_button, "Edytuj wybrany plan"),
            (self.duplicate_button, "Powiel wybrany plan"),
            (self.toggle_button, "Włącz lub wyłącz wybrany plan"),
            (self.skip_button, "Pomiń najbliższy termin lub cofnij pominięcie"),
            (self.delete_button, "Usuń wybrany plan"),
            (close, "Zamknij harmonogram nagrywania"),
        ):
            button.SetName(name)
            buttons.Add(button, 0, wx.RIGHT, 8)

        shell = wx.BoxSizer(wx.VERTICAL)
        shell.Add(panel, 1, wx.EXPAND)
        shell.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizer(shell)
        self.SetMinSize((760, 500))
        self.SetSize((850, 580))

        self.Bind(wx.EVT_CHAR_HOOK, self._on_key)
        # Some Windows/NVDA combinations deliver Space to the native list box
        # without first bubbling EVT_CHAR_HOOK through the dialog.  Bind the
        # list directly as well; the hook consumes handled keys, so one press
        # still performs exactly one change.
        self.schedule_list.Bind(wx.EVT_KEY_DOWN, self._on_key)
        self.schedule_list.Bind(wx.EVT_CONTEXT_MENU, self._show_context_menu)
        self.schedule_list.Bind(wx.EVT_LISTBOX_DCLICK, self._edit)
        self.new_button.Bind(wx.EVT_BUTTON, self._new)
        self.edit_button.Bind(wx.EVT_BUTTON, self._edit)
        self.duplicate_button.Bind(wx.EVT_BUTTON, self._duplicate)
        self.toggle_button.Bind(wx.EVT_BUTTON, self._toggle)
        self.skip_button.Bind(wx.EVT_BUTTON, self._toggle_next_occurrence)
        self.delete_button.Bind(wx.EVT_BUTTON, self._delete)
        self.global_wake.Bind(wx.EVT_CHECKBOX, self._change_global_wake)
        self._refresh(labels_payload)
        self.schedule_list.SetFocus()
        if initial_station_id:
            wx.CallAfter(self._new_for_station, initial_station_id)

    def _announce(self, message: str) -> None:
        announce_status(self.status, message)

    def _announce_short_information(self, _event: object | None = None) -> None:
        selected = self._selected_schedule()
        self._announce(
            _short_schedule_information(selected)
            if selected is not None
            else "Nie wybrano planu nagrywania"
        )

    def _announce_full_information(self, _event: object | None = None) -> None:
        selected = self._selected_schedule()
        self._announce(
            _full_schedule_information(selected, self._recording)
            if selected is not None
            else "Nie wybrano planu nagrywania"
        )

    def _show_context_menu(self, _event: object | None = None) -> None:
        """Menu harmonogramu prowadzi do tych samych metod co klawiatura."""

        menu = wx.Menu()
        schedule = self._selected_schedule()
        selected = schedule is not None
        can_skip = self._skip_unavailable_reason(schedule) is None
        commands = tuple(
            (f"{name} ({shortcut})", getattr(self, handler),
             (selected or not needs_selection)
             and (handler != "_toggle_next_occurrence" or can_skip))
            for shortcut, name, _description, handler, needs_selection
            in _LIST_ACTIONS
        )
        try:
            for index, (label, handler, enabled) in enumerate(commands):
                if index == len(commands) - 1:
                    menu.AppendSeparator()
                item_id = int(wx.NewIdRef())
                item = menu.Append(item_id, label)
                item.Enable(enabled)
                menu.Bind(wx.EVT_MENU, handler, id=item_id)
            self.PopupMenu(menu)
        finally:
            menu.Destroy()
            wx.CallAfter(self.schedule_list.SetFocus)

    def _invoke_list_action(self, shortcut: str, event: object) -> None:
        for key, _name, _description, handler, _needs_selection in _LIST_ACTIONS:
            if key == shortcut:
                getattr(self, handler)(event)
                return
        raise ValueError(f"Nieznane polecenie listy: {shortcut}")

    def _refresh(self, payload: object, preferred_id: str | None = None) -> None:
        previous_id = preferred_id
        if previous_id is None:
            index = self.schedule_list.GetSelection()
            if 0 <= index < len(self._shown_ids):
                previous_id = self._shown_ids[index]

        schedules_by_id = {schedule["id"]: schedule for schedule in self._schedules}
        shown_ids: list[str] = []
        labels: list[str] = []
        active_ids: set[str] = set()
        if isinstance(payload, dict):
            raw_active = payload.get("activeIds")
            if isinstance(raw_active, list):
                active_ids = {
                    value for value in raw_active if isinstance(value, str) and value
                }
            entries = payload.get("schedules")
            if isinstance(entries, list):
                for entry in entries:
                    if not isinstance(entry, dict):
                        continue
                    schedule_id = entry.get("id")
                    label = entry.get("label")
                    if (
                        isinstance(schedule_id, str)
                        and schedule_id in schedules_by_id
                        and isinstance(label, str)
                        and label.strip()
                    ):
                        shown_ids.append(schedule_id)
                        labels.append(label.strip())
        for schedule in self._schedules:
            if schedule["id"] not in shown_ids:
                shown_ids.append(schedule["id"])
                state = "włączone" if schedule.get("enabled") else "wyłączone"
                labels.append(f"{_display_name(schedule)}, {state}")

        self._shown_ids = shown_ids
        self._active_ids = active_ids
        self.schedule_list.Set(labels)
        if shown_ids:
            selection = shown_ids.index(previous_id) if previous_id in shown_ids else 0
            self.schedule_list.SetSelection(selection)
        self._update_buttons()

    def _update_buttons(self) -> None:
        selected = self._selected_schedule() is not None
        for button in (
            self.edit_button,
            self.duplicate_button,
            self.toggle_button,
            self.delete_button,
        ):
            button.Enable(selected)
        self.skip_button.Enable(
            self._skip_unavailable_reason(self._selected_schedule()) is None
        )

    def _selected_schedule(self) -> dict | None:
        index = self.schedule_list.GetSelection()
        if not (0 <= index < len(self._shown_ids)):
            return None
        wanted = self._shown_ids[index]
        return next((item for item in self._schedules if item["id"] == wanted), None)

    def _commit(
        self,
        candidate: list[dict],
        wake: bool,
        *,
        preferred_id: str | None,
        success_message: str,
    ) -> bool:
        try:
            payload = self._commit_callback(clone_schedules(candidate), bool(wake))
        except Exception:  # noqa: BLE001 - callback laczy dysk i proces hosta
            self.global_wake.SetValue(self._wake)
            self._announce("Nie udało się zapisać harmonogramu. Niczego nie zmieniono")
            return False
        self._schedules = schedules_from_host_status(payload, candidate)
        self._wake = bool(wake)
        self.global_wake.SetValue(self._wake)
        self._refresh(payload, preferred_id=preferred_id)
        self._announce(success_message)
        wx.CallAfter(self.schedule_list.SetFocus)
        return True

    def _new(self, _event) -> None:
        self._new_for_station(None)

    def _new_for_station(self, station_id: str | None) -> None:
        with RadioScheduleEditorDialog(
            self,
            stations=self._stations,
            recording=self._recording,
            global_wake=self._wake,
            preferred_station_id=station_id,
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK or dialog.result_schedule is None:
                self.schedule_list.SetFocus()
                return
            schedule = dialog.result_schedule
        candidate = [*self._schedules, schedule]
        self._commit(
            candidate,
            self._wake,
            preferred_id=schedule["id"],
            success_message=f"Dodano plan {_display_name(schedule)}",
        )

    def _edit(self, _event) -> None:
        selected = self._selected_schedule()
        if selected is None:
            self._announce("Nie wybrano planu nagrywania")
            return
        with RadioScheduleEditorDialog(
            self,
            stations=self._stations,
            recording=self._recording,
            global_wake=self._wake,
            existing=selected,
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK or dialog.result_schedule is None:
                self.schedule_list.SetFocus()
                return
            updated = dialog.result_schedule
        candidate = [
            updated if item["id"] == selected["id"] else item
            for item in self._schedules
        ]
        self._commit(
            candidate,
            self._wake,
            preferred_id=updated["id"],
            success_message=f"Zapisano plan {_display_name(updated)}",
        )

    def _duplicate(self, _event) -> None:
        selected = self._selected_schedule()
        if selected is None:
            self._announce("Nie wybrano planu nagrywania")
            return
        try:
            copy = duplicate_schedule(selected, self._schedules)
        except ScheduleValidationError as error:
            self._announce(str(error))
            return
        with RadioScheduleEditorDialog(
            self,
            stations=self._stations,
            recording=self._recording,
            global_wake=self._wake,
            existing=copy,
        ) as dialog:
            if dialog.ShowModal() != wx.ID_OK or dialog.result_schedule is None:
                self.schedule_list.SetFocus()
                return
            copy = dialog.result_schedule
        candidate = [*self._schedules, copy]
        self._commit(
            candidate,
            self._wake,
            preferred_id=copy["id"],
            success_message=(
                f"Powielono i zapisano plan {_display_name(copy)}. "
                + ("Plan jest włączony" if copy.get("enabled") else "Kopia jest wyłączona")
            ),
        )

    def _toggle(self, _event) -> None:
        selected = self._selected_schedule()
        if selected is None:
            self._announce("Nie wybrano planu nagrywania")
            return
        updated = dict(selected)
        updated["enabled"] = not bool(selected.get("enabled"))
        candidate = [
            updated if item["id"] == selected["id"] else item
            for item in self._schedules
        ]
        state = "włączony" if updated["enabled"] else "wyłączony"
        self._commit(
            candidate,
            self._wake,
            preferred_id=updated["id"],
            success_message=f"{_display_name(updated)}, {state}",
        )

    def _skip_unavailable_reason(self, schedule: Mapping | None) -> str | None:
        if schedule is None:
            return "Nie wybrano planu nagrywania"
        if not schedule.get("enabled"):
            return "Plan jest wyłączony. Włącz go Spacją przed pominięciem terminu"
        if schedule["id"] in self._active_ids:
            return "Nagrywanie już trwa. Pominięcie dotyczy tylko przyszłego terminu"
        now_ticks = (
            int(datetime.now(timezone.utc).timestamp() * 10_000_000)
            + DOTNET_UNIX_EPOCH_TICKS
        )
        if schedule["nextStartUtcTicks"] <= now_ticks:
            return "Ten termin już się rozpoczął. Nie można go teraz pominąć"
        return None

    def _toggle_next_occurrence(self, _event) -> None:
        selected = self._selected_schedule()
        reason = self._skip_unavailable_reason(selected)
        if reason is not None:
            self._announce(reason)
            return
        ticks = selected["nextStartUtcTicks"]
        updated = dict(selected)
        skipped = updated.get("suppressedOccurrenceStartUtcTicks") == ticks
        updated["suppressedOccurrenceStartUtcTicks"] = None if skipped else ticks
        candidate = [
            updated if item["id"] == selected["id"] else item
            for item in self._schedules
        ]
        date_text, time_text = _schedule_when(updated)
        result = "Przywrócono" if skipped else "Pominięto"
        self._commit(
            candidate, self._wake, preferred_id=updated["id"],
            success_message=(
                f"{result} termin {date_text}, {time_text} planu "
                f"{_display_name(updated)}"
            ),
        )

    def _delete(self, _event) -> None:
        selected = self._selected_schedule()
        if selected is None:
            self._announce("Nie wybrano planu nagrywania")
            return
        name = _display_name(selected)
        active = selected["id"] in self._active_ids
        question = (
            f"Zatrzymać nagrywanie i usunąć plan {name}?"
            if active
            else f"Usunąć plan nagrywania {name}?"
        )
        if (
            wx.MessageBox(
                question,
                "Usuń plan nagrywania",
                wx.YES_NO | wx.NO_DEFAULT | wx.ICON_QUESTION,
                self,
            )
            != wx.YES
        ):
            return
        candidate = [item for item in self._schedules if item["id"] != selected["id"]]
        self._commit(
            candidate,
            self._wake,
            preferred_id=None,
            success_message=f"Usunięto plan {name}",
        )

    def _change_global_wake(self, _event) -> None:
        wanted = self.global_wake.GetValue()
        self._commit(
            self._schedules,
            wanted,
            preferred_id=(self._selected_schedule() or {}).get("id"),
            success_message=(
                "Domyślne wybudzanie komputera włączone"
                if wanted
                else "Domyślne wybudzanie komputera wyłączone"
            ),
        )

    def _on_key(self, event: wx.KeyEvent) -> None:
        code = event.GetKeyCode()
        event_object = (
            event.GetEventObject()
            if hasattr(event, "GetEventObject")
            else None
        )
        focus_on_list = (
            event_object is self.schedule_list
            or wx.Window.FindFocus() is self.schedule_list
        )
        menu_key = getattr(wx, "WXK_WINDOWS_MENU", -1)
        if focus_on_list and (
            (
                code == wx.WXK_F10
                and event.ShiftDown()
                and not event.ControlDown()
                and not event.AltDown()
            )
            or (
                code == menu_key
                and not event.ControlDown()
                and not event.AltDown()
                and not event.ShiftDown()
            )
        ):
            self._show_context_menu(event)
            return
        if (
            focus_on_list
            and event.ControlDown()
            and not event.AltDown()
            and not event.ShiftDown()
            and code in (ord("D"), ord("d"))
        ):
            self._invoke_list_action("Ctrl+D", event)
            return
        if (
            focus_on_list and code == wx.WXK_SPACE and event.ShiftDown()
            and not event.ControlDown() and not event.AltDown()
        ):
            self._invoke_list_action("Shift+Spacja", event)
            return
        if (
            focus_on_list and code == wx.WXK_F1 and event.ControlDown()
            and not event.AltDown() and not event.ShiftDown()
        ):
            self._announce(self._help_text)
            return
        if (
            focus_on_list and code in (ord("K"), ord("k"))
            and event.ControlDown() and event.ShiftDown()
            and not event.AltDown()
        ):
            self._show_context_menu(event)
            return
        if event.ControlDown() or event.AltDown() or event.ShiftDown():
            event.Skip()
            return
        insert_key = getattr(wx, "WXK_INSERT", 322)
        if code == insert_key:
            self._invoke_list_action("Insert", event)
        elif focus_on_list and code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER):
            self._invoke_list_action("Enter", event)
        elif focus_on_list and code == wx.WXK_SPACE:
            self._invoke_list_action("Spacja", event)
        elif focus_on_list and code == wx.WXK_LEFT:
            self._invoke_list_action("strzałka w lewo", event)
        elif focus_on_list and code == wx.WXK_RIGHT:
            self._invoke_list_action("strzałka w prawo", event)
        elif focus_on_list and code == wx.WXK_DELETE:
            self._invoke_list_action("Delete", event)
        else:
            event.Skip()
