"""History destinations and clipboard data stay tied to the selected service."""

from __future__ import annotations

from types import SimpleNamespace

import amc_wx_lite.gui as gui
from amc_wx_lite.gui import LiteFrame, RelationDestinationWindow
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    LibraryView, Navigator, OpenTidalContainer, SessionId, ShowTidalRelations,
    TidalRelationChoice, View,
)
from amc_wx_lite.playback_history import history_rows as tidal_history_rows
from amc_wx_lite.playback_history import record_row as record_tidal_row
from amc_wx_lite.service_links import exact_service_link
from amc_wx_lite.shortcuts import Action, Chord, describe, resolve
from amc_wx_lite.spotify_source import history_rows as spotify_history_rows
from amc_wx_lite.spotify_source import record_history_row as record_spotify_row


def test_tidal_history_relations_speak_names_and_copy_exact_targets() -> None:
    track = Row(
        "tidal-track", "Wszystko się może zdarzyć", "track",
        service_id="tracks:123", service_kind="track",
        artist_name="Anita Lipnicka",
        related_album_service_id="albums:456",
        related_album_title="Wszystko się może zdarzyć",
        related_artist_service_id="artists:789",
        related_artist_name="Anita Lipnicka",
    )
    restored = tidal_history_rows(
        "tidal", record_tidal_row([], track, replay_supported=True)
    )[0]
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_service_history(SessionId.TIDAL, [restored])
    choices = nav.tidal_relations()[0].choices
    labels, names, links = gui.relation_destination_details(
        choices, service="tidal", detailed_hints=False,
    )
    assert labels == (
        "Wszystko się może zdarzyć, album, Anita Lipnicka",
        "Anita Lipnicka, wykonawca",
    )
    assert names == ("Wszystko się może zdarzyć", "Anita Lipnicka")
    assert links == (
        "https://tidal.com/browse/album/456",
        "https://tidal.com/browse/artist/789",
    )
    assert exact_service_link("tidal", restored.kind, restored.service_id, restored.url) == (
        "https://tidal.com/browse/track/123"
    )


def test_spotify_history_relations_and_episode_source() -> None:
    track = Row(
        "spotify-track", "Piosenka", "track",
        service_id="spotify:track:abc123", service_kind="track",
        artist_name="Anita Lipnicka",
        related_album_service_id="spotify:album:album123",
        related_album_title="Wszystko się może zdarzyć",
        related_artist_service_id="spotify:artist:artist123",
        related_artist_name="Anita Lipnicka",
    )
    restored = spotify_history_rows(record_spotify_row([], track))[0]
    nav = Navigator()
    nav.active = SessionId.SPOTIFY
    nav.apply_spotify_history([restored])
    choices = nav.spotify_relations()[0].choices
    labels, names, links = gui.relation_destination_details(
        choices, service="spotify", detailed_hints=False,
    )
    assert labels == (
        "Wszystko się może zdarzyć, album, Anita Lipnicka",
        "Anita Lipnicka, wykonawca",
    )
    assert names == ("Wszystko się może zdarzyć", "Anita Lipnicka")
    assert links == (
        "https://open.spotify.com/album/album123",
        "https://open.spotify.com/artist/artist123",
    )
    assert exact_service_link("spotify", restored.kind, restored.service_id, restored.url) == (
        "https://open.spotify.com/track/abc123"
    )

    episode = Row(
        "episode", "Odcinek", "episode", service_id="spotify:episode:ep123",
        related_podcast_service_id="spotify:show:show123",
        related_podcast_title="Rozmowy",
    )
    nav.apply_spotify_history(list(spotify_history_rows(record_spotify_row([], episode))))
    podcast_choices = nav.spotify_relations()[0].choices
    podcast_labels, _, podcast_links = gui.relation_destination_details(
        podcast_choices, service="spotify", detailed_hints=False,
    )
    assert podcast_labels == ("Rozmowy, podcast",)
    assert podcast_links == ("https://open.spotify.com/show/show123",)


def test_links_require_service_identity_and_never_guess_unknown_provider() -> None:
    assert exact_service_link("tidal", "album", "spotify:album:abc") == ""
    assert exact_service_link("spotify", "album", "spotify:track:abc") == ""
    assert exact_service_link(
        "tidal", "album", "albums:456", "https://open.spotify.com/album/abc"
    ) == "https://tidal.com/browse/album/456"
    assert exact_service_link("future", "album", "opaque") == ""
    assert exact_service_link(
        "future", "album", "opaque", "https://music.example/album/opaque"
    ) == "https://music.example/album/opaque"


def test_single_tidal_destination_waits_for_enter() -> None:
    choice = TidalRelationChoice(
        "Przejdź do wykonawcy",
        OpenTidalContainer(
            "artist", "artists:789", "Anita Lipnicka", "artist",
        ),
    )
    opened: list[dict] = []
    activated: list[object] = []
    frame = SimpleNamespace(
        _detailed_hints=False,
        _show_relation_destination_window=lambda **kwargs: opened.append(kwargs),
        navigator=SimpleNamespace(
            activate_tidal_relation=lambda selected: activated.append(selected) or [],
        ),
        _run=lambda _events: None,
    )
    LiteFrame._show_tidal_relations(frame, ShowTidalRelations("Przejdź do", (choice,)))
    assert activated == []
    assert opened[0]["labels"] == ("Anita Lipnicka, wykonawca",)
    assert opened[0]["names"] == ("Anita Lipnicka",)
    assert opened[0]["links"] == ("https://tidal.com/browse/artist/789",)
    opened[0]["activate"](0)
    assert activated == [choice]


def test_history_main_list_copies_title_and_exact_track_link() -> None:
    copied: list[str] = []
    spoken: list[str] = []
    row = Row(
        "track", "Piosenka", "track", service_id="spotify:track:abc123",
    )
    session = SimpleNamespace(view=View.LIST, library_view=LibraryView.SPOTIFY_HISTORY)
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SPOTIFY, session=session),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [row],
        _in_download_history=lambda: False,
        _clear_pending_external_moves=lambda: None,
        _file_drop_path=lambda _address: None,
        _to_clipboard=lambda value, **_kwargs: copied.append(value) or True,
    )
    LiteFrame._copy_name(frame)
    LiteFrame._copy_address(frame)
    assert copied == ["Piosenka", "https://open.spotify.com/track/abc123"]
    assert spoken == ["Skopiowano nazwę", "Skopiowano link Spotify"]


def test_history_main_list_reports_missing_exact_link_without_copying() -> None:
    copied: list[str] = []
    spoken: list[str] = []
    row = Row("track", "Piosenka", "track", service_id="unknown")
    session = SimpleNamespace(view=View.LIST, library_view=LibraryView.SERVICE_HISTORY)
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.TIDAL, session=session),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [row],
        _file_drop_path=lambda _address: None,
        _to_clipboard=lambda value, **_kwargs: copied.append(value) or True,
    )
    LiteFrame._copy_address(frame)
    assert copied == []
    assert spoken == ["Ten element nie ma dokładnego linku"]


def test_destination_copy_uses_name_or_link_without_exposing_identifier(monkeypatch) -> None:
    copied: list[str] = []
    spoken: list[str] = []
    monkeypatch.setattr(gui, "announce_status", lambda _status, value: spoken.append(value))
    window = SimpleNamespace(
        selected_index=0,
        _names=("Wszystko się może zdarzyć",),
        _links=("https://tidal.com/browse/album/456",),
        _service="tidal",
        _copy_to_clipboard=lambda value: copied.append(value) or True,
        destinations=SimpleNamespace(SetFocus=lambda: None),
        status=object(),
    )
    RelationDestinationWindow._copy_selected(window, link=False)
    RelationDestinationWindow._copy_selected(window, link=True)
    assert copied == [
        "Wszystko się może zdarzyć",
        "https://tidal.com/browse/album/456",
    ]
    assert spoken == ["Skopiowano nazwę", "Skopiowano link TIDAL"]
    window._links = ("",)
    RelationDestinationWindow._copy_selected(window, link=True)
    assert spoken[-1] == "Ten cel nie ma dokładnego linku"


def test_destination_keyboard_routes_both_copy_shortcuts_once() -> None:
    calls: list[bool] = []
    window = SimpleNamespace(_copy_selected=lambda *, link: calls.append(link))

    def key_event(*, shift: bool) -> SimpleNamespace:
        return SimpleNamespace(
            GetKeyCode=lambda: ord("C"),
            GetRawKeyCode=lambda: ord("C"),
            ControlDown=lambda: True,
            ShiftDown=lambda: shift,
            AltDown=lambda: False,
        )

    RelationDestinationWindow._on_key(window, key_event(shift=False))
    RelationDestinationWindow._on_key(window, key_event(shift=True))
    assert calls == [False, True]


def test_podcast_right_arrow_matches_help_and_preserves_player_seek() -> None:
    assert LiteFrame._menu_shortcut_text("Right") == "strzałka w prawo"
    assert resolve(
        Chord("Right"), player_view=False, radio_session=False,
        podcast_session=True,
    ) is Action.GO_TO_RELATED_PODCAST
    assert resolve(
        Chord("Right"), player_view=True, radio_session=False,
        podcast_session=True,
    ) is Action.SEEK_FORWARD_10
    help_entries = dict(describe("Podcasty i YouTube"))
    assert "Strzałka w prawo (Podcasty i YouTube)" in help_entries
    assert "podcast lub kanał" in help_entries[
        "Strzałka w prawo (Podcasty i YouTube)"
    ]
