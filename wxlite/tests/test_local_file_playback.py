"""Plik spoza listy gra i AMC o tym wie.

Plik otwarty z Eksploratora, Total Commandera albo oknem „Otwórz plik” szedł do
silnika z pominięciem nawigatora. Grał, ale F6 mówił „Nic nie jest
odtwarzane”. Gdy wcześniej grało coś z listy, F6 otwierał tamten utwór, a Page
Down włączał następny plik z tamtego folderu.
"""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite import gui
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Announce, Navigator, PlayTrack, SessionId, View
from amc_wx_lite.shell_requests import ShellRequest

FOLDER = r"D:\Muzyka\Girih"


def _tracks(folder: str, *names: str) -> list[Row]:
    rows = []
    for name in names:
        path = f"{folder}\\{name}.flac"
        rows.append(Row(f"file:{path}", name, "track", path=path))
    return rows


def test_file_from_outside_is_current_for_f6_and_page_down() -> None:
    nav = Navigator()
    rows = _tracks(FOLDER, "01 Lustro", "02 Rama", "03 Drzwi")
    opened = rows[1]

    intents = nav.play_local_file(opened.path, "02 Rama")
    assert intents == [PlayTrack(opened.path, opened.item_id, "02 Rama")]
    # Odtwarzacz otwiera się dopiero na F6, jak dotąd.
    assert nav.session.view is View.LIST
    # Zanim folder się wczyta, Page Down nie ma dokąd iść, ale nie szuka
    # żywej kolejki hosta i nie skacze nigdzie indziej.
    assert nav.step_playback_source(True) == [
        Announce("To ostatni element listy źródłowej")
    ]

    nav.apply_folder(FOLDER, rows, preferred_id=opened.item_id)

    assert nav.show_player() == [Announce("Odtwarzacz, 02 Rama")]
    assert nav.session.view is View.PLAYER
    assert nav.step_playback_source(True)[0] == PlayTrack(
        rows[2].path, rows[2].item_id, rows[2].title
    )


def test_file_from_outside_replaces_track_played_earlier_from_the_list() -> None:
    nav = Navigator()
    old = _tracks(r"C:\Inne", "stary", "drugi")
    nav.apply_folder(r"C:\Inne", old, preferred_id=old[0].item_id)
    nav.activate_selected(open_player_on_activation=False)
    rows = _tracks(FOLDER, "01 Lustro", "02 Rama", "03 Drzwi")

    nav.play_local_file(rows[1].path, "02 Rama")
    nav.apply_folder(FOLDER, rows, preferred_id=rows[1].item_id)

    assert nav.show_player() == [Announce("Odtwarzacz, 02 Rama")]
    assert nav.step_playback_source(True)[0] == PlayTrack(
        rows[2].path, rows[2].item_id, rows[2].title
    )


def test_returning_to_folder_keeps_single_track_source_chosen_by_enter() -> None:
    """Ctrl+L wraca do folderu z grającym wierszem. Źródło z Enter zostaje,
    także gdy ma jeden wiersz (playlista z jednym utworem, preset)."""
    nav = Navigator()
    rows = _tracks(FOLDER, "01 Lustro", "02 Rama", "03 Drzwi")
    nav.apply_folder(r"D:\Playlista", [rows[1]], preferred_id=rows[1].item_id)
    nav.activate_selected(open_player_on_activation=False)
    assert nav.session.playback_source_rows == (rows[1],)

    nav.apply_folder(FOLDER, rows, preferred_id=rows[1].item_id)

    assert nav.session.playback_source_rows == (rows[1],)


def test_file_missing_from_folder_listing_keeps_its_own_source() -> None:
    """Folder hosta nie pokazuje np. playlist .m3u. Wtedy nie podmieniamy
    źródła na utwory, wśród których grającego pliku nie ma."""
    nav = Navigator()
    rows = _tracks(FOLDER, "01 Lustro", "02 Rama")
    playlist = FOLDER + r"\lista.m3u"

    nav.play_local_file(playlist, "lista")
    nav.apply_folder(FOLDER, rows, preferred_id=f"file:{playlist}")

    assert [row.item_id for row in nav.session.playback_source_rows] == [
        f"file:{playlist}"
    ]


def test_shell_open_goes_through_navigator(tmp_path: Path, monkeypatch) -> None:
    song = tmp_path / "02 Rama.flac"
    song.write_bytes(b"")
    nav = Navigator()
    ran: list[object] = []
    folders: list[tuple[str, str | None]] = []
    frame = SimpleNamespace(
        navigator=nav,
        announcer=SimpleNamespace(say=lambda _message: None),
        _switch_session=nav.switch_session,
        _run=ran.extend,
        _open_folder=lambda path, preferred_id=None: folders.append((path, preferred_id)),
    )
    monkeypatch.setattr(gui, "_present_main_window", lambda _frame: None)

    gui.LiteFrame._handle_shell_request(
        frame, ShellRequest(files=(str(song),), add_to_queue=False)
    )

    assert ran == [PlayTrack(str(song), f"file:{song}", "02 Rama")]
    assert nav.sessions[SessionId.FILES].now_playing_id == f"file:{song}"
    assert folders == [(str(tmp_path), f"file:{song}")]
