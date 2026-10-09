"""Szybka zakladka B i odrebna lista Ctrl+B."""

from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient
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
        {"itemId": "plik-1", "itemTitle": "Nagranie"},
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
