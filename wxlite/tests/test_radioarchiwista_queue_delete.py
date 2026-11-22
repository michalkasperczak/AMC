"""Selection after Delete in the Radio Archiwista queue."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId


def _delete(
    selected: str, query: str = "", move_during_write: str | None = None,
) -> tuple[str | None, list[str]]:
    rows = [
        Row("a", "Nagranie A", "recording", url="https://example.test/a.mp3"),
        Row("b", "Nagranie B", "recording", url="https://example.test/b.mp3"),
        Row("c", "Inne C", "recording", url="https://example.test/c.mp3"),
        Row("d", "Nagranie D", "recording", url="https://example.test/d.mp3"),
    ]
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    navigator.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_QUEUE, rows)
    navigator.session.model.select_id(selected)
    remaining = list(rows)
    spoken: list[str] = []

    def toggle(recording, *, play_next, current_id):
        assert recording == selected and not play_next
        remaining[:] = [row for row in remaining if row.item_id != selected]
        return False

    source = SimpleNamespace(
        recording_by_id=lambda item_id: item_id,
        toggle_queue=toggle,
        queue_rows=lambda: list(remaining),
    )

    def submit(_slot, work, done, failed):
        try:
            result = work()
            if move_during_write:
                navigator.session.model.select_id(move_during_write)
            done(result)
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        navigator=navigator,
        announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(submit=submit),
        filter_box=SimpleNamespace(GetValue=lambda: query),
        _radioarchiwista_source=lambda: source,
        _run=lambda effects: None,
    )
    gui.LiteFrame._toggle_radioarchiwista_queue(frame, play_next=False)
    return navigator.session.model.selected_id, spoken


def test_delete_middle_selects_next_visible_recording() -> None:
    selected, spoken = _delete("b")
    assert selected == "c"
    assert spoken == ["Usunięto z kolejki: Nagranie B"]


def test_delete_uses_filtered_neighbor() -> None:
    selected, spoken = _delete("b", "Nagranie")
    assert selected == "d"
    assert spoken == ["Usunięto z kolejki: Nagranie B"]


def test_delete_last_selects_previous_recording() -> None:
    selected, spoken = _delete("d")
    assert selected == "c"
    assert spoken == ["Usunięto z kolejki: Nagranie D"]


def test_delete_preserves_newer_selection_during_write() -> None:
    selected, spoken = _delete("b", move_during_write="d")
    assert selected == "d"
    assert spoken == ["Usunięto z kolejki: Nagranie B"]
