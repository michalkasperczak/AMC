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

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Announce, LibraryView, Navigator, SessionId
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
    Action.VIEW_PODCAST_DOWNLOADS,
    Action.SORT_PODCAST_INBOX_ADDED,
    Action.SORT_PODCAST_INBOX_ALPHABETICAL,
    Action.SORT_PODCAST_INBOX_BY_PODCAST,
    Action.YOUTUBE_BROADCASTS,
    Action.REFRESH_YOUTUBE_BROADCASTS,
    Action.ADD_YOUTUBE_LIVE_STATION,
    Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
    Action.CONFIGURE_YOUTUBE_LIVE_CHANNELS,
    Action.TOGGLE_YOUTUBE_LIVE_AGGREGATE_CHANNEL,
    Action.REFRESH_YOUTUBE_LIVE_AGGREGATE,
    Action.RECORD_YOUTUBE_LIVE_NOW,
    Action.SCHEDULE_YOUTUBE_LIVE,
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
    LibraryView.PODCAST_DOWNLOADS,
    LibraryView.PODCAST_PLAYLISTS,
    LibraryView.PODCAST_PLAYLIST_CONTENTS,
    # Transmisje pochodzą z oficjalnego API przez host C# i krótką pamięć
    # backendu, nie z lokalnej bazy plików ani podcasts.db.
    LibraryView.YOUTUBE_BROADCASTS,
    LibraryView.YOUTUBE_BROADCASTS_AGGREGATE,
    # Widoki Radio Archiwisty czytają publiczny katalog HTTP przez własny
    # moduł źródła z cache w profilu, nie z library.db ani podcasts.db.
    LibraryView.RADIOARCHIWISTA_STATIONS,
    LibraryView.RADIOARCHIWISTA_YEARS,
    LibraryView.RADIOARCHIWISTA_MONTHS,
    LibraryView.RADIOARCHIWISTA_DAYS,
    LibraryView.RADIOARCHIWISTA_HOURS,
    LibraryView.RADIOARCHIWISTA_FAVORITES,
    # Pierwsze widoki TIDAL czytaja state.json przez osobny TidalSource.
    # Nie wolno kierowac ich do lokalnego LibrarySource/library.db.
    LibraryView.TIDAL_LIBRARY,
    LibraryView.TIDAL_FAVORITES,
    LibraryView.TIDAL_PLAYLISTS,
    # Zawartosc kontenera pochodzi z oficjalnego katalogu przez host C#.
    LibraryView.TIDAL_CONTAINER,
    # Prywatna kolejka TIDAL jest zapisana w state.json AMC-wx-Lite.
    LibraryView.TIDAL_QUEUE,
    # Biblioteka i kontenery Spotify przychodzą z osobnego kontraktu hosta C#;
    # Historia jest prywatnym stanem AMC Python. Żaden z tych widoków nie
    # należy do lokalnej bazy SQLite plików.
    LibraryView.SPOTIFY_LIBRARY,
    LibraryView.SPOTIFY_CONTAINER,
    LibraryView.SPOTIFY_HISTORY,
    # Historia WiiM i Sonosa jest prywatnym stanem AMC Python, oddzielnym
    # dla obu sesji; nie wolno odczytywać jej z biblioteki plików SQLite.
    LibraryView.SERVICE_HISTORY,
    # Korzeń źródeł jest stałą, uczciwą listą dwóch możliwości interfejsu;
    # zapisane strumienie czyta osobny WiimSource ze wspólnego state.json.
    LibraryView.WIIM_SOURCE_LIBRARY,
    LibraryView.WIIM_NETWORK_STREAMS,
    # Biblioteka, ulubione i playlisty Sonos przychodza z hosta C# oraz
    # oficjalnej warstwy konta Sonos. Nie sa widokami lokalnej bazy SQLite.
    LibraryView.SONOS_LIBRARY,
    LibraryView.SONOS_FAVORITES,
    LibraryView.SONOS_PLAYLISTS,
    LibraryView.SONOS_OWN_STREAMS,
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


def test_active_recording_preview_syncs_before_naming_selected_file() -> None:
    """Jeden wiersz musi być nazwany nawet bez nowego zdarzenia fokusu MSAA."""
    from types import SimpleNamespace

    from amc_wx_lite.gui import LiteFrame

    navigator = Navigator()
    navigator.switch_session(SessionId.RADIO)
    navigator.apply_radio_view(
        LibraryView.ACTIVE_RADIO_RECORDINGS,
        "Nagrywane",
        [Row("techniczne-id", "TVP Info - 2026-10-21 17-14-45.mp3", "station")],
    )
    calls: list[object] = []
    frame = SimpleNamespace(
        navigator=navigator,
        _sync_views=lambda: calls.append("sync"),
        _announce_after_native_list_update=lambda text: calls.append(("say", text)),
    )

    LiteFrame._announce_transient_preview(
        frame,
        [Announce("Nagrywane, 1 z 1")],
    )

    assert calls == [
        "sync",
        ("say", "Nagrywane, 1 z 1, TVP Info - 2026-10-21 17-14-45.mp3"),
    ]
    assert "techniczne-id" not in calls[1][1]


def test_empty_active_recording_preview_stays_exact_and_after_sync() -> None:
    """Pusta lista nadal kończy się dokładnym, krótkim komunikatem."""
    from types import SimpleNamespace

    from amc_wx_lite.gui import LiteFrame

    navigator = Navigator()
    navigator.switch_session(SessionId.RADIO)
    navigator.apply_radio_view(
        LibraryView.ACTIVE_RADIO_RECORDINGS,
        "Nagrywane",
        [],
    )
    calls: list[object] = []
    frame = SimpleNamespace(
        navigator=navigator,
        _sync_views=lambda: calls.append("sync"),
        _announce_after_native_list_update=lambda text: calls.append(("say", text)),
    )

    LiteFrame._announce_transient_preview(frame, [Announce("zero elementów")])

    assert calls == ["sync", ("say", "zero elementów")]


def test_transient_preview_with_many_rows_keeps_native_list_speech() -> None:
    """Pełny komunikat własny jest potrzebny tylko przy jednym wierszu."""
    from types import SimpleNamespace

    from amc_wx_lite.gui import LiteFrame

    navigator = Navigator()
    navigator.apply_library_view(
        LibraryView.HISTORY,
        "Biblioteka — Historia odtwarzania",
        [
            Row("id-1", "Pierwszy", "track"),
            Row("id-2", "Drugi", "track"),
        ],
    )
    calls: list[object] = []
    events = [Announce("Biblioteka — Historia odtwarzania, 2 elementy")]
    frame = SimpleNamespace(
        navigator=navigator,
        _run=lambda value: calls.append(("run", value)),
        _sync_views=lambda: calls.append("sync"),
        _announce_after_native_list_update=lambda text: calls.append(("say", text)),
    )

    LiteFrame._announce_transient_preview(frame, events)

    assert calls == [("run", events)]


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


def test_podcast_playlists_have_their_own_loader_and_are_not_local_files() -> None:
    dispatch = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_dispatch"
    )
    dispatch_source = ast.get_source_segment(SOURCE, dispatch) or ""
    assert "self._open_podcast_playlist_view(OpenPodcastPlaylistView())" in dispatch_source

    runner = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_run"
    )
    runner_source = ast.get_source_segment(SOURCE, runner) or ""
    assert "isinstance(intent, OpenPodcastPlaylistView)" in runner_source
    assert "self._open_podcast_playlist_view(intent)" in runner_source

    loader = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_open_podcast_playlist_view"
    )
    loader_source = ast.get_source_segment(SOURCE, loader) or ""
    assert "self.podcasts.playlists()" in loader_source
    assert "self.podcasts.playlist(" in loader_source


def test_tidal_sort_is_private_preserves_selection_and_orders_fresh_rows() -> None:
    setter = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_set_tidal_collection_sort"
    )
    setter_source = ast.get_source_segment(SOURCE, setter) or ""
    assert "self.state.tidal_sort_modes[scope] = mode" in setter_source
    assert "preferred_id=state.model.selected_id" in setter_source
    assert "announcement=label" in setter_source

    loader = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_open_tidal_view"
    )
    loader_source = ast.get_source_segment(SOURCE, loader) or ""
    assert "self.tidal.order_rows" in loader_source
    assert "sort_mode=sort_override" in loader_source
    assert "self.announcer.say(intent.announcement)" in loader_source


def test_tidal_right_arrow_uses_a_named_list_and_restores_list_focus() -> None:
    key_handler = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_on_key"
        and "tidal_relations" in (ast.get_source_segment(SOURCE, node) or "")
    )
    key_source = ast.get_source_segment(SOURCE, key_handler) or ""
    assert 'chord.canonical == "Right"' in key_source
    assert "self.navigator.tidal_relations()" in key_source

    presenter = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef)
        and node.name == "_show_tidal_relations"
    )
    presenter_source = ast.get_source_segment(SOURCE, presenter) or ""
    assert "len(intent.choices) == 1" in presenter_source
    assert "self.navigator.activate_tidal_relation(intent.choices[0])" in presenter_source
    assert "self._show_relation_destination_window(" in presenter_source
    assert "choice.label" in presenter_source
    assert "PopupMenu" not in presenter_source


def test_play_track_forwards_the_bookmark_position() -> None:
    """Bez ``position_seconds`` zakladka odtworzylaby plik od zera."""
    assert "position_seconds=intent.position_seconds" in SOURCE, (
        "gui._play_track gubi pozycje zakladki"
    )


def test_bookmark_view_asks_the_data_layer_for_the_file_id() -> None:
    """``item_id`` i mapa celow musza przejsc przez okno do nawigatora."""
    assert "item_id=item_id" in SOURCE
    assert "bookmark_targets=result.bookmark_targets" in SOURCE


def test_tidal_external_player_is_wired_without_reusing_local_transport_state() -> None:
    run = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_run"
    )
    run_source = ast.get_source_segment(SOURCE, run) or ""
    assert "isinstance(intent, PlayTidalTrack)" in run_source
    assert "self._play_tidal_track(intent)" in run_source

    play_pause = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_play_pause"
    )
    play_pause_source = ast.get_source_segment(SOURCE, play_pause) or ""
    assert 'tidal_external_transport("toggle")' in play_pause_source
    assert "self.navigator.active is SessionId.TIDAL" in play_pause_source

    refresh = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_refresh_status"
    )
    refresh_source = ast.get_source_segment(SOURCE, refresh) or ""
    assert "and not remote_player" in refresh_source
    assert "self.navigator.active is SessionId.WIIM" in refresh_source


def test_tidal_stays_on_the_list_and_exposes_only_external_transport() -> None:
    on_key_source = next(
        source
        for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_on_key"
        if "SessionId.TIDAL" in (
            source := ast.get_source_segment(SOURCE, node) or ""
        )
    )
    assert '"Prior", "Next", "Shift+Prior", "Shift+Next"' in on_key_source
    assert "self._tidal_external_skip(forward)" in on_key_source
    assert "self._queue_step(forward)" in on_key_source
    assert 'chord.canonical == "Ctrl+Return"' in on_key_source
    assert "play_selected_tidal_container()" in on_key_source

    dispatch = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_dispatch"
    )
    dispatch_source = ast.get_source_segment(SOURCE, dispatch) or ""
    assert "nie ma " in dispatch_source
    assert "odtwarzacza AMC" in dispatch_source

    announce = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_announce_tidal_time"
    )
    announce_source = ast.get_source_segment(SOURCE, announce) or ""
    assert "client.tidal_external_state" in announce_source

    run = next(
        node for node in ast.walk(TREE)
        if isinstance(node, ast.FunctionDef) and node.name == "_run"
    )
    run_source = ast.get_source_segment(SOURCE, run) or ""
    assert "isinstance(intent, PlayTidalContainer)" in run_source
    assert "self._play_tidal_container(intent)" in run_source
