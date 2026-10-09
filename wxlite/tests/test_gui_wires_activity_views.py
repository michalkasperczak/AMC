"""Czy ``gui.py`` naprawde PODLACZYL nowe akcje i widoki.

``wx`` nie da sie zaimportowac w tym srodowisku, wiec okna nie zbudujemy.
Mozna jednak sprawdzic to, co i tak jest statyczne: czy kazda nowa akcja ma
galaz w ``_dispatch``, czy kazdy nowy ``LibraryView`` ma klucz danych i czy
pozycja zakladki trafia do ``play_file``. Bez tego skrot byl by martwy.
"""

from __future__ import annotations

import ast
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.navigation import LibraryView
from amc_wx_lite.shortcuts import Action

GUI = Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
SOURCE = GUI.read_text(encoding="utf-8")
TREE = ast.parse(SOURCE)

NEW_ACTIONS = (
    Action.VIEW_HISTORY,
    Action.VIEW_SAVED_QUEUE,
    Action.VIEW_ITEM_BOOKMARKS,
    Action.VIEW_FOLDERS,
    Action.SHOW_LIST,
    Action.MANAGE_RADIO_SCHEDULES,
    Action.VIEW_PODCAST_INBOX,
    Action.VIEW_PODCAST_IN_PROGRESS,
    Action.VIEW_PODCAST_DOWNLOADS,
    Action.SORT_PODCAST_INBOX_ADDED,
    Action.SORT_PODCAST_INBOX_ALPHABETICAL,
    Action.SORT_PODCAST_INBOX_BY_PODCAST,
)


def _attribute_names() -> set[str]:
    """Nazwy ``Action.X`` wymienione gdziekolwiek w ``gui.py``."""
    names = set()
    for node in ast.walk(TREE):
        if (
            isinstance(node, ast.Attribute)
            and isinstance(node.value, ast.Name)
            and node.value.id == "Action"
        ):
            names.add(node.attr)
    return names


def test_every_new_action_is_handled_in_the_window() -> None:
    handled = _attribute_names()
    missing = [a.name for a in NEW_ACTIONS if a.name not in handled]
    assert not missing, f"skroty bez obslugi w gui.py (martwe): {missing}"


#: Widoki czytane z PROFILU (SQLite). Kazdy z nich musi miec klucz danych,
#: bo ``_VIEW_KEYS[view]`` bez wpisu to ``KeyError`` w watku roboczym.
#: Ponizsze widoki sa tu CELOWO nieobecne: pochodza ze stanu hosta albo
#: ``state.json``, nie z SQLite profilu.
_NON_SQLITE_VIEWS = (
    LibraryView.LIVE_QUEUE,
    LibraryView.ACTIVE_RADIO_RECORDINGS,
    # Historia jest czytana z radio.recordingHistory w state.json, a nie z
    # lokalnych widokow SQLite obslugiwanych przez _VIEW_KEYS.
    LibraryView.RECORDED_RADIO_FILES,
    # Harmonogramy sa czytane z radio.recordingSchedules w state.json, a ich
    # etykiety sklada wspolny kod C# uzywany takze przez glowne AMC.
    LibraryView.RADIO_RECORDING_SCHEDULES,
    # Oba widoki podcastow ida przez osobne PodcastSource/podcasts.db. Nie
    # wolno kierowac ich do lokalnego LibrarySource/library.db przez _VIEW_KEYS.
    LibraryView.PODCAST_LIBRARY,
    LibraryView.PODCAST_EPISODES,
    LibraryView.PODCAST_FAVORITES,
    LibraryView.PODCAST_HISTORY,
    LibraryView.PODCAST_QUEUE,
    LibraryView.PODCAST_INBOX,
    LibraryView.PODCAST_IN_PROGRESS,
    LibraryView.PODCAST_DOWNLOADS,
)


def test_every_library_view_has_a_data_key() -> None:
    """``_VIEW_KEYS[view]`` bez wpisu to ``KeyError`` w watku roboczym."""
    keys = None
    for node in ast.walk(TREE):
        if isinstance(node, ast.Assign) and any(
            isinstance(t, ast.Name) and t.id == "_VIEW_KEYS" for t in node.targets
        ):
            keys = node.value
            break
    assert isinstance(keys, ast.Dict), "nie znalazlem _VIEW_KEYS w gui.py"
    mapped = {
        k.attr
        for k in keys.keys
        if isinstance(k, ast.Attribute)
    }
    missing = [
        v.name
        for v in LibraryView
        if v.name not in mapped and v not in _NON_SQLITE_VIEWS
    ]
    assert not missing, f"widoki bez klucza danych: {missing}"


def test_live_queue_view_is_not_read_from_the_profile() -> None:
    """ZYWA kolejka NIE moze miec klucza danych profilu.

    Ten widok jest stanem grajacego silnika: dane przychodza z
    ``queue.status``, nie z SQLite. Wpis w ``_VIEW_KEYS`` oznaczalby, ze ktos
    podlaczyl go do ``library.load_view`` -- a wtedy Ctrl+Q znow czytalby
    zapisany porzadek i widok klamalby w trakcie odtwarzania.

    Dawniej ten test wymagal klucza dla KAZDEGO widoku. To wymaganie bylo
    prawdziwe, dopoki wszystkie widoki pochodzily z profilu; teraz jeden
    pochodzi z hosta, wiec odwracamy zadanie dla tego jednego.
    """
    keys = None
    for node in ast.walk(TREE):
        if isinstance(node, ast.Assign) and any(
            isinstance(t, ast.Name) and t.id == "_VIEW_KEYS" for t in node.targets
        ):
            keys = node.value
            break
    assert isinstance(keys, ast.Dict)
    mapped = {k.attr for k in keys.keys if isinstance(k, ast.Attribute)}
    assert "LIVE_QUEUE" not in mapped, (
        "ZYWA kolejka dostala klucz danych profilu: Ctrl+Q znow czytalby zapis"
    )
    # I druga strona tej samej umowy: okno MUSI pytac host o stan kolejki.
    assert "queue_status()" in SOURCE, (
        "gui.py nie wola queue.status -- zywego widoku kolejki nie ma z czego zbudowac"
    )


def test_active_radio_recordings_are_read_from_the_host_not_the_profile() -> None:
    keys = None
    for node in ast.walk(TREE):
        if isinstance(node, ast.Assign) and any(
            isinstance(t, ast.Name) and t.id == "_VIEW_KEYS" for t in node.targets
        ):
            keys = node.value
            break
    assert isinstance(keys, ast.Dict)
    mapped = {k.attr for k in keys.keys if isinstance(k, ast.Attribute)}
    assert "ACTIVE_RADIO_RECORDINGS" not in mapped
    assert "radio_recording_status" in SOURCE


def test_escape_from_transient_preview_restores_without_verbose_return_message() -> None:
    """Po Escape ma przemowic odzyskany wiersz, nie opis technicznego widoku."""
    dispatch = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_dispatch"
    )
    source = ast.get_source_segment(SOURCE, dispatch) or ""
    assert "_restore_transient_preview(self, announce=False)" in source


def test_empty_recording_view_speaks_exact_count_after_native_empty_event() -> None:
    """Systemowe ``pusto, nieznane`` nie moze zostac ostatnim komunikatem."""
    method = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_announce_after_native_list_update"
    )
    source = ast.get_source_segment(SOURCE, method) or ""
    assert "CallLater" in source
    assert "self.announcer.say" in source


def test_podcast_inbox_sort_reuses_the_source_and_preserves_selection() -> None:
    method = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_set_podcast_inbox_sort"
    )
    source = ast.get_source_segment(SOURCE, method) or ""
    assert "preferred_id=state.model.selected_id" in source
    assert "self.state.podcast_inbox_sort_mode = mode" in source
    assert "self._open_podcast_aggregate" in source

    loader = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_open_podcast_aggregate"
    )
    loader_source = ast.get_source_segment(SOURCE, loader) or ""
    assert "sort_mode=sort_mode_override" in loader_source
    assert "self.announcer.say(intent.announcement)" in loader_source


def test_play_track_forwards_the_bookmark_position() -> None:
    """Bez ``position_seconds`` zakladka odtworzylaby plik od zera."""
    assert "position_seconds=intent.position_seconds" in SOURCE, (
        "gui._play_track gubi pozycje zakladki"
    )


def test_bookmark_view_asks_the_data_layer_for_the_file_id() -> None:
    """``item_id`` i mapa celow musza przejsc przez okno do nawigatora."""
    assert "item_id=item_id" in SOURCE
    assert "bookmark_targets=result.bookmark_targets" in SOURCE
