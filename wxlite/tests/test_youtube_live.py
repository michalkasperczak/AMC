from __future__ import annotations

import unittest
from types import SimpleNamespace

from amc_wx_lite import menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.navigation import (
    LibraryView,
    Navigator,
    OpenPodcastView,
    OpenYouTubeBroadcasts,
    PlayMedia,
    SessionId,
)
from amc_wx_lite.shortcuts import Action, Chord, describe, describe_action, resolve
from amc_wx_lite.youtube_live import (
    YouTubeLivePayloadError,
    aggregate_broadcast_rows,
    broadcast_rows,
    dynamic_live_url,
)


class YouTubeLiveTests(unittest.TestCase):
    def test_aggregate_scan_blocks_duplicate_requests_and_releases_guard(self) -> None:
        messages: list[str] = []
        submitted: list[tuple] = []

        class Runner:
            def submit(self, *request) -> None:
                submitted.append(request)

        podcast_state = SimpleNamespace(
            library_view=LibraryView.YOUTUBE_BROADCASTS_AGGREGATE,
            library_playlist_id=None,
            library_return_view="",
            model=SimpleNamespace(selected_id=None),
            view=None,
        )
        frame = SimpleNamespace(
            _youtube_live_scan_pending=False,
            _youtube_live_scan_version=0,
            _youtube_account_available=True,
            _youtube_live_channels_by_id={},
            announcer=SimpleNamespace(say=messages.append),
            client=object(),
            state=SimpleNamespace(youtube_live_channel_ids=["channel-one"]),
            navigator=SimpleNamespace(
                active=SessionId.PODCASTS,
                session=podcast_state,
                sessions={SessionId.PODCASTS: podcast_state},
            ),
            runner=Runner(),
            _sync_views=lambda: None,
        )

        LiteFrame._show_youtube_live_aggregate(frame)
        self.assertTrue(frame._youtube_live_scan_pending)
        self.assertEqual(len(submitted), 1)

        LiteFrame._show_youtube_live_aggregate(frame, force_refresh=True)
        self.assertEqual(len(submitted), 1)
        self.assertEqual(
            messages[-1],
            "Sprawdzanie transmisji wybranych kanałów YouTube już trwa",
        )

        # Zmiana widoku przed końcem nie może pozostawić trwałej blokady.
        podcast_state.library_view = LibraryView.PODCAST_LIBRARY
        submitted[0][2](([], [], [], 0))
        self.assertFalse(frame._youtube_live_scan_pending)

        podcast_state.library_view = LibraryView.YOUTUBE_BROADCASTS_AGGREGATE
        LiteFrame._show_youtube_live_aggregate(frame)
        self.assertTrue(frame._youtube_live_scan_pending)
        submitted[1][3](RuntimeError("awaria"))
        self.assertFalse(frame._youtube_live_scan_pending)
        self.assertEqual(messages[-1], "Nie udało się sprawdzić transmisji YouTube")

    def test_dynamic_station_accepts_channel_id_and_handle(self) -> None:
        self.assertEqual(
            dynamic_live_url("https://www.youtube.com/channel/UC_test-123/videos"),
            "https://www.youtube.com/channel/UC_test-123/live",
        )
        self.assertEqual(
            dynamic_live_url("https://youtube.com/%40Parafia/videos"),
            "https://www.youtube.com/@Parafia/live",
        )

    def test_dynamic_station_rejects_video_and_foreign_hosts(self) -> None:
        for address in (
            "https://www.youtube.com/watch?v=abc",
            "https://youtube.com.example.org/channel/UC_test/videos",
        ):
            with self.subTest(address=address):
                with self.assertRaises(YouTubeLivePayloadError):
                    dynamic_live_url(address)

    def test_rows_expose_only_intentional_labels_and_public_addresses(self) -> None:
        rows = broadcast_rows(
            {
                "items": [
                    {
                        "title": "  Msza\r\nna żywo ",
                        "channelTitle": "Parafia",
                        "pageUrl": "https://www.youtube.com/watch?v=PublicznyAdres",
                        "state": "live",
                        "actualStartUtc": "2026-10-23T16:00:00Z",
                        "technicalId": "NIE-WOLNO-MOWIC",
                    },
                    {
                        "title": "Msza wieczorna",
                        "pageUrl": "https://www.youtube.com/watch?v=Zaplanowana",
                        "state": "upcoming",
                        "scheduledStartUtc": "2026-10-25T17:00:00Z",
                    },
                ]
            },
            subscription_id="wewnetrzny-rodzic",
        )
        self.assertEqual([row.title for row in rows], ["Msza na żywo", "Msza wieczorna"])
        self.assertEqual([row.service_kind for row in rows], ["youtubeLive", "youtubeUpcoming"])
        self.assertTrue(rows[0].detail.startswith("na żywo"))
        self.assertTrue(rows[1].detail.startswith("zaplanowana"))
        self.assertEqual(rows[0].path, rows[0].url)
        self.assertEqual(rows[0].parent_id, "wewnetrzny-rodzic")
        spoken = " ".join(ListModel(rows).text_for(0, column) for column in range(3))
        self.assertNotIn("NIE-WOLNO-MOWIC", spoken)

    def test_navigation_opens_refreshes_plays_and_returns_to_channel(self) -> None:
        navigator = Navigator()
        navigator.switch_session(SessionId.PODCASTS)
        navigator.apply_podcast_library([
            Row(
                "subscription-one",
                "Parafia św. Ignacego",
                "podcast",
                url="https://www.youtube.com/channel/UC_test/videos",
                service_kind="youtubeChannel",
            )
        ])

        request = navigator.open_youtube_broadcasts()[0]
        self.assertIsInstance(request, OpenYouTubeBroadcasts)
        self.assertEqual(request.subscription_id, "subscription-one")
        rows = broadcast_rows(
            {"items": [{
                "title": "Msza",
                "pageUrl": "https://www.youtube.com/watch?v=MszaLive",
                "state": "live",
            }]},
            subscription_id="subscription-one",
        )
        announcement = navigator.apply_youtube_broadcasts(
            "subscription-one", "Parafia św. Ignacego", rows
        )[0]
        self.assertEqual(navigator.session.library_view, LibraryView.YOUTUBE_BROADCASTS)
        self.assertIn("na żywo 1", announcement.text)

        refresh = navigator.open_youtube_broadcasts(force_refresh=True)[0]
        self.assertTrue(refresh.force_refresh)
        play = next(
            item for item in navigator.activate_selected() if isinstance(item, PlayMedia)
        )
        self.assertEqual(play.source, "https://www.youtube.com/watch?v=MszaLive")
        navigator.back_to_list()
        back = navigator.go_to_parent()[0]
        self.assertIsInstance(back, OpenPodcastView)
        self.assertEqual(back.preferred_id, "subscription-one")

    def test_aggregate_labels_sort_and_navigation_hide_channel_ids(self) -> None:
        rows = aggregate_broadcast_rows([
            (
                "wewnetrzny-kanal-b",
                "Kanał B",
                {"items": [{
                    "title": "Jutro",
                    "pageUrl": "https://www.youtube.com/watch?v=jutro",
                    "state": "upcoming",
                    "scheduledStartUtc": "2026-10-26T10:00:00Z",
                }]},
            ),
            (
                "wewnetrzny-kanal-a",
                "Kanał A",
                {"items": [
                    {
                        "title": "Teraz",
                        "pageUrl": "https://www.youtube.com/watch?v=teraz",
                        "state": "live",
                        "actualStartUtc": "2026-10-24T10:00:00Z",
                    },
                    {
                        "title": "Wcześniej",
                        "pageUrl": "https://www.youtube.com/watch?v=wczesniej",
                        "state": "upcoming",
                        "scheduledStartUtc": "2026-10-25T10:00:00Z",
                    },
                ]},
            ),
        ])
        self.assertEqual([row.title for row in rows], ["Teraz", "Wcześniej", "Jutro"])
        self.assertTrue(rows[0].detail.startswith("Kanał A, na żywo"))
        self.assertIsNotNone(rows[1].service_start_utc)
        spoken = " ".join(
            ListModel(rows).text_for(index, column)
            for index in range(len(rows))
            for column in range(3)
        )
        self.assertNotIn("wewnetrzny-kanal", spoken)

        navigator = Navigator()
        events = navigator.apply_youtube_broadcasts_aggregate(
            rows, checked_channels=2
        )
        self.assertEqual(
            navigator.sessions[SessionId.PODCASTS].library_view,
            LibraryView.YOUTUBE_BROADCASTS_AGGREGATE,
        )
        self.assertIn("na żywo 1", events[0].text)

    def test_f5_and_command_surfaces_share_one_contract(self) -> None:
        self.assertIs(
            resolve(
                Chord("T", ctrl=True),
                player_view=False,
                radio_session=False,
                podcast_session=True,
                youtube_channel=True,
                youtube_account_available=True,
            ),
            Action.YOUTUBE_BROADCASTS,
        )
        self.assertIs(
            resolve(
                Chord("Y", ctrl=True),
                player_view=False,
                radio_session=False,
                podcast_session=True,
                youtube_channel=True,
            ),
            Action.ADD_YOUTUBE_LIVE_STATION,
        )
        self.assertIsNone(
            resolve(
                Chord("T", ctrl=True),
                player_view=False,
                radio_session=False,
                podcast_session=True,
                youtube_channel=True,
                youtube_account_available=False,
            )
        )
        self.assertIsNone(
            resolve(
                Chord("Y", ctrl=True),
                player_view=False,
                radio_session=False,
                podcast_session=True,
                youtube_channel=False,
            )
        )
        self.assertIs(
            resolve(
                Chord("F5"),
                player_view=False,
                radio_session=False,
                podcast_session=True,
                youtube_broadcasts=True,
            ),
            Action.REFRESH_YOUTUBE_BROADCASTS,
        )
        self.assertIs(
            resolve(
                Chord("F5"),
                player_view=False,
                radio_session=False,
                podcast_session=True,
            ),
            Action.REFRESH_PODCAST,
        )
        actions = {
            Action.YOUTUBE_BROADCASTS,
            Action.REFRESH_YOUTUBE_BROADCASTS,
            Action.ADD_YOUTUBE_LIVE_STATION,
            Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
            Action.CONFIGURE_YOUTUBE_LIVE_CHANNELS,
            Action.TOGGLE_YOUTUBE_LIVE_AGGREGATE_CHANNEL,
            Action.REFRESH_YOUTUBE_LIVE_AGGREGATE,
            Action.RECORD_YOUTUBE_LIVE_NOW,
            Action.SCHEDULE_YOUTUBE_LIVE,
        }
        menu_items = {
            item.action: item
            for menu in menu_model.menus_for_session(
                SessionId.PODCASTS,
                youtube_account_available=True,
            )
            for item in menu.items
            if item.action in actions
        }
        palette = {
            entry.action: entry
            for entry in entries_for_session(
                SessionId.PODCASTS,
                youtube_account_available=True,
            )
            if entry.action in actions
        }
        self.assertEqual(set(menu_items), actions)
        self.assertEqual(set(palette), actions)
        self.assertEqual(
            menu_items[Action.REFRESH_YOUTUBE_BROADCASTS].shortcut,
            palette[Action.REFRESH_YOUTUBE_BROADCASTS].shortcut,
        )
        self.assertEqual(
            menu_items[Action.YOUTUBE_BROADCASTS].shortcut,
            palette[Action.YOUTUBE_BROADCASTS].shortcut,
        )
        self.assertEqual(
            menu_items[Action.ADD_YOUTUBE_LIVE_STATION].shortcut,
            palette[Action.ADD_YOUTUBE_LIVE_STATION].shortcut,
        )
        self.assertEqual(menu_items[Action.YOUTUBE_BROADCASTS].shortcut, "Ctrl+T")
        self.assertEqual(menu_items[Action.ADD_YOUTUBE_LIVE_STATION].shortcut, "Ctrl+Y")
        self.assertEqual(
            menu_items[Action.VIEW_YOUTUBE_LIVE_AGGREGATE].shortcut,
            "Ctrl+Shift+I",
        )
        self.assertIs(
            resolve(
                Chord("I", ctrl=True, shift=True),
                player_view=False,
                radio_session=False,
                youtube_account_available=True,
            ),
            Action.VIEW_YOUTUBE_LIVE_AGGREGATE,
        )
        for chord, action in (
            (Chord("F5"), Action.REFRESH_YOUTUBE_LIVE_AGGREGATE),
            (Chord("R", ctrl=True), Action.RECORD_YOUTUBE_LIVE_NOW),
            (Chord("R", shift=True), Action.SCHEDULE_YOUTUBE_LIVE),
        ):
            self.assertIs(
                resolve(
                    chord,
                    player_view=False,
                    radio_session=False,
                    podcast_session=True,
                    youtube_broadcasts_aggregate=True,
                    youtube_account_available=True,
                ),
                action,
            )
        help_entries = dict(describe("Podcasty i YouTube", actions))
        self.assertEqual(
            help_entries["Ctrl+T"],
            "Pokaż transmisje na żywo i zaplanowane kanału YouTube",
        )
        self.assertEqual(
            help_entries["Ctrl+Y"],
            "Dodaj kanał jako dynamiczną stację YouTube na żywo",
        )
        for action in actions:
            self.assertNotIn("Action.", palette[action].label)
            self.assertNotIn("{", palette[action].label)
            self.assertTrue(describe_action(action))


if __name__ == "__main__":
    unittest.main()


def test_radio_broadcast_choice_prefers_live_then_nearest_upcoming() -> None:
    from datetime import datetime, timezone
    from amc_wx_lite.youtube_live import current_or_next_broadcast, is_youtube_station_url

    now = datetime(2026, 11, 11, 17, 0, tzinfo=timezone.utc)
    upcoming = {"title": "Później", "pageUrl": "https://www.youtube.com/watch?v=later",
                "state": "upcoming", "scheduledStartUtc": "2026-11-11T19:00:00Z"}
    nearest = {"title": "Najbliżej", "pageUrl": "https://www.youtube.com/watch?v=near",
               "state": "upcoming", "scheduledStartUtc": "2026-11-11T18:00:00Z"}
    live = {"title": "Teraz", "pageUrl": "https://www.youtube.com/watch?v=live",
            "state": "live", "actualStartUtc": "2026-11-11T16:30:00Z"}
    assert current_or_next_broadcast({"items": [upcoming, nearest]}, now=now).title == "Najbliżej"
    assert current_or_next_broadcast({"items": [upcoming, live, nearest]}, now=now).title == "Teraz"
    assert current_or_next_broadcast({"items": [{**nearest, "scheduledStartUtc": "2026-11-10T18:00:00Z"}]}, now=now) is None
    assert is_youtube_station_url("https://www.youtube.com/watch?v=old")
    assert is_youtube_station_url("https://www.youtube.com/@channel/live")
    assert not is_youtube_station_url("https://youtube.com.evil.example/watch?v=old")


def test_radio_youtube_update_is_in_menu_palette_and_help() -> None:
    action = Action.REFRESH_YOUTUBE_RADIO_STATION
    radio_menu = {
        item.action: item
        for menu in menu_model.menus_for_session(
            SessionId.RADIO, youtube_account_available=True
        )
        for item in menu.items
    }
    radio_palette = {
        entry.action: entry
        for entry in entries_for_session(
            SessionId.RADIO, youtube_account_available=True
        )
    }
    assert action in radio_menu and action in radio_palette
    assert radio_menu[action].needs_youtube_radio_station
    assert radio_menu[action].shortcut is None
    assert "transmisję" in describe_action(action, "Radio")
    assert action not in {
        item.action
        for menu in menu_model.menus_for_session(
            SessionId.RADIO, youtube_account_available=False
        )
        for item in menu.items
    }


def test_radio_youtube_update_replaces_station_and_linked_plans() -> None:
    from amc_wx_lite.state_store import Station

    station = Station(
        id="kramer", name="Kramer",
        url="https://www.youtube.com/watch?v=old",
    )
    calls = []
    spoken = []
    client = SimpleNamespace(
        youtube_radio_broadcasts=lambda url, subscription_id=None: (
            calls.append(("read", url, subscription_id))
            or {"items": [{
                "title": "Następna transmisja",
                "pageUrl": "https://www.youtube.com/watch?v=new",
                "state": "upcoming",
                "scheduledStartUtc": "2099-01-01T18:00:00Z",
            }]}
        ),
        edit_radio_station=lambda station_id, name, url: calls.append(
            ("edit", station_id, name, url)
        ),
    )

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = SimpleNamespace(
        _selected_station=lambda: station,
        _youtube_account_available=True,
        _youtube_channel_subscriptions=lambda: [],
        radio=SimpleNamespace(may_edit=False),
        client=client,
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
        _update_private_radio_schedules_for_station=lambda station_id, name, url: (
            calls.append(("plans", station_id, name, url)) or True
        ),
        _refresh_profile_view=lambda **kwargs: calls.append(("view", kwargs)),
    )
    LiteFrame._refresh_selected_youtube_radio_station(frame)
    assert calls[0] == ("read", station.url, None)
    assert calls[1] == ("edit", "kramer", "Kramer", "https://www.youtube.com/watch?v=new")
    assert calls[2] == ("plans", "kramer", "Kramer", "https://www.youtube.com/watch?v=new")
    assert spoken[-1] == "Zapisano najbliższą zaplanowaną transmisję: Następna transmisja"


def test_radio_youtube_command_language_parity() -> None:
    from amc_wx_lite import i18n

    action = Action.REFRESH_YOUTUBE_RADIO_STATION
    try:
        for language in ("pl", "en"):
            i18n.configure(language)
            item = next(
                item for menu in menu_model.menus_for_session(
                    SessionId.RADIO, youtube_account_available=True
                ) for item in menu.items if item.action is action
            )
            palette = next(
                entry for entry in entries_for_session(
                    SessionId.RADIO, youtube_account_available=True
                ) if entry.action is action
            )
            assert item.label.replace("&", "") == palette.name
            assert item.shortcut == palette.shortcut is None
            context = LiteFrame._context_menu_entry_label(
                SimpleNamespace(
                    navigator=SimpleNamespace(
                        session=SimpleNamespace(
                            model=SimpleNamespace(selected_row=None)
                        )
                    ),
                    _in_download_history=lambda: False,
                    _without_menu_mnemonic=LiteFrame._without_menu_mnemonic,
                ),
                item,
                item.label,
            )
            assert context == palette.name
            help_entry = dict(describe("Radio", {action}))["Bez skrótu"]
            assert help_entry
            if language == "en":
                assert describe_action(action, "Radio") == i18n.english_action_name(action.value)
                assert i18n.tr("radio.youtube.updatedUpcoming", title="Test") == (
                    "Saved the next scheduled broadcast: Test"
                )
            else:
                assert describe_action(action, "Radio").startswith("Wybierz trwającą")
                assert i18n.tr("radio.youtube.updatedUpcoming", title="Próba") == (
                    "Zapisano najbliższą zaplanowaną transmisję: Próba"
                )
    finally:
        i18n.configure("pl")
