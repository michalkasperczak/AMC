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

from amc_wx_lite.gui import MediaListCtrl


def test_native_list_keeps_its_msaa_children() -> None:
    source = inspect.getsource(MediaListCtrl.__init__)

    assert "SetAccessible" not in source
    assert "MediaListAccessible" not in source


def test_native_list_still_has_an_intentional_user_label() -> None:
    source = inspect.getsource(MediaListCtrl.__init__)

    assert "self.SetLabel(label)" in source
    assert "self.SetName(label)" in source
