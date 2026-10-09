"""First visible TIDAL session: shortcuts, state and honest activation."""

from __future__ import annotations

from pathlib import Path

from amc_wx_lite import menu_model
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenLibraryView,
    OpenTidalContainer,
    PlayTidalTrack,
    SessionId,
)
from amc_wx_lite.shortcuts import Action, Chord, TIDAL_SUPPORTED_ACTIONS, resolve


def test_ctrl_4_selects_tidal_from_list_and_player() -> None:
    chord = Chord("4", ctrl=True)
    assert resolve(chord, player_view=False, radio_session=False) is Action.SESSION_TIDAL
    assert resolve(chord, player_view=True, radio_session=False) is Action.SESSION_TIDAL

    view = next(menu for menu in menu_model.build_menus() if "Widok" in menu.title)
    item = next(entry for entry in view.items if entry.action is Action.SESSION_TIDAL)
    assert item.label == "Sesja: &TIDAL"
    assert item.shortcut == "Ctrl+4"


def test_tidal_session_keeps_its_own_selection_across_switches() -> None:
    nav = Navigator()
    rows = [
        Row("album-1", "Album pierwszy", "album"),
        Row("album-2", "Album drugi", "album"),
    ]
    nav.apply_tidal_view(LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", rows)
    nav.sessions[SessionId.TIDAL].model.select_id("album-2")

    nav.switch_session(SessionId.TIDAL)
    nav.switch_session(SessionId.FILES)
    events = nav.switch_session(SessionId.TIDAL)

    assert nav.session.model.selected_id == "album-2"
    assert isinstance(events[0], Announce)
    assert events[0].text == "TIDAL, lista, Album drugi"


def test_tidal_view_announcement_never_uses_technical_ids() -> None:
    nav = Navigator()
    events = nav.apply_tidal_view(
        LibraryView.TIDAL_FAVORITES,
        "Ulubione TIDAL",
        [Row("opaque-service-id", "Piosenka", "track")],
        order_matches_amc=False,
    )
    assert events == [Announce("Ulubione TIDAL, 1 pozycja, kolejność zastępcza")]
    assert "opaque-service-id" not in events[0].text


def test_tidal_playlist_activation_does_not_open_a_local_playlist() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_view(
        LibraryView.TIDAL_PLAYLISTS,
        "Playlisty TIDAL",
        [Row(
            "playlist-service-id",
            "Moja playlista",
            "playlist",
            activation_message="Katalog online nie jest jeszcze podłączony",
        )],
    )

    events = nav.activate_selected()
    assert events == [Announce("Katalog online nie jest jeszcze podłączony")]
    assert not any(isinstance(event, OpenLibraryView) for event in events)


def test_tidal_album_activation_requests_the_shared_online_catalog() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_view(
        LibraryView.TIDAL_LIBRARY,
        "Biblioteka TIDAL",
        [Row(
            "tidal:albums:123",
            "Album próby",
            "album",
            service_id="albums:123",
            service_kind="album",
            artist_name="Wykonawca",
        )],
    )

    events = nav.activate_selected()
    assert events == [OpenTidalContainer(
        item_id="tidal:albums:123",
        service_id="albums:123",
        title="Album próby",
        kind="album",
        artist="Wykonawca",
    )]
    assert not any(isinstance(event, OpenLibraryView) for event in events)


def test_tidal_artist_categories_and_backspace_restore_exact_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    artist = Row(
        "tidal:artists:7",
        "Artysta",
        "artist",
        service_id="artists:7",
        service_kind="artist",
    )
    nav.apply_tidal_view(LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", [artist])

    events = nav.activate_selected()
    assert events == [Announce("Wykonawca, Artysta, 3 pozycje")]
    assert [row.title for row in nav.session.model.rows] == [
        "Albumy", "Utwory", "Podobni wykonawcy"
    ]
    assert "artists:7" not in events[0].text

    section = nav.activate_selected()[0]
    assert isinstance(section, OpenTidalContainer)
    assert section.artist_section == "albums"
    assert section.title == "Artysta"

    assert nav.go_to_parent() == []
    assert nav.session.model.selected_id == "tidal:artists:7"
    assert nav.session.model.selected_row == artist


def test_tidal_container_survives_session_switch_and_backspace() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    album = Row(
        "tidal:albums:123",
        "Album próby",
        "album",
        service_id="albums:123",
        service_kind="album",
    )
    nav.apply_tidal_view(LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", [album])
    intent = nav.activate_selected()[0]
    assert isinstance(intent, OpenTidalContainer)
    nav.apply_tidal_container(
        intent,
        "Album, Album próby",
        [Row("tidal:tracks:1", "Utwór", "track")],
    )

    nav.switch_session(SessionId.FILES)
    nav.switch_session(SessionId.TIDAL)
    assert nav.session.library_view is LibraryView.TIDAL_CONTAINER
    assert nav.session.model.selected_row.title == "Utwór"

    assert nav.go_to_parent() == []
    assert nav.session.library_view is LibraryView.TIDAL_LIBRARY
    assert nav.session.model.selected_row == album


def test_backspace_at_tidal_collection_root_is_silent_and_stays_in_tidal() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_view(LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", [])
    assert nav.go_to_parent() == []
    assert nav.active is SessionId.TIDAL


def test_enter_on_tidal_track_hands_it_to_original_tidal_and_keeps_source_order() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    tracks = [
        Row(
            f"tidal:tracks:{number}",
            f"Utwór {number}",
            "track",
            service_id=f"tracks:{number}",
            service_kind="track",
            related_album_service_id="albums:44",
        )
        for number in (1, 2)
    ]
    nav.apply_tidal_container(
        OpenTidalContainer("album", "albums:44", "Album", "album"),
        "Album, Album",
        tracks,
    )

    events = nav.activate_selected()

    assert events == [PlayTidalTrack(
        item_id="tidal:tracks:1",
        title="Utwór 1",
        service_id="tracks:1",
        related_album_service_id="albums:44",
    )]
    assert nav.session.playback_source_rows == tuple(tracks)
    # Odtwarzanie nalezy do oryginalnego TIDALa. AMC pozostaje na liscie
    # zamiast otwierac sztuczny widok lokalnego odtwarzacza.
    assert nav.session.view.value == "list"


def test_tidal_next_uses_the_visible_amc_list_not_an_internal_audio_queue() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    tracks = [
        Row(
            f"tidal:tracks:{number}", f"Utwór {number}", "track",
            service_id=f"tracks:{number}", service_kind="track",
            related_album_service_id=f"albums:{number}",
        )
        for number in (1, 2)
    ]
    nav.apply_tidal_view(LibraryView.TIDAL_FAVORITES, "Ulubione TIDAL", tracks)
    nav.activate_selected()
    nav.note_tidal_playback_started("tidal:tracks:1", "Utwór 1")

    events = nav.step_playback_source(True)

    assert events == [PlayTidalTrack(
        "tidal:tracks:2", "Utwór 2", "tracks:2", "albums:2",
        previous_item_id="tidal:tracks:1",
        previous_title="Utwór 1",
    )]
    # Dopiero potwierdzenie z kontrolera zmienia bieżący element.
    assert nav.session.now_playing_id == "tidal:tracks:1"


def test_tidal_supported_actions_cannot_trigger_another_sessions_operations() -> None:
    assert {
        Action.ACTIVATE,
        Action.PLAY_PAUSE,
        Action.QUEUE_NEXT,
        Action.QUEUE_PREVIOUS,
        Action.TIME_ELAPSED,
        Action.TIME_REMAINING,
        Action.TIME_TOTAL,
        Action.COPY_NAME,
        Action.COPY_ADDRESS,
        Action.FOCUS_FILTER,
        Action.VIEW_LIBRARY,
        Action.VIEW_FAVORITES,
        Action.VIEW_PLAYLISTS,
    } <= TIDAL_SUPPORTED_ACTIONS
    assert {
        Action.SHOW_PLAYER,
        Action.RECORD_TOGGLE,
        Action.MANAGE_RADIO_SCHEDULES,
        Action.ADD_PODCAST_SOURCE,
        Action.DOWNLOAD_PODCAST_EPISODES,
        Action.REMOVE_SELECTED,
    }.isdisjoint(TIDAL_SUPPORTED_ACTIONS)

    gui = (
        Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    assert "action not in TIDAL_SUPPORTED_ACTIONS" in gui
