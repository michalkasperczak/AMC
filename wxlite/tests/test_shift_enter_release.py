from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, LIST_VIEW, PLAYER_VIEW, RADIOARCHIWISTA_SESSION_VIEW
from amc_wx_lite import menu_model


def test_play_next_shift_enter_and_old_shortcut_free():
    for shortcuts in (LIST_VIEW, PLAYER_VIEW, RADIOARCHIWISTA_SESSION_VIEW):
        assert shortcuts["Shift+Return"] is Action.PLAY_NEXT_ONCE
        assert shortcuts["Ctrl+Shift+Return"] is Action.TOGGLE_PLAY_NEXT
        assert shortcuts["Ctrl+Shift+Q"] is Action.ADD_TO_QUEUE


def test_play_next_menu_uses_shift_enter():
    items = menu_model.build_menus(SessionId.FILES)
    def walk(items):
        for item in items:
            yield item
            yield from walk(getattr(item, "items", ()) or ())
    one_shot = [item for item in walk(items) if getattr(item, "action", None) is Action.PLAY_NEXT_ONCE]
    toggle = [item for item in walk(items) if getattr(item, "action", None) is Action.TOGGLE_PLAY_NEXT]
    assert one_shot and all(item.shortcut == "Shift+Return" for item in one_shot)
    assert toggle and all(item.shortcut == "Ctrl+Shift+Return" for item in toggle)


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
    assert actions == [SearchResultAction.PLAY_NEXT, SearchResultAction.PLAY_NEXT_TOGGLE,
                       SearchResultAction.PLAY_NEXT, SearchResultAction.PLAY_NEXT_TOGGLE,
                       SearchResultAction.QUEUE]


def test_search_results_one_shot_keeps_dialog_and_focus():
    from types import SimpleNamespace
    import amc_wx_lite.gui as gui
    from amc_wx_lite.search_catalog import SearchResultAction

    entry = SimpleNamespace(session_id=SessionId.FILES, source_view=None, parent_id=None)
    calls = []
    dialog = SimpleNamespace(
        _lead_entry=lambda: entry,
        _selected_entries=lambda: [entry],
        _execute_direct_action=lambda entries, action, owner: calls.append((entries, action, owner)),
        EndModal=lambda result: calls.append(("closed", result)),
        announcer=SimpleNamespace(say=lambda message: None),
    )
    gui.SearchDialog._complete_selected(dialog, SearchResultAction.PLAY_NEXT)
    assert len(calls) == 1
    assert calls[0][0] == [entry]
    assert calls[0][1] is SearchResultAction.PLAY_NEXT
    assert calls[0][2] is dialog


def test_search_results_one_shot_adds_in_visible_selection_order():
    from types import SimpleNamespace
    import amc_wx_lite.gui as gui
    from amc_wx_lite.list_model import Row

    rows = [
        Row("x", "X", "track", path=r"C:\music\x.mp3"),
        Row("y", "Y", "track", path=r"C:\music\y.mp3"),
    ]
    calls, spoken = [], []
    frame = SimpleNamespace(
        client=SimpleNamespace(queue_add_play_next=lambda items: (
            calls.append([item["id"] for item in items]) or {"changed": 2}
        )),
        runner=SimpleNamespace(submit=lambda slot, work, done, failed: done(work())),
    )
    entries = [SimpleNamespace(session_id=SessionId.FILES, row=row, remote_kind=None)
               for row in rows]
    gui.LiteFrame._add_search_results_play_next(frame, entries, announce=spoken.append)
    assert calls == [["x", "y"]]
    assert spoken == ["Odtwarzaj jako następne: 2 elementy"]


def test_search_play_next_rejects_mixed_sessions():
    from types import SimpleNamespace
    import amc_wx_lite.gui as gui
    from amc_wx_lite.list_model import Row

    entries = [
        SimpleNamespace(session_id=SessionId.FILES,
                        row=Row("x", "X", "track", path=r"C:\music\x.mp3"),
                        remote_kind=None),
        SimpleNamespace(session_id=SessionId.PODCASTS,
                        row=Row("y", "Y", "episode"), remote_kind=None),
    ]
    spoken = []
    gui.LiteFrame._add_search_results_play_next(
        SimpleNamespace(), entries, announce=spoken.append)
    assert spoken == ["Wybierz wyniki z jednej sesji"]


def test_one_shot_is_in_menu_palette_and_help_only_for_supported_sessions():
    from amc_wx_lite.command_palette import entries_for_session
    from amc_wx_lite.shortcuts import describe, Chord, keyboard_help_announcement

    for session in (SessionId.FILES, SessionId.PODCASTS, SessionId.RADIOARCHIWISTA):
        palette = {item.action: item for item in entries_for_session(session)}
        assert palette[Action.PLAY_NEXT_ONCE].shortcut == "Shift+Enter"
        assert palette[Action.TOGGLE_PLAY_NEXT].shortcut == "Ctrl+Shift+Enter"
    for session in (SessionId.RADIO, SessionId.TIDAL, SessionId.SPOTIFY,
                    SessionId.WIIM, SessionId.SONOS, SessionId.HEOS):
        assert Action.PLAY_NEXT_ONCE not in {item.action for item in entries_for_session(session)}
    help_rows = describe("Pliki lokalne", {Action.PLAY_NEXT_ONCE, Action.TOGGLE_PLAY_NEXT})
    assert any(shortcut == "Shift+Enter" for shortcut, _ in help_rows)
    assert any(shortcut == "Ctrl+Shift+Enter" for shortcut, _ in help_rows)
    spoken = keyboard_help_announcement(
        Chord("Return", shift=True), Action.PLAY_NEXT_ONCE, "Pliki lokalne")
    assert "Shift+Enter" in spoken and "Dodaj zaznaczenie" in spoken
