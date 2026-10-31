"""Pobieranie wycinka Radio Archiwisty bez naruszania nagrania źródłowego."""

from __future__ import annotations

import io
from pathlib import Path

import pytest

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.radioarchiwista_source import (
    ArchiveRecording, RadioarchiwistaError, download_fragment,
    fragment_destination, fragment_range, parse_fragment_time,
)
from amc_wx_lite.shortcuts import Action, chord_from_canonical, describe, resolve


def recording() -> ArchiveRecording:
    return ArchiveRecording(
        station_slug="radio", station_name="Radio", item_id="rarch:rec:radio:1",
        title="Godzina", audio_url="https://archive.org/download/godzina.mp3",
        duration_seconds=3600, local_date_label="31.10.2026",
        local_hour_label="12:00", local_label="31.10.2026 12:00",
        utc_started_at="2026-10-31T11:00:00Z", hour=12,
    )


class FakeResponse(io.BytesIO):
    status = 200

    def __init__(self, content: bytes) -> None:
        super().__init__(content)
        self.headers = {"Content-Length": str(len(content))}


def test_time_and_range_validation() -> None:
    assert parse_fragment_time("12:30") == 750
    assert parse_fragment_time("1:02:03") == 3723
    assert fragment_range(recording(), "12:30", "15:00") == (750, 900)
    with pytest.raises(RadioarchiwistaError, match="późniejszy"):
        fragment_range(recording(), "15:00", "12:30")
    with pytest.raises(RadioarchiwistaError, match="poza czas"):
        fragment_range(recording(), "0:00", "61:00")
    with pytest.raises(RadioarchiwistaError, match="minuty:sekundy"):
        parse_fragment_time("15")


def test_fragment_download_uses_private_temporary_source_and_new_output(tmp_path: Path) -> None:
    destination = fragment_destination(tmp_path, recording(), 750, 900)
    calls: list[tuple[str, str, int, int]] = []

    def export(source: str, target: str, start: int, end: int) -> None:
        assert Path(source).read_bytes() == b"MP3 source"
        calls.append((source, target, start, end))
        Path(target).write_bytes(b"MP3 fragment")

    result = download_fragment(
        recording(), destination, 750, 900,
        export=export, fetch=lambda _url: FakeResponse(b"MP3 source"),
    )

    assert result == destination
    assert result.read_bytes() == b"MP3 fragment"
    assert (calls[0][2], calls[0][3]) == (750, 900)
    assert not Path(calls[0][0]).exists()
    assert "od-12-30 do-15-00" in result.name
    assert fragment_destination(tmp_path, recording(), 750, 900) != result


def test_failure_removes_only_new_partial_output(tmp_path: Path) -> None:
    destination = tmp_path / "fragment.mp3"

    def fail(_source: str, target: str, _start: int, _end: int) -> None:
        Path(target).write_bytes(b"partial")
        raise RuntimeError("export failed")

    with pytest.raises(RuntimeError, match="export failed"):
        download_fragment(
            recording(), destination, 0, 60, export=fail,
            fetch=lambda _url: FakeResponse(b"MP3 source"),
        )
    assert not destination.exists()
    assert list(tmp_path.iterdir()) == []


def test_existing_destination_is_preserved(tmp_path: Path) -> None:
    destination = tmp_path / "important.mp3"
    destination.write_bytes(b"user file")
    with pytest.raises(RadioarchiwistaError, match="już istnieje"):
        download_fragment(
            recording(), destination, 0, 60,
            export=lambda *_: None, fetch=lambda _url: FakeResponse(b"source"),
        )
    assert destination.read_bytes() == b"user file"


def test_empty_export_is_removed(tmp_path: Path) -> None:
    destination = tmp_path / "fragment.mp3"
    with pytest.raises(RadioarchiwistaError, match="Nie udało się zapisać"):
        download_fragment(
            recording(), destination, 0, 60,
            export=lambda _source, target, *_: Path(target).write_bytes(b""),
            fetch=lambda _url: FakeResponse(b"source"),
        )
    assert list(tmp_path.iterdir()) == []


def test_command_is_consistent_in_keyboard_menu_palette_and_help() -> None:
    action = Action.RADIOARCHIWISTA_DOWNLOAD_FRAGMENT
    for player in (False, True):
        assert resolve(
            chord_from_canonical("Ctrl+Shift+D"), player_view=player,
            radio_session=False, radioarchiwista_session=True,
        ) is action
    assert resolve(
        chord_from_canonical("Ctrl+Shift+D"), player_view=False,
        radio_session=False, radioarchiwista_session=False,
        podcast_session=True,
    ) is Action.DOWNLOAD_YOUTUBE_VIDEOS
    assert any(item.action is action and item.shortcut == "Ctrl+Shift+D"
               for menu in menus_for_session(SessionId.RADIOARCHIWISTA)
               for item in menu.items)
    assert any(item.action is action
               for item in entries_for_session(SessionId.RADIOARCHIWISTA))
    assert "fragment" in dict(describe("Radio Archiwista"))["Ctrl+Shift+D"].lower()
