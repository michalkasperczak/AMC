from __future__ import annotations

from amc_wx_lite import menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.session_order import (
    AVAILABLE_TEST_SESSIONS,
    MAX_ACTIVE_SESSIONS,
    action_for_slot,
    adjacent,
    disable,
    enable,
    manager_labels,
    migrate_legacy,
    move,
    normalize,
    selection_labels,
    shortcut_for_slot,
    slot_for_session,
    slot_for_key,
    values,
)
from amc_wx_lite.shortcuts import Action, describe


def test_fresh_profile_has_three_compact_sessions() -> None:
    assert values(None) == ["files", "radio", "podcasts"]
    assert action_for_slot(1, None) is Action.SESSION_FILES
    assert action_for_slot(2, None) is Action.SESSION_RADIO
    assert action_for_slot(3, None) is Action.SESSION_PODCASTS
    assert action_for_slot(4, None) is None
    assert slot_for_session(SessionId.SPOTIFY, None) is None


def test_custom_active_order_does_not_reenable_missing_sessions() -> None:
    raw = ["sonos", "radio", "files", "sonos", "technical", 7]

    assert normalize(raw) == (SessionId.SONOS, SessionId.RADIO, SessionId.FILES)
    assert slot_for_session(SessionId.FILES, raw) == 3
    assert action_for_slot(1, raw) is Action.SESSION_SONOS
    assert selection_labels(raw) == (
        "Ctrl+1, Sonos",
        "Ctrl+2, Radio",
        "Ctrl+3, Pliki lokalne",
    )
    assert all("SessionId" not in label for label in selection_labels(raw))


def test_legacy_migration_preserves_order_and_appends_known_sessions_once() -> None:
    migrated = migrate_legacy(["sonos", "radio", "sonos", "obca-wartosc"])
    assert migrated[:2] == (SessionId.SONOS, SessionId.RADIO)
    assert len(migrated) == MAX_ACTIVE_SESSIONS
    assert set(migrated).issubset(set(SessionId))
    assert SessionId.APPLE_MUSIC in AVAILABLE_TEST_SESSIONS


def test_tenth_session_uses_ctrl_zero_without_dropping_apple_music() -> None:
    first_nine = [
        session.value
        for session in SessionId
        if session is not SessionId.APPLE_MUSIC
    ]

    enabled, message = enable(SessionId.APPLE_MUSIC, first_nine)

    assert len(enabled) == MAX_ACTIVE_SESSIONS == 10
    assert enabled[-1] is SessionId.APPLE_MUSIC
    assert message == "Włączono sesję Apple Music, Ctrl+0"
    assert shortcut_for_slot(10) == "Ctrl+0"
    assert slot_for_key("0") == 10
    assert action_for_slot(10, enabled) is Action.SESSION_APPLE_MUSIC
    assert adjacent(SessionId.RADIOARCHIWISTA, 1, enabled) is SessionId.APPLE_MUSIC
    assert dict(manager_labels(enabled))[SessionId.APPLE_MUSIC].startswith(
        "Ctrl+0, Apple Music, włączona"
    )


def test_enable_disable_reenable_and_move_keep_compact_slots() -> None:
    enabled, message = enable(SessionId.SONOS, ["files", "radio", "podcasts"])
    assert enabled[-1] is SessionId.SONOS
    assert message.endswith("Ctrl+4")

    moved, message = move(SessionId.SONOS, -1, enabled)
    assert moved == (
        SessionId.FILES,
        SessionId.RADIO,
        SessionId.SONOS,
        SessionId.PODCASTS,
    )
    assert message.endswith("Ctrl+3")

    disabled, current, message = disable(
        SessionId.RADIO,
        moved,
        current=SessionId.RADIO,
    )
    assert disabled == (SessionId.FILES, SessionId.SONOS, SessionId.PODCASTS)
    assert current is SessionId.SONOS
    assert message == "Wyłączono sesję Radio"

    reenabled, message = enable(SessionId.RADIO, disabled)
    assert reenabled[-1] is SessionId.RADIO
    assert message.endswith("Ctrl+4")


def test_disable_rejects_last_and_busy_session_without_changing_order() -> None:
    last, current, message = disable(
        SessionId.FILES,
        ["files"],
        current=SessionId.FILES,
    )
    assert last == (SessionId.FILES,)
    assert current is SessionId.FILES
    assert message == "Nie można wyłączyć ostatniej aktywnej sesji"

    busy, current, message = disable(
        SessionId.RADIO,
        ["files", "radio"],
        current=SessionId.FILES,
        blocked=[SessionId.RADIO],
    )
    assert busy == (SessionId.FILES, SessionId.RADIO)
    assert current is SessionId.FILES
    assert "trwa odtwarzanie albo nagrywanie" in message


def test_manager_labels_are_explicit_accessible_and_inactive_rows_have_no_slot() -> None:
    rows = dict(manager_labels(
        ["files", "radio", "podcasts"],
        {
            SessionId.FILES: "Gotowa",
            SessionId.SPOTIFY: "Wymaga zalogowania",
        },
    ))
    assert rows[SessionId.FILES] == "Ctrl+1, Pliki lokalne, włączona, gotowa"
    assert rows[SessionId.SPOTIFY] == "Spotify, wyłączona, wymaga zalogowania"
    joined = " ".join(rows.values())
    assert "SessionId" not in joined
    assert "{" not in joined


def test_adjacent_session_wraps_only_in_active_order() -> None:
    raw = ["wiim", "files", "radio"]
    assert adjacent(SessionId.WIIM, -1, raw) is SessionId.RADIO
    assert adjacent(SessionId.WIIM, 1, raw) is SessionId.FILES
    assert adjacent(SessionId.RADIO, 1, raw) is SessionId.WIIM


def test_active_order_drives_menu_palette_and_help_shortcuts() -> None:
    raw = ["sonos", "radio", "files"]
    view = next(menu for menu in menu_model.build_menus(raw) if menu.title == "&Widok")
    sessions = [
        item for item in view.items
        if item.action in {
            Action.SESSION_FILES,
            Action.SESSION_RADIO,
            Action.SESSION_PODCASTS,
            Action.SESSION_TIDAL,
            Action.SESSION_WIIM,
            Action.SESSION_SONOS,
            Action.SESSION_SPOTIFY,
        }
    ]
    assert [(item.action, item.shortcut) for item in sessions] == [
        (Action.SESSION_SONOS, "Ctrl+1"),
        (Action.SESSION_RADIO, "Ctrl+2"),
        (Action.SESSION_FILES, "Ctrl+3"),
    ]
    palette = {
        entry.action: entry.shortcut
        for entry in entries_for_session(SessionId.FILES, raw)
    }
    assert palette[Action.SESSION_SONOS] == "Ctrl+1"
    assert palette[Action.SESSION_FILES] == "Ctrl+3"
    assert Action.SESSION_SPOTIFY not in palette

    active_actions = {
        Action.SESSION_SONOS,
        Action.SESSION_RADIO,
        Action.SESSION_FILES,
    }
    help_rows = dict(describe(
        available_actions=active_actions,
        shortcut_overrides={
            Action.SESSION_SONOS: "Ctrl+1",
            Action.SESSION_RADIO: "Ctrl+2",
            Action.SESSION_FILES: "Ctrl+3",
        },
    ))
    assert help_rows["Ctrl+1"] == "Sonos"
    assert help_rows["Ctrl+2"] == "Radio"
    assert "Podcasty i YouTube" not in help_rows.values()


def test_tenth_session_has_ctrl_zero_in_menu_palette_and_help() -> None:
    raw = [session.value for session in SessionId]
    view = next(menu for menu in menu_model.build_menus(raw) if menu.title == "&Widok")
    apple = next(item for item in view.items if item.action is Action.SESSION_APPLE_MUSIC)
    assert apple.shortcut == "Ctrl+0"
    palette = {
        entry.action: entry.shortcut
        for entry in entries_for_session(SessionId.FILES, raw)
    }
    assert palette[Action.SESSION_APPLE_MUSIC] == "Ctrl+0"
    help_rows = dict(describe(
        available_actions={Action.SESSION_APPLE_MUSIC},
        shortcut_overrides={Action.SESSION_APPLE_MUSIC: "Ctrl+0"},
    ))
    assert help_rows["Ctrl+0"] == "Apple Music"


def test_session_manager_commands_share_the_view_menu_and_configuration_is_in_files() -> None:
    view = next(menu for menu in menu_model.build_menus() if menu.title == "&Widok")
    by_action = {item.action: item for item in view.items}
    assert by_action[Action.SESSION_LIST].shortcut == "Ctrl+Shift+S"
    assert "Menedżer sesji" in by_action[Action.SESSION_LIST].label
    assert Action.SESSION_ENABLE in by_action
    assert Action.SESSION_DISABLE_CURRENT in by_action
    assert Action.SESSION_CONFIGURE_CURRENT not in by_action
    files = next(menu for menu in menu_model.build_menus() if menu.title == "&Pliki")
    configure = next(
        item for item in files.items
        if item.action is Action.SESSION_CONFIGURE_CURRENT
    )
    assert configure.shortcut == "Ctrl+F5"
    assert configure.visible_sessions
