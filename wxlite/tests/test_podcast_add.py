"""Dodawanie źródła podcastów ma czystą mowę i wąski zapis przez host."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.navigation import LibraryView, SessionId, View  # noqa: E402


class _Dialog:
    values = ("https://example.invalid/feed.xml", "Moja nazwa")

    def __init__(self, _parent):
        pass

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        return False

    def ShowModal(self):  # noqa: N802 - API wx
        return gui.wx.ID_OK


def _frame():
    calls: list[tuple[str, str]] = []
    spoken: list[str] = []
    statuses: list[str] = []
    reopened: list[tuple[str | None, str]] = []
    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.PODCAST_LIBRARY,
    )

    def add(address, title):
        calls.append((address, title))
        return {
            "subscriptionId": "private-subscription-id",
            "title": "Moja nazwa",
            "sourceLabel": "podcast",
            "added": True,
            "restored": False,
            "itemCount": 12,
        }

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(add_podcast_source=add),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_field=SimpleNamespace(SetLabel=statuses.append),
        status_bar=SimpleNamespace(show=statuses.append),
        _podcast_add_pending=False,
        _open_podcast_library=lambda preferred_id=None, completion_message="": (
            reopened.append((preferred_id, completion_message))
        ),
        calls=calls,
        spoken=spoken,
        statuses=statuses,
        reopened=reopened,
    )
    return frame


def test_ctrl_n_adds_source_without_speaking_private_identifier() -> None:
    frame = _frame()
    original = gui.PodcastSourceDialog
    gui.PodcastSourceDialog = _Dialog
    try:
        gui.LiteFrame._add_podcast_source(frame)
    finally:
        gui.PodcastSourceDialog = original

    assert frame.calls == [
        ("https://example.invalid/feed.xml", "Moja nazwa")
    ]
    assert frame._podcast_add_pending is False
    assert frame.spoken == ["Sprawdzanie i dodawanie źródła"]
    assert frame.reopened == [
        (
            "private-subscription-id",
            "Dodano: podcast: Moja nazwa. Pozycji: 12",
        )
    ]
    exposed = " ".join(frame.spoken + frame.statuses + [frame.reopened[0][1]])
    assert "private-subscription-id" not in exposed


def test_empty_address_is_rejected_before_host_call() -> None:
    frame = _frame()

    class EmptyDialog(_Dialog):
        values = ("", "")

    original = gui.PodcastSourceDialog
    gui.PodcastSourceDialog = EmptyDialog
    try:
        gui.LiteFrame._add_podcast_source(frame)
    finally:
        gui.PodcastSourceDialog = original

    assert frame.calls == []
    assert frame.spoken == ["Wpisz adres źródła"]
