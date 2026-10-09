from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.menu_model import build_menus
from amc_wx_lite.navigation import (
    ActivateWiiMDevice,
    Navigator,
    SessionId,
    View,
)
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    WIIM_SUPPORTED_ACTIONS,
    resolve,
)
from amc_wx_lite.wiim_source import device_rows, now_playing_label, snapshot


def test_ctrl_5_and_menu_open_the_wiim_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("5", ctrl=True),
            player_view=player,
            radio_session=False,
        ) is Action.SESSION_WIIM
    view = next(menu for menu in build_menus() if menu.title == "&Widok")
    item = next(entry for entry in view.items if entry.action is Action.SESSION_WIIM)
    assert item.label == "Sesja: &WiiM"
    assert item.shortcut == "Ctrl+5"


def test_wiim_device_activation_separates_user_label_from_internal_id() -> None:
    nav = Navigator()
    rows = device_rows({
        "items": [{
            "itemId": "wiim:device-1",
            "deviceId": "device-1",
            "title": "Salon",
            "detail": "WiiM Pro",
            "selected": True,
            "address": "192.168.1.40",
        }]
    })
    nav.apply_wiim_devices(list(rows))
    nav.switch_session(SessionId.WIIM)

    intents = nav.activate_selected()

    assert len(intents) == 1
    intent = intents[0]
    assert isinstance(intent, ActivateWiiMDevice)
    assert intent.title == "Salon"
    assert intent.device_id == "device-1"
    assert "192.168.1.40" not in repr(nav.sessions[SessionId.WIIM].model.rows)
    assert nav.view is View.PLAYER


def test_wiim_payload_is_allowlisted_for_accessible_text() -> None:
    state = snapshot({
        "deviceId": "device-1",
        "deviceTitle": "Salon",
        "title": "Audycja",
        "artist": "Radio Poznań",
        "playbackState": "odtwarzanie",
        "volume": 35,
        "muted": False,
        "positionSeconds": 61,
        "durationSeconds": 600,
        "message": "Audycja, odtwarzanie, głośność 35%",
        "address": "192.168.1.40",
        "runtimeObject": "WiiMDeviceSettings { Address = 192.168.1.40 }",
    })

    assert now_playing_label(state) == "Audycja, Radio Poznań"
    assert "address" not in state
    assert "runtimeObject" not in state
    assert "192.168.1.40" not in repr(state)


def test_wiim_command_gate_blocks_local_audio_features() -> None:
    assert {
        Action.PLAY_PAUSE,
        Action.QUEUE_PREVIOUS,
        Action.QUEUE_NEXT,
        Action.VOLUME_UP_5,
        Action.VOLUME_DOWN_1,
        Action.TIME_ELAPSED,
        Action.SELECT_AUDIO_OUTPUT,
        Action.COPY_NAME,
        Action.FOCUS_FILTER,
        Action.VIEW_ACTIVE_RECORDINGS,
        Action.MANAGE_RADIO_SCHEDULES,
    } <= WIIM_SUPPORTED_ACTIONS
    assert {
        Action.ADD_BOOKMARK,
        Action.RATE_UP,
        Action.SEEK_FORWARD_10,
        Action.RECORD_TOGGLE,
        Action.ADD_TO_QUEUE,
    }.isdisjoint(WIIM_SUPPORTED_ACTIONS)


def test_wiim_host_client_sends_only_device_id_command_and_volume() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.wiim_devices()
    client.wiim_snapshot("device-1")
    client.wiim_transport("device-1", "setVolume", volume=37)

    assert calls == [
        ("wiim.devices", None, 15.0),
        ("wiim.snapshot", {"deviceId": "device-1"}, 30.0),
        (
            "wiim.transport",
            {"deviceId": "device-1", "command": "setVolume", "volume": 37},
            30.0,
        ),
    ]
    assert "address" not in repr(calls).casefold()


def test_wiim_dispatch_reaches_remote_volume_and_blocks_local_rate() -> None:
    spoken: list[str] = []
    volume_steps: list[int] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.WIIM),
        announcer=SimpleNamespace(say=spoken.append),
        messages=SimpleNamespace(custom_seek_seconds=300),
        _adjust_volume=volume_steps.append,
    )

    LiteFrame._dispatch(frame, Action.VOLUME_UP_5)
    assert volume_steps == [5]
    assert spoken == []

    LiteFrame._dispatch(frame, Action.RATE_UP)
    assert volume_steps == [5]
    assert spoken == ["To polecenie nie jest dostępne w sesji WiiM"]
