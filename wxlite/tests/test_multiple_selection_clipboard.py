"""Wielokrotne zaznaczenie Shift/Ctrl zachowuje caly wybor w schowku.

Regula pochodzi z pelnego AMC: ``ActionItems`` czyta ``SelectedItems`` w
kolejnosci listy, Ctrl+C laczy nazwy po jednej w wierszu, Ctrl+Shift+C ustawia
wszystkie sciezki w CF_HDROP, a Ctrl+X robi to samo z DROPEFFECT_MOVE.
"""

from __future__ import annotations

import os
import tempfile
from pathlib import Path
from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, list_sync  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402


def row(item_id: str, title: str, path: str) -> Row:
    return Row(item_id=item_id, title=title, kind="track", path=path)


class SelectedList:
    """Natywna kolejnosc wyboru, niezalezna od pojedynczego kursora modelu."""

    def __init__(self, model: ListModel, selected: list[int]) -> None:
        self.model = model
        self._selected = selected
        self._shown = list_sync.model_row_texts(model)

    def GetFirstSelected(self) -> int:  # noqa: N802
        return self._selected[0] if self._selected else -1

    def GetNextSelected(self, index: int) -> int:  # noqa: N802
        later = [candidate for candidate in self._selected if candidate > index]
        return later[0] if later else -1

    shown_item_id = gui.MediaListCtrl.shown_item_id
    selected_rows = gui.MediaListCtrl.selected_rows


def make_frame(rows: list[Row], selected: list[int]):
    model = ListModel()
    model.replace(rows)
    # Kursor jest na ostatnim elemencie zakresu, jak po Shift+strzalka.
    model.select_id(rows[selected[-1]].item_id)
    control = SelectedList(model, selected)
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    spoken: list[str] = []
    frame.navigator = SimpleNamespace(
        session=SimpleNamespace(model=model, library_view=None)
    )
    frame.announcer = SimpleNamespace(say=spoken.append)
    frame.pending_external_moves = {}
    frame._active_list = lambda: control
    return frame, spoken


def test_ctrl_c_copies_every_selected_name_one_per_line() -> None:
    rows = [
        row("a", "Alfa.mp3", "C:\\m\\Alfa.mp3"),
        row("b", "Beta.mp3", "C:\\m\\Beta.mp3"),
        row("c", "Gamma.mp3", "C:\\m\\Gamma.mp3"),
    ]
    frame, spoken = make_frame(rows, [0, 2])
    captured: dict[str, str] = {}

    def copy(text: str, **_kwargs) -> bool:
        captured["text"] = text
        return True

    frame._to_clipboard = copy
    frame._copy_name()

    assert captured["text"].splitlines() == ["Alfa.mp3", "Gamma.mp3"]
    assert spoken == ["Skopiowano nazwy: 2 elementy"]


def test_selected_rows_use_visible_filtered_order_not_model_indexes() -> None:
    model = ListModel()
    rows = [
        row("a", "Alfa.mp3", "C:\\m\\Alfa.mp3"),
        row("b", "Beta.mp3", "C:\\m\\Beta.mp3"),
        row("c", "Gamma.mp3", "C:\\m\\Gamma.mp3"),
    ]
    model.replace(rows)
    control = SelectedList(model, [0, 1])
    visible = ListModel()
    visible.replace(rows[1:])
    # Filtr pokazuje modelowe wiersze 1 i 2 pod widocznymi indeksami 0 i 1.
    control._shown = list_sync.model_row_texts(visible)

    assert [item.item_id for item in control.selected_rows()] == ["b", "c"]


def test_periodic_cursor_sync_does_not_break_a_shift_selection() -> None:
    model = ListModel()
    rows = [
        row("a", "Alfa.mp3", "C:\\m\\Alfa.mp3"),
        row("b", "Beta.mp3", "C:\\m\\Beta.mp3"),
        row("c", "Gamma.mp3", "C:\\m\\Gamma.mp3"),
    ]
    model.replace(rows)
    model.select_id("c")

    class CursorList:
        filter_query = ""

        def GetItemCount(self) -> int:  # noqa: N802
            return len(model.rows)

        def GetFirstSelected(self) -> int:  # noqa: N802
            return 0

        def GetFocusedItem(self) -> int:  # noqa: N802
            return 2

        def GetItemState(self, index: int, _mask: int) -> int:  # noqa: N802
            import wx

            return wx.LIST_STATE_SELECTED if index in (0, 1, 2) else 0

    control = CursorList()
    control.model = model
    control._wanted_visible_index = gui.MediaListCtrl._wanted_visible_index.__get__(
        control, CursorList
    )
    control._is_index_selected = gui.MediaListCtrl._is_index_selected.__get__(
        control, CursorList
    )

    assert gui.MediaListCtrl._cursor_target(control) is None


def test_ctrl_arrow_focus_is_kept_until_ctrl_space_toggles_it() -> None:
    model = ListModel()
    rows = [
        row("a", "Alfa.mp3", "C:\\m\\Alfa.mp3"),
        row("b", "Beta.mp3", "C:\\m\\Beta.mp3"),
        row("c", "Gamma.mp3", "C:\\m\\Gamma.mp3"),
    ]
    model.replace(rows)
    model.select_id("a")
    control = gui.MediaListCtrl.__new__(gui.MediaListCtrl)
    control.model = model
    control._shown = list_sync.model_row_texts(model)
    control.filter_query = ""
    control.updating = False
    control.GetFocusedItem = lambda: 2
    control.GetFirstSelected = lambda: 0
    control.GetItemCount = lambda: len(model.rows)
    control.GetItemState = lambda index, _mask: 4 if index == 0 else 0

    class Event:
        def GetEventObject(self):  # noqa: N802
            return control

        def GetIndex(self) -> int:  # noqa: N802
            return 2

        def Skip(self) -> None:  # noqa: N802
            pass

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = SimpleNamespace(session=SimpleNamespace(model=model))
    frame._refresh_menu_state = lambda: None

    frame._on_item_focused(Event())

    assert model.selected_id == "c"
    # Wiersz C ma tylko fokus, a A pozostaje zaznaczony. Najblizszy tick nie
    # moze zaznaczyc C za uzytkownika przed nacisnieciem Ctrl+Spacji.
    assert control._cursor_target() is None


def test_deselecting_focused_row_keeps_it_as_ctrl_space_anchor() -> None:
    model = ListModel()
    rows = [
        row("a", "Alfa.mp3", "C:\\m\\Alfa.mp3"),
        row("b", "Beta.mp3", "C:\\m\\Beta.mp3"),
        row("c", "Gamma.mp3", "C:\\m\\Gamma.mp3"),
    ]
    model.replace(rows)
    model.select_id("c")
    control = gui.MediaListCtrl.__new__(gui.MediaListCtrl)
    control.model = model
    control._shown = list_sync.model_row_texts(model)
    control.updating = False
    control.GetFocusedItem = lambda: 2
    control.GetFirstSelected = lambda: 0
    control.GetItemCount = lambda: len(model.rows)
    control.GetItemState = lambda index, _mask: 4 if index == 0 else 0

    class Event:
        def GetEventObject(self):  # noqa: N802
            return control

        def GetIndex(self) -> int:  # noqa: N802
            return 2

        def Skip(self) -> None:  # noqa: N802
            pass

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = SimpleNamespace(session=SimpleNamespace(model=model))
    frame._refresh_menu_state = lambda: None

    frame._on_item_deselected(Event())

    assert model.selected_id == "c"


def test_deselecting_nonfocused_anchor_moves_to_selected_focus() -> None:
    model = ListModel()
    rows = [
        row("a", "Alfa.mp3", "C:\\m\\Alfa.mp3"),
        row("b", "Beta.mp3", "C:\\m\\Beta.mp3"),
        row("c", "Gamma.mp3", "C:\\m\\Gamma.mp3"),
    ]
    model.replace(rows)
    model.select_id("c")
    control = gui.MediaListCtrl.__new__(gui.MediaListCtrl)
    control.model = model
    control._shown = list_sync.model_row_texts(model)
    control.updating = False
    control.GetFocusedItem = lambda: 0
    control.GetFirstSelected = lambda: 0
    control.GetItemCount = lambda: len(model.rows)
    control.GetItemState = lambda index, _mask: 4 if index == 0 else 0

    class Event:
        def GetEventObject(self):  # noqa: N802
            return control

        def GetIndex(self) -> int:  # noqa: N802
            return 2

        def Skip(self) -> None:  # noqa: N802
            pass

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = SimpleNamespace(session=SimpleNamespace(model=model))
    frame._refresh_menu_state = lambda: None

    frame._on_item_deselected(Event())

    assert model.selected_id == "a"


def test_native_list_focus_event_is_bound_for_ctrl_arrow_navigation() -> None:
    import wx

    bindings: list[object] = []
    control = SimpleNamespace(
        Bind=lambda event_type, _handler: bindings.append(event_type)
    )
    frame = gui.LiteFrame.__new__(gui.LiteFrame)

    frame._bind_list(control)

    assert wx.EVT_LIST_ITEM_FOCUSED in bindings


def test_ctrl_shift_c_copies_all_selected_real_files() -> None:
    with tempfile.TemporaryDirectory() as folder:
        paths = [os.path.join(folder, name) for name in ("a.mp3", "b.mp3")]
        for path in paths:
            Path(path).write_bytes(b"x")
        rows = [row("a", "a.mp3", paths[0]), row("b", "b.mp3", paths[1])]
        frame, spoken = make_frame(rows, [0, 1])
        captured = {}

        def copy(text, **kwargs):
            captured.update(text=text, **kwargs)
            return True

        frame._to_clipboard = copy
        frame._copy_address()

        assert captured["text"].splitlines() == paths
        assert captured["file_paths"] == paths
        assert spoken == ["Skopiowano pliki i pełne ścieżki: 2 pliki"]


def test_real_clipboard_builder_adds_every_path_to_file_drop() -> None:
    from test_ctrl_shift_c_copies_a_real_file import (
        FakeClipboard,
        FakeFileData,
        make_frame as make_clipboard_frame,
    )

    clipboard = FakeClipboard()
    frame, _spoken = make_clipboard_frame(clipboard)
    paths = ["C:\\m\\a.mp3", "C:\\m\\b.mp3"]

    assert frame._to_clipboard(os.linesep.join(paths), file_paths=paths) is True
    file_data = next(
        part for part, _preferred in clipboard.data.parts
        if isinstance(part, FakeFileData)
    )
    assert file_data.files == paths


def test_ctrl_x_cuts_all_selected_files_without_deleting_them() -> None:
    with tempfile.TemporaryDirectory() as folder:
        paths = [os.path.join(folder, name) for name in ("a.mp3", "b.mp3")]
        for path in paths:
            Path(path).write_bytes(b"x")
        rows = [row("a", "a.mp3", paths[0]), row("b", "b.mp3", paths[1])]
        frame, spoken = make_frame(rows, [0, 1])
        captured = {}

        def copy(text, **kwargs):
            captured.update(text=text, **kwargs)
            return True

        frame._to_clipboard = copy
        frame._cut_file()

        assert captured["text"].splitlines() == paths
        assert captured["file_paths"] == paths
        assert captured["preferred_drop_effect"] == gui.DROPEFFECT_MOVE
        assert frame.pending_external_moves == {"a": paths[0], "b": paths[1]}
        assert all(os.path.isfile(path) for path in paths)
        assert spoken == [
            "Pliki gotowe do przeniesienia: 2 pliki. Wklej je w folderze docelowym"
        ]


def test_list_style_does_not_force_single_selection() -> None:
    source = Path(gui.__file__).read_text(encoding="utf-8")
    constructor = source[
        source.index("class MediaListCtrl"):
        source.index("# ------------------------------------------------------------ aktualizacja")
    ]
    assert "style=wx.LC_REPORT | wx.LC_SINGLE_SEL" not in constructor
    assert "style=wx.LC_REPORT | wx.BORDER_SUNKEN" in constructor
