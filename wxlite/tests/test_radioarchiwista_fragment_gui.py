"""Native wx contract for excerpt progress, cancellation and focus return."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

import pytest

wx = pytest.importorskip("wx")
from amc_wx_lite import gui
from amc_wx_lite.list_model import Row


class Runner:
    def __init__(self) -> None:
        self.jobs = {}

    def submit(self, name, work, done, failed):
        self.jobs[name] = (work, done, failed)


def setup(monkeypatch, tmp_path: Path):
    app = wx.GetApp() or wx.App(False)
    dialogs = []

    class HiddenDialog(wx.Dialog):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, **kwargs)
            dialogs.append(self)

        def Show(self, show=True):
            return True

    class RangeDialog:
        selected_range = (10, 30)

        def __init__(self, *_args):
            pass

        def ShowModal(self):
            return wx.ID_OK

        def Destroy(self):
            pass

    monkeypatch.setattr(gui.wx, "Dialog", HiddenDialog)
    monkeypatch.setattr(gui, "RadioarchiwistaFragmentDialog", RangeDialog)
    monkeypatch.setattr(
        gui.LiteFrame, "_description_target_row",
        staticmethod(lambda _frame, _session: Row(
            "rarch:rec:pr3:one", "Audycja", "recording",
            url="https://archive.org/a.mp3",
        )),
    )
    frame = wx.Frame(None)
    frame._radioarchiwista_downloads = set()
    frame._radioarchiwista_source = lambda: SimpleNamespace(recording_by_id=lambda _id: object())
    frame.layout = SimpleNamespace(lite_settings_dir=tmp_path)
    frame.status_field = wx.StaticText(frame)
    frame.status_bar = SimpleNamespace(show=lambda _message: None)
    frame.announcer = SimpleNamespace(say=lambda _message: None)
    focused = []
    frame._restore_focus_after_dialog = lambda: focused.append(True)
    frame.runner = Runner()
    frame.saved = []
    frame._record_download = lambda path, **kwargs: frame.saved.append((path, kwargs)) or True
    return app, frame, dialogs, focused


def test_excerpt_progress_transfer_export_and_finish(monkeypatch, tmp_path: Path) -> None:
    app, frame, dialogs, focused = setup(monkeypatch, tmp_path)
    destination = tmp_path / "fragment.mp3"
    operation_ids = []
    frame.client = SimpleNamespace(
        audio_clip_capabilities=lambda _name: {
            "formats": [{"value": "original", "available": True}]
        },
        export_audio_clip=lambda **args: operation_ids.append(args["operation_id"]),
        cancel_audio_clip=lambda _id: None,
    )
    monkeypatch.setattr(gui._radioarchiwista_module, "fragment_destination", lambda *_: destination)

    def download(_recording, target, _start, _end, *, export, progress, should_cancel):
        assert not should_cancel()
        progress(100)
        export("source.mp3", str(target), 10, 30)
        return target

    monkeypatch.setattr(gui._radioarchiwista_module, "download_fragment", download)
    try:
        gui.LiteFrame._download_radioarchiwista_fragment(frame)
        assert len(dialogs) == 1
        assert "rozmiar pliku nieznany" in dialogs[0].GetChildren()[0].GetChildren()[-1].GetName()
        result = frame.runner.jobs["radioarchiwista.fragment"][0]()
        # Host may publish progress before the queued stage transition runs.
        frame._radioarchiwista_fragment_progress({
            "operationId": operation_ids[0], "percent": 65,
        })
        app.Yield()
        assert "65 procent" in frame.status_field.GetLabel()
        button = next(child for child in dialogs[0].GetChildren()[0].GetChildren()
                      if isinstance(child, wx.Button))
        assert "65 procent" in button.GetName()
        frame.runner.jobs["radioarchiwista.fragment"][1](result)
        assert "_radioarchiwista_fragment_progress" not in frame.__dict__
        assert frame._radioarchiwista_downloads == set()
        assert focused
        assert frame.saved == [(destination, {"title": "Audycja", "source": "archive"})]
    finally:
        frame.Destroy()
        app.Yield()


def test_excerpt_cancel_while_exporting_requests_host_stop(monkeypatch, tmp_path: Path) -> None:
    app, frame, dialogs, _focused = setup(monkeypatch, tmp_path)
    destination = tmp_path / "fragment.mp3"
    export_ids = []
    frame.client = SimpleNamespace(
        audio_clip_capabilities=lambda _name: {
            "formats": [{"value": "original", "available": True}]
        },
        export_audio_clip=lambda **args: export_ids.append(args["operation_id"]),
        cancel_audio_clip=lambda operation_id: export_ids.append(f"cancel:{operation_id}"),
    )
    monkeypatch.setattr(gui._radioarchiwista_module, "fragment_destination", lambda *_: destination)

    def download(_recording, target, _start, _end, *, export, progress, should_cancel):
        export("source.mp3", str(target), 10, 30)
        app.Yield()
        button = next(child for child in dialogs[0].GetChildren()[0].GetChildren()
                      if isinstance(child, wx.Button))
        button.Command(wx.CommandEvent(wx.EVT_BUTTON.typeId, button.GetId()))
        assert should_cancel()
        raise gui._radioarchiwista_module.RadioarchiwistaError("anulowane")

    monkeypatch.setattr(gui._radioarchiwista_module, "download_fragment", download)
    try:
        gui.LiteFrame._download_radioarchiwista_fragment(frame)
        with pytest.raises(gui._radioarchiwista_module.RadioarchiwistaError):
            frame.runner.jobs["radioarchiwista.fragment"][0]()
        cancel_work = frame.runner.jobs["radioarchiwista.fragment.cancel"][0]
        cancel_work()
        assert export_ids == [export_ids[0], f"cancel:{export_ids[0]}"]
        frame.runner.jobs["radioarchiwista.fragment"][2](RuntimeError("cancelled"))
        assert frame._radioarchiwista_downloads == set()
    finally:
        frame.Destroy()
        app.Yield()
