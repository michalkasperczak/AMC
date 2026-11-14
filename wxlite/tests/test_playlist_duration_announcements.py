"""Spoken duration after opening playlists in every supported session."""

from __future__ import annotations

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Navigator, OpenAppleMusicPlaylist, OpenSpotifyContainer,
    OpenTidalContainer, SessionId,
)


MINUTE = 60 * 10_000_000


def test_podcast_playlist_announces_total():
    nav = Navigator()
    nav.active = SessionId.PODCASTS
    rows = [Row("one", "Odcinek 1", "episode", duration_ticks=2 * MINUTE),
            Row("two", "Odcinek 2", "episode", duration_ticks=3 * MINUTE)]
    spoken = nav.apply_podcast_playlist_contents("p", "Moja", rows)[0].text
    assert spoken == "Playlista — Moja, 2 pozycje, łączny czas trwania 5 min 0 s"


def test_spotify_playlist_announces_loaded_part_only():
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    intent = OpenSpotifyContainer("playlist:p", "p", "Moja", "playlist")
    rows = [Row("one", "Utwór", "track", duration_ticks=2 * MINUTE),
            Row("more", "Załaduj więcej", "loadMore")]
    spoken = nav.apply_spotify_container(intent, "Moja", rows)[0].text
    assert "wczytana część playlisty: łączny czas trwania 2 min 0 s" in spoken


def test_tidal_playlist_announces_unknown_length_honestly():
    nav = Navigator()
    nav.active = SessionId.TIDAL
    intent = OpenTidalContainer("playlist:p", "p", "Moja", "playlist")
    spoken = nav.apply_tidal_container(intent, "Moja", [Row("one", "Utwór", "track")])[0].text
    assert spoken == "Moja, 1 pozycja, czas trwania nieznany"


def test_apple_music_playlist_announces_known_length():
    nav = Navigator()
    nav.active = SessionId.APPLE_MUSIC
    intent = OpenAppleMusicPlaylist("playlist:p", "Moja")
    spoken = nav.apply_apple_music_playlist(
        intent, "Playlista: Moja", [Row("one", "Utwór", "track", duration_ticks=2 * MINUTE)]
    )[0].text
    assert spoken == "Playlista: Moja, 1 pozycja, łączny czas trwania 2 min 0 s"
