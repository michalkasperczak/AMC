"""Trwały postęp podcastu: okresowy zapis bez szumu czytnika ekranu."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402


def _frame(*, active: bool = True, fail: Exception | None = None) -> Any:
    calls: list[str] = []
    said: list[str] = []

    def checkpoint() -> dict:
        calls.append("checkpoint")
        if fail is not None:
            raise fail
        return {"saved": True}

    def submit(_name, work, done, failed) -> None:
        try:
            done(work())
        except Exception as error:
            failed(error)

    return SimpleNamespace(
        client=SimpleNamespace(checkpoint_podcast=checkpoint),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=said.append),
        _podcast_checkpoint_active=active,
        _podcast_checkpoint_pending=False,
        _podcast_checkpoint_due=0.0,
        _last_podcast_progress_error=None,
        calls=calls,
        said=said,
        _note_podcast_progress_error=lambda message: None,
    )


def _wire_error_method(frame) -> None:
    frame._note_podcast_progress_error = (
        lambda message: gui.LiteFrame._note_podcast_progress_error(frame, message)
    )


def test_due_checkpoint_runs_once_and_stays_silent_on_success() -> None:
    frame = _frame()
    _wire_error_method(frame)
    gui.LiteFrame._checkpoint_podcast_if_due(frame)
    gui.LiteFrame._checkpoint_podcast_if_due(frame)
    assert frame.calls == ["checkpoint"]
    assert frame.said == []
    assert frame._podcast_checkpoint_pending is False
    assert frame._podcast_checkpoint_due > 0


def test_no_checkpoint_when_podcast_is_not_playing() -> None:
    frame = _frame(active=False)
    _wire_error_method(frame)
    gui.LiteFrame._checkpoint_podcast_if_due(frame)
    assert frame.calls == []


def test_same_save_failure_is_announced_only_once() -> None:
    frame = _frame(fail=RuntimeError("baza jest zajęta"))
    _wire_error_method(frame)
    gui.LiteFrame._checkpoint_podcast_if_due(frame)
    frame._podcast_checkpoint_due = 0.0
    gui.LiteFrame._checkpoint_podcast_if_due(frame)
    assert frame.calls == ["checkpoint", "checkpoint"]
    assert frame.said == ["Nie zapisano postępu odcinka: baza jest zajęta"]
