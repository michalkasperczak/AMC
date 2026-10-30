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

from amc_wx_lite.gui import LiteFrame, radio_timeshift_label
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.library_db import LibraryDatabase, LibraryItem
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId
from amc_wx_lite.profile_layout import read_only_mirror
from amc_wx_lite.radio_recording import (
    RadioRecordingHistoryEntry,
    RadioRecordingPreferences,
    active_recording_rows,
    available_recorded_files,
    current_station_recording_label,
    format_duration,
    preferences_from_amc_state,
    preferences_with_overrides,
    recording_history_from_amc_state,
    recording_history_payload_from_event,
    recording_history_rows,
    recent_recording_history,
    station_activity_rows,
)
from amc_wx_lite.radio_source import RadioSource
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    FRONTIER_SMART_SUPPORTED_ACTIONS,
    SESSION_INDEPENDENT_ACTIONS,
    SONOS_SUPPORTED_ACTIONS,
    SPOTIFY_SUPPORTED_ACTIONS,
    TIDAL_SUPPORTED_ACTIONS,
    WIIM_SUPPORTED_ACTIONS,
    resolve,
)
from amc_wx_lite.state_store import Station


def test_current_station_recording_label_never_confuses_background_station() -> None:
    payload = {
        "recordings": [
            {
                "stationId": "radio-poznan",
                "stationName": "Radio Poznań",
                "state": "recording",
                "paused": False,
            },
            {
                "stationId": "radio-opole",
                "stationName": "Radio Opole",
                "state": "paused",
                "paused": True,
            },
        ]
    }

    assert current_station_recording_label(payload, "radio-poznan") == "nagrywanie"
    assert current_station_recording_label(payload, "radio-opole") == (
        "nagrywanie wstrzymane"
    )
    assert current_station_recording_label(payload, "radio-inne") == ""
    assert "radio-poznan" not in current_station_recording_label(
        payload, "radio-poznan"
    )


def test_current_station_recording_label_names_transition_states() -> None:
    assert current_station_recording_label(
        {"recordings": [{"stationId": "x", "state": "starting"}]}, "x"
    ) == "przygotowywanie nagrania"
    assert current_station_recording_label(
        {"recordings": [{"stationId": "x", "state": "stopping"}]}, "x"
    ) == "zatrzymywanie nagrania"
    assert current_station_recording_label({"count": 1}, "x") == ""


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


def test_separate_station_folders_preserve_explicit_station_folder() -> None:
    station = Station("one", "Radio One", "https://example.invalid/live")
    base = RadioRecordingPreferences(
        default_folder=r"D:\Radio",
        station_folders={"one": r"D:\Custom"},
    )
    enabled = preferences_with_overrides(base, separate_station_folders=True)
    assert enabled.payload_for(station)["folder"] == r"D:\Custom"
    assert "separateStationFolders" not in enabled.payload_for(station)
    other = Station("two", "Radio Two", station.url)
    assert enabled.payload_for(other)["separateStationFolders"] is True
    assert "separateStationFolders" not in base.payload_for(other)


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


def test_private_recording_preferences_override_profile_without_losing_station_folders() -> None:
    base = RadioRecordingPreferences(
        format="Mp3",
        bitrate_kbps=192,
        default_folder=r"D:\Profil",
        folder_preset="podcasts",
        station_folders={"one": r"D:\Stacja"},
        prefer_station_folder_in_new_schedules=True,
    )
    effective = preferences_with_overrides(
        base,
        recording_format="Flac",
        bitrate_kbps=255,
        default_folder=r"D:\AMC Python",
        override_default_folder=True,
        prefer_station_folder=False,
    )
    assert effective.format == "Flac"
    assert effective.bitrate_kbps == 256
    assert effective.default_folder == r"D:\AMC Python"
    assert effective.folder_preset == "radio"
    assert effective.radio_default_folder == r"D:\AMC Python"
    assert effective.station_folders == {"one": r"D:\Stacja"}
    assert effective.prefer_station_folder_in_new_schedules is False


def test_empty_private_recording_folder_means_hosts_system_default() -> None:
    effective = preferences_with_overrides(
        RadioRecordingPreferences(default_folder=r"D:\Profil"),
        default_folder="",
        override_default_folder=True,
    )
    assert effective.default_folder is None


def test_shared_podcast_folder_keeps_the_separate_radio_folder_for_return() -> None:
    base = RadioRecordingPreferences(
        default_folder=r"D:\Nagrania radia",
        folder_preset="radio",
        radio_default_folder=r"D:\Nagrania radia",
    )
    shared = preferences_with_overrides(
        base,
        use_podcast_downloads_folder=True,
        podcast_downloads_folder=r"D:\Podcasty",
        override_podcast_downloads_folder=True,
    )
    assert shared.folder_preset == "podcasts"
    assert shared.default_folder == r"D:\Podcasty"
    assert shared.radio_default_folder == r"D:\Nagrania radia"
    returned = preferences_with_overrides(
        shared,
        use_podcast_downloads_folder=False,
    )
    assert returned.folder_preset == "radio"
    assert returned.default_folder == r"D:\Nagrania radia"


def test_timeshift_choices_explain_duration_and_storage_in_polish() -> None:
    assert radio_timeshift_label(10) == "10 minut, w pamięci"
    assert radio_timeshift_label(60) == "1 godzina, na dysku"
    assert radio_timeshift_label(120) == "2 godziny, na dysku"
    assert radio_timeshift_label(720) == "12 godzin, na dysku"


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
    # W plikach R steruje losowaniem, ale nigdy nie uruchamia nagrania.
    assert (
        resolve(Chord("R"), player_view=True, radio_session=False)
        is Action.TOGGLE_SHUFFLE
    )
    assert resolve(Chord("T"), player_view=True, radio_session=False) is None


def test_stop_all_recordings_is_global_in_surfaces_and_execution() -> None:
    from amc_wx_lite import menu_model

    assert Action.RECORD_STOP_ALL in SESSION_INDEPENDENT_ACTIONS
    for supported in (
        TIDAL_SUPPORTED_ACTIONS,
        SPOTIFY_SUPPORTED_ACTIONS,
        WIIM_SUPPORTED_ACTIONS,
        SONOS_SUPPORTED_ACTIONS,
        FRONTIER_SMART_SUPPORTED_ACTIONS,
    ):
        assert Action.RECORD_STOP_ALL in supported

    for session in (
        SessionId.RADIO,
        SessionId.TIDAL,
        SessionId.SPOTIFY,
        SessionId.WIIM,
        SessionId.SONOS,
        SessionId.FRONTIER_SMART,
    ):
        navigator = Navigator()
        navigator.active = session
        calls: list[str] = []
        messages: list[str] = []
        frame = SimpleNamespace(
            navigator=navigator,
            announcer=SimpleNamespace(say=messages.append),
            _stop_all_radio_recordings=lambda: calls.append("stop"),
            _global_transient_previews=True,
            messages=SimpleNamespace(custom_seek_seconds=60),
        )
        LiteFrame._dispatch(frame, Action.RECORD_STOP_ALL)
        assert calls == ["stop"], session
        assert messages == [], session

        menu_item = next(
            entry
            for menu in menu_model.menus_for_session(session)
            for entry in menu.items
            if entry.action is Action.RECORD_STOP_ALL
        )
        assert menu_item.shortcut == "Ctrl+Alt+Shift+R"
        assert menu_item.needs_radio_session is False


def test_recording_bookmark_shortcuts_do_not_steal_letter_navigation() -> None:
    assert resolve(
        Chord("B"), player_view=True, radio_session=True
    ) is Action.ADD_BOOKMARK
    assert resolve(
        Chord("B", ctrl=True, shift=True), player_view=True, radio_session=True
    ) is Action.ADD_NAMED_BOOKMARK
    assert resolve(
        Chord("B", ctrl=True, shift=True), player_view=True, radio_session=True
    ) is Action.ADD_NAMED_BOOKMARK

    for chord, expected in (
        (Chord("B"), Action.ADD_BOOKMARK),
        (Chord("B", ctrl=True, shift=True), Action.ADD_NAMED_BOOKMARK),
        (Chord("B", ctrl=True, shift=True), Action.ADD_NAMED_BOOKMARK),
    ):
        assert resolve(
            chord,
            player_view=False,
            radio_session=True,
            radio_recording_context=True,
        ) is expected
    assert resolve(
        Chord("B"),
        player_view=False,
        radio_session=True,
        radio_recording_context=False,
    ) is None


def test_ctrl_r_nie_jest_polykany_przez_kontekstowy_akcelerator_menu() -> None:
    """Ctrl+R obsługuje hook okna, nie stan pozycji menu wxWidgets."""
    from amc_wx_lite import menu_model

    item = next(
        entry
        for menu in menu_model.build_menus()
        for entry in menu.items
        if entry.action is Action.RECORD_TOGGLE
    )
    assert item.shortcut == "Ctrl+R"
    assert item.needs_radio_session
    assert item.accelerator is False


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
    client.add_radio_recording_bookmark("id", "https://x", "Gość audycji")
    client.stop_all_radio_recordings()
    client.radio_recording_status()
    client.radio_recording_history()
    assert [entry[0] for entry in calls] == [
        "radio.recordingToggle",
        "radio.recordingPauseToggle",
        "radio.recordingSplit",
        "radio.recordingBookmark",
        "radio.recordingStopAll",
        "radio.recordingStatus",
        "radio.recordingHistory",
    ]
    assert calls[3][1] == {
        "stationId": "id",
        "url": "https://x",
        "name": "Gość audycji",
    }


def test_gui_adds_quick_radio_recording_bookmark_with_user_facing_message() -> None:
    class Runner:
        def submit(self, key, work, done, failed):
            assert key == "radio-recording-bookmark"
            try:
                done(work())
            except Exception as error:
                failed(error)

    class Client:
        def __init__(self):
            self.call = None

        def add_radio_recording_bookmark(self, station_id, url, name):
            self.call = (station_id, url, name)
            return {
                "state": "added",
                "positionSeconds": 65,
                "affectedCount": 1,
                "recordingCount": 1,
            }

    messages: list[str] = []
    client = Client()
    frame = SimpleNamespace(
        client=client,
        runner=Runner(),
        _recording_station=lambda: Station(
            "radio-test", "Radio Test", "https://example.invalid/live"
        ),
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._add_radio_recording_bookmark(frame, named=False)
    assert client.call == (
        "radio-test", "https://example.invalid/live", None
    )
    assert messages == ["Dodano zakładkę nagrania: 1:05"]


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


def test_scheduled_terminal_event_refreshes_persisted_schedule_state() -> None:
    messages: list[str] = []
    refreshes: list[str] = []
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        announcer=SimpleNamespace(say=messages.append),
        _refresh_recording_status=lambda: refreshes.append("recording-status"),
        _refresh_persisted_radio_schedules=lambda: refreshes.append(
            "schedule-status"
        ),
        _remember_recording_result=lambda _payload: True,
        navigator=SimpleNamespace(
            sessions={
                SessionId.RADIO: SimpleNamespace(library_view=LibraryView.FAVORITES),
                SessionId.FILES: SimpleNamespace(library_view=LibraryView.ALL_FILES),
            }
        ),
    )

    LiteFrame._handle_engine_event(frame, "radio.recordingStopped", {
        "stationName": "Radio Test",
        "scheduleName": "Poranna audycja",
        "path": r"D:\Nagrania\radio-test.mp3",
        "savedFileCount": 1,
    })

    assert refreshes == ["schedule-status", "recording-status"]
    assert messages == [
        "Zatrzymano zaplanowane nagrywanie Radio Test: radio-test.mp3"
    ]


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
    assert decorated[0].state_detail == "odtwarzane, nagrywane"
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


def test_radio_playlist_contents_receive_live_playback_and_recording_state() -> None:
    """Stan stacji musi być dostępny także wewnątrz playlisty radiowej."""
    from amc_wx_lite.list_model import Row

    navigator = Navigator()
    navigator.switch_session(SessionId.RADIO)
    navigator.apply_radio_view(
        LibraryView.PLAYLIST_CONTENTS,
        "Playlista — Poranek",
        [Row("radio", "Radio Poznań", "station")],
        playlist_id="poranek",
    )
    frame = LiteFrame.__new__(LiteFrame)
    frame.navigator = navigator
    frame.options = SimpleNamespace(
        radio_playback_state_position="before",
        radio_recording_state_position="after",
    )
    frame._transport_status_by_session = {
        SessionId.RADIO: {
            "engine": "radio",
            "loadedId": "radio",
            "paused": False,
        }
    }
    frame._last_status = {}
    frame._last_recording_status = {
        "recordings": [{"stationId": "radio", "state": "recording"}]
    }
    synchronized: list[bool] = []
    frame._sync_views = lambda: synchronized.append(True)

    LiteFrame._apply_radio_activity_status(frame)

    row = navigator.session.model.rows[0]
    assert row.title == "Radio Poznań"
    assert row.state_prefix == "odtwarzane"
    assert row.state_detail == "nagrywane"
    assert synchronized == [True]


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
                "path": r"D:\Nagrania\Polskie Radio Trójka 2026-10-19.mp3",
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
    assert rows[0].title == "Polskie Radio Trójka 2026-10-19"
    assert rows[0].url == "https://example.invalid/live"
    assert rows[0].detail == ""
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
    assert rows[0].title == "audycja"
    assert not rows[0].title.startswith("Nagrane")
    assert rows[0].path == r"D:\Nagrania\audycja.mp3"
    assert "techniczne-udane" not in rows[0].title
    assert rows[1].title == "Nieudane, Radio Awaria, Brak połączenia"
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
    assert row.title == "Zatrzymane, brak pliku nagrania, brak"
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
    assert outside.detail == ""
    interrupted = next(row for row in rows if row.title.startswith("Zatrzymane"))
    assert interrupted.path == r"D:\Nagrania\czesc.mp3"
    assert all("plik-" not in row.title for row in rows)


def test_recorded_view_drops_stale_database_rows_whose_files_are_gone() -> None:
    files = [
        LibraryItem("jest", "Jest", r"D:\Nagrania\jest.mp3", 0, False, True, True, True, 2),
        LibraryItem("brak", "Brak", r"D:\Nagrania\brak.mp3", 0, False, True, True, True, 1),
    ]
    states = {
        r"D:\Nagrania\jest.mp3": "available",
        r"D:\Nagrania\brak.mp3": "missing",
    }

    result = available_recorded_files(files, path_probe=states.__getitem__)

    assert [item.id for item in result] == ["jest"]


def test_visible_recording_history_does_not_merge_stale_archives_forever() -> None:
    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    method = source[source.index("def _show_radio_recording_history") :]
    method = method[:method.index("def _show_radio_schedules")]
    assert "recent_recording_history(current_entries, stored_entries)" in method
    assert "recorded_radio_items" not in method
    assert "snapshot.recording_history" not in method


def test_recent_recording_history_survives_restart_without_dead_folder_rows() -> None:
    def entry(identifier: str, path: str, outcome: str, ticks: int):
        return RadioRecordingHistoryEntry(
            identifier, "radio", identifier, path, outcome, "", "", 0, ticks, 1
        )

    current = [entry("bieżące", r"D:\Nagrania\bieżące.mp3", "completed", 50)]
    stored = [
        entry("bieżące", r"D:\Nagrania\bieżące.mp3", "completed", 50),
        entry("dzisiejsze", r"D:\Nagrania\dzisiejsze.mp3", "completed", 40),
        entry("usunięte", r"D:\Nagrania\usunięte.mp3", "completed", 30),
        entry("nieudane", "", "failed", 20),
    ]
    states = {
        r"D:\Nagrania\bieżące.mp3": "available",
        r"D:\Nagrania\dzisiejsze.mp3": "available",
        r"D:\Nagrania\usunięte.mp3": "missing",
    }

    merged = recent_recording_history(
        current, stored, path_probe=lambda path: states[path]
    )

    assert [item.id for item in merged] == ["bieżące", "dzisiejsze", "nieudane"]


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
    events = nav.restore_transient_navigation(snapshot)

    assert nav.active is SessionId.FILES
    assert nav.session.view is View.PLAYER
    assert nav.session.library_view is LibraryView.ALL_FILES
    assert nav.session.model.selected_id == "plik"
    assert events == [], "natywna kontrolka wypowie odzyskany element bez komunikatu Powrót"


def test_empty_active_recordings_have_an_exact_user_facing_count() -> None:
    nav = Navigator()

    events = nav.apply_radio_view(
        LibraryView.ACTIVE_RADIO_RECORDINGS,
        "Nagrywane",
        [],
    )

    assert len(events) == 1
    assert events[0].text == "zero elementów"


def test_recording_history_announces_short_heading_and_selected_position() -> None:
    nav = Navigator()
    row = Row(
        item_id="radio-recording-history:1",
        title="audycja",
        kind="track",
        path=r"D:\Nagrania\audycja.mp3",
    )

    events = nav.apply_library_view(
        LibraryView.RECORDED_RADIO_FILES,
        "Nagrane",
        [row],
        announce_count=False,
        announce_position=True,
    )

    assert events[0].text == "Nagrane, 1 z 1"
    assert "element" not in events[0].text


def test_recording_history_gui_uses_complete_single_item_announcement() -> None:
    """Alt+Shift+R korzysta z tej samej poprawki co Alt+R."""
    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    method = source[source.index("def _show_radio_recording_history") :]
    method = method[:method.index("def _show_radio_schedules")]
    assert "self._announce_transient_preview(events)" in method


def test_active_recordings_announce_view_position_once_not_in_each_row() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    rows = [
        Row(item_id="a1", title="pierwszy.mp3", kind="station"),
        Row(item_id="a2", title="drugi.mp3", kind="station"),
    ]

    nav.apply_radio_view(
        LibraryView.ACTIVE_RADIO_RECORDINGS,
        "Nagrywane",
        rows,
    )
    nav.session.model.select_id("a2")
    events = nav.apply_radio_view(
        LibraryView.ACTIVE_RADIO_RECORDINGS,
        "Nagrywane",
        rows,
        announce_position=True,
    )

    assert events[0].text == "Nagrywane, 2 z 2"
    assert [row.title for row in nav.session.model.rows] == [
        "pierwszy.mp3",
        "drugi.mp3",
    ]


def test_active_and_completed_recording_views_remember_separate_selection() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    active = [
        Row(item_id="a1", title="Nagrywane, jeden", kind="station"),
        Row(item_id="a2", title="Nagrywane, dwa", kind="station"),
    ]
    nav.apply_radio_view(LibraryView.ACTIVE_RADIO_RECORDINGS, "Nagrywane", active)
    nav.session.model.select_id("a2")
    nav.apply_radio_view(None, "Radio", [Row(item_id="s1", title="Stacja", kind="station")])
    nav.apply_radio_view(LibraryView.ACTIVE_RADIO_RECORDINGS, "Nagrywane", active)
    assert nav.session.model.selected_id == "a2"

    nav.active = SessionId.FILES
    completed = [
        Row(item_id="h1", title="jeden", kind="track"),
        Row(item_id="h2", title="dwa", kind="track"),
    ]
    nav.apply_library_view(
        LibraryView.RECORDED_RADIO_FILES,
        "Historia nagrywania",
        completed,
        announce_count=False,
    )
    nav.session.model.select_id("h2")
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", [])
    nav.apply_library_view(
        LibraryView.RECORDED_RADIO_FILES,
        "Historia nagrywania",
        completed,
        announce_count=False,
    )
    assert nav.session.model.selected_id == "h2"


def test_escape_and_backspace_share_the_recording_preview_exit_action() -> None:
    for chord in (Chord("Escape"), Chord("Back")):
        expected = (
            Action.CLOSE_LIST_PREVIEW
            if chord.key == "Escape"
            else Action.PARENT_FOLDER
        )
        assert resolve(chord, player_view=False, radio_session=True) is expected
        assert resolve(chord, player_view=False, radio_session=False) is expected
