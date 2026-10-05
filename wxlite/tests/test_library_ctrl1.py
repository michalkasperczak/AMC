"""Ctrl+1 pokazuje PRAWDZIWA Biblioteke i nie blokuje okna.

Test pilnuje dokladnie tego, co zglosil uzytkownik: pod Ctrl+1 nie wczytywaly
sie ani pliki, ani foldery. Regresja jest przypieta na dwa sposoby:

1. ``_load_initial_content`` NIE moze juz decydowac przez ``Path.exists`` --
   sciezki profilu nie istnieja na tej maszynie, a lista ma byc pelna;
2. odczyt 11 tysiecy rekordow idzie przez ``runner.submit``, czyli poza watek
   GUI.
"""

from __future__ import annotations

import os
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.library_source import LibrarySource  # noqa: E402
from amc_wx_lite.profile_layout import private_sandbox  # noqa: E402

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)


def requires_fixture(test):
    return unittest.skipUnless(
        (FIXTURE / "library.db").exists(), f"Brak kopii bazy w {FIXTURE}"
    )(test)


def _frame(library: LibrarySource, queued: list, said: list):
    """Atrapa okna: tylko te pola, ktorych dotyka wczytywanie zawartosci."""
    frame = SimpleNamespace(
        library=library,
        options=SimpleNamespace(last_folder=None),
        announcer=SimpleNamespace(say=said.append),
        runner=SimpleNamespace(
            submit=lambda tag, work, done, failed: queued.append((tag, work, done, failed))
        ),
        navigator=SimpleNamespace(
            apply_stations=lambda rows, preferred_id=None: [],
            apply_folder=lambda path, rows, preferred_id=None: [("folder", path, rows)],
            sessions={gui.SessionId.FILES: SimpleNamespace(folder_path=None)},
        ),
        stations=SimpleNamespace(as_payload=lambda: []),
        # Radio czyta teraz stacje z profilu AMC; atrapa oddaje puste zrodlo.
        # ``load_error=None`` znaczy "odczyt sie udal, profil po prostu nie ma
        # stacji" -- to NIE jest blad i nic nie ma byc o nim powiedziane.
        _radio_snapshot=SimpleNamespace(current_id=None, load_error=None, kept_previous=False),
        _run=lambda intents: None,
    )
    # Prawdziwa metoda z gui.py, tylko podpieta do atrapy -- testujemy KOD
    # produkcyjny, nie jego kopie.
    frame._open_library = gui.LiteFrame._open_library.__get__(frame, type(frame))
    return frame


@requires_fixture
class LibraryLoadsUnderCtrl1(unittest.TestCase):
    def setUp(self):
        self.library = LibrarySource(private_sandbox(FIXTURE))
        self.queued: list = []
        self.said: list = []
        self.frame = _frame(self.library, self.queued, self.said)

    def test_initial_content_reads_the_library_not_the_disk(self):
        gui.LiteFrame._load_initial_content(self.frame)
        self.assertEqual(len(self.queued), 1, "Biblioteka musi byc wczytana")
        tag, work, done, _failed = self.queued[0]
        self.assertEqual(tag, "folder")
        snapshot = work()
        self.assertGreater(len(snapshot.rows), 0, "Lista NIE moze byc pusta")
        self.assertEqual(snapshot.total_active, 2475)

    def test_gui_thread_does_no_database_io(self):
        gui.LiteFrame._load_initial_content(self.frame)
        # Przed wykonaniem zadania w tle nie wolno miec gotowych wierszy:
        # odczyt 11 tysiecy rekordow stoi w kolejce runnera, nie w GUI.
        self.assertEqual(len(self.queued), 1)
        self.assertEqual(self.queued[0][0], "folder")
        # Rutynowej zapowiedzi \"Wczytywanie Biblioteki...\" tu BYC NIE MA.
        # Test wymagal jej wczesniej, ale oryginal przy wejsciu w widok milczy,
        # a dla uzytkownika czytnika to byl szum przed kazda lista. Praca w tle
        # jest udowodniona kolejka wyzej, nie komunikatem.
        self.assertEqual(
            [text for text in self.said if "Wczytywanie" in text], []
        )

    def test_root_shows_all_three_sources_and_orphans(self):
        snapshot = self.library.load(None)
        folders = [r for r in snapshot.rows if r.kind == "folder"]
        tracks = [r for r in snapshot.rows if r.kind == "track"]
        self.assertEqual(len(folders), 3)
        self.assertEqual(len(tracks), 7)
        self.assertEqual(snapshot.heading, "Biblioteka — Foldery")
        # Dowod, ze nie filtrujemy po dysku.
        self.assertFalse(any(Path(r.path).exists() for r in folders))

    def test_descending_keeps_polish_names_and_adds_parent_row(self):
        root = self.library.load(None)
        kazania = next(
            r for r in root.rows if r.title == "Kazania Dominikanie Grobla"
        )
        level = self.library.load(kazania.path)
        self.assertEqual(level.rows[0].kind, "parent")
        self.assertEqual(level.rows[0].title, "..")
        self.assertIn("Kazania Dominikanie Grobla", level.heading)
        self.assertGreater(len([r for r in level.rows if r.kind != "parent"]), 0)

    def test_rows_carry_stable_string_ids(self):
        snapshot = self.library.load(None)
        ids = [r.item_id for r in snapshot.rows]
        self.assertEqual(len(ids), len(set(ids)), "ID musza byc unikalne")
        for row_id in ids:
            self.assertIsInstance(row_id, str)
        again = [r.item_id for r in self.library.load(None).rows]
        self.assertEqual(ids, again, "ID i kolejnosc musza byc powtarzalne")

    def test_saved_folder_comes_from_amc_local_state(self):
        # AMC zapisalo ``current_folder_path = NULL`` -> startujemy w korzeniu.
        self.assertIsNone(self.library.saved_folder())

    def test_missing_library_falls_back_with_a_spoken_hint(self):
        empty = LibrarySource(private_sandbox("/nonexistent-amc-profile"))
        frame = _frame(empty, [], [])
        said: list = []
        frame.announcer = SimpleNamespace(say=said.append)
        gui.LiteFrame._load_initial_content(frame)
        self.assertTrue(any("Ctrl+O" in text for text in said), said)


if __name__ == "__main__":
    unittest.main(verbosity=2)
