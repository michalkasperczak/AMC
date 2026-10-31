from __future__ import annotations

import io
import json
from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite import apple_music_links, gui as gui_module, radio_recognition
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import SessionId, View
from amc_wx_lite.recognition_actions import (
    APPLE_MUSIC,
    SPOTIFY,
    TIDAL,
    action_url,
    all_actions,
    cached_matches,
    match_from_row,
    quick_actions,
    select_catalog_match,
    store_match,
)
from amc_wx_lite.state_store import LiteState, Options, StateStore


GUI_SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


def _entry() -> dict:
    return {
        "id": "recognized-1",
        "title": "Kto Dał Nam Deszcz (1980)",
        "artist": "Krystyna Prońko",
        "album": "Osobista Kolekcja 2",
        "stationName": "Poznań",
        "recognizedUtcTicks": 638000000000000000,
    }


def _spotify_row() -> Row:
    return Row(
        item_id="spotify-track-1",
        title="Kto Dał Nam Deszcz (1980)",
        kind="track",
        url="https://open.spotify.com/track/track-1",
        service_id="track-1",
        service_kind="track",
        artist_name="Krystyna Pronko",
        related_album_service_id="album-1",
        related_album_title="Osobista Kolekcja 2",
        related_artist_service_id="artist-1",
        related_artist_name="Krystyna Prońko",
    )


def test_shared_action_model_keeps_right_arrow_short_and_context_complete() -> None:
    entry = _entry()
    quick = quick_actions(entry, TIDAL)
    complete = all_actions(entry, TIDAL)
    assert quick
    assert set(quick).issubset(set(complete))
    assert quick[0].label == "Krystyna Prońko — wykonawca"
    assert quick[1].label == "Osobista Kolekcja 2 — album"
    labels = {action.label for action in complete}
    assert "Spotify — pokaż utwór w AMC" in labels
    assert "TIDAL — pokaż utwór w AMC" in labels
    assert "Apple Music — wyszukaj w przeglądarce" in labels


def test_public_actions_offer_web_search_but_never_private_amc_sessions() -> None:
    entry = _entry()
    quick = quick_actions(entry, TIDAL, internal_services=())
    complete = all_actions(entry, TIDAL, internal_services=())
    labels = {action.label for action in complete}

    assert quick
    assert set(quick).issubset(set(complete))
    assert not any(action.operation in {"show_amc", "play_default"} for action in complete)
    assert "Spotify — wyszukaj na stronie" in labels
    assert "TIDAL — wyszukaj na stronie" in labels
    assert "Spotify — pokaż utwór w AMC" not in labels
    assert "TIDAL — pokaż utwór w AMC" not in labels
    assert any(
        action.service == SPOTIFY
        and action.operation == "open_url"
        and action.url.startswith("https://open.spotify.com/search/")
        for action in quick
    )
    assert any(
        action.service == TIDAL
        and action.operation == "open_url"
        and action.url.startswith("https://listen.tidal.com/search?")
        for action in quick
    )


def test_right_arrow_actions_match_concise_spotify_tidal_window_contract() -> None:
    source = GUI_SOURCE.read_text(encoding="utf-8")
    window = source.split("class RecognitionActionsWindow", 1)[1].split(
        "class RadioRecognitionHistoryDialog", 1
    )[0]
    assert "(wx.Frame):" in window
    assert "wx.CallAfter(self.actions.SetFocus)" in window
    assert 'self.actions.SetHelpText(hint if detailed_hints else "")' in window
    assert "wx.StaticText(panel, label=hint)" not in window
    assert "ShowModal" not in window

    history = source.split("class RadioRecognitionHistoryDialog", 1)[1].split(
        "class GeneralPlaybackOptionsDialog", 1
    )[0]
    assert "detailed_hints=self._detailed_hints" in history
    assert "actions_window.show_for_keyboard()" in history
    assert "internal_services=self._internal_services" in history


def test_copy_confirmations_use_the_active_recognition_window_live_region() -> None:
    source = GUI_SOURCE.read_text(encoding="utf-8")
    window = source.split("class RecognitionActionsWindow", 1)[1].split(
        "class RadioRecognitionHistoryDialog", 1
    )[0]
    history = source.split("class RadioRecognitionHistoryDialog", 1)[1].split(
        "class GeneralPlaybackOptionsDialog", 1
    )[0]

    for section in (window, history):
        assert "announce_status(self.status, text)" in section
        assert "self._announce(text)" not in section

    assert 'self._set_status("Skopiowano nazwę działania")' in window
    assert 'self._set_status("Skopiowano łącze")' in window
    assert '"Skopiowano opis" if len(selected) == 1' in history
    assert 'f"Skopiowano link {recognition_service_label(action.service)}"' in history
    assert 'f"Skopiowano link wyszukiwania "' in history


def test_cached_copy_link_does_not_precede_confirmation_with_lookup_message() -> None:
    source = GUI_SOURCE.read_text(encoding="utf-8")
    window = source.split("class RecognitionActionsWindow", 1)[1].split(
        "class RadioRecognitionHistoryDialog", 1
    )[0]
    history = source.split("class RadioRecognitionHistoryDialog", 1)[1].split(
        "class GeneralPlaybackOptionsDialog", 1
    )[0]

    assert "recognition_cached_matches(self._entry).get(action.service) is None" in window
    assert "Sprawdzam dokładne łącze do utworu" not in history


def test_escape_returns_to_the_same_session_and_view_that_opened_history() -> None:
    events: list[str] = []

    class FocusTarget:
        def __init__(self, name: str) -> None:
            self.name = name

        def SetFocus(self) -> None:
            events.append(f"focus:{self.name}")

    class CancelledHistoryDialog:
        def __init__(self, *_args, **_kwargs) -> None:
            self.changed = False
            self.requested_action = None

        def ShowModal(self) -> int:
            events.append("escape")
            return 0

        def Destroy(self) -> None:
            events.append("destroy")

    original = gui_module.RadioRecognitionHistoryDialog
    gui_module.RadioRecognitionHistoryDialog = CancelledHistoryDialog
    try:
        for session, view, focus_name in (
            (SessionId.FILES, View.LIST, "files"),
            (SessionId.FILES, View.PLAYER, "player"),
            (SessionId.RADIO, View.LIST, "radio"),
            (SessionId.RADIO, View.PLAYER, "player"),
        ):
            events.clear()
            selected_row = object()
            session_state = SimpleNamespace(
                view=view,
                model=SimpleNamespace(selected_row=selected_row),
            )
            frame = SimpleNamespace(
                navigator=SimpleNamespace(
                    active=session,
                    session=session_state,
                ),
                state=SimpleNamespace(recognized_tracks=[]),
                options=SimpleNamespace(
                    radio_recognition_default_service=TIDAL,
                ),
                runner=SimpleNamespace(submit=lambda *_args, **_kwargs: None),
                files_list=FocusTarget("files"),
                radio_list=FocusTarget("radio"),
                play_button=FocusTarget("player"),
                _detailed_hints=False,
                _to_clipboard=lambda _text: True,
                _resolve_recognition_match=lambda *_args: None,
                _save_state=lambda: None,
                _execute_recognition_action=lambda *_args: None,
            )
            frame._active_list = LiteFrame._active_list.__get__(frame, type(frame))
            frame._restore_focus_after_dialog = (
                LiteFrame._restore_focus_after_dialog.__get__(frame, type(frame))
            )

            LiteFrame._show_radio_recognition_history(frame)

            assert frame.navigator.active is session
            assert frame.navigator.session.view is view
            assert frame.navigator.session.model.selected_row is selected_row
            assert events == ["escape", "destroy", f"focus:{focus_name}"]
    finally:
        gui_module.RadioRecognitionHistoryDialog = original


def test_catalogue_match_is_conservative_and_diacritic_tolerant() -> None:
    selected = select_catalog_match(_entry(), [
        {"title": "Inny utwór", "artist": "Krystyna Prońko", "album": "Album"},
        {
            "title": "Kto Dal Nam Deszcz 1980",
            "artist": "Krystyna Pronko",
            "album": "Osobista Kolekcja 2",
            "publicUrl": "https://example.test/right",
        },
    ])
    assert selected is not None
    assert selected["publicUrl"].endswith("/right")


def test_exact_service_match_round_trips_through_state(tmp_path) -> None:
    entry = _entry()
    match = match_from_row(SPOTIFY, _spotify_row())
    assert store_match(entry, match)
    assert action_url(
        next(action for action in all_actions(entry, SPOTIFY)
             if action.action_id == "spotify.amc.album"),
        match,
    ) == "https://open.spotify.com/album/album-1"
    store = StateStore(tmp_path)
    store.save(LiteState(
        options=Options(radio_recognition_default_service=TIDAL),
        recognized_tracks=[entry],
    ))
    loaded = store.load()
    assert loaded.options.radio_recognition_default_service == TIDAL
    assert cached_matches(loaded.recognized_tracks[0])[SPOTIFY]["serviceId"] == "track-1"


def test_apple_music_lookup_is_anonymous_and_returns_public_links() -> None:
    payload = json.dumps({"results": [{
        "trackName": "Kto Dal Nam Deszcz 1980",
        "artistName": "Krystyna Pronko",
        "collectionName": "Osobista Kolekcja 2",
        "trackId": 101,
        "collectionId": 202,
        "trackViewUrl": "https://music.apple.com/pl/song/example/101",
        "collectionViewUrl": "https://music.apple.com/pl/album/example/202",
    }]}).encode("utf-8")
    seen = {}

    class Response(io.BytesIO):
        def __enter__(self):
            return self

        def __exit__(self, *_args):
            self.close()

    def opener(request, *, timeout):
        seen["url"] = request.full_url
        seen["authorization"] = request.get_header("Authorization")
        seen["timeout"] = timeout
        return Response(payload)

    match = apple_music_links.resolve_public_links(_entry(), opener=opener)
    assert match is not None
    assert match["service"] == APPLE_MUSIC
    assert match["albumUrl"].endswith("/202")
    assert "itunes.apple.com/search" in seen["url"]
    assert seen["authorization"] is None


def test_clipboard_description_remains_multiline_and_uses_cached_exact_link() -> None:
    entry = _entry()
    store_match(entry, match_from_row(SPOTIFY, _spotify_row()))
    description = radio_recognition.copy_text(entry)
    assert "\nWykonawca: Krystyna Prońko" in description
    assert radio_recognition.service_links(entry)["Spotify"].endswith("/track/track-1")
