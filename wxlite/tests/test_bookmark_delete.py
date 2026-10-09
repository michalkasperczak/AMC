"""Delete w obu widokach zakładek usuwa wpis, a nie plik źródłowy."""

from __future__ import annotations

import sys
from pathlib import Path
from types import MethodType, SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import LibraryView, SessionId  # noqa: E402


def _frame(view: LibraryView, selected: list[Row]):
    rows = [
        Row(item_id="bookmark:b-1", title="Pierwsza, 0:10, zakładka", kind="track"),
        Row(item_id="bookmark:b-2", title="Druga, 0:20, zakładka", kind="track"),
        Row(item_id="bookmark:b-3", title="Trzecia, 0:30, zakładka", kind="track"),
    ]
    model = ListModel()
    model.replace(rows)
    model.select_id(selected[0].item_id)
    state = SimpleNamespace(library_view=view, model=model)
    calls: list[list[str]] = []
    spoken: list[str] = []
    refreshed: list[str | None] = []

    class Client:
        def remove_bookmarks(self, bookmark_ids):
            calls.append(list(bookmark_ids))
            return {"requestedCount": len(bookmark_ids), "removedCount": len(bookmark_ids)}

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    client = Client()
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.FILES,
            session=state,
        ),
        _selected_action_rows=lambda: selected,
        _profile_edit_client=lambda: client,
        _refresh_profile_view=lambda preferred_id=None: refreshed.append(preferred_id),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
    )
    frame._remove_selected_bookmarks = MethodType(
        gui.LiteFrame._remove_selected_bookmarks, frame
    )
    return frame, calls, spoken, refreshed


def test_delete_removes_all_non_adjacent_bookmarks_and_keeps_next_focus() -> None:
    selected = [
        Row(item_id="bookmark:b-1", title="Pierwsza, 0:10, zakładka", kind="track"),
        Row(item_id="bookmark:b-3", title="Trzecia, 0:30, zakładka", kind="track"),
    ]
    frame, calls, spoken, refreshed = _frame(LibraryView.ALL_BOOKMARKS, selected)

    gui.LiteFrame._remove_selected_items(frame)

    assert calls == [["b-1", "b-3"]], "do hosta idą ID zakładek bez prefiksu wiersza"
    assert refreshed == ["bookmark:b-2"], "fokus przechodzi na najbliższy pozostały wpis"
    assert spoken == ["Usunięto z Zakładek: 2 elementy"]


def test_delete_from_item_bookmarks_never_calls_generic_file_removal() -> None:
    selected = [
        Row(item_id="bookmark:b-2", title="Druga, 0:20, zakładka", kind="track")
    ]
    frame, calls, spoken, refreshed = _frame(LibraryView.ITEM_BOOKMARKS, selected)

    gui.LiteFrame._remove_selected_items(frame)

    assert calls == [["b-2"]]
    assert refreshed == ["bookmark:b-3"]
    assert spoken == ["Usunięto z Zakładek: Druga, 0:20, zakładka"]
