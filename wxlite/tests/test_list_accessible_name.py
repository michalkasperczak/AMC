"""The native Windows list owns accessible rows read by NVDA.

Regression reported by the user: arrow keys still moved and Enter activated a
station, but NVDA said no row at all. NVDA+Up exposed only the control label
(``Pliki lokalne``). The custom ``wx.Accessible`` attached to the ListCtrl
therefore replaced the useful native SysListView32 child tree even though its
child methods returned ``ACC_NOT_IMPLEMENTED``.

The fix deliberately keeps an explicit label but does not install a custom
accessible provider. Live NVDA remains the acceptance test; these guards make
sure the provider-breaking line cannot return unnoticed.
"""
from __future__ import annotations

import inspect
from pathlib import Path

from amc_wx_lite.gui import MediaListCtrl, PresetAssignmentDialog, PresetListDialog
from amc_wx_lite.radio_schedule_dialogs import RadioSchedulesDialog


def test_native_list_keeps_its_msaa_children() -> None:
    source = inspect.getsource(MediaListCtrl)

    assert "SetAccessible" not in source
    assert "MediaListAccessible" not in source
    assert "NotifyWinEvent" not in source
    assert "_announce_empty_list" not in source


def test_native_list_still_has_an_intentional_user_label() -> None:
    source = inspect.getsource(MediaListCtrl.__init__)

    assert "self.SetLabel(label)" in source
    assert "self.SetName(label)" in source


def test_gui_does_not_mark_lists_for_an_nvda_overlay() -> None:
    import amc_wx_lite.gui as gui

    source = inspect.getsource(gui)
    assert "radio_position" not in source
    assert "_radio_markers" not in source


def test_schedule_and_preset_lists_use_only_native_wx_controls() -> None:
    for control in (RadioSchedulesDialog, PresetListDialog, PresetAssignmentDialog):
        source = inspect.getsource(control)
        assert "SetAccessible" not in source
        assert "NotifyWinEvent" not in source
        assert "chooseNVDAObjectOverlayClasses" not in source

    assert "wx.ListBox" in inspect.getsource(RadioSchedulesDialog)
    assert "wx.ListBox" in inspect.getsource(PresetListDialog)
    assert "wx.ListBox" in inspect.getsource(PresetAssignmentDialog)


def test_nvda_addon_does_not_install_list_object_overlays() -> None:
    plugin = (
        Path(__file__).resolve().parents[2]
        / "nvda-addon/addon/globalPlugins/amcController/__init__.py"
    ).read_text(encoding="utf-8")

    assert "radioList" not in plugin
    assert "chooseNVDAObjectOverlayClasses" not in plugin
