from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

import amc_wx_lite.gui as gui_module
import pytest
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    LibraryView,
    Navigator,
    OpenTidalContainer,
    SessionId,
    View,
)
from amc_wx_lite.search_catalog import SearchEntry
from amc_wx_lite.search_catalog import SearchResultAction
from amc_wx_lite.search_sources import SearchCollection
from amc_wx_lite.shortcuts import Action


def frame_stub():
    calls = []
    frame = SimpleNamespace(
        _transient_preview_return="old",
        _switch_session=lambda session: calls.append(("session", session)),
        _open_library_view=lambda intent, **kwargs: calls.append(
            ("library", intent, kwargs)
        ),
        _open_radio_view=lambda intent, **kwargs: calls.append(
            ("radio", intent, kwargs)
        ),
        _open_podcast_view=lambda intent, **kwargs: calls.append(
            ("podcast", intent, kwargs)
        ),
        _open_podcast_library=lambda preferred_id=None, **kwargs: calls.append(
            ("podcast-library", preferred_id, kwargs)
        ),
        _open_tidal_view=lambda intent, **kwargs: calls.append(
            ("tidal", intent, kwargs)
        ),
    )
    return frame, calls


def test_file_result_returns_to_all_files_and_stable_item() -> None:
    frame, calls = frame_stub()
    result = SearchEntry(
        SessionId.FILES,
        Row("file-1", "Nagranie", "track", path=r"D:\Nagranie.mp3"),
        LibraryView.ALL_FILES,
    )
    LiteFrame._open_search_result(frame, result)
    assert calls[0] == ("session", SessionId.FILES)
    intent = calls[1][1]
    assert intent.view is LibraryView.ALL_FILES
    assert intent.preferred_id == "file-1"
    assert intent.target_session_id is SessionId.FILES
    assert frame._transient_preview_return is None


def test_radio_result_returns_to_radio_library() -> None:
    frame, calls = frame_stub()
    result = SearchEntry(
        SessionId.RADIO,
        Row("radio-1", "Radio Poznań", "station", url="https://radio"),
    )
    LiteFrame._open_search_result(frame, result)
    intent = calls[1][1]
    assert calls[0] == ("session", SessionId.RADIO)
    assert calls[1][0] == "radio"
    assert intent.view is None
    assert intent.preferred_id == "radio-1"


def test_episode_result_opens_parent_and_selects_episode() -> None:
    frame, calls = frame_stub()
    result = SearchEntry(
        SessionId.PODCASTS,
        Row("episode-1", "Odcinek", "episode", parent_id="podcast-1"),
        LibraryView.PODCAST_EPISODES,
        parent_id="podcast-1",
    )
    LiteFrame._open_search_result(frame, result)
    intent = calls[1][1]
    assert calls[0] == ("session", SessionId.PODCASTS)
    assert intent.subscription_id == "podcast-1"
    assert intent.preferred_id == "episode-1"


def test_tidal_result_returns_to_collection_it_came_from() -> None:
    frame, calls = frame_stub()
    result = SearchEntry(
        SessionId.TIDAL,
        Row("tidal-1", "Album", "album"),
        LibraryView.TIDAL_LIBRARY,
    )
    LiteFrame._open_search_result(frame, result)
    intent = calls[1][1]
    assert calls[0] == ("session", SessionId.TIDAL)
    assert intent.view is LibraryView.TIDAL_LIBRARY
    assert intent.preferred_id == "tidal-1"


def test_search_from_player_is_refused_with_the_full_amc_message() -> None:
    messages = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(view=View.PLAYER),
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._show_search(frame, all_services=False)
    assert messages == ["Wyszukiwanie jest dostępne na listach"]


def test_search_favorite_uses_the_same_command_as_the_real_list() -> None:
    opened = []
    dispatched = []
    frame = SimpleNamespace(
        _open_search_result=lambda entry, **kwargs: opened.append((entry, kwargs)),
        _dispatch=dispatched.append,
    )
    result = SearchEntry(
        SessionId.FILES,
        Row("file-1", "Nagranie", "track", path=r"D:\Nagranie.mp3"),
        LibraryView.ALL_FILES,
    )
    LiteFrame._complete_search_result_action(
        frame,
        result,
        [result],
        SearchResultAction.FAVORITE,
    )
    assert opened[0][1]["selected_ids"] == ["file-1"]
    opened[0][1]["after_open"]()
    assert dispatched == [Action.TOGGLE_FAVORITE]


def test_search_open_does_not_invent_a_second_action() -> None:
    opened = []
    frame = SimpleNamespace(_open_search_result=opened.append)
    result = SearchEntry(SessionId.RADIO, Row("r", "Radio", "station"))
    LiteFrame._complete_search_result_action(
        frame,
        result,
        [result],
        SearchResultAction.OPEN,
    )
    assert opened == [result]


def test_podcast_search_combines_cached_and_public_results() -> None:
    shown = []
    searched = []
    local = SearchEntry(
        SessionId.PODCASTS,
        Row("saved", "Zapisany podcast", "podcast", url="https://saved.test/feed"),
        LibraryView.PODCAST_LIBRARY,
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((local,), ())
        ),
        client=SimpleNamespace(
            search_podcast_directories=lambda query: (
                searched.append(query)
                or {
                    "items": [{
                        "id": "remote",
                        "title": "Nowy kanał",
                        "artist": "Autor",
                        "kind": "podcast",
                        "remoteKind": "rss",
                        "address": "https://remote.test/feed",
                        "detail": "Apple Podcasts",
                    }],
                    "warnings": [],
                }
            )
        ),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: shown.append("failed"),
    )

    LiteFrame._run_search_query(
        frame,
        "podcast",
        dialog,
        all_services=False,
        session_before=SessionId.PODCASTS,
    )

    assert searched == ["podcast"]
    assert [entry.row.title for entry in shown[0][0]] == [
        "Zapisany podcast",
        "Nowy kanał",
    ]
    assert shown[0][1] == ()
    assert dialog.catalog.entries == (local,)


def test_public_search_failure_preserves_cached_results() -> None:
    shown = []
    local = SearchEntry(
        SessionId.PODCASTS,
        Row("saved", "Audycja", "podcast", url="https://saved.test/feed"),
        LibraryView.PODCAST_LIBRARY,
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    def unavailable(_query):
        raise ValueError("technical details must not be spoken")

    frame = SimpleNamespace(
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((local,), ())
        ),
        client=SimpleNamespace(search_podcast_directories=unavailable),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: shown.append("failed"),
    )

    LiteFrame._run_search_query(
        frame,
        "audycja",
        dialog,
        all_services=False,
        session_before=SessionId.PODCASTS,
    )

    assert [entry.row.item_id for entry in shown[0][0]] == ["saved"]
    assert shown[0][1] == (
            "Nie udało się przeszukać publicznych katalogów podcastów, "
            "SoundCloud i YouTube",
    )
    assert "technical" not in shown[0][1][0]


def test_radio_search_combines_library_and_public_directory() -> None:
    shown = []
    searched = []
    local = SearchEntry(
        SessionId.RADIO,
        Row("saved", "Radio lokalne", "station", url="https://saved.test/live"),
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((local,), ())
        ),
        client=SimpleNamespace(
            search_radio_directory=lambda query: (
                searched.append(query)
                or {
                    "items": [{
                        "id": "remote",
                        "title": "Radio publiczne",
                        "kind": "station",
                        "remoteKind": "radioStation",
                        "address": "https://remote.test/live",
                        "detail": "Polska, MP3, 128 kb/s",
                    }],
                    "warnings": [],
                }
            )
        ),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: shown.append("failed"),
    )

    LiteFrame._run_search_query(
        frame,
        "radio",
        dialog,
        all_services=False,
        session_before=SessionId.RADIO,
    )

    assert searched == ["radio"]
    assert [entry.row.title for entry in shown[0][0]] == [
        "Radio lokalne",
        "Radio publiczne",
    ]
    assert shown[0][1] == ()


def test_tidal_search_uses_online_catalog_and_preserves_result_kind() -> None:
    shown = []
    searched = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((), ())
        ),
        client=SimpleNamespace(
            tidal_search=lambda query: (
                searched.append(query)
                or {
                    "heading": "Wyniki wyszukiwania TIDAL",
                    "items": [{
                        "id": "tidal:artists:7",
                        "externalId": "artists:7",
                        "title": "Nowy artysta",
                        "artist": "",
                        "kind": "artist",
                        "durationTicks": 0,
                        "isFavorite": False,
                        "isInLibrary": False,
                    }],
                }
            )
        ),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: shown.append("failed"),
    )

    LiteFrame._run_search_query(
        frame,
        "Nowy artysta",
        dialog,
        all_services=False,
        session_before=SessionId.TIDAL,
    )

    assert searched == ["Nowy artysta"]
    assert len(shown[0][0]) == 1
    result = shown[0][0][0]
    assert result.row.kind == "artist"
    assert result.row.service_id == "artists:7"
    assert result.remote_kind == "tidalCatalog"
    assert shown[0][1] == ()


def test_tidal_search_right_arrow_uses_catalog_row_without_library_write() -> None:
    calls = []
    result = SearchEntry(
        SessionId.TIDAL,
        Row(
            "tidal:artists:7",
            "Nowy artysta",
            "artist",
            service_id="artists:7",
            service_kind="artist",
        ),
        remote_kind="tidalCatalog",
    )
    frame = SimpleNamespace(
        _open_tidal_search_result=lambda entry, *, relations: calls.append(
            (entry, relations)
        )
    )

    LiteFrame._complete_search_result_action(
        frame,
        result,
        [result],
        SearchResultAction.RELATIONS,
    )

    assert calls == [(result, True)]


def test_spotify_search_uses_online_catalog_without_library_write() -> None:
    shown = []
    searched = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((), ())
        ),
        client=SimpleNamespace(
            spotify_search=lambda query: (
                searched.append(query)
                or {
                    "heading": "Wyniki wyszukiwania Spotify",
                    "items": [{
                        "id": "spotify:artist:private",
                        "externalId": "privateartist",
                        "title": "Nowy wykonawca",
                        "artist": "",
                        "kind": "artist",
                        "isAvailable": True,
                    }],
                    "warning": "",
                }
            )
        ),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: shown.append("failed"),
    )

    LiteFrame._run_search_query(
        frame,
        "Nowy wykonawca",
        dialog,
        all_services=False,
        session_before=SessionId.SPOTIFY,
    )

    assert searched == ["Nowy wykonawca"]
    assert len(shown[0][0]) == 1
    result = shown[0][0][0]
    assert result.row.kind == "artist"
    assert result.row.service_id == "privateartist"
    assert result.remote_kind == "spotifyCatalog"
    assert shown[0][1] == ()


def test_global_search_uses_loaded_spotify_rows_without_catalog_request(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(
        gui_module.session_order, "available_sessions",
        lambda: (SessionId.SPOTIFY,),
    )
    shown = []
    track = Row(
        "spotify:track:known", "Znany utwór", "track",
        service_id="known", service_kind="track", artist_name="Wykonawca",
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={
            SessionId.SPOTIFY: SimpleNamespace(
                model=SimpleNamespace(rows=(track,))
            ),
        }),
        state=SimpleNamespace(spotify_playback_history=[]),
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((), ())
        ),
        client=SimpleNamespace(spotify_search=lambda _query: pytest.fail(
            "Global search must use the local Spotify snapshot"
        )),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: pytest.fail("Search failed"),
    )

    LiteFrame._run_search_query(
        frame, "znany", dialog, all_services=True,
        session_before=SessionId.FILES,
    )

    assert len(shown[0][0]) == 1
    entry = shown[0][0][0]
    assert entry.session_id is SessionId.SPOTIFY
    assert entry.kind_label == "utwór"
    assert entry.remote_kind == "spotifyCatalog"
    assert dialog.catalog.entries == (entry,)


def test_spotify_search_right_arrow_uses_catalog_row_without_write() -> None:
    calls = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:artist:private",
            "Nowy wykonawca",
            "artist",
            service_id="privateartist",
            service_kind="artist",
        ),
        remote_kind="spotifyCatalog",
    )
    frame = SimpleNamespace(
        _open_spotify_search_result=lambda entry, *, relations: calls.append(
            (entry, relations)
        )
    )

    LiteFrame._complete_search_result_action(
        frame,
        result,
        [result],
        SearchResultAction.RELATIONS,
    )

    assert calls == [(result, True)]


def test_search_relation_picker_stays_owned_by_the_search_window() -> None:
    shown = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:track:private",
            "Utwór",
            "track",
            service_id="privatetrack",
            service_kind="track",
            related_album_service_id="privatealbum",
            related_album_title="Album",
            related_artist_service_id="privateartist",
            related_artist_name="Wykonawca",
        ),
        remote_kind="spotifyCatalog",
    )
    search_dialog = SimpleNamespace(
        announcer=SimpleNamespace(say=lambda _message: None),
        show_relation_choices=lambda choices, labels, *, title: shown.append(
            (choices, labels, title)
        ),
    )

    frame = SimpleNamespace(navigator=Navigator(), _detailed_hints=False)
    choice = LiteFrame._choose_search_result_relation(
        frame,
        result,
        search_dialog,
    )

    assert choice is None
    assert len(shown) == 1
    choices, labels, title = shown[0]
    assert [candidate.label for candidate in choices] == [
        "Przejdź do albumu",
        "Przejdź do wykonawcy",
    ]
    assert labels == ("Album", "Wykonawca")
    assert title == "Powiązania"


def test_single_search_relation_is_shown_before_it_can_be_opened() -> None:
    shown = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:episode:private",
            "Odcinek",
            "episode",
            service_id="privateepisode",
            service_kind="episode",
            related_podcast_service_id="privatepodcast",
            related_podcast_title="Podcast",
        ),
        remote_kind="spotifyCatalog",
    )
    search_dialog = SimpleNamespace(
        announcer=SimpleNamespace(say=lambda _message: None),
        show_relation_choices=lambda choices, labels, *, title: shown.append(
            (choices, labels, title)
        ),
    )
    frame = SimpleNamespace(navigator=Navigator(), _detailed_hints=False)

    choice = LiteFrame._choose_search_result_relation(
        frame,
        result,
        search_dialog,
    )

    assert choice is None
    assert len(shown) == 1
    assert len(shown[0][0]) == 1
    assert shown[0][1] == ("Podcast",)


@pytest.mark.parametrize(
    "session_id",
    (
        SessionId.FILES,
        SessionId.RADIO,
        SessionId.PODCASTS,
        SessionId.WIIM,
        SessionId.SONOS,
    ),
)
def test_other_search_sources_do_not_open_a_relation_window(session_id) -> None:
    messages = []
    result = SearchEntry(
        session_id,
        Row("result", "Wynik", "episode"),
    )
    search_dialog = SimpleNamespace(
        announcer=SimpleNamespace(say=messages.append),
        show_relation_choices=lambda *_args, **_kwargs: pytest.fail(
            "Źródło bez relacji nie może otwierać pomocniczego okna"
        ),
    )
    frame = SimpleNamespace(navigator=Navigator(), _detailed_hints=False)

    choice = LiteFrame._choose_search_result_relation(
        frame,
        result,
        search_dialog,
    )

    assert choice is None
    assert messages == [
        "Przejście do albumu, wykonawcy lub podcastu jest dostępne "
        "dla wyników TIDAL i Spotify"
    ]


def test_spotify_search_library_action_uses_confirmed_membership_write() -> None:
    calls = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:album:private",
            "Album",
            "album",
            service_id="privatealbum",
            service_kind="album",
        ),
        remote_kind="spotifyCatalog",
    )
    frame = SimpleNamespace(
        _change_spotify_search_result_membership=(
            lambda entries, *, favorite, announce: calls.append(
                (entries, favorite, announce)
            )
        ),
        announcer=SimpleNamespace(say=lambda _message: None),
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
    )

    assert len(calls) == 1
    assert calls[0][:2] == ([result], False)
    assert callable(calls[0][2])


def test_spotify_search_playback_opens_the_result_then_activates_it() -> None:
    calls = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:track:private",
            "Utwór",
            "track",
            service_id="privatetrack",
            service_kind="track",
        ),
        remote_kind="spotifyCatalog",
    )
    frame = SimpleNamespace(
        _open_spotify_search_result=lambda entry, *, relations: calls.append(
            ("open", entry, relations)
        ),
        _dispatch=lambda action: calls.append(("dispatch", action)),
    )

    LiteFrame._complete_search_result_action(
        frame,
        result,
        [result],
        SearchResultAction.TOGGLE_PLAYBACK,
    )

    assert calls == [
        ("open", result, False),
        ("dispatch", Action.ACTIVATE),
    ]


def test_saved_local_search_library_action_removes_only_the_library_entry() -> None:
    calls = []
    entry = SearchEntry(
        SessionId.FILES,
        Row("local-private", "Nagranie", "track", path="D:/Muzyka/nagranie.mp3"),
        LibraryView.ALL_FILES,
    )
    frame = SimpleNamespace(
        _remove_selected_items=lambda: calls.append("remove-library-entry"),
        announcer=SimpleNamespace(say=lambda message: calls.append(message)),
    )

    LiteFrame._perform_search_result_action(
        frame,
        entry,
        SearchResultAction.LIBRARY,
    )

    assert calls == ["remove-library-entry"]


def test_online_tidal_library_action_uses_tidal_membership_not_podcast_import() -> None:
    calls = []
    result = SearchEntry(
        SessionId.TIDAL,
        Row(
            "tidal:artists:7",
            "Nowy artysta",
            "artist",
            service_id="artists:7",
            service_kind="artist",
        ),
        remote_kind="tidalCatalog",
    )
    frame = SimpleNamespace(
        _change_tidal_search_result_membership=(
            lambda entries, *, favorite, announce: calls.append(
                (entries, favorite, announce)
            )
        ),
        announcer=SimpleNamespace(say=lambda message: calls.append(message)),
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
    )

    assert len(calls) == 1
    assert calls[0][:2] == ([result], False)
    assert callable(calls[0][2])


def test_online_tidal_library_action_uses_confirmed_membership_toggle() -> None:
    calls = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    row = Row(
        "tidal:albums:44",
        "365 widoków na przyszłość",
        "album",
        service_id="albums:44",
        service_kind="album",
        artist_name="Metro",
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(
            tidal_collection_membership=lambda items, **kwargs: (
                calls.append((items, kwargs))
                or {"added": True, "message": "Dodano do Biblioteki TIDAL"}
            )
        ),
        runner=Runner(),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
    )

    LiteFrame._change_tidal_search_result_membership(
        frame,
        [SearchEntry(SessionId.TIDAL, row, remote_kind="tidalCatalog")],
        favorite=False,
    )

    assert calls[0][1] == {"mode": "library", "add": None}
    assert calls[-1] == ("say", "Dodano do Biblioteki TIDAL")


def test_tidal_catalog_add_and_saved_library_remove_are_explicit() -> None:
    requests = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    row = Row(
        "tidal:albums:44",
        "365 widoków na przyszłość",
        "album",
        service_id="albums:44",
        service_kind="album",
        artist_name="Metro",
    )

    def run_for(view: LibraryView) -> bool:
        state = SimpleNamespace(library_view=view)

        def membership(_items, **kwargs):
            requests.append(kwargs)
            return {
                "added": kwargs["add"],
                "message": "Zmieniono kolekcję TIDAL",
            }

        frame = SimpleNamespace(
            navigator=SimpleNamespace(
                active=SessionId.TIDAL,
                sessions={SessionId.TIDAL: state},
            ),
            client=SimpleNamespace(tidal_collection_membership=membership),
            runner=Runner(),
            announcer=SimpleNamespace(say=lambda _message: None),
            _selected_action_rows=lambda: [row],
            _tidal_membership_pending=False,
            _open_tidal_view=lambda _intent: None,
        )
        LiteFrame._toggle_tidal_membership(frame, favorite=False)
        return frame._tidal_membership_pending

    assert run_for(LibraryView.TIDAL_CONTAINER) is False
    assert run_for(LibraryView.TIDAL_LIBRARY) is False
    assert requests == [
        {"mode": "library", "add": True},
        {"mode": "library", "add": False},
    ]


def test_library_shortcut_saves_open_tidal_album_instead_of_selected_track() -> None:
    requests, spoken = [], []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    nav = Navigator()
    nav.active = SessionId.TIDAL
    album = OpenTidalContainer("recognized-album", "albums:44", "Cały album", "album")
    track = Row("track-1", "Utwór", "track", service_id="tracks:1")
    nav.apply_tidal_container(album, "Cały album", [track])
    frame = SimpleNamespace(
        navigator=nav,
        client=SimpleNamespace(
            tidal_collection_membership=lambda items, *, mode, add: (
                requests.append((items, mode, add))
                or {"added": True, "message": "Dodano album do Biblioteki TIDAL"}
            )
        ),
        runner=Runner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [track],
        _tidal_membership_pending=False,
    )

    LiteFrame._toggle_tidal_membership(frame, favorite=False)

    assert requests[0][0][0]["kind"] == "album"
    assert requests[0][0][0]["externalId"] == "albums:44"
    assert requests[0][1:] == ("library", True)
    assert spoken == ["Dodano album do Biblioteki TIDAL"]


def test_remote_radio_open_plays_without_profile_write() -> None:
    calls = []
    client = SimpleNamespace(
        add_radio_station=lambda *_args: calls.append(("unexpected-profile-write",))
    )
    intents = []
    frame = SimpleNamespace(
        _radio_search_action_pending=False,
        client=client,
        announcer=SimpleNamespace(say=lambda value: calls.append(("say", value))),
        _switch_session=lambda session: calls.append(("session", session)),
        _session_open_player_on_enter=lambda session: False,
        navigator=SimpleNamespace(
            open_radio_search_result=lambda row, *, open_player: [row]
        ),
        _run=lambda values: intents.extend(values),
    )
    result = SearchEntry(
        SessionId.RADIO,
        Row(
            "directory-radio",
            "Radio Poznań",
            "station",
            url="https://example.test/live",
        ),
        remote_kind="radioStation",
    )

    LiteFrame._complete_remote_radio_search_result_action(
        frame,
        [result],
        SearchResultAction.OPEN,
    )

    assert calls == [("session", SessionId.RADIO)]
    assert intents == [result.row]
    assert not any(call[0] == "unexpected-profile-write" for call in calls)


def test_remote_radio_queue_is_refused_without_profile_write() -> None:
    messages = []
    frame = SimpleNamespace(
        announcer=SimpleNamespace(say=messages.append),
    )
    result = SearchEntry(
        SessionId.RADIO,
        Row("directory-radio", "Radio", "station", url="https://example.test/live"),
        remote_kind="radioStation",
    )

    LiteFrame._complete_remote_radio_search_result_action(
        frame,
        [result],
        SearchResultAction.QUEUE,
    )

    assert messages == ["Ta funkcja nie jest dostępna w Radiu"]


def test_remote_result_uses_materialization_instead_of_local_navigation() -> None:
    calls = []
    frame = SimpleNamespace(
        _complete_remote_search_result_action=lambda entries, action: calls.append(
            (entries, action)
        )
    )
    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "remote-id",
            "Kanał",
            "podcast",
            url="https://www.youtube.com/channel/UCexample/videos",
        ),
        LibraryView.PODCAST_LIBRARY,
        remote_kind="youtubeChannel",
    )

    LiteFrame._complete_search_result_action(
        frame,
        result,
        [result],
        SearchResultAction.OPEN,
    )

    assert calls == [([result], SearchResultAction.OPEN)]


def test_global_search_includes_verified_soundcloud_directory_result(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    shown: list[tuple[list[SearchEntry], tuple[str, ...]]] = []
    calls: list[tuple[str, object]] = []
    monkeypatch.setattr(
        gui_module.session_order,
        "available_sessions",
        lambda: (SessionId.FILES, SessionId.PODCASTS),
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    def collect(sessions) -> SearchCollection:
        calls.append(("collect", tuple(sessions)))
        return SearchCollection((), ())

    def search_podcast_directories(query: str) -> dict[str, object]:
        calls.append(("podcast-directories", query))
        return {
            "items": [
                {
                    "id": "podcast-directory:soundcloud:152970424",
                    "title": "Freta10",
                    "artist": "Dominikanie Warszawa-Freta",
                    "kind": "podcast",
                    "remoteKind": "soundcloudPodcast",
                    "address": (
                        "https://feeds.soundcloud.com/users/"
                        "soundcloud:users:152970424/sounds.rss"
                    ),
                    "pageAddress": "https://soundcloud.com/freta10",
                    "detail": "podcast SoundCloud",
                    "durationTicks": 0,
                }
            ],
            "warnings": [],
        }

    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={}),
        search_sources=SimpleNamespace(collect=collect),
        client=SimpleNamespace(
            search_podcast_directories=search_podcast_directories,
        ),
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: pytest.fail("wyszukiwanie nie powinno zawieść"),
    )

    LiteFrame._run_search_query(
        frame,
        "Dominikanie Freta",
        dialog,
        all_services=True,
        session_before=SessionId.FILES,
    )

    assert calls == [
        ("collect", (SessionId.FILES, SessionId.PODCASTS)),
        ("podcast-directories", "Dominikanie Freta"),
    ]
    entries, warnings = shown[0]
    assert warnings == ()
    assert len(entries) == 1
    result = entries[0]
    assert result.session_id is SessionId.PODCASTS
    assert result.remote_kind == "soundcloudPodcast"
    assert result.row.title == "Freta10"
    assert result.row.detail == "podcast SoundCloud"
    assert result.accessible_label == (
        "Freta10, podcast, Dominikanie Warszawa-Freta, "
        "podcast SoundCloud, Podcasty i YouTube"
    )
    assert "152970424" not in result.accessible_label


@pytest.mark.parametrize(
    ("session_id", "remote_kind", "kind"),
    (
        (SessionId.FILES, None, "track"),
        (SessionId.RADIO, "radioStation", "station"),
        (SessionId.PODCASTS, "youtubeChannel", "podcast"),
        (SessionId.PODCASTS, None, "podcast"),
        (SessionId.WIIM, None, "wiimSource"),
        (SessionId.SONOS, None, "sonosGroup"),
        (SessionId.TIDAL, "tidalCatalog", "album"),
        (SessionId.TIDAL, None, "album"),
        (SessionId.SPOTIFY, "spotifyCatalog", "album"),
        (SessionId.SPOTIFY, None, "album"),
    ),
)
@pytest.mark.parametrize("action", (
    SearchResultAction.LIBRARY, SearchResultAction.FAVORITE,
))
def test_membership_action_keeps_search_dialog_open(
    session_id: SessionId,
    remote_kind: str | None,
    kind: str,
    action: SearchResultAction,
) -> None:
    direct_actions = []
    modal_results = []
    result = SearchEntry(
        session_id,
        Row(
            "remote-channel",
            "Kanał",
            kind,
            service_id="private-id",
            url="https://www.youtube.com/channel/UCexample/videos",
        ),
        LibraryView.PODCAST_LIBRARY,
        remote_kind=remote_kind,
    )
    dialog = SimpleNamespace(
        _lead_entry=lambda: result,
        _selected_entries=lambda: [result],
        _execute_direct_action=lambda entries, action, owner: direct_actions.append(
            (entries, action, owner)
        ),
        announcer=SimpleNamespace(say=lambda _message: None),
        EndModal=modal_results.append,
        selected_result=None,
        selected_results=[],
        selected_action=SearchResultAction.OPEN,
    )

    gui_module.SearchDialog._complete_selected(
        dialog,
        action,
    )

    assert direct_actions == [([result], action, dialog)]
    assert modal_results == []
    assert dialog.selected_result is result


def test_empty_search_library_action_keeps_dialog_and_returns_query_focus() -> None:
    calls = []
    dialog = SimpleNamespace(
        _lead_entry=lambda: None,
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        query=SimpleNamespace(SetFocus=lambda: calls.append(("focus", "query"))),
        EndModal=lambda result: calls.append(("close", result)),
    )

    gui_module.SearchDialog._complete_selected(
        dialog,
        SearchResultAction.LIBRARY,
    )

    assert calls == [
        ("say", "Najpierw wykonaj wyszukiwanie i wybierz wynik"),
        ("focus", "query"),
    ]


def test_local_search_library_action_stays_open_and_explains_safe_removal() -> None:
    spoken = []
    result = SearchEntry(
        SessionId.FILES,
        Row(
            "local-private",
            "Nagranie",
            "track",
            path="D:/Muzyka/nagranie.mp3",
        ),
        LibraryView.ALL_FILES,
    )
    frame = SimpleNamespace(
        _search_dialog_alive=lambda _dialog: True,
        announcer=SimpleNamespace(
            say=lambda _message: pytest.fail(
                "Główne okno nie może przejąć komunikatu wyszukiwarki"
            )
        ),
    )
    dialog = SimpleNamespace(
        IsShown=lambda: True,
        announcer=SimpleNamespace(say=spoken.append),
    )

    LiteFrame._execute_search_dialog_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        dialog,
    )

    assert spoken == [
        "Wynik lokalny jest już w Bibliotece. "
        "Aby usunąć wpis, przejdź skrótem Ctrl+L do Biblioteki "
        "i naciśnij Delete. Plik pozostanie na dysku"
    ]


def test_radio_search_library_confirmation_uses_dialog_and_keeps_focus_context() -> None:
    calls = []
    spoken = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:
                failed(error)

    result = SearchEntry(
        SessionId.RADIO,
        Row(
            "radio-directory:poznan",
            "Polskie Radio Poznań",
            "station",
            url="https://example.test/poznan",
        ),
        remote_kind="radioStation",
    )
    frame = SimpleNamespace(
        _radio_search_action_pending=False,
        client=SimpleNamespace(
            add_radio_station=lambda title, address: (
                calls.append((title, address))
                or {"preferredStationId": "saved-poznan", "addedCount": 1}
            )
        ),
        runner=Runner(),
        status_field=SimpleNamespace(SetLabel=lambda _message: None),
        status_bar=SimpleNamespace(show=lambda _message: None),
        announcer=SimpleNamespace(
            say=lambda _message: pytest.fail(
                "Główne okno nie może przejąć komunikatu wyszukiwarki"
            )
        ),
    )

    LiteFrame._complete_remote_radio_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        announce=spoken.append,
    )

    assert calls == [("Polskie Radio Poznań", "https://example.test/poznan")]
    assert spoken == ["Dodano do Biblioteki: Polskie Radio Poznań"]
    assert frame._radio_search_action_pending is False


def test_saved_radio_search_result_toggles_library_without_closing_dialog() -> None:
    spoken = []
    in_library = True

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    def toggle(_ids):
        nonlocal in_library
        in_library = not in_library
        return {"inLibrary": in_library, "changed": 1}

    result = SearchEntry(
        SessionId.RADIO,
        Row("saved-poznan", "Polskie Radio Poznań", "station"),
    )
    frame = SimpleNamespace(
        _search_dialog_alive=lambda _dialog: True,
        _radio_search_action_pending=False,
        client=SimpleNamespace(toggle_radio_library=toggle),
        runner=Runner(),
        announcer=SimpleNamespace(
            say=lambda _message: pytest.fail(
                "Główne okno nie może przejąć komunikatu wyszukiwarki"
            )
        ),
    )
    frame._change_saved_radio_search_result_membership = (
        lambda entries, *, announce:
        LiteFrame._change_saved_radio_search_result_membership(
            frame, entries, announce=announce,
        )
    )
    dialog = SimpleNamespace(
        IsShown=lambda: True,
        announcer=SimpleNamespace(say=spoken.append),
    )

    LiteFrame._execute_search_dialog_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        dialog,
    )

    LiteFrame._execute_search_dialog_action(
        frame, [result], SearchResultAction.LIBRARY, dialog,
    )
    assert spoken == [
        "Usunięto z Biblioteki: Polskie Radio Poznań",
        "Dodano do Biblioteki: Polskie Radio Poznań",
    ]


def test_ctrl_l_closes_search_then_uses_shared_library_command(monkeypatch) -> None:
    calls = []
    dialog = SimpleNamespace(
        EndModal=lambda result: calls.append(("close", result)),
        _open_library=lambda: calls.append(("library", Action.VIEW_LIBRARY)),
    )
    event = SimpleNamespace(
        ControlDown=lambda: True,
        ShiftDown=lambda: False,
        AltDown=lambda: False,
        GetKeyCode=lambda: ord("L"),
    )
    monkeypatch.setattr(gui_module.wx, "CallAfter", lambda callback: callback())

    gui_module.SearchDialog._on_dialog_shortcut_hook(dialog, event)

    assert calls == [
        ("close", gui_module.wx.ID_CANCEL),
        ("library", Action.VIEW_LIBRARY),
    ]


def test_spotify_search_library_confirmation_stays_inside_search_dialog() -> None:
    calls = []
    spoken = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:
                failed(error)

    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:album:private",
            "Album użytkownika",
            "album",
            service_id="privatealbum",
            service_kind="album",
        ),
        remote_kind="spotifyCatalog",
    )
    frame = SimpleNamespace(
        _spotify_membership_pending=False,
        client=SimpleNamespace(
            spotify_collection_membership=lambda items, *, mode, add=None: (
                calls.append((items, mode, add))
                or {
                    "added": True,
                    "changed": 1,
                    "message": "Dodano do Biblioteki Spotify: Album użytkownika",
                }
            )
        ),
        runner=Runner(),
        announcer=SimpleNamespace(
            say=lambda _message: pytest.fail(
                "Główne okno nie może przesłonić komunikatu wyszukiwarki"
            )
        ),
    )
    frame._change_spotify_search_result_membership = (
        lambda entries, *, favorite, announce: (
            LiteFrame._change_spotify_search_result_membership(
                frame,
                entries,
                favorite=favorite,
                announce=announce,
            )
        )
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        announce=spoken.append,
    )

    assert calls[0][1:] == ("library", None)
    assert spoken == ["Dodano do Biblioteki Spotify: Album użytkownika"]
    assert frame._spotify_membership_pending is False


def test_saved_spotify_search_result_toggles_without_closing_dialog() -> None:
    calls = []
    spoken = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:album:saved",
            "Zapisany album",
            "album",
            service_id="savedalbum",
            service_kind="album",
        ),
        LibraryView.SPOTIFY_LIBRARY,
    )
    frame = SimpleNamespace(
        _search_dialog_alive=lambda _dialog: True,
        announcer=SimpleNamespace(
            say=lambda _message: pytest.fail(
                "Komunikat ma pozostać w oknie wyszukiwania"
            )
        ),
        _change_spotify_search_result_membership=(
            lambda entries, *, favorite, add, announce: calls.append(
                (entries, favorite, add, announce)
            )
        ),
    )
    dialog = SimpleNamespace(
        IsShown=lambda: True,
        announcer=SimpleNamespace(say=spoken.append),
    )

    LiteFrame._execute_search_dialog_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        dialog,
    )

    assert len(calls) == 1
    assert calls[0][:3] == ([result], False, None)
    calls[0][3]("Usunięto z Biblioteki Spotify: Zapisany album")
    assert spoken == ["Usunięto z Biblioteki Spotify: Zapisany album"]


@pytest.mark.parametrize(("session_id", "view"), (
    (SessionId.TIDAL, LibraryView.TIDAL_LIBRARY),
    (SessionId.SPOTIFY, LibraryView.SPOTIFY_LIBRARY),
))
def test_saved_service_search_result_uses_toggle_intent_after_each_press(
    session_id: SessionId, view: LibraryView,
) -> None:
    calls = []
    entry = SearchEntry(
        session_id,
        Row("saved-album", "Album", "album", service_id="album-1"),
        view,
    )
    change = lambda entries, *, favorite, add, announce: calls.append(
        (entries, favorite, add, announce)
    )
    frame = SimpleNamespace(
        _search_dialog_alive=lambda _dialog: True,
        _change_tidal_search_result_membership=change,
        _change_spotify_search_result_membership=change,
    )
    dialog = SimpleNamespace(IsShown=lambda: True)
    for _ in range(2):
        LiteFrame._execute_search_dialog_action(
            frame, [entry], SearchResultAction.LIBRARY, dialog,
        )
    assert [(items, favorite, add) for items, favorite, add, _ in calls] == [
        ([entry], False, None), ([entry], False, None),
    ]


def test_saved_podcast_search_result_changes_source_without_navigation() -> None:
    calls = []
    spoken = []

    class Runner:
        @staticmethod
        def submit(name, work, done, _failed):
            calls.append(name)
            done(work())

    result = SearchEntry(
        SessionId.PODCASTS,
        Row("saved-channel", "Zapisany kanał", "podcast"),
        LibraryView.PODCAST_LIBRARY,
    )
    frame = SimpleNamespace(
        _podcast_library_pending=False,
        client=SimpleNamespace(
            toggle_podcast_library=lambda ids: (
                calls.append(("toggle", ids)) or {"added": False}
            )
        ),
        runner=Runner(),
        _refresh_visible_podcast_search_membership=(
            lambda subscription_id, **kwargs: calls.append(
                ("refresh", subscription_id, kwargs)
            )
        ),
    )

    LiteFrame._change_saved_podcast_search_result_membership(
        frame,
        [result],
        announce=spoken.append,
    )

    assert ("toggle", ["saved-channel"]) in calls
    assert (
        "refresh",
        "saved-channel",
        {"preferred_episode_id": None, "source_removed": True},
    ) in calls
    assert spoken == ["Usunięto z Biblioteki: Zapisany kanał"]
    assert frame._podcast_library_pending is False


def test_saved_youtube_video_removes_only_the_video_from_internet_media() -> None:
    calls = []
    spoken = []

    class Runner:
        @staticmethod
        def submit(name, work, done, _failed):
            calls.append(name)
            done(work())

    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "saved-video",
            "Zapisany film",
            "episode",
            parent_id=gui_module.PUBLIC_INTERNET_MEDIA_SAVED_ID,
        ),
        LibraryView.PODCAST_EPISODES,
        parent_id=gui_module.PUBLIC_INTERNET_MEDIA_SAVED_ID,
    )
    frame = SimpleNamespace(
        _podcast_library_pending=False,
        client=SimpleNamespace(
            remove_profile_items=lambda session, view, ids: calls.append(
                ("remove", session, view, ids)
            )
        ),
        runner=Runner(),
        _refresh_visible_podcast_search_membership=(
            lambda subscription_id, **kwargs: calls.append(
                ("refresh", subscription_id, kwargs)
            )
        ),
    )

    LiteFrame._change_saved_podcast_search_result_membership(
        frame,
        [result],
        announce=spoken.append,
    )

    assert (
        "remove",
        SessionId.PODCASTS.value,
        LibraryView.PODCAST_EPISODES.value,
        ["saved-video"],
    ) in calls
    assert spoken == [
        "Usunięto z Biblioteki, Media internetowe: Zapisany film"
    ]
    assert frame._podcast_library_pending is False


def test_spotify_track_library_action_explains_the_favorites_shortcut() -> None:
    spoken = []
    result = SearchEntry(
        SessionId.SPOTIFY,
        Row(
            "spotify:track:private",
            "Utwór",
            "track",
            service_id="privatetrack",
            service_kind="track",
        ),
        remote_kind="spotifyCatalog",
    )
    frame = SimpleNamespace(
        _spotify_membership_pending=False,
        client=SimpleNamespace(),
        announcer=SimpleNamespace(say=lambda _message: None),
    )

    LiteFrame._change_spotify_search_result_membership(
        frame,
        [result],
        favorite=False,
        announce=spoken.append,
    )

    assert spoken == [
        "Do Biblioteki Spotify wybierz album, wykonawcę albo podcast. "
        "Utwór i odcinek dodaj do Ulubionych skrótem Ctrl+Shift+U"
    ]


@pytest.mark.parametrize(
    ("remote_kind", "title", "address"),
    (
        (
            "youtubeChannel",
            "Kanał YouTube",
            "https://www.youtube.com/channel/UCexample/videos",
        ),
        (
            "youtubeVideo",
            "Film YouTube",
            "https://www.youtube.com/watch?v=abc123",
        ),
        ("rss", "Podcast", "https://example.test/feed.xml"),
        (
            "soundcloudPodcast",
            "Podcast SoundCloud",
            "https://feeds.soundcloud.com/users/"
            "soundcloud:users:878535568/sounds.rss",
        ),
    ),
)
def test_remote_library_write_is_confirmed_inside_search_dialog(
    remote_kind: str,
    title: str,
    address: str,
) -> None:
    calls = []
    spoken = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    client = SimpleNamespace(
        add_podcast_source=lambda received_address, received_title: (
            calls.append((received_address, received_title))
            or {
                "subscriptionId": "saved-source",
                "preferredEpisodeId": (
                    "saved-video" if remote_kind == "youtubeVideo" else None
                ),
                "title": received_title,
                "added": True,
                "restored": False,
            }
        )
    )
    frame = SimpleNamespace(
        _podcast_add_pending=False,
        client=client,
        status_field=SimpleNamespace(SetLabel=lambda value: calls.append(("status", value))),
        status_bar=SimpleNamespace(show=lambda value: calls.append(("bar", value))),
        announcer=SimpleNamespace(
            say=lambda _value: pytest.fail(
                "Główne okno nie może przesłonić komunikatu wyszukiwarki"
            )
        ),
        runner=Runner(),
        _refresh_visible_podcast_library_after_search_add=(
            lambda subscription_id: calls.append(("refresh", subscription_id))
        ),
    )
    result = SearchEntry(
        SessionId.PODCASTS,
        Row("remote-result", title, "podcast", url=address),
        LibraryView.PODCAST_LIBRARY,
        remote_kind=remote_kind,
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        announce=spoken.append,
    )

    assert (address, title) in calls
    expected_confirmation = (
        f"Dodano do Biblioteki, Media internetowe: {title}"
        if remote_kind == "youtubeVideo"
        else f"Dodano do Biblioteki: {title}"
    )
    assert spoken == ["Przygotowywanie wyniku katalogu", expected_confirmation]
    assert ("refresh", "saved-source") in calls
    assert frame._podcast_add_pending is False


def test_remote_library_removes_already_saved_podcast() -> None:
    spoken = []
    removed = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        _podcast_add_pending=False,
        client=SimpleNamespace(
            add_podcast_source=lambda _address, title: {
                "subscriptionId": "saved-source",
                "preferredEpisodeId": None,
                "title": title,
                "added": False,
                "restored": False,
            },
            toggle_podcast_library=lambda ids: (
                removed.append(ids) or {"added": False, "changed": 1}
            ),
        ),
        status_field=SimpleNamespace(SetLabel=lambda _value: None),
        status_bar=SimpleNamespace(show=lambda _value: None),
        announcer=SimpleNamespace(say=lambda _value: None),
        runner=Runner(),
        _refresh_visible_podcast_library_after_search_add=lambda _value: None,
        _refresh_visible_podcast_search_membership=lambda *args, **kwargs: None,
    )
    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "remote-channel",
            "Kanał YouTube",
            "podcast",
            url="https://www.youtube.com/channel/UCexample/videos",
        ),
        LibraryView.PODCAST_LIBRARY,
        remote_kind="youtubeChannel",
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        announce=spoken.append,
    )

    assert removed == [["saved-source"]]
    assert spoken[-1] == "Usunięto z Biblioteki: Kanał YouTube"


def test_saved_youtube_video_names_its_library_collection() -> None:
    spoken = []
    removed = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        _podcast_add_pending=False,
        client=SimpleNamespace(
            add_podcast_source=lambda _address, title: {
                "subscriptionId": "internet-media:public",
                "preferredEpisodeId": "saved-video",
                "title": title,
                "sourceKind": "internetMedia",
                "added": False,
                "restored": False,
            },
            remove_profile_items=lambda session, view, ids: (
                removed.append((session, view, ids)) or {"removedCount": 1}
            ),
        ),
        status_field=SimpleNamespace(SetLabel=lambda _value: None),
        status_bar=SimpleNamespace(show=lambda _value: None),
        announcer=SimpleNamespace(say=lambda _value: None),
        runner=Runner(),
        _refresh_visible_podcast_library_after_search_add=lambda _value: None,
        _refresh_visible_podcast_search_membership=lambda *args, **kwargs: None,
    )
    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "remote-video",
            "Film YouTube",
            "episode",
            url="https://www.youtube.com/watch?v=abc123",
        ),
        LibraryView.PODCAST_EPISODES,
        remote_kind="youtubeVideo",
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.LIBRARY,
        announce=spoken.append,
    )

    assert removed == [(
        SessionId.PODCASTS.value, LibraryView.PODCAST_EPISODES.value,
        ["saved-video"],
    )]
    assert spoken[-1] == (
        "Usunięto z Biblioteki, Media internetowe: Film YouTube"
    )


def test_search_add_refreshes_visible_podcast_library_silently() -> None:
    opened = []
    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.PODCAST_LIBRARY,
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        _open_podcast_library=lambda **kwargs: opened.append(kwargs),
    )

    LiteFrame._refresh_visible_podcast_library_after_search_add(
        frame,
        "new-channel",
    )

    assert opened == [{
        "preferred_id": "new-channel",
        "announce_result": False,
    }]


def test_remote_youtube_queue_materializes_then_uses_real_list_action() -> None:
    calls = []
    selected = []
    dispatched = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    client = SimpleNamespace(
        add_podcast_source=lambda address, title: (
            calls.append(("add", address, title))
            or {
                "subscriptionId": "internet-media",
                "preferredEpisodeId": "saved-episode",
                "title": title,
            }
        )
    )
    opened = []
    frame = SimpleNamespace(
        _podcast_add_pending=False,
        client=client,
        status_field=SimpleNamespace(SetLabel=lambda value: calls.append(("status", value))),
        status_bar=SimpleNamespace(show=lambda value: calls.append(("bar", value))),
        announcer=SimpleNamespace(say=lambda value: calls.append(("say", value))),
        runner=Runner(),
        _switch_session=lambda session: calls.append(("session", session)),
        _open_podcast_view=lambda intent, **kwargs: opened.append((intent, kwargs)),
        _active_list=lambda: SimpleNamespace(
            select_item_ids=lambda values: selected.extend(values)
        ),
        _dispatch=dispatched.append,
    )
    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "remote-video",
            "Materiał",
            "episode",
            url="https://www.youtube.com/watch?v=abc123",
        ),
        LibraryView.PODCAST_EPISODES,
        parent_id="remote:internet-media",
        remote_kind="youtubeVideo",
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.QUEUE,
    )

    assert ("add", result.row.url, "Materiał") in calls
    assert opened[0][0].subscription_id == "internet-media"
    assert opened[0][0].preferred_id == "saved-episode"
    opened[0][1]["after_refresh"]()
    assert selected == ["saved-episode"]
    assert dispatched == [Action.ADD_TO_QUEUE]
    assert frame._podcast_add_pending is False


def test_remote_podcast_favorite_is_added_then_favorited_without_navigation() -> None:
    calls = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    client = SimpleNamespace(
        add_podcast_source=lambda _address, title: {
            "subscriptionId": "saved-podcast",
            "preferredEpisodeId": None,
            "title": title,
        },
        toggle_podcast_favorites=lambda subscriptions, episodes: (
            calls.append(("favorite", subscriptions, episodes))
            or {"favorite": True, "changed": 1}
        ),
    )
    frame = SimpleNamespace(
        _podcast_add_pending=False,
        client=client,
        status_field=SimpleNamespace(SetLabel=lambda value: calls.append(("status", value))),
        status_bar=SimpleNamespace(show=lambda value: calls.append(("bar", value))),
        announcer=SimpleNamespace(say=lambda value: calls.append(("say", value))),
        runner=Runner(),
    )
    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "remote-podcast",
            "Podcast",
            "podcast",
            url="https://example.test/feed.xml",
        ),
        LibraryView.PODCAST_LIBRARY,
        remote_kind="rss",
    )

    LiteFrame._complete_remote_search_result_action(
        frame,
        [result],
        SearchResultAction.FAVORITE,
    )

    assert ("favorite", ["saved-podcast"], []) in calls
    assert ("say", "Dodano do ulubionych: Podcast") in calls
    assert frame._podcast_add_pending is False


def test_search_dialog_intercepts_enter_before_default_ok_processing() -> None:
    source = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    text = source.read_text(encoding="utf-8")
    dialog = text[
        text.index("class SearchDialog") :
        text.index("class GeneralPlaybackOptionsDialog")
    ]
    assert "self.Bind(wx.EVT_CHAR_HOOK, self._on_dialog_shortcut_hook)" in dialog
    assert "if focus is self.query:" in dialog
    assert "self._on_query_key(event)" in dialog
    assert "if focus is self.results:" in dialog


@pytest.mark.parametrize(("session_id", "kind"), (
    (SessionId.FILES, "track"),
    (SessionId.RADIO, "station"),
    (SessionId.PODCASTS, "podcast"),
))
def test_saved_search_favorite_reports_both_confirmed_directions(
    session_id: SessionId, kind: str,
) -> None:
    spoken = []
    calls = []
    favorite = False

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    def toggle(*args, **kwargs):
        nonlocal favorite
        calls.append((args, kwargs))
        favorite = not favorite
        return {"favorite": favorite, "changed": 1}

    row = Row("saved-item", "Pozycja", kind, path="D:/Muzyka/pozycja.mp3")
    frame = SimpleNamespace(
        client=SimpleNamespace(
            toggle_profile_favorites=toggle,
            toggle_podcast_favorites=toggle,
        ),
        runner=Runner(),
    )
    entry = SearchEntry(session_id, row)
    for _ in range(2):
        LiteFrame._change_saved_search_result_favorites(
            frame, [entry], announce=spoken.append,
        )

    assert spoken == [
        "Dodano do ulubionych: Pozycja",
        "Usunięto z ulubionych: Pozycja",
    ]
    assert len(calls) == 2
    assert frame._search_favorite_pending is False


@pytest.mark.parametrize(("session_id", "favorite", "kind"), (
    (SessionId.TIDAL, False, "album"),
    (SessionId.TIDAL, True, "track"),
    (SessionId.SPOTIFY, False, "album"),
    (SessionId.SPOTIFY, True, "track"),
))
def test_online_search_membership_uses_confirmed_toggle_twice(
    session_id: SessionId, favorite: bool, kind: str,
) -> None:
    spoken = []
    requested = []
    member = False
    destination = "Ulubionych" if favorite else "Biblioteki"
    service = "TIDAL" if session_id is SessionId.TIDAL else "Spotify"

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    def membership(_items, *, mode, add):
        nonlocal member
        requested.append((mode, add))
        assert add is None
        member = not member
        return {
            "added": member,
            "changed": 1,
            "message": (
                f"{'Dodano do' if member else 'Usunięto z'} "
                f"{destination} {service}: Pozycja"
            ),
        }

    client = SimpleNamespace(
        tidal_collection_membership=membership,
        spotify_collection_membership=membership,
    )
    frame = SimpleNamespace(
        client=client, runner=Runner(),
        _spotify_membership_pending=False,
        _tidal_membership_pending=False,
    )
    entry = SearchEntry(
        session_id,
        Row("online-item", "Pozycja", kind, service_id="service-item"),
        remote_kind="tidalCatalog" if service == "TIDAL" else "spotifyCatalog",
    )
    change = (
        LiteFrame._change_tidal_search_result_membership
        if service == "TIDAL"
        else LiteFrame._change_spotify_search_result_membership
    )
    for _ in range(2):
        change(frame, [entry], favorite=favorite, announce=spoken.append)

    assert requested == [("favorite" if favorite else "library", None)] * 2
    assert spoken == [
        f"Dodano do {destination} {service}: Pozycja",
        f"Usunięto z {destination} {service}: Pozycja",
    ]


def test_radio_directory_library_repeated_presses_add_remove_readd() -> None:
    spoken = []
    in_library = False
    toggles = []
    add_calls = 0

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:
                failed(error)

    def add_station(_title, _address):
        nonlocal in_library, add_calls
        add_calls += 1
        promoted = not in_library
        in_library = True
        return {
            "preferredStationId": "station-1",
            "addedCount": int(promoted and add_calls == 1),
            "promotedCount": int(promoted and add_calls > 1),
        }

    def toggle(ids):
        nonlocal in_library
        toggles.append(ids)
        in_library = not in_library
        return {"inLibrary": in_library, "changed": 1}

    frame = SimpleNamespace(
        _radio_search_action_pending=False,
        client=SimpleNamespace(
            add_radio_station=add_station,
            toggle_radio_library=toggle,
        ),
        runner=Runner(),
        status_field=SimpleNamespace(SetLabel=lambda _value: None),
        status_bar=SimpleNamespace(show=lambda _value: None),
    )
    entry = SearchEntry(
        SessionId.RADIO,
        Row("directory-1", "Stacja", "station", url="https://example.test/live"),
        remote_kind="radioStation",
    )
    for _ in range(3):
        LiteFrame._complete_remote_radio_search_result_action(
            frame, [entry], SearchResultAction.LIBRARY, announce=spoken.append,
        )

    assert toggles == [["station-1"]]
    assert in_library is True
    assert spoken == [
        "Dodano do Biblioteki: Stacja",
        "Usunięto z Biblioteki: Stacja",
        "Dodano do Biblioteki: Stacja",
    ]


@pytest.mark.parametrize("video", (False, True))
def test_podcast_directory_library_repeated_presses_add_remove_readd(
    video: bool,
) -> None:
    spoken = []
    in_library = False
    removed = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:
                failed(error)

    def add_source(_address, title):
        nonlocal in_library
        added = not in_library
        in_library = True
        return {
            "subscriptionId": "source-1",
            "preferredEpisodeId": "episode-1" if video else None,
            "title": title,
            "added": added,
            "restored": False,
        }

    def remove_source(ids):
        nonlocal in_library
        removed.append(ids)
        in_library = False
        return {"added": False, "changed": 1}

    def remove_video(_session, _view, ids):
        nonlocal in_library
        removed.append(ids)
        in_library = False
        return {"removedCount": 1}

    frame = SimpleNamespace(
        _podcast_add_pending=False,
        client=SimpleNamespace(
            add_podcast_source=add_source,
            toggle_podcast_library=remove_source,
            remove_profile_items=remove_video,
        ),
        runner=Runner(),
        status_field=SimpleNamespace(SetLabel=lambda _value: None),
        status_bar=SimpleNamespace(show=lambda _value: None),
        _refresh_visible_podcast_library_after_search_add=lambda _id: None,
        _refresh_visible_podcast_search_membership=lambda *args, **kwargs: None,
    )
    entry = SearchEntry(
        SessionId.PODCASTS,
        Row("remote-1", "Pozycja", "episode" if video else "podcast",
            url="https://example.test/item"),
        remote_kind="youtubeVideo" if video else "rss",
    )
    for _ in range(3):
        LiteFrame._complete_remote_search_result_action(
            frame, [entry], SearchResultAction.LIBRARY,
            announce=spoken.append,
        )

    assert removed == [["episode-1" if video else "source-1"]]
    assert in_library is True
    destination = "Biblioteki, Media internetowe" if video else "Biblioteki"
    assert spoken[1::2] == [
        f"Dodano do {destination}: Pozycja",
        f"Usunięto z {destination}: Pozycja",
        f"Dodano do {destination}: Pozycja",
    ]


def test_radioarchiwista_search_favorite_toggles_recording_twice() -> None:
    spoken = []
    favorite = False
    recording = SimpleNamespace(title="Nagranie")

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    def toggle(_recording):
        nonlocal favorite
        favorite = not favorite
        return favorite

    frame = SimpleNamespace(
        _radioarchiwista_source=lambda: SimpleNamespace(
            recording_by_id=lambda _id: recording,
            toggle_favorite=toggle,
        ),
        runner=Runner(),
    )
    entry = SearchEntry(
        SessionId.RADIOARCHIWISTA,
        Row("recording-1", "Nagranie", "recording"),
    )
    for _ in range(2):
        LiteFrame._change_saved_search_result_favorites(
            frame, [entry], announce=spoken.append,
        )
    assert spoken == [
        "Dodano do ulubionych: Nagranie",
        "Usunięto z ulubionych: Nagranie",
    ]


@pytest.mark.parametrize("session_id", (SessionId.WIIM, SessionId.SONOS))
def test_speaker_search_favorite_explains_unavailable_action(
    session_id: SessionId,
) -> None:
    spoken = []
    entry = SearchEntry(session_id, Row("source-1", "Źródło", "station"))
    LiteFrame._change_saved_search_result_favorites(
        SimpleNamespace(), [entry], announce=spoken.append,
    )
    assert spoken == ["Ulubione nie są dostępne dla tego wyniku"]
