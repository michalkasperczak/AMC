from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.list_model import Row
from amc_wx_lite.menu_model import build_menus
from amc_wx_lite.navigation import (
    ActivateWiiMDevice,
    LibraryView,
    Navigator,
    OpenWiiMDevicePresets,
    OpenWiiMNetworkStreams,
    PlayWiiMNetworkStream,
    SessionId,
    View,
)
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    WIIM_SUPPORTED_ACTIONS,
    resolve,
)
from amc_wx_lite.wiim_source import (
    device_rows,
    network_stream_edit_info,
    network_stream_rows,
    now_playing_label,
    option_choices,
    preset_choices,
    snapshot,
)


def test_ctrl_5_and_menu_open_the_wiim_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("5", ctrl=True),
            player_view=player,
            radio_session=False,
        ) is Action.SESSION_WIIM
    view = next(
        menu
        for menu in build_menus([session.value for session in SessionId])
        if menu.title == "&Widok"
    )
    item = next(entry for entry in view.items if entry.action is Action.SESSION_WIIM)
    assert item.label == "Sesja: &WiiM"
    assert item.shortcut == "Ctrl+5"


def test_wiim_device_activation_separates_user_label_from_internal_id() -> None:
    nav = Navigator()
    rows = device_rows({
        "items": [{
            "itemId": "wiim:device-1",
            "deviceId": "device-1",
            "title": "Salon",
            "detail": "WiiM Pro",
            "selected": True,
            "address": "192.168.1.40",
        }]
    })
    nav.apply_wiim_devices(list(rows))
    nav.switch_session(SessionId.WIIM)

    intents = nav.activate_selected()

    assert len(intents) == 2
    intent = intents[0]
    assert isinstance(intent, ActivateWiiMDevice)
    assert intent.title == "Salon"
    assert intent.device_id == "device-1"
    assert "192.168.1.40" not in repr(nav.sessions[SessionId.WIIM].model.rows)
    assert nav.view is View.LIST
    assert nav.session.library_view is LibraryView.WIIM_SOURCE_LIBRARY
    assert [row.title for row in nav.session.model.rows] == [
        "Presety urządzenia",
        "Strumienie zapisane w AMC",
    ]
    assert intents[1].text == "Biblioteka źródeł WiiM, 2 pozycje"


def test_wiim_payload_is_allowlisted_for_accessible_text() -> None:
    state = snapshot({
        "deviceId": "device-1",
        "deviceTitle": "Salon",
        "title": "Audycja",
        "artist": "Radio Poznań",
        "playbackState": "odtwarzanie",
        "volume": 35,
        "muted": False,
        "positionSeconds": 61,
        "durationSeconds": 600,
        "bitrateKbps": 192,
        "message": "Audycja, odtwarzanie, głośność 35%",
        "address": "192.168.1.40",
        "runtimeObject": "WiiMDeviceSettings { Address = 192.168.1.40 }",
    })

    assert now_playing_label(state) == "Audycja, Radio Poznań, 192 kb/s"
    assert "address" not in state
    assert "runtimeObject" not in state
    assert "192.168.1.40" not in repr(state)


def test_wiim_command_gate_blocks_local_audio_features() -> None:
    assert {
        Action.PLAY_PAUSE,
        Action.QUEUE_PREVIOUS,
        Action.QUEUE_NEXT,
        Action.VOLUME_UP_5,
        Action.VOLUME_DOWN_1,
        Action.TIME_ELAPSED,
        Action.VIEW_PRESETS,
        Action.WIIM_SELECT_INPUT,
        Action.WIIM_SELECT_OUTPUT,
        Action.WIIM_SELECT_EQUALIZER,
        Action.WIIM_SELECT_REPEAT,
        Action.WIIM_TOGGLE_SHUFFLE,
        Action.WIIM_SET_SLEEP_TIMER,
        Action.WIIM_PREVIOUS_SOURCE,
        Action.WIIM_NEXT_SOURCE,
        Action.ASSIGN_PRESET,
        Action.PRESET_1,
        Action.PRESET_12,
        Action.COPY_NAME,
        Action.COPY_ADDRESS,
        Action.FOCUS_FILTER,
        Action.VIEW_ACTIVE_RECORDINGS,
        Action.MANAGE_RADIO_SCHEDULES,
    } <= WIIM_SUPPORTED_ACTIONS
    assert {
        Action.ADD_BOOKMARK,
        Action.RATE_UP,
        Action.SEEK_FORWARD_10,
        Action.RECORD_TOGGLE,
        Action.ADD_TO_QUEUE,
        Action.PREVIOUS_AUDIO_OUTPUT,
        Action.NEXT_AUDIO_OUTPUT,
    }.isdisjoint(WIIM_SUPPORTED_ACTIONS)
    assert Action.SELECT_AUDIO_OUTPUT in WIIM_SUPPORTED_ACTIONS


def test_wiim_host_client_sends_only_device_id_command_and_volume() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.wiim_devices()
    client.wiim_snapshot("device-1")
    client.wiim_transport("device-1", "setVolume", volume=37)
    client.wiim_presets("device-1")
    client.wiim_activate_preset("device-1", 5)
    client.wiim_options("device-1", "equalizer")
    client.wiim_set_option("device-1", "repeat", "-1")
    client.wiim_toggle_shuffle("device-1")
    client.wiim_navigate_source("device-1", -1)
    client.wiim_navigate_source("device-1", 1)
    client.wiim_network_streams()
    client.wiim_play_network_stream("device-1", "stream-7")
    client.wiim_network_stream_edit_info("stream-7")
    client.wiim_add_network_stream("Radio Poznań", "https://radio.example/live")
    client.wiim_edit_network_stream(
        "stream-7", "Radio Poznań 2", "https://radio.example/live2"
    )
    client.wiim_remove_network_streams(["stream-7", "stream-8"])
    client.wiim_import_network_streams("C:/listy/wiim.m3u")
    client.wiim_export_network_streams("C:/listy/eksport.m3u")
    client.wiim_sort_network_streams("Alphabetical")
    client.wiim_move_network_streams(["stream-7", "stream-8"], -1)
    client.wiim_place_network_streams(["stream-7"], "stream-9")

    assert calls == [
        ("wiim.devices", None, 15.0),
        ("wiim.snapshot", {"deviceId": "device-1"}, 30.0),
        (
            "wiim.transport",
            {"deviceId": "device-1", "command": "setVolume", "volume": 37},
            30.0,
        ),
        ("wiim.presets", {"deviceId": "device-1"}, 30.0),
        (
            "wiim.presetActivate",
            {"deviceId": "device-1", "presetNumber": 5},
            30.0,
        ),
        (
            "wiim.options",
            {"deviceId": "device-1", "option": "equalizer"},
            30.0,
        ),
        (
            "wiim.optionSet",
            {"deviceId": "device-1", "option": "repeat", "value": "-1"},
            30.0,
        ),
        (
            "wiim.shuffleToggle",
            {"deviceId": "device-1"},
            30.0,
        ),
        (
            "wiim.sourceNavigate",
            {"deviceId": "device-1", "direction": -1},
            30.0,
        ),
        (
            "wiim.sourceNavigate",
            {"deviceId": "device-1", "direction": 1},
            30.0,
        ),
        ("wiim.networkStreams", None, 15.0),
        (
            "wiim.networkStreamPlay",
            {"deviceId": "device-1", "streamId": "stream-7"},
            30.0,
        ),
        (
            "wiim.networkStreamEditInfo",
            {"streamId": "stream-7"},
            15.0,
        ),
        (
            "wiim.networkStreamAdd",
            {"name": "Radio Poznań", "url": "https://radio.example/live"},
            30.0,
        ),
        (
            "wiim.networkStreamEdit",
            {
                "streamId": "stream-7",
                "name": "Radio Poznań 2",
                "url": "https://radio.example/live2",
            },
            30.0,
        ),
        (
            "wiim.networkStreamRemove",
            {"itemIds": ["stream-7", "stream-8"]},
            30.0,
        ),
        (
            "wiim.networkStreamImport",
            {"path": "C:/listy/wiim.m3u"},
            60.0,
        ),
        (
            "wiim.networkStreamExport",
            {"path": "C:/listy/eksport.m3u"},
            60.0,
        ),
        (
            "wiim.networkStreamSort",
            {"mode": "Alphabetical"},
            30.0,
        ),
        (
            "wiim.networkStreamMove",
            {"itemIds": ["stream-7", "stream-8"], "direction": -1},
            30.0,
        ),
        (
            "wiim.networkStreamPlace",
            {"itemIds": ["stream-7"], "targetId": "stream-9"},
            30.0,
        ),
    ]
    assert "address" not in repr(calls).casefold()


def test_wiim_dispatch_reaches_remote_volume_and_blocks_local_rate() -> None:
    spoken: list[str] = []
    volume_steps: list[int] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.WIIM),
        announcer=SimpleNamespace(say=spoken.append),
        messages=SimpleNamespace(custom_seek_seconds=300),
        _adjust_volume=volume_steps.append,
    )

    LiteFrame._dispatch(frame, Action.VOLUME_UP_5)
    assert volume_steps == [5]
    assert spoken == []

    LiteFrame._dispatch(frame, Action.RATE_UP)
    assert volume_steps == [5]
    assert spoken == ["To polecenie nie jest dostępne w sesji WiiM"]


def test_wiim_option_actions_route_only_to_remote_option_handlers() -> None:
    calls: list[object] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.WIIM),
        _show_wiim_option=calls.append,
        _toggle_wiim_shuffle=lambda: calls.append("shuffle"),
    )

    LiteFrame._dispatch(frame, Action.WIIM_SELECT_INPUT)
    LiteFrame._dispatch(frame, Action.WIIM_SELECT_OUTPUT)
    LiteFrame._dispatch(frame, Action.WIIM_SELECT_EQUALIZER)
    LiteFrame._dispatch(frame, Action.WIIM_SELECT_REPEAT)
    LiteFrame._dispatch(frame, Action.WIIM_SET_SLEEP_TIMER)
    LiteFrame._dispatch(frame, Action.WIIM_TOGGLE_SHUFFLE)

    assert calls == [
        Action.WIIM_SELECT_INPUT,
        Action.WIIM_SELECT_OUTPUT,
        Action.WIIM_SELECT_EQUALIZER,
        Action.WIIM_SELECT_REPEAT,
        Action.WIIM_SET_SLEEP_TIMER,
        "shuffle",
    ]


def test_wiim_source_navigation_routes_to_one_remote_step() -> None:
    calls: list[int] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.WIIM),
        _navigate_wiim_source=calls.append,
    )

    LiteFrame._dispatch(frame, Action.WIIM_PREVIOUS_SOURCE)
    LiteFrame._dispatch(frame, Action.WIIM_NEXT_SOURCE)

    assert calls == [-1, 1]


def test_wiim_preset_list_has_only_twelve_intentional_labels() -> None:
    items = [
        {
            "itemId": f"wiim-preset:{number}",
            "presetNumber": number,
            "name": "Radio Test" if number == 2 else "",
            "source": "radio internetowe" if number == 2 else "",
            "label": (
                "Preset 2, Radio Test, radio internetowe"
                if number == 2
                else f"Preset {number}, pusty"
            ),
            "occupied": number == 2,
            "current": number == 2,
            "activationError": "",
            "uri": "https://secret.example/stream",
            "runtimeObject": "WiiMPresetInformation { Number = 2 }",
        }
        for number in range(1, 13)
    ]

    title, choices = preset_choices({
        "deviceId": "device-1",
        "deviceTitle": "Salon",
        "items": items,
    })

    assert title == "Salon"
    assert len(choices) == 12
    assert choices[1].label == "Preset 2, Radio Test, radio internetowe"
    assert choices[1].title == "Radio Test"
    assert choices[1].source == "radio internetowe"
    assert choices[1].occupied is True
    assert choices[1].current is True
    assert "secret" not in repr(choices)
    assert "WiiMPresetInformation" not in repr(choices)


def test_wiim_preset_connect_reason_stays_user_facing() -> None:
    items = [
        {
            "presetNumber": number,
            "label": "Preset 5, TIDAL, TIDAL Connect"
            if number == 5 else f"Preset {number}, pusty",
            "occupied": number == 5,
            "current": False,
            "activationError": (
                "Preset 5 to TIDAL Connect. Urządzenie samo tego nie uruchomi, "
                "bo odbiera dźwięk z aplikacji."
                if number == 5 else ""
            ),
        }
        for number in range(1, 13)
    ]

    _title, choices = preset_choices({"deviceTitle": "Salon", "items": items})

    assert choices[4].activation_error.startswith("Preset 5 to TIDAL Connect")


def test_wiim_option_list_separates_values_from_accessible_labels() -> None:
    device, title, description, choices, selected = option_choices({
        "deviceId": "device-1",
        "deviceTitle": "Salon",
        "option": "output",
        "title": "Wyjście urządzenia WiiM",
        "description": "Wybierz fizyczne wyjście audio.",
        "selectedValue": "1",
        "items": [
            {"value": "2", "label": "Wyjście analogowe AUX"},
            {"value": "1", "label": "Wyjście optyczne SPDIF"},
            {"value": "3", "label": "Wyjście koncentryczne"},
        ],
        "address": "192.168.1.40",
        "runtimeObject": "WiiMPlaybackInformation { LoopMode = 2 }",
    })

    assert device == "Salon"
    assert title == "Wyjście urządzenia WiiM"
    assert description == "Wybierz fizyczne wyjście audio."
    assert [choice.label for choice in choices] == [
        "Wyjście analogowe AUX",
        "Wyjście optyczne SPDIF",
        "Wyjście koncentryczne",
    ]
    assert choices[selected].value == "1"
    assert "192.168.1.40" not in repr(choices)
    assert "WiiMPlaybackInformation" not in repr(choices)


def test_wiim_player_letters_override_local_clip_shortcuts_only_in_player() -> None:
    expected = {
        "I": Action.WIIM_SELECT_INPUT,
        "O": Action.WIIM_SELECT_OUTPUT,
        "E": Action.WIIM_SELECT_EQUALIZER,
        "R": Action.WIIM_SELECT_REPEAT,
        "S": Action.WIIM_TOGGLE_SHUFFLE,
        "T": Action.WIIM_SET_SLEEP_TIMER,
    }
    for key, action in expected.items():
        assert resolve(
            Chord(key),
            player_view=True,
            radio_session=False,
            wiim_session=True,
        ) is action
        assert resolve(
            Chord(key),
            player_view=False,
            radio_session=False,
            wiim_session=True,
        ) is None

    assert resolve(
        Chord("I"),
        player_view=True,
        radio_session=False,
    ) is Action.CLIP_MARK_START


def test_wiim_player_alt_page_keys_navigate_sources_only_in_player() -> None:
    assert resolve(
        Chord("Prior", alt=True),
        player_view=True,
        radio_session=False,
        wiim_session=True,
    ) is Action.WIIM_PREVIOUS_SOURCE
    assert resolve(
        Chord("Next", alt=True),
        player_view=True,
        radio_session=False,
        wiim_session=True,
    ) is Action.WIIM_NEXT_SOURCE
    assert resolve(
        Chord("Prior", alt=True),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is None


def test_ctrl_alt_p_routes_to_native_wiim_presets() -> None:
    calls: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.WIIM),
        _show_wiim_presets=lambda: calls.append("wiim"),
    )

    LiteFrame._show_presets(frame)

    assert calls == ["wiim"]


def test_wiim_assignment_and_digit_activation_use_device_specific_paths() -> None:
    calls: list[object] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.WIIM),
        _show_wiim_presets=lambda **kwargs: calls.append(("assign", kwargs)),
        _activate_wiim_shortcut_preset=lambda slot: calls.append(("play", slot)),
    )

    LiteFrame._assign_preset(frame)
    LiteFrame._activate_preset(frame, 12)

    assert calls == [
        ("assign", {"select_for_shortcut": True}),
        ("play", 12),
    ]


def test_ctrl_l_opens_wiim_source_library_and_ctrl_o_opens_playlist() -> None:
    assert resolve(
        Chord("L", ctrl=True),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is Action.VIEW_LIBRARY
    chord = Chord("O", ctrl=True)
    assert resolve(
        chord,
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is Action.IMPORT_WIIM_NETWORK_STREAMS
    assert resolve(
        chord,
        player_view=True,
        radio_session=False,
        wiim_session=True,
    ) is Action.IMPORT_WIIM_NETWORK_STREAMS
    assert resolve(
        chord,
        player_view=False,
        radio_session=False,
    ) is Action.OPEN_FILE_DIALOG


def test_wiim_stream_edit_shortcuts_are_active_only_inside_stream_list() -> None:
    assert resolve(
        Chord("N", ctrl=True),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is Action.ADD_WIIM_NETWORK_STREAM

    assert resolve(
        Chord("F2"),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is None
    assert resolve(
        Chord("Delete"),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is None
    assert resolve(
        Chord("F2"),
        player_view=False,
        radio_session=False,
        wiim_session=True,
        wiim_streams=True,
    ) is Action.EDIT_WIIM_NETWORK_STREAM
    assert resolve(
        Chord("Delete"),
        player_view=False,
        radio_session=False,
        wiim_session=True,
        wiim_streams=True,
    ) is Action.REMOVE_WIIM_NETWORK_STREAMS

    assert resolve(
        Chord("O", ctrl=True, shift=True),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is Action.IMPORT_WIIM_NETWORK_STREAMS
    assert resolve(
        Chord("E", ctrl=True),
        player_view=False,
        radio_session=False,
        wiim_session=True,
    ) is Action.EXPORT_WIIM_NETWORK_STREAMS
    for key, action in (
        ("1", Action.SORT_WIIM_NETWORK_STREAMS_ADDED),
        ("2", Action.SORT_WIIM_NETWORK_STREAMS_ALPHABETICAL),
        ("3", Action.SORT_WIIM_NETWORK_STREAMS_CUSTOM),
    ):
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            wiim_session=True,
        ) is None
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            wiim_session=True,
            wiim_streams=True,
        ) is action


def test_wiim_cut_and_paste_place_streams_before_named_target() -> None:
    spoken: list[str] = []
    calls: list[tuple[list[str], str]] = []
    reloads: list[tuple[str | None, str | None]] = []
    selected = [
        Row(
            "wiim-stream:one",
            "Pierwszy",
            "wiimNetworkStream",
            service_id="one",
            service_kind="wiimNetworkStream",
        ),
        Row(
            "wiim-stream:three",
            "Trzeci",
            "wiimNetworkStream",
            service_id="three",
            service_kind="wiimNetworkStream",
        ),
    ]
    client = SimpleNamespace(
        wiim_place_network_streams=lambda ids, target: (
            calls.append((list(ids), target))
            or {"preferredStreamId": "one"}
        )
    )
    frame = SimpleNamespace(
        _wiim_network_stream_sort_mode="Custom",
        _wiim_pending_move_ids=[],
        _selected_wiim_stream_rows=lambda: selected,
        _profile_edit_client=lambda: client,
        announcer=SimpleNamespace(say=spoken.append),
        runner=SimpleNamespace(
            submit=lambda _name, work, done, _failed: done(work())
        ),
        _show_wiim_network_streams=lambda **kwargs: reloads.append((
            kwargs.get("preferred_stream_id"),
            kwargs.get("announcement"),
        )),
    )

    LiteFrame._cut_wiim_network_streams(frame)
    assert frame._wiim_pending_move_ids == ["one", "three"]
    assert spoken == [
        "Zaznaczono strumienie do przeniesienia: 2. "
        "Wybierz miejsce i naciśnij Control V"
    ]

    target = Row(
        "wiim-stream:two",
        "Drugi",
        "wiimNetworkStream",
        service_id="two",
        service_kind="wiimNetworkStream",
    )
    frame._selected_wiim_stream_rows = lambda: [target]
    LiteFrame._paste_wiim_network_streams(frame)

    assert calls == [(["one", "three"], "two")]
    assert frame._wiim_pending_move_ids == []
    assert reloads == [("one", "Przeniesiono strumienie: 2, przed Drugi")]

    for chord, action in (
        (Chord("Up", alt=True), Action.MOVE_WIIM_NETWORK_STREAM_UP),
        (Chord("Down", alt=True), Action.MOVE_WIIM_NETWORK_STREAM_DOWN),
        (Chord("X", ctrl=True), Action.CUT_WIIM_NETWORK_STREAMS),
        (Chord("V", ctrl=True), Action.PASTE_WIIM_NETWORK_STREAMS),
    ):
        assert resolve(
            chord,
            player_view=False,
            radio_session=False,
            wiim_session=True,
        ) is None
        assert resolve(
            chord,
            player_view=False,
            radio_session=False,
            wiim_session=True,
            wiim_streams=True,
        ) is action


def test_wiim_timer_reads_playback_while_stream_list_is_open() -> None:
    submissions: list = []
    frame = SimpleNamespace(
        client=SimpleNamespace(wiim_snapshot=lambda _device_id: {}),
        navigator=SimpleNamespace(active=SessionId.WIIM, view=View.LIST),
        runner=SimpleNamespace(submit=lambda *args: submissions.append(args)),
        _wiim_active_device_id="device-one",
        _wiim_status_poll_pending=False,
    )

    LiteFrame._refresh_wiim_status(frame)

    assert len(submissions) == 1
    assert submissions[0][0] == "wiim-status"


def test_wiim_source_library_has_two_intentional_labels_and_actions() -> None:
    nav = Navigator()
    devices = list(device_rows({
        "items": [{
            "itemId": "wiim:device-1",
            "deviceId": "device-1",
            "title": "Salon",
            "detail": "WiiM Pro",
        }]
    }))
    nav.apply_wiim_devices(devices)
    nav.switch_session(SessionId.WIIM)

    announcements = nav.open_wiim_source_library(
        device_id="device-1",
        return_id="wiim:device-1",
    )

    assert nav.session.library_view is LibraryView.WIIM_SOURCE_LIBRARY
    assert [row.title for row in nav.session.model.rows] == [
        "Presety urządzenia",
        "Strumienie zapisane w AMC",
    ]
    assert [row.kind for row in nav.session.model.rows] == [
        "wiimSource",
        "wiimSource",
    ]
    assert [row.kind_label for row in nav.session.model.rows] == ["", ""]
    assert announcements[0].text == "Biblioteka źródeł WiiM, 2 pozycje"
    assert isinstance(nav.activate_selected()[0], OpenWiiMDevicePresets)

    nav.session.model.select_id("wiim-source:amc-streams")
    assert isinstance(nav.activate_selected()[0], OpenWiiMNetworkStreams)


def test_wiim_snapshot_remembers_device_not_source_library_row() -> None:
    """Restart ma wrocic do Pro Plus, nie do hostowego pierwszego WiiM Sound."""
    devices = list(device_rows({
        "items": [
            {
                "itemId": "wiim:sound",
                "deviceId": "sound",
                "title": "WiiM Sound",
                "detail": "WiiM Sound",
                "selected": True,
            },
            {
                "itemId": "wiim:pro-plus",
                "deviceId": "pro-plus",
                "title": "WiiM Pro Plus",
                "detail": "WiiM Pro Plus",
            },
        ]
    }))
    nav = Navigator()
    nav.apply_wiim_devices(devices)
    nav.switch_session(SessionId.WIIM)
    nav.session.model.select_id("wiim:pro-plus")
    nav.activate_selected()

    assert nav.session.library_view is LibraryView.WIIM_SOURCE_LIBRARY
    assert nav.session.model.selected_id == "wiim-source:device-presets"
    saved = nav.snapshot()
    assert saved["sessions"]["wiim"]["selectedId"] == "wiim:pro-plus"

    restored = Navigator()
    restored.restore(saved)
    restored.apply_wiim_devices(devices)

    assert restored.sessions[SessionId.WIIM].model.selected_id == "wiim:pro-plus"


def test_wiim_streams_opened_from_source_library_return_to_that_library() -> None:
    nav = Navigator()
    devices = list(device_rows({
        "items": [{
            "itemId": "wiim:device-1",
            "deviceId": "device-1",
            "title": "Salon",
            "detail": "WiiM Pro",
        }]
    }))
    nav.apply_wiim_devices(devices)
    nav.switch_session(SessionId.WIIM)
    nav.open_wiim_source_library(
        device_id="device-1",
        return_id="wiim:device-1",
    )
    nav.apply_wiim_network_streams(
        [],
        device_id="device-1",
        return_id="wiim:device-1",
        return_to_sources=True,
    )

    nav.go_to_parent()

    assert nav.session.library_view is LibraryView.WIIM_SOURCE_LIBRARY
    assert [row.title for row in nav.session.model.rows] == [
        "Presety urządzenia",
        "Strumienie zapisane w AMC",
    ]


def test_wiim_stream_edit_info_is_explicit_and_user_facing() -> None:
    assert network_stream_edit_info({
        "streamId": "stream-7",
        "name": "Radio Poznań",
        "url": "https://radio.example/live",
        "runtimeObject": "NetworkStreamSettings { Id = stream-7 }",
    }) == (
        "stream-7",
        "Radio Poznań",
        "https://radio.example/live",
    )


def test_wiim_network_stream_view_uses_only_user_labels_and_returns_to_device() -> None:
    nav = Navigator()
    devices = list(device_rows({
        "items": [{
            "itemId": "wiim:device-1",
            "deviceId": "device-1",
            "title": "Salon",
            "detail": "WiiM Pro",
        }]
    }))
    nav.apply_wiim_devices(devices)
    nav.switch_session(SessionId.WIIM)
    streams = list(network_stream_rows({
        "items": [{
            "itemId": "wiim-stream:stream-7",
            "streamId": "stream-7",
            "title": "Radio Poznań",
            "detail": "Strumień sieciowy",
            "streamUrl": "https://secret.example/live.m3u8",
            "runtimeObject": "NetworkStream { Id = stream-7 }",
        }]
    }, device_id="device-1"))

    nav.apply_wiim_network_streams(
        streams,
        device_id="device-1",
        return_id="wiim:device-1",
    )
    assert nav.session.library_view is LibraryView.WIIM_NETWORK_STREAMS
    assert nav.session.model.selected_row.title == "Radio Poznań"
    assert nav.session.model.selected_row.address == (
        "https://secret.example/live.m3u8"
    )
    assert "secret.example" not in nav.session.model.selected_row.title
    assert "secret.example" not in nav.session.model.selected_row.detail
    assert "Id = stream-7" not in repr(nav.session.model.rows)
    assert nav.session.model.text_for(0, 0) == "Radio Poznań"
    assert nav.session.model.text_for(0, 1) == ""
    assert nav.session.model.text_for(0, 2) == ""

    intents = nav.activate_selected()
    assert len(intents) == 1
    assert isinstance(intents[0], PlayWiiMNetworkStream)
    assert intents[0].device_id == "device-1"
    assert intents[0].stream_id == "stream-7"

    nav.session.view = View.LIST
    nav.go_to_parent()
    assert nav.session.library_view is None
    assert nav.session.model.selected_id == "wiim:device-1"
