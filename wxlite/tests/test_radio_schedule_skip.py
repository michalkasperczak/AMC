"""Jednorazowe pominięcie terminu na liście harmonogramów."""

from __future__ import annotations

from datetime import datetime, timedelta, timezone
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.radio_schedule_dialogs import (
    RadioSchedulesDialog,
    _LIST_ACTIONS,
    _short_schedule_information,
)
from amc_wx_lite.radio_schedule_settings import DOTNET_UNIX_EPOCH_TICKS


def _future_ticks() -> int:
    return (int((datetime.now(timezone.utc) + timedelta(days=7)).timestamp()
                * 10_000_000) + DOTNET_UNIX_EPOCH_TICKS)


def _dialog(schedule: dict):
    dialog = RadioSchedulesDialog.__new__(RadioSchedulesDialog)
    dialog._schedules = [schedule]
    dialog._wake = False
    dialog._active_ids = set()
    messages = []
    commits = []
    dialog._selected_schedule = lambda: dialog._schedules[0]
    dialog._announce = messages.append

    def commit(candidate, wake, *, preferred_id, success_message):
        commits.append((candidate, wake, preferred_id, success_message))
        dialog._schedules = candidate
        return True

    dialog._commit = commit
    return dialog, messages, commits


def test_shift_space_skips_once_then_restores_without_disabling_weekly_plan():
    ticks = _future_ticks()
    schedule = {
        "id": "weekly", "name": "Poniedziałkowa audycja", "enabled": True,
        "nextStartUtcTicks": ticks, "recurrence": "SelectedDays",
        "activeDays": ["Monday"], "suppressedOccurrenceStartUtcTicks": None,
    }
    dialog, messages, commits = _dialog(schedule)
    dialog._toggle_next_occurrence(None)
    skipped = dialog._schedules[0]
    assert skipped["enabled"] is True
    assert skipped["recurrence"] == "SelectedDays"
    assert skipped["activeDays"] == ["Monday"]
    assert skipped["suppressedOccurrenceStartUtcTicks"] == ticks
    assert "najbliższy termin pominięty" in _short_schedule_information(skipped)
    assert commits[0][1:3] == (False, "weekly")
    assert "Pominięto termin" in messages or "Pominięto termin" in commits[0][3]

    dialog._toggle_next_occurrence(None)
    assert dialog._schedules[0]["suppressedOccurrenceStartUtcTicks"] is None
    assert dialog._schedules[0]["enabled"] is True
    assert commits[1][3].startswith("Przywrócono termin")


def test_skip_rejects_disabled_running_and_past_occurrences():
    schedule = {
        "id": "weekly", "name": "Audycja", "enabled": False,
        "nextStartUtcTicks": _future_ticks(),
    }
    dialog, messages, commits = _dialog(schedule)
    dialog._toggle_next_occurrence(None)
    assert "wyłączony" in messages[-1]
    schedule["enabled"] = True
    dialog._active_ids.add("weekly")
    dialog._toggle_next_occurrence(None)
    assert "już trwa" in messages[-1]
    dialog._active_ids.clear()
    schedule["nextStartUtcTicks"] = DOTNET_UNIX_EPOCH_TICKS
    dialog._toggle_next_occurrence(None)
    assert "już się rozpoczął" in messages[-1]
    assert commits == []


def test_keyboard_menu_and_help_share_the_skip_command():
    import amc_wx_lite.radio_schedule_dialogs as module

    shortcut, name, description, handler, needs_selection = next(
        item for item in _LIST_ACTIONS if item[0] == "Shift+Spacja"
    )
    assert name == "Pomiń najbliższy termin lub cofnij pominięcie"
    assert description == (
        "pomija najbliższy przyszły termin włączonego planu lub cofa pominięcie"
    )
    assert handler == "_toggle_next_occurrence" and needs_selection

    dialog = RadioSchedulesDialog.__new__(RadioSchedulesDialog)
    schedule_list = object()
    dialog.schedule_list = schedule_list
    calls = []
    dialog._invoke_list_action = lambda key, event: calls.append((key, event))

    class Event:
        def GetKeyCode(self):
            return module.wx.WXK_SPACE

        def GetEventObject(self):
            return schedule_list

        def ControlDown(self):
            return False

        def AltDown(self):
            return False

        def ShiftDown(self):
            return True

        def Skip(self):
            raise AssertionError("Shift+Spacja musi zostać obsłużone")

    event = Event()
    RadioSchedulesDialog._on_key(dialog, event)
    assert calls == [(shortcut, event)]
    assert f"{name} ({shortcut})" == "Pomiń najbliższy termin lub cofnij pominięcie (Shift+Spacja)"


def test_real_host_ignores_skipped_weekly_occurrence():
    """Pełny obieg z własnym hostem: odwołanie, ponowne pominięcie, kolejny tydzień."""
    import time
    import unittest
    from amc_wx_lite.host_client import LiteHostClient
    from amc_wx_lite.radio_schedule_settings import DAY_NAMES

    host = (Path(__file__).resolve().parents[2]
            / "src/AccessibleMediaController.LiteHost/bin/Release"
            / "net8.0-windows10.0.19041.0/amc_lite_host.exe")
    if not host.is_file():
        raise unittest.SkipTest("najpierw zbuduj host AMC wxPython w konfiguracji Release")

    start = datetime.now(timezone.utc) + timedelta(seconds=5)
    ticks = int(start.timestamp() * 10_000_000) + DOTNET_UNIX_EPOCH_TICKS
    schedule = {
        "id": "skip-test", "name": "Audycja tygodniowa",
        "stationId": "station-test", "stationName": "Stacja testowa",
        "streamUrl": "https://example.invalid/live",
        "nextStartUtcTicks": ticks, "timeZoneId": "UTC",
        "durationMinutes": 1, "recurrence": "SelectedDays",
        "activeDays": [DAY_NAMES[start.weekday()]], "enabled": True,
        "wakeComputer": False, "suppressedOccurrenceStartUtcTicks": ticks,
    }
    client = LiteHostClient(host)
    try:
        client.start()

        def sync():
            return client.sync_radio_schedules({
                "schedules": [schedule], "wakeScheduledRecordings": False,
            })["schedules"][0]

        row = sync()
        assert "ten termin pominięty" in row["label"]
        schedule["suppressedOccurrenceStartUtcTicks"] = None
        row = sync()
        assert "ten termin pominięty" not in row["label"]
        schedule["suppressedOccurrenceStartUtcTicks"] = ticks
        row = sync()
        assert "ten termin pominięty" in row["label"]

        deadline = time.monotonic() + 9
        while time.monotonic() < deadline:
            status = client.radio_schedule_status()
            row = status["schedules"][0]
            if row["nextStartUtcTicks"] > ticks:
                break
            time.sleep(0.2)
        else:
            raise AssertionError("Host nie przesunął pominiętego terminu")
        assert row["enabled"] is True
        assert row["suppressedOccurrenceStartUtcTicks"] is None
        assert row["nextStartUtcTicks"] == ticks + 7 * 24 * 60 * 60 * 10_000_000
        assert status["activeIds"] == []
        assert client.radio_recording_history()["recordings"] == []
    finally:
        client.close()


def test_context_help_and_palette_route_from_the_schedule_list():
    import amc_wx_lite.radio_schedule_dialogs as module

    dialog = RadioSchedulesDialog.__new__(RadioSchedulesDialog)
    schedule_list = object()
    dialog.schedule_list = schedule_list
    dialog._help_text = " ".join(
        f"{key} {description}." for key, _name, description, _handler, _needs
        in _LIST_ACTIONS
    )
    announcements = []
    menus = []
    dialog._announce = announcements.append
    dialog._show_context_menu = menus.append

    class Event:
        def __init__(self, code, *, shift=False):
            self.code = code
            self.shift = shift

        def GetKeyCode(self):
            return self.code

        def GetEventObject(self):
            return schedule_list

        def ControlDown(self):
            return True

        def AltDown(self):
            return False

        def ShiftDown(self):
            return self.shift

        def Skip(self):
            raise AssertionError("Skrót dialogu musi zostać obsłużony")

    help_event = Event(module.wx.WXK_F1)
    palette_event = Event(ord("K"), shift=True)
    RadioSchedulesDialog._on_key(dialog, help_event)
    RadioSchedulesDialog._on_key(dialog, palette_event)
    assert "Shift+Spacja pomija najbliższy przyszły termin" in announcements[0]
    assert menus == [palette_event]
