"""Regression for local search with a large, isolated SQLite library."""

from __future__ import annotations

import base64
import json
import sqlite3
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.collation import HostCollation, MAX_REQUEST_BYTES
from amc_wx_lite.library_db import LibraryDatabase
from amc_wx_lite.library_views import all_files_rows
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.search_catalog import search
from amc_wx_lite.search_sources import SearchSources


def _library(path, count: int) -> None:
    with sqlite3.connect(path) as db:
        db.execute(
            "CREATE TABLE local_items ("
            "id TEXT PRIMARY KEY, title TEXT NOT NULL, path TEXT NOT NULL, "
            "duration_ticks INTEGER NOT NULL DEFAULT 0, "
            "is_favorite INTEGER NOT NULL DEFAULT 0, "
            "is_available INTEGER NOT NULL DEFAULT 1, "
            "is_in_library INTEGER NOT NULL DEFAULT 1, "
            "is_radio_recording INTEGER NOT NULL DEFAULT 0)"
        )
        db.executemany(
            "INSERT INTO local_items(id, title, path) VALUES (?, ?, ?)",
            (
                (
                    str(index),
                    f"Utwor {index:05d}" if index < count - 1 else "Jedyny cel",
                    f"C:/Muzyka/{index:05d}.mp3",
                )
                for index in range(count)
            ),
        )


class _Library:
    def __init__(self, path, collation):
        self.path = path
        self.collation = collation
        self.last_view = None

    def load_view(self, view: str):
        assert view == "all_files"
        with LibraryDatabase(self.path) as db:
            self.last_view = all_files_rows(db, self.collation)
        return self.last_view


@pytest.mark.parametrize("count", [50_000, 50_001])
def test_search_collect_keeps_real_hit_at_boundary(tmp_path, count):
    path = tmp_path / "library.db"
    _library(path, count)
    sent = []

    def host_call(op, args=None, timeout=None):
        assert op == "library.collationKeys"
        batch = list(args["titles"])
        line = json.dumps(
            {"id": "1", "op": op, "args": args}, ensure_ascii=False
        )
        assert len(line.encode("utf-8")) <= MAX_REQUEST_BYTES
        assert 0 < len(batch) <= 50_000
        sent.extend((args["mode"], value) for value in batch)
        return {
            "collation": args["mode"],
            "keys": [base64.b64encode(value.encode()).decode() for value in batch],
        }

    library = _Library(path, HostCollation(host_call))
    collection = SearchSources(library, None, None, None).collect((SessionId.FILES,))

    assert collection.warnings == ()
    assert len(collection.entries) == count
    assert len({entry.row.item_id for entry in collection.entries}) == count
    assert [item.row.title for item in search(list(collection.entries), "jedyny cel")] == [
        "Jedyny cel"
    ]
    assert library.last_view.order_matches_amc is True
    assert len(sent) == 2 * count
    assert len(set(sent)) == 2 * count


def test_search_collect_keeps_hit_when_host_fails(tmp_path):
    path = tmp_path / "library.db"
    _library(path, 2)

    def failing_call(*_args, **_kwargs):
        raise TimeoutError("host nie odpowiedział")

    library = _Library(path, HostCollation(failing_call))
    collection = SearchSources(library, None, None, None).collect((SessionId.FILES,))

    assert collection.warnings == ()
    assert len(collection.entries) == 2
    assert [item.row.title for item in search(list(collection.entries), "jedyny cel")] == [
        "Jedyny cel"
    ]
    assert library.last_view.order_matches_amc is False
