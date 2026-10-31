"""Unified download history, playback, and command discovery."""

from __future__ import annotations

from pathlib import Path

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.download_history import (
    DownloadEntry,
    DownloadHistoryStore,
    download_information,
    download_rows,
    entries_from_archive_folder,
    entries_from_podcast_rows,
    merge_entries,
)
from amc_wx_lite.list_model import Row
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import Announce, LibraryView, Navigator, SessionId
from amc_wx_lite.shortcuts import (
    Action, _help_entries, chord_from_canonical, resolve,
)


def test_history_merges_old_audio_and_archive_with_new_video(tmp_path: Path) -> None:
    archive_folder = tmp_path / "archiwista"
    archive_folder.mkdir()
    archive = archive_folder / "audycja.mp3"
    archive.write_bytes(b"archive")
    podcast = tmp_path / "odcinek.mp3"
    podcast.write_bytes(b"podcast")
    video = tmp_path / "inny folder" / "film.mp4"
    video.parent.mkdir()
    video.write_bytes(b"video")

    store = DownloadHistoryStore(tmp_path / "profil")
    recorded = store.record(video, title="Film", source="podcast_video", finished_at=100)
    assert store.load() == [recorded]
    entries = merge_entries(
        store.load(),
        entries_from_podcast_rows([Row("episode:1", "Odcinek", "episode", path=str(podcast))]),
        entries_from_archive_folder(archive_folder),
    )
    assert {item.path for item in entries} == {str(archive), str(podcast), str(video)}
    assert {item.source for item in entries} == {
        "archive", "podcast_audio", "podcast_video"
    }
    assert all(row.path and row.kind == "track" for row in download_rows(entries))
    assert "inny folder" in download_information(recorded)


def test_missing_download_remains_visible_but_cannot_play(tmp_path: Path) -> None:
    missing = tmp_path / "usunięty.mp3"
    entry = DownloadEntry(str(missing), "Audycja", "archive", 0)
    row = download_rows([entry])[0]
    assert row.path is None
    assert row.title == "Brak pliku: usunięty.mp3"
    assert "Plik niedostępny" in download_information(entry)

    navigator = Navigator()
    navigator.active = SessionId.FILES
    navigator.apply_library_view(LibraryView.DOWNLOADED_FILES, "Pobrane pliki", [row])
    assert navigator.activate_selected() == [Announce(row.activation_message)]


def test_download_command_matches_keyboard_menu_palette_and_help() -> None:
    labels = {action: label for _, label, action in _help_entries()}
    assert "pobran" in labels[Action.VIEW_DOWNLOADED_FILES].lower()
    for session in SessionId:
        for player in (False, True):
            assert resolve(
                chord_from_canonical("Ctrl+J"),
                player_view=player,
                radio_session=session is SessionId.RADIO,
                podcast_session=session is SessionId.PODCASTS,
                spotify_session=session is SessionId.SPOTIFY,
                tidal_session=session is SessionId.TIDAL,
                wiim_session=session is SessionId.WIIM,
                sonos_session=session is SessionId.SONOS,
                frontier_smart_session=session is SessionId.FRONTIER_SMART,
                radioarchiwista_session=session is SessionId.RADIOARCHIWISTA,
            ) is Action.VIEW_DOWNLOADED_FILES
        assert any(
            item.action is Action.VIEW_DOWNLOADED_FILES and item.shortcut == "Ctrl+J"
            for menu in menus_for_session(session) for item in menu.items
        )
        assert any(
            item.action is Action.VIEW_DOWNLOADED_FILES
            for item in entries_for_session(session)
        )
