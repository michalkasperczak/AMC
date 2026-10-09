"""Alt+Shift+Enter prowadzi od wiersza do jedynego właściciela bazy C#."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import SessionId, View  # noqa: E402


def _run(*, row: Row | None, player: bool, modal_result: int):
    model = ListModel()
    if row is not None:
        model.replace([row])
    state = SimpleNamespace(
        model=model,
        view=View.PLAYER if player else View.LIST,
        playback_source_rows=[] if player else list(model.rows),
        now_playing_id=row.item_id if player and row is not None else None,
        now_playing_title=row.title if player and row is not None else None,
    )
    read_calls: list[tuple[str, str]] = []
    write_calls: list[tuple[str, str, dict]] = []
    spoken: list[str] = []
    shown: list[dict] = []
    focus: list[str] = []

    class Client:
        def podcast_playback_options(self, item_id, target):
            read_calls.append((item_id, target))
            return {
                "target": target,
                "itemId": item_id,
                "title": row.title if row is not None else "Odcinek bez nazwy",
                "resumePositionMode": 0,
            }

        def set_podcast_playback_options(self, item_id, target, values):
            write_calls.append((item_id, target, dict(values)))
            return {
                "options": {"title": row.title if row is not None else "Odcinek"},
                "appliesOnNextPlayback": True,
            }

    class Dialog:
        def __init__(self, _parent, options):
            shown.append(dict(options))
            self.values = {
                "resumePositionMode": 1,
                "playbackRate": 1.0,
                "loudnessNormalization": None,
                "smoothTrackTransitions": None,
                "interTrackSilenceMs": None,
                "tempoAlgorithm": None,
            }

        def ShowModal(self):  # noqa: N802 - API wx
            return modal_result

        def Destroy(self):  # noqa: N802 - API wx
            pass

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
        client=Client(),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        status_field=SimpleNamespace(SetLabel=lambda _text: None),
        status_bar=SimpleNamespace(show=lambda _text: None),
        _podcast_options_pending=False,
        _restore_focus_after_dialog=lambda: focus.append("focus"),
    )
    original = gui.PodcastPlaybackOptionsDialog
    gui.PodcastPlaybackOptionsDialog = Dialog
    try:
        gui.LiteFrame._show_podcast_playback_options(frame)
    finally:
        gui.PodcastPlaybackOptionsDialog = original
    return SimpleNamespace(
        frame=frame,
        read=read_calls,
        written=write_calls,
        spoken=spoken,
        shown=shown,
        focus=focus,
    )


def test_podcast_options_read_and_write_keep_model_values_out_of_speech() -> None:
    result = _run(
        row=Row(item_id="private-podcast-id", title="Audycja tygodnia", kind="podcast"),
        player=False,
        modal_result=gui.wx.ID_OK,
    )

    assert result.read == [("private-podcast-id", "podcast")]
    assert result.shown[0]["title"] == "Audycja tygodnia"
    assert result.written[0][:2] == ("private-podcast-id", "podcast")
    assert result.written[0][2]["playbackRate"] == 1.0
    assert result.spoken == [
        "Zapisano opcje podcastu: Audycja tygodnia. "
        "Ustawienia toru dźwięku obowiązują od następnego otwarcia materiału"
    ]
    assert "private-podcast-id" not in result.spoken[0]
    assert result.focus == ["focus"]
    assert not result.frame._podcast_options_pending


def test_cancel_closes_options_without_writing() -> None:
    result = _run(
        row=Row(item_id="private-episode-id", title="Odcinek", kind="episode"),
        player=False,
        modal_result=gui.wx.ID_CANCEL,
    )

    assert result.read == [("private-episode-id", "episode")]
    assert result.written == []
    assert result.spoken == []
    assert result.focus == ["focus"]
    assert not result.frame._podcast_options_pending


def test_player_uses_current_episode_even_without_a_source_row() -> None:
    result = _run(
        row=Row(item_id="current-private-id", title="Bieżący odcinek", kind="episode"),
        player=True,
        modal_result=gui.wx.ID_CANCEL,
    )

    assert result.read == [("current-private-id", "episode")]
    assert result.shown[0]["title"] == "Bieżący odcinek"
