"""WiiM respects the session's Enter and native preset player policies."""

from __future__ import annotations

from types import SimpleNamespace

import pytest

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Navigator, SessionId, View
from amc_wx_lite.session_options import (
    SessionPlaybackOverrides,
    capabilities_for,
    effective_overrides,
)
from amc_wx_lite.state_store import LiteState
from amc_wx_lite.wiim_source import WiiMPresetChoice


class ImmediateRunner:
    def submit(self, _name, work, done, _failed):
        done(work())


def _navigator() -> Navigator:
    nav = Navigator()
    nav.apply_wiim_devices([Row(
        "wiim:device-1", "Salon", "device",
        service_id="device-1", service_kind="device",
    )])
    nav.switch_session(SessionId.WIIM)
    nav.open_wiim_source_library(
        device_id="device-1", return_id="wiim:device-1",
    )
    return nav


def _frame(nav: Navigator, client: object, *, preset_open_player: bool = False):
    labels: list[str] = []
    synced: list[View] = []
    frame = SimpleNamespace(
        client=client,
        navigator=nav,
        now_playing=SimpleNamespace(SetLabel=labels.append),
        _set_transport_label=lambda **_kwargs: None,
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=labels.append),
        _wiim_active_device_id=None,
        _wiim_snapshot={},
        _sync_views=lambda: synced.append(nav.view),
        _apply_wiim_snapshot=lambda _state, **_kwargs: None,
        _session_open_player_on_preset=lambda session: preset_open_player,
    )
    return frame, synced


@pytest.mark.parametrize("open_player", [False, True])
def test_saved_stream_keeps_enter_view_after_device_reply(open_player: bool) -> None:
    nav = _navigator()
    nav.apply_wiim_network_streams([
        Row(
            "wiim-stream:1", "Radio Poznań", "wiimNetworkStream",
            parent_id="device-1", service_id="stream-1",
            service_kind="wiimNetworkStream",
        ),
    ], device_id="device-1", return_id="wiim:device-1")
    intent = nav.activate_selected(open_player_on_activation=open_player)[0]
    expected = View.PLAYER if open_player else View.LIST
    assert nav.view is expected
    frame, synced = _frame(nav, SimpleNamespace(
        wiim_play_network_stream=lambda *_args: {"deviceId": "device-1"},
    ))

    LiteFrame._activate_wiim_network_stream(frame, intent)

    assert nav.view is expected
    assert nav.session.now_playing_id == "wiim-stream:1"
    assert synced == [expected]


@pytest.mark.parametrize("open_player", [False, True])
def test_native_preset_uses_wiim_session_choice(open_player: bool) -> None:
    nav = _navigator()
    frame, synced = _frame(nav, SimpleNamespace(
        wiim_activate_preset=lambda *_args: {"deviceId": "device-1"},
    ), preset_open_player=open_player)
    choice = WiiMPresetChoice(
        2, "Radio Poznań", "radio internetowe",
        "Preset 2, Radio Poznań", True, False, "",
    )

    LiteFrame._activate_wiim_preset(
        frame, "wiim:device-1", "device-1", "Salon", choice,
    )

    expected = View.PLAYER if open_player else View.LIST
    assert nav.view is expected
    assert synced == [expected]


def test_wiim_session_persists_its_preset_choice() -> None:
    assert capabilities_for(SessionId.WIIM).supports_preset_entry_behavior
    state = LiteState()
    state.session_overrides["wiim"] = SessionPlaybackOverrides(
        open_player_on_preset=False,
    )
    assert effective_overrides(state, SessionId.WIIM).open_player_on_preset is False
