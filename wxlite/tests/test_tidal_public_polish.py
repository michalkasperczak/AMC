"""Public feedback: browser handoff, language, quick reentry, playlist keyboard."""
import sys
import threading
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()
from amc_wx_lite import gui, i18n
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import SessionId, View
from amc_wx_lite.spotify_playlist_dialog import SpotifyPlaylistChoice, SpotifyPlaylistDialog
from amc_wx_lite.tidal_native_playback import TidalNativePlaybackAdapter
from amc_wx_lite.shortcuts import Action, ctrl_f5_presentation, describe_action
from amc_wx_lite.command_palette import entries_for_session


@pytest.fixture(autouse=True)
def polish(monkeypatch):
    monkeypatch.setattr(gui.wx, "MOD_NONE", 0, raising=False)
    previous = i18n.active_language()
    i18n.configure("pl")
    yield
    i18n.configure(previous)


def test_system_language_survives_wx_resetting_c_locale(monkeypatch):
    monkeypatch.setattr(i18n, "_windows_ui_locale", lambda: "pl_PL")
    monkeypatch.setattr(i18n.locale, "getlocale", lambda: (None, None))
    assert i18n.configure("system") == "pl"
    assert i18n.configure("en") == "en"
    assert i18n.configure("system", system_locale="en_US") == "en"


def test_system_language_has_non_windows_fallback(monkeypatch):
    monkeypatch.setattr(i18n, "_windows_ui_locale", lambda: None)
    monkeypatch.setattr(i18n.locale, "getlocale", lambda: ("Polish_Poland", None))
    assert i18n.configure("system") == "pl"


def test_device_page_opens_only_an_explicit_validated_link(monkeypatch):
    calls = []
    monkeypatch.setattr(gui.wx, "LaunchDefaultBrowser", lambda address: calls.append(address) or True,
                        raising=False)
    address = "https://link.tidal.com/ABC-123"
    assert gui.LiteFrame._open_tidal_native_login_page(object(), address, "ABC-123")
    assert calls == [address]


@pytest.mark.parametrize("address", ["https://evil.example/ABC-123", "http://link.tidal.com/ABC-123",
                                     "https://link.tidal.com/OTHER", "https://link.tidal.com/ABC-123#x"])
def test_device_page_rejects_unexpected_destinations(monkeypatch, address):
    monkeypatch.setattr(gui.wx, "LaunchDefaultBrowser",
                        lambda _: pytest.fail("Unexpected browser launch"), raising=False)
    assert not gui.LiteFrame._open_tidal_native_login_page(object(), address, "ABC-123")


def test_browser_launch_failure_is_reported_without_raw_exception(monkeypatch):
    def fail(_address):
        raise RuntimeError("private diagnostic")
    monkeypatch.setattr(gui.wx, "LaunchDefaultBrowser", fail, raising=False)
    assert not gui.LiteFrame._open_tidal_native_login_page(
        object(), "https://link.tidal.com/ABC-123", "ABC-123")


@pytest.mark.parametrize("browser_opened,expected_focus", [(True, []), (False, [True])])
def test_device_handoff_does_not_steal_browser_focus(browser_opened, expected_focus):
    focus = []
    closed = []
    dialog = SimpleNamespace(_tidal_browser_opened=browser_opened, IsBeingDeleted=lambda: False,
                             Hide=lambda: closed.append("hide"), Destroy=lambda: closed.append("destroy"))
    frame = SimpleNamespace(_tidal_native_challenge_dialog=dialog,
                             _restore_focus_after_dialog=lambda: focus.append(True))
    gui.LiteFrame._close_tidal_native_challenge(frame)
    assert closed == ["hide", "destroy"]
    assert frame._tidal_native_challenge_dialog is None
    assert focus == expected_focus


def dialog_fixture(selected=(0,)):
    dialog = object.__new__(SpotifyPlaylistDialog)
    dialog._choices = [SpotifyPlaylistChoice(str(i), name, name, selected=i in selected)
                       for i, name in enumerate(("Poranek", "Ulubione", "Wieczór"))]
    dialog._selection_touched = False
    selection = {"index": 1, "labels": [], "focus": False, "ended": None}
    dialog.playlist_list = SimpleNamespace(
        HasFocus=lambda: True, GetSelection=lambda: selection["index"],
        Set=lambda values: selection.update(labels=values),
        SetSelection=lambda index: selection.update(index=index),
        SetFocus=lambda: selection.update(focus=True))
    dialog.status = SimpleNamespace(SetLabel=lambda message: selection.update(status=message),
                                    GetLabel=lambda: selection.get("status", ""), SetName=lambda _: None)
    dialog.new_button = SimpleNamespace(SetFocus=lambda: selection.update(focus=True))
    dialog.EndModal = lambda result: selection.update(ended=result)
    return dialog, selection


def key(code):
    return SimpleNamespace(GetModifiers=lambda: gui.wx.MOD_NONE, GetKeyCode=lambda: code,
                           Skip=lambda: None)


@pytest.mark.parametrize("code", [gui.wx.WXK_RETURN, gui.wx.WXK_NUMPAD_ENTER])
def test_enter_adds_to_focused_name_instead_of_old_remembered_checkmarks(code):
    dialog, state = dialog_fixture()
    dialog._on_key(key(code))
    assert dialog.selected_playlist_ids == ["1"]
    assert state["ended"] == gui.wx.ID_OK
    assert state["labels"][1] == "Ulubione, zaznaczona"


def test_space_then_enter_preserves_explicit_multiple_destinations():
    dialog, state = dialog_fixture(selected=())
    dialog._on_key(key(gui.wx.WXK_SPACE))
    state["index"] = 2
    dialog._on_key(key(gui.wx.WXK_SPACE))
    dialog._on_key(key(gui.wx.WXK_RETURN))
    assert dialog.selected_playlist_ids == ["1", "2"]
    assert state["ended"] == gui.wx.ID_OK


def test_reopen_focuses_remembered_playlist_and_names_support_native_letter_search(monkeypatch):
    dialog, state = dialog_fixture(selected=(2,))
    dialog._rebuild("2")
    assert state["index"] == 2
    assert [label.split(",")[0] for label in state["labels"]] == ["Poranek", "Ulubione", "Wieczór"]
    monkeypatch.setattr(gui.wx, "CallAfter", lambda fn: fn(), raising=False)
    dialog._on_show(SimpleNamespace(Skip=lambda: None, IsShown=lambda: True))
    assert state["focus"]


def test_unchecking_everything_does_not_save_accidentally():
    dialog, state = dialog_fixture(selected=(1,))
    dialog._on_key(key(gui.wx.WXK_SPACE))
    dialog._on_key(key(gui.wx.WXK_RETURN))
    assert state["ended"] is None


def test_requested_track_covers_host_event_handoff_window():
    adapter = object.__new__(TidalNativePlaybackAdapter)
    adapter._lock = threading.Lock()
    adapter._closed = False
    adapter._pending = True
    adapter._pending_track_id = "123"
    adapter._track_id = None
    assert adapter.requested_track_id == "123"
    adapter._pending = False
    adapter._track_id = "123"
    assert adapter.requested_track_id == "123"
    adapter._closed = True
    assert adapter.requested_track_id is None


@pytest.mark.parametrize("already_started", [False, True])
def test_escape_enter_reuses_same_tidal_track_without_busy_or_second_start(already_started):
    spoken, refreshed = [], []
    state = SimpleNamespace(view=View.LIST)
    frame = SimpleNamespace(
        _tidal_native_adapter=SimpleNamespace(requested_track_id="123",
            play=lambda *_args, **_kwargs: pytest.fail("Duplicate TIDAL start")),
        _tidal_native_has_playback=already_started,
        _tidal_native_active_track_id="123" if already_started else None,
        client=object(), navigator=SimpleNamespace(sessions={SessionId.TIDAL: state}),
        announcer=SimpleNamespace(say=spoken.append),
        _refresh_menu_state=lambda: refreshed.append("menu"),
        _refresh_status=lambda: refreshed.append("status"), _sync_views=lambda: refreshed.append("views"))
    row = Row("row", "Track", "track", service_id="tracks:123")
    assert gui.LiteFrame._play_tidal_native_row(frame, row)
    assert not any("zajęty" in message for message in spoken)
    if already_started:
        assert state.view is View.PLAYER and refreshed == ["menu", "status", "views"]
    else:
        assert spoken == [i18n.tr("tidal.play.preparing")]


@pytest.mark.parametrize("profile", ["public", "test"])
def test_help_and_palette_keep_the_same_tidal_command(profile, monkeypatch):
    monkeypatch.setenv("AMC_RELEASE_PROFILE", profile)
    account = ctrl_f5_presentation("TIDAL")
    assert "osobnego zatwierdzenia" in account.description
    assert account.shortcut == "Ctrl+F5"
    playlist = describe_action(Action.MANAGE_PLAYLIST_MEMBERSHIP, session_label="TIDAL")
    assert all(word in playlist for word in ("Litery", "Enter", "Spacja", "zapamiętywany"))
    assert Action.MANAGE_PLAYLIST_MEMBERSHIP in {
        entry.action for entry in entries_for_session(SessionId.TIDAL)}
