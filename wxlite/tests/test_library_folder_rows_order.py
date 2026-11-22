"""Folder views avoid the expensive SQL title collation without changing order."""

from __future__ import annotations

import sqlite3

from amc_wx_lite.library_db import LibraryDatabase, folder_rows


def _database(path):
    connection = sqlite3.connect(path)
    connection.create_collation(
        "AMC_PL", lambda left, right: (left > right) - (left < right),
    )
    connection.executescript("""
        CREATE TABLE local_items (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL COLLATE AMC_PL,
            path TEXT NOT NULL,
            duration_ticks INTEGER NOT NULL,
            is_favorite INTEGER NOT NULL,
            is_in_library INTEGER NOT NULL,
            is_available INTEGER NOT NULL,
            is_in_queue INTEGER NOT NULL DEFAULT 0,
            is_radio_recording INTEGER NOT NULL
        );
        CREATE INDEX ix_local_items_membership
            ON local_items(is_in_library, is_available, is_favorite, is_in_queue);
        CREATE TABLE folder_sources (
            id TEXT PRIMARY KEY,
            ordinal INTEGER NOT NULL,
            path TEXT NOT NULL,
            display_name TEXT NOT NULL,
            resume_mode INTEGER NOT NULL
        );
    """)
    connection.execute(
        "INSERT INTO folder_sources VALUES ('one', 0, ?, 'muzyka', 0)",
        (r"X:\muzyka",),
    )
    rows = (
        ("one", "abc", r"X:\muzyka\abc1.mp3", 1),
        ("two", "ABC", r"X:\muzyka\abc2.mp3", 0),
        ("three", "z", r"X:\muzyka\CD\z.mp3", 1),
        ("four", "a", r"X:\muzyka\cd\a.mp3", 0),
        ("orphan", "samotny", r"Y:\samotny.mp3", 0),
    )
    connection.executemany(
        "INSERT INTO local_items (id,title,path,duration_ticks,is_favorite,"
        "is_in_library,is_available,is_radio_recording) VALUES (?,?,?,0,?,1,1,0)",
        rows,
    )
    connection.commit()
    connection.close()
    return LibraryDatabase(path)


def test_folder_rows_use_rowid_for_ties_and_first_folder_spelling(tmp_path) -> None:
    db = _database(tmp_path / "library.db")
    statements = []
    db.connection.set_trace_callback(statements.append)
    try:
        rows = folder_rows(db, r"X:\muzyka")
        assert [(row.title, row.item_id) for row in rows] == [
            ("CD", r"dir:X:\muzyka\CD"),
            ("abc", "one"),
            ("ABC", "two"),
        ]
        assert any("ORDER BY rowid" in sql for sql in statements)
        assert not any("ORDER BY title COLLATE AMC_PL" in sql for sql in statements)
    finally:
        db.close()


def test_root_avoids_title_sort_and_other_callers_keep_it(tmp_path) -> None:
    db = _database(tmp_path / "library.db")
    statements = []
    db.connection.set_trace_callback(statements.append)
    try:
        assert [row.title for row in folder_rows(db, None)] == ["muzyka", "samotny"]
        assert any("ORDER BY rowid" in sql for sql in statements)
        assert not any("ORDER BY title COLLATE AMC_PL" in sql for sql in statements)

        statements.clear()
        items = db.active_items_under(r"X:\muzyka")
        assert [item.title for item in items] == ["a", "ABC", "abc", "z"]
        assert any("ORDER BY title COLLATE AMC_PL" in sql for sql in statements)
    finally:
        db.close()
