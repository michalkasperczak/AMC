"""Ulubione podcastów zachowują wielokrotny wybór i czystą mowę."""

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


def _frame(rows: list[Row], *, favorite: bool, library_view=LibraryView.PODCAST_INBOX):
    model = ListModel()
    model.replace(rows)
    state = SimpleNamespace(
        library_view=library_view,
        library_playlist_id=None,
        model=model,
        view=View.LIST,
    )
    calls: list[tuple[list[str], list[str]]] = []
    spoken: list[str] = []
    statuses: list[str] = []
    reloaded: list[tuple[str, str]] = []
    opened_library: list[tuple[str, str]] = []

    def toggle(subscription_ids, episode_ids):
        calls.append((list(subscription_ids), list(episode_ids)))
        return {
            "favorite": favorite,
            "requested": len(subscription_ids) + len(episode_ids),
            "changed": len(subscription_ids) + len(episode_ids),
        }

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    def reload(preferred, message):
        reloaded.append((preferred, message))
        return True

    def open_library(*, preferred_id, completion_message):
        opened_library.append((preferred_id, completion_message))

    return SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(toggle_podcast_favorites=toggle),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_field=SimpleNamespace(SetLabel=statuses.append),
        status_bar=SimpleNamespace(show=statuses.append),
        _podcast_favorite_pending=False,
        _selected_action_rows=lambda: rows,
        _reload_podcast_list_after_download=reload,
        _open_podcast_library=open_library,
        calls=calls,
        spoken=spoken,
        statuses=statuses,
        reloaded=reloaded,
        opened_library=opened_library,
    )


def test_ctrl_shift_u_separates_podcasts_and_episodes_from_multi_selection() -> None:
    rows = [
        Row(item_id="podcast-private-1", title="Podcast jeden", kind="podcast"),
        Row(item_id="episode-private-1", title="Odcinek jeden", kind="episode"),
        Row(item_id="episode-private-2", title="Odcinek dwa", kind="episode"),
    ]
    frame = _frame(rows, favorite=True)

    gui.LiteFrame._toggle_podcast_favorite(frame)

    assert frame.calls == [
        (["podcast-private-1"], ["episode-private-1", "episode-private-2"])
    ]
    assert frame.reloaded == [
        ("podcast-private-1", "Dodano do ulubionych: 3 elementy")
    ]
    assert frame.spoken == []
    assert not frame._podcast_favorite_pending


def test_single_podcast_is_reloaded_in_library_with_its_user_facing_name() -> None:
    row = Row(item_id="podcast-private-1", title="Audycja tygodnia", kind="podcast")
    frame = _frame([row], favorite=False, library_view=LibraryView.PODCAST_LIBRARY)

    gui.LiteFrame._toggle_podcast_favorite(frame)

    assert frame.calls == [(["podcast-private-1"], [])]
    assert frame.opened_library == [
        ("podcast-private-1", "Usunięto z ulubionych: Audycja tygodnia")
    ]
    assert frame.spoken == []


def test_player_view_does_not_get_pulled_back_to_list_after_change() -> None:
    row = Row(item_id="episode-private-1", title="Odcinek", kind="episode")
    frame = _frame([row], favorite=True)
    frame.navigator.sessions[SessionId.PODCASTS].view = View.PLAYER

    gui.LiteFrame._toggle_podcast_favorite(frame)

    assert frame.reloaded == []
    assert frame.opened_library == []
    assert frame.spoken == ["Dodano do ulubionych: Odcinek"]


def test_non_podcast_rows_are_never_sent_as_private_ids() -> None:
    row = Row(item_id="D:/nagranie.mp3", title="Nagranie", kind="track")
    frame = _frame([row], favorite=True)

    gui.LiteFrame._toggle_podcast_favorite(frame)

    assert frame.calls == []
    assert frame.spoken == ["Zaznacz podcast albo odcinek"]
