from __future__ import annotations

from types import SimpleNamespace
from typing import Any, cast

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame, SearchDialog
from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.playback_activity import activity_rows
from amc_wx_lite.search_catalog import SearchEntry


def test_activity_marks_exact_playable_row_and_preserves_details() -> None:
    rows = [
        Row("a", "Pierwszy", "track", detail="FLAC"),
        Row("b", "Drugi", "episode", detail="25 minut"),
    ]
    decorated = activity_rows(
        rows, current_item_id="b", playing=True, paused=False
    )

    assert decorated[0].playback_state == ""
    assert decorated[1].playback_state == "Odtwarzane"
    assert decorated[1].detail == "25 minut"
    model = ListModel(decorated)
    assert model.text_for(1, 0) == "Odtwarzane, Drugi"
    assert model.text_for(1, 2) == "25 minut"


def test_real_activity_setting_renders_before_after_and_off_without_losing_cue() -> None:
    files = SimpleNamespace(
        model=ListModel([Row("current", "Bieżący", "track")]),
        current_material_id="current",
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={SessionId.FILES: files}),
        _playing_sessions={SessionId.FILES},
        _transport_status_by_session={SessionId.FILES: {"paused": False}},
        options=SimpleNamespace(radio_playback_state_position="before"),
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )

    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert files.model.text_for(0, 0) == "Odtwarzane, Bieżący"
    assert files.model.text_for(0, 2) == ""

    frame.options.radio_playback_state_position = "after"
    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert files.model.text_for(0, 0) == "Bieżący"
    assert files.model.text_for(0, 2) == "Odtwarzane"

    frame.options.radio_playback_state_position = "off"
    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert files.model.text_for(0, 0) == "Bieżący"
    assert files.model.text_for(0, 2) == ""
    assert files.model.rows[0].playback_activity is True


def test_shared_setting_refreshes_each_loaded_native_and_streamer_list() -> None:
    native = (
        SessionId.FILES, SessionId.PODCASTS, SessionId.SPOTIFY,
        SessionId.TIDAL, SessionId.RADIOARCHIWISTA,
    )
    sessions = {
        session_id: SimpleNamespace(
            model=ListModel([Row(f"{session_id.value}:1", "Materiał", "track")]),
            current_material_id=f"{session_id.value}:1",
        )
        for session_id in native
    }
    for session_id in (SessionId.WIIM, SessionId.SONOS):
        sessions[session_id] = SimpleNamespace(
            model=ListModel([Row(f"{session_id.value}:1", "Materiał", "track")]),
            current_material_id=f"{session_id.value}:1",
        )
    radio_refreshed: list[bool] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.FILES, sessions=sessions),
        options=SimpleNamespace(radio_playback_state_position="after"),
        _playing_sessions=set(native),
        _transport_status_by_session={session_id: {"paused": False} for session_id in native},
        _wiim_snapshot={"playbackState": "playing"},
        _sonos_snapshot={"playbackState": "playing"},
        _apply_radio_activity_status=lambda: radio_refreshed.append(True),
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )

    LiteFrame._refresh_playback_activity_presentations(frame)

    assert radio_refreshed == [True]
    for session_id, session in sessions.items():
        assert session.model.text_for(0, 0) == "Materiał"
        expected = "Odtwarzane" if session_id in native else "odtwarzane"
        assert session.model.text_for(0, 2) == expected
        assert session.model.rows[0].playback_activity is True


def test_activity_pause_transition_and_a_to_b_clear_old_state() -> None:
    rows = [Row("a", "A", "track"), Row("b", "B", "track")]
    first = activity_rows(rows, current_item_id="a", playing=True, paused=False)
    second = activity_rows(first, current_item_id="b", playing=True, paused=True)

    assert [row.playback_state for row in second] == ["", "Wstrzymane"]
    stopped = activity_rows(second, current_item_id="b", playing=False, paused=False)
    assert [row.playback_state for row in stopped] == ["", ""]


def test_activity_does_not_mark_containers_bookmarks_or_failed_rows() -> None:
    rows = [
        Row("same", "Album", "album"),
        Row("bookmark:1", "Zakładka", "track"),
        Row("failed", "Nieudane nagranie", "track", activation_message="Brak pliku"),
    ]

    for item_id in ("same", "bookmark:1", "failed"):
        decorated = activity_rows(
            rows, current_item_id=item_id, playing=True, paused=False
        )
        assert all(not row.playback_state for row in decorated)


def test_activity_resolves_presentation_row_to_real_material_and_session() -> None:
    rows = [
        Row(
            "bookmark:42", "Zakładka utworu", "track",
            material_id="track-7", material_session_id="local",
        ),
        Row(
            "bookmark:43", "Obca sesja", "track",
            material_id="track-7", material_session_id="podcasts",
        ),
    ]

    decorated = activity_rows(
        rows,
        current_item_id="track-7",
        playing=True,
        paused=False,
        session_id="files",
    )

    assert [row.playback_state for row in decorated] == ["Odtwarzane", ""]
    assert decorated[0].playback_activity is True


def test_activity_matrix_covers_queue_playlist_history_search_and_bookmark_views() -> None:
    rows_by_view = {
        "kolejka": Row("queue:54", "Kolejka", "track", material_id="media:7"),
        "zawartość playlisty": Row(
            "playlist-entry:3", "Playlista", "track", material_id="media:7"
        ),
        "historia": Row("history:15", "Historia", "track", material_id="media:7"),
        "wyniki wyszukiwania": Row("media:7", "Wynik", "track"),
        "zakładka": Row("bookmark:4", "Zakładka", "track", material_id="media:7"),
    }
    for view, row in rows_by_view.items():
        decorated = activity_rows(
            [row],
            current_item_id="media:7",
            playing=True,
            paused=False,
            session_id="spotify",
        )
        assert decorated[0].playback_state == "Odtwarzane", view

    wrong_session = activity_rows(
        [Row("queue:54", "Obca kolejka", "track", material_id="media:7",
             material_session_id="tidal")],
        current_item_id="media:7",
        playing=True,
        paused=False,
        session_id="spotify",
    )
    assert wrong_session[0].playback_state == ""


def test_activity_matrix_marks_only_confirmed_playable_material_in_each_native_session() -> None:
    kinds = {
        SessionId.FILES: "track",
        SessionId.PODCASTS: "episode",
        SessionId.SPOTIFY: "track",
        SessionId.TIDAL: "track",
        SessionId.RADIOARCHIWISTA: "recording",
    }
    for session_id, kind in kinds.items():
        state = SimpleNamespace(
            model=ListModel([
                Row("other", "Inny", kind),
                Row("playing", "Bieżący", kind),
                Row("container", "Kontener", "playlist"),
            ]),
            current_material_id="playing",
        )
        state.model.select_id("other")
        frame = SimpleNamespace(
            navigator=SimpleNamespace(sessions={session_id: state}),
            _playing_sessions={session_id},
            _transport_status_by_session={session_id: {"paused": False}},
        )
        typed_frame = cast(Any, frame)
        typed_frame._native_playback_snapshot = lambda current: (
            LiteFrame._native_playback_snapshot(typed_frame, current)
        )

        LiteFrame._apply_native_playback_activity_status(typed_frame, session_id)
        assert state.model.text_for(1, 0) == "Odtwarzane, Bieżący", session_id
        assert state.model.selected_id == "other", session_id

        typed_frame._transport_status_by_session[session_id]["paused"] = True
        LiteFrame._apply_native_playback_activity_status(typed_frame, session_id)
        assert state.model.text_for(1, 0) == "Wstrzymane, Bieżący", session_id

        typed_frame._playing_sessions = set()
        LiteFrame._apply_native_playback_activity_status(typed_frame, session_id)
        assert state.model.text_for(1, 0) == "Bieżący", session_id


def test_model_selection_survives_accessible_text_refresh() -> None:
    model = ListModel([Row("a", "A", "track"), Row("b", "B", "track")])
    model.select_id("b")
    model.replace(
        activity_rows(
            model.rows, current_item_id="a", playing=True, paused=False
        ),
        preferred_id=model.selected_id,
    )
    assert model.selected_id == "b"
    assert model.selected_index == 1


def _session_state(item_id: str) -> SimpleNamespace:
    return SimpleNamespace(current_material_id=item_id)


def test_search_label_requires_same_session_and_same_stable_id() -> None:
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={
            SessionId.FILES: _session_state("shared"),
            SessionId.PODCASTS: _session_state("other"),
            SessionId.SPOTIFY: _session_state(""),
        }),
        _playing_sessions={SessionId.FILES},
        _transport_status_by_session={SessionId.FILES: {"paused": False}},
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )
    row = Row("shared", "Ten sam identyfikator", "track")

    assert LiteFrame._search_playback_label(
        frame, SearchEntry(SessionId.FILES, row)
    ) == "Odtwarzane"
    assert LiteFrame._search_playback_label(
        frame, SearchEntry(SessionId.PODCASTS, row)
    ) == ""


def test_search_honours_off_and_focus_callback_cues_confirmed_material() -> None:
    entry = SearchEntry(
        SessionId.FILES, Row("shared", "Ten sam identyfikator", "track")
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={SessionId.FILES: _session_state("shared")}),
        _playing_sessions={SessionId.FILES},
        _transport_status_by_session={SessionId.FILES: {"paused": False}},
        options=SimpleNamespace(radio_playback_state_position="off"),
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )

    assert LiteFrame._search_playback_label(frame, entry) == ""

    cued: list[SearchEntry] = []
    dialog = SimpleNamespace(_entries=[entry], _playback_cue=cued.append)
    SearchDialog._on_result_focused(
        dialog, SimpleNamespace(GetIndex=lambda: 0, Skip=lambda: None)
    )
    assert cued == [entry]


def test_apply_activity_skips_work_when_rows_and_playback_are_unchanged(monkeypatch) -> None:
    from amc_wx_lite import gui

    files = SimpleNamespace(
        model=ListModel([Row("a", "A", "track"), Row("b", "B", "track")]),
        current_material_id="a",
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={SessionId.FILES: files}),
        _playing_sessions={SessionId.FILES},
        _transport_status_by_session={SessionId.FILES: {"paused": False}},
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )
    computed = []
    real = gui.playback_activity_rows
    monkeypatch.setattr(
        gui, "playback_activity_rows", lambda *a, **k: computed.append(1) or real(*a, **k)
    )

    for _ in range(3):
        LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert len(computed) == 1 and files.model.text_for(0, 0) == "Odtwarzane, A"

    frame._transport_status_by_session[SessionId.FILES]["paused"] = True
    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert len(computed) == 2 and files.model.text_for(0, 0) == "Wstrzymane, A"

    # Nowa lista z loadera przy tym samym stanie też musi dostać napis.
    files.model.replace([Row("a", "A", "track"), Row("b", "B", "track")])
    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert len(computed) == 3 and files.model.text_for(0, 0) == "Wstrzymane, A"

    files.current_material_id = "b"
    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert files.model.text_for(0, 0) == "A" and files.model.text_for(1, 0) == "Wstrzymane, B"

    frame._playing_sessions = set()
    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)
    assert files.model.text_for(1, 0) == "B", "zatrzymanie zdejmuje napis"


def test_apply_activity_changes_only_text_not_selection() -> None:
    files = SimpleNamespace(
        model=ListModel([Row("a", "A", "track"), Row("b", "B", "track")]),
        current_material_id="a",
    )
    files.model.select_id("b")
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={SessionId.FILES: files}),
        _playing_sessions={SessionId.FILES},
        _transport_status_by_session={SessionId.FILES: {"paused": True}},
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )

    LiteFrame._apply_native_playback_activity_status(frame, SessionId.FILES)

    assert files.model.text_for(0, 0) == "Wstrzymane, A"
    assert files.model.selected_id == "b"


def test_tidal_confirmed_session_marks_its_exact_track_without_moving_focus() -> None:
    """The native TIDAL callback commits an ID before the visible list sync."""
    tidal = SimpleNamespace(
        model=ListModel([
            Row("tidal:a", "Pierwszy", "track"),
            Row("tidal:b", "Bieżący", "track"),
        ]),
        current_material_id="tidal:b",
    )
    tidal.model.select_id("tidal:a")
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={SessionId.TIDAL: tidal}),
        _playing_sessions={SessionId.TIDAL},
        _transport_status_by_session={SessionId.TIDAL: {"paused": False}},
    )
    frame._native_playback_snapshot = lambda session: (
        LiteFrame._native_playback_snapshot(frame, session)
    )

    LiteFrame._apply_native_playback_activity_status(frame, SessionId.TIDAL)

    assert tidal.model.text_for(1, 0) == "Odtwarzane, Bieżący"
    assert tidal.model.selected_id == "tidal:a"
