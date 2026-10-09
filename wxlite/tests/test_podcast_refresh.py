"""Odswiezanie podcastow zachowuje widok i techniczne Id trzyma poza mowa."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import LibraryView, SessionId, View  # noqa: E402


def _frame(*, library_view=LibraryView.PODCAST_EPISODES):
    calls: list[str | None] = []
    spoken: list[str] = []
    statuses: list[str] = []
    reopened: list[tuple[object, str]] = []
    model = ListModel()
    model.replace([
        Row(
            item_id="episode-1",
            title="Jawny tytuł odcinka",
            kind="episode",
            parent_id="subscription-private-id",
        )
    ])
    state = SimpleNamespace(
        library_view=library_view,
        library_playlist_id=(
            "subscription-private-id"
            if library_view is LibraryView.PODCAST_EPISODES
            else None
        ),
        model=model,
        view=View.LIST,
    )

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    def refresh(subscription_id=None):
        calls.append(subscription_id)
        return {
            "requested": 1,
            "succeeded": 1,
            "failed": 0,
            "addedEpisodes": 2,
            "inboxCount": 7,
        }

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(refresh_podcasts=refresh),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_bar=SimpleNamespace(show=statuses.append),
        _podcast_refresh_pending=False,
        calls=calls,
        spoken=spoken,
        statuses=statuses,
        reopened=reopened,
    )
    frame._current_podcast_subscription_id = lambda: (
        gui.LiteFrame._current_podcast_subscription_id(frame)
    )
    frame._open_podcast_view = (
        lambda intent, completion_message="": reopened.append(
            (intent, completion_message)
        )
    )
    frame._open_podcast_library = (
        lambda preferred_id=None, completion_message="": reopened.append(
            ((preferred_id, "library"), completion_message)
        )
    )
    frame._open_podcast_aggregate = (
        lambda intent, completion_message="": reopened.append(
            (intent, completion_message)
        )
    )
    return frame


def test_f5_refreshes_the_current_source_and_reopens_the_same_episode_view() -> None:
    frame = _frame()
    gui.LiteFrame._refresh_podcasts(frame, refresh_all=False)

    assert frame.calls == ["subscription-private-id"]
    assert frame._podcast_refresh_pending is False
    assert len(frame.reopened) == 1
    intent, message = frame.reopened[0]
    assert intent.subscription_id == "subscription-private-id"
    assert intent.preferred_id == "episode-1"
    assert message == (
        "Odświeżono źródła: 1 z 1. Nowe teraz: 2. W skrzynce: 7."
    )
    assert frame.spoken == []
    assert "subscription-private-id" not in " ".join(frame.statuses)


def test_ctrl_f5_refreshes_all_sources_without_sending_a_private_id() -> None:
    frame = _frame(library_view=LibraryView.PODCAST_INBOX)
    gui.LiteFrame._refresh_podcasts(frame, refresh_all=True)

    assert frame.calls == [None]
    assert len(frame.reopened) == 1
    assert frame.reopened[0][0].view is LibraryView.PODCAST_INBOX


def test_second_refresh_is_refused_while_the_first_is_running() -> None:
    frame = _frame()
    frame._podcast_refresh_pending = True
    gui.LiteFrame._refresh_podcasts(frame, refresh_all=False)

    assert frame.calls == []
    assert frame.spoken == ["Odświeżanie źródeł już trwa"]
