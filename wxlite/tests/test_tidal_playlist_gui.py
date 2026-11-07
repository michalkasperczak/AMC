"""Account playlist flow respects the selected TIDAL rows."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import (  # noqa: E402
    Navigator, OpenTidalContainer, SessionId,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve  # noqa: E402
from amc_wx_lite import menu_model  # noqa: E402
from amc_wx_lite.command_palette import entries_for_session  # noqa: E402
from amc_wx_lite.shortcuts import TIDAL_SUPPORTED_ACTIONS, describe_action  # noqa: E402


def test_tidal_album_playlist_creation_and_cached_reopening() -> None:
    calls = []
    spoken = []
    focused = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, failed):
            try:
                done(work())
            except Exception as error:
                failed(error)

    class Dialog:
        opened = 0

        def __init__(self, _parent, *, choices, target_label, provider):
            assert provider == "TIDAL"
            assert target_label == "Pierwszy utwór"
            assert all(choice.description == choice.name for choice in choices)
            self.__class__.opened += 1
            if self.opened == 1:
                assert [choice.name for choice in choices] == ["Istniejąca"]
                self.selected_playlist_ids = ["playlists:1"]
                self.new_playlists = [("Nowa", False)]
            else:
                assert [choice.name for choice in choices] == ["Istniejąca", "Nowa"]
                assert all(choice.selected for choice in choices)
                self.selected_playlist_ids = ["playlists:1", "playlists:2"]
                self.new_playlists = []

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return None

        def ShowModal(self):
            return gui.wx.ID_OK

    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_container(
        OpenTidalContainer("album:7", "albums:7", "Cały album", "album"),
        "Cały album",
        [Row("track:8", "Pierwszy utwór", "track", service_id="tracks:8")],
    )
    state = SimpleNamespace(service_picker_destination_ids={})
    client = SimpleNamespace(
        tidal_collection_view=lambda view: (
            calls.append(("list", view)) or {"items": [
                {"externalId": "playlists:1", "title": "Istniejąca"}
            ]}
        ),
        tidal_playlist_create=lambda name: (
            calls.append(("create", name)) or
            {"playlist": {"externalId": "playlists:2", "title": name}}
        ),
        tidal_playlist_add_items=lambda ids, items: (
            calls.append(("add", list(ids), list(items))) or
            {"message": "Dodano 2 utwory do playlist TIDAL",
             "succeededPlaylistExternalIds": list(ids)}
        ),
    )
    frame = SimpleNamespace(
        navigator=nav,
        client=client,
        runner=Runner(),
        state=state,
        store=SimpleNamespace(save=lambda _state: None),
        announcer=SimpleNamespace(say=spoken.append),
        _playlist_membership_pending=False,
        _selected_action_rows=lambda: [
            Row("track:8", "Pierwszy utwór", "track", service_id="tracks:8")
        ],
        _restore_focus_after_dialog=lambda: focused.append(True),
    )
    previous = gui.SpotifyPlaylistDialog
    gui.SpotifyPlaylistDialog = Dialog
    try:
        gui.LiteFrame._manage_tidal_playlist_membership(frame)
        gui.LiteFrame._manage_tidal_playlist_membership(frame)
    finally:
        gui.SpotifyPlaylistDialog = previous

    assert calls.count(("list", "playlists")) == 1
    assert calls[1] == ("create", "Nowa")
    assert calls[2][0:2] == ("add", ["playlists:1", "playlists:2"])
    assert calls[2][2][0]["kind"] == "track"
    assert calls[2][2][0]["externalId"] == "tracks:8"
    assert focused == [True, True]
    assert spoken.count("Pobieram playlisty TIDAL") == 1
    assert frame._playlist_membership_pending is False
    assert state.service_picker_destination_ids["tidal:playlists"] == [
        "playlists:1", "playlists:2"
    ]


def test_tidal_playlist_uses_multiple_selected_tracks_or_explicit_album() -> None:
    track_one = Row("track:8", "Pierwszy", "track", service_id="tracks:8")
    track_two = Row("track:9", "Drugi", "track", service_id="tracks:9")
    album = Row("album:7", "Cały album", "album", service_id="albums:7")
    for selected, expected in (
        ([track_one, track_two], ["tracks:8", "tracks:9"]),
        ([album], ["albums:7"]),
    ):
        captured = []

        class Runner:
            @staticmethod
            def submit(_name, work, done, failed):
                try:
                    done(work())
                except Exception as error:
                    failed(error)

        class Dialog:
            def __init__(self, *_args, **_kwargs):
                self.selected_playlist_ids = ["playlists:1"]
                self.new_playlists = []

            def __enter__(self):
                return self

            def __exit__(self, *_args):
                return None

            def ShowModal(self):
                return gui.wx.ID_OK

        nav = Navigator()
        nav.active = SessionId.TIDAL
        nav.apply_tidal_container(
            OpenTidalContainer("album:7", "albums:7", "Cały album", "album"),
            "Cały album", [track_one, track_two],
        )
        frame = SimpleNamespace(
            navigator=nav,
            client=SimpleNamespace(
                tidal_collection_view=lambda _view: {"items": [
                    {"externalId": "playlists:1", "title": "Lista"}
                ]},
                tidal_playlist_add_items=lambda _ids, items: (
                    captured.extend(items) or {
                        "message": "Dodano", "succeededPlaylistExternalIds": ["playlists:1"]
                    }
                ),
            ),
            runner=Runner(),
            state=SimpleNamespace(service_picker_destination_ids={}),
            store=SimpleNamespace(save=lambda _state: None),
            announcer=SimpleNamespace(say=lambda _message: None),
            _playlist_membership_pending=False,
            _selected_action_rows=lambda: selected,
            _restore_focus_after_dialog=lambda: None,
        )
        previous = gui.SpotifyPlaylistDialog
        gui.SpotifyPlaylistDialog = Dialog
        try:
            gui.LiteFrame._manage_tidal_playlist_membership(frame)
        finally:
            gui.SpotifyPlaylistDialog = previous
        assert [item["externalId"] for item in captured] == expected


def test_tidal_playlist_with_no_selected_row_does_not_add_open_album() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_container(
        OpenTidalContainer("album:7", "albums:7", "Cały album", "album"),
        "Cały album", [],
    )
    spoken = []
    frame = SimpleNamespace(
        navigator=nav,
        client=object(),
        announcer=SimpleNamespace(say=spoken.append),
        _playlist_membership_pending=False,
        _selected_action_rows=lambda: [],
    )
    gui.LiteFrame._manage_tidal_playlist_membership(frame)
    assert spoken == ["Do playlisty TIDAL wybierz utwór, wideo albo album"]


def test_tidal_playlist_shortcut_uses_session_command() -> None:
    assert resolve(
        Chord("P", ctrl=True, shift=True),
        player_view=False,
        radio_session=False,
        tidal_session=True,
    ) is Action.MANAGE_PLAYLIST_MEMBERSHIP
    assert Action.MANAGE_PLAYLIST_MEMBERSHIP in TIDAL_SUPPORTED_ACTIONS
    assert any(
        item.action is Action.MANAGE_PLAYLIST_MEMBERSHIP
        and item.visible_sessions == (SessionId.TIDAL,)
        and item.shortcut == "Ctrl+Shift+P"
        for menu in menu_model.build_menus() for item in menu.items
    )
    assert any(
        entry.action is Action.MANAGE_PLAYLIST_MEMBERSHIP
        for entry in entries_for_session(SessionId.TIDAL)
    )
    assert "playlist TIDAL" in describe_action(
        Action.MANAGE_PLAYLIST_MEMBERSHIP, session_label="TIDAL"
    )
