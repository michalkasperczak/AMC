"""Explicit jump inputs and contextual command parity."""

from amc_wx_lite.seek_input import parse_percent, parse_time
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite import menu_model


def test_jump_input_parsing() -> None:
    assert parse_time("45:21") == 2721
    assert parse_time("1:02:03") == 3723
    assert parse_percent("50%") == 50
    assert parse_percent("100") == 100
    for value in ("", "2:77", "x:02", "1:60:00", "999:59:59"):
        try:
            parse_time(value)
        except ValueError:
            pass
        else:
            raise AssertionError(value)
    for value in ("", "101", "-1", "50.5"):
        try:
            parse_percent(value)
        except ValueError:
            pass
        else:
            raise AssertionError(value)


def test_j_and_shift_j_are_player_only_and_menu_matches() -> None:
    assert resolve(Chord("J"), player_view=True, radio_session=False) is Action.SEEK_INPUT_TIME
    assert resolve(Chord("J", shift=True), player_view=True, radio_session=False) is Action.SEEK_INPUT_PERCENT
    assert resolve(Chord("J"), player_view=False, radio_session=False) is None
    items = [entry for menu in menu_model.build_menus() for entry in menu.items]
    assert any(entry.action is Action.SEEK_INPUT_TIME and entry.shortcut == "J" for entry in items)
    assert any(entry.action is Action.SEEK_INPUT_PERCENT and entry.shortcut == "Shift+J" for entry in items)


def test_jump_dialog_restores_player_focus_after_accept_and_cancel(monkeypatch) -> None:
    from types import SimpleNamespace
    from amc_wx_lite import gui
    from amc_wx_lite.gui import LiteFrame

    calls: list[tuple] = []
    result = {"modal": gui.wx.ID_OK, "value": "2:05"}

    class Dialog:
        def __init__(self, _parent, **kwargs):
            calls.append(("dialog", kwargs["title"], kwargs["field_name"]))

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):
            return result["modal"]

        @property
        def value(self):
            return result["value"]

    monkeypatch.setattr(gui, "RenameItemDialog", Dialog)
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=None),
        _restore_focus_after_dialog=lambda: calls.append(("focus",)),
        _seek_to_input_time=lambda value: calls.append(("time", value)),
        _seek_percent=lambda value: calls.append(("percent", value)),
        announcer=SimpleNamespace(say=lambda text: calls.append(("say", text))),
    )
    LiteFrame._seek_from_input(frame, percent=False)
    assert calls == [("dialog", "Skocz do czasu", "Skocz do czasu"),
                     ("focus",), ("time", 125)]
    calls.clear()
    result.update({"modal": gui.wx.ID_OK, "value": "47%"})
    LiteFrame._seek_from_input(frame, percent=True)
    assert calls == [("dialog", "Skocz do procentu", "Skocz do procentu"),
                     ("focus",), ("percent", 47)]
    calls.clear()
    result["modal"] = gui.wx.ID_CANCEL
    LiteFrame._seek_from_input(frame, percent=True)
    assert calls == [("dialog", "Skocz do procentu", "Skocz do procentu"),
                     ("focus",)]


def test_live_radio_only_offers_percent_of_buffer() -> None:
    from amc_wx_lite.navigation import SessionId
    radio_items = {
        entry.action for menu in menu_model.menus_for_session(SessionId.RADIO)
        for entry in menu.items
    }
    assert Action.SEEK_INPUT_TIME not in radio_items
    assert Action.SEEK_INPUT_PERCENT in radio_items
