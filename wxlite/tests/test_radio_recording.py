"""Nagrywanie Radia: ustawienia profilu, skroty i kontrakt hosta."""

from __future__ import annotations

import json
import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId
from amc_wx_lite.profile_layout import read_only_mirror
from amc_wx_lite.radio_recording import (
    RadioRecordingPreferences,
    active_recording_rows,
    format_duration,
    preferences_from_amc_state,
)
from amc_wx_lite.radio_source import RadioSource
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import Station


def test_profile_recording_settings_and_station_folder_reach_one_payload() -> None:
    raw = {
        "radio": {
            "recordingFormat": "Aac",
            "recordingBitrateKbps": 255,
            "recordingsFolder": r"D:\Nagrania ogolne",
            "stations": [
                {
                    "id": "trojka",
                    "recordingFolder": r"D:\Nagrania Trojki",
                }
            ],
        }
    }
    preferences = preferences_from_amc_state(raw)
    payload = preferences.payload_for(
        Station("trojka", "Polskie Radio Trójka", "https://example.invalid/live")
    )
    assert payload == {
        "stationId": "trojka",
        "stationName": "Polskie Radio Trójka",
        "url": "https://example.invalid/live",
        "format": "Aac",
        "bitrateKbps": 256,
        "folderPreset": "radio",
        "folder": r"D:\Nagrania Trojki",
    }


def test_empty_podcast_folder_is_resolved_by_windows_host_not_python_guess() -> None:
    preferences = preferences_from_amc_state({
        "radio": {
            "usePodcastDownloadsFolderForRecordings": True,
            "recordingFormat": 4,
        },
        "podcasts": {"downloadsFolder": ""},
    })
    payload = preferences.payload_for(Station("id", "Stacja", "https://example.invalid"))
    assert payload["folderPreset"] == "podcasts"
    assert payload["format"] == "Wav"
    assert "folder" not in payload


def test_radio_source_keeps_recording_preferences_from_the_same_profile_read() -> None:
    with tempfile.TemporaryDirectory() as directory:
        base = Path(directory)
        profile = base / "profile"
        profile.mkdir()
        (profile / "state.json").write_text(json.dumps({
            "radio": {
                "recordingFormat": "Flac",
                "recordingBitrateKbps": 192,
                "stations": [{
                    "id": "live",
                    "name": "YouTube na żywo",
                    "streamUrl": "https://www.youtube.com/watch?v=abc",
                    "isInLibrary": True,
                }],
            }
        }), encoding="utf-8")
        source = RadioSource(read_only_mirror(
            local_dir=base / "local",
            profile_dir=profile,
            lite_settings_dir=base / "lite",
        ))
        snapshot = source.load()
        assert snapshot.recording.format == "Flac"
        assert snapshot.stations[0].name == "YouTube na żywo"


def test_recording_shortcuts_have_the_scope_of_full_amc() -> None:
    # Ctrl+R i Shift+Spacja: lista i odtwarzacz Radia.
    for player in (False, True):
        assert resolve(Chord("R", ctrl=True), player_view=player, radio_session=True) is Action.RECORD_TOGGLE
        assert resolve(Chord("Space", shift=True), player_view=player, radio_session=True) is Action.RECORD_PAUSE
        assert resolve(Chord("R", alt=True), player_view=player, radio_session=True) is Action.VIEW_ACTIVE_RECORDINGS
    # Gole R/T tylko odtwarzacz Radia; na liscie zostaja natywnej kontrolce.
    assert resolve(Chord("R"), player_view=True, radio_session=True) is Action.RECORD_TOGGLE
    assert resolve(Chord("T"), player_view=True, radio_session=True) is Action.RECORD_SPLIT
    assert resolve(Chord("R"), player_view=False, radio_session=True) is None
    assert resolve(Chord("T"), player_view=False, radio_session=True) is None
    # W plikach zadna litera nie uruchamia nagrania.
    assert resolve(Chord("R"), player_view=True, radio_session=False) is None
    assert resolve(Chord("T"), player_view=True, radio_session=False) is None
    assert resolve(Chord("R", alt=True), player_view=False, radio_session=False) is None


def test_host_client_methods_send_only_documented_recording_operations() -> None:
    client = LiteHostClient("unused")
    calls: list[tuple[str, dict | None, float]] = []

    def call(op, args=None, *, timeout=20.0):
        calls.append((op, args, timeout))
        return {"ok": True}

    client.call = call  # type: ignore[method-assign]
    payload = {"stationId": "id", "stationName": "Nazwa", "url": "https://x"}
    client.toggle_radio_recording(payload)
    client.toggle_radio_recording_pause("id", "https://x")
    client.split_radio_recording("id", "https://x")
    client.stop_all_radio_recordings()
    client.radio_recording_status()
    assert [entry[0] for entry in calls] == [
        "radio.recordingToggle",
        "radio.recordingPauseToggle",
        "radio.recordingSplit",
        "radio.recordingStopAll",
        "radio.recordingStatus",
    ]


def test_gui_toggle_uses_station_label_and_profile_preferences() -> None:
    class Runner:
        def submit(self, key, work, done, failed):
            assert key == "radio-recording-command"
            try:
                done(work())
            except Exception as error:
                failed(error)

    class Client:
        def __init__(self):
            self.payload = None

        def toggle_radio_recording(self, payload):
            self.payload = payload
            return {"action": "starting", "recordingId": "techniczne-id"}

    messages: list[str] = []
    station = Station("stacja-1", "Radio Test", "https://example.invalid/live")
    client = Client()
    preferences = RadioRecordingPreferences(format="Mp3", bitrate_kbps=160)
    frame = SimpleNamespace(
        client=client,
        runner=Runner(),
        radio=SimpleNamespace(load=lambda previous: SimpleNamespace(recording=preferences)),
        _radio_snapshot=object(),
        _recording_station=lambda: station,
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._toggle_radio_recording(frame)
    assert client.payload["stationName"] == "Radio Test"
    assert client.payload["bitrateKbps"] == 160
    assert messages == ["Rozpoczynam nagrywanie w tle: Radio Test"]
    assert "techniczne-id" not in messages[0]


def test_recording_duration_label_is_neutral_and_readable() -> None:
    assert format_duration(0) == "0:00"
    assert format_duration(65) == "1:05"
    assert format_duration(3661) == "1:01:01"


def test_active_recordings_are_one_accessible_row_per_station() -> None:
    rows = active_recording_rows({
        "recordings": [
            {
                "recordingId": "techniczne-id-1",
                "stationId": "trojka",
                "stationName": "Polskie Radio Trójka",
                "url": "https://example.invalid/live",
                "state": "paused",
                "durationSeconds": 65,
                "completedFileCount": 2,
            },
            {
                "recordingId": "techniczne-id-2",
                "stationId": "trojka",
                "stationName": "Polskie Radio Trójka",
                "state": "recording",
            },
        ]
    })
    assert len(rows) == 1
    assert rows[0].title == "Polskie Radio Trójka"
    assert rows[0].url == "https://example.invalid/live"
    assert rows[0].detail == "nagrywanie wstrzymane, 1:05, zapisane części: 2"
    assert "techniczne-id" not in f"{rows[0].title} {rows[0].detail}"


def test_radio_navigator_accepts_active_recordings_and_back_returns_to_stations() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    rows = active_recording_rows({
        "recordings": [{
            "stationId": "stacja",
            "stationName": "Radio Test",
            "url": "https://example.invalid/live",
            "state": "recording",
            "durationSeconds": 5,
        }]
    })
    nav.apply_radio_view(LibraryView.ACTIVE_RADIO_RECORDINGS, "Nagrywane", rows)
    assert nav.session.library_view is LibraryView.ACTIVE_RADIO_RECORDINGS
    intent = nav.go_to_parent()[0]
    assert intent.view is None
    assert intent.target_session_id is SessionId.RADIO
