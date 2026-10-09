"""Persisted TIDAL catalogue: membership, ordering and accessible labels."""

from __future__ import annotations

import json

from amc_wx_lite.profile_layout import private_sandbox
from amc_wx_lite.tidal_source import (
    TidalProfileError,
    TidalSource,
    VIEW_FAVORITES,
    VIEW_LIBRARY,
    VIEW_PLAYLISTS,
    build_view,
    items_from_amc_state,
)


def _entry(item_id: str, title: str, kind: str, **changes) -> dict:
    entry = {
        "id": item_id,
        "externalId": "opaque-service-id",
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
                   collectionAddedUtcTicks=100),
            _entry("playlist-1", "Playlista", "Playlist", isInLibrary=True,
                   collectionAddedUtcTicks=200),
            _entry("artist-1", "Artysta", "Artist", isInLibrary=True,
                   collectionAddedUtcTicks=300),
            _entry("track-1", "Utwór", "Track", isFavorite=True,
                   collectionAddedUtcTicks=250),
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
    assert row.detail == "Wykonawca, 3:00"
    assert row.url == "https://tidal.com/browse/track/track-1"
    spoken = " ".join((row.title, row.kind_label, row.detail))
    assert "track-1" not in spoken
    assert "opaque-service-id" not in spoken
    assert "private-playback-handle" not in spoken


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
    assert result.rows[0].activation_message


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

