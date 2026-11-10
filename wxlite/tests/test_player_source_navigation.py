"""Page Up/Page Down rozroznia liste zrodlowa od prawdziwej kolejki."""

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    PlayStation,
    PlayTrack,
    SessionId,
)


def _track(item_id: str) -> Row:
    return Row(
        item_id=item_id,
        title=f"Utwor {item_id}",
        kind="track",
        path=rf"C:\Muzyka\{item_id}.mp3",
    )


def _station(item_id: str) -> Row:
    return Row(
        item_id=item_id,
        title=f"Stacja {item_id}",
        kind="station",
        url=f"https://example.invalid/{item_id}",
    )


def test_page_down_from_normal_file_plays_next_source_item_not_queue() -> None:
    nav = Navigator()
    nav.apply_folder(r"C:\Muzyka", [_track("a"), _track("b"), _track("c")], preferred_id="b")
    nav.activate_selected()

    effects = nav.step_playback_source(True)

    assert effects is not None
    play = next(effect for effect in effects if isinstance(effect, PlayTrack))
    assert play.item_id == "c"
    assert nav.session.now_playing_id == "c"
    assert nav.session.list_anchor_id == "c"


def test_page_up_from_normal_file_plays_previous_source_item() -> None:
    nav = Navigator()
    nav.apply_folder(r"C:\Muzyka", [_track("a"), _track("b")], preferred_id="b")
    nav.activate_selected()

    effects = nav.step_playback_source(False)

    play = next(effect for effect in effects or [] if isinstance(effect, PlayTrack))
    assert play.item_id == "a"


def test_page_down_in_radio_plays_next_station_from_same_view() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    nav.apply_radio_view(
        LibraryView.FAVORITES,
        "Ulubione radia",
        [_station("r1"), _station("r2")],
        preferred_id="r1",
    )
    nav.activate_selected()

    effects = nav.step_playback_source(True)

    play = next(effect for effect in effects or [] if isinstance(effect, PlayStation))
    assert play.item_id == "r2"
    assert nav.session.library_view is LibraryView.FAVORITES


def test_end_of_source_names_source_list_not_queue() -> None:
    nav = Navigator()
    nav.apply_folder(r"C:\Muzyka", [_track("a")], preferred_id="a")
    nav.activate_selected()

    effects = nav.step_playback_source(True)

    spoken = [effect.text for effect in effects or [] if isinstance(effect, Announce)]
    assert spoken == ["To ostatni element listy źródłowej"]
    assert all("kolejk" not in text.casefold() for text in spoken)


def test_saved_queue_still_delegates_page_keys_to_host_queue() -> None:
    nav = Navigator()
    rows = [_track("q1"), _track("q2")]
    nav.apply_library_view(LibraryView.SAVED_QUEUE, "Kolejka", rows, preferred_id="q1")
    nav.activate_selected()

    assert nav.step_playback_source(True) is None
