"""Queue speech and editing through the same GUI paths used by shortcuts."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui
from amc_wx_lite.list_model import Row, rows_from_queue_status
from amc_wx_lite.navigation import Announce, Navigator, SessionId
from amc_wx_lite.shortcuts import Action, Chord, resolve


MINUTE = 60 * 10_000_000


def _rows() -> list[Row]:
    return [
        Row("a", "Alfa", "track", duration_ticks=2 * MINUTE),
        Row("b", "Beta", "track", duration_ticks=3 * MINUTE),
    ]


def test_queue_views_speak_total_duration_or_unknown_part():
    nav = Navigator()
    nav.active = SessionId.FILES
    effects = nav.apply_live_queue(_rows())
    assert effects == [Announce("Kolejka odtwarzania, 2 pozycje, łączny czas trwania 5 min 0 s")]
    effects = nav.apply_live_queue([_rows()[0], Row("b", "Beta", "track")])
    assert effects == [Announce(
        "Kolejka odtwarzania, 2 pozycje, znany czas trwania 2 min 0 s, część bez danych"
    )]
    nav.active = SessionId.SPOTIFY
    assert "łączny czas trwania 5 min" in nav.apply_spotify_queue(_rows())[0].text
    nav.active = SessionId.TIDAL
    assert "łączny czas trwania 5 min" in nav.apply_tidal_queue(_rows())[0].text


def test_live_queue_duration_uses_library_lengths_and_bad_values_are_unknown():
    payload = {"rows": [{"id": "a", "title": "Alfa"}, {"id": "b", "title": "Beta"}]}
    frame = SimpleNamespace(
        client=SimpleNamespace(queue_status=lambda: payload),
        library=SimpleNamespace(
            is_available=True,
            duration_ticks_for_ids=lambda ids: {"a": 2 * MINUTE, "b": 3 * MINUTE},
        ),
    )
    enriched = gui.LiteFrame._queue_status_with_durations(frame)
    assert [r.duration_ticks for r in rows_from_queue_status(enriched)] == [2 * MINUTE, 3 * MINUTE]
    payload["rows"][0]["durationTicks"] = object()
    assert rows_from_queue_status(payload)[0].duration_ticks == 0


def test_live_queue_move_changes_view_and_delete_calls_host():
    nav = Navigator()
    nav.active = SessionId.FILES
    nav.apply_live_queue(_rows())
    nav.session.model.select_id("b")
    said = []
    moves = []
    removes = []
    refreshed = []
    client = SimpleNamespace(
        queue_move=lambda item_id, direction: (
            moves.append((item_id, direction)) or
            {"reorderStatus": "moved", "rows": [
                {"id": "b", "title": "Beta"}, {"id": "a", "title": "Alfa"}
            ]}
        ),
        queue_remove_membership=lambda ids: removes.append(ids) or {"changed": 1},
    )

    def submit(_slot, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        navigator=nav,
        announcer=SimpleNamespace(say=said.append),
        runner=SimpleNamespace(submit=submit),
        _selected_action_rows=lambda: [nav.session.model.selected_row],
        _profile_edit_client=lambda: client,
        _sync_views=lambda: None,
        _announce_after_native_list_update=said.append,
        _refresh_live_queue=lambda **kwargs: refreshed.append(kwargs),
    )
    gui.LiteFrame._move_queue_selection(frame, -1)
    assert moves == [("b", -1)]
    assert [r.item_id for r in nav.session.model.rows] == ["b", "a"]
    assert nav.session.model.selected_row.item_id == "b"
    assert "Przeniesiono Beta wyżej" in said

    gui.LiteFrame._remove_live_queue_items(frame)
    assert removes == [["b"]]
    assert refreshed == [{"preferred_id": "a"}]
    assert "Usunięto z kolejki: Beta" in said


def test_queue_shortcuts_resolve_to_edit_actions():
    assert resolve(Chord("Delete"), player_view=False, radio_session=False) is Action.REMOVE_SELECTED
    assert resolve(Chord("Up", alt=True), player_view=False, radio_session=False) is Action.MOVE_LIST_ITEM_UP
