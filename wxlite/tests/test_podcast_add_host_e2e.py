"""Rzeczywisty host sprawdza RSS i zapisuje nowe źródło w podcasts.db."""

from __future__ import annotations

import json
import sqlite3
import tempfile
import threading
import unittest
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient
from test_podcast_download_host_e2e import _create_database


class _FeedHandler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802 - nazwa wymagana przez BaseHTTPRequestHandler
        if self.path != "/feed.xml":
            self.send_error(404)
            return
        media = f"http://127.0.0.1:{self.server.server_port}/episode.mp3"
        body = (
            "<?xml version='1.0' encoding='utf-8'?>"
            "<rss version='2.0'><channel>"
            "<title>Podcast z serwera</title><description>Opis testowy</description>"
            "<item><guid>e2e-new-episode</guid>"
            "<title>Nowy odcinek z serwera</title>"
            f"<enclosure url='{media}' type='audio/mpeg' length='1234'/>"
            "</item></channel></rss>"
        ).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/rss+xml; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, _format, *args):
        pass


def test_real_host_adds_rss_source_and_preserves_user_facing_title() -> None:
    root = Path(__file__).resolve().parents[2]
    host = (
        root
        / "src"
        / "AccessibleMediaController.LiteHost"
        / "bin"
        / "Release"
        / "net8.0-windows10.0.19041.0"
        / "amc_lite_host.exe"
    )
    if not host.is_file():
        raise unittest.SkipTest("najpierw zbuduj host AMC-wx-Lite w konfiguracji Release")

    temporary_root = root / ".tmp-tests"
    temporary_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="podcast-add-", dir=temporary_root
    ) as directory:
        temp = Path(directory)
        database = temp / "podcasts.db"
        _create_database(
            database,
            media_url="https://example.invalid/already-there.mp3",
            downloads=temp / "Pobrane",
        )
        server = ThreadingHTTPServer(("127.0.0.1", 0), _FeedHandler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        address = f"http://127.0.0.1:{server.server_port}/feed.xml"
        client = LiteHostClient(host, podcasts_db=database)
        try:
            client.start()
            result = client.add_podcast_source(address, "Moja nazwa RSS")
            assert result["title"] == "Moja nazwa RSS"
            assert result["sourceKind"] == "rss"
            assert result["sourceLabel"] == "podcast"
            assert result["added"] is True
            assert result["itemCount"] == 1

            with closing(sqlite3.connect(database)) as connection:
                row = connection.execute(
                    "SELECT id, title, is_in_library, payload_json "
                    "FROM podcast_subscriptions WHERE feed_url = ?",
                    (address,),
                ).fetchone()
                assert row is not None
                episode = connection.execute(
                    "SELECT title, payload_json FROM podcast_episodes "
                    "WHERE subscription_id = ?",
                    (row[0],),
                ).fetchone()
            assert row[1:3] == ("Moja nazwa RSS", 1)
            payload = json.loads(row[3])
            assert payload["HasCustomTitle"] is True
            assert payload["RefreshIntervalMinutes"] == 60
            assert episode is not None
            assert episode[0] == "Nowy odcinek z serwera"
        finally:
            client.close()
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
