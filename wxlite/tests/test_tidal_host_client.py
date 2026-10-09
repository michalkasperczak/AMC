"""Narrow Python-to-host contract for online TIDAL navigation."""

from pathlib import Path

from amc_wx_lite.host_client import LiteHostClient


def test_tidal_container_request_contains_no_credentials_or_playback_source() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))

    def call(op, args=None, *, timeout=20.0):
        calls.append((op, args, timeout))
        return {"heading": "Album, Próba", "items": []}

    client.call = call  # type: ignore[method-assign]
    result = client.tidal_container_items(
        item_id="tidal:albums:123",
        external_id="albums:123",
        title="Próba",
        kind="album",
        artist="Wykonawca",
        public_uri="https://tidal.com/browse/album/123",
    )

    assert result == {"heading": "Album, Próba", "items": []}
    assert calls == [(
        "tidal.containerItems",
        {
            "itemId": "tidal:albums:123",
            "externalId": "albums:123",
            "title": "Próba",
            "kind": "album",
            "artist": "Wykonawca",
            "publicUri": "https://tidal.com/browse/album/123",
        },
        120.0,
    )]
    serialized = repr(calls)
    assert "token" not in serialized.casefold()
    assert "source" not in serialized.casefold()


def test_tidal_artist_section_is_an_explicit_model_value() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.tidal_container_items(
        item_id="tidal-section:artist:albums",
        external_id="artists:7",
        title="Artysta",
        kind="artist",
        artist_section="albums",
    )

    assert calls[0][1]["artistSection"] == "albums"


def test_tidal_desktop_play_sends_only_catalog_identity_and_restart_choice() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {"success": True}
    )

    client.tidal_desktop_play(
        item_id="tidal:tracks:1",
        external_id="tracks:1",
        title="Utwór",
        related_album_external_id="albums:44",
        restart_consent=True,
    )

    assert calls == [(
        "tidal.desktopPlay",
        {
            "itemId": "tidal:tracks:1",
            "externalId": "tracks:1",
            "title": "Utwór",
            "relatedAlbumExternalId": "albums:44",
            "restartConsent": True,
        },
        90.0,
    )]
    serialized = repr(calls).casefold()
    assert "token" not in serialized
    assert "credential" not in serialized


def test_tidal_desktop_play_container_names_only_album_or_playlist() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {"success": True}
    )

    client.tidal_desktop_play_container(
        item_id="tidal:playlists:7",
        external_id="playlists:7",
        title="Playlista próby",
        kind="playlist",
        restart_consent=False,
    )

    assert calls == [(
        "tidal.desktopPlay",
        {
            "itemId": "tidal:playlists:7",
            "externalId": "playlists:7",
            "title": "Playlista próby",
            "kind": "playlist",
            "restartConsent": False,
        },
        90.0,
    )]


def test_tidal_external_transport_is_a_named_narrow_operation() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {"handled": True}
    )

    client.tidal_external_transport("toggle")

    assert calls == [(
        "tidal.externalTransport", {"command": "toggle"}, 15.0
    )]


def test_tidal_external_state_uses_the_same_narrow_transport_boundary() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {"hasSession": True}
    )

    result = client.tidal_external_state()

    assert result == {"hasSession": True}
    assert calls == [(
        "tidal.externalTransport", {"command": "state"}, 15.0
    )]


def test_tidal_collection_calls_never_send_credentials() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.tidal_collection_view("favorites")
    client.tidal_collection_membership([{
        "itemId": "tidal:tracks:1",
        "externalId": "tracks:1",
        "title": "Utwór",
        "kind": "track",
    }], mode="favorite")

    assert calls == [
        ("tidal.collectionView", {"view": "favorites"}, 180.0),
        (
            "tidal.collectionMembership",
            {
                "mode": "favorite",
                "items": [{
                    "itemId": "tidal:tracks:1",
                    "externalId": "tracks:1",
                    "title": "Utwór",
                    "kind": "track",
                }],
            },
            120.0,
        ),
    ]
    assert "token" not in repr(calls).casefold()
    assert "credential" not in repr(calls).casefold()
