"""Pełny opis i przejście do podcastu zachowują tekst oraz fokus."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import SessionId  # noqa: E402
from amc_wx_lite.podcast_source import (  # noqa: E402
    PodcastDescription,
    PodcastSubscription,
)


def _sync_submit(_name, work, done, failed):
    try:
        done(work())
    except Exception as error:
        failed(error)


def _state(row: Row):
    model = ListModel()
    model.replace([row])
    return SimpleNamespace(model=model, library_return_id=None)


def test_alt_d_opens_native_description_and_copies_only_user_text() -> None:
    row = Row("private-episode-id", "Odcinek próbny", "episode")
    state = _state(row)
    spoken: list[str] = []
    copied: list[str] = []
    shown: list[PodcastDescription] = []
    information = PodcastDescription(
        window_title="Opis odcinka",
        text="Treść opisu.\n\nOdcinek podcastu\nTytuł: Odcinek próbny",
        initial_focus_name="Treść opisu.",
    )

    class Dialog:
        def __init__(self, _parent, received, copy_text):
            shown.append(received)
            self._copy_text = copy_text

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):  # noqa: N802 - wx API
            assert self._copy_text(information.text)
            return gui.wx.ID_CANCEL

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(
            description=lambda item_id, item_kind: information
            if (item_id, item_kind) == ("private-episode-id", "episode")
            else None
        ),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _to_clipboard=lambda text: copied.append(text) is None,
    )
    original = gui.PodcastDescriptionDialog
    gui.PodcastDescriptionDialog = Dialog
    try:
        gui.LiteFrame._show_podcast_description(frame)
    finally:
        gui.PodcastDescriptionDialog = original

    assert shown == [information]
    assert copied == [information.text]
    assert spoken == ["Skopiowano całą treść"]
    exposed = " ".join(spoken + copied)
    assert "private-episode-id" not in exposed


def test_alt_d_reports_an_empty_episode_description_in_plain_language() -> None:
    row = Row("private-episode-id", "Bez opisu", "episode")
    state = _state(row)
    spoken: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(description=lambda *_args: None),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=spoken.append),
    )

    gui.LiteFrame._show_podcast_description(frame)

    assert spoken == ["Ten odcinek nie zawiera opisu"]


def test_go_to_related_podcast_opens_parent_and_reselects_episode() -> None:
    row = Row("private-episode-id", "Wybrany odcinek", "episode")
    state = _state(row)
    opened = []
    spoken: list[str] = []
    subscription = PodcastSubscription(
        "private-parent-id", "Audycja tygodnia", 0
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(related_podcast=lambda _item_id: subscription),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _open_podcast_view=opened.append,
    )

    gui.LiteFrame._go_to_related_podcast(frame)

    assert spoken == []
    assert state.library_return_id == "private-parent-id"
    assert len(opened) == 1
    assert opened[0].subscription_id == "private-parent-id"
    assert opened[0].subscription_title == "Audycja tygodnia"
    assert opened[0].preferred_id == "private-episode-id"

