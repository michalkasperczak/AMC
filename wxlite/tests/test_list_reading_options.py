"""Odczyt listy: dziedziczenie, brak sondy przy nawigacji i zapis."""

from __future__ import annotations

from amc_wx_lite import list_reading
from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.list_sync import model_row_texts
from amc_wx_lite.state_store import LiteState, Options, StateStore
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.speaker_content import speaker_information_plan
from amc_wx_lite.quick_info import ordered_quick_info_parts


def test_updown_uses_existing_row_and_keeps_title() -> None:
    row = Row(
        "track", "Utwór", "track", detail="Wykonawca, 3:20, ulubione",
        artist_name="Wykonawca", duration_ticks=200 * 10_000_000,
        playback_state="Odtwarzane",
    )
    model = ListModel()
    model.replace([row])
    only_time = model_row_texts(model, reading_fields=("duration",))
    assert only_time[0].texts == ("Utwór", "3:20", "")
    full = model_row_texts(model, reading_fields=list_reading.UPDOWN_FIELDS)
    assert full[0].texts == ("Utwór", "utwór", "Wykonawca, 3:20, ulubione, Odtwarzane")

    reordered = model_row_texts(model, reading_fields=("duration", "artist", "kind"))
    assert reordered[0].texts == ("Utwór", "3:20", "Wykonawca, utwór")


def test_new_list_formats_each_row_once_for_three_columns(monkeypatch) -> None:
    rows = [Row(str(index), f"Utwór {index}", "track", detail="Wykonawca, 3:20")
            for index in range(200)]
    model = ListModel()
    model.replace(rows)
    original = list_reading.row_columns
    calls = 0

    def counted(row, fields):
        nonlocal calls
        calls += 1
        return original(row, fields)

    monkeypatch.setattr(list_reading, "row_columns", counted)
    model_row_texts(model, reading_fields=("artist", "duration", "other"))
    assert calls == len(rows)


def test_session_override_and_global_are_independent(tmp_path) -> None:
    state = LiteState(options=Options())
    state.options.list_updown_fields = ["duration"]
    state.options.list_left_fields = ["duration", "bitrate", "size"]
    state.list_reading_overrides["files"] = {
        "list_updown_fields": ["artist"],
        "list_left_fields": ["size", "duration", "unknown"],
    }
    store = StateStore(tmp_path)
    loaded = store.load_payload(store.to_payload(state))
    assert list_reading.session_fields(loaded.options, loaded.list_reading_overrides.get("files"), "updown", session="files") == ("artist",)
    assert list_reading.session_fields(loaded.options, loaded.list_reading_overrides.get("files"), "left", session="files") == ("size", "duration")
    assert list_reading.session_fields(loaded.options, loaded.list_reading_overrides.get("tidal"), "left", session="tidal") == ("duration",)
    assert loaded.options.list_updown_fields == ["duration"]


def test_global_order_survives_save_and_filters_unknown_duplicate_fields(tmp_path) -> None:
    state = LiteState(options=Options())
    state.options.list_updown_fields = ["duration", "kind", "duration", "unknown"]
    state.options.list_left_fields = ["size", "bitrate", "duration", "size"]
    store = StateStore(tmp_path)
    loaded = store.load_payload(store.to_payload(state))
    assert loaded.options.list_updown_fields == ["duration", "kind"]
    assert loaded.options.list_left_fields == ["size", "bitrate", "duration"]


def test_capabilities_hide_unavailable_fields() -> None:
    assert "size" not in list_reading.available_left_fields("spotify")
    assert "bitrate" in list_reading.available_left_fields("sonos")
    assert list_reading.available_left_fields("unknown-future-session") == ()
    assert set(list_reading.LEFT_CAPABILITIES) == {session.value for session in SessionId}


def test_speaker_other_only_does_not_probe_stream() -> None:
    row = Row("one", "Stacja", "station", url="https://example.invalid/radio", detail="Ulubione")
    plan = speaker_information_plan(row, session=SessionId.SONOS, view="list", fields=("other",))
    assert plan.request is None
    assert plan.message and "Ulubione" in plan.message
    measured = speaker_information_plan(row, session=SessionId.WIIM, view="list", fields=("bitrate", "format"))
    assert measured.request is not None
    assert measured.request["fields"] == ["bitrate", "format"]


def test_left_arrow_merges_measured_and_local_values_in_selected_order() -> None:
    response = {"text": "3:20, 128 kb/s", "parts": [
        {"field": "duration", "text": "3:20"},
        {"field": "bitrate", "text": "128 kb/s"},
    ]}
    assert ordered_quick_info_parts(
        response, ("bitrate", "other", "duration"), "folder Muzyka"
    ) == ["128 kb/s", "folder Muzyka", "3:20"]
