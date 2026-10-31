from __future__ import annotations

import inspect
from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite import menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId, View
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    GLOBAL_VIEW_SHORTCUTS,
    SONOS_SUPPORTED_ACTIONS,
    SPOTIFY_SUPPORTED_ACTIONS,
    WIIM_SUPPORTED_ACTIONS,
    FRONTIER_SMART_SUPPORTED_ACTIONS,
    RADIOARCHIWISTA_SUPPORTED_ACTIONS,
    describe,
    keyboard_help_announcement,
    resolve,
)
from amc_wx_lite.state_store import LiteState
from amc_wx_lite.transient_preview_policy import (
    TRANSIENT_PREVIEW_ACTIONS,
    is_transient_preview_available,
    load_global_transient_previews,
    resolve_global_transient_previews,
    transient_preview_unavailable_message,
)


def test_global_mode_exposes_all_three_previews_in_every_session() -> None:
    for action in TRANSIENT_PREVIEW_ACTIONS:
        for session in SessionId:
            assert is_transient_preview_available(
                action, session, global_previews=True
            )


def test_global_preview_shortcuts_reach_dispatch_from_native_lists() -> None:
    """A mapping-only test misses keys swallowed by the Windows list."""

    frame_hook = inspect.getsource(LiteFrame._on_player_shortcut_hook)
    list_hook = inspect.getsource(LiteFrame._on_list_shortcut_hook)
    assert set(GLOBAL_VIEW_SHORTCUTS) == {
        "Ctrl+I", "Ctrl+Shift+I", "Alt+R", "Alt+Shift+R", "Ctrl+H", "Ctrl+J", "Ctrl+L",
    }
    # Obie bramki korzystaja z jednej tabeli zamiast kopiowac gesty do
    # adapterow. Sama nazwa tabeli w kodzie jest kontraktem tej sciezki.
    assert "GLOBAL_VIEW_SHORTCUTS" in frame_hook
    assert "GLOBAL_VIEW_SHORTCUTS" in list_hook


def test_global_view_shortcuts_resolve_before_provider_tables() -> None:
    cases = (
        (Chord("H", ctrl=True), Action.VIEW_HISTORY),
        (Chord("J", ctrl=True), Action.VIEW_DOWNLOADED_FILES),
        (Chord("L", ctrl=True), Action.VIEW_LIBRARY),
        (Chord("R", alt=True), Action.VIEW_ACTIVE_RECORDINGS),
        (Chord("R", alt=True, shift=True), Action.VIEW_RECORDED_RADIO_FILES),
    )
    sessions = tuple(SessionId)
    for session in sessions:
        for player in (False, True):
            for chord, expected in cases:
                assert resolve(
                    chord,
                    player_view=player,
                    radio_session=session is SessionId.RADIO,
                    podcast_session=session is SessionId.PODCASTS,
                    wiim_session=session is SessionId.WIIM,
                    sonos_session=session is SessionId.SONOS,
                    frontier_smart_session=session is SessionId.FRONTIER_SMART,
                    radioarchiwista_session=session is SessionId.RADIOARCHIWISTA,
                ) is expected


def test_ctrl_h_opens_once_and_escape_restores_origin_and_selected_item() -> None:
    origins = (
        SessionId.RADIO,
        SessionId.PODCASTS,
        SessionId.WIIM,
        SessionId.SONOS,
    )
    for origin in origins:
        navigator = Navigator()
        navigator.active = origin
        original_rows = [
            Row("pierwszy", "Pierwszy", "track"),
            Row("wybrany", "Wybrany", "track"),
        ]
        navigator.session.model.replace(original_rows, preferred_id="wybrany")
        opened: list[SessionId] = []
        syncs: list[str] = []
        frame = SimpleNamespace(
            navigator=navigator,
            state=LiteState(playback_history_by_session={
                origin.value: [{
                    "itemId": f"{origin.value}:observed:test",
                    "title": "Historia streamera",
                    "kind": "track",
                    "replaySupported": False,
                }]
            }),
            _transient_preview_return=None,
            _sync_views=lambda: syncs.append("focus"),
        )
        frame._begin_transient_preview = (
            lambda current=frame: LiteFrame._begin_transient_preview(current)
        )

        def open_radio(_intent, **kwargs) -> None:
            assert kwargs["announce_single_item"] is True
            opened.append(SessionId.RADIO)
            navigator.apply_radio_view(
                LibraryView.HISTORY,
                "Radio — Historia odtwarzania",
                [Row("historia", "Historia", "station")],
            )

        def open_podcasts(_intent, **kwargs) -> None:
            assert kwargs["announce_single_item"] is True
            opened.append(SessionId.PODCASTS)
            navigator.apply_podcast_aggregate(
                LibraryView.PODCAST_HISTORY,
                "Historia odtwarzania",
                [Row("historia", "Historia", "episode")],
            )

        def open_files(_intent, **kwargs) -> None:
            assert kwargs["announce_single_item"] is True
            opened.append(SessionId.FILES)
            navigator.apply_library_view(
                LibraryView.HISTORY,
                "Biblioteka — Historia odtwarzania",
                [Row("historia", "Historia", "track")],
            )

        frame._open_radio_view = open_radio
        frame._open_podcast_aggregate = open_podcasts
        frame._open_library_view = open_files
        frame._run = lambda _events: opened.append(origin)
        frame._announce_transient_preview = lambda _events: opened.append(origin)

        LiteFrame._show_playback_history_preview(frame)

        assert opened == [origin]
        if origin in (SessionId.WIIM, SessionId.SONOS):
            assert navigator.active is origin
            assert navigator.session.library_view is LibraryView.SERVICE_HISTORY
            assert navigator.session.model.selected_row.title == (
                "Historia streamera"
            )
        assert LiteFrame._restore_transient_preview(frame, announce=False)
        assert navigator.active is origin
        assert navigator.session.view is View.LIST
        assert navigator.session.model.selected_id == "wybrany"
        assert syncs == ["focus"]
        assert not LiteFrame._restore_transient_preview(frame, announce=False)


def test_repeated_ctrl_i_loads_next_batch_and_preserves_preview_origin() -> None:
    navigator = Navigator()
    navigator.active = SessionId.RADIO
    navigator.session.model.replace(
        [Row("station", "Wybrana stacja", "station")],
        preferred_id="station",
    )
    opened = []
    frame = SimpleNamespace(
        navigator=navigator,
        _transient_preview_return=None,
        _sync_views=lambda: None,
        _open_podcast_aggregate=lambda intent, **_kwargs: opened.append(intent),
    )
    frame._begin_transient_preview = (
        lambda: LiteFrame._begin_transient_preview(frame)
    )

    LiteFrame._show_podcast_inbox(frame)

    assert len(opened) == 1
    assert opened[0].view is LibraryView.PODCAST_INBOX
    assert not opened[0].load_more
    assert navigator.active is SessionId.PODCASTS
    assert navigator.session.view is View.LIST
    assert navigator.session.library_view is LibraryView.PODCAST_INBOX
    origin = frame._transient_preview_return
    assert origin is not None
    assert origin.session_id is SessionId.RADIO

    LiteFrame._show_podcast_inbox(frame)

    assert len(opened) == 2
    assert opened[1].load_more
    assert frame._transient_preview_return is origin


def test_remote_provider_gates_allow_the_global_history_action() -> None:
    for supported in (
        WIIM_SUPPORTED_ACTIONS, SONOS_SUPPORTED_ACTIONS,
        SPOTIFY_SUPPORTED_ACTIONS,
        FRONTIER_SMART_SUPPORTED_ACTIONS, RADIOARCHIWISTA_SUPPORTED_ACTIONS,
    ):
        assert Action.VIEW_HISTORY in supported
        assert Action.VIEW_ACTIVE_RECORDINGS in supported
        assert Action.VIEW_RECORDED_RADIO_FILES in supported
    assert Action.REMOVE_SELECTED in WIIM_SUPPORTED_ACTIONS
    assert Action.REMOVE_SELECTED in SONOS_SUPPORTED_ACTIONS


def test_recording_and_playback_history_are_global_even_with_option_off() -> None:
    for session in SessionId:
        for action in (
            Action.VIEW_ACTIVE_RECORDINGS,
            Action.VIEW_RECORDED_RADIO_FILES,
            Action.VIEW_HISTORY,
        ):
            assert is_transient_preview_available(
                action, session, global_previews=False
            )
    assert is_transient_preview_available(
        Action.VIEW_PODCAST_INBOX, SessionId.PODCASTS, global_previews=False
    )
    assert not is_transient_preview_available(
        Action.VIEW_PODCAST_INBOX, SessionId.TIDAL, global_previews=False
    )


def test_unavailable_messages_name_user_facing_home_sessions() -> None:
    assert "Podcasty i YouTube" in transient_preview_unavailable_message(
        Action.VIEW_PODCAST_INBOX
    )


def test_profile_value_is_strict_and_private_override_wins(tmp_path: Path) -> None:
    state = tmp_path / "state.json"
    assert load_global_transient_previews(state) is True
    state.write_text(
        '{"settings":{"globalTransientPreviews":false}}', encoding="utf-8"
    )
    assert load_global_transient_previews(state) is False
    assert resolve_global_transient_previews(None, False) is False
    assert resolve_global_transient_previews(True, False) is True

    state.write_text(
        '{"settings":{"globalTransientPreviews":"false"}}', encoding="utf-8"
    )
    assert load_global_transient_previews(state) is True


def test_recording_views_execute_from_every_session_with_option_off() -> None:
    for session in SessionId:
        calls: list[str] = []
        frame = SimpleNamespace(
            navigator=Navigator(),
            _global_transient_previews=False,
            messages=SimpleNamespace(custom_seek_seconds=300),
            announcer=SimpleNamespace(say=lambda message: calls.append(message)),
            _show_active_radio_recordings=lambda: calls.append("active"),
            _show_radio_recording_history=lambda: calls.append("recorded"),
        )
        frame.navigator.active = session
        LiteFrame._dispatch(frame, Action.VIEW_ACTIVE_RECORDINGS)
        LiteFrame._dispatch(frame, Action.VIEW_RECORDED_RADIO_FILES)
        assert calls == ["active", "recorded"], session


def test_recording_and_history_commands_match_menu_and_palette_in_every_session() -> None:
    shortcuts = {
        Action.VIEW_ACTIVE_RECORDINGS: "Alt+R",
        Action.VIEW_RECORDED_RADIO_FILES: "Alt+Shift+R",
        Action.VIEW_HISTORY: "Ctrl+H",
    }
    for session in SessionId:
        menu_items = {
            item.action: item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
        }
        palette_items = {
            entry.action: entry for entry in entries_for_session(session)
        }
        descriptions = dict(describe(session.value, set(shortcuts)))
        for action, shortcut in shortcuts.items():
            assert action in menu_items, (session, action)
            assert action in palette_items, (session, action)
            name = menu_items[action].label.replace("&", "")
            assert menu_items[action].shortcut == shortcut
            assert palette_items[action].name == name
            assert palette_items[action].shortcut == shortcut
            assert descriptions[shortcut] == name
            assert keyboard_help_announcement(
                Chord("H", ctrl=True) if action is Action.VIEW_HISTORY
                else Chord("R", alt=True, shift=action is Action.VIEW_RECORDED_RADIO_FILES),
                action,
                session.value,
            ) == f"{shortcut}. {name}. {session.value}"


def test_recording_previews_from_archive_restore_its_selection(monkeypatch) -> None:
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    navigator.session.model.replace([
        Row("first", "Pierwsze nagranie", "recording"),
        Row("chosen", "Wybrane nagranie", "recording"),
    ], preferred_id="chosen")
    client = SimpleNamespace(
        radio_recording_status=lambda: {"recordings": []},
        radio_recording_history=lambda: {"recordings": []},
    )
    frame = SimpleNamespace(
        navigator=navigator,
        client=client,
        state=LiteState(),
        runner=SimpleNamespace(submit=lambda _name, work, done, _failed: done(work())),
        radio=SimpleNamespace(load=lambda **_kwargs: SimpleNamespace(
            load_error="", list=[],
        )),
        _radio_snapshot=None,
        _transient_preview_return=None,
        _announce_transient_preview=lambda _events: None,
        _sync_views=lambda: None,
        announcer=SimpleNamespace(say=lambda _message: None),
    )
    frame._begin_transient_preview = (
        lambda: LiteFrame._begin_transient_preview(frame)
    )
    monkeypatch.setattr(
        LiteFrame, "_with_radio_recording_options",
        lambda _self, snapshot: snapshot,
    )

    for show, target_session, target_view in (
        (LiteFrame._show_active_radio_recordings,
         SessionId.RADIO, LibraryView.ACTIVE_RADIO_RECORDINGS),
        (LiteFrame._show_radio_recording_history,
         SessionId.FILES, LibraryView.RECORDED_RADIO_FILES),
    ):
        show(frame)
        assert navigator.active is target_session
        assert navigator.session.library_view is target_view
        assert LiteFrame._restore_transient_preview(frame, announce=False)
        assert navigator.active is SessionId.RADIOARCHIWISTA
        assert navigator.session.model.selected_id == "chosen"


def test_window_runs_preview_in_its_home_session_when_global_mode_is_off() -> None:
    calls: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIO),
        _global_transient_previews=False,
        announcer=SimpleNamespace(say=lambda _text: None),
        messages=SimpleNamespace(custom_seek_seconds=300),
        _show_active_radio_recordings=lambda: calls.append("active"),
    )

    LiteFrame._dispatch(frame, Action.VIEW_ACTIVE_RECORDINGS)

    assert calls == ["active"]
