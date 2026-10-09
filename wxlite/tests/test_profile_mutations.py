"""End-to-end witness for F2/Delete profile mutations through the C# host."""

from __future__ import annotations

import json
import sqlite3
import tempfile
from contextlib import closing
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient


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
            CREATE TABLE playlist_items(playlist_id TEXT, ordinal INTEGER, item_id TEXT);
            """
        )
        db.execute(
            "INSERT INTO local_items(id,title,has_custom_title,path) VALUES(?,?,0,?)",
            ("local-1", "Stara nazwa", str(media)),
        )
        db.execute(
            "INSERT INTO bookmarks(id,session_id,item_id,item_title) VALUES('b','local','local-1','Stara nazwa')"
        )
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
                id TEXT PRIMARY KEY, subscription_id TEXT, payload_json TEXT);
            """
        )
        db.execute(
            "INSERT INTO podcast_subscriptions VALUES(?,?,1,?)",
            ("podcast-1", "Stary podcast", json.dumps(payload)),
        )
        db.commit()


def test_profile_edits_go_through_the_real_host() -> None:
    assert HOST.exists(), "Najpierw zbuduj LiteHost w konfiguracji Release"
    with tempfile.TemporaryDirectory(prefix="amc-profile-mutations-") as raw:
        root = Path(raw)
        media = root / "nagranie.mp3"
        media.write_bytes(b"test")
        library = root / "library.db"
        podcasts = root / "podcasts.db"
        state = root / "state.json"
        _library(library, media)
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
            client.rename_podcast_subscription("podcast-1", "Nowy podcast")
            radio_edit = client.edit_radio_station(
                "radio-1", "Nowe radio", "https://example.invalid/new"
            )
            assert radio_edit["streamChanged"] is True
            client.remove_profile_items("files", "library", ["local-1"])
            client.remove_profile_items(
                "podcasts", "podcastLibrary", ["podcast-1"]
            )
            client.remove_profile_items("radio", "library", ["radio-1"])
        finally:
            client.close()

        with closing(sqlite3.connect(library)) as db:
            row = db.execute(
                "SELECT title,path,is_in_library FROM local_items WHERE id='local-1'"
            ).fetchone()
            assert row == (
                "Nazwa biblioteczna",
                str(root / "nowy plik.mp3"),
                0,
            )
            assert (root / "nowy plik.mp3").exists(), (
                "Delete ma usunąć rekord z Biblioteki, ale zostawić plik na dysku"
            )
            excluded = db.execute(
                "SELECT path FROM excluded_paths"
            ).fetchone()
            assert excluded == (str(root / "nowy plik.mp3"),)
            bookmark = db.execute(
                "SELECT item_title FROM bookmarks WHERE id='b'"
            ).fetchone()
            assert bookmark == ("Nazwa biblioteczna",)
        with closing(sqlite3.connect(podcasts)) as db:
            title, in_library, payload = db.execute(
                "SELECT title,is_in_library,payload_json FROM podcast_subscriptions WHERE id='podcast-1'"
            ).fetchone()
            assert title == "Nowy podcast"
            assert in_library == 0
            assert json.loads(payload)["HasCustomTitle"] is True
            assert json.loads(payload)["IsInLibrary"] is False
        radio = json.loads(state.read_text(encoding="utf-8"))["radio"]["stations"][0]
        assert radio["name"] == "Nowe radio"
        assert radio["streamUrl"] == "https://example.invalid/new"
        assert radio["isInLibrary"] is False
        assert radio["isFavorite"] is False
