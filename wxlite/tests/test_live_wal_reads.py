"""Swiezy odczyt Biblioteki przy ZYWYM, NADAL OTWARTYM writerze.

Dlaczego ten plik istnieje
--------------------------
Istniejacy ``test_host_can_still_write_while_python_reads`` MASKUJE defekt:
zamyka writer przed swiezym odczytem, a zamkniecie polaczenia robi checkpoint
WAL i przepisuje zmiane do samego ``.db``. Wtedy nawet ``immutable=1`` widzi
nowa wartosc i test jest zielony mimo zepsutego kontraktu.

Prawdziwy uklad w AMC jest inny: host C# ZYJE i trzyma baze otwarta, a zmiany
siedza w ``-wal``. Te testy trzymaja writer OTWARTY przez caly czas pomiaru.

Zmierzone (SQLite 3.53.1, wrzesniowa kopia profilu):

* ``mode=ro`` ................... widzi zatwierdzony commit hosta,
* ``mode=ro&immutable=1`` ....... widzi STARA wartosc sprzed commitu,
* ``mode=ro`` na bazie w katalogu BEZ prawa zapisu .... ``OperationalError``,
  bo WAL wymaga ``-shm``; dopiero wtedy ``immutable=1`` jest jedynym wyjsciem.

Stad kontrakt: domyslnie zwykly readonly (widzi WAL), a ``immutable`` tylko
jako jawnie oznaczony, awaryjny tryb migawki.
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

from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402

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


class _LiveHost:
    """Atrapa hosta C#: otwarte polaczenie zapisujace w trybie WAL.

    Nie zamyka sie w trakcie pomiaru -- to jest caly sens tego pliku.
    """

    def __init__(self, path: Path) -> None:
        self.connection = sqlite3.connect(path, isolation_level=None)
        self.connection.create_collation("AMC_PL", _ordinal)
        self.connection.execute("PRAGMA journal_mode=WAL").fetchone()

    def commit_marker(self, value: str) -> None:
        self.connection.execute("BEGIN")
        self.connection.execute(f"UPDATE metadata SET value = ? WHERE key = '{MARKER}'", (value,))
        self.connection.execute("COMMIT")

    def begin_uncommitted(self, value: str) -> None:
        self.connection.execute("BEGIN")
        self.connection.execute(f"UPDATE metadata SET value = ? WHERE key = '{MARKER}'", (value,))

    def rollback(self) -> None:
        self.connection.execute("ROLLBACK")

    def insert_item(self, item_id: str, title: str, path: str) -> None:
        """Wstawka zgodna z PRAWDZIWYM schematem (odczytanym z bazy, nie z pamieci).

        ``local_items`` ma kilkanascie kolumn ``NOT NULL`` bez domyslnej
        wartosci (``has_custom_title``, ``bitrate_estimated``, ``is_in_queue``,
        ``is_play_next``, ``resume_mode``, ``resume_position_ticks``).
        """
        self.connection.execute("BEGIN")
        self.connection.execute(
            "INSERT INTO local_items ("
            "id, title, has_custom_title, path, duration_ticks, bitrate_estimated, "
            "is_favorite, is_in_library, is_available, is_in_queue, is_play_next, "
            "resume_mode, resume_position_ticks, is_radio_recording"
            ") VALUES (?, ?, 0, ?, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0)",
            (item_id, title, path),
        )
        self.connection.execute("COMMIT")

    def close(self) -> None:
        self.connection.close()


@requires_fixture
class FreshReadSeesLiveHostCommits(unittest.TestCase):
    """Writer zyje i jest OTWARTY przez caly czas tych pomiarow."""

    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.db_path = Path(self.tmp.name) / "library.db"
        shutil.copy2(LIBRARY_DB, self.db_path)
        self.db_path.chmod(0o644)  # host ma prawo zapisu, tak jak w profilu
        self.host = _LiveHost(self.db_path)
        self.addCleanup(self.host.close)

    def _marker(self) -> str:
        with LibraryDatabase(self.db_path) as db:
            return db.connection.execute(
                f"SELECT value FROM metadata WHERE key = '{MARKER}'"
            ).fetchone()[0]

    def test_fresh_reader_sees_commit_while_writer_stays_open(self):
        self.host.commit_marker("zapis-hosta-1")
        self.assertEqual(
            self._marker(),
            "zapis-hosta-1",
            "Swiezy odczyt przy ZYWYM writerze musi widziec zatwierdzona zmiane",
        )

    def test_uncommitted_write_is_not_visible_and_rollback_restores(self):
        self.host.commit_marker("baza")
        self.host.begin_uncommitted("brudne")
        self.assertEqual(self._marker(), "baza", "Niezatwierdzony zapis NIE moze byc widoczny")
        self.host.rollback()
        self.assertEqual(self._marker(), "baza", "Po rollbacku wraca stan sprzed transakcji")
        self.host.commit_marker("po-rollbacku")
        self.assertEqual(self._marker(), "po-rollbacku")

    def test_new_record_raises_count_for_a_fresh_reader(self):
        with LibraryDatabase(self.db_path) as db:
            before = db.count_items()
        self.host.insert_item("probe:wal:1", "Probny wpis WAL", "D:\\Probny\\wal.mp3")
        with LibraryDatabase(self.db_path) as db:
            self.assertEqual(
                db.count_items(), before + 1,
                "Swiezy LibraryDatabase musi policzyc rekord dopisany przez zywego hosta",
            )

    def test_reader_reports_that_it_tracks_live_writes(self):
        with LibraryDatabase(self.db_path) as db:
            self.assertTrue(
                db.sees_live_writes,
                "Zwykly odczyt profilu ma sledzic WAL, nie zamrazac obrazu bazy",
            )

    def test_wal_bytes_exist_during_the_measurement(self):
        """Dowod, ze pomiar naprawde szedl przez WAL, a nie po checkpointcie."""
        self.host.commit_marker("wal-zywy")
        wal = Path(str(self.db_path) + "-wal")
        self.assertTrue(wal.exists() and wal.stat().st_size > 0, "Brak zywego -wal: pomiar bez wartosci")
        self.assertEqual(self._marker(), "wal-zywy")


@requires_fixture
class ImmutableSnapshotIsAnExplicitFallback(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name) / "profil"
        self.dir.mkdir()
        self.db_path = self.dir / "library.db"
        shutil.copy2(LIBRARY_DB, self.db_path)
        self.db_path.chmod(0o644)

    def test_readonly_location_still_opens_and_says_it_is_a_snapshot(self):
        host = _LiveHost(self.db_path)
        host.commit_marker("przed-zamknieciem")
        host.close()  # checkpoint: -wal/-shm znikaja, zmiana jest w .db
        self.db_path.chmod(0o444)
        self.dir.chmod(0o555)
        self.addCleanup(self.dir.chmod, 0o755)
        with LibraryDatabase(self.db_path) as db:
            self.assertEqual(db.count_items(), 11200)
            self.assertFalse(
                db.sees_live_writes,
                "W katalogu bez prawa zapisu dziala tylko migawka -- i trzeba to przyznac",
            )

    def test_strict_mode_refuses_the_stale_snapshot_instead_of_lying(self):
        self.db_path.chmod(0o444)
        self.dir.chmod(0o555)
        self.addCleanup(self.dir.chmod, 0o755)
        with self.assertRaises(sqlite3.OperationalError):
            LibraryDatabase(self.db_path, allow_snapshot_fallback=False)


@requires_fixture
class DatabaseUriIsBuiltSafely(unittest.TestCase):
    def test_path_with_hash_question_mark_and_spaces_opens(self):
        with tempfile.TemporaryDirectory() as tmp:
            awkward = Path(tmp) / "Moja muzyka #1 ? kopia"
            awkward.mkdir()
            target = awkward / "library.db"
            shutil.copy2(LIBRARY_DB, target)
            target.chmod(0o644)
            with LibraryDatabase(target) as db:
                self.assertEqual(db.count_items(), 11200)

    def test_uri_escapes_reserved_characters(self):
        with tempfile.TemporaryDirectory() as tmp:
            awkward = Path(tmp) / "a b#c?d"
            awkward.mkdir()
            target = awkward / "library.db"
            shutil.copy2(LIBRARY_DB, target)
            target.chmod(0o644)
            with LibraryDatabase(target) as db:
                self.assertIn("%23", db.uri)
                self.assertIn("%3F", db.uri)
                self.assertIn("%20", db.uri)
                self.assertNotIn("immutable", db.uri)


if __name__ == "__main__":
    unittest.main(verbosity=2)
