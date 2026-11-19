"""Bounded review regressions for confirmed playback presentation."""

from __future__ import annotations

import json
import os
from pathlib import Path
from types import SimpleNamespace

import pytest

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame, SearchDialog
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Navigator, SessionId
from amc_wx_lite.search_catalog import SearchEntry
from amc_wx_lite.sonos_source import material_rows as sonos_material_rows
from amc_wx_lite.sonos_source import snapshot as sonos_snapshot
from amc_wx_lite.state_store import LiteState
from amc_wx_lite.wiim_source import network_stream_rows
from amc_wx_lite.wiim_source import snapshot as wiim_snapshot


class _Results:
    def __init__(self) -> None:
        self.rows: list[list[str]] = []
        self.focused: list[int] = []

    def Freeze(self) -> None:  # noqa: N802 - wx API
        pass

    def Thaw(self) -> None:  # noqa: N802 - wx API
        pass

    def DeleteAllItems(self) -> None:  # noqa: N802 - wx API
        self.rows = []

    def InsertItem(self, index: int, text: str) -> None:  # noqa: N802 - wx API
        self.rows.insert(index, [text, "", ""])

    def SetItem(self, index: int, column: int, text: str) -> None:  # noqa: N802
        self.rows[index][column] = text

    def SetItemState(self, index: int, *_args: object) -> None:  # noqa: N802
        self.focused.append(index)

    def EnsureVisible(self, _index: int) -> None:  # noqa: N802 - wx API
        pass

    def SetFocus(self) -> None:  # noqa: N802 - wx API
        pass


class _FocusEvent:
    def __init__(self, index: int) -> None:
        self.index = index
        self.skipped = False

    def GetIndex(self) -> int:  # noqa: N802 - wx API
        return self.index

    def Skip(self) -> None:  # noqa: N802 - wx API
        self.skipped = True


@pytest.mark.parametrize(
    ("position", "expected_title"),
    [
        ("before", "Odtwarzane, Utwór"),
        ("after", "Utwór, Odtwarzane"),
        ("off", "Utwór"),
    ],
)
def test_search_dialog_renders_playback_position_without_changing_cue_identity(
    position: str, expected_title: str,
) -> None:
    entry = SearchEntry(
        SessionId.PODCASTS,
        Row("episode:1", "Utwór", "episode"),
    )
    cues: list[SearchEntry] = []
    dialog = object.__new__(SearchDialog)
    dialog.search_button = SimpleNamespace(Enable=lambda _enabled: None)
    dialog.results = _Results()
    dialog.status = SimpleNamespace(SetLabel=lambda _text: None, SetName=lambda _text: None)
    dialog._close_relations = lambda **_kwargs: None
    dialog._entries = []
    dialog._all_services = False
    dialog._playback_label = lambda candidate: "Odtwarzane" if candidate is entry else ""
    dialog._playback_state_position = lambda: position
    dialog._playback_cue = cues.append

    SearchDialog.show_results(dialog, [entry])
    event = _FocusEvent(0)
    SearchDialog._on_result_focused(dialog, event)

    assert [row[0] for row in dialog.results.rows] == [expected_title]
    assert cues == [entry]
    assert event.skipped is True


def _navigator_with_material(
    session_id: SessionId, row: Row, confirmed_id: str,
) -> Navigator:
    navigator = Navigator()
    navigator.active = session_id
    navigator.session.model.replace([row], preferred_id=row.item_id)
    navigator.session.current_material_id = confirmed_id
    return navigator


def test_requested_wiim_id_stays_pending_and_client_none_clears_it() -> None:
    requested = Row(
        "wiim-stream:requested", "Radio", "wiimNetworkStream",
        parent_id="device-1", service_id="requested", service_kind="wiimNetworkStream",
    )
    navigator = _navigator_with_material(SessionId.WIIM, requested, "wiim-stream:old")
    intent = navigator.activate_selected()[0]
    assert navigator.session.current_material_id == "wiim-stream:old"
    assert navigator.session.pending_material_id == requested.item_id
    frame = SimpleNamespace(
        client=None,
        navigator=navigator,
        _run=lambda _events: None,
        announcer=SimpleNamespace(say=lambda _text: None),
    )

    LiteFrame._activate_wiim_network_stream(frame, intent)

    assert navigator.session.current_material_id == ""
    assert navigator.session.pending_material_id == ""


def test_requested_sonos_id_stays_pending_when_client_or_group_is_unavailable() -> None:
    requested = Row(
        "sonos-stream:requested", "Radio", "sonosOwnStream",
        parent_id="house-1", service_id="requested", service_kind="ownStream",
    )
    for client, group_id in ((None, "group-1"), (SimpleNamespace(), "")):
        navigator = _navigator_with_material(
            SessionId.SONOS, requested, "sonos-stream:old",
        )
        intent = navigator.activate_selected()[0]
        assert navigator.session.current_material_id == "sonos-stream:old"
        assert navigator.session.pending_material_id == requested.item_id
        frame = SimpleNamespace(
            client=client,
            navigator=navigator,
            announcer=SimpleNamespace(say=lambda _text: None),
            _sonos_active_household_id="house-1",
            _sonos_active_group_id=group_id,
        )

        LiteFrame._play_sonos_material(frame, intent)

        assert navigator.session.current_material_id == ""
        assert navigator.session.pending_material_id == ""


def test_sonos_host_prefix_is_the_only_confirmable_own_stream_prefix() -> None:
    row = Row(
        "sonos-stream:stream-7", "Radio", "sonosOwnStream",
        parent_id="house-1", service_id="stream-7", service_kind="ownStream",
    )
    navigator = _navigator_with_material(SessionId.SONOS, row, "")
    frame = SimpleNamespace(
        navigator=navigator,
        _sonos_active_household_id="house-1",
        _sonos_active_group_id="group-1",
        state=LiteState(),
        _queue_streamer_history_save=lambda: None,
    )
    confirmed = {
        "householdId": "house-1", "groupId": "group-1",
        "playbackState": "playing", "observedMaterialId": row.item_id,
        "identityProvenance": "playbackItemId",
    }

    LiteFrame._apply_streamer_observed_material_identity(
        frame, SessionId.SONOS, confirmed,
    )
    assert navigator.session.current_material_id == row.item_id

    wrong_prefix = {**confirmed, "observedMaterialId": "sonos-ownStream:stream-7"}
    assert LiteFrame._streamer_observed_material_id(
        frame, SessionId.SONOS, wrong_prefix,
    ) == ""


def test_buffering_never_marks_or_cues_streamer_material() -> None:
    row = Row(
        "sonos-stream:stream-7", "Radio", "sonosOwnStream",
        parent_id="house-1", service_id="stream-7", service_kind="ownStream",
    )
    navigator = _navigator_with_material(SessionId.SONOS, row, "")
    frame = SimpleNamespace(
        navigator=navigator,
        _sonos_active_household_id="house-1",
        _sonos_active_group_id="group-1",
        _sonos_snapshot={
            "householdId": "house-1", "groupId": "group-1",
            "playbackState": "buffering",
            "observedMaterialId": row.item_id,
            "identityProvenance": "playbackItemId",
        },
        options=SimpleNamespace(radio_playback_state_position="before"),
        state=LiteState(),
        _queue_streamer_history_save=lambda: None,
    )

    LiteFrame._apply_streamer_observed_material_identity(
        frame, SessionId.SONOS, frame._sonos_snapshot,
    )
    LiteFrame._search_playback_cue(frame, SearchEntry(SessionId.SONOS, row))

    assert navigator.session.current_material_id == ""
    assert frame.state.playback_history_by_session == {}


def test_paused_native_search_result_has_label_but_no_activity_cue() -> None:
    row = Row("episode:1", "Odcinek", "episode")
    navigator = _navigator_with_material(SessionId.PODCASTS, row, row.item_id)
    cues: list[Row] = []
    frame = SimpleNamespace(
        navigator=navigator,
        options=SimpleNamespace(radio_playback_state_position="before"),
        _playing_sessions={SessionId.PODCASTS},
        _transport_status_by_session={SessionId.PODCASTS: {"paused": True}},
        _native_playback_snapshot=lambda _session_id: (True, True),
        _play_activity_cue=cues.append,
    )
    entry = SearchEntry(SessionId.PODCASTS, row)

    assert LiteFrame._search_playback_label(frame, entry) == "Wstrzymane"
    LiteFrame._search_playback_cue(frame, entry)

    assert cues == []


def test_csharp_observed_material_fixture_drives_real_python_loaders_and_gui() -> None:
    """Read each C#-mapped case; rows are host producer payloads, never test rows."""
    raw_path = os.environ.get("AMC_OBSERVED_MATERIAL_FIXTURE")
    if not raw_path:
        pytest.skip("AMC_OBSERVED_MATERIAL_FIXTURE is not supplied by the C# contract worker")
    fixture = json.loads(Path(raw_path).read_text(encoding="utf-8"))
    cases = fixture["cases"]
    assert isinstance(cases, list) and cases
    coverage: dict[SessionId, list[str]] = {SessionId.WIIM: [], SessionId.SONOS: []}

    for case in cases:
        assert isinstance(case, dict)
        session_id = SessionId[str(case["session"])]
        expected_id = case["expectedMaterialId"]
        assert expected_id is None or isinstance(expected_id, str)
        if session_id is SessionId.WIIM:
            device_id = case["deviceId"]
            rows = network_stream_rows(case["listPayload"], device_id=device_id)
            state = wiim_snapshot(case["snapshot"])
            context = {"_wiim_active_device_id": device_id}
        elif session_id is SessionId.SONOS:
            household_id = case["householdId"]
            rows = sonos_material_rows(case["listPayload"], household_id)
            state = sonos_snapshot(case["snapshot"])
            context = {
                "_sonos_active_household_id": household_id,
                "_sonos_active_group_id": case["groupId"],
            }
        else:
            raise AssertionError(f"unsupported fixture session: {case['session']}")

        navigator = Navigator()
        navigator.active = session_id
        navigator.session.model.replace(rows)
        frame = SimpleNamespace(
            navigator=navigator,
            options=SimpleNamespace(radio_playback_state_position="before"),
            state=LiteState(),
            _queue_streamer_history_save=lambda: None,
            _wiim_snapshot=state if session_id is SessionId.WIIM else {},
            _sonos_snapshot=state if session_id is SessionId.SONOS else {},
            **context,
        )

        LiteFrame._apply_streamer_observed_material_identity(frame, session_id, state)
        assert navigator.session.current_material_id == (expected_id or ""), case["name"]
        playback = str(state["playbackState"]).casefold()
        if expected_id is None:
            coverage[session_id].append(
                "idle" if playback not in {"playing", "play", "odtwarzanie"} else "unresolved-live"
            )
            assert frame.state.playback_history_by_session == {}, case["name"]
            continue
        coverage[session_id].append(
            "paused" if playback in {"paused", "pause", "pauza", "wstrzymane"} else "playing"
        )
        matched = next(row for row in rows if row.item_id == expected_id)
        expected_label = (
            "Wstrzymane"
            if playback in {"paused", "pause", "pauza", "wstrzymane"}
            else "Odtwarzane"
        )
        assert playback in {
            "playing", "play", "odtwarzanie", "paused", "pause", "pauza", "wstrzymane",
        }, case["name"]
        entry = SearchEntry(session_id, matched)
        assert LiteFrame._search_playback_label(frame, entry) == expected_label, case["name"]
        dialog = object.__new__(SearchDialog)
        dialog.search_button = SimpleNamespace(Enable=lambda _enabled: None)
        dialog.results = _Results()
        dialog.status = SimpleNamespace(SetLabel=lambda _text: None, SetName=lambda _text: None)
        dialog._close_relations = lambda **_kwargs: None
        dialog._entries = []
        dialog._all_services = False
        dialog._playback_label = lambda candidate: LiteFrame._search_playback_label(frame, candidate)
        dialog._playback_state_position = lambda: "before"
        dialog._playback_cue = lambda _candidate: None
        SearchDialog.show_results(dialog, [entry])
        assert dialog.results.rows[0][0] == f"{expected_label}, {matched.title}", case["name"]
        if expected_label == "Odtwarzane":
            assert frame.state.playback_history_by_session[session_id.value], case["name"]
        else:
            assert frame.state.playback_history_by_session == {}, case["name"]

    for session_id, scenarios in coverage.items():
        assert {"playing", "paused", "idle"}.issubset(scenarios), session_id.value
        # Mismatch and ambiguity deliberately expose no observed ID; the fixture
        # must exercise two distinct live-unresolved producer snapshots.
        assert scenarios.count("unresolved-live") >= 2, session_id.value
