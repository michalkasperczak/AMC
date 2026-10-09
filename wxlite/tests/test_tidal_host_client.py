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
