from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, SessionId
from amc_wx_lite.search_catalog import SearchEntry, query_words, record_query, search
from amc_wx_lite.search_sources import SearchSources


def entry(
    session: SessionId,
    item_id: str,
    title: str,
    *,
    artist: str = "",
    detail: str = "",
    kind: str = "track",
) -> SearchEntry:
    return SearchEntry(
        session,
        Row(
            item_id=item_id,
            title=title,
            kind=kind,
            artist_name=artist,
            detail=detail,
        ),
        LibraryView.ALL_FILES,
    )


def test_query_is_case_and_accent_insensitive_and_requires_all_words() -> None:
    entries = [
        entry(SessionId.RADIO, "1", "Radio Poznań", detail="Polska"),
        entry(SessionId.RADIO, "2", "Radio Kraków", detail="Polska"),
    ]
    assert [item.row.item_id for item in search(entries, "POZNAN polska")] == ["1"]
    assert query_words("  ŻÓŁĆ   radio ") == ("zołc", "radio")


def test_search_uses_artist_kind_detail_and_user_facing_session_name() -> None:
    entries = [
        entry(SessionId.PODCASTS, "1", "Rozmowa", artist="Anna", kind="episode"),
        entry(SessionId.TIDAL, "2", "Album", artist="Anna", kind="album"),
    ]
    assert [item.row.item_id for item in search(entries, "anna odcinek")] == ["1"]
    assert [item.row.item_id for item in search(entries, "tidal anna")] == ["2"]


def test_accessible_result_includes_intentional_artist_not_model_data() -> None:
    result = entry(
        SessionId.PODCASTS,
        "technical-id",
        "Odcinek",
        artist="Kanał",
        kind="episode",
    )
    assert result.accessible_label == (
        "Odcinek, Kanał, odcinek, Podcasty i YouTube"
    )
    assert "technical-id" not in result.accessible_label


def test_search_deduplicates_same_item_in_multiple_collection_views() -> None:
    duplicate = entry(SessionId.TIDAL, "same", "Piosenka")
    assert search([duplicate, duplicate], "piosenka") == [duplicate]


def test_history_is_newest_first_case_insensitive_and_bounded() -> None:
    assert record_query(["Radio", "Podcast"], "radio") == ["radio", "Podcast"]
    assert record_query([str(index) for index in range(60)], "nowe") == [
        "nowe",
        *(str(index) for index in range(49)),
    ]


def test_accessible_label_never_contains_private_identifiers_or_repr() -> None:
    result = SearchEntry(
        SessionId.PODCASTS,
        Row(
            item_id="private-123",
            title="Audycja",
            kind="episode",
            parent_id="parent-456",
            service_id="service-789",
            detail="12 minut",
        ),
    )
    assert result.accessible_label == (
        "Audycja, odcinek, 12 minut, Podcasty i YouTube"
    )
    assert "private" not in result.accessible_label
    assert "parent" not in result.accessible_label
    assert "Row(" not in result.accessible_label


class _Library:
    def load_view(self, view):
        assert view == "all_files"
        return type("Result", (), {"rows": [Row("f", "Plik", "track")]})()


class _Radio:
    def load(self, *, scope):
        assert scope == "library"
        return type(
            "Snapshot",
            (),
            {
                "load_error": None,
                "as_payload": lambda self: [
                    {"id": "r", "name": "Radio", "url": "https://radio"}
                ],
            },
        )()


class _Podcasts:
    def search_rows(self):
        return [Row("p", "Podcast", "podcast")]


class _Tidal:
    def load_view(self, view):
        return type(
            "Result",
            (),
            {"rows": (Row("t", "TIDAL", "album"),)},
        )()


def test_sources_collect_all_cached_sessions_and_deduplicate_later() -> None:
    sources = SearchSources(_Library(), _Radio(), _Podcasts(), _Tidal())
    result = sources.collect((
        SessionId.FILES,
        SessionId.RADIO,
        SessionId.PODCASTS,
        SessionId.TIDAL,
    ))
    assert not result.warnings
    assert {item.session_id for item in result.entries} == {
        SessionId.FILES,
        SessionId.RADIO,
        SessionId.PODCASTS,
        SessionId.TIDAL,
    }
    assert len([item for item in result.entries if item.row.item_id == "t"]) == 3
    assert len(search(list(result.entries), "tidal")) == 1


def test_one_source_failure_does_not_erase_other_global_results() -> None:
    class BrokenRadio(_Radio):
        def load(self, *, scope):
            raise OSError("technical failure")

    sources = SearchSources(_Library(), BrokenRadio(), _Podcasts(), _Tidal())
    result = sources.collect((SessionId.FILES, SessionId.RADIO))
    assert [item.row.item_id for item in result.entries] == ["f"]
    assert result.warnings == (
        "Nie udało się odczytać zapisanych danych: Radio",
    )
    assert "OSError" not in result.warnings[0]


def test_fresh_empty_profile_is_a_valid_search_catalog() -> None:
    class EmptyLibrary:
        def load_view(self, view):
            assert view == "all_files"
            return type("Result", (), {"rows": ()})()

    class EmptyRadio:
        def load(self, *, scope):
            assert scope == "library"
            return type(
                "Snapshot",
                (),
                {"load_error": None, "as_payload": lambda self: ()},
            )()

    class EmptyPodcasts:
        def search_rows(self):
            return ()

    sources = SearchSources(
        EmptyLibrary(),
        EmptyRadio(),
        EmptyPodcasts(),
        None,
    )
    result = sources.collect((
        SessionId.FILES,
        SessionId.RADIO,
        SessionId.PODCASTS,
    ))

    assert result.entries == ()
    assert result.warnings == ()


def test_radioarchiwista_doklada_stacje_i_ulubione_bez_sieci() -> None:
    class _Radioarchiwista:
        def search_rows(self):
            return [
                Row("rarch:station:pr1", "Polskie Radio Program 1", "folder"),
                Row(
                    "rarch:pr1:2026-10-29T14:00:00+00:00",
                    "Polskie Radio Program 1, 29.10.2026 15:00",
                    "recording",
                ),
            ]

    sources = SearchSources(
        _Library(),
        _Radio(),
        _Podcasts(),
        _Tidal(),
        _Radioarchiwista(),
    )
    result = sources.collect((SessionId.RADIOARCHIWISTA,))
    assert not result.warnings
    views = [entry.source_view for entry in result.entries]
    assert views == [
        LibraryView.RADIOARCHIWISTA_STATIONS,
        LibraryView.RADIOARCHIWISTA_FAVORITES,
    ]


def test_radioarchiwista_bez_zrodla_nie_psuje_wyszukiwania() -> None:
    sources = SearchSources(_Library(), _Radio(), _Podcasts(), _Tidal())
    result = sources.collect((SessionId.RADIOARCHIWISTA, SessionId.FILES))
    assert not result.warnings
    assert [entry.row.item_id for entry in result.entries] == ["f"]
