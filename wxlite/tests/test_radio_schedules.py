"""Harmonogram radia: wspolne etykiety C#, dostepnosc i wyjscie klawiatura."""

from __future__ import annotations

import json
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.navigation import (
    LibraryView,
    Navigator,
    OpenLibraryView,
    SessionId,
)
from amc_wx_lite.profile_layout import read_only_mirror
from amc_wx_lite.radio_schedules import schedule_rows
from amc_wx_lite.radio_source import RadioSource, recording_schedules_from_amc_state
from amc_wx_lite.shortcuts import Action, Chord, resolve


def sample_schedule() -> dict:
    return {
        "id": "techniczne-id-planu",
        "name": "Poranna audycja",
        "stationId": "radio-test",
        "stationName": "Radio Test",
        "streamUrl": "https://example.invalid/live",
        "nextStartUtcTicks": 639000000000000000,
        "timeZoneId": "Central European Standard Time",
        "durationMinutes": 90,
        "segmentMinutes": 30,
        "recurrence": "Daily",
        "activeDays": [],
        "fileNameTemplate": "{stacja} - {data} {czas}",
        "enabled": True,
    }


def test_profile_reader_keeps_only_schedule_objects() -> None:
    schedules = recording_schedules_from_amc_state({
        "radio": {"recordingSchedules": [sample_schedule(), "zly wpis", 7]}
    })
    assert len(schedules) == 1
    assert schedules[0]["name"] == "Poranna audycja"


def test_radio_snapshot_reads_schedules_in_the_same_profile_pass() -> None:
    with tempfile.TemporaryDirectory() as directory:
        base = Path(directory)
        profile = base / "profile"
        profile.mkdir()
        (profile / "state.json").write_text(json.dumps({
            "radio": {
                "stations": [],
                "recordingSchedules": [sample_schedule()],
            }
        }), encoding="utf-8")
        source = RadioSource(read_only_mirror(
            local_dir=base / "local",
            profile_dir=profile,
            lite_settings_dir=base / "lite",
        ))
        snapshot = source.load()
    assert len(snapshot.recording_schedules) == 1
    assert snapshot.recording_schedules[0]["stationName"] == "Radio Test"


def test_schedule_rows_expose_only_the_user_label_not_the_object_or_id() -> None:
    rows = schedule_rows({"schedules": [{
        "id": "techniczne-id-planu",
        "navigationText": "Poranna audycja",
        "label": (
            "Poranna audycja, włączone, 08.10.2026 08:00, długość nagrania: "
            "1 godzina 30 minut, części co 30 min, codziennie"
        ),
        "enabled": True,
    }]})
    assert len(rows) == 1
    assert rows[0].title.startswith("Poranna audycja, włączone")
    assert "techniczne-id" not in rows[0].title
    assert "dict" not in rows[0].title and "{" not in rows[0].title
    assert "tylko do odczytu" in (rows[0].activation_message or "")


def test_host_client_requests_the_csharp_schedule_formatter() -> None:
    client = LiteHostClient("unused")
    calls = []

    def call(operation, args=None, *, timeout=20.0):
        calls.append((operation, args, timeout))
        return {"schedules": []}

    client.call = call  # type: ignore[method-assign]
    client.radio_schedule_labels([sample_schedule()])
    assert calls == [(
        "radio.scheduleLabels",
        {"schedules": [sample_schedule()], "activeIds": []},
        5.0,
    )]


def test_schedule_shortcut_is_radio_only_in_list_and_player() -> None:
    chord = Chord("H", ctrl=True, shift=True)
    for player in (False, True):
        assert resolve(chord, player_view=player, radio_session=True) is (
            Action.MANAGE_RADIO_SCHEDULES
        )
        assert resolve(chord, player_view=player, radio_session=False) is None


def test_escape_and_backspace_both_leave_schedule_view_for_radio_stations() -> None:
    rows = schedule_rows({"schedules": [{
        "id": "plan",
        "navigationText": "Plan",
        "label": "Plan, włączone",
    }]})
    for chord in (Chord("Escape"), Chord("Back")):
        assert resolve(chord, player_view=False, radio_session=True) is Action.PARENT_FOLDER
        navigator = Navigator()
        navigator.switch_session(SessionId.RADIO)
        navigator.apply_radio_view(
            LibraryView.RADIO_RECORDING_SCHEDULES,
            "Harmonogram nagrywania",
            rows,
        )
        intent = navigator.go_to_parent()[0]
        assert isinstance(intent, OpenLibraryView)
        assert intent.view is None
        assert intent.target_session_id is SessionId.RADIO
