"""Pobieranie podcastów zachowuje wielokrotne zaznaczenie i czystą mowę."""

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


def _frame(*, result: dict | None = None):
    rows = [
        Row(item_id="private-episode-1", title="Pierwszy odcinek", kind="episode"),
        Row(item_id="private-episode-2", title="Drugi odcinek", kind="episode"),
    ]
    model = ListModel()
    model.replace(rows)
    state = SimpleNamespace(
        library_view=LibraryView.PODCAST_INBOX,
        library_playlist_id=None,
        model=model,
        view=View.LIST,
    )
    calls: list[list[str]] = []
    spoken: list[str] = []
    statuses: list[str] = []
    reloaded: list[tuple[str, str]] = []

    def download(ids):
        calls.append(list(ids))
        return result or {
            "requested": 2,
            "downloaded": 2,
            "alreadyDownloaded": 0,
            "failed": 0,
        }

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    def reload(preferred, message):
        reloaded.append((preferred, message))
        spoken.append(message)
        return True

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        client=SimpleNamespace(download_podcast_episodes=download),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_field=SimpleNamespace(SetLabel=statuses.append),
        status_bar=SimpleNamespace(show=statuses.append),
        _podcast_download_pending=False,
        _selected_action_rows=lambda: rows,
        _reload_podcast_list_after_download=reload,
        calls=calls,
        spoken=spoken,
        statuses=statuses,
        reloaded=reloaded,
    )
    return frame


def test_ctrl_d_passes_all_selected_episode_ids_and_reports_counts() -> None:
    frame = _frame()
    gui.LiteFrame._download_podcast_episodes(frame)

    assert frame.calls == [["private-episode-1", "private-episode-2"]]
    assert frame._podcast_download_pending is False
    assert frame.spoken == ["Pobieranie odcinków: 2", "Pobrano: 2"]
    assert frame.reloaded == [("private-episode-1", "Pobrano: 2")]
    assert "private-episode" not in " ".join(frame.spoken + frame.statuses)


def test_ctrl_d_ignores_non_episode_rows() -> None:
    frame = _frame()
    frame._selected_action_rows = lambda: [
        Row(item_id="private-podcast", title="Podcast", kind="podcast")
    ]
    gui.LiteFrame._download_podcast_episodes(frame)

    assert frame.calls == []
    assert frame.spoken == ["Zaznacz co najmniej jeden odcinek podcastu"]


def test_single_download_failure_uses_user_facing_reason() -> None:
    frame = _frame(result={
        "requested": 1,
        "downloaded": 0,
        "alreadyDownloaded": 0,
        "failed": 1,
        "firstFailure": "Serwer nie zwrócił danych odcinka.",
    })
    first = frame._selected_action_rows()[0]
    frame._selected_action_rows = lambda: [first]
    gui.LiteFrame._download_podcast_episodes(frame)

    assert frame.spoken[-1] == (
        "Nie można pobrać odcinka: Serwer nie zwrócił danych odcinka."
    )


def test_progress_is_visible_in_status_without_interrupting_nvda() -> None:
    spoken: list[str] = []
    statuses: list[str] = []
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        _podcast_download_pending=True,
        announcer=SimpleNamespace(say=spoken.append),
        status_field=SimpleNamespace(SetLabel=statuses.append),
        status_bar=SimpleNamespace(show=statuses.append),
    )

    gui.LiteFrame._handle_engine_event(frame, "podcast.downloadProgress", {
        "current": 2,
        "total": 3,
        "title": "Rozmowa tygodnia",
        "percent": 47,
    })

    assert statuses == [
        "Pobieranie 2 z 3: 47%. Rozmowa tygodnia",
        "Pobieranie 2 z 3: 47%. Rozmowa tygodnia",
    ]
    assert spoken == []


def test_ctrl_s_uses_suggested_name_and_speaks_only_the_file_name() -> None:
    row = Row(item_id="private-episode-1", title="Pierwszy odcinek", kind="episode")
    calls: list[tuple] = []
    spoken: list[str] = []
    statuses: list[str] = []

    def info(episode_id):
        calls.append(("info", episode_id))
        return {
            "title": "Pierwszy odcinek",
            "suggestedFileName": "Pierwszy odcinek.mp3",
            "initialFolder": "D:/Pobrane",
        }

    def save(episode_id, path):
        calls.append(("save", episode_id, path))
        return {"fileName": "Wybrana kopia.mp3", "bytesWritten": 123}

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.PODCASTS),
        client=SimpleNamespace(
            podcast_save_as_info=info,
            save_podcast_episode_as=save,
        ),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_field=SimpleNamespace(SetLabel=statuses.append),
        status_bar=SimpleNamespace(show=statuses.append),
        _podcast_download_pending=False,
        _selected_action_rows=lambda: [row],
    )

    class Picker:
        def __init__(self, _parent, **kwargs):
            calls.append(("dialog", kwargs["defaultDir"], kwargs["defaultFile"]))

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):  # noqa: N802 - API wx
            return gui.wx.ID_OK

        def GetPath(self):  # noqa: N802 - API wx
            return "D:/Inny folder/Wybrana kopia.mp3"

    original = gui.wx.FileDialog
    gui.wx.FileDialog = Picker
    gui.wx.FD_SAVE = 1
    gui.wx.FD_OVERWRITE_PROMPT = 2
    try:
        gui.LiteFrame._save_podcast_episode_as(frame)
    finally:
        gui.wx.FileDialog = original

    assert calls == [
        ("info", "private-episode-1"),
        ("dialog", "D:/Pobrane", "Pierwszy odcinek.mp3"),
        (
            "save",
            "private-episode-1",
            "D:/Inny folder/Wybrana kopia.mp3",
        ),
    ]
    assert frame._podcast_download_pending is False
    assert spoken[-1] == "Zapisano odcinek jako: Wybrana kopia.mp3"
    exposed = " ".join(spoken + statuses)
    assert "private-episode" not in exposed
    assert "D:/Inny folder" not in exposed
