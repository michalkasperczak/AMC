"""Natywne okna zarzadzania harmonogramami nagrywania radia.

Listy i pola dostaja wylacznie gotowe polskie napisy. Surowe slowniki,
identyfikatory i nazwy enumow pozostaja w modelu i nie sa elementami kontrolek
dostepnosciowych.
"""

from __future__ import annotations

import os
import uuid
from collections.abc import Callable, Mapping, Sequence
from datetime import datetime, timedelta

import wx

from .radio_schedule_settings import (
    BITRATES,
    DAY_LABELS,
    DAY_NAMES,
    DEFAULT_FILE_NAME_TEMPLATE,
    ScheduleValidationError,
    clone_schedules,
    duplicate_schedule,
    local_start_from_utc_ticks,
    output_folder_is_valid,
    parse_local_start,
    sanitize_schedule,
    schedules_from_host_status,
    utc_ticks_from_local,
    validate_file_name_template,
)
from .state_store import Station

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
        else "Plan nagrywania"
    )


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
        self.result_schedule: dict | None = None
        self._folder_values: list[str] = []

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

        initial = (
            local_start_from_utc_ticks(self._existing["nextStartUtcTicks"])
            if self._existing is not None
            else datetime.now().astimezone().replace(second=0, microsecond=0)
            + timedelta(minutes=5)
        )
        self.start_date = self._add_text(
            scroll,
            grid,
            "&Data rozpoczęcia:",
            "Data rozpoczęcia, format rok miesiąc dzień",
            initial.strftime("%Y-%m-%d"),
        )
        self.start_time = self._add_text(
            scroll,
            grid,
            "&Czas rozpoczęcia:",
            "Czas rozpoczęcia, godzina i minuta",
            initial.strftime("%H:%M"),
        )

        duration = int((self._existing or {}).get("durationMinutes") or 60)
        hours, minutes = divmod(duration, 60)
        self.duration_hours = self._add_spin(
            scroll,
            grid,
            "Długość, &godziny:",
            "Długość nagrania w godzinach",
            hours,
            0,
            168,
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
            scroll, choices=list(DAY_LABELS), style=wx.LB_SINGLE
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
        control = wx.Choice(parent, choices=list(choices))
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

    def _selected_station(self) -> Station | None:
        index = self.station.GetSelection()
        return self._stations[index] if 0 <= index < len(self._stations) else None

    def _select_initial_station(self) -> None:
        wanted = (self._existing or {}).get("stationId")
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
        labels = [f"Domyślny folder nagrań — {default}"]
        values = ["default"]
        station_folder = (
            self._recording.station_folders.get(station.id)
            if station is not None
            else None
        )
        if station_folder:
            labels.append(f"Folder tej stacji — {station_folder}")
            values.append("station")
        labels.append("Własny folder tego planu")
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
            local_start = parse_local_start(
                self.start_date.GetValue(), self.start_time.GetValue()
            )
            duration = (
                self.duration_hours.GetValue() * 60 + self.duration_minutes.GetValue()
            )
            if duration <= 0:
                raise ScheduleValidationError(
                    "Długość nagrania musi wynosić co najmniej jedną minutę"
                )
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
        help_text = (
            "Insert dodaje plan. Ctrl+D powiela wybrany plan jako wyłączoną kopię. "
            "Enter edytuje. Spacja włącza lub wyłącza. Delete usuwa. "
            "Każda zmiana jest zapisywana od razu."
        )
        help_label = wx.StaticText(panel, label=help_text)
        help_label.SetName(help_text)
        layout.Add(help_label, 0, wx.ALL | wx.EXPAND, 12)

        self.schedule_list = wx.ListBox(panel, choices=[], style=wx.LB_SINGLE)
        self.schedule_list.SetName("Zaplanowane nagrania radia")
        self.schedule_list.SetToolTip(
            "Strzałki wybierają plan. Enter edytuje, Spacja włącza lub wyłącza."
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
        self.delete_button = wx.Button(self, label="&Usuń")
        close = wx.Button(self, wx.ID_CANCEL, label="&Zamknij")
        for button, name in (
            (self.new_button, "Nowy plan nagrywania"),
            (self.edit_button, "Edytuj wybrany plan"),
            (self.duplicate_button, "Powiel wybrany plan"),
            (self.toggle_button, "Włącz lub wyłącz wybrany plan"),
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
        self.schedule_list.Bind(wx.EVT_LISTBOX_DCLICK, self._edit)
        self.new_button.Bind(wx.EVT_BUTTON, self._new)
        self.edit_button.Bind(wx.EVT_BUTTON, self._edit)
        self.duplicate_button.Bind(wx.EVT_BUTTON, self._duplicate)
        self.toggle_button.Bind(wx.EVT_BUTTON, self._toggle)
        self.delete_button.Bind(wx.EVT_BUTTON, self._delete)
        self.global_wake.Bind(wx.EVT_CHECKBOX, self._change_global_wake)
        self._refresh(labels_payload)
        self.schedule_list.SetFocus()

    def _announce(self, message: str) -> None:
        self.status.SetLabel(message)
        self.status.SetName(message)

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
        with RadioScheduleEditorDialog(
            self,
            stations=self._stations,
            recording=self._recording,
            global_wake=self._wake,
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
        candidate = [*self._schedules, copy]
        self._commit(
            candidate,
            self._wake,
            preferred_id=copy["id"],
            success_message=(
                f"Powielono plan {copy['name']}. Kopia jest wyłączona. "
                "Enter edytuje, Spacja włącza"
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
        focus_on_list = wx.Window.FindFocus() is self.schedule_list
        if (
            focus_on_list
            and event.ControlDown()
            and not event.AltDown()
            and not event.ShiftDown()
            and code in (ord("D"), ord("d"))
        ):
            self._duplicate(event)
            return
        if event.ControlDown() or event.AltDown() or event.ShiftDown():
            event.Skip()
            return
        insert_key = getattr(wx, "WXK_INSERT", 322)
        if code == insert_key:
            self._new(event)
        elif focus_on_list and code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER):
            self._edit(event)
        elif focus_on_list and code == wx.WXK_SPACE:
            self._toggle(event)
        elif focus_on_list and code == wx.WXK_DELETE:
            self._delete(event)
        else:
            event.Skip()
