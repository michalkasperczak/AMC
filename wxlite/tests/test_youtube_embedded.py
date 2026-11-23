"""Focused contracts for official YouTube playback in the Podcast player."""

from types import SimpleNamespace
import unittest
from unittest.mock import patch

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, PlayMedia, SessionId, View
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.shortcuts import Action, command_presentation
from amc_wx_lite.youtube_embedded import youtube_video_id, _player_html, virtual_host_for_profile


URL = "https://www.youtube.com/watch?v=M7lc1UVf-VE"


class YouTubeEmbeddedContracts(unittest.TestCase):
    def test_exact_youtube_video_urls_only(self):
        for address in (
            URL,
            "https://youtu.be/M7lc1UVf-VE",
            "https://youtube.com/shorts/M7lc1UVf-VE",
            "https://www.youtube.com/live/M7lc1UVf-VE",
        ):
            with self.subTest(address=address):
                self.assertEqual(youtube_video_id(address), "M7lc1UVf-VE")
        for address in (
            "http://www.youtube.com/watch?v=M7lc1UVf-VE",
            "https://youtube.com.evil.invalid/watch?v=M7lc1UVf-VE",
            "https://www.youtube.com/watch?v=M7lc1UVf-VE&v=aaaaaaaaaaa",
            "https://www.youtube.com/@channel/videos",
            "https://www.youtube.com/watch?v=<script>",
            "https://www.youtube.com:444/watch?v=M7lc1UVf-VE",
        ):
            with self.subTest(address=address):
                self.assertIsNone(youtube_video_id(address))

    def test_iframe_keeps_original_controls_and_no_autoplay_parameter(self):
        html = _player_html("M7lc1UVf-VE")
        self.assertIn("https://www.youtube.com/iframe_api", html)
        self.assertIn("autoplay: 0, controls: 1", html)
        self.assertIn("strict-origin-when-cross-origin", html)
        self.assertNotIn("player.playVideo()", html)

    def test_online_film_uses_existing_podcast_player_without_media_path(self):
        navigator = Navigator()
        navigator.active = SessionId.PODCASTS
        navigator.apply_podcast_aggregate(
            LibraryView.PODCAST_HISTORY, "Historia podcastów",
            [Row("film", "Film YouTube", "episode", url=URL,
                 service_kind="youtubeVideo")],
        )
        observed = []
        frame = SimpleNamespace(
            navigator=navigator,
            _local_video_target_path=lambda: None,
            _online_youtube_target=lambda: LiteFrame._online_youtube_target(frame),
            _run=lambda intents: observed.extend(intents),
            announcer=SimpleNamespace(say=observed.append),
        )
        with patch("amc_wx_lite.gui.current_profile", return_value=TEST_PROFILE):
            self.assertTrue(LiteFrame._action_available_in_active_session(
                frame, Action.PLAY_LOCAL_VIDEO
            ))
            self.assertFalse(LiteFrame._action_available_in_active_session(
                frame, Action.PLAY_LOCAL_AUDIO
            ))
            LiteFrame._play_local_video_mode(frame, True)
        self.assertEqual(frame._youtube_pending, ("film", "M7lc1UVf-VE"))
        self.assertIs(navigator.session.view, View.PLAYER)
        self.assertTrue(any(
            isinstance(intent, PlayMedia) and intent.item_id == "film"
            for intent in observed
        ))
        self.assertIn("YouTube online", command_presentation(
            Action.PLAY_LOCAL_VIDEO
        ).description)

    def test_public_profile_exposes_online_video_with_separate_origin(self):
        self.assertNotEqual(virtual_host_for_profile("test"), virtual_host_for_profile("public"))
        self.assertIn("1e437448", virtual_host_for_profile("public"))
        navigator = Navigator()
        navigator.active = SessionId.PODCASTS
        navigator.apply_podcast_aggregate(
            LibraryView.PODCAST_HISTORY, "Historia podcastów",
            [Row("film", "Film YouTube", "episode", url=URL,
                 service_kind="youtubeVideo")],
        )
        frame = SimpleNamespace(
            navigator=navigator,
            _local_video_target_path=lambda: None,
            _online_youtube_target=lambda: LiteFrame._online_youtube_target(frame),
        )
        with patch("amc_wx_lite.gui.current_profile", return_value=PUBLIC_PROFILE):
            self.assertTrue(LiteFrame._action_available_in_active_session(
                frame, Action.PLAY_LOCAL_VIDEO
            ))

    def test_audio_host_timeline_commands_are_unavailable_during_online_video(self):
        spoken = []
        frame = SimpleNamespace(
            _youtube_playback_active=lambda: True,
            announcer=SimpleNamespace(say=spoken.append),
        )
        for action in (
            Action.ADD_BOOKMARK, Action.NEXT_BOOKMARK,
            Action.VIEW_CHAPTERS, Action.PREVIOUS_CHAPTER,
            Action.CLIP_EXPORT, Action.PLAYBACK_HISTORY_OLDER,
        ):
            with self.subTest(action=action):
                self.assertFalse(LiteFrame._action_available_in_active_session(
                    frame, action
                ))
                LiteFrame._dispatch(frame, action)
        self.assertEqual(len(spoken), 6)
        self.assertTrue(all("YouTube online" in message for message in spoken))


if __name__ == "__main__":
    unittest.main()
