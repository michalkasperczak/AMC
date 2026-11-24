"""Keyboard contract of the native radio schedule list."""

from __future__ import annotations

from pathlib import Path

import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.radio_schedule_dialogs import (
    RadioSchedulesDialog,
    _full_schedule_information,
    _short_schedule_information,
)
from amc_wx_lite.live_region import (
    CHILDID_SELF,
    EVENT_OBJECT_LIVEREGIONCHANGED,
    OBJID_CLIENT,
    announce_status,
)


SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "radio_schedule_dialogs.py"


def test_schedule_list_receives_space_directly_and_through_the_dialog_hook() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "self.Bind(wx.EVT_CHAR_HOOK, self._on_key)" in source
    assert "self.schedule_list.Bind(wx.EVT_KEY_DOWN, self._on_key)" in source
    assert "event_object is self.schedule_list" in source
    assert "focus_on_list and code == wx.WXK_SPACE" in source
    assert 'self._invoke_list_action("Spacja", event)' in source


def test_modal_status_sends_one_real_live_region_event() -> None:
    calls: list[tuple[int, int, int, int]] = []

    class _Status:
        label = "Gotowe"
        name = ""

        def GetLabel(self):
            return self.label

        def SetLabel(self, value):
            self.label = value

        def SetName(self, value):
            self.name = value

        def GetHandle(self):
            return 741

    status = _Status()
    assert announce_status(status, "włączony, jutro 10:00", notify=lambda *args: calls.append(args)) == (
        "włączony, jutro 10:00"
    )
    assert status.label == status.name == "włączony, jutro 10:00"
    assert calls == [(EVENT_OBJECT_LIVEREGIONCHANGED, 741, OBJID_CLIENT, CHILDID_SELF)]


def test_schedule_save_result_is_announced_after_focus_returns(monkeypatch) -> None:
    import amc_wx_lite.radio_schedule_dialogs as dialogs

    pending: list[tuple[object, tuple]] = []
    events: list[object] = []
    monkeypatch.setattr(
        dialogs.wx, "CallAfter",
        lambda callback, *args: pending.append((callback, args)),
    )
    monkeypatch.setattr(dialogs, "clone_schedules", lambda schedules: schedules)
    monkeypatch.setattr(
        dialogs, "schedules_from_host_status",
        lambda _payload, schedules: schedules,
    )

    dialog = RadioSchedulesDialog.__new__(RadioSchedulesDialog)
    dialog.IsBeingDeleted = lambda: False
    dialog.schedule_list = type(
        "ScheduleList", (), {"SetFocus": lambda self: events.append("focus")}
    )()
    dialog.global_wake = type(
        "GlobalWake", (), {"SetValue": lambda self, value: None}
    )()
    dialog._schedules = []
    dialog._wake = False
    dialog._refresh = lambda _payload, preferred_id=None: events.append("refresh")
    dialog._announce = lambda message: events.append(("speech", message))
    dialog._commit_callback = lambda _schedules, _wake: {"schedules": []}

    assert dialog._commit([], False, preferred_id=None,
                          success_message="Zapisano plan Test")
    assert events == ["refresh"]
    while pending:
        callback, args = pending.pop(0)
        callback(*args)
    assert events == ["refresh", "focus", ("speech", "Zapisano plan Test")]


def test_schedule_context_menu_contains_the_same_actions_as_keyboard() -> None:
    import amc_wx_lite.radio_schedule_dialogs as dialogs

    labels: list[tuple[str, bool]] = []
    focus: list[bool] = []
    shown: list[bool] = []

    class _Item:
        def __init__(self, label: str) -> None:
            self.label = label

        def Enable(self, enabled: bool) -> None:
            labels.append((self.label, enabled))

    class _Menu:
        def Append(self, _item_id: int, label: str):
            return _Item(label)

        def AppendSeparator(self) -> None:
            return None

        def Bind(self, *_args, **_kwargs) -> None:
            return None

        def Destroy(self) -> None:
            return None

    dialog = RadioSchedulesDialog.__new__(RadioSchedulesDialog)
    dialog.schedule_list = type("ScheduleList", (), {"SetFocus": lambda self: focus.append(True)})()
    dialog._selected_schedule = lambda: {
        "id": "plan-1", "enabled": True,
        "nextStartUtcTicks": 999_999_999_999_999_999,
    }
    dialog._active_ids = set()
    dialog.PopupMenu = lambda _menu: shown.append(True)
    previous_menu = dialogs.wx.Menu
    previous_id = getattr(dialogs.wx, "NewIdRef", None)
    previous_call_after = dialogs.wx.CallAfter
    dialogs.wx.Menu = _Menu
    dialogs.wx.NewIdRef = iter(range(100, 200)).__next__
    dialogs.wx.CallAfter = lambda callback: callback()
    try:
        RadioSchedulesDialog._show_context_menu(dialog)
    finally:
        dialogs.wx.Menu = previous_menu
        dialogs.wx.CallAfter = previous_call_after
        if previous_id is None:
            delattr(dialogs.wx, "NewIdRef")
        else:
            dialogs.wx.NewIdRef = previous_id

    assert shown == [True] and focus == [True]
    assert labels == [
        ("Nowy plan (Insert)", True),
        ("Edytuj plan (Enter)", True),
        ("Powiel plan (Ctrl+D)", True),
        ("Włącz lub wyłącz plan (Spacja)", True),
        ("Pomiń najbliższy termin lub cofnij pominięcie (Shift+Spacja)", True),
        ("Krótka informacja (strzałka w lewo)", True),
        ("Ustawienia pliku (strzałka w prawo)", True),
        ("Usuń plan (Delete)", True),
    ]


def test_shift_f10_opens_schedule_context_menu_before_modifier_filter() -> None:
    import amc_wx_lite.radio_schedule_dialogs as dialogs

    calls: list[object] = []
    schedule_list = object()
    dialog = RadioSchedulesDialog.__new__(RadioSchedulesDialog)
    dialog.schedule_list = schedule_list
    dialog._show_context_menu = calls.append

    class _Event:
        def GetKeyCode(self):
            return dialogs.wx.WXK_F10

        def GetEventObject(self):
            return schedule_list

        def ControlDown(self):
            return False

        def AltDown(self):
            return False

        def ShiftDown(self):
            return True

        def Skip(self):
            raise AssertionError("Shift+F10 nie może zostać oddane kontrolce")

    event = _Event()
    RadioSchedulesDialog._on_key(dialog, event)
    assert calls == [event]


def test_schedule_toggle_keeps_an_intentional_spoken_state() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert 'state = "włączony" if updated["enabled"] else "wyłączony"' in source
    assert 'success_message=f"{_display_name(updated)}, {state}"' in source


def test_schedule_list_left_and_right_have_read_only_information_actions() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "focus_on_list and code == wx.WXK_LEFT" in source
    assert "_short_schedule_information(selected)" in source
    assert "focus_on_list and code == wx.WXK_RIGHT" in source
    assert "_full_schedule_information(selected, self._recording)" in source


def test_ctrl_enter_is_not_reassigned_and_ctrl_d_opens_disabled_copy_editor() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert 'code in (ord("D"), ord("d"))' in source
    duplicate = source[source.index("    def _duplicate"):source.index("    def _toggle")]
    assert "copy = duplicate_schedule" in duplicate
    assert "existing=copy" in duplicate
    assert "dialog.ShowModal()" in duplicate
    assert "candidate = [*self._schedules, copy]" in duplicate
    assert "WXK_CONTROL" not in source


def test_segmented_controls_have_only_user_facing_names_and_expected_keys() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "_SegmentedDateControl" in source
    assert "_SegmentedTimeControl" in source
    assert "DateSegments" in source and "TimeSegments" in source
    assert "wx.WXK_LEFT" in source and "wx.WXK_RIGHT" in source
    assert "wx.WXK_UP" in source and "wx.WXK_DOWN" in source
    assert "control.SetName(i18n.ui_text(self._state.label(segment)))" in source
    for leaked in ("DateSegments(", "TimeSegments(", "segment='", "value="):
        assert f"SetName({leaked}" not in source


def test_schedule_information_is_short_on_left_and_complete_on_right() -> None:
    schedule = {
        "name": "Poranek",
        "stationName": "Radio Test",
        "nextStartUtcTicks": 639_271_800_000_000_000,
        "durationMinutes": 85,
        "recurrence": "Daily",
        "activeDays": [],
        "recordingFormat": "Mp3",
        "outputFolder": r"D:\Nagrania",
        "enabled": True,
    }
    short = _short_schedule_information(schedule)
    assert short.startswith("włączony, ")
    assert "najbliższy termin" not in short and "godzina" not in short
    assert "Poranek" not in short and "D:\\Nagrania" not in short
    full = _full_schedule_information(schedule)
    for expected in (
        "źródło Radio Test",
        "codziennie",
        "długość 1 godzina 25 minut",
        "format MP3",
        r"folder D:\Nagrania",
    ):
        assert expected in full
    for repeated in ("nazwa Poranek", "włączony", "najbliżej"):
        assert repeated not in full


def test_new_schedule_updates_weekday_until_user_edits_days() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "self._days_user_changed = False" in source
    assert "self.days.Bind(wx.EVT_CHECKLISTBOX, self._on_days_changed)" in source
    assert "if self._existing is not None or self._days_user_changed" in source
    assert "self.days.Check(index, index == wanted)" in source
