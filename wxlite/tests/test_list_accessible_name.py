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

from amc_wx_lite.gui import (
    MediaListCtrl,
    PresetAssignmentDialog,
    PresetListDialog,
    RelationDestinationDialog,
    RelationDestinationWindow,
)
from amc_wx_lite.radio_schedule_dialogs import RadioSchedulesDialog


def test_native_list_keeps_its_msaa_children() -> None:
    source = inspect.getsource(MediaListCtrl)

    assert "SetAccessible" not in source
    assert "MediaListAccessible" not in source
    assert "NotifyWinEvent" not in source
    assert "_announce_empty_list" not in source


def test_native_list_still_has_an_intentional_user_label() -> None:
    source = inspect.getsource(MediaListCtrl.__init__)

    assert "localized_label = i18n.ui_text(label)" in source
    assert "self.SetLabel(localized_label)" in source
    assert "self.SetName(localized_label)" in source
    assert "self._base_accessible_label = label" in source
    assert "self._accessible_label_source = label" in source


def test_native_list_relocalizes_its_accessible_name_and_columns() -> None:
    label_source = inspect.getsource(MediaListCtrl.set_accessible_label)
    chrome_source = inspect.getsource(MediaListCtrl.relocalize_interface)

    assert "localized_label = i18n.ui_text(source_label)" in label_source
    assert "self.SetName(localized_label)" in label_source
    assert "self._accessible_label_source = source_label" in label_source
    assert "column.SetText(i18n.ui_text(source_label))" in chrome_source
    assert "self.set_accessible_label(self._accessible_label_source)" in chrome_source


def test_leaving_a_named_view_restores_the_session_list_label() -> None:
    import amc_wx_lite.gui as gui

    source = inspect.getsource(gui.LiteFrame._sync_views)
    assert 'LibraryView.PLAYLIST_CONTENTS: "Playlista"' in source
    assert 'getattr(active_list, "_base_accessible_label", None)' in source
    assert "self.session_label.SetLabel(i18n.ui_text({" in source


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


def test_relation_destinations_are_a_named_native_list_not_a_context_menu() -> None:
    source = inspect.getsource(RelationDestinationDialog)

    assert "wx.ListBox" in source
    assert 'SetName("Powiązania")' in source
    assert "SetSelection(0)" in source
    assert "SetAccessible" not in source
    assert "PopupMenu" not in source

    quick_source = inspect.getsource(RelationDestinationWindow)
    assert "wx.Frame" in quick_source
    assert "wx.ListBox" in quick_source
    assert "self.destinations.SetName(title)" in quick_source
    assert "wx.Dialog" not in quick_source
    assert "PopupMenu" not in quick_source


def test_sonos_fixed_target_is_a_native_named_checkbox() -> None:
    source = inspect.getsource(PresetAssignmentDialog)

    assert "wx.CheckBox" in source
    assert '"Zawsze na tym zestawie głośników: "' in source
    assert "self.fixed_target_checkbox.SetName(fixed_label)" in source
    assert "service_members" not in source


def test_nvda_addon_does_not_install_list_object_overlays() -> None:
    plugin = (
        Path(__file__).resolve().parents[2]
        / "nvda-addon/addon/globalPlugins/amcController/__init__.py"
    ).read_text(encoding="utf-8")

    assert "radioList" not in plugin
    assert "chooseNVDAObjectOverlayClasses" not in plugin
