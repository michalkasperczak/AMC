from __future__ import annotations

from types import SimpleNamespace

from amc_wx_lite import i18n, menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, command_presentation, describe
from amc_wx_lite.youtube_variants import (
    can_choose_variant,
    is_youtube_source_row,
    read_variants,
)


def _offer():
    return {"variants": [
        {"selector": "137+140", "signature": "opaque-signature",
         "kind": "videoAudio", "extension": "mkv", "height": 1080,
         "videoCodec": "avc1.640028", "audioCodec": "mp4a.40.2",
         "sizeBytes": 110_000_000, "sizeApproximate": True,
         "needsMerge": True},
        {"selector": "140", "signature": "audio-signature",
         "kind": "audioOnly", "extension": "m4a", "audioCodec": "mp4a.40.2",
         "bitrateKbps": 128, "sizeBytes": None},
        {"selector": "137", "signature": "silent-signature",
         "kind": "videoOnly", "extension": "mp4", "height": 1080,
         "videoCodec": "avc1.640028"},
    ]}


def test_spoken_variants_describe_result_without_identifiers() -> None:
    choices = read_variants(_offer())
    assert len(choices) == 3
    assert "Wideo z dźwiękiem" in choices[0].label
    assert "1080p" in choices[0].label
    assert "H.264" in choices[0].label
    assert "AAC" in choices[0].label
    assert "około 110 MB" in choices[0].label
    assert "bez przekodowania" in choices[0].label
    assert "Tylko dźwięk" in choices[1].label
    assert "Wideo bez dźwięku" in choices[2].label
    for choice in choices:
        assert choice.selector not in choice.label
        assert choice.signature not in choice.label
    assert read_variants({"variants": [*_offer()["variants"], _offer()["variants"][0]]}) == choices


def test_english_labels_stay_user_facing() -> None:
    previous = i18n.active_language()
    try:
        i18n.configure(i18n.ENGLISH)
        labels = [choice.label for choice in read_variants(_offer())]
        assert "Video with audio" in labels[0]
        assert "Audio only" in labels[1]
        assert "Video without audio" in labels[2]
        assert "137+140" not in labels[0]
    finally:
        i18n.configure(previous)


def test_only_youtube_episode_exposes_exact_variant_choice() -> None:
    assert is_youtube_source_row(Row(
        "one", "Film", "episode", url="https://www.youtube.com/watch?v=abc"))
    assert is_youtube_source_row(Row(
        "one", "Film", "episode", url="https://youtu.be/abc"))
    assert not is_youtube_source_row(Row(
        "two", "Film", "episode", url="https://facebook.com/reel/123"))
    assert not is_youtube_source_row(Row(
        "three", "Film", "episode", url="https://youtube.com.evil.test/watch?v=abc"))
    assert not is_youtube_source_row(Row(
        "four", "Kanał", "podcast", url="https://youtube.com/watch?v=abc"))
    youtube = Row("one", "Film", "episode", url="https://youtube.com/watch?v=abc")
    social = Row("two", "Film", "episode", url="https://facebook.com/reel/123")
    assert can_choose_variant([youtube])
    assert not can_choose_variant([])
    assert not can_choose_variant([youtube, youtube])
    assert not can_choose_variant([social])


def test_host_client_keeps_exact_selection_separate_from_simple_download() -> None:
    client = object.__new__(LiteHostClient)
    seen = []

    def call(op, args=None, *, timeout=20.0):
        seen.append((op, args, timeout))
        return {}

    client.call = call
    client.youtube_video_variants("one")
    client.save_youtube_variant_as("one", r"D:\Film.mkv", "137+140", "signature")
    assert seen[0][:2] == ("podcast.videoVariants", {"episodeId": "one"})
    assert seen[1][:2] == (
        "podcast.videoVariantSaveAs",
        {"episodeId": "one", "path": r"D:\Film.mkv",
         "selector": "137+140", "signature": "signature"},
    )


def test_menu_palette_and_full_help_use_one_command_name() -> None:
    action = Action.SELECT_YOUTUBE_VARIANT
    for language in (i18n.POLISH, i18n.ENGLISH):
        previous = i18n.active_language()
        try:
            i18n.configure(language)
            menu = next(item for group in menu_model.menus_for_session(SessionId.PODCASTS)
                        for item in group.items if item.action is action)
            palette = next(item for item in entries_for_session(SessionId.PODCASTS)
                           if item.action is action)
            presentation = command_presentation(action)
            assert presentation is not None
            assert menu.label.replace("&", "") == palette.name == presentation.name
            assert menu.shortcut is None and palette.shortcut is None
            assert menu.needs_single_youtube_item and menu.needs_selection
            assert ("Bez skrótu", presentation.description) in describe(
                "Podcasty i YouTube", {action})
        finally:
            i18n.configure(previous)


def test_selected_command_reaches_the_variant_flow() -> None:
    from amc_wx_lite.gui import LiteFrame

    called = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.PODCASTS),
        client=None,
        messages=SimpleNamespace(custom_seek_seconds=30),
        _select_youtube_variant=lambda: called.append(True),
    )
    LiteFrame._dispatch(frame, Action.SELECT_YOUTUBE_VARIANT)
    assert called == [True]
