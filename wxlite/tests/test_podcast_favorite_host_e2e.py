"""Prawdziwy LiteHost zapisuje ulubione w obu reprezentacjach bazy."""

from __future__ import annotations

import json
import sqlite3
import tempfile
import unittest
from contextlib import closing
from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient
from test_podcast_download_host_e2e import _create_database


def test_real_host_toggles_mixed_podcast_and_episode_selection() -> None:
    temporary_root = Path(__file__).resolve().parents[2] / ".tmp-tests"
    temporary_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="podcast-favorite-", dir=temporary_root
    ) as directory:
        _run_real_host_toggle(Path(directory))


def _run_real_host_toggle(tmp_path: Path) -> None:
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

    database = tmp_path / "podcasts.db"
    _create_database(
        database,
        media_url="https://example.invalid/odcinek.mp3",
        downloads=tmp_path / "Pobrane",
    )
    client = LiteHostClient(host, podcasts_db=database)
    try:
        client.start()
        added = client.toggle_podcast_favorites(
            ["subscription-private-id"],
            ["episode-private-id"],
        )
        assert added == {"favorite": True, "requested": 2, "changed": 2}

        with closing(sqlite3.connect(database)) as connection:
            subscription_payload = connection.execute(
                "SELECT payload_json FROM podcast_subscriptions WHERE id = ?",
                ("subscription-private-id",),
            ).fetchone()[0]
            episode_row = connection.execute(
                "SELECT is_favorite, payload_json FROM podcast_episodes WHERE id = ?",
                ("episode-private-id",),
            ).fetchone()
        assert json.loads(subscription_payload)["IsFavorite"] is True
        assert episode_row[0] == 1
        assert json.loads(episode_row[1])["IsFavorite"] is True

        removed = client.toggle_podcast_favorites(
            ["subscription-private-id"],
            ["episode-private-id"],
        )
        assert removed == {"favorite": False, "requested": 2, "changed": 2}
    finally:
        client.close()
