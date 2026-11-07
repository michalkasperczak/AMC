"""Delete changes only selected entries of an open TIDAL playlist."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import Navigator, OpenTidalContainer, SessionId  # noqa: E402


class Runner:
    @staticmethod
    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)


def _frame(selected):
    nav = Navigator()
    nav.active = SessionId.TIDAL
    context = OpenTidalContainer("playlist:1", "playlists:1", "Moja lista", "playlist")
    one = Row("track:7:entry:a", "Pierwszy", "track", service_id="tracks:7")
    two = Row("track:7:entry:b", "Drugi wpis", "track", service_id="tracks:7")
    other = Row("track:8:entry:c", "Następny", "track", service_id="tracks:8")
    nav.apply_tidal_container(context, "Moja lista", [one, two, other])
    calls = []
    spoken = []
    opened = []
    frame = SimpleNamespace(
        navigator=nav,
        client=SimpleNamespace(tidal_playlist_remove_items=lambda playlist_id, ids: (
            calls.append((playlist_id, ids)) or {
                "removedCount": len(ids), "message": "Usunięto z playlisty TIDAL"
            }
        )),
        runner=Runner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: selected(one, two, other),
        _restore_focus_after_dialog=lambda: None,
        _open_tidal_container=lambda intent, after_open=None: (
            opened.append(intent), after_open() if after_open else None
        ),
    )
    frame._remove_tidal_playlist_items = lambda: gui.LiteFrame._remove_tidal_playlist_items(frame)
    return frame, calls, spoken, opened


def test_delete_removes_only_selected_entries_and_restores_next_row(monkeypatch):
    monkeypatch.setattr(gui.wx, "ICON_QUESTION", 0, raising=False)
    monkeypatch.setattr(gui.wx, "NO_DEFAULT", 0, raising=False)
    monkeypatch.setattr(gui.wx, "MessageBox", lambda *_args: gui.wx.YES, raising=False)
    frame, calls, spoken, opened = _frame(lambda one, two, _other: [one, two])
    gui.LiteFrame._remove_selected_items(frame)
    assert calls == [("playlists:1", ["track:7:entry:a", "track:7:entry:b"])]
    assert opened[0].preferred_id == "track:8:entry:c"
    assert spoken[-1] == "Usunięto z playlisty TIDAL"
    assert frame._tidal_playlist_removal_pending is False


def test_delete_cancel_leaves_playlist_untouched(monkeypatch):
    monkeypatch.setattr(gui.wx, "ICON_QUESTION", 0, raising=False)
    monkeypatch.setattr(gui.wx, "NO_DEFAULT", 0, raising=False)
    monkeypatch.setattr(gui.wx, "MessageBox", lambda *_args: -1, raising=False)
    frame, calls, _spoken, opened = _frame(lambda one, _two, _other: [one])
    gui.LiteFrame._remove_selected_items(frame)
    assert calls == []
    assert opened == []


def test_delete_requires_entry_from_open_playlist(monkeypatch):
    monkeypatch.setattr(gui.wx, "MessageBox", lambda *_args: gui.wx.YES, raising=False)
    frame, calls, spoken, _opened = _frame(
        lambda _one, _two, _other: [Row("track:7", "Katalog", "track")]
    )
    gui.LiteFrame._remove_selected_items(frame)
    assert calls == []
    assert spoken == ["Wybierz pozycje otwartej playlisty TIDAL do usunięcia"]
