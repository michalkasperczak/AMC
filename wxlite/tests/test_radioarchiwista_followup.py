"""Regresje po pierwszym odbiorze Radio Archiwisty w AMC Test."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace
import sys
import types
from typing import Any, cast

try:
    import wx
except ModuleNotFoundError:
    class _WxStub(types.ModuleType):
        def __getattr__(self, name: str):
            value = 0 if name.isupper() or name.startswith("ID_") else type(name, (), {})
            setattr(self, name, value)
            return value

    wx = _WxStub("wx")
    wx.GetTopLevelWindows = lambda: ()
    wx.version = None
    sys.modules["wx"] = wx

from amc_wx_lite import gui, i18n, menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame, copied_address_message
from amc_wx_lite.state_store import LiteState, Options
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, PlayMedia, SessionId, View
from amc_wx_lite.playback_activity import activity_rows
from amc_wx_lite.radioarchiwista_source import (
    ArchiveRecording,
    RadioarchiwistaSource,
    day_rows,
)
from amc_wx_lite.shortcuts import (
    Action, RADIOARCHIWISTA_SUPPORTED_ACTIONS, chord_from_canonical,
    describe_action, readable_shortcut, resolve,
)
from amc_wx_lite.transport_parity import MessagePolicy


def recording(item_id: str) -> ArchiveRecording:
    return ArchiveRecording(
        station_slug="pr1", station_name="Program 1", item_id=item_id,
        title="08:00", audio_url=f"https://archive.org/download/{item_id}.mp3",
        duration_seconds=3600, local_date_label="30.10.2026",
        local_hour_label="08:00", local_label="30.10.2026 08:00",
        utc_started_at="2026-10-30T07:00:00Z", hour=8,
        completeness_ratio=1.0, status="published",
    )


def test_archive_shortcuts_route_in_list_and_player() -> None:
    expected = {
        "F5": Action.RADIOARCHIWISTA_REFRESH,
        "Ctrl+Shift+U": Action.TOGGLE_RADIOARCHIWISTA_FAVORITE,
        "Ctrl+Shift+C": Action.COPY_ADDRESS,
        "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
        "Ctrl+L": Action.VIEW_LIBRARY,
        "Ctrl+Shift+Q": Action.ADD_TO_QUEUE,
        "Ctrl+Shift+Return": Action.TOGGLE_PLAY_NEXT,
        "Space": Action.PLAY_PAUSE,
    }
    for player in (False, True):
        for key, action in expected.items():
            assert resolve(
                chord_from_canonical(key), player_view=player, radio_session=False,
                radioarchiwista_session=True,
            ) is action
            assert action in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    for digit in range(10):
        action = Action[f"SEEK_PERCENT_{digit * 10}"]
        digit_chord = chord_from_canonical(str(digit))
        assert digit_chord is not None
        assert resolve(
            digit_chord, player_view=True, radio_session=False,
            radioarchiwista_session=True,
        ) is action
        assert action in RADIOARCHIWISTA_SUPPORTED_ACTIONS
        # Lista audycji zostawia cyfrę natywnej liście: literalny wybór nazwy,
        # bez startu albo skoku w aktualnym nagraniu.
        assert resolve(
            digit_chord, player_view=False, radio_session=False,
            radioarchiwista_session=True,
        ) is None
    assert Action.ADD_BOOKMARK in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert Action.NEXT_BOOKMARK in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    for key, action in (
        ("B", Action.ADD_BOOKMARK),
        ("Ctrl+Shift+B", Action.ADD_NAMED_BOOKMARK),
        ("Shift+Prior", Action.PREVIOUS_BOOKMARK),
        ("Shift+Next", Action.NEXT_BOOKMARK),
    ):
        assert resolve(
            chord_from_canonical(key), player_view=True, radio_session=False,
            radioarchiwista_session=True,
        ) is action
        assert action in RADIOARCHIWISTA_SUPPORTED_ACTIONS


def test_archive_queue_menu_and_copy_label() -> None:
    entries = {
        item.action: item
        for menu in menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
        for item in menu.items if item.action is not None
    }
    assert entries[Action.VIEW_SAVED_QUEUE].shortcut == "Ctrl+Q"
    assert entries[Action.VIEW_LIBRARY].shortcut == "Ctrl+L"
    assert entries[Action.ADD_TO_QUEUE].shortcut == "Ctrl+Shift+Q"
    assert entries[Action.TOGGLE_PLAY_NEXT].shortcut == "Ctrl+Shift+Return"
    assert entries[Action.COPY_ADDRESS].shortcut == "Ctrl+Shift+C"
    assert "link do nagrania" in entries[Action.COPY_ADDRESS].label
    assert "link do nagrania" in describe_action(
        Action.COPY_ADDRESS, "Radio Archiwista"
    )
    assert "lokalną kolejkę" in describe_action(
        Action.VIEW_SAVED_QUEUE, "Radio Archiwista"
    )
    palette = {item.action: item for item in entries_for_session(SessionId.RADIOARCHIWISTA)}
    for action in (Action.VIEW_LIBRARY, Action.VIEW_SAVED_QUEUE,
                   Action.ADD_TO_QUEUE, Action.TOGGLE_PLAY_NEXT, Action.COPY_ADDRESS):
        assert palette[action].shortcut == readable_shortcut(entries[action].shortcut)
        assert palette[action].name == entries[action].label.replace("&", "")
    row = Row("rarch:rec:a", "08:00", "recording", url="https://archive.org/a.mp3")
    assert row.address == "https://archive.org/a.mp3"
    assert copied_address_message(row) == "Skopiowano link do nagrania"


def test_archive_player_context_menu_contains_copy_link() -> None:
    recording_row = Row(
        "rarch:rec:playing", "Grające", "recording",
        url="https://archive.org/playing.mp3",
    )
    entry = next(
        item
        for menu in menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
        for item in menu.items
        if item.action is Action.COPY_ADDRESS
    )
    native = SimpleNamespace(
        IsEnabled=lambda: True,
        GetItemLabel=lambda: "Skopiuj &link do nagrania\tCtrl+Shift+C",
    )
    frame = cast(Any, LiteFrame.__new__(LiteFrame))
    frame.navigator = SimpleNamespace(
        session=SimpleNamespace(model=SimpleNamespace(selected_row=recording_row)),
    )
    frame._menu_items = [(native, entry)]
    frame._refresh_menu_state = lambda: None

    groups = frame._context_menu_command_groups(player=True)
    labels = [command.label for group in groups for command in group]

    assert "Skopiuj link do nagrania (Ctrl+Shift+C)" in labels


def test_archive_bookmark_reaches_shared_writer() -> None:
    messages: list[str] = []
    requests: list[dict] = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    state = SimpleNamespace(
        current_material_id="rarch:rec:playing",
        pending_material_id="",
        now_playing_title="Nagranie 08:00",
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIOARCHIWISTA, session=state),
        client=SimpleNamespace(add_bookmark=lambda **args: (
            requests.append(args) or {"added": True, "positionSeconds": 120}
        )),
        runner=Runner(),
        announcer=SimpleNamespace(say=messages.append),
    )

    LiteFrame._add_bookmark(frame)

    assert requests == [{
        "item_id": "rarch:rec:playing",
        "item_title": "Nagranie 08:00",
        "session_id": "radioarchiwista",
        "name": None,
    }]
    assert messages == ["Dodano zakładkę: 2:00"]


def test_archive_player_dispatches_named_bookmark_navigation_to_shared_host() -> None:
    """Nie wystarcza wpis w resolverze: akcja odtwarzacza niesie sesję do hosta."""
    requests: list[tuple[int, str, bool]] = []
    spoken: list[str] = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    frame = cast(Any, LiteFrame.__new__(LiteFrame))
    frame.navigator = SimpleNamespace(active=SessionId.RADIOARCHIWISTA)
    frame.client = SimpleNamespace(
        navigate_bookmark=lambda direction, session_id, named_only: (
            requests.append((direction, session_id, named_only))
            or {"found": True, "positionSeconds": 120, "name": "Wstęp"}
        ),
    )
    frame.runner = Runner()
    frame.announcer = SimpleNamespace(say=spoken.append, show=lambda _text: None)
    frame.messages = MessagePolicy()
    frame._last_status = {}

    LiteFrame._dispatch(frame, Action.NEXT_NAMED_BOOKMARK)

    assert requests == [(1, "radioarchiwista", True)]
    assert spoken == ["Wstęp, 2:00"]
    assert frame._last_status["positionSeconds"] == 120.0


def test_archive_player_dispatches_percent_seek_to_shared_host() -> None:
    """Cyfra w odtwarzaczu daje absolutny seek z rarch session_id, nie radio live."""
    requests: list[tuple[float, str]] = []
    spoken: list[str] = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    frame = cast(Any, LiteFrame.__new__(LiteFrame))
    frame.navigator = SimpleNamespace(active=SessionId.RADIOARCHIWISTA)
    frame.client = SimpleNamespace(
        seek_to_position=lambda seconds, session_id: (
            requests.append((seconds, session_id))
            or {"positionSeconds": seconds}
        ),
    )
    frame.runner = Runner()
    frame.announcer = SimpleNamespace(say=spoken.append)
    frame.messages = MessagePolicy()
    frame._last_status = {"positionSeconds": 0.0, "durationSeconds": 600.0}

    LiteFrame._dispatch(frame, Action.SEEK_PERCENT_50)

    assert requests == [(300.0, "radioarchiwista")]
    assert spoken == ["50%"]


def test_archive_player_percent_seek_without_duration_never_calls_host() -> None:
    """Brak długości nagrania jest komunikowany; cyfra nie udaje sukcesu."""
    requests: list[tuple[float, str]] = []
    spoken: list[str] = []

    frame = cast(Any, LiteFrame.__new__(LiteFrame))
    frame.navigator = SimpleNamespace(active=SessionId.RADIOARCHIWISTA)
    frame.client = SimpleNamespace(
        seek_to_position=lambda seconds, session_id: requests.append((seconds, session_id)),
    )
    frame.announcer = SimpleNamespace(say=spoken.append)
    frame.messages = MessagePolicy(enabled=False, percentage_seek_messages=False)
    frame._last_status = {"positionSeconds": 10.0, "durationSeconds": None}

    LiteFrame._dispatch(frame, Action.SEEK_PERCENT_50)

    assert requests == []
    assert spoken == ["Skok procentowy niedostępny: czas trwania jest nieznany"]


def test_archive_ctrl_h_dispatches_to_own_history_from_list_and_player() -> None:
    """Ctrl+H przechodzi resolver i dispatcher; nie miesza historii Plików."""
    for player_view in (False, True):
        navigator = Navigator()
        navigator.active = SessionId.RADIOARCHIWISTA
        navigator.session.view = View.PLAYER if player_view else View.LIST
        history_events: list[list[object]] = []
        frame = cast(Any, LiteFrame.__new__(LiteFrame))
        frame.navigator = navigator
        frame.state = LiteState(playback_history_by_session={
            "radioarchiwista": [{
                "itemId": "rarch:rec:history",
                "title": "Nagranie z historii",
                "kind": "recording",
                "url": "https://archive.org/history.mp3",
                "replaySupported": True,
            }]
        })
        frame._global_transient_previews = False
        frame.messages = MessagePolicy()
        frame.announcer = SimpleNamespace(say=lambda _text: None)
        frame._transient_preview_return = None
        frame._announce_transient_preview = history_events.append
        frame._begin_transient_preview = (
            lambda current=frame: LiteFrame._begin_transient_preview(current)
        )
        frame._show_playback_history_preview = (
            lambda current=frame: LiteFrame._show_playback_history_preview(current)
        )

        action = resolve(
            chord_from_canonical("Ctrl+H"),
            player_view=player_view,
            radio_session=False,
            radioarchiwista_session=True,
        )
        assert action is Action.VIEW_HISTORY
        LiteFrame._dispatch(frame, action)

        assert navigator.active is SessionId.RADIOARCHIWISTA
        assert navigator.session.library_view is LibraryView.SERVICE_HISTORY
        assert navigator.session.model.selected_row.title == "Nagranie z historii"
        assert len(history_events) == 1


def test_archive_queue_persists_and_reorders(tmp_path: Path) -> None:
    source = RadioarchiwistaSource(tmp_path)
    first, second, third = (recording(f"rarch:rec:{number}") for number in range(3))
    assert source.toggle_queue(first)
    assert source.toggle_queue(second)
    assert source.toggle_queue(third, play_next=True, current_id=first.item_id)
    reopened = RadioarchiwistaSource(tmp_path)
    assert [row.item_id for row in reopened.queue_rows()] == [
        first.item_id, third.item_id, second.item_id,
    ]
    assert reopened.recording_by_id(third.item_id) is not None
    assert not reopened.toggle_queue(third)
    assert [row.item_id for row in reopened.queue_rows()] == [first.item_id, second.item_id]


def test_archive_queue_continues_with_remote_url() -> None:
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    rows = [
        Row(f"rarch:rec:{number}", str(number), "recording",
            url=f"https://archive.org/{number}.mp3")
        for number in range(2)
    ]
    navigator.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_QUEUE, rows)
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id(rows[0].item_id)
    assert any(isinstance(intent, PlayMedia) for intent in navigator.activate_selected())
    next_items = navigator.continue_radioarchiwista_queue_after_end()
    play = next(intent for intent in next_items if isinstance(intent, PlayMedia))
    assert play.source == rows[1].url
    assert navigator.continue_radioarchiwista_queue_after_end() == []


def test_archive_page_down_uses_remote_recording_source() -> None:
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    rows = [
        Row(f"rarch:rec:{number}", str(number), "recording",
            url=f"https://archive.org/{number}.mp3")
        for number in range(2)
    ]
    navigator.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_HOURS, rows)
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id(rows[0].item_id)
    navigator.activate_selected()
    intents = navigator.step_playback_source(True)
    assert intents is not None
    play = next(intent for intent in intents if isinstance(intent, PlayMedia))
    assert play.source == rows[1].url


def test_archive_left_reads_duration_without_file_size_or_bitrate() -> None:
    messages: list[str] = []
    row = Row("rarch:rec:a", "08:00", "recording",
              url="https://archive.org/a.mp3", duration_ticks=3600 * 10_000_000)
    frame = SimpleNamespace(
        state=LiteState(), options=Options(),
        navigator=SimpleNamespace(
            active=SessionId.RADIOARCHIWISTA,
            session=SimpleNamespace(model=SimpleNamespace(selected_row=row)),
        ),
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._announce_quick_information(frame)
    assert messages == ["1:00:00"]


def test_archive_ctrl_l_returns_to_full_station_list() -> None:
    calls: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIOARCHIWISTA),
        _open_radioarchiwista_stations=lambda: calls.append("stations"),
    )
    LiteFrame._return_to_library(frame)
    assert calls == ["stations"]


def test_archive_recording_has_spoken_playback_state() -> None:
    row = Row("rarch:rec:a", "08:00", "recording",
              url="https://archive.org/a.mp3")
    playing = activity_rows([row], current_item_id=row.item_id,
                            playing=True, paused=False)
    assert playing[0].playback_state == "Odtwarzane"
    paused = activity_rows(playing, current_item_id=row.item_id,
                           playing=True, paused=True)
    assert paused[0].playback_state == "Wstrzymane"


def test_archive_player_copies_link_of_playing_recording() -> None:
    playing = Row("rarch:rec:playing", "Grające", "recording",
                  url="https://archive.org/playing.mp3")
    stale = Row("rarch:rec:stale", "Zaznaczone dawniej", "recording",
                url="https://archive.org/stale.mp3")
    copied: list[str] = []
    spoken: list[str] = []
    state = SimpleNamespace(
        view=View.PLAYER, now_playing_id=playing.item_id,
        playback_source_rows=(playing, stale),
        model=SimpleNamespace(selected_row=stale),
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.RADIOARCHIWISTA, view=View.PLAYER,
            sessions={SessionId.RADIOARCHIWISTA: state},
        ),
        _selected_action_rows=lambda: [stale],
        _file_drop_path=lambda _address: None,
        _clear_pending_external_moves=lambda: None,
        _to_clipboard=lambda text, **_kwargs: copied.append(text) or True,
        announcer=SimpleNamespace(say=spoken.append),
    )
    LiteFrame._copy_address(frame)
    assert copied == [playing.url]
    assert spoken == ["Skopiowano link do nagrania"]


def test_open_archive_day_list_relocalizes_through_saved_language_change(
    monkeypatch,
) -> None:
    """Zmiana języka odświeża tylko etykiety już otwartego poziomu dni."""

    class Dialog:
        languages = iter(("en", "pl"))

        def __init__(self, *_args) -> None:
            self.values = {
                "radio_schedule_wake_override": False,
                "shortcut_overrides": {},
                "interface_language": next(self.languages),
                "podcast_automatic_refresh_interval_minutes": 0,
            }

        def ShowModal(self) -> int:  # noqa: N802 - wx API
            return gui.wx.ID_OK

        def Destroy(self) -> None:  # noqa: N802 - wx API
            pass

    class MediaList:
        def __init__(self) -> None:
            self.relocalizations = 0

        def relocalize_interface(self) -> None:
            self.relocalizations += 1

    try:
        i18n.configure("pl")
        navigator = Navigator()
        navigator.active = SessionId.RADIOARCHIWISTA
        navigator.apply_radioarchiwista_rows(
            LibraryView.RADIOARCHIWISTA_DAYS,
            day_rows("pr1", 2026, 10, (15, 13, 14)),
            slug="pr1",
            year=2026,
            month=10,
        )
        archive_state = navigator.sessions[SessionId.RADIOARCHIWISTA]
        archive_state.model.select_id("rarch:days:pr1:2026:10:13")
        archive_state.breadcrumb = [("rarch:years:pr1", "rarch:station:pr1")]
        day_row_ids = tuple(row.item_id for row in archive_state.model.rows)
        other_state = navigator.sessions[SessionId.FILES]
        other_state.model.replace([Row("file:unchanged", "Nie zmieniaj", "track")])
        other_rows = tuple(other_state.model.rows)

        options = Options(interface_language="pl")
        lists = [MediaList() for _ in range(9)]
        source_calls: list[str] = []
        sync_calls: list[str] = []
        frame = SimpleNamespace(
            _radio_snapshot=SimpleNamespace(
                wake_scheduled_recordings=False,
                recording=SimpleNamespace(),
            ),
            state=SimpleNamespace(
                options=options,
                radio_schedule_wake_override=False,
                shortcut_overrides={},
            ),
            options=options,
            podcasts=SimpleNamespace(
                global_settings=lambda: gui.PodcastGlobalSettings(),
            ),
            layout=SimpleNamespace(state_json=Path("unused-state.json")),
            messages=gui.MessagePolicy(language="pl"),
            _global_transient_previews=False,
            _remember_local_playback_positions=True,
            _detailed_hints=True,
            client=None,
            navigator=navigator,
            files_list=lists[0],
            radio_list=lists[1],
            podcasts_list=lists[2],
            tidal_list=lists[3],
            spotify_list=lists[4],
            wiim_list=lists[5],
            frontier_smart_list=lists[6],
            radioarchiwista_list=lists[7],
            sonos_list=lists[8],
            announcer=SimpleNamespace(say=lambda _message: None),
            _save_state=lambda: True,
            _restore_focus_after_dialog=lambda: None,
            _update_window_title=lambda: None,
            _apply_detailed_hints=lambda: None,
            _apply_radio_activity_status=lambda: None,
            # _show_general_playback_options odświeża teraz też prezentacje
            # natywnego odtwarzania; to neutralny stan tej atrapy, nie test
            # silnika ani listy Radio Archiwisty.
            _native_playback_snapshot=lambda _session_id: (False, False),
            _wiim_snapshot={},
            _sonos_snapshot={},
            _build_menu=lambda: None,
            _sync_views=lambda: sync_calls.append("sync"),
            Layout=lambda: None,
            Refresh=lambda: None,
            _radioarchiwista_source=lambda: source_calls.append("source"),
        )
        monkeypatch.setattr(gui, "GeneralPlaybackOptionsDialog", Dialog)
        monkeypatch.setattr(gui, "resolve_keep_audio_edit_backups", lambda *_args: True)
        monkeypatch.setattr(
            LiteFrame,
            "_with_radio_recording_options",
            lambda _self, snapshot: snapshot,
        )
        monkeypatch.setattr(gui.wx, "GetTopLevelWindows", lambda: ())

        LiteFrame._show_general_playback_options(frame)
        assert [row.title for row in archive_state.model.rows] == [
            "15.10.2026, Thursday",
            "13.10.2026, Tuesday",
            "14.10.2026, Wednesday",
        ]
        assert archive_state.model.selected_id == "rarch:days:pr1:2026:10:13"
        assert archive_state.library_view is LibraryView.RADIOARCHIWISTA_DAYS
        assert tuple(row.item_id for row in archive_state.model.rows) == day_row_ids
        assert archive_state.breadcrumb == [("rarch:years:pr1", "rarch:station:pr1")]
        assert tuple(other_state.model.rows) == other_rows
        assert source_calls == []

        LiteFrame._show_general_playback_options(frame)
        assert [row.title for row in archive_state.model.rows] == [
            "15.10.2026, czwartek",
            "13.10.2026, wtorek",
            "14.10.2026, środa",
        ]
        assert archive_state.model.selected_id == "rarch:days:pr1:2026:10:13"
        assert archive_state.library_view is LibraryView.RADIOARCHIWISTA_DAYS
        assert tuple(row.item_id for row in archive_state.model.rows) == day_row_ids
        assert archive_state.breadcrumb == [("rarch:years:pr1", "rarch:station:pr1")]
        assert tuple(other_state.model.rows) == other_rows
        assert source_calls == []
        assert sync_calls == ["sync", "sync"]
        assert all(media_list.relocalizations == 2 for media_list in lists)
    finally:
        i18n.configure("pl")
