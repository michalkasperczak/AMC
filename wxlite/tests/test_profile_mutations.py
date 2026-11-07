"""End-to-end witness for F2/Delete profile mutations through the C# host."""

from __future__ import annotations

import json
import os
import sqlite3
import tempfile
import threading
from contextlib import closing, contextmanager
import ctypes
from ctypes import wintypes
from pathlib import Path

from amc_wx_lite.host_client import HostError, LiteHostClient


ROOT = Path(__file__).resolve().parents[2]
HOST = (
    ROOT
    / "src"
    / "AccessibleMediaController.LiteHost"
    / "bin"
    / "Release"
    / "net8.0-windows10.0.19041.0"
    / "amc_lite_host.exe"
)


@contextmanager
def _exclusive_windows_file(path: Path):
    """Trzymaj prawdziwy uchwyt bez FILE_SHARE_DELETE podczas próby hosta."""

    handle = _open_exclusive_windows_file(path)
    try:
        yield
    finally:
        _close_windows_handle(handle)


def _open_exclusive_windows_file(path: Path) -> int:
    """Otwórz plik bez współdzielenia, tak jak uchwyt jeszcze gaszonego dekodera."""

    if os.name != "nt":
        raise RuntimeError("Test blokady pliku wymaga Windows")
    create_file = ctypes.windll.kernel32.CreateFileW
    create_file.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    create_file.restype = wintypes.HANDLE
    handle = create_file(
        str(path),
        0x80000000,  # GENERIC_READ
        0,  # bez współdzielenia: także bez FILE_SHARE_DELETE
        None,
        3,  # OPEN_EXISTING
        0x80,  # FILE_ATTRIBUTE_NORMAL
        None,
    )
    if handle == wintypes.HANDLE(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    return int(handle)


def _close_windows_handle(handle: int) -> None:
    close_handle = ctypes.windll.kernel32.CloseHandle
    close_handle.argtypes = [wintypes.HANDLE]
    close_handle.restype = wintypes.BOOL
    close_handle(handle)


def _library(path: Path, media: Path) -> None:
    with closing(sqlite3.connect(path)) as db:
        db.create_collation(
            "AMC_PL",
            lambda left, right: (left.casefold() > right.casefold())
            - (left.casefold() < right.casefold()),
        )
        db.executescript(
            """
            CREATE TABLE local_items(
                id TEXT PRIMARY KEY, title TEXT NOT NULL,
                has_custom_title INTEGER NOT NULL, path TEXT NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                is_in_library INTEGER NOT NULL DEFAULT 1,
                is_in_queue INTEGER NOT NULL DEFAULT 0,
                is_play_next INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE INDEX ix_local_items_title ON local_items(title COLLATE AMC_PL);
            CREATE TABLE bookmarks(
                id TEXT PRIMARY KEY, session_id TEXT, item_id TEXT,
                item_title TEXT);
            CREATE TABLE excluded_paths(ordinal INTEGER PRIMARY KEY, path TEXT NOT NULL);
            CREATE TABLE playback_history(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE custom_order(ordinal INTEGER, item_id TEXT);
            CREATE TABLE favorite_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE favorite_added_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE library_added_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE library_custom_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE queue_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE queue_play_next_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE queue_regular_order(session_id TEXT, ordinal INTEGER, item_id TEXT);
            CREATE TABLE playlists(
                id TEXT PRIMARY KEY, session_id TEXT NOT NULL,
                ordinal INTEGER NOT NULL, name TEXT NOT NULL COLLATE AMC_PL,
                created_utc_ticks INTEGER NOT NULL);
            CREATE UNIQUE INDEX ix_playlists_session_name
                ON playlists(session_id, name COLLATE AMC_PL);
            CREATE TABLE playlist_items(
                playlist_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
                item_id TEXT NOT NULL,
                PRIMARY KEY(playlist_id, ordinal),
                UNIQUE(playlist_id, item_id),
                FOREIGN KEY(playlist_id) REFERENCES playlists(id) ON DELETE CASCADE);
            """
        )
        db.execute(
            "INSERT INTO local_items(id,title,has_custom_title,path) VALUES(?,?,0,?)",
            ("local-1", "Stara nazwa", str(media)),
        )
        db.execute(
            "INSERT INTO local_items(id,title,has_custom_title,path) VALUES(?,?,0,?)",
            ("local-2", "Drugi plik", str(media)),
        )
        db.execute(
            "INSERT INTO bookmarks(id,session_id,item_id,item_title) VALUES('b','local','local-1','Stara nazwa')"
        )
        db.execute(
            "INSERT INTO bookmarks(id,session_id,item_id,item_title) "
            "VALUES('b-episode-remove','podcasts','episode-remove','Odcinek do usunięcia')"
        )
        db.execute(
            "INSERT INTO playback_history VALUES('podcasts',0,'episode-1')"
        )
        db.execute(
            "INSERT INTO playback_history VALUES('podcasts',1,'episode-kept')"
        )
        db.execute(
            "INSERT INTO playback_history VALUES('podcasts',2,'episode-remove')"
        )
        for table in (
            "queue_order", "queue_regular_order", "queue_play_next_order"
        ):
            db.execute(
                f"INSERT INTO {table} VALUES('podcasts',0,'episode-queue')"
            )
        db.execute(
            "INSERT INTO playlists VALUES('p-local','local',0,'Stara lista',638000000000000000)"
        )
        db.execute("INSERT INTO playlist_items VALUES('p-local',0,'local-1')")
        db.execute(
            "INSERT INTO playlists VALUES('p-radio','radio',0,'Stacje',638000000000000001)"
        )
        db.execute("INSERT INTO playlist_items VALUES('p-radio',0,'radio-1')")
        db.execute(
            "INSERT INTO playlists VALUES('p-podcast','podcasts',0,'Do słuchania',638000000000000002)"
        )
        db.execute("INSERT INTO playlist_items VALUES('p-podcast',0,'episode-queue')")
        db.execute(
            "INSERT INTO playlists VALUES('p-podcast-kept','podcasts',1,'Zostaje',638000000000000003)"
        )
        db.execute("INSERT INTO playlist_items VALUES('p-podcast-kept',0,'episode-queue')")
        db.execute("INSERT INTO playlist_items VALUES('p-podcast-kept',1,'episode-remove')")
        db.commit()


def _podcasts(path: Path) -> None:
    payload = {
        "Id": "podcast-1",
        "Title": "Stary podcast",
        "HasCustomTitle": False,
        "FeedUrl": "https://example.invalid/feed",
        "IsInLibrary": True,
    }
    with closing(sqlite3.connect(path)) as db:
        db.executescript(
            """
            CREATE TABLE podcast_subscriptions(
                id TEXT PRIMARY KEY, title TEXT, is_in_library INTEGER,
                payload_json TEXT);
            CREATE TABLE podcast_episodes(
                id TEXT PRIMARY KEY, subscription_id TEXT, title TEXT,
                is_in_queue INTEGER, is_play_next INTEGER,
                payload_json TEXT);
            CREATE TABLE podcast_state(
                singleton INTEGER PRIMARY KEY, current_item_id TEXT);
            CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """
        )
        db.execute(
            "INSERT INTO podcast_subscriptions VALUES(?,?,1,?)",
            ("podcast-1", "Stary podcast", json.dumps(payload)),
        )
        db.execute(
            "INSERT INTO podcast_episodes VALUES(?,?,?,1,1,?)",
            (
                "episode-queue",
                "podcast-1",
                "Odcinek w kolejce",
                json.dumps({"IsInQueue": True, "IsPlayNext": True}),
            ),
        )
        db.execute(
            "INSERT INTO podcast_episodes VALUES(?,?,?,0,0,?)",
            (
                "episode-remove",
                "podcast-1",
                "Odcinek do usunięcia",
                json.dumps({"Id": "episode-remove", "Title": "Odcinek do usunięcia"}),
            ),
        )
        db.execute("INSERT INTO podcast_state VALUES(1,'episode-remove')")
        db.commit()


def test_profile_edits_go_through_the_real_host() -> None:
    assert HOST.exists(), "Najpierw zbuduj LiteHost w konfiguracji Release"
    temporary_root = ROOT / ".tmp-tests"
    temporary_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="amc-profile-mutations-", dir=temporary_root
    ) as raw:
        root = Path(raw)
        media = root / "nagranie.mp3"
        media.write_bytes(b"test")
        history_media = root / "Zagadkowa niedziela-prawa dziecka.mp3"
        history_media.write_bytes(b"history")
        fresh_history_media = root / "Swieze nagranie bez indeksu.mp3"
        fresh_history_media.write_bytes(b"fresh history")
        busy_media = root / "zajety plik.mp3"
        busy_media.write_bytes(b"busy")
        releasing_media = root / "plik zwalniany przez odtwarzacz.mp3"
        releasing_media.write_bytes(b"decoder")
        library = root / "library.db"
        podcasts = root / "podcasts.db"
        state = root / "state.json"
        _library(library, media)
        with closing(sqlite3.connect(library)) as database:
            database.create_collation(
                "AMC_PL",
                lambda left, right: (left.casefold() > right.casefold())
                - (left.casefold() < right.casefold()),
            )
            database.execute(
                "UPDATE local_items SET path = ?, is_in_library = 0 "
                "WHERE id = 'local-2'",
                (str(history_media),),
            )
            database.execute(
                "INSERT INTO local_items(id,title,has_custom_title,path) "
                "VALUES('local-busy','Zajety plik',0,?)",
                (str(busy_media),),
            )
            database.execute(
                "INSERT INTO local_items(id,title,has_custom_title,path) "
                "VALUES('local-releasing','Plik zwalniany przez odtwarzacz',0,?)",
                (str(releasing_media),),
            )
            database.commit()
        _podcasts(podcasts)
        state.write_text(
            json.dumps(
                {
                    "radio": {
                        "stations": [
                            {
                                "id": "radio-1",
                                "name": "Stare radio",
                                "streamUrl": "https://example.invalid/old",
                                "isInLibrary": True,
                                "isFavorite": True,
                            }
                        ],
                        "recordingSchedules": [],
                        "recordingHistory": [
                            {
                                "id": "history-without-file",
                                "stationId": "radio-1",
                                "stationName": "Stare radio",
                                "path": "",
                                "outputPath": "",
                                "outcome": "Stopped",
                            },
                            {
                                "id": "history-1",
                                "stationId": "radio-1",
                                "stationName": "Stare radio",
                                "path": str(history_media),
                                "outcome": "Completed",
                            },
                            {
                                "id": "history-fresh",
                                "stationId": "radio-1",
                                "stationName": "Stare radio",
                                "path": str(fresh_history_media),
                                "outcome": "Completed",
                            }
                        ],
                    }
                }
            ),
            encoding="utf-8",
        )

        client = LiteHostClient(
            HOST, library_db=library, podcasts_db=podcasts, state_json=state
        )
        try:
            client.start()
            client.rename_library_item("local-1", "Nazwa biblioteczna")
            renamed = client.rename_local_file("local-1", "nowy plik")
            assert Path(renamed["path"]).name == "nowy plik.mp3"
            assert Path(renamed["path"]).exists()
            assert not media.exists(), "Stara ścieżka musi zniknąć po powodzeniu"

            # Zmiana wyłącznie wielkości liter przechodzi tą samą publiczną
            # drogą hosta i ma zmienić rzeczywistą pisownię wpisu na NTFS.
            case_renamed = client.rename_local_file(
                "local-1",
                "NOWY PLIK",
                path=renamed["path"],
            )
            case_path = Path(case_renamed["path"])
            assert case_path.exists()
            assert case_path.name == "NOWY PLIK.mp3"
            assert "NOWY PLIK.mp3" in [entry.name for entry in root.iterdir()]

            # Stop odtwarzacza odczepia tor od razu, ale dekoder zamyka uchwyt
            # w tle. Shift+F2 musi przeczekać ten krótki, rzeczywisty wyścig,
            # zamiast zwrócić błąd po pierwszym File.Move.
            releasing_handle = _open_exclusive_windows_file(releasing_media)
            releasing_lock = threading.Lock()
            handle_state: list[int] = [releasing_handle]

            def release_decoder_handle() -> None:
                with releasing_lock:
                    if handle_state:
                        _close_windows_handle(handle_state.pop())

            release_timer = threading.Timer(0.25, release_decoder_handle)
            release_timer.start()
            try:
                released_rename = client.rename_local_file(
                    "local-releasing",
                    "plik po zwolnieniu dekodera",
                    path=str(releasing_media),
                )
            finally:
                release_timer.cancel()
                release_decoder_handle()
                release_timer.join()
            released_path = Path(released_rename["path"])
            assert released_path.name == "plik po zwolnieniu dekodera.mp3"
            assert released_path.exists()
            assert not releasing_media.exists()

            # Prawdziwie zajęty plik nie może dać pozornego sukcesu ani
            # zmienić modelu. Uchwyt blokuje rename na poziomie Windows.
            busy_target = root / "zajety plik po zmianie.mp3"
            with _exclusive_windows_file(busy_media):
                try:
                    client.rename_local_file(
                        "local-busy",
                        "zajety plik po zmianie",
                        path=str(busy_media),
                    )
                except HostError as error:
                    assert "Nie można zmienić nazwy pliku" in str(error)
                else:
                    raise AssertionError("Host zgłosił sukces mimo zajętego pliku")
            assert busy_media.exists()
            assert not busy_target.exists()
            with closing(sqlite3.connect(library)) as database:
                stored_busy = database.execute(
                    "SELECT path FROM local_items WHERE id='local-busy'"
                ).fetchone()
            assert stored_busy == (str(busy_media),)
            renamed_history = client.rename_local_file(
                "radio-recording-history:history-1",
                "Zagadkowa niedziela - prawa dziecka",
                path=str(history_media),
            )
            assert Path(renamed_history["path"]).name == (
                "Zagadkowa niedziela - prawa dziecka.mp3"
            )
            assert Path(renamed_history["path"]).exists()
            stored_history = json.loads(state.read_text(encoding="utf-8"))[
                "radio"
            ]["recordingHistory"][1]
            assert stored_history["path"] == renamed_history["path"]
            renamed_fresh_history = client.rename_local_file(
                "radio-recording-history:history-fresh",
                "Swieze nagranie - poprawione",
                path=str(fresh_history_media),
            )
            assert Path(renamed_fresh_history["path"]).name == (
                "Swieze nagranie - poprawione.mp3"
            )
            assert renamed_fresh_history["itemId"] == (
                "radio-recording-history:history-fresh"
            )
            assert Path(renamed_fresh_history["path"]).exists()
            stored_fresh_history = json.loads(
                state.read_text(encoding="utf-8")
            )["radio"]["recordingHistory"][2]
            assert stored_fresh_history["path"] == renamed_fresh_history["path"]
            forgotten = client.forget_recording_history_paths(
                [renamed_fresh_history["path"]]
            )
            assert forgotten == {"removedCount": 1}
            remaining_history = json.loads(
                state.read_text(encoding="utf-8")
            )["radio"]["recordingHistory"]
            assert [item["id"] for item in remaining_history] == [
                "history-without-file",
                "history-1",
            ]
            client.rename_podcast_subscription("podcast-1", "Nowy podcast")
            removed_library = client.toggle_podcast_library(["podcast-1"])
            assert removed_library == {"added": False, "requested": 1, "changed": 1}
            restored_library = client.toggle_podcast_library(["podcast-1"])
            assert restored_library == {"added": True, "requested": 1, "changed": 1}
            removed_queue = client.toggle_podcast_queue(["episode-queue"])
            assert removed_queue["added"] is False
            assert removed_queue["changed"] == 1
            restored_queue = client.toggle_podcast_queue(["episode-queue"])
            assert restored_queue["added"] is True
            assert restored_queue["changed"] == 1
            made_next = client.toggle_podcast_queue(
                ["episode-queue"], play_next=True
            )
            assert made_next["added"] is True
            assert made_next["changed"] == 1
            radio_edit = client.edit_radio_station(
                "radio-1", "Nowe radio", "https://example.invalid/new"
            )
            assert radio_edit["streamChanged"] is True
            local_favorite = client.toggle_profile_favorites(
                "files",
                ["file:" + str(case_path), "file:" + renamed_history["path"]],
                paths=[str(case_path), renamed_history["path"]],
            )
            assert local_favorite == {
                "favorite": True,
                "requested": 2,
                "changed": 2,
                "titles": [
                    "Nazwa biblioteczna",
                    "Zagadkowa niedziela - prawa dziecka",
                ],
            }, local_favorite
            with closing(sqlite3.connect(library)) as database:
                flags = database.execute(
                    "SELECT id, is_favorite, is_in_library FROM local_items "
                    "WHERE id IN ('local-1', 'local-2') ORDER BY id"
                ).fetchall()
                assert flags == [
                    ("local-1", 1, 1),
                    ("local-2", 1, 1),
                ]
                assert database.execute(
                    "SELECT COUNT(*) FROM library_added_order "
                    "WHERE session_id = 'local' AND item_id = 'local-2'"
                ).fetchone() == (1,)
            unindexed_path = renamed_fresh_history["path"]
            try:
                client.toggle_profile_favorites(
                    "files",
                    ["file:" + unindexed_path],
                    paths=[unindexed_path],
                )
            except HostError as error:
                assert "Ctrl+O tylko otwiera folder" in str(error)
                assert "Ctrl+F5" in str(error)
            else:
                raise AssertionError(
                    "Plik spoza Biblioteki nie może otrzymać pozornego Ulubionego"
                )
            radio_favorite = client.toggle_profile_favorites("radio", ["radio-1"])
            assert radio_favorite["favorite"] is False
            assert radio_favorite["changed"] == 1
            radio_favorite = client.toggle_profile_favorites("radio", ["radio-1"])
            assert radio_favorite["favorite"] is True
            assert radio_favorite["changed"] == 1
            playlist_snapshot = client.profile_playlists("files")
            assert playlist_snapshot == {
                "sessionId": "local",
                "playlists": [
                    {
                        "playlistId": "p-local",
                        "name": "Stara lista",
                        "createdUtcTicks": 638000000000000000,
                        "itemIds": ["local-1"],
                    }
                ],
            }
            saved_playlists = client.replace_profile_playlists(
                "files",
                [
                    {
                        "playlistId": "p-local",
                        "name": "Nowa nazwa listy",
                        "createdUtcTicks": 638000000000000000,
                        "itemIds": ["local-2"],
                    },
                    {
                        "playlistId": "p-new",
                        "name": "Druga lista",
                        "createdUtcTicks": 638000000000000002,
                        "itemIds": ["local-1", "local-2"],
                    },
                ],
            )
            assert saved_playlists == {
                "sessionId": "local",
                "saved": 2,
                "items": 3,
            }
            moved_playlist = client.move_profile_playlist_items(
                "local",
                "p-new",
                ["local-2"],
                ["local-1", "local-2"],
                -1,
            )
            assert moved_playlist == {
                "sessionId": "local",
                "status": "moved",
                "moved": True,
                "count": 1,
            }
            placed_playlist = client.place_profile_playlist_items_before(
                "local", "p-new", ["local-1"], "local-2"
            )
            assert placed_playlist == {
                "sessionId": "local",
                "status": "moved",
                "moved": True,
                "count": 1,
            }
            playlist_boundary = client.move_profile_playlist_items(
                "local",
                "p-new",
                ["local-1"],
                ["local-1", "local-2"],
                -1,
            )
            assert playlist_boundary["status"] == "boundary"
            assert playlist_boundary["moved"] is False
            try:
                client.remove_profile_items(
                    "files", "playlistContents", ["local-1"],
                    playlist_id="p-radio",
                )
            except HostError:
                pass
            else:
                raise AssertionError("Obca playlista nie może być zmieniona")
            assert client.remove_profile_items(
                "files", "playlistContents", ["local-2"],
                playlist_id="p-new",
            ) == {"removedCount": 1}
            assert client.remove_profile_items(
                "radio", "playlistContents", ["radio-1"],
                playlist_id="p-radio",
            ) == {"removedCount": 1}
            with closing(sqlite3.connect(library)) as database:
                assert database.execute(
                    "SELECT is_in_library FROM local_items WHERE id='local-2'"
                ).fetchone() == (1,)
                assert database.execute(
                    "SELECT is_in_library FROM local_items WHERE id='local-1'"
                ).fetchone() == (1,)
                assert database.execute(
                    "SELECT item_id FROM playlist_items WHERE playlist_id='p-radio'"
                ).fetchall() == []
            removed_from_podcast_playlist = client.remove_profile_items(
                "podcasts",
                "podcastPlaylistContents",
                ["episode-queue"],
                playlist_id="p-podcast",
            )
            assert removed_from_podcast_playlist == {"removedCount": 1}
            removed_episode = client.remove_profile_items(
                "podcasts", "podcastEpisodes", ["episode-remove"]
            )
            assert removed_episode == {
                "removedCount": 1,
                "titles": ["Odcinek do usunięcia"],
            }
            client.remove_profile_items("files", "library", ["local-1"])
            client.remove_profile_items(
                "podcasts", "podcastLibrary", ["podcast-1"]
            )
            client.remove_profile_items(
                "podcasts", "podcastHistory", ["episode-1"]
            )
            client.remove_profile_items(
                "podcasts", "podcastQueue", ["episode-queue"]
            )
            client.remove_profile_items("radio", "library", ["radio-1"])
        finally:
            client.close()

        with closing(sqlite3.connect(library)) as db:
            row = db.execute(
                "SELECT title,path,is_favorite,is_in_library FROM local_items WHERE id='local-1'"
            ).fetchone()
            assert row == (
                "Nazwa biblioteczna",
                str(root / "NOWY PLIK.mp3"),
                1,
                0,
            )
            assert db.execute(
                "SELECT item_id FROM favorite_order WHERE session_id='local' ORDER BY ordinal"
            ).fetchall() == [("local-1",), ("local-2",)]
            assert db.execute(
                "SELECT item_id FROM favorite_added_order WHERE session_id='local' ORDER BY ordinal"
            ).fetchall() == [("local-1",), ("local-2",)]
            assert (root / "NOWY PLIK.mp3").exists(), (
                "Delete ma usunąć rekord z Biblioteki, ale zostawić plik na dysku"
            )
            excluded = db.execute(
                "SELECT path FROM excluded_paths"
            ).fetchone()
            assert excluded == (str(root / "NOWY PLIK.mp3"),)
            bookmark = db.execute(
                "SELECT item_title FROM bookmarks WHERE id='b'"
            ).fetchone()
            assert bookmark == ("Nazwa biblioteczna",)
            podcast_history = db.execute(
                "SELECT item_id FROM playback_history "
                "WHERE session_id='podcasts' ORDER BY ordinal"
            ).fetchall()
            assert podcast_history == [("episode-kept",)]
            assert db.execute(
                "SELECT id FROM bookmarks WHERE item_id='episode-remove'"
            ).fetchone() is None
            for table in (
                "queue_order", "queue_regular_order", "queue_play_next_order"
            ):
                assert db.execute(
                    f"SELECT item_id FROM {table} WHERE session_id='podcasts'"
                ).fetchall() == []
            assert db.execute(
                "SELECT id,name FROM playlists WHERE session_id='local' ORDER BY ordinal"
            ).fetchall() == [
                ("p-local", "Nowa nazwa listy"),
                ("p-new", "Druga lista"),
            ]

            assert db.execute(
                "SELECT playlist_id,item_id FROM playlist_items "
                "WHERE playlist_id IN ('p-local','p-new') ORDER BY playlist_id,ordinal"
            ).fetchall() == [
                ("p-local", "local-2"),
                ("p-new", "local-1"),
            ]
            assert db.execute(
                "SELECT name FROM playlists WHERE id='p-radio'"
            ).fetchone() == ("Stacje",), "Zapis sesji lokalnej nie może ruszyć Radia"
            assert db.execute(
                "SELECT item_id FROM playlist_items WHERE playlist_id='p-podcast'"
            ).fetchall() == []
            assert db.execute(
                "SELECT item_id FROM playlist_items WHERE playlist_id='p-podcast-kept'"
            ).fetchall() == [("episode-queue",)], (
                "Usuwanie ma dotyczyć tylko otwartej playlisty podcastów"
            )
        with closing(sqlite3.connect(podcasts)) as db:
            title, in_library, payload = db.execute(
                "SELECT title,is_in_library,payload_json FROM podcast_subscriptions WHERE id='podcast-1'"
            ).fetchone()
            assert title == "Nowy podcast"
            assert in_library == 0
            assert json.loads(payload)["HasCustomTitle"] is True
            assert json.loads(payload)["IsInLibrary"] is False
            queue_row = db.execute(
                "SELECT is_in_queue,is_play_next,payload_json "
                "FROM podcast_episodes WHERE id='episode-queue'"
            ).fetchone()
            assert queue_row[:2] == (0, 0)
            queue_payload = json.loads(queue_row[2])
            assert queue_payload["IsInQueue"] is False
            assert queue_payload["IsPlayNext"] is False
            assert db.execute(
                "SELECT id FROM podcast_episodes WHERE id='episode-remove'"
            ).fetchone() is None
            assert db.execute(
                "SELECT id FROM podcast_removed_episodes WHERE id='episode-remove'"
            ).fetchone() == ("episode-remove",)
            assert db.execute(
                "SELECT current_item_id FROM podcast_state WHERE singleton=1"
            ).fetchone() == (None,)
        radio = json.loads(state.read_text(encoding="utf-8"))["radio"]["stations"][0]
        assert radio["name"] == "Nowe radio"
        assert radio["streamUrl"] == "https://example.invalid/new"
        assert radio["isInLibrary"] is False
        assert radio["isFavorite"] is False


def test_radio_manual_order_mutations_persist_through_the_real_host() -> None:
    assert HOST.exists(), "Najpierw zbuduj LiteHost w konfiguracji Release"
    temporary_root = ROOT / ".tmp-tests"
    temporary_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="amc-radio-order-", dir=temporary_root
    ) as raw:
        root = Path(raw)
        media = root / "nagranie.mp3"
        media.write_bytes(b"test")
        library = root / "library.db"
        podcasts = root / "podcasts.db"
        state = root / "state.json"
        _library(library, media)
        _podcasts(podcasts)
        with closing(sqlite3.connect(library)) as database:
            for table in ("library_custom_order", "favorite_order"):
                database.executemany(
                    f"INSERT INTO {table}(session_id, ordinal, item_id) "
                    "VALUES('radio', ?, ?)",
                    enumerate(("radio-1", "radio-2", "radio-3")),
                )
            database.commit()
        state.write_text(
            json.dumps(
                {
                    "radio": {
                        "stations": [
                            {
                                "id": f"radio-{index}",
                                "name": name,
                                "streamUrl": f"https://example.invalid/{index}",
                                "isInLibrary": True,
                                "isFavorite": True,
                            }
                            for index, name in enumerate(
                                ("Jedynka", "Dwójka", "Trójka"), start=1
                            )
                        ],
                        "recordingSchedules": [],
                        "recordingHistory": [],
                    }
                }
            ),
            encoding="utf-8",
        )

        client = LiteHostClient(
            HOST, library_db=library, podcasts_db=podcasts, state_json=state
        )
        try:
            client.start()
            moved = client.move_radio_collection_items(
                "library",
                ["radio-2"],
                ["radio-1", "radio-2", "radio-3"],
                -1,
            )
            assert moved == {
                "view": "library",
                "status": "moved",
                "moved": True,
                "count": 1,
            }
            placed = client.place_radio_collection_items_before(
                "favorites",
                ["radio-3"],
                ["radio-1", "radio-2", "radio-3"],
                "radio-1",
            )
            assert placed == {
                "view": "favorites",
                "status": "moved",
                "moved": True,
                "count": 1,
            }
            try:
                client.move_radio_collection_items(
                    "history",
                    ["radio-1"],
                    ["radio-1", "radio-2", "radio-3"],
                    1,
                )
            except HostError as error:
                assert "Bibliotece i Ulubionych Radia" in str(error)
            else:
                raise AssertionError("Host dopuścił ręczną kolejność Historii")
        finally:
            client.close()

        with closing(sqlite3.connect(library)) as database:
            library_order = database.execute(
                "SELECT item_id FROM library_custom_order "
                "WHERE session_id='radio' ORDER BY ordinal"
            ).fetchall()
            favorite_order = database.execute(
                "SELECT item_id FROM favorite_order "
                "WHERE session_id='radio' ORDER BY ordinal"
            ).fetchall()
        assert library_order == [
            ("radio-2",), ("radio-1",), ("radio-3",)
        ]
        assert favorite_order == [
            ("radio-3",), ("radio-1",), ("radio-2",)
        ]
