"""Cichy powrot do zamrozonej migawki NIE MOZE byc domyslny.

Co bylo zle
-----------
``LibraryDatabase`` mialo ``allow_snapshot_fallback=True`` i po KAZDYM
``OperationalError`` przechodzilo na ``immutable=1``. Zmierzone tutaj na
prawdziwej kopii profilu, z writerem NADAL OTWARTYM i NIEPUSTYM ``-wal``:

* ``mode=ro`` w katalogu bez prawa zapisu  -> ``unable to open database file``,
* ``mode=ro&immutable=1`` na tym samym pliku -> oddaje znacznik SPRZED commitu,
  bo ``immutable`` kaze SQLite pominac ``-wal`` (4152 bajty realnej tresci).

Skutek dla uzytkownika byl cichy: okno pokazywalo STARY profil i nic o tym nie
mowilo, bo ``sees_live_writes`` nie wychodzilo poza ``LibraryDatabase``.

Kontrakt po poprawce
--------------------
1. Domyslnie **brak fallbacku**: blad odczytu zywego profilu wychodzi na wierzch.
2. Migawka tylko w SWIADOMYM trybie (``allow_snapshot_fallback=True``) i tylko
   wtedy, gdy jest naprawde spojna -- czyli gdy nie ma nieprzeniesionego ``-wal``.
3. Czesciowo otwarte polaczenie jest zamykane przed retry/wyjsciem.
4. Zwykly zywy readonly dziala bez zmian.
5. ``LibrarySnapshot`` niesie ``sees_live_writes``, a okno ma z czego zrobic
   krotki, PRAWDZIWY sygnal degradacji.
"""

from __future__ import annotations

import os
import shutil
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_db import (  # noqa: E402
    LibraryDatabase,
    LiveProfileReadDenied,
    StaleSnapshotRefused,
)

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"
MARKER = "library_initialized"


def requires_fixture(test):
    return unittest.skipUnless(LIBRARY_DB.exists(), f"Brak kopii bazy: {LIBRARY_DB}")(test)


def _ordinal(left: str, right: str) -> int:
    return (left > right) - (left < right)


class _LiveWriter:
    """Writer, ktory ZOSTAJE OTWARTY -- inaczej checkpoint maskuje defekt."""

    def __init__(self, path: Path) -> None:
        self.connection = sqlite3.connect(path, isolation_level=None)
        self.connection.create_collation("AMC_PL", _ordinal)
        self.connection.execute("PRAGMA journal_mode=WAL").fetchone()

    def commit_marker(self, value: str) -> None:
        self.connection.execute("BEGIN")
        self.connection.execute(
            f"UPDATE metadata SET value = ? WHERE key = '{MARKER}'", (value,)
        )
        self.connection.execute("COMMIT")

    def close(self) -> None:
        self.connection.close()


@requires_fixture
class DefaultReadNeverHidesTheLiveProfileBehindASnapshot(unittest.TestCase):
    """Baza + NIEPUSTY ``-wal`` w miejscu bez prawa zapisu."""

    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        base = Path(self.tmp.name)

        live = base / "zywy"
        live.mkdir()
        source = live / "library.db"
        shutil.copy2(LIBRARY_DB, source)
        source.chmod(0o644)
        self.writer = _LiveWriter(source)
        self.addCleanup(self.writer.close)
        self.writer.commit_marker("zapis-w-wal")

        # Przenosimy baze RAZEM z dziennikiem: tresc commitu lezy w -wal.
        self.dir = base / "bez-zapisu"
        self.dir.mkdir()
        self.db_path = self.dir / "library.db"
        shutil.copy2(source, self.db_path)
        shutil.copy2(str(source) + "-wal", str(self.db_path) + "-wal")
        self.wal = Path(str(self.db_path) + "-wal")
        self.assertGreater(self.wal.stat().st_size, 0, "Pomiar bez zywego -wal nie ma wartosci")

        for entry in self.dir.iterdir():
            entry.chmod(0o444)
        self.dir.chmod(0o555)
        self.addCleanup(self._restore)

    def _restore(self) -> None:
        self.dir.chmod(0o755)
        for entry in self.dir.iterdir():
            entry.chmod(0o644)

    def test_default_refuses_instead_of_serving_the_stale_snapshot(self):
        with self.assertRaises(LiveProfileReadDenied):
            LibraryDatabase(self.db_path)

    def test_the_refusal_names_the_profile_and_the_cause(self):
        try:
            LibraryDatabase(self.db_path)
        except LiveProfileReadDenied as error:
            text = str(error)
        else:
            self.fail("Odczyt mial odmowic")
        self.assertIn("library.db", text)
        self.assertIn("unable to open database file", text)

    def test_conscious_snapshot_mode_refuses_when_wal_is_not_merged(self):
        """Jawna migawka musi byc SPOJNA. Z nieprzeniesionym -wal nie jest."""
        with self.assertRaises(StaleSnapshotRefused):
            LibraryDatabase(self.db_path, allow_snapshot_fallback=True)

    def test_refusal_leaves_no_open_connection_and_no_new_files(self):
        before = sorted(p.name for p in self.dir.iterdir())
        for attempt in (
            lambda: LibraryDatabase(self.db_path),
            lambda: LibraryDatabase(self.db_path, allow_snapshot_fallback=True),
        ):
            with self.assertRaises(sqlite3.Error):
                attempt()
        self.assertEqual(sorted(p.name for p in self.dir.iterdir()), before)


@requires_fixture
class ConsciousSnapshotIsAllowedOnlyWhenItIsConsistent(unittest.TestCase):
    """Bez ``-wal`` (po checkpointcie) migawka jest uczciwa -- i wolno ja wziac."""

    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name) / "profil"
        self.dir.mkdir()
        self.db_path = self.dir / "library.db"
        shutil.copy2(LIBRARY_DB, self.db_path)
        self.db_path.chmod(0o644)
        writer = _LiveWriter(self.db_path)
        writer.commit_marker("przed-checkpointem")
        writer.close()  # checkpoint: -wal znika, tresc jest w .db
        self.assertFalse(Path(str(self.db_path) + "-wal").exists())
        self.db_path.chmod(0o444)
        self.dir.chmod(0o555)
        self.addCleanup(self._restore)

    def _restore(self) -> None:
        self.dir.chmod(0o755)
        self.db_path.chmod(0o644)

    def test_snapshot_opens_and_admits_it_is_not_live(self):
        with LibraryDatabase(self.db_path, allow_snapshot_fallback=True) as db:
            self.assertEqual(db.count_items(), 11200)
            self.assertFalse(db.sees_live_writes)

    def test_snapshot_carries_the_committed_value_not_an_older_one(self):
        with LibraryDatabase(self.db_path, allow_snapshot_fallback=True) as db:
            value = db.connection.execute(
                f"SELECT value FROM metadata WHERE key = '{MARKER}'"
            ).fetchone()[0]
        self.assertEqual(value, "przed-checkpointem")


@requires_fixture
class NormalLiveReadonlyStillWorks(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.db_path = Path(self.tmp.name) / "library.db"
        shutil.copy2(LIBRARY_DB, self.db_path)
        self.db_path.chmod(0o644)
        self.writer = _LiveWriter(self.db_path)
        self.addCleanup(self.writer.close)

    def test_default_path_sees_live_writes_from_an_open_writer(self):
        self.writer.commit_marker("po-poprawce")
        with LibraryDatabase(self.db_path) as db:
            self.assertTrue(db.sees_live_writes)
            self.assertEqual(
                db.connection.execute(
                    f"SELECT value FROM metadata WHERE key = '{MARKER}'"
                ).fetchone()[0],
                "po-poprawce",
            )

    def test_default_uri_has_no_immutable_flag(self):
        with LibraryDatabase(self.db_path) as db:
            self.assertNotIn("immutable", db.uri)


@requires_fixture
class SnapshotStateReachesTheSnapshotAndTheWindow(unittest.TestCase):
    """``sees_live_writes`` musi wyjsc z ``LibraryDatabase`` do GUI."""

    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)
        shutil.copy2(LIBRARY_DB, self.dir / "library.db")
        (self.dir / "library.db").chmod(0o644)

    def _layout(self):
        from amc_wx_lite.profile_layout import private_sandbox

        return private_sandbox(self.dir)

    def test_live_snapshot_reports_that_it_is_live(self):
        from amc_wx_lite.library_source import LibrarySource

        snapshot = LibrarySource(self._layout()).load(None)
        self.assertTrue(snapshot.sees_live_writes)

    def test_notice_is_empty_when_nothing_is_degraded(self):
        from amc_wx_lite.library_source import LibrarySnapshot, degradation_notice

        fresh = LibrarySnapshot(
            rows=[], heading="x", folder_path=None, total_active=0,
            order_matches_amc=True, sees_live_writes=True,
        )
        self.assertEqual(degradation_notice(fresh), "")

    def test_notice_names_the_stale_snapshot(self):
        from amc_wx_lite.library_source import LibrarySnapshot, degradation_notice

        stale = LibrarySnapshot(
            rows=[], heading="x", folder_path=None, total_active=0,
            order_matches_amc=True, sees_live_writes=False,
        )
        text = degradation_notice(stale)
        self.assertTrue(text, "Migawka musi dac widoczny sygnal, nie cisze")
        self.assertIn("migawka", text.lower())

    def test_notice_names_the_substitute_order(self):
        from amc_wx_lite.library_source import LibrarySnapshot, degradation_notice

        unsorted = LibrarySnapshot(
            rows=[], heading="x", folder_path=None, total_active=0,
            order_matches_amc=False, sees_live_writes=True,
        )
        text = degradation_notice(unsorted)
        self.assertIn("kolejno", text.lower())

    def test_both_problems_are_reported_together(self):
        from amc_wx_lite.library_source import LibrarySnapshot, degradation_notice

        both = LibrarySnapshot(
            rows=[], heading="x", folder_path=None, total_active=0,
            order_matches_amc=False, sees_live_writes=False,
        )
        text = degradation_notice(both)
        self.assertIn("migawka", text.lower())
        self.assertIn("kolejno", text.lower())


if __name__ == "__main__":
    unittest.main(verbosity=2)
