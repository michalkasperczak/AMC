"""Shift+F6 command, honest playback rows, and targeted Space."""

from __future__ import annotations

from types import SimpleNamespace

import pytest

from amc_wx_lite import command_palette, i18n, menu_model
from amc_wx_lite.active_playback import ActivePlaybackItem, host_item, remote_item
from amc_wx_lite.navigation import Navigator, SessionId, View
from amc_wx_lite.shortcuts import (
    Action, Chord, command_presentation, describe, resolve,
)


def test_active_playback_rows_require_live_owner_and_user_text() -> None:
    radio = host_item(
        SessionId.RADIO,
        {"sessionId": "radio", "id": "hidden-id", "title": "Radio 357",
         "playing": True, "paused": False},
        service="Radio internetowe", device="WiiM Sound",
    )
    assert radio is not None
    assert radio.label == "Radio 357, Radio internetowe, WiiM Sound, odtwarzanie"
    assert str(radio) == radio.label
    assert "hidden-id" not in radio.label
    assert host_item(
        SessionId.RADIO,
        {"sessionId": "files", "id": "hidden-id", "playing": True},
        service="Radio", device="Komputer",
    ) is None
    assert host_item(
        SessionId.RADIO,
        {"id": "old", "title": "Old", "playing": False, "paused": False},
        service="Radio", device="Komputer",
    ) is None
    paused = remote_item(
        SessionId.SONOS,
        {"title": "Album", "playbackState": "paused", "groupId": "hidden"},
        service="Sonos", device="Salon",
    )
    assert paused is not None and paused.label == "Album, Sonos, Salon, pauza"
    assert remote_item(
        SessionId.SONOS, {"title": "Album", "playbackState": "idle"},
        service="Sonos", device="Salon",
    ) is None


@pytest.mark.parametrize("player", [False, True])
@pytest.mark.parametrize("session", list(SessionId))
def test_shift_f6_command_has_same_menu_palette_help_and_execution(session, player):
    action = Action.SHOW_ACTIVE_PLAYBACK
    chord = Chord("F6", shift=True)
    presentation = command_presentation(action)
    assert resolve(
        chord, player_view=player, radio_session=session is SessionId.RADIO
    ) is action
    assert (presentation.shortcut, presentation.description) in describe()
    menu = next(item for group in menu_model.menus_for_session(session)
                for item in group.items if item.action is action)
    palette = next(item for item in command_palette.entries_for_session(session)
                   if item.action is action)
    assert menu.shortcut == palette.shortcut == presentation.shortcut
    assert menu.label == presentation.menu_label
    assert palette.name == presentation.name


def test_shift_f6_english_menu_palette_and_help() -> None:
    previous = i18n.active_language()
    i18n.configure("en")
    try:
        action = Action.SHOW_ACTIVE_PLAYBACK
        presentation = command_presentation(action)
        menu = next(item for group in menu_model.menus_for_session(SessionId.RADIO)
                    for item in group.items if item.action is action)
        palette = next(item for item in command_palette.entries_for_session(SessionId.RADIO)
                       if item.action is action)
        assert "Space" in presentation.description
        assert presentation.shortcut == menu.shortcut == palette.shortcut
        assert presentation.name == palette.name
        assert presentation.menu_label == menu.label
        assert (presentation.shortcut, presentation.description) in describe()
    finally:
        i18n.configure(previous)


def test_space_toggles_only_the_selected_host_session() -> None:
    from amc_wx_lite.gui import LiteFrame

    class Client:
        def __init__(self):
            self.calls = []
            self.paused = False

        def status(self, session_id):
            self.calls.append(("status", session_id))
            return {"sessionId": session_id, "id": "station", "title": "Radio 357",
                    "playing": True, "paused": self.paused}

        def pause_resume(self, session_id):
            self.calls.append(("toggle", session_id))
            self.paused = not self.paused
            return {"paused": self.paused}

    class Runner:
        def submit(self, _name, work, done, _failed):
            done(work())

    client = Client()
    frame = SimpleNamespace(
        client=client, runner=Runner(), _heos_service=None,
        _heos_state=SimpleNamespace(selected_target=None),
        _sonos_local_client=None, _wiim_active_device_id="",
        _sonos_active_household_id="", _sonos_active_group_id="",
        _frontier_smart_active_device_id="", _transport_status_by_session={},
    )
    item = ActivePlaybackItem(
        SessionId.RADIO, "Radio 357", "Radio internetowe", "Komputer",
        "odtwarzanie", "station",
    )
    results = []
    LiteFrame._toggle_active_playback_item(
        frame, item, lambda updated, message: results.append((updated, message)),
        wiim_id="", sonos_household="", sonos_group="", frontier_id="",
        heos_target=None,
    )
    assert client.calls == [
        ("status", "radio"), ("toggle", "radio"), ("status", "radio")
    ]
    assert results[0][0].state == "pauza"
    assert results[0][1] == "Radio internetowe, Komputer: pauza"


def test_enter_then_escape_keeps_the_selected_session() -> None:
    navigator = Navigator()
    navigator.sessions[SessionId.RADIO].now_playing_id = "station"
    navigator.sessions[SessionId.RADIO].now_playing_title = "Radio 357"
    navigator.switch_session(SessionId.RADIO)
    navigator.show_player()
    assert navigator.active is SessionId.RADIO
    assert navigator.session.view is View.PLAYER
    navigator.back_to_list()
    assert navigator.active is SessionId.RADIO
    assert navigator.session.view is View.LIST


def test_space_targets_the_selected_wiim_device() -> None:
    from amc_wx_lite.gui import LiteFrame

    class Client:
        def __init__(self):
            self.calls = []

        def wiim_transport(self, device_id, command):
            self.calls.append((device_id, command))
            return {"deviceId": device_id, "deviceTitle": "Salon",
                    "title": "Album", "playbackState": "wstrzymane"}

    class Runner:
        def submit(self, _name, work, done, _failed):
            done(work())

    client = Client()
    frame = SimpleNamespace(
        client=client, runner=Runner(), _heos_service=None,
        _heos_state=SimpleNamespace(selected_target=None),
        _sonos_local_client=None, _wiim_active_device_id="wiim-1",
        _sonos_active_household_id="", _sonos_active_group_id="",
        _frontier_smart_active_device_id="", _transport_status_by_session={},
    )
    item = ActivePlaybackItem(
        SessionId.WIIM, "Album", "WiiM", "Salon", "odtwarzanie"
    )
    results = []
    LiteFrame._toggle_active_playback_item(
        frame, item, lambda updated, message: results.append((updated, message)),
        wiim_id="wiim-1", sonos_household="", sonos_group="", frontier_id="",
        heos_target=None,
    )
    assert client.calls == [("wiim-1", "toggle")]
    assert results[0][0].state == "pauza"


@pytest.mark.parametrize("accept", [True, False])
@pytest.mark.parametrize("partial_error", [True, False])
def test_shift_f6_collects_radio_and_enter_opens_its_player(
    monkeypatch, accept, partial_error,
) -> None:
    from amc_wx_lite import gui

    class Client:
        def status(self, session_id):
            if session_id == "files" and partial_error:
                raise OSError("Host unavailable")
            if session_id == "radio":
                return {"sessionId": "radio", "id": "radio-357",
                        "title": "Radio 357", "playing": True, "paused": False}
            return {"sessionId": session_id, "playing": False, "paused": False}

        def audio_outputs(self, _device_id, _session_id):
            return {"devices": [{"id": None, "name": "Domyślne"}]}

        def cast_playback_context(self, _session_id):
            return None

    class Runner:
        def submit(self, _name, work, done, failed):
            try:
                done(work())
            except Exception as error:
                failed(error)

    navigator = Navigator()
    captured = []

    class Dialog:
        def __init__(self, _parent, items, _current, *, toggle,
                     status_message=""):
            captured.extend(items)
            captured.append(status_message)
            self.selected_item = items[0]
            self._closed = False

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return None

        def ShowModal(self):
            return gui.wx.ID_OK if accept else gui.wx.ID_CANCEL

    monkeypatch.setattr(gui, "ActivePlaybackDialog", Dialog)
    spoken = []
    focus_returns = []
    frame = SimpleNamespace(
        client=Client(), navigator=navigator,
        options=SimpleNamespace(session_order=(SessionId.FILES, SessionId.RADIO)),
        state=SimpleNamespace(audio_output_device_ids_by_session={}),
        _profile_audio_outputs={}, _wiim_active_device_id="",
        _sonos_active_household_id="", _sonos_active_group_id="",
        _frontier_smart_active_device_id="", _heos_service=None,
        _heos_state=SimpleNamespace(selected_target=None),
        _sonos_local_client=None, _tidal_native_has_playback=False,
        _youtube_playback_active=lambda: False, _youtube_status={},
        _youtube_active_item_id=None, _youtube_active_title="",
        _window_alive=lambda: True, runner=Runner(),
        announcer=SimpleNamespace(say=spoken.append),
        _switch_session=lambda session, **_kwargs: navigator.switch_session(session),
        _run=lambda _intents: None,
        _restore_focus_after_dialog=lambda: focus_returns.append(navigator.active),
    )
    gui.LiteFrame._show_active_playback(frame)
    assert [item.label for item in captured[:-1]] == [
        "Radio 357, Radio, Komputer, urządzenie domyślne Windows, odtwarzanie"
    ]
    assert bool(captured[-1]) is partial_error
    assert navigator.active is (SessionId.RADIO if accept else SessionId.FILES)
    assert navigator.session.view is (View.PLAYER if accept else View.LIST)
    assert focus_returns == [navigator.active]
    if accept:
        assert navigator.session.now_playing_title == "Radio 357"


def test_native_active_playback_list_selection_space_and_reopen() -> None:
    wx = pytest.importorskip("wx")
    from amc_wx_lite.gui import ActivePlaybackDialog

    app = wx.GetApp() or wx.App(False)
    parent = wx.Frame(None)
    rows = (
        ActivePlaybackItem(SessionId.RADIO, "Radio 357", "Radio", "Komputer",
                           "odtwarzanie"),
        ActivePlaybackItem(SessionId.WIIM, "Album", "WiiM", "Salon",
                           "odtwarzanie"),
    )
    toggled = []
    try:
        for _ in range(2):
            dialog = ActivePlaybackDialog(
                parent, rows, SessionId.WIIM,
                lambda item, completed: (
                    toggled.append(item.session),
                    completed(ActivePlaybackItem(
                        item.session, item.title, item.service, item.device, "pauza"
                    ), "WiiM, Salon: pauza"),
                ),
                status_message="Nie udało się sprawdzić: HEOS",
            )
            try:
                assert dialog.playback_list.GetName() == "Odtwarzane i wstrzymane materiały"
                assert dialog.status.GetLabel() == "Nie udało się sprawdzić: HEOS"
                assert dialog.playback_list.GetSelection() == 1
                assert dialog.selected_item.session is SessionId.WIIM
                dialog.playback_list.SetSelection(0)
                assert dialog.selected_item.session is SessionId.RADIO
                dialog.playback_list.SetSelection(1)
                event = SimpleNamespace(GetKeyCode=lambda: wx.WXK_SPACE)
                dialog._on_key(event)
                assert dialog.selected_item.state == "pauza"
                assert dialog.playback_list.GetString(1).endswith("pauza")
                dialog._closed = True
            finally:
                wx.YieldIfNeeded()
                dialog.Destroy()
        assert toggled == [SessionId.WIIM, SessionId.WIIM]
    finally:
        parent.Destroy()
