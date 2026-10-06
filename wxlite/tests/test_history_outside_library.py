"""Historia odtwarzania pokazuje tez DOSTEPNE pliki spoza Biblioteki.

Uzgodnienie uzytkownika: ,,samo zapisanie w profilu nie oznacza czlonkostwa''
dziala w obie strony. Plik, ktory uzytkownik OTWORZYL (Ctrl+O, wynik
wyszukiwania, nagranie), ma zostac w Ctrl+H, nawet jesli nie zostal dodany
do Biblioteki -- bo historia odpowiada na pytanie ,,co odtwarzalem'', a nie
,,co mam w Bibliotece''.

Co sie zmienia, a co NIE
------------------------
* ``history_rows`` dostaje WLASNY katalog: ``is_available = 1``.
  ``is_available`` ZOSTAJE -- niedostepny plik dalej nie ma wiersza, bo
  Enter na nim i tak nie zagralby.
* ``all_files_rows``, Foldery, Ulubione, playlisti i ZAPISANA kolejka
  zostaja na ``_ACTIVE`` (``is_available = 1 AND is_in_library = 1``).
  To widoki CZLONKOSTWA i nie wolno ich poluzowac.
* ``library_db._ACTIVE`` nie zmienia tresci -- zmiana jest wylacznie
  w katalogu historii.
* Baza dalej otwierana ``mode=ro``; nic nie jest dopisywane do Biblioteki.

Prereq: ``amc_pomoc/local-library-optin-20261006/ANALIZA.md`` rozdz. 4 + I12.
Jego ,,dopiero po WPF'' dotyczy LACZNEGO sensu funkcji (zanim WPF zacznie
produkowac ``is_in_library = 0`` przy otwarciu, w bazie takich wpisow bedzie
malo) -- nie jest zakazem przygotowania czytelnika. Osobne dziecko WPF
wlasnie oddziela katalog silnika od Biblioteki.
"""

from __future__ import annotations

import sqlite3
import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.library_activity import (
    all_bookmark_rows,
    history_rows,
    saved_queue_rows,
)
from amc_wx_lite.library_db import LibraryDatabase
from amc_wx_lite.library_views import active_items, all_files_rows

FIXTURE_DB = Path(
    "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture/library.db"
)

_SCHEMA = """
CREATE TABLE local_items (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL COLLATE AMC_PL,
    has_custom_title INTEGER NOT NULL,
    path TEXT NOT NULL,
    duration_ticks INTEGER NOT NULL,
    bitrate_estimated INTEGER NOT NULL,
    is_favorite INTEGER NOT NULL,
    is_in_library INTEGER NOT NULL,
    is_available INTEGER NOT NULL,
    is_in_queue INTEGER NOT NULL,
    is_play_next INTEGER NOT NULL,
    resume_mode INTEGER NOT NULL,
    resume_position_ticks INTEGER NOT NULL,
    is_radio_recording INTEGER NOT NULL
);
CREATE TABLE playback_history (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_regular_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_play_next_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE bookmarks (
    id TEXT PRIMARY KEY,
    ordinal INTEGER NOT NULL,
    session_id TEXT NOT NULL,
    session_name TEXT NOT NULL,
    item_id TEXT NOT NULL,
    item_title TEXT NOT NULL COLLATE AMC_PL,
    name TEXT NOT NULL COLLATE AMC_PL,
    position_ticks INTEGER NOT NULL,
    created_utc_ticks INTEGER NOT NULL,
    purpose INTEGER NOT NULL DEFAULT 1,
    chapter_origin INTEGER NOT NULL DEFAULT 0,
    chapter_source_id TEXT NULL
);
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""


def _ordinal(left: str, right: str) -> int:
    return (left > right) - (left < right)


class _Builder:
    def __init__(self, path: Path) -> None:
        self.connection = sqlite3.connect(path, isolation_level=None)
        self.connection.create_collation("AMC_PL", _ordinal)
        self.connection.execute("PRAGMA journal_mode=WAL").fetchone()
        self.connection.executescript(_SCHEMA)

    def item(
        self,
        item_id: str,
        title: str,
        path: str,
        *,
        available: bool = True,
        in_library: bool = True,
        favorite: bool = False,
    ) -> None:
        self.connection.execute(
            "INSERT INTO local_items (id, title, has_custom_title, path, "
            "duration_ticks, bitrate_estimated, is_favorite, is_in_library, "
            "is_available, is_in_queue, is_play_next, resume_mode, "
            "resume_position_ticks, is_radio_recording) "
            "VALUES (?, ?, 0, ?, 0, 0, ?, ?, ?, 0, 0, 0, 0, 0)",
            (item_id, title, path, int(favorite), int(in_library), int(available)),
        )

    def history(self, *item_ids: str, session: str = "local") -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO playback_history (session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def queue(self, *item_ids: str, session: str = "local") -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO queue_order (session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def close(self) -> None:
        self.connection.close()


class _Case(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / "library.db"
        self.build = _Builder(self.path)
        self.addCleanup(self.build.close)

    def open_db(self) -> LibraryDatabase:
        db = LibraryDatabase(self.path)
        self.addCleanup(db.close)
        return db


class HistoryKeepsAvailableNonMembers(_Case):
    def test_available_record_outside_the_library_has_a_row(self) -> None:
        """RED: ``is_in_library = 0`` + ``is_available = 1`` MA byc w Ctrl+H."""
        self.build.item("w", "W bibliotece", "C:/m/w.mp3")
        self.build.item("poza", "Poza biblioteka", "C:/m/p.mp3", in_library=False)
        self.build.history("poza", "w")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["poza", "w"])
        self.assertEqual(result.missing_item_count, 0)

    def test_member_records_are_still_there(self) -> None:
        """Poluzowanie filtra nie moze zgubic tego, co bylo widoczne."""
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.item("b", "Beta", "C:/m/b.mp3")
        self.build.history("b", "a")

        self.assertEqual(
            [row.item_id for row in history_rows(self.open_db()).rows], ["b", "a"]
        )

    def test_unavailable_record_is_still_hidden(self) -> None:
        """``is_available`` ZOSTAJE: niedostepnego pliku i tak nie da sie zagrac."""
        self.build.item("ok", "Dostepny", "C:/m/ok.mp3", in_library=False)
        self.build.item("brak", "Niedostepny", "C:/m/x.mp3", available=False)
        self.build.item(
            "brak2", "Niedostepny spoza", "C:/m/y.mp3", available=False, in_library=False
        )
        self.build.history("brak", "brak2", "ok")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["ok"])
        self.assertEqual(result.missing_item_count, 2)

    def test_record_absent_from_the_table_still_has_no_placeholder(self) -> None:
        self.build.item("a", "Alfa", "C:/m/a.mp3", in_library=False)
        self.build.history("nie-ma-takiego", "a")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a"])
        self.assertEqual(result.missing_item_count, 1)

    def test_favorites_flag_changes_nothing_here(self) -> None:
        """Ulubione to osobny zakres; historia nie patrzy na te flage."""
        self.build.item("f", "Ulubiony spoza", "C:/m/f.mp3", in_library=False, favorite=True)
        self.build.item("n", "Zwykly spoza", "C:/m/n.mp3", in_library=False)
        self.build.history("f", "n")

        self.assertEqual(
            [row.item_id for row in history_rows(self.open_db()).rows], ["f", "n"]
        )

    def test_row_carries_a_durable_id_and_path_for_play(self) -> None:
        """Enter z historii musi miec czym zagrac: trwale Id i sciezke.

        Zaden z tych wierszy nie przechodzi juz przez katalog Biblioteki,
        wiec gdyby wiersz nie niosl sciezki, odtworzenie bylo by niemozliwe.
        """
        self.build.item("poza", "Poza biblioteka", "C:/m/poza.mp3", in_library=False)
        self.build.history("poza")

        row = history_rows(self.open_db()).rows[0]

        self.assertEqual(row.item_id, "poza")
        self.assertEqual(row.path, "C:/m/poza.mp3")
        self.assertEqual(row.kind, "track")

    def test_reading_history_does_not_add_anything_to_the_library(self) -> None:
        """Samo pokazanie w Ctrl+H nie jest dodaniem do Biblioteki."""
        self.build.item("poza", "Poza biblioteka", "C:/m/poza.mp3", in_library=False)
        self.build.history("poza")

        history_rows(self.open_db())

        flag = self.build.connection.execute(
            "SELECT is_in_library FROM local_items WHERE id = 'poza'"
        ).fetchone()[0]
        self.assertEqual(flag, 0, "odczyt historii nie ustanawia czlonkostwa")


class LibraryViewsKeepTheMembershipFilter(_Case):
    """Kontrdowod: poluzowanie dotyczy WYLACZNIE historii."""

    def setUp(self) -> None:
        super().setUp()
        self.build.item("w", "W bibliotece", "C:/m/w.mp3")
        self.build.item("poza", "Poza biblioteka", "C:/m/p.mp3", in_library=False)
        self.build.history("poza", "w")
        self.build.queue("poza", "w")

    def test_all_files_still_hides_non_members(self) -> None:
        rows = all_files_rows(self.open_db()).rows
        self.assertEqual([row.item_id for row in rows], ["w"])

    def test_active_items_catalog_is_unchanged(self) -> None:
        self.assertEqual([i.id for i in active_items(self.open_db())], ["w"])

    def test_saved_queue_still_hides_non_members(self) -> None:
        """Zapisana kolejka to widok czlonkostwa i zostaje na ``_ACTIVE``."""
        rows = saved_queue_rows(self.open_db()).rows
        self.assertEqual([row.item_id for row in rows], ["w"])

    def test_history_and_all_files_now_disagree_on_purpose(self) -> None:
        db = self.open_db()
        history = {row.item_id for row in history_rows(db).rows}
        library = {row.item_id for row in all_files_rows(db).rows}
        self.assertEqual(history - library, {"poza"})
        self.assertEqual(library - history, set())

    def test_active_filter_constant_still_means_membership(self) -> None:
        from amc_wx_lite.library_db import _ACTIVE

        self.assertEqual(_ACTIVE, "is_available = 1 AND is_in_library = 1")


class BookmarksAreNotPartOfThisIncrement(_Case):
    """Zbiorczy widok zakladek ma wlasna regule i jej nie ruszamy.

    ``all_bookmark_rows`` NIE uzywa ``_active_catalog`` -- czyta tabele
    ``bookmarks`` i dopiero potem odwzorowuje Id na pliki. Ten test jest
    kontrdowodem: zmiana katalogu historii nie przeciekla do zakladek.
    """

    def test_all_bookmark_rows_still_runs_unchanged(self) -> None:
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=None,
        )
        self.assertEqual(result.rows, [])


class MeasuredOnTheProtectedFixture(unittest.TestCase):
    """Przeliczenie na READ-ONLY kopii pelnej bazy profilu."""

    @classmethod
    def setUpClass(cls) -> None:
        if not FIXTURE_DB.exists():
            raise unittest.SkipTest(f"brak kopii bazy: {FIXTURE_DB}")
        cls.db = LibraryDatabase(FIXTURE_DB)
        cls.db.__enter__()
        cls.before = FIXTURE_DB.read_bytes()

    @classmethod
    def tearDownClass(cls) -> None:
        cls.db.__exit__(None, None, None)

    def _stored(self) -> list[str]:
        return [
            str(r["item_id"])
            for r in self.db.connection.execute(
                "SELECT item_id FROM playback_history "
                "WHERE session_id = 'local' COLLATE NOCASE ORDER BY ordinal"
            )
        ]

    def _flags(self) -> dict[str, tuple[bool, bool]]:
        return {
            str(r["id"]): (bool(r["is_available"]), bool(r["is_in_library"]))
            for r in self.db.connection.execute(
                "SELECT id, is_available, is_in_library FROM local_items"
            )
        }

    def test_history_now_counts_available_records_not_members(self) -> None:
        """Liczby liczone w zrodle, nie zahardkodowane jako oczekiwanie."""
        stored = list(dict.fromkeys(self._stored()))
        flags = self._flags()
        available = [i for i in stored if flags.get(i, (False, False))[0]]

        result = history_rows(self.db)

        self.assertEqual([row.item_id for row in result.rows], available)
        self.assertEqual(result.missing_item_count, len(stored) - len(available))

    def test_the_gain_is_exactly_the_available_non_members(self) -> None:
        stored = list(dict.fromkeys(self._stored()))
        flags = self._flags()
        members = {i for i in stored if all(flags.get(i, (False, False)))}
        shown = {row.item_id for row in history_rows(self.db).rows}
        gained = shown - members

        self.assertTrue(gained, "kopia ma dostepne wpisy spoza Biblioteki")
        self.assertTrue(
            all(flags[i] == (True, False) for i in gained),
            "przybyly WYLACZNIE wpisy dostepne i poza Biblioteka",
        )

    def test_unavailable_records_are_still_absent(self) -> None:
        flags = self._flags()
        shown = {row.item_id for row in history_rows(self.db).rows}
        self.assertTrue(all(flags[i][0] for i in shown))

    def test_library_views_on_the_fixture_are_unchanged(self) -> None:
        catalog = {i.id for i in active_items(self.db)}
        flags = self._flags()
        self.assertTrue(all(all(flags[i]) for i in catalog))

    def test_reading_does_not_modify_the_fixture(self) -> None:
        history_rows(self.db)
        active_items(self.db)
        self.assertEqual(FIXTURE_DB.read_bytes(), self.before)


if __name__ == "__main__":
    unittest.main()
