from __future__ import annotations

import io
import json
from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite import apple_music_links, gui as gui_module, radio_recognition
from amc_wx_lite.gui import LiteFrame, RadioRecognitionHistoryDialog, RecognitionActionsWindow
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Navigator, SessionId, View
from amc_wx_lite.recognition_actions import (
    APPLE_MUSIC,
    SPOTIFY,
    TIDAL,
    action_url,
    all_actions,
    cached_matches,
    match_from_row,
    membership_row,
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
    assert [action.action_id for action in quick[:3]] == [
        "tidal.amc.track", "spotify.amc.track", "apple.copy.track"
    ]
    assert quick[0].label == "Kto Dał Nam Deszcz (1980), Krystyna Prońko, utwór — znajdź w TIDAL"
    assert quick[1].label == "Kto Dał Nam Deszcz (1980), Krystyna Prońko, utwór — znajdź w Spotify"
    assert quick[2].label == "Kto Dał Nam Deszcz (1980), Krystyna Prońko, utwór — kopiuj link Apple Music"
    assert any("Osobista Kolekcja 2, album" in action.label for action in quick)
    labels = {action.label for action in complete}
    assert quick[0].label in labels
    assert quick[1].label in labels
    assert "Apple Music — wyszukaj w przeglądarce" in labels


def test_public_profile_omits_apple_music_from_recognition_actions() -> None:
    entry = _entry()
    quick = quick_actions(entry, TIDAL, apple_music_enabled=False)
    complete = all_actions(entry, TIDAL, apple_music_enabled=False)
    assert quick
    assert set(quick).issubset(set(complete))
    assert all(action.service != APPLE_MUSIC for action in complete)
    assert all("Apple Music" not in action.label for action in complete)
    assert any(action.service == SPOTIFY for action in complete)
    assert any(action.service == TIDAL for action in complete)


def test_recognized_membership_uses_album_for_library_and_track_for_favorites() -> None:
    for service in (SPOTIFY, TIDAL):
        match = match_from_row(service, _spotify_row())
        action = next(item for item in quick_actions(_entry(), service)
                      if item.service == service and item.entity == "track")
        album = membership_row(action, match, favorite=False)
        track = membership_row(action, match, favorite=True)
        assert album is not None and album.kind == "album"
        assert album.service_id == "album-1"
        assert track is not None and track.kind == "track"
        assert track.service_id == "track-1"
        assert membership_row(action, None, favorite=True) is None
        assert membership_row(action, match_from_row(
            TIDAL if service == SPOTIFY else SPOTIFY, _spotify_row()
        ), favorite=True) is None


def test_recognized_match_rejects_other_album_and_ambiguous_editions() -> None:
    known = _entry()
    wrong_album = {"title": known["title"], "artist": known["artist"],
                   "album": "Completely Different Album", "serviceId": "one"}
    assert select_catalog_match(known, [wrong_album]) is None
    unknown_album = {"title": known["title"], "artist": known["artist"]}
    editions = [
        {"title": known["title"], "artist": known["artist"],
         "serviceId": identity, "album": album}
        for identity, album in (("one", "Album A"), ("two", "Album B"))
    ]
    assert select_catalog_match(unknown_album, editions) is None


def test_recognized_shortcuts_mutate_selected_service_without_opening_it() -> None:
    calls = []
    for service in (SPOTIFY, TIDAL):
        action = next(item for item in quick_actions(_entry(), service)
                      if item.service == service and item.entity == "track")
        match = match_from_row(service, _spotify_row())
        window = SimpleNamespace(
            _selected_action=lambda: action,
            _entry=_entry(), _closing=False,
            _set_status=lambda message: calls.append(("status", message)),
            _membership_status=lambda message: calls.append(("confirmed", message)),
            _resolve_action=lambda _entry, _action, ready, _failed: ready(match),
            _change_membership_action=lambda selected, resolved, **options:
                calls.append((selected.service, resolved["serviceId"],
                              options["favorite"])),
        )
        RecognitionActionsWindow._change_membership(window, favorite=False)
        RecognitionActionsWindow._change_membership(window, favorite=True)
    assert calls == [
        (SPOTIFY, "track-1", False), (SPOTIFY, "track-1", True),
        (TIDAL, "track-1", False), (TIDAL, "track-1", True),
    ]


def test_recognized_collection_command_uses_confirmed_search_mutation() -> None:
    calls = []
    frame = SimpleNamespace(
        _complete_remote_search_result_action=lambda entries, action, **options:
            calls.append((entries[0].session_id, entries[0].row.kind,
                          entries[0].row.service_id, action.name,
                          options["announce"])),
    )
    say = lambda message: calls.append(("spoken", message))
    for service, session in ((SPOTIFY, SessionId.SPOTIFY),
                             (TIDAL, SessionId.TIDAL)):
        action = next(item for item in quick_actions(_entry(), service)
                      if item.service == service and item.entity == "track")
        match = match_from_row(service, _spotify_row())
        LiteFrame._change_recognition_membership(
            frame, action, match, favorite=False, announce=say
        )
        LiteFrame._change_recognition_membership(
            frame, action, match, favorite=True, announce=say
        )
        assert calls[-2:] == [
            (session, "album", "album-1", "LIBRARY", say),
            (session, "track", "track-1", "FAVORITE", say),
        ]


def test_right_arrow_uses_track_before_album_and_never_guesses_album_from_title() -> None:
    entry = {"title": "Mosty", "artist": "Anita Lipnicka"}
    actions = quick_actions(entry, TIDAL)
    assert [action.action_id for action in actions[:3]] == [
        "tidal.amc.track", "spotify.amc.track", "apple.copy.track"
    ]
    assert "Mosty, Anita Lipnicka" in actions[0].label
    assert not any(action.entity == "album" for action in actions)
    assert any(action.entity == "artist" for action in actions)
    assert [action.service for action in quick_actions(entry, SPOTIFY)[:3]] == [
        SPOTIFY, TIDAL, APPLE_MUSIC
    ]


def test_album_choice_uses_only_recognition_field_not_service_guess() -> None:
    entry = _entry()
    del entry["album"]
    cached = match_from_row(SPOTIFY, _spotify_row())
    assert store_match(entry, cached)
    assert not any(action.entity == "album" for action in quick_actions(entry, TIDAL))
    assert not any(action.entity == "album" for action in quick_actions(entry, SPOTIFY))
    entry["album"] = "Inny album z rozpoznawania"
    assert any(
        action.entity == "album" and "Inny album z rozpoznawania" in action.label
        for action in quick_actions(entry, TIDAL)
    )


def test_public_actions_offer_web_search_but_never_private_amc_sessions() -> None:
    entry = _entry()
    quick = quick_actions(entry, TIDAL, internal_services=())
    complete = all_actions(entry, TIDAL, internal_services=())
    labels = {action.label for action in complete}

    assert quick
    assert set(quick).issubset(set(complete))
    assert not any(action.operation in {"show_amc", "play_default"} for action in complete)
    assert [action.service for action in quick[:3]] == [TIDAL, SPOTIFY, APPLE_MUSIC]
    assert any("wyszukaj na stronie Spotify" in label for label in labels)
    assert any("wyszukaj na stronie TIDAL" in label for label in labels)
    assert not any(action.operation == "show_amc" for action in complete)
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
        "class RadioClipDialog", 1
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
    assert 'f"Skopiowano link {service}"' in window
    assert '"Skopiowano opis" if len(selected) == 1' in history
    assert 'f"Skopiowano link {service}"' in history
    assert 'f"Skopiowano link wyszukiwania {service}"' in history


def test_cached_copy_link_does_not_precede_confirmation_with_lookup_message() -> None:
    source = GUI_SOURCE.read_text(encoding="utf-8")
    window = source.split("class RecognitionActionsWindow", 1)[1].split(
        "class RadioRecognitionHistoryDialog", 1
    )[0]
    history = source.split("class RadioRecognitionHistoryDialog", 1)[1].split(
        "class GeneralPlaybackOptionsDialog", 1
    )[0]

    assert "Sprawdzam dokładne łącze" not in window
    assert "Sprawdzam dokładne łącze do utworu" not in history


def test_right_arrow_copy_uses_exact_album_link_without_preparation_message() -> None:
    action = next(
        item for item in all_actions(_entry(), SPOTIFY)
        if item.action_id == "spotify.amc.album"
    )
    copied, spoken = [], []
    window = SimpleNamespace(
        _entry=_entry(),
        _actions=[action],
        _closing=False,
        actions=SimpleNamespace(GetSelection=lambda: 0, SetFocus=lambda: None),
        _copy_to_clipboard=lambda link: copied.append(link) or True,
        _set_status=spoken.append,
        _resolve_action=lambda _entry, _action, ready, _failed: ready(
            match_from_row(SPOTIFY, _spotify_row())
        ),
    )
    window._selected_action = RecognitionActionsWindow._selected_action.__get__(
        window, type(window)
    )

    RecognitionActionsWindow._copy_link(window)

    assert copied == ["https://open.spotify.com/album/album-1"]
    assert spoken == ["Skopiowano link Spotify"]


def test_right_arrow_copy_falls_back_to_named_album_search_if_exact_match_fails() -> None:
    action = next(
        item for item in all_actions(_entry(), TIDAL)
        if item.action_id == "tidal.amc.album"
    )
    copied, spoken = [], []
    window = SimpleNamespace(
        _entry=_entry(),
        _actions=[action],
        _closing=False,
        actions=SimpleNamespace(GetSelection=lambda: 0, SetFocus=lambda: None),
        _copy_to_clipboard=lambda link: copied.append(link) or True,
        _set_status=spoken.append,
        _resolve_action=lambda _entry, _action, _ready, failed: failed(
            LookupError("missing exact match")
        ),
    )
    window._selected_action = RecognitionActionsWindow._selected_action.__get__(
        window, type(window)
    )

    RecognitionActionsWindow._copy_link(window)

    assert len(copied) == 1
    assert "Osobista+Kolekcja+2" in copied[0]
    assert spoken == ["Skopiowano link wyszukiwania TIDAL"]


def test_history_copy_reports_exact_or_search_link_for_both_services() -> None:
    for service, label in ((SPOTIFY, "Spotify"), (TIDAL, "TIDAL")):
        entry = _entry()
        match = match_from_row(service, _spotify_row())
        for exact in (True, False):
            copied, spoken = [], []
            dialog = SimpleNamespace(
                _selected_entry=lambda: entry,
                _default_service=service,
                _internal_services=(SPOTIFY, TIDAL),
                _copy_to_clipboard=lambda link: copied.append(link) or True,
                _set_status=spoken.append,
                _resolve_action=(
                    (lambda _entry, _action, ready, _failed: ready(match))
                    if exact else
                    (lambda _entry, _action, _ready, failed: failed(LookupError()))
                ),
            )
            RadioRecognitionHistoryDialog._copy_track_link(dialog)
            assert len(copied) == 1
            assert copied[0].startswith("https://")
            assert spoken == [
                f"Skopiowano link {label}" if exact else
                f"Skopiowano link wyszukiwania {label}"
            ]
            assert ("/search" in copied[0]) is not exact


def test_apple_music_enter_in_right_arrow_copies_link_and_keeps_window_open() -> None:
    action = next(item for item in quick_actions(_entry(), TIDAL)
                  if item.action_id == "apple.copy.track")
    calls = []
    window = SimpleNamespace(
        _selected_action=lambda: action,
        _copy_link=lambda: calls.append("copy"),
        _finish=lambda executed: calls.append(("finish", executed)),
    )
    RecognitionActionsWindow._activate(window)
    assert calls == ["copy"]

    copied, spoken = [], []
    window = SimpleNamespace(
        _entry=_entry(),
        _actions=[action],
        _closing=False,
        actions=SimpleNamespace(GetSelection=lambda: 0, SetFocus=lambda: None),
        _copy_to_clipboard=lambda link: copied.append(link) or True,
        _set_status=spoken.append,
        _resolve_action=lambda _entry, _action, _ready, failed: failed(
            LookupError("no catalogue match")
        ),
    )
    window._selected_action = RecognitionActionsWindow._selected_action.__get__(
        window, type(window)
    )
    RecognitionActionsWindow._copy_link(window)
    assert copied[0].startswith("https://music.apple.com/search?term=")
    assert spoken == ["Skopiowano link wyszukiwania Apple Music"]


def test_apple_music_context_action_uses_same_copy_and_search_fallback() -> None:
    entry = _entry()
    action = next(item for item in all_actions(entry, TIDAL)
                  if item.action_id == "apple.copy.track")
    copied, spoken = [], []
    dialog = SimpleNamespace(
        _selected_entry=lambda: entry,
        _copy_to_clipboard=lambda link: copied.append(link) or True,
        _set_status=spoken.append,
        _resolve_action=lambda _entry, _action, _ready, failed: failed(
            LookupError("no catalogue match")
        ),
    )
    RadioRecognitionHistoryDialog._request_action(dialog, action)
    assert copied[0].startswith("https://music.apple.com/search?term=")
    assert spoken == ["Skopiowano link wyszukiwania Apple Music"]


def test_named_track_action_opens_track_in_selected_amc_service_not_album() -> None:
    for service, session in ((SPOTIFY, SessionId.SPOTIFY), (TIDAL, SessionId.TIDAL)):
        match = match_from_row(service, _spotify_row())
        action = next(item for item in quick_actions(_entry(), service)
                      if item.service == service and item.entity == "track")
        calls = []
        frame = SimpleNamespace(
            options=SimpleNamespace(session_order=(SessionId.FILES, session)),
            announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
            _open_spotify_search_result=lambda entry, *, relations: calls.append(
                (SPOTIFY, entry.row.title, relations)
            ),
            _open_tidal_search_result=lambda entry, *, relations: calls.append(
                (TIDAL, entry.row.title, relations)
            ),
            _restore_focus_after_dialog=lambda: calls.append(("focus",)),
        )
        LiteFrame._show_recognition_match_in_amc(frame, action, match)
        assert calls == [(service, "Kto Dał Nam Deszcz (1980)", False)]


def test_direct_album_handoff_does_not_start_competing_root_load() -> None:
    action = next(
        item for item in all_actions(_entry(), SPOTIFY)
        if item.action_id == "spotify.amc.album"
    )
    calls = []
    navigator = Navigator()
    navigator.sessions[SessionId.SPOTIFY].model.replace([
        Row("root", "Biblioteka Spotify", "folder")
    ])

    def open_container(intent, *, after_open=None, on_failure=None):
        calls.append(("container", intent))
        if intent.kind == "artist":
            navigator.apply_spotify_container(
                intent, "Albumy wykonawcy", [
                    Row("album", "Osobista Kolekcja 2", "album",
                        service_id="album-1", service_kind="album")
                ]
            )
            if after_open is not None:
                after_open()
        else:
            navigator.apply_spotify_container(
                intent, "Osobista Kolekcja 2", [_spotify_row()]
            )

    def switch_session(session, **options):
        calls.append(("session", session, options))
        navigator.active = session

    frame = SimpleNamespace(
        options=SimpleNamespace(session_order=(
            SessionId.FILES, SessionId.RADIO, SessionId.SPOTIFY,
        )),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        navigator=navigator,
        _switch_session=switch_session,
        _open_spotify_container=open_container,
        _sync_views=lambda: None,
        _restore_focus_after_dialog=lambda: calls.append(("focus",)),
    )

    LiteFrame._show_recognition_match_in_amc(
        frame, action, match_from_row(SPOTIFY, _spotify_row())
    )

    assert calls[0] == (
        "session", SessionId.SPOTIFY, {"initialize_if_empty": False}
    )
    assert [call[1].kind for call in calls[1:]] == ["artist", "album"]
    assert calls[2][1].service_id == "album-1"
    assert LiteFrame._open_service_album_row(
        SessionId.SPOTIFY, navigator.session
    ).service_id == "album-1"
    navigator.go_to_parent()
    assert navigator.session.spotify_heading == "Albumy wykonawcy"
    assert navigator.session.model.selected_row.service_id == "album-1"
    navigator.go_to_parent()
    assert navigator.session.model.selected_row.item_id == "root"


def test_recognized_tidal_album_returns_to_artist_albums() -> None:
    action = next(
        item for item in all_actions(_entry(), TIDAL)
        if item.action_id == "tidal.amc.album"
    )
    track = Row(
        "tidal:tracks:1", "Utwór", "track",
        service_id="tracks:1", service_kind="track",
        artist_name="Budka Suflera",
        related_album_service_id="albums:2",
        related_album_title="Album",
        related_artist_service_id="artists:3",
        related_artist_name="Budka Suflera",
    )
    navigator = Navigator()
    navigator.sessions[SessionId.TIDAL].model.replace([
        Row("root", "Biblioteka TIDAL", "folder")
    ])
    calls = []

    def open_container(intent, *, after_open=None, on_failure=None):
        calls.append(intent)
        if intent.kind == "artist":
            # Wynik albumu może leżeć poza pierwszą stroną katalogu.
            navigator.apply_tidal_container(
                intent, "Albumy wykonawcy", []
            )
            if after_open is not None:
                after_open()
        else:
            navigator.apply_tidal_container(intent, "Album", [track])

    def switch_session(session, **_options):
        navigator.active = session

    frame = SimpleNamespace(
        options=SimpleNamespace(session_order=(SessionId.TIDAL,)),
        navigator=navigator,
        announcer=SimpleNamespace(say=lambda _message: None),
        _switch_session=switch_session,
        _open_tidal_container=open_container,
        _sync_views=lambda: None,
        _restore_focus_after_dialog=lambda: None,
    )

    LiteFrame._show_recognition_match_in_amc(
        frame, action, match_from_row(TIDAL, track)
    )

    assert [intent.kind for intent in calls] == ["artist", "album"]
    assert calls[0].artist_section == "albums"
    assert LiteFrame._open_service_album_row(
        SessionId.TIDAL, navigator.session
    ).service_id == "albums:2"
    navigator.go_to_parent()
    assert navigator.session.tidal_heading == "Albumy wykonawcy"
    assert navigator.session.model.selected_row.service_id == "albums:2"
    navigator.go_to_parent()
    assert navigator.session.model.selected_row.item_id == "root"


def test_explicit_original_tidal_recognition_action_keeps_desktop_route() -> None:
    action = next(
        item for item in all_actions(_entry(), TIDAL)
        if item.action_id == "tidal.external.track"
    )
    row = Row(
        "tidal:tracks:1", "Utwór", "track",
        service_id="tracks:1", service_kind="track",
        related_album_service_id="albums:2",
    )
    calls = []
    frame = SimpleNamespace(
        navigator=Navigator(),
        _switch_session=lambda session, **options: calls.append(
            ("session", session, options)
        ),
        _play_tidal_track=lambda intent: calls.append(("desktop", intent)),
        _open_tidal_search_result=lambda *_args, **_kwargs: (
            calls.append(("unexpected AMC route",))
        ),
    )

    LiteFrame._execute_recognition_action(
        frame, _entry(), action, match_from_row(TIDAL, row)
    )

    assert calls[0] == (
        "session", SessionId.TIDAL, {"initialize_if_empty": False}
    )
    assert calls[1][0] == "desktop"
    assert calls[1][1].related_album_service_id == "albums:2"


def test_escape_returns_to_the_same_session_and_view_that_opened_history() -> None:
    events: list[str] = []

    class FocusTarget:
        def __init__(self, name: str) -> None:
            self.name = name

        def SetFocus(self) -> None:
            events.append(f"focus:{self.name}")

    class HistoryDialog:
        def __init__(self, *_args, **kwargs) -> None:
            self.changed = False
            self.requested_action = None
            self.finished = kwargs["finished"]

        def IsBeingDeleted(self) -> bool:
            return False

        def show_for_keyboard(self) -> None:
            events.append("show")

        def close(self) -> None:
            events.append("escape")
            self.finished()

    original = gui_module.RadioRecognitionHistoryDialog
    original_call_after = gui_module.wx.CallAfter
    gui_module.RadioRecognitionHistoryDialog = HistoryDialog
    gui_module.wx.CallAfter = lambda work, *args: work(*args)
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
                _radio_recognition_history_dialog=None,
                _detailed_hints=False,
                _to_clipboard=lambda _text: True,
                _resolve_recognition_match=lambda *_args: None,
                _change_recognition_membership=lambda *_args, **_kwargs: None,
                _save_state=lambda: None,
                _execute_recognition_action=lambda *_args: None,
            )
            frame._active_list = LiteFrame._active_list.__get__(frame, type(frame))
            frame._restore_focus_after_dialog = (
                LiteFrame._restore_focus_after_dialog.__get__(frame, type(frame))
            )

            LiteFrame._show_radio_recognition_history(frame)

            dialog = frame._radio_recognition_history_dialog
            assert dialog is not None
            LiteFrame._show_radio_recognition_history(frame)
            assert frame._radio_recognition_history_dialog is dialog
            dialog.close()

            assert frame.navigator.active is session
            assert frame.navigator.session.view is view
            assert frame.navigator.session.model.selected_row is selected_row
            assert events == ["show", "show", "escape", f"focus:{focus_name}"]
            assert frame._radio_recognition_history_dialog is None
    finally:
        gui_module.RadioRecognitionHistoryDialog = original
        gui_module.wx.CallAfter = original_call_after


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
