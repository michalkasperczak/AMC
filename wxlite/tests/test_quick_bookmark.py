"""Szybka zakladka B i odrebna lista Ctrl+B."""

from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.menu_model import build_menus
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, Chord, resolve


def test_b_in_player_adds_bookmark() -> None:
    assert resolve(
        Chord("B"), player_view=True, radio_session=False
    ) is Action.ADD_BOOKMARK


def test_b_on_list_stays_native_letter_navigation() -> None:
    assert resolve(
        Chord("B"), player_view=False, radio_session=False
    ) is None


def test_ctrl_b_in_player_still_opens_all_bookmarks() -> None:
    assert resolve(
        Chord("B", ctrl=True), player_view=True, radio_session=False
    ) is Action.VIEW_ALL_BOOKMARKS


def test_ctrl_shift_b_adds_named_bookmark_only_in_player() -> None:
    assert resolve(
        Chord("B", ctrl=True, shift=True),
        player_view=False,
        radio_session=False,
    ) is None
    assert resolve(
        Chord("B", ctrl=True, shift=True),
        player_view=True,
        radio_session=False,
    ) is Action.ADD_NAMED_BOOKMARK


def test_shift_page_keys_navigate_bookmarks_without_stealing_clip_boundaries() -> None:
    assert resolve(
        Chord("Prior", shift=True), player_view=True, radio_session=False
    ) is Action.PREVIOUS_BOOKMARK
    assert resolve(
        Chord("Next", shift=True), player_view=True, radio_session=False
    ) is Action.NEXT_BOOKMARK
    assert resolve(
        Chord("Prior", alt=True), player_view=True, radio_session=False
    ) is Action.CLIP_PREVIOUS_BOUNDARY
    assert resolve(
        Chord("Next", alt=True), player_view=True, radio_session=False
    ) is Action.CLIP_NEXT_BOUNDARY


def test_playback_menu_exposes_bookmark_navigation_for_stable_timeline_sessions() -> None:
    entries = {
        item.action: item
        for menu in build_menus()
        for item in menu.items
        if item.action is not None
    }

    previous = entries[Action.PREVIOUS_BOOKMARK]
    following = entries[Action.NEXT_BOOKMARK]
    assert "Shift+Page Up" in previous.label.replace("&", "")
    assert "Shift+Page Down" in following.label.replace("&", "")
    assert previous.needs_playback and not previous.needs_files_session
    assert following.needs_playback and not following.needs_files_session
    stable = (
        SessionId.FILES, SessionId.PODCASTS, SessionId.SPOTIFY,
        SessionId.RADIOARCHIWISTA,
    )
    assert previous.visible_sessions == stable
    assert following.visible_sessions == stable
    named = entries[Action.ADD_NAMED_BOOKMARK]
    assert named.shortcut == "Ctrl+Shift+B"
    assert named.needs_playback and not named.needs_files_session
    assert named.visible_sessions == stable


def test_host_receives_library_database_without_queue_write() -> None:
    client = LiteHostClient(
        Path("host.exe"),
        library_db=Path(r"C:\Profil\library.db"),
    )
    command = client._command()
    assert "--library-db" in command
    assert r"C:\Profil\library.db" in command
    assert "--queue-write" not in command


def test_client_add_bookmark_uses_narrow_operation() -> None:
    calls: list[tuple] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, timeout=None: calls.append((op, args, timeout)) or {}

    client.add_bookmark(item_id="plik-1", item_title="Nagranie")

    assert calls == [(
        "bookmark.add",
        {"sessionId": "files", "itemId": "plik-1", "itemTitle": "Nagranie"},
        15.0,
    )]


def test_client_adds_named_bookmark_without_another_operation() -> None:
    calls: list[tuple] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, timeout=None: calls.append((op, args, timeout)) or {}

    client.add_bookmark(
        item_id="plik-1",
        item_title="Nagranie",
        name="Ważny fragment",
    )

    assert calls == [(
        "bookmark.add",
        {
            "sessionId": "files",
            "itemId": "plik-1",
            "itemTitle": "Nagranie",
            "name": "Ważny fragment",
        },
        15.0,
    )]


def test_client_removes_unique_bookmarks_with_a_narrow_operation() -> None:
    calls: list[tuple] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, timeout=None: calls.append((op, args, timeout)) or {}

    client.remove_bookmarks(["b-1", "b-1", "b-2"])

    assert calls == [(
        "bookmark.remove",
        {"itemIds": ["b-1", "b-2"]},
        20.0,
    )]


def test_client_navigates_bookmarks_in_the_local_session() -> None:
    calls: list[tuple] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, timeout=None: calls.append((op, args, timeout)) or {}

    client.navigate_bookmark(-10)
    client.navigate_bookmark(0)

    assert calls == [
        (
            "bookmark.navigate",
                {"sessionId": "files", "direction": -1, "namedOnly": False},
            15.0,
        ),
        (
            "bookmark.navigate",
                {"sessionId": "files", "direction": 1, "namedOnly": False},
            15.0,
        ),
    ]
