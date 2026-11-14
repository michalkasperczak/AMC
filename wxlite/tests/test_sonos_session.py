from __future__ import annotations

from pathlib import Path
from types import MethodType, SimpleNamespace

from amc_wx_lite import gui as gui_module
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import HostError, LiteHostClient
from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.menu_model import build_menus
from amc_wx_lite.navigation import (
    ActivateSonosGroup,
    LibraryView,
    Navigator,
    OpenSonosAccount,
    OpenSonosView,
    PlaySonosMaterial,
    SessionId,
    View,
)
from amc_wx_lite.profile_presets import PresetEntry
from amc_wx_lite.shortcuts import Action, Chord, SONOS_SUPPORTED_ACTIONS, resolve
from amc_wx_lite.sonos_source import (
    library_rows,
    material_rows,
    now_playing_label,
    own_stream_edit_info,
    playback_status_label,
    snapshot,
    target_rows,
)


def test_ctrl_6_and_menu_open_the_sonos_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("6", ctrl=True),
            player_view=player,
            radio_session=False,
        ) is Action.SESSION_SONOS
    view = next(
        menu
        for menu in build_menus([session.value for session in SessionId])
        if menu.title == "&Widok"
    )
    item = next(entry for entry in view.items if entry.action is Action.SESSION_SONOS)
    assert item.label == "Sesja: &Sonos"
    assert item.shortcut == "Ctrl+6"


def test_sonos_content_and_target_shortcuts_work_in_list_and_player() -> None:
    expected = {
        "Ctrl+L": Action.VIEW_LIBRARY,
        "Ctrl+U": Action.VIEW_FAVORITES,
        "Ctrl+P": Action.VIEW_PLAYLISTS,
        "Ctrl+F5": Action.SELECT_SONOS_TARGET,
    }
    for player in (False, True):
        for chord, action in expected.items():
            parts = chord.split("+")
            assert resolve(
                Chord(
                    parts[-1],
                    ctrl="Ctrl" in parts,
                    shift="Shift" in parts,
                    alt="Alt" in parts,
                ),
                player_view=player,
                radio_session=False,
                sonos_session=True,
            ) is action


def test_sonos_own_stream_edit_shortcuts_are_strictly_contextual() -> None:
    expected = {
        "Ctrl+N": Action.ADD_SONOS_OWN_STREAM,
        "F2": Action.EDIT_SONOS_OWN_STREAM,
        "Delete": Action.REMOVE_SONOS_OWN_STREAMS,
        "Ctrl+O": Action.IMPORT_SONOS_OWN_STREAMS,
        "Alt+1": Action.SORT_SONOS_OWN_STREAMS_ADDED,
        "Alt+2": Action.SORT_SONOS_OWN_STREAMS_ALPHABETICAL,
        "Alt+3": Action.SORT_SONOS_OWN_STREAMS_CUSTOM,
        "Alt+Up": Action.MOVE_SONOS_OWN_STREAM_UP,
        "Alt+Down": Action.MOVE_SONOS_OWN_STREAM_DOWN,
        "Ctrl+X": Action.CUT_SONOS_OWN_STREAMS,
        "Ctrl+V": Action.PASTE_SONOS_OWN_STREAMS,
        "Ctrl+E": Action.EXPORT_SONOS_OWN_STREAMS,
    }
    for chord, action in expected.items():
        parts = chord.split("+")
        parsed = Chord(
            parts[-1],
            ctrl="Ctrl" in parts,
            shift="Shift" in parts,
            alt="Alt" in parts,
        )
        assert resolve(
            parsed,
            player_view=False,
            radio_session=False,
            sonos_session=True,
            sonos_own_streams=True,
        ) is action
        assert resolve(
            parsed,
            player_view=False,
            radio_session=False,
            sonos_session=True,
            sonos_own_streams=False,
        ) is None


def test_sonos_library_categories_materials_and_back_are_typed() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.SONOS)
    categories = list(library_rows({
        "items": [
            {
                "itemId": "sonos.library.favorites",
                "title": "Ulubione Sonos",
                "detail": "Enter otwiera listę",
                "kind": "favorites",
            },
            {
                "itemId": "sonos.library.playlists",
                "title": "Playlisty Sonos",
                "detail": "Enter otwiera listę",
                "kind": "playlists",
            },
            {
                "itemId": "sonos.library.ownstreams",
                "title": "Moje stacje",
                "detail": "Enter otwiera listę",
                "kind": "ownStreams",
            },
        ]
    }, "house-1"))
    nav.apply_sonos_library(categories, household_id="house-1")
    opened = nav.activate_selected()
    assert opened == [OpenSonosView(
        LibraryView.SONOS_FAVORITES,
        household_id="house-1",
        preferred_id=None,
    )]

    nav.apply_sonos_library(categories, household_id="house-1")
    assert nav.session.model.select_id("sonos.library.ownstreams")
    assert nav.activate_selected() == [OpenSonosView(
        LibraryView.SONOS_OWN_STREAMS,
        household_id="house-1",
        preferred_id=None,
    )]

    favorites = list(material_rows({
        "items": [{
            "itemId": "sonos-favorite:item-1",
            "materialId": "favorite-1",
            "title": "Radio Poznań",
            "kind": "favorite",
        }]
    }, "house-1"))
    nav.apply_sonos_materials(
        LibraryView.SONOS_FAVORITES,
        favorites,
        household_id="house-1",
        return_id="sonos.library.favorites",
    )
    played = nav.activate_selected()
    assert played == [PlaySonosMaterial(
        item_id="sonos-favorite:item-1",
        title="Radio Poznań",
        household_id="house-1",
        kind="favorite",
        material_id="favorite-1",
    )]
    nav.back_to_list()
    assert nav.go_to_parent() == [OpenSonosView(
        LibraryView.SONOS_LIBRARY,
        household_id="house-1",
        preferred_id="sonos.library.favorites",
    )]


def test_sonos_own_stream_is_a_typed_material_without_spoken_url() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.SONOS)
    rows = list(material_rows({
        "items": [{
            "itemId": "sonos-stream:technical-item",
            "materialId": "technical-material",
            "title": "Radio domowe",
            "detail": "Stacja zapisana w AMC",
            "kind": "ownStream",
            "streamUrl": "https://secret.example/live",
        }]
    }, "house-1"))
    nav.apply_sonos_materials(
        LibraryView.SONOS_OWN_STREAMS,
        rows,
        household_id="house-1",
        return_id="sonos.library.ownstreams",
    )

    assert rows[0].title == "Radio domowe"
    assert rows[0].detail == ""
    assert rows[0].show_kind is False
    assert rows[0].address == "https://secret.example/live"
    assert "secret.example" not in rows[0].title
    assert "secret.example" not in rows[0].detail
    assert nav.activate_selected() == [PlaySonosMaterial(
        item_id="sonos-stream:technical-item",
        title="Radio domowe",
        household_id="house-1",
        kind="ownStream",
        material_id="technical-material",
    )]


def test_sonos_own_stream_url_is_exposed_only_to_explicit_editor() -> None:
    assert own_stream_edit_info({
        "streamId": "stream-1",
        "name": "Radio domowe",
        "url": "https://radio.example/live",
        "runtimeObject": "SonosOwnStreamSettings { StreamUrl = secret }",
    }) == (
        "stream-1",
        "Radio domowe",
        "https://radio.example/live",
    )


def test_sonos_material_uses_active_group_and_stays_on_one_request_lane() -> None:
    submissions: list = []
    calls: list = []
    frame = SimpleNamespace(
        client=SimpleNamespace(
            sonos_load_material=lambda *args: calls.append(args) or {}
        ),
        announcer=SimpleNamespace(say=lambda _text: None),
        runner=SimpleNamespace(submit=lambda *args: submissions.append(args)),
        navigator=SimpleNamespace(active=SessionId.SONOS),
        _sonos_active_household_id="house-1",
        _sonos_active_group_id="group-1",
        _sonos_request_pending=False,
        _sonos_retry_pending=False,
        _sonos_pending_material=None,
    )
    intent = PlaySonosMaterial(
        "item-1", "Radio Poznań", "house-1", "favorite", "favorite-1"
    )

    LiteFrame._play_sonos_material(frame, intent)

    assert len(submissions) == 1
    stream, work, _done, _failed = submissions[0]
    assert stream == "sonos-material"
    assert frame._sonos_request_pending is True
    work()
    assert calls == [("house-1", "group-1", "favorite", "favorite-1")]


def test_sonos_activation_separates_labels_from_internal_ids() -> None:
    nav = Navigator()
    rows = target_rows({
        "items": [{
            "itemId": "sonos:group-1",
            "householdId": "house-technical",
            "groupId": "group-technical",
            "title": "Salon",
            "detail": "Dwa głośniki",
        }]
    })
    nav.apply_sonos_targets(list(rows))
    nav.switch_session(SessionId.SONOS)

    intents = nav.activate_selected()

    assert len(intents) == 1
    intent = intents[0]
    assert isinstance(intent, ActivateSonosGroup)
    assert intent.title == "Salon"
    assert intent.household_id == "house-technical"
    assert intent.group_id == "group-technical"
    row = nav.sessions[SessionId.SONOS].model.selected_row
    assert row is not None
    assert row.title == "Salon"
    assert row.detail == "Dwa głośniki"
    assert "technical" not in row.title
    assert "technical" not in row.detail
    assert nav.view is View.LIST


def test_empty_sonos_session_exposes_one_login_row_for_enter() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.SONOS)

    announcements = nav.apply_sonos_account_prompt()

    row = nav.sessions[SessionId.SONOS].model.selected_row
    assert row is not None
    assert row.title == "Rozpocznij logowanie do Sonos"
    assert row.detail == "Enter otwiera konto Sonos"
    assert row.kind_label == ""
    assert "Naciśnij Enter" in announcements[0].text
    intents = nav.activate_selected()
    assert len(intents) == 1
    assert isinstance(intents[0], OpenSonosAccount)


def test_sonos_target_failure_replaces_empty_list_with_login_row() -> None:
    spoken: list[str] = []
    applied: list[list[object]] = []
    navigator = SimpleNamespace(
        active=SessionId.SONOS,
        apply_sonos_account_prompt=lambda: ["login-prompt"],
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(sonos_targets=lambda: None),
        navigator=navigator,
        announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(
            submit=lambda _name, _work, _done, failed: failed(
                RuntimeError("Nie ma połączonego konta Sonos")
            )
        ),
        _run=applied.append,
    )

    LiteFrame._load_sonos_targets(frame)

    assert spoken == [
        "Nie udało się odczytać grup Sonos: Nie ma połączonego konta Sonos"
    ]
    assert applied == [["login-prompt"]]


def test_sonos_target_failure_does_not_call_network_error_a_missing_account() -> None:
    spoken: list[str] = []
    applied: list[list[object]] = []
    navigator = SimpleNamespace(
        active=SessionId.SONOS,
        apply_sonos_account_prompt=lambda: ["login-prompt"],
    )
    frame = SimpleNamespace(
        client=SimpleNamespace(sonos_targets=lambda: None),
        navigator=navigator,
        announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(
            submit=lambda _name, _work, _done, failed: failed(
                RuntimeError("Brak połączenia z usługą Sonos")
            )
        ),
        _run=applied.append,
    )

    LiteFrame._load_sonos_targets(frame)

    assert spoken == [
        "Nie udało się odczytać grup Sonos: Brak połączenia z usługą Sonos"
    ]
    assert applied == []


def test_sonos_material_uses_shared_stay_on_list_preference() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.SONOS)
    nav.apply_sonos_materials(
        LibraryView.SONOS_FAVORITES,
        list(material_rows({
            "items": [{
                "itemId": "sonos-favorite:item-1",
                "materialId": "favorite-1",
                "title": "Radio Poznań",
                "kind": "favorite",
            }]
        }, "house-1")),
        household_id="house-1",
    )

    intents = nav.activate_selected(open_player_on_activation=False)

    assert isinstance(intents[0], PlaySonosMaterial)
    assert nav.view is View.LIST


def test_sonos_target_keeps_normalized_members_out_of_accessible_text() -> None:
    rows = target_rows({
        "items": [{
            "itemId": "sonos:house-private:group-private",
            "householdId": "house-private",
            "groupId": "group-private",
            "playerIds": ["player-b", "player-a", "player-a"],
            "title": "Salon",
            "detail": "głośników: 2, zatrzymane",
        }]
    })

    assert rows[0].service_members == ("player-a", "player-b")
    assert rows[0].title == "Salon"
    assert rows[0].detail == "głośników: 2, zatrzymane"
    assert "player-a" not in rows[0].title
    assert "player-a" not in rows[0].detail


def test_sonos_snapshot_allowlists_accessible_text() -> None:
    state = snapshot({
        "itemId": "sonos:group-1",
        "householdId": "house-1",
        "groupId": "group-1",
        "groupTitle": "Salon",
        "title": "Audycja",
        "source": "Radio Poznań",
        "stateText": "odtwarzanie",
        "volumeText": "głośność 35%",
        "positionText": "1:01 z 10:00",
        "playbackState": "playing",
        "volume": 35,
        "muted": False,
        "fixedVolume": False,
        "positionSeconds": 61,
        "durationSeconds": 600,
        "message": "Audycja, odtwarzanie",
        "token": "secret",
        "runtimeObject": "SonosGroup { Id = group-1 }",
    })

    assert now_playing_label(state) == "Audycja, Radio Poznań"
    assert "token" not in state
    assert "runtimeObject" not in state
    assert "secret" not in repr(state)


def test_sonos_nvda_end_uses_last_material_and_only_available_parameters() -> None:
    state = snapshot({
        "groupTitle": "Salon",
        "title": "Brak informacji",
        "source": "",
        "stateText": "zatrzymane",
        "volumeText": "głośność 35%",
        "playbackState": "idle",
        "volume": 35,
    })

    text = playback_status_label(state, fallback_title="Radio domowe")

    assert text == "Radio domowe, zatrzymane, głośność 35%"
    assert "Salon" not in text
    assert "kb/s" not in text


def test_sonos_command_gate_blocks_local_only_features() -> None:
    assert {
        Action.PLAY_PAUSE,
        Action.SELECT_AUDIO_OUTPUT,
        Action.QUEUE_PREVIOUS,
        Action.QUEUE_NEXT,
        Action.SEEK_FORWARD_10,
        Action.SEEK_PERCENT_50,
        Action.VOLUME_UP_5,
        Action.TIME_ELAPSED,
        Action.COPY_NAME,
        Action.COPY_ADDRESS,
        Action.ADD_SONOS_OWN_STREAM,
        Action.EDIT_SONOS_OWN_STREAM,
        Action.REMOVE_SONOS_OWN_STREAMS,
        Action.IMPORT_SONOS_OWN_STREAMS,
        Action.SORT_SONOS_OWN_STREAMS_ALPHABETICAL,
        Action.MOVE_SONOS_OWN_STREAM_UP,
        Action.MOVE_SONOS_OWN_STREAM_DOWN,
        Action.CUT_SONOS_OWN_STREAMS,
        Action.PASTE_SONOS_OWN_STREAMS,
        Action.EXPORT_SONOS_OWN_STREAMS,
        Action.VIEW_PRESETS,
        Action.ASSIGN_PRESET,
        Action.PRESET_1,
        Action.PRESET_12,
    } <= SONOS_SUPPORTED_ACTIONS
    assert {
        Action.ADD_BOOKMARK,
        Action.RATE_UP,
        Action.RECORD_TOGGLE,
        Action.ADD_TO_QUEUE,
        Action.PREVIOUS_AUDIO_OUTPUT,
        Action.NEXT_AUDIO_OUTPUT,
    }.isdisjoint(SONOS_SUPPORTED_ACTIONS)


def test_fixed_sonos_preset_refreshes_topology_and_uses_exact_group() -> None:
    submissions: list[str] = []
    played: list[PlaySonosMaterial] = []
    spoken: list[str] = []
    payload = {
        "items": [{
            "itemId": "sonos:house-1:group-2",
            "householdId": "house-1",
            "groupId": "group-2",
            "playerIds": ["player-b", "player-a"],
            "title": "Cały salon",
            "detail": "głośników: 2, zatrzymane",
        }]
    }
    sonos_state = SimpleNamespace(sonos_target_rows=())
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.SONOS,
            sessions={SessionId.SONOS: sonos_state},
        ),
        client=SimpleNamespace(sonos_targets=lambda: payload),
        runner=SimpleNamespace(submit=lambda name, work, done, _failed: (
            submissions.append(name), done(work())
        )),
        announcer=SimpleNamespace(say=spoken.append),
        _sonos_active_household_id="house-old",
        _sonos_active_group_id="group-old",
        _play_sonos_material=played.append,
    )
    frame._start_sonos_preset_material = MethodType(
        LiteFrame._start_sonos_preset_material, frame
    )
    entry = PresetEntry(
        4,
        "favorite-1",
        "sonos-favorite",
        "Radio domowe",
        None,
        sonos_household_id="house-1",
        sonos_fixed_player_ids=("player-a", "player-b"),
    )

    LiteFrame._activate_sonos_preset(frame, entry, "4")

    assert submissions == ["sonos-preset-target"]
    assert spoken == []
    assert frame._sonos_active_household_id == "house-1"
    assert frame._sonos_active_group_id == "group-2"
    assert len(played) == 1
    assert played[0].kind == "favorite"
    assert played[0].material_id == "favorite-1"
    assert "player-a" not in played[0].title


def test_sonos_preset_never_sends_household_bound_material_to_another_home() -> None:
    played: list[PlaySonosMaterial] = []
    spoken: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SONOS),
        announcer=SimpleNamespace(say=spoken.append),
        _sonos_active_household_id="house-2",
        _sonos_active_group_id="group-2",
        _play_sonos_material=played.append,
        _start_sonos_preset_material=None,
    )
    frame._start_sonos_preset_material = MethodType(
        LiteFrame._start_sonos_preset_material, frame
    )
    entry = PresetEntry(
        2,
        "favorite-opaque",
        "sonos-favorite",
        "Radio domowe",
        None,
        sonos_household_id="house-1",
    )

    LiteFrame._activate_sonos_preset(frame, entry, "2")

    assert played == []
    assert spoken == [
        "Preset 2 zapisano dla innego domu Sonos. "
        "Przypisz go ponownie w tym domu. Nic nie uruchomiłem"
    ]


def test_sonos_host_client_uses_narrow_commands_and_arguments() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.sonos_targets()
    client.sonos_snapshot("house-1", "group-1")
    client.sonos_transport(
        "house-1", "group-1", "seekRelative", seconds=30
    )
    client.sonos_library()
    client.sonos_materials("house-1", "favorite")
    client.sonos_materials("house-1", "ownStream")
    client.sonos_load_material(
        "house-1", "group-1", "favorite", "favorite-1"
    )
    client.sonos_load_material(
        "house-1", "group-1", "ownStream", "stream-1"
    )
    client.sonos_own_stream_edit_info("stream-1")
    client.sonos_add_own_stream("Radio domowe", "https://radio.example/live")
    client.sonos_edit_own_stream(
        "stream-1", "Radio nowe", "https://radio.example/new"
    )
    client.sonos_remove_own_streams(["stream-1", "stream-2"])
    client.sonos_import_own_streams("C:/listy/sonos.m3u")
    client.sonos_sort_own_streams("Alphabetical")
    client.sonos_move_own_streams(["stream-1", "stream-2"], -1)
    client.sonos_place_own_streams(["stream-1"], "stream-2")
    client.sonos_export_own_streams("C:/listy/sonos-export.m3u")

    assert calls == [
        ("sonos.targets", None, 60.0),
        (
            "sonos.snapshot",
            {"householdId": "house-1", "groupId": "group-1"},
            45.0,
        ),
        (
            "sonos.transport",
            {
                "householdId": "house-1",
                "groupId": "group-1",
                "command": "seekRelative",
                "seconds": 30.0,
            },
            45.0,
        ),
        ("sonos.library", None, 10.0),
        (
            "sonos.materials",
            {"householdId": "house-1", "kind": "favorite"},
            60.0,
        ),
        (
            "sonos.materials",
            {"householdId": "house-1", "kind": "ownStream"},
            60.0,
        ),
        (
            "sonos.loadMaterial",
            {
                "householdId": "house-1",
                "groupId": "group-1",
                "kind": "favorite",
                "materialId": "favorite-1",
            },
            60.0,
        ),
        (
            "sonos.loadMaterial",
            {
                "householdId": "house-1",
                "groupId": "group-1",
                "kind": "ownStream",
                "materialId": "stream-1",
            },
            60.0,
        ),
        (
            "sonos.ownStreamEditInfo",
            {"streamId": "stream-1"},
            15.0,
        ),
        (
            "sonos.ownStreamAdd",
            {"name": "Radio domowe", "url": "https://radio.example/live"},
            30.0,
        ),
        (
            "sonos.ownStreamEdit",
            {
                "streamId": "stream-1",
                "name": "Radio nowe",
                "url": "https://radio.example/new",
            },
            30.0,
        ),
        (
            "sonos.ownStreamRemove",
            {"itemIds": ["stream-1", "stream-2"]},
            30.0,
        ),
        (
            "sonos.ownStreamImport",
            {"path": "C:/listy/sonos.m3u"},
            60.0,
        ),
        (
            "sonos.ownStreamSort",
            {"mode": "Alphabetical"},
            30.0,
        ),
        (
            "sonos.ownStreamMove",
            {"itemIds": ["stream-1", "stream-2"], "direction": -1},
            30.0,
        ),
        (
            "sonos.ownStreamPlace",
            {"itemIds": ["stream-1"], "targetId": "stream-2"},
            30.0,
        ),
        (
            "sonos.ownStreamExport",
            {"path": "C:/listy/sonos-export.m3u"},
            60.0,
        ),
    ]


def test_sonos_account_host_client_never_accepts_credentials() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.sonos_account_status()
    client.sonos_account_login_start()
    client.sonos_account_login_check()
    client.sonos_account_login_cancel()
    client.sonos_account_refresh()
    client.sonos_account_persist_retry()
    client.sonos_account_disconnect()

    assert calls == [
        ("sonos.accountStatus", None, 15.0),
        ("sonos.accountLoginStart", None, 60.0),
        ("sonos.accountLoginCheck", None, 60.0),
        ("sonos.accountLoginCancel", None, 15.0),
        ("sonos.accountRefresh", None, 60.0),
        ("sonos.accountPersistRetry", None, 15.0),
        ("sonos.accountDisconnect", None, 15.0),
    ]


def test_ctrl_f5_routes_connected_account_to_targets_and_missing_account_to_login() -> None:
    submissions: list[str] = []
    targets: list[str | None] = []
    accounts: list[object] = []
    spoken: list[str] = []
    payloads = [
        {"connected": True, "label": "Konto Sonos jest połączone."},
        {"connected": False, "label": "Konto Sonos nie jest połączone."},
    ]
    frame = SimpleNamespace(
        client=SimpleNamespace(sonos_account_status=lambda: payloads.pop(0)),
        navigator=SimpleNamespace(
            sessions={
                SessionId.SONOS: SimpleNamespace(list_anchor_id="sonos:home:group")
            }
        ),
        runner=SimpleNamespace(submit=lambda name, work, done, _failed: (
            submissions.append(name), done(work())
        )),
        announcer=SimpleNamespace(say=spoken.append),
        _load_sonos_targets=lambda preferred_id=None: targets.append(preferred_id),
        _show_sonos_account=accounts.append,
    )

    LiteFrame._select_sonos_target(frame)
    LiteFrame._select_sonos_target(frame)

    assert submissions == ["sonos-account-status", "sonos-account-status"]
    assert targets == ["sonos:home:group"]
    assert spoken == [
        "Konto Sonos jest połączone. Wczytuję głośniki i grupy."
    ]
    assert accounts == [
        {"connected": False, "label": "Konto Sonos nie jest połączone."}
    ]


def test_ctrl_f5_uses_local_discovery_without_account_check() -> None:
    targets: list[str | None] = []
    spoken: list[str] = []
    account_checks: list[str] = []
    local = SimpleNamespace(
        local_control=True,
        sonos_account_status=lambda: account_checks.append("unexpected"),
    )
    frame = SimpleNamespace(
        _sonos_local_client=local,
        client=SimpleNamespace(),
        navigator=SimpleNamespace(
            sessions={
                SessionId.SONOS: SimpleNamespace(list_anchor_id="saved-group")
            }
        ),
        announcer=SimpleNamespace(say=spoken.append),
        _load_sonos_targets=lambda preferred_id=None: targets.append(preferred_id),
    )

    LiteFrame._select_sonos_target(frame)

    assert targets == ["saved-group"]
    assert account_checks == []
    assert spoken == [
        "Wyszukuję głośniki i grupy Sonos w sieci lokalnej"
    ]


def test_session_manager_describes_local_sonos_without_login_language() -> None:
    source = Path(gui_module.__file__).read_text(encoding="utf-8")
    section = source[source.index("    def _session_readiness") :]
    section = section[:section.index("    # ---------------------------------------------------- Radio Archiwista")]
    sonos = section[section.index("SessionId.SONOS:") :]
    sonos = sonos[:sonos.index("SessionId.RADIOARCHIWISTA:")]
    assert 'else "Wymaga wyszukania głośników"' in sonos
    assert "Wymaga zalogowania" not in sonos


def test_sonos_cut_and_paste_preserve_all_selected_stream_ids() -> None:
    spoken: list[str] = []
    calls: list[tuple[list[str], str]] = []
    reloads: list[tuple[str | None, str | None]] = []
    selected = [
        Row(
            "sonos-stream:one",
            "Pierwsza",
            "sonosOwnStream",
            service_id="one",
            service_kind="ownStream",
        ),
        Row(
            "sonos-stream:three",
            "Trzecia",
            "sonosOwnStream",
            service_id="three",
            service_kind="ownStream",
        ),
    ]
    client = SimpleNamespace(
        sonos_place_own_streams=lambda ids, target: (
            calls.append((list(ids), target))
            or {
                "preferredStreamId": "one",
                "message": "Przeniesiono 2 stacje, od pozycji 1 z 3",
            }
        )
    )
    frame = SimpleNamespace(
        _sonos_own_stream_sort_mode="Custom",
        _sonos_pending_move_ids=[],
        _selected_sonos_own_stream_rows=lambda: selected,
        _profile_edit_client=lambda: client,
        announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(
            submit=lambda _name, work, done, _failed: done(work())
        ),
        _reload_sonos_own_streams=lambda **kwargs: reloads.append((
            kwargs.get("preferred_stream_id"),
            kwargs.get("announcement"),
        )),
    )

    LiteFrame._cut_sonos_own_streams(frame)
    assert frame._sonos_pending_move_ids == ["one", "three"]
    assert spoken == [
        "Zaznaczono 2 stacji do przeniesienia. "
        "Wybierz miejsce i naciśnij Control V"
    ]

    target = Row(
        "sonos-stream:two",
        "Druga",
        "sonosOwnStream",
        service_id="two",
        service_kind="ownStream",
    )
    frame._selected_sonos_own_stream_rows = lambda: [target]
    LiteFrame._paste_sonos_own_streams(frame)

    assert calls == [(["one", "three"], "two")]
    assert frame._sonos_pending_move_ids == []
    assert reloads == [(
        "one",
        "Przeniesiono stacje: 2, przed Druga",
    )]


def test_sonos_library_and_material_rows_keep_ids_out_of_spoken_text() -> None:
    categories = library_rows({
        "items": [{
            "itemId": "sonos.library.favorites",
            "title": "Ulubione Sonos",
            "detail": "Enter otwiera listę",
            "kind": "favorites",
        }]
    }, "house-technical")
    assert categories[0].title == "Ulubione Sonos"
    assert categories[0].detail == "Enter otwiera listę"
    assert categories[0].parent_id == "house-technical"
    assert "technical" not in categories[0].title
    assert "technical" not in categories[0].detail

    materials = material_rows({
        "items": [{
            "itemId": "sonos-favorite:technical-item",
            "materialId": "technical-material",
            "title": "Radio Poznań, TuneIn",
            "kind": "favorite",
            "runtimeObject": "Favorite { Id = technical-material }",
        }]
    }, "house-technical")
    assert materials[0].title == "Radio Poznań, TuneIn"
    assert materials[0].service_id == "technical-material"
    assert "technical" not in materials[0].title
    assert "runtimeObject" not in materials[0].detail


def test_sonos_and_wiim_style_own_stream_rows_speak_only_the_name() -> None:
    rows = material_rows({
        "items": [{
            "itemId": "sonos-stream:radio-poznan",
            "materialId": "radio-poznan",
            "title": "Radio Poznań",
            "kind": "ownStream",
            "detail": "Stacja zapisana w AMC",
            "streamUrl": "https://secret.example/live",
        }]
    }, "house-technical")
    model = ListModel()
    model.replace(rows)

    assert model.text_for(0, 0) == "Radio Poznań"
    assert model.text_for(0, 1) == ""
    assert model.text_for(0, 2) == ""
    assert model.selected_row is not None
    assert model.selected_row.address == "https://secret.example/live"


def test_sonos_dispatch_reaches_remote_volume_and_blocks_local_rate() -> None:
    spoken: list[str] = []
    volume_steps: list[int] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.SONOS),
        announcer=SimpleNamespace(say=spoken.append),
        messages=SimpleNamespace(custom_seek_seconds=300),
        _adjust_volume=volume_steps.append,
    )

    LiteFrame._dispatch(frame, Action.VOLUME_UP_5)
    assert volume_steps == [5]
    assert spoken == []

    LiteFrame._dispatch(frame, Action.RATE_UP)
    assert volume_steps == [5]
    assert spoken == ["To polecenie nie jest dostępne w sesji Sonos"]


def _queued_sonos_frame(submissions: list, spoken: list[str]) -> SimpleNamespace:
    calls: list[tuple] = []
    frame = SimpleNamespace(
        client=SimpleNamespace(
            sonos_transport=lambda *args, **kwargs: (
                calls.append((args, kwargs)) or {}
            )
        ),
        navigator=SimpleNamespace(active=SessionId.SONOS, view=View.PLAYER),
        announcer=SimpleNamespace(say=spoken.append),
        messages=SimpleNamespace(
            volume_text=lambda value: f"{value}%",
            speaks_routine=True,
        ),
        runner=SimpleNamespace(
            submit=lambda *args: submissions.append(args)
        ),
        _sonos_active_household_id="house-1",
        _sonos_active_group_id="group-1",
        _sonos_request_pending=False,
        _sonos_retry_pending=False,
        _sonos_pending_activation=None,
        _sonos_status_poll_pending=False,
        _sonos_command_queue=[],
        _sonos_transport_calls=calls,
    )
    frame._start_next_sonos_command = MethodType(
        LiteFrame._start_next_sonos_command, frame
    )
    frame._finish_sonos_request = MethodType(
        LiteFrame._finish_sonos_request, frame
    )
    return frame


def test_sonos_volume_response_speaks_only_confirmed_volume() -> None:
    submissions: list = []
    spoken: list[str] = []
    applied: list[tuple[dict, bool]] = []
    frame = _queued_sonos_frame(submissions, spoken)
    frame._apply_sonos_snapshot = (
        lambda state, *, announce=False: applied.append((state, announce))
    )

    LiteFrame._sonos_transport(frame, "volumeUp5")
    submissions[0][2]({
        "householdId": "house-1",
        "groupId": "group-1",
        "playbackState": "paused",
        "positionText": "3:57",
        "volume": 8,
        "message": "Brak informacji, 3:57, zatrzymane, głośność 8%",
    })

    assert spoken == ["8%"]
    assert applied[0][1] is False


def test_sonos_volume_response_honours_disabled_routine_messages() -> None:
    submissions: list = []
    spoken: list[str] = []
    applied: list[tuple[dict, bool]] = []
    frame = _queued_sonos_frame(submissions, spoken)
    frame.messages = SimpleNamespace(
        volume_text=lambda _value: None,
        speaks_routine=True,
    )
    frame._apply_sonos_snapshot = (
        lambda state, *, announce=False: applied.append((state, announce))
    )

    LiteFrame._sonos_transport(frame, "setVolume", volume=12)
    submissions[0][2]({
        "householdId": "house-1",
        "groupId": "group-1",
        "volume": 11,
        "message": "Głośność 11%",
    })

    assert spoken == []
    assert applied[0][0]["volume"] == 11
    assert applied[0][1] is False


def test_sonos_command_waits_while_snapshot_is_pending() -> None:
    submissions: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame._sonos_request_pending = True

    LiteFrame._sonos_transport(frame, "toggle")

    assert submissions == []
    assert frame._sonos_command_queue == [
        ("house-1", "group-1", "toggle", None, None, 0)
    ]


def test_sonos_timer_does_not_overlap_a_read_or_waiting_command() -> None:
    submissions: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame.client.sonos_snapshot = lambda *_args: {}
    frame._sonos_request_pending = True

    LiteFrame._refresh_sonos_status(frame)

    assert submissions == []
    assert frame._sonos_status_poll_pending is False

    frame._sonos_request_pending = False
    frame._sonos_command_queue.append(
        ("house-1", "group-1", "toggle", None, None, 0)
    )
    LiteFrame._refresh_sonos_status(frame)
    assert submissions == []


def test_sonos_timer_does_not_poll_while_library_list_is_open() -> None:
    submissions: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame.navigator.view = View.LIST
    frame.client.sonos_snapshot = lambda *_args: {}

    LiteFrame._refresh_sonos_status(frame)

    assert submissions == []
    assert frame._sonos_status_poll_pending is False


def test_sonos_timer_limits_upnp_snapshots_to_one_per_three_seconds() -> None:
    submissions: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame.client.sonos_snapshot = lambda *_args: {}
    frame._sonos_status_poll_due = float("inf")

    LiteFrame._refresh_sonos_status(frame)

    assert submissions == []


def test_sonos_play_restarts_last_amc_material_after_stop() -> None:
    played: list[PlaySonosMaterial] = []
    transported: list[str] = []
    intent = PlaySonosMaterial(
        "item-1", "Radio domowe", "house-1", "ownStream", "stream-1"
    )
    frame = SimpleNamespace(
        client=object(),
        navigator=SimpleNamespace(active=SessionId.SONOS),
        announcer=SimpleNamespace(say=lambda _text: None),
        _sonos_snapshot={"playbackState": "idle"},
        _sonos_active_household_id="house-1",
        _sonos_active_group_id="group-1",
        _sonos_last_material=("house-1", "group-1", intent),
        _play_sonos_material=played.append,
        _sonos_transport=transported.append,
    )

    LiteFrame._play_pause(frame)

    assert played == [intent]
    assert transported == []


def test_sonos_play_asks_device_to_resume_unknown_stopped_material() -> None:
    transported: list[str] = []
    frame = SimpleNamespace(
        client=object(),
        navigator=SimpleNamespace(active=SessionId.SONOS),
        announcer=SimpleNamespace(say=lambda _text: None),
        _sonos_snapshot={"playbackState": "idle"},
        _sonos_active_household_id="house-1",
        _sonos_active_group_id="group-1",
        _sonos_last_material=None,
        _play_sonos_material=lambda _intent: None,
        _sonos_transport=transported.append,
    )

    LiteFrame._play_pause(frame)

    assert transported == ["toggle"]


def test_finishing_sonos_read_starts_one_waiting_command() -> None:
    submissions: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame._sonos_request_pending = True
    frame._sonos_command_queue.append(
        ("house-1", "group-1", "toggle", None, None, 0)
    )

    LiteFrame._finish_sonos_request(frame)

    assert len(submissions) == 1
    stream, work, _done, _failed = submissions[0]
    assert stream == "sonos-transport"
    assert frame._sonos_request_pending is True
    assert frame._sonos_command_queue == []
    work()
    assert frame._sonos_transport_calls == [
        (("house-1", "group-1", "toggle"), {
            "volume": None,
            "seconds": None,
        })
    ]


def test_sonos_command_is_not_discarded_while_library_list_is_visible() -> None:
    submissions: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame.navigator.view = View.LIST

    LiteFrame._sonos_transport(frame, "toggle")

    assert len(submissions) == 1
    assert submissions[0][0] == "sonos-transport"


def test_selecting_sonos_group_opens_library_after_snapshot() -> None:
    submissions: list = []
    opened: list[OpenSonosView] = []
    frame = _queued_sonos_frame(submissions, [])
    frame.navigator.view = View.LIST
    frame.client.sonos_snapshot = lambda *_args: {}
    frame.now_playing = SimpleNamespace(SetLabel=lambda _label: None)
    frame._set_transport_label = lambda **_kwargs: None
    frame._open_sonos_view = opened.append
    frame._activate_sonos_group = MethodType(LiteFrame._activate_sonos_group, frame)
    intent = ActivateSonosGroup(
        "sonos:group-1", "Salon", "house-1", "group-1"
    )

    LiteFrame._activate_sonos_group(frame, intent)
    submissions[0][2]({
        "householdId": "house-1",
        "groupId": "group-1",
        "groupTitle": "Salon",
        "playbackState": "paused",
    })

    assert opened == [OpenSonosView(
        LibraryView.SONOS_LIBRARY,
        household_id="house-1",
        preferred_id=None,
    )]


def test_busy_sonos_command_is_retried_without_speaking_error() -> None:
    submissions: list = []
    spoken: list[str] = []
    scheduled: list = []
    frame = _queued_sonos_frame(submissions, spoken)
    original_call_later = getattr(gui_module.wx, "CallLater", None)
    gui_module.wx.CallLater = (
        lambda _milliseconds, callback: scheduled.append(callback)
    )
    try:
        frame._sonos_command_queue.append(
            ("house-1", "group-1", "toggle", None, None, 0)
        )

        LiteFrame._start_next_sonos_command(frame)
        submissions[0][3](HostError("Trwa odczyt", code="operation_busy"))

        assert spoken == []
        assert frame._sonos_request_pending is False
        assert frame._sonos_retry_pending is True
        assert frame._sonos_command_queue == [
            ("house-1", "group-1", "toggle", None, None, 1)
        ]
        assert len(scheduled) == 1

        scheduled[0]()
        assert len(submissions) == 2
        assert frame._sonos_retry_pending is False
        assert frame._sonos_request_pending is True
    finally:
        if original_call_later is None:
            del gui_module.wx.CallLater
        else:
            gui_module.wx.CallLater = original_call_later


def test_busy_initial_sonos_read_is_retried_without_returning_to_list() -> None:
    submissions: list = []
    spoken: list[str] = []
    scheduled: list = []
    frame = _queued_sonos_frame(submissions, spoken)
    frame.client.sonos_snapshot = lambda *_args: {}
    frame.now_playing = SimpleNamespace(SetLabel=lambda _label: None)
    frame._set_transport_label = lambda **_kwargs: None
    frame._activate_sonos_group = MethodType(LiteFrame._activate_sonos_group, frame)
    original_call_later = getattr(gui_module.wx, "CallLater", None)
    gui_module.wx.CallLater = (
        lambda _milliseconds, callback: scheduled.append(callback)
    )
    intent = ActivateSonosGroup(
        "sonos:group-1", "Salon", "house-1", "group-1"
    )

    try:
        LiteFrame._activate_sonos_group(frame, intent)
        submissions[0][3](HostError("Trwa odczyt", code="operation_busy"))

        assert spoken == []
        assert frame.navigator.view is View.PLAYER
        assert frame._sonos_retry_pending is True
        scheduled[0]()
        assert len(submissions) == 2
        assert frame._sonos_request_pending is True
    finally:
        if original_call_later is None:
            del gui_module.wx.CallLater
        else:
            gui_module.wx.CallLater = original_call_later


def test_new_group_selection_wins_over_busy_command_retry() -> None:
    submissions: list = []
    scheduled: list = []
    frame = _queued_sonos_frame(submissions, [])
    frame.client.sonos_snapshot = lambda *_args: {}
    frame.now_playing = SimpleNamespace(SetLabel=lambda _label: None)
    frame._set_transport_label = lambda **_kwargs: None
    frame._activate_sonos_group = MethodType(LiteFrame._activate_sonos_group, frame)
    original_call_later = getattr(gui_module.wx, "CallLater", None)
    gui_module.wx.CallLater = (
        lambda _milliseconds, callback: scheduled.append(callback)
    )
    try:
        frame._sonos_command_queue.append(
            ("house-1", "group-1", "toggle", None, None, 0)
        )
        LiteFrame._start_next_sonos_command(frame)
        submissions[0][3](HostError("Trwa odczyt", code="operation_busy"))

        new_group = ActivateSonosGroup(
            "sonos:group-2", "Kuchnia", "house-1", "group-2"
        )
        LiteFrame._activate_sonos_group(frame, new_group)
        assert frame._sonos_pending_activation is new_group

        scheduled[0]()
        assert len(submissions) == 2
        assert submissions[1][0] == "sonos-snapshot"
        assert frame._sonos_active_group_id == "group-2"
        assert frame._sonos_pending_activation is None
    finally:
        if original_call_later is None:
            del gui_module.wx.CallLater
        else:
            gui_module.wx.CallLater = original_call_later
