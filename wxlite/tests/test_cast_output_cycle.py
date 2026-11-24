"""Primary Cast target cycling uses the same choices as Shift+C."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.cast_protocol import CastDevice
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame, chord_from_event
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.network_output_cycle import adjacent_target_id
from amc_wx_lite.shortcuts import Action, Chord, describe, resolve


class Client:
    def __init__(self, devices=()):
        self.devices = devices
        self.selected = None
        self.calls = []

    def cast_devices(self):
        return self.devices

    def cast_selected_device_id(self, _session):
        return self.selected

    def select_cast(self, session, target_id):
        self.calls.append((session, target_id))
        self.selected = target_id
        device = next((item for item in self.devices if item.device_id == target_id), None)
        return {"name": device.name if device else "Ten komputer"}


def frame_for(client, *, deferred=False):
    spoken, queued, refreshed = [], [], []

    def submit(_name, work, done, failed):
        if deferred:
            queued.append((work, done, failed))
            return
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        client=client,
        navigator=SimpleNamespace(active=SessionId.RADIO),
        announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(submit=submit),
        _action_available_in_active_session=lambda _action: True,
        _window_alive=lambda: True,
        _refresh_status=lambda: refreshed.append("status"),
        _refresh_menu_state=lambda: refreshed.append("menu"),
    )
    return frame, spoken, queued, refreshed


def test_cycle_wraps_between_local_and_available_cast_receivers():
    assert adjacent_target_id(["a", "b"], None, 1) == "a"
    assert adjacent_target_id(["a", "b"], "a", -1) is None
    assert adjacent_target_id(["a", "b"], "b", 1) is None
    assert adjacent_target_id(["a", "b"], "missing", -1) == "b"
    assert adjacent_target_id([], None, 1) is None

    client = Client((
        CastDevice("private-a", "WiiM Sound", "192.0.2.1"),
        CastDevice("private-b", "Niedostępny", "192.0.2.2", available=False),
    ))
    frame, spoken, _queued, refreshed = frame_for(client)
    LiteFrame._cycle_cast_output(frame, 1)
    assert client.calls == [("radio", "private-a")]
    assert spoken == ["Wyjście bieżącej sesji: WiiM Sound"]
    LiteFrame._cycle_cast_output(frame, 1)
    assert client.calls[-1] == ("radio", None)
    assert spoken[-1] == "Wyjście bieżącej sesji: Ten komputer"
    assert refreshed == ["status", "menu", "status", "menu"]
    assert frame._network_output_cycle_pending is False


def test_cycle_says_when_no_other_target_and_never_sends_selection():
    client = Client()
    frame, spoken, _queued, _refreshed = frame_for(client)
    LiteFrame._cycle_cast_output(frame, 1)
    assert spoken == ["Brak innego dostępnego wyjścia Google Cast"]
    assert client.calls == []
    assert frame._network_output_cycle_pending is False


def test_stale_discovery_after_session_change_does_not_switch_output():
    client = Client((CastDevice("private-a", "WiiM Sound", "192.0.2.1"),))
    frame, _spoken, queued, _refreshed = frame_for(client, deferred=True)
    LiteFrame._cycle_cast_output(frame, 1)
    frame.navigator.active = SessionId.PODCASTS
    work, done, _failed = queued.pop()
    done(work())
    assert client.calls == []
    assert frame._network_output_cycle_pending is False


def test_rejected_switch_keeps_previous_target_and_releases_busy_state():
    class RejectingClient(Client):
        def select_cast(self, _session, _target_id):
            raise ValueError("Odbiornik jest niedostępny")

    client = RejectingClient((CastDevice("private-a", "WiiM Sound", "192.0.2.1"),))
    frame, spoken, _queued, _refreshed = frame_for(client)
    LiteFrame._cycle_cast_output(frame, 1)
    assert client.selected is None
    assert frame._network_output_cycle_pending is False
    assert spoken == [
        "Nie udało się przełączyć wyjścia Google Cast: Odbiornik jest niedostępny"
    ]


def test_shortcuts_menu_palette_and_help_share_cast_cycle_actions():
    expected = {
        "Ctrl+Shift+[": Action.PREVIOUS_CAST_OUTPUT,
        "Ctrl+Shift+]": Action.NEXT_CAST_OUTPUT,
    }
    for session, flags in (
        (SessionId.FILES, {}),
        (SessionId.RADIO, {}),
        (SessionId.PODCASTS, {}),
        (SessionId.TIDAL, {"tidal_session": True}),
        (SessionId.RADIOARCHIWISTA, {"radioarchiwista_session": True}),
    ):
        menus = menus_for_session(session)
        menu_items = {item.action: item for menu in menus for item in menu.items}
        palette = {entry.action: entry for entry in entries_for_session(session)}
        help_entries = dict(describe(session.value, set(menu_items)))
        for shortcut, action in expected.items():
            assert action in menu_items and action in palette
            assert menu_items[action].shortcut == shortcut
            assert palette[action].shortcut == shortcut
            assert shortcut in help_entries
            for player_view in (False, True):
                chord = Chord(shortcut.split("+")[-1], ctrl=True, shift=True)
                assert resolve(
                    chord, player_view=player_view,
                    radio_session=session is SessionId.RADIO, **flags
                ) is action
    assert resolve(
        Chord("[", ctrl=True, shift=True), player_view=False,
        radio_session=False, wiim_session=True,
    ) is None
    for session in (
        SessionId.WIIM, SessionId.SONOS, SessionId.HEOS,
        SessionId.SPOTIFY, SessionId.APPLE_MUSIC, SessionId.FRONTIER_SMART,
    ):
        assert Action.PREVIOUS_CAST_OUTPUT not in {
            item.action for menu in menus_for_session(session) for item in menu.items
        }


def test_windows_shifted_bracket_event_keeps_physical_key():
    class Event:
        def __init__(self, code, raw, alt=False):
            self.code, self.raw, self.alt = code, raw, alt
        def GetKeyCode(self): return self.code
        def GetRawKeyCode(self): return self.raw
        def ControlDown(self): return True
        def ShiftDown(self): return True
        def AltDown(self): return self.alt

    assert chord_from_event(Event(ord("{"), 0xDB)).canonical == "Ctrl+Shift+["
    assert chord_from_event(Event(ord("}"), 0xDD)).canonical == "Ctrl+Shift+]"
    assert resolve(
        chord_from_event(Event(ord("{"), 0xDB, alt=True)),
        player_view=False, radio_session=True,
    ) is None
