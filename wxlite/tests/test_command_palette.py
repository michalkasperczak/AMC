"""Paleta udostępnia wyłącznie jawne nazwy istniejących akcji."""

from __future__ import annotations

from amc_wx_lite.command_palette import (
    available_entries,
    count_label,
    entries_for_session,
    filter_entries,
)
from amc_wx_lite.navigation import SessionId
from amc_wx_lite import menu_model
from amc_wx_lite import session_options
from amc_wx_lite.search_catalog import SESSION_LABELS
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    command_presentation,
    ctrl_f5_presentation,
    describe,
    describe_action,
    resolve,
)
from amc_wx_lite.shortcuts import keyboard_help_announcement


def test_palette_is_reachable_from_both_views() -> None:
    chord = Chord("K", ctrl=True, shift=True)
    assert resolve(chord, player_view=False, radio_session=False) is Action.COMMAND_PALETTE
    assert resolve(chord, player_view=True, radio_session=False) is Action.COMMAND_PALETTE


def test_palette_entries_are_unique_user_facing_actions_for_the_session() -> None:
    for session in SessionId:
        entries = entries_for_session(session)
        assert entries
        assert len({entry.action for entry in entries}) == len(entries)
        assert all(entry.label and "Action." not in entry.label for entry in entries)
        assert all("{" not in entry.label for entry in entries)
        assert any(entry.action is Action.COMMAND_PALETTE for entry in entries)

    tidal = entries_for_session(SessionId.TIDAL)
    assert not any(entry.action is Action.RECORD_TOGGLE for entry in tidal)
    radio = entries_for_session(SessionId.RADIO)
    assert any(entry.action is Action.RECORD_TOGGLE for entry in radio)


def test_palette_filter_is_accent_insensitive_and_requires_every_word() -> None:
    entries = entries_for_session(SessionId.RADIO)
    result = filter_entries(entries, "paleta polecen")
    assert [entry.action for entry in result] == [Action.COMMAND_PALETTE]
    assert filter_entries(entries, "nieistniejące polecenie") == ()


def test_palette_can_follow_current_view_availability() -> None:
    entries = entries_for_session(SessionId.FILES)
    result = available_entries(
        entries,
        {Action.COMMAND_PALETTE, Action.QUICK_INFORMATION},
    )

    assert {entry.action for entry in result} == {
        Action.COMMAND_PALETTE,
        Action.QUICK_INFORMATION,
    }
    assert len(result) == 2


def test_opcje_sesji_maja_jeden_kontrakt_menu_pomocy_i_palety() -> None:
    expected = (
        "Ustawienia sesji: uruchamianie materiału, pozycja, domyślna prędkość, "
        "dźwięk i zachowanie po wyjściu, zależnie od możliwości sesji"
    )
    chord = Chord("Return", ctrl=True, alt=True)
    assert resolve(chord, player_view=False, radio_session=False) is Action.SESSION_OPTIONS
    assert dict(describe(SESSION_LABELS[SessionId.FILES], {Action.SESSION_OPTIONS}))[
        "Ctrl+Alt+Enter"
    ] == expected
    assert keyboard_help_announcement(
        chord, Action.SESSION_OPTIONS, SESSION_LABELS[SessionId.FILES]
    ) == f"Ctrl+Alt+Enter. {expected}. {SESSION_LABELS[SessionId.FILES]}"

    for session in SessionId:
        if not session_options.capabilities_for(session).has_options:
            assert not any(
                item.action is Action.SESSION_OPTIONS
                for item in entries_for_session(session)
            )
            continue
        menu_entry = next(
            item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if item.action is Action.SESSION_OPTIONS
        )
        palette_entry = next(
            item
            for item in entries_for_session(session)
            if item.action is Action.SESSION_OPTIONS
        )
        assert menu_entry.shortcut == "Ctrl+Alt+Return"
        assert palette_entry.shortcut == "Ctrl+Alt+Enter"
        assert palette_entry.name == "Ustawienia sesji…"


def test_global_stop_recordings_has_one_contract_in_every_session() -> None:
    expected = "Zatrzymaj wszystkie nagrania radia"
    chord = Chord("R", ctrl=True, alt=True, shift=True)

    for session in SessionId:
        assert resolve(
            chord,
            player_view=False,
            radio_session=session is SessionId.RADIO,
        ) is Action.RECORD_STOP_ALL

        menu_entry = next(
            item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if item.action is Action.RECORD_STOP_ALL
        )
        palette_entry = next(
            item
            for item in entries_for_session(session)
            if item.action is Action.RECORD_STOP_ALL
        )
        assert menu_entry.shortcut == "Ctrl+Alt+Shift+R"
        assert palette_entry.shortcut == "Ctrl+Alt+Shift+R"
        assert palette_entry.name == "Zatrzymaj wszystkie nagrania"
        assert dict(describe(
            SESSION_LABELS[session],
            {Action.RECORD_STOP_ALL},
        ))["Ctrl+Alt+Shift+R"] == expected
        assert keyboard_help_announcement(
            chord,
            Action.RECORD_STOP_ALL,
            SESSION_LABELS[session],
        ) == f"Ctrl+Alt+Shift+R. {expected}. {SESSION_LABELS[session]}"


def test_recognition_history_has_one_contract_in_files_and_radio_only() -> None:
    expected = (
        "Pokaż rozpoznane utwory; Enter otwiera utwór, Ctrl+Enter album, "
        "a prawa strzałka najważniejsze działania"
    )
    chord = Chord("S", ctrl=True, alt=True)
    supported = {SessionId.FILES, SessionId.RADIO}

    for session in SessionId:
        context = {
            "radio_session": session is SessionId.RADIO,
            "files_session": session is SessionId.FILES,
            "podcast_session": session is SessionId.PODCASTS,
            "tidal_session": session is SessionId.TIDAL,
            "spotify_session": session is SessionId.SPOTIFY,
            "wiim_session": session is SessionId.WIIM,
            "sonos_session": session is SessionId.SONOS,
            "frontier_smart_session": session is SessionId.FRONTIER_SMART,
        }
        menu_entries = [
            item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if item.action is Action.VIEW_RADIO_RECOGNITION_HISTORY
        ]
        palette_entries = [
            item
            for item in entries_for_session(session)
            if item.action is Action.VIEW_RADIO_RECOGNITION_HISTORY
        ]

        if session not in supported:
            assert not menu_entries
            assert not palette_entries
            for player_view in (False, True):
                assert resolve(
                    chord,
                    player_view=player_view,
                    **context,
                ) is None
            assert keyboard_help_announcement(
                chord,
                None,
                SESSION_LABELS[session],
            ) == (
                f"Ctrl+Alt+S. Brak polecenia w sesji "
                f"{SESSION_LABELS[session]}"
            )
            continue

        assert len(menu_entries) == 1
        assert len(palette_entries) == 1
        assert menu_entries[0].shortcut == "Ctrl+Alt+S"
        assert palette_entries[0].shortcut == "Ctrl+Alt+S"
        assert palette_entries[0].name == "Rozpoznane utwory…"
        assert dict(describe(
            SESSION_LABELS[session],
            {Action.VIEW_RADIO_RECOGNITION_HISTORY},
        ))["Ctrl+Alt+S"] == expected
        assert keyboard_help_announcement(
            chord,
            Action.VIEW_RADIO_RECOGNITION_HISTORY,
            SESSION_LABELS[session],
        ) == f"Ctrl+Alt+S. {expected}. {SESSION_LABELS[session]}"
        for player_view in (False, True):
            assert resolve(
                chord,
                player_view=player_view,
                **context,
            ) is Action.VIEW_RADIO_RECOGNITION_HISTORY


def test_palette_count_labels_are_intentional_polish_text() -> None:
    assert count_label(0) == "Brak pasujących poleceń"
    assert count_label(1) == "1 polecenie"
    assert count_label(3) == "3 polecenia"
    assert count_label(5) == "5 poleceń"


def test_palette_shortcuts_never_expose_internal_key_names() -> None:
    labels = [entry.label for entry in entries_for_session(SessionId.FILES)]
    assert any("Page Down" in label for label in labels)
    assert all("Prior" not in label and "Next" not in label for label in labels)
    assert all("Page strzałka" not in label for label in labels)


def test_ctrl_f5_has_one_precise_contextual_contract_on_every_surface() -> None:
    expected = {
        SessionId.FILES: (
            Action.MANAGE_LIBRARY_FOLDERS,
            "Foldery Biblioteki",
            "Foldery Biblioteki: dodaj, odśwież albo bezpiecznie odłącz folder",
            "files_session",
        ),
        SessionId.RADIO: (
            Action.REFRESH_RADIO_STATIONS,
            "Odśwież stacje Radia",
            "Odśwież stacje i bieżący widok Radia",
            "radio_session",
        ),
        SessionId.PODCASTS: (
            Action.REFRESH_PODCAST_LIBRARY,
            "Odśwież wszystkie podcasty, kanały YouTube i playlisty",
            "Odśwież wszystkie podcasty, kanały YouTube i playlisty",
            "podcast_session",
        ),
        SessionId.SPOTIFY: (
            Action.SESSION_CONFIGURE_CURRENT,
            "Konto Spotify",
            "Otwórz konto Spotify, ustaw identyfikator aplikacji, zaloguj katalog lub połącz odtwarzacz",
            "spotify_session",
        ),
        SessionId.TIDAL: (
            Action.SESSION_CONFIGURE_CURRENT,
            "Konto i synchronizacja TIDAL",
            "Otwórz konto, logowanie i synchronizację TIDAL",
            "tidal_session",
        ),
        SessionId.WIIM: (
            Action.SESSION_CONFIGURE_CURRENT,
            "Urządzenia WiiM",
            "Odśwież listę zapisanych urządzeń WiiM",
            "wiim_session",
        ),
        SessionId.SONOS: (
            Action.SELECT_SONOS_TARGET,
            "Głośniki i grupy Sonos",
            "Wybierz dom i grupę sterowania Sonos; przy braku połączenia otwórz konto Sonos",
            "sonos_session",
        ),
    }
    chord = Chord("F5", ctrl=True)

    for session, (action, name, description, session_flag) in expected.items():
        presentation = ctrl_f5_presentation(SESSION_LABELS[session])
        assert presentation is not None
        assert presentation.action is action
        assert presentation.name == name
        assert presentation.description == description
        assert presentation.shortcut == "Ctrl+F5"

        arguments = {
            "player_view": False,
            "radio_session": False,
            session_flag: True,
        }
        assert resolve(chord, **arguments) is action

        menu_entries = [
            item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if item.shortcut == "Ctrl+F5"
        ]
        assert len(menu_entries) == 1
        assert menu_entries[0].action is action
        assert menu_entries[0].label.replace("&", "").rstrip("…") == name

        palette_entry = next(
            item
            for item in entries_for_session(session)
            if item.action is action and item.shortcut == "Ctrl+F5"
        )
        assert palette_entry.name == name
        assert dict(describe(SESSION_LABELS[session], {action}))["Ctrl+F5"] == description
        assert keyboard_help_announcement(
            chord,
            action,
            SESSION_LABELS[session],
        ) == f"Ctrl+F5. {description}. {SESSION_LABELS[session]}"


def test_podcast_ctrl_n_has_one_command_contract_on_every_surface() -> None:
    expected = "Dodaj podcast, SoundCloud, kanał YouTube, stronę Facebooka lub medium internetowe"
    chord = Chord("N", ctrl=True)
    assert resolve(
        chord,
        player_view=False,
        radio_session=False,
        podcast_session=True,
    ) is Action.ADD_PODCAST_SOURCE

    menu_entry = next(
        item
        for menu in menu_model.menus_for_session(SessionId.PODCASTS)
        for item in menu.items
        if item.action is Action.ADD_PODCAST_SOURCE
    )
    palette_entry = next(
        item
        for item in entries_for_session(SessionId.PODCASTS)
        if item.action is Action.ADD_PODCAST_SOURCE
    )
    assert menu_entry.label.replace("&", "") == expected
    assert menu_entry.shortcut == palette_entry.shortcut == "Ctrl+N"
    assert palette_entry.name == expected
    assert dict(describe("Podcasty i YouTube", {Action.ADD_PODCAST_SOURCE}))[
        "Ctrl+N"
    ] == expected
    assert keyboard_help_announcement(
        chord,
        Action.ADD_PODCAST_SOURCE,
        "Podcasty i YouTube",
    ) == f"Ctrl+N. {expected}. Podcasty i YouTube"
    assert all(
        not any(
            item.action is Action.ADD_PODCAST_SOURCE
            for item in entries_for_session(session)
        )
        for session in SessionId
        if session is not SessionId.PODCASTS
    )

def test_recording_views_have_one_user_facing_command_contract() -> None:
    entries = {entry.action: entry for entry in entries_for_session(SessionId.RADIO)}

    active = entries[Action.VIEW_ACTIVE_RECORDINGS]
    history = entries[Action.VIEW_RECORDED_RADIO_FILES]
    assert active.name == "Pokaż aktualnie nagrywane pliki"
    assert active.shortcut == "Alt+R"
    assert describe_action(Action.VIEW_ACTIVE_RECORDINGS) == active.name
    assert history.name == "Historia nagrywania"
    assert history.shortcut == "Alt+Shift+R"
    assert describe_action(Action.VIEW_RECORDED_RADIO_FILES) == history.name
    assert resolve(
        Chord("R", alt=True),
        player_view=False,
        radio_session=True,
    ) is Action.VIEW_ACTIVE_RECORDINGS
    assert resolve(
        Chord("R", alt=True, shift=True),
        player_view=False,
        radio_session=True,
    ) is Action.VIEW_RECORDED_RADIO_FILES


def test_podcast_inbox_command_explains_repeated_ctrl_i_everywhere() -> None:
    presentation = command_presentation(Action.VIEW_PODCAST_INBOX)
    assert presentation is not None
    assert presentation.name == "Nowe odcinki i materiały"
    assert presentation.shortcut == "Ctrl+I"
    assert "kolejne 150 pozycji" in presentation.description

    menu_entry = next(
        item
        for menu in menu_model.menus_for_session(SessionId.PODCASTS)
        for item in menu.items
        if item.action is Action.VIEW_PODCAST_INBOX
    )
    palette_entry = next(
        item
        for item in entries_for_session(SessionId.PODCASTS)
        if item.action is Action.VIEW_PODCAST_INBOX
    )
    assert menu_entry.label.replace("&", "") == presentation.name
    assert menu_entry.shortcut == palette_entry.shortcut == presentation.shortcut
    assert palette_entry.name == presentation.name
    assert dict(describe("Podcasty i YouTube", {Action.VIEW_PODCAST_INBOX}))[
        "Ctrl+I"
    ] == presentation.description
    assert keyboard_help_announcement(
        Chord("I", ctrl=True),
        Action.VIEW_PODCAST_INBOX,
        "Podcasty i YouTube",
    ) == f"Ctrl+I. {presentation.description}. Podcasty i YouTube"


def test_schedule_manager_name_and_shortcut_match_palette_and_keyboard_help() -> None:
    entries = {entry.action: entry for entry in entries_for_session(SessionId.RADIO)}
    schedule = entries[Action.MANAGE_RADIO_SCHEDULES]
    assert schedule.name == "Harmonogram nagrywania"
    assert schedule.shortcut == "Ctrl+Shift+H"
    assert describe_action(Action.MANAGE_RADIO_SCHEDULES) == schedule.name


def test_current_radio_information_has_one_name_in_every_command_surface() -> None:
    entries = {entry.action: entry for entry in entries_for_session(SessionId.RADIO)}
    information = entries[Action.CURRENT_RADIO_BROADCAST_INFORMATION]
    expected = "Informacje o bieżącym odtwarzaniu"
    assert information.name == expected
    assert information.shortcut == "Alt+D"
    assert describe_action(Action.CURRENT_RADIO_BROADCAST_INFORMATION) == expected
    assert dict(describe("Radio"))["Alt+D"] == expected
    message = keyboard_help_announcement(
        Chord("D", alt=True),
        Action.CURRENT_RADIO_BROADCAST_INFORMATION,
        "Radio",
    )
    assert expected in message and "Alt+D" in message


def test_spotify_playlist_command_matches_palette_and_contextual_keyboard_help() -> None:
    entries = {entry.action: entry for entry in entries_for_session(SessionId.SPOTIFY)}
    playlist = entries[Action.MANAGE_PLAYLIST_MEMBERSHIP]
    assert playlist.name == "Dodaj do playlist Spotify lub utwórz playlistę…"
    assert playlist.shortcut == "Ctrl+Shift+P"
    message = keyboard_help_announcement(
        Chord("P", ctrl=True, shift=True),
        Action.MANAGE_PLAYLIST_MEMBERSHIP,
        "Spotify",
    )
    assert "Dodaj do playlist Spotify lub utwórz playlistę" in message
    assert "Ctrl+Shift+P" in message
    assert dict(describe("Spotify"))["Ctrl+Shift+P"] == (
        "Dodaj do playlist Spotify lub utwórz playlistę"
    )
