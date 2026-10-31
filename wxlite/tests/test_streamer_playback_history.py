from __future__ import annotations

from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Announce, LibraryView, Navigator, SessionId, View
from amc_wx_lite.playback_history import (
    activity_rows,
    history_rows,
    observed_frontier_smart_row,
    observed_sonos_row,
    observed_wiim_row,
    record_row,
    normalize_histories,
)
from amc_wx_lite.shortcuts import (
    Action, Chord, resolve, FRONTIER_SMART_SUPPORTED_ACTIONS,
    RADIOARCHIWISTA_SUPPORTED_ACTIONS, TIDAL_SUPPORTED_ACTIONS,
)
from amc_wx_lite.state_store import LiteState


def test_wiim_and_sonos_observations_require_real_playback() -> None:
    assert observed_wiim_row({
        "playbackState": "paused",
        "title": "Nie zapisuj",
    }) is None
    assert observed_sonos_row({
        "playbackState": "idle",
        "title": "Nie zapisuj",
    }) is None

    wiim = observed_wiim_row({
        "playbackState": "odtwarzanie",
        "title": "Wieczorna audycja",
        "artist": "Prowadzący",
        "album": "Program",
        "source": "radio internetowe",
    })
    sonos = observed_sonos_row({
        "playbackState": "buffering",
        "title": "Utwór Sonos",
        "source": "Źródło: Spotify",
        "durationSeconds": 180,
    })

    assert wiim is not None and wiim.kind == "station"
    assert sonos is not None and sonos.kind == "track"
    assert "observed" in wiim.item_id
    assert "observed" in sonos.item_id


def test_streamer_list_marks_only_confirmed_material_as_playing() -> None:
    rows = [
        Row("first", "Pierwsza", "sonosFavorite", detail="Radio"),
        Row("second", "Druga", "sonosFavorite"),
    ]

    playing = activity_rows(
        rows,
        current_item_id="first",
        playback_state="playing",
    )
    assert playing[0].detail == "Radio"
    assert playing[0].state_detail == "odtwarzane"
    assert playing[0].playback_activity is True
    assert playing[1].state_detail == ""

    cleared = activity_rows(
        playing,
        current_item_id="first",
        playback_state="idle",
    )
    assert all(not row.state_detail for row in cleared)
    assert all(not row.playback_activity for row in cleared)


def test_confirmed_sonos_material_is_saved_as_replayable_history() -> None:
    saved: list[bool] = []
    frame = SimpleNamespace(
        state=LiteState(),
        _queue_streamer_history_save=lambda: saved.append(True),
    )
    row = Row(
        "sonos-stream:one",
        "Radio domowe",
        "sonosOwnStream",
        parent_id="house-one",
        service_id="stream-one",
        service_kind="ownStream",
    )

    LiteFrame._record_streamer_history_row(
        frame, SessionId.SONOS, row, replay_supported=True
    )

    restored = history_rows(
        "sonos", frame.state.playback_history_by_session["sonos"]
    )
    assert len(restored) == 1
    assert restored[0].activation_message is None
    assert restored[0].parent_id == "house-one"
    assert restored[0].service_id == "stream-one"
    assert saved == [True]


def test_streamer_history_is_newest_first_deduplicated_and_user_facing() -> None:
    first = observed_wiim_row({
        "playbackState": "playing",
        "title": "Pierwsza stacja",
        "artist": "",
        "source": "radio internetowe",
    })
    second = observed_wiim_row({
        "playbackState": "playing",
        "title": "Druga stacja",
        "artist": "",
        "source": "radio internetowe",
    })
    assert first is not None and second is not None

    history = record_row([], first, replay_supported=False)
    history = record_row(history, second, replay_supported=False)
    history = record_row(history, first, replay_supported=False)
    rows = history_rows("wiim", history)

    assert [row.title for row in rows] == ["Pierwsza stacja", "Druga stacja"]
    assert rows[0].detail == "radio internetowe"
    assert rows[0].activation_message == (
        "Ten wpis pokazuje materiał zaobserwowany przez WiiM. "
        "Urządzenie nie udostępniło danych potrzebnych do ponownego "
        "uruchomienia go z Historii"
    )
    assert "observed" not in rows[0].title
    assert "observed" not in rows[0].detail


def test_snapshot_recorder_keeps_sessions_separate_and_ignores_duplicates() -> None:
    queued_saves: list[bool] = []
    frame = SimpleNamespace(
        state=LiteState(),
        _queue_streamer_history_save=lambda: queued_saves.append(True),
    )

    wiim = {
        "playbackState": "playing",
        "title": "Stacja WiiM",
        "source": "radio internetowe",
    }
    sonos = {
        "playbackState": "playing",
        "title": "Utwór Sonos",
        "source": "Źródło: Spotify",
        "durationSeconds": 180,
    }
    LiteFrame._record_streamer_history_snapshot(frame, SessionId.WIIM, wiim)
    LiteFrame._record_streamer_history_snapshot(frame, SessionId.WIIM, wiim)
    LiteFrame._record_streamer_history_snapshot(frame, SessionId.SONOS, sonos)

    assert set(frame.state.playback_history_by_session) == {"wiim", "sonos"}
    assert len(frame.state.playback_history_by_session["wiim"]) == 1
    assert len(frame.state.playback_history_by_session["sonos"]) == 1
    assert queued_saves == [True, True]


def test_ctrl_h_keeps_each_streamer_in_its_own_session() -> None:
    for session_id in (
        SessionId.WIIM, SessionId.SONOS, SessionId.FRONTIER_SMART,
        SessionId.TIDAL, SessionId.RADIOARCHIWISTA,
    ):
        navigator = Navigator()
        navigator.active = session_id
        navigator.session.model.replace([])
        events: list[list[object]] = []
        frame = SimpleNamespace(
            navigator=navigator,
            state=LiteState(playback_history_by_session={
                session_id.value: [{
                    "itemId": f"{session_id.value}:observed:one",
                    "title": "Własny wpis",
                    "kind": "track",
                    "replaySupported": False,
                }]
            }),
            _transient_preview_return=None,
            _announce_transient_preview=events.append,
        )
        frame._begin_transient_preview = (
            lambda current=frame: LiteFrame._begin_transient_preview(current)
        )

        LiteFrame._show_playback_history_preview(frame)

        assert navigator.active is session_id
        assert navigator.session.library_view is LibraryView.SERVICE_HISTORY
        assert navigator.session.model.selected_row.title == "Własny wpis"
        assert len(events) == 1
        assert "Historia odtwarzania" in events[0][0].text


def test_ctrl_h_reaches_wiim_and_frontier_history_from_list_and_player() -> None:
    for session_id in (SessionId.WIIM, SessionId.FRONTIER_SMART):
        for view in (View.LIST, View.PLAYER):
            navigator = Navigator()
            navigator.active = session_id
            navigator.session.view = view
            frame = SimpleNamespace(
                navigator=navigator,
                state=LiteState(playback_history_by_session={
                    session_id.value: [{
                        "itemId": f"{session_id.value}:observed:one",
                        "title": "Odtwarzany materiał",
                        "kind": "track",
                        "replaySupported": False,
                    }]
                }),
                _global_transient_previews=False,
                messages=SimpleNamespace(custom_seek_seconds=300),
                _transient_preview_return=None,
                _announce_transient_preview=lambda events: None,
                announcer=SimpleNamespace(say=lambda message: None),
            )
            frame._begin_transient_preview = (
                lambda current=frame: LiteFrame._begin_transient_preview(current)
            )
            frame._show_playback_history_preview = (
                lambda current=frame: LiteFrame._show_playback_history_preview(current)
            )
            action = resolve(
                Chord("H", ctrl=True), player_view=view is View.PLAYER,
                radio_session=False,
                wiim_session=session_id is SessionId.WIIM,
                frontier_smart_session=session_id is SessionId.FRONTIER_SMART,
            )
            assert action is Action.VIEW_HISTORY
            LiteFrame._dispatch(frame, action)
            assert navigator.active is session_id
            assert navigator.session.library_view is LibraryView.SERVICE_HISTORY
            assert navigator.session.model.selected_row.title == "Odtwarzany materiał"


def test_new_session_history_contract_and_persistence() -> None:
    for supported in (
        TIDAL_SUPPORTED_ACTIONS, FRONTIER_SMART_SUPPORTED_ACTIONS,
        RADIOARCHIWISTA_SUPPORTED_ACTIONS,
    ):
        assert Action.VIEW_HISTORY in supported
        assert Action.REMOVE_SELECTED in supported

    archive = Row(
        "archive:recording:one", "TOK FM, 30.10.2026 23:00", "recording",
        url="https://example.org/recording.mp3",
    )
    tidal = Row(
        "tidal:track:one", "Utwór", "track", service_id="track-one",
        related_album_service_id="album-one",
    )
    saved = normalize_histories({
        "radioarchiwista": record_row([], archive, replay_supported=True),
        "tidal": record_row([], tidal, replay_supported=True),
    })
    archive_row = history_rows("radioarchiwista", saved["radioarchiwista"])[0]
    tidal_row = history_rows("tidal", saved["tidal"])[0]
    assert archive_row.url == archive.url
    assert archive_row.activation_message is None
    assert tidal_row.related_album_service_id == "album-one"
    assert tidal_row.activation_message is None

    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    navigator.apply_service_history(SessionId.RADIOARCHIWISTA, [archive_row])
    assert any(event.__class__.__name__ == "PlayMedia"
               for event in navigator.activate_selected())


def test_frontier_history_only_records_confirmed_playback() -> None:
    assert observed_frontier_smart_row({
        "playbackState": "wstrzymane", "title": "Nie zapisuj",
    }) is None
    row = observed_frontier_smart_row({
        "playbackState": "odtwarzanie", "title": "Radio TOK FM",
        "deviceTitle": "Radio w salonie",
    })
    assert row is not None
    assert row.title == "Radio TOK FM"
    assert row.detail == "Radio w salonie"
    saved = record_row([], row, replay_supported=False)
    restored = history_rows("frontierSmart", saved)[0]
    assert "Frontier Smart" in restored.activation_message
    navigator = Navigator()
    navigator.active = SessionId.FRONTIER_SMART
    navigator.apply_service_history(SessionId.FRONTIER_SMART, [restored])
    assert isinstance(navigator.activate_selected()[0], Announce)


def test_escape_returns_from_new_session_history_to_same_selection() -> None:
    for session_id in (
        SessionId.FRONTIER_SMART, SessionId.TIDAL,
        SessionId.RADIOARCHIWISTA,
    ):
        navigator = Navigator()
        navigator.active = session_id
        navigator.session.model.replace([
            Row("one", "Pierwszy", "track"),
            Row("two", "Drugi", "track"),
        ], preferred_id="two")
        frame = SimpleNamespace(
            navigator=navigator,
            state=LiteState(),
            _transient_preview_return=None,
            _announce_transient_preview=lambda events: None,
            _sync_views=lambda: None,
        )
        frame._begin_transient_preview = (
            lambda current=frame: LiteFrame._begin_transient_preview(current)
        )
        LiteFrame._show_playback_history_preview(frame)
        assert navigator.session.library_view is LibraryView.SERVICE_HISTORY
        assert LiteFrame._restore_transient_preview(frame, announce=False)
        assert navigator.active is session_id
        assert navigator.session.model.selected_id == "two"


def test_enter_on_observed_history_row_explains_api_limit() -> None:
    navigator = Navigator()
    navigator.active = SessionId.SONOS
    rows = list(history_rows("sonos", [{
        "itemId": "sonos:observed:one",
        "title": "Utwór",
        "kind": "track",
        "replaySupported": False,
    }]))
    navigator.apply_service_history(SessionId.SONOS, rows)

    events = navigator.activate_selected()

    assert len(events) == 1
    assert isinstance(events[0], Announce)
    assert "Sonos nie udostępnił danych" in events[0].text


def test_delete_is_history_only_and_keeps_focus_on_next_row() -> None:
    spoken: list[str] = []
    saves: list[bool] = []
    synced: list[bool] = []
    history = [
        {
            "itemId": "wiim:observed:first",
            "title": "Pierwsza",
            "kind": "station",
            "replaySupported": False,
        },
        {
            "itemId": "wiim:observed:second",
            "title": "Druga",
            "kind": "station",
            "replaySupported": False,
        },
    ]
    navigator = Navigator()
    navigator.active = SessionId.WIIM
    navigator.apply_service_history(
        SessionId.WIIM, list(history_rows("wiim", history))
    )
    selected = navigator.session.model.selected_row
    state = LiteState(playback_history_by_session={"wiim": history})
    frame = SimpleNamespace(
        navigator=navigator,
        state=state,
        announcer=SimpleNamespace(say=spoken.append),
        _selected_action_rows=lambda: [selected],
        _save_state=lambda: saves.append(True) or True,
        _sync_views=lambda: synced.append(True),
    )

    LiteFrame._remove_streamer_history_entries(frame)

    assert [item["title"] for item in state.playback_history_by_session["wiim"]] == [
        "Druga"
    ]
    assert navigator.session.model.selected_row.title == "Druga"
    assert saves == [True]
    assert synced == [True]
    assert spoken == [
        "Usunięto z Historii WiiM: Pierwsza. "
        "Urządzenie i źródło pozostały bez zmian"
    ]


def test_history_delete_is_owned_but_page_keys_remain_native() -> None:
    for session_id in (SessionId.WIIM, SessionId.SONOS):
        kwargs = {
            "player_view": False,
            "radio_session": False,
            "wiim_session": session_id is SessionId.WIIM,
            "sonos_session": session_id is SessionId.SONOS,
            "service_history": True,
        }
        assert resolve(Chord("Delete"), **kwargs) is Action.REMOVE_SELECTED
        assert resolve(Chord("Prior"), **kwargs) is None
        assert resolve(Chord("Next"), **kwargs) is None
