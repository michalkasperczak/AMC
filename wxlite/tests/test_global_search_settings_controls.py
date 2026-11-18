"""Exercise the actual General-page block with strict native control APIs.

This is a headless control-contract test, not a Windows/NVDA acceptance test.
Extract only the named block to avoid mocking unrelated audio/settings widgets.
"""
from __future__ import annotations

import ast
from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite import session_order
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.state_store import Options


class _Window:
    def __init__(self, _parent, **kwargs):
        self.label = kwargs.get("label", "")
        self.enabled = True
        self.checked = False

    def Enable(self, enable=True):
        self.enabled = enable

    def SetName(self, value):
        self.name = value

    def SetHelpText(self, value):
        self.help = value

    def SetValue(self, value):
        self.checked = value

    def GetValue(self):
        return self.checked

    def Check(self, _index, value):
        self.checked = value


def _general_source_controls(options):
    filename = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    tree = ast.parse(filename.read_text(encoding="utf-8"))
    dialog = next(node for node in tree.body if isinstance(node, ast.ClassDef)
                  and node.name == "GeneralPlaybackOptionsDialog")
    constructor = next(node for node in dialog.body if isinstance(node, ast.FunctionDef)
                       and node.name == "__init__")
    def assigned(node, name):
        return isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Attribute) and target.attr == name
            for target in node.targets
        )
    start = next(index for index, node in enumerate(constructor.body)
                 if assigned(node, "_global_search_source_ids"))
    end = next(index for index, node in enumerate(constructor.body)
               if index > start and isinstance(node, ast.Assign)
               and any(isinstance(target, ast.Name) and target.id == "custom_seek_label"
                       for target in node.targets))
    frame = SimpleNamespace(_session_order=list(session_order.normalize(options.session_order)))
    context = {
        "self": frame, "options": options, "session_order": session_order,
        "general": object(), "general_layout": SimpleNamespace(Add=lambda *args: None),
        "wx": SimpleNamespace(CheckListBox=_Window, CheckBox=_Window, StaticText=_Window,
                              TOP=1, BOTTOM=2, EXPAND=4, LB_SINGLE=8),
    }
    block = ast.Module(body=constructor.body[start:end], type_ignores=[])
    exec(compile(block, str(filename), "exec"), context)
    # Execute the actual value expression, not a test-owned serializer.
    values_method = next(node for node in dialog.body if isinstance(node, ast.FunctionDef)
                         and node.name == "values")
    mapping = next(node for node in ast.walk(values_method) if isinstance(node, ast.Dict)
                   and any(isinstance(key, ast.Constant) and key.value == "global_search_session_ids"
                           for key in node.keys))
    value = next(value for key, value in zip(mapping.keys, mapping.values)
                 if isinstance(key, ast.Constant) and key.value == "global_search_session_ids")
    def selected():
        return eval(compile(ast.Expression(value), str(filename), "eval"), context)
    return frame, selected


def test_general_source_controls_preserve_checked_disabled_and_unchecked_active_sources():
    active = SessionId.FILES
    disabled = SessionId.RADIOARCHIWISTA
    frame, selected = _general_source_controls(Options(
        session_order=[active.value], global_search_session_ids=[disabled.value],
    ))
    assert frame.global_search_sources[disabled].GetValue() is True
    assert frame.global_search_sources[disabled].enabled is False
    assert "wyłączona" in frame.global_search_sources[disabled].name
    assert frame.global_search_sources[active].GetValue() is False
    assert frame.global_search_sources[active].enabled is True
    assert selected() == [disabled.value]
    frame.global_search_sources[active].SetValue(True)
    assert set(selected()) == {active.value, disabled.value}


def test_saving_old_profile_general_settings_does_not_exclude_temporarily_disabled_sources():
    frame, selected = _general_source_controls(Options(session_order=[SessionId.FILES.value]))
    assert frame.global_search_sources[SessionId.RADIOARCHIWISTA].enabled is False
    assert frame.global_search_sources[SessionId.RADIOARCHIWISTA].GetValue() is True
    assert SessionId.RADIOARCHIWISTA.value in selected()


def test_explicit_empty_global_selection_is_not_replaced_by_default():
    _frame, selected = _general_source_controls(Options(global_search_session_ids=[]))
    assert selected() == []


def test_general_settings_preserve_but_do_not_show_manifest_excluded_preferences(monkeypatch):
    monkeypatch.setattr(session_order, "current_profile", lambda: SimpleNamespace(
        session_ids=(SessionId.FILES.value,),
    ))
    frame, selected = _general_source_controls(Options(
        session_order=[SessionId.FILES.value],
        global_search_session_ids=[SessionId.APPLE_MUSIC.value],
    ))
    assert SessionId.APPLE_MUSIC not in frame.global_search_sources
    assert selected() == [SessionId.APPLE_MUSIC.value]
