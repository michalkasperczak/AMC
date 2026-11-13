"""Rzeczywiste metody LiteFrame z atrapa wx; nie jest to odbior GUI/NVDA."""
import sys
from pathlib import Path
from types import SimpleNamespace
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import FakeKeyEvent, install_wx_stub
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
                            _run=requests.extend,
                            _announce_transient_preview=requests.extend,
                            _open_radio_view=lambda intent, **_kwargs: requests.append(intent),
                            messages=SimpleNamespace(custom_seek_seconds=300),
                            _radio_effective_sort_modes={
                                "library": "AddedNewest",
                                "favorites": "AddedNewest",
                            },
                            _transient_preview_return=None)
    frame._begin_transient_preview = lambda: LiteFrame._begin_transient_preview(frame)
    frame._show_playback_history_preview = (
        lambda: LiteFrame._show_playback_history_preview(frame)
    )
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
                              ("Ctrl+H", Action.VIEW_HISTORY, LibraryView.HISTORY),
                              ("Ctrl+P", Action.VIEW_PLAYLISTS, LibraryView.PLAYLISTS)]:
        assert LIST_VIEW[key] is action and menu[action].shortcut == key
        assert key in help_rows
        frame, requests, _ = radio_shell()
        frame._return_to_library = lambda: LiteFrame._return_to_library(frame)
        LiteFrame._dispatch(frame, action)
        assert len(requests) == 1 and requests[0].target_session_id is SessionId.RADIO
        assert requests[0].view is view


def test_main_window_routes_radio_sort_favorite_and_library_shortcuts():
    from amc_wx_lite.shortcuts import Action

    frame, _, _ = radio_shell()
    actions = []
    frame._dispatch = actions.append
    frame._screen_reader_modifier_down = lambda: False
    frame.filter_box = SimpleNamespace(GetValue=lambda: "")
    frame.options = SimpleNamespace(session_order=None)

    for event, expected in (
        (FakeKeyEvent(ord("1"), alt=True), Action.SORT_RADIO_COLLECTION_ADDED),
        (FakeKeyEvent(ord("2"), alt=True), Action.SORT_RADIO_COLLECTION_ALPHABETICAL),
        (FakeKeyEvent(ord("3"), alt=True), Action.SORT_RADIO_COLLECTION_CUSTOM),
        (FakeKeyEvent(ord("U"), ctrl=True, shift=True), Action.TOGGLE_FAVORITE),
        (FakeKeyEvent(ord("L"), ctrl=True, shift=True), Action.TOGGLE_RADIO_LIBRARY),
    ):
        LiteFrame._on_key(frame, event)
        assert actions[-1] is expected


def test_main_window_routes_shared_order_shortcuts_in_radio_playlist():
    import wx
    from amc_wx_lite.shortcuts import Action

    frame, _, _ = radio_shell()
    frame.navigator.session.library_view = LibraryView.PLAYLIST_CONTENTS
    frame.navigator.session.library_playlist_id = "radio-morning"
    actions = []
    frame._dispatch = actions.append
    frame._screen_reader_modifier_down = lambda: False
    frame.filter_box = SimpleNamespace(GetValue=lambda: "")
    frame.options = SimpleNamespace(session_order=None)

    for event, expected in (
        (FakeKeyEvent(ord("X"), ctrl=True), Action.CUT_FILE),
        (FakeKeyEvent(ord("V"), ctrl=True), Action.PASTE_OR_MOVE),
        (FakeKeyEvent(wx.WXK_UP, alt=True), Action.MOVE_LIST_ITEM_UP),
        (FakeKeyEvent(wx.WXK_DOWN, alt=True), Action.MOVE_LIST_ITEM_DOWN),
    ):
        LiteFrame._on_key(frame, event)
        assert actions[-1] is expected


def test_dispatch_uses_same_radio_sort_and_library_implementations_as_menu():
    from amc_wx_lite.shortcuts import Action

    frame, _, _ = radio_shell()
    calls = []
    frame._set_radio_collection_sort = lambda action: calls.append(("sort", action))
    frame._toggle_radio_library = lambda: calls.append(("library", None))
    frame._toggle_profile_favorite = lambda: calls.append(("favorite", None))
    LiteFrame._dispatch(frame, Action.SORT_RADIO_COLLECTION_ALPHABETICAL)
    LiteFrame._dispatch(frame, Action.TOGGLE_RADIO_LIBRARY)
    LiteFrame._dispatch(frame, Action.TOGGLE_FAVORITE)
    assert calls == [
        ("sort", Action.SORT_RADIO_COLLECTION_ALPHABETICAL),
        ("library", None),
        ("favorite", None),
    ]


def test_radio_library_and_favorite_commands_write_and_announce_result():
    from amc_wx_lite.list_model import Row

    frame, _, messages = radio_shell()
    row = Row(item_id="radio-1", title="Jedynka", kind="station")
    frame.navigator.apply_stations([row])
    calls, refreshes = [], []
    frame.client = SimpleNamespace(
        toggle_radio_library=lambda ids: calls.append(("library", ids)) or {
            "changed": 1,
            "inLibrary": False,
        },
        toggle_profile_favorites=lambda session, ids: calls.append(
            ("favorite", session, ids)
        ) or {
            "changed": 1,
            "favorite": True,
        },
    )
    frame.runner = DeferredRunner()
    frame._selected_action_rows = lambda: [row]

    def refresh(**kwargs):
        refreshes.append(kwargs)
        callback = kwargs.get("after_refresh")
        if callback is not None:
            callback()

    frame._refresh_profile_view = refresh

    LiteFrame._toggle_radio_library(frame)
    assert frame.runner.key == "radio-library"
    frame.runner.done(frame.runner.work())
    assert calls[-1] == ("library", ["radio-1"])
    assert messages[-1] == "Usunięto z Biblioteki: Jedynka"

    LiteFrame._toggle_profile_favorite(frame)
    assert frame.runner.key == "profile-favorite"
    frame.runner.done(frame.runner.work())
    assert calls[-1] == ("favorite", "radio", ["radio-1"])
    assert messages[-1] == "Dodano do ulubionych: Jedynka"
    assert [item["preferred_id"] for item in refreshes] == [None]


def test_radio_sort_command_persists_refreshes_and_announces_user_label():
    from amc_wx_lite.list_model import Row
    from amc_wx_lite.shortcuts import Action

    frame, _, messages = radio_shell()
    frame.navigator.apply_stations([
        Row(item_id="radio-1", title="Jedynka", kind="station")
    ])
    calls, opened = [], []
    frame.client = SimpleNamespace(
        set_radio_collection_sort=lambda view, mode: calls.append(
            (view, mode)
        ) or {"sortMode": mode}
    )
    frame.runner = DeferredRunner()
    frame._refresh_menu_state = lambda: None

    def open_view(intent, *, after_refresh=None):
        opened.append(intent)
        if after_refresh is not None:
            after_refresh()

    frame._open_radio_view = open_view
    LiteFrame._set_radio_collection_sort(
        frame,
        Action.SORT_RADIO_COLLECTION_ALPHABETICAL,
    )
    assert frame.runner.key == "radio-sort"
    frame.runner.done(frame.runner.work())
    assert calls == [("library", "Alphabetical")]
    assert opened[0].preferred_id == "radio-1"
    assert messages == ["Radio: alfabetycznie"]


def test_shift_r_dispatches_direct_new_schedule_request():
    from amc_wx_lite.shortcuts import Action

    frame, _, _ = radio_shell()
    calls = []
    frame._show_radio_schedules = lambda **kwargs: calls.append(kwargs)
    LiteFrame._dispatch(frame, Action.ADD_RADIO_SCHEDULE)
    assert calls == [{"create_for_selected": True}]


def test_ctrl_f5_dispatches_radio_refresh_and_preserves_selection():
    from amc_wx_lite.list_model import Row
    from amc_wx_lite.shortcuts import Action

    frame, _, messages = radio_shell()
    frame.navigator.apply_stations([
        Row(item_id="radio-2", title="Radio Dwa", kind="station")
    ])
    refreshes = []
    frame._refresh_profile_view = lambda **kwargs: refreshes.append(kwargs)

    LiteFrame._dispatch(frame, Action.REFRESH_RADIO_STATIONS)

    assert messages == ["Odświeżanie stacji Radia"]
    assert refreshes[0]["preferred_id"] == "radio-2"
    refreshes[0]["after_refresh"]()
    assert messages == [
        "Odświeżanie stacji Radia",
        "Odświeżono stacje Radia",
    ]


def test_shared_profile_add_station_uses_host_writer_and_opens_library():
    import amc_wx_lite.gui as gui

    calls, opened, messages = [], [], []

    class FakeDialog:
        values = ("Radio Test", "https://radio.invalid/live")

        def __init__(self, *_args, **_kwargs):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):
            return gui.wx.ID_OK

    client = SimpleNamespace(
        add_radio_station=lambda name, url: calls.append((name, url)) or {
            "addedCount": 1,
            "updatedCount": 0,
            "preferredStationId": "radio:custom:1",
            "preferredStationName": name,
        }
    )
    frame = SimpleNamespace(
        radio=SimpleNamespace(may_edit=False),
        runner=DeferredRunner(),
        announcer=SimpleNamespace(say=messages.append),
        _switch_session=lambda session: calls.append(("session", session)),
        _profile_edit_client=lambda: client,
    )

    def open_view(intent, *, after_refresh=None):
        opened.append(intent)
        if after_refresh is not None:
            after_refresh()

    frame._open_radio_view = open_view
    original = gui.StationDialog
    gui.StationDialog = FakeDialog
    try:
        LiteFrame._station_add(frame)
        assert frame.runner.key == "profile-edit"
        frame.runner.done(frame.runner.work())
    finally:
        gui.StationDialog = original

    assert calls[-1] == ("Radio Test", "https://radio.invalid/live")
    assert len(opened) == 1
    assert opened[0].view is None
    assert opened[0].preferred_id == "radio:custom:1"
    assert messages == ["Dodano stację do Biblioteki: Radio Test"]


def test_shared_profile_import_persists_and_preserves_existing_names():
    import amc_wx_lite.gui as gui

    calls, opened, messages = [], [], []

    class FakeFileDialog:
        def __init__(self, *_args, **_kwargs):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):
            return gui.wx.ID_OK

        def GetPath(self):
            return "D:/wymiana/stacje.m3u"

    client = SimpleNamespace(
        import_playlist_to_library=lambda path: calls.append(path) or {
            "addedCount": 2,
            "promotedCount": 1,
            "skippedCount": 3,
            "preferredStationId": "radio:catalog:1",
        }
    )
    frame = SimpleNamespace(
        client=client,
        radio=SimpleNamespace(may_edit=False),
        runner=DeferredRunner(),
        announcer=SimpleNamespace(say=messages.append),
        _switch_session=lambda _session: None,
    )

    def open_view(intent, *, after_refresh=None):
        opened.append(intent)
        if after_refresh is not None:
            after_refresh()

    frame._open_radio_view = open_view
    original_dialog = gui.wx.FileDialog
    original_open = gui.wx.FD_OPEN
    original_existing = gui.wx.FD_FILE_MUST_EXIST
    gui.wx.FileDialog = FakeFileDialog
    gui.wx.FD_OPEN = 1
    gui.wx.FD_FILE_MUST_EXIST = 2
    try:
        LiteFrame._station_import(frame)
        assert frame.runner.key == "import"
        frame.runner.done(frame.runner.work())
    finally:
        gui.wx.FileDialog = original_dialog
        gui.wx.FD_OPEN = original_open
        gui.wx.FD_FILE_MUST_EXIST = original_existing

    assert calls == ["D:/wymiana/stacje.m3u"]
    assert len(opened) == 1 and opened[0].view is None
    assert opened[0].preferred_id == "radio:catalog:1"
    assert messages == [
        "Dodano nowe stacje: 2. Włączono istniejące w Bibliotece: 1. "
        "Zachowano dotychczasowe nazwy. Pominięto: 3"
    ]


def test_radio_favorites_export_uses_selected_format_and_short_message():
    import amc_wx_lite.gui as gui

    calls, messages = [], []

    class FakeFileDialog:
        def __init__(self, *_args, **_kwargs):
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):
            return gui.wx.ID_OK

        def GetPath(self):
            return "D:/wymiana/ulubione.m3u"

        def GetFilterIndex(self):
            return gui.RADIO_EXPORT_JSON_FILTER

    client = SimpleNamespace(
        export_radio_favorites=lambda path: calls.append(path) or {"exportedCount": 4}
    )
    frame = SimpleNamespace(
        client=client,
        runner=DeferredRunner(),
        announcer=SimpleNamespace(say=messages.append),
        _restore_focus_after_dialog=lambda: None,
    )
    original_dialog = gui.wx.FileDialog
    original_save = getattr(gui.wx, "FD_SAVE", None)
    original_overwrite = getattr(gui.wx, "FD_OVERWRITE_PROMPT", None)
    gui.wx.FileDialog = FakeFileDialog
    gui.wx.FD_SAVE = 1
    gui.wx.FD_OVERWRITE_PROMPT = 2
    try:
        LiteFrame._station_export_favorites(frame)
        assert frame.runner.key == "radio-export"
        frame.runner.done(frame.runner.work())
    finally:
        gui.wx.FileDialog = original_dialog
        if original_save is None:
            delattr(gui.wx, "FD_SAVE")
        else:
            gui.wx.FD_SAVE = original_save
        if original_overwrite is None:
            delattr(gui.wx, "FD_OVERWRITE_PROMPT")
        else:
            gui.wx.FD_OVERWRITE_PROMPT = original_overwrite

    assert calls == [str(Path("D:/wymiana/ulubione.m3u").with_suffix(".json"))]
    assert messages == ["Wyeksportowano ulubione stacje: 4"]


def test_direct_new_schedule_editor_uses_selected_station_and_commits():
    import amc_wx_lite.gui as gui
    from amc_wx_lite.state_store import Station

    selected = Station(id="radio-1", name="Radio Test", url="https://example.invalid/")
    snapshot = SimpleNamespace(stations=(selected,), recording=object())
    schedule = {"id": "plan-1", "name": "Poranny plan", "stationId": selected.id}
    received, committed, messages, focus = {}, [], [], []

    class FakeDialog:
        result_schedule = schedule

        def __init__(self, _parent, **kwargs):
            received.update(kwargs)

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):
            return gui.wx.ID_OK

    frame = SimpleNamespace(
        announcer=SimpleNamespace(say=messages.append),
        _restore_focus_after_dialog=lambda: focus.append(True),
    )
    original = gui.RadioScheduleEditorDialog
    gui.RadioScheduleEditorDialog = FakeDialog
    try:
        LiteFrame._open_new_radio_schedule(
            frame,
            snapshot=snapshot,
            schedules=[],
            wake=False,
            selected_station=selected,
            commit=lambda schedules, wake: committed.append((schedules, wake)),
        )
    finally:
        gui.RadioScheduleEditorDialog = original

    assert received["preferred_station_id"] == selected.id
    assert received["stations"] == [selected]
    assert committed == [([schedule], False)]
    assert messages == ["Dodano plan Poranny plan"]
    assert focus == [True]


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
        frame._open_library_view = (
            lambda intent, **kwargs: LiteFrame._open_library_view(
                frame, intent, **kwargs
            )
        )
        frame._open_radio_view = (
            lambda intent, **kwargs: LiteFrame._open_radio_view(
                frame, intent, **kwargs
            )
        )
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

        state = open_view(Action.VIEW_PLAYLISTS)
        assert [row.title for row in state.model.rows] == ["Poranne radio"]
        request = next(
            item for item in frame.navigator.activate_selected()
            if isinstance(item, OpenLibraryView)
        )
        assert request.target_session_id is SessionId.RADIO
        LiteFrame._open_library_view(frame, request)
        frame.runner.done(frame.runner.work())
        state = frame.navigator.session
        assert [row.item_id for row in state.model.rows] == ["3", "1"]
        assert state.library_playlist_id == "radio-morning"

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
        frame._open_radio_view = (
            lambda intent, **kwargs: LiteFrame._open_radio_view(
                frame, intent, **kwargs
            )
        )
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
