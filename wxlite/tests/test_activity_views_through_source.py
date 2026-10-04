"""Trzy widoki aktywnosci PRZEZ ``LibrarySource`` i przez brame hosta.

Warstwa danych i nawigacja sa sprawdzone osobno. Tutaj domykamy droge:
``load_view`` musi te widoki znac, a ``play_file`` musi przeniesc pozycje
zakladki do hosta JEDNYM wywolaniem, nie przez osobny ``seek``.
"""

from __future__ import annotations

import sqlite3
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_source import LibrarySource
from amc_wx_lite.profile_layout import ProfileLayout, ProfileMode

SCHEMA = """
CREATE TABLE local_items (
    id TEXT, title TEXT, path TEXT, duration_ticks INTEGER,
    is_favorite INTEGER, is_available INTEGER, is_in_library INTEGER,
    is_in_queue INTEGER, is_play_next INTEGER, is_radio_recording INTEGER);
CREATE TABLE playback_history (session_id TEXT, item_id TEXT, ordinal INTEGER);
CREATE TABLE queue_order (session_id TEXT, item_id TEXT, ordinal INTEGER);
CREATE TABLE queue_regular_order (session_id TEXT, item_id TEXT, ordinal INTEGER);
CREATE TABLE queue_play_next_order (session_id TEXT, item_id TEXT, ordinal INTEGER);
CREATE TABLE bookmarks (
    id TEXT, session_id TEXT, session_name TEXT, item_id TEXT, item_title TEXT,
    name TEXT, position_ticks INTEGER, created_utc_ticks INTEGER, purpose INTEGER);
CREATE TABLE folder_sources (path TEXT);
"""


def make_db(tmp: Path) -> Path:
    path = tmp / "library.db"
    con = sqlite3.connect(path)
    con.executescript(SCHEMA)
    con.execute(
        "INSERT INTO local_items VALUES ('42','Alfa','C:\\m\\Alfa.mp3',600000000,0,1,1,0,0,0)"
    )
    con.execute(
        "INSERT INTO local_items VALUES ('43','Beta','C:\\m\\Beta.mp3',600000000,0,1,1,1,0,0)"
    )
    con.execute("INSERT INTO playback_history VALUES ('local','42',0)")
    con.execute("INSERT INTO queue_order VALUES ('local','43',0)")
    con.execute("INSERT INTO queue_regular_order VALUES ('local','43',0)")
    # 83.456 s = 834_560_000 tickow -- czesc ulamkowa MUSI przezyc.
    con.execute(
        "INSERT INTO bookmarks VALUES "
        "('b1','local','Pliki lokalne','42','Alfa','',834560000,0,1)"
    )
    con.commit()
    con.close()
    return path


def source(tmp: Path) -> LibrarySource:
    """``LibrarySource`` na WLASNEJ, tymczasowej bazie -- nie na profilu."""
    db = make_db(tmp)
    layout = ProfileLayout(
        mode=ProfileMode.READ_ONLY_MIRROR,
        library_db=db,
        podcasts_db=tmp / "podcasts.db",
        state_json=tmp / "state.json",
        lite_settings_dir=tmp / "_lite",
    )
    return LibrarySource(layout)


def test_load_view_knows_the_three_activity_views() -> None:
    with tempfile.TemporaryDirectory() as raw:
        src = source(Path(raw))
        history = src.load_view("history")
        assert [r.title for r in history.rows] == ["Alfa"]
        assert "Historia" in history.heading

        queue = src.load_view("saved_queue")
        assert [r.title for r in queue.rows] == ["Beta"]
        # Naglowek nie moze sugerowac uruchomionej kolejki silnika.
        assert "kolejka" in queue.heading.lower()

        marks = src.load_view("item_bookmarks", item_id="42")
        assert len(marks.rows) == 1
        assert marks.rows[0].item_id == "bookmark:b1"


def test_item_bookmarks_without_item_id_is_a_programming_error() -> None:
    """``GetForItem`` bez Id nie istnieje -- nie wolno cicho oddac pustki."""
    with tempfile.TemporaryDirectory() as raw:
        src = source(Path(raw))
        try:
            src.load_view("item_bookmarks")
        except ValueError:
            return
        raise AssertionError("brak item_id musi byc bledem, nie pusta lista")


def test_bookmark_targets_carry_the_real_path_and_fractional_position() -> None:
    """Mapa celow: Row.item_id -> (sciezka PLIKU, sekundy z ulamkiem, tytul)."""
    with tempfile.TemporaryDirectory() as raw:
        src = source(Path(raw))
        result = src.load_view("item_bookmarks", item_id="42")
        targets = result.bookmark_targets
        assert "bookmark:b1" in targets
        path, seconds, title = targets["bookmark:b1"]
        assert path == "C:\\m\\Alfa.mp3", "do backendu idzie PLIK, nie bookmark:<id>"
        assert abs(seconds - 83.456) < 1e-9, "ulamek pozycji nie moze zginac"
        assert title == "Alfa"


def test_bookmark_outside_the_catalog_has_no_target_instead_of_a_fake_one() -> None:
    """Zakladki nie maja filtra ActiveLocalItems: cel moze byc nieznany."""
    with tempfile.TemporaryDirectory() as raw:
        tmp = Path(raw)
        src = source(tmp)
        con = sqlite3.connect(tmp / "library.db")
        con.execute(
            "INSERT INTO bookmarks VALUES "
            "('b9','local','Pliki lokalne','999','Zniknal','',10000000,0,1)"
        )
        con.commit()
        con.close()
        result = src.load_view("item_bookmarks", item_id="999")
        assert len(result.rows) == 1, "wiersz zostaje, tak jak w oryginale"
        assert "bookmark:b9" not in result.bookmark_targets


def _spy_client():
    """``LiteHostClient`` bez procesu: liczymy WYWOLANIA protokolu."""
    from amc_wx_lite.host_client import LiteHostClient

    sent: list[tuple[str, dict]] = []

    class Spy(LiteHostClient):
        def __init__(self) -> None:  # bez procesu i bez gniazda
            pass

        def call(self, op, args=None, *, timeout=20.0):
            sent.append((op, dict(args or {})))
            return {"ok": True}

    return Spy(), sent


def test_play_file_sends_position_in_one_call_not_a_separate_seek() -> None:
    """``play.file`` niesie ``positionSeconds`` (LiteEngineHandlers.cs:290).

    Osobny ``transport.seek`` po ``files.play`` scigal by sie z rozruchem
    strumienia, ktory zeruje czas (cs:309).
    """
    client, sent = _spy_client()
    client.play_file("C:\\m\\Alfa.mp3", volume=50, rate=1.0, position_seconds=83.456)
    assert len(sent) == 1, f"jedno wywolanie, nie play+seek: {sent}"
    op, args = sent[0]
    assert op == "files.play"
    assert abs(args["positionSeconds"] - 83.456) < 1e-9


def test_play_file_omits_position_when_starting_from_zero() -> None:
    """Zwykle odtwarzanie nie zmienia protokolu -- brak pola, jak dotad."""
    client, sent = _spy_client()
    client.play_file("C:\\m\\Alfa.mp3", volume=50, rate=1.0)
    assert "positionSeconds" not in sent[0][1]
