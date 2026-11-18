from __future__ import annotations

from types import SimpleNamespace
from typing import cast
import sys
import types

try:
    import wx
except ModuleNotFoundError:
    class _WxStub(types.ModuleType):
        def __getattr__(self, name: str):
            value = 0 if name.isupper() or name.startswith("ID_") else type(name, (), {})
            setattr(self, name, value)
            return value

    wx = _WxStub("wx")
    wx.GetTopLevelWindows = lambda: ()
    wx.version = None
    sys.modules["wx"] = wx

from amc_wx_lite.gui import LiteFrame, SearchDialog
from amc_wx_lite.navigation import LibraryView, SessionId, View
from amc_wx_lite.radioarchiwista_source import (
    RadioarchiwistaSource,
    SUPPORTED_CATALOG_VERSION,
)
from amc_wx_lite.search_sources import SearchCollection
from amc_wx_lite.shortcuts import Action, RADIOARCHIWISTA_SUPPORTED_ACTIONS
from amc_wx_lite.state_store import LiteState, Options, StateStore


def _stations_payload() -> dict:
    return {
        "catalog_version": SUPPORTED_CATALOG_VERSION,
        "stations": [{
            "slug": "pr1",
            "display_name": "Program 1",
            "public_name": "Jedynka",
            "archive_count": 123,
            "timezone": "Europe/Warsaw",
        }],
    }


def test_archive_station_search_reads_only_public_station_index_on_cold_profile(tmp_path) -> None:
    requested: list[str] = []
    source = RadioarchiwistaSource(
        tmp_path,
        fetch_json=lambda url: requested.append(url) or _stations_payload(),
    )

    rows = source.search_station_rows()

    assert [row.title for row in rows] == ["Jedynka, 123 nagrania"]
    assert len(requested) == 1
    assert requested[0].endswith("/data/stations.json")


def test_archive_ctrl_f_handler_runs_public_station_search_and_opens_real_list(tmp_path) -> None:
    requested: list[str] = []
    source = RadioarchiwistaSource(
        tmp_path,
        fetch_json=lambda url: requested.append(url) or _stations_payload(),
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    shown: list[tuple[list, tuple[str, ...]]] = []
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: None,
    )
    navigator = SimpleNamespace(
        active=SessionId.RADIOARCHIWISTA,
        view=View.LIST,
        sessions={SessionId.RADIOARCHIWISTA: SimpleNamespace(
            model=SimpleNamespace(rows=[]),
            library_view=LibraryView.RADIOARCHIWISTA_STATIONS,
        )},
    )
    frame = SimpleNamespace(
        navigator=navigator,
        options=Options(session_order=[SessionId.RADIOARCHIWISTA.value]),
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((), ()),
            radioarchiwista=source,
        ),
        client=None,
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
        _apple_music_records={},
        _apple_music_search_rows=(),
        _show_search=lambda **kwargs: LiteFrame._run_search_query(
            frame, "jedynka", dialog,
            all_services=kwargs["all_services"],
            session_before=SessionId.RADIOARCHIWISTA,
        ),
    )

    assert Action.SEARCH_CURRENT in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    LiteFrame._dispatch(frame, Action.SEARCH_CURRENT)

    assert len(requested) == 1
    result = shown[0][0][0]
    assert result.session_id is SessionId.RADIOARCHIWISTA
    assert result.row.item_id == "rarch:station:pr1"

    opened: list[object] = []
    frame._transient_preview_return = "search"
    frame._switch_session = opened.append
    frame._open_radioarchiwista_stations = lambda **kwargs: opened.append(kwargs)
    LiteFrame._open_search_result(frame, result)
    assert opened == [SessionId.RADIOARCHIWISTA, {"preferred_id": "rarch:station:pr1"}]


def test_global_search_selection_intersects_available_active_and_preference() -> None:
    active = (SessionId.FILES, SessionId.RADIOARCHIWISTA)
    assert LiteFrame._global_search_sessions(
        SimpleNamespace(
            options=Options(global_search_session_ids=[
                SessionId.RADIOARCHIWISTA.value,
                SessionId.PODCASTS.value,
            ])
        ),
        active,
    ) == (SessionId.RADIOARCHIWISTA,)


def test_old_profile_without_selection_defaults_to_active_sessions() -> None:
    options = Options(
        session_order=[SessionId.FILES.value, SessionId.RADIOARCHIWISTA.value]
    )
    assert options.global_search_session_ids is None
    assert LiteFrame._global_search_sessions(
        SimpleNamespace(options=options),
        (SessionId.FILES, SessionId.RADIOARCHIWISTA),
    ) == (SessionId.FILES, SessionId.RADIOARCHIWISTA)


def test_global_search_selection_survives_disabled_session_and_restart(tmp_path) -> None:
    store = StateStore(tmp_path)
    state = LiteState(options=Options(
        session_order=[SessionId.FILES.value],
        global_search_session_ids=[SessionId.RADIOARCHIWISTA.value],
    ))
    store.save(state)

    restarted = store.load()
    assert restarted.options.global_search_session_ids == [
        SessionId.RADIOARCHIWISTA.value
    ]
    assert LiteFrame._global_search_sessions(
        SimpleNamespace(options=restarted.options),
        (SessionId.FILES,),
    ) == ()
    assert LiteFrame._global_search_sessions(
        SimpleNamespace(options=restarted.options),
        (SessionId.FILES, SessionId.RADIOARCHIWISTA),
    ) == (SessionId.RADIOARCHIWISTA,)


def test_global_search_with_no_selected_source_does_not_collect_or_start_io() -> None:
    collected: list[object] = []
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: collected.append((entries, warnings)),
    )
    frame = SimpleNamespace(
        options=Options(global_search_session_ids=[]),
        search_sources=SimpleNamespace(collect=lambda sessions: (_ for _ in ()).throw(AssertionError(sessions))),
    )

    LiteFrame._run_search_query(
        frame, "nic", dialog, all_services=True, session_before=SessionId.FILES,
    )

    assert collected == [([], ("Nie wybrano źródeł wyszukiwania globalnego",))]


def test_disabled_but_remembered_source_does_not_collect_or_start_io() -> None:
    shown: list[object] = []
    frame = SimpleNamespace(
        options=Options(
            session_order=[SessionId.FILES.value],
            global_search_session_ids=[SessionId.RADIOARCHIWISTA.value],
        ),
        search_sources=SimpleNamespace(
            collect=lambda sessions: (_ for _ in ()).throw(AssertionError(sessions))
        ),
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
    )

    LiteFrame._run_search_query(
        frame, "jedynka", dialog, all_services=True, session_before=SessionId.FILES,
    )

    assert shown == [([], ("Nie wybrano źródeł wyszukiwania globalnego",))]


def test_global_search_does_not_reuse_a_catalog_from_a_different_scope() -> None:
    from amc_wx_lite.list_model import Row
    from amc_wx_lite.search_catalog import SearchEntry

    previous = SearchEntry(SessionId.RADIOARCHIWISTA, Row("old-station", "Szukane archiwum", "station"))
    current = SearchEntry(SessionId.FILES, Row("current-file", "Szukane nagranie", "track"))
    collected, shown = [], []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        options=Options(session_order=[SessionId.FILES.value],
                        global_search_session_ids=[SessionId.FILES.value]),
        search_sources=SimpleNamespace(collect=lambda sessions: collected.append(sessions) or SearchCollection((current,))),
        runner=Runner(), _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=SearchCollection((previous,)),
        catalog_sessions=(SessionId.RADIOARCHIWISTA,),
        show_results=lambda entries, warnings: shown.append(entries),
    )
    LiteFrame._run_search_query(cast(LiteFrame, frame), "Szukane", cast(SearchDialog, dialog), all_services=True, session_before=SessionId.FILES)
    assert shown == [[current]]
    assert collected == [(SessionId.FILES,)]
    assert dialog.catalog_sessions == (SessionId.FILES,)
    LiteFrame._run_search_query(cast(LiteFrame, frame), "nagranie", cast(SearchDialog, dialog), all_services=True, session_before=SessionId.FILES)
    assert collected == [(SessionId.FILES,)]  # Same scope reuses the valid snapshot.


def test_explicit_opt_out_remains_after_disable_restart_and_reenable(tmp_path) -> None:
    store = StateStore(tmp_path)
    store.save(LiteState(options=Options(
        session_order=[SessionId.FILES.value],
        global_search_session_ids=[SessionId.FILES.value],
    )))
    state = store.load()
    state.options.session_order = [SessionId.FILES.value, SessionId.RADIOARCHIWISTA.value]
    assert LiteFrame._global_search_sessions(SimpleNamespace(options=state.options)) == (SessionId.FILES,)


def test_manifest_exclusions_override_a_stored_search_preference(monkeypatch) -> None:
    from amc_wx_lite import session_order
    monkeypatch.setattr(session_order, "current_profile", lambda: SimpleNamespace(
        session_ids=(SessionId.FILES.value, SessionId.RADIOARCHIWISTA.value),
    ))
    options = Options(
        session_order=[SessionId.FILES.value, SessionId.APPLE_MUSIC.value],
        global_search_session_ids=[SessionId.FILES.value, SessionId.APPLE_MUSIC.value],
    )
    assert LiteFrame._global_search_sessions(SimpleNamespace(options=options)) == (SessionId.FILES,)
