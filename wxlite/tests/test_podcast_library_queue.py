"""Biblioteka i kolejka podcastów nie mogą wpadać do operacji plikowych."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import LibraryView, SessionId, View  # noqa: E402
from amc_wx_lite.podcast_source import PodcastSubscription  # noqa: E402


def _submit(_name, work, done, failed):
    try:
        done(work())
    except Exception as error:  # pragma: no cover - wyłącznie ścieżka atrapy
        failed(error)


def test_ctrl_shift_l_on_episodes_targets_each_parent_once() -> None:
    rows = [
        Row("episode-1", "Pierwszy", "episode", parent_id="podcast-1"),
        Row("episode-2", "Drugi", "episode", parent_id="podcast-1"),
        Row("episode-3", "Trzeci", "episode", parent_id="podcast-2"),
    ]
    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.PODCAST_INBOX,
        library_playlist_id=None,
        model=ListModel(),
    )
    calls = []
    reloaded = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(
            toggle_podcast_library=lambda ids: calls.append(list(ids))
            or {"added": True, "changed": 2}
        ),
        runner=SimpleNamespace(submit=_submit),
        announcer=SimpleNamespace(say=lambda text: None),
        _podcast_library_pending=False,
        _selected_action_rows=lambda: rows,
        _open_podcast_library=lambda **_kwargs: None,
        _reload_podcast_list_after_download=lambda preferred, message: (
            reloaded.append((preferred, message)) or True
        ),
    )

    gui.LiteFrame._toggle_podcast_library(frame)

    assert calls == [["podcast-1", "podcast-2"]]
    assert reloaded == [("episode-1", "Dodano do Biblioteki: 3 elementy")]
    assert not frame._podcast_library_pending


@pytest.mark.parametrize("filter_query", ["", "match"])
def test_ctrl_shift_l_removing_library_source_keeps_next_visible_source_selected(filter_query) -> None:
    """Removing a source refreshes the real model onto its visible neighbour."""

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:  # pragma: no cover - fixture safety
                failed(error)

    before = Row("podcast-1", "Pierwszy match", "podcast")
    removed = Row("podcast-2", "Usuwany kanał match", "podcast")
    after = Row("podcast-3", "Następny kanał match", "podcast")
    hidden = Row("podcast-hidden", "Inny kanał", "podcast")
    library_rows = [before, removed, hidden, after] if filter_query else [before, removed, after]
    navigator = gui.Navigator()
    navigator.active = SessionId.PODCASTS
    navigator.apply_podcast_library(library_rows)
    navigator.session.model.select_id(removed.item_id)
    spoken = []

    subscriptions_after_removal = [
        PodcastSubscription(row.item_id, row.title, rank)
        for rank, row in enumerate(library_rows) if row is not removed
    ]
    frame = SimpleNamespace(
        navigator=navigator,
        client=SimpleNamespace(
            toggle_podcast_library=lambda ids: (
                {"added": False, "changed": len(ids)}
            )
        ),
        podcasts=SimpleNamespace(
            is_available=True,
            ordered_subscriptions=lambda *_args, **_kwargs: (
                subscriptions_after_removal,
                True,
            ),
        ),
        runner=Runner(),
        announcer=SimpleNamespace(say=spoken.append),
        _podcast_library_pending=False,
        filter_box=SimpleNamespace(GetValue=lambda: filter_query),
        _selected_action_rows=lambda: [navigator.session.model.selected_row],
        _sync_views=lambda: None,
    )
    frame._open_podcast_library = lambda **kwargs: gui.LiteFrame._open_podcast_library(
        frame, **kwargs
    )

    gui.LiteFrame._toggle_podcast_library(frame)

    assert navigator.session.model.selected_id == after.item_id
    assert spoken == ["Usunięto z Biblioteki: Usuwany kanał match"]
    assert not frame._podcast_library_pending


def test_ctrl_shift_l_late_success_keeps_newer_library_selection() -> None:
    queued = []

    class DeferredRunner:
        @staticmethod
        def submit(_name, work, done, failed):
            queued.append((work, done, failed))

    removed = Row("podcast-1", "Usuwany kanał", "podcast")
    later = Row("podcast-2", "Czytany kanał", "podcast")
    navigator = gui.Navigator()
    navigator.active = SessionId.PODCASTS
    navigator.apply_podcast_library([removed, later])
    navigator.session.model.select_id(removed.item_id)
    opened = []
    frame = SimpleNamespace(
        navigator=navigator,
        client=SimpleNamespace(toggle_podcast_library=lambda _ids: {
            "added": False, "changed": 1,
        }),
        runner=DeferredRunner(),
        announcer=SimpleNamespace(say=lambda _message: None),
        _podcast_library_pending=False,
        _selected_action_rows=lambda: [removed],
        _open_podcast_library=lambda **kwargs: opened.append(kwargs),
    )

    gui.LiteFrame._toggle_podcast_library(frame)
    navigator.session.model.select_id(later.item_id)
    work, done, _failed = queued.pop()
    done(work())

    assert opened == [{
        "preferred_id": later.item_id,
        "completion_message": "Usunięto z Biblioteki: Usuwany kanał",
    }]


def test_ctrl_shift_l_late_success_after_session_change_does_not_restore_podcast_list() -> None:
    """A completed background write must not reclaim focus from another session."""

    queued = []

    class DeferredRunner:
        @staticmethod
        def submit(_name, work, done, failed):
            queued.append((work, done, failed))

    row = Row("podcast-1", "Kanał", "podcast")
    navigator = gui.Navigator()
    navigator.active = SessionId.PODCASTS
    navigator.apply_podcast_library([row])
    spoken = []

    def unexpected_refresh(**_kwargs):
        raise AssertionError("stale refresh")

    frame = SimpleNamespace(
        navigator=navigator,
        client=SimpleNamespace(
            toggle_podcast_library=lambda _ids: {"added": False, "changed": 1}
        ),
        runner=DeferredRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _podcast_library_pending=False,
        _selected_action_rows=lambda: [row],
        _open_podcast_library=unexpected_refresh,
    )

    gui.LiteFrame._toggle_podcast_library(frame)
    navigator.active = SessionId.FILES
    work, done, _failed = queued.pop()
    done(work())

    assert navigator.active is SessionId.FILES
    assert navigator.sessions[SessionId.PODCASTS].model.selected_id == row.item_id
    assert spoken == ["Usunięto z Biblioteki: Kanał"]
    assert not frame._podcast_library_pending


def test_ctrl_shift_l_failed_write_keeps_selection_and_announces_once() -> None:
    """A refused write leaves the model untouched and reports one failure."""

    row = Row("podcast-1", "Kanał", "podcast")
    model = ListModel()
    model.replace([row])
    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.PODCAST_LIBRARY,
        model=model,
    )
    spoken = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(
            toggle_podcast_library=lambda _ids: (_ for _ in ()).throw(
                RuntimeError("odmowa")
            )
        ),
        runner=SimpleNamespace(submit=_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _podcast_library_pending=False,
        _selected_action_rows=lambda: [row],
    )

    gui.LiteFrame._toggle_podcast_library(frame)

    assert model.selected_id == row.item_id
    assert spoken == ["Nie udało się zmienić Biblioteki: odmowa"]
    assert not frame._podcast_library_pending


def test_removing_open_podcast_returns_to_main_library() -> None:
    row = Row("episode-1", "Odcinek", "episode", parent_id="podcast-1")
    library_rows = [
        Row("podcast-1", "Usuwany podcast", "podcast"),
        Row("podcast-2", "Sąsiedni podcast", "podcast"),
    ]
    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.PODCAST_EPISODES,
        library_playlist_id="podcast-1",
        model=ListModel(),
    )
    opened = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(
            toggle_podcast_library=lambda _ids: {"added": False, "changed": 1}
        ),
        runner=SimpleNamespace(submit=_submit),
        announcer=SimpleNamespace(say=lambda text: None),
        _podcast_library_pending=False,
        _podcast_library_rows=library_rows,
        _selected_action_rows=lambda: [row],
        _open_podcast_library=lambda **kwargs: opened.append(kwargs),
        _reload_podcast_list_after_download=lambda *_args: False,
    )

    gui.LiteFrame._toggle_podcast_library(frame)

    assert state.library_view is LibraryView.PODCAST_LIBRARY
    assert state.library_playlist_id is None
    assert opened == [{
        "preferred_id": "podcast-2",
        "completion_message": "Usunięto z Biblioteki: Odcinek",
    }]


@pytest.mark.parametrize(
    "view",
    (
        LibraryView.PODCAST_LIBRARY,
        LibraryView.PODCAST_EPISODES,
        LibraryView.PODCAST_FAVORITES,
    ),
)
def test_podcast_profile_refresh_delivers_confirmation_after_list_update(view) -> None:
    """Delete must pass its completion callback through every podcast list."""

    opened = []
    callback = lambda: None
    state = SimpleNamespace(library_view=view, library_playlist_id="source-1")
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.PODCASTS, session=state),
        _open_podcast_library=lambda **kwargs: opened.append(kwargs),
        _open_podcast_view=lambda _intent, **kwargs: opened.append(kwargs),
        _open_podcast_aggregate=lambda _intent, **kwargs: opened.append(kwargs),
    )

    gui.LiteFrame._refresh_profile_view(
        frame, preferred_id="next-row", after_refresh=callback,
    )

    assert opened == [{"announce_result": False, "after_refresh": callback, **(
        {"preferred_id": "next-row"} if view is LibraryView.PODCAST_LIBRARY else {}
    )}]


def test_podcast_queue_sends_only_episode_ids_and_keeps_local_queue_separate() -> None:
    rows = [
        Row("episode-1", "Pierwszy", "episode", parent_id="podcast-1"),
        Row("podcast-1", "Podcast", "podcast"),
    ]
    state = SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.PODCAST_EPISODES,
    )
    calls = []
    spoken = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(
            toggle_podcast_queue=lambda ids, play_next=False: (
                calls.append((list(ids), play_next))
                or {"added": True, "changed": 1}
            )
        ),
        runner=SimpleNamespace(submit=_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _podcast_queue_pending=False,
        _selected_action_rows=lambda: rows,
        _open_podcast_aggregate=lambda *_args, **_kwargs: None,
    )

    gui.LiteFrame._toggle_podcast_queue(frame, False)

    assert calls == [(["episode-1"], False)]
    assert spoken == ["Dodano do kolejki: Pierwszy"]


def test_generic_queue_dispatches_podcasts_before_local_files() -> None:
    calls = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.PODCASTS),
        _toggle_podcast_queue=lambda play_next: calls.append(play_next),
    )

    gui.LiteFrame._toggle_queue_membership(frame, True)

    assert calls == [True]
