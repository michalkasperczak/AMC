"""Zapisz jako tworzy kopię, ale nie zmienia pola pobrania w bibliotece."""

from __future__ import annotations

import json
import sqlite3
import tempfile
import threading
import unittest
from contextlib import closing
from http.server import ThreadingHTTPServer
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient
from test_podcast_download_host_e2e import _EpisodeHandler, _create_database


def test_real_host_saves_one_episode_as_without_marking_it_downloaded() -> None:
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
    with tempfile.TemporaryDirectory(prefix="podcast-save-as-", dir=temporary_root) as directory:
        temp = Path(directory)
        server = ThreadingHTTPServer(("127.0.0.1", 0), _EpisodeHandler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        database = temp / "podcasts.db"
        media_url = f"http://127.0.0.1:{server.server_port}/odcinek.mp3"
        _create_database(database, media_url=media_url, downloads=temp / "Pobrane")
        destination = temp / "Moja kopia odcinka.mp3"
        client = LiteHostClient(host, podcasts_db=database)
        try:
            client.start()
            info = client.podcast_save_as_info("episode-private-id")
            assert info["title"] == "Odcinek testowy"
            assert info["suggestedFileName"] == "Odcinek testowy.mp3"

            saved = client.save_podcast_episode_as(
                "episode-private-id", str(destination)
            )
            assert saved["fileName"] == destination.name
            assert saved["bytesWritten"] > 0
            assert destination.is_file()

            with closing(sqlite3.connect(database)) as connection:
                row = connection.execute(
                    "SELECT download_path, payload_json FROM podcast_episodes WHERE id = ?",
                    ("episode-private-id",),
                ).fetchone()
            assert row is not None
            assert row[0] is None
            assert json.loads(row[1]).get("DownloadPath") is None
        finally:
            client.close()
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
