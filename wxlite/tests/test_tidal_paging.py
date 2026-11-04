"""Large TIDAL collections use bounded native rows without losing search."""

from __future__ import annotations

from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    LibraryView, Navigator, OpenTidalView, SessionId,
)
from amc_wx_lite.tidal_source import TidalViewResult, paged_collection_rows


def _albums(count: int = 944) -> tuple[Row, ...]:
    return tuple(
        Row(f"tidal:albums:{index}", f"Album {index}", "album",
            service_id=f"albums:{index}")
        for index in range(count)
    )


def test_collection_pages_150_and_searches_unloaded_titles() -> None:
    rows = _albums()
    first, total, loaded = paged_collection_rows(rows, scope="library")
    assert (total, loaded, len(first)) == (944, 150, 151)
    assert first[-1].kind == "loadMore"
    assert first[-1].title == "Załaduj więcej"
    assert all(row.kind == "album" for row in first[:-1])

    second, total, loaded = paged_collection_rows(
        rows, scope="library", requested=300,
    )
    assert (total, loaded, len(second)) == (944, 300, 301)
    assert second[150].item_id == rows[150].item_id

    found, total, loaded = paged_collection_rows(
        rows, scope="library", query="Album 800",
    )
    assert (total, loaded) == (1, 1)
    assert [row.item_id for row in found] == [rows[800].item_id]


def test_load_more_row_requests_current_tidal_collection() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    rows, total, _loaded = paged_collection_rows(_albums(), scope="library")
    nav.apply_tidal_view(
        LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", list(rows),
        total_count=total,
    )
    nav.session.model.select_id(rows[-1].item_id)
    assert nav.activate_selected() == [
        OpenTidalView(LibraryView.TIDAL_LIBRARY, load_more=True)
    ]


def test_gui_load_more_uses_cached_collection_and_focuses_first_new_row() -> None:
    rows = _albums()
    nav = Navigator()
    nav.active = SessionId.TIDAL
    first, _total, _loaded = paged_collection_rows(rows, scope="library")
    nav.apply_tidal_view(
        LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", list(first),
        total_count=len(rows),
    )
    nav.session.model.select_id(first[-1].item_id)
    cache = TidalViewResult("library", "Biblioteka TIDAL", rows, True, "addedNewest")
    spoken: list[str] = []
    calls: list[str] = []
    frame = SimpleNamespace(
        navigator=nav,
        _tidal_view_cache={"library": cache},
        _tidal_loaded_counts={("library", ""): 150},
        _tidal_render_signature=None,
        _tidal_rendered_rows=None,
        filter_box=SimpleNamespace(GetValue=lambda: ""),
        announcer=SimpleNamespace(say=spoken.append),
        _sync_views=lambda: calls.append("sync"),
    )
    frame._sync_tidal_paged_model = lambda **kwargs: LiteFrame._sync_tidal_paged_model(
        frame, **kwargs,
    )
    LiteFrame._open_tidal_view(
        frame, OpenTidalView(LibraryView.TIDAL_LIBRARY, load_more=True),
    )
    assert len(nav.session.model.rows) == 301
    assert nav.session.model.selected_id == rows[150].item_id
    assert calls == ["sync"]
    assert spoken == ["Pokazano 300 z 944 pozycji TIDAL"]


def test_filter_across_full_cache_keeps_native_page_bounded() -> None:
    rows = _albums()
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.session.library_view = LibraryView.TIDAL_LIBRARY
    query = "Album 8"
    frame = SimpleNamespace(
        navigator=nav,
        _tidal_view_cache={
            "library": TidalViewResult(
                "library", "Biblioteka TIDAL", rows, True, "addedNewest",
            ),
        },
        _tidal_loaded_counts={},
        _tidal_render_signature=None,
        _tidal_rendered_rows=None,
        filter_box=SimpleNamespace(GetValue=lambda: query),
    )
    count = LiteFrame._sync_tidal_paged_model(frame)
    assert count == (111, 111)
    assert all(row.kind == "album" for row in nav.session.model.rows)
    assert rows[800].item_id in {row.item_id for row in nav.session.model.rows}


def test_clearing_filter_restores_full_first_page_after_filtered_result() -> None:
    rows = _albums()
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.session.library_view = LibraryView.TIDAL_LIBRARY
    value = {"text": "Album 800"}
    cache = TidalViewResult("library", "Biblioteka TIDAL", rows, True, "addedNewest")
    frame = SimpleNamespace(
        navigator=nav,
        _tidal_view_cache={"library": cache},
        _tidal_loaded_counts={},
        _tidal_render_signature=None,
        _tidal_rendered_rows=None,
        filter_box=SimpleNamespace(GetValue=lambda: value["text"]),
    )
    assert LiteFrame._sync_tidal_paged_model(frame) == (1, 1)
    value["text"] = ""
    assert LiteFrame._sync_tidal_paged_model(frame) == (944, 150)
    assert len(nav.session.model.rows) == 151
    assert nav.session.model.rows[0].item_id == rows[0].item_id
    assert nav.session.model.rows[-1].kind == "loadMore"
