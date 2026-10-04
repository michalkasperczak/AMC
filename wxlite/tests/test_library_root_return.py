"""Powrot do KORZENIA Biblioteki, nie w "folder najwyzszego poziomu".

Zgloszony defekt
----------------
Korzen Biblioteki AMC nie jest sciezka na dysku -- to lista ``folder_sources``
plus sieroty. ``breadcrumb_rows`` oznacza go wierszem ``path=None``, ale
``Navigator.go_to_parent`` odrzucal kazda wartosc falsy i mowil
"To jest folder najwyzszego poziomu", zamiast wrocic.

Ten plik mierzy CALA sekwencje na prawdziwej kopii profilu:
korzen -> podfolder -> wyzej -> korzen, z zachowaniem wyboru opuszczonego
folderu. Dodatkowo pilnuje, ze zwykle przegladanie DYSKU (Ctrl+O, gdzie
korzen to ``C:\\``) nie zaczelo nagle "wracac do Biblioteki".
"""

from __future__ import annotations

import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_source import LibrarySource  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import (  # noqa: E402
    Announce,
    Navigator,
    OpenFolder,
    SessionId,
)
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


class RootRowIsAValidTarget(unittest.TestCase):
    """Jednostkowo, bez bazy: ``path=None`` to korzen, nie blad."""

    def _nav_in_library_folder(self) -> Navigator:
        nav = Navigator()
        rows = [
            Row(item_id="parent:", title="..", kind="parent", path=None),
            Row(item_id="file:1", title="Utwor", kind="track", path="D:\\a\\1.mp3"),
        ]
        nav.apply_folder("D:\\a", rows)
        return nav

    def test_parent_row_without_path_returns_to_library_root(self):
        nav = self._nav_in_library_folder()
        tasks = nav.go_to_parent()
        opens = [t for t in tasks if isinstance(t, OpenFolder)]
        self.assertTrue(opens, f"Powrot do korzenia nie zostal zlecony: {tasks}")
        self.assertIsNone(opens[0].path, "Korzen Biblioteki ma path=None")
        self.assertFalse(
            any(isinstance(t, Announce) and "najwyzszego poziomu" in t.text for t in tasks)
        )

    def test_returning_keeps_the_folder_we_left_selected(self):
        nav = self._nav_in_library_folder()
        opens = [t for t in nav.go_to_parent() if isinstance(t, OpenFolder)]
        self.assertEqual(opens[0].preferred_id, "dir:D:\\a")

    def test_plain_disk_browsing_still_reports_top_level(self):
        """Ctrl+O na ``C:\\``: brak wiersza rodzica = naprawde szczyt dysku."""
        nav = Navigator()
        nav.apply_folder("C:\\", [Row(item_id="file:x", title="x", kind="track", path="C:\\x.mp3")])
        tasks = nav.go_to_parent()
        self.assertTrue(
            any(isinstance(t, Announce) and "najwyzszego poziomu" in t.text for t in tasks),
            tasks,
        )
        self.assertFalse([t for t in tasks if isinstance(t, OpenFolder)])


@requires_fixture
class FullRoundTripOverTheRealProfile(unittest.TestCase):
    """Korzen -> podfolder -> wyzej -> korzen na PELNEJ kopii Biblioteki."""

    def setUp(self) -> None:
        self.library = LibrarySource(private_sandbox(FIXTURE))
        self.nav = Navigator()

    def _open(self, folder: str | None, preferred_id: str | None = None):
        """Odwzorowanie ``LiteFrame._open_library`` bez wx."""
        snapshot = self.library.load(folder)
        self.nav.apply_folder(snapshot.folder_path or "", snapshot.rows, preferred_id=preferred_id)
        return snapshot

    def test_root_then_child_then_back_to_root_with_selection(self):
        root = self._open(None)
        folders = [r for r in root.rows if r.kind == "folder"]
        self.assertEqual(len(folders), 3, "Korzen ma trzy zrodla folderowe")
        target = folders[0]

        self.nav.session.model.select_id(target.item_id)
        enter = self.nav.activate_selected()
        opens = [t for t in enter if isinstance(t, OpenFolder)]
        self.assertEqual(opens[0].path, target.path)

        child = self._open(opens[0].path)
        self.assertEqual(child.rows[0].kind, "parent")
        self.assertIsNone(child.rows[0].path, "Rodzic zrodla to korzen Biblioteki")

        back = self.nav.go_to_parent()
        back_opens = [t for t in back if isinstance(t, OpenFolder)]
        self.assertTrue(back_opens, f"Nie wrocilismy z podfolderu: {back}")
        self.assertIsNone(back_opens[0].path)

        again = self._open(back_opens[0].path, preferred_id=back_opens[0].preferred_id)
        self.assertIsNone(again.folder_path, "Jestesmy znowu w korzeniu")
        self.assertEqual(
            self.nav.session.model.selected_id,
            target.item_id,
            "Po powrocie fokus stoi na folderze, z ktorego wyszlismy",
        )
        self.assertEqual([r.item_id for r in again.rows], [r.item_id for r in root.rows])

    def test_three_levels_deep_and_back_step_by_step(self):
        root = self._open(None)
        source = next(r for r in root.rows if r.kind == "folder")
        level1 = self._open(source.path)
        deeper = next((r for r in level1.rows if r.kind == "folder"), None)
        if deeper is None:
            self.skipTest("To zrodlo nie ma podfolderow w kopii profilu")

        self.nav.session.model.select_id(deeper.item_id)
        self.nav.activate_selected()
        self._open(deeper.path)

        step_up = [t for t in self.nav.go_to_parent() if isinstance(t, OpenFolder)]
        self.assertEqual(step_up[0].path, source.path)
        self._open(step_up[0].path, preferred_id=step_up[0].preferred_id)
        self.assertEqual(self.nav.session.model.selected_id, deeper.item_id)

        to_root = [t for t in self.nav.go_to_parent() if isinstance(t, OpenFolder)]
        self.assertIsNone(to_root[0].path)
        self._open(to_root[0].path, preferred_id=to_root[0].preferred_id)
        self.assertEqual(self.nav.session.model.selected_id, source.item_id)

    def test_enter_on_the_parent_row_does_the_same_as_backspace(self):
        root = self._open(None)
        source = next(r for r in root.rows if r.kind == "folder")
        self.nav.session.model.select_id(source.item_id)
        self.nav.activate_selected()
        self._open(source.path)

        self.nav.session.model.select_id("parent:")
        tasks = self.nav.activate_selected()
        opens = [t for t in tasks if isinstance(t, OpenFolder)]
        self.assertTrue(opens, f"Enter na '..' nie wrocil do korzenia: {tasks}")
        self.assertIsNone(opens[0].path)

    def test_the_gui_path_opens_the_root_instead_of_falling_back_to_disk(self):
        """``_open_library(None)`` musi wczytac korzen, nie uznac go za brak."""
        from types import SimpleNamespace

        sys.path.insert(0, str(Path(__file__).resolve().parent))
        from test_gui_logic import install_wx_stub

        install_wx_stub()
        import amc_wx_lite.gui as gui

        queued: list = []
        said: list = []
        applied: list = []
        frame = SimpleNamespace(
            library=self.library,
            announcer=SimpleNamespace(say=said.append),
            runner=SimpleNamespace(
                submit=lambda tag, work, done, failed: queued.append((tag, work, done, failed))
            ),
            navigator=SimpleNamespace(
                apply_folder=lambda path, rows, preferred_id=None: applied.append(
                    (path, rows, preferred_id)
                )
                or [],
            ),
            _run=lambda intents: None,
        )
        bound = gui.LiteFrame._open_library.__get__(frame, type(frame))
        bound(None, preferred_id="dir:D:\\cokolwiek")
        self.assertEqual(len(queued), 1)
        _tag, work, done, _failed = queued[0]
        snapshot = work()
        self.assertFalse(snapshot.is_empty, "Korzen Biblioteki NIE jest pusty")
        done(snapshot)
        self.assertEqual(len(applied), 1, f"apply_folder nie zostal wywolany: {said}")
        self.assertEqual(applied[0][2], "dir:D:\\cokolwiek", "preferred_id musi dojsc do modelu")
        self.assertFalse(
            any("pusta" in text for text in said), f"Falszywy komunikat o pustce: {said}"
        )

    def test_saved_folder_none_means_root_not_missing_library(self):
        self.assertIsNone(self.library.saved_folder())
        snapshot = self.library.load(self.library.saved_folder())
        self.assertFalse(snapshot.is_empty)
        self.assertIsNone(snapshot.folder_path)


if __name__ == "__main__":
    unittest.main(verbosity=2)
