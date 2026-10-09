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
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import (
    LibraryView,
    Navigator,
    OpenLibraryView,
    SessionId,
)
from amc_wx_lite.profile_layout import read_only_mirror
from amc_wx_lite.radio_schedules import schedule_rows
from amc_wx_lite.radio_source import (
    RadioSource,
    recording_schedules_from_amc_state,
    wake_scheduled_recordings_from_amc_state,
)
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


def test_profile_reader_keeps_strict_wake_default() -> None:
    assert wake_scheduled_recordings_from_amc_state({
        "radio": {"wakeScheduledRecordings": True}
    }) is True
    assert wake_scheduled_recordings_from_amc_state({
        "radio": {"wakeScheduledRecordings": "true"}
    }) is False


def test_radio_snapshot_reads_schedules_in_the_same_profile_pass() -> None:
    with tempfile.TemporaryDirectory() as directory:
        base = Path(directory)
        profile = base / "profile"
        profile.mkdir()
        (profile / "state.json").write_text(json.dumps({
            "radio": {
                "stations": [],
                "recordingSchedules": [sample_schedule()],
                "wakeScheduledRecordings": True,
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
    assert snapshot.wake_scheduled_recordings is True


def test_gui_sends_recording_defaults_and_station_folders_with_schedules() -> None:
    from types import SimpleNamespace

    snapshot = SimpleNamespace(
        recording_schedules=(sample_schedule(),),
        wake_scheduled_recordings=True,
        recording=SimpleNamespace(
            default_folder=r"D:\Nagrania",
            folder_preset="radio",
            station_folders={"radio-test": r"D:\Radio Test"},
            format="Flac",
            bitrate_kbps=256,
        ),
    )
    payload = LiteFrame._radio_schedule_sync_payload(snapshot)
    assert payload["schedules"] == [sample_schedule()]
    assert payload["stationFolders"] == {"radio-test": r"D:\Radio Test"}
    assert payload["recordingFormat"] == "Flac"
    assert payload["recordingBitrateKbps"] == 256
    assert payload["wakeScheduledRecordings"] is True


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
    assert "wykonywany automatycznie" in (rows[0].activation_message or "")
    assert "zarządzanie harmonogramami" in (rows[0].activation_message or "")


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


def test_host_client_syncs_executable_schedules() -> None:
    client = LiteHostClient("unused")
    calls = []

    def call(operation, args=None, *, timeout=20.0):
        calls.append((operation, args, timeout))
        return {"schedules": []}

    client.call = call  # type: ignore[method-assign]
    payload = {"schedules": [sample_schedule()], "wakeScheduledRecordings": True}
    client.sync_radio_schedules(payload)
    client.radio_schedule_status()
    assert calls == [
        ("radio.scheduleSync", payload, 10.0),
        ("radio.scheduleStatus", None, 5.0),
    ]


def test_schedule_shortcut_matches_full_amc_from_every_session_and_view() -> None:
    chord = Chord("H", ctrl=True, shift=True)
    for player in (False, True):
        for radio in (False, True):
            assert resolve(chord, player_view=player, radio_session=radio) is (
                Action.MANAGE_RADIO_SCHEDULES
            )


def test_escape_and_backspace_both_leave_unanchored_schedule_view_for_stations() -> None:
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


def test_schedule_preview_restores_the_exact_previous_session_and_player() -> None:
    navigator = Navigator()
    navigator.apply_library_view(
        LibraryView.ALL_FILES,
        "Wszystkie pliki",
        schedule_rows({"schedules": [{
            "id": "plik",
            "navigationText": "Plik",
            "label": "Plik",
        }]}),
    )
    navigator.session.now_playing_id = "plik"
    navigator.show_player()
    snapshot = navigator.capture_transient_navigation()
    navigator.active = SessionId.RADIO
    navigator.apply_radio_view(
        LibraryView.RADIO_RECORDING_SCHEDULES,
        "Harmonogram nagrywania",
        [],
    )

    navigator.restore_transient_navigation(snapshot)

    assert navigator.active is SessionId.FILES
    assert navigator.session.view.name == "PLAYER"
    assert navigator.session.library_view is LibraryView.ALL_FILES
