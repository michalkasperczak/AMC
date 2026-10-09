"""Prawdziwy host pobiera plik RSS i zapisuje wąską zmianę podcasts.db."""

from __future__ import annotations

import json
import sqlite3
import threading
import tempfile
import unittest
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient


_AUDIO = b"ID3" + bytes(range(64)) * 32


class _EpisodeHandler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802 - nazwa wymagana przez BaseHTTPRequestHandler
        if self.path != "/odcinek.mp3":
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", "audio/mpeg")
        self.send_header("Content-Length", str(len(_AUDIO)))
        self.end_headers()
        self.wfile.write(_AUDIO)

    def log_message(self, _format, *args):
        pass


def _create_database(path: Path, *, media_url: str, downloads: Path) -> None:
    subscription = {
        "Id": "subscription-private-id",
        "Title": "Podcast testowy",
        "FeedUrl": "https://example.invalid/feed.xml",
        "SourceKind": 0,
        "IsInLibrary": True,
    }
    episode = {
        "Id": "episode-private-id",
        "SubscriptionId": "subscription-private-id",
        "Title": "Odcinek testowy",
        "MediaUrl": media_url,
        "MediaType": "audio/mpeg",
        "IsNew": True,
    }
    with closing(sqlite3.connect(path)) as connection:
        connection.executescript(
            """
            CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE podcast_state (
                singleton INTEGER PRIMARY KEY CHECK(singleton = 1),
                downloads_folder TEXT NULL,
                current_item_id TEXT NULL,
                volume INTEGER NOT NULL,
                playback_rate REAL NOT NULL,
                rss_refresh_interval_minutes INTEGER NOT NULL DEFAULT 60,
                youtube_refresh_interval_minutes INTEGER NOT NULL DEFAULT 60,
                automatic_refresh_batch_size INTEGER NOT NULL DEFAULT 4
            );
            CREATE TABLE podcast_subscriptions (
                id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL, title TEXT NOT NULL,
                feed_url TEXT NOT NULL, is_in_library INTEGER NOT NULL,
                last_refresh_utc_ticks INTEGER NOT NULL, content_hash TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE TABLE podcast_episodes (
                id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL,
                subscription_id TEXT NOT NULL, title TEXT NOT NULL,
                published_utc_ticks INTEGER NOT NULL, is_new INTEGER NOT NULL,
                is_started INTEGER NOT NULL, is_played INTEGER NOT NULL,
                is_favorite INTEGER NOT NULL, is_in_queue INTEGER NOT NULL,
                is_play_next INTEGER NOT NULL, download_path TEXT NULL,
                content_hash TEXT NOT NULL, payload_json TEXT NOT NULL
            );
            """
        )
        connection.execute(
            """INSERT INTO podcast_state(
                   singleton, downloads_folder, volume, playback_rate)
               VALUES(1, ?, 35, 1.0)""",
            (str(downloads),),
        )
        connection.execute(
            """INSERT INTO podcast_subscriptions(
                   id, ordinal, title, feed_url, is_in_library,
                   last_refresh_utc_ticks, content_hash, payload_json)
               VALUES(?, 0, ?, ?, 1, 0, 'seed', ?)""",
            (
                subscription["Id"],
                subscription["Title"],
                subscription["FeedUrl"],
                json.dumps(subscription, ensure_ascii=False),
            ),
        )
        connection.execute(
            """INSERT INTO podcast_episodes(
                   id, ordinal, subscription_id, title, published_utc_ticks,
                   is_new, is_started, is_played, is_favorite, is_in_queue,
                   is_play_next, download_path, content_hash, payload_json)
               VALUES(?, 0, ?, ?, 0, 1, 0, 0, 0, 0, 0, NULL, 'seed', ?)""",
            (
                episode["Id"],
                episode["SubscriptionId"],
                episode["Title"],
                json.dumps(episode, ensure_ascii=False),
            ),
        )
        connection.commit()


def test_real_host_downloads_rss_episode_and_saves_only_download_path() -> None:
    temporary_root = Path(__file__).resolve().parents[2] / ".tmp-tests"
    temporary_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="podcast-download-", dir=temporary_root
    ) as directory:
        _run_real_host_download(Path(directory))


def _run_real_host_download(tmp_path: Path) -> None:
    host = (
        Path(__file__).resolve().parents[2]
        / "src"
        / "AccessibleMediaController.LiteHost"
        / "bin"
        / "Release"
        / "net8.0-windows10.0.19041.0"
        / "amc_lite_host.exe"
    )
    if not host.is_file():
        raise unittest.SkipTest("najpierw zbuduj host AMC-wx-Lite w konfiguracji Release")

    server = ThreadingHTTPServer(("127.0.0.1", 0), _EpisodeHandler)
    server_thread = threading.Thread(target=server.serve_forever, daemon=True)
    server_thread.start()
    downloads = tmp_path / "Pobrane"
    database = tmp_path / "podcasts.db"
    address = f"http://127.0.0.1:{server.server_port}/odcinek.mp3"
    _create_database(database, media_url=address, downloads=downloads)
    events: list[tuple[str, dict]] = []
    client = LiteHostClient(
        host,
        podcasts_db=database,
        on_event=lambda name, data: events.append((name, data)),
    )
    try:
        client.start()
        result = client.download_podcast_episodes(["episode-private-id"])
        assert result == {
            "requested": 1,
            "downloaded": 1,
            "alreadyDownloaded": 0,
            "failed": 0,
            "singleTitle": "Odcinek testowy",
            "firstFailure": None,
        }, result
        with closing(sqlite3.connect(database)) as connection:
            row = connection.execute(
                "SELECT download_path, payload_json FROM podcast_episodes WHERE id = ?",
                ("episode-private-id",),
            ).fetchone()
        assert row is not None
        saved_path = Path(row[0])
        assert saved_path.is_file()
        assert saved_path.read_bytes() == _AUDIO
        assert json.loads(row[1])["DownloadPath"] == str(saved_path)
        assert any(
            name == "podcast.downloadProgress" and data.get("percent") == 100
            for name, data in events
        )
    finally:
        client.close()
        server.shutdown()
        server.server_close()
        server_thread.join(timeout=5)
