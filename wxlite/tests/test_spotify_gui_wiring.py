from __future__ import annotations

from types import SimpleNamespace

import amc_wx_lite.gui as gui_module
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import HostError
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenSpotifyCollectionView,
    OpenSpotifyContainer,
    OpenSpotifyView,
    PlaySpotifyItem,
    SessionId,
    ShowSpotifyRelations,
    SpotifyRelationChoice,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import LiteState


class ImmediateRunner:
    @staticmethod
    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:  # pragma: no cover - diagnostics path
            failed(error)


def test_spotify_account_setup_and_pairing_use_explicit_host_operations(monkeypatch) -> None:
    calls = []
    spoken = []

    class Field:
        def __init__(self):
            self.value = ""

        def SetValue(self, value):  # noqa: N802 - wx API
            self.value = value

        def SetFocus(self):  # noqa: N802 - wx API
            calls.append("focus-field")

    class Dialog:
        def __init__(self, _parent, service_name, _status, _start_login, **options):
            assert service_name == "Spotify"
            assert options["needs_setup"] is True
            assert options["redirect_uri"] == "http://127.0.0.1:43822/spotify/callback/"
            self.options = options
            self.client_id = Field()
            self.pairing_code = Field()
            self.pairing_address = Field()
            self.pairing_button = Field()
            self.check_pairing_button = Field()
            self.pairing_active = False
            self._login_started = False

        def Raise(self):  # noqa: N802 - wx API
            calls.append("raise")

        def focus_primary_action(self):
            calls.append("focus-primary")

        def IsBeingDeleted(self):  # noqa: N802 - wx API
            return False

        def set_status(self, message):
            calls.append(("status", message))

        def set_busy(self, busy):
            calls.append(("busy", busy))

        def finish(self, message, **_kwargs):
            self.set_status(message)

        def Show(self):  # noqa: N802 - wx API
            self.options["save_spotify_client_id"](self, "a" * 32)
            self.options["start_spotify_pairing"](self)
            assert self.pairing_code.value == "ABCD-EFGH"
            assert self.pairing_address.value == "https://www.spotify.com/pair"
            self.options["check_spotify_pairing"](self)
            self.options["cancel_spotify_pairing"](self)
            calls.append("show")

    client = SimpleNamespace(
        spotify_account_status=lambda: {
            "catalog": {"configured": False, "label": "Brak konfiguracji."},
            "playback": {"label": "Brak połączenia."},
            "redirectUri": "http://127.0.0.1:43822/spotify/callback/",
        },
        spotify_configure=lambda value: calls.append(("configure", value)) or {},
        spotify_pairing_start=lambda: calls.append("start") or {
            "userCode": "ABCD-EFGH",
            "verificationUri": "https://www.spotify.com/pair",
        },
        spotify_pairing_status=lambda: calls.append("check") or {
            "state": "pending",
            "message": "Czekam na zatwierdzenie.",
        },
        spotify_pairing_cancel=lambda: calls.append("cancel") or {},
    )
    frame = SimpleNamespace(
        client=client,
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _service_account_status_message=LiteFrame._service_account_status_message,
        _restore_focus_after_dialog=lambda: calls.append("restore-focus"),
        _window_alive=lambda: True,
    )
    monkeypatch.setattr(gui_module, "ServiceAccountDialog", Dialog)
    monkeypatch.setattr(gui_module.wx, "CallAfter", lambda callback, *args: callback(*args))

    LiteFrame._show_service_account(frame, SessionId.SPOTIFY)

    assert ("configure", "a" * 32) in calls
    assert calls.count("start") == 1
    assert calls.count("check") == 1
    assert calls.count("cancel") == 1
    assert calls[-1] == "raise"
    assert any("ABCD-EFGH" in message for message in spoken)
    dialog = frame._service_account_dialogs[SessionId.SPOTIFY]
    dialog.pairing_active = True
    dialog.options["on_closed"](dialog)
    assert calls[-1] == "restore-focus"
    assert calls.count("cancel") == 2
    assert SessionId.SPOTIFY not in frame._service_account_dialogs


def test_spotify_rate_command_reaches_the_embedded_engine() -> None:
    calls = []
    selections = []
    spoken = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SPOTIFY),
        _runtime_rate_by_session={},
        rate_choice=SimpleNamespace(SetSelection=selections.append),
        announcer=SimpleNamespace(say=spoken.append),
        client=SimpleNamespace(
            set_rate=lambda value, session_id: calls.append((value, session_id))
        ),
        runner=ImmediateRunner(),
    )

    LiteFrame._set_rate(frame, 1.25)

    assert calls == [(1.25, "spotify")]
    assert selections
    assert spoken == ["Prędkość 1,25 razy"]
    assert frame._runtime_rate_by_session[SessionId.SPOTIFY] == 1.25


def test_deferred_spotify_feedback_outlasts_native_focus_speech(monkeypatch) -> None:
    scheduled = []
    shown = []
    spoken = []
    monkeypatch.setattr(
        gui_module.wx,
        "CallLater",
        lambda delay, callback: scheduled.append((delay, callback)),
        raising=False,
    )
    frame = SimpleNamespace(
        _deferred_list_announcement_version=0,
        _window_alive=lambda: True,
        announcer=SimpleNamespace(show=shown.append, say=spoken.append),
    )

    LiteFrame._announce_after_list_update(
        frame,
        "Dodano do Biblioteki Spotify: Album",
    )

    assert shown == ["Dodano do Biblioteki Spotify: Album"]
    assert scheduled[0][0] >= 650
    assert spoken == []
    scheduled[0][1]()
    assert spoken == ["Dodano do Biblioteki Spotify: Album"]


def test_alt_d_is_the_spotify_podcast_description_command() -> None:
    assert resolve(
        Chord("D", alt=True),
        player_view=False,
        radio_session=False,
        spotify_session=True,
    ) is Action.SHOW_PODCAST_DESCRIPTION


def test_ctrl_bracket_cycles_real_spotify_outputs_not_a_fixed_denon_name() -> None:
    calls = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SPOTIFY),
        state=LiteState(audio_output_device_ids_by_session={
            "spotify": "Interfejs pierwszy",
        }),
        _profile_audio_outputs={},
        client=SimpleNamespace(
            audio_outputs=lambda selected, session: (
                calls.append(("list", selected, session))
                or {"devices": [
                    {"id": None, "name": "Domyślne urządzenie audio"},
                    {"id": "Interfejs pierwszy", "name": "Interfejs pierwszy"},
                    {"id": "Słuchawki USB", "name": "Słuchawki USB"},
                ]}
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        _apply_audio_output_choice=lambda *values: calls.append(("apply", *values)),
    )

    LiteFrame._cycle_audio_output(frame, 1)

    assert calls[0] == ("list", "Interfejs pierwszy", "spotify")
    assert calls[1][0] == "apply"
    assert calls[1][-1].label == "Słuchawki USB"


def test_spotify_album_shortcut_opens_the_same_collection_path_as_enter() -> None:
    received = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SPOTIFY),
        _open_spotify_collection_view=received.append,
        messages=SimpleNamespace(custom_seek_seconds=300),
    )

    LiteFrame._dispatch(frame, Action.SPOTIFY_VIEW_ALBUMS)

    assert received == [OpenSpotifyCollectionView(
        "spotify-collection:albums", "albums", "Albumy"
    )]


def test_spotify_library_shortcut_calls_confirmed_host_write() -> None:
    calls = []
    spoken = []
    album = Row(
        "spotify:album:private",
        "Album",
        "album",
        service_id="albumprivate",
        service_kind="album",
        artist_name="Wykonawca",
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            sessions={
                SessionId.SPOTIFY: SimpleNamespace(spotify_context=None)
            },
        ),
        client=SimpleNamespace(
            spotify_collection_membership=lambda items, *, mode, add=None: (
                calls.append((items, mode, add))
                or {
                    "added": True,
                    "changed": 1,
                    "message": "Dodano do Biblioteki Spotify: Album",
                }
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [album],
        _spotify_membership_pending=False,
    )

    LiteFrame._toggle_spotify_membership(frame, favorite=False)

    assert calls == [([{
        "itemId": "spotify:album:private",
        "externalId": "albumprivate",
        "title": "Album",
        "kind": "album",
        "artist": "Wykonawca",
        "publicUri": "",
    }], "library", None)]
    assert spoken == ["Dodano do Biblioteki Spotify: Album"]
    assert frame._spotify_membership_pending is False


def test_spotify_library_shortcut_removes_a_saved_episode_and_refreshes_view() -> None:
    calls = []
    spoken = []
    refreshed = []
    episode = Row(
        "spotify:episode:private",
        "Odcinek",
        "episode",
        service_id="episodeprivate",
        service_kind="episode",
        artist_name="Podcast użytkowy",
    )
    context = OpenSpotifyCollectionView(
        "spotify-collection:episodes", "episodes", "Zapisane odcinki"
    )
    frame = SimpleNamespace(
        navigator=Navigator(),
        client=SimpleNamespace(
            spotify_collection_membership=lambda items, *, mode, add=None: (
                calls.append((items, mode, add))
                or {
                    "added": False,
                    "changed": 1,
                    "message": "Usunięto z Biblioteki Spotify: Odcinek",
                }
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [episode],
        _spotify_membership_pending=False,
        _spotify_loaded_counts={"episodes": 150},
        _sync_views=lambda: refreshed.append(True),
        _announce_after_list_update=spoken.append,
    )
    frame.navigator.active = SessionId.SPOTIFY
    frame.navigator.apply_spotify_collection_view(
        context, "Zapisane odcinki Spotify", [episode]
    )

    LiteFrame._toggle_spotify_membership(frame, favorite=False)

    assert calls[0][1:] == ("library", False)
    assert calls[0][0][0]["kind"] == "episode"
    assert spoken == ["Usunięto z Biblioteki Spotify: Odcinek"]
    assert frame._spotify_loaded_counts == {"episodes": 0}
    assert frame.navigator.session.model.rows == []
    assert refreshed == [True]


def test_spotify_library_shortcut_removes_a_saved_podcast_and_announces_it() -> None:
    calls = []
    spoken = []
    podcast = Row(
        "spotify:show:private",
        "Skądinąd",
        "podcast",
        service_id="showprivate",
        service_kind="podcast",
        artist_name="Tomasz Stawiszyński",
    )
    context = OpenSpotifyCollectionView(
        "spotify-collection:podcasts", "podcasts", "Podcasty"
    )
    frame = SimpleNamespace(
        navigator=Navigator(),
        client=SimpleNamespace(
            spotify_collection_membership=lambda items, *, mode, add=None: (
                calls.append((items, mode, add))
                or {
                    "added": False,
                    "changed": 1,
                    "message": "Usunięto z Biblioteki Spotify: Skądinąd",
                }
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [podcast],
        _spotify_membership_pending=False,
        _spotify_loaded_counts={"podcasts": 150},
        _sync_views=lambda: None,
        _announce_after_list_update=spoken.append,
    )
    frame.navigator.active = SessionId.SPOTIFY
    frame.navigator.apply_spotify_collection_view(
        context, "Podcasty Spotify", [podcast]
    )

    LiteFrame._toggle_spotify_membership(frame, favorite=False)

    assert calls[0][1:] == ("library", False)
    assert calls[0][0][0]["kind"] == "podcast"
    assert calls[0][0][0]["externalId"] == "showprivate"
    assert spoken == ["Usunięto z Biblioteki Spotify: Skądinąd"]
    assert frame._spotify_loaded_counts == {"podcasts": 0}
    assert frame.navigator.session.model.rows == []


def test_spotify_library_shortcut_keeps_album_page_sort_and_neighbor() -> None:
    calls = []
    spoken = []
    opened = []
    navigator = Navigator()
    navigator.active = SessionId.SPOTIFY
    context = OpenSpotifyCollectionView(
        "spotify-collection:albums", "albums", "Albumy"
    )
    rows = [
        Row(
            f"row-{index}", f"Album {index}", "album",
            service_id=f"album{index}", service_kind="album",
        )
        for index in range(1, 4)
    ]
    navigator.apply_spotify_collection_view(context, "Albumy Spotify", rows)
    navigator.session.model.select_id("row-2")
    frame = SimpleNamespace(
        navigator=navigator,
        client=SimpleNamespace(
            spotify_collection_membership=lambda items, *, mode, add=None: (
                calls.append((items, mode, add))
                or {
                    "added": False,
                    "changed": 1,
                    "message": "Usunięto z Biblioteki Spotify: Album 2",
                }
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [rows[1]],
        _spotify_membership_pending=False,
        _spotify_loaded_counts={"albums": 300},
        _spotify_effective_sort_modes={"albums": "Alphabetical"},
        _open_spotify_collection_view=opened.append,
        _sync_views=lambda: None,
        _announce_after_list_update=spoken.append,
    )

    LiteFrame._toggle_spotify_membership(frame, favorite=False)

    assert calls[0][1:] == ("library", False)
    assert spoken == ["Usunięto z Biblioteki Spotify: Album 2"]
    assert frame._spotify_loaded_counts == {"albums": 2}
    assert frame._spotify_effective_sort_modes == {"albums": "Alphabetical"}
    assert [row.item_id for row in navigator.session.model.rows] == [
        "row-1", "row-3"
    ]
    assert navigator.session.model.selected_id == "row-3"
    assert opened == []


def test_spotify_sort_reloads_first_page_without_adding_a_history_level() -> None:
    opened = []
    spoken = []
    context = OpenSpotifyCollectionView(
        "spotify-collection:liked", "liked", "Polubione utwory"
    )
    session = SimpleNamespace(
        view=gui_module.View.LIST,
        spotify_context=context,
        model=SimpleNamespace(selected_id="private-track-row"),
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            sessions={SessionId.SPOTIFY: session},
        ),
        state=LiteState(),
        _spotify_effective_sort_modes={"liked": "AddedNewest"},
        _spotify_loaded_counts={"liked": 300},
        _save_state=lambda: True,
        _refresh_menu_state=lambda: None,
        _open_spotify_collection_view=opened.append,
        announcer=SimpleNamespace(say=spoken.append),
    )

    LiteFrame._set_spotify_collection_sort(
        frame,
        Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL,
    )

    assert frame.state.spotify_sort_modes == {"liked": "Alphabetical"}
    assert frame._spotify_loaded_counts == {}
    assert spoken == []
    assert opened == [OpenSpotifyCollectionView(
        "spotify-collection:liked",
        "liked",
        "Polubione utwory",
        preferred_id="private-track-row",
        replace_current=True,
        announcement="Alfabetycznie",
    )]


def test_spotify_root_reads_status_and_applies_only_user_labels() -> None:
    calls = []
    messages = []
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_collection_root=lambda: {
                "heading": "Biblioteka Spotify",
                "views": [
                    {"view": "liked", "label": "Polubione utwory"},
                    {"view": "artists", "label": "Wykonawcy"},
                ],
            },
            spotify_account_status=lambda: {
                "catalog": {
                    "connected": False,
                    "label": "Katalog Spotify nie jest skonfigurowany.",
                }
            },
        ),
        _spotify_navigation_version=0,
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_view=lambda heading, rows, preferred_id=None: (
                calls.append((heading, rows, preferred_id))
                or [Announce(heading)]
            ),
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=messages.append),
        _run=lambda events: calls.append(events),
    )

    LiteFrame._open_spotify_view(frame, OpenSpotifyView("spotify-collection:artists"))

    heading, rows, preferred_id = calls[0]
    assert heading == "Biblioteka Spotify"
    assert [row.title for row in rows] == ["Polubione utwory", "Wykonawcy"]
    assert preferred_id == "spotify-collection:artists"
    assert messages == ["Katalog Spotify nie jest skonfigurowany."]


def test_spotify_collection_view_requests_first_page_and_adds_load_more_row() -> None:
    calls = []
    intent = OpenSpotifyCollectionView(
        "spotify-collection:artists", "artists", "Wykonawcy"
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_collection_view=lambda view, *, loaded_count, sort_mode: (
                calls.append(("host", view, loaded_count, sort_mode))
                or {
                    "heading": "Wykonawcy Spotify",
                    "items": [],
                    "warning": "",
                    "hasMore": True,
                }
            )
        ),
        _spotify_navigation_version=0,
        _spotify_loaded_counts={},
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_collection_view=lambda received, heading, rows, preferred_id=None: (
                calls.append(("apply", received, heading, rows, preferred_id)) or []
            ),
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        _run=lambda events: calls.append(("run", events)),
    )

    LiteFrame._open_spotify_collection_view(frame, intent)

    assert calls[0] == ("host", "artists", 150, "AddedNewest")
    assert calls[1][0:3] == ("apply", intent, "Wykonawcy Spotify")
    assert [row.title for row in calls[1][3]] == ["Załaduj więcej"]
    assert calls[1][4] is None


def test_spotify_sort_confirmation_is_spoken_after_the_new_rows_are_applied() -> None:
    calls = []
    intent = OpenSpotifyCollectionView(
        "spotify-collection:albums",
        "albums",
        "Albumy",
        announcement="Alfabetycznie",
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_collection_view=lambda *_args, **_kwargs: {
                "heading": "Albumy Spotify",
                "items": [{
                    "id": "private-album",
                    "externalId": "albumprivate",
                    "title": "Album",
                    "kind": "album",
                }],
            }
        ),
        _spotify_navigation_version=0,
        _spotify_loaded_counts={},
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_collection_view=lambda *_args, **_kwargs: (
                calls.append("apply") or [Announce("Albumy Spotify, 1 pozycja")]
            ),
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        _sync_views=lambda: calls.append("sync"),
        _announce_after_list_update=lambda message: calls.append(("deferred", message)),
        _run=lambda _events: calls.append("run"),
    )

    LiteFrame._open_spotify_collection_view(frame, intent)

    assert calls == ["apply", "sync", ("deferred", "Alfabetycznie")]


def test_left_arrow_reads_total_spotify_album_duration_without_moving_focus() -> None:
    album = Row(
        "private-album-row",
        "Album",
        "album",
        service_id="privatealbum",
        service_kind="album",
        artist_name="Wykonawca",
    )
    model = SimpleNamespace(selected_row=album)
    spoken = []
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_container_items=lambda **_kwargs: {
                "heading": "Album, Album",
                "items": [
                    {
                        "id": "private-track-1",
                        "externalId": "trackone",
                        "title": "Pierwszy",
                        "kind": "track",
                        "durationTicks": 120 * 10_000_000,
                    },
                    {
                        "id": "private-track-2",
                        "externalId": "tracktwo",
                        "title": "Drugi",
                        "kind": "track",
                        "durationTicks": 180 * 10_000_000,
                    },
                ],
            }
        ),
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            sessions={SessionId.SPOTIFY: SimpleNamespace(model=model)},
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
    )

    LiteFrame._announce_spotify_information(frame, album)

    assert len(spoken) == 1
    assert spoken[0] == "5:00"
    assert model.selected_row is album


def test_alt_d_opens_spotify_episode_description_in_the_native_text_dialog() -> None:
    episode = Row(
        "private-episode-row",
        "Odcinek",
        "episode",
        service_id="privateepisode",
        service_kind="episode",
    )
    state = SimpleNamespace(
        view=gui_module.View.LIST,
        model=SimpleNamespace(selected_row=episode),
        playback_source_rows=(),
        now_playing_id=None,
    )
    shown = []
    copied = []

    class Dialog:
        def __init__(self, _parent, information, copy_text):
            shown.append(information)
            self.copy_text = copy_text

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):  # noqa: N802 - wx API
            self.copy_text(shown[0].text)
            return gui_module.wx.ID_CANCEL

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            sessions={SessionId.SPOTIFY: state},
        ),
        client=SimpleNamespace(
            spotify_description=lambda **values: (
                {"description": "Opis odcinka Spotify."}
                if values == {"external_id": "privateepisode", "kind": "episode"}
                else {}
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda message: copied.append(message)),
        _to_clipboard=lambda text: copied.append(text) is None,
    )
    original = gui_module.PodcastDescriptionDialog
    gui_module.PodcastDescriptionDialog = Dialog
    try:
        LiteFrame._show_podcast_description(frame)
    finally:
        gui_module.PodcastDescriptionDialog = original

    assert shown[0].window_title == "Opis odcinka Spotify"
    assert shown[0].text == "Opis odcinka Spotify."
    assert copied == ["Opis odcinka Spotify.", "Skopiowano całą treść"]


def test_spotify_load_more_requests_next_page_and_focuses_first_new_item() -> None:
    calls = []
    intent = OpenSpotifyCollectionView(
        "spotify-collection:liked",
        "liked",
        "Polubione utwory",
        load_more=True,
    )
    items = [
        {
            "id": f"private-row-{index}",
            "externalId": f"track{index}",
            "title": f"Utwór {index}",
            "kind": "track",
        }
        for index in range(151)
    ]
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_collection_view=lambda view, *, loaded_count, sort_mode: (
                calls.append(("host", view, loaded_count, sort_mode))
                or {
                    "heading": "Polubione utwory Spotify",
                    "items": items,
                    "warning": "",
                    "hasMore": False,
                }
            )
        ),
        _spotify_navigation_version=0,
        _spotify_loaded_counts={"liked": 150},
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_collection_view=(
                lambda received, heading, rows, preferred_id=None: (
                    calls.append(("apply", received, heading, rows, preferred_id))
                    or []
                )
            ),
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        _run=lambda events: calls.append(("run", events)),
    )

    LiteFrame._open_spotify_collection_view(frame, intent)

    assert calls[0] == ("host", "liked", 300, "AddedNewest")
    assert calls[1][0:3] == (
        "apply",
        intent,
        "Polubione utwory Spotify",
    )
    assert len(calls[1][3]) == 151
    assert calls[1][4] == "private-row-150"
    assert frame._spotify_loaded_counts == {"liked": 151}


def test_spotify_artist_section_uses_the_read_only_container_contract() -> None:
    received = []
    intent = OpenSpotifyContainer(
        "private-row", "privateartist", "Wykonawca", "artist", section="tracks"
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_container_items=lambda **values: (
                received.append(values)
                or {"heading": "Utwory wykonawcy, Wykonawca", "items": []}
            )
        ),
        _spotify_navigation_version=0,
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_container=lambda *_args: [],
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda _message: None),
        _run=lambda _events: None,
    )

    LiteFrame._open_spotify_container(frame, intent)

    assert received == [{
        "external_id": "privateartist",
        "title": "Wykonawca",
        "kind": "artist",
        "artist": "",
        "artist_section": "tracks",
        "loaded_count": 150,
    }]


def test_spotify_podcast_load_more_requests_300_and_focuses_first_new_episode() -> None:
    calls = []
    intent = OpenSpotifyContainer(
        "private-podcast-row",
        "privatepodcast",
        "Podcast",
        "podcast",
        load_more=True,
    )
    items = [
        {
            "id": f"private-episode-row-{index}",
            "externalId": f"episode{index}",
            "title": f"Odcinek {index}",
            "kind": "episode",
        }
        for index in range(151)
    ]
    scope = "container:podcast:privatepodcast:"
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_container_items=lambda **values: (
                calls.append(("host", values))
                or {
                    "heading": "Podcast, Podcast",
                    "items": items,
                    "warning": "",
                    "hasMore": False,
                }
            )
        ),
        _spotify_navigation_version=0,
        _spotify_loaded_counts={scope: 150},
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_container=(
                lambda received, heading, rows, preferred_id=None: (
                    calls.append(("apply", received, heading, rows, preferred_id))
                    or []
                )
            ),
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=lambda message: calls.append(("say", message))),
        _run=lambda events: calls.append(("run", events)),
    )

    LiteFrame._open_spotify_container(frame, intent)

    assert calls[0][0] == "host"
    assert calls[0][1]["loaded_count"] == 300
    assert calls[1][0:3] == ("apply", intent, "Podcast, Podcast")
    assert len(calls[1][3]) == 151
    assert calls[1][4] == "private-episode-row-150"
    assert frame._spotify_loaded_counts == {scope: 151}


def test_unavailable_spotify_playlist_stays_on_the_current_list() -> None:
    applied = []
    spoken = []
    intent = OpenSpotifyContainer(
        "private-playlist-row", "privateplaylist", "Moja playlista", "playlist"
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_container_items=lambda **_values: {
                "heading": "Playlista, Moja playlista",
                "items": [],
                "warning": "Spotify nie udostępnił zawartości tego elementu.",
                "available": False,
            }
        ),
        _spotify_navigation_version=0,
        navigator=SimpleNamespace(
            active=SessionId.SPOTIFY,
            apply_spotify_container=lambda *_args: applied.append(True) or [],
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _run=lambda events: applied.append(events),
    )

    LiteFrame._open_spotify_container(frame, intent)

    assert applied == []
    assert spoken == ["Spotify nie udostępnił zawartości tego elementu."]


def test_spotify_right_arrow_uses_two_real_destinations_without_context_menu() -> None:
    created = []
    windows = []
    activated = []
    run = []
    focused = []
    choices = (
        SpotifyRelationChoice(
            "Przejdź do albumu",
            OpenSpotifyContainer("album", "album-id", "Album", "album"),
        ),
        SpotifyRelationChoice(
            "Przejdź do wykonawcy",
            OpenSpotifyContainer("artist", "artist-id", "Wykonawca", "artist"),
        ),
    )
    intent = ShowSpotifyRelations("Przejdź do", choices)

    class FakeWindow:
        def __init__(self, _parent, *, title, labels, detailed_hints, finished):
            self.finished = finished
            created.append((title, labels, detailed_hints))
            windows.append(self)

        @staticmethod
        def show_for_keyboard():
            return None

        @staticmethod
        def Destroy():
            return None

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            activate_spotify_relation=(
                lambda choice: activated.append(choice) or [choice.label]
            )
        ),
        _active_list=lambda: SimpleNamespace(SetFocus=lambda: focused.append(True)),
        _run=run.append,
        _detailed_hints=False,
        _relation_destination_window=None,
    )
    frame._show_relation_destination_window = lambda **kwargs: (
        LiteFrame._show_relation_destination_window(frame, **kwargs)
    )
    original_window = gui_module.RelationDestinationWindow
    gui_module.RelationDestinationWindow = FakeWindow
    try:
        LiteFrame._show_spotify_relations(frame, intent)
        assert activated == []
        assert run == []
        windows[0].finished(0)
        LiteFrame._show_spotify_relations(frame, intent)
        assert activated == [choices[0]]
        windows[1].finished(1)
    finally:
        gui_module.RelationDestinationWindow = original_window

    assert created == [
        ("Przejdź do", ("Album", "Wykonawca"), False),
        ("Przejdź do", ("Album", "Wykonawca"), False),
    ]
    assert activated == [choices[0], choices[1]]
    assert run == [["Przejdź do albumu"], ["Przejdź do wykonawcy"]]
    assert focused == []


def test_spotify_right_arrow_waits_for_enter_on_one_podcast_destination() -> None:
    created = []
    windows = []
    activated = []
    run = []
    focused = []
    choice = SpotifyRelationChoice(
        "Przejdź do Podcast użytkowy",
        OpenSpotifyContainer(
            "episode:related-podcast", "podcast-id", "Podcast użytkowy", "podcast"
        ),
    )
    intent = ShowSpotifyRelations("Przejdź do", (choice,))

    class FakeWindow:
        def __init__(self, _parent, *, title, labels, detailed_hints, finished):
            self.finished = finished
            created.append((title, labels, detailed_hints))
            windows.append(self)

        @staticmethod
        def show_for_keyboard():
            return None

        @staticmethod
        def Destroy():
            return None

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            activate_spotify_relation=(
                lambda selected: activated.append(selected) or [selected.label]
            )
        ),
        _active_list=lambda: SimpleNamespace(SetFocus=lambda: focused.append(True)),
        _run=run.append,
        _detailed_hints=False,
        _relation_destination_window=None,
    )
    frame._show_relation_destination_window = lambda **kwargs: (
        LiteFrame._show_relation_destination_window(frame, **kwargs)
    )
    original_window = gui_module.RelationDestinationWindow
    gui_module.RelationDestinationWindow = FakeWindow
    try:
        LiteFrame._show_spotify_relations(frame, intent)
        assert activated == []
        assert run == []
        windows[0].finished(0)
    finally:
        gui_module.RelationDestinationWindow = original_window

    assert created == [("Przejdź do", ("Podcast użytkowy",), False)]
    assert activated == [choice]
    assert run == [["Przejdź do Podcast użytkowy"]]
    assert focused == []


def test_busy_spotify_collection_enter_is_retried_without_speaking_error() -> None:
    submissions = []
    scheduled = []
    messages = []
    intent = OpenSpotifyCollectionView(
        "spotify-collection:artists", "artists", "Wykonawcy"
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_collection_view=lambda _view, *, loaded_count, sort_mode: {}
        ),
        _spotify_navigation_version=0,
        _spotify_loaded_counts={},
        navigator=SimpleNamespace(active=SessionId.SPOTIFY),
        runner=SimpleNamespace(submit=lambda *args: submissions.append(args)),
        announcer=SimpleNamespace(say=messages.append),
    )
    frame._open_spotify_collection_view = lambda received, *, attempt=0: (
        LiteFrame._open_spotify_collection_view(
            frame,
            received,
            attempt=attempt,
        )
    )
    original_call_later = getattr(gui_module.wx, "CallLater", None)
    gui_module.wx.CallLater = (
        lambda _milliseconds, callback: scheduled.append(callback)
    )
    try:
        LiteFrame._open_spotify_collection_view(frame, intent)
        submissions[0][3](HostError(
            "Trwa odczyt informacji. Powtórz skrót za chwilę.",
            code="operation_busy",
        ))

        assert messages == []
        assert len(scheduled) == 1
        scheduled[0]()
        assert len(submissions) == 2
        assert frame._spotify_navigation_version == 2
    finally:
        if original_call_later is None:
            del gui_module.wx.CallLater
        else:
            gui_module.wx.CallLater = original_call_later


def test_new_spotify_enter_cancels_an_older_busy_retry() -> None:
    submissions = []
    scheduled = []
    first = OpenSpotifyContainer(
        "album-one", "one", "Pierwszy album", "album"
    )
    second = OpenSpotifyContainer(
        "album-two", "two", "Drugi album", "album"
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(spotify_container_items=lambda **_values: {}),
        _spotify_navigation_version=0,
        navigator=SimpleNamespace(active=SessionId.SPOTIFY),
        runner=SimpleNamespace(submit=lambda *args: submissions.append(args)),
        announcer=SimpleNamespace(say=lambda _message: None),
    )
    frame._open_spotify_container = lambda received, *, attempt=0: (
        LiteFrame._open_spotify_container(frame, received, attempt=attempt)
    )
    original_call_later = getattr(gui_module.wx, "CallLater", None)
    gui_module.wx.CallLater = (
        lambda _milliseconds, callback: scheduled.append(callback)
    )
    try:
        LiteFrame._open_spotify_container(frame, first)
        submissions[0][3](HostError("Trwa odczyt", code="operation_busy"))
        LiteFrame._open_spotify_container(frame, second)

        assert len(submissions) == 2
        assert frame._spotify_navigation_version == 2
        scheduled[0]()
        assert len(submissions) == 2
    finally:
        if original_call_later is None:
            del gui_module.wx.CallLater
        else:
            gui_module.wx.CallLater = original_call_later


def test_spotify_playback_is_handed_to_librespot_without_committing_early() -> None:
    host_calls = []
    restored = []
    messages = []
    refreshed = []
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_play=lambda **values: host_calls.append(values) or {"ok": True}
        ),
        navigator=SimpleNamespace(
            restore_spotify_playback_after_failed_handoff=(
                lambda item_id, title: restored.append((item_id, title))
            )
        ),
        announcer=SimpleNamespace(say=messages.append),
        runner=ImmediateRunner(),
        options=SimpleNamespace(volume=37),
        _session_default_rate=lambda _session: 1.5,
        _pending_spotify_playback=None,
        _pending_playback_session=None,
        _refresh_status=lambda: refreshed.append(True),
    )
    intent = PlaySpotifyItem(
        "new", "Nowy utwór", "private", "track",
        previous_item_id="old", previous_title="Poprzedni utwór",
    )

    LiteFrame._play_spotify(frame, intent)

    assert host_calls == [{
        "item_id": "new",
        "title": "Nowy utwór",
        "external_id": "private",
        "kind": "track",
        "volume": 37,
        "playback_rate": 1.5,
    }]
    assert frame._pending_spotify_playback is intent
    assert frame._pending_playback_session is SessionId.SPOTIFY
    assert restored == []
    assert messages == ["Przygotowywanie odtwarzania Spotify"]
    assert refreshed == [True]


def test_spotify_started_event_commits_the_pending_item() -> None:
    committed = []
    recorded = []
    intent = PlaySpotifyItem("new", "Nowy utwór", "private", "track")
    confirmed = Row(
        "new", "Nowy utwór", "track",
        service_id="private", service_kind="track",
    )
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        _pending_spotify_playback=intent,
        _pending_playback_session=SessionId.SPOTIFY,
        _playing_session=None,
        _playing_sessions=set(),
        navigator=SimpleNamespace(
            note_spotify_playback_started=(
                lambda item_id, title: (
                    committed.append((item_id, title)) or confirmed
                )
            )
        ),
        _record_spotify_history=recorded.append,
        announcer=SimpleNamespace(say=lambda _message: None),
    )

    LiteFrame._handle_engine_event(frame, "playback.started", {
        "engine": "spotify",
        "id": "technical-host-id",
        "title": "Techniczny tytuł",
    })

    assert committed == [("new", "Nowy utwór")]
    assert recorded == [confirmed]
    assert frame._pending_spotify_playback is None
    assert frame._pending_playback_session is None
    assert frame._playing_session is SessionId.SPOTIFY
    assert frame._playing_sessions == {SessionId.SPOTIFY}


def test_spotify_started_event_announces_default_output_fallback_once() -> None:
    messages = []
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        _pending_spotify_playback=None,
        _pending_playback_session=SessionId.SPOTIFY,
        _playing_session=None,
        _playing_sessions=set(),
        navigator=SimpleNamespace(
            note_spotify_playback_started=lambda _item_id, _title: None
        ),
        announcer=SimpleNamespace(say=messages.append),
    )

    LiteFrame._handle_engine_event(frame, "playback.started", {
        "engine": "spotify",
        "id": "track-1",
        "title": "Utwór",
        "audioOutputFallbackDeviceName": (
            "Cyfrowy interfejs audio (PMA-1700NE)"
        ),
    })

    assert messages == [
        "Zapamiętane urządzenie audio Cyfrowy interfejs audio "
        "(PMA-1700NE) jest niedostępne. Spotify odtwarza na "
        "domyślnym urządzeniu systemowym"
    ]


def test_ctrl_h_in_spotify_opens_its_own_history_not_local_files() -> None:
    navigator = Navigator()
    navigator.active = SessionId.SPOTIFY
    navigator.apply_spotify_view("Biblioteka Spotify", [Row(
        "album", "Album", "album",
        service_id="album-private", service_kind="album",
    )])
    events = []
    frame = SimpleNamespace(
        navigator=navigator,
        state=LiteState(spotify_playback_history=[{
            "itemId": "track-row",
            "externalId": "track-private",
            "title": "Utwór z historii",
            "kind": "track",
            "artist": "Wykonawca",
        }]),
        _begin_transient_preview=lambda: None,
        _announce_transient_preview=events.append,
    )

    LiteFrame._show_playback_history_preview(frame)

    assert navigator.active is SessionId.SPOTIFY
    assert navigator.session.library_view is LibraryView.SPOTIFY_HISTORY
    assert navigator.session.model.selected_row.title == "Utwór z historii"
    assert events == [[Announce("Spotify — Historia odtwarzania, 1 pozycja")]]


def test_confirmed_spotify_history_is_persisted_and_deduplicated() -> None:
    saves = []
    state = LiteState()
    frame = SimpleNamespace(
        state=state,
        _save_state=lambda: saves.append(True) or True,
    )
    first = Row(
        "row-1", "Pierwszy", "track",
        service_id="external-1", service_kind="track",
    )
    second = Row(
        "row-2", "Drugi", "track",
        service_id="external-2", service_kind="track",
    )

    LiteFrame._record_spotify_history(frame, first)
    LiteFrame._record_spotify_history(frame, second)
    LiteFrame._record_spotify_history(frame, first)

    assert [
        item["title"] for item in state.spotify_playback_history
    ] == ["Pierwszy", "Drugi"]
    assert saves == [True, True, True]


def test_delete_in_spotify_history_removes_only_history_and_keeps_focus() -> None:
    messages = []
    saves = []
    synced = []
    state = LiteState(spotify_playback_history=[
        {
            "itemId": "row-1",
            "externalId": "external-1",
            "title": "Pierwszy",
            "kind": "track",
        },
        {
            "itemId": "row-2",
            "externalId": "external-2",
            "title": "Drugi",
            "kind": "track",
        },
    ])
    navigator = Navigator()
    navigator.active = SessionId.SPOTIFY
    navigator.apply_spotify_history(list(
        gui_module.spotify_history_rows(state.spotify_playback_history)
    ))
    selected = navigator.session.model.rows[0]
    frame = SimpleNamespace(
        navigator=navigator,
        state=state,
        announcer=SimpleNamespace(say=messages.append),
        _selected_action_rows=lambda: [selected],
        _save_state=lambda: saves.append(True) or True,
        _sync_views=lambda: synced.append(True),
    )
    frame._remove_spotify_history_entries = (
        lambda: LiteFrame._remove_spotify_history_entries(frame)
    )

    assert LiteFrame._action_available_in_active_session(
        frame, Action.REMOVE_SELECTED
    )
    LiteFrame._remove_selected_items(frame)

    assert [
        item["title"] for item in state.spotify_playback_history
    ] == ["Drugi"]
    assert navigator.session.model.selected_row.title == "Drugi"
    assert saves == [True]
    assert synced == [True]
    assert messages == [
        "Usunięto z Historii Spotify: Pierwszy. "
        "Biblioteka Spotify pozostała bez zmian"
    ]


def test_delete_remains_unavailable_in_spotify_library() -> None:
    navigator = Navigator()
    navigator.active = SessionId.SPOTIFY
    navigator.apply_spotify_view("Biblioteka Spotify", [Row(
        "track", "Utwór", "track",
        service_id="external", service_kind="track",
    )])
    frame = SimpleNamespace(navigator=navigator)

    assert not LiteFrame._action_available_in_active_session(
        frame, Action.REMOVE_SELECTED
    )


def test_delete_in_saved_spotify_view_is_explicit_remove_and_keeps_neighbor() -> None:
    calls = []
    opened = []
    messages = []
    navigator = Navigator()
    navigator.active = SessionId.SPOTIFY
    context = OpenSpotifyCollectionView(
        "spotify-collection:albums", "albums", "Albumy"
    )
    rows = [
        Row(
            "row-1", "Pierwszy", "album",
            service_id="AlbumOne", service_kind="album",
        ),
        Row(
            "row-2", "Drugi", "album",
            service_id="AlbumTwo", service_kind="album",
        ),
    ]
    navigator.apply_spotify_collection_view(context, "Albumy Spotify", rows)
    frame = SimpleNamespace(
        navigator=navigator,
        client=SimpleNamespace(
            spotify_collection_membership=(
                lambda items, *, mode, add: calls.append((items, mode, add))
                or {
                    "added": False,
                    "changed": 1,
                    "message": "Usunięto z Biblioteki Spotify: Pierwszy",
                }
            )
        ),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=messages.append),
        _selected_action_rows=lambda: [rows[0]],
        _spotify_membership_pending=False,
        _spotify_loaded_counts={"albums": 2},
        _open_spotify_collection_view=opened.append,
    )

    assert LiteFrame._action_available_in_active_session(
        frame, Action.REMOVE_SELECTED
    )
    LiteFrame._remove_spotify_saved_items(frame)

    assert len(calls) == 1
    assert calls[0][1:] == ("library", False)
    assert opened == [OpenSpotifyCollectionView(
        "spotify-collection:albums",
        "albums",
        "Albumy",
        preferred_id="row-2",
        replace_current=True,
        announcement="Usunięto z Biblioteki Spotify: Pierwszy",
    )]
    assert frame._spotify_membership_pending is False
    assert frame._spotify_loaded_counts == {}
    assert messages == []


def test_spotify_album_preset_plays_first_available_track_without_moving_focus() -> None:
    received = []
    messages = []
    navigator = Navigator()
    navigator.active = SessionId.SPOTIFY
    visible = [Row(
        "visible-album", "Widoczny album", "album",
        service_id="VisibleAlbum", service_kind="album",
    )]
    navigator.apply_spotify_view("Biblioteka Spotify", visible)
    frame = SimpleNamespace(
        client=SimpleNamespace(
            spotify_container_items=lambda **_values: {
                "heading": "Album z presetu",
                "items": [{
                    "id": "preset-track",
                    "externalId": "TrackOne",
                    "title": "Pierwszy utwór",
                    "kind": "track",
                    "artist": "Wykonawca",
                    "isAvailable": True,
                }],
            }
        ),
        navigator=navigator,
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=messages.append),
        _spotify_preset_playback_version=4,
        _run=received.extend,
    )
    album = Row(
        "preset-album", "Album z presetu", "album",
        service_id="PresetAlbum", service_kind="album",
    )

    LiteFrame._play_spotify_album_preset(
        frame,
        album,
        spoken_slot="cztery",
        request_version=4,
    )

    assert len(received) == 1
    assert isinstance(received[0], PlaySpotifyItem)
    assert received[0].item_id == "preset-track"
    assert received[0].preserve_navigation
    assert received[0].success_announcement == "Album z presetu"
    assert navigator.session.model.selected_row == visible[0]
    assert messages == []


def test_spotify_failed_event_restores_prior_item_and_list_context() -> None:
    restored = []
    messages = []
    intent = PlaySpotifyItem(
        "new", "Nowy utwór", "private", "track",
        previous_item_id="old", previous_title="Poprzedni utwór",
    )
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        _pending_spotify_playback=intent,
        _pending_playback_session=SessionId.SPOTIFY,
        _playing_session=SessionId.SPOTIFY,
        _playing_sessions={SessionId.SPOTIFY},
        navigator=SimpleNamespace(
            restore_spotify_playback_after_failed_handoff=(
                lambda item_id, title: restored.append((item_id, title))
            )
        ),
        announcer=SimpleNamespace(say=messages.append),
    )

    LiteFrame._handle_engine_event(frame, "playback.failed", {
        "engine": "spotify",
        "message": "Brak połączenia",
    })

    assert restored == [("old", "Poprzedni utwór")]
    assert frame._pending_spotify_playback is None
    assert frame._pending_playback_session is None
    assert SessionId.SPOTIFY not in frame._playing_sessions
    assert messages == ["Nie udało się odtworzyć Spotify: Brak połączenia"]
