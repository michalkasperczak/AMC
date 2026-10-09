"""Nagrywanie Radia: ustawienia profilu, skroty i kontrakt hosta."""

from __future__ import annotations

import json
import sqlite3
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.library_db import LibraryDatabase, LibraryItem
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId
from amc_wx_lite.profile_layout import read_only_mirror
from amc_wx_lite.radio_recording import (
    RadioRecordingHistoryEntry,
    RadioRecordingPreferences,
    active_recording_rows,
    format_duration,
    preferences_from_amc_state,
    recording_history_from_amc_state,
    recording_history_payload_from_event,
    recording_history_rows,
    station_activity_rows,
)
from amc_wx_lite.radio_source import RadioSource
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import Station


def test_terminal_recording_event_becomes_sanitized_private_history() -> None:
    payload = recording_history_payload_from_event({
        "recordingId": "techniczne-id",
        "stationId": "radio-test",
        "stationName": " Radio Test ",
        "path": r"D:\Nagrania\test.mp3",
        "outcome": "Stopped",
        "error": "",
        "scheduleName": " Poranna audycja ",
        "startedUtcTicks": 100,
        "finishedUtcTicks": 200,
        "savedFileCount": 1,
        "technicalObject": {"nie": "zapisuj"},
    })
    assert payload == {
        "id": "techniczne-id",
        "stationId": "radio-test",
        "stationName": "Radio Test",
        "path": r"D:\Nagrania\test.mp3",
        "outcome": "stopped",
        "reason": "",
        "scheduleName": "Poranna audycja",
        "startedUtcTicks": 100,
        "finishedUtcTicks": 200,
        "savedFileCount": 1,
    }


def test_gui_persists_terminal_recording_once_in_private_state() -> None:
    saves = []
    state = SimpleNamespace(recording_history=[{
        "id": "techniczne-id", "stationName": "stara nazwa"
    }])
    frame = SimpleNamespace(
        state=state,
        store=SimpleNamespace(save=lambda saved: saves.append(saved.recording_history.copy())),
        _recording_history_persist_error=False,
    )
    event = {
        "recordingId": "techniczne-id",
        "stationId": "radio-test",
        "stationName": "Nowa nazwa",
        "outcome": "Completed",
        "savedFileCount": 1,
    }
    assert LiteFrame._remember_recording_result(frame, event) is True
    assert len(state.recording_history) == 1
    assert state.recording_history[0]["stationName"] == "Nowa nazwa"
    assert len(saves) == 1


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
        for radio_session in (False, True):
            assert resolve(Chord("R", alt=True), player_view=player, radio_session=radio_session) is Action.VIEW_ACTIVE_RECORDINGS
            assert resolve(Chord("R", alt=True, shift=True), player_view=player, radio_session=radio_session) is Action.VIEW_RECORDED_RADIO_FILES
    # Gole R/T tylko odtwarzacz Radia; na liscie zostaja natywnej kontrolce.
    assert resolve(Chord("R"), player_view=True, radio_session=True) is Action.RECORD_TOGGLE
    assert resolve(Chord("T"), player_view=True, radio_session=True) is Action.RECORD_SPLIT
    assert resolve(Chord("R"), player_view=False, radio_session=True) is None
    assert resolve(Chord("T"), player_view=False, radio_session=True) is None
    # W plikach zadna litera nie uruchamia nagrania.
    assert resolve(Chord("R"), player_view=True, radio_session=False) is None
    assert resolve(Chord("T"), player_view=True, radio_session=False) is None


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
    client.radio_recording_history()
    assert [entry[0] for entry in calls] == [
        "radio.recordingToggle",
        "radio.recordingPauseToggle",
        "radio.recordingSplit",
        "radio.recordingStopAll",
        "radio.recordingStatus",
        "radio.recordingHistory",
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
        _refresh_recording_status=lambda: None,
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._toggle_radio_recording(frame)
    assert client.payload["stationName"] == "Radio Test"
    assert client.payload["bitrateKbps"] == 160
    # Dokladnie jedno, natychmiastowe potwierdzenie -- bez nazwy pliku.
    assert messages == ["Rozpoczynam nagrywanie w tle: Radio Test"]


def test_gui_toggle_announces_stopping_without_technical_values() -> None:
    class Runner:
        def submit(self, key, work, done, failed):
            assert key == "radio-recording-command"
            try:
                done(work())
            except Exception as error:
                failed(error)

    class Client:
        def toggle_radio_recording(self, _payload):
            return {
                "action": "stopping",
                "recordingId": "techniczne-id",
                "stationId": "stacja-1",
            }

    messages: list[str] = []
    station = Station("stacja-1", "Radio Test", "https://example.invalid/live")
    frame = SimpleNamespace(
        client=Client(),
        runner=Runner(),
        radio=SimpleNamespace(load=lambda previous: SimpleNamespace(
            recording=RadioRecordingPreferences()
        )),
        _radio_snapshot=object(),
        _recording_station=lambda: station,
        _refresh_recording_status=lambda: None,
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._toggle_radio_recording(frame)
    assert messages == ["Zatrzymuję nagrywanie: Radio Test"]
    assert "techniczne-id" not in messages[0]


def test_manual_started_event_refreshes_state_without_second_announcement() -> None:
    messages: list[str] = []
    refreshes: list[str] = []
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        announcer=SimpleNamespace(say=messages.append),
        _refresh_recording_status=lambda: refreshes.append("status"),
        navigator=SimpleNamespace(
            sessions={
                SessionId.RADIO: SimpleNamespace(library_view=LibraryView.FAVORITES),
            }
        ),
    )
    LiteFrame._handle_engine_event(frame, "radio.recordingStarted", {
        "stationName": "Radio Test",
        "path": r"D:\Nagrania\radio-test.mp3",
        "scheduleName": "",
    })
    assert messages == []
    assert refreshes == ["status"]


def test_scheduled_due_event_is_announced_once_and_file_start_is_silent() -> None:
    messages: list[str] = []
    refreshes: list[str] = []
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        announcer=SimpleNamespace(say=messages.append),
        _refresh_recording_status=lambda: refreshes.append("status"),
        navigator=SimpleNamespace(
            sessions={
                SessionId.RADIO: SimpleNamespace(library_view=LibraryView.FAVORITES),
            }
        ),
    )
    LiteFrame._handle_engine_event(frame, "radio.recordingScheduled", {
        "stationName": "Radio Test",
        "scheduleName": "Poranna audycja",
        "recordingId": "techniczne-id",
    })
    LiteFrame._handle_engine_event(frame, "radio.recordingStarted", {
        "stationName": "Radio Test",
        "path": r"D:\Nagrania\radio-test.mp3",
        "scheduleName": "Poranna audycja",
    })
    assert messages == [
        "Rozpoczynam zaplanowane nagrywanie: Radio Test"
    ]
    assert refreshes == ["status", "status"]
    assert "techniczne-id" not in messages[0]


def test_station_rows_expose_playback_and_recording_without_technical_values() -> None:
    from amc_wx_lite.list_model import Row

    rows = [
        Row("radio-1", "Radio Pierwsze", "station", detail="stereo"),
        Row("radio-2", "Radio Drugie", "station"),
    ]
    decorated = station_activity_rows(
        rows,
        transport_status={
            "engine": "radio",
            "loadedId": "radio-1",
            "paused": False,
        },
        recording_status={
            "recordings": [
                {"stationId": "radio-1", "state": "recording", "recordingId": "sekret"},
                {"stationId": "radio-2", "state": "paused", "recordingId": "sekret-2"},
            ]
        },
    )
    assert decorated[0].detail == "stereo"
    assert decorated[0].state_detail == "odtwarzanie, nagrywanie"
    assert decorated[1].state_detail == "nagrywanie wstrzymane"
    assert "sekret" not in " ".join(row.state_detail for row in decorated)


def test_station_activity_refresh_replaces_old_state_instead_of_repeating_it() -> None:
    from amc_wx_lite.list_model import Row

    first = station_activity_rows(
        [Row("radio", "Radio", "station")],
        transport_status={"engine": "radio", "loadedId": "radio", "paused": False},
        recording_status={"recordings": []},
    )
    second = station_activity_rows(
        first,
        transport_status={"engine": "radio", "loadedId": "radio", "paused": True},
        recording_status={"recordings": []},
    )
    assert second[0].state_detail == "odtwarzanie wstrzymane"


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


def test_recording_history_keeps_success_failure_and_accessible_labels() -> None:
    now = datetime(2026, 10, 8, 12, 0, tzinfo=timezone.utc)
    ticks = int((now - datetime(1, 1, 1, tzinfo=timezone.utc)).total_seconds() * 10_000_000)
    entries = recording_history_from_amc_state({
        "radio": {
            "recordingHistory": [
                {
                    "id": "techniczne-udane",
                    "stationId": "radio-test",
                    "stationName": "Radio Test",
                    "path": r"D:\Nagrania\audycja.mp3",
                    "outcome": "Completed",
                    "finishedUtcTicks": ticks,
                    "savedFileCount": 1,
                },
                {
                    "id": "techniczne-nieudane",
                    "stationName": "Radio Awaria",
                    "outcome": 3,
                    "reason": "Brak połączenia.",
                    "finishedUtcTicks": ticks - 10_000_000,
                },
            ]
        }
    })
    rows = recording_history_rows(entries, now=now, path_probe=lambda path: "available")
    assert rows[0].title.startswith("Nagrane, Radio Test, audycja, dziś")
    assert rows[0].title.endswith("folder Nagrania")
    assert rows[0].path == r"D:\Nagrania\audycja.mp3"
    assert "techniczne-udane" not in rows[0].title
    assert rows[1].title.startswith("Nieudane, Radio Awaria")
    assert rows[1].path is None
    assert rows[1].activation_message == (
        "Nagranie Radio Awaria nie powstało. Brak połączenia"
    )


def test_missing_recording_is_visible_but_enter_refuses_honestly() -> None:
    entries = recording_history_from_amc_state({
        "radio": {"recordingHistory": [{
            "id": "brak",
            "stationName": "Radio Test",
            "path": r"D:\Nagrania\brak.mp3",
            "outcome": "Stopped",
            "finishedUtcTicks": 1,
        }]}
    })
    row = recording_history_rows(entries, path_probe=lambda path: "missing")[0]
    assert row.title.startswith("Zatrzymane, brak pliku nagrania, Radio Test")
    assert row.path is None
    nav = Navigator()
    nav.apply_library_view(LibraryView.RECORDED_RADIO_FILES, "Historia nagrywania", [row])
    message = nav.activate_selected()[0]
    assert "nie istnieje już na dysku" in message.text


def test_recorded_files_merge_without_duplicates_and_include_outside_library() -> None:
    completed = RadioRecordingHistoryEntry(
        "historia", "radio", "Stara nazwa", r"D:\Nagrania\audycja.mp3",
        "completed", "", "", 0, 200, 1,
    )
    stopped = RadioRecordingHistoryEntry(
        "przerwane", "radio", "Radio Przerwane", r"D:\Nagrania\czesc.mp3",
        "stopped", "", "", 0, 300, 1,
    )
    files = [
        LibraryItem(
            "plik-1", "Audycja z Biblioteki", r"D:\Nagrania\audycja.mp3",
            650_000_000, True, True, True, True, 200,
        ),
        LibraryItem(
            "plik-2", "Nagranie poza Biblioteką", r"D:\Nagrania\nowe.mp3",
            0, False, True, False, True, 400,
        ),
        LibraryItem(
            "plik-3", "Część przerwana", r"D:\Nagrania\czesc.mp3",
            0, False, True, True, True, 300,
        ),
    ]
    rows = recording_history_rows(
        [completed, stopped],
        recorded_files=files,
        path_probe=lambda path: "available",
    )
    assert len(rows) == 3
    assert sum("audycja.mp3" in (row.path or "").casefold() for row in rows) == 1
    outside = next(row for row in rows if row.path and row.path.endswith("nowe.mp3"))
    assert outside.detail == "poza Biblioteką"
    interrupted = next(row for row in rows if row.title.startswith("Zatrzymane"))
    assert interrupted.path == r"D:\Nagrania\czesc.mp3"
    assert all("plik-" not in row.title for row in rows)


def test_database_reads_recordings_outside_library_and_old_schema() -> None:
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / "library.db"
        connection = sqlite3.connect(path)
        connection.executescript("""
            CREATE TABLE local_items (
                id TEXT, title TEXT, path TEXT, duration_ticks INTEGER,
                is_favorite INTEGER, is_available INTEGER, is_in_library INTEGER,
                is_radio_recording INTEGER
            );
            INSERT INTO local_items VALUES
                ('1', 'Poza Biblioteką', 'D:\\Nagrania\\poza.mp3', 0, 0, 1, 0, 1),
                ('2', 'Zwykły plik', 'D:\\Muzyka\\zwykly.mp3', 0, 0, 1, 1, 0);
        """)
        connection.commit()
        connection.close()
        with LibraryDatabase(path) as database:
            items = database.recorded_radio_items()
        assert [item.title for item in items] == ["Poza Biblioteką"]
        assert items[0].radio_recording_completed_utc_ticks == 0


def test_transient_recording_preview_restores_session_view_and_selection() -> None:
    from amc_wx_lite.list_model import Row
    from amc_wx_lite.navigation import View

    nav = Navigator()
    original = Row(item_id="plik", title="Audycja", kind="track", path=r"D:\audycja.mp3")
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", [original])
    nav.activate_selected()
    assert nav.session.view is View.PLAYER
    snapshot = nav.capture_transient_navigation()

    nav.active = SessionId.RADIO
    nav.apply_radio_view(LibraryView.ACTIVE_RADIO_RECORDINGS, "Nagrywane", [])
    nav.restore_transient_navigation(snapshot)

    assert nav.active is SessionId.FILES
    assert nav.session.view is View.PLAYER
    assert nav.session.library_view is LibraryView.ALL_FILES
    assert nav.session.model.selected_id == "plik"


def test_escape_and_backspace_share_the_recording_preview_exit_action() -> None:
    for chord in (Chord("Escape"), Chord("Back")):
        assert resolve(chord, player_view=False, radio_session=True) is (
            Action.PARENT_FOLDER
        )
        assert resolve(chord, player_view=False, radio_session=False) is (
            Action.PARENT_FOLDER
        )
