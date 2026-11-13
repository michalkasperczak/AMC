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
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId, View  # noqa: E402
from amc_wx_lite.transport_parity import MessagePolicy  # noqa: E402


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
    client.queue_add_membership(items)

    assert [call[0] for call in client.calls] == [
        "queue.toggleMembership",
        "queue.togglePlayNext",
        "queue.addMembership",
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
    assert said == ["Odtwarzaj jako następne: Pierwszy"]


def test_queue_uses_private_template_and_empty_template_really_silences_it() -> None:
    row = Row("a", "Pierwszy", "track", path=r"C:\muzyka\A.mp3")
    client = SimpleNamespace(
        queue_toggle_membership=lambda _items: {"added": True, "changed": 1}
    )
    frame, said = _frame([row], client)
    frame.messages = MessagePolicy(templates={"queue.added": "Moja kolejka: {item}"})

    gui.LiteFrame._toggle_queue_membership(frame, play_next=False)
    assert said == ["Moja kolejka: Pierwszy"]

    said.clear()
    frame.messages = MessagePolicy(templates={"queue.added": ""})
    gui.LiteFrame._toggle_queue_membership(frame, play_next=False)
    assert said == []


def test_master_message_switch_keeps_status_without_speaking_membership() -> None:
    spoken: list[str] = []
    status: list[str] = []
    frame = SimpleNamespace(
        messages=MessagePolicy(enabled=False),
        announcer=SimpleNamespace(say=spoken.append, show=status.append),
    )

    gui.LiteFrame._announce_routine_text(frame, "Dodano do kolejki: Plik")

    assert spoken == []
    assert status == ["Dodano do kolejki: Plik"]


def test_tidal_queue_switches_twice_from_player_without_opening_list() -> None:
    row = Row(
        "track-1", "Utwór", "track", service_id="tidal-track-1",
        related_album_service_id="tidal-album-1",
    )
    spoken: list[str] = []
    opened: list[object] = []
    state = SimpleNamespace(view=View.PLAYER, library_view=LibraryView.TIDAL_QUEUE)
    frame: Any = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.TIDAL, sessions={SessionId.TIDAL: state}),
        _selected_action_rows=lambda: [row],
        _tidal_queue_rows=[],
        state=SimpleNamespace(tidal_queue_items=[]),
        store=SimpleNamespace(save=lambda _state: None),
        announcer=SimpleNamespace(say=spoken.append),
        _open_tidal_queue=lambda **kwargs: opened.append(kwargs),
    )

    gui.LiteFrame._toggle_tidal_queue(frame)
    gui.LiteFrame._toggle_tidal_queue(frame)

    assert spoken == ["Dodano do kolejki: Utwór", "Usunięto z kolejki: Utwór"]
    assert frame._tidal_queue_rows == []
    assert frame.state.tidal_queue_items == []
    assert opened == []


def test_tidal_queue_failed_save_keeps_previous_membership() -> None:
    row = Row(
        "track-1", "Utwór", "track", service_id="tidal-track-1",
        related_album_service_id="tidal-album-1",
    )
    spoken: list[str] = []

    def fail(_state):
        raise OSError("brak miejsca")

    frame: Any = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.TIDAL,
            sessions={SessionId.TIDAL: SimpleNamespace(
                view=View.LIST, library_view=LibraryView.TIDAL_QUEUE,
            )},
        ),
        _selected_action_rows=lambda: [row],
        _tidal_queue_rows=[row],
        state=SimpleNamespace(tidal_queue_items=[{"id": "old"}]),
        store=SimpleNamespace(save=fail),
        announcer=SimpleNamespace(say=spoken.append),
    )

    gui.LiteFrame._toggle_tidal_queue(frame)

    assert frame._tidal_queue_rows == [row]
    assert frame.state.tidal_queue_items == [{"id": "old"}]
    assert spoken == ["Nie zapisano kolejki TIDAL: brak miejsca"]


def test_spotify_queue_switches_twice_from_player_without_refreshing_list() -> None:
    row = Row(
        "track-1", "Utwór", "track", service_id="spotify-track-1",
        service_kind="track",
    )
    spoken: list[str] = []
    synced: list[str] = []
    state = SimpleNamespace(
        view=View.PLAYER, library_view=LibraryView.SPOTIFY_QUEUE,
        model=SimpleNamespace(replace=lambda *_args, **_kwargs: synced.append("replace")),
    )
    frame: Any = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SPOTIFY, sessions={SessionId.SPOTIFY: state}),
        _selected_action_rows=lambda: [row],
        _spotify_queue_rows=[],
        state=SimpleNamespace(spotify_queue_items=[]),
        store=SimpleNamespace(save=lambda _state: None),
        announcer=SimpleNamespace(say=spoken.append),
        _sync_views=lambda: synced.append("sync"),
    )

    gui.LiteFrame._toggle_spotify_queue(frame)
    gui.LiteFrame._toggle_spotify_queue(frame)

    assert spoken == ["Dodano do kolejki: Utwór", "Usunięto z kolejki: Utwór"]
    assert frame._spotify_queue_rows == []
    assert frame.state.spotify_queue_items == []
    assert synced == []


def test_spotify_queue_removal_keeps_the_next_visible_row_and_speaks_after_sync() -> None:
    rows = [
        Row(f"track-{index}", title, "track", service_id=f"spotify-{index}")
        for index, title in enumerate(("Pierwszy", "Drugi", "Trzeci"), 1)
    ]
    selected = rows[1]
    order: list[str] = []
    preferred: list[str | None] = []

    def replace(_rows, *, preferred_id=None):
        preferred.append(preferred_id)
        order.append("replace")

    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.SPOTIFY_QUEUE,
        model=SimpleNamespace(replace=replace),
    )
    frame: Any = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            sessions={SessionId.SPOTIFY: state},
        ),
        _selected_action_rows=lambda: [selected],
        _spotify_queue_rows=list(rows),
        state=SimpleNamespace(spotify_queue_items=gui.spotify_queue_payload(rows)),
        store=SimpleNamespace(save=lambda _state: None),
        announcer=SimpleNamespace(say=lambda text: order.append(f"say:{text}")),
        _sync_views=lambda: order.append("sync"),
        _announce_after_list_update=lambda text: order.append(f"defer:{text}"),
    )

    gui.LiteFrame._toggle_spotify_queue(frame)

    assert preferred == [rows[2].item_id]
    assert order == [
        "replace",
        "sync",
        "defer:Usunięto z kolejki: Drugi",
    ]


def test_tidal_queue_removal_keeps_the_next_visible_row_and_speaks_after_refresh() -> None:
    rows = [
        Row(
            f"track-{index}", title, "track",
            service_id=f"tidal-{index}",
            related_album_service_id="album-1",
        )
        for index, title in enumerate(("Pierwszy", "Drugi", "Trzeci"), 1)
    ]
    selected = rows[1]
    opened: list[str | None] = []
    deferred: list[str] = []
    state = SimpleNamespace(view=View.LIST, library_view=LibraryView.TIDAL_QUEUE)
    frame: Any = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.TIDAL,
            sessions={SessionId.TIDAL: state},
        ),
        _selected_action_rows=lambda: [selected],
        _tidal_queue_rows=list(rows),
        state=SimpleNamespace(tidal_queue_items=gui.tidal_queue_payload(rows)),
        store=SimpleNamespace(save=lambda _state: None),
        announcer=SimpleNamespace(say=lambda _text: None),
        _open_tidal_queue=lambda *, preferred_id=None: opened.append(preferred_id),
        _announce_after_list_update=deferred.append,
    )

    gui.LiteFrame._toggle_tidal_queue(frame)

    assert opened == [rows[2].item_id]
    assert deferred == ["Usunięto z kolejki: Drugi"]


def test_saved_youtube_video_queue_switches_twice_from_player() -> None:
    row = Row(
        "youtube-episode-1", "Film YouTube", "episode",
        url="https://www.youtube.com/watch?v=abc123",
    )
    queue: set[str] = set()
    spoken: list[str] = []
    opened: list[object] = []

    def toggle(ids, *, play_next=False):
        assert ids == [row.item_id]
        assert play_next is False
        added = row.item_id not in queue
        if added:
            queue.add(row.item_id)
        else:
            queue.remove(row.item_id)
        return {"added": added, "changed": 1}

    frame: Any = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: SimpleNamespace(
                view=View.PLAYER, library_view=LibraryView.PODCAST_QUEUE,
            )},
        ),
        _selected_action_rows=lambda: [row],
        _podcast_queue_pending=False,
        client=SimpleNamespace(toggle_podcast_queue=toggle),
        runner=SimpleNamespace(submit=lambda _slot, work, done, _failed: done(work())),
        announcer=SimpleNamespace(say=spoken.append),
        _open_podcast_aggregate=lambda *args, **kwargs: opened.append((args, kwargs)),
    )

    gui.LiteFrame._toggle_podcast_queue(frame, False)
    gui.LiteFrame._toggle_podcast_queue(frame, False)

    assert spoken == [
        "Dodano do kolejki: Film YouTube",
        "Usunięto z kolejki: Film YouTube",
    ]
    assert queue == set()
    assert opened == []
