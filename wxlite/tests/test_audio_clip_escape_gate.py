"""Escape during an audio edit leaves the edit operation alive."""

from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import SessionId, View
from amc_wx_lite.shortcuts import Action, Chord, resolve


def test_escape_returns_to_list_without_cancelling_audio_edit() -> None:
    events: list[tuple[str, object]] = []

    class Navigator:
        active = SessionId.FILES

        def __init__(self) -> None:
            self.session = SimpleNamespace(view=View.PLAYER)

        def back_to_list(self, *, follow_playback: bool) -> str:
            events.append(("back", follow_playback))
            self.session.view = View.LIST
            return "returned"

    frame = SimpleNamespace(
        navigator=Navigator(),
        options=SimpleNamespace(follow_playback_on_player_exit=False),
        _audio_clip_edit_in_progress=True,
        _pause_on_player_exit_if_needed=lambda session: events.append(("pause", session)),
        _run=lambda intent: events.append(("run", intent)),
        client=SimpleNamespace(
            cancel_audio_clip=lambda operation_id: events.append(("cancel", operation_id))
        ),
    )

    assert resolve(Chord("Escape"), player_view=True, radio_session=False) is Action.SHOW_LIST
    LiteFrame._leave_player_to_list(frame)
    assert frame._audio_clip_edit_in_progress
    assert frame.navigator.session.view is View.LIST
    assert not any(event[0] == "cancel" for event in events)


if __name__ == "__main__":
    test_escape_returns_to_list_without_cancelling_audio_edit()
    print("OK: Escape wraca na listę podczas cięcia bez anulowania operacji")
