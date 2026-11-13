"""Apple Music handoff uses only public links and Apple's app alias."""

from __future__ import annotations

import pytest

from amc_wx_lite.apple_music_original import (
    AppleMusicOriginalUnavailable,
    open_in_original,
    track_url,
)
from amc_wx_lite.apple_music_source import records_from_resources
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import (
    Action,
    APPLE_MUSIC_SUPPORTED_ACTIONS,
    Chord,
    command_presentation,
    describe,
    keyboard_help_announcement,
)


def _song(*, catalog_id: str = "123456", url: str | None = None):
    attributes: dict[str, object] = {
        "name": "Utwór",
        "artistName": "Wykonawca",
        "playParams": {"catalogId": catalog_id},
    }
    if url is not None:
        attributes["url"] = url
    return records_from_resources([{
        "id": "i.private-song",
        "type": "library-songs",
        "attributes": attributes,
    }])[0]


def test_library_catalog_id_builds_public_song_link() -> None:
    record = _song()
    assert record.row.url is None
    assert track_url(record) == "https://music.apple.com/pl/song/123456"


def test_apple_provided_public_link_is_preferred() -> None:
    address = "https://music.apple.com/pl/song/przyklad/123456"
    assert track_url(_song(url=address)) == address


def test_non_numeric_identifier_without_public_link_cannot_launch() -> None:
    assert track_url(_song(catalog_id="not-a-catalog-id")) is None


def test_launcher_uses_app_alias_and_never_browser_default() -> None:
    calls: list[tuple[list[str], bool]] = []

    def start(arguments: list[str], *, close_fds: bool) -> None:
        calls.append((arguments, close_fds))

    open_in_original(
        _song(),
        find_executable=lambda name: r"C:\WindowsApps\AppleMusic.exe",
        start=start,
    )
    assert calls == [([
        r"C:\WindowsApps\AppleMusic.exe",
        "/url",
        "https://music.apple.com/pl/song/123456",
    ], True)]


def test_missing_app_does_not_open_a_browser() -> None:
    with pytest.raises(AppleMusicOriginalUnavailable):
        open_in_original(_song(), find_executable=lambda name: None)


def test_command_is_scoped_to_apple_music_menu_and_capability() -> None:
    assert Action.APPLE_MUSIC_OPEN_ORIGINAL in APPLE_MUSIC_SUPPORTED_ACTIONS
    apple = menus_for_session(SessionId.APPLE_MUSIC)
    tidal = menus_for_session(SessionId.TIDAL)
    assert any(
        item.action is Action.APPLE_MUSIC_OPEN_ORIGINAL
        for menu in apple for item in menu.items
    )
    assert not any(
        item.action is Action.APPLE_MUSIC_OPEN_ORIGINAL
        for menu in tidal for item in menu.items
    )
    presentation = command_presentation(Action.APPLE_MUSIC_OPEN_ORIGINAL)
    assert presentation is not None
    assert presentation.shortcut == ""
    assert presentation.name == "Otwórz utwór w oryginalnym Apple Music"
    assert any(
        entry.action is Action.APPLE_MUSIC_OPEN_ORIGINAL
        and entry.name == presentation.name
        for entry in entries_for_session(SessionId.APPLE_MUSIC)
    )
    assert ("Bez skrótu", presentation.description) in describe(
        "Apple Music", {Action.APPLE_MUSIC_OPEN_ORIGINAL}
    )
    enter_help = dict(describe("Apple Music", {Action.ACTIVATE}))["Enter"]
    assert "oryginalnej aplikacji" in enter_help
    assert enter_help in keyboard_help_announcement(
        Chord("Return"), Action.ACTIVATE, "Apple Music"
    )
