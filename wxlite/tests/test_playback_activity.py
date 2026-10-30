from __future__ import annotations

from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
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
