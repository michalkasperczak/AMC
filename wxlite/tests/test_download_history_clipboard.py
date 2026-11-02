"""Schowek listy pobrań: ścieżka tekstowa i plik do wklejenia."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from test_ctrl_shift_c_copies_a_real_file import (
    FakeClipboard, FakeComposite, FakeFileData, FakeTextData, make_frame,
)
from amc_wx_lite import gui
from amc_wx_lite.download_history import DownloadEntry, download_rows
from amc_wx_lite.navigation import LibraryView, SessionId, View
from amc_wx_lite.shortcuts import Action, describe, keyboard_help_announcement, chord_from_canonical


def _select_download(frame, entry: DownloadEntry) -> None:
    frame.navigator = SimpleNamespace(
        active=SessionId.FILES,
        session=SimpleNamespace(
            view=View.LIST,
            library_view=LibraryView.DOWNLOADED_FILES,
            model=SimpleNamespace(selected_row=download_rows([entry])[0]),
        ),
    )
    frame._download_history_entries = {entry.item_id: entry}


def _make_frame(clipboard: FakeClipboard):
    frame, spoken = make_frame(clipboard)
    # Inne moduły testowe mogą podmienić sys.modules['wx']; LiteFrame trzyma
    # odniesienie do wx z chwili importu, więc ustawiamy także ten moduł.
    gui.wx.TheClipboard = clipboard
    gui.wx.TextDataObject = FakeTextData
    gui.wx.FileDataObject = FakeFileData
    gui.wx.DataObjectComposite = FakeComposite
    return frame, spoken


def test_ctrl_c_copies_full_path_as_text_without_file_drop(tmp_path: Path) -> None:
    path = tmp_path / "audycja.mp3"
    path.write_bytes(b"audio")
    entry = DownloadEntry(str(path), "Audycja", "archive", 1)
    clipboard = FakeClipboard()
    frame, spoken = _make_frame(clipboard)
    _select_download(frame, entry)

    frame._copy_name()

    assert isinstance(clipboard.data, FakeTextData)
    assert clipboard.data.text == str(path)
    assert spoken == ["Skopiowano pełną ścieżkę"]


def test_ctrl_shift_c_copies_real_download_file(tmp_path: Path) -> None:
    path = tmp_path / "film.mp4"
    path.write_bytes(b"video")
    entry = DownloadEntry(str(path), "Film", "podcast_video", 1)
    clipboard = FakeClipboard()
    frame, spoken = _make_frame(clipboard)
    _select_download(frame, entry)

    frame._copy_address()

    assert isinstance(clipboard.data, FakeComposite)
    file_data = next(part for part, _ in clipboard.data.parts
                     if isinstance(part, FakeFileData))
    text_data = next(part for part, _ in clipboard.data.parts
                     if isinstance(part, FakeTextData))
    assert file_data.files == [str(path)]
    assert text_data.text == str(path)
    assert spoken == ["Skopiowano plik i pełną ścieżkę"]


def test_missing_download_allows_old_path_but_refuses_file_drop(tmp_path: Path) -> None:
    entry = DownloadEntry(str(tmp_path / "brak.mp3"), "Brak", "archive", 1)
    clipboard = FakeClipboard()
    frame, spoken = _make_frame(clipboard)
    _select_download(frame, entry)

    frame._copy_name()
    assert isinstance(clipboard.data, FakeTextData)
    assert clipboard.data.text == entry.path
    frame._copy_address()
    assert isinstance(clipboard.data, FakeTextData)
    assert spoken[-1] == "Plik niedostępny. Ctrl+C skopiuje zapisaną ścieżkę"


def test_help_describes_contextual_clipboard_commands() -> None:
    entries = dict(describe("Pliki lokalne", downloaded_files=True))
    assert entries["Ctrl+C"] == "Skopiuj pełną ścieżkę pobranego pliku"
    assert entries["Ctrl+Shift+C"] == "Skopiuj pobrany plik do wklejenia"
    assert "pełną ścieżkę" in keyboard_help_announcement(
        chord_from_canonical("Ctrl+C"), Action.COPY_NAME,
        "Pliki lokalne", downloaded_files=True,
    )
    assert dict(describe("Pliki lokalne"))["Ctrl+C"] == "Skopiuj nazwe"
