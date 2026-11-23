"""Focused, windowless contracts for AMC video choices and browser handoff."""

from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import patch

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.local_video import local_video_file, local_video_target
from amc_wx_lite.list_model import Row
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import LibraryView, Navigator, PlayTrack, SessionId, View
from amc_wx_lite.podcast_source import external_video_page_url
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.shortcuts import Action, _help_entries, command_presentation


class VideoDisplayContracts(unittest.TestCase):
    def test_downloaded_and_local_video_are_the_same_target(self):
        with tempfile.TemporaryDirectory() as folder:
            video = Path(folder) / "Film.MP4"
            video.write_bytes(b"fixture")
            audio = Path(folder) / "Film.mp3"
            audio.write_bytes(b"fixture")
            self.assertTrue(local_video_file(str(video)))
            self.assertTrue(local_video_target(Row("video", "Film", "track", path=str(video))))
            self.assertFalse(local_video_file(str(audio)))
            self.assertFalse(local_video_file(str(video) + ".missing"))
            self.assertFalse(local_video_target(Row("episode", "Film", "episode", path=str(video))))

    def test_browser_opens_only_exact_public_video_page(self):
        good = Row("yt", "Film", "episode", url="https://www.youtube.com/watch?v=abc123", service_kind="youtubeVideo")
        self.assertEqual(external_video_page_url(good), good.url)
        for address in (
            "https://www.youtube.com/@channel/videos",
            "https://youtube.com.evil.invalid/watch?v=abc123",
            "file:///C:/film.mp4",
            "http://www.youtube.com/watch?v=abc123",
            "https://www.youtube.com/watch?x=v=abc123",
        ):
            with self.subTest(address=address):
                row = Row("yt", "Film", "episode", url=address, service_kind="youtubeVideo")
                self.assertIsNone(external_video_page_url(row))

    def test_menu_palette_and_help_have_the_same_actions(self):
        for session, actions in (
            (SessionId.FILES, {Action.PLAY_LOCAL_AUDIO, Action.PLAY_LOCAL_VIDEO}),
            (SessionId.PODCASTS, {Action.OPEN_VIDEO_IN_BROWSER}),
        ):
            with self.subTest(session=session):
                menu = {item.action for group in menus_for_session(session) for item in group.items}
                palette = {item.action for item in entries_for_session(session)}
                help_actions = {action for _, _, action in _help_entries()}
                self.assertLessEqual(actions, menu)
                self.assertLessEqual(actions, palette)
                self.assertLessEqual(actions, help_actions)

    def test_video_selection_uses_the_existing_navigator_player(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "film.mp4"
            path.write_bytes(b"fixture")
            navigator = Navigator()
            navigator.active = SessionId.FILES
            navigator.apply_library_view(
                LibraryView.DOWNLOADED_FILES, "Pobrane pliki",
                [Row("file:film", "Film", "track", path=str(path))],
            )
            observed = []
            frame = SimpleNamespace(
                navigator=navigator,
                _local_video_target_path=lambda: str(path),
                _run=lambda intents: observed.extend(intents),
                announcer=SimpleNamespace(say=lambda message: observed.append(message)),
            )
            LiteFrame._play_local_video_mode(frame, True)
            self.assertTrue(frame._video_mode_requested)
            self.assertIs(navigator.session.view, View.PLAYER)
            self.assertTrue(any(isinstance(intent, PlayTrack) for intent in observed))

    def test_video_surface_follows_host_status_and_hides_in_audio_mode(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "film.mp4"
            path.write_bytes(b"fixture")
            navigator = Navigator()
            navigator.active = SessionId.FILES
            state = navigator.session
            state.view = View.PLAYER
            state.now_playing_id = "file:film"
            shown = []
            surface = SimpleNamespace(
                control=SimpleNamespace(IsShown=lambda: True),
                present=lambda source, status: shown.append((source, status)),
                hide=lambda: shown.append("hidden"),
            )
            status = {
                "id": "file:film", "source": str(path),
                "playing": True, "paused": False, "positionSeconds": 3,
            }
            frame = SimpleNamespace(
                navigator=navigator,
                _video_mode_requested=True,
                _video_surface=surface,
                _transport_status_by_session={SessionId.FILES: status},
                player_panel=SimpleNamespace(Layout=lambda: None),
            )
            LiteFrame._sync_embedded_video(frame, status)
            self.assertEqual(shown, [(str(path), status)])
            frame._video_mode_requested = False
            LiteFrame._sync_embedded_video(frame, status)
            self.assertEqual(shown[-1], "hidden")

    def test_command_labels_descriptions_and_shortcuts_are_shared(self):
        for session, action in (
            (SessionId.FILES, Action.PLAY_LOCAL_AUDIO),
            (SessionId.FILES, Action.PLAY_LOCAL_VIDEO),
            (SessionId.PODCASTS, Action.OPEN_VIDEO_IN_BROWSER),
        ):
            with self.subTest(action=action):
                shared = command_presentation(action)
                menu = next(item for group in menus_for_session(session)
                            for item in group.items if item.action is action)
                palette = next(item for item in entries_for_session(session)
                               if item.action is action)
                help_entry = next(item for item in _help_entries() if item[2] is action)
                self.assertEqual(menu.label.replace("&", ""), shared.name)
                self.assertEqual(palette.name, shared.name)
                self.assertEqual(help_entry[1], shared.description)
                self.assertEqual(help_entry[0], "Bez skrótu")
                self.assertIsNone(palette.shortcut)

    def test_browser_action_opens_selected_page_only(self):
        row = Row("yt", "Film", "episode",
                  url="https://www.youtube.com/watch?v=abc123",
                  service_kind="youtubeVideo")
        messages = []
        frame = SimpleNamespace(
            navigator=SimpleNamespace(
                active=SessionId.PODCASTS,
                session=SimpleNamespace(view=View.LIST,
                                        model=SimpleNamespace(selected_row=row)),
            ),
            announcer=SimpleNamespace(say=messages.append),
        )
        with patch("amc_wx_lite.gui.wx.LaunchDefaultBrowser", return_value=True) as launch:
            LiteFrame._open_video_in_browser(frame)
            launch.assert_called_once_with(row.url)
        self.assertEqual(messages, [])
        frame.navigator.session.model.selected_row = Row("bad", "Kanał", "episode",
            url="https://www.youtube.com/@channel/videos", service_kind="youtubeVideo")
        with patch("amc_wx_lite.gui.wx.LaunchDefaultBrowser") as launch:
            LiteFrame._open_video_in_browser(frame)
            launch.assert_not_called()
        self.assertEqual(messages, ["Wybierz film internetowy z poprawnym adresem"])

    def test_video_unavailable_falls_back_to_audio_and_announces_it(self):
        messages = []
        hidden = []
        frame = SimpleNamespace(
            _video_mode_requested=True,
            _video_surface=SimpleNamespace(hide=lambda: hidden.append(True)),
            player_panel=SimpleNamespace(Layout=lambda: None),
            announcer=SimpleNamespace(say=messages.append),
        )
        LiteFrame._video_unavailable(frame)
        self.assertFalse(frame._video_mode_requested)
        self.assertEqual(hidden, [True])
        self.assertEqual(messages, [
            "Nie można wyświetlić obrazu tego pliku. Dźwięk pozostaje w AMC"
        ])

    def test_new_actions_route_to_real_handlers(self):
        calls = []
        frame = SimpleNamespace(
            navigator=SimpleNamespace(active=SessionId.FILES),
            messages=SimpleNamespace(custom_seek_seconds=10),
            _play_local_video_mode=lambda show: calls.append(show),
            _open_video_in_browser=lambda: calls.append("browser"),
        )
        LiteFrame._dispatch(frame, Action.PLAY_LOCAL_VIDEO)
        LiteFrame._dispatch(frame, Action.PLAY_LOCAL_AUDIO)
        frame.navigator.active = SessionId.PODCASTS
        LiteFrame._dispatch(frame, Action.OPEN_VIDEO_IN_BROWSER)
        self.assertEqual(calls, [True, False, "browser"])

    def test_new_actions_follow_current_selection(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "film.mp4"
            path.write_bytes(b"fixture")
            row = Row("film", "Film", "track", path=str(path))
            navigator = SimpleNamespace(
                active=SessionId.FILES,
                session=SimpleNamespace(view=View.LIST,
                    model=SimpleNamespace(selected_row=row)),
            )
            frame = SimpleNamespace(
                navigator=navigator,
                _local_video_target_path=lambda: str(path)
                    if local_video_target(navigator.session.model.selected_row) else None,
            )
            for action in (Action.PLAY_LOCAL_VIDEO, Action.PLAY_LOCAL_AUDIO):
                self.assertTrue(LiteFrame._action_available_in_active_session(frame, action))
            navigator.session.model.selected_row = Row("audio", "Audio", "track",
                                                       path=str(Path(folder) / "audio.mp3"))
            for action in (Action.PLAY_LOCAL_VIDEO, Action.PLAY_LOCAL_AUDIO):
                self.assertFalse(LiteFrame._action_available_in_active_session(frame, action))
            navigator.active = SessionId.PODCASTS
            navigator.session.model.selected_row = Row("yt", "Film", "episode",
                url="https://www.youtube.com/watch?v=abc123", service_kind="youtubeVideo")
            self.assertTrue(LiteFrame._action_available_in_active_session(
                frame, Action.OPEN_VIDEO_IN_BROWSER))
            navigator.session.model.selected_row = Row("yt", "Film", "episode",
                url="https://www.youtube.com/@channel/videos", service_kind="youtubeVideo")
            self.assertFalse(LiteFrame._action_available_in_active_session(
                frame, Action.OPEN_VIDEO_IN_BROWSER))

    def test_video_commands_are_test_only_across_menu_palette_and_execution(self):
        video_actions = {
            Action.PLAY_LOCAL_VIDEO, Action.PLAY_LOCAL_AUDIO,
            Action.OPEN_VIDEO_IN_BROWSER,
        }
        for profile, expected in ((TEST_PROFILE, True), (PUBLIC_PROFILE, False)):
            with self.subTest(profile=profile.profile_id), patch(
                "amc_wx_lite.menu_model.current_profile", return_value=profile
            ), patch("amc_wx_lite.gui.current_profile", return_value=profile):
                actions = {
                    item.action
                    for session in (SessionId.FILES, SessionId.PODCASTS)
                    for menu in menus_for_session(session)
                    for item in menu.items
                }
                palette_actions = {
                    item.action
                    for session in (SessionId.FILES, SessionId.PODCASTS)
                    for item in entries_for_session(session)
                }
                self.assertEqual(video_actions <= actions, expected)
                self.assertEqual(video_actions <= palette_actions, expected)
                frame = SimpleNamespace(_local_video_target_path=lambda: "film.mp4")
                self.assertEqual(LiteFrame._action_available_in_active_session(
                    frame, Action.PLAY_LOCAL_VIDEO), expected)


if __name__ == "__main__":
    unittest.main()
