"""Regresje po przeglądzie integracji Radio Archiwisty w AMC Test."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

import pytest

from amc_wx_lite import menu_model, radioarchiwista_source as rarch
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, SessionId
from amc_wx_lite.shortcuts import (
    Action,
    RADIOARCHIWISTA_SUPPORTED_ACTIONS,
    ctrl_f5_presentation,
    describe,
)


def test_invalid_station_slug_is_ignored_before_navigation() -> None:
    index = rarch.parse_stations_index({
        "catalog_version": 2,
        "stations": [
            {"slug": "../outside", "display_name": "Bad station"},
            {"slug": "pr1", "display_name": "Program 1"},
        ],
    })
    assert [row.service_id for row in rarch.station_rows(index)] == ["pr1"]


def test_download_name_is_unique_and_existing_recording_is_preserved(tmp_path: Path) -> None:
    recording = rarch.ArchiveRecording(
        item_id="rarch:rec:pr1:2026-10-29T14:00:00+00:00",
        station_slug="pr1",
        station_name="Program 1",
        title="15:00",
        audio_url="https://archive.org/a.mp3",
        duration_seconds=3600,
        local_date_label="29.10.2026",
        local_hour_label="15:00",
        local_label="29.10.2026 15:00 czasu polskiego",
        utc_started_at="2026-10-29T14:00:00+00:00",
        completeness_ratio=1.0,
        status="published",
        hour=15,
    )
    first = rarch.download_destination(tmp_path, recording)
    first.write_bytes(b"user edited audio")
    second = rarch.download_destination(tmp_path, recording)
    assert second != first
    assert second.name.endswith("-2.mp3")
    with pytest.raises(rarch.RadioarchiwistaError, match="istnieje"):
        rarch.stream_to_file(recording.audio_url, first, fetch=lambda _: None)
    assert first.read_bytes() == b"user edited audio"


def test_catalog_address_requires_https_and_is_normalized(tmp_path: Path) -> None:
    source = rarch.RadioarchiwistaSource(tmp_path)
    with pytest.raises(rarch.RadioarchiwistaError):
        source.set_catalog_base("http://example.com/")
    with pytest.raises(rarch.RadioarchiwistaError):
        source.set_catalog_base("https://user:pass@example.com/")
    source.set_catalog_base("https://example.com/archive")
    assert source.catalog_base() == "https://example.com/archive/"


def test_latest_refresh_returns_to_latest_with_current_selection() -> None:
    row = Row(item_id="rarch:rec:pr1:a", title="15:00", kind="recording")
    state = SimpleNamespace(
        library_view=LibraryView.RADIOARCHIWISTA_HOURS,
        radioarchiwista_slug="",
        model=SimpleNamespace(selected_id=row.item_id),
    )
    calls: list[tuple[bool, str | None]] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions={SessionId.RADIOARCHIWISTA: state}),
        _open_radioarchiwista_latest=lambda **kw: calls.append(
            (kw["refresh"], kw["preferred_id"])
        ),
    )
    LiteFrame._refresh_radioarchiwista_view(frame)
    assert calls == [(True, row.item_id)]


def test_catalog_commands_match_menu_and_help() -> None:
    presentation = ctrl_f5_presentation("Radio Archiwista")
    assert presentation is not None
    assert presentation.action is Action.SESSION_CONFIGURE_CURRENT
    menus = menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
    entries = [item for menu in menus for item in menu.items if item.action]
    by_action = {item.action: item for item in entries}
    assert by_action[Action.SESSION_CONFIGURE_CURRENT].shortcut == "Ctrl+F5"
    assert by_action[Action.RADIOARCHIWISTA_DOWNLOAD].needs_radioarchiwista_recording
    assert by_action[Action.TOGGLE_RADIOARCHIWISTA_FAVORITE].needs_radioarchiwista_recording
    help_text = describe("Radio Archiwista", set(RADIOARCHIWISTA_SUPPORTED_ACTIONS))
    assert any(key == "F5" and "katalog" in value.lower() for key, value in help_text)
    assert any(key == "Ctrl+F5" and "katalog" in value.lower() for key, value in help_text)
    assert Action.RATE_UP in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert Action.SEEK_FORWARD_10 in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert Action.VIEW_PRESETS not in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert Action.FRONTIER_SMART_SELECT_MODE not in RADIOARCHIWISTA_SUPPORTED_ACTIONS
