"""Grupowa zmiana żywej kolejki z wxPython.

Pilnujemy drogi GUI i jawnego protokołu. Kolejność oraz naturalne przejście
mierzą testy C# -- tutaj ważne jest wielokrotne zaznaczenie i brak queue.set.
"""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.host_client import LiteHostClient  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import Navigator  # noqa: E402


class _SpyClient(LiteHostClient):
    def __init__(self) -> None:
        self.calls: list[tuple[str, dict, float]] = []

    def call(self, op, args=None, *, timeout=20.0):
        self.calls.append((op, args or {}, timeout))
        return {"added": True, "changed": len((args or {}).get("items", []))}


def test_client_uses_two_explicit_incremental_operations() -> None:
    client = _SpyClient()
    items = [{"id": "a", "title": "A", "path": r"C:\muzyka\A.mp3"}]

    client.queue_toggle_membership(items)
    client.queue_toggle_play_next(items)

    assert [call[0] for call in client.calls] == [
        "queue.toggleMembership",
        "queue.togglePlayNext",
    ]
    assert all(call[1]["items"] == items for call in client.calls)
    assert all(call[1]["sessionId"] == "local" for call in client.calls)


def _frame(rows: list[Row], client: Any):
    said: list[str] = []
    frame: Any = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = Navigator()
    frame.navigator.session.model.replace(rows)
    frame.client = client
    frame.announcer = SimpleNamespace(say=said.append)
    frame.runner = SimpleNamespace(
        submit=lambda _slot, work, done, _failed: done(work())
    )
    frame._active_list = lambda: SimpleNamespace(selected_rows=lambda: rows)
    frame._note_queue_persistence = lambda _payload: None
    frame._refresh_live_queue = lambda: None
    return frame, said


def test_gui_sends_all_native_selected_rows_without_rebuilding_queue() -> None:
    captured: list[list[dict]] = []
    rows = [
        Row("a", "Pierwszy", "track", path=r"C:\muzyka\A.mp3"),
        Row("b", "Drugi", "track", path=r"C:\muzyka\B.mp3"),
    ]
    client = SimpleNamespace(
        queue_toggle_membership=lambda items: (
            captured.append(items)
            or {"added": True, "changed": 2}
        )
    )
    frame, said = _frame(rows, client)

    gui.LiteFrame._toggle_queue_membership(frame, play_next=False)

    assert [[item["id"] for item in batch] for batch in captured] == [["a", "b"]]
    assert any("2 elementy" in message for message in said)
    assert not hasattr(client, "queue_set"), "zmiana nie może wymagać queue.set"


def test_gui_play_next_uses_the_separate_operation() -> None:
    calls: list[list[dict]] = []
    row = Row("a", "Pierwszy", "track", path=r"C:\muzyka\A.mp3")
    client = SimpleNamespace(
        queue_toggle_play_next=lambda items: (
            calls.append(items)
            or {"added": True, "changed": 1}
        )
    )
    frame, said = _frame([row], client)

    gui.LiteFrame._toggle_queue_membership(frame, play_next=True)

    assert calls and calls[0][0]["id"] == "a"
    assert said == ["Ustawiono jako następne: Pierwszy"]
