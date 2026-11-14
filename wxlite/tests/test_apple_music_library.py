"""The optional Apple library stays Test-only and returns sanitized records."""

from __future__ import annotations

from dataclasses import dataclass
from types import SimpleNamespace
import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.apple_music_credentials import AppleMusicWebCredentials
from amc_wx_lite.apple_music_library import (
    AppleMusicLibraryError,
    AppleMusicNotConnected,
    AppleMusicPageCursor,
    library_membership_direction,
    open_apple_music_library,
)
from amc_wx_lite.apple_music_source import records_from_resources
from amc_wx_lite.profile_layout import private_sandbox
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE


PRIVATE_TOKEN = "header.private.signature"
PRIVATE_MEDIA_TOKEN = "private-media-user-token"
PRIVATE_COOKIE = "media-user-token=private-session"


class FakeStore:
    def __init__(self, value: AppleMusicWebCredentials | None) -> None:
        self.value = value

    def read(self) -> AppleMusicWebCredentials | None:
        return self.value


@dataclass
class ExternalCredentials:
    developer_token: str
    media_user_token: str
    cookie_header: str


class ExternalClient:
    last_instance: "ExternalClient | None" = None

    def __init__(self, credentials: ExternalCredentials, **options: object) -> None:
        self.credentials = credentials
        self.options = options
        self.calls: list[object] = []
        self.closed = False
        ExternalClient.last_instance = self

    @staticmethod
    def _resource(kind: str, identifier: str, name: str) -> dict[str, object]:
        return {
            "id": identifier,
            "type": kind,
            "attributes": {"name": name},
            "technical": PRIVATE_TOKEN,
        }

    def current_user_saved_tracks(self):
        self.calls.append("songs")
        return [self._resource("library-songs", "i.song", "Utwór")]

    def current_user_saved_albums(self):
        self.calls.append("albums")
        return [self._resource("library-albums", "l.album", "Album")]

    def current_user_saved_artists(self):
        self.calls.append("artists")
        return [self._resource("library-artists", "r.artist", "Wykonawca")]

    def current_user_playlists(self):
        self.calls.append("playlists")
        return [self._resource("library-playlists", "p.list", "Moja lista")]

    def playlist_tracks(self, playlist_id: str):
        self.calls.append(("playlist", playlist_id))
        return [self._resource("library-songs", "i.inside", "W playliście")]

    def library_add(self, **items):
        self.calls.append(("library-add", items))

    def library_remove_song(self, item_id: str):
        self.calls.append(("library-remove-song", item_id))

    def library_remove_album(self, item_id: str):
        self.calls.append(("library-remove-album", item_id))

    def _get(self, path: str, params=None):
        self.calls.append(("page", path, params))
        if path == "/v1/me/library/search":
            return {"results": {
                "library-songs": {"data": [self._resource(
                    "library-songs", "i.outside", "Utwór spoza strony"
                )]},
                "library-albums": {"data": [self._resource(
                    "library-albums", "l.outside", "Album spoza strony"
                )]},
            }}
        kind_by_path = {
            "/v1/me/library/songs": ("library-songs", "Utwór stronicowany"),
            "/v1/me/library/albums": ("library-albums", "Album stronicowany"),
            "/v1/me/library/artists": ("library-artists", "Wykonawca stronicowany"),
            "/v1/me/library/playlists": ("library-playlists", "Playlista stronicowana"),
        }
        base_path = path.split("?", 1)[0]
        if base_path.endswith("/tracks"):
            kind, name = "library-songs", "Utwór playlisty"
        else:
            kind, name = kind_by_path[base_path]
        page = "2" if "offset=25" in path else "1"
        result = {
            "data": [self._resource(kind, "private." + page, name + " " + page)]
        }
        if page == "1":
            result["next"] = base_path + "?offset=25&private=" + PRIVATE_TOKEN
        return result

    def close(self) -> None:
        self.closed = True


MODULE = SimpleNamespace(
    AppleMusicCredentials=ExternalCredentials,
    AppleMusic=ExternalClient,
)


def credentials() -> AppleMusicWebCredentials:
    return AppleMusicWebCredentials(
        PRIVATE_TOKEN,
        PRIVATE_MEDIA_TOKEN,
        PRIVATE_COOKIE,
    )


class AppleMusicLibraryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.layout = private_sandbox(Path(self.temporary.name))

    def tearDown(self) -> None:
        self.temporary.cleanup()
        ExternalClient.last_instance = None

    def open(self):
        return open_apple_music_library(
            self.layout,
            profile=TEST_PROFILE,
            store=FakeStore(credentials()),
            client_module=MODULE,
        )

    def test_open_builds_polish_client_without_exposing_credentials(self) -> None:
        library = self.open()
        external = ExternalClient.last_instance

        self.assertIsNotNone(external)
        self.assertEqual(external.options, {"language": "pl-PL"})
        self.assertEqual(external.credentials.developer_token, PRIVATE_TOKEN)
        self.assertNotIn(PRIVATE_TOKEN, str(library.view("songs").records[0]))

    def test_each_read_only_view_calls_one_explicit_method(self) -> None:
        library = self.open()
        headings = []
        labels = []
        for view in ("songs", "albums", "artists", "playlists"):
            result = library.view(view)
            headings.append(result.heading)
            labels.append(result.records[0].accessible_label)

        self.assertEqual(
            headings,
            [
                "Utwory Apple Music",
                "Albumy Apple Music",
                "Wykonawcy Apple Music",
                "Playlisty Apple Music",
            ],
        )
        self.assertEqual(
            ExternalClient.last_instance.calls,
            ["songs", "albums", "artists", "playlists"],
        )
        self.assertFalse(any(PRIVATE_TOKEN in label for label in labels))

    def test_bounded_page_keeps_continuation_out_of_rows_and_labels(self) -> None:
        library = self.open()

        first = library.view_page("songs", page_size=25)
        cursor = first.next_cursor
        second = library.view_page("songs", cursor=cursor)

        self.assertEqual(first.records[0].row.title, "Utwór stronicowany 1")
        self.assertIsInstance(cursor, AppleMusicPageCursor)
        self.assertEqual(str(cursor), "Dalsza strona Apple Music")
        self.assertEqual(repr(cursor), "AppleMusicPageCursor()")
        self.assertNotIn(PRIVATE_TOKEN, str(first.rows))
        self.assertNotIn(PRIVATE_TOKEN, str(cursor))
        self.assertEqual(second.records[0].row.title, "Utwór stronicowany 2")
        self.assertIsNone(second.next_cursor)
        self.assertIn(
            ("page", "/v1/me/library/songs", {"l": "pl-PL", "limit": 25}),
            ExternalClient.last_instance.calls,
        )
        self.assertIn(
            (
                "page",
                "/v1/me/library/songs?offset=25&private=" + PRIVATE_TOKEN
                + "&limit=25&l=pl-PL",
                None,
            ),
            ExternalClient.last_instance.calls,
        )

    def test_default_page_has_one_hundred_items_and_search_uses_server(self) -> None:
        library = self.open()
        first_page = library.view_page("songs")
        library.view_page("songs", cursor=first_page.next_cursor)
        results = library.search_library("Spoza")

        self.assertEqual(
            [record.row.title for record in results],
            ["Utwór spoza strony", "Album spoza strony"],
        )
        self.assertEqual(results[0].row.kind, "track")
        self.assertNotIn(PRIVATE_TOKEN, str(results))
        self.assertIn(
            ("page", "/v1/me/library/songs", {"l": "pl-PL", "limit": 100}),
            ExternalClient.last_instance.calls,
        )
        self.assertIn(
            ("page", "/v1/me/library/songs?offset=25&private=" + PRIVATE_TOKEN
             + "&limit=100&l=pl-PL", None),
            ExternalClient.last_instance.calls,
        )
        self.assertIn(
            ("page", "/v1/me/library/search", {
                "term": "Spoza",
                "types": "library-songs,library-albums,library-artists,library-playlists",
                "limit": 25,
                "l": "pl-PL",
            }),
            ExternalClient.last_instance.calls,
        )

    def test_page_rejects_wrong_view_size_and_external_cursor(self) -> None:
        library = self.open()
        wrong_view = AppleMusicPageCursor(
            view="albums",
            _path="/v1/me/library/albums?offset=25",
            _base_path="/v1/me/library/albums",
        )
        for invalid_size in (True, 0, 101):
            with self.subTest(size=invalid_size):
                with self.assertRaises(AppleMusicLibraryError):
                    library.view_page("songs", page_size=invalid_size)
        with self.assertRaises(AppleMusicLibraryError):
            library.view_page("songs", cursor=wrong_view)
        forged_cursor = AppleMusicPageCursor(
            view="songs",
            _path="https://example.com/v1/me/library/songs?offset=25",
            _base_path="/v1/me/library/songs",
        )
        with self.assertRaises(AppleMusicLibraryError):
            library.view_page("songs", cursor=forged_cursor)

        class ExternalCursorClient(ExternalClient):
            def _get(self, _path, _params=None):
                return {
                    "data": [],
                    "next": "https://example.com/v1/me/library/songs?secret="
                    + PRIVATE_TOKEN,
                }

        module = SimpleNamespace(
            AppleMusicCredentials=ExternalCredentials,
            AppleMusic=ExternalCursorClient,
        )
        guarded = open_apple_music_library(
            self.layout,
            profile=TEST_PROFILE,
            store=FakeStore(credentials()),
            client_module=module,
        )
        with self.assertRaises(AppleMusicLibraryError) as caught:
            guarded.view_page("songs")
        self.assertNotIn(PRIVATE_TOKEN, str(caught.exception))

    def test_playlist_pages_are_tied_to_one_private_playlist(self) -> None:
        library = self.open()
        playlist = library.view("playlists").records[0]

        first = library.playlist_tracks_page(playlist, page_size=25)
        second = library.playlist_tracks_page(
            playlist, cursor=first.next_cursor
        )

        self.assertEqual(first.heading, "Playlista: Moja lista")
        self.assertEqual(first.records[0].row.title, "Utwór playlisty 1")
        self.assertEqual(second.records[0].row.title, "Utwór playlisty 2")
        self.assertIsNone(second.next_cursor)
        self.assertNotIn("p.list", first.heading)
        self.assertIn(
            ("page", "/v1/me/library/playlists/p.list/tracks?offset=25&private="
             + PRIVATE_TOKEN + "&limit=25&l=pl-PL", None),
            ExternalClient.last_instance.calls,
        )

        other_cursor = AppleMusicPageCursor(
            view="playlist-tracks:p.other",
            _path="/v1/me/library/playlists/p.other/tracks?offset=25",
            _base_path="/v1/me/library/playlists/p.other/tracks",
        )
        with self.assertRaises(AppleMusicLibraryError):
            library.playlist_tracks_page(playlist, cursor=other_cursor)

    def test_album_pages_are_tied_to_one_private_album(self) -> None:
        library = self.open()
        album = library.view("albums").records[0]

        first = library.album_tracks_page(album, page_size=25)
        second = library.album_tracks_page(album, cursor=first.next_cursor)

        self.assertEqual(first.heading, "Album: Album")
        self.assertEqual(first.records[0].row.title, "Utwór playlisty 1")
        self.assertEqual(second.records[0].row.title, "Utwór playlisty 2")
        self.assertIsNone(second.next_cursor)
        self.assertNotIn("l.album", first.heading)
        self.assertIn(
            ("page", "/v1/me/library/albums/l.album/tracks",
             {"l": "pl-PL", "limit": 25}),
            ExternalClient.last_instance.calls,
        )
        self.assertIn(
            ("page", "/v1/me/library/albums/l.album/tracks?offset=25&private="
             + PRIVATE_TOKEN + "&limit=25&l=pl-PL", None),
            ExternalClient.last_instance.calls,
        )

        other_cursor = AppleMusicPageCursor(
            view="album-tracks:l.other",
            _path="/v1/me/library/albums/l.other/tracks?offset=25",
            _base_path="/v1/me/library/albums/l.other/tracks",
        )
        with self.assertRaises(AppleMusicLibraryError):
            library.album_tracks_page(album, cursor=other_cursor)

    def test_playlist_uses_private_id_but_heading_uses_only_name(self) -> None:
        library = self.open()
        playlist = library.view("playlists").records[0]

        result = library.playlist_tracks(playlist)

        self.assertEqual(result.heading, "Playlista: Moja lista")
        self.assertEqual(result.records[0].row.title, "W playliście")
        self.assertIn(("playlist", "p.list"), ExternalClient.last_instance.calls)
        self.assertNotIn("p.list", result.heading)

    def test_playlist_rejects_a_non_playlist_target(self) -> None:
        library = self.open()
        song = library.view("songs").records[0]

        with self.assertRaises(AppleMusicLibraryError):
            library.playlist_tracks(song)

    def test_library_membership_adds_catalog_and_removes_library_records(self) -> None:
        library = self.open()
        library_song = records_from_resources([{
            "id": "i.song",
            "type": "library-songs",
            "attributes": {
                "name": "W Bibliotece",
                "playParams": {"catalogId": "catalog.song"},
            },
        }])[0]
        catalog_album = records_from_resources([{
            "id": "catalog.album",
            "type": "albums",
            "attributes": {"name": "Z katalogu"},
        }])[0]

        removed = library.toggle_library(library_song)
        added = library.toggle_library(catalog_album)

        self.assertFalse(removed.added)
        self.assertEqual(removed.item_kind, "track")
        self.assertTrue(added.added)
        self.assertEqual(added.item_kind, "album")
        self.assertEqual(library_membership_direction(library_song), "remove")
        self.assertEqual(library_membership_direction(catalog_album), "add")
        self.assertIn(
            ("library-remove-song", "i.song"),
            ExternalClient.last_instance.calls,
        )
        self.assertIn(
            ("library-add", {"albums": ["catalog.album"]}),
            ExternalClient.last_instance.calls,
        )

    def test_library_membership_rejects_artist_and_hides_provider_failure(self) -> None:
        library = self.open()
        artist = records_from_resources([{
            "id": "catalog.artist",
            "type": "artists",
            "attributes": {"name": "Wykonawca"},
        }])[0]

        self.assertIsNone(library_membership_direction(artist))
        with self.assertRaises(AppleMusicLibraryError) as caught:
            library.toggle_library(artist)
        self.assertEqual(
            str(caught.exception),
            "Nie udało się zmienić Biblioteki Apple Music.",
        )

    def test_missing_connection_has_one_user_facing_message(self) -> None:
        with self.assertRaises(AppleMusicNotConnected) as caught:
            open_apple_music_library(
                self.layout,
                profile=TEST_PROFILE,
                store=FakeStore(None),
                client_module=MODULE,
            )

        self.assertEqual(str(caught.exception), "Apple Music nie jest zalogowane.")

    def test_public_profile_refuses_before_touching_the_dependency(self) -> None:
        with self.assertRaises(AppleMusicLibraryError) as caught:
            open_apple_music_library(
                self.layout,
                profile=PUBLIC_PROFILE,
                store=FakeStore(credentials()),
                client_module=SimpleNamespace(),
            )

        self.assertEqual(
            str(caught.exception),
            "Nie udało się odczytać Biblioteki Apple Music.",
        )

    def test_provider_exception_never_reaches_user_message(self) -> None:
        class FailingClient(ExternalClient):
            def current_user_saved_tracks(self):
                raise RuntimeError(PRIVATE_TOKEN + PRIVATE_COOKIE)

        module = SimpleNamespace(
            AppleMusicCredentials=ExternalCredentials,
            AppleMusic=FailingClient,
        )
        library = open_apple_music_library(
            self.layout,
            profile=TEST_PROFILE,
            store=FakeStore(credentials()),
            client_module=module,
        )

        with self.assertRaises(AppleMusicLibraryError) as caught:
            library.view("songs")

        message = str(caught.exception)
        self.assertNotIn(PRIVATE_TOKEN, message)
        self.assertNotIn(PRIVATE_COOKIE, message)

    def test_unknown_view_does_not_echo_technical_value(self) -> None:
        library = self.open()

        with self.assertRaises(AppleMusicLibraryError) as caught:
            library.view("internal-view-with-" + PRIVATE_TOKEN)

        self.assertNotIn(PRIVATE_TOKEN, str(caught.exception))

    def test_context_manager_closes_the_private_client(self) -> None:
        with self.open() as library:
            library.view("albums")

        self.assertTrue(ExternalClient.last_instance.closed)

    def test_result_rows_are_plain_amc_rows_not_provider_records(self) -> None:
        library = self.open()
        result = library.view("artists")

        self.assertEqual(result.rows[0].title, "Wykonawca")
        self.assertNotIsInstance(result.rows[0], dict)
        raw = records_from_resources([{
            "id": "r.other",
            "type": "library-artists",
            "attributes": {"name": "Inny wykonawca"},
        }])
        self.assertEqual(raw[0].row.title, "Inny wykonawca")


if __name__ == "__main__":
    unittest.main()
