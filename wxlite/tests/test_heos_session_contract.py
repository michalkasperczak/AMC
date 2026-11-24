"""HEOS commands agree across keys, menus, palette, help and dispatch."""

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite import gui
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.heos_client import HeosListUnavailable
from amc_wx_lite.heos_navigation import HeosBrowseState, HeosLocation
from amc_wx_lite.heos_source import HeosItem
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.shortcuts import Action, Chord, describe, resolve


def test_heos_is_test_only_and_session_command_has_a_user_label():
    assert "heos" in TEST_PROFILE.session_ids
    assert "session.heos" in TEST_PROFILE.capabilities
    assert "heos" not in PUBLIC_PROFILE.session_ids
    assert "session.heos" not in PUBLIC_PROFILE.capabilities
    order = ["files", "radio", "heos"]
    menu = menus_for_session(SessionId.HEOS, order)
    actions = {item.action for section in menu for item in section.items}
    assert Action.SESSION_HEOS in actions
    palette = {entry.action: entry for entry in entries_for_session(SessionId.HEOS, order)}
    assert palette[Action.SESSION_HEOS].name == "Sesja: HEOS"


def test_heos_shortcuts_match_visible_menu_palette_help_and_dispatch():
    cases = {
        "Ctrl+F5": Action.SESSION_CONFIGURE_CURRENT,
        "Ctrl+L": Action.VIEW_LIBRARY,
        "Ctrl+U": Action.VIEW_FAVORITES,
        "Ctrl+P": Action.VIEW_PLAYLISTS,
        "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
        "Ctrl+Shift+Q": Action.ADD_TO_QUEUE,
        "Ctrl+Shift+Return": Action.TOGGLE_PLAY_NEXT,
        "Return": Action.ACTIVATE,
        "Back": Action.PARENT_FOLDER,
        "Escape": Action.CLOSE_LIST_PREVIEW,
        "Left": Action.QUICK_INFORMATION,
        "Space": Action.PLAY_PAUSE,
        "Ctrl+M": Action.TOGGLE_MUTE,
    }
    order = ["files", "heos"]
    menu_actions = {
        item.action for section in menus_for_session(SessionId.HEOS, order)
        for item in section.items if item.action is not None
    }
    palette_actions = {
        entry.action for entry in entries_for_session(SessionId.HEOS, order)
    }
    help_rows = describe("HEOS", set(cases.values()))
    help_labels = {shortcut for shortcut, _label in help_rows}
    seen = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.HEOS), client=None,
        announcer=SimpleNamespace(say=lambda _text: None),
        _dispatch_heos=lambda action: seen.append(action),
    )
    for chord, action in cases.items():
        assert resolve(Chord(chord), player_view=False, radio_session=False,
                       heos_session=True) is action
        if action is not Action.CLOSE_LIST_PREVIEW:
            # Escape uses the existing parent command's menu/palette entry.
            assert action in menu_actions
            assert action in palette_actions
        assert {
            "Return": "Enter", "Back": "Backspace",
            "Left": "Strzałka w lewo (HEOS)",
            "Ctrl+Shift+Return": "Ctrl+Shift+Enter",
        }.get(chord, chord) in help_labels
        if action is not Action.SESSION_CONFIGURE_CURRENT:
            LiteFrame._dispatch(frame, action)
            assert seen[-1] is action

    assert resolve(Chord("Left"), player_view=True, radio_session=False,
                   heos_session=True) is Action.QUICK_INFORMATION


def test_backspace_and_escape_in_heos_catalog_restore_parent_selection():
    from amc_wx_lite.heos_source import HeosSource, catalog_items

    state = HeosBrowseState()
    state.set_targets((gui.HeosTarget("speaker", "Głośnik", "1"),))
    state.choose_target("speaker")
    source = HeosSource("favorites", "Favorites", "1024", "music_service")
    assert state.apply_list(state.begin_list_request(), (source,))
    state.open_source(source)
    albums = catalog_items("1024", None, [
        {"name": "Album", "type": "album", "cid": "a", "container": "yes"},
    ])
    assert state.apply_list(state.begin_list_request(), albums)
    state.open_item(albums[0])
    loaded = []
    frame = SimpleNamespace(
        _heos_state=state,
        _heos_load_location=lambda: loaded.append(state.location.title),
        navigator=SimpleNamespace(sessions={SessionId.HEOS: SimpleNamespace()}),
    )
    LiteFrame._dispatch_heos(frame, Action.PARENT_FOLDER)
    assert state.location.title == "Favorites"
    assert state.selected_item_id == albums[0].item_id
    assert loaded == ["Favorites"]
    LiteFrame._dispatch_heos(frame, Action.CLOSE_LIST_PREVIEW)
    assert state.location.kind == "sources"
    assert state.selected_item_id == source.item_id
    assert loaded == ["Favorites", "Biblioteka HEOS"]


def test_left_in_heos_catalog_speaks_information_without_ids():
    from amc_wx_lite.heos_source import HeosSource
    from amc_wx_lite.navigation import View

    source = HeosSource("internal-id", "Favorites", "1024", "music_service")
    state = HeosBrowseState(items=(source,))
    spoken = []
    session = SimpleNamespace(
        view=View.LIST,
        model=SimpleNamespace(selected_row=SimpleNamespace(
            item_id=source.item_id, title=source.title,
        )),
    )
    frame = SimpleNamespace(
        _heos_state=state,
        navigator=SimpleNamespace(sessions={SessionId.HEOS: session}),
        announcer=SimpleNamespace(say=spoken.append),
    )
    frame._heos_quick_information = lambda: LiteFrame._heos_quick_information(frame)
    LiteFrame._dispatch_heos(frame, Action.QUICK_INFORMATION)
    assert spoken == ["Favorites, źródło HEOS"]
    session.view = View.PLAYER
    frame._heos_status = SimpleNamespace(spoken_label=lambda: "Utwór, odtwarzanie")
    LiteFrame._dispatch_heos(frame, Action.QUICK_INFORMATION)
    assert spoken[-1] == "Utwór, odtwarzanie"


def test_favorites_from_heos_player_opens_speaker_source_list():
    from amc_wx_lite.heos_source import HeosSource, HeosTarget
    from amc_wx_lite.navigation import View

    source = HeosSource("favorites", "Favorites", "1024", "music_service")
    state = HeosBrowseState()
    state.set_targets((HeosTarget("speaker", "Głośnik", "1"),))
    state.choose_target("speaker")
    session = SimpleNamespace(view=View.PLAYER)
    loaded = []

    class Runner:
        def submit(self, _name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        _heos_state=state,
        _heos_service=SimpleNamespace(sources=lambda: (source,)),
        _heos_render=lambda: None,
        _heos_load_location=lambda: loaded.append(state.location.title),
        _heos_failure=lambda error: (_ for _ in ()).throw(error),
        navigator=SimpleNamespace(sessions={SessionId.HEOS: session}),
        runner=Runner(),
    )
    frame._heos_open_named_source = lambda kind: LiteFrame._heos_open_named_source(frame, kind)
    LiteFrame._dispatch_heos(frame, Action.VIEW_FAVORITES)
    assert session.view is View.LIST
    assert state.location.kind == "catalog"
    assert state.location.title == "Favorites"
    assert loaded == ["Favorites"]


def test_heos_queue_action_uses_current_playable_item_and_reads_back():
    item = HeosItem("item", "Utwór", "utwór", "5", "", "8", False, True)
    state = HeosBrowseState()
    state.location = HeosLocation("catalog", "Album", "5")
    state.items = (item,)

    class Service:
        def __init__(self):
            self.queued = []

        def queue(self, _player_id):
            return tuple(self.queued)

        def play_item(self, _player_id, chosen, *, placement):
            assert chosen is item
            assert placement == "next"
            self.queued.append(chosen)

    service = Service()
    spoken = []
    frame = SimpleNamespace(
        _heos_state=state,
        navigator=SimpleNamespace(sessions={SessionId.HEOS: SimpleNamespace(
            model=SimpleNamespace(selected_row=SimpleNamespace(item_id="item"))
        )}),
        announcer=SimpleNamespace(say=spoken.append),
    )
    frame._heos_selected_queue_candidate = lambda: LiteFrame._heos_selected_queue_candidate(frame)

    def control(operation, message):
        operation(service, "player")
        spoken.append(message)

    frame._heos_control = control
    LiteFrame._heos_enqueue(frame, next_item=True)
    assert service.queued == [item]
    assert spoken == ["HEOS: dodano jako następne: Utwór"]

    state.items = (HeosItem("item", "Radio", "stacja", "5", "", "8", False, True,
                            media_type="station"),)
    assert LiteFrame._heos_selected_queue_candidate(frame) is None


def test_heos_target_dialog_restores_list_focus_after_both_choices(monkeypatch):
    events = []

    class Choice:
        selection = 0

        def __init__(self, *_args):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            pass

        def SetName(self, _name):
            pass

        def ShowModal(self):
            return gui.wx.ID_OK

        def GetSelection(self):
            return self.selection

    class Address(Choice):
        def GetValue(self):
            return "192.0.2.1"

    monkeypatch.setattr(gui.wx, "SingleChoiceDialog", Choice, raising=False)
    monkeypatch.setattr(gui.wx, "TextEntryDialog", Address, raising=False)
    frame = SimpleNamespace(
        _load_heos_targets=lambda host="": events.append(("load", host)),
        _restore_focus_after_dialog=lambda: events.append(("focus", None)),
    )
    LiteFrame._configure_heos_target(frame)
    assert events == [("load", ""), ("focus", None)]
    events.clear()
    Choice.selection = 1
    LiteFrame._configure_heos_target(frame)
    assert events == [("load", "192.0.2.1"), ("focus", None)]


def test_heos_unavailable_list_speaks_user_label_without_command_id():
    spoken = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.HEOS),
        announcer=SimpleNamespace(say=spoken.append),
    )
    LiteFrame._heos_failure(frame, HeosListUnavailable("browse/browse"))
    assert spoken == ["To źródło HEOS nie udostępnia teraz listy"]
