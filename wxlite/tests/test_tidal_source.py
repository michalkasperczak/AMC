"""Persisted TIDAL catalogue: membership, ordering and accessible labels."""

from __future__ import annotations

import json

from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.profile_layout import private_sandbox
from amc_wx_lite.tidal_source import (
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    TidalContainerPayloadError,
    TidalProfileError,
    TidalSource,
    VIEW_FAVORITES,
    VIEW_LIBRARY,
    VIEW_PLAYLISTS,
    artist_overview_rows,
    build_view,
    queue_payload,
    queue_rows,
    container_result_from_host,
    items_from_amc_state,
)


def _entry(item_id: str, title: str, kind: str, **changes) -> dict:
    plural = {
        "Album": "albums",
        "Artist": "artists",
        "Playlist": "playlists",
        "Track": "tracks",
        "Video": "videos",
    }[kind]
    entry = {
        "id": item_id,
        "externalId": f"{plural}:{item_id}",
        "title": title,
        "artist": "Wykonawca",
        "kind": kind,
        "source": "private-playback-handle-must-not-leak",
        "durationTicks": 1_800_000_000,
        "publicUri": f"https://tidal.com/browse/{kind.casefold()}/{item_id}",
        "collectionAddedUtcTicks": 100,
        "isFavorite": False,
        "isInLibrary": False,
        "isAvailable": True,
    }
    entry.update(changes)
    return entry


def _state() -> dict:
    return {
        "tidal": {"cachedCollectionItems": [
            _entry("album-1", "Album pierwszy", "Album", isInLibrary=True,
                   collectionAddedUtcTicks=100,
                   relatedArtistExternalId="artists:7",
                   relatedArtistName="Wykonawca"),
            _entry("playlist-1", "Playlista", "Playlist", isInLibrary=True,
                   collectionAddedUtcTicks=200),
            _entry("artist-1", "Artysta", "Artist", isInLibrary=True,
                   collectionAddedUtcTicks=300),
            _entry("track-1", "Utwór", "Track", isFavorite=True,
                   collectionAddedUtcTicks=250,
                   relatedAlbumExternalId="albums:44",
                   relatedAlbumTitle="Album utworu"),
            _entry("video-1", "Wideo", "Video", isFavorite=True,
                   collectionAddedUtcTicks=350),
            _entry("track-library", "Nie kontener", "Track", isInLibrary=True),
            _entry("album-favorite", "Nie utwór", "Album", isFavorite=True),
            _entry("missing", "Niedostępny", "Album", isInLibrary=True,
                   isAvailable=False),
            {"id": "technical-only", "title": "", "kind": "Track"},
        ]},
        "collectionOrders": {
            "libraryAddedItemIdsBySession": {
                "tidal": ["album-1", "playlist-1", "artist-1"],
            },
            "favoriteAddedItemIdsBySession": {
                "tidal": ["track-1", "video-1"],
            },
            "libraryItemIdsBySession": {
                "tidal|Playlisty": ["playlist-1"],
            },
        },
        "sessionNavigation": {"sessions": {"tidal": {
            "collectionSortModes": {
                "Biblioteka": "AddedNewest",
                "Ulubione": "AddedNewest",
                "Playlisty": "Custom",
            }
        }}},
    }


def test_tidal_items_keep_model_ids_out_of_spoken_data() -> None:
    items = items_from_amc_state(_state())
    assert {item.item_id for item in items} >= {"album-1", "track-1"}

    row = build_view(_state(), VIEW_FAVORITES).rows[-1]
    assert row.title == "Utwór"
    assert row.kind == "track"
    assert row.kind_label == "utwór"
    assert row.show_kind is False
    assert row.detail == "Wykonawca, 3:00"
    assert row.duration_ticks == 1_800_000_000
    assert row.url == "https://tidal.com/browse/track/track-1"
    spoken = " ".join((row.title, row.kind_label, row.detail))
    assert "track-1" not in spoken
    assert "tracks:track-1" not in spoken
    assert "private-playback-handle" not in spoken
    album = next(
        item for item in build_view(_state(), VIEW_LIBRARY).rows
        if item.item_id == "album-1"
    )
    assert album.related_artist_service_id == "artists:7"
    assert album.related_artist_name == "Wykonawca"
    library = ListModel(rows=list(build_view(_state(), VIEW_LIBRARY).rows))
    album_index = next(i for i, item in enumerate(library.rows) if item.item_id == "album-1")
    assert library.text_for(album_index, 1) == ""
    assert library.text_for(album_index, 0) == album.title
    assert library.text_for(album_index, 2) == album.detail
    assert "artists:7" not in " ".join(
        (album.title, album.kind_label, album.detail)
    )


def test_tidal_library_and_favorites_follow_full_amc_membership_semantics() -> None:
    library = build_view(_state(), VIEW_LIBRARY)
    favorites = build_view(_state(), VIEW_FAVORITES)

    assert [row.item_id for row in library.rows] == [
        "artist-1", "playlist-1", "album-1"
    ]
    assert [row.item_id for row in favorites.rows] == ["video-1", "track-1"]
    assert library.order_matches_amc is True
    assert favorites.order_matches_amc is True
    assert "track-library" not in {row.item_id for row in library.rows}
    assert "album-favorite" not in {row.item_id for row in favorites.rows}
    assert "missing" not in {row.item_id for row in library.rows}


def test_tidal_playlist_view_uses_its_own_saved_custom_order() -> None:
    result = build_view(_state(), VIEW_PLAYLISTS)
    assert result.heading == "Playlisty TIDAL"
    assert [row.item_id for row in result.rows] == ["playlist-1"]
    assert result.order_matches_amc is True
    assert result.rows[0].activation_message is None


def test_online_container_payload_is_sanitized_before_becoming_rows() -> None:
    result = container_result_from_host({
        "heading": "Album, Próba",
        "items": [
            {
                "id": "tidal:tracks:1",
                "externalId": "tracks:1",
                "title": "Utwór",
                "artist": "Wykonawca",
                "kind": "track",
                "durationTicks": 1_800_000_000,
                "publicUri": "https://tidal.com/browse/track/1",
                "relatedAlbumExternalId": "albums:44",
                "relatedAlbumTitle": "Album próby",
                "relatedArtistExternalId": "artists:7",
                "relatedArtistName": "Wykonawca",
                "source": "private-handle",
                "token": "secret",
            },
            {"id": "bad", "externalId": "", "title": "Uszkodzony", "kind": "track"},
        ],
    })
    assert result.heading == "Album, Próba"
    assert len(result.rows) == 1
    row = result.rows[0]
    assert row.title == "Utwór"
    assert row.detail == "Wykonawca, 3:00"
    assert row.duration_ticks == 1_800_000_000
    assert row.activation_message is None
    assert row.related_album_service_id == "albums:44"
    assert row.related_album_title == "Album próby"
    assert row.related_artist_service_id == "artists:7"
    assert row.related_artist_name == "Wykonawca"
    assert "tracks:1" not in " ".join((row.title, row.kind_label, row.detail))


def test_artist_overview_has_only_three_user_facing_categories() -> None:
    rows = artist_overview_rows(Row(
        "tidal:artists:7",
        "Artysta",
        "artist",
        service_id="artists:7",
        service_kind="artist",
    ))
    assert [row.title for row in rows] == ["Albumy", "Utwory", "Podobni wykonawcy"]
    assert all(row.kind == "folder" for row in rows)
    assert all("artists:7" not in row.title for row in rows)


def test_malformed_online_payload_has_a_short_user_facing_error() -> None:
    try:
        container_result_from_host({"heading": "Album", "items": "not-a-list"})
    except TidalContainerPayloadError as error:
        assert str(error) == "Katalog TIDAL zwrócił niepełną odpowiedź"
        assert "dict" not in str(error)
    else:
        raise AssertionError("nieprawidłowa odpowiedź powinna zostać odrzucona")


def test_tidal_alphabetical_mode_is_explicitly_marked_as_fallback() -> None:
    raw = _state()
    raw["sessionNavigation"]["sessions"]["tidal"]["collectionSortModes"][
        "Biblioteka"
    ] = "Alphabetical"
    result = build_view(raw, VIEW_LIBRARY)
    assert result.order_matches_amc is False
    assert [row.title for row in result.rows] == sorted(
        [row.title for row in result.rows], key=str.casefold
    )


def test_private_tidal_sort_override_wins_without_writing_shared_profile() -> None:
    raw = _state()
    before = json.dumps(raw, ensure_ascii=False, sort_keys=True)

    alphabetical = build_view(
        raw, VIEW_LIBRARY, sort_mode=SORT_ALPHABETICAL
    )
    custom = build_view(raw, VIEW_LIBRARY, sort_mode=SORT_CUSTOM)

    assert alphabetical.sort_mode == SORT_ALPHABETICAL
    assert [row.title for row in alphabetical.rows] == [
        "Album pierwszy", "Artysta", "Playlista"
    ]
    assert alphabetical.order_matches_amc is False
    assert custom.sort_mode == SORT_CUSTOM
    assert [row.item_id for row in custom.rows] == [
        "album-1", "playlist-1", "artist-1"
    ]
    assert json.dumps(raw, ensure_ascii=False, sort_keys=True) == before


def test_fresh_host_rows_sort_by_added_timestamp_newest_first(tmp_path) -> None:
    raw = _state()
    raw["collectionOrders"] = {}
    layout = private_sandbox(tmp_path)
    layout.state_json.write_text(json.dumps(raw), encoding="utf-8")
    fresh = container_result_from_host({
        "heading": "Biblioteka TIDAL",
        "items": [
            {
                "id": "old", "externalId": "albums:old", "title": "Stary",
                "artist": "Wykonawca", "kind": "album",
                "collectionAddedUtcTicks": 100,
            },
            {
                "id": "new", "externalId": "albums:new", "title": "Nowy",
                "artist": "Wykonawca", "kind": "album",
                "collectionAddedUtcTicks": 300,
            },
        ],
    })

    result = TidalSource(layout).order_rows(
        VIEW_LIBRARY,
        fresh.heading,
        fresh.rows,
        sort_mode=SORT_ADDED_NEWEST,
    )

    assert [row.item_id for row in result.rows] == ["new", "old"]
    assert result.sort_mode == SORT_ADDED_NEWEST
    assert result.order_matches_amc is True
    assert all("100" not in row.detail and "300" not in row.detail for row in result.rows)


def test_tidal_source_reads_profile_without_modifying_it(tmp_path) -> None:
    layout = private_sandbox(tmp_path)
    layout.state_json.write_text(json.dumps(_state()), encoding="utf-8")
    before = layout.state_json.read_bytes()

    result = TidalSource(layout).load_view(VIEW_LIBRARY)

    assert result.rows
    assert layout.state_json.read_bytes() == before


def test_tidal_source_names_profile_failures_for_the_user(tmp_path) -> None:
    source = TidalSource(private_sandbox(tmp_path))
    try:
        source.load_view(VIEW_LIBRARY)
    except TidalProfileError as error:
        assert "profilu AMC" in str(error)
        assert "FileNotFoundError" not in str(error)
    else:
        raise AssertionError("brak profilu powinien być jawną odmową")


def test_private_tidal_queue_round_trip_keeps_ids_out_of_labels() -> None:
    row = Row(
        "tidal:tracks:secret",
        "Utwór próby",
        "track",
        service_id="tracks:secret",
        service_kind="track",
        artist_name="Wykonawca",
        related_album_service_id="albums:44",
        related_album_title="Album",
        related_artist_service_id="artists:7",
        related_artist_name="Wykonawca",
    )

    restored = queue_rows(queue_payload([row]))

    assert len(restored) == 1
    assert restored[0].title == "Utwór próby"
    assert restored[0].detail == "Wykonawca"
    assert restored[0].related_artist_service_id == "artists:7"
    assert restored[0].related_artist_name == "Wykonawca"
    assert "secret" not in restored[0].title
    assert "secret" not in restored[0].detail

