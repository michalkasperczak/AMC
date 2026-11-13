"""Native wx smoke contract for the Radio Archiwista download window."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

import pytest

wx = pytest.importorskip("wx")
from amc_wx_lite import gui
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import SessionId


class _Runner:
    def submit(self, _name, work, done, failed):
        self.work = work
        self.done = done
        self.failed = failed


def test_progress_reaches_100_without_native_completion_dialog(monkeypatch, tmp_path: Path) -> None:
    app = wx.GetApp() or wx.App(False)
    dialogs = []

    class HiddenDialog(wx.Dialog):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, **kwargs)
            dialogs.append(self)

        def Show(self, show=True):  # noqa: N802 - wx API
            return True

    monkeypatch.setattr(gui.wx, "Dialog", HiddenDialog)
    row = Row("rarch:rec:pr3:one", "Audycja", "recording", url="https://archive.org/a.mp3")
    monkeypatch.setattr(
        gui.LiteFrame, "_description_target_row",
        staticmethod(lambda _frame, _session: row),
    )
    destination = tmp_path / "audycja.mp3"
    monkeypatch.setattr(gui._radioarchiwista_module, "download_destination", lambda *_: destination)

    def download(_url, target, *, progress, should_cancel):
        assert not should_cancel()
        progress(100)
        return target

    monkeypatch.setattr(gui._radioarchiwista_module, "stream_to_file", download)
    frame = wx.Frame(None)
    frame.navigator = SimpleNamespace(sessions={SessionId.RADIOARCHIWISTA: object()})
    frame._radioarchiwista_downloads = set()
    frame._radioarchiwista_source = lambda: SimpleNamespace(recording_by_id=lambda _id: object())
    frame.layout = SimpleNamespace(lite_settings_dir=tmp_path)
    frame.status_field = wx.StaticText(frame)
    shown = []
    frame.status_bar = SimpleNamespace(show=shown.append)
    spoken = []
    frame.announcer = SimpleNamespace(say=spoken.append)
    focused = []
    frame._restore_focus_after_dialog = lambda: focused.append(True)
    frame.runner = _Runner()
    saved = []
    frame._record_download = lambda path, **kwargs: saved.append((path, kwargs)) or True
    try:
        gui.LiteFrame._download_radioarchiwista_recording(frame)
        assert len(dialogs) == 1
        dialog = dialogs[0]
        button = next(child for child in dialog.GetChildren()[0].GetChildren()
                      if isinstance(child, wx.Button))
        assert "rozmiar pliku nieznany" in button.GetName()
        app.Yield()
        assert focused == [True]
        assert any("w tle" in message for message in spoken)
        result = frame.runner.work()
        app.Yield()
        assert "100 procent" in button.GetName()
        assert "100 procent" in frame.status_field.GetLabel()
        frame.runner.done(result)
        assert focused == [True, True]
        assert frame._radioarchiwista_downloads == set()
        assert any("Pobrano nagranie" in message for message in spoken)
        assert saved == [(destination, {"title": "Audycja", "source": "archive"})]
    finally:
        frame.Destroy()
        app.Yield()
