"""Ctrl+Shift+P w Spotify: dostępne etykiety, fokus i uczciwy zapis."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.spotify_playlist_dialog import (  # noqa: E402
    SpotifyPlaylistDialog,
    SpotifyPlaylistPayloadError,
    choices_from_payload,
)


def test_playlist_payload_uses_only_explicit_accessible_labels() -> None:
    choices = choices_from_payload({
        "playlists": [{
            "id": "secret-id",
            "name": "Poranek",
            "accessibleLabel": "Poranek, prywatna, właściciel Michał",
            "snapshotId": "secret-snapshot",
        }]
    })

    assert choices[0].label == (
        "Poranek, prywatna, właściciel Michał, niezaznaczona"
    )
    assert "secret-id" not in choices[0].label
    assert "secret-snapshot" not in choices[0].label
    assert str(choices[0]) == choices[0].label


def test_playlist_payload_rejects_missing_accessible_label() -> None:
    try:
        choices_from_payload({"playlists": [{"id": "p1", "name": "Nazwa"}]})
    except SpotifyPlaylistPayloadError as error:
        assert "niepełny wpis" in str(error)
    else:
        raise AssertionError("Niepełny wpis nie został odrzucony")


class _Dialog:
    def __init__(self, _parent, *, choices, target_label) -> None:
        assert target_label == "2 elementy"
        assert [choice.name for choice in choices] == ["Istniejąca"]
        self.selected_playlist_ids = ["existing"]
        self.new_playlists = [("Nowa prywatna", False)]

    def __enter__(self):
        return self

    def __exit__(self, *_args) -> None:
        return None

    def ShowModal(self) -> int:
        return gui.wx.ID_OK


def test_spotify_playlist_flow_creates_then_adds_and_restores_focus() -> None:
    rows = [
        Row("row-1", "Pierwszy", "track", service_id="track-1"),
        Row("row-2", "Drugi", "episode", service_id="episode-2"),
    ]
    calls: list[tuple[str, object]] = []
    spoken: list[str] = []
    focused: list[bool] = []

    client = SimpleNamespace(
        spotify_playlist_targets=lambda: {
            "playlists": [{
                "id": "existing",
                "name": "Istniejąca",
                "accessibleLabel": "Istniejąca, prywatna",
            }]
        },
        spotify_playlist_create=lambda name, is_public=False: (
            calls.append(("create", (name, is_public)))
            or {
                "playlist": {"id": "created"},
                "message": "Utworzono playlistę Spotify: Nowa prywatna, prywatna.",
            }
        ),
        spotify_playlist_add_items=lambda playlist_ids, items: (
            calls.append(("add", (playlist_ids, items)))
            or {"message": "Dodano 4 pozycje do wybranych playlist Spotify."}
        ),
    )

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        client=client,
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        _playlist_membership_pending=False,
        _selected_action_rows=lambda: rows,
        _restore_focus_after_dialog=lambda: focused.append(True),
    )
    previous = gui.SpotifyPlaylistDialog
    gui.SpotifyPlaylistDialog = _Dialog
    try:
        gui.LiteFrame._manage_spotify_playlist_membership(frame)
    finally:
        gui.SpotifyPlaylistDialog = previous

    assert calls[0] == ("create", ("Nowa prywatna", False))
    assert calls[1][0] == "add"
    playlist_ids, items = calls[1][1]
    assert playlist_ids == ["existing", "created"]
    assert [item["kind"] for item in items] == ["track", "episode"]
    assert focused == [True]
    assert spoken[0] == "Pobieram playlisty Spotify"
    assert spoken[1] == "Tworzę playlistę Spotify i dodaję wybrane elementy"
    assert "Utworzono playlistę Spotify" in spoken[-1]
    assert "Dodano 4 pozycje" in spoken[-1]
    assert frame._playlist_membership_pending is False


def test_spotify_playlist_flow_does_not_claim_unconfirmed_creation() -> None:
    spoken: list[str] = []

    class _NewOnlyDialog(_Dialog):
        def __init__(self, _parent, *, choices, target_label) -> None:
            assert choices == []
            assert target_label == "Pierwszy"
            self.selected_playlist_ids = []
            self.new_playlists = [("Bez identyfikatora", False)]

    client = SimpleNamespace(
        spotify_playlist_targets=lambda: {"playlists": []},
        spotify_playlist_create=lambda _name, is_public=False: {
            "playlist": {},
            "message": "Utworzono playlistę Spotify",
        },
        spotify_playlist_add_items=lambda _playlist_ids, _items: (_ for _ in ()).throw(
            AssertionError("Nie wolno dodawać do niepotwierdzonej playlisty")
        ),
    )

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        client=client,
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        _playlist_membership_pending=False,
        _selected_action_rows=lambda: [
            Row("row-1", "Pierwszy", "track", service_id="track-1")
        ],
        _restore_focus_after_dialog=lambda: None,
    )
    previous = gui.SpotifyPlaylistDialog
    gui.SpotifyPlaylistDialog = _NewOnlyDialog
    try:
        gui.LiteFrame._manage_spotify_playlist_membership(frame)
    finally:
        gui.SpotifyPlaylistDialog = previous

    assert spoken[-1] == (
        "Spotify nie potwierdził utworzenia playlisty Bez identyfikatora"
    )
    assert frame._playlist_membership_pending is False


def test_spotify_playlist_flow_caches_created_playlist_and_last_destinations() -> None:
    target_calls: list[bool] = []
    added_to: list[list[str]] = []
    spoken: list[str] = []
    saved_states: list[list[str]] = []

    class _DialogSequence:
        opened = 0

        def __init__(self, _parent, *, choices, target_label) -> None:
            assert target_label == "Pierwszy"
            type(self).opened += 1
            if self.opened == 1:
                assert [choice.name for choice in choices] == ["Istniejąca"]
                assert not choices[0].selected
                self.selected_playlist_ids = ["existing"]
                self.new_playlists = [("Nowa publiczna", True)]
            else:
                assert [choice.name for choice in choices] == [
                    "Istniejąca",
                    "Nowa publiczna",
                ]
                assert [choice.selected for choice in choices] == [True, True]
                self.selected_playlist_ids = ["existing", "created"]
                self.new_playlists = []

        def __enter__(self):
            return self

        def __exit__(self, *_args) -> None:
            return None

        def ShowModal(self) -> int:
            return gui.wx.ID_OK

    client = SimpleNamespace(
        spotify_playlist_targets=lambda: (
            target_calls.append(True)
            or {
                "playlists": [{
                    "id": "existing",
                    "name": "Istniejąca",
                    "accessibleLabel": "Istniejąca, prywatna",
                }]
            }
        ),
        spotify_playlist_create=lambda _name, is_public=False: {
            "playlist": {
                "id": "created",
                "name": "",
                "accessibleLabel": "",
            },
            "message": "Utworzono playlistę Spotify: Nowa publiczna, publiczna.",
        },
        spotify_playlist_add_items=lambda playlist_ids, _items: (
            added_to.append(list(playlist_ids))
            or {
                "message": "Dodano pozycję do playlist Spotify.",
                "playlists": [
                    {
                        "playlistId": playlist_id,
                        "added": 1,
                        "alreadyPresent": 0,
                        "failed": 0,
                    }
                    for playlist_id in playlist_ids
                ],
            }
        ),
    )

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    state = SimpleNamespace(
        spotify_playlist_destination_ids=[],
        service_picker_destination_ids={},
    )
    frame = SimpleNamespace(
        client=client,
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        state=state,
        store=SimpleNamespace(
            save=lambda current: saved_states.append(
                list(current.service_picker_destination_ids["spotify:playlists"])
            )
        ),
        _playlist_membership_pending=False,
        _selected_action_rows=lambda: [
            Row("row-1", "Pierwszy", "track", service_id="track-1")
        ],
        _restore_focus_after_dialog=lambda: None,
    )
    previous = gui.SpotifyPlaylistDialog
    gui.SpotifyPlaylistDialog = _DialogSequence
    try:
        gui.LiteFrame._manage_spotify_playlist_membership(frame)
        gui.LiteFrame._manage_spotify_playlist_membership(frame)
    finally:
        gui.SpotifyPlaylistDialog = previous

    assert len(target_calls) == 1
    assert spoken.count("Pobieram playlisty Spotify") == 1
    assert added_to == [
        ["existing", "created"],
        ["existing", "created"],
    ]
    assert state.spotify_playlist_destination_ids == ["existing", "created"]
    assert state.service_picker_destination_ids == {
        "spotify:playlists": ["existing", "created"]
    }
    assert saved_states[-1] == ["existing", "created"]


def test_new_playlist_dialog_prepares_clear_selected_row_and_instruction() -> None:
    import amc_wx_lite.service_playlist_dialog as playlist_dialog

    spoken: list[str] = []
    focused: list[bool] = []
    rebuilt: list[str] = []

    class _NameDialog:
        result_name = "Wieczór"
        result_public = False

        def __init__(self, _parent) -> None:
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args) -> None:
            return None

        def ShowModal(self) -> int:
            return gui.wx.ID_OK

    dialog = object.__new__(SpotifyPlaylistDialog)
    dialog._temporary_counter = 0
    dialog._choices = []
    dialog.status = object()
    dialog.playlist_list = SimpleNamespace(SetFocus=lambda: focused.append(True))
    dialog._rebuild = rebuilt.append
    previous_name_dialog = playlist_dialog.SpotifyPlaylistNameDialog
    previous_announce = playlist_dialog.announce_status
    playlist_dialog.SpotifyPlaylistNameDialog = _NameDialog
    playlist_dialog.announce_status = lambda _control, message: spoken.append(message)
    try:
        SpotifyPlaylistDialog._create(dialog)
    finally:
        playlist_dialog.SpotifyPlaylistNameDialog = previous_name_dialog
        playlist_dialog.announce_status = previous_announce

    assert dialog._choices[0].label == "Nowa playlista Wieczór, prywatna, zaznaczona"
    assert rebuilt == ["new-1"] and focused == [True]
    assert spoken == [
        "Przygotowano nową playlistę Wieczór, prywatna, zaznaczoną. "
        "Naciśnij Enter, aby ją utworzyć i dodać wybrane elementy"
    ]
