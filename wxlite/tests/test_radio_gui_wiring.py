"""Rzeczywiste metody LiteFrame z atrapa wx; nie jest to odbior GUI/NVDA."""
import sys
from pathlib import Path
from types import SimpleNamespace
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.radio_views import RadioViewResult
from amc_wx_lite.navigation import LibraryView, Navigator, OpenLibraryView, SessionId


class NoLocalRead:
    @property
    def is_available(self):
        raise AssertionError("Widok Radia probuje czytac lokalna Biblioteke")


def radio_shell():
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    requests, messages = [], []
    frame = SimpleNamespace(navigator=nav, library=NoLocalRead(),
                            announcer=SimpleNamespace(say=messages.append),
                            _run=requests.extend, _open_radio_view=requests.append,
                            messages=SimpleNamespace(custom_seek_seconds=300))
    return frame, requests, messages


def test_radio_read_request_does_not_go_to_local_provider():
    frame, requests, _ = radio_shell()
    request = frame.navigator.open_library_view(LibraryView.HISTORY)[0]
    LiteFrame._open_library_view(frame, request)
    assert requests == [request]


def test_ctrl_l_gui_routes_radio_without_local_settings():
    frame, requests, _ = radio_shell()
    LiteFrame._return_to_library(frame)
    assert len(requests) == 1, "Ctrl+L Radia nie wywolal odczytu Biblioteki stacji"
    assert isinstance(requests[0], OpenLibraryView)
    assert requests[0].target_session_id is SessionId.RADIO
    assert requests[0].view is None


class DeferredRunner:
    def submit(self, key, work, done, failed):
        self.key, self.work, self.done, self.failed = key, work, done, failed


def test_radio_loader_completion_updates_radio_without_announcing_in_files():
    from amc_wx_lite.list_model import Row
    frame, requests, messages = radio_shell()
    frame.runner = DeferredRunner()
    frame.radio = object()
    intent = frame.navigator.open_library_view(LibraryView.HISTORY)[0]
    LiteFrame._open_radio_view(frame, intent)
    frame.navigator.switch_session(SessionId.FILES)
    before = frame.navigator.session.model.rows[:]
    frame.runner.done(RadioViewResult(rows=[Row(item_id="outside", title="Outside", kind="station", url="https://example.invalid/")],
                                      heading="Historia radia", unavailable_reason=None, missing_item_count=0))
    assert frame.navigator.sessions[SessionId.RADIO].model.selected_id == "outside"
    assert frame.navigator.session.model.rows == before
    assert not requests and not messages, "Spoznione Radio przemowilo w Plikach"


def test_radio_unavailable_keeps_previous_rows_and_scope():
    from amc_wx_lite.list_model import Row
    frame, requests, messages = radio_shell()
    frame.navigator.apply_stations([Row(item_id="keep", title="Keep", kind="station")])
    frame.runner = DeferredRunner()
    frame.radio = object()
    LiteFrame._open_radio_view(frame, frame.navigator.open_library_view(LibraryView.FAVORITES)[0])
    frame.runner.done(RadioViewResult(rows=[], heading="Ulubione", unavailable_reason="Nie moge odczytac profilu", missing_item_count=0))
    assert frame.navigator.session.model.selected_id == "keep"
    assert frame.navigator.session.library_view is None
    assert messages == ["Nie moge odczytac profilu"]


def test_menu_help_and_keyboard_share_radio_actions():
    from amc_wx_lite.menu_model import build_menus
    from amc_wx_lite.shortcuts import LIST_VIEW, Action, describe
    menu = {item.action: item for group in build_menus() for item in group.items if item.action}
    help_rows = dict(describe())
    for key, action, view in [("Ctrl+L", Action.VIEW_LIBRARY, None),
                              ("Ctrl+U", Action.VIEW_FAVORITES, LibraryView.FAVORITES),
                              ("Ctrl+H", Action.VIEW_HISTORY, LibraryView.HISTORY)]:
        assert LIST_VIEW[key] is action and menu[action].shortcut == key
        assert key in help_rows
        frame, requests, _ = radio_shell()
        frame._return_to_library = lambda: LiteFrame._return_to_library(frame)
        LiteFrame._dispatch(frame, action)
        assert len(requests) == 1 and requests[0].target_session_id is SessionId.RADIO
        assert requests[0].view is view


def test_real_radio_data_flows_through_gui_dispatch_without_profile_writes():
    from test_radio_view_data import RadioViewDataBase
    from amc_wx_lite.list_model import rows_from_stations
    from amc_wx_lite.list_filter import FilterState
    from amc_wx_lite.navigation import view_context, PlayStation
    from amc_wx_lite.shortcuts import Action
    fixture = RadioViewDataBase()
    fixture.setUp()
    try:
        frame, events, said = radio_shell()
        frame.radio = fixture.source
        frame.runner = DeferredRunner()
        frame._sync_views = lambda: None
        frame._open_library_view = lambda intent: LiteFrame._open_library_view(frame, intent)
        frame._open_radio_view = lambda intent: LiteFrame._open_radio_view(frame, intent)
        frame._return_to_library = lambda: LiteFrame._return_to_library(frame)
        frame._run = lambda intents: LiteFrame._run(frame, intents)
        snapshot = fixture.source.load()
        frame.navigator.apply_stations(rows_from_stations(snapshot.as_payload()), preferred_id="2")
        filters = FilterState()
        library_context = view_context(frame.navigator.session)
        filters.set_for_view(library_context, "dwoj")

        def open_view(action):
            LiteFrame._dispatch(frame, action)
            assert frame.runner.key == "radio-view"
            frame.runner.done(frame.runner.work())
            return frame.navigator.session

        state = open_view(Action.VIEW_FAVORITES)
        assert {r.item_id for r in state.model.rows} == {"1", "3"}
        state.model.select_id("3")
        station = next(c for c in frame.navigator.activate_selected() if isinstance(c, PlayStation))
        assert station.item_id == "3" and station.url == "http://ns"
        frame.navigator.back_to_list()
        assert state.model.selected_id == "3"
        favorites_context = view_context(state)
        filters.set_for_view(favorites_context, "nowy")
        state = open_view(Action.VIEW_HISTORY)
        assert [r.item_id for r in state.model.rows] == ["4", "1", "2"]
        assert "bez stacji" in " ".join(said)
        state = open_view(Action.VIEW_LIBRARY)
        assert state.model.selected_id == "2"
        assert view_context(state) == library_context
        assert filters.text_for_view(view_context(state)) == "dwoj"
        state = open_view(Action.VIEW_FAVORITES)
        assert state.model.selected_id == "3"
        assert filters.text_for_view(view_context(state)) == "nowy"
        assert frame.navigator.sessions[SessionId.FILES].model.rows == []
        fixture.assert_nothing_written()
    finally:
        fixture.tearDown()


def test_initial_radio_library_uses_saved_order_before_ctrl_l():
    from test_radio_saved_order import RadioOrderBase
    fixture = RadioOrderBase()
    fixture.SORT_MODES = {"Biblioteka": "Custom"}
    fixture.setUp()
    try:
        frame, _, _ = radio_shell()
        frame.navigator.switch_session(SessionId.FILES)
        frame.radio = fixture.source
        frame._radio_snapshot = fixture.source.load()
        frame.stations = frame._radio_snapshot.list
        frame.options = SimpleNamespace(last_folder="")
        frame.library = SimpleNamespace(is_available=False, describe=lambda: "")
        frame.runner = DeferredRunner()
        frame._sync_views = lambda: None
        frame._open_radio_view = lambda intent: LiteFrame._open_radio_view(frame, intent)
        frame._run = lambda intents: LiteFrame._run(frame, intents)
        LiteFrame._load_initial_content(frame)
        # Zadanie jest konczone jawnie: bez dodatkowego Ctrl+L po starcie.
        if hasattr(frame.runner, "work"):
            frame.runner.done(frame.runner.work())
        state = frame.navigator.sessions[SessionId.RADIO]
        assert [row.item_id for row in state.model.rows] == ["b", "a", "c"], (
            "Pierwszy widok Radia nadal ma kolejnosc cache, nie zapisana"
        )
        assert state.model.selected_id == "b"
    finally:
        fixture.tearDown()
