"""Apple Music membership keeps focus and reports only confirmed outcomes."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite import i18n
from amc_wx_lite.apple_music_library import AppleMusicLibraryMutation
from amc_wx_lite.apple_music_source import records_from_resources
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Navigator, OpenAppleMusicView, SessionId


class ImmediateRunner:
    def __init__(self) -> None:
        self.keys: list[str] = []

    def submit(self, key, work, done, failed) -> None:
        self.keys.append(key)
        try:
            done(work())
        except Exception as error:  # pragma: no cover - failure asserted elsewhere
            failed(error)


class LibraryContext:
    def __init__(self, result: AppleMusicLibraryMutation) -> None:
        self.result = result
        self.records = []

    def __enter__(self):
        return self

    def __exit__(self, *_args) -> None:
        return None

    def toggle_library(self, record):
        self.records.append(record)
        return self.result


def _record(resource_type: str, identifier: str):
    return records_from_resources([{
        "id": identifier,
        "type": resource_type,
        "attributes": {
            "name": "Wybrany materiał",
            "artistName": "Wykonawca",
            "playParams": {"catalogId": identifier.removeprefix("library.")},
        },
    }])[0]


def test_remove_refreshes_same_view_with_neighbor_and_speaks_last(monkeypatch) -> None:
    removed = _record("library-songs", "library.song")
    next_row = Row("appleMusic:library-songs:next", "Następny", "track")
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.apply_apple_music_view(
        OpenAppleMusicView("songs"),
        "Utwory Apple Music",
        [removed.row, next_row],
    )
    source = LibraryContext(AppleMusicLibraryMutation(False, "track"))
    messages: list[str] = []
    refreshed: list[tuple[OpenAppleMusicView, str | None]] = []
    frame = SimpleNamespace(
        _apple_music_library_pending=False,
        _apple_music_pages={"songs": ([removed], None)},
        _apple_music_records={removed.row.item_id: removed},
        _selected_action_rows=lambda: [removed.row],
        navigator=navigator,
        layout=object(),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=messages.append),
    )

    def refresh(intent, *, finished_message=None, finished=None):
        refreshed.append((intent, finished_message))
        if finished_message:
            messages.append(finished_message)
        if finished is not None:
            finished()

    frame._open_apple_music_view = refresh
    monkeypatch.setattr(gui, "open_apple_music_library", lambda _layout: source)

    gui.LiteFrame._toggle_apple_music_library(frame)

    assert source.records == [removed]
    assert frame.runner.keys == ["apple-music-library-membership"]
    assert refreshed == [(
        OpenAppleMusicView("songs", preferred_id=next_row.item_id),
        "Usunięto utwór z Biblioteki Apple Music",
    )]
    assert removed.row.item_id not in frame._apple_music_records
    assert messages[-1] == "Usunięto utwór z Biblioteki Apple Music"
    assert frame._apple_music_library_pending is False


def test_add_catalog_album_reports_confirmation_without_fake_removal(monkeypatch) -> None:
    album = _record("albums", "catalog.album")
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.session.apple_music_context = "playlist:example"
    navigator.session.model.replace([album.row])
    source = LibraryContext(AppleMusicLibraryMutation(True, "album"))
    messages: list[str] = []
    frame = SimpleNamespace(
        _apple_music_library_pending=False,
        _apple_music_pages={},
        _apple_music_records={album.row.item_id: album},
        _selected_action_rows=lambda: [album.row],
        navigator=navigator,
        layout=object(),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=messages.append),
    )
    monkeypatch.setattr(gui, "open_apple_music_library", lambda _layout: source)

    gui.LiteFrame._toggle_apple_music_library(frame)

    assert source.records == [album]
    assert messages == ["Dodano album do Biblioteki Apple Music"]
    assert album.row.item_id in frame._apple_music_records
    assert frame._apple_music_library_pending is False


def test_membership_messages_have_intentional_english_labels() -> None:
    try:
        i18n.configure("en")
        assert i18n.ui_text(
            "Dodano utwór do Biblioteki Apple Music"
        ) == "Added the song to Apple Music Library"
        assert i18n.ui_text(
            "Usunięto album z Biblioteki Apple Music"
        ) == "Removed the album from Apple Music Library"
        assert i18n.ui_text(
            "Usuwanie z Biblioteki Apple Music jest dostępne na liście Utwory albo Albumy"
        ) == "Removal from Apple Music Library is available in the Songs or Albums list"
    finally:
        i18n.configure("pl")
