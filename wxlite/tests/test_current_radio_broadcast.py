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
    assert item.label.replace("&", "") == "Informacje o bieżącym odtwarzaniu"


def test_alt_d_help_names_information_instead_of_navigation() -> None:
    from amc_wx_lite.shortcuts import describe_action

    description = describe_action(Action.CURRENT_RADIO_BROADCAST_INFORMATION)
    assert description == "Informacje o bieżącym odtwarzaniu"


def test_alt_d_is_not_also_a_top_level_menu_mnemonic() -> None:
    titles = [menu.title for menu in menu_model.build_menus()]
    assert all("&d" not in title.casefold() for title in titles)
    assert "Dźwię&k" in titles


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


def test_radio_alt_d_does_not_shorten_multiline_host_text() -> None:
    spoken: list[str] = []
    complete = (
        "Pierwszy akapit bieżącej audycji.\n\n"
        "Drugi akapit pozostaje w automatycznej wypowiedzi."
    )

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.client = SimpleNamespace(
        current_radio_broadcast_information=lambda: {"text": complete}
    )
    frame.runner = Runner()
    frame.announcer = SimpleNamespace(say=spoken.append)

    frame._announce_current_radio_broadcast_information()

    assert spoken == [complete]


def test_nvda_end_receives_the_same_user_facing_broadcast_text() -> None:
    shown: list[str] = []
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame._last_status = {}
    frame.status_bar = SimpleNamespace(show=shown.append)

    frame._update_native_playback_status({
        "currentBroadcastText": "Program Trzeci, 192 kb/s, Serwis informacyjny",
        "id": "radio:{nie-czytaj}",
        "loadedId": "techniczny-identyfikator",
    })

    assert shown == ["Program Trzeci, 192 kb/s, Serwis informacyjny"]


def test_nvda_end_receives_playback_text_for_file_and_archive() -> None:
    shown: list[str] = []
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame._last_status = {}
    frame.status_bar = SimpleNamespace(show=shown.append)

    for session in ("files", "radioarchiwista"):
        frame._update_native_playback_status({
            "sessionId": session,
            "currentPlaybackText": "Nagranie, 192 kb/s, 44,1 kHz",
            "id": "rarch:rec:nie-czytaj",
        })

    assert shown == ["Nagranie, 192 kb/s, 44,1 kHz"] * 2


def test_host_uses_shared_audio_formatter_and_no_technical_id_in_text() -> None:
    root = Path(__file__).resolve().parents[2]
    host = (
        root
        / "src"
        / "AccessibleMediaController.LiteHost"
        / "LiteEngineHandlers.cs"
    ).read_text(encoding="utf-8")
    method = host[
        host.index("private string BuildCurrentRadioBroadcastText(MediaItem item)") :
        host.index("private object StopTransport(JsonElement args)")
    ]
    assert "AudioParametersFormatter.FormatCompact(item)" in method
    assert "NowPlayingParts.SameValue(item.Title, streamTitle)" in method
    assert "item.Id" not in method


def test_transport_status_reuses_the_alt_d_formatter_for_nvda_end() -> None:
    root = Path(__file__).resolve().parents[2]
    host = (
        root
        / "src"
        / "AccessibleMediaController.LiteHost"
        / "LiteEngineHandlers.cs"
    ).read_text(encoding="utf-8")
    status = host[host.index("private object Status(JsonElement args)") :]
    assert "BuildCurrentRadioBroadcastText(item)" in status
    assert "currentBroadcastText" in status



def test_window_title_names_station_and_track_only_while_radio_plays() -> None:
    from amc_wx_lite.version_info import is_main_window_title

    playing = {
        "engine": "radio",
        "playing": True,
        "title": "Trance - DI.FM Premium",
        "streamTitle": "Artysta - Utwór",
    }
    title = gui.radio_window_title(playing)
    assert title == f"{gui.PRODUCT_NAME} — Trance - DI.FM Premium — Artysta - Utwór"
    # Drugie uruchomienie nadal odnajduje okno po początku tytułu.
    assert is_main_window_title(title)
    assert gui.radio_window_title({**playing, "streamTitle": None}) == (
        f"{gui.PRODUCT_NAME} — Trance - DI.FM Premium"
    )
    assert gui.radio_window_title({**playing, "playing": False}) is None
    assert gui.radio_window_title(None) is None
