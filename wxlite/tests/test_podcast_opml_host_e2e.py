"""Rzeczywisty host sprawdza, importuje i eksportuje OPML wspólnym Core."""

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
from test_podcast_add_host_e2e import _FeedHandler
from test_podcast_download_host_e2e import _create_database


YOUTUBE_SOURCES = [
    {
        "Id": "youtube-channel:UCabc_DEF-123",
        "Title": "Kanał testowy",
        "FeedUrl": "https://www.youtube.com/feeds/videos.xml?channel_id=UCabc_DEF-123",
        "SourceKind": 2,
        "HomepageUrl": "https://www.youtube.com/channel/UCabc_DEF-123",
        "IsInLibrary": True,
    },
    {
        "Id": "youtube-playlist:PLxyz_789",
        "Title": "Playlista testowa",
        "FeedUrl": "https://www.youtube.com/feeds/videos.xml?playlist_id=PLxyz_789",
        "SourceKind": 3,
        "HomepageUrl": "https://www.youtube.com/playlist?list=PLxyz_789",
        "IsInLibrary": True,
    },
]


def _add_youtube_sources(database: Path) -> None:
    with closing(sqlite3.connect(database)) as connection:
        next_ordinal = connection.execute(
            "SELECT COUNT(*) FROM podcast_subscriptions"
        ).fetchone()[0]
        for offset, subscription in enumerate(YOUTUBE_SOURCES):
            connection.execute(
                """INSERT INTO podcast_subscriptions(
                       id, ordinal, title, feed_url, is_in_library,
                       last_refresh_utc_ticks, content_hash, payload_json)
                   VALUES(?, ?, ?, ?, 1, 0, ?, ?)""",
                (
                    subscription["Id"],
                    next_ordinal + offset,
                    subscription["Title"],
                    subscription["FeedUrl"],
                    f"youtube-{offset}",
                    json.dumps(subscription, ensure_ascii=False),
                ),
            )
        connection.commit()


def _assert_youtube_exports(client: LiteHostClient, temp: Path) -> None:
    youtube_csv = temp / "youtube.csv"
    csv_result = client.export_youtube_subscriptions(str(youtube_csv))
    assert csv_result["format"] == "csv"
    assert csv_result["exported"] == 1
    assert csv_result["skippedPlaylists"] == 1
    csv_text = youtube_csv.read_text(encoding="utf-8")
    assert csv_text.startswith("Channel Id,Channel Url,Channel Title")
    assert "UCabc_DEF-123" in csv_text
    assert "PLxyz_789" not in csv_text

    youtube_opml = temp / "youtube.opml"
    opml_result = client.export_youtube_subscriptions(str(youtube_opml))
    assert opml_result["format"] == "opml"
    assert opml_result["exported"] == 2
    assert opml_result["skippedPlaylists"] == 0
    youtube_text = youtube_opml.read_text(encoding="utf-8")
    assert "channel_id=UCabc_DEF-123" in youtube_text
    assert "playlist_id=PLxyz_789" in youtube_text


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

            _add_youtube_sources(database)

            exported = client.export_podcast_opml(str(destination))
            assert exported["count"] == 2
            text = destination.read_text(encoding="utf-8")
            assert "Podcast wybrany" in text
            assert address in text
            assert "Nie wybrany" not in text

            _assert_youtube_exports(client, temp)
        finally:
            client.close()
            server.shutdown()
            server.server_close()
            thread.join(timeout=5)


def test_real_host_exports_youtube_subscriptions_in_portable_formats() -> None:
    """Eksport YouTube nie zależy od sieci ani wcześniejszego importu RSS."""
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
    with tempfile.TemporaryDirectory(prefix="youtube-export-", dir=temporary_root) as directory:
        temp = Path(directory)
        database = temp / "podcasts.db"
        _create_database(
            database,
            media_url="https://example.invalid/already-there.mp3",
            downloads=temp / "Pobrane",
        )
        _add_youtube_sources(database)
        client = LiteHostClient(host, podcasts_db=database)
        try:
            client.start()
            _assert_youtube_exports(client, temp)
        finally:
            client.close()
