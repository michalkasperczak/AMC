from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.menu_model import build_menus
from amc_wx_lite.navigation import ActivateSonosGroup, Navigator, SessionId, View
from amc_wx_lite.shortcuts import Action, Chord, SONOS_SUPPORTED_ACTIONS, resolve
from amc_wx_lite.sonos_source import now_playing_label, snapshot, target_rows


def test_ctrl_6_and_menu_open_the_sonos_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("6", ctrl=True),
            player_view=player,
            radio_session=False,
        ) is Action.SESSION_SONOS
    view = next(menu for menu in build_menus() if menu.title == "&Widok")
    item = next(entry for entry in view.items if entry.action is Action.SESSION_SONOS)
    assert item.label == "Sesja: &Sonos"
    assert item.shortcut == "Ctrl+6"


def test_sonos_activation_separates_labels_from_internal_ids() -> None:
    nav = Navigator()
    rows = target_rows({
        "items": [{
            "itemId": "sonos:group-1",
            "householdId": "house-technical",
            "groupId": "group-technical",
            "title": "Salon",
            "detail": "Dwa głośniki",
        }]
    })
    nav.apply_sonos_targets(list(rows))
    nav.switch_session(SessionId.SONOS)

    intents = nav.activate_selected()

    assert len(intents) == 1
    intent = intents[0]
    assert isinstance(intent, ActivateSonosGroup)
    assert intent.title == "Salon"
    assert intent.household_id == "house-technical"
    assert intent.group_id == "group-technical"
    row = nav.sessions[SessionId.SONOS].model.selected_row
    assert row is not None
    assert row.title == "Salon"
    assert row.detail == "Dwa głośniki"
    assert "technical" not in row.title
    assert "technical" not in row.detail
    assert nav.view is View.PLAYER


def test_sonos_snapshot_allowlists_accessible_text() -> None:
    state = snapshot({
        "itemId": "sonos:group-1",
        "householdId": "house-1",
        "groupId": "group-1",
        "groupTitle": "Salon",
        "title": "Audycja",
        "source": "Radio Poznań",
        "stateText": "odtwarzanie",
        "volumeText": "głośność 35%",
        "positionText": "1:01 z 10:00",
        "playbackState": "playing",
        "volume": 35,
        "muted": False,
        "fixedVolume": False,
        "positionSeconds": 61,
        "durationSeconds": 600,
        "message": "Audycja, odtwarzanie",
        "token": "secret",
        "runtimeObject": "SonosGroup { Id = group-1 }",
    })

    assert now_playing_label(state) == "Audycja, Radio Poznań"
    assert "token" not in state
    assert "runtimeObject" not in state
    assert "secret" not in repr(state)


def test_sonos_command_gate_blocks_local_only_features() -> None:
    assert {
        Action.PLAY_PAUSE,
        Action.QUEUE_PREVIOUS,
        Action.QUEUE_NEXT,
        Action.SEEK_FORWARD_10,
        Action.SEEK_PERCENT_50,
        Action.VOLUME_UP_5,
        Action.TIME_ELAPSED,
        Action.SELECT_AUDIO_OUTPUT,
        Action.COPY_NAME,
    } <= SONOS_SUPPORTED_ACTIONS
    assert {
        Action.ADD_BOOKMARK,
        Action.RATE_UP,
        Action.RECORD_TOGGLE,
        Action.ADD_TO_QUEUE,
    }.isdisjoint(SONOS_SUPPORTED_ACTIONS)


def test_sonos_host_client_sends_only_ids_command_and_arguments() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.sonos_targets()
    client.sonos_snapshot("house-1", "group-1")
    client.sonos_transport(
        "house-1", "group-1", "seekRelative", seconds=30
    )

    assert calls == [
        ("sonos.targets", None, 60.0),
        (
            "sonos.snapshot",
            {"householdId": "house-1", "groupId": "group-1"},
            45.0,
        ),
        (
            "sonos.transport",
            {
                "householdId": "house-1",
                "groupId": "group-1",
                "command": "seekRelative",
                "seconds": 30.0,
            },
            45.0,
        ),
    ]


def test_sonos_dispatch_reaches_remote_volume_and_blocks_local_rate() -> None:
    spoken: list[str] = []
    volume_steps: list[int] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SONOS),
        announcer=SimpleNamespace(say=spoken.append),
        messages=SimpleNamespace(custom_seek_seconds=300),
        _adjust_volume=volume_steps.append,
    )

    LiteFrame._dispatch(frame, Action.VOLUME_UP_5)
    assert volume_steps == [5]
    assert spoken == []

    LiteFrame._dispatch(frame, Action.RATE_UP)
    assert volume_steps == [5]
    assert spoken == ["To polecenie nie jest dostępne w sesji Sonos"]
