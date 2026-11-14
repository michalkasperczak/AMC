"""Apple Music Test session stays read-only and keyboard discoverable."""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.list_filter import FilterState
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenAppleMusicAlbum,
    OpenAppleMusicPlaylist,
    OpenAppleMusicView,
    SessionId,
    view_context,
)
from amc_wx_lite.shortcuts import Action, Chord, describe_action, resolve


def _resolve(chord: Chord, *, available: bool) -> Action | None:
    return resolve(
        chord,
        player_view=False,
        radio_session=False,
        music_collection_session=available,
        apple_music_session=available,
    )


def test_root_has_four_intentional_labels_and_no_provider_dump() -> None:
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC

    events = navigator.open_apple_music_root()
    state = navigator.session

    assert state.library_view is LibraryView.APPLE_MUSIC_LIBRARY
    assert [row.title for row in state.model.rows] == [
        "Utwory",
        "Albumy",
        "Wykonawcy",
        "Playlisty",
    ]
    assert all(row.service_kind == "appleMusicCollection" for row in state.model.rows)
    assert events == [Announce("Biblioteka Apple Music, 4 pozycje")]


def test_enter_opens_category_playlist_and_explains_original_app_for_song() -> None:
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.open_apple_music_root()

    assert navigator.session.model.select_id("appleMusic-collection:playlists")
    assert navigator.activate_selected() == [OpenAppleMusicView("playlists")]

    playlist = Row(
        "appleMusic:library-playlists:private.list",
        "Moja playlista",
        "playlist",
        service_kind="playlist",
    )
    load_more = Row("loadMore:apple", "Załaduj więcej", "loadMore")
    navigator.apply_apple_music_view(
        OpenAppleMusicView("playlists"),
        "Playlisty Apple Music",
        [playlist, load_more],
    )
    assert navigator.session.model.select_id(playlist.item_id)
    assert navigator.activate_selected() == [
        OpenAppleMusicPlaylist(playlist.item_id, "Moja playlista")
    ]

    track = Row("appleMusic:library-songs:private.song", "Utwór", "track")
    navigator.apply_apple_music_playlist(
        OpenAppleMusicPlaylist(playlist.item_id, playlist.title),
        "Playlista: Moja playlista",
        [track, load_more],
    )
    assert navigator.session.model.select_id(track.item_id)
    assert navigator.activate_selected() == [
        Announce("Ten utwór można otworzyć w oryginalnym Apple Music z menu kontekstowego")
    ]
    assert navigator.session.model.select_id(load_more.item_id)
    assert navigator.activate_selected() == [OpenAppleMusicPlaylist(
        playlist.item_id,
        "Moja playlista",
        load_more=True,
    )]


def test_backspace_restores_playlist_collection_then_root() -> None:
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.open_apple_music_root()
    playlist = Row(
        "appleMusic:library-playlists:private.list",
        "Moja playlista",
        "playlist",
        service_kind="playlist",
    )
    navigator.apply_apple_music_view(
        OpenAppleMusicView("playlists"),
        "Playlisty Apple Music",
        [playlist],
    )
    navigator.session.apple_music_return_id = playlist.item_id
    navigator.apply_apple_music_playlist(
        OpenAppleMusicPlaylist(playlist.item_id, playlist.title),
        "Playlista: Moja playlista",
        [],
    )

    assert navigator.go_to_parent() == [OpenAppleMusicView(
        "playlists",
        preferred_id=playlist.item_id,
    )]
    navigator.apply_apple_music_view(
        OpenAppleMusicView("playlists"),
        "Playlisty Apple Music",
        [playlist],
        preferred_id=playlist.item_id,
    )
    navigator.go_to_parent()
    assert navigator.session.library_view is LibraryView.APPLE_MUSIC_LIBRARY
    assert navigator.session.model.selected_id == "appleMusic-collection:playlists"


def test_album_enter_opens_tracks_and_backspace_restores_album() -> None:
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.open_apple_music_root()
    navigator.session.model.select_id("appleMusic-collection:albums")
    assert navigator.activate_selected() == [OpenAppleMusicView("albums")]

    album = Row(
        "appleMusic:library-albums:private.album", "Album", "album",
        service_kind="album",
    )
    navigator.apply_apple_music_view(
        OpenAppleMusicView("albums"), "Albumy Apple Music", [album],
    )
    assert navigator.activate_selected() == [
        OpenAppleMusicAlbum(album.item_id, "Album")
    ]

    song = Row("appleMusic:library-songs:private.song", "Utwór", "track")
    more = Row("loadMore:album", "Załaduj więcej", "loadMore")
    navigator.apply_apple_music_album(
        OpenAppleMusicAlbum(album.item_id, album.title),
        "Album: Album", [song, more],
    )
    navigator.session.model.select_id(song.item_id)
    assert navigator.activate_selected() == [
        Announce("Ten utwór można otworzyć w oryginalnym Apple Music z menu kontekstowego")
    ]
    navigator.session.model.select_id(more.item_id)
    assert navigator.activate_selected() == [
        OpenAppleMusicAlbum(album.item_id, "Album", load_more=True)
    ]
    assert navigator.go_to_parent() == [
        OpenAppleMusicView("albums", preferred_id=album.item_id)
    ]
    navigator.apply_apple_music_view(
        OpenAppleMusicView("albums"), "Albumy Apple Music", [album],
        preferred_id=album.item_id,
    )
    assert navigator.session.model.selected_id == album.item_id
    assert "album" in describe_action(Action.ACTIVATE, "Apple Music")
    assert _resolve(Chord("Return"), available=True) is Action.ACTIVATE


def test_ctrl_alt_digits_are_shared_globally() -> None:
    expected = {
        "1": Action.VIEW_MUSIC_SONGS,
        "2": Action.VIEW_MUSIC_ALBUMS,
        "3": Action.VIEW_MUSIC_ARTISTS,
    }
    for key, action in expected.items():
        chord = Chord(key, ctrl=True, alt=True)
        assert _resolve(chord, available=True) is action
        assert _resolve(chord, available=False) is action


def test_search_results_have_distinct_filter_context_from_songs() -> None:
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    song = Row("appleMusic:library-songs:i.one", "Jeden", "track")
    navigator.apply_apple_music_view(
        OpenAppleMusicView("songs"), "Utwory Apple Music", [song]
    )
    songs_context = view_context(navigator.session)
    filters = FilterState()
    filters.set_for_view(songs_context, "Niewidoczny na wyniku")

    navigator.show_apple_music_search_results(
        [song], preferred_id=song.item_id,
    )

    assert view_context(navigator.session) != songs_context
    assert filters.text_for_view(view_context(navigator.session)) == ""
    assert filters.text_for_view(songs_context) == "Niewidoczny na wyniku"
    assert navigator.session.model.selected_id == song.item_id


def test_ctrl_shift_l_is_scoped_to_apple_music_session() -> None:
    chord = Chord("L", ctrl=True, shift=True)

    assert _resolve(chord, available=True) is Action.APPLE_MUSIC_TOGGLE_LIBRARY
    assert _resolve(chord, available=False) is None
