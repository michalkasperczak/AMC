from __future__ import annotations

import json
from types import SimpleNamespace

from amc_wx_lite import menu_model, radio_recognition
from amc_wx_lite.gui import LiteFrame, RadioRecognitionHistoryDialog
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import LiteState, Options, StateStore


def _host_entry(*, ticks: int, title: str = "Piosenka") -> dict:
    return {
        "stationId": "radio-1",
        "stationName": "Radio Poznań",
        "title": title,
        "artist": "Wykonawca",
        "album": "Album",
        "releaseDate": "2026",
        "providerUri": "https://example.test/wynik",
        "recognizedUtcTicks": ticks,
    }


def test_scope_labels_are_explicit_user_text() -> None:
    assert radio_recognition.scope_label("CurrentStation") == (
        "Tylko aktualnie odtwarzana stacja"
    )
    assert radio_recognition.normalize_scope("Technical.Enum.Value") == (
        radio_recognition.CURRENT_STATION
    )
    assert "CurrentStation" not in radio_recognition.scope_label("CurrentStation")


def test_host_result_is_persistable_and_recent_duplicate_is_not_added() -> None:
    now = radio_recognition.DOTNET_UNIX_EPOCH_TICKS + 900_000_000
    history: list[dict] = []
    payload = {"requested": 1, "recognized": [_host_entry(ticks=now)]}
    first = radio_recognition.merge_host_result(
        history, payload, current_ticks=now
    )
    second = radio_recognition.merge_host_result(
        history, payload, current_ticks=now + 10_000_000
    )
    assert len(first.added) == 1
    assert not first.duplicates
    assert not second.added
    assert len(second.duplicates) == 1
    assert len(history) == 1
    assert radio_recognition.track_label(history[0]).startswith(
        "Piosenka — Wykonawca, Radio Poznań, "
    )


def test_clip_token_refreshes_duplicate_but_is_not_persisted(tmp_path) -> None:
    now = radio_recognition.DOTNET_UNIX_EPOCH_TICKS + 900_000_000
    history: list[dict] = []
    first = _host_entry(ticks=now)
    first["clipToken"] = "first-token"
    radio_recognition.merge_host_result(
        history, {"recognized": [first]}, current_ticks=now
    )
    second = dict(first, clipToken="fresh-token")
    radio_recognition.merge_host_result(
        history, {"recognized": [second]}, current_ticks=now + 10_000_000
    )
    assert len(history) == 1
    assert history[0]["clipToken"] == "fresh-token"
    store = StateStore(tmp_path)
    store.save(LiteState(recognized_tracks=history))
    assert "clipToken" not in store.path.read_text(encoding="utf-8")
    assert "clipToken" not in store.load().recognized_tracks[0]


def test_provider_uri_and_malformed_records_do_not_leak() -> None:
    ticks = radio_recognition.DOTNET_UNIX_EPOCH_TICKS + 10_000_000
    unsafe = _host_entry(ticks=ticks)
    unsafe["providerUri"] = "javascript:alert(1)"
    history: list[dict] = []
    radio_recognition.merge_host_result(
        history,
        {"requested": 1, "recognized": [unsafe]},
        current_ticks=ticks,
    )
    assert history[0]["providerUri"] == ""
    assert radio_recognition.read_tracks([{"id": "x", "title": object()}]) == []


def test_history_is_imported_only_from_the_radio_profile_section() -> None:
    ticks = radio_recognition.DOTNET_UNIX_EPOCH_TICKS + 10_000_000
    entry = {"id": "wynik-1", **_host_entry(ticks=ticks)}
    imported = radio_recognition.tracks_from_amc_state({
        "radio": {"recognizedTracks": [entry]},
        "recognizedTracks": [{"id": "nie-ten", **_host_entry(ticks=ticks)}],
    })
    assert imported == (radio_recognition.read_tracks([entry])[0],)
    assert radio_recognition.tracks_from_amc_state({
        "radio": {"recognizedTracks": "techniczny tekst"}
    }) == ()


def test_state_round_trip_keeps_settings_and_history(tmp_path) -> None:
    ticks = radio_recognition.DOTNET_UNIX_EPOCH_TICKS + 10_000_000
    store = StateStore(tmp_path)
    state = LiteState(
        options=Options(
            radio_recognition_monitoring=True,
            radio_recognition_scope=radio_recognition.CURRENT_AND_RECORDING_STATIONS,
            radio_recognition_announcements=False,
        ),
        recognized_tracks=[{
            "id": "wynik-1",
            **_host_entry(ticks=ticks),
        }],
        recognized_tracks_imported_from_amc=True,
    )
    store.save(state)
    loaded = store.load()
    assert loaded.options.radio_recognition_monitoring is True
    assert loaded.options.radio_recognition_scope == (
        radio_recognition.CURRENT_AND_RECORDING_STATIONS
    )
    assert loaded.options.radio_recognition_announcements is False
    assert loaded.recognized_tracks[0]["title"] == "Piosenka"
    assert loaded.recognized_tracks_imported_from_amc is True
    raw = json.loads(store.path.read_text(encoding="utf-8"))
    assert "recognized_tracks" in raw


def test_radio_shortcuts_match_the_original_window() -> None:
    assert resolve(
        Chord("S"), player_view=True, radio_session=True
    ) is Action.RECOGNIZE_RADIO_TRACK
    assert resolve(
        Chord("S", shift=True), player_view=True, radio_session=True
    ) is Action.TOGGLE_RADIO_RECOGNITION_MONITORING
    assert resolve(
        Chord("S", ctrl=True, alt=True),
        player_view=False,
        radio_session=True,
    ) is Action.VIEW_RADIO_RECOGNITION_HISTORY
    assert resolve(
        Chord("S", ctrl=True, alt=True, shift=True),
        player_view=False,
        radio_session=True,
    ) is Action.TOGGLE_RADIO_RECOGNITION_ANNOUNCEMENTS
    assert resolve(
        Chord("S"), player_view=False, radio_session=True
    ) is None


def test_radio_menu_exposes_same_actions_and_stateful_toggles() -> None:
    entries = [
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is not None
    ]
    by_action = {item.action: item for item in entries}
    assert by_action[Action.RECOGNIZE_RADIO_TRACK].shortcut == "S"
    assert by_action[Action.VIEW_RADIO_RECOGNITION_HISTORY].shortcut == "Ctrl+Alt+S"
    assert by_action[Action.TOGGLE_RADIO_RECOGNITION_MONITORING].checkable
    assert by_action[Action.TOGGLE_RADIO_RECOGNITION_ANNOUNCEMENTS].checkable


def test_client_sends_only_scope_not_audio() -> None:
    client = object.__new__(LiteHostClient)
    seen: dict = {}

    def call(op, args=None, *, timeout=20.0):
        seen.update(op=op, args=args, timeout=timeout)
        return {"requested": 0}

    client.call = call
    client.recognize_radio_tracks(radio_recognition.RECORDING_STATIONS)
    assert seen == {
        "op": "radio.recognition.run",
        "args": {"scope": "RecordingStations"},
        "timeout": 45.0,
    }


def test_paused_sources_do_not_keep_automatic_recognition_awake() -> None:
    frame = SimpleNamespace(
        options=Options(
            radio_recognition_scope=(
                radio_recognition.CURRENT_AND_RECORDING_STATIONS
            )
        ),
        _playing_sessions={SessionId.RADIO},
        _transport_status_by_session={SessionId.RADIO: {"paused": True}},
        _last_recording_status={
            "count": 1,
            "recordings": [{"state": "paused", "paused": True}],
        },
    )
    assert LiteFrame._recognition_source_available(frame) is False

    frame._last_recording_status["recordings"][0] = {
        "state": "recording",
        "paused": False,
    }
    assert LiteFrame._recognition_source_available(frame) is True


def test_new_automatic_result_refreshes_open_history_with_announcements_off() -> None:
    for announcements in (False, True):
        events: list[str] = []
        history: list[dict] = []
        payload = {
            "requested": 1,
            "recognized": [_host_entry(ticks=radio_recognition.now_dotnet_ticks())],
        }

        def submit(_name, work, done, _failed):
            done(work())

        frame = SimpleNamespace(
            client=SimpleNamespace(recognize_radio_tracks=lambda _scope: payload),
            runner=SimpleNamespace(submit=submit),
            state=SimpleNamespace(recognized_tracks=history),
            options=Options(
                radio_recognition_monitoring=True,
                radio_recognition_announcements=announcements,
            ),
            messages=SimpleNamespace(speaks_routine=True),
            announcer=SimpleNamespace(say=lambda _message: events.append("spoken")),
            _radio_recognition_pending=False,
            _radio_recognition_history_dialog=SimpleNamespace(
                refresh_after_recognition=lambda: events.append("refreshed")
            ),
            _save_state=lambda: events.append("saved"),
        )

        LiteFrame._recognize_radio_track(frame, automatic=True)

        assert len(history) == 1
        assert events[:2] == ["saved", "refreshed"]
        assert events.count("spoken") == int(announcements)


def test_live_history_refresh_preserves_filter_and_selected_entry() -> None:
    calls: list[object] = []
    dialog = SimpleNamespace(
        _selected_entry=lambda: {"id": "old-track"},
        history=SimpleNamespace(GetSelections=lambda: [4]),
        _refresh_rows=lambda **kwargs: calls.append(kwargs),
    )

    RadioRecognitionHistoryDialog.refresh_after_recognition(dialog)

    assert calls == [{"fallback_index": 4, "preferred_id": "old-track"}]




def test_history_tab_order_starts_with_tracks_then_station_query() -> None:
    from pathlib import Path

    source = (Path(__file__).parents[1] / "amc_wx_lite" / "gui.py").read_text(
        encoding="utf-8"
    )
    history = source.split("class RadioRecognitionHistoryDialog", 1)[1].split(
        "class GeneralPlaybackOptionsDialog", 1
    )[0]
    assert history.index("self.history = wx.ListBox(panel") < history.index(
        "self.station_query = wx.TextCtrl(panel)"
    )
    assert "wx.CallAfter(self._focus_history)" in history
    assert "self.history.SetFocus()" in history
    assert "self.station_filter" not in history


def test_history_copy_spotify_link_announces_only_completed_copy() -> None:
    entry = {"id": "recognized-1", "title": "Piosenka", "artist": "Wykonawca"}
    messages: list[str] = []
    copied: list[str] = []
    match = {
        "service": "spotify",
        "title": "Piosenka",
        "artist": "Wykonawca",
        "publicUrl": "https://open.spotify.com/track/test",
    }
    dialog = SimpleNamespace(
        _selected_entry=lambda: entry,
        _default_service="spotify",
        _internal_services=("spotify",),
        _copy_to_clipboard=lambda link: copied.append(link) or True,
        _set_status=messages.append,
        _resolve_action=lambda _entry, _action, ready, _failed: ready(match),
    )

    RadioRecognitionHistoryDialog._copy_track_link(dialog)

    assert copied == ["https://open.spotify.com/track/test"]
    assert messages == ["Skopiowano link Spotify"]


def test_history_station_query_filters_tracks_without_selecting_station() -> None:
    class History:
        def __init__(self) -> None:
            self.labels: list[str] = []
            self.focused = False

        def Set(self, labels: list[str]) -> None:
            self.labels = labels

        def SetSelection(self, _index: int) -> None:
            pass

        def SetFocus(self) -> None:
            self.focused = True

    query = SimpleNamespace(value="", GetValue=lambda: query.value)
    history = History()
    status_text: list[str] = []
    status = SimpleNamespace(
        SetLabel=lambda value: status_text.append(value),
        SetName=lambda _value: None,
    )
    dialog = SimpleNamespace(
        _entries=[
            {"id": "a", "stationId": "1", "stationName": "Radio Gdańsk", "title": "A"},
            {"id": "b", "stationId": "2", "stationName": "TOK FM", "title": "B"},
            {"id": "c", "stationId": "1", "stationName": "Radio Gdańsk", "title": "C"},
        ],
        station_query=query,
        history=history,
        status=status,
        _station_matches_query=RadioRecognitionHistoryDialog._station_matches_query,
    )
    dialog._refresh_rows = lambda: RadioRecognitionHistoryDialog._refresh_rows(dialog)

    dialog._refresh_rows()
    assert [row["id"] for row in dialog._visible] == ["a", "b", "c"]
    query.value = "GDA"
    RadioRecognitionHistoryDialog._on_station_query(dialog, None)
    assert [row["id"] for row in dialog._visible] == ["a", "c"]
    assert status_text[-1] == "Wpisów: 2, filtr stacji: GDA"
    assert history.focused is False
    assert RadioRecognitionHistoryDialog._station_matches_query("Radio Gdańsk", "gdansk")

    query.value = "nieistniejąca"
    RadioRecognitionHistoryDialog._on_station_query(dialog, None)
    assert dialog._visible == []
    assert status_text[-1] == "Brak rozpoznań dla wpisanego filtra stacji"
