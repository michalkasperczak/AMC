"""Ctrl+X: WYCIECIE PLIKU do przeniesienia poza AMC.

ZGLOSZENIE. W dostarczonym porcie ``Ctrl+Shift+C`` nie wkleja pliku, a
``Ctrl+X`` nie istnieje wcale. W DZIALAJACYM ORYGINALE oba dzialaja i Michal
z nich korzysta.

KONTRAKT ODCZYTANY Z C#, nie z pamieci -- ``MainWindow.xaml.cs``:

``CutLocalFilesForExternalMove`` (:25277-25313)
    * ``items.Count == 0`` -> "Brak pliku do wyciecia";
    * widok ZAKLADEK -> "Wycinanie plikow nie dziala na liscie zakladek";
    * kazdy element musi miec ``TryGetLocalPath`` ORAZ ``File.Exists``, inaczej
      "Wycinanie jest dostepne tylko dla istniejacych plikow lokalnych"
      (uwaga: tu jest ``File.Exists``, a NIE ``Directory.Exists`` -- folder
      sie nie kwalifikuje, inaczej niz przy kopiowaniu);
    * schowek dostaje TRZY rzeczy: ``UnicodeText`` ze sciezkami,
      ``SetFileDropList`` (``CF_HDROP``) i
      ``SetData("Preferred DropEffect", BitConverter.GetBytes(2))``;
      ``2 == DROPEFFECT_MOVE`` (``1`` to COPY);
    * sukces: "Plik gotowy do przeniesienia. Wklej go w folderze docelowym";
    * ``_pendingExternalMoves`` zapamietuje sciezki -- czyli AMC SAM NIC NIE
      USUWA. Usuniecie wykonuje Explorer przy wklejeniu, a
      ``ReconcileCompletedExternalMoves`` tylko to ZAUWAZA.

Strona kopiowania (:24778-24787) ustawia te same dwa formaty, ale ZADNEGO
``Preferred DropEffect`` -- brak tego formatu to dla shella COPY. Dlatego
copy zachowuje zrodlo, a cut je oddaje.
"""

from __future__ import annotations

import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

from amc_wx_lite import gui  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import LibraryView  # noqa: E402

DROPEFFECT_COPY = 1
DROPEFFECT_MOVE = 2


class FakeClipboard:
    """Atrapa schowka. Zapisuje KAZDY format osobno, zeby dalo sie zmierzyc
    obecnosc albo BRAK ``Preferred DropEffect`` -- w tym tkwi roznica."""

    def __init__(self, *, set_data_result: bool = True) -> None:
        self.data = None
        self.flushed = False
        self._set_data_result = set_data_result

    def Open(self) -> bool:
        return True

    def Close(self) -> None:
        pass

    def SetData(self, data) -> bool:
        self.data = data
        return self._set_data_result

    def Flush(self) -> bool:
        self.flushed = True
        return True


def make_frame(row, *, view=None, clipboard=None):
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    spoken: list[str] = []
    frame.announcer = type("A", (), {"say": staticmethod(spoken.append)})()

    class Model:
        selected_row = row

    class Session:
        model = Model()
        library_view = view

    class Nav:
        session = Session()

    frame.navigator = Nav()
    frame._spoken = spoken
    frame.pending_external_moves = {}
    return frame, spoken


class CutContract(unittest.TestCase):
    def setUp(self) -> None:
        self.folder = tempfile.mkdtemp(prefix="amc-cut-")
        self.path = os.path.join(self.folder, "utwor.mp3")
        Path(self.path).write_bytes(b"ID3test")
        self.row = Row(item_id="i1", title="utwor.mp3", kind="track", path=self.path)

    # ----------------------------------------------------- kontrakt formatow
    def test_cut_ustawia_preferred_drop_effect_MOVE(self) -> None:
        """``Preferred DropEffect`` == 2 (MOVE). Bez tego shell robi KOPIE."""
        payload = gui.file_cut_clipboard_payload(self.path)
        self.assertEqual(payload["preferred_drop_effect"], DROPEFFECT_MOVE)
        self.assertEqual(payload["file_path"], self.path)
        self.assertEqual(payload["text"], self.path)

    def test_copy_NIE_ustawia_preferred_drop_effect(self) -> None:
        """Strona kopiowania w C# nie wola ``SetData("Preferred DropEffect")``.

        Brak tego formatu to dla shella COPY -- i wlasnie dlatego copy musi
        ZACHOWAC zrodlo. Gdyby copy ustawialo MOVE, Explorer usuwalby plik.
        """
        payload = gui.file_copy_clipboard_payload(self.path)
        self.assertIsNone(payload["preferred_drop_effect"])

    # ----------------------------------------------------- warunki z C#
    def test_cut_odmawia_gdy_nie_ma_zaznaczenia(self) -> None:
        frame, spoken = make_frame(None)
        frame._cut_file()
        self.assertEqual(spoken, ["Brak pliku do wycięcia"])

    def test_cut_odmawia_na_liscie_zakladek(self) -> None:
        """``CutLocalFilesForExternalMove`` :25280-25284."""
        frame, spoken = make_frame(self.row, view=LibraryView.ALL_BOOKMARKS)
        frame._cut_file()
        self.assertEqual(spoken, ["Wycinanie plików nie działa na liście zakładek"])

    def test_cut_odmawia_dla_nieistniejacego_pliku(self) -> None:
        row = Row(item_id="i2", title="x.mp3", kind="track",
                  path=os.path.join(self.folder, "nie-ma.mp3"))
        frame, spoken = make_frame(row)
        frame._cut_file()
        self.assertEqual(
            spoken, ["Wycinanie jest dostępne tylko dla istniejących plików lokalnych"])

    def test_cut_odmawia_dla_stacji_radiowej(self) -> None:
        row = Row(item_id="s1", title="Radio", kind="station",
                  path="http://stream.example/live")
        frame, spoken = make_frame(row)
        frame._cut_file()
        self.assertEqual(
            spoken, ["Wycinanie jest dostępne tylko dla istniejących plików lokalnych"])

    def test_cut_odmawia_dla_FOLDERU_inaczej_niz_copy(self) -> None:
        """C# ma tu ``File.Exists``, bez ``Directory.Exists`` (:25289).

        Kopiowanie bierze wariant szerszy (plik LUB folder), wycinanie nie.
        Rozdzial jest celowy i mierzymy go, zeby nikt go nie "ujednolicil".
        """
        row = Row(item_id="d1", title="folder", kind="folder", path=self.folder)
        frame, spoken = make_frame(row)
        frame._cut_file()
        self.assertEqual(
            spoken, ["Wycinanie jest dostępne tylko dla istniejących plików lokalnych"])
        # Kontrdowod, ze folder NADAL kwalifikuje sie do KOPIOWANIA:
        self.assertEqual(gui.LiteFrame._file_drop_path(self.folder), self.folder)

    # ----------------------------------------------------- komunikat i zrodlo
    def test_cut_mowi_gotowy_do_przeniesienia_i_NIE_usuwa_zrodla(self) -> None:
        frame, spoken = make_frame(self.row)
        captured = {}

        def to_clipboard(text, *, file_path=None, preferred_drop_effect=None):
            captured.update(text=text, file_path=file_path,
                            preferred_drop_effect=preferred_drop_effect)
            return True

        frame._to_clipboard = to_clipboard
        frame._cut_file()
        self.assertEqual(
            spoken, ["Plik gotowy do przeniesienia. Wklej go w folderze docelowym"])
        self.assertEqual(captured["preferred_drop_effect"], DROPEFFECT_MOVE)
        # AMC SAM nic nie usuwa -- usuwa Explorer przy wklejeniu.
        self.assertTrue(os.path.isfile(self.path))

    def test_cut_przy_porazce_schowka_NIE_mowi_o_gotowosci(self) -> None:
        frame, spoken = make_frame(self.row)
        frame._to_clipboard = lambda *a, **k: False
        frame._cut_file()
        self.assertNotIn(
            "Plik gotowy do przeniesienia. Wklej go w folderze docelowym", spoken)
        self.assertTrue(os.path.isfile(self.path))

    def test_cut_zapamietuje_oczekujace_przeniesienie(self) -> None:
        """``_pendingExternalMoves[entry.Item.Id] = entry.Path`` (:25307)."""
        frame, _ = make_frame(self.row)
        frame._to_clipboard = lambda *a, **k: True
        frame._cut_file()
        self.assertEqual(frame.pending_external_moves, {"i1": self.path})

    def test_skrot_ctrl_x_jest_w_tablicy_listy(self) -> None:
        from amc_wx_lite import shortcuts

        self.assertIs(shortcuts.LIST_VIEW["Ctrl+X"], shortcuts.Action.CUT_FILE)


if __name__ == "__main__":
    unittest.main()
