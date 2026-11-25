from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, LIST_VIEW, PLAYER_VIEW, RADIOARCHIWISTA_SESSION_VIEW
from amc_wx_lite import menu_model


def test_play_next_shift_enter_and_old_shortcut_free():
    for shortcuts in (LIST_VIEW, PLAYER_VIEW, RADIOARCHIWISTA_SESSION_VIEW):
        assert shortcuts["Shift+Return"] is Action.TOGGLE_PLAY_NEXT
        assert "Ctrl+Shift+Return" not in shortcuts
        assert shortcuts["Ctrl+Shift+Q"] is Action.ADD_TO_QUEUE


def test_play_next_menu_uses_shift_enter():
    items = menu_model.build_menus(SessionId.FILES)
    def walk(items):
        for item in items:
            yield item
            yield from walk(getattr(item, "items", ()) or ())
    matching = [item for item in walk(items) if getattr(item, "action", None) is Action.TOGGLE_PLAY_NEXT]
    assert matching and all(item.shortcut == "Shift+Return" for item in matching)


def test_search_results_keyboard_uses_next_and_queue_without_old_alias():
    from types import SimpleNamespace
    import amc_wx_lite.gui as gui
    from amc_wx_lite.search_catalog import SearchResultAction

    actions = []
    dialog = SimpleNamespace(_complete_selected=actions.append)
    def key(code, *, ctrl=False, shift=False):
        return SimpleNamespace(
            GetKeyCode=lambda: code,
            ControlDown=lambda: ctrl,
            ShiftDown=lambda: shift,
            AltDown=lambda: False,
            Skip=lambda: None,
        )
    for code in (gui.wx.WXK_RETURN, gui.wx.WXK_NUMPAD_ENTER):
        gui.SearchDialog._on_results_key(dialog, key(code, shift=True))
        gui.SearchDialog._on_results_key(dialog, key(code, ctrl=True, shift=True))
    gui.SearchDialog._on_results_key(dialog, key(ord("Q"), ctrl=True, shift=True))
    assert actions == [SearchResultAction.PLAY_NEXT, SearchResultAction.PLAY_NEXT, SearchResultAction.QUEUE]
