"""Opcje Escape, fokusu, Entera Radia i otwierania presetow."""

from __future__ import annotations

import tempfile
from pathlib import Path

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, PlayTrack, SessionId, View
from amc_wx_lite.state_store import LiteState, Options, StateStore


def _track(item_id: str) -> Row:
    return Row(item_id=item_id, title=item_id, kind="track", path=f"C:\\{item_id}.mp3")


def _station(item_id: str) -> Row:
    return Row(
        item_id=item_id,
        title=item_id,
        kind="station",
        url=f"https://{item_id}.invalid",
        show_kind=False,
    )


def test_escape_can_return_to_entry_focus_instead_of_following_page_down() -> None:
    nav = Navigator()
    nav.apply_folder("C:\\", [_track("a"), _track("b"), _track("c")], preferred_id="b")
    nav.activate_selected()
    nav.step_playback_source(True)

    nav.back_to_list(follow_playback=False)

    assert nav.session.model.selected_id == "b"


def test_escape_can_follow_item_selected_in_player() -> None:
    nav = Navigator()
    nav.apply_folder("C:\\", [_track("a"), _track("b"), _track("c")], preferred_id="b")
    nav.activate_selected()
    nav.step_playback_source(True)

    nav.back_to_list(follow_playback=True)

    assert nav.session.model.selected_id == "c"


def test_radio_enter_can_play_without_leaving_the_list() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    nav.apply_radio_view(None, "Radio", [_station("r1")], preferred_id="r1")

    nav.activate_selected(stay_on_list_after_radio_enter=True)

    assert nav.session.view is View.LIST
    assert nav.session.now_playing_id == "r1"


def test_radio_enter_opens_player_when_option_is_off() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    nav.apply_radio_view(None, "Radio", [_station("r1")], preferred_id="r1")

    nav.activate_selected(stay_on_list_after_radio_enter=False)

    assert nav.session.view is View.PLAYER


def test_radio_preset_can_play_in_background_and_keep_focus() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    nav.apply_radio_view(
        LibraryView.FAVORITES, "Ulubione", [_station("visible")], preferred_id="visible"
    )
    sequence = (_station("preset1"), _station("preset2"))

    nav.activate_radio_preset(sequence[1], sequence, open_player=False)

    assert nav.session.view is View.LIST
    assert nav.session.model.selected_id == "visible"
    assert nav.session.now_playing_id == "preset2"
    assert nav.session.playback_source_rows == sequence


def test_local_file_preset_can_play_in_background_and_keep_focus() -> None:
    nav = Navigator()
    visible = _track("visible")
    target = _track("preset")
    nav.apply_folder("C:\\", [visible], preferred_id="visible")

    intents = nav.activate_local_preset(target, open_player=False)

    assert nav.session.view is View.LIST
    assert nav.session.model.selected_id == "visible"
    assert nav.session.now_playing_id == "preset"
    assert nav.session.playback_source_rows == (target,)
    assert isinstance(intents[0], PlayTrack)
    assert intents[0].path == target.path


def test_new_options_round_trip_and_reject_non_boolean_values() -> None:
    directory = Path(tempfile.mkdtemp(prefix="amc-options-"))
    store = StateStore(directory)
    options = Options(
        pause_on_player_exit=False,
        follow_playback_on_player_exit=False,
        open_player_when_activating_preset=True,
        stay_on_list_after_radio_enter=True,
    )
    store.save(LiteState(options=options))

    loaded = store.load().options

    assert loaded.pause_on_player_exit is False
    assert loaded.follow_playback_on_player_exit is False
    assert loaded.open_player_when_activating_preset is True
    assert loaded.stay_on_list_after_radio_enter is True

    invalid = Options()
    invalid.follow_playback_on_player_exit = "nie"  # type: ignore[assignment]
    invalid.open_player_when_activating_preset = 1  # type: ignore[assignment]
    invalid.stay_on_list_after_radio_enter = None  # type: ignore[assignment]
    invalid.clamp()
    assert invalid.follow_playback_on_player_exit is True
    assert invalid.open_player_when_activating_preset is False
    assert invalid.stay_on_list_after_radio_enter is False
