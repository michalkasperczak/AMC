"""Ctrl+Shift+U zmienia Ulubione bez technicznych nazw w komunikatach."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import LibraryView, SessionId, View  # noqa: E402


def _frame(
    active: SessionId,
    rows: list[Row],
    *,
    favorite: bool,
    player: bool = False,
    library_view: LibraryView | None = None,
):
    model = ListModel()
    model.replace(rows)
    state = SimpleNamespace(
        model=model,
        view=View.PLAYER if player else View.LIST,
        library_view=library_view,
    )
    calls: list[tuple[str, list[str], list[str] | None]] = []
    spoken: list[str] = []
    refreshed: list[str | None] = []

    def toggle(session_id, item_ids, *, paths=None):
        calls.append((session_id, list(item_ids), paths))
        return {
            "favorite": favorite,
            "requested": len(item_ids),
            "changed": len(item_ids),
        }

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    def refresh(preferred_id=None, *, after_refresh=None):
        refreshed.append(preferred_id)
        if after_refresh is not None:
            after_refresh()

    return SimpleNamespace(
        navigator=SimpleNamespace(active=active, session=state),
        client=SimpleNamespace(toggle_profile_favorites=toggle),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: rows,
        _refresh_profile_view=refresh,
        calls=calls,
        spoken=spoken,
        refreshed=refreshed,
    )


def test_local_multiselection_sends_paths_without_replacing_the_current_folder() -> None:
    rows = [
        Row(item_id="local-1", title="Pierwszy plik", kind="track", path="D:/pierwszy.mp3"),
        Row(item_id="local-2", title="Drugi plik", kind="track", path="D:/drugi.mp3"),
    ]
    frame = _frame(SessionId.FILES, rows, favorite=True)

    gui.LiteFrame._toggle_profile_favorite(frame)

    assert frame.calls == [(
        "files",
        ["local-1", "local-2"],
        ["D:/pierwszy.mp3", "D:/drugi.mp3"],
    )]
    assert frame.refreshed == []
    assert frame.spoken == ["Dodano do ulubionych: 2 elementy"]


def test_removing_the_last_visible_favorite_announces_after_refresh() -> None:
    row = Row(
        item_id="local-1",
        title="Jedyny ulubiony",
        kind="track",
        path="D:/jedyny.mp3",
    )
    frame = _frame(
        SessionId.FILES,
        [row],
        favorite=False,
        library_view=LibraryView.FAVORITES,
    )

    gui.LiteFrame._toggle_profile_favorite(frame)

    assert frame.calls == [(
        "files",
        ["local-1"],
        ["D:/jedyny.mp3"],
    )]
    assert frame.refreshed == [None]
    assert frame.spoken == ["Usunięto z ulubionych: Jedyny ulubiony"]


def test_removal_focus_uses_next_then_previous_and_skips_load_more() -> None:
    first = Row("first", "Pierwszy", "track")
    middle = Row("middle", "Środkowy", "track")
    last = Row("last", "Ostatni", "track")
    load_more = Row("more", "Załaduj więcej", "loadMore")

    assert gui.LiteFrame._preferred_item_after_removal(
        [first, middle, last, load_more], {middle.item_id}
    ) == last.item_id
    assert gui.LiteFrame._preferred_item_after_removal(
        [first, middle, last, load_more], {last.item_id}
    ) == middle.item_id
    assert gui.LiteFrame._preferred_item_after_removal(
        [last, load_more], {last.item_id}
    ) is None


def test_radio_uses_station_names_and_does_not_leave_player() -> None:
    row = Row(item_id="station-private", title="Radio Poznań", kind="station")
    frame = _frame(SessionId.RADIO, [row], favorite=False, player=True)

    gui.LiteFrame._toggle_profile_favorite(frame)

    assert frame.calls == [("radio", ["station-private"], None)]
    assert frame.refreshed == []
    assert frame.spoken == ["Usunięto z ulubionych: Radio Poznań"]


def test_container_is_not_sent_as_an_item_identifier() -> None:
    row = Row(item_id="folder:private", title="Muzyka", kind="folder")
    frame = _frame(SessionId.FILES, [row], favorite=True)

    gui.LiteFrame._toggle_profile_favorite(frame)

    assert frame.calls == []
    assert frame.spoken == ["Wybierz plik"]
