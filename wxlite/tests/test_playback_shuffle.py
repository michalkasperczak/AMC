"""Losowanie R ma jeden kontrakt dla nawigacji, menu, pomocy i stanu."""

from types import SimpleNamespace

from amc_wx_lite import menu_model
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Navigator,
    PlayMedia,
    PlaySpotifyItem,
    PlayTrack,
    SessionId,
)
from amc_wx_lite.shortcuts import Action, Chord, describe, resolve
from amc_wx_lite.state_store import LiteState, StateStore


def _track(item_id: str) -> Row:
    return Row(
        item_id=item_id,
        title=f"Utwór {item_id}",
        kind="track",
        path=rf"C:\Muzyka\{item_id}.mp3",
    )


def _episode(item_id: str) -> Row:
    return Row(
        item_id=item_id,
        title=f"Odcinek {item_id}",
        kind="episode",
        path=f"https://example.invalid/{item_id}.mp3",
    )


def _spotify(item_id: str) -> Row:
    return Row(
        item_id=item_id,
        title=f"Spotify {item_id}",
        kind="track",
        service_id=f"spotify:track:{item_id}",
    )


def _reverse(rows: list[Row]) -> None:
    rows.reverse()


def test_shuffle_keeps_current_and_disable_restores_source_order() -> None:
    nav = Navigator()
    nav._shuffle_rows = _reverse
    nav.apply_folder(
        r"C:\Muzyka",
        [_track("a"), _track("b"), _track("c")],
        preferred_id="b",
    )
    nav.activate_selected()

    assert nav.toggle_shuffle(SessionId.FILES) is True
    assert [row.item_id for row in nav.session.playback_source_rows] == [
        "b", "c", "a"
    ]
    assert nav.toggle_shuffle(SessionId.FILES) is False
    assert [row.item_id for row in nav.session.playback_source_rows] == [
        "a", "b", "c"
    ]


def test_saved_shuffle_applies_on_new_file_source_and_continues_without_focus_move() -> None:
    nav = Navigator()
    nav._shuffle_rows = _reverse
    nav.set_shuffle_enabled(SessionId.FILES, True)
    nav.apply_folder(
        r"C:\Muzyka",
        [_track("a"), _track("b"), _track("c")],
        preferred_id="b",
    )
    nav.activate_selected()
    selected_before = nav.session.model.selected_id

    first = nav.continue_source_after_end(SessionId.FILES)
    second = nav.continue_source_after_end(SessionId.FILES)

    assert isinstance((first or [None])[0], PlayTrack)
    assert (first or [None])[0].item_id == "c"
    assert isinstance((second or [None])[0], PlayTrack)
    assert (second or [None])[0].item_id == "a"
    assert nav.continue_source_after_end(SessionId.FILES) is None
    assert nav.session.model.selected_id == selected_before


def test_local_album_continues_in_order_without_shuffle_or_focus_move() -> None:
    nav = Navigator()
    nav.apply_folder(
        r"C:\Muzyka", [_track("a"), _track("b"), _track("c")],
        preferred_id="b",
    )
    nav.activate_selected()
    selected = nav.session.model.selected_id

    following = nav.continue_source_after_end(SessionId.FILES, "b")
    assert isinstance((following or [None])[0], PlayTrack)
    assert following[0].item_id == "c"
    assert nav.session.model.selected_id == selected
    assert nav.continue_source_after_end(SessionId.FILES, "b") is None
    assert nav.continue_source_after_end(SessionId.FILES, "c") is None


def test_podcast_inbox_does_not_autoplay_all_unplayed_episodes() -> None:
    nav = Navigator()
    state = nav.sessions[SessionId.PODCASTS]
    state.now_playing_id = "p1"
    nav._set_playback_source(state, (_episode("p1"), _episode("p2")))
    assert nav.continue_source_after_end(SessionId.PODCASTS, "p1") is None


def test_podcast_and_spotify_use_their_own_shuffled_sources() -> None:
    nav = Navigator()
    nav._shuffle_rows = _reverse

    podcasts = nav.sessions[SessionId.PODCASTS]
    podcasts.now_playing_id = "p1"
    podcasts.now_playing_title = "Odcinek p1"
    nav._set_playback_source(podcasts, (_episode("p1"), _episode("p2")))
    nav.set_shuffle_enabled(SessionId.PODCASTS, True)
    podcast_next = nav.continue_source_after_end(SessionId.PODCASTS)
    assert isinstance((podcast_next or [None])[0], PlayMedia)
    assert (podcast_next or [None])[0].item_id == "p2"

    spotify = nav.sessions[SessionId.SPOTIFY]
    spotify.now_playing_id = "s1"
    spotify.now_playing_title = "Spotify s1"
    spotify.current_material_id = "s1"
    nav._set_playback_source(spotify, (_spotify("s1"), _spotify("s2")))
    nav.set_shuffle_enabled(SessionId.SPOTIFY, True)
    spotify_next = nav.continue_source_after_end(SessionId.SPOTIFY)
    assert isinstance((spotify_next or [None])[0], PlaySpotifyItem)
    assert (spotify_next or [None])[0].item_id == "s2"
    assert (spotify_next or [None])[0].preserve_navigation is True


def test_spotify_album_enter_continues_after_switching_to_another_session() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    first, second = _spotify("s1"), _spotify("s2")
    nav.apply_spotify_view("Album", [first, second])
    nav.activate_selected()
    nav.note_spotify_playback_started(first.item_id, first.title)
    nav.switch_session(SessionId.RADIO)

    following = nav.continue_source_after_end(SessionId.SPOTIFY, first.item_id)
    assert isinstance((following or [None])[0], PlaySpotifyItem)
    assert following[0].item_id == second.item_id
    assert following[0].preserve_navigation is True
    assert nav.active is SessionId.RADIO
    assert nav.continue_source_after_end(SessionId.SPOTIFY, first.item_id) is None


def test_engine_end_dispatches_next_local_track_without_end_announcement() -> None:
    nav = Navigator()
    nav.apply_folder(r"C:\Muzyka", [_track("a"), _track("b")])
    nav.activate_selected()
    dispatched: list[object] = []
    spoken: list[str] = []
    frame = SimpleNamespace(
        navigator=nav,
        _playing_sessions={SessionId.FILES},
        _playing_session=SessionId.FILES,
        _window_alive=lambda: True,
        _run=lambda intents: dispatched.extend(intents),
        announcer=SimpleNamespace(say=spoken.append),
    )

    LiteFrame._handle_engine_event(frame, "playback.ended", {
        "engine": "files", "sessionId": "files", "id": "a",
        "queueContinues": False,
    })
    assert isinstance(dispatched[0], PlayTrack)
    assert dispatched[0].item_id == "b"
    assert spoken == []


def test_shuffle_does_not_reorder_explicit_queue() -> None:
    nav = Navigator()
    state = nav.sessions[SessionId.FILES]
    rows = (_track("q1"), _track("q2"))
    nav._set_playback_source(state, rows, uses_queue=True)

    assert nav.set_shuffle_enabled(SessionId.FILES, True) is True
    assert state.playback_source_rows == rows
    assert nav.continue_source_after_end(SessionId.FILES) is None


def test_r_menu_help_and_palette_have_the_same_session_contract() -> None:
    chord = Chord("R")
    for session, flag in (
        (SessionId.FILES, "files_session"),
        (SessionId.PODCASTS, "podcast_session"),
        (SessionId.SPOTIFY, "spotify_session"),
    ):
        arguments = {"player_view": True, "radio_session": False, flag: True}
        assert resolve(chord, **arguments) is Action.TOGGLE_SHUFFLE
        menu_item = next(
            item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if item.action is Action.TOGGLE_SHUFFLE
        )
        assert menu_item.shortcut == "R"
        assert menu_item.checkable is True
        assert menu_item.needs_player_view is True
        assert any(
            entry.action is Action.TOGGLE_SHUFFLE and entry.shortcut == "R"
            for entry in entries_for_session(session)
        )
        assert dict(describe(session.value, {Action.TOGGLE_SHUFFLE}))["R"] == (
            "Włącz lub wyłącz odtwarzanie losowe"
        )

    assert resolve(
        chord, player_view=True, radio_session=True
    ) is Action.RECORD_TOGGLE
    assert resolve(
        chord, player_view=True, radio_session=False, wiim_session=True
    ) is Action.WIIM_SELECT_REPEAT
    for session in (
        SessionId.RADIO,
        SessionId.TIDAL,
        SessionId.WIIM,
        SessionId.SONOS,
        SessionId.FRONTIER_SMART,
    ):
        assert not any(
            item.action is Action.TOGGLE_SHUFFLE
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
        )


def test_shuffle_state_is_filtered_and_round_trips(tmp_path) -> None:
    store = StateStore(tmp_path)
    state = LiteState(
        shuffle_enabled_by_session={
            "files": True,
            "podcasts": False,
            "spotify": True,
            "radio": True,
        }
    )

    store.save(state)
    loaded = store.load()

    assert loaded.shuffle_enabled_by_session == {
        "files": True,
        "spotify": True,
    }

