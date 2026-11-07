"""Delete removes selected items from an open Spotify playlist."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import Navigator, OpenSpotifyContainer, SessionId  # noqa: E402


class Runner:
    @staticmethod
    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)


def _frame(selected):
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    context = OpenSpotifyContainer("playlist:1", "playlist1", "Moja", "playlist")
    one = Row("one:0", "Pierwszy", "track", service_id="track1")
    two = Row("two:1", "Drugi", "episode", service_id="episode2")
    other = Row("other:2", "Trzeci", "track", service_id="track3")
    nav.apply_spotify_container(context, "Moja", [one, two, other])
    calls, spoken, opened = [], [], []
    frame = SimpleNamespace(
        navigator=nav,
        client=SimpleNamespace(spotify_playlist_remove_items=lambda playlist_id, items: (
            calls.append((playlist_id, items)) or {"removedCount": len(items),
                                                   "message": "Usunięto z playlisty Spotify"}
        )),
        runner=Runner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: selected(one, two, other),
        _restore_focus_after_dialog=lambda: None,
        _open_spotify_container=lambda intent, after_open=None, on_failure=None: (
            opened.append(intent), after_open() if after_open else None
        ),
    )
    frame._remove_spotify_playlist_items = lambda: gui.LiteFrame._remove_spotify_playlist_items(frame)
    return frame, calls, spoken, opened


def test_delete_removes_selected_rows_and_restores_next_row(monkeypatch):
    monkeypatch.setattr(gui.wx, "ICON_QUESTION", 0, raising=False)
    monkeypatch.setattr(gui.wx, "NO_DEFAULT", 0, raising=False)
    monkeypatch.setattr(gui.wx, "MessageBox", lambda *_args: gui.wx.YES, raising=False)
    frame, calls, spoken, opened = _frame(lambda one, two, _other: [one, two])
    gui.LiteFrame._remove_selected_items(frame)
    assert calls == [("playlist1", [
        {"kind": "track", "externalId": "track1"},
        {"kind": "episode", "externalId": "episode2"},
    ])]
    assert opened[0].preferred_id == "other:2"
    assert spoken[-1] == "Usunięto z playlisty Spotify"


def test_cancel_keeps_playlist_untouched(monkeypatch):
    monkeypatch.setattr(gui.wx, "ICON_QUESTION", 0, raising=False)
    monkeypatch.setattr(gui.wx, "NO_DEFAULT", 0, raising=False)
    monkeypatch.setattr(gui.wx, "MessageBox", lambda *_args: -1, raising=False)
    frame, calls, _spoken, opened = _frame(lambda one, _two, _other: [one])
    gui.LiteFrame._remove_selected_items(frame)
    assert calls == []
    assert opened == []
