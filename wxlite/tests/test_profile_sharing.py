"""Dwie instancje na TYCH SAMYCH danych nie moga sobie zaszkodzic.

Cel etapu 3 to jeden zestaw danych dla WPF i dla wxPython. Ryzyko jest
konkretne: dwie kopie stanu, dwa harmonogramy, to samo nagranie radiowe
uruchomione dwa razy. Te testy mierza, ze tak sie NIE dzieje -- i robia to na
osobnych PROCESACH, nie na atrapach w jednym interpreterze.
"""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sqlite3
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_db import LibraryDatabase, folder_rows  # noqa: E402
from amc_wx_lite.profile_layout import (  # noqa: E402
    HOST_OWNED_DUTIES,
    ProfileMode,
    ProfileWriteDenied,
    private_sandbox,
    read_only_mirror,
)

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"
WXLITE = str(Path(__file__).resolve().parents[1])

READER = textwrap.dedent(
    """
    import json, sys
    sys.path.insert(0, %r)
    from amc_wx_lite.library_db import LibraryDatabase, folder_rows
    db = LibraryDatabase(sys.argv[1])
    rows = folder_rows(db, None)
    sources = db.folder_sources()
    out = {
        "active": db.count_active_items(),
        "total": db.count_items(),
        "row_ids": [r.item_id for r in rows],
        "source_ids": [s.id for s in sources],
        "id_types": sorted({type(s.id).__name__ for s in sources}),
    }
    db.close()
    print(json.dumps(out))
    """
) % WXLITE


def requires_fixture(test):
    return unittest.skipUnless(LIBRARY_DB.exists(), f"Brak kopii bazy: {LIBRARY_DB}")(test)


class ProfileOwnershipRules(unittest.TestCase):
    def test_read_only_mirror_refuses_to_write_host_files(self):
        layout = read_only_mirror(local_dir=Path("/tmp/x"), profile_dir=Path("/tmp/y"))
        self.assertIs(layout.mode, ProfileMode.READ_ONLY_MIRROR)
        self.assertFalse(layout.may_write_profile)
        for name in ("state.json", "library.db", "podcasts.db"):
            with self.assertRaises(ProfileWriteDenied):
                layout.assert_may_write(Path("/tmp/x") / name)

    def test_parallel_variant_never_runs_schedulers(self):
        layout = read_only_mirror(local_dir=Path("/tmp/x"), profile_dir=Path("/tmp/y"))
        self.assertFalse(layout.runs_schedulers)
        self.assertIn("recording-scheduler", layout.duties_refused())
        self.assertEqual(set(layout.duties_refused()), set(HOST_OWNED_DUTIES))

    def test_lite_settings_never_land_inside_the_amc_profile(self):
        layout = read_only_mirror(
            local_dir=Path("/x/Local/AccessibleMediaController"),
            profile_dir=Path("/x/Roaming/AccessibleMediaController"),
        )
        lite = str(layout.lite_settings_dir)
        self.assertNotIn("Roaming/AccessibleMediaController", lite)
        self.assertNotIn("Local/AccessibleMediaController", lite)

    def test_sandbox_may_write_because_it_is_not_the_profile(self):
        layout = private_sandbox("/tmp/sandbox")
        self.assertTrue(layout.may_write_profile)
        layout.assert_may_write(Path("/tmp/sandbox/library.db"))
        self.assertFalse(layout.runs_schedulers)


@requires_fixture
class TwoProcessesShareOneDatabase(unittest.TestCase):
    """Prawdziwe dwa procesy, jedna baza, zero zapisow."""

    def _run_reader(self, db_path: Path) -> dict:
        result = subprocess.run(
            [sys.executable, "-c", READER, str(db_path)],
            capture_output=True, text=True, timeout=180,
        )
        self.assertEqual(result.returncode, 0, result.stderr[-2000:])
        return json.loads(result.stdout)

    def test_two_concurrent_readers_agree_and_change_nothing(self):
        before = hashlib.sha256(LIBRARY_DB.read_bytes()).hexdigest()
        procs = [
            subprocess.Popen(
                [sys.executable, "-c", READER, str(LIBRARY_DB)],
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
            )
            for _ in range(2)
        ]
        outs = []
        for proc in procs:
            stdout, stderr = proc.communicate(timeout=180)
            self.assertEqual(proc.returncode, 0, stderr[-2000:])
            outs.append(json.loads(stdout))

        first, second = outs
        self.assertEqual(first["active"], second["active"])
        self.assertEqual(first["total"], second["total"])
        # Identyczna kolejnosc i identyczne ID -- inaczej dwa interfejsy
        # pokazywalyby rozne Biblioteki.
        self.assertEqual(first["row_ids"], second["row_ids"])
        self.assertEqual(first["source_ids"], second["source_ids"])

        after = hashlib.sha256(LIBRARY_DB.read_bytes()).hexdigest()
        self.assertEqual(before, after, "Dwa procesy zmienily baze profilu!")

    def test_ids_cross_the_process_boundary_as_strings(self):
        # W tym projekcie napis pomylony z liczba juz raz zepsul protokol.
        out = self._run_reader(LIBRARY_DB)
        self.assertEqual(out["id_types"], ["str"])
        for source_id in out["source_ids"]:
            self.assertIsInstance(source_id, str)
            self.assertFalse(source_id.isdigit())
        local = [s.id for s in LibraryDatabase(LIBRARY_DB).folder_sources()]
        self.assertEqual(out["source_ids"], local)

    def test_reader_does_not_create_wal_or_shm_next_to_the_profile(self):
        with tempfile.TemporaryDirectory() as tmp:
            copy = Path(tmp) / "library.db"
            shutil.copy2(LIBRARY_DB, copy)
            self._run_reader(copy)
            self._run_reader(copy)
            leftovers = sorted(p.name for p in Path(tmp).iterdir())
            self.assertEqual(leftovers, ["library.db"], f"Smieci: {leftovers}")

    def test_host_can_still_write_while_python_reads(self):
        """Wlasciciel zapisu (host) nie jest blokowany przez czytelnika.

        Uwaga na ``immutable=1``: czytelnik NIE zobaczy zmian hosta na swoim
        otwartym polaczeniu. Dlatego wariant wxPython musi otwierac baze na
        nowo przy odswiezeniu Biblioteki, a nie trzymac jednego uchwytu
        bez konca.
        """
        with tempfile.TemporaryDirectory() as tmp:
            copy = Path(tmp) / "library.db"
            shutil.copy2(LIBRARY_DB, copy)
            copy.chmod(0o644)  # fixture jest 444; host ma prawo zapisu
            reader = LibraryDatabase(copy)
            self.addCleanup(reader.close)
            before = reader.count_active_items()
            writer = sqlite3.connect(copy)
            writer.create_collation("AMC_PL", lambda a, b: (a > b) - (a < b))
            writer.execute(
                "UPDATE metadata SET value = 'probe' WHERE key = 'library_initialized'"
            )
            writer.commit()
            writer.close()
            # Zapis hosta sie udal i czytelnik nadal dziala (bez wyjatku).
            self.assertEqual(reader.count_active_items(), before)
            fresh = LibraryDatabase(copy)
            self.addCleanup(fresh.close)
            self.assertEqual(
                fresh.connection.execute(
                    "SELECT value FROM metadata WHERE key = 'library_initialized'"
                ).fetchone()[0],
                "probe",
                "Ponowne otwarcie musi pokazac zapis hosta",
            )
