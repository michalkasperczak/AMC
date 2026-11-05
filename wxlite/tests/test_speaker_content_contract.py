"""Wspólny kontrakt WiiM/Sonos: lewa strzałka i lokalne ``Ctrl+F``."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.navigation import LibraryView, SessionId, View
from amc_wx_lite.search_catalog import SearchEntry, search
from amc_wx_lite.search_sources import SearchCollection
from amc_wx_lite.speaker_content import (
    speaker_information_failure,
    speaker_information_plan,
    speaker_information_reply,
    speaker_information_text,
    speaker_search_entries,
    speaker_search_scope_label,
)


def test_left_arrow_uses_one_stream_contract_for_both_speakers() -> None:
    cases = [
        (SessionId.WIIM, "wiimNetworkStream", "wiimNetworkStream", "strumień WiiM"),
        (SessionId.SONOS, "sonosOwnStream", "ownStream", "stacja Sonos"),
    ]
    for session, kind, service_kind, expected_kind in cases:
        row = Row(
            item_id=f"technical-{session.value}",
            title="Radio Poznań",
            kind=kind,
            url="https://stream.example/live",
            service_id="private-material-id",
            service_kind=service_kind,
        )
        plan = speaker_information_plan(row, session=session, view="list")

        assert plan.request is not None
        assert plan.request["session"] == "radio", (
            "strumień używa tej samej sondy co Radio"
        )
        assert plan.guard is not None and plan.guard.session == session.value

        spoken: list[str] = []
        speaker_information_reply(
            {"text": "MP3, 128 kb/s, 48 kHz"},
            plan=plan,
            item_id=row.item_id,
            session=session.value,
            view="list",
            say=spoken.append,
        )
        assert spoken == [
            f"MP3, 128 kb/s, 48 kHz, {expected_kind}, "
            "adres: https://stream.example/live"
        ]
        assert "private-material-id" not in spoken[0]
        assert "technical-" not in spoken[0]


def test_left_arrow_describes_read_only_sonos_items_without_inventing_an_address() -> None:
    cases = [
        ("sonosFavorite", "favorite", "ulubione Sonos"),
        ("sonosPlaylist", "playlist", "playlista Sonos"),
    ]
    for kind, service_kind, expected in cases:
        row = Row(
            "sonos:technical",
            "Wieczorne granie",
            kind,
            service_id="technical-material-id",
            service_kind=service_kind,
        )
        text = speaker_information_text(row)
        plan = speaker_information_plan(row, session=SessionId.SONOS, view="list")

        assert text == f"{expected}, adres nieudostępniony przez Sonos"
        assert plan.message == text
        assert plan.request is None
        assert "technical" not in text


def test_late_speaker_information_does_not_follow_focus() -> None:
    for session in (SessionId.WIIM, SessionId.SONOS):
        kind = "wiimNetworkStream" if session is SessionId.WIIM else "sonosOwnStream"
        row = Row("stream-one", "Pierwsza", kind, url="https://example.test/one")
        plan = speaker_information_plan(row, session=session, view="list")
        spoken: list[str] = []

        speaker_information_reply(
            {"text": "AAC, 96 kb/s"},
            plan=plan,
            item_id="stream-two",
            session=session.value,
            view="list",
            say=spoken.append,
        )
        speaker_information_failure(
            plan=plan,
            item_id="stream-two",
            session=session.value,
            view="list",
            say=spoken.append,
        )
        assert spoken == []


def test_loaded_speaker_rows_are_searchable_with_user_facing_types() -> None:
    cases = [
        (
            SessionId.WIIM,
            LibraryView.WIIM_NETWORK_STREAMS,
            "wiimNetworkStream",
            "strumien wiim",
            "strumień WiiM",
        ),
        (
            SessionId.SONOS,
            LibraryView.SONOS_PLAYLISTS,
            "sonosPlaylist",
            "playlista sonos",
            "playlista Sonos",
        ),
    ]
    for session, view, kind, query, expected_kind in cases:
        row = Row(
            "technical-id",
            "Wieczorne granie",
            kind,
            service_id="private-id",
        )
        entries = speaker_search_entries(session, [row], view)

        assert [entry.row.title for entry in search(list(entries), query)] == [
            "Wieczorne granie"
        ]
        assert entries[0].source_view is view
        assert entries[0].kind_label == expected_kind
        assert "technical-id" not in entries[0].accessible_label
        assert "private-id" not in entries[0].accessible_label


def test_ctrl_f_searches_the_loaded_list_without_provider_or_profile_writes() -> None:
    cases = [
        (SessionId.WIIM, LibraryView.WIIM_NETWORK_STREAMS, "wiimNetworkStream"),
        (SessionId.SONOS, LibraryView.SONOS_PLAYLISTS, "sonosPlaylist"),
    ]

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    for session, view, kind in cases:
        shown: list[tuple[list[SearchEntry], tuple[str, ...]]] = []
        row = Row("result", "Wieczorne granie", kind)
        state = SimpleNamespace(model=SimpleNamespace(rows=[row]), library_view=view)
        frame = SimpleNamespace(
            navigator=SimpleNamespace(sessions={session: state}),
            search_sources=SimpleNamespace(
                collect=lambda sessions: SearchCollection((), ())
            ),
            client=SimpleNamespace(),
            runner=Runner(),
            _search_dialog_alive=lambda _dialog: True,
        )

        def failed() -> None:
            raise AssertionError("wyszukiwanie nie powinno zawieść")

        dialog = SimpleNamespace(
            catalog=None,
            show_results=lambda entries, warnings: shown.append((entries, warnings)),
            search_failed=failed,
        )

        LiteFrame._run_search_query(
            frame,
            "wieczorne",
            dialog,
            all_services=False,
            session_before=session,
        )

        assert [entry.row.item_id for entry in shown[0][0]] == ["result"]
        assert shown[0][1] == ()
        assert dialog.catalog is not None
        assert not hasattr(frame.client, "save")


def test_global_search_includes_loaded_rows_from_both_speaker_integrations() -> None:
    shown: list[tuple[list[SearchEntry], tuple[str, ...]]] = []
    sessions = {
        SessionId.WIIM: SimpleNamespace(
            model=SimpleNamespace(rows=[
                Row("wiim-result", "Wspólna stacja WiiM", "wiimNetworkStream")
            ]),
            library_view=LibraryView.WIIM_NETWORK_STREAMS,
        ),
        SessionId.SONOS: SimpleNamespace(
            model=SimpleNamespace(rows=[
                Row("sonos-result", "Wspólna stacja Sonos", "sonosOwnStream")
            ]),
            library_view=LibraryView.SONOS_OWN_STREAMS,
        ),
    }

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed):
            done(work())

    frame = SimpleNamespace(
        navigator=SimpleNamespace(sessions=sessions),
        search_sources=SimpleNamespace(
            collect=lambda _sessions: SearchCollection((), ())
        ),
        client=None,
        runner=Runner(),
        _search_dialog_alive=lambda _dialog: True,
    )
    dialog = SimpleNamespace(
        catalog=None,
        show_results=lambda entries, warnings: shown.append((entries, warnings)),
        search_failed=lambda: None,
    )

    LiteFrame._run_search_query(
        frame,
        "wspólna stacja",
        dialog,
        all_services=True,
        session_before=SessionId.WIIM,
    )

    assert [entry.session_id for entry in shown[0][0]] == [
        SessionId.WIIM,
        SessionId.SONOS,
    ]
    assert {entry.row.item_id for entry in shown[0][0]} == {
        "wiim-result",
        "sonos-result",
    }


def test_enter_on_speaker_search_result_returns_to_the_real_row() -> None:
    cases = [
        (SessionId.WIIM, LibraryView.WIIM_NETWORK_STREAMS, "wiimNetworkStream"),
        (SessionId.SONOS, LibraryView.SONOS_PLAYLISTS, "sonosPlaylist"),
    ]
    for session, view, kind in cases:
        model = ListModel()
        model.replace([
            Row("first", "Pierwsza", kind),
            Row("wanted", "Szukana", kind),
        ])
        state = SimpleNamespace(model=model, library_view=view)
        calls: list[str] = []
        control = SimpleNamespace(
            SetFocus=lambda: calls.append("focus"),
            sync_cursor=lambda: calls.append("cursor"),
        )
        frame = SimpleNamespace(
            _transient_preview_return="old",
            navigator=SimpleNamespace(sessions={session: state}),
            _switch_session=lambda chosen: calls.append(f"session:{chosen.value}"),
            _sync_views=lambda: calls.append("sync"),
            _active_list=lambda: control,
            announcer=SimpleNamespace(say=lambda message: calls.append(message)),
        )
        entry = speaker_search_entries(session, [model.rows[1]], view)[0]

        LiteFrame._open_search_result(frame, entry)

        assert model.selected_id == "wanted"
        assert calls == [f"session:{session.value}", "sync", "focus", "cursor"]
        assert frame._transient_preview_return is None


def test_speaker_search_scope_is_explicitly_local() -> None:
    assert speaker_search_scope_label(SessionId.WIIM) == (
        "WiiM — lokalnie w wczytanej liście"
    )
    assert speaker_search_scope_label(SessionId.SONOS) == (
        "Sonos — lokalnie w wczytanej liście"
    )
