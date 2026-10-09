"""Rzeczywisty host sprawdza, importuje i eksportuje OPML wspólnym Core."""

from __future__ import annotations

import tempfile
import threading
import unittest
from http.server import ThreadingHTTPServer
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient
from test_podcast_add_host_e2e import _FeedHandler
from test_podcast_download_host_e2e import _create_database


def test_real_host_inspects_imports_and_exports_podcast_opml() -> None:
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
    with tempfile.TemporaryDirectory(prefix="podcast-opml-", dir=temporary_root) as directory:
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
        source = temp / "do-importu.opml"
        source.write_text(
            "<?xml version='1.0' encoding='utf-8'?>"
            "<opml version='2.0'><body>"
            f"<outline text='Podcast wybrany' type='rss' xmlUrl='{address}'/>"
            "<outline text='Nie wybrany' type='rss' xmlUrl='https://example.invalid/skip.xml'/>"
            "</body></opml>",
            encoding="utf-8",
        )
        destination = temp / "wyeksportowane.opml"
        client = LiteHostClient(host, podcasts_db=database)
        try:
            client.start()
            inspected = client.inspect_podcast_opml(str(source))
            assert inspected["count"] == 2
            assert inspected["entries"][0]["label"].startswith("Podcast wybrany")
            assert "PodcastOpmlEntry" not in inspected["entries"][0]["label"]

            imported = client.import_podcast_opml(str(source), [address])
            assert imported == {"selected": 1, "imported": 1, "failed": 0}

            exported = client.export_podcast_opml(str(destination))
            assert exported["count"] == 2
            text = destination.read_text(encoding="utf-8")
            assert "Podcast wybrany" in text
            assert address in text
            assert "Nie wybrany" not in text
        finally:
            client.close()
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)
