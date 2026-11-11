"""Native Radio Browser list stays open while another station starts."""

from __future__ import annotations

import unittest

try:
    import wx
except ImportError:
    wx = None


def test_station_enter_keeps_dialog_and_reopen_resumes_country() -> None:
    if wx is None or not getattr(wx, "version", None):
        raise unittest.SkipTest("test wymaga prawdziwego wxPython")

    from amc_wx_lite.radio_browser_dialog import RadioBrowserDialog
    from amc_wx_lite.search_catalog import SearchResultAction

    class DialogSpy(RadioBrowserDialog):
        def EndModal(self, code: int) -> None:  # noqa: N802 - wx API
            self.closed.append(code)

    app = wx.GetApp() or wx.App(False)
    frame = wx.Frame(None)
    calls = []
    requests = []
    countries = {"items": [{"code": "PL", "name": "Polska", "stationCount": 2}]}
    stations = {"items": [{
        "id": "radio:one", "title": "Pierwsza stacja", "kind": "station",
        "remoteKind": "radioStation", "address": "https://example.test/one",
        "detail": "Polska, AAC, 128 kb/s",
    }, {
        "id": "radio:two", "title": "Druga stacja", "kind": "station",
        "remoteKind": "radioStation", "address": "https://example.test/two",
        "detail": "Polska, AAC, 192 kb/s",
    }], "nextOffset": None}

    def load_stations(code, order, offset, dialog):
        requests.append((code, order, offset))
        dialog.show_stations(stations, offset)

    def make_dialog(resume=None):
        dialog = DialogSpy(
            frame, load_countries=lambda target: target.show_countries(countries),
            load_stations=load_stations,
            station_action=lambda entries, action, say: calls.append(
                (entries[0].row.title, action)
            ),
            copy_text=lambda _text: True, resume=resume,
        )
        dialog.closed = []
        return dialog

    first = make_dialog()
    try:
        first.start()
        assert first.items.GetStringSelection() == "Europa"
        first._activate()
        assert first.items.GetStringSelection().startswith("Polska")
        first._activate()
        assert "kolejność" in first.heading.GetLabel()
        first._activate()
        assert requests == [("PL", "name", 0)]

        first.items.SetSelection(1)
        first._activate()
        assert calls == [("Druga stacja", SearchResultAction.OPEN)]
        assert first.closed == []
        assert first.items.GetSelection() == 1
        location = first.location
        assert location == ("Europa", "PL", "name")
    finally:
        first.Destroy()

    reopened = make_dialog(resume=location)
    try:
        reopened.start()
        assert requests == [("PL", "name", 0), ("PL", "name", 0)]
        assert reopened.heading.GetLabel() == "Stacje: Polska"
        assert reopened.items.GetCount() == 2
    finally:
        reopened.Destroy()
        frame.Destroy()
        app.Yield()
