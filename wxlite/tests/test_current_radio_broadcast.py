"""Alt+D w Radiu: ten sam zakres i czysta odpowiedz co w glownym AMC."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, menu_model  # noqa: E402
from amc_wx_lite.shortcuts import Action, Chord, resolve  # noqa: E402


def test_alt_d_is_current_broadcast_in_both_radio_views() -> None:
    chord = Chord("D", alt=True)
    assert resolve(
        chord,
        player_view=False,
        radio_session=True,
    ) is Action.CURRENT_RADIO_BROADCAST_INFORMATION
    assert resolve(
        chord,
        player_view=True,
        radio_session=True,
    ) is Action.CURRENT_RADIO_BROADCAST_INFORMATION


def test_alt_d_keeps_podcast_description_in_podcast_session() -> None:
    assert resolve(
        Chord("D", alt=True),
        player_view=False,
        radio_session=False,
        podcast_session=True,
    ) is Action.SHOW_PODCAST_DESCRIPTION


def test_radio_menu_exposes_alt_d_without_a_window_accelerator() -> None:
    radio = next(menu for menu in menu_model.build_menus() if "Radio" in menu.title)
    item = next(
        entry
        for entry in radio.items
        if entry.action is Action.CURRENT_RADIO_BROADCAST_INFORMATION
    )
    assert item.shortcut == "Alt+D"
    assert item.needs_radio_session
    assert not item.accelerator


def test_gui_speaks_only_the_host_user_facing_text() -> None:
    spoken: list[str] = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.client = SimpleNamespace(
        current_radio_broadcast_information=lambda: {
            "text": "Program Trzeci, 192 kb/s, Serwis informacyjny",
            "technicalId": "radio:{nie-czytaj}",
        }
    )
    frame.runner = Runner()
    frame.announcer = SimpleNamespace(say=spoken.append)

    frame._announce_current_radio_broadcast_information()

    assert spoken == ["Program Trzeci, 192 kb/s, Serwis informacyjny"]


def test_host_uses_shared_audio_formatter_and_no_technical_id_in_text() -> None:
    root = Path(__file__).resolve().parents[2]
    host = (
        root
        / "src"
        / "AccessibleMediaController.LiteHost"
        / "LiteEngineHandlers.cs"
    ).read_text(encoding="utf-8")
    method = host[
        host.index("private object CurrentRadioBroadcastInformation()") :
        host.index("private object StopAll()")
    ]
    assert "AudioParametersFormatter.FormatCompact(item)" in method
    assert "NowPlayingParts.SameValue(item.Title, streamTitle)" in method
    assert "item.Id" not in method

