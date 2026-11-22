"""Delete keeps a visible neighbor in podcast and saved YouTube queues."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui
from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.navigation import LibraryView, SessionId, View


def _setup(selected="b", query=""):
    rows = [
        Row("a", "Podcast A", "episode"),
        Row("b", "Podcast B", "episode"),
        Row("c", "Film YouTube C", "episode"),
        Row("d", "Podcast D", "episode"),
    ]
    model = ListModel()
    model.replace(rows)
    model.select_id(selected)
    state = SimpleNamespace(
        view=View.LIST, library_view=LibraryView.PODCAST_QUEUE, model=model,
    )
    navigator = SimpleNamespace(
        active=SessionId.PODCASTS,
        session=state,
        sessions={SessionId.PODCASTS: state},
    )
    requests = []
    refreshes = []
    spoken = []
    operations = []
    client = SimpleNamespace(remove_profile_items=lambda *args: operations.append(args) or {"removedCount": 1})
    frame = SimpleNamespace(
        navigator=navigator,
        _podcast_queue_removal_pending=False,
        _selected_action_rows=lambda: [model.selected_row],
        _profile_edit_client=lambda: client,
        filter_box=SimpleNamespace(GetValue=lambda: query),
        runner=SimpleNamespace(submit=lambda *args: requests.append(args)),
        announcer=SimpleNamespace(say=spoken.append),
        _open_podcast_aggregate=lambda intent, **kwargs: refreshes.append((intent, kwargs)),
    )
    return frame, requests, refreshes, spoken, operations


def _finish(requests, payload=None):
    _slot, work, done, _failed = requests.pop()
    result = work()
    done(result if payload is None else payload)


def test_delete_middle_prefers_next_visible_youtube_episode_once():
    frame, requests, refreshes, spoken, operations = _setup()
    gui.LiteFrame._remove_podcast_queue_items(frame)
    _finish(requests)
    assert operations == [("podcasts", "podcastQueue", ["b"])]
    assert len(refreshes) == 1
    assert refreshes[0][0].preferred_id == "c"
    assert refreshes[0][1] == {"completion_message": "Usunięto z Kolejki podcastów: Podcast B"}
    assert spoken == []


def test_delete_with_filter_uses_visible_neighbor():
    frame, requests, refreshes, _, _ = _setup(query="Podcast")
    gui.LiteFrame._remove_podcast_queue_items(frame)
    _finish(requests)
    assert refreshes[0][0].preferred_id == "d"


def test_delete_last_prefers_previous_visible_episode():
    frame, requests, refreshes, _, _ = _setup(selected="d", query="Podcast")
    gui.LiteFrame._remove_podcast_queue_items(frame)
    _finish(requests)
    assert refreshes[0][0].preferred_id == "b"


def test_delete_preserves_user_selection_changed_during_write():
    frame, requests, refreshes, _, _ = _setup()
    gui.LiteFrame._remove_podcast_queue_items(frame)
    frame.navigator.session.model.select_id("d")
    _finish(requests)
    assert refreshes[0][0].preferred_id == "d"


def test_delete_does_not_refresh_or_announce_after_view_change():
    frame, requests, refreshes, spoken, _ = _setup()
    gui.LiteFrame._remove_podcast_queue_items(frame)
    frame.navigator.session.library_view = LibraryView.PODCAST_LIBRARY
    _finish(requests)
    assert refreshes == []
    assert spoken == []
    assert frame._podcast_queue_removal_pending is False


def test_duplicate_delete_during_write_submits_only_once():
    frame, requests, _, _, _ = _setup()
    gui.LiteFrame._remove_podcast_queue_items(frame)
    gui.LiteFrame._remove_podcast_queue_items(frame)
    assert len(requests) == 1
