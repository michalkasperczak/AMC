"""Spacja steruje wybranym celem bieżącej sesji głośnikowej."""

from types import SimpleNamespace

import pytest

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, Chord, resolve


@pytest.mark.parametrize("session", (
    SessionId.WIIM, SessionId.SONOS, SessionId.FRONTIER_SMART,
))
def test_space_in_both_views_and_command_surfaces(session: SessionId) -> None:
    for player in (False, True):
        assert resolve(
            Chord("Space"), player_view=player, radio_session=False,
            wiim_session=session is SessionId.WIIM,
            sonos_session=session is SessionId.SONOS,
            frontier_smart_session=session is SessionId.FRONTIER_SMART,
        ) is Action.PLAY_PAUSE
    entries = [
        item for menu in menus_for_session(session)
        for item in menu.items if item.action is Action.PLAY_PAUSE
    ]
    assert len(entries) == 1
    assert entries[0].shortcut == "Space"


@pytest.mark.parametrize("session, expected", (
    (SessionId.WIIM, "wiim"),
    (SessionId.SONOS, "sonos"),
    (SessionId.FRONTIER_SMART, "frontier"),
))
def test_play_pause_routes_only_to_current_remote_session(
    session: SessionId, expected: str,
) -> None:
    calls: list[tuple[str, str]] = []
    frame = SimpleNamespace(
        client=object(), navigator=SimpleNamespace(active=session),
        _wiim_transport=lambda command: calls.append(("wiim", command)),
        _sonos_transport=lambda command: calls.append(("sonos", command)),
        _frontier_smart_transport=lambda command: calls.append(("frontier", command)),
    )
    LiteFrame._play_pause(frame)
    assert calls == [(expected, "toggle")]
