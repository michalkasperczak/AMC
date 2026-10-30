"""Duza lista Plikow: licznik wlascicielski, punktowe odswiezanie i wybor."""

from __future__ import annotations

from amc_wx_lite.list_model import ListModel, Row


def track(number: int, title: str | None = None) -> Row:
    name = title or f"Utwor {number}"
    return Row(item_id=str(number), title=name, kind="track", path=f"C:\\m\\{number}.mp3")


class VirtualList:
    def __init__(self, model: ListModel) -> None:
        from amc_wx_lite.gui import _BRAK_KONTEKSTU

        self.model = model
        self.state = None
        self.filter_query = ""
        self._virtual = True
        self._shown = []
        self._shown_context = _BRAK_KONTEKSTU
        self._shown_rows_source = model.rows
        self._synced_rows = None
        self._synced_query = None
        self._row_text_cache = {}
        self.updating = False
        self.count = 0
        self.selected = -1
        self.focused = -1
        self.calls: list[tuple] = []

    def SetItemCount(self, count: int) -> None:  # noqa: N802
        self.calls.append(("SetItemCount", count))
        self.count = count
        self.selected = self.focused = -1

    def GetItemCount(self) -> int:  # noqa: N802
        return self.count

    def RefreshItem(self, index: int) -> None:  # noqa: N802
        assert 0 <= index < self.count
        self.calls.append(("RefreshItem", index))

    def GetFirstSelected(self) -> int:  # noqa: N802
        return self.selected

    def GetFocusedItem(self) -> int:  # noqa: N802
        return self.focused

    def GetItemState(self, index: int, _mask: int) -> int:  # noqa: N802
        import wx

        assert 0 <= index < self.count
        return wx.LIST_STATE_SELECTED if index == self.selected else 0

    def SetItemState(self, index: int, state: int, mask: int) -> None:  # noqa: N802
        import wx

        assert 0 <= index < self.count
        self.calls.append(("SetItemState", index, state, mask))
        if mask & wx.LIST_STATE_SELECTED:
            self.selected = index if state & wx.LIST_STATE_SELECTED else -1
        if mask & wx.LIST_STATE_FOCUSED:
            self.focused = index if state & wx.LIST_STATE_FOCUSED else -1

    def EnsureVisible(self, index: int) -> None:  # noqa: N802
        self.calls.append(("EnsureVisible", index))


def make_list(model: ListModel) -> VirtualList:
    from amc_wx_lite.gui import MediaListCtrl

    ctrl = VirtualList(model)
    for name in (
        "sync_rows", "_sync_virtual_rows", "OnGetItemText", "_view_context",
        "_cursor_target", "_wanted_visible_index", "_is_index_selected",
        "_move_cursor", "_move_cursor_quietly", "shown_item_id",
    ):
        setattr(ctrl, name, getattr(MediaListCtrl, name).__get__(ctrl, VirtualList))
    return ctrl


def test_42866_files_need_one_count_change_and_no_native_rows() -> None:
    model = ListModel()
    ctrl = make_list(model)
    model.replace([track(i) for i in range(42_866)], preferred_id="42865")
    ctrl.sync_rows()

    assert ctrl.count == 42_866
    assert [call for call in ctrl.calls if call[0] == "SetItemCount"] == [
        ("SetItemCount", 42_866),
    ]
    assert ctrl.OnGetItemText(42_865, 0) == "Utwor 42865"
    assert ctrl.OnGetItemText(42_865, 1) == model.text_for(42_865, 1)
    assert ctrl.selected == ctrl.focused == 42_865

    ctrl.calls.clear()
    ctrl.sync_rows()  # Tick statusu, Enter/Escape bez zmiany danych.
    assert ctrl.calls == []


def test_virtual_filter_selection_text_change_and_reopening() -> None:
    model = ListModel()
    ctrl = make_list(model)
    original = [track(i) for i in range(100)]
    model.replace(original, preferred_id="50")
    ctrl.sync_rows()
    ctrl.calls.clear()

    ctrl.filter_query = "Utwor 9"
    ctrl.sync_rows()
    assert ctrl.count == 11
    assert ctrl.OnGetItemText(0, 0) == "Utwor 9"
    assert model.selected_id == "9"
    assert ctrl.selected == ctrl.focused == 0
    assert [call for call in ctrl.calls if call[0] == "SetItemCount"] == [
        ("SetItemCount", 0), ("SetItemCount", 11),
    ]

    ctrl.calls.clear()
    ctrl.filter_query = ""
    ctrl.sync_rows()
    assert ctrl.count == 100
    assert ctrl.selected == ctrl.focused == 9
    assert ctrl.OnGetItemText(9, 0) == "Utwor 9"

    ctrl.calls.clear()
    changed = original.copy()
    changed[9] = track(9, "Nowy tytul")
    model.replace(changed)
    ctrl.sync_rows()
    assert [call for call in ctrl.calls if call[0] == "RefreshItem"] == [
        ("RefreshItem", 9),
    ]
    assert not [call for call in ctrl.calls if call[0] == "SetItemCount"]
    assert ctrl.OnGetItemText(9, 0) == "Nowy tytul"

    ctrl.calls.clear()
    ctrl.sync_rows()  # Ponowne wejscie bez zmiany danych.
    assert ctrl.calls == []


def test_virtual_view_replacement_does_not_reuse_old_cached_text() -> None:
    model = ListModel()
    ctrl = make_list(model)
    model.replace([track(i) for i in range(20)], preferred_id="5")
    ctrl.sync_rows()
    ctrl.calls.clear()

    model.replace([track(100 + i, f"Nowy {i}") for i in range(20)])
    ctrl.sync_rows()
    assert [call for call in ctrl.calls if call[0] == "SetItemCount"] == [
        ("SetItemCount", 0), ("SetItemCount", 20),
    ]
    assert ctrl.OnGetItemText(5, 0) == "Nowy 5"
    assert ctrl.OnGetItemText(20, 0) == ""
    assert ctrl.selected == ctrl.focused == 0
