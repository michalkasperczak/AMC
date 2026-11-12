from __future__ import annotations

from dataclasses import replace

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenSpotifyCollectionView,
    OpenSpotifyContainer,
    OpenSpotifyView,
    PlaySpotifyItem,
    SessionId,
    ShowSpotifyRelations,
    View,
)
from amc_wx_lite.search_catalog import SESSION_LABELS
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    SPOTIFY_SUPPORTED_ACTIONS,
    resolve,
)


def _artist() -> Row:
    return Row(
        "private-artist-row", "Wykonawca", "artist",
        service_id="spotify:artist:secret", service_kind="artist",
    )


def _album(number: int = 1) -> Row:
    return Row(
        f"private-album-row-{number}", f"Album {number}", "album",
        service_id=f"spotify:album:secret-{number}", service_kind="album",
        artist_name="Wykonawca",
        related_artist_service_id="spotify:artist:secret",
        related_artist_name="Wykonawca",
    )


def _track(number: int = 1) -> Row:
    return Row(
        f"private-track-row-{number}", f"Utwór {number}", "track",
        service_id=f"spotify:track:secret-{number}", service_kind="track",
        artist_name="Wykonawca",
        related_album_service_id="spotify:album:secret",
        related_album_title="Album użytkowy",
        related_artist_service_id="spotify:artist:secret",
        related_artist_name="Wykonawca",
    )


def test_one_spotify_session_has_user_labels_and_contextual_arrow_shortcuts() -> None:
    assert SESSION_LABELS[SessionId.SPOTIFY] == "Spotify"
    assert resolve(
        Chord("7", ctrl=True), player_view=False, radio_session=False
    ) is Action.SESSION_SPOTIFY
    assert resolve(
        Chord("Right"), player_view=False, radio_session=False,
        spotify_session=True,
    ) is Action.SPOTIFY_RELATIONS
    assert resolve(
        Chord("Left"), player_view=False, radio_session=False,
        spotify_session=True,
    ) is Action.QUICK_INFORMATION
    assert {
        Action.VIEW_LIBRARY,
        Action.VIEW_HISTORY,
        Action.SEARCH_CURRENT,
        Action.SPOTIFY_RELATIONS,
        Action.SPOTIFY_VIEW_LIKED,
        Action.SPOTIFY_VIEW_ALBUMS,
        Action.SPOTIFY_VIEW_ARTISTS,
        Action.SPOTIFY_VIEW_PLAYLISTS,
        Action.SPOTIFY_VIEW_PODCASTS,
        Action.SPOTIFY_VIEW_EPISODES,
        Action.SORT_SPOTIFY_COLLECTION_ADDED,
        Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL,
        Action.SORT_SPOTIFY_COLLECTION_CATALOG,
        Action.SHOW_PODCAST_DESCRIPTION,
    } <= SPOTIFY_SUPPORTED_ACTIONS


def test_spotify_categories_use_shared_shortcuts_and_keep_service_commands() -> None:
    expected = {
        Chord("U", ctrl=True): Action.SPOTIFY_VIEW_LIKED,
        Chord("1", ctrl=True, alt=True): Action.VIEW_MUSIC_SONGS,
        Chord("2", ctrl=True, alt=True): Action.VIEW_MUSIC_ALBUMS,
        Chord("3", ctrl=True, alt=True): Action.VIEW_MUSIC_ARTISTS,
        Chord("P", ctrl=True): Action.SPOTIFY_VIEW_PLAYLISTS,
        Chord("O", ctrl=True, alt=True): Action.SPOTIFY_VIEW_PODCASTS,
        Chord("E", ctrl=True, alt=True): Action.SPOTIFY_VIEW_EPISODES,
    }
    for chord, action in expected.items():
        for player_view in (False, True):
            assert resolve(
                chord,
                player_view=player_view,
                radio_session=False,
                spotify_session=True,
            ) is action
    for key in ("A", "W"):
        assert resolve(
            Chord(key, ctrl=True, shift=True),
            player_view=False,
            radio_session=False,
            spotify_session=True,
        ) is None


def test_enter_on_artist_opens_overview_then_backspace_restores_exact_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    rows = [_album(), _artist(), _album(2)]
    nav.apply_spotify_view("Biblioteka Spotify", rows)
    nav.session.model.select_id("private-artist-row")

    events = nav.activate_selected()

    assert events == [Announce("Wykonawca, Wykonawca, 2 pozycje")]
    assert [row.title for row in nav.session.model.rows] == ["Albumy", "Utwory"]
    section = nav.activate_selected()[0]
    assert isinstance(section, OpenSpotifyContainer)
    assert section.kind == "artist"
    assert section.section == "albums"

    assert nav.go_to_parent() == []
    assert nav.session.model.selected_id == "private-artist-row"
    assert nav.session.model.selected_row == rows[1]


def test_album_playlist_and_podcast_enter_request_their_real_container() -> None:
    for kind, title in (
        ("album", "Album"),
        ("playlist", "Playlista"),
        ("podcast", "Podcast"),
    ):
        nav = Navigator()
        nav.active = SessionId.SPOTIFY
        row = Row(
            f"private-{kind}", title, kind,
            service_id=f"spotify:{kind}:secret", service_kind=kind,
        )
        nav.apply_spotify_view("Biblioteka Spotify", [row])

        events = nav.activate_selected()

        assert events == [OpenSpotifyContainer(
            item_id=f"private-{kind}",
            service_id=f"spotify:{kind}:secret",
            title=title,
            kind=kind,
        )]


def test_track_enter_opens_player_and_container_back_restores_rows() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    albums = [_album(), _album(2)]
    nav.apply_spotify_view("Biblioteka Spotify", albums)
    nav.session.model.select_id(albums[1].item_id)
    target = nav.activate_selected()[0]
    tracks = [_track(), _track(2)]
    nav.apply_spotify_container(target, "Album 2", tracks)
    nav.session.model.select_id(tracks[1].item_id)

    events = nav.activate_selected()

    assert events == [PlaySpotifyItem(
        tracks[1].item_id, tracks[1].title, tracks[1].service_id or "", "track"
    )]
    assert nav.session.view is View.PLAYER
    nav.back_to_list()
    assert nav.session.view is View.LIST
    assert nav.session.model.selected_row == tracks[1]

    assert nav.close_spotify_preview() == []
    assert nav.session.library_view is LibraryView.SPOTIFY_LIBRARY
    assert nav.session.model.selected_row == albums[1]


def test_collection_root_enter_opens_named_view_and_back_restores_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    rows = [
        Row(
            "spotify-collection:liked",
            "Polubione utwory",
            "folder",
            service_id="liked",
            service_kind="spotifyCollection",
        ),
        Row(
            "spotify-collection:artists",
            "Wykonawcy",
            "folder",
            service_id="artists",
            service_kind="spotifyCollection",
        ),
    ]
    nav.apply_spotify_view("Biblioteka Spotify", rows)
    nav.session.model.select_id(rows[1].item_id)

    intent = nav.activate_selected()[0]

    assert intent == OpenSpotifyCollectionView(
        rows[1].item_id, "artists", "Wykonawcy"
    )
    nav.apply_spotify_collection_view(intent, "Wykonawcy Spotify", [_artist()])
    assert nav.go_to_parent() == []
    assert nav.session.model.selected_row == rows[1]


def test_collection_load_more_stays_on_level_and_does_not_duplicate_history() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    root = Row(
        "spotify-collection:liked",
        "Polubione utwory",
        "folder",
        service_id="liked",
        service_kind="spotifyCollection",
    )
    nav.apply_spotify_view("Biblioteka Spotify", [root])
    intent = nav.activate_selected()[0]
    first_page = [_track(), Row(
        "load-more:spotify:liked:1",
        "Załaduj więcej",
        "loadMore",
        detail="następne 150 elementów",
    )]
    nav.apply_spotify_collection_view(intent, "Polubione utwory Spotify", first_page)
    nav.session.model.select_id(first_page[-1].item_id)

    load_more = nav.activate_selected()[0]

    assert load_more == OpenSpotifyCollectionView(
        root.item_id,
        "liked",
        "Polubione utwory",
        load_more=True,
    )
    assert len(nav.session.spotify_history) == 1
    second_page = [_track(), _track(2)]
    nav.apply_spotify_collection_view(
        load_more,
        "Polubione utwory Spotify",
        second_page,
        preferred_id=second_page[1].item_id,
    )
    assert len(nav.session.spotify_history) == 1
    assert nav.session.model.selected_row == second_page[1]
    assert nav.go_to_parent() == []
    assert nav.session.model.selected_row == root


def test_podcast_container_load_more_stays_on_level_and_keeps_back_target() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    podcast = Row(
        "private-podcast", "Podcast", "podcast",
        service_id="spotify:show:secret", service_kind="podcast",
    )
    nav.apply_spotify_view("Biblioteka Spotify", [podcast])
    intent = nav.activate_selected()[0]
    first_page = [Row(
        "private-episode-1", "Odcinek 1", "episode",
        service_id="spotify:episode:first", service_kind="episode",
    ), Row(
        "load-more:spotify-podcast:1", "Załaduj więcej", "loadMore",
        detail="następne 150 elementów",
    )]
    nav.apply_spotify_container(intent, "Podcast, Podcast", first_page)
    nav.session.model.select_id(first_page[-1].item_id)

    load_more = nav.activate_selected()[0]

    assert isinstance(load_more, OpenSpotifyContainer)
    assert load_more.load_more is True
    assert len(nav.session.spotify_history) == 1
    second_page = [first_page[0], Row(
        "private-episode-2", "Odcinek 2", "episode",
        service_id="spotify:episode:second", service_kind="episode",
    )]
    nav.apply_spotify_container(
        load_more,
        "Podcast, Podcast",
        second_page,
        preferred_id=second_page[1].item_id,
    )
    assert len(nav.session.spotify_history) == 1
    assert nav.session.model.selected_row == second_page[1]
    assert nav.go_to_parent() == []
    assert nav.session.model.selected_row == podcast


def test_collection_sort_refresh_stays_on_level_and_preserves_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    root = Row(
        "spotify-collection:liked",
        "Polubione utwory",
        "folder",
        service_id="liked",
        service_kind="spotifyCollection",
    )
    nav.apply_spotify_view("Biblioteka Spotify", [root])
    intent = nav.activate_selected()[0]
    nav.apply_spotify_collection_view(intent, "Polubione utwory Spotify", [_track()])
    history_depth = len(nav.session.spotify_history)
    sorted_rows = [_track(2), _track()]
    refresh = OpenSpotifyCollectionView(
        root.item_id,
        "liked",
        "Polubione utwory",
        preferred_id=sorted_rows[1].item_id,
        replace_current=True,
    )

    nav.apply_spotify_collection_view(
        refresh,
        "Polubione utwory Spotify",
        sorted_rows,
        preferred_id=refresh.preferred_id,
    )

    assert len(nav.session.spotify_history) == history_depth
    assert nav.session.model.selected_row == sorted_rows[1]
    assert nav.go_to_parent() == []
    assert nav.session.model.selected_row == root


def test_episode_enter_and_right_arrow_return_to_its_podcast() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    episode = Row(
        "private-episode", "Odcinek", "episode",
        service_id="spotify:episode:secret", service_kind="episode",
        related_podcast_service_id="spotify:show:secret",
        related_podcast_title="Podcast użytkowy",
    )
    nav.apply_spotify_view("Biblioteka Spotify", [episode])

    menu = nav.spotify_relations()[0]
    assert isinstance(menu, ShowSpotifyRelations)
    assert [choice.label for choice in menu.choices] == [
        "Przejdź do Podcast użytkowy"
    ]
    assert nav.activate_spotify_relation(menu.choices[0]) == [OpenSpotifyContainer(
        item_id="private-episode:related-podcast",
        service_id="spotify:show:secret",
        title="Podcast użytkowy",
        kind="podcast",
    )]
    assert isinstance(nav.activate_selected()[0], PlaySpotifyItem)


def test_left_and_right_arrow_speak_only_labels_and_keep_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    track = _track()
    nav.apply_spotify_view("Biblioteka Spotify", [track])
    before = nav.session.model.selected_id

    information = nav.spotify_information()[0]
    relations = nav.spotify_relations()[0]

    assert isinstance(information, Announce)
    assert isinstance(relations, ShowSpotifyRelations)
    assert relations.title == "Przejdź do"
    assert [choice.label for choice in relations.choices] == [
        "Przejdź do albumu",
        "Przejdź do wykonawcy",
    ]
    spoken = " ".join((
        information.text,
        relations.title,
        *(choice.label for choice in relations.choices),
    ))
    assert nav.session.model.selected_id == before
    assert "secret" not in spoken
    assert "spotify:" not in spoken
    assert "SessionId" not in spoken
    assert "Row(" not in spoken
    assert "powiąz" not in spoken.casefold()


def test_visible_source_order_drives_next_item_and_confirmation() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    tracks = [_track(), _track(2)]
    nav.apply_spotify_view("Biblioteka Spotify", tracks)
    nav.activate_selected()
    confirmed = nav.note_spotify_playback_started(
        tracks[0].item_id, tracks[0].title
    )
    assert confirmed == tracks[0]

    events = nav.step_playback_source(True)

    assert events == [PlaySpotifyItem(
        tracks[1].item_id,
        tracks[1].title,
        tracks[1].service_id or "",
        "track",
        previous_item_id=tracks[0].item_id,
        previous_title=tracks[0].title,
    )]
    assert nav.session.now_playing_id == tracks[0].item_id
    confirmed = nav.note_spotify_playback_started(
        tracks[1].item_id, tracks[1].title
    )
    assert confirmed == tracks[1]
    assert nav.session.now_playing_id == tracks[1].item_id


def test_spotify_preset_playback_keeps_visible_list_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    albums = [_album(), _album(2)]
    nav.apply_spotify_view("Biblioteka Spotify", albums)
    nav.session.model.select_id(albums[1].item_id)
    anchor_before = nav.session.list_anchor_id
    track = _track()

    intent = nav.prepare_spotify_preset_playback(
        track,
        source_rows=(track, _track(2)),
        announcement="Album z presetu",
    )

    assert isinstance(intent, PlaySpotifyItem)
    assert intent.preserve_navigation
    assert intent.success_announcement == "Album z presetu"
    assert nav.session.model.selected_row == albums[1]
    confirmed = nav.note_spotify_playback_started(
        track.item_id,
        track.title,
        preserve_navigation=True,
    )
    assert confirmed == track
    assert nav.session.model.selected_row == albums[1]
    assert nav.session.list_anchor_id == anchor_before


def test_spotify_history_is_named_and_escape_restores_list_and_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    albums = [_album(), _album(2)]
    tracks = [_track(), _track(2)]
    nav.apply_spotify_view("Biblioteka Spotify", albums)
    nav.session.model.select_id(albums[1].item_id)
    snapshot = nav.capture_transient_navigation()

    events = nav.apply_spotify_history(tracks)

    assert events == [Announce("Spotify — Historia odtwarzania, 2 pozycje")]
    assert nav.session.library_view is LibraryView.SPOTIFY_HISTORY
    nav.session.model.select_id(tracks[1].item_id)

    assert nav.restore_transient_navigation(snapshot) == []
    assert nav.session.library_view is LibraryView.SPOTIFY_LIBRARY
    assert nav.session.model.selected_row == albums[1]

    nav.apply_spotify_history(tracks)
    assert nav.session.model.selected_row == tracks[1]


def test_empty_spotify_history_has_one_clear_message() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY

    events = nav.apply_spotify_history([])

    assert events == [Announce(
        "Spotify — Historia odtwarzania, brak odtworzonych elementów"
    )]
    assert nav.session.library_view is LibraryView.SPOTIFY_HISTORY


def test_session_switch_preserves_nested_list_and_focus() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    album = _album()
    nav.apply_spotify_view("Biblioteka Spotify", [album])
    target = nav.activate_selected()[0]
    tracks = [_track(), _track(2)]
    nav.apply_spotify_container(target, "Album", tracks, preferred_id=tracks[1].item_id)

    nav.switch_session(SessionId.FILES)
    event = nav.switch_session(SessionId.SPOTIFY)[0]

    assert event == Announce("Spotify, lista, Utwór 2")
    assert nav.session.library_view is LibraryView.SPOTIFY_CONTAINER
    assert nav.session.model.selected_row == tracks[1]


def test_backspace_from_direct_recognized_album_loads_spotify_root() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    nav.apply_spotify_container(
        OpenSpotifyContainer("recognized", "album-1", "Album", "album"),
        "Album", [_track()],
    )

    assert nav.go_to_parent() == [OpenSpotifyView()]
    assert nav.active is SessionId.SPOTIFY


def test_search_result_opens_without_adding_it_to_the_saved_library() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    saved = _album()
    nav.apply_spotify_view("Biblioteka Spotify", [saved])
    online = Row(
        "private-online", "Nowa playlista", "playlist",
        service_id="spotify:playlist:online-secret", service_kind="playlist",
    )

    events = nav.open_spotify_search_result(online)

    assert events == [OpenSpotifyContainer(
        "private-online", "spotify:playlist:online-secret", "Nowa playlista", "playlist"
    )]
    assert nav.session.model.rows == [saved]
    assert nav.session.spotify_history == []


def test_search_track_can_play_without_leaving_the_list() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    track = _track()
    nav.apply_spotify_view("Wyniki Spotify", [track])

    events = nav.open_spotify_search_result(track, open_player=False)

    assert events == [PlaySpotifyItem(
        track.item_id, track.title, track.service_id or "", "track"
    )]
    assert nav.session.view is View.LIST


def test_unplayable_row_refuses_once_without_exposing_private_identity() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    row = replace(
        _track(),
        activation_message="Ten materiał nie jest obecnie dostępny do odtworzenia",
    )
    nav.apply_spotify_view("Biblioteka Spotify", [row])

    events = nav.activate_selected()

    assert events == [Announce("Ten materiał nie jest obecnie dostępny do odtworzenia")]
    assert "secret" not in events[0].text
    assert nav.session.view is View.LIST

def test_spotify_queue_playback_advances_without_shuffle_or_changing_provider() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    first, second = _track(1), _track(2)
    nav.apply_spotify_queue([first, second])
    assert nav.session.library_view is LibraryView.SPOTIFY_QUEUE
    assert nav.session.model.selected_row == first

    started = nav.activate_selected()[0]
    assert isinstance(started, PlaySpotifyItem)
    assert started.service_id == first.service_id
    nav.note_spotify_playback_started(first.item_id, first.title)
    continuation = nav.continue_source_after_end(SessionId.SPOTIFY)
    assert continuation is not None
    following = continuation[0]
    assert isinstance(following, PlaySpotifyItem)
    assert following.service_id == second.service_id
    assert nav.sessions[SessionId.TIDAL].playback_source_rows == ()


def test_entire_spotify_album_advances_after_first_track() -> None:
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    first, second = _track(1), _track(2)
    intent = nav.prepare_spotify_preset_playback(
        first, source_rows=(first, second), announcement="Album użytkowy",
    )
    assert isinstance(intent, PlaySpotifyItem)
    nav.note_spotify_playback_started(first.item_id, first.title, preserve_navigation=True)
    continuation = nav.continue_source_after_end(SessionId.SPOTIFY)
    assert continuation is not None
    assert isinstance(continuation[0], PlaySpotifyItem)
    assert continuation[0].service_id == second.service_id
