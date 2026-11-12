"""Wspólne kategorie muzyczne w lokalnej Bibliotece AMC."""

from __future__ import annotations

import sqlite3
from pathlib import Path

from amc_wx_lite.library_db import LibraryDatabase
from amc_wx_lite.library_views import (
    local_album_rows,
    local_album_track_rows,
    local_artist_album_rows,
    local_artist_rows,
)
from amc_wx_lite.list_model import Row
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import LibraryView, Navigator, OpenLibraryView, SessionId
from amc_wx_lite.shortcuts import Action, Chord, resolve


def _database(path: Path) -> LibraryDatabase:
    connection = sqlite3.connect(path)
    connection.executescript("""
        CREATE TABLE folder_sources (
            id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL, path TEXT NOT NULL,
            display_name TEXT NOT NULL, resume_mode INTEGER NOT NULL
        );
        CREATE TABLE local_items (
            id TEXT PRIMARY KEY, title TEXT NOT NULL, path TEXT NOT NULL,
            duration_ticks INTEGER NOT NULL, is_favorite INTEGER NOT NULL,
            is_available INTEGER NOT NULL, is_in_library INTEGER NOT NULL,
            is_radio_recording INTEGER NOT NULL
        );
    """)
    connection.execute(
        "INSERT INTO folder_sources VALUES ('music', 0, ?, 'Muzyka', 0)",
        (r"C:\Music",),
    )
    rows = (
        ("a1", "Pierwszy", r"C:\Music\Artysta A\Album Jeden\01 - Pierwszy.mp3"),
        ("a2", "Drugi", r"C:\Music\Artysta A\Album Jeden\02 - Drugi.mp3"),
        ("b1", "Trzeci", r"C:\Music\Artysta A\Album Dwa\01. Trzeci.mp3"),
        ("b2", "Czwarty", r"C:\Music\Artysta A\Album Dwa\02. Czwarty.mp3"),
        ("single", "Samotny", r"C:\Music\Luźne\Samotny.mp3"),
    )
    connection.executemany(
        "INSERT INTO local_items VALUES (?, ?, ?, 10000000, 0, 1, 1, 0)",
        rows,
    )
    connection.commit()
    connection.close()
    return LibraryDatabase(path)


def test_local_album_artist_and_track_views_are_intentional(tmp_path: Path) -> None:
    db = _database(tmp_path / "library.db")
    try:
        albums = local_album_rows(db)
        assert [row.title for row in albums.rows] == ["Album Dwa", "Album Jeden"]
        assert all(row.service_kind == "localAlbum" for row in albums.rows)
        assert all("C:\\Music" not in row.title + row.detail for row in albums.rows)
        assert all(row.artist_name == "Artysta A" for row in albums.rows)

        artists = local_artist_rows(db)
        assert [row.title for row in artists.rows] == ["Artysta A"]
        assert artists.rows[0].detail == "wykonawca, 2 albumy"

        artist_albums = local_artist_album_rows(db, "Artysta A")
        assert [row.title for row in artist_albums.rows] == ["Album Dwa", "Album Jeden"]

        tracks = local_album_track_rows(db, albums.rows[1].service_id or "")
        assert [row.title for row in tracks.rows] == ["Pierwszy", "Drugi"]
        assert tracks.heading == "Album — Album Jeden"
    finally:
        db.close()


def test_local_artist_album_navigation_restores_each_parent() -> None:
    nav = Navigator()
    nav.active = SessionId.FILES
    artist = Row(
        "artist-a", "Artysta A", "artist",
        service_id="Artysta A", service_kind="localArtist",
    )
    nav.apply_library_view(LibraryView.LOCAL_ARTISTS, "Wykonawcy", [artist])
    open_artist = nav.activate_selected()[0]
    assert isinstance(open_artist, OpenLibraryView)
    assert open_artist.view is LibraryView.LOCAL_ARTIST_ALBUMS

    album = Row(
        "album-a", "Album Jeden", "album",
        service_id=r"C:\Music\Artysta A\Album Jeden", service_kind="localAlbum",
    )
    nav.apply_library_view(
        LibraryView.LOCAL_ARTIST_ALBUMS,
        "Albumy wykonawcy — Artysta A",
        [album],
        item_id="Artysta A",
    )
    open_album = nav.activate_selected()[0]
    assert isinstance(open_album, OpenLibraryView)
    assert open_album.view is LibraryView.LOCAL_ALBUM_CONTENTS

    nav.apply_library_view(
        LibraryView.LOCAL_ALBUM_CONTENTS,
        "Album — Album Jeden",
        [],
        item_id=album.service_id,
    )
    back_to_albums = nav.go_to_parent()[0]
    assert isinstance(back_to_albums, OpenLibraryView)
    assert back_to_albums.view is LibraryView.LOCAL_ARTIST_ALBUMS
    assert back_to_albums.item_id == "Artysta A"


def test_music_category_commands_are_global_and_discoverable() -> None:
    expected = {
        "1": Action.VIEW_MUSIC_SONGS,
        "2": Action.VIEW_MUSIC_ALBUMS,
        "3": Action.VIEW_MUSIC_ARTISTS,
    }
    for key, action in expected.items():
        assert resolve(Chord(key, ctrl=True, alt=True), player_view=False,
                       radio_session=False) is action
    for session in SessionId:
        actions = {
            item.action
            for menu in menus_for_session(session)
            for item in menu.items
        }
        assert set(expected.values()) <= actions
