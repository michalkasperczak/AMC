"""Biblioteka (Ctrl+1) na PRAWDZIWEJ bazie AMC, nie na systemie plikow.

Obserwowany blad uzytkownika: pod Ctrl+1 nie wczytuja sie ani pliki, ani
foldery. Przyczyna w kodzie: ``gui._load_initial_content`` pytalo wylacznie
o ``Path(folder).exists()`` i hosta ``files.listFolder`` -- czyli czytalo
DYSK, a nie Biblioteke AMC. Biblioteka zyje w ``library.db`` (SQLite) i w
HERMESie zadna z tych sciezek nie istnieje (profil nagrany jako ``micha``,
tu nie ma ani ``D:\\`` ani ``C:\\Users\\micha``), wiec lista byla pusta.

Te testy przypinaja kontrakt odczytu do schematu C#
(``LocalLibraryDatabase.cs`` + ``MainWindow.CreateFolderRows``):

* kolacja ``AMC_PL`` MUSI byc zarejestrowana, inaczej SQLite odmawia zapytan
  o ``local_items.title`` / ``folder_sources.display_name``;
* wiersze korzenia to ``folder_sources`` (po ``display_name``), a nie dysk;
* ``is_available``/``is_in_library`` filtruja tak jak ``ActiveLocalItems()``;
* ID zostaja NAPISAMI (w tym projekcie mylenie napisu z liczba juz raz
  zepsulo protokol) i nie wolno ich przenumerowac;
* NIE wolno odsiewac wierszy przez ``Path.exists`` -- D:\\ i C:\\Users\\micha
  nie istnieja na tej maszynie, a rekordy i tak maja byc widoczne.
"""

from __future__ import annotations

import os
import sqlite3
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_db import (  # noqa: E402
    AMC_PL,
    LibraryDatabase,
    folder_rows,
    polish_collation,
)

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"

# Liczby PRZELICZONE z pliku fixture (nie przepisane z historycznych kwitow).
EXPECTED_TOTAL_ITEMS = 11200
EXPECTED_ACTIVE_ITEMS = 2475
EXPECTED_SOURCES = 3
EXPECTED_SCHEMA_VERSION = "9"


def requires_fixture(test):
    return unittest.skipUnless(
        LIBRARY_DB.exists(), f"Brak kopii bazy: {LIBRARY_DB}"
    )(test)


class CollationContract(unittest.TestCase):
    """Bez AMC_PL baza nie da sie czytac -- to twardy kontrakt schematu."""

    def test_plain_sqlite_connection_cannot_query_titles(self):
        if not LIBRARY_DB.exists():
            self.skipTest("Brak kopii bazy")
        raw = sqlite3.connect(f"file:{LIBRARY_DB}?mode=ro&immutable=1", uri=True)
        self.addCleanup(raw.close)
        with self.assertRaises(sqlite3.OperationalError):
            raw.execute("select title from local_items order by title limit 1").fetchall()

    def test_collation_orders_polish_letters_after_base_letter(self):
        # "lodz" < "lodz z kreska" < "m..." -- polskie znaki nie moga ladowac
        # na koncu alfabetu, bo lista Biblioteki jest czytana po kolei.
        words = ["zebra", "lodz", "\u0142\u00f3d\u017a", "mama", "\u0105gata", "agata"]
        ordered = sorted(words, key=None, reverse=False)
        del ordered
        got = sorted(words, key=lambda w: w, reverse=False)
        del got
        result = sorted(words, key=_CollKey)
        self.assertLess(result.index("agata"), result.index("mama"))
        self.assertLess(result.index("\u0105gata"), result.index("mama"))
        self.assertLess(result.index("\u0142\u00f3d\u017a"), result.index("mama"))
        self.assertEqual(result[-1], "zebra")


class _CollKey:
    __slots__ = ("value",)

    def __init__(self, value: str) -> None:
        self.value = value

    def __lt__(self, other: "_CollKey") -> bool:
        return polish_collation(self.value, other.value) < 0


@requires_fixture
class RealLibraryContract(unittest.TestCase):
    """Odczyt pelnej Biblioteki z kopii prawdziwego profilu."""

    @classmethod
    def setUpClass(cls):
        cls.db = LibraryDatabase(LIBRARY_DB)

    @classmethod
    def tearDownClass(cls):
        cls.db.close()

    def test_registers_amc_pl_collation(self):
        self.assertEqual(AMC_PL, "AMC_PL")
        rows = self.db.connection.execute(
            "select title from local_items order by title collate AMC_PL limit 3"
        ).fetchall()
        self.assertEqual(len(rows), 3)

    def test_reads_whole_library_not_a_demo_slice(self):
        self.assertEqual(self.db.count_items(), EXPECTED_TOTAL_ITEMS)
        self.assertEqual(self.db.count_active_items(), EXPECTED_ACTIVE_ITEMS)
        self.assertGreater(self.db.count_active_items(), 1000)

    def test_schema_version_matches_csharp_store(self):
        self.assertEqual(self.db.schema_version(), EXPECTED_SCHEMA_VERSION)

    def test_folder_sources_keep_id_order_and_polish_names(self):
        sources = self.db.folder_sources()
        self.assertEqual(len(sources), EXPECTED_SOURCES)
        # Kontrakt C# (CreateFolderRows): kolejnosc po display_name, a NIE po
        # ordinal. Tu daje to Kazania(2) < Muzyka(1) < Sideloads(0).
        self.assertEqual(
            [s.display_name for s in sources],
            ["Kazania Dominikanie Grobla", "Muzyka najnowsza iCloud", "Sideloads"],
        )
        self.assertEqual(sorted(s.ordinal for s in sources), [0, 1, 2])
        for s in sources:
            self.assertIsInstance(s.id, str)          # napis, NIE liczba
            self.assertEqual(len(s.id), 32)
            self.assertTrue(s.path and s.display_name)
        names = {s.display_name for s in sources}
        self.assertIn("Kazania Dominikanie Grobla", names)

    def test_root_rows_come_from_sources_not_from_disk(self):
        rows = folder_rows(self.db, None)
        kinds = {r.kind for r in rows}
        self.assertIn("folder", kinds)
        folders = [r for r in rows if r.kind == "folder"]
        self.assertEqual(len(folders), EXPECTED_SOURCES)
        # Zadna z tych sciezek NIE istnieje na tej maszynie -- i wlasnie
        # dlatego poprzednia wersja pokazywala pusta liste.
        self.assertFalse(any(Path(r.path).exists() for r in folders))
        self.assertTrue(all(r.item_id.startswith("dir:") for r in folders))

    def test_root_also_lists_orphan_items_outside_every_source(self):
        rows = folder_rows(self.db, None)
        tracks = [r for r in rows if r.kind == "track"]
        self.assertEqual(len(tracks), 7)

    def test_descending_into_a_source_lists_children_and_files(self):
        source = next(
            s for s in self.db.folder_sources()
            if s.display_name == "Kazania Dominikanie Grobla"
        )
        rows = folder_rows(self.db, source.path)
        self.assertGreater(len(rows), 0)
        total = self.db.count_active_under(source.path)
        self.assertEqual(total, 1533)
        # Wejscie w folder nie moze zgubic ani jednego rekordu z poddrzewa.
        direct = sum(1 for r in rows if r.kind == "track")
        children = [r for r in rows if r.kind == "folder"]
        reachable = direct + sum(
            self.db.count_active_under(c.path) for c in children
        )
        self.assertEqual(reachable, total)

    def test_item_ids_are_strings_and_survive_a_round_trip(self):
        items = self.db.active_items_under(None, limit=50)
        self.assertEqual(len(items), 50)
        for item in items:
            self.assertIsInstance(item.id, str)
            self.assertFalse(item.id.isdigit(), "ID to napis, nie liczba")
            self.assertTrue(item.path)

    def test_distant_paths_are_not_filtered_out(self):
        # D:\ nie istnieje w HERMES, ale rekordy MUSZA byc widoczne.
        drives = self.db.active_path_prefixes()
        self.assertIn("D:\\", drives)
        self.assertGreater(drives["D:\\"], 100)

    def test_local_state_gives_the_saved_library_view(self):
        state = self.db.local_state()
        self.assertEqual(state.library_view, "Foldery")
        self.assertIsInstance(state.current_item_id, str)


@requires_fixture
class SourceIsNeverMutated(unittest.TestCase):
    """Odczyt profilu nie moze migrowac ani zapisywac w tle."""

    def test_opening_the_database_leaves_bytes_and_mtime_untouched(self):
        import hashlib

        before = hashlib.sha256(LIBRARY_DB.read_bytes()).hexdigest()
        before_mtime = LIBRARY_DB.stat().st_mtime_ns
        db = LibraryDatabase(LIBRARY_DB)
        db.count_active_items()
        db.folder_sources()
        folder_rows(db, None)
        db.close()
        after = hashlib.sha256(LIBRARY_DB.read_bytes()).hexdigest()
        self.assertEqual(before, after, "Baza zmieniona przez sam odczyt!")
        self.assertEqual(before_mtime, LIBRARY_DB.stat().st_mtime_ns)

    def test_no_side_car_files_are_created(self):
        for suffix in ("-wal", "-shm", "-journal"):
            side = LIBRARY_DB.with_name(LIBRARY_DB.name + suffix)
            existed = side.exists()
            db = LibraryDatabase(LIBRARY_DB)
            db.count_items()
            db.close()
            self.assertEqual(
                side.exists(), existed,
                f"Odczyt stworzyl {side.name} -- to juz jest zapis do profilu",
            )

    def test_writes_are_refused_by_the_connection(self):
        db = LibraryDatabase(LIBRARY_DB)
        self.addCleanup(db.close)
        with self.assertRaises(sqlite3.OperationalError):
            db.connection.execute("delete from local_items")


if __name__ == "__main__":
    unittest.main(verbosity=2)
