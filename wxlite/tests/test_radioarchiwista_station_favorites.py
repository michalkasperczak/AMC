"""Station favorites share the Radio Archiwista shortcut with recordings."""

from __future__ import annotations

from types import SimpleNamespace

import pytest

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, i18n, menu_model, radioarchiwista_source as rarch
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId
from amc_wx_lite.shortcuts import Action, RADIOARCHIWISTA_SUPPORTED_ACTIONS, describe


def test_station_and_legacy_recording_favorites_remain_separate(tmp_path) -> None:
    source = rarch.RadioarchiwistaSource(tmp_path / "profile")
    recording = rarch.ArchiveRecording(
        station_slug="pr1", station_name="Program 1",
        item_id="rarch:rec:pr1:2026-11-22T12-00-00+00-00",
        title="13:00", audio_url="https://example.test/hour.mp3",
        duration_seconds=3600, local_date_label="22.11.2026",
        local_hour_label="13:00", local_label="22.11.2026 13:00",
        utc_started_at="2026-11-22T12:00:00+00:00", hour=13,
    )
    assert source.toggle_favorite(recording)
    assert source.toggle_station_favorite("pr1", "Program 1")
    saved = source.load_favorites()
    assert len(saved) == 2
    assert source.favorite_ids() == {recording.item_id, "rarch:station:pr1"}
    assert source.recording_by_id("rarch:station:pr1") is None
    rows = rarch.favorite_rows(saved)
    assert {row.kind for row in rows} == {"folder", "recording"}
    station = next(row for row in rows if row.kind == "folder")
    assert station.item_id == "rarch:station:pr1"
    assert station.title == "Program 1"
    assert station.service_kind == "rarchStation" and station.service_id == "pr1"
    assert source.toggle_station_favorite("pr1", "Program 1") is False
    assert [row.item_id for row in rarch.favorite_rows(source.load_favorites())] == [recording.item_id]
    with pytest.raises(rarch.RadioarchiwistaError):
        source.toggle_station_favorite("../outside", "Błędna stacja")


def test_station_row_is_marked_without_changing_spoken_name() -> None:
    station = rarch.StationRecord("pr1", "Program 1", "Polskie Radio Program 1", 120)
    index = rarch.StationsIndex((station,), catalog_version=2)
    row = rarch.station_rows(index, favorite_ids={"rarch:station:pr1"})[0]
    assert row.title == "Polskie Radio Program 1, 120 nagrań"
    assert row.service_title == "Polskie Radio Program 1"
    assert row.state_prefix == "★ "


def test_favorite_shortcut_accepts_station_and_keeps_recording_command() -> None:
    row = Row(
        "rarch:station:pr1", "Program 1, 120 nagrań", "folder",
        service_id="pr1", service_kind="rarchStation", service_title="Program 1",
    )
    nav = Navigator()
    nav.active = SessionId.RADIOARCHIWISTA
    nav.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_STATIONS, [row])
    spoken: list[str] = []
    calls: list[tuple[str, str]] = []
    favorite = False

    def toggle(slug: str, title: str) -> bool:
        nonlocal favorite
        calls.append((slug, title))
        favorite = not favorite
        return favorite

    source = SimpleNamespace(toggle_station_favorite=toggle)

    def submit(_slot, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        navigator=nav, announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(submit=submit), _radioarchiwista_source=lambda: source,
        _run=lambda effects: spoken.extend(effect.text for effect in effects),
    )
    gui.LiteFrame._toggle_radioarchiwista_favorite(frame)
    assert calls == [("pr1", "Program 1")]
    assert nav.session.model.selected_row.state_prefix == "★ "
    assert spoken == ["Dodano do ulubionych: Program 1"]

    nav.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_FAVORITES, [row])
    gui.LiteFrame._toggle_radioarchiwista_favorite(frame)
    assert calls == [("pr1", "Program 1"), ("pr1", "Program 1")]
    assert nav.session.model.rows == []
    assert spoken[-1] == "Usunięto z ulubionych: Program 1"


def test_favorite_command_surfaces_name_both_target_kinds() -> None:
    menu = menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
    entry = next(item for group in menu for item in group.items
                 if item.action is Action.TOGGLE_RADIOARCHIWISTA_FAVORITE)
    assert entry.shortcut == "Ctrl+Shift+U"
    assert entry.needs_radioarchiwista_favorite_target
    assert "stacji lub nagrania" in entry.label
    help_rows = describe("Radio Archiwista", set(RADIOARCHIWISTA_SUPPORTED_ACTIONS))
    assert any(key == "Ctrl+Shift+U" and "stację albo nagranie" in text
               for key, text in help_rows)

    station = Row("rarch:station:pr1", "Program 1", "folder",
                  service_kind="rarchStation", service_id="pr1")
    recording = Row("rarch:rec:pr1:one", "13:00", "recording",
                    url="https://example.test/hour.mp3")
    year = Row("rarch:years:pr1:2026", "2026", "folder",
               service_kind="rarchYear", service_id="pr1")
    assert menu_model.radioarchiwista_favorite_target(station)
    assert menu_model.radioarchiwista_favorite_target(recording)
    assert not menu_model.radioarchiwista_favorite_target(year)
    assert not menu_model.radioarchiwista_favorite_target(None)

    try:
        i18n.configure("en")
        english_menu = menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
        english_item = next(item for group in english_menu for item in group.items
                            if item.action is Action.TOGGLE_RADIOARCHIWISTA_FAVORITE)
        english_palette = next(item for item in entries_for_session(SessionId.RADIOARCHIWISTA)
                               if item.action is Action.TOGGLE_RADIOARCHIWISTA_FAVORITE)
        assert "station or recording" in english_item.label
        assert "station or recording" in english_palette.name
        assert english_item.shortcut == english_palette.shortcut == "Ctrl+Shift+U"
    finally:
        i18n.configure("pl")
