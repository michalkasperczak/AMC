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
    automatic_calls: list[bool] = []
    refreshed_views: list[bool] = []
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

    def refresh_automatically():
        automatic_calls.append(True)
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
            session=state,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(
            refresh_podcasts=refresh,
            refresh_podcasts_automatically=refresh_automatically,
        ),
        podcasts=SimpleNamespace(is_available=True),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_bar=SimpleNamespace(show=statuses.append),
        _podcast_refresh_pending=False,
        _podcast_automatic_refresh_pending=False,
        _podcast_automatic_refresh_due=100.0,
        _last_automatic_podcast_error=None,
        calls=calls,
        automatic_calls=automatic_calls,
        refreshed_views=refreshed_views,
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
    frame._refresh_profile_view = lambda **kwargs: refreshed_views.append(kwargs)
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
    assert frame.spoken == ["Odświeżanie wybranego źródła podcastów"]
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


def test_partial_refresh_names_failed_source_and_reopens_with_focus() -> None:
    frame = _frame()
    frame.client.refresh_podcasts = lambda _subscription_id: {
        "requested": 1,
        "succeeded": 0,
        "partial": 0,
        "failed": 1,
        "addedEpisodes": 0,
        "inboxCount": 7,
        "failures": [{
            "title": "TyfloPodcast",
            "reason": "przekroczono czas oczekiwania na odpowiedź",
        }],
        "partialSources": [],
    }

    gui.LiteFrame._refresh_podcasts(frame, refresh_all=False)

    assert frame.spoken == ["Odświeżanie wybranego źródła podcastów"]
    intent, message = frame.reopened[0]
    assert intent.preferred_id == "episode-1"
    assert message == (
        "Odświeżono w całości: 0 z 1. Nie odświeżono: 1. "
        "TyfloPodcast — przekroczono czas oczekiwania na odpowiedź. "
        "Nowe teraz: 0. W skrzynce: 7."
    )
    assert "subscription-private-id" not in message


def test_truncated_feed_is_reported_as_partial_not_full_success() -> None:
    message = gui.podcast_refresh_message({
        "requested": 1,
        "succeeded": 0,
        "partial": 1,
        "failed": 0,
        "addedEpisodes": 25,
        "inboxCount": 9,
        "partialSources": [{
            "Title": "Duży podcast",
            "Reason": "wczytano pierwsze 5000 odcinków; kanał zawiera więcej pozycji",
        }],
    })

    assert message == (
        "Odświeżono w całości: 0 z 1. Częściowo odświeżono: 1. "
        "Duży podcast — wczytano pierwsze 5000 odcinków; kanał zawiera więcej pozycji. "
        "Nowe teraz: 25. W skrzynce: 9."
    )


def test_refresh_message_never_stringifies_protocol_objects() -> None:
    marker = SimpleNamespace(secret="technical-record")
    message = gui.podcast_refresh_message({
        "requested": 1,
        "succeeded": 0,
        "partial": 0,
        "failed": 1,
        "addedEpisodes": 0,
        "inboxCount": 0,
        "failures": [{"title": marker, "reason": marker}],
    })

    assert "SimpleNamespace" not in message
    assert "technical-record" not in message
    assert "Źródło bez nazwy — brak szczegółów" in message


def test_automatic_refresh_waits_until_due_and_stays_silent() -> None:
    frame = _frame()
    original = gui.time.monotonic
    try:
        gui.time.monotonic = lambda: 99.0
        gui.LiteFrame._refresh_due_podcasts_automatically(frame)
        assert frame.automatic_calls == []

        gui.time.monotonic = lambda: 100.0
        gui.LiteFrame._refresh_due_podcasts_automatically(frame)
    finally:
        gui.time.monotonic = original
    assert frame.automatic_calls == [True]
    assert frame._podcast_refresh_pending is False
    assert frame._podcast_automatic_refresh_pending is False
    assert frame._podcast_automatic_refresh_due == 160.0
    assert frame.spoken == []
    assert frame.statuses == ["Automatycznie dodano nowe odcinki: 2"]
    assert frame.refreshed_views == [{"announce_result": False}]


def test_automatic_refresh_with_no_due_sources_does_not_change_the_view() -> None:
    frame = _frame()
    frame.client.refresh_podcasts_automatically = lambda: {
        "requested": 0,
        "succeeded": 0,
        "failed": 0,
        "addedEpisodes": 0,
        "inboxCount": 7,
    }
    original = gui.time.monotonic
    try:
        gui.time.monotonic = lambda: 100.0
        gui.LiteFrame._refresh_due_podcasts_automatically(frame)
    finally:
        gui.time.monotonic = original
    assert frame.statuses == []
    assert frame.spoken == []
    assert frame.refreshed_views == []


def test_automatic_refresh_does_not_report_an_error_without_a_podcast_database() -> None:
    frame = _frame()
    frame.podcasts.is_available = False
    frame._podcast_automatic_refresh_due = 0.0
    gui.LiteFrame._refresh_due_podcasts_automatically(frame)
    assert frame.automatic_calls == []
    assert frame.statuses == []
    assert frame.spoken == []


def test_automatic_refresh_with_no_new_items_does_not_reopen_or_announce() -> None:
    frame = _frame()
    frame.client.refresh_podcasts_automatically = lambda: {
        "requested": 1, "addedEpisodes": 0,
    }
    frame._podcast_automatic_refresh_due = 0.0
    gui.LiteFrame._refresh_due_podcasts_automatically(frame)
    assert frame.refreshed_views == []
    assert frame.spoken == []
    assert frame.statuses == []


def test_automatic_refresh_does_not_reopen_the_player() -> None:
    frame = _frame()
    frame.navigator.session.view = View.PLAYER
    frame._podcast_automatic_refresh_due = 0.0
    gui.LiteFrame._refresh_due_podcasts_automatically(frame)
    assert frame.refreshed_views == []
    assert frame.spoken == []
    assert frame.statuses == ["Automatycznie dodano nowe odcinki: 2"]
